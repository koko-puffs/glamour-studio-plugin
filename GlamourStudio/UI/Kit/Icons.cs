using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourStudio.UI.Model;

namespace GlamourStudio.UI;

/// <summary>The Lucide icons the window uses (the site's own icon set).</summary>
public enum Icon
{
    Play,
    Pause,
    Square,
    Eye,
    List,
    Sliders,
    X,
    ChevronRight,
    ChevronDown,
    MapPin,
    Search,
    Clipboard,
    Droplet,
    Package,
    Move,
    House,
    Upload,
    Info,
    Alert,
    Refresh,
    Check,
    ArrowLeft,
    Shelf,
    Minus,
    Plus,
    Backpack,
    Bell,
    Store,
    Scale,
    Hammer,
    Flask,
    Brick,
    Sofa,
    Undo,
    Send,
    Save,
    File,
    Link,
    AlertTriangle,
    Camera,
    Heart,
    Bookmark,
    Stamp,
    ExternalLink,
    Inbox,
    ChevronLeft,
    Swords,
    Trash,
    Folder,
    Users,
    Clock,
    FileDown,
    Map,
    Sparkles,
    Hanger,
    Shirt,
    Layers,
    Paint,
    Archive,
    User,
    ArrowRight,
    Record,

    // The Character Sheet mock-up's (docs/redesign/assets/kit.js).
    ChevronUp,
    Box,
    Star,
    Key,
    Globe,
    Shield,
    Copy,
    Bag,
    SlidersH,
    RefreshCw,
}

