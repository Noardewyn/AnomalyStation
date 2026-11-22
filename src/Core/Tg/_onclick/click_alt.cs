// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/click_alt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P1: the whole file; the loot panel (/mob/living/proc/try_open_loot_panel_on and its two calls) came with the gaps package G1
// (D291). The interaction guard of D132 is at the top of the target's click_alt and click_alt_secondary bases
// (docs/ai/specs/interaction.md 7, DV27): a refusal is CLICK_ACTION_BLOCKING.

// tg: code/_onclick/click_alt.dm:2
public partial class mob : atom_movable
{
    ///Main proc for primary alt click
    // tg: code/_onclick/click_alt.dm:2
    public virtual async DmTask AltClickOn(atom target = null)
    {
        await base_click_alt(target);
    }

    /**
     * ### Base proc for alt click interaction left click. Returns if the click was intercepted & handled
     *
     * If you wish to add custom `click_alt` behavior for a single type, use that proc.
     */
    // tg: code/_onclick/click_alt.dm:10
    public virtual async DmTask<object> base_click_alt(atom target = null)
    {
        // Check if they've hooked in to prevent src from alt clicking anything
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOB_ALTCLICKON]) ? (object)NONE : this._SendSignal(COMSIG_MOB_ALTCLICKON, Dm.list(this, target))), COMSIG_MOB_CANCEL_CLICKON)))
            return TRUE;

        // If it has a signal handler that returns a click action, done.
        if (Dm.truthy(Dm.bitand((!Dm.truthy(target._listen_lookup?[COMSIG_CLICK_ALT]) ? (object)NONE : target._SendSignal(COMSIG_CLICK_ALT, Dm.list(target, this))), CLICK_ACTION_ANY)))
            return TRUE;

        // If it has a custom click_alt that returns success/block, done.
        if (Dm.truthy(await can_perform_action(target, Dm.bitor(target.interaction_flags_click, SILENT_ADJACENCY))))
            return Dm.bitand(await target.click_alt(this), CLICK_ACTION_ANY);

        return FALSE;
    }
}

// tg: code/_onclick/click_alt.dm:27
public partial class mob_living : mob
{
    // tg: code/_onclick/click_alt.dm:27
    public override async DmTask<object> base_click_alt(atom target = null)
    {
        object dot = null;
        dot = await base.base_click_alt(target);
        if (Dm.truthy(dot))
            return dot;

        return await try_open_loot_panel_on(target);
    }
}

// tg: code/_onclick/click_alt.dm:63
public partial class atom : datum
{
    /**
     * ## Custom alt click interaction
     * Override this to change default alt click behavior. Return `CLICK_ACTION_SUCCESS`, `CLICK_ACTION_BLOCKING` or `NONE`.
     *
     * ### Guard clauses
     * Consider adding `interaction_flags_click` before adding unique guard clauses.
     *
     * ### Return flags
     * Forgetting your return will cause the default alt click behavior to occur thereafter.
     *
     * The difference between NONE and BLOCKING can get hazy, but I like to keep NONE limited to guard clauses and "never" cases.
     *
     * A good usage for BLOCKING over NONE is when it's situational for the item and there's some feedback indicating this.
     *
     * ### Examples:
     * User is a ghost, alt clicks on item with special disk eject: NONE
     *
     * Machine broken, no feedback: NONE
     *
     * Alt click a pipe to max output but its already max: BLOCKING
     *
     * Alt click a gun that normally works, but is out of ammo: BLOCKING
     *
     * User unauthorized, machine beeps: BLOCKING
     *
     * @param {mob} user - The person doing the alt clicking.
     */
    // tg: code/_onclick/click_alt.dm:63
    public virtual async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        // DEVIATION(DV27): the interaction guard (D132).
        if (InertSeam.MissingOverride(this, "click_alt", user))
            return CLICK_ACTION_BLOCKING;
        return NONE;
    }
}

// tg: code/_onclick/click_alt.dm:69
public partial class mob : atom_movable
{
    ///Main proc for secondary alt click
    // tg: code/_onclick/click_alt.dm:69
    public virtual async DmTask AltClickSecondaryOn(atom target = null)
    {
        await base_click_alt_secondary(target);
    }

