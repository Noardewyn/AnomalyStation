namespace Anomaly.Protocol;

// Protocol version 87 (D336, the map editor's ME7; docs/ai/specs/protocol.md section 84; the codec: Codec.V87.cs).
// - MapGrid ends with the map's own sky and sun, when its map json sets them (the key anomaly: docs/ai/specs/map-editor.md 5.10): the sky's
//   kind with its colours, its stars and the bytes of its picture, and the sun's light, colour, size and height. A map that sets none
//   sends one byte and has the game's own.

public sealed partial class MapGrid
{
    /// <summary>Version 87: the map's own sky and sun; null: the game's own.</summary>
    public MapLook Look { get; set; }
}

/// <summary>Version 87: a map's own sky and sun (the map editor's settings).</summary>
public sealed record MapLook
{
    /// <summary>The sky's kinds: space with the game's planet, moon and rocks; the stars alone; two colours; a picture.</summary>
    public const byte SkySpace = 0, SkyStars = 1, SkyColours = 2, SkyImage = 3;

    public byte Sky { get; init; }

    /// <summary>The colour overhead and the colour at the horizon of <see cref="SkyColours"/>, 0xRRGGBB.</summary>
    public uint SkyTop { get; init; }

    public uint SkyHorizon { get; init; }

    /// <summary>How bright the stars are, 0 to 4 (1: the game's own).</summary>
    public float Stars { get; init; } = 1f;

    /// <summary>The picture of <see cref="SkyImage"/>: a png or a jpg file's bytes, a panorama laid around the map; empty for the other kinds, and when the map's picture is missing.</summary>
    public byte[] Image { get; init; } = Array.Empty<byte>();

    /// <summary>Whether the map has a sun: its light, its disc and its flare.</summary>
    public bool Sun { get; init; } = true;

    /// <summary>The sun's colour, 0xRRGGBB.</summary>
    public uint SunColour { get; init; } = 0xFFF5E0;

    /// <summary>The strength of the sun's light, 0 to 8.</summary>
    public float SunEnergy { get; init; } = 0.7f;

    /// <summary>The radius of the sun's disc, 0.2 to 30 degrees.</summary>
    public float SunSize { get; init; } = 2f;

    /// <summary>The sun's height over the map's plane, 1 to 90 degrees.</summary>
    public float SunElevation { get; init; } = 35f;

    public bool Equals(MapLook other)
        => other != null && Sky == other.Sky && SkyTop == other.SkyTop && SkyHorizon == other.SkyHorizon && Stars == other.Stars && Image.AsSpan().SequenceEqual(other.Image)
            && Sun == other.Sun && SunColour == other.SunColour && SunEnergy == other.SunEnergy && SunSize == other.SunSize && SunElevation == other.SunElevation;

    public override int GetHashCode() => HashCode.Combine(Sky, SkyTop, SkyHorizon, Stars, Image.Length, Sun, SunColour, SunEnergy);
}
