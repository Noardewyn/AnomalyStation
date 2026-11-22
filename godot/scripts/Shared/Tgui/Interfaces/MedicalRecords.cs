using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MedicalRecords/ (index.tsx, RecordTabs.tsx,
// RecordView.tsx, NoteKeeper.tsx, constants.ts, helpers.ts; gaps SC2, D333: the medical records console). The login screen, the
// crew's records filtered and sorted by name, the selected record with its character preview (a 2D picture, D284), its notes, its
// editable fields, blood type, physical and mental statuses and its disabilities and quirks. Engine-free. The selected record and note
// are the window's local state (tg's useLocalState); the search filters as it is typed.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MedicalRecords
{
    // tg: MedicalRecords/constants.ts:1
    private static readonly Dictionary<string, string> PhysicalStatus2Icon = new()
    {
        ["Active"] = "person-running",
        ["Debilitated"] = "crutch",
        ["Unconscious"] = "moon-o",
        ["Deceased"] = "skull",
    };

    private static readonly Dictionary<string, string> PhysicalStatus2Color = new()
    {
        ["Active"] = "green",
        ["Debilitated"] = "purple",
        ["Unconscious"] = "orange",
        ["Deceased"] = "red",
    };

    private static readonly Dictionary<string, string> PhysicalStatus2Desc = new()
    {
        ["Active"] = "Active. Individual is conscious and healthy.",
        ["Debilitated"] = "Debilitated. Individual is conscious, but unhealthy.",
        ["Unconscious"] = "Unconscious. Individual may require medical attention.",
        ["Deceased"] = "Deceased. Individual has died and begun to decay.",
    };

    private static readonly Dictionary<string, string> MentalStatus2Icon = new()
    {
        ["Stable"] = "face-smile-o",
        ["Watch"] = "eye-o",
        ["Unstable"] = "scale-unbalanced-flip",
        ["Insane"] = "head-side-virus",
    };

    private static readonly Dictionary<string, string> MentalStatus2Color = new()
    {
        ["Stable"] = "green",
        ["Watch"] = "purple",
        ["Unstable"] = "orange",
        ["Insane"] = "red",
    };

    private static readonly Dictionary<string, string> MentalStatus2Desc = new()
    {
        ["Stable"] = "Stable. Individual is sane and free from psychological disorders.",
        ["Watch"] = "Watch. Individual has symptoms of mental illness. Monitor them closely.",
        ["Unstable"] = "Unstable. Individual has one or more mental illnesses.",
        ["Insane"] = "Insane. Individual exhibits severe, abnormal mental behaviors.",
    };

    private const string SelectedKey = "medicalRecord";
    private const string SelectedNoteKey = "selectedNote";
    private const string WritingKey = "note";

    // tg: MedicalRecords/index.tsx:8
    public static UiNode Render(TguiContext c)
    {
        bool authenticated = c.Data.Truthy("authenticated");
        return c.Window(750, 550, "Medical Records",
            new UiNode(UiKind.Stack) { Key = "root", Fill = true }.With(!authenticated ? new[] { UnauthorizedView(c) } : AuthView(c)));
    }

    // tg: MedicalRecords/index.tsx:24
    private static UiNode UnauthorizedView(TguiContext c) => new UiNode(UiKind.StackItem) { Key = "unauthorized", Grow = 1 }.With(
        new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "top", Grow = 1 },
            new UiNode(UiKind.StackItem) { Key = "logo", Align = "center", Grow = 2 }.With(
                new UiNode(UiKind.Icon) { Color = "teal", Icon = "staff-snake", Size = 15 }),
            new UiNode(UiKind.StackItem) { Key = "brand", Align = "center", Grow = 1 }.With(
                new UiNode(UiKind.Box) { Color = "good", FontSize = "18px", Bold = true, Text = "Nanotrasen HealthPRO" }),
            new UiNode(UiKind.StackItem) { Key = "loginRow" }.With(
                NoticeRow(null, "You are not logged in.",
                    new UiNode(UiKind.Button) { Key = "login", Icon = "lock-open", Text = "Login", OnClick = () => c.Act("login") }))));

    /// <summary>tg's right-aligned NoticeBox with its text and a button after it (the kit's notice with content: a row).</summary>
    public static UiNode NoticeRow(string color, string text, UiNode button) => new UiNode(UiKind.NoticeBox) { Color = color }.With(
        new UiNode(UiKind.Stack).With(
            new UiNode(UiKind.StackItem) { Key = "text", Grow = 1 }.With(new UiNode(UiKind.Box) { TextAlign = "right", Color = color != null ? "#ffffff" : "#000000", Text = text }),
            new UiNode(UiKind.StackItem) { Key = "button" }.With(button)));

    // tg: MedicalRecords/index.tsx:53
    private static UiNode[] AuthView(TguiContext c) => new[]
    {
        new UiNode(UiKind.StackItem) { Key = "tabs", Grow = 1 }.With(RecordTabs(c)),
        new UiNode(UiKind.StackItem) { Key = "view", Grow = 2 }.With(
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "recordView", Grow = 1 }.With(RecordView(c)),
                new UiNode(UiKind.StackItem) { Key = "logoutRow" }.With(
                    NoticeRow("info", "Secure Your Workspace.",
                        new UiNode(UiKind.Button) { Key = "logout", Icon = "lock", Color = "good", Text = "Log Out", OnClick = () => c.Act("logout") })))),
    };

    /// <summary>tg's getMedicalRecord (helpers.ts:10): the selected record as the data has it now, or null.</summary>
    public static UiValue SelectedRecord(TguiContext c)
    {
        string selected = c.UseState<string>(SelectedKey, null);
        if (selected == null)
            return null;
        return c.Data.Arr("records").FirstOrDefault(record => record.Str("crew_ref") == selected);
    }

    // tg: MedicalRecords/helpers.ts:6  string?.split('<br>') || []
    private static string[] QuirkStrings(UiValue value) =>
        value == null || value.IsNull || value.Text == null ? System.Array.Empty<string>() : value.Text.Split("<br>");

    /// <summary>tg's isRecordMatch (SecurityRecords/helpers.ts:33): the search in the name, the job, the fingerprint or the DNA.</summary>
    public static bool IsRecordMatch(UiValue record, string search)
    {
        if (string.IsNullOrEmpty(search))
            return true;
        string lower = search.ToLowerInvariant();
        foreach (string key in new[] { "name", "rank", "fingerprint", "dna" })
        {
            UiValue value = record.Get(key);
            if (value != null && value.Kind == UiValueKind.String && value.Text.ToLowerInvariant().Contains(lower, System.StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    // tg: MedicalRecords/RecordTabs.tsx:20
    private static UiNode RecordTabs(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> records = data.Arr("records");
        bool station_z = data.Truthy("station_z");

        string errorMessage = records.Count == 0 ? "No records found." : "No match. Refine your search.";

        string search = c.UseState("search", "");

        // sortBy(filter(records, isRecordMatch), [(record) => record.name?.toLowerCase()])
        var sorted = records.Where(record => IsRecordMatch(record, search))
            .OrderBy(record => record.Str("name", null)?.ToLowerInvariant(), System.StringComparer.Ordinal).ToList();

        var tabs = new UiNode(UiKind.Tabs) { Key = "records", Vertical = true };
        if (sorted.Count == 0)
            tabs.With(new UiNode(UiKind.NoticeBox) { Key = "empty", Text = errorMessage });
        else
            for (int index = 0; index < sorted.Count; index++)
                tabs.With(CrewTab(c, sorted[index], index));

        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "searchRow" }.With(new UiNode(UiKind.Input)
            {
                Key = "search",
                Fluid = true,
                Tooltip = "Name/Job/DNA",
                TextValue = search,
                OnInput = value => c.SetState("search", value),
            }),
            new UiNode(UiKind.StackItem) { Key = "list", Grow = 1 }.With(
                new UiNode(UiKind.Section) { Key = "listSection", Fill = true, Scrollable = true }.With(tabs)),
            new UiNode(UiKind.StackItem) { Key = "buttons", Align = "center" }.With(
                new UiNode(UiKind.Stack) { Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "createItem" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "create",
                        Disabled = true,
                        Icon = "plus",
                        Tooltip = "Add new records by inserting a 1 by 1 meter photo into the terminal. You do not need this screen open.",
                        Text = "Create",
                    }),
                    new UiNode(UiKind.StackItem) { Key = "purgeItem" }.With(c.ButtonConfirm("purge", new UiNode(UiKind.Button)
                    {
                        Text = "Purge",
                        Icon = "trash",
                        Disabled = !station_z,
                        Tooltip = "Wipe all record data.",
                        OnClick = () => c.Act("purge_records"),
                    })))));
    }

    // tg: MedicalRecords/RecordTabs.tsx:84  CrewTab
    private static UiNode CrewTab(TguiContext c, UiValue record, int index)
    {
        string selected = c.UseState<string>(SelectedKey, null);
        string assigned_view = c.Data.Str("assigned_view");
        string crew_ref = record.Str("crew_ref");
        string name = record.Str("name");
        string trim = record.Str("trim");

        /** Sets the record to preview */
        void SelectRecord()
        {
            if (selected == crew_ref)
                c.SetState<string>(SelectedKey, null);
            else
            {
                // GOD, I REALLY HATE IT!
                // THIS FUCKING HACK NEEDED CAUSE "WINSET MAP"
                // MAKING UI DISAPPEAR, AND WE NEED RE-RENDER SHIT
                // AFTER BYOND DONE MAKING THEIR SHIT
                // Anyway... that's better than hack before
                // (the first act makes the preview's map view, the second gives it the record's look: update_preview, records.dm:120)
                if (selected == null)
                    c.Act("view_record", ("assigned_view", assigned_view), ("crew_ref", crew_ref));
                c.SetState(SelectedKey, crew_ref);
                c.Act("view_record", ("assigned_view", assigned_view), ("crew_ref", crew_ref));
            }
        }

        return new UiNode(UiKind.Tab)
        {
            Key = "record:" + index,
            Candystripe = true,
            Selected = selected == crew_ref,
            Icon = string.IsNullOrEmpty(trim) ? "question" : trim,
            Text = name,
            OnClick = SelectRecord,
        };
    }

    // tg: MedicalRecords/RecordView.tsx:30
    private static UiNode RecordView(TguiContext c)
    {
        UiValue foundRecord = SelectedRecord(c);
        if (foundRecord == null)
            return new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No record selected." };

        UiValue data = c.Data;
        string assigned_view = data.Str("assigned_view");
        IReadOnlyList<UiValue> physical_statuses = data.Arr("physical_statuses");
        IReadOnlyList<UiValue> mental_statuses = data.Arr("mental_statuses");
        bool station_z = data.Truthy("station_z");
        IReadOnlyList<UiValue> blood_types = data.Arr("blood_types");
        double min_age = data.Num("min_age");
        double max_age = data.Num("max_age");

        string blood_type = foundRecord.Str("blood_type", null);
        string crew_ref = foundRecord.Str("crew_ref");
        string dna = foundRecord.Str("dna");
        string gender = foundRecord.Str("gender");
        string physical_status = foundRecord.Str("physical_status");
        string cause_of_death = foundRecord.Str("cause_of_death");
        string mental_status = foundRecord.Str("mental_status");
        string name = foundRecord.Str("name");
        string rank = foundRecord.Str("rank");
        string species = foundRecord.Str("species");

        string[] minor_disabilities_array = QuirkStrings(foundRecord.Get("minor_disabilities"));
        string[] major_disabilities_array = QuirkStrings(foundRecord.Get("major_disabilities"));
        string[] quirk_notes_array = QuirkStrings(foundRecord.Get("quirk_notes"));

        string ageKey = "age:" + crew_ref;
        bool isValid = c.UseState(ageKey + ":valid", true);

        UiNode StatusButtons(string key, IReadOnlyList<UiValue> statuses, string current, Dictionary<string, string> colors, Dictionary<string, string> icons, Dictionary<string, string> descs, string action, string param)
        {
            var buttons = new UiNode(UiKind.Fragment) { Key = key };
            for (int index = 0; index < statuses.Count; index++)
            {
                string button = statuses[index].AsStr();
                bool isSelected = button == current;
                buttons.With(new UiNode(UiKind.Button)
                {
                    Key = key + ":" + index,
                    Color = isSelected ? colors.GetValueOrDefault(button) : "grey",
                    Height = "1.75rem",
                    Icon = icons.GetValueOrDefault(button),
                    TextAlign = "center",
                    Tooltip = descs.GetValueOrDefault(button) ?? "",
                    Width = "3.0rem",
                    Text = button.Length > 0 ? button[..1] : "",
                    OnClick = () => c.Act(action, ("crew_ref", crew_ref), (param, button)),
                });
            }
            return buttons;
        }

        UiNode Entries(string key, string[] entries)
        {
            var item = new UiNode(UiKind.Fragment) { Key = key };
            for (int index = 0; index < entries.Length; index++)
                item.With(new UiNode(UiKind.Box) { Key = key + ":" + index, Text = "• " + entries[index] });
            return item;
        }

        var list = new UiNode(UiKind.LabeledList) { Key = "fields" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Name" }.With(EditableText.Render(c, "name", crew_ref, name)),
            new UiNode(UiKind.LabeledItem) { Key = "job", Label = "Job" }.With(EditableText.Render(c, "job", crew_ref, rank)),
            new UiNode(UiKind.LabeledItem) { Key = "age", Label = "Age" }.With(new UiNode(UiKind.NumberInput)
            {
                Key = ageKey,
                MinValue = min_age,
                MaxValue = max_age,
                Value = foundRecord.Num("age"),
                OnChange = value => c.Local.SetQuiet(ageKey, value),
                OnValidation = valid => c.SetState(ageKey + ":valid", valid),
                OnEnter = () =>
                {
                    if (isValid)
                        c.Act("edit_field", ("field", "age"), ("ref", crew_ref), ("value", c.Local.Get(ageKey, foundRecord.Num("age"))));
                },
            }),
            new UiNode(UiKind.LabeledItem) { Key = "species", Label = "Species" }.With(EditableText.Render(c, "species", crew_ref, species)),
            new UiNode(UiKind.LabeledItem) { Key = "gender", Label = "Gender" }.With(EditableText.Render(c, "gender", crew_ref, gender)),
            new UiNode(UiKind.LabeledItem) { Key = "dna", Label = "DNA" }.With(EditableText.Render(c, "dna", crew_ref, dna, "good")),
            new UiNode(UiKind.LabeledItem) { Key = "blood", Label = "Blood Type", Color = "bad" }.With(new UiNode(UiKind.Dropdown)
            {
                Key = "blood_type",
                TextValue = blood_type,
                Options = blood_types.Select(type => type.AsStr()).ToArray(),
                Width = "6rem",
                OnSelected = value => c.Act("set_blood_type", ("crew_ref", crew_ref), ("blood_type", value)),
            }),
            new UiNode(UiKind.LabeledItem)
            {
                Key = "physical",
                Label = "Physical Status",
                Buttons = StatusButtons("physical_statuses", physical_statuses, physical_status, PhysicalStatus2Color, PhysicalStatus2Icon, PhysicalStatus2Desc, "set_physical_status", "physical_status"),
            }.With(new UiNode(UiKind.Box) { Color = PhysicalStatus2Color.GetValueOrDefault(physical_status), Text = physical_status }),
            physical_status == "Deceased"
                ? new UiNode(UiKind.LabeledItem) { Key = "death", Label = "Cause of Death" }.With(new UiNode(UiKind.Box).With(new UiNode(UiKind.Input)
                {
                    Key = "cause:" + crew_ref,
                    Fluid = true,
                    Tooltip = "Input Cause of Death...",
                    TextValue = cause_of_death,
                    OnInput = value => c.Act("set_cause_of_death", ("crew_ref", crew_ref), ("cause", value)),
                }))
                : null,
            new UiNode(UiKind.LabeledItem)
            {
                Key = "mental",
                Label = "Mental Status",
                Buttons = StatusButtons("mental_statuses", mental_statuses, mental_status, MentalStatus2Color, MentalStatus2Icon, MentalStatus2Desc, "set_mental_status", "mental_status"),
            }.With(new UiNode(UiKind.Box) { Color = MentalStatus2Color.GetValueOrDefault(mental_status), Text = mental_status }),
            new UiNode(UiKind.LabeledItem) { Key = "minor", Label = "Minor Disabilities" }.With(Entries("minor", minor_disabilities_array)),
            new UiNode(UiKind.LabeledItem) { Key = "major", Label = "Major Disabilities" }.With(Entries("major", major_disabilities_array)),
            new UiNode(UiKind.LabeledItem) { Key = "quirks", Label = "Quirks" }.With(Entries("quirks", quirk_notes_array)));

        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "top", Grow = 1 }.With(
                new UiNode(UiKind.Stack) { Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "preview" }.With(CharacterPreview.Render("100%", assigned_view)),
                    new UiNode(UiKind.StackItem) { Key = "notes", Grow = 1 }.With(NoteKeeper(c, foundRecord)))),
            new UiNode(UiKind.StackItem) { Key = "bottom", Grow = 1 }.With(new UiNode(UiKind.Section)
            {
                Key = "record",
                Title = name,
                Fill = true,
                Scrollable = true,
                Buttons = c.ButtonConfirm("delete", new UiNode(UiKind.Button)
                {
                    Icon = "trash",
                    Disabled = !station_z,
                    Tooltip = "Expunge record data.",
                    Text = "Delete",
                    OnClick = () => c.Act("expunge_record", ("crew_ref", crew_ref)),
                }),
            }.With(list)));
    }

    /** Small section for adding notes. Passes a ref and note to Byond. */
    // tg: MedicalRecords/NoteKeeper.tsx:18
    private static UiNode NoteKeeper(TguiContext c, UiValue foundRecord)
    {
        string crew_ref = foundRecord.Str("crew_ref");
        IReadOnlyList<UiValue> notes = foundRecord.Arr("notes");
        string selectedRef = c.UseState<string>(SelectedNoteKey, null);
        UiValue selectedNote = selectedRef == null ? null : notes.FirstOrDefault(note => note.Str("note_ref") == selectedRef);
        bool writing = c.UseState(WritingKey, false);

        void AddNote()
        {
            c.Act("add_note", ("crew_ref", crew_ref), ("content", c.Local.Get("input:noteText", "")));
            c.SetState("input:noteText", "");
            c.SetState(WritingKey, false);
        }

        void DeleteNote()
        {
            if (selectedNote == null)
                return;
            c.Act("delete_note", ("crew_ref", crew_ref), ("note_ref", selectedNote.Str("note_ref")));
            c.SetState<string>(SelectedNoteKey, null);
        }

        var section = new UiNode(UiKind.Section) { Key = "noteKeeper", Title = "Notes", Fill = true, Scrollable = true, Buttons = NoteTabs(c, notes, selectedRef, writing) };
        if (writing)
        {
            section.With(new UiNode(UiKind.TextArea)
            {
                Key = "noteText",
                Fluid = true,
                Height = "100%",
                MaxLength = 1024,
                OnEnter = AddNote,
            });
        }

        if (selectedNote != null)
        {
            section.With(
                new UiNode(UiKind.LabeledList) { Key = "note" }.With(
                    new UiNode(UiKind.LabeledItem)
                    {
                        Key = "author",
                        Label = "Author",
                        Buttons = new UiNode(UiKind.Button) { Key = "deleteNote", Color = "bad", Icon = "trash", OnClick = DeleteNote },
                    }.With(new UiNode(UiKind.Box) { Text = selectedNote.Str("author") }),
                    new UiNode(UiKind.LabeledItem) { Key = "time", Label = "Time" }.With(new UiNode(UiKind.Box) { Text = selectedNote.Str("time") })),
                new UiNode(UiKind.Box) { Key = "contentLabel", Color = "label", Text = "Content:" },
                // tg's BlockQuote
                new UiNode(UiKind.Box) { Key = "content", Color = "label", Wrap = true, Text = selectedNote.Str("content") });
        }
        return section;
    }

    /** Displays the notes with an add tab next to. */
    // tg: MedicalRecords/NoteKeeper.tsx:87
    private static UiNode NoteTabs(TguiContext c, IReadOnlyList<UiValue> notes, string selectedRef, bool writing)
    {
        var tabs = new UiNode(UiKind.Tabs) { Key = "noteTabs" };
        for (int index = 0; index < notes.Count; index++)
        {
            string noteRef = notes[index].Str("note_ref");
            tabs.With(new UiNode(UiKind.Tab)
            {
                Key = "note:" + index,
                Selected = selectedRef == noteRef,
                Text = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                // Selects or deselects a note.
                OnClick = () => c.SetState(SelectedNoteKey, selectedRef == noteRef ? null : noteRef),
            });
        }
        tabs.With(new UiNode(UiKind.Tab)
        {
            Key = "new",
            Icon = "plus",
            Text = "New",
            Selected = writing,
            Tooltip = "Add a new note. Press enter or escape to exit view.",
            // Sets the note to writing mode.
            OnClick = () =>
            {
                c.SetState(WritingKey, true);
                c.SetState<string>(SelectedNoteKey, null);
            },
        });
        return tabs;
    }
}
