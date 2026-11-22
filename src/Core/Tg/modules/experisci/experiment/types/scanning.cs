// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/scanning.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the scanning experiment, which counts the scanned samples of each required type
// (by weak reference, by count for destructive scans, by type for typecache ones). The var block is generated (D111).

/**
 * # Scanning Experiment
 *
 * This is the base implementation of scanning experiments.
 *
 * This class should be subclassed for producing actual experiments. The
 * procs should be extended where necessary.
 */
// tg: code/modules/experisci/experiment/types/scanning.dm:9
public partial class datum_experiment_scanning : datum_experiment
{
    /**
     * Initializes the scanned atoms lists
     *
     * Initializes the internal scanned atoms list to keep track of which atoms have already been scanned
     */
    // tg: code/modules/experisci/experiment/types/scanning.dm:28
    [DmParams("techweb")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        foreach (object req_atom in Dm.each(required_atoms))
            scanned[req_atom] = (Dm.truthy(Dm.bitand(traits, EXPERIMENT_TRAIT_DESTRUCTIVE)) && !Dm.truthy(Dm.bitand(traits, EXPERIMENT_TRAIT_TYPECACHE))) ? (object)0 : Dm.list();
    }

    /**
     * Checks if the scanning experiment is complete
     *
     * Returns TRUE/FALSE as to if the necessary number of atoms have been scanned.
     */
    // tg: code/modules/experisci/experiment/types/scanning.dm:38
    public override object is_complete()
    {
        object dot = null;
        dot = TRUE;
        object destructive = Dm.bitand(traits, EXPERIMENT_TRAIT_DESTRUCTIVE);
        object typecache = Dm.bitand(traits, EXPERIMENT_TRAIT_TYPECACHE);
        foreach (object req_atom in Dm.each(required_atoms))
        {
            DmList seen = scanned[req_atom] as DmList;
            ///typecache experiments work all the same whether it's destructive or not
            if (Dm.truthy(typecache) && Dm.eq(length(seen), required_atoms[req_atom]))
                continue;
            if (Dm.truthy(destructive) && (!Dm.@in(req_atom, scanned) || !Dm.eq(scanned[req_atom], required_atoms[req_atom])))
                return FALSE;
            if (!Dm.truthy(destructive) && (!Dm.truthy(seen) || !Dm.eq(seen.len, required_atoms[req_atom])))
                return FALSE;
        }
        return dot;
    }

