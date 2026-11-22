// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/machinery/oven.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the oven, the range and the oven tray. The var blocks are generated (D111).

// tg: code/modules/food_and_drinks/machinery/oven.dm:11
public partial class obj_machinery_oven : obj_machinery
{
    // tg: code/modules/food_and_drinks/machinery/oven.dm:1-9  file-local #defines (#undef at :315-323)
    private const float OVEN_SMOKE_STATE_NONE = 0;
    private const float OVEN_SMOKE_STATE_GOOD = 1;
    private const float OVEN_SMOKE_STATE_NEUTRAL = 2;
    private const float OVEN_SMOKE_STATE_BAD = 3;
    private const float OVEN_LID_Y_OFFSET = -15;
    private const float OVEN_TRAY_Y_OFFSET = -16;
    private const float OVEN_TRAY_X_OFFSET = -2;

    // tg: code/modules/food_and_drinks/machinery/oven.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        oven_loop = Dm.@new<datum_looping_sound_oven>(this);
        if (Dm.truthy(mapload))
            add_tray_to_oven(Dm.@new<obj_item_plate_oven_tray>(this)); //Start with a tray
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:43
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref oven_loop);
        if (particle_type != null)
            remove_shared_particles(particle_type);
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:49
    public override object IsContainedAtomAccessible(atom contained = null, atom_movable user = null)
    {
        return (base.IsContainedAtomAccessible(contained, user) is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.num(contained is obj_item_plate_oven_tray));
    }

    /// Used to determine if the oven appears active and cooking, or offline.
    // tg: code/modules/food_and_drinks/machinery/oven.dm:53
    public virtual object appears_active()
    {
        return Dm.num(!Dm.truthy(open) && Dm.truthy(Dm.length(used_tray?.contents)) && !Dm.truthy(Dm.bitand(machine_stat, BROKEN | NOPOWER)));
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:56
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(appears_active()))
            icon_state = Dm.text("[]_on", base_icon_state);
        else
            icon_state = Dm.text("[]_off", base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:63
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(open))
        {
            mutable_appearance door_overlay = mutable_appearance(icon, Dm.text("[]_lid_open", base_icon_state)) as mutable_appearance;
            door_overlay.pixel_z = OVEN_LID_Y_OFFSET;
            Dm.append(ref dot, door_overlay);
        }
        else
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_lid_closed", base_icon_state)));
            // DEVIATION(DV12): if(length(used_tray?.contents)) . += emissive_appearance(icon, "[base_icon_state]_light_mask", src, alpha = src.alpha),
            // the glow on the emissive plane, is not created
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:74
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(appears_active()))
        {
            set_smoke_state(OVEN_SMOKE_STATE_NONE);
            update_baking_audio();
            update_appearance(UPDATE_ICON);
            end_processing();
            return null;
        }

        ///We take the worst smoke state, so if something is burning we always know.
        float worst_cooked_food_state = 0;
        foreach (obj_item baked_item in Dm.each<obj_item>(used_tray.contents))
        {
            object signal_result = !Dm.truthy(baked_item._listen_lookup?[COMSIG_ITEM_OVEN_PROCESS]) ? NONE : baked_item._SendSignal(COMSIG_ITEM_OVEN_PROCESS, Dm.list(baked_item, this, seconds_per_tick));

            if (Dm.truthy(Dm.bitand(signal_result, COMPONENT_HANDLED_BAKING))) //This means something responded to us baking!
            {
                if (Dm.truthy(Dm.bitand(signal_result, COMPONENT_BAKING_GOOD_RESULT)) && worst_cooked_food_state < OVEN_SMOKE_STATE_GOOD)
                    worst_cooked_food_state = OVEN_SMOKE_STATE_GOOD;
                else if (Dm.truthy(Dm.bitand(signal_result, COMPONENT_BAKING_BAD_RESULT)) && worst_cooked_food_state < OVEN_SMOKE_STATE_NEUTRAL)
                    worst_cooked_food_state = OVEN_SMOKE_STATE_NEUTRAL;
                continue;
            }

            worst_cooked_food_state = OVEN_SMOKE_STATE_BAD;
            Waiting.NoSleep(baked_item.fire_act(1000)); //Hot hot hot!

            if (SPT_PROB(10, seconds_per_tick))
            {
                DmList asomnia_hadders = Dm.list();
                foreach (mob smeller in Dm.each<mob>(get_hearers_in_view(DEFAULT_MESSAGE_RANGE, this)))
                {
                    if (HAS_TRAIT(smeller, TRAIT_ANOSMIA))
                        Dm.append(ref asomnia_hadders, smeller);
                }
                visible_message(span_danger(I18n.T("You smell a burnt smell coming from []!", this)), ignored_mobs: asomnia_hadders);
            }
        }
        set_smoke_state(worst_cooked_food_state);
        update_appearance();
        Waiting.NoSleep(use_energy(active_power_usage));
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:108
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(open) && used_tray != null && tool.atom_storage != null)
            return await used_tray.item_interaction(user, tool, modifiers);

        if (!Dm.truthy(open) || used_tray != null || tool is not obj_item_plate_oven_tray)
            return NONE;

        if (!Dm.truthy(await user.transferItemToLoc(tool, this, silent: FALSE)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You put [] in [].", tool, this)));
        add_tray_to_oven((obj_item_plate)tool, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:122
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (Dm.truthy(open) && used_tray != null && tool.atom_storage != null)
            return await used_tray.item_interaction_secondary(user, tool, modifiers);
        return NONE;
    }

    ///Adds a tray to the oven, making sure the shit can get baked.
    // tg: code/modules/food_and_drinks/machinery/oven.dm:128
    public virtual void add_tray_to_oven(obj_item_plate oven_tray = null, mob baker = null)
    {
        used_tray = oven_tray as obj_item_plate_oven_tray;
        playsound(this, SFX_TRAY_INSERT, 50, TRUE);

        if (!Dm.truthy(open))
            oven_tray.vis_flags = Dm.bitor(oven_tray.vis_flags, VIS_HIDE);
        Dm.append(vis_contents, oven_tray);
        oven_tray.vis_flags = Dm.bitor(oven_tray.vis_flags, VIS_INHERIT_PLANE);
        oven_tray.pixel_y = OVEN_TRAY_Y_OFFSET;
        oven_tray.pixel_x = OVEN_TRAY_X_OFFSET;

        RegisterSignal(used_tray, COMSIG_MOVABLE_MOVED, nameof(on_tray_moved));
        update_baking_audio();
        update_appearance();
    }

    ///Called when the tray is moved out of the oven in some way
    // tg: code/modules/food_and_drinks/machinery/oven.dm:144
    [SignalHandler]
    public virtual void on_tray_moved(obj_item oven_tray = null, atom OldLoc = null, object Dir = null, object Forced = null)
    {
        tray_removed_from_oven(oven_tray);
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:149
    [SignalHandler]
    public virtual void tray_removed_from_oven(obj_item oven_tray = null)
    {
        oven_tray.vis_flags = Dm.bitand(oven_tray.vis_flags, Dm.bitnot(VIS_INHERIT_PLANE));
        Dm.remove(vis_contents, oven_tray);
        used_tray = null;
        UnregisterSignal(oven_tray, COMSIG_MOVABLE_MOVED);
        update_baking_audio();
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:157
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        open = Dm.num(!Dm.truthy(open));
        if (Dm.truthy(open))
        {
            playsound(this, Dm.resource("sound/machines/oven/oven_open.ogg"), 75, TRUE);
            set_smoke_state(OVEN_SMOKE_STATE_NONE);
            to_chat(user, span_notice(I18n.T("You open [].", this)));
            end_processing();
            if (used_tray != null)
                used_tray.vis_flags = Dm.bitand(used_tray.vis_flags, Dm.bitnot(VIS_HIDE));
        }
        else
        {
            playsound(this, Dm.resource("sound/machines/oven/oven_close.ogg"), 75, TRUE);
            to_chat(user, span_notice(I18n.T("You close [].", this)));
            if (used_tray != null)
            {
                begin_processing();
                used_tray.vis_flags = Dm.bitor(used_tray.vis_flags, VIS_HIDE);

                // yeah yeah i figure you don't need connect loc for just baking trays
                foreach (obj_item baked_item in Dm.each<obj_item>(used_tray.contents))
                {
                    if (Dm.truthy(baked_item._listen_lookup?[COMSIG_ITEM_OVEN_PLACED_IN]))
                        baked_item._SendSignal(COMSIG_ITEM_OVEN_PLACED_IN, Dm.list(baked_item, this, user));
                }
            }
        }

        update_appearance();
        update_baking_audio();
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:182
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_robot(user, modifiers);
        open = Dm.num(!Dm.truthy(open));
        if (Dm.truthy(open))
        {
            playsound(this, Dm.resource("sound/machines/oven/oven_open.ogg"), 75, TRUE);
            set_smoke_state(OVEN_SMOKE_STATE_NONE);
            to_chat(user, span_notice(I18n.T("You open [].", this)));
            end_processing();
            if (used_tray != null)
                used_tray.vis_flags = Dm.bitand(used_tray.vis_flags, Dm.bitnot(VIS_HIDE));
        }
        else
        {
            playsound(this, Dm.resource("sound/machines/oven/oven_close.ogg"), 75, TRUE);
            to_chat(user, span_notice(I18n.T("You close [].", this)));
            if (used_tray != null)
            {
                begin_processing();
                used_tray.vis_flags = Dm.bitor(used_tray.vis_flags, VIS_HIDE);

                // yeah yeah i figure you don't need connect loc for just baking trays
                foreach (obj_item baked_item in Dm.each<obj_item>(used_tray.contents))
                {
                    if (Dm.truthy(baked_item._listen_lookup?[COMSIG_ITEM_OVEN_PLACED_IN]))
                        baked_item._SendSignal(COMSIG_ITEM_OVEN_PLACED_IN, Dm.list(baked_item, this, user));
                }
            }
        }

        update_appearance();
        update_baking_audio();
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:207
    public virtual void update_baking_audio()
    {
        if (oven_loop == null)
            return;
        if (Dm.truthy(appears_active()))
            oven_loop.start();
        else
            oven_loop.stop();
    }

    ///Updates the smoke state to something else, setting particles if relevant
    // tg: code/modules/food_and_drinks/machinery/oven.dm:216
    public virtual void set_smoke_state(object new_state = null)
    {
        if (Dm.eq(new_state, smoke_state))
            return;

        smoke_state = Dm.num(new_state);
        if (particle_type != null)
        {
            remove_shared_particles(particle_type);
            particle_type = null;
        }

        float sw__1 = smoke_state;
        if (sw__1 == OVEN_SMOKE_STATE_BAD)
            particle_type = typeof(particles_smoke);
        else if (sw__1 == OVEN_SMOKE_STATE_NEUTRAL)
            particle_type = typeof(particles_smoke_steam);
        else if (sw__1 == OVEN_SMOKE_STATE_GOOD)
            particle_type = typeof(particles_smoke_steam_mild);

        if (particle_type != null)
            add_shared_particles(particle_type);
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:236
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:239
    public override object can_crowbar_deconstruct()
    {
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:242
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await default_unfasten_wrench(user, tool, time: 2 * SECONDS);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/food_and_drinks/machinery/oven.dm:246
public partial class obj_machinery_oven_range : obj_machinery_oven
{
    // tg: code/modules/food_and_drinks/machinery/oven.dm:255
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        obj_item_reagent_containers_cup_soup_pot mapload_container = null;
        if (Dm.truthy(mapload))
            mapload_container = Dm.@new<obj_item_reagent_containers_cup_soup_pot>(loc);

        Waiting.NoSleep(AddComponent(typeof(datum_component_stove), Dm.named("container_x", -6), Dm.named("container_y", 14), Dm.named("spawn_container", mapload_container)));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/machinery/oven.dm:263
public partial class obj_item_plate_oven_tray : obj_item_plate
{
    // tg: code/modules/food_and_drinks/machinery/oven.dm:275
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item item = null, DmList modifiers = null)
    {
        if (item.atom_storage == null)
            return NONE;

        foreach (obj tray_item in Dm.each<obj>(this))
            ((datum_storage)item.atom_storage).attempt_insert(tray_item as obj_item, user, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/oven.dm:283
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item item = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, item, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (item.atom_storage == null)
            return NONE;

        if (Dm.length(contents) >= max_items)
        {
            balloon_alert(user, I18n.T("it's full!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (item is not obj_item_storage_bag_tray)
        {
            // Non-tray dumping requires a do_after
            to_chat(user, span_notice(I18n.T("You start dumping out the contents of [] into []...", item, this)));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: item)))
                return ITEM_INTERACT_BLOCKING;
        }

        float loaded = 0;
        foreach (obj tray_item in Dm.each<obj>(item))
        {
            if (!IS_EDIBLE(tray_item))
                continue;
            if (Dm.length(contents) >= max_items)
                break;
            if (Dm.truthy(((datum_storage)item.atom_storage).attempt_remove(tray_item as obj_item, this)))
            {
                loaded++;
                AddToPlate(tray_item as obj_item); // tg passes the user too, which AddToPlate does not take
            }
        }
        if (Dm.truthy(loaded))
        {
            to_chat(user, span_notice(I18n.T("You insert [] item\\s into [].", loaded, this)));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }
}
