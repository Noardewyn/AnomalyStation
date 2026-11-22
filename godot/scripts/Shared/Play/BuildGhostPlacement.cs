using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// D320 (DV352): where the build ghost stands. The server names the tile a click at what the player hovers would build on
// (BuildPreviewMessage, protocol version 71: reach, a free pipe layer, nothing taken away); the client draws it only while its crosshair
// aims at that tile (a turf, or a thing standing on it), so a ghost never lags on a tile the player has looked away from.

/// <summary>The tile of the build ghost, or none.</summary>
public static class BuildGhostPlacement
{
    /// <summary>Whether <paramref name="preview"/> shows now: the aimed turf or thing is on its tile.</summary>
    public static bool Shows(BuildPreviewMessage preview, in PickHit hit)
        => preview is { IsSet: true } && hit.IsHit && hit.Kind is TargetKind.Turf or TargetKind.Entity
            && hit.X == preview.X && hit.Y == preview.Y && hit.Z == preview.Z;
}
