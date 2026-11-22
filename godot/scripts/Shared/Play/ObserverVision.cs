using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

public static class ObserverVision
{
    // DV174: the server gives a corpse SEE_MOBS | SEE_OBJS without SEE_TURFS; its dark is lifted only by its own night-vision action
    public static bool CorpseSight(VisionState vision, bool ghost, byte posture)
        => !ghost && PostureBits.IsLimp(posture)
            && (vision.Sight & (SightFlags.SeeMobs | SightFlags.SeeObjs)) == (SightFlags.SeeMobs | SightFlags.SeeObjs);

    public static bool Mesons(VisionState vision, bool ghost, byte posture = 0)
        => !ghost && !CorpseSight(vision, ghost, posture) && vision.SeesTurfs;

    // DV374: a living mob with SEE_MOBS (tg's TRAIT_THERMAL_VISION, TRAIT_XRAY_VISION) sees the other mobs behind the walls
    public static bool SeesMobs(VisionState vision, bool ghost, byte posture = 0)
        => !ghost && !CorpseSight(vision, ghost, posture) && (vision.Sight & SightFlags.SeeMobs) != 0;

    // M10x slice C (protocol 51, DV201): tg's do_observe sets client.eye to the observed mob; the camera is then that mob's first person
    // view, while the client draws the mob (its pose known), at its eye height, looking where it faces, level.

    /// <summary>Whether the camera looks through another mob's eyes: the replica's eye names a mob other than the own one that is drawn.</summary>
    public static bool Observing(long own, long eye, bool eyeDrawn) => eye != 0 && eye != own && eyeDrawn;

    /// <summary>
    /// DEVIATION(DV382): whether the mob the client looks from is a camera eye (tg's <c>/mob/eye/camera</c>: an advanced camera console's, a
    /// navigation computer's), which the camera sees from a ghost's orbit, turned by the mouse, rather than through its eyes (DV201).
    /// </summary>
    public static bool IsCameraEye(string typePath) => typePath != null && typePath.StartsWith("/mob/eye/camera", StringComparison.Ordinal);

    /// <summary>The observed mob's eye above its floor: the camera layer's standing or lying eye.</summary>
    public static float EyeHeight(byte posture) => PostureBits.IsLying(posture) ? CameraLayer.LyingEye : CameraLayer.StandingEye;

    /// <summary>The Godot yaw of the observed mob's replicated yaw byte.</summary>
    public static float Yaw(byte yaw) => (float)MoveWish.GodotYaw(MoveWish.FromYawByte(yaw));
}
