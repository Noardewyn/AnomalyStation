using System.Linq;
using Godot;
using Anomaly.Core.Motion;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Models;
using Anomaly.Protocol;

namespace Anomaly.Client;

// The BYOND filters a thing a 3D model draws shows besides its meshes (ModelFilters, docs/ai/specs/models.md "Filters"): the supermatter's rays and outline.
public partial class MapRenderer
{
    private sealed class FilterNodes
    {
        public MeshInstance3D Rays;
        public ShaderMaterial RaysMaterial;
        public readonly List<MeshInstance3D> Outline = new();
        public ShaderMaterial OutlineMaterial;
        public List<PropInstance> OutlinedInstances;
    }

    private readonly Dictionary<StaticRecord, FilterNodes> _filtered = new();
    private static readonly QuadMesh RaysQuad = new() { Size = Vector2.One };

    // A model was built for the record: it shows its appearance's rays and outline from now on, or no longer.
    private void TrackModelFilters(StaticRecord record, AppearanceEntry entry)
    {
        if (ModelFilters.Any(entry.Filters))
            _filtered.TryAdd(record, new FilterNodes());
        else
            DropModelFilters(record);
    }

    private void DropModelFilters(StaticRecord record)
    {
        if (!_filtered.Remove(record, out FilterNodes nodes))
            return;
        nodes.Rays?.QueueFree();
        foreach (MeshInstance3D rim in nodes.Outline)
            rim.QueueFree();
    }

    // Each frame: the filters at the render tick (their animate() chains played, FilterPlayback), placed on the model as it stands now.
    private void StepModelFilters()
    {
        if (_filtered.Count == 0)
            return;
        double tick = double.IsNaN(RenderTick) ? _replica.Tick : RenderTick;
        foreach (StaticRecord record in _filtered.Keys.ToList())
        {
            if (record.Model == null || !_records.TryGetValue(record.Entity.Id, out StaticRecord live) || !ReferenceEquals(live, record)
                || !_replica.TryGetAppearance(record.Entity.State.Appearance, out AppearanceEntry entry) || !ModelFilters.Any(entry.Filters))
            {
                DropModelFilters(record);
                continue;
            }
            IReadOnlyList<FilterEntry> filters = entry.Filters;
            if (FilterPlayback.TryEvaluate(record.Entity.Animation, tick, MotionMath.TicksPerSecond, entry.Filters, TableRef.None, out List<FilterEntry> shown))
                filters = shown;
            FilterNodes nodes = _filtered[record];
            StepRays(record, nodes, ModelFilters.Rays(filters));
            StepOutline(record, nodes, ModelFilters.Outline(filters));
        }
    }

    private void StepRays(StaticRecord record, FilterNodes nodes, FilterEntry rays)
    {
        if (rays == null)
        {
            if (nodes.Rays != null)
                nodes.Rays.Visible = false;
            return;
        }
        if (nodes.Rays == null)
        {
            nodes.RaysMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/prop_rays.gdshader") };
            nodes.RaysMaterial.SetShaderParameter("metres_per_pixel", ModelFilters.MetresPerPixel);
            nodes.Rays = new MeshInstance3D
            {
                Name = "Rays",
                Mesh = RaysQuad,
                MaterialOverride = nodes.RaysMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                CustomAabb = new Aabb(new Vector3(-8f, -8f, -8f), new Vector3(16f, 16f, 16f)), // the quad is sized in the shader
            };
            AddChild(nodes.Rays);
        }
        nodes.Rays.Visible = true;
        // the rays come from the model's middle (BYOND's centre of the icon, x and y moving it)
        nodes.Rays.Position = record.ModelOrigin + record.CarryOffset
            + new Vector3(rays.X * ModelFilters.MetresPerPixel, _models.TopOf(record.Model) / 2f + rays.Y * ModelFilters.MetresPerPixel, 0f);
        ShaderMaterial m = nodes.RaysMaterial;
        m.SetShaderParameter("colour", ColourOf(rays.Rgba));
        m.SetShaderParameter("size", rays.Size);
        m.SetShaderParameter("density", rays.Density);
        m.SetShaderParameter("factor", rays.Factor);
        m.SetShaderParameter("threshold", rays.Threshold);
        m.SetShaderParameter("offset", rays.Offset);
    }

    private void StepOutline(StaticRecord record, FilterNodes nodes, FilterEntry outline)
    {
        // the rim follows the crystal parts drawn now (a part shown by an overlay comes and goes with a new build of the record's instances)
        List<PropInstance> crystals = outline == null ? new List<PropInstance>() : record.Instances.Where(i => i.Mesh >= 0 && _models.IsCrystal(i.Mesh)).ToList();
        if (nodes.OutlinedInstances == null || !nodes.OutlinedInstances.SequenceEqual(crystals))
        {
            foreach (MeshInstance3D rim in nodes.Outline)
                rim.QueueFree();
            nodes.Outline.Clear();
            nodes.OutlinedInstances = crystals;
            nodes.OutlineMaterial ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/prop_outline.gdshader"), RenderPriority = -1 }; // before the crystal
            foreach (PropInstance crystal in crystals)
            {
                var rim = new MeshInstance3D
                {
                    Name = "Outline",
                    Mesh = _models.HullAt(crystal.Mesh),
                    MaterialOverride = nodes.OutlineMaterial,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                AddChild(rim);
                nodes.Outline.Add(rim);
            }
        }
        if (outline == null)
            return;
        nodes.OutlineMaterial.SetShaderParameter("colour", ColourOf(outline.Rgba));
        nodes.OutlineMaterial.SetShaderParameter("width", ModelFilters.OutlineWidth(outline));
        for (int i = 0; i < crystals.Count; i++)
            nodes.Outline[i].Transform = crystals[i].Transform;
    }

    private static Color ColourOf(uint rgba)
        => new((rgba >> 24) / 255f, ((rgba >> 16) & 255) / 255f, ((rgba >> 8) & 255) / 255f, (rgba & 255) / 255f);
}
