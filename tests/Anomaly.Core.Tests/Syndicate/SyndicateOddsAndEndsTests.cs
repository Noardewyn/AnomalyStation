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
/// Syndicate package G3b (D316), the small syndicate items: the cash briefcase holds tg's 5000 credits behind its lock, the syndicate
/// toolbox its tools, the seditious spraycan draws its rune in three steps and is spent, the syndie lipstick paints lips and gives the
/// deadly kiss, the syndicate tome bites its reader and writes the name in blood, the syndicate soap crumbles when it runs out, the
/// catnade lets five feral cats out; a sweep spawns and deletes each newly live type with no runtimes.
/// </summary>
public class SyndicateOddsAndEndsTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed = 41)
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
            typeof(obj_item_toy_balloon_syndicate), typeof(obj_item_sticker_syndicate), typeof(obj_item_grenade_spawnergrenade_cat),
            typeof(obj_item_traitor_spraycan), typeof(obj_effect_decal_cleanable_traitor_rune), typeof(datum_proximity_monitor_advanced_demoraliser),
            typeof(obj_item_soap_syndie), typeof(obj_item_book_bible_syndicate), typeof(obj_item_storage_toolbox_syndicate), typeof(obj_item_screwdriver_nuke),
            typeof(obj_item_storage_briefcase_secure_syndie), typeof(obj_item_stack_spacecash_c1000), typeof(obj_item_lipstick_syndie),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        var r = new AudioRoom(5, 5, seed: 3);
        foreach (Type entry in new[]
        {
            typeof(datum_uplink_item_badass_balloon), typeof(datum_uplink_item_badass_stickers), typeof(datum_uplink_item_badass_syndiecash),
            typeof(datum_uplink_item_badass_syndie_spraycan), typeof(datum_uplink_item_dangerous_cat), typeof(datum_uplink_item_device_tools_soap),
            typeof(datum_uplink_item_device_tools_syndietome), typeof(datum_uplink_item_device_tools_toolbox), typeof(datum_uplink_item_stealthy_weapons_slipstick),
        })
            Assert.False(r.In(() => uplink_item_hidden(Dm.@new(entry) as datum_uplink_item)), entry.Name + " is hidden");
    }

    [Fact]
    public void TheCashBriefcaseAndTheToolboxHoldTheirContents()
    {
        var (r, _, bob) = Room();
        var cash = r.In(() => Dm.@new<obj_item_storage_briefcase_secure_syndie>(bob.loc));
        r.Step(2);
        // tg's five bills merge into one stack as they are made (stack.dm's merge on Initialize)
        var bills = r.In(() => Dm.each<obj_item_stack_spacecash_c1000>(cash.contents).ToList());
        Assert.Single(bills);
        Assert.Equal(5, r.In(() => Dm.num(bills[0].amount)));
        Assert.Equal(5000f, r.In(() => Dm.num(bills[0].get_item_credit_value())));
        Assert.Equal("It's worth 5000 credits in total.", r.In(() => Dm.str(bills[0].desc)));
        Assert.NotNull(r.In(() => cash.GetComponent(typeof(datum_component_lockable_storage))));
        Assert.Equal(15, r.In(() => Dm.num(cash.force)));

        var toolbox = r.In(() => Dm.@new<obj_item_storage_toolbox_syndicate>(bob.loc));
        r.Step(2);
        Assert.Equal(7, r.In(() => Dm.length(toolbox.contents)));
        Assert.NotNull(r.In(() => Dm.locate<obj_item_screwdriver_nuke>(toolbox)));
        Assert.NotNull(r.In(() => Dm.locate<obj_item_clothing_gloves_combat>(toolbox)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSpraycanDrawsItsRuneInThreeStepsAndIsSpent()
    {
        var (r, feed, bob) = Room();
        var can = r.In(() => Dm.@new<obj_item_traitor_spraycan>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(can)));
        turf floor = r.In(() => bob.loc as turf);
        r.In(() => Waiting.Detach(can.interact_with_atom(floor, bob)));
        r.Step(10 * Second); // three 3-second steps: the outline, the design, the final coating
        var rune = r.In(() => Dm.locate<obj_effect_decal_cleanable_traitor_rune>(floor));
        Assert.NotNull(rune);
        Assert.Equal(obj_effect_decal_cleanable_traitor_rune.RUNE_STAGE_COMPLETE, r.In(() => rune.drawn_stage));
        Assert.Equal("traitor_rune_sheen", r.In(() => rune.icon_state));
        Assert.NotNull(r.In(() => rune.demoraliser));
        Assert.True(r.In(() => Dm.truthy(can.expended)));
        Assert.True(feed.Any("balloon: finished!"));
        // the finished rune cannot be washed off, and the spent can paints no more
        r.In(() => Waiting.NoSleep(rune.wash(CLEAN_ALL)));
        Assert.False(r.In(() => QDELETED(rune)));
        r.In(() => Waiting.Detach(can.interact_with_atom(floor, bob)));
        r.Step(2);
        Assert.True(feed.Any("balloon: all out of paint..."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLipstickTheTomeAndTheSoap()
    {
        var (r, feed, bob) = Room();
        var lipstick = r.In(() => Dm.@new<obj_item_lipstick_syndie>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(lipstick)));
        r.In(() => Waiting.NoSleep(lipstick.attack_self(bob)));
        Assert.Equal("slipstick_uncap", r.In(() => lipstick.icon_state));
        r.In(() => Waiting.NoSleep(lipstick.attack__proc(bob, bob)));
        r.Step(2);
        Assert.Equal("lipstick", r.In(() => Dm.str(bob.lip_style)));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_SYNDIE_KISS)));
        r.In(() => qdel(lipstick));

        var tome = r.In(() => Dm.@new<obj_item_book_bible_syndicate>(bob.loc));
        float brute = r.In(() => Dm.num(bob.get_brute_loss()));
        r.In(() => Waiting.NoSleep(tome.attack_self(bob)));
        r.Step(2);
        Assert.Equal(HOLY_ROLE_PRIEST, r.In(() => Dm.num(bob.mind.holy_role)));
        Assert.Equal(brute + 5, r.In(() => Dm.num(bob.get_brute_loss())));
        Assert.True(feed.Any("You try to open the book AND IT BITES YOU!"));
        Assert.Equal(r.In(() => bob.real_name), r.In(() => Dm.str(tome.owner_name)));
        Assert.Null(r.In(() => tome.get_attack_self_context(bob)));

        var soap = r.In(() => Dm.@new<obj_item_soap_syndie>(bob.loc));
        Assert.Equal(0.5f * SECONDS, r.In(() => soap.cleanspeed));
        Assert.NotNull(r.In(() => soap.GetComponent(typeof(datum_component_cleaner))));
        Assert.NotNull(r.In(() => soap.GetComponent(typeof(datum_component_slippery))));
        r.In(() => soap.uses = 1);
        r.In(() => soap.decreaseUses(null, bob.loc, bob, TRUE));
        Assert.True(r.In(() => QDELETED(soap)));
        Assert.True(feed.Any("crumbles into tiny bits!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCatnadeLetsFiveFeralCatsOutAndTheStickersAreContraband()
    {
        var (r, _, bob) = Room();
        var catnade = r.In(() => Dm.@new<obj_item_grenade_spawnergrenade_cat>(locate(8, 5, 1)));
        r.In(() => Waiting.Detach(catnade.detonate()));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(catnade)));
        Assert.Equal(5, r.In(() => Dm.each<mob_living_basic_pet_cat_feral>(Dm.world).Count(c => HAS_TRAIT(c, TRAIT_SPAWNED_MOB))));

        var stickers = r.In(() => Dm.@new<obj_item_storage_box_stickers_syndie_kit>(bob.loc));
        r.Step(2);
        Assert.True(r.In(() => Dm.length(stickers.contents)) > 0);
        Assert.All(r.In(() => Dm.each<obj_item>(stickers.contents).ToList()), s => Assert.True(r.In(() => s is obj_item_sticker_syndicate && HAS_TRAIT(s, TRAIT_CONTRABAND))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryNewItemSpawnsAndDeletesCleanly()
    {
        var (r, _, bob) = Room();
        var types = new[]
        {
            typeof(obj_item_toy_balloon_syndicate), typeof(obj_item_grenade_spawnergrenade_syndiesoap), typeof(obj_item_traitor_spraycan),
            typeof(obj_effect_decal_cleanable_traitor_rune), typeof(obj_item_soap), typeof(obj_item_soap_homemade), typeof(obj_item_soap_nanotrasen),
            typeof(obj_item_soap_deluxe), typeof(obj_item_soap_drone), typeof(obj_item_soap_omega), typeof(obj_item_book_bible_syndicate),
            typeof(obj_item_storage_briefcase_secure), typeof(obj_item_storage_briefcase_secure_digital_storage), typeof(obj_item_storage_briefcase_secure_riches),
            typeof(obj_item_storage_briefcase_hitchiker), typeof(obj_item_stack_spacecash_c1), typeof(obj_item_stack_spacecash_c10000), typeof(obj_item_lipstick),
            typeof(obj_item_lipstick_random), typeof(obj_item_lipstick_black_death),
        }.Concat(typeof(obj_item_sticker_syndicate).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(obj_item_sticker_syndicate))));
        foreach (Type t in types)
        {
            var made = r.In(() => Dm.@new(t, bob.loc) as atom);
            r.Step(1);
            r.In(() => qdel(made));
            r.Step(1);
        }
        Assert.Equal(new object[] { typeof(obj_item_stack_spacecash_c200), typeof(obj_item_stack_spacecash_c200), typeof(obj_item_stack_spacecash_c50) },
            r.In(() => Dm.each<object>(credits_to_spacecash(450)).ToList()));
        r.AssertNoRuntimes();
    }
}
