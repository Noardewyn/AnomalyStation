// Translated from tgstation (snapshot 1d99bd7f): code/datums/mood_events/death.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MS1 (D333, D339): the whole file. The var blocks are generated (D111); the file-local priority defines (:4-12) are read only
// by the var blocks.

// tg: code/datums/mood_events/death.dm:14
public partial class datum_mood_event_conditional_see_death : datum_mood_event_conditional
{
    // tg: code/datums/mood_events/death.dm:18
    [DmParams("home", "who", "dead_mob", "dusted", "gibbed")]
    public override object can_effect_mob(datum_mood home = null, mob_living who = null, params object[] __extra)
    {
        mob dead_mob = Dm.arg<mob>(__extra, 0);
        if (dead_mob == null)
        {
            stack_trace("Death mood event being applied with null dead_mob", "code/datums/mood_events/death.dm", 20);
            return FALSE;
        }

        return base.can_effect_mob(home, who, __extra);
    }

    // tg: code/datums/mood_events/death.dm:25
    [DmParams("who", "dead_mob", "dusted", "gibbed")]
    public override object condition_fulfilled(mob_living who = null, params object[] __extra)
    {
        return TRUE;
    }

    // tg: code/datums/mood_events/death.dm:28
    [DmParams("dead_mob", "dusted", "gibbed")]
    public override async DmTask add_effects(params object[] __extra)
    {
        mob dead_mob = Dm.arg<mob>(__extra, 0);
        object dusted = Dm.arg(__extra, 1);
        object gibbed = Dm.arg(__extra, 2);
        update_effect(dead_mob, dusted, gibbed);

        if (HAS_TRAIT(dead_mob, TRAIT_SPAWNED_MOB))
        {
            mood_change *= 0.25f;
            timeout = Dm.num(timeout) * 0.2f;
        }

        if (mood_change < 0)
        {
            float? desensitized_level = owner.mind?.desensitized_level;
            mood_change = ceil(mood_change * max(DESENSITIZED_MINIMUM, Dm.truthy(desensitized_level) ? Dm.num(desensitized_level) : 1.0f));

            if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_compassionate))))
            {
                mood_change *= 1.5f;
                timeout = Dm.num(timeout) * 1.5f;
            }
        }

        if (Dm.truthy(gibbed) || Dm.truthy(dusted))
        {
            mood_change *= 1.2f;
            timeout = Dm.num(timeout) * 1.5f;
        }

        if (!Dm.truthy(description))
        {
            if (Dm.truthy(gibbed))
                description = I18n.T("%DEAD_MOB% just exploded in front of me!!");
            else if (Dm.truthy(dusted))
                description = I18n.T("%DEAD_MOB% was just vaporized in front of me!!");
            else
                description = I18n.T("I just saw %DEAD_MOB% die. How horrible...");
        }

        description = capitalize(replacetext(description, "%DEAD_MOB%", get_descriptor(dead_mob)));
    }

    /// Blank proc which allows conditional effects to modify mood, timeout, or description before the main effect is applied
    // tg: code/datums/mood_events/death.dm:57
    public virtual void update_effect(mob dead_mob = null, object dusted = null, object gibbed = null)
    {
        return;
    }

    /// Checks if the dead mob is a pet
    // tg: code/datums/mood_events/death.dm:61
    public virtual object is_pet(mob dead_mob = null)
    {
        return Dm.num(istype(dead_mob, typeof(mob_living_basic_pet)) || HAS_TRAIT(dead_mob, TRAIT_LESSER_HUMANOID));
    }

    // tg: code/datums/mood_events/death.dm:64
    [DmParams("home", "dead_mob", "dusted", "gibbed")]
    public override object be_refreshed(datum_mood home = null, params object[] __extra)
    {
        mob dead_mob = Dm.arg<mob>(__extra, 0);
        if (Dm.truthy(can_stack_effect(dead_mob)))
            mood_change *= 1.5f;
        return base.be_refreshed(home, __extra);
    }

    // tg: code/datums/mood_events/death.dm:69
    [DmParams("home", "new_event", "dead_mob", "dusted", "gibbed")]
    public override object be_replaced(datum_mood home = null, datum_mood_event new_event = null, params object[] __extra)
    {
        mob dead_mob = Dm.arg<mob>(__extra, 0);
        object dusted = Dm.arg(__extra, 1);
        object gibbed = Dm.arg(__extra, 2);
        object dot = null;
        dot = base.be_replaced(home, new_event, __extra);
        // when blocking a new mood event (because it's lower priority), refresh ourselves instead
        if (Dm.eq(dot, BLOCK_NEW_MOOD))
            return be_refreshed(home, dead_mob, dusted, gibbed);
        return dot;
    }

    /// Checks if our mood can get worse by seeing another death (or better if we're weird like that)
    // tg: code/datums/mood_events/death.dm:76
    public virtual object can_stack_effect(mob dead_mob = null)
    {
        // if we're desensitized, don't stack unless it's a buff
        if (IS_DESENSITIZED(owner) && mood_change < 0)
            return FALSE;
        // if we're seeing a spawned mob die, don't stack
        if (HAS_TRAIT(dead_mob, TRAIT_SPAWNED_MOB))
            return FALSE;
        return TRUE;
    }

    /// Changes "I saw Joe x" to "I saw the engineer x"
    // tg: code/datums/mood_events/death.dm:86
    public virtual object get_descriptor(mob dead_mob = null)
    {
        if (Dm.truthy(is_pet(dead_mob)))
            return I18n.T("[]", dead_mob);
        if (dead_mob.name != "Unknown" && Dm.truthy(Dm.bitand(dead_mob.mind?.assigned_role?.job_flags, JOB_CREW_MEMBER)))
            return I18n.T("the []", LOWER_TEXT(dead_mob.mind?.assigned_role.title));
        return I18n.T("someone");
    }
}

