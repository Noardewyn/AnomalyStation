using System.Text.Json;
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
/// Gaps package RD4 (D333, the experimentor) in a booted toy room with Bob as a player: the E.X.P.E.R.I-MENTOR takes an alien
/// cautery, gives its type a reaction, shows it in its Experimentor window, fails a wrong experiment, reveals the cautery's hidden
/// node with the right one and ejects it; a grenade's critical irradiation clones it; a relic's Discover reveals its power. The
/// revealed relics: a free drink once per cooldown, a necrotech shield that blocks a hit, and a costume disguise with a cardboard ID.
/// </summary>
public class ExperimentorRD4Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, datum_techweb Web) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        datum_techweb web = r.In(() => Dm.each<datum_techweb>(SSresearch.techwebs).OfType<datum_techweb_science>().FirstOrDefault());
        Assert.NotNull(web);
        return (r, feed, bob, web);
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
    private static void Use(AudioRoom r, mob who, atom target)
    {
        r.Step(Second);
        Click(r, who, target);
    }

    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    [Fact]
    public void TheExperimentorRevealsANodeClonesACriticalItemAndDiscoversARelic()
    {
        var (r, feed, bob, web) = Room(8411);
        var experimentor = Place<obj_machinery_rnd_experimentor>(r, 5, 4);
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(experimentor));
            Assert.Same(web, experimentor.stored_research); // linked at round start (no server: the station's web)
            Assert.Equal(8, Dm.length(experimentor.experimentor_result_handlers)); // six scans, Discover and the failure
            experimentor.malfunction_probability_coeff = 100; // no malfunction: the test drives the reactions
        });
        // the cautery unlocks two hidden nodes (alien_nodes.dm:17, :85: the alien technology and the alien surgery); a matched
        // experiment reveals one of them, picked
        List<Type> cauteryNodes = r.In(() => Dm.each<Type>(SSresearch.techweb_unlock_items[typeof(obj_item_cautery_alien)]).ToList());
        Assert.Contains(typeof(datum_techweb_node_alien_base), cauteryNodes);
        int Hidden() => r.In(() => cauteryNodes.Count(n => Dm.truthy(web.hidden_nodes[n])));
        Assert.Equal(cauteryNodes.Count, Hidden());

        // the cautery goes in with a left click and its type gets a reaction
        var cautery = Hold<obj_item_cautery_alien>(r, bob);
        Use(r, bob, experimentor);
        r.In(() =>
        {
            Assert.Same(cautery, experimentor.loaded_item);
            Assert.Same(experimentor, cautery.loc);
            object reaction = experimentor.item_reactions["/obj/item/cautery/alien"];
            Assert.Contains(Dm.str(reaction), new[] { "Poke", "Irradiate", "Gas", "Heat", "Freeze", "Obliterate" });
            experimentor.item_reactions["/obj/item/cautery/alien"] = SCANTYPE_POKE; // a reaction that keeps the item
        });
        Assert.True(feed.Any("You put"), Diag(feed, r));

        // its window shows the item, the node it can reveal and the experiments
        Take(bob);
        Use(r, bob, experimentor);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("Experimentor", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(1, data.GetProperty("isServerConnected").GetDouble());
        Assert.Equal(1, data.GetProperty("hasItem").GetDouble());
        JsonElement item = data.GetProperty("loadedItem");
        Assert.Equal("alien cautery", item.GetProperty("name").GetString());
        Assert.Equal("icons/obj/antags/abductor.dmi", item.GetProperty("icon").GetProperty("icon").GetString());
        Assert.Equal(0, item.GetProperty("associatedNodes").EnumerateArray().First().GetProperty("isUnlocked").GetDouble());
        var experiments = data.GetProperty("availableExperiments").EnumerateArray().ToList();
        Assert.Equal(7, experiments.Count);
        Assert.Equal(0, experiments.Single(e => e.GetProperty("id").GetString() == "Discover").GetProperty("isAvailable").GetDouble());
        Assert.Equal("radiation", experiments.Single(e => e.GetProperty("id").GetString() == "Irradiate").GetProperty("fa_icon").GetString());

        // the wrong experiment fails and reveals nothing
        Act(r, bob, window, "experiment", ("id", "Heat"));
        Assert.True(feed.Any("the experiment was a failure."), Diag(feed, r));
        Assert.Equal(cauteryNodes.Count, Hidden());

        // the right one reveals the cautery's node
        r.Step(Second);
        Act(r, bob, window, "experiment", ("id", "Poke"));
        // tg's replacetext() turns the item into its name as text on its own, so its article is capitalized ("prods at The alien
        // cautery"); only the rest is checked
        Assert.True(feed.Any("alien cautery with mechanical arms."), Diag(feed, r));
        Assert.Equal(cauteryNodes.Count - 1, Hidden()); // one of its nodes revealed
        Assert.Same(experimentor, r.In(() => cautery.loc));

        // eject: the item lands east of the machine
        Act(r, bob, window, "eject");
        r.In(() =>
        {
            Assert.Null(experimentor.loaded_item);
            Assert.Same(get_step(experimentor, EAST), cautery.loc);
        });

        // a grenade is critical: a critical irradiation clones it
        var grenade = Hold<obj_item_grenade_frag>(r, bob);
        Use(r, bob, experimentor);
        r.In(() =>
        {
            Assert.Same(grenade, experimentor.loaded_item);
            experimentor.item_reactions["/obj/item/grenade/frag"] = SCANTYPE_IRRADIATE;
            (experimentor.experimentor_result_handlers[SCANTYPE_IRRADIATE] as datum_experimentor_result_handler).critical_prob = 100;
        });
        r.Step(Second);
        Act(r, bob, window, "experiment", ("id", "Irradiate"));
        Assert.True(feed.Any("has activated an unknown subroutine!"), Diag(feed, r));
        Assert.True(feed.Any("A duplicate of the frag grenade pops out!"), Diag(feed, r));
        r.In(() =>
        {
            Assert.Same(grenade, experimentor.loaded_item);
            Assert.Single(Dm.each<obj_item_grenade_frag>(get_step(experimentor, EAST).contents));
        });
        Act(r, bob, window, "eject");

        // a relic reacts only to Discover, which reveals its power and ejects it
        var relic = Hold<obj_item_assembly_relic>(r, bob);
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(relic));
            Assert.StartsWith("strange ", Dm.str(relic.name));
            Assert.StartsWith("prototype", Dm.str(relic.icon_state));
        });
        Use(r, bob, experimentor);
        r.In(() => Assert.Equal(SCANTYPE_DISCOVER, Dm.str(experimentor.item_reactions["/obj/item/assembly/relic"])));
        r.Step(Second);
        Act(r, bob, window, "experiment", ("id", "Discover"));
        Assert.True(feed.Any("revealing its true nature!"), Diag(feed, r));
        r.In(() =>
        {
            Assert.True(Dm.truthy(relic.activated));
            Assert.Equal(relic.real_name, Dm.str(relic.name));
            Assert.NotNull(relic.hidden_power);
            Assert.Null(experimentor.loaded_item);
            Assert.Same(get_step(experimentor, EAST), relic.loc);
        });
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("supermatter.ogg", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void RevealedRelicsPourADrinkShieldTheirHolderAndDisguiseThem()
    {
        var (r, feed, bob, _) = Room(8412);

        // a drink dispenser: a glass of something alcoholic next to its user, then not again until its cooldown
        var relic = Hold<obj_item_assembly_relic>(r, bob);
        r.In(() =>
        {
            relic.hidden_power = nameof(obj_item_assembly_relic.drink_dispenser);
            relic.reveal();
            Assert.InRange(Dm.num(relic.activation_cooldown), 60, 300);
            Waiting.NoSleep(relic.attack_self(bob));
        });
        r.Step(Second);
        r.In(() =>
        {
            var glass = Dm.each<obj_item_reagent_containers_cup_glass_drinkingglass>(range(1, bob)).Single();
            Assert.True(Dm.num((glass.reagents as datum_reagents).total_volume) > 0, "the glass is empty");
            Assert.Contains(Dm.each<datum_reagent>((glass.reagents as datum_reagents).reagent_list), x => x is datum_reagent_consumable_ethanol);
            Waiting.NoSleep(relic.attack_self(bob));
        });
        Assert.True(feed.Any("does not react!"), Diag(feed, r));
        r.In(() => qdel(relic));

        // a necrotech shield: the relic in hand blocks a hit and glows
        var necro = Hold<obj_item_assembly_relic_lavaland>(r, bob);
        r.In(() =>
        {
            Assert.Equal("strange relic", Dm.str(necro.name));
            Assert.StartsWith("necrotech", Dm.str(necro.icon_state));
            necro.hidden_power = nameof(obj_item_assembly_relic.t1_shield_holder);
            necro.reveal();
            Waiting.NoSleep(necro.attack_self(bob));
        });
        Assert.True(feed.Any("starts to glow a bright yellow!"), Diag(feed, r));
        var shield = r.In(() => necro.GetComponent(typeof(datum_component_shielded)) as datum_component_shielded);
        Assert.NotNull(shield);
        r.In(() =>
        {
            Assert.Same(bob, shield.wearer);
            Assert.Equal(3f, Dm.num(shield.current_charges));
            Assert.True(Dm.truthy(Waiting.NoSleep(necro.hit_reaction(bob, null, "the punch", 0, 10, MELEE_ATTACK))), "the hit was not blocked");
            Assert.Equal(2f, Dm.num(shield.current_charges));
        });
        r.Step(Second);
        Assert.True(feed.Any("blocking the punch with a projected shield!"), Diag(feed, r));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("marauder.ogg", StringComparison.Ordinal));
        r.In(() => qdel(necro));

        // a disguise: a costume and a cardboard ID with someone else's name
        r.In(() =>
        {
            var suit = Dm.@new<obj_item_clothing_under_color_grey>(bob.loc);
            Waiting.NoSleep(bob.equip_to_slot_or_del(suit, ITEM_SLOT_ICLOTHING));
        });
        var disguiser = Hold<obj_item_assembly_relic>(r, bob);
        r.In(() =>
        {
            disguiser.hidden_power = nameof(obj_item_assembly_relic.disguiser);
            disguiser.reveal();
            Waiting.NoSleep(disguiser.attack_self(bob));
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.NotNull(bob.head);
            Assert.NotNull(bob.w_uniform);
            Assert.IsNotType<obj_item_clothing_under_color_grey>(bob.w_uniform);
            Assert.True(Dm.truthy(Dm.bitand(bob.head.item_flags, DROPDEL)), "the costume is not DROPDEL");
            var card = Assert.IsType<obj_item_card_cardboard>(bob.wear_id);
            Assert.False(InertSeam.IsInert(card));
            string scribbled = Dm.str(card.scribbled_name);
            Assert.Contains(" ", scribbled);
            Assert.StartsWith(scribbled + "'s ID Card (", Dm.str(card.name));
            Assert.Equal(scribbled, Dm.str(card.get_displayed_name()));
        });
        r.AssertNoRuntimes();
    }
}
