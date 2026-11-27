// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/debuffs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the M2 subset translated (M2 P1, the living core): /datum/status_effect/incapacitating and its stun,
// knockdown, immobilized, paralyzed, incapacitated, unconscious and sleeping subtypes, knocked_out and
// GLOB.unconscious_appearances, with the file-local defines; M8 slice E: convulsing; M8y slice B: stasis; the other debuffs of the file come
// with their systems. Syndicate G4c: his_wrath. Gaps P3: spasms, dna_melt, go_away (with deletes_mob) and freezing_blast. Gaps E1a: teleport_madness. Gaps E4: fake_virus. Gaps B1: discoordinated (the discoordination trauma). Gaps MS1: gonbola_pacify (the gonbola's peace and its moodlet). Gaps GU1: designated_target (the .38 flare round's mark). Gaps RG2: necropolis_curse with apply_necropolis_curse and the curse's temp visual. The kinetic crusher's mark (crusher_mark, its detonation and the backstab).

// tg: code/datums/status_effects/debuffs/debuffs.dm:2  #define HEALING_SLEEP_DEFAULT 0.2 (file-local, #undef at the end): a private const of
// /datum/status_effect/incapacitating/sleeping below; HEALING_SLEEP_ORGAN_MULTIPLIER (:4) and SLEEP_QUALITY_WORKOUT_MULTIPLER
// (:6) likewise.

//Largely negative status effects go here, even if they have small beneficial effects
//STUN EFFECTS
// tg: code/datums/status_effects/debuffs/debuffs.dm:10
[DmPath("/datum/status_effect/incapacitating")]
public partial class datum_status_effect_incapacitating : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:11-17  the var overrides of the type block (rules 10.2)
    public datum_status_effect_incapacitating()
    {
        id = STATUS_EFFECT_ID_ABSTRACT;
        tick_interval = STATUS_EFFECT_NO_TICK;
        status_type = STATUS_EFFECT_REPLACE;
        alert_type = null;
        processing_speed = STATUS_EFFECT_PRIORITY;
        remove_on_fullheal = TRUE;
        heal_flag_necessary = HEAL_CC_STATUS;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:25
    [DmParams("new_owner", "set_duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object set_duration = Dm.arg(__extra, 0);
        object dot = null;
        if (isnum(set_duration))
            duration = Dm.num_or_null(set_duration);
        dot = await base.on_creation(new_owner, Dm.forward(__extra, set_duration));
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(needs_update_stat) || issilicon(owner))
            owner.update_stat();
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:34
    public override async DmTask<object> on_remove()
    {
        if (Dm.truthy(needs_update_stat) || issilicon(owner)) //silicons need stat updates in addition to normal canmove updates
            owner.update_stat();
        return await base.on_remove();
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:39
    [ShouldCallParent]
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = null;
        dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;

        try_force_say();
        return dot;
    }

    /// Chance of triggering a force_say
    // tg: code/datums/status_effects/debuffs/debuffs.dm:19
    public float force_say_chance = 66;
    /// If TRUE we run update_stat on apply/remove
    // tg: code/datums/status_effects/debuffs/debuffs.dm:21
    public float needs_update_stat = FALSE;
    /// Suffixes attached to the force_say when applied, uses the "hurt" suffixes by default
    // tg: code/datums/status_effects/debuffs/debuffs.dm:23
    public DmList alter_phrases;
}

// tg: code/datums/status_effects/debuffs/debuffs.dm:47
public partial class datum_status_effect_incapacitating : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:47
    [ShouldCallParent]
    public virtual void try_force_say()
    {
        mob_living_carbon_human human = owner as mob_living_carbon_human;
        if (human is mob_living_carbon_human && prob(force_say_chance) && (Dm.num(duration) >= 6 * SECONDS || (Dm.num(duration) >= 2 * SECONDS && prob(66))))
            human.force_say(alter_phrases, immediate: TRUE, major: Dm.num(duration) >= 30 * SECONDS ? TRUE : FALSE);
    }
}

