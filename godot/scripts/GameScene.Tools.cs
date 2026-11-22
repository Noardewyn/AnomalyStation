using System.IO;
using System.Linq;
using Godot;
using ImGuiNET;
using Anomaly.Client.Hud;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Models;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;
using NVector3 = System.Numerics.Vector3;

namespace Anomaly.Client;

// The dev menu's Tools tab (the owner, 2026-11-06): live tuning of how things look, saved into the files the look is read from. The held things' turn,
// size and move (held_items.json); a 2D thing's look in the world: standing or flat, size and move, by type (world_looks.json); a map model's
// scale and offset (models.json).
public partial class GameScene
{
    private ModelEntry _tuned;
    private float _tunedScale; // as it was when picked (Revert)
    private System.Numerics.Vector3 _tunedOffset;
    private string _toolStatus;
    private string _lookPicked; // the type of the thing picked to tune (WorldLooks)
    private string _lookType; // the type its look is set on: the picked one or an ancestor (its subtypes take it too)
    private string _lookStatus;
    private static readonly string[] LookModes = { "Auto (the rules)", "Billboard (standing)", "Floor (flat)" };

    /// <summary>The 2D things' looks by type (WorldLooks), which the dev menu edits.</summary>
    public const string WorldLooksPath = "res://assets/world_looks.json";

