// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/shoes/sneakers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Holds the [DmPath] of a type that M2 F1's translated defaults name (the job outfit's slots, the cloth that repairs clothing, the
// prisoner suit get_random_jumpsuit leaves out), in the earliest .dme file that declares it (rules 2.3). J1 (D298): the orange
// shoes (the prisoners'): chaining handcuffs to them. Their machine_wash (washing_machine.dm:329) is in washing_machine.cs (gaps H1).
// Gaps CL1 (D333): the whole file (the sneakers' general colour for the blood and the random sneakers' colours); the digitigrade
// sneakers came with the lizards' legs (gaps SP1).

// tg: code/modules/clothing/shoes/sneakers.dm:1
[DmPath("/obj/item/clothing/shoes/sneakers")]
public partial class obj_item_clothing_shoes_sneakers : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/sneakers.dm:20
    public override object get_general_color(icon base_icon = null)
    {
        DmList colors = SSgreyscale.ParseColorString(greyscale_colors) as DmList;
        return Dm.truthy(colors) ? colors[1] : base.get_general_color(base_icon);
    }

    // tg: code/modules/clothing/shoes/sneakers.dm:24
    public override object generate_digitigrade_icons(icon base_icon = null, object greyscale_colors = null)
    {
        return Dm.icon(SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_digitigrade), greyscale_colors), "sneakers_worn");
    }
}

// tg: code/modules/clothing/shoes/sneakers.dm:27
public partial class obj_item_clothing_shoes_sneakers_random : obj_item_clothing_shoes_sneakers
{
    // tg: code/modules/clothing/shoes/sneakers.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        greyscale_colors = "#" + Dm.str(random_color()) + "#" + Dm.str(random_color());
        Waiting.NoSleep(update_greyscale());
        return dot;
    }
}

// tg: code/modules/clothing/shoes/sneakers.dm:35
[DmPath("/obj/item/clothing/shoes/sneakers/black")]
public partial class obj_item_clothing_shoes_sneakers_black : obj_item_clothing_shoes_sneakers
{
}

// tg: code/modules/clothing/shoes/sneakers.dm:107
public partial class obj_item_clothing_shoes_sneakers_orange : obj_item_clothing_shoes_sneakers
{
    // tg: code/modules/clothing/shoes/sneakers.dm:119
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob), ITEM_SLOT_HANDCUFFED));
        return dot;
    }

    // tg: code/modules/clothing/shoes/sneakers.dm:123
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref attached_cuffs);
        return base.Destroy(force);
    }

    // tg: code/modules/clothing/shoes/sneakers.dm:127
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == attached_cuffs)
        {
            attached_cuffs = null;
            slowdown = SHOES_SLOWDOWN;
            update_appearance(UPDATE_ICON);
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/clothing/shoes/sneakers.dm:134
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (arrived.type == typeof(obj_item_restraints_handcuffs))
        {
            attached_cuffs = arrived as obj_item_restraints_handcuffs;
            slowdown = 15;
            update_appearance(UPDATE_ICON);
        }
        return await base.Entered(arrived, old_loc, old_locs);
    }

    // tg: code/modules/clothing/shoes/sneakers.dm:141
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (attached_cuffs != null)
        {
            icon_state = "sneakers_chained";
            inhand_icon_state = icon_state;
        }
        else
        {
            object initial_state = Dm.initial(this, (atom p) => p.post_init_icon_state);
            icon_state = (string)(Dm.truthy(initial_state) ? initial_state : Dm.initial(this, (atom p) => p.icon_state));
            inhand_icon_state = (string)Dm.initial(this, (obj_item p) => p.inhand_icon_state);
        }
        await update_greyscale();
        return dot;
    }

    // tg: code/modules/clothing/shoes/sneakers.dm:150
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (attached_cuffs != null)
        {
            to_chat(user, span_notice(I18n.T("You remove [] from [].", attached_cuffs, this)));
            if (Dm.truthy(await Adjacent(user))) //tk is love, tk is life.
                await user.put_in_hands(attached_cuffs);
            else
                await attached_cuffs.forceMove(drop_location() as atom);
            return null;
        }
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/modules/clothing/shoes/sneakers.dm:160
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (attached_cuffs != null || tool.type != typeof(obj_item_restraints_handcuffs)) // Note: not using istype here because we want to ignore all subtypes
            return await base.item_interaction(user, tool, modifiers, __extra);
        await tool.forceMove(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/clothing/shoes/sneakers.dm:166
    public override object can_mob_unequip(mob user = null)
    {
        if (Dm.eq(user.get_item_by_slot(slot_flags), this) && attached_cuffs != null)
        {
            to_chat(user, span_warning(I18n.T("You need help taking these off!")));
            return FALSE;
        }
        return base.can_mob_unequip(user);
    }

    // tg: code/modules/clothing/shoes/sneakers.dm:172
    [DmParams("over_object", "user")]
    public override async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        if (ishuman(user))
        {
            mob_living_carbon_human c = user as mob_living_carbon_human;
            if (c.shoes == this && attached_cuffs != null)
            {
                to_chat(c, span_warning(I18n.T("You need help taking these off!")));
                return null;
            }
        }
        return await base.mouse_drop_dragged(over, user, src_location, over_location, @params);
    }
}
