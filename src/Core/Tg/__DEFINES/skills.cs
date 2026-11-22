// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/skills.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the two macros the defines converter does not emit (rules 11.1); the file's constants are generated.

public static partial class GlobalProcs
{
    //Allows us to get EXP from level, or level from EXP
    // tg: code/__DEFINES/skills.dm:24  #define SKILL_EXP_LIST list(SKILL_EXP_NONE, ...) (a new list at every use)
    public static DmList SKILL_EXP_LIST() => Dm.list(SKILL_EXP_NONE, SKILL_EXP_NOVICE, SKILL_EXP_APPRENTICE, SKILL_EXP_JOURNEYMAN, SKILL_EXP_EXPERT, SKILL_EXP_MASTER, SKILL_EXP_LEGENDARY);

    // Gets the reference for the skill type that was given
    // tg: code/__DEFINES/skills.dm:37  #define GetSkillRef(A) (SSskills.all_skills[A])
    public static object GetSkillRef(object A) => SSskills.all_skills[A];
}
