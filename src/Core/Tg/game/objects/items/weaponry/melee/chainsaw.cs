// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/chainsaw.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole. The chainsaw: held in both hands, its starting cord pulled (its action) turns it on (24 force,
// tg's chainsaw hit sound and running loop, a saw three times as fast, a butcher), a demolition tool only while on; on, it takes the
// head off a dead body aimed at the head in fifteen seconds (while the head stays the same); mounted as an arm (the prosthetic
// icon element) it needs one hand. Its suicide snaps the neck off, shreds or beheads. THE GREAT COMMUNICATOR (100 armour
// penetration, 30 force, beheads in two seconds, angered by shots) and the double-ended chainsaw spear (40 force, blocks half the
// time but breaks into two chainsaws and cable cuffs three hits in ten, a hulk drops it, half its hits tear its wielder, and it
// spins its wielder now and then). The var blocks are generated (D111).

// CHAINSAW
// tg: code/game/objects/items/weaponry/melee/chainsaw.dm:2
public partial class obj_item_chainsaw : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:35
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        chainsaw_loop = Dm.@new<datum_looping_sound_chainsaw>(this);
        Waiting.NoSleep(AddComponent(typeof(datum_component_transforming),
            Dm.named("force_on", force_on),
            Dm.named("throwforce_on", force_on),
            Dm.named("throw_speed_on", throw_speed),
            Dm.named("sharpness_on", SHARP_EDGED),
            Dm.named("hitsound_on", Dm.resource("sound/items/weapons/chainsawhit.ogg")),
            Dm.named("w_class_on", w_class)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering),
            Dm.named("speed", 3 * SECONDS),
            Dm.named("effectiveness", 100),
            Dm.named("bonus_modifier", 0),
            Dm.named("butcher_sound", Dm.resource("sound/items/weapons/chainsawhit.ogg")),
            Dm.named("disabled", TRUE)));
        Waiting.NoSleep(AddElement(typeof(datum_element_prosthetic_icon), "mounted", 180, TRUE));
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("require_twohands", TRUE)));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        RegisterSignal(this, COMSIG_ITEM_PRE_USED_AS_PROSTHETIC, nameof(disable_twohanded_comp));
        RegisterSignal(this, COMSIG_ITEM_DROPPED_FROM_PROSTHETIC, nameof(enable_twohanded_comp));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:60
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        to_chat(user, span_notice(I18n.T("As you pull the starting cord dangling from [], [].", this, Dm.truthy(active) ? I18n.T("it begins to whirr") : I18n.T("the chain stops moving"))));
        datum_component_butchering butchering = GetComponent(typeof(datum_component_butchering)) as datum_component_butchering;
        butchering.butchering_enabled = Dm.num(active);
        if (Dm.truthy(active))
            chainsaw_loop.start();
        else
            chainsaw_loop.stop();

        toolspeed = Dm.truthy(active) ? 0.5f : Dm.num(Dm.initial(this, (obj_item p) => p.toolspeed));
        Waiting.NoSleep(update_item_action_buttons());

        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:76
    [SignalHandler]
    public virtual void disable_twohanded_comp()
    {
        qdel(GetComponent(typeof(datum_component_two_handed)));
    }

    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:81
    [SignalHandler]
    public virtual void enable_twohanded_comp()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("require_twohands", TRUE)));
    }

    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:86
    public override object get_demolition_modifier(obj target = null)
    {
        return HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE) ? demolition_mod : 0.8f;
    }

    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:89
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
        {
            user.visible_message(span_suicide(I18n.T("[] smashes [] into [] neck, destroying [] esophagus! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_their(), user.p_theyre())));
            playsound(this, Dm.resource("sound/items/weapons/genhit1.ogg"), 100, TRUE);
            return BRUTELOSS;
        }

        if (!iscarbon(user))
        {
            user.visible_message(span_suicide(I18n.T("[] begins to shred [] with []! It looks like [] trying to commit suicide!", user, user.p_themselves(), this, user.p_theyre())));
            return BRUTELOSS;
        }

        user.visible_message(span_suicide(I18n.T("[] begins to tear [] head off with []! It looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
        obj_item_bodypart_head myhead = user.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (myhead == null)
        {
            visible_message(span_suicide(I18n.T("[] realises that [] cannot cut off [] head because [] []n't have one!", user, user.p_they(), user.p_their(), user.p_they(), user.p_do())));
            return SHAME;
        }

        playsound(this, Dm.resource("sound/items/weapons/chainsawhit.ogg"), 100, TRUE);
        if (Dm.truthy(await myhead.dismember()))
            return BRUTELOSS;

        datum_wound_slash crit_wound = Dm.@new<datum_wound_slash>();
        await crit_wound.apply_wound(myhead);
        visible_message(span_suicide(I18n.T("[] tries in vain to cut off [] head but perishes in the attempt!", user, user.p_their())));
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:114
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);

        if (target_mob.stat != DEAD)
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);

        if (!Dm.eq(user.zone_selected, BODY_ZONE_HEAD))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);

        obj_item_bodypart head = target_mob.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (head == null)
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);

        playsound(this, Dm.resource("sound/items/weapons/chainsawhit.ogg"), vol: 100, vary: TRUE);
        target_mob.balloon_alert(user, I18n.T("cutting off head..."));

        if (!Dm.truthy(await do_after(user, behead_time, target_mob, extra_checks: Dm.@new<datum_callback>(this, nameof(has_same_head), target_mob, head))))
            return TRUE;

        if (Dm.truthy(await head.dismember(silent: FALSE)))
            playsound(this, Dm.resource("sound/items/weapons/chainsawhit.ogg"), vol: 100, vary: TRUE);
        else
            to_chat(user, span_warning(I18n.T("[]'s head is attached too firmly to cut off!", target_mob)));

        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:141
    public virtual object has_same_head(mob_living target_mob = null, obj_item_bodypart head = null)
    {
        return target_mob.get_bodypart(BODY_ZONE_HEAD) == head;
    }
}

/**
 * Handles adding components to the chainsaw. Added in Initialize()
 *
 * Applies components to the chainsaw. Added as a separate proc to allow for
 * variance between subtypes
 */

// tg: code/game/objects/items/weaponry/melee/chainsaw.dm:151
public partial class obj_item_chainsaw_doomslayer : obj_item_chainsaw
{
    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:158
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_type ??= MELEE_ATTACK;
        if (attack_type == PROJECTILE_ATTACK)
        {
            owner.visible_message(span_danger(I18n.T("Ranged attacks just make [] angrier!", owner)));
            playsound(this, SFX_BULLET_MISS, 75, TRUE);
            return TRUE;
        }
        await DmTask.CompletedTask;
        return FALSE;
    }
}

