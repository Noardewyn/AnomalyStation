// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/support/seance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): The chaplain's seance, speaking with the dead at night. The var block is generated (D111).

/**
 * Seance
 *
 * An ability that doesn't give you any actions, instead grants the ability to speak with the dead during the Night.
 */
// tg: code/modules/mafia/abilities/support/seance.dm:6
public partial class datum_mafia_ability_seance : datum_mafia_ability
{
    /**
     * handle_message
     *
     * During the night, Seancers speaking will instead be talking to deadchat.
     */
    // tg: code/modules/mafia/abilities/support/seance.dm:16
    [SignalHandler]
    public override object handle_speech(datum source = null, DmList speech_args = null)
    {
        object dot = base.handle_speech(source, speech_args);
        if (host_role.mafia_game_controller.phase != MAFIA_PHASE_NIGHT)
            return FALSE;

        string message = span_changeling(I18n.T("<b>\\[DEAD CHAT - CHAPLAIN\\] []</b>: []", source, html_decode(speech_args[SPEECH_MESSAGE])));
        host_role.mafia_game_controller.send_message(message, team: MAFIA_TEAM_DEAD);
        speech_args[SPEECH_MESSAGE] = "";
        return TRUE;
    }
}