/// Highest priority: Clown naivety about death
// tg: code/datums/mood_events/death.dm:94
public partial class datum_mood_event_conditional_see_death_naive : datum_mood_event_conditional_see_death
{
    // tg: code/datums/mood_events/death.dm:98
    [DmParams("who", "dead_mob", "dusted", "gibbed")]
    public override object condition_fulfilled(mob_living who = null, params object[] __extra)
    {
        object dusted = Dm.arg(__extra, 1);
        object gibbed = Dm.arg(__extra, 2);
        return Dm.num(HAS_MIND_TRAIT(who, TRAIT_NAIVE) && !Dm.truthy(dusted) && !Dm.truthy(gibbed));
    }

    // tg: code/datums/mood_events/death.dm:101
    public override void update_effect(mob dead_mob = null, object dusted = null, object gibbed = null)
    {
        description = I18n.T("Have a good nap, [].", get_descriptor(dead_mob));
    }
}

/// Cultists are super brainwashed so they get buffs instead
// tg: code/datums/mood_events/death.dm:105
public partial class datum_mood_event_conditional_see_death_cult : datum_mood_event_conditional_see_death
{
    // tg: code/datums/mood_events/death.dm:110
    [DmParams("who", "dead_mob", "dusted", "gibbed")]
    public override object condition_fulfilled(mob_living who = null, params object[] __extra)
    {
        mob dead_mob = Dm.arg<mob>(__extra, 0);
        if (!HAS_TRAIT(who, TRAIT_CULT_HALO))
            return FALSE;
        if (HAS_TRAIT(dead_mob, TRAIT_CULT_HALO))
            return FALSE;
        return TRUE;
    }
}

/// Revs are also brainwashed but less so
// tg: code/datums/mood_events/death.dm:118
public partial class datum_mood_event_conditional_see_death_revolutionary : datum_mood_event_conditional_see_death
{
    // tg: code/datums/mood_events/death.dm:122
    [DmParams("who", "dead_mob", "dusted", "gibbed")]
    public override object condition_fulfilled(mob_living who = null, params object[] __extra)
    {
        mob dead_mob = Dm.arg<mob>(__extra, 0);
        return Dm.num(Dm.truthy(IS_REVOLUTIONARY(who)) && Dm.truthy(Dm.bitand(dead_mob.mind?.assigned_role.job_flags, JOB_HEAD_OF_STAFF)));
    }

    // tg: code/datums/mood_events/death.dm:125
    public override void update_effect(mob dead_mob = null, object dusted = null, object gibbed = null)
    {
        datum_job possible_head_job = dead_mob.mind?.assigned_role;
        description = I18n.T("[] is dead! Long live the revolution!", Dm.truthy(possible_head_job.title) ? I18n.T("The []", LOWER_TEXT(possible_head_job.title)) : I18n.T("Another head of staff"));
    }
}

/// Then gamers
// tg: code/datums/mood_events/death.dm:130
public partial class datum_mood_event_conditional_see_death_gamer : datum_mood_event_conditional_see_death
{
    // tg: code/datums/mood_events/death.dm:135
    [DmParams("who", "dead_mob", "dusted", "gibbed")]
    public override object condition_fulfilled(mob_living who = null, params object[] __extra)
    {
        return Dm.num(istype(who.mind?.assigned_role, typeof(datum_job_bitrunning_glitch)) || istype(who.mind?.assigned_role, typeof(datum_job_bit_avatar)));
    }
}

