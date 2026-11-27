// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/charged/_charged.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the chapel: the psyker's booster stands on it): the channelled spells and the beam spells' base. The var blocks are
// generated (D111).

/**
 * ## Channelled spells
 *
 * These spells do something after a channel time.
 * To use this template, all that's needed is for cast() to be implemented.
 */
// tg: code/modules/spells/spell_types/charged/_charged.dm:7
public partial class datum_action_cooldown_spell_charged : datum_action_cooldown_spell
{
    // tg: code/modules/spells/spell_types/charged/_charged.dm:33
    [DmParams("Target", "original")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!Dm.truthy(channel_message))
            channel_message = span_notice(I18n.T("You start chanelling []...", this));

        if (Dm.truthy(charge_sound))
            charge_sound_instance = sound(charge_sound, channel: CHANNEL_CHARGED_SPELL);

        if (Dm.truthy(charge_overlay_icon) && Dm.truthy(charge_overlay_state))
            charge_overlay_instance = mutable_appearance(charge_overlay_icon, (string)charge_overlay_state, EFFECTS_LAYER) as mutable_appearance;
    }

    // tg: code/modules/spells/spell_types/charged/_charged.dm:44
    public override object Destroy(float? force = null)
    {
        if (owner != null)
            Waiting.NoSleep(stop_channel_effect(owner));

        charge_overlay_instance = null;
        charge_sound_instance = null;
        return base.Destroy(force);
    }

    // tg: code/modules/spells/spell_types/charged/_charged.dm:52
    public override async DmTask Remove(mob removed_from = null)
    {
        await stop_channel_effect(removed_from);
        await base.Remove(removed_from);
    }

    // tg: code/modules/spells/spell_types/charged/_charged.dm:56
    public override object is_action_active(atom_movable_screen_movable_action_button current_button = null)
    {
        return currently_channeling;
    }

    // tg: code/modules/spells/spell_types/charged/_charged.dm:59
    public override async DmTask<object> can_cast_spell(float? feedback = null)
    {
        feedback ??= TRUE;
        object dot = null;
        dot = await base.can_cast_spell(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (Dm.truthy(currently_channeling))
        {
            if (Dm.truthy(feedback))
                to_chat(owner, span_warning(I18n.T("You're already channeling []!", this)));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/spells/spell_types/charged/_charged.dm:70
    public override async DmTask<object> before_cast(atom cast_on = null)
    {
        object dot = null;
        dot = await base.before_cast(cast_on);
        if (Dm.truthy(Dm.bitand(dot, SPELL_CANCEL_CAST)))
            return dot;

        to_chat(cast_on, channel_message);

        if (charge_sound_instance != null)
            playsound(cast_on, charge_sound_instance, 50, FALSE);

        if (charge_overlay_instance != null)
            cast_on.add_overlay(charge_overlay_instance);

        currently_channeling = TRUE;
        await build_all_button_icons(UPDATE_BUTTON_STATUS);
        if (!Dm.truthy(await do_after(cast_on as atom_movable, channel_time, timed_action_flags: channel_flags)))
        {
            await stop_channel_effect(cast_on as mob);
            return Dm.bitor(dot, SPELL_CANCEL_CAST);
        }
        return dot;
    }

    // tg: code/modules/spells/spell_types/charged/_charged.dm:89
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = null;
        dot = await base.cast(cast_on);
        await stop_channel_effect(cast_on as mob);
        return dot;
    }

    /// Interrupts the chanelling effect, removing any overlay or sound playing (for the passed mob)
    // tg: code/modules/spells/spell_types/charged/_charged.dm:94
    public virtual async DmTask stop_channel_effect(mob for_who = null)
    {
        if (charge_overlay_instance != null)
            for_who.cut_overlay(charge_overlay_instance);

        if (charge_sound_instance != null)
        {
            for_who.stop_sound_channel(CHANNEL_CHARGED_SPELL);
            // Play a null sound in to cancel the sound playing, because byond
            playsound(for_who, sound(null, repeat: 0, channel: CHANNEL_CHARGED_SPELL), 50, FALSE);
        }

        currently_channeling = FALSE;
        await build_all_button_icons(UPDATE_BUTTON_STATUS);
    }
}

/**
 * ### Channelled "Beam" spells
 *
 * Channelled spells that pick a random target from nearby atoms to cast a spell on.
 * Commonly used for beams, hence the name, but nothing's stopping projectiles or whatever from working.
 *
 * If no targets are nearby, cancels the spell and refunds the cooldown.
 */
// tg: code/modules/spells/spell_types/charged/_charged.dm:114
public partial class datum_action_cooldown_spell_charged_beam : datum_action_cooldown_spell_charged
{
    // tg: code/modules/spells/spell_types/charged/_charged.dm:122
    public override object Destroy(float? force = null)
    {
        initial_target = null; // This like shouuld never hang references but I've seen some cursed things so let's be safe
        return base.Destroy(force);
    }

    // tg: code/modules/spells/spell_types/charged/_charged.dm:126
    public override async DmTask<object> before_cast(atom cast_on = null)
    {
        object dot = null;
        dot = await base.before_cast(cast_on);
        if (Dm.truthy(Dm.bitand(dot, SPELL_CANCEL_CAST)))
            return dot;

        initial_target = await get_target(cast_on) as atom;
        if (initial_target == null)
        {
            cast_on.balloon_alert(cast_on as mob, I18n.T("no targets nearby!"));
            await stop_channel_effect(cast_on as mob);
            return Dm.bitor(dot, SPELL_CANCEL_CAST);
        }
        return dot;
    }

    // tg: code/modules/spells/spell_types/charged/_charged.dm:137
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = null;
        dot = await base.cast(cast_on);
        await send_beam(cast_on, initial_target, max_beam_bounces);
        initial_target = null;
        return dot;
    }

    // tg: code/modules/spells/spell_types/charged/_charged.dm:142
    public virtual async DmTask send_beam(atom origin = null, atom to_beam = null, object bounces = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        throw Dm.CRASH(Dm.text("[] did not implement send_beam and either has no effects or implemented the spell incorrectly.", type), "code/modules/spells/spell_types/charged/_charged.dm", 144);
    }

    // tg: code/modules/spells/spell_types/charged/_charged.dm:146
    public virtual async DmTask<object> get_target(atom center = null)
    {
        DmList things = Dm.list();
        foreach (atom nearby_thing in Dm.each<atom>(range(target_radius, center)))
        {
            if (nearby_thing == owner || nearby_thing == center)
                continue;

            Dm.append(ref things, nearby_thing);
        }

        if (!Dm.truthy(length(things)))
            return null;

        await DmTask.CompletedTask;
        return pick(things);
    }
}
