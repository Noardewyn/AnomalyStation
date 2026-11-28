using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV685): a photo is what the photographer's client sees. tg's camera composes its picture on the server
// from the captured atoms' sprites (camera_get_icon, a top-down icon); the port's world is drawn in 3D by the clients, so the server asks
// the photographer's client to render the shot from its eye toward the target tile and takes the PNG it sends back. Everything else of tg's
// camera (the range, what it captures, the description, the names, the blueprints, the ghosts, the photo item) stays as tg runs it on the
// server; only the picture's image comes a moment later. Not tg code: a host seam where camera.dm:285 calls camera_get_icon.

/// <summary>A picture the photographer's client is asked to render (the host sends it as the version 99 PhotoRequest).</summary>
public readonly record struct PhotoRequest(uint Id, int X, int Y, int Z, int TilesX, int TilesY, int Width, int Height);

public partial class client
{
    /// <summary>
    /// DV685: the pictures this client was asked to render since the host last took them, oldest first. Presentation output, like
    /// <see cref="dm_tgui_output"/>: not world state.
    /// </summary>
    [NotHashed]
    public readonly List<PhotoRequest> dm_photo_requests = new();

    /// <summary>The most requests <see cref="dm_photo_requests"/> keeps for a client nothing takes them from (a bot's: the oldest go).</summary>
    public const int DmPhotoRequestCap = 8;
}

/// <summary>The photo seam: tg's camera asks a client for a picture's image here, and the host hands in the client's answer.</summary>
public static class PhotoSeam
{
    /// <summary>The pixels of a tile of the picture's frame (tg's 32, doubled for the 3D view's detail).</summary>
    public const int PixelsPerTile = 64;

    /// <summary>The most pixels on a side of a picture (tg's widest aperture, 7 tiles, at <see cref="PixelsPerTile"/>).</summary>
    public const int MaxSide = 7 * PixelsPerTile;

    /// <summary>The fewest pixels on a side the server takes (a client may send a smaller image than asked, to fit the byte cap).</summary>
    public const int MinSide = 16;

    /// <summary>How long a request waits for its image, in ticks (10 seconds): a later answer is refused and the picture stays blank.</summary>
    public const long DeadlineTicks = 10 * 20;

    /// <summary>The most bytes of a picture's PNG (the protocol's <c>Limits.MaxPhotoBytes</c>).</summary>
    public const int MaxBytes = 512 * 1024;

    /// <summary><see cref="Develop"/>'s refusal of an image that answers no request of its client: none, another client's, or one answered.</summary>
    public const string NoRequest = "it answers no request of this client";

    /// <summary><see cref="Develop"/>'s refusal of the first answer to a request of its client that expired unanswered within <see cref="GraceTicks"/>.</summary>
    public const string Late = "it answers a request of this client too late";

    /// <summary>
    /// How long after its deadline an expired request is remembered, in ticks (a minute, six deadlines): a client that stalled (a window
    /// dragged or minimised, a slow render) sends its queued answers late, and each is told from an answer to nothing for this long. Only
    /// requests a camera shot made are remembered, so a client cannot grow the set by sending answers.
    /// </summary>
    public const long GraceTicks = 60 * 20;

    /// <summary>
    /// The camera of <paramref name="user"/> took <paramref name="picture"/> of <paramref name="target"/>, <paramref name="tilesX"/> by
    /// <paramref name="tilesY"/> tiles: its client is asked to render it. Returns the request's id, 0 when there is no client to ask (the
    /// picture stays blank, as a camera with no one behind it).
    /// </summary>
    public static uint Request(mob user, datum_picture picture, turf target, float tilesX, float tilesY)
    {
        client asked = user?.client;
        World world = World.CurrentOrNull;
        if (asked == null || picture == null || target == null || world == null)
            return 0;
        Holder holder = Holder.Of(world);
        holder.Prune(world.Tick);
        int tx = Math.Clamp((int)tilesX, 1, 15), ty = Math.Clamp((int)tilesY, 1, 15);
        int width = tx * PixelsPerTile, height = ty * PixelsPerTile;
        if (width > MaxSide || height > MaxSide)
        {
            float scale = (float)MaxSide / Math.Max(width, height);
            width = Math.Max(1, (int)(width * scale));
            height = Math.Max(1, (int)(height * scale));
        }
        uint id = ++holder.LastId;
        holder.Pending[id] = new Pending(picture, asked, world.Tick + DeadlineTicks, width, height);
        if (asked.dm_photo_requests.Count >= client.DmPhotoRequestCap)
            asked.dm_photo_requests.RemoveAt(0);
        asked.dm_photo_requests.Add(new PhotoRequest(id, (int)target.x, (int)target.y, (int)target.z, tx, ty, width, height));
        return id;
    }

