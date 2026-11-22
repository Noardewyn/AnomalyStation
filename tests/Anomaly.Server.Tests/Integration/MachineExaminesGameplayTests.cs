using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Package X1 (D291), the object examines of the machines, through the real server on MetaStation: Bob shift-clicks the mapped fire alarm,
/// air alarm, firelock, airlock, vent and light of the first room he finds them in, and the machines he sets down next to himself (a space
/// heater, a cell charger with a cell in it, a research scanner's parts list): the chat box shows tg's lines of each (the status of the
/// alarm's area, the heater's hatch and status display, the charger's cell), and the base machine's damage line once a machine is hurt.
/// </summary>
public class MachineExaminesGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static string Lines(IEnumerable<ServerText> lines) => string.Join(" | ", lines.Select(t => t.Html));

    [MetaServerFact]
    public void Bob_examines_Metas_machines_and_the_chat_shows_tgs_lines_of_each()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 88, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var him = (mob_living_carbon_human)w.MobOf(bob);
        var chat = new List<ServerText>();
        bob.Client.ServerTextReceived += chat.Add;
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // Bob stands on the machine's tile (or where it is mapped on a wall, next to it) and shift-clicks it.
        string Examine(atom machine, int wait = Second)
        {
            using (w.World.Enter())
            {
                turf spot = machine.loc as turf_open ?? (turf)Dm.each_cast<turf_open>(Builtins.oview(1, machine)).First();
                Waiting.NoSleep(him.forceMove(spot));
            }
            w.Run(Second); // tg's client/Click drops a click within a decisecond of the last
            chat.Clear();
            Assert.True(bob.Session.Click(machine.dm_id, ClickButton.Left, ClickModifiers.Shift));
            w.Run(wait);
            ServerText box = chat.LastOrDefault(t => t.Html.Contains("boxed_message", StringComparison.Ordinal));
            Assert.True(box.Html != null, "no examine box: " + Lines(chat) + "\n" + w.Err);
            output.WriteLine(machine.GetType().Name + ": " + box.Html);
            return box.Html;
        }

        T Mapped<T>(Func<T, bool> pick = null) where T : obj_machinery => In(w, () =>
            Dm.each_cast<T>(SSmachines.get_machines_by_type_and_subtypes(typeof(T))).OrderBy(m => m.dm_id).First(m => m.loc is turf_open && (pick == null || pick(m))));

        // ---- 1. The mapped alarms, doors, vents and lights.
        string box = Examine(Mapped<obj_machinery_firealarm>());
        Assert.Contains("The local area thermal detection light is", box, StringComparison.Ordinal);
        Assert.Contains("<b>Left-Click</b> to activate all firelocks in this area.", box, StringComparison.Ordinal);

        box = Examine(Mapped<obj_machinery_airalarm>(a => a.buildstage == AIR_ALARM_BUILD_COMPLETE));
        Assert.Contains("Right-click to", box, StringComparison.Ordinal);
        Assert.Contains("the interface.", box, StringComparison.Ordinal);

        box = Examine(Mapped<obj_machinery_door_firedoor>());
        Assert.True(box.Contains("It is open, but could be <b>pried</b> closed.", StringComparison.Ordinal) || box.Contains("It is closed, but could be <b>pried</b> open.", StringComparison.Ordinal), box);

        box = Examine(Mapped<obj_machinery_door_airlock>(a => a.GetType() == typeof(obj_machinery_door_airlock) || a is obj_machinery_door_airlock_external));
        Assert.True(box.Contains("This airlock cycles", StringComparison.Ordinal) || box.Contains("This airlock does not cycle.", StringComparison.Ordinal), box);
        Assert.Contains("Its maintenance panel is", box, StringComparison.Ordinal); // the door's own line

        box = Examine(Mapped<obj_machinery_atmospherics_components_unary_vent_pump>());
        Assert.Contains(" is on layer ", box, StringComparison.Ordinal);
        Assert.Contains("You can link it with an air sensor using a multitool.", box, StringComparison.Ordinal);

        box = Examine(Mapped<obj_machinery_light>(l => Dm.eq(l.status, LIGHT_OK))); // a working light: a mapped broken one says its bulb is smashed
        Assert.Contains("It is turned", box, StringComparison.Ordinal);

        // ---- 2. A space heater and a cell charger set down next to Bob; the base machine's damage line.
        (obj_machinery_space_heater heater, obj_machinery_cell_charger charger) = In(w, () =>
        {
            turf near = Builtins.get_step(him, Dm.EAST) as turf;
            return (Dm.@new<obj_machinery_space_heater>(near), Dm.@new<obj_machinery_cell_charger>(near));
        });
        box = Examine(heater);
        Assert.Contains("is off, and the hatch is closed.", box, StringComparison.Ordinal);
        Assert.Contains("The charge meter reads 100%.", box, StringComparison.Ordinal);
        Assert.Contains("The status display reads:", box, StringComparison.Ordinal);
        Assert.Contains("Right-click</b> to toggle on.", box, StringComparison.Ordinal);
        Assert.DoesNotContain("damaged", box, StringComparison.Ordinal);

        using (w.World.Enter())
            heater.atom_integrity = Dm.num(heater.max_integrity) * 0.6f;
        Assert.Contains("It looks slightly damaged.", Examine(heater), StringComparison.Ordinal);

        Assert.Contains("There's no cell in the charger.", Examine(charger), StringComparison.Ordinal);
        using (w.World.Enter())
        {
            charger.charging = Dm.@new<obj_item_stock_parts_power_store_cell_high>(charger);
            charger.charging.charge = charger.charging.maxcharge / 2;
        }
        box = Examine(charger);
        Assert.Contains("Current charge: 50%.", box, StringComparison.Ordinal);
        Assert.Contains("Charging power: <b>2.5 kW</b>", box, StringComparison.Ordinal);

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
