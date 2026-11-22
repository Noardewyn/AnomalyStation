// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/voting/changeling_kill.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): The changelings' kill vote and their night chat. The var block is generated (D111).

/**
 * Changeling kill
 *
 * During the night, changelings vote for who to kill.
 * The attacker will always be the first person in the list, killing them will go to the next.
 */
// tg: code/modules/mafia/abilities/voting/changeling_kill.dm:7
public partial class datum_mafia_ability_changeling_kill : datum_mafia_ability
{
    ///Boolean on whether a Changeling has been sent to attack someone yet.
    // tg: code/modules/mafia/abilities/voting/changeling_kill.dm:12  var/static/ling_sent (rules 10.6)
    private static float ling_sent
    {
        get => TgState.Current.datum_mafia_ability_changeling_kill__ling_sent;
        set => TgState.Current.datum_mafia_ability_changeling_kill__ling_sent = value;
    }

    // tg: code/modules/mafia/abilities/voting/changeling_kill.dm:14
    [SignalHandler]
    public override void clean_action_refs(datum_mafia_controller game = null)
    {
        ling_sent = FALSE;
        game.reset_votes("Mafia");
        base.clean_action_refs(game);
    }

    // tg: code/modules/mafia/abilities/voting/changeling_kill.dm:19
    public override async DmTask<object> perform_action_target(datum_mafia_controller game = null, datum_mafia_role day_target = null)
    {
        datum_mafia_role victim = game.get_vote_winner("Mafia") as datum_mafia_role;
        if (victim == null)
            return FALSE;
        target_role = victim;

        object dot = await base.perform_action_target(game, day_target);
        if (!Dm.truthy(dot))
            return FALSE;
        if (Dm.truthy(ling_sent))
            return FALSE;

        ling_sent = TRUE;
        if (Dm.truthy(await target_role.kill(game, host_role, FALSE)))
            target_role.send_message_to_player(span_userdanger(I18n.T("You have been killed by a Changeling!")));
        game.send_message(span_danger(I18n.T("[] was selected to attack [] tonight!", host_role.body.real_name, target_role.body.real_name)), MAFIA_TEAM_MAFIA);
        return TRUE;
    }

    // tg: code/modules/mafia/abilities/voting/changeling_kill.dm:37
    public override object set_target(datum_mafia_role new_target = null)
    {
        if (Dm.truthy(Dm.bitand(new_target.team, MAFIA_TEAM_MAFIA)))
            return FALSE;
        if (!Dm.truthy(validate_action_target(new_target)))
            return FALSE;
        using_ability = TRUE;
        host_role.mafia_game_controller.vote_for(host_role, new_target, "Mafia", MAFIA_TEAM_MAFIA);
        return null;
    }

    /**
     * handle_speech
     *
     * During the night, Changelings talking will instead redirect it to Changeling chat.
     */
    // tg: code/modules/mafia/abilities/voting/changeling_kill.dm:50
    [SignalHandler]
    public override object handle_speech(datum source = null, DmList speech_args = null)
    {
        object dot = base.handle_speech(source, speech_args);
        if (host_role.mafia_game_controller.phase != MAFIA_PHASE_NIGHT)
            return FALSE;

        string message = span_changeling(I18n.T("<b>\\[CHANGELING CHAT\\] []</b>: []", source, html_decode(speech_args[SPEECH_MESSAGE])));
        host_role.mafia_game_controller.send_message(message, MAFIA_TEAM_MAFIA);
        speech_args[SPEECH_MESSAGE] = "";
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mafia/abilities/voting/changeling_kill.dm:12  var/static/ling_sent (rules 10.6)
    internal float datum_mafia_ability_changeling_kill__ling_sent;
}
