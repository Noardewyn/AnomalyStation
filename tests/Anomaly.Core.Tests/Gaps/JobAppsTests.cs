using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps S4a (D327): the NtOS command and records apps: Plexagon Access Management (card.dm) on the identification console,
/// Plexagon HR Core (jobmanagement.dm), the Medical and Security Records (records.dm) and the status display app (statusdisplay.dm).
/// </summary>
public class JobAppsTests
{
    private static (AudioRoom r, mob_living_carbon_human hop, obj_item_modular_computer cpu) IdConsole(ulong seed)
    {
        var r = new AudioRoom(9, 7, seed: seed);
        mob_living_carbon_human hop = r.Join(3, 3, "hop");
        obj_item_modular_computer cpu = r.In(() => Dm.@new<obj_machinery_modular_computer_preset_id>(locate(4, 3, 1)).cpu);
        return (r, hop, cpu);
    }

    private static object Act(AudioRoom r, mob user, datum_computer_file_program program, string action, DmList @params = null) => r.In(() =>
    {
        World w = World.Current;
        object caller = w.usr;
        w.usr = user;
        try
        {
            return Waiting.NoSleep(program.ui_act(action, @params ?? Dm.list()));
        }
        finally
        {
            w.usr = caller;
        }
    });

    [Fact]
    public void The_id_writer_logs_in_with_a_change_ids_card_applies_a_template_toggles_access_renames_and_demotes()
    {
        var (r, hop, cpu) = IdConsole(4101);
        var writer = r.In(() => cpu.find_file_by_name("plexagonidwriter") as datum_computer_file_program_card_mod);
        Assert.NotNull(writer);
        Assert.True(r.In(() => HAS_TRAIT(cpu, TRAIT_MODPC_TWO_ID_SLOTS))); // on_install gives the computer its second slot

        obj_item_card_id target = r.In(() =>
        {
            var spare = Dm.@new<obj_item_card_id_advanced_gold_captains_spare>(locate(3, 3, 1));
            Waiting.NoSleep(cpu.insert_id(spare, hop));
            var card = Dm.@new<obj_item_card_id_advanced>(locate(3, 3, 1));
            Waiting.NoSleep(SSid_access.apply_trim_to_card(card, typeof(datum_id_trim_job_scientist)));
            card.registered_name = "Bob Roe";
            Waiting.NoSleep(cpu.insert_secondary_id(card, hop));
            return (obj_item_card_id)card;
        });
        r.In(() => Assert.Same(target, cpu.alt_stored_id));

        Act(r, hop, writer, "PRG_authenticate");
        r.In(() =>
        {
            Assert.Equal(0f, writer.minor);
            Assert.StartsWith(Dm.str(cpu.stored_id.name), Dm.str(writer.authenticated_card));
            Assert.Equal(SSid_access.station_job_templates.Count, writer.job_templates.Count);
        });

        // the access toggle removes and adds back one of the trim's accesses (a card holds its trim's access without a wildcard)
        Act(r, hop, writer, "PRG_access", Dm.list(Dm.kv("access_target", ACCESS_GENETICS)));
        r.In(() => Assert.DoesNotContain(ACCESS_GENETICS, Dm.each<object>(target.access)));
        Act(r, hop, writer, "PRG_access", Dm.list(Dm.kv("access_target", ACCESS_GENETICS)));
        r.In(() => Assert.Contains(ACCESS_GENETICS, Dm.each<object>(target.access)));
        // a template clears the card and gives it the job's access as far as its trim allows (add_trim_access_to_card)
        Act(r, hop, writer, "PRG_access", Dm.list(Dm.kv("access_target", ACCESS_RESEARCH)));
        r.In(() => Assert.DoesNotContain(ACCESS_RESEARCH, Dm.each<object>(target.access)));
        Act(r, hop, writer, "PRG_template", Dm.list(Dm.kv("name", "Scientist")));
        r.In(() => Assert.Contains(ACCESS_RESEARCH, Dm.each<object>(target.access)));
        Act(r, hop, writer, "PRG_template", Dm.list(Dm.kv("name", "Cook"))); // another job's: cleared, only what both trims share
        r.In(() =>
        {
            Assert.DoesNotContain(ACCESS_RESEARCH, Dm.each<object>(target.access));
            Assert.All(Dm.each<object>(target.access), a => Assert.Contains(a, Dm.each<object>((target.trim as datum_id_trim).access)));
        });
        Act(r, hop, writer, "PRG_template", Dm.list(Dm.kv("name", "Scientist")));

        Act(r, hop, writer, "PRG_edit", Dm.list(Dm.kv("name", "Robert Roe")));
        Act(r, hop, writer, "PRG_assign", Dm.list(Dm.kv("assignment", "Lab Rat")));
        Act(r, hop, writer, "PRG_age", Dm.list(Dm.kv("id_age", 33)));
        r.In(() =>
        {
            Assert.Equal("Robert Roe", target.registered_name);
            Assert.Equal("Lab Rat", target.assignment);
            Assert.Equal(33f, target.registered_age);
            DmList data = Waiting.NoSleep(writer.ui_data(hop)) as DmList;
            DmList modified = data["modified_card"] as DmList;
            Assert.Equal("Robert Roe", Dm.str(modified["id_owner"]));
            Assert.Equal("Lab Rat", Dm.str(modified["id_rank"]));
        });

        // demoting removes the trim and its access
        Act(r, hop, writer, "PRG_terminate");
        r.In(() =>
        {
            Assert.Equal("Demoted", target.assignment);
            Assert.Null(target.trim);
            Assert.DoesNotContain(ACCESS_RESEARCH, Dm.each<object>(target.access));
        });
        Assert.True(r.Sounds.Heard.Any(h => h.File != null && h.File.Contains("terminal_prompt_deny", StringComparison.Ordinal)), r.Sounds.Dump());

        Act(r, hop, writer, "PRG_logout");
        r.In(() => Assert.Null(writer.authenticated_card));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_card_without_change_ids_logs_in_limited_to_its_department()
    {
        var (r, hop, cpu) = IdConsole(4102);
        var writer = r.In(() => cpu.find_file_by_name("plexagonidwriter") as datum_computer_file_program_card_mod);
        r.In(() =>
        {
            var card = Dm.@new<obj_item_card_id_advanced_silver>(locate(3, 3, 1));
            Waiting.NoSleep(SSid_access.apply_trim_to_card(card, typeof(datum_id_trim_job_research_director)));
            Waiting.NoSleep(cpu.insert_id(card, hop));
        });
        Act(r, hop, writer, "PRG_authenticate");
        r.In(() =>
        {
            Assert.Equal(1f, writer.minor);
            Assert.EndsWith("[LIMITED ACCESS]", Dm.str(writer.authenticated_card));
            Assert.Contains(REGION_RESEARCH, Dm.each<object>(writer.region_access));
            Assert.DoesNotContain(REGION_SECURITY, Dm.each<object>(writer.region_access));
            Assert.Contains(ACCESS_RESEARCH, Dm.each<object>(writer.valid_access));
            Assert.DoesNotContain(ACCESS_ARMORY, Dm.each<object>(writer.valid_access));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_hr_core_closes_and_reopens_a_slot_and_prioritizes_a_job()
    {
        var (r, hop, cpu) = IdConsole(4103);
        var hr = r.In(() => cpu.find_file_by_name("plexagoncore") as datum_computer_file_program_job_management);
        Assert.NotNull(hr);
        r.In(() => Waiting.NoSleep(cpu.insert_id(Dm.@new<obj_item_card_id_advanced_gold_captains_spare>(locate(3, 3, 1)), hop)));
        datum_job scientist = r.In(() => SSjob.get_job("Scientist") as datum_job);
        float total = r.In(() => scientist.total_positions);
        r.In(() => GLOB.time_last_changed_position = -1000f); // past the slot cooldown

        // a slot closes; with nobody aboard none opens (at most 30 % of the crew, jobmanagement.dm:41), and the cooldown restarts
        Act(r, hop, hr, "PRG_close_job", Dm.list(Dm.kv("target", "Scientist")));
        r.In(() => Assert.Equal(total - 1, scientist.total_positions));
        Act(r, hop, hr, "PRG_open_job", Dm.list(Dm.kv("target", "Scientist")));
        Act(r, hop, hr, "PRG_close_job", Dm.list(Dm.kv("target", "Scientist")));
        r.In(() =>
        {
            Assert.Equal(total - 1, scientist.total_positions);
            Assert.Equal(-1f, Dm.num(hr.opened_positions["Scientist"]));
            Assert.True(Dm.num((Waiting.NoSleep(hr.ui_data(hop)) as DmList)["cooldown"]) > 0);
        });

        Act(r, hop, hr, "PRG_priority", Dm.list(Dm.kv("target", "Scientist")));
        r.In(() => Assert.Contains(scientist, Dm.each<object>(SSjob.prioritized_jobs)));
        r.In(() =>
        {
            DmList data = Waiting.NoSleep(hr.ui_data(hop)) as DmList;
            Assert.Equal(1f, Dm.num(data["authed"]));
            Assert.Contains("Scientist", Dm.each<object>(data["prioritized"] as DmList));
            Assert.Contains(Dm.each<DmList>(data["slots"] as DmList), s => Dm.eq(s["title"], "Scientist"));
        });
        Act(r, hop, hr, "PRG_priority", Dm.list(Dm.kv("target", "Scientist")));
        r.In(() => Assert.DoesNotContain(scientist, Dm.each<object>(SSjob.prioritized_jobs)));

        // a card without the HoP's access sees the notice and changes nothing
        r.In(() =>
        {
            Waiting.NoSleep(cpu.remove_id(hop));
            Assert.Equal(0f, Dm.num((Waiting.NoSleep(hr.ui_data(hop)) as DmList)["authed"]));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_records_apps_list_the_manifest_and_the_status_app_sets_the_displays()
    {
        var r = new AudioRoom(9, 7, seed: 4104);
        mob_living_carbon_human alice = r.Join(3, 3, "alice");
        r.In(() => alice.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_assistant)) as datum_job));
        r.In(() => GLOB.manifest.inject(alice));
        r.Step(2);
        r.In(() =>
        {
            var security = Dm.@new<datum_computer_file_program_records_security>();
            DmList records = security.GetRecordsReadable();
            DmList entry = Dm.each<DmList>(records).Single(e => Dm.eq(e["name"], alice.real_name));
            Assert.Equal("None", Dm.str(entry["wanted"]));
            var medical = Dm.@new<datum_computer_file_program_records_medical>();
            DmList medEntry = Dm.each<DmList>(medical.GetRecordsReadable()).Single(e => Dm.eq(e["name"], alice.real_name));
            Assert.NotNull(medEntry["bloodtype"]);
        });

        var display = r.In(() => Dm.@new<obj_machinery_status_display_evac>(locate(5, 5, 1)));
        var pda = r.In(() => Dm.@new<obj_item_modular_computer_pda_crew_heads_captain>(locate(3, 3, 1)));
        var status = r.In(() => Dm.each<datum_computer_file_program_status>(pda.stored_files).Single());
        Act(r, alice, status, "setStatusMessage", Dm.list(Dm.kv("upperText", "HELLO"), Dm.kv("lowerText", "CREW")));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(("HELLO", "CREW"), (status.upper_text, status.lower_text));
            Assert.Equal(("HELLO", "CREW"), (display.message1, display.message2));
        });
        r.AssertNoRuntimes();
    }
}
