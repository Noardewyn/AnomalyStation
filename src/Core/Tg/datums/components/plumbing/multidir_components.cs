// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/plumbing/multidir_components.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var blocks are generated (D111).

///When you have a component accepting multiple connections
// tg: code/datums/components/plumbing/multidir_components.dm:2
public partial class datum_component_plumbing_multidirectional : datum_component_plumbing
{
    /**
     * Returns the direction in which this component is connected to an ductnet without the rotation
     * Arguments
     *
     * * datum/ductnet/net - the net we are checking for an connection to
    */
    // tg: code/datums/components/plumbing/multidir_components.dm:12
    public virtual object get_connection(datum_ductnet net = null)
    {
        atom_movable parent_movable = (atom_movable)parent;
        foreach (object A in Dm.each(ducts))
        {
            if (Dm.eq(ducts[A], net))
                return turn(Dm.num(text2num(A)), Dm.num(dir2angle(parent_movable.dir)) - 180);
        }
        return null;
    }
}

///Splits reagent amounts along 3 directions
// tg: code/datums/components/plumbing/multidir_components.dm:21
public partial class datum_component_plumbing_multidirectional_splitter : datum_component_plumbing_multidirectional
{
    // tg: code/datums/components/plumbing/multidir_components.dm:21
    [DmParams("ducting_layer")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!istype(parent, typeof(obj_machinery_plumbing_splitter)))
            return COMPONENT_INCOMPATIBLE;
        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/plumbing/multidir_components.dm:26
    public override async DmTask transfer_to(datum_component_plumbing target = null, object amount = null, object reagent = null, datum_ductnet net = null, float? round_robin = null)
    {
        round_robin ??= TRUE;
        obj_machinery_plumbing_splitter S = (obj_machinery_plumbing_splitter)parent;
        float limit = INFINITY;
        switch (Dm.num(get_connection(net)))
        {
            case SOUTH:
                limit = S.transfer_straight;
                break;
            case EAST:
                limit = S.transfer_left;
                break;
            case WEST:
                limit = S.transfer_right;
                break;
        }
        amount = min(Dm.num(amount), limit, reagents.total_volume);
        await S.use_energy(S.active_power_usage);
        await base.transfer_to(target, amount, reagent, net, round_robin);
    }
}

///Splits reagents along 3 directions
// tg: code/datums/components/plumbing/multidir_components.dm:41
public partial class datum_component_plumbing_multidirectional_filter : datum_component_plumbing_multidirectional
{
    // tg: code/datums/components/plumbing/multidir_components.dm:41
    [DmParams("ducting_layer")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!istype(parent, typeof(obj_machinery_plumbing_filter)))
            return COMPONENT_INCOMPATIBLE;
        return await base.Initialize(__extra);
    }

    /**
     * Returns if an reagent can be supplied in an specific direction
     * Arguments
     *
     * * dir - the direction to check
     * * datum/reagent/reagent - the reagent we are checking for
    */
    // tg: code/datums/components/plumbing/multidir_components.dm:54
    public virtual object can_give_in_direction(object dir = null, object reagent = null)
    {
        obj_machinery_plumbing_filter F = (obj_machinery_plumbing_filter)parent;
        switch (Dm.num(dir))
        {
            case SOUTH: //straight
                return !Dm.truthy(F.left.Find(reagent)) && !Dm.truthy(F.right.Find(reagent)) ? TRUE : FALSE;
            case EAST: //left
                return F.left.Find(reagent);
            case WEST: //right
                return F.right.Find(reagent);
        }
        return null;
    }

    // tg: code/datums/components/plumbing/multidir_components.dm:66
    public override object can_give(object amount = null, object reagent = null, datum_ductnet net = null)
    {
        return (Dm.truthy(reagent) ? Dm.truthy(can_give_in_direction(get_connection(net), reagent)) : true) && Dm.truthy(base.can_give(amount, reagent, net)) ? TRUE : FALSE;
    }

    // tg: code/datums/components/plumbing/multidir_components.dm:69
    public override async DmTask transfer_to(datum_component_plumbing target = null, object amount = null, object reagent = null, datum_ductnet net = null, float? round_robin = null)
    {
        round_robin ??= TRUE;
        obj_machinery_plumbing_filter F = (obj_machinery_plumbing_filter)parent;
        if (Dm.truthy(reagent))
            await reagents.trans_to(target.parent, Dm.num(amount), target_id: reagent, methods: Dm.truthy(round_robin) ? LINEAR : NONE);
        else
        {
            object direction = get_connection(net);
            DmList rejected_reagents = Dm.list();
            foreach (datum_reagent R in Dm.each_cast<datum_reagent>(reagents.reagent_list))
            {
                if (!Dm.truthy(can_give_in_direction(direction, R.type)))
                    Dm.append(ref rejected_reagents, R.type);
            }
            await reagents.trans_to(target.parent, Dm.num(amount), remove_blacklisted: rejected_reagents, methods: Dm.truthy(round_robin) ? LINEAR : NONE);
        }
        await F.use_energy(F.active_power_usage);
    }
}
