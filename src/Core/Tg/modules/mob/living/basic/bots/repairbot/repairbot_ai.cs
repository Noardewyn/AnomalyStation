// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:25
public partial class datum_bt_node_ai_behavior_repairbot_speech : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:27
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        if (Dm.num(controller.blackboard[BB_REPAIRBOT_SPEECH_COOLDOWN]) > world.time)
            return FALSE;
        DmList keys_to_look = TgState.Current.datum_bt_node_ai_behavior_repairbot_speech__keys_to_look ??= Dm.list(BB_CURRENT_TARGET, BB_DECONSTRUCT_TARGET);
        foreach (object key in Dm.each<object>(keys_to_look))
        {
            if (Dm.truthy(controller.blackboard_key_exists(key)))
                return await base.setup(controller);
        }
        return FALSE;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:38
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        datum_action_cooldown_bot_announcement announcement = controller.blackboard[BB_ANNOUNCE_ABILITY] as datum_action_cooldown_bot_announcement;
        // determine speech type: emagged -> emagged speech, otherwise normal
        DmList speech_to_pick_from;
        if (Dm.truthy(controller.blackboard_key_exists(BB_DECONSTRUCT_TARGET)))
            speech_to_pick_from = controller.blackboard[BB_REPAIRBOT_EMAGGED_SPEECH] as DmList;
        else
            speech_to_pick_from = controller.blackboard[BB_REPAIRBOT_NORMAL_SPEECH] as DmList;
        if (!Dm.truthy(length(speech_to_pick_from)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        announcement.announce(pick(speech_to_pick_from));
        controller.set_blackboard_key(BB_REPAIRBOT_SPEECH_COOLDOWN, world.time + 30 * SECONDS); // REPAIRBOT_SPEECH_TIMER
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

// tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:54
public partial class datum_bt_node_ai_behavior_bot_interact_tip_robot : datum_bt_node_ai_behavior_bot_interact
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:56
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        mob_living target = controller.blackboard[target_key] as mob_living;
        mob_living pawn = controller.pawn as mob_living;
        if (QDELETED(target) || pawn.pulling != target)
            return FALSE;
        return await base.setup(controller);
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:63
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        if (Dm.truthy(succeeded))
        {
            mob_living living_pawn = controller.pawn as mob_living;
            await living_pawn.stop_pulling();
        }
    }
}

// tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:69
public partial class datum_bt_node_ai_behavior_bot_search_valid_robot : datum_bt_node_ai_behavior_bot_search
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:72
    public override async DmTask<object> valid_target(datum_ai_controller_basic_controller_bot controller = null, atom my_target = null)
    {
        if (my_target is not mob_living_silicon_robot)
            return FALSE;
        return Dm.num(!HAS_TRAIT(my_target, TRAIT_MOB_TIPPED) && Dm.truthy(can_see(controller.pawn, my_target)));
    }
}

// tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:79
public partial class datum_bt_node_ai_behavior_bot_search_deconstructable : datum_bt_node_ai_behavior_bot_search
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:82
    public override async DmTask<object> valid_target(datum_ai_controller_basic_controller_bot controller = null, atom my_target = null)
    {
        return Dm.num(!Dm.truthy(Dm.bitand(my_target.resistance_flags, INDESTRUCTIBLE)) && !Dm.truthy(isgroundlessturf(my_target)));
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:85
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        looking_for = TgState.Current.datum_bt_node_ai_behavior_bot_search_deconstructable__things_to_deconstruct ??= typecacheof(Dm.list(typeof(obj_structure_window), typeof(turf_open_floor), typeof(turf_closed_wall))) as DmList;
        return base.perform(seconds_per_tick, controller);
    }
}

// tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:96
public partial class datum_bt_node_ai_behavior_bot_search_refillable_target : datum_bt_node_ai_behavior_bot_search
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:99
    public override async DmTask<object> valid_target(datum_ai_controller_basic_controller_bot controller = null, atom my_target = null)
    {
        DmList desired_types = TgState.Current.datum_bt_node_ai_behavior_bot_search_refillable_target__desired_types ??= Dm.list(typeof(obj_item_stack_sheet_iron), typeof(obj_item_stack_sheet_glass), typeof(obj_item_stack_tile));
        foreach (Type object_type in Dm.each<Type>(desired_types))
        {
            if (!Dm.istype(my_target, object_type))
                continue;
            obj_item_stack sheet_type = Dm.locate_type(object_type, controller.pawn) as obj_item_stack;
            if (sheet_type == null)
                return TRUE; //we dont have any of it!
            if (Dm.num(sheet_type.amount) < Dm.num(sheet_type.max_amount) && Dm.truthy(sheet_type.can_merge(my_target as obj_item_stack)))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:113
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        looking_for = TgState.Current.datum_bt_node_ai_behavior_bot_search_refillable_target__refillable_items ??= typecacheof(Dm.list(typeof(obj_item_stack_sheet_iron), typeof(obj_item_stack_sheet_glass), typeof(obj_item_stack_tile))) as DmList;
        return base.perform(seconds_per_tick, controller);
    }
}

// tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:123
public partial class datum_bt_node_ai_behavior_bot_search_valid_plateless_turf : datum_bt_node_ai_behavior_bot_search
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:127
    public virtual object get_turf_type_filter()
    {
        return typecacheof(Dm.list(typeof(turf_open_floor_plating)));
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:130
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        mob_living_basic_bot_repairbot bot_pawn = (mob_living_basic_bot_repairbot)controller.pawn;
        if (!Dm.truthy(Dm.bitand(bot_pawn.repairbot_flags, REPAIRBOT_REPLACE_TILES)))
            return FALSE;
        if (Dm.locate<obj_item_stack_tile>(bot_pawn) == null)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:138
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        looking_for = get_turf_type_filter() as DmList;
        return base.perform(seconds_per_tick, controller);
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:142
    public override async DmTask<object> valid_target(datum_ai_controller_basic_controller_bot controller = null, atom my_target = null)
    {
        DmList blacklist_objects = TgState.Current.datum_bt_node_ai_behavior_bot_search_valid_plateless_turf__blacklist_objects ??= typecacheof(Dm.list(typeof(obj_structure_window), typeof(obj_structure_grille))) as DmList;
        foreach (atom possible_blacklisted in Dm.each<atom>(my_target.contents))
        {
            if (Dm.truthy(is_type_in_typecache(possible_blacklisted, blacklist_objects)))
                return FALSE;
        }
        if (my_target is turf_open_floor_plating && !Dm.truthy(can_see(controller.pawn, my_target, 5)))
            return FALSE;
        DmList blacklist_areas = TgState.Current.datum_bt_node_ai_behavior_bot_search_valid_plateless_turf__blacklist_areas ??= typecacheof(Dm.list(typeof(area_space), typeof(area_station_maintenance))) as DmList;
        area turf_area = get_area(my_target) as area;
        return Dm.num(!Dm.truthy(is_type_in_typecache(turf_area, blacklist_areas)));
    }
}

/// Breach variant: searches /turf/open/space instead, requires REPAIRBOT_FIX_BREACHES flag.
// tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:159
public partial class datum_bt_node_ai_behavior_bot_search_valid_plateless_turf_breached : datum_bt_node_ai_behavior_bot_search_valid_plateless_turf
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:161
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        mob_living_basic_bot_repairbot bot_pawn = (mob_living_basic_bot_repairbot)controller.pawn;
        if (!Dm.truthy(Dm.bitand(bot_pawn.repairbot_flags, REPAIRBOT_FIX_BREACHES)))
            return FALSE;
        if (Dm.locate<obj_item_stack_tile_iron>(bot_pawn) == null)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:169
    public override object get_turf_type_filter()
    {
        return typecacheof(Dm.list(typeof(turf_open_space)));
    }
}

// tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:172
public partial class datum_bt_node_ai_behavior_bot_search_valid_girder : datum_bt_node_ai_behavior_bot_search
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:175
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        mob_living_basic_bot_repairbot bot_pawn = (mob_living_basic_bot_repairbot)controller.pawn;
        if (!Dm.truthy(Dm.bitand(bot_pawn.repairbot_flags, REPAIRBOT_FIX_GIRDERS)))
            return FALSE;
        obj_item_stack_sheet_iron my_iron = Dm.locate<obj_item_stack_sheet_iron>(bot_pawn);
        if (my_iron == null || Dm.num(my_iron.amount) < 2)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:184
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        looking_for = TgState.Current.datum_bt_node_ai_behavior_bot_search_valid_girder__searchable_girder ??= typecacheof(Dm.list(typeof(obj_structure_girder))) as DmList;
        return base.perform(seconds_per_tick, controller);
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:189
    public override async DmTask<object> valid_target(datum_ai_controller_basic_controller_bot controller = null, atom my_target = null)
    {
        if (my_target is not obj_structure_girder)
            return FALSE;
        return Dm.num(isfloorturf(my_target.loc));
    }
}

// tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:195
public partial class datum_bt_node_ai_behavior_targeted_mob_ability_build_girder : datum_bt_node_ai_behavior_targeted_mob_ability
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:198
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        mob_living_basic_bot_repairbot bot_pawn = (mob_living_basic_bot_repairbot)controller.pawn;
        if (!Dm.truthy(Dm.bitand(bot_pawn.repairbot_flags, REPAIRBOT_BUILD_GIRDERS)))
            return FALSE;
        obj_item_stack_rods my_rods = Dm.locate<obj_item_stack_rods>(bot_pawn);
        if (my_rods == null || Dm.num(my_rods.amount) < 2)
            return FALSE;
        datum_action_cooldown ability = controller.blackboard[ability_key] as datum_action_cooldown;
        if (ability == null || !Dm.truthy(await ability.IsAvailable()))
            return FALSE;
        return await base.setup(controller);
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:210
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}

