// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/ai_control_examine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C5).

/**
 * ai control examine; which gives the pawn of the parent the noticable organs depending on AI status!
 *
 * Used for monkeys to have PRIMAL eyes
 */
// tg: code/datums/elements/ai_control_examine.dm:7
public partial class datum_element_ai_control_examine : datum_element
{
    // tg: code/datums/elements/ai_control_examine.dm:13
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        DmList noticable_organ_examines = Dm.arg<DmList>(__extra, 0) ?? Dm.list(Dm.kv(ORGAN_SLOT_BRAIN, span_deadsay("doesn't appear to be themself.")));
        object dot = await base.Attach(target, __extra);

        if (target is not datum_ai_controller target_controller)
            return ELEMENT_INCOMPATIBLE;
        this.noticable_organ_examines = noticable_organ_examines;
        RegisterSignal(target_controller, COMSIG_AI_CONTROLLER_POSSESSED_PAWN, nameof(on_ai_controller_possessed_pawn));
        RegisterSignal(target_controller, COMSIG_AI_CONTROLLER_UNPOSSESSED_PAWN, nameof(on_ai_controller_unpossessed_pawn));
        return dot;
    }

    // tg: code/datums/elements/ai_control_examine.dm:23
    public override object Detach(datum source = null, params object[] __extra)
    {
        datum_ai_controller target_controller = source as datum_ai_controller;
        object dot = base.Detach(source, __extra);
        UnregisterSignal(target_controller, Dm.list(COMSIG_AI_CONTROLLER_POSSESSED_PAWN, COMSIG_AI_CONTROLLER_UNPOSSESSED_PAWN));
        if (target_controller.pawn != null && ishuman(target_controller.pawn))
            UnregisterSignal(target_controller.pawn, COMSIG_ORGAN_IMPLANTED);
        return dot;
    }

    /// Signal when the ai controller possesses a pawn
    // tg: code/datums/elements/ai_control_examine.dm:30
    public virtual void on_ai_controller_possessed_pawn(datum_ai_controller source_controller = null)
    {
        if (!ishuman(source_controller.pawn))
            return; //not supported
        mob_living_carbon_human human_pawn = source_controller.pawn as mob_living_carbon_human;
        //make current organs noticable
        foreach (object organ_slot_key in Dm.each_cast<object>(noticable_organ_examines))
        {
            obj_item_organ found = human_pawn.get_organ_slot(organ_slot_key) as obj_item_organ;
            if (found == null)
                continue;
            make_organ_noticable(organ_slot_key, found, human_pawn);
        }
        //listen for future insertions (the element removes itself on removal, so we can ignore organ removal)
        RegisterSignal(human_pawn, COMSIG_ORGAN_IMPLANTED, nameof(on_organ_implanted));
    }

    // tg: code/datums/elements/ai_control_examine.dm:45
    public virtual void on_organ_implanted(obj_item_organ possibly_noticable = null, mob_living_carbon receiver = null)
    {
        if (Dm.truthy(noticable_organ_examines[possibly_noticable.slot]))
            make_organ_noticable(possibly_noticable.slot, possibly_noticable);
    }

    // tg: code/datums/elements/ai_control_examine.dm:50
    public virtual void make_organ_noticable(object organ_slot = null, obj_item_organ noticable_organ = null, mob_living_carbon_human human_pawn = null)
    {
        object examine_text = noticable_organ_examines[organ_slot];
        object body_zone = !Dm.eq(organ_slot, ORGAN_SLOT_BRAIN) ? noticable_organ.zone : null;
        Waiting.NoSleep(noticable_organ.AddElement(typeof(datum_element_noticable_organ_ai_control), examine_text, body_zone));
    }

    /// Signal when the ai controller stops possessing a pawn, either it's deleted or it got moved to another pawn for some reason
    // tg: code/datums/elements/ai_control_examine.dm:56
    public virtual void on_ai_controller_unpossessed_pawn(datum_ai_controller source_controller = null)
    {
        if (!ishuman(source_controller.pawn))
            return;

        mob_living_carbon_human human_pawn = source_controller.pawn as mob_living_carbon_human;

        foreach (object organ_slot_key in Dm.each_cast<object>(noticable_organ_examines))
        {
            obj_item_organ found = human_pawn.get_organ_slot(organ_slot_key) as obj_item_organ;
            if (found == null)
                continue;
            make_organ_uninteresting(organ_slot_key, found, human_pawn);
        }

        UnregisterSignal(human_pawn, COMSIG_ORGAN_IMPLANTED);
    }

    // tg: code/datums/elements/ai_control_examine.dm:71
    public virtual void make_organ_uninteresting(object organ_slot = null, obj_item_organ noticable_organ = null, mob_living_carbon_human human_pawn = null)
    {
        object examine_text = noticable_organ_examines[organ_slot];
        object body_zone = !Dm.eq(organ_slot, ORGAN_SLOT_BRAIN) ? noticable_organ.zone : null;
        noticable_organ.RemoveElement(typeof(datum_element_noticable_organ_ai_control), examine_text, body_zone);
    }
}
