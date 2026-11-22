// Translated from tgstation (snapshot 1d99bd7f): code/datums/records/manifest.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: the crew manifest): GLOB.manifest, a joining crew member's records, the manifest by department, its
// html and its CrewManifest window; M10 slice F: the round start's build; gaps SC2: change_pictures recreates the records' photos.
// Left out with a comment or a marked leaf: the blackbox's roundstart manifest (the database). The var block is generated (D111).

// tg: code/datums/records/manifest.dm:1  GLOBAL_DATUM_INIT(manifest, /datum/manifest, new)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/datums/records/manifest.dm:1  (GLOBAL_RAW(/datum/manifest/manifest))
    public datum_manifest manifest;

    // tg: code/datums/records/manifest.dm:1  (GLOBAL_MANAGED(manifest, new /datum/manifest), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmanifest()
    {
        manifest = Dm.@new<datum_manifest>();
        Dm.append(ref gvars_datum_init_order, "manifest");
    }
}

/** Stores crew records. */
// tg: code/datums/records/manifest.dm:4
public partial class datum_manifest : datum
{
    /// Builds the list of crew records for all crew members.
    // tg: code/datums/records/manifest.dm:13
    public virtual void build()
    {
        // List of mobs we want to mass insert into the manifest table at round start
        DmList players_to_log = Dm.list();
        foreach (mob_dead_new_player readied_player in Dm.each<mob_dead_new_player>(GLOB.new_player_list))
        {
            if (readied_player.new_character != null)
            {
                log_manifest(readied_player.ckey, readied_player.new_character.mind, readied_player.new_character);
                players_to_log[readied_player.ckey] = readied_player.new_character;
            }
            if (ishuman(readied_player.new_character))
                inject(readied_player.new_character as mob_living_carbon_human);
            CHECK_TICK();
        }
        // if(length(players_to_log)) SSblackbox.ReportRoundstartManifest(players_to_log): the database's manifest table (SSdbcore, M10+)
    }

    /// Gets the current manifest.
    // tg: code/datums/records/manifest.dm:27
    public virtual DmList get_manifest()
    {
        // First we build up the order in which we want the departments to appear in.
        DmList manifest_out = Dm.list();
        foreach (datum_job_department department in Dm.each<datum_job_department>(SSjob.joinable_departments))
            manifest_out[department.department_name] = Dm.list();
        manifest_out[DEPARTMENT_UNASSIGNED] = Dm.list();

        DmList departments_by_type = SSjob.joinable_departments_by_type;
        foreach (datum_record_crew target in Dm.each<datum_record_crew>(GLOB.manifest.general))
        {
            string name = target.name;
            object rank = target.rank; // user-visible job
            object trim = target.trim; // internal jobs by trim type
            datum_job job = SSjob.get_job(trim) as datum_job;
            if (job == null || !Dm.truthy(Dm.bitand(job.job_flags, JOB_CREW_MANIFEST)) || !Dm.truthy(LAZYLEN(job.departments_list))) // In case an unlawful custom rank is added.
            {
                DmList misc_list = manifest_out[DEPARTMENT_UNASSIGNED] as DmList;
                misc_list.len++;
                misc_list[misc_list.len] = Dm.list(
                    Dm.kv("name", name),
                    Dm.kv("rank", rank),
                    Dm.kv("trim", job?.tgui_icon));
                continue;
            }
            foreach (object department_type in Dm.each(job.departments_list))
            {
                //Jobs under multiple departments should only be displayed if this is their first department or the command department
                if (!Dm.eq(job.departments_list[1], department_type) && !Dm.truthy(Dm.bitand(job.departments_bitflags, DEPARTMENT_BITFLAG_COMMAND)))
                    continue;
                datum_job_department department = departments_by_type[department_type] as datum_job_department;
                if (department == null)
                {
                    stack_trace(Dm.text("get_manifest() failed to get job department for [] of []", department_type, job.GetType()), "code/datums/records/manifest.dm", 52);
                    continue;
                }
                DmList entry = Dm.list(
                    Dm.kv("name", name),
                    Dm.kv("rank", rank),
                    Dm.kv("trim", job.tgui_icon));
                DmList department_list = manifest_out[department.department_name] as DmList;
                if (department.department_head != null && department.department_head.IsInstanceOfType(job))
                {
                    department_list.Insert(1, null);
                    department_list[1] = entry;
                }
                else
                {
                    department_list.len++;
                    department_list[department_list.len] = entry;
                }
            }
        }

        // Trim the empty categories.
        foreach (object department in Dm.each(manifest_out.Copy()))
        {
            if (Dm.length(manifest_out[department]) == 0)
                Dm.remove(ref manifest_out, department);
        }

        return manifest_out;
    }

