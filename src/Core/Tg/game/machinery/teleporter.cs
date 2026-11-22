// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/teleporter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps TP1 (D333's station order: the bridge's teleporter): the whole file. The teleporter hub (the walk-in pad that sends what
// bumps it to the console's target, its accuracy from its matter bins, the uncalibrated jump's fly mutation) and the teleporter
// station (linking its hub and console at the map load, engaged by hand, linked to other stations by a multitool for the gate
// regime). Deviation: in Moth Week tg turns an uncalibrated jumper into a moth; the moth species is not ported yet, so the fly
// stands in (not-translated.md). The var blocks are generated (D111).

// tg: code/game/machinery/teleporter.dm:6
public partial class obj_machinery_teleport_hub : obj_machinery_teleport
{
    // tg: code/game/machinery/teleporter.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        link_power_station();
        return dot;
    }

    // tg: code/game/machinery/teleporter.dm:20
    public override object Destroy(float? force = null)
    {
        if (power_station != null)
        {
            power_station.teleporter_hub = null;
            power_station = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/machinery/teleporter.dm:26
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float A = 0;
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            A += Dm.num(matter_bin.tier);
        accuracy = A;
        return dot;
    }

    // tg: code/game/machinery/teleporter.dm:33
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (in_range(user, this) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Success chance is <b>[]%</b>.", 70 + (accuracy * 10))));
        return dot;
    }

    // tg: code/game/machinery/teleporter.dm:38
    public virtual obj_machinery_teleport_station link_power_station()
    {
        if (power_station != null)
            return null;
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            power_station = Dm.locate<obj_machinery_teleport_station>(get_step(this, Dm.num(direction)));
            if (power_station != null)
            {
                power_station.link_console_and_hub();
                break;
            }
        }
        return power_station;
    }

    // tg: code/game/machinery/teleporter.dm:48
    public override object Bumped(atom_movable bumped_atom = null)
    {
        if (Dm.truthy(is_centcom_level(z)))
        {
            to_chat(bumped_atom, span_warning(I18n.T("You can't use this here!")));
            return null;
        }
        if (Dm.truthy(is_ready()))
            Waiting.NoSleep(teleport(bumped_atom));
        return null;
    }

    // tg: code/game/machinery/teleporter.dm:55
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/teleporter.dm:58
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/teleporter.dm:61
    public override void on_set_panel_open(object old_value = null)
    {
        if (Dm.truthy(panel_open) && Dm.truthy(power_station?.engaged))
        {
            power_station.engaged = FALSE;
            update_appearance();
        }
    }

    // tg: code/game/machinery/teleporter.dm:66
    public virtual async DmTask teleport(atom_movable M = null, turf T = null)
    {
        obj_machinery_computer_teleporter com = power_station.teleporter_console;
        if (QDELETED(com))
            return;
        atom target = null;
        if (com.target_ref != null)
            target = com.target_ref.resolve() as atom;
        if (target == null)
        {
            com.target_ref = null;
            visible_message(span_alert(I18n.T("Cannot authenticate locked on coordinates. Please reinstate coordinate matrix.")));
            return;
        }
        if (!ismovable(M))
            return;
        turf start_turf = get_turf(M) as turf;
        if (!Dm.truthy(await do_teleport(M, target, channel: TELEPORT_CHANNEL_BLUESPACE)))
            return;
        playsound(loc as atom, SFX_PORTAL_ENTER, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        await use_energy(active_power_usage);
        Dm.@new<obj_effect_temp_visual_portal_animation>(start_turf, this, M);
        if (!Dm.truthy(calibrated) && ishuman(M) && prob(30 - ((accuracy) * 10))) //oh dear a problem
        {
            var human = (mob_living_carbon_human)M;
            if (!Dm.truthy(Dm.bitand(human.mob_biotypes, Dm.bitor(Dm.bitor(Dm.bitor(MOB_ROBOTIC, MOB_MINERAL), MOB_UNDEAD), MOB_SPIRIT))))
            {
                Type species_to_transform = typeof(datum_species_fly);
                // tg: if(check_holidays(MOTH_WEEK)) species_to_transform = /datum/species/moth
                // DEFERRED (not-translated.md): the moth species is not ported, so in Moth Week the fly stands in.
                if (human.dna != null && !Dm.eq((human.dna.species as datum_species)?.id, Dm.initial(species_to_transform, (datum_species s) => s.id)))
                {
                    to_chat(M, span_hear(I18n.T("You hear a buzzing in your ears.")));
                    human.set_species(species_to_transform);
                    human.log_message(Dm.text("was turned into a [] through [].", Dm.initial(species_to_transform, (datum_species s) => s.name), this), LOG_GAME);
                }
            }
        }
        calibrated = FALSE;
    }

    // tg: code/game/machinery/teleporter.dm:97
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(panel_open) ? "-o" : (Dm.truthy(is_ready()) ? 1 : 0));
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/teleporter.dm:101
    public virtual object is_ready()
    {
        return !Dm.truthy(panel_open) && !Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))) && power_station != null
            && Dm.truthy(power_station.engaged) && !Dm.truthy(Dm.bitand(power_station.machine_stat, Dm.bitor(BROKEN, NOPOWER)));
    }
}