    /// <summary>
    /// The image <paramref name="from"/> rendered for request <paramref name="id"/>. It must answer a request that client was sent, in time,
    /// once, and be a PNG the server decodes of at least <see cref="MinSide"/> and at most the requested pixels on each side. The PNG goes
    /// into the world's resource cache (each client is sent it once) and becomes the picture's image; the photos showing that picture and
    /// their frames redraw.
    /// Returns null when it was taken, else why it was refused.
    /// </summary>
    public static string Develop(client from, uint id, byte[] png)
    {
        World world = World.CurrentOrNull;
        if (world == null || from == null)
            return "no world";
        Holder holder = Holder.Of(world);
        holder.Prune(world.Tick);
        if (!holder.Pending.TryGetValue(id, out Pending pending) || pending.Asked != from)
        {
            // a request of this client that expired unanswered is forgiven once: an honest client answers every request it was sent
            if (holder.Expired.TryGetValue(id, out Expired expired) && expired.Asked == from)
            {
                holder.Expired.Remove(id);
                return Late;
            }
            return NoRequest;
        }
        holder.Pending.Remove(id);
        if (png == null || png.Length < 1 || png.Length > MaxBytes)
            return "it is not 1 to " + MaxBytes + " bytes";
        try
        {
            // the size the header claims is checked before decoding, so a hostile header never makes the decoder allocate for it
            (int width, int height) = DmPng.SizeOf(png);
            if (width < MinSide || height < MinSide || width > pending.Width || height > pending.Height)
                return "it is " + width + " x " + height + " pixels, not " + MinSide + " to " + pending.Width + " x " + pending.Height;
            DmPng.Decode(png);
        }
        catch (Exception e) when (e is not OutOfMemoryException) // a client's bytes: whatever the decoder trips on refuses them
        {
            return "it is not a PNG the server reads (" + e.Message + ")";
        }
        datum_picture picture = pending.Picture;
        if (picture == null || picture.gc_destroyed != null)
            return "its picture is gone";
        string path = icon.RuntimePrefix + Convert.ToHexString(System.Security.Cryptography.MD5.HashData(png)).ToLowerInvariant() + ".png";
        DmWorldState.Current.AddRuntimeResource(path, png);
        picture.picture_image = Dm.icon(Dm.resource(path));
        picture.picture_icon = null;
        picture.regenerate_small_icon();
        foreach (obj_item_photo photo in Dm.each<obj_item_photo>())
        {
            if (photo.picture != picture)
                continue;
            photo.update_appearance();
            // a frame draws its photo as an overlay, a copy of the photo's look when it was framed
            if (photo.loc is obj_structure_sign_picture_frame or obj_item_wallframe_picture)
                (photo.loc as atom).update_appearance();
        }
        return null;
    }

    /// <summary>The requests still waiting for their image (tests).</summary>
    public static int PendingCount(World world) => Holder.Of(world).Pending.Count;

    /// <summary>The expired requests still remembered for a late answer (tests).</summary>
    public static int ExpiredCount(World world) => Holder.Of(world).Expired.Count;

    private readonly record struct Pending(datum_picture Picture, client Asked, long Deadline, int Width, int Height);

    private readonly record struct Expired(client Asked, long Forget);

    // The per-world requests (World.GetExtension: no static state).
    private sealed class Holder
    {
        internal uint LastId;
        internal readonly Dictionary<uint, Pending> Pending = new();
        internal readonly Dictionary<uint, Expired> Expired = new();

        internal void Prune(long tick)
        {
            if (Pending.Count > 0)
            {
                foreach (KeyValuePair<uint, Pending> stale in Pending.Where(kv => kv.Value.Deadline < tick).ToList())
                {
                    Pending.Remove(stale.Key);
                    Expired[stale.Key] = new Expired(stale.Value.Asked, stale.Value.Deadline + GraceTicks);
                }
            }
            if (Expired.Count > 0)
            {
                foreach (uint forgotten in Expired.Where(kv => kv.Value.Forget < tick).Select(kv => kv.Key).ToList())
                    Expired.Remove(forgotten);
            }
        }

        internal static Holder Of(World world) => world.GetExtension(static _ => new Holder());
    }
}
