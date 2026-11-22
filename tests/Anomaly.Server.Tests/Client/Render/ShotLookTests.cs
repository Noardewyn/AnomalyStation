using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>D176: the client's own 3D shots: the kind from the type path, tg's colour rule, the sizes, the own gun's kick and muzzle.</summary>
public class ShotLookTests
{
    [Theory]
    [InlineData("/obj/projectile/beam/laser", ShotKind.Bolt)]
    [InlineData("/obj/projectile/beam/disabler", ShotKind.Bolt)]
    [InlineData("/obj/projectile/beam/emitter/hitscan", ShotKind.Bolt)]
    [InlineData("/obj/projectile/beam/practice", ShotKind.ShortBolt)]
    [InlineData("/obj/projectile/beam/laser/rapid", ShotKind.ShortBolt)]
    [InlineData("/obj/projectile/beam/laser/rapid/practice", ShotKind.ShortBolt)]
    [InlineData("/obj/projectile/energy/electrode", ShotKind.Spark)]
    [InlineData("/obj/projectile/ion/weak", ShotKind.Ion)]
    [InlineData("/obj/projectile/temp", ShotKind.Temp)]
    [InlineData("/obj/projectile/temp/hot", ShotKind.Temp)]
    [InlineData("/obj/projectile/bullet/c38", ShotKind.Bullet)]
    [InlineData("/obj/projectile/bullet/pellet/shotgun_buckshot", ShotKind.Pellet)]
    [InlineData("/obj/effect/projectile/tracer/laser/emitter", ShotKind.Tracer)]
    [InlineData("/obj/effect/projectile/muzzle/disabler", ShotKind.Muzzle)]
    [InlineData("/obj/effect/projectile/impact/laser", ShotKind.Impact)]
    [InlineData("/obj/effect/temp_visual/impact_effect", ShotKind.Impact)]
    [InlineData("/obj/effect/temp_visual/impact_effect/red_laser/wall", ShotKind.Impact)]
    [InlineData("/obj/effect/temp_visual/dir_setting/firing_effect", ShotKind.Firing)]
    [InlineData("/obj/effect/temp_visual/dir_setting/firing_effect/blue", ShotKind.Firing)]
    [InlineData("/obj/projectile/bullet/a40mm", ShotKind.None)] // a 40mm shell is drawn as its picture
    [InlineData("/obj/projectile/bullet/a40mm/incendiary", ShotKind.None)]
    [InlineData("/obj/projectile/bullet/rocket", ShotKind.None)] // a shell: drawn as its picture with a fire trail
    [InlineData("/obj/projectile/bullet/rocket/heap", ShotKind.None)]
    [InlineData("/obj/projectile/energy/tesla", ShotKind.None)]
    [InlineData("/obj/projectile/magic", ShotKind.None)]
    [InlineData("/obj/projectile/beamx", ShotKind.None)]
    [InlineData("/obj/effect/ebeam", ShotKind.None)]
    [InlineData("/obj/effect/temp_visual/dir_setting", ShotKind.None)]
    [InlineData("/mob/living/carbon/human", ShotKind.None)]
    [InlineData(null, ShotKind.None)]
    public void The_kind_comes_from_the_type_path(string path, ShotKind kind)
    {
        Assert.Equal(kind, ShotLook.KindOf(path));
        Assert.Equal(kind is >= ShotKind.Bolt and <= ShotKind.Pellet, ShotLook.IsProjectile(kind));
    }

    [Fact]
    public void Shots_and_their_effects_are_never_static_props()
    {
        Assert.False(StaticPropRule.IsCandidate("/obj/projectile/beam/laser", false));
        Assert.False(StaticPropRule.IsCandidate("/obj/effect/temp_visual/impact_effect", false));
        Assert.False(StaticPropRule.IsCandidate("/obj/effect/projectile/tracer/laser", false));
        Assert.False(StaticPropRule.IsCandidate("/obj/projectile/magic", false));
        Assert.False(StaticPropRule.IsCandidate("/obj/effect/ebeam", false));
        Assert.True(StaticPropRule.IsCandidate("/obj/effect/temp_visual/dir_setting", false));
        Assert.True(StaticPropRule.IsCandidate("/obj/item/gun/energy/laser", false));
    }

    [Fact]
    public void The_colour_is_the_light_else_the_color_else_the_picture()
    {
        // a red picture: two opaque red pixels and a clear one
        byte[] red = { 200, 0, 0, 255, 200, 0, 0, 255, 0, 0, 255, 0 };
        var light = new LightState(1f, 1.4f, 0xFA8282FF, true, 0f, 0, 0f, 0f, 0f, 0); // the laser's COLOR_SOFT_RED
        (float r, float g, float b) = ShotLook.Colour(light, 0xFFFF00FF, red);
        Assert.Equal((0xFA / 255f, 0x82 / 255f, 0x82 / 255f), (r, g, b));
        (r, g, b) = ShotLook.Colour(LightState.None, 0xFFFF00FF, red); // the electrode's COLOR_YELLOW
        Assert.Equal((1f, 1f, 0f), (r, g, b));
        (r, g, b) = ShotLook.Colour(LightState.None, 0xFFFFFFFF, red);
        Assert.Equal(200 / 255f, r, 4);
        Assert.Equal(0f, g);
        Assert.Equal(0f, b);
    }

