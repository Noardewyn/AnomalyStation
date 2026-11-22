using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package H3 (D333, the bar) in a booted toy room with Bob as a player: the drinking glass that takes its drink's look, the
/// shot glass, the bottles with procs of their own (the wine's vintage, the champagne's cork, the molotov, the pruno), the mauna mug,
/// the bar sign (its list, the area it renames, the EMP, the emag, the panel), the jukebox (its window, its music on its own channel,
/// the cooldown) and the radiant dance machine (its spotlights and its dances). With H1's face-wash fix: the sink's face wash cleans
/// worn glasses (the signal now carries the mob first).
/// </summary>
public class BarH3Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() =>
        {
            ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test");
            (get_area(locate(4, 4, 1)) as area).requires_power = FALSE; // the toy room has no APC
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target, string modifiers = "")
    {
        r.Step(Second);
        Click(r, who, target, modifiers);
    }

    private static void RightClick(AudioRoom r, mob who, atom target)
    {
        r.Step(Second);
        r.Command(() => ClientSeam.Click(who.client, target, "icon-x=16;icon-y=16;right=1;button=right"));
        r.Step(2);
    }

    /// <summary>An atom as the map loader makes it: Initialize with mapload (as BrigTimerTests' Mapped).</summary>
    private static T Mapped<T>(AudioRoom r, int x, int y, params object[] args) where T : atom => r.In(() =>
    {
        float previous = SSatoms.initialized;
        SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
        T made = (T)Dm.@new(typeof(T), new object[] { locate(x, y, 1) }.Concat(args).ToArray());
        SSatoms.initialized = previous;
        return made;
    });

    // an act in the newest window of that interface the player has open
    private static void ActIn(AudioRoom r, mob who, string iface, string action, params (string Key, object Value)[] args)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/" + action, Dm.list(args.Select(a => (object)Dm.kv(a.Key, a.Value)).ToArray())));
        r.Step(4);
    }

    private static float Volume(AudioRoom r, atom a) => r.In(() => ((datum_reagents)a.reagents).total_volume);

    [Fact]
    public void TheDrinkingGlassTakesItsDrinksLookAndTheShotGlassItsName()
    {
        var (r, _, _) = Room(7301);
        var glass = Place<obj_item_reagent_containers_cup_glass_drinkingglass>(r, 5, 4);
        string emptyName = r.In(() => glass.name);
        string emptyIcon = r.In(() => glass.icon_state);

        // a glass of beer: tg's glass style for beer in a drinking glass
        r.In(() => Waiting.NoSleep(((datum_reagents)glass.reagents).add_reagent(typeof(datum_reagent_consumable_ethanol_beer), 30)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal("glass of beer", glass.name);
            Assert.Equal("beerglass", glass.icon_state);
            Assert.Null(glass.fill_icon_thresholds); // a styled glass has no fill overlay
        });

        // emptied, it is a plain glass again with its fill thresholds back
        r.In(() => Waiting.NoSleep(((datum_reagents)glass.reagents).clear_reagents()));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(emptyName, glass.name);
            Assert.Equal(emptyIcon, glass.icon_state);
            Assert.NotNull(glass.fill_icon_thresholds);
        });

        // the shot glass names itself by its contents
        var shot = Place<obj_item_reagent_containers_cup_glass_drinkingglass_shotglass>(r, 6, 4);
        r.In(() => Waiting.NoSleep(((datum_reagents)shot.reagents).add_reagent(typeof(datum_reagent_water), 5)));
        r.Step(2);
        r.In(() => Assert.Equal("filled shot glass", shot.name));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWineCarriesItsVintageAndTheChampagnesCorkFliesOut()
    {
        var (r, feed, bob) = Room(7302);
        var wine = Place<obj_item_reagent_containers_cup_glass_bottle_wine>(r, 5, 4);
        r.In(() =>
        {
            var located = Dm.locate<datum_reagent_consumable_ethanol_wine>(((datum_reagents)wine.reagents).reagent_list);
            Assert.NotNull(located);
            Assert.Contains("Nanotrasen Light Red", Dm.str((located.data as DmList)?["vintage"]));
        });

        // the champagne: fiddling with the cork for a second pops it; the bottle opens and the cork is a flying projectile
        var champagne = Hold<obj_item_reagent_containers_cup_glass_bottle_champagne>(r, bob);
        r.In(() => Assert.False(Dm.truthy(champagne.is_open_container())));
        r.Command(() => Waiting.Detach(champagne.attack_self(bob)));
        r.Step(Second + 5);
        r.In(() => Assert.True(Dm.truthy(champagne.is_open_container())));
        Assert.True(feed.Any("causing it to pop out of the bottle with great force"));
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("champagne_pop") == true);
        r.In(() => Assert.EndsWith("_popped", champagne.icon_state));
        r.Step(2 * Second);
        // the cork lands as an item where it stops (the projectile_drop element)
        bool corkLanded = r.In(() =>
        {
            for (int x = 1; x <= 13; x++)
                for (int y = 1; y <= 9; y++)
                    if (Dm.locate<obj_item_trash_champagne_cork>(locate(x, y, 1)) != null)
                        return true;
            return false;
        });
        Assert.True(corkLanded, "no champagne cork landed");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALitMolotovSetsWhoeverItSmashesOnAlight()
    {
        var (r, feed, bob) = Room(7303);
        var molotov = Place<obj_item_reagent_containers_cup_glass_bottle_molotov>(r, 4, 4);
        r.In(() => Waiting.NoSleep(((datum_reagents)molotov.reagents).add_reagent(typeof(datum_reagent_fuel), 50)));

        // a lit lighter primes it: the fire overlay is on
        var lighter = r.In(() => Dm.@new<obj_item_lighter>(bob.loc));
        r.In(() => lighter.set_lit(TRUE));
        r.In(() => Waiting.NoSleep(molotov.item_interaction(bob, lighter, Dm.list())));
        r.In(() => Assert.True(Dm.truthy(molotov.active)));
        Assert.True(feed.Any("You light"));

        // smashed on Alice two tiles away: the fuel splashes over her and she catches fire (a hotspot lives only on burnable gas, so
        // in the toy room's air the one it spawns on her tile goes out at once, as in tg)
        var alice = r.Dummy(6, 4);
        r.In(() =>
        {
            Waiting.NoSleep(molotov.smash(alice, bob));
            Assert.True(QDELETED(molotov));
            Assert.True(Dm.truthy(alice.on_fire), "Alice did not catch fire");
            Assert.Contains(Dm.each<object>(bob.held_items), i => i is obj_item_broken_bottle); // smashed by hand, its neck stays in the hand
        });
    }

    [Fact]
    public void PrunoFermentsInAStructureAndStopsWhenTakenOut()
    {
        var (r, feed, _) = Room(7304);
        var crate = Place<obj_structure_closet_crate>(r, 5, 4);
        var pruno = Place<obj_item_reagent_containers_cup_glass_bottle_pruno>(r, 5, 4);

        // taken out after ten seconds in the crate, it keeps the twenty it has left
        r.In(() => Waiting.NoSleep(pruno.forceMove(crate)));
        r.Step(10 * Second);
        r.In(() => Waiting.NoSleep(pruno.forceMove(locate(5, 4, 1))));
        r.In(() =>
        {
            Assert.Null(pruno.fermentation_timer);
            Assert.InRange(Dm.num(pruno.fermentation_time_remaining), 190f, 210f); // deciseconds: twenty seconds left of thirty
            Assert.Equal("pruno mix", pruno.name);
        });

        // back in, it finishes after the rest
        r.In(() => Waiting.NoSleep(pruno.forceMove(crate)));
        r.Step(21 * Second);
        r.In(() =>
        {
            Assert.Equal("bag of pruno", pruno.name);
            Assert.Equal("trashbag1", pruno.icon_state);
            var holder = (datum_reagents)pruno.reagents;
            Assert.False(Dm.truthy(holder.has_reagent(typeof(datum_reagent_consumable_prunomix))));
            Assert.True(Dm.truthy(holder.has_reagent(typeof(datum_reagent_consumable_ethanol_pruno))));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMaunaMugHeatsItsDrinkFromItsCellAndGlows()
    {
        var (r, _, bob) = Room(7305);
        var mug = Hold<obj_item_reagent_containers_cup_maunamug>(r, bob);
        r.In(() => Assert.NotNull(mug.cell));
        r.In(() => Waiting.NoSleep(((datum_reagents)mug.reagents).add_reagent(typeof(datum_reagent_water), 20, reagtemp: 300)));
        r.Command(() => Waiting.Detach(mug.item_ctrl_click(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(mug.on));
            Assert.Equal("maunamug_on", mug.icon_state);
        });
        float before = r.In(() => ((datum_reagents)mug.reagents).chem_temp);
        r.Step(10 * Second);
        float after = r.In(() => ((datum_reagents)mug.reagents).chem_temp);
        Assert.True(after > before + 10, $"the mug did not heat: {before} -> {after}");
        r.In(() => Assert.True(mug.cell.charge < mug.cell.maxcharge));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBarSignShowsThePickedSignRenamesItsAreaAndFallsToTheEmpAndTheEmag()
    {
        var (r, feed, bob) = Room(7306);
        r.In(() => Waiting.NoSleep(bob.equip_to_slot_or_del(Dm.@new<obj_item_card_id_advanced_gold>(bob.loc), ITEM_SLOT_ID)));
        var sign = Mapped<obj_machinery_barsign_all_access>(r, 5, 5);
        r.In(() =>
        {
            Assert.True(Dm.truthy(sign.change_area_name)); // a mapped sign belongs to its area
            Assert.Equal("empty", sign.icon_state);      // it starts off
        });

        // picking The Drunk Carp from the list: its picture, its name and its area's
        Use(r, bob, sign);
        ActIn(r, bob, "ListInputWindow", "submit", ("entry", "The Drunk Carp"));
        r.In(() =>
        {
            Assert.Equal("thedrunkcarp", sign.icon_state);
            Assert.Equal("bar sign (The Drunk Carp)", sign.name);
            Assert.Equal("The Drunk Carp", Dm.str((get_area(sign) as area).name));
            Assert.Equal("#a82196", Dm.str(sign.light_color).ToLowerInvariant()); // its neon
        });

        // an EMP shows the EMP'd sign for a minute, then the old one
        r.In(() => Waiting.NoSleep(sign.emp_act(EMP_HEAVY)));
        r.In(() => Assert.Equal("empbarsign", sign.icon_state));
        r.Step(61 * Second);
        r.In(() => Assert.Equal("thedrunkcarp", sign.icon_state));

        // an emag loads the Syndi Cat after ten seconds
        r.In(() => Waiting.NoSleep(sign.emag_act(bob, null)));
        r.Step(11 * Second);
        r.In(() => Assert.Equal("syndibarsign", sign.icon_state));

        // the panel opened with a screwdriver turns it off
        var screwdriver = Hold<obj_item_screwdriver>(r, bob);
        r.In(() => Waiting.NoSleep(sign.screwdriver_act(bob, screwdriver)));
        r.In(() =>
        {
            Assert.True(Dm.truthy(sign.panel_open));
            Assert.Equal("empty", sign.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheJukeboxPlaysItsTrackToBobOnItsChannelAndCoolsDownAfter()
    {
        var (r, feed, bob) = Room(7307);
        var jukebox = Place<obj_machinery_jukebox_no_access>(r, 5, 4);
        r.In(() =>
        {
            Assert.Equal("Tintin on the Moon", jukebox.music_player.selection?.song_name); // the default track: no config tracks
            Assert.Equal("jukebox", jukebox.icon_state);
        });

        // the window: the track list, then the volume set to its minimum and back to its maximum
        Use(r, bob, jukebox);
        ActIn(r, bob, "Jukebox", "set_volume", ("volume", "min"));
        r.In(() => Assert.Equal(0f, Dm.num(jukebox.music_player.volume)));
        ActIn(r, bob, "Jukebox", "set_volume", ("volume", "max"));
        r.In(() => Assert.Equal(50f, Dm.num(jukebox.music_player.volume)));

        // play: Bob hears tg's title track on the jukebox channel, placed relative to the jukebox
        ActIn(r, bob, "Jukebox", "toggle");
        r.In(() =>
        {
            Assert.NotNull(jukebox.music_player.active_song_sound);
            Assert.Equal("jukebox-active", jukebox.icon_state);
        });
        var heard = r.Sounds.Heard.Where(h => ReferenceEquals(h.Target, bob) && h.Channel == CHANNEL_JUKEBOX).ToList();
        Assert.NotEmpty(heard);
        Assert.Contains("title3", heard[0].File);
        Assert.Equal(1f, heard[0].X); // the jukebox is a tile east

        // Bob steps back west: the sound is updated, now two tiles away
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(3, 4, 1))));
        r.Step(2);
        var last = r.Sounds.Heard.Last(h => ReferenceEquals(h.Target, bob) && h.Channel == CHANNEL_JUKEBOX);
        Assert.Equal(2f, last.X);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(4, 4, 1)))); // back beside it to work it
        Assert.True(((int)last.Status & (int)SOUND_UPDATE) != 0);

        // stop: the channel is cut, and it cannot start again for ten seconds
        ActIn(r, bob, "Jukebox", "toggle");
        r.In(() =>
        {
            Assert.Null(jukebox.music_player.active_song_sound);
            Assert.Equal("jukebox", jukebox.icon_state);
        });
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.Channel == CHANNEL_JUKEBOX && h.File == null);
        ActIn(r, bob, "Jukebox", "toggle");
        r.In(() => Assert.Null(jukebox.music_player.active_song_sound));
        r.Step(10 * Second);
        ActIn(r, bob, "Jukebox", "toggle");
        r.In(() => Assert.NotNull(jukebox.music_player.active_song_sound));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDanceMachineLightsTheFloorAndMakesItsListenersDance()
    {
        var (r, _, bob) = Room(7308);
        var disco = Place<obj_machinery_jukebox_disco_indestructible>(r, 5, 4);
        RightClick(r, bob, disco);
        r.In(() =>
        {
            Assert.NotNull(disco.music_player.active_song_sound);
            Assert.True(Dm.length(disco.spotlights) > 10, "too few spotlights");
            Assert.Equal("disco-active", disco.icon_state);
        });

        // its dances: Bob is listening, so within a couple of seconds he dances
        bool danced = false;
        for (int i = 0; i < 6 && !danced; i++)
        {
            r.Step(Second);
            danced = r.In(() => HAS_TRAIT(bob, TRAIT_DISCO_DANCER));
        }
        Assert.True(danced, "Bob never danced");

        // the sparkles orbit it, and its spotlights cycle their colours
        r.Step(20 * Second);
        r.In(() => Assert.True(Dm.length(disco.sparkles) >= 25));

        // stopped, its lights and sparkles go
        RightClick(r, bob, disco);
        r.In(() =>
        {
            Assert.Null(disco.music_player.active_song_sound);
            Assert.Equal(0, Dm.length(disco.spotlights));
            Assert.Equal(0, Dm.length(disco.sparkles));
        });
        r.Step(5 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WashingTheFaceAtASinkCleansTheBloodOffWornGlasses()
    {
        var (r, feed, bob) = Room(7309);
        var sink = Place<obj_structure_sink>(r, 5, 4);
        var glasses = r.In(() => Dm.@new<obj_item_clothing_glasses_sunglasses>(bob.loc));
        r.In(() =>
        {
            Waiting.NoSleep(bob.equip_to_slot_or_del(glasses, ITEM_SLOT_EYES));
            Waiting.NoSleep(glasses.add_blood_DNA(Dm.list(Dm.kv("dna-of-alice", "O+"))));
            Assert.True(GET_ATOM_BLOOD_DNA_LENGTH(glasses) > 0);
            bob.zone_selected = BODY_ZONE_HEAD;
        });
        Use(r, bob, sink);
        Assert.True(feed.Any("You start washing your face"));
        r.Step(4 * Second + 5);
        r.In(() => Assert.Equal(0f, GET_ATOM_BLOOD_DNA_LENGTH(glasses)));
        r.AssertNoRuntimes();
    }
}
