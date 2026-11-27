using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/GreyscaleModifyMenu.tsx (the GAGS colour menu: a
// canister's recolour, a vending machine's colourable product). DV686: the preview is the GAGS icon the server names
// (sprites.finished, "gags:<type>|<colours>") in its icon state (sprites.finished_icon_state), drawn by the kit's icon box; it shows the
// sprite's south view (the kit's icons have no direction), and the full preview's steps are never sent. The loading cog is a plain
// cog (the kit does not spin icons). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class GreyscaleModifyMenu
{
    // tg: GreyscaleModifyMenu.tsx:51
    private static readonly (string Dir, string Abbreviation)[] Directions =
    {
        ("north", "N"), ("northeast", "NE"), ("east", "E"), ("southeast", "SE"),
        ("south", "S"), ("southwest", "SW"), ("west", "W"), ("northwest", "NW"),
    };

    // tg: GreyscaleModifyMenu.tsx:294
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool unlocked = data.Truthy("unlocked");
        var content = new UiNode(UiKind.Section) { Key = "content", Fill = true, Scrollable = true }.With(
            ConfigDisplay(c),
            ColorDisplay(c),
            IconStatesDisplay(c),
            new UiNode(UiKind.Stack) { Key = "actions", Vertical = true }.With(
                unlocked
                    ? new UiNode(UiKind.StackItem) { Key = "files" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "monitor",
                            Icon = "file-image-o",
                            Selected = data.Truthy("monitoring_files"),
                            Tooltip = "Continuously checks files for changes and reloads when necessary. WARNING: Very expensive",
                            OnClick = () => c.Act("toggle_mass_refresh"),
                        },
                        new UiNode(UiKind.Button)
                        {
                            Key = "refresh",
                            Text = "Refresh Icon File",
                            Tooltip = "Loads the json configuration and icon file fresh from disk. This is useful to avoid restarting the server to see changes. WARNING: Expensive",
                            OnClick = () => c.Act("refresh_file"),
                        },
                        new UiNode(UiKind.Button)
                        {
                            Key = "save",
                            Text = "Save Icon File",
                            Tooltip = "Saves the icon to a temp file in tmp/. This is useful if you want to use a generated icon elsewhere or just view a more accurate representation",
                            OnClick = () => c.Act("save_dmi"),
                        })
                    : null,
                new UiNode(UiKind.StackItem) { Key = "apply" }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "apply",
                        Text = "Apply",
                        Tooltip = "Applies changes made to the object this menu was created from.",
                        Color = "red",
                        OnClick = () => c.Act("apply"),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "full",
                        Icon = data.Truthy("generate_full_preview") ? "check-square-o" : "square-o",
                        Text = "Full Preview",
                        Tooltip = "Generates and displays the full sprite generation process instead of just the final output.",
                        Disabled = !data.Truthy("generate_full_preview") && !unlocked,
                        Selected = data.Truthy("generate_full_preview"),
                        OnClick = () => c.Act("toggle_full_preview"),
                    })),
            PreviewDisplay(c),
            data.Truthy("refreshing") ? new UiNode(UiKind.Box) { Key = "loading", TextAlign = "center" }.With(new UiNode(UiKind.Icon) { Key = "cog", Icon = "cog", Size = 5 }) : null);
        // tg: GreyscaleModifyMenu.tsx:297  <Window title="Color Configuration" width={325} height={800}>
        return c.Window(325, 800, "Color Configuration", content);
    }

    // tg: GreyscaleModifyMenu.tsx:73
    private static UiNode ConfigDisplay(TguiContext c) =>
        new UiNode(UiKind.Section) { Key = "designs", Title = "Designs" }.With(
            new UiNode(UiKind.LabeledList) { Key = "list" }.With(
                new UiNode(UiKind.LabeledItem) { Key = "type", Label = "Design Type" }.With(
                    new UiNode(UiKind.Button) { Key = "select", Icon = "cogs", OnClick = () => c.Act("select_config") },
                    new UiNode(UiKind.Input)
                    {
                        Key = "config",
                        TextValue = c.Data.Str("greyscale_config"),
                        OnCommit = value => c.Act("load_config_from_string", ("config_string", value)),
                    })));

    // tg: GreyscaleModifyMenu.tsx:93
    private static UiNode ColorDisplay(TguiContext c)
    {
        IReadOnlyList<UiValue> colors = c.Data.Arr("colors");
        var list = new UiNode(UiKind.LabeledList) { Key = "list" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "full", Label = "Full Color String" }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "dice",
                    Icon = "dice",
                    Tooltip = "Randomizes all color groups.",
                    OnClick = () => c.Act("random_all_colors"),
                },
                new UiNode(UiKind.Input)
                {
                    Key = "string",
                    TextValue = string.Concat(colors.Select(item => item.Str("value"))),
                    OnCommit = value => c.Act("recolor_from_string", ("color_string", value)),
                }));
        foreach (UiValue item in colors)
        {
            double index = item.Num("index");
            string value = item.Str("value");
            list.With(new UiNode(UiKind.LabeledItem) { Key = "colorgroup" + index + value, Label = "Color Group " + index, LabelColor = value }.With(
                new UiNode(UiKind.ColorBox) { Key = "box", Color = value },
                new UiNode(UiKind.Button)
                {
                    Key = "palette",
                    Icon = "palette",
                    Tooltip = "Brings up a color pick window to replace this color group.",
                    OnClick = () => c.Act("pick_color", ("color_index", index)),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "dice",
                    Icon = "dice",
                    Tooltip = "Randomizes the color for this color group.",
                    OnClick = () => c.Act("random_color", ("color_index", index)),
                },
                new UiNode(UiKind.Input)
                {
                    Key = "value",
                    TextValue = value,
                    Width = "7em",
                    OnCommit = text => c.Act("recolor", ("color_index", index), ("new_color", text)),
                }));
        }
        return new UiNode(UiKind.Section) { Key = "colors", Title = "Colors" }.With(list);
    }

    // tg: GreyscaleModifyMenu.tsx:181
    private static UiNode IconStatesDisplay(TguiContext c)
    {
        string current = c.Data.Str("icon_state");
        var row = new UiNode(UiKind.Box) { Key = "states" };
        foreach (UiValue state in c.Data.Get("sprites")?.Arr("icon_states") ?? Array.Empty<UiValue>())
        {
            string item = state.AsStr();
            row.With(new UiNode(UiKind.Button)
            {
                Key = "state-" + item,
                Text = item.Length > 0 ? item : "Blank State",
                Disabled = item == current,
                OnClick = () => c.Act("select_icon_state", ("new_icon_state", item)),
            });
        }
        return new UiNode(UiKind.Section) { Key = "icon-states", Title = "Icon States" }.With(row);
    }

    // tg: GreyscaleModifyMenu.tsx:136
    private static UiNode PreviewCompassSelect(TguiContext c)
    {
        string current = c.Data.Str("sprites_dir");
        UiNode Single(string dir)
        {
            (string _, string abbreviation) = Directions.First(d => d.Dir == dir);
            // tg: GreyscaleModifyMenu.tsx:163
            return new UiNode(UiKind.StackItem) { Key = dir, Grow = 1 }.With(new UiNode(UiKind.Button)
            {
                Key = "button",
                Text = abbreviation,
                Tooltip = "Sets the direction of the preview sprite to " + dir,
                Disabled = dir == current,
                TextAlign = "center",
                Fluid = true,
                OnClick = () => c.Act("change_dir", ("new_sprite_dir", dir)),
            });
        }
        UiNode Row(string key, params UiNode[] cells) => new UiNode(UiKind.StackItem) { Key = key }.With(new UiNode(UiKind.Stack) { Key = "row" }.With(cells));
        return new UiNode(UiKind.Stack) { Key = "compass", Vertical = true }.With(
            Row("top", Single("northwest"), Single("north"), Single("northeast")),
            Row("middle", Single("west"),
                new UiNode(UiKind.StackItem) { Key = "center", Grow = 1 }.With(
                    new UiNode(UiKind.Button) { Key = "button", Icon = "arrows-alt", Fluid = true }),
                Single("east")),
            Row("bottom", Single("southwest"), Single("south"), Single("southeast")));
    }

    // tg: GreyscaleModifyMenu.tsx:203
    private static UiNode PreviewDisplay(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue sprites = data.Get("sprites");
        string finished = sprites?.Str("finished");
        UiNode picture = !string.IsNullOrEmpty(finished)
            ? new UiNode(UiKind.Box) { Key = "finished", DmIcon = finished, DmIconState = sprites.Str("finished_icon_state"), Width = "96px", Height = "96px" }
            : new UiNode(UiKind.Box) { Key = "none" }.With(new UiNode(UiKind.Icon) { Key = "image", Icon = "image", Size = 5 });
        var section = new UiNode(UiKind.Section) { Key = "preview", Title = "Preview (" + data.Str("sprites_dir") + ")" }.With(
            new UiNode(UiKind.Table) { Key = "table" }.With(
                new UiNode(UiKind.TableRow) { Key = "row" }.With(
                    new UiNode(UiKind.TableCell) { Key = "compass", Width = "50%" }.With(PreviewCompassSelect(c)),
                    new UiNode(UiKind.TableCell) { Key = "picture" }.With(picture))));
        if (data.Truthy("unlocked"))
            section.With(new UiNode(UiKind.Box) { Key = "time", Text = "Time Spent: " + (sprites?.Num("time_spent") ?? 0) + "ms" });
        section.With(new UiNode(UiKind.Divider) { Key = "divider" });
        return section;
    }
}
