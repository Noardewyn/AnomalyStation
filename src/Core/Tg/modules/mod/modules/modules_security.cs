// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/modules_security.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole. The security MOD modules: the magnetic harness (a dropped gun snaps back to the suit storage), the
// pepper shoulders (a pepper smoke when hit or used), the holster, the megaphone (loud speech), criminal capture (a hardlight prisoner
// bag, dissipated at a distance) and its patient transport kind, the mirage grenade dispenser and the mirage grenade (a decoy of its
// thrower), the projectile dampener (a field that slows and weakens bullets), the active sonar (pings of the living around), the
// shooting assistant (stormtrooper or sharpshooter firing), the bulwark (no shoves or staggers) and the restraint assist (fast
// cuffing). The var blocks are generated (D111).

///Magnetic Harness - Automatically puts guns in your suit storage when you drop them.
// tg: code/modules/mod/modules/modules_security.dm:4
public partial class obj_item_mod_module_magnetic_harness : obj_item_mod_module
{
    /// The typecache of all guns we allow.
    // tg: code/modules/mod/modules/modules_security.dm:16  var/static/list/guns_typecache
    public static ref DmList guns_typecache => ref TgState.Current.obj_item_mod_module_magnetic_harness__guns_typecache;

    // tg: code/modules/mod/modules/modules_security.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(guns_typecache))
        {
            guns_typecache = typecacheof(Dm.list(
                typeof(obj_item_gun_ballistic),
                typeof(obj_item_gun_energy),
                typeof(obj_item_gun_grenadelauncher),
                typeof(obj_item_gun_chem),
                typeof(obj_item_gun_syringe),
                typeof(obj_item_kinetic_crusher)
            )) as DmList;
        }
        return dot;
    }

    // tg: code/modules/mod/modules/modules_security.dm:32
    public override async DmTask on_install()
    {
        await base.on_install();
        obj_item_clothing suit = mod.get_part_from_slot(ITEM_SLOT_OCLOTHING) as obj_item_clothing;
        if (!istype(suit, typeof(obj_item_clothing)))
            return;
        already_allowed_guns = Dm.bitand(guns_typecache, suit.allowed) as DmList;
        Dm.combine(ref suit.allowed, guns_typecache);
    }

    // tg: code/modules/mod/modules/modules_security.dm:40
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        if (Dm.truthy(deleting))
            return;
        obj_item_clothing suit = mod.get_part_from_slot(ITEM_SLOT_OCLOTHING) as obj_item_clothing;
        if (!istype(suit, typeof(obj_item_clothing)))
            return;
        Dm.remove(ref suit.allowed, Dm.sub(guns_typecache, already_allowed_guns));
    }

    // tg: code/modules/mod/modules/modules_security.dm:49
    public override async DmTask on_part_activation()
    {
        RegisterSignal(mod.wearer, COMSIG_MOB_UNEQUIPPED_ITEM, nameof(check_dropped_item));
    }

    // tg: code/modules/mod/modules/modules_security.dm:52
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        UnregisterSignal(mod.wearer, COMSIG_MOB_UNEQUIPPED_ITEM);
    }

    // tg: code/modules/mod/modules/modules_security.dm:55
    [SignalHandler]
    public virtual void check_dropped_item(datum source = null, obj_item dropped_item = null, object force = null, object new_location = null)
    {
        if (!Dm.truthy(is_type_in_typecache(dropped_item, guns_typecache)))
            return;
        if (new_location != get_turf(this))
            return;
        _addtimer(Dm.@new<datum_callback>(this, nameof(pick_up_item), dropped_item), magnet_delay, file: "code/modules/mod/modules/modules_security.dm", line: 62);
    }

    // tg: code/modules/mod/modules/modules_security.dm:64
    public virtual async DmTask pick_up_item(obj_item item = null)
    {
        if (!isturf(item.loc) || !Dm.truthy(await item.Adjacent(mod.wearer)))
            return;
        if (!Dm.truthy(await mod.wearer.equip_to_slot_if_possible(item, ITEM_SLOT_SUITSTORE, qdel_on_fail: FALSE, disable_warning: TRUE)))
            return;
        playsound(this, Dm.resource("sound/items/modsuit/magnetic_harness.ogg"), 50, TRUE);
        balloon_alert(mod.wearer, I18n.T("[] reattached", item));
        await drain_power(use_energy_cost);
    }
}

