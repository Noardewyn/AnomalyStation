using System.Collections.Generic;
using Godot;
using Anomaly.Client.Shared.Editor;

namespace Anomaly.Client.Editor;

/// <summary>
/// The preview's blue (docs/ai/specs/map-editor.md 5.9): the boxes of what the map editor is about to place, given to every world material through
/// the shaders' globals (light_grid.gdshaderinc, anomaly_previewed), so a model, a picture, a wall and a floor are tinted alike whatever draws them.
/// </summary>
public sealed class PreviewTint
{
    private const string Boxes = "anomaly_preview_boxes", Count = "anomaly_preview_count", Min = "anomaly_preview_min", Max = "anomaly_preview_max";

    private Image _image; // one column a box: its least corner over its greatest
    private readonly List<PreviewBox> _shown = new();
    private ImageTexture _texture;

    /// <summary>No box is tinted: the game's state, and a new scene's (the globals outlive a scene).</summary>
    public static void Clear() => RenderingServer.GlobalShaderParameterSet(Count, 0f);

    /// <summary>The boxes that are tinted from now on (at most <see cref="PreviewBoxes.Most"/>); none takes the tint away.</summary>
    public void Show(IReadOnlyList<PreviewBox> boxes)
    {
        int count = Mathf.Min(boxes.Count, PreviewBoxes.Most);
        bool same = count == _shown.Count;
        for (int i = 0; same && i < count; i++)
            same = boxes[i] == _shown[i];
        if (same)
            return;
        _shown.Clear();
        _image ??= Image.CreateEmpty(PreviewBoxes.Most, 2, false, Image.Format.Rgbaf);
        Vector3 min = Vector3.Inf, max = -Vector3.Inf;
        for (int i = 0; i < count; i++)
        {
            PreviewBox box = boxes[i];
            _shown.Add(box);
            _image.SetPixel(i, 0, new Color(box.MinX, box.MinY, box.MinZ, box.Turf ? 1f : 0f));
            _image.SetPixel(i, 1, new Color(box.MaxX, box.MaxY, box.MaxZ, 0f));
            min = min.Min(new Vector3(box.MinX, box.MinY, box.MinZ));
            max = max.Max(new Vector3(box.MaxX, box.MaxY, box.MaxZ));
        }
        if (count > 0)
        {
            if (_texture == null)
            {
                _texture = ImageTexture.CreateFromImage(_image);
                RenderingServer.GlobalShaderParameterSet(Boxes, _texture);
            }
            else
                _texture.Update(_image);
            RenderingServer.GlobalShaderParameterSet(Min, min);
            RenderingServer.GlobalShaderParameterSet(Max, max);
        }
        RenderingServer.GlobalShaderParameterSet(Count, (float)count);
    }
}