// tg: code/game/objects/items/weaponry/melee/chainsaw.dm:165
public partial class obj_item_chainsaw_dual : obj_item_chainsaw
{
    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:187
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.pre_attack(target, user, modifiers, attack_modifiers);
        if (HAS_TRAIT(user, TRAIT_HULK))
        {
            to_chat(user, span_warning(I18n.T("You grip the weapon too hard and accidentally drop it!")));
            await user.dropItemToGround(this, force: TRUE);
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:194
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);

        if (prob(50))
        {
            await impale(user);
            return TRUE;
        }

        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }

    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:204
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (prob(50))
            Waiting.Detach(jedi_spin(user as mob_living));
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:208
    public virtual async DmTask jedi_spin(mob_living user = null)
    {
        await dance_rotate(user, Dm.@new<datum_callback>(user, nameof(mob.dance_flip)));
    }

    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:211
    public virtual async DmTask impale(mob_living user = null)
    {
        to_chat(user, span_warning(I18n.T("You horrifically tear yourself with []!", this)));
        await user.take_bodypart_damage(45, check_armor: TRUE, wound_bonus: 20, sharpness: SHARP_EDGED);
        await user.do_attack_animation(user);
        await user.Stun(1 * SECONDS);
        await user.Knockdown(5 * SECONDS);
    }

    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:218
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (attack_type == PROJECTILE_ATTACK || attack_type == LEAP_ATTACK || attack_type == OVERWHELMING_ATTACK)
            final_block_chance = 0;
        if (attack_type != PROJECTILE_ATTACK && prob(30))
        {
            await atom_destruction(MELEE);
            return FALSE;
        }

        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }

    // tg: code/game/objects/items/weaponry/melee/chainsaw.dm:227
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        playsound(this, Dm.resource("sound/effects/grillehit.ogg"), 50);
        Dm.@new<obj_item_chainsaw>(drop_location());
        Dm.@new<obj_item_chainsaw>(drop_location());
        Dm.@new<obj_item_restraints_handcuffs_cable>(drop_location());
        if (isliving(loc))
            ((atom)loc).balloon_alert(loc as mob, I18n.T("weapon broken!"));
        await base.atom_destruction(damage_flag);
    }
}
