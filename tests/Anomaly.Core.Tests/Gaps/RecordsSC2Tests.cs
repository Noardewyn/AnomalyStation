using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using Xunit;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package SC2 (D333): the records consoles in a toy room. A security officer logs in at the security records console, writes a
/// crime on a crewman's record (who becomes wanted, as the security HUD shows), sets her incarcerated, writes a citation, edits and voids
/// a crime, previews the record's character and prints a rapsheet and a missing poster with the record's mugshot; the warrant console
/// takes a fine from the payer's account into the security budget and prints a bounty; a doctor edits a record at the medical records
/// console, keeps notes and expunges it on a station level only; a login without access fails.
/// </summary>
public class RecordsSC2Tests
{
    private readonly ITestOutputHelper _output;

    public RecordsSC2Tests(ITestOutputHelper output) => _output = output;

    private sealed class Room
    {
        public AudioRoom R;
        public Feed Feed;
        public mob_living_carbon_human Bob;
        public mob_living_carbon_human Carol;
        public datum_record_crew Record;
        public string CrewRef;
    }

    /// <summary>Bob at (3, 3) with an ID of the given trim; Carol at (6, 3) on the crew manifest.</summary>
    private static Room Make(ulong seed, Type trim, bool station = false)
    {
        var r = new AudioRoom(9, 7, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 3, "Bob");
        mob_living_carbon_human carol = r.Join(6, 3, "Carol");
        r.In(() => (get_area(locate(3, 3, 1)) as area).requires_power = FALSE); // the toy room has no APC
        for (int i = 0; i < 30 && r.In(() => SSeconomy.times_fired) < 1; i++) // the department budgets
            r.Step(Second);
        if (station) // a station level, as SSmapping keeps for a loaded map (the consoles' expunge and purge read it, through the cache)
            r.In(() =>
            {
                SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
                GLOB.station_levels_cache.len = 1;
                GLOB.station_levels_cache[1] = TRUE;
            });
        VendingTests.Funds(r, bob, 500);
        r.In(() => Waiting.NoSleep(SSid_access.apply_trim_to_card((obj_item_card_id)bob.wear_id, trim)));
        datum_record_crew record = r.In(() =>
        {
            carol.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_assistant)) as datum_job);
            GLOB.manifest.inject(carol);
            return Dm.each<datum_record_crew>(GLOB.manifest.general).Single(x => Dm.eq(x.name, carol.real_name));
        });
        r.Step(2);
        return new Room { R = r, Feed = feed, Bob = bob, Carol = carol, Record = record, CrewRef = r.In(() => (string)REF(record)) };
    }

    private static string Open(Room c, atom console, string name, out JsonElement payload)
    {
        Take(c.Bob);
        Click(c.R, c.Bob, console);
        payload = LastUpdate(Take(c.Bob), out string window);
        Assert.Equal(name, InterfaceOf(payload));
        return window;
    }

    private static string Diag(Room c) => "chat: " + string.Join(" | ", c.Feed.Chat.Select(m => m.Html)) + " runtimes: " + string.Join("\n", c.R.Room.RuntimeText);

    private static string WantedHud(Room c) => c.R.In(() => (c.Carol.hud_list?[WANTED_HUD] as image)?.icon_state as string);

    [Fact]
    public void A_security_officer_writes_a_crime_sets_the_status_the_hud_shows_and_prints_a_rapsheet()
    {
        Room c = Make(3301, typeof(datum_id_trim_job_security_officer));
        var console = Place<obj_machinery_computer_records_security>(c.R, 4, 4);
        string window = Open(c, console, "SecurityRecords", out JsonElement payload);
        Assert.Equal(0, payload.GetProperty("data").GetProperty("authenticated").GetDouble());

        Act(c.R, c.Bob, window, "login");
        JsonElement data = Data(c.R, c.Bob, out window);
        Assert.Equal(1, data.GetProperty("authenticated").GetDouble());
        Assert.True(c.Feed.Any("balloon: logged in"), Diag(c));
        JsonElement record = data.GetProperty("records").EnumerateArray().Single(x => x.GetProperty("crew_ref").GetString() == c.CrewRef);
        Assert.Equal("None", record.GetProperty("wanted_status").GetString());
        Assert.Contains("Arrest", data.GetProperty("available_statuses").EnumerateArray().Select(x => x.GetString()));

        // Arrest needs a crime
        Act(c.R, c.Bob, window, "set_wanted", ("crew_ref", c.CrewRef), ("status", "Arrest"));
        Assert.Equal(WANTED_NONE, c.R.In(() => c.Record.wanted_status));

        // a crime (no fine): Carol is wanted, and the security HUD over her says so
        Act(c.R, c.Bob, window, "add_crime", ("crew_ref", c.CrewRef), ("name", "Theft"), ("details", "Took the <b>captain</b> spare"), ("fine", 0f));
        Assert.Equal(WANTED_ARREST, c.R.In(() => c.Record.wanted_status));
        datum_crime crime = c.R.In(() => Dm.each<datum_crime>(c.Record.crimes).Single());
        Assert.Equal(("Theft", "Took the captain spare"), c.R.In(() => (Dm.str(crime.name), Dm.str(crime.details)))); // strip_html_full
        Assert.Same(c.Bob, c.R.In(() => crime.author));
        Assert.Equal("hudwanted", WantedHud(c));
        data = Data(c.R, c.Bob, out window);
        record = data.GetProperty("records").EnumerateArray().Single(x => x.GetProperty("crew_ref").GetString() == c.CrewRef);
        Assert.Equal("Arrest", record.GetProperty("wanted_status").GetString());
        JsonElement crimeData = record.GetProperty("crimes").EnumerateArray().Single();
        Assert.Equal(c.R.In(() => Dm.str(c.Bob.name)), crimeData.GetProperty("author").GetString()); // a mob's JSON is its name
        Assert.Equal(1, crimeData.GetProperty("valid").GetDouble());

        // the status buttons
        Act(c.R, c.Bob, window, "set_wanted", ("crew_ref", c.CrewRef), ("status", "Incarcerated"));
        Assert.Equal(WANTED_PRISONER, c.R.In(() => c.Record.wanted_status));
        Assert.Equal("hudincarcerated", WantedHud(c));
        Act(c.R, c.Bob, window, "set_wanted", ("crew_ref", c.CrewRef), ("status", "Nonsense"));
        Assert.Equal(WANTED_PRISONER, c.R.In(() => c.Record.wanted_status));

        // a fine is a citation; one over the configured maximum is refused
        Act(c.R, c.Bob, window, "add_crime", ("crew_ref", c.CrewRef), ("name", "Littering"), ("fine", 150f));
        datum_crime_citation citation = c.R.In(() => Dm.each<datum_crime_citation>(c.Record.citations).Single());
        Assert.Equal(150f, c.R.In(() => Dm.num(citation.fine)));
        Act(c.R, c.Bob, window, "add_crime", ("crew_ref", c.CrewRef), ("name", "Speeding"), ("fine", 100000f));
        Assert.Equal(1, c.R.In(() => c.Record.citations.len));
        Assert.True(c.Feed.Any("The maximum fine is"), Diag(c));

        // the author edits the crime; the note
        Act(c.R, c.Bob, window, "edit_crime", ("crew_ref", c.CrewRef), ("crime_ref", c.R.In(() => (string)REF(crime))), ("name", "Grand theft"));
        Assert.Equal("Grand theft", c.R.In(() => Dm.str(crime.name)));
        Act(c.R, c.Bob, window, "set_note", ("crew_ref", c.CrewRef), ("note", "Known <i>troublemaker</i>"));
        Assert.Equal("Known troublemaker", c.R.In(() => Dm.str(c.Record.security_note)));
        Act(c.R, c.Bob, window, "edit_field", ("ref", c.CrewRef), ("field", "fingerprint"), ("value", "ABC123"));
        Assert.Equal("ABC123", c.R.In(() => Dm.str(c.Record.fingerprint)));

        // the character preview: tg's first click makes the view, the second gives it the record's appearance
        string assigned = data.GetProperty("assigned_view").GetString();
        Assert.Equal(c.R.In(() => Dm.text("preview_[]_[]_records", c.Bob.ckey, REF(console))), assigned);
        Act(c.R, c.Bob, window, "view_record", ("assigned_view", assigned), ("crew_ref", c.CrewRef));
        Act(c.R, c.Bob, window, "view_record", ("assigned_view", assigned), ("crew_ref", c.CrewRef));
        var view = c.R.In(() => console.character_preview_view);
        Assert.NotNull(view);
        Assert.NotNull(c.R.In(() => c.Bob.client.screen_maps[assigned]));

        // the rapsheet comes out after two seconds
        Act(c.R, c.Bob, window, "print_record", ("crew_ref", c.CrewRef), ("type", "rapsheet"), ("alias", ""), ("desc", ""), ("head", ""));
        Assert.Equal(TRUE, c.R.In(() => Dm.num(console.printing)));
        Act(c.R, c.Bob, window, "print_record", ("crew_ref", c.CrewRef), ("type", "rapsheet"), ("alias", ""), ("desc", ""), ("head", ""));
        Assert.True(c.Feed.Any("balloon: printer busy"), Diag(c));
        c.R.Step(2 * Second + 2);
        var paper = c.R.In(() => Dm.each<obj_item_paper>(console.loc).SingleOrDefault());
        Assert.NotNull(paper);
        Assert.Equal(FALSE, c.R.In(() => Dm.num(console.printing)));
        string text = c.R.In(() => Dm.str(Dm.each<datum_paper_input>(paper.raw_text_inputs).First().raw_text));
        Assert.Contains("Grand theft", text);

        // voiding the crime by its author: discharged, and the HUD follows
        Act(c.R, c.Bob, window, "invalidate_crime", ("crew_ref", c.CrewRef), ("crime_ref", c.R.In(() => (string)REF(crime))));
        Assert.Equal(FALSE, c.R.In(() => Dm.num(crime.valid)));
        Assert.Same(c.Bob, c.R.In(() => crime.voider));
        Assert.Equal(WANTED_DISCHARGED, c.R.In(() => c.Record.wanted_status));
        Assert.Equal("huddischarged", WantedHud(c));

        // the window closes: the preview goes with it
        c.R.In(() => console.ui_close(c.Bob));
        Assert.Null(c.R.In(() => console.character_preview_view));
        Assert.True(c.R.In(() => QDELETED(view)));

        Act(c.R, c.Bob, window, "logout");
        Assert.Equal(FALSE, c.R.In(() => Dm.num(console.authenticated)));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void Posters_print_with_the_records_mugshot_and_the_header()
    {
        Room c = Make(3302, typeof(datum_id_trim_job_security_officer));
        var console = Place<obj_machinery_computer_records_security>(c.R, 4, 4);
        string window = Open(c, console, "SecurityRecords", out _);
        Act(c.R, c.Bob, window, "login");

        // a missing poster: no crimes needed
        Act(c.R, c.Bob, window, "print_record", ("crew_ref", c.CrewRef), ("type", "missing"), ("alias", "Carrie"), ("desc", "Last seen in the bar."), ("head", ""));
        c.R.Step(2 * Second + 2);
        var poster = c.R.In(() => Dm.each<obj_item_poster_wanted_missing>(console.loc).SingleOrDefault());
        Assert.True(poster != null, Diag(c));
        Assert.Equal(("missing poster (Carrie)", "A missing poster for Carrie."), c.R.In(() => (Dm.str(poster.name), Dm.str(poster.desc))));
        var structure = c.R.In(() => poster.poster_structure as obj_structure_sign_poster_wanted);
        Assert.NotNull(structure);
        Assert.Equal(("Missing", "Last seen in the bar."), c.R.In(() => (Dm.str(structure.posterHeaderText), Dm.str(structure.desc))));
        // the paper, with the mugshot and the header's seven letters over it (DV498: overlays, not one blended icon)
        Assert.Equal("icons/obj/poster_wanted.dmi", c.R.In(() => (structure.icon as icon)?.dm_resource()));
        var recipes = c.R.In(() => Dm.each(structure.overlays).OfType<IDmAppearance>().Select(a => (a.icon as icon)?.dm_resource() ?? "").ToList());
        _output.WriteLine("the poster's overlays' recipes: " + string.Join(", ", recipes.Select(x => x.Length)) + " bytes");
        Assert.Equal(8, recipes.Count);
        Assert.Contains("bodyparts", recipes[0], StringComparison.Ordinal); // the mugshot: the record's flat picture, shifted and cropped
        Assert.EndsWith("|102,-5,-2,k,26,29", recipes[0], StringComparison.Ordinal);
        // DV498: the mugshot's recipe alone is longer than a resource (Limits.MaxResourceBytes), so the client does not draw it yet
        Assert.True(recipes[0].Length > 512, "the mugshot's recipe: " + recipes[0].Length + " bytes");
        Assert.All(recipes.Skip(1), letter =>
        {
            Assert.StartsWith("iconops:icons/testing/Font_Minimal.dmi|", letter, StringComparison.Ordinal);
            Assert.True(letter.Length <= 512, "a letter fits a resource (Limits.MaxResourceBytes): " + letter.Length);
        });
        Assert.Equal("MISSING", string.Concat(recipes.Skip(1).Select(letter => letter.Split('|')[1])));

        // the record keeps the photo it made
        var front = c.R.In(() => c.Record.get_front_photo() as obj_item_photo);
        Assert.Same(front, c.R.In(() => c.Record.get_front_photo()));
        Assert.Equal(c.R.In(() => Dm.text("This is [].", c.Record.name)), c.R.In(() => Dm.str(front.picture.picture_desc)));
        var side = c.R.In(() => c.Record.get_side_photo() as obj_item_photo);
        Assert.NotSame(front, side);

        // a wanted poster needs crimes, and lists them
        Act(c.R, c.Bob, window, "add_crime", ("crew_ref", c.CrewRef), ("name", "Assault"), ("details", "Robusted a clown"), ("fine", 0f));
        Act(c.R, c.Bob, window, "print_record", ("crew_ref", c.CrewRef), ("type", "wanted"), ("alias", ""), ("desc", ""), ("head", ""));
        c.R.Step(2 * Second + 2);
        var wanted = c.R.In(() => Dm.each<obj_item_poster_wanted>(console.loc).SingleOrDefault(p => p is not obj_item_poster_wanted_missing));
        Assert.True(wanted != null, Diag(c));
        Assert.Equal(c.R.In(() => Dm.text("wanted poster ([])", c.Record.name)), c.R.In(() => Dm.str(wanted.name)));
        string description = c.R.In(() => Dm.str(wanted.poster_structure.desc));
        Assert.Contains("WANTED FOR:", description);
        Assert.Contains("Robusted a clown", description);
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_warrant_console_takes_a_fine_into_the_security_budget_and_prints_a_bounty()
    {
        Room c = Make(3303, typeof(datum_id_trim_job_assistant));
        datum_crime_citation citation = c.R.In(() =>
        {
            var made = Dm.@new<datum_crime_citation>(Dm.named("name", "Littering"), Dm.named("details", "Dropped a peel"), Dm.named("author", "Officer Beepsky"), Dm.named("fine", 300f));
            Dm.append(ref c.Record.citations, made);
            return made;
        });
        string fineRef = c.R.In(() => (string)REF(citation));
        datum_bank_account mine = c.R.In(() => ((obj_item_card_id)c.Bob.wear_id).registered_account);
        float security = c.R.In(() => SSeconomy.get_dep_account(ACCOUNT_SEC).account_balance);

        var console = Place<obj_machinery_computer_warrant>(c.R, 4, 4);
        string window = Open(c, console, "WarrantConsole", out JsonElement payload);
        JsonElement record = payload.GetProperty("data").GetProperty("records").EnumerateArray().Single();
        Assert.Equal(c.R.In(() => Dm.str(c.Record.name)), record.GetProperty("crew_name").GetString());
        JsonElement fine = record.GetProperty("citations").EnumerateArray().Single();
        Assert.Equal(("Littering", 300.0, fineRef), (fine.GetProperty("fine_name").GetString(), fine.GetProperty("fine").GetDouble(), fine.GetProperty("fine_ref").GetString()));

        // an amount over the fine is refused
        Act(c.R, c.Bob, window, "pay", ("amount", 400f), ("crew_ref", c.CrewRef), ("fine_ref", fineRef));
        Assert.True(c.Feed.Any("ACCESS DENIED: Invalid amount."), Diag(c));
        Assert.Equal(500f, c.R.In(() => mine.account_balance));

        Act(c.R, c.Bob, window, "pay", ("amount", 100f), ("crew_ref", c.CrewRef), ("fine_ref", fineRef));
        Assert.Equal(400f, c.R.In(() => mine.account_balance));
        Assert.Equal(security + 100, c.R.In(() => SSeconomy.get_dep_account(ACCOUNT_SEC).account_balance));
        Assert.Equal((200f, 100f), c.R.In(() => (Dm.num(citation.fine), Dm.num(citation.paid))));

        // a bounty paper for the rest
        Act(c.R, c.Bob, window, "print", ("crew_ref", c.CrewRef), ("fine_ref", fineRef));
        c.R.Step(2 * Second + 2);
        var bounty = c.R.In(() => Dm.each<obj_item_paper>(console.loc).SingleOrDefault());
        Assert.True(bounty != null, Diag(c));
        Assert.Equal(c.R.In(() => Dm.text("Bounty for []", c.Record.name)), c.R.In(() => Dm.str(bounty.name)));
        Assert.Contains("<b>FINE:</b> 200", c.R.In(() => Dm.str(Dm.each<datum_paper_input>(bounty.raw_text_inputs).First().raw_text)));

        // paid off: the citation is no longer valid and leaves the list
        window = Open(c, console, "WarrantConsole", out _);
        Act(c.R, c.Bob, window, "pay", ("amount", 200f), ("crew_ref", c.CrewRef), ("fine_ref", fineRef));
        // tg caps what was paid at the fine left before this payment (crime.dm:40): 100 + 200 paid shows 200
        Assert.Equal((0f, 200f, FALSE), c.R.In(() => (Dm.num(citation.fine), Dm.num(citation.paid), Dm.num(citation.valid))));
        Assert.Equal(200f, c.R.In(() => mine.account_balance));
        Act(c.R, c.Bob, window, "refresh");
        JsonElement data = Data(c.R, c.Bob, out _);
        Assert.Empty(data.GetProperty("records").EnumerateArray().Single().GetProperty("citations").EnumerateArray());
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_doctor_edits_a_medical_record_keeps_notes_and_expunges_it_on_a_station_level()
    {
        Room c = Make(3304, typeof(datum_id_trim_job_medical_doctor));
        var console = Place<obj_machinery_computer_records_medical>(c.R, 4, 4);
        string window = Open(c, console, "MedicalRecords", out JsonElement payload);
        Act(c.R, c.Bob, window, "login");
        JsonElement data = Data(c.R, c.Bob, out window);
        Assert.Equal(1, data.GetProperty("authenticated").GetDouble());
        JsonElement record = data.GetProperty("records").EnumerateArray().Single(x => x.GetProperty("crew_ref").GetString() == c.CrewRef);
        Assert.Equal(c.R.In(() => Dm.str(c.Record.name)), record.GetProperty("name").GetString());
        // initial(target.blood_type:name) of the manifest's text blood type is null, as in BYOND (medical.dm:57): the dropdown starts empty
        Assert.Equal(JsonValueKind.Null, record.GetProperty("blood_type").ValueKind);

        Act(c.R, c.Bob, window, "set_physical_status", ("crew_ref", c.CrewRef), ("physical_status", PHYSICAL_DECEASED));
        Act(c.R, c.Bob, window, "set_cause_of_death", ("crew_ref", c.CrewRef), ("cause", "Blunt Trauma"));
        Act(c.R, c.Bob, window, "set_mental_status", ("crew_ref", c.CrewRef), ("mental_status", MENTAL_WATCH));
        Assert.Equal((PHYSICAL_DECEASED, "Blunt Trauma", MENTAL_WATCH), c.R.In(() => (Dm.str(c.Record.physical_status), Dm.str(c.Record.cause_of_death), Dm.str(c.Record.mental_status))));
        Act(c.R, c.Bob, window, "set_physical_status", ("crew_ref", c.CrewRef), ("physical_status", PHYSICAL_ACTIVE));
        Assert.Equal("", c.R.In(() => Dm.str(c.Record.cause_of_death))); // alive again: no cause of death
        Act(c.R, c.Bob, window, "set_physical_status", ("crew_ref", c.CrewRef), ("physical_status", "Nonsense"));
        Assert.Equal(PHYSICAL_ACTIVE, c.R.In(() => Dm.str(c.Record.physical_status)));

        // the notes: three are kept, the oldest goes first
        foreach (string note in new[] { "First Visit", "Second Visit", "Third Visit", "Fourth Visit" })
            Act(c.R, c.Bob, window, "add_note", ("crew_ref", c.CrewRef), ("content", note));
        var notes = c.R.In(() => Dm.each<datum_medical_note>(c.Record.medical_notes).Select(n => (Dm.str(n.content), Dm.str(n.author))).ToList());
        string bob = c.R.In(() => Dm.str(c.Bob.name));
        Assert.Equal(new[] { ("Second Visit", bob), ("Third Visit", bob), ("Fourth Visit", bob) }, notes);
        string noteRef = c.R.In(() => (string)REF(Dm.each<datum_medical_note>(c.Record.medical_notes).First()));
        Act(c.R, c.Bob, window, "delete_note", ("crew_ref", c.CrewRef), ("note_ref", noteRef));
        Assert.Equal(2, c.R.In(() => c.Record.medical_notes.len));

        // the blood type is one of the round's
        string blood = c.R.In(() => Dm.str(Dm.initial(Dm.each_cast<Type>(get_roundstart_blood_types()).Last(), "name"))); // the static data's list
        Act(c.R, c.Bob, window, "set_blood_type", ("crew_ref", c.CrewRef), ("blood_type", blood));
        Assert.Equal(blood, c.R.In(() => Dm.str(Dm.initial(c.Record.blood_type, "name"))));

        Act(c.R, c.Bob, window, "edit_field", ("ref", c.CrewRef), ("field", "species"), ("value", "Lizard"));
        Assert.Equal("Lizard", c.R.In(() => Dm.str(c.Record.species)));
        Act(c.R, c.Bob, window, "edit_field", ("ref", c.CrewRef), ("field", "no_such_var"), ("value", "x"));

        // off the station's levels the record stays
        Act(c.R, c.Bob, window, "expunge_record", ("crew_ref", c.CrewRef));
        Assert.True(c.Feed.Any("balloon: out of range!"), Diag(c));
        Assert.Equal("Lizard", c.R.In(() => Dm.str(c.Record.species)));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void Expunging_wipes_the_record_on_a_station_level_and_a_login_needs_access()
    {
        Room c = Make(3305, typeof(datum_id_trim_job_assistant), station: true);
        var console = Place<obj_machinery_computer_records_medical>(c.R, 4, 4);
        string window = Open(c, console, "MedicalRecords", out _);
        Act(c.R, c.Bob, window, "login");
        Assert.Equal(FALSE, c.R.In(() => Dm.num(console.authenticated)));
        Assert.True(c.Feed.Any("balloon: access denied"), Diag(c));
        JsonElement data = Data(c.R, c.Bob, out window);
        Assert.Equal(0, data.GetProperty("authenticated").GetDouble());

        c.R.In(() => Waiting.NoSleep(SSid_access.apply_trim_to_card((obj_item_card_id)c.Bob.wear_id, typeof(datum_id_trim_job_medical_doctor))));
        Act(c.R, c.Bob, window, "login");
        Assert.Equal(TRUE, c.R.In(() => Dm.num(console.authenticated)));
        Act(c.R, c.Bob, window, "expunge_record", ("crew_ref", c.CrewRef));
        Assert.True(c.Feed.Any("balloon: record expunged"), Diag(c));
        Assert.Equal(("Unknown", "Unknown", 18f), c.R.In(() => (Dm.str(c.Record.name), Dm.str(c.Record.species), Dm.num(c.Record.age))));
        Assert.Contains(c.Record, c.R.In(() => Dm.each<datum_record_crew>(GLOB.manifest.general).ToList())); // wiped, not deleted
        c.R.AssertNoRuntimes();
    }
}
