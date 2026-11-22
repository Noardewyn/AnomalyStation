namespace Anomaly.Protocol;

// Protocol version 79 (silicons SI4d, D326): an image the world made at run time (a painting's PNG), BYOND's resource cache entry of a file
// the server wrote. The server sends each to a client once, before the messages that name it (a sprite's icon, a tgui window's picture).

/// <summary>
/// Version 79: the image a runtime resource path names: <see cref="Path"/> is <c>rsc/MD5.png</c> (the lower-case hex MD5 of the bytes) and
/// <see cref="Png"/> the PNG file; an appearance or a window names it by the path as it names a tg icon file.
/// </summary>
public sealed record RuntimeResourceMessage(string Path, byte[] Png);