    /// Returns the manifest as an html.
    // tg: code/datums/records/manifest.dm:76
    public virtual string get_html(object monochrome = null)
    {
        bool mono = Dm.truthy(monochrome);
        DmList manifest = get_manifest();
        string dat = Dm.text(@"
	<head><style>
		.manifest {border-collapse:collapse;}
		.manifest td, th {border:1px solid []; padding:.25em}
		.manifest th {height: 2em; []}
		.manifest tr.head th { [] }
		.manifest tr.alt td {[]}
	</style></head>
	<table class=""manifest"" width='350px'>
	<tr class='head'><th>Name</th><th>Rank</th></tr>
	",
            mono ? "black" : "#DEF; background-color:white; color:black",
            mono ? "border-top-width: 3px" : "background-color: #48C; color:white",
            mono ? "border-top-width: 1px" : "background-color: #488;",
            mono ? "border-top-width: 2px" : "background-color: #DEF");
        foreach (object department in Dm.each(manifest))
        {
            DmList entries = manifest[department] as DmList;
            dat += Dm.text("<tr><th colspan=3>[]</th></tr>", department);
            //JUST
            bool even = false;
            foreach (object entry in Dm.each(entries))
            {
                DmList entry_list = entry as DmList;
                dat += Dm.text("<tr[]><td>[]</td><td>[]</td></tr>", even ? " class='alt'" : "", entry_list["name"], entry_list["rank"]);
                even = !even;
            }
        }

        dat += "</table>";
        dat = Dm.str(replacetext(dat, "\n", ""));
        dat = Dm.str(replacetext(dat, "\t", ""));
        return dat;
    }

    /// Injects a record into the manifest.
    // tg: code/datums/records/manifest.dm:106  (set waitfor = FALSE: a detached body, waiting-model.md 5.4)
    public virtual void inject(mob_living_carbon_human person = null, atom appearance_proxy = null)
    {
        Waiting.Detach(inject__body(person, appearance_proxy));
    }

    private async DmTask inject__body(mob_living_carbon_human person, atom appearance_proxy)
    {
        if (!Dm.truthy(Dm.bitand(person.mind?.assigned_role?.job_flags, JOB_CREW_MANIFEST)))
            return;

        // Attempt to get assignment from ID, otherwise default to mind.
        obj_item_card_id id_card = person.get_idcard(hand_first: FALSE) as obj_item_card_id;
        object assignment = id_card?.get_trim_assignment();
        if (!Dm.truthy(assignment))
            assignment = person.mind.assigned_role.title;

        mutable_appearance character_appearance = Dm.@new<mutable_appearance>(appearance_proxy != null ? appearance_proxy.appearance : person.appearance);
        string person_gender = "Other";
        if (Dm.eq(person.gender, "male"))
            person_gender = "Male";
        if (Dm.eq(person.gender, "female"))
            person_gender = "Female";
        datum_dna_stored record_dna = Dm.@new<datum_dna_stored>();
        await person.dna.copy_dna(record_dna);

        object blood_type_name = (person.get_bloodtype() as datum_blood_type)?.name;
        datum_record_locked lockfile = Dm.@new<datum_record_locked>(
            Dm.named("age", person.age),
            Dm.named("blood_type", Dm.truthy(blood_type_name) ? blood_type_name : "UNKNOWN"),
            Dm.named("character_appearance", character_appearance),
            Dm.named("dna_string", record_dna.unique_enzymes),
            Dm.named("fingerprint", md5(record_dna.unique_identity)),
            Dm.named("gender", person.gender),
            Dm.named("initial_rank", assignment),
            Dm.named("name", person.real_name),
            Dm.named("rank", assignment),
            Dm.named("species", (record_dna.species as datum_species)?.name),
            Dm.named("trim", assignment),
            // Locked specifics
            Dm.named("locked_dna", record_dna),
            Dm.named("mind_ref", person.mind));

        Dm.@new<datum_record_crew>(
            Dm.named("age", person.age),
            Dm.named("blood_type", Dm.truthy(blood_type_name) ? blood_type_name : "UNKNOWN"),
            Dm.named("character_appearance", character_appearance),
            Dm.named("dna_string", record_dna.unique_enzymes),
            Dm.named("fingerprint", md5(record_dna.unique_identity)),
            Dm.named("gender", person_gender),
            Dm.named("initial_rank", assignment),
            Dm.named("name", person.real_name),
            Dm.named("rank", assignment),
            Dm.named("species", (record_dna.species as datum_species)?.name),
            Dm.named("trim", assignment),
            // Crew specific
            Dm.named("lock_ref", REF(lockfile)),
            Dm.named("major_disabilities", person.get_quirk_string(FALSE, CAT_QUIRK_MAJOR_DISABILITY, from_scan: TRUE)),
            Dm.named("major_disabilities_desc", person.get_quirk_string(TRUE, CAT_QUIRK_MAJOR_DISABILITY)),
            Dm.named("minor_disabilities", person.get_quirk_string(FALSE, CAT_QUIRK_MINOR_DISABILITY, from_scan: TRUE)),
            Dm.named("minor_disabilities_desc", person.get_quirk_string(TRUE, CAT_QUIRK_MINOR_DISABILITY)),
            Dm.named("quirk_notes", person.get_quirk_string(TRUE, CAT_QUIRK_NOTES)));
    }

