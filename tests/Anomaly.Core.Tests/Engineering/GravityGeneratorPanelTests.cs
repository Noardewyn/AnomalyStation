using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Engineering package E4: the main gravity generator's tgui panel (gravitygenerator.dm:281-310): ui_interact opens the
/// "GravityGenerator" window, ui_data gives the breaker, the charge, the charging state, on and operational, and ui_act's one act,
/// gentoggle, flips the breaker and starts the discharge or the charge (set_power), which process works off two points at a time.
/// </summary>
public class GravityGeneratorPanelTests
{
    private static DmList Data(obj_machinery_gravity_generator_main gen, mob who) => (DmList)Waiting.NoSleep(gen.ui_data(who));

    [Fact]
    public void TheBreakerStartsTheDischargeAndTheChargeAndThePanelFollowsIt()
    {
        var r = new AudioRoom(11, 11, seed: 7501);
        var bob = r.Join(5, 4, "Bob");
        r.Step(20);
        r.In(() =>
        {
            var gen = Dm.@new<obj_machinery_gravity_generator_main>(locate(5, 5, 1));
            Assert.Equal(1f, Dm.num(SSmapping.gravity_by_z_level[1]));

            // ui_interact opens tg's window for the user
            Waiting.NoSleep(gen.ui_interact(bob));
            datum_tgui ui = Assert.IsType<datum_tgui>(SStgui.get_open_ui(bob, gen));
            Assert.Equal("GravityGenerator", ui.@interface);

            // ui_data: a charged generator (its first process turns the start-up charge, set_power's POWER_UP, into enable)
            gen.process(null);
            DmList data = Data(gen, bob);
            Assert.Equal((1f, 100f, 0f, 1f, 1f), (Dm.num(data["breaker"]), Dm.num(data["charge_count"]), Dm.num(data["charging_state"]), Dm.num(data["on"]), Dm.num(data["operational"])));

            // gentoggle: the breaker off, the generator discharges two points a process, the gravity stays until it is empty
            Assert.True(Dm.truthy(Waiting.NoSleep(gen.ui_act("gentoggle", Dm.list(), ui, null))));
            Assert.Equal(0f, gen.breaker);
            Assert.Equal(2f, Dm.num(Data(gen, bob)["charging_state"])); // POWER_DOWN
            for (int n = 0; n < 49; n++)
                gen.process(null);
            Assert.Equal(2f, gen.charge_count);
            Assert.Equal(1f, Dm.num(SSmapping.gravity_by_z_level[1]));
            gen.process(null);
            Assert.Equal(0f, gen.charge_count);
            Assert.Equal(1f, Dm.num(SSmapping.gravity_by_z_level[1]));
            gen.process(null); // the empty generator shuts down: no gravity on the level
            Assert.Equal(0f, Dm.num(SSmapping.gravity_by_z_level[1]));
            gen.process(null); // disable's update_use_power runs power_change, which sets POWER_DOWN again; the next process idles it
            data = Data(gen, bob);
            Assert.Equal((0f, 0f, 0f, 0f), (Dm.num(data["breaker"]), Dm.num(data["charge_count"]), Dm.num(data["charging_state"]), Dm.num(data["on"])));

            // and back on: it charges (POWER_UP) fifty processes, then enables
            Assert.True(Dm.truthy(Waiting.NoSleep(gen.ui_act("gentoggle", Dm.list(), ui, null))));
            Assert.Equal(1f, gen.breaker);
            Assert.Equal(1f, Dm.num(Data(gen, bob)["charging_state"])); // POWER_UP
            for (int n = 0; n < 50; n++)
                gen.process(null);
            Assert.Equal(100f, Dm.num(Data(gen, bob)["charge_count"]));
            Assert.Equal(0f, Dm.num(SSmapping.gravity_by_z_level[1]));
            gen.process(null);
            Assert.Equal(1f, Dm.num(SSmapping.gravity_by_z_level[1]));
            gen.process(null); // as at the shutdown: enable's power_change sets POWER_UP once more, the next process idles it
            Assert.Equal((1f, 0f), (Dm.num(Data(gen, bob)["on"]), Dm.num(Data(gen, bob)["charging_state"])));

            // an act the generator does not know is not handled
            Assert.False(Dm.truthy(Waiting.NoSleep(gen.ui_act("nonsense", Dm.list(), ui, null))));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABrokenGeneratorIsNotOperationalOnThePanel()
    {
        var r = new AudioRoom(11, 11, seed: 7502);
        var bob = r.Join(5, 4, "Bob");
        r.Step(20);
        r.In(() =>
        {
            var gen = Dm.@new<obj_machinery_gravity_generator_main>(locate(5, 5, 1));
            Assert.Equal(1f, Dm.num(Data(gen, bob)["operational"]));
            Waiting.NoSleep(gen.set_broken());
            DmList data = Data(gen, bob);
            Assert.Equal((0f, 0f, 0f), (Dm.num(data["operational"]), Dm.num(data["breaker"]), Dm.num(data["charge_count"])));
        });
        r.AssertNoRuntimes();
    }
}
