// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/fearful/sources/_sources.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N3b, D316: the fear system's simple sources; the quirks that add them come with the quirks). The var blocks
// are generated (D111).

// Terror source handlers
/// Simple source which passively increases terror based on a single condition and can do something when its added/removed
// tg: code/datums/components/fearful/sources/_sources.dm:3
public partial class datum_terror_handler_simple_source : datum_terror_handler
{
    // tg: code/datums/components/fearful/sources/_sources.dm:8
    public override async DmTask<object> tick(object seconds_per_tick = null, object terror_buildup = null)
    {
        object dot = await base.tick(seconds_per_tick, terror_buildup);
        float buildup = Dm.num(terror_buildup);
        bool temporarily_immune = HAS_TRAIT(owner, TRAIT_FEARLESS) || HAS_TRAIT(owner, TRAIT_MIND_TEMPORARILY_GONE) || IS_UNCONSCIOUS(owner);
        float buildup_per_second = temporarily_immune ? 0 : Dm.num(await check_condition(seconds_per_tick, terror_buildup));
        if (buildup_per_second > 0 && buildup < TERROR_BUILDUP_PASSIVE_MAXIMUM)
        {
            if (!Dm.truthy(active))
            {
                active = TRUE;
                await on_activation(terror_buildup);
            }
            dot = Dm.add(dot, min(buildup_per_second * Dm.num(seconds_per_tick), TERROR_BUILDUP_PASSIVE_MAXIMUM - buildup));
        }
        else if (Dm.truthy(active))
        {
            active = FALSE;
            on_deactivation(terror_buildup);
        }
        return dot;
    }

    /**
     * Checks how much terror to build up this tick
     *
     * * seconds_per_tick - How many seconds are in each tick, static
     * * terror_buildup - How much terror the owner currently has
     *
     * Return a number, which is the amount of terror to build up this tick.
     * Return 0 for no buildup.
     */
    // tg: code/datums/components/fearful/sources/_sources.dm:31
    public virtual async DmTask<object> check_condition(object seconds_per_tick = null, object terror_buildup = null)
    {
        return 0;
    }

    /// Proc that's called when the effect is first applied, for moodlets and alike
    // tg: code/datums/components/fearful/sources/_sources.dm:35
    public virtual async DmTask on_activation(object terror_buildup = null)
    {
        return;
    }

    /// Proc that's called when the effect stops working, for moodlets and alike
    // tg: code/datums/components/fearful/sources/_sources.dm:39
    public virtual void on_deactivation(object terror_buildup = null)
    {
        return;
    }
}

/// Makes the owner terrified of darkness
// tg: code/datums/components/fearful/sources/_sources.dm:43
public partial class datum_terror_handler_simple_source_nyctophobia : datum_terror_handler_simple_source
{
    // tg: code/datums/components/fearful/sources/_sources.dm:47
    public override object Destroy(float? force = null)
    {
        owner.clear_mood_event("nyctophobia");
        return base.Destroy(force);
    }

    // tg: code/datums/components/fearful/sources/_sources.dm:51
    public override async DmTask<object> check_condition(object seconds_per_tick = null, object terror_buildup = null)
    {
        if (ishuman(owner))
        {
            var as_human = (mob_living_carbon_human)owner;
            if (Dm.@in((as_human.dna?.species as datum_species)?.id, Dm.list(SPECIES_SHADOW, SPECIES_NIGHTMARE)))
                return 0;
        }

        if (Dm.truthy(meson_negated) && Dm.truthy(Dm.bitand(owner.sight, SEE_TURFS)))
            return 0;

        float lit_tiles = 0;
        float unlit_tiles = 0;

        foreach (turf_open turf_to_check in Dm.each<turf_open>(Builtins.range(1, owner)))
        {
            if (Dm.truthy(turf_to_check.check_lumcount_above(LIGHTING_TILE_IS_DARK)))
                lit_tiles++;
            else
                unlit_tiles++;
        }

        return lit_tiles < unlit_tiles ? 5 : 0;
    }

