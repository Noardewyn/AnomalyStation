// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/gravitygenerator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M3 R4, the owner's first walk on Meta: the station had no gravity, so tg's Process_Spacemove refused every step):
// GLOB.gravity_generators and the station generator's gravity path: the main generator's Initialize, its parts, enable and
// disable, the generator list and the z-level gravity (update_list, SSmapping.calculate_z_level_gravity), power_change and
// set_power, the charge in process, the appearance (get_status, update_icon_state, the parts' on_update_icon) and
// shake_everyone's gravity refresh; breaking it (set_broken, from ex_act, M5x slice 3) and its repair in tg's four stages
// (screwdriver, welder, 10 plasteel, wrench; M5 slice 10); the hum (/datum/looping_sound/gravgen, M4b audio slice 5) and the other sounds (M4b audio
// slice 6), the graviton field near the generator (/datum/proximity_monitor/advanced/gravity, M4c slice 8), its tgui
// panel with the breaker (ui_interact, ui_data, ui_act; engineering package E4). Not here: the
// blackout event, the admin kickstart and shuttles (M10+): live-partial stubs
// (docs/ai/specs/live-partial.txt, ## R4) or Dm.Deferred leaves. The var blocks are generated (D111).

// tg: code/modules/power/gravitygenerator.dm:7
public partial class datum_controller_global_vars : datum_controller
{
    /// We will keep track of this by adding new gravity generators to the list, and keying it with the z level.
    // tg: code/modules/power/gravitygenerator.dm:7  GLOBAL_LIST_EMPTY(gravity_generators)
    public DmList gravity_generators;

    // tg: code/modules/power/gravitygenerator.dm:7  (GLOBAL_MANAGED(gravity_generators, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgravity_generators()
    {
        gravity_generators = Dm.list();
        Dm.append(ref gvars_datum_init_order, "gravity_generators");
    }
}

//
// Abstract Generator
//

// tg: code/modules/power/gravitygenerator.dm:22
public partial class obj_machinery_gravity_generator : obj_machinery
{
    // tg: code/modules/power/gravitygenerator.dm:33
    public override async DmTask<object> safe_throw_at(atom target = null, object range = null, object speed = null, atom thrower = null, float? spin = null, float? diagonals_first = null, datum_callback callback = null, float? force = null, float? gentle = null)
    {
        return FALSE;
    }

    // tg: code/modules/power/gravitygenerator.dm:47  (the M7 zaps slice)
    public override async DmTask<object> zap_act(object power = null, object zap_flags = null)
    {
        object dot = null;
        dot = await base.zap_act(power, zap_flags);
        if (Dm.truthy(Dm.bitand(zap_flags, ZAP_MACHINE_EXPLOSIVE)))
            qdel(this);//like the singulo, tesla deletes it. stops it from exploding over and over
        return dot;
    }

    // tg: code/modules/power/gravitygenerator.dm:52
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]_[]", get_status(), sprite_number);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/power/gravitygenerator.dm:56
    public virtual object get_status()
    {
        return "off";
    }

    // You aren't allowed to move.
    // tg: code/modules/power/gravitygenerator.dm:60
    [DmParams()]
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        qdel(this);
        return dot;
    }

    // tg: code/modules/power/gravitygenerator.dm:36  (M5x slice 3; set waitfor = FALSE, inherited from /atom/ex_act: the
    // wrapper of waiting-model.md 5.4; set_broken may sleep)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(ex_act__body(severity, dot), dot);
    }

    private async DmTask<object> ex_act__body(object severity, DotBox<object> dot)
    {
        if (Dm.num(severity) >= EXPLODE_DEVASTATE) // Very sturdy.
        {
            await set_broken();
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/power/gravitygenerator.dm:43
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        if (prob(20))
            await set_broken();
        return null;
    }

    // tg: code/modules/power/gravitygenerator.dm:64
    public virtual async DmTask set_broken()
    {
        await atom_break();
    }

    // tg: code/modules/power/gravitygenerator.dm:67
    public virtual async DmTask set_fix()
    {
        await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(BROKEN)));
    }
}