    /**
     * Gets the number of atoms that have been scanned and the goal
     *
     * This proc returns a string describing the number of atoms that
     * have been scanned as well as the target number of atoms.
     */
    // tg: code/modules/experisci/experiment/types/scanning.dm:58
    public override async DmTask<object> check_progress()
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (object atom_type in Dm.each(required_atoms))
        {
            Type required_atom = atom_type as Type;
            DmList seen_instances = scanned[required_atom] as DmList;
            Dm.append(ref dot, await serialize_progress_stage(required_atom, seen_instances));
        }
        return dot;
    }

    /**
     * Serializes a progress stage into a list to be sent to the UI
     *
     * Arguments:
     * * target - The targeted atom for this progress stage
     * * seen_instances - The number of instances seen of this atom
     */
    // tg: code/modules/experisci/experiment/types/scanning.dm:72
    public virtual async DmTask<object> serialize_progress_stage(Type target = null, DmList seen_instances = null)
    {
        object scanned_total = (Dm.truthy(Dm.bitand(traits, EXPERIMENT_TRAIT_DESTRUCTIVE)) && !Dm.truthy(Dm.bitand(traits, EXPERIMENT_TRAIT_TYPECACHE))) ? scanned[target] : seen_instances.len;
        object message = (scan_message is var or__1 && Dm.truthy(or__1) ? or__1 : I18n.T("Scan samples of \\a []", Dm.initial(target, (atom p) => p.name)));
        return EXPERIMENT_PROG_INT(message, scanned_total, required_atoms[target]);
    }

    /**
     * Attempts to scan an atom towards the experiment's goal
     *
     * This proc attempts to scan an atom towards the experiment's goal,
     * and returns TRUE/FALSE based on success.
     * Arguments:
     * * target - The atom to attempt to scan
     */
    // tg: code/modules/experisci/experiment/types/scanning.dm:85
    [DmParams("experiment_handler", "target")]
    public override async DmTask<object> perform_experiment_actions(datum_component_experiment_handler experiment_handler = null, params object[] __extra)
    {
        atom target = Dm.arg<atom>(__extra, 0);
        object contributing_index_value = await experiment_requirements(experiment_handler, target);
        if (contributing_index_value != null)
        {
            if (Dm.truthy(Dm.bitand(traits, EXPERIMENT_TRAIT_TYPECACHE)))
                ((DmList)scanned[contributing_index_value])[target.type] = TRUE;
            else
                Dm.append_at(scanned, contributing_index_value, Dm.truthy(Dm.bitand(traits, EXPERIMENT_TRAIT_DESTRUCTIVE)) ? (object)1 : WEAKREF(target));
            if (Dm.truthy(Dm.bitand(traits, EXPERIMENT_TRAIT_DESTRUCTIVE)) && !isliving(target))//only qdel things when destructive scanning and they're not living (living things get gibbed)
                qdel(target);
            do_after_experiment(target, contributing_index_value);
            return TRUE;
        }
        return null;
    }

    // tg: code/modules/experisci/experiment/types/scanning.dm:97
    [DmParams("experiment_handler", "target")]
    public override async DmTask<object> actionable(params object[] __extra)
    {
        datum_component_experiment_handler experiment_handler = Dm.arg<datum_component_experiment_handler>(__extra, 0);
        atom target = Dm.arg<atom>(__extra, 1);
        return (await base.actionable(Dm.forward(__extra, experiment_handler, target)) is var and__1 && !Dm.truthy(and__1) ? and__1 : Dm.num(await experiment_requirements(experiment_handler, target) != null));
    }

    /**
     * Attempts to get the typepath for an atom that would contribute to the experiment
     *
     * This proc checks the required atoms for a typepath that this target atom can contribute to
     * and if found returns that typepath, otherwise returns null
     * Arguments:
     * * target - The atom to attempt to scan
     */
    // tg: code/modules/experisci/experiment/types/scanning.dm:108
    public virtual async DmTask<object> experiment_requirements(datum_component_experiment_handler experiment_handler = null, atom target = null)
    {
        object destructive = Dm.bitand(traits, EXPERIMENT_TRAIT_DESTRUCTIVE);
        foreach (object req_atom in Dm.each(required_atoms))
        {
            if (!istype(target, (Type)req_atom))
                continue;
            // Try to select a required atom that this scanned atom would contribute towards
            object selected = null;
            DmList seen = scanned[req_atom] as DmList;
            if (Dm.truthy(destructive) && Dm.@in(req_atom, scanned) && Dm.lt(scanned[req_atom], required_atoms[req_atom]))
                selected = req_atom;
            else if (!Dm.truthy(destructive) && Dm.lt(seen.len, required_atoms[req_atom]) && !Dm.@in(WEAKREF(target), seen))
                selected = req_atom;
            // Run any additonal checks if necessary
            if (Dm.truthy(selected) && Dm.truthy(await final_contributing_index_checks(experiment_handler, target, selected)))
                return selected;
        }
        return null;
    }

    /**
     * Performs any additional checks against the atom being considered for selection as a contributing index
     *
     * This proc is intended to be used to add additional functionality to contributing index checks
     * without having to duplicate the iteration structure of experiment_requirements()
     * Arguments:
     * * target - The atom being scanned
     * * typepath - The typepath (selected index) of the target atom
     */
    // tg: code/modules/experisci/experiment/types/scanning.dm:133
    public virtual async DmTask<object> final_contributing_index_checks(datum_component_experiment_handler experiment_handler = null, atom target = null, object typepath = null)
    {
        return TRUE;
    }

    /**
     * Performs actions following a successful experiment action
     *
     * This proc is intended to be used to add additional functionality to follow experiment
     * actions without having to change the perform_experiment_actions proc to get access to the
     * selected typepath index
     * Arguments:
     * * target - The atom being scanned
     * * typepath - The typepath (selected index) of the target atom
     */
    // tg: code/modules/experisci/experiment/types/scanning.dm:146
    public virtual object do_after_experiment(atom target = null, object typepath = null)
    {
        return TRUE;
    }
}
