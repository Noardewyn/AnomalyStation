// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/modules_supply.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file but one leaf. The supply MOD modules: the internal GPS, the hydraulic clamp and the loader's (carries
// crates), the drill (digs rock by hand or by walking into it, faster in the sphere), the ore bag (picks up ore walked over), the
// loader's hydraulic arms (a charged leap) and hydraulic magnet (pulls and throws lockers), the disposal selector (a disposal tag for
// the wearer), the explorer's ash accretion (armour from ash walked through) and sphere transform (a rolling ball firing mining
// missiles), the mining micromissile and the Candela beacon spool. Left with a row (not-translated.md 222): the micromissile's impact
// particles (:778, DM particles, which the client does not draw). The var blocks are generated (D111).

///Internal GPS - Extends a GPS you can use.
// tg: code/modules/mod/modules/modules_supply.dm:4
public partial class obj_item_mod_module_gps : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_supply.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_gps_item), "MOD0", Dm.named("state", GLOB.deep_inventory_state), Dm.named("overlay_state", FALSE)));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:22
    public override async DmTask<object> on_use(mob activator = null)
    {
        await attack_self(mod.wearer); // todo: refactor to make compatable with pAIs.  Maybe ui_interact(activator)
        return null;
    }
}

///Hydraulic Clamp - Lets you pick up and drop crates.
// tg: code/modules/mod/modules/modules_supply.dm:26
public partial class obj_item_mod_module_clamp : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_supply.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        accepted_items = typecacheof(Dm.list(
            typeof(obj_structure_closet_crate),
            typeof(obj_item_delivery_big)
        )) as DmList;
        return dot;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:59
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await mod.wearer.Adjacent(target)))
            return dot;
        if (Dm.truthy(is_type_in_typecache(target, accepted_items)))
        {
            atom_movable picked_crate = target as atom_movable;
            if (!Dm.truthy(check_crate_pickup(picked_crate)))
                return dot;
            playsound(this, Dm.resource("sound/vehicles/mecha/hydraulic.ogg"), 25, TRUE);
            if (!Dm.truthy(await do_after(mod.wearer, load_time, target)))
            {
                balloon_alert(mod.wearer, I18n.T("interrupted!"));
                return dot;
            }
            if (!Dm.truthy(check_crate_pickup(picked_crate)))
                return dot;
            Dm.append(ref stored_crates, picked_crate);
            await picked_crate.forceMove(this);
            balloon_alert(mod.wearer, I18n.T("picked up crate"));
            await drain_power(use_energy_cost);
            return dot;
        }

        if (Dm.truthy(length(stored_crates)))
        {
            turf target_turf = get_turf(target);
            if (Dm.truthy(await target_turf.is_blocked_turf()))
                return dot;
            playsound(this, Dm.resource("sound/vehicles/mecha/hydraulic.ogg"), 25, TRUE);
            if (!Dm.truthy(await do_after(mod.wearer, load_time, target)))
            {
                balloon_alert(mod.wearer, I18n.T("interrupted!"));
                return dot;
            }
            if (Dm.truthy(await target_turf.is_blocked_turf()))
                return dot;
            atom_movable dropped_crate = pop(stored_crates) as atom_movable;
            if (dropped_crate == null)
                return dot;
            await dropped_crate.forceMove(target_turf);
            balloon_alert(mod.wearer, I18n.T("dropped []", dropped_crate));
            await drain_power(use_energy_cost);
            return dot;
        }

        balloon_alert(mod.wearer, I18n.T("invalid target!"));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:101
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        deleting ??= FALSE;
        if (Dm.truthy(deleting))
            return;
        foreach (atom_movable crate in Dm.each_cast<atom_movable>(stored_crates))
        {
            await crate.forceMove(drop_location() as atom);
            Dm.remove(ref stored_crates, crate);
        }
    }

    // tg: code/modules/mod/modules/modules_supply.dm:108
    public virtual object check_crate_pickup(atom_movable target = null)
    {
        if (length(stored_crates) >= max_crates)
        {
            balloon_alert(mod.wearer, I18n.T("too many crates!"));
            return FALSE;
        }
        foreach (mob_living mob in Dm.each<mob_living>(target.get_all_contents()))
        {
            if (mob.mob_size <= max_mob_size)
                continue;
            balloon_alert(mod.wearer, I18n.T("crate too heavy!"));
            return FALSE;
        }
        return TRUE;
    }
}

