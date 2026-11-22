using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Silicons;

/// <summary>
/// Silicons SI1 (D326): the lawsets and their numbering (ion and hacked laws numbered with symbols, the zeroth law first), the
/// custom lawset read from config/silicon_laws.txt, tg's ion law generator; the module rack: a core module welded in at map load
/// gives Asimov, a freeform module in a law slot adds a supplied law to the combined lawset, screwed and welded modules cannot be
/// pulled, an ion scramble adds a smoking ion law that a multitool repairs; the State Laws window's data and toggles; a bare
/// silicon's defense (pets, dentless punches, no toxin or stamina damage, EMP burns) and binary's say mode.
/// </summary>
public class SiliconLawsTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed = 47)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static List<string> Laws(AudioRoom r, datum_ai_laws laws, bool zeroth = true) =>
        r.In(() => Dm.each(laws.get_law_list(include_zeroth: zeroth ? TRUE : FALSE, render_html: FALSE)).Select(Dm.str).ToList());

    // A core rack as Meta's map loads it: a runtime `new` passes no mapload (SSatoms' InitAtom), so the map load's steps are done here
    // (law.dm:1040-1045: anchored and secured, the round default's core module welded into the core slot).
    private static obj_machinery_ai_law_rack_base_core CoreRack(AudioRoom r)
    {
        var rack = r.In(() => Dm.@new<obj_machinery_ai_law_rack_base_core>(locate(6, 5, 1)));
        r.In(() =>
        {
            rack.anchored = TRUE;
            rack.secured = TRUE;
            Waiting.NoSleep(rack.load_config_law());
        });
        r.Step(2);
        return rack;
    }

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_ai_laws), typeof(datum_ai_laws_default_asimov), typeof(datum_ai_laws_custom), typeof(datum_state_laws_ui),
            typeof(obj_machinery_ai_law_rack_base_core), typeof(obj_machinery_ai_law_rack_broadcaster), typeof(obj_machinery_ai_law_rack_base_small),
            typeof(obj_item_ai_module_law_core_full_asimov), typeof(obj_item_ai_module_law_supplied_freeform), typeof(obj_item_ai_module_law_zeroth_onehuman),
            typeof(obj_item_ai_module_law_core_full_damaged), typeof(obj_item_ai_module_law_toy_ai), typeof(obj_item_ai_module_law_syndicate),
            typeof(datum_language_machine), typeof(datum_status_effect_borg_slow), typeof(datum_status_effect_firewalled),
            typeof(datum_saymode_binary_handle_message), typeof(datum_preference_choiced_silicon_gender),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void LawsetsNumberTheirLawsAndIonLawsGetSymbols()
    {
        var (r, _, _) = Room();
        var asimov = r.In(() => Dm.@new<datum_ai_laws_default_asimov>());
        List<string> laws = Laws(r, asimov);
        Assert.Equal(3, laws.Count);
        Assert.StartsWith("1: You may not injure a human being", laws[0], StringComparison.Ordinal);
        Assert.StartsWith("3: You must protect your own existence", laws[2], StringComparison.Ordinal);

        r.In(() =>
        {
            asimov.set_zeroth_law("Accomplish your objectives at all costs.");
            asimov.add_hacked_law("Hack the planet.");
            asimov.add_inherent_law("THE CLOWN IS A GOD", 1, ioned: TRUE);
            asimov.add_supplied_law("Protect the bar.");
        });
        laws = Laws(r, asimov);
        Assert.Equal("0: Accomplish your objectives at all costs.", laws[0]);
        Assert.Matches(@"^![!@#$%^&][!@#$%^&*]{3}: Hack the planet\.$", laws[1]);
        Assert.Matches(@"^![!@#$%^&][!@#$%^&*]{3}: THE CLOWN IS A GOD$", laws[2]);
        Assert.StartsWith("1: You may not injure", laws[3], StringComparison.Ordinal); // the ion law takes no number
        Assert.Equal("4: Protect the bar.", laws[6]);
        Assert.DoesNotContain(Laws(r, asimov, zeroth: false), l => l.StartsWith("0:", StringComparison.Ordinal));

        // a copy is independent; a cyborg copy takes the borg's zeroth law
        var copy = r.In(() => (datum_ai_laws)asimov.copy_lawset());
        r.In(() => copy.clear_supplied_laws());
        Assert.Equal(7, Laws(r, asimov).Count);
        var borg = r.In(() => Dm.@new<datum_ai_laws>());
        r.In(() =>
        {
            asimov.set_zeroth_law("Kill.", "Help your master kill.", force: TRUE);
            asimov.ai_to_cyborg(borg);
        });
        Assert.Equal("Help your master kill.", r.In(() => Dm.str(borg.zeroth)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCustomLawsetReadsTheConfigAndIonLawsAreGenerated()
    {
        var (r, _, _) = Room();
        var custom = r.In(() => Dm.@new<datum_ai_laws_custom>());
        Assert.Equal(3, r.In(() => custom.inherent.len)); // tg's config/silicon_laws.txt: Asimov's three
        Assert.Equal("asimov", r.In(() => Dm.str(Dm.initial(typeof(datum_ai_laws_default_asimov), (datum_ai_laws p) => p.id))));
        Assert.Equal(typeof(datum_ai_laws_default_asimov), r.In(() => lawid_to_type("asimov")));
        for (int i = 0; i < 20; i++)
            Assert.False(string.IsNullOrEmpty(r.In(() => Dm.str(generate_ion_law()))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRackCombinesItsModulesIntoOneLawset()
    {
        var (r, feed, bob) = Room();
        var rack = CoreRack(r);
        Assert.Equal(10, r.In(() => rack.ai_modules.len));
        Assert.IsType<obj_item_ai_module_law_core_full_asimov>(r.In(() => rack.ai_modules[1]));
        Assert.Equal(obj_machinery_ai_law_rack.MODULE_WELDED, r.In(() => Dm.num(rack.ai_modules[rack.ai_modules[1]])));
        Assert.Equal(3, r.In(() => rack.combined_lawset.inherent.len));
        Assert.Matches(@"^⁠?core module rack '\w+'$", r.In(() => Dm.str(rack.name)).Replace("", "", StringComparison.Ordinal).Replace("", "", StringComparison.Ordinal));

        // a freeform module configured and inserted into a law slot adds a supplied law
        var freeform = r.In(() => Dm.@new<obj_item_ai_module_law_supplied_freeform>(bob.loc));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(freeform)))));
        r.In(() => freeform.laws[1] = "Bob is the captain.");
        object result = r.In(() => Waiting.NoSleep(rack.ui_act("insert_module", Dm.list(Dm.kv("slot", "2")), Dm.@new<datum_tgui>(bob, rack, "LawRack"), null)));
        r.Step(2);
        Assert.Same(freeform, r.In(() => rack.ai_modules[2]));
        Assert.Contains("Bob is the captain.", r.In(() => Dm.each(rack.combined_lawset.supplied).Select(Dm.str).ToList()));
        Assert.True(feed.Any("inserted slot 2"));

        // a core module never goes into a law slot
        var asimov = r.In(() => Dm.@new<obj_item_ai_module_law_core_full_asimov>(bob.loc));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(asimov)))));
        r.In(() => Waiting.NoSleep(rack.ui_act("insert_module", Dm.list(Dm.kv("slot", "3")), Dm.@new<datum_tgui>(bob, rack, "LawRack"), null)));
        Assert.True(feed.Any("You can't install a core module in a non-core slot!"));
        Assert.Null(r.In(() => rack.ai_modules[3]));

        // the welded core module stays; the unsecured freeform module comes out into Bob's hands
        r.In(() => Waiting.NoSleep(rack.ui_act("remove_module", Dm.list(Dm.kv("slot", "1")), Dm.@new<datum_tgui>(bob, rack, "LawRack"), null)));
        Assert.IsType<obj_item_ai_module_law_core_full_asimov>(r.In(() => rack.ai_modules[1]));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(asimov)));
        r.In(() => Waiting.NoSleep(rack.ui_act("remove_module", Dm.list(Dm.kv("slot", "2")), Dm.@new<datum_tgui>(bob, rack, "LawRack"), null)));
        r.Step(2);
        Assert.Null(r.In(() => rack.ai_modules[2]));
        Assert.Same(freeform, r.In(() => bob.get_active_held_item()));
        Assert.Equal(0, r.In(() => rack.combined_lawset.supplied.len));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnIonScrambleSmokesAModuleAndAMultitoolRepairsIt()
    {
        var (r, feed, bob) = Room();
        var rack = CoreRack(r);
        var core = r.In(() => (obj_item_ai_module_law_core)rack.ai_modules[1]);
        object changed = r.In(() => Waiting.NoSleep(rack.scramble_ai_rack(base_ion_prob: 100, ion_message: "ALL CLOWNS ARE HUMAN")));
        Assert.True(Dm.truthy(changed));
        Assert.Equal(1, r.In(() => core.ioned));
        Assert.Equal("ALL CLOWNS ARE HUMAN", r.In(() => Dm.str(core.laws[1])));
        Assert.Equal(4, r.In(() => rack.combined_lawset.inherent.len));
        Assert.True(r.In(() => Dm.truthy(rack.combined_lawset.inherent["ALL CLOWNS ARE HUMAN"])));
        Assert.Contains("It's smoking.", r.In(() => Dm.str(rack.get_slot_examine(1))));
        Assert.True(r.In(() => GLOB.lawchanges.len) >= 1);

        // the multitool repair restores the saved laws
        var multitool = r.In(() => Dm.@new<obj_item_multitool>(bob.loc));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(multitool)))));
        r.In(() => Waiting.Detach(core.multitool_act(bob, multitool)));
        r.Step(5 * Second);
        Assert.Equal(0, r.In(() => core.ioned));
        Assert.Equal(3, r.In(() => rack.combined_lawset.inherent.len));
        Assert.True(feed.Any("balloon: module repaired"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASiliconStatesItsLawsWithTheStateLawsWindow()
    {
        var (r, feed, bob) = Room();
        var silicon = r.In(() => Dm.@new<mob_living_silicon>(locate(7, 5, 1)));
        r.In(() =>
        {
            silicon.laws = Dm.@new<datum_ai_laws_default_asimov>();
            silicon.laws.add_supplied_law("Protect the bar.");
            silicon.law_ui.update_inherent_stated_laws(silicon.laws);
        });
        var data = r.In(() => (DmList)Waiting.NoSleep(silicon.law_ui.ui_data(silicon)));
        var all = (DmList)data["all_laws"];
        Assert.Equal(4, all.len);
        Assert.Equal(("core", 1f), (Dm.str(((DmList)all[1])["type"]), Dm.num(((DmList)all[1])["number"])));
        Assert.Equal(("supplied", 4f), (Dm.str(((DmList)all[4])["type"]), Dm.num(((DmList)all[4])["number"])));
        // tg: to_state starts as assoc_to_values(laws.inherent), the ion flags (|= keeps one FALSE), not the law texts
        Assert.Equal(1, ((DmList)data["stated_laws"]).len);
        r.In(() => Waiting.NoSleep(silicon.law_ui.ui_act("toggle_stated", Dm.list(Dm.kv("law", "Protect the bar.")), Dm.@new<datum_tgui>(silicon, silicon.law_ui, "StateLawUi"), null)));
        Assert.Equal(2, r.In(() => ((DmList)Waiting.NoSleep(silicon.law_ui.ui_data(silicon)))["stated_laws"] is DmList l ? l.len : 0));

        // stating the laws says each chosen law a second apart, then locks for three seconds
        r.In(() => silicon.law_ui.state_laws(force_all_laws: TRUE));
        r.Step(6 * Second);
        Assert.True(feed.Any("Current Active Laws:"));
        Assert.True(feed.Any("4. Protect the bar."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASiliconShrugsOffPunchesToxinsAndStaminaButBurnsInAnEmp()
    {
        var (r, feed, bob) = Room();
        var silicon = r.In(() => Dm.@new<mob_living_silicon>(locate(6, 5, 1)));
        r.Step(2);
        Assert.True(r.In(() => HAS_TRAIT(silicon, TRAIT_SILICON_ACCESS)));
        Assert.True(r.In(() => Dm.@in(silicon, GLOB.silicon_mobs)));
        r.In(() => Waiting.NoSleep(silicon.attack_hand(bob)));
        Assert.True(feed.Any("You pet "));
        r.In(() => bob.set_combat_mode(TRUE));
        r.In(() => Waiting.NoSleep(silicon.attack_hand(bob)));
        Assert.True(feed.Any("but don't leave a dent!"));
        Assert.Equal(FALSE, r.In(() => Waiting.NoSleep(silicon.adjust_tox_loss(50))));
        Assert.Equal(FALSE, r.In(() => Waiting.NoSleep(silicon.adjust_stamina_loss(50))));
        Assert.Equal(0, r.In(() => Dm.num(silicon.get_tox_loss())));
        r.In(() => Waiting.NoSleep(silicon.emp_act(EMP_HEAVY)));
        Assert.Equal(20, r.In(() => Dm.num(silicon.get_fire_loss())));
        Assert.True(feed.Any("*BZZZT*"));
        Assert.Equal(-10, r.In(() => Dm.num(silicon.assess_threat(null))));
        Assert.True(r.In(() => Dm.truthy(silicon.binarycheck())));
        Assert.False(r.In(() => Dm.truthy(bob.binarycheck())));
        // SSradio's PreInit (radio.dm:8) makes one of each written say mode by key; tg's misnamed /datum/saymode/binary/handle_message
        // comes after /datum/saymode/binary, so it holds "b"
        Assert.Equal(typeof(datum_saymode_binary_handle_message), r.In(() => Dm.each(subtypesof_written(typeof(datum_saymode)))
            .Select(t => (Type)t).Last(t => Dm.str(Dm.initial(t, (datum_saymode p) => p.key)) == "b")));
        r.AssertNoRuntimes();
    }
}