//STUN
// tg: code/datums/status_effects/debuffs/debuffs.dm:55
[DmPath("/datum/status_effect/incapacitating/stun")]
public partial class datum_status_effect_incapacitating_stun : datum_status_effect_incapacitating
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:56
    public datum_status_effect_incapacitating_stun()
    {
        id = "stun";
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:58
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = null;
        dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;
        owner.add_traits(Dm.list(TRAIT_INCAPACITATED, TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED), TRAIT_STATUS_EFFECT(id));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:64
    public override async DmTask<object> on_remove()
    {
        owner.remove_traits(Dm.list(TRAIT_INCAPACITATED, TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED), TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }
}

//KNOCKDOWN
// tg: code/datums/status_effects/debuffs/debuffs.dm:69
[DmPath("/datum/status_effect/incapacitating/knockdown")]
public partial class datum_status_effect_incapacitating_knockdown : datum_status_effect_incapacitating
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:70
    public datum_status_effect_incapacitating_knockdown()
    {
        id = "knockdown";
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:72
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = null;
        dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(owner, TRAIT_FLOORED, TRAIT_STATUS_EFFECT(id));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:78
    public override async DmTask<object> on_remove()
    {
        REMOVE_TRAIT(owner, TRAIT_FLOORED, TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }
}

//IMMOBILIZED
// tg: code/datums/status_effects/debuffs/debuffs.dm:84
[DmPath("/datum/status_effect/incapacitating/immobilized")]
public partial class datum_status_effect_incapacitating_immobilized : datum_status_effect_incapacitating
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:85-86
    public datum_status_effect_incapacitating_immobilized()
    {
        id = "immobilized";
        force_say_chance = 0;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:88
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = null;
        dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(owner, TRAIT_IMMOBILIZED, TRAIT_STATUS_EFFECT(id));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:94
    public override async DmTask<object> on_remove()
    {
        REMOVE_TRAIT(owner, TRAIT_IMMOBILIZED, TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }
}

//PARALYZED
// tg: code/datums/status_effects/debuffs/debuffs.dm:100
[DmPath("/datum/status_effect/incapacitating/paralyzed")]
public partial class datum_status_effect_incapacitating_paralyzed : datum_status_effect_incapacitating
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:101
    public datum_status_effect_incapacitating_paralyzed()
    {
        id = "paralyzed";
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:104
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = null;
        dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;
        owner.add_traits(Dm.list(TRAIT_INCAPACITATED, TRAIT_IMMOBILIZED, TRAIT_FLOORED, TRAIT_HANDS_BLOCKED), TRAIT_STATUS_EFFECT(id));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:110
    public override async DmTask<object> on_remove()
    {
        owner.remove_traits(Dm.list(TRAIT_INCAPACITATED, TRAIT_IMMOBILIZED, TRAIT_FLOORED, TRAIT_HANDS_BLOCKED), TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:102  the object-creating override (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        alter_phrases = Dm.list(""); // "Why am I about to be froz-"
    }
}

//INCAPACITATED
/// This status effect represents anything that leaves a character unable to perform basic tasks (interrupting do-afters, for example), but doesn't incapacitate them further than that (no stuns etc..)
// tg: code/datums/status_effects/debuffs/debuffs.dm:116
[DmPath("/datum/status_effect/incapacitating/incapacitated")]
public partial class datum_status_effect_incapacitating_incapacitated : datum_status_effect_incapacitating
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:117-118
    public datum_status_effect_incapacitating_incapacitated()
    {
        id = "incapacitated";
        force_say_chance = 0;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:121
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = null;
        dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(owner, TRAIT_INCAPACITATED, TRAIT_STATUS_EFFECT(id));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:128
    public override async DmTask<object> on_remove()
    {
        REMOVE_TRAIT(owner, TRAIT_INCAPACITATED, TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }
}

//UNCONSCIOUS
// tg: code/datums/status_effects/debuffs/debuffs.dm:134
[DmPath("/datum/status_effect/incapacitating/unconscious")]
public partial class datum_status_effect_incapacitating_unconscious : datum_status_effect_incapacitating
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:135-136
    public datum_status_effect_incapacitating_unconscious()
    {
        id = "unconscious";
        force_say_chance = 100;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:138
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = null;
        dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(owner, TRAIT_KNOCKEDOUT, TRAIT_STATUS_EFFECT(id));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:144
    public override async DmTask<object> on_remove()
    {
        REMOVE_TRAIT(owner, TRAIT_KNOCKEDOUT, TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }
}

/// Handles the effects of being knocked out - don't apply directly, all you should be doing is using [TRAIT_KNOCKEDOUT]
// tg: code/datums/status_effects/debuffs/debuffs.dm:149
[DmPath("/datum/status_effect/knocked_out")]
public partial class datum_status_effect_knocked_out : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:150-151
    public datum_status_effect_knocked_out()
    {
        id = "is_knocked_out";
        alert_type = null;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:153
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.become_blind(TRAIT_STATUS_EFFECT(id));
        await owner.apply_status_effect(typeof(datum_status_effect_grouped_see_no_names), TRAIT_STATUS_EFFECT(id));
        owner.add_traits(Dm.list(TRAIT_HANDS_BLOCKED, TRAIT_IMMOBILIZED, TRAIT_BLOCK_SECHUD, TRAIT_BLOCK_MEDHUD, TRAIT_INCAPACITATED, TRAIT_FLOORED), TRAIT_STATUS_EFFECT(id));
        await owner.update_eyes(); // updates eyelids
        RegisterSignal(owner, COMSIG_MOB_STATCHANGE, nameof(on_mob_statchange));
        RegisterSignal(owner, COMSIG_MOB_CLIENT_LOGIN, nameof(show_unconscious_hud));
        if (Dm.truthy(GET_CLIENT(owner))) // let's not waste time giving the hud to non-player characters
            show_unconscious_hud(owner);
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:164
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.on_creation(new_owner, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (owner.stat == DEAD)
            stop_ticking();
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:171
    public override async DmTask<object> on_remove()
    {
        owner.cure_blind(TRAIT_STATUS_EFFECT(id));
        owner.remove_status_effect(typeof(datum_status_effect_grouped_see_no_names), TRAIT_STATUS_EFFECT(id));
        owner.remove_traits(Dm.list(TRAIT_HANDS_BLOCKED, TRAIT_IMMOBILIZED, TRAIT_BLOCK_SECHUD, TRAIT_BLOCK_MEDHUD, TRAIT_INCAPACITATED, TRAIT_FLOORED), TRAIT_STATUS_EFFECT(id));
        await owner.update_eyes(); // updates eyelids
        UnregisterSignal(owner, Dm.list(COMSIG_MOB_CLIENT_LOGIN, COMSIG_MOB_STATCHANGE));
        if (Dm.truthy(GET_CLIENT(owner)))
            hide_unconscious_hud(owner);
        return null;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:180
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        await owner.adjust_stamina_loss(Dm.mul(-3, seconds_between_ticks));
        return null;
    }
}

public partial class datum_controller_global_vars : datum_controller
{
    /// Global list of images that correspond to a mob's unconscious appearance
    // tg: code/datums/status_effects/debuffs/debuffs.dm:184  GLOBAL_LIST_EMPTY(unconscious_appearances)
    public DmList unconscious_appearances;

    // tg: code/datums/status_effects/debuffs/debuffs.dm:184  (GLOBAL_MANAGED(unconscious_appearances, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalunconscious_appearances()
    {
        unconscious_appearances = Dm.list();
        Dm.append(ref gvars_datum_init_order, "unconscious_appearances");
    }
}

// tg: code/datums/status_effects/debuffs/debuffs.dm:186
public partial class datum_status_effect_knocked_out : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:186
    [SignalHandler]
    public virtual void show_unconscious_hud(mob_living source = null)
    {

        if (source.client != null)
            Dm.append(source.client.images, Dm.sub(GLOB.unconscious_appearances, source.unconscious_appearance));
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:191
    [SignalHandler]
    public virtual void hide_unconscious_hud(mob_living source = null)
    {

        if (source.client != null)
            Dm.remove(source.client.images, GLOB.unconscious_appearances);
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:196
    [SignalHandler]
    public virtual void on_mob_statchange(mob_living source = null, params object[] __extra)
    {
        if (owner.stat == DEAD)
            stop_ticking();
        else
            start_ticking();
    }
}

//SLEEPING
// tg: code/datums/status_effects/debuffs/debuffs.dm:204
[DmPath("/datum/status_effect/incapacitating/sleeping")]
public partial class datum_status_effect_incapacitating_sleeping : datum_status_effect_incapacitating
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:205-210  the var overrides of the type block (rules 10.2); alter_phrases (:209) creates a list:
    // dm_init_vars below
    public datum_status_effect_incapacitating_sleeping()
    {
        id = "sleeping";
        alert_type = typeof(atom_movable_screen_alert_status_effect_asleep);
        needs_update_stat = TRUE;
        tick_interval = 2 * SECONDS;
        force_say_chance = 100;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:2  #define HEALING_SLEEP_DEFAULT 0.2 (file-local, #undef at the end of the file)
    /// The damage healed per tick while sleeping without any modifiers
    private const float HEALING_SLEEP_DEFAULT = 0.2f;
    // tg: code/datums/status_effects/debuffs/debuffs.dm:4  #define HEALING_SLEEP_ORGAN_MULTIPLIER 5 (file-local)
    /// The sleep healing multiplier for organ passive healing (since organs heal slowly)
    private const int HEALING_SLEEP_ORGAN_MULTIPLIER = 5;
    // tg: code/datums/status_effects/debuffs/debuffs.dm:6  #define SLEEP_QUALITY_WORKOUT_MULTIPLER 10 (file-local)
    /// The sleep multiplier for fitness xp conversion
    private const int SLEEP_QUALITY_WORKOUT_MULTIPLER = 10;

    // tg: code/datums/status_effects/debuffs/debuffs.dm:209  the object-creating override (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        alter_phrases = Dm.list("Zzz...", "ZZz...", "ZZZ...", "zzZ...", "zZZ...", "ZzZ...", "zzz...", "zZz...", "mimimimimimi...");
    }
}

// tg: code/datums/status_effects/debuffs/debuffs.dm:238
public partial class datum_status_effect_incapacitating_sleeping : datum_status_effect_incapacitating
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:212
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = null;
        dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;
        if (!HAS_TRAIT(owner, TRAIT_SLEEPIMMUNE))
            ADD_TRAIT(owner, TRAIT_KNOCKEDOUT, TRAIT_STATUS_EFFECT(id));
        RegisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_SLEEPIMMUNE), nameof(on_owner_insomniac));
        RegisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_SLEEPIMMUNE), nameof(on_owner_sleepy));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:221
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.on_creation(new_owner, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (HAS_TRAIT(owner, TRAIT_SLEEPIMMUNE))
            stop_ticking();
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:228
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, Dm.list(SIGNAL_ADDTRAIT(TRAIT_SLEEPIMMUNE), SIGNAL_REMOVETRAIT(TRAIT_SLEEPIMMUNE)));
        REMOVE_TRAIT(owner, TRAIT_KNOCKEDOUT, TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:233
    public override void try_force_say()
    {
        if (!HAS_TRAIT(owner, TRAIT_SLEEPIMMUNE))
            base.try_force_say();
    }

    ///If the mob is sleeping and gain the TRAIT_SLEEPIMMUNE we remove the TRAIT_KNOCKEDOUT and stop the tick() from happening
    // tg: code/datums/status_effects/debuffs/debuffs.dm:238
    [SignalHandler]
    public virtual void on_owner_insomniac(mob_living source = null)
    {
        REMOVE_TRAIT(owner, TRAIT_KNOCKEDOUT, TRAIT_STATUS_EFFECT(id));
        stop_ticking();
    }

    ///If the mob has the TRAIT_SLEEPIMMUNE but somehow looses it we make him sleep and restart the tick()
    // tg: code/datums/status_effects/debuffs/debuffs.dm:244
    [SignalHandler]
    public virtual void on_owner_sleepy(mob_living source = null)
    {
        ADD_TRAIT(owner, TRAIT_KNOCKEDOUT, TRAIT_STATUS_EFFECT(id));
        start_ticking();
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:249
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (Dm.truthy(owner.maxHealth))
        {
            float health_ratio = owner.health / owner.maxHealth;
            float sleep_quality = HEALING_SLEEP_DEFAULT;

            // having high spirits helps us recover
            if (Dm.truthy(owner.mob_mood))
            {
                switch (owner.mob_mood.sanity_level)
                {
                    case SANITY_LEVEL_GREAT:
                        sleep_quality = 0.2f;
                        break;
                    case SANITY_LEVEL_NEUTRAL:
                        sleep_quality = 0.1f;
                        break;
                    case SANITY_LEVEL_DISTURBED:
                        sleep_quality = 0;
                        break;
                    case SANITY_LEVEL_UNSTABLE:
                        sleep_quality = 0;
                        break;
                    case SANITY_LEVEL_CRAZY:
                        sleep_quality = -0.1f;
                        break;
                    case SANITY_LEVEL_INSANE:
                        sleep_quality = -0.2f;
                        break;
                }
            }

            turf rest_turf = get_turf(owner);
            bool is_sleeping_in_darkness = !Dm.truthy(rest_turf.check_lumcount_above(LIGHTING_TILE_IS_DARK));

            // sleeping with a blindfold or in the dark helps us rest
            if (Dm.truthy(owner.is_blind_from(EYES_COVERED)) || is_sleeping_in_darkness)
                sleep_quality += 0.1f;

            // sleeping in silence is always better
            if (HAS_TRAIT(owner, TRAIT_DEAF))
                sleep_quality += 0.1f;

            // check for beds
            // tg: :282  if((locate(/obj/structure/bed) in owner.loc)) sleep_quality += 0.2
            // GAP(lead): /obj/structure/bed (game/objects/structures/beds_chairs/bed.dm) is not declared, so no bed is found
            // (furniture comes with the map, M3); the else branch below is the table's.
            if (Dm.truthy(Dm.locate<obj_structure_table>(owner.loc)))
                sleep_quality += 0.1f;

            // don't forget the bedsheet
            // tg: :287-288  if(locate(/obj/item/bedsheet) in owner.loc) sleep_quality += 0.1
            // you forgot the pillow
            // tg: :291-292  if(locate(/obj/item/pillow) in owner.loc) sleep_quality += 0.1
            // GAP(lead): /obj/item/bedsheet and /obj/item/pillow are not declared, so neither is found (items, M3-M4a).

            object need_mob_update = FALSE;
            if (sleep_quality > 0)
            {
                if (iscarbon(owner))
                {
                    mob_living_carbon carbon_owner = (mob_living_carbon)owner;
                    // tg: :296-305
                    foreach (obj_item_organ target_organ in Dm.each_cast<obj_item_organ>(carbon_owner.organs))
                    {
                        // no healing boost for robotic or dying organs
                        if (Dm.truthy(IS_ROBOTIC_ORGAN(target_organ)) || !Dm.truthy(target_organ.damage) || Dm.truthy(Dm.bitand(target_organ.organ_flags, ORGAN_FAILING)))
                            continue;

                        // organ regeneration is very low so we crank up the healing rate to give a good bonus
                        float healing_bonus = target_organ.healing_factor * sleep_quality * HEALING_SLEEP_ORGAN_MULTIPLIER;
                        await target_organ.apply_organ_damage(-healing_bonus * target_organ.maxHealth);
                    }

                    // tg: :307
                    datum_status_effect_exercised exercised = carbon_owner.has_status_effect(typeof(datum_status_effect_exercised)) as datum_status_effect_exercised;
                    if (exercised != null && Dm.truthy(carbon_owner.mind))
                    {
                        // the better you sleep, the more xp you gain
                        carbon_owner.mind.adjust_experience(typeof(datum_skill_athletics), Dm.num(seconds_between_ticks) * sleep_quality * SLEEP_QUALITY_WORKOUT_MULTIPLER);
                        await carbon_owner.adjust_timed_status_effect(-1 * Dm.num(seconds_between_ticks) * sleep_quality * SLEEP_QUALITY_WORKOUT_MULTIPLER, typeof(datum_status_effect_exercised));
                        if (prob(2))
                            to_chat(carbon_owner, span_notice(I18n.T("You feel your fitness improving!")));
                    }
                }

                if (health_ratio > 0.8f) // only heals minor physical damage
                {
                    need_mob_update = Dm.add(need_mob_update, await owner.adjust_brute_loss(-0.4f * sleep_quality * Dm.num(seconds_between_ticks), updating_health: FALSE, required_bodytype: BODYTYPE_ORGANIC));
                    need_mob_update = Dm.add(need_mob_update, await owner.adjust_fire_loss(-0.4f * sleep_quality * Dm.num(seconds_between_ticks), updating_health: FALSE, required_bodytype: BODYTYPE_ORGANIC));
                    need_mob_update = Dm.add(need_mob_update, await owner.adjust_tox_loss(-0.2f * sleep_quality * Dm.num(seconds_between_ticks), updating_health: FALSE, forced: TRUE, required_biotype: MOB_ORGANIC));
                }
            }
            need_mob_update = Dm.add(need_mob_update, await owner.adjust_stamina_loss(min(-0.4f * sleep_quality * Dm.num(seconds_between_ticks), -0.4f * HEALING_SLEEP_DEFAULT * Dm.num(seconds_between_ticks)), updating_stamina: FALSE));
            if (Dm.truthy(need_mob_update))
                await owner.updatehealth();
        }
        // Drunkenness gets reduced by 0.3% per tick (6% per 2 seconds)
        await owner.set_drunk_effect(Dm.mul(owner.get_drunk_amount(), 0.997f));

        if (iscarbon(owner))
        {
            // tg: :326-328  var/mob/living/carbon/carbon_owner = owner; carbon_owner.handle_dreams()
            // GAP(M2 P2): /mob/living/carbon/proc/handle_dreams (modules/mob/living/carbon/life.dm) is not declared; dreams are
            // chat text (M9).
            Dm.Deferred("code/datums/status_effects/debuffs/debuffs.dm:328", "sleeping tick: carbon_owner.handle_dreams(), the dream texts (dreams, M10+)");
        }

        if (prob(8) && owner.health > owner.crit_threshold)
        {
            // tg: :330-331  owner.emote("snore")
            await owner.emote("snore");
        }
        return null;
    }
}

// tg: code/datums/status_effects/debuffs/debuffs.dm:333
[DmPath("/atom/movable/screen/alert/status_effect/asleep")]
public partial class atom_movable_screen_alert_status_effect_asleep : atom_movable_screen_alert_status_effect
{
}

// M8y slice B: the stasis effect of the stasis bed (stasis.dm: STASIS_MACHINE_EFFECT); the var blocks and the alert are generated.
//STASIS
// tg: code/datums/status_effects/debuffs/debuffs.dm:340
public partial class datum_status_effect_grouped_stasis : datum_status_effect_grouped
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:346
    public virtual void update_time_of_death()
    {
        if (Dm.truthy(last_dead_time))
        {
            float delta = Dm.num(world.time) - Dm.num(last_dead_time);
            float new_timeofdeath = owner.timeofdeath + delta;
            owner.timeofdeath = new_timeofdeath;
            owner.station_timestamp_timeofdeath = round_timestamp(wtime: new_timeofdeath);
            last_dead_time = null;
        }
        if (owner.stat == DEAD)
            last_dead_time = world.time;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:356  (arg 2 is the source: grouped's on_creation takes it)
    [DmParams("new_owner", "set_duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object dot = await base.on_creation(new_owner, __extra);
        if (Dm.truthy(dot))
        {
            update_time_of_death();
            if (owner.reagents != null)
                await ((datum_reagents)owner.reagents).end_metabolization(owner as mob_living_carbon, FALSE);
        }
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:362
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;
        owner.add_traits(Dm.list(TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED, TRAIT_STASIS), TRAIT_STATUS_EFFECT(id));
        owner.add_filter("stasis_status_ripple", 2, Dm.list(Dm.kv("type", "ripple"), Dm.kv("flags", WAVE_BOUNDED), Dm.kv("radius", 0), Dm.kv("size", 2)));
        object filter = owner.get_filter("stasis_status_ripple");
        animate(filter, Dm.named("radius", 0), Dm.named("time", 0.2f * SECONDS), Dm.named("size", 2), Dm.named("easing", JUMP_EASING), Dm.named("loop", -1), Dm.named("flags", ANIMATION_PARALLEL));
        animate(Dm.named("radius", 32), Dm.named("time", 1.5f * SECONDS), Dm.named("size", 0));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:372
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        update_time_of_death();
        return null;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:375
    public override async DmTask<object> on_remove()
    {
        owner.remove_traits(Dm.list(TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED, TRAIT_STASIS), TRAIT_STATUS_EFFECT(id));
        owner.remove_filter("stasis_status_ripple");
        update_time_of_death();
        return await base.on_remove();
    }
}

// Gaps P3: the spastic mutation's muscle spasms; the var block is generated.
// tg: code/datums/status_effects/debuffs/debuffs.dm:740
public partial class datum_status_effect_spasms : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:745
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (Dm.truthy(owner.incapacitated) || IS_UNCONSCIOUS(owner) || HAS_TRAIT(owner, TRAIT_HANDS_BLOCKED) || HAS_TRAIT(owner, TRAIT_IMMOBILIZED))
            return null;
        if (!prob(15))
            return null;
        switch (rand(1, 5))
        {
            case 1:
                if (Dm.truthy(Dm.bitand(owner.mobility_flags, MOBILITY_MOVE)) && isturf(owner.loc))
                {
                    to_chat(owner, span_warning(I18n.T("Your leg spasms!")));
                    await step(owner, Dm.num(pick(GLOB.cardinals)));
                }
                break;
            case 2:
            {
                obj_item held_item = owner.get_active_held_item() as obj_item;
                if (held_item == null)
                    return null;
                to_chat(owner, span_warning(I18n.T("Your fingers spasm!")));
                owner.log_message(Dm.text("used [] due to a Muscle Spasm", held_item), LOG_ATTACK);
                await held_item.attack_self(owner);
                break;
            }
            case 3:
            {
                owner.set_combat_mode(TRUE);

                float range = 1;
                if (owner.get_active_held_item() is obj_item_gun) //get targets to shoot at
                    range = 7;

                DmList targets = Dm.list();
                foreach (mob_living nearby_mobs in Dm.each<mob_living>(oview(range, owner)))
                    Dm.append(ref targets, nearby_mobs);
                if (Dm.truthy(LAZYLEN(targets)))
                {
                    to_chat(owner, span_warning(I18n.T("Your arm spasms!")));
                    owner.log_message(" attacked someone due to a Muscle Spasm", LOG_ATTACK); //the following attack will log itself
                    await owner.ClickOn(pick(targets) as atom);
                }
                owner.set_combat_mode(FALSE);
                break;
            }
            case 4:
                owner.set_combat_mode(TRUE);
                to_chat(owner, span_warning(I18n.T("Your arm spasms!")));
                owner.log_message(Dm.text("attacked []self to a Muscle Spasm", owner.p_them()), LOG_ATTACK);
                await owner.ClickOn(owner);
                owner.set_combat_mode(FALSE);
                break;
            case 5:
            {
                obj_item held_item = owner.get_active_held_item() as obj_item;
                DmList targets = Dm.list();
                foreach (turf nearby_turfs in Dm.each<turf>(oview(3, owner)))
                    Dm.append(ref targets, nearby_turfs);
                if (Dm.truthy(LAZYLEN(targets)) && held_item != null)
                {
                    to_chat(owner, span_warning(I18n.T("Your arm spasms!")));
                    owner.log_message(Dm.text("threw [] due to a Muscle Spasm", held_item), LOG_ATTACK);
                    await owner.throw_item(pick(targets) as atom);
                }
                break;
            }
        }
        return null;
    }
}

// M8 slice E: the convulsions of a defibrillator's disarming shock (defib.dm:539); the var blocks are generated.
// tg: code/datums/status_effects/debuffs/debuffs.dm:793
public partial class datum_status_effect_convulsing : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:800
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        mob_living zappy_boy = new_owner;
        object dot = await base.on_creation(zappy_boy, __extra);
        to_chat(zappy_boy, span_boldwarning(I18n.T("You feel a shock moving through your body! Your hands start shaking!")));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:804
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        mob_living_carbon H = owner as mob_living_carbon;
        if (prob(40))
        {
            obj_item I = H.get_active_held_item() as obj_item;
            if (I != null && Dm.truthy(await H.dropItemToGround(I)))
            {
                H.visible_message(
                    span_notice(I18n.T("[]'s hand convulses, and they drop their []!", H, I.name)),
                    span_userdanger(I18n.T("Your hand convulses violently, and you drop what you were holding!")));
                await H.adjust_jitter(10 * SECONDS);
            }
        }
        return null;
    }
}

// tg: code/datums/status_effects/debuffs/debuffs.dm:1149
public partial class datum_status_effect_careful_driving : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:1155
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_careful_driving), update: TRUE);
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:1159
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_careful_driving), update: TRUE);
        return dot;
    }
}

