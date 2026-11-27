using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Bitrunning's virtual domains and the glitch in a booted toy room with a quantum server and a byteforge: every domain the port hosts
/// loads from its converted map with its mobs live; the domains' secondary objectives (the beach bar's drinks, the breeze bay's fish,
/// the island brawl's deaths, the heretic hunt's sacrifices); the domains' mobs (the revolutionaries, the aliens, the zombies, the crate
/// mimics, the clowns, the fake crewmen) and the glacier grind's cliffs; the server's threat spawning a cyber police for a ghost, the
/// cyber tactical unit and the netguardian prime; the glitch event; and every newly live type created and deleted.
/// </summary>
public class BitrunningDomainTests
{
    private const int W = 128, H = 128;
    private const int ServerX = 6, ServerY = 6;
    private readonly ITestOutputHelper _output;

    public BitrunningDomainTests(ITestOutputHelper output) => _output = output;

    private sealed record Den(AudioRoom R, Feed Feed, obj_machinery_quantum_server Server, obj_machinery_byteforge Forge, mob_living_carbon_human Bob);

    /// The domain maps and safehouses the host mounts from the converted maps (MapSeam), the safehouse rooms' toml and the hacker
    /// and agent names (TgStrings), before the boot.
    private static void Mount(World w)
    {
        IDmFileSystem files = DmWorldState.Of(w).Files;
        foreach (string folder in new[] { "virtual_domains", "safehouses" })
            foreach (string converted in Directory.GetFiles(Path.Combine(MapFixtures.RepoRoot(), "maps", folder), "*" + MapText.Extension))
                files.WriteText("_maps/" + folder + "/" + Path.GetFileNameWithoutExtension(converted) + ".dmm", File.ReadAllText(converted));
        TgStrings.Mount(files, TgClone.Root, new[] { "strings/modular_maps/safehouse.toml", "strings/names/hackers.txt", "strings/names/cyberauth.txt" });
    }

    private static string Why(Den d) => " | " + string.Join(" | ", d.Feed.Chat.Select(c => c.Html)) + "\n"
        + string.Join("\n---\n", d.R.Room.RuntimeText.Select(t => t.Length > 2000 ? t[..2000] : t));

