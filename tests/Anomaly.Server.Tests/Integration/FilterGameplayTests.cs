using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M6 slice 7 (D155, protocol version 25) through the real server on MetaStation with a headless client: tg's filters reach the
/// client. A gloved person who loses the right arm wears the left glove masked by tg's "missingr" render target; a cranial fissure
/// displaces the person's picture; the player's own wounded arm is outlined red on the HUD's health doll.
/// </summary>
public class FilterGameplayTests
{
    private const int Second = 20;
    private const int X = 43, Y = 155;

    private readonly ITestOutputHelper _output;

    public FilterGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static turf At(int x, int y) => Builtins.locate(x, y, 1);

    // Every appearance of a tree, the root first.
    private static IEnumerable<AppearanceEntry> Tree(ReplicaWorld replica, TableRef root, int depth = 0)
    {
        if (depth > 12 || !replica.TryGetAppearance(root, out AppearanceEntry e))
            yield break;
        yield return e;
        foreach (TableRef child in e.Underlays.Concat(e.Overlays))
            foreach (AppearanceEntry below in Tree(replica, child, depth + 1))
                yield return below;
    }

    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    [MetaServerFact]
    public void Tg_filters_reach_the_client_the_masked_glove_the_split_skull_and_the_dolls_outline()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 71, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int bootRuntimes = w.World.RuntimeErrorCount;

        mob_living_carbon_human bob = In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(At(X, Y)));
            var b = Dm.@new<mob_living_carbon_human>(At(X + 1, Y));
            var gloves = Dm.@new<obj_item_clothing_gloves_color_black>(b.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(b.equip_to_slot_if_possible(gloves, ITEM_SLOT_GLOVES))));
            return b;
        });
        w.Run(Second);
        In(w, () => Waiting.NoSleep(((obj_item_bodypart)bob.get_bodypart(BODY_ZONE_R_ARM)).dismember()));
        w.Run(Second);

        // the glove: an alpha filter whose render source is the "missingr" overlay's render target
        Assert.True(replica.TryGet(bob.dm_id, out ReplicaEntity seen), "the client sees Bob");
        List<AppearanceEntry> look = Tree(replica, seen.State.Appearance).ToList();
        AppearanceEntry masked = look.FirstOrDefault(e => e.Filters?.Any(f => f.Kind == FilterKind.Alpha) == true);
        Assert.NotNull(masked);
        FilterEntry mask = masked.Filters.First(f => f.Kind == FilterKind.Alpha);
        string source = replica.ResolveString(mask.RenderSource);
        _output.WriteLine("the glove's mask: " + source);
        Assert.StartsWith("*MissGlove ", source, StringComparison.Ordinal);
        Assert.Equal(FilterFlags.MaskInverse, mask.Flags);
        AppearanceEntry target = look.Single(e => !e.RenderTarget.IsNone && replica.ResolveString(e.RenderTarget) == source);
        Assert.Equal("missingr", replica.ResolveString(target.IconState));
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root != null)
        {
            var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
            Assert.True(compositor.TryCompose(new Tables(replica), seen.State.Appearance, seen.State.Dir, 0, out _));
        }

        // the split skull
        In(w, () =>
        {
            var fissure = (datum_wound_cranial_fissure)Waiting.NoSleep(Dm.new_async(typeof(datum_wound_cranial_fissure)));
            Waiting.NoSleep(fissure.apply_wound((obj_item_bodypart)bob.get_bodypart(BODY_ZONE_HEAD)));
            return 0;
        });
        w.Run(Second);
        Assert.True(replica.TryGet(bob.dm_id, out seen));
        Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry bobRoot));
        FilterEntry displace = Assert.Single(bobRoot.Filters, f => f.Kind == FilterKind.Displace);
        Assert.Equal("icons/effects/cranial_fissure.dmi", replica.ResolveResource(displace.Icon));
        Assert.Equal("displacement", replica.ResolveString(displace.IconState));
        Assert.Equal(3f, displace.Size);

        // the player's own wounded arm on the doll
        In(w, () => Waiting.NoSleep(me.cause_wound_of_type_and_severity(WOUND_SLASH, (obj_item_bodypart)me.get_bodypart(BODY_ZONE_L_ARM), WOUND_SEVERITY_MODERATE)));
        w.Run(Second);
        bool outlined = replica.Screen.Values.Any(entry => Tree(replica, entry.Appearance)
            .Any(e => e.Filters?.Any(f => f.Kind == FilterKind.Outline && f.Rgba == 0xFF0033FF) == true));
        Assert.True(outlined, "the doll's wounded arm is outlined red");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // A filter added to or removed from a world entity, with nothing else about it changed, reaches the client on the next delta:
    // the entity's filters are part of its public appearance (version 26), so add_filter's appearance change is a hint.
    [MetaServerFact]
    public void A_filter_alone_added_to_an_entity_and_removed_reaches_the_client_at_once()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 71, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;

        obj_item box = In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(At(X, Y)));
            return (obj_item)Dm.@new<obj_item_storage_toolbox_mechanical>(At(X + 1, Y));
        });
        w.Run(Second);
        bool Outlined() => replica.TryGet(box.dm_id, out ReplicaEntity e) && replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry root)
            && root.Filters?.Any(f => f.Kind == FilterKind.Outline) == true;
        Assert.False(Outlined(), "the toolbox starts with no outline");

        In(w, () => { box.add_filter("hint_test", 1, GlobalProcs.outline_filter(1, "#ff0000") as DmList); return 0; });
        w.Run(3);
        Assert.True(Outlined(), "the outline added alone reaches the client within a few ticks");

        In(w, () => box.remove_filter("hint_test"));
        w.Run(3);
        Assert.False(Outlined(), "the outline removed alone leaves the client within a few ticks");
    }
}
