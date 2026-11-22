using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tests.Tg.M4aTools.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tests.Playtest;

/// <summary>
/// The owner's M5 play test (2026-10-16): a lit welder welds airlocks but not the other doors and lockers. tg's rules, through the
/// real click chain: an airlock (airlock.dm:1189) and a firelock (firedoor.dm:533) weld shut on a right click (welder_act_secondary), a
/// closed locker on a left click (closets.dm:924, 4 s), a crate never (can_weld_shut = FALSE, crates.dm:186).
/// </summary>
public class WeldingTests
{
    private const int Second = 20;
    private const string Left = "left=1;button=left";
    private const string Right = "right=1;button=right";

    private static void Click(CapsuleRoom room, mob who, atom target, string @params, int seconds)
    {
        room.Command(() => ClientSeam.Click(who.client, target, @params));
        room.Step(seconds * Second);
    }

    private static (CapsuleRoom, mob_living_carbon_human) LitWelder(ulong seed)
    {
        var (room, a) = P4aToolTests.Room(seed);
        var welder = P4aToolTests.Hold<obj_item_weldingtool>(room, a);
        room.In(() => Waiting.NoSleep(welder.attack_self(a)));
        return (room, a);
    }

    [Fact]
    public void ARightClickWeldsAFirelockShut()
    {
        var (room, a) = LitWelder(9701);
        var door = room.In(() => Dm.@new<obj_machinery_door_firedoor>(locate(6, 5, 1)));
        room.Step(Second);
        room.In(() => Waiting.Detach(door.close()));
        room.Step(2 * Second);
        Assert.True(room.In(() => Dm.truthy(door.density)), "closed");
        Click(room, a, door, Right, 6);
        Assert.True(room.In(() => Dm.truthy(door.welded)), "welded shut");
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnOpenFirelockIsWeldedToo()
    {
        var (room, a) = LitWelder(9703);
        var door = room.In(() => Dm.@new<obj_machinery_door_firedoor>(locate(6, 5, 1)));
        room.Step(Second);
        Assert.False(room.In(() => Dm.truthy(door.density)), "open");
        Click(room, a, door, Right, 6);
        Assert.True(room.In(() => Dm.truthy(door.welded)), "welded open (firedoor.dm:533 has no density check)");
        room.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(typeof(obj_structure_closet))]
    [InlineData(typeof(obj_structure_closet_crate))]
    [InlineData(typeof(obj_structure_closet_emcloset))]
    [InlineData(typeof(obj_structure_closet_toolcloset))]
    [InlineData(typeof(obj_structure_closet_firecloset))]
    [InlineData(typeof(obj_structure_closet_secure_closet_personal))]
    public void ALeftClickWeldsAClosedLockerOrCrateShut(Type type)
    {
        var (room, a) = LitWelder(9702);
        var locker = room.In(() => (obj_structure_closet)Dm.@new(type, locate(6, 5, 1)));
        room.Step(Second);
        Click(room, a, locker, Left, 6);
        Assert.True(room.In(() => Dm.truthy(locker.welded)), "welded shut");
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WeldingWithBareEyesBurnsThem()
    {
        var (room, a) = LitWelder(9705);
        var eyes = room.In(() => (obj_item_organ_eyes)a.get_organ_slot(Defines.ORGAN_SLOT_EYES));
        var door = room.In(() => Dm.@new<obj_machinery_door_firedoor>(locate(6, 5, 1)));
        room.Step(Second);
        Click(room, a, door, Right, 6);
        Assert.True(room.In(() => Dm.num(eyes.damage)) > 0, "tool_flash on the bare eyes (tool_flash.dm:33, carbon_defense.dm:432)");
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheWeldingHardHatsVisorProtectsTheEyes()
    {
        var (room2, b) = LitWelder(9707);
        var hat = room2.In(() => Dm.@new<obj_item_clothing_head_utility_hardhat_welding_up>(b.loc));
        room2.In(() => Assert.Equal(0, Dm.num(hat.tint)));
        room2.In(() => Assert.Equal(0, Dm.num(hat.flash_protect)));
        room2.In(() => Waiting.NoSleep(b.equip_to_slot_if_possible(hat, Defines.ITEM_SLOT_HEAD)));
        room2.In(() => Waiting.NoSleep(hat.adjust_visor(b)));
        room2.In(() => Assert.Equal(2, Dm.num(hat.tint)));
        var eyes2 = room2.In(() => (obj_item_organ_eyes)b.get_organ_slot(Defines.ORGAN_SLOT_EYES));
        var door2 = room2.In(() => Dm.@new<obj_machinery_door_firedoor>(locate(6, 5, 1)));
        room2.Step(Second);
        Click(room2, b, door2, Right, 6);
        Assert.Equal(0f, room2.In(() => Dm.num(eyes2.damage)));
        room2.AssertNoRuntimes();
    }

    [Fact]
    public void TheEngineersWeldingHardHatStartsWithItsVisorUpAndItsLampOff()
    {
        var (room, a) = LitWelder(9709);
        var hat = room.In(() => Dm.@new<obj_item_clothing_head_utility_hardhat_welding_up>(a.loc));
        room.In(() =>
        {
            Assert.Equal(1, Dm.num(hat.up));
            Assert.Equal(0, Dm.num(hat.tint));
            Assert.False(Dm.truthy(hat.light_on));
            Assert.Equal("hardhat0_yellow", hat.icon_state);
        });
        room.In(() => Waiting.NoSleep(hat.attack_self(a)));
        room.In(() =>
        {
            Assert.True(Dm.truthy(hat.light_on));
            Assert.Equal("hardhat1_yellow", hat.icon_state);
        });
        room.AssertNoRuntimes();
    }
}
