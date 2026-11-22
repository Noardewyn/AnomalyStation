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
/// The M7 shocks slice through the real server on MetaStation: a bare-handed player cuts a live cable of the station's grid
/// with wirecutters (cable.dm:210: shock(user, 50), cable.dm:282, objs.dm:402, power.dm:478). When the coin falls he is
/// shocked: burnt by tg's damage of the grid's power, stunned, and his client sees the sparks and his own body black with the
/// skeleton flash of human.dmi (electrocution_animation, human.dm:634) and hears the sparks; the cable stays. When the coin
/// misses, the cable is cut and he lays the piece back in insulated gloves, and tries again. In insulated gloves the cut goes through and he is not
/// shocked. Last, a grille over the live cable shocks him when he punches it (grille.dm:169, :354). No runtime.
/// </summary>
public class ShockGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const string SparksPath = "/obj/effect/particle_effect/sparks";

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    // with air: a solar array's cables lie on space (a station area, open turfs), where the cold and the vacuum keep burning
    // the player (body_temperature_damage, _species.dm:1243) while the test compares his burn before and after a cut
    private static bool Clear(turf t) => t is turf_open o && o.air != null && Dm.num(o.air.return_pressure()) > WARNING_LOW_PRESSURE
        && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    // An exposed cable on plating in a powered, pressurized station room, alone on its tile with no power machine, whose network
    // carries enough power to shock (ELECTROCUTE_DAMAGE: 1 kW, powernet.dm:95), with an open tile north of it for the player.
    private static obj_structure_cable PickCable()
    {
        foreach (obj_structure_cable c in Dm.each_cast<obj_structure_cable>(GLOB.cable_list).OrderBy(c => c.dm_id))
        {
            if (c.loc is not turf_open t || !Dm.eq(c.z, 1) || t.underfloor_accessibility < UNDERFLOOR_INTERACTABLE || Dm.truthy(t.overfloor_placed))
                continue;
            if (c.GetType() != typeof(obj_structure_cable) || !Dm.eq(c.linked_dirs, EAST + WEST) || !Clear(t) || !Clear(get_step(t, NORTH)))
                continue;
            if (Dm.each_cast<atom>(t.contents).Any(a => a != c && (a is obj_structure_cable || a is obj_machinery_power)))
                continue;
            if (get_area(c) is not area room || !Dm.@in(room.type, GLOB.the_station_areas))
                continue;
            if (c.powernet == null || Dm.num(c.powernet.get_electrocute_damage()) < 20)
                continue;
            return c;
        }
        return null;
    }

    [MetaServerFact]
    public void A_bare_handed_cut_of_a_live_cable_shocks_and_flashes_the_player_insulated_gloves_stop_it_and_a_live_grille_bites()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 79, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        // a network carries power from SSmachines' second fire on: its first fire comes up to 2 s after start (master.dm:621)
        // and each fire's reset publishes the supply the previous cycle gathered (machines.dm:95, wait 2 s)
        obj_structure_cable cable = null;
        for (int i = 0; i < 5 && cable == null; i++)
        {
            w.Run(Second);
            cable = In(w, PickCable);
        }
        Assert.NotNull(cable);
        int x = In(w, () => (int)cable.x), y = In(w, () => (int)cable.y), z = In(w, () => (int)cable.z);
        output.WriteLine($"the cable at {x},{y} in {In(w, () => Dm.str(((area)get_area(cable)).name))}: its network carries {In(w, () => cable.powernet.avail)} J a cycle");
        obj_item_wirecutters cutters = null;
        In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(locate(x, y + 1, z)));
            if (me.gloves != null)
                qdel(me.gloves);
            cutters = Dm.@new<obj_item_wirecutters>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(cutters))));
        });
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        // a piece laid on a live network shocks the bare hand and falls apart on a coin (cable.dm:721): it is laid in
        // insulated gloves (power.dm:486) so the shock under test is the cut's
        void LayBack()
        {
            Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, x, y, z), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
        }
        ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);
        ReplicaEntity Own() => replica.Entities.Single(e => e.Id == me.dm_id);
        float Burn() => In(w, () => Dm.num(me.get_fire_loss()));

        // 1. bare-handed: the cut until the coin of shock(user, 50) falls; a miss cuts the cable and the piece is laid back
        bool flashed = false, black = false, sparksSeen = false;
        void Watch()
        {
            if (!replica.TryGetAppearance(Own().State.Appearance, out AppearanceEntry look))
                return;
            flashed |= CrewOutfitTests.Icons(replica, Own().State.Appearance).Any(i => i.State == "electrocuted_base");
            black |= look.Rgba == 0x000000FF;
            sparksSeen |= replica.Entities.Any(e => e.TypePath == SparksPath);
        }
        int attempts = 0;
        while (Burn() == 0 && attempts++ < 6)
        {
            Assert.True(p.Session.Click(On(cable), ClickButton.Left, ClickModifiers.None));
            for (int i = 0; i < Second; i++)
            {
                w.Run(1);
                Watch();
            }
            if (Burn() > 0)
                break;
            Assert.True(In(w, () => QDELETED(cable)), "no shock: the cable is cut");
            obj_item_stack_cable_coil piece = In(w, () => Dm.locate<obj_item_stack_cable_coil>(locate(x, y, z)));
            In(w, () =>
            {
                Waiting.NoSleep(me.dropItemToGround(cutters));
                Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(piece))));
                Assert.True(Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_gloves_color_yellow>(me.loc), ITEM_SLOT_GLOVES))));
            });
            LayBack();
            cable = In(w, () => Dm.locate<obj_structure_cable>(locate(x, y, z)));
            Assert.NotNull(cable);
            In(w, () =>
            {
                qdel(me.gloves);
                Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(cutters))));
            });
            w.Run(Second);
        }
        float burn = Burn();
        output.WriteLine($"shocked after {attempts} cut(s): {burn} burn, stunned {In(w, () => Dm.num(me.AmountStun()))} ds; the client saw the flash {flashed}, black {black}, sparks {sparksSeen}; heard {string.Join(", ", heard.Select(h => h.File).Where(f => f != null).Distinct())}");
        Assert.True(burn > 0, "the live cable shocked the bare hand");
        Assert.False(In(w, () => QDELETED(cable)), "the shock stopped the cut");
        Assert.True(In(w, () => Dm.truthy(me.IsStun()) || Dm.truthy(me.IsParalyzed())), "the shock stuns");
        Assert.True(flashed, "the client sees the skeleton flash on the body");
        Assert.True(black, "the client sees the body turned black");
        Assert.True(sparksSeen, "the client sees the sparks");
        Assert.Contains(heard, h => h.File != null && (h.File ?? "").Contains("sparks", StringComparison.Ordinal));

        // the flash ends with the animation (four seconds); the stun and the secondary paralysis wear off
        w.Run(12 * Second);
        Assert.DoesNotContain(CrewOutfitTests.Icons(replica, Own().State.Appearance), i => i.State == "electrocuted_base");
        Assert.False(In(w, () => Dm.truthy(me.IsStun()) || Dm.truthy(me.IsParalyzed())));

        // 2. in insulated gloves the cut goes through and nothing shocks (the stun blocked his hands: he dropped the cutters,
        // on_handsblocked_start, living.dm:2746, and picks them up)
        In(w, () =>
        {
            Assert.NotSame(cutters, me.get_active_held_item());
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(cutters))));
            var gloves = Dm.@new<obj_item_clothing_gloves_color_yellow>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(gloves, ITEM_SLOT_GLOVES))));
            Assert.Equal(0f, gloves.siemens_coefficient);
        });
        w.Run(Second);
        Assert.True(p.Session.Click(On(cable), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => QDELETED(cable)), "the insulated hand cut the live cable");
        Assert.Equal(burn, Burn());

        // 3. the piece laid back, the gloves off, a grille over the live cable: a punch shocks (shock(user, 70) on each hit)
        obj_item_stack_cable_coil coil = In(w, () => Dm.locate<obj_item_stack_cable_coil>(locate(x, y, z)));
        In(w, () =>
        {
            qdel(cutters);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(coil))));
        });
        w.Run(2);
        LayBack();
        Assert.NotNull(In(w, () => Dm.locate<obj_structure_cable>(locate(x, y, z))));
        In(w, () => qdel(me.gloves));
        obj_structure_grille grille = In(w, () => Dm.@new<obj_structure_grille>(locate(x, y, z)));
        w.Run(2 * Second);
        Assert.True(In(w, () => Dm.truthy(grille.is_shocked())), "the grille is live");
        Assert.Contains(replica.Entities, e => e.Id == grille.dm_id);
        for (int i = 0; i < 8 && Burn() == burn; i++)
        {
            Assert.True(p.Session.Click(On(grille), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
        }
        output.WriteLine($"the grille: {Burn() - burn} more burn");
        Assert.True(Burn() > burn, "the live grille shocked the punching player");
        Assert.True(In(w, () => Dm.truthy(me.IsStun()) || Dm.truthy(me.IsParalyzed())));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the player did ran into a runtime
    }
}
