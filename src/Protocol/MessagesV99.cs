namespace Anomaly.Protocol;

// Protocol version 99 (DV685: a photo is the photographer's own 3D view; docs/ai/specs/protocol.md section 94; the
// codec: Codec.V99.cs).
// - PhotoRequest (server to client, State channel, after the tick's Delta): tg's camera took a picture; the photographer's client renders it
//   from its eye toward the target tile and answers with the image.
// - PhotoPart (client to server, State channel): a part of that image, a PNG, sent in order from offset 0 as the bug report's parts are (the
//   server takes no client message over 8 fragments). The server gives the image back to every client as a RuntimeResource (version 79).

/// <summary>
/// Version 99: a picture to render. <see cref="Id"/> names it (the answer's parts carry it); the camera looks from the own eye at tile
/// (<see cref="X"/>, <see cref="Y"/>, <see cref="Z"/>) and frames <see cref="TilesX"/> by <see cref="TilesY"/> tiles around it, rendered at
/// most <see cref="Width"/> by <see cref="Height"/> pixels.
/// </summary>
public sealed record PhotoRequestMessage(uint Id, int X, int Y, int Z, byte TilesX, byte TilesY, ushort Width, ushort Height);

/// <summary>
/// Version 99: one part of the PNG answering the <see cref="PhotoRequestMessage"/> <see cref="Id"/>: the PNG's <see cref="Total"/> length,
/// this part's <see cref="Offset"/> in it and its <see cref="Bytes"/>. A part at offset 0 starts the image.
/// </summary>
public sealed record PhotoPart(uint Id, int Total, int Offset, byte[] Bytes);
