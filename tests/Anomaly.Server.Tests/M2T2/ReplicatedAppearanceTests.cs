using System.Globalization;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Replication;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.M2T2;

/// <summary>
/// M2 T2: the replicated appearance table reproduces the server's stack. A watcher's replica, fed only by the bytes the server
/// sent it, resolves a real human's appearance (the interned table, the resource and string tables) to the same list of
/// entries the world holds and the Core tests' goldens write down (icon file, state, layer, colour of each entry, the damage
/// appearance with its nested entries, the lying transform and offset); nothing numeric about the damage is in a table, only the
/// states a viewer sees (D40).
/// </summary>
public class ReplicatedAppearanceTests
{
    private const string Greyscale = "icons/mob/human/bodyparts_greyscale.dmi";
    private const string DamMob = "icons/mob/effects/dam_mob.dmi";

    private sealed class Rig
    {
        public readonly V2Run Run = V2Run.Quiet(seed: 31);
        public readonly mob_living_carbon_human Subject;
        public readonly SessionView Watcher;

        public Rig()
        {
            Subject = Run.In(() => Dm.@new<mob_living_carbon_human_consistent>(locate(8, 8, 1)));
            mob watcherMob = Run.In(() => Dm.@new<mob_living_carbon_human>(locate(4, 4, 1)));
            Watcher = Run.Connect("Alice", watcherMob);
            Run.Step();
            Run.Step();
        }

        public void Tick(int n = 2)
        {
            for (int i = 0; i < n; i++) Run.Step();
        }

        public void Damage(string type, float amount, string zone)
        {
            Run.In(() => Waiting.NoSleep(Subject.apply_damage(amount, type, zone)));
            for (int i = 0; i < 8; i++) // past the damaged part's red flash (DV400, 3 ds)
                Tick();
        }

        public ReplicaEntity Entity()
        {
            Assert.True(Watcher.Replica.TryGet(Subject.dm_id, out ReplicaEntity e), "the watcher's replica has no entity for the subject");
            return e;
        }

        public AppearanceEntry Top() => Entry(Entity().State.Appearance);

        public AppearanceEntry Entry(TableRef reference)
        {
            Assert.True(Watcher.Replica.TryGetAppearance(reference, out AppearanceEntry entry), "no appearance " + reference);
            return entry;
        }

        /// <summary>file:state@layer#rrggbbaa of an entry, as the golden lists write it ("" for none).</summary>
        public string Describe(AppearanceEntry e)
        {
            string icon = e.Icon.IsNone ? "" : Watcher.Replica.ResolveResource(e.Icon);
            string state = e.IconState.IsNone ? "" : Watcher.Replica.ResolveString(e.IconState);
            string color = e.ColorMatrix != null ? "matrix" : (e.Rgba == 0xFFFFFFFF ? "" : (e.Rgba >> 8).ToString("x6", CultureInfo.InvariantCulture));
            return $"{icon}:{state}@{e.Layer.ToString(CultureInfo.InvariantCulture)}#{color}";
        }

        public string[] Stack() => Top().Overlays.Select(o => Describe(Entry(o))).ToArray();
    }

    // layers: BODYPARTS_LAYER 23 and BODYPARTS_HIGH_LAYER 17.5 (the hands), DAMAGE_LAYER 20 and DAMAGE_OVERLAY_LAYER 19.9; a
    // /consistent human's skin is #ffe0d1 and its blood colour #AA1010 (BLOOD_COLOR_RED)
    // M6 slice 7b: a leg is two images cut by leg_masks.dmi (carbon_update_icons.dm:512 handle_masking) at -BODYPARTS_LAYER and
    // -BODYPARTS_LOW_LAYER 23.1, each an /icon Blend recipe resource (protocol.md 34.3)
    private static string[] Healthy() => new[]
    {
        $"{Greyscale}:human_chest_m@-23#ffe0d1", $"{Greyscale}:human_head_m@-23#ffe0d1",
        $"{Leg("human_l_leg", "left_leg")}:human_l_leg@-23#ffe0d1", $"{Leg("human_l_leg", "left_leg_lower")}:human_l_leg@-23.1#ffe0d1",
        $"{Leg("human_r_leg", "right_leg")}:human_r_leg@-23#ffe0d1", $"{Leg("human_r_leg", "right_leg_lower")}:human_r_leg@-23.1#ffe0d1",
        $"{Greyscale}:human_l_arm@-23#ffe0d1", $"{Greyscale}:human_l_hand@-17.5#ffe0d1",
        $"{Greyscale}:human_r_arm@-23#ffe0d1", $"{Greyscale}:human_r_hand@-17.5#ffe0d1",
        // M10x slice D2: the eyes (_eyes.dm:306), black for a consistent human
        "icons/mob/human/human_eyes.dmi:eyes_l@-21#000000", "icons/mob/human/human_eyes.dmi:eyes_r@-21#000000",
        ":@-21#", ":@-21#", // the open eyes' eyelid overlays (_eyes.dm:491): no icon, a render_source
    };

    private const int LimbEntries = 10;

    // The recipe of a leg's state blended (multiplied) by a leg mask, as the server sends it.
    private static string Leg(string state, string mask)
    {
        icon made = Dm.icon(Dm.resource(Greyscale), state);
        made.Blend(Dm.icon(Dm.resource("icons/mob/leg_masks.dmi"), mask), Dm.ICON_MULTIPLY);
        return made.dm_resource();
    }

