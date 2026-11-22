// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/loomable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST4 (D333): the file whole, for the loom: an item with it (the raw cotton, durathread and wool bundles) used on a secured
// loom spins four of a stack at a time, a second each, into cloth (or what it makes), with its examine line. The var block is
// generated (D111).

/// Element that makes items turn into other items when you use them on a loom (or any other thing really if you change the var)
// tg: code/datums/elements/loomable.dm:2
public partial class datum_element_loomable : datum_element
{
    // tg: code/datums/elements/loomable.dm:18
    [DmParams("target", "resulting_atom", "required_amount", "loom_type", "process_completion_verb", "target_needs_anchoring", "loom_time")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object resulting_atom = Dm.arg(__extra, 0);
        object required_amount = Dm.arg(__extra, 1);
        object loom_type = Dm.arg(__extra, 2);
        object process_completion_verb = Dm.arg(__extra, 3);
        object target_needs_anchoring = Dm.arg(__extra, 4);
        object loom_time = Dm.arg(__extra, 5);
        resulting_atom ??= typeof(obj_item_stack_sheet_cloth);
        required_amount ??= 4;
        loom_type ??= typeof(obj_structure_loom);
        process_completion_verb ??= "spun";
        target_needs_anchoring ??= TRUE;
        loom_time ??= 1 * SECONDS;
        object dot = await base.Attach(target, Dm.forward(__extra, resulting_atom, required_amount, loom_type, process_completion_verb, target_needs_anchoring, loom_time));
        //currently this element only works for items as we need to call /obj/item/attack_atom()
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;
        this.resulting_atom = resulting_atom as Type;
        this.required_amount = required_amount;
        this.loom_type = loom_type as Type;
        this.process_completion_verb = process_completion_verb;
        this.target_needs_anchoring = target_needs_anchoring;
        this.loom_time = loom_time;
        RegisterSignal(target, COMSIG_ITEM_ATTACK_ATOM, nameof(try_and_loom_me));
        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        return dot;
    }

    // tg: code/datums/elements/loomable.dm:40
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_ITEM_ATTACK_ATOM, COMSIG_ATOM_EXAMINE));
        return dot;
    }

    /// Adds an examine blurb to the description of any item that can be loomed
    // tg: code/datums/elements/loomable.dm:45
    [SignalHandler]
    public virtual void on_examine(obj_item source = null, mob examiner = null, DmList examine_list = null)
    {
        Dm.append(examine_list, span_notice(I18n.T("You could probably process [] at \\a <b>[]</b>.", source, Dm.initial(loom_type, (atom p) => p.name))));
    }

    /// Checks if the thing we clicked on can be used as a loom, and if we can actually loom the source at present (an example being does the stack have enough in it (if its a stack))
    // tg: code/datums/elements/loomable.dm:51
    [SignalHandler]
    public virtual object try_and_loom_me(obj_item source = null, atom target = null, mob_living user = null)
    {
        if (!istype(target, loom_type))
            return null;

        if (target is atom_movable movable_target)
        {
            if (Dm.truthy(target_needs_anchoring) && !Dm.truthy(movable_target.anchored))
            {
                user.balloon_alert(user, I18n.T("[] must be secured!", movable_target));
                return null;
            }
        }

        if ((Dm.num(required_amount) > 1) && source is obj_item_stack source_stack)
        {
            if (Dm.num(source_stack.amount) < Dm.num(required_amount))
            {
                user.balloon_alert(user, I18n.T("need [] of []!", required_amount, source));
                return null;
            }
        }

        Waiting.Detach(loom_me(source, user, target));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// If a do_after of the specified loom_time passes, will create a new one of resulting_atom and either delete the item, or .use the required amount if its a stack
    // tg: code/datums/elements/loomable.dm:73
    public virtual async DmTask loom_me(obj_item source = null, mob_living user = null, atom target = null)
    {
        //this allows us to count the amount of times it has successfully used the stack's required amount
        float spawning_amount = 0;
        if (source is obj_item_stack stack_we_use)
        {
            while (Dm.num(stack_we_use.amount) >= Dm.num(required_amount))
            {
                if (!Dm.truthy(await do_after(user, loom_time, target)))
                    break;

                if (!Dm.truthy(await stack_we_use.use(required_amount)))
                {
                    if (spawning_amount == 0)
                        user.balloon_alert(user, I18n.T("need [] of []!", required_amount, source));
                    break;
                }

                spawning_amount++;
            }
        }
        else
        {
            if (!Dm.truthy(await do_after(user, loom_time, target)))
            {
                user.balloon_alert(user, I18n.T("interrupted!"));
                return;
            }

            qdel(source);
            spawning_amount++;
        }

        if (spawning_amount == 0)
            return;

        atom new_thing = null;
        if (ispath(resulting_atom, typeof(obj_item_stack)))
        {
            float max_amount = Dm.num(Dm.initial(resulting_atom, (obj_item_stack p) => p.max_amount));
            while (spawning_amount > 0)
            {
                new_thing = Dm.@new(resulting_atom, target.drop_location(), Math.Min(spawning_amount, max_amount)) as atom;
                spawning_amount -= max_amount;
            }
        }
        else
        {
            for (float repeated = 1; repeated <= spawning_amount; repeated++)
                new_thing = Dm.@new(resulting_atom, target.drop_location()) as atom;
        }
        user.balloon_alert_to_viewers(I18n.T("[] []", process_completion_verb, new_thing));
    }
}
