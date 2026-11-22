using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Protocol version 21 (M5x slice 2): the camera shake. tg's shake_camera animates the player's BYOND client (its pixel_x and
// pixel_y, mob_helpers.dm:164); the presentation sink's AnimateStep reports each step with the client as its subject. This keeps the
// tick's last chain per client (a call with an object starts a chain and replaces the one before, as BYOND's animate does) and turns
// it into the session's CameraShake message after the tick.

public sealed partial class Replicator
{
    // The animate chains on clients since the last BuildPrivate: the client to its steps (the last chain started this tick) and the
    // world tick it was recorded in (a client no session takes it from, a bot's, is dropped two ticks later: PruneCameraChains).
    private readonly Dictionary<client, List<CameraShakeStep>> _cameraChains = new();
    private readonly Dictionary<client, long> _cameraChainTicks = new();

    private void NoteCameraStep(client subject, bool startsChain, DmList arguments)
    {
        if (startsChain || !_cameraChains.TryGetValue(subject, out List<CameraShakeStep> steps))
            _cameraChains[subject] = steps = new List<CameraShakeStep>();
        _cameraChainTicks[subject] = _world?.Tick ?? 0;
        if (steps.Count >= Limits.MaxCameraShakeSteps)
            return; // a path longer than the cap ends early (tg's shakes take one step a decisecond or so)
        // M8 slice B (DV106): a relative step (ANIMATION_RELATIVE: tg's dizziness wobble) ends where the shim has already put the client's
        // vars (the step applies before the sink hears it), so its point is the client's pixel_x and pixel_y, not the step's offsets
        bool relative = ((int)Dm.num(arguments["flags"]) & (int)Dm.ANIMATION_RELATIVE) != 0;
        float Value(string var) => relative ? (var == "pixel_x" ? subject.pixel_x : subject.pixel_y) : Dm.num(arguments[var]);
        int Pixels(string var) => (int)Math.Clamp(Math.Round(Value(var)), -Limits.MaxCameraShakePixels, Limits.MaxCameraShakePixels);
        int time = (int)Math.Clamp(Math.Round(Dm.num(arguments["time"])), 1, Limits.MaxCameraShakeStepTime);
        steps.Add(new CameraShakeStep(Pixels("pixel_x"), Pixels("pixel_y"), time));
    }

    private void PruneCameraChains(long tick)
    {
        if (_cameraChains.Count == 0)
            return;
        foreach (client stale in _cameraChainTicks.Where(kv => kv.Value < tick - 1).Select(kv => kv.Key).ToList())
        {
            _cameraChains.Remove(stale);
            _cameraChainTicks.Remove(stale);
        }
    }

    private sealed partial class SessionState
    {
        /// <summary>The CameraShake message of the last <see cref="Update"/>, or null.</summary>
        public CameraShakeMessage PendingCameraShake { get; set; }

        private void UpdateCameraShake(Replicator owner, client player)
        {
            PendingCameraShake = null;
            if (player == null || !owner._cameraChains.Remove(player, out List<CameraShakeStep> steps))
                return;
            owner._cameraChainTicks.Remove(player);
            if (steps.Count == 0)
                return;
            // version 34 (DV96): tg's recoil_camera kick or shake_camera's path, the client's last chain
            var message = new CameraShakeMessage { Kind = player.dm_camera_recoil ? CameraShakeKind.Recoil : CameraShakeKind.Shake };
            message.Steps.AddRange(steps);
            PendingCameraShake = message;
        }
    }

    /// <inheritdoc/>
    public CameraShakeMessage TakeCameraShake(int session)
    {
        if (!_sessions.TryGetValue(session, out SessionState state) || state.PendingCameraShake == null)
            return null;
        CameraShakeMessage taken = state.PendingCameraShake;
        state.PendingCameraShake = null;
        return taken;
    }
}
