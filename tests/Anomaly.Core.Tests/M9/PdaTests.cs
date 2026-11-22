using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M9;

/// <summary>
/// M9 slice E: tg's PDA (modular_computers/computers/item/pda.dm, computer.dm): its default and PDA apps, its pen, its imprint that
/// registers its messenger, its power button and NtOS's window, an app's start and its program overlay.
/// </summary>
public class PdaTests
{
    [Fact]
    public void A_crew_pda_holds_tgs_apps_and_its_imprint_registers_the_messenger()
    {
        var r = new AudioRoom(6, 6, seed: 5);
        r.In(() =>
        {
            var pda = Dm.@new<obj_item_modular_computer_pda_crew_assistant>(locate(2, 2, 1));
            var names = Dm.each<datum_computer_file>(pda.stored_files).Select(f => f.filename).ToList();
            Assert.Equal(new[] { "themeify", "ntsoftwarehub", "filemanager", "nt_messenger", "ntpay", "notepad", "plexagoncrew", "bountyboard" }, names); // the assistant's starting program last
            Assert.IsType<obj_item_pen>(pda.inserted_item);
            Assert.Equal("assistant PDA", pda.name);
            Assert.Empty(Dm.each(GLOB.pda_messengers));

            pda.imprint_id("Alice Doe", "Assistant");
            Assert.Equal("Alice Doe (Assistant)", pda.name);
            var messenger = Dm.each<datum_computer_file_program_messenger>(pda.stored_files).Single();
            Assert.Same(messenger, GLOB.pda_messengers[REF(messenger)]);
            Assert.Equal("Alice Doe (Assistant)", Dm.str(get_messenger_name(messenger)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_power_button_boots_ntos_and_an_app_shows_its_overlay()
    {
        var r = new AudioRoom(6, 6, seed: 6);
        mob_living_carbon_human alice = r.Dummy(2, 2);
        r.In(() =>
        {
            var pda = Dm.@new<obj_item_modular_computer_pda_crew_assistant>(locate(2, 2, 1));
            Assert.False(Dm.truthy(pda.enabled));
            Assert.True(Dm.truthy(Waiting.NoSleep(pda.turn_on(null, FALSE))));
            Assert.True(Dm.truthy(pda.enabled));
            var notepad = Dm.each<datum_computer_file_program_notepad>(pda.stored_files).Single();
            Assert.True(Dm.truthy(Waiting.NoSleep(pda.open_program(null, notepad, FALSE))));
            Assert.Same(notepad, pda.active_program);
            Assert.Contains(Dm.each<object>(Waiting.NoSleep(pda.update_overlays()) as DmList), o => o is mutable_appearance ma && Dm.eq(ma.icon_state, "generic"));
            // the cell drains with use (computer_power.dm:39): 2 W plus the app's 2 per second
            float before = Dm.num(pda.cell.charge);
            pda.handle_power(1);
            Assert.True(Dm.num(pda.cell.charge) < before);
            pda.shutdown_computer(FALSE);
            Assert.Null(pda.active_program);
            Assert.False(Dm.truthy(pda.enabled));
        });
        r.AssertNoRuntimes();
    }
}
