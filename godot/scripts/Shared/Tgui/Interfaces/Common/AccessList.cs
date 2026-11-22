using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/common/AccessList.jsx (syndicate G1b, D316): the
// access picker of the ID cards (the agent card's ChameleonCard window): wildcard tabs, the region tabs and the access checkboxes.
// Flex is drawn as a horizontal Stack (the client's layout has no Flex).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AccessList
{
    // A number as a JSON object key (DM writes an assoc list's number keys as text)
    private static string N(double value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private sealed record Region(string Name, List<UiValue> Accesses, bool HasSelected, bool AllSelected);

    // tg: AccessList.jsx:6
    public static UiNode Render(TguiContext c, IReadOnlyList<UiValue> accesses, UiValue wildcardSlots, List<string> selectedList, Action<string, string> accessMod,
        List<string> trimAccess, UiValue accessFlags, UiValue accessFlagNames, UiValue wildcardFlags, UiNode extraButtons, bool showBasic)
    {
        accesses ??= Array.Empty<UiValue>();
        selectedList ??= new List<string>();
        trimAccess ??= new List<string>();
        string selectedWildcard = SelectedWildcard(c, wildcardSlots, showBasic);

        var parsedRegions = new List<Region>();
        var selectedTrimAccess = new List<string>();
        foreach (UiValue region in accesses)
        {
            string regionName = region.Str("name");
            IReadOnlyList<UiValue> regionAccess = region.Arr("accesses");
            var parsed = new List<UiValue>();
            bool hasSelected = false;
            bool allSelected = true;

            // If we're showing the basic accesses included as part of the trim,
            // we want to figure out how many are selected for later formatting
            // logic.
            if (showBasic)
            {
                foreach (UiValue access in regionAccess)
                {
                    string reference = access.Str("ref");
                    if (trimAccess.Contains(reference) && selectedList.Contains(reference) && !selectedTrimAccess.Contains(reference))
                        selectedTrimAccess.Add(reference);
                }
            }

            // If there's no wildcard selected, grab accesses in
            // the trimAccess list as they require no wildcard.
            if (selectedWildcard == "None")
            {
                foreach (UiValue access in regionAccess)
                {
                    string reference = access.Str("ref");
                    if (!trimAccess.Contains(reference))
                        continue;
                    parsed.Add(access);
                    if (selectedList.Contains(reference))
                        hasSelected = true;
                    else
                        allSelected = false;
                }
                if (parsed.Count > 0)
                    parsedRegions.Add(new Region(regionName, parsed, hasSelected, allSelected));
                continue;
            }
            // Otherwise, a trim is selected. We want to grab all
            // accesses not in trimAccess that are compatible with
            // the given wildcard.
            foreach (UiValue access in regionAccess)
            {
                string reference = access.Str("ref");
                if (trimAccess.Contains(reference))
                    continue;
                if (((long)accessFlags.Num(reference) & (long)wildcardFlags.Num(selectedWildcard)) != 0)
                {
                    parsed.Add(access);
                    if (selectedList.Contains(reference))
                        hasSelected = true;
                    else
                        allSelected = false;
                }
            }
            if (parsed.Count > 0)
                parsedRegions.Add(new Region(regionName, parsed, hasSelected, allSelected));
        }

        return new UiNode(UiKind.Section) { Title = "Access", Buttons = extraButtons }.With(
            new UiNode(UiKind.Stack) { Vertical = true, Width = "100%" }.With(
                new UiNode(UiKind.StackItem).With(FormatWildcards(c, wildcardSlots, showBasic, selectedTrimAccess.Count, trimAccess.Count)),
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Stack).With(
                        new UiNode(UiKind.StackItem).With(RegionTabList(c, parsedRegions)),
                        new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                            RegionAccessList(c, parsedRegions, selectedList, accessMod, trimAccess, accessFlags, accessFlagNames, wildcardSlots, showBasic))))));
    }

    // tg: AccessList.jsx:24 (and :145, :223): the shared 'wildcardSelected' state, reset to the first wildcard when the selected
    // one is gone
    private static string SelectedWildcard(TguiContext c, UiValue wildcardSlots, bool showBasic)
    {
        string first = wildcardSlots?.Fields.Count > 0 ? wildcardSlots.Fields[0].Key : null;
        string initial = showBasic ? "None" : first;
        string wildcardTab = c.UseState("wildcardSelected", initial);
        if (wildcardTab != "None" && (wildcardTab == null || wildcardSlots?.Get(wildcardTab) is not { IsNull: false }))
        {
            c.SetState("wildcardSelected", initial);
            return initial;
        }
        return wildcardTab;
    }

    // tg: AccessList.jsx:142
    public static UiNode FormatWildcards(TguiContext c, UiValue wildcardSlots, bool showBasic, int basicUsed, int basicMax)
    {
        string selectedWildcard = SelectedWildcard(c, wildcardSlots, showBasic);
        var tabs = new List<UiNode>();
        if (showBasic)
            tabs.Add(new UiNode(UiKind.Tab)
            {
                Key = "None",
                Selected = selectedWildcard == "None",
                OnClick = () => c.SetState("wildcardSelected", "None"),
                Text = $"Trim:\n{basicUsed}/{basicMax}",
            });
        foreach (KeyValuePair<string, UiValue> slot in wildcardSlots?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>())
        {
            string wildcard = slot.Key;
            double wcLimit = slot.Value.Num("limit");
            int wcUsage = slot.Value.Arr("usage").Count;
            double wcLeft = wcLimit - wcUsage;
            string limitText = wcLeft < 0 ? "∞" : N(wcLimit);
            tabs.Add(new UiNode(UiKind.Tab)
            {
                Key = wildcard,
                Selected = selectedWildcard == wildcard,
                OnClick = () => c.SetState("wildcardSelected", wildcard),
                Text = $"{wildcard}:\n{wcUsage}/{limitText}",
            });
        }
        return new UiNode(UiKind.Tabs).With(tabs);
    }

    // tg: AccessList.jsx:197
    private static UiNode RegionTabList(TguiContext c, List<Region> accesses)
    {
        string selectedAccessName = c.UseState("accessName", accesses.Count > 0 ? accesses[0].Name : null);
        return new UiNode(UiKind.Tabs) { Vertical = true }.With(
            accesses.Select(access => new UiNode(UiKind.Tab)
            {
                Key = access.Name,
                Icon = access.AllSelected ? "check" : access.HasSelected ? "minus" : "times",
                MinWidth = "100%",
                Selected = access.Name == selectedAccessName,
                OnClick = () => c.SetState("accessName", access.Name),
                Text = access.Name,
            }));
    }

    // tg: AccessList.jsx:228
    private static UiNode RegionAccessList(TguiContext c, List<Region> accesses, List<string> selectedList, Action<string, string> accessMod,
        List<string> trimAccess, UiValue accessFlags, UiValue accessFlagNames, UiValue wildcardSlots, bool showBasic)
    {
        string selWildcard = SelectedWildcard(c, wildcardSlots, showBasic);
        string selectedAccessName = c.UseState("accessName", accesses.Count > 0 ? accesses[0].Name : null);

        Region selectedAccess = accesses.FirstOrDefault(access => access.Name == selectedAccessName);
        List<UiValue> selectedAccessEntries = TguiFormat.SortBy(selectedAccess?.Accesses ?? new List<UiValue>(), entry => entry.Str("desc"));

        var wcAccess = new Dictionary<string, string>();
        foreach (KeyValuePair<string, UiValue> slot in wildcardSlots?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>())
            foreach (UiValue access in slot.Value.Arr("usage"))
                wcAccess[access.Text ?? N(access.Number)] = slot.Key;

        UiValue wildcard = selWildcard == null ? null : wildcardSlots?.Get(selWildcard);
        // If there's no wildcard, -1 limit as there's infinite basic slots.
        double wcLimit = wildcard is { IsNull: false } ? wildcard.Num("limit") : -1;
        int wcUsage = wildcard is { IsNull: false } ? wildcard.Arr("usage").Count : 0;
        double wcAvail = wcLimit - wcUsage;

        return new UiNode(UiKind.Stack) { Vertical = true }.With(
            selectedAccessEntries.Select(entry =>
            {
                string id = entry.Str("ref");
                wcAccess.TryGetValue(id, out string accessWildcard);
                bool disableButton = (wcAvail == 0 && accessWildcard != selWildcard) || (wcAvail > 0 && accessWildcard != null && accessWildcard != selWildcard);
                string flagName = accessFlagNames.Str(N(accessFlags.Num(id)));
                string entryName = accessWildcard == null && trimAccess.Contains(id) ? entry.Str("desc") : $"{entry.Str("desc")} ({flagName})";
                return new UiNode(UiKind.StackItem) { Key = entry.Str("desc") }.With(new UiNode(UiKind.Button)
                {
                    Fluid = true,
                    Text = entryName,
                    Disabled = disableButton,
                    Checkbox = selectedList.Contains(id),
                    OnClick = () => accessMod(id, selWildcard == "None" ? null : selWildcard),
                });
            }));
    }
}
