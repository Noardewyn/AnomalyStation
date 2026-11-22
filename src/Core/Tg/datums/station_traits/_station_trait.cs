// Translated from tgstation (snapshot 1d99bd7f): code/datums/station_traits/_station_trait.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R2 (D327): the whole file: the station trait datum, its round start hook, its code trait on SSstation, its report line and
// its lobby button. The var block is generated (D111).

public partial class datum_controller_global_vars : datum_controller
{
    /// Station traits displayed in the lobby
    // tg: code/datums/station_traits/_station_trait.dm:2  GLOBAL_LIST_EMPTY(lobby_station_traits)
    public DmList lobby_station_traits;

    // tg: code/datums/station_traits/_station_trait.dm:2  (GLOBAL_MANAGED(lobby_station_traits, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGloballobby_station_traits()
    {
        lobby_station_traits = Dm.list();
        Dm.append(ref gvars_datum_init_order, "lobby_station_traits");
    }
}

///Base class of station traits. These are used to influence rounds in one way or the other by influencing the levers of the station.
// tg: code/datums/station_traits/_station_trait.dm:5
public partial class datum_station_trait : datum
{
    // tg: code/datums/station_traits/_station_trait.dm:37
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);

        RegisterSignal(SSticker, COMSIG_TICKER_ROUND_STARTING, nameof(on_round_start));

        if (Dm.truthy(sign_up_button))
        {
            Dm.append(ref GLOB.lobby_station_traits, this);
            // SEND_SIGNAL(SSdcs, COMSIG_GLOB_LOBBY_TRAIT_ADDED) (rules 11.1 (e), a statement)
            if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_LOBBY_TRAIT_ADDED]))
                SSdcs._SendSignal(COMSIG_GLOB_LOBBY_TRAIT_ADDED, Dm.list(SSdcs));
        }
        if (Dm.truthy(trait_processes))
            START_PROCESSING(SSstation, this);
        if (Dm.truthy(trait_to_give))
            ADD_TRAIT(SSstation, trait_to_give, STATION_TRAIT);
    }

    // tg: code/datums/station_traits/_station_trait.dm:50
    public override object Destroy(float? force = null)
    {
        object had_button = sign_up_button;
        Dm.remove(ref SSstation.station_traits, this);
        Dm.remove(ref GLOB.lobby_station_traits, this);
        if (Dm.truthy(had_button))
        {
            // SEND_SIGNAL(SSdcs, COMSIG_GLOB_LOBBY_TRAIT_REMOVED)
            if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_LOBBY_TRAIT_REMOVED]))
                SSdcs._SendSignal(COMSIG_GLOB_LOBBY_TRAIT_REMOVED, Dm.list(SSdcs));
        }
        REMOVE_TRAIT(SSstation, trait_to_give, STATION_TRAIT);
        return base.Destroy(force);
    }

    /// Returns the type of info the centcom report has on this trait, if any.
    // tg: code/datums/station_traits/_station_trait.dm:60
    public virtual object get_report()
    {
        return I18n.T("<i>[]</i> - []", name, report_message);
    }

    /// Will attempt to revert the station trait, used by admins.
    // tg: code/datums/station_traits/_station_trait.dm:64
    public virtual void revert()
    {
        if (!Dm.truthy(can_revert))
            throw Dm.CRASH(Dm.text("revert() was called on [], which can't be reverted!", type), "code/datums/station_traits/_station_trait.dm", 66);

        if (Dm.truthy(trait_to_give))
            REMOVE_TRAIT(SSstation, trait_to_give, STATION_TRAIT);

        qdel(this);
    }

    /// Return a color for the decals, if any
    // tg: code/datums/station_traits/_station_trait.dm:74
    public virtual object get_decal_color(object thing_to_color = null, object pattern = null)
    {
        return null;
    }

    /// Return TRUE if we want to show a lobby button, by default we assume we don't want it after the round begins
    // tg: code/datums/station_traits/_station_trait.dm:78
    public virtual object can_display_lobby_button(client player = null)
    {
        return Dm.truthy(sign_up_button) && !Dm.truthy(SSticker.HasRoundStarted()) ? TRUE : FALSE;
    }

    /// Called when a player clicks this trait's lobby button.
    /// Return a string to show as feedback in the lobby UI.
    // tg: code/datums/station_traits/_station_trait.dm:83
    public virtual async DmTask<object> on_lobby_button_click(mob_dead_new_player player = null)
    {
        return null;
    }

    /// Returns the tooltip text for this trait's lobby button
    // tg: code/datums/station_traits/_station_trait.dm:87
    public virtual object get_lobby_description()
    {
        return report_message;
    }

    /// Returns the icon state for this trait's lobby button for a given player
    // tg: code/datums/station_traits/_station_trait.dm:91
    public virtual object get_lobby_icon_state(mob_dead_new_player player = null)
    {
        return "signup";
    }

    /// Returns a list of overlay icon states to layer on top of the lobby button
    // tg: code/datums/station_traits/_station_trait.dm:95
    public virtual object get_lobby_overlay_states(mob_dead_new_player player = null)
    {
        return Dm.list();
    }

    /// Proc ran when round starts. Use this for roundstart effects.
    // tg: code/datums/station_traits/_station_trait.dm:99  SIGNAL_HANDLER
    public virtual void on_round_start()
    {
    }

    /// Called when overriding a pulsar star command report message.
    // tg: code/datums/station_traits/_station_trait.dm:103
    public virtual object get_pulsar_message()
    {
        return null;
    }
}
