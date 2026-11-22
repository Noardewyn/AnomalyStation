using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV424): the port's own AI ability (the owner, 2026-11-14): tg's AI sees turfs through walls (SEE_TURFS), which the 3D client
// draws as the meson view; an action button switches that sight off and back on. It is a plain /datum/action/innate whose trigger the AI
// answers (COMSIG_ACTION_TRIGGER, as tg's storage gather-mode button), so no DM type of the port's own is needed; active (the default) is tg's sight.
public partial class mob_living_silicon_ai
{
    /// <summary>DV424: the AI's meson vision button (granted in Initialize).</summary>
    public datum_action_innate dm_meson_action;

    // DV424: Initialize's grant of the button
    public virtual void grant_meson_vision_action()
    {
        dm_meson_action = Dm.@new<datum_action_innate>();
        dm_meson_action.name = "Toggle Meson Vision";
        dm_meson_action.desc = "Sees the station's structure through walls, or not.";
        dm_meson_action.background_icon_state = "bg_tech_blue";
        dm_meson_action.overlay_icon_state = "bg_tech_blue_border";
        dm_meson_action.button_icon = Dm.resource("icons/obj/clothing/glasses.dmi");
        dm_meson_action.button_icon_state = "meson";
        dm_meson_action.active = TRUE;
        RegisterSignal(dm_meson_action, COMSIG_ACTION_TRIGGER, nameof(toggle_meson_vision));
        Waiting.NoSleep(dm_meson_action.Grant(this));
    }

    [SignalHandler]
    public virtual object toggle_meson_vision(datum source = null, datum_action triggered = null)
    {
        dm_meson_action.active = Dm.truthy(dm_meson_action.active) ? FALSE : TRUE;
        update_sight();
        Waiting.Detach(dm_meson_action.build_all_button_icons(UPDATE_BUTTON_STATUS));
        return COMPONENT_ACTION_BLOCK_TRIGGER;
    }
}
