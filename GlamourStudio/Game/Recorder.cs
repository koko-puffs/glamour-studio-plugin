using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GlamourStudio.Core;

namespace GlamourStudio.Game;

/// <summary>
/// Watch-only diagnostics for Glamour Studio, as Housing Studio's recorder is: while on, it writes
/// down every game window that opens, refreshes or closes (the glamour windows with all their words),
/// every click a window sends (its values: what doing it by hand sends, so a recording shows exactly
/// what to send), every event the glamour agents receive (the plate editor, the dresser, outfit sets,
/// the armoire, Examine, gearsets, the fitting room, dyes), the game's system messages and toasts,
/// and the state as it changes: the plate editor (the plate, the slot picked, each slot's piece,
/// where it came from, its dyes and pending dyes), the dresser and plates as the game has them, the
/// armoire, your gearsets and their plates, Examine, what you wear, your bags, where you are and the
/// HUD. What the plugin itself does (filling a plate, storing, linking a gearset, a shot) is written
/// down as PLUGIN lines, and you can write a MARK before each step. It never sends anything.
/// </summary>
public sealed unsafe class Recorder : IDisposable
{
    private const int MaxLines = 20000;

    /// <summary>The first lines (the version, the character, the settings) are kept when old ones are let go of.</summary>
    private const int HeaderLines = 5;

    private const int LineAtMost = 6000;

    private const int WordsAtMost = 160;

    /// <summary>A recording stops by itself after this long (a whole list of steps fits).</summary>
    private static readonly TimeSpan MaxDuration = TimeSpan.FromHours(2);

    /// <summary>How often the state is looked at for changes.</summary>
    private static readonly TimeSpan StateEvery = TimeSpan.FromMilliseconds(200);

    /// <summary>The windows of the glamour features: all their words are written down, and their refreshes.</summary>
    public static readonly string[] GlamourWindows =
    {
        "MiragePrismMiragePlate", "MiragePrismPrismBox", "MiragePrismPrismSetConvert", "MiragePrismPrismSetConvertC", "MiragePrismPrismItemDetail",
        "MiragePrismExecute", "MiragePrismRemove", "MiragePrismPrismBoxCrystallize", "Cabinet", "CabinetWithdraw", "CharacterInspect", "GearSetList",
        "Character", "Tryon", "ColorantColoring", "ColorantEquipment", "ContextMenu", "ContextIconMenu", "SelectYesno", "SelectString",
        "SelectIconString", "InventoryBuddy", "InventoryBuddy2", "InventoryRetainer", "InventoryRetainerLarge", "RetainerList", "ArmouryBoard",
        "Inventory", "InventoryLarge", "InventoryExpansion", "ItemSearch", "ItemSearchResult", "Shop", "ShopExchangeItem", "ShopExchangeCurrency", "InclusionShop",
    };

    private static readonly HashSet<string> Glamour = new(GlamourWindows, StringComparer.Ordinal);

    /// <summary>Windows whose clicks and openings say nothing about glamour: other plugins' overlays and the HUD's own.</summary>
    private static readonly HashSet<string> Noise = new(StringComparer.Ordinal)
    {
        "CursorAddon", "Tooltip", "ItemDetail", "ActionDetail", "TooltipDetail", "ScreenFrameSystem", "NamePlate", "ChatLog", "ChatLogPanel_0",
        "ChatLogPanel_1", "ChatLogPanel_2", "ChatLogPanel_3", "ScreenLog", "FadeMiddle", "FadeBack", "DTR", "NowLoading", "Talk",
    };

    private static bool IsNoise(string name) => name.Length == 0 || name[0] == '_' || Noise.Contains(name) || name.StartsWith("BMR_", StringComparison.Ordinal);

    /// <summary>The glamour agents whose events are written down.</summary>
    private static readonly (AgentId Id, string Name)[] Agents =
    {
        (AgentId.MiragePrismMiragePlate, "MiragePlate"), (AgentId.MiragePrismPrismBox, "PrismBox"), (AgentId.MiragePrismPrismSetConvert, "PrismSetConvert"),
        (AgentId.MiragePrismPrismItemDetail, "PrismItemDetail"), (AgentId.MiragePrism, "MiragePrism"), (AgentId.Cabinet, "Cabinet"),
        (AgentId.CabinetWithdraw, "CabinetWithdraw"), (AgentId.Inspect, "Inspect"), (AgentId.GearSet, "GearSet"), (AgentId.Tryon, "Tryon"),
        (AgentId.Colorant, "Colorant"), (AgentId.InventoryContext, "InventoryContext"), (AgentId.Context, "Context"), (AgentId.RetainerList, "RetainerList"),
    };

    /// <summary>The recording going now (for the game's code to write PLUGIN lines into), or null.</summary>
    public static Recorder? Active { get; private set; }

    /// <summary>Writes a PLUGIN line into the recording going, if one is.</summary>
    public static void Log(string line) => Active?.Add("PLUGIN " + line);

    /// <summary>Writes a SITE line (or any line as given) into the recording going; any thread.</summary>
    public static void Raw(string line) => Active?.Add(line);

    /// <summary>Writes a PLUGIN line, if this is recording.</summary>
    public void Note(string line)
    {
        if (Recording) Add("PLUGIN " + line);
    }

    private readonly GameData data;
    private readonly Hook<AtkUnitBase.Delegates.FireCallback>? callbackHook;
    private readonly Dictionary<nint, Hook<AgentInterface.Delegates.ReceiveEvent>> agentHooks = new();

    /// <summary>What was hooked (each agent's event functions, their place in the game, shared or not), for the header.</summary>
    private readonly List<string> hookReport = new();

