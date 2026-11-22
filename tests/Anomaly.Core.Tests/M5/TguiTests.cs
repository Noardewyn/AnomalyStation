using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// M5 slice 3 (D148, DV44): tg's tgui on the server. A click on a canister opens its "Canister" window through tg's interact,
/// ui_interact, /datum/tgui and SStgui (the window's messages go to the client's output, as JSON); the client's "act/..." messages
/// reach the canister's ui_act (valve, release pressure, the number input), SStgui's fire sends ui_data again, and ui_status by tg's
/// physical_state disables and then closes the window as the player walks away. tgui_input_number and tgui_alert wait for the
/// client's submit, choice, cancel or close.
/// </summary>
public class TguiTests
{
    private const int Second = 20;

    private sealed class Pending
    {
        public bool Done;
        public object Answer = "unanswered";
    }

    private static (AudioRoom r, mob_living_carbon_human bob, obj_machinery_portable_atmospherics_canister_air can) Room(ulong seed)
    {
        var r = new AudioRoom(14, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        var can = r.In(() => Dm.@new<obj_machinery_portable_atmospherics_canister_air>(locate(5, 4, 1)));
        r.Step(Second);
        return (r, bob, can);
    }

    private static void Click(AudioRoom r, mob who, atom target)
    {
        r.Command(() => ClientSeam.Click(who.client, target, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(2);
    }

    private static List<TguiOutput> Take(mob who)
    {
        var taken = who.client.dm_tgui_output.ToList();
        who.client.dm_tgui_output.Clear();
        return taken;
    }

    // The last "update" message of a window: its payload.
    private static JsonElement LastUpdate(List<TguiOutput> outputs, out string windowId, bool withStaticData = false)
    {
        windowId = null;
        JsonElement found = default;
        foreach (TguiOutput o in outputs)
        {
            if (o.Kind != TguiOutputKind.Message)
                continue;
            using JsonDocument doc = JsonDocument.Parse(o.Json);
            if (doc.RootElement.GetProperty("type").GetString() != "update")
                continue;
            // a full update (tgui's send_full_update, with the static data) or also SStgui's process updates
            if (withStaticData && !doc.RootElement.GetProperty("payload").TryGetProperty("static_data", out _))
                continue;
            found = doc.RootElement.GetProperty("payload").Clone();
            windowId = o.WindowId;
        }
        Assert.True(found.ValueKind == JsonValueKind.Object, "no update message: " + string.Join(" | ", outputs.Select(o => o.Kind + " " + o.Json)));
        return found;
    }

    private static void Send(AudioRoom r, mob who, string windowId, string type, DmList payload = null, int steps = 2)
    {
        r.Command(() => ClientSeam.TguiMessage(who.client, windowId, type, payload));
        r.Step(steps);
    }

    private static void MoveTo(AudioRoom r, mob who, int x, int y) => r.In(() => Waiting.NoSleep(((atom_movable)who).forceMove(locate(x, y, 1))));

    [Fact]
    public void AClickOnACanisterOpensItsWindowWithTgsData()
    {
        var (r, bob, can) = Room(8301);
        Take(bob);
        Click(r, bob, can);
        List<TguiOutput> outputs = Take(bob);
        Assert.Equal(TguiOutputKind.Initialize, outputs[0].Kind);
        Assert.Equal("tgui-window-1", outputs[0].WindowId);
        JsonElement payload = LastUpdate(outputs, out string window, withStaticData: true); // SStgui's process update may follow it
        Assert.Equal("tgui-window-1", window);
        JsonElement config = payload.GetProperty("config");
        Assert.Equal("Canister", config.GetProperty("interface").GetProperty("name").GetString());
        Assert.Equal("Air canister", config.GetProperty("title").GetString());
        Assert.Equal(UI_INTERACTIVE, config.GetProperty("status").GetInt32());
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(0, data.GetProperty("valveOpen").GetInt32());
        Assert.Equal(101, data.GetProperty("releasePressure").GetInt32());
        Assert.InRange(data.GetProperty("tankPressure").GetInt32(), 4000, 5000);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("holdingTank").ValueKind);
        JsonElement statics = payload.GetProperty("static_data");
        Assert.Equal(2533, statics.GetProperty("maxReleasePressure").GetInt32());
        Assert.Equal(10, statics.GetProperty("minReleasePressure").GetInt32());
        Assert.Equal(1, r.In(() => SStgui.all_uis.len));
        Assert.Equal(1, r.In(() => Dm.length(can.open_uis)));
        Assert.Equal(1, r.In(() => Dm.length(bob.tgui_open_uis)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWindowsActionsReachTheCanistersUiAct()
    {
        var (r, bob, can) = Room(8302);
        Click(r, bob, can);
        LastUpdate(Take(bob), out string window);

        Send(r, bob, window, "act/valve");
        Assert.True(r.In(() => Dm.truthy(can.valve_open)), "the valve button opens the valve");
        Send(r, bob, window, "act/pressure", r.In(() => Dm.list(Dm.kv("pressure", 500))));
        Assert.Equal(500f, r.In(() => can.release_pressure));
        Send(r, bob, window, "act/pressure", r.In(() => Dm.list(Dm.kv("pressure", "max"))));
        Assert.Equal(CAN_MAX_RELEASE_PRESSURE, r.In(() => can.release_pressure));
        Send(r, bob, window, "act/pressure", r.In(() => Dm.list(Dm.kv("pressure", "reset"))));
        Assert.Equal(ONE_ATMOSPHERE, r.In(() => can.release_pressure));
        Send(r, bob, window, "act/pressure", r.In(() => Dm.list(Dm.kv("pressure", 99999))));
        Assert.Equal(CAN_MAX_RELEASE_PRESSURE, r.In(() => can.release_pressure)); // clamped as tg does

        // ui_act's TRUE runs SStgui.update_uis: the window gets the new data at once.
        JsonElement data = LastUpdate(Take(bob), out _).GetProperty("data");
        Assert.Equal(1, data.GetProperty("valveOpen").GetInt32());
        Assert.Equal(2533, data.GetProperty("releasePressure").GetInt32());

        // The close button: "close" closes the ui (not suspended), the canister's open_uis empties.
        Send(r, bob, window, "close");
        Assert.Equal(0, r.In(() => SStgui.all_uis.len));
        Assert.Null(r.In(() => can.open_uis));
        Assert.Contains(Take(bob), o => o.Kind == TguiOutputKind.Close && o.WindowId == window);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void PhysicalStateDisablesThenClosesTheWindowAsThePlayerWalksAway()
    {
        var (r, bob, can) = Room(8303);
        Click(r, bob, can);
        LastUpdate(Take(bob), out string window);
        datum_tgui ui = r.In(() => (datum_tgui)SStgui.all_uis[1f]);

        // dist 2: shared_living_ui_distance says UI_UPDATE; an act is refused by /datum/ui_act.
        MoveTo(r, bob, 3, 4);
        r.Step(Second);
        Assert.Equal(UI_UPDATE, r.In(() => ui.status));
        Send(r, bob, window, "act/valve");
        Assert.False(r.In(() => Dm.truthy(can.valve_open)), "a window that only updates takes no action");
        Assert.Equal(UI_UPDATE, LastUpdate(Take(bob), out _).GetProperty("config").GetProperty("status").GetInt32());

        // dist 3: UI_DISABLED, the window stays open but stops auto-updating through ui_interact.
        MoveTo(r, bob, 2, 4);
        r.Step(Second);
        Assert.Equal(UI_DISABLED, r.In(() => ui.status));
        Assert.Equal(1, r.In(() => SStgui.all_uis.len));

        // dist 6: UI_CLOSE closes it.
        MoveTo(r, bob, 11, 4);
        r.Step(Second);
        Assert.Equal(0, r.In(() => SStgui.all_uis.len));
        Assert.Contains(Take(bob), o => o.Kind == TguiOutputKind.Close || (o.Kind == TguiOutputKind.Message && o.Json.Contains("\"suspend\"")));
        Assert.Null(r.In(() => can.open_uis));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStatesFollowTgsRules()
    {
        var (r, bob, can) = Room(8304);
        Assert.Equal((float)UI_INTERACTIVE, r.In(() => Dm.num(Waiting.NoSleep(GLOB.physical_state.can_use_topic(can, bob)))));
        Assert.Equal((float)UI_INTERACTIVE, r.In(() => Dm.num(Waiting.NoSleep(GLOB.always_state.can_use_topic(can, bob)))));
        Assert.Equal((float)UI_CLOSE, r.In(() => Dm.num(Waiting.NoSleep(GLOB.never_state.can_use_topic(can, bob)))));
        Assert.Equal((float)UI_CLOSE, r.In(() => Dm.num(Waiting.NoSleep(GLOB.hands_state.can_use_topic(can, bob)))));
        Assert.Equal((float)UI_INTERACTIVE, r.In(() => Dm.num(Waiting.NoSleep(GLOB.default_state.can_use_topic(can, bob)))));
        Assert.Equal((float)UI_CLOSE, r.In(() => Dm.num(Waiting.NoSleep(GLOB.self_state.can_use_topic(can, bob)))));
        Assert.Equal((float)UI_INTERACTIVE, r.In(() => Dm.num(Waiting.NoSleep(GLOB.self_state.can_use_topic(bob, bob)))));
        // A player without a client: shared_ui_interaction closes ("mindless").
        mob_living_carbon_human dummy = r.Dummy(6, 4);
        Assert.Equal((float)UI_CLOSE, r.In(() => Dm.num(Waiting.NoSleep(GLOB.physical_state.can_use_topic(can, dummy)))));
        // ui_status: the state's answer, and /atom/ui_status's can_interact cap.
        Assert.Equal((float)UI_INTERACTIVE, r.In(() => Dm.num(Waiting.NoSleep(can.ui_status(bob, GLOB.physical_state)))));
        Assert.Equal((float)UI_CLOSE, r.In(() => Dm.num(Waiting.NoSleep(can.ui_status(bob, null)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheReleasePressureInputIsANumberWindowTheClientAnswers()
    {
        var (r, bob, can) = Room(8305);
        Click(r, bob, can);
        LastUpdate(Take(bob), out string canisterWindow);
        Send(r, bob, canisterWindow, "act/pressure", r.In(() => Dm.list(Dm.kv("pressure", "input"))));

        JsonElement payload = LastUpdate(Take(bob), out string numberWindow);
        Assert.Equal("tgui-window-2", numberWindow);
        Assert.Equal("NumberInputModal", payload.GetProperty("config").GetProperty("interface").GetProperty("name").GetString());
        JsonElement statics = payload.GetProperty("static_data");
        Assert.Equal("New release pressure", statics.GetProperty("message").GetString());
        Assert.Equal("Canister Pressure", statics.GetProperty("title").GetString());
        Assert.Equal(2533.125, statics.GetProperty("max_value").GetDouble(), 3);
        Assert.Equal(0, statics.GetProperty("round_value").GetInt32());
        Assert.Equal(ONE_ATMOSPHERE, r.In(() => can.release_pressure)); // still waiting

        r.Step(5 * Second);
        Assert.Equal(ONE_ATMOSPHERE, r.In(() => can.release_pressure));
        Send(r, bob, numberWindow, "act/submit", r.In(() => Dm.list(Dm.kv("entry", 321.5f))), steps: 6);
        Assert.Equal(321.5f, r.In(() => can.release_pressure));
        Assert.Equal(1, r.In(() => SStgui.all_uis.len)); // the number window closed, the canister's stays
        r.AssertNoRuntimes();
    }

    private static Pending Ask(AudioRoom r, Func<DmTask<object>> call)
    {
        var pending = new Pending();
        r.In(() => spawn(-1, async () =>
        {
            pending.Answer = await call();
            pending.Done = true;
        }));
        return pending;
    }

    [Fact]
    public void TguiInputNumberWaitsForSubmitCancelOrClose()
    {
        var (r, bob, _) = Room(8306);
        Take(bob);
        Pending first = Ask(r, () => tgui_input_number(bob, "How many?", "Split", 5, 50, 1));
        r.Step(4);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal(5, payload.GetProperty("static_data").GetProperty("init_value").GetInt32());
        Assert.Equal(1, payload.GetProperty("static_data").GetProperty("round_value").GetInt32());
        r.Step(10 * Second);
        Assert.False(first.Done, "tg's number input has no time limit of its own");
        Send(r, bob, window, "act/submit", r.In(() => Dm.list(Dm.kv("entry", 7.9f))), steps: 4);
        Assert.True(first.Done);
        Assert.Equal(7f, first.Answer); // round_value rounds down

        Pending second = Ask(r, () => tgui_input_number(bob, "How many?", "Split", 5, 50, 1));
        r.Step(4);
        LastUpdate(Take(bob), out window);
        Send(r, bob, window, "act/cancel", steps: 4);
        Assert.True(second.Done);
        Assert.Null(second.Answer);

        Pending third = Ask(r, () => tgui_input_number(bob, "How many?", "Split", 5, 50, 1));
        r.Step(4);
        LastUpdate(Take(bob), out window);
        Send(r, bob, window, "close", steps: 4);
        Assert.True(third.Done);
        Assert.Null(third.Answer);
        Assert.Equal(0, r.In(() => SStgui.all_uis.len));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TguiAlertReturnsTheChosenButton()
    {
        var (r, bob, _) = Room(8307);
        Take(bob);
        Pending answer = Ask(r, () => tgui_alert(bob, "Respawn?", "Respawn", Dm.list("Yes", "No")));
        r.Step(4);
        JsonElement payload = LastUpdate(Take(bob), out string window, withStaticData: true); // SStgui's process update may come after the full one
        Assert.Equal("AlertModal", payload.GetProperty("config").GetProperty("interface").GetProperty("name").GetString());
        JsonElement buttons = payload.GetProperty("static_data").GetProperty("buttons");
        Assert.Equal(new[] { "Yes", "No" }, buttons.EnumerateArray().Select(b => b.GetString()).ToArray());
        Send(r, bob, window, "act/choose", r.In(() => Dm.list(Dm.kv("choice", "No"))), steps: 4);
        Assert.True(answer.Done);
        Assert.Equal("No", answer.Answer);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRelabelButtonAsksForAGasInAListWindowAndTheCanisterTakesThatCanistersLook()
    {
        // canister.dm:621-636 (the M5 leftovers, 2026-10-16): tg_input_list of GLOB.gas_id_to_canister, then the new type's name,
        // desc, icon_state and greyscale
        var (r, bob, can) = Room(8311);
        Click(r, bob, can);
        LastUpdate(Take(bob), out string canisterWindow);
        Send(r, bob, canisterWindow, "act/relabel");
        JsonElement payload = LastUpdate(Take(bob), out string listWindow);
        Assert.Equal("ListInputWindow", payload.GetProperty("config").GetProperty("interface").GetProperty("name").GetString());
        string[] items = payload.GetProperty("static_data").GetProperty("items").EnumerateArray().Select(b => b.GetString()).ToArray();
        Assert.Equal(22, items.Length);
        Assert.Equal(items.OrderBy(i => i, StringComparer.Ordinal).ToArray(), items); // sort_list
        Assert.Contains("caution", items);
        Send(r, bob, listWindow, "act/submit", r.In(() => Dm.list(Dm.kv("entry", GAS_PLASMA))), steps: 6);
        var plasma = typeof(obj_machinery_portable_atmospherics_canister_plasma);
        Assert.Equal(r.In(() => Dm.initial(plasma, "name")), r.In(() => can.name));
        Assert.Equal(r.In(() => Dm.initial(plasma, "desc")), r.In(() => can.desc));
        Assert.Equal(r.In(() => Dm.initial(plasma, "greyscale_colors")), r.In(() => can.greyscale_colors));
        Assert.Equal(r.In(() => can.icon_state), r.In(() => can.base_icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TguiInputListReturnsTheListedValueOfTheChosenEntry()
    {
        var (r, bob, can) = Room(8310);
        Take(bob);
        // M5x slice 4: the entries are the items' texts, without the illegal characters and with duplicates told apart; the
        // answer is the listed value itself (here the canister for its name)
        Pending answer = Ask(r, () => tgui_input_list(bob, "Pick one", "Picker", Dm.list("Instant", can, "Instant", null, "5")));
        r.Step(4);
        JsonElement payload = LastUpdate(Take(bob), out string window, withStaticData: true);
        Assert.Equal("ListInputWindow", payload.GetProperty("config").GetProperty("interface").GetProperty("name").GetString());
        JsonElement statics = payload.GetProperty("static_data");
        // "[i]" of the canister: "\improper Air canister" at the text's start reads "The Air canister" (BYOND's article)
        Assert.Equal(new[] { "Instant", "The Air canister", "Instant (2)", "5" }, statics.GetProperty("items").EnumerateArray().Select(b => b.GetString()).ToArray());
        Assert.Equal("Instant", statics.GetProperty("init_value").GetString());
        Send(r, bob, window, "act/submit", r.In(() => Dm.list(Dm.kv("entry", "Not listed"))), steps: 4);
        Assert.False(answer.Done, "an entry that is not listed is ignored");
        Send(r, bob, window, "act/submit", r.In(() => Dm.list(Dm.kv("entry", "The Air canister"))), steps: 4);
        Assert.True(answer.Done);
        Assert.Same(can, answer.Answer);

        Pending empty = Ask(r, () => tgui_input_list(bob, "Pick one", "Picker", Dm.list()));
        r.Step(2);
        Assert.True(empty.Done);
        Assert.Null(empty.Answer);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMessageForAWindowTheClientDoesNotHaveForceClosesIt()
    {
        var (r, bob, _) = Room(8308);
        Take(bob);
        Send(r, bob, "tgui-window-7", "act/valve");
        Assert.Contains(Take(bob), o => o.Kind == TguiOutputKind.Close && o.WindowId == "tgui-window-7");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void LoggingOutClosesThePlayersWindows()
    {
        var (r, bob, can) = Room(8309);
        Click(r, bob, can);
        Assert.Equal(1, r.In(() => SStgui.all_uis.len));
        r.Command(() => ClientSeam.Disconnect(bob.client));
        r.Step(2);
        Assert.Equal(0, r.In(() => SStgui.all_uis.len));
        Assert.Null(r.In(() => can.open_uis));
    }
}
