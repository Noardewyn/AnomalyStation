// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/death.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the carbon's death (M2 P2, scope.md 2e "carbon": a human dies of its damage); M6 slice 5 (gibbing): gib,
// get_gibs_type, spill_organs and spread_bodyparts (:19-97). The suicide procs (set_suicide, can_suicide) are M10.

// tg: code/modules/mob/living/carbon/death.dm:1
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/death.dm:1
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = null;
        if (stat == DEAD)
            return dot;

        losebreath = 0;
        breathing_loop.stop(); //This would've happened eventually but it's nice to make it stop immediatelly in this case
        await ((datum_reagents)reagents).end_metabolization(this);

        dot = await base.death(gibbed, __extra);

        if (!Dm.truthy(gibbed) && !QDELING(this)) //double check they didn't start getting deleted in ..()
            await attach_rot();

        foreach (object T in Dm.each(get_traumas()))
        {
            datum_brain_trauma BT = T as datum_brain_trauma;
            BT.on_death();
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/death.dm:19
    public override async DmTask gib(float? drop_bitflags = null)
    {
        drop_bitflags ??= NONE;
        if (Dm.truthy(Dm.bitand(drop_bitflags, DROP_ITEMS)))
        {
            foreach (obj_item W in Dm.each<obj_item>(this))
            {
                if (Dm.truthy(await dropItemToGround(W)))
                {
                    if (prob(50))
                        await step(W, Dm.num(pick(GLOB.alldirs)));
                }
            }
        }
        atom Tsec = drop_location() as atom;
        foreach (mob M in Dm.each<mob>(this))
        {
            await M.forceMove(Tsec);
            visible_message(span_danger(I18n.T("[] bursts out of []!", M, this)));
        }
        await base.gib(drop_bitflags);
    }

    // tg: code/modules/mob/living/carbon/death.dm:31
    public override object get_gibs_type(float? drop_bitflags = null)
    {
        drop_bitflags ??= NONE;
        obj_item_bodypart chest = (get_bodypart(BODY_ZONE_CHEST) ?? get_bodypart()) as obj_item_bodypart;
        if (chest == null) // what
            return base.get_gibs_type(drop_bitflags);

        if (Dm.truthy(Dm.bitand(chest.bodytype, BODYTYPE_ROBOTIC)))
            return typeof(obj_effect_gibspawner_robot);

        if (Dm.truthy(Dm.bitand(chest.bodytype, BODYTYPE_LARVA_PLACEHOLDER)))
        {
            if (Dm.truthy(Dm.bitand(drop_bitflags, DROP_BODYPARTS)))
                return typeof(obj_effect_gibspawner_larva);
            return typeof(obj_effect_gibspawner_larva_bodypartless);
        }

        if (Dm.truthy(Dm.bitand(chest.bodytype, BODYTYPE_ALIEN)))
        {
            if (Dm.truthy(Dm.bitand(drop_bitflags, DROP_BODYPARTS)))
                return typeof(obj_effect_gibspawner_xeno);
            return typeof(obj_effect_gibspawner_xeno_bodypartless);
        }

        if (Dm.truthy(Dm.bitand(drop_bitflags, DROP_BODYPARTS)))
            return typeof(obj_effect_gibspawner_human);
        return typeof(obj_effect_gibspawner_human_bodypartless);
    }

    // tg: code/modules/mob/living/carbon/death.dm:53
    public override async DmTask spill_organs(float? drop_bitflags = null)
    {
        drop_bitflags ??= NONE;
        atom Tsec = drop_location() as atom;

        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(organs))
        {
            if (Dm.truthy(Dm.bitand(drop_bitflags, DROP_BRAIN)) && organ is obj_item_organ_brain)
            {
                if (Dm.truthy(Dm.bitand(drop_bitflags, DROP_BODYPARTS)) && !Dm.eq(check_zone(organ.zone), BODY_ZONE_CHEST)) // chests can't drop
                    continue; // the head will drop, so the brain should stay inside

                await organ.Remove(this);
                await organ.forceMove(Tsec);
                await organ.throw_at(get_edge_target_turf(this, Dm.num(pick(GLOB.alldirs))) as atom, rand(1, 3), 5);
                continue;
            }

            if (Dm.truthy(Dm.bitand(drop_bitflags, DROP_ORGANS)) && !(organ is obj_item_organ_brain))
            {
                if (Dm.truthy(Dm.bitand(drop_bitflags, DROP_BODYPARTS)) && !Dm.eq(check_zone(organ.zone), BODY_ZONE_CHEST))
                    continue; // only chest & groin organs will be ejected

                await organ.Remove(this);
                await organ.forceMove(Tsec);
                await organ.throw_at(get_edge_target_turf(this, Dm.num(pick(GLOB.alldirs))) as atom, rand(1, 3), 5);
                continue;
            }

            qdel(organ);
        }
    }

    // tg: code/modules/mob/living/carbon/death.dm:79
    public override async DmTask spread_bodyparts(float? drop_bitflags = null, float? gibbed = null)
    {
        drop_bitflags ??= NONE;
        gibbed ??= FALSE;
        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            if (Dm.eq(part.body_zone, BODY_ZONE_CHEST))
                continue; // never drop this
            if (!Dm.truthy(Dm.bitand(drop_bitflags, DROP_BRAIN)) && Dm.eq(part.body_zone, BODY_ZONE_HEAD))
                continue; // don't drop head if we aren't dropping a brain

            DmList leftover_organs = Dm.list();
            foreach (obj_item_organ leftover in Dm.each<obj_item_organ>(part))
                Dm.append(ref leftover_organs, leftover);

            await part.drop_limb(special: gibbed, dismembered: Dm.not(gibbed));
            await part.throw_at(get_edge_target_turf(this, Dm.num(pick(GLOB.alldirs))) as atom, rand(1, 3), 5);
            // any organs that weren't throw out already about need to follow the bodypart out
            foreach (obj_item_organ leftover in Dm.each_cast<obj_item_organ>(leftover_organs))
            {
                // depending on whether gibbed flag was set changes how the organs are removed,
                // so just let's be... very careful here and double check everything
                if (leftover.owner == this)
                    await leftover.Remove(this, gibbed);
                if (leftover.loc != part)
                    await leftover.bodypart_insert(part);
            }
        }
    }
}
