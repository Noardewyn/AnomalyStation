// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/porta_turret/portable_turret.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4d, D316: the toolbox sentry gun with the portable turrets; in_faction and the guns'
// get_turret_properties were the gaps package C1's). Left out: the blob branch of process (GLOB.blobs: the blob antagonist is not
// ported, no blob exists), the syndicate turret's shuttle navigation icon (shuttles, M10) and its station map blip (the minimap is
// not ported). The var blocks are generated (D111).

// tg: code/game/machinery/porta_turret/portable_turret.dm:27
public partial class obj_machinery_porta_turret : obj_machinery
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:1-14 (file-local defines)
    public const float TURRET_STUN = 0;
    public const float TURRET_LETHAL = 1;

    public const float POPUP_ANIM_TIME = 5;
    public const float POPDOWN_ANIM_TIME = 5; //Be sure to change the icon animation at the same time or it'll look bad

    public const int TURRET_FLAG_SHOOT_ALL_REACT = 1 << 0; // The turret gets pissed off and shoots at people nearby (unless they have sec access!)
    public const int TURRET_FLAG_AUTH_WEAPONS = 1 << 1; // Checks if it can shoot people that have a weapon they aren't authorized to have
    public const int TURRET_FLAG_SHOOT_CRIMINALS = 1 << 2; // Checks if it can shoot people that are wanted
    public const int TURRET_FLAG_SHOOT_ALL = 1 << 3; // The turret gets pissed off and shoots at people nearby (unless they have sec access!)
    public const int TURRET_FLAG_SHOOT_ANOMALOUS = 1 << 4; // Checks if it can shoot at unidentified lifeforms (ie xenos)
    public const int TURRET_FLAG_SHOOT_UNSHIELDED = 1 << 5; // Checks if it can shoot people that aren't mindshielded and who arent heads
    public const int TURRET_FLAG_SHOOT_BORGS = 1 << 6; // checks if it can shoot cyborgs
    public const int TURRET_FLAG_SHOOT_HEADS = 1 << 7; // checks if it can shoot at heads of staff

    // tg: code/game/machinery/porta_turret/portable_turret.dm:122
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(@base))
            @base = this;
        update_appearance();
        // Sets up a spark system
        spark_system = Dm.@new<datum_effect_system_basic_spark_spread>(this, 5, FALSE);
        spark_system.attach(this);
        tracker = Dm.@new<datum_proximity_monitor_advanced_turret_tracking>(this, scan_range);
        tracker.recalculate_field(full_recalc: TRUE); // manually call this so that our tracker var is set before we set anything up
        Waiting.NoSleep(setup());
        if (Dm.truthy(has_cover))
        {
            cover = Dm.@new<obj_machinery_porta_turret_cover>(loc);
            cover.parent_turret = this;
            mutable_appearance base_underlay = (mutable_appearance)mutable_appearance("icons/obj/weapons/turrets.dmi", "basedark");
            base_underlay.layer = NOT_HIGH_OBJ_LAYER;
            Dm.append(underlays, base_underlay);
        }
        if (!Dm.truthy(has_cover))
            popUp(); // INVOKE_ASYNC: popUp cannot sleep

        Waiting.NoSleep(AddElement(typeof(datum_element_hostile_machine)));
        return dot;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:144
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref tracker);
        //deletes its own cover with it
        QDEL_NULL(ref cover);
        @base = null;
        QDEL_NULL(ref stored_gun);
        QDEL_NULL(ref spark_system);
        Waiting.NoSleep(remove_control());
        return base.Destroy(force);
    }

    ///Toggles the turret on or off depending on the value of the turn_on arg.
    // tg: code/game/machinery/porta_turret/portable_turret.dm:155
    public virtual void toggle_on(object turn_on = null)
    {
        turn_on ??= TRUE;
        if (Dm.eq(on, turn_on))
            return;
        // COOLDOWN_FINISHED(src, disabled_time) (__DEFINES/cooldowns.dm:113), inline
        if (Dm.truthy(turn_on) && !(disabled_time <= world.time))
            return;
        on = Dm.num(turn_on);
        check_should_process();
        if (!Dm.truthy(on))
            popDown();
    }

    ///Prevents turned from being turned on for a duration, then restarts them after that if the second ard is true.
    // tg: code/game/machinery/porta_turret/portable_turret.dm:166
    public virtual void set_disabled(object duration = null, object will_restart = null)
    {
        duration ??= 6 * SECONDS;
        will_restart ??= on;
        // COOLDOWN_START(src, disabled_time, duration) (__DEFINES/cooldowns.dm:110), inline
        disabled_time = world.time + Dm.num(duration);
        if (Dm.truthy(will_restart))
            _addtimer(Dm.@new<datum_callback>(this, nameof(toggle_on), TRUE), Dm.num(duration) + 1, file: "code/game/machinery/porta_turret/portable_turret.dm", line: 169); //the cooldown isn't over until the tick after its end.
        toggle_on(FALSE);
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:172
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        await base.on_saboteur(source, disrupt_duration);
        set_disabled(disrupt_duration); // INVOKE_ASYNC: set_disabled cannot sleep
        return TRUE;
    }

    /// Checks to see if this should be processing, and starts/stops processing if so.
    /// Returns TRUE if processing began, FALSE if processing ended, or null if the processing state was not changed.
    // tg: code/game/machinery/porta_turret/portable_turret.dm:179
    public virtual object check_should_process()
    {
        if (Dm.truthy(Dm.bitand(datum_flags, DF_ISPROCESSING)))
        {
            if (!Dm.truthy(on) || !Dm.truthy(anchored) || !Dm.truthy(LAZYLEN(tracker.tracking)) || Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || !Dm.truthy(powered__proc()))
            {
                end_processing();
                return FALSE;
            }
        }
        else
        {
            if (Dm.truthy(on) && Dm.truthy(anchored) && Dm.truthy(LAZYLEN(tracker.tracking)) && !Dm.truthy(Dm.bitand(machine_stat, BROKEN)) && Dm.truthy(powered__proc()))
            {
                begin_processing();
                return TRUE;
            }
        }
        return null;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:190
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(anchored))
        {
            icon_state = "turretCover";
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            icon_state = Dm.text("[]_broken", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        if (!Dm.truthy(powered__proc()))
        {
            icon_state = Dm.text("[]_off", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        if (!Dm.truthy(on) || !Dm.truthy(raised))
        {
            icon_state = Dm.text("[]_off", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        if (Dm.eq(mode, TURRET_STUN))
            icon_state = Dm.text("[]_stun", base_icon_state);
        else if (Dm.eq(mode, TURRET_LETHAL))
            icon_state = Dm.text("[]_lethal", base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:210
    public virtual async DmTask<object> setup(obj_item_gun turret_gun = null)
    {
        if (Dm.truthy(stored_gun))
        {
            qdel(stored_gun);
            stored_gun = null;
        }

        if (Dm.truthy(installation) && !Dm.truthy(turret_gun))
            stored_gun = Dm.@new(installation, this) as obj_item_gun;
        else if (Dm.truthy(turret_gun))
        {
            await turret_gun.forceMove(this);
            stored_gun = turret_gun;
        }

        RegisterSignal(stored_gun, COMSIG_QDELETING, nameof(null_gun));
        DmList gun_properties = stored_gun.get_turret_properties() as DmList;

        //required properties
        stun_projectile = gun_properties["stun_projectile"] as Type;
        stun_projectile_sound = gun_properties["stun_projectile_sound"];
        lethal_projectile = gun_properties["lethal_projectile"] as Type;
        lethal_projectile_sound = gun_properties["lethal_projectile_sound"];
        base_icon_state = gun_properties["base_icon_state"] as string;

        //optional properties
        if (Dm.truthy(gun_properties["shot_delay"]))
            shot_delay = Dm.num(gun_properties["shot_delay"]);
        if (Dm.truthy(gun_properties["reqpower"]))
            reqpower = Dm.num(gun_properties["reqpower"]);

        update_appearance();
        return gun_properties;
    }

    ///destroys reference to stored_gun to prevent hard deletions
    // tg: code/game/machinery/porta_turret/portable_turret.dm:241
    [SignalHandler]
    public virtual void null_gun()
    {
        stored_gun = null;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:245
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "PortableTurret", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:251
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list(
            Dm.kv("locked", locked),
            Dm.kv("on", on),
            Dm.kv("check_weapons", Dm.bitand(turret_flags, TURRET_FLAG_AUTH_WEAPONS)),
            Dm.kv("neutralize_criminals", Dm.bitand(turret_flags, TURRET_FLAG_SHOOT_CRIMINALS)),
            Dm.kv("neutralize_all", Dm.bitand(turret_flags, TURRET_FLAG_SHOOT_ALL)),
            Dm.kv("neutralize_unidentified", Dm.bitand(turret_flags, TURRET_FLAG_SHOOT_ANOMALOUS)),
            Dm.kv("neutralize_nonmindshielded", Dm.bitand(turret_flags, TURRET_FLAG_SHOOT_UNSHIELDED)),
            Dm.kv("neutralize_cyborgs", Dm.bitand(turret_flags, TURRET_FLAG_SHOOT_BORGS)),
            Dm.kv("neutralize_heads", Dm.bitand(turret_flags, TURRET_FLAG_SHOOT_HEADS)),
            Dm.kv("manual_control", manual_control),
            Dm.kv("silicon_user", FALSE),
            Dm.kv("allow_manual_control", FALSE),
            Dm.kv("lasertag_turret", istype(this, typeof(obj_machinery_porta_turret_lasertag)) ? TRUE : FALSE));
        if (issilicon(user))
        {
            data["silicon_user"] = TRUE;
            if (!Dm.truthy(manual_control))
            {
                mob_living_silicon S = user as mob_living_silicon;
                if (Dm.truthy(S.hack_software))
                    data["allow_manual_control"] = TRUE;
            }
        }
        return data;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:275
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action as string)
        {
            case "power":
                if (Dm.truthy(anchored))
                {
                    toggle_on(Dm.truthy(on) ? FALSE : TRUE);
                    return TRUE;
                }
                else
                    to_chat(usr, span_warning(I18n.T("It has to be secured first!")));
                break;
            case "authweapon":
                Dm.bitxor_assign(ref turret_flags, TURRET_FLAG_AUTH_WEAPONS);
                return TRUE;
            case "shootcriminals":
                Dm.bitxor_assign(ref turret_flags, TURRET_FLAG_SHOOT_CRIMINALS);
                return TRUE;
            case "shootall":
                Dm.bitxor_assign(ref turret_flags, TURRET_FLAG_SHOOT_ALL);
                return TRUE;
            case "checkxenos":
                Dm.bitxor_assign(ref turret_flags, TURRET_FLAG_SHOOT_ANOMALOUS);
                return TRUE;
            case "checkloyal":
                Dm.bitxor_assign(ref turret_flags, TURRET_FLAG_SHOOT_UNSHIELDED);
                return TRUE;
            case "shootborgs":
                Dm.bitxor_assign(ref turret_flags, TURRET_FLAG_SHOOT_BORGS);
                return TRUE;
            case "shootheads":
                Dm.bitxor_assign(ref turret_flags, TURRET_FLAG_SHOOT_HEADS);
                return TRUE;
            case "manual":
                if (!issilicon(usr))
                    return dot;
                await give_control(usr as mob);
                return TRUE;
        }
        return dot;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:314
    public override object ui_host(mob user = null)
    {
        if (Dm.truthy(has_cover) && Dm.truthy(cover))
            return cover;
        if (Dm.truthy(@base))
            return @base;
        return this;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:321
    public override object power_change()
    {
        object dot = base.power_change();
        if (!Dm.truthy(anchored) || Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || !Dm.truthy(powered__proc()))
        {
            update_appearance();
            Waiting.NoSleep(remove_control());
        }
        check_should_process();
        return dot;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:328
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = NONE;
        if (Dm.truthy(locked))
            return dot;

        ((obj_item_multitool)tool).set_buffer(this);
        balloon_alert(user, I18n.T("saved to multitool buffer"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:337
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return NONE;

        //If the turret is destroyed, you can remove it with a crowbar to
        //try and salvage its components
        to_chat(user, span_notice(I18n.T("You begin prying the metal coverings off...")));
        if (!Dm.truthy(await tool.use_tool(this, user, 20)))
            return ITEM_INTERACT_BLOCKING;
        if (prob(70))
        {
            if (Dm.truthy(stored_gun))
            {
                await stored_gun.forceMove(loc);
                stored_gun = null;
            }
            to_chat(user, span_notice(I18n.T("You remove the turret and salvage some components.")));
            if (prob(50))
                Dm.@new<obj_item_stack_sheet_iron>(loc, rand(1, 4));
            if (prob(50))
                Dm.@new<obj_item_assembly_prox_sensor>(loc);
        }
        else
            to_chat(user, span_notice(I18n.T("You remove the turret but did not manage to salvage anything.")));
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:360
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(on) || Dm.truthy(raised))
            return NONE;

        //This code handles moving the turret around. After all, it's a portable turret!
        if (!Dm.truthy(anchored) && !Dm.truthy(isinspace()))
        {
            await set_anchored(TRUE);
            RemoveInvisibility(id: type);
            update_appearance();
            to_chat(user, span_notice(I18n.T("You secure the exterior bolts on the turret.")));
            if (Dm.truthy(has_cover))
            {
                cover = Dm.@new<obj_machinery_porta_turret_cover>(loc); //create a new turret. While this is handled in process(), this is to workaround a bug where the turret becomes invisible for a split second
                cover.parent_turret = this; //make the cover's parent src
            }
        }
        else if (Dm.truthy(anchored))
        {
            await set_anchored(FALSE);
            to_chat(user, span_notice(I18n.T("You unsecure the exterior bolts on the turret.")));
            power_change();
            SetInvisibility(INVISIBILITY_NONE, id: type);
            qdel(cover); //deletes the cover, and the turret instance itself becomes its own cover.
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:381
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(tool.GetID()))
            return NONE;

        //Behavior lock/unlock mangement
        if (!Dm.truthy(await allowed__proc(user)))
        {
            to_chat(user, span_alert(I18n.T("Access denied.")));
            return ITEM_INTERACT_BLOCKING;
        }
        locked = Dm.truthy(locked) ? FALSE : TRUE;
        to_chat(user, span_notice(I18n.T("Controls are now [].", Dm.truthy(locked) ? "locked" : "unlocked")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:393
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        balloon_alert(user, I18n.T("threat assessment circuits shorted"));
        audible_message(span_hear(I18n.T("[] hums oddly...", this)));
        Dm.combine(ref obj_flags, EMAGGED);
        controllock = TRUE;
        set_disabled(6 * SECONDS);
        update_appearance();
        //turns it back on. The cover popUp() popDown() are automatically called in process(), no need to define it here
        return TRUE;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:405
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (Dm.truthy(on))
        {
            //if the turret is on, the EMP no matter how severe disables the turret for a while
            //and scrambles its settings, with a slight chance of having an emag effect
            if (prob(50))
                Dm.combine(ref turret_flags, TURRET_FLAG_SHOOT_CRIMINALS);
            if (prob(50))
                Dm.combine(ref turret_flags, TURRET_FLAG_AUTH_WEAPONS);
            if (prob(20))
                Dm.combine(ref turret_flags, TURRET_FLAG_SHOOT_ALL); // Shooting everyone is a pretty big deal, so it's least likely to get turned on

            set_disabled(rand(6 * SECONDS, 20 * SECONDS));
            await remove_control();
        }
        return dot;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:422
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.truthy(dot) && Dm.num(atom_integrity) > 0) //damage received
        {
            if (prob(30))
                await spark_system.start();
            if (Dm.truthy(on) && !Dm.truthy(Dm.bitand(turret_flags, TURRET_FLAG_SHOOT_ALL_REACT)) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            {
                Dm.combine(ref turret_flags, TURRET_FLAG_SHOOT_ALL_REACT);
                _addtimer(Dm.@new<datum_callback>(this, nameof(reset_attacked)), 6 * SECONDS, file: "code/game/machinery/porta_turret/portable_turret.dm", line: 429);
            }
        }
        return dot;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:431
    public virtual void reset_attacked()
    {
        Dm.mask(ref turret_flags, ~TURRET_FLAG_SHOOT_ALL_REACT);
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:434
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (Dm.truthy(dot))
        {
            power_change();
            SetInvisibility(INVISIBILITY_NONE, id: type);
            await spark_system.start(); //creates some sparks because they look cool
            has_cover = FALSE;
            qdel(cover); //deletes the cover - no need on keeping it there!
        }
        return dot;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:443
    public override async DmTask atom_fix()
    {
        await set_machine_stat(Dm.bitand(machine_stat, ~BROKEN));
        has_cover = Dm.initial(this, (obj_machinery_porta_turret p) => p.has_cover);
        check_should_process();
        await base.atom_fix();
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:450
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        //the main machinery process
        if (Dm.truthy(has_cover) && cover == null && Dm.truthy(anchored) && !Dm.truthy(Dm.bitand(machine_stat, BROKEN))) //if it has no cover and is anchored
        {
            cover = Dm.@new<obj_machinery_porta_turret_cover>(loc); //if the turret has no cover and is anchored, give it a cover
            cover.parent_turret = this; //assign the cover its parent_turret, which would be this (src)
            if (Dm.truthy(raised))
                cover.icon_state = "openTurretCover";
        }

        if (!Dm.truthy(on) || Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return PROCESS_KILL;

        if (Dm.truthy(manual_control))
            return PROCESS_KILL;

        if (Dm.truthy(uses_stored) && !Dm.truthy(stored_gun))
            return PROCESS_KILL;

        DmList targets = Dm.list();
        foreach (mob A in Dm.each<mob>(view(scan_range, @base)))
        {
            if (A.invisibility > SEE_INVISIBLE_LIVING)
                continue;

            if (Dm.truthy(Dm.bitand(turret_flags, TURRET_FLAG_SHOOT_ANOMALOUS))) //if it's set to check for simple animals
            {
                if (isanimal_or_basicmob(A))
                {
                    mob_living animal = A as mob_living;
                    if (IS_UNCONSCIOUS_OR_CRIT(animal) || Dm.truthy(in_faction(animal))) //don't target if dead or in faction
                        continue;
                    Dm.append(targets, animal);
                    continue;
                }
            }

            if (issilicon(A))
            {
                if (!Dm.truthy(Dm.bitand(turret_flags, TURRET_FLAG_SHOOT_BORGS)))
                    continue;

                if (ispAI(A))
                    continue;

                if (iscyborg(A))
                {
                    mob_living_silicon_robot sillyconerobot = A as mob_living_silicon_robot;
                    if (IS_UNCONSCIOUS_OR_CRIT(sillyconerobot))
                        continue;
                    if (Dm.truthy(in_faction(sillyconerobot))) // borgs in faction are friendly
                        continue;
                    if (Dm.truthy(has_faction(ROLE_SYNDICATE)) && Dm.truthy(sillyconerobot.emagged)) // special case: emagged station borgs are friendly to syndicate turrets
                        continue;

                    Dm.append(targets, sillyconerobot);
                }
            }
            else if (iscarbon(A))
            {
                mob_living_carbon C = A as mob_living_carbon;
                //If not emagged, only target carbons that can use items
                if (Dm.eq(mode, TURRET_STUN))
                {
                    if (!Dm.truthy(Dm.bitand(C.mobility_flags, MOBILITY_USE)))
                        continue;
                    if (HAS_TRAIT(C, TRAIT_INCAPACITATED))
                        continue;
                }
                //If emagged, target all but dead carbons
                else if (Dm.eq(mode, TURRET_LETHAL))
                {
                    if (Dm.eq(C.stat, DEAD))
                        continue;
                }

                //if the target is a human and not in our faction, analyze threat level
                if (ishuman(C) && !Dm.truthy(in_faction(C)))
                {
                    if (Dm.num(Waiting.NoSleep(assess_perp(C as mob_living_carbon_human))) >= 4)
                        Dm.append(targets, C);
                }
                else if (Dm.truthy(Dm.bitand(turret_flags, TURRET_FLAG_SHOOT_ANOMALOUS))) //non humans who are not simple animals (xenos etc)
                {
                    if (!Dm.truthy(in_faction(C)))
                        Dm.append(targets, C);
                }
            }
        }

        foreach (object A in Dm.each(GLOB.mechas_list))
        {
            if (get_dist(A, @base) < scan_range && Dm.truthy(can_see(@base, A as atom, scan_range)))
            {
                obj_vehicle_sealed_mecha mech = A as obj_vehicle_sealed_mecha;
                foreach (object O in Dm.each(mech.occupants))
                {
                    mob_living occupant = O as mob_living;
                    if (!Dm.truthy(in_faction(occupant))) //If there is a user and they're not in our faction
                    {
                        // assess_perp reads a human: a mech's other pilots (not ported) reach it as null
                        if (Dm.num(Waiting.NoSleep(assess_perp(occupant as mob_living_carbon_human))) >= 4)
                            Dm.append(targets, mech);
                    }
                }
            }
        }

        // if((turret_flags & TURRET_FLAG_SHOOT_ANOMALOUS) && GLOB.blobs.len && (mode == TURRET_LETHAL)) targets += every blob in view:
        // the blob antagonist is not ported, so GLOB.blobs (overmind.dm:2) is not declared and no blob exists to target

        if (targets.len > 0)
            Waiting.NoSleep(tryToShootAt(targets));
        else if (!Dm.truthy(always_up))
            popDown(); // no valid targets, close the cover
        return null;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:541
    public virtual async DmTask<object> tryToShootAt(DmList targets = null)
    {
        while (targets.len > 0)
        {
            atom_movable M = pick(targets) as atom_movable;
            Dm.remove(targets, M);
            if (Dm.truthy(await target(M)))
                return 1;
        }
        return null;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:548
    public virtual void popUp() //pops the turret up
    {
        if (!Dm.truthy(anchored))
            return;
        if (Dm.truthy(raising) || Dm.truthy(raised))
            return;
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return;
        SetInvisibility(INVISIBILITY_NONE, id: type);
        raising = 1;
        if (Dm.truthy(cover))
            flick("popup", cover);
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_popup)), POPUP_ANIM_TIME, Dm.bitor(TIMER_STOPPABLE, TIMER_DELETE_ME), file: "code/game/machinery/porta_turret/portable_turret.dm", line: 559);
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:561
    public virtual void finish_popup()
    {
        raising = 0;
        if (Dm.truthy(cover))
            cover.icon_state = "openTurretCover";
        raised = 1;
        layer = MOB_LAYER;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:568
    public virtual void popDown() //pops the turret down
    {
        if (Dm.truthy(raising) || !Dm.truthy(raised))
            return;
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return;
        layer = OBJ_LAYER;
        raising = 1;
        if (Dm.truthy(cover))
            flick("popdown", cover);
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_popdown)), POPDOWN_ANIM_TIME, Dm.bitor(TIMER_STOPPABLE, TIMER_DELETE_ME), file: "code/game/machinery/porta_turret/portable_turret.dm", line: 577);
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:579
    public virtual void finish_popdown()
    {
        raising = 0;
        if (Dm.truthy(cover))
            cover.icon_state = "turretCover";
        raised = 0;
        SetInvisibility(2, id: type);
        update_appearance();
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:587
    public virtual async DmTask<object> assess_perp(mob_living_carbon_human perp = null)
    {
        float threatcount = 0; //the integer returned

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return 10; //if emagged, always return 10.

        if (Dm.truthy(Dm.bitand(turret_flags, TURRET_FLAG_SHOOT_ALL | TURRET_FLAG_SHOOT_ALL_REACT)) && !Dm.truthy(await allowed__proc(perp)))
        {
            //if the turret has been attacked or is angry, target all non-sec people
            if (!Dm.truthy(await allowed__proc(perp)))
                return 10;
        }

        // If we aren't shooting heads then return a threatcount of 0
        if (!Dm.truthy(Dm.bitand(turret_flags, TURRET_FLAG_SHOOT_HEADS)))
        {
            datum_job apparent_job = SSjob.get_job(perp.get_assignment()) as datum_job;
            if (Dm.truthy(Dm.bitand(apparent_job?.job_flags, JOB_HEAD_OF_STAFF)))
                return 0;
        }

        if (Dm.truthy(Dm.bitand(turret_flags, TURRET_FLAG_AUTH_WEAPONS))) //check for weapon authorization
        {
            if (!istype((perp.wear_id as obj_item)?.GetID(), typeof(obj_item_card_id_advanced_chameleon)))
            {
                if (Dm.truthy(await allowed__proc(perp))) //if the perp has security access, return 0
                    return 0;
                if (Dm.truthy(perp.is_holding_item_of_type(typeof(obj_item_gun))) || Dm.truthy(perp.is_holding_item_of_type(typeof(obj_item_melee_baton))))
                    threatcount += 4;

                if (istype(perp.belt, typeof(obj_item_gun)) || istype(perp.belt, typeof(obj_item_melee_baton)))
                    threatcount += 2;
            }
        }

        if (Dm.truthy(Dm.bitand(turret_flags, TURRET_FLAG_SHOOT_CRIMINALS))) //if the turret can check the records, check if they are set to *Arrest* on records
        {
            object perpname = perp.get_face_name(perp.get_id_name() as string);
            datum_record_crew target = find_record(perpname) as datum_record_crew;
            if (target == null || Dm.eq(target.wanted_status, WANTED_ARREST))
                threatcount += 4;
        }

        if (Dm.truthy(Dm.bitand(turret_flags, TURRET_FLAG_SHOOT_UNSHIELDED)) && !HAS_TRAIT(perp, TRAIT_MINDSHIELD))
            threatcount += 4;

        return threatcount;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:626
    public virtual object in_faction(mob target = null)
    {
        return faction_check_atom(target);
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:629
    public virtual async DmTask<object> target(atom_movable target = null)
    {
        if (Dm.truthy(target))
        {
            popUp(); //pop the turret up if it's not already up.
            await setDir(get_dir(@base, target)); //even if you can't shoot, follow the target
            await shootAt(target);
            return 1;
        }
        return null;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:637
    public virtual async DmTask<object> shootAt(atom_movable target = null)
    {
        if (!Dm.truthy(raised)) //the turret has to be raised in order to fire - makes sense, right?
            return null;

        if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED))) //if it hasn't been emagged, cooldown before shooting again
        {
            if (last_fired + shot_delay > world.time)
                return null;
            last_fired = world.time;
        }

        turf T = get_turf(this) as turf;
        turf U = get_turf(target) as turf;
        if (T == null || U == null)
            return null;

        //Wall turrets will try to find adjacent empty turf to shoot from to cover full arc
        if (Dm.truthy(T.density))
        {
            if (Dm.truthy(wall_turret_direction))
            {
                turf closer = get_step(T, Dm.num(wall_turret_direction));
                if (closer != null && !Dm.truthy(await closer.is_blocked_turf()) && Dm.truthy(await T.Adjacent(closer)))
                    T = closer;
            }
            else
            {
                float target_dir = get_dir(T, target);
                foreach (float d in new float[] { 0, -45, 45 })
                {
                    turf closer = get_step(T, turn(target_dir, d));
                    if (closer != null && !Dm.truthy(await closer.is_blocked_turf()) && Dm.truthy(await T.Adjacent(closer)))
                    {
                        T = closer;
                        break;
                    }
                }
            }
        }

        update_appearance();
        obj_projectile A;
        //any emagged turrets drains 2x power and uses a different projectile?
        if (Dm.eq(mode, TURRET_STUN))
        {
            await use_energy(reqpower);
            A = Dm.@new(stun_projectile, T) as obj_projectile;
            playsound(loc, stun_projectile_sound, 75, TRUE);
        }
        else
        {
            await use_energy(reqpower * 2);
            A = Dm.@new(lethal_projectile, T) as obj_projectile;
            playsound(loc, lethal_projectile_sound, 75, TRUE);
        }

        //Shooting Code:
        await A.aim_projectile(target, T);
        A.firer = this;
        A.fired_from = this;
        if (Dm.truthy(ignore_faction))
        {
            // APPLY_FACTION_AND_ALLIES_FROM(A, src) (__DEFINES/mobfactions.dm:135), inline
            A.add_faction(get_faction());
            A.add_ally(LAZYLISTDUPLICATE(allies));
        }
        await A.fire();
        return A;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:687
    public virtual void setState(object on = null, object mode = null, object shoot_cyborgs = null)
    {
        if (Dm.truthy(controllock))
            return;

        if (Dm.truthy(shoot_cyborgs))
            Dm.combine(ref turret_flags, TURRET_FLAG_SHOOT_BORGS);
        else
            Dm.mask(ref turret_flags, ~TURRET_FLAG_SHOOT_BORGS);
        toggle_on(on);
        this.mode = Dm.num_or_null(mode);
        power_change();
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:724
    public virtual async DmTask<object> give_control(mob A = null)
    {
        if (Dm.truthy(manual_control) || !Dm.truthy(await can_interact(A)))
            return FALSE;
        remote_controller = A;
        if (!Dm.truthy(quit_action))
            quit_action = Dm.@new<datum_action_turret_quit>(this);
        await quit_action.Grant(remote_controller);
        if (!Dm.truthy(toggle_action))
            toggle_action = Dm.@new<datum_action_turret_toggle>(this);
        await toggle_action.Grant(remote_controller);
        await remote_controller.reset_perspective(this);
        remote_controller.click_intercept = this;
        manual_control = TRUE;
        always_up = TRUE;
        popUp();
        return TRUE;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:741
    public virtual async DmTask<object> remove_control(object warning_message = null)
    {
        warning_message ??= TRUE;
        if (!Dm.truthy(manual_control))
            return FALSE;
        if (Dm.truthy(remote_controller))
        {
            if (Dm.truthy(warning_message))
                to_chat(remote_controller, span_warning(I18n.T("Your uplink to [] has been severed!", this)));
            await quit_action.Remove(remote_controller);
            await toggle_action.Remove(remote_controller);
            remote_controller.click_intercept = null;
            await remote_controller.reset_perspective();
        }
        always_up = Dm.initial(this, (obj_machinery_porta_turret p) => p.always_up);
        manual_control = FALSE;
        remote_controller = null;
        return TRUE;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:756
    public virtual async DmTask<object> InterceptClickOn(mob_living clicker = null, object @params = null, atom A = null)
    {
        if (!Dm.truthy(manual_control))
            return FALSE;
        if (!Dm.truthy(await can_interact(clicker)))
        {
            await remove_control();
            return FALSE;
        }
        log_combat(clicker, A, "fired with manual turret control at");
        await target(A as atom_movable);
        return TRUE;
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:696
public partial class datum_action_turret_toggle : datum_action
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:701
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        obj_machinery_porta_turret P = target as obj_machinery_porta_turret;
        if (P == null)
            return dot;
        P.setState(P.on, Dm.truthy(P.mode) ? FALSE : TRUE);
        return dot;
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:710
public partial class datum_action_turret_quit : datum_action
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:715
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        obj_machinery_porta_turret P = target as obj_machinery_porta_turret;
        if (P == null)
            return dot;
        await P.remove_control(FALSE);
        return dot;
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:766
public partial class obj_machinery_porta_turret_syndicate : obj_machinery_porta_turret
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:785
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), Dm.bitor(EMP_PROTECT_SELF, EMP_PROTECT_WIRES)));
        Waiting.NoSleep(AddElement(typeof(datum_element_nav_computer_icon), Dm.resource("icons/effects/nav_computer_indicators.dmi"), "turret", FALSE));
        // add_minimap_blip(src, MINIMAP_SYNDIE_TURRET_BLIP, "sentry_passive"): the minimap is not ported, so no blip is made
        return dot;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:791
    public virtual void update_turret_minimap_icon(object new_icon_state = null)
    {
        // get_minimap_blip(MINIMAP_SYNDIE_TURRET_BLIP, src): the minimap is not ported, so the turret has no blip (isnull(blip))
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:797
    public override async DmTask<object> shootAt(atom_movable target = null)
    {
        object dot = await base.shootAt(target);
        if (Dm.truthy(raised) && (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) || last_fired == world.time))
        {
            update_turret_minimap_icon("sentry_firing");
            _addtimer(Dm.@new<datum_callback>(this, nameof(update_turret_minimap_icon), "sentry_passive"), shot_delay + 1 * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/game/machinery/porta_turret/portable_turret.dm", line: 801);
        }
        return dot;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:803
    public override async DmTask<object> setup(obj_item_gun turret_gun = null)
    {
        return null;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:806
    public override async DmTask<object> assess_perp(mob_living_carbon_human perp = null)
    {
        return 10; //Syndicate turrets shoot everything not in their faction
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:819
public partial class obj_machinery_porta_turret_syndicate_energy_ruin : obj_machinery_porta_turret_syndicate_energy
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:819
    public override async DmTask<object> assess_perp(mob_living_carbon_human perp = null)
    {
        if (!Dm.truthy(check_access((perp.wear_id as obj_item)?.GetID() as obj_item)))
            return 10;
        return 0;
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:861
public partial class obj_machinery_porta_turret_syndicate_shuttle : obj_machinery_porta_turret_syndicate
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:879
    public override async DmTask<object> target(atom_movable target = null)
    {
        if (Dm.truthy(target))
        {
            await setDir(get_dir(@base, target)); //even if you can't shoot, follow the target
            await shootAt(target);
            _addtimer(Dm.@new<datum_callback>(this, nameof(shootAt), target), 0.5f * SECONDS, file: "code/game/machinery/porta_turret/portable_turret.dm", line: 883);
            _addtimer(Dm.@new<datum_callback>(this, nameof(shootAt), target), 1 * SECONDS, file: "code/game/machinery/porta_turret/portable_turret.dm", line: 884);
            _addtimer(Dm.@new<datum_callback>(this, nameof(shootAt), target), 1.5f * SECONDS, file: "code/game/machinery/porta_turret/portable_turret.dm", line: 885);
            return TRUE;
        }
        return null;
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:888
public partial class obj_machinery_porta_turret_ai : obj_machinery_porta_turret
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:892
    public override async DmTask<object> assess_perp(mob_living_carbon_human perp = null)
    {
        return 10; //AI turrets shoot at everything not in their faction
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:895
public partial class obj_machinery_porta_turret_aux_base : obj_machinery_porta_turret
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:905
    public override async DmTask<object> assess_perp(mob_living_carbon_human perp = null)
    {
        return 0; //Never shoot humanoids. You are on your own if Ashwalkers or the like attack!
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:908
    public override async DmTask<object> setup(obj_item_gun turret_gun = null)
    {
        return null;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:911
    public override async DmTask<object> interact(mob user = null, params object[] __extra) //Controlled solely from the base console.
    {
        return null;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:914
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cover.name = name;
        cover.desc = desc;
        return dot;
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:919
public partial class obj_machinery_porta_turret_centcom_shuttle : obj_machinery_porta_turret
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:935
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), Dm.bitor(EMP_PROTECT_SELF, EMP_PROTECT_WIRES)));
        return dot;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:939
    public override async DmTask<object> assess_perp(mob_living_carbon_human perp = null)
    {
        return 0;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:942
    public override async DmTask<object> setup(obj_item_gun turret_gun = null)
    {
        return null;
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:961
public partial class obj_item_gun : obj_item
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:961
    public virtual object get_turret_properties()
    {
        DmList dot = Dm.list();
        dot["lethal_projectile"] = null;
        dot["lethal_projectile_sound"] = null;
        dot["stun_projectile"] = null;
        dot["stun_projectile_sound"] = null;
        dot["base_icon_state"] = "standard";
        return dot;
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:969
public partial class obj_item_gun_energy : obj_item_gun
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:969
    public override object get_turret_properties()
    {
        DmList dot = (DmList)base.get_turret_properties();

        object primary_ammo = ammo_type[1];

        dot["stun_projectile"] = Dm.initial(primary_ammo, (obj_item_ammo_casing p) => p.projectile_type);
        dot["stun_projectile_sound"] = Dm.initial(primary_ammo, (obj_item_ammo_casing p) => p.fire_sound);

        if (ammo_type.len > 1)
        {
            object secondary_ammo = ammo_type[2];
            dot["lethal_projectile"] = Dm.initial(secondary_ammo, (obj_item_ammo_casing p) => p.projectile_type);
            dot["lethal_projectile_sound"] = Dm.initial(secondary_ammo, (obj_item_ammo_casing p) => p.fire_sound);
        }
        else
        {
            dot["lethal_projectile"] = dot["stun_projectile"];
            dot["lethal_projectile_sound"] = dot["stun_projectile_sound"];
        }
        return dot;
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:985
public partial class obj_item_gun_ballistic : obj_item_gun
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:985
    public override object get_turret_properties()
    {
        DmList dot = (DmList)base.get_turret_properties();
        object primary_ammo = Dm.initial(spawn_magazine_type, (obj_item_ammo_box p) => p.ammo_type);

        dot["base_icon_state"] = "syndie";
        dot["stun_projectile"] = Dm.initial(primary_ammo, (obj_item_ammo_casing p) => p.projectile_type);
        dot["stun_projectile_sound"] = Dm.initial(primary_ammo, (obj_item_ammo_casing p) => p.fire_sound);
        dot["lethal_projectile"] = dot["stun_projectile"];
        dot["lethal_projectile_sound"] = dot["stun_projectile_sound"];
        return dot;
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:997
public partial class obj_item_gun_energy_laser_bluetag : obj_item_gun_energy_laser
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:997
    public override object get_turret_properties()
    {
        DmList dot = (DmList)base.get_turret_properties();
        dot["stun_projectile"] = typeof(obj_projectile_beam_lasertag_bluetag);
        dot["lethal_projectile"] = typeof(obj_projectile_beam_lasertag_bluetag);
        dot["base_icon_state"] = "blue";
        dot["shot_delay"] = 3 * SECONDS;
        dot["team_color"] = "blue";
        return dot;
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:1005
public partial class obj_item_gun_energy_laser_redtag : obj_item_gun_energy_laser
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:1005
    public override object get_turret_properties()
    {
        DmList dot = (DmList)base.get_turret_properties();
        dot["stun_projectile"] = typeof(obj_projectile_beam_lasertag_redtag);
        dot["lethal_projectile"] = typeof(obj_projectile_beam_lasertag_redtag);
        dot["base_icon_state"] = "red";
        dot["shot_delay"] = 3 * SECONDS;
        dot["team_color"] = "red";
        return dot;
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:1013
public partial class obj_item_gun_energy_e_gun_turret : obj_item_gun_energy_e_gun
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:1013
    public override object get_turret_properties()
    {
        return base.get_turret_properties();
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:1016
public partial class obj_machinery_porta_turret_lasertag : obj_machinery_porta_turret
{
    // tg: code/game/machinery/porta_turret/portable_turret.dm:1021
    public override async DmTask<object> assess_perp(mob_living_carbon_human perp = null)
    {
        float dot = 0;
        if (Dm.eq(team_color, "blue")) //Lasertag turrets target the opposing team, how great is that? -Sieve
        {
            dot = 0; //But does not target anyone else
            if (istype(perp.wear_suit, typeof(obj_item_clothing_suit_redtag)))
                dot += 4;
            if (Dm.truthy(perp.is_holding_item_of_type(typeof(obj_item_gun_energy_laser_redtag))))
                dot += 4;
            if (istype(perp.belt, typeof(obj_item_gun_energy_laser_redtag)))
                dot += 2;
        }

        if (Dm.eq(team_color, "red"))
        {
            dot = 0;
            if (istype(perp.wear_suit, typeof(obj_item_clothing_suit_bluetag)))
                dot += 4;
            if (Dm.truthy(perp.is_holding_item_of_type(typeof(obj_item_gun_energy_laser_bluetag))))
                dot += 4;
            if (istype(perp.belt, typeof(obj_item_gun_energy_laser_bluetag)))
                dot += 2;
        }
        return dot;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:1041
    public override async DmTask<object> setup(obj_item_gun turret_gun = null)
    {
        DmList properties = await base.setup(turret_gun) as DmList;
        if (Dm.truthy(properties["team_color"]))
            team_color = properties["team_color"];
        return null;
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:1046
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (ishuman(user))
        {
            mob_living_carbon_human H = user as mob_living_carbon_human;
            if (Dm.eq(team_color, "blue") && istype(H.wear_suit, typeof(obj_item_clothing_suit_redtag)))
                return UI_CLOSE;
            if (Dm.eq(team_color, "red") && istype(H.wear_suit, typeof(obj_item_clothing_suit_bluetag)))
                return UI_CLOSE;
        }
        return await base.ui_status(user, state);
    }

    // tg: code/game/machinery/porta_turret/portable_turret.dm:1064
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        object dot = await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        if (!Dm.truthy(on) || !Dm.eq(dot, BULLET_ACT_HIT))
            return dot;
        if (Dm.eq(team_color, "blue") && istype(hitting_projectile, typeof(obj_projectile_beam_lasertag_redtag)))
            set_disabled(10 * SECONDS);
        else if (Dm.eq(team_color, "red") && istype(hitting_projectile, typeof(obj_projectile_beam_lasertag_bluetag)))
            set_disabled(10 * SECONDS);
        return dot;
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:117
public partial class obj_machinery_porta_turret
{
    // tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:117
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        await base.shuttleRotate(rotation, @params);
        if (Dm.truthy(wall_turret_direction) && Dm.truthy(Dm.bitand(@params, ROTATE_DIR)))
            wall_turret_direction = turn(wall_turret_direction, Dm.num(rotation));
    }
}
