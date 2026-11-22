// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/adult/caste/drone.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the drone and its evolution to praetorian. The var blocks are generated (D111).

// tg: code/modules/mob/living/carbon/alien/adult/caste/drone.dm:1
public partial class mob_living_carbon_alien_adult_drone : mob_living_carbon_alien_adult
{
    // tg: code/modules/mob/living/carbon/alien/adult/caste/drone.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(Dm.@new<datum_action_cooldown_alien_evolve_to_praetorian>(this).Grant(this)); // GRANT_ACTION
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/mob/living/carbon/alien/adult/caste/drone.dm:26
public partial class datum_action_cooldown_alien_evolve_to_praetorian : datum_action_cooldown_alien
{
    // tg: code/modules/mob/living/carbon/alien/adult/caste/drone.dm:32
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;

        if (!isturf(owner.loc))
            return FALSE;

        if (get_alien_type(typeof(mob_living_carbon_alien_adult_royal)) != null)
            return FALSE;

        mob evolver = owner; // tg types it /mob/living/carbon/alien/adult/royal, but a drone runs it (DM checks no type)
        obj_item_organ_alien_hivenode node = evolver.get_organ_by_type(typeof(obj_item_organ_alien_hivenode)) as obj_item_organ_alien_hivenode;
        // Players are Murphy's Law. We may not expect
        // there to ever be a living xeno with no hivenode,
        // but they _WILL_ make it happen.
        if (node == null || Dm.truthy(node.recent_queen_death))
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/caste/drone.dm:53
    public override async DmTask<object> Activate(atom target = null)
    {
        mob_living_carbon_alien_adult evolver = owner as mob_living_carbon_alien_adult;
        mob_living_carbon_alien_adult_royal_praetorian new_xeno = Dm.@new<mob_living_carbon_alien_adult_royal_praetorian>(owner.loc);
        await evolver.alien_evolve(new_xeno);
        return TRUE;
    }
}
