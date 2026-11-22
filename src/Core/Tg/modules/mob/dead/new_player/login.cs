// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/dead/new_player/login.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F): the lobby mob's Login. Not translated: the experience tracking (the database), the panic bunker's interview
// and the BYOND build warning (no BYOND clients).

public partial class mob_dead_new_player : mob_dead
{
    // tg: code/modules/mob/dead/new_player/login.dm:1
    public override async DmTask<object> Login()
    {
        if (client == null)
            return null;

        // if(CONFIG_GET(flag/use_exp_tracking)) client.set_exp_from_db(); client.set_db_player_flags(): the database (M10+)

        if (mind == null)
        {
            mind = Dm.@new<datum_mind>(key);
            mind.active = TRUE;
            mind.set_current(this);
        }

        // Check if user should be added to interview queue: the panic bunker (admin tooling)

        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;

        object motd = config.motd;
        if (Dm.truthy(motd))
            to_chat(this, Dm.text("<div class=\"motd\">[]</div>", motd), handle_whitespace: FALSE);

        // if(GLOB.admin_notice) to_chat(...): GAP(lead), the admin notice (an admin verb's, M10+) is not declared

        object spc = config.Get(typeof(datum_config_entry_number_soft_popcap));
        if (Dm.truthy(spc) && Dm.ge(living_player_count(), spc))
            to_chat(this, span_notice(Dm.text("<b>Server Notice:</b>\n \t []", config.Get(typeof(datum_config_entry_string_soft_popcap_message)))));

        add_sight(SEE_TURFS);

        client.playtitlemusic();

        // var/datum/asset/asset_datum = get_asset_datum(/datum/asset/simple/lobby); asset_datum.send(client): the lobby's stylesheet for
        // the BYOND browser (the port's client draws the lobby itself, DV44)
        if (client == null) // client disconnected during asset transit
            return FALSE;

        // The parent call for Login() may do a bunch of stuff, like add verbs.
        // Delaying the register_for_interview until the very end makes sure it can clean everything up
        // and set the player's client up for interview.
        if (Dm.truthy(client.interviewee))
        {
            Dm.NotTranslated("code/modules/mob/dead/new_player/login.dm:52", "Login: register_for_interview (the panic bunker's interview, admin tooling)");
            return null;
        }

        if (Dm.lt(SSticker.current_state, GAME_STATE_SETTING_UP))
        {
            object tl = SSticker.GetTimeLeft();
            to_chat(this, Dm.text("Please set up your character and select \"Ready\". The game will start [].", Dm.gt(tl, 0) ? Dm.text("in about []", DisplayTimeText(tl)) : "soon"));
        }

        // if(GLOB.unrecommended_builds[num2text(client.byond_build)]): no BYOND clients
        return null;
    }
}
