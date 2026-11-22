// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/high_frequency_blade.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole. The high frequency blade: wielded (`hfrequency1`), it deflects every shot and parries twice as
// often, and every swing is a slash (tg's blue slash drawn where the target was clicked, its bladeslice and zap): its damage grows
// with how far the click moved on the same target since the last swing, a dead body may burst into gore, an object takes three
// times the damage, a wall may come down and rock is drilled. Its pacifism check reads the blade, not its wielder: translated as
// written. The wizard's kind can only be wielded by the magically gifted. In 3D the click's pixel is the hit point on the target's
// sprite or model (the port's click modifiers carry ICON_X and ICON_Y as tg's). The var blocks are generated (D111).

// tg: code/game/objects/items/weaponry/melee/high_frequency_blade.dm:1
public partial class obj_item_highfrequencyblade : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/high_frequency_blade.dm:36
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed),
            Dm.named("wield_callback", Dm.@new<datum_callback>(this, nameof(on_wield))),
            Dm.named("unwield_callback", Dm.@new<datum_callback>(this, nameof(on_unwield)))));
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/high_frequency_blade.dm:44
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("hfrequency[]", HAS_TRAIT(this, TRAIT_WIELDED) ? 1 : 0);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/weaponry/melee/high_frequency_blade.dm:48
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        attack_type ??= MELEE_ATTACK;
        if (attack_type == PROJECTILE_ATTACK)
        {
            if (HAS_TRAIT(this, TRAIT_WIELDED) || prob(final_block_chance.Value))
            {
                owner.visible_message(span_danger(I18n.T("[] deflects [] with []!", owner, attack_text, this)));
                playsound(this, SFX_BULLET_MISS, 75, TRUE);
                return TRUE;
            }
            return FALSE;
        }
        float stop_that_blade = (final_block_chance.Value + (attack_type == OVERWHELMING_ATTACK ? 25 : 0)) * (HAS_TRAIT(this, TRAIT_WIELDED) ? 2 : 1);
        await DmTask.CompletedTask;
        if (prob(stop_that_blade))
        {
            owner.visible_message(span_danger(I18n.T("[] parries [] with []!", owner, attack_text, this)));
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/items/weaponry/melee/high_frequency_blade.dm:61
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.pre_attack(target, user, modifiers, attack_modifiers);
        if (Dm.truthy(dot))
            return dot;
        if (!HAS_TRAIT(this, TRAIT_WIELDED))
            return dot; // Default attack
        if (isliving(target) && HAS_TRAIT(this, TRAIT_PACIFISM))
            return dot; // Default attack (ultimately nothing)

        return await slash(target, user, modifiers);
    }

    /// triggered on wield of two handed item
    // tg: code/game/objects/items/weaponry/melee/high_frequency_blade.dm:73
    public virtual async DmTask on_wield(obj_item source = null, mob user = null)
    {
        await update_icon(UPDATE_ICON_STATE);
    }

    /// triggered on unwield of two handed item
    // tg: code/game/objects/items/weaponry/melee/high_frequency_blade.dm:77
    public virtual async DmTask on_unwield(obj_item source = null, mob user = null)
    {
        await update_icon(UPDATE_ICON_STATE);
    }

    // tg: code/game/objects/items/weaponry/melee/high_frequency_blade.dm:80
    public virtual async DmTask<object> slash(atom target = null, mob_living user = null, DmList modifiers = null)
    {
        await user.do_attack_animation(target, "nothing");
        float damage_mod = 1;
        object x_slashed = Dm.truthy(text2num(modifiers?[ICON_X])) ? text2num(modifiers[ICON_X]) : ICON_SIZE_X / 2; //in case we arent called by a client
        object y_slashed = Dm.truthy(text2num(modifiers?[ICON_Y])) ? text2num(modifiers[ICON_Y]) : ICON_SIZE_Y / 2; //in case we arent called by a client
        Dm.@new<obj_effect_temp_visual_slash>(get_turf(target), target, x_slashed, y_slashed, slash_color);
        if (target == previous_target?.resolve()) //if the same target, we calculate a damage multiplier if you swing your mouse around
        {
            float x_mod = Dm.num(previous_x) - Dm.num(x_slashed);
            float y_mod = Dm.num(previous_y) - Dm.num(y_slashed);
            damage_mod = max(1, Dm.num(round(Dm.sqrt(Dm.pow(x_mod, 2) + Dm.pow(y_mod, 2)) / 10, 0.1f)));
        }
        previous_target = (datum_weakref)WEAKREF(target);
        previous_x = x_slashed;
        previous_y = y_slashed;
        playsound(this, Dm.resource("sound/items/weapons/bladeslice.ogg"), 100, vary: TRUE);
        playsound(this, Dm.resource("sound/items/weapons/zapbang.ogg"), 50, vary: TRUE);
        if (target is mob_living living_target)
        {
            await living_target.apply_damage(force * damage_mod, BRUTE, sharpness: SHARP_EDGED, wound_bonus: wound_bonus, exposed_wound_bonus: exposed_wound_bonus, def_zone: user.zone_selected);
            log_combat(user, living_target, "slashed", this);
            if (living_target.stat == DEAD && prob(force * damage_mod * 0.5f))
            {
                living_target.visible_message(span_danger(I18n.T("[] explodes in a shower of gore!", living_target)), blind_message: span_hear(I18n.T("You hear organic matter ripping and tearing!")));
                living_target.investigate_log(Dm.text("has been gibbed by [].", this), INVESTIGATE_DEATHS);
                await living_target.gib(DROP_ALL_REMAINS);
                log_combat(user, living_target, "gibbed", this);
            }
            return TRUE;
        }
        else if (Dm.truthy(target.uses_integrity))
        {
            await target.take_damage(force * damage_mod * 3, BRUTE, MELEE, FALSE, null, 50);
            return TRUE;
        }
        else if (iswallturf(target) && prob(force * damage_mod * 0.5f))
        {
            turf_closed_wall wall_target = (turf_closed_wall)target;
            await wall_target.dismantle_wall();
            return TRUE;
        }
        else if (ismineralturf(target) && prob(force * damage_mod))
        {
            turf_closed_mineral mineral_target = (turf_closed_mineral)target;
            await mineral_target.gets_drilled();
            return TRUE;
        }
        return FALSE;
    }
}

