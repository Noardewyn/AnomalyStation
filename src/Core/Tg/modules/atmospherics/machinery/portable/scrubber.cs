// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/portable/scrubber.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the portable air scrubbers): the pictures, process_atmos and scrub, unregister_holding, and the
// huge scrubber's picture and process_atmos. Generated live-partial stubs: on_deconstruction (M5),
// replace_tank (the hand-held tanks, M4a) and the huge scrubber's wrench act (M4a). Package E3 (docs/ai/specs/engineering.md): the tgui
// window (ui_interact, ui_data, ui_act; the scrubber is switched on there); its admin messages and investigate logs are deferred
// leaves (admin tooling, M10). The var blocks are generated.

// tg: code/modules/atmospherics/machinery/portable/scrubber.dm:1
public partial class obj_machinery_portable_atmospherics_scrubber : obj_machinery_portable_atmospherics
{
    // tg: code/modules/atmospherics/machinery/portable/scrubber.dm:114
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (Dm.truthy(is_operational))
        {
            if (prob(50 / Dm.num(severity)))
            {
                on = !Dm.truthy(on) ? TRUE : FALSE;
                if (Dm.truthy(on))
                    SSair.start_processing_machine(this);
            }
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/portable/scrubber.dm:39
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]_[]", Dm.initial(this, (atom p) => p.icon_state), on);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/atmospherics/machinery/portable/scrubber.dm:44
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(use_overlays))
            return dot;
        if (holding != null)
            Dm.append(ref dot, "scrubber-open");
        if (connected_port != null)
            Dm.append(ref dot, "scrubber-connector");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/portable/scrubber.dm:52
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(seconds_per_tick, dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(object seconds_per_tick, DotBox<object> dot)
    {
        if (Dm.truthy(await take_atmos_damage()))
        {
            excited = TRUE;
            return base.process_atmos(seconds_per_tick);
        }

        if (!Dm.truthy(on))
            return base.process_atmos(seconds_per_tick);

        excited = TRUE;

        if (holding != null)
        {
            scrub(await holding.return_air() as datum_gas_mixture);
            return base.process_atmos(seconds_per_tick);
        }

        turf epicentre = get_turf(this);
        if (isopenturf(epicentre))
        {
            if (Dm.truthy(scrub(await epicentre.return_air() as datum_gas_mixture)))
                epicentre.air_update_turf(FALSE, FALSE);
        }

        foreach (turf_open openturf in Dm.each_cast<turf_open>(epicentre.get_atmos_adjacent_turfs(alldir: TRUE)))
        {
            if (Dm.truthy(scrub(await openturf.return_air() as datum_gas_mixture)))
                openturf.air_update_turf(FALSE, FALSE);
        }
        return base.process_atmos(seconds_per_tick);
    }

    /**
     * Called in process_atmos(), handles the scrubbing of the given gas_mixture
     * Arguments:
     * * mixture: the gas mixture to be scrubbed
     * Returns: TRUE if anything was scrubbed, FALSE otherwise
     */
    // tg: code/modules/atmospherics/machinery/portable/scrubber.dm:82
    public virtual object scrub(datum_gas_mixture environment = null)
    {
        if (Dm.num(air_contents.return_pressure()) >= overpressure_m * ONE_ATMOSPHERE)
            return FALSE;

        DmList cached_moles = environment.moles;

        //contains all of the gas we're sucking out of the tile, gets put into our parent pipenet
        datum_gas_mixture filtered_out = Dm.@new<datum_gas_mixture>();
        filtered_out.temperature = environment.temperature;

        //maximum percentage of the turfs gas we can filter
        float removal_ratio = min(1, volume_rate / Dm.num(environment.volume));

        float total_moles_to_remove = 0;
        foreach ((object gas_id, object value) in Dm.pairs(Dm.bitand(cached_moles, scrubbing)))
            total_moles_to_remove += Dm.num(value);

        if (!Dm.truthy(total_moles_to_remove)) //no gases to remove
            return FALSE;

        foreach ((object gas_id, object value) in Dm.pairs(Dm.bitand(cached_moles, scrubbing)))
        {
            float v = Dm.num(value);
            float transferred_moles = max(QUANTIZE(v * removal_ratio * (v / total_moles_to_remove)), min(MOLAR_ACCURACY * 1000, v));
            Dm.append_at(filtered_out.moles, gas_id, transferred_moles);
            Dm.remove_at(cached_moles, gas_id, transferred_moles);
        }

        environment.garbage_collect();

        //Remix the resulting gases
        air_contents.merge(filtered_out);
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/portable/scrubber.dm:197
    public override void unregister_holding()
    {
        on = FALSE;
        base.unregister_holding();
    }

    // tg: code/modules/atmospherics/machinery/portable/scrubber.dm:158
    public override async DmTask<object> replace_tank(mob_living user = null, object close_valve = null, obj_item_tank new_tank = null)
    {
        object dot = await base.replace_tank(user, close_valve, new_tank);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(close_valve))
        {
            if (Dm.truthy(on))
            {
                on = FALSE;
                update_appearance();
            }
        }
        else if (Dm.truthy(on) && holding != null)
            user.investigate_log(Dm.text("started a transfer into [].", holding), INVESTIGATE_ATMOS);
        return dot;
    }
}

