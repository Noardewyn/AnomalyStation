// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/portable/pipe_scrubber.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the pipe scrubbers): the init (the internal canister; its break and deletion deconstruct the
// scrubber since M5 slice 10), return_analyzable_air, the alt-click and
// tank refusals, the picture, process_atmos and scrub, insert_nob_crystal and toggle_reaction_suppression. Package E3
// (docs/ai/specs/engineering.md): the tgui window (ui_interact, ui_data, ui_static_data, ui_act; the scrubber is switched on there);
// its admin messages and investigate logs are deferred leaves (admin tooling, M10). M5 slice 10: atom_deconstruct (the scrubber's and
// its canister's air to the turf) and the welder act (the hull repair of the canister and the scrubber). The var block is
// generated.

// tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:1
public partial class obj_machinery_portable_atmospherics_pipe_scrubber : obj_machinery_portable_atmospherics
{
    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:34
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        internal_tank = Dm.@new<obj_machinery_portable_atmospherics_canister>(this);
        RegisterSignal(internal_tank, COMSIG_ATOM_BREAK, nameof(deconstruct__signal));
        RegisterSignal(internal_tank, COMSIG_QDELETING, nameof(deconstruct__signal));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:37-38  PROC_REF(deconstruct) as the signal handler: DM
    // binds the signal's first argument, the canister (the source), to deconstruct's disassembled, so the scrubber is
    // deconstructed with a truthy disassembled. The slot's disassembled is a number (rules Appendix A), so this adapter
    // passes TRUE for the source.
    [SignalHandler]
    public virtual object deconstruct__signal(datum source = null, object extra = null)
    {
        Waiting.NoSleep(deconstruct(Dm.truthy(source) ? TRUE : FALSE));
        return null;
    }

    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:40
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        await base.atom_deconstruct(disassembled);
        turf my_turf = get_turf(this);
        await my_turf.assume_air(air_contents);
        await my_turf.assume_air(internal_tank.air_contents);
        SSair.stop_processing_machine(internal_tank);
        qdel(internal_tank);
    }

    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:54
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        await internal_tank.welder_act(user, tool);
        return await base.welder_act(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:48
    public override object return_analyzable_air()
    {
        return Dm.list(
            air_contents,
            internal_tank.air_contents);
    }

    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:58
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        return CLICK_ACTION_BLOCKING;
    }

    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:61
    public override async DmTask<object> replace_tank(mob_living user = null, object close_valve = null, obj_item_tank new_tank = null)
    {
        return FALSE;
    }

    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:64
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.truthy(on) ? Dm.text("[]_active", Dm.initial(this, (atom p) => p.icon_state)) : Dm.initial(this, (atom p) => p.icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:68
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

        if (Dm.eq(direction, PUMP_IN))
            scrub();
        else
            internal_tank.air_contents.pump_gas_to(air_contents, PUMP_MAX_PRESSURE);

        return base.process_atmos(seconds_per_tick);
    }

    /// Scrub gasses from own air_contents into internal_tank.air_contents
    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:82
    public virtual void scrub()
    {
        if (Dm.num(internal_tank.air_contents.return_pressure()) >= PUMP_MAX_PRESSURE)
            return;

        float transfer_moles = min(1, volume_rate / Dm.num(air_contents.volume)) * Dm.num(air_contents.total_moles());

        datum_gas_mixture filtering = air_contents.remove(transfer_moles) as datum_gas_mixture; // Remove part of the mixture to filter.
        datum_gas_mixture filtered = Dm.@new<datum_gas_mixture>();
        if (filtering == null)
            return;

        filtered.temperature = filtering.temperature;
        foreach (object gas in Dm.each(Dm.bitand(filtering.moles, scrubbing)))
        {
            filtered.add_gas(gas);
            filtered.moles[gas] = filtering.moles[gas]; // Shuffle the "bad" gasses to the filtered mixture.
            filtering.moles[gas] = 0;
        }
        filtering.garbage_collect(); // Now that the gasses are set to 0, clean up the mixture.

        internal_tank.air_contents.merge(filtered); // Store filtered out gasses.
        air_contents.merge(filtering); // Returned the cleaned gas.
    }
    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:165
    public override void insert_nob_crystal()
    {
        base.insert_nob_crystal();
        internal_tank.nob_crystal_inserted = TRUE;
    }

    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:169
    public virtual void toggle_reaction_suppression()
    {
        float new_value = Dm.truthy(suppress_reactions) ? FALSE : TRUE;
        suppress_reactions = new_value;
        internal_tank.suppress_reactions = new_value;
    }

    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:104
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "PipeScrubber", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:110
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list(
            Dm.kv("on", on),
            Dm.kv("direction", direction),
            Dm.kv("connected", Dm.truthy(connected_port) ? 1 : 0),
            Dm.kv("pressureTank", round(Dm.num(internal_tank.air_contents.return_pressure()))),
            Dm.kv("pressurePump", round(Dm.num(air_contents.return_pressure()))),
            Dm.kv("hasHypernobCrystal", nob_crystal_inserted),
            Dm.kv("reactionSuppressionEnabled", suppress_reactions)
        );

        DmList filterTypes = Dm.list();
        data["filterTypes"] = filterTypes;
        DmList cached_gas_info = GLOB.meta_gas_info;
        foreach (object gas_path in Dm.each(cached_gas_info[META_GAS_ID]))
            Dm.append(filterTypes, Dm.list(Dm.list(
                Dm.kv("gasId", ((DmList)cached_gas_info[META_GAS_ID])[gas_path]),
                Dm.kv("gasName", ((DmList)cached_gas_info[META_GAS_NAME])[gas_path]),
                Dm.kv("enabled", Dm.@in(gas_path, scrubbing) ? TRUE : FALSE)
            )));

        return data;
    }

    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:131
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        return Dm.list(
            Dm.kv("pressureLimitPump", pressure_limit),
            Dm.kv("pressureLimitTank", internal_tank.pressure_limit)
        );
    }

    // tg: code/modules/atmospherics/machinery/portable/pipe_scrubber.dm:137
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
                {
                    SSair.start_processing_machine(this);
                    SSair.start_processing_machine(internal_tank);
                }
                dot = TRUE;
                break;

            case "direction":
                direction = Dm.truthy(direction) ? FALSE : TRUE;
                dot = TRUE;
                break;

            case "toggle_filter":
                Dm.bitxor_assign(ref scrubbing, gas_id2path(@params["val"]));
                dot = TRUE;
                break;

            case "reaction_suppression":
                if (!Dm.truthy(internal_tank.nob_crystal_inserted))
                {
                    message_admins(Dm.text("[] tried to toggle reaction suppression on a pipe scrubber without a noblium crystal inside, possible href exploit attempt.", ADMIN_LOOKUPFLW(usr)));
                    return dot;
                }
                internal_tank.suppress_reactions = Dm.truthy(internal_tank.suppress_reactions) ? FALSE : TRUE;
                SSair.start_processing_machine(internal_tank);
                message_admins(Dm.text("[] turned [] the [] reaction suppression.", ADMIN_LOOKUPFLW(usr), Dm.truthy(internal_tank.suppress_reactions) ? "on" : "off", internal_tank));
                usr.investigate_log(Dm.text("turned [] the [] reaction suppression.", Dm.truthy(internal_tank.suppress_reactions) ? "on" : "off", internal_tank), INVESTIGATE_ATMOS);
                dot = TRUE;
                break;
        }

        update_appearance();
        return dot;
    }
}
