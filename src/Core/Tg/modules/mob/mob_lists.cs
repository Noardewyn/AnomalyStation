// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/mob_lists.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The procs reached only by a mob with a client (Login and Logout, M1) keep the reads the port cannot make yet as
// Dm.NotTranslated with a GAP: SSlag_switch (controllers/subsystem/lag_switch.dm), for a dead player. The observer's (the ghosts,
// 2026-10-16) read the lag switch's default (its measures off). Syndicate A1: add_to_current_living_antags.

// tg: code/modules/mob/mob_lists.dm:1
public partial class mob : atom_movable
{
    ///Adds the mob reference to the list and directory of all mobs. Called on Initialize().
    // tg: code/modules/mob/mob_lists.dm:2
    public virtual void add_to_mob_list()
    {
        Dm.combine(ref GLOB.mob_list, this);
    }

    ///Removes the mob reference from the list and directory of all mobs. Called on Destroy().
    // tg: code/modules/mob/mob_lists.dm:6
    public virtual void remove_from_mob_list()
    {
        Dm.remove(ref GLOB.mob_list, this);
    }

    ///Adds the mob reference to the list of all mobs alive. If mob is cliented, it adds it to the list of all living player-mobs.
    // tg: code/modules/mob/mob_lists.dm:10
    public virtual void add_to_alive_mob_list()
    {
        if (QDELETED(this))
            return;
        Dm.combine(ref GLOB.alive_mob_list, this);
        if (Dm.truthy(client))
            add_to_current_living_players();
    }

    ///Removes the mob reference from the list of all mobs alive. If mob is cliented, it removes it from the list of all living player-mobs.
    // tg: code/modules/mob/mob_lists.dm:18
    public virtual void remove_from_alive_mob_list()
    {
        Dm.remove(ref GLOB.alive_mob_list, this);
        if (Dm.truthy(client))
            remove_from_current_living_players();
    }

    ///Adds a mob reference to the list of all suicided mobs
    // tg: code/modules/mob/mob_lists.dm:24
    public virtual void add_to_mob_suicide_list()
    {
        Dm.append(ref GLOB.suicided_mob_list, this);
    }

    ///Removes a mob references from the list of all suicided mobs
    // tg: code/modules/mob/mob_lists.dm:28
    public virtual void remove_from_mob_suicide_list()
    {
        Dm.remove(ref GLOB.suicided_mob_list, this);
    }

    ///Adds the mob reference to the list of all the dead mobs. If mob is cliented, it adds it to the list of all dead player-mobs.
    // tg: code/modules/mob/mob_lists.dm:32
    public virtual void add_to_dead_mob_list()
    {
        if (QDELETED(this))
            return;
        Dm.combine(ref GLOB.dead_mob_list, this);
        if (Dm.truthy(client))
            add_to_current_dead_players();
    }

    ///Remvoes the mob reference from list of all the dead mobs. If mob is cliented, it adds it to the list of all dead player-mobs.
    // tg: code/modules/mob/mob_lists.dm:40
    public virtual void remove_from_dead_mob_list()
    {
        Dm.remove(ref GLOB.dead_mob_list, this);
        if (Dm.truthy(client))
            remove_from_current_dead_players();
    }