/// People who just don't gaf
// tg: code/datums/mood_events/death.dm:139
public partial class datum_mood_event_conditional_see_death_dontcare : datum_mood_event_conditional_see_death
{
    // tg: code/datums/mood_events/death.dm:144
    [DmParams("who", "dead_mob", "dusted", "gibbed")]
    public override object condition_fulfilled(mob_living who = null, params object[] __extra)
    {
        mob dead_mob = Dm.arg<mob>(__extra, 0);
        if (Dm.truthy(HAS_PERSONALITY(who, typeof(datum_personality_callous))))
            return TRUE;
        if (Dm.truthy(HAS_PERSONALITY(who, typeof(datum_personality_animal_disliker))) && Dm.truthy(is_pet(dead_mob)))
            return TRUE;
        return FALSE;
    }

    // tg: code/datums/mood_events/death.dm:151
    public override void update_effect(mob dead_mob = null, object dusted = null, object gibbed = null)
    {
        if (Dm.truthy(gibbed))
            description = I18n.T("Oh, %DEAD_MOB% exploded. Now I have to get the mop.");
        else if (Dm.truthy(dusted))
            description = I18n.T("Oh, %DEAD_MOB% was vaporized. Now I have to get the dustpan.");
        else
            description = I18n.T("Oh, %DEAD_MOB% died. Shame, I guess.");
    }
}

/// Ashwalkers get a small boost from sacrificing people to the necropolis spire, and don't care otherwise
// tg: code/datums/mood_events/death.dm:160
public partial class datum_mood_event_conditional_see_death_ashwalker : datum_mood_event_conditional_see_death
{
    // tg: code/datums/mood_events/death.dm:164
    [DmParams("who", "dead_mob", "dusted", "gibbed")]
    public override object condition_fulfilled(mob_living who = null, params object[] __extra)
    {
        mob dead_mob = Dm.arg<mob>(__extra, 0);
        return Dm.num(HAS_TRAIT(who, TRAIT_NECROPOLIS_WORSHIP) && !HAS_TRAIT(dead_mob, TRAIT_NECROPOLIS_WORSHIP));
    }

    // tg: code/datums/mood_events/death.dm:167
    public override void update_effect(mob dead_mob = null, object dusted = null, object gibbed = null)
    {
        if (Dm.truthy(gibbed))
        {
            description = I18n.T("%DEAD_MOB% hasss been torn asssunder, glory to the Necropolisss!");
            mood_change = Dm.initial(typeof(datum_mood_event_conditional_see_death), (datum_mood_event p) => p.mood_change) * -0.5f;
        }
        else if (Dm.truthy(dusted))
            description = I18n.T("Oh, %DEAD_MOB% wasss vaporized.");
        else
            description = I18n.T("Oh, %DEAD_MOB% died. Ssshame, I guesss.");
    }
}

/// Pets take priority over normal death moodlets
// tg: code/datums/mood_events/death.dm:177
public partial class datum_mood_event_conditional_see_death_pet : datum_mood_event_conditional_see_death
{
    // tg: code/datums/mood_events/death.dm:180
    [DmParams("who", "dead_mob", "dusted", "gibbed")]
    public override object condition_fulfilled(mob_living who = null, params object[] __extra)
    {
        mob dead_mob = Dm.arg<mob>(__extra, 0);
        return is_pet(dead_mob);
    }

    // tg: code/datums/mood_events/death.dm:183
    public override void update_effect(mob dead_mob = null, object dusted = null, object gibbed = null)
    {
        if (Dm.truthy(gibbed))
            description = I18n.T("%DEAD_MOB% just exploded!!");
        else if (Dm.truthy(dusted))
            description = I18n.T("%DEAD_MOB% just vaporized!!");
        else
            description = I18n.T("%DEAD_MOB% just died!!");

        // future todo : make the hop care about ian, cmo runtime, etc.
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_animal_friend))))
        {
            mood_change *= 1.5f;
            timeout = Dm.num(timeout) * 1.25f;
        }
        else if (!Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_compassionate))))
        {
            mood_change *= 0.25f;
            timeout = Dm.num(timeout) * 0.5f;
        }
    }
}

/// Small boost if you see a xenomorph die
// tg: code/datums/mood_events/death.dm:200
public partial class datum_mood_event_conditional_see_death_xeno : datum_mood_event_conditional_see_death
{
    /// Check if the passed mob is any kind of xenomorph (covering for both carbon and basic types)
    // tg: code/datums/mood_events/death.dm:204
    public virtual object is_any_xenomorph(mob target = null)
    {
        return Dm.num(isalien(target) || isalienadult(target)); // latter proc should have coverage for basic mbos
    }

