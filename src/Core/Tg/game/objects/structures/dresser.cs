// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/dresser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H1: the whole file (the dresser: changing underwear, its colour, the undershirt and the socks; wrenching it). The var blocks
// are generated (D111).

// tg: code/game/objects/structures/dresser.dm:1
public partial class obj_structure_dresser : obj_structure
{
    // tg: code/game/objects/structures/dresser.dm:11
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        to_chat(user, span_notice(Dm.truthy(anchored) ? I18n.T("You begin to unwrench [].", this) : I18n.T("You begin to wrench [].", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 20, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(Dm.truthy(anchored) ? I18n.T("You successfully unwrench [].", this) : I18n.T("You successfully wrench [].", this)));
        await set_anchored(!Dm.truthy(anchored) ? TRUE : FALSE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/dresser.dm:20
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_mineral_wood>(drop_location(), 10);
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/dresser.dm:23
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await Adjacent(user))) //no tele-grooming
            return dot;
        if (user is not mob_living_carbon_human dressing_human)
            return dot;
        if (HAS_TRAIT(dressing_human, TRAIT_NO_UNDERWEAR))
        {
            to_chat(dressing_human, span_warning(I18n.T("You are not capable of wearing underwear.")));
            return dot;
        }

        object choice = await tgui_input_list(user, I18n.T("Underwear, Undershirt, or Socks?"), I18n.T("Changing"), Dm.list("Underwear", "Underwear Color", "Undershirt", "Socks"));
        if (choice == null)
            return dot;

        if (!Dm.truthy(await Adjacent(user)))
            return dot;
        switch (Dm.str(choice))
        {
            case "Underwear":
            {
                object new_undies = await tgui_input_list(user, I18n.T("Select your underwear"), I18n.T("Changing"), SSaccessories.underwear_list);
                if (Dm.truthy(new_undies))
                    dressing_human.underwear = Dm.str(new_undies);
                break;
            }
            case "Underwear Color":
            {
                object new_underwear_color = await tgui_color_picker(dressing_human, I18n.T("Choose your underwear color"), I18n.T("Underwear Color"), dressing_human.underwear_color);
                if (Dm.truthy(new_underwear_color))
                    dressing_human.underwear_color = Dm.str(sanitize_hexcolor(new_underwear_color));
                break;
            }
            case "Undershirt":
            {
                object new_undershirt = await tgui_input_list(user, I18n.T("Select your undershirt"), I18n.T("Changing"), SSaccessories.undershirt_list);
                if (Dm.truthy(new_undershirt))
                    dressing_human.undershirt = Dm.str(new_undershirt);
                break;
            }
            case "Socks":
            {
                object new_socks = await tgui_input_list(user, I18n.T("Select your socks"), I18n.T("Changing"), SSaccessories.socks_list);
                if (Dm.truthy(new_socks))
                    dressing_human.socks = Dm.str(new_socks);
                break;
            }
        }

        add_fingerprint(dressing_human);
        await dressing_human.update_body();
        return dot;
    }
}
