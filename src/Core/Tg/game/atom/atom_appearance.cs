// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/atom_appearance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (package B, wave 4: /area/Initialize reaches update_appearance through LateInitialize and
// power_change, areas.dm:225, :521). Only the reads of an overlay's appearance wait for the presentation work.

// tg: code/game/atom/atom_appearance.dm:1
public partial class atom : datum
{
    ///overlays managed by [update_overlays][/atom/proc/update_overlays] to prevent removing overlays that weren't added by the same proc. Single items are stored on their own, not in a list.
    // tg: code/game/atom/atom_appearance.dm:3
    // (object, not tg's declared list: a single overlay is stored on its own, :93; rules 3.2, typed vars that hold wider
    // values)
    public object managed_overlays;
    /// The icon state that will be switched to during initialization.
    /// Mostly intended for things that have a special map icon.
    // tg: code/game/atom/atom_appearance.dm:6
    public object post_init_icon_state;

    /**
     * Updates the appearence of the icon
     *
     * Mostly delegates to update_name, update_desc, and update_icon
     *
     * Arguments:
     * - updates: A set of bitflags dictating what should be updated. Defaults to [ALL]
     */
    // tg: code/game/atom/atom_appearance.dm:16
    [ShouldNotSleep, ShouldCallParent]
    public virtual object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = null;

