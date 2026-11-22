using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M10x slice C (protocol 51): the camera through an observed mob's eyes (DV201) and BYOND's double click.</summary>
public class ObserverViewTests
{
    [Fact]
    public void The_camera_looks_through_another_drawn_mob_only()
    {
        Assert.True(ObserverVision.Observing(own: 5, eye: 9, eyeDrawn: true));
        Assert.False(ObserverVision.Observing(5, 9, eyeDrawn: false)); // not received yet: the own view
        Assert.False(ObserverVision.Observing(5, 0, true));
        Assert.False(ObserverVision.Observing(5, 5, true));
        Assert.Equal(CameraLayer.StandingEye, ObserverVision.EyeHeight(0));
        Assert.Equal(CameraLayer.LyingEye, ObserverVision.EyeHeight(PostureBits.Encode(true, 90)));
        // the replicated yaw byte as the rig's Godot yaw: compass east (64) is Godot's -90 degrees, west (192) +90
        Assert.Equal(0f, ObserverVision.Yaw(0), 1);
        Assert.Equal(-MathF.PI / 2, ObserverVision.Yaw(64), 1);
        Assert.Equal(MathF.PI / 2, ObserverVision.Yaw(192), 1);
    }

    [Fact]
    public void Two_clicks_on_one_thing_within_the_window_are_a_double_click_and_a_third_starts_over()
    {
        var model = new DoubleClick();
        var bob = new ClickTarget(TargetKind.Entity, 7, 0, 0, 0, 16, 16);
        var turf = new ClickTarget(TargetKind.Turf, 0, 3, 4, 1, 1, 1);
        Assert.False(model.Clicked(bob, ClickButton.Left, 1.0));
        Assert.True(model.Clicked(bob with { IconX = 3 }, ClickButton.Left, 1.3)); // the pixel may differ
        Assert.False(model.Clicked(bob, ClickButton.Left, 1.4)); // a third press starts over
        Assert.False(model.Clicked(bob, ClickButton.Left, 1.4 + DoubleClick.Window + 0.01));
        Assert.False(model.Clicked(turf, ClickButton.Left, 3.0));
        Assert.False(model.Clicked(turf, ClickButton.Right, 3.1)); // another button
        Assert.False(model.Clicked(bob, ClickButton.Left, 3.2)); // another thing
        Assert.False(model.Clicked(ClickTarget.None, ClickButton.Left, 3.3));
        Assert.False(model.Clicked(ClickTarget.None, ClickButton.Left, 3.4)); // nothing is never doubled
    }

    [Fact]
    public void ACameraEyeIsSeenFromItsOrbitAndAnObservedBodyThroughItsEyes()
    {
        // DV382: tg's /mob/eye/camera and its kinds; DV201: a mob observed by a ghost
        Assert.True(ObserverVision.IsCameraEye("/mob/eye/camera/remote/shuttle_docker"));
        Assert.True(ObserverVision.IsCameraEye("/mob/eye/camera/remote"));
        Assert.False(ObserverVision.IsCameraEye("/mob/living/carbon/human"));
        Assert.False(ObserverVision.IsCameraEye("/mob/eye/blob"));
        Assert.False(ObserverVision.IsCameraEye(null));
    }
}

