// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/roles/neutral/neutral_chaos.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): the obsessed, given a townie to get lynched on the first night, and the clown, who takes a voter
// down when lynched. The var blocks are generated (D111).

// tg: code/modules/mafia/roles/neutral/neutral_chaos.dm:1
public partial class datum_mafia_role_obsessed : datum_mafia_role
{
    // tg: code/modules/mafia/roles/neutral/neutral_chaos.dm:16  note: obsession is always a townie
    [DmParams("game")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        datum_mafia_controller game = Dm.arg(__extra, 0) as datum_mafia_controller;
        desc = Dm.initial(this, (datum_mafia_role_obsessed r) => r.desc) + I18n.T(" Obsessions are assigned on the first night.");
        RegisterSignal(game, COMSIG_MAFIA_SUNDOWN, nameof(find_obsession));
    }

    // tg: code/modules/mafia/roles/neutral/neutral_chaos.dm:21
    [SignalHandler]
    public virtual void find_obsession(datum_mafia_controller game = null)
    {
        DmList all_roles_shuffle = Dm.sub(shuffle(game.living_roles), this) as DmList;
        foreach (datum_mafia_role possible in Dm.each_cast<datum_mafia_role>(all_roles_shuffle))
        {
            if (Dm.truthy(Dm.bitand(possible.team, MAFIA_TEAM_TOWN)))
            {
                obsession = possible;
                break;
            }
        }
        if (obsession == null)
            obsession = pick(all_roles_shuffle) as datum_mafia_role; //okay no town just pick anyone here
        desc = Dm.initial(this, (datum_mafia_role_obsessed r) => r.desc) + I18n.T(" Target: []", obsession.body.real_name);
        obj_item_modular_computer modpc = player_pda;
        if (modpc != null)
            Waiting.NoSleep(modpc.update_static_data_for_all_viewers());
        else
            Waiting.NoSleep(game.update_static_data(body));
        send_message_to_player(span_userdanger(I18n.T("Your obsession is []! Get them lynched to win!", obsession.body.real_name)));
        RegisterSignal(obsession, COMSIG_MAFIA_ON_KILL, nameof(check_victory));
        UnregisterSignal(game, COMSIG_MAFIA_SUNDOWN);
    }

    // tg: code/modules/mafia/roles/neutral/neutral_chaos.dm:41
    [SignalHandler]
    public virtual void check_victory(datum source = null, datum_mafia_controller game = null, object attacker = null, object lynch = null)
    {
        UnregisterSignal(source, COMSIG_MAFIA_ON_KILL);
        if (game_status == MAFIA_DEAD)
            return;
        if (Dm.truthy(lynch))
        {
            game.send_message(I18n.T("<span class='big comradio'>!! OBSESSED VICTORY !!</span>"));
            game.award_role(winner_award, this);
            Waiting.NoSleep(reveal_role(game, FALSE));
        }
        else
            to_chat(body, span_userdanger(I18n.T("You have failed your objective to lynch []!", obsession.body.real_name)));
    }
}

// tg: code/modules/mafia/roles/neutral/neutral_chaos.dm:54
public partial class datum_mafia_role_clown : datum_mafia_role
{
    // tg: code/modules/mafia/roles/neutral/neutral_chaos.dm:66
    [DmParams("game")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(this, COMSIG_MAFIA_ON_KILL, nameof(prank));
    }

    // tg: code/modules/mafia/roles/neutral/neutral_chaos.dm:70
    [SignalHandler]
    public virtual void prank(datum source = null, datum_mafia_controller game = null, object attacker = null, object lynch = null)
    {
        if (Dm.truthy(lynch))
        {
            datum_mafia_role victim = pick(Dm.add(game.judgement_guilty_votes, game.judgement_abstain_votes) as DmList) as datum_mafia_role;
            game.send_message(I18n.T("<span class='big clown'>[] WAS A CLOWN! HONK! They take down [] with their last prank.</span>", body.real_name, victim.body.real_name));
            game.send_message(I18n.T("<span class='big clown'>!! CLOWN VICTORY !!</span>"));
            game.award_role(winner_award, this);
            Waiting.NoSleep(victim.kill(game, FALSE));
        }
    }
}
