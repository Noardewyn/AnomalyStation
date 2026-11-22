// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/// Repeatedly throw up until there's nothing left inside, regrettably sufficiently complex that it requires its own file
// tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:3
public partial class datum_action_cooldown_mob_cooldown_goose_vomit : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:15
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = await base.Grant(granted_to, __extra);
        if (owner == null)
            return dot;
        RegisterSignals(owner, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_EXITED), nameof(update_status_on_signal));
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:21
    public override async DmTask Remove(mob removed_from = null)
    {
        UnregisterSignal(owner, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_EXITED));
        await base.Remove(removed_from);
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:25
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!Dm.truthy(length(owner.contents)))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, "stomach empty!");
            return FALSE;
        }
        if (!isliving(owner))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, "you're not alive!");
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:39
    public override async DmTask<object> Activate(atom target = null)
    {
        StartCooldown(INFINITY);
        if (owner is mob_living_basic_goose)
        {
            owner.icon_state = "vomit";
            Dm.flick("vomit_start", owner);
            (owner.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_GOOSE_VOMIT_CHANCE, 0);
            _addtimer(Dm.@new<datum_callback>(this, nameof(start_vomiting)), 13, file: "code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm", line: 45); // 13 frame long animation
        }
        else
            await start_vomiting();
        return null;
    }

    /// Start the performance
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:50
    public virtual async DmTask start_vomiting()
    {
        mob_living living_owner = owner as mob_living;
        await living_owner.apply_status_effect(typeof(datum_status_effect_goose_vomit), extra_duration);
        extra_duration = 0;
    }
}

/// Handles iteratively emptying our stomach
// tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:57
public partial class datum_status_effect_goose_vomit : datum_status_effect
{
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:69
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        vomit_duration += Dm.num(Dm.arg(__extra, 0) ?? 0f);
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:73
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.set_jitter_if_lower(vomit_duration);
        (owner.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_GOOSE_PANICKED, TRUE);
        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(on_owner_died));
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(on_owner_moved));
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:80
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, Dm.list(COMSIG_LIVING_DEATH, COMSIG_MOVABLE_MOVED));
        (owner.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_GOOSE_PANICKED, FALSE);
        if (owner is mob_living_basic_goose goose_mob && owner.stat != DEAD)
        {
            Dm.flick("vomit_end", owner);
            goose_mob.icon_state = goose_mob.icon_living;
        }

        datum_action_cooldown_mob_cooldown_goose_vomit vomit_action = Dm.locate<datum_action_cooldown_mob_cooldown_goose_vomit>(owner.actions) as datum_action_cooldown_mob_cooldown_goose_vomit;
        vomit_action?.StartCooldown(0 * SECONDS);
        return null;
    }

    /// Don't keep vomiting from beyond the grave
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:92
    [SignalHandler]
    public virtual void on_owner_died()
    {
        qdel(this);
    }

    /// For good measure we'll spit up every time we take a step too
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:97
    [SignalHandler]
    public virtual void on_owner_moved()
    {
        Waiting.Detach(vomit_iteratively(can_move: FALSE));
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:101
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        elapsed_time += Dm.num(seconds_between_ticks);

        if (!Dm.truthy(length(owner.contents)))
        {
            qdel(this);
            return null;
        }

        if (elapsed_time <= vomit_duration)
            await vomit_iteratively();
        else
            await vomit_finale();
        return null;
    }

    /// One to come up
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:114
    public virtual async DmTask vomit_iteratively(float? can_move = null)
    {
        can_move ??= TRUE;
        if (prob(vomit_item_chance))
            await hurl_item();
        else
            await make_mess(owner.drop_location());

        if (Dm.truthy(can_move) && prob(move_chance))
        {
            object move_dir = pick(GLOB.alldirs);
            atom destination_turf = get_step(owner, Dm.num(move_dir)) as atom;
            await owner.Move(destination_turf, Dm.num(move_dir));
        }
    }

    /// Stop fucking around and get the rest of it out
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:126
    public virtual async DmTask vomit_finale()
    {
        tick_interval = 0.2f * SECONDS;
        await owner.set_jitter_if_lower(1 * SECONDS);
        await hurl_item(vomit_strongly: TRUE);
    }

    /// Produce an item from our inventory
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:132
    public virtual async DmTask hurl_item(float? vomit_strongly = null)
    {
        vomit_strongly ??= FALSE;
        if (!Dm.truthy(length(owner.contents)))
            return;
        // pick_n_take(owner.contents) takes it off the contents' copy: owner.contents reads the live list here, and the move below takes it off
        atom_movable thing = pick(owner.contents) as atom_movable;
        if (!ismovable(thing) || HAS_TRAIT(thing, TRAIT_NOT_BARFABLE))
        {
            qdel(this); // Someone is fucking around with this goose, let's not get stuck in this forever
            return;
        }
        object drop_location = owner.drop_location();

        await thing.forceMove(drop_location as atom);
        if (isopenturf(drop_location))
        {
            await make_mess(drop_location);
            if (QDELETED(thing)) // Stacks can merge and delete themselves
                return;
            object destination = get_edge_target_turf(drop_location as atom, pick(GLOB.alldirs));
            float throwRange = Dm.truthy(vomit_strongly) ? rand(2, 8) : 1;
            await thing.safe_throw_at(destination as atom, throwRange, 2);
        }
    }

    /// Make a mess
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:151
    public virtual async DmTask make_mess(object drop_turf = null)
    {
        if (drop_turf is not turf_open open_turf)
            return;
        playsound(open_turf, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
        await open_turf.add_vomit_floor(owner);
    }
}

/// Wheeze until we die
// tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:160
public partial class datum_status_effect_goose_choking : datum_status_effect
{
    /// What things do we do while dying
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:167
    private static DmList emotes => TgState.Current.datum_status_effect_goose_choking__emotes ??= Dm.list("chokes.", "coughs.", "gasps.", "tries urgently to breathe.", "shudders violently.", "wheezes.");

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:169
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (SPT_PROB(emote_prob, seconds_between_ticks))
            owner.manual_emote(pick(emotes));
        return null;
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:173
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.set_jitter_if_lower(duration);
        (owner.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_GOOSE_PANICKED, TRUE);
        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(on_owner_died));
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:179
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_LIVING_DEATH);
        if (Dm.num(duration) > 0)
            return null; // Saved by something, although probably by dying early
        owner.death_message = Dm.text("lets out one final oxygen-deprived honk before [] go[] limp and lifeless..", owner.p_they(), owner.p_es());
        await owner.death();
        return null;
    }

    /// Don't keep dying if we died
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose_vomit.dm:187
    [SignalHandler]
    public virtual void on_owner_died()
    {
        qdel(this);
    }
}

public sealed partial class TgState
{
    internal DmList datum_status_effect_goose_choking__emotes;
}