// The syndicate campaign G2 (D316): the trance of the hypnotic flash and grenade; the next words heard become a hypnosis.
// tg: code/datums/status_effects/debuffs/debuffs.dm:681
public partial class datum_status_effect_trance : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:694
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (Dm.truthy(stun))
            await owner.Stun(6 * SECONDS, TRUE);
        await owner.set_dizzy(40 * SECONDS);
        return null;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:699
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!iscarbon(owner))
            return FALSE;
        RegisterSignal(owner, COMSIG_MOVABLE_HEAR, nameof(hypnotize));
        ADD_TRAIT(owner, TRAIT_MUTE, TRAIT_STATUS_EFFECT(id));
        owner.add_client_colour(typeof(datum_client_colour_monochrome), REF(this));
        owner.visible_message(Dm.text("[]", Dm.truthy(stun) ? span_warning(I18n.T("[] stands still as [] eyes seem to focus on a distant point.", owner, owner.p_their())) : ""),
            span_warning((string)pick(I18n.T("You feel your thoughts slow down..."), I18n.T("You suddenly feel extremely dizzy..."), I18n.T("You feel like you're in the middle of a dream..."), I18n.T("You feel incredibly relaxed...")) as string));
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:709
    [DmParams("new_owner", "_duration", "_stun")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object _duration = Dm.arg(__extra, 0);
        object _stun = Dm.arg(__extra, 1) ?? TRUE;
        duration = Dm.num_or_null(_duration);
        stun = Dm.num(_stun);
        return await base.on_creation(new_owner, Dm.forward(__extra, _duration, _stun));
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:714
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_MOVABLE_HEAR);
        REMOVE_TRAIT(owner, TRAIT_MUTE, TRAIT_STATUS_EFFECT(id));
        owner.remove_status_effect(typeof(datum_status_effect_dizziness));
        owner.remove_client_colour(REF(this));
        to_chat(owner, span_warning(I18n.T("You snap out of your trance!")));
        return null;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:721
    public override object get_examine_text(mob examiner = null)
    {
        return span_warning(I18n.T("[] seem[] slow and unfocused.", owner.p_They(), owner.p_s()));
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:724
    [SignalHandler]
    public virtual void hypnotize(datum source = null, DmList hearing_args = null)
    {
        if (HAS_TRAIT(owner, TRAIT_DEAF) || owner == hearing_args[HEARING_SPEAKER])
            return;

        mob hearing_speaker = hearing_args[HEARING_SPEAKER] as mob;
        mob_living_carbon C = (mob_living_carbon)owner;
        C.cure_trauma_type(typeof(datum_brain_trauma_hypnosis), TRAUMA_RESILIENCE_SURGERY); //clear previous hypnosis
        // The brain trauma itself does its own set of logging, but this is the only place the source of the hypnosis phrase can be found.
        hearing_speaker.log_message(Dm.text("hypnotised [] with the phrase '[]'", key_name(C), hearing_args[HEARING_RAW_MESSAGE]), LOG_ATTACK, color: "red");
        C.log_message(Dm.text("has been hypnotised by the phrase '[]' spoken by []", hearing_args[HEARING_RAW_MESSAGE], key_name(hearing_speaker)), LOG_VICTIM, color: "orange", log_globally: FALSE);
        _addtimer(Dm.@new<datum_callback>(C, nameof(mob_living_carbon.gain_trauma), typeof(datum_brain_trauma_hypnosis), TRAUMA_RESILIENCE_SURGERY, hearing_args[HEARING_RAW_MESSAGE]), 1 * SECONDS, file: "code/datums/status_effects/debuffs/debuffs.dm", line: 736);
        _addtimer(Dm.@new<datum_callback>(C, nameof(mob_living.Stun), 60, TRUE, TRUE), 15, file: "code/datums/status_effects/debuffs/debuffs.dm", line: 737); //Take some time to think about it
        qdel(this);
    }
}

