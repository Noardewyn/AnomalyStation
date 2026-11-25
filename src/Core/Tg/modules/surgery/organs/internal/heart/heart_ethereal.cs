// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/heart/heart_ethereal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1, D339, D341): the ethereal's crystal core and its resurrection crystal. It works in any carbon (the species
// sets its colour). The var blocks are generated (D111).

// tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:1
public partial class obj_item_organ_heart_ethereal : obj_item_organ_heart
{
    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(set_ethereal_color(ethereal_color));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:24
    public virtual async DmTask set_ethereal_color(object new_color = null)
    {
        ethereal_color = Dm.str(new_color);
        await add_atom_colour(ethereal_color, FIXED_COLOUR_PRIORITY);
        update_appearance();
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:29
    [DmParams("heart_owner", "special", "movement_flags")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        special ??= FALSE;
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);

        RegisterSignal(organ_owner, COMSIG_MOB_STATCHANGE, nameof(on_stat_change));
        RegisterSignal(organ_owner, COMSIG_LIVING_POST_FULLY_HEAL, nameof(on_owner_fully_heal));
        RegisterSignal(organ_owner, COMSIG_QDELETING, nameof(owner_deleted));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:36
    [DmParams("heart_owner", "special", "movement_flags")]
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon heart_owner = organ_owner as mob_living_carbon;
        UnregisterSignal(heart_owner, Dm.list(COMSIG_MOB_STATCHANGE, COMSIG_LIVING_POST_FULLY_HEAL, COMSIG_QDELETING));
        REMOVE_TRAIT(heart_owner, TRAIT_CORPSELOCKED, SPECIES_TRAIT);
        stop_crystalization_process(heart_owner);
        QDEL_NULL(ref current_crystal);
        await base.on_mob_remove(organ_owner, special, movement_flags);
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:43
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, mutable_appearance(icon, icon_state: Dm.text("[]_overlay-[]", base_icon_state, Dm.truthy(beating) ? "on" : "off"), appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:47
    [SignalHandler]
    public virtual void on_owner_fully_heal(mob_living_carbon healed = null, object heal_flags = null)
    {
        QDEL_NULL(ref current_crystal); //Kicks out the ethereal
    }

    ///Ran when examined while crystalizing, gives info about the amount of time left
    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:53
    [SignalHandler]
    public virtual void on_examine(mob_living_carbon_human examined_human = null, mob user = null, DmList examine_list = null)
    {
        if (!Dm.truthy(crystalize_timer_id))
            return;

        float sw__1 = Dm.num(timeleft(crystalize_timer_id));
        if (sw__1 >= 0 && sw__1 <= CRYSTALIZE_STAGE_ENGULFING)
            Dm.append(examine_list, span_warning(I18n.T("Crystals are almost engulfing []! ", examined_human)));
        else if (sw__1 >= CRYSTALIZE_STAGE_ENGULFING && sw__1 <= CRYSTALIZE_STAGE_ENCROACHING)
            Dm.append(examine_list, span_notice(I18n.T("Crystals are starting to cover []. ", examined_human)));
        else if (sw__1 >= CRYSTALIZE_STAGE_SMALL)
            Dm.append(examine_list, span_notice(I18n.T("Some crystals are coming out of []. ", examined_human)));
    }

    ///On stat changes, if the victim is no longer dead but they're crystalizing, cancel it, if they become dead, start the crystalizing process if possible
    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:68
    [SignalHandler]
    public virtual void on_stat_change(mob_living victim = null, object new_stat = null)
    {
        if (!Dm.eq(new_stat, DEAD))
        {
            if (Dm.truthy(crystalize_timer_id))
                stop_crystalization_process(victim);
            return;
        }

        if (QDELETED(victim) || HAS_TRAIT(victim, TRAIT_SUICIDED))
            return; //lol rip

        if (!(crystalize_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
            return; //lol double rip

        if (HAS_TRAIT(victim, TRAIT_CANNOT_CRYSTALIZE))
            return; // no reviving during mafia, or other inconvenient times.

        to_chat(victim, span_nicegreen(I18n.T("Crystals start forming around your dead body.")));
        victim.visible_message(span_notice(I18n.T("Crystals start forming around [].", victim)), ignored_mobs: victim);

        ADD_TRAIT(victim, TRAIT_CORPSELOCKED, SPECIES_TRAIT);

        crystalize_timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(crystalize), victim), CRYSTALIZE_PRE_WAIT_TIME, TIMER_STOPPABLE, file: "code/modules/surgery/organs/internal/heart/heart_ethereal.dm", line: 93);

        RegisterSignal(victim, COMSIG_LIVING_DISARM_HIT, nameof(reset_crystalizing));
        RegisterSignal(victim, COMSIG_ATOM_EXAMINE, nameof(on_examine), @override: TRUE);
        RegisterSignal(victim, COMSIG_MOB_APPLY_DAMAGE, nameof(on_take_damage));
    }

    ///Ran when disarmed, prevents the ethereal from reviving
    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:98
    [SignalHandler]
    public virtual void reset_crystalizing(mob_living defender = null, mob_living attacker = null, object zone = null, obj_item weapon = null)
    {
        defender.visible_message(
            span_notice(I18n.T("The crystals on [] are gently broken off.", defender)),
            span_notice(I18n.T("The crystals on your corpse are gently broken off, and will need some time to recover.")));
        deltimer(crystalize_timer_id);
        crystalize_timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(crystalize), defender), CRYSTALIZE_DISARM_WAIT_TIME, TIMER_STOPPABLE, file: "code/modules/surgery/organs/internal/heart/heart_ethereal.dm", line: 105); //Lets us restart the timer on disarm
    }

