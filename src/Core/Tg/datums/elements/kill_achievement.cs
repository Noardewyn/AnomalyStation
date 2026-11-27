// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/kill_achievement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated. The achievements are not ported, so their subsystem reads as disabled and
// on_death takes tg's own early return for that case.

/// Element that grants an achievement to all mobs who killed the owner when it dies
// tg: code/datums/elements/kill_achievement.dm:2
public partial class datum_element_kill_achievement : datum_element
{
    // tg: code/datums/elements/kill_achievement.dm:19
    [DmParams("target", "achievement_types", "crusher_achievement_type", "kill_memory_type", "achievement_range", "crusher_kill_threshold", "tally_string")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        DmList achievement_types = Dm.arg(__extra, 0) as DmList;
        object crusher_achievement_type = Dm.arg(__extra, 1);
        object kill_memory_type = Dm.arg(__extra, 2);
        float? achievement_range = Dm.arg<float?>(__extra, 3);
        float? crusher_kill_threshold = Dm.arg<float?>(__extra, 4);
        string tally_string = Dm.arg(__extra, 5) as string;
        achievement_range ??= 7;
        crusher_kill_threshold ??= 0.6f;
        tally_string ??= "megafauna_kills";
        object dot = await base.Attach(target, Dm.forward(__extra, achievement_types, crusher_achievement_type, kill_memory_type, achievement_range, crusher_kill_threshold, tally_string));
        if (!isliving(target) || !Dm.truthy(length(achievement_types)))
            return ELEMENT_INCOMPATIBLE;
        this.achievement_types = achievement_types;
        this.crusher_achievement_type = crusher_achievement_type as System.Type;
        this.kill_memory_type = kill_memory_type;
        this.achievement_range = Dm.num(achievement_range);
        this.crusher_kill_threshold = crusher_kill_threshold;
        this.tally_string = tally_string;
        RegisterSignal(target, COMSIG_LIVING_DEATH, nameof(on_death));
        return dot;
    }

    // tg: code/datums/elements/kill_achievement.dm:31
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_LIVING_DEATH);
        return dot;
    }

    // tg: code/datums/elements/kill_achievement.dm:35
    [SignalHandler]
    public virtual void on_death(mob_living source = null, object gibbed = null)
    {
        // SSachievements.achievements_enabled: the subsystem is not ported (no achievements), so it reads FALSE and tg's own
        // early return is taken; the awards, the killers' memory and the blackbox tally below it wait for the achievements.
        object achievements_enabled__gap = FALSE;
        if (Dm.truthy(Dm.bitand(source.flags_1, ADMIN_SPAWNED_1)) || !Dm.truthy(achievements_enabled__gap))
            return;
    }
}
