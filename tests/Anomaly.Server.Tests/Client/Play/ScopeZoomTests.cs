using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M7x slice D, DV98: the first-person scope's zoom (ScopeZoom.cs), engine-free.</summary>
public class ScopeZoomTests
{
    [Fact]
    public void The_scopes_fullscreen_is_the_one_in_the_fullscreens_file_with_a_scope_state()
    {
        Assert.True(ScopeZoom.IsScope("icons/hud/screen_full.dmi", "scope1"));
        Assert.True(ScopeZoom.IsScope("icons/hud/screen_full.dmi", "scope0"));
        Assert.False(ScopeZoom.IsScope("icons/hud/screen_full.dmi", "brutedamageoverlay3"));
        Assert.False(ScopeZoom.IsScope("icons/hud/screen_gen.dmi", "scope1"));
        Assert.False(ScopeZoom.IsScope(null, "scope1"));
        Assert.False(ScopeZoom.IsScope("icons/hud/screen_full.dmi", null));
    }

    [Theory]
    [InlineData(2.02f, 2f)] // the battle rifle
    [InlineData(2.015f, 1.5f)] // a hunting rifle
    [InlineData(2.04f, 4f)] // a sniper rifle, binoculars
    [InlineData(2.012f, 1.2f)] // a moth's cap
    [InlineData(2f, 1f)] // a fullscreen that carries no zoom
    [InlineData(2.0001f, 1f)]
    public void The_range_modifier_rides_in_the_hundredths_of_the_layer_above_the_fullscreen_layer(float layer, float expected)
        => Assert.Equal(expected, ScopeZoom.RangeModifier(layer), 2);

    [Theory]
    [InlineData(75f, 1f, 75f)]
    [InlineData(75f, 2f, 41.98f)] // 2 atan(tan(37.5 deg) / 2)
    [InlineData(90f, 4f, 28.07f)]
    public void The_field_of_view_narrows_so_that_the_tangent_of_its_half_falls_by_the_zoom(float fov, float zoom, float expected)
        => Assert.Equal(expected, ScopeZoom.FovDegrees(fov, zoom), 1);

    [Fact]
    public void The_zoom_settles_on_its_target_without_a_cut_and_without_overshoot()
    {
        float zoom = 1f;
        float previous = zoom;
        for (int frame = 0; frame < 6; frame++) // 60 frames a second
        {
            zoom = ScopeZoom.Approach(zoom, 2f, 1f / 60f);
            Assert.InRange(zoom, previous, 2f);
            previous = zoom;
        }
        Assert.InRange(zoom, 1.2f, 2f); // on its way, not yet there after a tenth of a second
        for (int frame = 0; frame < 120; frame++)
            zoom = ScopeZoom.Approach(zoom, 2f, 1f / 60f);
        Assert.Equal(2f, zoom);
        for (int frame = 0; frame < 120; frame++)
            zoom = ScopeZoom.Approach(zoom, 1f, 1f / 60f);
        Assert.Equal(1f, zoom);
    }
}
