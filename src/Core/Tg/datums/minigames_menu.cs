// Translated from tgstation (snapshot 1d99bd7f): code/datums/minigames_menu.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): the ghost's Minigames menu, with the HUD button and the verb that open it
// (_onclick/hud/screen_objects/ghost.dm:65-72, mob/dead/observer/observer.dm:973). Mafia opens its panel; capture the flag,
// basketball and deathmatch are recorded leaves (their games are not ported, not-translated section 164). The var blocks are
// generated (D111).

// tg: code/datums/minigames_menu.dm:1
public partial class datum_minigames_menu : datum
{
    // tg: code/datums/minigames_menu.dm:4
    [DmParams("new_owner")]
    public override async DmTask New(params object[] __extra)
    {
        mob_dead_observer new_owner = Dm.arg(__extra, 0) as mob_dead_observer;
        if (new_owner == null)
            qdel(this);
        owner = new_owner;
        await DmTask.CompletedTask;
    }

    // tg: code/datums/minigames_menu.dm:9
    public override object Destroy(float? force = null)
    {
        owner = null;
        return base.Destroy(force);
    }

    // tg: code/datums/minigames_menu.dm:13
    public override object ui_state(mob user = null) => GLOB.observer_state;

    // tg: code/datums/minigames_menu.dm:16
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "MinigamesMenu");
            await ui.open();
        }
        return null;
    }

    // tg: code/datums/minigames_menu.dm:22
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (Dm.str(action))
        {
            case "mafia":
                ui.close();
                await mafia();
                return TRUE;
            case "ctf":
                ui.close();
                ctf();
                return TRUE;
            case "basketball":
                ui.close();
                basketball();
                return TRUE;
            case "deathmatch":
                ui.close();
                deathmatch();
                return TRUE;
        }
        return dot;
    }

    // tg: code/datums/minigames_menu.dm:45
    public virtual async DmTask mafia()
    {
        datum_mafia_controller game = GLOB.mafia_game as datum_mafia_controller; //this needs to change if you want multiple mafia games up at once.
        game ??= create_mafia_game();
        await game.ui_interact(usr);
    }

    // tg: code/datums/minigames_menu.dm:51
    public virtual void ctf()
    {
        // var/datum/ctf_panel/ctf_panel = new(src); ctf_panel.ui_interact(usr): capture the flag's panel (the CTF game is not ported)
        Dm.Deferred("code/datums/minigames_menu.dm:52", "ctf: the capture the flag panel (CTF, not ported)");
    }

    // tg: code/datums/minigames_menu.dm:57
    public virtual void basketball()
    {
        // var/datum/basketball_controller/game = GLOB.basketball_game || create_basketball_game(); game.ui_interact(usr): basketball
        Dm.Deferred("code/datums/minigames_menu.dm:58", "basketball: the basketball minigame's panel (basketball, not ported)");
    }

    // tg: code/datums/minigames_menu.dm:63
    public virtual void deathmatch()
    {
        // GLOB.deathmatch_game ||= new; GLOB.deathmatch_game.ui_interact(usr): the deathmatch lobby
        Dm.Deferred("code/datums/minigames_menu.dm:64", "deathmatch: the deathmatch lobby (deathmatch, not ported)");
    }
}

// tg: code/_onclick/hud/screen_objects/ghost.dm:65
public partial class atom_movable_screen_ghost_minigames_menu : atom_movable_screen_ghost
{
    // tg: code/_onclick/hud/screen_objects/ghost.dm:70
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        mob_dead_observer observer = usr as mob_dead_observer;
        observer.open_minigames_menu();
        await DmTask.CompletedTask;
        return null;
    }
}

public partial class mob_dead_observer
{
    // tg: code/modules/mob/dead/observer/observer.dm:973  GAME_VERB_PROC(/mob/dead/observer, open_minigames_menu, "Minigames Menu", null)
    // (__DEFINES/verb.dm:96-114): the body proc; the verb proc (open_minigames_menu) is below.
    public virtual async DmTask __gvb_open_minigames_menu(DmList structured_args = null)
    {
        if (client == null)
            return;
        if (!isobserver(this))
        {
            to_chat(usr, span_warning(I18n.T("You must be a ghost to play minigames!")));
            return;
        }
        minigames_menu ??= Dm.@new<datum_minigames_menu>(this);

        await minigames_menu.ui_interact(this);
    }

    // tg: code/modules/mob/dead/observer/observer.dm:973  the verb proc's INVOKE_ASYNC(SSverbs, invoke_verb, ...), as open_spawners_menu's:
    // a detached call of the body; a caller without a client does nothing; the verb has no arguments.
    public virtual void open_minigames_menu(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_open_minigames_menu(Dm.list()));
    }
}