// Syndicate G4c (D316): His Grace's wrath.
// tg: code/datums/status_effects/debuffs/debuffs.dm:388
public partial class datum_status_effect_his_wrath : datum_status_effect //does minor damage over time unless holding His Grace
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:394
    [DmParams("new_owner", "His", "Him")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object His = Dm.arg(__extra, 0);
        object Him = Dm.arg(__extra, 1);
        object dot = await base.on_creation(new_owner, __extra);
        linked_alert.name = Dm.text("[] Wrath", His);
        linked_alert.desc = Dm.text("You fled from [] Grace instead of feeding [], and now you suffer.", His, Him);
        linked_alert.icon_state = Dm.text("[]_grace", lowertext(Dm.text("[]", His)));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:406
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        float seconds = Dm.num(seconds_between_ticks);
        foreach (obj_item_his_grace HG in Dm.each<obj_item_his_grace>(owner.held_items))
        {
            qdel(this);
            return null;
        }
        object need_mob_update;
        need_mob_update = await owner.adjust_brute_loss(0.04f * seconds, updating_health: FALSE);
        need_mob_update = Dm.add(need_mob_update, await owner.adjust_fire_loss(0.04f * seconds, updating_health: FALSE));
        need_mob_update = Dm.add(need_mob_update, await owner.adjust_tox_loss(0.08f * seconds, updating_health: FALSE, forced: TRUE));
        if (Dm.truthy(need_mob_update))
            await owner.updatehealth();
        return null;
    }
}

