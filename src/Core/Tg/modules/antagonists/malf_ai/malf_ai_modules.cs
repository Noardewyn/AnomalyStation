// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/malf_ai/malf_ai_modules.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI8, D326): the malfunctioning AI's modules, their actions, the doomsday device and the voice changer.
// The var blocks are generated (D111).

// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1-10  the file's defines (#undef'd at its end)
internal static class MalfDefines
{
    public const float DEFAULT_DOOMSDAY_TIMER = 4500;
    public const float DOOMSDAY_ANNOUNCE_INTERVAL = 600;
    public const float VENDOR_TIPPING_USES = 8;
    public const float MALF_VENDOR_TIPPING_TIME = 0.5f * 10; //within human reaction time
    public const float MALF_VENDOR_TIPPING_CRIT_CHANCE = 100; //percent - guaranteed
    public const float MALF_AI_ROLL_TIME = 0.5f * 10;
    public const float MALF_AI_ROLL_COOLDOWN = 1 * 10 + MALF_AI_ROLL_TIME;
    public const float MALF_AI_ROLL_DAMAGE = 75;
    public const float MALF_AI_ROLL_CRIT_CHANCE = 5; //percent
}

public partial class datum_controller_global_vars
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:12  GLOBAL_LIST_INIT(blacklisted_malf_machines, typecacheof(list(...)))
    public DmList blacklisted_malf_machines;

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:12  (GLOBAL_MANAGED(blacklisted_malf_machines, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalblacklisted_malf_machines()
    {
        blacklisted_malf_machines = (DmList)typecacheof(Dm.list(
            typeof(obj_machinery_field_containment),
            typeof(obj_machinery_power_supermatter_crystal),
            typeof(obj_machinery_gravity_generator),
            typeof(obj_machinery_doomsday_device),
            typeof(obj_machinery_nuclearbomb),
            typeof(obj_machinery_nuclearbomb_selfdestruct),
            typeof(obj_machinery_nuclearbomb_syndicate),
            typeof(obj_machinery_syndicatebomb),
            typeof(obj_machinery_syndicatebomb_badmin),
            typeof(obj_machinery_syndicatebomb_badmin_clown),
            typeof(obj_machinery_syndicatebomb_empty),
            typeof(obj_machinery_syndicatebomb_self_destruct),
            typeof(obj_machinery_syndicatebomb_training),
            typeof(obj_machinery_atmospherics_pipe_layer_manifold),
            typeof(obj_machinery_atmospherics_pipe_multiz),
            typeof(obj_machinery_atmospherics_pipe_smart),
            typeof(obj_machinery_atmospherics_pipe_smart_manifold), //mapped one
            typeof(obj_machinery_atmospherics_pipe_smart_manifold4w), //mapped one
            typeof(obj_machinery_atmospherics_pipe_color_adapter),
            typeof(obj_machinery_atmospherics_pipe_bridge_pipe),
            typeof(obj_machinery_atmospherics_pipe_heat_exchanging_simple),
            typeof(obj_machinery_atmospherics_pipe_heat_exchanging_junction),
            typeof(obj_machinery_atmospherics_pipe_heat_exchanging_manifold),
            typeof(obj_machinery_atmospherics_pipe_heat_exchanging_manifold4w),
            typeof(obj_machinery_atmospherics_components_tank),
            typeof(obj_machinery_atmospherics_components_unary_portables_connector),
            typeof(obj_machinery_atmospherics_components_unary_passive_vent),
            typeof(obj_machinery_atmospherics_components_unary_heat_exchanger),
            typeof(obj_machinery_atmospherics_components_unary_hypertorus_core),
            typeof(obj_machinery_atmospherics_components_unary_hypertorus_waste_output),
            typeof(obj_machinery_atmospherics_components_unary_hypertorus_moderator_input),
            typeof(obj_machinery_atmospherics_components_unary_hypertorus_fuel_input),
            typeof(obj_machinery_hypertorus_interface),
            typeof(obj_machinery_hypertorus_corner),
            typeof(obj_machinery_atmospherics_components_binary_valve),
            typeof(obj_machinery_portable_atmospherics_canister),
            typeof(obj_machinery_computer_shuttle),
            typeof(obj_machinery_computer_emergency_shuttle),
            typeof(obj_machinery_computer_gateway_control),
            typeof(obj_machinery_ai_law_rack)));
        Dm.append(ref gvars_datum_init_order, "blacklisted_malf_machines");
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:56  GLOBAL_LIST_INIT(malf_modules, subtypesof(/datum/ai_module/malf))
    public DmList malf_modules;

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:56  (GLOBAL_MANAGED(malf_modules, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmalf_modules()
    {
        malf_modules = subtypesof(typeof(datum_ai_module_malf));
        Dm.append(ref gvars_datum_init_order, "malf_modules");
    }
}

/// The malf AI action subtype. All malf actions are subtypes of this.
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:59
public partial class datum_action_innate_ai : datum_action_innate
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:74
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        if (!isAI(owner))
        {
            // WARNING(MSG) (__HELPERS/logging/_logging.dm:2), expanded inline (rules 11.1 (d), (f): src, usr, __FILE__, __LINE__)
            warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("AI action [] attempted to grant itself to non-AI mob []!", name, key_name(grant_to)), "code/modules/antagonists/malf_ai/malf_ai_modules.dm", 77, this, usr));
            qdel(this);
        }
        else
            owner_AI = (mob_living_silicon_ai)owner;
        return dot;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:82
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        if (owner_AI != null && !(owner_AI.malf_cooldown <= world.time)) // COOLDOWN_FINISHED(owner_AI, malf_cooldown)
            return FALSE;
        return await base.IsAvailable(feedback);
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:87
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(auto_use_uses))
            adjust_uses(-1);
        if (Dm.truthy(cooldown_period))
            owner_AI.malf_cooldown = world.time + cooldown_period; // COOLDOWN_START(owner_AI, malf_cooldown, cooldown_period)
        return dot;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:96
    public virtual void adjust_uses(float amt, object silent = null)
    {
        uses += amt;
        if (!Dm.truthy(silent) && Dm.truthy(uses))
            to_chat(owner, span_notice(I18n.T("[] now has <b>[]</b> use[] remaining.", name, uses, uses > 1 ? "s" : "")));
        if (uses <= 0)
        {
            if (Dm.initial(GetType(), (datum_action_innate_ai a) => a.uses) > 1) //no need to tell 'em if it was one-use anyway!
                to_chat(owner, span_warning(I18n.T("[] has run out of uses!", name)));
            qdel(this);
        }
    }

    // the "[desc] It has [uses] use\s remaining." the counted actions keep in their description (their New and do_ability)
    protected void describe_uses() => desc = Dm.text("[] It has [] use[] remaining.", Dm.initial(GetType(), (datum_action a) => a.desc), uses, uses == 1 ? "" : "s");
}

