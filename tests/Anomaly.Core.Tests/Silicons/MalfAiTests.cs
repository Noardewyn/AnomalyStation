using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Silicons;

/// <summary>
/// Silicons SI8 (D326): the malfunctioning AI. A malf AI gets the zeroth law, the module picker, codespeak, the malf APC HUD and its
/// objectives; the picker sells a module's ability for its processing time, more uses on a second purchase and upgrades at once; a
/// hacked APC gives processing time; the doomsday device counts down at delta and resets the alert when it dies; the lockdown bolts the
/// station's doors and lifts them again; the voice changer swaps the AI's verbs; the combat software module gives a lawful AI the picker.
/// </summary>
public class MalfAiTests
{
    private const int Second = 20;

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        r.In(() =>
        {
            (get_area(locate(5, 5, 1)) as area).requires_power = FALSE;
            Waiting.NoSleep(config.Set(typeof(datum_config_entry_flag_allow_ai), TRUE));
            SSjob.setup_occupations();
        });
        r.Step(Second);
        return (r, bob);
    }

    private static mob_living_silicon_ai Aiize(AudioRoom r, mob_living_carbon_human who)
    {
        r.In(() => Waiting.Detach(who.AIize(who.client, FALSE)));
        r.Step(Second);
        return r.In(() => Dm.each<mob_living_silicon_ai>(GLOB.ai_list).Single());
    }

    private static datum_antagonist_malf_ai Malf(AudioRoom r, mob_living_silicon_ai ai)
    {
        var malf = r.In(() => Waiting.NoSleep(ai.mind.add_antag_datum(typeof(datum_antagonist_malf_ai))) as datum_antagonist_malf_ai);
        r.Step(Second);
        return malf;
    }

    private static void Act(AudioRoom r, mob who, string iface, string action, string key, object value)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/" + action, Dm.list(Dm.kv(key, value))));
        r.Step(4);
    }

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_antagonist_malf_ai), typeof(datum_antagonist_malf_ai_infected), typeof(datum_module_picker), typeof(datum_ai_module),
            typeof(datum_action_innate_ai), typeof(datum_action_innate_ai_ranged), typeof(datum_action_innate_ai_nuke_station), typeof(obj_machinery_doomsday_device),
            typeof(datum_action_innate_ai_lockdown), typeof(datum_action_innate_ai_ranged_override_machine), typeof(datum_action_innate_ai_destroy_rcds),
            typeof(datum_action_innate_ai_ranged_overload_machine), typeof(datum_action_innate_ai_blackout), typeof(datum_action_innate_ai_honk),
            typeof(datum_action_innate_ai_place_transformer), typeof(datum_action_innate_ai_break_air_alarms), typeof(datum_action_innate_ai_break_fire_alarms),
            typeof(datum_action_innate_ai_emergency_lights), typeof(datum_action_innate_ai_reactivate_cameras), typeof(datum_ai_module_malf_upgrade_upgrade_cameras),
            typeof(datum_ai_module_malf_upgrade_upgrade_turrets), typeof(datum_ai_module_malf_upgrade_eavesdrop), typeof(datum_ai_module_malf_upgrade_mecha_domination),
            typeof(datum_action_innate_ai_voice_changer), typeof(obj_machinery_ai_voicechanger), typeof(datum_action_innate_ai_ranged_emag),
            typeof(datum_action_innate_ai_ranged_core_tilt), typeof(datum_action_innate_ai_ranged_remote_vendor_tilt), typeof(obj_machinery_transformer),
            typeof(obj_effect_countdown_transformer), typeof(obj_effect_countdown_doomsday), typeof(datum_cinematic_malf), typeof(datum_objective_robot_army),
            typeof(datum_objective_survive_malf), typeof(datum_dynamic_ruleset_roundstart_malf_ai), typeof(datum_dynamic_ruleset_midround_from_living_malf_ai),
            typeof(obj_item_ai_module_upgrade), typeof(obj_item_ai_module_combat), typeof(obj_item_ai_module_upgrade_surveillance),
            typeof(datum_ai_module_power_apc), typeof(datum_action_innate_ai_ranged_power_apc),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void AnAiBecomesMalfunctioning()
    {
        var (r, bob) = Room(9801);
        var ai = Aiize(r, bob);
        var malf = Malf(r, ai);
        r.In(() =>
        {
            Assert.True(Dm.truthy(IS_MALF_AI(ai)));
            Assert.True(Dm.truthy(ai.laws.zeroth), "the zeroth law");
            Assert.Equal(TRUE, ai.laws.protected_zeroth);
            Assert.NotNull(ai.malf_picker);
            Assert.NotNull(ai.modules_action);
            Assert.Equal(TRUE, ai.hack_software);
            Assert.True(Dm.truthy(ai.has_language(typeof(datum_language_codespeak))));
            Assert.Contains(Dm.each(malf.objectives), o => o is datum_objective_survive_malf);
            Assert.Equal(50f, ai.malf_picker.processing_time);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePickerSellsAbilitiesAndUpgrades()
    {
        var (r, bob) = Room(9802);
        var ai = Aiize(r, bob);
        Malf(r, ai);
        datum_ai_module Module(string name) => Dm.each<datum_ai_module>(ai.malf_picker.possible_modules).Single(m => m.name == name);
        // Blackout: its action, three uses for 15 PT; a second purchase adds three more
        r.In(() => Waiting.NoSleep(ai.malf_picker.purchase_module(ai, Module("Blackout"))));
        var blackout = r.In(() => Dm.each<datum_action_innate_ai_blackout>(ai.actions).Single());
        Assert.Equal(3f, r.In(() => blackout.uses));
        Assert.Equal(35f, r.In(() => ai.malf_picker.processing_time));
        r.In(() => Waiting.NoSleep(ai.malf_picker.purchase_module(ai, Module("Blackout"))));
        Assert.Equal(6f, r.In(() => blackout.uses));
        // Enhanced Surveillance (30 PT, more than the 20 left): refused; with the time, an upgrade at once, and gone from the shop
        r.In(() => Waiting.NoSleep(ai.malf_picker.purchase_module(ai, Module("Enhanced Surveillance"))));
        Assert.Equal(FALSE, r.In(() => ai.eyeobj.relay_speech));
        r.In(() => ai.malf_picker.processing_time = 200);
        r.In(() => Waiting.NoSleep(ai.malf_picker.purchase_module(ai, Module("Enhanced Surveillance"))));
        Assert.Equal(TRUE, r.In(() => ai.eyeobj.relay_speech));
        Assert.DoesNotContain(r.In(() => Dm.each<datum_ai_module>(ai.malf_picker.possible_modules).Select(m => m.name).ToList()), n => n == "Enhanced Surveillance");
        // the doomsday device wants 10 hacked APCs
        r.In(() => Waiting.NoSleep(ai.malf_picker.purchase_module(ai, Module("Doomsday Device"))));
        Assert.Empty(r.In(() => Dm.each<datum_action_innate_ai_nuke_station>(ai.actions).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDoomsdayDeviceCountsDownAndAbortsOffTheStation()
    {
        var (r, bob) = Room(9803);
        var ai = Aiize(r, bob);
        Malf(r, ai);
        // the doomsday device holds the emergency shuttle (SSshuttle's hostile environment): the room has no map, so one idle in a corner
        r.In(() => SSshuttle.emergency = Dm.@new<obj_docking_port_mobile_emergency>(locate(12, 8, 1)));
        var doom = r.In(() =>
        {
            var d = Dm.@new<obj_machinery_doomsday_device>(ai);
            ai.nuking = TRUE;
            ai.doomsday_device = d;
            d.start();
            return d;
        });
        r.In(() =>
        {
            Assert.Equal(TRUE, doom.timing);
            Assert.InRange(doom.seconds_remaining(), 449, 450);
            Assert.Contains(Dm.text("[]", doom.seconds_remaining()), Dm.str(doom.countdown.get_value()));
            Assert.True(Dm.@in(doom, SSshuttle.hostile_environments), "the shuttle is held");
        });
        // the toy room is no station level: tg's process aborts it ("DOOMSDAY DEVICE OUT OF STATION RANGE") and shuts it off
        // (at its next process: the MC spreads the subsystems' first fires with a random offset each, one more since SSstation fires, gaps R2)
        for (int i = 0; i < 5 * Second && !r.In(() => QDELETED(doom)); i++)
            r.Step(1);
        Assert.True(r.In(() => QDELETED(doom)));
        Assert.Null(r.In(() => ai.doomsday_device));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRoboticFactoryMakesTheAisCyborgs()
    {
        var (r, bob) = Room(9806);
        var ai = Aiize(r, bob);
        Malf(r, ai);
        var factory = r.In(() =>
        {
            var f = Dm.@new<obj_machinery_transformer>(locate(8, 5, 1));
            f.master_ai = ai;
            return f;
        });
        Assert.Equal(3, r.In(() => Dm.each<obj_machinery_conveyor_auto>().Count())); // its conveyor belts
        // a lying human comes in from the east (tg's Bumped)
        mob_living_carbon_human vic = r.Join(9, 5, "Vic");
        client player = r.In(() => vic.client);
        r.In(() => Waiting.NoSleep(vic.Knockdown(10 * SECONDS)));
        r.Step(2);
        Assert.Equal(LYING_DOWN, r.In(() => vic.body_position));
        r.In(() => factory.Bumped(vic));
        r.Step(2 * Second);
        var borg = Assert.IsType<mob_living_silicon_robot>(r.In(() => player.mob));
        Assert.Same(ai, r.In(() => borg.connected_ai));
        Assert.Equal(TRUE, r.In(() => factory.cooldown));
        Assert.Equal("separator-AO0", r.In(() => factory.icon_state));
        // unlocked a few seconds later
        r.Step(10 * Second);
        Assert.Equal(FALSE, r.In(() => borg.lockcharge));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheVoiceChangerSwapsTheVerbs()
    {
        var (r, bob) = Room(9804);
        var ai = Aiize(r, bob);
        Malf(r, ai);
        // the Voice Changer module's action opens the AiVoiceChanger window
        r.In(() => Waiting.NoSleep(ai.malf_picker.purchase_module(ai, Dm.each<datum_ai_module>(ai.malf_picker.possible_modules).Single(m => m.name == "Voice Changer"))));
        var action = r.In(() => Dm.each<datum_action_innate_ai_voice_changer>(ai.actions).Single());
        r.In(() =>
        {
            World.Current.usr = ai;
            Waiting.Detach(action.Activate());
        });
        Act(r, ai, "AiVoiceChanger", "verb", "verb", "hisses");
        Act(r, ai, "AiVoiceChanger", "look", "look", SPAN_CLOWN);
        Act(r, ai, "AiVoiceChanger", "power", "power", 1);
        Assert.Equal("hisses", r.In(() => ai.verb_say));
        Assert.Equal(SPAN_CLOWN, r.In(() => ai.speech_span));
        Act(r, ai, "AiVoiceChanger", "power", "power", 1);
        Assert.NotEqual("hisses", r.In(() => ai.verb_say));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCombatSoftwareModuleGivesALawfulAiThePicker()
    {
        var (r, bob) = Room(9805);
        var ai = Aiize(r, bob);
        var module = r.In(() => Dm.@new<obj_item_ai_module_combat>(locate(4, 4, 1)));
        r.In(() => Waiting.NoSleep(module.silicon_linked_to_installed(ai)));
        Assert.NotNull(r.In(() => ai.malf_picker));
        Assert.Equal(TRUE, r.In(() => ai.hack_software));
        r.In(() => module.silicon_unlinked_from_installed(ai));
        Assert.Null(r.In(() => ai.malf_picker));
        r.AssertNoRuntimes();
    }
}
