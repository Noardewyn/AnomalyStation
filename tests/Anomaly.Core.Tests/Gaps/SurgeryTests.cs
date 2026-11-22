using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package P1 (D298): the surgery department in a booted toy room. A surgeon operates on a patient buckled to the operating
/// table (tg's free operation): the incision, retraction, clamping and closing with each tool; a heart taken out and put back; an arm
/// amputated and a new one attached to the stump; a forced failure of a coronary bypass doing its harm; a dead body revived by a brain
/// shock; an autopsy printing the scanner's paper report of the cadaver; the operating computer listing its patient's state and next steps and loading a surgery disk; and every new live type and
/// operation made and destroyed with no runtimes.
/// </summary>
public class SurgeryTests
{
    private static readonly Type[] AllTypes =
    {
        typeof(obj_item_surgery_tray), typeof(obj_item_surgery_tray_deployed), typeof(obj_item_surgery_tray_full), typeof(obj_item_surgery_tray_full_deployed),
        typeof(obj_item_surgery_tray_full_morgue), typeof(obj_item_surgery_tray_full_morgue_deployed), typeof(obj_item_surgery_tray_full_advanced),
        typeof(obj_item_disk_surgery), typeof(obj_item_disk_surgery_debug), typeof(obj_item_disk_surgery_advanced_plastic_surgery), typeof(obj_item_disk_surgery_brainwashing),
        typeof(obj_item_disk_surgery_sleeper_protocol), typeof(obj_item_surgical_processor), typeof(obj_item_research_notes), typeof(obj_machinery_computer_operating),
        typeof(obj_item_scalpel_cruel), typeof(obj_item_scalpel_cruel_augment), typeof(obj_item_surgical_drapes), typeof(obj_item_surgical_drapes_cyborg),
        typeof(obj_item_bodypart_arm_left_robot), typeof(obj_item_bodypart_arm_right_robot_surplus), typeof(obj_item_bodypart_leg_left_robot_advanced),
        typeof(obj_item_bodypart_leg_right_robot), typeof(obj_item_bodypart_chest_robot), typeof(obj_item_bodypart_head_robot),
        typeof(obj_item_storage_toolbox_medical), typeof(obj_item_storage_toolbox_medical_full), typeof(obj_item_storage_toolbox_medical_coroner),
        typeof(obj_item_organ_cyberimp_eyes_hud_medical), typeof(obj_item_organ_cyberimp_eyes_hud_security_syndicate), typeof(obj_item_organ_cyberimp_eyes_hud_diagnostic),
        typeof(obj_item_organ_cyberimp_brain_surgical_processor), typeof(obj_item_organ_cyberimp_brain_surgical_processor_pre_loaded), typeof(obj_item_clothing_neck_robe_cape),
    };

    private const int TableX = 5, TableY = 4;

    [ThreadStatic] private static Feed LastFeed;