    private void DrawToolsTab()
    {
        // D285: the first-person held things' tuning (ViewModel), saved into held_items.json for everyone (the owner, 2026-11-20: every thing's own
        // place on the screen, loaded as it is picked up; every held thing's field of view, size and shift saved too)
        ImGui.SeparatorText("Held things (first person)");
        ImGui.TextDisabled("Every held thing:");
        if (ImGui.SliderFloat("All: field of view", ref ViewModel.Fov, 40f, 100f, "%.0f"))
            _viewModel.ApplyFov();
        ImGui.SliderFloat("All: size", ref ViewModel.Size, 0.4f, 2f, "%.2f");
        var shift = new NVector3(ViewModel.Shift.X, ViewModel.Shift.Y, ViewModel.Shift.Z);
        if (ImGui.SliderFloat3("All: shift (m)", ref shift, -1f, 1f, "%.3f"))
            ViewModel.Shift = new Vector3(shift.X, shift.Y, shift.Z);
        ViewModel.Turns.All = new HeldGlobal(ViewModel.Fov, ViewModel.Size, ViewModel.Shift.X, ViewModel.Shift.Y, ViewModel.Shift.Z);
        if (ImGui.Button("Reset all##heldall"))
        {
            HeldGlobal saved = ViewModel.Turns.SavedAll;
            (ViewModel.Fov, ViewModel.Size, ViewModel.Shift) = (saved.Fov, saved.Size, new Vector3(saved.X, saved.Y, saved.Z));
            ViewModel.Turns.All = saved;
            _viewModel.ApplyFov();
        }
        ImGui.Separator();
        // the active hand's thing's own turn, size and move (HeldTurns)
        if (_viewModel.ActivePicture is (string icon, string state))
            DrawHeldTurn(icon, state, _viewModel.SecondPicture == null ? "" : "Left arm: ");
        else
            ImGui.TextDisabled("This thing: nothing in the active hand");
        // DV361: in a mech the right arm's equipment is tuned beside the left's (each is drawn in its own place, mirrored)
        if (_viewModel.SecondPicture is (string icon2, string state2))
            DrawHeldTurn(icon2, state2, "Right arm: ");
        ImGui.Separator();
        bool unsaved = ViewModel.Turns.Unsaved;
        if (ImGui.Button(unsaved ? "Save held things *##heldsave" : "Save held things##heldsave"))
        {
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(ViewModel.TurnsPath), ViewModel.Turns.ToJson());
            ViewModel.Turns.MarkSaved();
        }
        ImGui.SameLine();
        ImGui.TextDisabled(unsaved ? "unsaved changes (kept while the client runs)" : "saved in held_items.json");
        DrawWorldLooks();
        DrawModelTuning();
    }

    private static void DrawHeldTurn(string icon, string state, string label)
    {
        ImGui.PushID(label);
        HeldTurn t = ViewModel.Turns.Find(icon, state);
        bool own = ViewModel.Turns.HasOwn(icon, state);
        ImGui.TextDisabled($"{label}This thing: {icon}:{state}");
        ImGui.TextDisabled(own ? (t == ViewModel.Turns.Saved(icon, state) ? "  its own tuning (saved)" : "  its own tuning (not saved yet)")
            : t.IsNone ? "  no tuning of its own: as drawn" : "  no tuning of its own: a look of the same thing's (a change makes it its own)");
        var angles = new NVector3(t.Yaw, t.Pitch, t.Roll);
        if (ImGui.SliderFloat3("Turn yaw/pitch/roll", ref angles, -180f, 180f, "%.0f"))
            ViewModel.Turns.Set(icon, state, t with { Yaw = angles.X, Pitch = angles.Y, Roll = angles.Z });
        float size = t.Size;
        if (ImGui.SliderFloat("Its size", ref size, 0.2f, 2f, "%.2f"))
            ViewModel.Turns.Set(icon, state, t with { Scale = size });
        var move = new NVector3(t.X, t.Y, t.Z);
        if (ImGui.SliderFloat3("Its move x/y/z (m)", ref move, -1f, 1f, "%.3f"))
            ViewModel.Turns.Set(icon, state, t with { X = move.X, Y = move.Y, Z = move.Z });
        float recoil = t.Kick; // DV96: a gun's own recoil, times the server's kick
        if (ImGui.SliderFloat("Its recoil (guns)", ref recoil, 0.05f, 3f, "%.2f"))
            ViewModel.Turns.Set(icon, state, t with { Recoil = recoil });
        if (ImGui.Button("Yaw 180"))
            ViewModel.Turns.Set(icon, state, t with { Yaw = t.Yaw == 180f ? 0f : 180f });
        ImGui.SameLine();
        if (ImGui.Button("Roll 180"))
            ViewModel.Turns.Set(icon, state, t with { Roll = t.Roll == 180f ? 0f : 180f });
        ImGui.SameLine();
        // back to what the file has for it, or as drawn when it has nothing
        if (ImGui.Button("Reset##held"))
            ViewModel.Turns.Set(icon, state, ViewModel.Turns.Saved(icon, state));
        ImGui.SameLine();
        if (ImGui.Button("Clear##held"))
            ViewModel.Turns.Set(icon, state, default);
        ImGui.PopID();
    }

    // A 2D thing's look in the world (the owner, 2026-11-20: a pool of blood standing up, a cut-off leg too small): the picture under the crosshair
    // is picked, its type's look (or an ancestor's, for all its subtypes) changed live and saved for everyone.
    private void DrawWorldLooks()
    {
        ImGui.SeparatorText("World things (2D pictures: items, blood, creatures ...)");
        ImGui.TextWrapped("Aim the crosshair at a picture, then Pick. A change applies to the chosen type and all its subtypes.");
        if (ImGui.Button("Pick under crosshair##look"))
            PickLookToTune();
        if (_lookType == null)
        {
            if (_lookStatus != null)
                ImGui.TextDisabled(_lookStatus);
            return;
        }
        WorldLooks looks = WorldLooks.Active;
        ImGui.TextDisabled($"Picked: {_lookPicked}");
        if (ImGui.BeginCombo("Tune type##look", _lookType))
        {
            // the picked type and its ancestors down to /obj: a look set on one is taken by all its subtypes
            for (string t = _lookPicked; t.Count(c => c == '/') >= 2; t = t[..t.LastIndexOf('/')])
                if (ImGui.Selectable(looks.HasOwn(t) ? t + "  *" : t, t == _lookType))
                    _lookType = t;
            ImGui.EndCombo();
        }
        string source = looks.Source(_lookType);
        ImGui.TextDisabled(source == _lookType ? (looks.Find(_lookType) == looks.Saved(_lookType) ? "  its own look (saved)" : "  its own look (not saved yet)")
            : source == null ? "  no look of its own: drawn by the rules" : $"  takes the look of {source} (a change makes it its own)");
        WorldLook look = looks.Find(_lookType);
        int mode = (int)look.Mode;
        bool changed = ImGui.Combo("Drawn as##look", ref mode, LookModes, LookModes.Length);
        float size = look.Size;
        changed |= ImGui.SliderFloat("Size##look", ref size, 0.2f, 3f, "%.2f");
        var move = new NVector3(look.X, look.Y, look.Z);
        changed |= ImGui.SliderFloat3("Move x/y/z (m)##look", ref move, -1f, 1f, "%.3f");
        ImGui.TextDisabled("x east, y up, z north; size is times what the rules draw");
        if (changed)
            SetLook(_lookType, new WorldLook((WorldLookMode)mode, size, move.X, move.Y, move.Z));
        if (ImGui.Button("Reset##look"))
            SetLook(_lookType, looks.Saved(_lookType)); // back to what the file has for it, or the rules when it has nothing
        ImGui.SameLine();
        if (ImGui.Button("Clear##look"))
            SetLook(_lookType, default);
        ImGui.SameLine();
        if (ImGui.Button(looks.Unsaved ? "Save world things *##look" : "Save world things##look"))
        {
            try
            {
                File.WriteAllText(ProjectSettings.GlobalizePath(WorldLooksPath), looks.ToJson());
                looks.MarkSaved();
                _lookStatus = "saved in world_looks.json";
            }
            catch (System.Exception e) when (e is IOException or System.UnauthorizedAccessException)
            {
                _lookStatus = "not saved: " + e.Message;
            }
        }
        if (_lookStatus != null)
            ImGui.TextDisabled(_lookStatus);
    }

    // shortcut: every batched thing of the type is planned again on each change (a slider drags many a frame), fine for the few hundred of a type a round has
    private void SetLook(string type, WorldLook look)
    {
        WorldLooks looks = WorldLooks.Active;
        if (looks.HasOwn(type) ? looks.Find(type) == look : look.IsNone)
            return;
        looks.Set(type, look);
        _map?.Retune(type);
        _lookStatus = null;
    }

    private void PickLookToTune()
    {
        Camera3D camera = _rig.Camera;
        if (camera == null || _session == null || _feed?.Replica == null)
            return;
        (_, _, int z) = _session.Prediction.RenderPosition();
        PickHit hit = _picker.Pick(MakeRay(camera, z, WorldPicker.ViewRange), _feed.OwnEntity);
        if (!hit.IsHit || !_feed.Replica.TryGet(hit.Id, out ReplicaEntity entity) || entity.TypePath == null)
        {
            _lookStatus = hit.IsHit ? "that is no thing of the world (a wall, a floor)" : "nothing under the crosshair";
            return;
        }
        if (_map?.ModelOf(hit.Id) != null)
        {
            _lookStatus = $"{entity.TypePath} is drawn by a 3D model: tune it below";
            return;
        }
        (_lookPicked, _lookType, _lookStatus) = (entity.TypePath, entity.TypePath, null);
    }

    // A map model's size and place: the model under the crosshair is picked, its catalog entry changed live (every thing it draws), and saved.
    private void DrawModelTuning()
    {
        ImGui.SeparatorText("Map models (consoles, crates, wall fixtures ...)");
        ImGui.TextWrapped("Aim the crosshair at a 3D model, then Pick. A change applies to every thing that model draws.");
        if (ImGui.Button("Pick under crosshair"))
            PickModelToTune();
        if (_tuned == null)
        {
            if (_toolStatus != null)
                ImGui.TextDisabled(_toolStatus);
            return;
        }
        ImGui.TextDisabled(_tuned.Id);
        float scale = _tuned.Scale;
        var offset = _tuned.Offset;
        bool changed = ImGui.SliderFloat("Scale", ref scale, 0.2f, 3f, "%.3f");
        changed |= ImGui.SliderFloat3("Offset x/y/z (m)", ref offset, -1f, 1f, "%.3f");
        ImGui.TextDisabled("x across, y up, z toward its front");
        if (ImGui.Button("Revert##model"))
        {
            (scale, offset) = (_tunedScale, _tunedOffset);
            changed = true;
        }
        ImGui.SameLine();
        if (ImGui.Button("Save##model"))
        {
            try
            {
                string path = MapRenderer.ModelCatalogPath;
                File.WriteAllText(path, ModelCatalogText.SetPlacement(File.ReadAllText(path), _tuned.Id, _tuned.Scale, _tuned.Offset));
                (_tunedScale, _tunedOffset) = (_tuned.Scale, _tuned.Offset);
                _toolStatus = $"saved {_tuned.Id} to {path}";
            }
            catch (System.Exception e) when (e is IOException or InvalidDataException or System.UnauthorizedAccessException)
            {
                _toolStatus = "not saved: " + e.Message;
            }
        }
        if (_toolStatus != null)
            ImGui.TextWrapped(_toolStatus);
        if (changed)
        {
            _tuned.Scale = System.Math.Max(0.01f, scale);
            _tuned.Offset = offset;
            _map.Refit(_tuned);
        }
    }

    // The menu frees the mouse and so clears the crosshair's hit: the pick is cast again from the camera along its view.
    private void PickModelToTune()
    {
        Camera3D camera = _rig.Camera;
        if (camera == null || _session == null || _map == null)
            return;
        (_, _, int z) = _session.Prediction.RenderPosition();
        PickHit hit = _picker.Pick(MakeRay(camera, z, WorldPicker.ViewRange), _feed.OwnEntity);
        ModelEntry model = hit.IsHit ? _map.ModelOf(hit.Id) : null;
        if (model == null)
        {
            _toolStatus = hit.IsHit ? "that is not drawn by a 3D model" : "nothing under the crosshair";
            return;
        }
        if (!ReferenceEquals(model, _tuned))
            (_tuned, _tunedScale, _tunedOffset) = (model, model.Scale, model.Offset);
        _toolStatus = null;
    }
}
