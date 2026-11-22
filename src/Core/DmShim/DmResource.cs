namespace Anomaly.Core.DmShim;

// Owner: lead (wave 4). DM resource literals ('icons/x.dmi', 'sound/y.ogg'): the value DM's single-quoted file literals
// compile to. The port has no resource cache: a resource names the path of its file, which the presentation layer
// resolves (the .dmi converter of M3 and the client's assets).

/// <summary>
/// A DM resource, what a resource literal such as <c>'icons/turf/floors.dmi'</c> evaluates to (BYOND ref
/// <c>/DM/resource</c>; OpenDream <c>DreamResource</c>). It is <c>isfile</c>, renders as its path in text, and two
/// resources of the same path are equal (one cache entry per file), but a resource never equals the text of its path.
/// Immutable: it can be written in a field initializer or a constructor (it never touches the World).
/// </summary>
public sealed class DmResource : IEquatable<DmResource>
{
    internal DmResource(string path) => Path = path;

    /// <summary>The path of the resource file, as written in the literal.</summary>
    public string Path { get; }

    /// <inheritdoc />
    public bool Equals(DmResource other) => other is not null && string.Equals(Path, other.Path, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is DmResource other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => string.GetHashCode(Path, StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => Path;

    // Image resources, the ones isicon() accepts (OpenDream IconResource: the image file types BYOND loads as icons).
    internal bool IsIcon
    {
        get
        {
            string ext = System.IO.Path.GetExtension(Path).ToLowerInvariant();
            return ext is ".dmi" or ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp";
        }
    }
}

public static partial class Dm
{
    /// <summary>
    /// The DM resource literal <c>'path'</c> (translation-rules.md 3.7): <c>'icons/turf/floors.dmi'</c> is
    /// <c>Dm.resource("icons/turf/floors.dmi")</c>. A null or empty path is a runtime error (DM rejects an empty literal
    /// at compile time).
    /// </summary>
    public static DmResource resource(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new DmRuntimeException("resource(): empty path");
        return new DmResource(path);
    }
}
