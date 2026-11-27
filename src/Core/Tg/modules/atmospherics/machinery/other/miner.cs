// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/other/miner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the gas miners, vents that pour one gas into the room around them while they are powered (from a cable node under
// them or from the area) and stop with a debug message when their vent is blocked, open to the planet's air or to space, or the room
// is too full. The gas kinds are data (their var blocks are generated).

// tg: code/modules/atmospherics/machinery/other/miner.dm:7
public partial class obj_machinery_atmospherics_miner : obj_machinery_atmospherics
{
    // tg: code/modules/atmospherics/machinery/other/miner.dm:1-5
    public const int GASMINER_POWER_NONE = 0;
    public const int GASMINER_POWER_STATIC = 1;
    public const int GASMINER_POWER_MOLES = 2; //Scaled from here on down.
    public const int GASMINER_POWER_KPA = 3;
    public const int GASMINER_POWER_FULLSCALE = 4;

    // tg: code/modules/atmospherics/machinery/other/miner.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_active(active); //Force overlay update.
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/other/miner.dm:37
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        if (Dm.truthy(broken))
            Dm.append(ref dot, I18n.T("Its debug output is printing \"[]\".", broken_message));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/other/miner.dm:42
    public virtual async DmTask<object> check_operation()
    {
        if (!Dm.truthy(active))
            return FALSE;
        turf T = get_turf(this);
        if (!isopenturf(T))
        {
            broken_message = span_boldnotice(I18n.T("VENT BLOCKED"));
            set_broken(TRUE);
            return FALSE;
        }
        var OT = (turf_open)T;
        if (Dm.truthy(OT.planetary_atmos))
        {
            broken_message = span_boldwarning(I18n.T("DEVICE NOT ENCLOSED IN A PRESSURIZED ENVIRONMENT"));
            set_broken(TRUE);
            return FALSE;
        }
        if (isspaceturf(T))
        {
            broken_message = span_boldnotice(I18n.T("AIR VENTING TO SPACE"));
            set_broken(TRUE);
            return FALSE;
        }
        var G = await OT.return_air() as datum_gas_mixture;
        if (Dm.num(G.return_pressure()) > (max_ext_kpa - ((spawn_mol * spawn_temp * R_IDEAL_GAS_EQUATION) / CELL_VOLUME)))
        {
            broken_message = span_boldwarning(I18n.T("EXTERNAL PRESSURE OVER THRESHOLD"));
            set_broken(TRUE);
            return FALSE;
        }
        if (Dm.num(G.total_moles()) > max_ext_mol)
        {
            broken_message = span_boldwarning(I18n.T("EXTERNAL AIR CONCENTRATION OVER THRESHOLD"));
            set_broken(TRUE);
            return FALSE;
        }
        if (Dm.truthy(broken))
        {
            set_broken(FALSE);
            broken_message = "";
        }
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/other/miner.dm:73
    public virtual void set_active(object setting = null)
    {
        if (!Dm.eq(active, setting))
        {
            active = Dm.num(setting);
            update_appearance();
        }
    }

    // tg: code/modules/atmospherics/machinery/other/miner.dm:78
    public virtual void set_broken(object setting = null)
    {
        if (!Dm.eq(broken, setting))
        {
            broken = Dm.num(setting);
            update_appearance();
        }
    }

    // tg: code/modules/atmospherics/machinery/other/miner.dm:83
    // tg passes the new draw (from the pressure P for the scaled modes) as a second argument that update_use_power (one parameter)
    // ignores; the calls keep the one it reads.
    public virtual async DmTask update_power()
    {
        if (!Dm.truthy(active))
            active_power_usage = idle_power_usage;
        turf T = get_turf(this);
        var G = await T.return_air() as datum_gas_mixture;
        float P = Dm.num(G.return_pressure());
        switch (power_draw)
        {
            case GASMINER_POWER_NONE:
                update_use_power(ACTIVE_POWER_USE);
                break;
            case GASMINER_POWER_STATIC:
                update_use_power(ACTIVE_POWER_USE);
                break;
            case GASMINER_POWER_MOLES:
                update_use_power(ACTIVE_POWER_USE);
                break;
            case GASMINER_POWER_KPA:
                update_use_power(ACTIVE_POWER_USE);
                break;
            case GASMINER_POWER_FULLSCALE:
                update_use_power(ACTIVE_POWER_USE);
                break;
        }
    }

    // tg: code/modules/atmospherics/machinery/other/miner.dm:101
    public virtual async DmTask<object> do_use_energy(object amount = null)
    {
        turf T = get_turf(this);
        if (T != null)
        {
            var C = T.get_cable_node() as obj_structure_cable; //check if we have a node cable on the machine turf, the first found is picked
            if (C != null && C.powernet != null && (C.powernet.avail > Dm.num(amount)))
            {
                C.powernet.load = Dm.num(C.powernet.load) + Dm.num(amount);
                return TRUE;
            }
        }
        if (Dm.truthy(powered__proc()))
        {
            await use_energy(amount);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/atmospherics/machinery/other/miner.dm:113
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList ?? Dm.list();
        if (Dm.truthy(broken))
        {
            Dm.append(ref dot, "broken");
            return dot;
        }

        if (Dm.truthy(active))
        {
            var on_overlay = (mutable_appearance)mutable_appearance(icon, "on");
            on_overlay.color = overlay_color;
            Dm.append(ref dot, on_overlay);
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/other/miner.dm:124
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        Waiting.NoSleep(update_power());
        Waiting.NoSleep(check_operation());
        if (Dm.truthy(active) && !Dm.truthy(broken))
        {
            if (spawn_id == null)
                return FALSE;
            if (Dm.truthy(Waiting.NoSleep(do_use_energy(active_power_usage))))
                Waiting.NoSleep(mine_gas(Dm.num(seconds_per_tick)));
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/other/miner.dm:133
    public virtual async DmTask<object> mine_gas(float seconds_per_tick = 2)
    {
        var O = get_turf(this) as turf_open;
        if (!isopenturf(O))
            return FALSE;
        var merger = Dm.@new<datum_gas_mixture>();
        merger.assert_gas(spawn_id);
        merger.moles[spawn_id] = spawn_mol * seconds_per_tick;
        merger.temperature = spawn_temp;
        await O.assume_air(merger);
        return null;
    }

    // tg: code/modules/atmospherics/machinery/other/miner.dm:143
    public override async DmTask<object> attack_ai(mob user = null)
    {
        if (Dm.truthy(broken))
            to_chat(user, I18n.T("[] seems to be broken. Its debug interface outputs: []", this, broken_message));
        return await base.attack_ai(user);
    }
}
