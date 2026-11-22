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
/// Gaps S4c (D327): NtOS's device, game, maintenance and antagonist apps: EmojiPedia, the Physical Scanner, the Botanical Encyclopedia,
/// the Donksoft Micro Arcade, the NtOS Cursor Replacer, the PDA themes, the Spectre-Meter, the DoS Traffic Generator and Revelation.
/// </summary>
public class DeviceAppsTests
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

    private static T Install<T>(AudioRoom r, obj_item_modular_computer computer) where T : datum_computer_file_program => r.In(() =>
    {
        T app = Dm.each<T>(computer.stored_files).FirstOrDefault();
        if (app != null)
            return app;
        app = Dm.@new<T>();
        Assert.True(Dm.truthy(computer.store_file(app)));
        return app;
    });

    [Fact]
    public void Emojipedia_lists_tgs_emoji_and_the_encyclopedia_every_plant()
    {
        var r = new AudioRoom(7, 7, seed: 4301);
        mob_living_carbon_human alice = r.Join(3, 3, "alice");
        var pda = r.In(() => Dm.@new<obj_item_modular_computer_pda_crew_assistant>(locate(3, 3, 1)));
        var emoji = Install<datum_computer_file_program_emojipedia>(r, pda);
        var botany = Install<datum_computer_file_program_botanical_encyclopedia>(r, pda);
        r.In(() =>
        {
            DmList emojiData = Waiting.NoSleep(emoji.ui_static_data(alice)) as DmList;
            Assert.True((emojiData["emoji_list"] as DmList).Count > 50);
            DmList botanyData = Waiting.NoSleep(botany.ui_static_data(alice)) as DmList;
            Assert.True((botanyData["seeds"] as DmList).Count > 50);
            Assert.True((botanyData["trait_db"] as DmList).Count > 10);
            Assert.Equal(20f, Dm.num(botanyData["cycle_seconds"]));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_physical_scanner_scans_a_tapped_carbon()
    {
        var r = new AudioRoom(7, 7, seed: 4302);
        mob_living_carbon_human alice = r.Join(3, 3, "alice");
        mob_living_carbon_human bob = r.Dummy(4, 3);
        var pda = r.In(() => Dm.@new<obj_item_modular_computer_pda_crew_assistant>(locate(3, 3, 1)));
        var scanner = Install<datum_computer_file_program_maintenance_phys_scanner>(r, pda);
        r.In(() => Waiting.NoSleep(scanner.tap(bob, alice)));
        r.In(() => Assert.Contains("Analyzing results for", scanner.last_record ?? "", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_arcade_plays_a_turn_wins_and_prints_a_ticket()
    {
        var r = new AudioRoom(7, 7, seed: 4303);
        mob_living_carbon_human alice = r.Join(3, 3, "alice");
        var laptop = r.In(() => Dm.@new<obj_item_modular_computer_laptop>(locate(3, 3, 1)));
        var arcade = Install<datum_computer_file_program_arcade>(r, laptop);
        datum_tgui ui = r.In(() => Dm.@new<datum_tgui>(alice, laptop, "NtosArcade"));
        void Turn(string action)
        {
            r.In(() =>
            {
                World w = World.Current;
                w.usr = alice;
                Waiting.Detach(arcade.ui_act(action, Dm.list(), ui));
                w.usr = null;
            });
            r.Step(60); // the turn's sleeps: 1 s, then the checks' half and whole seconds
        }

        Turn("Start_Game");
        float bossBefore = r.In(() => arcade.boss_hp);
        Turn("Attack");
        r.In(() => Assert.True(arcade.boss_hp < bossBefore));
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("arcade/hit", StringComparison.Ordinal));

        r.In(() => arcade.boss_hp = 1);
        Turn("Attack");
        r.In(() =>
        {
            Assert.Equal(0f, arcade.game_active);
            Assert.Equal(1f, arcade.ticket_count);
            Assert.Equal("arcade_off", arcade.program_open_overlay);
        });
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("arcade/win", StringComparison.Ordinal));
        Turn("Dispense_Tickets");
        r.In(() =>
        {
            Assert.Equal(0f, arcade.ticket_count);
            Assert.Single(Dm.each<obj_item_stack_arcadeticket>(laptop.loc.contents));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_maintenance_theme_joins_themeify_and_deletes_its_holder()
    {
        var r = new AudioRoom(7, 7, seed: 4304);
        mob_living_carbon_human alice = r.Join(3, 3, "alice");
        var pda = r.In(() => Dm.@new<obj_item_modular_computer_pda_crew_assistant>(locate(3, 3, 1)));
        var themeify = Install<datum_computer_file_program_themeify>(r, pda);
        float sizeBefore = r.In(() => Dm.num(themeify.size));
        r.In(() =>
        {
            var theme = Dm.@new<datum_computer_file_program_maintenance_theme_cat>();
            Assert.Equal(Dm.str(theme.theme_name) + " Theme", theme.filename);
            Assert.True(Dm.truthy(pda.store_file(theme))); // its on_install runs through COMSIG_COMPUTER_FILE_STORE
            Assert.Contains(Dm.str(theme.theme_name), Dm.each<object>(themeify.imported_themes).Select(Dm.str));
            Assert.True(Dm.num(themeify.size) > sizeBefore);
            Assert.False(Dm.truthy(theme.can_store_file(pda))); // the same theme twice: no
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_spectre_meter_feels_a_ghost_and_its_needle_moves()
    {
        var r = new AudioRoom(9, 7, seed: 4305);
        mob_living_carbon_human alice = r.Join(3, 3, "alice");
        var pda = r.In(() => Dm.@new<obj_item_modular_computer_pda_crew_assistant>(locate(3, 3, 1)));
        var meter = Install<datum_computer_file_program_maintenance_spectre_meter>(r, pda);
        r.In(() => Waiting.NoSleep(meter.on_start(alice)));
        mob_living_carbon_human spirit = r.Dummy(3, 3);
        r.In(() => spirit.mob_biotypes = Dm.bitor(spirit.mob_biotypes, MOB_SPIRIT));
        r.In(() => Waiting.NoSleep(meter.scan_surroundings()));
        r.In(() =>
        {
            Assert.Equal(90f, meter.last_spook_value); // a spirit on the same turf: 10 * 6 * 1.5
            Assert.Equal("spectre_meter_90", meter.program_open_overlay);
            Assert.False(meter.manual_scan_cd <= world.time); // the manual scan's cooldown
        });
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("ping_hit", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_dos_floods_a_relay_and_revelation_wipes_its_computer()
    {
        var r = new AudioRoom(9, 7, seed: 4306);
        mob_living_carbon_human alice = r.Join(3, 3, "alice");
        var relay = r.In(() => Dm.@new<obj_machinery_ntnet_relay>(locate(6, 3, 1)));
        var laptop = r.In(() => Dm.@new<obj_item_modular_computer_laptop>(locate(3, 3, 1)));
        var dos = Install<datum_computer_file_program_ntnet_dos>(r, laptop);
        Act(r, alice, dos, "PRG_target_relay", Dm.list(Dm.kv("targid", r.In(() => relay.uid))));
        Act(r, alice, dos, "PRG_execute");
        r.In(() =>
        {
            Assert.Same(relay, dos.target);
            Assert.Contains(dos, Dm.each<object>(relay.dos_sources));
            Assert.Equal(1f, SSmodular_computers.intrusion_detection_alarm);
            Assert.Contains(Dm.each<object>(SSmodular_computers.modpc_logs).Select(Dm.str), l => l.Contains("IDS WARNING", StringComparison.Ordinal));
        });

        var revelation = Install<datum_computer_file_program_revelation>(r, laptop);
        Act(r, alice, revelation, "PRG_obfuscate", Dm.list(Dm.kv("new_name", "Totally Safe")));
        Act(r, alice, revelation, "PRG_arm");
        r.In(() =>
        {
            Assert.Equal("Totally Safe", revelation.filedesc);
            Assert.Equal(1f, Dm.num((revelation.clone() as datum_computer_file_program_revelation).armed)); // a copy keeps the arming
        });
        float integrity = r.In(() => Dm.num(laptop.atom_integrity));
        Act(r, alice, revelation, "PRG_activate");
        r.In(() =>
        {
            Assert.Empty(Dm.each<object>(laptop.stored_files));
            Assert.False(Dm.truthy(laptop.enabled));
            Assert.Equal(integrity, Dm.num(laptop.atom_integrity)); // tg's purge runtimes before the damage (revelation.cs)
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_cursor_replacer_swaps_the_wearers_pointer_from_the_id_slot()
    {
        var r = new AudioRoom(7, 7, seed: 4307);
        mob_living_carbon_human alice = r.Join(3, 3, "alice");
        var pda = r.In(() => Dm.@new<obj_item_modular_computer_pda_crew_assistant>(locate(3, 3, 1)));
        var sword = Install<datum_computer_file_program_maintenance_cool_sword>(r, pda);
        r.In(() =>
        {
            if (alice.wear_id != null)
                qdel(alice.wear_id);
            if (alice.w_uniform == null) // the ID slot needs a jumpsuit
                Assert.True(Dm.truthy(Waiting.NoSleep(alice.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(alice.loc), ITEM_SLOT_ICLOTHING))));
            Assert.True(Dm.truthy(Waiting.NoSleep(alice.equip_to_slot_if_possible(pda, ITEM_SLOT_ID))));
            Assert.Equal(Dm.str(sword.sword_icon), Dm.str(alice.client.mouse_override_icon));
            Waiting.NoSleep(alice.dropItemToGround(pda));
            Assert.Null(alice.client.mouse_override_icon);
        });
        r.AssertNoRuntimes();
    }
}