// tg: code/game/machinery/teleporter.dm:104
public partial class obj_machinery_teleport_hub_syndicate : obj_machinery_teleport_hub
{
    // tg: code/game/machinery/teleporter.dm:104
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        LAZYADD(ref component_parts, GLOB.stock_part_datums[typeof(datum_stock_part_matter_bin_tier3)]);
        Waiting.NoSleep(RefreshParts());
        return dot;
    }
}

// tg: code/game/machinery/teleporter.dm:109
public partial class obj_machinery_teleport_station : obj_machinery_teleport
{
    // tg: code/game/machinery/teleporter.dm:121
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        link_console_and_hub();
        return dot;
    }

    // tg: code/game/machinery/teleporter.dm:125
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float E = 0;
        foreach (datum_stock_part_capacitor C in Dm.each<datum_stock_part_capacitor>(component_parts))
            E += Dm.num(C.tier);
        efficiency = E - 1;
        return dot;
    }

    // tg: code/game/machinery/teleporter.dm:132
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("The panel is <i>screwed</i> in, obstructing the linking device and wiring panel.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("The <i>linking</i> device is now able to be <i>scanned</i> with a multitool.")));
        if (in_range(user, this) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: This station can be linked to <b>[]</b> other station(s).", efficiency)));
        return dot;
    }

    // tg: code/game/machinery/teleporter.dm:141
    public virtual object link_console_and_hub()
    {
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            teleporter_hub = Dm.locate<obj_machinery_teleport_hub>(get_step(this, Dm.num(direction)));
            if (teleporter_hub != null)
            {
                teleporter_hub.link_power_station();
                break;
            }
        }
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            teleporter_console = Dm.locate<obj_machinery_computer_teleporter>(get_step(this, Dm.num(direction)));
            if (teleporter_console != null)
            {
                teleporter_console.link_power_station();
                break;
            }
        }
        return teleporter_hub != null && teleporter_console != null ? TRUE : FALSE;
    }

    // tg: code/game/machinery/teleporter.dm:155
    public override object Destroy(float? force = null)
    {
        if (teleporter_hub != null)
        {
            teleporter_hub.power_station = null;
            teleporter_hub.update_appearance();
            teleporter_hub = null;
        }
        if (teleporter_console != null)
        {
            teleporter_console.power_station = null;
            teleporter_console = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/machinery/teleporter.dm:166
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        var multitool = (obj_item_multitool)tool;
        if (Dm.truthy(panel_open))
        {
            multitool.set_buffer(this);
            balloon_alert(user, I18n.T("saved to multitool buffer"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (multitool.buffer is not obj_machinery_teleport_station || multitool.buffer == this)
            return ITEM_INTERACT_BLOCKING;

        if (linked_stations.len < efficiency)
        {
            linked_stations.Add(multitool.buffer);
            multitool.set_buffer(null);
            balloon_alert(user, I18n.T("data uploaded from buffer"));
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/game/machinery/teleporter.dm:182
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/teleporter.dm:185
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/teleporter.dm:188
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        toggle(user);
        return null;
    }

    // tg: code/game/machinery/teleporter.dm:191
    public virtual void toggle(mob user = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))) || teleporter_hub == null || teleporter_console == null)
            return;
        if (teleporter_console.target_ref?.resolve() != null)
        {
            if (Dm.truthy(teleporter_hub.panel_open) || Dm.truthy(Dm.bitand(teleporter_hub.machine_stat, Dm.bitor(BROKEN, NOPOWER))))
                to_chat(user, span_alert(I18n.T("The teleporter hub isn't responding.")));
            else
            {
                engaged = Dm.truthy(engaged) ? FALSE : TRUE;
                Waiting.NoSleep(use_energy(active_power_usage));
                to_chat(user, span_notice(I18n.T("Teleporter []engaged!", Dm.truthy(engaged) ? "" : I18n.T("dis"))));
            }
        }
        else
        {
            teleporter_console.target_ref = null;
            to_chat(user, span_alert(I18n.T("No target detected.")));
            engaged = FALSE;
        }
        teleporter_hub.update_appearance();
        add_fingerprint(user);
    }

    // tg: code/game/machinery/teleporter.dm:209
    public override object power_change()
    {
        object result = base.power_change();
        teleporter_hub?.update_appearance();
        return result;
    }

    // tg: code/game/machinery/teleporter.dm:214
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(panel_open))
        {
            icon_state = Dm.text("[]-o", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
        {
            icon_state = Dm.text("[]-p", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(teleporter_console?.calibrating))
        {
            icon_state = Dm.text("[]-c", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = (string)base_icon_state;
        return await base.update_icon_state(__extra);
    }
}