///Drill - Lets you dig through rock and basalt.
// tg: code/modules/mod/modules/modules_supply.dm:132
public partial class obj_item_mod_module_drill : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_supply.dm:156
    public override async DmTask on_install()
    {
        await base.on_install();
        RegisterSignal(mod, COMSIG_MOD_MODULE_ACTIVATED, nameof(on_module_activated));
        RegisterSignal(mod, COMSIG_MOD_MODULE_DEACTIVATED, nameof(on_module_deactivated));
    }

    // tg: code/modules/mod/modules/modules_supply.dm:161
    public override async DmTask on_uninstall(float? deleting = null)
    {
        await base.on_uninstall(deleting);
        UnregisterSignal(mod, Dm.list(COMSIG_MOD_MODULE_ACTIVATED, COMSIG_MOD_MODULE_DEACTIVATED));
        toolspeed = Dm.initial(this, (obj_item_mod_module_drill p) => p.toolspeed);
        use_energy_cost = Dm.initial(this, (obj_item_mod_module_drill p) => p.use_energy_cost);
        exp_multiplier = Dm.initial(this, (obj_item_mod_module_drill p) => p.exp_multiplier);
        ballin = FALSE;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:169
    public override async DmTask on_activation(mob activator = null)
    {
        if (Dm.truthy(ballin))
            return;
        tool_behaviour = TOOL_MINING;
        // DEVIATION(DV354): walking into rock with the drill out does not dig it, as with a pick in the hand: the drill digs on its click
        // (on_select_use); the sphere's excavation registers the bump itself (on_module_activated)
    }

    // tg: code/modules/mod/modules/modules_supply.dm:175
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        if (Dm.truthy(ballin))
            return;
        tool_behaviour = null; // NONE: tool_behaviour holds a tool's name (text), and NONE (0) reads as none
    }

    // tg: code/modules/mod/modules/modules_supply.dm:181
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot) || !Dm.truthy(await mod.wearer.Adjacent(target)))
            return dot;

        if (!ismineralturf(target) && !isasteroidturf(target))
            return dot;

        if (Dm.truthy(await drain_power(use_energy_cost)))
            await target.base_item_interaction(mod.wearer, this);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:192
    [SignalHandler]
    public virtual void bump_mine(mob_living_carbon_human bumper = null, atom bumped_into = null, object proximity = null)
    {
        if (world.time == last_bumpmine_tick)
            return;

        if (!ismineralturf(bumped_into) || !Dm.truthy(Waiting.NoSleep(drain_power(use_energy_cost))))
            return;

        turf_closed_mineral_gibtonite giberal_turf = bumped_into as turf_closed_mineral_gibtonite;
        if (!istype(giberal_turf, typeof(turf_closed_mineral_gibtonite)) || !Dm.eq(giberal_turf.stage, GIBTONITE_UNSTRUCK))
        {
            last_bumpmine_tick = world.time;
            turf_closed_mineral rock = bumped_into as turf_closed_mineral;
            Waiting.Detach(mine_rock(rock, bumper)); // INVOKE_ASYNC(src, PROC_REF(mine_rock), rock, bumper)
            return;
        }

        if (!(gibtonite_warning_cd <= world.time)) // !COOLDOWN_FINISHED(src, gibtonite_warning_cd)
            return;

        gibtonite_warning_cd = world.time + 3 * SECONDS; // COOLDOWN_START(src, gibtonite_warning_cd, 3 SECONDS)
        playsound(bumper, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
        // [icon2html(src, bumper)]: the drill's picture in the chat line; the 3D client's chat is text (chat icons, M10+)
        to_chat(bumper, span_warning(I18n.T("Unstable gibtonite ore deposit detected!")));
    }

    // tg: code/modules/mod/modules/modules_supply.dm:215
    public virtual async DmTask mine_rock(turf_closed_mineral rock = null, mob_living_carbon_human bumper = null)
    {
        // Even faster if it has ore!
        float has_ore = Dm.num(rock.mineral_type != null);
        if (Dm.truthy(has_ore))
            toolspeed /= 2;
        await rock.manual_mine(bumper, this, exp_multiplier);
        if (Dm.truthy(has_ore))
            toolspeed *= 2;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:224
    [SignalHandler]
    public virtual void on_module_activated(datum source = null, obj_item_mod_module module = null)
    {
        if (!istype(module, typeof(obj_item_mod_module_sphere_transform)))
            return;
        // In sphere mode we get faster mining and halved power drain
        toolspeed = 0.075f;
        use_energy_cost *= 0.5f;
        exp_multiplier *= 0.2f;
        if (!Dm.truthy(active))
            Waiting.NoSleep(on_activation());
        ballin = TRUE;
        RegisterSignal(mod.wearer, COMSIG_MOVABLE_BUMP, nameof(bump_mine)); // DEVIATION(DV354): only the sphere digs on the bump (tg: on_activation)
    }

    // tg: code/modules/mod/modules/modules_supply.dm:236
    [SignalHandler]
    public virtual void on_module_deactivated(datum source = null, obj_item_mod_module module = null)
    {
        if (!istype(module, typeof(obj_item_mod_module_sphere_transform)))
            return;
        toolspeed = Dm.initial(this, (obj_item_mod_module_drill p) => p.toolspeed);
        use_energy_cost *= 2;
        exp_multiplier /= 0.2f;
        ballin = FALSE;
        UnregisterSignal(mod.wearer, COMSIG_MOVABLE_BUMP); // DEVIATION(DV354): the sphere's bump dig ends with it (tg: on_deactivation)
        if (!Dm.truthy(active))
            Waiting.NoSleep(on_deactivation());
    }
}

/// Ore Bag - Lets you pick up ores and drop them from the suit.
// tg: code/modules/mod/modules/modules_supply.dm:248
public partial class obj_item_mod_module_orebag : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_supply.dm:265
    public override void on_equip()
    {
        RegisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED, nameof(on_wearer_moved));
        if (mod.wearer.loc != null)
        {
            RegisterSignal(mod.wearer.loc, COMSIG_ATOM_ENTERED, nameof(on_obj_entered));
            RegisterSignal(mod.wearer.loc, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(on_atom_initialized_on));
        }
    }

    // tg: code/modules/mod/modules/modules_supply.dm:271
    public override void on_unequip()
    {
        UnregisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED);
        if (mod.wearer.loc != null)
            UnregisterSignal(mod.wearer.loc, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON));
    }

    // tg: code/modules/mod/modules/modules_supply.dm:276
    [SignalHandler]
    public virtual void on_wearer_moved(atom_movable source = null, atom old_loc = null, object dir = null, object forced = null)
    {
        if (old_loc != null)
            UnregisterSignal(old_loc, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON));

        if (mod.wearer.loc != null)
        {
            RegisterSignal(mod.wearer.loc, COMSIG_ATOM_ENTERED, nameof(on_obj_entered));
            RegisterSignal(mod.wearer.loc, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(on_atom_initialized_on));
        }

        float ore_found = FALSE;
        foreach (obj_item_stack_ore ore in Dm.each<obj_item_stack_ore>(get_turf(mod.wearer)))
        {
            ore_found = TRUE;
            Waiting.Detach(move_ore(ore)); // INVOKE_ASYNC(src, PROC_REF(move_ore), ore)
        }

        if (Dm.truthy(ore_found))
            playsound(mod.wearer, SFX_RUSTLE, 50, TRUE);
    }

    // tg: code/modules/mod/modules/modules_supply.dm:293
    public virtual async DmTask move_ore(obj_item_stack ore = null)
    {
        foreach (obj_item_stack_ore stored_ore in Dm.each_cast<obj_item_stack_ore>(this))
        {
            if (!Dm.truthy(ore.can_merge(stored_ore)))
                continue;
            await ore.merge(stored_ore);
            if (QDELETED(ore))
                return;
        }
        await ore.forceMove(this);
    }

    // tg: code/modules/mod/modules/modules_supply.dm:302
    public override async DmTask<object> on_use(mob activator = null)
    {
        dropping_ores = TRUE;
        foreach (obj_item ore in Dm.each_cast<obj_item>(this))
            await ore.forceMove(mod.drop_location() as atom);
        dropping_ores = FALSE;
        await drain_power(use_energy_cost);
        return null;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:309
    [SignalHandler]
    public virtual void on_obj_entered(atom new_loc = null, atom_movable arrived = null, atom old_loc = null)
    {
        if (istype(arrived, typeof(obj_item_stack_ore)) && !Dm.truthy(dropping_ores) && old_loc != mod.wearer)
        {
            Waiting.Detach(move_ore(arrived as obj_item_stack)); // INVOKE_ASYNC(src, PROC_REF(move_ore), arrived)
            playsound(mod.wearer, SFX_RUSTLE, 50, TRUE);
        }
    }

    // tg: code/modules/mod/modules/modules_supply.dm:315
    [SignalHandler]
    public virtual void on_atom_initialized_on(atom loc = null, atom new_atom = null)
    {
        // tg: is_type_in_list(new_atom, /obj/item/stack/ore): a typepath where a list is expected (a tg quirk), which DM's `in` reads
        // as false for every atom, so the ore initialized on the wearer's tile is never taken
        if (Dm.truthy(is_type_in_list(new_atom, null)))
        {
            Waiting.Detach(move_ore(new_atom as obj_item_stack)); // INVOKE_ASYNC(src, PROC_REF(move_ore), new_atom)
            playsound(mod.wearer, SFX_RUSTLE, 50, TRUE);
        }
    }
}

