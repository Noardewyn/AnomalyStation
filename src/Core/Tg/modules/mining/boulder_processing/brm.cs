// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/boulder_processing/brm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2c (D304): the whole file. The var block is generated (D111). The silicons' clicks are the root slots here (no live
// ancestor declares them); their parent calls are /atom's bodies inline (_onclick/ai.dm:104-115, cyborg.dm:209-226).

// tg: code/modules/mining/boulder_processing/brm.dm:13
public partial class obj_machinery_brm : obj_machinery
{
    ///Sound played when boulders are teleported manually by hand
    private static readonly object MANUAL_TELEPORT_SOUND = Dm.resource("sound/machines/mining/manual_teleport.ogg");
    ///Sound played when boulders are teleported automatically in process()
    private static readonly object AUTO_TELEPORT_SOUND = Dm.resource("sound/machines/mining/auto_teleport.ogg");
    ///Time taken to spawn a boulder, also the cooldown applied before the next manual teleportation
    private const float TELEPORTATION_TIME = 1.5f * SECONDS;
    ///Cooldown for automatic teleportation after processing boulders_processing_max number of boulders
    private const float BATCH_COOLDOWN = 3 * SECONDS;
    ///Special case when we are trying to teleport a boulder but there is already another boulder in our loc
    private const float TURF_BLOCKED_BY_BOULDER = -1;