///Pepper Shoulders - When hit, reacts with a spray of pepper spray around the user.
// tg: code/modules/mod/modules/modules_security.dm:74
public partial class obj_item_mod_module_pepper_shoulders : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_security.dm:87
    public override async DmTask on_part_activation()
    {
        RegisterSignal(mod.wearer, COMSIG_LIVING_CHECK_BLOCK, nameof(on_check_block));
    }

    // tg: code/modules/mod/modules/modules_security.dm:90
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        UnregisterSignal(mod.wearer, COMSIG_LIVING_CHECK_BLOCK);
    }

    // tg: code/modules/mod/modules/modules_security.dm:93
    public override async DmTask<object> on_use(mob activator = null)
    {
        playsound(this, Dm.resource("sound/effects/spray.ogg"), 30, TRUE, -6);
        await do_chem_smoke(1, this, get_turf(this), typeof(datum_reagent_consumable_condensedcapsaicin), 10, log: TRUE, smoke_type: typeof(datum_effect_system_fluid_spread_smoke_chem_quick));
        return null;
    }

    // tg: code/modules/mod/modules/modules_security.dm:97
    [SignalHandler]
    public virtual void on_check_block()
    {
        if (!(cooldown_timer <= world.time)) // !COOLDOWN_FINISHED(src, cooldown_timer)
            return;
        if (!Dm.truthy(check_power(use_energy_cost)))
            return;
        mod.wearer.visible_message(span_warning(I18n.T("[] reacts to the attack with a smoke of pepper spray!", this)), span_notice(I18n.T("Your [] releases a cloud of pepper spray!", this)));
        Waiting.NoSleep(used());
    }
}

///Holster - Instantly holsters any not huge gun.
// tg: code/modules/mod/modules/modules_security.dm:108
public partial class obj_item_mod_module_holster : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_security.dm:124
    public override async DmTask<object> on_use(mob activator = null)
    {
        if (holstered == null)
        {
            obj_item_gun holding = mod.wearer.get_active_held_item() as obj_item_gun;
            if (mod.wearer.get_active_held_item() == null)
            {
                balloon_alert(mod.wearer, I18n.T("nothing to holster!"));
                return null;
            }
            if (!istype(holding, typeof(obj_item_gun)) || holding.w_class > WEIGHT_CLASS_BULKY)
            {
                balloon_alert(mod.wearer, I18n.T("doesn't fit!"));
                return null;
            }
            if (Dm.truthy(await mod.wearer.transferItemToLoc(holding, this, force: FALSE, silent: TRUE)))
            {
                holstered = holding;
                balloon_alert(mod.wearer, I18n.T("weapon holstered"));
                playsound(this, Dm.resource("sound/items/weapons/gun/revolver/empty.ogg"), 100, TRUE);
            }
        }
        else if (Dm.truthy(await mod.wearer.put_in_active_hand(holstered, forced: FALSE, ignore_animation: TRUE)))
        {
            balloon_alert(mod.wearer, I18n.T("weapon drawn"));
            playsound(this, Dm.resource("sound/items/weapons/gun/revolver/empty.ogg"), 100, TRUE);
        }
        else
            balloon_alert(mod.wearer, I18n.T("holster full!"));
        return null;
    }

    // tg: code/modules/mod/modules/modules_security.dm:143
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        if (holstered != null)
            await holstered.forceMove(mod.drop_location() as atom);
    }

    // tg: code/modules/mod/modules/modules_security.dm:148
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == holstered)
            holstered = null;
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/mod/modules/modules_security.dm:153
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref holstered);
        return base.Destroy(force);
    }
}