/// Framework for ranged abilities that can have different effects by left-clicking stuff.
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:106
public partial class datum_action_innate_ai_ranged : datum_action_innate_ai
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:111
    public override void adjust_uses(float amt, object silent = null)
    {
        uses += amt;
        if (!Dm.truthy(silent) && Dm.truthy(uses))
            to_chat(owner, span_notice(I18n.T("[] now has <b>[]</b> use[] remaining.", name, uses, uses == 1 ? "" : "s")));
        if (!Dm.truthy(uses))
        {
            if (Dm.initial(GetType(), (datum_action_innate_ai a) => a.uses) > 1) //no need to tell 'em if it was one-use anyway!
                to_chat(owner, span_warning(I18n.T("[] has run out of uses!", name)));
            Waiting.Detach(Remove(owner));
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 10 * SECONDS > GC_FILTER_QUEUE ? WEAKREF(this) : this), 10 * SECONDS, file: "code/modules/antagonists/malf_ai/malf_ai_modules.dm", line: 119); // QDEL_IN(src, 10 SECONDS): let any active timers on us finish up
        }
    }
}

/// The base module type, which holds info about each ability.
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:122
public partial class datum_ai_module : datum
{
    /// Applies upgrades
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:144
    public virtual async DmTask upgrade__proc(mob_living_silicon_ai AI = null)
    {
        await DmTask.CompletedTask;
    }
}

/// Doomsday Device: Starts the self-destruct timer. It can only be stopped by killing the AI completely.
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:178
public partial class datum_action_innate_ai_nuke_station : datum_action_innate_ai
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:185
    public override async DmTask<object> Activate()
    {
        turf T = get_turf(owner);
        if (T == null || !Dm.truthy(is_station_level(T.z)))
        {
            to_chat(owner, span_warning(I18n.T("You cannot activate the doomsday device while off-station!")));
            return null;
        }
        if (!Dm.eq(await tgui_alert(owner, "Send arming signal? (true = arm, false = cancel)", "purge_all_life()", Dm.list("confirm = TRUE;", "confirm = FALSE;")), "confirm = TRUE;"))
            return null;
        if (Dm.truthy(active) || owner_AI.stat == DEAD)
            return null; //prevent the AI from activating an already active doomsday or while they are dead
        if (!isturf(owner_AI.loc))
            return null; //prevent AI from activating doomsday while shunted or carded, fucking abusers
        active = TRUE;
        Waiting.Detach(set_up_us_the_bomb(owner as mob_living)); // set waitfor = FALSE
        return null;
    }

    // still connected: the AI exists and sits in its core (tg's repeated QDELETED(owner) || !isturf(owner_AI.loc) check)
    private bool lost_link(mob_living owner)
    {
        if (QDELETED(owner) || !isturf(owner_AI.loc))
        {
            active = FALSE;
            return true;
        }
        return false;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:200
    public virtual async DmTask set_up_us_the_bomb(mob_living owner = null)
    {
        //oh my GOD.
        message_admins(Dm.text("[][] has activated AI Doomsday.", key_name_admin(owner), ADMIN_FLW(owner)));
        string pass = prob(10) ? "******" : "hunter2";
        to_chat(owner, "<span class='small bolddanger'>run -o -a 'selfdestruct'</span>");
        await sleep(0.5f * SECONDS);
        if (lost_link(owner))
            return;
        to_chat(owner, "<span class='small bolddanger'>Running executable 'selfdestruct'...</span>");
        await sleep(rand(10, 30));
        if (lost_link(owner))
            return;
        owner.playsound_local(owner, Dm.resource("sound/announcer/alarm/bloblarm.ogg"), 50, 0, use_reverb: FALSE);
        to_chat(owner, span_userdanger(I18n.T("!!! UNAUTHORIZED SELF-DESTRUCT ACCESS !!!")));
        to_chat(owner, span_bolddanger(I18n.T("This is a class-3 security violation. This incident will be reported to Central Command.")));
        for (int i = 1; i <= 3; i++)
        {
            await sleep(2 * SECONDS);
            if (lost_link(owner))
                return;
            to_chat(owner, span_bolddanger(I18n.T("Sending security report to Central Command.....[]%", rand(0, 9) + (rand(20, 30) * i))));
        }
        await sleep(0.3f * SECONDS);
        if (lost_link(owner))
            return;
        to_chat(owner, Dm.text("<span class='small bolddanger'>auth 'akjv9c88asdf12nb' []</span>", pass));
        owner.playsound_local(owner, Dm.resource("sound/items/timer.ogg"), 50, 0, use_reverb: FALSE);
        await sleep(3 * SECONDS);
        if (lost_link(owner))
            return;
        to_chat(owner, span_boldnotice(I18n.T("Credentials accepted. Welcome, akjv9c88asdf12nb.")));
        owner.playsound_local(owner, Dm.resource("sound/misc/server-ready.ogg"), 50, 0, use_reverb: FALSE);
        await sleep(0.5f * SECONDS);
        if (lost_link(owner))
            return;
        to_chat(owner, span_boldnotice(I18n.T("Arm self-destruct device? (Y/N)")));
        owner.playsound_local(owner, Dm.resource("sound/machines/compiler/compiler-stage1.ogg"), 50, 0, use_reverb: FALSE);
        await sleep(2 * SECONDS);
        if (lost_link(owner))
            return;
        to_chat(owner, "<span class='small bolddanger'>Y</span>");
        await sleep(1.5f * SECONDS);
        if (lost_link(owner))
            return;
        to_chat(owner, span_boldnotice(I18n.T("Confirm arming of self-destruct device? (Y/N)")));
        owner.playsound_local(owner, Dm.resource("sound/machines/compiler/compiler-stage2.ogg"), 50, 0, use_reverb: FALSE);
        await sleep(1 * SECONDS);
        if (lost_link(owner))
            return;
        to_chat(owner, "<span class='small bolddanger'>Y</span>");
        await sleep(rand(15, 25));
        if (lost_link(owner))
            return;
        to_chat(owner, span_boldnotice(I18n.T("Please repeat password to confirm.")));
        owner.playsound_local(owner, Dm.resource("sound/machines/compiler/compiler-stage2.ogg"), 50, 0, use_reverb: FALSE);
        await sleep(1.4f * SECONDS);
        if (lost_link(owner))
            return;
        to_chat(owner, Dm.text("<span class='small bolddanger'>[]</span>", pass));
        await sleep(4 * SECONDS);
        if (lost_link(owner))
            return;
        to_chat(owner, span_boldnotice(I18n.T("Credentials accepted. Transmitting arming signal...")));
        owner.playsound_local(owner, Dm.resource("sound/misc/server-ready.ogg"), 50, 0, use_reverb: FALSE);
        await sleep(3 * SECONDS);
        if (lost_link(owner))
            return;
        if (owner_AI.stat != DEAD)
        {
            priority_announce(I18n.T("Hostile runtimes detected in all station systems, please deactivate your AI to prevent possible damage to its morality core."), I18n.T("Anomaly Alert"), ANNOUNCER_AIMALF);
            SSsecurity_level.set_level(SEC_LEVEL_DELTA);
            var DOOM = Dm.@new<obj_machinery_doomsday_device>(owner_AI);
            owner_AI.nuking = TRUE;
            owner_AI.doomsday_device = DOOM;
            owner_AI.doomsday_device.start();
            foreach (obj_item_pinpointer_nuke P in Dm.each<obj_item_pinpointer_nuke>(GLOB.pinpointer_list))
                P.switch_mode_to(TRACK_MALF_AI); //Pinpointers start tracking the AI wherever it goes

            notify_ghosts(
                Dm.text("[] has activated a Doomsday Device!", owner_AI),
                source: owner_AI,
                header: "DOOOOOOM!!!");

            qdel(this);
        }
    }
}

// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:296
public partial class obj_machinery_doomsday_device : obj_machinery
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:310
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!isAI(loc))
        {
            stack_trace("Doomsday created outside an AI somehow, shit's fucking broke. Anyway, we're just gonna qdel now. Go make a github issue report.", "code/modules/antagonists/malf_ai/malf_ai_modules.dm", 313);
            return INITIALIZE_HINT_QDEL;
        }
        owner = (mob_living_silicon_ai)loc;
        countdown = Dm.@new<obj_effect_countdown_doomsday>(this);
        return dot;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:318
    public override object Destroy(float? force = null)
    {
        timing = FALSE;
        QDEL_NULL(ref countdown);
        STOP_PROCESSING(SSfastprocess, this);
        SSshuttle.clearHostileEnvironment(this);
        SSmapping.remove_nuke_threat(this);
        SSsecurity_level.set_level(SEC_LEVEL_RED);
        foreach (mob_living_silicon_robot borg in Dm.each<mob_living_silicon_robot>(owner?.connected_robots))
        {
            borg.lamp_doom = FALSE;
            Waiting.Detach(borg.toggle_headlamp(FALSE, TRUE)); //forces borg lamp to update
        }
        if (owner != null)
        {
            owner.doomsday_device = null;
            owner.nuking = null;
        }
        owner = null;
        foreach (obj_item_pinpointer_nuke P in Dm.each<obj_item_pinpointer_nuke>(GLOB.pinpointer_list))
        {
            P.switch_mode_to(TRACK_NUKE_DISK); //Party's over, back to work, everyone
            P.alert = FALSE;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:339
    public virtual void start()
    {
        detonation_timer = world.time + MalfDefines.DEFAULT_DOOMSDAY_TIMER;
        next_announce = world.time + MalfDefines.DOOMSDAY_ANNOUNCE_INTERVAL;
        timing = TRUE;
        countdown.start();
        START_PROCESSING(SSfastprocess, this);
        SSshuttle.registerHostileEnvironment(this);
        SSmapping.add_nuke_threat(this); //This causes all blue "circuit" tiles on the map to change to animated red icon state.
        foreach (mob_living_silicon_robot borg in Dm.each<mob_living_silicon_robot>(owner.connected_robots))
        {
            borg.lamp_doom = TRUE;
            Waiting.Detach(borg.toggle_headlamp(FALSE, TRUE)); //forces borg lamp to update
        }
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:351
    public virtual float seconds_remaining() => max(0, Dm.round((Dm.num(detonation_timer) - world.time) / 10));

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:354
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        turf T = get_turf(this);
        if (T == null || !Dm.truthy(is_station_level(T.z)))
        {
            minor_announce(I18n.T("DOOMSDAY DEVICE OUT OF STATION RANGE, ABORTING"), "ERROR ER0RR $R0RRO$!R41.%%!!(%$^^__+ @#F0E4", TRUE);
            owner.ShutOffDoomsdayDevice();
            return null;
        }
        if (!Dm.truthy(timing))
        {
            STOP_PROCESSING(SSfastprocess, this);
            return null;
        }
        float sec_left = seconds_remaining();
        if (sec_left == 0)
        {
            timing = FALSE;
            sound_to_playing_players(Dm.resource("sound/announcer/alarm/nuke_alarm.ogg"), 70);
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, nameof(play_cinematic), typeof(datum_cinematic_malf), World.Current, Dm.@new<datum_callback>(this, nameof(trigger_doomsday))), 10 * SECONDS, file: "code/modules/antagonists/malf_ai/malf_ai_modules.dm", line: 367);
        }
        else if (world.time >= Dm.num(next_announce))
        {
            minor_announce(I18n.T("[] SECONDS UNTIL DOOMSDAY DEVICE ACTIVATION!", sec_left), "ERROR ER0RR $R0RRO$!R41.%%!!(%$^^__+ @#F0E4", TRUE);
            next_announce = Dm.num(next_announce) + MalfDefines.DOOMSDAY_ANNOUNCE_INTERVAL;
        }
        return null;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:373
    public virtual async DmTask trigger_doomsday()
    {
        await callback_on_everyone_on_z(SSmapping.levels_by_trait(ZTRAIT_STATION) as DmList, Dm.@new<datum_callback>(GLOBAL_PROC, nameof(bring_doomsday)), this);
        to_chat(World.Current, span_bold(I18n.T("The AI cleansed the station of life with []!", this)));
        SSticker.force_ending = FORCE_END_ROUND;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:378
    public static object bring_doomsday(mob_living victim = null, atom source = null)
    {
        if (issilicon(victim))
            return FALSE;

        to_chat(victim, span_userdanger(I18n.T("The blast wave from [] tears you atom from atom!", source)));
        victim.investigate_log("has been dusted by a doomsday device.", INVESTIGATE_DEATHS);
        Waiting.Detach(victim.dust());
        return TRUE;
    }

    /// For Lockdown malf AI ability. Opens all doors on the station.
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:427
    public static void _malf_ai_undo_lockdown()
    {
        foreach (obj_machinery_door locked_down in Dm.each<obj_machinery_door>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door))))
        {
            if (QDELETED(locked_down) || !Dm.truthy(is_station_level(locked_down.z)))
                continue;
            Waiting.Detach(locked_down.disable_lockdown()); // INVOKE_ASYNC
        }

        // Clear the lockdown emergency display
        clear_status_display_lockdown();
    }
}

/// Hostile Station Lockdown: Locks, bolts, and electrifies every airlock on the station. After 90 seconds, the doors reset.
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:396
public partial class datum_action_innate_ai_lockdown : datum_action_innate_ai
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:405
    public override async DmTask<object> IsAvailable(float? feedback = null)
        => Dm.truthy(await base.IsAvailable(feedback)) && !Dm.truthy(hack_in_progress) ? TRUE : FALSE;

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:408
    public override async DmTask<object> Activate()
    {
        hack_in_progress = TRUE;
        foreach (obj_machinery_door locked_down in Dm.each<obj_machinery_door>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door))))
        {
            if (QDELETED(locked_down) || !Dm.truthy(is_station_level(locked_down.z)))
                continue;
            Waiting.Detach(locked_down.hostile_lockdown(owner)); // INVOKE_ASYNC
        }

        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, nameof(_malf_ai_undo_lockdown)), 90 * SECONDS, file: "code/modules/antagonists/malf_ai/malf_ai_modules.dm", line: 415);

        // Set status displays to lockdown alert
        send_status_display_lockdown_alert();

        minor_announce(I18n.T("Hostile runtime detected in door controllers. Isolation lockdown protocols are now in effect. Please remain calm."), I18n.T("Network Alert:"), TRUE);
        to_chat(owner, span_danger(I18n.T("Lockdown initiated. Network reset in 90 seconds.")));
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, nameof(minor_announce),
            I18n.T("Automatic system reboot complete. Have a secure day."),
            I18n.T("Network reset:")), 90 * SECONDS, file: "code/modules/antagonists/malf_ai/malf_ai_modules.dm", line: 422);
        hack_in_progress = FALSE;
        await DmTask.CompletedTask;
        return null;
    }
}