// tg: code/modules/mod/modules/modules_supply.dm:321
public partial class obj_item_mod_module_hydraulic : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_supply.dm:339
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        atom game_renderer = mod.wearer.hud_used.get_plane_master(MUTATE_PLANE(RENDER_PLANE_GAME, mod.wearer)) as atom;
        matrix render_matrix = matrix(game_renderer.transform);
        render_matrix.Scale(1.25f, 1.25f);
        animate(game_renderer, Dm.named("time", launch_time), Dm.named("transform", render_matrix));
        float current_time = world.time;
        mod.wearer.visible_message(span_warning(I18n.T("[] starts whirring!", mod.wearer)),
            blind_message: span_hear(I18n.T("You hear a whirring sound.")));
        playsound(this, Dm.resource("sound/items/modsuit/loader_charge.ogg"), 75, TRUE);
        lightning = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "electricity3", layer: LOW_MOB_LAYER) as mutable_appearance;
        mod.wearer.add_overlay(lightning);
        balloon_alert(mod.wearer, I18n.T("you start charging..."));
        float power = launch_time;
        if (!Dm.truthy(await do_after(mod.wearer, launch_time, target: mod)))
        {
            power = world.time - current_time;
            animate(game_renderer);
        }
        await drain_power(use_energy_cost);
        Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(this));
        playsound(this, Dm.resource("sound/items/modsuit/loader_launch.ogg"), 75, TRUE);
        game_renderer.transform = (game_renderer.transform as matrix).Scale(0.8f, 0.8f);
        mod.wearer.cut_overlay(lightning);
        float angle = Dm.num(get_angle(mod.wearer, target));
        mod.wearer.transform = (mod.wearer.transform as matrix).Turn(angle);
        await mod.wearer.throw_at(get_ranged_target_turf_direct(mod.wearer, target, power) as atom,
            callback: Dm.@new<datum_callback>(this, nameof(on_throw_end), mod.wearer, -angle));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:369
    public virtual void on_throw_end(mob user = null, object angle = null)
    {
        if (user == null)
            return;
        user.transform = (user.transform as matrix).Turn(Dm.num(angle));
    }
}

