using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Ceilings;

public class CeilingTests
{
    private const string Glass = "/turf/open/floor/glass";

    [Fact]
    public void TwoBuildersCannotSpendMaterialsOnTheSameReinforcement()
    {
        var r = new T2Room();
        var alice = r.Join(2, 3, "alice");
        var bob = r.Join(2, 4, "bob");
        obj_item_stack_rods a = null, b = null;
        r.In(() =>
        {
            var s = CeilingSystem.Of(r.World); s.Initialize();
            Assert.Equal(CeilingState.Plating, s.At(2, 3, 1).Turf);
            a = Dm.@new<obj_item_stack_rods>(alice, 6);
            b = Dm.@new<obj_item_stack_rods>(bob, 6);
            Waiting.NoSleep(alice.put_in_hands(a)); Waiting.NoSleep(bob.put_in_hands(b));
            Assert.True(Dm.truthy(Waiting.NoSleep(alice.Adjacent(locate(2, 3, 1)))));
            // tg's two rods on plating make a reinforced floor after 3 seconds (plating.dm:45)
            Waiting.Detach(s.Interact(alice, locate(2, 3, 1)));
            Waiting.Detach(s.Interact(bob, locate(2, 3, 1)));
        });
        r.Step(80);
        r.In(() =>
        {
            Assert.Equal("/turf/open/floor/engine", CeilingSystem.Of(r.World).At(2, 3, 1).Turf);
            Assert.Equal(10, Dm.num(a.get_amount()) + Dm.num(b.get_amount()));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void MovingAwayCancelsConstructionWithoutTakingMaterials()
    {
        var r = new T2Room();
        var alice = r.Join(2, 3, "alice");
        obj_item_stack_rods rods = null;
        r.In(() =>
        {
            var s = CeilingSystem.Of(r.World); s.Initialize();
            rods = Dm.@new<obj_item_stack_rods>(alice, 6);
            Waiting.NoSleep(alice.put_in_hands(rods));
            Waiting.Detach(s.Interact(alice, locate(2, 3, 1)));
            Waiting.NoSleep(alice.forceMove(locate(5, 5, 1)));
        });
        r.Step(80);
        r.In(() =>
        {
            Assert.Equal(CeilingState.Plating, CeilingSystem.Of(r.World).At(2, 3, 1).Turf);
            Assert.Equal(6, Dm.num(rods.get_amount()));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void FloorRecipesBuildAndTakeApartTheCeiling()
    {
        var r = new T2Room();
        var player = r.Join(8, 8, "builder");
        r.In(() =>
        {
            Waiting.NoSleep(locate(7, 8, 1).ChangeTurf(typeof(turf_closed_wall))); // a new lattice must reach a wall (D295 keeps DV183's support)
            var s = CeilingSystem.Of(r.World); s.Initialize(); s.Set(8, 8, 1, default);
        });
        obj_item held = null;
        void Use<T>(int steps = 5) where T : obj_item
        {
            r.In(() =>
            {
                if (held != null) qdel(held);
                held = Dm.@new<T>(player); // not on the turf, where a new stack merges into the refunded one
                if (held is obj_item_stack stack) stack.amount = 6;
                Waiting.NoSleep(player.put_in_hands(held));
                Assert.Same(held, player.get_active_held_item());
                Waiting.Detach(CeilingSystem.Of(r.World).Interact(player, locate(8, 8, 1)));
            });
            r.Step(steps);
            r.AssertNoRuntimes();
        }
        CeilingState State() => r.In(() => CeilingSystem.Of(r.World).At(8, 8, 1));
        int Dropped<T>() where T : class => r.In(() => Dm.each<T>(locate(8, 8, 1)).Count(t => !ReferenceEquals(t, held)));
        Use<obj_item_stack_rods>();
        Assert.Equal(CeilingLattice.Lattice, State().Lattice);
        Assert.Equal(5, ((obj_item_stack)held).amount);
        Assert.False(State().Sealed);
        Use<obj_item_stack_rods>();
        Assert.Equal(CeilingLattice.Catwalk, State().Lattice);
        Use<obj_item_wirecutters>();
        Assert.False(State().Exists);
        Assert.Equal(1, Dropped<obj_item_stack_rods>());
        Use<obj_item_stack_rods>();
        Use<obj_item_stack_tile_iron>();
        Assert.Equal(CeilingState.Plating, State().Turf);
        Assert.True(State().Sealed);
        Use<obj_item_stack_tile_iron_dark>();
        Assert.Equal("/turf/open/floor/iron/dark", State().Turf);
        Use<obj_item_crowbar>();
        Assert.Equal(CeilingState.Plating, State().Turf);
        Assert.Equal(1, Dropped<obj_item_stack_tile_iron_dark>());
        Use<obj_item_stack_tile_glass>();
        Assert.Equal(Glass, State().Turf);
        Assert.True(State().TransmitsLight);
        Use<obj_item_crowbar>();
        Use<obj_item_stack_rods>(80);
        Assert.Equal("/turf/open/floor/engine", State().Turf);
        Use<obj_item_crowbar>();
        Assert.Equal("/turf/open/floor/engine", State().Turf); // tg's reinforced floor ignores a crowbar
        Use<obj_item_wrench>(80);
        Assert.Equal(CeilingState.Plating, State().Turf);
        // a floor thing is refused: the ceiling takes no cable
        Use<obj_item_stack_cable_coil>();
        Assert.Equal(CeilingState.Plating, State().Turf);
        // plating cannot be taken apart by hand, as in tg
        Use<obj_item_crowbar>();
        Use<obj_item_wirecutters>();
        Assert.Equal(CeilingState.Plating, State().Turf);
    }

    [Fact]
    public void ABrokenPlatingTakesNoTileUntilWelded()
    {
        var r = new T2Room();
        var player = r.Join(8, 8, "builder");
        obj_item_stack_tile_iron tiles = null;
        r.In(() =>
        {
            var s = CeilingSystem.Of(r.World); s.Initialize();
            s.BreakTile(8, 8, 1);
            Assert.NotEqual(0, s.At(8, 8, 1).Broken);
            tiles = Dm.@new<obj_item_stack_tile_iron>(player, 4);
            Waiting.NoSleep(player.put_in_hands(tiles));
            Waiting.Detach(s.Interact(player, locate(8, 8, 1)));
        });
        r.Step(5);
        r.In(() =>
        {
            Assert.Equal(CeilingState.Plating, CeilingSystem.Of(r.World).At(8, 8, 1).Turf);
            Assert.Equal(4, Dm.num(tiles.get_amount()));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALightBlastNeverOpensTheCeiling()
    {
        // tg's severities are LIGHT 1, HEAVY 2, DEVASTATE 3; a light blast only breaks a tile (floor.dm:48)
        var r = new T2Room();
        r.In(() =>
        {
            var s = CeilingSystem.Of(r.World); s.Initialize();
            for (int i = 0; i < 20; i++) s.ExAct(8, 8, 1, Defines.EXPLODE_LIGHT, null);
            Assert.True(s.At(8, 8, 1).Sealed);
            Assert.NotEqual(0, s.At(8, 8, 1).Broken);
            s.ExAct(8, 8, 1, Defines.EXPLODE_DEVASTATE, null);
            Assert.False(s.At(8, 8, 1).Exists);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ColumnSupportsCeilingsAndAnExplosionTakesThemAsAFloor()
    {
        var r = new T2Room();
        r.In(() =>
        {
            var s = CeilingSystem.Of(r.World); s.Initialize();
            foreach (var p in s.Cells.Keys.ToArray()) s.Set(p.X, p.Y, p.Z, default);
            Waiting.NoSleep(locate(8, 8, 1).ChangeTurf(typeof(turf_closed_wall_ceiling_column)));
            Assert.True(s.CanExtend(9, 8, 1));
            s.Set(9, 8, 1, CeilingState.Of(Glass));
            locate(9, 8, 1).ex_act(Defines.EXPLODE_DEVASTATE);
            Assert.False(s.At(9, 8, 1).Exists);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALatticeKeepsItsDamageBetweenBlastsAndDropsItsRodsWhenDestroyed()
    {
        // tg's obj take_damage: 50 integrity, nothing against a bomb (lattice.dm, obj_defense.dm:10)
        var r = new T2Room();
        r.In(() =>
        {
            var s = CeilingSystem.Of(r.World); s.Initialize();
            s.Set(8, 8, 1, CeilingState.Of(CeilingLattice.Catwalk));
            Assert.Equal(30f, s.DamageLattice(8, 8, 1, 30, Defines.BRUTE, Defines.BOMB, 0, false));
            Assert.Equal(300, s.At(8, 8, 1).Damage);
            Assert.Equal(CeilingLattice.Catwalk, s.At(8, 8, 1).Lattice);
            s.DamageLattice(8, 8, 1, 30, Defines.BRUTE, Defines.BOMB, 0, false);
            Assert.False(s.At(8, 8, 1).Exists);
            Assert.Equal(2, Dm.num(Dm.each<obj_item_stack_rods>(locate(8, 8, 1)).Single().amount));
            s.Set(8, 8, 1, CeilingState.Of(CeilingLattice.Lattice));
            s.ExAct(8, 8, 1, Defines.EXPLODE_HEAVY, null); // rand(100, 250) is past its 50
            Assert.False(s.At(8, 8, 1).Exists);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AToolThatBuildsNothingHitsTheLatticeThroughItsMeleeArmourAndCooldown()
    {
        var r = new T2Room();
        var player = r.Join(8, 8, "hitter");
        r.In(() =>
        {
            Waiting.NoSleep(locate(7, 8, 1).ChangeTurf(typeof(turf_closed_wall)));
            var s = CeilingSystem.Of(r.World); s.Initialize();
            s.Set(8, 8, 1, CeilingState.Of(CeilingLattice.Lattice));
            var crowbar = Dm.@new<obj_item_crowbar>(player);
            Waiting.NoSleep(player.put_in_hands(crowbar));
            Waiting.Detach(s.Interact(player, locate(8, 8, 1)));
            Waiting.Detach(s.Interact(player, locate(8, 8, 1))); // the second click is inside the attack's cooldown
        });
        r.Step(2);
        // a crowbar's 5 force times its demolition_mod of 1.25, through the lattice's melee armour of 50, is 3.1 (to tg's 0.1)
        Assert.Equal(31, r.In(() => CeilingSystem.Of(r.World).At(8, 8, 1).Damage));
        r.Step(20);
        r.In(() => Waiting.Detach(CeilingSystem.Of(r.World).Interact(player, locate(8, 8, 1))));
        r.Step(2);
        Assert.Equal(62, r.In(() => CeilingSystem.Of(r.World).At(8, 8, 1).Damage));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCeilingIsDrawnWithTgsSmoothedSpriteAndBrokenOverlay()
    {
        var r = new T2Room();
        r.In(() =>
        {
            var s = CeilingSystem.Of(r.World); s.Initialize();
            s.Set(8, 8, 1, CeilingState.Of(Glass));
            Assert.Equal("glass-0", s.Appearance(8, 8, 1).icon_state);
            s.Set(9, 8, 1, CeilingState.Of(Glass));
            Assert.Equal("glass-" + Defines.EAST_JUNCTION, s.Appearance(8, 8, 1).icon_state);
            Assert.Equal("plating", s.Appearance(8, 9, 1).icon_state);
            s.BreakTile(8, 9, 1);
            Assert.Equal(1, s.Appearance(8, 9, 1).overlays.len);
            Assert.Equal("glass floor", s.NameAt(8, 8, 1));
            s.Set(8, 8, 1, default);
            Assert.Null(s.Appearance(8, 8, 1));
        });
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData("/turf/open/floor/iron/solarpanel/airless", "/area/station/solars/aft", false)]
    [InlineData("/turf/open/floor/plating", "/area/station/maintenance/solars/aft", true)]
    [InlineData("/turf/open/space", "/area/space", false)]
    [InlineData("/turf/open/floor/plating/airless", "/area/station/engineering", true)]
    public void InitialRoofUsesLocationNotPressure(string turf, string area, bool expected)
        => Assert.Equal(expected, CeilingSystem.DefaultPresent(turf, area));

    [Fact]
    public void An_outdoor_area_has_no_roof() // D304: Lavaland's surface, tg's area.outdoors
        => Assert.False(CeilingSystem.DefaultPresent("/turf/open/misc/asteroid/basalt/lava_land_surface", "/area/lavaland/surface/outdoors", outdoors: true));

    [Fact]
    public void ReplacingFloorDoesNotRecreateOrDestroyItsCeiling()
    {
        var r = new T2Room();
        r.In(() =>
        {
            var s = CeilingSystem.Of(r.World); s.Initialize();
            s.Set(8, 8, 1, CeilingState.Of(Glass));
            Waiting.NoSleep(locate(8, 8, 1).ChangeTurf(typeof(turf_open_space)));
            Assert.Equal(Glass, s.At(8, 8, 1).Turf);
            s.Set(8, 8, 1, default);
            Waiting.NoSleep(locate(8, 8, 1).ChangeTurf(typeof(turf_open_floor_plating)));
            Assert.False(s.At(8, 8, 1).Exists);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void LosingSupportRetainsSealAndStopsExpansionUntilWallIsRestored()
    {
        var r = new T2Room();
        r.In(() =>
        {
            var s = CeilingSystem.Of(r.World); s.Initialize();
            foreach (var p in s.Cells.Keys.ToArray()) s.Set(p.X, p.Y, p.Z, default);
            Waiting.NoSleep(locate(8, 8, 1).ChangeTurf(typeof(turf_closed_wall)));
            s.Set(9, 8, 1, CeilingState.Of(CeilingState.Plating));
            s.Set(10, 8, 1, CeilingState.Of(CeilingState.Plating));
            Assert.True(s.CanExtend(11, 8, 1));
            Waiting.NoSleep(locate(8, 8, 1).ChangeTurf(typeof(turf_open_floor_plating)));
            s.RefreshSupport();
            Assert.True(s.At(10, 8, 1).Sealed);
            Assert.False(s.At(10, 8, 1).Supported);
            Assert.False(s.CanExtend(11, 8, 1));
            Waiting.NoSleep(locate(8, 8, 1).ChangeTurf(typeof(turf_closed_wall)));
            Assert.True(s.CanExtend(11, 8, 1));
            Assert.False(s.CanExtend(11, 9, 1)); // diagonal contact does not support a new frame
        });
    }

    [Fact]
    public void OnlyUnsealedBoundariesLeakAndRepairStopsTheOutlet()
    {
        var r = new T2Room();
        r.In(() =>
        {
            var s = CeilingSystem.Of(r.World); s.Initialize();
            var cell = (turf_open)locate(8, 8, 1);
            float Pressure() => Dm.num(cell.air.return_pressure());
            float pressure = Pressure();
            Assert.True(pressure > 90);
            s.Set(8, 8, 1, CeilingState.Of(Glass));
            s.ExchangeExterior(); Assert.Equal(pressure, Pressure());
            s.Set(8, 8, 1, CeilingState.Of("/turf/open/floor/iron"));
            s.ExchangeExterior(); Assert.Equal(pressure, Pressure());
            s.Set(8, 8, 1, CeilingState.Of(CeilingLattice.Lattice));
            s.ExchangeExterior(); Assert.True(Pressure() < pressure * .1f);
            Assert.Same(cell, locate(8, 8, 1));
            s.Set(8, 8, 1, CeilingState.Of(CeilingState.Plating));
            DevCommands.Gas(Defines.OPENTURF_DEFAULT_ATMOS, 8, 8, 1);
            pressure = Pressure(); s.ExchangeExterior(); Assert.Equal(pressure, Pressure());
        });
        r.AssertNoRuntimes();
    }

    private sealed class CeilingFlow : IAirflowSink
    {
        public readonly List<(int X, int Y, int Direction)> Flows = new();
        public void Flow(turf_open cell, float difference, int direction)
        {
            if (difference > 0) Flows.Add(((int)cell.x, (int)cell.y, direction));
        }
        public void CycleEnd() { }
    }

    [Fact]
    public void BreachDrawsHorizontalAirflowAndLooseItemsTowardItsCell()
    {
        var r = new T2Room();
        var flow = new CeilingFlow(); AirflowSeam.Install(r.World, flow);
        obj_item_stack_rods item = null;
        r.In(() =>
        {
            var s = CeilingSystem.Of(r.World); s.Initialize();
            item = Dm.@new<obj_item_stack_rods>(locate(9, 8, 1));
            s.Set(8, 8, 1, default);
        });
        r.Step(40);
        Assert.Contains(flow.Flows, f => f.X == 9 && f.Y == 8 && f.Direction == 8);
        Assert.Contains(flow.Flows, f => f.X == 8 && f.Y == 8 && f.Direction == (int)Dm.UP);
        Assert.False(QDELETED(item));
        Assert.Equal(1, item.z);
        Assert.True(item.x < 9, $"loose item did not approach the breach: {item.x},{item.y}");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APlayerAtTheOutletStaysOnTheSameLevel()
    {
        var r = new T2Room();
        var player = r.Join(8, 8, "ceiling-test");
        r.In(() => { var s = CeilingSystem.Of(r.World); s.Initialize(); s.Set(8, 8, 1, default); });
        r.Step(40);
        Assert.Equal(1, Dm.num(player.z));
        Assert.False(QDELETED(player));
        r.AssertNoRuntimes();
    }
}
