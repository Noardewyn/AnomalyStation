using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/TacVisorEyesMenu.tsx (gaps OG1): the tactical IFF visor's
// friendly and hostile factions, its visor image and its threat flags (eyes_augments.dm ui_data). Engine-free (compiled into the tests
// too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class TacVisorEyesMenu
{
    // tg: TacVisorEyesMenu.tsx:17
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        int threatFlags = (int)data.Num("threatFlags");
        var threatButtons = data.Obj("threatOptions").Select(option =>
        {
            int bit = (int)option.Value.AsNum();
            return new UiNode(UiKind.Button)
            {
                Key = "threat:" + option.Key, Checkbox = (threatFlags & bit) != 0, Text = option.Key,
                OnClick = () => c.Act("set_threat_flags", ("threat_flags", threatFlags ^ bit)),
            };
        }).ToArray();
        return c.Window(320, 264, "Tactical IFF Visor",
            new UiNode(UiKind.Section) { Key = "settings", Title = "Settings" }.With(new UiNode(UiKind.LabeledList).With(
                Dropdown(c, "friendly", "Friendly Faction", data, "validFriendlyFactions", "friendlyFaction", "set_friendly", "faction"),
                Dropdown(c, "hostile", "Hostile Faction", data, "validHostileFactions", "hostileFaction", "set_hostile", "faction"),
                Dropdown(c, "display", "Visor Image", data, "visorOptions", "visorDisplay", "set_display", "display"))),
            new UiNode(UiKind.Section) { Key = "threat", Title = "Threat Parameters" }.With(threatButtons));
    }

    private static UiNode Dropdown(TguiContext c, string key, string label, UiValue data, string optionsKey, string selectedKey, string action, string param) =>
        new UiNode(UiKind.LabeledItem) { Key = key, Label = label }.With(new UiNode(UiKind.Dropdown)
        {
            Key = key + "_dropdown", Options = data.Arr(optionsKey).Select(v => v.AsStr()).ToList(), TextValue = data.Str(selectedKey),
            OnSelected = value => c.Act(action, (param, value)),
        });
}
