using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Fishing: a rod cast at a fishing spot (a toilet, a moisture trap, the water, lava, a chasm, a hydroponics tray) throws its
/// bobber and starts the challenge; the bite, the minigame's bait and fish, winning it for the catch and losing it; what the line,
/// the hook and the bait change; every fish source's loot; the fish-porter's dimensions and its links; the fish scans that unlock
/// its settings; the rod's window; the fishing capsule's spring; and a sweep over the newly live fishing types.
/// </summary>
public class FishingTests
{
    private const float WAIT_PHASE = 1;
    private const float BITING_PHASE = 2;
    private const float MINIGAME_PHASE = 3;

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int w = 13, int h = 9)
    {
        var r = new AudioRoom(w, h, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => r.World.Rng.Reseed(seed, 1));
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T thing = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(thing)))));
        return thing;
    }

    /// <summary>A station toilet: tg gives only the toilets mapped on a station level a fishing spot, so a test adds its toilet's.</summary>
    private static obj_structure_toilet Toilet(AudioRoom r, int x, int y)
    {
        var toilet = Place<obj_structure_toilet>(r, x, y);
        r.In(() => Waiting.NoSleep(toilet.AddComponent(typeof(datum_component_fishing_spot), GLOB.preset_fish_sources[typeof(datum_fish_source_toilet)])));
        return toilet;
    }

    private static datum_fishing_challenge Challenge(AudioRoom r, mob who)
        => r.In(() => GLOB.fishing_challenges_by_user[who] as datum_fishing_challenge);

    /// <summary>Casts the held rod at the spot and waits for the bobber to land and the challenge to begin.</summary>
    private static datum_fishing_challenge Cast(AudioRoom r, mob_living_carbon_human bob, atom spot)
    {
        Click(r, bob, spot);
        datum_fishing_challenge challenge = null;
        for (int i = 0; i < 4 * Second && challenge == null; i++)
        {
            r.Step();
            challenge = Challenge(r, bob);
        }
        Assert.True(challenge != null, "no fishing challenge began");
        return challenge;
    }

    /// <summary>Waits for the bite and clicks, which starts the minigame (or the catch, when the difficulty is crushed).</summary>
    private static void Strike(AudioRoom r, mob_living_carbon_human bob, datum_fishing_challenge challenge, atom spot)
    {
        bool biting = false;
        for (int i = 0; i < 40 * Second && !biting; i++)
        {
            r.Step();
            biting = r.In(() => challenge.phase == BITING_PHASE);
        }
        Assert.True(biting, "nothing bit");
        Click(r, bob, spot);
        r.Step(2);
    }

    /// <summary>Plays the minigame: the bait kept on the fish (win) or away from it (lose), until the challenge ends.</summary>
    private static void Play(AudioRoom r, datum_fishing_challenge challenge, bool win)
    {
        bool over = false;
        for (int i = 0; i < 90 * Second && !over; i++)
        {
            r.In(() =>
            {
                if (QDELETED(challenge) || challenge.phase != MINIGAME_PHASE)
                    return;
                challenge.bait_position = win
                    ? max(0, challenge.fish_position - (challenge.bait_height - challenge.fish_height) / 2)
                    : (challenge.fish_position > 500 ? 0 : 1000 - challenge.bait_height);
                challenge.bait_velocity = 0;
            });
            r.Step();
            over = r.In(() => QDELETED(challenge));
        }
        Assert.True(over, "the minigame never ended");
    }

    [Fact]
    public void A_cast_at_a_toilet_throws_the_bobber_and_starts_the_challenge()
    {
        var (r, _, bob) = Room(14701);
        var rod = Hold<obj_item_fishing_rod>(r, bob);
        var toilet = Toilet(r, 6, 4);
        var challenge = Cast(r, bob, toilet);
        r.In(() =>
        {
            Assert.Same(rod, challenge.used_rod);
            Assert.Equal(WAIT_PHASE, challenge.phase);
            Assert.NotNull(rod.fishing_line);
            Assert.NotNull(challenge.@float);
            Assert.Same(toilet.loc, challenge.@float.loc);
            Assert.Equal("fishing_line", rod.fishing_line.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Winning_the_minigame_lands_the_catch()
    {
        var (r, feed, bob) = Room(14702);
        Hold<obj_item_fishing_rod>(r, bob);
        var toilet = Toilet(r, 6, 4);
        var challenge = Cast(r, bob, toilet);
        Strike(r, bob, challenge, toilet);
        r.In(() =>
        {
            Assert.Equal(MINIGAME_PHASE, challenge.phase);
            Assert.NotNull(challenge.fishing_hud);
            Assert.Contains(challenge.fishing_hud, Dm.each_cast<atom>(bob.client.screen));
            // the reward the bite rolled is replaced by a fish, so the catch is known
            challenge.reward_path = typeof(obj_item_fish_sludgefish);
        });
        Play(r, challenge, win: true);
        r.Step(Second);
        r.In(() =>
        {
            var fish = Dm.each_cast<atom>(bob.contents).OfType<obj_item_fish_sludgefish>().FirstOrDefault()
                ?? Dm.each_cast<atom>(range(2, bob)).OfType<obj_item_fish_sludgefish>().FirstOrDefault();
            Assert.True(fish != null, "no sludgefish was caught");
            Assert.Null(GLOB.fishing_challenges_by_user[bob]);
            Assert.True(Dm.num(bob.mind.get_skill_level(typeof(datum_skill_fishing))) >= 1, "fishing gives fishing experience");
        });
        Assert.True(feed.Any("sludgefish"), "the catch is named");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Losing_the_minigame_lets_the_fish_go()
    {
        var (r, _, bob) = Room(14703);
        var rod = Hold<obj_item_fishing_rod>(r, bob);
        var toilet = Toilet(r, 6, 4);
        var challenge = Cast(r, bob, toilet);
        Strike(r, bob, challenge, toilet);
        r.In(() =>
        {
            Assert.Equal(MINIGAME_PHASE, challenge.phase);
            challenge.reward_path = typeof(obj_item_fish_sludgefish);
        });
        Play(r, challenge, win: false);
        r.Step(Second);
        r.In(() =>
        {
            Assert.Empty(Dm.each_cast<atom>(range(3, bob)).OfType<obj_item_fish_sludgefish>());
            Assert.Null(rod.fishing_line);
            Assert.DoesNotContain(Dm.each_cast<atom>(bob.client.screen), a => a is atom_movable_screen_fishing_hud);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Clicking_before_the_bite_is_a_miss()
    {
        var (r, feed, bob) = Room(14704);
        Hold<obj_item_fishing_rod>(r, bob);
        var toilet = Toilet(r, 6, 4);
        var challenge = Cast(r, bob, toilet);
        r.Step(Second);
        Click(r, bob, toilet);
        r.In(() => Assert.Equal(WAIT_PHASE, challenge.phase));
        Assert.True(feed.Any("miss!"), "a click before the bite misses");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Lava_needs_a_reinforced_line_and_a_chasm_takes_the_hook()
    {
        var (r, feed, bob) = Room(14705);
        var rod = Hold<obj_item_fishing_rod>(r, bob);
        r.In(() =>
        {
            var lava = GLOB.preset_fish_sources[typeof(datum_fish_source_lavaland)] as datum_fish_source;
            Assert.NotNull(lava.reason_we_cant_fish(rod, bob, bob.loc));
            rod.set_slot(Dm.@new<obj_item_fishing_line_reinforced>(rod), ROD_SLOT_LINE);
            Assert.True(HAS_TRAIT(rod, TRAIT_ROD_LAVA_USABLE), "the reinforced line makes the rod lava-proof");
            Assert.Null(lava.reason_we_cant_fish(rod, bob, bob.loc));

            var chasm = GLOB.preset_fish_sources[typeof(datum_fish_source_chasm)] as datum_fish_source;
            chasm.on_start_fishing(rod, bob, bob.loc);
        });
        Assert.True(feed.Any("stuck on the wall of the chasm"), "a plain hook catches on the chasm's wall");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_hook_and_the_bait_change_the_odds()
    {
        var (r, _, bob) = Room(14706);
        var rod = Hold<obj_item_fishing_rod>(r, bob);
        var toilet = Toilet(r, 6, 4);
        r.In(() =>
        {
            var source = GLOB.preset_fish_sources[typeof(datum_fish_source_toilet)] as datum_fish_source;
            DmList plain = Waiting.NoSleep(source.get_modified_fish_table(rod, bob, toilet)) as DmList;
            Assert.True(Dm.num(plain[FISHING_DUD]) > 0, "a plain rod fishes up duds");

            rod.set_slot(Dm.@new<obj_item_fishing_hook_magnet>(rod), ROD_SLOT_HOOK);
            DmList magnet = Waiting.NoSleep(source.get_modified_fish_table(rod, bob, toilet)) as DmList;
            Assert.True(Dm.num(magnet[typeof(obj_item_storage_wallet_money)]) > Dm.num(plain[typeof(obj_item_storage_wallet_money)]),
                "the magnet hook favours what is not a fish");

            var worm = Dm.@new<obj_item_food_bait_worm>(rod);
            rod.set_slot(worm, ROD_SLOT_BAIT);
            Assert.Same(worm, rod.bait_value);
            Assert.True(Dm.num(source.calculate_difficulty(typeof(obj_item_fish_sludgefish), rod, bob)) > 0);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Every_fish_source_gives_its_loot()
    {
        var (r, _, bob) = Room(14707);
        var spot = r.In(() => locate(8, 4, 1) as turf);
        List<(Type Source, object Reward)> rewards = r.In(() =>
        {
            var all = new List<(Type, object)>();
            foreach (object key in Dm.each(GLOB.preset_fish_sources))
            {
                var source = GLOB.preset_fish_sources[key] as datum_fish_source;
                foreach (object reward in Dm.each(source.fish_table))
                    all.Add(((Type)key, reward));
            }
            return all;
        });
        Assert.True(rewards.Count > 100, "the fish sources' tables are filled");
        int landed = 0;
        foreach (var (sourceType, reward) in rewards)
        {
            if (reward is not Type rewardType)
                continue; // the duds, the random seeds, organs and arms, the influences: their sources' own tests
            r.In(() =>
            {
                var source = GLOB.preset_fish_sources[sourceType] as datum_fish_source;
                Waiting.Detach(source.spawn_reward(rewardType, spot, spot));
                // the catch is cleared at once: the mobs among the loot (lobstrosities, brimdemons) would otherwise roam the room
                foreach (atom_movable caught in Dm.each(range(1, spot)).OfType<atom_movable>().Where(a => a != bob && a is obj or mob).ToList())
                {
                    landed++;
                    qdel(caught);
                }
            });
            r.Step(1);
        }
        Assert.True(landed > 100, "the loot was spawned: " + landed);
        r.Step(Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_fish_porter_opens_a_portal_and_links_a_spot()
    {
        var (r, feed, bob) = Room(14708);
        var porter = Place<obj_machinery_fishing_portal_generator>(r, 6, 4);
        var toilet = Toilet(r, 8, 6);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(porter, TRAIT_UNLINKABLE_FISHING_SPOT));
            Waiting.NoSleep(porter.activate(GLOB.preset_fish_sources[typeof(datum_fish_source_portal)] as datum_fish_source, bob));
            Assert.NotNull(porter.active);
            Assert.True(HAS_TRAIT(porter, TRAIT_FISHING_SPOT), "the open portal is a fishing spot");
        });
        r.Step(2);
        r.In(() =>
        {
            Waiting.NoSleep(porter.deactivate());
            Assert.Null(porter.active);
            Assert.False(HAS_TRAIT(porter, TRAIT_FISHING_SPOT));

            var source = GLOB.preset_fish_sources[typeof(datum_fish_source_toilet)] as datum_fish_source;
            Assert.Equal(ITEM_INTERACT_SUCCESS, porter.link_fishing_spot(source, toilet, bob));
            Assert.Same(source, porter.linked_fishing_spots[toilet]);
            Waiting.NoSleep(porter.activate(source, bob));
            Assert.Same(toilet, porter.current_linked_atom);
            Waiting.NoSleep(porter.deactivate());
            Waiting.NoSleep(porter.emag_act(bob, null));
            Assert.True(Dm.truthy(Dm.bitand(porter.obj_flags, EMAGGED)));
        });
        Assert.True(feed.Any("fishing spot linked"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_fish_porter_offers_the_dimensions_its_scans_unlocked()
    {
        var (r, _, bob) = Room(14709);
        var porter = Place<obj_machinery_fishing_portal_generator>(r, 5, 4);
        // only the aquarium dimension: it opens without a menu
        r.In(() => Waiting.Detach(porter.select_fish_source(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.IsType<datum_fish_source_portal>(porter.active.fish_source);
            Waiting.NoSleep(porter.deactivate());
            Waiting.NoSleep(porter.emag_act(bob, null));
        });
        // emagged: the syndicate dimension joins, so a radial menu asks
        r.In(() => Waiting.Detach(porter.select_fish_source(bob)));
        r.Step(2);
        (long id, int index) = r.In(() =>
        {
            var menu = Dm.each_cast<datum_radial_menu>(bob.client.dm_radial_menus).Single();
            int i = menu.dm_elements.FindIndex(e => e.Name != null && e.Name.Contains("Syndicate", StringComparison.Ordinal));
            Assert.True(i >= 0);
            return (menu.dm_id, i + 1);
        });
        r.Command(() => ClientSeam.RadialPick(bob.client, id, index));
        r.Step(4);
        r.In(() => Assert.IsType<datum_fish_source_portal_syndicate>(porter.active?.fish_source));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_rod_window_swaps_the_bait()
    {
        var (r, _, bob) = Room(14710);
        var rod = Hold<obj_item_fishing_rod>(r, bob);
        Take(bob);
        r.In(() => Waiting.Detach(rod.ui_interact(bob)));
        r.Step(4);
        var payload = LastFullUpdate(Take(bob), out string window);
        Assert.Equal("FishingRod", InterfaceOf(payload));
        Assert.Equal("None", payload.GetProperty("data").GetProperty("bait_name").ValueKind == System.Text.Json.JsonValueKind.Null ? "None" : "set");
        // a worm in the other hand goes on as bait
        var worm = r.In(() => Dm.@new<obj_item_food_bait_worm>(bob.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_inactive_hand(worm)))));
        r.In(() => { bob.swap_hand(); });
        r.Step(2);
        Act(r, bob, window, "slot_action", ("slot", "bait"));
        r.In(() => Assert.Same(worm, rod.bait_value));
        var data = Data(r, bob, out _);
        Assert.Equal("worm", data.GetProperty("bait_name").GetString());
        Assert.Equal("icons/obj/fishing.dmi", data.GetProperty("bait_icon").GetProperty("icon").GetString());
        r.AssertNoRuntimes();
    }

    /// The converted fishing spots (maps/templates/fishing_*.as3dmap) at tg's paths, as the host mounts them (MapSeam).
    private static void MountFishingSpots(World w)
    {
        IDmFileSystem files = DmWorldState.Of(w).Files;
        foreach (string converted in Directory.GetFiles(Path.Combine(MapFixtures.RepoRoot(), "maps", "templates"), "fishing_*" + MapText.Extension))
            files.WriteText("_maps/templates/" + Path.GetFileNameWithoutExtension(converted) + ".dmm", File.ReadAllText(converted));
    }

    [Fact]
    public void The_fishing_capsule_unfolds_a_freshwater_spring()
    {
        var r = new AudioRoom(21, 21, seed: 14712, configure: MountFishingSpots);
        Feed feed = Listen(r);
        var bob = r.Join(4, 10, "Bob");
        r.In(() =>
        {
            r.World.Rng.Reseed(14712, 1);
            SSmapping.preloadShelterTemplates();
            Assert.Equal(new[] { "fishing_beach", "fishing_default", "fishing_depths", "fishing_hot_spring", "fishing_ice", "fishing_lava", "fishing_plasma", "fishing_tizira" },
                Dm.each(SSmapping.shelter_templates).Select(k => Dm.str(k)).Where(k => k.StartsWith("fishing_", StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToArray());
        });
        r.Step(Second);
        var capsule = Hold<obj_item_survivalcapsule_fishing>(r, bob);
        r.In(() => Waiting.NoSleep(capsule.attack_self(bob)));
        r.In(() => Waiting.NoSleep(capsule.forceMove(locate(10, 10, 1))));
        r.Step(5 * Second + 4);
        r.In(() =>
        {
            Assert.True(QDELETED(capsule), "the capsule is spent: " + string.Join(" | ", feed.Chat.Select(c => c.Html)));
            var water = Dm.each(range(3, locate(10, 10, 1))).OfType<turf>().OfType<turf_open_water>().ToList();
            Assert.NotEmpty(water);
            Assert.All(water, t => Assert.True(HAS_TRAIT(t, TRAIT_FISHING_SPOT), "the spring's water is a fishing spot"));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Every_newly_live_fishing_type_is_made_and_deleted()
    {
        var (r, _, _) = Room(14711);
        Type[] roots =
        [
            typeof(obj_item_fishing_rod), typeof(obj_item_fishing_line), typeof(obj_item_fishing_hook), typeof(obj_item_fishing_lure),
            typeof(obj_item_bait_can), typeof(obj_machinery_fishing_portal_generator), typeof(obj_item_fish_gene),
            typeof(obj_item_fish_genegun), typeof(obj_item_storage_bag_fishing),
        ];
        var types = roots.SelectMany(t => r.In(() => Dm.each_cast<Type>(typesof(t)).ToList())).Distinct().ToList();
        foreach (Type type in types)
        {
            atom made = r.In(() => Dm.@new(type, locate(8, 4, 1)) as atom);
            r.Step(2);
            r.In(() => qdel(made));
            r.Step(2);
        }
        r.AssertNoRuntimes();
    }
}
