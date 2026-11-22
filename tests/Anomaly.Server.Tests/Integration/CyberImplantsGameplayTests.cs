using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps package P6 (D298) through the real server on MetaStation: a crew member uses an autosurgeon loaded with tg's integrated toolset
/// implant on himself (Z), sees his implant's button on the HUD with the implant's picture, clicks it and picks the hydraulic crowbar from
/// tg's radial; the client sees the augment on his arm; the crowbar comes out into his hand with tg's mechanical sound and the client draws
/// it in the hand slot, then he pries a
/// floor tile up with it and the drop key (Q) stows it back into the arm.
/// </summary>
public class CyberImplantsGameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public CyberImplantsGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action f)
    {
        using (w.World.Enter())
            f();
    }

    private static ClickTarget Screen(long id) => new(TargetKind.Screen, id, 0, 0, 0, 16, 16);

    private static List<ScreenEntry> HoldersOf(ReplicaWorld replica, long id)
        => replica.Screen.Values.Where(e => e.VisChildren != null && e.VisChildren.Contains(id)).ToList();

    private static List<string> SeenStates(ReplicaWorld replica, long id)
        => CrewOutfitTests.Icons(replica, replica.Screen.Values.Single(e => e.Id == id).Appearance).Select(i => i.State ?? "").ToList();

    [MetaServerFact]
    public void An_engineer_implants_the_toolset_extends_its_crowbar_pries_a_tile_and_the_client_sees_it_in_his_hand()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 31, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var runtimes = new List<string>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.Message); sink?.Invoke(e); };
        string Diag() => "chat: " + string.Join(" | ", texts.TakeLast(6)) + " runtimes: " + string.Join("\n", runtimes.TakeLast(3));

        // an autosurgeon with the integrated toolset in the active hand, used on himself with Z
        var (surgeon, kit) = In(w, () =>
        {
            foreach (obj_item held in Dm.each_cast<obj_item>(me.held_items.Copy()))
                if (held != null)
                    Waiting.NoSleep(me.dropItemToGround(held));
            var s = Dm.@new<obj_item_autosurgeon>(me.loc);
            var k = Dm.@new<obj_item_organ_cyberimp_arm_toolkit_toolset>(me.loc);
            Waiting.NoSleep(s.load_organ(k));
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(s))));
            return (s, k);
        });
        w.Run(Second / 2);
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.True(In(w, () => kit.owner == me), "implanted; " + Diag());
        Assert.Contains(texts, t => t.Contains("as it plunges into your body"));
        Assert.Contains(heard, h => h.File == "sound/items/weapons/circsawhit.ogg");
        string zone = In(w, () => kit.zone as string);
        int side = zone == BODY_ZONE_R_ARM ? RIGHT_HANDS : LEFT_HANDS;
        _output.WriteLine($"the toolset went into {zone}");
        Do(w, () =>
        {
            if (Dm.@in(surgeon, me.held_items))
                Waiting.NoSleep(me.dropItemToGround(surgeon));
            me.active_hand_index = side;
        });
        w.Run(Second / 2);

        // the augment on his body as the client sees it: tg's arm overlay and its hand layer (augments_arms.dm:20, :83)
        Assert.True(replica.TryGet(In(w, () => me.dm_id), out ReplicaEntity body), "the client sees his body");
        List<string> bodyStates = CrewOutfitTests.Icons(replica, body.State.Appearance).Select(i => i.State ?? "").ToList();
        string arm = zone == BODY_ZONE_R_ARM ? "right" : "left";
        Assert.Contains($"toolkit_engi_{arm}", bodyStates);
        Assert.Contains($"toolkit_engi_{arm}_hand", bodyStates);

        // the implant's button on the client's HUD: the implant's own picture on it
        long button = In(w, () => (Assert.Single(Dm.each_cast<datum_action>(kit.actions)).viewers[me.hud_used] as atom).dm_id);
        List<string> look = SeenStates(replica, button);
        _output.WriteLine("button: " + string.Join(", ", look));
        Assert.Contains("toolkit_engineering", look);

        // a click on it: tg's radial of the six tools on the client, the hydraulic crowbar picked
        heard.Clear();
        Assert.True(p.Session.Click(Screen(button), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        RadialMenuMessage menu = replica.TopRadial;
        Assert.True(menu != null, "no radial; " + Diag());
        _output.WriteLine("radial: " + string.Join(", ", menu.Elements.Select(e => e.Name)));
        Assert.Equal(6, menu.Elements.Count(e => !string.IsNullOrEmpty(e.Name)));
        int slice = menu.Elements.FindIndex(e => (e.Name ?? "").Contains("crowbar", StringComparison.OrdinalIgnoreCase));
        Assert.True(slice >= 0, "no crowbar slice");
        Assert.True(p.Session.RadialPick(menu.MenuId, slice + 1));
        w.Run(Second);
        var crowbar = In(w, () => kit.active_item as obj_item_crowbar_cyborg);
        Assert.True(crowbar != null, "the crowbar is out; " + Diag());
        Assert.Same(crowbar, In(w, () => me.held_items[side]));
        Assert.Contains(texts, t => t.Contains("hydraulic crowbar from your"));
        Assert.Contains(heard, h => h.File == "sound/vehicles/mecha/mechmove03.ogg");

        // the client draws it in the hand: the hand slot's entry lists the crowbar as its child, with tg's picture
        ScreenEntry hand = Assert.Single(HoldersOf(replica, In(w, () => crowbar.dm_id)));
        Assert.True(In(w, () => w.World.Objects.TryGet(hand.Id, out object o) && o is atom_movable_screen_inventory_hand));
        Assert.True(replica.TryGetAppearance(Assert.Single(hand.VisChildren.Items).Appearance, out AppearanceEntry drawn));
        Assert.Equal("toolkit_engiborg_crowbar", replica.ResolveString(drawn.IconState));

        // used: an iron floor tile beside him pried up (the cyborg crowbar's tool act) into plating and a floor tile
        turf_open_floor floor = In(w, () => new[] { NORTH, SOUTH, EAST, WEST }.Select(d => get_step(me, d)).OfType<turf_open_floor>()
            .FirstOrDefault(t => t.GetType() == typeof(turf_open_floor_iron) && !Dm.each<atom_movable>(t).Any(a => Dm.truthy(a.density) || a is obj_machinery || a is obj_structure)));
        if (floor == null)
        {
            // stand on a free iron floor of the same room first
            turf_open_floor spot = In(w, () => Dm.each<turf_open_floor_iron>(range(6, me)).FirstOrDefault(t => t.GetType() == typeof(turf_open_floor_iron) && new[] { NORTH, SOUTH, EAST, WEST }.Select(d => get_step(t, d)).Any(n => n?.GetType() == typeof(turf_open_floor_iron) && !Dm.each<atom_movable>(n).Any())));
            Assert.NotNull(spot);
            Do(w, () => Waiting.NoSleep(me.forceMove(spot)));
            w.Run(Second / 2);
            floor = In(w, () => new[] { NORTH, SOUTH, EAST, WEST }.Select(d => get_step(me, d)).OfType<turf_open_floor>().First(t => t.GetType() == typeof(turf_open_floor_iron) && !Dm.each<atom_movable>(t).Any()));
        }
        var (fx, fy, fz) = In(w, () => ((int)floor.x, (int)floor.y, (int)floor.z));
        heard.Clear();
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, fx, fy, fz), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.IsAssignableFrom<turf_open_floor_plating>(In(w, () => locate(fx, fy, fz)));
        Assert.True(In(w, () => Dm.each<obj_item_stack_tile>(locate(fx, fy, fz)).Any()), "the pried tile lies there");
        Assert.Contains(heard, h => h.File == "sound/items/tools/jaws_pry.ogg"); // the hydraulic crowbar's usesound

        // the drop key with that hand stows the crowbar back into the arm; the hand slot shows nothing
        p.Session.Keybinding(KeybindingCode.DropItem);
        w.Run(Second / 2);
        Assert.Same(kit, In(w, () => crowbar.loc));
        Assert.Contains(texts, t => t.Contains("snaps back into your"));
        Assert.Empty(HoldersOf(replica, In(w, () => crowbar.dm_id)));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n", runtimes));
    }
}
