// Translated from tgstation (snapshot 1d99bd7f): code/modules/detectivework/detective_scan_category.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298). The var blocks and the subtypes' vars are generated.

// tg: code/modules/detectivework/detective_scan_category.dm:1  GLOBAL_LIST_INIT_TYPED(detective_scan_categories, /datum/detective_scan_category, initialize_detective_scan_categories())
public partial class datum_controller_global_vars
{
    public DmList detective_scan_categories;

    // tg: code/modules/detectivework/detective_scan_category.dm:1  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldetective_scan_categories()
    {
        detective_scan_categories = initialize_detective_scan_categories() as DmList;
        Dm.append(ref gvars_datum_init_order, nameof(detective_scan_categories));
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/detectivework/detective_scan_category.dm:3
    public static object initialize_detective_scan_categories()
    {
        DmList categories = Dm.list();
        foreach (object category_path in Dm.each(subtypesof(typeof(datum_detective_scan_category))))
        {
            datum_detective_scan_category category = Dm.@new((Type)category_path) as datum_detective_scan_category;
            datum_detective_scan_category existing_category = categories[category.id] as datum_detective_scan_category;
            if (existing_category != null)
            {
                stack_trace(Dm.text("`[]` has duplicate id - `[]` of `[]`", category_path, category.id, existing_category.type), "modules/detectivework/detective_scan_category.dm", 0);
                continue;
            }

            categories[category.id] = category;
        }

        return categories;
    }

    // tg: code/modules/detectivework/detective_scanner_data/detective_scanner_data_entry.dm:1
    public static object cmp_detective_scanner_data_entry(datum_detective_scanner_data_entry a = null, datum_detective_scanner_data_entry b = null)
    {
        return cmp_numeric_asc(a.display_order, b.display_order);
    }
}

// tg: code/modules/detectivework/detective_scan_category.dm:17
public partial class datum_detective_scan_category : datum
{
    /// Generates report data used in `/datum/detective_scanner_log/proc/generate_report_text()`
    // tg: code/modules/detectivework/detective_scan_category.dm:28
    public virtual object generate_report_data(DmList data = null)
    {
        DmList report_text = Dm.list();
        Dm.append(ref report_text, Dm.text("<dt><b>[]</b></dt><dd>", name));

        foreach (object entry in Dm.each(data))
            Dm.append(ref report_text, format_report_entry(entry, data[entry]));

        Dm.append(ref report_text, "</dd>");
        return report_text;
    }

    /// Formats entered log data. Can be used to have unique formating per each category
    // tg: code/modules/detectivework/detective_scan_category.dm:41
    public virtual object format_report_entry(object entry = null, object entry_associated_value = null)
    {
        return Dm.text("[]<br>", entry);
    }
}

// tg: code/modules/detectivework/detective_scan_category.dm:52
public partial class datum_detective_scan_category_blood : datum_detective_scan_category
{
    // tg: code/modules/detectivework/detective_scan_category.dm:58
    public override object format_report_entry(object entry = null, object entry_associated_value = null)
    {
        return Dm.text("[], []<br>", entry, entry_associated_value);
    }
}

// tg: code/modules/detectivework/detective_scan_category.dm:69
public partial class datum_detective_scan_category_drink : datum_detective_scan_category
{
    // tg: code/modules/detectivework/detective_scan_category.dm:75
    public override object format_report_entry(object entry = null, object entry_associated_value = null)
    {
        return Dm.text("<b>[]</b>: [] u.<br>", entry, entry_associated_value);
    }
}

// tg: code/modules/detectivework/detective_scan_category.dm:79
public partial class datum_detective_scan_category_access : datum_detective_scan_category
{
    // tg: code/modules/detectivework/detective_scan_category.dm:85
    public override object format_report_entry(object entry = null, object entry_associated_value = null)
    {
        return Dm.text("<b>[]</b>: []<br>", entry, entry_associated_value);
    }
}

// tg: code/modules/detectivework/detective_scanner_data/detective_scanner_data_entry.dm:6
public partial class datum_detective_scanner_data_entry : datum
{
    // GAP(types): tg's `var/category` (:8) holds the category's id, a string; the generated var is a type.
    public object category_id;

