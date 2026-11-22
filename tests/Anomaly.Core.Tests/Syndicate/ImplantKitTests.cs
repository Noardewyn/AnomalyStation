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
/// Syndicate package G3 (D316), the implants and the kits: the freedom implant breaks the cuffs, the storage implant holds two items
/// and stacks, the syndicate radio implant speaks on the syndicate channel, the uplink implant carries a charged uplink, the
/// microbomb explodes on death, the syndi-kit delivers one of tg's bundles, the poison kit's bottles hold their reagents, the
/// stealth implant hides its host in a box; a sweep spawns and deletes every implant, implanter and kit with no runtimes.
/// </summary>
public class ImplantKitTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, mob_living_carbon_human Alice) Room(ulong seed = 31)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        mob_living_carbon_human alice = r.Join(6, 4, "Alice");
        r.In(() => { Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))); Waiting.NoSleep(alice.equipOutfit(typeof(datum_outfit_job_assistant))); });
        r.Step(Second);
        return (r, feed, bob, alice);
    }

    /// Implants the implanter's implant into the target, as the implanter's own use does.
    private static T Implant<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item_implant
    {
        T imp = r.In(() => Dm.@new<T>(who.loc));
        object ok = r.In(() => Waiting.NoSleep(imp.implant(who, who, TRUE)));
        r.Step(2);
        Assert.True(Dm.truthy(ok), typeof(T).Name + " did not implant");
        return imp;
    }

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_implant_freedom), typeof(obj_item_implant_emp), typeof(obj_item_implant_radio), typeof(obj_item_implant_radio_syndicate),
            typeof(obj_item_implant_storage), typeof(datum_storage_implant), typeof(obj_item_implant_uplink), typeof(obj_item_implanter_uplink),
            typeof(obj_item_implant_explosive), typeof(obj_item_implant_explosive_macro_fake), typeof(obj_item_implant_explosive_deniability),
            typeof(obj_item_implant_deathrattle), typeof(datum_deathrattle_group), typeof(obj_structure_closet_cardboard_agent),
            typeof(datum_action_item_action_agent_box), typeof(obj_item_storage_box_syndicate_bundle_a), typeof(obj_item_storage_box_syndicate_bundle_b),
            typeof(obj_item_storage_box_syndie_kit_chemical), typeof(obj_item_storage_box_syndie_kit_imp_freedom), typeof(obj_item_storage_box_syndie_kit_space),
            typeof(datum_storage_box_syndicate_space), typeof(datum_storage_box_syndicate_throwing), typeof(obj_item_implant_nuclear_operative),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        // the kits whose contents are live sell; the uplink implant does not wait any more
        var r = new AudioRoom(5, 5, seed: 3);
        Assert.False(r.In(() => uplink_item_hidden(Dm.@new<datum_uplink_item_implants_uplink>())));
        Assert.False(r.In(() => uplink_item_hidden(Dm.@new<datum_uplink_item_implants_freedom>())));
        Assert.True(r.In(() => uplink_item_hidden(Dm.@new<datum_uplink_item_bundles_tc_bundle_a>())));
    }

    [Fact]
    public void TheFreedomImplantBreaksTheCuffsAndDegrades()
    {
        var (r, feed, bob, _) = Room();
        var imp = Implant<obj_item_implant_freedom>(r, bob);
        r.In(() => Waiting.Detach(imp.activate("action_button")));
        r.Step(2);
        Assert.True(feed.Any("balloon: no restraints!"));

        var cuffs = r.In(() => Dm.@new<obj_item_restraints_handcuffs>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.equip_to_slot(cuffs, ITEM_SLOT_HANDCUFFED)));
        r.Step(2);
        Assert.Same(cuffs, r.In(() => bob.handcuffed));
        float uses = r.In(() => imp.uses);
        r.In(() => Waiting.Detach(imp.activate("action_button")));
        r.Step(4);
        Assert.Null(r.In(() => bob.handcuffed));
        Assert.Equal(uses - 1, r.In(() => imp.uses));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStorageImplantHoldsTwoItemsAndASecondOneStacks()
    {
        var (r, _, bob, _) = Room();
        var imp = Implant<obj_item_implant_storage>(r, bob);
        datum_storage pocket = r.In(() => imp.atom_storage);
        Assert.IsType<datum_storage_implant>(pocket);
        Assert.Equal(2, r.In(() => pocket.max_slots));
        Assert.True(r.In(() => HAS_TRAIT(imp, TRAIT_CONTRABAND_BLOCKER)));
        var a = r.In(() => Dm.@new<obj_item_pen>(bob.loc));
        var b = r.In(() => Dm.@new<obj_item_pen>(bob.loc));
        var c = r.In(() => Dm.@new<obj_item_pen>(bob.loc));
        r.In(() => { pocket.attempt_insert(a, bob); pocket.attempt_insert(b, bob); pocket.attempt_insert(c, bob); });
        r.Step(2);
        Assert.Same(imp, r.In(() => a.loc));
        Assert.Same(imp, r.In(() => b.loc));
        Assert.NotSame(imp, r.In(() => c.loc));

        // a second storage implant widens the first pocket (tg's max_slot_stacking) and is spent
        var second = Implant<obj_item_implant_storage>(r, bob);
        Assert.Equal(4, r.In(() => pocket.max_slots));
        Assert.False(r.In(() => Dm.@in(second, bob.implants)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSyndicateRadioImplantSpeaksOnTheSyndicateChannel()
    {
        var (r, feed, bob, alice) = Room();
        var imp = Implant<obj_item_implant_radio_syndicate>(r, bob);
        Assert.True(r.In(() => Dm.@in(RADIO_CHANNEL_SYNDICATE, imp.radio.channels)));
        Assert.Equal("internal radio", r.In(() => imp.radio.name));
        // the implant's radio is subspace only: the operatives' telecomms mainframe relays the syndicate channel
        Place<obj_machinery_telecomms_allinone_nuclear>(r, 1, 1);
        // Alice listens on a syndicate headset, out of earshot of Bob's voice
        r.In(() => qdel(alice.ears)); // the assistant's headset
        r.Step(1);
        var headset = r.In(() => Dm.@new<obj_item_radio_headset_syndicate_alt>(alice.loc));
        r.In(() => Waiting.NoSleep(alice.equip_to_slot_or_del(headset, ITEM_SLOT_EARS)));
        r.In(() => Waiting.NoSleep(alice.forceMove(locate(10, 8, 1))));
        r.Step(2);
        object said = r.In(() => Waiting.NoSleep(bob.radio__proc("the eagle has landed", Dm.list(Dm.kv(RADIO_EXTENSION, RADIO_CHANNEL_SYNDICATE)), null, null)));
        r.Step(4);
        Assert.Equal(Dm.bitor(ITALICS, REDUCE_RANGE), Dm.num(said));
        Assert.Contains(feed.Chat, c => c.Target == alice && c.Html.Contains("syndradio", StringComparison.Ordinal) && c.Html.Contains("the eagle has landed", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheUplinkImplantCarriesAChargedUplinkThatOpens()
    {
        var (r, _, bob, _) = Room();
        var implanter = r.In(() => Dm.@new<obj_item_implanter_uplink_precharged>(bob.loc));
        var imp = r.In(() => implanter.imp) as obj_item_implant_uplink;
        Assert.NotNull(imp);
        r.In(() => Waiting.NoSleep(imp.implant(bob, bob, TRUE)));
        r.Step(2);
        var uplink = r.In(() => imp.GetComponent(typeof(datum_component_uplink))) as datum_component_uplink;
        Assert.NotNull(uplink);
        Assert.Equal(TELECRYSTALS_PRELOADED_IMPLANT, r.In(() => Dm.num(uplink.uplink_handler.telecrystals)));
        Assert.Same(bob.mind, r.In(() => uplink.uplink_handler.owner));
        r.In(() => Take(bob));
        r.In(() => Waiting.Detach(imp.activate("action_button")));
        r.Step(4);
        Assert.Contains(r.In(() => Take(bob)), o => o.Json != null && o.Json.Contains("\"Uplink\""));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMicrobombExplodesOnDeath()
    {
        var (r, _, bob, _) = Room();
        var imp = Implant<obj_item_implant_explosive>(r, bob);
        r.In(() => Waiting.Detach(bob.death()));
        r.Step(Second);
        Assert.True(r.In(() => QDELETED(imp)));
        Assert.True(r.In(() => QDELETED(bob)), "the microbomb gibs its host");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStealthImplantHidesItsHostInABox()
    {
        var (r, _, bob, _) = Room();
        Implant<obj_item_implant_stealth>(r, bob);
        var action = r.In(() => Dm.locate<datum_action_item_action_agent_box>(bob.actions));
        Assert.NotNull(action);
        r.In(() => Waiting.Detach(action.Trigger()));
        r.Step(Second);
        var box = r.In(() => bob.loc) as obj_structure_closet_cardboard_agent;
        Assert.NotNull(box);
        Assert.Equal(0, r.In(() => Dm.num(box.alpha)));
        // the second press opens the box, which goes away
        r.In(() => action.box_cooldown = 0);
        r.In(() => Waiting.Detach(action.Trigger()));
        r.Step(4);
        Assert.True(r.In(() => isturf(bob.loc)));
        Assert.True(r.In(() => QDELETED(box)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheKitsDeliverTheirContents()
    {
        var (r, _, bob, _) = Room();
        var poison = r.In(() => Dm.@new<obj_item_storage_box_syndie_kit_chemical>(bob.loc));
        r.Step(2);
        Assert.Equal(15, r.In(() => Dm.length(poison.contents)));
        var polonium = r.In(() => Dm.locate<obj_item_reagent_containers_cup_bottle_polonium>(poison));
        Assert.True(r.In(() => Dm.truthy(((datum_reagents)polonium.reagents).has_reagent(typeof(datum_reagent_toxin_polonium)))));

        var freedom = r.In(() => Dm.@new<obj_item_storage_box_syndie_kit_imp_freedom>(bob.loc));
        Assert.IsType<obj_item_implant_freedom>(r.In(() => Dm.locate<obj_item_implanter_freedom>(freedom).imp));

        var space = r.In(() => Dm.@new<obj_item_storage_box_syndie_kit_space>(bob.loc));
        Assert.NotNull(r.In(() => Dm.locate<obj_item_clothing_suit_space_syndicate>(space)));
        Assert.NotNull(r.In(() => Dm.locate<obj_item_clothing_head_helmet_space_syndicate>(space)));

        var rattle = r.In(() => Dm.@new<obj_item_storage_box_syndie_kit_imp_deathrattle>(bob.loc));
        Assert.Equal(9, r.In(() => Dm.length(rattle.contents)));
        Assert.Contains("The implants are registered to the", r.In(() => Dm.str(rattle.desc)));

        // the syndi-kit picks one of tg's bundles: never empty
        for (int i = 0; i < 6; i++)
        {
            var kit = r.In(() => Dm.@new<obj_item_storage_box_syndicate_bundle_a>(bob.loc));
            Assert.True(r.In(() => Dm.length(kit.contents)) >= 5);
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryImplantImplanterAndKitSpawnsAndDeletesCleanly()
    {
        var (r, _, bob, _) = Room();
        var types = typeof(obj_item_implant).Assembly.GetTypes()
            .Where(t => (t.IsSubclassOf(typeof(obj_item_implant)) || t.IsSubclassOf(typeof(obj_item_implanter)) || t.IsSubclassOf(typeof(obj_item_implantcase))
                || t.IsSubclassOf(typeof(obj_item_storage_box_syndie_kit)) || t.IsSubclassOf(typeof(obj_item_storage_box_syndicate))
                || t == typeof(obj_structure_closet_cardboard) || t == typeof(obj_structure_closet_cardboard_agent))
                && !t.IsAbstract && !Attribute.IsDefined(t, typeof(DmInertAttribute), inherit: false)
                // tg's own stack trace: the circus kit's toy pistol loads riot darts heavier than its magazine's metal, and the
                // magazine's material count goes negative (_box_magazine.dm:54, atom_materials.dm:46)
                && t != typeof(obj_item_storage_box_syndie_kit_induction_kit_clown))
            .ToList();
        Assert.True(types.Count > 60, "found " + types.Count);
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
