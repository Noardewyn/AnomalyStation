using System.Diagnostics;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>DV586 (the owner, 2026-11-21: a swing of a knife or a sword was silent): the own swing whooshes by what the hand holds.</summary>
public class SwingSoundTests(ITestOutputHelper output)
{
    [Fact]
    public void AKnifeSlashesAToolboxSwingsHardAndAFistWhiffs()
    {
        var watch = Stopwatch.StartNew();
        Dictionary<(string Icon, string State), SwingKind> looks = SwingSound.Looks(new ReflectionTypeSource());
        output.WriteLine($"{looks.Count} pictures in {watch.ElapsedMilliseconds} ms");
        SwingKind Of(string path, bool longThing = false)
        {
            Assert.True(new ReflectionTypeSource().TryGet(path, out TypeDefaults d), path);
            return SwingSound.KindOf(looks, d.IconAfterInit(null), d.StateAfterInit(null), longThing);
        }
        Assert.Equal(SwingKind.Sharp, Of("/obj/item/knife/kitchen"));
        Assert.Equal(SwingKind.Sharp, Of("/obj/item/claymore"));
        Assert.Equal(SwingKind.Sharp, Of("/obj/item/scalpel"));
        Assert.Equal(SwingKind.Pointy, Of("/obj/item/screwdriver")); // tg's SHARP_POINTY: it stabs
        Assert.Equal(SwingKind.Pointy, Of("/obj/item/spear"));
        Assert.Equal(SwingKind.Pointy, SwingSound.KindOf(looks, "icons/obj/weapons/spear.dmi", "spearglass1", false)); // wielded
        Assert.Equal(SwingKind.Pointy, SwingSound.KindOf(looks, "gags:/datum/greyscale_config/screwdriver|#18a2d5", "screwdriver", false)); // a random colour of it
        Assert.Equal(SwingSound.SlashOne, SwingSound.FileOf(SwingKind.Pointy, 0));
        Assert.Equal(SwingKind.Heavy, Of("/obj/item/storage/toolbox/mechanical"));
        Assert.Equal(SwingKind.Light, Of("/obj/item/bikehorn"));
        Assert.Equal(SwingKind.Heavy, Of("/obj/item/bikehorn", longThing: true)); // a long picture swings heavy
        Assert.Equal(SwingKind.Fist, SwingSound.KindOf(looks, null, null, false));
        Assert.Equal(SwingSound.SlashOne, SwingSound.FileOf(SwingKind.Sharp, 0));
        Assert.Equal(SwingSound.SlashTwo, SwingSound.FileOf(SwingKind.Sharp, 1)); // the slashes take turns
        Assert.Equal(SwingSound.SlashOne, SwingSound.FileOf(SwingKind.Sharp, 2));
        foreach (string file in new[] { SwingSound.Fist, SwingSound.Soft, SwingSound.Hard, SwingSound.SlashOne, SwingSound.SlashTwo })
            Assert.True(System.IO.File.Exists(System.IO.Path.Combine(RepoRoot(), "godot", "assets", "tg", file)), file);
    }

    private static string RepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!System.IO.File.Exists(System.IO.Path.Combine(dir, "AnomalyStation.sln")))
            dir = System.IO.Path.GetDirectoryName(dir)!;
        return dir;
    }
}
