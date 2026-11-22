// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/paper_wizard/paper_abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_abilities.dm:1
public partial class datum_action_cooldown_spell_conjure_wizard_summon_minions : datum_action_cooldown_spell_conjure
{
    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_abilities.dm:22
    public override async DmTask<object> can_cast_spell(float? feedback = null)
    {
        feedback ??= TRUE;
        object dot = await base.can_cast_spell(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (summoned_minions >= max_minions)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_abilities.dm:30
    public override async DmTask post_summon(atom summoned_object = null, atom cast_on = null)
    {
        RegisterSignals(summoned_object, Dm.list(COMSIG_QDELETING, COMSIG_LIVING_DEATH), nameof(lost_minion));
        summoned_minions++;
    }

    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_abilities.dm:35
    [SignalHandler]
    public virtual void lost_minion(mob source = null)
    {
        UnregisterSignal(source, Dm.list(COMSIG_QDELETING, COMSIG_LIVING_DEATH));
        summoned_minions--;
    }
}

// tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_abilities.dm:41
public partial class datum_action_cooldown_spell_pointed_wizard_mimic : datum_action_cooldown_spell_pointed
{
    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_abilities.dm:54
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = await base.Grant(granted_to, __extra);
        if (owner == null)
            return dot;
        RegisterSignal(owner, COMSIG_LIVING_HEALTH_UPDATE, nameof(delete_clones));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_abilities.dm:60
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        return isliving(cast_on) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_abilities.dm:65
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = await base.cast(cast_on);
        DmList directions = GLOB.cardinals.Copy();
        for (int i = 1; i <= 3; i++)
        {
            mob_living_basic_paper_wizard_copy copy = Dm.@new<mob_living_basic_paper_wizard_copy>(get_step(cast_on, Dm.num(pick_n_take(directions))));
            await invocation__proc(copy);
            RegisterSignals(copy, Dm.list(COMSIG_QDELETING, COMSIG_LIVING_DEATH), nameof(lost_minion));
            Dm.append(ref copies, copy);
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), copy), clone_lifespan, file: "code/modules/mob/living/basic/space_fauna/paper_wizard/paper_abilities.dm", line: 73); // QDEL_IN
        }
        await owner.forceMove(get_step(cast_on, Dm.num(pick_n_take(directions))) as atom);
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_abilities.dm:76
    [SignalHandler]
    public virtual void lost_minion(mob_living_basic_paper_wizard_copy source = null)
    {
        Dm.remove(ref copies, source);
        UnregisterSignal(source, Dm.list(COMSIG_QDELETING, COMSIG_LIVING_DEATH));
    }

    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_abilities.dm:82
    [SignalHandler]
    public virtual void delete_clones(mob source = null)
    {
        QDEL_LIST(copies);
    }

    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_abilities.dm:87
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(copies);
        return base.Destroy(force);
    }
}
