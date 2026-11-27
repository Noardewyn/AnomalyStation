// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/sparring/sparring_datum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the sparring match (its weapon and arena rules with their violations, the outsiders' interference punished by the
// deity, the victory at crit, the stakes). The rest of the var block is generated (D111).

// tg: code/modules/religion/sparring/sparring_datum.dm:1
public partial class datum_sparring_match : datum
{
    // The two vars whose generated types would be wrong (typepaths for a mob and an area instance), declared here (rules 3.2)
    ///the chaplain. it isn't actually a chaplain all the time, but in the cases where the chaplain is needed this will always be them.
    // tg: code/modules/religion/sparring/sparring_datum.dm:3
    public mob_living_carbon_human chaplain;
    ///area instance the participants must stay in
    // tg: code/modules/religion/sparring/sparring_datum.dm:9
    public area arena_condition;

    // tg: code/modules/religion/sparring/sparring_datum.dm:19
    [DmParams("weapons_condition", "arena_condition", "stakes_condition", "chaplain", "opponent")]
    public override async DmTask New(params object[] __extra)
    {
        object weapons_condition = Dm.arg(__extra, 0);
        object arena_condition = Dm.arg(__extra, 1);
        object stakes_condition = Dm.arg(__extra, 2);
        mob_living_carbon_human chaplain = Dm.arg<mob_living_carbon_human>(__extra, 3);
        mob_living_carbon_human opponent = Dm.arg<mob_living_carbon_human>(__extra, 4);
        await base.New(__extra);
        this.weapons_condition = weapons_condition;
        this.arena_condition = arena_condition as area;
        this.stakes_condition = stakes_condition;
        this.chaplain = chaplain;
        this.opponent = opponent;
        ADD_TRAIT(chaplain, TRAIT_SPARRING, TRAIT_GENERIC);
        ADD_TRAIT(opponent, TRAIT_SPARRING, TRAIT_GENERIC);
        hook_signals(chaplain);
        hook_signals(opponent);
        chaplain.add_filter("sparring_outline", 9, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", "#e02200")));
        opponent.add_filter("sparring_outline", 9, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", "#004ee0")));
    }

    // tg: code/modules/religion/sparring/sparring_datum.dm:33
    public virtual void hook_signals(mob_living_carbon_human sparring = null)
    {
        //weapon conditions
        if (Dm.lt(weapons_condition, CONDITION_ANY_WEAPON))
        {
            RegisterSignal(sparring, COMSIG_MOB_FIRED_GUN, nameof(gun_violation));
            RegisterSignal(sparring, COMSIG_MOB_GRENADE_ARMED, nameof(grenade_violation));
        }
        if (Dm.le(weapons_condition, CONDITION_CEREMONIAL_ONLY))
            RegisterSignal(sparring, COMSIG_ATOM_ATTACKBY, nameof(melee_violation));
        //arena conditions
        RegisterSignal(sparring, COMSIG_MOVABLE_MOVED, nameof(arena_violation));
        //severe violations (insta violation win for other party) conditions
        RegisterSignal(sparring, COMSIG_MOVABLE_POST_TELEPORT, nameof(teleport_violation));
        //win conditions
        RegisterSignal(sparring, COMSIG_MOB_STATCHANGE, nameof(check_for_victory));
        //flub conditions
        RegisterSignal(sparring, COMSIG_ATOM_ATTACKBY, nameof(outsider_interference), @override: TRUE);
        RegisterSignal(sparring, COMSIG_ATOM_HULK_ATTACK, nameof(hulk_interference), @override: TRUE);
        RegisterSignal(sparring, COMSIG_ATOM_ATTACK_HAND, nameof(hand_interference), @override: TRUE);
        RegisterSignal(sparring, COMSIG_ATOM_ATTACK_PAW, nameof(paw_interference), @override: TRUE);
        RegisterSignal(sparring, COMSIG_ATOM_HITBY, nameof(thrown_interference), @override: TRUE);
        RegisterSignal(sparring, COMSIG_ATOM_BULLET_ACT, nameof(projectile_interference), @override: TRUE);
        //severe flubs (insta match ender, no winners) conditions
        RegisterSignal(sparring, COMSIG_LIVING_DEATH, nameof(death_flub));
        RegisterSignal(sparring, COMSIG_QDELETING, nameof(deletion_flub));
    }

    // tg: code/modules/religion/sparring/sparring_datum.dm:57
    public virtual void unhook_signals(mob_living_carbon_human sparring = null)
    {
        if (sparring == null)
            return;
        UnregisterSignal(sparring, Dm.list(
            COMSIG_MOB_FIRED_GUN,
            COMSIG_MOB_GRENADE_ARMED,
            COMSIG_MOVABLE_MOVED,
            COMSIG_MOVABLE_POST_TELEPORT,
            COMSIG_MOB_STATCHANGE,
            COMSIG_ATOM_ATTACKBY,
            COMSIG_ATOM_HULK_ATTACK,
            COMSIG_ATOM_ATTACK_HAND,
            COMSIG_ATOM_ATTACK_PAW,
            COMSIG_ATOM_HITBY,
            COMSIG_ATOM_BULLET_ACT,
            COMSIG_LIVING_DEATH,
            COMSIG_QDELETING));
    }

    ///someone is changing health state, end the fight in crit
    // tg: code/modules/religion/sparring/sparring_datum.dm:77
    [SignalHandler]
    public virtual void check_for_victory(datum participant = null, object new_stat = null)
    {
        //death needs to be a flub, conscious means they haven't won
        if (Dm.eq(new_stat, STABLE) || Dm.eq(new_stat, DEAD))
            return;
        if (participant == chaplain)
            Waiting.NoSleep(end_match(opponent, chaplain));
        else
            Waiting.NoSleep(end_match(chaplain, opponent));
    }

    // SIGNALS THAT ARE FOR BEING ATTACKED FIRST (GUILTY)
    // tg: code/modules/religion/sparring/sparring_datum.dm:89
    [SignalHandler]
    public virtual void outsider_interference(datum source = null, obj_item I = null, mob attacker = null)
    {
        if (attacker == chaplain || attacker == opponent)
            return;
        Waiting.Detach(flub(attacker as mob_living));
    }

    // tg: code/modules/religion/sparring/sparring_datum.dm:95
    [SignalHandler]
    public virtual void hulk_interference(datum source = null, mob attacker = null)
    {
        if ((attacker == chaplain || attacker == opponent))
        {
            // fist fighting a hulk is so dumb. i can't fathom why you would do this.
            return;
        }
        Waiting.Detach(flub(attacker as mob_living));
    }

    // tg: code/modules/religion/sparring/sparring_datum.dm:102
    [SignalHandler]
    public virtual void hand_interference(datum source = null, mob_living attacker = null)
    {
        if (attacker == chaplain || attacker == opponent)
        {
            //you can pretty much always use fists as a participant
            return;
        }

        Waiting.Detach(flub(attacker));
    }

    // tg: code/modules/religion/sparring/sparring_datum.dm:110
    [SignalHandler]
    public virtual void paw_interference(datum source = null, mob_living attacker = null)
    {
        if (attacker == chaplain || attacker == opponent)
        {
            //you can pretty much always use paws as a participant
            return;
        }

        Waiting.Detach(flub(attacker));
    }

    // tg: code/modules/religion/sparring/sparring_datum.dm:119
    [SignalHandler]
    public virtual void thrown_interference(datum source = null, atom_movable thrown_movable = null, float? skipcatch = null, float? hitpush = null, float? blocked = null, datum_thrownthing throwingdatum = null)
    {
        skipcatch ??= FALSE;
        hitpush ??= TRUE;
        blocked ??= FALSE;
        if (!isitem(thrown_movable))
            return;
        mob_living honorbound = source as mob_living;
        obj_item thrown_item = thrown_movable as obj_item;
        mob thrown_by = throwingdatum?.get_thrower() as mob;
        if (thrown_item.throwforce < honorbound.health && ishuman(thrown_by))
            Waiting.Detach(flub(thrown_by as mob_living));
    }

    // tg: code/modules/religion/sparring/sparring_datum.dm:130
    [SignalHandler]
    public virtual void projectile_interference(datum participant = null, obj_projectile proj = null)
    {
        if (proj.firer == chaplain || proj.firer == opponent)
        {
            //oh, well that's allowed. or maybe it isn't. doesn't matter because firing the gun will trigger a violation, so no additional violation needed
            return;
        }
        mob_living interfering = null;
        if (isliving(proj.firer))
            interfering = proj.firer as mob_living;
        Waiting.Detach(flub(interfering));
    }

    ///someone randomly fucking died
    // tg: code/modules/religion/sparring/sparring_datum.dm:141
    [SignalHandler]
    public virtual void death_flub(datum deceased = null)
    {
        flubbed_match();
    }

    ///someone randomly fucking deleted
    // tg: code/modules/religion/sparring/sparring_datum.dm:147
    [SignalHandler]
    public virtual void deletion_flub(datum qdeleting = null)
    {
        flubbed_match();
    }

    ///someone used a gun
    // tg: code/modules/religion/sparring/sparring_datum.dm:153
    [SignalHandler]
    public virtual void gun_violation(mob offender = null, obj_item_gun gun_fired = null, object target = null, object @params = null, object zone_override = null, DmList bonus_spread_values = null)
    {
        violation(offender as mob_living_carbon_human, I18n.T("using guns"));
    }

    ///someone used a grenade
    // tg: code/modules/religion/sparring/sparring_datum.dm:158
    [SignalHandler]
    public virtual void grenade_violation(datum offender = null)
    {
        violation(offender as mob_living_carbon_human, I18n.T("using grenades"));
    }

    ///someone used melee weapons
    // tg: code/modules/religion/sparring/sparring_datum.dm:163
    [SignalHandler]
    public virtual void melee_violation(datum offender = null, obj_item thing = null, mob user = null, object @params = null)
    {
        if (!Dm.eq(weapons_condition, CONDITION_CEREMONIAL_ONLY))
            violation(offender as mob_living_carbon_human, I18n.T("using melee weapons"));
        if (thing is obj_item_ceremonial_blade)
            return;
        violation(offender as mob_living_carbon_human, I18n.T("using non ceremonial weapons"));
    }

    // tg: code/modules/religion/sparring/sparring_datum.dm:172
    [SignalHandler]
    public virtual void teleport_violation(datum offender = null)
    {
        if (offender == chaplain)
            Waiting.NoSleep(end_match(opponent, chaplain, violation_victory: TRUE));
        else
            Waiting.NoSleep(end_match(chaplain, opponent, violation_victory: TRUE));
    }

    ///someone tried to leave
    // tg: code/modules/religion/sparring/sparring_datum.dm:180
    [SignalHandler]
    public virtual void arena_violation(atom_movable mover = null, atom oldloc = null, object direction = null)
    {
        area inhabited_area = get_area(mover) as area;
        if (inhabited_area == arena_condition)
            return; //still in the ring!! :)

        violation(mover as mob_living_carbon_human, I18n.T("leaving the arena"));
        atom throw_target = get_edge_target_turf(mover, REVERSE_DIR(Dm.num(direction))) as atom;
        Waiting.NoSleep(mover.throw_at(throw_target, 6, 4));
    }

    // tg: code/modules/religion/sparring/sparring_datum.dm:191
    [SignalHandler]
    public virtual void violation(mob_living_carbon_human offender = null, object reason = null)
    {
        to_chat(offender, span_userdanger(I18n.T("Violation! No []!", reason)));
        if (offender == chaplain)
        {
            chaplain_violations_allowed--;
            if (!Dm.truthy(chaplain_violations_allowed))
                Waiting.NoSleep(end_match(opponent, chaplain, violation_victory: TRUE));
        }
        else
        {
            opponent_violations_allowed--;
            if (!Dm.truthy(opponent_violations_allowed))
                Waiting.NoSleep(end_match(chaplain, opponent, violation_victory: TRUE));
        }
    }

    // tg: code/modules/religion/sparring/sparring_datum.dm:204
    public virtual async DmTask flub(mob_living interfering = null)
    {
        if (interfering != null)
        {
            DmList possible_punishments = Dm.list(PUNISHMENT_OMEN, PUNISHMENT_LIGHTNING);
            if (ishuman(interfering))
                Dm.append(ref possible_punishments, PUNISHMENT_BRAND);
            object sw__1 = pick(possible_punishments);
            if (Dm.eq(sw__1, PUNISHMENT_OMEN))
            {
                to_chat(interfering, span_warning(I18n.T("You get a bad feeling... for interfering with []'s sparring match...", chaplain)));
                await interfering.AddComponent(typeof(datum_component_omen));
            }
            else if (Dm.eq(sw__1, PUNISHMENT_LIGHTNING))
            {
                to_chat(interfering, span_warning(I18n.T("[] has punished you for interfering with []'s sparring match!", GLOB.deity, chaplain)));
                await lightningbolt(interfering);
            }
            else if (Dm.eq(sw__1, PUNISHMENT_BRAND))
            {
                mob_living_carbon_human branded = interfering as mob_living_carbon_human;
                to_chat(interfering, span_warning(I18n.T("[] brands your flesh for interfering with []'s sparring match!!", GLOB.deity, chaplain)));
                obj_item_bodypart branded_limb = pick(branded.get_bodyparts()) as obj_item_bodypart;
                await branded_limb.force_wound_upwards(typeof(datum_wound_burn_flesh_severe_brand), wound_source: I18n.T("divine intervention"));
                await branded.emote("scream");
            }
        }

        flubs--;
        if (!Dm.truthy(flubs)) //too many interferences
            flubbed_match();
    }

    ///this match was interfered on, nobody wins or loses anything, just end
    // tg: code/modules/religion/sparring/sparring_datum.dm:228
    public virtual void flubbed_match()
    {
        cleanup_sparring_match();

        if (chaplain != null) //flubing means we don't know who is still standing
            to_chat(chaplain, span_bolddanger(I18n.T("The match was flub'd! No winners, no losers. You may restart the match with another contract.")));
        if (opponent != null)
            to_chat(opponent, span_bolddanger(I18n.T("The match was flub'd! No winners, no losers.")));
        qdel(this);
    }

    ///helper to remove all the effects after a match ends
    // tg: code/modules/religion/sparring/sparring_datum.dm:238
    public virtual void cleanup_sparring_match()
    {
        REMOVE_TRAIT(chaplain, TRAIT_SPARRING, TRAIT_GENERIC);
        REMOVE_TRAIT(opponent, TRAIT_SPARRING, TRAIT_GENERIC);
        unhook_signals(chaplain);
        unhook_signals(opponent);
        chaplain.remove_filter("sparring_outline");
        opponent.remove_filter("sparring_outline");
    }

    // tg: code/modules/religion/sparring/sparring_datum.dm:246
    public virtual async DmTask end_match(mob_living_carbon_human winner = null, mob_living_carbon_human loser = null, float? violation_victory = null)
    {
        violation_victory ??= FALSE;
        cleanup_sparring_match();
        to_chat(chaplain, span_bolddanger(I18n.T("[]  [] HAS WON!", Dm.truthy(violation_victory) ? I18n.T("[] DISQUALIFIED!", loser) : "", winner)));
        to_chat(opponent, span_bolddanger(I18n.T("[]  [] HAS WON!", Dm.truthy(violation_victory) ? I18n.T("[] DISQUALIFIED!", loser) : "", winner)));
        win(winner, loser, violation_victory);
        await lose(loser, winner);
        if (!Dm.eq(stakes_condition, STAKES_YOUR_SOUL))
        {
            string healing_message = I18n.T("You may want to heal up the loser now.");
            if (winner == chaplain)
                healing_message += I18n.T(" Your bible will heal the loser for awhile.");
            to_chat(winner, span_notice(healing_message));
        }
        qdel(this);
    }

    ///most of the effects are handled on `lose()` instead.
    // tg: code/modules/religion/sparring/sparring_datum.dm:260
    public virtual void win(mob_living_carbon_human winner = null, mob_living_carbon_human loser = null, object violation_victory = null)
    {
        object sw__1 = stakes_condition;
        if (Dm.eq(sw__1, STAKES_HOLY_MATCH))
        {
            if (winner == chaplain)
            {
                if (Dm.truthy(violation_victory))
                    to_chat(winner, span_warning(I18n.T("[] is not entertained from a matched decided by violations. No favor awarded...", GLOB.deity)));
                else
                    to_chat(winner, span_nicegreen(I18n.T("You've won favor with []!", GLOB.deity)));
                datum_religion_sect_spar sect = GLOB.religious_sect as datum_religion_sect_spar;
                sect.adjust_favor(1, winner);
                Dm.append(ref sect.past_opponents, WEAKREF(loser));
            }
        }
        else if (Dm.eq(sw__1, STAKES_MONEY_MATCH))
            to_chat(winner, span_nicegreen(I18n.T("You've won all of []'s money!", loser)));
        else if (Dm.eq(sw__1, STAKES_YOUR_SOUL))
            to_chat(winner, span_nicegreen(I18n.T("You've won []'s SOUL!", loser)));
    }

    // tg: code/modules/religion/sparring/sparring_datum.dm:276
    public virtual async DmTask lose(mob_living_carbon_human loser = null, mob_living_carbon_human winner = null)
    {
        if (loser == null) //shit happened?
            return;
        object sw__1 = stakes_condition;
        if (Dm.eq(sw__1, STAKES_HOLY_MATCH))
        {
            if (loser == chaplain)
            {
                datum_religion_sect_spar sect = GLOB.religious_sect as datum_religion_sect_spar;
                sect.matches_lost++;
                if (sect.matches_lost < 3)
                {
                    to_chat(loser, span_userdanger(I18n.T("[] is angry you lost in their name!", GLOB.deity)));
                    return;
                }
                to_chat(loser, span_userdanger(I18n.T("[] is enraged by your lackluster sparring record!", GLOB.deity)));
                await lightningbolt(loser);
                await loser.add_mood_event("sparring", typeof(datum_mood_event_banished));
                loser.mind.set_holy_role(NONE);
                to_chat(loser, span_userdanger(I18n.T("You have been excommunicated! You are no longer holy!")));
            }
        }
        else if (Dm.eq(sw__1, STAKES_MONEY_MATCH))
        {
            to_chat(loser, span_userdanger(I18n.T("You've lost all your money to []!", winner)));
            datum_bank_account loser_account = loser.get_bank_account() as datum_bank_account;
            datum_bank_account winner_account = winner.get_bank_account() as datum_bank_account;
            if (loser_account == null || winner_account == null)//the winner is pretty owned in this case but whatever shoulda read the fine print of the contract
                return;
            winner_account.transfer_money(loser_account, loser_account.account_balance, "Bet: Sparring");
        }
        else if (Dm.eq(sw__1, STAKES_YOUR_SOUL))
        {
            turf shard_turf = get_turf(loser);
            if (shard_turf == null)
                return;
            to_chat(loser, span_userdanger(I18n.T("You've lost ownership over your soul to []!", winner)));
            obj_item_soulstone_anybody_chaplain_sparring shard = Dm.@new<obj_item_soulstone_anybody_chaplain_sparring>(shard_turf);
            Waiting.Detach(shard.capture_soul(loser, winner, forced: TRUE));
        }
    }
}
