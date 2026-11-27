// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/sources/subtypes/fishing_portal_machine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the autowiki's table (a live-partial row): the fishing portal generator's dimensions. The beach and the
// ocean wash up messages in bottles; the randomizer pools every other portal's catch, skews the minigame and gives its fish
// random traits. The var blocks are generated (D111).

// tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:20
public partial class datum_fish_source_portal_beach : datum_fish_source_portal
{
    // tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:40
    public override void on_fishing_spot_init(datum_component_fishing_spot spot = null)
    {
        ADD_TRAIT(spot.parent, TRAIT_MESSAGE_IN_A_BOTTLE_LOCATION, INNATE_TRAIT);
    }

    // tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:43
    public override void on_fishing_spot_del(datum_component_fishing_spot spot = null)
    {
        REMOVE_TRAIT(spot.parent, TRAIT_MESSAGE_IN_A_BOTTLE_LOCATION, INNATE_TRAIT);
    }
}

// tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:60
public partial class datum_fish_source_portal_ocean : datum_fish_source_portal
{
    // tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:88
    public override void on_fishing_spot_init(datum_component_fishing_spot spot = null)
    {
        ADD_TRAIT(spot.parent, TRAIT_MESSAGE_IN_A_BOTTLE_LOCATION, INNATE_TRAIT);
    }

    // tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:91
    public override void on_fishing_spot_del(datum_component_fishing_spot spot = null)
    {
        REMOVE_TRAIT(spot.parent, TRAIT_MESSAGE_IN_A_BOTTLE_LOCATION, INNATE_TRAIT);
    }
}

/**
 * A special portal fish source which fish table is populated on init with the contents of all
 * portal fish sources, except for FISHING_DUD, and a couple more caveats.
 */
// tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:146
public partial class datum_fish_source_portal_random : datum_fish_source_portal
{
    ///Generate the fish table if we don't have one already.
    // tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:156
    public override void on_fishing_spot_init(datum_component_fishing_spot spot = null)
    {
        if (TgState.Current.datum_fish_source_portal_random__all_portal_fish_sources_at_once != null)
        {
            fish_table = TgState.Current.datum_fish_source_portal_random__all_portal_fish_sources_at_once;
            return;
        }

        ///rewards not found in other fishing portals
        fish_table = Dm.list(Dm.kv(typeof(obj_item_fish_holo_checkered), 1));

        foreach (object portal_type in Dm.each<object>(GLOB.preset_fish_sources))
        {
            if (Dm.eq(portal_type, type) || !ispath(portal_type, typeof(datum_fish_source_portal)))
                continue;
            datum_fish_source_portal preset_portal = GLOB.preset_fish_sources[portal_type] as datum_fish_source_portal;
            foreach (object reward_path in Dm.each<object>(preset_portal.fish_table))
            {
                if (Dm.eq(reward_path, FISHING_DUD)) // We don't serve duds.
                    continue;
                fish_table[reward_path] = rand(1, 4);
            }
        }

        TgState.Current.datum_fish_source_portal_random__all_portal_fish_sources_at_once = fish_table;
    }

    ///Difficulty has to be calculated before the rest, because of how it influences jump chances
    // tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:177
    public override object calculate_difficulty(object result = null, obj_item_fishing_rod rod = null, mob fisherman = null, params object[] __extra)
    {
        float dot = Dm.num(base.calculate_difficulty(result, rod, fisherman, __extra));
        dot += rand(-10, 15);
        return dot;
    }

    ///In the spirit of randomness, we skew a few values here and there
    // tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:182
    public override void pre_challenge_started(obj_item_fishing_rod rod = null, mob user = null, datum_fishing_challenge challenge = null)
    {
        challenge.bait_bounce_mult = max(challenge.bait_bounce_mult + rand(-3, 3) * 0.1f, 0.1f);
        challenge.completion_loss = max(challenge.completion_loss + rand(-2, 2), 0);
        challenge.completion_gain = max(challenge.completion_gain + rand(-1, 1), 2);
        challenge.mover.short_jump_velocity_limit += rand(-100, 100);
        challenge.mover.long_jump_velocity_limit += rand(-100, 100);
        DmList active_effects = TgState.Current.datum_fish_source_portal_random__active_effects ??= bitfield_to_list(FISHING_MINIGAME_ACTIVE_EFFECTS) as DmList;
        foreach (object effect in Dm.each<object>(active_effects))
        {
            if (prob(30))
                Dm.combine(ref challenge.special_effects, effect);
        }
        RegisterSignal(challenge, COMSIG_FISHING_CHALLENGE_MOVER_INITIALIZED, nameof(randomize_mover_velocity));
    }

    // tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:195
    [SignalHandler]
    public virtual void randomize_mover_velocity(datum source = null, datum_fish_movement mover = null)
    {
        mover.short_jump_velocity_limit += rand(-100, 100);
        mover.long_jump_velocity_limit += rand(-100, 100);
    }

    ///Cherry on top, fish caught from the randomizer portal also have (almost completely) random traits
    // tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:201
    public override async DmTask<object> spawn_reward(object reward_path = null, atom spawn_location = null, atom fishing_spot = null, params object[] __extra)
    {
        if (!ispath(reward_path, typeof(obj_item_fish)))
            return await base.spawn_reward(reward_path, spawn_location, fishing_spot, __extra);

        DmList weighted_traits = TgState.Current.datum_fish_source_portal_random__weighted_traits;
        if (!Dm.truthy(weighted_traits))
        {
            weighted_traits = TgState.Current.datum_fish_source_portal_random__weighted_traits = Dm.list();
            foreach (object trait_type in Dm.each<object>(GLOB.fish_traits))
            {
                datum_fish_trait trait = GLOB.fish_traits[trait_type] as datum_fish_trait;
                weighted_traits[trait.type] = round(Dm.pow(Dm.num(trait.inheritability), 2) / 100);
            }
        }

        obj_item_fish caught_fish = await Dm.new_async((Type)reward_path, spawn_location, FALSE) as obj_item_fish;
        DmList new_traits = Dm.list();
        float iterations = rand(1, 4);
        for (float iteration = 1; iteration <= iterations; iteration++)
            Dm.combine(ref new_traits, pick_weight(weighted_traits));
        await caught_fish.inherit_traits(new_traits);
        await caught_fish.randomize_size_and_weight(deviation: 0.3f);
        return caught_fish;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:154  var/static/list/all_portal_fish_sources_at_once
    internal DmList datum_fish_source_portal_random__all_portal_fish_sources_at_once;
    // tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:190  var/static/list/active_effects
    internal DmList datum_fish_source_portal_random__active_effects;
    // tg: code/modules/fishing/sources/subtypes/fishing_portal_machine.dm:205  var/static/list/weighted_traits
    internal DmList datum_fish_source_portal_random__weighted_traits;
}
