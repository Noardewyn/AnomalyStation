// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/infuser_entry.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): the infuser's entries, one of each made at the world's start. The var blocks are generated (D111).

/// A list of all infuser entries
// tg: code/game/machinery/dna_infuser/infuser_entry.dm:2 (interleaved types, rules 1.9)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/game/machinery/dna_infuser/infuser_entry.dm:2  GLOBAL_LIST_INIT(infuser_entries, prepare_infuser_entries())
    public DmList infuser_entries;

    // tg: code/game/machinery/dna_infuser/infuser_entry.dm:2  (GLOBAL_MANAGED(infuser_entries, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalinfuser_entries()
    {
        infuser_entries = prepare_infuser_entries();
        Dm.append(ref gvars_datum_init_order, "infuser_entries");
    }
}

// tg: code/game/machinery/dna_infuser/infuser_entry.dm:5 (global procs; interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    /// Global proc that sets up each [/datum/infuser_entry] sub-type as singleton instances in a list, and returns it.
    // tg: code/game/machinery/dna_infuser/infuser_entry.dm:5
    public static DmList prepare_infuser_entries()
    {
        DmList entries = Dm.list();
        foreach (object entry_type in Dm.each(subtypesof(typeof(datum_infuser_entry))))
        {
            datum_infuser_entry entry = Dm.@new(entry_type as Type) as datum_infuser_entry;
            entries[entry_type] = entry;
        }
        return entries;
    }
}

// tg: code/game/machinery/dna_infuser/infuser_entry.dm:13
public partial class datum_infuser_entry : datum
{
    ///Returns a list of organs that can be infused into the target human. Useful for custom behavior for certain entries
    // tg: code/game/machinery/dna_infuser/infuser_entry.dm:53
    public virtual DmList get_output_organs(mob_living_carbon_human target = null, atom_movable infused_from = null)
    {
        return output_organs.Copy();
    }
}
