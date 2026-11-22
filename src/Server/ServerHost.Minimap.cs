using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server;

// Version 67 (D315): the minimap of the level a session's mob stands on, after the Delta, once per level and join. A level's minimap is
// made once a round (MinimapSeam runs tg's load_z outside the step and changes nothing of the world) and kept encoded for every session;
// the map's later changes are not in it, as tg's get_minimap_for_z keeps the first picture it made of a level.

public sealed partial class ServerHost
{
    private World _minimapWorld;
    private readonly Dictionary<int, byte[]> _minimaps = new(); // null for a level with nothing tg draws, or one that failed

    private void SendMinimap(Session s)
    {
        int z = TurfOf(s.Client?.mob) is turf t ? (int)t.z : 0;
        if (z is < 1 or > Limits.MaxMapLevels || !s.MinimapLevels.Add(z))
            return;
        if (_minimapWorld != World)
        {
            _minimaps.Clear();
            _minimapWorld = World;
        }
        if (!_minimaps.TryGetValue(z, out byte[] bytes))
            _minimaps[z] = bytes = MakeMinimap(z);
        if (bytes != null)
            Send(s, Channel.State, bytes);
    }

    // tg's get_turf, read raw outside the world
    private static turf TurfOf(object thing)
    {
        while (thing is atom_movable movable)
            thing = movable.loc;
        return thing as turf;
    }

    // D315's dev trackers (minimapmobs on): every living mob of the session's level but its own (tg's GLOB.alive_mob_list, read raw), twice a
    // second; a mob a player has (a ckey, connected or not) apart from the rest, with its name for the hover.
    private const int MinimapMobsEveryTicks = 10;

    private void SendMinimapMobs(Session s, long tick)
    {
        if (!s.MinimapMobs || tick % MinimapMobsEveryTicks != 0)
            return;
        mob own = s.Client?.mob;
        int z = TurfOf(own) is turf t ? (int)t.z : 0;
        if (z is < 1 or > Limits.MaxMapLevels)
            return;
        var message = new MinimapMobsMessage { Z = z };
        foreach (mob_living m in Dm.each<mob_living>(TgState.Of(World).GLOB?.alive_mob_list))
        {
            if (message.Mobs.Count >= Limits.MaxMinimapMobs)
                break;
            if (ReferenceEquals(m, own) || TurfOf(m) is not turf at || (int)at.z != z)
                continue;
            string name = m.name ?? "";
            message.Mobs.Add(new MinimapMob((int)at.x, (int)at.y, !string.IsNullOrEmpty(m.ckey), name.Length > 32 ? name[..32] : name)); // 32 chars fit the name cap's bytes
        }
        Send(s, Channel.State, Codec.Encode(message));
    }

    private byte[] MakeMinimap(int z)
    {
        try
        {
            return EncodeMinimap(MinimapSeam.Build(World, z));
        }
        catch (Exception e)
        {
            _out.WriteLine($"minimap of level {z} failed: {e.Message}");
            return null;
        }
    }

    /// <summary>A level's minimap as its message (the colours indexed in a table, the cells as runs), or null for none.</summary>
    internal static byte[] EncodeMinimap(MinimapPicture picture)
    {
        if (picture == null)
            return null;
        var message = new MinimapMessage { Z = picture.Z, MinX = picture.MinX, MinY = picture.MinY, Width = picture.Width, Height = picture.Height };
        var index = new Dictionary<uint, int>();
        var pixels = new int[picture.Pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            uint colour = picture.Pixels[i];
            if (colour == 0)
                continue;
            if (!index.TryGetValue(colour, out int k))
            {
                message.Colours.Add(colour);
                index[colour] = k = message.Colours.Count;
            }
            pixels[i] = k;
        }
        AddMinimapRuns(message.PixelRuns, pixels);
        message.AreaNames.AddRange(picture.AreaNames);
        message.AreaCodes.AddRange(picture.AreaCodes);
        AddMinimapRuns(message.AreaRuns, picture.Areas);
        return Codec.Encode(message);
    }

    private static void AddMinimapRuns(List<TurfRun> runs, int[] cells)
    {
        for (int start = 0, i = 1; i <= cells.Length; i++)
        {
            if (i < cells.Length && cells[i] == cells[start])
                continue;
            runs.Add(new TurfRun(i - start, cells[start]));
            start = i;
        }
    }
}
