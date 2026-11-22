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
/// Syndicate package X4 (D316): the blood brothers (brother.dm, can_flash_from_behind.dm, the roundstart ruleset, carbon
/// equip_conspicuous_item) in a booted toy room: the Dynamic ruleset makes a brother with the team's objectives, a flash hidden on
/// the body, the info window and the flash from behind; his flash converts a player head-on or from behind, burns out and completes
/// the shared objective; the refusals answer with tg's balloons; the admins' conversion count takes the skills away and gives them back.
/// </summary>
public class BloodBrotherTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, mob_living_carbon_human Alice) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        mob_living_carbon_human alice = r.Join(5, 4, "Alice");
        // pockets and a backpack for the hidden flash (tg deletes it on a body with neither)
        r.In(() => { Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))); Waiting.NoSleep(alice.equipOutfit(typeof(datum_outfit_job_assistant))); });
        r.Step(Second);
        return (r, feed, bob, alice);
    }

    /// The roundstart ruleset's own call, as Dynamic makes a brother.
    private static datum_antagonist_brother MakeBrother(AudioRoom r, mob_living_carbon_human who)
    {
        r.In(() =>
        {
            var ruleset = Dm.@new<datum_dynamic_ruleset_roundstart_blood_brother>(SSdynamic.get_config());
            Waiting.NoSleep(ruleset.assign_role(who.mind));
        });
        r.Step(4);
        var brother = r.In(() => who.mind.has_antag_datum(typeof(datum_antagonist_brother))) as datum_antagonist_brother;
        Assert.NotNull(brother);
        // the targets the team drew in this two-player room are the other player: cleared, so the flash tests convert
        r.In(() =>
        {
            foreach (datum_objective o in Dm.each<datum_objective>(((datum_team)brother.get_team()).objectives))
                o.target = null;
        });
        return brother;
    }

    private static obj_item_assembly_flash FlashInHand(AudioRoom r, mob_living_carbon_human who)
    {
        var flash = r.In(() => Dm.locate<obj_item_assembly_flash>(who.get_all_contents()));
        Assert.NotNull(flash);
        r.In(() => Waiting.NoSleep(who.put_in_active_hand(flash, TRUE)));
        r.Step(2);
        return flash;
    }

    private static void Flash(AudioRoom r, mob_living_carbon_human user, mob_living_carbon_human target, obj_item_assembly_flash flash)
    {
        r.In(() => { flash.last_trigger = -1000; flash.times_used = 0; flash.burnout_resistance = 1; }); // ready, and no random burnout (only the conversion burns it)
        r.In(() => Waiting.NoSleep(flash.attack__proc(target, user)));
        r.Step(4);
    }

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_antagonist_brother), typeof(datum_team_brother_team), typeof(datum_objective_convert_brother),
            typeof(datum_component_can_flash_from_behind), typeof(datum_dynamic_ruleset_roundstart_blood_brother),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheRulesetMakesABrotherWithTheTeamsObjectivesAHiddenFlashAndTheWindow()
    {
        var (r, feed, bob, _) = Room(7401);
        datum_antagonist_brother brother = MakeBrother(r, bob);
        var team = r.In(() => brother.get_team()) as datum_team_brother_team;
        Assert.NotNull(team);

        // greet, the window, the HUD name; the team spent its founder's slot (2 or, one time in ten, 3, minus one)
        Assert.True(feed.Any("You are a Blood Brother."), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.Contains(r.In(() => Take(bob)), o => o.Json != null && o.Json.Contains("AntagInfoBrother"));
        Assert.Contains(r.In(() => team.brothers_left), new[] { 1f, 2f });
        Assert.True(r.In(() => Dm.@in(bob.mind, team.members)));
        Assert.EndsWith("'s Isolated Intifada", r.In(() => Dm.str(team.name)));

        // the shared objectives: convert first, an escape or a hijack last, all of them the brother's too
        var objectives = r.In(() => Dm.each<datum_objective>(team.objectives).ToList());
        Assert.IsType<datum_objective_convert_brother>(objectives[0]);
        Assert.True(objectives[^1] is datum_objective_escape or datum_objective_hijack, objectives[^1].GetType().Name);
        Assert.All(objectives, o => Assert.True(r.In(() => Dm.@in(o, brother.objectives))));
        Assert.False(r.In(() => Dm.truthy(objectives[0].check_completion())));

        // the conversion skills: a flash in a pocket or the backpack, never in the hands; the flash from behind
        var flash = r.In(() => Dm.locate<obj_item_assembly_flash>(bob.get_all_contents()));
        Assert.NotNull(flash);
        Assert.DoesNotContain(flash, r.In(() => Dm.each<object>(bob.held_items).ToList()));
        Assert.NotNull(r.In(() => bob.GetComponent(typeof(datum_component_can_flash_from_behind))));
        Assert.Contains("Adjust Remaining Conversions", r.In(() => Dm.each<object>(brother.get_admin_commands()).Select(k => Dm.str(k)).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFlashConvertsAPlayerBurnsOutAndCompletesTheSharedObjective()
    {
        var (r, feed, bob, alice) = Room(7402);
        datum_antagonist_brother brother = MakeBrother(r, bob);
        var team = r.In(() => brother.get_team()) as datum_team_brother_team;
        float before = r.In(() => team.brothers_left);
        var flash = FlashInHand(r, bob);
        r.In(() => { bob.face_atom(alice); alice.face_atom(bob); });

        Flash(r, bob, alice, flash);

        var converted = r.In(() => alice.mind.has_antag_datum(typeof(datum_antagonist_brother))) as datum_antagonist_brother;
        Assert.NotNull(converted);
        Assert.Same(team, r.In(() => converted.get_team()));
        Assert.True(r.In(() => Dm.truthy(flash.burnt_out)));
        Assert.Equal(before - 1, r.In(() => team.brothers_left));
        Assert.True(r.In(() => Dm.truthy(Dm.locate<datum_objective_convert_brother>(team.objectives).check_completion())));
        Assert.Contains("Blood Brothers of ", r.In(() => Dm.str(team.name))); // after the \improper marker
        Assert.Contains(" & ", r.In(() => Dm.str(team.name)));
        Assert.True(feed.Any("has been converted to aid you as your brother!"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.True(feed.Any("balloon: converted"));
        Assert.Equal("Conspirators : " + r.In(() => Dm.str(alice.mind.name)) + " | Remaining: " + (before - 1), r.In(() => Dm.str(brother.antag_panel_data())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBrothersFlashWorksFromBehindAndAPlainOneDoesNot()
    {
        var (r, _, bob, alice) = Room(7403);
        // Alice looks away from Bob: a plain flash does nothing
        var plain = Place<obj_item_assembly_flash_handheld>(r, 4, 4);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(plain, TRUE)));
        r.In(() => { bob.face_atom(alice); Waiting.NoSleep(alice.setDir(EAST)); });
        Flash(r, bob, alice, plain);
        Assert.Equal(0f, r.In(() => Dm.num(alice.get_stamina_loss())));

        // the brother's component turns the full deviation into a partial one: the flash lands and converts
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(plain)));
        MakeBrother(r, bob);
        var flash = FlashInHand(r, bob);
        r.In(() => Waiting.NoSleep(alice.setDir(EAST)));
        Flash(r, bob, alice, flash);
        Assert.NotEqual(0f, r.In(() => Dm.num(alice.get_stamina_loss())));
        Assert.True(r.In(() => Dm.truthy(alice.mind.has_antag_datum(typeof(datum_antagonist_brother)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRefusalsAnswerWithTgsBalloons()
    {
        var (r, feed, bob, alice) = Room(7404);
        datum_antagonist_brother brother = MakeBrother(r, bob);
        var flash = FlashInHand(r, bob);
        r.In(() => { bob.face_atom(alice); alice.face_atom(bob); });

        // a target of the brothers' objectives
        r.In(() => ((datum_objective)Dm.each<datum_objective>(((datum_team)brother.get_team()).objectives).Last(o => o is datum_objective_assassinate or datum_objective_steal or datum_objective_escape or datum_objective_hijack)).target = alice.mind);
        Flash(r, bob, alice, flash);
        Assert.True(feed.Any("balloon: that's your target!"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.In(() =>
        {
            foreach (datum_objective o in Dm.each<datum_objective>(((datum_team)brother.get_team()).objectives))
                o.target = null;
        });

        // the unconvertable resist
        r.In(() => ADD_TRAIT(alice, TRAIT_UNCONVERTABLE, "test"));
        Flash(r, bob, alice, flash);
        Assert.True(feed.Any("balloon: she resists!") || feed.Any("balloon: he resists!") || feed.Any("balloon: they resist!"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.In(() => REMOVE_TRAIT(alice, TRAIT_UNCONVERTABLE, "test"));

        // a body with no player
        var dummy = Place<mob_living_carbon_human>(r, 4, 5);
        r.In(() => { bob.face_atom(dummy); dummy.face_atom(bob); });
        Flash(r, bob, dummy, flash);
        Assert.True(feed.Any("mind is vacant!"), string.Join(" | ", feed.Chat.Select(c => c.Html)));

        Assert.False(r.In(() => Dm.truthy(alice.mind.has_antag_datum(typeof(datum_antagonist_brother)))));
        Assert.False(r.In(() => Dm.truthy(flash.burnt_out)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAdminsConversionCountTakesTheSkillsAwayAndGivesThemBack()
    {
        var (r, _, bob, alice) = Room(7405);
        datum_antagonist_brother brother = MakeBrother(r, bob);
        var team = r.In(() => brother.get_team()) as datum_team_brother_team;

        r.In(() => Waiting.NoSleep(team.set_brothers_left(0)));
        Assert.Null(r.In(() => bob.GetComponent(typeof(datum_component_can_flash_from_behind))));
        var flash = FlashInHand(r, bob);
        r.In(() => { bob.face_atom(alice); alice.face_atom(bob); });
        Flash(r, bob, alice, flash);
        Assert.False(r.In(() => Dm.truthy(alice.mind.has_antag_datum(typeof(datum_antagonist_brother)))));

        r.In(() => Waiting.NoSleep(team.set_brothers_left(1)));
        Assert.NotNull(r.In(() => bob.GetComponent(typeof(datum_component_can_flash_from_behind))));
        r.Step(10 * Second); // the flash's daze wears off
        Flash(r, bob, alice, flash);
        Assert.True(r.In(() => Dm.truthy(alice.mind.has_antag_datum(typeof(datum_antagonist_brother)))));
        Assert.Equal(0f, r.In(() => team.brothers_left));

        // losing the datum takes the skills; the last member gone deletes the team
        r.In(() => Waiting.NoSleep(team.remove_member(bob.mind)));
        Assert.Null(r.In(() => bob.mind.has_antag_datum(typeof(datum_antagonist_brother))));
        r.In(() => Waiting.NoSleep(team.remove_member(alice.mind)));
        Assert.True(r.In(() => Dm.truthy(team.gc_destroyed)));
        r.AssertNoRuntimes();
    }
}
