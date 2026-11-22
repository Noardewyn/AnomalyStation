using System.IO;
using System.Linq;

namespace Anomaly.Core.DmShim;

// Owner: contracts (W3.0). The host file seam (translation-rules.md Appendix C, "file I/O"): the core has no file
// system of its own; BYOND's file builtins (Dm.Files.cs), file output (Dm.output_to to a DmFileRef) and tg's rust-g file
// procs go through the world's IDmFileSystem (DmWorldState.Files). Tests use the in-memory store every world starts
// with; a host that writes real files sets a DmDirectoryFiles.

/// <summary>
/// The host file seam of one world (<see cref="DmWorldState.Files"/>). Paths are DM paths: relative to the world's
/// directory, with <c>/</c> separators (tg writes <c>"data/logs/..."</c>, <c>"config/config.txt"</c>); a path that
/// ends in <c>/</c> names a directory. Implementations normalize <c>\</c> to <c>/</c> and drop a leading <c>./</c>.
/// </summary>
public interface IDmFileSystem
{
    /// <summary>Whether a file (or, for a path ending in <c>/</c>, a directory) exists.</summary>
    bool Exists(string path);

    /// <summary>The whole text of a file, or null when it does not exist.</summary>
    string ReadText(string path);

    /// <summary>Creates or replaces a file with <paramref name="text"/> (rust-g <c>rustg_file_write</c>).</summary>
    void WriteText(string path, string text);

    /// <summary>Appends <paramref name="text"/> to a file, creating it when it does not exist (DM <c>text2file</c>).</summary>
    void AppendText(string path, string text);

    /// <summary>Deletes a file, or a directory and everything in it (a path ending in <c>/</c>); false when nothing was there.</summary>
    bool Delete(string path);

    /// <summary>Copies a file, or a directory and everything in it (a source ending in <c>/</c>); false when the source does not exist.</summary>
    bool Copy(string source, string destination);

    /// <summary>
    /// DM <c>flist(Path)</c>: the names of the files and directories (with a trailing <c>/</c>) directly inside the
    /// directory part of <paramref name="path"/> whose names start with the part after its last <c>/</c>, in ordinal order.
    /// </summary>
    IReadOnlyList<string> List(string path);

    /// <summary>
    /// Silicons SI4d (D326): the bytes of a file (rust-g's PNG output, BYOND's <c>icon(file)</c> of a PNG the world wrote), or null when it
    /// does not exist. A store of text keeps bytes as Latin-1 text, which round-trips every byte.
    /// </summary>
    byte[] ReadBytes(string path) => ReadText(path) is string text ? System.Text.Encoding.Latin1.GetBytes(text) : null;

    /// <summary>Silicons SI4d: creates or replaces a file with <paramref name="bytes"/> (rust-g's <c>rustg_dmi_create_png</c>).</summary>
    void WriteBytes(string path, byte[] bytes) => WriteText(path, System.Text.Encoding.Latin1.GetString(bytes ?? Array.Empty<byte>()));
}

/// <summary>
/// A file system in memory (the default of every world, <see cref="DmWorldState.Files"/>): files are path keys with
/// text, directories exist implicitly while a file is inside them. Tests put configuration files in it and read the
/// logs a world wrote. Deterministic and per world.
/// </summary>
public sealed class DmMemoryFiles : IDmFileSystem
{
    private readonly SortedDictionary<string, string> _files = new(StringComparer.Ordinal);

    /// <summary>The paths of all files, in ordinal order (tests).</summary>
    public IReadOnlyList<string> Paths => _files.Keys.ToList();

    /// <inheritdoc/>
    public bool Exists(string path)
    {
        string p = DmFileSystemPaths.Normalize(path);
        if (p.Length == 0) return false;
        if (p.EndsWith('/')) return _files.Keys.Any(k => k.StartsWith(p, StringComparison.Ordinal));
        return _files.ContainsKey(p);
    }

    /// <inheritdoc/>
    public string ReadText(string path) => _files.TryGetValue(DmFileSystemPaths.Normalize(path), out string text) ? text : null;

    /// <inheritdoc/>
    public void WriteText(string path, string text) => _files[DmFileSystemPaths.FilePath(path)] = text ?? "";

    /// <inheritdoc/>
    public void AppendText(string path, string text)
    {
        string p = DmFileSystemPaths.FilePath(path);
        _files[p] = (_files.TryGetValue(p, out string old) ? old : "") + (text ?? "");
    }

    /// <inheritdoc/>
    public bool Delete(string path)
    {
        string p = DmFileSystemPaths.Normalize(path);
        if (p.Length == 0) return false;
        if (!p.EndsWith('/')) return _files.Remove(p);
        List<string> inside = _files.Keys.Where(k => k.StartsWith(p, StringComparison.Ordinal)).ToList();
        foreach (string k in inside) _files.Remove(k);
        return inside.Count > 0;
    }

