using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Render;
using Anomaly.Client.Shared.Models;
using Anomaly.Protocol;

namespace Anomaly.Client;

public partial class MapRenderer
{
    private readonly HashSet<StaticRecord> _overlayAnimated = new();
    private readonly Dictionary<StaticRecord, List<(OmniLight3D Light, Vector3 Position, Vector3 Pivot, ModelPart Part)>> _modelLights = new();

    /// <summary>Visible local lights from model indicators; useful for repeatable state checks.</summary>
    public int ModelLightCount => _modelLights.Values.Sum(lights => lights.Count(l => l.Light.Visible));

    private void ClearModelLights(StaticRecord record)
    {
        if (!_modelLights.Remove(record, out var lights))
            return;
        foreach (var light in lights)
        {
            light.Light.Visible = false;
            light.Light.QueueFree();
        }
    }

    private void AddModelLight(StaticRecord record, ModelPart part, Vector3 pivot)
    {
        ModelPointLight spec = part.Light;
        System.Numerics.Vector3 color = part.Color ?? spec.Color;
        var scale = ModelMesh.Scale(record.Model, TileSpace.TileSize, WorldScale.WallHeight, WorldScale.TableTop);
        var light = new OmniLight3D
        {
            Visible = false,
            LightColor = new Color(color.X, color.Y, color.Z),
            LightEnergy = spec.Energy,
            OmniRange = spec.Range * scale.Horizontal,
            DistanceFadeEnabled = true,
            DistanceFadeBegin = 18f,
            DistanceFadeLength = 4f,
        };
        AddChild(light);
        if (!_modelLights.TryGetValue(record, out var lights))
            _modelLights[record] = lights = new();
        lights.Add((light, new Vector3(spec.Position.X * scale.Horizontal, spec.Position.Y * scale.Vertical, spec.Position.Z * scale.Horizontal), pivot, part));
    }

    private void StepModelLights()
    {
        foreach (var record in _modelLights)
            foreach (var light in record.Value)
            {
                Transform3D transform = light.Part.Pivot && light.Part.Degrees != 0f
                    ? HingeTransform(record.Key, light.Pivot, new Vector3(light.Part.Axis.X, light.Part.Axis.Y, light.Part.Axis.Z), light.Part.Degrees)
                    : new Transform3D(new Basis(Vector3.Up, record.Key.ModelYaw), record.Key.ModelOrigin + record.Key.CarryOffset);
                light.Light.Position = transform * (light.Position - (light.Part.Pivot ? light.Pivot : Vector3.Zero));
                light.Light.Visible = light.Part.LitAt(_clock);
            }
    }

    // Keep the overlay's own appearance, including nested charge/safety indicators on a defibrillator.
    // The model frame fixes the surface UVs; only its atlas picture changes during animation.
    private bool TryModelOverlayPicture(AppearanceEntry entry, ModelFrame frame, out int picture, out AtlasAnimation animation)
    {
        picture = -1;
        animation = null;
        foreach ((TableRef reference, AppearanceEntry overlay) in ModelOverlays.All(_tables, entry))
        {
            if (_tables.ResolveString(overlay.IconState) == frame.State)
            {
                bool found = IsSquareFrame(frame)
                    ? _atlas.TryPictureFacing(_tables, reference, frame.Dir, trim: false, out picture)
                    : _atlas.TryPictureFrame(_tables, reference, frame.Width, frame.Height, out picture);
                if (!found)
                    return false;
                _atlas.TryModelAnimation(_tables, reference, frame.Dir, frame.Width, frame.Height, out animation);
                return true;
            }
        }
        return false;
    }

    private void StepModelOverlayFrames()
    {
        foreach (StaticRecord record in _overlayAnimated)
            foreach ((PropInstance instance, AtlasAnimation animation) in record.OverlayFrames)
            {
                int picture = animation.Ids[animation.Timeline.FrameAt(_clock)];
                if (instance.Custom.R != picture % 2048 || instance.Custom.G != picture / 2048)
                    record.Batch.SetPieceCustom(instance, new Color(picture % 2048, picture / 2048, instance.Custom.B, instance.Custom.A));
            }
    }
}
