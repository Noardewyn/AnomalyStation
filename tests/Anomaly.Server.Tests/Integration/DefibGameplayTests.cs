using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
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
/// M8 slice E through the real server on MetaStation with headless clients: Bob is killed by a dev command and ghosts (a
/// movement key, tg's client/Move); Alice picks up one of medbay's loaded defibrillators, hooks it into medbay's wall mount,
/// takes the paddles off the wall with an empty hand, wields them (Z) and clicks Bob's chest. After tg's 3 + 2 seconds Bob is
/// alive halfway into crit and his client drives his body again (grab_ghost); Alice reads the unit's ping as a viewer and she
/// hears tg's charge, zap and success sounds; the mount's picture holds the hooked unit with its safety light and charge while the
/// paddles are in Alice's hands.
/// </summary>
public class DefibGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    // A loaded defibrillator of medbay's storage and the wall mount nearest to it (the treatment center's), the mount's tile with an
    // open neighbour for the patient.
    private static (obj_item_defibrillator Defib, obj_machinery_defibrillator_mount Mount, turf Patient) Pick()
    {
        var mounts = new List<obj_machinery_defibrillator_mount>();
        var defibs = new List<obj_item_defibrillator_loaded>();
        for (int x = 1; x <= Dm.num(world.maxx); x++)
        {
            for (int y = 1; y <= Dm.num(world.maxy); y++)
            {
                foreach (obj_item_defibrillator_loaded d in Dm.each<obj_item_defibrillator_loaded>(locate(x, y, 1)))
                    defibs.Add(d);
                foreach (obj_machinery_defibrillator_mount m in Dm.each<obj_machinery_defibrillator_mount>(locate(x, y, 1)))
                    mounts.Add(m);
            }
        }
        obj_item_defibrillator_loaded near = defibs.OrderBy(d => d.dm_id).FirstOrDefault();
        if (near == null)
            return (null, null, null);
        foreach (obj_machinery_defibrillator_mount mount in mounts.OrderBy(m => Dm.num(get_dist(near, m))).ThenBy(m => m.dm_id))
        {
            turf at = (turf)mount.loc;
            turf patient = new[] { NORTH, SOUTH, EAST, WEST }.Select(d => get_step(at, d) as turf).FirstOrDefault(t => t != null && Clear(t));
            if (patient == null || !Clear(at))
                continue;
            return (near, mount, patient);
        }
        return (null, null, null);
    }

    [MetaServerFact]
    public void Alice_revives_the_ghosted_Bob_with_medbays_defibrillator_from_the_wall_mount()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 83, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var texts = new List<string>();
        alice.Client.ServerTextReceived += t => texts.Add(t.Html);
        var heard = new List<SoundEvent>();
        alice.Session.Replica.SoundsReceived += events => heard.AddRange(events);
        w.Run(Second);

        (obj_item_defibrillator defib, obj_machinery_defibrillator_mount mount, turf patient) = In(w, Pick);
        Assert.NotNull(defib);
        output.WriteLine(In(w, () => $"the defib at {defib.x},{defib.y}, the mount at {mount.x},{mount.y} in {Dm.str(((area)get_area(mount)).name)}"));
        Do(w, () =>
        {
            Waiting.NoSleep(aliceMob.forceMove(get_turf(defib)));
            Waiting.NoSleep(bobMob.forceMove(patient));
        });
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        ClickTarget On(atom a, AimZone zone = AimZone.None) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16, zone);

        // 1. Alice picks the unit up, walks to the mount and hooks it up.
        Assert.True(alice.Session.Click(On(defib), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Same(aliceMob, In(w, () => defib.loc));
        Do(w, () => Waiting.NoSleep(aliceMob.forceMove(mount.loc)));
        w.Run(5);
        Assert.True(alice.Session.Click(On(mount), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Same(mount, In(w, () => defib.loc));
        Assert.Contains(heard, h => h.File == "sound/machines/click.ogg");

        // 2. Bob dies (dev) and ghosts: a movement key moves the ghost off his body.
        alice.Session.DevCommand("kill Bob");
        w.Run(5);
        Assert.Equal((float)DEAD, In(w, () => bobMob.stat));
        bob.Yaw = 0;
        bob.Keys = MoveKeys.Forward;
        w.Run(6);
        bob.Keys = MoveKeys.None;
        w.Run(3);
        Assert.True(bob.Session.Ghosting, "Bob's client drives a ghost");
        Assert.NotEqual(bobMob.dm_id, bob.Client.OwnEntity);

        // 3. The paddles off the wall (an empty hand), wielded with Z.
        Assert.True(alice.Session.Click(On(mount), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Same(aliceMob, In(w, () => defib.paddles.loc));
        alice.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(5);
        Assert.True(In(w, () => HAS_TRAIT(defib.paddles, TRAIT_WIELDED)), "Z wields the paddles");
        Assert.Equal("defibpaddles1", In(w, () => defib.paddles.icon_state));

        // the mount's picture: the hooked unit with its safety light and charge (tg's mount draws no paddles)
        Assert.True(alice.Session.Replica.TryGet(mount.dm_id, out ReplicaEntity mountSeen));
        List<(string Icon, string State, float Layer)> looks = CrewOutfitTests.Icons(alice.Session.Replica, mountSeen.State.Appearance);
        output.WriteLine("the mount: " + string.Join(", ", looks.Select(l => l.State)));
        Assert.Contains(looks, l => l.Icon == "icons/obj/machines/defib_mount.dmi" && l.State == "defib");
        Assert.Contains(looks, l => l.State == "online");
        Assert.Contains(looks, l => l.State != null && l.State.StartsWith("charge", StringComparison.Ordinal));

        // 4. The click on Bob's chest: 3 s to place the paddles, 2 s to shock.
        heard.Clear();
        Assert.True(alice.Session.Click(On(bobMob, AimZone.Chest), ClickButton.Left, ClickModifiers.None));
        // Bob's health is read in the tick of the shock: each Life run of a man in hard crit costs him a little health (he cannot breathe,
        // carbon/life.dm:86), and the Master staggers SSmobs' first fire by a random 0 to 2 s (master.dm:621), so a fixed number of ticks
        // after the shock may hold one
        int waited = 0;
        for (; In(w, () => bobMob.stat) == DEAD; waited++)
        {
            Assert.True(waited < 5 * Second + 10, "the shock did not revive Bob within its delays");
            w.Run(1);
        }
        float revivedHealth = In(w, () => bobMob.health);
        w.Run(5 * Second + 10 - waited);
        Assert.Equal(BODY_ZONE_CHEST, In(w, () => aliceMob.zone_selected));
        output.WriteLine(string.Join("\n", texts.Where(t => t.Contains("defib", StringComparison.OrdinalIgnoreCase) || t.Contains("paddles", StringComparison.Ordinal))));
        Assert.NotEqual((float)DEAD, In(w, () => bobMob.stat));
        Assert.Equal(-50f, revivedHealth, 3); // tg's HALFWAYCRITDEATH: halfway between crit (0) and death (-100)
        Assert.Contains(texts, t => t.Contains("pings: Resuscitation successful.", StringComparison.Ordinal));
        foreach (string file in new[] { "sound/machines/defib/defib_charge.ogg", "sound/machines/defib/defib_zap.ogg", "sound/machines/defib/defib_success.ogg" })
            Assert.Contains(heard, h => h.File == file);

        // Bob's client is back in his body
        w.Run(5);
        Assert.False(bob.Session.Ghosting, "Bob's client drives his body again");
        Assert.Equal(bobMob.dm_id, bob.Client.OwnEntity);
        Assert.Same(bobMob, In(w, () => bobMob.client?.mob));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