    // tg: code/datums/mood_events/death.dm:207
    [DmParams("who", "dead_mob", "dusted", "gibbed")]
    public override object condition_fulfilled(mob_living who = null, params object[] __extra)
    {
        mob dead_mob = Dm.arg<mob>(__extra, 0);
        if (Dm.truthy(is_any_xenomorph(who)))
            return FALSE;

        if (HAS_TRAIT(who, TRAIT_XENO_HOST))
            return TRUE;

        return is_any_xenomorph(dead_mob);
    }

    // Give buffs based on the type of xenomorph dying
    // tg: code/datums/mood_events/death.dm:217
    public override void update_effect(mob dead_mob = null, object dusted = null, object gibbed = null)
    {
        // following values are in absolute value form, we make it have a positive effect later
        float change_modifier = 0;
        float timeout_modifier = 0;

        if (HAS_TRAIT(owner, TRAIT_XENO_HOST))
        {
            handle_embryo_carrier(dead_mob);
            return;
        }

        if (islarva(dead_mob))
        {
            change_modifier = 0.1f;
            timeout_modifier = 0.1f;
            description = I18n.T("Fine day whenever those slugs get squished.");
        }

        if (isalienadult(dead_mob))
        {
            change_modifier = 0.25f;
            timeout_modifier = 0.25f;
            description = I18n.T("That xenomorph bit the dust! Hell yeah!");
            if (Dm.truthy(gibbed) || Dm.truthy(dusted))
            {
                change_modifier += 0.1f;
                timeout_modifier += 0.1f;
                description = I18n.T("It warms my heart to see xenomorphs get blown to bits!");
            }
        }

        if (isalienroyal(dead_mob) || istype(dead_mob, typeof(mob_living_basic_alien_queen)))
        {
            change_modifier = 0.5f;
            timeout_modifier = 0.5f;
            description = I18n.T("The queen has fallen! The galaxy lives another day! I hope those bastards all rot in hell!");
            if (Dm.truthy(gibbed) || Dm.truthy(dusted))
            {
                change_modifier += 0.25f;
                timeout_modifier += 0.25f;
                description = I18n.T("Seeing the xenomorph queen blown to bits fills me with extreme joy!");
            }
        }


        mood_change = Dm.initial(this, (datum_mood_event p) => p.mood_change) * -change_modifier;
        timeout = Dm.num(Dm.initial(this, (datum_mood_event p) => p.timeout)) * timeout_modifier;
    }

    /// Separate proc that handles cases where the viewer is carrying a xenomorph embryo
    // tg: code/datums/mood_events/death.dm:254
    public virtual void handle_embryo_carrier(mob dead_mob = null)
    {
        if (!HAS_TRAIT(owner, TRAIT_XENO_HOST))
            return;

        obj_item_organ_body_egg_alien_embryo embryo = owner.get_organ_by_type(typeof(obj_item_organ_body_egg_alien_embryo)) as obj_item_organ_body_egg_alien_embryo;
        if (embryo == null)
        {
            stack_trace(Dm.text("Xeno Host [] missing embryo organ despite having XENO_HOST trait. What the fuck?", owner), "code/datums/mood_events/death.dm", 260);
            return;
        }

        if (IS_UNCONSCIOUS_OR_CRIT(owner)) // if the carrier is sleeping then presumably the embryo's hivemind isn't affected
            return;

        // You feel a lot worse if you're conscious and see a xenomorph die while implanted because the hivemind feels the loss of their sister
        float embryo_stage_multiplier = 1 + (embryo.stage / 10);
        mood_change *= embryo_stage_multiplier;
        timeout = Dm.num(timeout) * embryo_stage_multiplier;
        description = I18n.T("There's something inside of me churning after I saw that xenomorph die.");
        RegisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_XENO_HOST), nameof(on_embryo_removal));
    }

    /// Handles cleanup once the embryo carrier dies
    // tg: code/datums/mood_events/death.dm:274
    [SignalHandler]
    public virtual void on_embryo_removal(datum source = null)
    {
        qdel(this);
    }
}

/// Desensitized brings up the rear
// tg: code/datums/mood_events/death.dm:279
public partial class datum_mood_event_conditional_see_death_desensitized : datum_mood_event_conditional_see_death
{
    // tg: code/datums/mood_events/death.dm:283
    [DmParams("who", "dead_mob", "dusted", "gibbed")]
    public override object condition_fulfilled(mob_living who = null, params object[] __extra)
    {
        return Dm.num(IS_DESENSITIZED(who));
    }

    // tg: code/datums/mood_events/death.dm:286
    public override void update_effect(mob dead_mob = null, object dusted = null, object gibbed = null)
    {
        if (Dm.truthy(gibbed))
            description = I18n.T("I saw %DEAD_MOB% explode.");
        else if (Dm.truthy(dusted))
            description = I18n.T("I saw %DEAD_MOB% get vaporized.");
        else
            description = I18n.T("I saw %DEAD_MOB% die.");
    }
}
