// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/investigative/reveal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): The psychologist's reveal, which announces a player's role in the morning, once a game. The var block is generated (D111).

/**
 * Reveal
 *
 * During the night, revealing someone will announce their role when day comes.
 * This is one time use, we'll delete ourselves once done.
 */
// tg: code/modules/mafia/abilities/investigative/reveal.dm:7
public partial class datum_mafia_ability_reveal_role : datum_mafia_ability
{
    // tg: code/modules/mafia/abilities/investigative/reveal.dm:11
    public override async DmTask<object> perform_action_target(datum_mafia_controller game = null, datum_mafia_role day_target = null)
    {
        object dot = await base.perform_action_target(game, day_target);
        if (!Dm.truthy(dot))
            return FALSE;

        host_role.send_message_to_player(span_warning(I18n.T("You have revealed the true nature of the []!", target_role)));
        await target_role.reveal_role(game, verbose: TRUE);
        return TRUE;
    }

    // tg: code/modules/mafia/abilities/investigative/reveal.dm:20
    [SignalHandler]
    public override void clean_action_refs(datum_mafia_controller game = null)
    {
        if (Dm.truthy(using_ability))
        {
            Dm.remove(ref host_role.role_unique_actions, this);
            qdel(this);
        }
        base.clean_action_refs(game);
    }
}