    // tg: code/modules/detectivework/detective_scanner_data/detective_scanner_data_entry.dm:12
    public override async DmTask New(params object[] __extra)
    {
        await base.New();
        category_id = __extra.Length > 0 ? __extra[0] : null;
        display_order = __extra.Length > 1 ? Dm.num(__extra[1]) : 0;
        object new_data = __extra.Length > 2 ? __extra[2] : null;
        if (new_data != null)
            Dm.append(ref data, new_data);
    }

    // tg: code/modules/detectivework/detective_scanner_data/detective_scanner_data_entry.dm:18
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList ui_data = Dm.list();
        ui_data["category"] = category_id;
        ui_data["data"] = data;
        return ui_data;
    }

    // tg: code/modules/detectivework/detective_scanner_data/detective_scanner_data_entry.dm:24
    public virtual void add_data(object new_data = null)
    {
        Dm.append(ref data, new_data);
    }
}

// tg: code/modules/detectivework/detective_scanner_data/detective_scanner_log_entry.dm:3
public partial class datum_detective_scanner_log : datum
{
    // tg: code/modules/detectivework/detective_scanner_data/detective_scanner_log_entry.dm:13
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList ui_data = Dm.list();
        ui_data["scanTarget"] = scan_target;
        ui_data["scanTime"] = scan_time;

        sort_data_entries();
        DmList data_entries_ui_data = Dm.list();
        foreach (object key in Dm.each(data_entries))
        {
            datum_detective_scanner_data_entry entry = data_entries[key] as datum_detective_scanner_data_entry;
            data_entries_ui_data.Add(Dm.list(await entry.ui_data(user))); // UNTYPED_LIST_ADD
        }

        ui_data["dataEntries"] = data_entries_ui_data;
        return ui_data;
    }

    /// Adds new data entry to `data_entries` or updates existing one
    /// Entries will be not sorted after using it
    /// Returns TRUE if `data_entries` can be unsorted
    // tg: code/modules/detectivework/detective_scanner_data/detective_scanner_log_entry.dm:30
    public virtual void add_data_entry(object scan_category_id = null, object data = null)
    {
        datum_detective_scan_category category = GLOB.detective_scan_categories[scan_category_id] as datum_detective_scan_category;
        if (category == null)
        {
            stack_trace(Dm.text("scan_category_id - `[]` with no corresponding `/datum/detective_scan_category`", scan_category_id), "modules/detectivework/detective_scan_category.dm", 0);
            category = GLOB.detective_scan_categories[DETSCAN_CATEGORY_NOTES] as datum_detective_scan_category;
        }

        datum_detective_scanner_data_entry data_entry = data_entries[category.id] as datum_detective_scanner_data_entry;
        if (data_entry != null)
        {
            data_entry.add_data(data);
            return;
        }

        data_entries[scan_category_id] = Dm.@new<datum_detective_scanner_data_entry>(scan_category_id, category.display_order, data);
        sorted = length(data_entries) <= 1 ? TRUE : FALSE;
    }

    /// Sorts the `data_entries` list if it's considered not sorted
    // tg: code/modules/detectivework/detective_scanner_data/detective_scanner_log_entry.dm:45
    public virtual void sort_data_entries()
    {
        if (!Dm.truthy(sorted))
        {
            sortTim(data_entries, "/proc/" + nameof(GlobalProcs.cmp_detective_scanner_data_entry), TRUE);
            sorted = TRUE;
        }
    }

    /// Return text that will be used in printed paper report
    /// Called in `/obj/item/detective_scanner/proc/print_report()`
    // tg: code/modules/detectivework/detective_scanner_data/detective_scanner_log_entry.dm:52
    public virtual object generate_report_text()
    {
        DmList report_text = Dm.list();
        Dm.append(ref report_text, Dm.text("<h2>[] scan at []</h2><dr>", capitalize(scan_target), scan_time));
        if (length(data_entries) == 0)
        {
            Dm.append(ref report_text, "No forensic traces found.");
        }
        else
        {
            sort_data_entries();
            foreach (object log_category in Dm.each(data_entries))
            {
                datum_detective_scanner_data_entry data_entry_datum = data_entries[log_category] as datum_detective_scanner_data_entry;
                Dm.append(ref report_text, (GLOB.detective_scan_categories[log_category] as datum_detective_scan_category).generate_report_data(data_entry_datum.data));
            }
        }

        Dm.append(ref report_text, "</dl><hr>");

        return report_text;
    }
}
