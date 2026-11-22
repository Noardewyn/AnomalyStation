// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/clothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J5, D298): the suit sensors' modes.

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/lists/clothing.dm:14  GLOBAL_LIST_INIT(suit_sensor_mode_to_defines, ...)
    public DmList suit_sensor_mode_to_defines;

    public virtual void InitGlobalsuit_sensor_mode_to_defines()
    {
        suit_sensor_mode_to_defines = Dm.list(
            Dm.kv("Off", SENSOR_OFF),
            Dm.kv("Binary vitals", SENSOR_LIVING),
            Dm.kv("Exact vitals", SENSOR_VITALS),
            Dm.kv("Tracking beacon", SENSOR_COORDS));
        Dm.append(ref gvars_datum_init_order, "suit_sensor_mode_to_defines");
    }
}
