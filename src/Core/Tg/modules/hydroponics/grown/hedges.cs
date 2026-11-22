// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/hedges.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/hedges.dm:19
public partial class obj_item_grown_shrub : obj_item_grown
{
    // tg: code/modules/hydroponics/grown/hedges.dm:24
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        turf player_turf = get_turf(user);
        if (player_turf != null && Dm.truthy(await player_turf.is_blocked_turf(TRUE)))
            return FALSE;
        user.visible_message(span_danger(I18n.T("[] begins to plant \\the []...", user, this)));
        if (Dm.truthy(await do_after(user, 8 * SECONDS, target: user.drop_location())))
        {
            Dm.@new<obj_structure_hedge_opaque>(user.drop_location());
            to_chat(user, span_notice(I18n.T("You plant \\the [].", this)));
            qdel(this);
        }
        return null;
    }
}

// tg: code/modules/hydroponics/grown/hedges.dm:36
public partial class obj_structure_hedge : obj_structure
{
    // tg: code/modules/hydroponics/grown/hedges.dm:49
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(opacity) || !HAS_TRAIT(user, TRAIT_BONSAI) || !Dm.truthy(tool.get_sharpness()))
            return NONE;
        balloon_alert(user, I18n.T("trimming..."));
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;
        await set_opacity(FALSE);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/hydroponics/grown/hedges.dm:65
public partial class obj_item_seeds_organ_tree : obj_item_seeds
{
    // tg: code/modules/hydroponics/grown/hedges.dm:80
    public override async DmTask<object> harvest(mob user = null)
    {
        float yield_amount = Dm.num(getYield());
        obj_machinery_hydroponics parent = loc as obj_machinery_hydroponics;
        if (yield_amount <= 0)
        {
            await parent.update_tray(user, yield_amount);
            return Dm.list();
        }

        DmList possible_organs = Dm.list(
            Dm.kv(typeof(obj_item_bodypart_arm_left_pod), 2),
            Dm.kv(typeof(obj_item_bodypart_arm_right_pod), 2),
            Dm.kv(typeof(obj_item_bodypart_leg_left_pod), 2),
            Dm.kv(typeof(obj_item_bodypart_leg_right_pod), 2),
            Dm.kv(typeof(obj_item_food_meat_slab_human_mutant_plant), 3),
            Dm.kv(typeof(obj_item_organ_appendix_pod), 1),
            Dm.kv(typeof(obj_item_organ_brain_pod), 1),
            Dm.kv(typeof(obj_item_organ_ears_pod), 1),
            Dm.kv(typeof(obj_item_organ_eyes_pod), 1),
            Dm.kv(typeof(obj_item_organ_heart_pod), 1),
            Dm.kv(typeof(obj_item_organ_liver_pod), 1),
            Dm.kv(typeof(obj_item_organ_lungs_pod), 1),
            Dm.kv(typeof(obj_item_organ_stomach_pod), 1),
            Dm.kv(typeof(obj_item_organ_tongue_pod), 1));

        DmList created = Dm.list();
        atom drop_at = (Dm.truthy(await user.Adjacent(loc as atom)) ? user.drop_location() : drop_location()) as atom;
        for (float i = 1; i <= yield; i++)
        {
            object organ = pick_weight(possible_organs);
            if (prob(66)) // 66% chance to reduce the chance to 0 so we get less duplicates
                possible_organs[organ] = 0;
            obj_item spawned = Dm.@new((Type)organ, drop_at) as obj_item;
            if (spawned is obj_item_bodypart bodypart_spawned)
            {
                bodypart_spawned.species_color = COLOR_GREEN;
                await bodypart_spawned.update_icon_dropped();
            }
            qdel(spawned.GetComponent(typeof(datum_component_decomposition)));
            qdel(spawned.GetComponent(typeof(datum_component_germ_sensitive)));
            Dm.append(ref created, spawned);
        }

        await parent.update_tray(user, yield_amount);

        return created;
    }
}
