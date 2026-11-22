// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/apc/apc_main.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 3 (the APC): the whole file. Left to their milestones (rows in docs/ai/specs/live-partial.txt, ## M7 slice 3):
// on_saboteur (the saboteur's disruptor, antagonist gear, the parity tier after M10); connect_remote_access and disconnect_remote_access came with the APC control console (the engineering package E6,
// D287). The AI's malfunction procs are apc_malf.cs's. Deferred leaves:
// the speech of say (M9) and the emissive overlays (DEVIATION DV12, apc_appearance.cs).
//
// GAP(types): tg's `var/area/area` (:35) and `var/obj/item/stock_parts/power_store/cell` (:39) would be generated as Types (rules
// 3.2 V3: tg assigns typepaths to vars of those names elsewhere), which the APC's area and its cell instance do not fit
// (types-converter.md 3.5, "Not covered"): the hand-written members keep tg's declared types, and the generator leaves them
// out. A hand-written var makes the class's var block hand-written (types-converter.md 3.2, "Hand"): the constructor with
// tg's overrides (:18-30, :82, :145) is here, as light.cs does for its cell.

// tg: code/modules/power/apc/apc_main.dm:17
public partial class obj_machinery_power_apc : obj_machinery_power
{
    // tg: code/modules/power/apc/apc_main.dm:8  #define CHARGELEVEL 0.01 (file-local, #undef at :825)
    ///Cap for how fast cells charge, as a percentage per second (.01 means cellcharge is capped to 1% per second)
    private const float CHARGELEVEL = 0.01f;
    // tg: code/modules/power/apc/apc_main.dm:11  #define APC_CHANNEL_LIGHT_TRESHOLD 15 (file-local, #undef at :826)
    ///Charge percentage at which the lights channel stops working
    private const int APC_CHANNEL_LIGHT_TRESHOLD = 15;
    // tg: code/modules/power/apc/apc_main.dm:13  #define APC_CHANNEL_EQUIP_TRESHOLD 30 (file-local, #undef at :827)
    ///Charge percentage at which the equipment channel stops working
    private const int APC_CHANNEL_EQUIP_TRESHOLD = 30;
    // tg: code/modules/power/apc/apc_main.dm:15  #define APC_CHANNEL_ALARM_TRESHOLD 75 (file-local, #undef at :828)
    ///Charge percentage at which the APC icon indicates discharging
    private const int APC_CHANNEL_ALARM_TRESHOLD = 75;

    ///Reference to our area
    // tg: code/modules/power/apc/apc_main.dm:35  (hand-written: see the GAP(types) above)
    public area area;
    ///Reference to our internal cell
    // tg: code/modules/power/apc/apc_main.dm:39  (hand-written: see the GAP(types) above)
    public obj_item_stock_parts_power_store cell;

    // tg: code/modules/power/apc/apc_main.dm:18
    public obj_machinery_power_apc()
    {
        name = "area power controller";
        desc = "A control terminal for the area's electrical systems.";
        icon = Dm.resource("icons/obj/machines/wallmounts.dmi");
        icon_state = "apc0";
        use_power = NO_POWER_USE;
        req_access = null;
        max_integrity = 200;
        integrity_failure = 0.25f;
        damage_deflection = 10;
        resistance_flags = FIRE_PROOF;
        interaction_flags_machine = Dm.bitor(Dm.bitor(INTERACT_MACHINE_WIRES_IF_OPEN, INTERACT_MACHINE_ALLOW_SILICON), INTERACT_MACHINE_OPEN_SILICON);
        interaction_flags_click = ALLOW_SILICON_REACH;
        processing_flags = START_PROCESSING_MANUALLY;
        powernet = FALSE; // set so that APCs aren't found as powernet nodes //Hackish, Horrible, was like this before I changed it :(
        armor_type = typeof(datum_armor_power_apc);
    }

    // tg: code/modules/power/apc/apc_main.dm:176
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        //APCs get added to their own processing tasks for the machines subsystem.
        if (!Dm.truthy(Dm.bitand(datum_flags, DF_ISPROCESSING)))
        {
            Dm.combine(ref datum_flags, DF_ISPROCESSING);
            // rules 10.6: tg's Master creates SSmachines before the map; a test harness world may lack it (as _machinery.cs).
            if (SSmachines != null)
                Dm.append(ref SSmachines.processing_apcs, this);
        }

        //Assign it to its area. If mappers already assigned an area string fast load the area from it else get the current area
        area our_area = get_area(loc) as area;
        if (Dm.truthy(areastring))
        {
            area = get_area_instance_from_text(areastring) as area;
            if (area == null)
            {
                area = our_area;
                stack_trace(Dm.text("Bad areastring path for [], []", this, areastring), "code/modules/power/apc/apc_main.dm", 190);
            }
        }
        else if (isarea(our_area) && areastring == null)
            area = our_area;
        if (Dm.truthy(area))
        {
            if (Dm.truthy(area.apc))
                log_mapping(Dm.text("Duplicate APC created at [] []. Original at [] [].", AREACOORD(this), area.type, AREACOORD(area.apc), area.type));
            area.apc = this;
        }

        //Initialize name & access of the apc. Name requires area to be assigned first
        if (!Dm.truthy(req_access))
            req_access = Dm.list(ACCESS_ENGINE_EQUIP);
        if (Dm.truthy(auto_name))
            name = I18n.T("\\improper [] APC", get_area_name(area, TRUE));

