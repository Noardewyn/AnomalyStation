// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob_spawn/ghost_roles/venus_human_trap.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4b (D327): the whole file, the venus human trap's ghost role (it opens when the flower bud bears fruit; the trap takes the
// bud's vine mutations). The var block is generated (D111).

/// Handles logic for ghost spawning code, visible object in game is handled by /obj/structure/alien/resin/flower_bud
// tg: code/modules/mob_spawn/ghost_roles/venus_human_trap.dm:2
public partial class obj_effect_mob_spawn_ghost_role_venus_human_trap : obj_effect_mob_spawn_ghost_role
{
    // tg: code/modules/mob_spawn/ghost_roles/venus_human_trap.dm:20
    public override object Destroy(float? force = null)
    {
        if (flower_bud != null) // anti harddel checks
        {
            if (!QDELETED(flower_bud))
                qdel(flower_bud);
            flower_bud = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/mob_spawn/ghost_roles/venus_human_trap.dm:27
    [DmParams("venus_trap")]
    public override void equip(mob_living spawned_mob = null)
    {
        mob_living_basic_venus_human_trap venus_trap = spawned_mob as mob_living_basic_venus_human_trap;
        if (venus_trap == null || flower_bud == null)
            return;

        foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(flower_bud.mutations))
            Waiting.NoSleep(mutation.equip_venus_trap(venus_trap));
    }

    // tg: code/modules/mob_spawn/ghost_roles/venus_human_trap.dm:34
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        if (flower_bud != null && Dm.truthy(length(flower_bud.mutations)))
        {
            DmList flavour_mutation_list = Dm.list();
            Dm.append(ref flavour_mutation_list, I18n.T(" You have the following vine mutations:"));
            foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(flower_bud.mutations))
                Dm.append(ref flavour_mutation_list, span_notice(mutation.venus_flavor_text));
            flavour_text += flavour_mutation_list.Join("\n");
        }
        base.special(spawned_mob, mob_possessor, apply_prefs);
        Waiting.NoSleep(spawned_mob.mind.add_antag_datum(typeof(datum_antagonist_venus_human_trap)));
    }

    /// Called when the attached flower bud has borne fruit (ie. is ready)
    // tg: code/modules/mob_spawn/ghost_roles/venus_human_trap.dm:45
    public virtual void bear_fruit()
    {
        ready = TRUE;
        notify_ghosts(
            I18n.T("[] has borne fruit!", this),
            source: this,
            header: "Venus Human Trap",
            click_interact: TRUE,
            ignore_key: POLL_IGNORE_VENUSHUMANTRAP);
    }

    // tg: code/modules/mob_spawn/ghost_roles/venus_human_trap.dm:55
    public override object allow_spawn(mob user = null, object silent = null)
    {
        silent ??= FALSE;
        object dot = base.allow_spawn(user, silent);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!Dm.truthy(ready))
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("\\The [] has not borne fruit yet!", this)));
            return FALSE;
        }
        return TRUE;
    }
}