    private static Den Room(ulong seed, int size = W)
    {
        var r = new AudioRoom(size, size, seed: seed, configure: Mount);
        Feed feed = Listen(r);
        r.In(() => (get_area(locate(ServerX, ServerY, 1)) as area).requires_power = FALSE);
        r.In(() =>
        {
            SSmapping.z_list = Dm.list();
            SSmapping.z_level_to_plane_offset ??= Dm.list();
            SSmapping.z_level_to_lowest_plane_offset ??= Dm.list();
            SSmapping.gravity_by_z_level ??= Dm.list();
            SSmapping.z_level_to_stack ??= Dm.list();
            SSmapping.manage_z_level(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))), FALSE, contain_turfs: FALSE);
            // a station map always has the near-station space area that a turf built on deep space joins (space.dm:200); the toy room has none
            Dm.@new<area_space_nearstation>();
        });
        var server = r.In(() => Dm.@new<obj_machinery_quantum_server>(locate(ServerX, ServerY, 1)));
        r.Step(2);
        var forge = r.In(() => Dm.@new<obj_machinery_byteforge>(locate(ServerX, ServerY - 2, 1)));
        mob_living_carbon_human bob = r.Join(ServerX + 2, ServerY + 2, "Bob");
        r.Step(Second);
        return new Den(r, feed, server, forge, bob);
    }

    /// The domain loaded by the server (enough points for any), its loaded domain out.
    private static datum_lazy_template_virtual_domain Load(Den d, string key)
    {
        d.R.In(() => d.Server.points = 100);
        d.R.In(() => d.Server.glitch_chance = 0); // no glitch from the threat roll unless a test asks for one
        d.R.In(() => Waiting.Detach(d.Server.cold_boot_map(key, FALSE)));
        for (int i = 0; i < 40 && !d.R.In(() => Dm.truthy(d.Server.is_ready) && d.Server.generated_domain != null); i++)
            d.R.Step(10);
        Assert.True(d.R.In(() => Dm.str(d.Server.generated_domain?.key)) == key, key + " did not load" + Why(d));
        Assert.False(d.R.Room.RuntimeText.Any(t => t.Contains("Failed to reserve")), key + " found no room" + Why(d));
        return d.R.In(() => d.Server.generated_domain);
    }

    /// No runtimes but tg's own: the food cart's packed table, nullspaced in the cart, is queued for smoothing on its
    /// Initialize (structures.dm:23) and SSicon_smooth's fire crashes on it (icon_smoothing.dm:138), as in tg.
    private static void AssertNoRuntimes(Den d)
    {
        d.R.Room.Runtimes.RemoveAll(e => e.Message.Contains("/obj/structure/table/reinforced called smooth_icon() without being on a z-level", StringComparison.Ordinal));
        d.R.AssertNoRuntimes();
    }

    private static List<T> InDomain<T>(Den d) where T : atom => d.R.In(() => Dm.each<T>().Where(a => is_area_virtual(get_area(a))).ToList());

    /// A player who joins on (x, y) and ghosts: their client is on the observer.
    private static mob_dead_observer Ghost(AudioRoom r, int x, int y, string key)
    {
        mob_living_carbon_human body = r.Join(x, y, key);
        client player = body.client;
        r.In(() => body.ghostize(TRUE));
        r.Step(2);
        return Assert.IsType<mob_dead_observer>(player.mob);
    }

    private static atom_movable_screen_alert_poll_alert AlertOf(AudioRoom r, mob who)
        => r.In(() => Dm.each(who.alerts).Select(k => who.alerts[k]).OfType<atom_movable_screen_alert_poll_alert>().SingleOrDefault());

    /// The server's threat spawning a glitch of the role for Gina, a ghost who signs up for it: the glitch's mob out.
    private static mob_living SpawnGlitch(Den d, Type role, string key = "Gina")
    {
        mob_dead_observer gina = Ghost(d.R, ServerX + 4, ServerY, key);
        client ginas = d.R.In(() => gina.client);
        d.R.In(() => Waiting.Detach(d.Server.setup_glitch(role)));
        d.R.Step(2);
        atom_movable_screen_alert_poll_alert alert = AlertOf(d.R, gina);
        Assert.True(alert != null, "no poll" + Why(d));
        Click(d.R, gina, alert);
        d.R.Step(21 * Second); // the poll's 20 seconds
        mob_living glitch = d.R.In(() => ginas.mob as mob_living);
        Assert.True(glitch != null, "the ghost is not the glitch" + Why(d));
        return glitch;
    }

    // tg's virtual domains the port hosts; the six megafauna domains wait for Lavaland's mobs, the island brawl needs a room of its own
    public static IEnumerable<object[]> Domains() => new[]
    {
        "abductor_ship", "beach_bar", "breeze_bay", "clown_planet", "fredingtonfastingbear", "heretic_hunt", "meta_central", "pipedream",
        "pirates", "psyker_shuffle", "psyker_zombies", "stairs_and_cliffs", "syndicate_assault", "vaporwave", "xeno_nest",
    }.Select(k => new object[] { k });

    [Fact]
    public void TheDomainsMobsAndGlitchesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_lazy_template_virtual_domain_beach_bar), typeof(datum_lazy_template_virtual_domain_breeze_bay),
            typeof(datum_lazy_template_virtual_domain_island_brawl), typeof(datum_lazy_template_virtual_domain_heretic_hunt),
            typeof(datum_lazy_template_virtual_domain_meta_central), typeof(datum_lazy_template_virtual_domain_xeno_nest),
            typeof(mob_living_basic_fake_crewman_sec), typeof(mob_living_basic_fake_crewman_boss_ce), typeof(mob_living_basic_fake_crewman_boss_hos),
            typeof(mob_living_basic_revolutionary), typeof(mob_living_basic_alien), typeof(mob_living_basic_alien_sentinel),
            typeof(mob_living_basic_alien_drone), typeof(mob_living_basic_alien_queen_large), typeof(mob_living_basic_alien_maid),
            typeof(mob_living_basic_zombie), typeof(mob_living_basic_zombie_rotten), typeof(mob_living_basic_mimic_crate), typeof(mob_living_basic_clown),
            typeof(mob_living_basic_clown_honkling), typeof(mob_living_basic_clown_clownhulk_honkmunculus), typeof(mob_living_basic_netguardian),
            typeof(datum_antagonist_bitrunning_glitch), typeof(datum_antagonist_bitrunning_glitch_cyber_police), typeof(datum_antagonist_bitrunning_glitch_cyber_tac),
            typeof(datum_antagonist_bitrunning_glitch_netguardian), typeof(datum_antagonist_domain_ghost_actor), typeof(datum_outfit_cyber_police),
            typeof(datum_outfit_cyber_police_tactical), typeof(datum_outfit_virtual_domain_heretic), typeof(datum_round_event_control_bitrunning_glitch),
            typeof(datum_round_event_ghost_role_bitrunning_glitch), typeof(datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire_netguardian),
            typeof(obj_effect_mob_spawn_corpse_human_cyber_police), typeof(obj_effect_mob_spawn_corpse_human_bitrunner), typeof(datum_status_effect_gutted),
            typeof(datum_element_rust_healing), typeof(obj_structure_closet_crate_eva), typeof(turf_open_cliff_snowrock_virtual_domain),
            typeof(datum_controller_subsystem_movement_cliff_falling), typeof(datum_bt_node_ai_behavior_plant_alien_weeds),
            typeof(datum_bt_node_ai_behavior_lay_alien_egg),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Theory]
    [MemberData(nameof(Domains))]
    public void EachDomainLoadsWithItsMobsLive(string key)
    {
        Den d = Room(10560);
        var before = d.R.In(() => Dm.each<mob_living>().ToHashSet());
        Load(d, key);
        d.R.Step(2 * Second);
        var mobs = d.R.In(() => Dm.each<mob_living>().Where(m => !before.Contains(m)).ToList());
        _output.WriteLine($"{key}: {d.R.In(() => d.Server.exit_turfs.len)} exits, mobs " + string.Join(", ", mobs.GroupBy(m => m.GetType().Name).Select(g => g.Key + " x" + g.Count())));
        Assert.True(d.R.In(() => d.Server.exit_turfs.len) >= 1, key + ": no exit");
        // every mob of the domain runs its own code, but the heretic's summons, which wait for the heretic
        Assert.All(mobs.Where(m => m is not mob_living_basic_heretic_summon), m => Assert.False(InertSeam.IsInert(m), key + ": " + m.GetType().Name + " is inert"));
        // the converted map's own mobs: none lost on the way
        int expected = key switch
        {
            "xeno_nest" => 6, "psyker_zombies" => 16, "psyker_shuffle" => 5, "clown_planet" => 4, "heretic_hunt" => 9, "meta_central" => 1, _ => 0,
        };
        Type kind = key switch
        {
            "xeno_nest" => typeof(mob_living_basic_alien), "psyker_zombies" => typeof(mob_living_basic_zombie), "psyker_shuffle" => typeof(mob_living_basic_mimic_crate),
            "clown_planet" => typeof(mob_living_basic_clown), "heretic_hunt" => typeof(mob_living_basic_fake_crewman), "meta_central" => typeof(mob_living_basic_revolutionary),
            _ => typeof(mob_living),
        };
        Assert.True(mobs.Count(m => kind.IsInstanceOfType(m)) >= expected, key + ": " + mobs.Count(m => kind.IsInstanceOfType(m)) + " " + kind.Name);
        AssertNoRuntimes(d);
        d.R.In(() => d.Server.reset(fast: TRUE));
        d.R.Step(Second);
        Assert.Null(d.R.In(() => d.Server.generated_domain));
    }

    [Fact]
    public void TheIslandBrawlsDeathsAreItsPoints()
    {
        Den d = Room(10561, 192); // the island is 98 by 80 turfs
        var domain = (datum_lazy_template_virtual_domain_island_brawl)Load(d, "island_brawl");
        // the islanders' spawners are the domain's custom spawns
        int spawners = InDomain<obj_effect_mob_spawn_ghost_role_human_virtual_domain_islander>(d).Count;
        Assert.True(spawners > 0);
        Assert.Equal(spawners, d.R.In(() => Dm.length(domain.custom_spawns)));
        // a bitrunner spawned from one of them, then killed: a point towards the main crate
        var spawner = InDomain<obj_effect_mob_spawn_ghost_role_human_virtual_domain_islander>(d).First();
        var runner = d.R.In(() => Dm.@new<mob_living_carbon_human>(get_turf(spawner)));
        d.R.In(() => spawner.artificial_spawn(runner));
        float points = d.R.In(() => domain.main_crate_points);
        d.R.In(() => Waiting.NoSleep(runner.death(FALSE)));
        Assert.Equal(points + 1, d.R.In(() => domain.main_crate_points));
        // a spawner deleted leaves the custom spawns
        d.R.In(() => qdel(spawner));
        Assert.Equal(spawners - 1, d.R.In(() => Dm.length(domain.custom_spawns)));
        AssertNoRuntimes(d);
    }

    [Fact]
    public void TheBeachBarsDrinksAndTheBreezeBaysFishRevealTheCrate()
    {
        Den d = Room(10562);
        var bar = (datum_lazy_template_virtual_domain_beach_bar)Load(d, "beach_bar");
        var glass = InDomain<obj_item_reagent_containers_cup_glass>(d).First();
        d.R.In(() => Waiting.NoSleep(((datum_reagents)glass.reagents).add_reagent(typeof(datum_reagent_water), 10)));
        d.R.In(() => Waiting.NoSleep(glass.try_drink(d.Bob, d.Bob)));
        d.R.Step(Second);
        Assert.Equal(0.5f, d.R.In(() => bar.main_crate_points));
        d.R.In(() => d.Server.reset(fast: TRUE));
        d.R.In(() => Waiting.Detach(d.Server.cool_off()));
        d.R.Step(Second);

        // the breeze bay's rods count a caught fish (fishing itself waits for its package: the rod's signal, as its catch sends it)
        var bay = (datum_lazy_template_virtual_domain_breeze_bay)Load(d, "breeze_bay");
        var rod = InDomain<obj_item_fishing_rod>(d).First();
        d.R.In(() => rod._SendSignal(COMSIG_FISHING_ROD_CAUGHT_FISH, Dm.list(rod, null)));
        Assert.Equal(0f, d.R.In(() => bay.main_crate_points));
        d.R.In(() => rod._SendSignal(COMSIG_FISHING_ROD_CAUGHT_FISH, Dm.list(rod, Dm.@new<obj_item_food_cornchips>(get_turf(rod)))));
        Assert.Equal(2f, d.R.In(() => bay.main_crate_points));
        AssertNoRuntimes(d);
    }

    [Fact]
    public void TheHereticHuntsSacrificesScoreByRank()
    {
        Den d = Room(10563);
        var hunt = (datum_lazy_template_virtual_domain_heretic_hunt)Load(d, "heretic_hunt");
        var rune = InDomain<obj_effect_heretic_rune_big>(d).Single();
        Assert.False(string.IsNullOrEmpty(d.R.In(() => Dm.str(rune.greyscale_colors))), "the rune got no colour");
        // a security officer: his weapon and his corpse drop, the corpse dragged onto the rune is worth two points
        var officer = InDomain<mob_living_basic_fake_crewman_sec>(d).First();
        Assert.Equal("security officer", d.R.In(() => officer.name));
        Assert.Equal(typeof(obj_item_knife_combat_survival), d.R.In(() => officer.weapon));
        turf where = d.R.In(() => get_turf(officer));
        var before = d.R.In(() => Dm.each<mob_living_carbon_human>().ToHashSet());
        d.R.In(() => Waiting.NoSleep(officer.death(FALSE)));
        d.R.Step(2);
        var body = d.R.In(() => Dm.each<mob_living_carbon_human>().Single(h => !before.Contains(h)));
        Assert.NotNull(d.R.In(() => Dm.locate<obj_item_knife_combat_survival>(where)));
        Assert.Equal(0f, d.R.In(() => hunt.main_crate_points));
        d.R.In(() => Waiting.NoSleep(d.Bob.forceMove(get_step(rune, EAST)))); // Bob watches
        d.R.In(() => Waiting.Detach(body.forceMove(get_turf(rune))));
        d.R.Step(2);
        // tg's own runtime: the body gibbed by the rune in its Moved signal goes on to change z in the same Moved
        // (atoms_movable.dm), whose gravity refresh reads the deleted human (its dna, human_movement.dm:24)
        d.R.Room.Runtimes.RemoveAll(e => e.StackTrace?.Contains("on_changed_z_level") == true);
        Assert.Equal(2f, d.R.In(() => hunt.main_crate_points));
        Assert.True(d.R.In(() => QDELETED(body)), "the sacrifice stayed");
        Assert.Contains(d.R.Sounds.Heard, s => (s.File ?? "").Contains("castsummon"));
        Assert.True(d.Feed.Any("sacrifice accepted"), "no balloon" + Why(d));
        AssertNoRuntimes(d);
    }

    [Fact]
    public void TheVirtualHereticHealsOnRust()
    {
        Den d = Room(10564);
        var heretic = d.R.In(() => Dm.@new<mob_living_carbon_human>(locate(10, 10, 1)));
        d.R.In(() => Waiting.NoSleep(heretic.equipOutfit(typeof(datum_outfit_virtual_domain_heretic))));
        Assert.True(d.R.In(() => HAS_TRAIT(heretic, TRAIT_ACT_AS_HERETIC) && HAS_TRAIT(heretic, TRAIT_RUSTIMMUNE)));
        Assert.True(d.R.In(() => Dm.@in(FACTION_HERETIC, heretic.faction)));
        d.R.In(() => Waiting.NoSleep(heretic.adjust_brute_loss(30)));
        d.R.In(() => Waiting.NoSleep(get_turf(heretic).AddElement(typeof(datum_element_rust))));
        d.R.Step(4 * Second); // two of SSmobs' lives
        Assert.True(d.R.In(() => Dm.num(heretic.get_brute_loss())) < 30, "no healing on rust");
        AssertNoRuntimes(d);
    }

    [Fact]
    public void TheDomainsMobsFightAndDieAsTgs()
    {
        Den d = Room(10565);
        // the revolutionary: a name, a cause, a weapon, and its corpse on death
        var rev = d.R.In(() => Dm.@new<mob_living_basic_revolutionary>(locate(12, 12, 1)));
        d.R.Step(2);
        Assert.Contains(" ", d.R.In(() => Dm.str(rev.real_name)));
        Assert.Contains("Today, that cause is", d.R.In(() => Dm.str(rev.desc)));
        Assert.NotNull(d.R.In(() => rev.attack_sound));
        Assert.Contains(d.R.In(() => rev.attack_verb_simple), new[] { "robust", "pierce", "slice", "bat", "discipline" });
        d.R.In(() => Waiting.NoSleep(rev.death(FALSE)));
        d.R.Step(2);
        Assert.True(d.R.In(() => QDELETED(rev)));
        Assert.NotNull(d.R.In(() => Dm.locate<mob_living_carbon_human>(locate(12, 12, 1))));

        // the alien drone plants weeds and the sentinel spits
        var drone = d.R.In(() => Dm.@new<mob_living_basic_alien_drone>(locate(14, 12, 1)));
        var sentinel = d.R.In(() => Dm.@new<mob_living_basic_alien_sentinel>(locate(16, 12, 1)));
        d.R.Step(2);
        Assert.Equal(TRUE, d.R.In(() => drone.place_weeds()));
        Assert.NotNull(d.R.In(() => Dm.locate<obj_structure_alien_weeds_node>(locate(14, 12, 1))));
        Assert.Equal(FALSE, d.R.In(() => drone.place_weeds()));
        Assert.NotNull(d.R.In(() => sentinel.GetComponent(typeof(datum_component_ranged_attacks))));
        Assert.Equal(typeof(obj_effect_gibspawner_xeno_bodypartless), d.R.In(() => drone.get_gibs_type()));

        // the zombie leaves bones
        var zombie = d.R.In(() => Dm.@new<mob_living_basic_zombie>(locate(18, 12, 1)));
        d.R.Step(2);
        d.R.In(() => Waiting.NoSleep(zombie.death(FALSE)));
        d.R.Step(2);
        Assert.NotNull(d.R.In(() => Dm.locate<obj_effect_decal_remains_human>(locate(18, 12, 1))));

        // the clown waddles on the bananium floor, which is always slippery
        var clown = d.R.In(() => Dm.@new<mob_living_basic_clown>(locate(20, 12, 1)));
        d.R.Step(2);
        Assert.True(d.R.In(() => HAS_TRAIT(clown, TRAIT_WADDLING)));
        var floor = d.R.In(() => Dm.@new<turf_open_indestructible_honk>(locate(22, 12, 1)));
        Assert.NotNull(d.R.In(() => floor.GetComponent(typeof(datum_component_wet_floor))));

        // the wendigo's victims: a fallen cyber police officer's corpse lies gutted
        var spawner = d.R.In(() => Dm.@new<obj_effect_mob_spawn_corpse_human_cyber_police>(locate(24, 12, 1), TRUE)); // no spawn of its own
        var corpse = d.R.In(() => spawner.create() as mob_living_carbon_human);
        Assert.NotNull(corpse);
        Assert.NotNull(d.R.In(() => corpse.has_status_effect(typeof(datum_status_effect_gutted))));

        // the honkling's bluespace peel sends whoever slips on it, a clown too, through spacetime
        var peel = d.R.In(() => Dm.@new<obj_item_grown_bananapeel_bluespace>(locate(27, 12, 1)));
        var slipper = d.R.In(() => Dm.@new<mob_living_basic_clown>(locate(26, 12, 1)));
        d.R.Step(2);
        d.R.In(() => Waiting.NoSleep(slipper.Move(locate(27, 12, 1), EAST)));
        d.R.Step(2);
        Assert.True(d.R.In(() => QDELETED(peel) || peel.loc != locate(27, 12, 1) || Dm.locate<obj_effect_decal_cleanable_molten_object>(locate(27, 12, 1)) != null), "the peel did not go");
        AssertNoRuntimes(d);
    }

    [Fact]
    public void TheGondolaAsteroidsMeatTurnsYouIntoAVirtualGondola()
    {
        Den d = Room(10572);
        var bob = d.R.Dummy(12, 12);
        // the meat's advanced tranquility, eaten: its own transformation
        var meat = d.R.In(() => Dm.@new<obj_item_food_meat_slab_gondola_virtual_domain>(locate(12, 13, 1)));
        d.R.In(() => Waiting.NoSleep(((datum_reagents)meat.reagents).trans_to(bob, 5, methods: INGEST)));
        var disease = d.R.In(() => Dm.locate<datum_disease_transformation_gondola_virtual_domain>(bob.diseases));
        Assert.NotNull(disease);
        Assert.Equal(9f, d.R.In(() => disease.stage_prob));
        // its last stage: a virtual gondola stands where Bob was
        turf where = d.R.In(() => get_turf(bob));
        d.R.In(() => Waiting.NoSleep(disease.do_disease_transformation(bob)));
        d.R.Step(2);
        Assert.True(d.R.In(() => QDELETED(bob)));
        Assert.NotNull(d.R.In(() => Dm.locate<mob_living_basic_pet_gondola_virtual_domain>(where)));
        AssertNoRuntimes(d);
    }

    [Fact]
    public void TheVoiceDoorOpensToItsSpokenPassword()
    {
        Den d = Room(10573);
        var door = d.R.In(() => Dm.@new<obj_machinery_door_password_voice>(locate(ServerX + 2, ServerY + 4, 1)));
        d.R.Step(2);
        Assert.Equal("Swordfish", d.R.In(() => door.password));
        Assert.Equal("closed", d.R.In(() => door.icon_state));
        // a wrong word keeps it shut; the password said beside it opens it with the blast door's sound
        d.R.In(() => Waiting.Detach(d.Bob.say("open sesame")));
        d.R.Step(Second);
        Assert.True(d.R.In(() => Dm.truthy(door.density)));
        d.R.In(() => Waiting.Detach(d.Bob.say("is it Swordfish")));
        d.R.Step(2 * Second);
        Assert.False(d.R.In(() => Dm.truthy(door.density)), "the door stayed shut" + Why(d));
        Assert.Equal("open", d.R.In(() => door.icon_state));
        Assert.Contains(d.R.Sounds.Heard, s => (s.File ?? "").Contains("blastdoor"));
        // no explosion moves it
        Assert.Equal(FALSE, d.R.In(() => Dm.num(door.ex_act(EXPLODE_DEVASTATE))));
        AssertNoRuntimes(d);
    }

    [Fact]
    public void TheCrateMimicPlaysDeadUntilOpened()
    {
        Den d = Room(10566);
        turf spot = d.R.In(() => locate(ServerX + 2, ServerY + 3, 1));
        var mimic = d.R.In(() => Dm.@new<mob_living_basic_mimic_crate>(spot));
        d.R.Step(2);
        // it carries some loot and looks like a crate, its brain off
        var chips = d.R.In(() => Dm.@new<obj_item_food_cornchips>(spot));
        d.R.In(() => Waiting.NoSleep(chips.forceMove(mimic)));
        Assert.Equal("crate", d.R.In(() => mimic.icon_state));
        Assert.Equal(AI_STATUS_OFF, d.R.In(() => (mimic.ai_controller as datum_ai_controller).ai_status as string));
        // Bob tries to open it: it moves, and with loot inside it locks itself
        d.R.In(() => Waiting.NoSleep(mimic.attack_hand(d.Bob)));
        Assert.True(d.Feed.Any("starts to move!"), "no warning" + Why(d));
        Assert.True(d.Feed.Any("nearly clamps down on your fingers!"));
        Assert.Equal(TRUE, d.R.In(() => mimic.locked));
        Assert.False(d.R.In(() => HAS_TRAIT(mimic, TRAIT_AI_PAUSED)));
        // its death leaves a real crate with the loot
        d.R.In(() => Waiting.NoSleep(mimic.death(FALSE)));
        d.R.Step(2);
        var crate = d.R.In(() => Dm.locate<obj_structure_closet_crate>(spot));
        Assert.NotNull(crate);
        Assert.NotNull(d.R.In(() => Dm.locate<obj_item_food_cornchips>(crate)));
        AssertNoRuntimes(d);
    }

    [Fact]
    public void TheGlacierGrindsCliffsSlideYouDown()
    {
        Den d = Room(10567);
        Load(d, "stairs_and_cliffs");
        // a cliff with open ground north of it and more cliff south of it
        var (top, cliff) = d.R.In(() =>
        {
            foreach (turf_open_cliff c in Dm.each<turf_open_cliff>().Where(t => is_area_virtual(get_area(t))))
            {
                turf north = get_step(c, NORTH) as turf;
                if (north is turf_open && north is not turf_open_cliff && get_step(c, SOUTH) is turf_open_cliff && !Dm.truthy(north.density))
                    return (north, c);
            }
            return ((turf)null, (turf_open_cliff)null);
        });
        Assert.NotNull(cliff);
        var climber = d.R.In(() => Dm.@new<mob_living_carbon_human>(top));
        d.R.Step(2);
        d.R.In(() => Waiting.NoSleep(climber.Move(cliff, SOUTH)));
        Assert.Same(cliff, d.R.In(() => climber.loc));
        Assert.True(d.R.In(() => Dm.@in(climber, SScliff_falling.cliff_grinders)), "not falling" + Why(d));
        Assert.True(d.R.In(() => Dm.truthy(climber.IsKnockdown())), "not knocked down");
        // slid down the cliffs and off them
        var path = new List<string>();
        for (int i = 0; i < 30 && d.R.In(() => Dm.@in(climber, SScliff_falling.cliff_grinders)); i++)
        {
            d.R.Step(Second / 2);
            path.Add(d.R.In(() => $"{climber.x},{climber.y} {climber.loc?.GetType().Name}"));
        }
        Assert.True(d.R.In(() => Dm.num(climber.y)) < d.R.In(() => Dm.num(cliff.y)), "did not slide");
        Assert.False(d.R.In(() => Dm.@in(climber, SScliff_falling.cliff_grinders)), string.Join(" > ", path) + Why(d));
        // a goat walks the cliffs while alive, and falls when it dies on one
        var goat = d.R.In(() => Dm.@new<mob_living_basic_goat>(cliff));
        d.R.Step(2);
        d.R.In(() => Waiting.NoSleep(goat.forceMove(cliff))); // back from wherever it wandered
        Assert.False(d.R.In(() => Dm.@in(goat, SScliff_falling.cliff_grinders)), "a living goat fell");
        d.R.In(() => Waiting.NoSleep(goat.death(FALSE)));
        Assert.True(d.R.In(() => Dm.@in(goat, SScliff_falling.cliff_grinders)), "the dead goat did not fall");
        // a cliff is no way up: it lets you in only from above
        Assert.Equal(FALSE, d.R.In(() => Dm.num(Waiting.NoSleep(cliff.CanPass(climber, SOUTH)))));
        Assert.Equal(TRUE, d.R.In(() => Dm.num(Waiting.NoSleep(cliff.CanPass(climber, NORTH)))));
    }

    [Fact]
    public void TheThreatSpawnsACyberPoliceForAGhost()
    {
        Den d = Room(10568);
        Load(d, "test_only");
        mob_living glitch = SpawnGlitch(d, typeof(datum_antagonist_bitrunning_glitch_cyber_police));
        var police = Assert.IsType<mob_living_carbon_human>(glitch);
        var antag = d.R.In(() => police.mind.has_antag_datum(typeof(datum_antagonist_bitrunning_glitch_cyber_police)) as datum_antagonist_bitrunning_glitch_cyber_police);
        Assert.NotNull(antag);
        Assert.True(d.R.In(() => is_area_virtual(get_area(police))), "not in the domain");
        // the agent: his suit and shades, a name of the cyber authority on his ID, the sleeping carp, no organic needs
        Assert.IsType<obj_item_clothing_under_suit_black_really>(d.R.In(() => police.w_uniform));
        Assert.IsType<obj_item_clothing_glasses_sunglasses>(d.R.In(() => police.glasses));
        var id = d.R.In(() => police.wear_id as obj_item_card_id);
        Assert.Equal(d.R.In(() => police.real_name), d.R.In(() => id.registered_name));
        Assert.True(d.R.In(() => Dm.@in(police.real_name, GLOB.cyberauth_names)), d.R.In(() => Dm.str(police.real_name)));
        Assert.IsType<datum_martial_art_the_sleeping_carp>(d.R.In(() => LAZYACCESS(police.martial_arts, 1)));
        Assert.True(d.R.In(() => HAS_TRAIT(police, TRAIT_NOBREATH)));
        Assert.NotNull(d.R.In(() => police.GetComponent(typeof(datum_component_npc_friendly))));
        Assert.Contains(ROLE_GLITCH, d.R.In(() => Dm.each(police.faction).Select(f => Dm.str(f)).ToList()));
        // a threat of the server, hunting the domain's intruders
        Assert.Equal(1, d.R.In(() => Dm.length(d.Server.spawned_threat_refs)));
        Assert.Equal(ROLE_GLITCH, d.R.In(() => police.mind.assigned_role.title));
        Assert.False(string.IsNullOrEmpty(d.R.In(() => Dm.each<datum_objective>(antag.objectives).Single().explanation_text)));
        // its info window: the glitch's name and objective
        var data = d.R.In(() => Waiting.NoSleep(antag.ui_static_data(police)) as DmList);
        Assert.Equal("Cyber Police", d.R.In(() => Dm.str(data["antag_name"])));
        Assert.Equal("AntagInfoGlitch", d.R.In(() => antag.ui_name));
        Assert.Contains(d.R.Sounds.Heard, s => (s.File ?? "").Contains("ethereal_exit"));
        Assert.True(d.R.Room.RuntimeText.All(t => !t.Contains("bitrunning")), Why(d));
    }

    [Fact]
    public void TheHeavyThreatsSpawnTheTacticalUnitAndTheNetguardian()
    {
        Den d = Room(10569);
        Load(d, "test_only");
        mob_living tac = SpawnGlitch(d, typeof(datum_antagonist_bitrunning_glitch_cyber_tac));
        var human = Assert.IsType<mob_living_carbon_human>(tac);
        Assert.True(d.R.In(() => Dm.each<obj_item_gun_ballistic_automatic_m90>(human.held_items).Any()), "no M-90");
        Assert.NotNull(d.R.In(() => Dm.locate<obj_item_implant_weapons_auth>(human)));
        d.R.In(() => d.Server.reset(fast: TRUE));
        d.R.In(() => Waiting.Detach(d.Server.cool_off()));
        d.R.Step(Second);

        Load(d, "test_only");
        mob_living guardian = SpawnGlitch(d, typeof(datum_antagonist_bitrunning_glitch_netguardian), "Hal");
        var netguardian = Assert.IsType<mob_living_basic_netguardian>(guardian);
        Assert.NotNull(d.R.In(() => netguardian.GetComponent(typeof(datum_component_ranged_attacks))));
        var rockets = d.R.In(() => (netguardian.ai_controller as datum_ai_controller).blackboard[BB_NETGUARDIAN_ROCKET_ABILITY] as datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire_netguardian);
        Assert.NotNull(rockets);
        Assert.Same(netguardian, d.R.In(() => rockets.owner));
        Assert.True(d.R.In(() => HAS_TRAIT(netguardian, TRAIT_MOVE_FLYING)));
        // its death: sparks and the mech's destruction sound
        d.R.In(() => Waiting.NoSleep(netguardian.death(FALSE)));
        d.R.Step(2);
        Assert.Contains(d.R.Sounds.Heard, s => (s.File ?? "").Contains("weapdestr"));
        Assert.Equal("crash", d.R.In(() => netguardian.icon_state));
    }

    [Fact]
    public void TheGlitchEventPicksARunningServerWithMobsToMutate()
    {
        Den d = Room(10570);
        var control = d.R.In(() => Dm.each<datum_round_event_control_bitrunning_glitch>(SSevents.control).Single());
        Assert.Equal(FALSE, d.R.In(() => control.validate_servers()));
        Load(d, "xeno_nest");
        Assert.Equal(TRUE, d.R.In(() => control.validate_servers()));
        Assert.Equal(1, d.R.In(() => Dm.length(control.active_servers)));
        // the event's spawn: the server's threat polls the ghosts
        mob_dead_observer gina = Ghost(d.R, ServerX + 4, ServerY, "Gina");
        client ginas = d.R.In(() => gina.client);
        var ev = d.R.In(() => Dm.@new<datum_round_event_ghost_role_bitrunning_glitch>(TRUE, control));
        d.R.In(() => ev.forced_role = typeof(datum_antagonist_bitrunning_glitch_cyber_police));
        object[] result = new object[1];
        async DmTask Spawn() => result[0] = await ev.spawn_role();
        d.R.In(() => Waiting.Detach(Spawn()));
        d.R.Step(2);
        Click(d.R, gina, AlertOf(d.R, gina));
        d.R.Step(21 * Second);
        Assert.True(Dm.eq(result[0], SUCCESSFUL_SPAWN), Dm.str(result[0]) + Why(d));
        Assert.NotNull(d.R.In(() => (ginas.mob as mob)?.mind?.has_antag_datum(typeof(datum_antagonist_bitrunning_glitch))));
        Assert.Equal(0, d.R.In(() => Dm.length(control.active_servers)));
    }

    [Fact]
    public void EveryNewlyLiveDomainTypeIsCreatedAndDeleted()
    {
        Den d = Room(10571);
        Type[] types =
        {
            typeof(mob_living_basic_revolutionary), typeof(mob_living_basic_alien), typeof(mob_living_basic_alien_sentinel), typeof(mob_living_basic_alien_drone),
            typeof(mob_living_basic_alien_queen), typeof(mob_living_basic_alien_queen_large), typeof(mob_living_basic_alien_maid), typeof(mob_living_basic_zombie),
            typeof(mob_living_basic_zombie_rotten), typeof(mob_living_basic_zombie_rotten_assistant), typeof(mob_living_basic_mimic_crate),
            typeof(mob_living_basic_clown), typeof(mob_living_basic_clown_lube), typeof(mob_living_basic_clown_honkling), typeof(mob_living_basic_clown_fleshclown),
            typeof(mob_living_basic_clown_clownhulk), typeof(mob_living_basic_clown_clownhulk_chlown), typeof(mob_living_basic_clown_clownhulk_honkmunculus),
            typeof(mob_living_basic_clown_clownhulk_destroyer), typeof(mob_living_basic_clown_mutant), typeof(mob_living_basic_fake_crewman),
            typeof(mob_living_basic_fake_crewman_md), typeof(mob_living_basic_fake_crewman_sec), typeof(mob_living_basic_fake_crewman_engi),
            typeof(mob_living_basic_fake_crewman_engi_mod), typeof(mob_living_basic_fake_crewman_assistant), typeof(mob_living_basic_fake_crewman_assistant_alt),
            typeof(mob_living_basic_fake_crewman_boss_cmo), typeof(mob_living_basic_fake_crewman_boss_ce), typeof(mob_living_basic_fake_crewman_boss_hos),
            typeof(mob_living_basic_netguardian), typeof(obj_structure_closet_crate_eva), typeof(obj_effect_mob_spawn_corpse_human_cyber_police),
            typeof(obj_effect_mob_spawn_corpse_human_bitrunner), typeof(obj_machinery_door_password), typeof(obj_machinery_door_password_voice),
        };
        foreach (Type type in types)
        {
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
            var thing = d.R.In(() => Dm.@new(type, locate(20, 20, 1)) as datum);
            d.R.Step(2);
            d.R.In(() => qdel(thing));
            d.R.Step(2);
        }
        foreach (Type type in new[] { typeof(turf_open_cliff), typeof(turf_open_cliff_snowrock), typeof(turf_open_cliff_snowrock_virtual_domain), typeof(turf_open_indestructible_permalube) })
        {
            d.R.In(() => Dm.@new(type, locate(24, 20, 1)));
            d.R.Step(2);
        }
        AssertNoRuntimes(d);
    }
}
