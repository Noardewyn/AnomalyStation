// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/gloves/_gloves.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the fantasy bonuses (generated live-partial stubs, M10; the suicide act came with gaps CL1): worn_overlays,
// separate_worn_overlays and update_clothes_damaged_state (M4a clothing: gloves are put on, taken off and drawn on the
// wearer), wash (M4b: water from a fire extinguisher), and can_cut_with and item_interaction (the M7 shocks slice: cutting
// the fingertips off; the gloves turn live with the insulated ones, whose siemens_coefficient a shock reads).

// tg: code/modules/clothing/gloves/_gloves.dm:1
public partial class obj_item_clothing_gloves : obj_item_clothing
{
    // tg: code/modules/clothing/gloves/_gloves.dm:38  (M4b: the fire extinguisher's water washes what it reaches)
    public override async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        object dot = await base.wash(clean_types, __extra);
        if (Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_BLOOD)) && transfer_blood > 0)
        {
            transfer_blood = 0;
            dot = Dm.bitor(dot, Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP));
        }
        return dot;
    }

    // tg: code/modules/clothing/gloves/_gloves.dm:48
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands))
            return dot;
        if (Dm.truthy(damaged_clothes))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/effects/item_damage.dmi"), "damagedgloves"));
        return dot;
    }

    // tg: code/modules/clothing/gloves/_gloves.dm:55
    public override async DmTask<object> separate_worn_overlays(mutable_appearance standing = null, mutable_appearance draw_target = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        DmList dot = null;
        dot = await base.separate_worn_overlays(standing, draw_target, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands))
            return dot;
        object blood_overlay = await get_blood_overlay("glove", bodyshape);
        if (Dm.truthy(blood_overlay))
            Dm.append(ref dot, blood_overlay);
        return dot;
    }

    // tg: code/modules/clothing/gloves/_gloves.dm:63
    public override async DmTask update_clothes_damaged_state(float? damaged_state = null)
    {
        damaged_state ??= CLOTHING_DAMAGED;
        await base.update_clothes_damaged_state(damaged_state);
        if (ismob(loc))
        {
            mob M = loc as mob;
            await M.update_worn_gloves();
        }
    }

    // tg: code/modules/clothing/gloves/_gloves.dm:69  (the M7 shocks slice: the gloves turn live with the insulated ones)
    public virtual object can_cut_with(obj_item tool = null)
    {
        if (!Dm.truthy(cut_type))
            return FALSE;
        if (icon_state != Dm.initial(this, (obj_item_clothing_gloves p) => p.icon_state))
            return FALSE; // We don't want to cut dyed gloves.
        return TRUE;
    }

    // tg: code/modules/clothing/gloves/_gloves.dm:76  (the M7 shocks slice)
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(ITEM_INTERACT_ANY_BLOCKER, dot)))
            return dot;

        if (!Dm.eq(tool.tool_behaviour, TOOL_WIRECUTTER) && !Dm.truthy(tool.get_sharpness()))
            return dot;

        if (!Dm.truthy(can_cut_with(tool)))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("cutting off fingertips..."));

        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this, extra_checks: Dm.@new<datum_callback>(this, nameof(can_cut_with), tool))))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("cut fingertips off"));
        qdel(this);
        await user.put_in_hands(Dm.@new(cut_type) as obj_item);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/clothing/gloves/_gloves.dm:44
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("\\the [] are forcing []'s hands around [] neck! It looks like the gloves are possessed!", this, user, user.p_their())));
        return OXYLOSS;
    }
}
