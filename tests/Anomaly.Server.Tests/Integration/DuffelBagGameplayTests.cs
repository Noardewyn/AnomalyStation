using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Server.Tests.Bots;
using Anomaly.Tools.Bots;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// tg's duffel bag through the real server (code/game/objects/items/storage/dufflebags.dm): worn on the back it starts zipped (its storage
/// soft-locked, its contents hidden, no unzipped slowdown); its Unzip button takes tg's 2.1 s do_after, then the storage opens, an item goes
/// in and the unzipped slowdown (1) applies; using it in the open state zips it (0.5 s) and closes the storage again.
/// </summary>
public class DuffelBagGameplayTests
{
    private const int Second = 20; // ticks per second

    [Fact]
    public void A_duffel_bag_on_the_back_starts_zipped_unzips_slowly_takes_an_item_and_zips_again()
    {
        using var rig = new BotServerRig(strict: true);
        Bot alice = rig.AddBot("Alice", BotBehaviour.Idle);
        rig.UntilJoined(alice);
        var me = (mob_living_carbon_human)rig.MobOf("Alice");

        T In<T>(Func<T> f)
        {
            using (rig.World.Enter())
                return f();
        }

        if (In(() => me.back) != null)
        {
            In(() => Waiting.NoSleep(me.dropItemToGround(me.back)));
            rig.Tick(5);
        }
        var bag = In(() => Dm.@new<obj_item_storage_backpack_duffelbag>(me.loc));
        Assert.True(In(() => Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(bag, ITEM_SLOT_BACK)))));
        rig.Tick(5);
        var zipper = In(() => Assert.Single(Dm.each_cast<datum_action_item_action_zipper>(bag.actions)));
        var crowbar = In(() => Dm.@new<obj_item_crowbar>(me.loc));

        bool Zipped() => In(() => Dm.truthy(bag.zipped_up));
        bool Slowed() => In(() => Dm.truthy(me.has_movespeed_modifier(typeof(datum_movespeed_modifier_equipment_speedmod))));

        // Zipped at spawn: soft-locked, contents hidden, no slowdown, tg's "Unzip" button, nothing goes in.
        Assert.True(Zipped());
        Assert.Equal(STORAGE_SOFT_LOCKED, In(() => bag.atom_storage.locked));
        Assert.Equal(0, In(() => bag.atom_storage.display_contents));
        Assert.False(Slowed());
        Assert.Equal("Unzip", In(() => zipper.name));
        Assert.False(In(() => Dm.truthy(bag.atom_storage.attempt_insert(crowbar, me))), "nothing goes into a zipped bag");

        // The Unzip button: still zipped after a second, unzipped after tg's 2.1 s.
        In(() => Waiting.Detach(zipper.Trigger(me)));
        rig.Tick(Second);
        Assert.True(Zipped(), "unzipping takes 2.1 seconds");
        rig.Tick(Second + 10);
        Assert.False(Zipped());
        Assert.Equal(STORAGE_NOT_LOCKED, In(() => bag.atom_storage.locked));
        Assert.Equal(1, In(() => bag.atom_storage.display_contents));
        Assert.True(Slowed(), "the unzipped bag slows its wearer");
        Assert.Equal(1f, In(() => bag.slowdown) - In(() => Dm.initial(bag, (obj_item i) => i.slowdown)));
        Assert.Equal("Zip", In(() => zipper.name));

        // Unzipped, an item goes in.
        Assert.True(In(() => Dm.truthy(bag.atom_storage.attempt_insert(crowbar, me))));
        Assert.Same(bag, In(() => crowbar.loc));

        // Used in the open state, the bag zips again after 0.5 s and the storage closes.
        In(() => Waiting.Detach(bag.attack_self(me)));
        rig.Tick(Second);
        Assert.True(Zipped());
        Assert.Equal(STORAGE_SOFT_LOCKED, In(() => bag.atom_storage.locked));
        Assert.Equal(0, In(() => bag.atom_storage.display_contents));
        Assert.False(Slowed());
        Assert.Equal("Unzip", In(() => zipper.name));
    }
}
