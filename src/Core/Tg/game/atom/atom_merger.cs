// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/atom_merger.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the firelocks close as a row): the whole file. The var (mergers) is generated.

// tg: code/game/atom/atom_merger.dm:1
public partial class atom
{
    /// Gets a merger datum representing the connected blob of objects in the allowed_types argument
    // tg: code/game/atom/atom_merger.dm:6
    public virtual async DmTask<object> GetMergeGroup(object id = null, DmList allowed_types = null)
    {
        datum_merger candidate = null;
        if (mergers != null)
            candidate = mergers[id] as datum_merger;
        if (candidate == null)
        {
            await Dm.new_async<datum_merger>(id, allowed_types, this);
            candidate = mergers[id] as datum_merger;
        }
        return candidate;
    }
}