    ///Adds the cliented mob reference to the list of all player-mobs, besides to either the of dead or alive player-mob lists, as appropriate. Called on Login().
    // tg: code/modules/mob/mob_lists.dm:47
    [ShouldCallParent]
    public virtual void add_to_player_list()
    {
        Dm.combine(ref GLOB.player_list, this);
        if (stat == DEAD)
            add_to_current_dead_players();
        else
            add_to_current_living_players();
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_PLAYER_LOGIN]))
            SSdcs._SendSignal(COMSIG_GLOB_PLAYER_LOGIN, Dm.list(SSdcs, this));
    }

    ///Removes the mob reference from the list of all player-mobs, besides from either the of dead or alive player-mob lists, as appropriate. Called on Logout().
    // tg: code/modules/mob/mob_lists.dm:59
    [ShouldCallParent]
    public virtual void remove_from_player_list()
    {
        Dm.remove(ref GLOB.player_list, this);
        Dm.remove(ref GLOB.keyloop_list, this);
        if (stat == DEAD)
            remove_from_current_dead_players();
        else
            remove_from_current_living_players();
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_PLAYER_LOGOUT]))
            SSdcs._SendSignal(COMSIG_GLOB_PLAYER_LOGOUT, Dm.list(SSdcs, this));
    }

    ///Adds the cliented mob reference to either the list of dead player-mobs or to the list of observers, depending on how they joined the game.
    // tg: code/modules/mob/mob_lists.dm:72
    public virtual void add_to_current_dead_players()
    {
        Dm.combine(ref GLOB.dead_player_list, this);
        // GAP(lead): SSlag_switch (controllers/subsystem/lag_switch.dm, the lag switch measures of admins) is not declared;
        // SSlag_switch__gap stands in for it (rules 2.5), read as null: no measure is on, as in a round without admin lag
        // measures (M2 P2: dead humans with a client reach this since their damage kills them).
        datum SSlag_switch__gap = null;
        if (!Dm.truthy((Dm.get_var_or_null(SSlag_switch__gap, "measures") as DmList)?[DISABLE_DEAD_KEYLOOP]) || Dm.truthy(client.holder))
            Dm.combine(ref GLOB.keyloop_list, this);
        else
            Dm.remove(ref GLOB.keyloop_list, this);
    }

    ///Removes the mob reference from either the list of dead player-mobs or from the list of observers, depending on how they joined the game.
    // tg: code/modules/mob/mob_lists.dm:92
    public virtual void remove_from_current_dead_players()
    {
        Dm.remove(ref GLOB.dead_player_list, this);
    }

    ///Adds the cliented mob reference to the list of living player-mobs. If the mob is an antag, it adds it to the list of living antag player-mobs.
    // tg: code/modules/mob/mob_lists.dm:103
    public virtual void add_to_current_living_players()
    {
        Dm.combine(ref GLOB.alive_player_list, this);
        Dm.combine(ref GLOB.keyloop_list, this);
        if (Dm.truthy(is_antag(NONE)))
            add_to_current_living_antags();
    }

    ///Removes the mob reference from the list of living player-mobs. If the mob is an antag, it removes it from the list of living antag player-mobs.
    // tg: code/modules/mob/mob_lists.dm:110
    public virtual void remove_from_current_living_players()
    {
        Dm.remove(ref GLOB.alive_player_list, this);
        if (Dm.truthy(is_antag(NONE)))
            remove_from_current_living_antags();
    }

    ///Adds the cliented mob reference to the list of living antag player-mobs.
    // tg: code/modules/mob/mob_lists.dm:117
    public virtual void add_to_current_living_antags()
    {
        if (length(mind.antag_datums) == 0)
            return;

        foreach (datum_antagonist antagonist in Dm.each<datum_antagonist>(mind.antag_datums))
        {
            if (Dm.truthy(Dm.bitand(antagonist.antag_flags, ANTAG_SKIP_GLOBAL_LIST)))
                continue;
            Dm.combine(ref GLOB.current_living_antags, this);
            return;
        }
    }

    ///Removes the mob reference from the list of living antag player-mobs.
    // tg: code/modules/mob/mob_lists.dm:128
    public virtual void remove_from_current_living_antags()
    {
        Dm.remove(ref GLOB.current_living_antags, this);
    }
}

// tg: code/modules/mob/mob_lists.dm:79
public partial class mob_dead_observer : mob_dead
{
    // tg: code/modules/mob/mob_lists.dm:79
    public override void add_to_current_dead_players()
    {
        // tg: :80  if(!SSlag_switch.measures[DISABLE_DEAD_KEYLOOP] || client?.holder) // observers can move
        // GAP(lead): SSlag_switch (controllers/subsystem/lag_switch.dm, the lag switch, M10) is not declared; its measures are
        // off by default, so the test is TRUE (as death.cs and observer.cs read it).
        Dm.combine(ref GLOB.keyloop_list, this);
        if (Dm.truthy(started_as_observer))
        {
            Dm.combine(ref GLOB.current_observers_list, this);
            return;
        }
        else
        {
            Dm.combine(ref GLOB.dead_player_list, this);
        }
    }

    // tg: code/modules/mob/mob_lists.dm:95
    public override void remove_from_current_dead_players()
    {
        if (Dm.truthy(started_as_observer))
        {
            Dm.remove(ref GLOB.current_observers_list, this);
            return;
        }
        base.remove_from_current_dead_players();
    }
}

// tg: code/modules/mob/mob_lists.dm:88
public partial class mob_dead_new_player : mob_dead
{
    // tg: code/modules/mob/mob_lists.dm:88
    public override void add_to_current_dead_players()
    {
        return;
    }
}
