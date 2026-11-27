// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/objects_and_mobs/cursed_slot_machine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: greed's slot machine (each pull curses, one in twenty pays) and its bag of money with the die of fate. The var
// blocks are generated.

/// Greed's slot machine: Used in the Greed ruin. Deals damage on each use, with a successful use giving a d20 of fate.
// tg: code/modules/mapfluff/ruins/objects_and_mobs/cursed_slot_machine.dm:2
public partial class obj_structure_cursed_slot_machine : obj_structure
{
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/cursed_slot_machine.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        Waiting.NoSleep(set_light(brightness_on));
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/cursed_slot_machine.dm:33
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (!ishuman(user))
            return null;

        if (!Dm.truthy(check_and_set_usage((mob_living_carbon_human)user)))
            return null;

        user.visible_message(
            span_warning(I18n.T("[] pulls []'s lever with a glint in [] eyes!", user, this, user.p_their())),
            span_warning(I18n.T("You feel a draining as you pull the lever, but you know it'll be worth it.")));

        icon_screen = "slots_screen_working";
        update_appearance();
        playsound(this, Dm.resource("sound/machines/lavaland/cursed_slot_machine.ogg"), 50, FALSE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(determine_victor), user), 5 * SECONDS, file: "code/modules/mapfluff/ruins/objects_and_mobs/cursed_slot_machine.dm", line: 49);
        return null;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/cursed_slot_machine.dm:51
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        string overlay_state = icon_screen;
        Dm.append(ref dot, mutable_appearance(icon, overlay_state));
        // . += emissive_appearance(icon, overlay_state, src): the screen's glow on the emissive plane (DEVIATION DV12)
        return dot;
    }

    /// Validates that the user can use the cursed slot machine. User is the person using the slot machine. Returns TRUE if we can, FALSE otherwise.
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/cursed_slot_machine.dm:58
    public virtual object check_and_set_usage(mob_living_carbon_human user = null)
    {
        if (Dm.truthy(in_use))
        {
            balloon_alert_to_viewers(I18n.T("already spinning!"));
            return FALSE;
        }

        object signal_value = !Dm.truthy(user._listen_lookup?[COMSIG_CURSED_SLOT_MACHINE_USE]) ? NONE : user._SendSignal(COMSIG_CURSED_SLOT_MACHINE_USE, Dm.list(user, max_curse_amount));

        if (!(spin_cooldown <= world.time) || Dm.truthy(Dm.bitand(signal_value, SLOT_MACHINE_USE_POSTPONE))) // COOLDOWN_FINISHED(src, spin_cooldown)
        {
            to_chat(user, span_danger(I18n.T("The machine doesn't engage. You get the compulsion to try again in a few seconds.")));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(signal_value, SLOT_MACHINE_USE_CANCEL))) // failsafe in case we don't want to let the machine be used for some reason (like if we're maxed out on curses but not getting gibbed)
        {
            Waiting.NoSleep(say(I18n.T("We're sorry, but we can no longer serve you at this establishment.")));
            return FALSE;
        }

        in_use = TRUE;
        return TRUE;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/cursed_slot_machine.dm:80
    public virtual async DmTask determine_victor(mob_living_carbon_human user = null)
    {
        icon_screen = Dm.initial(this, (obj_structure_cursed_slot_machine m) => m.icon_screen);
        update_appearance();

        in_use = FALSE;
        spin_cooldown = world.time + cooldown_length; // COOLDOWN_START(src, spin_cooldown, cooldown_length)

        if (!prob(win_prob))
        {
            if (Dm.truthy(status_effect_on_roll))
                await user.apply_status_effect(typeof(datum_status_effect_slot_machine_curse));

            if (Dm.truthy(user._listen_lookup?[COMSIG_CURSED_SLOT_MACHINE_LOST]))
                user._SendSignal(COMSIG_CURSED_SLOT_MACHINE_LOST, Dm.list(user));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 30, TRUE);
            balloon_alert_to_viewers(I18n.T("you lost!"));
            return;
        }

        playsound(this, Dm.resource("sound/machines/lavaland/cursed_slot_machine_jackpot.ogg"), 50, FALSE);
        Dm.@new(prize, get_turf(this));
        if (user != null)
            to_chat(user, span_boldwarning(I18n.T("You've hit the jackpot!!! Laughter echoes around you as your reward appears in the machine's place.")));

        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_CURSED_SLOT_MACHINE_WON]))
            SSdcs._SendSignal(COMSIG_GLOB_CURSED_SLOT_MACHINE_WON, Dm.list(SSdcs));
        qdel(this);
    }
}

/// Prize given out by the cursed slot machine that will give the user one Die of Fate and then delete itself.
// tg: code/modules/mapfluff/ruins/objects_and_mobs/cursed_slot_machine.dm:101
public partial class obj_structure_cursed_money : obj_structure
{
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/cursed_slot_machine.dm:109
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        _addtimer(Dm.@new<datum_callback>(this, nameof(collapse)), 1 * MINUTES, file: "code/modules/mapfluff/ruins/objects_and_mobs/cursed_slot_machine.dm", line: 111);
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/cursed_slot_machine.dm:113
    public virtual void collapse()
    {
        if (QDELETED(this))
            return;
        visible_message(span_warning(I18n.T("[] falls in on itself, with the canvas rotting away and contents vanishing.", this)));
        qdel(this);
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/cursed_slot_machine.dm:119
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        user.visible_message(
            span_warning(I18n.T("[] opens the bag and removes a die.", user)),
            span_warning(I18n.T("[] But all you see is a bag full of dice. Confused, you take one...", span_boldwarning(I18n.T("You open the bag...!")))));
        turf location = get_turf(user);
        var critical_fail = Dm.@new<obj_item_dice_d20_fate_one_use>(location);
        await user.put_in_hands(critical_fail);
        collapse();
        return dot;
    }
}
