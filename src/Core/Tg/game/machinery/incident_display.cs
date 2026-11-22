// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/incident_display.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the engineering package E2, D287): the delamination and tram incident displays, their digits, trend arrows,
// shame screen, SAFETY MOTH advert, light, repair, multitool mode switch and EMP scramble. Left out with a comment or a marked leaf:
// the emissive overlays (DEVIATION DV12: the emissive plane is not ported); SStransport's tram collisions (the trams are not ported; no MetaStation
// tram). The var block is generated (D111).

/**
 * List of incident displays on the map
 * Required as persistence subsystem loads after the ones present at mapload, and to reset to 0 upon explosion.
 */
// tg: code/game/machinery/incident_display.dm:5  GLOBAL_LIST_EMPTY(map_incident_displays)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/game/machinery/incident_display.dm:5  GLOBAL_LIST_EMPTY(map_incident_displays)
    public DmList map_incident_displays;

    // tg: code/game/machinery/incident_display.dm:5  (GLOBAL_MANAGED(map_incident_displays, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmap_incident_displays()
    {
        map_incident_displays = Dm.list();
        Dm.append(ref gvars_datum_init_order, "map_incident_displays");
    }
}

// tg: code/game/machinery/incident_display.dm:37
public partial class obj_machinery_incident_display : obj_machinery
{
    /// Display days since last delam on incident sign
    // tg: code/game/machinery/incident_display.dm:8  #define DISPLAY_DELAM (1<<0) (file-local, rules 11.4)
    private const int DISPLAY_DELAM = 1 << 0;
    /// Display current number of tram hits on incident sign
    // tg: code/game/machinery/incident_display.dm:10  #define DISPLAY_TRAM (1<<1)
    private const int DISPLAY_TRAM = 1 << 1;

    // tg: code/game/machinery/incident_display.dm:17-18  #define TREND_RISING, TREND_FALLING
    private const string TREND_RISING = "rising";
    private const string TREND_FALLING = "falling";

    // tg: code/game/machinery/incident_display.dm:20-24  #define NAME_DELAM, NAME_TRAM, DESC_DELAM, DESC_TRAM
    private const string NAME_DELAM = "delamination incident display";
    private const string NAME_TRAM = "tram incident display";
    private const string DESC_DELAM = "A signs describe how long it's been since the last delamination incident. Features an advert for SAFETY MOTH.";
    private const string DESC_TRAM = "A display that provides the number of tram related safety incidents this shift.";

    // tg: code/game/machinery/incident_display.dm:26-31  #define DISPLAY_PIXEL_1_W ... DISPLAY_PIXEL_ALPHA
    private const int DISPLAY_PIXEL_1_W = 21;
    private const int DISPLAY_PIXEL_1_Z = -2;
    private const int DISPLAY_PIXEL_2_W = 16;
    private const int DISPLAY_PIXEL_2_Z = -2;

    // tg: code/game/machinery/incident_display.dm:34-35  #define LIGHT_COLOR_NORMAL, LIGHT_COLOR_SHAME
    private const string LIGHT_COLOR_NORMAL = "#4b4290";
    private const string LIGHT_COLOR_SHAME = "#e24e76";

    // tg: code/game/machinery/incident_display.dm:94
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        register_context();
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/machinery/incident_display.dm:99
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        Dm.append(ref GLOB.map_incident_displays, this);
        update_delam_count(SSpersistence.rounds_since_engine_exploded, SSpersistence.delam_highscore);
        // RegisterSignal(SStransport, COMSIG_TRAM_COLLISION, PROC_REF(update_tram_count)): SStransport and the trams are not ported

