// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui_input/say_modal/typing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice A (docs/ai/specs/social.md). The typing preference is read through DV161 (client New sets typing_indicators from its default).
// play_fov_effect (the talk icon a viewer with a limited field of view sees at the edge of it) is presentation not ported
// (do_attack_animation's call is deferred the same way).

public partial class mob : atom_movable
{
    /** Creates a thinking indicator over the mob. */
    // tg: code/modules/tgui_input/say_modal/typing.dm:2
    public virtual object create_thinking_indicator()
    {
        return null;
    }

    /** Removes the thinking indicator over the mob. */
    // tg: code/modules/tgui_input/say_modal/typing.dm:6
    public virtual object remove_thinking_indicator()
    {
        return null;
    }

    /** Creates a typing indicator over the mob. */
    // tg: code/modules/tgui_input/say_modal/typing.dm:10
    public virtual object create_typing_indicator()
    {
        return null;
    }

    /** Removes the typing indicator over the mob. */
    // tg: code/modules/tgui_input/say_modal/typing.dm:14
    public virtual object remove_typing_indicator()
    {
        return null;
    }

    /** Removes any indicators and marks the mob as not speaking IC. */
    // tg: code/modules/tgui_input/say_modal/typing.dm:18
    public virtual object remove_all_indicators()
    {
        return null;
    }

    // tg: code/modules/tgui_input/say_modal/typing.dm:21  (rules 7.5: the slot's root; its ..() is mob.dm's set_stat__d0)
    public virtual async DmTask<object> set_stat(object new_stat = null)
    {
        object dot = await set_stat__d0(new_stat);
        if (Dm.truthy(dot))
            remove_all_indicators();
        return dot;
    }

    // tg: code/modules/tgui_input/say_modal/typing.dm:26  (rules 7.5: the slot's root; its ..() is logout.dm's Logout__d1)
    public virtual async DmTask<object> Logout()
    {
        remove_all_indicators();
        return await Logout__d1();
    }
}

public partial class datum_tgui_say : datum
{
    /** Sets the mob as "thinking" - with indicator and the TRAIT_THINKING_IN_CHARACTER trait */
    // tg: code/modules/tgui_input/say_modal/typing.dm:40
    public virtual object start_thinking()
    {
        if (!Dm.truthy(window_open))
            return FALSE;
        return client.start_thinking();
    }

    /** Removes typing/thinking indicators and flags the mob as not thinking */
    // tg: code/modules/tgui_input/say_modal/typing.dm:46
    public virtual object stop_thinking()
    {
        return client.stop_thinking();
    }

    /**
     * Handles the user typing. After a brief period of inactivity,
     * signals the client mob to revert to the "thinking" icon.
     */
    // tg: code/modules/tgui_input/say_modal/typing.dm:53
    public virtual object start_typing()
    {
        if (!Dm.truthy(window_open))
            return FALSE;
        return client.start_typing();
    }

    /**
     * Remove the typing indicator after a brief period of inactivity or during say events.
     * If the user was typing IC, the thinking indicator is shown.
     */
    // tg: code/modules/tgui_input/say_modal/typing.dm:62
    public virtual object stop_typing()
    {
        if (!Dm.truthy(window_open))
            return FALSE;
        client.stop_typing();
        return null;
    }
}

public partial class mob_living : mob
{
    /// Overrides for overlay creation
    // tg: code/modules/tgui_input/say_modal/typing.dm:68
    public override object create_thinking_indicator()
    {
        if (Dm.truthy(active_thinking_indicator) || Dm.truthy(active_typing_indicator) || IS_UNCONSCIOUS_OR_CRIT(this) || !HAS_TRAIT(this, TRAIT_THINKING_IN_CHARACTER))
            return FALSE;
        active_thinking_indicator = mutable_appearance("icons/mob/effects/talk.dmi", Dm.text("[]3", bubble_icon), TYPING_LAYER);
        add_overlay(active_thinking_indicator);
        // tg: :73  play_fov_effect(src, 6, "talk", ignore_self = TRUE) (presentation, see the file comment)
        return null;
    }

    // tg: code/modules/tgui_input/say_modal/typing.dm:75
    public override object remove_thinking_indicator()
    {
        if (!Dm.truthy(active_thinking_indicator))
            return FALSE;
        cut_overlay(active_thinking_indicator);
        active_thinking_indicator = null;
        return null;
    }

    // tg: code/modules/tgui_input/say_modal/typing.dm:81
    public override object create_typing_indicator()
    {
        if (Dm.truthy(active_typing_indicator) || Dm.truthy(active_thinking_indicator) || IS_UNCONSCIOUS_OR_CRIT(this) || !HAS_TRAIT(this, TRAIT_THINKING_IN_CHARACTER))
            return FALSE;
        active_typing_indicator = mutable_appearance("icons/mob/effects/talk.dmi", Dm.text("[]0", bubble_icon), TYPING_LAYER);
        add_overlay(active_typing_indicator);
        // tg: :86  play_fov_effect(src, 6, "talk", ignore_self = TRUE) (presentation, see the file comment)
        return null;
    }

    // tg: code/modules/tgui_input/say_modal/typing.dm:88
    public override object remove_typing_indicator()
    {
        if (!Dm.truthy(active_typing_indicator))
            return FALSE;
        cut_overlay(active_typing_indicator);
        active_typing_indicator = null;
        return null;
    }

    // tg: code/modules/tgui_input/say_modal/typing.dm:94
    public override object remove_all_indicators()
    {
        REMOVE_TRAIT(this, TRAIT_THINKING_IN_CHARACTER, CURRENTLY_TYPING_TRAIT);
        remove_thinking_indicator();
        remove_typing_indicator();
        return null;
    }
}
