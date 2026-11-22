using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/StationTraitsPanel.tsx (gaps R2: the admins' Modify
// Station Traits window, /datum/station_traits_panel). Original copyright and license: tgstation, AGPL-3.0 (code). See
// ../tgstation/LICENSE.

public static class StationTraitsPanel
{
    // tg: StationTraitsPanel.tsx:35  enum Tab
    private const int SetupFutureStationTraits = 0, ViewStationTraits = 1;

    // tg: StationTraitsPanel.tsx:203
    public static UiNode Render(TguiContext c)
    {
        int currentTab = c.UseState("currentTab", ViewStationTraits);

        UiNode currentPage = currentTab == SetupFutureStationTraits ? FutureStationTraitsPage(c) : ViewStationTraitsPage(c);

        // tg: :219  <Window title="Modify Station Traits" height={500} width={500}> <Window.Content scrollable>: the content in the window's own area
        return c.Window(500, 500, "Modify Station Traits",
            new UiNode(UiKind.Tabs).With(
                new UiNode(UiKind.Tab)
                {
                    Key = "view",
                    Icon = "eye",
                    Selected = currentTab == ViewStationTraits,
                    OnClick = () => c.SetState("currentTab", ViewStationTraits),
                    Text = "View",
                },
                new UiNode(UiKind.Tab)
                {
                    Key = "edit",
                    Icon = "edit",
                    Selected = currentTab == SetupFutureStationTraits,
                    OnClick = () => c.SetState("currentTab", SetupFutureStationTraits),
                    Text = "Edit",
                }),
            new UiNode(UiKind.Divider) { Rule = true },
            currentPage);
    }

    // tg: StationTraitsPanel.tsx:40  FutureStationTraitsPage
    private static UiNode FutureStationTraitsPage(TguiContext c)
    {
        UiValue future = c.Data.Field("future_station_traits");
        bool isArray = future is { Kind: UiValueKind.Array };
        IReadOnlyList<UiValue> futureStationTraits = isArray ? future.Items : Array.Empty<UiValue>();

        string selectedTrait = c.UseState("selectedTrait", "");

        // Object.fromEntries(valid_station_traits.map(trait => [trait.name, trait.path])): the last path of a name wins
        var traitsByName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (UiValue trait in c.Data.Arr("valid_station_traits"))
            traitsByName[trait.Str("name")] = trait.Str("path");

        // traitNames.sort(): JavaScript's default sort, by UTF-16 code units
        string[] traitNames = traitsByName.Keys.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        var page = new UiNode(UiKind.Box).With(
            new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Dropdown)
                {
                    Key = "select_trait",
                    Options = traitNames,
                    Tooltip = "Select trait to add...",
                    TextValue = selectedTrait,
                    Width = "100%",
                    OnSelected = name => c.SetState("selectedTrait", name),
                }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "add",
                    Color = "green",
                    Icon = "plus",
                    Text = "Add",
                    OnClick = () =>
                    {
                        if (string.IsNullOrEmpty(selectedTrait))
                            return;

                        string selectedPath = traitsByName[selectedTrait];

                        var newStationTraits = new List<string> { selectedPath };
                        if (isArray)
                        {
                            List<string> selectedTraitPaths = futureStationTraits.Select(trait => trait.Str("path")).ToList();

                            if (selectedTraitPaths.IndexOf(selectedPath) != -1)
                                return;

                            newStationTraits.AddRange(selectedTraitPaths);
                        }

                        c.Act("setup_future_traits", ("station_traits", UiValue.ListOf(newStationTraits.Select(UiValue.Of))));
                    },
                })),
            new UiNode(UiKind.Divider) { Rule = true });

        if (isArray)
        {
            if (futureStationTraits.Count > 0)
            {
                page.With(new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                    futureStationTraits.Select(trait =>
                    {
                        string path = trait.Str("path");
                        return new UiNode(UiKind.StackItem) { Key = path }.With(
                            new UiNode(UiKind.Stack) { Fill = true }.With(
                                new UiNode(UiKind.StackItem) { Grow = 1, Text = trait.Str("name") },
                                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                                {
                                    Key = "delete:" + path,
                                    Color = "red",
                                    Icon = "times",
                                    Text = "Delete",
                                    OnClick = () => c.Act("setup_future_traits", ("station_traits", UiValue.ListOf(
                                        futureStationTraits.Select(t => t.Str("path")).Where(p => p != path).Select(UiValue.Of)))),
                                })));
                    })));
            }
            else
            {
                page.With(
                    new UiNode(UiKind.Box) { Text = "No station traits will run next round." },
                    new UiNode(UiKind.Box).With(new UiNode(UiKind.Button)
                    {
                        Key = "clear_future_traits",
                        Color = "red",
                        Icon = "times",
                        Tooltip = "The next round will roll station traits randomly, just like normal",
                        Text = "Run Station Traits Normally",
                        OnClick = () => c.Act("clear_future_traits"),
                    }));
            }
        }
        else
        {
            page.With(
                new UiNode(UiKind.Box) { Text = "No future station traits are planned." },
                new UiNode(UiKind.Box).With(new UiNode(UiKind.Button)
                {
                    Key = "prevent_traits",
                    Color = "red",
                    Icon = "times",
                    Text = "Prevent station traits from running next round",
                    OnClick = () => c.Act("setup_future_traits", ("station_traits", UiValue.ListOf(Array.Empty<UiValue>()))),
                }));
        }

        return page;
    }

    // tg: StationTraitsPanel.tsx:162  ViewStationTraitsPage
    private static UiNode ViewStationTraitsPage(TguiContext c)
    {
        IReadOnlyList<UiValue> currentTraits = c.Data.Arr("current_traits");
        bool tooLateToRevert = c.Data.Truthy("too_late_to_revert");

        if (currentTraits.Count == 0)
            return new UiNode(UiKind.Box) { Text = "There are no active station traits." };

        return new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            currentTraits.Select(stationTrait =>
            {
                string @ref = stationTrait.Str("ref");
                bool canRevert = stationTrait.Truthy("can_revert");
                return new UiNode(UiKind.StackItem) { Key = @ref }.With(
                    new UiNode(UiKind.Stack) { Fill = true }.With(
                        new UiNode(UiKind.StackItem) { Grow = 1, Text = stationTrait.Str("name") },
                        new UiNode(UiKind.StackItem).With(c.ButtonConfirm("revert:" + @ref, new UiNode(UiKind.Button)
                        {
                            Text = "Revert",
                            Color = "red",
                            Disabled = tooLateToRevert || !canRevert,
                            Tooltip = !canRevert ? "This trait is not revertable."
                                : tooLateToRevert ? "It's too late to revert station traits, the round has already started." : null,
                            Icon = "times",
                            OnClick = () => c.Act("revert", ("ref", @ref)),
                        }))));
            }));
    }
}
