using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosRadar.tsx and NtosRadarSyndicate.tsx (syndicate N1b,
// D316: the NTOS radar programs, the nuclear operatives' Fission360). Engine-free (compiled into the tests too). The radar's pictures
// are tg's /datum/asset/simple/radar_assets (icons/ui/tgui/ntosradar_*.png, copied by the icon converter).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosRadar
{
    public const string Background = "icons/ui/tgui/ntosradar_background.png";

    // tg's resolveAsset names (radar.dm's assets list) and the files they stand for
    private static string Asset(string name) => name switch
    {
        "ntosradarpointerS.png" => "icons/ui/tgui/ntosradar_pointer_S.png",
        _ => "icons/ui/tgui/ntosradar_pointer.png",
    };

    // tg: NtosRadar.tsx:38
    public static UiNode Render(TguiContext c) => NtosWindow.Render(c, 800, 600, null, Content(c));

    // tg: NtosRadarSyndicate.tsx:4
    public static UiNode RenderSyndicate(TguiContext c) => NtosWindow.Render(c, 800, 600, null, Content(c));

    // tg: NtosRadar.tsx:46
    public static UiNode Content(TguiContext c) => new UiNode(UiKind.Stack) { Fill = true }.With(
        new UiNode(UiKind.StackItem) { Width = 20.5, Scrollable = true }.With(ObjectDisplay(c)),
        new UiNode(UiKind.StackItem).With(TargetDisplay(c)));

    // tg: NtosRadar.tsx:70
    private static UiNode ObjectDisplay(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> objects = data.Get("object")?.Items ?? Array.Empty<UiValue>();
        bool scanning = data.Truthy("scanning");
        string selected = data.Str("selected");
        var section = new UiNode(UiKind.Section).With(new UiNode(UiKind.Button)
        {
            Key = "scan",
            Icon = "redo-alt",
            Text = scanning ? "Scanning..." : "Scan",
            Color = "blue",
            Disabled = scanning,
            OnClick = () => c.Act("scan"),
        });
        if (objects.Count == 0 && !scanning)
            section.With(new UiNode(UiKind.Box) { Text = "No trackable signals found" });
        if (!scanning)
        {
            section.With(objects.Select((o, i) =>
            {
                string reference = o.Str("ref");
                return new UiNode(UiKind.Button)
                {
                    Key = "target:" + i,
                    Text = o.Str("name"),
                    Tooltip = o.Str("name"),
                    Fluid = true,
                    Color = "transparent",
                    Selected = reference == selected,
                    OnClick = () => c.Act("selecttarget", ("ref", reference)),
                };
            }));
        }
        return section;
    }

    // tg: NtosRadar.tsx:107; NtosRadarContent's 45 by 45 rem box (540 px) with the 500 px background picture centred in it (20 px in),
    // its children placed in pixels from the box's corner as tg's absolute positions
    private static UiNode TargetDisplay(TguiContext c)
    {
        UiValue data = c.Data;
        string selected = data.Str("selected");
        UiValue target = data.Get("target");
        var area = new UiNode(UiKind.Overlay) { Key = "radar", Width = 45, Height = 45 }.With(
            new UiNode(UiKind.Box) { Key = "background", DmIcon = Background, Anchor = "top-left", PosX = 20, PosY = 20, Width = "500px", Height = "500px" });
        if (string.IsNullOrEmpty(selected) || target == null || target.IsNull)
            return area;
        if (target.Fields.Count == 0)
        {
            return area.With(new UiNode(UiKind.NoticeBox)
            {
                Key = "lost",
                Anchor = "top-left",
                PosX = 16,
                PosY = 247,
                Width = 504,
                FontSize = "30px",
                TextAlign = "center",
                Text = "Signal Lost",
            });
        }
        if (target.Truthy("userot"))
        {
            return area.With(new UiNode(UiKind.Box)
            {
                Key = "arrow",
                Anchor = "top-left",
                DmIcon = Asset(target.Str("arrowstyle")),
                Width = "50px",
                Height = "500px",
                PosX = 243,
                PosY = 20,
                Rotation = target.Num("rot"),
            });
        }
        return area.With(new UiNode(UiKind.Icon)
        {
            Key = "pointer",
            Anchor = "top-left",
            Icon = target.Str("pointer"),
            Size = 2,
            Color = target.Str("color"),
            PosX = target.Num("locx") * 10 + 16,
            PosY = target.Num("locy") * 10 + 19,
        });
    }
}
