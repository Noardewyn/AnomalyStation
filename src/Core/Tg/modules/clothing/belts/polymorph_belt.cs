// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/belts/polymorph_belt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the polymorphic field inverter. A bioscrambler core switches it on; hitting a simple organic animal with it scans
// that animal's kind (five seconds), and then the belt gives its wearer the shapeshift into that animal, with a three second
// channel that shakes and shrinks them before the change. The shapeshift's status effect follows the animal when it grows up
// into another kind. The var blocks are generated (D111).

// tg: code/modules/clothing/belts/polymorph_belt.dm:2
public partial class obj_item_polymorph_belt : obj_item
{
    // tg: code/modules/clothing/belts/polymorph_belt.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        return dot;
    }

    // tg: code/modules/clothing/belts/polymorph_belt.dm:23
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref transform_action);
        return base.Destroy(force);
    }

    // tg: code/modules/clothing/belts/polymorph_belt.dm:27
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(stored_mob_type))
            Dm.append(ref dot, span_notice(I18n.T("It contains digitised [] DNA.", Dm.initial((Type)stored_mob_type, (mob_living p) => p.name))));
        if (!Dm.truthy(active))
            Dm.append(ref dot, span_warning(I18n.T("It requires a Bioscrambler Anomaly Core in order to function.")));
        return dot;
    }

    // tg: code/modules/clothing/belts/polymorph_belt.dm:35
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = base_icon_state + (Dm.truthy(active) ? "" : "_inactive");
        worn_icon_state = base_icon_state + (Dm.truthy(active) ? "" : "_inactive");
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/clothing/belts/polymorph_belt.dm:40
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_assembly_signaler_anomaly_bioscrambler)))
            return NONE;

        if (Dm.truthy(active))
        {
            balloon_alert(user, "core already inserted!");
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, "inserting...");

        if (!Dm.truthy(await do_after(user, delay: 3 * SECONDS, target: this)))
        {
            balloon_alert(user, "interrupted!");
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(active))
        {
            balloon_alert(user, "core already inserted!");
            return ITEM_INTERACT_BLOCKING;
        }

        active = TRUE;
        update_appearance(UPDATE_ICON_STATE);
        await update_transform_action();
        playsound(this, "sound/machines/crate/crate_open.ogg", 50, FALSE);
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/clothing/belts/polymorph_belt.dm:65
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!isliving(target_mob))
            return dot;
        if (!isanimal_or_basicmob(target_mob))
        {
            balloon_alert(user, "target too complex!");
            return TRUE;
        }
        if (Dm.truthy(Dm.bitand(target_mob.mob_biotypes, Dm.bitor(Dm.bitor(MOB_HUMANOID, MOB_ROBOTIC), Dm.bitor(Dm.bitor(MOB_SPECIAL, MOB_SPIRIT), MOB_UNDEAD)))))
        {
            balloon_alert(user, "incompatible!");
            return TRUE;
        }
        if (!Dm.truthy(target_mob.compare_sentience_type(SENTIENCE_ORGANIC)))
        {
            balloon_alert(user, "target too intelligent!");
            return TRUE;
        }
        if (Dm.eq(stored_mob_type, target_mob.type))
        {
            balloon_alert(user, "already scanned!");
            return TRUE;
        }
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, target_mob)))
        {
            balloon_alert(user, "busy!");
            return TRUE;
        }
        balloon_alert(user, "scanning...");
        visible_message(span_notice(I18n.T("[] begins scanning [] with [].", user, target_mob, this)));
        if (!Dm.truthy(await do_after(user, delay: 5 * SECONDS, target: target_mob)))
            return TRUE;
        visible_message(span_notice(I18n.T("[] scans [] with [].", user, target_mob, this)));
        stored_mob_type = target_mob.type;
        await update_transform_action();
        playsound(this, "sound/machines/ping.ogg", 50, FALSE);
        return TRUE;
    }

    /// Make sure we can transform into the scanned target
    // tg: code/modules/clothing/belts/polymorph_belt.dm:97
    public virtual async DmTask update_transform_action()
    {
        if (stored_mob_type == null || !Dm.truthy(active))
            return;
        if (transform_action == null)
            transform_action = await add_item_action(typeof(datum_action_cooldown_spell_shapeshift_polymorph_belt)) as datum_action_cooldown_spell_shapeshift_polymorph_belt;
        await transform_action.update_type(stored_mob_type);
    }
}

/// Ability provided by the polymorph belt
// tg: code/modules/clothing/belts/polymorph_belt.dm:111
public partial class datum_action_cooldown_spell_shapeshift_polymorph_belt : datum_action_cooldown_spell_shapeshift
{
    // tg: code/modules/clothing/belts/polymorph_belt.dm:123
    public override async DmTask<object> cast(atom cast_on = null)
    {
        cast_on = owner; //make sure this is only affecting the wearer of the belt
        return await base.cast(cast_on);
    }

