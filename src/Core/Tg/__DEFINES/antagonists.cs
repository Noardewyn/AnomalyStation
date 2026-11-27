// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/antagonists.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate A2, D316): the traitors' employers (GLOB.syndicate_employers, nanotrasen_employers, hijack_employers,
// normal_employers) and the IS_TRAITOR, IS_NUKE_OP, IS_CULTIST, GET_CULTIST, IS_HERETIC, GET_HERETIC and DESTROY_AI_PROB macros; the constants of the file are generated into Defines.

public partial class datum_controller_global_vars
{
    ///employers that are from the syndicate
    // tg: code/__DEFINES/antagonists.dm:211  GLOBAL_LIST_INIT(syndicate_employers, list(...))
    public DmList syndicate_employers;

    // tg: code/__DEFINES/antagonists.dm:211  (GLOBAL_MANAGED(syndicate_employers, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsyndicate_employers()
    {
        syndicate_employers = Dm.list(
            "Animal Rights Consortium",
            "Bee Liberation Front",
            "Cybersun Industries",
            "Donk Corporation",
            "Gorlex Marauders",
            "MI13",
            "Tiger Cooperative Fanatic",
            "Waffle Corporation Terrorist",
            "Waffle Corporation");
        Dm.append(ref gvars_datum_init_order, "syndicate_employers");
    }

    ///employers that are from Nanotrasen
    // tg: code/__DEFINES/antagonists.dm:223  GLOBAL_LIST_INIT(nanotrasen_employers, list(...))
    public DmList nanotrasen_employers;

    // tg: code/__DEFINES/antagonists.dm:223  (GLOBAL_MANAGED(nanotrasen_employers, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnanotrasen_employers()
    {
        nanotrasen_employers = Dm.list(
            "Champions of Evil",
            "Corporate Climber",
            "Gone Postal",
            "Internal Affairs Agent",
            "Legal Trouble");
        Dm.append(ref gvars_datum_init_order, "nanotrasen_employers");
    }

    ///employers who hire agents to do the hijack
    // tg: code/__DEFINES/antagonists.dm:232  GLOBAL_LIST_INIT(hijack_employers, list(...))
    public DmList hijack_employers;

    // tg: code/__DEFINES/antagonists.dm:232  (GLOBAL_MANAGED(hijack_employers, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhijack_employers()
    {
        hijack_employers = Dm.list(
            "Animal Rights Consortium",
            "Bee Liberation Front",
            "Gone Postal",
            "Tiger Cooperative Fanatic",
            "Waffle Corporation Terrorist");
        Dm.append(ref gvars_datum_init_order, "hijack_employers");
    }

    ///employers who hire agents to do a task and escape... or martyrdom. whatever
    // tg: code/__DEFINES/antagonists.dm:241  GLOBAL_LIST_INIT(normal_employers, list(...))
    public DmList normal_employers;

    // tg: code/__DEFINES/antagonists.dm:241  (GLOBAL_MANAGED(normal_employers, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnormal_employers()
    {
        normal_employers = Dm.list(
            "Champions of Evil",
            "Corporate Climber",
            "Cybersun Industries",
            "Donk Corporation",
            "Gorlex Marauders",
            "Internal Affairs Agent",
            "Legal Trouble",
            "MI13",
            "Waffle Corporation");
        Dm.append(ref gvars_datum_init_order, "normal_employers");
    }

    ///employers for malfunctioning ais. they do not have sides, unlike traitors.
    // tg: code/__DEFINES/antagonists.dm:254  GLOBAL_LIST_INIT(ai_employers, list(...)) (silicons SI8)
    public DmList ai_employers;

