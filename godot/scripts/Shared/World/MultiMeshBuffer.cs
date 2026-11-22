namespace Anomaly.Client.Shared.World;

/// <summary>
/// Godot's MultiMesh instance buffer (<c>MultiMesh.buffer</c>) for 3D transforms with colours and custom data: per instance the basis's
/// three rows, each followed by that row's origin component, then the colour, then the custom data. A chunk's props are written with one
/// buffer instead of three engine calls per instance (the M7 visual check: a cascade's crystals refilled their chunks every frame); a
/// chunk's tiles, which have no colours, the same way without them.
/// </summary>
public static class MultiMeshBuffer
{
    /// <summary>Floats per instance: the transform's 12, the colour's 4 and the custom data's 4.</summary>
    public const int Stride = 20;

    /// <summary>Floats per instance without colours: the transform's 12 and the custom data's 4.</summary>
    public const int StrideNoColor = 16;

    /// <summary>
    /// Writes instance <paramref name="index"/> from <paramref name="values"/>: the basis rows (9, row by row), the origin (3), the colour
    /// (r, g, b, a; left out for a MultiMesh without colours) and the custom data (4), in that order; the stride is the values' count
    /// (<see cref="Stride"/> or <see cref="StrideNoColor"/>).
    /// </summary>
    public static void Write(float[] buffer, int index, ReadOnlySpan<float> values)
    {
        int o = index * values.Length;
        for (int row = 0; row < 3; row++)
        {
            buffer[o + row * 4] = values[row * 3];
            buffer[o + row * 4 + 1] = values[row * 3 + 1];
            buffer[o + row * 4 + 2] = values[row * 3 + 2];
            buffer[o + row * 4 + 3] = values[9 + row];
        }
        values[12..].CopyTo(buffer.AsSpan(o + 12, values.Length - 12));
    }
}
