// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/_string_lists.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The four function-like #defines of this file are defined outside __DEFINES and never #undef'd: GlobalProcs members
// (rules 11.1), each argument evaluated once.

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/_string_lists.dm:1  #define pick_list(FILE, KEY) (pick(strings(FILE, KEY)))
    public static object pick_list(object FILE, object KEY)
    {
        return pick(strings(FILE, KEY));
    }

    // tg: code/__HELPERS/_string_lists.dm:2  #define pick_list_weighted(FILE, KEY) (pick_weight(strings(FILE, KEY)))
    public static object pick_list_weighted(object FILE, object KEY)
    {
        return pick_weight(strings(FILE, KEY) as DmList);
    }

    // tg: code/__HELPERS/_string_lists.dm:3  #define pick_list_replacements(FILE, KEY) (strings_replacement(FILE, KEY))
    public static object pick_list_replacements(object FILE, object KEY)
    {
        return strings_replacement(FILE, KEY);
    }

    // tg: code/__HELPERS/_string_lists.dm:4  #define json_load(FILE) (json_decode(file2text(FILE)))
    public static object json_load(object FILE)
    {
        return json_decode(file2text(FILE));
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/__HELPERS/_string_lists.dm:6  GLOBAL_LIST(string_cache)
    public DmList string_cache;

    // tg: code/__HELPERS/_string_lists.dm:6  (GLOBAL_UNMANAGED(string_cache), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalstring_cache()
    {
        return;
    }

    // tg: code/__HELPERS/_string_lists.dm:7  GLOBAL_VAR(string_filename_current_key)
    public object string_filename_current_key;

    // tg: code/__HELPERS/_string_lists.dm:7  (GLOBAL_UNMANAGED(string_filename_current_key), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalstring_filename_current_key()
    {
        return;
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/_string_lists.dm:10
    public static object strings_replacement(object filepath = null, object key = null)
    {
        filepath = sanitize_filepath(filepath);
        load_strings_file(filepath);

        if (Dm.@in(filepath, GLOB.string_cache) && Dm.@in(key, GLOB.string_cache[filepath]))
        {
            object response = pick(((DmList)GLOB.string_cache[filepath])[key]);
            regex needs_replacing = Dm.regex("@pick\\((\\D+?)\\)", "g");
            response = needs_replacing.Replace(response, "/proc/" + nameof(strings_subkey_lookup));
            return response;
        }
        else
            throw Dm.CRASH(Dm.text("strings list not found: []/[], index=[]", STRING_DIRECTORY, filepath, key), "code/__HELPERS/_string_lists.dm", 20);
    }

    // tg: code/__HELPERS/_string_lists.dm:22
    public static object strings(object filepath = null, object key = null, string directory = null)
    {
        directory ??= STRING_DIRECTORY;
        if (Dm.truthy(IsAdminAdvancedProcCall()))
            return null;

        filepath = sanitize_filepath(filepath);
        load_strings_file(filepath, directory);
        if (Dm.@in(filepath, GLOB.string_cache) && Dm.@in(key, GLOB.string_cache[filepath]))
            return ((DmList)GLOB.string_cache[filepath])[key];
        else
            throw Dm.CRASH(Dm.text("strings list not found: []/[], index=[]", directory, filepath, key), "code/__HELPERS/_string_lists.dm", 31);
    }

    // tg: code/__HELPERS/_string_lists.dm:33
    public static object strings_subkey_lookup(object match = null, object group1 = null)
    {
        return pick_list(GLOB.string_filename_current_key, group1);
    }

    // tg: code/__HELPERS/_string_lists.dm:36
    public static void load_strings_file(object filepath = null, string directory = null)
    {
        directory ??= STRING_DIRECTORY;
        if (Dm.truthy(IsAdminAdvancedProcCall()))
            return;

        GLOB.string_filename_current_key = filepath;
        if (Dm.@in(filepath, GLOB.string_cache))
            return; //no work to do

        if (!Dm.truthy(GLOB.string_cache))
            GLOB.string_cache = Dm.list();

        if (Dm.truthy(fexists(Dm.text("[]/[]", directory, filepath))))
            GLOB.string_cache[filepath] = json_load(Dm.text("[]/[]", directory, filepath));
        else
            throw Dm.CRASH(Dm.text("file not found: []/[]", directory, filepath), "code/__HELPERS/_string_lists.dm", 50);
    }
}
