// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/masks/gas_filter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/clothing/masks/gas_filter.dm:14
public partial class obj_item_gas_filter : obj_item
{
    private const float HIGH_FILTERING_RATIO = 0.01f;
    private const float LOW_FILTERING_RATIO = 0.0025f;
    private const float HIGH_FILTERING_MOLES = 0.001f;
    private const float MID_FILTERING_MOLES = 0.0025f;
    private const float LOW_FILTERING_MOLES = 0.005f;
    private const float FILTERS_CONSTANT_WEAR = 0.025f;

    // tg: code/modules/clothing/masks/gas_filter.dm:55
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("[] is at <b>[]%</b> durability.", this, filter_status)));
        return dot;
    }

    /**
     * called by the gas mask where the filter is installed, lower the filter_status depending on the breath gas composition and by the strength of the filter
     * returns the modified breath gas mixture
     *
     * Arguments:
     * * breath - the current gas mixture of the breathed air
     *
     */
    // tg: code/modules/clothing/masks/gas_filter.dm:67
    public virtual object reduce_filter_status(datum_gas_mixture breath = null)
    {
        float danger_points = 0;

        foreach (object gas_id in Dm.each(breath.moles))
        {
            float amount = Dm.num(breath.moles[gas_id]);
            if (Dm.@in(gas_id, high_filtering_gases))
            {
                if (amount > HIGH_FILTERING_MOLES)
                {
                    breath.set_gas(gas_id, Dm.max(amount - filter_strength_high * filter_efficiency * HIGH_FILTERING_RATIO, 0));
                    danger_points += 1;
                    continue;
                }
                breath.set_gas(gas_id, Dm.max(amount - filter_strength_high * filter_efficiency * LOW_FILTERING_RATIO, 0));
                danger_points += 0.2f;
                continue;
            }
            if (Dm.@in(gas_id, mid_filtering_gases))
            {
                if (amount > MID_FILTERING_MOLES)
                {
                    breath.set_gas(gas_id, Dm.max(amount - filter_strength_mid * filter_efficiency * HIGH_FILTERING_RATIO, 0));
                    danger_points += 1.25f;
                    continue;
                }
                breath.set_gas(gas_id, Dm.max(amount - filter_strength_mid * filter_efficiency * LOW_FILTERING_RATIO, 0));
                danger_points += 0.25f;
                continue;
            }
            if (Dm.@in(gas_id, low_filtering_gases))
            {
                if (amount > LOW_FILTERING_MOLES)
                {
                    breath.set_gas(gas_id, Dm.max(amount - filter_strength_low * filter_efficiency * HIGH_FILTERING_RATIO, 0));
                    danger_points += 1.5f;
                    continue;
                }
                breath.set_gas(gas_id, Dm.max(amount - filter_strength_low * filter_efficiency * LOW_FILTERING_RATIO, 0));
                danger_points += 0.5f;
                continue;
            }
        }

        filter_status = Dm.max(filter_status - danger_points - FILTERS_CONSTANT_WEAR, 0);
        return breath;
    }
}

// tg: code/modules/clothing/masks/gas_filter.dm:100
public partial class obj_item_gas_filter_damaged : obj_item_gas_filter
{
    // tg: code/modules/clothing/masks/gas_filter.dm:105
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        filter_status = rand(35, 65);
        return dot;
    }
}