    /// <inheritdoc/>
    public bool Copy(string source, string destination)
    {
        string from = DmFileSystemPaths.Normalize(source);
        string to = DmFileSystemPaths.Normalize(destination);
        if (from.Length == 0 || to.Length == 0) return false;
        if (!from.EndsWith('/'))
        {
            if (!_files.TryGetValue(from, out string text)) return false;
            _files[to.EndsWith('/') ? to + from.Substring(from.LastIndexOf('/') + 1) : to] = text;
            return true;
        }
        if (!to.EndsWith('/')) to += "/";
        List<KeyValuePair<string, string>> inside = _files.Where(kv => kv.Key.StartsWith(from, StringComparison.Ordinal)).ToList();
        foreach ((string key, string text) in inside) _files[to + key.Substring(from.Length)] = text;
        return inside.Count > 0;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> List(string path)
    {
        string p = DmFileSystemPaths.Normalize(path);
        int slash = p.LastIndexOf('/');
        string dir = slash < 0 ? "" : p.Substring(0, slash + 1);
        string prefix = p.Substring(slash + 1);
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string key in _files.Keys)
        {
            if (!key.StartsWith(dir, StringComparison.Ordinal)) continue;
            string rest = key.Substring(dir.Length);
            int next = rest.IndexOf('/');
            string name = next < 0 ? rest : rest.Substring(0, next + 1);
            if (name.StartsWith(prefix, StringComparison.Ordinal)) names.Add(name);
        }
        return names.ToList();
    }
}

/// <summary>
/// The host's real directory as the world's file system (set by a host, never by tests that must not touch the disk):
/// DM paths are resolved under <see cref="Root"/>; a path that would leave it (<c>..</c>, an absolute path) is refused
/// (false or null, as for a missing file; writes are dropped).
/// </summary>
public sealed class DmDirectoryFiles : IDmFileSystem
{
    /// <summary>Creates the seam over the directory <paramref name="root"/> (created on first write).</summary>
    public DmDirectoryFiles(string root)
    {
        if (string.IsNullOrEmpty(root)) throw new ArgumentException("A root directory is required.", nameof(root));
        Root = Path.GetFullPath(root);
    }

    /// <summary>The directory DM paths are relative to.</summary>
    public string Root { get; }

    // The full path of a DM path under Root, or null when it leaves Root.
    private string Resolve(string path)
    {
        string p = DmFileSystemPaths.Normalize(path);
        if (p.Length == 0 || Path.IsPathRooted(p)) return null;
        string full = Path.GetFullPath(Path.Combine(Root, p.TrimEnd('/')));
        string rootWithSeparator = Root.EndsWith(Path.DirectorySeparatorChar) ? Root : Root + Path.DirectorySeparatorChar;
        return full == Root || full.StartsWith(rootWithSeparator, StringComparison.Ordinal) ? full : null;
    }

    private static bool IsDirectoryPath(string path) => DmFileSystemPaths.Normalize(path).EndsWith('/');

    /// <inheritdoc/>
    public bool Exists(string path)
    {
        string full = Resolve(path);
        if (full == null) return false;
        return IsDirectoryPath(path) ? Directory.Exists(full) : File.Exists(full);
    }

    /// <inheritdoc/>
    public string ReadText(string path)
    {
        string full = Resolve(path);
        return full != null && File.Exists(full) ? File.ReadAllText(full) : null;
    }

