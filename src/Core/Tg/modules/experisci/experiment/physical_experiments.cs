// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/physical_experiments.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the extreme cooking experiment (an emitter fired at a tracked meat wall, which
// falls into gibs and a steak) and the arcade playtest (won at a tracked arcade cabinet; the arcade machines are not ported yet, so
// COMSIG_ARCADE_VICTORY is never sent: not-translated.md section 195). The var blocks are generated (D111).

// tg: code/modules/experisci/experiment/physical_experiments.dm:1
public partial class datum_experiment_physical_meat_wall_explosion : datum_experiment_physical
{
    // tg: code/modules/experisci/experiment/physical_experiments.dm:5
    public override async DmTask<object> register_events()
    {
        if (!iswallturf(currently_scanned_atom))
        {
            await linked_experiment_handler.announce_message(I18n.T("Incorrect object for experiment."));
            return FALSE;
        }

        if (!Dm.truthy(await currently_scanned_atom.has_material_type(typeof(datum_material_meat))))
        {
            await linked_experiment_handler.announce_message(I18n.T("Object is not made out of the correct materials."));
            return FALSE;
        }

        RegisterSignal(currently_scanned_atom, COMSIG_ATOM_BULLET_ACT, nameof(check_experiment));
        await linked_experiment_handler.announce_message(I18n.T("Experiment ready to start."));
        return TRUE;
    }

    // tg: code/modules/experisci/experiment/physical_experiments.dm:18
    public override void unregister_events()
    {
        UnregisterSignal(currently_scanned_atom, COMSIG_ATOM_BULLET_ACT);
    }

    // tg: code/modules/experisci/experiment/physical_experiments.dm:21
    public override async DmTask<object> check_progress()
    {
        DmList dot = null;
        Dm.append(ref dot, EXPERIMENT_PROG_BOOL(I18n.T("Fire an emitter at a tracked meat wall"), is_complete()));
        return dot;
    }

    // tg: code/modules/experisci/experiment/physical_experiments.dm:24
    [SignalHandler]
    public virtual void check_experiment(datum source = null, obj_projectile proj = null)
    {
        if (proj is obj_projectile_beam_emitter)
        {
            UnregisterSignal(currently_scanned_atom, COMSIG_ATOM_BULLET_ACT);
            Waiting.NoSleep(finish_experiment(linked_experiment_handler));
        }
    }

    // tg: code/modules/experisci/experiment/physical_experiments.dm:30
    public override async DmTask finish_experiment(datum_component_experiment_handler experiment_handler = null, datum_techweb linked_web_override = null)
    {
        await base.finish_experiment(experiment_handler, linked_web_override);
        Dm.@new<obj_effect_gibspawner_generic>(currently_scanned_atom);
        turf meat_wall = currently_scanned_atom as turf;
        turf new_turf = await meat_wall.ScrapeAway() as turf;
        Dm.@new<obj_effect_gibspawner_generic>(new_turf);
        Dm.@new<obj_item_food_meat_steak_plain>(new_turf);
    }
}

// tg: code/modules/experisci/experiment/physical_experiments.dm:38
public partial class datum_experiment_physical_arcade_winner : datum_experiment_physical
{
    // tg: code/modules/experisci/experiment/physical_experiments.dm:42
    public override async DmTask<object> register_events()
    {
        if (!(currently_scanned_atom is obj_machinery_computer_arcade))
        {
            await linked_experiment_handler.announce_message(I18n.T("Incorrect object for experiment."));
            return FALSE;
        }

        RegisterSignal(currently_scanned_atom, COMSIG_ARCADE_VICTORY, nameof(win_arcade));
        await linked_experiment_handler.announce_message(I18n.T("Experiment ready to start."));
        return TRUE;
    }

    // tg: code/modules/experisci/experiment/physical_experiments.dm:51
    public override void unregister_events()
    {
        UnregisterSignal(currently_scanned_atom, COMSIG_ARCADE_VICTORY);
    }

    // tg: code/modules/experisci/experiment/physical_experiments.dm:54
    public override async DmTask<object> check_progress()
    {
        DmList dot = null;
        Dm.append(ref dot, EXPERIMENT_PROG_BOOL(I18n.T("Win an arcade game at a tracked arcade cabinet."), is_complete()));
        return dot;
    }

    // tg: code/modules/experisci/experiment/physical_experiments.dm:57
    [SignalHandler]
    public virtual void win_arcade(datum source = null)
    {
        UnregisterSignal(currently_scanned_atom, COMSIG_ARCADE_VICTORY);
        Waiting.NoSleep(finish_experiment(linked_experiment_handler));
    }
}