    // tg: code/datums/components/fearful/sources/_sources.dm:71
    public override async DmTask on_activation(object terror_buildup = null)
    {
        await owner.add_mood_event("nyctophobia", typeof(datum_mood_event_nyctophobia));
    }

    // tg: code/datums/components/fearful/sources/_sources.dm:74
    public override void on_deactivation(object terror_buildup = null)
    {
        owner.clear_mood_event("nyctophobia");
    }
}

/// Nightmare spell version with quicker buildup that shadows the quirk one, also handles removing the status upon reaching 0 terror
// tg: code/datums/components/fearful/sources/_sources.dm:78
public partial class datum_terror_handler_simple_source_nyctophobia_terrified : datum_terror_handler_simple_source_nyctophobia
{
    // tg: code/datums/components/fearful/sources/_sources.dm:83
    public override async DmTask<object> tick(object seconds_per_tick = null, object terror_buildup = null)
    {
        object dot = Dm.mul(await base.tick(seconds_per_tick, terror_buildup), 3);
        if (Dm.num(terror_buildup) == 0 && Dm.num(dot) <= 0)
            owner.RemoveComponentSource("terrified", typeof(datum_component_fearful));
        return dot;
    }
}

/// Makes the owner afraid of being stuck in closets, crates, mechs, etc
// tg: code/datums/components/fearful/sources/_sources.dm:89
public partial class datum_terror_handler_simple_source_claustrophobia : datum_terror_handler_simple_source
{
    // tg: code/datums/components/fearful/sources/_sources.dm:92
    public override object Destroy(float? force = null)
    {
        owner.clear_mood_event("claustrophobia");
        return base.Destroy(force);
    }

    // tg: code/datums/components/fearful/sources/_sources.dm:96
    public override async DmTask<object> check_condition(object seconds_per_tick = null, object terror_buildup = null)
    {
        if (isturf(owner.loc))
            return 0;

        if (message_cd <= world.time && SPT_PROB(15, seconds_per_tick)) // COOLDOWN_FINISHED(src, message_cd)
        {
            to_chat(owner, span_warning(I18n.T("You feel trapped! Must escape... can't breathe...")));
            message_cd = world.time + TERROR_MESSAGE_CD; // COOLDOWN_START(src, message_cd, TERROR_MESSAGE_CD)
        }

        return 15;
    }

    // tg: code/datums/components/fearful/sources/_sources.dm:106
    public override async DmTask on_activation(object terror_buildup = null)
    {
        await owner.add_mood_event("claustrophobia", typeof(datum_mood_event_claustrophobia));
    }

    // tg: code/datums/components/fearful/sources/_sources.dm:109
    public override void on_deactivation(object terror_buildup = null)
    {
        owner.clear_mood_event("claustrophobia");
    }
}

/// Makes the owner afraid of certain jolly figures
// tg: code/datums/components/fearful/sources/_sources.dm:113
public partial class datum_terror_handler_simple_source_clausophobia : datum_terror_handler_simple_source
{
    // tg: code/datums/components/fearful/sources/_sources.dm:116
    public override async DmTask<object> check_condition(object seconds_per_tick = null, object terror_buildup = null)
    {
        bool certified_jolly = false;

        foreach (mob_living_carbon_human possible_claus in Dm.each<mob_living_carbon_human>(view(5, owner)))
        {
            if (possible_claus == owner)
                continue; // imagine being scared of your own existence

            if (possible_claus.wear_suit is not obj_item_clothing_suit_space_santa)
                continue;

            if (possible_claus.back is obj_item_storage_backpack_santabag)
            {
                certified_jolly = true;
                break;
            }

            if (possible_claus.head is obj_item_clothing_head_costume_santa || possible_claus.head is obj_item_clothing_head_helmet_space_santahat)
            {
                certified_jolly = true;
                break;
            }
        }

        if (!certified_jolly)
            return 0;

        if (message_cd <= world.time && SPT_PROB(15, seconds_per_tick)) // COOLDOWN_FINISHED(src, message_cd)
        {
            to_chat(owner, span_warning(I18n.T("Santa Claus is here! I gotta get out of here!")));
            message_cd = world.time + TERROR_MESSAGE_CD; // COOLDOWN_START(src, message_cd, TERROR_MESSAGE_CD)
        }

        return 15;
    }
}