    [Fact]
    public void AReplicaResolvesAHealthyHumansEightLimbsEntryByEntry()
    {
        var rig = new Rig();
        Assert.Equal(string.Join("\n", Healthy()), string.Join("\n", rig.Stack()));
        // and the same list the world's snapshot gives (the replicator interned exactly what IDmAppearance says)
        string[] world = rig.Run.In(() => Dm.each(rig.Subject.overlays).Cast<object>().Select(o => (IDmAppearance)o)
            .Select(a => $"{(a.icon is DmResource r ? r.Path : a.icon is icon made ? made.dm_resource() : a.icon as string)}:{a.icon_state}@{a.layer.ToString(CultureInfo.InvariantCulture)}#{(a.color is string c ? c.TrimStart('#') : "")}").ToArray());
        Assert.Equal(world.Select(w => w.ToLowerInvariant()), rig.Stack().Select(w => w.ToLowerInvariant()));
    }

    [Fact]
    public void ADamagedBodyAddsOneDamageAppearanceWithItsNestedEntriesAndTheLimbsStay()
    {
        var rig = new Rig();
        rig.Damage(BRUTE, 130f, BODY_ZONE_CHEST);   // level 2
        rig.Damage(BURN, 47.5f, BODY_ZONE_L_ARM);   // 0.95 of 50: level 3
        string[] stack = rig.Stack();
        Assert.Equal(Healthy().Take(LimbEntries), stack.Take(LimbEntries).ToArray());
        // M10x slice D2: then the eyes, closed in the crit's eyelid colour (_eyes.dm:476), then the damage
        Assert.StartsWith("icons/mob/human/human_eyes.dmi:eyes_l@-21#", stack[LimbEntries]);
        Assert.StartsWith("icons/mob/human/human_eyes.dmi:eyes_r@-21#", stack[LimbEntries + 1]);
        Assert.Equal(LimbEntries + 3, stack.Length);
        AppearanceEntry damage = rig.Entry(rig.Top().Overlays[LimbEntries + 2]);
        Assert.Equal(":@-20#", rig.Describe(damage)); // no icon, no state, at -DAMAGE_LAYER
        Assert.Equal(new[]
        {
            $"{DamMob}:human_chest_20@-20#aa1010",
            $"{DamMob}:human_chest_20_overlay@-19.9#",
            $"{DamMob}:human_l_arm_03@-20#",
        }, damage.Overlays.Select(o => rig.Describe(rig.Entry(o))).ToArray());
        // the detail overlay does not take the blood colour: RESET_COLOR (appearance flag 2)
        Assert.Equal(2, rig.Entry(damage.Overlays[1]).AppearanceFlags & 2);
    }

    [Fact]
    public void NoTableHoldsTheHitPointsOnlyTheStatesAViewerSees()
    {
        var rig = new Rig();
        rig.Damage(BRUTE, 123.5f, BODY_ZONE_CHEST);
        var strings = new List<string>();
        AppearanceEntry top = rig.Top();
        void Walk(AppearanceEntry e)
        {
            if (!e.IconState.IsNone) strings.Add(rig.Watcher.Replica.ResolveString(e.IconState));
            foreach (TableRef o in e.Overlays) Walk(rig.Entry(o));
        }
        Walk(top);
        Assert.DoesNotContain(strings, s => s.Contains("123", StringComparison.Ordinal));
        Assert.Contains("human_chest_20", strings);
    }

    [Fact]
    public void LyingIsTheRotatedTransformTheOffsetAndThePostureBitNotAChangedStack()
    {
        var rig = new Rig();
        string[] standing = rig.Stack();
        Assert.True(rig.Top().Transform == null || rig.Top().Transform.SequenceEqual(new[] { 1f, 0f, 0f, 0f, 1f, 0f }));
        rig.Run.In(() => Waiting.NoSleep(rig.Subject.set_resting(TRUE, TRUE)));
        rig.Tick(3);
        Assert.True(PostureBits.IsLying(rig.Entity().State.Posture), "the posture bit is not set");
        AppearanceEntry top = rig.Top();
        Assert.NotNull(top.Transform);
        float angle = rig.Run.In(() => Dm.num(rig.Subject.lying_angle));
        float sin = angle == 90 ? 1 : -1;
        Assert.Equal(new[] { 0f, sin, 0f, -sin, 0f, 0f }, top.Transform.Select(f => MathF.Round(f, 4) + 0f).ToArray());
        Assert.Equal(-6, top.PixelY); // PIXEL_Y_OFFSET_LYING
        Assert.Equal(standing, rig.Stack());
    }

    [Fact]
    public void AGagsIconIsReplicatedAsItsDescriptorText()
    {
        var rig = new Rig();
        string gags = rig.Run.In(() => Dm.str(SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_jumpsuit_worn), "#3f3f3f")));
        Assert.Equal("gags:/datum/greyscale_config/jumpsuit/worn|#3f3f3f", gags);
        rig.Run.In(() => rig.Subject.add_overlay(mutable_appearance(gags, "jumpsuit", -UNIFORM_LAYER)));
        rig.Tick(3);
        AppearanceEntry last = rig.Entry(rig.Top().Overlays[^1]);
        Assert.Equal(gags, rig.Watcher.Replica.ResolveResource(last.Icon));
        Assert.Equal("jumpsuit", rig.Watcher.Replica.ResolveString(last.IconState));
        Assert.Equal(-UNIFORM_LAYER, last.Layer);
    }
}