        //Initialize its electronics
        set_wires(Dm.@new<datum_wires_apc>(this));
        alarm_manager = Dm.@new<datum_alarm_handler>(this);
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        // for apcs created during map load make them fully functional
        if (Dm.truthy(mapload))
        {
            has_electronics = APC_ELECTRONICS_SECURED;
            // is starting with a power cell installed, create it and set its charge level
            if (cell_type != null)
            {
                cell = Dm.@new(cell_type, this) as obj_item_stock_parts_power_store;
                cell.charge = start_charge * cell.maxcharge / 100; // (convert percentage to actual value)
            }
            make_terminal();
            ///This is how we test to ensure that mappers use the directional subtypes of APCs, rather than use the parent and pixel-shift it themselves.
            Waiting.NoSleep(setDir(dir));
            if (abs(Dm.num(offset_old)) != APC_PIXEL_OFFSET)
                log_mapping(Dm.text("APC: ([]) at [] with dir ([] | []) has pixel_[] value [] - should be [][]. Use the directional/ helpers!", this, AREACOORD(this), dir, uppertext(Dm.str(dir2text(dir))), Dm.truthy(Dm.bitand(dir, Dm.bitor(WEST, EAST))) ? "x" : "y", offset_old, Dm.truthy(Dm.bitand(dir, Dm.bitor(SOUTH, EAST))) ? "-" : "", APC_PIXEL_OFFSET));
            Waiting.NoSleep(find_and_mount_on_atom());
        }
        // For apcs created during the round players need to configure them from scratch
        else
        {
            opened = APC_COVER_OPENED;
            operating = FALSE;
            Waiting.NoSleep(set_machine_stat(Dm.bitor(machine_stat, MAINT)));
        }

