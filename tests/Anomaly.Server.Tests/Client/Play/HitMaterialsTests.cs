using Anomaly.Client.Shared.Play;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>The owner (2026-10-29): a hit's particles by what was hit (Render/HitBurst), not blood on a wall.</summary>
public class HitMaterialsTests
{
    [Theory]
    [InlineData("/mob/living/carbon/human", HitMaterial.Flesh)]
    [InlineData("/mob/living/carbon/alien/adult/hunter", HitMaterial.AlienFlesh)]
    [InlineData("/mob/living/silicon/robot", HitMaterial.Electronics)]
    [InlineData("/obj/structure/window/reinforced/fulltile", HitMaterial.Glass)]
    [InlineData("/obj/structure/table/wood/fancy", HitMaterial.Wood)]
    [InlineData("/obj/structure/table", HitMaterial.Metal)]
    [InlineData("/obj/structure/tables_are_not_tables", HitMaterial.Metal)] // a prefix is a whole path step
    [InlineData("/obj/machinery/computer/crew", HitMaterial.Electronics)]
    [InlineData(null, HitMaterial.Metal)]
    public void AThingIsMadeOfItsKindsMaterial(string path, HitMaterial material) => Assert.Equal(material, HitMaterials.OfThing(path));

    [Theory]
    [InlineData("/turf/closed/wall", HitMaterial.Metal)]
    [InlineData("/turf/closed/wall/r_wall", HitMaterial.Metal)]
    [InlineData("/turf/closed/wall/mineral/wood", HitMaterial.Wood)]
    [InlineData("/turf/closed/mineral/random", HitMaterial.Stone)]
    [InlineData("/turf/open/floor/iron", HitMaterial.Metal)]
    [InlineData("/turf/open/floor/glass/reinforced", HitMaterial.Glass)]
    public void ATurfIsMadeOfItsKindsMaterial(string path, HitMaterial material) => Assert.Equal(material, HitMaterials.OfTurf(path));

    [Fact]
    public void OnlyBodiesBleedAndHardThingsSpark()
    {
        HitDebris wall = HitMaterials.Look(HitMaterials.OfTurf("/turf/closed/wall"));
        HitDebris body = HitMaterials.Look(HitMaterial.Flesh);
        Assert.True(body.R > 0.5f && body.G < 0.1f && body.B < 0.1f, "blood is red");
        Assert.False(wall.R > 0.5f && wall.G < 0.1f, "a wall's debris is not blood red");
        Assert.True(wall.Sparks > 0);
        Assert.Equal(0, body.Sparks);
    }
}