    [Fact]
    public void Sizes_follow_the_design()
    {
        Assert.Equal(1.2f, ShotLook.Size(ShotKind.Bolt).Length);
        Assert.True(ShotLook.Size(ShotKind.ShortBolt).Length < ShotLook.Size(ShotKind.Bolt).Length);
        Assert.Equal(0.6f, ShotLook.Size(ShotKind.Bullet).Length);
        // the size of a bullet follows its calibre: a pistol round is smaller than a rifle round, a slug and an anti-materiel round bigger still
        Assert.True(ShotLook.Calibre("/obj/projectile/bullet/c9mm") < ShotLook.Calibre("/obj/projectile/bullet/junk"));
        Assert.True(ShotLook.Calibre("/obj/projectile/bullet/a223/weak") > ShotLook.Calibre("/obj/projectile/bullet/c46x30mm/ap"));
        Assert.True(ShotLook.Calibre("/obj/projectile/bullet/p50") > ShotLook.Calibre("/obj/projectile/bullet/shotgun_slug"));
        Assert.Equal(1f, ShotLook.Calibre("/obj/projectile/beam/laser"));
        // a shot the server places 78 pixels on a tick is drawn at that even pace: between its places, before the first one (it comes out of
        // the gun at the same pace, not slower) and no more than a tick past the last; one place alone gives no pace before it
        (double, double)[] places = [(10, 30), (11, 108), (12, 186)];
        Assert.Equal(69, ShotLook.AlongAt(places, 10.5)!.Value, 6);
        Assert.Equal(-9, ShotLook.AlongAt(places, 9.5)!.Value, 6);
        Assert.Equal(264, ShotLook.AlongAt(places, 15)!.Value, 6);
        Assert.Null(ShotLook.AlongAt([(10, 30)], 9.5));
        Assert.Equal(30, ShotLook.AlongAt([(10, 30)], 11)!.Value, 6);
        Assert.True(ShotLook.Trail(ShotKind.Bolt).Width > ShotLook.Trail(ShotKind.Bullet).Width);
        Assert.True(ShotLook.Size(ShotKind.Pellet).Radius < ShotLook.Size(ShotKind.Bullet).Radius);
        Assert.Equal(2 * ShotLook.Size(ShotKind.Spark).Radius, ShotLook.Size(ShotKind.Spark).Length);
    }

    [Fact]
    public void A_projectile_is_kept_ahead_of_its_lines_start_and_under_the_ceiling()
    {
        Assert.Equal(0, ShotLook.ClampAlong(-12, 20.8, 0)); // its tile's middle behind the eye: not behind the shooter
        Assert.Equal(500, ShotLook.ClampAlong(500, 20.8, 0)); // a level shot flies on
        Assert.Equal(11.2, ShotLook.ClampAlong(16, 20.8, 1), 6); // a rising shot ends at the ceiling (32)
        Assert.Equal(20.8, ShotLook.ClampAlong(40, 20.8, -1), 6); // a falling one at the floor
        Assert.Equal(11.2, ShotLook.ClampAlong(0, -11.2, 1), 6); // come up into the next level: from its floor on
        Assert.Equal(48 - 20.8, ShotLook.ClampAlong(100, 20.8, 1, 48), 6); // a 3 m level's ceiling (the server's wall height)
        // a shot is hidden its first 1.5 m, but no more than half the way to the ceiling or the floor it meets, so it is seen flying into it
        Assert.Equal(ShotLook.HiddenNearMetres, ShotLook.HiddenNear(double.MaxValue));
        Assert.Equal(0.5f, ShotLook.HiddenNear(1.0), 6);
        // a homing shot steered a few degrees is the same flight, across north too; a ricochet back is a new one
        Assert.True(ShotLook.SameFlight(90, 95));
        Assert.True(ShotLook.SameFlight(355, 10));
        Assert.False(ShotLook.SameFlight(90, 270));
        Assert.False(ShotLook.SameFlight(0, 45));
    }

    [Fact]
    public void The_flash_fades_and_the_kick_comes_back()
    {
        Assert.Equal(1f, ShotLook.Fade(0, ShotLook.FlashSeconds));
        Assert.Equal(0.25f, ShotLook.Fade(ShotLook.FlashSeconds / 2, ShotLook.FlashSeconds), 4);
        Assert.Equal(0f, ShotLook.Fade(ShotLook.FlashSeconds, ShotLook.FlashSeconds));
        Assert.Equal(0f, ShotLook.Kick(0));
        Assert.Equal(1f, ShotLook.Kick(0.03), 4);
        Assert.True(ShotLook.Kick(0.1) is > 0f and < 1f);
        Assert.Equal(0f, ShotLook.Kick(0.2));
        Assert.Equal(0f, ShotLook.Kick(-1));
    }

    [Fact]
    public void The_muzzle_is_the_middle_of_the_rightmost_drawn_column()
    {
        // 4 x 3: drawn pixels in column 1 (rows 0 to 2) and column 2 (rows 1 and 2), nothing in column 3
        const int w = 4, h = 3;
        var rgba = new byte[w * h * 4];
        foreach ((int x, int y) in new[] { (1, 0), (1, 1), (1, 2), (2, 1), (2, 2) })
            rgba[(y * w + x) * 4 + 3] = 255;
        Assert.True(ShotLook.TryMuzzle(rgba, w, h, out float mx, out float my));
        Assert.Equal(3f, mx);
        Assert.Equal(2f, my);
        Assert.False(ShotLook.TryMuzzle(new byte[w * h * 4], w, h, out _, out _));
    }
}
