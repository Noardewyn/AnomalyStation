// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/virus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). virus.dm is the earliest file of tgstation.dme that declares something on /datum/disease
// (/datum/disease/proc/admin_details, virus.dm:6) and on /datum/disease/advance (its admin_details, virus.dm:10), so this part
// carries their [DmPath] (rules 2.3).

public static partial class GlobalProcs
{
    /// Logging for the creation and contraction of viruses
    // tg: code/__HELPERS/logging/virus.dm:2
    public static void log_virus(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_VIRUS, text, data);
    }
}

// tg: code/__HELPERS/logging/virus.dm:6
[DmPath("/datum/disease")]
public partial class datum_disease : datum
{
    /// Returns a string for admin logging uses, should describe the disease in detail
    // tg: code/__HELPERS/logging/virus.dm:6
    public virtual object admin_details()
    {
        return Dm.text("[] : []", name, type);
    }
}

// tg: code/__HELPERS/logging/virus.dm:10
[DmPath("/datum/disease/advance")]
public partial class datum_disease_advance : datum_disease
{
    /// Describes this disease to an admin in detail (for logging)
    // tg: code/__HELPERS/logging/virus.dm:10
    public override object admin_details()
    {
        DmList name_symptoms = Dm.list();
        foreach (datum_symptom S in Dm.each<datum_symptom>(symptoms))
            Dm.append(ref name_symptoms, S.name);
        return Dm.text("[] - sym: [] re:[] st:[] ss:[] tr:[] se:[]", name, english_list(name_symptoms), totalResistance(), totalStealth(), totalStageSpeed(), totalTransmittable(), totalSeverity());
    }
}