// tg: code/modules/mod/modules/modules_supply.dm:374
public partial class obj_item_mod_module_disposal_connector : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_supply.dm:385
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // tg stores pick(GLOB.TAGGERLOCATIONS), a destination's name, in this number var (a tg bug): no sorting junction's number
        // matches a name and the configuration's list shows no choice for it, so the port keeps NONE, which routes and shows the
        // same until the wearer picks a destination. The pick is kept for the random sequence.
        pick(GLOB.TAGGERLOCATIONS);
        disposal_tag = NONE;
        return dot;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:389
    public override async DmTask on_part_activation()
    {
        RegisterSignal(mod.wearer, COMSIG_MOVABLE_DISPOSING, nameof(disposal_handling));
    }

    // tg: code/modules/mod/modules/modules_supply.dm:392
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        UnregisterSignal(mod.wearer, COMSIG_MOVABLE_DISPOSING);
    }

    // tg: code/modules/mod/modules/modules_supply.dm:395
    public override object get_configuration(mob user = null)
    {
        DmList dot = base.get_configuration(user) as DmList;
        dot["disposal_tag"] = add_ui_configuration(I18n.T("Disposal Tag"), "list", disposal_tag >= 1 ? GLOB.TAGGERLOCATIONS[disposal_tag] : null, GLOB.TAGGERLOCATIONS); // tg's text index reads null
        return dot;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:399
    public override async DmTask configure_edit(object key = null, object value = null)
    {
        switch (key)
        {
            case "disposal_tag":
                for (float tag = 1; tag <= length(GLOB.TAGGERLOCATIONS); tag++)
                {
                    if (Dm.eq(GLOB.TAGGERLOCATIONS[tag], value))
                    {
                        disposal_tag = tag;
                        break;
                    }
                }
                break;
        }
    }

    // tg: code/modules/mod/modules/modules_supply.dm:407
    [SignalHandler]
    public virtual void disposal_handling(datum disposal_source = null, obj_structure_disposalholder disposal_holder = null, obj_machinery_disposal disposal_machine = null, object hasmob = null)
    {
        disposal_holder.destinationTag = disposal_tag;
    }
}

// tg: code/modules/mod/modules/modules_supply.dm:412
public partial class obj_item_mod_module_magnet : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_supply.dm:425
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        if (istype(mod.wearer.pulling, typeof(obj_structure_closet)))
        {
            obj_structure_closet locker__1 = mod.wearer.pulling as obj_structure_closet;
            playsound(locker__1, Dm.resource("sound/effects/gravhit.ogg"), 75, TRUE);
            await locker__1.forceMove(mod.wearer.loc);
            await locker__1.throw_at(target, range: 7, speed: 4, thrower: mod.wearer);
            return dot;
        }
        if (!istype(target, typeof(obj_structure_closet)) || !Dm.@in(target, view(null, mod.wearer)))
        {
            balloon_alert(mod.wearer, I18n.T("invalid target!"));
            return dot;
        }
        obj_structure_closet locker = target as obj_structure_closet;
        if (Dm.truthy(locker.anchored) || locker.move_resist >= MOVE_FORCE_OVERPOWERING)
        {
            balloon_alert(mod.wearer, I18n.T("target anchored!"));
            return dot;
        }
        Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(locker));
        playsound(locker, Dm.resource("sound/effects/gravhit.ogg"), 75, TRUE);
        await locker.throw_at(mod.wearer, range: 7, speed: 3, force: MOVE_FORCE_WEAK,
            callback: Dm.@new<datum_callback>(this, nameof(check_locker), locker));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:447
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        if (istype(mod.wearer.pulling, typeof(obj_structure_closet)))
            await mod.wearer.stop_pulling();
    }

    // tg: code/modules/mod/modules/modules_supply.dm:451
    public virtual async DmTask check_locker(obj_structure_closet locker = null)
    {
        if (mod?.wearer == null)
            return;
        if (!Dm.truthy(await locker.Adjacent(mod.wearer)) || !isturf(locker.loc) || !isturf(mod.wearer.loc))
            return;
        await mod.wearer.start_pulling(locker);
        ADD_TRAIT(locker, TRAIT_STRONGPULL, REF(mod.wearer));
        RegisterSignal(locker, COMSIG_ATOM_NO_LONGER_PULLED, nameof(on_stop_pull));
    }

    // tg: code/modules/mod/modules/modules_supply.dm:460
    [SignalHandler]
    public virtual void on_stop_pull(obj_structure_closet locker = null, atom_movable last_puller = null)
    {
        REMOVE_TRAIT(locker, TRAIT_STRONGPULL, REF(mod.wearer));
        UnregisterSignal(locker, COMSIG_ATOM_NO_LONGER_PULLED);
    }
}

