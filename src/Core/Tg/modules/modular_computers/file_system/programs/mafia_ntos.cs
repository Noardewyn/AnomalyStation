// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/mafia_ntos.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): Mafia on a PDA: the panel's data with the PDA as the player, the header and the alert when a game
// with the PDA starts, and leaving the game when the app is deleted. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/mafia_ntos.dm:1
public partial class datum_computer_file_program_mafia : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/mafia_ntos.dm:12
    public override void on_install(datum_computer_file source = null, obj_item_modular_computer computer_installing = null, mob user = null)
    {
        base.on_install(source, computer_installing, user);
        RegisterSignal(SSdcs, COMSIG_MAFIA_GAME_START, nameof(on_game_start));
    }

    // tg: code/modules/modular_computers/file_system/programs/mafia_ntos.dm:16
    public override object Destroy(float? force = null)
    {
        datum_mafia_controller game = GLOB.mafia_game as datum_mafia_controller;
        if (game == null)
            return base.Destroy(force);
        UnregisterSignal(game, COMSIG_MAFIA_GAME_END);
        datum_mafia_role pda_role = game.get_role_player(computer) as datum_mafia_role;
        if (pda_role == null)
            return base.Destroy(force);
        game.send_message(span_notice(I18n.T("[] has deleted the game from their PDA, and therefore has left the game.", pda_role.body)));
        Waiting.NoSleep(pda_role.kill(game));
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/file_system/programs/mafia_ntos.dm:29
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        datum_mafia_controller game = GLOB.mafia_game as datum_mafia_controller;
        game ??= create_mafia_game();
        Dm.append(ref data, await game.ui_static_data(computer));
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/mafia_ntos.dm:37
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        datum_mafia_controller game = GLOB.mafia_game as datum_mafia_controller;
        game ??= create_mafia_game();
        Dm.append(ref data, await game.ui_data(computer));
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/mafia_ntos.dm:45
    public override async DmTask<object> ui_assets(mob user = null)
    {
        DmList data = Dm.list();
        datum_mafia_controller game = GLOB.mafia_game as datum_mafia_controller;
        game ??= create_mafia_game();
        Dm.append(ref data, await game.ui_assets(user));
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/mafia_ntos.dm:53  (tg names its first parameter 'user'; it is the action)
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        await base.ui_act(action, @params, ui, state);
        datum_mafia_controller game = GLOB.mafia_game as datum_mafia_controller;
        game ??= create_mafia_game();
        return await game.ui_act(action, @params, ui, state);
    }

    ///Called when a game of Mafia starts, sets the ui header to the proper one.
    // tg: code/modules/modular_computers/file_system/programs/mafia_ntos.dm:60
    [SignalHandler]
    public virtual void on_game_start(datum_controller_subsystem_processing_dcs source = null, datum_mafia_controller game = null)
    {
        RegisterSignal(game, COMSIG_MAFIA_GAME_END, nameof(on_game_end));
        ui_header = "mafia.gif";
        if (Dm.truthy(game.get_role_player(computer)))
        {
            alert_pending = TRUE;
            computer.alert_call(this, I18n.T("Mafia game started!"));
        }
    }

    ///Called when a game of Mafia ends, deletes its ui header.
    // tg: code/modules/modular_computers/file_system/programs/mafia_ntos.dm:69
    [SignalHandler]
    public virtual void on_game_end(datum_mafia_controller game = null)
    {
        UnregisterSignal(game, COMSIG_MAFIA_GAME_END);
        ui_header = null;
        Waiting.NoSleep(update_static_data_for_all_viewers());
    }
}
