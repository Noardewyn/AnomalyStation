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
/// Syndicate package A1 (D316): the antagonist core (antag_datum.dm, antag_team.dm, antag_hud.dm, mind/antag.dm, objective.dm,
/// objective_items.dm, roundend.dm's antagonist report) in a booted toy room: a mind gains a custom antagonist with an assassinate
/// objective on a live target, a steal of a live item and an escape, whose completions turn as tg's do; the round-end report lists it
/// with tg's green and red text; the team HUD shows its icon only to its teammates and the combo HUD to an admin's view; the admin's
/// add and remove leave nothing behind.
/// </summary>
public class AntagonistCoreTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, mob_living_carbon_human Alice) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        mob_living_carbon_human alice = r.Join(6, 4, "Alice");
        r.Step(Second);
        return (r, feed, bob, alice);
    }

    private static datum_antagonist_custom MakeAntag(AudioRoom r, mob who, string hud = null)
    {
        datum_antagonist_custom antag = r.In(() =>
        {
            datum_antagonist_custom made = Dm.@new<datum_antagonist_custom>();
            made.antag_hud_name = hud;
            return Waiting.NoSleep(who.mind.add_antag_datum(made)) as datum_antagonist_custom;
        });
        r.Step(4);
        Assert.NotNull(antag);
        return antag;
    }

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_antagonist), typeof(datum_antagonist_custom), typeof(datum_team), typeof(datum_action_antag_info), typeof(datum_objective),
            typeof(datum_objective_assassinate), typeof(datum_objective_steal), typeof(datum_objective_escape), typeof(datum_objective_custom),
            typeof(datum_objective_item_steal_traitor_fireaxe), typeof(datum_atom_hud_alternate_appearance_basic_has_antagonist),
            typeof(datum_atom_hud_alternate_appearance_basic_antagonist_hud),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void ACustomAntagonistGetsTheGreetingTheButtonAndObjectivesThatCompleteAsTgSays()
    {
        var (r, feed, bob, alice) = Room(7101);
        datum_antagonist_custom antag = MakeAntag(r, bob);

        // on_gain: the greeting, the panel's line, the info button that opens AntagInfoGeneric at once, the living antagonists' list
        Assert.True(feed.Any("You are the Antagonist."), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.True(feed.Any("For more info, read the panel."));
        Assert.Contains(r.In(() => Take(bob)), o => o.Json != null && o.Json.Contains("AntagInfoGeneric"));
        Assert.True(r.In(() => Dm.@in(antag, bob.mind.antag_datums)));
        Assert.True(r.In(() => Dm.@in(antag, GLOB.antagonists)));
        Assert.True(r.In(() => Dm.@in(bob, GLOB.current_living_antags)));
        Assert.Contains(r.In(() => Dm.each(bob.actions).ToList()), a => a is datum_action_antag_info);
        Assert.True(r.In(() => Dm.truthy(bob.is_antag())));

        // an assassination of a live target
        datum_objective_assassinate kill = r.In(() =>
        {
            datum_objective_assassinate o = Dm.@new<datum_objective_assassinate>();
            o.owner = bob.mind;
            o.target = alice.mind;
            o.update_explanation_text();
            Dm.append(antag.objectives, o);
            return o;
        });
        Assert.StartsWith("Assassinate " + r.In(() => Dm.str(alice.mind.name)) + ", the ", r.In(() => kill.explanation_text));
        Assert.False(r.In(() => Dm.truthy(kill.check_completion())));

        // a steal of the fire axe: a live target item on a station level makes its steal target exist (the toy room has no z-level
        // traits: is_station_level's cache says its level is the station's)
        r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
        });
        obj_item_fireaxe axe = r.In(() => Dm.@new<obj_item_fireaxe>(locate(8, 6, 1)));
        r.Step(2);
        datum_objective_steal steal = r.In(() =>
        {
            var item = Dm.@new<datum_objective_item_steal_traitor_fireaxe>();
            Assert.False(QDELETED(item), "the fire axe's steal target found no axe");
            Assert.True(Dm.@in(item, GLOB.possible_items));
            datum_objective_steal o = Dm.@new<datum_objective_steal>();
            o.owner = bob.mind;
            Waiting.NoSleep(o.set_target(item));
            Dm.append(antag.objectives, o);
            return o;
        });
        Assert.Equal("Steal a fire axe.", r.In(() => steal.explanation_text));
        Assert.False(r.In(() => Dm.truthy(steal.check_completion())));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(axe)))));
        Assert.True(r.In(() => Dm.truthy(steal.check_completion())));

        // escape: the shuttle has not left, so only tg's forced escape counts
        datum_objective_escape escape = r.In(() =>
        {
            datum_objective_escape o = Dm.@new<datum_objective_escape>();
            o.owner = bob.mind;
            Dm.append(antag.objectives, o);
            return o;
        });
        r.In(() => bob.mind.force_escaped = TRUE);
        Assert.True(r.In(() => Dm.truthy(escape.check_completion())));

        // the window's static data lists the three objectives
        DmList data = r.In(() => (DmList)Waiting.NoSleep(antag.ui_static_data(bob)));
        Assert.EndsWith("Antagonist", r.In(() => Dm.str(data["antag_name"]))); // with BYOND's \improper marker, as tg sends it
        Assert.Equal(3, r.In(() => ((DmList)data["objectives"]).len));

        // the round-end report while the target lives: the antagonist failed
        string report = r.In(() => Dm.str(SSticker.antag_report()));
        Assert.Contains("<b>Bob</b> was <b>" + r.In(() => Dm.str(bob.mind.name)) + "</b>", report);
        Assert.Contains("survived", report);
        Assert.Contains("Assassinate " + r.In(() => Dm.str(alice.mind.name)), report);
        Assert.Contains("Antagonist has failed!</span>", report); // tg's "The [name]": the name keeps its \improper marker
        Assert.Contains("<span class='redtext big'>The ", report);
        Assert.Contains(span_greentext("Success!"), report);

        // the target dies: the assassination is complete, and the antagonist greentexts
        r.In(() => Waiting.NoSleep(alice.death()));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(kill.check_completion())));
        report = r.In(() => Dm.str(SSticker.antag_report()));
        Assert.Contains("Antagonist was successful!</span>", report);
        Assert.Contains("<span class='greentext big'>The ", report);

        // the owner dies: escape fails again
        r.In(() => Waiting.NoSleep(bob.death()));
        r.Step(2);
        Assert.False(r.In(() => Dm.truthy(escape.check_completion())));
        Assert.False(r.In(() => Dm.@in(bob, GLOB.current_living_antags)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTeamHudShowsTheIconOnlyToTeammatesAndTheComboHudToAnAdminsView()
    {
        var (r, _, bob, alice) = Room(7102);
        mob_living_carbon_human carol = r.Join(5, 6, "Carol");
        r.Step(2);
        datum_antagonist_custom bobs = MakeAntag(r, bob, "traitor");
        MakeAntag(r, carol, "traitor");
        r.In(() => bobs.add_team_hud(bob));
        r.Step(2);

        // has_antagonist: a custom antagonist sees Bob's icon, a crewmember does not
        image icon = r.In(() => (image)((datum_atom_hud_alternate_appearance_basic)((datum_weakref)bobs.team_hud_ref).resolve()).alt_image);
        Assert.Equal("traitor", r.In(() => Dm.str(icon.icon_state)));
        Assert.True(r.In(() => Dm.@in(icon, carol.client.images)), "a teammate does not see the antagonist's icon");
        Assert.False(r.In(() => Dm.@in(icon, alice.client.images)), "a crewmember sees the antagonist's icon");

        // the mind's combo HUD: its icon follows the antagonist datums; nobody sees it before the round's end
        // (the mob keeps it under "combo_hud": tg's mind_initialize at each login sets mind.antag_hud to add_alt_appearance's null when the
        // body has one already, initialization.dm:13)
        datum_atom_hud_alternate_appearance_basic_antagonist_hud combo = r.In(() => bob.alternate_appearances?["combo_hud"] as datum_atom_hud_alternate_appearance_basic_antagonist_hud);
        Assert.NotNull(combo);
        // the combo HUD changes its image's icon to an icon of the antagonist's state (antag_hud.dm:93), as tg
        Assert.Equal("traitor", r.In(() => ((icon)combo.alt_image.icon).dm_state));
        Assert.False(r.In(() => Dm.@in(combo.alt_image, alice.client.images)));
        Assert.False(r.In(() => Dm.truthy(combo.mobShouldSee(alice))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAdminsAddAndRemoveLeaveNothingBehind()
    {
        var (r, feed, bob, alice) = Room(7103);
        datum_antagonist_custom antag = r.In(() => Dm.@new<datum_antagonist_custom>());

        // admin_add asks for the custom name (stripped_input, DV340: tgui's text prompt) and makes the mind the antagonist
        r.In(() => Waiting.Detach(antag.admin_add(bob.mind, alice)));
        r.Step(2);
        LastUpdate(Take(alice), out string prompt);
        Act(r, alice, prompt, "submit", ("entry", "Saboteur"));
        r.Step(4);
        Assert.Equal("Saboteur", r.In(() => antag.name));
        Assert.Same(r.In(() => bob.mind), r.In(() => antag.owner));
        // BYOND's \the drops before a proper (capitalised) name
        Assert.True(feed.Any("You are Saboteur."), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.Contains(r.In(() => Dm.each(bob.actions).ToList()), a => a is datum_action_antag_info);
        Assert.True(r.In(() => Dm.@in(bob, GLOB.current_living_antags)));

        // admin_remove: the farewell, and no datum, button, special role or living antagonist entry is left
        r.In(() => Waiting.NoSleep(antag.admin_remove(alice)));
        r.Step(4);
        Assert.True(feed.Any("You are no longer Saboteur!"));
        Assert.False(r.In(() => Dm.@in(antag, bob.mind.antag_datums)));
        Assert.DoesNotContain(r.In(() => Dm.each(bob.actions).ToList()), a => a is datum_action_antag_info);
        Assert.False(r.In(() => Dm.@in(bob, GLOB.current_living_antags)));
        Assert.Equal(0, r.In(() => (float)LAZYLEN(bob.mind.special_roles)));
        Assert.False(r.In(() => Dm.truthy(bob.is_antag())));

        // the mind's remove_antag_datum deletes it
        datum_antagonist_custom again = MakeAntag(r, bob);
        r.In(() => Waiting.NoSleep(bob.mind.remove_antag_datum(typeof(datum_antagonist_custom))));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(again)));
        Assert.False(r.In(() => Dm.@in(again, GLOB.antagonists)));
        r.AssertNoRuntimes();
    }
}
