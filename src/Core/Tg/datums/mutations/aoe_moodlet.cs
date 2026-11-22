// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/aoe_moodlet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the unsightly and comely mutations, which give the people around a mood event; the mood events they
// give (aoe_moodlet.dm:75-126) since gaps MS1. The var blocks are generated (D111).

// tg: code/datums/mutations/aoe_moodlet.dm:1
public partial class datum_mutation_aoe_moodlet : datum_mutation
{
    // tg: code/datums/mutations/aoe_moodlet.dm:11
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignal(acquirer, COMSIG_ATOM_EXAMINE, nameof(on_examined));
        RegisterSignal(acquirer, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        return dot;
    }

    // tg: code/datums/mutations/aoe_moodlet.dm:18
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        UnregisterSignal(owner, COMSIG_ATOM_EXAMINE);
        UnregisterSignal(owner, COMSIG_MOVABLE_MOVED);
        return dot;
    }

    // tg: code/datums/mutations/aoe_moodlet.dm:25
    [SignalHandler]
    public virtual void on_examined(datum source = null, mob examiner = null, params object[] __extra)
    {
        if (examiner == owner || !isliving(examiner) || HAS_TRAIT(owner, TRAIT_FACE_COVERED))
            return;
        Waiting.NoSleep(apply_other_moodlet(examiner as mob_living));
    }

    // tg: code/datums/mutations/aoe_moodlet.dm:32
    [SignalHandler]
    public virtual void on_moved(datum source = null, params object[] __extra)
    {
        obj_structure_mirror mirror = Dm.locate_type(typeof(obj_structure_mirror), owner.loc) as obj_structure_mirror;
        if (mirror == null)
            return;
        Waiting.NoSleep(apply_own_moodlet());
    }

    // tg: code/datums/mutations/aoe_moodlet.dm:40
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (HAS_TRAIT(owner, TRAIT_FACE_COVERED))
            return; // this is applied by newspapers, and i think it would be funny if newspapers specifically blocked 100% of the effect.

        foreach (mob_living nearby in Dm.each<mob_living>(viewers(3, owner)))
        {
            if (nearby == owner)
                continue;
            await apply_other_moodlet(nearby);
        }
    }

    // tg: code/datums/mutations/aoe_moodlet.dm:49
    public virtual async DmTask apply_own_moodlet()
    {
        await owner.add_mood_event(type, typeof(datum_mood_event_seen_ugly_mutation_self), owner);
    }

    // tg: code/datums/mutations/aoe_moodlet.dm:52
    public virtual async DmTask apply_other_moodlet(mob_living other = null)
    {
        await other.add_mood_event(type, get_moodlet(), owner);
    }

    // tg: code/datums/mutations/aoe_moodlet.dm:55
    public virtual object get_moodlet()
    {
        return Dm.truthy(Dm.bitand(owner.obscured_slots, HIDEFACE)) ? typeof(datum_mood_event_seen_ugly_mutation_weak) : typeof(datum_mood_event_seen_ugly_mutation);
    }
}

// tg: code/datums/mutations/aoe_moodlet.dm:58
public partial class datum_mutation_aoe_moodlet_positive : datum_mutation_aoe_moodlet
{
    // tg: code/datums/mutations/aoe_moodlet.dm:68
    public override async DmTask apply_own_moodlet()
    {
        await owner.add_mood_event(type, typeof(datum_mood_event_seen_pretty_mutation_self), owner);
    }

    // tg: code/datums/mutations/aoe_moodlet.dm:71
    public override object get_moodlet()
    {
        return Dm.truthy(Dm.bitand(owner.obscured_slots, HIDEFACE)) ? typeof(datum_mood_event_seen_pretty_mutation_weak) : typeof(datum_mood_event_seen_pretty_mutation);
    }
}

// Negative version
// tg: code/datums/mutations/aoe_moodlet.dm:75  (gaps MS1)
public partial class datum_mood_event_seen_ugly_mutation : datum_mood_event
{
    // tg: code/datums/mutations/aoe_moodlet.dm:80
    public override object can_effect_mob(datum_mood home = null, mob_living who = null, params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(who, typeof(datum_personality_compassionate))) || Dm.truthy(HAS_PERSONALITY(who, typeof(datum_personality_empathetic))))
            return FALSE;

        return base.can_effect_mob(home, who, __extra);
    }

    // tg: code/datums/mutations/aoe_moodlet.dm:86
    [DmParams("mutant")]
    public override async DmTask add_effects(params object[] __extra)
    {
        mob mutant = Dm.arg<mob>(__extra, 0);
        description = I18n.T("Something is seriously wrong with []!", Dm.truthy(mutant) ? mutant : I18n.T("that guy"));
    }
}

// tg: code/datums/mutations/aoe_moodlet.dm:89  (gaps MS1)
public partial class datum_mood_event_seen_ugly_mutation_weak : datum_mood_event_seen_ugly_mutation
{
    // tg: code/datums/mutations/aoe_moodlet.dm:93
    [DmParams("mutant")]
    public override async DmTask add_effects(params object[] __extra)
    {
        mob mutant = Dm.arg<mob>(__extra, 0);
        description = I18n.T("Something looks wrong about [].", Dm.truthy(mutant) ? mutant : I18n.T("that guy"));
    }
}

// Positive version
// tg: code/datums/mutations/aoe_moodlet.dm:102  (gaps MS1)
public partial class datum_mood_event_seen_pretty_mutation : datum_mood_event
{
    // tg: code/datums/mutations/aoe_moodlet.dm:107
    public override object can_effect_mob(datum_mood home = null, mob_living who = null, params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(who, typeof(datum_personality_misanthropic))) || Dm.truthy(HAS_PERSONALITY(who, typeof(datum_personality_callous))))
            return FALSE;

        return base.can_effect_mob(home, who, __extra);
    }

    // tg: code/datums/mutations/aoe_moodlet.dm:113
    [DmParams("mutant")]
    public override async DmTask add_effects(params object[] __extra)
    {
        mob mutant = Dm.arg<mob>(__extra, 0);
        description = I18n.T("[] looks pretty good - must be a new haircut or something.", Dm.truthy(mutant) ? mutant : I18n.T("That person"));
    }
}

// tg: code/datums/mutations/aoe_moodlet.dm:116  (gaps MS1)
public partial class datum_mood_event_seen_pretty_mutation_weak : datum_mood_event_seen_pretty_mutation
{
    // tg: code/datums/mutations/aoe_moodlet.dm:120
    [DmParams("mutant")]
    public override async DmTask add_effects(params object[] __extra)
    {
        mob mutant = Dm.arg<mob>(__extra, 0);
        description = I18n.T("[] looks alright - it must be the lighting in here.", Dm.truthy(mutant) ? mutant : I18n.T("That person"));
    }
}
