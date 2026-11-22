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
/// Syndicate stage N3b (D316) in a booted toy room: the bananium nuke and the fear system. The bananium nuke's cinematics and icons; its
/// effect clowns a human for good (undroppable clown clothes, clumsiness, a phobia of clowns); a clown in sight or the word "honk" frightens
/// the phobic; terror past the panic line races the heart; a hug calms; losing the phobia drops its fear.
/// </summary>
public class SyndicateN3bTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(15, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static datum_component_fearful Fear(AudioRoom r, mob who)
        => r.In(() => who.GetComponent(typeof(datum_component_fearful)) as datum_component_fearful);

    [Fact]
    public void TheFearSystemIsLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_component_fearful), typeof(datum_terror_handler), typeof(datum_terror_handler_jittering), typeof(datum_terror_handler_stuttering),
            typeof(datum_terror_handler_heart_problems), typeof(datum_terror_handler_vomiting), typeof(datum_terror_handler_panic),
            typeof(datum_terror_handler_startle), typeof(datum_terror_handler_force_stop_drop_roll), typeof(datum_terror_handler_simple_source_nyctophobia),
            typeof(datum_terror_handler_simple_source_claustrophobia), typeof(datum_terror_handler_simple_source_pyrophobia),
            typeof(datum_terror_handler_phobia_source), typeof(datum_terror_handler_phobia_source_blood), typeof(datum_brain_trauma_mild_phobia),
            typeof(datum_brain_trauma_mild_phobia_clowns), typeof(obj_machinery_nuclearbomb_syndicate_bananium),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheBananiumNukeShowsItsOwnCinematicsAndIcons()
    {
        var (r, _, _) = Room(2001);
        var nuke = r.In(() => Dm.@new<obj_machinery_nuclearbomb_syndicate_bananium>(locate(8, 4, 1)));
        r.Step(2);
        Assert.Equal(typeof(datum_cinematic_nuke_clown), r.In(() => nuke.get_cinematic_type(DETONATION_HIT_STATION)));
        Assert.Equal(typeof(datum_cinematic_nuke_ops_miss), r.In(() => nuke.get_cinematic_type(DETONATION_NEAR_MISSED_STATION)));
        Assert.Equal(typeof(datum_cinematic_nuke_fake), r.In(() => nuke.get_cinematic_type(DETONATION_MISSED_STATION)));
        Assert.Equal("bananiumbomb_base", r.In(() => Dm.str(nuke.icon_state)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBananiumNukeClownsEveryoneForGood()
    {
        var (r, feed, bob) = Room(2002);
        var nuke = r.In(() => Dm.@new<obj_machinery_nuclearbomb_syndicate_bananium>(locate(8, 4, 1)));
        r.In(() => nuke.nuke_effects(Dm.list(1)));
        r.Step(2 * Second);

        Assert.IsType<obj_item_clothing_under_rank_civilian_clown>(r.In(() => bob.w_uniform));
        Assert.IsType<obj_item_clothing_shoes_clown_shoes>(r.In(() => bob.shoes));
        Assert.IsType<obj_item_clothing_mask_gas_clown_hat>(r.In(() => bob.wear_mask));
        Assert.True(r.In(() => HAS_TRAIT(bob.wear_mask, TRAIT_NODROP)), "the mask is stuck on");
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_CLUMSY)));
        Assert.NotNull(r.In(() => bob.has_trauma_type(typeof(datum_brain_trauma_mild_phobia_clowns))));
        Assert.True(feed.Any("You start finding clowns very unnerving..."));
        var fear = Fear(r, bob);
        Assert.NotNull(fear);
        var phobia = r.In(() => Dm.each(fear.terror_handlers).OfType<datum_terror_handler_phobia_source>().Single());
        Assert.True(r.In(() => Dm.truthy(is_type_in_typecache(typeof(obj_item_clothing_mask_gas_clown_hat), phobia.trigger_objs))));
    }

    [Fact]
    public void AClownInSightFrightensThePhobic()
    {
        var (r, feed, bob) = Room(2003);
        r.In(() => Waiting.NoSleep(bob.gain_trauma(typeof(datum_brain_trauma_mild_phobia_clowns), TRAUMA_RESILIENCE_LOBOTOMY)));
        mob_living_carbon_human clown = r.Dummy(6, 4);
        r.In(() => Waiting.NoSleep(clown.equip_to_slot_or_del(Dm.@new<obj_item_clothing_mask_gas_clown_hat>(), ITEM_SLOT_MASK)));
        r.Step(10 * Second);
        Assert.True(feed.Any("Seeing "), "the clown frightens Bob: " + string.Join(" | ", feed.Chat.Select(c => c.Html).TakeLast(6)));
        Assert.True(r.In(() => Fear(r, bob).terror_buildup) > 0);
    }

    [Fact]
    public void TheWordHonkFrightensThePhobic()
    {
        var (r, feed, bob) = Room(2004);
        r.In(() => Waiting.NoSleep(bob.gain_trauma(typeof(datum_brain_trauma_mild_phobia_clowns), TRAUMA_RESILIENCE_LOBOTOMY)));
        mob_living_carbon_human carol = r.Join(5, 4, "Carol");
        r.Step(Second);
        r.In(() => Waiting.NoSleep(carol.say("Honk!")));
        r.Step(2 * Second);
        // the heard word is marked as the phobia; tg's timer calls freak_out(null, word) (phobia.dm:108), so its reason is null and the
        // message is the generic "Something ...!"
        Assert.True(feed.Any("class='phobia'>Honk</span>"), "the word is marked: " + string.Join(" | ", feed.Chat.Select(c => c.Html).TakeLast(4)));
        Assert.True(feed.Any("Something "), "the word frightens Bob: " + string.Join(" | ", feed.Chat.Select(c => c.Html).TakeLast(6)));
    }

    [Fact]
    public void PanicRacesTheHeartAndAHugCalms()
    {
        var (r, feed, bob) = Room(2005);
        r.In(() => Waiting.NoSleep(bob.gain_trauma(typeof(datum_brain_trauma_mild_phobia_clowns), TRAUMA_RESILIENCE_LOBOTOMY)));
        var fear = Fear(r, bob);
        r.In(() => fear.terror_buildup = TERROR_BUILDUP_PANIC + 10);
        r.Step(3 * Second);
        Assert.True(feed.Any("You feel your heart racing!"));
        Assert.True(r.In(() => Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_jitter)))), "the terror's jitters");

        mob_living_carbon_human carol = r.Dummy(4, 4);
        float before = r.In(() => fear.terror_buildup);
        r.In(() => fear.comfort_owner(bob, carol));
        Assert.True(r.In(() => fear.terror_buildup) < before, "the hug calms");
    }

    [Fact]
    public void LosingThePhobiaDropsItsFear()
    {
        var (r, _, bob) = Room(2006);
        var trauma = r.In(() => Waiting.NoSleep(bob.gain_trauma(typeof(datum_brain_trauma_mild_phobia_clowns), TRAUMA_RESILIENCE_LOBOTOMY)) as datum_brain_trauma);
        Assert.NotNull(Fear(r, bob));
        r.In(() => qdel(trauma));
        r.Step(2);
        Assert.Null(Fear(r, bob));
        r.AssertNoRuntimes();
    }
}