// tg: code/datums/status_effects/debuffs/debuffs.dm:1073  (syndicate X3b: hunter boxing's crit)
public partial class datum_status_effect_rebuked : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:1082
    public override DmTask<object> on_apply(params object[] __extra)
    {
        owner.next_move_modifier *= 2;
        if (owner is mob_living_simple_animal_hostile simple_owner) // ishostile
            simple_owner.ranged_cooldown_time *= cd_increase;
        return DmTask<object>.FromResult(TRUE);
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:1089
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        if (QDELETED(owner))
            return dot;
        owner.next_move_modifier *= 0.5f;
        if (owner is mob_living_simple_animal_hostile simple_owner) // ishostile
            simple_owner.ranged_cooldown_time /= cd_increase;
        return dot;
    }
}

// Gaps P3 (D327): genetic breakdown at zero stability, and the meltdown that phases its victim away.
// tg: code/datums/status_effects/debuffs/debuffs.dm:821
public partial class datum_status_effect_dna_melt : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:828
    [DmParams("new_owner", "set_duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object dot = await base.on_creation(new_owner, __extra);
        to_chat(new_owner, span_boldwarning(I18n.T("My body can't handle the mutations! I need to get my mutations removed fast!")));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:832
    public override async DmTask<object> on_remove()
    {
        if (!ishuman(owner))
        {
            await owner.gib(DROP_ALL_REMAINS); //fuck you in particular
            return null;
        }
        mob_living_carbon_human H = owner as mob_living_carbon_human;
        Waiting.Detach(H.something_horrible(kill_either_way)); // INVOKE_ASYNC
        return null;
    }
}

// tg: code/datums/status_effects/debuffs/debuffs.dm:845
public partial class datum_status_effect_go_away : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:853
    [DmParams("new_owner", "set_duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object dot = await base.on_creation(new_owner, __extra);
        direction = pick(NORTH, SOUTH, EAST, WEST);
        await new_owner.setDir(Dm.num(direction));
        owner.add_traits(Dm.list(TRAIT_NO_FLOATING_ANIM, TRAIT_MOVE_FLYING), TRAIT_STATUS_EFFECT(id)); //I believe I can fly!
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:859
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        await owner.AdjustStun(1, ignore_canstun: TRUE);
        turf turf = get_step(owner, Dm.num(direction));
        if (turf == null)
        {
            qdel(this);
            return null;
        }
        await owner.forceMove(turf);
        return null;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:867
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        owner.remove_traits(Dm.list(TRAIT_NO_FLOATING_ANIM, TRAIT_MOVE_FLYING), TRAIT_STATUS_EFFECT(id));
        return dot;
    }
}

///Subtype of the go away effect (phases the mob in one direction) that deletes the owner on z-level change or when the time's up.
// tg: code/datums/status_effects/debuffs/debuffs.dm:872
public partial class datum_status_effect_go_away_deletes_mob : datum_status_effect_go_away
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:878
    [DmParams("new_owner", "set_duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object dot = await base.on_creation(new_owner, __extra);
        deletion_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(wipe_bozo)), duration - 5 * SECONDS, TIMER_STOPPABLE | TIMER_DELETE_ME, file: "code/datums/status_effects/debuffs/debuffs.dm", line: 880);
        ADD_TRAIT(owner, TRAIT_IMMOBILIZED, REF(this));
        RegisterSignals(new_owner, Dm.list(COMSIG_MOVABLE_Z_CHANGED, COMSIG_LIVING_DEATH), nameof(wipe_bozo));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:884
    [SignalHandler]
    public virtual void wipe_bozo(params object[] __extra)
    {
        deltimer(deletion_timer);
        owner.fade_into_nothing();
        UnregisterSignal(owner, Dm.list(COMSIG_MOVABLE_Z_CHANGED, COMSIG_LIVING_DEATH));
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:889
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        deltimer(deletion_timer);
        REMOVE_TRAIT(owner, TRAIT_IMMOBILIZED, REF(this));
        UnregisterSignal(owner, Dm.list(COMSIG_MOVABLE_Z_CHANGED, COMSIG_LIVING_DEATH));
        return dot;
    }
}