/// <summary>
/// Lucide's outlined icons, drawn with ImGui's draw lists from their SVG paths
/// (<c>viewBox="0 0 24 24"</c>, <c>stroke-width:1.8</c>, round caps and joins). The stroke is in the
/// icon's 24-unit grid as in SVG, so a 16px icon has the mockup's 1.2px line and a 24px one 1.8px.
///
/// The paths are Lucide's (https://lucide.dev), under the ISC License: copyright (c) for portions of
/// Lucide are held by Cole Bemis 2013-2022 as part of Feather (MIT); all other copyright (c) for
/// Lucide are held by Lucide Contributors 2022. Permission to use, copy, modify, and/or distribute
/// this software for any purpose with or without fee is hereby granted, provided that the above
/// copyright notice and this permission notice appear in all copies.
/// </summary>
public static class Icons
{
    /// <summary>Each icon's SVG elements as path data (circles, rects and lines written as paths).</summary>
    private static readonly Dictionary<Icon, (string[] Paths, bool Filled)> Source = new()
    {
        [Icon.Play] = (new[] { "M7 4 20 12 7 20 7 4Z" }, true),
        [Icon.Pause] = (new[] { Rect(14, 4, 4, 16, 1), Rect(6, 4, 4, 16, 1) }, false),
        [Icon.Square] = (new[] { Rect(5, 5, 14, 14, 2) }, false),
        [Icon.Eye] = (new[] { "M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z", Circle(12, 12, 3) }, false),
        [Icon.List] = (new[] { "M8 6H21", "M8 12H21", "M8 18H21", "M3 6H3.01", "M3 12H3.01", "M3 18H3.01" }, false),
        [Icon.Sliders] = (new[]
        {
            "M4 21v-7", "M4 10V3", "M12 21v-9", "M12 8V3", "M20 21v-5", "M20 12V3", "M1 14h6", "M9 8h6", "M17 16h6",
        }, false),
        [Icon.X] = (new[] { "M18 6 6 18", "m6 6 12 12" }, false),
        [Icon.ChevronRight] = (new[] { "m9 18 6-6-6-6" }, false),
        [Icon.ChevronDown] = (new[] { "m6 9 6 6 6-6" }, false),
        [Icon.MapPin] = (new[] { "M20 10c0 4.993-5.539 10.193-7.399 11.799a1 1 0 0 1-1.202 0C9.539 20.193 4 14.993 4 10a8 8 0 0 1 16 0", Circle(12, 10, 3) }, false),
        [Icon.Search] = (new[] { Circle(11, 11, 8), "m21 21-4.3-4.3" }, false),
        [Icon.Clipboard] = (new[] { Rect(8, 2, 8, 4, 1), "M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2" }, false),
        [Icon.Droplet] = (new[] { "M12 22a7 7 0 0 0 7-7c0-2-1-3.9-3-5.5s-3.5-4-4-6.5c-.5 2.5-2 4.9-4 6.5C6 11.1 5 13 5 15a7 7 0 0 0 7 7z" }, false),
        [Icon.Package] = (new[]
        {
            "m7.5 4.27 9 5.15",
            "M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z",
            "m3.3 7 8.7 5 8.7-5",
            "M12 22V12",
        }, false),
        [Icon.Move] = (new[] { "M5 9l-3 3 3 3", "M9 5l3-3 3 3", "M15 19l-3 3-3-3", "M19 9l3 3-3 3", "M2 12h20", "M12 2v20" }, false),
        [Icon.House] = (new[]
        {
            "M15 21v-8a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v8",
            "M3 10a2 2 0 0 1 .709-1.528l7-5.999a2 2 0 0 1 2.582 0l7 5.999A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z",
        }, false),
        [Icon.Upload] = (new[] { "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4", "M17 8 12 3 7 8", "M12 3V15" }, false),
        [Icon.Info] = (new[] { Circle(12, 12, 10), "M12 16v-4", "M12 8h.01" }, false),
        [Icon.Alert] = (new[] { Circle(12, 12, 10), "M12 8V12", "M12 16H12.01" }, false),
        [Icon.Refresh] = (new[] { "M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8", "M3 3v5h5" }, false),
        [Icon.Check] = (new[] { "M20 6 9 17l-5-5" }, false),
        [Icon.ArrowLeft] = (new[] { "m12 19-7-7 7-7", "M19 12H5" }, false),
        [Icon.ArrowRight] = (new[] { "M5 12h14", "m12 5 7 7-7 7" }, false),
        [Icon.Record] = (new[] { Circle(12, 12, 6) }, true),
        [Icon.Shelf] = (new[] { Rect(4, 3, 16, 18, 1), "M4 9h16", "M4 15h16" }, false),
        [Icon.Minus] = (new[] { "M5 12h14" }, false),
        [Icon.Plus] = (new[] { "M5 12h14", "M12 5v14" }, false),
        [Icon.Backpack] = (new[]
        {
            "M4 10a4 4 0 0 1 4-4h8a4 4 0 0 1 4 4v10a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2Z",
            "M8 10h8", "M8 18h8", "M8 22v-6a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v6", "M9 6V4a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2",
        }, false),
        // The 0.6 flow mock-up's (docs/mockups/flow.html): the Get stage's sources, Finish's fixtures, the journey.
        [Icon.Bell] = (new[] { "M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9", "M10.3 21a1.9 1.9 0 0 0 3.4 0" }, false),
        [Icon.Store] = (new[] { "M3 9 4.5 4h15L21 9", "M3 9h18v2a3 3 0 0 1-6 0 3 3 0 0 1-6 0 3 3 0 0 1-6 0z", "M5 13v7h14v-7", "M10 20v-5h4v5" }, false),
        [Icon.Scale] = (new[] { "M12 3v18", "M5 21h14", "M3 7h18", "M6 7l-3 7a3 3 0 0 0 6 0z", "M18 7l-3 7a3 3 0 0 0 6 0z" }, false),
        [Icon.Hammer] = (new[] { "m15 12-8.5 8.5a2.1 2.1 0 0 1-3-3L12 9", "M17.6 15 22 10.6", "M20.9 11.7 12.3 3.1a2 2 0 0 0-2.8 0L7 5.6l7.4 7.4" }, false),
        [Icon.Flask] = (new[] { "M9 3h6", "M10 3v6L4.5 19a1.5 1.5 0 0 0 1.3 2h12.4a1.5 1.5 0 0 0 1.3-2L14 9V3", "M7 15h10" }, false),
        [Icon.Brick] = (new[] { "M3 3h18v18H3z", "M3 9h18", "M3 15h18", "M9 3v6", "M15 9v6", "M9 15v6" }, false),
        [Icon.Sofa] = (new[] { "M20 9V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v3", "M2 16a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-5a2 2 0 0 0-4 0v1.5H6V11a2 2 0 0 0-4 0z", "M4 18v2", "M20 18v2" }, false),
        [Icon.Undo] = (new[] { "M9 14 4 9l5-5", "M4 9h10.5a5.5 5.5 0 0 1 0 11H11" }, false),
        [Icon.Send] = (new[] { "m22 2-7 20-4-9-9-4z", "M22 2 11 13" }, false),
        [Icon.Save] = (new[] { "M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z", "M17 21v-8H7v8", "M7 3v5h8" }, false),
        [Icon.File] = (new[] { "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z", "M14 2v6h6" }, false),
        [Icon.Link] = (new[] { "M10 13a5 5 0 0 0 7.5.5l3-3a5 5 0 0 0-7-7l-1.7 1.7", "M14 11a5 5 0 0 0-7.5-.5l-3 3a5 5 0 0 0 7 7l1.7-1.7" }, false),
        [Icon.AlertTriangle] = (new[] { "M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z", "M12 9v4", "M12 17h.01" }, false),
        // 0.7's site link: Take a shot, the visitor panel's like, save and stamp, Open on the site, From the site.
        [Icon.Camera] = (new[] { "M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3z", Circle(12, 13, 3) }, false),
        [Icon.Heart] = (new[] { "M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z" }, false),
        [Icon.Bookmark] = (new[] { "m19 21-7-4-7 4V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v16z" }, false),
        [Icon.Stamp] = (new[]
        {
            "M5 22h14",
            "M19.27 13.73A2.5 2.5 0 0 0 17.5 13h-11A2.5 2.5 0 0 0 4 15.5V17a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1v-1.5c0-.66-.26-1.3-.73-1.77Z",
            "M14 13V8.5C14 7 15 7 15 5a3 3 0 0 0-6 0c0 2 1 2 1 3.5V13",
        }, false),
        [Icon.ExternalLink] = (new[] { "M15 3h6v6", "M10 14 21 3", "M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" }, false),
        [Icon.ChevronLeft] = (new[] { "m15 18-6-6 6-6" }, false),
        // Swords: the armoury chest.
        [Icon.Swords] = (new[]
        {
            "m13 19 6-6", "M14.5 17.5 3.586 6.586A2 2 0 0 1 3 5.172V3h2.172a2 2 0 0 1 1.414.586L17.5 14.5",
            "m14.828 6.172 2.586-2.586A2 2 0 0 1 18.828 3H21v2.172a2 2 0 0 1-.586 1.414l-2.586 2.586",
            "m16 16 4 4", "m19 21 2-2", "m5 14 4 4", "m5 21-2-2", "M7.5 16.5 4 20",
        }, false),
        [Icon.Trash] = (new[] { "M3 6h18", "M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6", "M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2", "M10 11v6", "M14 11v6" }, false),
        [Icon.Folder] = (new[] { "M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z" }, false),
        [Icon.Users] = (new[] { "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2", Circle(9, 7, 4), "M22 21v-2a4 4 0 0 0-3-3.87", "M16 3.13a4 4 0 0 1 0 7.75" }, false),
        [Icon.Clock] = (new[] { Circle(12, 12, 10), "M12 6v6l4 2" }, false),
        [Icon.FileDown] = (new[] { "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z", "M14 2v6h6", "M12 18v-6", "m9 15 3 3 3-3" }, false),
        [Icon.Map] = (new[] { "M14.106 5.553a2 2 0 0 0 1.788 0l3.659-1.83A1 1 0 0 1 21 4.619v12.764a1 1 0 0 1-.553.894l-4.553 2.277a2 2 0 0 1-1.788 0l-4.212-2.106a2 2 0 0 0-1.788 0l-3.659 1.83A1 1 0 0 1 3 19.381V6.618a1 1 0 0 1 .553-.894l4.553-2.277a2 2 0 0 1 1.788 0z", "M15 5.764v15", "M9 3.236v15" }, false),
        [Icon.Sparkles] = (new[] { "M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z", "M20 3v4", "M22 5h-4" }, false),
        [Icon.Inbox] = (new[] { "M22 12h-6l-2 3h-4l-2-3H2", "M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z" }, false),
        // Glamour Studio's: its hanger (the mark), what's worn, plates, dyes, the armoire, a character.
        [Icon.Hanger] = (new[] { "M10.05 7.65a1.95 1.95 0 1 1 2.925 1.6875c-.6.345-.975.825-.975 1.5v.7125", "M12 11.55 4.2 16.425c-.5625.3525-.315 1.2.345 1.2h14.925c.66 0 .9-.8475.345-1.2Z" }, false),
        [Icon.Shirt] = (new[] { "M20.38 3.46 16 2a4 4 0 0 1-8 0L3.62 3.46a2 2 0 0 0-1.34 2.23l.58 3.47a1 1 0 0 0 .99.84H6v10c0 1.1.9 2 2 2h8a2 2 0 0 0 2-2V10h2.15a1 1 0 0 0 .99-.84l.58-3.47a2 2 0 0 0-1.34-2.23z" }, false),
        [Icon.Layers] = (new[]
        {
            "M12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83Z",
            "m22 17.65-9.17 4.16a2 2 0 0 1-1.66 0L2 17.65", "m22 12.65-9.17 4.16a2 2 0 0 1-1.66 0L2 12.65",
        }, false),
        [Icon.Paint] = (new[]
        {
            "M12 22a1 1 0 0 1 0-20 10 9 0 0 1 10 9 5 5 0 0 1-5 5h-2.25a1.75 1.75 0 0 0-1.4 2.8l.3.4a1.75 1.75 0 0 1-1.4 2.8z",
            Circle(13.5f, 6.5f, 0.5f), Circle(17.5f, 10.5f, 0.5f), Circle(6.5f, 12.5f, 0.5f), Circle(8.5f, 7.5f, 0.5f),
        }, false),
        [Icon.Archive] = (new[] { Rect(2, 3, 20, 5, 1), "M4 8v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8", "M10 12h4" }, false),
        [Icon.User] = (new[] { Circle(12, 8, 5), "M20 21a8 8 0 0 0-16 0" }, false),
        // The Character Sheet mock-up's: the bar's Closet (a box) and Settings (sliders across), Refresh's two arrows.
        [Icon.ChevronUp] = (new[] { "m18 15-6-6-6 6" }, false),
        [Icon.Box] = (new[] { "M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z", "m3.3 7 8.7 5 8.7-5", "M12 22V12" }, false),
        [Icon.Star] = (new[] { "M11.525 2.295a.53.53 0 0 1 .95 0l2.31 4.679a2.123 2.123 0 0 0 1.595 1.16l5.166.756a.53.53 0 0 1 .294.904l-3.736 3.638a2.123 2.123 0 0 0-.611 1.878l.882 5.14a.53.53 0 0 1-.771.56l-4.618-2.428a2.122 2.122 0 0 0-1.973 0L6.396 21.01a.53.53 0 0 1-.77-.56l.881-5.139a2.122 2.122 0 0 0-.611-1.879L2.16 9.795a.53.53 0 0 1 .294-.906l5.165-.755a2.122 2.122 0 0 0 1.597-1.16z" }, false),
        [Icon.Key] = (new[] { "m15.5 7.5 2.3 2.3a1 1 0 0 0 1.4 0l2.1-2.1a1 1 0 0 0 0-1.4L19 4", "m21 2-9.6 9.6", Circle(7.5f, 15.5f, 5.5f) }, false),
        [Icon.Globe] = (new[] { Circle(12, 12, 10), "M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20", "M2 12h20" }, false),
        [Icon.Shield] = (new[] { "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z" }, false),
        [Icon.Copy] = (new[] { Rect(8, 8, 14, 14, 2), "M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2" }, false),
        [Icon.Bag] = (new[] { "M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z", "M3 6h18", "M16 10a4 4 0 0 1-8 0" }, false),
        [Icon.SlidersH] = (new[] { "M21 4h-7", "M10 4H3", "M21 12h-9", "M8 12H3", "M21 20h-5", "M12 20H3", "M14 2v4", "M8 10v4", "M16 18v4" }, false),
        [Icon.RefreshCw] = (new[] { "M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8", "M21 3v5h-5", "M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16", "M8 16H3v5" }, false),
    };

