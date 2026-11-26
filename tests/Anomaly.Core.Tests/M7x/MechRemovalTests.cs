using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7x;

/// <summary>
/// tg's mech removal crowbar (crowbar.dm:353-412): two handed, 5 force in one hand and 19 wielded with the "mechremoval1" icon state;
/// one hand only gets "not wielded!", wielded it pries the pilot out of a mech in 5 seconds (an enclosed mech).
/// </summary>
public class MechRemovalTests
{
    private const int LineY = 4, Second = 20;

    [Fact]
    public void AWieldedRemoverPriesThePilotOutAndAOneHandedOneCannot()
    {
        var r = new AudioRoom(12, 9, seed: 4250);
        mob_living_carbon_human alice = r.Join(2, LineY, "Alice");
        mob_living_carbon_human bob = r.Join(4, LineY, "Bob");
        var mech = r.In(() => Dm.@new<obj_vehicle_sealed_mecha_gygax_dark_loaded>(locate(3, LineY, 1)));
        var remover = r.In(() => Dm.@new<obj_item_crowbar_mechremoval>(locate(2, LineY, 1)));
        r.Step(Second);
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(mech.mob_enter(bob)))));
        r.In(() => Waiting.NoSleep(alice.put_in_hands(remover)));
        r.Step(2);
        Assert.Equal(5f, r.In(() => remover.force));

        // one hand: refused at once
        r.In(() => Waiting.Detach(remover.empty_mech(mech, alice)));
        r.Step(6 * Second);
        Assert.Same(mech, r.In(() => bob.loc));

        // wielded (use in hand, the activate_inhand keybinding)
        r.Command(() => ClientSeam.Keybinding(alice.client, 4, true));
        r.Command(() => ClientSeam.Keybinding(alice.client, 4, false));
        r.Step(2);
        Assert.True(r.In(() => HAS_TRAIT(remover, TRAIT_WIELDED)));
        Assert.Equal(19f, r.In(() => remover.force));
        Assert.Equal("mechremoval1", r.In(() => remover.icon_state));

        r.In(() => Waiting.Detach(remover.empty_mech(mech, alice)));
        r.Step(3 * Second);
        Assert.Same(mech, r.In(() => bob.loc)); // still prying: an enclosed mech takes 5 seconds
        r.Step(3 * Second);
        Assert.NotSame(mech, r.In(() => bob.loc));
        Assert.Equal(0, r.In(() => (int)Dm.num(LAZYLEN(mech.occupants))));
        r.AssertNoRuntimes();
    }
}
