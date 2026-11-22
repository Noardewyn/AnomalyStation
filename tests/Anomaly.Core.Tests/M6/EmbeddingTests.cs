using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using System.Text.RegularExpressions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M6;

/// <summary>
/// M6 slice 3b, embedding (datums/embedding.dm) in a running world: a thrown throwing star lodges itself in a limb, hurts,
/// makes it bleed and raises the embedded object's alert; a toy star only sticks; wirecutters pluck a star out into the
/// user's hand; ripping one out by hand takes its time and hurts; a shard falls out by itself; the self-check and the health
/// analyzer list the embedded object.
/// </summary>
public class EmbeddingTests
{
    private const int Second = 20;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        // tg's strings/ files (the scars' texts of the cuts a sharp embed makes), as the server mounts them
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root);
        r.In(() =>
        {
            for (int x = 2; x <= 11; x++)
            {
                for (int y = 2; y <= 8; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        r.Step(Second);
        return r;
    }

    private static datum_embedding Embed(obj_item item) => (datum_embedding)item.get_embed();

    /// Throws a new item of type T from the thrower's turf at the victim and waits for it to land.
    private static T Throw<T>(AudioRoom r, mob thrower, mob victim) where T : obj_item
    {
        T item = r.In(() =>
        {
            T made = Dm.@new<T>(thrower.loc);
            Waiting.Detach(made.throw_at(victim, 10, made.throw_speed, thrower));
            return made;
        });
        r.Step(Second);
        return item;
    }

    [Fact]
    public void AThrownThrowingStarEmbedsInALimbHurtsAndBleeds()
    {
        var r = Room(6601);
        mob_living_carbon_human thrower = r.Join(4, 5, "Thrower");
        mob_living_carbon_human victim = r.Dummy(8, 5);
        r.Step(Second);
        obj_item_throwing_star star = Throw<obj_item_throwing_star>(r, thrower, victim);
        r.In(() =>
        {
            datum_embedding embed = Embed(star);
            Assert.Same(victim, star.loc);
            Assert.Same(victim, embed.owner);
            obj_item_bodypart limb = embed.owner_limb;
            Assert.NotNull(limb);
            Assert.Contains(star, Dm.each_cast<obj_item>(limb.embedded_objects));
            Assert.True(Dm.truthy(victim.has_embedded_objects()));
            // throwforce 10 plus w_class 2 * impact_pain_mult 4 (embed_into), no armour on the dummy
            Assert.Equal(18f, Dm.num(limb.brute_dam), 3);
            Assert.True(Dm.num(limb.cached_bleed_rate) >= 0.25f, "an embedded harmful object makes the limb bleed");
            Assert.IsType<atom_movable_screen_alert_embeddedobject>(victim.alerts[ALERT_EMBEDDED_OBJECT]);
            Assert.True(Dm.truthy(GET_ATOM_BLOOD_DNA(star)), "it embedded itself in him, of course it's bloody");
            Assert.True(Dm.@in(embed, SSprocessing.processing), "the embed processes its pain and fall chance");
        });
        Assert.NotEmpty(r.Sounds.Of(thrower, "sound/items/weapons/bladeslice.ogg"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AToyStarOnlySticksAndDoesNoHarm()
    {
        var r = Room(6602);
        mob_living_carbon_human thrower = r.Join(4, 5, "Thrower");
        mob_living_carbon_human victim = r.Join(8, 5, "Target");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.Step(Second);
        obj_item_throwing_star_toy star = Throw<obj_item_throwing_star_toy>(r, thrower, victim);
        r.In(() =>
        {
            datum_embedding embed = Embed(star);
            Assert.Same(victim, embed.owner);
            Assert.True(Dm.truthy(embed.is_harmless()));
            Assert.Null(victim.alerts[ALERT_EMBEDDED_OBJECT]);
            Assert.False(Dm.truthy(victim.has_embedded_objects()), "a harmless embed is not counted");
            Assert.True(Dm.truthy(victim.has_embedded_objects(include_harmless: TRUE)));
            Assert.Equal(0f, Dm.num(embed.owner_limb.cached_bleed_rate));
        });
        Assert.Contains(chat.Html, h => h.Contains("sticks itself to your", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WirecuttersPluckTheStarIntoTheUsersHand()
    {
        var r = Room(6603);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human victim = r.Dummy(6, 5);
        r.Step(Second);
        (obj_item_throwing_star star, obj_item_wirecutters cutters, float brute) = r.In(() =>
        {
            var s = Dm.@new<obj_item_throwing_star>(victim.loc);
            Assert.Equal(TRUE, Waiting.NoSleep(s.force_embed(victim, BODY_ZONE_L_ARM)));
            Embed(s).pain_chance = 0; // no pain rolls while it is plucked, so the damage stays exact
            var c = Dm.@new<obj_item_wirecutters>(medic.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(medic.put_in_active_hand(c))));
            medic.zone_selected = BODY_ZONE_L_ARM;
            return (s, c, Dm.num(((obj_item_bodypart)victim.get_bodypart(BODY_ZONE_L_ARM)).brute_dam));
        });
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.Command(() => ClientSeam.Click(medic.client, victim, "left=1;button=left"));
        // rip_time 3 s * (w_class 2 * 0.3) * toolspeed 1, * 1.5 for the wirecutters (a safe pluck): 2.7 s
        r.Step(4 * Second);
        r.In(() =>
        {
            Assert.True(Embed(star).owner == null, string.Join("\n", chat.Html) + "\n" + string.Join("\n", r.Room.RuntimeText));
            Assert.Same(medic, star.loc);
            Assert.True(Dm.@in(star, medic.held_items), "the plucked star is put in the medic's hand");
            Assert.Equal(0f, LAZYLEN(((obj_item_bodypart)victim.get_bodypart(BODY_ZONE_L_ARM)).embedded_objects));
            Assert.Equal(brute, Dm.num(((obj_item_bodypart)victim.get_bodypart(BODY_ZONE_L_ARM)).brute_dam), 3); // wirecutters pluck safely
            Assert.Null(victim.alerts[ALERT_EMBEDDED_OBJECT]);
            Assert.False(Dm.@in(Embed(star), SSprocessing.processing));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void RippingAStarOutByHandTakesTimeHurtsAndPutsItInTheHand()
    {
        var r = Room(6604);
        mob_living_carbon_human victim = r.Join(6, 5, "Ripper");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.Step(Second);
        obj_item_throwing_star star = r.In(() =>
        {
            var s = Dm.@new<obj_item_throwing_star>(victim.loc);
            Assert.Equal(TRUE, Waiting.NoSleep(s.force_embed(victim, BODY_ZONE_R_LEG)));
            Embed(s).pain_chance = 0; // no pain rolls while it is ripped out, so the damage stays exact
            // the wound's second wind (determination, M8 slice B) heals the limb as it metabolizes: purged, so the damage stays exact
            Waiting.NoSleep(((datum_reagents)victim.reagents).del_reagent(typeof(datum_reagent_determination)));
            return s;
        });
        float brute = r.In(() => Dm.num(((obj_item_bodypart)victim.get_bodypart(BODY_ZONE_R_LEG)).brute_dam));
        // the self-check lists the star with its rip-out link; the click on the link is BYOND's Topic, the carbon's (D154)
        r.In(() => victim.check_self_for_injuries());
        string selfCheck = string.Join("\n", chat.Html);
        Assert.Contains("embedded in your right leg!", selfCheck, StringComparison.Ordinal);
        Match link = Regex.Match(selfCheck, "href='(byond://\\?[^']*embedded_object=[^']*)'");
        Assert.True(link.Success, selfCheck);
        Assert.Contains("embedded_object=" + r.In(() => REF(star)), link.Groups[1].Value, StringComparison.Ordinal);

        // a link naming a limb that is not his does nothing
        r.Command(() => ClientSeam.Topic(victim.client, link.Groups[1].Value.Replace("embedded_limb=", "embedded_limb=[0x1]&x=", StringComparison.Ordinal)));
        r.Step(2);
        Assert.DoesNotContain(chat.Html, h => h.Contains("You attempt to remove", StringComparison.Ordinal));
        r.Command(() => ClientSeam.Topic(victim.client, link.Groups[1].Value));
        r.Step(4 * Second); // rip_time 3 s * w_class 2 = 6 s
        r.In(() => Assert.Same(victim, star.loc));
        // read the damage on the tick the star comes out: the pierced wound it leaves gives a second wind (determination) that
        // heals the limb from the next Life on, whose phase depends on the room's subsystems
        for (int i = 0; i < 3 * Second && r.In(() => Embed(star).owner != null); i++)
            r.Step(1);
        r.In(() =>
        {
            Assert.True(Embed(star).owner == null, string.Join("\n", chat.Html) + "\n" + string.Join("\n", r.Room.RuntimeText));
            Assert.True(Dm.@in(star, victim.held_items), "the ripped out star is put in the hand");
            // w_class 2 * remove_pain_mult 6
            Assert.Equal(brute + 12f, Dm.num(((obj_item_bodypart)victim.get_bodypart(BODY_ZONE_R_LEG)).brute_dam), 3);
            Assert.Null(victim.alerts[ALERT_EMBEDDED_OBJECT]);
        });
        Assert.Contains(chat.Html, h => h.Contains("You successfully remove the throwing star from your right leg.", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmbeddedShardFallsOutByItselfAndHurts()
    {
        var r = Room(6605);
        mob_living_carbon_human victim = r.Dummy(6, 5);
        r.Step(Second);
        (obj_item_shard shard, float brute) = r.In(() =>
        {
            var s = Dm.@new<obj_item_shard>(victim.loc);
            Assert.Equal(TRUE, Waiting.NoSleep(s.force_embed(victim, BODY_ZONE_CHEST)));
            Embed(s).fall_chance = 100;
            Embed(s).pain_chance = 0;
            // the wound's second wind (determination, M8 slice B) heals the limb as it metabolizes: purged, so the damage stays exact
            Waiting.NoSleep(((datum_reagents)victim.reagents).del_reagent(typeof(datum_reagent_determination)));
            return (s, Dm.num(((obj_item_bodypart)victim.get_bodypart(BODY_ZONE_CHEST)).brute_dam));
        });
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Null(Embed(shard).owner);
            Assert.Same(victim.loc, shard.loc);
            // w_class 1 * remove_pain_mult 6
            Assert.Equal(brute + 6f, Dm.num(((obj_item_bodypart)victim.get_bodypart(BODY_ZONE_CHEST)).brute_dam), 3);
            Assert.False(Dm.truthy(victim.has_embedded_objects()));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHealthAnalyzerListsTheForeignObjects()
    {
        var r = Room(6606);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human victim = r.Dummy(6, 5);
        r.Step(Second);
        string report = r.In(() =>
        {
            for (int i = 0; i < 2; i++)
            {
                var s = Dm.@new<obj_item_throwing_star>(victim.loc);
                Assert.Equal(TRUE, Waiting.NoSleep(s.force_embed(victim, BODY_ZONE_L_LEG)));
            }
            return Dm.str(healthscan(medic, victim, SCANNER_VERBOSE, SCANPOWER_BASIC, tochat: FALSE));
        });
        Assert.Contains("Foreign object(s): 2x Throwing star", report, StringComparison.Ordinal);
        r.AssertNoRuntimes();
    }
}
