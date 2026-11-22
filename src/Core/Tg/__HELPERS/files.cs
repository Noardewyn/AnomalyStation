// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/files.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only sanitize_filepath translated (package D, wave 4, declared it; M6 slice 2 translated it with the shim's
// /regex): strings() and strings_replacement() (__HELPERS/_string_lists.dm) call it, and its SANITIZE_FILENAME
// (__DEFINES/text.dm:53) is GLOB.filename_forbidden_chars.Replace(text, ""), a /regex (_globalvars/_regexes.dm:22). The
// rest of files.dm comes with its users.

public static partial class GlobalProcs
{
    /**
     * Sanitizes the name of each node in the path.
     *
     * Im case you are wondering when to use this proc and when to use SANITIZE_FILENAME,
     *
     * You use SANITIZE_FILENAME to sanitize the name of a file [e.g. example.txt]
     *
     * You use sanitize_filepath sanitize the path of a file [e.g. root/node/example.txt]
     *
     * If you use SANITIZE_FILENAME to sanitize a file path things will break.
     */
    // tg: code/__HELPERS/files.dm:131
    public static object sanitize_filepath(object path = null)
    {
        object dot = "";
        string delimiter = "/"; //Very much intentionally hardcoded
        DmList all_nodes = splittext(path, delimiter);
        foreach (object node in Dm.each(all_nodes))
        {
            if (Dm.truthy(dot))
                dot = Dm.add(dot, delimiter); // Add the delimiter before each successive node.
            dot = Dm.add(dot, SANITIZE_FILENAME(node));
        }
        return dot;
    }
}

// tg: code/__HELPERS/files.dm:149 (interleaved types, rules 1.9; M10 slice F: the ticker's lobby music, IS_SOUND_FILE)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/files.dm:149
    public static object is_file_type_in_list(object file = null, DmList file_types = null)
    {
        file_types ??= Dm.list();
        float extstart = findlasttext(Dm.text("[]", file), ".");
        if (!Dm.truthy(extstart))
            return FALSE;
        object ext = copytext(Dm.text("[]", file), extstart + 1);
        if (Dm.truthy(file_types[ext]))
            return TRUE;
        return null;
    }
}

// tg: code/__HELPERS/files.dm:174 (Gaps H3: the jukebox's config tracks)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/files.dm:174
    public static object strip_filepath_extension(object file = null, DmList file_types = null)
    {
        float extstart = findlasttext(Dm.text("[]", file), ".");
        if (!Dm.truthy(extstart))
            return Dm.text("[]", file);
        object ext = copytext(Dm.text("[]", file), extstart + 1);
        if (Dm.@in(ext, file_types))
            return copytext(Dm.text("[]", file), 1, extstart);
        return Dm.text("[]", file);
    }
}
