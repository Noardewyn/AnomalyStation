// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/gastrolisis.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111). The snail species' parts (its eyes, tongue and shell) are
// ported with the snail species (D333's species leftovers); the disease reaches them as tg does.

// tg: code/datums/diseases/gastrolisis.dm:1
public partial class datum_disease_gastrolosis : datum_disease
{

    // tg: code/datums/diseases/gastrolisis.dm:16
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.truthy(is_species(affected_mob, typeof(datum_species_snail))))
        {
            await cure();
            return FALSE;
        }

        if (stage == 2)
        {
            if (SPT_PROB(1, seconds_per_tick))
                await affected_mob.emote("gag");
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                turf_open OT = get_turf(affected_mob) as turf_open;
                if (isopenturf(OT))
                    await OT.MakeSlippery(TURF_WET_LUBE, 40);
            }
        }
        else if (stage == 3)
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.emote("gag");
            if (SPT_PROB(2.5f, seconds_per_tick))
            {
                turf_open OT = get_turf(affected_mob) as turf_open;
                if (isopenturf(OT))
                    await OT.MakeSlippery(TURF_WET_LUBE, 100);
            }
        }
        else if (stage == 4)
        {
            obj_item_organ_eyes eyes = Dm.locate<obj_item_organ_eyes_snail>(affected_mob.organs);
            if (eyes == null && SPT_PROB(2.5f, seconds_per_tick))
            {
                obj_item_organ_eyes_snail new_eyes = Dm.@new<obj_item_organ_eyes_snail>();
                await new_eyes.Insert(affected_mob);
                affected_mob.visible_message(span_warning(I18n.T("[]'s eyes fall out, with snail eyes taking its place!", affected_mob)),
                span_userdanger(I18n.T("You scream in pain as your eyes are pushed out by your new snail eyes!")));
                await affected_mob.emote("scream");
                return dot;
            }

            obj_item shell = affected_mob.get_item_by_slot(ITEM_SLOT_BACK) as obj_item;
            if (!(shell is obj_item_storage_backpack_snail))
                shell = null;
            if (shell == null && SPT_PROB(2.5f, seconds_per_tick))
            {
                if (Dm.truthy(await affected_mob.dropItemToGround(affected_mob.get_item_by_slot(ITEM_SLOT_BACK) as obj_item)))
                {
                    await affected_mob.equip_to_slot_or_del(Dm.@new<obj_item_storage_backpack_snail>(affected_mob), ITEM_SLOT_BACK);
                    affected_mob.visible_message(span_warning(I18n.T("[] grows a grotesque shell on their back!", affected_mob)),
                    span_userdanger(I18n.T("You scream in pain as a shell pushes itself out from under your skin!")));
                    await affected_mob.emote("scream");
                    return dot;
                }
            }

            obj_item_organ_tongue tongue = Dm.locate<obj_item_organ_tongue_snail>(affected_mob.organs);
            if (tongue == null && SPT_PROB(2.5f, seconds_per_tick))
            {
                obj_item_organ_tongue_snail new_tongue = Dm.@new<obj_item_organ_tongue_snail>();
                await new_tongue.Insert(affected_mob);
                to_chat(affected_mob, span_userdanger(I18n.T("You feel your speech slow down...")));
                return dot;
            }

            if (shell != null && eyes != null && tongue != null && SPT_PROB(2.5f, seconds_per_tick))
            {
                affected_mob.set_species(typeof(datum_species_snail));
                // affected_mob.client?.give_award(/datum/award/achievement/jobs/snail, affected_mob): the achievements (D332)
                affected_mob.visible_message(span_warning(I18n.T("[] turns into a snail!", affected_mob)),
                span_boldnotice(I18n.T("You turned into a snail person! You feel an urge to cccrrraaawwwlll...")));
                await cure();
                return FALSE;
            }

            if (SPT_PROB(5, seconds_per_tick))
                await affected_mob.emote("gag");
            if (SPT_PROB(5, seconds_per_tick))
            {
                turf_open OT = get_turf(affected_mob) as turf_open;
                if (isopenturf(OT))
                    await OT.MakeSlippery(TURF_WET_LUBE, 100);
            }
        }
        return dot;
    }


    // tg: code/datums/diseases/gastrolisis.dm:84
    public override async DmTask cure(float? add_resistance = null)
    {
        add_resistance ??= TRUE;
        mob_living_carbon affected_mob__cure = affected_mob; // the parent's qdel clears affected_mob before tg's lines below read it (DV559)
        await base.cure(add_resistance);
        if (affected_mob__cure != null && !Dm.truthy(is_species(affected_mob__cure, typeof(datum_species_snail)))) //undo all the snail fuckening
        {
            mob_living_carbon_human H = (mob_living_carbon_human)affected_mob__cure;
            obj_item_organ_tongue tongue = Dm.locate<obj_item_organ_tongue_snail>(H.organs);
            if (tongue != null)
            {
                obj_item_organ_tongue new_tongue = Dm.@new(((datum_species)H.dna.species).mutanttongue) as obj_item_organ_tongue;
                await new_tongue.Insert(H);
            }
            obj_item_organ_eyes eyes = Dm.locate<obj_item_organ_eyes_snail>(H.organs);
            if (eyes != null)
            {
                obj_item_organ_eyes new_eyes = Dm.@new(((datum_species)H.dna.species).mutanteyes) as obj_item_organ_eyes;
                await new_eyes.Insert(H);
            }
            obj_item_storage_backpack bag = H.get_item_by_slot(ITEM_SLOT_BACK) as obj_item_storage_backpack;
            if (bag is obj_item_storage_backpack_snail)
            {
                await bag.emptyStorage();
                await H.temporarilyRemoveItemFromInventory(bag, TRUE);
                qdel(bag);
            }
        }
    }
}
