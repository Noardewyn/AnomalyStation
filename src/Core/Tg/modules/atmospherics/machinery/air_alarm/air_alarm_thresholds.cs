// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/air_alarm/air_alarm_thresholds.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the air alarms): the whole file. The var blocks (the subtypes' thresholds) are generated.

// A datum for dealing with threshold limit values
// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_thresholds.dm:2
public partial class datum_tlv : datum
{
    /** Initialize a TLV and set its values if given arguments, mostly for map varedits.
     * We provide this functionality but please consider not doing this and making proper subtypes.
     * Only by doing the latter will [datum/tlv/proc/reset_value] work.
     */
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_thresholds.dm:12
    [DmParams("min2", "min1", "max1", "max2")]
    public override async DmTask New(params object[] __extra)
    {
        object min2 = Dm.arg(__extra, 0);
        object min1 = Dm.arg(__extra, 1);
        object max1 = Dm.arg(__extra, 2);
        object max2 = Dm.arg(__extra, 3);
        if (Dm.truthy(min2))
            hazard_min = Dm.num(min2);
        if (Dm.truthy(min1))
            warning_min = Dm.num(min1);
        if (Dm.truthy(max1))
            warning_max = Dm.num(max1);
        if (Dm.truthy(max2))
            hazard_max = Dm.num(max2);
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_thresholds.dm:22
    public virtual object check_value(object val = null)
    {
        float v = Dm.num(val);
        if (hazard_max != TLV_VALUE_IGNORE && v >= hazard_max)
            return AIR_ALARM_ALERT_HAZARD;
        if (hazard_min != TLV_VALUE_IGNORE && v <= hazard_min)
            return AIR_ALARM_ALERT_HAZARD;
        if (warning_max != TLV_VALUE_IGNORE && v >= warning_max)
            return AIR_ALARM_ALERT_WARNING;
        if (warning_min != TLV_VALUE_IGNORE && v <= warning_min)
            return AIR_ALARM_ALERT_WARNING;

        return AIR_ALARM_ALERT_NONE;
    }

    /** Set this particular TLV
     *
     * Arguments:
     * * threshold_type: What kind of variable do we want to set. Accepts bitfield subsets of [TLV_VAR_ALL].
     * * value: How much to set it to. Accepts a number or [TLV_VALUE_IGNORE]
     */
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_thresholds.dm:40
    public virtual void set_value(object threshold_type = null, object value = null)
    {
        if (Dm.truthy(Dm.bitand(threshold_type, TLV_VAR_WARNING_MIN)))
            warning_min = Dm.num(value);
        if (Dm.truthy(Dm.bitand(threshold_type, TLV_VAR_HAZARD_MIN)))
            hazard_min = Dm.num(value);
        if (Dm.truthy(Dm.bitand(threshold_type, TLV_VAR_WARNING_MAX)))
            warning_max = Dm.num(value);
        if (Dm.truthy(Dm.bitand(threshold_type, TLV_VAR_HAZARD_MAX)))
            hazard_max = Dm.num(value);
    }

    /** Reset this particular TLV to its original value.
     *
     * Arguments:
     * * threshold_type: What kind of variable do we want to set. Accepts bitfield subsets of [TLV_VAR_ALL].
     */
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_thresholds.dm:55
    public virtual void reset_value(object threshold_type = null)
    {
        if (Dm.truthy(Dm.bitand(threshold_type, TLV_VAR_WARNING_MIN)))
            warning_min = Dm.num(Dm.initial(this, (datum_tlv p) => p.warning_min));
        if (Dm.truthy(Dm.bitand(threshold_type, TLV_VAR_HAZARD_MIN)))
            hazard_min = Dm.num(Dm.initial(this, (datum_tlv p) => p.hazard_min));
        if (Dm.truthy(Dm.bitand(threshold_type, TLV_VAR_WARNING_MAX)))
            warning_max = Dm.num(Dm.initial(this, (datum_tlv p) => p.warning_max));
        if (Dm.truthy(Dm.bitand(threshold_type, TLV_VAR_HAZARD_MAX)))
            hazard_max = Dm.num(Dm.initial(this, (datum_tlv p) => p.hazard_max));
    }
}
