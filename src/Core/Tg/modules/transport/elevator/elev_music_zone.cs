// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/elevator/elev_music_zone.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D325 (multi-z, elevators): the whole file.

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/transport/elevator/elev_music_zone.dm:1  GLOBAL_LIST_EMPTY(elevator_music)
    public DmList elevator_music;

    // tg: code/modules/transport/elevator/elev_music_zone.dm:1  (GLOBAL_MANAGED(elevator_music, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalelevator_music()
    {
        elevator_music = Dm.list();
        Dm.append(ref gvars_datum_init_order, "elevator_music");
    }
}

// tg: code/modules/transport/elevator/elev_music_zone.dm:3
public partial class obj_effect_abstract_elevator_music_zone : obj_effect_abstract
{
    // tg: code/modules/transport/elevator/elev_music_zone.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(linked_elevator_id))
        {
            log_mapping(Dm.text("No elevator ID for elevator music provided at [].", AREACOORD(this)));
            return INITIALIZE_HINT_QDEL;
        }

        GLOB.elevator_music[linked_elevator_id] = this;
        sound_player = Dm.@new<datum_proximity_monitor_elevator_music_area>(this, Dm.named("range", range), Dm.named("soundloop_type", soundloop_type));
        return dot;
    }

    // tg: code/modules/transport/elevator/elev_music_zone.dm:28
    public override object Destroy(float? force = null)
    {
        Dm.remove(GLOB.elevator_music, this);
        QDEL_NULL(ref sound_player);
        return base.Destroy(force);
    }

    // tg: code/modules/transport/elevator/elev_music_zone.dm:33
    public virtual void link_to_panel(atom elevator_panel = null)
    {
        RegisterSignal(elevator_panel, COMSIG_MACHINERY_POWER_RESTORED, nameof(on_panel_powered));
        RegisterSignal(elevator_panel, COMSIG_MACHINERY_POWER_LOST, nameof(on_panel_depowered));
        RegisterSignal(elevator_panel, COMSIG_QDELETING, nameof(on_panel_destroyed));
    }

    /// Start sound loops when power is restored
    // tg: code/modules/transport/elevator/elev_music_zone.dm:39
    public virtual void on_panel_powered()
    {
        sound_player.turn_on();
    }

    /// Stop sound loops if power is lost
    // tg: code/modules/transport/elevator/elev_music_zone.dm:44
    public virtual void on_panel_depowered()
    {
        sound_player.turn_off();
    }

    /// Die if panel is destroyed, although currently they are invincible
    // tg: code/modules/transport/elevator/elev_music_zone.dm:49
    public virtual void on_panel_destroyed()
    {
        qdel(this);
    }
}

/// Load or unload a looping sound when mobs enter or exit the area
// tg: code/modules/transport/elevator/elev_music_zone.dm:55
public partial class datum_proximity_monitor_elevator_music_area : datum_proximity_monitor
{
    // tg: code/modules/transport/elevator/elev_music_zone.dm:65
    [DmParams("_host", "range", "_ignore_if_not_on_turf", "soundloop_type")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        soundloop_type = Dm.arg<Type>(__extra, 3);
    }

    // tg: code/modules/transport/elevator/elev_music_zone.dm:69
    public override object Destroy(float? force = null)
    {
        QDEL_LIST_ASSOC_VAL(tracked_mobs);
        return base.Destroy(force);
    }

    // tg: code/modules/transport/elevator/elev_music_zone.dm:73
    public override void on_entered(atom source = null, atom_movable arrived = null, turf old_loc = null)
    {
        base.on_entered(source, arrived, old_loc);
        if (arrived is not mob entered || entered.mind == null)
            return;

        if (Dm.@in(entered, tracked_mobs))
            return;
        object pref_volume = entered.client?.prefs.read_preference(typeof(datum_preference_numeric_volume_sound_ambience_volume));
        if (Dm.num(pref_volume) > 0)
        {
            datum_looping_sound soundloop = (datum_looping_sound)Dm.@new((Type)soundloop_type, Dm.named("_parent", entered), Dm.named("_direct", TRUE), Dm.named("start_immediately", enabled));
            soundloop.volume = Dm.num(soundloop.volume) * Dm.num(pref_volume) / 100;
            tracked_mobs[entered] = soundloop;
        }
        else
        {
            tracked_mobs[entered] = null; // Still add it to the list so we don't keep making this check
        }
        RegisterSignal(entered, COMSIG_QDELETING, nameof(mob_destroyed));
    }

    // tg: code/modules/transport/elevator/elev_music_zone.dm:89
    public override void on_uncrossed(atom source = null, atom_movable gone = null, object direction = null)
    {
        base.on_uncrossed(source, gone, direction);
        if (!Dm.@in(gone, tracked_mobs))
            return;
        if (Dm.num(get_dist(host, gone)) <= Dm.num(current_range))
            return;
        qdel(tracked_mobs[gone]);
        Dm.remove(tracked_mobs, gone);
        UnregisterSignal(gone, COMSIG_QDELETING);
    }

    /// Remove references on mob deletion
    // tg: code/modules/transport/elevator/elev_music_zone.dm:100
    public virtual void mob_destroyed(mob former_mob = null)
    {
        if (Dm.@in(former_mob, tracked_mobs))
        {
            qdel(tracked_mobs[former_mob]);
            Dm.remove(tracked_mobs, former_mob);
        }
    }

    /// Start sound loops playing
    // tg: code/modules/transport/elevator/elev_music_zone.dm:107
    public virtual void turn_on()
    {
        enabled = TRUE;
        foreach (object mob in Dm.each(tracked_mobs))
        {
            datum_looping_sound loop = tracked_mobs[mob] as datum_looping_sound;
            loop.start();
        }
    }

    /// Stop active sound loops
    // tg: code/modules/transport/elevator/elev_music_zone.dm:113
    public virtual void turn_off()
    {
        enabled = FALSE;
        foreach (object mob in Dm.each(tracked_mobs))
        {
            datum_looping_sound loop = tracked_mobs[mob] as datum_looping_sound;
            loop.stop();
        }
    }
}
