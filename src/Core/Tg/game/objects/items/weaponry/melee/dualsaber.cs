// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/dualsaber.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): the double-bladed energy sword whole: wielded in both hands it lights, cuts, blocks and
// reflects; a hulk cannot wield it, a clown impales himself, the multitool's rainbow.

/*
 * Double-Bladed Energy Swords - Cheridan
 */
// tg: code/game/objects/items/weaponry/melee/dualsaber.dm:4
public partial class obj_item_dualsaber : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:46  (the first of tg's two Initialize definitions, rules 2.5)
    private object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed),
            Dm.named("force_unwielded", force),
            Dm.named("force_wielded", two_hand_force),
            Dm.named("wieldsound", Dm.resource("sound/items/weapons/saberon.ogg")),
            Dm.named("unwieldsound", Dm.resource("sound/items/weapons/saberoff.ogg")),
            Dm.named("wield_callback", Dm.@new<datum_callback>(this, nameof(on_wield))),
            Dm.named("unwield_callback", Dm.@new<datum_callback>(this, nameof(on_unwield)))));
        return dot;
    }

    /// Triggered on wield of two handed item
    /// Specific hulk checks due to reflection chance for balance issues and switches hitsounds.
    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:59
    public virtual object on_wield(obj_item source = null, mob_living_carbon user = null)
    {
        if (user != null && HAS_TRAIT(user, TRAIT_HULK))
        {
            to_chat(user, span_warning(I18n.T("You lack the grace to wield this!")));
            return COMPONENT_TWOHANDED_BLOCK_WIELD;
        }
        if (HAS_TRAIT_FROM(this, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT))
        {
            to_chat(user, span_warning(I18n.T("You can't seem to hold [] properly!", this)));
            return COMPONENT_TWOHANDED_BLOCK_WIELD;
        }
        update_weight_class(w_class_on);
        hitsound = Dm.resource("sound/items/weapons/blade1.ogg");
        START_PROCESSING(SSobj, this);
        Waiting.NoSleep(set_light_on(TRUE));
        return null;
    }

    /// Triggered on unwield of two handed item
    /// switch hitsounds
    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:73
    public virtual void on_unwield(obj_item source = null, mob_living_carbon user = null)
    {
        update_weight_class(Dm.initial(this, nameof(w_class)));
        hitsound = SFX_SWING_HIT;
        STOP_PROCESSING(SSobj, this);
        Waiting.NoSleep(set_light_on(FALSE));
    }

    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:79
    public override object get_sharpness()
    {
        if (!HAS_TRAIT(this, TRAIT_WIELDED))
            return NONE;
        return base.get_sharpness();
    }

    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:84
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = HAS_TRAIT(this, TRAIT_WIELDED) ? Dm.text("dualsaber[][]", saber_color, HAS_TRAIT(this, TRAIT_WIELDED) ? 1 : 0) : "dualsaber0";
        inhand_icon_state = icon_state;
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:88
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (HAS_TRAIT(this, TRAIT_WIELDED))
        {
            user.visible_message(span_suicide(I18n.T("[] begins spinning way too fast! It looks like [] trying to commit suicide!", user, user.p_theyre())));

            obj_item_bodypart_head myhead = user.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head; //stole from chainsaw code
            obj_item_organ_brain mybrain = user.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
            if (mybrain != null)
                mybrain.organ_flags = Dm.num(Dm.bitand(mybrain.organ_flags, Dm.bitnot(ORGAN_VITAL))); //this cant possibly be a good idea

            object randdir;
            for (int i = 1; i <= 24; i++) //like a headless chicken!
            {
                if (Dm.truthy(user.is_holding(this)))
                {
                    randdir = pick(GLOB.alldirs);
                    await user.Move(get_step(user, Dm.num(randdir)) as atom, randdir);
                    await user.emote("spin");
                    if (i == 3 && myhead != null)
                        await myhead.drop_limb();
                    await sleep(0.3f * SECONDS);
                }
                else
                {
                    user.visible_message(span_suicide(I18n.T("[] panics and starts choking to death!", user)));
                    return OXYLOSS;
                }
            }
        }
        else
        {
            user.visible_message(span_suicide(I18n.T("[] begins beating []self to death with \\the []'s handle! It probably would've been cooler if [] turned it on first!", user, user.p_them(), this, user.p_they())));
        }
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:114
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = Initialize__d0(mapload, __extra);
        if (Dm.truthy(LAZYLEN(possible_colors)))
        {
            saber_color = pick(possible_colors) as string;
            string sw__1 = saber_color;
            if (Dm.eq(sw__1, "red"))
                set_light_color(COLOR_SOFT_RED);
            else if (Dm.eq(sw__1, "green"))
                set_light_color(LIGHT_COLOR_GREEN);
            else if (Dm.eq(sw__1, "blue"))
                set_light_color(LIGHT_COLOR_LIGHT_CYAN);
            else if (Dm.eq(sw__1, "purple"))
                set_light_color(LIGHT_COLOR_LAVENDER);
        }
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:128
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:132
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (HAS_TRAIT(user, TRAIT_HULK))
        {
            to_chat(user, span_warning(I18n.T("You grip the blade too hard and accidentally drop it!")));
            if (HAS_TRAIT(this, TRAIT_WIELDED))
            {
                await user.dropItemToGround(this, force: TRUE);
                return null;
            }
        }
        await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (!HAS_TRAIT(this, TRAIT_WIELDED))
            return null;

        if (HAS_TRAIT(user, TRAIT_CLUMSY) && prob(40))
        {
            await impale(user);
            return null;
        }
        if (prob(50))
            Waiting.DetachSync(() => jedi_spin(user)); // INVOKE_ASYNC(src, PROC_REF(jedi_spin), user)
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:148
    public virtual void jedi_spin(mob_living user = null)
    {
        Waiting.Detach(dance_rotate(user, Dm.@new<datum_callback>(user, nameof(mob.dance_flip)))); // dance_rotate: set waitfor = FALSE
    }

    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:151
    public virtual async DmTask impale(mob_living user = null)
    {
        to_chat(user, span_warning(I18n.T("You twirl around a bit before losing your balance and impaling yourself on [].", this)));
        if (HAS_TRAIT(this, TRAIT_WIELDED))
            await user.take_bodypart_damage(20, 25, check_armor: TRUE);
        else
            await user.adjust_stamina_loss(25);
    }

    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:158
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (!HAS_TRAIT(this, TRAIT_WIELDED))
            return FALSE; //not interested unless we're wielding

        if (attack_type == PROJECTILE_ATTACK)
        {
            obj_projectile our_projectile = (obj_projectile)hitby;

            if (Dm.truthy(our_projectile.reflectable))
                final_block_chance = 0; //we handle this via IsReflect(), effectively 75% block
            else
                final_block_chance -= 25; //We aren't AS good at blocking physical projectiles, like ballistics and thermals
        }

        if (attack_type == LEAP_ATTACK)
            final_block_chance -= 50; //We are particularly bad at blocking someone JUMPING at us..

        if (attack_type == OVERWHELMING_ATTACK)
            final_block_chance = 0; //Far too small to block these kinds of attacks.

        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }

    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:178
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (HAS_TRAIT(this, TRAIT_WIELDED))
        {
            if (Dm.truthy(hacked))
                set_light_color(pick(COLOR_SOFT_RED, LIGHT_COLOR_GREEN, LIGHT_COLOR_LIGHT_CYAN, LIGHT_COLOR_LAVENDER));
            open_flame();
        }
        else
        {
            return PROCESS_KILL;
        }
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:186
    public override async DmTask<object> IsReflect(object def_zone = null)
    {
        if (HAS_TRAIT(this, TRAIT_WIELDED) && prob(block_chance))
            return TRUE;
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:190
    public override async DmTask<object> ignition_effect(atom A = null, mob user = null)
    {
        // same as /obj/item/melee/energy, mostly
        if (!HAS_TRAIT(this, TRAIT_WIELDED))
            return "";
        string in_mouth = "";
        if (iscarbon(user) && user.get_item_by_slot(ITEM_SLOT_MASK) != null)
            in_mouth = I18n.T(", barely missing [] nose", user.p_their());
        object dot = span_rose(I18n.T("[] swings [] [][]. [] light[] [] in the process.", user, user.p_their(), name, in_mouth, user.p_They(), user.p_s(), A.loc == user ? Dm.text("[] []", user.p_their(), A.name) : A));
        playsound(loc, hitsound, get_clamped_volume(), TRUE, -1);
        add_fingerprint(user);
        // Light your candles while spinning around the room
        Waiting.DetachSync(() => jedi_spin(user as mob_living)); // INVOKE_ASYNC(src, PROC_REF(jedi_spin), user)
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/dualsaber.dm:215
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(hacked))
        {
            to_chat(user, span_warning(I18n.T("It's starting to look like a triple rainbow - no, nevermind.")));
            return ITEM_INTERACT_BLOCKING;
        }
        hacked = TRUE;
        to_chat(user, span_warning(I18n.T("2XRNBW_ENGAGE")));
        saber_color = "rainbow";
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }
}