        update_appearance();
    }

    // tg: code/game/machinery/incident_display.dm:107
    public override object Destroy(float? force = null)
    {
        GLOB.map_incident_displays.Remove(this);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/incident_display.dm:111
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(Dm.bitor(NOPOWER, BROKEN), MAINT))))
            return null;

        // COOLDOWN_FINISHED(src, advert_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (configured_advert != null && advert_cooldown <= world.time) // time to show an advert
        {
            show_advert(advert: configured_advert, duration: configured_advert_duration);
            // COOLDOWN_START(src, advert_cooldown, rand(advert_frequency - 5 SECONDS, advert_frequency + 5 SECONDS))
            advert_cooldown = world.time + rand(advert_frequency - 5 * SECONDS, advert_frequency + 5 * SECONDS);
            return null;
        }

        if (!Dm.truthy(live_display)) // displaying static content, no processing required
            return null;

        if (active_advert <= world.time) // advert finished, revert to static content
        {
            active_advert = 0; // COOLDOWN_RESET(src, active_advert)
            live_display = FALSE;
            update_appearance();
        }
        return null;
    }

    // tg: code/game/machinery/incident_display.dm:128
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (!isliving(user))
            return null;

        mob_living living_user = (mob_living)user;

        if (Dm.eq(held_item?.tool_behaviour, TOOL_WELDER) && !Dm.truthy(living_user.combat_mode) && Dm.num(atom_integrity) < max_integrity)
            context[SCREENTIP_CONTEXT_LMB] = "repair display";

        if (Dm.eq(held_item?.tool_behaviour, TOOL_MULTITOOL) && !Dm.truthy(living_user.combat_mode))
        {
            if (sign_features == DISPLAY_TRAM)
                context[SCREENTIP_CONTEXT_LMB] = "change to delam mode";
            else
                context[SCREENTIP_CONTEXT_LMB] = "change to tram mode";
        }

        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/machinery/incident_display.dm:145
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return FALSE;

        if (Dm.num(atom_integrity) >= max_integrity && !Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            balloon_alert(user, I18n.T("it doesn't need repairs!"));
            return TRUE;
        }

        balloon_alert(user, I18n.T("repairing display..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, amount: 0, volume: 50)))
            return TRUE;

        balloon_alert(user, I18n.T("repaired"));
        atom_integrity = max_integrity;
        await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(BROKEN)));
        update_appearance();
        return TRUE;
    }

    // Switch modes with multitool
    // tg: code/game/machinery/incident_display.dm:164
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return FALSE;

        if (sign_features == DISPLAY_TRAM)
        {
            tool.play_tool_sound(this);
            balloon_alert(user, I18n.T("set to delam"));
            name = NAME_DELAM;
            desc = DESC_DELAM;
            sign_features = DISPLAY_DELAM;
            update_delam_count(SSpersistence.rounds_since_engine_exploded, SSpersistence.delam_highscore);
            update_appearance();
            return TRUE;
        }
        else
        {
            tool.play_tool_sound(this);
            balloon_alert(user, I18n.T("set to tram"));
            name = NAME_TRAM;
            desc = DESC_TRAM;
            sign_features = DISPLAY_TRAM;
            update_tram_count(this, SSpersistence.tram_hits_this_round);
            update_appearance();
            return TRUE;
        }
    }

    // EMP causes the display to display random numbers or outright break.
    // tg: code/game/machinery/incident_display.dm:188
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (prob(50))
        {
            await set_machine_stat(Dm.bitor(machine_stat, BROKEN));
            update_appearance();
            return dot;
        }

        hit_threshold = rand(1, 99);
        hit_count = rand(1, 99);

        if (prob(33))
        {
            last_delam = 0;
            delam_record = 0;
        }
        else
        {
            last_delam = rand(1, 99);
            delam_record = rand(1, 99);
        }

        update_appearance();
        return dot;
    }

    // tg: code/game/machinery/incident_display.dm:207
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_mineral_titanium>(drop_location(), 2);
        Dm.@new<obj_item_shard>(drop_location());
        Dm.@new<obj_item_shard>(drop_location());
        return null;
    }

    /**
     * Update the delamination count on the display
     *
     * Use the provided args to update the incident display when in delam mode.
     * Arguments:
     * * new_count - number of shifts without a delam
     * * record - current high score for the delam count
     */
    // tg: code/game/machinery/incident_display.dm:220
    public virtual void update_delam_count(object new_count = null, object record = null)
    {
        delam_record = Dm.num(record);
        last_delam = Dm.num(Dm.min(new_count, 199));
        update_appearance();
    }

    /**
     * Update the tram hit count on the display
     *
     * Sign receives a signal from SStransport that the tram has hit someone, and updates the count.
     * Arguments:
     * * source - hopefully is the transport subsystem
     * * tram_collisions - current number of tram hits
     */
    // tg: code/game/machinery/incident_display.dm:233
    [SignalHandler]
    public virtual void update_tram_count(object source = null, object tram_collisions = null)
    {
        hit_count = Dm.num(Dm.min(tram_collisions, 199));
        update_appearance();
    }

    /**
     * Run an animated advert on the display
     *
     * Arguments:
     * * advert - icon state to flick to
     * * duration - length of the advert animation
     */
    // tg: code/game/machinery/incident_display.dm:246
    public virtual void show_advert(object advert = null, object duration = null)
    {
        duration ??= 7 * SECONDS;
        // COOLDOWN_START(src, active_advert, duration) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        active_advert = world.time + Dm.num(duration);
        live_display = TRUE;
        update_appearance();
        flick(advert, this);
    }

    // tg: code/game/machinery/incident_display.dm:252
    public override async DmTask on_set_machine_stat(object old_value = null)
    {
        await base.on_set_machine_stat(old_value);
        update_appearance();
    }

    // tg: code/game/machinery/incident_display.dm:256
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_appearance(updates);
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            icon_state = "display_normal";
            Waiting.NoSleep(set_light(l_on: FALSE));
            return dot;
        }
        else if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            icon_state = "display_broken";
            Waiting.NoSleep(set_light(l_range: 1.7f, l_power: 1.5f, l_color: LIGHT_COLOR_NORMAL, l_on: TRUE));
        }
        else if (Dm.truthy(Dm.bitand(sign_features, DISPLAY_DELAM)) && last_delam <= 0) // you done fucked up
        {
            icon_state = "display_shame";
            Waiting.NoSleep(set_light(l_range: 1.7f, l_power: 1.5f, l_color: LIGHT_COLOR_SHAME, l_on: TRUE));
        }
        else
        {
            icon_state = "display_normal";
            Waiting.NoSleep(set_light(l_range: 1.7f, l_power: 1.5f, l_color: LIGHT_COLOR_NORMAL, l_on: TRUE));
        }
        return dot;
    }

    // tg: code/game/machinery/incident_display.dm:272
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return dot;

        // DEVIATION(DV12): emissive_appearance(icon, "display_emissive", src, alpha = DISPLAY_BASE_ALPHA) and the emissive copy of each
        // overlay below (__HELPERS/lighting.dm:2), the display's glow on the emissive plane, are not created (lighting planes and
        // emissives are not ported).

        if (active_advert != 0) // COOLDOWN_STARTED(src, active_advert): we don't show the static content during adverts
            return dot;

        if (Dm.truthy(Dm.bitand(sign_features, DISPLAY_DELAM)))
        {
            Dm.append(ref dot, mutable_appearance(icon, "overlay_delam"));

            float delam_pos1 = Dm.mod(clamp(last_delam, 0, 199), 10);
            mutable_appearance delam_pos1_overlay = mutable_appearance(icon, Dm.text("num_[]", delam_pos1)) as mutable_appearance;
            delam_pos1_overlay.color = delam_display_color;
            delam_pos1_overlay.pixel_w = DISPLAY_PIXEL_1_W;
            delam_pos1_overlay.pixel_z = DISPLAY_PIXEL_1_Z;
            Dm.append(ref dot, delam_pos1_overlay);

            float delam_pos2 = Dm.mod(clamp(last_delam, 0, 199) / 10, 10);
            mutable_appearance delam_pos2_overlay = mutable_appearance(icon, Dm.text("num_[]", delam_pos2)) as mutable_appearance;
            delam_pos2_overlay.color = delam_display_color;
            delam_pos2_overlay.pixel_w = DISPLAY_PIXEL_2_W;
            delam_pos2_overlay.pixel_z = DISPLAY_PIXEL_2_Z;
            Dm.append(ref dot, delam_pos2_overlay);

            if (last_delam >= 100)
                Dm.append(ref dot, mutable_appearance(icon, "num_100_red"));

            if (last_delam == delam_record)
            {
                mutable_appearance delam_trend_overlay = mutable_appearance(icon, TREND_RISING) as mutable_appearance;
                delam_trend_overlay.color = COLOR_DISPLAY_GREEN;
                Dm.append(ref dot, delam_trend_overlay);
            }
            else
            {
                mutable_appearance delam_trend_overlay = mutable_appearance(icon, TREND_FALLING) as mutable_appearance;
                delam_trend_overlay.color = COLOR_DISPLAY_RED;
                Dm.append(ref dot, delam_trend_overlay);
            }
        }

        if (Dm.truthy(Dm.bitand(sign_features, DISPLAY_TRAM)))
        {
            Dm.append(ref dot, mutable_appearance(icon, "overlay_tram"));

            float tram_pos1 = Dm.mod(hit_count, 10);
            mutable_appearance tram_pos1_overlay = mutable_appearance(icon, Dm.text("num_[]", tram_pos1)) as mutable_appearance;
            tram_pos1_overlay.color = tram_display_color;
            tram_pos1_overlay.pixel_w = DISPLAY_PIXEL_1_W;
            tram_pos1_overlay.pixel_z = DISPLAY_PIXEL_1_Z;
            Dm.append(ref dot, tram_pos1_overlay);

            float tram_pos2 = Dm.mod(hit_count / 10, 10);
            mutable_appearance tram_pos2_overlay = mutable_appearance(icon, Dm.text("num_[]", tram_pos2)) as mutable_appearance;
            tram_pos2_overlay.color = tram_display_color;
            tram_pos2_overlay.pixel_w = DISPLAY_PIXEL_2_W;
            tram_pos2_overlay.pixel_z = DISPLAY_PIXEL_2_Z;
            Dm.append(ref dot, tram_pos2_overlay);

            if (hit_count >= 100)
                Dm.append(ref dot, mutable_appearance(icon, "num_100_blue"));

            if (hit_count > SSpersistence.tram_hits_last_round)
            {
                mutable_appearance tram_trend_overlay = mutable_appearance(icon, TREND_RISING) as mutable_appearance;
                tram_trend_overlay.color = COLOR_DISPLAY_RED;
                Dm.append(ref dot, tram_trend_overlay);
            }
            else
            {
                mutable_appearance tram_trend_overlay = mutable_appearance(icon, TREND_FALLING) as mutable_appearance;
                tram_trend_overlay.color = COLOR_DISPLAY_GREEN;
                Dm.append(ref dot, tram_trend_overlay);
            }
        }
        return dot;
    }

    // tg: code/game/machinery/incident_display.dm:368
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.num(atom_integrity) < max_integrity)
            Dm.append(ref dot, span_notice(I18n.T("It can be repaired with a [].", EXAMINE_HINT("welder"))));

        if (Dm.truthy(Dm.bitand(sign_features, DISPLAY_DELAM)))
        {
            Dm.append(ref dot, span_notice(I18n.T("It can be changed to display tram hits with a [].", EXAMINE_HINT("multitool"))));
            if (last_delam >= 0)
            {
                Dm.append(ref dot, span_info(I18n.T("It has been [] shift\\s since the last delamination event at this Nanotrasen facility.", last_delam)));
                if (last_delam == 0)
                    Dm.append(ref dot, span_info(I18n.T("Let's do better today.<br/>")));
                else if (last_delam >= 1 && last_delam <= 5)
                    Dm.append(ref dot, span_info(I18n.T("There's room for improvement.<br/>")));
                else if (last_delam >= 6 && last_delam <= 10)
                    Dm.append(ref dot, span_info(I18n.T("Good work!<br/>")));
                else if (last_delam == 69)
                    Dm.append(ref dot, span_info(I18n.T("Nice.<br/>")));
                else
                    Dm.append(ref dot, span_info(I18n.T("Incredible!<br/>")));
            }
            else
                Dm.append(ref dot, span_info(I18n.T("The supermatter crystal has delaminated, in case you didn't notice.")));
        }

        if (Dm.truthy(Dm.bitand(sign_features, DISPLAY_TRAM)))
        {
            Dm.append(ref dot, span_notice(I18n.T("It can be changed to display delam-free shifts with a [].", EXAMINE_HINT("multitool"))));
            Dm.append(ref dot, span_info(I18n.T("The station has had [] tram incident\\s this shift.", hit_count)));
            if (hit_count == 0)
                Dm.append(ref dot, span_info(I18n.T("Fantastic! Champions of safety.<br/>")));
            else if (hit_count == 1)
                Dm.append(ref dot, span_info(I18n.T("Let's do better tomorrow.<br/>")));
            else if (hit_count >= 2 && hit_count <= 5)
                Dm.append(ref dot, span_info(I18n.T("There's room for improvement.<br/>")));
            else if (hit_count >= 6 && hit_count <= 10)
                Dm.append(ref dot, span_info(I18n.T("Good work! Nanotrasen's finest!<br/>")));
            else if (hit_count == 69)
                Dm.append(ref dot, span_info(I18n.T("Nice.<br/>")));
            else
                Dm.append(ref dot, span_info(I18n.T("Incredible! You're probably reading this from medbay.<br/>")));
        }
        return dot;
    }
}