// Gaps P3 (D327): cryokinesis's bolt.
// tg: code/datums/status_effects/debuffs/debuffs.dm:1098
public partial class datum_status_effect_freezing_blast : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:1109
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_freezing_blast), update: TRUE);
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:1113
    public override async DmTask<object> on_remove()
    {
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_freezing_blast), update: TRUE);
        return null;
    }
}

// tg: code/datums/status_effects/debuffs/debuffs.dm:1119  (gaps B1: the discoordination trauma; its alert is a var block, generated)
public partial class datum_status_effect_discoordinated : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:1130
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        ADD_TRAIT(owner, TRAIT_DISCOORDINATED_TOOL_USER, TRAIT_STATUS_EFFECT(id));
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:1134
    public override async DmTask<object> on_remove()
    {
        REMOVE_TRAIT(owner, TRAIT_DISCOORDINATED_TOOL_USER, TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }
}

// tg: code/datums/status_effects/debuffs/debuffs.dm:1246  (gaps GU1: the .38 flare round's mark, beams.dm flare; its alert is a var
// block, generated)
public partial class datum_status_effect_designated_target : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:1254
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        mob_flare = owner.mob_light(3, 15, LIGHT_COLOR_FLARE) as obj_effect_dummy_lighting_obj_moblight;
        ADD_TRAIT(owner, TRAIT_DESIGNATED_TARGET, id);
        owner.add_filter("designated_target", 3, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", COLOR_RED), Dm.kv("size", 1)));
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:1260
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        // If we are ever wet, remove our flare status effect
        datum_status_effect_fire_handler_wet_stacks splashed_with_water = Dm.locate<datum_status_effect_fire_handler_wet_stacks>(owner.status_effects);
        if (splashed_with_water is datum_status_effect_fire_handler_wet_stacks)
            qdel(this);
        return null;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:1266
    public override async DmTask<object> on_remove()
    {
        QDEL_NULL(ref mob_flare);
        owner.remove_filter("designated_target");
        REMOVE_TRAIT(owner, TRAIT_DESIGNATED_TARGET, id);
        return null;
    }
}

///Maddly teleports the victim around all of space for 10 seconds
// tg: code/datums/status_effects/debuffs/debuffs.dm:1139  (gaps E1a: the big bluespace anomaly's bump)
public partial class datum_status_effect_teleport_madness : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:1146
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        dump_in_space(owner);
        return null;
    }
}

// tg: code/datums/status_effects/debuffs/debuffs.dm:901
public partial class datum_status_effect_fake_virus : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:909
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (HAS_TRAIT(owner, TRAIT_VIRUSIMMUNE))
            return FALSE;
        if (IS_UNCONSCIOUS_OR_CRIT(owner))
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:916
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        object fake_msg = "";
        string fake_emote = "";
        if (msg_stage <= 300)
        {
            if (prob(1))
                fake_msg = pick(
                    span_warning((string)pick(I18n.T("Your head hurts."), I18n.T("Your head pounds."))),
                    span_warning((string)pick(I18n.T("You're having difficulty breathing."), I18n.T("Your breathing becomes heavy."))),
                    span_warning((string)pick(I18n.T("You feel dizzy."), I18n.T("Your head spins."))),
                    span_warning((string)pick(I18n.T("You swallow excess mucus."), I18n.T("You lightly cough."))),
                    span_warning((string)pick(I18n.T("Your head hurts."), I18n.T("Your mind blanks for a moment."))),
                    span_warning((string)pick(I18n.T("Your throat hurts."), I18n.T("You clear your throat."))));
        }
        else if (msg_stage >= 301 && msg_stage <= 600)
        {
            if (prob(2))
                fake_msg = pick(
                    span_warning((string)pick(I18n.T("Your head hurts a lot."), I18n.T("Your head pounds incessantly."))),
                    span_warning((string)pick(I18n.T("Your windpipe feels like a straw."), I18n.T("Your breathing becomes tremendously difficult."))),
                    span_warning(I18n.T("You feel very [].", pick(I18n.T("dizzy"), I18n.T("woozy"), I18n.T("faint")))),
                    span_warning((string)pick(I18n.T("You hear a ringing in your ear."), I18n.T("Your ears pop."))),
                    span_warning(I18n.T("You nod off for a moment.")));
        }
        else
        {
            if (prob(3))
            {
                if (prob(50))// coin flip to throw a message or an emote
                    fake_msg = pick(
                        span_userdanger((string)pick(I18n.T("Your head hurts!"), I18n.T("You feel a burning knife inside your brain!"), I18n.T("A wave of pain fills your head!"))),
                        span_userdanger((string)pick(I18n.T("Your lungs hurt!"), I18n.T("It hurts to breathe!"))),
                        span_warning((string)pick(I18n.T("You feel nauseated."), I18n.T("You feel like you're going to throw up!"))));
                else
                {
                    if (prob(40))
                        fake_emote = "cough";
                    else
                        fake_emote = "sneeze";
                }
            }
        }

        if (Dm.truthy(fake_emote))
            await owner.emote(fake_emote);
        else if (Dm.truthy(fake_msg))
            to_chat(owner, fake_msg);

        msg_stage++;
        return null;
    }
}

// Gaps RG2 (D339): the necropolis curse (helbital's overdose and its game of rock, paper, scissors with death) and its wasting
// effect's visual.

// tg: code/datums/status_effects/debuffs/debuffs.dm:584 (interleaved types, rules 1.9)
public partial class mob_living : mob
{
    /// Applies a curse with various possible effects
    // tg: code/datums/status_effects/debuffs/debuffs.dm:584
    public virtual async DmTask<object> apply_necropolis_curse(object set_curse = null)
    {
        datum_status_effect_necropolis_curse curse = has_status_effect(typeof(datum_status_effect_necropolis_curse)) as datum_status_effect_necropolis_curse;
        if (!Dm.truthy(set_curse))
            set_curse = pick(CURSE_BLINDING, CURSE_WASTING, CURSE_GRASPING);
        if (QDELETED(curse))
        {
            await apply_status_effect(typeof(datum_status_effect_necropolis_curse), set_curse);
        }
        else
        {
            curse.apply_curse(set_curse);
            curse.duration = Dm.num(curse.duration) + 5 * MINUTES; //time added by additional curses
        }
        return curse;
    }
}

