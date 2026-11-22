using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server;

// M4b slice 3 (the dev menu's gas overlay, protocol version 5): a player's client asks for the air around a point with
// ActionKind.DevGasProbe while its gas overlay is on; the answer is a DevGasReport on the Control channel, sent after the
// tick with the chat. Only with DEV_COMMANDS on (the network side drops the probe otherwise). It reads the air and changes
// nothing, so a replay skips it.
public sealed partial class ServerHost
{
    // The gases a report names: tg's id, name and primary_color of each /datum/gas (gas_types.dm), in the order first met.
    private static readonly Dictionary<Type, DevGasInfo> GasInfos = new();

    private void ApplyDevGasProbe(Session s, in QueuedCommand command)
    {
        if (_replay || s.Connection == null || s.State != SessionState.Joined) return;
        DevGasReport report = BuildGasReport(command.TurfX, command.TurfY, command.TurfZ);
        if (report == null) return;
        while (s.ControlOutbox.Count >= Session.ControlOutboxCap) s.ControlOutbox.Dequeue();
        s.ControlOutbox.Enqueue(Codec.Encode(report));
    }

    // The (2r + 1) x (2r + 1) cells around (x, y) on level z, clipped to the map (inside the world, in the command phase).
    private static DevGasReport BuildGasReport(int x, int y, int z)
    {
        World world = World.Current;
        int width = MapView.Width(world), height = MapView.Height(world), levels = MapView.Levels(world);
        if (z < 1 || z > levels || x < 1 || y < 1 || x > width || y > height) return null;
        const int r = Limits.DevGasProbeRadius;
        int x0 = Math.Max(1, x - r), y0 = Math.Max(1, y - r);
        int x1 = Math.Min(width, x + r), y1 = Math.Min(height, y + r);
        var report = new DevGasReport { X0 = x0, Y0 = y0, Z = z, Width = x1 - x0 + 1, Height = y1 - y0 + 1 };
        var index = new Dictionary<Type, byte>();
        var amounts = new List<DevGasAmount>();
        for (int cy = y0; cy <= y1; cy++)
        {
            for (int cx = x0; cx <= x1; cx++)
            {
                turf cell = MapView.TurfAt(world, cx, cy, z);
                if (cell is not turf_open open || open.air == null)
                {
                    report.Cells.Add(new DevGasCell(cell == null ? DevCellKind.None : DevCellKind.Closed, 0, 0, Array.Empty<DevGasAmount>()));
                    continue;
                }

                datum_gas_mixture air = open.air;
                amounts.Clear();
                foreach ((object gas, object moles) in Dm.pairs(air.moles))
                {
                    float amount = Dm.num(moles);
                    if (!(amount > 0f) || gas is not Type type) continue;
                    if (!index.TryGetValue(type, out byte i))
                    {
                        if (report.Gases.Count >= Limits.MaxDevGases) continue;
                        i = (byte)report.Gases.Count;
                        index[type] = i;
                        report.Gases.Add(InfoOf(type));
                    }
                    if (amounts.Count < Limits.MaxDevGases) amounts.Add(new DevGasAmount(i, amount));
                }

                DevCellKind kind = open is turf_open_space ? DevCellKind.Space : DevCellKind.Open;
                report.Cells.Add(new DevGasCell(kind, Dm.num(air.return_pressure()), air.temperature, amounts.ToArray()));
            }
        }

        return report;
    }

    private static DevGasInfo InfoOf(Type gas)
    {
        lock (GasInfos)
        {
            if (GasInfos.TryGetValue(gas, out DevGasInfo info)) return info;
            string id = Dm.initial(gas, (datum_gas p) => p.id) as string ?? DmTypes.PathOf(gas);
            string name = Dm.initial(gas, (datum_gas p) => p.name) ?? id;
            uint rgb = CellState.ParseColor(Dm.initial(gas, (datum_gas p) => p.primary_color) as string) >> 8;
            info = new DevGasInfo(Clip(id), Clip(name), rgb);
            GasInfos[gas] = info;
            return info;
        }
    }

    private static string Clip(string text) =>
        System.Text.Encoding.UTF8.GetByteCount(text) <= Limits.MaxDevGasTextBytes ? text : text.Substring(0, Limits.MaxDevGasTextBytes / 4);
}
