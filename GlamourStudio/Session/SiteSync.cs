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
    private static readonly TimeSpan InboxClosed = TimeSpan.FromMinutes(5);

    private readonly Configuration config;
    private readonly WardrobeReader wardrobe;
    private readonly HttpClient http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly CancellationTokenSource stopping = new();
    private readonly ConcurrentQueue<Action> answers = new();

    private int versionSeen = -1;
    private DateTime changedAt;
    private DateTime nextAutoSync;
    private string lastSent = string.Empty;
    private DateTime nextLibrary;
    private DateTime nextInbox;

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
        TokenRefused = false;
        Library = Library.Empty;
        LibraryAt = null;
        LibraryProblem = null;
        Inbox = Array.Empty<SentLook>();
        LastSync = null;
        LastSyncAt = null;
        SyncProblem = null;
        lastSent = string.Empty;
        nextLibrary = nextInbox = nextAutoSync = DateTime.MinValue;
        Version++;
    }

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
        if (now - changedAt >= Settle && now >= nextAutoSync && wardrobe.Current != null) SyncNow(auto: true);
    }

    /// <summary>Sends your wardrobe now (or, <paramref name="auto"/>, only when it changed since the last send).</summary>
    public void SyncNow(bool auto = false)
    {
        if (!Linked || Syncing) return;
        var current = wardrobe.Current;
        var characters = wardrobe.All;
        if (current == null && characters.Count == 0) return;
        var (closet, read) = Wardrobe.Closet(characters, config.AllCharacters ? null : current?.Key);
        var character = config.SendPlates ? current : null;
        var fingerprint = Wardrobe.Fingerprint(closet, read, character);
        nextAutoSync = DateTime.UtcNow + AutoSyncGap;
        if (auto && fingerprint == lastSent) return;
        JsonObject args;
        if (character != null) args = Wardrobe.SyncArgs(closet, read, character);
        else
        {
            args = Wardrobe.SyncArgs(closet, read, null);
            // Plates off: still says whose read it is, so the site can show when it was read.
            if (current != null) args["p_character"] = new JsonObject { ["key"] = current.Key, ["name"] = current.Name, ["world"] = current.World };
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

    public void RefreshLibrary()
    {
        if (!Linked || LibraryBusy) return;
        LibraryBusy = true;
        nextLibrary = DateTime.UtcNow + LibraryEvery;
        Version++;
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
            if (r.Ok) Inbox = r.Value!;
            else TokenRefused |= r.TokenRefused;
        });
    }

    /// <summary>A look sent from the site is done with: off the list here at once, and on the site.</summary>
    public void DoneWith(string sentId)
    {
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

    private void Run<T>(Task<T> call, Action<T> then) =>
        call.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully) answers.Enqueue(() => then(t.Result));
            else
            {
                if (t.Exception != null) Services.Log.Warning(t.Exception, "Glamour Studio's call to the site failed");
                // (The calls don't throw: should one, nothing's left waiting on it.)
                answers.Enqueue(() => Syncing = LibraryBusy = false);
            }
        }, TaskScheduler.Default);

    public void Dispose()
    {
        stopping.Cancel();
        http.Dispose();
        stopping.Dispose();
    }
}
