using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using System.Text.Json;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// MetaStation's machines with windows: the PDA & ID painter paints a PDA with a department's look and swaps and ejects what it holds
/// (PDApainter.dm), a department's painter offers only its department's PDAs; the pipe dispensers make pipes, meters, disposal pipes
/// on a cooldown and take pipes back (pipe_dispenser.dm); a laptop starts open, closes with an alt-click and signs its messages
/// (laptop.dm); the station's gateway registers its home destination, its console links up and lists no away destination, and a
/// multitool finds it calibrated (gateway.dm).
/// </summary>
public class PainterDispenserGatewayTests
{
    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(16, 11, seed: seed);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        // the toy room has no APC: free power
        r.In(() => (get_area(locate(5, 5, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        return (r, bob);
    }

    private static string Open(AudioRoom r, mob who, datum machine, string expected, out JsonElement payload)
    {
        Take(who);
        r.Command(() => Waiting.Detach(((obj_machinery)machine).ui_interact(who)));
        r.Step(4);
        payload = LastFullUpdate(Take(who), out string window);
        Assert.Equal(expected, InterfaceOf(payload));
        return window;
    }

    [Fact]
    public void ThePainterPaintsAPdaWithTheChosenLookAndEjectsItIntoTheHand()
    {
        var (r, bob) = Room(15201);
        var painter = Place<obj_machinery_pdapainter>(r, 6, 5);
        var pda = r.In(() => Dm.@new<obj_item_modular_computer_pda>(bob.loc));
        r.In(() =>
        {
            Waiting.NoSleep(bob.put_in_active_hand(pda));
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(painter.item_interaction(bob, pda)));
            Assert.Same(pda, painter.stored_pda);
            Assert.Same(painter, pda.loc);
        });
        string window = Open(r, bob, painter, "PaintingMachine", out JsonElement payload);
        Assert.True(payload.GetProperty("data").GetProperty("hasPDA").GetDouble() == 1);
        string security = r.In(() => Dm.str(painter.pda_types[typeof(obj_item_modular_computer_pda_crew_security)]));
        Assert.False(string.IsNullOrEmpty(security));

        Act(r, bob, window, "trim_pda", ("selection", security));
        r.In(() =>
        {
            Assert.Equal(Dm.str(Dm.initial(typeof(obj_item_modular_computer_pda_crew_security), (obj_item_modular_computer_pda p) => p.greyscale_colors)), Dm.str(pda.greyscale_colors));
            Assert.Equal(Dm.str(Dm.initial(typeof(obj_item_modular_computer_pda_crew_security), (obj_item_modular_computer_pda p) => p.desc)), Dm.str(pda.desc));
        });

        Act(r, bob, window, "eject_pda");
        r.In(() =>
        {
            Assert.Null(painter.stored_pda);
            Assert.Contains(pda, Dm.each(bob.held_items).ToList());
        });
    }

    [Fact]
    public void ADepartmentsPainterOffersOnlyItsDepartmentsPdas()
    {
        var (r, _) = Room(15202);
        var station = Place<obj_machinery_pdapainter>(r, 6, 5);
        var security = Place<obj_machinery_pdapainter_security>(r, 7, 5);
        r.In(() =>
        {
            Assert.True(security.pda_types.len > 0);
            Assert.True(security.pda_types.len < station.pda_types.len);
            Assert.True(Dm.@in(typeof(obj_item_modular_computer_pda_crew_security), security.pda_types));
            Assert.False(Dm.@in(typeof(obj_item_modular_computer_pda_crew_medical), security.pda_types));
            Assert.True(security.card_trims.len > 0);
        });
    }

    [Fact]
    public void ThePipeDispenserMakesAPipeOnACooldownAndTakesPipesBack()
    {
        var (r, bob) = Room(15203);
        var dispenser = Place<obj_machinery_pipedispenser>(r, 6, 5);
        string window = Open(r, bob, dispenser, "PipeDispenser", out JsonElement payload);
        JsonElement categories = payload.GetProperty("data").GetProperty("categories");
        Assert.True(categories.GetArrayLength() > 0);
        string first = categories[0].GetProperty("cat_name").GetString();

        Act(r, bob, window, "pipe_type", ("pipe_type", 1), ("pipe_dir", (float)NORTH), ("category", first));
        Act(r, bob, window, "pipe_type", ("pipe_type", 1), ("pipe_dir", (float)NORTH), ("category", first));
        var pipes = r.In(() => Dm.each<obj_item_pipe>(locate(6, 5, 1)).ToList());
        Assert.Single(pipes);
        r.In(() => Assert.Equal(3f, pipes[0].piping_layer));

        r.Step(2 * Second);
        Act(r, bob, window, "pipe_type", ("pipe_type", 1), ("pipe_dir", (float)NORTH), ("category", first));
        Assert.Equal(2, r.In(() => Dm.each<obj_item_pipe>(locate(6, 5, 1)).Count()));

        r.In(() =>
        {
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(dispenser.item_interaction(bob, pipes[0])));
            Assert.True(QDELETED(pipes[0]));
        });
    }

    [Fact]
    public void TheDisposalDispenserMakesDisposalPipesAndEatsThemDraggedIn()
    {
        var (r, bob) = Room(15204);
        var dispenser = Place<obj_machinery_pipedispenser_disposal>(r, 6, 5);
        string window = Open(r, bob, dispenser, "PipeDispenser", out JsonElement payload);
        Assert.Equal(1, payload.GetProperty("data").GetProperty("category").GetDouble());
        string first = payload.GetProperty("data").GetProperty("categories")[0].GetProperty("cat_name").GetString();

        Act(r, bob, window, "pipe_type", ("pipe_type", 1), ("pipe_dir", (float)NORTH), ("category", first));
        var made = r.In(() => Dm.locate<obj_structure_disposalconstruct>(locate(6, 5, 1)));
        Assert.NotNull(made);
        r.In(() =>
        {
            Waiting.NoSleep(dispenser.mouse_drop_receive(made, bob, null));
            Assert.True(QDELETED(made));
        });
    }

    [Fact]
    public void ALaptopStartsOpenAndAnAltClickClosesIt()
    {
        var (r, bob) = Room(15205);
        var laptop = r.In(() => Dm.@new<obj_item_modular_computer_laptop_preset_civilian>(locate(6, 5, 1)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(laptop.screen_on));
            Assert.Equal((float)WEIGHT_CLASS_BULKY, Dm.num(laptop.w_class));
            Assert.Equal(1f, laptop.slowdown);
            Assert.Equal("Sent from my UNIX Laptop", Dm.str(laptop.get_messenger_ending()));
        });
        r.Command(() => Waiting.Detach(laptop.click_alt(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(laptop.screen_on));
            Assert.Equal("laptop-closed", Dm.str(laptop.icon_state));
            Assert.Equal((float)WEIGHT_CLASS_NORMAL, Dm.num(laptop.w_class));
            Assert.Equal(0f, laptop.slowdown);
        });
        // a closed laptop opens with a use in hand
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(laptop)));
        r.Command(() => Waiting.Detach(laptop.attack_self(bob)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(laptop.screen_on)));
    }

    [Fact]
    public void TheStationsGatewayRegistersItsHomeAndItsConsoleLinksUpWithNoAwayDestination()
    {
        var (r, bob) = Room(15206);
        var gateway = Place<obj_machinery_gateway_centerstation>(r, 8, 7);
        var console = Place<obj_machinery_computer_gateway_control>(r, 6, 5);
        r.In(() =>
        {
            Assert.Same(gateway, GLOB.the_gateway);
            Assert.IsType<datum_gateway_destination_gateway_home>(gateway.destination);
            Assert.Equal("Home Gateway", gateway.destination.name);
            Assert.True(Dm.@in(gateway.destination, GLOB.gateway_destinations));
            Assert.Same(gateway, console.G);
            Assert.False(Dm.truthy(gateway.valid_destination(gateway.destination)));
        });
        string window = Open(r, bob, console, "Gateway", out JsonElement payload);
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(0, data.GetProperty("destinations").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("current_target").ValueKind);

        Feed feed = Listen(r);
        r.In(() =>
        {
            var multitool = Dm.@new<obj_item_multitool>(bob.loc);
            Waiting.NoSleep(gateway.multitool_act(bob, multitool));
            Assert.True(feed.Any("already calibrated"));
            qdel(gateway);
            Assert.Null(GLOB.the_gateway);
            Assert.Equal(0, GLOB.gateway_destinations.len);
        });
    }
}
