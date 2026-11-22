using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: contracts (W3.0). BYOND's file builtins over the world's host file seam (DmWorldState.Files, DmFiles.cs):
// file, fexists, fdel, fcopy, file2text, text2file, flist. A File argument is a path text or a file() reference
// (DmFileRef); any other value names the path of its text ("[x]"); null names nothing. The results that BYOND gives as 1
// or 0 are DM numbers.
public static partial class Dm
{
    // The DM path a File argument names, or null.
    private static string FilePathOf(object File) => File switch
    {
        null => null,
        DmFileRef file => file.Path,
        DmResource resource => resource.Path,
        string text => text,
        _ => str(File),
    };

    private static IDmFileSystem Files => DmWorldState.Current.Files;

    /// <summary>
    /// DM <c>file(Path)</c> (BYOND ref <c>/proc/file</c>): a reference to the file at <paramref name="Path"/> of the
    /// world's file seam, which renders as its path, is <c>isfile</c>, and takes output (<c>F &lt;&lt; text</c>,
    /// <c>Dm.output_to</c>, appends a line). A file reference is returned as is; null gives null.
    /// </summary>
    public static DmFileRef file(object Path) => Path switch
    {
        null => null,
        DmFileRef existing => existing,
        _ => new DmFileRef(FilePathOf(Path)),
    };

    /// <summary>DM <c>fexists(File)</c> (BYOND ref <c>/proc/fexists</c>): 1 if the file (or the directory, a path ending in <c>/</c>) exists, else 0.</summary>
    public static float fexists(object File)
    {
        string path = FilePathOf(File);
        return path != null && (Files.Exists(path) || DmWorldState.Current.GameFileExists?.Invoke(path) == true) ? 1f : 0f;
    }

    /// <summary>
    /// DM <c>fdel(File)</c> (BYOND ref <c>/proc/fdel</c>): deletes the file, or the directory and all its contents for a
    /// path ending in <c>/</c>; 1 on success, 0 when there was nothing to delete.
    /// </summary>
    public static float fdel(object File)
    {
        string path = FilePathOf(File);
        return path != null && Files.Delete(path) ? 1f : 0f;
    }

    /// <summary>
    /// DM <c>fcopy(Source, Destination)</c> (BYOND ref <c>/proc/fcopy</c>): copies a file (or a directory, a source ending
    /// in <c>/</c>) to the destination path; 1 on success, 0 when the source does not exist.
    /// </summary>
    public static float fcopy(object Source, object Destination)
    {
        string from = FilePathOf(Source);
        string to = FilePathOf(Destination);
        return from != null && to != null && Files.Copy(from, to) ? 1f : 0f;
    }

    /// <summary>DM <c>file2text(File)</c> (BYOND ref <c>/proc/file2text</c>): the text of the file, null when it does not exist.</summary>
    public static string file2text(object File)
    {
        string path = FilePathOf(File);
        return path == null ? null : Files.ReadText(path);
    }

    /// <summary>
    /// DM <c>text2file(Text, File)</c> (BYOND ref <c>/proc/text2file</c>): appends the text (a non-text value as
    /// <c>"[x]"</c>) to the file, creating it when needed; 1 on success, 0 for no file.
    /// </summary>
    public static float text2file(object Text, object File)
    {
        string path = FilePathOf(File);
        if (path == null) return 0f;
        Files.AppendText(path, Text as string ?? str(Text));
        return 1f;
    }

    /// <summary>
    /// DM <c>flist(Path)</c> (BYOND ref <c>/proc/flist</c>): the files and sub-directories (with a trailing <c>/</c>) of
    /// the directory part of <paramref name="Path"/> whose names start with the rest of it, as a new list in ordinal
    /// order (BYOND does not specify an order; the port's is deterministic).
    /// </summary>
    public static DmList flist(object Path)
    {
        var result = new DmList();
        string path = FilePathOf(Path);
        if (path == null) return result;
        foreach (string name in Files.List(path)) result.AppendItem(name);
        return result;
    }
}
