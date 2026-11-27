using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SparringContract.tsx: the sparring contract's window
// (/obj/item/sparring_contract's ui_data and ui_static_data, sparring_contract.dm): the terms (weapons, arena, stakes), the two
// signatures and the fight button. The terms picked but not signed yet are the window's own state (tg's useState). Engine-free
// (compiled into the tests too). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class SparringContract
{
    // tg: SparringContract.tsx:13
    private static readonly string[] weaponlist =
    {
        "Fist Fight",
        "Ceremonial Weapons",
        "Melee Only",
        "Any Weapons",
    };

    // tg: SparringContract.tsx:20
    private static readonly string[] stakelist = { "No Stakes", "Holy Match", "Money Match", "Your Soul" };

    // tg: SparringContract.tsx:22
    private static readonly string[] weaponblurb =
    {
        "You will fight with your fists only. Any weapons will be considered a violation.",
        "You can only fight with ceremonial weapons. You will be at a severe disadvantage without one!",
        "You can fight with weapons, or fists if you have none. Ranged weapons are a violation.",
        "You can fight with any and all weapons as you please. Try not to kill them, okay?",
    };

    // tg: SparringContract.tsx:29
    private static readonly string[] stakesblurb =
    {
        "No stakes, just for fun. Who doesn't love some recreational sparring?",
        "A match for the chaplain's deity. The Chaplain suffers large consequences for failure, but advances their sect by winning.",
        "A match with money on the line. Whomever wins takes all the money of whomever loses.",
        "A lethal match with the loser's soul becoming under ownership of the winner.",
    };

    private static string At(string[] list, int oneBased) => oneBased >= 1 && oneBased <= list.Length ? list[oneBased - 1] : null;

    // tg: SparringContract.tsx:48
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        int set_weapon = (int)data.Num("set_weapon", 1);
        string set_area = data.Str("set_area");
        int set_stakes = (int)data.Num("set_stakes", 1);
        List<string> possible_areas = data.Arr("possible_areas").Select(a => a.AsStr()).ToList();
        string left_sign = data.Str("left_sign");
        string right_sign = data.Str("right_sign");
        bool in_area = data.Truthy("in_area");
        bool no_chaplains = data.Truthy("no_chaplains");
        int stakes_holy_match = (int)data.Num("stakes_holy_match");
        // tg: SparringContract.tsx:61  const [weapon, setWeapon] = useState(set_weapon); [area, setArea]; [stakes, setStakes]
        int weapon = c.UseState("weapon", set_weapon);
        string area = c.UseState("area", set_area);
        int stakes = c.UseState("stakes", set_stakes);
        bool bothSigned = left_sign != "none" && right_sign != "none";

        UiNode SignSlot(string key, string sign, bool left) =>
            new UiNode(UiKind.StackItem) { Key = key, Grow = 1, FontSize = sign != "none" ? "14px" : null }.With(sign == "none"
                ? new UiNode(UiKind.Button)
                {
                    Key = "sign",
                    Icon = "pen",
                    Text = "Sign Here",
                    OnClick = () =>
                    {
                        if (left)
                            c.Act("sign", ("weapon", weapon), ("area", area), ("stakes", stakes), ("sign_position", "left"));
                        else
                            c.Act("sign", ("weapon", weapon), ("area", area), ("stakes", stakes));
                    },
                }
                : new UiNode(UiKind.Box) { Key = "signature", Text = sign });

        // tg: SparringContract.tsx:67  <Window width={420} height={380}><Window.Content><Section fill><Stack vertical fill>
        return c.Window(420, 380, null,
            new UiNode(UiKind.Section) { Key = "contract", Fill = true }.With(
                new UiNode(UiKind.Stack) { Key = "root", Vertical = true, Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "weapons" }.With(new UiNode(UiKind.Stack) { Key = "stack", Vertical = true }.With(
                        new UiNode(UiKind.StackItem) { Key = "head" }.With(new UiNode(UiKind.Stack) { Key = "row", Fill = true }.With(
                            new UiNode(UiKind.StackItem) { Key = "label", Grow = 1, FontSize = "16px", Text = "Weapons:" },
                            new UiNode(UiKind.StackItem) { Key = "info" }.With(new UiNode(UiKind.Button)
                            {
                                Key = "contract",
                                Icon = "info",
                                Text = "Contract?",
                                Tooltip = "The Chaplain's Deity wishes for honorable fighting. As such, it uses contracts. Signing your name will set the terms for the battle. Then, the person you intend to spar with must sign the other side. If terms are changed on an already signed contract, the signatures will erase and the new terms must be re-agreed upon.",
                            }))),
                        new UiNode(UiKind.StackItem) { Key = "dropdown" }.With(new UiNode(UiKind.Dropdown)
                        {
                            Key = "pick:weapon",
                            Width = "100%",
                            Options = weaponlist,
                            TextValue = At(weaponlist, weapon),
                            OnSelected = value => c.SetState("weapon", System.Array.IndexOf(weaponlist, value) + 1),
                        }),
                        new UiNode(UiKind.StackItem) { Key = "blurb" }.With(BlockQuote("text", At(weaponblurb, weapon))))),
                    new UiNode(UiKind.StackItem) { Key = "arena" }.With(new UiNode(UiKind.Stack) { Key = "stack", Vertical = true }.With(
                        new UiNode(UiKind.StackItem) { Key = "label", FontSize = "16px", Text = "Arena:" },
                        new UiNode(UiKind.StackItem) { Key = "dropdown" }.With(new UiNode(UiKind.Dropdown)
                        {
                            Key = "pick:area",
                            Width = "100%",
                            Options = possible_areas,
                            TextValue = area,
                            OnSelected = value => c.SetState("area", value),
                        }),
                        new UiNode(UiKind.StackItem) { Key = "blurb" }.With(BlockQuote("text", "This fight will take place in the " + area + ". Leaving the arena mid-fight is a violation.")))),
                    new UiNode(UiKind.StackItem) { Key = "stakes" }.With(new UiNode(UiKind.Stack) { Key = "stack", Vertical = true }.With(
                        new UiNode(UiKind.StackItem) { Key = "label", FontSize = "16px", Text = "Stakes:" },
                        new UiNode(UiKind.StackItem) { Key = "dropdown" }.With(new UiNode(UiKind.Dropdown)
                        {
                            Key = "pick:stakes",
                            Width = "100%",
                            Options = stakelist,
                            TextValue = At(stakelist, stakes),
                            OnSelected = value => c.SetState("stakes", System.Array.IndexOf(stakelist, value) + 1),
                        }),
                        new UiNode(UiKind.StackItem) { Key = "blurb" }.With(BlockQuote("text", At(stakesblurb, stakes))))),
                    new UiNode(UiKind.StackItem) { Key = "signatures", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "row", TextAlign = "center" }.With(
                        SignSlot("left", left_sign, true),
                        new UiNode(UiKind.StackItem) { Key = "vs", FontSize = "16px", Text = "VS" },
                        SignSlot("right", right_sign, false))),
                    new UiNode(UiKind.StackItem) { Key = "footer" }.With(new UiNode(UiKind.Stack) { Key = "row", Fill = true }.With(
                        new UiNode(UiKind.StackItem) { Key = "actions", Grow = 1 }.With(
                            new UiNode(UiKind.Button)
                            {
                                Key = "fight",
                                Icon = "fist-raised",
                                Text = "FIGHT!",
                                Disabled = !in_area || (no_chaplains && set_stakes == stakes_holy_match),
                                OnClick = () => c.Act("fight"),
                            },
                            new UiNode(UiKind.Button)
                            {
                                Key = "clear",
                                Icon = "door-open",
                                Text = "Clear",
                                Tooltip = "If you've already signed but you want to renegotiate the terms, you can clear out the signatures with this button.",
                                OnClick = () => c.Act("clear"),
                            }),
                        new UiNode(UiKind.StackItem) { Key = "status" }.With(
                            new UiNode(UiKind.Button)
                            {
                                Key = "arena",
                                Icon = "ring",
                                Text = "Arena",
                                Color = in_area ? "green" : "red",
                                Tooltip = in_area ? "Both participants are present in the " + area + "." : "Both participants need to be in the arena!",
                            },
                            new UiNode(UiKind.Button)
                            {
                                Key = "signed",
                                Icon = "file-signature",
                                Text = "Signatures",
                                Color = bothSigned ? "green" : "red",
                                Tooltip = bothSigned ? "Both signatures present, terms agreed upon." : "You need signatures from both fighters on the terms!",
                            },
                            new UiNode(UiKind.Button)
                            {
                                Key = "chaplain",
                                Icon = "cross",
                                Text = "Chaplain",
                                Color = !no_chaplains ? "green" : "yellow",
                                Tooltip = !no_chaplains ? "At least one chaplain is present. Holy matches allowed." : "No chaplain present for this fight. No Holy Matches!",
                            }))))));
    }

    // tgui-core's BlockQuote: a grey, indented block of text.
    private static UiNode BlockQuote(string key, string text) => new UiNode(UiKind.Box) { Key = key, Color = "label", Text = text };
}
