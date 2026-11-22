using System.IO;
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
/// Syndicate package A3 (D316): Dynamic (controllers/subsystem/dynamic/) in a booted toy room: the config read from tg's dynamic.toml; the
/// tier picked by tg's weights and the population; the roundstart traitors taken only from those with the preference, in tg's numbers;
/// a latejoin traitor at tg's chance; a midround traitor after its start time and its cooldown; no inert ruleset ever picked (DV346);
/// force_extended picking none; the antagonist's preference picture (tg's flat outfit, scaled, shifted and cropped).
/// </summary>
public class DynamicTests
{
    private static Feed _feed;

    private static (AudioRoom R, mob_living_carbon_human[] Players) Room(ulong seed, int players)
    {
        var r = new AudioRoom(15, 9, seed: seed);
        _feed = Listen(r);
        var joined = new mob_living_carbon_human[players];
        for (int i = 0; i < players; i++)
            joined[i] = r.Join(2 + i, 4, "P" + (i + 1));
        r.Step(Second);
        return (r, joined);
    }

    private static void Prefer(AudioRoom r, mob who, params string[] roles)
        => r.In(() => { foreach (string role in roles) Dm.append(ref who.client.prefs.be_special, role); });

    private static void Crew(AudioRoom r, mob who) => r.In(() => who.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_assistant)) as datum_job));

    private static bool IsTraitor(AudioRoom r, mob who) => r.In(() => Dm.truthy(who.mind.has_antag_datum(typeof(datum_antagonist_traitor))));

    [Fact]
    public void The_rulesets_of_ported_antagonists_are_live_and_the_rest_inert()
    {
        foreach (Type type in new[]
        {
            typeof(datum_controller_subsystem_dynamic), typeof(datum_dynamic_tier_lowmedium), typeof(datum_dynamic_ruleset_roundstart_traitor),
            typeof(datum_dynamic_ruleset_roundstart_extended), typeof(datum_dynamic_ruleset_latejoin_traitor), typeof(datum_dynamic_ruleset_midround_from_living_traitor),
            typeof(datum_dynamic_ruleset_midround_from_living_traitor_mass), typeof(datum_dynamic_ruleset_midround_from_ghosts), typeof(datum_dynamic_panel), typeof(datum_dynamic_tester),
            typeof(datum_dynamic_ruleset_roundstart_blood_brother), typeof(datum_dynamic_ruleset_roundstart_nukies), typeof(datum_dynamic_ruleset_roundstart_nukies_clown),
            typeof(datum_dynamic_ruleset_roundstart_revolution), typeof(datum_dynamic_ruleset_latejoin_revolution), // gaps V1
            typeof(datum_dynamic_ruleset_roundstart_changeling), typeof(datum_dynamic_ruleset_latejoin_changeling), // gaps K1
            typeof(datum_dynamic_ruleset_midround_from_ghosts_space_changeling), typeof(datum_dynamic_ruleset_midround_from_ghosts_space_changeling_mass),
            typeof(datum_dynamic_ruleset_midround_from_ghosts_xenomorph),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        foreach (Type type in new[]
        {
            typeof(datum_dynamic_ruleset_roundstart_heretic),
            typeof(datum_dynamic_ruleset_roundstart_meteor), typeof(datum_dynamic_ruleset_midround_from_ghosts_wizard),
        })
            Assert.True(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is live");
    }

    [Fact]
    public void The_config_is_read_from_tgs_dynamic_toml()
    {
        var (r, _) = Room(3101, 1);
        string repoToml = FindUp("config/dynamic.toml");
        r.In(() =>
        {
            ((datum_config_entry)config.entries_by_type[typeof(datum_config_entry_flag_dynamic_config_enabled)]).config_entry_value = TRUE;
            // tg's own file parses whole: every tier and ruleset table
            DmWorldState.Of(r.World).Files.WriteText("config/dynamic.toml", File.ReadAllText(repoToml));
            SSdynamic.load_config();
            DmList full = SSdynamic.get_config() as DmList;
            Assert.Equal(50, full.len);
            Assert.Equal(46f, Dm.num((full["Low-Medium Chaos"] as DmList)["weight"]));
            Assert.Equal(7f, Dm.num((full["Mass Traitors"] as DmList)["repeatable_weight_decrease"]));

            // an edited file: the tier's settings and a ruleset's tier weights, minimum population and antagonist cap come from it
            DmWorldState.Of(r.World).Files.WriteText("config/dynamic.toml",
                "[\"Low Chaos\"]\nweight = 99\nruleset_type_settings.light_midround.time_threshold = 7 # minutes\n\n"
                + "[\"Roundstart Traitor\"]\nweight.2 = 4\nweight.4 = 9\nmin_pop = 0\nmax_antag_cap = 3\nconfig_tag = \"locked\"\n");
            SSdynamic.dynamic_config = Dm.list();
            Assert.Equal(99f, Dm.num(SSdynamic.get_config_value(typeof(datum_dynamic_tier_low), "weight", 8)));
            var low = Dm.@new<datum_dynamic_tier_low>(SSdynamic.get_config());
            Assert.Equal(7f * MINUTES, Dm.num((low.ruleset_type_settings[LIGHT_MIDROUND] as DmList)[TIME_THRESHOLD]));
            var traitor = Dm.@new<datum_dynamic_ruleset_roundstart_traitor>(SSdynamic.get_config());
            Assert.Equal("Roundstart Traitor", traitor.config_tag); // tg locks config_tag
            Assert.Equal(3f, Dm.num(traitor.max_antag_cap));
            Assert.Equal(4f, Dm.num(traitor.get_weight(5, DYNAMIC_TIER_LOWMEDIUM)));
            Assert.Equal(9f, Dm.num(traitor.get_weight(5, DYNAMIC_TIER_MEDIUMHIGH))); // a tier with no weight takes the next higher one
            Assert.Equal(4f, Dm.num(traitor.get_weight(5, DYNAMIC_TIER_LOW)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_tier_is_picked_by_tgs_weights_and_the_population()
    {
        var (r, _) = Room(3102, 1);
        r.In(() =>
        {
            var picks = new Dictionary<float, int>();
            for (int i = 0; i < 600; i++)
            {
                SSdynamic.pick_tier(10);
                float tier = SSdynamic.current_tier.tier.Value;
                picks[tier] = picks.GetValueOrDefault(tier) + 1;
                // 10 players are under every half range threshold (25): the roundstart high end is a quarter, never under the low end
                Assert.InRange(Dm.num(SSdynamic.rulesets_to_spawn[ROUNDSTART]), Dm.num(((SSdynamic.current_tier.ruleset_type_settings[ROUNDSTART]) as DmList)[LOW_END]),
                    Math.Max(Dm.num(((SSdynamic.current_tier.ruleset_type_settings[ROUNDSTART]) as DmList)[LOW_END]), MathF.Ceiling(Dm.num(((SSdynamic.current_tier.ruleset_type_settings[ROUNDSTART]) as DmList)[HIGH_END]) * 0.25f)));
            }
            // High Chaos needs 25 players; the others weigh 2, 8, 46 and 36
            Assert.False(picks.ContainsKey(DYNAMIC_TIER_HIGH));
            Assert.InRange(picks[DYNAMIC_TIER_GREEN], 3, 30);
            Assert.InRange(picks[DYNAMIC_TIER_LOW], 30, 80);
            Assert.InRange(picks[DYNAMIC_TIER_LOWMEDIUM], 260, 340);
            Assert.InRange(picks[DYNAMIC_TIER_MEDIUMHIGH], 190, 280);

            bool high = false;
            for (int i = 0; i < 200 && !high; i++)
            {
                SSdynamic.pick_tier(30);
                high = Dm.eq(SSdynamic.current_tier.tier, DYNAMIC_TIER_HIGH);
            }
            Assert.True(high, "30 players never got High Chaos");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_roundstart_traitors_come_only_from_those_with_the_preference_in_tgs_numbers()
    {
        var (r, p) = Room(3103, 4);
        Prefer(r, p[0], ROLE_TRAITOR);
        Prefer(r, p[1], ROLE_TRAITOR);
        Prefer(r, p[2], ROLE_SYNDICATE_INFILTRATOR, ROLE_SLEEPER_AGENT);
        datum_dynamic_ruleset_roundstart_traitor ruleset = r.In(() =>
        {
            SSdynamic.set_tier(typeof(datum_dynamic_tier_lowmedium), 4);
            var made = Dm.@new<datum_dynamic_ruleset_roundstart_traitor>(SSdynamic.get_config());
            // tg's numbers: weight 10 from 3 players, one traitor per 24 players (rounded up)
            Assert.Equal(0f, Dm.num(made.get_weight(2, DYNAMIC_TIER_LOWMEDIUM)));
            Assert.Equal(10f, Dm.num(made.get_weight(4, DYNAMIC_TIER_LOWMEDIUM)));
            Assert.Equal(1f, Dm.num(made.get_antag_cap(4, made.max_antag_cap)));
            Assert.Equal(1f, Dm.num(made.get_antag_cap(24, made.max_antag_cap)));
            Assert.Equal(2f, Dm.num(made.get_antag_cap(25, made.max_antag_cap)));
            Assert.Equal(3f, Dm.num(made.get_antag_cap(49, made.max_antag_cap)));
            return made;
        });
        DmList candidates = r.In(() => Dm.list(p[0], p[1], p[2], p[3]));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(ruleset.prepare_execution(4, candidates)))));
        datum_mind chosen = r.In(() => Assert.Single(Dm.each_cast<datum_mind>(ruleset.selected_minds).ToList()));
        Assert.Contains(chosen, new[] { p[0].mind, p[1].mind });
        Assert.Equal(3, r.In(() => candidates.len));
        // the role blacklist goes to the job assignment: no traitor AI or cyborg
        Assert.True(r.In(() => Dm.@in(JOB_AI, LAZYACCESS(SSjob.prevented_occupations, chosen) as DmList)));

        r.In(() => Waiting.NoSleep(ruleset.execute()));
        r.Step(4);
        Assert.True(r.In(() => Dm.truthy(chosen.has_antag_datum(typeof(datum_antagonist_traitor)))));
        Assert.False(IsTraitor(r, p[2]) || IsTraitor(r, p[3]));
        Assert.True(_feed.Any("You are the Traitor."), string.Join(" | ", _feed.Chat.Select(c => c.Html))); // tg's greeting (the traitor's own procs are A2's)

        // nobody with the preference: the ruleset fails with tg's reason
        var none = r.In(() => Dm.@new<datum_dynamic_ruleset_roundstart_traitor>(SSdynamic.get_config()));
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(none.prepare_execution(4, Dm.list(p[2], p[3]))))));
        Assert.Equal("Reason: Not enough eligible candidates. Have: 0, Need: 1", r.In(() => none.log_data));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_latejoin_traitor_comes_at_tgs_chance()
    {
        var (r, p) = Room(3104, 4);
        foreach (mob who in p)
            Crew(r, who);
        Prefer(r, p[3], ROLE_SYNDICATE_INFILTRATOR);
        r.In(() =>
        {
            SSdynamic.set_tier(typeof(datum_dynamic_tier_low), 4);
            SSdynamic.rulesets_to_spawn[LATEJOIN] = 1;
            SSdynamic.light_ruleset_start = world.time + 30 * MINUTES;
            // 100 - 200 * dead / (alive + dead), a fifth before the light midrounds start, 15 more per failed latejoin
            Assert.Equal(20f, Dm.num(SSdynamic.get_latejoin_chance()));
            SSdynamic.failed_latejoins = 1;
            Assert.Equal(23f, Dm.num(SSdynamic.get_latejoin_chance()));
            SSdynamic.failed_latejoins = 0;
            SSdynamic.light_ruleset_start = 0;
            Assert.Equal(100f, Dm.num(SSdynamic.get_latejoin_chance()));
        });
        // a latejoiner without the preference: the pick fails and raises the next chance
        r.In(() => Waiting.NoSleep(SSdynamic.on_latejoin(p[2])));
        Assert.False(IsTraitor(r, p[2]));
        Assert.Equal(1f, r.In(() => SSdynamic.failed_latejoins));
        // with it: the latejoin traitor, the only live latejoin ruleset
        r.In(() => Waiting.NoSleep(SSdynamic.on_latejoin(p[3])));
        r.Step(4);
        Assert.True(IsTraitor(r, p[3]));
        r.In(() =>
        {
            Assert.Equal(0f, Dm.num(SSdynamic.rulesets_to_spawn[LATEJOIN]));
            Assert.Equal(0f, SSdynamic.failed_latejoins);
            Assert.InRange(SSdynamic.latejoin_cooldown - world.time, 10 * MINUTES, 20 * MINUTES);
            Assert.IsType<datum_dynamic_ruleset_latejoin_traitor>(Assert.Single(Dm.each(SSdynamic.executed_rulesets).ToList()));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_midround_traitor_comes_after_its_start_and_its_cooldown()
    {
        var (r, p) = Room(3105, 4);
        foreach (mob who in p)
            Crew(r, who);
        Prefer(r, p[1], ROLE_SLEEPER_AGENT);
        r.In(() =>
        {
            SSdynamic.set_tier(typeof(datum_dynamic_tier_lowmedium), 4);
            SSdynamic.rulesets_to_spawn[LIGHT_MIDROUND] = 2;
            SSdynamic.rulesets_to_spawn[HEAVY_MIDROUND] = 0;
            SSdynamic.light_ruleset_start = world.time + 10 * SECONDS;
            SSdynamic.midround_cooldown = 0;
            Waiting.NoSleep(SSdynamic.fire());
        });
        Assert.False(IsTraitor(r, p[1])); // the light midrounds have not started
        r.Step(10 * Second);
        r.In(() => Waiting.NoSleep(SSdynamic.fire()));
        r.Step(4);
        Assert.True(IsTraitor(r, p[1]));
        r.In(() =>
        {
            Assert.Equal(1f, Dm.num(SSdynamic.rulesets_to_spawn[LIGHT_MIDROUND]));
            Assert.InRange(SSdynamic.midround_cooldown - world.time, 10 * MINUTES, 20 * MINUTES);
        });
        // the cooldown holds the next one back
        Prefer(r, p[2], ROLE_SLEEPER_AGENT);
        r.In(() => Waiting.NoSleep(SSdynamic.fire()));
        Assert.False(IsTraitor(r, p[2]));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_admin_who_forces_a_midround_picks_its_antagonist_from_the_valid_candidates()
    {
        // DV437: the Dynamic Admin Panel's Execute asks the admin whom, from the ruleset's valid candidates, or Random (tg's pick)
        var (r, p) = Room(3106, 4);
        foreach (mob who in p)
            Crew(r, who);
        foreach (mob who in p[1..])
            Prefer(r, who, ROLE_SLEEPER_AGENT);
        mob admin = p[0]; // no preference: not a candidate
        r.In(() => Dm.@new<datum_admins>(Dm.list(Dm.@new<datum_admin_rank>("!localhost!", RANK_SOURCE_LOCAL, R_EVERYTHING, R_DBRANKS, R_EVERYTHING)), admin.ckey, 1, 1));
        Assert.True(r.In(() => admin.client.holder != null), "the admin's rights");
        r.In(() => Waiting.Detach(SSdynamic.force_run_midround(typeof(datum_dynamic_ruleset_midround_from_living_traitor), alert_admins_on_fail: TRUE, admin: admin)));
        r.Step(4);
        datum_tgui ui = r.In(() => Dm.each_cast<datum_tgui>(admin.tgui_open_uis).Last(u => Dm.str(u.@interface) == "ListInputWindow"));
        string[] choices = r.In(() => Dm.each(((datum_tgui_list_input)ui.src_object).items).Select(Dm.str).ToArray());
        string third = r.In(() => Dm.text("[] ([])", p[3].name, p[3].ckey));
        Assert.Equal(4, choices.Length);
        Assert.Contains("Random", choices);
        Assert.Contains(third, choices);
        Assert.DoesNotContain(r.In(() => Dm.text("[] ([])", admin.name, admin.ckey)), choices);
        string window = r.In(() => Dm.str(ui.window.id));
        r.Command(() => ClientSeam.TguiMessage(admin.client, window, "act/submit", Dm.list(Dm.kv("entry", third))));
        r.Step(4);
        Assert.True(IsTraitor(r, p[3]));
        Assert.False(IsTraitor(r, p[1]) || IsTraitor(r, p[2]) || IsTraitor(r, admin));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_inert_ruleset_is_never_picked_and_force_extended_picks_none()
    {
        var (r, _) = Room(3106, 1);
        r.In(() =>
        {
            SSdynamic.set_tier(typeof(datum_dynamic_tier_high), 60);
            // tg weighs the roundstart heretics 3 at the highest tier; inert here (DV346; the changelings', the example until gaps K1, are live)
            var heretic = Dm.@new<datum_dynamic_ruleset_roundstart_heretic>(SSdynamic.get_config());
            Assert.Equal(0f, Dm.num(heretic.get_weight(60, DYNAMIC_TIER_HIGH)));

            DmList sixty = Dm.list();
            for (int i = 0; i < 60; i++)
                Dm.append(ref sixty, i);
            SSdynamic.rulesets_to_spawn[ROUNDSTART] = 4;
            DmList picked = SSdynamic.pick_roundstart_rulesets(sixty) as DmList;
            Assert.Equal(4, picked.len);
            Assert.All(Dm.each(picked), t => Assert.Contains(t, new object[] { typeof(datum_dynamic_ruleset_roundstart_traitor), typeof(datum_dynamic_ruleset_roundstart_blood_brother), typeof(datum_dynamic_ruleset_roundstart_nukies),
                typeof(datum_dynamic_ruleset_roundstart_malf_ai), typeof(datum_dynamic_ruleset_roundstart_revolution), typeof(datum_dynamic_ruleset_roundstart_changeling) })); // the malf AI's since silicons SI8, the revolution's since gaps V1, the changelings' since K1

            Waiting.NoSleep(SSdynamic.queue_ruleset(typeof(datum_dynamic_ruleset_latejoin_traitor)));
            SSdynamic.force_extended();
            Assert.All(Dm.each(SSdynamic.rulesets_to_spawn), category => Assert.Equal(0f, Dm.num(SSdynamic.rulesets_to_spawn[category])));
            Assert.Equal(0, SSdynamic.queued_rulesets.len);
            Assert.Equal(0, (SSdynamic.pick_roundstart_rulesets(sixty) as DmList).len);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_antagonists_preference_picture_is_its_flat_outfit_scaled_shifted_and_cropped()
    {
        var (r, _) = Room(3107, 1);
        icon picture = r.In(() =>
        {
            var antag = Dm.@new<datum_antagonist_custom>();
            antag.preview_outfit = typeof(datum_outfit_job_assistant);
            return Waiting.NoSleep(antag.get_preview_icon()) as icon;
        });
        r.Step(Second);
        Assert.Equal("nothing", picture.dm_state);
        string recipe = picture.dm_resource();
        Assert.StartsWith("iconops:icons/blanks/32x32.dmi|nothing|0|", recipe);
        Assert.True(recipe.Contains("bodyparts_greyscale.dmi,human_chest_m", StringComparison.Ordinal), recipe); // the body's parts, blended over the blank
        Assert.Contains("greyscale_config/jumpsuit/worn", recipe, StringComparison.Ordinal); // the assistant's jumpsuit
        Assert.Contains("back/backpack.dmi,backpack", recipe, StringComparison.Ordinal);
        string[] ops = recipe.Split('|');
        Assert.Equal(new[] { "103,1,1,s,115,115", "104,1,1,t,8,8,0", "104,1,1,t,2,30,0", "102,1,1,k,96,96" }, ops[^4..]);
    }

    private static string FindUp(string relative)
    {
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, relative);
            if (File.Exists(path))
                return path;
        }
        throw new FileNotFoundException(relative);
    }
}
