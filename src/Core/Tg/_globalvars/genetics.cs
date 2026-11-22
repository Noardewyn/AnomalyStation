// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/genetics.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Each GLOBAL_LIST_EMPTY(X) (__DEFINES/__globals.dm:42) is expanded inline (rules 10.6, 11.1 (b)): the GLOB var and its
// InitGlobalX proc (GLOBAL_MANAGED, __DEFINES/__globals.dm:5).
public partial class datum_controller_global_vars : datum_controller
{
    //faster than having to constantly loop for them
    // tg: code/_globalvars/genetics.dm:2  GLOBAL_LIST_EMPTY_TYPED(all_mutations, /datum/mutation) //type = initialized mutation
    public DmList all_mutations;

    // tg: code/_globalvars/genetics.dm:2
    public virtual void InitGlobalall_mutations()
    {
        all_mutations = Dm.list();
        Dm.append(ref gvars_datum_init_order, "all_mutations");
    }

    // tg: code/_globalvars/genetics.dm:3  GLOBAL_LIST_EMPTY(full_sequences) //type = correct sequence
    public DmList full_sequences;

    // tg: code/_globalvars/genetics.dm:3
    public virtual void InitGlobalfull_sequences()
    {
        full_sequences = Dm.list();
        Dm.append(ref gvars_datum_init_order, "full_sequences");
    }

    // tg: code/_globalvars/genetics.dm:4  GLOBAL_LIST_EMPTY(bad_mutations) //bad initialized mutations
    public DmList bad_mutations;

    // tg: code/_globalvars/genetics.dm:4
    public virtual void InitGlobalbad_mutations()
    {
        bad_mutations = Dm.list();
        Dm.append(ref gvars_datum_init_order, "bad_mutations");
    }

    // tg: code/_globalvars/genetics.dm:5  GLOBAL_LIST_EMPTY(good_mutations) //good initialized mutations
    public DmList good_mutations;

    // tg: code/_globalvars/genetics.dm:5
    public virtual void InitGlobalgood_mutations()
    {
        good_mutations = Dm.list();
        Dm.append(ref gvars_datum_init_order, "good_mutations");
    }

    // tg: code/_globalvars/genetics.dm:6  GLOBAL_LIST_EMPTY(not_good_mutations) //neutral initialized mutations
    public DmList not_good_mutations;

    // tg: code/_globalvars/genetics.dm:6
    public virtual void InitGlobalnot_good_mutations()
    {
        not_good_mutations = Dm.list();
        Dm.append(ref gvars_datum_init_order, "not_good_mutations");
    }

    // tg: code/_globalvars/genetics.dm:7  GLOBAL_LIST_EMPTY(alias_mutations) //alias = type
    public DmList alias_mutations;

    // tg: code/_globalvars/genetics.dm:7
    public virtual void InitGlobalalias_mutations()
    {
        alias_mutations = Dm.list();
        Dm.append(ref gvars_datum_init_order, "alias_mutations");
    }
}
