using System.IO;
using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The holodeck in a toy room (modules/holodeck/computer.dm, holo_effect.dm, items.dm): a 9x10 recreational holodeck area beside its
/// control console, with tg's converted programs mounted as SSmapping would: the console starts on the offline program, loads a
/// chosen program from its Holodeck window and swaps it for another after its cooldown, derezzes the holographic items that leave
/// the holodeck, keeps its holograms to stamina damage until it is emagged, and lists its dangerous programs once emagged; the
/// wildlife program's effects spawn holographic carp that go with the program; the pet park's effects spawn its animals; a broken
/// floor overloads it; a console mapped on reinforced floor, as Meta's, keeps it until a program is projected over it.
/// </summary>
public class HolodeckTests
{
    private const int Size = 16;

    /// The converted holodeck programs (maps/templates/holodeck_*.as3dmap) at tg's paths, as the host mounts them (MapSeam).
    private static void MountPrograms(World w)
    {
        IDmFileSystem files = DmWorldState.Of(w).Files;
        foreach (string converted in Directory.GetFiles(Path.Combine(MapFixtures.RepoRoot(), "maps", "templates"), "holodeck_*" + MapText.Extension))
            files.WriteText("_maps/templates/" + Path.GetFileNameWithoutExtension(converted) + ".dmm", File.ReadAllText(converted));
    }

    internal sealed class Deck
    {
        public AudioRoom R;
        public Feed Feed;
        public obj_machinery_computer_holodeck Console;
        public area_station_holodeck Area;
        public mob_living_carbon_human Bob;
    }

    /// The holodeck's area covers x 2-10, y 2-11 with holographic plating; the console stands at (13, 5), outside it.
    internal static Deck Room(ulong seed)
    {
        var d = new Deck();
        d.R = new AudioRoom(Size, Size, seed: seed, configure: MountPrograms);
        d.Feed = Listen(d.R);
        d.R.In(() =>
        {
            SSmapping.preloadHolodeckTemplates();
            d.Area = Dm.@new<area_station_holodeck_rec_center>();
            foreach (turf t in Dm.each<turf>(block(2, 2, 1, 10, 11, 1)))
            {
                t.change_area((area)t.loc, d.Area);
                Waiting.NoSleep(t.ChangeTurf(typeof(turf_open_floor_holofloor_plating)));
            }
            d.Console = Dm.@new<obj_machinery_computer_holodeck>(locate(13, 5, 1));
        });
        d.Bob = d.R.Join(12, 5, "Bob");
        d.R.Step(Second);
        return d;
    }

    internal static void LoadNow(Deck d, string program) => d.R.In(() =>
    {
        d.Console.holodeck_cooldown = 0;
        Waiting.NoSleep(d.Console.load_program(program));
    });

    private static List<T> Spawned<T>(Deck d) where T : datum => d.R.In(() => Dm.each(d.Console.spawned).OfType<T>().Where(a => !QDELETED(a)).ToList());