    /// <inheritdoc/>
    public void WriteText(string path, string text)
    {
        string full = Resolve(path);
        if (full == null || IsDirectoryPath(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, text ?? "");
    }

    /// <inheritdoc/>
    public byte[] ReadBytes(string path)
    {
        string full = Resolve(path);
        return full != null && File.Exists(full) ? File.ReadAllBytes(full) : null;
    }

    /// <inheritdoc/>
    public void WriteBytes(string path, byte[] bytes)
    {
        string full = Resolve(path);
        if (full == null || IsDirectoryPath(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllBytes(full, bytes ?? Array.Empty<byte>());
    }

    /// <inheritdoc/>
    public void AppendText(string path, string text)
    {
        string full = Resolve(path);
        if (full == null || IsDirectoryPath(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.AppendAllText(full, text ?? "");
    }

    /// <inheritdoc/>
    public bool Delete(string path)
    {
        string full = Resolve(path);
        if (full == null || full == Root) return false;
        if (IsDirectoryPath(path))
        {
            if (!Directory.Exists(full)) return false;
            Directory.Delete(full, recursive: true);
            return true;
        }
        if (!File.Exists(full)) return false;
        File.Delete(full);
        return true;
    }

    /// <inheritdoc/>
    public bool Copy(string source, string destination)
    {
        string from = Resolve(source);
        string to = Resolve(destination);
        if (from == null || to == null) return false;
        if (!IsDirectoryPath(source))
        {
            if (!File.Exists(from)) return false;
            if (IsDirectoryPath(destination)) to = Path.Combine(to, Path.GetFileName(from));
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Copy(from, to, overwrite: true);
            return true;
        }
        if (!Directory.Exists(from)) return false;
        foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, overwrite: true);
        }
        return true;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> List(string path)
    {
        string p = DmFileSystemPaths.Normalize(path);
        int slash = p.LastIndexOf('/');
        string dirPart = slash < 0 ? "" : p.Substring(0, slash + 1);
        string prefix = p.Substring(slash + 1);
        string dir = dirPart.Length == 0 ? Root : Resolve(dirPart);
        if (dir == null || !Directory.Exists(dir)) return Array.Empty<string>();
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string d in Directory.EnumerateDirectories(dir))
            names.Add(Path.GetFileName(d) + "/");
        foreach (string f in Directory.EnumerateFiles(dir))
            names.Add(Path.GetFileName(f));
        return names.Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).ToList();
    }
}

/// <summary>
/// One DM folder of a world kept elsewhere (a host's real directory for tg's <c>data/player_saves/</c>, the rest in memory):
/// paths under <see cref="Prefix"/> go to the mount without the prefix, everything else to the inner file system.
/// </summary>
public sealed class DmMountedFiles : IDmFileSystem
{
    private readonly IDmFileSystem _inner;
    private readonly IDmFileSystem _mount;

    /// <summary>Mounts <paramref name="mount"/> at the DM folder <paramref name="prefix"/> (ending in <c>/</c>) over <paramref name="inner"/>.</summary>
    public DmMountedFiles(IDmFileSystem inner, string prefix, IDmFileSystem mount)
    {
        Prefix = DmFileSystemPaths.Normalize(prefix);
        if (!Prefix.EndsWith('/')) throw new ArgumentException("The mount folder must end in '/'.", nameof(prefix));
        _inner = inner;
        _mount = mount;
    }

    /// <summary>The DM folder the mount is at.</summary>
    public string Prefix { get; }

    private (IDmFileSystem Files, string Path) Route(string path)
    {
        string p = DmFileSystemPaths.Normalize(path);
        return p.StartsWith(Prefix, StringComparison.Ordinal) && p.Length > Prefix.Length ? (_mount, p.Substring(Prefix.Length)) : (_inner, path);
    }

    /// <inheritdoc/>
    public bool Exists(string path) { var (f, p) = Route(path); return f.Exists(p); }

    /// <inheritdoc/>
    public string ReadText(string path) { var (f, p) = Route(path); return f.ReadText(p); }

    /// <inheritdoc/>
    public void WriteText(string path, string text) { var (f, p) = Route(path); f.WriteText(p, text); }

    /// <inheritdoc/>
    public void AppendText(string path, string text) { var (f, p) = Route(path); f.AppendText(p, text); }

    /// <inheritdoc/>
    public bool Delete(string path) { var (f, p) = Route(path); return f.Delete(p); }

    /// <inheritdoc/>
    public bool Copy(string source, string destination)
    {
        var (from, s) = Route(source);
        var (to, d) = Route(destination);
        // shortcut: no copy across the mount (tg's savefile backup is beside the savefile), add one if a caller needs it
        return from == to && from.Copy(s, d);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> List(string path) { var (f, p) = Route(path); return f.List(p); }
}

/// <summary>
/// A DM file reference, what <c>file(Path)</c> returns (BYOND ref <c>/proc/file</c>): it names a path of the world's
/// file seam and renders as that path in text (<c>"[F]"</c>). <c>F &lt;&lt; text</c> (tg's <c>WRITE_FILE</c>)
/// appends a line to it; the file builtins accept it wherever they accept a path.
/// </summary>
public sealed class DmFileRef
{
    internal DmFileRef(string path) => Path = path;

    /// <summary>The DM path the reference names.</summary>
    public string Path { get; }
}

// Path normalization shared by the file systems and the file builtins.
internal static class DmFileSystemPaths
{
    internal static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        string p = path.Replace('\\', '/');
        while (p.StartsWith("./", StringComparison.Ordinal)) p = p.Substring(2);
        while (p.Contains("//", StringComparison.Ordinal)) p = p.Replace("//", "/", StringComparison.Ordinal);
        return p;
    }

    // A file's path: a path ending in '/' names a directory and cannot be written.
    internal static string FilePath(string path)
    {
        string p = Normalize(path);
        if (p.Length == 0 || p.EndsWith('/')) throw new DmRuntimeException("cannot write to the directory path \"" + path + "\"");
        return p;
    }
}
