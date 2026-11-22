using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>M3 R2, D120, D137, D157: the standing eye is 1.3 m, at the eyes of the mob-scaled human (1.5 m under D137, 1.2 m under D120), one client setting next to the wall height.</summary>
public sealed class WalkFixEyeTests
{
    [Fact]
    public void TheStandingEyeIs1Point3MetresOneSettingNextToTheWallHeight()
    {
        Assert.Equal(1.3f, WorldScale.DefaultEyeHeight);
        Assert.Equal(WorldScale.DefaultEyeHeight, WorldScale.EyeHeight);
        Assert.Equal(WorldScale.EyeHeight, TileSpace.EyeHeight);
        Assert.Equal(WorldScale.EyeHeight, CameraLayer.StandingEye);
        Assert.Equal(0.4f, CameraLayer.LyingEye); // D119's 0.5 m at the mob scale
        Assert.Equal(0.7f, WorldScale.WallHeight - TileSpace.EyeHeight, 4); // the ceiling is 0.7 m above the eye
        try
        {
            Assert.True(WorldScale.SetEyeHeight(1.4f));
            Assert.Equal(1.4f, TileSpace.EyeHeight);
            Assert.Equal(1.4f, CameraLayer.StandingEye);
            var layer = new CameraLayer();
            Assert.Equal(1.4f, layer.EyeHeight);
            Assert.False(WorldScale.SetEyeHeight(0.3f));
            Assert.False(WorldScale.SetEyeHeight(2.5f));
            Assert.False(WorldScale.SetEyeHeight(float.NaN));
            Assert.Equal(1.4f, WorldScale.EyeHeight);
        }
        finally
        {
            WorldScale.ResetEyeHeight();
        }
        Assert.Equal(1.3f, TileSpace.EyeHeight);
        Assert.True(WorldScale.MinEyeHeight > CameraLayer.LyingEye);
        Assert.True(WorldScale.MaxEyeHeight < WorldScale.MinWallHeight);
    }

    [Fact]
    public void TheCameraLayerStartsAndEasesAtTheNewEyeInAboutFourTenthsOfASecond()
    {
        var layer = new CameraLayer();
        Assert.Equal(1.3f, layer.EyeHeight);
        layer.Update(0.1, lying: true, 0f, false);
        Assert.True(layer.EyeHeight < 1.3f);
        for (int i = 0; i < 20; i++)
            layer.Update(0.1, lying: true, 0f, false);
        Assert.Equal(0.4f, layer.EyeHeight);
        // Getting up: 0.9 m at the layer's speed is about 0.4 s (1 m under D137's 1.5 m eye; 0.7 m at 1.8 m/s under the 1.2 m eye, 1.1 m at 2.8 m/s under the 1.6 m one).
        float seconds = (CameraLayer.StandingEye - CameraLayer.LyingEye) / CameraLayer.EyeSpeed;
        Assert.InRange(seconds, 0.35f, 0.45f);
        for (int i = 0; i < 20; i++)
            layer.Update(0.1, lying: false, 0f, false);
        Assert.Equal(1.3f, layer.EyeHeight);
    }

    [Fact]
    public void TheEyeHeightComesFromTheArgumentOrTheOptionsFile()
    {
        ClientArgs args = ClientArgs.Parse(new[] { "--fake-server", "--eye-height=1.35" });
        Assert.Empty(args.Errors);
        Assert.Equal(1.35f, args.EyeHeight);
        ClientArgs spaced = ClientArgs.Parse(new[] { "--eye-height", "1.1" });
        Assert.Equal(1.1f, spaced.EyeHeight);
        Assert.Null(ClientArgs.Parse(new[] { "--fake-server" }).EyeHeight);
        ClientArgs bad = ClientArgs.Parse(new[] { "--eye-height=3" });
        Assert.Null(bad.EyeHeight);
        Assert.Single(bad.Errors);

        var options = new PlayOptions();
        Assert.Equal(1.3f, options.EyeHeight);
        Assert.Contains("eye_height=1.3", options.ToText());
        var errors = new List<string>();
        PlayOptions read = PlayOptions.Parse("eye_height=1.45\nwall_height=2.4\n", errors);
        Assert.Empty(errors);
        Assert.Equal(1.45f, read.EyeHeight); // an old file's wall height is ignored: the server's now (protocol 91)
        PlayOptions.Parse("eye_height=9\n", errors);
        Assert.Single(errors);
        Assert.Equal(1.45f, PlayOptions.Parse(read.ToText(), null).EyeHeight);
    }

