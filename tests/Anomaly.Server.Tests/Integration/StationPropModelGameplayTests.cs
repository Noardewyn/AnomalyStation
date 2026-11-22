using System.IO;
using Anomaly.Client.Shared.Models;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The station props' 3D models (docs/ai/specs/models.md) against what a client of the real server sees on MetaStation: the icons and states tg gives the map's
/// canisters (GAGS keys built from canister.dm's config and colours), stationary tanks, vending machines, chairs and racks are the ones the catalog names.
/// </summary>
public class StationPropModelGameplayTests(ITestOutputHelper output)
{
    [MetaServerFact]
    public void The_maps_props_as_a_client_sees_them_are_drawn_by_the_catalogs_models()
    {
        var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "metastation", MapsFolder = MetaServer.Folder() };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 4000 }, config, strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        ReplicaWorld replica = p.Session.Replica;
        ModelCatalog catalog = ModelCatalog.Parse(File.ReadAllText(ModelCatalog.Locate()!));

        // The map's things of these types the server has, and how the client sees each (its entities reach it as the whole map does, D105).
        var seen = new Dictionary<string, List<(string Icon, string State, string Model)>>();
        foreach (string type in new[] { "/obj/machinery/portable_atmospherics/canister", "/obj/machinery/atmospherics/components/tank", "/obj/machinery/vending",
                     "/obj/structure/chair/office", "/obj/structure/rack", "/obj/structure/sink" })
        {
            List<long> ids = new();
            using (w.World.Enter())
            {
                foreach (atom a in Dm.each(Dm.world).OfType<atom>())
                {
                    if (a is atom_movable && DmTypes.PathOf(a.GetType()).StartsWith(type, StringComparison.Ordinal))
                        ids.Add(a.dm_id);
                }
            }
            var list = new List<(string, string, string)>();
            foreach (long id in ids)
            {
                ReplicaEntity e = replica.Entities.SingleOrDefault(x => x.Id == id);
                if (e == null || !replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry look))
                    continue;
                string icon = replica.ResolveResource(look.Icon), state = replica.ResolveString(look.IconState);
                list.Add((icon, state, catalog.Find(icon, state)?.Id));
            }
            seen[type] = list;
            output.WriteLine($"{type}: {ids.Count} on the map, {list.Count} seen; " + string.Join(", ", list.GroupBy(x => (x.Item1, x.Item2, x.Item3)).Select(g => $"{g.Key.Item1} {g.Key.Item2} -> {g.Key.Item3 ?? "2D"} x{g.Count()}")));
        }

        foreach ((string type, List<(string Icon, string State, string Model)> list) in seen)
        {
            Assert.NotEmpty(list);
            // The canisters' GAGS keys and the tanks' smoothing states are the ones the catalog names; every vending machine (the snack and soda machines tg's
            // spawners pick at random too) has its model; most racks do (a minibar rack has none yet).
            int drawn = list.Count(x => x.Model != null);
            int needed = type.Contains("rack") ? list.Count * 8 / 10 : list.Count;
            Assert.True(drawn >= needed, $"{type}: {drawn} of {list.Count} drawn by a model");
        }
    }
}
