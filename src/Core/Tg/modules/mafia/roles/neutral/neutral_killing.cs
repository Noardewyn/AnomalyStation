// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/roles/neutral/neutral_killing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): the traitor, immune to night kills, and the nightmare, revealed as a shadowperson. The var blocks
// are generated (D111).

// tg: code/modules/mafia/roles/neutral/neutral_killing.dm:1
public partial class datum_mafia_role_traitor : datum_mafia_role
{
    // tg: code/modules/mafia/roles/neutral/neutral_killing.dm:16
    [DmParams("game")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(this, COMSIG_MAFIA_ON_KILL, nameof(nightkill_immunity));
    }

    // tg: code/modules/mafia/roles/neutral/neutral_killing.dm:20
    [SignalHandler]
    public virtual object nightkill_immunity(datum source = null, datum_mafia_controller game = null, object attacker = null, object lynch = null)
    {
        if (game.phase == MAFIA_PHASE_NIGHT && !Dm.truthy(lynch))
        {
            to_chat(body, span_userdanger(I18n.T("You were attacked, but they'll have to try harder than that to put you down.")));
            return MAFIA_PREVENT_KILL;
        }
        return null;
    }
}

// tg: code/modules/mafia/roles/neutral/neutral_killing.dm:27
public partial class datum_mafia_role_nightmare : datum_mafia_role
{
    // tg: code/modules/mafia/roles/neutral/neutral_killing.dm:42
    public override void special_reveal_equip(datum_mafia_controller game = null)
    {
        body.set_species(typeof(datum_species_shadow));
        Waiting.NoSleep(body.update_body());
    }
}
