// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/battle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. Every sound is played to the hallucinator's client alone. The var blocks are generated (D111).

/// Battle hallucination, makes it sound like a melee or gun battle is going on in the background.
// tg: code/modules/hallucination/battle.dm:2
public partial class datum_hallucination_battle : datum_hallucination
{
    // tg: code/modules/hallucination/battle.dm:8
    public override async DmTask<object> start()
    {
        if (HAS_TRAIT(hallucinator, TRAIT_DEAF))
            return FALSE;

        // for subtypes
        return TRUE;
    }
}

/// Subtype of battle hallucination for gun based battles, where it sounds like someone is being shot.
// tg: code/modules/hallucination/battle.dm:16
public partial class datum_hallucination_battle_gun : datum_hallucination_battle
{
    // tg: code/modules/hallucination/battle.dm:33
    public override async DmTask<object> start()
    {
        object dot = await base.start();
        if (!Dm.truthy(dot))
            return dot;

        fire_loop(random_far_turf() as turf, rand(shots_to_fire_lower_range, shots_to_fire_upper_range));
        return dot;
    }

    /// The main loop for gun based hallucinations.
    // tg: code/modules/hallucination/battle.dm:40
    public virtual void fire_loop(turf source = null, float? shots_left = null, float? hits = null)
    {
        shots_left ??= 3;
        hits ??= 0;
        if (QDELETED(this) || QDELETED(hallucinator) || source == null)
            return;

        // We shoot our shot.
        hallucinator.playsound_local(source, fire_sound, 25, TRUE);

        // Shortly after shooting our shot, it plays a hit (or miss) sound.
        float next_hit_sound = rand(0.5f * SECONDS, 1 * SECONDS);
        if (prob(50))
        {
            _addtimer(Dm.@new<datum_callback>(hallucinator, "playsound_local", source, hit_person_sound, 25, TRUE), next_hit_sound, file: "code/modules/hallucination/battle.dm", line: 49);
            hits++;
        }
        else
            _addtimer(Dm.@new<datum_callback>(hallucinator, "playsound_local", source, hit_wall_sound, 25, TRUE), next_hit_sound, file: "code/modules/hallucination/battle.dm", line: 52);

        // If we scored enough hits, we have a chance to knock them down and stop the hallucination early.
        if (hits >= number_of_hits_to_end && prob(chance_to_fall))
        {
            _addtimer(Dm.@new<datum_callback>(hallucinator, "playsound_local", source, SFX_BODYFALL, 25, TRUE), next_hit_sound, file: "code/modules/hallucination/battle.dm", line: 56);
            qdel(this);
        }

        // Or, if we do have shots left, keep it going.
        else if (shots_left >= 0)
        {
            shots_left--;
            _addtimer(Dm.@new<datum_callback>(this, nameof(fire_loop), source, shots_left, hits), rand(CLICK_CD_RANGE, CLICK_CD_RANGE + 6), file: "code/modules/hallucination/battle.dm", line: 62);
        }

        // Otherwise, if we have no shots left, stop the hallucination.
        else
            qdel(this);
    }
}

/// A hallucination of someone being hit with a stun prod, followed by cable cuffing.
// tg: code/modules/hallucination/battle.dm:101
public partial class datum_hallucination_battle_stun_prod : datum_hallucination_battle
{
    // tg: code/modules/hallucination/battle.dm:103
    public override async DmTask<object> start()
    {
        object dot = await base.start();
        if (!Dm.truthy(dot))
            return dot;

        turf source = random_far_turf() as turf;

        hallucinator.playsound_local(source, Dm.resource("sound/items/weapons/egloves.ogg"), 40, TRUE);
        hallucinator.playsound_local(source, SFX_BODYFALL, 25, TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(fake_cuff), source), 2 * SECONDS, file: "code/modules/hallucination/battle.dm", line: 113);
        return dot;
    }

    /// Plays a fake cable-cuff sound and deletes the hallucination.
    // tg: code/modules/hallucination/battle.dm:116
    public virtual void fake_cuff(turf source = null)
    {
        if (QDELETED(this) || QDELETED(hallucinator) || source == null)
            return;

        hallucinator.playsound_local(source, Dm.resource("sound/items/weapons/cablecuff.ogg"), 15, TRUE);
        qdel(this);
    }
}