    // tg: code/modules/clothing/belts/polymorph_belt.dm:127
    public override async DmTask Remove(mob remove_from = null)
    {
        datum_status_effect_shapechange_mob shapechange = remove_from.has_status_effect(typeof(datum_status_effect_shapechange_mob_from_spell)) as datum_status_effect_shapechange_mob;
        atom changer = (atom)shapechange?.caster_mob ?? remove_from;
        if (changer != null)
            changer.transform = matrix();
        await base.Remove(remove_from);
    }

    // tg: code/modules/clothing/belts/polymorph_belt.dm:133
    public override async DmTask<object> before_cast(atom cast_on = null)
    {
        cast_on = owner;
        object dot = await base.before_cast(cast_on);
        if (Dm.truthy(Dm.bitand(dot, SPELL_CANCEL_CAST)))
            return dot;
        if (channel_time <= 0)
            return dot;
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(cast_on, cast_on)))
            return Dm.bitor(dot, SPELL_CANCEL_CAST);

        object old_transform = cast_on.transform;

        float animate_step = channel_time / 6;
        playsound(cast_on, "sound/effects/wounds/crack1.ogg", 50);
        animate(cast_on, Dm.named("transform", Dm.mul(matrix(), 1.1f)), Dm.named("time", animate_step), Dm.named("easing", SINE_EASING));
        animate(Dm.named("transform", Dm.mul(matrix(), 0.9f)), Dm.named("time", animate_step), Dm.named("easing", SINE_EASING));
        animate(Dm.named("transform", Dm.mul(matrix(), 1.2f)), Dm.named("time", animate_step), Dm.named("easing", SINE_EASING));
        animate(Dm.named("transform", Dm.mul(matrix(), 0.8f)), Dm.named("time", animate_step), Dm.named("easing", SINE_EASING));
        animate(Dm.named("transform", Dm.mul(matrix(), 1.3f)), Dm.named("time", animate_step), Dm.named("easing", SINE_EASING));
        animate(Dm.named("transform", Dm.mul(matrix(), 0.1f)), Dm.named("time", animate_step), Dm.named("easing", SINE_EASING));

        cast_on.balloon_alert(cast_on as mob, "transforming...");
        if (!Dm.truthy(await do_after(cast_on as atom_movable, delay: channel_time, target: cast_on)))
        {
            animate(cast_on, Dm.named("transform", matrix()), Dm.named("time", 0), Dm.named("easing", SINE_EASING));
            cast_on.transform = old_transform;
            return Dm.bitor(dot, SPELL_CANCEL_CAST);
        }
        cast_on.visible_message(span_warning(I18n.T("[]'s body rearranges itself with a horrible crunching sound!", cast_on)));
        playsound(cast_on, "sound/effects/magic/demon_consume.ogg", 50, TRUE);
        return dot;
    }

    // tg: code/modules/clothing/belts/polymorph_belt.dm:162
    public override async DmTask after_cast(atom cast_on = null)
    {
        await base.after_cast(cast_on);
        if (QDELETED(owner))
            return;
        animate(owner, Dm.named("transform", Dm.mul(matrix(), 0.1f)), Dm.named("time", 0), Dm.named("easing", JUMP_EASING));
        animate(Dm.named("transform", matrix()), Dm.named("time", 0.25f * SECONDS), Dm.named("easing", SINE_EASING));
    }

    /// Update what you are transforming to or from
    // tg: code/modules/clothing/belts/polymorph_belt.dm:170
    public virtual async DmTask update_type(object transform_type = null)
    {
        unshift_owner();
        shapeshift_type = transform_type as Type;
        possible_shapes = Dm.list(transform_type);
        desc = I18n.T("Assume your [] form!", Dm.initial((Type)transform_type, (mob_living p) => p.name));
        await build_all_button_icons(update_flags: UPDATE_BUTTON_NAME);
    }
}

/// Subtype of the polymorph status effect which tracks arbitrary mob transformation
// tg: code/modules/clothing/belts/polymorph_belt.dm:179
public partial class datum_status_effect_shapechange_mob_from_spell_polymorph_belt : datum_status_effect_shapechange_mob_from_spell
{
    // tg: code/modules/clothing/belts/polymorph_belt.dm:181
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        RegisterSignal(owner, COMSIG_MOB_CHANGED_TYPE, nameof(on_type_change));
        return dot;
    }

    // tg: code/modules/clothing/belts/polymorph_belt.dm:185
    [SignalHandler]
    public override object on_pre_type_change(mob_living source = null)
    {
        return null; // Stub out base effect because we don't want to cancel if they transform
    }

    /// If our mob transforms (via aging usually) then move the status effect across to the new mob
    // tg: code/modules/clothing/belts/polymorph_belt.dm:189
    [SignalHandler]
    public virtual void on_type_change(mob_living source = null, mob_living new_mob = null)
    {
        mob_living caster = caster_mob; // Will be unset when the mob is unshifted
        datum_action_cooldown_spell_shapeshift transform_action = source_weakref?.resolve() as datum_action_cooldown_spell_shapeshift;
        Dm.combine(ref transform_action.possible_shapes, new_mob.type);
        Waiting.NoSleep(restore_caster());
        Waiting.NoSleep(new_mob.apply_status_effect(type, caster, transform_action));
    }
}
