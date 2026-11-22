// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/toggle_suit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the whole file but the screentip context (on_adding_context is a live-partial row, the owner's rule of
// 2026-10-29), which RegisterWithParent leaves unregistered. The var block is generated.

/*
 * Simple component for allowing a user to change an atom's appearance on alt-click.
 * When toggled, changes the atom's icon_state between [base_icon_state] and [base_icon_state]_t.
 */
// tg: code/datums/components/toggle_suit.dm:7
public partial class datum_component_toggle_icon : datum_component
{
    // tg: code/datums/components/toggle_suit.dm:18
    [DmParams("toggle_noun")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object toggle_noun = Dm.arg(__extra, 0);
        toggle_noun ??= "buttons";
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        atom atom_parent = parent as atom;
        Dm.combine(ref atom_parent.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);

        this.toggle_noun = toggle_noun;
        this.inferred_base_icon_state = Dm.truthy(atom_parent.post_init_icon_state) ? atom_parent.post_init_icon_state : atom_parent.icon_state;
        return null;
    }

    // tg: code/datums/components/toggle_suit.dm:30
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_CLICK_ALT, nameof(on_click_alt));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        // COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM -> on_adding_context: screentips (the owner's rule of 2026-10-29)
    }

    // tg: code/datums/components/toggle_suit.dm:36
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_CLICK_ALT, COMSIG_ATOM_EXAMINE, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM));
    }

    /*
     * Signal proc for COMSIG_CLICK_ALT.
     * If the user is living, adjacent to the source,
     * and has arms / is not incapacitated, call do_icon_toggle.
     *
     * source - the atom being clicked on
     * user - the mob doing the click
     */
    // tg: code/datums/components/toggle_suit.dm:48
    [SignalHandler]
    public virtual object on_click_alt(atom source = null, mob_living living_user = null)
    {
        if (!isliving(living_user) || !Dm.truthy(Waiting.NoSleep(living_user.can_perform_action(source))))
            return null;

        if (living_user.usable_hands <= 0)
        {
            source.balloon_alert(living_user, I18n.T("you don't have hands!"));
            return null;
        }

        do_icon_toggle(source, living_user);
        return CLICK_ACTION_SUCCESS;
    }

    /*
     * Signal proc for COMSIG_ATOM_EXAMINE.
     * Lets the user know they can toggle the parent open or closed.
     *
     * source - the atom being examined (parent)
     * user - the mob examining it
     * examine_list - the list of things within the examine output
     */
    // tg: code/datums/components/toggle_suit.dm:68
    [SignalHandler]
    public virtual void on_examine(atom source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_notice(I18n.T("Alt-click on [] to toggle the [].", source, toggle_noun)));
    }

    /*
     * Signal proc for COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM.
     * Adds usage context for toggling the parent open or closed.
     */
    // tg: code/datums/components/toggle_suit.dm:82  (a live-partial stub: screentips)

    /*
     * Actually do the toggle of the icon.
     * Swaps the icon from [base_icon_state] to [base_icon_state]_t.
     *
     * source - the atom being toggled
     * user - the mob doing the toggling
     */
    // tg: code/datums/components/toggle_suit.dm:93
    public virtual void do_icon_toggle(atom source = null, mob_living user = null)
    {
        source.balloon_alert(user, I18n.T("toggled []", toggle_noun));

        toggled = Dm.truthy(toggled) ? FALSE : TRUE;

        object icon_state_to_use = Dm.truthy(source.base_icon_state) ? source.base_icon_state : inferred_base_icon_state;
        if (Dm.truthy(toggled))
            source.icon_state = Dm.text("[]_t", icon_state_to_use);
        else
            source.icon_state = (string)icon_state_to_use;

        if (isitem(source))
        {
            obj_item item_source = source as obj_item;
            item_source.update_slot_icon();
        }
    }
}