    /// <summary>The agents in a hooked event call on this thread: an agent calling its own base is written once; one agent handing to another is written.</summary>
    [ThreadStatic]
    private static HashSet<nint>? inCall;

    /// <summary>Windows that show other people (their names): opened and closed, but not their words or values.</summary>
    private static readonly HashSet<string> Private = new(StringComparer.Ordinal)
    {
        // Others' names: who you examine, the market board's sellers and retainers, a shop's.
        "CharacterInspect", "ItemSearch", "ItemSearchResult", "Shop", "ShopExchangeItem", "ShopExchangeCurrency", "InclusionShop",
    };

    /// <summary>The lines kept when old ones go: the header and the start's state.</summary>
    private int headerCount = HeaderLines;

    /// <summary>The last line's words (without its time) and how many times it came again since.</summary>
    private string lastBody = string.Empty;

    private int repeats;
    private readonly Dictionary<nint, string> agentNames = new();

    /// <summary>The agents' detours, kept so they're never collected while hooked.</summary>
    private readonly List<AgentInterface.Delegates.ReceiveEvent> detours = new();
    private readonly List<string> lines = new();
    private readonly Dictionary<string, string> lastState = new();
    private readonly Dictionary<string, int> lastWords = new();
    private DateTime started;
    private DateTime nextState;
    private int dropped;
    private bool listening;

    /// <summary>The plugin's own state, for a STATE line (what's linked, sending, a shot being taken), from the plugin.</summary>
    public Func<string>? PluginState { get; set; }

    /// <summary>The header's lines from the plugin: the version, the character, the settings.</summary>
    public Func<IEnumerable<string>>? Header { get; set; }

    public bool Recording { get; private set; }

    public string? SetupError { get; private set; }

    public int LineCount
    {
        get
        {
            lock (lines) return lines.Count + dropped;
        }
    }

    public int Marks { get; private set; }

    /// <summary>Where the last recording was saved, if it was.</summary>
    public string? SavedTo { get; private set; }

    /// <summary>The recording has been saved or copied since it was last added to (a new one won't lose it).</summary>
    public bool HandedOver { get; private set; }

    /// <summary>Says the recording's been copied (Settings' Copy).</summary>
    public void Copied() => HandedOver = true;

    public Recorder(GameData data)
    {
        this.data = data;
        try
        {
            callbackHook = Services.GameInterop.HookFromAddress<AtkUnitBase.Delegates.FireCallback>((nint)AtkUnitBase.MemberFunctionPointers.FireCallback, CallbackDetour);
        }
        catch (Exception ex)
        {
            SetupError = "Couldn't attach to the game's window clicks, so they aren't recorded (windows, events and state still are).";
            Services.Log.Error(ex, "Glamour Studio's recorder couldn't hook the window callbacks");
        }
    }

