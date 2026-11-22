namespace Anomaly.Protocol;

// Protocol version 53 (M10x slice G5, docs/ai/specs/protocol.md section 57; the codec: Codec.V53.cs).
// - BugReport (client to server, State channel): a part of the Esc menu's bug report form with its optional WebP screenshot. The
//   server takes no client message over 8 fragments (LiteNetServerTransport.MaxInboundFragments), so the report's body goes in parts of
//   at most 4 KB, in order. The client never sends anything else on the State channel, so a large report holds back no command on Control.

/// <summary>
/// Version 53: a bug report from the Esc menu. <see cref="Title"/> and <see cref="WhatHappened"/> are required; <see cref="Steps"/>
/// and <see cref="Client"/> (the client's version, OS, GPU and preset, lines of text) may be null; <see cref="Image"/> is a WebP
/// screenshot or null. The server adds what it knows itself (the key, the round, the map, the area and the tile).
/// </summary>
public sealed record BugReportMessage(string Title, string WhatHappened, string Steps, string Client, byte[] Image);

/// <summary>
/// Version 53: one part of a <see cref="BugReportMessage"/>'s encoded body (<c>Codec.EncodeBugReportParts</c>): the body's
/// <see cref="Total"/> length, this part's <see cref="Offset"/> in it and its <see cref="Bytes"/>. A part at offset 0 starts a report.
/// </summary>
public sealed record BugReportPart(int Total, int Offset, byte[] Bytes);