    [Fact]
    public void AnOptionsFileFromBeforeD137GetsTheNewDefaultsItStillHad()
    {
        // the owner's file of 2026-10-13: no version, the eye at the old default, the dev menu on F8
        PlayOptions old = PlayOptions.Parse("eye_height=1.2\nbind.dev_menu=F8\nbind.debug_hud=F4\n", null);
        Assert.Equal(1.3f, old.EyeHeight);
        Assert.Equal(new[] { "Backslash" }, old.Bindings.KeysOf(PlayAction.DevMenu));
        Assert.Equal(new[] { "F12" }, old.Bindings.KeysOf(PlayAction.Fullscreen));
        Assert.Equal(PlayOptions.FileVersion, old.ReadVersion);

        // a value the player chose stays
        PlayOptions chosen = PlayOptions.Parse("eye_height=1.35\nbind.dev_menu=F7\n", null);
        Assert.Equal((1.35f, "F7"), (chosen.EyeHeight, chosen.Bindings.KeysOf(PlayAction.DevMenu)[0]));

        // a file of version 2 keeps even the older values
        PlayOptions current = PlayOptions.Parse("options_version=2\neye_height=1.2\nbind.dev_menu=F8\n", null);
        Assert.Equal((1.2f, "F8"), (current.EyeHeight, current.Bindings.KeysOf(PlayAction.DevMenu)[0]));
        Assert.Contains("options_version=" + PlayOptions.FileVersion, new PlayOptions().ToText());
        Assert.Equal(1.2f, PlayOptions.Parse(current.ToText(), null).EyeHeight);
    }

    [Fact]
    public void AnOptionsFileFromBeforeD157WithD137sEyeGetsTheNewOne()
    {
        // D137's default 1.5 m in a version 2 file becomes D157's 1.3 m; a chosen value stays
        PlayOptions d137 = PlayOptions.Parse("options_version=2\neye_height=1.5\n", null);
        Assert.Equal((1.3f, PlayOptions.FileVersion), (d137.EyeHeight, d137.ReadVersion));
        Assert.Equal(1.35f, PlayOptions.Parse("options_version=2\neye_height=1.35\n", null).EyeHeight);
        // a version 3 file keeps 1.5 m
        Assert.Equal(1.5f, PlayOptions.Parse("options_version=3\neye_height=1.5\n", null).EyeHeight);
    }

    [Fact]
    public void TheStandingEyeIsAtTheEyesOfTheHumanDrawnAtTheMobScale()
    {
        // tg's human_eyes.dmi: the eyes are the 26th pixel row above the feet (their middle 25.5 pixels up); a pixel is 2 m / 32 at the tile scale
        float metresPerPixel = TileSpace.TileSize / 32f * WorldScale.MobScale;
        Assert.Equal(1.275f, 25.5f * metresPerPixel, 4);
        Assert.Equal(1.45f, 29f * metresPerPixel, 4); // the top of the head (the human is 29 rows tall)
        Assert.InRange(WorldScale.DefaultEyeHeight, 25.5f * metresPerPixel - 0.05f, 25.5f * metresPerPixel + 0.05f);
        Assert.True(WorldScale.DefaultEyeHeight < 29f * metresPerPixel, "the eye is under the top of the head");
    }
}