/**
 * Generator part
 *
 * Parts of the gravity generator used to have a proper sprite.
 */
// tg: code/modules/power/gravitygenerator.dm:75
public partial class obj_machinery_gravity_generator_part : obj_machinery_gravity_generator
{
    // tg: code/modules/power/gravitygenerator.dm:78
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(atom_break());
        if (Dm.truthy(main_part))
        {
            Dm.remove(main_part.generator_parts, this);
            UnregisterSignal(main_part, COMSIG_ATOM_UPDATED_ICON);
            main_part = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/power/gravitygenerator.dm:86
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(main_part))
            return NONE;
        return await main_part.item_interaction(user, tool);
    }

    // tg: code/modules/power/gravitygenerator.dm:91
    public override object get_status()
    {
        if (!Dm.truthy(main_part))
            return null;
        return main_part.get_status();
    }

    // tg: code/modules/power/gravitygenerator.dm:96
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(main_part))
            return null;
        return await main_part.attack_hand(user, modifiers);
    }

    // tg: code/modules/power/gravitygenerator.dm:101
    public override async DmTask set_broken()
    {
        await base.set_broken();
        if (!Dm.truthy(main_part) || Dm.truthy(Dm.bitand(main_part.machine_stat, BROKEN)))
            return;
        await main_part.set_broken();
    }

    /// Used to eat args
    // tg: code/modules/power/gravitygenerator.dm:108
    [SignalHandler]
    public virtual object on_update_icon(obj_machinery_gravity_generator source = null, object updates = null, object updated = null)
    {
        return update_appearance(Dm.num_or_null(updates));
    }
}

/**
 * Main gravity generator
 *
 * The actual gravity generator, that actually holds the UI, contains the grav gen parts, ect.
 */
// tg: code/modules/power/gravitygenerator.dm:117
public partial class obj_machinery_gravity_generator_main : obj_machinery_gravity_generator
{
    // tg: code/modules/power/gravitygenerator.dm:8  #define POWER_IDLE 0 (file-local, #undef at :510)
    private const int POWER_IDLE = 0;
    // tg: code/modules/power/gravitygenerator.dm:9  #define POWER_UP 1 (file-local, #undef at :511)
    private const int POWER_UP = 1;
    // tg: code/modules/power/gravitygenerator.dm:10  #define POWER_DOWN 2 (file-local, #undef at :512)
    private const int POWER_DOWN = 2;

    // tg: code/modules/power/gravitygenerator.dm:12  #define GRAV_NEEDS_SCREWDRIVER 0 (file-local, #undef at :522)
    private const int GRAV_NEEDS_SCREWDRIVER = 0;
    // tg: code/modules/power/gravitygenerator.dm:13  #define GRAV_NEEDS_WELDING 1 (file-local, #undef at :523)
    private const int GRAV_NEEDS_WELDING = 1;
    // tg: code/modules/power/gravitygenerator.dm:14  #define GRAV_NEEDS_PLASTEEL 2 (file-local, #undef at :521)
    private const int GRAV_NEEDS_PLASTEEL = 2;
    // tg: code/modules/power/gravitygenerator.dm:15  #define GRAV_NEEDS_WRENCH 3 (file-local, #undef at :524)
    private const int GRAV_NEEDS_WRENCH = 3;

    // tg: code/modules/power/gravitygenerator.dm:161
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        soundloop = Dm.@new<datum_looping_sound_gravgen>(this, Dm.named("start_immediately", FALSE));
        setup_parts();
        if (Dm.truthy(on))
        {
            Waiting.NoSleep(enable());
            center_part.add_overlay("activated");
        }

