// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:4
public partial class obj_item_cain_and_abel : obj_item
{
    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:1-2 (file-local #defines, read by dagger_throw.dm too)
    internal const string THROW_MODE_CRYSTALS = "throw_mode_crystals";
    internal const string THROW_MODE_LAUNCH = "throw_mode_launch";

    /// Animation positions used by wisps
    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:36 (var/static/list/animation_steps)
    public static DmList animation_steps
    {
        get => TgState.Current.obj_item_cain_and_abel__animation_steps;
        set => TgState.Current.obj_item_cain_and_abel__animation_steps = value;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(length(animation_steps)))
            return dot;

        animation_steps = Dm.list(
            Dm.@new<datum_abel_wisp_frame>(-18, -2, MOB_LAYER, EASE_OUT, EASE_IN),
            Dm.@new<datum_abel_wisp_frame>(-6, -6, ABOVE_MOB_LAYER, EASE_IN, EASE_OUT),
            Dm.@new<datum_abel_wisp_frame>(6, -6, ABOVE_MOB_LAYER, EASE_IN, EASE_OUT),
            Dm.@new<datum_abel_wisp_frame>(18, -2, MOB_LAYER, EASE_OUT, EASE_IN),
            Dm.@new<datum_abel_wisp_frame>(6, 2, BELOW_MOB_LAYER, EASE_IN, EASE_OUT),
            Dm.@new<datum_abel_wisp_frame>(-6, 2, BELOW_MOB_LAYER, EASE_IN, EASE_OUT));
        return dot;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:58
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (isliving(old_loc))
            unset_user(old_loc as mob_living);
        return dot;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:63
    public virtual void unset_user(mob_living source = null)
    {
        if (HAS_TRAIT(source, TRAIT_RELAYING_ATTACKER))
            source.RemoveElement(typeof(datum_element_relay_attackers));

        set_combo(new_value: 0, user: source);
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_WAS_ATTACKED, COMSIG_MOB_UPDATE_HELD_ITEMS));
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:70
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)))
        {
            unset_user(user as mob_living);
            return;
        }

        if (!HAS_TRAIT(user, TRAIT_RELAYING_ATTACKER))
            await user.AddElement(typeof(datum_element_relay_attackers));

        RegisterSignal(user, COMSIG_ATOM_WAS_ATTACKED, nameof(on_attacked));
        RegisterSignal(user, COMSIG_MOB_UPDATE_HELD_ITEMS, nameof(on_updated_held_items));
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:82
    [SignalHandler]
    public virtual void on_attacked(datum source = null, atom attacker = null, object attack_flags = null)
    {
        set_combo(new_value: 0, user: source as mob_living);
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:86
    public override async DmTask<object> ranged_interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.num(get_dist(interacting_with, user)) > 9 || interacting_with.z != user.z)
            return NONE;

        if (!Dm.truthy(check_wield(user)))
        {
            user.balloon_alert(user, "offhand busy!");
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(length(current_wisps)))
        {
            user.balloon_alert(user, "no wisps!");
            return ITEM_INTERACT_BLOCKING;
        }

        for (float index = 0, index__end = Dm.num(length(current_wisps)) - 1; index <= index__end; index++)
            _addtimer(Dm.@new<datum_callback>(this, nameof(fire_wisp), user, interacting_with), index * 0.15f * SECONDS, file: "code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm", line: 101);
        await DmTask.CompletedTask;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:102
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.pre_attack(target, user, modifiers, attack_modifiers);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(check_wield(user)))
        {
            user.balloon_alert(user, "offhand busy!");
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:110
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!(target is mob_living) || Dm.num(target.mob_size) < MOB_SIZE_LARGE || target.stat == DEAD)
        {
            attack_speed = CLICK_CD_MELEE;
            return await base.attack__proc(target, user, modifiers, attack_modifiers, __extra);
        }

        attack_speed = Dm.initial(this, (obj_item p) => p.attack_speed);
        float old_force = force;
        float bonus_value = Dm.truthy(combo_count) ? combo_count : 1;
        force = ceil((bonus_value * damage_boost) * force);
        object dot = await base.attack__proc(target, user, modifiers, attack_modifiers, __extra);
        force = old_force;
        set_combo(new_value: combo_count + 1, user: user);
        return dot;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:123
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return TRUE;
        throw_mode = (throw_mode == THROW_MODE_CRYSTALS) ? THROW_MODE_LAUNCH : THROW_MODE_CRYSTALS;
        user.balloon_alert(user, Dm.text("crystals []", throw_mode == THROW_MODE_CRYSTALS ? "activated" : "deactivated"));
        return TRUE;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:131
    public virtual void set_combo(object new_value = null, mob_living user = null, float? instant = null)
    {
        instant ??= FALSE;
        float new_combo = clamp(Dm.num(new_value), 0, max_combo);
        if (new_combo == combo_count)
            return;

        if (new_combo > combo_count)
        {
            for (float i = 1, i__end = new_combo - combo_count; i <= i__end; i++)
                add_wisp(user);
        }
        else
        {
            for (float i = 1, i__end = combo_count - new_combo; i <= i__end; i++)
                remove_wisp(current_wisps[i] as obj, instant);
        }

        combo_count = new_combo;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:145
    [SignalHandler]
    public virtual void on_updated_held_items(mob_living source = null)
    {
        update_dagger_icon();
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:149
    public virtual void update_dagger_icon()
    {
        inhand_icon_state = Dm.text("[][]", Dm.initial(this, (obj_item p) => p.inhand_icon_state), (Dm.truthy(dagger_thrown) || !Dm.truthy(check_wield(loc as mob_living))) ? "_thrown" : "");
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:152
    public virtual void add_wisp(mob_living user = null)
    {
        obj_effect_overlay_blood_wisp new_wisp = Dm.@new<obj_effect_overlay_blood_wisp>(this);
        Dm.append(current_wisps, new_wisp);
        Dm.append(user.vis_contents, new_wisp);
        RegisterSignal(new_wisp, COMSIG_QDELETING, nameof(on_wisp_delete));
        for (float wisp_index = 1, wisp_index__end = Dm.num(length(current_wisps)); wisp_index <= wisp_index__end; wisp_index++)
        {
            obj_effect_overlay_blood_wisp wisp = current_wisps[wisp_index] as obj_effect_overlay_blood_wisp;
            float spawn_index = floor(Dm.num(length(animation_steps)) / Dm.num(length(current_wisps)) * wisp_index);
            datum_abel_wisp_frame spawn_position = animation_steps[spawn_index] as datum_abel_wisp_frame;

            // Latest added wisp is teleported to its spawn position, others animate from their current position
            if (wisp_index == Dm.num(length(current_wisps)))
            {
                wisp.pixel_w = Dm.num(spawn_position.x);
                wisp.pixel_z = Dm.num(spawn_position.y);
                wisp.layer = Dm.num(spawn_position.layer);
            }
            else
            {
                animate(wisp, Dm.named("tag", "wisp_anim_x"));
                animate(wisp, Dm.named("tag", "wisp_anim_y"));
            }

            float start_index = Dm.mod(spawn_index, Dm.num(length(animation_steps))) + 1;
            datum_abel_wisp_frame position = animation_steps[start_index] as datum_abel_wisp_frame;
            animate(wisp, Dm.named("pixel_w", position.x), Dm.named("layer", position.layer), Dm.named("time", 0.6f * SECONDS), Dm.named("loop", -1), Dm.named("tag", "wisp_anim_x"));
            // We need to animate x and y coordinates separately as they have different easing steps
            for (float frame_index = 1, frame_index__end = Dm.num(length(animation_steps)) - 1; frame_index <= frame_index__end; frame_index++)
            {
                // Get actual index starting from our *next* animation step, not initial position
                float anim_index = Dm.mod(start_index + frame_index - 1, Dm.num(length(animation_steps))) + 1;
                position = animation_steps[anim_index] as datum_abel_wisp_frame;
                animate(Dm.named("time", 0.6f * SECONDS), Dm.named("pixel_w", position.x), Dm.named("layer", position.layer), Dm.named("easing", Dm.bitor(SINE_EASING, position.x_easing)));
            }

            animate(wisp, Dm.named("time", 0.6f * SECONDS), Dm.named("pixel_z", position.y), Dm.named("loop", -1), Dm.named("tag", "wisp_anim_y"));
            for (float frame_index = 1, frame_index__end = Dm.num(length(animation_steps)) - 1; frame_index <= frame_index__end; frame_index++)
            {
                // Get actual index starting from our *next* animation step, not initial position
                float anim_index = Dm.mod(start_index + frame_index - 1, Dm.num(length(animation_steps))) + 1;
                position = animation_steps[anim_index] as datum_abel_wisp_frame;
                animate(Dm.named("time", 0.6f * SECONDS), Dm.named("pixel_z", position.y), Dm.named("easing", Dm.bitor(SINE_EASING, position.y_easing)));
            }
        }
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:188
    [SignalHandler]
    public virtual void on_wisp_delete(datum source = null)
    {
        Dm.remove(current_wisps, source);
        UnregisterSignal(source, COMSIG_QDELETING);
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:193
    public virtual async DmTask fire_wisp(atom user = null, atom target = null)
    {
        await user.fire_projectile(typeof(obj_projectile_dagger_wisp), target);
        set_combo(combo_count - 1, user as mob_living, TRUE);
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:197
    public virtual void remove_wisp(obj wisp_to_remove = null, float? instant = null)
    {
        instant ??= FALSE;
        if (Dm.truthy(instant))
        {
            qdel(wisp_to_remove);
            return;
        }
        animate(wisp_to_remove, Dm.named("alpha", 0), Dm.named("time", 0.2f * SECONDS));
        // QDEL_IN(wisp_to_remove, 0.2 SECONDS) (__DEFINES/qdel.dm:59), inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 0.2f * SECONDS > GC_FILTER_QUEUE ? WEAKREF(wisp_to_remove) : wisp_to_remove), 0.2f * SECONDS, file: "code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm", line: 205);
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:204
    public virtual object check_wield(mob_living user = null)
    {
        if (!(user is mob_living))
            return TRUE;
        bool active_item = this == user.get_active_held_item();
        obj_item other_held = (active_item ? user.get_inactive_held_item() : user.get_active_held_item()) as obj_item;
        if (other_held != null)
            return FALSE;
        obj_item_bodypart hand = (active_item ? user.get_inactive_hand() : user.get_active_hand()) as obj_item_bodypart;
        if (hand == null || Dm.truthy(hand.bodypart_disabled))
            return FALSE;
        return TRUE;
    }
}

/// Frame data for cain & abel wisps so we don't have to make list monstrocities
// tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:217
public partial class datum_abel_wisp_frame : datum
{
    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:224
    [DmParams("x", "y", "layer", "x_easing", "y_easing")]
    public override async DmTask New(params object[] __extra)
    {
        object x = Dm.arg(__extra, 0);
        object y = Dm.arg(__extra, 1);
        object layer = Dm.arg(__extra, 2);
        object x_easing = Dm.arg(__extra, 3);
        object y_easing = Dm.arg(__extra, 4);
        await base.New(__extra);
        this.x = Dm.num(x);
        this.y = Dm.num(y);
        this.layer = Dm.num(layer);
        this.x_easing = Dm.num(x_easing);
        this.y_easing = Dm.num(y_easing);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/lavaland/cain_and_abel/_cain_and_abel.dm:36 (var/static/list/animation_steps)
    internal DmList obj_item_cain_and_abel__animation_steps;
}