///Megaphone - Lets you speak loud.
// tg: code/modules/mod/modules/modules_security.dm:158
public partial class obj_item_mod_module_megaphone : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_security.dm:170
    public override async DmTask on_activation(mob activator = null)
    {
        RegisterSignal(mod.wearer, COMSIG_MOB_SAY, nameof(handle_speech));
        RegisterSignal(mod.wearer, COMSIG_LIVING_TREAT_MESSAGE, nameof(add_tts_filter));
    }

    // tg: code/modules/mod/modules/modules_security.dm:174
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        UnregisterSignal(mod.wearer, Dm.list(COMSIG_LIVING_TREAT_MESSAGE, COMSIG_MOB_SAY));
    }

    // tg: code/modules/mod/modules/modules_security.dm:177
    [SignalHandler]
    public virtual void handle_speech(datum source = null, DmList speech_args = null)
    {
        Dm.combine_at(speech_args, SPEECH_SPANS, voicespan);
        Waiting.NoSleep(drain_power(use_energy_cost));
    }

    // tg: code/modules/mod/modules/modules_security.dm:183
    [SignalHandler]
    public virtual void add_tts_filter(mob_living_carbon user = null, DmList message_args = null)
    {
        ///A sharper and louder sound with a bit of echo
        Dm.append_at(message_args, TREAT_TTS_FILTER_ARG, "acrusher=samples=2:level_out=6,aecho=delays=90:decays=0.3,aemphasis=type=cd,acontrast=30,crystalizer=i=5");
    }
}

///Criminal Capture - Generates hardlight bags you can put people in and sinch.
// tg: code/modules/mod/modules/modules_security.dm:189
public partial class obj_item_mod_module_criminalcapture : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_security.dm:213
    public override async DmTask<object> on_process(object seconds_per_tick = null)
    {
        idle_power_cost = linked_bodybag != null ? (DEFAULT_CHARGE_DRAIN * 3) : 0;
        return await base.on_process(seconds_per_tick);
    }

    // tg: code/modules/mod/modules/modules_security.dm:217
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        if (linked_bodybag == null)
            return;
        packup();
    }

    // tg: code/modules/mod/modules/modules_security.dm:222
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await mod.wearer.Adjacent(target)))
            return dot;
        if (target == linked_bodybag)
        {
            playsound(this, Dm.resource("sound/machines/ding.ogg"), 25, TRUE);
            if (!Dm.truthy(await do_after(mod.wearer, packup_time, target: target)))
                balloon_alert(mod.wearer, I18n.T("interrupted!"));
            packup();
            return dot;
        }
        if (linked_bodybag != null)
            return dot;
        turf target_turf = get_turf(target);
        if (Dm.truthy(await target_turf.is_blocked_turf(exclude_mobs: TRUE)))
            return dot;
        playsound(this, Dm.resource("sound/machines/ding.ogg"), 25, TRUE);
        if (!Dm.truthy(await do_after(mod.wearer, capture_time, target: target)))
        {
            balloon_alert(mod.wearer, I18n.T("interrupted!"));
            return dot;
        }
        if (linked_bodybag != null)
            return dot;
        linked_bodybag = Dm.@new(bodybag_type, target_turf) as obj_structure_closet_body_bag;
        await linked_bodybag.take_contents();
        playsound(linked_bodybag, Dm.resource("sound/items/weapons/egloves.ogg"), 80, TRUE);
        RegisterSignal(linked_bodybag, COMSIG_MOVABLE_MOVED, nameof(check_range));
        RegisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED, nameof(check_range));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_security.dm:251
    public virtual void packup()
    {
        if (linked_bodybag == null)
            return;
        playsound(linked_bodybag, Dm.resource("sound/items/weapons/egloves.ogg"), 80, TRUE);
        apply_wibbly_filters(linked_bodybag);
        animate(linked_bodybag, Dm.named("time", 0.5f * SECONDS), Dm.named("alpha", 50), Dm.named("flags", ANIMATION_PARALLEL));
        _addtimer(Dm.@new<datum_callback>(this, nameof(delete_bag), linked_bodybag), 0.5f * SECONDS, file: "code/modules/mod/modules/modules_security.dm", line: 257);
        linked_bodybag = null;
    }

    // tg: code/modules/mod/modules/modules_security.dm:260
    [SignalHandler]
    public virtual void check_range()
    {
        if (get_dist(mod.wearer, linked_bodybag) <= 9)
            return;
        packup();
    }

    // tg: code/modules/mod/modules/modules_security.dm:267
    public virtual async DmTask delete_bag(obj_structure_closet_body_bag bag = null)
    {
        if (mod?.wearer != null)
        {
            UnregisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED); // tg passes PROC_REF(check_range) too, an argument UnregisterSignal has no parameter for
            balloon_alert(mod.wearer, I18n.T("bag dissipated"));
        }
        await bag.open(force: TRUE);
        qdel(bag);
    }
}