    [Fact]
    public void The_console_loads_a_program_from_its_window_and_swaps_it_after_its_cooldown()
    {
        var d = Room(14301);
        Assert.Same(d.Area, d.R.In(() => d.Console.linked));
        Assert.Same(d.Console, d.R.In(() => d.Area.linked));
        Assert.Equal("holodeck_offline", d.R.In(() => d.Console.program));
        Assert.Equal(29, d.R.In(() => SSmapping.holodeck_templates.len));

        // the Holodeck window lists the default programs and holds back the dangerous ones
        Take(d.Bob);
        Click(d.R, d.Bob, d.Console);
        d.R.Step(4);
        JsonElement payload = LastFullUpdate(Take(d.Bob), out string window);
        Assert.Equal("Holodeck", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.Contains(data.GetProperty("default_programs").EnumerateArray(), p => p.GetProperty("id").GetString() == "holodeck_dodgeball");
        Assert.DoesNotContain(data.GetProperty("default_programs").EnumerateArray(), p => p.GetProperty("id").GetString() == "holodeck_burntest");
        Assert.False(data.TryGetProperty("emag_programs", out _));

        d.R.Step(3 * Second); // the console's two-second cooldown from its init
        Act(d.R, d.Bob, window, "load_program", ("id", "holodeck_dodgeball"));
        d.R.Step(Second);
        Assert.Equal("holodeck_dodgeball", d.R.In(() => d.Console.program));
        List<obj_item_toy_dodgeball> balls = Spawned<obj_item_toy_dodgeball>(d);
        Assert.Equal(18, balls.Count); // tg's court: three balls on each of six tiles
        Assert.All(balls, b => Assert.True(d.R.In(() => Dm.@in(get_turf(b), d.Area)), "the balls are in the holodeck"));
        Assert.All(balls, b => Assert.True(Dm.truthy(Dm.bitand(b.flags_1, HOLOGRAM_1)), "a holographic ball"));

        // a second program at once is refused while the projector recalibrates
        Act(d.R, d.Bob, window, "load_program", ("id", "holodeck_lounge"));
        d.R.Step(4);
        Assert.Equal("holodeck_dodgeball", d.R.In(() => d.Console.program));
        Assert.True(d.Feed.Any("ERROR. Recalibrating projection apparatus."));

        // after the cooldown the lounge replaces the court: the balls are derezzed
        d.R.Step(3 * Second);
        Act(d.R, d.Bob, window, "load_program", ("id", "holodeck_lounge"));
        d.R.Step(Second);
        Assert.Equal("holodeck_lounge", d.R.In(() => d.Console.program));
        Assert.All(balls, b => Assert.True(QDELETED(b), "the court's balls are gone"));
        Assert.NotEmpty(Spawned<obj_structure_table_wood>(d));
        Assert.True(d.R.In(() => Dm.each<turf>(d.Area).Any(t => t is turf_open_floor_holofloor_carpet)), "the lounge's carpet is laid");
        d.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_holographic_item_that_leaves_the_holodeck_is_derezzed()
    {
        var d = Room(14302);
        LoadNow(d, "holodeck_dodgeball");
        d.R.Step(4);
        List<obj_item_toy_dodgeball> balls = Spawned<obj_item_toy_dodgeball>(d);
        Assert.Equal(18, balls.Count);
        obj_item_toy_dodgeball taken = balls[0];
        d.R.In(() => Waiting.NoSleep(taken.forceMove(locate(12, 8, 1))));
        d.R.Step(5 * Second); // the console's process
        Assert.True(QDELETED(taken), "the ball outside the holodeck faded away");
        Assert.All(balls.Skip(1), b => Assert.False(QDELETED(b), "the balls inside stay"));
        Assert.Equal(17, Spawned<obj_item_toy_dodgeball>(d).Count);
        d.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_emag_unlocks_the_dangerous_programs_and_the_holograms_lose_their_safety()
    {
        var d = Room(14303);
        // the safeties: every holographic item hits for stamina damage; the swords are stamina weapons by their own type
        LoadNow(d, "holodeck_thunderdome");
        d.R.Step(4);
        List<obj_item_melee_energy_sword_holographic> swords = Spawned<obj_item_melee_energy_sword_holographic>(d);
        Assert.Equal(14, swords.Count); // seven green and seven red
        Assert.All(swords, s => Assert.Equal(STAMINA, d.R.In(() => s.damtype)));
        Assert.Equal(2, d.R.In(() => Dm.each(d.Console.spawned).OfType<obj_machinery_readybutton>().Count()));
        LoadNow(d, "holodeck_dodgeball");
        d.R.Step(4);
        List<obj_item_toy_dodgeball> balls = Spawned<obj_item_toy_dodgeball>(d);
        Assert.All(balls, b => Assert.Equal(STAMINA, d.R.In(() => b.damtype)));

        bool emagged = d.R.In(() => Dm.truthy(Waiting.NoSleep(d.Console.emag_act(d.Bob))));
        Assert.True(emagged);
        Assert.True(d.Feed.Any("Automatic shutoff and derezzing protocols have been corrupted"));
        Assert.All(balls, b => Assert.Equal(BRUTE, d.R.In(() => b.damtype)));

        // the window now lists the dangerous programs, and one of them loads
        Take(d.Bob);
        Click(d.R, d.Bob, d.Console);
        d.R.Step(4);
        JsonElement data = LastFullUpdate(Take(d.Bob), out string window).GetProperty("data");
        Assert.Equal(1, data.GetProperty("emagged").GetInt32());
        Assert.Contains(data.GetProperty("emag_programs").EnumerateArray(), p => p.GetProperty("id").GetString() == "holodeck_wildlifesim");
        d.R.In(() => d.Console.holodeck_cooldown = 0);
        Act(d.R, d.Bob, window, "load_program", ("id", "holodeck_wildlifesim"));
        d.R.Step(Second);
        Assert.Equal("holodeck_wildlifesim", d.R.In(() => d.Console.program));
        Assert.Equal(12, d.R.In(() => Dm.each(d.Console.effects).OfType<obj_effect_holodeck_effect_mobspawner>().Count()));
        List<mob_living_basic_carp_holographic> carps = Spawned<mob_living_basic_carp_holographic>(d);
        Assert.Equal(12, carps.Count);
        mob_living_basic_carp_holographic carp = carps[0];
        Assert.All(carps, c => Assert.True(Dm.truthy(Dm.bitand(c.flags_1, HOLOGRAM_1))));

        // the offline program takes the carp with it
        d.R.In(() => Waiting.NoSleep(d.Console.emergency_shutdown()));
        d.R.Step(4);
        Assert.Equal("holodeck_offline", d.R.In(() => d.Console.program));
        Assert.All(carps, c => Assert.True(QDELETED(c), "the holographic carp are derezzed"));
        Assert.Empty(d.R.In(() => Dm.each(d.Console.effects).ToList()));
        d.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_holodeck_effects_spawn_their_deck_and_pets_and_the_floor_check_holds()
    {
        var d = Room(14304);
        LoadNow(d, "holodeck_petpark");
        d.R.Step(4);
        Assert.Equal(11, d.R.In(() => Dm.each(d.Console.effects).OfType<obj_effect_holodeck_effect_mobspawner_pet>().Count()));
        List<mob_living> pets = Spawned<mob_living>(d);
        Assert.True(pets.Count >= 11, "each pet effect spawned its animal: " + pets.Count);
        Assert.All(pets, p => Assert.True(d.R.In(() => HAS_TRAIT(p, TRAIT_SPAWNED_MOB))));
        Assert.True(Dm.truthy(d.R.In(() => d.Console.floorcheck())));

        // a broken floor overloads the holodeck: it shuts down and is damaged
        d.R.In(() => Waiting.NoSleep(((turf)locate(5, 5, 1)).ChangeTurf(typeof(turf_open_floor_plating))));
        d.R.Step(5 * Second);
        Assert.Equal("holodeck_offline", d.R.In(() => d.Console.program));
        Assert.Equal(1f, d.R.In(() => d.Console.damaged));
        Assert.True(d.Feed.Any("The holodeck overloads!"));
        d.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_console_mapped_beside_its_holodeck_keeps_the_engine_floor_until_a_program_loads()
    {
        // as Meta's: the holodeck's area is mapped on reinforced floor and the console beside it, both at the map's load. tg's console
        // starts with its program set to the offline one, so its init's load of it returns at once (computer.dm:198): the floor stays
        // reinforced, and the console's process skips the floor check while the offline program is set
        obj_machinery_computer_holodeck console = null;
        area_station_holodeck_rec_center holodeck = null;
        var r = new AudioRoom(Size, Size, seed: 14305, configure: MountPrograms, mapExtras: w =>
        {
            holodeck = Dm.@new<area_station_holodeck_rec_center>();
            foreach (turf t in Dm.each<turf>(block(2, 2, 1, 10, 11, 1)).ToList())
            {
                t.change_area((area)t.loc, holodeck);
                Dm.@new<turf_open_floor_engine>(t);
            }
            console = Dm.@new<obj_machinery_computer_holodeck>(locate(13, 5, 1));
        });
        r.In(() => SSmapping.preloadHolodeckTemplates()); // the toy room's SSmapping skips the templates
        r.Step(5 * Second);
        Assert.Same(holodeck, r.In(() => console.linked));
        Assert.Equal("holodeck_offline", r.In(() => console.program));
        Assert.All(r.In(() => Dm.each<turf>(holodeck).ToList()), t => Assert.IsType<turf_open_floor_engine>(t));
        Assert.Equal(0f, r.In(() => console.damaged)); // no floor check while offline

        // a program projects over the reinforced floor (it is holodeck-compatible), and the offline program then lays the plating
        r.In(() => Waiting.NoSleep(console.load_program("holodeck_basketball", TRUE)));
        r.Step(Second);
        Assert.All(r.In(() => Dm.each<turf>(holodeck).ToList()), t => Assert.IsAssignableFrom<turf_open_floor_holofloor>(t));
        Assert.True(Dm.truthy(r.In(() => console.floorcheck())), "the projected floor is whole");
        Assert.Equal(2, r.In(() => Dm.each(console.spawned).OfType<obj_structure_hoop>().Count()));
        r.In(() => Waiting.NoSleep(console.load_program("holodeck_offline", TRUE)));
        r.Step(Second);
        Assert.All(r.In(() => Dm.each<turf>(holodeck).ToList()), t => Assert.IsType<turf_open_floor_holofloor_plating>(t));
        r.Step(5 * Second);
        Assert.Equal(0f, r.In(() => console.damaged));
        r.AssertNoRuntimes();
    }
}
