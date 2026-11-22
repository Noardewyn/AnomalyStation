using System.Linq;
using Godot;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Models;
using Anomaly.Client.Shared.World;
using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Client;

// The 3D models' closets and crates, and the places things rest on (docs/ai/specs/models.md).
public partial class MapRenderer
{
    private readonly List<StaticRecord> _closets = new();

    // The aims the solar heads show, by entity (kept over a rebuild, so a new aim is turned to from where the head stands), and the heads turning now.
    private readonly Dictionary<long, SunHeadMotion> _sunMotions = new();
    private readonly HashSet<StaticRecord> _sunTurning = new();
    private const float SolarPanelLift = 0.12f; // metres

    // A panel's or tracker's head aims at tg's azimuth and the tilt for it (SunHeadMotion.TiltFor); one first seen shows its aim at once.
    private SunHeadMotion AimSunHead(StaticRecord record, ModelEntry model, float azimuth)
    {
        long id = record.Entity.Id;
        float tilt = SunHeadMotion.TiltFor(id, azimuth, model.Azimuth == "solar_panel_");
        if (_sunMotions.TryGetValue(id, out SunHeadMotion motion))
            motion.Aim(azimuth, tilt);
        else
            _sunMotions[id] = motion = new SunHeadMotion(azimuth, tilt);
        record.SunMotion = motion;
        if (motion.Moving)
            _sunTurning.Add(record);
        return motion;
    }

    private static Transform3D SunHeadTransform(Vector3 origin, Vector3 pivot, float yawOffset, SunHeadMotion aim)
        => new Transform3D(new Basis(Vector3.Up, ModelRules.AzimuthYaw(aim.Azimuth) + yawOffset), origin)
           * new Transform3D(new Basis(Vector3.Right, Mathf.DegToRad(aim.Tilt)), pivot);

    // Once per frame: the turning heads move along their curve.
    private void StepSunHeads(float dt)
    {
        if (_sunTurning.Count == 0)
            return;
        foreach (StaticRecord record in _sunTurning.ToArray())
        {
            SunHeadMotion aim = record.SunMotion;
            aim.Step(dt);
            foreach ((PropInstance instance, Vector3 pivot, float yawOffset) in record.SunHeads)
                record.Batch.SetModelTransform(instance, SunHeadTransform(record.ModelOrigin + record.CarryOffset, pivot, yawOffset, aim));
            if (!aim.Moving)
                _sunTurning.Remove(record);
        }
    }

    /// <summary>
    /// Where a thing on a tile rests (D121, docs/ai/specs/models.md), for the things the map renderer does not draw (a thing a player dropped): on a closet's shelf when a
    /// closet model stands on the tile (<paramref name="onSlot"/>: the place is the slot itself, the thing's own pixel offset is not used), else on a table's top
    /// (the tile's centre at the table's height; the offset still applies). False: on the floor.
    /// </summary>
    public bool TryRest(long entityId, int x, int y, int z, out Vector3 world, out bool onSlot)
    {
        onSlot = false;
        world = TileCenter(x, y, z);
        if (_byCell.TryGetValue((x, y, z), out List<StaticRecord> here))
        {
            foreach (StaticRecord record in here)
            {
                if (record.Model == null || record.Instances.Count == 0)
                    continue;
                System.Numerics.Vector3[] slots = _models.SlotsOf(record.Model);
                if (slots.Length == 0)
                    continue;
                System.Numerics.Vector3 slot = slots[(int)((ulong)entityId % (ulong)slots.Length)];
                world = record.ModelOrigin + new Basis(Vector3.Up, record.ModelYaw) * new Vector3(slot.X, slot.Y, slot.Z);
                onSlot = true;
                return true;
            }
        }
        if (_plan != null && _plan.Index.Tables.Contains((x, y, z)))
        {
            world += new Vector3(0f, TableTopAt(x, y, z), 0f);
            return true;
        }
        return false;
    }