// tg: code/modules/mod/modules/modules_supply.dm:466
public partial class obj_item_mod_module_ash_accretion : obj_item_mod_module
{
    /// Turfs that let us accrete ash.
    // tg: code/modules/mod/modules/modules_supply.dm:485  var/static/list/accretion_turfs
    public static ref DmList accretion_turfs => ref TgState.Current.obj_item_mod_module_ash_accretion__accretion_turfs;
    /// Turfs that let us keep ash.
    // tg: code/modules/mod/modules/modules_supply.dm:487  var/static/list/keep_turfs
    public static ref DmList keep_turfs => ref TgState.Current.obj_item_mod_module_ash_accretion__keep_turfs;

    // tg: code/modules/mod/modules/modules_supply.dm:496
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(accretion_turfs))
        {
            accretion_turfs = typecacheof(Dm.list(
                typeof(turf_open_misc_asteroid),
                typeof(turf_open_misc_ashplanet),
                typeof(turf_open_misc_dirt)
            )) as DmList;
        }
        if (!Dm.truthy(keep_turfs))
        {
            keep_turfs = typecacheof(Dm.list(
                typeof(turf_open_misc_grass),
                typeof(turf_open_floor_plating_snowed),
                typeof(turf_open_misc_sandy_dirt),
                typeof(turf_open_misc_ironsand),
                typeof(turf_open_misc_ice),
                typeof(turf_open_indestructible_hierophant),
                typeof(turf_open_indestructible_boss),
                typeof(turf_open_indestructible_necropolis),
                typeof(turf_open_lava),
                typeof(turf_open_water)
            )) as DmList;
        }
        return dot;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:518
    public override async DmTask on_part_activation()
    {
        mod.wearer.add_traits(Dm.list(TRAIT_ASHSTORM_IMMUNE, TRAIT_SNOWSTORM_IMMUNE), REF(this));
        RegisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED, nameof(on_move));
        RegisterSignal(mod, COMSIG_MOD_UPDATE_SPEED, nameof(on_update_speed));
    }

    // tg: code/modules/mod/modules/modules_supply.dm:523
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        mod.wearer.remove_traits(Dm.list(TRAIT_ASHSTORM_IMMUNE, TRAIT_SNOWSTORM_IMMUNE), REF(this));
        UnregisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED);
        UnregisterSignal(mod, COMSIG_MOD_UPDATE_SPEED);
        if (!Dm.truthy(traveled_tiles))
            return;
        datum_armor to_remove = get_armor_by_type(armor_mod) as datum_armor;
        foreach (obj_item part in Dm.each_cast<obj_item>(mod.get_parts(all: TRUE)))
            part.set_armor(part.get_armor().subtract_other_armor(to_remove.generate_new_with_multipliers(Dm.list(Dm.kv(ARMOR_ALL, traveled_tiles)))));
        if (traveled_tiles == max_traveled_tiles)
            await mod.update_speed();
        traveled_tiles = 0;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:536
    public override object generate_worn_overlay(obj_item source = null, mutable_appearance standing = null)
    {
        overlay_state_inactive = Dm.text("[]-[]", Dm.initial(this, (obj_item_mod_module_ash_accretion p) => p.overlay_state_inactive), mod.skin);
        return base.generate_worn_overlay(source, standing);
    }

    // tg: code/modules/mod/modules/modules_supply.dm:540
    [SignalHandler]
    public virtual void on_update_speed(datum source = null, DmList module_slowdowns = null, object prevent_slowdown = null)
    {
        if (traveled_tiles == max_traveled_tiles)
            Dm.append(ref module_slowdowns, speed_added);
    }

    // tg: code/modules/mod/modules/modules_supply.dm:545
    public virtual async DmTask on_move(atom source = null, atom oldloc = null, object dir = null, object forced = null)
    {
        if (!isturf(mod.wearer.loc)) //dont lose ash from going in a locker
            return;

        if (Dm.truthy(is_type_in_typecache(mod.wearer.loc, accretion_turfs)))
        {
            if (traveled_tiles >= max_traveled_tiles)
                return;

            traveled_tiles++;
            foreach (obj_item part in Dm.each_cast<obj_item>(mod.get_parts(all: TRUE)))
                part.set_armor(part.get_armor().add_other_armor(armor_mod));

            if (traveled_tiles < max_traveled_tiles)
                return;

            balloon_alert(mod.wearer, I18n.T("fully ash covered"));
            object cur_color = mod.wearer.color;
            mod.wearer.color = Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 3); // Make them super light
            animate(mod.wearer, Dm.named("time", 1 * SECONDS), Dm.named("color", cur_color), Dm.named("flags", ANIMATION_PARALLEL));
            playsound(this, Dm.resource("sound/effects/sparks/sparks1.ogg"), 100, TRUE);
            await mod.update_speed();
            return;
        }

        if (Dm.truthy(is_type_in_typecache(mod.wearer.loc, keep_turfs)))
            return;

        if (Dm.truthy(traveled_tiles)) //leave ash every tile
            Dm.@new<obj_effect_temp_visual_light_ash>(get_turf(this));

        if (traveled_tiles <= 0)
            return;

        traveled_tiles--;
        if (traveled_tiles == max_traveled_tiles - 1) // Just lost our speed buff
            await mod.update_speed();

        foreach (obj_item part in Dm.each_cast<obj_item>(mod.get_parts(all: TRUE)))
            part.set_armor(part.get_armor().subtract_other_armor(armor_mod));

        if (traveled_tiles <= 0)
            balloon_alert(mod.wearer, I18n.T("ran out of ash!"));
    }
}