    /**
     * ### Base proc for alt click interaction right click.
     *
     * If you wish to add custom `click_alt_secondary` behavior for a single type, use that proc.
     */
    // tg: code/_onclick/click_alt.dm:77
    public virtual async DmTask base_click_alt_secondary(atom target = null)
    {
        //Hook on the mob to intercept the click
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOB_ALTCLICKON_SECONDARY]) ? (object)NONE : this._SendSignal(COMSIG_MOB_ALTCLICKON_SECONDARY, Dm.list(this, target))), COMSIG_MOB_CANCEL_CLICKON)))
            return;

        //Hook on the atom to intercept the click
        if (Dm.truthy(Dm.bitand((!Dm.truthy(target._listen_lookup?[COMSIG_CLICK_ALT_SECONDARY]) ? (object)NONE : target._SendSignal(COMSIG_CLICK_ALT_SECONDARY, Dm.list(target, this))), COMPONENT_CANCEL_CLICK_ALT_SECONDARY)))
            return;

        // If it has a custom click_alt_secondary then do that
        if (Dm.truthy(await can_perform_action(target, Dm.bitor(target.interaction_flags_click, SILENT_ADJACENCY))))
            await target.click_alt_secondary(this);
    }
}

// tg: code/_onclick/click_alt.dm:99
public partial class atom : datum
{
    /**
     * ## Custom alt click secondary interaction
     * Override this to change default alt right click behavior.
     *
     * ### Guard clauses
     * Consider adding `interaction_flags_click` before adding unique guard clauses.
     **/
    // tg: code/_onclick/click_alt.dm:99
    public virtual async DmTask<object> click_alt_secondary(mob user = null)
    {
        // DEVIATION(DV27): the interaction guard (D132).
        if (InertSeam.MissingOverride(this, "click_alt_secondary", user))
            return CLICK_ACTION_BLOCKING;
        return NONE;
    }
}

// tg: code/_onclick/click_alt.dm:106
public partial class mob : atom_movable
{
    /**
     * ## No-op for unambiguous loot panel bind as a non-living mob.
     **/
    // tg: code/_onclick/click_alt.dm:106
    public virtual async DmTask<object> alt_shift_click_on(atom target = null)
    {
        return FALSE;
    }
}

// tg: code/_onclick/click_alt.dm:114
public partial class mob_living : mob
{
    /**
     * ## Bind for unambiguously opening the loot panel as a living mob.
     * This raises no signals and is not meant to have its behavior overridden.
     **/
    // tg: code/_onclick/click_alt.dm:114
    public override async DmTask<object> alt_shift_click_on(atom target = null)
    {
        return await try_open_loot_panel_on(target);
    }

    ///Helper for determining if a living mob may open the loot panel for some target, since it is shared between
    ///alt and alt-shift click.
    ///Returns FALSE if the mob is unable to open the loot panel at the target and TRUE if the loot panel was opened.
    // tg: code/_onclick/click_alt.dm:121
    public virtual async DmTask<object> try_open_loot_panel_on(atom target = null)
    {
        if (!Dm.@in(this, viewers(target)) || (Dm.truthy(is_blind()) && !IN_GIVEN_RANGE(this, target, 1)))
            return FALSE;

        // No alt clicking to view turf from beneath
        if (Dm.truthy(HAS_TRAIT(this, TRAIT_MOVE_VENTCRAWLING)))
            return FALSE;

        /// No loot panel if it's on our person
        if (isobj(target) && Dm.@in(target, get_all_gear(Dm.bitor(Dm.bitor(INCLUDE_PROSTHETICS, INCLUDE_ABSTRACT), INCLUDE_ACCESSORIES))))
        {
            to_chat(this, span_warning(I18n.T("You can't search for this item, it's already in your inventory![]", !Dm.truthy(HAS_TRAIT(target, TRAIT_NODROP)) ? I18n.T(" Take it off first.") : null)));
            return FALSE;
        }

        await client.loot_panel.open(get_turf(target));
        return TRUE;
    }
}
