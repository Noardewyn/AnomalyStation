// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/harvester.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package MD3, D333's station order: the medical machines): the organ harvester that takes a dead, stripped,
// organic body, warms up and drops its limbs and organs one by one on its output side (alt-click with its panel open turns the
// output), with its pictures, its juicer sound and its say lines; the emag lets it take the living and the clothed. The var block
// is generated (D111).

// tg: code/game/machinery/harvester.dm:1
public partial class obj_machinery_harvester : obj_machinery
{
    // tg: code/game/machinery/harvester.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(1))
            name = I18n.T("auto-autopsy");
        return dot;
    }

    // tg: code/game/machinery/harvester.dm:25
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        interval = 0;
        float max_time = 40;
        foreach (datum_stock_part_micro_laser micro_laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            max_time -= Dm.num(micro_laser.tier);
        interval = max(max_time, 1);
        return dot;
    }

    // tg: code/game/machinery/harvester.dm:33
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(panel_open))
        {
            icon_state = Dm.text("[]-o", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(state_open))
        {
            icon_state = Dm.text("[]-open", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(warming_up))
        {
            icon_state = Dm.text("[]-charging", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(harvesting))
        {
            icon_state = Dm.text("[]-active", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.str(base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/harvester.dm:50
    public override async DmTask<object> open_machine(float? drop = null, float? density_to_set = null)
    {
        drop ??= TRUE;
        density_to_set ??= FALSE;
        if (Dm.truthy(panel_open))
            return null;
        object dot = await base.open_machine(drop, density_to_set);
        warming_up = FALSE;
        harvesting = FALSE;
        return dot;
    }

    // tg: code/game/machinery/harvester.dm:57
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(state_open))
            await close_machine();
        else if (!Dm.truthy(harvesting))
            await open_machine();
        return dot;
    }

    // tg: code/game/machinery/harvester.dm:64
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(panel_open))
        {
            output_dir = turn(output_dir, -90);
            to_chat(user, span_notice(I18n.T("You change []'s output settings, setting the output to [].", this, dir2text(output_dir))));
            return CLICK_ACTION_SUCCESS;
        }
        if (Dm.truthy(harvesting) || Dm.truthy(state_open) || !Dm.truthy(await can_harvest()))
            return CLICK_ACTION_BLOCKING;

        await start_harvest();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/machinery/harvester.dm:75
    public virtual async DmTask<object> can_harvest()
    {
        if (!Dm.truthy(powered__proc()) || Dm.truthy(state_open) || occupant == null || !iscarbon(occupant))
            return null;
        var carbon_occupant = (mob_living_carbon)occupant;
        if (!Dm.truthy(allow_clothing))
        {
            DmList worn = Dm.list(); // held_items + get_equipped_items()
            foreach (object held in Dm.each(carbon_occupant.held_items))
                worn.Add(held);
            foreach (object equipped in Dm.each(carbon_occupant.get_equipped_items() as DmList))
                worn.Add(equipped);
            foreach (obj_item abiotic_item in Dm.each<obj_item>(worn))
            {
                if (!HAS_TRAIT(abiotic_item, TRAIT_NODROP))
                {
                    await say(I18n.T("Subject may not have abiotic items on."));
                    playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 30, TRUE);
                    return null;
                }
            }
        }
        if (!Dm.truthy(Dm.bitand(carbon_occupant.mob_biotypes, MOB_ORGANIC)))
        {
            await say(I18n.T("Subject is not organic."));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 30, TRUE);
            return null;
        }
        if (!Dm.truthy(allow_living) && !IS_DEAD_OR_FAKING(carbon_occupant))
        {
            await say(I18n.T("Subject is still alive."));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 30, TRUE);
            return null;
        }
        return TRUE;
    }

    // tg: code/game/machinery/harvester.dm:97
    public virtual async DmTask start_harvest()
    {
        if (occupant == null || !iscarbon(occupant))
            return;

        var carbon_occupant = (mob_living_carbon)occupant;

        if (carbon_occupant.client != null && carbon_occupant.stat <= HARD_CRIT)
        {
            notify_ghosts(
                I18n.T("[] is about to be ground up by a malfunctioning organ harvester!", carbon_occupant.real_name),
                source: this,
                header: I18n.T("Gruesome!"));
        }

        operation_order = reverse_range(carbon_occupant.get_bodyparts() as DmList) as DmList; //Chest and head are first in bodyparts, so we invert it to make them suffer more
        warming_up = TRUE;
        harvesting = TRUE;
        visible_message(span_notice(I18n.T("\\The [] begins warming up!", this)));
        await say(I18n.T("Initializing harvest protocol."));
        update_appearance();
        _addtimer(Dm.@new<datum_callback>(this, nameof(harvest)), interval, file: "code/game/machinery/harvester.dm", line: 116);
    }

    // tg: code/game/machinery/harvester.dm:118
    public virtual async DmTask harvest()
    {
        warming_up = FALSE;
        update_appearance();
        if (!Dm.truthy(harvesting) || Dm.truthy(state_open) || !Dm.truthy(powered__proc()) || occupant == null || !iscarbon(occupant))
        {
            await end_harvesting(success: FALSE);
            return;
        }
        playsound(this, Dm.resource("sound/machines/juicer.ogg"), 20, TRUE);
        var carbon_occupant = (mob_living_carbon)occupant;
        if (!Dm.truthy(LAZYLEN(operation_order))) //The list is empty, so we're done here
        {
            await end_harvesting(success: TRUE);
            return;
        }
        turf target = get_step(this, output_dir) as turf;
        foreach (obj_item_bodypart limb_to_remove in Dm.each_cast<obj_item_bodypart>(operation_order)) //first we do non-essential limbs
        {
            await limb_to_remove.drop_limb();
            await carbon_occupant.emote("scream");
            if (!Dm.eq(limb_to_remove.body_zone, "chest"))
            {
                await limb_to_remove.forceMove(target); //Move the limbs right next to it, except chest, that's a weird one
                await limb_to_remove.drop_organs();
            }
            else
            {
                foreach (obj_item_organ organ_to_remove in Dm.each<obj_item_organ>(await limb_to_remove.dismember() as DmList))
                    await organ_to_remove.forceMove(target); //Some organs, like chest ones, are different so we need to manually move them
            }
            Dm.remove(ref operation_order, limb_to_remove);
            break;
        }
        await use_energy(active_power_usage);
        _addtimer(Dm.@new<datum_callback>(this, nameof(harvest)), interval, file: "code/game/machinery/harvester.dm", line: 141);
    }

    // tg: code/game/machinery/harvester.dm:143
    public virtual async DmTask end_harvesting(object success = null)
    {
        success ??= TRUE;
        warming_up = FALSE;
        harvesting = FALSE;
        await open_machine();
        if (!Dm.truthy(success))
        {
            await say(I18n.T("Protocol interrupted. Aborting harvest."));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 30, TRUE);
        }
        else
        {
            await say(I18n.T("Subject has been successfully harvested."));
            playsound(this, Dm.resource("sound/machines/microwave/microwave-end.ogg"), 100, FALSE);
        }
    }

    // tg: code/game/machinery/harvester.dm:155
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        if (occupant != null)
        {
            to_chat(user, span_warning(I18n.T("[] is currently occupied!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(state_open))
        {
            to_chat(user, span_warning(I18n.T("[] must be closed to [] its maintenance hatch!", this, Dm.truthy(panel_open) ? I18n.T("close") : I18n.T("open"))));
            return ITEM_INTERACT_BLOCKING;
        }
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/harvester.dm:164
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
        => await default_pry_open(user, tool, deconstruct_on_fail: TRUE);

    // tg: code/game/machinery/harvester.dm:167
    public override object can_crowbar_pry_open() => !Dm.truthy(state_open) && !Dm.truthy(panel_open) ? TRUE : FALSE;

    // tg: code/game/machinery/harvester.dm:170
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        allow_living = TRUE;
        allow_clothing = TRUE;
        balloon_alert(user, I18n.T("lifesign scanners overloaded"));
        return TRUE;
    }

    // tg: code/game/machinery/harvester.dm:179
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        if (!Dm.truthy(harvesting))
        {
            visible_message(span_notice(I18n.T("[] emerges from []!", occupant, this)),
                span_notice(I18n.T("You climb out of []!", this)));
            await open_machine();
        }
        else
            to_chat(user, span_warning(I18n.T("[] is active and can't be opened!", this))); //rip
        return null;
    }

    // tg: code/game/machinery/harvester.dm:187
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (!Dm.truthy(state_open) && ReferenceEquals(gone, occupant))
            await container_resist_act(gone as mob_living);
        return await base.Exited(gone, direction);
    }

    // tg: code/game/machinery/harvester.dm:192
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (!Dm.truthy(state_open))
            await container_resist_act(user);
        return null;
    }

    // tg: code/game/machinery/harvester.dm:196
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return dot;
        if (Dm.truthy(state_open))
            Dm.append(ref dot, span_notice(I18n.T("[] must be closed before harvesting.", this)));
        else if (!Dm.truthy(harvesting))
            Dm.append(ref dot, span_notice(I18n.T("Alt-click [] to start harvesting.", this)));
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Harvest speed at <b>[]</b> seconds per organ. Outputting to the <b>[]</b>.", interval * 0.1f, dir2text(output_dir))));
        return dot;
    }
}