// tg: code/modules/mod/modules/modules_supply.dm:587
public partial class obj_item_mod_module_sphere_transform : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_supply.dm:618
    public override async DmTask on_install()
    {
        await base.on_install();
        RegisterSignal(mod, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_item_interaction));
    }

    // Isn't supposed to happen outside of deletion but just in case
    // tg: code/modules/mod/modules/modules_supply.dm:623
    public override async DmTask on_uninstall(float? deleting = null)
    {
        await base.on_uninstall(deleting);
        // No need to drop the hide as we're supposed to be inbuilt and unremovable
        UnregisterSignal(mod, COMSIG_ATOM_ITEM_INTERACTION);
    }

    // tg: code/modules/mod/modules/modules_supply.dm:628
    [SignalHandler]
    public virtual object on_item_interaction(atom_movable source = null, mob_living user = null, obj_item item = null, object modifiers = null)
    {
        if (!istype(item, typeof(obj_item_stack_sheet_animalhide_bileworm)))
            return NONE;

        if (Dm.truthy(hide_upgrade))
        {
            to_chat(user, span_warning(I18n.T("[] is already reinforced with bileworm skin!", mod)));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item_stack_sheet_animalhide_bileworm hide = item as obj_item_stack_sheet_animalhide_bileworm;
        if (!Dm.truthy(Waiting.NoSleep(hide.use(hide_amount))))
        {
            to_chat(user, span_warning(I18n.T("You need more hide to fully reinforce []!", mod)));
            return ITEM_INTERACT_BLOCKING;
        }

        hide_upgrade = TRUE;
        overlay_state_inactive = "module_bileworm_bracing";
        Dm.append(ref user_traits, TRAIT_LAVA_IMMUNE);
        mod.balloon_alert(user, I18n.T("plating reinforced!"));
        if (Dm.truthy(active))
            ADD_TRAIT(mod.wearer, TRAIT_LAVA_IMMUNE, REF(this));
        Waiting.NoSleep(update_clothing_slots());
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:652
    public override async DmTask<object> activate(mob activator = null)
    {
        if (!Dm.truthy(mod.wearer.has_gravity()))
        {
            balloon_alert(activator, I18n.T("no gravity!"));
            return FALSE;
        }
        return await base.activate(activator);
    }

    // tg: code/modules/mod/modules/modules_supply.dm:658
    public override async DmTask on_activation(mob activator = null)
    {
        playsound(this, Dm.resource("sound/items/modsuit/ballin.ogg"), 100, TRUE);
        mod.wearer.add_filter("mod_ball", 1, alpha_mask_filter(icon: icon(Dm.resource("icons/mob/clothing/modsuit/mod_modules.dmi"), "ball_mask"), flags: MASK_INVERSE) as DmList);
        mod.wearer.add_filter("mod_blur", 2, angular_blur_filter(size: 15) as DmList);
        mod.wearer.add_filter("mod_outline", 3, outline_filter(color: "#000000AA") as DmList);
        mod.wearer.add_offsets(REF(this), y_add: -4);
        mod.wearer.SpinAnimation(1.5f, tag: "sphere_transform");
        mod.wearer.add_traits(user_traits, REF(this));
        mod.wearer.RemoveElement(typeof(datum_element_footstep), FOOTSTEP_MOB_HUMAN, 1, -6);
        await mod.wearer.AddElement(typeof(datum_element_footstep), FOOTSTEP_OBJ_ROBOT, 1, -6, Dm.named("sound_vary", TRUE));
        mod.wearer.add_movespeed_mod_immunities(REF(this), typeof(datum_movespeed_modifier_damage_slowdown));
        await mod.wearer.add_movespeed_modifier(typeof(datum_movespeed_modifier_sphere));
        RegisterSignal(mod.wearer, COMSIG_MOB_STATCHANGE, nameof(on_statchange));
        RegisterSignal(mod.wearer, COMSIG_CARBON_GET_FIRE_OVERLAY, nameof(replace_fire_overlay));
        mod.wearer.update_appearance(UPDATE_ICON);
        foreach (obj_item part in Dm.each_cast<obj_item>(mod.get_parts(all: TRUE)))
            part.set_armor(part.get_armor().add_other_armor(armor_mod));
    }

    // tg: code/modules/mod/modules/modules_supply.dm:676
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        deleting ??= FALSE;
        if (!Dm.truthy(deleting))
            playsound(this, Dm.resource("sound/items/modsuit/ballin.ogg"), 100, TRUE, frequency: -1);
        mod.wearer.remove_offsets(REF(this));
        _addtimer(Dm.@new<datum_callback>(mod.wearer, nameof(datum.remove_filter), Dm.list("mod_ball", "mod_blur", "mod_outline")), animate_time, file: "code/modules/mod/modules/modules_supply.dm", line: 679);
        mod.wearer.remove_traits(user_traits, REF(this));
        mod.wearer.remove_movespeed_mod_immunities(REF(this), typeof(datum_movespeed_modifier_damage_slowdown));
        animate(mod.wearer, Dm.named("tag", "sphere_transform"));
        mod.wearer.RemoveElement(typeof(datum_element_footstep), FOOTSTEP_OBJ_ROBOT, 1, -6, Dm.named("sound_vary", TRUE));
        await mod.wearer.AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_HUMAN, 1, -6);
        mod.wearer.remove_movespeed_modifier(typeof(datum_movespeed_modifier_sphere));
        UnregisterSignal(mod.wearer, Dm.list(COMSIG_MOB_STATCHANGE, COMSIG_CARBON_GET_FIRE_OVERLAY));
        mod.wearer.update_appearance(UPDATE_ICON);
        foreach (obj_item part in Dm.each_cast<obj_item>(mod.get_parts(all: TRUE)))
            part.set_armor(part.get_armor().subtract_other_armor(armor_mod));
    }

    // tg: code/modules/mod/modules/modules_supply.dm:692
    [SignalHandler]
    public virtual void replace_fire_overlay(datum source = null, object stacks = null, object on_fire = null, object fire_icon = null, DmList overrides = null)
    {
        mutable_appearance fire_overlay = mutable_appearance(
            Dm.resource("icons/mob/effects/onfire.dmi"),
            fire_icon as string,
            -HIGHEST_LAYER,
            appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)
        ) as mutable_appearance;
        fire_overlay.add_filter("mod_ball", 1, alpha_mask_filter(icon: icon(Dm.resource("icons/mob/clothing/modsuit/mod_modules.dmi"), "ball_mask"), flags: MASK_INVERSE) as DmList);
        Dm.append(ref overrides, fire_overlay);
    }

    // tg: code/modules/mod/modules/modules_supply.dm:704
    public override async DmTask<object> used(mob activator = null)
    {
        if (!Dm.truthy(lavaland_equipment_pressure_check(get_turf(this))))
        {
            balloon_alert(activator, I18n.T("too much pressure!"));
            playsound(this, Dm.resource("sound/items/weapons/gun/general/dry_fire.ogg"), 25, TRUE);
            return FALSE;
        }
        return await base.used(activator);
    }

    // tg: code/modules/mod/modules/modules_supply.dm:711
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        await drain_power(use_energy_cost);
        Waiting.Detach(fire_missile(target)); // INVOKE_ASYNC(src, PROC_REF(fire_missile), target)
        for (float i = 1; i <= 2; i++)
            _addtimer(Dm.@new<datum_callback>(this, nameof(fire_missile), target), 0.2f * SECONDS * i, file: "code/modules/mod/modules/modules_supply.dm", line: 718);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:720
    public virtual async DmTask fire_missile(atom target = null)
    {
        obj_projectile_bullet_mining_missile missile = Dm.@new<obj_projectile_bullet_mining_missile>(mod.wearer.loc);
        await missile.aim_projectile(target, mod.wearer);
        missile.firer = mod.wearer;
        if (isliving(target))
            missile.set_homing_target(target);
        playsound(this, Dm.resource("sound/items/weapons/gun/general/rocket_launch.ogg"), 30, TRUE);
        await missile.fire();
    }

    // tg: code/modules/mod/modules/modules_supply.dm:729
    public override async DmTask on_active_process(object seconds_per_tick = null)
    {
        if (!Dm.truthy(mod.wearer.has_gravity()))
            await deactivate(); //deactivate in no grav
    }

    // tg: code/modules/mod/modules/modules_supply.dm:733
    [SignalHandler]
    public virtual void on_statchange(datum source = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(mod.wearer))
            Waiting.NoSleep(deactivate());
    }
}

