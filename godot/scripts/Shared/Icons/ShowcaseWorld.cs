using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Icons;

// Owner: M2 P7. A world as the version-2 server would send it, made by hand from tg's real icon states: the look of a tg human (the
// limb sprites tinted by the skin, hair, a GAGS jumpsuit, tg's damage overlays per zone, tg's lying transform), a toolbox, and a test room
// with tg's floor and wall pictures. It exists to see and test the renderers (the showcase scene's screenshots, the compositor's tests on
// real icons) before the server's own appearances (M2 P3's translated update_icons) are merged; it is not a model of what the server sends.

/// <summary>The look of one human of the showcase.</summary>
/// <param name="Skin">The skin tone as a colour text (tg's <c>skin_tone</c> hex).</param>
/// <param name="Hair">The hair colour text, or null for bald.</param>
/// <param name="Jumpsuit">The jumpsuit's colour text (a GAGS colour), or null for none.</param>
/// <param name="Brute">Brute damage level 0 to 3 per zone: chest, head, right arm, left arm, right leg, left leg.</param>
/// <param name="Burn">Burn damage level 0 to 3 per zone, same order.</param>
/// <param name="LyingAngle">0 standing; 90 or 270 lying (tg's <c>lying_angle</c>: the transform turns the sprite).</param>
public sealed record HumanLook(string Skin, string Hair, string Jumpsuit, int[] Brute = null, int[] Burn = null, int LyingAngle = 0);

/// <summary>Builds the showcase's tables, map and entities.</summary>
public sealed class ShowcaseWorld
{
    /// <summary>Tg's overlay layer indexes used negative (<c>__DEFINES/mobs.dm</c>): the larger the index, the lower the layer.</summary>
    public const float BodypartsLayer = AppearanceBuilder.BodypartsLayer, EyesLayer = AppearanceBuilder.EyesLayer, DamageLayer = AppearanceBuilder.DamageLayer, UniformLayer = AppearanceBuilder.UniformLayer, HairLayer = AppearanceBuilder.HairLayer;

    public static readonly string[] Zones = AppearanceBuilder.Zones;

    private readonly List<string> _resources = new();
    private readonly List<string> _strings = new();
    private readonly List<AppearanceEntry> _appearances = new();
    private readonly List<string> _types = new();
    private readonly List<EntityState> _entities = new();
    private readonly AppearanceBuilder _builder;

    public ShowcaseWorld() => _builder = new AppearanceBuilder(_resources, _strings, _appearances);

    /// <summary>The map.</summary>
    public MapGrid Map { get; private set; }

    /// <summary>The Snapshot of tick 1 with the tables and the entities.</summary>
    public Snapshot Snapshot { get; private set; }

    private TableRef Res(string text) => _builder.Res(text);

    private TableRef Str(string text) => _builder.Str(text);

    /// <summary>Interns an appearance (the same value gives the same reference).</summary>
    public TableRef Add(AppearanceEntry entry) => _builder.Add(entry);

    private TableRef Part(string icon, string state, float layer, string color = null, int flags = 0, byte dir = 0) => _builder.Part(icon, state, layer, color, flags, dir);

    /// <summary>A colour text as <c>0xRRGGBBAA</c>.</summary>
    public static uint Rgba(string color) => GagsColors.TryParseOne(color, out uint rgba) ? rgba : 0xFFFFFFFF;

    /// <summary>The appearance of a human as tg's update_icons composes it: a KEEP_TOGETHER mob whose overlays are the limbs, face, jumpsuit and damage.</summary>
    public TableRef Human(HumanLook look) => _builder.Human(look);

    /// <summary>A simple icon-state appearance (an item).</summary>
    public TableRef Item(string icon, string state) => Part(icon, state, 0);

    /// <summary>Adds an entity of <paramref name="type"/>; a capsule mob (<paramref name="yaw"/> given) has its continuous position and facing.</summary>
    public long AddEntity(string type, string name, double x, double y, TableRef appearance, byte dir = ByondDir.South, byte? yaw = null, byte posture = 0)
    {
        int typeIndex = _types.IndexOf(type);
        if (typeIndex < 0)
        {
            _types.Add(type);
            typeIndex = _types.Count - 1;
        }
        long id = _entities.Count + 1;
        int tileX = (int)Math.Round(x), tileY = (int)Math.Round(y);
        var state = new EntityState(id, typeIndex, tileX, tileY, 1, dir, name, 0f, 0)
        {
            PosX = (int)Math.Round(x * 256),
            PosY = (int)Math.Round(y * 256),
            Yaw = yaw ?? 0,
            Posture = posture,
            Appearance = appearance,
            HasPos = yaw.HasValue,
        };
        _entities.Add(state);
        return id;
    }

    /// <summary>The test room: a rectangle of walls, a floor of tg's <c>floor</c> picture, a darker patch and a plating strip, a pillar.</summary>
    public void BuildRoom(int width, int height)
    {
        TableRef floor = Part("icons/turf/floors.dmi", "floor", 0), dark = Part("icons/turf/floors.dmi", "dark", 0);
        TableRef plating = Part("icons/turf/floors.dmi", "plating", 0), wall = Part("icons/turf/walls.dmi", string.Empty, 0);
        var map = new MapGrid { Width = width, Height = height, Levels = 1 };
        map.TurfTypes.AddRange(new[] { "/turf/open/floor/iron", "/turf/closed/indestructible/riveted", "/turf/open/floor/iron/dark", "/turf/open/floor/plating" });
        var solid = new byte[(width * height + 7) / 8];
        TurfRun run = default;
        TurfRun appearanceRun = default;
        bool first = true;
        for (int y = 1; y <= height; y++)
        {
            for (int x = 1; x <= width; x++)
            {
                bool edge = x == 1 || y == 1 || x == width || y == height;
                bool pillar = x == width - 3 && y == 3;
                int type = edge || pillar ? 1 : (y == 2 ? 3 : (x > width - 5 && y > height - 4 ? 2 : 0));
                TableRef picture = type == 1 ? wall : type == 2 ? dark : type == 3 ? plating : floor;
                if (type == 1)
                {
                    int i = (x - 1) + (y - 1) * width;
                    solid[i >> 3] |= (byte)(1 << (i & 7));
                }
                if (!first && run.TypeIndex == type)
                    run = run with { Count = run.Count + 1 };
                else
                {
                    if (!first)
                        map.Runs.Add(run);
                    run = new TurfRun(1, type);
                }
                if (!first && appearanceRun.TypeIndex == (int)picture.Index)
                    appearanceRun = appearanceRun with { Count = appearanceRun.Count + 1 };
                else
                {
                    if (!first)
                        map.AppearanceRuns.Add(appearanceRun);
                    appearanceRun = new TurfRun(1, (int)picture.Index);
                }
                first = false;
            }
        }
        map.Runs.Add(run);
        map.AppearanceRuns.Add(appearanceRun);
        map.SolidBits = solid;
        Map = map;
    }

    /// <summary>Finishes the world: the Snapshot of everything added.</summary>
    public void Finish(long tick = 1, long ownEntity = 0)
    {
        var snapshot = new Snapshot { Tick = tick, OwnEntity = ownEntity };
        snapshot.Types.AddRange(_types);
        snapshot.Entities.AddRange(_entities);
        snapshot.Resources.AddRange(_resources);
        snapshot.Strings.AddRange(_strings);
        snapshot.Appearances.AddRange(_appearances);
        Snapshot = snapshot;
    }
}
