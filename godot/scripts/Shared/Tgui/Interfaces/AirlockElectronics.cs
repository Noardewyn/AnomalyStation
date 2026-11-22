using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AirlockElectronics.tsx and common/AccessConfig.tsx
// (M5 slice 3, D148): the airlock electronics' access window (tg's /obj/item/electronics/airlock ui_data; the electronics come in
// slice 6).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AirlockElectronics
{
    // tg: tgui/packages/tgui/interfaces/AirlockElectronics.tsx:23
    public static UiNode Render(TguiContext c) =>
        c.Window(420, 485, null, AirLockMainSection(c));

    // tg: tgui/packages/tgui/interfaces/AirlockElectronics.tsx:33
    public static UiNode AirLockMainSection(TguiContext c)
    {
        UiValue data = c.Data;
        List<string> accesses = data.Arr("accesses").Select(a => a.AsStr()).ToList();
        bool oneAccess = data.Truthy("oneAccess");
        string passedName = data.Str("passedName");
        string passedCycleId = data.Str("passedCycleId");
        IReadOnlyList<UiValue> regions = data.Arr("regions");
        int unres_direction = (int)data.Num("unres_direction");
        bool shell = data.Truthy("shell");

        UiNode Direction(int bit, string name) => new UiNode(UiKind.Button)
        {
            Key = name,
            Icon = (unres_direction & bit) != 0 ? "check-square-o" : "square-o",
            Selected = (unres_direction & bit) != 0,
            OnClick = () => c.Act("direc_set", ("unres_direction", bit.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            Text = name,
        };

        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.Section) { Fill = true }.With(
                    new UiNode(UiKind.LabeledList).With(
                        new UiNode(UiKind.LabeledItem) { Label = "Integrated Circuit Shell" }.With(
                            new UiNode(UiKind.Button)
                            {
                                Key = "shell",
                                Checkbox = shell,
                                OnClick = () => c.Act("set_shell", ("on", !shell)),
                                Tooltip = "Whether this airlock can have an integrated circuit placed inside of it or not.",
                                Text = "Shell",
                            }),
                        new UiNode(UiKind.LabeledItem) { Label = "Access Required" }.With(
                            new UiNode(UiKind.Button)
                            {
                                Key = "one_access",
                                Icon = oneAccess ? "unlock" : "lock",
                                OnClick = () => c.Act("one_access"),
                                Text = oneAccess ? "One" : "All",
                            }),
                        new UiNode(UiKind.LabeledItem) { Label = "Unrestricted Access" }.With(
                            Direction(1, "North"),
                            Direction(2, "South"),
                            Direction(4, "East"),
                            Direction(8, "West")),
                        new UiNode(UiKind.LabeledItem) { Label = "Airlock Name" }.With(
                            new UiNode(UiKind.Input)
                            {
                                Key = "passedName",
                                Fluid = true,
                                MaxLength = 30,
                                TextValue = passedName,
                                OnCommit = value => c.Act("passedName", ("passedName", value)),
                            }),
                        new UiNode(UiKind.LabeledItem) { Label = "Cycling Id" }.With(
                            new UiNode(UiKind.Input)
                            {
                                Key = "passedCycleId",
                                Fluid = true,
                                MaxLength = 30,
                                TextValue = passedCycleId,
                                OnCommit = value => c.Act("passedCycleId", ("passedCycleId", value)),
                            })))),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                AccessConfig.Render(c,
                    accesses: regions,
                    selectedList: accesses,
                    accessMod: reference => c.Act("set", ("access", reference)),
                    grantAll: () => c.Act("grant_all"),
                    denyAll: () => c.Act("clear_all"),
                    grantDep: reference => c.Act("grant_region", ("region", reference)),
                    denyDep: reference => c.Act("deny_region", ("region", reference)))));
    }
}

public static class AccessConfig
{
    // tg: tgui/packages/tgui/interfaces/common/AccessConfig.tsx:34
    private enum ACCESS
    {
        Denied = 0,
        Partial = 1,
        Granted = 2,
    }

