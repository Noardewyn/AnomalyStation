// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/ghost.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F): the ghost HUD toggle (the ghost's HUD reads it), the accessories and the others (the observer reads them);
// the engineering package E2 (D287): the inquisitive ghost toggle (a ghost's click examines, _onclick/observer.dm:58); syndicate X2: the
// ghost roles toggle (the ghosts' polls read it). The ghost's
// form, orbit and the lighting keep DV161 (their defaults).

/// Determines what accessories your ghost will look like they have.
// tg: code/modules/client/preferences/ghost.dm:2
public partial class datum_preference_choiced_ghost_accessories : datum_preference_choiced
{
    // tg: code/modules/client/preferences/ghost.dm:7
    public override DmList init_possible_values()
    {
        return Dm.list(GHOST_ACCS_NONE, GHOST_ACCS_DIR, GHOST_ACCS_FULL);
    }

    // tg: code/modules/client/preferences/ghost.dm:10
    public override object create_default_value()
    {
        return GHOST_ACCS_DEFAULT_OPTION;
    }

    // tg: code/modules/client/preferences/ghost.dm:13
    public override void apply_to_client(client client = null, object value = null)
    {
        if (client.mob is not mob_dead_observer ghost)
            return;

        ghost.ghost_accs = value as string;
        ghost.update_appearance();
    }

    // tg: code/modules/client/preferences/ghost.dm:21
    public override object deserialize(object input = null, datum_preferences preferences = null)
    {
        // Old ghost preferences used to be 1/50/100.
        // Whoever did that wasted an entire day of my time trying to get those sent
        // properly, so I'm going to buck them.
        if (Dm.isnum(input))
        {
            if (Dm.eq(input, 1))
                input = GHOST_ACCS_NONE;
            else if (Dm.eq(input, 50))
                input = GHOST_ACCS_DIR;
            else if (Dm.eq(input, 100))
                input = GHOST_ACCS_FULL;
        }

        return base.deserialize(input);
    }
}

/// Determines the appearance of your ghost to others, when you are a BYOND member
// tg: code/modules/client/preferences/ghost.dm:135
public partial class datum_preference_choiced_ghost_others : datum_preference_choiced
{
    // tg: code/modules/client/preferences/ghost.dm:140
    public override DmList init_possible_values()
    {
        return Dm.list(GHOST_OTHERS_SIMPLE, GHOST_OTHERS_DEFAULT_SPRITE, GHOST_OTHERS_THEIR_SETTING);
    }

    // tg: code/modules/client/preferences/ghost.dm:147
    public override object create_default_value()
    {
        return GHOST_OTHERS_DEFAULT_OPTION;
    }

    // tg: code/modules/client/preferences/ghost.dm:150
    public override void apply_to_client(client client = null, object value = null)
    {
        if (client.mob is not mob_dead_observer ghost)
            return;

        ghost.update_sight();
    }

    // tg: code/modules/client/preferences/ghost.dm:157
    public override object deserialize(object input = null, datum_preferences preferences = null)
    {
        // Old ghost preferences used to be 1/50/100.
        // Whoever did that wasted an entire day of my time trying to get those sent
        // properly, so I'm going to buck them.
        if (Dm.isnum(input))
        {
            if (Dm.eq(input, 1))
                input = GHOST_OTHERS_SIMPLE;
            else if (Dm.eq(input, 50))
                input = GHOST_OTHERS_DEFAULT_SPRITE;
            else if (Dm.eq(input, 100))
                input = GHOST_OTHERS_THEIR_SETTING;
        }

        return base.deserialize(input, preferences);
    }
}

/// Toggles the HUD for ghosts
// tg: code/modules/client/preferences/ghost.dm:100
[DmPath("/datum/preference/toggle/ghost_hud")]
public partial class datum_preference_toggle_ghost_hud : datum_preference_toggle
{
    // tg: code/modules/client/preferences/ghost.dm:105
    public override void apply_to_client(client client = null, object value = null)
    {
        if (isobserver(client?.mob) && client.mob.hud_used != null)
            Waiting.NoSleep(client.mob.hud_used.show_hud());
    }
}

/// Whether or not ghosts can examine things by clicking on them.
// tg: code/modules/client/preferences/ghost.dm:174  (its vars only; the toggle's procs are the parent's)
public partial class datum_preference_toggle_inquisitive_ghost : datum_preference_toggle
{
}

/// When enabled, prevents any and all ghost role pop-ups.
// tg: code/modules/client/preferences/ghost.dm:179  (its vars only; syndicate X2: SSpolling reads it, so it is an entry, DV180)
public partial class datum_preference_toggle_ghost_roles : datum_preference_toggle
{
}
