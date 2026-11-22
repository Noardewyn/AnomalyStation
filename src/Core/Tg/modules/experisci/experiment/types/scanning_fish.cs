// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/scanning_fish.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the fish scans, whose scanned species a techweb's fish experiments share, each
// adding the next when it completes. The fish themselves, the tech fishing rod, the fish analyzer and the fishing portal generator the
// scans unlock settings of wait for fishing (not-translated.md section 195). The var blocks are generated (D111).

// tg: code/modules/experisci/experiment/types/scanning_fish.dm:2
public partial class datum_controller_global_vars
{
    ///a superlist containing typecaches shared between the several fish scanning experiments for each techweb.
    // tg: code/modules/experisci/experiment/types/scanning_fish.dm:2  GLOBAL_LIST_EMPTY(scanned_fish_by_techweb)
    public DmList scanned_fish_by_techweb;

    // tg: code/modules/experisci/experiment/types/scanning_fish.dm:2  (GLOBAL_MANAGED(scanned_fish_by_techweb, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalscanned_fish_by_techweb()
    {
        scanned_fish_by_techweb = Dm.list();
        Dm.append(ref gvars_datum_init_order, "scanned_fish_by_techweb");
    }
}

/**
 * A special scanning experiment that unlocks further settings for the fishing portal generator.
 * Mainly as an inventive solution to many a fish source being limited to maps that have it,
 * and to make the fishing portal generator a bit than just gubby and goldfish.
 */
// tg: code/modules/experisci/experiment/types/scanning_fish.dm:9
public partial class datum_experiment_scanning_fish : datum_experiment_scanning
{
    /**
     * We make sure the scanned list is shared between all fish scanning experiments for this techweb,
     * since this is about scanning each species, and having to redo it for each species is a hassle.
     */
    // tg: code/modules/experisci/experiment/types/scanning_fish.dm:27
    [DmParams("techweb")]
    public override async DmTask New(params object[] __extra)
    {
        datum_techweb techweb = Dm.arg<datum_techweb>(__extra, 0);
        await base.New(Dm.forward(__extra, techweb));
        if (techweb == null)
            return;
        object techweb_ref = REF(techweb);
        DmList scanned_fish = GLOB.scanned_fish_by_techweb[techweb_ref] as DmList;
        if (scanned_fish == null)
        {
            scanned_fish = Dm.list();
            GLOB.scanned_fish_by_techweb[techweb_ref] = scanned_fish;
        }
        foreach (object atom_type in Dm.each(required_atoms))
        {
            // LAZYINITLIST(scanned_fish[atom_type]) (__HELPERS/_lists.dm:52, L ||= list()), expanded: its argument is a list element
            if (!Dm.truthy(scanned_fish[atom_type]))
                scanned_fish[atom_type] = Dm.list();
        }
        scanned = scanned_fish;
    }

    /**
     * Registers a couple signals to review the fish scanned so far.
     * It'd be an hassle not having any way (beside memory) to know which fish species have been scanned already otherwise.
     */
    // tg: code/modules/experisci/experiment/types/scanning_fish.dm:44
    public override void on_selected(datum_component_experiment_handler experiment_handler = null)
    {
        RegisterSignal(experiment_handler.parent, COMSIG_ATOM_EXAMINE, nameof(on_handler_examine));
        RegisterSignal(experiment_handler.parent, COMSIG_ATOM_EXAMINE_MORE, nameof(on_handler_examine_more));
    }

    // tg: code/modules/experisci/experiment/types/scanning_fish.dm:48
    public override void on_unselected(datum_component_experiment_handler experiment_handler = null)
    {
        UnregisterSignal(experiment_handler.parent, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_ATOM_EXAMINE_MORE));
    }

    // tg: code/modules/experisci/experiment/types/scanning_fish.dm:51
    [SignalHandler]
    public virtual void on_handler_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_notice(I18n.T("Examine again to review all the species of fish scanned so far.")));
    }

    // tg: code/modules/experisci/experiment/types/scanning_fish.dm:55
    [SignalHandler]
    public virtual void on_handler_examine_more(datum source = null, mob user = null, DmList examine_list = null)
    {
        string message = span_notice(I18n.T("Fish species scanned hitherto, if any:"));
        message += "<span class='info ml-1'>";
        foreach (object atom_type in Dm.each(required_atoms))
        {
            foreach (Type fish_path in Dm.each_cast<Type>(scanned[atom_type]))
                message += Dm.text("\n[]", Dm.initial(fish_path, (obj_item_fish p) => p.name));
        }
        message += "</span>";
        Dm.append(ref examine_list, message);
    }

    ///Only scannable fish will contribute towards the experiment.
    // tg: code/modules/experisci/experiment/types/scanning_fish.dm:66
    public override async DmTask<object> final_contributing_index_checks(datum_component_experiment_handler experiment_handler = null, atom target = null, object typepath = null)
    {
        return Dm.bitand(((obj_item_fish)target).fish_flags, FISH_FLAG_EXPERIMENT_SCANNABLE);
    }

    /**
     * After a fish scanning experiment is done, more may be unlocked. If so, add them to the techweb
     * and automatically link the handler to the next experiment in the list as a bit of qol.
     */
    // tg: code/modules/experisci/experiment/types/scanning_fish.dm:73
    public override async DmTask finish_experiment(datum_component_experiment_handler experiment_handler = null, datum_techweb linked_web_override = null)
    {
        await base.finish_experiment(experiment_handler, linked_web_override);
        if (Dm.truthy(next_experiments))
        {
            experiment_handler.linked_web.add_experiments(next_experiments);
            datum_experiment next_in_line = Dm.locate_type(next_experiments[1] as Type, experiment_handler.linked_web.available_experiments) as datum_experiment;
            experiment_handler.link_experiment(next_in_line);
        }
    }
}

// tg: code/modules/experisci/experiment/types/scanning_fish.dm:96
public partial class datum_experiment_scanning_fish_holographic : datum_experiment_scanning_fish
{
    ///holo fishes are normally unscannable, but this is an experiment for them, so we don't care for the experisci_scannable variable.
    // tg: code/modules/experisci/experiment/types/scanning_fish.dm:107
    public override async DmTask<object> final_contributing_index_checks(datum_component_experiment_handler experiment_handler = null, atom target = null, object typepath = null)
    {
        return TRUE;
    }
}
