using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GlamourStudio.Core;

/// <summary>
/// What the site said to one of the plugin's calls: its answer, or why not in words (the site's
/// own, written to be shown) with its hint, what to do about it: <c>token</c> link again,
/// <c>rate</c> too many this hour, <c>pieces</c> nothing to keep. The plugin's own: <c>offline</c>
/// (no connection), <c>wait</c> (no answer in time), <c>stopped</c>, <c>answer</c> (an answer it
/// couldn't read), <c>server</c> (the site's trouble, 5xx).
/// </summary>
public sealed record SiteReply<T>(T? Value, string? Why = null, string? Hint = null)
{
    public bool Ok => Why == null;

    /// <summary>The site doesn't know the token (revoked, or never made): link again.</summary>
    public bool TokenRefused => Hint == "token";

    /// <summary>Worth trying again later by itself: no connection, no answer, the site's own trouble, too many.</summary>
    public bool Passing => Hint is "offline" or "wait" or "server" or "rate";

    public SiteReply<TOther> As<TOther>() => new(default, Why, Hint);
}

/// <summary>What the site kept of a read of your wardrobe (<c>gs_plugin_sync</c>).</summary>
/// <param name="WishesGot">The pieces taken off your shopping list: the read shows you have them now.</param>
public sealed record SyncResult(int Total, int Added, int Moved, int Removed, int Plates, DateTimeOffset? ReadAt, IReadOnlyList<uint>? WishesGot = null);

/// <summary>A shot the site added to a look's Worn in game (<c>gs-plugin-shot</c>).</summary>
public sealed record ShotSent(string Id, string LookName);

/// <summary>A look kept on the site from the game (<c>gs_plugin_save_look</c>).</summary>
public sealed record SavedLook(string Id, string Name);

/// <summary>
/// Linking the plugin to your Glamour Studio account, with the token made on the site (Settings →
/// Glamour Studio → In-game plugin), exactly as Housing Studio's and Raid Planner's plugins link. The
/// plugin calls the site's database functions (the contract in the site's <c>…_gs_plugin.sql</c>) with
/// the site's publishable key, as the site itself would; the token, in the body only, says whose
/// account it is. None of the calls throws (a lost connection or no answer in time is said in words),
/// and none is ever made on the game's framework thread. The token is never logged.
/// </summary>
public static class SiteLink
{
    /// <summary>The site's database (Supabase): its address and its publishable key, public by design.</summary>
    public const string SupabaseUrl = "https://upjcxjuuzxujqddtwehn.supabase.co";

    public const string PublishableKey = "sb_publishable_A-5zpDIk5O6xyKTicipcuA_M3J0sGVP";

    public const string SiteUrl = "https://www.kokos-tools.com/glamour-studio";

    /// <summary>Where a token is made: Settings → Glamour Studio → In-game plugin.</summary>
    public const string TokensUrl = "https://www.kokos-tools.com/glamour-studio?settings=glamour-plugin";

    public const string ClosetUrl = "https://www.kokos-tools.com/glamour-studio/closet";

    public static string LookUrl(string id) => $"{SiteUrl}/look/{Uri.EscapeDataString(id)}";

    /// <summary>A look opened in the dressing room (yours to change; someone else's to remix).</summary>
    public static string DressingUrl(string id) => $"{SiteUrl}/dressing-room/{Uri.EscapeDataString(id)}";

    /// <summary>The longest a light read (the inbox) or an action waits for the site.</summary>
    public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(15);

    /// <summary>The longest your looks or a read of your wardrobe waits (they can be big).</summary>
    public static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(40);

    private static readonly Regex Shape = new(@"^gsp_[A-Za-z0-9_-]{43}$", RegexOptions.CultureInvariant);

    /// <summary>A token as pasted, without the spaces, quotes or line breaks that come with a copy.</summary>
    public static string Clean(string? pasted) => (pasted ?? string.Empty).Trim().Trim('"', '\'', '`').Trim();

    /// <summary>Does it look like a token the site makes ("gsp_" and 43 letters)?</summary>
    public static bool LooksRight(string? token) => token != null && Shape.IsMatch(token);

    /// <summary>A token shown without giving it away: "gsp_••••••••3xQz".</summary>
    public static string Masked(string? token) =>
        string.IsNullOrEmpty(token) ? string.Empty
        : token.Length <= 8 ? new string('•', 8)
        : (token.StartsWith("gsp_", StringComparison.Ordinal) ? "gsp_" : string.Empty) + new string('•', 8) + token[^4..];

