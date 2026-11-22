// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/sign_language.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file (the sign language component's toggle, tg's sign_language button of icons/hud/actions.dmi). The var
// block is generated (D111).

/**
 * Allows a Carbon to toggle sign language on/off. The button is invisible for mute Carbons.
 * Theory of Operation:
 * A. If TRAIT_SIGN_LANG is added/removed, and the button is visible, then update the button.
 * B. React to presence of trait TRAIT_MUTE for quality/convenience purposes:
 * C. If TRAIT_MUTE is added, then activate and hide the Action.
 * D. If TRAIT_MUTE is then removed, then show the Action.
 *
 * * Credits:
 * - Action sprite created by @Wallemations (icons/hud/actions.dmi:sign_language)
*/
// tg: code/datums/actions/mobs/sign_language.dm:12
public partial class datum_action_innate_sign_language : datum_action_innate
{
    // tg: code/datums/actions/mobs/sign_language.dm:19
    public override object is_action_active(atom_movable_screen_movable_action_button current_button = null)
    {
        return HAS_TRAIT(owner, TRAIT_SIGN_LANG);
    }

    // tg: code/datums/actions/mobs/sign_language.dm:22
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        if (!Dm.truthy(owner))
            return dot;

        RegisterSignal(grant_to, SIGNAL_REMOVETRAIT(TRAIT_MUTE), nameof(on_unmuted));
        RegisterSignal(grant_to, SIGNAL_ADDTRAIT(TRAIT_MUTE), nameof(on_muted));

        if (HAS_TRAIT(grant_to, TRAIT_MUTE))
        {
            // Convenience. Mute Carbons can only speak with sign language.
            if (!Dm.truthy(active))
                await Activate();
        }
        else
        {
            // Convenience. Only display action if the Carbon isn't mute.
            await show_action();
        }
        return dot;
    }

    // tg: code/datums/actions/mobs/sign_language.dm:38
    public override async DmTask Remove(mob removed_from = null)
    {
        await base.Remove(removed_from);
        mob grant_to = removed_from;
        UnregisterSignal(grant_to, Dm.list(
            SIGNAL_ADDTRAIT(TRAIT_SIGN_LANG),
            SIGNAL_REMOVETRAIT(TRAIT_SIGN_LANG),
            SIGNAL_ADDTRAIT(TRAIT_MUTE),
            SIGNAL_REMOVETRAIT(TRAIT_MUTE)));
        REMOVE_TRAIT(grant_to, TRAIT_SIGN_LANG, ACTION_TRAIT);
    }

    // tg: code/datums/actions/mobs/sign_language.dm:48
    public override async DmTask<object> Activate()
    {
        active = TRUE;
        ADD_TRAIT(owner, TRAIT_SIGN_LANG, ACTION_TRAIT);
        to_chat(owner, span_green(I18n.T("You are now communicating with sign language.")));
        await build_all_button_icons(UPDATE_BUTTON_BACKGROUND);
        return null;
    }

    // tg: code/datums/actions/mobs/sign_language.dm:54
    public override async DmTask<object> Deactivate()
    {
        active = FALSE;
        REMOVE_TRAIT(owner, TRAIT_SIGN_LANG, ACTION_TRAIT);
        to_chat(owner, span_green(I18n.T("You have stopped using sign language.")));
        await build_all_button_icons(UPDATE_BUTTON_BACKGROUND);
        return null;
    }

    /// Shows the linked action to the owner Carbon.
    // tg: code/datums/actions/mobs/sign_language.dm:61
    public virtual async DmTask show_action()
    {
        owner_has_control = TRUE;
        RegisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_SIGN_LANG), nameof(update_status_on_signal));
        RegisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_SIGN_LANG), nameof(update_status_on_signal));
        await GiveAction(owner as mob);
    }

    /// Hides the linked action from the owner Carbon.
    // tg: code/datums/actions/mobs/sign_language.dm:68
    public virtual void hide_action()
    {
        owner_has_control = FALSE;
        UnregisterSignal(owner, Dm.list(
            SIGNAL_ADDTRAIT(TRAIT_SIGN_LANG),
            SIGNAL_REMOVETRAIT(TRAIT_SIGN_LANG)));
        HideFrom(owner as mob);
    }

    /// Signal handler for SIGNAL_ADDTRAIT(TRAIT_MUTE)
    /// Hides the action if the signing Carbon gains TRAIT_MUTE.
    // tg: code/datums/actions/mobs/sign_language.dm:78
    [SignalHandler]
    public virtual void on_muted()
    {
        hide_action();
        // Enable sign language if the Carbon knows it and just gained TRAIT_MUTE
        if (!HAS_TRAIT(owner, TRAIT_SIGN_LANG))
            Waiting.NoSleep(Activate());
    }

    /// Signal handler for SIGNAL_REMOVETRAIT(TRAIT_MUTE)
    /// Re-shows the action if the signing Carbon loses TRAIT_MUTE.
    // tg: code/datums/actions/mobs/sign_language.dm:88
    [SignalHandler]
    public virtual void on_unmuted()
    {
        Waiting.NoSleep(show_action());
    }
}