/// Makes the owner afraid of being alone
// tg: code/datums/components/fearful/sources/_sources.dm:144
public partial class datum_terror_handler_simple_source_monophobia : datum_terror_handler_simple_source
{
    // tg: code/datums/components/fearful/sources/_sources.dm:147
    public override async DmTask<object> check_condition(object seconds_per_tick = null, object terror_buildup = null)
    {
        float check_radius = 7;
        if (Dm.truthy(owner.is_blind()))
            check_radius = 1;

        foreach (mob_living friend in Dm.each<mob_living>(view(check_radius, owner)))
        {
            if (friend == owner)
                continue;

            if (friend is mob_living_basic_pet || Dm.truthy(friend.ckey))
                return 0;
        }

        if (message_cd <= world.time && SPT_PROB(10, seconds_per_tick)) // COOLDOWN_FINISHED(src, message_cd)
        {
            to_chat(owner, span_warning(I18n.T("You feel terribly lonely...")));
            message_cd = world.time + TERROR_MESSAGE_CD; // COOLDOWN_START(src, message_cd, TERROR_MESSAGE_CD)
        }

        return 2.5f; // Pretty low, ~4 minutes to reach passive cap
    }
}

/// Afraid of being on fire or being near open flames (even flames such as candles or fireplaces)
// tg: code/datums/components/fearful/sources/_sources.dm:166
public partial class datum_terror_handler_simple_source_pyrophobia : datum_terror_handler_simple_source
{
    // tg: code/datums/components/fearful/sources/_sources.dm:172
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(owner, COMSIG_LIVING_HALLUCINATING, nameof(on_hallucination));
    }

    // tg: code/datums/components/fearful/sources/_sources.dm:176
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(owner, COMSIG_LIVING_HALLUCINATING);
        return base.Destroy(force);
    }

    // tg: code/datums/components/fearful/sources/_sources.dm:180
    public override async DmTask<object> check_condition(object seconds_per_tick = null, object terror_buildup = null)
    {
        var fire_hallucination = fire_hallucination_weakref?.resolve() as datum_hallucination_fire;
        if (Dm.truthy(owner.on_fire) || !QDELETED(fire_hallucination))
        {
            owner.clear_mood_event("pyrophobia"); // they have the "ON FIRE" moodlet
            return max(Dm.num(owner.fire_stacks), fire_hallucination.fake_firestacks) * 2;
        }

        float fire_size = Dm.num(owner.count_nearby_fire_sources());
        if (fire_size != last_fire_size)
        {
            if (fire_size >= 1)
                await owner.add_mood_event("pyrophobia", typeof(datum_mood_event_pyrophobia), round(fire_size));
            else
                owner.clear_mood_event("pyrophobia");
        }
        last_fire_size = fire_size;
        // 40 -> 15 seconds to reach passive cap
        return min(round(fire_size * 3, 0.1f), 40);
    }

    // tg: code/datums/components/fearful/sources/_sources.dm:196
    [SignalHandler]
    public virtual void on_hallucination(datum source = null, datum_hallucination hallucination = null)
    {
        if (hallucination is datum_hallucination_fire)
            fire_hallucination_weakref = WEAKREF(hallucination) as datum_weakref;
    }
}

/// While stop drop and rolling you lose a tiny bit of terror
// tg: code/datums/components/fearful/sources/_sources.dm:203
public partial class datum_terror_handler_is_stop_drop_rolling : datum_terror_handler
{
    // tg: code/datums/components/fearful/sources/_sources.dm:205
    public override async DmTask<object> tick(object seconds_per_tick = null, object terror_buildup = null)
    {
        if (Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_stop_drop_roll))))
            return -0.2f * TERROR_BUILDUP_PASSIVE_DECREASE * Dm.num(seconds_per_tick); // reduces terror a bit because you think it's helping!
        return 0;
    }
}