    // ---------------------------------------------------------------------------------------------
    // One call, and what came back.

    /// <summary>Told of each call and how it went (the recorder's SITE lines; any thread). Never the token or the body.</summary>
    public static Action<string>? Trace { get; set; }

    /// <summary>Calls one of the site's database functions; its JSON answer, or why not.</summary>
    public static async Task<SiteReply<JsonNode?>> CallAsync(HttpClient http, string function, JsonObject args, TimeSpan wait, CancellationToken cancel = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(wait);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{SupabaseUrl}/rest/v1/rpc/{function}")
            {
                Content = new StringContent(args.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("apikey", PublishableKey);
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            var answer = ReadCall(response.StatusCode, text);
            Trace?.Invoke($"SITE {function}: {(int)response.StatusCode} in {clock.ElapsedMilliseconds} ms, {text.Length:N0} characters{(answer.Ok ? string.Empty : $", refused ({answer.Hint}): {answer.Why}")}");
            return answer;
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            Trace?.Invoke($"SITE {function}: no answer in {clock.ElapsedMilliseconds} ms");
            return new SiteReply<JsonNode?>(null, "The site didn't answer in time. Try again in a minute.", "wait");
        }
        catch (OperationCanceledException)
        {
            return new SiteReply<JsonNode?>(null, "Stopped.", "stopped");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            Trace?.Invoke($"SITE {function}: couldn't reach the site ({ex.GetType().Name}) after {clock.ElapsedMilliseconds} ms");
            return new SiteReply<JsonNode?>(null, "Couldn't reach the site. Check your connection.", "offline");
        }
    }

    /// <summary>A function's answer: its JSON (null is an answer too), or its refusal in words.</summary>
    public static SiteReply<JsonNode?> ReadCall(HttpStatusCode status, string? body)
    {
        var code = (int)status;
        if (code is >= 200 and < 300)
        {
            try
            {
                return new SiteReply<JsonNode?>(string.IsNullOrWhiteSpace(body) ? null : JsonNode.Parse(body));
            }
            catch (JsonException)
            {
                return new SiteReply<JsonNode?>(null, "The site answered with something the plugin can't read.", "answer");
            }
        }
        var (why, hint) = Refusal(status, body);
        return new SiteReply<JsonNode?>(null, why, hint);
    }

    /// <summary>Words that are the database's, not the site's (a missing function, a bad value): not for players.</summary>
    private static readonly string[] Internal =
    {
        "function", "column", "relation", "syntax", "violates", "permission denied", "schema", "operator does not exist", "invalid input",
    };

    /// <summary>
    /// A refusal in words, and its hint: the site's own message when it wrote one (a token it doesn't
    /// know gets "Link again" after it), else what the status says.
    /// </summary>
    public static (string Why, string? Hint) Refusal(HttpStatusCode status, string? body)
    {
        var code = (int)status;
        string? message = null, hint = null, pgCode = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(body) && JsonNode.Parse(body) is JsonObject o)
            {
                message = Text(o["message"]);
                hint = Text(o["hint"]);
                pgCode = Text(o["code"]);
            }
        }
        catch (JsonException)
        {
            // Not JSON (a proxy's page): said by the status below.
        }

