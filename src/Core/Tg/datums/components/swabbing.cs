// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/swabbing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (cytology's swab and biopsy tools). The var block is generated (D111).

/*!

This component is used in vat growing to swab for microbiological samples which can then be mixed with reagents in a petridish to create a culture plate.

*/
// tg: code/datums/components/swabbing.dm:6
public partial class datum_component_swabbing : datum_component
{
    // tg: code/datums/components/swabbing.dm:20
    [DmParams("can_swab_objs", "can_swab_turfs", "can_swab_mobs", "update_icons", "update_overlays", "swab_time", "max_items")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object can_swab_objs = Dm.arg(__extra, 0) ?? TRUE;
        object can_swab_turfs = Dm.arg(__extra, 1) ?? TRUE;
        object can_swab_mobs = Dm.arg(__extra, 2) ?? FALSE;
        datum_callback update_icons = Dm.arg(__extra, 3) as datum_callback;
        datum_callback update_overlays = Dm.arg(__extra, 4) as datum_callback;
        // swab_time (1 SECONDS) and max_items (3), the last two parameters, are not read: tg's swab takes 3 seconds and one
        // sample whatever they are
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        RegisterSignal(parent, COMSIG_ITEM_PRE_ATTACK, nameof(try_to_swab));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(examine));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(handle_overlays));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_ICON, nameof(handle_icon));

        this.can_swab_objs = can_swab_objs;
        this.can_swab_turfs = can_swab_turfs;
        this.can_swab_mobs = can_swab_mobs;
        this.update_icons = update_icons;
        this.update_overlays = update_overlays;
        return null;
    }

    // tg: code/datums/components/swabbing.dm:35
    public override object Destroy(float? force = null)
    {
        foreach (object swabbed in Dm.each(swabbed_items))
            qdel(swabbed);
        update_icons = null;
        update_overlays = null;
        return base.Destroy(force);
    }

    ///Changes examine based on your sample
    // tg: code/datums/components/swabbing.dm:44
    [SignalHandler]
    public virtual void examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        if (Dm.truthy(LAZYLEN(swabbed_items)))
        {
            Dm.append(ref examine_list, span_nicegreen(I18n.T("There is a microbiological sample on []!", parent)));
            Dm.append(ref examine_list, Dm.text("[]\n", span_notice(I18n.T("You can see the following micro-organisms:"))));
            foreach (object i in Dm.each(swabbed_items))
            {
                datum_biological_sample samp = i as datum_biological_sample;
                foreach (object organism in Dm.each(samp.micro_organisms))
                {
                    datum_micro_organism MO = organism as datum_micro_organism;
                    Dm.append(ref examine_list, MO.get_details());
                }
            }
        }
    }

    ///Ran when you attack an object, tries to get a swab of the object. if a swabbable surface is found it will run behavior and hopefully
    // tg: code/datums/components/swabbing.dm:56
    [SignalHandler]
    public virtual object try_to_swab(datum source = null, atom target = null, mob user = null, DmList modifiers = null)
    {
        if (target is obj_structure_table)//help how do i do this less shitty
            return NONE; //idk bro pls send help

        if (target is obj_item_petri_dish)
        {
            if (!Dm.truthy(LAZYLEN(swabbed_items)))
                return NONE;
            obj_item_petri_dish dish = (obj_item_petri_dish)target;
            if (dish.sample != null)
                return null;

            datum_biological_sample deposited_sample = null;

            foreach (object i in Dm.each(swabbed_items)) //Typed in case there is a non sample on the swabbing tool because someone was fucking with swabbable element
            {
                if (!(i is datum_biological_sample))
                {
                    stack_trace("Non biological sample being swabbed, no bueno.", "code/datums/components/swabbing.dm", 76);
                    continue;
                }
                datum_biological_sample sample = (datum_biological_sample)i;
                //Collapse the samples into one sample; one gooey mess essentialy.
                if (deposited_sample == null)
                    deposited_sample = sample;
                else
                    deposited_sample.Merge(sample);
            }

            dish.deposit_sample(user, deposited_sample);
            LAZYCLEARLIST(swabbed_items);

            obj_item I = (obj_item)parent;
            I.update_appearance();

            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        if (!Dm.truthy(can_swab(target)))
            return NONE; //Just do the normal attack.


        object dot = COMPONENT_CANCEL_ATTACK_CHAIN; //Point of no return. No more attacking after this.

        if (Dm.truthy(LAZYLEN(swabbed_items)))
        {
            to_chat(user, span_warning(I18n.T("You cannot collect another sample on []!", parent)));
            return dot;
        }

        to_chat(user, span_notice(I18n.T("You start swabbing [] for samples!", target)));
        Waiting.Detach(async_try_to_swab(target, user)); // INVOKE_ASYNC(src, PROC_REF(async_try_to_swab), target, user)
        return dot;
    }

    // tg: code/datums/components/swabbing.dm:102
    public virtual async DmTask async_try_to_swab(atom target = null, mob user = null)
    {
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target))) // Start swabbing boi
            return;

        LAZYINITLIST(ref swabbed_items); //If it isn't initialized, initialize it. As we need to pass it by reference

        if (Dm.eq(target.SEND_SIGNAL(COMSIG_SWAB_FOR_SAMPLES, Dm.list(target, swabbed_items)), NONE)) //If we found something to swab now we let the swabbed thing handle what it would do, we just sit back and relax now.
        {
            to_chat(user, span_warning(I18n.T("You do not manage to find anything on []!", target)));
            return;
        }

        to_chat(user, span_nicegreen(I18n.T("You manage to collect a microbiological sample from []!", target)));

        obj_item parent_item = (obj_item)parent;
        parent_item.update_appearance();
    }

    ///Checks if the swabbing component can swab the specific object or nots
    // tg: code/datums/components/swabbing.dm:118
    public virtual object can_swab(atom target = null)
    {
        if (isobj(target))
            return can_swab_objs;
        if (isturf(target))
            return can_swab_turfs;
        if (ismob(target))
            return can_swab_mobs;
        return null;
    }

    ///Handle any special overlay cases on the item itself
    // tg: code/datums/components/swabbing.dm:127
    [SignalHandler]
    public virtual void handle_overlays(datum source = null, DmList overlays = null)
    {
        if (update_overlays != null)
            Waiting.NoSleep(update_overlays.Invoke(overlays, swabbed_items));
    }

    ///Handle any special icon cases on the item itself
    // tg: code/datums/components/swabbing.dm:132
    [SignalHandler]
    public virtual void handle_icon(datum source = null)
    {
        if (update_icons != null)
            Waiting.NoSleep(update_icons.Invoke(swabbed_items));
    }
}