// tg: code/modules/mod/modules/modules_supply.dm:738
public partial class obj_projectile_bullet_mining_missile : obj_projectile_bullet
{
    // tg: code/modules/mod/modules/modules_supply.dm:757
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = null;
        playsound(get_turf(target), Dm.resource("sound/items/weapons/sonic_jackhammer.ogg"), 75, TRUE);
        if (ismineralturf(target))
        {
            dot = await base.on_hit(target, blocked, pierce_hit);
            spawn_particles(target);
            turf_closed_mineral rock = target as turf_closed_mineral;
            await rock.gets_drilled(firer as mob);
            return BULLET_ACT_HIT;
        }

        if (!isliving(target))
        {
            dot = await base.on_hit(target, blocked, pierce_hit);
            spawn_particles(target);
            return dot;
        }

        if (isliving(target))
        {
            mob_living victim = target as mob_living;
            if (ismining(victim))
                damage = Dm.num(damage) * fauna_boost;
        }
        dot = await base.on_hit(target, blocked, pierce_hit);
        spawn_particles(target);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:778
    public virtual void spawn_particles(atom target = null)
    {
        // var/obj/effect/abstract/particle_holder/impact_particles = new(get_turf(target), /particles/micromissile_impact); its position
        // and velocity generators (generator(GEN_BOX, ...)) and QDEL_IN(impact_particles, /particles/micromissile_impact::lifespan):
        // DM particles, which the client does not draw (not-translated.md 222, as the lavaland core's pollen)
        Dm.Deferred("code/modules/mod/modules/modules_supply.dm:778", "spawn_particles: the micromissile's impact particles (DM particles; the client draws none)");
    }
}

