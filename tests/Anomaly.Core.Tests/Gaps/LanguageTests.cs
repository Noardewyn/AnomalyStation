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
/// Gaps package L2 (D327): the languages in a booted toy room. The language button on a crewman's HUD opens his language menu with the
/// languages he speaks and understands and his default; he cannot pick a default he cannot speak; a ghost grants himself a language
/// through the menu's prompt and toggles the omnitongue; the species languages make names from their species' lists and Nar'Sian mixes
/// its syllables; an EMP'd machine speaks a random uncommon roundstart language when there is one.
/// </summary>
public class LanguageTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        return (r, feed, bob);
    }

    private static string OpenMenu(AudioRoom r, mob who, out JsonElement data)
    {
        Take(who);
        var button = r.In(() => (atom)who.hud_used.screen_objects[HUD_MOB_LANGUAGE_MENU]);
        Assert.NotNull(button);
        r.Command(() => ClientSeam.Click(who.client, button, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(4);
        JsonElement payload = LastUpdate(Take(who), out string window);
        Assert.Equal("LanguageMenu", InterfaceOf(payload));
        data = payload.GetProperty("data");
        return window;
    }

    private static JsonElement Language(JsonElement data, string name) =>
        data.GetProperty("languages").EnumerateArray().First(l => l.GetProperty("name").GetString() == name);

    [Fact]
    public void The_language_button_opens_the_menu_with_what_the_crewman_speaks()
    {
        var (r, feed, bob) = Room(9961);
        string window = OpenMenu(r, bob, out JsonElement data);
        JsonElement common = Language(data, "Galactic Common");
        Assert.Equal(1, common.GetProperty("can_speak").GetDouble());
        Assert.Equal(1, common.GetProperty("can_understand").GetDouble());
        Assert.Equal(1, common.GetProperty("is_default").GetDouble());
        Assert.Equal(1, data.GetProperty("is_living").GetDouble());
        Assert.Equal(0, data.GetProperty("admin_mode").GetDouble());
        JsonElement draconic = Language(data, "Draconic");
        Assert.Equal(0, draconic.GetProperty("can_speak").GetDouble());

        // a language he cannot speak is no default
        Act(r, bob, window, "select_default", ("language_name", "Draconic"));
        Assert.Equal(typeof(datum_language_common), r.In(() => bob.get_language_holder().selected_language));
        // a crewman cannot grant himself one
        Act(r, bob, window, "toggle_omnitongue");
        Assert.Equal(FALSE, r.In(() => bob.get_language_holder().omnitongue));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_ghost_grants_himself_a_language_and_the_omnitongue()
    {
        var (r, feed, bob) = Room(9962);
        client player = r.In(() => bob.client);
        r.In(() => bob.ghostize(TRUE));
        r.Step(2);
        var ghost = r.In(() => (mob_dead_observer)player.mob);
        string window = OpenMenu(r, ghost, out JsonElement data);
        Assert.Equal(1, data.GetProperty("admin_mode").GetDouble());

        Act(r, ghost, window, "grant_language", ("language_name", "Draconic"));
        r.Step(4);
        string prompt = null;
        foreach (TguiOutput o in Take(ghost))
        {
            if (o.Kind != TguiOutputKind.Message)
                continue;
            using JsonDocument doc = JsonDocument.Parse(o.Json);
            if (doc.RootElement.GetProperty("type").GetString() == "update" && InterfaceOf(doc.RootElement.GetProperty("payload")) == "ListInputWindow")
                prompt = o.WindowId;
        }
        Assert.NotNull(prompt);
        Act(r, ghost, prompt, "submit", ("entry", "Both"));
        Assert.True(r.In(() => Dm.truthy(ghost.has_language(typeof(datum_language_draconic), SPOKEN_LANGUAGE)) && Dm.truthy(ghost.has_language(typeof(datum_language_draconic), UNDERSTOOD_LANGUAGE))), "the ghost speaks and understands Draconic");

        float omnitongue = r.In(() => ghost.get_language_holder().omnitongue); // a ghost's universal holder starts with it
        Act(r, ghost, window, "toggle_omnitongue");
        Assert.Equal(omnitongue == TRUE ? FALSE : TRUE, r.In(() => ghost.get_language_holder().omnitongue));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_species_languages_name_their_speakers_and_narsian_mixes_its_syllables()
    {
        var (r, _, _) = Room(9963);
        r.In(() =>
        {
            var draconic = (datum_language)GLOB.language_datum_instances[typeof(datum_language_draconic)];
            string lizard = Dm.str(draconic.get_random_name(MALE));
            string[] parts = lizard.Split(Dm.str(draconic.random_name_spacer));
            Assert.Equal(2, parts.Length);
            Assert.Contains(parts[0], Dm.each(GLOB.lizard_names_male).Select(Dm.str));
            var calcic = (datum_language)GLOB.language_datum_instances[typeof(datum_language_calcic)];
            Assert.Matches(@"^\S.* [IVXLC]+$", Dm.str(calcic.get_random_name()));
            var moffic = (datum_language)GLOB.language_datum_instances[typeof(datum_language_moffic)];
            Assert.Contains(Dm.str(moffic.get_random_name()).Split(' ')[0], Dm.each(GLOB.moth_first).Select(Dm.str));
            // forced syllables use the base generator
            Assert.DoesNotContain(Dm.str(moffic.get_random_name(force_use_syllables: TRUE)).Split(' ')[0], Dm.each(GLOB.moth_first).Select(Dm.str));

            var narsie = (datum_language)GLOB.language_datum_instances[typeof(datum_language_narsie)];
            Assert.True(narsie.syllables.len > 1000, "the base syllables are mixed into the list");
            Assert.Contains(Dm.each(narsie.syllables).Select(Dm.str), s => s.Contains('\'') || s.Contains('-'));
            return 0;
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_emp_teaches_a_machine_a_random_uncommon_language()
    {
        var (r, _, _) = Room(9964);
        var machine = Place<obj_machinery_light_switch>(r, 5, 5);
        // no roundstart species but the human: no uncommon language, the machine keeps its own
        r.In(() => Waiting.NoSleep(machine.emp_act(EMP_HEAVY)));
        Assert.Equal(typeof(datum_language_common), r.In(() => machine.get_selected_language()));

        // with one (tg's quirks unit test does the same), the EMP'd machine speaks it
        r.In(() => GLOB.uncommon_roundstart_languages = Dm.list(typeof(datum_language_uncommon)));
        for (int i = 0; i < 20 && !Equals(r.In(() => machine.get_selected_language()), typeof(datum_language_uncommon)); i++)
            r.In(() => Waiting.NoSleep(machine.emp_act(EMP_HEAVY)));
        Assert.Equal(typeof(datum_language_uncommon), r.In(() => machine.get_selected_language()));
        r.AssertNoRuntimes();
    }
}
