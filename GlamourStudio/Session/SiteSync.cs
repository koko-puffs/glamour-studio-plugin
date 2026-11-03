using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using GlamourStudio.Core;
using GlamourStudio.Game;

namespace GlamourStudio.Session;

/// <summary>
/// The plugin and the site, kept in step in the background: your closet (what <see cref="WardrobeReader"/>
/// read) sent when it changes, at most every three minutes and once it's settled for ten seconds; your
/// looks and the ones you love read when the window opens (and every ten minutes while it's open); the
/// looks sent from the site looked for every minute while the window's open, every five otherwise.
/// Calls run off the framework thread; their answers come back to it (<see cref="Tick"/>), where the
/// window reads them. Nothing is sent before the plugin's linked, and nothing of a character before you've
/// said yes to sending it (<see cref="Consent"/>: asked once per character, kept here and on the site).
/// </summary>
public sealed class SiteSync : IDisposable
{
    private static readonly TimeSpan AutoSyncGap = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LibraryEvery = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan InboxOpen = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan InboxClosed = TimeSpan.FromMinutes(15);

    private readonly Configuration config;
    private readonly WardrobeReader wardrobe;
    private readonly HttpClient http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly CancellationTokenSource stopping = new();
    private readonly ConcurrentQueue<Action> answers = new();

    private int versionSeen = -1;

    /// <summary>Goes up with each link: an answer for an earlier one is let go.</summary>
    private int generation;

    /// <summary>Sent looks done with here, kept off the list until a read of it no longer has them.</summary>
    private readonly HashSet<string> doneWith = new();

    /// <summary>The wardrobe's version last sent (nothing's worked out again until it moves).</summary>
    private int versionSent = -1;
    private DateTime changedAt;
    private DateTime nextAutoSync;
    private string lastSent = string.Empty;
    private DateTime nextLibrary;
    private DateTime nextInbox;

    /// <summary>Whether the plugin's window was open at the last tick.</summary>
    private bool windowWasOpen;
    private DateTime nextPlans;

    /// <summary>What the site does with each character's data (null: not read yet since linking).</summary>
    private Dictionary<string, SendChoice>? siteChoices;
    private DateTime nextCharacters;
    private DateTime nextTell;
    private string? charactersFor;

    /// <summary>Characters whose answer is on its way to the site (not told twice at once).</summary>
    private readonly HashSet<string> telling = new();

    /// <summary>Characters you said "Not now" for, this session: not asked again until the plugin loads again.</summary>
    private readonly HashSet<string> notNow = new();

    private static readonly TimeSpan CharactersOpen = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CharactersClosed = TimeSpan.FromMinutes(30);