// tg: code/modules/atmospherics/machinery/portable/scrubber.dm:201
public partial class obj_machinery_portable_atmospherics_scrubber_huge : obj_machinery_portable_atmospherics_scrubber
{
    // tg: code/modules/atmospherics/machinery/portable/scrubber.dm:220
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]_[]", Dm.initial(this, (atom p) => p.icon_state), on);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/atmospherics/machinery/portable/scrubber.dm:224
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(seconds_per_tick, dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(object seconds_per_tick, DotBox<object> dot)
    {
        if ((!Dm.truthy(anchored) && !Dm.truthy(movable)) || !Dm.truthy(is_operational))
        {
            on = FALSE;
            update_appearance();
        }
        update_use_power(Dm.truthy(on) ? ACTIVE_POWER_USE : IDLE_POWER_USE);
        if (!Dm.truthy(on))
            return base.process_atmos(seconds_per_tick);

        excited = TRUE;

        if (holding == null)
        {
            turf T = get_turf(this);
            foreach (turf AT in Dm.each<turf>(T.get_atmos_adjacent_turfs(alldir: TRUE)))
            {
                if (Dm.truthy(scrub(await AT.return_air() as datum_gas_mixture)))
                    AT.air_update_turf(FALSE, FALSE);
            }
        }

        return base.process_atmos(seconds_per_tick);
    }

    // tg: code/modules/atmospherics/machinery/portable/scrubber.dm:242
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        if (Dm.truthy(await default_unfasten_wrench(user, tool)))
        {
            if (!Dm.truthy(movable))
                on = FALSE;
            return ITEM_INTERACT_SUCCESS;
        }
        return FALSE;
    }
}

// tg: code/modules/atmospherics/machinery/portable/scrubber.dm:1
public partial class obj_machinery_portable_atmospherics_scrubber : obj_machinery_portable_atmospherics
{
    // tg: code/modules/atmospherics/machinery/portable/scrubber.dm:35  (M5 slice 7)
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        turf local_turf = get_turf(this) as turf;
        await local_turf.assume_air(air_contents);
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/atmospherics/machinery/portable/scrubber.dm:125
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "PortableScrubber", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/portable/scrubber.dm:131
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list(
            Dm.kv("on", on),
            Dm.kv("connected", Dm.truthy(connected_port) ? 1 : 0),
            Dm.kv("pressure", round(Dm.num(air_contents.return_pressure()))),
            Dm.kv("hasHypernobCrystal", Dm.truthy(nob_crystal_inserted) ? TRUE : FALSE),
            Dm.kv("reactionSuppressionEnabled", Dm.truthy(suppress_reactions) ? TRUE : FALSE)
        );

        DmList filterTypes = Dm.list();
        data["filterTypes"] = filterTypes;
        DmList cached_gas_info = GLOB.meta_gas_info;
        foreach (object path in Dm.each(cached_gas_info[META_GAS_ID]))
            Dm.append(filterTypes, Dm.list(Dm.list(
                Dm.kv("gasId", ((DmList)cached_gas_info[META_GAS_ID])[path]),
                Dm.kv("gasName", ((DmList)cached_gas_info[META_GAS_NAME])[path]),
                Dm.kv("enabled", Dm.@in(path, scrubbing) ? TRUE : FALSE)
            )));

        if (holding != null)
        {
            datum_gas_mixture holding_mix = await holding.return_air() as datum_gas_mixture;
            data["holding"] = Dm.list(
                Dm.kv("name", holding.name),
                Dm.kv("pressure", round(Dm.num(holding_mix.return_pressure())))
            );
        }
        else
            data["holding"] = null;
        return data;
    }

    // tg: code/modules/atmospherics/machinery/portable/scrubber.dm:169
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "power":
                on = Dm.truthy(on) ? FALSE : TRUE;
                if (Dm.truthy(on))
                    SSair.start_processing_machine(this);
                dot = TRUE;
                break;

            case "eject":
                if (holding != null)
                {
                    await replace_tank(World.Current.usr as mob_living, FALSE);
                    dot = TRUE;
                }
                break;

            case "toggle_filter":
                Dm.bitxor_assign(ref scrubbing, gas_id2path(@params["val"]));
                dot = TRUE;
                break;

            case "reaction_suppression":
                if (!Dm.truthy(nob_crystal_inserted))
                {
                    message_admins(Dm.text("[] tried to toggle reaction suppression on a scrubber without a noblium crystal inside, possible href exploit attempt.", ADMIN_LOOKUPFLW(usr)));
                    return dot;
                }
                suppress_reactions = Dm.truthy(suppress_reactions) ? FALSE : TRUE;
                SSair.start_processing_machine(this);
                message_admins(Dm.text("[] turned [] the [] reaction suppression.", ADMIN_LOOKUPFLW(usr), Dm.truthy(suppress_reactions) ? "on" : "off", this));
                usr.investigate_log(Dm.text("turned [] the [] reaction suppression.", Dm.truthy(suppress_reactions) ? "on" : "off", this), INVESTIGATE_ATMOS);
                dot = TRUE;
                break;
        }

        update_appearance();
        return dot;
    }
}
