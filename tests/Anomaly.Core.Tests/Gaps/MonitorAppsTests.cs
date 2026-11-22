using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps S4b (D327): the NtOS monitors: AmpCheck (powermonitor.dm), SignalCommander (signalcommander.dm), BotKeeper (robocontrol.dm),
/// SiliConnect (borg_monitor.dm), WireCarp (wirecarp.dm) and the NTNRC chat client (chatroom/*.dm).
/// </summary>
public class MonitorAppsTests
{
    private static object Act(AudioRoom r, mob user, datum_computer_file_program program, string action, DmList @params = null, datum_tgui ui = null) => r.In(() =>
    {
        World w = World.Current;
        object caller = w.usr;
        w.usr = user;
        try
        {
            return Waiting.NoSleep(program.ui_act(action, @params ?? Dm.list(), ui));
        }
        finally
        {
            w.usr = caller;
        }
    });

    private static T App<T>(AudioRoom r, obj_item_modular_computer cpu) where T : datum_computer_file_program =>
        r.In(() => Dm.each<T>(cpu.stored_files).Single());

    [Fact]
    public void Ampcheck_finds_the_cable_under_its_console_and_reports_the_network()
    {
        var r = new AudioRoom(9, 7, seed: 4201);
        mob_living_carbon_human alice = r.Join(3, 3, "alice");
        var cpu = r.In(() =>
        {
            Dm.@new<obj_structure_cable>(locate(4, 3, 1));
            return Dm.@new<obj_machinery_modular_computer_preset_engineering>(locate(4, 3, 1)).cpu;
        });
        var amp = App<datum_computer_file_program_power_monitor>(r, cpu);
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(amp.on_start(alice))));
            Assert.NotNull(amp.attached_wire_ref?.resolve());
            Assert.NotNull(amp.history["supply"]);
            DmList data = Waiting.NoSleep(amp.ui_data(alice)) as DmList;
            Assert.Equal(60f, Dm.num(data["stored"]));
            Assert.Equal(5f, Dm.num(data["interval"]));
            Assert.Same(amp.history, data["history"]);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Signalcommander_tunes_and_sends_a_logged_signal_with_its_cooldown()
    {
        var r = new AudioRoom(9, 7, seed: 4202);
        mob_living_carbon_human alice = r.Join(3, 3, "alice");
        var pda = r.In(() => Dm.@new<obj_item_modular_computer_pda_crew_heads_rd>(locate(3, 3, 1)));
        var signaler = App<datum_computer_file_program_signal_commander>(r, pda); // the RD's PDA comes with it
        r.In(() => Waiting.NoSleep(signaler.on_start(alice)));
        Act(r, alice, signaler, "freq", Dm.list(Dm.kv("freq", 144.9f)));
        Act(r, alice, signaler, "code", Dm.list(Dm.kv("code", "12.7")));
        r.In(() =>
        {
            Assert.Equal(1449f, signaler.signal_frequency);
            Assert.Equal(12f, signaler.signal_code); // DM's round(A) with one argument is floor
            Assert.NotNull(signaler.radio_connection);
        });
        int before = r.In(() => (int)Dm.num(length(GLOB.investigate_signaler)));
        Act(r, alice, signaler, "signal");
        Act(r, alice, signaler, "signal"); // still cooling down: not sent
        r.In(() =>
        {
            Assert.Equal(before + 1, (int)Dm.num(length(GLOB.investigate_signaler)));
            Assert.Contains("144.9/12", Dm.str(GLOB.investigate_signaler[GLOB.investigate_signaler.len]));
        });
        Act(r, alice, signaler, "reset", Dm.list(Dm.kv("reset", "freq")));
        r.In(() => Assert.Equal(FREQ_SIGNALER, signaler.signal_frequency));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Botkeeper_lists_a_bot_it_can_reach_and_stops_its_patrol()
    {
        var r = new AudioRoom(11, 9, seed: 4203);
        mob_living_carbon_human alice = r.Join(3, 3, "alice");
        var cpu = r.In(() =>
        {
            var console = Dm.@new<obj_machinery_modular_computer_preset_research>(locate(4, 3, 1));
            Waiting.NoSleep(console.cpu.insert_id(Dm.@new<obj_item_card_id_advanced_gold_captains_spare>(locate(3, 3, 1)), alice));
            return console.cpu;
        });
        var bot = r.In(() => Dm.@new<mob_living_basic_bot_cleanbot>(locate(8, 6, 1)));
        r.Step(2);
        var keeper = App<datum_computer_file_program_robocontrol>(r, cpu);
        string botRef = r.In(() =>
        {
            DmList data = Waiting.NoSleep(keeper.ui_data(alice)) as DmList;
            Assert.Equal(1f, Dm.num(data["botcount"]));
            DmList entry = Dm.each<DmList>(data["bots"] as DmList).Single();
            Assert.Equal(Dm.str(bot.name), Dm.str(entry["name"]));
            return Dm.str(entry["bot_ref"]);
        });
        datum_tgui ui = r.In(() => Dm.@new<datum_tgui>(alice, cpu, "NtosRoboControl"));
        Act(r, alice, keeper, "patrolon", Dm.list(Dm.kv("robot", botRef)), ui);
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(bot.bot_mode_flags, BOT_MODE_AUTOPATROL))));
        Act(r, alice, keeper, "patroloff", Dm.list(Dm.kv("robot", botRef)), ui);
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(bot.bot_mode_flags, BOT_MODE_AUTOPATROL))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Siliconnect_lists_a_cyborg_messages_it_and_downloads_its_log_by_tapping()
    {
        var r = new AudioRoom(9, 7, seed: 4204);
        mob_living_carbon_human alice = r.Join(3, 3, "alice");
        var borg = r.In(() => Dm.@new<mob_living_silicon_robot>(locate(4, 3, 1)));
        var pda = r.In(() => Dm.@new<obj_item_modular_computer_pda_crew_heads_rd>(locate(3, 3, 1)));
        var monitor = App<datum_computer_file_program_borg_monitor>(r, pda); // the RD's PDA comes with it
        r.In(() => Waiting.NoSleep(pda.insert_id(Dm.@new<obj_item_card_id_advanced_gold_captains_spare>(locate(3, 3, 1)), alice)));
        r.In(() =>
        {
            DmList data = Waiting.NoSleep(monitor.ui_data(alice)) as DmList;
            Assert.Equal(1f, Dm.num(data["card"]));
            DmList cyborg = Dm.each<DmList>(data["cyborgs"] as DmList).Single();
            Assert.Equal(Dm.str(borg.name), Dm.str(cyborg["name"]));
            Assert.Equal(Dm.str(borg.model.name) + " Model", Dm.str(cyborg["module"])); // a new cyborg has its default model
        });
        Assert.Equal(1f, Dm.num(r.In(() => Waiting.NoSleep(monitor.send_message("report in", borg, alice)))));
        Assert.Contains(r.In(() => Dm.each<object>(borg.modularInterface.borglog).Select(Dm.str).ToList()), l => l.Contains("report in", StringComparison.Ordinal));

        // tapping the cyborg starts the download; four ticks later the log is in
        Assert.Equal(1f, Dm.num(r.In(() => Waiting.NoSleep(monitor.tap(borg, alice)))));
        r.In(() =>
        {
            for (int i = 0; i < 5; i++)
                monitor.process_tick(2);
            Assert.Null(monitor.DL_source);
            Assert.StartsWith("System log of unit", Dm.str(monitor.loglist[1]));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Wirecarp_toggles_the_ids_purges_the_logs_and_allows_a_pdas_mass_messages()
    {
        var r = new AudioRoom(9, 7, seed: 4205);
        mob_living_carbon_human alice = r.Join(3, 3, "alice");
        var cpu = r.In(() => Dm.@new<obj_machinery_modular_computer_preset_research>(locate(4, 3, 1)).cpu);
        var carp = App<datum_computer_file_program_ntnetmonitor>(r, cpu);
        var pda = r.In(() =>
        {
            var p = Dm.@new<obj_item_modular_computer_pda_crew_assistant>(locate(3, 3, 1));
            p.imprint_id("Bob Roe", "Assistant");
            return p;
        });
        var messenger = App<datum_computer_file_program_messenger>(r, pda);
        float enabled = r.In(() => SSmodular_computers.intrusion_detection_enabled);
        Act(r, alice, carp, "toggleIDS");
        r.In(() => Assert.NotEqual(enabled, SSmodular_computers.intrusion_detection_enabled));
        r.In(() => SSmodular_computers.add_log("a test line"));
        Act(r, alice, carp, "purgelogs");
        r.In(() => Assert.EndsWith("-!- LOGS DELETED BY SYSTEM OPERATOR -!-", Dm.str(Dm.each<object>(SSmodular_computers.modpc_logs).Single()))); // purge_logs adds its notice
        Act(r, alice, carp, "toggle_mass_pda", Dm.list(Dm.kv("ref", r.In(() => REF(messenger)))));
        r.In(() =>
        {
            Assert.Equal(1f, messenger.spam_mode);
            DmList data = Waiting.NoSleep(carp.ui_data(alice)) as DmList;
            Assert.Contains(Dm.each<DmList>(data["tablets"] as DmList), t => Dm.eq(t["name"], "Bob Roe") && Dm.num(t["enabled_spam"]) == 1); // saved_identification: the name alone
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Two_chat_clients_meet_in_a_channel_talk_ping_and_the_operator_mutes()
    {
        var r = new AudioRoom(11, 7, seed: 4206);
        mob_living_carbon_human alice = r.Join(3, 3, "alice");
        mob_living_carbon_human bob = r.Join(7, 3, "bob");
        var aliceCpu = r.In(() => Dm.@new<obj_machinery_modular_computer_preset_civilian>(locate(4, 3, 1)).cpu);
        var bobCpu = r.In(() => Dm.@new<obj_machinery_modular_computer_preset_civilian>(locate(8, 3, 1)).cpu);
        var aliceChat = App<datum_computer_file_program_chatclient>(r, aliceCpu);
        var bobChat = App<datum_computer_file_program_chatclient>(r, bobCpu);
        r.In(() => Assert.StartsWith("DefaultUser", Dm.str(aliceChat.username)));

        Act(r, alice, aliceChat, "PRG_changename", Dm.list(Dm.kv("new_name", "Alice D")));
        r.In(() => Assert.Equal("Alice_D", Dm.str(aliceChat.username)));
        Act(r, alice, aliceChat, "PRG_newchannel", Dm.list(Dm.kv("new_channel_name", "ops")));
        datum_ntnet_conversation channel = r.In(() => SSmodular_computers.get_chat_channel_by_id(aliceChat.active_channel));
        r.In(() => Assert.Same(aliceChat, channel.channel_operator));
        Act(r, bob, bobChat, "PRG_joinchannel", Dm.list(Dm.kv("id", r.In(() => Dm.text("[]", channel.id)))));
        Act(r, bob, bobChat, "PRG_speak", Dm.list(Dm.kv("message", "hello there")));
        r.In(() =>
        {
            List<string> lines = Dm.each<object>(channel.messages).Select(k => Dm.str(channel.messages[k])).ToList();
            Assert.Contains(lines, l => l.EndsWith(Dm.str(bobChat.username) + ": hello there", StringComparison.Ordinal));
            Assert.Contains(lines, l => l.EndsWith(Dm.str(bobChat.username) + " has joined the channel.", StringComparison.Ordinal));
            DmList data = Waiting.NoSleep(aliceChat.ui_data(alice)) as DmList;
            Assert.Equal(1f, Dm.num(data["is_operator"]));
            Assert.Equal(2, (data["clients"] as DmList).Count);
        });

        // the operator mutes Bob: his lines are dropped; a ping comes through as a status line
        Act(r, alice, aliceChat, "PRG_mute_user", Dm.list(Dm.kv("ref", r.In(() => REF(bobChat)))));
        int count = r.In(() => channel.messages.Count);
        Act(r, bob, bobChat, "PRG_speak", Dm.list(Dm.kv("message", "can you hear me")));
        r.In(() => Assert.Equal(count, channel.messages.Count));
        Act(r, alice, aliceChat, "PRG_ping_user", Dm.list(Dm.kv("ref", r.In(() => REF(bobChat)))));
        r.In(() => Assert.Contains(Dm.each<object>(channel.messages).Select(k => Dm.str(channel.messages[k])), l => l.Contains("Alice_D pinged", StringComparison.Ordinal)));

        // a password keeps a newcomer out until he says it
        Act(r, alice, aliceChat, "PRG_setpassword", Dm.list(Dm.kv("new_password", "swordfish")));
        Act(r, bob, bobChat, "PRG_leavechannel");
        Act(r, bob, bobChat, "PRG_joinchannel", Dm.list(Dm.kv("id", r.In(() => Dm.text("[]", channel.id)))));
        r.In(() => Assert.DoesNotContain(bobChat, Dm.each<object>(channel.active_clients)));
        Act(r, bob, bobChat, "PRG_speak", Dm.list(Dm.kv("message", "swordfish")));
        r.In(() => Assert.Contains(bobChat, Dm.each<object>(channel.active_clients)));
        r.AssertNoRuntimes();
    }
}