    public SiteSync(Configuration config, WardrobeReader wardrobe)
    {
        this.config = config;
        this.wardrobe = wardrobe;
        var version = typeof(SiteSync).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"GlamourStudioPlugin/{version}");
    }

    public bool Linked => SiteLink.LooksRight(config.SiteToken);

    /// <summary>The site said the token's no good (revoked): link again.</summary>
    public bool TokenRefused { get; private set; }

    // What came back, read by the window (framework thread).
    public Library Library { get; private set; } = Library.Empty;

    /// <summary>The public looks you own every piece of (Wear now), as last read.</summary>
    public IReadOnlyList<Look> Wearable { get; private set; } = Array.Empty<Look>();

    /// <summary>The plate plans of the character they were read for (the one you're on), by plate.</summary>
    public IReadOnlyList<PlannedPlate> Plans { get; private set; } = Array.Empty<PlannedPlate>();

    /// <summary>The character <see cref="Plans"/> are for.</summary>
    private string? plansFor;

    /// <summary>The HTTP client the plugin's calls share (the shots' too).</summary>
    public HttpClient Http => http;

    /// <summary>The planned look for a plate of the character you're on, or null.</summary>
    public Look? PlanFor(int plate) =>
        plansFor != null && plansFor == wardrobe.Current?.Key ? Plans.FirstOrDefault(p => p.Plate == plate)?.Look : null;

    public DateTime? LibraryAt { get; private set; }

    public bool LibraryBusy { get; private set; }

    public string? LibraryProblem { get; private set; }

    public IReadOnlyList<SentLook> Inbox { get; private set; } = Array.Empty<SentLook>();

    public SyncResult? LastSync { get; private set; }

    public DateTime? LastSyncAt { get; private set; }

    public bool Syncing { get; private set; }

    public string? SyncProblem { get; private set; }

    /// <summary>Changed whenever something above did (for the window to notice).</summary>
    public int Version { get; private set; }

    /// <summary>The token was changed (linked, unlinked): start again.</summary>
    public void Relinked()
    {
        // Answers still on their way are the old token's: let go of them.
        generation++;
        Syncing = false;
        LibraryBusy = false;
        doneWith.Clear();
        TokenRefused = false;
        Library = Library.Empty;
        LibraryAt = null;
        LibraryProblem = null;
        Inbox = Array.Empty<SentLook>();
        Wearable = Array.Empty<Look>();
        Plans = Array.Empty<PlannedPlate>();
        plansFor = null;
        siteChoices = null;
        charactersFor = null;
        telling.Clear();
        LastSync = null;
        LastSyncAt = null;
        SyncProblem = null;
        lastSent = string.Empty;
        versionSent = -1;
        nextLibrary = nextInbox = nextAutoSync = nextWearable = nextPlans = nextCharacters = nextTell = DateTime.MinValue;
        Version++;
    }

    // ---------------------------------------------------------------------------------------------
    // Asking about each character.

    /// <summary>What the site does with a character's data, as last read (null: not read yet).</summary>
    public SendChoice? SiteChoice(string key) =>
        siteChoices == null ? null : siteChoices.TryGetValue(key, out var c) ? c : SendChoice.Ask;

    /// <summary>Your answer for a character, kept here (null: not asked).</summary>
    public bool? MyAnswer(string key) => config.Characters.TryGetValue(key, out var k) ? k.Send : null;

    /// <summary>What to do about a character's data now.</summary>
    public ConsentStep StepFor(CharacterWardrobe? c) => c == null ? ConsentStep.Wait : Consent.Decide(MyAnswer(c.Key), SiteChoice(c.Key));

    /// <summary>The site takes this character's data: its closet may be sent, and its key said.</summary>
    public bool Sends(CharacterWardrobe? c) => Linked && StepFor(c) == ConsentStep.Send;

    /// <summary>The character to ask about now (the one you're on, linked, not answered, not put off), or null.</summary>
    public CharacterWardrobe? ToAsk
    {
        get
        {
            var c = wardrobe.Current;
            if (!Linked || TokenRefused || c == null || c.Name.Length == 0 || notNow.Contains(c.Key)) return null;
            return StepFor(c) == ConsentStep.AskPlayer ? c : null;
        }
    }

    /// <summary>"Not now": not asked about this character again until the plugin loads again (nothing's sent meanwhile).</summary>
    public void NotNow(string key)
    {
        notNow.Add(key);
        Version++;
    }

    /// <summary>
    /// Your answer for a character: kept here at once, and told to the site (yes: its name and world go with
    /// it, and its closet follows; no: the site lets go of what it had of it).
    /// </summary>
    public void Answer(string key, string name, string world, bool send)
    {
        if (!config.Characters.TryGetValue(key, out var k)) config.Characters[key] = k = new KnownCharacter();
        if (name.Length > 0) k.Name = name;
        if (world.Length > 0) k.World = world;
        k.Send = send;
        k.AnsweredAt = DateTime.UtcNow;
        config.Save();
        notNow.Remove(key);
        Version++;
        Tell(key, send);
    }

    /// <summary>Tells the site your answer for a character (once at a time each).</summary>
    private void Tell(string key, bool send)
    {
        if (!Linked || !telling.Add(key)) return;
        var k = config.Characters.TryGetValue(key, out var known) ? known : null;
        var me = wardrobe.Current?.Key == key ? wardrobe.Current : null;
        var name = me?.Name is { Length: > 0 } n ? n : k?.Name ?? string.Empty;
        var world = me?.World is { Length: > 0 } w ? w : k?.World ?? string.Empty;
        Run(SiteLink.CharacterAsync(http, config.SiteToken, key, name, world, send, stopping.Token), r =>
        {
            telling.Remove(key);
            if (r.Ok)
            {
                siteChoices ??= new Dictionary<string, SendChoice>(StringComparer.Ordinal);
                siteChoices[key] = r.Value;
                if (r.Value == SendChoice.Yes && wardrobe.Current?.Key == key)
                {
                    // Its closet goes now, and its plans and what it can wear are read.
                    versionSent = -1;
                    lastSent = string.Empty;
                    nextAutoSync = nextPlans = nextWearable = DateTime.MinValue;
                    plansFor = null;
                }
            }
            else
            {
                TokenRefused |= r.TokenRefused;
                // Tried again in a minute.
                nextTell = DateTime.UtcNow + TimeSpan.FromMinutes(1);
            }
        }, () => telling.Remove(key));
    }

    /// <summary>What the site does with each of your characters (read when linking, changing character, and now and then).</summary>
    public void RefreshCharacters(bool windowOpen = false)
    {
        if (!Linked) return;
        nextCharacters = DateTime.UtcNow + (windowOpen ? CharactersOpen : CharactersClosed);
        Run(SiteLink.CharactersAsync(http, config.SiteToken, stopping.Token), r =>
        {
            if (!r.Ok)
            {
                TokenRefused |= r.TokenRefused;
                return;
            }
            siteChoices = r.Value!;
            // The site's word wins: what you changed there is your answer here too.
            var changed = false;
            foreach (var (key, choice) in siteChoices)
            {
                if (choice == SendChoice.Ask || !config.Characters.TryGetValue(key, out var k)) continue;
                var send = choice == SendChoice.Yes;
                if (k.Send == send) continue;
                k.Send = send;
                changed = true;
            }
            if (changed) config.Save();
        });
    }

    /// <summary>The window opened: what's sent from the site is looked for at once.</summary>
    public void Wake() => nextInbox = DateTime.MinValue;

    /// <summary>Framework thread: takes the answers in, and sends or asks what's due.</summary>
    public void Tick(bool windowOpen)
    {
        while (answers.TryDequeue(out var a))
        {
            try
            {
                a();
            }
            catch (Exception ex)
            {
                Services.Log.Warning(ex, "Glamour Studio couldn't take the site's answer in");
            }
            Version++;
        }
        if (!Linked || TokenRefused) return;
        var now = DateTime.UtcNow;

        // What the site does with your characters: now and then, and at once on another character.
        var current = wardrobe.Current;
        if (now >= nextCharacters || (current != null && current.Key != charactersFor))
        {
            // Another character: what it can wear is read again too.
            if (current != null && current.Key != charactersFor) nextWearable = DateTime.MinValue;
            charactersFor = current?.Key;
            RefreshCharacters(windowOpen);
        }
        // An answer given here the site doesn't know yet (given while unlinked, or a call that failed): told.
        if (current != null && now >= nextTell)
        {
            var step = StepFor(current);
            if (step == ConsentStep.TellYes) Tell(current.Key, true);
            else if (step == ConsentStep.TellNo) Tell(current.Key, false);
        }

        if (windowOpen && now >= nextLibrary && !LibraryBusy) RefreshLibrary();
        if (windowOpen && now >= nextWearable) RefreshWearable();
        // Another character: its plans (only for one the site takes).
        if (Sends(current) && current!.Key != plansFor && now >= nextPlans)
        {
            nextPlans = now + TimeSpan.FromSeconds(30);
            RefreshPlans();
        }
        // The window just opened (by any way: /gstudio, a panel's arrow, the plugin list): what's
        // been sent is read at once, not at the next quarter hour (a recording saw a look sent from
        // the site never turn up, the last one shown instead).
        if (windowOpen && !windowWasOpen) nextInbox = DateTime.MinValue;
        windowWasOpen = windowOpen;
        if (now >= nextInbox)
        {
            nextInbox = now + (windowOpen ? InboxOpen : InboxClosed);
            RefreshInbox();
        }

        if (!config.AutoSync || Syncing) return;
        var v = wardrobe.Version;
        if (v != versionSeen)
        {
            versionSeen = v;
            changedAt = now;
        }
        if (now - changedAt >= Settle && now >= nextAutoSync && Sends(wardrobe.Current) && v != versionSent) SyncNow(auto: true);
    }

    /// <summary>Sends your wardrobe now (or, <paramref name="auto"/>, only when it changed since the last send).</summary>
    public void SyncNow(bool auto = false)
    {
        if (!Linked || Syncing) return;
        // The character you're on, only: the site keeps each character's reads apart and makes your
        // closet from all of them, so another PC or character never takes this one's pieces off.
        var current = wardrobe.Current;
        if (current == null) return;
        // Nothing of a character you haven't said yes to (asked again if you put it off).
        if (!Sends(current))
        {
            if (!auto) notNow.Remove(current.Key);
            return;
        }
        // First: should what follows throw, it isn't tried again every frame.
        nextAutoSync = DateTime.UtcNow + AutoSyncGap;
        var (closet, read) = Wardrobe.Closet(new[] { current }, current.Key);
        var character = config.SendPlates ? current : null;
        var version = wardrobe.Version;
        var fingerprint = Wardrobe.Fingerprint(closet, read, character);
        if (auto && fingerprint == lastSent)
        {
            versionSent = version;
            return;
        }
        JsonObject args;
        if (character != null) args = Wardrobe.SyncArgs(closet, read, character);
        else
        {
            args = Wardrobe.SyncArgs(closet, read, null);
            // Plates off: still says whose read it is (the site keeps each character's closet apart).
            args["p_character"] = new JsonObject { ["key"] = current.Key, ["name"] = current.Name, ["world"] = current.World };
        }
        Syncing = true;
        SyncProblem = null;
        Version++;
        var token = config.SiteToken;
        Run(SiteLink.SyncAsync(http, token, args, stopping.Token), r =>
        {
            Syncing = false;
            if (r.Ok)
            {
                LastSync = r.Value;
                LastSyncAt = DateTime.UtcNow;
                lastSent = fingerprint;
                versionSent = version;
                if (r.Value!.WishesGot is { Count: > 0 } got)
                {
                    // Off the list here too, before the looks are read again.
                    var left = new HashSet<uint>(Library.Wishes);
                    left.ExceptWith(got);
                    Library = Library with { Wishes = left };
                    // (A read of your looks already on its way could bring them back: read again after it.)
                    nextLibrary = DateTime.MinValue;
                    LastWishesGot = (got, DateTime.UtcNow);
                    WishesGot?.Invoke(got);
                }
                // What you could wear now may have changed with it.
                if (r.Value.Added + r.Value.Removed > 0) nextWearable = DateTime.MinValue;
            }
            else
            {
                SyncProblem = r.Why;
                TokenRefused |= r.TokenRefused;
                // The site says no to this character (you said so there): not sent again, and so it is here.
                if (r.Hint == "refused")
                {
                    siteChoices ??= new Dictionary<string, SendChoice>(StringComparer.Ordinal);
                    siteChoices[current.Key] = SendChoice.No;
                    if (config.Characters.TryGetValue(current.Key, out var k) && k.Send != false)
                    {
                        k.Send = false;
                        config.Save();
                    }
                    SyncProblem = null;
                }
                // A passing trouble is tried again by itself in a few minutes.
                if (r.Passing) nextAutoSync = DateTime.UtcNow + AutoSyncGap;
            }
        });
    }

    /// <summary>Pieces a read took off your shopping list (you have them now): the plugin says so.</summary>
    public event Action<IReadOnlyList<uint>>? WishesGot;

    /// <summary>The last pieces a read took off your shopping list, and when.</summary>
    public (IReadOnlyList<uint> Items, DateTime At)? LastWishesGot { get; private set; }

    private DateTime nextWearable;

    /// <summary>Your plans for the character you're on: read again when it changes, or with your looks.</summary>
    public void RefreshPlans()
    {
        var key = wardrobe.Current?.Key;
        if (!Sends(wardrobe.Current) || key == null) return;
        Run(SiteLink.PlansAsync(http, config.SiteToken, key, stopping.Token), r =>
        {
            if (r.Ok)
            {
                Plans = r.Value!;
                plansFor = key;
                // Changed character while it was asked: the new one's are asked for at once.
                if (wardrobe.Current?.Key != key) nextPlans = DateTime.MinValue;
            }
            else TokenRefused |= r.TokenRefused;
        });
    }

    public void RefreshWearable()
    {
        if (!Linked) return;
        nextWearable = DateTime.UtcNow + LibraryEvery;
        // As the character you're on, when the site takes it; else with every character's pieces together.
        var me = wardrobe.Current;
        var character = Sends(me) ? me!.Key : null;
        Run(SiteLink.WearableAsync(http, config.SiteToken, character, stopping.Token), r =>
        {
            if (r.Ok) Wearable = r.Value!;
            else TokenRefused |= r.TokenRefused;
        });
    }

    public void RefreshLibrary()
    {
        if (!Linked || LibraryBusy) return;
        LibraryBusy = true;
        nextLibrary = DateTime.UtcNow + LibraryEvery;
        Version++;
        RefreshPlans();
        Run(SiteLink.LooksAsync(http, config.SiteToken, stopping.Token), r =>
        {
            LibraryBusy = false;
            if (r.Ok)
            {
                Library = r.Value!;
                LibraryAt = DateTime.UtcNow;
                LibraryProblem = null;
            }
            else
            {
                LibraryProblem = r.Why;
                TokenRefused |= r.TokenRefused;
                nextLibrary = DateTime.UtcNow + TimeSpan.FromMinutes(1);
            }
        });
    }

    public void RefreshInbox()
    {
        if (!Linked) return;
        Run(SiteLink.InboxAsync(http, config.SiteToken, stopping.Token), r =>
        {
            if (r.Ok)
            {
                // One done with here stays off, until the site's list no longer has it.
                doneWith.IntersectWith(r.Value!.Select(s => s.Id));
                Inbox = r.Value!.Where(s => !doneWith.Contains(s.Id)).ToList();
            }
            else TokenRefused |= r.TokenRefused;
        });
    }

    /// <summary>A look sent from the site is done with: off the list here at once, and on the site.</summary>
    public void DoneWith(string sentId)
    {
        doneWith.Add(sentId);
        Inbox = Inbox.Where(s => s.Id != sentId).ToList();
        Version++;
        if (!Linked) return;
        Run(SiteLink.InboxRemoveAsync(http, config.SiteToken, sentId, stopping.Token), _ => { });
    }

    /// <summary>Keeps an outfit as a new private look of yours; <paramref name="done"/> hears how it went (framework thread).</summary>
    public void SaveLook(string name, Body body, IReadOnlyDictionary<Slot, Piece> pieces, Action<SiteReply<SavedLook>> done)
    {
        if (!Linked)
        {
            done(new SiteReply<SavedLook>(null, "Link your account first (Settings → Account).", "token"));
            return;
        }
        Run(SiteLink.SaveLookAsync(http, config.SiteToken, name, body, pieces, stopping.Token), r =>
        {
            TokenRefused |= r.TokenRefused;
            // The new look is yours now: your looks are read again.
            if (r.Ok) nextLibrary = DateTime.MinValue;
            done(r);
        }, () => done(new SiteReply<SavedLook>(null, "It didn't go through (the link changed, or the call failed). Try again.")));
    }

    /// <summary>
    /// Puts pieces on your shopping list; <paramref name="done"/> hears how many went on (framework
    /// thread). They're on the list here at once (the site's list is read again after).
    /// </summary>
    public void AddWishes(IReadOnlyList<Piece> pieces, Action<SiteReply<int>> done)
    {
        if (!Linked)
        {
            done(new SiteReply<int>(0, "Link your account first (Settings → Account).", "token"));
            return;
        }
        Run(SiteLink.WishAddAsync(http, config.SiteToken, pieces, stopping.Token), r =>
        {
            TokenRefused |= r.TokenRefused;
            if (r.Ok)
            {
                var wishes = new HashSet<uint>(Library.Wishes);
                foreach (var p in pieces) wishes.Add(LookJson.BaseId(p.Item));
                Library = Library with { Wishes = wishes };
                Version++;
                nextLibrary = DateTime.MinValue;
            }
            done(r);
        }, () => done(new SiteReply<int>(0, "It didn't go through (the link changed, or the call failed). Try again.")));
    }

    /// <summary>Puts a piece on one of your looks; <paramref name="done"/> hears the look's name (framework thread). Your looks are read again after.</summary>
    public void PutOnLook(string lookId, Slot slot, Piece piece, Action<SiteReply<string>> done)
    {
        if (!Linked)
        {
            done(new SiteReply<string>(null, "Link your account first (Settings → Account).", "token"));
            return;
        }
        Run(SiteLink.LookPutAsync(http, config.SiteToken, lookId, slot, piece, stopping.Token), r =>
        {
            TokenRefused |= r.TokenRefused;
            if (r.Ok) nextLibrary = DateTime.MinValue;
            done(r);
        }, () => done(new SiteReply<string>(null, "It didn't go through (the link changed, or the call failed). Try again.")));
    }

    /// <param name="dropped">Told when the answer isn't taken (the link changed meanwhile, or the call threw), so nothing waits on it for good.</param>
    private void Run<T>(Task<T> call, Action<T> then, Action? dropped = null)
    {
        var made = generation;
        call.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
                answers.Enqueue(() =>
                {
                    if (made == generation) then(t.Result);
                    else dropped?.Invoke();
                });
            else
            {
                if (t.Exception != null) Services.Log.Warning(t.Exception, "Glamour Studio's call to the site failed");
                // (The calls don't throw: should one, nothing's left waiting on it.)
                answers.Enqueue(() =>
                {
                    if (made == generation) Syncing = LibraryBusy = false;
                    dropped?.Invoke();
                });
            }
        }, TaskScheduler.Default);
    }

    public void Dispose()
    {
        stopping.Cancel();
        http.Dispose();
        stopping.Dispose();
    }
}