        if (hint == "token")
            return ((message ?? "The site doesn't know this token.") + " Link again in Settings with a new one.", hint);
        var own = message != null && message.Length <= 240
                  && !(pgCode?.StartsWith("PGRST", StringComparison.OrdinalIgnoreCase) ?? false)
                  && !Internal.Any(w => message.Contains(w, StringComparison.OrdinalIgnoreCase));
        if (own) return (message!, hint ?? (code >= 500 ? "server" : null));
        return code switch
        {
            401 or 403 => ("The site turned the plugin away. It may need updating.", hint),
            404 => ("The site can't do that from the plugin right now. The plugin may need updating.", hint),
            413 => ("That's too big to send.", hint),
            429 => ("The site is busy. Try again in a minute.", "rate"),
            >= 500 => ($"The site had a problem ({code}). Try again in a minute.", "server"),
            _ => ($"The site said no ({code}).", hint),
        };
    }

    private static string? Text(JsonNode? n) =>
        n is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    private static SiteReply<T> NoToken<T>() => new(default, "That isn't a Glamour Studio token. Link again in Settings.", "token");

    private static SiteReply<T> Unreadable<T>() => new(default, "The site answered with something the plugin can't read.", "answer");

    private static int Int(JsonNode? n)
    {
        if (n is not JsonValue v) return 0;
        if (v.TryGetValue<int>(out var i)) return i;
        if (v.TryGetValue<long>(out var l)) return (int)Math.Clamp(l, int.MinValue, int.MaxValue);
        if (v.TryGetValue<double>(out var d) && double.IsFinite(d)) return (int)Math.Clamp(d, int.MinValue, int.MaxValue);
        return 0;
    }

    /// <summary>A read of your wardrobe's answer; null when it isn't one.</summary>
    public static SyncResult? ReadSync(JsonNode? node)
    {
        if (node is not JsonObject o || o["closet"] is not JsonObject c) return null;
        DateTimeOffset? at = Text(o["read_at"]) is { } s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : null;
        var got = (o["wishes_got"] as JsonArray)?.Select(LookJson.ItemId).OfType<uint>().Distinct().ToList() ?? new List<uint>();
        return new SyncResult(Int(c["total"]), Int(c["added"]), Int(c["moved"]), Int(c["removed"]), Int(o["plates"]), at, got);
    }

    // ---------------------------------------------------------------------------------------------
    // The calls.

    /// <summary>Your looks, the ones you love, and your shopping list.</summary>
    public static async Task<SiteReply<Library>> LooksAsync(HttpClient http, string token, CancellationToken cancel = default)
    {
        if (!LooksRight(token)) return NoToken<Library>();
        var r = await CallAsync(http, "gs_plugin_looks", new JsonObject { ["p_token"] = token }, LongTimeout, cancel).ConfigureAwait(false);
        if (!r.Ok) return r.As<Library>();
        return LookJson.ReadLibrary(r.Value) is { } lib ? new SiteReply<Library>(lib) : Unreadable<Library>();
    }

    /// <summary>The looks sent to the game from the site, newest first.</summary>
    public static async Task<SiteReply<List<SentLook>>> InboxAsync(HttpClient http, string token, CancellationToken cancel = default)
    {
        if (!LooksRight(token)) return NoToken<List<SentLook>>();
        var r = await CallAsync(http, "gs_plugin_inbox_list", new JsonObject { ["p_token"] = token }, CallTimeout, cancel).ConfigureAwait(false);
        if (!r.Ok) return r.As<List<SentLook>>();
        return LookJson.ReadInbox(r.Value) is { } list ? new SiteReply<List<SentLook>>(list) : Unreadable<List<SentLook>>();
    }

    /// <summary>Done with a look sent from the site (it's on a plate, or let go).</summary>
    public static async Task<SiteReply<bool>> InboxRemoveAsync(HttpClient http, string token, string id, CancellationToken cancel = default)
    {
        if (!LooksRight(token)) return NoToken<bool>();
        if (!LookJson.IsUuid(id)) return new SiteReply<bool>(true);
        var r = await CallAsync(http, "gs_plugin_inbox_remove", new JsonObject { ["p_token"] = token, ["p_id"] = id }, CallTimeout, cancel).ConfigureAwait(false);
        return r.Ok ? new SiteReply<bool>(true) : r.As<bool>();
    }

    /// <summary>Sends what the plugin read of your wardrobe (<see cref="Wardrobe.SyncArgs"/>).</summary>
    public static async Task<SiteReply<SyncResult>> SyncAsync(HttpClient http, string token, JsonObject args, CancellationToken cancel = default)
    {
        if (!LooksRight(token)) return NoToken<SyncResult>();
        var body = (JsonObject)args.DeepClone();
        body["p_token"] = token;
        var r = await CallAsync(http, "gs_plugin_sync", body, LongTimeout, cancel).ConfigureAwait(false);
        if (!r.Ok) return r.As<SyncResult>();
        return ReadSync(r.Value) is { } result ? new SiteReply<SyncResult>(result) : Unreadable<SyncResult>();
    }

    /// <summary>A character's plate plans (the looks planned on the site for its plates).</summary>
    public static async Task<SiteReply<List<PlannedPlate>>> PlansAsync(HttpClient http, string token, string character, CancellationToken cancel = default)
    {
        if (!LooksRight(token)) return NoToken<List<PlannedPlate>>();
        var r = await CallAsync(http, "gs_plugin_plans", new JsonObject { ["p_token"] = token, ["p_character"] = character }, CallTimeout, cancel).ConfigureAwait(false);
        if (!r.Ok) return r.As<List<PlannedPlate>>();
        return LookJson.ReadPlans(r.Value) is { } list ? new SiteReply<List<PlannedPlate>>(list) : Unreadable<List<PlannedPlate>>();
    }

    /// <summary>
    /// The public looks you own every piece of, the most loved first: as <paramref name="character"/> (the
    /// one you're on, when the site takes its data), else with every character's pieces together.
    /// </summary>
    public static async Task<SiteReply<List<Look>>> WearableAsync(HttpClient http, string token, string? character = null, CancellationToken cancel = default)
    {
        if (!LooksRight(token)) return NoToken<List<Look>>();
        var args = new JsonObject { ["p_token"] = token };
        if (character != null) args["p_character"] = character;
        var r = await CallAsync(http, "gs_plugin_wearable", args, LongTimeout, cancel).ConfigureAwait(false);
        if (!r.Ok) return r.As<List<Look>>();
        return LookJson.ReadLooks(r.Value) is { } list ? new SiteReply<List<Look>>(list) : Unreadable<List<Look>>();
    }

    /// <summary>What the site does with each of your characters' data (<c>gs_plugin_characters</c>), by key.</summary>
    public static async Task<SiteReply<Dictionary<string, SendChoice>>> CharactersAsync(HttpClient http, string token, CancellationToken cancel = default)
    {
        if (!LooksRight(token)) return NoToken<Dictionary<string, SendChoice>>();
        var r = await CallAsync(http, "gs_plugin_characters", new JsonObject { ["p_token"] = token }, CallTimeout, cancel).ConfigureAwait(false);
        if (!r.Ok) return r.As<Dictionary<string, SendChoice>>();
        return Consent.ReadCharacters(r.Value) is { } map ? new SiteReply<Dictionary<string, SendChoice>>(map) : Unreadable<Dictionary<string, SendChoice>>();
    }

    /// <summary>
    /// Tells the site your answer for a character (<c>gs_plugin_character</c>): yes, its name and world go
    /// with it; no, only its key, and the site lets go of anything it had of it. The value: what the site does now.
    /// </summary>
    public static async Task<SiteReply<SendChoice>> CharacterAsync(HttpClient http, string token, string key, string name, string world, bool send,
        CancellationToken cancel = default)
    {
        if (!LooksRight(token)) return NoToken<SendChoice>();
        var args = Consent.CharacterArgs(key, name, world, send);
        args["p_token"] = token;
        var r = await CallAsync(http, "gs_plugin_character", args, CallTimeout, cancel).ConfigureAwait(false);
        if (!r.Ok) return r.As<SendChoice>();
        return r.Value is JsonObject o && Text(o["send"]) is { } s ? new SiteReply<SendChoice>(Consent.Parse(s)) : Unreadable<SendChoice>();
    }

    /// <summary>Says which of your characters wore a shot just sent (shown on the site only while you show that character on your shots).</summary>
    public static async Task<SiteReply<bool>> ShotCharacterAsync(HttpClient http, string token, string shotId, string character, CancellationToken cancel = default)
    {
        if (!LooksRight(token)) return NoToken<bool>();
        if (!LookJson.IsUuid(shotId)) return new SiteReply<bool>(false);
        var args = new JsonObject { ["p_token"] = token, ["p_shot"] = shotId, ["p_character"] = character };
        var r = await CallAsync(http, "gs_plugin_shot_character", args, CallTimeout, cancel).ConfigureAwait(false);
        if (!r.Ok) return r.As<bool>();
        return new SiteReply<bool>(r.Value is JsonObject o && o["ok"] is JsonValue v && v.TryGetValue<bool>(out var ok) && ok);
    }

    /// <summary>The edge function that takes shots from the game.</summary>
    public static string ShotUrl => $"{SupabaseUrl}/functions/v1/gs-plugin-shot";

    /// <summary>The most a shot's picture may be: the shots bucket's 3 MB, less room for the form.</summary>
    public const int ShotMaxBytes = 3_000_000;

    /// <summary>The longest a shot's upload waits.</summary>
    public static readonly TimeSpan ShotTimeout = TimeSpan.FromSeconds(90);

    /// <summary>The shot's form: <c>token</c>, <c>look</c>, <c>caption</c> and <c>image</c> (the JPEG, as a file).</summary>
    public static MultipartFormDataContent ShotForm(string token, string lookId, string caption, byte[] jpeg)
    {
        var form = new MultipartFormDataContent();
        form.Add(Field("token", token));
        form.Add(Field("look", lookId));
        form.Add(Field("caption", caption));
        var image = new ByteArrayContent(jpeg);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        image.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"image\"", FileName = "\"shot.jpg\"" };
        form.Add(image);
        return form;
    }

    private static StringContent Field(string name, string value)
    {
        var c = new StringContent(value, Encoding.UTF8);
        c.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = $"\"{name}\"" };
        return c;
    }

    /// <summary>Sends a shot of you wearing a public look to its Worn in game; never throws.</summary>
    public static async Task<SiteReply<ShotSent>> ShotAsync(HttpClient http, string token, string lookId, string caption, byte[] jpeg, CancellationToken cancel = default)
    {
        if (!LooksRight(token)) return NoToken<ShotSent>();
        if (!LookJson.IsUuid(lookId)) return new SiteReply<ShotSent>(null, "The plugin didn't say which look.", "look");
        if (jpeg.Length == 0 || jpeg.Length > ShotMaxBytes) return new SiteReply<ShotSent>(null, "That picture is too big to send.", "image");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(ShotTimeout);
        try
        {
            using var form = ShotForm(token, lookId, caption, jpeg);
            using var request = new HttpRequestMessage(HttpMethod.Post, ShotUrl) { Content = form };
            request.Headers.Add("apikey", PublishableKey);
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            var shot = ReadShot(response.StatusCode, text);
            Trace?.Invoke($"SITE gs-plugin-shot: {(int)response.StatusCode}, {jpeg.Length:N0} bytes sent{(shot.Ok ? string.Empty : $", refused ({shot.Hint}): {shot.Why}")}");
            return shot;
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            return new SiteReply<ShotSent>(null, "The site didn't answer in time. Look on the look's page before sending it again.", "wait");
        }
        catch (OperationCanceledException)
        {
            return new SiteReply<ShotSent>(null, "Stopped.", "stopped");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return new SiteReply<ShotSent>(null, "Couldn't reach the site. Check your connection and try again.", "offline");
        }
    }

    /// <summary>A shot's size with its longest side at most <paramref name="longEdge"/>, its shape kept; null when it's too small (under 64).</summary>
    public static (int Width, int Height)? FitShot(int width, int height, int longEdge)
    {
        if (width < 64 || height < 64 || longEdge < 64) return null;
        var scale = Math.Min(1.0, (double)longEdge / Math.Max(width, height));
        var (w, h) = ((int)Math.Round(width * scale), (int)Math.Round(height * scale));
        return w < 64 || h < 64 ? null : (w, h);
    }

    /// <summary>The edge function's answer: the shot (its <c>id</c> and the look's <c>name</c>), or <c>{message, hint}</c> in words.</summary>
    public static SiteReply<ShotSent> ReadShot(HttpStatusCode status, string? body)
    {
        var code = (int)status;
        if (code is >= 200 and < 300)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(body) && JsonNode.Parse(body) is JsonObject o && Text(o["id"]) is { } id && LookJson.IsUuid(id))
                    return new SiteReply<ShotSent>(new ShotSent(id, LookJson.Named(o["name"], "the look")));
            }
            catch (JsonException)
            {
                // Said below.
            }
            return new SiteReply<ShotSent>(null, "The site answered, but not with the shot. Look on the look's page before sending it again.", "answer");
        }
        var (why, hint) = Refusal(status, body);
        if (code == 404 && hint == null) return new SiteReply<ShotSent>(null, "The site can't take shots from the plugin right now. The plugin may need updating.");
        return new SiteReply<ShotSent>(null, why, hint);
    }

    /// <summary>Keeps an outfit from the game as a new private look of yours.</summary>
    public static async Task<SiteReply<SavedLook>> SaveLookAsync(HttpClient http, string token, string name, Body body, IReadOnlyDictionary<Slot, Piece> pieces,
        CancellationToken cancel = default)
    {
        if (!LooksRight(token)) return NoToken<SavedLook>();
        if (pieces.Count == 0) return new SiteReply<SavedLook>(null, "There's nothing on it to keep.", "pieces");
        var args = new JsonObject
        {
            ["p_token"] = token,
            ["p_name"] = name,
            ["p_body"] = LookJson.BodyKey(body),
            ["p_pieces"] = LookJson.Write(pieces),
        };
        var r = await CallAsync(http, "gs_plugin_save_look", args, CallTimeout, cancel).ConfigureAwait(false);
        if (!r.Ok) return r.As<SavedLook>();
        if (r.Value is JsonObject o && Text(o["id"]) is { } id && LookJson.IsUuid(id))
            return new SiteReply<SavedLook>(new SavedLook(id, LookJson.Named(o["name"], name)));
        return Unreadable<SavedLook>();
    }

    /// <summary>Puts pieces on your shopping list (40 at most; one there already stays as it was). The value: how many went on.</summary>
    public static async Task<SiteReply<int>> WishAddAsync(HttpClient http, string token, IReadOnlyList<Piece> pieces, CancellationToken cancel = default)
    {
        if (!LooksRight(token)) return NoToken<int>();
        var items = pieces.Where(p => p.Item != 0).GroupBy(p => LookJson.BaseId(p.Item)).Take(40)
            .Select(g => (JsonNode)new JsonObject { ["item"] = g.Key, ["dyes"] = new JsonArray(g.First().Dye1, g.First().Dye2) })
            .ToArray();
        if (items.Length == 0) return new SiteReply<int>(0, "There's nothing to put on the list.", "items");
        var args = new JsonObject { ["p_token"] = token, ["p_items"] = new JsonArray(items) };
        var r = await CallAsync(http, "gs_plugin_wish_add", args, CallTimeout, cancel).ConfigureAwait(false);
        if (!r.Ok)
        {
            // An older site, without the call yet.
            if (r.Hint == null && r.Why?.Contains("gs_plugin_wish_add", StringComparison.Ordinal) == true)
                return new SiteReply<int>(0, "The site can't take shopping list pieces from the plugin yet.");
            return r.As<int>();
        }
        if (r.Value is JsonObject o && o["added"] is JsonValue v && v.TryGetValue<int>(out var n)) return new SiteReply<int>(n);
        return Unreadable<int>();
    }

    /// <summary>Takes pieces off your shopping list (item ids). The value: how many came off.</summary>
    public static async Task<SiteReply<int>> WishRemoveAsync(HttpClient http, string token, IReadOnlyList<uint> items, CancellationToken cancel = default)
    {
        if (!LooksRight(token)) return NoToken<int>();
        var ids = items.Where(i => i != 0).Select(LookJson.BaseId).Distinct().Take(40).Select(i => (JsonNode)JsonValue.Create(i)).ToArray();
        if (ids.Length == 0) return new SiteReply<int>(0, "There's nothing to take off the list.", "items");
        var args = new JsonObject { ["p_token"] = token, ["p_items"] = new JsonArray(ids) };
        var r = await CallAsync(http, "gs_plugin_wish_remove", args, CallTimeout, cancel).ConfigureAwait(false);
        if (!r.Ok)
        {
            // An older site, without the call yet.
            if (r.Hint == null && r.Why?.Contains("gs_plugin_wish_remove", StringComparison.Ordinal) == true)
                return new SiteReply<int>(0, "The site can't take pieces off the shopping list from the plugin yet.");
            return r.As<int>();
        }
        if (r.Value is JsonObject o && o["removed"] is JsonValue v && v.TryGetValue<int>(out var n)) return new SiteReply<int>(n);
        return Unreadable<int>();
    }

    /// <summary>Puts a piece on one of your own looks, in a slot (its piece there replaced). The value: the look's name.</summary>
    public static async Task<SiteReply<string>> LookPutAsync(HttpClient http, string token, string lookId, Slot slot, Piece piece, CancellationToken cancel = default)
    {
        if (!LooksRight(token)) return NoToken<string>();
        var args = new JsonObject
        {
            ["p_token"] = token,
            ["p_look"] = lookId,
            ["p_slot"] = Slots.Key(slot),
            ["p_item"] = LookJson.BaseId(piece.Item),
            ["p_dye1"] = piece.Dye1,
            ["p_dye2"] = piece.Dye2,
        };
        var r = await CallAsync(http, "gs_plugin_look_put", args, CallTimeout, cancel).ConfigureAwait(false);
        if (!r.Ok) return r.As<string>();
        return r.Value is JsonObject o ? new SiteReply<string>(LookJson.Named(o["name"], "your look")) : Unreadable<string>();
    }
}
