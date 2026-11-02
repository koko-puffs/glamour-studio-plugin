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
        "MiragePrismMiragePlateConfirm", "MiragePrismE", "Cabinet", "CabinetWithdraw", "CharacterInspect", "GearSetList", "Character",
        "Tryon", "ColorantColoring", "ColorantEquipment", "ContextMenu", "ContextIconMenu", "SelectYesno", "SelectString", "SelectIconString",
        "InventoryBuddy", "InventoryBuddy2", "InventoryRetainer", "InventoryRetainerLarge", "ArmouryBoard",
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
        (AgentId.Colorant, "Colorant"),
    };

    /// <summary>The recording going now (for the game's code to write PLUGIN lines into), or null.</summary>
    public static Recorder? Active { get; private set; }

    /// <summary>Writes a PLUGIN line into the recording going, if one is.</summary>
    public static void Log(string line) => Active?.Add("PLUGIN " + line);

    /// <summary>Writes a PLUGIN line, if this is recording.</summary>
    public void Note(string line)
    {
        if (Recording) Add("PLUGIN " + line);
    }

    private readonly GameData data;
    private readonly Hook<AtkUnitBase.Delegates.FireCallback>? callbackHook;
    private readonly Dictionary<nint, Hook<AgentInterface.Delegates.ReceiveEvent>> agentHooks = new();
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

    public int LineCount => lines.Count + dropped;

    public int Marks { get; private set; }

    /// <summary>Where the last recording was saved, if it was.</summary>
    public string? SavedTo { get; private set; }

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

    /// <summary>The glamour agents' event functions, hooked once (framework thread, when recording starts).</summary>
    private void HookAgents()
    {
        try
        {
            var module = AgentModule.Instance();
            if (module == null) return;
            foreach (var (id, name) in Agents)
            {
                var agent = module->GetAgentByInternalId(id);
                if (agent == null || agent->VirtualTable == null) continue;
                agentNames[(nint)agent] = name;
                var fn = (nint)agent->VirtualTable->ReceiveEvent;
                if (fn == 0 || agentHooks.ContainsKey(fn)) continue;
                try
                {
                    // Each hook its own detour, so the original called is always this one's (a function
                    // several agents share, or one an agent calls as its base, still reaches the game).
                    Hook<AgentInterface.Delegates.ReceiveEvent>? hook = null;
                    AgentInterface.Delegates.ReceiveEvent detour = (a, r, v, c, k) => AgentDetour(hook!, a, r, v, c, k);
                    hook = Services.GameInterop.HookFromAddress(fn, detour);
                    detours.Add(detour);
                    agentHooks[fn] = hook;
                }
                catch (Exception ex)
                {
                    Services.Log.Warning(ex, $"Glamour Studio's recorder couldn't hook {name}'s events");
                }
            }
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio's recorder couldn't find the glamour agents");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Starting and stopping.

    /// <summary>Starts a recording (framework thread).</summary>
    public void Start()
    {
        if (Recording) return;
        lines.Clear();
        lastState.Clear();
        lastWords.Clear();
        dropped = 0;
        Marks = 0;
        SavedTo = null;
        started = DateTime.UtcNow;
        nextState = DateTime.MinValue;
        Recording = true;
        Active = this;
        Add($"Recording started {DateTime.Now:yyyy-MM-dd HH:mm:ss} (local). Glamour Studio {typeof(Recorder).Assembly.GetName().Version}, client {Services.ClientState.ClientLanguage}.");
        foreach (var h in Header?.Invoke() ?? Array.Empty<string>()) Add(h);
        if (SetupError != null) Add(SetupError);
        HookAgents();
        callbackHook?.Enable();
        foreach (var h in agentHooks.Values) h.Enable();
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, OnOpen);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, OnClose);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, GlamourWindows, OnRefresh);
        Services.Toasts.ErrorToast += OnErrorToast;
        Services.Toasts.Toast += OnToast;
        Services.Toasts.QuestToast += OnQuestToast;
        Services.Chat.ChatMessage += OnChat;
        Services.Chat.LogMessage += OnLogMessage;
        listening = true;
        Add("Write a MARK before each step (Settings → Making a report, or /gstudio mark <what you'll do>), do it by hand, then stop and copy or save.");
        WriteAllState("START");
    }

    /// <summary>Stops the recording (framework thread).</summary>
    public void Stop()
    {
        if (!Recording) return;
        WriteAllState("END");
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

    /// <summary>Every value the glamour windows hold now, and the whole state (framework thread).</summary>
    public void DumpWindows()
    {
        if (!Recording) return;
        Add("DUMP: every value the open glamour windows hold, and the whole state.");
        foreach (var name in GlamourWindows)
        {
            var a = (AtkUnitBase*)Services.GameGui.GetAddonByName(name).Address;
            if (a == null) continue;
            Add("VALUES " + AtkText.DumpValues(name));
        }
        Add("EDITOR DETAIL " + PlateEditor.Describe(data, detail: true));
        Add("DRESSER DETAIL " + DescribeDresser(detail: true));
        Add("PLATES DETAIL " + DescribePlates(detail: true));
        Add("ARMOIRE DETAIL " + DescribeArmoire(detail: true));
        WriteAllState("DUMP");
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
        var who = Services.Objects.SearchByEntityId(agent->CurrentEntityId)?.Name.TextValue ?? "(not in the object table)";
        return head + $", who {who}, {pieces.Count} pieces: {string.Join("; ", pieces)}";
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
                    hash = (hash ^ it->ItemId ^ ((ulong)i << 32) ^ ((ulong)(int)t << 44)) * 1099511628211;
                }
            }
            parts.Add($"{name} {n} in {loaded}/{types.Length} loaded ({(uint)hash:X8})");
        }
        return string.Join(", ", parts);
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

    private AtkValue* AgentDetour(Hook<AgentInterface.Delegates.ReceiveEvent> hook, AgentInterface* self, AtkValue* returnValue, AtkValue* values, uint valueCount, ulong eventKind)
    {
        try
        {
            if (Recording && self != null && agentNames.TryGetValue((nint)self, out var name))
                Add($"AGENT {name} kind={eventKind} [{AtkText.Describe(values, valueCount)}]");
        }
        catch
        {
            // Never let watching break the game's own call.
        }
        var result = hook.Original(self, returnValue, values, valueCount, eventKind);
        try
        {
            if (Recording && self != null && agentNames.TryGetValue((nint)self, out var name) && result != null)
                Add($"  AGENT {name} returned [{AtkText.Describe(result, 1)}]");
        }
        catch
        {
            // As above.
        }
        return result;
    }

    private void OnOpen(AddonEvent type, AddonArgs args)
    {
        if (!Recording || IsNoise(args.AddonName)) return;
        var a = (AtkUnitBase*)args.Addon.Address;
        var full = Glamour.Contains(args.AddonName);
        var words = a != null ? AtkText.Strings(a, full ? 600 : 10) : new List<string>();
        if (full) lastWords[args.AddonName] = string.Join("|", words).GetHashCode();
        Add($"OPEN {args.AddonName}{(a != null ? $" ({a->AtkValuesCount} values)" : string.Empty)}{(words.Count > 0 ? " " + string.Join(" ", words.Select(Cut)) : string.Empty)}");
    }

    /// <summary>A glamour window refreshed: its words again, when they changed.</summary>
    private void OnRefresh(AddonEvent type, AddonArgs args)
    {
        if (!Recording) return;
        var a = (AtkUnitBase*)args.Addon.Address;
        if (a == null) return;
        var words = AtkText.Strings(a, 600);
        var hash = string.Join("|", words).GetHashCode();
        if (lastWords.TryGetValue(args.AddonName, out var last) && last == hash)
        {
            Add($"REFRESH {args.AddonName} (same words)");
            return;
        }
        lastWords[args.AddonName] = hash;
        Add($"REFRESH {args.AddonName} ({a->AtkValuesCount} values) {string.Join(" ", words.Select(Cut))}");
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

    private void Add(string line)
    {
        if (started == default) started = DateTime.UtcNow;
        if (line.Length > LineAtMost) line = $"{line[..LineAtMost]}… (+{line.Length - LineAtMost:N0} characters)";
        lines.Add($"{(DateTime.UtcNow - started).TotalSeconds,8:0.000}  {line}");
        // The oldest lines after the header go, a batch at a time.
        if (lines.Count > MaxLines + 500)
        {
            var cut = lines.Count - MaxLines;
            lines.RemoveRange(HeaderLines, cut);
            dropped += cut;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Giving it to you.

    public string AsText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Glamour Studio recording");
        for (var i = 0; i < lines.Count; i++)
        {
            if (i == Math.Min(HeaderLines, lines.Count) && dropped > 0) sb.AppendLine($"          ({dropped:N0} earlier lines let go of: a recording keeps the last {MaxLines:N0}.)");
            sb.AppendLine(lines[i]);
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
            var path = Path.Combine(dir, $"gstudio-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, text, new UTF8Encoding(false));
            SavedTo = path;
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
        Stop();
        callbackHook?.Dispose();
        foreach (var h in agentHooks.Values) h.Dispose();
        agentHooks.Clear();
    }
}