    // tg: code/modules/mining/boulder_processing/brm.dm:36
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/brm.dm:40
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;

        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Teleport single boulder";
            context[SCREENTIP_CONTEXT_RMB] = Dm.text("Toggle [] automatic boulder retrieval", Dm.truthy(toggled_on) ? "Off" : "On");
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (held_item != null)
        {
            if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
            {
                context[SCREENTIP_CONTEXT_LMB] = Dm.text("[]chor", Dm.truthy(anchored) ? "Unan" : "An");
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            {
                context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] panel", Dm.truthy(panel_open) ? "Close" : "Open");
                return CONTEXTUAL_SCREENTIP_SET;
            }
            else if (Dm.truthy(panel_open) && Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/brm.dm:62
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("The small screen reads there are [] available to teleport.", span_boldnotice(I18n.T("[] boulders", SSore_generation.available_boulders.Count)))));
        Dm.append(ref dot, span_notice(I18n.T("Can collect up to <b>[] boulders</b> at a time.", boulders_processing_max)));
        Dm.append(ref dot, span_notice(I18n.T("Automatic boulder retrieval can be toggled [] with [].", EXAMINE_HINT(Dm.truthy(toggled_on) ? I18n.T("Off") : I18n.T("On")), EXAMINE_HINT(I18n.T("Right Click")))));

        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("It's [] in place.", EXAMINE_HINT(I18n.T("anchored")))));
        else
            Dm.append(ref dot, span_warning(I18n.T("It needs to be [] to start operations.", EXAMINE_HINT(I18n.T("anchored")))));

        Dm.append(ref dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT(I18n.T("screwed")), Dm.truthy(panel_open) ? I18n.T("closed") : I18n.T("open"))));

        if (Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("The whole machine can be [] apart.", EXAMINE_HINT(I18n.T("pried")))));
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/brm.dm:78
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = (string)base_icon_state;

        if (!Dm.truthy(anchored) || !Dm.truthy(is_operational) || Dm.truthy(Dm.bitand(machine_stat, BROKEN | NOPOWER)) || Dm.truthy(panel_open))
        {
            icon_state = Dm.text("[]-off", icon_state);
            return null;
        }

        if (Dm.truthy(toggled_on))
        {
            icon_state = Dm.text("[]-toggled", icon_state);
            return null;
        }

        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/mining/boulder_processing/brm.dm:91
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        if (Dm.eq(await default_unfasten_wrench(user, tool, time: 1.5f * SECONDS), SUCCESSFUL_UNFASTEN))
        {
            update_appearance(UPDATE_ICON_STATE);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/brm.dm:97
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/mining/boulder_processing/brm.dm:100
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    ///To allow boulders on a conveyor belt to move unobstructed if multiple machines are made on a single line
    // tg: code/modules/mining/boulder_processing/brm.dm:104
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        if (!Dm.truthy(anchored))
            return FALSE;
        if (istype(mover, typeof(obj_item_boulder)))
            return TRUE;
        return await base.CanAllowThrough(mover, border_dir);
    }

    // tg: code/modules/mining/boulder_processing/brm.dm:111
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();

        boulders_processing_max = 0;
        foreach (datum_stock_part part in Dm.each<datum_stock_part>(component_parts))
            boulders_processing_max += Dm.num(part.tier);

        boulders_processing_max = ROUND_UP((boulders_processing_max / 12) * 7);
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/brm.dm:120
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot) || Dm.truthy(panel_open))
            return dot;
        if (!Dm.truthy(handle_teleport_conditions(user)))
            return dot;

        object result = await pre_collect_boulder();
        if (Dm.eq(result, TURF_BLOCKED_BY_BOULDER))
            balloon_alert(user, I18n.T("no space!"));
        else if (Dm.truthy(result))
            balloon_alert(user, I18n.T("teleporting..."));
        manual_teleport_cooldown = world.time + TELEPORTATION_TIME; // COOLDOWN_START (__DEFINES/cooldowns.dm), inline

        return TRUE;
    }

    /**
     * Handles qualifiers for enabling teleportation of boulders.
     * Returns TRUE if the teleportation can proceed, FALSE otherwise.
     */
    // tg: code/modules/mining/boulder_processing/brm.dm:144
    public virtual object handle_teleport_conditions(mob user = null)
    {
        if (!(manual_teleport_cooldown <= world.time))
            return FALSE;
        if (Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("close panel first!"));
            return FALSE;
        }
        if (Dm.truthy(batch_processing))
        {
            balloon_alert(user, I18n.T("batch still processing!"));
            return FALSE;
        }
        playsound(this, MANUAL_TELEPORT_SOUND, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        return TRUE;
    }

    // tg: code/modules/mining/boulder_processing/brm.dm:159
    public override async DmTask<object> attack_ai(mob user = null)
    {
        object dot = await base.attack_ai(user);
        if (Dm.truthy(dot) || Dm.truthy(panel_open))
            return dot;
        if (!Dm.truthy(handle_teleport_conditions(user)))
            return dot;

        object result = await pre_collect_boulder();
        if (Dm.eq(result, TURF_BLOCKED_BY_BOULDER))
            balloon_alert(user, I18n.T("no space!"));
        else if (Dm.truthy(result))
            balloon_alert(user, I18n.T("teleporting..."));

        manual_teleport_cooldown = world.time + TELEPORTATION_TIME; // COOLDOWN_START (__DEFINES/cooldowns.dm), inline

        return TRUE;
    }

    // tg: code/modules/mining/boulder_processing/brm.dm:176
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_robot(user, modifiers);
        if (Dm.truthy(dot) || Dm.truthy(panel_open))
            return dot;
        if (!Dm.truthy(handle_teleport_conditions(user)))
            return dot;

        object result = await pre_collect_boulder();
        if (Dm.eq(result, TURF_BLOCKED_BY_BOULDER))
            balloon_alert(user, I18n.T("no space!"));
        else if (Dm.truthy(result))
            balloon_alert(user, I18n.T("teleporting..."));

        manual_teleport_cooldown = world.time + TELEPORTATION_TIME; // COOLDOWN_START (__DEFINES/cooldowns.dm), inline

        return TRUE;
    }

    // tg: code/modules/mining/boulder_processing/brm.dm:193
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN) || Dm.truthy(panel_open))
            return dot;
        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("anchor it first!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        toggle_auto_on(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    /**
     * Toggles automatic boulder retrieval on.
     * Adjusts the teleportation sound, icon state, and begins processing.
     */
    // tg: code/modules/mining/boulder_processing/brm.dm:210
    public virtual void toggle_auto_on(mob user = null)
    {
        if (Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("close panel first!"));
            return;
        }
        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("anchor it first!"));
            return;
        }
        if (!Dm.truthy(is_operational) || Dm.truthy(Dm.bitand(machine_stat, BROKEN | NOPOWER)))
            return;

        toggled_on = Dm.truthy(toggled_on) ? FALSE : TRUE;
        if (Dm.truthy(toggled_on))
            begin_processing();
        else
            end_processing();
        update_appearance(UPDATE_ICON_STATE);
    }

    // tg: code/modules/mining/boulder_processing/brm.dm:229
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_ai_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN) || Dm.truthy(panel_open))
            return dot;
        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("unanchored!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }

        toggle_auto_on(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/mining/boulder_processing/brm.dm:240
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_robot_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN) || Dm.truthy(panel_open))
            return dot;
        if (!Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH | FORBID_TELEKINESIS_REACH)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("unanchored!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }

        toggle_auto_on(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/mining/boulder_processing/brm.dm:253
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(toggled_on))
            return PROCESS_KILL;

        //have some cooldown after processing the previous batch of boulders
        if (Dm.truthy(batch_processing) || !(batch_start_cooldown <= world.time))
            return null;

        Waiting.NoSleep(pre_collect_boulder(FALSE, boulders_processing_max));
        return null;
    }

    /**
     * Begins to collect a boulder from the available boulders list in SSore_generation.
     * Boulders must be in the available boulders list.
     * A selected boulder is picked randomly.
     * Arguments
     *
     * * feedback - should we play sound and display allert if now boulders are available
     * * boulders_remaining - how many boulders we want to try & collect spawning a boulder every TELEPORTATION_TIME seconds
     */
    // tg: code/modules/mining/boulder_processing/brm.dm:273
    public virtual async DmTask<object> pre_collect_boulder(float? feedback = null, float? boulders_remaining = null)
    {
        feedback ??= TRUE;
        boulders_remaining ??= 1;

        batch_processing = TRUE;

        //not within operation parameters
        if (!Dm.truthy(anchored) || Dm.truthy(panel_open) || !Dm.truthy(is_operational) || Dm.truthy(Dm.bitand(machine_stat, BROKEN | NOPOWER)))
        {
            batch_processing = FALSE;
            return FALSE;
        }

        //There is an boulder in our loc. it has be removed so we don't clog up our loc with even more boulders
        if (Dm.locate<obj_item_boulder>(loc) != null)
        {
            batch_processing = FALSE;
            return TURF_BLOCKED_BY_BOULDER;
        }

        //no more boulders
        if (!Dm.truthy(SSore_generation.available_boulders.Count))
        {
            if (Dm.truthy(feedback))
            {
                playsound(loc, Dm.resource("sound/machines/synth/synth_no.ogg"), 30, TRUE);
                balloon_alert_to_viewers(I18n.T("no boulders to collect!"));
            }
            batch_processing = FALSE;
            return FALSE;
        }

        //pick & spawn the boulder
        flick("brm-flash", this);
        playsound(this, Dm.truthy(toggled_on) ? AUTO_TELEPORT_SOUND : MANUAL_TELEPORT_SOUND, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        obj_item_boulder random_boulder = pick(SSore_generation.available_boulders) as obj_item_boulder;
        SSore_generation.available_boulders.Remove(random_boulder);
        await random_boulder.forceMove(drop_location() as atom);
        random_boulder.pixel_x = Dm.rand(-2, 2);
        random_boulder.pixel_y = Dm.rand(-2, 2);
        balloon_alert_to_viewers(I18n.T("boulder appears!"));
        await use_energy(active_power_usage);

        //try again if we have more boulders to work with
        boulders_remaining -= 1;
        if (boulders_remaining <= 0)
        {
            batch_start_cooldown = world.time + BATCH_COOLDOWN; // COOLDOWN_START (__DEFINES/cooldowns.dm), inline
            batch_processing = FALSE;
            return TRUE;
        }
        else
            _addtimer(Dm.@new<datum_callback>(this, nameof(pre_collect_boulder), feedback, boulders_remaining, FALSE), TELEPORTATION_TIME, file: "code/modules/mining/boulder_processing/brm.dm", line: 309);
        return null;
    }
}