        // tg: :169  (gaps R2: the radioactive nebula station trait's shielding)
        add_to_nebula_shielding(this, typeof(datum_station_trait_nebula_hostile_radiation), nameof(get_radioactive_nebula_shielding));
        return dot;
    }

    // If we somehow get deleted, remove all of our other parts.
    // tg: code/modules/power/gravitygenerator.dm:171
    public override object Destroy(float? force = null)
    {
        investigate_log("was destroyed!", INVESTIGATE_GRAVITY);
        Waiting.NoSleep(disable());
        QDEL_NULL(ref soundloop);
        QDEL_NULL(ref center_part);
        QDEL_LIST(generator_parts);
        return base.Destroy(force);
    }

    // tg: code/modules/power/gravitygenerator.dm:179
    public virtual void setup_parts()
    {
        turf our_turf = get_turf(this);
        // 9x9 block obtained from the bottom middle of the block
        // CORNER_BLOCK_OFFSET(our_turf, 3, 3, -1, 0) (__DEFINES/turfs.dm:37), expanded inline (rules 11.1 (e))
        DmList spawn_turfs = block(our_turf.x + -1, our_turf.y + 0, our_turf.z, our_turf.x + (3 - 1) + -1, our_turf.y + (3 - 1) + 0, our_turf.z);
        float count = 10;
        foreach (turf T in Dm.each<turf>(spawn_turfs))
        {
            count--;
            if (T == our_turf) // Skip our turf.
                continue;
            var part = Dm.@new<obj_machinery_gravity_generator_part>(T);
            if (count == 5) // Middle
                center_part = part;
            if (count <= 3) // Their sprite is the top part of the generator
            {
                part.set_density(FALSE);
                part.layer = WALL_OBJ_LAYER;
            }
            part.sprite_number = count;
            part.main_part = this;
            Dm.append(generator_parts, part);
            part.update_appearance();
            part.RegisterSignal(this, COMSIG_ATOM_UPDATED_ICON, nameof(obj_machinery_gravity_generator_part.on_update_icon));
        }
    }

    // tg: code/modules/power/gravitygenerator.dm:200
    public override async DmTask set_broken()
    {
        await base.set_broken();
        foreach (obj_machinery_gravity_generator internal_parts in Dm.each_cast<obj_machinery_gravity_generator>(generator_parts))
        {
            if (!Dm.truthy(Dm.bitand(internal_parts.machine_stat, BROKEN)))
                await internal_parts.set_broken();
        }
        center_part.cut_overlays();
        charge_count = 0;
        breaker = FALSE;
        set_power();
        await disable();
        investigate_log("has broken down.", INVESTIGATE_GRAVITY);
    }

    // tg: code/modules/power/gravitygenerator.dm:212
    public override async DmTask set_fix()
    {
        await base.set_fix();
        foreach (obj_machinery_gravity_generator internal_parts in Dm.each_cast<obj_machinery_gravity_generator>(generator_parts))
        {
            if (Dm.truthy(Dm.bitand(internal_parts.machine_stat, BROKEN)))
                await internal_parts.set_fix();
        }
        broken_state = FALSE;
        update_appearance();
        set_power();
    }

    // tg: code/modules/power/gravitygenerator.dm:223
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return dot;
        float sw__1 = broken_state;
        if (sw__1 == GRAV_NEEDS_SCREWDRIVER)
            Dm.append(ref dot, span_notice(I18n.T("The entire frame is barely holding together, the <b>screws</b> need to be refastened.")));
        else if (sw__1 == GRAV_NEEDS_WELDING)
            Dm.append(ref dot, span_notice(I18n.T("There's lots of broken seals on the framework, it could use some <b>welding</b>.")));
        else if (sw__1 == GRAV_NEEDS_PLASTEEL)
            Dm.append(ref dot, span_notice(I18n.T("Some of this damaged plating needs full replacement. <b>10 plasteel</> should be enough.")));
        else if (sw__1 == GRAV_NEEDS_WRENCH)
            Dm.append(ref dot, span_notice(I18n.T("The new plating just needs to be <b>bolted</b> into place now.")));
        return dot;
    }

    // Fixing the gravity generator.
    // tg: code/modules/power/gravitygenerator.dm:238
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || (broken_state != GRAV_NEEDS_PLASTEEL))
            return NONE;
        if (!istype(tool, typeof(obj_item_stack_sheet_plasteel)))
            return NONE;
        var metal = (obj_item_stack_sheet_plasteel)tool;
        if (Dm.num(metal.get_amount()) < 10)
        {
            to_chat(user, span_warning(I18n.T("You need 10 sheets of plasteel!")));
            return ITEM_INTERACT_BLOCKING;
        }
        await metal.use(10);
        to_chat(user, span_notice(I18n.T("You add the plating to the framework.")));
        playsound(this.loc, Dm.resource("sound/machines/click.ogg"), 75, TRUE); // tg: code/modules/power/gravitygenerator.dm:249
        broken_state++;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/gravitygenerator.dm:254
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || (broken_state != GRAV_NEEDS_WELDING))
            return NONE;
        if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 50)))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("You mend the damaged framework.")));
        broken_state++;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/gravitygenerator.dm:264
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || (broken_state != GRAV_NEEDS_WRENCH))
            return NONE;
        to_chat(user, span_notice(I18n.T("You secure the plating to the framework.")));
        tool.play_tool_sound(this);
        await set_fix();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/gravitygenerator.dm:272
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || (broken_state != GRAV_NEEDS_SCREWDRIVER))
            return NONE;
        to_chat(user, span_notice(I18n.T("You secure the screws of the framework.")));
        tool.play_tool_sound(this);
        broken_state++;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/gravitygenerator.dm:281
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "GravityGenerator", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/power/gravitygenerator.dm:287
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["breaker"] = breaker;
        data["charge_count"] = charge_count;
        data["charging_state"] = charging_state;
        data["on"] = on;
        data["operational"] = Dm.truthy(Dm.bitand(machine_stat, BROKEN)) ? FALSE : TRUE;

        return data;
    }

    // tg: code/modules/power/gravitygenerator.dm:298
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "gentoggle":
                breaker = Dm.truthy(breaker) ? FALSE : TRUE;
                investigate_log(Dm.text("was toggled [] by [].", Dm.truthy(breaker) ? "<font color='green'>ON</font>" : "<font color='red'>OFF</font>", key_name(usr)), INVESTIGATE_GRAVITY);
                set_power();
                dot = TRUE;
                break;
        }
        return dot;
    }

    // tg: code/modules/power/gravitygenerator.dm:312
    public override object power_change()
    {
        object dot = base.power_change();
        if (Dm.eq(SSticker.current_state, GAME_STATE_PLAYING))
            investigate_log(Dm.text("has [] power.", Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) ? "lost" : "regained"), INVESTIGATE_GRAVITY);
        set_power();
        return dot;
    }

    // tg: code/modules/power/gravitygenerator.dm:318
    public override object get_status()
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return Dm.text("fix[]", min(broken_state, 3));
        return Dm.truthy(on) || charging_state != POWER_IDLE ? "on" : "off";
    }

    // Set the charging state based on power/breaker.
    // tg: code/modules/power/gravitygenerator.dm:324
    public virtual void set_power()
    {
        float new_state = FALSE;
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || !Dm.truthy(breaker))
            new_state = FALSE;
        else if (Dm.truthy(breaker))
            new_state = TRUE;

        charging_state = Dm.truthy(new_state) ? POWER_UP : POWER_DOWN; // Startup sequence animation.
        if (Dm.eq(SSticker.current_state, GAME_STATE_PLAYING))
            investigate_log(Dm.text("is now [].", Dm.eq(charging_state, POWER_UP) ? "charging" : "discharging"), INVESTIGATE_GRAVITY);
        update_appearance();
    }

    // tg: code/modules/power/gravitygenerator.dm:336
    public virtual async DmTask enable()
    {
        charging_state = POWER_IDLE;
        on = TRUE;
        update_use_power(ACTIVE_POWER_USE);

        soundloop.start();
        object old_gravity = gravity_in_level();
        complete_state_update();
        if (gravity_field == null) // because if it isn't null, we have just overwritten it
            gravity_field = await Dm.new_async<datum_proximity_monitor_advanced_gravity>(this, 2, TRUE, 6);

        if (!Dm.truthy(old_gravity))
        {
            if (Dm.eq(SSticker.current_state, GAME_STATE_PLAYING))
            {
                investigate_log("was brought online and is now producing gravity for this level.", INVESTIGATE_GRAVITY);
                message_admins(Dm.text("The gravity generator was brought online []", ADMIN_VERBOSEJMP(this)));
            }
            await shake_everyone();
        }
    }

    // tg: code/modules/power/gravitygenerator.dm:354
    public virtual async DmTask disable()
    {
        charging_state = POWER_IDLE;
        on = FALSE;
        update_use_power(IDLE_POWER_USE);

        soundloop.stop();
        QDEL_NULL(ref gravity_field);
        object old_gravity = gravity_in_level();
        complete_state_update();

        if (Dm.truthy(old_gravity))
        {
            if (Dm.eq(SSticker.current_state, GAME_STATE_PLAYING))
            {
                investigate_log("was brought offline and there is now no gravity for this level.", INVESTIGATE_GRAVITY);
                message_admins(Dm.text("The gravity generator was brought offline with no backup generator. []", ADMIN_VERBOSEJMP(this)));
            }
            await shake_everyone();
        }
    }

    // tg: code/modules/power/gravitygenerator.dm:371
    public virtual void complete_state_update()
    {
        update_appearance();
        update_list();
    }

    // Charge/Discharge and turn on/off gravity when you reach 0/100 percent.
    // tg: code/modules/power/gravitygenerator.dm:376
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return null;
        if (charging_state == POWER_IDLE)
            return null;
        if (charging_state == POWER_UP && charge_count >= 100)
            Waiting.NoSleep(enable());
        else if (charging_state == POWER_DOWN && charge_count <= 0)
            Waiting.NoSleep(disable());
        else
        {
            if (charging_state == POWER_UP)
                charge_count += 2;
            else if (charging_state == POWER_DOWN)
                charge_count -= 2;

            if (Dm.mod(charge_count, 4) == 0 && Dm.truthy(prob(75))) // Let them know it is charging/discharging.
                playsound(this.loc, Dm.resource("sound/effects/empulse.ogg"), 100, TRUE); // tg: code/modules/power/gravitygenerator.dm:392

            object overlay_state = null;
            float sw__1 = charge_count;
            if (sw__1 >= 0 && sw__1 <= 20)
                overlay_state = null;
            else if (sw__1 >= 21 && sw__1 <= 40)
                overlay_state = "startup";
            else if (sw__1 >= 41 && sw__1 <= 60)
                overlay_state = "idle";
            else if (sw__1 >= 61 && sw__1 <= 80)
                overlay_state = "activating";
            else if (sw__1 >= 81 && sw__1 <= 100)
                overlay_state = "activated";

            if (!Dm.eq(overlay_state, current_overlay))
            {
                if (Dm.truthy(center_part))
                {
                    center_part.cut_overlays();
                    if (Dm.truthy(overlay_state))
                        center_part.add_overlay(overlay_state);
                    current_overlay = overlay_state;
                }
            }
        }
        return null;
    }

    /// Shake everyone on the z level to let them know that gravity was enagaged/disengaged.
    // tg: code/modules/power/gravitygenerator.dm:415
    public virtual async DmTask shake_everyone()
    {
        turf T = get_turf(this);
        sound alert_sound = Dm.sound(Dm.resource("sound/effects/alert.ogg")); // tg: code/modules/power/gravitygenerator.dm:417
        foreach (object mobs__any in Dm.each(GLOB.mob_list))
        {
            var mobs = (mob)mobs__any;
            turf mob_turf = get_turf(mobs);
            if (!istype(mob_turf, typeof(turf)))
                continue;
            if (!Dm.truthy(is_valid_z_level(T, mob_turf)))
                continue;
            if (Dm.truthy(isliving(mobs)))
            {
                var grav_update = (mob_living)mobs;
                await grav_update.refresh_gravity();
            }
            if (Dm.truthy(mobs.client))
            {
                shake_camera(mobs, 15, 1); // tg: code/modules/power/gravitygenerator.dm:428
                mobs.playsound_local(T, null, 100, 1, 0.5f, sound_to_use: alert_sound); // tg: code/modules/power/gravitygenerator.dm:429
            }
        }
    }

    // tg: code/modules/power/gravitygenerator.dm:431
    public virtual object gravity_in_level()
    {
        turf T = get_turf(this);
        if (!Dm.truthy(T))
            return FALSE;
        if (Dm.truthy(GLOB.gravity_generators[Dm.text("[]", T.z)]))
            return length(GLOB.gravity_generators[Dm.text("[]", T.z)]);
        return FALSE;
    }

    // tg: code/modules/power/gravitygenerator.dm:439
    public virtual void update_list()
    {
        turf T = get_turf(this);
        if (!Dm.truthy(T))
            return;
        DmList z_list = Dm.list();
        // Multi-Z, station gravity generator generates gravity on all ZTRAIT_STATION z-levels.
        if (Dm.truthy(SSmapping.level_trait(T.z, ZTRAIT_STATION)))
        {
            foreach (object z in Dm.each(SSmapping.levels_by_trait(ZTRAIT_STATION)))
                Dm.append(z_list, z);
        }
        else
            Dm.append(z_list, T.z);
        foreach (object z in Dm.each(z_list))
        {
            if (!Dm.truthy(GLOB.gravity_generators[Dm.text("[]", z)]))
                GLOB.gravity_generators[Dm.text("[]", z)] = Dm.list();
            if (Dm.truthy(on))
                Dm.combine_at(GLOB.gravity_generators, Dm.text("[]", z), this);
            else
                Dm.remove_at(GLOB.gravity_generators, Dm.text("[]", z), this);
            SSmapping.calculate_z_level_gravity(z);
        }
    }

    // tg: code/modules/power/gravitygenerator.dm:459
    public virtual async DmTask blackout()
    {
        charge_count = 0;
        breaker = FALSE;
        set_power();
        await disable();
        investigate_log("was turned off by blackout event or a gravity anomaly detonation.", INVESTIGATE_GRAVITY);
    }

    // tg: code/modules/power/gravitygenerator.dm:466
    public override async DmTask<object> beforeShuttleMove(turf newT = null, object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = await base.beforeShuttleMove(newT, rotation, move_mode, moving_dock);
        await disable();
        return dot;
    }

    // tg: code/modules/power/gravitygenerator.dm:470
    public override async DmTask<object> afterShuttleMove(turf oldT = null, DmList movement_force = null, object shuttle_dir = null, object shuttle_preferred_direction = null, object move_dir = null, object rotation = null)
    {
        object dot = await base.afterShuttleMove(oldT, movement_force, shuttle_dir, shuttle_preferred_direction, move_dir, rotation);
        if (charge_count != 0 && charging_state != POWER_UP)
            await enable();
        return dot;
    }

    // tg: code/modules/power/gravitygenerator.dm:475
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (Dm.truthy(same_z_layer))
            return;
        foreach (obj_machinery_gravity_generator part in Dm.each<obj_machinery_gravity_generator>(generator_parts))
            SET_PLANE(part, Dm.num(PLANE_TO_TRUE(part.plane)), new_turf);
    }

    /// Returns the radioactive shielding (if there's a radioactive nebula). Called from a callback set in add_to_nebula_shielding()
    // tg: code/modules/power/gravitygenerator.dm:483
    public virtual object get_radioactive_nebula_shielding()
    {
        return Dm.truthy(on) ? radioactive_nebula_shielding : 0;
    }

    //prevents shuttles attempting to rotate this since it messes up sprites
    // tg: code/modules/power/gravitygenerator.dm:487
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        @params = NONE;
        await base.shuttleRotate(rotation, @params);
    }

    /// Admin proc that causes gravity to fully restart, via the secrets panel's fix gravity.
    // tg: code/modules/power/gravitygenerator.dm:492
    public virtual async DmTask kickstart()
    {
        charge_count = 100;
        breaker = TRUE;
        set_power();
        await enable();
        investigate_log("was turned re-enabled by admin event.", INVESTIGATE_GRAVITY);
    }
}