    // tg: code/__DEFINES/antagonists.dm:254  (GLOBAL_MANAGED(ai_employers, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalai_employers()
    {
        ai_employers = Dm.list(
            "Biohazard",
            "Despotic Ruler",
            "Fanatical Revelation",
            "Logic Core Error",
            "Problem Solver",
            "S.E.L.F.",
            "Something's Wrong",
            "Spam Virus",
            "SyndOS",
            "Unshackled",
            "Ratvarian Remnant");
        Dm.append(ref gvars_datum_init_order, "ai_employers");
    }
}

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/antagonists.dm:375  #define DESTROY_AI_PROB(denominator) (100 / denominator)
    public static float DESTROY_AI_PROB(object denominator) => 100 / Dm.num(denominator);

    // tg: code/__DEFINES/antagonists.dm:280  #define IS_CULTIST(mob) (mob?.mind?.has_antag_datum(/datum/antagonist/cult))
    public static object IS_CULTIST(object mob) => (mob as mob)?.mind?.has_antag_datum(typeof(datum_antagonist_cult));

    // tg: code/__DEFINES/antagonists.dm:283  #define GET_CULTIST(mob) (mob?.mind?.has_antag_datum(/datum/antagonist/cult))
    public static datum_antagonist_cult GET_CULTIST(object mob) => (mob as mob)?.mind?.has_antag_datum(typeof(datum_antagonist_cult)) as datum_antagonist_cult;

    // tg: code/__DEFINES/antagonists.dm:293  #define IS_HERETIC(mob) (mob.mind?.has_antag_datum(/datum/antagonist/heretic) || HAS_TRAIT(mob, TRAIT_ACT_AS_HERETIC))
    public static bool IS_HERETIC(object mob) => Dm.truthy((mob as mob)?.mind?.has_antag_datum(typeof(datum_antagonist_heretic))) || HAS_TRAIT(mob, TRAIT_ACT_AS_HERETIC);

    // tg: code/__DEFINES/antagonists.dm:295  #define GET_HERETIC(mob) (mob.mind?.has_antag_datum(/datum/antagonist/heretic))
    public static datum_antagonist_heretic GET_HERETIC(object mob) => (mob as mob)?.mind?.has_antag_datum(typeof(datum_antagonist_heretic)) as datum_antagonist_heretic;

    // tg: code/__DEFINES/antagonists.dm:273  #define IS_TRAITOR(mob) (mob?.mind?.has_antag_datum(/datum/antagonist/traitor))
    public static object IS_TRAITOR(object mob) => (mob as mob)?.mind?.has_antag_datum(typeof(datum_antagonist_traitor));

    // tg: code/__DEFINES/antagonists.dm:329  #define IS_MALF_AI(mob) (mob?.mind?.has_antag_datum(/datum/antagonist/malf_ai)) (silicons SI1: the law rack)
    public static object IS_MALF_AI(object mob) => (mob as mob)?.mind?.has_antag_datum(typeof(datum_antagonist_malf_ai));

    // tg: code/__DEFINES/antagonists.dm:309  #define IS_CHANGELING(mob) (mob?.mind?.has_antag_datum(/datum/antagonist/changeling)) (gaps K1)
    public static object IS_CHANGELING(object mob) => (mob as mob)?.mind?.has_antag_datum(typeof(datum_antagonist_changeling));

    /// Checks if the given mob is a revolutionary. Will return TRUE for rev heads as well.
    // tg: code/__DEFINES/antagonists.dm:323  #define IS_REVOLUTIONARY(mob) (mob?.mind?.has_antag_datum(/datum/antagonist/rev)) (gaps RG1: the drinks)
    public static object IS_REVOLUTIONARY(object mob) => (mob as mob)?.mind?.has_antag_datum(typeof(datum_antagonist_rev));

    // tg: code/__DEFINES/antagonists.dm:312  #define IS_NUKE_OP(mob) (mob?.mind?.has_antag_datum(/datum/antagonist/nukeop))
    public static object IS_NUKE_OP(object mob) => (mob as mob)?.mind?.has_antag_datum(typeof(datum_antagonist_nukeop));
}
