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
/// window reads them. Nothing is sent before the plugin's linked.
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
    private DateTime nextPlans;

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
        LastSync = null;
        LastSyncAt = null;
        SyncProblem = null;
        lastSent = string.Empty;
        versionSent = -1;
        nextLibrary = nextInbox = nextAutoSync = nextWearable = nextPlans = DateTime.MinValue;
        Version++;
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

        if (windowOpen && now >= nextLibrary && !LibraryBusy) RefreshLibrary();
        if (windowOpen && now >= nextWearable) RefreshWearable();
        // Another character: its plans.
        if (wardrobe.Current?.Key is { } me && me != plansFor && now >= nextPlans)
        {
            nextPlans = now + TimeSpan.FromSeconds(30);
            RefreshPlans();
        }
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
        if (now - changedAt >= Settle && now >= nextAutoSync && wardrobe.Current != null && v != versionSent) SyncNow(auto: true);
    }

    /// <summary>Sends your wardrobe now (or, <paramref name="auto"/>, only when it changed since the last send).</summary>
    public void SyncNow(bool auto = false)
    {
        if (!Linked || Syncing) return;
        // The character you're on, only: the site keeps each character's reads apart and makes your
        // closet from all of them, so another PC or character never takes this one's pieces off.
        var current = wardrobe.Current;
        if (current == null) return;
        var (closet, read) = Wardrobe.Closet(new[] { current }, current.Key);
        var character = config.SendPlates ? current : null;
        var version = wardrobe.Version;
        var fingerprint = Wardrobe.Fingerprint(closet, read, character);
        nextAutoSync = DateTime.UtcNow + AutoSyncGap;
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
                if (r.Value!.WishesGot is { Count: > 0 } got) WishesGot?.Invoke(got);
                // What you could wear now may have changed with it.
                if (r.Value.Added + r.Value.Removed > 0) nextWearable = DateTime.MinValue;
            }
            else
            {
                SyncProblem = r.Why;
                TokenRefused |= r.TokenRefused;
                // A passing trouble is tried again by itself in a few minutes.
                if (r.Passing) nextAutoSync = DateTime.UtcNow + AutoSyncGap;
            }
        });
    }

    /// <summary>Pieces a read took off your shopping list (you have them now): the plugin says so.</summary>
    public event Action<IReadOnlyList<uint>>? WishesGot;

    private DateTime nextWearable;

    /// <summary>Your plans for the character you're on: read again when it changes, or with your looks.</summary>
    public void RefreshPlans()
    {
        var key = wardrobe.Current?.Key;
        if (!Linked || key == null) return;
        Run(SiteLink.PlansAsync(http, config.SiteToken, key, stopping.Token), r =>
        {
            if (r.Ok)
            {
                Plans = r.Value!;
                plansFor = key;
            }
            else TokenRefused |= r.TokenRefused;
        });
    }

    public void RefreshWearable()
    {
        if (!Linked) return;
        nextWearable = DateTime.UtcNow + LibraryEvery;
        Run(SiteLink.WearableAsync(http, config.SiteToken, stopping.Token), r =>
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
        });
    }

    private void Run<T>(Task<T> call, Action<T> then)
    {
        var made = generation;
        call.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
                answers.Enqueue(() =>
                {
                    if (made == generation) then(t.Result);
                });
            else
            {
                if (t.Exception != null) Services.Log.Warning(t.Exception, "Glamour Studio's call to the site failed");
                // (The calls don't throw: should one, nothing's left waiting on it.)
                answers.Enqueue(() =>
                {
                    if (made == generation) Syncing = LibraryBusy = false;
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
