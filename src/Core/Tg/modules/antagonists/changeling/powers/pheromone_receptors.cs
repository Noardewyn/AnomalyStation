// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/pheromone_receptors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Pheromone Receptors and its pinpointer (the nearest changelings scented, weighted toward the
// closer, with the Pheromone Scent alert). The var blocks are generated (D111): the file-local CHANGELING_PHEROMONE_MIN_DISTANCE
// (10) and CHANGELING_PHEROMONE_PING_TIME (20) only fill the pinpointer's var block there.

// tg: code/modules/antagonists/changeling/powers/pheromone_receptors.dm:6
public partial class datum_action_changeling_pheromone_receptors : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/pheromone_receptors.dm:16
    public override async DmTask Remove(mob user = null)
    {
        if (Dm.truthy(receptors_active))
        {
            datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
            changeling.chem_recharge_slowdown -= 0.25f;
            ((mob_living)user).remove_status_effect(typeof(datum_status_effect_agent_pinpointer_changeling));
        }
        await base.Remove(user);
    }

    // tg: code/modules/antagonists/changeling/powers/pheromone_receptors.dm:23
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        await base.sting_action(user, target);
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        if (HAS_TRAIT(user, TRAIT_ANOSMIA)) //Anosmia quirk holders can't smell anything
        {
            to_chat(user, span_warning(I18n.T("We can't smell!")));
            return null;
        }
        if (!Dm.truthy(receptors_active))
        {
            to_chat(user, span_warning(I18n.T("We search for the scent of any nearby changelings.")));
            changeling.chem_recharge_slowdown += 0.25f;
            await user.apply_status_effect(typeof(datum_status_effect_agent_pinpointer_changeling));
        }
        else
        {
            to_chat(user, span_notice(I18n.T("We stop searching for now.")));
            changeling.chem_recharge_slowdown -= 0.25f;
            user.remove_status_effect(typeof(datum_status_effect_agent_pinpointer_changeling));
        }

        receptors_active = Dm.not(receptors_active);
        return null;
    }
}

//Modified IA pinpointer - Points to the NEAREST changeling, but will only get you within a few tiles of the target.
//You'll still have to rely on intuition and observation to make the identification.  Lings can 'hide' in public places.
// tg: code/modules/antagonists/changeling/powers/pheromone_receptors.dm:42
public partial class datum_status_effect_agent_pinpointer_changeling : datum_status_effect_agent_pinpointer
{
    // tg: code/modules/antagonists/changeling/powers/pheromone_receptors.dm:2  #define CHANGELING_PHEROMONE_MAX_DISTANCE 25 (file-local, #undef at :73)
    private const int CHANGELING_PHEROMONE_MAX_DISTANCE = 25; //They can smell your fear a mile away.  Well, 50 meters.

    // tg: code/modules/antagonists/changeling/powers/pheromone_receptors.dm:48
    public override void scan_for_target()
    {
        turf my_loc = get_turf(owner);

        DmList changelings = Dm.list();

        foreach (mob_living_carbon C in Dm.each<mob_living_carbon>(GLOB.alive_mob_list))
        {
            if (C != owner && C.mind != null)
            {
                datum_antagonist_changeling antag_datum = IS_CHANGELING(C) as datum_antagonist_changeling;
                if (antag_datum is datum_antagonist_changeling)
                {
                    object their_loc = get_turf(C);
                    object distance = get_dist_euclidean(my_loc, their_loc as atom);
                    if (Dm.lt(distance, CHANGELING_PHEROMONE_MAX_DISTANCE))
                        changelings[C] = Dm.pow(CHANGELING_PHEROMONE_MAX_DISTANCE, 2) - Dm.pow(distance, 2);
                }
            }
        }

        if (Dm.truthy(changelings.len))
            scan_target = pick_weight(changelings) as atom_movable; //Point at a 'random' changeling, biasing heavily towards closer ones.
        else
            scan_target = null;
    }
}