// tg: code/game/objects/items/weaponry/melee/high_frequency_blade.dm:118
public partial class obj_effect_temp_visual_slash : obj_effect_temp_visual
{
    // tg: code/game/objects/items/weaponry/melee/high_frequency_blade.dm:125
    [DmParams("mapload", "target", "x_slashed", "y_slashed", "slash_color")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom target = Dm.arg(__extra, 0) as atom;
        float x_slashed = Dm.num(Dm.arg(__extra, 1));
        float y_slashed = Dm.num(Dm.arg(__extra, 2));
        object slash_color = Dm.arg(__extra, 3);
        object dot = base.Initialize(mapload, __extra);
        if (target == null)
            return dot;
        matrix new_transform = Dm.matrix();
        new_transform.Turn(rand(1, 360)); // Random slash angle
        datum_decompose_matrix decomp = ((matrix)target.transform).decompose() as datum_decompose_matrix;
        new_transform.Translate((x_slashed - ICON_SIZE_X / 2) * decomp.scale_x, (y_slashed - ICON_SIZE_Y / 2) * decomp.scale_y); // Move to where we clicked
        //Follow target's transform while ignoring scaling
        new_transform.Turn(decomp.rotation);
        new_transform.Translate(decomp.shift_x, decomp.shift_y);
        new_transform.Translate(target.pixel_x, target.pixel_y); // Follow target's pixel offsets
        transform = new_transform;
        //Double the scale of the matrix by doubling the 2x2 part without touching the translation part
        matrix scaled_transform = Dm.matrix(new_transform).Add(Dm.matrix(new_transform.a, new_transform.b, 0, new_transform.d, new_transform.e, 0));
        animate(this, Dm.named("time", duration * 0.5f), Dm.named("color", slash_color), Dm.named("transform", scaled_transform), Dm.named("alpha", 255));
        return dot;
    }
}

// tg: code/game/objects/items/weaponry/melee/high_frequency_blade.dm:142
public partial class obj_item_highfrequencyblade_wizard : obj_item_highfrequencyblade
{
    // tg: code/game/objects/items/weaponry/melee/high_frequency_blade.dm:149
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!HAS_MIND_TRAIT(user, TRAIT_MAGICALLY_GIFTED))
        {
            balloon_alert(user, I18n.T("you're too weak!"));
            return null;
        }
        return await base.attack_self(user, modifiers, __extra);
    }
}
