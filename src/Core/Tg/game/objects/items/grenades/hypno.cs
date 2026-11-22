// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/grenades/hypno.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): the hypnotic flashbang. Its fantasy bonuses are live-partial rows, as the grenade's.

// tg: code/game/objects/items/grenades/hypno.dm:1
public partial class obj_item_grenade_hypnotic : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/hypno.dm:16
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        await update_mob();
        turf flashbang_turf = get_turf(this) as turf;
        if (flashbang_turf == null)
            return dot;
        do_sparks(Dm.num(rand(5, 9)), FALSE, this);
        playsound(flashbang_turf, Dm.resource("sound/effects/screech.ogg"), 100, TRUE, 8, 0.9f);
        Dm.@new<obj_effect_dummy_lighting_obj>(flashbang_turf, flashbang_range + 2, 4, LIGHT_COLOR_PURPLE, 2);
        foreach (mob_living living_mob in Dm.each<mob_living>(get_hearers_in_view(flashbang_range, flashbang_turf)))
            await bang(get_turf(living_mob) as turf, living_mob);
        qdel(this);
        return dot;
    }

    // tg: code/game/objects/items/grenades/hypno.dm:32
    public virtual async DmTask bang(turf turf = null, mob_living living_mob = null)
    {
        if (living_mob.stat == DEAD) //They're dead!
            return;
        float distance = Dm.num(get_dist(get_turf(this), turf));

        //Bang
        bool hypno_sound = false;

        //Hearing protection check
        if (Dm.num(living_mob.get_ear_protection()) < 0)
            hypno_sound = true;

        if (distance == 0)
        {
            await living_mob.Paralyze(1 * SECONDS);
            await living_mob.Knockdown(10 * SECONDS);
            to_chat(living_mob, span_hypnophrase(I18n.T("The sound echoes in your brain...")));
            await living_mob.adjust_hallucinations(150 * SECONDS);
        }
        else
        {
            if (distance <= 1)
            {
                await living_mob.Paralyze(0.5f * SECONDS);
                await living_mob.Knockdown(3 * SECONDS);
            }
            if (hypno_sound)
            {
                to_chat(living_mob, span_hypnophrase(I18n.T("The sound echoes in your brain...")));
                await living_mob.adjust_hallucinations(150 * SECONDS);
            }
        }

        //Flash
        if (!Dm.truthy(await living_mob.flash_act(affect_silicon: TRUE)))
            return;
        await living_mob.Paralyze(max(1 * SECONDS / (distance != 0 ? distance : 1), 0.5f * SECONDS));
        await living_mob.Knockdown(max(10 * SECONDS / (distance != 0 ? distance : 1), 4 * SECONDS));
        if (Dm.truthy(living_mob.hypnosis_vulnerable())) //The sound causes the necessary conditions unless the target has mindshield or hearing protection
        {
            await living_mob.apply_status_effect(typeof(datum_status_effect_trance), 10 * SECONDS, TRUE);
            return;
        }
        to_chat(living_mob, span_hypnophrase(I18n.T("The light is so pretty...")));
        await living_mob.adjust_drowsiness_up_to(20 * SECONDS, 40 * SECONDS);
        await living_mob.adjust_confusion_up_to(10 * SECONDS, 20 * SECONDS);
        await living_mob.adjust_dizzy_up_to(20 * SECONDS, 40 * SECONDS);
    }
}
