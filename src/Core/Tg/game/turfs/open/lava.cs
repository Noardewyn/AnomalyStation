// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/lava.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D304) but the plasma lava (:448-532, Icemoon's and the station's, which Lavaland does not have). Lava is a lazy fishing spot. The
// immerse element (datums/elements/immerse.dm: a vis_contents mask that sinks a standing atom into the liquid) is BYOND
// presentation the 3D client draws its own way (DV12, as the emissive and lighting-plane overlays below).

// tg: code/game/turfs/open/lava.dm:7
public partial class turf_open_lava : turf_open
{
    ///Generic return value of the can_burn_stuff() proc. Does nothing.
    private const int LAVA_BE_IGNORING = 0;
    /// Another. Won't burn the target but will make the turf start processing.
    private const int LAVA_BE_PROCESSING = 1;
    /// Burns the target and makes the turf process (depending on the return value of do_burn()).
    private const int LAVA_BE_BURNING = 2;

    // tg: code/game/turfs/open/lava.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(fish_source_type))
            add_lazy_fishing(fish_source_type);
        // You can release chrabs and lavaloops and likes in lava, or be an absolute scumbag and drop other fish there too.
        ADD_TRAIT(this, TRAIT_CATCH_AND_RELEASE, INNATE_TRAIT);
        refresh_light();
        if (!Dm.truthy(smoothing_flags))
            update_appearance();
        RegisterSignal(this, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(on_atom_inited));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_LAVA_STOPPED), nameof(drop_contents_into_lava));
        return dot;
    }

    // tg: code/game/turfs/open/lava.dm:62
    public override object Destroy(float? force = null)
    {
        checked_atoms = null;
        UnregisterSignal(this, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON);
        foreach (mob_living leaving_mob in Dm.each<mob_living>(contents))
        {
            leaving_mob.RemoveElement(typeof(datum_element_perma_fire_overlay));
            REMOVE_TRAIT(leaving_mob, TRAIT_NO_EXTINGUISH, TURF_TRAIT);
        }
        return base.Destroy(force);
    }

    ///We lazily add the immerse element when something is spawned or crosses this turf and not before.
    // tg: code/game/turfs/open/lava.dm:71
    [SignalHandler]
    public virtual void on_atom_inited(datum source = null, atom_movable movable = null)
    {
        if (burn_stuff(movable))
            START_PROCESSING(SSobj, this);
        if (Dm.truthy(immerse_added))
            return;
        // DEVIATION(DV12): is_type_in_typecache(movable, GLOB.immerse_ignored_movable) and AddElement(/datum/element/immerse, "immerse", 215), the vis_contents sinking mask
        immerse_added = TRUE;
    }

    // tg: code/game/turfs/open/lava.dm:80
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        apply_lava_effects(arrived);
        return dot;
    }

    // tg: code/game/turfs/open/lava.dm:84
    public override async DmTask initialize_occupant(atom_movable occupant = null)
    {
        await base.initialize_occupant(occupant);
        apply_lava_effects(occupant);
    }

    /// Entering lava and having the floor turn into lava apply the same effects.
    // tg: code/game/turfs/open/lava.dm:89
    public virtual void apply_lava_effects(atom_movable occupant = null)
    {
        // DEVIATION(DV12): is_type_in_typecache(occupant, GLOB.immerse_ignored_movable) and AddElement(/datum/element/immerse, "immerse", 215), the vis_contents sinking mask
        if (!Dm.truthy(immerse_added))
            immerse_added = TRUE;
        if (burn_stuff(occupant))
            START_PROCESSING(SSobj, this);
    }

    // tg: code/game/turfs/open/lava.dm:96
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        // DEVIATION(DV12): . += emissive_appearance(mask_icon, mask_state, src), with the additive light overlay and the
        // multiplying light mask on the lighting plane (a lava tile glowing without a light source of its own): in 3D the
        // edge tiles' lights (refresh_light) light the lava sheet
        return await base.update_overlays(__extra);
    }

    /// Refreshes this lava turf's lighting
    // tg: code/game/turfs/open/lava.dm:113
    public virtual void refresh_light()
    {
        bool border_turf = false;
        DmList turfs_to_check = RANGE_TURFS(1, this);
        if (Dm.truthy(GET_LOWEST_STACK_OFFSET(z)))
        {
            var above = GET_TURF_ABOVE(this) as turf;
            if (above != null)
                Dm.append(ref turfs_to_check, RANGE_TURFS(1, above));
            var below = GET_TURF_BELOW(this) as turf;
            if (below != null)
                Dm.append(ref turfs_to_check, RANGE_TURFS(1, below));
        }

        foreach (turf around in Dm.each<turf>(turfs_to_check))
        {
            if (islava(around))
                continue;
            border_turf = true;
        }

        if (!border_turf)
        {
            Waiting.NoSleep(set_light(l_on: FALSE));
            return;
        }

        Waiting.NoSleep(set_light(l_on: TRUE));
    }

    // tg: code/game/turfs/open/lava.dm:135
    public override async DmTask<object> ChangeTurf(object path = null, DmList new_baseturfs = null, object flags = null)
    {
        var result = await base.ChangeTurf(path, new_baseturfs, flags) as turf;

        if (result != null && !islava(result))
        {
            // We have gone from a lava turf to a non lava turf, time to let them know
            DmList turfs_to_check = RANGE_TURFS(1, result);
            if (Dm.truthy(GET_LOWEST_STACK_OFFSET(z)))
            {
                var above = GET_TURF_ABOVE(result) as turf;
                if (above != null)
                    Dm.append(ref turfs_to_check, RANGE_TURFS(1, above));
                var below = GET_TURF_BELOW(result) as turf;
                if (below != null)
                    Dm.append(ref turfs_to_check, RANGE_TURFS(1, below));
            }
            foreach (turf_open_lava inform in Dm.each<turf_open_lava>(turfs_to_check))
                await inform.set_light(l_on: TRUE);
        }

        return result;
    }

    // tg: code/game/turfs/open/lava.dm:153
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.truthy(fish_source))
            ((datum_fish_source)GLOB.preset_fish_sources[fish_source]).spawn_reward_from_explosion(this, severity);
        return FALSE;
    }

    // tg: code/game/turfs/open/lava.dm:158
    public override async DmTask MakeSlippery(float? wet_setting = null, float? min_wet_time = null, float? wet_time_to_add = null, float? max_wet_time = null, float? permanent = null, float? should_display_overlay = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/game/turfs/open/lava.dm:165
    public override async DmTask<object> acid_act(object acidpwr = null, object acid_volume = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/game/turfs/open/lava.dm:161
    public override async DmTask<object> Melt()
    {
        await DmTask.CompletedTask;
        to_be_destroyed = FALSE;
        return this;
    }

    // tg: code/game/turfs/open/lava.dm:168
    public override void MakeDry(float? wet_setting = null, float? immediate = null, float? amount = null)
    {
    }

    // tg: code/game/turfs/open/lava.dm:171
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (isliving(gone) && !islava(gone.loc))
        {
            gone.RemoveElement(typeof(datum_element_perma_fire_overlay));
            REMOVE_TRAIT(gone, TRAIT_NO_EXTINGUISH, TURF_TRAIT);
        }
        return dot;
    }

    // tg: code/game/turfs/open/lava.dm:177
    public override async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        await DmTask.CompletedTask;
        if (burn_stuff(hitting_atom))
            START_PROCESSING(SSobj, this);
        return null;
    }

    // tg: code/game/turfs/open/lava.dm:181
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!burn_stuff(null, Dm.num(seconds_per_tick)))
        {
            checked_atoms = null;
            return PROCESS_KILL;
        }
        return null;
    }

    // tg: code/game/turfs/open/lava.dm:186
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_TURF) && Dm.eq(the_rcd.rcd_design_path, typeof(turf_open_floor_plating_rcd)))
            return Dm.list(Dm.kv("delay", 0), Dm.kv("cost", 3));
        return FALSE;
    }

    // tg: code/game/turfs/open/lava.dm:191
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.eq(rcd_data[RCD_DESIGN_MODE], RCD_TURF) && Dm.eq(rcd_data[RCD_DESIGN_PATH], typeof(turf_open_floor_plating_rcd)))
        {
            await place_on_top(typeof(turf_open_floor_plating), flags: CHANGETURF_INHERIT_AIR);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/turfs/open/lava.dm:197
    public override async DmTask<object> singularity_act()
    {
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/turfs/open/lava.dm:200
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/turfs/open/lava.dm:203
    public override object GetHeatCapacity()
    {
        return 700000;
    }

    // tg: code/game/turfs/open/lava.dm:206
    public override object GetTemperature()
    {
        return lava_temperature;
    }

    // tg: code/game/turfs/open/lava.dm:209
    public override void TakeTemperature(object temp = null)
    {
    }

    // tg: code/game/turfs/open/lava.dm:211
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(ITEM_INTERACT_ANY_BLOCKER, dot)))
            return dot;

        if (istype(tool, typeof(obj_item_stack_rods_lava)))
        {
            if (locate<obj_structure_lattice_catwalk_lava>(this) != null)
            {
                to_chat(user, span_warning("There is already a lattice here!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await ((obj_item_stack_rods_lava)tool).use(1)))
            {
                to_chat(user, span_warning("You need one rod to build a heatproof lattice."));
                return ITEM_INTERACT_BLOCKING;
            }

            to_chat(user, span_notice("You construct a lattice."));
            playsound(this, Dm.resource("sound/items/weapons/genhit.ogg"), 50, TRUE);
            Dm.@new<obj_structure_lattice_catwalk_lava>(locate(x, y, z));
            return ITEM_INTERACT_SUCCESS;
        }

        // Light a cigarette in the lava
        if (istype(tool, typeof(obj_item_cigarette)))
        {
            var ciggie = (obj_item_cigarette)tool;
            if (Dm.truthy(ciggie.lit))
            {
                to_chat(user, span_warning(Dm.text("\\The [] is already lit!", ciggie)));
                return ITEM_INTERACT_BLOCKING;
            }

            float clumsy_modifier = HAS_TRAIT(user, TRAIT_CLUMSY) ? 2 : 1;
            if (Dm.prob(25 * clumsy_modifier))
            {
                await ciggie.light__proc(span_warning(Dm.text("[] expertly dips \\the [] into [], along with the rest of [] arm. What a dumbass.", user, ciggie.name, this, user.p_their())));
                var burned_guy = user;
                await burned_guy.apply_damage(90, BURN, user.get_active_hand());
                return ITEM_INTERACT_SUCCESS;
            }

            await ciggie.light__proc(span_rose(Dm.text("[] expertly dips \\the [] into [], lighting it with the scorching heat of the planet. Witnessing such a feat is almost enough to make you cry.", user, ciggie.name, this)));
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/game/turfs/open/lava.dm:247
    public virtual bool is_safe()
    {
        return HAS_TRAIT(this, TRAIT_LAVA_STOPPED);
    }

    ///Proc that sets on fire something or everything on the turf that's not immune to lava. Returns TRUE to make the turf start processing.
    // tg: code/game/turfs/open/lava.dm:258
    public virtual bool burn_stuff(atom_movable to_burn = null, float seconds_per_tick = 1)
    {
        if (is_safe())
            return false;

        bool dot = false;
        LAZYSETLEN(ref checked_atoms, 0);
        object thing_to_check = this;
        if (to_burn != null)
            thing_to_check = Dm.list(to_burn);
        foreach (atom_movable burn_target in Dm.each<atom_movable>(thing_to_check).ToList())
        {
            switch (cache_burn_check(burn_target))
            {
                case LAVA_BE_IGNORING:
                    continue;
                case LAVA_BE_BURNING:
                    if (!do_burn(burn_target, seconds_per_tick))
                        continue;
                    break;
            }
            dot = true;
        }
        return dot;
    }

    /// Wrapper for can_burn_stuff that checks if something can be burnt and caches the result
    // tg: code/game/turfs/open/lava.dm:276
    public virtual int cache_burn_check(atom_movable burn_target = null)
    {
        object check_result = checked_atoms[burn_target.weak_reference];
        if (check_result == null)
        {
            check_result = can_burn_stuff(burn_target);
            checked_atoms[WEAKREF(burn_target)] = check_result;
        }
        return (int)Dm.num(check_result);
    }

    // tg: code/game/turfs/open/lava.dm:283
    public virtual int can_burn_stuff(atom_movable burn_target = null)
    {
        if (QDELETED(burn_target))
            return LAVA_BE_IGNORING;
        if (Dm.truthy(Dm.bitand(burn_target.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)) || Dm.truthy(burn_target.throwing) || !Dm.truthy(burn_target.has_gravity())) //you're flying over it.
            return LAVA_BE_IGNORING;
        if (isobj(burn_target))
        {
            var burn_obj = (obj)burn_target;
            if (HAS_TRAIT(this, TRAIT_ELEVATED_TURF) && !HAS_TRAIT(burn_obj, TRAIT_ELEVATING_OBJECT))
                return LAVA_BE_PROCESSING;
            if (Dm.truthy(Dm.bitand(burn_obj.resistance_flags, immunity_resistance_flags)))
                return LAVA_BE_PROCESSING;
            return LAVA_BE_BURNING;
        }

        if (!isliving(burn_target))
            return LAVA_BE_IGNORING;

        if (HAS_TRAIT(burn_target, immunity_trait))
            return LAVA_BE_PROCESSING;

        if (HAS_TRAIT(burn_target, TRAIT_MOB_ELEVATED))
            return LAVA_BE_PROCESSING;

        var burn_living = (mob_living)burn_target;
        atom_movable burn_buckled = burn_living.buckled;
        if (burn_buckled != null && cache_burn_check(burn_buckled) != LAVA_BE_BURNING)
            return LAVA_BE_PROCESSING;

        if (iscarbon(burn_living))
        {
            var burn_carbon = (mob_living_carbon)burn_living;
            var burn_suit = burn_carbon.get_item_by_slot(ITEM_SLOT_OCLOTHING) as obj_item_clothing;
            var burn_helmet = burn_carbon.get_item_by_slot(ITEM_SLOT_HEAD) as obj_item_clothing;
            if (Dm.truthy(Dm.bitand(burn_suit?.clothing_flags, LAVAPROTECT)) && Dm.truthy(Dm.bitand(burn_helmet?.clothing_flags, LAVAPROTECT)))
                return LAVA_BE_PROCESSING;
        }

        return LAVA_BE_BURNING;
    }

    // tg: code/game/turfs/open/lava.dm:323
    public virtual bool do_burn(atom_movable burn_target = null, float seconds_per_tick = 1)
    {
        if (QDELETED(burn_target))
            return false;

        if (isobj(burn_target))
        {
            var burn_obj = (obj)burn_target;
            if (Dm.truthy(Dm.bitand(burn_obj.resistance_flags, ON_FIRE))) // already on fire; skip it.
                return true;
            if (!Dm.truthy(Dm.bitand(burn_obj.resistance_flags, FLAMMABLE)))
                burn_obj.resistance_flags = Dm.bitor(burn_obj.resistance_flags, FLAMMABLE); //Even fireproof things burn up in lava
            if (Dm.truthy(Dm.bitand(burn_obj.resistance_flags, FIRE_PROOF)))
                burn_obj.resistance_flags = Dm.bitand(burn_obj.resistance_flags, Dm.bitnot(FIRE_PROOF));
            if (Dm.num(burn_obj.get_armor_rating(FIRE)) > 50) //obj with 100% fire armor still get slowly burned away.
                burn_obj.set_armor_rating(FIRE, 50);
            Waiting.NoSleep(burn_obj.fire_act(temperature_damage, 1000 * seconds_per_tick));
            if (istype(burn_obj, typeof(obj_structure_closet)))
            {
                foreach (atom_movable burn_content in Dm.each<atom_movable>(burn_target).ToList())
                    burn_stuff(burn_content);
            }
            return true;
        }

        if (isliving(burn_target))
        {
            var burn_living = (mob_living)burn_target;
            if (!HAS_TRAIT_FROM(burn_living, TRAIT_NO_EXTINGUISH, TURF_TRAIT))
            {
                Waiting.NoSleep(burn_living.AddElement(typeof(datum_element_perma_fire_overlay)));
                ADD_TRAIT(burn_living, TRAIT_NO_EXTINGUISH, TURF_TRAIT);
            }
            Waiting.NoSleep(burn_living.adjust_fire_stacks(lava_firestacks * seconds_per_tick));
            burn_living.ignite_mob();
            Waiting.NoSleep(burn_living.adjust_fire_loss(lava_damage * seconds_per_tick));
            return true;
        }

        return false;
    }

    /**
     * Called when a lava stopper (Catwalks/boulder platforms) is removed and it's contents need to be subjected to the lava underneath.
     */
    // tg: code/game/turfs/open/lava.dm:358
    [SignalHandler]
    public virtual object drop_contents_into_lava()
    {
        balloon_alert_to_hearers(Dm.text("[]!", pick("splash", "pshhhh", "hiss", "blorble")));
        playsound(this, Dm.resource("sound/items/match_strike.ogg"), 15, TRUE);
        foreach (atom_movable each_content in Dm.each<atom_movable>(contents).ToList())
            on_atom_inited(this, each_content);
        return TRUE;
    }

    // tg: code/game/turfs/open/lava.dm:366
    public override object can_cross_safely(atom_movable crossing = null)
    {
        return HAS_TRAIT(this, TRAIT_LAVA_STOPPED) || HAS_TRAIT(crossing, immunity_trait) || HAS_TRAIT(crossing, TRAIT_MOVE_FLYING) ? TRUE : FALSE;
    }
}

// tg: code/game/turfs/open/lava.dm:372
public partial class turf_open_lava_smooth : turf_open_lava
{
    // tg: code/game/turfs/open/lava.dm:391
    public override void bitmask_smooth()
    {
        base.bitmask_smooth();
        basalt_junction = ALL_SMOOTHING_JUNCTIONS;
        siderite_junction = ALL_SMOOTHING_JUNCTIONS;
        shale_junction = ALL_SMOOTHING_JUNCTIONS;
        // We need to convert basalt/siderite/shale groups into a readable format
        TgState state = TgState.Current;
        if (state.turf_open_lava_smooth_bitmask_smooth__basalt_group == null)
        {
            SET_SMOOTHING_GROUPS(Dm.add(SMOOTH_GROUP_FLOOR_BASALT, SMOOTH_GROUP_MINERAL_WALLS), ref state.turf_open_lava_smooth_bitmask_smooth__basalt_group);
            SET_SMOOTHING_GROUPS(Dm.add(SMOOTH_GROUP_FLOOR_SIDERITE, SMOOTH_GROUP_RED_ROCK_WALLS), ref state.turf_open_lava_smooth_bitmask_smooth__siderite_group);
            SET_SMOOTHING_GROUPS(Dm.add(SMOOTH_GROUP_FLOOR_SHALE, SMOOTH_GROUP_SHALE_WALLS), ref state.turf_open_lava_smooth_bitmask_smooth__shale_group);
        }
        var basalt_group = (DmList)state.turf_open_lava_smooth_bitmask_smooth__basalt_group;
        var siderite_group = (DmList)state.turf_open_lava_smooth_bitmask_smooth__siderite_group;
        var shale_group = (DmList)state.turf_open_lava_smooth_bitmask_smooth__shale_group;
        // After smoothing normally we can check our smoothed directions for possible basalt/siderite/shale tiles
        foreach (object check_dir in Dm.each(GLOB.alldirs))
        {
            float junction = Dm.num(Dm.bitor(dir_to_junction(check_dir), all_junctions_of_dir(check_dir)));
            if (!Dm.truthy(Dm.bitand(junction, Dm.num(smoothing_junction))))
                continue;
            var to_smooth = get_step(this, Dm.num(check_dir)) as turf;
            if (to_smooth == null || !Dm.truthy(to_smooth.smoothing_groups))
                continue;
            DmList groups = (DmList)to_smooth.smoothing_groups;
            foreach (object key in Dm.each(groups))
            {
                object group = groups[key];
                if (Dm.truthy(Dm.bitand(group, basalt_group[key])))
                    basalt_junction = Dm.bitand(basalt_junction, Dm.bitnot(junction));
                else if (Dm.truthy(Dm.bitand(group, siderite_group[key])))
                    siderite_junction = Dm.bitand(siderite_junction, Dm.bitnot(junction));
                else if (Dm.truthy(Dm.bitand(group, shale_group[key])))
                    shale_junction = Dm.bitand(shale_junction, Dm.bitnot(junction));
            }
        }
    }

    // tg: code/game/turfs/open/lava.dm:420
    public override void smooth_icon__proc()
    {
        base.smooth_icon__proc();
        mask_state = Dm.text("lava-[]", Dm.bitand(Dm.num(smoothing_junction), Dm.bitand(basalt_junction, Dm.bitand(siderite_junction, shale_junction))));
        update_appearance(Dm.bitnot(UPDATE_SMOOTHING));
    }

    // tg: code/game/turfs/open/lava.dm:425
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (basalt_junction != ALL_SMOOTHING_JUNCTIONS)
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/turf/floors/basalt_outline.dmi"), Dm.text("basalt_outline-[]", basalt_junction)));
        if (siderite_junction != ALL_SMOOTHING_JUNCTIONS)
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/turf/floors/siderite_outline.dmi"), Dm.text("siderite_outline-[]", siderite_junction)));
        if (shale_junction != ALL_SMOOTHING_JUNCTIONS)
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/turf/floors/shale_outline.dmi"), Dm.text("shale_outline-[]", shale_junction)));
        return dot;
    }

    /// Smooth lava needs to take after basalt in order to blend better.
    // tg: code/game/turfs/open/lava.dm:435
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        underlay_appearance.icon = Dm.resource("icons/turf/floors.dmi"); // /turf/open/misc/asteroid/basalt::icon
        underlay_appearance.icon_state = "basalt"; // /turf/open/misc/asteroid/basalt::icon_state
        return TRUE;
    }
}

// rules 10.6: the proc-level statics above, made on first use
public sealed partial class TgState
{
    internal object turf_open_lava_smooth_bitmask_smooth__basalt_group;
    internal object turf_open_lava_smooth_bitmask_smooth__siderite_group;
    internal object turf_open_lava_smooth_bitmask_smooth__shale_group;
}