    private static string Diag(AudioRoom r, Feed feed) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, mob_living_carbon_human Pat, obj_structure_table_optable Table) Room(ulong seed, bool computer = false)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        LastFeed = feed;
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() =>
        {
            (get_area(locate(TableX, TableY, 1)) as area).requires_power = FALSE; // the toy room has no APC
            ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test");
        });
        obj_structure_table_optable table = r.In(() => Dm.@new<obj_structure_table_optable>(locate(TableX, TableY, 1)));
        if (computer)
            r.In(() => Dm.@new<obj_machinery_computer_operating>(locate(TableX - 1, TableY + 1, 1))); // beside the table and the surgeon
        mob_living_carbon_human pat = r.Dummy(TableX, TableY);
        r.Step(Second);
        r.In(() => Waiting.NoSleep(table.buckle_mob(pat, force: TRUE)));
        r.Step(2);
        return (r, feed, bob, pat, table);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        r.In(() =>
        {
            if (who.get_active_held_item() is obj_item old)
                Waiting.NoSleep(who.dropItemToGround(old));
        });
        T thing = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(thing)))));
        return thing;
    }

    private static obj_item_bodypart Part(AudioRoom r, mob_living_carbon who, string zone) => r.In(() => who.get_bodypart(zone, TRUE) as obj_item_bodypart);

    private static float State(AudioRoom r, mob_living_carbon who, string zone) => r.In(() => Part(r, who, zone)?.surgery_state ?? -1);

    /// <summary>The surgeon uses what he holds on the patient's zone; the operation's do-after runs its seconds.</summary>
    private static void Operate(AudioRoom r, mob_living_carbon_human bob, mob target, string zone, float seconds, string pick = null, bool fail = false)
    {
        r.In(() => bob.zone_selected = zone);
        // a right click is tg's intentional failure (item_attack.dm:189)
        r.Command(() => ClientSeam.Click(bob.client, target, "icon-x=16;icon-y=16;" + (fail ? "right=1;button=right" : "left=1;button=left")));
        r.Step(2);
        if (pick != null)
            PickRadial(r, bob, pick);
        r.Step((int)(seconds * Second) + Second);
    }

    /// <summary>The surgeon picks the radial slice named <paramref name="name"/> of the operations menu shown to him.</summary>
    private static void PickRadial(AudioRoom r, mob_living_carbon_human bob, string name)
    {
        r.Step(2);
        (long id, int index) = r.In(() =>
        {
            datum_radial_menu menu = Dm.each_cast<datum_radial_menu>(bob.client.dm_radial_menus).SingleOrDefault();
            Assert.True(menu != null, "no radial menu; " + Diag(r, LastFeed));
            int i = menu.dm_elements.FindIndex(e => e.Name != null && e.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            Assert.True(i >= 0, "no slice " + name + " in " + string.Join(", ", menu.dm_elements.Select(e => e.Name)));
            return (menu.dm_id, i + 1);
        });
        r.Command(() => ClientSeam.RadialPick(bob.client, id, index));
        r.Step(2);
    }

    /// <summary>The static data of the last update that carries it (tg sends it on opening and on update_static_data).</summary>
    private static System.Text.Json.JsonElement LastStatic(List<TguiOutput> outputs)
    {
        System.Text.Json.JsonElement found = default;
        foreach (TguiOutput o in outputs.Where(o => o.Kind == TguiOutputKind.Message))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(o.Json);
            if (doc.RootElement.TryGetProperty("payload", out var p) && p.TryGetProperty("static_data", out var st))
                found = st.Clone();
        }
        Assert.True(found.ValueKind == System.Text.Json.JsonValueKind.Object, "no static data");
        return found;
    }

    [Fact]
    public void A_full_operation_on_the_table_cuts_opens_clamps_and_closes_with_each_tool()
    {
        var (r, feed, bob, pat, table) = Room(9101);
        Assert.True(r.In(() => HAS_TRAIT(pat, TRAIT_READY_TO_OPERATE)), "the table's patient is ready to operate on");

        Hold<obj_item_scalpel>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 1.6f);
        Assert.True(feed.Any("You begin to make an incision in"), "the incision's message");
        Assert.True(r.In(() => LIMB_HAS_SURGERY_STATE(Part(r, pat, BODY_ZONE_CHEST), Dm.bitor(SURGERY_SKIN_CUT, SURGERY_VESSELS_UNCLAMPED))), "skin cut, vessels bleeding");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/handling/surgery/scalpel1.ogg");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/handling/surgery/scalpel2.ogg");

        Hold<obj_item_retractor>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 2.4f);
        Assert.True(r.In(() => LIMB_HAS_SURGERY_STATE(Part(r, pat, BODY_ZONE_CHEST), SURGERY_SKIN_OPEN)), "skin open");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/handling/surgery/retractor2.ogg");

        Hold<obj_item_hemostat>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 2.4f);
        Assert.True(r.In(() => LIMB_HAS_SURGERY_STATE(Part(r, pat, BODY_ZONE_CHEST), SURGERY_VESSELS_CLAMPED)), "vessels clamped");

        Hold<obj_item_cautery>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 2.4f);
        Assert.Equal(0f, r.In(() => Dm.num(Part(r, pat, BODY_ZONE_CHEST).get_reported_surgery_state())));
        Assert.True(feed.Any("You succeed."), "tg's success line");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/handling/surgery/cautery2.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_heart_is_taken_out_and_put_back()
    {
        var (r, feed, bob, pat, _) = Room(9102);
        obj_item_organ_heart heart = r.In(() => pat.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart);
        Hold<obj_item_scalpel>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 1.6f);
        Hold<obj_item_retractor>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 2.4f);
        Hold<obj_item_circular_saw>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 5.4f);
        Assert.True(r.In(() => LIMB_HAS_SURGERY_STATE(Part(r, pat, BODY_ZONE_CHEST), SURGERY_BONE_SAWED)), "the ribs are sawed");
        Hold<obj_item_scalpel>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 2.4f);
        Assert.True(r.In(() => LIMB_HAS_SURGERY_STATE(Part(r, pat, BODY_ZONE_CHEST), SURGERY_ORGANS_CUT)), "the organs are cut");

        Hold<obj_item_hemostat>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 1, pick: "remove heart");
        Assert.True(feed.Any("You successfully extract"), "the extraction's message");
        Assert.Null(r.In(() => pat.get_organ_slot(ORGAN_SLOT_HEART)));
        Assert.Same(r.In(() => locate(TableX, TableY, 1)), r.In(() => heart.loc));

        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(bob.get_active_held_item() as obj_item));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(heart))));
        });
        Operate(r, bob, pat, BODY_ZONE_CHEST, 1);
        Assert.Same(heart, r.In(() => pat.get_organ_slot(ORGAN_SLOT_HEART)));
        Assert.True(feed.Any("You successfully insert"), "the insertion's message");

        // the cautery mends the incision: the opened chest's states go (and with them tg's fishing spot, a deferred leaf)
        Hold<obj_item_cautery>(r, bob);
        r.In(() => bob.zone_selected = BODY_ZONE_CHEST);
        r.Command(() => ClientSeam.Click(bob.client, pat, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(4);
        if (r.In(() => Dm.each_cast<datum_radial_menu>(bob.client.dm_radial_menus).Any()))
            PickRadial(r, bob, "mend skin incision");
        r.Step(4 * Second);
        Assert.False(r.In(() => LIMB_HAS_SURGERY_STATE(Part(r, pat, BODY_ZONE_CHEST), SURGERY_ORGANS_CUT)), "the organs' incision is closed; " + Diag(r, feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_arm_is_amputated_and_a_new_one_attached_to_its_stump()
    {
        var (r, feed, bob, pat, _) = Room(9103);
        obj_item_bodypart old_arm = Part(r, pat, BODY_ZONE_R_ARM);
        Hold<obj_item_scalpel>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 1.6f);
        Hold<obj_item_retractor>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 2.4f);
        Hold<obj_item_hemostat>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 2.4f);
        Hold<obj_item_circular_saw>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 5.4f);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 6.4f);
        Assert.True(feed.Any("You successfully amputate"), "the amputation's message");
        Assert.Null(r.In(() => old_arm.owner));
        Assert.True(r.In(() => Dm.truthy(IS_STUMP(Part(r, pat, BODY_ZONE_R_ARM)))), "a stump is left");

        // tg's stump keeps the arm's skin, bones and vessels: it is opened and clamped before a limb goes on it
        Hold<obj_item_scalpel>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 1.6f);
        Hold<obj_item_retractor>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 2.4f);
        Hold<obj_item_hemostat>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 2.4f);

        obj_item_bodypart_arm_right new_arm = Hold<obj_item_bodypart_arm_right>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 3.2f);
        Assert.True(pat == r.In(() => new_arm.owner), Diag(r, feed));
        Assert.Same(new_arm, Part(r, pat, BODY_ZONE_R_ARM));
        Assert.True(feed.Any("You succeed in replacing"), "the replacement's message");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_arm_is_augmented_with_a_cyborg_arm_and_a_cyborg_torso_takes_a_cell_and_wires()
    {
        var (r, feed, bob, pat, _) = Room(9108);
        obj_item_bodypart old_arm = Part(r, pat, BODY_ZONE_R_ARM);
        Hold<obj_item_scalpel>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 1.6f);
        Hold<obj_item_retractor>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 2.4f);
        var robot_arm = Hold<obj_item_bodypart_arm_right_robot>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_R_ARM, 3.2f);
        Assert.True(feed.Any("You begin to augment"), Diag(r, feed));
        Assert.Same(robot_arm, Part(r, pat, BODY_ZONE_R_ARM));
        Assert.Null(r.In(() => old_arm.owner));
        Assert.True(r.In(() => Dm.truthy(IS_ROBOTIC_LIMB(robot_arm))), "a robotic limb");
        // its render key carries the augments' sheet (generate_icon_key), as the body's icon cache keys it
        Assert.Contains("icons/mob/augmentation/augments.dmi", r.In(() => Dm.each(robot_arm.generate_icon_key()).Select(k => Dm.str(k)).ToList()));

        // the cyborg torso of a robot's assembly: a cell goes in, a cable wires it, the screwdriver takes the cell out
        obj_item_bodypart_chest_robot torso = r.In(() => Dm.@new<obj_item_bodypart_chest_robot>(locate(4, 5, 1)));
        var cell = Hold<obj_item_stock_parts_power_store_cell>(r, bob);
        Click(r, bob, torso);
        r.Step(2);
        Assert.Same(cell, r.In(() => torso.cell));
        Assert.Same(cell, r.In(() => torso.get_cell()));
        var coil = Hold<obj_item_stack_cable_coil>(r, bob);
        Click(r, bob, torso);
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(torso.wired)), "wired");
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, torso);
        r.Step(2);
        Assert.Null(r.In(() => torso.cell));
        Assert.Same(r.In(() => torso.loc), r.In(() => cell.loc));
        Assert.True(feed.Any("You insert"), "the cell's message");

        // the implants: the medical HUD implant gives its HUD, the surgical processor implant its loaded operations to the surgeon
        var hud = r.In(() => Dm.@new<obj_item_organ_cyberimp_eyes_hud_medical>(locate(4, 5, 1)));
        r.In(() => Waiting.NoSleep(hud.Insert(pat)));
        r.Step(2);
        Assert.True(r.In(() => HAS_TRAIT(pat, TRAIT_MEDICAL_HUD)), "the medical HUD implant's HUD");
        var processor = r.In(() => Dm.@new<obj_item_organ_cyberimp_brain_surgical_processor>(locate(4, 5, 1)));
        r.In(() => Dm.combine(ref processor.loaded_surgeries, Dm.list(typeof(datum_surgery_operation_organ_brainwash))));
        r.In(() => Waiting.NoSleep(processor.Insert(bob)));
        r.Step(2);
        var operations = r.In(() =>
        {
            DmList found = Dm.list();
            bob._SendSignal(COMSIG_LIVING_OPERATING_ON, Dm.list(bob, pat, found));
            return Dm.each(found).ToList();
        });
        Assert.Contains(typeof(datum_surgery_operation_organ_brainwash), operations);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_forced_failure_of_the_coronary_bypass_tears_the_heart()
    {
        var (r, feed, bob, pat, _) = Room(9104);
        obj_item_organ_heart heart = r.In(() => pat.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart);
        r.In(() => Waiting.NoSleep(heart.set_organ_damage(heart.maxHealth * 0.7f)));
        Hold<obj_item_scalpel>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 1.6f);
        Hold<obj_item_retractor>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 2.4f);
        Hold<obj_item_circular_saw>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 5.4f);
        Hold<obj_item_scalpel>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 2.4f);
        float before = r.In(() => Dm.num(heart.damage));

        Hold<obj_item_hemostat>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 9, pick: "coronary bypass", fail: true);
        Assert.True(feed.Any("You screw up in attaching the graft"), "the failure's message");
        Assert.True(r.In(() => Dm.num(heart.damage)) > before, "the failure damages the heart");
        Assert.False(r.In(() => HAS_TRAIT(heart, TRAIT_ORGAN_OPERATED_ON)), "the failed repair does not count");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_brain_shock_revives_a_dead_but_whole_body()
    {
        var (r, feed, bob, pat, _) = Room(9105);
        r.In(() => Waiting.NoSleep(pat.death()));
        r.Step(Second);
        Assert.Equal(DEAD, r.In(() => pat.stat));
        Hold<obj_item_scalpel>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_HEAD, 1.6f);
        Hold<obj_item_retractor>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_HEAD, 2.4f);
        Hold<obj_item_circular_saw>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_HEAD, 5.4f);

        obj_item_melee_baton_security_loaded baton = Hold<obj_item_melee_baton_security_loaded>(r, bob);
        r.Command(() => Waiting.Detach(baton.attack_self(bob)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(baton.active)), "the baton is on");
        Operate(r, bob, pat, BODY_ZONE_HEAD, 5 * 1.33f);
        Assert.True(feed.Any("wakes up, alive and aware"), Diag(r, feed));
        Assert.NotEqual(DEAD, r.In(() => pat.stat));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_autopsy_prints_the_scanners_paper_report_of_the_cadaver()
    {
        var (r, feed, bob, pat, _) = Room(9108);
        r.In(() => Waiting.NoSleep(pat.fully_replace_character_name(null, "Pat Cadaver")));
        r.In(() => Waiting.NoSleep(pat.death()));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(((datum_reagents)pat.reagents).add_reagent(typeof(datum_reagent_medicine_morphine), 7)));
        Hold<obj_item_scalpel>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 1.6f);
        Hold<obj_item_retractor>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 2.4f);
        Hold<obj_item_autopsy_scanner>(r, bob);
        Operate(r, bob, pat, BODY_ZONE_CHEST, 10 * 1.5f);

        Assert.True(r.In(() => HAS_TRAIT_FROM(pat, TRAIT_DISSECTED, AUTOPSY_TRAIT)), Diag(r, feed));
        obj_item_paper report = r.In(() => Dm.each(bob.held_items).OfType<obj_item_paper>().SingleOrDefault());
        Assert.True(report != null, "the report is put in the surgeon's free hand; " + Diag(r, feed));
        Assert.True(feed.Any("report printed"), "the scanner's balloon");
        Assert.StartsWith("autopsy report of Pat Cadaver - ", r.In(() => report.name));
        Assert.Equal("#99ccff", r.In(() => report.color));
        string text = r.In(() => (string)report.get_raw_text());
        Assert.Contains("Analyzing results for <b>Pat Cadaver</b>", text);
        Assert.Contains("<td><b>Human chest:</b></td>", text);
        Assert.Contains("<td><b>Heart:</b></td>", text);
        Assert.Contains("<td><b>Lungs:</b></td>", text);
        Assert.Contains("<b>Morphine:</b> 7 units in bloodstream.", text);
        Assert.Matches(@"<b>Species:</b> \p{Co}?Human</br>", text); // tg's "\improper Human" keeps its marker in stored text
        Assert.EndsWith("<b>Coroner's Notes:</b>/", text);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_operating_computer_lists_its_patient_and_loads_a_disk()
    {
        var (r, feed, bob, pat, table) = Room(9106, computer: true);
        obj_machinery_computer_operating pc = r.In(() => table.computer as obj_machinery_computer_operating);
        Assert.True(pc != null, Diag(r, feed));
        Assert.Same(table, r.In(() => pc.table));
        Assert.Same(pat, r.In(() => table.patient));

        Hold<obj_item_scalpel>(r, bob);
        r.In(() => bob.zone_selected = BODY_ZONE_CHEST);
        Click(r, bob, pc);
        r.Step(4);
        List<TguiOutput> opened = Take(bob);
        System.Text.Json.JsonElement payload = LastUpdate(opened, out string windowId);
        Assert.Equal("OperatingComputer", InterfaceOf(payload));
        var data = payload.GetProperty("data");
        Assert.Equal("Stable", data.GetProperty("patient").GetProperty("stat").GetString());
        Assert.Contains(data.GetProperty("patient").GetProperty("surgery_state").EnumerateArray(), s => s.GetString() == "Ready for surgery");
        var surgeries = LastStatic(opened).GetProperty("surgeries").EnumerateArray().ToList();
        Assert.Contains(surgeries, s => s.GetProperty("name").GetString() == "make skin incision" && s.GetProperty("show_as_next").GetInt32() == 1);
        Assert.DoesNotContain(surgeries, s => s.GetProperty("name").GetString() == "Neural Brainwashing (Brainwash)");

        Hold<obj_item_disk_surgery_brainwashing>(r, bob);
        Click(r, bob, pc);
        r.Step(2 * Second);
        Assert.True(feed.Any("surgeries loaded"), "the disk's balloon");
        Assert.Contains(typeof(datum_surgery_operation_organ_brainwash), r.In(() => Dm.each(pc.advanced_surgeries).ToList()));
        var loaded = LastStatic(Take(bob));
        Assert.Contains(loaded.GetProperty("surgeries").EnumerateArray(), s => s.GetProperty("name").GetString() == "Neural Brainwashing (Brainwash)");

        Act(r, bob, windowId, "change_zone", ("new_zone", BODY_ZONE_HEAD));
        Assert.Equal(BODY_ZONE_HEAD, r.In(() => pc.target_zone));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Every_type_and_operation_is_made_and_destroyed_with_no_runtimes()
    {
        var (r, _, bob, pat, _) = Room(9107);
        r.In(() =>
        {
            foreach (datum_surgery_operation operation in Dm.each_cast<datum_surgery_operation>(GLOB.operations.get_instances_from(GLOB.operations.locked.Copy().Also(l => l.Add(GLOB.operations.unlocked)), FALSE)))
            {
                Assert.NotNull(operation.name);
                Assert.Equal(4, ((DmList)operation.get_requirements()).len);
                Assert.NotNull(Waiting.NoSleep(operation.get_default_radial_image()));
            }
        });
        foreach (Type type in AllTypes)
        {
            atom made = r.In(() => (atom)Dm.@new(type, locate(3, 3, 1)));
            r.Step(2);
            r.In(() => qdel(made));
            r.Step(2);
        }
        r.Step(Second);
        r.AssertNoRuntimes();
    }
}

internal static class SurgeryTestExtensions
{
    public static DmList Also(this DmList list, Action<DmList> act)
    {
        act(list);
        return list;
    }
}