/// A curse that does up to three nasty things to you
// tg: code/datums/status_effects/debuffs/debuffs.dm:597
public partial class datum_status_effect_necropolis_curse : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:610
    [DmParams("new_owner", "set_curse")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object set_curse = Dm.arg(__extra, 0);
        object dot = await base.on_creation(new_owner, __extra);
        if (Dm.truthy(dot))
            apply_curse(set_curse);
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:615
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(wasting_effect))
        {
            qdel(wasting_effect);
            wasting_effect = null;
        }
        return base.Destroy(force);
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:621
    public override async DmTask<object> on_remove()
    {
        remove_curse(curse_flags);
        return null;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:624
    public virtual void apply_curse(object set_curse = null)
    {
        curse_flags = Dm.bitor(curse_flags, Dm.num(set_curse));
        if (Dm.truthy(Dm.bitand(curse_flags, CURSE_BLINDING)))
            owner.overlay_fullscreen("curse", typeof(atom_movable_screen_fullscreen_curse), 1);
        if (Dm.truthy(Dm.bitand(curse_flags, CURSE_WASTING)) && wasting_effect == null)
            wasting_effect = Dm.@new<obj_effect_temp_visual_curse>();
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:631
    public virtual void remove_curse(object remove_curse = null)
    {
        if (Dm.truthy(Dm.bitand(remove_curse, CURSE_BLINDING)))
            owner.clear_fullscreen("curse", 50);
        curse_flags = Dm.bitand(curse_flags, Dm.bitnot(Dm.num(remove_curse)));
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:636
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (owner.stat == DEAD)
            return null;

        if (Dm.truthy(Dm.bitand(curse_flags, CURSE_WASTING)))
        {
            await wasting_effect.forceMove(owner.loc);
            await wasting_effect.setDir(owner.dir);
            wasting_effect.transform = owner.transform; //if the owner has been stunned the overlay should inherit that position
            wasting_effect.alpha = 255;
            Dm.animate(wasting_effect, Dm.named("alpha", 0), Dm.named("time", 32));
            playsound(owner, Dm.resource("sound/effects/curse/curse5.ogg"), 20, TRUE, -1);
            await owner.adjust_fire_loss(0.75f);
        }

        if (Dm.truthy(Dm.bitand(curse_flags, CURSE_GRASPING)))
        {
            if (effect_next_activation > world.time)
                return null;
            effect_next_activation = world.time + effect_cooldown;
            await fire_curse_hand(owner as mob_living_carbon, range: 5, projectile_type: typeof(obj_projectile_curse_hand)); // This one stuns people
        }
        return null;
    }
}

// Gaps RG3 (D339): the ants covering someone (the ants reagent's splash, other_reagents.dm:3281).

//Deals with ants covering someone.
// tg: code/datums/status_effects/debuffs/debuffs.dm:957
public partial class datum_status_effect_ants : datum_status_effect
{
    /// Common phrases people covered in ants scream
    // tg: code/datums/status_effects/debuffs/debuffs.dm:968  var/static/list/ant_debuff_speech (rules 10.6)
    public static DmList ant_debuff_speech => TgState.Current.datum_status_effect_ants__ant_debuff_speech ??= Dm.list(
        "GET THEM OFF ME!!",
        "OH GOD THE ANTS!!",
        "MAKE IT END!!",
        "THEY'RE EVERYWHERE!!",
        "GET THEM OFF!!",
        "SOMEBODY HELP ME!!"
    );

    // tg: code/datums/status_effects/debuffs/debuffs.dm:978
    [DmParams("new_owner", "amount_left")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object amount_left = Dm.arg(__extra, 0);
        if (isnum(amount_left) && new_owner.stat < HARD_CRIT)
        {
            if (!IS_UNCONSCIOUS(new_owner)) // Unconscious people won't get messages
                to_chat(new_owner, span_userdanger(I18n.T("You're covered in ants!")));
            ants_remaining = Dm.num(ants_remaining) + Dm.num(amount_left);
            RegisterSignal(new_owner, COMSIG_COMPONENT_CLEAN_ACT, nameof(ants_washed));
        }
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:986
    [DmParams("effect", "amount_left")]
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        object amount_left = Dm.arg(__extra, 0);
        var victim = owner as mob_living_carbon_human;
        if (isnum(amount_left) && Dm.num(ants_remaining) >= 1 && victim.stat < HARD_CRIT)
        {
            if (!IS_UNCONSCIOUS(victim)) // Unconscious people won't get messages
            {
                if (prob(99))
                    to_chat(victim, span_userdanger(I18n.T("You're covered in MORE ants!")));
                else
                    Waiting.Detach(victim.say(I18n.T("AAHH! THIS SITUATION HAS ONLY BEEN MADE WORSE WITH THE ADDITION OF YET MORE ANTS!!"), forced: typeof(datum_status_effect_ants))); // INVOKE_ASYNC
            }
            ants_remaining = Dm.num(ants_remaining) + Dm.num(amount_left);
        }
        await base.refresh(effect, __extra);
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:997
    public override async DmTask<object> on_remove()
    {
        ants_remaining = 0;
        to_chat(owner, span_notice(I18n.T("All of the ants are off of your body!")));
        UnregisterSignal(owner, COMSIG_COMPONENT_CLEAN_ACT);
        return await base.on_remove();
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:1003
    [SignalHandler]
    public virtual object ants_washed(datum source = null, params object[] __extra)
    {
        object dot = NONE;

        if (Dm.truthy(owner.remove_status_effect(typeof(datum_status_effect_ants))))
            return Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP);
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:1011
    public override object get_examine_text(mob examiner = null)
    {
        return span_warning(I18n.T("[] [] covered in ants!", owner.p_They(), owner.p_are()));
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:1014
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        var victim = owner as mob_living_carbon_human;
        float spt = Dm.num(seconds_between_ticks);
        await victim.apply_damage(max(0.1f, round((Dm.num(ants_remaining) * Dm.num(damage_per_ant)), 0.1f)) * spt, BRUTE, spread_damage: TRUE); //Scales with # of ants (lowers with time). Roughly 10 brute over 50 seconds.
        if (!IS_UNCONSCIOUS_OR_CRIT(victim)) //Makes sure people don't scratch at themselves while they're in a critical condition
        {
            if (prob(15))
            {
                float sw__1 = rand(1, 2);
                if (sw__1 == 1)
                    await victim.say(pick(ant_debuff_speech), forced: typeof(datum_status_effect_ants));
                else if (sw__1 == 2)
                    await victim.emote("scream");
            }
            if (prob(50)) // Most of the damage is done through random chance. When tested yielded an average 100 brute with 200u ants.
            {
                float sw__2 = rand(1, 50);
                if (sw__2 >= 1 && sw__2 <= 8) //16% Chance
                {
                    to_chat(victim, span_danger(I18n.T("You scratch at the ants on your scalp!.")));
                    await owner.apply_damage(0.4f * spt, BRUTE, BODY_ZONE_HEAD);
                }
                else if (sw__2 >= 9 && sw__2 <= 29) //40% chance
                {
                    to_chat(victim, span_danger(I18n.T("You scratch at the ants on your arms!")));
                    await owner.apply_damage(1.2f * spt, BRUTE, pick(GLOB.arm_zones));
                }
                else if (sw__2 >= 30 && sw__2 <= 49) //38% chance
                {
                    to_chat(victim, span_danger(I18n.T("You scratch at the ants on your leg!")));
                    await owner.apply_damage(1.2f * spt, BRUTE, pick(GLOB.leg_zones));
                }
                else if (sw__2 == 50) // 2% chance
                {
                    to_chat(victim, span_danger(I18n.T("You rub some ants away from your eyes!")));
                    await victim.set_eye_blur_if_lower(6 * SECONDS);
                    ants_remaining = Dm.num(ants_remaining) - 5; // To balance out the blindness, it'll be a little shorter.
                }
            }
        }
        ants_remaining = Dm.num(ants_remaining) - 1;
        if (Dm.num(ants_remaining) <= 0 || victim.stat >= HARD_CRIT)
            qdel(this); //If this person has no more ants on them or are dead, they are no longer affected.
        return null;
    }
}

// tg: code/datums/status_effects/debuffs/debuffs.dm:656
public partial class obj_effect_temp_visual_curse : obj_effect_temp_visual
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:658
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        deltimer(timerid);
        return dot;
    }
}

// tg: code/datums/status_effects/debuffs/debuffs.dm:663
public partial class datum_status_effect_gonbola_pacify : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:669
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        owner.add_traits(Dm.list(TRAIT_PACIFISM, TRAIT_MUTE), REF(this));
        await owner.add_mood_event(REF(this), typeof(datum_mood_event_gondola));
        to_chat(owner, span_notice(I18n.T("You suddenly feel at peace and feel no need to make any sudden or rash actions...")));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:675
    public override async DmTask<object> on_remove()
    {
        owner.remove_traits(Dm.list(TRAIT_PACIFISM, TRAIT_MUTE), REF(this));
        owner.clear_mood_event(REF(this));
        return await base.on_remove();
    }
}