/// A hallucination of someone being stun batonned, and subsequently harmbatonned.
// tg: code/modules/hallucination/battle.dm:123
public partial class datum_hallucination_battle_harm_baton : datum_hallucination_battle
{
    // tg: code/modules/hallucination/battle.dm:125
    public override async DmTask<object> start()
    {
        object dot = await base.start();
        if (!Dm.truthy(dot))
            return dot;

        turf source = random_far_turf() as turf;

        hallucinator.playsound_local(source, Dm.resource("sound/items/weapons/egloves.ogg"), 40, TRUE);
        hallucinator.playsound_local(source, SFX_BODYFALL, 25, TRUE);

        _addtimer(Dm.@new<datum_callback>(this, nameof(harmbaton_loop), source, rand(5, 12)), 2 * SECONDS, file: "code/modules/hallucination/battle.dm", line: 136);
        return dot;
    }

    /// The main sound loop for harmbatonning.
    // tg: code/modules/hallucination/battle.dm:139
    public virtual void harmbaton_loop(turf source = null, float? hits_remaing = null)
    {
        hits_remaing ??= 5;
        if (QDELETED(this) || QDELETED(hallucinator) || source == null)
            return;

        hallucinator.playsound_local(source, SFX_SWING_HIT, 50, TRUE);
        hits_remaing--;
        if (hits_remaing <= 0)
            qdel(this);

        else
            _addtimer(Dm.@new<datum_callback>(this, nameof(harmbaton_loop), source, hits_remaing), rand(CLICK_CD_MELEE, CLICK_CD_MELEE + 4), file: "code/modules/hallucination/battle.dm", line: 149);
    }
}

/// A hallucination of someone unsheathing an energy sword, going to town, and sheathing it again.
// tg: code/modules/hallucination/battle.dm:152
public partial class datum_hallucination_battle_e_sword : datum_hallucination_battle
{
    // tg: code/modules/hallucination/battle.dm:154
    public override async DmTask<object> start()
    {
        object dot = await base.start();
        if (!Dm.truthy(dot))
            return dot;

        turf source = random_far_turf() as turf;

        hallucinator.playsound_local(source, Dm.resource("sound/items/weapons/saberon.ogg"), 15, 1);
        _addtimer(Dm.@new<datum_callback>(this, nameof(stab_loop), source, rand(4, 8)), CLICK_CD_MELEE, file: "code/modules/hallucination/battle.dm", line: 162);
        return dot;
    }

    /// The main sound loop of someone being esworded.
    // tg: code/modules/hallucination/battle.dm:165
    public virtual void stab_loop(turf source = null, float? stabs_remaining = null)
    {
        stabs_remaining ??= 4;
        if (QDELETED(this) || QDELETED(hallucinator) || source == null)
            return;

        if (stabs_remaining >= 1)
            hallucinator.playsound_local(source, Dm.resource("sound/items/weapons/blade1.ogg"), 50, TRUE);

        else
        {
            hallucinator.playsound_local(source, Dm.resource("sound/items/weapons/saberoff.ogg"), 15, TRUE);
            qdel(this);
            return;
        }

        if (stabs_remaining == 4)
            hallucinator.playsound_local(source, SFX_BODYFALL, 25, TRUE);

        _addtimer(Dm.@new<datum_callback>(this, nameof(stab_loop), source, stabs_remaining - 1), rand(CLICK_CD_MELEE, CLICK_CD_MELEE + 6), file: "code/modules/hallucination/battle.dm", line: 180);
    }
}

/// A hallucination of a syndicate bomb ticking down.
// tg: code/modules/hallucination/battle.dm:183
public partial class datum_hallucination_battle_bomb : datum_hallucination_battle
{
    // tg: code/modules/hallucination/battle.dm:185
    public override async DmTask<object> start()
    {
        object dot = await base.start();
        if (!Dm.truthy(dot))
            return dot;

        _addtimer(Dm.@new<datum_callback>(this, nameof(fake_tick), random_far_turf(), rand(3, 11)), 1.5f * SECONDS, file: "code/modules/hallucination/battle.dm", line: 190);
        return dot;
    }

    /// The loop of the (fake) bomb ticking down.
    // tg: code/modules/hallucination/battle.dm:193
    public virtual void fake_tick(turf source = null, float? ticks_remaining = null)
    {
        ticks_remaining ??= 3;
        if (QDELETED(this) || QDELETED(hallucinator) || source == null)
            return;

        hallucinator.playsound_local(source, Dm.resource("sound/items/timer.ogg"), 25, FALSE);
        ticks_remaining--;
        if (ticks_remaining <= 0)
            qdel(this);

        else
            _addtimer(Dm.@new<datum_callback>(this, nameof(fake_tick), source, ticks_remaining), 1.5f * SECONDS, file: "code/modules/hallucination/battle.dm", line: 201);
    }
}