    ///Actually spawns the crystal which puts the ethereal in it.
    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:108
    public virtual void crystalize(mob_living ethereal = null)
    {
        atom location = ethereal.loc;

        if (!(crystalize_cooldown <= world.time) || ethereal.stat != DEAD) // COOLDOWN_FINISHED, inline
            return; //Should probably not happen, but lets be safe.

        if (ismob(location) || isitem(location) || iseffect(location) || HAS_TRAIT_FROM(this, TRAIT_HUSK, CHANGELING_DRAIN)) //Stops crystallization if they are eaten by a dragon, turned into a legion, consumed by his grace, etc.
        {
            to_chat(ethereal, span_warning(I18n.T("You were unable to finish your crystallization, for obvious reasons.")));
            stop_crystalization_process(ethereal, FALSE);
            return;
        }
        crystalize_cooldown = world.time + INFINITY; //Prevent cheeky double-healing until we get out, this is against stupid admemery (COOLDOWN_START, inline)
        current_crystal = Dm.@new<obj_structure_ethereal_crystal>(get_turf(ethereal), this);
        stop_crystalization_process(ethereal, TRUE);
    }

    ///Stop the crystalization process, unregistering any signals and resetting any variables.
    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:124
    public virtual void stop_crystalization_process(mob_living ethereal = null, float? successful = null)
    {
        successful ??= FALSE;
        UnregisterSignal(ethereal, COMSIG_LIVING_DISARM_HIT);
        UnregisterSignal(ethereal, COMSIG_ATOM_EXAMINE);
        UnregisterSignal(ethereal, COMSIG_MOB_APPLY_DAMAGE);

        crystalization_process_damage = 0; //Reset damage taken during crystalization

        if (!Dm.truthy(successful))
        {
            REMOVE_TRAIT(ethereal, TRAIT_CORPSELOCKED, SPECIES_TRAIT);
            QDEL_NULL(ref current_crystal);
        }

        if (Dm.truthy(crystalize_timer_id))
        {
            deltimer(crystalize_timer_id);
            crystalize_timer_id = null;
        }
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:139
    [SignalHandler]
    public virtual void owner_deleted(datum source = null)
    {
        stop_crystalization_process(owner);
        return;
    }

    ///Lets you stop the process with enough brute damage
    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:146
    [SignalHandler]
    public virtual void on_take_damage(datum source = null, object damage = null, object damagetype = null, object def_zone = null, params object[] __extra)
    {
        if (!Dm.eq(damagetype, BRUTE))
            return;

        crystalization_process_damage += Dm.num(damage);

        if (crystalization_process_damage < BRUTE_DAMAGE_REQUIRED_TO_STOP_CRYSTALIZATION)
            return;

        mob_living_carbon_human ethereal = source as mob_living_carbon_human;

        ethereal.visible_message(
            span_notice(I18n.T("The crystals on [] are completely shattered and stopped growing.", ethereal)),
            span_warning(I18n.T("The crystals on your body have completely broken.")));

        stop_crystalization_process(ethereal);
    }
}

// tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:165
public partial class obj_structure_ethereal_crystal : obj_structure
{
    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:182
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:185
    [DmParams("mapload", "ethereal_heart")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_organ_heart_ethereal ethereal_heart = Dm.arg(__extra, 0) as obj_item_organ_heart_ethereal;
        object dot = base.Initialize(mapload, Dm.forward(__extra, ethereal_heart));
        if (ethereal_heart == null)
        {
            stack_trace("Our crystal has no related heart", "code/modules/surgery/organs/internal/heart/heart_ethereal.dm", 188);
            return INITIALIZE_HINT_QDEL;
        }
        this.ethereal_heart = ethereal_heart;
        ethereal_heart.owner.visible_message(span_notice(I18n.T("The crystals fully encase []!", ethereal_heart.owner)));
        to_chat(ethereal_heart.owner, span_notice(I18n.T("You are encased in a huge crystal!")));
        playsound(get_turf(this), Dm.resource("sound/mobs/humanoids/ethereal/ethereal_crystalization.ogg"), 50);
        atom_movable possible_chair = ethereal_heart.owner.buckled;
        if (possible_chair != null)
            Waiting.NoSleep(possible_chair.unbuckle_mob(ethereal_heart.owner, force: TRUE));
        Waiting.NoSleep(ethereal_heart.owner.forceMove(this)); //put that ethereal in
        Waiting.NoSleep(add_atom_colour(ethereal_heart.ethereal_color, FIXED_COLOUR_PRIORITY));
        crystal_heal_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(heal_ethereal)), CRYSTALIZE_HEAL_TIME, TIMER_STOPPABLE, file: "code/modules/surgery/organs/internal/heart/heart_ethereal.dm", line: 199);
        Waiting.NoSleep(set_light(4, 10, ethereal_heart.ethereal_color));
        update_appearance(UPDATE_OVERLAYS);
        flick("ethereal_crystal_forming", this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(start_crystalization)), 1 * SECONDS, file: "code/modules/surgery/organs/internal/heart/heart_ethereal.dm", line: 203);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:204
    public virtual void start_crystalization()
    {
        being_built = FALSE;
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:208
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        playsound(get_turf(ethereal_heart.owner), Dm.resource("sound/mobs/humanoids/ethereal/ethereal_revive_fail.ogg"), 100);
        await base.atom_destruction(damage_flag);
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:212
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(set_light(0));
        if (ethereal_heart == null)
            return base.Destroy(force);

        ethereal_heart.current_crystal = null;
        ethereal_heart.crystalize_cooldown = world.time + CRYSTALIZE_COOLDOWN_LENGTH; // COOLDOWN_START, inline

        foreach (mob_living living in Dm.each<mob_living>(contents))
        {
            Waiting.NoSleep(living.forceMove(get_turf(this)));
            REMOVE_TRAIT(living, TRAIT_CORPSELOCKED, SPECIES_TRAIT);
            visible_message(span_notice(I18n.T("The crystals shatters, causing [] to fall out.", living)));
        }

        deltimer(crystal_heal_timer);
        return base.Destroy(force);
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:228
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(being_built))
            Dm.append(ref dot, mutable_appearance(icon, icon_state: Dm.text("[]_shine", icon_state), appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/heart/heart_ethereal.dm:233
    public virtual async DmTask heal_ethereal()
    {
        // revive will regenerate organs, so our heart refence is going to be null'd. Unreliable
        mob_living_carbon regenerating = ethereal_heart.owner;

        playsound(get_turf(regenerating), Dm.resource("sound/mobs/humanoids/ethereal/ethereal_revive.ogg"), 100);
        to_chat(regenerating, span_notice(I18n.T("You burst out of the crystal with vigour... </span><span class='userdanger'>But at a cost.")));
        await regenerating.revive(Dm.bitand(HEAL_ALL, ~HEAL_REFRESH_ORGANS));

        if (prob(10)) //10% chance for a severe trauma
            await regenerating.gain_trauma_type(typeof(datum_brain_trauma_severe) /* BRAIN_TRAUMA_SEVERE */, TRAUMA_RESILIENCE_ABSOLUTE);
        else
            await regenerating.gain_trauma_type(typeof(datum_brain_trauma_mild) /* BRAIN_TRAUMA_MILD */, TRAUMA_RESILIENCE_ABSOLUTE);

        // revive calls fully heal -> deletes the crystal.
        // this qdeleted check is just for sanity.
        if (!QDELETED(this))
            qdel(this);
    }
}
