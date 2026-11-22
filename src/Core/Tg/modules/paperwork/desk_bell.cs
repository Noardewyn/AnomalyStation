// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/desk_bell.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file but the screentips (gaps package C5: Pun Pun rings the bar's bell); attaching it to a wheelchair is a row, the
// wheelchair is inert.

// A receptionist's bell

// tg: code/modules/paperwork/desk_bell.dm:4
public partial class obj_structure_desk_bell : obj_structure
{
    // tg: code/modules/paperwork/desk_bell.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/modules/paperwork/desk_bell.dm:47
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!(ring_cooldown <= world.time) && Dm.truthy(ring_cooldown_length)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return TRUE;
        if (!Dm.truthy(await ring_bell(user as mob_living)))
            to_chat(user, span_notice(I18n.T("[] is silent. Some idiot broke it.", this)));
        if (Dm.truthy(ring_cooldown_length))
            ring_cooldown = world.time + ring_cooldown_length;
        return TRUE;
    }

    // tg: code/modules/paperwork/desk_bell.dm:57
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/modules/paperwork/desk_bell.dm:60
    public override async DmTask<object> attackby(obj_item weapon = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attackby(weapon, user, modifiers, attack_modifiers);
        times_rang += Dm.num(weapon.force);
        await ring_bell(user as mob_living);
        return dot;
    }

    // tg: code/modules/paperwork/desk_bell.dm:65
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        Waiting.NoSleep(ring_bell());
        return dot;
    }

    // Fix the clapper
    // tg: code/modules/paperwork/desk_bell.dm:70
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(broken_ringer))
        {
            balloon_alert(user, "repairing...");
            tool.play_tool_sound(this);
            if (Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS)))
            {
                balloon_alert_to_viewers("repaired");
                playsound(user, Dm.resource("sound/items/tools/change_drill.ogg"), 50, vary: TRUE);
                broken_ringer = FALSE;
                times_rang = 0;
                return ITEM_INTERACT_SUCCESS;
            }
            return FALSE;
        }
        return await base.screwdriver_act(user, tool);
    }

    // Deconstruct
    // tg: code/modules/paperwork/desk_bell.dm:84
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        balloon_alert(user, "taking apart...");
        tool.play_tool_sound(this);
        if (Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS)))
        {
            balloon_alert(user, "disassembled");
            playsound(user, Dm.resource("sound/items/deconstruct.ogg"), 50, vary: TRUE);
            if (!Dm.truthy(broken_ringer)) // Drop 2 if it's not broken.
                Dm.@new<obj_item_stack_sheet_iron>(drop_location());
            Dm.@new<obj_item_stack_sheet_iron>(drop_location());
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.wrench_act_secondary(user, tool);
    }

    /// Check if the clapper breaks, and if it does, break it
    // tg: code/modules/paperwork/desk_bell.dm:98
    public virtual void check_clapper(mob_living user = null)
    {
        if ((times_rang >= 10000 || prob(times_rang / 100)) && Dm.truthy(ring_cooldown_length))
        {
            if (user != null)
                to_chat(user, span_notice(I18n.T("You hear []'s clapper fall off of its hinge. Nice job, you broke it.", this)));
            else
                audible_message(span_notice(I18n.T("You hear []'s clapper fall off of its hinge. Nice job, you broke it.", this)));
            broken_ringer = TRUE;
        }
    }

    /// Ring the bell
    // tg: code/modules/paperwork/desk_bell.dm:107
    public virtual async DmTask<object> ring_bell(mob_living user = null)
    {
        if (Dm.truthy(broken_ringer))
            return FALSE;
        check_clapper(user);
        // The lack of varying is intentional. The only variance occurs on the strike the bell breaks.
        playsound(this, ring_sound, 70, vary: broken_ringer, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        flick("desk_bell_ring", this);
        times_rang++;
        return TRUE;
    }

    // tg: code/modules/paperwork/desk_bell.dm:117
    public override async DmTask<object> mouse_drop_dragged(atom over_object = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        if (over_object != user)
            return FALSE;
        obj_item_inhand_desk_bell held_bell = Dm.@new<obj_item_inhand_desk_bell>(user, this);
        await user.put_in_hands(held_bell, del_on_fail: FALSE);
        return null;
    }
}

/// Handheld bell
// tg: code/modules/paperwork/desk_bell.dm:131
public partial class obj_item_inhand_desk_bell : obj_item
{
    // tg: code/modules/paperwork/desk_bell.dm:140
    [DmParams("mapload", "bell")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_structure_desk_bell bell = Dm.arg<obj_structure_desk_bell>(__extra, 0);
        if (Dm.truthy(mapload))
            stack_trace("You shouldn't map in this item, use /obj/structure/desk_bell", "code/modules/paperwork/desk_bell.dm", 142);
        if (bell == null)
            return INITIALIZE_HINT_QDEL;
        // register_item_context(): the screentips (deferred, D291)
        this.bell = bell;
        Waiting.NoSleep(bell.forceMove(this));
        appearance = bell.appearance;
        RegisterSignals(bell, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED), nameof(on_bell_gone));
        return base.Initialize(mapload, __extra);
    }

    /// If we don't have a bell we're nothing
    // tg: code/modules/paperwork/desk_bell.dm:153
    public virtual void on_bell_gone()
    {
        UnregisterSignal(bell, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED));
        bell = null;
        qdel(this);
    }

    // tg: code/modules/paperwork/desk_bell.dm:167
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        return await bell.examine(user);
    }

    // tg: code/modules/paperwork/desk_bell.dm:170
    public override async DmTask<object> on_thrown(mob_living_carbon user = null, atom target = null)
    {
        obj throwing_bell = bell; // We are about to null this variable but also want to return it
        await bell.forceMove(user.drop_location() as atom);
        return throwing_bell;
    }

    // tg: code/modules/paperwork/desk_bell.dm:175
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (isturf(loc))
            await bell.forceMove(loc);
        return dot;
    }

    // tg: code/modules/paperwork/desk_bell.dm:180
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        await bell.ring_bell(user);
        return dot;
    }

    // tg: code/modules/paperwork/desk_bell.dm:186
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        await bell.ring_bell(user as mob_living);
        return dot;
    }

    // tg: code/modules/paperwork/desk_bell.dm:192
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        await base.interact_with_atom(interacting_with, user, modifiers);
        if (interacting_with is not obj_vehicle_ridden_wheelchair)
            return NONE;
        Dm.Deferred("code/modules/paperwork/desk_bell.dm:196", "interact_with_atom: attaching the bell to a wheelchair (the wheelchair is inert)");
        return ITEM_INTERACT_FAILURE;
    }
}
