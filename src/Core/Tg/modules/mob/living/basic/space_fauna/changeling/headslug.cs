// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/changeling/headslug.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the headslug the changeling's Last Resort leaves, which plants its egg in a corpse and dies,
// its AI controller (vars only) and the beakless headslug. The var blocks are generated (D111).

/**
 * ## Headslugs
 *
 * Player-controlled slugs that arise from a changeling ability in order to live on in an extremely limited capacity until they can find a suitable corpse to inhabit.
 */
// tg: code/modules/mob/living/basic/space_fauna/changeling/headslug.dm:6
public partial class mob_living_basic_headslug : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/changeling/headslug.dm:36
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        RegisterSignal(this, COMSIG_HOSTILE_POST_ATTACKINGTARGET, nameof(check_and_implant));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/changeling/headslug.dm:41
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(this, COMSIG_HOSTILE_POST_ATTACKINGTARGET);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/space_fauna/changeling/headslug.dm:45
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (stat != DEAD)
        {
            if (client == null)
                Dm.append(ref dot, span_notice(I18n.T("It appears to be moving around listlessly.")));
            else
                Dm.append(ref dot, span_warning(I18n.T("It's moving around intelligently!")));
        }
        if (Dm.truthy(egg_lain))
            Dm.append(ref dot, span_notice(I18n.T("Its reproductive equipment appears to have withered.")));
        return dot;
    }

    /// Signal Handler proc that runs on every attack and checks to see if this is a valid target for implantation. If so, it implants the egg and starts the countdown to death.
    // tg: code/modules/mob/living/basic/space_fauna/changeling/headslug.dm:56
    [SignalHandler]
    public virtual void check_and_implant(mob_living_basic attacker = null, atom target = null)
    {
        if (Dm.truthy(egg_lain) || !iscarbon(target) || HAS_TRAIT(target, TRAIT_LESSER_HUMANOID))
            return;

        mob_living_carbon victim = target as mob_living_carbon;
        if (victim.stat != DEAD)
            return;
        if (HAS_TRAIT(victim, TRAIT_XENO_HOST))
        {
            target.balloon_alert(this, I18n.T("already pregnant!")); // Maybe the worst balloon alert in the codebase
            return;
        }

        if (!Dm.truthy(Waiting.NoSleep(infect(victim))))
        {
            target.balloon_alert(this, I18n.T("failed to implant egg!"));
            stack_trace(Dm.text("[] in [] failed to implant egg in [], despite all checks suggesting it should have worked!", key, this, victim), "code/modules/mob/living/basic/space_fauna/changeling/headslug.dm", 71);
            return;
        }

        egg_lain = TRUE;
        to_chat(this, span_userdanger(I18n.T("With our egg laid, our death approaches rapidly...")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(death)), 10 * SECONDS, file: "code/modules/mob/living/basic/space_fauna/changeling/headslug.dm", line: 76);
    }

    /// Simply infects the target corpse with our changeling eggs. This shouldn't fail, because all checks should have been done in check_and_implant()
    /// Just to be super-duper safe to the player, we do return TRUE if all goes well and read that value in check_and_implant() to be nice to the player.
    // tg: code/modules/mob/living/basic/space_fauna/changeling/headslug.dm:80
    public virtual async DmTask<object> infect(mob_living_carbon victim = null)
    {
        obj_item_organ_body_egg_changeling_egg egg = Dm.@new<obj_item_organ_body_egg_changeling_egg>(victim);

        egg.origin = mind;

        foreach (obj_item_organ target in Dm.each<obj_item_organ>(this))
            await target.forceMove(egg);

        visible_message(
            span_warning(I18n.T("[] plants something in []'s flesh!", this, victim)),
            span_danger(I18n.T("We inject our egg into []'s body!", victim))
        );

        return TRUE;
    }
}