        //Make the apc visually interactive
        register_context();
        _addtimer(Dm.@new<datum_callback>(this, nameof(update)), 0.5f * SECONDS, file: "code/modules/power/apc/apc_main.dm", line: 229);
        RegisterSignal(SSdcs, COMSIG_GLOB_GREY_TIDE, nameof(grey_tide));
        update_appearance();

        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_bare_hands), Dm.named("rmb_text", I18n.T("Toggle interface lock"))));
        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_mob_typechecks), Initialize__hovering_mob_typechecks));
        return dot;
    }

    // tg: code/modules/power/apc/apc_main.dm:233  var/static/list/hovering_mob_typechecks (a proc-level static, rules 10.6)
    private static ref DmList Initialize__hovering_mob_typechecks => ref TgState.Current.obj_machinery_power_apc__Initialize__hovering_mob_typechecks;

    // tg: code/modules/power/apc/apc_main.dm:245
    public override object Destroy(float? force = null)
    {
        // rules 10.6: tg's Master creates SSmachines before the map; a test harness world may lack it (as _machinery.cs).
        if (SSmachines != null)
            Dm.remove(ref SSmachines.processing_apcs, this);
        if (malfai != null)
        {
            Dm.remove(ref malfai.hacked_apcs, this);
            malfai = null;
        }
        disconnect_from_area();
        QDEL_NULL(ref alarm_manager);
        if (occupier != null)
            Waiting.NoSleep(malfvacate(TRUE));
        if (cell != null)
            QDEL_NULL(ref cell);
        if (terminal != null)
            Waiting.NoSleep(disconnect_terminal());
        return base.Destroy(force);
    }

    // tg: code/modules/power/apc/apc_main.dm:260
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);

        object sw__1 = newdir;
        if (Dm.eq(sw__1, NORTH))
        {
            offset_old = pixel_y;
            pixel_y = APC_PIXEL_OFFSET;
        }
        else if (Dm.eq(sw__1, SOUTH))
        {
            offset_old = pixel_y;
            pixel_y = -APC_PIXEL_OFFSET;
        }
        else if (Dm.eq(sw__1, EAST))
        {
            offset_old = pixel_x;
            pixel_x = APC_PIXEL_OFFSET;
        }
        else if (Dm.eq(sw__1, WEST))
        {
            offset_old = pixel_x;
            pixel_x = -APC_PIXEL_OFFSET;
        }

        image hud_image = image(icon: Dm.resource("icons/mob/huds/hud.dmi"), icon_state: "apc_hacked"); // DEFAULT_HUDS_DMI (__DEFINES/atom_hud.dm:75)
        hud_image.pixel_w = pixel_x;
        hud_image.pixel_z = pixel_y;
        hud_list = Dm.list(
            Dm.kv(MALF_APC_HUD, hud_image)
        );
    }

    // tg: code/modules/power/apc/apc_main.dm:290
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        update_area_power_usage(Dm.not(old_value));
    }

    // tg: code/modules/power/apc/apc_main.dm:293
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        if (Dm.truthy(auto_name))
            name = I18n.T("\\improper [] APC", get_area_name(area, TRUE));
        return dot;
    }

    // tg: code/modules/power/apc/apc_main.dm:298
    public virtual void assign_to_area(area target_area = null)
    {
        target_area ??= get_area(this) as area;
        if (area == target_area)
            return;

        disconnect_from_area();
        area = target_area;
        update_area_power_usage(TRUE);
        area.apc = this;
        auto_name = TRUE;

        update_appearance(UPDATE_NAME);
    }

    // tg: code/modules/power/apc/apc_main.dm:310
    public virtual void update_area_power_usage(object state = null)
    {
        //apc is non functional so force disable
        if (Dm.truthy(state) && (has_electronics != APC_ELECTRONICS_SECURED || Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, MAINT))) || QDELETED(cell)))
            state = FALSE;

        //no change in value
        if (Dm.eq(state, area.power_light) && Dm.eq(state, area.power_equip) && Dm.eq(state, area.power_environ))
            return;

        area.power_light = Dm.num(state);
        area.power_equip = Dm.num(state);
        area.power_environ = Dm.num(state);

        area.power_change();
    }

    // tg: code/modules/power/apc/apc_main.dm:325
    public virtual void disconnect_from_area()
    {
        if (area == null)
            return;

        update_area_power_usage(FALSE);
        area.apc = null;
        area = null;
    }

    // tg: code/modules/power/apc/apc_main.dm:333
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == cell)
        {
            cell = null;
            charging = APC_NOT_CHARGING;
            update_appearance();
            if (!QDELING(this))
                SStgui.update_uis(this);
        }
        return dot;
    }

    // tg: code/modules/power/apc/apc_main.dm:342
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            if (opened != APC_COVER_REMOVED)
            {
                Dm.append(ref dot, I18n.T("The cover is broken and can probably be <i>pried</i> off with enough force."));
                return dot;
            }
            if (terminal != null && Dm.truthy(has_electronics))
                Dm.append(ref dot, I18n.T("The cover is missing but can be replaced using a new frame."));
            return dot;
        }
        if (Dm.truthy(opened))
        {
            if (Dm.truthy(has_electronics) && terminal != null)
                Dm.append(ref dot, I18n.T("The cover is [] and the power cell is [].", opened == APC_COVER_REMOVED ? I18n.T("removed") : I18n.T("open"), cell != null ? I18n.T("installed") : I18n.T("missing")));
            else
                Dm.append(ref dot, I18n.T("It's [] wired up.\n\n\t\t\tThe electronics are[] installed.", terminal == null ? I18n.T("not") : "", !Dm.truthy(has_electronics) ? I18n.T("n't") : ""));
        }
        else
        {
            if (Dm.truthy(Dm.bitand(machine_stat, MAINT)))
                Dm.append(ref dot, I18n.T("The cover is closed. Something is wrong with it. It doesn't work."));
            else if (Dm.truthy(malfhack))
                Dm.append(ref dot, I18n.T("The cover is broken. It may be hard to force it open."));
            else
                Dm.append(ref dot, I18n.T("The cover is closed."));
        }
        return dot;
    }

    // tg: code/modules/power/apc/apc_main.dm:365
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = null;
        dot = await base.atom_break(damage_flag);
        if (Dm.truthy(dot))
        {
            operating = FALSE;
            if (occupier != null)
                await malfvacate(TRUE);
            update();
        }

        if (opened != APC_COVER_REMOVED)
        {
            opened = APC_COVER_REMOVED;
            coverlocked = FALSE;
            visible_message(span_warning(I18n.T("The APC cover is knocked down!")));
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/power/apc/apc_main.dm:379
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Apc", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/power/apc/apc_main.dm:385
    public override async DmTask<object> ui_data(mob user = null)
    {
        // GAP(lead): isAdminGhostAI (modules/mob/mob_helpers.dm:357) is not declared: no admin ghosts yet (as _machinery.cs).
        bool isAdminGhostAI__gap = false;
        DmList data = Dm.list(
            Dm.kv("locked", locked),
            Dm.kv("failTime", failure_timer),
            Dm.kv("isOperating", operating),
            Dm.kv("externalPower", main_status),
            Dm.kv("powerCellStatus", cell != null ? cell.percent() : null),
            Dm.kv("chargeMode", chargemode),
            Dm.kv("chargingStatus", charging),
            Dm.kv("chargingPowerDisplay", display_power(lastused_charge)),
            Dm.kv("totalLoad", display_power(lastused_total)),
            Dm.kv("coverLocked", coverlocked),
            Dm.kv("remoteAccess", Dm.num(user == remote_control_user)),
            Dm.kv("siliconUser", Dm.num(HAS_TRAIT(user, TRAIT_SILICON_ACCESS) || isAdminGhostAI__gap)), // HAS_SILICON_ACCESS(user) (__DEFINES/is_helpers.dm:178)
            Dm.kv("malfStatus", get_malf_status(user as mob_living_silicon_ai)),
            Dm.kv("emergencyLights", Dm.not(emergency_lights)),
            Dm.kv("nightshiftLights", nightshift_lights),
            Dm.kv("disable_nightshift_toggle", low_power_nightshift_lights),

            Dm.kv("powerChannels", Dm.list(
                Dm.list(
                    Dm.kv("title", I18n.T("Equipment")),
                    Dm.kv("powerLoad", display_power(lastused_equip)),
                    Dm.kv("status", equipment),
                    Dm.kv("topicParams", Dm.list(
                        Dm.kv("auto", Dm.list(Dm.kv("eqp", 3))),
                        Dm.kv("on", Dm.list(Dm.kv("eqp", 2))),
                        Dm.kv("off", Dm.list(Dm.kv("eqp", 1)))
                    ))
                ),
                Dm.list(
                    Dm.kv("title", I18n.T("Lighting")),
                    Dm.kv("powerLoad", display_power(lastused_light)),
                    Dm.kv("status", lighting),
                    Dm.kv("topicParams", Dm.list(
                        Dm.kv("auto", Dm.list(Dm.kv("lgt", 3))),
                        Dm.kv("on", Dm.list(Dm.kv("lgt", 2))),
                        Dm.kv("off", Dm.list(Dm.kv("lgt", 1)))
                    ))
                ),
                Dm.list(
                    Dm.kv("title", I18n.T("Environment")),
                    Dm.kv("powerLoad", display_power(lastused_environ)),
                    Dm.kv("status", environ),
                    Dm.kv("topicParams", Dm.list(
                        Dm.kv("auto", Dm.list(Dm.kv("env", 3))),
                        Dm.kv("on", Dm.list(Dm.kv("env", 2))),
                        Dm.kv("off", Dm.list(Dm.kv("env", 1)))
                    ))
                )
            ))
        );
        return data;
    }

    // tg: code/modules/power/apc/apc_main.dm:439
    public virtual async DmTask connect_remote_access(mob remote_user = null)
    {
        if (Dm.truthy(opened))
            return;
        remote_control_user = remote_user;
        await ui_interact(remote_user);
        remote_user.log_message(Dm.text("remotely accessed [].", this), LOG_GAME);
        await say(I18n.T("Remote access detected.[]", Dm.truthy(locked) ? I18n.T(" Interface unlocked.") : ""));
        // [icon2html(src, remote_control_user)] before the text: the APC's picture in the chat line
        Dm.Deferred("code/modules/power/apc/apc_main.dm:446", "connect_remote_access: icon2html(src, remote_control_user), the APC's icon in the chat line (chat icons, M10+)");
        to_chat(remote_control_user, span_danger(I18n.T("Connected to [].", this)));
        if (Dm.truthy(locked))
        {
            playsound(this, Dm.resource("sound/machines/terminal/terminal_on.ogg"), 25, FALSE);
            locked = FALSE;
        }
        playsound(this, Dm.resource("sound/machines/terminal/terminal_alert.ogg"), 50, FALSE);
        update_appearance();
    }

    /**
     * Disconnects anyone using this APC via an APC control console and locks the interface.
     * arguments:
     * mute - whether the APC should announce the disconnection locally
     */
    // tg: code/modules/power/apc/apc_main.dm:458
    public virtual async DmTask disconnect_remote_access(float? mute = null)
    {
        mute ??= FALSE;
        // nothing to disconnect from
        if (remote_control_user == null)
            return;
        locked = TRUE;
        // [icon2html(src, remote_control_user)] before the text: the APC's picture in the chat line
        Dm.Deferred("code/modules/power/apc/apc_main.dm:463", "disconnect_remote_access: icon2html(src, remote_control_user), the APC's icon in the chat line (chat icons, M10+)");
        to_chat(remote_control_user, span_danger(I18n.T("Disconnected from [].", this)));
        if (!Dm.truthy(mute))
        {
            await say(I18n.T("Remote access canceled. Interface locked."));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 25, FALSE);
            playsound(this, Dm.resource("sound/machines/terminal/terminal_alert.ogg"), 50, FALSE);
        }
        update_appearance();
        remote_control_user = null;
    }

    // tg: code/modules/power/apc/apc_main.dm:471
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        object dot = null;
        dot = await base.ui_status(user, state);
        if (!QDELETED(remote_control_user) && user == remote_control_user)
            dot = UI_INTERACTIVE;
        return dot;
    }

    // tg: code/modules/power/apc/apc_main.dm:476
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = null;
        dot = await base.ui_act(action, @params, ui, state);
        mob user = ui.user;
        // GAP(lead): isAdminGhostAI (modules/mob/mob_helpers.dm:357) is not declared: no admin ghosts yet (as _machinery.cs).
        bool isAdminGhostAI__gap = false;
        bool silicon_access = HAS_TRAIT(user, TRAIT_SILICON_ACCESS) || isAdminGhostAI__gap; // HAS_SILICON_ACCESS(user) (__DEFINES/is_helpers.dm:178)

        if (Dm.truthy(dot) || !Dm.truthy(can_use(user, 1)) || (Dm.truthy(locked) && !silicon_access && !Dm.truthy(failure_timer) && !Dm.eq(action, "toggle_nightshift")))
            return dot;
        object sw__1 = action;
        if (Dm.eq(sw__1, "lock"))
        {
            if (silicon_access)
            {
                if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) || Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, MAINT))) || remote_control_user != null)
                    to_chat(user, span_warning(I18n.T("The APC does not respond to the command!")));
                else
                {
                    locked = Dm.not(locked);
                    update_appearance();
                    dot = TRUE;
                }
            }
        }
        else if (Dm.eq(sw__1, "cover"))
        {
            coverlocked = Dm.not(coverlocked);
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "breaker"))
        {
            await toggle_breaker(user);
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "toggle_nightshift"))
        {
            toggle_nightshift_lights(user);
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "charge"))
        {
            chargemode = Dm.not(chargemode);
            if (!Dm.truthy(chargemode))
            {
                charging = APC_NOT_CHARGING;
                update_appearance();
            }
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "channel"))
        {
            if (Dm.truthy(@params["eqp"]))
            {
                equipment = Dm.num_or_null(setsubsystem(text2num(@params["eqp"])));
                update_appearance();
                update();
            }
            else if (Dm.truthy(@params["lgt"]))
            {
                lighting = Dm.num(setsubsystem(text2num(@params["lgt"])));
                update_appearance();
                update();
            }
            else if (Dm.truthy(@params["env"]))
            {
                environ = Dm.num(setsubsystem(text2num(@params["env"])));
                update_appearance();
                update();
            }
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "overload"))
        {
            if (silicon_access)
            {
                await overload_lighting();
                dot = TRUE;
            }
        }
        else if (Dm.eq(sw__1, "hack"))
        {
            if (Dm.truthy(get_malf_status(user as mob_living_silicon_ai)))
                malfhack__proc(user as mob_living_silicon_ai);
        }
        else if (Dm.eq(sw__1, "occupy"))
        {
            if (Dm.truthy(get_malf_status(user as mob_living_silicon_ai)))
                malfoccupy(user as mob_living_silicon_ai);
        }
        else if (Dm.eq(sw__1, "deoccupy"))
        {
            if (Dm.truthy(get_malf_status(user as mob_living_silicon_ai)))
                await malfvacate();
        }
        else if (Dm.eq(sw__1, "reboot"))
        {
            failure_timer = 0;
            force_update = FALSE;
            update_appearance();
            update();
        }
        else if (Dm.eq(sw__1, "emergency_lighting"))
        {
            emergency_lights = Dm.not(emergency_lights);
            foreach (obj_machinery_light area_light in Dm.each_cast<obj_machinery_light>(get_lights()))
            {
                if (!Dm.truthy(Dm.initial(area_light, (obj_machinery_light p) => p.no_low_power))) //If there was an override set on creation, keep that override
                {
                    area_light.no_low_power = emergency_lights;
                    Waiting.Detach(area_light.update(FALSE)); // INVOKE_ASYNC(area_light, TYPE_PROC_REF(/obj/machinery/light/, update), FALSE)
                }
                CHECK_TICK();
            }
        }
        return TRUE;
    }

    // tg: code/modules/power/apc/apc_main.dm:547
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        if (user == remote_control_user)
            Waiting.NoSleep(disconnect_remote_access());
    }

    /// Returns a list of lights powered/controlled by src
    // tg: code/modules/power/apc/apc_main.dm:553
    public virtual object get_lights()
    {
        DmList lights = Dm.list();
        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(area.get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each_cast<turf>(zlevel_turfs))
            {
                foreach (obj_machinery_light found_light in Dm.each<obj_machinery_light>(area_turf))
                    Dm.append(ref lights, found_light);
            }
        }
        return lights;
    }

    /**
     * APC early processing. This gets processed after any other machine on the powernet does.
     * This adds up the total static power usage for the apc's area, then draw that power usage from the grid or APC cell.
     */
    // tg: code/modules/power/apc/apc_main.dm:565
    public virtual async DmTask early_process()
    {
        if (!QDELETED(cell) && cell.charge < cell.maxcharge)
        {
            last_charging = charging;
            charging = APC_NOT_CHARGING;
        }
        if (area == null)
            return;

        float total_static_energy_usage = 0;
        if (Dm.truthy(operating))
        {
            total_static_energy_usage += Dm.num(APC_CHANNEL_IS_ON(lighting)) * Dm.num(area.energy_usage[AREA_USAGE_STATIC_LIGHT]);
            total_static_energy_usage += Dm.num(APC_CHANNEL_IS_ON(Dm.num(equipment))) * Dm.num(area.energy_usage[AREA_USAGE_STATIC_EQUIP]);
            total_static_energy_usage += Dm.num(APC_CHANNEL_IS_ON(environ)) * Dm.num(area.energy_usage[AREA_USAGE_STATIC_ENVIRON]);
        }
        area.clear_usage();

        // COOLDOWN_FINISHED(src, malf_ai_pt_generation) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (malfai != null && malf_ai_pt_generation <= world.time && !Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, MAINT))) && !Dm.truthy(failure_timer) && Dm.le(total_static_energy_usage + 60 * KILO * JOULES, Dm.add(terminal?.surplus(), cell.charge)) && malfai.malf_picker.processing_time < MALF_MAX_PP)
        //if we're hacked, we're off point cooldown, we're not broken or off temporarily, we can gather enough power, and our ai can take more points
        {
            total_static_energy_usage += 60 * KILO * JOULES;
            malf_ai_pt_generation = world.time + 30 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))
            malfai.malf_picker.processing_time += 1;
        }

        if (Dm.truthy(total_static_energy_usage)) //Use power from static power users.
        {
            object grid_used = min(terminal?.surplus(), total_static_energy_usage);
            terminal?.add_load(grid_used);
            if (Dm.gt(total_static_energy_usage, grid_used) && !QDELETED(cell))
                await cell.use(Dm.sub(total_static_energy_usage, grid_used), TRUE);
        }
    }

    // tg: code/modules/power/apc/apc_main.dm:591
    public virtual void late_process(object seconds_per_tick = null)
    {
        if (Dm.truthy(icon_update_needed))
            update_appearance();
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, MAINT))))
            return;
        if (!Dm.truthy(area?.requires_power))
            return;
        if (Dm.truthy(failure_timer))
        {
            failure_timer = max(0, failure_timer - Dm.num(seconds_per_tick));
            force_update = TRUE;
            return;
        }

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) || malfai != null)
        {
            hacked_flicker_counter = hacked_flicker_counter - 1;
            if (hacked_flicker_counter <= 0)
                flicker_hacked_icon();
        }

        //dont use any power from that channel if we shut that power channel off
        if (Dm.truthy(operating))
        {
            lastused_light = APC_CHANNEL_IS_ON(lighting) ? Dm.num(area.energy_usage[AREA_USAGE_LIGHT]) + Dm.num(area.energy_usage[AREA_USAGE_STATIC_LIGHT]) : 0;
            lastused_equip = APC_CHANNEL_IS_ON(Dm.num(equipment)) ? Dm.num(area.energy_usage[AREA_USAGE_EQUIP]) + Dm.num(area.energy_usage[AREA_USAGE_STATIC_EQUIP]) : 0;
            lastused_environ = APC_CHANNEL_IS_ON(environ) ? Dm.num(area.energy_usage[AREA_USAGE_ENVIRON]) + Dm.num(area.energy_usage[AREA_USAGE_STATIC_ENVIRON]) : 0;
        }
        else
        {
            lastused_light = 0;
            lastused_equip = 0;
            lastused_environ = 0;
        }

        lastused_charge = Dm.eq(charging, APC_CHARGING) ? Dm.num(area.energy_usage[AREA_USAGE_APC_CHARGE]) : 0;

        lastused_total = lastused_light + lastused_equip + lastused_environ + lastused_charge;

        //store states to update icon if any change
        float last_lt = lighting;
        float? last_eq = equipment;
        float last_en = environ;
        object excess = surplus();

        if (!Dm.truthy(avail()))
            main_status = APC_NO_POWER;
        else if (Dm.le(excess, 0))
            main_status = APC_LOW_POWER;
        else
            main_status = APC_HAS_POWER;

        if (cell != null && !Dm.truthy(shorted)) //need to check to make sure the cell is still there since rigged/corrupted cells can randomly explode after give().
        {
            // set channels depending on how much charge we have left
            object cell_percent = cell.percent();
            if (cell.charge <= 0) // zero charge, turn all off
            {
                equipment = Dm.num_or_null(autoset(equipment, AUTOSET_FORCE_OFF));
                lighting = Dm.num(autoset(lighting, AUTOSET_FORCE_OFF));
                environ = Dm.num(autoset(environ, AUTOSET_FORCE_OFF));
                alarm_manager.send_alarm(ALARM_POWER);
                if (!Dm.truthy(nightshift_lights) || (Dm.truthy(nightshift_lights) && !Dm.truthy(low_power_nightshift_lights)))
                {
                    low_power_nightshift_lights = TRUE;
                    Waiting.DetachSync(() => set_nightshift(TRUE)); // INVOKE_ASYNC(src, PROC_REF(set_nightshift), TRUE)
                }
            }
            else if (Dm.lt(cell_percent, APC_CHANNEL_LIGHT_TRESHOLD)) // turn off lighting & equipment
            {
                equipment = Dm.num_or_null(autoset(equipment, AUTOSET_OFF));
                lighting = Dm.num(autoset(lighting, AUTOSET_OFF));
                environ = Dm.num(autoset(environ, AUTOSET_ON));
                alarm_manager.send_alarm(ALARM_POWER);
                if (!Dm.truthy(nightshift_lights) || (Dm.truthy(nightshift_lights) && !Dm.truthy(low_power_nightshift_lights)))
                {
                    low_power_nightshift_lights = TRUE;
                    Waiting.DetachSync(() => set_nightshift(TRUE)); // INVOKE_ASYNC(src, PROC_REF(set_nightshift), TRUE)
                }
            }
            else if (Dm.lt(cell_percent, APC_CHANNEL_EQUIP_TRESHOLD)) // turn off equipment
            {
                equipment = Dm.num_or_null(autoset(equipment, AUTOSET_OFF));
                lighting = Dm.num(autoset(lighting, AUTOSET_ON));
                environ = Dm.num(autoset(environ, AUTOSET_ON));
                alarm_manager.send_alarm(ALARM_POWER);
                if (!Dm.truthy(nightshift_lights) || (Dm.truthy(nightshift_lights) && !Dm.truthy(low_power_nightshift_lights)))
                {
                    low_power_nightshift_lights = TRUE;
                    Waiting.DetachSync(() => set_nightshift(TRUE)); // INVOKE_ASYNC(src, PROC_REF(set_nightshift), TRUE)
                }
            }
            else // otherwise all can be on
            {
                equipment = Dm.num_or_null(autoset(equipment, AUTOSET_ON));
                lighting = Dm.num(autoset(lighting, AUTOSET_ON));
                environ = Dm.num(autoset(environ, AUTOSET_ON));
                //At this point the APC is fully powered. If we've swapped to night lights because of low power then
                //that gets undone here. The night lights are disabled depending on nightshift or security level.
                if (Dm.truthy(nightshift_lights) && Dm.truthy(low_power_nightshift_lights))
                {
                    low_power_nightshift_lights = FALSE;
                    bool nightshift_disabled = Dm.locate<datum_round_event_nightshift>(SSevents.running) == null;
                    if (nightshift_disabled || Dm.num(SSsecurity_level.get_current_level_as_number()) >= SEC_LEVEL_RED)
                        Waiting.DetachSync(() => set_nightshift(FALSE)); // INVOKE_ASYNC(src, PROC_REF(set_nightshift), FALSE)
                }
                if (Dm.gt(cell_percent, APC_CHANNEL_ALARM_TRESHOLD))
                    alarm_manager.clear_alarm(ALARM_POWER);
            }
        }
        else // no cell, switch everything off
        {
            charging = APC_NOT_CHARGING;
            equipment = Dm.num_or_null(autoset(equipment, AUTOSET_FORCE_OFF));
            lighting = Dm.num(autoset(lighting, AUTOSET_FORCE_OFF));
            environ = Dm.num(autoset(environ, AUTOSET_FORCE_OFF));
            alarm_manager.send_alarm(ALARM_POWER);
        }

        // update icon & area power if anything changed
        if (last_lt != lighting || !Dm.eq(last_eq, equipment) || last_en != environ || Dm.truthy(force_update))
        {
            force_update = FALSE;
            queue_icon_update();
            update();
        }
        else if (!Dm.eq(charging, last_charging))
            queue_icon_update();
    }

    // charge until the battery is full or to the treshold of the provided channel
    // tg: code/modules/power/apc/apc_main.dm:692
    public virtual async DmTask charge_channel(object channel = null, object seconds_per_tick = null)
    {
        if (cell == null || Dm.truthy(shorted) || !Dm.truthy(operating) || !Dm.truthy(chargemode) || !Dm.truthy(surplus()) || !Dm.truthy(cell.used_charge()))
            return;

        // no overcharge past the next treshold
        float need_charge_for_channel = 0;
        object sw__1 = channel;
        if (Dm.eq(sw__1, SSMACHINES_APCS_ENVIRONMENT))
            need_charge_for_channel = (cell.maxcharge * 0.05f) - cell.charge;
        else if (Dm.eq(sw__1, SSMACHINES_APCS_LIGHTS))
            need_charge_for_channel = (cell.maxcharge * (APC_CHANNEL_LIGHT_TRESHOLD + 5) * 0.01f) - cell.charge;
        else if (Dm.eq(sw__1, SSMACHINES_APCS_EQUIPMENT))
            need_charge_for_channel = (cell.maxcharge * (APC_CHANNEL_EQUIP_TRESHOLD + 5) * 0.01f) - cell.charge;
        else
            need_charge_for_channel = Dm.num(cell.used_charge());

        float charging_used = area != null ? Dm.num(area.energy_usage[AREA_USAGE_APC_CHARGE]) : 0;
        float remaining_charge_rate = min(cell.chargerate, cell.maxcharge * CHARGELEVEL) - charging_used;
        float need_charge = min(need_charge_for_channel, remaining_charge_rate) * Dm.num(seconds_per_tick);
        //check if we can charge the battery
        if (need_charge < 0)
            return;

        await charge_cell__proc(need_charge, cell: cell, grid_only: TRUE, channel: AREA_USAGE_APC_CHARGE);

        // show cell as fully charged if so
        if (cell.charge >= cell.maxcharge)
        {
            cell.charge = cell.maxcharge;
            charging = APC_FULLY_CHARGED;
        }
        else
            charging = APC_CHARGING;
    }

    // tg: code/modules/power/apc/apc_main.dm:724
    public virtual void reset(object wire = null)
    {
        object sw__1 = wire;
        if (Dm.eq(sw__1, WIRE_IDSCAN))
            locked = TRUE;
        else if (Dm.eq(sw__1, WIRE_POWER1) || Dm.eq(sw__1, WIRE_POWER2))
        {
            if (!Dm.truthy(wires.is_cut(WIRE_POWER1)) && !Dm.truthy(wires.is_cut(WIRE_POWER2)))
                shorted = FALSE;
        }
        else if (Dm.eq(sw__1, WIRE_AI))
        {
            if (!Dm.truthy(wires.is_cut(WIRE_AI)))
                aidisabled = FALSE;
        }
        else if (Dm.eq(sw__1, APC_RESET_EMP))
        {
            equipment = APC_CHANNEL_AUTO_ON;
            environ = APC_CHANNEL_AUTO_ON;
            update_appearance();
            update();
        }
    }

    // overload all the lights in this APC area
    // tg: code/modules/power/apc/apc_main.dm:741
    public virtual async DmTask overload_lighting()
    {
        if (!Dm.truthy(operating) || Dm.truthy(shorted))
            return;
        if (cell != null && Dm.truthy(await cell.use(0.02f * STANDARD_BATTERY_CHARGE)))
            Waiting.Detach(break_lights()); // INVOKE_ASYNC(src, PROC_REF(break_lights))
    }

    // tg: code/modules/power/apc/apc_main.dm:747
    public virtual async DmTask break_lights()
    {
        foreach (obj_machinery_light breaked_light in Dm.each_cast<obj_machinery_light>(get_lights()))
        {
            breaked_light.on = TRUE;
            await breaked_light.break_light_tube();
            CHECK_TICK();
        }
    }

    // tg: code/modules/power/apc/apc_main.dm:753
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(Dm.gt(exposed_temperature, 2000));
    }

    // tg: code/modules/power/apc/apc_main.dm:756
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        await take_damage(min(Dm.div(exposed_temperature, 100), 10), BURN);
        return null;
    }

    // tg: code/modules/power/apc/apc_main.dm:759
    public virtual object report()
    {
        return Dm.text("[] : []/[]/[] ([]) : [] ([])", area.name, equipment, lighting, environ, lastused_total, cell != null ? cell.percent() : "N/C", charging);
    }

    // tg: code/modules/power/apc/apc_main.dm:762
    [SignalHandler]
    public virtual void grey_tide(datum source = null, DmList grey_tide_areas = null)
    {
        if (!Dm.truthy(is_station_level(z)))
            return;

        foreach (object area_type in Dm.each(grey_tide_areas))
        {
            if (!istype(get_area(this), area_type as Type))
                continue;
            lighting = APC_CHANNEL_OFF; //Escape (or sneak in) under the cover of darkness
            update_appearance(UPDATE_ICON);
            update();
        }
    }

    ///Used for cell_5k apc helper, which installs 5k cell into apc.
    // tg: code/modules/power/apc/apc_main.dm:776
    public virtual void install_cell_5k()
    {
        cell_type = typeof(obj_item_stock_parts_power_store_battery_upgraded);
        cell = Dm.@new(cell_type, this) as obj_item_stock_parts_power_store;
    }

    /// Used for cell_10k apc helper, which installs 10k cell into apc.
    // tg: code/modules/power/apc/apc_main.dm:781
    public virtual void install_cell_10k()
    {
        cell_type = typeof(obj_item_stock_parts_power_store_battery_high);
        cell = Dm.@new(cell_type, this) as obj_item_stock_parts_power_store;
    }

    /// Used for unlocked apc helper, which unlocks the apc.
    // tg: code/modules/power/apc/apc_main.dm:786
    public virtual void unlock()
    {
        locked = FALSE;
    }

    /// Used for syndicate_access apc helper, which sets apc's required access to syndicate_access.
    // tg: code/modules/power/apc/apc_main.dm:790
    public virtual void give_syndicate_access()
    {
        req_access = Dm.list(ACCESS_SYNDICATE);
    }

    ///Used for away_general_access apc helper, which set apc's required access to away_general_access.
    // tg: code/modules/power/apc/apc_main.dm:794
    public virtual void give_away_general_access()
    {
        req_access = Dm.list(ACCESS_AWAY_GENERAL);
    }

    /// Used for no_charge apc helper, which sets apc charge to 0%.
    // tg: code/modules/power/apc/apc_main.dm:798
    public virtual void set_no_charge()
    {
        cell.charge = 0;
    }

    /// Used for full_charge apc helper, which sets apc charge to 100%.
    // tg: code/modules/power/apc/apc_main.dm:802
    public virtual void set_full_charge()
    {
        cell.charge = cell.maxcharge;
    }

    /// Returns the cell's current charge.
    // tg: code/modules/power/apc/apc_main.dm:806
    public virtual object charge()
    {
        return cell.charge;
    }

    /// Returns the amount of time it will take the APC at its current trickle charge rate to reach a charge level. If the APC is functionally not charging, returns null.
    // tg: code/modules/power/apc/apc_main.dm:817
    public virtual object time_to_charge(object joules = null)
    {
        object required_joules = Dm.sub(joules, charge());
        object trickle_charge_power = energy_to_power(area.energy_usage[AREA_USAGE_APC_CHARGE]);
        if (Dm.ge(trickle_charge_power, 1 * KILO * WATTS)) // require at least a bit of charging
            return round(Dm.num(energy_to_power(Dm.div(required_joules, trickle_charge_power))) * Dm.num(SSmachines.wait) + Dm.num(SSmachines.wait), Dm.num(SSmachines.wait));

        return null;
    }
}