    private static readonly Dictionary<Icon, (List<SvgPath.Sub> Subs, bool Filled)> Parsed = new();

    private static string Circle(float cx, float cy, float r) => FormattableString.Invariant($"M{cx - r} {cy}a{r} {r} 0 1 0 {2 * r} 0a{r} {r} 0 1 0 {-2 * r} 0Z");

    private static string Rect(float x, float y, float w, float h, float rx) => FormattableString.Invariant(
        $"M{x + rx} {y}h{w - 2 * rx}a{rx} {rx} 0 0 1 {rx} {rx}v{h - 2 * rx}a{rx} {rx} 0 0 1 {-rx} {rx}h{-(w - 2 * rx)}a{rx} {rx} 0 0 1 {-rx} {-rx}v{-(h - 2 * rx)}a{rx} {rx} 0 0 1 {rx} {-rx}Z");

    private static (List<SvgPath.Sub> Subs, bool Filled) Get(Icon icon)
    {
        if (Parsed.TryGetValue(icon, out var p)) return p;
        var (paths, filled) = Source[icon];
        var subs = new List<SvgPath.Sub>();
        foreach (var d in paths) subs.AddRange(SvgPath.Parse(d));
        return Parsed[icon] = (subs, filled);
    }

    /// <summary>Draws an icon with its top-left at <paramref name="pos"/>, <paramref name="size"/> pixels square (already scaled).</summary>
    /// <param name="stroke">The stroke in the icon's 24-unit grid (Lucide's strokeWidth): 1.8, or 1.6 at 18px and up.</param>
    public static void Draw(ImDrawListPtr dl, Icon icon, Vector2 pos, float size, Vector4 color, float stroke = 1.8f)
    {
        var (subs, filled) = Get(icon);
        var k = size / 24f;
        var col = Palette.U32(color);
        var thickness = stroke * k;
        Span<Vector2> buffer = stackalloc Vector2[128];
        foreach (var sub in subs)
        {
            var n = Math.Min(sub.Points.Count, buffer.Length);
            for (var i = 0; i < n; i++) buffer[i] = pos + sub.Points[i] * k;
            if (filled)
            {
                // ImGui smooths a filled shape's edge only when its points go clockwise on screen; the
                // dot's circle goes the other way (its edge came out jagged), so it's turned round.
                var area = 0f;
                for (var i = 0; i < n; i++)
                {
                    var a = buffer[i];
                    var b = buffer[(i + 1) % n];
                    area += a.X * b.Y - b.X * a.Y;
                }
                if (area < 0) buffer[..n].Reverse();
                dl.AddConvexPolyFilled(ref buffer[0], n, col);
                continue;
            }
            var length = 0f;
            for (var i = 1; i < n; i++) length += Vector2.Distance(buffer[i - 1], buffer[i]);
            if (n == 1 || length < thickness * 0.5f)
            {
                // A dot ("h.01" with a round cap).
                dl.AddCircleFilled(buffer[0], thickness * 0.75f, col, 8);
                continue;
            }
            dl.AddPolyline(ref buffer[0], n, col, sub.Closed ? ImDrawFlags.Closed : ImDrawFlags.None, thickness);
            if (!sub.Closed)
            {
                // Round caps.
                dl.AddCircleFilled(buffer[0], thickness * 0.5f, col, 6);
                dl.AddCircleFilled(buffer[n - 1], thickness * 0.5f, col, 6);
            }
        }
    }
}
