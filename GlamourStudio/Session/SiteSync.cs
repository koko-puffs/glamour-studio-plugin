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
    // (Connections let go of after ten minutes, so a change of the site's address is picked up.)
    private readonly HttpClient http = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) }) { Timeout = Timeout.InfiniteTimeSpan };
    private readonly CancellationTokenSource stopping = new();
    private readonly ConcurrentQueue<Action> answers = new();

    private int versionSeen = -1;

    /// <summary>Goes up with each link: an answer for an earlier one is let go.</summary>
    private int generation;

    /// <summary>Sent looks done with here, kept off the list until a read of it no longer has them.</summary>
    private readonly HashSet<string> doneWith = new();

    /// <summary>Sent looks done with whose telling the site failed: told again after the next read of the list.</summary>
    private readonly HashSet<string> removeAgain = new();

    /// <summary>Goes up with each read of what you can wear: only the latest one's answer is taken.</summary>
    private int wearableAsked;

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

    /// <summary>Characters whose answer is on its way to the site, and the answer sent (not told twice at once).</summary>
    private readonly Dictionary<string, bool> telling = new(StringComparer.Ordinal);

    /// <summary>
    /// When each character was last answered here, or its answer confirmed by the site (a count, not a time):
    /// a read of the characters begun before then is older than your answer, and doesn't undo it.
    /// </summary>
    private readonly Dictionary<string, int> touched = new(StringComparer.Ordinal);
    private int touches;

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
        removeAgain.Clear();
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

    /// <summary>The site said the token's no good to another of the plugin's calls (a shot's): link again.</summary>
    public void TokenWasRefused()
    {
        if (TokenRefused) return;
        TokenRefused = true;
        Version++;
    }

    /// <summary>"Not now": not asked about this character again until the plugin loads again (nothing's sent meanwhile).</summary>
    public void NotNow(string key)
    {
        notNow.Add(key);
        Version++;
    }

    /// <summary>
    /// Your answer for a character: kept here at once, and told to the site (yes: its name and world go with
    /// it, and its closet follows; no: the site lets go of what it had of it). Until the site confirms it,
    /// your answer stands over what the site had: nothing of the character is sent meanwhile, and it's told
    /// again until the site hears it.
    /// </summary>
    public void Answer(string key, string name, string world, bool send)
    {
        if (!config.Characters.TryGetValue(key, out var k)) config.Characters[key] = k = new KnownCharacter();
        if (name.Length > 0) k.Name = name;
        if (world.Length > 0) k.World = world;
        k.Send = send;
        k.AnsweredAt = DateTime.UtcNow;
        k.Pending = true;
        config.Save();
        // What the site had is the old answer: let go of, so the step is to tell it (nothing's sent meanwhile).
        siteChoices?.Remove(key);
        Touch(key);
        notNow.Remove(key);
        Version++;
        Tell(key, send);
    }

    private void Touch(string key) => touched[key] = ++touches;

    /// <summary>Tells the site your answer for a character (once at a time each; a newer answer is told when it's back).</summary>
    private void Tell(string key, bool send)
    {
        if (!Linked || telling.ContainsKey(key)) return;
        telling[key] = send;
        var gen = generation;
        var k = config.Characters.TryGetValue(key, out var known) ? known : null;
        var me = wardrobe.Current?.Key == key ? wardrobe.Current : null;
        var name = me?.Name is { Length: > 0 } n ? n : k?.Name ?? string.Empty;
        var world = me?.World is { Length: > 0 } w ? w : k?.World ?? string.Empty;
        Run((t, c) => SiteLink.CharacterAsync(http, t, key, name, world, send, c), r =>
        {
            telling.Remove(key);
            var mine = config.Characters.TryGetValue(key, out var kc) ? kc : null;
            // Answered otherwise while it was on its way: this reply is for the old answer, and the new one is told.
            if (mine?.Send is { } want && want != send)
            {
                Tell(key, want);
                return;
            }
            if (!r.Ok)
            {
                TokenRefused |= r.TokenRefused;
                // Tried again in a minute.
                nextTell = DateTime.UtcNow + TimeSpan.FromMinutes(1);
                return;
            }
            if (r.Value != (send ? SendChoice.Yes : SendChoice.No) && r.Value != SendChoice.No)
            {
                // The site didn't take the answer (it still has another): your answer stands, nothing's sent, and it's told again later.
                Services.Log.Warning($"Glamour Studio: the site kept \"{Consent.Key(r.Value)}\" for a character told \"{(send ? "yes" : "no")}\"");
                nextTell = DateTime.UtcNow + TimeSpan.FromMinutes(15);
                return;
            }
            // The site has it now (or said no to a yes: so it is here too).
            siteChoices ??= new Dictionary<string, SendChoice>(StringComparer.Ordinal);
            siteChoices[key] = r.Value;
            Touch(key);
            var sends = r.Value == SendChoice.Yes;
            if (mine != null && (mine.Pending || mine.Send != sends))
            {
                mine.Pending = false;
                mine.Send = sends;
                config.Save();
            }
            if (sends && wardrobe.Current?.Key == key)
            {
                // Its closet goes now, and its plans and what it can wear are read.
                versionSent = -1;
                lastSent = string.Empty;
                nextAutoSync = nextPlans = nextWearable = DateTime.MinValue;
                plansFor = null;
            }
        }, () =>
        {
            if (gen != generation) return;
            telling.Remove(key);
            nextTell = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        });
    }

    /// <summary>What the site does with each of your characters (read when linking, changing character, and now and then).</summary>
    public void RefreshCharacters(bool windowOpen = false)
    {
        if (!Linked) return;
        nextCharacters = DateTime.UtcNow + (windowOpen ? CharactersOpen : CharactersClosed);
        var asked = touches;
        Run((t, c) => SiteLink.CharactersAsync(http, t, c), r =>
        {
            if (!r.Ok)
            {
                TokenRefused |= r.TokenRefused;
                // A passing trouble: read again in a minute (nothing's sent or asked until it is).
                if (r.Hint is "offline" or "wait" or "server")
                    nextCharacters = Min(nextCharacters, DateTime.UtcNow + TimeSpan.FromMinutes(1));
                return;
            }
            var read = r.Value!;
            // Your answer the site hasn't confirmed, or one answered or confirmed since this was read: it stands,
            // not what was read (the site's old answer).
            var kept = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (key, k) in config.Characters)
                if (k.Pending) kept.Add(key);
            foreach (var (key, at) in touched)
                if (at > asked) kept.Add(key);
            foreach (var key in kept)
            {
                if (siteChoices != null && siteChoices.TryGetValue(key, out var was)) read[key] = was;
                else read.Remove(key);
            }
            siteChoices = read;
            // Else the site's word wins: what you changed there is your answer here too.
            var changed = false;
            foreach (var (key, choice) in siteChoices)
            {
                if (choice == SendChoice.Ask || kept.Contains(key) || !config.Characters.TryGetValue(key, out var k)) continue;
                var send = choice == SendChoice.Yes;
                if (k.Send == send) continue;
                k.Send = send;
                changed = true;
            }
            if (changed) config.Save();
        });
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

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
        if (now >= nextTell)
        {
            if (current != null)
            {
                var step = StepFor(current);
                if (step == ConsentStep.TellYes) Tell(current.Key, true);
                else if (step == ConsentStep.TellNo) Tell(current.Key, false);
            }
            // Any character's the site hasn't confirmed (answered in Settings for another, say).
            foreach (var (key, k) in config.Characters)
            {
                if (k.Pending && k.Send is { } send && !telling.ContainsKey(key)) Tell(key, send);
            }
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
        var fingerprint = Wardrobe.Fingerprint(closet, read, character, current.Key);
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
        Run((t, c) => SiteLink.SyncAsync(http, t, args, c), r =>
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
        Run((t, c) => SiteLink.PlansAsync(http, t, key, c), r =>
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
        var key = me?.Key;
        var asked = ++wearableAsked;
        Run((t, c) => SiteLink.WearableAsync(http, t, character, c), r =>
        {
            if (!r.Ok) TokenRefused |= r.TokenRefused;
            // Only the latest read's, and only while you're on the character it was read for (one for the
            // character before can come back after a change).
            else if (asked == wearableAsked && wardrobe.Current?.Key == key) Wearable = r.Value!;
        });
    }

    public void RefreshLibrary()
    {
        if (!Linked || LibraryBusy) return;
        LibraryBusy = true;
        nextLibrary = DateTime.UtcNow + LibraryEvery;
        Version++;
        RefreshPlans();
        Run((t, c) => SiteLink.LooksAsync(http, t, c), r =>
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
                // Read again soon only for a passing trouble: each read counts against the token's calls an hour,
                // so one that would fail the same way waits its usual ten minutes (a quarter hour when there were too many).
                nextLibrary = DateTime.UtcNow + (r.Hint switch
                {
                    "offline" or "wait" or "server" => TimeSpan.FromMinutes(1),
                    "rate" => TimeSpan.FromMinutes(15),
                    _ => LibraryEvery,
                });
            }
        });
    }

    /// <summary>Everything read from the site again now: your looks, list and plans, what you can wear, the looks sent to you, your characters.</summary>
    public void RefreshAll()
    {
        if (!Linked) return;
        RefreshLibrary();
        RefreshWearable();
        RefreshInbox();
        RefreshCharacters(true);
        Version++;
    }

    public void RefreshInbox()
    {
        if (!Linked) return;
        Run((t, c) => SiteLink.InboxAsync(http, t, c), r =>
        {
            if (r.Ok)
            {
                // One done with here stays off, until the site's list no longer has it.
                var ids = r.Value!.Select(s => s.Id).ToList();
                doneWith.IntersectWith(ids);
                Inbox = r.Value!.Where(s => !doneWith.Contains(s.Id)).ToList();
                // One the site wasn't told of (the call failed): told again, while its list still has it.
                removeAgain.IntersectWith(ids);
                foreach (var id in removeAgain.ToList())
                {
                    removeAgain.Remove(id);
                    TellDone(id);
                }
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
        TellDone(sentId);
    }

    /// <summary>Tells the site a sent look's done with; should that fail, it's told again after the next read of the list.</summary>
    private void TellDone(string sentId)
    {
        var gen = generation;
        Run((t, c) => SiteLink.InboxRemoveAsync(http, t, sentId, c), r =>
        {
            TokenRefused |= r.TokenRefused;
            if (!r.Ok && !r.TokenRefused) removeAgain.Add(sentId);
        }, () =>
        {
            if (gen == generation) removeAgain.Add(sentId);
        });
    }

    /// <summary>Keeps an outfit as a new private look of yours; <paramref name="done"/> hears how it went (framework thread).</summary>
    public void SaveLook(string name, Body body, IReadOnlyDictionary<Slot, Piece> pieces, Action<SiteReply<SavedLook>> done)
    {
        if (!Linked)
        {
            done(new SiteReply<SavedLook>(null, "Link your account first (Settings → Account).", "token"));
            return;
        }
        var kept = pieces.ToDictionary(p => p.Key, p => p.Value);
        Run((t, c) => SiteLink.SaveLookAsync(http, t, name, body, kept, c), r =>
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
        var asked = pieces.ToList();
        Run((t, c) => SiteLink.WishAddAsync(http, t, asked, c), r =>
        {
            TokenRefused |= r.TokenRefused;
            if (r.Ok)
            {
                var wishes = new HashSet<uint>(Library.Wishes);
                foreach (var p in asked) wishes.Add(LookJson.BaseId(p.Item));
                Library = Library with { Wishes = wishes };
                Version++;
                nextLibrary = DateTime.MinValue;
            }
            done(r);
        }, () => done(new SiteReply<int>(0, "It didn't go through (the link changed, or the call failed). Try again.")));
    }

    /// <summary>
    /// Takes pieces off your shopping list: off the plugin's copy at once (put back should the site
    /// say no); <paramref name="done"/> hears how it went (framework thread).
    /// </summary>
    public void RemoveWishes(IReadOnlyList<uint> items, Action<SiteReply<int>> done)
    {
        if (!Linked)
        {
            done(new SiteReply<int>(0, "Link your account first (Settings → Account).", "token"));
            return;
        }
        var was = Library.Wishes;
        var gone = items.Select(LookJson.BaseId).ToHashSet();
        Library = Library with { Wishes = was.Where(w => !gone.Contains(w)).ToHashSet() };
        Version++;
        void Undo()
        {
            var back = new HashSet<uint>(Library.Wishes);
            back.UnionWith(was.Where(gone.Contains));
            Library = Library with { Wishes = back };
            Version++;
        }
        var taken = items.ToList();
        Run((t, c) => SiteLink.WishRemoveAsync(http, t, taken, c), r =>
        {
            TokenRefused |= r.TokenRefused;
            if (!r.Ok) Undo();
            else nextLibrary = DateTime.MinValue;
            done(r);
        }, () =>
        {
            Undo();
            done(new SiteReply<int>(0, "It didn't go through (the link changed, or the call failed). Try again."));
        });
    }

    /// <summary>Puts a piece on one of your looks; <paramref name="done"/> hears the look's name (framework thread). Your looks are read again after.</summary>
    public void PutOnLook(string lookId, Slot slot, Piece piece, Action<SiteReply<string>> done)
    {
        if (!Linked)
        {
            done(new SiteReply<string>(null, "Link your account first (Settings → Account).", "token"));
            return;
        }
        Run((t, c) => SiteLink.LookPutAsync(http, t, lookId, slot, piece, c), r =>
        {
            TokenRefused |= r.TokenRefused;
            if (r.Ok) nextLibrary = DateTime.MinValue;
            done(r);
        }, () => done(new SiteReply<string>(null, "It didn't go through (the link changed, or the call failed). Try again.")));
    }

    /// <summary>
    /// Makes a call with the token and the stop read here (framework thread), on a worker: its own work before
    /// it waits on the site (the body written, a read of your wardrobe's ~200 KB; the connection found) stays
    /// off the framework thread. Its answer comes back to <see cref="Tick"/>.
    /// </summary>
    /// <param name="dropped">Told when the answer isn't taken (the link changed meanwhile, or the call threw), so nothing waits on it for good.</param>
    private void Run<T>(Func<string, CancellationToken, Task<T>> call, Action<T> then, Action? dropped = null)
    {
        var made = generation;
        var token = config.SiteToken;
        var cancel = stopping.Token;
        Task.Run(() => call(token, cancel)).ContinueWith(t =>
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
