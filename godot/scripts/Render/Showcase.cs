using System.Globalization;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>
/// A scene to see the renderers without a server (D104, M2 P7): tg's real icons, composed and drawn by the same
/// <see cref="MapRenderer"/> and <see cref="EntityLayer"/> the game uses, over a replica filled from a hand-made world
/// (<see cref="ShowcaseWorld"/>). Run it with
/// <c>godot --path godot res://scripts/Render/Showcase.tscn -- --screenshot-after=2,/abs/path.png</c> (the automation of <see cref="App"/>
/// takes the screenshot). The scenario is <c>ANOMALY_SHOWCASE</c> (<c>room</c>, <c>dirs</c>, <c>lying</c>, <c>damage</c>, <c>orbit</c>); the camera
/// <c>ANOMALY_SHOWCASE_CAM=x,y,eye,yawDeg,pitchDeg</c> (BYOND tile coordinates, metres, degrees: 0 looks north, 90 west) overrides the scenario's.
/// </summary>
public partial class Showcase : Node3D
{
    private MapRenderer _map;
    private EntityLayer _entities;
    private Camera3D _camera;
    private ShowcaseFeed _feed;
    private double _startedAt;
    private int _frames;

    public override void _Ready()
    {
        string scenario = System.Environment.GetEnvironmentVariable("ANOMALY_SHOWCASE") ?? "room";
        var world = new ShowcaseWorld();
        world.BuildRoom(17, 11);
        (double X, double Y, double Eye, double Yaw, double Pitch) camera = Populate(world, scenario);
        world.Finish();

        var replica = new ReplicaWorld();
        replica.Apply(world.Map);
        replica.Apply(world.Snapshot);
        _feed = new ShowcaseFeed(replica);

        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.03f, 0.035f, 0.05f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.72f, 0.75f, 0.85f),
                AmbientLightEnergy = 0.4f,
            },
        });
        var sun = new DirectionalLight3D { LightEnergy = 0.85f, ShadowEnabled = true, DirectionalShadowMaxDistance = 140f };
        sun.Transform = new Transform3D(new Basis(new Vector3(0.866025f, 0, -0.5f), new Vector3(-0.286788f, 0.819152f, -0.496732f), new Vector3(0.409576f, 0.573576f, 0.709406f)), new Vector3(0, 40, 0));
        AddChild(sun);

        _map = new MapRenderer { Name = "Map" };
        _entities = new EntityLayer { Name = "Entities" };
        AddChild(_map);
        AddChild(_entities);
        var textures = new AppearanceTextures();
        _map.DrawProps = false; // the showcase checks the entity layer: every entity is a node
        _map.Build(_feed.Map, _feed.Replica, textures);
        _entities.Bind(_feed, textures);

        string overrideCamera = System.Environment.GetEnvironmentVariable("ANOMALY_SHOWCASE_CAM");
        if (!string.IsNullOrEmpty(overrideCamera))
        {
            string[] p = overrideCamera.Split(',');
            camera = (D(p[0]), D(p[1]), D(p[2]), D(p[3]), D(p[4]));
        }
        _camera = new Camera3D { Fov = 75f, Near = 0.05f, Far = 400f, Current = true };
        AddChild(_camera);
        _camera.Position = new Vector3(TileSpace.WorldX((float)camera.X), (float)camera.Eye, TileSpace.WorldZ((float)camera.Y));
        _camera.Rotation = new Vector3(Mathf.DegToRad((float)camera.Pitch), Mathf.DegToRad((float)camera.Yaw), 0);

        _startedAt = App.NowSeconds;
        if (App.Instance != null)
        {
            App.Instance.SummaryProvider = Summary;
            App.Instance.GameStarted();
        }
        GD.Print($"[anomaly] showcase: {scenario}, {_feed.Count} entities, icons {(IconPaths.Exists ? "found" : "MISSING")} in {IconPaths.Root}");
    }

    public override void _Process(double delta)
    {
        _frames++;
        _entities.Update(_feed.Tick, App.NowSeconds, 0, false, false);
        if (_frames == 3 && System.Environment.GetEnvironmentVariable("ANOMALY_SHOWCASE_PICK") != null)
            PickCheck();
    }

    // The pick interface (EntityLayer.TryPick) against the pictures: a ray at the middle of every entity finds it, a ray over its head
    // does not, and a ray through a wall finds nothing behind it. Printed for the smoke script.
    private void PickCheck()
    {
        Vector3 eye = _camera.GlobalPosition;
        int total = 0, hits = 0, overHeadMisses = 0;
        foreach (ReplicaEntity e in _feed.Entities)
        {
            // A capsule mob is at its continuous position, anything else at its tile.
            float x = TileSpace.WorldX(e.HasPos ? e.State.PosX / 256f : e.State.X), z = TileSpace.WorldZ(e.HasPos ? e.State.PosY / 256f : e.State.Y);
            bool lying = PostureBits.IsLying(e.State.Posture);
            var middle = new Vector3(x, lying ? 0.25f : (e.IsMob() ? 1.0f : 0.6f), z);
            var over = new Vector3(x, 2.6f, z);
            total++;
            if (_entities.TryPick(eye, (middle - eye).Normalized(), 100f, 0, false, out EntityPick pick) && pick.EntityId == e.Id)
                hits++;
            else
                GD.Print($"[anomaly] showcase pick: missed {e.State.Name} #{e.Id}, got #{(_entities.TryPick(eye, (middle - eye).Normalized(), 100f, 0, false, out EntityPick other) ? other.EntityId : 0)}");
            if (!(_entities.TryPick(eye, (over - eye).Normalized(), 100f, 0, false, out EntityPick above) && above.EntityId == e.Id))
                overHeadMisses++;
        }
        // A ray at the nearest wall of the room, with an entity behind it (the wall is nearer): nothing.
        Vector3 wallTarget = new(TileSpace.WorldX(0.5f), 1.2f, TileSpace.WorldZ(6f));
        bool behindWall = _entities.TryPick(eye, (wallTarget - eye).Normalized(), 100f, 0, false, out _);
        GD.Print($"[anomaly] showcase pick: hits={hits}/{total} over_head_misses={overHeadMisses}/{total} wall_ray_hit={behindWall}");
    }

    private static double D(string text) => double.Parse(text, CultureInfo.InvariantCulture);

    private string Summary()
        => $"showcase frames={_frames} entities={_feed.Count} pictures={_entities.PictureCount} walls={_map.WallCount} floors={_map.FloorCount} textured_groups={_map.TexturedGroups} tinted_groups={_map.TintedGroups} icon_problems={_entities.Textures?.Library.Problems.Count}";

    private static (double X, double Y, double Eye, double Yaw, double Pitch) Populate(ShowcaseWorld w, string scenario)
    {
        var skins = new[] { "#ffe0d1", "#fcccb3", "#e8b59b", "#d9ba93", "#a98f6a", "#6b4a2e" };
        var suits = new[] { "#3f3f3f", "#b0302e", "#2e60b0", "#2eb04f", "#d6a82b", "#7f3fa8" };
        HumanLook Look(int i, int[] brute = null, int[] burn = null, int lying = 0) => new(skins[i % skins.Length], i % 3 == 2 ? null : (i % 2 == 0 ? "#3b2a1a" : "#c9a14a"), suits[i % suits.Length], brute, burn, lying);
        int[] All(int level) => new[] { level, level, level, level, level, level };

        switch (scenario)
        {
            case "dirs":
            {
                // Four humans in a row facing north, east, south and west, seen from the south: back, right side, front, left side.
                for (int i = 0; i < 4; i++)
                    w.AddEntity("/mob/living/carbon/human", "human" + i, 5.5 + i * 2, 6, w.Human(Look(i)), ByondDir.South, (byte)(i * 64));
                return (8.5, 2.6, 1.6, 0, -4);
            }
            case "lying":
            {
                w.AddEntity("/mob/living/carbon/human", "stand", 4.5, 5.5, w.Human(Look(0)), ByondDir.South, 128);
                byte lyingPosture90 = PostureBits.Encode(true, 90);
                w.AddEntity("/mob/living/carbon/human", "lying90", 7, 5.5, w.Human(Look(1, lying: 90)), ByondDir.South, 128, lyingPosture90);
                w.AddEntity("/mob/living/carbon/human", "limp270", 9.5, 5.5, w.Human(Look(3, lying: 270)), ByondDir.North, 0, PostureBits.Encode(true, 270, limp: true));
                w.AddEntity("/mob/living/carbon/human", "lyingdamaged", 12, 5.5, w.Human(Look(4, All(2), All(1), 90)), ByondDir.South, 128, lyingPosture90);
                return (8.5, 3.2, 2.3, 0, -38);
            }
            case "damage":
            {
                // Brute 0..3 on every zone, then burn 1..3, in one row: the damage levels of tg's overlays.
                for (int i = 0; i < 4; i++)
                    w.AddEntity("/mob/living/carbon/human", "brute" + i, 3.3 + i * 1.7, 6, w.Human(Look(i, All(i))), ByondDir.South, 128);
                for (int i = 0; i < 3; i++)
                    w.AddEntity("/mob/living/carbon/human", "burn" + (i + 1), 10.1 + i * 1.7, 6, w.Human(Look(i + 4, null, All(i + 1))), ByondDir.South, 128);
                return (8.5, 1.8, 1.7, 0, -3);
            }
            case "orbit":
            {
                w.AddEntity("/mob/living/carbon/human", "center", 8.5, 6, w.Human(Look(0)), ByondDir.South, 128);
                return (8.5, 3.2, 1.6, 0, -3);
            }
            default:
            {
                for (int i = 0; i < 5; i++)
                    w.AddEntity("/mob/living/carbon/human", "h" + i, 4 + i * 2.2, 5 + (i % 2) * 2.5, w.Human(Look(i, i == 3 ? All(1) : null)), ByondDir.South, (byte)(40 * i));
                w.AddEntity("/mob/living/carbon/human", "lying", 7.5, 3.4, w.Human(Look(1, lying: 90)), ByondDir.South, 128, PostureBits.Encode(true, 90));
                w.AddEntity("/obj/item/storage/toolbox/mechanical", "toolbox", 12, 4, w.Item("icons/obj/storage/toolbox.dmi", "red"));
                return (2.8, 2.3, 1.6, -35, -6);
            }
        }
    }
}
