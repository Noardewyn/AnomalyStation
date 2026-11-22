using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// DV46 (the space play test, 2026-10-15): tg's particle effects near the own body. A particle of the own body is drawn from the drawn body
/// (moved by the view's lag when it appears), any particle fades out within a tile of the eye, particles are never static props.
/// </summary>
public class NearEyeFadeTests
{
    [Fact]
    public void Particles_fade_out_within_a_tile_of_the_eye()
    {
        Assert.True(NearEyeFade.Applies("/obj/effect/particle_effect/ion_trails/flight"));
        Assert.True(NearEyeFade.Applies("/obj/effect/particle_effect/water/extinguisher"));
        Assert.False(NearEyeFade.Applies("/obj/effect/hotspot"));
        Assert.False(NearEyeFade.Applies("/mob/living/carbon/human"));
        Assert.Equal(0f, NearEyeFade.Alpha(0f, 0f));
        Assert.Equal(0f, NearEyeFade.Alpha(0.3f, -0.3f));
        Assert.Equal(0.5f, NearEyeFade.Alpha(0.75f, 0f), 3);
        Assert.Equal(1f, NearEyeFade.Alpha(0f, 1f));
        Assert.Equal(1f, NearEyeFade.Alpha(-3f, 2f));
        Assert.False(StaticPropRule.IsCandidate("/obj/effect/particle_effect/ion_trails", false));
        Assert.True(StaticPropRule.IsCandidate("/obj/item/crowbar", false));
        Assert.False(StaticPropRule.IsCandidate(StaticPropRule.BlastType, false)); // D303: a burst of puffs, not a flat sprite on the walls
        Assert.False(StaticPropRule.IsCandidate("/obj/effect/temp_visual/point", false)); // DV232: the arrow stands on the pointed point
        Assert.False(StaticPropRule.IsCandidate("/obj/effect/temp_visual/point/holo", false));
    }

    [Fact]
    public void A_particle_of_the_own_body_is_drawn_from_where_the_body_is_drawn()
    {
        // Flying east: the server's mob stepped from 40 to 41 and left a trail particle on 40, while the body is drawn at 39.8 (a step
        // behind, OwnGlide). The particle is drawn 1.2 tiles back from 40: a step behind the drawn body, not 0.2 tile in front of the eye.
        (float sx, float sy) = NearEyeFade.OwnShift(40, 116, 1, 41, 116, 1, 39.8f, 116f);
        Assert.Equal((-1.2f, 0f), (sx, sy), new Tolerance());
        Assert.True(40 + sx < 39.8f);
        // Another flier's particle three tiles off, another level, or a lag of a jump: where tg put it.
        Assert.Equal((0f, 0f), NearEyeFade.OwnShift(44, 116, 1, 41, 116, 1, 39.8f, 116f));
        Assert.Equal((0f, 0f), NearEyeFade.OwnShift(40, 116, 2, 41, 116, 1, 39.8f, 116f));
        Assert.Equal((0f, 0f), NearEyeFade.OwnShift(40, 116, 1, 41, 116, 1, 30f, 116f));
    }

    private sealed class Tolerance : IEqualityComparer<(float, float)>
    {
        public bool Equals((float, float) a, (float, float) b) => Math.Abs(a.Item1 - b.Item1) < 1e-4 && Math.Abs(a.Item2 - b.Item2) < 1e-4;
        public int GetHashCode((float, float) obj) => 0;
    }
}
