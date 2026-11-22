// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/plumbing/reaction_chamber.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var blocks are generated (D111). The chamber's handle_reactions reaches the
// holder's deferred reaction lookup (chemistry is not ported: reactions.cs).

// tg: code/datums/components/plumbing/reaction_chamber.dm:1
public partial class datum_component_plumbing_reaction_chamber : datum_component_plumbing
{
    // tg: code/datums/components/plumbing/reaction_chamber.dm:5
    [DmParams("ducting_layer")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!istype(parent, typeof(obj_machinery_plumbing_reaction_chamber)))
            return COMPONENT_INCOMPATIBLE;
        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/plumbing/reaction_chamber.dm:10
    public override object can_give(object amount = null, object reagent = null, datum_ductnet net = null)
    {
        object dot = FALSE;

        obj_machinery_plumbing_reaction_chamber reaction_chamber = (obj_machinery_plumbing_reaction_chamber)parent;

        //cannot give when we outselves are requesting or reacting the reagents
        if (Dm.num(amount) <= 0 || !Dm.truthy(reagents.total_volume) || !Dm.truthy(reaction_chamber.emptying) || Dm.truthy(reagents.is_reacting))
            return dot;

        //check to see if we can give catalysts only if they are in excess
        DmList catalysts = reaction_chamber.catalysts;
        foreach (datum_reagent chemical in Dm.each_cast<datum_reagent>(reagents.reagent_list))
        {
            if (Dm.truthy(reagent) && !Dm.eq(chemical.type, reagent))
                continue;

            //we have the exact amounts so no excess to spare
            if (Dm.num(chemical.volume) <= Dm.num(Dm.truthy(catalysts[chemical.type]) ? catalysts[chemical.type] : 0))
            {
                if (Dm.truthy(reagent))
                    break;
                else
                    continue;
            }

            //atleast 1 reagent to give so take whatever
            return TRUE;
        }
        return dot;
    }

    // tg: code/datums/components/plumbing/reaction_chamber.dm:35
    public override async DmTask send_request(object dir = null)
    {
        obj_machinery_plumbing_reaction_chamber chamber = (obj_machinery_plumbing_reaction_chamber)parent;

        if (Dm.truthy(chamber.emptying))
            return;

        //take in reagents
        float present_amount;
        float diff;
        DmList required_reagents = Dm.bitor(chamber.catalysts, chamber.required_reagents) as DmList;
        foreach (object required_reagent in Dm.each(required_reagents))
        {
            //find how much amount is already present if at all and get the reagent reference
            present_amount = 0;
            foreach (datum_reagent present_reagent in Dm.each_cast<datum_reagent>(reagents.reagent_list))
            {
                if (Dm.eq(required_reagent, present_reagent.type))
                {
                    present_amount = Dm.num(present_reagent.volume);
                    break;
                }
            }

            //compute how much more is needed
            diff = min(Dm.num(required_reagents[required_reagent]) - present_amount, MACHINE_REAGENT_TRANSFER);
            if (diff >= CHEMICAL_QUANTISATION_LEVEL) // the closest we can ask for so values like 0.9999 become 1
            {
                await process_request(diff, required_reagent, dir);
                if (!Dm.truthy(chamber.catalysts[required_reagent])) //only block if not a catalyst as they can come in whenever they are available
                    return;
            }
        }

        Dm.mask(ref reagents.flags, Dm.bitnot(NO_REACT));
        await reagents.handle_reactions();

        chamber.emptying = TRUE; //If we move this up, it'll instantly get turned off since any reaction always sets the reagent_total to zero. Other option is make the reaction update
        //everything for every chemical removed, wich isn't a good option either.
        chamber.on_reagent_change(reagents as datum_reagents_plumbing_reaction_chamber); //We need to check it now, because some reactions leave nothing left.
        if (Dm.truthy(chamber.emptying)) //if we are still emptying then keep checking for reagents until we are emptied out
            chamber.RegisterSignal(reagents, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(obj_machinery_plumbing_reaction_chamber.on_reagent_change));
    }
}

// tg: code/datums/components/plumbing/reaction_chamber.dm:69
public partial class datum_component_plumbing_buffered : datum_component_plumbing
{
    // tg: code/datums/components/plumbing/reaction_chamber.dm:75
    [DmParams("ducting_layer")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = await base.Initialize(__extra);
        buffer = Dm.@new<datum_reagents>(Dm.named("maximum", 50), Dm.named("new_flags", NO_REACT));
        buffer.my_atom = parent as atom;
        return dot;
    }

    // tg: code/datums/components/plumbing/reaction_chamber.dm:80
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref buffer);
        return base.Destroy(force);
    }

    // tg: code/datums/components/plumbing/reaction_chamber.dm:84
    public override object recipient_reagents_holder()
    {
        return buffer;
    }

    // tg: code/datums/components/plumbing/reaction_chamber.dm:87
    public override async DmTask send_request(object dir = null)
    {
        await process_request(reagent: request, dir: dir);
    }
}