/// Search for open turfs adjacent to space (valid girder build locations).
// tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:215
public partial class datum_bt_node_ai_behavior_bot_search_valid_wall_target : datum_bt_node_ai_behavior_bot_search
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:219
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        mob_living_basic_bot_repairbot bot_pawn = (mob_living_basic_bot_repairbot)controller.pawn;
        if (!Dm.truthy(Dm.bitand(bot_pawn.repairbot_flags, REPAIRBOT_BUILD_GIRDERS)))
            return FALSE;
        obj_item_stack_rods my_rods = Dm.locate<obj_item_stack_rods>(bot_pawn);
        if (my_rods == null || Dm.num(my_rods.amount) < 2)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:228
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        looking_for = TgState.Current.datum_bt_node_ai_behavior_bot_search_valid_wall_target__searchable_turfs ??= typecacheof(Dm.list(typeof(turf_open))) as DmList;
        return base.perform(seconds_per_tick, controller);
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:233
    public override async DmTask<object> valid_target(datum_ai_controller_basic_controller_bot controller = null, atom my_target = null)
    {
        if (my_target is not turf_open target_turf)
            return FALSE;
        if (get_area(target_turf) is area_space || Dm.truthy(isgroundlessturf(target_turf)) || Dm.truthy(await target_turf.is_blocked_turf()))
            return FALSE;
        DmList blacklist_objects = TgState.Current.datum_bt_node_ai_behavior_bot_search_valid_wall_target__blacklist_objects ??= typecacheof(Dm.list(typeof(obj_machinery_door), typeof(obj_structure_grille))) as DmList;
        foreach (atom content in Dm.each<atom>(target_turf))
        {
            if (Dm.truthy(is_type_in_typecache(content, blacklist_objects)))
                return FALSE;
        }
        DmList adjacent_turfs = get_adjacent_open_turfs(target_turf) as DmList;
        foreach (turf possible_spaced_turf in Dm.each_cast<turf>(adjacent_turfs))
        {
            if (isspaceturf(possible_spaced_turf) && get_area(possible_spaced_turf) is area_space)
                return TRUE;
        }
        return FALSE;
    }
}

// tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:251
public partial class datum_bt_node_ai_behavior_bot_search_valid_grille_target : datum_bt_node_ai_behavior_bot_search
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:254
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        mob_living_basic_bot_repairbot bot_pawn = (mob_living_basic_bot_repairbot)controller.pawn;
        if (!Dm.truthy(Dm.bitand(bot_pawn.repairbot_flags, REPAIRBOT_REPLACE_WINDOWS)))
            return FALSE;
        if (Dm.locate<obj_item_stack_sheet_glass>(bot_pawn) == null)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:262
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        looking_for = TgState.Current.datum_bt_node_ai_behavior_bot_search_valid_grille_target__searchable_grilles ??= typecacheof(Dm.list(typeof(obj_structure_grille))) as DmList;
        return base.perform(seconds_per_tick, controller);
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:267
    public override async DmTask<object> valid_target(datum_ai_controller_basic_controller_bot controller = null, atom my_target = null)
    {
        if (my_target is not obj_structure_grille)
            return FALSE;
        if (Dm.locate<obj_structure_window>(get_turf(my_target)) != null)
            return FALSE;
        return Dm.num(get_area(my_target) is not area_space);
    }
}

// tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:274
public partial class datum_bt_node_ai_behavior_bot_search_valid_window_fix : datum_bt_node_ai_behavior_bot_search
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:277
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        looking_for = TgState.Current.datum_bt_node_ai_behavior_bot_search_valid_window_fix__searchable_objects ??= typecacheof(Dm.list(typeof(obj_structure_window))) as DmList;
        return base.perform(seconds_per_tick, controller);
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_ai.dm:282
    public override async DmTask<object> valid_target(datum_ai_controller_basic_controller_bot controller = null, atom my_target = null)
    {
        if (my_target is not obj_structure_window target_window)
            return FALSE;
        return Dm.num(Dm.num(target_window.get_integrity()) < Dm.num(target_window.max_integrity) || !Dm.truthy(target_window.anchored));
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList datum_bt_node_ai_behavior_repairbot_speech__keys_to_look;
    internal DmList datum_bt_node_ai_behavior_bot_search_deconstructable__things_to_deconstruct;
    internal DmList datum_bt_node_ai_behavior_bot_search_refillable_target__desired_types;
    internal DmList datum_bt_node_ai_behavior_bot_search_refillable_target__refillable_items;
    internal DmList datum_bt_node_ai_behavior_bot_search_valid_plateless_turf__blacklist_objects;
    internal DmList datum_bt_node_ai_behavior_bot_search_valid_plateless_turf__blacklist_areas;
    internal DmList datum_bt_node_ai_behavior_bot_search_valid_girder__searchable_girder;
    internal DmList datum_bt_node_ai_behavior_bot_search_valid_wall_target__searchable_turfs;
    internal DmList datum_bt_node_ai_behavior_bot_search_valid_wall_target__blacklist_objects;
    internal DmList datum_bt_node_ai_behavior_bot_search_valid_grille_target__searchable_grilles;
    internal DmList datum_bt_node_ai_behavior_bot_search_valid_window_fix__searchable_objects;
}