///Mirage grenade dispenser - Dispenses grenades that copy the user's appearance.
// tg: code/modules/mod/modules/modules_security.dm:275
public partial class obj_item_mod_module_dispenser_mirage : obj_item_mod_module_dispenser
{
    // tg: code/modules/mod/modules/modules_security.dm:284
    public override async DmTask<object> on_use(mob activator = null)
    {
        obj_item_grenade_mirage grenade = await base.on_use(activator) as obj_item_grenade_mirage;
        await grenade.arm_grenade(mod.wearer);
        return null;
    }
}

// tg: code/modules/mod/modules/modules_security.dm:288
public partial class obj_item_grenade_mirage : obj_item_grenade
{
    // tg: code/modules/mod/modules/modules_security.dm:297
    public override async DmTask arm_grenade(mob user = null, object delayoverride = null, float? msg = null, float? volume = null)
    {
        await base.arm_grenade(user, delayoverride, msg, volume);
        thrower = user as mob_living;
    }

    // tg: code/modules/mod/modules/modules_security.dm:301
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        do_sparks(rand(3, 6), FALSE, this);
        if (thrower != null)
        {
            mob_living_basic_illusion_mirage mirage = Dm.@new<mob_living_basic_illusion_mirage>(get_turf(this));
            await mirage.mock_as(thrower, 15 * SECONDS);
        }
        qdel(this);
        return dot;
    }
}

///Projectile Dampener - Weakens projectiles in range.
// tg: code/modules/mod/modules/modules_security.dm:310
public partial class obj_item_mod_module_projectile_dampener : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_security.dm:336
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        projectile_effect = image(Dm.resource("icons/effects/fields.dmi"), "projectile_dampen_effect");
        return dot;
    }

    // tg: code/modules/mod/modules/modules_security.dm:340
    public override async DmTask on_activation(mob activator = null)
    {
        if (istype(dampening_field, typeof(datum_proximity_monitor_advanced_bubble_projectile_dampener)))
            QDEL_NULL(ref dampening_field);
        dampening_field = await Dm.new_async<datum_proximity_monitor_advanced_bubble_projectile_dampener>(mod.wearer, field_radius, TRUE, this);
    }

    // tg: code/modules/mod/modules/modules_security.dm:345
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        QDEL_NULL(ref dampening_field);
    }
}