// tg: code/datums/status_effects/debuffs/debuffs.dm:1050
public partial class atom_movable_screen_alert_status_effect_ants : atom_movable_screen_alert_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:1050
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;
        var living = owner as mob_living;
        if (!istype(living, typeof(mob_living)) || !Dm.truthy(living.can_resist()) || living != owner)
            return dot;
        to_chat(living, span_notice(I18n.T("You start to shake the ants off!")));
        if (!Dm.truthy(await do_after(living, 2 * SECONDS, target: living)))
            return dot;
        foreach (datum_status_effect_ants ant_covered in Dm.each<datum_status_effect_ants>(living.status_effects))
        {
            to_chat(living, span_notice(I18n.T("You manage to get some of the ants off!")));
            ant_covered.ants_remaining = Dm.num(ant_covered.ants_remaining) - 10; // 5 Times more ants removed per second than just waiting in place
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:968  /datum/status_effect/ants's var/static/list/ant_debuff_speech
    internal DmList datum_status_effect_ants__ant_debuff_speech;
}

// tg: code/datums/status_effects/debuffs/debuffs.dm:429
public partial class datum_status_effect_crusher_mark : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/debuffs.dm:445
    [DmParams("new_owner", "was_boosted", "crusher")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object was_boosted = Dm.arg(__extra, 0);
        obj_item_kinetic_crusher crusher = Dm.arg<obj_item_kinetic_crusher>(__extra, 1);
        object dot = await base.on_creation(new_owner, __extra);
        boosted = Dm.num(was_boosted);
        this.crusher = crusher;
        if (crusher != null)
            RegisterSignal(crusher, COMSIG_QDELETING, nameof(on_crusher_destroyed));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:452
    [SignalHandler]
    public virtual void on_crusher_destroyed(datum source = null)
    {
        crusher = null;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:456
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (owner.mob_size < MOB_SIZE_LARGE)
            return FALSE;

        marked_underlay = Dm.@new<obj_effect_abstract_crusher_mark>();
        marked_underlay.pixel_w = -(owner.base_pixel_x + owner.base_pixel_w);
        marked_underlay.pixel_z = -(owner.base_pixel_y + owner.base_pixel_z);
        marked_underlay.transform = Dm.mul(marked_underlay.transform, 0.5f);
        Dm.append(owner.vis_contents, marked_underlay);
        animate(marked_underlay, ready_delay, Dm.named("transform", Dm.mul(Dm.matrix(), 1.2f)), Dm.named("flags", Dm.bitor(CIRCULAR_EASING, EASE_IN)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(scale_back)), ready_delay, file: "code/datums/status_effects/debuffs/debuffs.dm", line: 466); // Animate chains do not function with vis_contents
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:469
    public virtual void scale_back()
    {
        animate(marked_underlay, Dm.named("time", 0.1f * SECONDS), Dm.named("transform", Dm.matrix()), Dm.named("flags", Dm.bitor(CUBIC_EASING, EASE_OUT)));
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:472
    public override object Destroy(float? force = null)
    {
        if (owner != null)
            Dm.remove(owner.underlays, marked_underlay);
        QDEL_NULL(ref marked_underlay);
        crusher = null;
        return base.Destroy(force);
    }

    // tg: code/datums/status_effects/debuffs/debuffs.dm:479
    public virtual async DmTask detonate(obj_item_kinetic_crusher used_crusher = null, mob_living user = null, float? melee_hit = null)
    {
        melee_hit ??= TRUE;
        datum_status_effect_crusher_damage crusher_damage_effect = (owner.has_status_effect(typeof(datum_status_effect_crusher_damage)) ?? await owner.apply_status_effect(typeof(datum_status_effect_crusher_damage))) as datum_status_effect_crusher_damage;
        float target_health = owner.health;
        float combined_damage = detonation_damage;
        if (used_crusher != null)
        {
            foreach (obj_item_crusher_trophy crusher_trophy in Dm.each<obj_item_crusher_trophy>(used_crusher.trophies))
                combined_damage += Dm.num(await crusher_trophy.on_mark_detonation(owner, user, used_crusher));
        }

        // Trophy effects kill the target, abort
        if (QDELETED(owner))
            return;

        if (!QDELETED(crusher_damage_effect))
            crusher_damage_effect.total_damage += target_health - owner.health; // We did some damage, but let's not assume how much we did

        Dm.@new<obj_effect_temp_visual_kinetic_blast>(get_turf(owner));
        bool backstabbed = false;
        object def_check = owner.getarmor(type: BOMB);

        // Backstab bonus, only if a crusher was used to detonate the mark
        if (Dm.truthy(melee_hit) && used_crusher != null && (Dm.truthy(boosted) || (user != null && Dm.truthy(check_behind(user, owner)) && !HAS_TRAIT(owner, TRAIT_BACKSTAB_IMMUNE))))
        {
            backstabbed = true;
            combined_damage += used_crusher.backstab_bonus;
            playsound(user, used_crusher.backstab_sound, 100, TRUE);
        }

        if (!QDELETED(crusher_damage_effect))
            crusher_damage_effect.total_damage += combined_damage;

        if (user != null && Dm.truthy(user._listen_lookup?[COMSIG_LIVING_CRUSHER_DETONATE]))
            user._SendSignal(COMSIG_LIVING_CRUSHER_DETONATE, Dm.list(user, owner, used_crusher, backstabbed ? TRUE : FALSE));
        await owner.apply_damage(combined_damage, BRUTE, blocked: Dm.num(def_check));
        if (!QDELETED(owner))
            owner.remove_status_effect(this);
    }
}
