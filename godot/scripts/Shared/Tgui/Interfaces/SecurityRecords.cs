using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SecurityRecords/ (index.tsx, RecordTabs.tsx,
// RecordView.tsx, CrimeWatcher.tsx, RecordPrint.tsx, constants.ts, helpers.ts, types.ts; gaps SC2, D333: the security records
// console). The login screen, the crew's records filtered and sorted by name in their wanted status's colour, the selected record with
// its character preview (a 2D picture, D284), its crimes and citations (edited, voided, a new one written), its wanted status buttons,
// its editable fields, and the print form of the rapsheet and the missing and wanted posters. Engine-free. The selected record, tab
// and print form are the window's local state (tg's useLocalState and useState).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class SecurityRecords
{
    // tg: SecurityRecords/constants.ts:1
    private static readonly Dictionary<string, string> CrimeStatus2Color = new()
    {
        ["Arrest"] = "bad",
        ["Discharged"] = "blue",
        ["Incarcerated"] = "average",
        ["Parole"] = "good",
        ["Suspected"] = "teal",
    };

    private static readonly Dictionary<string, string> CrimeStatus2Desc = new()
    {
        ["Arrest"] = "Arrest. Target must have valid crimes to set this status.",
        ["Discharged"] = "Discharged. Individual has been acquitted from wrongdoing.",
        ["Incarcerated"] = "Incarcerated. Individual is currently serving a sentence.",
        ["Parole"] = "Parole. Released from prison, but still under supervision.",
        ["Suspected"] = "Suspected. Monitor closely for criminal activity.",
    };

    // tg: SecurityRecords/types.ts:42  enum SECURETAB
    private const int TabCrimes = 0;
    private const int TabCitations = 1;
    private const int TabAdd = 2;

    // tg: SecurityRecords/types.ts:48  enum PRINTOUT
    private const string PrintMissing = "missing";
    private const string PrintRapsheet = "rapsheet";
    private const string PrintWanted = "wanted";

    private const string SelectedKey = "securityRecord";
    private const string PrintOpenKey = "printOpen";
    private const string SelectedTabKey = "selectedTab";

    // tg: SecurityRecords/index.tsx:9
    public static UiNode Render(TguiContext c)
    {
        bool authenticated = c.Data.Truthy("authenticated");
        return c.Window(750, 550, "Security Records",
            new UiNode(UiKind.Stack) { Key = "root", Fill = true }.With(!authenticated ? new[] { RestrictedView(c) } : AuthView(c)));
    }

    /** Unauthorized view. User can only log in with ID */
    // tg: SecurityRecords/index.tsx:24
    private static UiNode RestrictedView(TguiContext c) => new UiNode(UiKind.StackItem) { Key = "restricted", Grow = 1 }.With(
        new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "top", Grow = 1 },
            new UiNode(UiKind.StackItem) { Key = "logo", Align = "center", Grow = 2 }.With(
                new UiNode(UiKind.Icon) { Color = "average", Icon = "exclamation-triangle", Size = 15 }),
            new UiNode(UiKind.StackItem) { Key = "brand", Align = "center", Grow = 1 }.With(
                new UiNode(UiKind.Box) { Color = "red", FontSize = "18px", Bold = true, Text = "Nanotrasen SecurityHUB" }),
            new UiNode(UiKind.StackItem) { Key = "loginRow" }.With(
                MedicalRecords.NoticeRow(null, "You are not logged in.",
                    new UiNode(UiKind.Button) { Key = "login", Icon = "lock-open", Text = "Login", OnClick = () => c.Act("login") }))));

    /** Logged in view */
    // tg: SecurityRecords/index.tsx:54
    private static UiNode[] AuthView(TguiContext c) => new[]
    {
        new UiNode(UiKind.StackItem) { Key = "tabs", Grow = 1 }.With(RecordTabs(c)),
        new UiNode(UiKind.StackItem) { Key = "view", Grow = 2 }.With(
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "recordView", Grow = 1 }.With(RecordView(c)),
                new UiNode(UiKind.StackItem) { Key = "logoutRow" }.With(
                    MedicalRecords.NoticeRow("info", "Secure Your Workspace.",
                        new UiNode(UiKind.Button) { Key = "logout", Icon = "lock", Color = "good", Text = "Log Out", OnClick = () => c.Act("logout") })))),
    };

    /// <summary>tg's getSecurityRecord (helpers.ts:10): the selected record as the data has it now, or null.</summary>
    public static UiValue SelectedRecord(TguiContext c)
    {
        string selected = c.UseState<string>(SelectedKey, null);
        if (selected == null)
            return null;
        return c.Data.Arr("records").FirstOrDefault(record => record.Str("crew_ref") == selected);
    }

    // tg: SecurityRecords/helpers.ts:47
    public static string DefaultPrintHeader(string printType) => printType switch
    {
        PrintRapsheet => "Record",
        PrintWanted => "WANTED",
        PrintMissing => "MISSING",
        _ => null,
    };

    // tg: SecurityRecords/helpers.ts:59
    public static string DefaultPrintDescription(string name, string printType) => printType switch
    {
        PrintRapsheet => $"A standard security record for {name}.",
        PrintWanted => $"A poster declaring {name} to be a wanted criminal, wanted by Nanotrasen. Report any sightings to security immediately.",
        PrintMissing => $"A poster declaring {name} to be a missing individual, missed by Nanotrasen. Report any sightings to security immediately.",
        _ => null,
    };

    /** Tabs on left, with search bar */
    // tg: SecurityRecords/RecordTabs.tsx:20
    private static UiNode RecordTabs(TguiContext c)
    {
        UiValue data = c.Data;
        bool higher_access = data.Truthy("higher_access");
        IReadOnlyList<UiValue> records = data.Arr("records");
        bool station_z = data.Truthy("station_z");

        string errorMessage = records.Count == 0 ? "No records found." : "No match. Refine your search.";

        string search = c.UseState("search", "");

        // sortBy(filter(records, isRecordMatch), [(record) => record.name])
        var sorted = records.Where(record => MedicalRecords.IsRecordMatch(record, search))
            .OrderBy(record => record.Str("name", null), System.StringComparer.Ordinal).ToList();

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
                Tooltip = "Name/Job/Fingerprints",
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
                        Disabled = !higher_access || !station_z,
                        Icon = "trash",
                        Tooltip = "Wipe criminal record data.",
                        OnClick = () => c.Act("purge_records"),
                    })))));
    }

    /** Individual record */
    // tg: SecurityRecords/RecordTabs.tsx:84
    private static UiNode CrewTab(TguiContext c, UiValue record, int index)
    {
        string selected = c.UseState<string>(SelectedKey, null);
        string assigned_view = c.Data.Str("assigned_view");
        string crew_ref = record.Str("crew_ref");
        string name = record.Str("name");
        string trim = record.Str("trim");
        string wanted_status = record.Str("wanted_status");

        /** Chooses a record */
        void SelectRecord()
        {
            if (selected == crew_ref)
                c.SetState<string>(SelectedKey, null);
            else
            {
                // See MedicalRecords/RecordTabs.tsx for explanation
                if (selected == null)
                    c.Act("view_record", ("assigned_view", assigned_view), ("crew_ref", crew_ref));
                c.SetState(SelectedKey, crew_ref);
                c.Act("view_record", ("assigned_view", assigned_view), ("crew_ref", crew_ref));
            }
        }

        bool isSelected = selected == crew_ref;

        return new UiNode(UiKind.Tab)
        {
            Key = "record:" + index,
            Candystripe = true,
            Selected = isSelected,
            Bold = isSelected,
            Color = CrimeStatus2Color.GetValueOrDefault(wanted_status),
            Icon = string.IsNullOrEmpty(trim) ? "question" : trim,
            Text = name,
            OnClick = SelectRecord,
        };
    }

    /** Views a selected record. */
    // tg: SecurityRecords/RecordView.tsx:23
    private static UiNode RecordView(TguiContext c)
    {
        UiValue foundRecord = SelectedRecord(c);
        if (foundRecord == null)
            return new UiNode(UiKind.NoticeBox) { Key = "none", Text = "Nothing selected." };

        string assigned_view = c.Data.Str("assigned_view");
        bool open = c.UseState(PrintOpenKey, false);

        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "top", Grow = 1 }.With(
                new UiNode(UiKind.Stack) { Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "preview" }.With(CharacterPreview.Render("100%", assigned_view)),
                    new UiNode(UiKind.StackItem) { Key = "crimeWatcher", Grow = 1 }.With(CrimeWatcher(c, foundRecord)))),
            new UiNode(UiKind.StackItem) { Key = "bottom", Grow = 1 }.With(open ? RecordPrint(c, foundRecord) : RecordInfo(c, foundRecord)));
    }

    // tg: SecurityRecords/RecordView.tsx:48
    private static UiNode RecordInfo(TguiContext c, UiValue foundRecord)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> available_statuses = data.Arr("available_statuses");
        double min_age = data.Num("min_age");
        double max_age = data.Num("max_age");

        string crew_ref = foundRecord.Str("crew_ref");
        IReadOnlyList<UiValue> crimes = foundRecord.Arr("crimes");
        string fingerprint = foundRecord.Str("fingerprint");
        string gender = foundRecord.Str("gender");
        string name = foundRecord.Str("name");
        string note = foundRecord.Str("note");
        string rank = foundRecord.Str("rank");
        string species = foundRecord.Str("species");
        string wanted_status = foundRecord.Str("wanted_status");
        string voice = foundRecord.Str("voice");

        string ageKey = "age:" + crew_ref;
        bool isValid = c.UseState(ageKey + ":valid", true);

        bool hasValidCrimes = crimes.Any(crime => crime.Truthy("valid"));

        var statusButtons = new UiNode(UiKind.Fragment) { Key = "statuses" };
        for (int index = 0; index < available_statuses.Count; index++)
        {
            string button = available_statuses[index].AsStr();
            bool isSelected = button == wanted_status;
            statusButtons.With(new UiNode(UiKind.Button)
            {
                Key = "status:" + index,
                Color = isSelected ? CrimeStatus2Color.GetValueOrDefault(button) : "grey",
                Disabled = button == "Arrest" && !hasValidCrimes,
                Icon = isSelected ? "check" : "",
                Tooltip = CrimeStatus2Desc.GetValueOrDefault(button) ?? "",
                Text = button.Length > 0 ? button[..1] : "",
                OnClick = () => c.Act("set_wanted", ("crew_ref", crew_ref), ("status", button)),
            });
        }

        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "statusRow", Grow = 1 }.With(new UiNode(UiKind.Section)
            {
                Key = "statusSection",
                Fill = true,
                Title = name,
                Color = CrimeStatus2Color.GetValueOrDefault(wanted_status),
                Buttons = new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem) { Key = "printItem" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "print",
                        Height = "1.7rem",
                        Icon = "print",
                        Tooltip = "Print a rapsheet or poster.",
                        Text = "Print",
                        OnClick = () => c.SetState(PrintOpenKey, true),
                    }),
                    new UiNode(UiKind.StackItem) { Key = "deleteItem" }.With(c.ButtonConfirm("delete", new UiNode(UiKind.Button)
                    {
                        Icon = "trash",
                        Tooltip = "Delete record data.",
                        Text = "Delete",
                        OnClick = () => c.Act("delete_record", ("crew_ref", crew_ref)),
                    }))),
            }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Key = "status", Label = "Status", Buttons = statusButtons }.With(
                    new UiNode(UiKind.Box) { Color = CrimeStatus2Color.GetValueOrDefault(wanted_status), Text = wanted_status })))),
            new UiNode(UiKind.StackItem) { Key = "fieldsRow", Grow = 2 }.With(
                new UiNode(UiKind.Section) { Key = "fieldsSection", Fill = true, Scrollable = true }.With(new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Name" }.With(EditableText.Render(c, "name", crew_ref, name)),
                    new UiNode(UiKind.LabeledItem) { Key = "job", Label = "Job" }.With(EditableText.Render(c, "rank", crew_ref, rank)),
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
                            // tg sends the record as crew_ref here, where edit_field reads ref (records.dm:39): the age stays
                            if (isValid)
                                c.Act("edit_field", ("crew_ref", crew_ref), ("field", "age"), ("value", c.Local.Get(ageKey, foundRecord.Num("age"))));
                        },
                    }),
                    new UiNode(UiKind.LabeledItem) { Key = "species", Label = "Species" }.With(EditableText.Render(c, "species", crew_ref, species)),
                    new UiNode(UiKind.LabeledItem) { Key = "gender", Label = "Gender" }.With(EditableText.Render(c, "gender", crew_ref, gender)),
                    new UiNode(UiKind.LabeledItem) { Key = "fingerprint", Label = "Fingerprint", Color = "good" }.With(EditableText.Render(c, "fingerprint", crew_ref, fingerprint, "good")),
                    new UiNode(UiKind.LabeledItem) { Key = "voice", Label = "Voice" }.With(EditableText.Render(c, "voice", crew_ref, voice)),
                    new UiNode(UiKind.LabeledItem) { Key = "note", Label = "Note" }.With(EditableText.Render(c, "security_note", crew_ref, note))))));
    }

    /** Displays a list of crimes and allows to add new ones. */
    // tg: SecurityRecords/CrimeWatcher.tsx:24
    private static UiNode CrimeWatcher(TguiContext c, UiValue foundRecord)
    {
        IReadOnlyList<UiValue> crimes = foundRecord.Arr("crimes");
        IReadOnlyList<UiValue> citations = foundRecord.Arr("citations");
        int selectedTab = c.UseState(SelectedTabKey, TabCrimes);

        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "tabs" }.With(new UiNode(UiKind.Tabs) { Key = "crimeTabs", Fluid = true }.With(
                new UiNode(UiKind.Tab) { Key = "crimes", Selected = selectedTab == TabCrimes, Text = "Crimes: " + crimes.Count, OnClick = () => c.SetState(SelectedTabKey, TabCrimes) },
                new UiNode(UiKind.Tab) { Key = "citations", Selected = selectedTab == TabCitations, Text = "Citations: " + citations.Count, OnClick = () => c.SetState(SelectedTabKey, TabCitations) },
                new UiNode(UiKind.Tab) { Key = "add", Selected = selectedTab == TabAdd, Icon = "plus", Tooltip = "Add a new crime or citation", OnClick = () => c.SetState(SelectedTabKey, TabAdd) })),
            new UiNode(UiKind.StackItem) { Key = "list", Grow = 1 }.With(
                new UiNode(UiKind.Section) { Key = "crimeSection", Fill = true, Scrollable = true }.With(
                    selectedTab < TabAdd ? CrimeList(c, foundRecord, selectedTab) : CrimeAuthor(c, foundRecord))));
    }

    /** Displays the crimes and citations of a record. */
    // tg: SecurityRecords/CrimeWatcher.tsx:72
    private static UiNode CrimeList(TguiContext c, UiValue foundRecord, int tab)
    {
        IReadOnlyList<UiValue> toDisplay = tab == TabCrimes ? foundRecord.Arr("crimes") : foundRecord.Arr("citations");
        var stack = new UiNode(UiKind.Stack) { Key = "crimeList", Fill = true, Vertical = true };
        if (toDisplay.Count == 0)
            stack.With(new UiNode(UiKind.StackItem) { Key = "empty" }.With(
                new UiNode(UiKind.NoticeBox) { Text = "No " + (tab == TabCrimes ? "crimes" : "citations") + " found." }));
        else
            for (int index = 0; index < toDisplay.Count; index++)
                stack.With(CrimeDisplay(c, foundRecord, toDisplay[index], index));
        return stack;
    }

    /** Displays an individual crime */
    // tg: SecurityRecords/CrimeWatcher.tsx:96
    private static UiNode CrimeDisplay(TguiContext c, UiValue foundRecord, UiValue item, int index)
    {
        string crew_ref = foundRecord.Str("crew_ref");
        string current_user = c.Data.Str("current_user");
        bool higher_access = c.Data.Truthy("higher_access");
        string author = item.Str("author");
        string crime_ref = item.Str("crime_ref");
        string details = item.Str("details");
        UiValue fineValue = item.Get("fine");
        bool hasFine = fineValue != null; // fine !== undefined (a citation's; a crime has none)
        double fine = fineValue.AsNum();
        string name = item.Str("name");
        bool valid = item.Truthy("valid");
        string voider = item.Str("voider");
        bool fineDue = hasFine && !fineValue.IsNull && fine > 0;
        string showFine = fineDue ? ": " + TguiFormat.JsNumber(fine) + " cr" : ": PAID OFF";

        string collapsibleColor = "";
        if (!valid)
            collapsibleColor = "grey";
        else if (fineDue)
            collapsibleColor = "average";

        string displayTitle = name;
        if (hasFine)
            displayTitle = (name.Length > 18 ? name[..18] : name) + showFine;

        string editingKey = "editing_" + crime_ref;
        bool editing = c.UseState(editingKey, false);
        bool cannotEdit = !valid || (!higher_access && author != current_user);

        var list = new UiNode(UiKind.LabeledList) { Key = "info" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "time", Label = "Time" }.With(new UiNode(UiKind.Box) { Text = item.Str("time") }),
            new UiNode(UiKind.LabeledItem) { Key = "author", Label = "Author" }.With(new UiNode(UiKind.Box) { Text = author }),
            new UiNode(UiKind.LabeledItem) { Key = "status", Label = "Status", Color = !valid ? "bad" : "good" }.With(
                new UiNode(UiKind.Box) { Color = !valid ? "bad" : "good", Text = !valid ? "Void" : "Active" }),
            !valid
                ? new UiNode(UiKind.LabeledItem) { Key = "voider", Label = "Voided by", Color = !string.IsNullOrEmpty(voider) ? "gold" : "good" }.With(
                    new UiNode(UiKind.Box) { Color = !string.IsNullOrEmpty(voider) ? "gold" : "good", Text = string.IsNullOrEmpty(voider) ? "Automation" : voider })
                : null,
            fineDue
                ? new UiNode(UiKind.LabeledItem) { Key = "fine", Label = "Fine", Color = "bad" }.With(
                    new UiNode(UiKind.Box) { Color = "bad", Text = TguiFormat.JsNumber(fine) + "cr" }, new UiNode(UiKind.Icon) { Color = "gold", Icon = "coins" })
                : null,
            fineDue
                ? new UiNode(UiKind.LabeledItem) { Key = "paid", Label = "Paid", Color = "good" }.With(
                    new UiNode(UiKind.Box) { Color = "good", Text = item.Str("paid") + "cr" }, new UiNode(UiKind.Icon) { Color = "gold", Icon = "coins" })
                : null);

        var collapsible = new UiNode(UiKind.Collapsible) { Key = "crime:" + crime_ref, Color = collapsibleColor, Open = editing, Title = displayTitle }.With(
            list,
            new UiNode(UiKind.Box) { Key = "detailsLabel", Color = "label", Text = "Details:" },
            // tg's BlockQuote
            new UiNode(UiKind.Box) { Key = "details", Color = "label", Wrap = true, Text = details });

        if (!editing)
        {
            collapsible.With(new UiNode(UiKind.Box) { Key = "editRow" }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "edit",
                    Disabled = cannotEdit,
                    Icon = "pen",
                    Text = "Edit",
                    OnClick = () => c.SetState(editingKey, true),
                },
                c.ButtonConfirm("invalidate:" + crime_ref, new UiNode(UiKind.Button)
                {
                    Text = "Invalidate",
                    Disabled = cannotEdit,
                    Icon = "ban",
                    OnClick = () => c.Act("invalidate_crime", ("crew_ref", crew_ref), ("crime_ref", crime_ref)),
                })));
        }
        else
        {
            string nameKey = "crimeName:" + crime_ref;
            string descKey = "crimeDesc:" + crime_ref;
            collapsible.With(
                new UiNode(UiKind.Input)
                {
                    Key = nameKey,
                    Fluid = true,
                    MaxLength = 25,
                    Tooltip = "Enter a new name",
                    OnEnter = () =>
                    {
                        c.SetState(editingKey, false);
                        c.Act("edit_crime", ("crew_ref", crew_ref), ("crime_ref", crime_ref), ("name", c.Local.Get("input:" + nameKey, "")));
                    },
                },
                new UiNode(UiKind.Input)
                {
                    Key = descKey,
                    Fluid = true,
                    MaxLength = 1025,
                    Tooltip = "Enter a new description",
                    OnEnter = () =>
                    {
                        c.SetState(editingKey, false);
                        c.Act("edit_crime", ("crew_ref", crew_ref), ("crime_ref", crime_ref), ("description", c.Local.Get("input:" + descKey, "")));
                    },
                });
        }

        return new UiNode(UiKind.StackItem) { Key = "crime:" + index }.With(collapsible);
    }

    /** Writes a new crime. Reducers don't seem to work here, so... */
    // tg: SecurityRecords/CrimeWatcher.tsx:216
    private static UiNode CrimeAuthor(TguiContext c, UiValue foundRecord)
    {
        string crew_ref = foundRecord.Str("crew_ref");

        const string NameKey = "input:crimeName";
        const string DetailsKey = "input:crimeDetails";
        const string FineKey = "crimeFine";
        string crimeName = c.UseState(NameKey, "");
        string crimeDetails = c.UseState(DetailsKey, "");
        double crimeFine = c.UseState(FineKey, 0.0);
        bool crimeFineIsValid = c.UseState(FineKey + ":valid", true);

        bool nameMeetsReqs = crimeName.Length > 2;

        /** Resets form data since it persists.. */
        void Reset()
        {
            c.SetState(DetailsKey, "");
            c.SetState(FineKey, 0.0);
            c.SetState("input:" + FineKey, "0");
            c.SetState(NameKey, "");
            c.SetState(SelectedTabKey, crimeFine != 0 ? TabCitations : TabCrimes);
        }

        /** Sends form to backend */
        void CreateCrime()
        {
            if (string.IsNullOrEmpty(crimeName) || !crimeFineIsValid)
                return;
            c.Act("add_crime", ("crew_ref", crew_ref), ("details", crimeDetails), ("fine", crimeFine), ("name", crimeName));
            Reset();
        }

        return new UiNode(UiKind.Stack) { Key = "author", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "nameRow", Color = "label" }.With(
                new UiNode(UiKind.Box) { Color = "label", Text = "Name" },
                new UiNode(UiKind.Input)
                {
                    Key = "crimeName",
                    Fluid = true,
                    MaxLength = 25,
                    Tooltip = "Brief overview",
                    OnInput = _ => c.SetState(NameKey, c.Local.Get(NameKey, "")), // a redraw: the Create button reads the name
                }),
            new UiNode(UiKind.StackItem) { Key = "detailsRow", Color = "label" }.With(
                new UiNode(UiKind.Box) { Color = "label", Text = "Details" },
                new UiNode(UiKind.TextArea)
                {
                    Key = "crimeDetails",
                    Fluid = true,
                    Height = 4,
                    MaxLength = 1025,
                    Tooltip = "Type some details...",
                }),
            new UiNode(UiKind.StackItem) { Key = "fineRow", Color = "label" }.With(
                new UiNode(UiKind.Box) { Color = "label", Text = "Fine (leave blank to arrest)" },
                new UiNode(UiKind.NumberInput)
                {
                    Key = FineKey,
                    Fluid = true,
                    Value = crimeFine,
                    MinValue = 0,
                    MaxValue = 1000,
                    OnChange = value => c.SetState(FineKey, value),
                    OnValidation = valid => c.SetState(FineKey + ":valid", valid),
                }),
            new UiNode(UiKind.StackItem) { Key = "createItem" }.With(c.ButtonConfirm("createCrime", new UiNode(UiKind.Button)
            {
                Disabled = !nameMeetsReqs || !crimeFineIsValid,
                Icon = "plus",
                Tooltip = !nameMeetsReqs ? "Name must be at least 3 characters." : "",
                Text = "Create",
                OnClick = CreateCrime,
            })));
    }

    /** Handles printing posters and rapsheets */
    // tg: SecurityRecords/RecordPrint.tsx:14
    private static UiNode RecordPrint(TguiContext c, UiValue foundRecord)
    {
        string crew_ref = foundRecord.Str("crew_ref");
        string name = foundRecord.Str("name");
        bool innocent = foundRecord.Arr("crimes").Count == 0;

        // the form's fields are their inputs' text (the kit keeps it under "input:" and the key); closing clears them, as the form's
        // unmount does in tg
        const string AliasKey = "input:printAlias";
        const string HeaderKey = "input:printHeader";
        const string DescriptionKey = "input:printDescription";
        const string TypeKey = "printType";
        string alias = c.UseState(AliasKey, name);
        string printType = c.UseState(TypeKey, PrintMissing);
        string header = c.UseState(HeaderKey, "");
        string description = c.UseState(DescriptionKey, "");

        /** Close everything and reset to blank. */
        void Reset()
        {
            c.SetState<string>(AliasKey, null);
            c.SetState<string>(HeaderKey, null);
            c.SetState<string>(DescriptionKey, null);
            c.SetState(TypeKey, PrintMissing);
            c.SetState(PrintOpenKey, false);
        }

        /** Prints the record and resets. */
        void PrintSheet()
        {
            c.Act("print_record", ("alias", alias), ("crew_ref", crew_ref), ("desc", description), ("head", header), ("type", printType));
            Reset();
        }

        /** If they have the fields defaulted to a specific type, change the message */
        void SwapTabs(string tab)
        {
            if (description == DefaultPrintDescription(name, printType))
                c.SetState(DescriptionKey, DefaultPrintDescription(name, tab));
            if (header == DefaultPrintHeader(printType))
                c.SetState(HeaderKey, DefaultPrintHeader(tab));
            c.SetState(TypeKey, tab);
        }

        UiNode Reload(string key, System.Action clear) => new UiNode(UiKind.Button) { Key = key, Icon = "sync", Tooltip = "Reset", OnClick = clear };

        return new UiNode(UiKind.Section)
        {
            Key = "print",
            Title = "Print Record",
            Fill = true,
            Scrollable = true,
            Buttons = new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Button)
                {
                    Key = "missing",
                    Icon = "question",
                    Selected = printType == PrintMissing,
                    Tooltip = "Prints a poster with mugshot and description.",
                    Text = "Missing",
                    OnClick = () => SwapTabs(PrintMissing),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "rapsheet",
                    Disabled = innocent,
                    Icon = "file-alt",
                    Selected = printType == PrintRapsheet,
                    Tooltip = "Prints a standard paper with the record on it." + (innocent ? " (Requires crimes)" : ""),
                    Text = "Rapsheet",
                    OnClick = () => SwapTabs(PrintRapsheet),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "wanted",
                    Disabled = innocent,
                    Icon = "handcuffs",
                    Selected = printType == PrintWanted,
                    Tooltip = "Prints a poster with mugshot and crimes." + (innocent ? " (Requires crimes)" : ""),
                    Text = "Wanted",
                    OnClick = () => SwapTabs(PrintWanted),
                },
                new UiNode(UiKind.Button) { Key = "close", Color = "bad", Icon = "times", OnClick = Reset }),
        }.With(new UiNode(UiKind.Stack) { Key = "form", Color = "label", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "headerRow" }.With(
                new UiNode(UiKind.Box) { Text = "Enter a Header:" },
                new UiNode(UiKind.Input) { Key = "printHeader", MaxLength = 7, TextValue = header },
                Reload("resetHeader", () => c.SetState(HeaderKey, DefaultPrintHeader(printType)))),
            new UiNode(UiKind.StackItem) { Key = "aliasRow" }.With(
                new UiNode(UiKind.Box) { Text = "Enter an Alias:" },
                new UiNode(UiKind.Input) { Key = "printAlias", MaxLength = 42, TextValue = alias, Width = "55%" },
                Reload("resetAlias", () => c.SetState(AliasKey, name))),
            new UiNode(UiKind.StackItem) { Key = "descriptionRow" }.With(
                new UiNode(UiKind.Box) { Text = "Enter a Description:" },
                new UiNode(UiKind.Stack) { Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "field", Grow = 1 }.With(
                        new UiNode(UiKind.Input) { Key = "printDescription", Fluid = true, MaxLength = 150, TextValue = description }),
                    new UiNode(UiKind.StackItem) { Key = "reset" }.With(
                        Reload("resetDescription", () => c.SetState(DescriptionKey, DefaultPrintDescription(name, printType)))))),
            new UiNode(UiKind.StackItem) { Key = "actions" }.With(
                new UiNode(UiKind.Box) { TextAlign = "right" }.With(
                    new UiNode(UiKind.Button) { Key = "cancel", Color = "bad", Text = "Cancel", OnClick = () => c.SetState(PrintOpenKey, false) },
                    new UiNode(UiKind.Button) { Key = "doPrint", Color = "good", Text = "Print", OnClick = PrintSheet }))));
    }
}