    /// Edits the rank and trim of the found record.
    // tg: code/datums/records/manifest.dm:163
    public virtual void modify(object name = null, object assignment = null, object trim = null)
    {
        if (!(find_record(name) is datum_record_crew target))
            return;

        target.rank = assignment;
        target.trim = trim;
    }

    ///Removes a record based on its name.
    // tg: code/datums/records/manifest.dm:172
    public virtual void remove(object name = null)
    {
        if (!(find_record(name) is datum_record_crew target))
            return;
        Dm.remove(ref general, target);
        qdel(target);
    }

    /**
     * Using the name to find the record, and person in reference to the body, we recreate photos for the manifest (and records).
     * Args:
     * - name - The name of the record we're looking for, which should be the name of the person.
     * - person - The mob we're taking pictures of to update the records.
     * - add_height_chart - If we should add a height chart to the background of the photo.
     */
    // tg: code/datums/records/manifest.dm:186
    public virtual object change_pictures(object name = null, mob_living person = null, object add_height_chart = null)
    {
        if (!(find_record(name) is datum_record_crew target))
            return FALSE;

        target.character_appearance = Dm.@new<mutable_appearance>(person.appearance);
        target.recreate_manifest_photos(add_height_chart);
        return TRUE;
    }

    // tg: code/datums/records/manifest.dm:195
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/datums/records/manifest.dm:198
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        return (isnewplayer(user) || isobserver(user) || isAI(user) || ispAI(user) || user.client?.holder != null) ? UI_INTERACTIVE : UI_CLOSE;
    }

    // tg: code/datums/records/manifest.dm:201
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "CrewManifest");
            await ui.open();
        }
        return null;
    }

    // tg: code/datums/records/manifest.dm:207
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList positions = Dm.list();
        foreach (datum_job_department department in Dm.each<datum_job_department>(SSjob.joinable_departments))
        {
            float open = 0;
            DmList exceptions = Dm.list();
            foreach (datum_job job in Dm.each<datum_job>(department.department_jobs))
            {
                if (job.total_positions == -1)
                {
                    Dm.append(ref exceptions, job.title);
                    continue;
                }
                float open_slots = job.total_positions - Dm.num(job.current_positions);
                if (open_slots < 1)
                    continue;
                open += open_slots;
            }
            positions[department.department_name] = Dm.list(Dm.kv("exceptions", exceptions), Dm.kv("open", open), Dm.kv("color", department.ui_color));
        }

        return Dm.list(
            Dm.kv("manifest", get_manifest()),
            Dm.kv("positions", positions));
    }
}
