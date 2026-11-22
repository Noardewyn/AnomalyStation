// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/scanning_points.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the point scanning experiment, whose samples of each required type are worth
// that type's points until the required points are reached. The var block is generated (D111).

// tg: code/modules/experisci/experiment/types/scanning_points.dm:1
public partial class datum_experiment_scanning_points : datum_experiment_scanning
{
    // tg: code/modules/experisci/experiment/types/scanning_points.dm:9
    public override object is_complete()
    {
        return Dm.num(Dm.ge(points, required_points));
    }

    // tg: code/modules/experisci/experiment/types/scanning_points.dm:12
    public override async DmTask<object> check_progress()
    {
        DmList dot = null;
        dot = EXPERIMENT_PROG_INT(I18n.T("Scan samples of the following objects to accumulate enough points to complete this experiment."), points, required_points);
        object complete = is_complete();
        object point_val_cache = Dm.list();
        foreach (object a_type in Dm.each(required_atoms))
        {
            Type req_atom = a_type as Type;
            if (!Dm.truthy(((DmList)point_val_cache)[Dm.text("[]", required_atoms[a_type])]))
                ((DmList)point_val_cache)[Dm.text("[]", required_atoms[a_type])] = Dm.list();
            Dm.append_at((DmList)point_val_cache, Dm.text("[]", required_atoms[a_type]), Dm.initial(req_atom, (atom p) => p.name));
        }

        foreach (object point_amt in Dm.each(point_val_cache))
        {
            DmList types = ((DmList)point_val_cache)[point_amt] as DmList;
            object types_joined = types.Join(", ");
            Dm.append(ref dot, EXPERIMENT_PROG_DETAIL(I18n.T("[] point\\s: []", text2num(point_amt), types_joined), complete));
        }
        return dot;
    }

    // tg: code/modules/experisci/experiment/types/scanning_points.dm:27
    public override async DmTask<object> experiment_requirements(datum_component_experiment_handler experiment_handler = null, atom target = null)
    {
        object destructive = Dm.bitand(traits, EXPERIMENT_TRAIT_DESTRUCTIVE);
        foreach (object req_atom in Dm.each(required_atoms))
        {
            if (!istype(target, (Type)req_atom))
                continue;

            // Try to select a required atom that this scanned atom would contribute towards
            object selected = null;
            if (Dm.truthy(destructive) && Dm.@in(req_atom, scanned))
                selected = req_atom;
            else if (!Dm.truthy(destructive) && !Dm.@in(WEAKREF(target), scanned[req_atom]))
                selected = req_atom;

            // Run any additonal checks if necessary
            if (Dm.truthy(selected) && Dm.truthy(await final_contributing_index_checks(experiment_handler, target, selected)))
                return selected;
        }
        return null;
    }

    // tg: code/modules/experisci/experiment/types/scanning_points.dm:44
    public override object do_after_experiment(atom target = null, object typepath = null)
    {
        object dot = base.do_after_experiment(target, typepath);
        points = min(Dm.num(required_points), points + Dm.num(required_atoms[typepath]));
        return dot;
    }
}
