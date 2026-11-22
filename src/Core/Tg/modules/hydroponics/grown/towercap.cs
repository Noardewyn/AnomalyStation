// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/towercap.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/towercap.dm:34
public partial class obj_item_grown_log : obj_item_grown
{
    // tg: code/modules/hydroponics/grown/towercap.dm:60
    [DmParams("mapload", "new_seed")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        if (seed != null)
            plank_count += Dm.round(Dm.num(((obj_item_seeds)seed).potency) / 25);
        return dot;
    }

    // tg: code/modules/hydroponics/grown/towercap.dm:87
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(tool.get_sharpness()))
        {
            user.balloon_alert(user, I18n.T("made [] []", plank_count, plank_name));
            Dm.@new(plank_type, user.loc, plank_count);
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(CheckAccepted(tool)))
        {
            obj_item_food_grown leaf = tool as obj_item_food_grown;
            if (!HAS_TRAIT(leaf, TRAIT_DRIED))
            {
                balloon_alert(user, I18n.T("dry it first!"));
                return ITEM_INTERACT_BLOCKING;
            }

            user.balloon_alert(user, I18n.T("torch crafted"));
            obj_item_flashlight_flare_torch new_torch = Dm.@new<obj_item_flashlight_flare_torch>(user.loc);
            await user.dropItemToGround(tool);
            await user.put_in_active_hand(new_torch);
            qdel(leaf);
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/hydroponics/grown/towercap.dm:110
    public virtual object CheckAccepted(obj_item I = null)
    {
        return is_type_in_typecache(I, accepted);
    }

    /// var/static/list/accepted (rules 10.6)
    // tg: code/modules/hydroponics/grown/towercap.dm:51
    public static ref DmList accepted => ref TgState.Current.obj_item_grown_log__accepted;
}

// tg: code/modules/hydroponics/grown/towercap.dm:120
public partial class obj_item_grown_log_steel : obj_item_grown_log
{
    // tg: code/modules/hydroponics/grown/towercap.dm:127
    public override object CheckAccepted(obj_item I = null)
    {
        return FALSE;
    }
}

// tg: code/modules/hydroponics/grown/towercap.dm:131
public partial class obj_structure_punji_sticks : obj_structure
{
    // tg: code/modules/hydroponics/grown/towercap.dm:144
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_caltrop), Dm.named("min_damage", 20), Dm.named("max_damage", 30), Dm.named("flags", CALTROP_BYPASS_SHOES)));
        build_stab_overlay();
        return dot;
    }

    // tg: code/modules/hydroponics/grown/towercap.dm:149
    public virtual void build_stab_overlay()
    {
        stab_overlay = mutable_appearance(icon, Dm.text("[]_stab", icon_state), layer: ABOVE_MOB_LAYER) as mutable_appearance;
    }

    // tg: code/modules/hydroponics/grown/towercap.dm:152
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (Dm.truthy(same_z_layer))
            return;
        build_stab_overlay();
        update_appearance();
    }

    // tg: code/modules/hydroponics/grown/towercap.dm:159
    public override async DmTask post_buckle_mob(mob_living buckled_mob = null)
    {
        update_appearance();
        await base.post_buckle_mob(buckled_mob);
    }

    // tg: code/modules/hydroponics/grown/towercap.dm:163
    public override async DmTask post_unbuckle_mob(mob_living unbuckled_mob = null)
    {
        update_appearance();
        await base.post_unbuckle_mob(unbuckled_mob);
    }

    // tg: code/modules/hydroponics/grown/towercap.dm:167
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(length(buckled_mobs)))
            Dm.append(ref dot, stab_overlay);
        return dot;
    }

    // tg: code/modules/hydroponics/grown/towercap.dm:172
    public override async DmTask<object> intercept_zImpact(DmList falling_movables = null, float? levels = null)
    {
        object dot = await base.intercept_zImpact(falling_movables, levels);
        foreach (mob_living fallen_mob in Dm.each<mob_living>(falling_movables))
        {
            if (Dm.truthy(LAZYLEN(buckled_mobs)))
                return dot;
            if (Dm.truthy(await buckle_mob(fallen_mob, TRUE)))
            {
                to_chat(fallen_mob, span_userdanger(I18n.T("You are impaled by []!", this)));
                await fallen_mob.apply_damage(25 * Dm.num(levels), BRUTE, sharpness: SHARP_POINTY);
                if (fallen_mob is mob_living_carbon fallen_carbon)
                {
                    await fallen_carbon.emote("scream");
                    await fallen_carbon.bleed(30);
                }
            }
        }
        dot = Dm.bitor(dot, Dm.bitor(FALL_INTERCEPTED, FALL_NO_MESSAGE));
        return dot;
    }

    // tg: code/modules/hydroponics/grown/towercap.dm:186
    public override async DmTask<object> unbuckle_mob(mob_living buckled_mob = null, float? force = null, float? can_fall = null)
    {
        if (Dm.truthy(force))
            return await base.unbuckle_mob(buckled_mob, force, can_fall);
        to_chat(buckled_mob, span_warning(I18n.T("You begin climbing out of [].", this)));
        await buckled_mob.apply_damage(5, BRUTE, sharpness: SHARP_POINTY);
        if (!Dm.truthy(await do_after(buckled_mob, 5 * SECONDS, target: this)))
        {
            to_chat(buckled_mob, span_userdanger(I18n.T("You fail to detach yourself from [].", this)));
            return null;
        }
        return await base.unbuckle_mob(buckled_mob, force, can_fall);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/hydroponics/grown/towercap.dm:51  var/static/list/accepted = typecacheof(list(...))
    internal DmList obj_item_grown_log__accepted = typecacheof(Dm.list(
        typeof(obj_item_food_grown_tobacco),
        typeof(obj_item_food_grown_tea),
        typeof(obj_item_food_grown_ash_flora_mushroom_leaf),
        typeof(obj_item_food_grown_ambrosia_vulgaris),
        typeof(obj_item_food_grown_ambrosia_deus),
        typeof(obj_item_food_grown_wheat))) as DmList;
}