/// Override Machine: Allows the AI to override a machine, animating it into an angry, living version of itself.
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:448
public partial class datum_action_innate_ai_ranged_override_machine : datum_action_innate_ai_ranged
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:457
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        describe_uses();
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:461
    public override async DmTask<object> do_ability(mob_living clicker = null, atom clicked_on = null)
    {
        if (Dm.truthy(clicker.incapacitated))
        {
            unset_ranged_ability(clicker);
            return FALSE;
        }
        if (!ismachinery(clicked_on))
        {
            to_chat(clicker, span_warning(I18n.T("You can only animate machines!")));
            return FALSE;
        }
        obj_machinery clicked_machine = (obj_machinery)clicked_on;

        if (clicked_machine is obj_machinery_porta_turret_cover clicked_turret) //clicking on a closed turret will attempt to override the turret itself instead of the animated/abstract cover.
            clicked_machine = clicked_turret.parent_turret;

        if (Dm.truthy(Dm.bitand(clicked_machine.resistance_flags, INDESTRUCTIBLE)) || Dm.truthy(is_type_in_typecache(clicked_machine, GLOB.blacklisted_malf_machines)))
        {
            to_chat(clicker, span_warning(I18n.T("That machine can't be overridden!")));
            return FALSE;
        }

        clicker.playsound_local(clicker, Dm.resource("sound/misc/interference.ogg"), 50, FALSE, use_reverb: FALSE);

        clicked_machine.audible_message(span_userdanger(I18n.T("You hear a loud electrical buzzing sound coming from []!", clicked_machine)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(animate_machine), clicker, clicked_machine), 5 * SECONDS, file: "code/modules/antagonists/malf_ai/malf_ai_modules.dm", line: 480); //kabeep!
        unset_ranged_ability(clicker, span_danger(I18n.T("Sending override signal...")));
        adjust_uses(-1); //adjust after we unset the active ability since we may run out of charges, thus deleting the ability

        if (Dm.truthy(uses))
        {
            describe_uses();
            await build_all_button_icons();
        }
        return TRUE;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:490
    public virtual void animate_machine(mob_living clicker = null, obj_machinery to_animate = null)
    {
        if (QDELETED(to_animate))
            return;

        Dm.@new<mob_living_basic_mimic_copy_machine>(get_turf(to_animate), to_animate, clicker, TRUE);
    }
}

/// Destroy RCDs: Detonates all non-cyborg RCDs on the station.
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:506
public partial class datum_action_innate_ai_destroy_rcds : datum_action_innate_ai
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:513
    public override async DmTask<object> Activate()
    {
        foreach (object potential_rcd in Dm.each(GLOB.rcd_list))
        {
            if (potential_rcd is obj_item_construction_rcd_borg) //Ensures that cyborg RCDs are spared.
                continue;
            var definite_rcd = (obj_item_construction_rcd)potential_rcd;
            definite_rcd.detonate_pulse();
        }
        to_chat(owner, span_danger(I18n.T("RCD detonation pulse emitted.")));
        owner.playsound_local(owner, Dm.resource("sound/machines/beep/twobeep.ogg"), 50, 0);
        await DmTask.CompletedTask;
        return null;
    }
}

/// Overload Machine: Allows the AI to overload a machine, detonating it after a delay. Two uses per purchase.
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:531
public partial class datum_action_innate_ai_ranged_overload_machine : datum_action_innate_ai_ranged
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:540
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        describe_uses();
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:544
    public virtual async DmTask detonate_machine(mob_living clicker = null, obj_machinery to_explode = null)
    {
        if (QDELETED(to_explode))
            return;

        turf machine_turf = get_turf(to_explode);
        message_admins(Dm.text("[] overloaded [] ([]) at [].", ADMIN_LOOKUPFLW(clicker), to_explode.name, to_explode.type, ADMIN_VERBOSEJMP(machine_turf)));
        clicker.log_message(Dm.text("overloaded [] ([])", to_explode.name, to_explode.type), LOG_ATTACK);
        await explosion(to_explode, heavy_impact_range: 2, light_impact_range: 3);
        if (!QDELETED(to_explode)) //to check if the explosion killed it before we try to delete it
            qdel(to_explode);
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:555
    public override async DmTask<object> do_ability(mob_living clicker = null, atom clicked_on = null)
    {
        if (Dm.truthy(clicker.incapacitated))
        {
            unset_ranged_ability(clicker);
            return FALSE;
        }
        if (!ismachinery(clicked_on))
        {
            to_chat(clicker, span_warning(I18n.T("You can only overload machines!")));
            return FALSE;
        }
        obj_machinery clicked_machine = (obj_machinery)clicked_on;

        if (clicked_machine is obj_machinery_porta_turret_cover clicked_turret) //clicking on a closed turret will attempt to override the turret itself instead of the animated/abstract cover.
            clicked_machine = clicked_turret.parent_turret;

        if (Dm.truthy(Dm.bitand(clicked_machine.resistance_flags, INDESTRUCTIBLE)) || Dm.truthy(is_type_in_typecache(clicked_machine, GLOB.blacklisted_malf_machines)))
        {
            to_chat(clicker, span_warning(I18n.T("You cannot overload that device!")));
            return FALSE;
        }

        clicker.playsound_local(clicker, SFX_SPARKS, 50, 0);
        adjust_uses(-1);
        if (Dm.truthy(uses))
        {
            describe_uses();
            await build_all_button_icons();
        }

        clicked_machine.audible_message(span_userdanger(I18n.T("You hear a loud electrical buzzing sound coming from []!", clicked_machine)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(detonate_machine), clicker, clicked_machine), 5 * SECONDS, file: "code/modules/antagonists/malf_ai/malf_ai_modules.dm", line: 578); //kaboom!
        unset_ranged_ability(clicker, span_danger(I18n.T("Overcharging machine...")));
        return TRUE;
    }
}

/// Blackout: Overloads a random number of lights across the station. Three uses.
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:592
public partial class datum_action_innate_ai_blackout : datum_action_innate_ai
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:599
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        describe_uses();
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:603
    public override async DmTask<object> Activate()
    {
        foreach (obj_machinery_power_apc apc in Dm.each<obj_machinery_power_apc>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc))))
        {
            if (prob(30 * apc.overload))
                Waiting.Detach(apc.overload_lighting());
            else
                apc.overload++;
        }
        to_chat(owner, span_notice(I18n.T("Overcurrent applied to the powernet.")));
        owner.playsound_local(owner, SFX_SPARKS, 50, 0);
        adjust_uses(-1);
        if (QDELETED(this) || Dm.truthy(uses)) //Not sure if not having src here would cause a runtime, so it's here to be safe
            return null;
        describe_uses();
        await build_all_button_icons();
        return null;
    }
}

