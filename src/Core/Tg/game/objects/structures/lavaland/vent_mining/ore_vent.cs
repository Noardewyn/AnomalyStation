// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/lavaland/vent_mining/ore_vent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2c (D304): the whole file. The var block is generated (D111). The wave defence (the NODE drone, the clearing of the
// vent's surroundings and the spawner's waves of fauna) waits for the fauna, L5: a confirmed excavation stops after tg's alert.
// The tapped vent's emissive glow stays under DV12, the GPS signal waits for the GPS (L2d), and the trimming of the vent's top
// when a mob stands behind it is not done (DV261).

// tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:8
public partial class obj_structure_ore_vent : obj_structure
{
    private const float ARTIFACT_ROLL_CHANCE = 7;
    private const float MINERAL_TYPE_OPTIONS_RANDOM = 4;
    private const float MINERALS_PER_BOULDER = 3;
    private const float MAX_MINERAL_PICK_ATTEMPTS = 10;

    /// What items can be used to scan a vent?
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:60
    public static DmList scanning_equipment => TgState.Current.obj_structure_ore_vent__scanning_equipment ??= Dm.list(
        typeof(obj_item_t_scanner_adv_mining_scanner),
        typeof(obj_item_mining_scanner));

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:86
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(mapload))
            generate_description();
        register_context();
        if (!Dm.truthy(unique_vent))
            SSore_generation?.possible_vents.Add(this); // a host-built room without the Master has no SSore_generation
        boulder_icon_state = (string)pick(Dm.list(
            "boulder",
            "rock",
            "stone"));
        if (Dm.truthy(tapped))
        {
            SSore_generation?.processed_vents.Add(this); // a host-built room without the Master has no SSore_generation
            Waiting.NoSleep(become_tapped());
        }

        RegisterSignal(this, COMSIG_SPAWNER_SPAWNED_DEFAULT, nameof(anti_cheese));
        RegisterSignal(this, COMSIG_SPAWNER_SPAWNED, nameof(log_mob_spawned));
        Waiting.NoSleep(AddElement(typeof(datum_element_give_turf_traits), string_list(Dm.list(TRAIT_NO_TERRAFORM))));
        set_turf_tracking();
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:107
    public override object Destroy(float? force = null)
    {
        SSore_generation?.possible_vents.Remove(this); // a host-built room without the Master has no SSore_generation
        Waiting.NoSleep(reset_drone(success: FALSE));
        if (Dm.truthy(tapped))
            SSore_generation?.processed_vents.Remove(this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:114
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(is_type_in_list(tool, scanning_equipment)))
            return NONE;

        if (Dm.truthy(tapped))
        {
            balloon_alert_to_viewers(I18n.T("vent tapped!"));
            return ITEM_INTERACT_BLOCKING;
        }

        await scan_and_confirm(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:125
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!HAS_TRAIT(user, TRAIT_BOULDER_BREAKER))
            return dot;
        if (!Dm.truthy(discovered))
        {
            to_chat(user, span_notice(I18n.T("You can't quite find the weakpoint of []... Perhaps it needs to be scanned first?", this)));
            return dot;
        }
        to_chat(user, span_notice(I18n.T("You start striking [] with your golem's fist, attempting to dredge up a boulder...", this)));
        for (int i = 1; i <= 3; i++)
        {
            if (Dm.truthy(await do_after(user, boulder_size * 1 * SECONDS, this)))
            {
                await ((mob_living)user).apply_damage(20, STAMINA);
                playsound(this, Dm.resource("sound/items/weapons/genhit.ogg"), 50, TRUE);
            }
        }
        await produce_boulder(TRUE);
        visible_message(span_notice(I18n.T("You've successfully produced a boulder! Boy are your arms tired.")));
        return dot;
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:142
    public override async DmTask<object> attack_basic_mob(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_basic_mob(user, modifiers);
        if (HAS_TRAIT(user, TRAIT_BOULDER_BREAKER))
        {
            await produce_boulder(TRUE);
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:148
    public override async DmTask<object> is_buckle_possible(mob_living target = null, float? force = null, float? check_loc = null)
    {
        object dot = await base.is_buckle_possible(target, force, check_loc);
        if (Dm.truthy(tapped))
            return FALSE;
        if (istype(target, typeof(mob_living_basic_node_drone)))
            return TRUE;
        return dot;
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:155
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(discovered))
        {
            switch (boulder_size)
            {
                case BOULDER_SIZE_SMALL:
                    Dm.append(ref dot, span_notice(I18n.T("This vent produces [] boulders containing []", span_bold(I18n.T("small")), ore_string)));
                    break;
                case BOULDER_SIZE_MEDIUM:
                    Dm.append(ref dot, span_notice(I18n.T("This vent produces [] boulders containing []", span_bold(I18n.T("medium")), ore_string)));
                    break;
                case BOULDER_SIZE_LARGE:
                    Dm.append(ref dot, span_notice(I18n.T("This vent produces [] boulders containing []", span_bold(I18n.T("large")), ore_string)));
                    break;
            }
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("This vent can be scanned with a [].", span_bold(I18n.T("Mining Scanner")))));
        if (Dm.truthy(artifact_chance))
            Dm.append(ref dot, span_notice(I18n.T("This vent has a low chance to produce an [] These may contain rare minerals or strange artifacts.", span_bold(I18n.T("artifact boulder.")))));
        return dot;
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:170
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (Dm.truthy(is_type_in_list(held_item, scanning_equipment)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Scan vent";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return null;
    }

    /**
     * This proc is called when the ore vent is initialized, in order to determine what minerals boulders it spawns can contain.
     * The materials available are determined by SSore_generation.ore_vent_minerals, which is a list of all minerals that can be contained in ore vents for a given cave generation.
     * Once a material is picked from the weighted list, it's removed from ore_vent_minerals, so that it can't be picked again.
     * The above applies only when spawning in at mapload, otherwise we pick randomly from ore_vent_minerals_lavaland.
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:187
    public virtual void generate_mineral_breakdown(float? new_minerals = null, float? map_loading = null)
    {
        new_minerals ??= MINERAL_TYPE_OPTIONS_RANDOM;
        map_loading ??= FALSE;
        if (new_minerals < 1)
        {
            throw Dm.CRASH("generate_mineral_breakdown called with new_minerals < 1.", "code/game/objects/structures/lavaland/vent_mining/ore_vent.dm", 189);
        }

        //should have enough minerals for the vent during round start
        DmList available_minerals = SSore_generation?.ore_vent_minerals; // a host-built room without the Master has no SSore_generation
        if (Dm.truthy(map_loading) && available_minerals.Count < new_minerals)
        {
            throw Dm.CRASH("No minerals left to pick from! We may have spawned too many ore vents in init, or the map config in seedRuins may not have enough resources for the mineral budget.", "code/game/objects/structures/lavaland/vent_mining/ore_vent.dm", 194);
        }

        DmList picked_minerals = Dm.list();
        for (int _ = 1; _ <= new_minerals; _++)
        {
            object mineral;

            //pick an unique mineral but try only MAX_MINERAL_PICK_ATTEMPTS times before giving up else we could be stuck here forever
            float attempts = 0;
            do
            {
                mineral = Dm.truthy(length(mineral_breakdown)) ? pick_weight(mineral_breakdown) : null;
                if (Dm.truthy(map_loading))
                {
                    if (!Dm.truthy(mineral) || !Dm.truthy(available_minerals.Find(mineral)))
                        mineral = pick(available_minerals);
                }
                else
                    mineral = Dm.truthy(mineral) ? mineral : pick_weight(GLOB.ore_vent_minerals_lavaland);
                attempts += 1;
            }
            while (attempts < MAX_MINERAL_PICK_ATTEMPTS && Dm.truthy(picked_minerals.Find(mineral)));

            //register the picked mineral, removing it from the round start available minerals if nessassary
            if (Dm.truthy(map_loading))
                available_minerals.Remove(mineral);
            Dm.combine(picked_minerals, mineral);
        }

        //assign random weights to picked minerals
        mineral_breakdown.Cut();
        foreach (object mineral in Dm.each(picked_minerals))
            mineral_breakdown[mineral] = Dm.rand(1, (float)new_minerals);
    }

    /**
     * Returns the quantity of mineral sheets in each ore vent's boulder contents roll.
     * First roll can produce the most ore, with subsequent rolls scaling lower logarithmically.
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:230
    public virtual object ore_quantity_function(object ore_floor = null, float? ore_per_size = null)
    {
        ore_per_size ??= HALF_SHEET_MATERIAL_AMOUNT;
        float floor_value = Dm.num(ore_floor);
        return ore_per_size * max(round(boulder_size * MathF.Pow(log(Dm.rand(1 + floor_value, 4 + floor_value)), -1)), 1);
    }

    /**
     * This confirms that the user wants to start the wave defense event, and that they can start it.
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:236
    public virtual async DmTask<object> pre_wave_defense(mob user = null, float? spawn_drone = null, float? mech_scan = null)
    {
        spawn_drone ??= TRUE;
        mech_scan ??= FALSE;
        if (!Dm.eq(await tgui_alert(user, excavation_warning, I18n.T("Begin defending ore vent?"), Dm.list("Yes", "No")), "Yes"))
            return FALSE;
        if (!Dm.truthy(await can_interact(user)) && !Dm.truthy(mech_scan))
            return FALSE;
        // Shake(duration = 3 SECONDS); the NODE drone (new /mob/living/basic/node_drone, arrive, the conclusion's signals); the ash
        // particles; the clearing of the walls, rubble and lava platforms within 5 tiles: the wave defence comes with the fauna.
        Dm.Deferred("code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:245", "pre_wave_defense: the NODE drone and the clearing before the wave defence (the fauna, Lavaland L5)");
        return FALSE;
    }

    /**
     * Starts the wave defense event, which will spawn a number of lavaland mobs based on the size of the ore vent.
     * Called after the vent has been tapped by a scanning device.
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:286
    public virtual async DmTask start_wave_defense()
    {
        // AddComponent(/datum/component/spawner, defending_mobs, ...), COOLDOWN_START(src, wave_cooldown, INFINITY), the
        // COMSIG_VENT_WAVE_CONCLUDED signal and the icon: the spawner's waves of fauna.
        Dm.Deferred("code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:290", "start_wave_defense: the spawner's waves of fauna (Lavaland L5)");
        await DmTask.CompletedTask;
    }

    /**
     * Called when the wave defense event ends, after a variable amount of time in start_wave_defense.
     * If the node drone is still alive, the ore vent is tapped and the ore vent will begin generating boulders.
     * If the node drone is dead, the ore vent is not tapped and the wave defense can be reattempted.
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:317
    [SignalHandler]
    public virtual void handle_wave_conclusion(datum source = null)
    {
        Dm.Deferred("code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:325", "handle_wave_conclusion: remove_shared_particles(/particles/smoke/ash) (particles are not ported)");
        UnregisterSignal(this, COMSIG_VENT_WAVE_CONCLUDED);
        //happens in COMSIG_QDELETING
        if (QDELETED(node) || node.stat == DEAD)
        {
            Waiting.NoSleep(initiate_wave_loss(I18n.T("\\the [] creaks and groans as the mining attempt fails, and the vent closes back up.", this)));
            return;
        }

        //happens in COMSIG_MOVABLE_MOVED
        if (get_turf(node) != get_turf(this))
        {
            Waiting.NoSleep(initiate_wave_loss(I18n.T("The [] detaches from the [], and the vent closes back up!", node, this)));
            return; //Start over!
        }

        Waiting.NoSleep(initiate_wave_win());
    }

    /**
     * Handles reseting our ore vent to its original state so we can start over
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:337
    public virtual async DmTask initiate_wave_loss(object loss_message = null)
    {
        visible_message(span_danger(loss_message as string));
        playsound(this, Dm.resource("sound/effects/rock/rock_break.ogg"), 50);
        update_appearance(UPDATE_ICON_STATE);
        await reset_drone(success: FALSE);
        wave_cooldown = 0; // COOLDOWN_RESET (__DEFINES/cooldowns.dm), inline
    }

    /**
     * Handles winning the event, gives everyone a payout and start boulder production
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:347
    public virtual async DmTask initiate_wave_win(float? forced = null)
    {
        forced ??= FALSE;
        await become_tapped(); //The Node Drone has survived the wave defense, and the ore vent is tapped.
        SSore_generation.processed_vents.Add(this);
        if (!Dm.truthy(forced))
        {
            log_game(Dm.text("Ore vent [] was tapped", key_name_and_tag(this)));
            SSblackbox.record_feedback("tally", "ore_vent_completed", 1, GetType());
            balloon_alert_to_viewers(I18n.T("vent tapped!"));
        }
        playsound(node, Dm.resource("sound/effects/node_victory.ogg"), 40, TRUE);
        update_appearance(UPDATE_ICON_STATE);
        add_tapped_visual();
        qdel(GetComponent(typeof(datum_component_gps)));
        await reset_drone(success: TRUE);

        if (Dm.truthy(forced))
            return;

        foreach (mob_living miner in Dm.each<mob_living>(range(7, this))) //Give the miners who are near the vent points and xp.
        {
            if (Dm.truthy(miner._listen_lookup?[COMSIG_LIVING_ON_VENT_WIN]))
                miner._SendSignal(COMSIG_LIVING_ON_VENT_WIN, Dm.list(miner, this));
            obj_item_card_id user_id_card = miner.get_idcard(TRUE) as obj_item_card_id;
            if (miner.stat <= SOFT_CRIT)
                miner.mind?.adjust_experience(typeof(datum_skill_mining), MINING_SKILL_BOULDER_SIZE_XP * boulder_size);
            if (user_id_card == null)
                continue;
            float point_reward_val = (MINER_POINT_MULTIPLIER * (boulder_size + 2)) - MINER_POINT_MULTIPLIER; // We remove the base value of discovering the vent
            if (user_id_card.registered_account != null)
            {
                user_id_card.registered_account.mining_points += point_reward_val;
                Waiting.Detach(user_id_card.registered_account.bank_card_talk(I18n.T("You have been awarded [] mining points for your efforts.", point_reward_val)));
            }
        }
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:375
    public virtual void add_tapped_visual()
    {
        if (vent_visual != null)
        {
            Dm.combine(vis_contents, vent_visual);
            return;
        }

        vent_visual = Dm.@new<obj_effect_abstract>(this);
        vent_visual.icon = Dm.resource("icons/obj/mining_zones/terrain.dmi");
        vent_visual.icon_state = "well";
        vent_visual.layer = ABOVE_MOB_LAYER;
        vent_visual.vis_flags = Dm.bitor(VIS_INHERIT_PLANE, VIS_INHERIT_ID);
        // DEVIATION(DV12): vent_visual.overlays += emissive_appearance('icons/obj/mining_zones/terrain.dmi', "well_e", src, alpha = 192)
        Dm.append(vis_contents, vent_visual);
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:388
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(tapped))
            icon_state = Dm.text("[]_active", base_icon_state);
        else
            icon_state = (string)base_icon_state;
        if (Dm.truthy(trimmed))
            icon_state = Dm.text("[]_trimmed", icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:397
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        set_turf_tracking();
        return dot;
    }

    /// Update the turf which we track for mobs entering/exiting
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:402
    public virtual void set_turf_tracking()
    {
        if (tracked_turf != null)
            UnregisterSignal(tracked_turf, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_EXITED));

        tracked_turf = locate(x, y + 1, z) as turf;
        if (tracked_turf != null)
        {
            RegisterSignal(tracked_turf, COMSIG_ATOM_ENTERED, nameof(on_entered));
            RegisterSignal(tracked_turf, COMSIG_ATOM_EXITED, nameof(on_exited));
        }
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:411
    [SignalHandler]
    public virtual void on_entered(atom source = null, atom_movable entered = null)
    {
        if (!isliving(entered))
            return;

        // DEVIATION(DV261): if (!trimmed) trim_vent(): the vent's top is not trimmed for a mob behind it
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:420
    [SignalHandler]
    public virtual void on_exited(atom source = null, atom_movable exited = null, object direction = null)
    {
        if (!isliving(exited) || Dm.locate<mob_living>(tracked_turf) != null)
            return;

        if (Dm.truthy(trimmed))
            untrim_vent();
    }

    /// Turn the top part of the vent transparent and clickthrough
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:430
    public virtual void trim_vent()
    {
        trimmed = TRUE;
        update_appearance(UPDATE_ICON_STATE);

        if (vent_visual != null)
        {
            vent_visual.vis_flags = Dm.bitand(vent_visual.vis_flags, Dm.bitnot(VIS_INHERIT_ID));
            vent_visual.mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
        }

        if (top_bit == null)
        {
            top_bit = Dm.@new<obj_effect_abstract>(this);
            top_bit.icon = Dm.resource("icons/obj/mining_zones/terrain.dmi");
            // Because its transparent we can't just underlay the full thing
            top_bit.icon_state = Dm.text("[][]_trimmed_top", base_icon_state, Dm.truthy(tapped) ? "_active" : "");
            top_bit.layer = ABOVE_MOB_LAYER;
            top_bit.vis_flags = Dm.bitor(Dm.bitor(VIS_INHERIT_PLANE, VIS_INHERIT_LAYER), VIS_UNDERLAY);
            top_bit.mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
        }

        Dm.combine(vis_contents, top_bit);
        animate(this, 0.5f * SECONDS, Dm.named("alpha", 128));
    }

    /// Revert the transparency of the top half of the vent
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:451
    public virtual void untrim_vent()
    {
        trimmed = FALSE;
        update_appearance(UPDATE_ICON_STATE);

        if (vent_visual != null)
        {
            vent_visual.mouse_opacity = MOUSE_OPACITY_ICON;
            vent_visual.vis_flags = Dm.bitor(vent_visual.vis_flags, VIS_INHERIT_ID);
        }

        if (top_bit != null)
            Dm.remove(vis_contents, top_bit);

        animate(this, 0.5f * SECONDS, Dm.named("alpha", 255));
    }

    /**
     * Sends our node back to base and cleans up after the reference
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:467
    public virtual async DmTask reset_drone(object success = null)
    {
        if (!QDELETED(node))
        {
            // node.pre_escape(success = success): the NODE drone comes with the wave defence
            Dm.Deferred("code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:481", "reset_drone: the NODE drone's escape (the wave defence, Lavaland L5)");
            UnregisterSignal(node, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED));
        }
        node = null;
        await DmTask.CompletedTask;
    }

    /**
     * Called when the ore vent is tapped by a scanning device.
     * Gives a readout of the ores available in the vent that gets added to the description,
     * then asks the user if they want to start wave defense if it's already been discovered.
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:480
    public virtual async DmTask scan_and_confirm(mob_living user = null, float? mech_scan = null)
    {
        mech_scan ??= FALSE;
        if (Dm.truthy(tapped))
        {
            balloon_alert_to_viewers(I18n.T("vent tapped!"));
            return;
        }
        if (!(wave_cooldown <= world.time) || node != null) //We're already defending the vent, so don't scan it again.
        {
            balloon_alert_to_viewers(I18n.T("protect the node drone!"));
            return;
        }
        if (!Dm.truthy(discovered))
        {
            if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, this)))
            {
                balloon_alert(user, I18n.T("already scanning!"));
                return;
            }
            balloon_alert(user, I18n.T("scanning..."));
            playsound(this, Dm.resource("sound/items/timer.ogg"), 30, TRUE);
            if (!Dm.truthy(await do_after(user, 4 * SECONDS, this)))
                return;

            discovered = TRUE;
            balloon_alert(user, I18n.T("vent scanned!"));
            generate_description(user);
            // AddComponent(/datum/component/gps, name): the vent's GPS signal
            Dm.Deferred("code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:513", "scan_and_confirm: the vent's GPS signal (the GPS, Lavaland L2d)");
            obj_item_card_id user_id_card = user.get_idcard(TRUE) as obj_item_card_id;
            if (user_id_card == null)
                return;
            if (user_id_card.registered_account != null)
            {
                user_id_card.registered_account.mining_points += MINER_POINT_MULTIPLIER;
                Waiting.Detach(user_id_card.registered_account.bank_card_talk(I18n.T("You've been awarded [] mining points for discovery of an ore vent.", MINER_POINT_MULTIPLIER)));
            }
            return;
        }

        if (!Dm.truthy(await pre_wave_defense(user, spawn_drone_on_tap, mech_scan)))
            return;
        await start_wave_defense();
    }

    /**
     * Generates a description of the ore vent to ore_string, based on the minerals contained within it.
     * Ore_string is passed to examine().
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:516
    public virtual void generate_description(mob user = null)
    {
        ore_string = "";
        DmList mineral_names = Dm.list();
        foreach (object resource in Dm.each(mineral_breakdown))
            mineral_names.Add(Dm.initial((System.Type)resource, "name"));

        ore_string = Dm.text("[].", english_list(mineral_names));
        if (user != null)
            ore_string += I18n.T("\nThis vent was first discovered by [].", user);
    }

    /**
     * Adds floating temp_visual overlays to the vent, showcasing what minerals are contained within it.
     * If undiscovered, adds a single overlay with the icon_state "unknown".
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:529
    public virtual void add_mineral_overlays()
    {
        if (Dm.truthy(mineral_breakdown.Count) && !Dm.truthy(discovered))
        {
            atom_movable_flick_visual visual = flick_overlay_view((mutable_appearance)mutable_appearance(Dm.resource("icons/effects/vent_overlays.dmi"), "unknown"), 4.5f * SECONDS) as atom_movable_flick_visual;
            animate(visual, Dm.named("alpha", 0), Dm.named("time", 4.5f * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_IN)));
            return;
        }

        foreach (object selected_mat in Dm.each(mineral_breakdown))
        {
            // tg reads selected_mat.name on the typepath: the material's initial name
            atom_movable_flick_visual visual = flick_overlay_view((mutable_appearance)mutable_appearance(Dm.resource("icons/effects/vent_overlays.dmi"), (string)Dm.initial((System.Type)selected_mat, "name")), 4.5f * SECONDS) as atom_movable_flick_visual;
            animate(visual, Dm.named("alpha", 0), Dm.named("time", 4.5f * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_IN)));
        }

        if (Dm.truthy(artifact_chance))
        {
            atom_movable_flick_visual rare = flick_overlay_view((mutable_appearance)mutable_appearance(Dm.resource("icons/effects/vent_overlays.dmi"), "rare_ore"), 4.5f * SECONDS) as atom_movable_flick_visual;
            animate(rare, Dm.named("alpha", 0), Dm.named("time", 4.5f * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_IN)));
        }
    }

    /**
     * Here is where we handle producing a new boulder, based on the qualities of this ore vent.
     * Returns the boulder produced.
     * @params apply_cooldown Should we apply a cooldown to producing boulders? Default's false, used by manual boulder production (goldgrubs, golems, etc).
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:549
    public virtual async DmTask<object> produce_boulder(float? apply_cooldown = null)
    {
        apply_cooldown ??= FALSE;
        //cooldown applies only for manual processing by hand
        if (Dm.truthy(apply_cooldown) && !(manual_vent_cooldown <= world.time))
            return null;

        //produce the boulder
        obj_item_boulder new_rock;
        if (Dm.prob(artifact_chance))
        {
            object picked_artifact = pick(typesof(typeof(obj_item_boulder_artifact)));
            new_rock = Dm.@new((System.Type)picked_artifact, loc) as obj_item_boulder;
        }
        else
            new_rock = Dm.@new<obj_item_boulder>(loc);
        Shake(duration: 1.5f * SECONDS);

        //decorate the boulder with materials
        DmList mats_list = new_rock.custom_materials?.Copy() ?? Dm.list();
        for (int iteration = 1; iteration <= MINERALS_PER_BOULDER; iteration++)
        {
            object material = pick_weight(mineral_breakdown);
            mats_list[material] = Dm.num(mats_list[material]) + Dm.num(ore_quantity_function(iteration, HALF_SHEET_MATERIAL_AMOUNT * (Dm.truthy(boosted) ? BOULDER_MAT_MULT_BOOSTED : 1)));
        }
        await new_rock.set_custom_materials(mats_list);

        //set size & durability
        new_rock.boulder_size = boulder_size;
        new_rock.durability = Dm.rand(2, boulder_size); //randomize durability a bit for some flavor.
        new_rock.boulder_string = boulder_icon_state;

        switch (boulder_size)
        {
            case BOULDER_SIZE_SMALL:
                new_rock.platform_lifespan = PLATFORM_LIFE_SMALL;
                break;
            case BOULDER_SIZE_MEDIUM:
                new_rock.platform_lifespan = PLATFORM_LIFE_MEDIUM;
                break;
            case BOULDER_SIZE_LARGE:
                new_rock.platform_lifespan = PLATFORM_LIFE_LARGE;
                break;
        }

        new_rock.update_appearance(UPDATE_ICON_STATE);

        //start the cooldown & return the boulder
        if (Dm.truthy(apply_cooldown))
            manual_vent_cooldown = world.time + 10 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm), inline
        return new_rock;
    }

    /**
     * When the ore vent cannot spawn a mob due to being blocked from all sides, we cause some MILD, MILD explosions.
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:596
    [SignalHandler]
    public virtual void anti_cheese()
    {
        Waiting.NoSleep(explosion(this, heavy_impact_range: 1, light_impact_range: 3, flame_range: 0, flash_range: 0, adminlog: FALSE));
    }

    /**
     * Handle logging for mobs spawned
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:602
    [SignalHandler]
    public virtual void log_mob_spawned(datum source = null, mob_living created = null)
    {
        log_game(Dm.text("Ore vent [] spawned the following mob: []", key_name_and_tag(this), key_name_and_tag(created)));
        SSblackbox.record_feedback("tally", "ore_vent_mobs_spawned", 1, created.GetType());
        RegisterSignal(created, COMSIG_LIVING_DEATH, nameof(log_mob_killed));
    }

    /**
     * Handle logging for mobs killed
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:611
    [SignalHandler]
    public virtual void log_mob_killed(datum source = null, mob_living killed = null)
    {
        log_game(Dm.text("Vent-spawned mob [] was killed", key_name_and_tag(killed)));
        SSblackbox.record_feedback("tally", "ore_vent_mobs_killed", 1, killed.GetType());
    }

    /**
     * The part of initialization and the ore_generation subsystem's setup that assigns the size of an ore_vent and it's boulders.
     * @param random: Used for random sized boulders, and the standard (pre 9-6-2025) behavior.
     * @param force_size: An override we use for map_generation, when we know what size boulder we need from this ore vent already.
     */
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:621
    public virtual void vent_size_setup(float? random = null, object force_size = null, object map_loading = null)
    {
        random ??= FALSE;
        object string_boulder_size = null;

        if (Dm.truthy(force_size))
            string_boulder_size = force_size;

        else if (Dm.truthy(random))
            string_boulder_size = pick_weight(ore_vent_options);

        name = Dm.text("[] ore vent", string_boulder_size);
        switch (string_boulder_size as string)
        {
            case LARGE_VENT_TYPE:
                boulder_size = BOULDER_SIZE_LARGE;
                wave_timer = WAVE_DURATION_LARGE;
                if (Dm.truthy(map_loading))
                    GLOB.ore_vent_sizes["large"] = Dm.num(GLOB.ore_vent_sizes["large"]) + 1;
                break;
            case MEDIUM_VENT_TYPE:
                boulder_size = BOULDER_SIZE_MEDIUM;
                wave_timer = WAVE_DURATION_MEDIUM;
                if (Dm.truthy(map_loading))
                    GLOB.ore_vent_sizes["medium"] = Dm.num(GLOB.ore_vent_sizes["medium"]) + 1;
                break;
            case SMALL_VENT_TYPE:
                boulder_size = BOULDER_SIZE_SMALL;
                wave_timer = WAVE_DURATION_SMALL;
                if (Dm.truthy(map_loading))
                    GLOB.ore_vent_sizes["small"] = Dm.num(GLOB.ore_vent_sizes["small"]) + 1;
                break;
            default:
                boulder_size = BOULDER_SIZE_SMALL; //Might as well set a default value
                wave_timer = WAVE_DURATION_SMALL;
                name = (string)Dm.initial(this, "name");
                break;
        }
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:652
    public virtual async DmTask become_tapped()
    {
        tapped = TRUE;
        RegisterSignal(this, COMSIG_CANDELA_NODE_NETWORK_CHANGED, nameof(on_network_changed));
        await AddComponent(typeof(datum_component_candela_node), Dm.@new<datum_mining_beacon_network>(), Dm.named("connection_pixel_x", base_pixel_w), Dm.named("connection_pixel_y", Dm.num(base_pixel_z) + 36), Dm.named("power_flags", network_power_flags), Dm.named("fulton_point", TRUE));
        update_appearance(UPDATE_ICON_STATE);
        add_tapped_visual();
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:659
    [SignalHandler]
    public virtual void on_network_changed(datum source = null, datum_mining_beacon_network old_network = null, datum_mining_beacon_network new_network = null)
    {
        if (old_network != null)
            UnregisterSignal(old_network, COMSIG_CANDELA_NETWORK_POWER_CHANGED);
        on_power_changed(old_state: old_network?.powered, new_state: new_network?.powered);
        if (new_network != null)
            RegisterSignal(new_network, COMSIG_CANDELA_NETWORK_POWER_CHANGED, nameof(on_power_changed));
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:668
    [SignalHandler]
    public virtual void on_power_changed(datum source = null, object old_state = null, object new_state = null)
    {
        boosted = Dm.truthy(Dm.bitand(Dm.num(new_state), CANDELA_NETWORK_BOOSTED)) ? TRUE : FALSE;
    }
}

//comes with the station, and is already tapped.
// tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:674
public partial class obj_structure_ore_vent_starter_resources : obj_structure_ore_vent
{
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:687
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        generate_description();
        return dot;
    }
}

// tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:691
public partial class obj_structure_ore_vent_random : obj_structure_ore_vent
{
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:694
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(unique_vent) && !Dm.truthy(mapload))
        {
            generate_mineral_breakdown(map_loading: Dm.truthy(mapload) ? TRUE : FALSE); //Default to random mineral breakdowns, unless this is a unique vent or we're still setting up default vent distribution.
            generate_description();
        }
        if (!Dm.truthy(mapload))
            vent_size_setup(random: TRUE); // We only do this here specific to random distribution ore vents, and within mapload we handle this manually within SSore_generation.
        return dot;
    }

    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:702
    public override void LateInitialize(params object[] __extra)
    {
        base.LateInitialize(__extra);
        if (!Dm.truthy(length(mineral_breakdown)))
            throw Dm.CRASH("We generated an ore vent, and after init, it had no mineral breakdown!", "code/game/objects/structures/lavaland/vent_mining/ore_vent.dm", 726);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/lavaland/vent_mining/ore_vent.dm:60  var/static/list/scanning_equipment
    internal DmList obj_structure_ore_vent__scanning_equipment;
}