        dot = NONE;
        Dm.mask(ref updates, Dm.bitnot((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_APPEARANCE]) ? NONE : this._SendSignal(COMSIG_ATOM_UPDATE_APPEARANCE, Dm.list(this, updates)))));
        if (Dm.truthy(Dm.bitand(updates, UPDATE_NAME)))
            Dm.combine(ref dot, update_name(updates));
        if (Dm.truthy(Dm.bitand(updates, UPDATE_DESC)))
            Dm.combine(ref dot, update_desc(updates));
        if (Dm.truthy(Dm.bitand(updates, UPDATE_ICON)))
            Dm.combine(ref dot, Waiting.NoSleep(update_icon(updates)));
        return dot;
    }

    /// Updates the name of the atom
    // tg: code/game/atom/atom_appearance.dm:30
    [ShouldCallParent]
    public virtual object update_name(float? updates = null)
    {
        updates ??= ALL;
        return (!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_NAME]) ? NONE : this._SendSignal(COMSIG_ATOM_UPDATE_NAME, Dm.list(this, updates)));
    }

    /// Updates the description of the atom
    // tg: code/game/atom/atom_appearance.dm:35
    [ShouldCallParent]
    public virtual object update_desc(float? updates = null)
    {
        updates ??= ALL;
        return (!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_DESC]) ? NONE : this._SendSignal(COMSIG_ATOM_UPDATE_DESC, Dm.list(this, updates)));
    }

    /// Updates the icon of the atom
    // tg: code/game/atom/atom_appearance.dm:40
    [ShouldCallParent]
    public virtual async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        updates ??= ALL;
        object dot = null;

        dot = NONE;
        Dm.mask(ref updates, Dm.bitnot((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_ICON]) ? NONE : this._SendSignal(COMSIG_ATOM_UPDATE_ICON, Dm.list(this, updates)))));
        if (Dm.truthy(Dm.bitand(updates, UPDATE_ICON_STATE)))
        {
            await update_icon_state();
            Dm.combine(ref dot, UPDATE_ICON_STATE);
        }

        if (Dm.truthy(Dm.bitand(updates, UPDATE_OVERLAYS)))
        {
            if (Dm.truthy(LAZYLEN(managed_vis_overlays)))
                SSvis_overlays.remove_vis_overlay(this as atom_movable, managed_vis_overlays);

            DmList new_overlays = await update_overlays(updates) as DmList;
            float nulls = 0;
            for (float i = 1, i__end = length(new_overlays); i <= i__end; i++)
            {
                // (object, not tg's declared atom: the overlays are text, icons and appearances, rules 3.4)
                object maybe_not_an_atom = new_overlays[i];
                if (maybe_not_an_atom == null)
                {
                    nulls++;
                    continue;
                }
                if (istext(maybe_not_an_atom) || isicon(maybe_not_an_atom))
                    continue;
                // BYOND's appearance var of an atom, an image or a mutable appearance is the overlay snapshot of it (M2 P3,
                // DmAppearance.Of; dm-conformance.md "M2 P3").
                new_overlays[i] = DmAppearance.Of((IDmAppearance)maybe_not_an_atom);
            }
            if (Dm.truthy(nulls))
            {
                for (float i = 1, i__end = nulls; i <= i__end; i++)
                    Dm.remove(ref new_overlays, null);
            }

            float identical = FALSE;
            float new_length = length(new_overlays);
            if (!Dm.truthy(managed_overlays) && !Dm.truthy(new_length))
                identical = TRUE;
            else if (!islist(managed_overlays))
            {
                if (new_length == 1 && Dm.eq(managed_overlays, new_overlays[1]))
                    identical = TRUE;
            }
            else if (length(managed_overlays) == new_length)
            {
                identical = TRUE;
                for (float i = 1, i__end = length(managed_overlays); i <= i__end; i++)
                {
                    if (!Dm.eq(((DmList)managed_overlays)[i], new_overlays[i]))
                    {
                        identical = FALSE;
                        break;
                    }
                }
            }

            if (!Dm.truthy(identical))
            {
                float full_control = FALSE;
                if (Dm.truthy(managed_overlays))
                {
                    full_control = Dm.num(length(overlays) == (islist(managed_overlays) ? length(managed_overlays) : 1));
                    if (Dm.truthy(full_control))
                        overlays = null;
                    else
                        cut_overlay(managed_overlays);
                }

                switch (length(new_overlays))
                {
                    case 0:
                        if (Dm.truthy(full_control))
                        {
                            // POST_OVERLAY_CHANGE(src) (__DEFINES/overlays.dm:17), expanded inline (rules 11.1 (d): it reads
                            // src's alternate_appearances)
                            if (Dm.truthy(alternate_appearances))
                                foreach (object I in Dm.each(alternate_appearances.Copy())) // POST_OVERLAY_CHANGE (M8 slice G: the alternate appearances are translated)
                                {
                                    datum_atom_hud_alternate_appearance AA = alternate_appearances[I] as datum_atom_hud_alternate_appearance;
                                    if (Dm.truthy(AA.transfer_overlays))
                                        AA.copy_overlays(this);
                                }
                        }
                        managed_overlays = null;
                        break;
                    case 1:
                        add_overlay(new_overlays);
                        managed_overlays = new_overlays[1];
                        break;
                    default:
                        add_overlay(new_overlays);
                        managed_overlays = new_overlays;
                        break;
                }
            }

            Dm.combine(ref dot, UPDATE_OVERLAYS);
        }

        Dm.combine(ref dot, (!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATED_ICON]) ? NONE : this._SendSignal(COMSIG_ATOM_UPDATED_ICON, Dm.list(this, updates, dot))));
        return dot;
    }

    /// Updates the icon state of the atom
    // tg: code/game/atom/atom_appearance.dm:107
    [ShouldCallParent]
    public virtual async DmTask<object> update_icon_state(params object[] __extra)
    {
        return (!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_ICON_STATE]) ? NONE : this._SendSignal(COMSIG_ATOM_UPDATE_ICON_STATE, Dm.list(this)));
    }

    /// Updates the overlays of the atom
    // tg: code/game/atom/atom_appearance.dm:112
    [ShouldCallParent]
    public virtual async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = null;
        dot = Dm.list();
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_OVERLAYS]))
            this._SendSignal(COMSIG_ATOM_UPDATE_OVERLAYS, Dm.list(this, dot));
        return dot;
    }
}