    /// <summary>
    /// D325: the height above the floor of a thing at (<paramref name="x"/>, <paramref name="y"/>) on a level's tile drawn as a ramp (tg's stairs,
    /// <see cref="ModelEntry.RestOnRamp"/>): up the flight by its dir, a level's height at its far edge; false: no ramp there.
    /// </summary>
    // DEVIATION(DV370): the stairs' flight lifts what stands on it
    public bool TryRampOn(float x, float y, int z, out float height)
    {
        height = 0f;
        int tx = (int)MathF.Round(x), ty = (int)MathF.Round(y);
        if (!_byCell.TryGetValue((tx, ty, z), out List<StaticRecord> here))
            return false;
        foreach (StaticRecord record in here)
        {
            if (record.Model is { RestOnRamp: true } && record.Instances.Count > 0)
            {
                height = StairRamp.Progress(record.SeenDir, x, y, tx, ty) * TileSpace.LevelHeight;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// DV127: the height a body lying on the tile is drawn at: the top of a bed's or a stretcher's model (<see cref="ModelEntry.RestOnTop"/>) or of a
    /// table (tg draws a patient over the bed's sprite, the elevation lifting one on a bed or a table); false: on the floor.
    /// </summary>
    // DEVIATION(DV127): a lying body on a bed's, a stretcher's or a table's tile is drawn on its top
    public bool TryLieOn(int x, int y, int z, out float height)
    {
        height = 0f;
        if (_byCell.TryGetValue((x, y, z), out List<StaticRecord> here))
        {
            foreach (StaticRecord record in here)
            {
                if (record.Model is { RestOnTop: true } && record.Instances.Count > 0 && _models.TopOf(record.Model) > 0f)
                {
                    height = record.ModelOrigin.Y - TileCenter(x, y, z).Y + _models.LieTopOf(record.Model);
                    return true;
                }
            }
        }
        if (_plan != null && _plan.Index.Tables.Contains((x, y, z)))
        {
            height = TableTopAt(x, y, z);
            return true;
        }
        return false;
    }

    /// <summary>
    /// D239: the height a standing mob on the tile is drawn and his eye placed at: on a tile solid only through climb_walkable structures (a table, a
    /// closed crate: MapGrid's climb-walkable bit) or solid at all (a rack, a canister he climbed: tg's elevation lifts him there too), the top of the
    /// highest structure model on it, else a table's top; false: on the floor.
    /// </summary>
    // DEVIATION(DV154): tg's elevation element (a pixel shift of the climber's sprite) is the structure model's top in 3D
    public bool TryStandOn(int x, int y, int z, out float height)
    {
        height = 0f;
        if (_replica == null || !_replica.IsClimbWalkable(x, y, z) && !_replica.IsSolid(x, y, z))
            return false;
        bool found = false;
        if (_byCell.TryGetValue((x, y, z), out List<StaticRecord> here))
        {
            foreach (StaticRecord record in here)
            {
                // a structure standing on the floor and lower than a wall (not a wall-mounted sign or lamp, not a full-height machine)
                if (record.Model == null || record.Instances.Count == 0 || _models.TopOf(record.Model) <= 0f || record.Model.FitHeight == "wall"
                    || MathF.Abs(record.ModelOrigin.Y - TileCenter(x, y, z).Y) > 0.05f)
                    continue;
                float top = _models.TopOf(record.Model);
                if (!found || top > height)
                    height = top;
                found = true;
            }
        }
        if (!found && _plan != null && _plan.Index.Tables.Contains((x, y, z)))
        {
            height = TableTopAt(x, y, z);
            found = true;
        }
        return found;
    }

    /// <summary>The height things on a table rest at: a table model's own top (its catalog fit may make it higher than the world's table top), else the table top.</summary>
    private float TableTopAt(int x, int y, int z)
    {
        if (_byCell.TryGetValue((x, y, z), out List<StaticRecord> here))
        {
            foreach (StaticRecord record in here)
            {
                if (record.Model?.Rule == ModelRule.Smooth4 && record.Instances.Count > 0 && _models.TopOf(record.Model) > 0f)
                    return _models.TopOf(record.Model);
            }
        }
        return WorldGeometry.TableTop;
    }

    // tg's closet (closets.dm closet_update_overlays): open when the door overlay is "<state>_open", shut when it is "<door>_door"; while the door animates
    // (is_animating_door) there is neither, and the door keeps going the way the last change sent it.
    private void ReadClosetState(StaticRecord record, AppearanceEntry entry)
    {
        bool? open = ClosetLook.Open(OverlayStates(entry), out string door);
        if (door != null)
            record.ClosetDoor = door;
        if (open != null)
        {
            record.ClosetRestOpen = open.Value;
            record.ClosetOverlaySeen = true;
        }
        if (!record.ClosetSeen)
        {
            record.ClosetSeen = true;
            record.ClosetOpen = record.ClosetRestOpen ? 1f : 0f; // as the map loads: no swing
        }
        // An overlay-less appearance (the door animating) after a shut one is an opening, after an open one a closing; a closet that never showed its door
        // overlay (a feed without overlays) stands shut.
        record.ClosetOpening = open ?? (record.ClosetOverlaySeen && !record.ClosetRestOpen);
        record.ClosetDoor ??= record.SeenState;
    }

    private bool HasDoorOverlay(AppearanceEntry entry) => ClosetLook.Open(OverlayStates(entry), out _) != null;

    private IEnumerable<string> OverlayStates(AppearanceEntry entry)
        => ModelOverlays.All(_tables, entry).Select(o => _tables.ResolveString(o.Entry.IconState));

    // A closet's parts with their pictures: the body its state, the door's front the composed closet while it is shut (the door overlay, the lock lights, the
    // weld) and its door state while it swings or stands open, the door's back the state's _back. It faces its dir, in the middle of its tile. A closet may have
    // more leaves (a cabinet's two doors, a box's flaps: parts named doorFront… and doorInside…), each on its own hinge: its axis and degrees, else the entry's
    // swing about the vertical.
    private bool TryBuildCloset(StaticRecord record, AppearanceEntry entry, ModelEntry model, Vector3 center)
    {
        string icon = _replica.ResolveResource(entry.Icon);
        string state = record.SeenState;
        ReadClosetState(record, entry);
        Placed p = record.Placed;
        // The way it faces is its dir (DV56: the server turns the map's closets away from the wall behind them and applies the owner's map patches).
        byte dir = record.Entity.State.Dir;
        byte face = dir is ByondDirConst.North or ByondDirConst.South or ByondDirConst.East or ByondDirConst.West ? dir : ByondDirConst.South;
        if (!_models.TryPart(model, ModelRules.Body, out int bodyMesh, out _))
            return false;
        var leaves = new List<(int Mesh, Vector3 Pivot, bool Front, ModelPart Spec)>();
        foreach ((string name, ModelPart spec) in model.Parts)
        {
            bool front = name.StartsWith(ModelRules.DoorFront, StringComparison.Ordinal);
            if (!front && !name.StartsWith(ModelRules.DoorInside, StringComparison.Ordinal))
                continue;
            if (!_models.TryPart(model, name, out int mesh, out _, out Vector3 pivot))
                return false;
            leaves.Add((mesh, pivot, front, spec));
        }
        if (!_atlas.TryLibraryPicture(icon, state, trim: false, out int bodyPicture))
            return false;
        if (!_atlas.TryLibraryPicture(icon, state + "_back", trim: false, out int insidePicture))
            insidePicture = bodyPicture;
        bool shut = record.ClosetOpen <= 0f && !record.ClosetOpening && HasDoorOverlay(entry);
        int frontPicture;
        if (!(shut && _atlas.TryPictureFrame(_tables, Look(record), model.CellWidth, model.CellHeight, out frontPicture))
            && !_atlas.TryLibraryPicture(icon, (record.ClosetDoor ?? state) + "_door", trim: false, out frontPicture))
            frontPicture = bodyPicture;
        float yaw = ModelRules.Choose(model, state, record.Entity.State.Dir, face)[0].Yaw;
        // In the middle of its tile, as tg draws it (the owner, 2026-10-15), moved by the catalog's offset (the dev menu's Tools tab tunes it).
        Vector3 origin = center + new Basis(Vector3.Up, yaw) * new Vector3(model.Offset.X, model.Offset.Y, model.Offset.Z);
        record.Model = model;
        record.ModelOrigin = origin;
        record.ModelYaw = yaw;
        record.Hinged.Clear();
        AddModelInstance(record, bodyMesh, bodyPicture, new Transform3D(new Basis(Vector3.Up, yaw), origin));
        foreach ((int mesh, Vector3 pivot, bool front, ModelPart spec) in leaves)
        {
            (Vector3 axis, float swing) = spec.Degrees != 0f
                ? (new Vector3(spec.Axis.X, spec.Axis.Y, spec.Axis.Z).Normalized(), spec.Degrees)
                : (Vector3.Up, model.OpenDegrees);
            int picture = front ? frontPicture : insidePicture;
            record.Hinged.Add((AddModelInstance(record, mesh, picture, HingeTransform(record, pivot, axis, swing)), pivot, axis, swing, Vector3.Zero, null, Vector3.Zero, Vector3.One));
        }
        if (!_closets.Contains(record))
            _closets.Add(record);
        record.Batch.Dirty = true;
        _dirty.Add(record.Batch);
        RebuildRestingOn(record);
        return true;
    }

    private PropInstance AddModelInstance(StaticRecord record, int mesh, int picture, Transform3D transform)
    {
        var instance = new PropInstance
        {
            Transform = transform,
            Color = new Color(PieceParts.ModeModel / 8f, 0, 0, 0),
            Custom = new Color(picture % 2048, picture / 2048, 0, 1),
            Mesh = mesh,
            Wall = PieceParts.BlocksLight(record.Placed.Shape, record.Entity.TypePath),
        };
        record.Instances.Add(instance);
        record.Batch.AddModel(instance);
        return instance;
    }

    // A crate's parts with their pictures (tg's crates.dm: the icon state is "[base]open" while it is open; tg does not animate it, DV57: the lid swings open
    // over the catalog's seconds): a part whose picture is "closed" shows the composed crate while it is shut (its lock lights, manifest and weld) and the closed
    // state while it is open; "open" the open state; else the state it names. The hinged parts swing about their pivots. It faces its dir, in the middle of its tile.
    private bool TryBuildCrate(StaticRecord record, AppearanceEntry entry, ModelEntry model, Vector3 center)
    {
        string icon = _replica.ResolveResource(entry.Icon);
        string state = record.SeenState;
        bool open = CrateLook.Open(model, state, OverlayStates(entry));
        if (!record.ClosetSeen)
        {
            record.ClosetSeen = true;
            record.ClosetOpen = open ? 1f : 0f; // as the map loads: no swing
        }
        record.ClosetOpening = open;
        bool shut = !open && record.ClosetOpen <= 0f;
        byte dir = record.Entity.State.Dir;
        byte face = dir is ByondDirConst.North or ByondDirConst.South or ByondDirConst.East or ByondDirConst.West ? dir : ByondDirConst.South;
        ModelPlacement[] placements = ModelRules.Choose(model, state, dir, face);
        var parts = new (int Mesh, Vector3 Pivot, int Picture, ModelPart Spec)[placements.Length];
        for (int i = 0; i < placements.Length; i++)
        {
            ModelPart spec = model.Parts[placements[i].Part];
            if (!_models.TryPart(model, placements[i].Part, out int mesh, out _, out Vector3 pivot))
                return false;
            string pictureState = CrateLook.PictureState(model, spec, state, open, shut);
            int picture;
            if (pictureState == null)
            {
                if (!_atlas.TryPictureFrame(_tables, Look(record), model.CellWidth, model.CellHeight, out picture)
                    && !_atlas.TryLibraryPicture(icon, CrateLook.ClosedState(model, state), trim: false, out picture))
                    return false;
            }
            else if (!_atlas.TryLibraryPicture(icon, pictureState, trim: false, out picture)
                     && !_atlas.TryLibraryPicture(icon, CrateLook.ClosedState(model, state), trim: false, out picture))
                return false;
            parts[i] = (mesh, pivot, picture, spec);
        }
        record.Model = model;
        // in the middle of its tile, as tg draws it, moved by the catalog's offset
        record.ModelOrigin = center + new Basis(Vector3.Up, placements[0].Yaw) * new Vector3(model.Offset.X, model.Offset.Y, model.Offset.Z);
        record.ModelYaw = placements[0].Yaw;
        record.Hinged.Clear();
        foreach ((int mesh, Vector3 pivot, int picture, ModelPart spec) in parts)
        {
            if (spec.Degrees != 0f)
            {
                Vector3 axis = new Vector3(spec.Axis.X, spec.Axis.Y, spec.Axis.Z).Normalized();
                record.Hinged.Add((AddModelInstance(record, mesh, picture, HingeTransform(record, pivot, axis, spec.Degrees)), pivot, axis, spec.Degrees, Vector3.Zero, null, Vector3.Zero, Vector3.One));
            }
            else
                AddModelInstance(record, mesh, picture, HingeTransform(record, pivot, Vector3.Up, 0f));
        }
        if (!_closets.Contains(record))
            _closets.Add(record);
        record.Batch.Dirty = true;
        _dirty.Add(record.Batch);
        RebuildRestingOn(record);
        return true;
    }

    // A firelock (firedoor.dm: its state is "door_opening" or "door_closing" while it moves, then "door_open" or "door_closed"): its leaves slide toward open from
    // "door_opening" over the catalog's seconds (tg's door_opening, 1.2 s), stepped with the closets'; the state's own frames are not drawn on them.
    private static void ReadSlideState(StaticRecord record, ModelEntry model)
    {
        bool open = ModelRules.SlidesOpen(record.SeenState);
        if (!record.ClosetSeen)
        {
            record.ClosetSeen = true;
            record.ClosetOpen = open ? 1f : 0f; // as the map loads: no slide
            record.ClosetRestOpen = open;
            record.ClosetOpening = open;
            return;
        }
        // Only a change of the state moves the leaves (its flick has usually started them already); a rebuild for another reason (an alarm light) keeps
        // them going the way the flick sent them.
        if (open != record.ClosetRestOpen)
            record.ClosetOpening = open;
        record.ClosetRestOpen = open;
    }

    private static bool SlidesByState(StaticRecord record) => record.Model?.Rule == ModelRule.Airlock && !record.IsDoor;

    // tg's computer (_computer.dm update_overlays: icon_keyboard, or its _off without power; icon_screen with power): the body shows the icon state, the screen
    // part the screen overlay (not drawn without one), the keyboard part the keyboard overlay, each over the frame its uv point into. Turned by its dir.
    private bool TryBuildConsole(StaticRecord record, AppearanceEntry entry, ModelEntry model, Vector3 center)
    {
        string icon = _replica.ResolveResource(entry.Icon);
        string keyboard = null, screen = null;
        foreach (TableRef reference in entry.Overlays)
        {
            if (!_replica.TryGetAppearance(reference, out AppearanceEntry o) || _replica.ResolveResource(o.Icon) != icon
                || _replica.ResolveString(o.IconState) is not string overlay || overlay.Length == 0)
                continue; // another icon's overlay (an emissive)
            if (overlay.Contains("_key", StringComparison.Ordinal))
                keyboard ??= overlay;
            else
                screen ??= overlay;
        }
        ModelPlacement[] placements = ModelRules.Choose(model, record.SeenState, record.Entity.State.Dir, record.Placed.Face);
        var parts = new List<(int Mesh, int Picture, float Yaw)>();
        foreach (ModelPlacement placement in placements)
        {
            string state = placement.Part switch { ModelRules.Screen => screen, ModelRules.Keyboard => keyboard, _ => record.SeenState };
            if (state == null)
                continue;
            if (!_models.TryPart(model, placement.Part, out int mesh, out _))
                return false;
            if (!_atlas.TryLibraryPicture(icon, state, trim: false, out int picture))
            {
                if (placement.Part == ModelRules.Body)
                    return false;
                continue;
            }
            parts.Add((mesh, picture, placement.Yaw));
        }
        Vector3 origin = center + new Basis(Vector3.Up, placements[0].Yaw) * new Vector3(model.Offset.X, model.Offset.Y, model.Offset.Z);
        foreach ((int mesh, int picture, float yaw) in parts)
            AddModelInstance(record, mesh, picture, new Transform3D(new Basis(Vector3.Up, yaw), origin));
        record.Model = model;
        record.ModelOrigin = origin;
        record.ModelYaw = placements[0].Yaw;
        record.Batch.Dirty = true;
        _dirty.Add(record.Batch);
        return true;
    }

    // A door or a lid turned about its hinge by the open fraction (animate_door: a linear swing to door_anim_angle); a part a flick moves (ModelPart.Flick), besides,
    // by how far the flick has taken it.
    private static Transform3D HingeTransform(StaticRecord record, Vector3 pivot, Vector3 axis, float degrees, Vector3 move = default, ModelFlick flick = null,
        Vector3 flickMove = default, Vector3? openScale = null)
    {
        float f = flick != null ? record.FlickOpen : 0f;
        float angle = Mathf.DegToRad(degrees * record.ClosetOpen + (flick?.Degrees ?? 0f) * f);
        var basis = new Basis(axis, angle);
        if (openScale is Vector3 squeeze && squeeze != Vector3.One)
            basis *= Basis.FromScale(Vector3.One.Lerp(squeeze, record.ClosetOpen));
        if (f > 0f && flick.Scale != System.Numerics.Vector3.One)
            basis *= Basis.FromScale(Vector3.One.Lerp(new Vector3(flick.Scale.X, flick.Scale.Y, flick.Scale.Z), f));
        return new Transform3D(new Basis(Vector3.Up, record.ModelYaw), record.ModelOrigin + record.CarryOffset)
               * new Transform3D(basis, pivot + move * record.ClosetOpen + flickMove * f);
    }

    // tg's flick of a state a model's parts move with (ModelPart.Flick): they go toward the pose of the flick's frame from where they are now.
    private void StartPartFlick(StaticRecord record)
    {
        EntityState s = record.Entity.State;
        if (record.Model == null || s.FlickAppearance.IsNone)
            return;
        string state = StateOf(s.FlickAppearance);
        if (!record.Hinged.Any(h => h.Flick != null && h.Flick.States.Contains(state))
            || !_atlas.TryAnimation(_tables, s.FlickAppearance, ByondDir.South, trim: false, once: true, out AtlasAnimation flick) || flick == null)
            return;
        double age = Math.Max(0, _replica.Tick - s.FlickTick) / (double)MotionMath.TicksPerSecond;
        record.PartFlick = flick;
        record.PartFlickStart = _clock - age;
        _partFlicks.Add(record);
    }

    private readonly HashSet<StaticRecord> _partFlicks = new();

    // Once per frame: the parts a flick moves go toward the pose of its frame at their pace, and home after it.
    private void StepPartFlicks(float dt)
    {
        if (_partFlicks.Count == 0)
            return;
        List<StaticRecord> done = null;
        foreach (StaticRecord record in _partFlicks)
        {
            ModelFlick flick = record.Hinged.Select(h => h.Flick).FirstOrDefault(f => f != null);
            if (flick == null || record.PartFlick == null)
            {
                (done ??= new List<StaticRecord>()).Add(record);
                continue;
            }
            double t = _clock - record.PartFlickStart;
            bool playing = t < record.PartFlick.Timeline.Length;
            float target = playing ? flick.TargetAt(record.PartFlick.Timeline.FrameAt(t)) : 0f;
            float step = dt / Math.Max(flick.Seconds, 0.01f);
            record.FlickOpen = record.FlickOpen < target ? Math.Min(target, record.FlickOpen + step) : Math.Max(target, record.FlickOpen - step);
            foreach (var h in record.Hinged)
                if (h.Flick != null)
                    record.Batch.SetModelTransform(h.Instance, HingeTransform(record, h.Pivot, h.Axis, h.Degrees, h.Move, h.Flick, h.FlickMove, h.OpenScale));
            if (!playing && record.FlickOpen == 0f)
                (done ??= new List<StaticRecord>()).Add(record);
        }
        foreach (StaticRecord record in done ?? Enumerable.Empty<StaticRecord>())
        {
            record.PartFlick = null;
            _partFlicks.Remove(record);
        }
    }

    // A spinning part (ModelPart.Spin) at the renderer's clock.
    private Transform3D SpinTransform(StaticRecord record, Vector3 pivot, Vector3 axis, float speed)
        => new Transform3D(new Basis(Vector3.Up, record.ModelYaw), record.ModelOrigin + record.CarryOffset)
           * new Transform3D(new Basis(axis, Mathf.DegToRad((float)(speed * _clock % 360.0))), pivot);

    // Once per frame: the spinning parts turn (a gravity generator's field ring).
    private void StepSpins()
    {
        if (_spinners.Count == 0)
            return;
        _spinners.RemoveWhere(r => r.Spinning.Count == 0);
        foreach (StaticRecord record in _spinners)
            foreach ((PropInstance instance, Vector3 pivot, Vector3 axis, float speed) in record.Spinning)
                record.Batch.SetModelTransform(instance, SpinTransform(record, pivot, axis, speed));
    }

    // Once per frame: the doors that swing move toward open or shut at tg's pace; a door that stops changes its picture (the composed front when shut).
    private void StepClosets(float dt)
    {
        foreach (StaticRecord record in _closets)
        {
            if (record.Model == null)
                continue;
            float target = record.ClosetOpening ? 1f : 0f;
            if (record.ClosetOpen == target)
                continue;
            float step = dt / Math.Max(record.Model.OpenSeconds, 0.01f);
            record.ClosetOpen = record.ClosetOpen < target ? Math.Min(target, record.ClosetOpen + step) : Math.Max(target, record.ClosetOpen - step);
            foreach ((PropInstance instance, Vector3 pivot, Vector3 axis, float degrees, Vector3 move, ModelFlick flick, Vector3 flickMove, Vector3 openScale) in record.Hinged)
                record.Batch.SetModelTransform(instance, HingeTransform(record, pivot, axis, degrees, move, flick, flickMove, openScale));
            if (record.Model.Rule == ModelRule.Airlock)
            {
                // A firelock's leaves slide by the open fraction in their colour, as a door's.
                foreach (PropInstance instance in record.Instances)
                {
                    Color c = instance.Color;
                    record.Batch.SetPieceColor(instance, new Color(c.R, c.G, c.B, record.ClosetOpen));
                }
            }
            if (record.ClosetOpen == target && record.Model.Rule != ModelRule.Wall && !SlidesByState(record))
                BuildInstances(record); // a closet's or crate's door at rest changes its picture; a firelock's leaves do not
        }
    }

    private readonly HashSet<StaticRecord> _blinkers = new();
    private readonly HashSet<StaticRecord> _spinners = new();
    private readonly Dictionary<(string Icon, string State, bool Dominant), (float R, float G, float B, float Coverage)> _spriteColors = new();

    // A tg picture's colour (SpriteColors), once per icon state; false when the icon or the state is not there.
    private bool TrySpriteColor(string icon, string state, bool dominant, out (float R, float G, float B, float Coverage) color)
    {
        if (_spriteColors.TryGetValue((icon, state, dominant), out color))
            return true;
        if (icon == null || _textures?.Library == null || !_textures.Library.TryGetIcon(icon, out Anomaly.Client.Shared.Icons.IIcon i)
            || !i.TryGetState(state, out Anomaly.Client.Shared.Icons.IIconState s))
            return false;
        _spriteColors[(icon, state, dominant)] = color = Anomaly.Client.Shared.Icons.SpriteColors.Mean(s, dominant);
        return true;
    }

    // DV73: the main colour of the appearance's overlay of an icon (a canister's held tank, its invisible overlay), wholly; no tint without one.
    private uint OverlayIconTint(AppearanceEntry entry, string icon)
    {
        foreach (TableRef reference in entry.Overlays)
        {
            if (_replica.TryGetAppearance(reference, out AppearanceEntry o) && _replica.ResolveResource(o.Icon) == icon
                && TrySpriteColor(icon, _replica.ResolveString(o.IconState), true, out var c))
                return ModelColors.Blend(new[] { (c.R, c.G, c.B, 1f) });
        }
        return ModelColors.NoTint;
    }

    // tg's update_window: the gases' pictures inside the overlay of a state (a canister's window-base), each by its alpha and its sprite's coverage; none: clear.
    private uint VisualsTint(AppearanceEntry entry, string state)
    {
        var layers = new List<(float, float, float, float)>();
        foreach (TableRef reference in entry.Overlays)
        {
            if (!_replica.TryGetAppearance(reference, out AppearanceEntry window) || _replica.ResolveString(window.IconState) != state)
                continue;
            foreach (TableRef visual in window.Overlays)
            {
                if (_replica.TryGetAppearance(visual, out AppearanceEntry gas)
                    && TrySpriteColor(_replica.ResolveResource(gas.Icon), _replica.ResolveString(gas.IconState), false, out var c))
                    layers.Add((c.R, c.G, c.B, gas.Alpha / 255f * c.Coverage));
            }
            break;
        }
        return ModelColors.Blend(layers);
    }

    // Once per frame: a model's blinking parts (ModelPart.BlinkOn: a canister's can-0 light, tg's two-frame state) shown or hidden by the renderer's clock; a
    // record rebuilt without them leaves the set.
    private void StepBlinks()
    {
        if (_blinkers.Count == 0)
            return;
        _blinkers.RemoveWhere(r => r.Blinking.Count == 0);
        foreach (StaticRecord record in _blinkers)
        {
            foreach ((PropInstance instance, Transform3D at, ModelPart part, Color litColor, Color litCustom, int offPicture) in record.Blinking)
            {
                Transform3D placed = at with { Origin = at.Origin + record.CarryOffset };
                int hinge = record.Hinged.FindIndex(h => h.Instance == instance);
                if (hinge >= 0)
                {
                    var h = record.Hinged[hinge];
                    placed = HingeTransform(record, h.Pivot, h.Axis, h.Degrees, h.Move, h.Flick, h.FlickMove);
                }
                bool lit = part.LitAt(_clock);
                record.Batch.SetModelTransform(instance, lit || offPicture >= 0 ? placed : placed with { Basis = Basis.FromScale(Vector3.Zero) });
                if (offPicture >= 0)
                {
                    record.Batch.SetPieceColor(instance, lit ? litColor : new Color(litColor.R, 0, 0, litColor.A));
                    record.Batch.SetPieceCustom(instance, lit ? litCustom : new Color(offPicture % 2048, offPicture / 2048, litCustom.B, litCustom.A));
                }
            }
        }
    }

    // The things the map renderer draws on a closet's or a table model's tile go on its shelves or top once it is built.
    private void RebuildRestingOn(StaticRecord closet)
    {
        if (!_byCell.TryGetValue(closet.Cell, out List<StaticRecord> here))
            return;
        foreach (StaticRecord record in here.ToArray())
        {
            if (record != closet && record.Placed.Kind is PlacedKind.Item or PlacedKind.Resting && record.Instances.Count > 0)
                BuildInstances(record);
        }
    }

    // An item its model draws (PropKinds.ItemHasModel) lies where its picture would, and on the bed under it: a bedsheet is a few centimetres thick.
    private Vector3 ItemModelOrigin(StaticRecord record, Vector3 center)
    {
        Placed p = record.Placed;
        if (TryShelf(record, out Vector3 shelf))
            return shelf;
        return center + new Vector3(p.OffsetX, TryLieOn(p.X, p.Y, p.Z, out float top) ? top : p.Height, -p.OffsetY);
    }

    // A static item on a closet's, crate's or rack's tile: its place on a shelf (inside the crate), else where the planner put it.
    private bool TryShelf(StaticRecord record, out Vector3 at)
    {
        at = default;
        Placed p = record.Placed;
        if (!_byCell.TryGetValue((p.X, p.Y, p.Z), out List<StaticRecord> here) || !here.Exists(r => r.Model != null && (r.Model.Rule is ModelRule.Closet or ModelRule.Crate || r.Model.RestOnShelves)))
            return false;
        return TryRest(record.Entity.Id, p.X, p.Y, p.Z, out at, out bool onSlot) && onSlot;
    }

    private static int Dx(byte dir) => (dir & ByondDirConst.East) != 0 ? 1 : (dir & ByondDirConst.West) != 0 ? -1 : 0;

    private static int Dy(byte dir) => (dir & ByondDirConst.North) != 0 ? 1 : (dir & ByondDirConst.South) != 0 ? -1 : 0;
}