// tg: code/modules/power/apc/apc_main.dm:233  the proc-level static of /obj/machinery/power/apc/Initialize (rules 10.6)
public sealed partial class TgState
{
    internal DmList obj_machinery_power_apc__Initialize__hovering_mob_typechecks = Dm.list(
        Dm.kv(typeof(mob_living_silicon), Dm.list(
            Dm.kv(SCREENTIP_CONTEXT_CTRL_LMB, "Toggle power"),
            Dm.kv(SCREENTIP_CONTEXT_ALT_LMB, "Toggle equipment power"),
            Dm.kv(SCREENTIP_CONTEXT_SHIFT_LMB, "Toggle lighting power"),
            Dm.kv(SCREENTIP_CONTEXT_CTRL_SHIFT_LMB, "Toggle environment power")
        ))
    );
}

/*Power module, used for APC construction*/
// tg: code/modules/power/apc/apc_main.dm:810  /obj/item/electronics/apc: var-only (its var block is generated)

// tg: code/modules/power/apc/apc_main.dm:284  (interleaved, rules 1.9)
public partial class obj_machinery_power_apc : obj_machinery_power
{
    // tg: code/modules/power/apc/apc_main.dm:284
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        await base.on_saboteur(source, disrupt_duration);
        // failure timer is in seconds, not deciseconds, so we need to convert
        energy_fail(Dm.num(disrupt_duration) * 0.1f);
        return TRUE;
    }
}

// tg: code/modules/power/apc/apc_main.dm:156  (interleaved, rules 1.9)
public partial class obj_machinery_power_apc : obj_machinery_power
{
    // tg: code/modules/power/apc/apc_main.dm:156
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        if (!Dm.truthy(auto_name))
            Dm.remove(ref dot, nameof(name));
        Dm.append(ref dot, nameof(opened));
        Dm.append(ref dot, nameof(coverlocked));
        Dm.append(ref dot, nameof(lighting));
        Dm.append(ref dot, nameof(equipment));
        Dm.append(ref dot, nameof(environ));

        Dm.append(ref dot, nameof(cell_type));
        if (cell_type != null)
        {
            start_charge = cell.charge / cell.maxcharge; // only used in Initialize() so direct edit is fine
            Dm.append(ref dot, nameof(start_charge));
        }
        return dot;
    }
}