    // tg: tgui/packages/tgui/interfaces/common/AccessConfig.tsx:40
    private static readonly (string Icon, string Color)[] DIFFMAP =
    {
        ("times-circle", "bad"),
        ("stop-circle", null),
        ("check-circle", "good"),
    };

    // tg: tgui/packages/tgui/interfaces/common/AccessConfig.tsx:55
    public static UiNode Render(TguiContext c, IReadOnlyList<UiValue> accesses, List<string> selectedList, Action<string> accessMod,
        Action grantAll, Action denyAll, Action<string> grantDep, Action<string> denyDep)
    {
        accesses ??= Array.Empty<UiValue>();
        selectedList ??= new List<string>();

        // tg: :66  const [selectedAccessName, setSelectedAccessName] = useState(accesses[0]?.name);
        string selectedAccessName = c.UseState("selectedAccessName", accesses.Count > 0 ? accesses[0].Str("name") : null);

        UiValue selectedAccess = accesses.FirstOrDefault(access => access.Str("name") == selectedAccessName) ?? (accesses.Count > 0 ? accesses[0] : null);

        List<UiValue> selectedAccessEntries = TguiFormat.SortBy(selectedAccess?.Arr("accesses") ?? Array.Empty<UiValue>(), entry => entry.Str("desc"));

        ACCESS checkAccessIcon(IReadOnlyList<UiValue> entries)
        {
            bool oneAccess = false;
            bool oneInaccess = false;
            foreach (UiValue element in entries)
            {
                if (selectedList.Contains(element.Str("ref")))
                    oneAccess = true;
                else
                    oneInaccess = true;
            }
            if (!oneAccess && oneInaccess)
                return ACCESS.Denied;
            else if (oneAccess && oneInaccess)
                return ACCESS.Partial;
            else
                return ACCESS.Granted;
        }

        return new UiNode(UiKind.Section)
        {
            Fill = true,
            Title = "Access",
            Buttons = new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Button) { Key = "grant_all", Icon = "check-double", Color = "good", OnClick = grantAll, Text = "Grant All" },
                new UiNode(UiKind.Button) { Key = "deny_all", Icon = "undo", Color = "bad", OnClick = denyAll, Text = "Deny All" }),
        }.With(
            new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Tabs) { Vertical = true }.With(
                        accesses.Select(access =>
                        {
                            IReadOnlyList<UiValue> entries = access.Arr("accesses");
                            (string icon, string color) = DIFFMAP[(int)checkAccessIcon(entries)];
                            string name = access.Str("name");
                            return new UiNode(UiKind.Tab)
                            {
                                Key = name,
                                Color = color,
                                Icon = icon,
                                Selected = name == selectedAccessName,
                                OnClick = () => c.SetState("selectedAccessName", name),
                                Text = name,
                            };
                        }))),
                new UiNode(UiKind.Divider),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    AccessButtons(selectedAccessEntries, selectedList, accessMod, grantDep, denyDep, selectedAccess))));
    }

    // tg: tgui/packages/tgui/interfaces/common/AccessConfig.tsx:139
    private static UiNode AccessButtons(List<UiValue> selectedAccessEntries, List<string> selectedList, Action<string> accessMod,
        Action<string> grantDep, Action<string> denyDep, UiValue selectedAccess)
    {
        string region = selectedAccess?.Str("name");
        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.Stack) { Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                        new UiNode(UiKind.Button) { Key = "grant_region", Fluid = true, Icon = "check", Color = "good", OnClick = () => grantDep(region), Text = "Grant Region" }),
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                        new UiNode(UiKind.Button) { Key = "deny_region", Fluid = true, Icon = "times", Color = "bad", OnClick = () => denyDep(region), Text = "Deny Region" }))),
            new UiNode(UiKind.Divider),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With(
                    selectedAccessEntries.Select(entry =>
                    {
                        string reference = entry.Str("ref");
                        return new UiNode(UiKind.Button)
                        {
                            Key = entry.Str("desc"),
                            Fluid = true,
                            Checkbox = selectedList.Contains(reference),
                            OnClick = () => accessMod(reference),
                            Text = entry.Str("desc"),
                        };
                    }))));
    }
}
