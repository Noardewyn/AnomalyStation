// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/skill_learning/generic_skillchips/point.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. The Kommand chip: a big pointing arrow, in a colour its action picks (tg's arrow_large states of
// icons/hud/screen_gen.dmi, as the ID trims' big pointer). The var blocks are generated (D111).

/**
 * A skillchip that gives the user bigger arrows when pointing at things (like some id trims do).
 * As a bonus, they can costumize the color of the arrow/pointer too.
 */
// tg: code/modules/library/skill_learning/generic_skillchips/point.dm:5
public partial class obj_item_skillchip_big_pointer : obj_item_skillchip
{
    // tg: code/modules/library/skill_learning/generic_skillchips/point.dm:16
    public override async DmTask on_activate(mob_living_carbon user = null, float? silent = null)
    {
        silent ??= FALSE;
        await base.on_activate(user, silent);
        RegisterSignal(user, COMSIG_MOVABLE_POINTED, nameof(fancier_pointer));
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/point.dm:20
    public override async DmTask on_deactivate(mob_living_carbon user = null, float? silent = null)
    {
        silent ??= FALSE;
        UnregisterSignal(user, COMSIG_MOVABLE_POINTED);
        datum_action_change_pointer_color action = Dm.locate<datum_action_change_pointer_color>(actions) as datum_action_change_pointer_color;
        if (action != null)
            action.arrow_color = null;
        if (action != null)
            action.arrow_overlay = null;
        await base.on_deactivate(user, silent);
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/point.dm:27
    [SignalHandler]
    public virtual void fancier_pointer(mob_living user = null, atom pointed = null, obj_effect_temp_visual_point point = null)
    {
        if (HAS_TRAIT(user, TRAIT_UNKNOWN_APPEARANCE))
            return;
        point.cut_overlays();
        datum_action_change_pointer_color action = Dm.locate<datum_action_change_pointer_color>(actions) as datum_action_change_pointer_color;
        if (!Dm.truthy(action.arrow_color))
        {
            point.icon_state = "arrow_large";
            return;
        }
        point.icon_state = "arrow_large_white";
        point.color = action.arrow_color;
        object highlight = mutable_appearance(point.icon, "arrow_large_white_highlights", appearance_flags: RESET_COLOR);
        point.add_overlay(highlight);
    }
}

// tg: code/modules/library/skill_learning/generic_skillchips/point.dm:41
public partial class datum_action_change_pointer_color : datum_action
{
    // tg: code/modules/library/skill_learning/generic_skillchips/point.dm:52
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        arrow_overlay = null;
        return dot;
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/point.dm:56
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        mob user = owner as mob;
        if (!Dm.truthy(arrow_color))
        {
            await pick_color(user);
            return dot;
        }
        object choice = await tgui_alert(owner, I18n.T("Reset or update pointer color?"), I18n.T("Pointer Color"), Dm.list(I18n.T("Reset"), I18n.T("Update")));
        if (user != owner || !Dm.truthy(choice) || !Dm.truthy(await IsAvailable(feedback: TRUE)))
            return dot;
        if (Dm.eq(choice, I18n.T("Update")))
            await pick_color(user);
        else
        {
            arrow_color = null;
            owner.balloon_alert(owner as mob, I18n.T("pointer reset"));
            await build_all_button_icons(update_flags: UPDATE_BUTTON_ICON, force: TRUE);
        }
        return dot;
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/point.dm:74
    public virtual async DmTask pick_color(mob user = null)
    {
        object ncolor = await tgui_color_picker(owner, I18n.T("Pick new color"), I18n.T("Pointer Color"), arrow_color);
        if (user != owner || !Dm.truthy(await IsAvailable(feedback: TRUE)))
            return;
        arrow_color = ncolor;
        owner.balloon_alert(owner as mob, I18n.T("pointer updated"));
        await build_all_button_icons(update_flags: UPDATE_BUTTON_ICON, force: TRUE);
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/point.dm:82
    public override void apply_button_icon(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        force ??= FALSE;
        object initial_icon = Dm.initial(typeof(obj_effect_temp_visual_point), (obj_effect_temp_visual_point p) => p.icon);
        current_button.cut_overlay(arrow_overlay);
        if (!Dm.truthy(arrow_color))
        {
            current_button.icon = initial_icon;
            current_button.icon_state = Dm.initial(typeof(obj_effect_temp_visual_point), (obj_effect_temp_visual_point p) => p.icon_state);
            base.apply_button_icon(current_button, force);
            return;
        }

        current_button.icon = null;
        current_button.icon_state = null;
        arrow_overlay = mutable_appearance(icon: initial_icon, icon_state: "arrow_large_white_still") as mutable_appearance;
        arrow_overlay.color = arrow_color;
        Dm.append(arrow_overlay.overlays, mutable_appearance(icon: initial_icon, icon_state: "arrow_large_white_still_highlights", appearance_flags: RESET_COLOR));
        current_button.add_overlay(arrow_overlay);
    }
}
