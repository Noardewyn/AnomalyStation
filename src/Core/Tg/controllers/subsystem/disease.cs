// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/disease.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): the disease archive (one prototype per common disease, each advance disease by its symptom id)
// and the symptom list whose index is a symptom's id. The var block is generated (D111).

// tg: code/controllers/subsystem/disease.dm:11
public partial class datum_controller_subsystem_disease : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/disease.dm:9  var/static/list/list_symptoms = subtypesof(/datum/symptom)
    // Made on first read (rules 10.6: it creates no datum); a harness world without SSdisease reads it too (a symptom's New).
    public static DmList list_symptoms => TgState.Current.datum_controller_subsystem_disease__list_symptoms ??= subtypesof(typeof(datum_symptom)) as DmList;

    // The New() that SUBSYSTEM_DEF(disease) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSdisease) (__DEFINES/MC.dm:29) expanded inline
    // tg: code/controllers/subsystem/disease.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSdisease != this)
        {
            if (SSdisease is datum_controller_subsystem_disease)
            {
                Recover();
                qdel(SSdisease);
            }
            SSdisease = this;
        }
        PreInit();
        await DmTask.CompletedTask;
    }

    // tg: code/controllers/subsystem/disease.dm:11
    public override void PreInit()
    {
        if (!Dm.truthy(diseases))
            diseases = subtypesof(typeof(datum_disease)) as DmList;
    }

    // tg: code/controllers/subsystem/disease.dm:15
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList all_common_diseases = Dm.sub(diseases, typesof(typeof(datum_disease_advance))) as DmList;
        foreach (object common_disease_type in Dm.each(all_common_diseases))
        {
            datum_disease prototype = (datum_disease)await Dm.new_async((Type)common_disease_type);
            archive_diseases[prototype.GetDiseaseID()] = prototype;
        }
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/disease.dm:22
    public override object stat_entry(object msg = null)
    {
        msg = Dm.text("P:[]", length(active_diseases));
        return base.stat_entry(msg);
    }

    // tg: code/controllers/subsystem/disease.dm:26
    public virtual object get_disease_name(object id = null)
    {
        datum_disease_advance A = archive_diseases[id] as datum_disease_advance;
        if (Dm.truthy(A.name))
            return A.name;
        else
            return "Unknown";
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/disease.dm:1  SUBSYSTEM_DEF(disease): GLOBAL_REAL(SSdisease, /datum/controller/subsystem/disease)
    internal datum_controller_subsystem_disease SSdisease;
    // tg: code/controllers/subsystem/disease.dm:9  /datum/controller/subsystem/disease/var/static/list/list_symptoms
    internal DmList datum_controller_subsystem_disease__list_symptoms;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_disease SSdisease => ref TgState.Current.SSdisease;
}
