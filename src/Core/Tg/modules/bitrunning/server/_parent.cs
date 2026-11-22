// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/server/_parent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the quantum server's machine (its parts, panel, emag, lights and states). The rest of the
// server is in this folder's other files. The var block is generated (D111).

/**
 * The base object for the quantum server
 */
// tg: code/modules/bitrunning/server/_parent.dm:4
public partial class obj_machinery_quantum_server : obj_machinery
{
    // tg: code/modules/bitrunning/server/_parent.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/modules/bitrunning/server/_parent.dm:62
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();

        RegisterSignals(this, Dm.list(COMSIG_MACHINERY_BROKEN, COMSIG_MACHINERY_POWER_LOST), nameof(on_broken));
        RegisterSignal(this, COMSIG_QDELETING, nameof(on_delete));
    }

    // tg: code/modules/bitrunning/server/_parent.dm:68
    public override object Destroy(float? force = null)
    {
        mutation_candidate_refs.Cut();
        avatar_connection_refs.Cut();
        spawned_threat_refs.Cut();
        exit_turfs.Cut();
        QDEL_NULL(ref generated_domain);
        return base.Destroy(force);
    }

    // tg: code/modules/bitrunning/server/_parent.dm:76
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;
        if (held_item == null)
            return dot;

        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(panel_open) ? I18n.T("Close Panel") : I18n.T("Open Panel");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR) && Dm.truthy(panel_open))
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Deconstruct");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/bitrunning/server/_parent.dm:88
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;

        Dm.append(ref dot, span_infoplain(I18n.T("Can be resource intensive to run. Ensure adequate power supply.")));

        Dm.append(ref dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT(I18n.T("screwed")), Dm.truthy(panel_open) ? I18n.T("close") : I18n.T("open"))));
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("It can be [] apart.", EXAMINE_HINT(I18n.T("pried")))));

        float upgraded = FALSE;
        if (capacitor_coefficient < 1)
        {
            Dm.append(ref dot, span_infoplain(I18n.T("- Its coolant capacity reduces cooldown time by []%.", (1 - capacitor_coefficient) * 100)));
            upgraded = TRUE;
        }

        if (servo_bonus > 0.2f)
        {
            Dm.append(ref dot, span_infoplain(I18n.T("- Its manipulation potential is increasing rewards by []x.", servo_bonus)));
            Dm.append(ref dot, span_infoplain(I18n.T("- Injury from unsafe ejection reduced []%.", servo_bonus * 100)));
            upgraded = TRUE;
        }

        if (!Dm.truthy(upgraded))
            Dm.append(ref dot, span_notice(I18n.T("Its output is suboptimal. Improved components will grant domain information, reduce cooldowns and increase rewards.")));

        if (!Dm.truthy(is_ready))
            Dm.append(ref dot, span_notice(I18n.T("It is currently cooling down. Give it a few moments.")));

        if (isobserver(user) && Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            Dm.append(ref dot, span_notice(I18n.T("Ominous warning lights are blinking red. This server has been tampered with.")));
        return dot;
    }

    // tg: code/modules/bitrunning/server/_parent.dm:117
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        object dot = await base.emag_act(user, emag_card, __extra);

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return dot;

        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        glitch_chance *= 2;
        threat_prob_max *= 2;

        add_overlay(mutable_appearance(Dm.resource("icons/obj/machines/bitrunning.dmi"), "emag_overlay"));
        balloon_alert(user, I18n.T("system jailbroken..."));
        playsound(this, Dm.resource("sound/effects/sparks/sparks1.ogg"), 35, vary: TRUE);
        return dot;
    }

    // tg: code/modules/bitrunning/server/_parent.dm:132
    public override object update_appearance(float? updates = null)
    {
        if (generated_domain == null || !Dm.truthy(is_operational))
        {
            Waiting.NoSleep(set_light(l_on: FALSE));
            return base.update_appearance(updates);
        }

        Waiting.NoSleep(set_light(l_range: 2, l_power: 1.5f, l_color: Dm.truthy(is_ready) ? LIGHT_COLOR_BABY_BLUE : LIGHT_COLOR_FIRE, l_on: TRUE));
        return base.update_appearance(updates);
    }

    // tg: code/modules/bitrunning/server/_parent.dm:141
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (generated_domain == null || !Dm.truthy(is_operational))
        {
            icon_state = Dm.str(base_icon_state);
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(panel_open))
        {
            icon_state = Dm.text("[]_panel", base_icon_state);
            return await base.update_icon_state(__extra);
        }

        icon_state = Dm.text("[]_[]", base_icon_state, Dm.truthy(is_ready) ? "on" : "off");
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/bitrunning/server/_parent.dm:153
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (tool is not obj_item_bitrunning_debug)
            return NONE;

        balloon_alert(user, I18n.T("*hacker voice* i'm in"));
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        glitch_chance = 0.5f;
        capacitor_coefficient = 0.1f;
        points = 100;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/bitrunning/server/_parent.dm:164
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        obj_item crowbar = tool;
        if (!Dm.truthy(is_ready))
        {
            balloon_alert(user, I18n.T("it's scalding hot!"));
            return ITEM_INTERACT_FAILURE;
        }
        if (Dm.truthy(length(avatar_connection_refs)))
        {
            balloon_alert(user, I18n.T("all clients must disconnect!"));
            return ITEM_INTERACT_FAILURE;
        }
        return await default_deconstruction_crowbar(user, crowbar);
    }

    // tg: code/modules/bitrunning/server/_parent.dm:173
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        obj_item screwdriver = tool;
        if (!Dm.truthy(is_ready))
        {
            balloon_alert(user, I18n.T("it's scalding hot!"));
            return ITEM_INTERACT_FAILURE;
        }
        return default_deconstruction_screwdriver(user, screwdriver);
    }

    // tg: code/modules/bitrunning/server/_parent.dm:179
    public override async DmTask<object> RefreshParts()
    {
        float capacitor_rating = 1.15f;
        datum_stock_part_capacitor cap = Dm.locate<datum_stock_part_capacitor>(component_parts);
        capacitor_rating -= Dm.num(cap.tier) * 0.15f;

        capacitor_coefficient = capacitor_rating;

        datum_stock_part_scanning_module scanner = Dm.locate<datum_stock_part_scanning_module>(component_parts);
        if (scanner != null)
            scanner_tier = Dm.num(scanner.tier);

        float servo_rating = 0;
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            servo_rating += Dm.num(servo.tier) * 0.1f;

        servo_bonus = servo_rating;

        return await base.RefreshParts();
    }
}
