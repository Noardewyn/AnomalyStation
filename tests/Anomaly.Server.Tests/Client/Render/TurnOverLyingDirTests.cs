using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>
/// Package T, slice T4c (DV573, DV600): which side of a lying body is drawn. One rule on the client (<c>BillboardMath.ChooseLyingDir</c>) and its
/// mirror for the server's shots (<c>ShotGeometry.ChooseLyingDir</c>): a turned body shows the side it was turned to, one lying buckled to an object
/// its front, from every side; otherwise the owner's rule of 2026-10-13 (conscious: its back; limp: by the viewer's side).
/// </summary>
public class TurnOverLyingDirTests
{
    // The body lies at (10, 10) facing north (yaw 0); viewers on its four sides.
    private static readonly (double X, double Y, string Side)[] Viewers = { (10, 15, "north"), (10, 5, "south"), (15, 10, "east"), (5, 10, "west") };

    private static byte Expected(bool? turnedFaceUp, bool buckled, bool limp, string side)
    {
        if (turnedFaceUp is bool up)
            return up ? ByondDir.South : ByondDir.North;
        if (buckled)
            return ByondDir.South;
        if (!limp)
            return ByondDir.North;
        return side == "south" ? ByondDir.North : ByondDir.South; // the limp body faces north: only a viewer behind it sees its back
    }

    [Fact]
    public void Every_combination_draws_the_rules_side_and_the_server_mirrors_it()
    {
        int checkedCases = 0;
        foreach (bool? turned in new bool?[] { null, true, false })
            foreach (bool buckled in new[] { false, true })
                foreach (bool limp in new[] { false, true })
                    foreach ((double vx, double vy, string side) in Viewers)
                    {
                        byte client = BillboardMath.ChooseLyingDir(0, 10, 10, vx, vy, 4, limp, buckled, turned);
                        byte server = ShotGeometry.ChooseLyingDir(0, 10, 10, vx, vy, limp, buckled, turned);
                        string what = $"turned {turned?.ToString() ?? "none"}, buckled {buckled}, limp {limp}, viewer {side}";
                        Assert.True(Expected(turned, buckled, limp, side) == client, what + ": client " + client);
                        Assert.True(client == server, what + ": the server chose " + server + ", the client " + client);
                        checkedCases++;
                    }
        Assert.Equal(3 * 2 * 2 * 4, checkedCases);
    }

    [Fact]
    public void The_mirror_holds_for_any_yaw_and_position_and_the_byte_carries_the_inputs()
    {
        var rng = new Random(588);
        for (int i = 0; i < 5000; i++)
        {
            byte yaw = (byte)rng.Next(256);
            double mx = rng.NextDouble() * 50, my = rng.NextDouble() * 50, vx = rng.NextDouble() * 50, vy = rng.NextDouble() * 50;
            bool limp = rng.Next(2) == 0, buckled = rng.Next(2) == 0;
            bool? turned = rng.Next(3) switch { 0 => null, 1 => true, _ => false };
            // the client reads the three inputs from the replicated byte, the server from the mob: the byte must carry them unchanged
            byte posture = PostureBits.Encode(true, 90, limp, buckled: buckled, turnedFaceUp: turned);
            Assert.Equal(turned, PostureBits.TurnedFaceUp(posture));
            Assert.Equal(buckled, PostureBits.IsBuckled(posture));
            Assert.Equal(BillboardMath.ChooseLyingDir(yaw, mx, my, vx, vy, 4, PostureBits.IsLimp(posture), PostureBits.IsBuckled(posture), PostureBits.TurnedFaceUp(posture)),
                ShotGeometry.ChooseLyingDir(yaw, mx, my, vx, vy, limp, buckled, turned));
        }
    }

    [Fact]
    public void A_still_icon_stays_south_and_the_old_callers_rule_is_unchanged()
    {
        Assert.Equal(ByondDir.South, BillboardMath.ChooseLyingDir(0, 10, 10, 10, 5, 1, limp: false, buckled: false, turnedFaceUp: false));
        // without the two new inputs (a body on the floor no one turned): the owner's rule as before
        Assert.Equal(ByondDir.North, BillboardMath.ChooseLyingDir(0, 10, 10, 10, 15, 4, limp: false));
        Assert.Equal(ByondDir.South, BillboardMath.ChooseLyingDir(0, 10, 10, 10, 15, 4, limp: true));
        Assert.Equal(ByondDir.North, BillboardMath.ChooseLyingDir(0, 10, 10, 10, 5, 4, limp: true));
    }
}
