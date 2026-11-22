// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/gasmixtures/immutable_mixtures.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated in M2 by P2 (scope.md 2e "atmosphere": immutable mixtures, the empty breath and the lungs' breath_out); share
// and temperature_share with the turf sharing (LINDA, M4b slice 1).

// tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:4
[DmPath("/datum/gas_mixture/immutable")]
public partial class datum_gas_mixture_immutable : datum_gas_mixture
{
    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:6
    public datum_gas_mixture_immutable()
    {
        gc_share = TRUE;
    }

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:5
    public object initial_temperature;

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:8
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        garbage_collect();
    }

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:12
    public override void garbage_collect()
    {
        temperature = temperature_archived = Dm.num(initial_temperature);
        moles.Cut();
        moles_archive.Cut();
    }

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:17
    public override void archive()
    {
        return; // nothing changes, so we do nothing
    }

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:20
    [DmParams()]
    public override object merge(datum_gas_mixture giver = null)
    {
        return FALSE; //we're immutable.
    }

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:23
    public override object share(datum_gas_mixture sharer = null, object our_coeff = null, object sharer_coeff = null)
    {
        object dot = base.share(sharer, our_coeff, sharer_coeff);
        sharer.temperature = Dm.num(initial_temperature);
        garbage_collect();
        return dot;
    }

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:28
    [DmParams()]
    public override async DmTask<object> react(datum holder = null)
    {
        return FALSE; //we're immutable.
    }

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:31
    public override object copy()
    {
        return Dm.@new(type); //we're immutable, so we can just return a new instance.
    }

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:34
    [DmParams()]
    public override object copy_from(datum_gas_mixture sample = null)
    {
        return FALSE; //we're immutable.
    }

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:37
    [DmParams()]
    public override object copy_from_ratio(datum_gas_mixture sample = null, float? partial = null)
    {
        return FALSE; //we're immutable.
    }

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:40
    public override object temperature_share(datum_gas_mixture sharer = null, object conduction_coefficient = null, object sharer_temperature = null, object sharer_heat_capacity = null)
    {
        object dot = base.temperature_share(sharer, conduction_coefficient, sharer_temperature, sharer_heat_capacity);
        temperature = Dm.num(initial_temperature);
        return dot;
    }
}

// tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:45
[DmPath("/datum/gas_mixture/immutable/space")]
public partial class datum_gas_mixture_immutable_space : datum_gas_mixture_immutable
{
    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:46
    public datum_gas_mixture_immutable_space()
    {
        initial_temperature = TCMB;
    }

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:48
    public override object heat_capacity()
    {
        return HEAT_CAPACITY_VACUUM;
    }

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:51
    [DmParams()]
    public override object remove(object amount = null)
    {
        return copy(); //we're always empty, so we can just return a copy.
    }

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:54
    [DmParams()]
    public override object remove_ratio(object ratio = null)
    {
        return copy(); //we're always empty, so we can just return a copy.
    }
}

// tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:58
[DmPath("/datum/gas_mixture/immutable/planetary")]
public partial class datum_gas_mixture_immutable_planetary : datum_gas_mixture_immutable
{
    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:59  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        initial_gas = Dm.list();
    }

    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:59
    public DmList initial_gas;

    // Intentionally duplicate code to save microseconds on a call to parent
    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:62
    public override void garbage_collect()
    {
        temperature = temperature_archived = Dm.num(initial_temperature);
        moles = initial_gas.Copy();
        moles_archive = initial_gas.Copy();
    }
}

// tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:67
public partial class datum_gas_mixture_immutable_planetary : datum_gas_mixture_immutable
{
    // tg: code/modules/atmospherics/gasmixtures/immutable_mixtures.dm:67
    public virtual void parse_string_immutable(object gas_string = null)
    {
        gas_string = SSair.preprocess_gas_string(gas_string);

        DmList mix = initial_gas;
        DmList gas = params2list(gas_string);
        if (Dm.truthy(gas["TEMP"]))
        {
            initial_temperature = text2num(gas["TEMP"]);
            temperature_archived = Dm.num(initial_temperature);
            temperature = Dm.num(initial_temperature);
            Dm.remove(ref gas, "TEMP");
        }
        mix.Cut();
        foreach ((object id, object value) in Dm.pairs(gas))
        {
            object path = id;
            if (!ispath(path))
                path = gas_id2path(path); //a lot of these strings can't have embedded expressions (especially for mappers), so support for IDs needs to stick around
            mix[path] = text2num(value);
        }

        DmList cached_moles = moles;
        DmList cached_moles_archive = moles_archive;
        foreach ((object gas_id, object value) in Dm.pairs(mix))
            cached_moles[gas_id] = cached_moles_archive[gas_id] = value;
    }
}