/// HIGH IMPACT HONKING
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:626
public partial class datum_action_innate_ai_honk : datum_action_innate_ai
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:633
    public override async DmTask<object> Activate()
    {
        to_chat(owner, span_clown(I18n.T("The intercom system plays your prepared file as commanded.")));
        foreach (obj_item_radio_intercom found_intercom in Dm.each<obj_item_radio_intercom>(GLOB.intercoms_list))
        {
            if (!Dm.truthy(found_intercom.is_on()) || !Dm.truthy(found_intercom.get_listening()) || Dm.truthy(found_intercom.wires.is_cut(WIRE_RX))) //Only operating intercoms play the honk
                continue;
            found_intercom.audible_message(message: Dm.text("[] crackles for a split second.", found_intercom), hearing_distance: 3);
            playsound(found_intercom, "sound/items/airhorn/airhorn.ogg", 100, TRUE);
            foreach (mob_living honk_victim in Dm.each<mob_living>(ohearers(6, found_intercom)))
            {
                if (issilicon(honk_victim))
                    continue;
                turf victim_turf = get_turf(honk_victim);
                if (isspaceturf(victim_turf) && !Dm.truthy(await victim_turf.Adjacent(found_intercom))) //Prevents getting honked in space
                    continue;
                if (Dm.truthy(await honk_victim.soundbang_act(SOUNDBANG_NORMAL, stun_pwr: 20, damage_pwr: 30, deafen_pwr: 60))) //Ear protection will prevent these effects
                {
                    await honk_victim.set_jitter_if_lower(120 * SECONDS);
                    to_chat(honk_victim, span_clown(I18n.T("HOOOOONK!")));
                }
            }
        }
        return null;
    }
}

// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:659
public partial class datum_action_innate_ai_place_transformer : datum_action_innate_ai
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:667
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        for (int i = 1; i <= 3; i++)
        {
            image I = image(icon: "icons/turf/overlays.dmi");
            LAZYADD(ref turfOverlays, I);
        }
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:673
    public override async DmTask<object> Activate()
    {
        if (!Dm.truthy(owner_AI.can_place_transformer(this)))
            return null;
        active = TRUE;
        if (Dm.eq(await tgui_alert(owner, I18n.T("Are you sure you want to place the machine here?"), I18n.T("Are you sure?"), Dm.list(I18n.T("Yes"), I18n.T("No"))), I18n.T("No")))
        {
            active = FALSE;
            return null;
        }
        if (!Dm.truthy(owner_AI.can_place_transformer(this)))
        {
            active = FALSE;
            return null;
        }
        turf T = get_turf(owner_AI.eyeobj);
        var conveyor = Dm.@new<obj_machinery_transformer>(T);
        conveyor.master_ai = owner_AI;
        playsound(T, "sound/effects/phasein.ogg", 100, TRUE);
        if (Dm.truthy(owner_AI.can_shunt)) //prevent repeated messages
        {
            owner_AI.can_shunt = FALSE;
            to_chat(owner, span_warning(I18n.T("You are no longer able to shunt your core to APCs.")));
        }
        adjust_uses(-1);
        active = FALSE;
        return null;
    }
}

public partial class mob_living_silicon_ai
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:694
    public virtual void remove_transformer_image(client C = null, image I = null, turf T = null)
    {
        if (C != null && I.loc == T)
            C.images.Remove(I);
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:698
    public virtual object can_place_transformer(datum_action_innate_ai_place_transformer action = null)
    {
        if (eyeobj == null || !isturf(loc) || Dm.truthy(incapacitated) || action == null)
            return null;
        turf middle = get_turf(eyeobj);
        DmList turfs = Dm.list(middle, locate(middle.x - 1, middle.y, middle.z), locate(middle.x + 1, middle.y, middle.z));
        string alert_msg = I18n.T("There isn't enough room! Make sure you are placing the machine in a clear area and on a floor.");
        bool success = true;
        for (int n = 1; n <= 3; n++) //We have to do this instead of iterating normally because of how overlay images are handled
        {
            turf T = turfs[n] as turf;
            if (!isfloorturf(T))
                success = false;
            if (!SScameras.is_visible_by_cameras(T))
            {
                alert_msg = I18n.T("You don't have camera vision of this location!");
                success = false;
            }
            foreach (atom_movable AM in Dm.each<atom_movable>(T?.contents))
            {
                if (Dm.truthy(AM.density))
                {
                    alert_msg = I18n.T("That area must be clear of objects!");
                    success = false;
                }
            }
            image I = action.turfOverlays[n] as image;
            I.loc = T;
            client?.images.Add(I);
            I.icon_state = Dm.text("[]Overlay", success ? "green" : "red"); //greenOverlay and redOverlay for success and failure respectively
            _addtimer(Dm.@new<datum_callback>(this, nameof(remove_transformer_image), client, I, T), 3 * SECONDS, file: "code/modules/antagonists/malf_ai/malf_ai_modules.dm", line: 720);
        }
        if (!success)
            to_chat(this, span_warning(alert_msg));
        return success ? TRUE : FALSE;
    }
}

// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:735
public partial class datum_action_innate_ai_break_air_alarms : datum_action_innate_ai
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:742
    public override async DmTask<object> Activate()
    {
        foreach (obj_machinery_airalarm AA in Dm.each<obj_machinery_airalarm>(GLOB.air_alarms))
        {
            if (!Dm.truthy(is_station_level(AA.z)))
                continue;
            AA.obj_flags = Dm.bitor(AA.obj_flags, EMAGGED);
        }
        to_chat(owner, span_notice(I18n.T("All air alarm safeties on the station have been overridden. Air alarms may now use extremely dangerous environmental modes.")));
        owner.playsound_local(owner, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 50, 0);
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:762
public partial class datum_action_innate_ai_break_fire_alarms : datum_action_innate_ai
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:768
    public override async DmTask<object> Activate()
    {
        foreach (obj_machinery_firealarm bellman in Dm.each<obj_machinery_firealarm>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_firealarm))))
        {
            if (!Dm.truthy(is_station_level(bellman.z)))
                continue;
            bellman.obj_flags = Dm.bitor(bellman.obj_flags, EMAGGED);
            bellman.update_appearance();
        }
        foreach (obj_machinery_door_firedoor firelock in Dm.each<obj_machinery_door_firedoor>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_firedoor))))
        {
            if (!Dm.truthy(is_station_level(firelock.z)))
                continue;
            await firelock.emag_act(owner_AI, null); // tg passes the action as emag_card; the firelock reads no card
        }
        to_chat(owner, span_notice(I18n.T("All thermal sensors on the station have been disabled. Fire alerts will no longer be recognized.")));
        owner.playsound_local(owner, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 50, 0);
        return null;
    }
}

// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:792
public partial class datum_action_innate_ai_emergency_lights : datum_action_innate_ai
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:799
    public override async DmTask<object> Activate()
    {
        foreach (obj_machinery_light L in Dm.each<obj_machinery_light>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_light))))
        {
            if (Dm.truthy(is_station_level(L.z)))
            {
                L.no_low_power = TRUE;
                Waiting.Detach(L.update(FALSE)); // INVOKE_ASYNC
            }
        }
        to_chat(owner, span_notice(I18n.T("Emergency light connections severed.")));
        owner.playsound_local(owner, Dm.resource("sound/effects/light_flicker.ogg"), 50, FALSE);
        await DmTask.CompletedTask;
        return null;
    }
}

/// Reactivate Camera Network: Reactivates up to 30 cameras across the station.
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:819
public partial class datum_action_innate_ai_reactivate_cameras : datum_action_innate_ai
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:827
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        describe_uses();
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:831
    public override async DmTask<object> Activate()
    {
        int fixed_cameras = 0;
        foreach (obj_machinery_camera C in Dm.each<obj_machinery_camera>(SScameras.cameras))
        {
            if (!Dm.truthy(uses))
                break;
            if (!Dm.truthy(C.camera_enabled) || !Dm.eq(C.view_range, Dm.initial(C.GetType(), (obj_machinery_camera c) => c.view_range)))
            {
                C.toggle_cam(owner_AI, 0); //Reactivates the camera based on status. Badly named proc.
                C.view_range = Dm.initial(C.GetType(), (obj_machinery_camera c) => c.view_range);
                fixed_cameras++;
                uses--; //Not adjust_uses() so it doesn't automatically delete or show a message
            }
        }
        to_chat(owner, span_notice(I18n.T("Diagnostic complete! Cameras reactivated: <b>[]</b>. Reactivations remaining: <b>[]</b>.", fixed_cameras, uses)));
        owner.playsound_local(owner, Dm.resource("sound/items/tools/wirecutter.ogg"), 50, 0);
        adjust_uses(0, TRUE); //Checks the uses remaining
        if (QDELETED(this) || !Dm.truthy(uses)) //Not sure if not having src here would cause a runtime, so it's here to be safe
            return null;
        describe_uses();
        await build_all_button_icons();
        return null;
    }
}

/// Upgrade Camera Network: EMP-proofs all cameras, in addition to giving them X-ray vision.
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:850
public partial class datum_ai_module_malf_upgrade_upgrade_cameras : datum_ai_module_malf_upgrade
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:860
    public override async DmTask upgrade__proc(mob_living_silicon_ai AI = null)
    {
        // Sets up nightvision
        RegisterSignal(AI, COMSIG_MOB_UPDATE_SIGHT, nameof(on_update_sight));
        AI.update_sight();

        int upgraded_cameras = 0;
        foreach (obj_machinery_camera camera in Dm.each<obj_machinery_camera>(SScameras.cameras))
        {
            bool upgraded = false;

            if (!Dm.truthy(camera.isXRay()))
            {
                camera.upgradeXRay(TRUE); //if this is removed you can get rid of camera_assembly/var/malf_xray_firmware_active and clean up isxray()
                //Update what it can see.
                upgraded = true;
            }

            if (!Dm.truthy(camera.isEmpProof()))
            {
                camera.upgradeEmpProof(TRUE); //if this is removed you can get rid of camera_assembly/var/malf_emp_firmware_active and clean up isemp()
                upgraded = true;
            }

            if (upgraded)
                upgraded_cameras++;
        }
        unlock_text = replacetext(unlock_text, "CAMSUPGRADED", Dm.text("<b>[]</b>", upgraded_cameras)) as string; //This works, since unlock text is called after upgrade()
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:882
    [SignalHandler]
    public virtual void on_update_sight(mob source = null)
    {
        // Dim blue, pretty
        source.lighting_color_cutoffs = blend_cutoff_colors(source.lighting_color_cutoffs, Dm.list(5, 25, 35)) as DmList;
    }
}

/// AI Turret Upgrade: Increases the health and damage of all turrets.
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:889
public partial class datum_ai_module_malf_upgrade_upgrade_turrets : datum_ai_module_malf_upgrade
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:899
    public override async DmTask upgrade__proc(mob_living_silicon_ai AI = null)
    {
        foreach (obj_machinery_porta_turret_ai turret in Dm.each<obj_machinery_porta_turret_ai>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_porta_turret_ai))))
        {
            await turret.AddElement(typeof(datum_element_empprotection), Dm.bitor(EMP_PROTECT_ALL, EMP_NO_EXAMINE));
            turret.max_integrity = 200;
            await turret.repair_damage(200);
            turret.lethal_projectile = typeof(obj_projectile_beam_laser_heavylaser); //Once you see it, you will know what it means to FEAR.
            turret.lethal_projectile_sound = Dm.resource("sound/items/weapons/lasercannonfire.ogg");
        }
    }
}

/// Enhanced Surveillance: Enables AI to hear conversations going on near its active vision.
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:908
public partial class datum_ai_module_malf_upgrade_eavesdrop : datum_ai_module_malf_upgrade
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:920
    public override async DmTask upgrade__proc(mob_living_silicon_ai AI = null)
    {
        if (AI.eyeobj != null)
            AI.eyeobj.relay_speech = TRUE;
        await DmTask.CompletedTask;
    }
}

/// Unlock Mech Domination: Unlocks the ability to dominate mechs. Big shocker, right?
// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:924
public partial class datum_ai_module_malf_upgrade_mecha_domination : datum_ai_module_malf_upgrade
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:937
    public override async DmTask upgrade__proc(mob_living_silicon_ai AI = null)
    {
        AI.can_dominate_mechs = TRUE; //Yep. This is all it does. Honk!
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:949
public partial class datum_action_innate_ai_voice_changer : datum_action_innate_ai
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:956
    public override async DmTask<object> Activate()
    {
        voice_changer_machine ??= Dm.@new<obj_machinery_ai_voicechanger>(owner_AI);
        await voice_changer_machine.ui_interact(usr);
        return null;
    }
}

// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:961
public partial class obj_machinery_ai_voicechanger : obj_machinery
{
    /// The list of available voices
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:984  var/static/list/voice_options
    public static readonly System.Collections.Immutable.ImmutableArray<string> voice_options = System.Collections.Immutable.ImmutableArray.Create("normal", SPAN_ROBOT, SPAN_YELL, SPAN_CLOWN);

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:986
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!isAI(loc))
            return INITIALIZE_HINT_QDEL;
        owner = (mob_living_silicon_ai)loc;
        owner.ai_voicechanger = this;
        prev_verbs = Dm.list(Dm.kv("say", owner.verb_say), Dm.kv("ask", owner.verb_ask), Dm.kv("exclaim", owner.verb_exclaim), Dm.kv("yell", owner.verb_yell));
        prev_span = owner.speech_span;
        say_name = owner.name;
        say_verb = owner.verb_say;
        say_span = owner.speech_span;
        return dot;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:998
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "AiVoiceChanger");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1004
    public override object Destroy(float? force = null)
    {
        if (owner != null)
        {
            owner.ai_voicechanger = null;
            owner = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1010
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        data["voices"] = Dm.list(voice_options.ToArray());
        data["loud"] = loudvoice;
        data["on"] = changing_voice;
        data["say_verb"] = say_verb;
        data["name"] = say_name;
        data["selected"] = Dm.truthy(say_span) ? say_span : owner.speech_span;
        return data;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1020
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (Dm.truthy(await base.ui_act(action, @params, ui, state)))
            return null;
        DmList prev = (DmList)prev_verbs;
        switch (Dm.str(action))
        {
            case "power":
                changing_voice = Dm.truthy(changing_voice) ? FALSE : TRUE;
                if (Dm.truthy(changing_voice))
                {
                    prev["say"] = owner.verb_say;
                    owner.verb_say = Dm.str(say_verb);
                    prev["ask"] = owner.verb_ask;
                    owner.verb_ask = Dm.str(say_verb);
                    prev["exclaim"] = owner.verb_exclaim;
                    owner.verb_exclaim = Dm.str(say_verb);
                    prev["yell"] = owner.verb_yell;
                    owner.verb_yell = Dm.str(say_verb);
                    prev_span = owner.speech_span;
                    owner.speech_span = Dm.str(say_span);
                    prev_loud = ((obj_item_radio)owner.radio).use_command;
                    ((obj_item_radio)owner.radio).use_command = loudvoice;
                }
                else
                {
                    owner.verb_say = Dm.str(prev["say"]);
                    owner.verb_ask = Dm.str(prev["ask"]);
                    owner.verb_exclaim = Dm.str(prev["exclaim"]);
                    owner.verb_yell = Dm.str(prev["yell"]);
                    owner.speech_span = Dm.str(prev_span);
                    ((obj_item_radio)owner.radio).use_command = Dm.num(prev_loud);
                }
                break;
            case "loud":
                loudvoice = Dm.truthy(loudvoice) ? FALSE : TRUE;
                if (Dm.truthy(changing_voice))
                    ((obj_item_radio)owner.radio).use_command = loudvoice;
                break;
            case "look":
            {
                object selection = @params?["look"];
                if (selection == null)
                    return FALSE;

                bool found = false;
                foreach (string option in voice_options)
                {
                    if (Dm.eq(option, selection))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    stack_trace("User attempted to select an unavailable voice option", "code/modules/antagonists/malf_ai/malf_ai_modules.dm", 1062);
                    return FALSE;
                }

                say_span = selection;
                if (Dm.truthy(changing_voice))
                    owner.speech_span = Dm.str(say_span);
                to_chat(usr, span_notice(I18n.T("Voice set to [].", selection)));
                break;
            }
            case "verb":
                say_verb = strip_html(@params?["verb"], MAX_NAME_LEN);
                if (Dm.truthy(changing_voice))
                {
                    owner.verb_say = Dm.str(say_verb);
                    owner.verb_ask = Dm.str(say_verb);
                    owner.verb_exclaim = Dm.str(say_verb);
                    owner.verb_yell = Dm.str(say_verb);
                }
                break;
            case "name":
                say_name = strip_html(@params?["name"], MAX_NAME_LEN);
                break;
        }
        return null;
    }
}

// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1086
public partial class datum_action_innate_ai_ranged_emag : datum_action_innate_ai_ranged
{
    // Only things with of or subtyped of any of these types may be remotely emagged
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1107  var/static/list/compatable_typepaths
    private static readonly System.Collections.Immutable.ImmutableArray<Type> compatable_typepaths = System.Collections.Immutable.ImmutableArray.Create(
        typeof(obj_machinery),
        typeof(obj_structure),
        typeof(obj_item_radio_intercom),
        typeof(obj_item_modular_computer),
        typeof(mob_living_basic_bot),
        typeof(mob_living_silicon));

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1098
    public override object Destroy(float? force = null) => base.Destroy(force);

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1101
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        describe_uses();
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1105
    public override async DmTask<object> do_ability(mob_living clicker = null, atom clicked_on = null)
    {
        if (!isAI(clicker))
            return FALSE;

        mob_living_silicon_ai ai_clicker = (mob_living_silicon_ai)clicker;

        if (Dm.truthy(ai_clicker.incapacitated))
        {
            unset_ranged_ability(clicker);
            return FALSE;
        }

        if (!Dm.truthy(ai_clicker.can_see(clicked_on)))
        {
            clicked_on.balloon_alert(ai_clicker, "can't see!");
            return FALSE;
        }

        if (clicked_on is obj_machinery clicked_machine && !Dm.truthy(clicked_machine.is_operational))
        {
            clicked_machine.balloon_alert(ai_clicker, "not operational!");
            return FALSE;
        }

        if (!compatable_typepaths.Any(t => t.IsInstanceOfType(clicked_on)))
        {
            clicked_on.balloon_alert(ai_clicker, "incompatable!");
            return FALSE;
        }

        if (clicked_on is obj_machinery_door_airlock clicked_airlock) // I HATE THIS CODE SO MUCHHH
        {
            if (!Dm.truthy(clicked_airlock.canAIControl(ai_clicker)))
            {
                clicked_airlock.balloon_alert(ai_clicker, "unable to interface!");
                return FALSE;
            }
        }

        if (clicked_on is obj_machinery_airalarm alarm && Dm.truthy(alarm.aidisabled))
        {
            alarm.balloon_alert(ai_clicker, "unable to interface!");
            return FALSE;
        }

        if (clicked_on is obj_machinery_power_apc clicked_apc && Dm.truthy(clicked_apc.aidisabled))
        {
            clicked_apc.balloon_alert(ai_clicker, "unable to interface!");
            return FALSE;
        }

        if (!Dm.truthy(await clicked_on.emag_act(ai_clicker)))
        {
            to_chat(ai_clicker, span_warning(I18n.T("Hostile software insertion failed!"))); // lets not overlap balloon alerts
            return FALSE;
        }

        to_chat(ai_clicker, span_notice(I18n.T("Software package successfully injected.")));

        adjust_uses(-1);
        if (Dm.truthy(uses))
        {
            describe_uses();
            await build_all_button_icons();
        }
        else
            unset_ranged_ability(ai_clicker, span_warning(I18n.T("Out of uses!")));

        return TRUE;
    }
}

// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1181
public partial class datum_action_innate_ai_ranged_core_tilt : datum_action_innate_ai_ranged
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1197
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        describe_uses();
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1201
    public override async DmTask<object> do_ability(mob_living clicker = null, atom clicked_on = null)
    {
        if (!(time_til_next_tilt <= world.time)) // COOLDOWN_FINISHED(src, time_til_next_tilt)
        {
            clicker.balloon_alert(clicker, "on cooldown!");
            return FALSE;
        }

        if (!isAI(clicker))
            return FALSE;
        mob_living_silicon_ai ai_clicker = (mob_living_silicon_ai)clicker;

        if (Dm.truthy(ai_clicker.incapacitated) || !isturf(ai_clicker.loc))
            return FALSE;

        turf target = get_turf(clicked_on);
        if (target == null)
            return FALSE;

        if (target == ai_clicker.loc)
        {
            target.balloon_alert(ai_clicker, "can't roll on yourself!");
            return FALSE;
        }

        float picked_dir = get_dir(ai_clicker, target);
        if (picked_dir == 0)
            return FALSE;
        turf temp_target = get_step(ai_clicker, picked_dir); // we can move during the timer so we cant just pass the ref

        Dm.@new<obj_effect_temp_visual_telegraphing_vending_machine_tilt>(temp_target, roll_over_time);
        ai_clicker.balloon_alert_to_viewers("rolling...");
        _addtimer(Dm.@new<datum_callback>(this, nameof(do_roll_over), ai_clicker, picked_dir), roll_over_time, file: "code/modules/antagonists/malf_ai/malf_ai_modules.dm", line: 1229);

        adjust_uses(-1);
        if (Dm.truthy(uses))
        {
            describe_uses();
            await build_all_button_icons();
        }

        time_til_next_tilt = world.time + roll_over_cooldown; // COOLDOWN_START(src, time_til_next_tilt, roll_over_cooldown)
        return null;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1238
    public virtual async DmTask<object> do_roll_over(mob_living_silicon_ai ai_clicker = null, float picked_dir = 0)
    {
        if (Dm.truthy(ai_clicker.incapacitated) || !isturf(ai_clicker.loc)) // prevents bugs where the ai is carded and rolls
            return null;

        turf target = get_step(ai_clicker, picked_dir); // in case we moved we pass the dir not the target turf

        if (target == null)
            return null;

        float paralyze_time = clamp(6 * SECONDS, 0, (roll_over_cooldown * 0.9f)); //the clamp prevents stunlocking as the max is always a little less than the cooldown between rolls

        return await ai_clicker.fall_and_crush(target, MalfDefines.MALF_AI_ROLL_DAMAGE, MalfDefines.MALF_AI_ROLL_CRIT_CHANCE, null, paralyze_time, picked_dir, rotation: get_rotation_from_dir(picked_dir));
    }

    /// Used in our radial menu, state-checking proc after the radial menu sleeps
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1253
    public virtual object radial_check(mob_living_silicon_ai clicker = null)
    {
        if (QDELETED(clicker) || Dm.truthy(clicker.incapacitated) || clicker.stat == DEAD)
            return FALSE;

        if (uses <= 0)
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1262
    public virtual float get_rotation_from_dir(float dir)
    {
        if (dir == NORTH || dir == NORTHWEST || dir == WEST || dir == SOUTHWEST)
            return 270; // try our best to not return 180 since it works badly with animate
        if (dir == EAST || dir == NORTHEAST || dir == SOUTH || dir == SOUTHEAST)
            return 90;
        stack_trace(Dm.text("non-standard dir entered to get_rotation_from_dir. (got: [])", dir), "code/modules/antagonists/malf_ai/malf_ai_modules.dm", 1269);
        return 0;
    }
}

// tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1280
public partial class datum_action_innate_ai_ranged_remote_vendor_tilt : datum_action_innate_ai_ranged
{
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1291
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        describe_uses();
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1295
    public override async DmTask<object> do_ability(mob_living clicker = null, atom clicked_on = null)
    {
        if (!isAI(clicker))
            return FALSE;
        mob_living_silicon_ai ai_clicker = (mob_living_silicon_ai)clicker;

        if (Dm.truthy(ai_clicker.incapacitated))
        {
            unset_ranged_ability(clicker);
            return FALSE;
        }

        if (clicked_on is not obj_machinery_vending) // isvendor
        {
            clicked_on.balloon_alert(ai_clicker, "not a vendor!");
            return FALSE;
        }

        obj_machinery_vending clicked_vendor = (obj_machinery_vending)clicked_on;

        if (Dm.truthy(clicked_vendor.tilted))
        {
            clicked_vendor.balloon_alert(ai_clicker, "already tilted!");
            return FALSE;
        }

        if (!Dm.truthy(clicked_vendor.tiltable))
        {
            clicked_vendor.balloon_alert(ai_clicker, "cannot be tilted!");
            return FALSE;
        }

        if (!Dm.truthy(clicked_vendor.is_operational))
        {
            clicked_vendor.balloon_alert(ai_clicker, "inoperable!");
            return FALSE;
        }

        object picked_dir_string = await show_radial_menu(ai_clicker, clicked_vendor, GLOB.all_radial_directions, custom_check: Dm.@new<datum_callback>(this, nameof(radial_check), clicker, clicked_vendor));
        if (picked_dir_string == null)
            return FALSE;
        float picked_dir = Dm.num(text2dir(picked_dir_string));

        turf target = get_step(clicked_vendor, picked_dir);
        if (!Dm.truthy(ai_clicker.can_see(target)))
        {
            to_chat(ai_clicker, span_warning(I18n.T("You can't see the target tile!")));
            return FALSE;
        }

        Dm.@new<obj_effect_temp_visual_telegraphing_vending_machine_tilt>(target, time_to_tilt);
        clicked_vendor.visible_message(span_warning(I18n.T("[] starts falling over...", clicked_vendor)));
        clicked_vendor.balloon_alert_to_viewers("falling over...");
        _addtimer(Dm.@new<datum_callback>(this, nameof(do_vendor_tilt), clicked_vendor, target), time_to_tilt, file: "code/modules/antagonists/malf_ai/malf_ai_modules.dm", line: 1342);

        adjust_uses(-1);
        if (Dm.truthy(uses))
        {
            describe_uses();
            await build_all_button_icons();
        }

        unset_ranged_ability(clicker, span_danger(I18n.T("Tilting...")));
        return TRUE;
    }

    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1354
    public virtual async DmTask<object> do_vendor_tilt(obj_machinery_vending vendor = null, turf target = null)
    {
        if (QDELETED(vendor))
            return FALSE;

        if (Dm.truthy(vendor.tilted) || !Dm.truthy(vendor.tiltable))
            return FALSE;

        return await vendor.tilt(target, MalfDefines.MALF_VENDOR_TIPPING_CRIT_CHANCE);
    }

    /// Used in our radial menu, state-checking proc after the radial menu sleeps
    // tg: code/modules/antagonists/malf_ai/malf_ai_modules.dm:1364
    public virtual object radial_check(mob_living_silicon_ai clicker = null, obj_machinery_vending clicked_vendor = null)
    {
        if (QDELETED(clicker) || Dm.truthy(clicker.incapacitated) || clicker.stat == DEAD)
            return FALSE;

        if (QDELETED(clicked_vendor))
            return FALSE;

        if (uses <= 0)
            return FALSE;

        if (!Dm.truthy(clicker.can_see(clicked_vendor)))
        {
            to_chat(clicker, span_warning(I18n.T("Lost sight of []!", clicked_vendor)));
            return FALSE;
        }

        return TRUE;
    }
}
