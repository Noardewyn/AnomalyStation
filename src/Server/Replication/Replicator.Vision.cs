using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Version 31 (M7 slice T3, DV81): the own mob's vision. tg's /mob/living/update_sight leaves the mob's `sight`, `lighting_cutoff` and
// `lighting_color_cutoffs` set (the mesons: SEE_TURFS and the glasses' colour cutoffs); the port has no plane masters, so a session
// gets the three values in its private block whenever they change (a Snapshot's block only when they are not a person's normal sight).
public sealed partial class Replicator
{
    private sealed partial class SessionState
    {
        private VisionState _visionSent = VisionState.Normal;

        private void UpdateVision(client player, bool full)
        {
            VisionState now = VisionOf(player?.mob);
            OwnBlock block = Block;
            block.VisionChanged = full ? now != VisionState.Normal : now != _visionSent;
            block.Vision = now;
            _visionSent = now;
        }

        // The values are read raw (the replicator runs outside the world's step) and clamped to the wire's 0 to 100.
        private static VisionState VisionOf(mob who)
        {
            if (who == null || who.gc_destroyed != null)
                return VisionState.Normal;
            DmList colors = who.lighting_color_cutoffs;
            float Channel(int i) => colors != null && colors.len >= i ? Clamp100(Dm.num(colors[i])) : 0f;
            return new VisionState((int)who.sight & ((1 << 24) - 1), Clamp100(who.lighting_cutoff ?? 0f), Channel(1), Channel(2), Channel(3));
        }

        private static float Clamp100(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 100f ? 100f : v;
    }
}
