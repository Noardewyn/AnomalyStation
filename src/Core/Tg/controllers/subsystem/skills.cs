// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/skills.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. The var block is generated (D111).

/*!
This subsystem mostly exists to populate and manage the skill singletons.
*/

// tg: code/controllers/subsystem/skills.dm:5
public partial class datum_controller_subsystem_skills : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(skills) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSskills) (__DEFINES/MC.dm:29) expanded inline
    // tg: code/controllers/subsystem/skills.dm:5
    public override async DmTask New(params object[] __extra)
    {
        if (SSskills != this)
        {
            if (SSskills is datum_controller_subsystem_skills)
            {
                Recover();
                qdel(SSskills);
            }
            SSskills = this;
        }
        PreInit();
        await DmTask.CompletedTask;
    }

    // tg: code/controllers/subsystem/skills.dm:13
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        InitializeSkills();
        await DmTask.CompletedTask;
        return SS_INIT_SUCCESS;
    }

    ///Ran on initialize, populates the skills dictionary
    // tg: code/controllers/subsystem/skills.dm:18
    public virtual void InitializeSkills()
    {
        foreach (object type in Dm.each(GLOB.skill_types))
        {
            datum_skill @ref = Dm.@new((Type)type) as datum_skill;
            all_skills[type] = @ref;
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/skills.dm:5  SUBSYSTEM_DEF(skills): GLOBAL_REAL(SSskills, /datum/controller/subsystem/skills)
    internal datum_controller_subsystem_skills SSskills;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_skills SSskills => ref TgState.Current.SSskills;
}
