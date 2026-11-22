// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/container_item/container_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4c slice 1: every gas tank adds the tank holder component at Initialize). The var block is generated.

/// Container item, an item which can be stored by specialized containers.
// tg: code/datums/components/container_item/container_item.dm:2
public partial class datum_component_container_item : datum_component
{
    // tg: code/datums/components/container_item/container_item.dm:4
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = null;
        dot = await base.Initialize(__extra);
        RegisterSignal(parent, COMSIG_CONTAINER_TRY_ATTACH, nameof(try_attach));
        return dot;
    }

    /// Called when parent is added to the container.
    // tg: code/datums/components/container_item/container_item.dm:9
    public virtual object try_attach(datum source = null, atom container = null, mob user = null)
    {
        return FALSE;
    }
}
