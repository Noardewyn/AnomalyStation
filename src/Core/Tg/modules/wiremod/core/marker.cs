// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/core/marker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, the circuit multitool: it marks an entity (a mob's visible items through a radial
// menu) for an entity port's upload button, with its marked overlay, its flick and its sound. The var block is generated (D111).

// tg: code/modules/wiremod/core/marker.dm:1
public partial class obj_item_multitool_circuit : obj_item_multitool
{
    // tg: code/modules/wiremod/core/marker.dm:12
    public override object Destroy(float? force = null)
    {
        marked_atom = null;
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/core/marker.dm:16
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(marked_atom != null ? I18n.T("It has a marked entity registered.") : I18n.T("It has no marked entity registered.")));
        return dot;
    }

    // tg: code/modules/wiremod/core/marker.dm:20
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (marked_atom == null)
            return dot;

        await say(I18n.T("Cleared marked targets."));
        await clear_marked_atom();
        return TRUE;
    }

    // tg: code/modules/wiremod/core/marker.dm:31
    public override async DmTask<object> melee_attack_chain(mob user = null, atom target = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object is_right_clicking = LAZYACCESS(modifiers, RIGHT_CLICK);

        if (marked_atom != null || !Dm.truthy(await user.Adjacent(target)) || Dm.truthy(is_right_clicking))
            return await base.melee_attack_chain(user, target, modifiers, attack_modifiers);

        if (isliving(target))
        {
            Waiting.Detach(mark_mob_or_contents(user, target as mob_living));
            return TRUE;
        }

        await mark_target(target);
        return null;
    }

    // tg: code/modules/wiremod/core/marker.dm:43
    public virtual async DmTask<object> mark_target(atom target = null)
    {
        await say(I18n.T("Marked [].", target));
        marked_atom = target;
        RegisterSignal(marked_atom, COMSIG_QDELETING, nameof(cleanup_marked_atom));
        await update_icon();
        Dm.flick("multitool_circuit_flick", this);
        playsound(loc, "sound/machines/compiler/compiler-stage2.ogg", 30, TRUE);
        return TRUE;
    }

    /// Allow users to mark items equipped by the target that are visible.
    // tg: code/modules/wiremod/core/marker.dm:53
    public virtual async DmTask mark_mob_or_contents(mob user = null, mob_living target = null)
    {
        DmList visible_items;
        mob_living_carbon carbon_target = null;
        if (iscarbon(target))
        {
            carbon_target = target as mob_living_carbon;
            visible_items = carbon_target.get_visible_items() as DmList;
        }
        else
            visible_items = target.get_equipped_items(INCLUDE_HELD) as DmList;

        Dm.remove(visible_items, this); // the multitool cannot mark itself.

        if (!Dm.truthy(length(visible_items)))
        {
            await mark_target(target);
            return;
        }

        DmList selectable_targets = Dm.list();
        datum_radial_menu_choice mob_choice = Dm.@new<datum_radial_menu_choice>();
        mob_choice.image = Dm.image(icon: "icons/hud/radial.dmi", icon_state: "radial_mob");
        mob_choice.name = target.name;
        selectable_targets[REF(target)] = mob_choice;
        foreach (obj_item item in Dm.each_cast<obj_item>(visible_items))
        {
            //no revealing items that are not obscured but meant to be hidden
            if (HAS_TRAIT(item, TRAIT_NO_STRIP) || HAS_TRAIT(item, TRAIT_EXAMINE_SKIP))
                continue;

            datum_radial_menu_choice item_choice = Dm.@new<datum_radial_menu_choice>();

            mutable_appearance item_appearance = Dm.@new<mutable_appearance>(item);
            item_appearance.layer = FLOAT_LAYER;
            item_appearance.plane = FLOAT_PLANE;

            item_choice.name = item.name;
            item_choice.image = item_appearance;
            selectable_targets[REF(item)] = item_choice;
        }

        object picked_ref = await show_radial_menu(user, this, selectable_targets, radius: 38, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user, target), tooltips: TRUE);
        if (!Dm.truthy(picked_ref))
            return;

        atom_movable chosen = Dm.locate_ref(picked_ref) as atom_movable;
        if (chosen == target || Dm.@in(chosen, carbon_target != null ? carbon_target.get_visible_items() : target.get_equipped_items(INCLUDE_HELD)))
            await mark_target(chosen);
        else
            balloon_alert(user, I18n.T("cannot mark entity"));
    }

    // tg: code/modules/wiremod/core/marker.dm:98
    public virtual async DmTask<object> check_menu(mob user = null, mob_living target = null)
    {
        return Dm.num(marked_atom == null && Dm.truthy(user.is_holding(this)) && Dm.truthy(await user.Adjacent(target)));
    }

    // tg: code/modules/wiremod/core/marker.dm:101
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        cut_overlays();
        if (marked_atom != null)
            Dm.append(ref dot, "marked_overlay");
        return dot;
    }

    /// Clears the current marked atom
    // tg: code/modules/wiremod/core/marker.dm:108
    public virtual async DmTask clear_marked_atom()
    {
        if (marked_atom == null)
            return;
        UnregisterSignal(marked_atom, COMSIG_QDELETING);
        marked_atom = null;
        await update_icon();
    }

    // tg: code/modules/wiremod/core/marker.dm:115
    [SignalHandler]
    public virtual void cleanup_marked_atom(datum source = null)
    {
        if (source == marked_atom)
            Waiting.NoSleep(clear_marked_atom());
    }
}
