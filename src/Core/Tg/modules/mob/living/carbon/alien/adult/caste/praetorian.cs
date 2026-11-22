// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/adult/caste/praetorian.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the praetorian and its evolution to queen (its tail sweep is the xeno repulse spell, live). The
// var blocks are generated (D111).

// tg: code/modules/mob/living/carbon/alien/adult/caste/praetorian.dm:1
public partial class mob_living_carbon_alien_adult_royal_praetorian : mob_living_carbon_alien_adult_royal
{
    // tg: code/modules/mob/living/carbon/alien/adult/caste/praetorian.dm:27  var/static/list/innate_actions (a proc-level static, rules 10.6)
    private static ref DmList Initialize__innate_actions => ref TgState.Current.mob_living_carbon_alien_adult_royal_praetorian__Initialize__innate_actions;

    // tg: code/modules/mob/living/carbon/alien/adult/caste/praetorian.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        real_name = name;

        grant_actions_by_list(Initialize__innate_actions);

        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/mob/living/carbon/alien/adult/caste/praetorian.dm:36
public partial class datum_action_cooldown_alien_evolve_to_queen : datum_action_cooldown_alien
{
    // tg: code/modules/mob/living/carbon/alien/adult/caste/praetorian.dm:42
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;

        if (!isturf(owner.loc))
            return FALSE;

        if (get_alien_type(typeof(mob_living_carbon_alien_adult_royal_queen)) != null)
            return FALSE;

        mob_living_carbon_alien_adult_royal evolver = owner as mob_living_carbon_alien_adult_royal;
        obj_item_organ_alien_hivenode node = evolver.get_organ_by_type(typeof(obj_item_organ_alien_hivenode)) as obj_item_organ_alien_hivenode;
        if (node == null || Dm.truthy(node.recent_queen_death))
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/caste/praetorian.dm:60
    public override async DmTask<object> Activate(atom target = null)
    {
        mob_living_carbon_alien_adult_royal evolver = owner as mob_living_carbon_alien_adult_royal;
        mob_living_carbon_alien_adult_royal_queen new_queen = Dm.@new<mob_living_carbon_alien_adult_royal_queen>(owner.loc);
        await evolver.alien_evolve(new_queen);
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/carbon/alien/adult/caste/praetorian.dm:27
    internal DmList mob_living_carbon_alien_adult_royal_praetorian__Initialize__innate_actions = Dm.list(
        typeof(datum_action_cooldown_alien_evolve_to_queen),
        typeof(datum_action_cooldown_spell_aoe_repulse_xeno));
}
