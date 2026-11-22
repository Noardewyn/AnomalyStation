using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// Syndicate stage G3c (D316), the uplink's seditious posters, pony box, cardboard cutouts and sleepytime kit with the systems they
/// need: a rolled poster hangs on a wall, demoralises, comes down with wirecutters and rips by hand; the bedsheet tears into cloth and
/// the linen bin hands one out; the plushes fall in love and marry; the dehydrated carp swells in water into a carp that knows its
/// owner; a cutout disguises whoever holds it; the pony potion refuses what it cannot raise and gives the syndicate pony its radio and
/// access; a sweep spawns and deletes each newly live type with no runtimes.
/// </summary>
public class SyndicateSleepytimeTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed = 43)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        return (r, feed, bob);
    }

    [Fact]
    public void TheTypesAreLiveAndTheirEntriesSell()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_poster_traitor), typeof(obj_structure_sign_poster_traitor), typeof(obj_item_slimepotion_sentience_nuclear_dangerous_horse),
            typeof(datum_antagonist_sentient_creature), typeof(datum_component_simple_access), typeof(obj_item_cardboard_cutout_adaptive),
            typeof(datum_component_tactical), typeof(datum_cardboard_cutout_clown), typeof(obj_item_bedsheet_syndie), typeof(obj_structure_bedsheetbin),
            typeof(datum_element_bed_tuckable), typeof(obj_item_toy_plush_carpplushie), typeof(obj_item_toy_plush_carpplushie_dehy_carp),
            typeof(obj_item_reagent_containers_cup_glass_mug_coco), typeof(obj_item_clothing_under_syndicate_bloodred_sleepytime),
            typeof(datum_component_keep_me_secure), typeof(datum_component_edit_complainer), typeof(datum_atom_hud_alternate_appearance_basic_everyone),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        var r = new AudioRoom(5, 5, seed: 3);
        foreach (Type entry in new[]
        {
            typeof(datum_uplink_item_badass_demotivational_posters), typeof(datum_uplink_item_badass_dangerous_horse),
            typeof(datum_uplink_item_badass_costumes_tactical_naptime), typeof(datum_uplink_item_device_tools_cutouts),
            typeof(datum_uplink_item_stealthy_weapons_dehy_carp),
        })
            Assert.False(r.In(() => uplink_item_hidden(Dm.@new(entry) as datum_uplink_item)), entry.Name + " is hidden");
    }

    [Fact]
    public void ThePosterHangsOnAWallComesDownAndRips()
    {
        var (r, feed, bob) = Room();
        turf wall = r.In(() => locate(5, 6, 1) as turf);
        r.In(() => Waiting.NoSleep(wall.ChangeTurf(typeof(turf_closed_wall))));
        wall = r.In(() => locate(5, 6, 1) as turf);
        var rolled = r.In(() => Dm.@new<obj_item_poster_traitor>(bob.loc));
        Assert.Equal("rolled_traitor", r.In(() => rolled.icon_state));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(rolled)));
        r.In(() => Waiting.Detach(rolled.interact_with_atom(wall, bob)));
        r.Step(5 * Second); // PLACE_SPEED, 3.7 seconds
        var placed = r.In(() => Dm.locate<obj_structure_sign_poster_traitor>(bob.loc));
        Assert.NotNull(placed);
        Assert.Equal(32, r.In(() => placed.pixel_y));
        Assert.StartsWith("poster - ", r.In(() => placed.name));
        Assert.NotNull(r.In(() => placed.demoraliser));
        Assert.True(r.In(() => QDELETED(rolled)));
        Assert.True(feed.Any("balloon: hanging poster..."));

        // the wirecutters take it down rolled into the cutter's hands
        var cutters = r.In(() => Dm.@new<obj_item_wirecutters>(bob.loc));
        r.In(() => Waiting.NoSleep(placed.wirecutter_act(bob, cutters)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(placed)));
        var again = r.In(() => Dm.each<obj_item_poster>(bob.contents).SingleOrDefault());
        Assert.NotNull(again);
        Assert.Equal(typeof(obj_item_poster_traitor), r.In(() => again.GetType()));

        // hung again, an empty hand rips it
        r.In(() => qdel(cutters));
        r.In(() => Waiting.Detach(again.interact_with_atom(wall, bob)));
        r.Step(5 * Second);
        var second = r.In(() => Dm.locate<obj_structure_sign_poster_traitor>(bob.loc));
        Assert.NotNull(second);
        Click(r, bob, second); // tg's sign examines it first (a verb, from the player's click), then the poster rips
        r.Step(2);
        Assert.True(r.In(() => QDELETED(second)));
        var ripped = r.In(() => Dm.locate<obj_structure_sign_poster_ripped>(bob.loc));
        Assert.NotNull(ripped);
        Assert.Equal(32, r.In(() => ripped.pixel_y));
        Assert.True(feed.Any("in a single, decisive motion!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBedsheetTearsIntoClothAndTheLinenBinHandsOneOut()
    {
        var (r, feed, bob) = Room();
        var sheet = r.In(() => Dm.@new<obj_item_bedsheet_syndie>(bob.loc));
        var cutters = r.In(() => Dm.@new<obj_item_wirecutters>(bob.loc));
        r.In(() => Waiting.NoSleep(sheet.item_interaction(bob, cutters)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(sheet)));
        var cloth = r.In(() => Dm.locate<obj_item_stack_sheet_cloth>(bob.loc));
        Assert.NotNull(cloth);
        Assert.Equal(3, r.In(() => Dm.num(cloth.amount)));

        var bin = r.In(() => Dm.@new<obj_structure_bedsheetbin>(locate(6, 5, 1)));
        r.In(() => Waiting.NoSleep(bin.attack_hand(bob)));
        r.Step(2);
        Assert.Equal(9, r.In(() => Dm.num(bin.amount)));
        Assert.Equal("linenbin-full", r.In(() => bin.icon_state));
        Assert.NotNull(r.In(() => Dm.each<obj_item_bedsheet>(bob.contents).SingleOrDefault()));
        Assert.True(feed.Any("out of"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSleepytimeKitAndThePlushesInLove()
    {
        var (r, _, bob) = Room();
        var box = r.In(() => Dm.@new<obj_item_storage_box_syndie_kit_sleepytime>(bob.loc));
        r.Step(2);
        Assert.Equal(new[] { typeof(obj_item_clothing_under_syndicate_bloodred_sleepytime), typeof(obj_item_reagent_containers_cup_glass_mug_coco), typeof(obj_item_toy_plush_carpplushie), typeof(obj_item_bedsheet_syndie) },
            r.In(() => Dm.each<atom>(box.contents).Select(a => a.GetType()).ToArray()));
        var mug = r.In(() => Dm.locate<obj_item_reagent_containers_cup_glass_mug_coco>(box));
        r.In(() => mug.update_appearance());
        Assert.Equal("tea", r.In(() => mug.icon_state));
        r.In(() => Waiting.NoSleep(((datum_reagents)mug.reagents).clear_reagents()));
        r.In(() => mug.update_appearance());
        Assert.Equal("tea_empty", r.In(() => mug.icon_state));

        var romeo = r.In(() => Dm.@new<obj_item_toy_plush_carpplushie>(bob.loc));
        var juliet = r.In(() => Dm.@new<obj_item_toy_plush_carpplushie>(bob.loc));
        r.In(() => Waiting.NoSleep(juliet.item_interaction(bob, romeo)));
        Assert.Same(romeo, r.In(() => juliet.lover));
        Assert.Same(juliet, r.In(() => romeo.lover));
        Assert.EndsWith("could rip a seam!", r.In(() => juliet.desc));
        r.In(() => Waiting.NoSleep(juliet.item_interaction(bob, romeo)));
        Assert.Same(romeo, r.In(() => juliet.partner));
        Assert.Same(juliet, r.In(() => romeo.partner));
        r.In(() => qdel(romeo));
        Assert.Null(r.In(() => juliet.partner));
        Assert.True(r.In(() => Dm.truthy(juliet.heartbroken)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDehydratedCarpSwellsInWaterIntoAFriendlyCarp()
    {
        var (r, feed, bob) = Room();
        var plush = r.In(() => Dm.@new<obj_item_toy_plush_carpplushie_dehy_carp>(bob.loc));
        r.In(() => Waiting.NoSleep(plush.attack_self(bob)));
        Assert.Same(bob, r.In(() => plush.owner));
        r.In(() => Waiting.NoSleep(Dm.@new<datum_reagent_water>().expose_obj(plush, 5, TOUCH)));
        Assert.Equal("It's growing!", r.In(() => plush.desc));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(plush)));
        var carp = r.In(() => Dm.locate<mob_living_basic_carp>(bob.loc));
        Assert.NotNull(carp);
        Assert.True(feed.Any("looks up at you with friendly eyes."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCutoutDisguisesItsHolder()
    {
        var (r, _, bob) = Room();
        var cutout = r.In(() => Dm.@new<obj_item_cardboard_cutout_adaptive>(bob.loc));
        Assert.NotNull(r.In(() => cutout.tacticool));
        r.In(() => get_cardboard_cutout_instance(typeof(datum_cardboard_cutout_clown)).apply(cutout));
        string disguise = r.In(() => cutout.name);
        Assert.Contains(disguise, r.In(() => Dm.each<string>(GLOB.clown_names).ToList()));
        Assert.Equal("A cardboard cutout of a clown. You get the feeling that it should be in a corner.", r.In(() => cutout.desc));
        Assert.True(obj_item_cardboard_cutout.cutout_waits(typeof(datum_cardboard_cutout_wizard)));
        Assert.False(obj_item_cardboard_cutout.cutout_waits(typeof(datum_cardboard_cutout_nuclear_operative))); // live since syndicate N1b
        Assert.False(obj_item_cardboard_cutout.cutout_waits(typeof(datum_cardboard_cutout_clown)));

        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(cutout)));
        r.Step(2);
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_UNKNOWN_APPEARANCE)));
        Assert.Equal(disguise, r.In(() => Dm.str(bob.get_visible_name())));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_WADDLING)));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(cutout)));
        r.Step(2);
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_UNKNOWN_APPEARANCE)));
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_WADDLING)));

        r.In(() => Waiting.NoSleep(cutout.push_over()));
        Assert.Equal("cutout_pushed_over", r.In(() => cutout.icon_state));
        Assert.Null(r.In(() => cutout.tacticool));
        r.In(() => Waiting.NoSleep(cutout.attack_self(bob)));
        Assert.NotNull(r.In(() => cutout.tacticool));
        Assert.Equal("cutout_basic", r.In(() => cutout.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePonyPotionRaisesOnlyAPonyAndArmsIt()
    {
        var (r, feed, bob) = Room();
        var box = r.In(() => Dm.@new<obj_item_storage_box_syndicate_horse_box>(bob.loc));
        r.Step(2);
        var potion = r.In(() => Dm.locate<obj_item_slimepotion_sentience_nuclear_dangerous_horse>(box));
        Assert.NotNull(potion);
        var cat = r.In(() => Dm.@new<mob_living_basic_pet_cat>(locate(6, 5, 1)));
        r.In(() => Waiting.Detach(potion.interact_with_atom(cat, bob)));
        r.Step(2);
        Assert.True(feed.Any("balloon: invalid creature!"));
        var pony = r.In(() => Dm.@new<mob_living_basic_pony_dangerous>(locate(4, 5, 1))); // the cube's pony: only it is SENTIENCE_PONY
        r.In(() => Waiting.Detach(potion.interact_with_atom(pony, bob)));
        r.Step(21 * Second); // no ghost answers the poll
        Assert.True(feed.Any("balloon: offering..."));
        Assert.True(feed.Any("balloon: try again later!"));
        Assert.False(r.In(() => Dm.truthy(potion.being_used)));

        // what the syndicate potion adds to a raised pony: the radio implant and the syndicate's access
        r.In(() => Waiting.NoSleep(potion.after_success(bob, pony)));
        r.Step(2);
        Assert.NotNull(r.In(() => Dm.locate<obj_item_implant_radio_syndicate>(pony)));
        DmList access = r.In(() => Dm.list());
        r.In(() => pony.SEND_SIGNAL(COMSIG_MOB_RETRIEVE_ACCESS, Dm.list(pony, access)));
        Assert.True(r.In(() => Dm.@in(ACCESS_SYNDICATE, access)));

        r.In(() => Waiting.NoSleep(bob.mind.add_antag_datum(typeof(datum_antagonist_sentient_creature))));
        var creature = r.In(() => bob.mind.has_antag_datum(typeof(datum_antagonist_sentient_creature)) as datum_antagonist_sentient_creature);
        Assert.NotNull(creature);
        DmList data = r.In(() => Waiting.NoSleep(creature.ui_static_data(bob)) as DmList);
        Assert.Equal(0, r.In(() => Dm.num(data["holographic"])));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryNewItemSpawnsAndDeletesCleanly()
    {
        var (r, _, bob) = Room();
        IEnumerable<Type> Live(Type root) => root.Assembly.GetTypes()
            .Where(t => (t == root || t.IsSubclassOf(root)) && !Attribute.IsDefined(t, typeof(DmInertAttribute), inherit: false));
        var types = Live(typeof(obj_item_toy_plush)).Concat(Live(typeof(obj_item_bedsheet))).Concat(Live(typeof(obj_structure_bedsheetbin)))
            .Concat(Live(typeof(obj_item_poster))).Concat(Live(typeof(obj_item_cardboard_cutout))).Concat(new[]
            {
                typeof(obj_item_slimepotion_sentience), typeof(obj_item_slimepotion_sentience_nuclear), typeof(obj_item_reagent_containers_cup_glass_mug_tea),
                typeof(obj_item_clothing_under_syndicate_bloodred), typeof(obj_item_storage_box_syndie_kit_poster_box), typeof(obj_item_storage_box_syndie_kit_cutouts),
            });
        foreach (Type t in types)
        {
            var made = r.In(() => Dm.@new(t, bob.loc) as atom);
            r.Step(1);
            r.In(() => qdel(made));
            r.Step(1);
        }
        r.AssertNoRuntimes();
    }
}
