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
/// Gaps package MD3 (D333's station order: the medical machines) in a booted toy room with players. The limb grower grows a
/// synthetic human arm and a lizard one from its synthflesh in tg's time with its pictures, buzzes without enough reagent, empties a
/// reagent, imports a limb design disk's organs and, emagged, unlocks its illegal designs; the organ harvester refuses the living,
/// then strips a dead body of its limbs and organs one by one onto its output side; the lazarus injector revives a dead dog that
/// befriends its reviver, is spent after one use, a dead mouse comes back through its signal, and an EMP makes it malfunction.
/// </summary>
public class MedicalMD3Tests
{
    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static List<string> Designs(System.Text.Json.JsonElement payload, string category)
    {
        foreach (var c in payload.GetProperty("static_data").GetProperty("categories").EnumerateArray())
        {
            if (c.GetProperty("name").GetString() == category)
                return c.GetProperty("designs").EnumerateArray().Select(d => d.GetProperty("name").GetString()).ToList();
        }
        return new List<string>();
    }

    private static List<string> Examine(AudioRoom r, atom thing, mob who) => r.In(() => Dm.each(Waiting.NoSleep(thing.examine(who)) as DmList).Select(Dm.str).ToList());

    [Fact]
    public void The_limb_grower_grows_synthetic_limbs_from_synthflesh_and_imports_a_disk()
    {
        var r = new AudioRoom(13, 9, seed: 9931);
        Feed feed = Listen(r);
        var bob = r.Join(5, 5, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var grower = Place<obj_machinery_limbgrower>(r, 5, 6);
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(grower));
            Assert.Equal("limbgrower_idleoff", Dm.str(grower.icon_state));
            Assert.Equal(100f, Dm.num((grower.reagents as datum_reagents).maximum_volume)); // its two beakers
            Assert.Equal(1f, grower.production_coefficient);
            Waiting.NoSleep((grower.reagents as datum_reagents).add_reagent(typeof(datum_reagent_medicine_c2_synthflesh), 60));
        });

        // the window: the reagents, and the human tab's limbs
        Click(r, bob, grower);
        var payload = LastUpdate(Take(bob), out string windowId);
        Assert.Equal("Limbgrower", InterfaceOf(payload));
        Assert.Equal(60, payload.GetProperty("data").GetProperty("total_reagents").GetDouble());
        Assert.Contains("Left Arm", Designs(payload, "human"));
        Assert.Contains("Left Arm", Designs(payload, "lizard"));

        // grow a human left arm: 25u synthflesh, three seconds
        Act(r, bob, windowId, "make_limb", ("design_path", "/datum/design/leftarm"), ("active_tab", "human"));
        r.In(() =>
        {
            Assert.Equal(1f, Dm.num(grower.busy));
            Assert.Equal("limbgrower_idleon", Dm.str(grower.icon_state));
        });
        r.Step(4 * Second);
        var arm = r.In(() => Dm.each<obj_item_bodypart_arm_left>(grower.loc.contents).Single());
        r.In(() =>
        {
            Assert.Equal(typeof(obj_item_bodypart_arm_left), arm.GetType());
            Assert.Contains("synthetic human left arm", Dm.str(arm.name));
            Assert.Equal("#62A262", arm.species_color);
            Assert.Equal(0f, Dm.num(grower.busy));
            Assert.Equal("limbgrower_idleoff", Dm.str(grower.icon_state));
            Assert.Equal(35f, Dm.num((grower.reagents as datum_reagents).get_reagent_amount(typeof(datum_reagent_medicine_c2_synthflesh))));
        });

        // a lizard's arm is its species' subtype
        Act(r, bob, windowId, "make_limb", ("design_path", "/datum/design/leftarm"), ("active_tab", "lizard"));
        r.Step(4 * Second);
        r.In(() =>
        {
            var lizard_arm = Dm.each<obj_item_bodypart_arm_left_lizard>(grower.loc.contents).Single();
            Assert.Contains("synthetic lizard left arm", Dm.str(lizard_arm.name));
            Assert.Equal("lizard", lizard_arm.limb_id);
        });

        // 10u left: not enough for a third arm, it buzzes
        Act(r, bob, windowId, "make_limb", ("design_path", "/datum/design/leftarm"), ("active_tab", "human"));
        Assert.True(feed.Any("buzzes."), Diag(feed, r));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("buzz-sigh.ogg", StringComparison.Ordinal));
        Assert.Equal(0f, r.In(() => Dm.num(grower.busy)));

        // empty the synthflesh
        Act(r, bob, windowId, "empty_reagent", ("reagent_type", "/datum/reagent/medicine/c2/synthflesh"));
        Assert.Equal(0f, r.In(() => Dm.num((grower.reagents as datum_reagents).total_volume)));

        // the lizard organ disk: two seconds of loading, then its designs are in the lizard tab
        var disk = Hold<obj_item_disk_design_disk_limbs_lizard>(r, bob);
        r.In(() => Waiting.Detach(grower.item_interaction(bob, disk, Dm.list())));
        Assert.True(feed.Any("You begin to load designs from"), Diag(feed, r));
        r.Step(3 * Second);
        Assert.True(r.In(() => Dm.truthy(grower.imported_designs?[typeof(datum_design_lizard_tail)])), Diag(feed, r));
        var lizard_designs = r.In(() =>
        {
            var static_data = (DmList)Waiting.NoSleep(grower.ui_static_data(bob));
            DmList lizard = Dm.each<DmList>(static_data["categories"] as DmList).First(c => Dm.str(c["name"]) == "lizard");
            return Dm.each<DmList>(lizard["designs"] as DmList).Select(d => Dm.str(d["name"])).ToList();
        });
        Assert.Contains("Lizard Tail", lizard_designs);

        // emagged: the illegal limb production
        var emag = r.In(() => Dm.@new<obj_item_card_emag>(bob.loc));
        r.In(() => Assert.Equal(TRUE, Waiting.NoSleep(grower.emag_act(bob, emag))));
        Assert.True(feed.Any("balloon: illegal limb production enabled"), Diag(feed, r));
        r.In(() => Assert.True(Dm.truthy(Dm.bitand(grower.obj_flags, EMAGGED))));
        Assert.Contains(Examine(r, grower, bob), l => l.Contains("Reagent consumption rate at <b>100%</b>", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_organ_harvester_refuses_the_living_and_strips_a_dead_body_onto_its_output()
    {
        var r = new AudioRoom(13, 9, seed: 9932);
        Feed feed = Listen(r);
        var bob = r.Join(4, 4, "Bob");
        var harvester = Place<obj_machinery_harvester>(r, 6, 6);
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(harvester));
            Assert.Equal(36f, harvester.interval); // four tier 1 micro lasers
            Assert.Equal("harvester", Dm.str(harvester.icon_state));
        });

        // open it, lay a naked man in it and close it
        r.In(() => Waiting.NoSleep(harvester.open_machine()));
        r.In(() => Assert.Equal("harvester-open", Dm.str(harvester.icon_state)));
        var rob = r.Dummy(6, 6);
        r.Step(2);
        r.In(() => Waiting.NoSleep(harvester.close_machine(rob)));
        r.In(() => Assert.Same(rob, harvester.occupant));

        // alive: refused
        r.In(() => Assert.Equal(CLICK_ACTION_BLOCKING, Waiting.NoSleep(harvester.click_alt(bob))));
        r.Step(2);
        Assert.True(feed.Any("Subject is still alive."), Diag(feed, r));

        // dead: harvested limb by limb onto the south tile
        r.In(() => Waiting.NoSleep(rob.death()));
        r.In(() => Assert.Equal(CLICK_ACTION_SUCCESS, Waiting.NoSleep(harvester.click_alt(bob))));
        r.Step(2);
        Assert.True(feed.Any("Initializing harvest protocol."), Diag(feed, r));
        r.In(() => Assert.Equal("harvester-charging", Dm.str(harvester.icon_state)));
        r.Step(5 * Second);
        r.In(() => Assert.Equal("harvester-active", Dm.str(harvester.icon_state)));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("juicer.ogg", StringComparison.Ordinal));
        r.Step(30 * Second);
        Assert.True(feed.Any("Subject has been successfully harvested."), Diag(feed, r));
        r.In(() =>
        {
            Assert.Equal(1f, Dm.num(harvester.state_open));
            Assert.Equal("harvester-open", Dm.str(harvester.icon_state));
            var output = locate(6, 5, 1) as turf;
            Assert.True(Dm.each<obj_item_bodypart>(output.contents).Count() >= 4, "too few limbs on the output tile");
            Assert.True(Dm.each<obj_item_organ>(output.contents).Any(), "no organs on the output tile");
        });
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("microwave-end.ogg", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_lazarus_injector_revives_a_dead_animal_once_and_malfunctions_under_an_emp()
    {
        var r = new AudioRoom(13, 9, seed: 9933);
        Feed feed = Listen(r);
        var bob = r.Join(5, 5, "Bob");
        var dog = r.In(() => Dm.@new<mob_living_basic_pet_dog_corgi>(locate(6, 5, 1)));
        var injector = Hold<obj_item_lazarus_injector>(r, bob);
        r.In(() => Assert.False(InertSeam.IsInert(injector)));

        // alive: refused; a man: an invalid creature
        r.In(() => Assert.Equal(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(injector.interact_with_atom(dog, bob, Dm.list()))));
        Assert.True(feed.Any("balloon: it's not dead!"), Diag(feed, r));
        var rob = r.Dummy(4, 5);
        r.In(() => Waiting.NoSleep(rob.death()));
        r.In(() => Assert.Equal(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(injector.interact_with_atom(rob, bob, Dm.list()))));
        Assert.True(feed.Any("balloon: invalid creature!"), Diag(feed, r));

        // the dead dog comes back, friendly to Bob
        r.In(() => Waiting.NoSleep(dog.death()));
        r.In(() => Assert.Equal(DEAD, dog.stat));
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(injector.interact_with_atom(dog, bob, Dm.list()))));
        r.Step(2);
        r.In(() =>
        {
            Assert.NotEqual(DEAD, dog.stat);
            Assert.True(Dm.@in(FACTION_NEUTRAL, dog.faction));
            Assert.True(Dm.truthy(dog.has_ally(REF(bob))));
            Assert.Equal(0f, Dm.num(injector.loaded));
            Assert.Equal("lazarus_empty", Dm.str(injector.icon_state));
        });
        Assert.True(feed.Any("reviving it."), Diag(feed, r));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("refill.ogg", StringComparison.Ordinal));
        Assert.Contains(Examine(r, injector, bob), l => l.Contains("is empty.", StringComparison.Ordinal));
        // spent: nothing more
        r.In(() => Waiting.NoSleep(dog.death()));
        r.In(() => Assert.Equal(NONE, Waiting.NoSleep(injector.interact_with_atom(dog, bob, Dm.list()))));

        // a dead mouse comes back through its own signal, and spends a fresh injector
        r.In(() => qdel(injector));
        var fresh = Hold<obj_item_lazarus_injector>(r, bob);
        var dead_mouse = r.In(() => Dm.@new<obj_item_food_deadmouse>(locate(6, 4, 1)));
        r.Step(2);
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(fresh.interact_with_atom(dead_mouse, bob, Dm.list()))));
        r.Step(2);
        r.In(() =>
        {
            Assert.Contains(Dm.each<mob_living_basic_mouse>(locate(6, 4, 1).contents), m => m.stat != DEAD);
            Assert.Equal(0f, Dm.num(fresh.loaded));
        });

        // an EMP: the display flickers, and the revived is not befriended
        r.In(() => qdel(fresh));
        var broken = Hold<obj_item_lazarus_injector>(r, bob);
        r.In(() => Waiting.NoSleep(broken.emp_act(EMP_HEAVY)));
        r.In(() => Assert.Equal(1f, Dm.num(broken.malfunctioning)));
        Assert.Contains(Examine(r, broken, bob), l => l.Contains("seems to be flickering", StringComparison.Ordinal));
        var dog2 = r.In(() => Dm.@new<mob_living_basic_pet_dog_corgi>(locate(7, 5, 1)));
        r.In(() => Waiting.NoSleep(dog2.death()));
        r.In(() => Waiting.NoSleep(broken.interact_with_atom(dog2, bob, Dm.list())));
        r.Step(2);
        r.In(() =>
        {
            Assert.NotEqual(DEAD, dog2.stat);
            Assert.False(Dm.truthy(dog2.has_ally(REF(bob))));
        });
        r.AssertNoRuntimes();
    }
}
