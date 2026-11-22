using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Icons;

// Owner: M2 P7 (made by the showcase), M3 R2 (the builder now serves the fake server's bots too). Engine-free.

/// <summary>
/// Interns resources, strings and appearances into three lists (the public tables of a scripted world: the showcase's, the fake server's), and
/// composes the look of a tg human from tg's real icon states. The same value gives the same reference, as the server's replicator interns.
/// </summary>
public sealed class AppearanceBuilder
{
    /// <summary>Tg's overlay layer indexes used negative (<c>__DEFINES/mobs.dm</c>): the larger the index, the lower the layer.</summary>
    public const float BodypartsLayer = -23, EyesLayer = -21, DamageLayer = -20, UniformLayer = -19, HairLayer = -8;

    /// <summary>The zones of tg's body, in the order of the damage arrays of <see cref="HumanLook"/>.</summary>
    public static readonly string[] Zones = { "chest", "head", "r_arm", "l_arm", "r_leg", "l_leg" };

    private readonly List<string> _resources;
    private readonly List<string> _strings;
    private readonly List<AppearanceEntry> _appearances;
    private readonly Dictionary<AppearanceEntry, TableRef> _byValue = new(new ByValue());

    /// <summary>Builds on <paramref name="resources"/>, <paramref name="strings"/> and <paramref name="appearances"/> (which may already hold entries: their references stay valid).</summary>
    public AppearanceBuilder(List<string> resources, List<string> strings, List<AppearanceEntry> appearances)
    {
        _resources = resources;
        _strings = strings;
        _appearances = appearances;
        for (int i = 0; i < appearances.Count; i++)
            _byValue.TryAdd(appearances[i], TableRef.Public((uint)i + 1));
    }

    public TableRef Res(string text)
    {
        if (text == null)
            return TableRef.None;
        int i = _resources.IndexOf(text);
        if (i < 0)
        {
            _resources.Add(text);
            i = _resources.Count - 1;
        }
        return TableRef.Public((uint)i + 1);
    }

    public TableRef Str(string text)
    {
        if (string.IsNullOrEmpty(text))
            return TableRef.None;
        int i = _strings.IndexOf(text);
        if (i < 0)
        {
            _strings.Add(text);
            i = _strings.Count - 1;
        }
        return TableRef.Public((uint)i + 1);
    }

    /// <summary>Interns an appearance (the same value gives the same reference).</summary>
    public TableRef Add(AppearanceEntry entry)
    {
        if (_byValue.TryGetValue(entry, out TableRef known))
            return known;
        _appearances.Add(entry);
        TableRef reference = TableRef.Public((uint)_appearances.Count);
        _byValue[entry] = reference;
        return reference;
    }

    public TableRef Part(string icon, string state, float layer, string color = null, int flags = 0, byte dir = 0)
        => Add(new AppearanceEntry
        {
            Icon = Res(icon),
            IconState = Str(state),
            Layer = layer,
            Rgba = color == null ? 0xFFFFFFFF : ShowcaseWorld.Rgba(color),
            AppearanceFlags = (ushort)flags,
            Dir = dir,
        });

    /// <summary>The appearance of a human as tg's update_icons composes it: a KEEP_TOGETHER mob whose overlays are the limbs, face, jumpsuit and damage (<paramref name="layer"/>: the mob's own layer, tg's 4 standing and 3.8 lying).</summary>
    public TableRef Human(HumanLook look, float layer = 0f)
    {
        const string Bodyparts = "icons/mob/human/bodyparts_greyscale.dmi";
        var root = new AppearanceEntry
        {
            Icon = Res("icons/mob/human/human.dmi"),
            IconState = Str("human_basic"),
            AppearanceFlags = AppearanceFlag.KeepTogether,
            Layer = layer,
        };
        var parts = new (string State, string Zone)[]
        {
            ("human_chest_m", "chest"), ("human_head_m", "head"), ("human_r_arm", "r_arm"), ("human_l_arm", "l_arm"),
            ("human_r_hand", "r_arm"), ("human_l_hand", "l_arm"), ("human_r_leg", "r_leg"), ("human_l_leg", "l_leg"),
        };
        foreach ((string state, _) in parts)
            root.Overlays.Add(Part(Bodyparts, state, BodypartsLayer, look.Skin));
        root.Overlays.Add(Part("icons/mob/human/human_eyes.dmi", "eyes_l", EyesLayer, "#2b6a9b"));
        root.Overlays.Add(Part("icons/mob/human/human_eyes.dmi", "eyes_r", EyesLayer, "#2b6a9b"));
        for (int i = 0; i < Zones.Length; i++)
        {
            int brute = look.Brute == null ? 0 : look.Brute[i], burn = look.Burn == null ? 0 : look.Burn[i];
            string prefix = "human_" + Zones[i];
            if (brute > 0)
            {
                root.Overlays.Add(Part("icons/mob/effects/dam_mob.dmi", prefix + "_" + brute + "0", DamageLayer, "#a10808"));
                root.Overlays.Add(Part("icons/mob/effects/dam_mob.dmi", prefix + "_" + brute + "0_overlay", DamageLayer, "#a10808"));
            }
            if (burn > 0)
                root.Overlays.Add(Part("icons/mob/effects/dam_mob.dmi", prefix + "_0" + burn, DamageLayer));
        }
        if (look.Jumpsuit != null)
            root.Overlays.Add(Part("gags:/datum/greyscale_config/jumpsuit/worn|" + look.Jumpsuit, "jumpsuit", UniformLayer));
        if (look.Hair != null)
            root.Overlays.Add(Part("icons/mob/human/human_face.dmi", "hair_crewcut", HairLayer, look.Hair));
        if (look.LyingAngle != 0)
        {
            // tg's update_transform: the matrix turned to lying_angle (M.Turn clockwise: a b c d e f = cos sin 0 -sin cos 0).
            double radians = look.LyingAngle * Math.PI / 180.0;
            float cos = (float)Math.Round(Math.Cos(radians), 6), sin = (float)Math.Round(Math.Sin(radians), 6);
            root.Transform = new[] { cos, sin, 0f, -sin, cos, 0f };
        }
        return Add(root);
    }

    // Appearances compare by their fields (the interning of the server's tables).
    private sealed class ByValue : IEqualityComparer<AppearanceEntry>
    {
        public bool Equals(AppearanceEntry a, AppearanceEntry b)
            => a.Icon == b.Icon && a.IconState == b.IconState && a.Dir == b.Dir && a.Rgba == b.Rgba && a.Alpha == b.Alpha && a.Layer == b.Layer
               && a.AppearanceFlags == b.AppearanceFlags && a.BlendMode == b.BlendMode && a.PixelX == b.PixelX && a.PixelY == b.PixelY
               && a.PixelW == b.PixelW && a.PixelZ == b.PixelZ && a.Plane == b.Plane
               && ((a.Transform == null && b.Transform == null) || (a.Transform != null && b.Transform != null && a.Transform.AsSpan().SequenceEqual(b.Transform)))
               && a.Overlays.SequenceEqual(b.Overlays) && a.Underlays.SequenceEqual(b.Underlays);

        public int GetHashCode(AppearanceEntry e)
        {
            var hash = new HashCode();
            hash.Add(e.Icon);
            hash.Add(e.IconState);
            hash.Add(e.Rgba);
            hash.Add(e.Layer);
            hash.Add(e.PixelW);
            hash.Add(e.PixelZ);
            hash.Add(e.Plane);
            hash.Add(e.Overlays.Count);
            return hash.ToHashCode();
        }
    }
}
