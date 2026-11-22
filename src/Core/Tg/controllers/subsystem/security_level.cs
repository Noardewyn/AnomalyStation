// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/security_level.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G4 (D272): the whole file: the emergency shuttle's call time and recall point and the communications console read the
// security level.

// tg: code/controllers/subsystem/security_level.dm:1 SUBSYSTEM_DEF(security_level)
[DmPath("/datum/controller/subsystem/security_level")]
public partial class datum_controller_subsystem_security_level : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(security_level) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSsecurity_level) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/security_level.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSsecurity_level != this)
        {
            if (SSsecurity_level is datum_controller_subsystem_security_level)
            {
                Recover();
                qdel(SSsecurity_level);
            }
            SSsecurity_level = this;
        }
        PreInit();
        await DmTask.CompletedTask;
    }

    // tg: code/controllers/subsystem/security_level.dm:11
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        foreach (Type iterating_security_level_type in Dm.each<Type>(subtypesof(typeof(datum_security_level))))
        {
            var new_security_level = (datum_security_level)Dm.@new(iterating_security_level_type);
            available_levels[new_security_level.name] = new_security_level;
            Dm.append(ref alert_level_icons, new_security_level.status_display_icon_state);
        }
        current_security_level = available_levels[number_level_to_text(SEC_LEVEL_GREEN)] as datum_security_level;
        await DmTask.CompletedTask;
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/security_level.dm:19
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        if (current_security_level.looping_sound == null) // No sound? No play.
        {
            can_fire = FALSE;
            return;
        }
        sound_to_playing_players(current_security_level.looping_sound, volume_preference: typeof(datum_preference_numeric_volume_sound_ambience_volume));
        await DmTask.CompletedTask;
    }

    /**
     * Sets a new security level as our current level
     *
     * This is how everything should change the security level.
     *
     * Arguments:
     * * new_level - The new security level that will become our current level
     * * announce - Play the announcement, set FALSE if you're doing your own custom announcement to prevent duplicates
     */
    // tg: code/controllers/subsystem/security_level.dm:35
    public virtual void set_level(object new_level = null, object announce = null)
    {
        announce ??= TRUE;
        new_level = istext(new_level) ? new_level : number_level_to_text(new_level);
        if (Dm.eq(new_level, current_security_level.name)) // If we are already at the desired level, do nothing
            return;

        var selected_level = available_levels[new_level] as datum_security_level;

        if (selected_level == null)
            throw Dm.CRASH(Dm.text("set_level was called with an invalid security level([])", new_level), "code/controllers/subsystem/security_level.dm", 43);

        if (Dm.truthy(announce))
            level_announce(selected_level, current_security_level.number_level); // We want to announce BEFORE updating to the new level

        SSsecurity_level.current_security_level = selected_level;

        if (selected_level.looping_sound != null)
        {
            wait = Dm.num(selected_level.looping_sound_interval);
            can_fire = TRUE;
        }
        else
        {
            can_fire = FALSE;
        }

        // SEND_SIGNAL(src, COMSIG_SECURITY_LEVEL_CHANGED, selected_level.number_level) (rules 11.1 (e), a statement)
        if (Dm.truthy(_listen_lookup?[COMSIG_SECURITY_LEVEL_CHANGED]))
            _SendSignal(COMSIG_SECURITY_LEVEL_CHANGED, Dm.list(this, selected_level.number_level));
        SSblackbox.record_feedback("tally", "security_level_changes", 1, selected_level.name);
    }

    /**
     * Returns the current security level as a number
     */
    // tg: code/controllers/subsystem/security_level.dm:62
    public virtual object get_current_level_as_number()
    {
        return (!Dm.truthy(initialized) || current_security_level == null) ? SEC_LEVEL_GREEN : current_security_level.number_level; //Send the default security level in case the subsystem hasn't finished initializing yet
    }

    /**
     * Returns the current security level as text
     */
    // tg: code/controllers/subsystem/security_level.dm:68
    public virtual object get_current_level_as_text()
    {
        return (!Dm.truthy(initialized) || current_security_level == null) ? "green" : current_security_level.name;
    }

    /**
     * Converts a text security level to a number
     *
     * Arguments:
     * * level - The text security level to convert
     */
    // tg: code/controllers/subsystem/security_level.dm:77
    public virtual object text_level_to_number(object text_level = null)
    {
        var selected_level = available_levels[text_level] as datum_security_level;
        return selected_level?.number_level;
    }

    /**
     * Converts a number security level to a text
     *
     * Arguments:
     * * level - The number security level to convert
     */
    // tg: code/controllers/subsystem/security_level.dm:87
    public virtual object number_level_to_text(object number_level = null)
    {
        foreach (object iterating_level_text in Dm.each(available_levels))
        {
            var iterating_security_level = (datum_security_level)available_levels[iterating_level_text];
            if (Dm.eq(iterating_security_level.number_level, number_level))
                return iterating_security_level.name;
        }
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/security_level.dm:1  SUBSYSTEM_DEF(security_level): GLOBAL_REAL(SSsecurity_level, /datum/controller/subsystem/security_level)
    internal datum_controller_subsystem_security_level SSsecurity_level;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_security_level SSsecurity_level => ref TgState.Current.SSsecurity_level;
}
