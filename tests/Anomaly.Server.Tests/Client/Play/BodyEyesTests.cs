using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>D346: a possessed body that is not a human's sees from its picture's height; a human keeps D157's standing eye.</summary>
public class BodyEyesTests
{
    private static float Settle(CameraLayer layer, bool lying)
    {
        float up = 0f;
        for (int i = 0; i < 240; i++)
            up = layer.Update(1.0 / 120, lying, 0f, false).Up;
        return up;
    }

    [Fact]
    public void A_human_keeps_the_standing_eye_whatever_it_wears_and_a_monkey_sees_lower()
    {
        Assert.Null(BodyEyes.For("/mob/living/carbon/human", 1.45f));
        Assert.Null(BodyEyes.For("/mob/living/carbon/human", 1.75f)); // a tall hat
        Assert.Equal(0.9f * BodyEyes.EyeShare, BodyEyes.For("/mob/living/carbon/human/species/monkey", 0.9f).Value, 4);
        Assert.Null(BodyEyes.For("/mob/dead/observer", 1.4f));
        Assert.Null(BodyEyes.For("/mob/living/basic/pet/dog/corgi", 0f)); // no picture measured
    }

    [Fact]
    public void A_corgi_and_a_mouse_see_from_their_height()
    {
        float corgi = BodyEyes.For("/mob/living/basic/pet/dog/corgi", 0.7f).Value;
        Assert.Equal(0.7f * BodyEyes.EyeShare, corgi, 4);
        Assert.Equal(corgi, BodyEyes.For("/mob/living/basic/pet/dog/corgi", 0.7f, covered: false).Value, 4); // a small body: the same outdoors
        Assert.Equal(BodyEyes.MinEye, BodyEyes.For("/mob/living/basic/mouse", 0.1f).Value, 4);
    }

    [Fact]
    public void A_creature_taller_than_the_walls_sees_from_its_height_in_the_open_and_stays_under_a_ceiling()
    {
        const string drake = "/mob/living/simple_animal/hostile/megafauna/dragon";
        float tall = WorldScale.WallHeight + 1.5f; // a 3.5 m picture at the default 2 m walls
        Assert.Equal(tall * BodyEyes.EyeShare, BodyEyes.For(drake, tall, covered: false).Value, 4);
        Assert.True(BodyEyes.For(drake, tall, covered: false).Value > WorldScale.WallHeight);
        Assert.Equal(WorldScale.WallHeight - BodyEyes.UnderCeiling, BodyEyes.For(drake, tall, covered: true).Value, 4);
        Assert.Equal(BodyEyes.MaxEye, BodyEyes.For(drake, 50f, covered: false).Value, 4); // a picture made absurd by an effect
    }

    [Fact]
    public void The_camera_layer_eases_to_the_body_eye_and_lies_lower_still()
    {
        var layer = new CameraLayer { BodyEye = 0.6f };
        layer.Reset(false);
        Assert.Equal(0.6f, Settle(layer, false), 3);
        Assert.Equal(BodyEyes.Lying(0.6f), Settle(layer, true), 3);
        Assert.True(BodyEyes.Lying(0.6f) < 0.6f);
        layer.BodyEye = null; // back in the human body
        Assert.Equal(CameraLayer.StandingEye, Settle(layer, false), 3);
    }
}
