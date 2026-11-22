// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/plumbing/ductnet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The ducts' network (modules/plumbing/ducts.dm). The whole file; the plumbing machines that add_plumber and
// remove_plumber reach are not ported yet (their component, /datum/component/plumbing, is inert: plumbing, M8).

///We handle the unity part of plumbing. We track who is connected to who.
// tg: code/datums/components/plumbing/ductnet.dm:2
public partial class datum_ductnet : datum
{
    // tg: code/datums/components/plumbing/ductnet.dm:10
    [DmParams("parent")]
    public override async DmTask New(params object[] __extra)
    {
        obj_machinery_duct parent = Dm.arg<obj_machinery_duct>(__extra, 0);
        ducts = parent != null ? Dm.list(parent) : Dm.list();
        suppliers = Dm.list();
        demanders = Dm.list();
        await base.New(__extra);
    }

    // tg: code/datums/components/plumbing/ductnet.dm:16
    public override object Destroy(float? force = null)
    {
        ducts.Cut();
        foreach (datum_component_plumbing plumbing in Dm.each_cast<datum_component_plumbing>(Dm.add(suppliers, demanders)))
            remove_plumber(plumbing);
        suppliers.Cut();
        demanders.Cut();
        return base.Destroy(force);
    }

    ///add a plumbing object to either demanders or suppliers
    // tg: code/datums/components/plumbing/ductnet.dm:25
    public virtual object add_plumber(datum_component_plumbing plumbing = null, object dir = null)
    {
        string dirtext = num2text(Dm.num(dir));
        if (Dm.eq(plumbing.ducts[dirtext], this))
            return FALSE;
        plumbing.ducts[dirtext] = this;
        if (Dm.truthy(Dm.bitand(dir, plumbing.supply_connects)))
            Dm.append(ref suppliers, plumbing);
        if (Dm.truthy(Dm.bitand(dir, plumbing.demand_connects)))
            Dm.append(ref demanders, plumbing);
        return TRUE;
    }

    ///remove a plumber. we don't delete ourselves because ductnets don't persist through plumbing objects.
    // tg: code/datums/components/plumbing/ductnet.dm:37
    public virtual object remove_plumber(datum_component_plumbing plumbing = null)
    {
        foreach (object dir__it in Dm.each(plumbing.ducts))
        {
            object dir = dir__it;
            if (Dm.eq(plumbing.ducts[dir], this))
            {
                Dm.remove(ref plumbing.ducts, dir);
                dir = text2num(dir);
                if (Dm.truthy(Dm.bitand(dir, plumbing.supply_connects)))
                    Dm.remove(ref suppliers, plumbing);
                if (Dm.truthy(Dm.bitand(dir, plumbing.demand_connects)))
                    Dm.remove(ref demanders, plumbing);
            }
        }

        //return if this net has no ducts like when 2 machines are connected
        return ducts.len == 0 ? TRUE : FALSE;
    }
}
