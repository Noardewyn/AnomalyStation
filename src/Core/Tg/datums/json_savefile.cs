// Translated from tgstation (snapshot 1d99bd7f): code/datums/json_savefile.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F: the preferences' savefile): the tree, its entries, load and save through the world's file seam (rust-g's
// file_read and file_write). Not translated: serialize_list (the admin serialization), import_byond_savefile (the BYOND savefile
// migration; no BYOND savefiles exist) and the JSON export to the client (ftp, an admin and player download).

/**
 * A savefile implementation that handles all data using json.
 * Also saves it using JSON too, fancy.
 * If you pass in a null path, it simply acts as a memory tree instead, and cannot be saved.
 */
// tg: code/datums/json_savefile.dm:6
[DmPath("/datum/json_savefile")]
public partial class datum_json_savefile : datum
{
    // tg: code/datums/json_savefile.dm:16
    public override async DmTask New(params object[] __extra)
    {
        object path = __extra.Length > 0 ? __extra[0] : null;
        this.path = Dm.str(path);
        tree = Dm.list();
        if (Dm.truthy(path) && Dm.truthy(fexists(path)))
            load();
    }

    /**
     * Gets an entry from the json tree, with an optional default value.
     * If no key is specified it throws the entire tree at you instead
     */
    // tg: code/datums/json_savefile.dm:26
    public virtual object get_entry(object key = null, object default_value = null)
    {
        if (!Dm.truthy(key))
            return tree;
        return Dm.@in(key, tree) ? tree[key] : default_value;
    }

    /// Sets an entry in the tree to the given value
    // tg: code/datums/json_savefile.dm:32
    public virtual void set_entry(object key = null, object value = null)
    {
        tree[key] = value;
        if (Dm.truthy(auto_save))
            save();
    }

    /// Removes the given key from the tree
    // tg: code/datums/json_savefile.dm:38
    public virtual void remove_entry(object key = null)
    {
        if (Dm.truthy(key))
            Dm.remove(ref tree, key);
        if (Dm.truthy(auto_save))
            save();
    }

    /// Wipes the entire tree
    // tg: code/datums/json_savefile.dm:45
    public virtual void wipe()
    {
        tree?.Cut();
    }

    // tg: code/datums/json_savefile.dm:48
    public virtual object load()
    {
        if (!Dm.truthy(path) || !Dm.truthy(fexists(path)))
            return FALSE;
        try
        {
            tree = Dm.json_decode(rustg_file_read(path)) as DmList ?? Dm.list();
            return TRUE;
        }
        catch (DmRuntimeException err)
        {
            stack_trace(Dm.text("failed to load json savefile at '[]': []", path, err.Message), "code/datums/json_savefile.dm", 54);
            return FALSE;
        }
    }

    // tg: code/datums/json_savefile.dm:57
    public virtual void save()
    {
        if (Dm.truthy(path))
            rustg_file_write(Dm.json_encode(tree, JSON_PRETTY_PRINT), path);
    }
}
