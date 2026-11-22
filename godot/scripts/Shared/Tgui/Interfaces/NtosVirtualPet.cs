using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosVirtualPet.tsx (gaps S4d, D327): the Virtual Pet, its
// stats, location and feed dropzone, its customization (name, hat, colour, gender), the shared feed of every pet's milestones with their
// likes, and its trick sequence. tg's pictures are base64 of flattened icons; here they come as their parts (DV405: icon, state, colour,
// hat) and are drawn as layered tg icons (the hat's pixel offset is not drawn: the kit's overlay has no offsets). Flex is a horizontal
// Stack. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosVirtualPet
{
    private const int TabStats = 0, TabCustomization = 1, TabUpdates = 2, TabTricks = 3;

    // tg: NtosVirtualPet.tsx:88
    public static UiNode Render(TguiContext c)
    {
        int tab = c.UseState("tab", TabStats);
        UiNode Tab(int id, string text) => new UiNode(UiKind.Tab) { Key = "tab:" + id, Text = text, Selected = tab == id, OnClick = () => c.SetState("tab", id) };
        var content = new UiNode(UiKind.Box) { Scrollable = true, Fill = true }.With(
            new UiNode(UiKind.Tabs) { Key = "tabs" }.With(
                Tab(TabStats, "Stats"), Tab(TabCustomization, "Customization"), Tab(TabUpdates, "Pet Updates"), Tab(TabTricks, "Tricks")),
            tab == TabStats ? Stats(c) : tab == TabCustomization ? Customization(c) : tab == TabUpdates ? AllPetUpdates(c) : PetTricks(c));
        return NtosWindow.Render(c, 390, 570, null, content);
    }

    /// A picture sent as its parts (DV405): the base icon with its colour, and the hat over it.
    public static UiNode Picture(string key, UiValue parts, string size)
    {
        if (parts == null || parts.IsNull)
            return null;
        var overlay = new UiNode(UiKind.Overlay) { Key = key, Width = size, Height = size }.With(
            new UiNode(UiKind.Box) { Key = "base", DmIcon = parts.Str("icon"), DmIconState = parts.Str("icon_state"), Color = parts.Get("color") is { IsNull: false } col ? col.Text : null, Width = size, Height = size });
        if (parts.Get("hat_icon") is { IsNull: false } hat)
            overlay.With(new UiNode(UiKind.Box) { Key = "hat", DmIcon = hat.Text, DmIconState = parts.Str("hat_icon_state"), Width = size, Height = size });
        return overlay;
    }

    // tg: NtosVirtualPet.tsx:130
    private static UiNode Stats(TguiContext c)
    {
        UiValue data = c.Data;
        bool summoned = data.Truthy("currently_summoned");
        string selectedArea = data.Str("selected_area");
        UiNode Bar(string key, string label, double value, double max) => new UiNode(UiKind.StackItem) { Key = key }.With(
            new UiNode(UiKind.Box) { Text = label },
            new UiNode(UiKind.ProgressBar) { Value = value, MaxValue = max, Color = "white" });
        return new UiNode(UiKind.Box).With(
            new UiNode(UiKind.Section) { Key = "stats", Title = "Pet Stats" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Key = "icon" }.With(PetIcon(c, data.Str("pet_state"))),
                new UiNode(UiKind.StackItem) { Key = "bars", Grow = 1 }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "level", Text = "Current Level: " + TguiFormat.JsNumber(data.Num("level")) },
                    Bar("happiness", "Happiness:", data.Num("happiness"), data.Num("maximum_happiness")),
                    Bar("exp", "Exp Progress:", data.Num("current_exp"), data.Num("required_exp")),
                    Bar("hunger", "Hunger:", data.Num("hunger"), data.Num("maximum_hunger")))))),
            new UiNode(UiKind.Section) { Key = "location", Title = "Pet Location" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Grow = 1, Text = data.Str("pet_area") },
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "summon",
                    Disabled = !data.Truthy("can_summon"),
                    Text = summoned ? "Recall" : "Release",
                    OnClick = () => c.Act("summon_pet"),
                }))),
            new UiNode(UiKind.Stack) { Key = "feed", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Section) { Title = "Pet Feed Dropzone" }.With(new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem) { Grow = 1, Text = selectedArea },
                    new UiNode(UiKind.StackItem).With(data.Truthy("in_dropzone")
                        ? new UiNode(UiKind.Button) { Key = "dropfeed", Text = "Get Food", OnClick = () => c.Act("drop_feed") }
                        : new UiNode(UiKind.Button)
                        {
                            Key = "reroll",
                            Disabled = !data.Truthy("can_reroll"),
                            Text = selectedArea == "No location set" ? "Generate" : "Reroll",
                            OnClick = () => c.Act("get_feed_location"),
                        })))),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Section) { Key = "steps", Bold = true, FontSize = "15px", Text = " Steps: " + TguiFormat.JsNumber(data.Num("steps_counter")) })));
    }

    // tg: NtosVirtualPet.tsx:244
    private static UiNode PetTricks(TguiContext c)
    {
        IReadOnlyList<string> possibleEmotes = c.Data.Arr("possible_emotes").Select(e => e.Text ?? "").ToList();
        string trickName = c.UseState("trick_name", "Trick");
        var sequences = Enumerable.Range(0, 4).Select(i => c.UseState("sequence:" + i, "none")).ToList();
        var list = new UiNode(UiKind.LabeledList);
        for (int index = 0; index < sequences.Count; index++)
        {
            int i = index;
            list.With(new UiNode(UiKind.LabeledItem) { Key = "sequence:" + i, Label = "Sequence " + (i + 1) }.With(new UiNode(UiKind.Dropdown)
            {
                Key = "trick:" + i,
                Width = "50%",
                TextValue = sequences[i],
                Options = possibleEmotes,
                OnSelected = selected => c.SetState("sequence:" + i, selected),
            }));
        }
        return new UiNode(UiKind.Section)
        {
            Key = "tricks",
            Title = TguiFormat.Capitalize(trickName),
            Buttons = new UiNode(UiKind.Input) { Key = "rename", Tooltip = "Rename Trick", TextValue = "", OnCommit = v => c.SetState("trick_name", v) },
        }.With(
            list,
            new UiNode(UiKind.Button)
            {
                Key = "teach",
                Fluid = true,
                TextAlign = "center",
                Text = "Teach",
                OnClick = () => c.Act("teach_tricks", ("trick_name", trickName), ("tricks", UiValue.ListOf(sequences.Select(UiValue.Of)))),
            });
    }

    // tg: NtosVirtualPet.tsx:295
    private static UiNode Customization(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> hats = data.Arr("hat_selections");
        IReadOnlyList<UiValue> colors = data.Arr("possible_colors");
        UiValue HatByName(string name) => hats.FirstOrDefault(h => h.Str("hat_name") == name);
        UiValue ColorByName(string name) => colors.FirstOrDefault(col => col.Str("color_name") == name);

        string selectedHat = c.UseState("hat", data.Str("pet_hat"));
        string selectedGender = c.UseState("gender", data.Str("pet_gender"));
        string selectedName = c.UseState("name", data.Str("pet_name"));
        string selectedColor = c.UseState("color", data.Str("pet_color"));
        UiNode Gender(string gender, string icon) => new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button)
        {
            Key = "gender:" + gender,
            Selected = selectedGender == gender,
            Icon = icon,
            OnClick = () => c.SetState("gender", gender),
        });

        return new UiNode(UiKind.Box).With(
            new UiNode(UiKind.Section) { Key = "preview", Title = "Pet Preview", TextAlign = "center" }.With(Picture("preview_icon", data.Get("preview_icon"), "160px")),
            new UiNode(UiKind.Stack) { Key = "row1" }.With(
                new UiNode(UiKind.StackItem) { Width = "50%" }.With(new UiNode(UiKind.Section) { Title = "Pet Name" }.With(new UiNode(UiKind.Input)
                {
                    Key = "name",
                    Fluid = true,
                    MaxLength = 30,
                    TextValue = selectedName,
                    OnInput = v => c.SetState("name", v),
                })),
                new UiNode(UiKind.StackItem) { Width = "50%" }.With(new UiNode(UiKind.Section) { Title = "Pet Hat" }.With(new UiNode(UiKind.Dropdown)
                {
                    Key = "pick-hat",
                    TextValue = HatByName(selectedHat)?.Str("hat_name"),
                    Options = hats.Select(h => h.Str("hat_name")).ToList(),
                    OnSelected = v => c.SetState("hat", v),
                }))),
            new UiNode(UiKind.Stack) { Key = "row2" }.With(
                new UiNode(UiKind.StackItem) { Width = "50%" }.With(new UiNode(UiKind.Section) { Title = "Pet Color" }.With(new UiNode(UiKind.Dropdown)
                {
                    Key = "pick-color",
                    TextValue = ColorByName(selectedColor)?.Str("color_name"),
                    Options = colors.Select(col => col.Str("color_name")).ToList(),
                    OnSelected = v => c.SetState("color", v),
                })),
                new UiNode(UiKind.StackItem) { Width = "50%" }.With(new UiNode(UiKind.Section) { Title = "Pet Gender" }.With(new UiNode(UiKind.Stack).With(
                    Gender("male", "mars"), Gender("female", "venus"), Gender("neuter", "neuter"))))),
            new UiNode(UiKind.Section) { Key = "apply", TextAlign = "center" }.With(new UiNode(UiKind.Button)
            {
                Key = "apply",
                Disabled = !data.Truthy("can_alter_appearance"),
                Text = "Apply",
                OnClick = () => c.Act("apply_customization",
                    ("chosen_hat", HatByName(selectedHat)?.Get("hat_id") ?? UiValue.Null),
                    ("chosen_name", selectedName),
                    ("chosen_color", ColorByName(selectedColor)?.Get("color_value") ?? UiValue.Null),
                    ("chosen_gender", selectedGender)),
            }));
    }

    // tg: NtosVirtualPet.tsx:430
    private static UiNode AllPetUpdates(TguiContext c)
    {
        var list = new UiNode(UiKind.Stack) { Vertical = true };
        foreach (UiValue update in c.Data.Arr("pet_updates"))
        {
            UiValue id = update.Get("update_id");
            string name = update.Str("update_name");
            string shortName = name.Length > 6 ? name[..6] : name;
            list.With(new UiNode(UiKind.StackItem) { Key = "update:" + TguiFormat.JsNumber(id?.Number ?? 0) }.With(new UiNode(UiKind.Box) { TextAlign = "center" }.With(
                new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem) { Width = "25%" }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                        new UiNode(UiKind.StackItem) { Text = shortName },
                        new UiNode(UiKind.StackItem).With(Picture("picture", update.Get("update_picture"), "64px")))),
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                        new UiNode(UiKind.Box) { Text = shortName + " " + update.Str("update_message") },
                        new UiNode(UiKind.Button)
                        {
                            Key = "like",
                            Selected = update.Truthy("update_already_liked"),
                            Color = "transparent",
                            Icon = "thumbs-up",
                            Text = TguiFormat.JsNumber(update.Num("update_likers")),
                            OnClick = () => c.Act("like_update", ("update_reference", id)),
                        })))));
        }
        return new UiNode(UiKind.Section) { Key = "updates", Title = "Pet Updates", Fill = true }.With(list);
    }

    // tg: NtosVirtualPet.tsx:497
    private static UiNode PetIcon(TguiContext c, string ourPetState)
    {
        UiValue iconDisplay = c.Data.Arr("pet_state_icons").FirstOrDefault(petIcon => petIcon.Str("name") == ourPetState);
        if (iconDisplay == null)
            return null;
        return new UiNode(UiKind.Stack) { Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(Picture("state_icon", iconDisplay.Get("icon"), "160px")),
            new UiNode(UiKind.StackItem) { TextAlign = "center", Text = TguiFormat.Capitalize(ourPetState) });
    }
}
