using System.IO;
using Xunit.Sdk;

namespace Anomaly.Server.Tests.M3Maps.R4;

/// <summary>The converted MetaStation's maps folder for the server tests (maps/ with map_jsons/metastation.json), or null.</summary>
internal static class MetaServer
{
    internal static string Folder()
    {
        string env = Environment.GetEnvironmentVariable("ANOMALY_META_MAP");
        if (!string.IsNullOrEmpty(env) && File.Exists(env))
        {
            string f = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(env)));
            if (File.Exists(Path.Combine(f, "map_jsons", "metastation.json"))) return f;
        }
        for (string dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "maps");
            if (File.Exists(Path.Combine(candidate, "map_jsons", "metastation.json"))) return candidate;
        }
        return null;
    }

    /// <summary>
    /// Empties Meta's Aux base construction zone for a test that builds or breaks things in it: tg loads the aux base there at
    /// round start (MetaStation.dmm's aux_base_zone roundstart_template, M10 slice G3); the port's jumpToNullSpace takes it away as
    /// a launched base leaves, and the zone's plating and area come back.
    /// </summary>
    /// <summary>
    /// Steps until SSair has worked off the boot's active turfs (at most 200 ticks): the round-start shuttles' moves and the
    /// reservations' turf changes wake a few thousand space turfs (tg's add_to_active takes space too, M10 slice G), which keep its
    /// first fire busy for about two seconds; a test that times the air from its own change waits for that first.
    /// </summary>
    internal static void SettleAir(Anomaly.Server.Tests.Integration.RealHeadlessWorld w)
    {
        for (int i = 0; i < 200; i++)
        {
            using (w.World.Enter())
            {
                if (Anomaly.Core.DmShim.Dm.length(Anomaly.Core.Tg.GlobalProcs.SSair.active_turfs) < 500 && Anomaly.Core.Tg.GlobalProcs.SSair.times_fired > 1)
                    return;
            }
            w.Run(1);
        }
    }

    internal static void ClearAuxBase(Anomaly.Core.Kernel.World world)
    {
        using (world.Enter())
        {
            foreach (var port in Anomaly.Core.DmShim.Dm.each<Anomaly.Core.Tg.obj_docking_port_mobile_auxiliary_base>(Anomaly.Core.Tg.GlobalProcs.SSshuttle.mobile_docking_ports).ToList())
                Anomaly.Core.Kernel.Waiting.NoSleep(port.jumpToNullSpace());
        }
    }
}

/// <summary>A fact that needs the converted MetaStation (tools/convert-maps.sh); skipped without it.</summary>
[TraitDiscoverer(MetaTraitDiscoverer.TypeName, MetaTraitDiscoverer.AssemblyName)]
public sealed class MetaServerFactAttribute : FactAttribute, ITraitAttribute
{
    public MetaServerFactAttribute()
    {
        if (MetaServer.Folder() == null)
            Skip = "no converted MetaStation (run tools/convert-maps.sh, or set ANOMALY_META_MAP)";
    }
}

/// <summary>A theory that needs the converted MetaStation (tools/convert-maps.sh); skipped without it.</summary>
[TraitDiscoverer(MetaTraitDiscoverer.TypeName, MetaTraitDiscoverer.AssemblyName)]
public sealed class MetaServerTheoryAttribute : TheoryAttribute, ITraitAttribute
{
    public MetaServerTheoryAttribute()
    {
        if (MetaServer.Folder() == null)
            Skip = "no converted MetaStation (run tools/convert-maps.sh, or set ANOMALY_META_MAP)";
    }
}