///Active Sonar - Displays a hud circle on the turf of any living creatures in the given radius
// tg: code/modules/mod/modules/modules_security.dm:349
public partial class obj_item_mod_module_active_sonar : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_security.dm:378
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        for (float i = 1; i <= radar_slices; i++)
            Dm.append(ref sorted_creatures, Dm.list(Dm.list()));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_security.dm:383
    public override async DmTask on_part_activation()
    {
        RegisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED, nameof(sort_all_creatures));
    }

    // tg: code/modules/mod/modules/modules_security.dm:386
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        UnregisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED);
    }

    /// Detects all living creatures within world.view, and returns the amount.
    // tg: code/modules/mod/modules/modules_security.dm:390
    public virtual object detect_living_creatures()
    {
        float creatures_detected = 0;
        foreach (mob_living creature in Dm.each<mob_living>(range(world.view, mod.wearer)))
        {
            if (creature == mod.wearer || Dm.eq(creature.stat, DEAD))
                continue;
            if (Dm.truthy(keyed_creatures[creature]))
            {
                creatures_detected++;
                continue;
            }
            sort_creature_angle(creature);
            RegisterSignal(creature, COMSIG_MOVABLE_MOVED, nameof(sort_creature_angle));
            creatures_detected++;
        }
        return creatures_detected;
    }

    /// Swaps around where a creature is, when they move or when they're first detected
    // tg: code/modules/mod/modules/modules_security.dm:404
    [SignalHandler]
    public virtual void sort_creature_angle(mob_living creature = null, atom old_loc = null, object movement_dir = null, object forced = null)
    {
        object oldgroup = keyed_creatures[creature];
        float newgroup = round(Dm.num(get_angle(mod.wearer, creature)) / (360 / radar_slices)) + 1;
        if (Dm.truthy(oldgroup))
        {
            if (Dm.eq(creature.stat, DEAD) || get_dist(get_turf(mod.wearer), get_turf(creature)) > Dm.num(world.view))
            {
                Dm.remove_at(sorted_creatures, oldgroup, creature);
                Dm.remove(ref keyed_creatures, creature);
                UnregisterSignal(creature, COMSIG_MOVABLE_MOVED);
                return;
            }

            if (Dm.eq(oldgroup, newgroup))
                return;

            Dm.remove_at(sorted_creatures, oldgroup, creature);
        }

        Dm.append_at(sorted_creatures, newgroup, creature);
        keyed_creatures[creature] = newgroup;
    }

    /// Swaps all creatures when mod.wearer moves
    // tg: code/modules/mod/modules/modules_security.dm:424
    [SignalHandler]
    public virtual void sort_all_creatures(mob_living wearer = null, atom old_loc = null, object movement_dir = null, object forced = null)
    {
        foreach (mob_living creature in Dm.each_cast<mob_living>(keyed_creatures))
            sort_creature_angle(creature); // Kinda spaghetti but it honestly seems like the shortest path to the same result
    }

    // tg: code/modules/mod/modules/modules_security.dm:430
    public override async DmTask<object> on_process(object seconds_per_tick = null)
    {
        object dot = await base.on_process(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;
        if (!(cooldown_timer <= world.time) || !(scan_cooldown <= world.time)) // !COOLDOWN_FINISHED(src, cooldown_timer) || !COOLDOWN_FINISHED(src, scan_cooldown)
            return dot;
        detect_living_creatures();
        foreach (mob_living creature in Dm.each_cast<mob_living>(sorted_creatures[scanned_slice]))
            Dm.@new<obj_effect_temp_visual_sonar_ping>(mod.wearer.loc, mod.wearer, creature, "sonar_ping_small", FALSE);
        // Next slice!
        scanned_slice++;
        // IT'S ENOUGH SLICES
        if (scanned_slice > radar_slices)
            scanned_slice = 1;
        scan_cooldown = world.time + scan_cooldown_time; // COOLDOWN_START(src, scan_cooldown, scan_cooldown_time)
        return dot;
    }

    // tg: code/modules/mod/modules/modules_security.dm:446
    public override async DmTask<object> on_use(mob activator = null)
    {
        balloon_alert(activator, I18n.T("readying sonar..."));
        playsound(mod.wearer, Dm.resource("sound/vehicles/mecha/skyfall_power_up.ogg"), vol: 20, vary: TRUE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        if (!Dm.truthy(await do_after(mod.wearer, 1.1f * SECONDS, target: mod)))
            return null;
        playsound(this, Dm.resource("sound/effects/ping_hit.ogg"), vol: 75, vary: TRUE); // Should be audible for the radius of the sonar
        to_chat(mod.wearer, span_notice(I18n.T("You slam your fist into the ground, sending out a sonic wave that detects [] living beings nearby!", detect_living_creatures())));
        foreach (mob_living creature in Dm.each_cast<mob_living>(keyed_creatures))
            Dm.@new<obj_effect_temp_visual_sonar_ping>(mod.wearer.loc, mod.wearer, creature);
        return null;
    }
}

/**
 * A module that enhances the user's ability with firearms, with a couple drawbacks:
 * In 'Stormtrooper' mode, the user will be given faster firerate, but lower accuracy.
 * In 'Sharpshooter' mode, the user will have better accuracy and ricochet to his shots, but slower movement speed.
 * Both modes prevent the user from dual wielding guns.
 */
// tg: code/modules/mod/modules/modules_security.dm:466
public partial class obj_item_mod_module_shooting_assistant : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_security.dm:460 (file-local, #undef at :565)
    private const string SHOOTING_ASSISTANT_OFF = "Currently Off";
    // tg: code/modules/mod/modules/modules_security.dm:461
    private const string STORMTROOPER_MODE = "Quick Fire Stormtrooper";
    // tg: code/modules/mod/modules/modules_security.dm:462
    private const string SHARPSHOOTER_MODE = "Slow Ricochet Sharpshooter";

    ///Association list, the assoc values are the balloon alerts shown to the user when the mode is set.
    // tg: code/modules/mod/modules/modules_security.dm:480  var/static/list/available_modes
    public static DmList available_modes => TgState.Current.obj_item_mod_module_shooting_assistant__available_modes ??= Dm.list(
        Dm.kv(SHOOTING_ASSISTANT_OFF, "assistant off"),
        Dm.kv(STORMTROOPER_MODE, "stormtrooper mode"),
        Dm.kv(SHARPSHOOTER_MODE, "sharpshooter mode"));

    // tg: code/modules/mod/modules/modules_security.dm:486
    public override object get_configuration(mob user = null)
    {
        DmList dot = base.get_configuration(user) as DmList;
        dot["shooting_mode"] = add_ui_configuration(I18n.T("Mode"), "list", selected_mode, assoc_to_keys(available_modes) as DmList);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_security.dm:490
    public override async DmTask configure_edit(object key = null, object value = null)
    {
        switch (key)
        {
            case "shooting_mode":
                await set_shooting_mode(value);
                break;
        }
    }

    // tg: code/modules/mod/modules/modules_security.dm:495
    public virtual async DmTask set_shooting_mode(object new_mode = null)
    {
        if (Dm.eq(new_mode, selected_mode) || !Dm.truthy(mod.active))
            return;
        if (!Dm.eq(new_mode, SHOOTING_ASSISTANT_OFF) && !Dm.truthy(mod.get_charge()))
        {
            balloon_alert(mod.wearer, I18n.T("no charge!"));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return;
        }

        //Remove the effects of the previously selected mode
        if (Dm.truthy(mod.active))
            remove_mode_effects();

        balloon_alert(mod.wearer, I18n.T(Dm.str(available_modes[new_mode])));
        selected_mode = new_mode as string;

        //Apply the effects of the new mode
        if (Dm.truthy(mod.active))
            await apply_mode_effects();
    }

    // tg: code/modules/mod/modules/modules_security.dm:514
    public virtual async DmTask apply_mode_effects()
    {
        switch (selected_mode)
        {
            case SHOOTING_ASSISTANT_OFF:
                idle_power_cost = 0;
                break;
            case STORMTROOPER_MODE:
                idle_power_cost = DEFAULT_CHARGE_DRAIN * 0.4f;
                mod.wearer.add_traits(Dm.list(TRAIT_NO_GUN_AKIMBO, TRAIT_DOUBLE_TAP), REF(this));
                RegisterSignal(mod.wearer, COMSIG_MOB_FIRED_GUN, nameof(stormtrooper_fired_gun));
                break;
            case SHARPSHOOTER_MODE:
                idle_power_cost = DEFAULT_CHARGE_DRAIN * 0.6f;
                mod.wearer.add_traits(Dm.list(TRAIT_NO_GUN_AKIMBO, TRAIT_NICE_SHOT), REF(this));
                RegisterSignal(mod.wearer, COMSIG_MOB_FIRED_GUN, nameof(sharpshooter_fired_gun));
                RegisterSignal(mod.wearer, COMSIG_PROJECTILE_FIRER_BEFORE_FIRE, nameof(apply_ricochet));
                await mod.wearer.add_movespeed_modifier(typeof(datum_movespeed_modifier_shooting_assistant));
                break;
        }
    }

    // tg: code/modules/mod/modules/modules_security.dm:529
    public virtual void remove_mode_effects()
    {
        switch (selected_mode)
        {
            case STORMTROOPER_MODE:
                UnregisterSignal(mod.wearer, COMSIG_MOB_FIRED_GUN);
                mod.wearer.remove_traits(Dm.list(TRAIT_NO_GUN_AKIMBO, TRAIT_DOUBLE_TAP), REF(this));
                break;
            case SHARPSHOOTER_MODE:
                UnregisterSignal(mod.wearer, Dm.list(COMSIG_MOB_FIRED_GUN, COMSIG_PROJECTILE_FIRER_BEFORE_FIRE));
                mod.wearer.remove_traits(Dm.list(TRAIT_NO_GUN_AKIMBO, TRAIT_NICE_SHOT), REF(this));
                mod.wearer.remove_movespeed_modifier(typeof(datum_movespeed_modifier_shooting_assistant));
                break;
        }
    }

    // tg: code/modules/mod/modules/modules_security.dm:539
    public override async DmTask<object> drain_power(object amount = null)
    {
        object dot = await base.drain_power(amount);
        if (!Dm.truthy(dot))
            await set_shooting_mode(SHOOTING_ASSISTANT_OFF);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_security.dm:544
    public override async DmTask on_part_activation()
    {
        await apply_mode_effects();
    }

    // tg: code/modules/mod/modules/modules_security.dm:547
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        remove_mode_effects();
    }

    // tg: code/modules/mod/modules/modules_security.dm:550
    [SignalHandler]
    public virtual void stormtrooper_fired_gun(mob user = null, obj_item_gun gun_fired = null, object target = null, object @params = null, object zone_override = null, DmList bonus_spread_values = null)
    {
        Dm.append_at(bonus_spread_values, MIN_BONUS_SPREAD_INDEX, 15);
        Dm.append_at(bonus_spread_values, MAX_BONUS_SPREAD_INDEX, 25);
    }

    // tg: code/modules/mod/modules/modules_security.dm:555
    [SignalHandler]
    public virtual void sharpshooter_fired_gun(mob user = null, obj_item_gun gun_fired = null, object target = null, object @params = null, object zone_override = null, DmList bonus_spread_values = null)
    {
        Dm.remove_at(bonus_spread_values, MIN_BONUS_SPREAD_INDEX, 20);
        Dm.remove_at(bonus_spread_values, MAX_BONUS_SPREAD_INDEX, 10);
    }

    // tg: code/modules/mod/modules/modules_security.dm:560
    [SignalHandler]
    public virtual void apply_ricochet(mob user = null, obj_projectile projectile = null, datum fired_from = null, atom clicked_atom = null)
    {
        projectile.ricochets_max += 1;
        projectile.min_ricochets += 1;
        projectile.ricochet_incidence_leeway = 0; //allows the projectile to bounce at any angle.
        projectile.accuracy_falloff = 0;
    }
}

// tg: code/modules/mod/modules/modules_security.dm:571
public partial class obj_item_mod_module_shove_blocker : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_security.dm:579
    public override async DmTask on_part_activation()
    {
        mod.wearer.add_traits(Dm.list(TRAIT_BRAWLING_KNOCKDOWN_BLOCKED, TRAIT_NO_STAGGER, TRAIT_NO_THROW_HITPUSH), REF(this));
    }

    // tg: code/modules/mod/modules/modules_security.dm:582
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        mod.wearer.remove_traits(Dm.list(TRAIT_BRAWLING_KNOCKDOWN_BLOCKED, TRAIT_NO_STAGGER, TRAIT_NO_THROW_HITPUSH), REF(this));
    }
}

// tg: code/modules/mod/modules/modules_security.dm:591
public partial class obj_item_mod_module_quick_cuff : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_security.dm:598
    public override async DmTask on_part_activation()
    {
        await base.on_part_activation();
        ADD_TRAIT(mod.wearer, TRAIT_FAST_CUFFING, REF(this));
    }

    // tg: code/modules/mod/modules/modules_security.dm:602
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_part_deactivation(deleting);
        REMOVE_TRAIT(mod.wearer, TRAIT_FAST_CUFFING, REF(this));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mod/modules/modules_security.dm:16  var/static/list/guns_typecache
    internal DmList obj_item_mod_module_magnetic_harness__guns_typecache;
    // tg: code/modules/mod/modules/modules_security.dm:480  var/static/list/available_modes
    internal DmList obj_item_mod_module_shooting_assistant__available_modes;
}
