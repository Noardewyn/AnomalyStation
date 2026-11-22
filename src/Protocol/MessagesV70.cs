namespace Anomaly.Protocol;

// Protocol version 70 (D320, docs/ai/specs/protocol.md section 71; the codec: Codec.V70.cs).
// - MouseWheel action (client to server): BYOND's MouseWheel over the aimed thing (ActionKind.MouseWheel, MessagesV2.cs).
// - BuildPreview (server to client, State channel, after the tick's Delta, only when it changed): what the tool in the active hand would
//   build, as a picture the client draws as a ghost on the aimed tile (the RPD's pipe; later the other construction tools).
// Version 71 (D320): the preview names the tile a click at what the player hovers would build on (the server decides it: reach, a free
// pipe layer, nothing taken or reprogrammed), in place of version 70's reach; the client draws it only while it aims at that tile.

/// <summary>
/// Version 70: what the held tool would build: tg's picture (<see cref="Icon"/> file, <see cref="State"/>, BYOND <see cref="Dir"/>) multiplied
/// by <see cref="Color"/> (RGBA) and shifted by <see cref="PixelX"/>, <see cref="PixelY"/> tg pixels, on tile (<see cref="X"/>, <see cref="Y"/>,
/// <see cref="Z"/>) (version 71). <see cref="None"/> (no icon): nothing to show.
/// </summary>
public sealed record BuildPreviewMessage(string Icon, string State, byte Dir, uint Color, sbyte PixelX, sbyte PixelY, int X, int Y, int Z)
{
    /// <summary>No preview.</summary>
    public static readonly BuildPreviewMessage None = new(null, null, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Whether there is a picture to show.</summary>
    public bool IsSet => !string.IsNullOrEmpty(Icon);
}
