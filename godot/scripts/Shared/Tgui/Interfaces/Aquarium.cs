using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Aquarium.tsx. The aquarium's settings (the temperature
// knob and the fluid buttons, both locked while the assisted mode runs them, the mode dropdown with its tooltip and the feeding
// interval), its fish (each with its picture, name, five hearts of happiness or a skull, and the pet, rename and take-out buttons)
// and its props (each a button that takes it out).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Aquarium
{
    // tg: Aquarium.tsx:59
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        // <Window width={765} height={500} theme="ntos">
        return c.Window(765, 500, null,
            new UiNode(UiKind.Stack) { Key = "body", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "settings" }.With(Settings(c)),
                new UiNode(UiKind.StackItem) { Key = "contents", Grow = 1 }.With(
                    new UiNode(UiKind.Stack) { Key = "columns" }.With(
                        new UiNode(UiKind.StackItem) { Key = "fish-column", Width = "75%", Height = "300px" }.With(
                            new UiNode(UiKind.Section) { Key = "fish", Title = "Fish", Fill = true, Scrollable = true }.With(
                                new UiNode(UiKind.Stack) { Key = "fish-list", Wrap = true }.With(
                                    data.Arr("fishData").Select(fish => new UiNode(UiKind.StackItem)
                                    {
                                        Key = "fish-" + fish.Str("fish_ref"),
                                        Width = "44%",
                                        Height = "100px",
                                        Background = "rgba(36, 50, 67, 0.5)",
                                    }.With(FishInfo(c, fish))).ToArray()))),
                        new UiNode(UiKind.StackItem) { Key = "props-column", Grow = 1 }.With(PropTypes(c))))));
    }

    // tg: Aquarium.tsx:106  FishInfo
    private static UiNode FishInfo(TguiContext c, UiValue fish)
    {
        string fishRef = fish.Str("fish_ref");
        return new UiNode(UiKind.Stack) { Key = "info", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "head" }.With(
                new UiNode(UiKind.Stack) { Key = "row" }.With(
                    new UiNode(UiKind.StackItem) { Key = "picture", Width = "40px" }.With(new UiNode(UiKind.Box)
                    {
                        Key = "icon",
                        DmIcon = fish.Str("fish_icon"),
                        DmIconState = fish.Str("fish_icon_state"),
                        Width = "40px",
                        Height = "40px",
                        Background = "#151326",
                    }),
                    new UiNode(UiKind.StackItem) { Key = "name-column", Grow = 1 }.With(
                        new UiNode(UiKind.Stack) { Key = "name-stack", Vertical = true }.With(
                            new UiNode(UiKind.StackItem) { Key = "name", Text = fish.Str("fish_name"), Bold = true, FontSize = "13px" },
                            new UiNode(UiKind.StackItem) { Key = "mood" }.With(fish.Truthy("fish_alive")
                                ? Happiness(c, fish.Num("fish_happiness"))
                                : new UiNode(UiKind.Icon) { Key = "dead", Icon = "skull-crossbones", Color = "white" }))),
                    new UiNode(UiKind.StackItem) { Key = "take" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "remove",
                        Icon = "arrow-up",
                        Color = "transparent",
                        Fluid = true,
                        OnClick = () => c.Act("remove_item", ("item_reference", fishRef)),
                    }))),
            new UiNode(UiKind.StackItem) { Key = "actions", Grow = 1 }.With(
                new UiNode(UiKind.Stack) { Key = "buttons" }.With(
                    new UiNode(UiKind.StackItem) { Key = "pet-cell", Width = "50%" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "pet",
                        Text = "Pet",
                        Icon = "paw",
                        Color = "transparent",
                        TextAlign = "center",
                        Fluid = true,
                        Background = "#151326",
                        OnClick = () => c.Act("pet_fish", ("fish_reference", fishRef)),
                    }),
                    // <Button.Input buttonText="Rename" value={fish.fish_name} onCommit=...>: an input that commits the new name
                    new UiNode(UiKind.StackItem) { Key = "rename-cell", Width = "50%" }.With(new UiNode(UiKind.Input)
                    {
                        Key = "rename",
                        Tooltip = "Rename",
                        TextValue = fish.Str("fish_name"),
                        Fluid = true,
                        OnCommit = value => c.Act("rename_fish", ("fish_reference", fishRef), ("chosen_name", value)),
                    }))));
    }

    // tg: Aquarium.tsx:199  PropTypes
    private static UiNode PropTypes(TguiContext c)
        => new UiNode(UiKind.Section) { Key = "props", Title = "Props", Scrollable = true, Fill = true }.With(
            new UiNode(UiKind.Stack) { Key = "prop-list", Vertical = true }.With(
                c.Data.Arr("propData").Select(prop => new UiNode(UiKind.StackItem) { Key = "prop-" + prop.Str("prop_ref"), Candystripe = true }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "take",
                        Color = "transparent",
                        Fluid = true,
                        OnClick = () => c.Act("remove_item", ("item_reference", prop.Str("prop_ref"))),
                    }.With(
                        new UiNode(UiKind.Stack) { Key = "row" }.With(
                            new UiNode(UiKind.StackItem) { Key = "picture" }.With(new UiNode(UiKind.Box)
                            {
                                Key = "icon",
                                DmIcon = prop.Str("prop_icon"),
                                DmIconState = prop.Str("prop_icon_state"),
                                Width = "40px",
                                Height = "40px",
                            }),
                            new UiNode(UiKind.StackItem) { Key = "name", Text = CapitalizeFirst(prop.Str("prop_name")), Bold = true, FontSize = "11px" })))).ToArray()));

    // tg: Aquarium.tsx:243  CalculateHappiness: five hearts, full up to the fish's happiness
    private static UiNode Happiness(TguiContext c, double happiness)
    {
        string heartIcon = c.Data.Str("heartIcon");
        return new UiNode(UiKind.Box) { Key = "hearts" }.With(Enumerable.Range(0, 5).Select(index => new UiNode(UiKind.Box)
        {
            Key = "heart-" + index,
            DmIcon = heartIcon,
            DmIconState = happiness >= index ? "full_heart" : "empty_heart",
            Width = "48px",
            Height = "48px",
        }).ToArray());
    }

    // tg: Aquarium.tsx:265  Settings
    private static UiNode Settings(TguiContext c)
    {
        UiValue data = c.Data;
        bool locked = data.Truthy("lockedFluidTemp");
        string fluidType = data.Str("fluidType");
        return new UiNode(UiKind.Stack) { Key = "settings-row", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "temperature-cell", Grow = 1 }.With(
                new UiNode(UiKind.Section) { Key = "temperature", Title = "Temperature", Fill = true }.With(
                    locked ? LockedSection("temperature-lock") : null,
                    new UiNode(UiKind.Knob)
                    {
                        Key = "knob",
                        Size = 1.5,
                        Value = data.Num("temperature"),
                        Unit = "K",
                        MinValue = data.Num("minTemperature"),
                        MaxValue = data.Num("maxTemperature"),
                        Step = 1,
                        StepPixelSize = 1,
                        Disabled = locked,
                        OnChange = value => c.Act("temperature", ("temperature", value)),
                    })),
            new UiNode(UiKind.StackItem) { Key = "fluid-cell", Grow = 1 }.With(
                new UiNode(UiKind.Section) { Key = "fluid", Title = "Fluid", Fill = true }.With(
                    locked ? LockedSection("fluid-lock") : null,
                    new UiNode(UiKind.Stack) { Key = "fluids", Vertical = true }.With(
                        data.Arr("fluidTypes").Select(f => f.AsStr()).Select(f => new UiNode(UiKind.StackItem) { Key = "fluid-" + f, Candystripe = true }.With(
                            new UiNode(UiKind.Button)
                            {
                                Key = "pick",
                                Text = f,
                                TextAlign = "center",
                                Fluid = true,
                                Color = "transparent",
                                Selected = fluidType == f,
                                Disabled = locked,
                                OnClick = () => c.Act("fluid", ("fluid", f)),
                            })).ToArray()))),
            new UiNode(UiKind.StackItem) { Key = "mode-cell", Grow = 1 }.With(
                new UiNode(UiKind.Section) { Key = "mode", Title = "Settings", Fill = true }.With(
                    new UiNode(UiKind.LabeledList) { Key = "options" }.With(
                        new UiNode(UiKind.LabeledItem) { Key = "aquarium-mode", Label = "Aquarium Mode" }.With(
                            new UiNode(UiKind.Dropdown)
                            {
                                Key = "modes",
                                Width = "80%",
                                Options = data.Arr("aquariumModes").Select(m => m.AsStr()).ToArray(),
                                TextValue = data.Str("currentMode"),
                                OnSelected = value => c.Act("change_mode", ("new_mode", value)),
                            },
                            // <Tooltip content={currentTooltip}><Icon name="question-circle" /></Tooltip>
                            new UiNode(UiKind.Icon) { Key = "mode-help", Icon = "question-circle", Color = "blue", Size = 1.5, Tooltip = data.Str("currentTooltip") }),
                        new UiNode(UiKind.LabeledItem) { Key = "feeding", Label = "Feeding Interval" }.With(
                            new UiNode(UiKind.NumberInput)
                            {
                                Key = "interval",
                                Width = "15px",
                                Value = data.Num("feedingInterval"),
                                MinValue = 1,
                                MaxValue = 7,
                                Step = 1,
                                Unit = "minutes",
                                OnChange = value => c.Act("feeding_interval", ("feeding_interval", value)),
                            })))));
    }

    // tg: Aquarium.tsx:375  LockedSection: the Dimmer over a section the assisted mode runs
    // DEVIATION(DV683): no Dimmer kind: the lock and LOCKED stand above the section's controls, which are disabled
    private static UiNode LockedSection(string key)
        => new UiNode(UiKind.Stack) { Key = key, Vertical = true, Align = "baseline" }.With(
            new UiNode(UiKind.StackItem) { Key = "lock-icon" }.With(new UiNode(UiKind.Icon) { Key = "lock", Icon = "lock", Color = "red", Size = 3 }),
            new UiNode(UiKind.StackItem) { Key = "locked", Text = "LOCKED", FontSize = "20px" });

    // tgui-core's capitalizeFirst
    private static string CapitalizeFirst(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
