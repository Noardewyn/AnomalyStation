// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/wig.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the wig drawn as the hairstyle it is set to, worn as hair over the head; styled in hand through
// tg's list and colour pickers, or copied from another's hair; the natural wig takes the wearer's hair colour, the random one a random
// style). The hat stabilizer (a hat worn over the wig, datums/components/hat_stabilizer.dm) came with gaps MO1. The
// emissive blocker of the hair overlay is DV12. The var blocks are generated (D111).

// tg: code/modules/clothing/head/wig.dm:1
public partial class obj_item_clothing_head_wig : obj_item_clothing_head
{
    // tg: code/modules/clothing/head/wig.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        Waiting.NoSleep(AddComponent(typeof(datum_component_hat_stabilizer), Dm.named("loose_hat", FALSE))); // gaps MO1: the component is live
        return dot;
    }

    // tg: code/modules/clothing/head/wig.dm:20
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (ishuman(user) && Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HEAD)))
            ADD_TRAIT(this, TRAIT_EXAMINE_SKIP, CLOTHING_TRAIT);
    }

    // tg: code/modules/clothing/head/wig.dm:25
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        REMOVE_TRAIT(this, TRAIT_EXAMINE_SKIP, CLOTHING_TRAIT);
        return dot;
    }

    // tg: code/modules/clothing/head/wig.dm:29
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        datum_sprite_accessory_hair hair_style = SSaccessories.hairstyles_list[hairstyle] as datum_sprite_accessory_hair;
        if (hair_style != null)
        {
            icon = hair_style.icon;
            icon_state = Dm.str(hair_style.icon_state);
        }
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/clothing/head/wig.dm:36
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands))
            return dot;

        datum_sprite_accessory_hair hair = SSaccessories.hairstyles_list[hairstyle] as datum_sprite_accessory_hair;
        if (hair == null)
            return dot;

        mutable_appearance hair_overlay = mutable_appearance(hair.icon, Dm.str(hair.icon_state), layer: -HAIR_LAYER, appearance_flags: RESET_COLOR) as mutable_appearance;
        hair_overlay.color = color;
        hair_overlay.pixel_z = hair.y_offset;
        Dm.append(ref dot, hair_overlay);

        // So that the wig actually blocks emissives.
        // DEVIATION(DV12): hair_overlay.overlays += emissive_blocker(hair_overlay.icon, hair_overlay.icon_state, src, alpha =
        // hair_overlay.alpha): emissives are not ported
        return dot;
    }

    // tg: code/modules/clothing/head/wig.dm:53
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object new_style = await tgui_input_list(user, I18n.T("Select a hairstyle"), I18n.T("Wig Styling"), Dm.sub(SSaccessories.hairstyles_list, "Bald") as DmList);
        object newcolor = Dm.truthy(adjustablecolor) ? await tgui_color_picker(usr, "", I18n.T("Choose Color"), color) : null;
        if (!Dm.truthy(await user.can_perform_action(this)))
            return null;
        if (Dm.truthy(new_style) && !Dm.eq(new_style, hairstyle))
        {
            hairstyle = Dm.str(new_style);
            user.visible_message(span_notice(I18n.T("[] changes \\the []'s hairstyle to [].", user, this, new_style)), span_notice(I18n.T("You change \\the []'s hairstyle to [].", this, new_style)));
        }
        if (Dm.truthy(newcolor) && !Dm.eq(newcolor, color)) // only update if necessary
            await add_atom_colour(newcolor, FIXED_COLOUR_PRIORITY);
        update_appearance();
        return null;
    }

    // tg: code/modules/clothing/head/wig.dm:65
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/modules/clothing/head/wig.dm:68
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!ishuman(interacting_with) || interacting_with == user)
            return NONE;
        mob_living_carbon_human target = (mob_living_carbon_human)interacting_with;
        if (target.head != null)
        {
            obj_item_clothing head = target.head as obj_item_clothing;
            if (Dm.truthy(Dm.bitand(head.flags_inv, HIDEHAIR)) && !(head is obj_item_clothing_head_wig))
            {
                to_chat(user, span_warning(I18n.T("You can't get a good look at [] hair!", target.p_their())));
                return ITEM_INTERACT_BLOCKING;
            }
        }
        obj_item_bodypart_head noggin = target.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (noggin == null)
        {
            to_chat(user, span_warning(I18n.T("[] have no head!", target.p_They())));
            return ITEM_INTERACT_BLOCKING;
        }

        object selected_hairstyle = null;
        object selected_hairstyle_color = null;
        if (target.head is obj_item_clothing_head_wig)
        {
            obj_item_clothing_head_wig wig = (obj_item_clothing_head_wig)target.head;
            selected_hairstyle = wig.hairstyle;
            selected_hairstyle_color = wig.color;
        }
        else if (Dm.truthy(Dm.bitand(noggin.head_flags, HEAD_HAIR)) && target.hairstyle != "Bald")
        {
            selected_hairstyle = target.hairstyle;
            selected_hairstyle_color = Dm.text("[]", target.hair_color);
        }

        if (Dm.truthy(selected_hairstyle))
        {
            to_chat(user, span_notice(I18n.T("You adjust the [] to look just like []'s [].", this, target.name, selected_hairstyle)));
            await add_atom_colour(selected_hairstyle_color, FIXED_COLOUR_PRIORITY);
            hairstyle = Dm.str(selected_hairstyle);
            update_appearance();
        }
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/clothing/head/wig.dm:99
public partial class obj_item_clothing_head_wig_random : obj_item_clothing_head_wig
{
    // tg: code/modules/clothing/head/wig.dm:99
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        hairstyle = Dm.str(pick(Dm.sub(SSaccessories.hairstyles_list, "Bald"))); //Don't want invisible wig
        Waiting.NoSleep(add_atom_colour(Dm.text("#[]", random_short_color()), FIXED_COLOUR_PRIORITY));
        object dot = base.Initialize(mapload, __extra);
        return dot;
    }
}

// tg: code/modules/clothing/head/wig.dm:104
public partial class obj_item_clothing_head_wig_natural : obj_item_clothing_head_wig
{
    // tg: code/modules/clothing/head/wig.dm:111
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        hairstyle = Dm.str(pick(Dm.sub(SSaccessories.hairstyles_list, "Bald")));
        object dot = base.Initialize(mapload, __extra);
        return dot;
    }

    // tg: code/modules/clothing/head/wig.dm:115
    [DmParams("user", "slot")]
    public override async DmTask<object> visual_equipped(mob user = null, object slot = null, float? initial = null)
    {
        object dot = await base.visual_equipped(user, slot, initial);
        if (ishuman(user) && Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HEAD)))
        {
            mob_living_carbon_human human_user = (mob_living_carbon_human)user;
            if (!Dm.eq(color, human_user.hair_color)) // only update if necessary
            {
                await add_atom_colour(human_user.hair_color, FIXED_COLOUR_PRIORITY);
                update_appearance();
            }
            await human_user.update_worn_head();
        }
        return dot;
    }
}