// tg: code/modules/mod/modules/modules_supply.dm:796
public partial class obj_item_mod_module_candela_spool : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_supply.dm:810
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        handler = Dm.@new<datum_candela_item_handler>(this, Dm.named("on_network_cut_callback", Dm.@new<datum_callback>(this, nameof(on_network_cut))));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:814
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref handler);
        return base.Destroy(force);
    }

    // tg: code/modules/mod/modules/modules_supply.dm:818
    public override async DmTask on_install()
    {
        await base.on_install();
        RegisterSignal(mod, COMSIG_ATOM_STORED_ITEM, nameof(on_stored_item));
    }

    // tg: code/modules/mod/modules/modules_supply.dm:822
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        UnregisterSignal(mod, COMSIG_ATOM_STORED_ITEM);
    }

    // tg: code/modules/mod/modules/modules_supply.dm:826
    public override async DmTask on_activation(mob activator = null)
    {
        handler.set_owner(activator as mob_living);
    }

    // tg: code/modules/mod/modules/modules_supply.dm:829
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        handler.set_owner(null);
    }

    // tg: code/modules/mod/modules/modules_supply.dm:832
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        await attempt_deploy(mod.wearer, get_turf(mod.wearer));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_supply.dm:838
    [SignalHandler]
    public virtual void on_stored_item(datum source = null, obj_item stored = null, mob user = null, object force = null)
    {
        if (!istype(stored, typeof(obj_item_stack_candela_beacon)))
            return;

        // When storing linked beacons in our MODsuit, inherit their link for convinience
        obj_item_stack_candela_beacon beacons = stored as obj_item_stack_candela_beacon;
        if (beacons.handler.network != null)
            handler.set_network(beacons.handler.network, beacons.handler.closest_node);
    }

    /// Callback to react to breaking LOS/reaching maximum distance with a beacon
    // tg: code/modules/mod/modules/modules_supply.dm:850
    public virtual async DmTask<object> on_network_cut(atom old_loc = null, object old_dir = null, object interrupt = null)
    {
        if (mod.wearer != null && handler.closest_node != null && !Dm.truthy(interrupt))
            return await attempt_deploy(mod.wearer, old_loc, silent: TRUE);
        return FALSE;
    }

    /// Attempt to deploy a candela beacon at a given location
    // tg: code/modules/mod/modules/modules_supply.dm:856
    public virtual async DmTask<object> attempt_deploy(mob activator = null, atom location = null, float? silent = null)
    {
        silent ??= FALSE;
        obj_item_stack_candela_beacon beacons = locate<obj_item_stack_candela_beacon>(mod.atom_storage.real_location);
        if (beacons == null)
        {
            balloon_alert(activator, I18n.T("no beacons!"));
            return FALSE;
        }

        if (!Dm.truthy(await beacons.place_beacon(activator, loc_override: location, handler_override: handler, silent: silent)))
            return FALSE;

        await drain_power(use_energy_cost);
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mod/modules/modules_supply.dm:485  var/static/list/accretion_turfs
    internal DmList obj_item_mod_module_ash_accretion__accretion_turfs;
    // tg: code/modules/mod/modules/modules_supply.dm:487  var/static/list/keep_turfs
    internal DmList obj_item_mod_module_ash_accretion__keep_turfs;
}