    /// <summary>
    /// The glamour agents' event functions (ReceiveEvent and ReceiveEventWithResult), hooked once
    /// (framework thread, when recording starts). One more than two agents share (the base's, or a
    /// one-line stub) isn't hooked: it'd be called for most of the game's agents.
    /// </summary>
    private void HookAgents()
    {
        if (hookReport.Count > 0) return;
        try
        {
            var module = AgentModule.Instance();
            if (module == null) return;
            var baseAddress = System.Diagnostics.Process.GetCurrentProcess().MainModule?.BaseAddress ?? 0;
            int Sharing(nint fn, bool withResult)
            {
                var n = 0;
                foreach (var p in module->Agents)
                {
                    var ag = p.Value;
                    if (ag == null || ag->VirtualTable == null) continue;
                    if ((withResult ? (nint)ag->VirtualTable->ReceiveEventWithResult : (nint)ag->VirtualTable->ReceiveEvent) == fn) n++;
                }
                return n;
            }
            foreach (var (id, name) in Agents)
            {
                var agent = module->GetAgentByInternalId(id);
                if (agent == null || agent->VirtualTable == null)
                {
                    hookReport.Add($"{name}: no agent");
                    continue;
                }
                agentNames[(nint)agent] = name;
                foreach (var withResult in new[] { false, true })
                {
                    var fn = withResult ? (nint)agent->VirtualTable->ReceiveEventWithResult : (nint)agent->VirtualTable->ReceiveEvent;
                    var which = withResult ? "ReceiveEventWithResult" : "ReceiveEvent";
                    if (fn == 0) continue;
                    var rva = baseAddress != 0 ? $"+0x{fn - baseAddress:X}" : $"0x{fn:X}";
                    if (agentHooks.ContainsKey(fn))
                    {
                        hookReport.Add($"{name}.{which} {rva}: hooked already (shared)");
                        continue;
                    }
                    var shared = Sharing(fn, withResult);
                    if (shared > 2)
                    {
                        hookReport.Add($"{name}.{which} {rva}: shared by {shared} agents, not hooked");
                        continue;
                    }
                    try
                    {
                        // Each hook its own detour, so the original called is always this one's.
                        Hook<AgentInterface.Delegates.ReceiveEvent>? hook = null;
                        var label = withResult ? " (with result)" : string.Empty;
                        AgentInterface.Delegates.ReceiveEvent detour = (ag, r, v, c, k) => AgentDetour(hook!, label, ag, r, v, c, k);
                        hook = Services.GameInterop.HookFromAddress(fn, detour);
                        detours.Add(detour);
                        agentHooks[fn] = hook;
                        hookReport.Add($"{name}.{which} {rva}: hooked{(shared > 1 ? $" (shared by {shared})" : string.Empty)}");
                    }
                    catch (Exception ex)
                    {
                        hookReport.Add($"{name}.{which} {rva}: couldn't hook ({ex.GetType().Name})");
                        Services.Log.Warning(ex, $"Glamour Studio's recorder couldn't hook {name}'s {which}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            hookReport.Add($"couldn't find the glamour agents ({ex.GetType().Name})");
            Services.Log.Warning(ex, "Glamour Studio's recorder couldn't find the glamour agents");
        }
    }

    /// <summary>What was hooked, for the report.</summary>
    public IReadOnlyList<string> HookReport => hookReport;

    // ---------------------------------------------------------------------------------------------
    // Starting and stopping.

    /// <summary>Starts a recording (framework thread).</summary>
    public void Start()
    {
        if (Recording) return;
        lock (lines)
        {
            lines.Clear();
            headerCount = HeaderLines;
            lastBody = string.Empty;
            repeats = 0;
        }
        lastState.Clear();
        lastWords.Clear();
        lock (lines)
        {
            dropped = 0;
            started = DateTime.UtcNow;
        }
        Marks = 0;
        SavedTo = null;
        HandedOver = false;
        nextState = DateTime.MinValue;
        Recording = true;
        Active = this;
        Add($"Recording started {DateTime.Now:yyyy-MM-dd HH:mm:ss} (local). Glamour Studio {typeof(Recorder).Assembly.GetName().Version}, client {Services.ClientState.ClientLanguage}.");
        try
        {
            foreach (var h in Header?.Invoke() ?? Array.Empty<string>()) Add(h);
        }
        catch (Exception ex)
        {
            Add($"(The header couldn't be written: {ex.GetType().Name}: {ex.Message})");
        }
        if (SetupError != null) Add(SetupError);
        HookAgents();
        Add("Hooks: " + string.Join("; ", hookReport));
        callbackHook?.Enable();
        foreach (var h in agentHooks.Values) h.Enable();
        // Set first: should a registration below throw, Stop still takes off the ones made.
        listening = true;
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, OnOpen);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, OnClose);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, GlamourWindows, OnRefresh);
        Services.Toasts.ErrorToast += OnErrorToast;
        Services.Toasts.Toast += OnToast;
        Services.Toasts.QuestToast += OnQuestToast;
        Services.Chat.ChatMessage += OnChat;
        Services.Chat.LogMessage += OnLogMessage;
        Services.ClientState.Login += OnLogin;
        Services.ClientState.Logout += OnLogout;
        Add("Write a MARK before each step (Settings → Making a report, or /gstudio mark <what you'll do>), do it by hand, then stop and copy or save.");
        WriteAllState("START");
        // The header and the start's state are kept when old lines go.
        lock (lines) headerCount = lines.Count;
    }

    private void OnLogin() => Add($"LOGIN content {Services.PlayerState.ContentId:X}");

    private void OnLogout(int type, int code) => Add($"LOGOUT type {type} code {code}");

    /// <summary>Stops the recording (framework thread).</summary>
    public void Stop() => Stop(writeState: true);

    private void Stop(bool writeState)
    {
        if (!Recording) return;
        if (writeState) WriteAllState("END");
        Recording = false;
        if (ReferenceEquals(Active, this)) Active = null;
        callbackHook?.Disable();
        foreach (var h in agentHooks.Values) h.Disable();
        if (listening)
        {
            Services.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, OnOpen);
            Services.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, OnClose);
            Services.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, GlamourWindows, OnRefresh);
            Services.Toasts.ErrorToast -= OnErrorToast;
            Services.Toasts.Toast -= OnToast;
            Services.Toasts.QuestToast -= OnQuestToast;
            Services.Chat.ChatMessage -= OnChat;
            Services.Chat.LogMessage -= OnLogMessage;
            Services.ClientState.Login -= OnLogin;
            Services.ClientState.Logout -= OnLogout;
            listening = false;
        }
        Add($"Recording stopped after {(DateTime.UtcNow - started).TotalMinutes:0.0} minutes, {Marks} marks.");
    }

    /// <summary>A step you're about to do, with the whole state as it is now (framework thread).</summary>
    public void Mark(string what)
    {
        if (!Recording) return;
        Marks++;
        Add($"MARK {Marks}: {(string.IsNullOrWhiteSpace(what) ? "(no words)" : what.Trim())}");
        WriteAllState("MARK");
    }

    /// <summary>Every value the glamour windows hold now, and the whole state, in lines of a hundred values (framework thread).</summary>
    public void DumpWindows()
    {
        if (!Recording) return;
        Add("DUMP: every value the open glamour windows hold, and the whole state.");
        foreach (var name in GlamourWindows)
        {
            var a = (AtkUnitBase*)Services.GameGui.GetAddonByName(name).Address;
            if (a == null) continue;
            if (Private.Contains(name))
            {
                Add($"VALUES {name}: {a->AtkValuesCount} values, visible {a->IsVisible} (not written down: someone else's)");
                continue;
            }
            var values = AtkText.Values(name);
            Add($"VALUES {name}: {values.Count} values, visible {a->IsVisible}");
            Chunked($"VALUES {name}", values, 100);
        }
        Chunked("EDITOR DETAIL", PlateEditor.Describe(data, detail: true).Split("; ", StringSplitOptions.RemoveEmptyEntries), 12);
        Chunked("DRESSER DETAIL", DescribeDresser(detail: true).Split(';', StringSplitOptions.RemoveEmptyEntries), 40);
        Chunked("PLATES DETAIL", DescribePlates(detail: true).Split(" | ", StringSplitOptions.RemoveEmptyEntries), 2);
        Chunked("ARMOIRE DETAIL", DescribeArmoire(detail: true).Split(';', StringSplitOptions.RemoveEmptyEntries), 40);
        WriteAllState("DUMP");
    }

    /// <summary>Parts written so many to a line, each line saying which they are.</summary>
    private void Chunked(string label, IReadOnlyList<string> parts, int perLine)
    {
        for (var i = 0; i < parts.Count; i += perLine)
        {
            var to = Math.Min(parts.Count, i + perLine);
            Add($"{label} [{i}-{to - 1}]: {string.Join("; ", parts.Skip(i).Take(to - i).Select(p => p.Trim()))}", cut: false);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Every frame: what changed.

    /// <summary>Framework thread, every frame while recording: state lines for what changed.</summary>
    public void Tick()
    {
        if (!Recording) return;
        var now = DateTime.UtcNow;
        if (now - started > MaxDuration)
        {
            Add($"Stopped by itself after {MaxDuration.TotalHours:0} hours.");
            Stop();
            return;
        }
        if (now < nextState) return;
        nextState = now + StateEvery;
        foreach (var (area, describe) in Areas())
        {
            string s;
            try
            {
                s = describe();
            }
            catch (Exception ex)
            {
                s = $"couldn't read ({ex.GetType().Name}: {ex.Message})";
            }
            if (lastState.TryGetValue(area, out var last) && last == s) continue;
            lastState[area] = s;
            Add($"STATE {area}: {s}");
        }
    }

    private void WriteAllState(string why)
    {
        foreach (var (area, describe) in Areas())
        {
            string s;
            try
            {
                s = describe();
            }
            catch (Exception ex)
            {
                s = $"couldn't read ({ex.GetType().Name}: {ex.Message})";
            }
            lastState[area] = s;
            Add($"  {why} {area}: {s}");
        }
    }

    private IEnumerable<(string Area, Func<string> Describe)> Areas()
    {
        yield return ("where", DescribeWhere);
        yield return ("editor", () => PlateEditor.Describe(data, detail: false));
        yield return ("dresser", () => DescribeDresser(detail: false));
        yield return ("plates", () => DescribePlates(detail: false));
        yield return ("armoire", () => DescribeArmoire(detail: false));
        yield return ("gearsets", DescribeGearsets);
        yield return ("examine", DescribeExamine);
        yield return ("worn", DescribeWorn);
        yield return ("bags", DescribeBags);
        yield return ("dyes", DescribeDyes);
        yield return ("dresser window", DescribeDresserWindow);
        yield return ("armoire window", DescribeArmoireWindow);
        yield return ("outfit sets", DescribeSetConvert);
        yield return ("try-on", DescribeTryOn);
        yield return ("retainers", DescribeRetainers);
        yield return ("windows", DescribeWindows);
        if (PluginState != null) yield return ("plugin", PluginState);
    }

    // ---------------------------------------------------------------------------------------------
    // The state, in words.

    private static string DescribeWhere()
    {
        var c = Services.Condition;
        var flags = new List<string>();
        if (c[ConditionFlag.InCombat]) flags.Add("combat");
        if (c[ConditionFlag.BoundByDuty] || c[ConditionFlag.BoundByDuty56] || c[ConditionFlag.BoundByDuty95]) flags.Add("duty");
        if (c[ConditionFlag.OccupiedInCutSceneEvent] || c[ConditionFlag.WatchingCutscene] || c[ConditionFlag.WatchingCutscene78]) flags.Add("cutscene");
        if (c[ConditionFlag.BetweenAreas] || c[ConditionFlag.BetweenAreas51]) flags.Add("loading");
        if (c[ConditionFlag.Mounted]) flags.Add("mounted");
        if (c[ConditionFlag.OccupiedInQuestEvent] || c[ConditionFlag.OccupiedInEvent]) flags.Add("event");
        if (c[ConditionFlag.UsingHousingFunctions]) flags.Add("housing");
        var hud = Services.GameGui.GameUiHidden ? "hidden" : "shown";
        var ps = Services.PlayerState;
        var who = ps.IsLoaded ? $"{ps.CharacterName}@{ps.HomeWorld.ValueNullable?.Name.ExtractText()} job {ps.ClassJob.RowId}" : "not logged in";
        return $"territory {Services.ClientState.TerritoryType} map {Services.ClientState.MapId}, {who}, hud {hud}, gpose {Services.ClientState.IsGPosing}"
               + (flags.Count > 0 ? ", " + string.Join(" ", flags) : string.Empty);
    }

    private string DescribeDresser(bool detail)
    {
        var mm = MirageManager.Instance();
        if (mm == null) return "no MirageManager";
        if (!mm->PrismBoxLoaded) return "not loaded";
        var ids = mm->PrismBoxItemIds;
        var s0 = mm->PrismBoxStain0Ids;
        var s1 = mm->PrismBoxStain1Ids;
        var n = 0;
        ulong hash = 1469598103934665603;
        var sb = detail ? new StringBuilder() : null;
        for (var i = 0; i < ids.Length; i++)
        {
            if (ids[i] == 0) continue;
            n++;
            hash = (hash ^ ids[i] ^ ((ulong)s0[i] << 32) ^ ((ulong)s1[i] << 40) ^ ((ulong)i << 48)) * 1099511628211;
            sb?.Append($" [{i}] {ids[i]} {data.ItemName(ids[i] % 500000)} {s0[i]}/{s1[i]};");
        }
        return $"loaded, {n} of {ids.Length} places, hash {hash:X16}" + (sb != null ? ":" + sb : string.Empty);
    }

    private string DescribePlates(bool detail)
    {
        var mm = MirageManager.Instance();
        if (mm == null) return "no MirageManager";
        if (!mm->GlamourPlatesLoaded) return "not loaded";
        var all = mm->GlamourPlates;
        var parts = new List<string>();
        for (var p = 0; p < all.Length; p++)
        {
            var plate = all[p];
            var ids = plate.ItemIds;
            var count = 0;
            ulong hash = 1469598103934665603;
            var slots = detail ? new List<string>() : null;
            for (var s = 0; s < ids.Length; s++)
            {
                if (ids[s] == 0) continue;
                count++;
                hash = (hash ^ ids[s] ^ ((ulong)plate.Stain0Ids[s] << 32) ^ ((ulong)plate.Stain1Ids[s] << 40) ^ ((ulong)s << 48)) * 1099511628211;
                slots?.Add($"{(Slots.FromPlateIndex(s) is { } sl ? Slots.Key(sl) : s.ToString())} {ids[s]} {data.ItemName(ids[s] % 500000)} {plate.Stain0Ids[s]}/{plate.Stain1Ids[s]}");
            }
            if (count == 0) continue;
            parts.Add(detail ? $"#{p + 1}: {string.Join(", ", slots!)}" : $"#{p + 1}:{count}/{(uint)hash:X8}");
        }
        return $"loaded, {parts.Count} with pieces" + (parts.Count > 0 ? " " + string.Join(detail ? " | " : " ", parts) : string.Empty);
    }

    private string DescribeArmoire(bool detail)
    {
        var ui = UIState.Instance();
        var open = Armoire.IsOpen;
        if (ui == null) return "no UIState";
        if (!ui->Cabinet.IsCabinetLoaded()) return $"not loaded, window {(open ? "open" : "closed")}";
        var n = 0;
        ulong hash = 1469598103934665603;
        var sb = detail ? new StringBuilder() : null;
        foreach (var (row, item) in data.CabinetRows)
        {
            if (!ui->Cabinet.IsItemInCabinet(row)) continue;
            n++;
            hash = (hash ^ row) * 1099511628211;
            sb?.Append($" {row}:{item} {data.ItemName(item)};");
        }
        return $"loaded, window {(open ? "open" : "closed")}, {n} stored, hash {hash:X16}" + (sb != null ? ":" + sb : string.Empty);
    }

    private static string DescribeGearsets()
    {
        var m = RaptureGearsetModule.Instance();
        if (m == null) return "no gearset module";
        var parts = new List<string>();
        var entries = m->Entries;
        for (var i = 0; i < entries.Length; i++)
        {
            ref var e = ref entries[i];
            if (!e.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
            parts.Add($"{i + 1}:{e.ClassJob}{(e.GlamourSetLink != 0 ? $"→plate{e.GlamourSetLink}" : string.Empty)}");
        }
        return $"current {m->CurrentGearsetIndex + 1}, {parts.Count} sets: {string.Join(" ", parts)}";
    }

    private string DescribeExamine()
    {
        var agent = AgentInspect.Instance();
        var open = Examine.IsOpen;
        if (agent == null) return "no agent";
        var head = $"window {(open ? "open" : "closed")}, request {agent->RequestEntityId:X}, current {agent->CurrentEntityId:X}, fetch {agent->FetchCharacterDataStatus}, "
                   + $"view state {agent->CharaView.State} copied {agent->CharaView.CharacterDataCopied} loaded {agent->CharaView.CharacterLoaded} sex {agent->CharaView.Sex}";
        if (!open) return head;
        var mgr = InventoryManager.Instance();
        var c = mgr != null ? mgr->GetInventoryContainer(InventoryType.Examine) : null;
        if (c == null || !c->IsLoaded) return head + ", container not loaded";
        var pieces = new List<string>();
        for (var i = 0; i < c->Size; i++)
        {
            var it = c->GetInventorySlot(i);
            if (it == null || it->IsEmpty()) continue;
            var glam = it->GlamourId;
            pieces.Add($"[{i}] {it->GetBaseItemId()}{(glam != 0 ? $" as {glam} {data.ItemName(glam % 500000)}" : $" {data.ItemName(it->GetBaseItemId())}")} {it->GetStain(0)}/{it->GetStain(1)}");
        }
        // (No names of other people in a recording: whether they're near is enough.)
        var near = Services.Objects.SearchByEntityId(agent->CurrentEntityId) != null;
        return head + $", {(near ? "in the object table" : "not in the object table")}, {pieces.Count} pieces: {string.Join("; ", pieces)}";
    }

    private string DescribeWorn()
    {
        var mgr = InventoryManager.Instance();
        var c = mgr != null ? mgr->GetInventoryContainer(InventoryType.EquippedItems) : null;
        if (c == null || !c->IsLoaded) return "not loaded";
        var pieces = new List<string>();
        for (var i = 0; i < c->Size; i++)
        {
            var it = c->GetInventorySlot(i);
            if (it == null || it->IsEmpty()) continue;
            var glam = it->GlamourId;
            pieces.Add($"[{i}] {it->GetBaseItemId()}{(glam != 0 ? $" as {glam}" : string.Empty)} {it->GetStain(0)}/{it->GetStain(1)}");
        }
        return string.Join("; ", pieces);
    }

    private static readonly (string Name, InventoryType[] Types)[] BagGroups =
    {
        ("bags", new[] { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 }),
        ("armoury", new[]
        {
            InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand, InventoryType.ArmoryHead, InventoryType.ArmoryBody, InventoryType.ArmoryHands,
            InventoryType.ArmoryLegs, InventoryType.ArmoryFeets, InventoryType.ArmoryEar, InventoryType.ArmoryNeck, InventoryType.ArmoryWrist, InventoryType.ArmoryRings,
        }),
        ("saddlebags", new[] { InventoryType.SaddleBag1, InventoryType.SaddleBag2, InventoryType.PremiumSaddleBag1, InventoryType.PremiumSaddleBag2 }),
        ("retainer", new[]
        {
            InventoryType.RetainerPage1, InventoryType.RetainerPage2, InventoryType.RetainerPage3, InventoryType.RetainerPage4, InventoryType.RetainerPage5,
            InventoryType.RetainerPage6, InventoryType.RetainerPage7, InventoryType.RetainerEquippedItems,
        }),
    };

    private static string DescribeBags()
    {
        var mgr = InventoryManager.Instance();
        if (mgr == null) return "no inventory";
        var parts = new List<string>();
        foreach (var (name, types) in BagGroups)
        {
            var n = 0;
            var loaded = 0;
            ulong hash = 1469598103934665603;
            foreach (var t in types)
            {
                var c = mgr->GetInventoryContainer(t);
                if (c == null || !c->IsLoaded) continue;
                loaded++;
                for (var i = 0; i < c->Size; i++)
                {
                    var it = c->GetInventorySlot(i);
                    if (it == null || it->IsEmpty()) continue;
                    n++;
                    hash = (hash ^ it->ItemId ^ ((ulong)it->Quantity << 24) ^ ((ulong)i << 32) ^ ((ulong)(int)t << 44)) * 1099511628211;
                    hash = (hash ^ it->GlamourId ^ ((ulong)it->GetStain(0) << 32) ^ ((ulong)it->GetStain(1) << 40) ^ ((ulong)(byte)it->Flags << 48)) * 1099511628211;
                }
            }
            parts.Add($"{name} {n} in {loaded}/{types.Length} loaded ({(uint)hash:X8})");
        }
        return string.Join(", ", parts);
    }

    /// <summary>The dyes in your bags: each stain and how many bottles.</summary>
    private string DescribeDyes()
    {
        var mgr = InventoryManager.Instance();
        if (mgr == null) return "no inventory";
        // By bottle: a Spectrum dye makes many colours, a colour's own bottle one.
        var counts = new SortedDictionary<uint, int>();
        foreach (var t in BagGroups[0].Types)
        {
            var c = mgr->GetInventoryContainer(t);
            if (c == null || !c->IsLoaded) continue;
            for (var i = 0; i < c->Size; i++)
            {
                var it = c->GetInventorySlot(i);
                if (it == null || it->IsEmpty()) continue;
                var id = it->GetBaseItemId();
                if (data.StainsOfItem(id).Count > 0) counts[id] = counts.GetValueOrDefault(id) + it->Quantity;
            }
        }
        return counts.Count == 0 ? "none in your bags" : string.Join(", ", counts.Select(kv =>
        {
            var makes = data.StainsOfItem(kv.Key);
            return $"{kv.Key} {data.ItemName(kv.Key)} ×{kv.Value} ({(makes.Count == 1 ? data.DyeName(makes[0]) : $"makes {makes.Count} colours")})";
        }));
    }

    /// <summary>The dresser's window: its tab and page, the piece right-clicked, search and filters, what it's doing.</summary>
    private static string DescribeDresserWindow()
    {
        var agent = AgentMiragePrismPrismBox.Instance();
        if (agent == null) return "no agent";
        var open = (AtkUnitBase*)Services.GameGui.GetAddonByName("MiragePrismPrismBox").Address != null;
        var head = $"window {(open ? "open" : "closed")}, data loaded {agent->IsDataLoaded}, tab {agent->TabIndex}, page {agent->PageIndex}";
        if (!open || agent->Data == null) return head;
        var d = agent->Data;
        return head + $", page picked {d->SelectedPageIndex}, items {d->ItemCount}, used {d->UsedSlots}, pending {d->PendingState}/{d->PendingStateStep} (last {d->LastProcessedStep}), "
                    + $"right-clicked [{d->TempContextItemIndex}] {d->TempContextItem.ItemId} slot {d->TempContextItem.Slot}, "
                    + $"search \"{d->SearchString}\" filter \"{d->FilterString}\" level {d->FilterLevel} sex {d->FilterSex} glamour {d->FilterGlamour} sort {d->SortType}";
    }

    /// <summary>The armoire's window: its category, the item picked, what it's waiting for.</summary>
    private static string DescribeArmoireWindow()
    {
        var agent = AgentCabinet.Instance();
        var ui = UIState.Instance();
        var state = ui != null ? ui->Cabinet.State.ToString() : "?";
        if (agent == null) return $"no agent, cabinet {state}";
        var open = (AtkUnitBase*)Services.GameGui.GetAddonByName("Cabinet").Address != null;
        return $"window {(open ? "open" : "closed")}, cabinet {state}, category {agent->SelectedCategoryIndex}, picked [{agent->SelectedIndex}] item {agent->SelectedItemId}, "
               + $"mode {agent->OpenMode}, items {agent->ItemCount}, pending update {agent->PendingUpdate}";
    }

    /// <summary>Storing an outfit set: the set picked, where its pieces are, whether it goes in as one.</summary>
    private static string DescribeSetConvert()
    {
        var agent = AgentMiragePrismPrismSetConvert.Instance();
        if (agent == null) return "no agent";
        var open = (AtkUnitBase*)Services.GameGui.GetAddonByName("MiragePrismPrismSetConvert").Address != null;
        if (!open || agent->Data == null) return $"window {(open ? "open" : "closed")}";
        var d = agent->Data;
        var items = new List<string>();
        var n = (int)Math.Min(d->NumItemsInSet, (uint)d->Items.Length);
        for (var i = 0; i < n; i++)
        {
            ref var it = ref d->Items[i];
            items.Add($"{it.ItemId} col {it.MirageStoreSetItemColumn} at {it.InventoryType}:{it.Slot}");
        }
        return $"window open, source {d->SourceItemId} at {d->SourceContainer}:{d->SourceSlot}, state {d->State}, set picked {d->SelectedSetIndex}, context {d->ContextMenuItemIndex}, "
               + $"dresser index {d->PrismBoxIndex}, store into existing {d->StoreInExistingOutfit}, can store {d->EnableStoring}, sets {d->ItemSetCount}, pieces {d->NumItemsInSet}: {string.Join("; ", items)}";
    }

    /// <summary>The fitting room: its list, what its view draws, and what's hidden.</summary>
    private static string DescribeTryOn()
    {
        var agent = AgentTryon.Instance();
        if (agent == null) return "no agent";
        var open = (AtkUnitBase*)Services.GameGui.GetAddonByName("Tryon").Address != null;
        if (!open) return "window closed";
        var v = &agent->CharaView;
        return $"window open, loaded {v->CharacterLoaded}, state {v->State}, keep tried on {agent->SaveDeleteOutfit}, hide others {v->HideOtherEquipment}, headgear hidden {v->HeadgearHidden}, "
               + $"weapon hidden {v->WeaponHidden}, {FittingRoom.Listed()}";
    }

    /// <summary>Retainers: whether their list is ready, the one picked, how many items it holds.</summary>
    private static string DescribeRetainers()
    {
        var m = RetainerManager.Instance();
        if (m == null) return "no retainer manager";
        if (!m->IsReady) return "not ready";
        var active = m->GetActiveRetainer();
        return $"ready, last picked {m->LastSelectedRetainerId:X}" + (active != null ? $", active has {active->ItemCount} items" : ", none active");
    }

    private static string DescribeWindows()
    {
        var open = new List<string>();
        foreach (var name in GlamourWindows)
        {
            try
            {
                var a = (AtkUnitBase*)Services.GameGui.GetAddonByName(name).Address;
                if (a != null) open.Add(a->IsVisible ? name : name + "(hidden)");
            }
            catch
            {
                // Not there.
            }
        }
        return open.Count == 0 ? "none of the glamour windows" : string.Join(" ", open);
    }

    // ---------------------------------------------------------------------------------------------
    // What the game says and does.

    private bool CallbackDetour(AtkUnitBase* self, uint count, AtkValue* values, bool close)
    {
        try
        {
            if (Recording && self != null)
            {
                var name = self->NameString;
                if (!IsNoise(name))
                {
                    Add($"CALLBACK {name} close={close} [{AtkText.Describe(values, count)}]");
                    if (name is "ContextMenu" or "ContextIconMenu" or "SelectString" or "SelectIconString")
                    {
                        var words = AtkText.Strings(self, 40);
                        if (words.Count > 0) Add("  its words: " + string.Join(" | ", words.Select(Cut)));
                    }
                }
            }
        }
        catch
        {
            // Never let watching break the game's own call.
        }
        return callbackHook!.Original(self, count, values, close);
    }

    private AtkValue* AgentDetour(Hook<AgentInterface.Delegates.ReceiveEvent> hook, string label, AgentInterface* self, AtkValue* returnValue, AtkValue* values, uint valueCount, ulong eventKind)
    {
        inCall ??= new HashSet<nint>();
        var entered = inCall.Add((nint)self);
        string? name = null;
        try
        {
            if (Recording && entered && self != null && agentNames.TryGetValue((nint)self, out name))
                Add($"AGENT {name}{label} kind={eventKind} [{AtkText.Describe(values, valueCount, hideText: name == "Context")}]");
        }
        catch
        {
            // Writing it down mustn't stop the game's own call.
        }
        AtkValue* result;
        try
        {
            result = hook.Original(self, returnValue, values, valueCount, eventKind);
        }
        finally
        {
            if (entered) inCall.Remove((nint)self);
        }
        try
        {
            if (name != null && Recording && result != null && result->Type != 0)
                Add($"  AGENT {name}{label} returned [{AtkText.Describe(result, 1, hideText: name == "Context")}]");
            // A menu opened on an item: which item, from where.
            if (name == "InventoryContext" && Recording) Add("  " + DescribeInventoryContext());
        }
        catch
        {
            // As above.
        }
        return result;
    }

    /// <summary>The item a menu was opened on: its container and slot, the item, the window it came from, how many entries.</summary>
    private static string DescribeInventoryContext()
    {
        var a = AgentInventoryContext.Instance();
        if (a == null) return "context: no agent";
        // A menu on someone, or chat: the item menu's agent isn't open, and what it holds is the last item's.
        if (!a->AgentInterface.IsAgentActive()) return "context: not an item's menu";
        var item = a->TargetInventorySlot;
        return $"context: {a->TargetInventoryId}:{a->TargetInventorySlotId} item {(item != null ? item->ItemId : 0)} flags {a->TargetInventoryFlags}, owner window {a->OwnerAddonId}, "
               + $"{a->ContextItemCount} entries from {a->ContexItemStartIndex}";
    }

    private void OnOpen(AddonEvent type, AddonArgs args)
    {
        if (!Recording || IsNoise(args.AddonName)) return;
        var a = (AtkUnitBase*)args.Addon.Address;
        var full = Glamour.Contains(args.AddonName);
        var words = a != null && !Private.Contains(args.AddonName) ? AtkText.Strings(a, full ? 600 : 10) : new List<string>();
        if (full) lastWords[args.AddonName] = string.Join("|", words).GetHashCode();
        Add($"OPEN {args.AddonName}{(a != null ? $" ({a->AtkValuesCount} values)" : string.Empty)}{(words.Count > 0 ? " " + string.Join(" ", words.Select(Cut)) : string.Empty)}");
        if (args.AddonName is "ContextMenu" or "ContextIconMenu") Add("  " + DescribeInventoryContext());
    }

    /// <summary>A glamour window refreshed: its words again, when they changed.</summary>
    private void OnRefresh(AddonEvent type, AddonArgs args)
    {
        if (!Recording) return;
        var a = (AtkUnitBase*)args.Addon.Address;
        if (a == null) return;
        if (Private.Contains(args.AddonName))
        {
            Add($"REFRESH {args.AddonName} ({a->AtkValuesCount} values; its words aren't written down: they're someone else's)");
            return;
        }
        // The values the refresh brought (not what the window held before it).
        var sent = args is AddonRefreshArgs r && r.AtkValues != 0 ? AtkText.Describe((AtkValue*)r.AtkValues, r.AtkValueCount, 120) : string.Empty;
        var words = AtkText.Strings(a, 600);
        var hash = (string.Join("|", words) + sent).GetHashCode();
        if (lastWords.TryGetValue(args.AddonName, out var last) && last == hash)
        {
            Add($"REFRESH {args.AddonName} (the same)");
            return;
        }
        lastWords[args.AddonName] = hash;
        Add($"REFRESH {args.AddonName} ({a->AtkValuesCount} values){(sent.Length > 0 ? $" sent [{sent}]" : string.Empty)} {string.Join(" ", words.Select(Cut))}");
    }

    private void OnClose(AddonEvent type, AddonArgs args)
    {
        if (Recording && !IsNoise(args.AddonName)) Add($"CLOSE {args.AddonName}");
    }

    private void OnErrorToast(ref SeString message, ref bool isHandled) => Add($"ERROR TOAST {message.TextValue}");

    private void OnToast(ref SeString message, ref Dalamud.Game.Gui.Toast.ToastOptions options, ref bool isHandled) => Add($"TOAST {message.TextValue}");

    private void OnQuestToast(ref SeString message, ref Dalamud.Game.Gui.Toast.QuestToastOptions options, ref bool isHandled) => Add($"QUEST TOAST {message.TextValue}");

    private static readonly HashSet<XivChatType> ChatKinds = new()
    {
        XivChatType.SystemMessage, XivChatType.ErrorMessage, XivChatType.SystemError, XivChatType.GatheringSystemMessage, XivChatType.Notice,
        XivChatType.Echo, XivChatType.Debug, XivChatType.Urgent,
    };

    private void OnChat(Dalamud.Game.Chat.IHandleableChatMessage message)
    {
        try
        {
            if (!Recording || !ChatKinds.Contains(message.LogKind)) return;
            Add($"CHAT [{message.LogKind}] {message.Message.TextValue}");
        }
        catch
        {
            // Not worth breaking chat over.
        }
    }

    private void OnLogMessage(Dalamud.Game.Chat.ILogMessage message)
    {
        try
        {
            if (!Recording) return;
            Add($"LOGMSG {message.LogMessageId} {message.FormatLogMessageForDebugging().ExtractText()}");
        }
        catch
        {
            // As above.
        }
    }

    private static string Cut(string w) => w.Length > WordsAtMost ? w[..WordsAtMost] + "…" : w;

    /// <summary>Writes a line (any thread). The same words again are counted, not written again.</summary>
    private void Add(string line, bool cut = true)
    {
        lock (lines)
        {
            if (started == default) started = DateTime.UtcNow;
            if (cut && line.Length > LineAtMost) line = $"{line[..LineAtMost]}… (+{line.Length - LineAtMost:N0} characters)";
            if (line == lastBody && lines.Count > headerCount)
            {
                repeats++;
                return;
            }
            if (repeats > 0)
            {
                lines.Add($"{(DateTime.UtcNow - started).TotalSeconds,8:0.000}    (the line above {repeats + 1} times in all)");
                repeats = 0;
            }
            lastBody = line;
            lines.Add($"{(DateTime.UtcNow - started).TotalSeconds,8:0.000}  {line}");
            // The oldest lines after the header go, a batch at a time.
            if (lines.Count > MaxLines + 500)
            {
                var drop = lines.Count - MaxLines;
                lines.RemoveRange(headerCount, drop);
                dropped += drop;
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Giving it to you.

    public string AsText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Glamour Studio recording");
        lock (lines)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (i == Math.Min(headerCount, lines.Count) && dropped > 0) sb.AppendLine($"          ({dropped:N0} earlier lines let go of: a recording keeps the last {MaxLines:N0}.)");
                sb.AppendLine(lines[i]);
            }
            if (repeats > 0) sb.AppendLine($"              (the line above {repeats + 1} times in all)");
        }
        return sb.ToString();
    }

    /// <summary>Saves the recording as a text file in the plugin's folder; returns its path (or null, with why).</summary>
    public string? Save(string text, out string? why)
    {
        why = null;
        try
        {
            var dir = Path.Combine(Services.PluginInterface.GetPluginConfigDirectory(), "recordings");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"gstudio-{DateTime.Now:yyyyMMdd-HHmmss-fff}.txt");
            File.WriteAllText(path, text, new UTF8Encoding(false));
            SavedTo = path;
            HandedOver = true;
            return path;
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, "Glamour Studio couldn't save the recording");
            why = $"Couldn't save it ({ex.Message})";
            return null;
        }
    }

    public void Dispose()
    {
        // (No END state while the plugin unloads: the game may be closing.)
        Stop(writeState: false);
        callbackHook?.Dispose();
        foreach (var h in agentHooks.Values) h.Dispose();
        agentHooks.Clear();
    }
}
