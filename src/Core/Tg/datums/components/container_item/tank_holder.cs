// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/container_item/tank_holder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: Initialize (M4c slice 1: every gas tank adds the component at Initialize) and try_attach, putting the tank into a
// tank holder structure (gaps ST2). The var block is generated.

/// Tank holder item - Added to an object which can be added to a tank holder.
// tg: code/datums/components/container_item/tank_holder.dm:2
public partial class datum_component_container_item_tank_holder : datum_component_container_item
{
    // tg: code/datums/components/container_item/tank_holder.dm:6
    [DmParams("state", "density")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object state = Dm.arg(__extra, 0);
        object density = Dm.arg(__extra, 1) ?? TRUE;
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        tank_holder_icon_state = state;
        make_density = density;
        return await base.Initialize(Dm.forward(__extra, state, density));
    }

    // tg: code/datums/components/container_item/tank_holder.dm:14
    public override object try_attach(datum source = null, atom container = null, mob user = null)
    {
        obj_structure_tank_holder holder = container as obj_structure_tank_holder;
        if (holder == null)
            return FALSE;
        obj_item I = (obj_item)parent;
        if (Dm.truthy(holder.contents.len))
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("There's already something in [].", holder)));
            return TRUE;
        }
        if (user != null)
        {
            if (!Dm.truthy(Waiting.NoSleep(user.transferItemToLoc(I, holder))))
                return TRUE;
            to_chat(user, span_notice(I18n.T("You put [] into [].", I, holder)));
        }
        else
            Waiting.NoSleep(I.forceMove(holder));
        holder.tank = I;
        holder.set_density(make_density);
        holder.icon_state = Dm.str(tank_holder_icon_state);
        return TRUE;
    }
}
