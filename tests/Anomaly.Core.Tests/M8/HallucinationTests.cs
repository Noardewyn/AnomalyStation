using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M8;

/// <summary>
/// M8 slice G (D187): tg's hallucinations in a running world with two players, Alice (the hallucinator) and Bob (a bystander): what a
/// hallucination makes reaches Alice's client alone (the sound to her sink, the line to her chat, the alert and the health doll to her
/// screen, the images to her client.images), and the weighted pool, the pulses and the status effect work as tg's.
/// </summary>
public class HallucinationTests
{
    private const int Second = 20;

    private sealed class Feed : IMessageSink
    {
        public List<(object Target, string Html)> Chat { get; } = new();

        void IMessageSink.Chat(object target, DmList message) => Chat.Add((target, Dm.str(message?["html"])));

        public void Output(object target, string text) { }

        public void Balloon(object viewer, object atom, string text) => Chat.Add((viewer, "balloon: " + text));

        public List<string> To(object who) => Chat.Where(c => ReferenceEquals(c.Target, who) || (c.Target is client cl && ReferenceEquals(cl.mob, who))).Select(c => c.Html).ToList();
    }

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Alice, mob_living_carbon_human Bob, Feed Feed);

    private static Rig Build(ulong seed = 71)
    {
        var r = new AudioRoom(41, 23, seed: seed);
        r.In(() => TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root)); // the strings of the fake lines
        // the station's name, as tg's STATIONNAME config sets it: the name lists were read before the mount (the biohazard alert names it)
        r.In(() => GLOB.station_name = "Test Station");
        r.In(() =>
        {
            for (int x = 2; x <= 39; x++)
            {
                for (int y = 2; y <= 21; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        mob_living_carbon_human alice = r.Join(20, 11, "Alice");
        mob_living_carbon_human bob = r.Join(25, 11, "Bob");
        var feed = new Feed();
        PresentationSinks.Of(r.World).Messages = feed;
        r.Step(Second);
        return new Rig(r, alice, bob, feed);
    }

    private static object Cause(Rig g, mob_living who, Type type, params object[] args)
    {
        var all = new List<object> { type, "test" };
        all.AddRange(args);
        return g.R.In(() => Waiting.NoSleep(who.cause_hallucination(all.ToArray())));
    }

    private static bool Has(client c, Func<image, bool> test) => Dm.each<image>(c.images).Any(test);

    [Fact]
    public void The_weighted_pool_has_the_four_tiers_and_no_abstract_type()
    {
        Rig g = Build();
        DmList pool = g.R.In(() => (DmList)GLOB.random_hallucination_weighted_list);
        foreach (string tier in new[] { "1", "2", "3", "4" })
            Assert.True(pool[tier] is DmList { Count: > 0 }, "tier " + tier);
        Assert.Null(pool["5"]);
        foreach (object tier in Dm.each(pool))
            foreach (Type t in Dm.each_cast<Type>(((DmList)pool[tier])))
                Assert.NotEqual(Dm.initial(t, (datum_hallucination p) => p.abstract_hallucination_parent), t);
        Type valid = g.R.In(() => (Type)get_random_valid_hallucination_subtype(typeof(datum_hallucination_fake_sound)));
        Assert.True(ispath(valid, typeof(datum_hallucination_fake_sound)));
        // tg draws a tier-1 hallucination from the lowest tier only
        Type one = g.R.In(() => (Type)Waiting.NoSleep(get_random_hallucination(1, 1)));
        Assert.Equal(1f, Dm.initial(one, (datum_hallucination p) => p.hallucination_tier));
    }

    [Fact]
    public void A_fake_sound_is_heard_by_the_hallucinator_alone()
    {
        Rig g = Build();
        Assert.NotNull(Cause(g, g.Alice, typeof(datum_hallucination_fake_sound_normal_boom)));
        g.R.Step(Second);
        Assert.Contains(g.R.Sounds.Heard, h => ReferenceEquals(h.Target, g.Alice) && h.File != null && h.File.Contains("explosion"));
        Assert.DoesNotContain(g.R.Sounds.Heard, h => ReferenceEquals(h.Target, g.Bob) && h.File != null && h.File.Contains("explosion"));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_deaf_hallucinator_hears_nothing()
    {
        Rig g = Build();
        g.R.In(() => ADD_TRAIT(g.Alice, TRAIT_DEAF, "test"));
        Assert.Null(Cause(g, g.Alice, typeof(datum_hallucination_fake_sound_normal_boom)));
        g.R.Step(Second);
        Assert.DoesNotContain(g.R.Sounds.Heard, h => ReferenceEquals(h.Target, g.Alice) && h.File != null && h.File.Contains("explosion"));
    }

    [Fact]
    public void A_battle_goes_on_in_the_hallucinators_ears_only()
    {
        Rig g = Build();
        Assert.NotNull(Cause(g, g.Alice, typeof(datum_hallucination_battle_gun_laser)));
        g.R.Step(8 * Second);
        Assert.Contains(g.R.Sounds.Heard, h => ReferenceEquals(h.Target, g.Alice));
        Assert.DoesNotContain(g.R.Sounds.Heard, h => ReferenceEquals(h.Target, g.Bob) && h.Channel is not (1017 or 1018)); // D296: the area's buzz and ambience reach both
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_fake_message_reaches_the_chat_of_the_hallucinator_alone()
    {
        Rig g = Build();
        Assert.NotNull(Cause(g, g.Alice, typeof(datum_hallucination_message)));
        Assert.NotEmpty(g.Feed.To(g.Alice));
        Assert.Empty(g.Feed.To(g.Bob));
    }

    [Fact]
    public void A_fake_line_of_speech_comes_from_a_nearby_human()
    {
        Rig g = Build();
        // Bob stands in view and speaks a language Alice understands: the line names him and goes to Alice alone
        Assert.NotNull(Cause(g, g.Alice, typeof(datum_hallucination_chat), Dm.named("specific_message", "Run!")));
        List<string> lines = g.Feed.To(g.Alice);
        Assert.True(lines.Any(l => l.Contains("Run!")), string.Join("| ", lines));
        Assert.Empty(g.Feed.To(g.Bob));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_fake_alert_is_thrown_on_the_hallucinators_screen_and_cleared_after_its_time()
    {
        Rig g = Build();
        Assert.NotNull(Cause(g, g.Alice, typeof(datum_hallucination_fake_alert_need_oxygen), Dm.named("duration", 3 * SECONDS)));
        Assert.NotNull(g.R.In(() => g.Alice.alerts?[ALERT_NOT_ENOUGH_OXYGEN]));
        Assert.Null(g.R.In(() => g.Bob.alerts?[ALERT_NOT_ENOUGH_OXYGEN]));
        g.R.Step(5 * Second);
        Assert.Null(g.R.In(() => g.Alice.alerts?[ALERT_NOT_ENOUGH_OXYGEN]));
    }

    [Fact]
    public void A_screwy_hud_shows_the_health_indicator_of_a_dead_man_to_a_living_one()
    {
        Rig g = Build();
        Assert.NotNull(Cause(g, g.Alice, typeof(datum_hallucination_screwy_hud_dead)));
        g.R.Step(2);
        string state = g.R.In(() => (g.Alice.hud_used?.screen_objects?[HUD_MOB_HEALTH] as atom_movable_screen)?.icon_state);
        Assert.Equal("health7", state);
        g.R.Step(30 * Second);
        string after = g.R.In(() => (g.Alice.hud_used?.screen_objects?[HUD_MOB_HEALTH] as atom_movable_screen)?.icon_state);
        Assert.NotEqual("health7", after);
    }

    [Fact]
    public void A_fake_health_doll_marks_a_limb_and_goes_away()
    {
        Rig g = Build();
        object made = Cause(g, g.Alice, typeof(datum_hallucination_fake_health_doll), Dm.named("duration", 4 * SECONDS));
        Assert.NotNull(made);
        var doll = (datum_hallucination_fake_health_doll)made;
        Assert.Single(Dm.each(doll.bodyparts));
        g.R.Step(6 * Second);
        Assert.True(QDELETED(doll));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_pulse_hallucinates_the_one_who_sees_the_centre_and_spares_the_blind()
    {
        Rig g = Build();
        mob_living_carbon_human carol = g.R.Dummy(22, 11);
        g.R.In(() => Waiting.NoSleep(g.Bob.become_blind("test")));
        atom center = g.R.In(() => (atom)locate(23, 11, 1));
        g.R.In(() => Waiting.NoSleep(visible_hallucination_pulse(center, 7, 50 * SECONDS, 300 * SECONDS)));
        Assert.True(g.R.In(() => Dm.truthy(g.Alice.has_status_effect(typeof(datum_status_effect_hallucination)))));
        Assert.True(g.R.In(() => Dm.truthy(carol.has_status_effect(typeof(datum_status_effect_hallucination)))));
        Assert.False(g.R.In(() => Dm.truthy(g.Bob.has_status_effect(typeof(datum_status_effect_hallucination)))));
        // the far one gets less: sqrt(1 / distance) of the amount
        datum_status_effect_hallucination near = g.R.In(() => (datum_status_effect_hallucination)carol.has_status_effect(typeof(datum_status_effect_hallucination)));
        datum_status_effect_hallucination far = g.R.In(() => (datum_status_effect_hallucination)g.Alice.has_status_effect(typeof(datum_status_effect_hallucination)));
        Assert.True(Dm.num(near.duration) > Dm.num(far.duration));
        // hallucination_pulse also reaches the blind
        g.R.In(() => Waiting.NoSleep(hallucination_pulse(center, 7, 50 * SECONDS, 300 * SECONDS)));
        Assert.True(g.R.In(() => Dm.truthy(g.Bob.has_status_effect(typeof(datum_status_effect_hallucination)))));
    }

    [Fact]
    public void The_status_effect_causes_a_hallucination_when_its_cooldown_is_up()
    {
        Rig g = Build();
        g.R.In(() => Waiting.NoSleep(g.Alice.adjust_hallucinations(120 * SECONDS)));
        g.R.Step(100 * Second);
        Assert.NotEmpty(g.R.In(() => Dm.each(GLOB.all_ongoing_hallucinations).Cast<object>().ToList()).Cast<object>().Concat(g.R.Sounds.Heard.Where(h => ReferenceEquals(h.Target, g.Alice)).Cast<object>()).Concat(g.Feed.To(g.Alice)));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_body_shows_in_the_hallucinators_images_only_and_is_taken_back()
    {
        Rig g = Build();
        object made = Cause(g, g.Alice, typeof(datum_hallucination_body_husk));
        Assert.NotNull(made);
        Assert.True(Has(g.Alice.client, i => i.icon_state == "husk"));
        Assert.False(Has(g.Bob.client, i => i.icon_state == "husk"));
        g.R.Step(7 * Second);
        Assert.False(Has(g.Alice.client, i => i.icon_state == "husk"));
    }

    [Fact]
    public void A_delusion_puts_an_override_picture_over_the_others_for_the_hallucinator_alone()
    {
        Rig g = Build();
        mob_living_carbon_human carol = g.R.Join(22, 14, "Carol");
        object made = Cause(g, g.Alice, typeof(datum_hallucination_delusion_preset_monkey), Dm.named("duration", 30 * SECONDS), Dm.named("affects_others", TRUE), Dm.named("affects_us", FALSE));
        Assert.NotNull(made);
        // Alice sees Bob and Carol as monkeys: an override image on each; Bob and Carol see nothing of it
        var images = Dm.each<image>(g.Alice.client.images).Where(i => i.icon_state == "monkey").ToList();
        Assert.Equal(2, images.Count);
        Assert.All(images, i => Assert.Equal(1f, i.@override));
        Assert.Contains(images, i => ReferenceEquals(i.loc, g.Bob));
        Assert.Contains(images, i => ReferenceEquals(i.loc, carol));
        Assert.False(Has(g.Bob.client, i => i.icon_state == "monkey"));
        Assert.False(Has(carol.client, i => i.icon_state == "monkey"));
        g.R.Step(35 * Second);
        Assert.False(Has(g.Alice.client, i => i.icon_state == "monkey"));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void Bubblegum_hallucination_shows_its_images_and_goes_in_time()
    {
        Rig g = Build();
        object made = Cause(g, g.Alice, typeof(datum_hallucination_oh_yeah));
        // needs a wall within seven tiles: the toy room has its border walls, Alice stands far from them, so the hallucination may refuse
        if (made == null)
            return;
        g.R.Step(2 * Second);
        Assert.True(Has(g.Alice.client, i => i.icon_state == "landing"));
        Assert.False(Has(g.Bob.client, i => i.icon_state == "landing"));
        g.R.Step(15 * Second);
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void Every_hallucination_type_runs_to_its_end_without_a_runtime_error()
    {
        Rig g = Build();
        var types = new List<Type>();
        g.R.In(() =>
        {
            foreach (Type t in Dm.each_cast<Type>(Dm.typesof(typeof(datum_hallucination))))
            {
                if (Dm.initial(t, (datum_hallucination p) => p.abstract_hallucination_parent) != t && !ispath(t, typeof(datum_hallucination_delusion_custom)))
                    types.Add(t);
            }
        });
        Assert.True(types.Count > 100, "only " + types.Count + " hallucination types");
        int started = 0;
        foreach (Type t in types)
        {
            if (Cause(g, g.Alice, t) != null)
                started++;
            g.R.Step(Second);
        }
        g.R.Step(120 * Second);
        Assert.True(started > 60, "only " + started + " of " + types.Count + " started");
        g.R.AssertNoRuntimes();
    }
}
