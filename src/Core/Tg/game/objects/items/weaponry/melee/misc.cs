// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. Before gaps DE1 only /obj/item/can_trigger_gun (M7x slice B: the gun's trigger check), the staff's walking aid
// (syndicate X3a: the bo staff's base), the flyswatter (silicons SI2: the service cyborg's) and the synthetic arm blade (gaps K1)
// were translated; gaps DE1 (D333) brings the file's other leaves: the beesword's sting and its block that a projectile, a leap or an
// overwhelming attack passes, the curator's whip that disarms, the advanced roasting stick (extended and collapsed by tg's
// transforming component, a sausage on it drawn as tg's overlay and roasted beside a singularity, an energy ball, a supermatter
// crystal or a bonfire), the cleric mace with its material slots (datums/materials/material_slots/generic.dm), the SORD, the
// carpenter hammer (a knee tapper with its death scream), the red phone, the bamboo staff (its wielded state) and the moonlight
// greatsword. The var blocks are generated (D111).

// tg: code/game/objects/items/weaponry/melee/misc.dm:528 (interleaved types, rules 1.9)
public partial class obj_item : obj
{
    // tg: code/game/objects/items/weaponry/melee/misc.dm:528
    public virtual async DmTask<object> can_trigger_gun(mob_living user = null, object akimbo_usage = null)
    {
        if (!Dm.truthy(user.can_use_guns(this)))
            return FALSE;
        return TRUE;
    }
}

// tg: code/game/objects/items/weaponry/melee/misc.dm:6
public partial class obj_item_melee_synthetic_arm_blade : obj_item_melee
{
    // tg: code/game/objects/items/weaponry/melee/misc.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple, -5));
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering), Dm.named("speed", 6 * SECONDS), Dm.named("effectiveness", 80)));
        //very imprecise
        return dot;
    }
}

// tg: code/game/objects/items/weaponry/melee/misc.dm:36
public partial class obj_item_melee_beesword : obj_item_melee
{
    // tg: code/game/objects/items/weaponry/melee/misc.dm:59
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (attack_type == PROJECTILE_ATTACK || attack_type == LEAP_ATTACK || attack_type == OVERWHELMING_ATTACK)
            final_block_chance = 0; //Don't bring a sword to a gunfight, and also you aren't going to really block someone full body tackling you with a sword. Or a road roller, if one happened to hit you.
        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:64
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (iscarbon(target) && !QDELETED(target))
        {
            mob_living_carbon carbon_target = (mob_living_carbon)target;
            await ((datum_reagents)carbon_target.reagents).add_reagent(typeof(datum_reagent_toxin), 4);
        }
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:69
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is stabbing []self in the throat with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        playsound(get_turf(this), hitsound, 75, TRUE, -1);
        return TOXLOSS;
    }
}

// tg: code/game/objects/items/weaponry/melee/misc.dm:74
public partial class obj_item_melee_curator_whip : obj_item_melee
{
    // tg: code/game/objects/items/weaponry/melee/misc.dm:92
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (ishuman(target))
        {
            mob_living_carbon_human human_target = (mob_living_carbon_human)target;
            await human_target.drop_all_held_items();
            human_target.visible_message(span_danger(I18n.T("[] disarms []!", user, human_target)), span_userdanger(I18n.T("[] disarmed you!", user)));
        }
    }
}

// tg: code/game/objects/items/weaponry/melee/misc.dm:98
public partial class obj_item_melee_roastingstick : obj_item_melee
{
    /// Static list of things our roasting stick can interact with.
    // tg: code/game/objects/items/weaponry/melee/misc.dm:116  var/static/list/ovens
    private static DmList ovens
    {
        get => TgState.Current.obj_item_melee_roastingstick__ovens;
        set => TgState.Current.obj_item_melee_roastingstick__ovens = value;
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:120
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(ovens))
            ovens = typecacheof(Dm.list(typeof(obj_singularity), typeof(obj_energy_ball), typeof(obj_machinery_power_supermatter_crystal), typeof(obj_structure_bonfire))) as DmList;
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_transforming),
            Dm.named("hitsound_on", hitsound),
            Dm.named("clumsy_check", FALSE),
            Dm.named("inhand_icon_change", FALSE)));
        RegisterSignal(this, COMSIG_TRANSFORMING_PRE_TRANSFORM, nameof(attempt_transform));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return dot;
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_PRE_TRANSFORM].
     *
     * If there is a sausage attached, returns COMPONENT_BLOCK_TRANSFORM.
     */
    // tg: code/game/objects/items/weaponry/melee/misc.dm:138
    [SignalHandler]
    public virtual object attempt_transform(obj_item source = null, mob user = null, object active = null)
    {
        if (held_sausage != null)
        {
            to_chat(user, span_warning(I18n.T("You can't retract [] while [] is attached!", this, held_sausage)));
            return COMPONENT_BLOCK_TRANSFORM;
        }
        return null;
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Gives feedback on stick extension.
     */
    // tg: code/game/objects/items/weaponry/melee/misc.dm:150
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        inhand_icon_state = Dm.truthy(active) ? "nullrod" : null;
        if (user != null)
            balloon_alert(user, I18n.T("[] []", Dm.truthy(active) ? I18n.T("extended") : I18n.T("collapsed"), this));
        playsound(this, Dm.resource("sound/items/weapons/batonextend.ogg"), 50, TRUE);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:159
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_food_sausage))
            return NONE;

        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
        {
            to_chat(user, span_warning(I18n.T("You must extend [] to attach anything to it!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (held_sausage != null)
        {
            to_chat(user, span_warning(I18n.T("[] is already attached to []!", held_sausage, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            to_chat(user, span_warning(I18n.T("[] doesn't seem to want to get on []!", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        held_sausage = (obj_item_food_sausage)tool;
        update_appearance();
        return ITEM_INTERACT_SKIP_TO_ATTACK;
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:179
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        await base.attack_hand(user, modifiers, __extra);
        if (held_sausage != null)
            await user.put_in_hands(held_sausage);
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:184
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (held_sausage != null)
            Dm.append(ref dot, mutable_appearance(icon, "roastingstick_sausage"));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:189
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == held_sausage)
        {
            held_sausage = null;
            update_appearance();
        }
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:195
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return NONE;
        if (!Dm.truthy(is_type_in_typecache(interacting_with, ovens)))
            return NONE;
        if (interacting_with is obj_singularity || interacting_with is obj_energy_ball && Dm.num(get_dist(user, interacting_with)) < 10)
        {
            to_chat(user, span_notice(I18n.T("You send [] towards [].", held_sausage, interacting_with)));
            playsound(this, Dm.resource("sound/items/tools/rped.ogg"), 50, TRUE);
            beam = user.Beam(interacting_with, icon_state: "rped_upgrade", time: 10 * SECONDS) as datum_beam;
            await finish_roasting(user, interacting_with);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:208
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return NONE;
        if (!Dm.truthy(is_type_in_typecache(interacting_with, ovens)))
            return NONE;
        to_chat(user, span_notice(I18n.T("You extend [] towards [].", this, interacting_with)));
        playsound(this, Dm.resource("sound/items/weapons/batonextend.ogg"), 50, TRUE);
        await finish_roasting(user, interacting_with);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:218
    public virtual async DmTask finish_roasting(object user = null, atom target = null)
    {
        if (Dm.truthy(await do_after(user as mob, 10 * SECONDS, target: user as atom)))
        {
            to_chat(user, span_notice(I18n.T("You finish roasting [].", held_sausage)));
            playsound(this, Dm.resource("sound/items/tools/welder2.ogg"), 50, TRUE);
            await held_sausage.add_atom_colour(rgb(103, 63, 24), FIXED_COLOUR_PRIORITY);
            held_sausage.name = I18n.T("[]-roasted []", target.name, held_sausage.name);
            held_sausage.desc = I18n.T("[] It has been cooked to perfection on \\a [].", held_sausage.desc, target);
            update_appearance();
        }
        else
        {
            QDEL_NULL(ref beam);
            playsound(this, Dm.resource("sound/items/weapons/batonextend.ogg"), 50, TRUE);
            to_chat(user, span_notice(I18n.T("You put [] away.", this)));
        }
    }
}

// tg: code/game/objects/items/weaponry/melee/misc.dm:231
public partial class obj_item_melee_cleric_mace : obj_item_melee
{
    // It only inherits the name of the main material it's made of. The secondary is in the description.
    // tg: code/game/objects/items/weaponry/melee/misc.dm:262
    public override async DmTask<object> get_material_prefixes(DmList materials = null)
    {
        datum_material material = await get_material_from_slot(typeof(datum_material_slot_weapon_head)) as datum_material;
        return material?.name;
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:266
    public override async DmTask finalize_material_effects(DmList materials = null)
    {
        await base.finalize_material_effects(materials);
        datum_material material = await get_material_from_slot(typeof(datum_material_slot_handle)) as datum_material;
        if (material != null)
            desc = I18n.T("[] Its handle is made of [].", Dm.initial(this, (atom p) => p.desc), material.name);
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:272
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        // Don't bring a...mace to a gunfight, and also you aren't going to really block someone full body tackling you with a mace.
        // Or a road roller, if one happened to hit you.
        if (attack_type == PROJECTILE_ATTACK || attack_type == LEAP_ATTACK || attack_type == OVERWHELMING_ATTACK)
            final_block_chance = 0;
        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }
}

// tg: code/game/objects/items/weaponry/melee/misc.dm:283
public partial class obj_item_sord : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/misc.dm:300
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is trying to impale []self with []! It might be a suicide attempt if it weren't so shitty.", user, user.p_them(), this)),
            span_suicide(I18n.T("You try to impale yourself with [], but it's USELESS...", this)));
        return SHAME;
    }
}

// tg: code/game/objects/items/weaponry/melee/misc.dm:305
public partial class obj_item_carpenter_hammer : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/misc.dm:323
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_kneejerk)));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_item_killsound),
            Dm.named("allowed_mobs", Dm.list(typeof(mob_living_carbon_human))),
            Dm.named("killsound", Dm.resource("sound/items/weapons/hammer_death_scream.ogg")),
            Dm.named("replace_default_death_sound", TRUE)));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:332
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, "");
        Dm.append(ref dot, I18n.T("Real World Tip:"));
        Dm.append(ref dot, pick(
            I18n.T("Every building, from hospitals to homes, has a room that serves as the heart of the building and carries blood and nutrients to its extremities. Try to find the heart of your home!"),
            I18n.T("All the food you've tried is rotten. You've never eaten fresh food."),
            I18n.T("Viruses do not exist. Illness is simply your body punishing you for what you have done wrong."),
            I18n.T("Space stations must have at least 50 mammalian teeth embedded in the north walls for structural safety reasons."),
            I18n.T("Queen dragonfly sleeps and smiles.")));
        return dot;
    }
}

// tg: code/game/objects/items/weaponry/melee/misc.dm:345
public partial class obj_item_phone : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/misc.dm:359
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (Dm.locate<obj_structure_chair_stool>(user.loc) != null)
            user.visible_message(span_suicide(I18n.T("[] begins to tie a noose with []'s cord! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        else
            user.visible_message(span_suicide(I18n.T("[] is strangling []self with []'s cord! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        return OXYLOSS;
    }
}

// tg: code/game/objects/items/weaponry/melee/misc.dm:366
public partial class obj_item_bambostaff : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/misc.dm:387
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // there are too many puns to choose from. ('Bo' is the 'real' name for this kind of weapon.)
        name = (string)pick(I18n.T("bamboo staff"), I18n.T("bambo staff"), I18n.T("bam-Bo staff"), I18n.T("bam boo staff"), I18n.T("bam-boo staff"), I18n.T("bam Bo"), I18n.T("bambo"), I18n.T("bam-Bo"), I18n.T("bamboo-Bo"));
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed),
            Dm.named("force_unwielded", 10),
            Dm.named("force_wielded", 14)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_walking_aid)));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:397
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        string state = Dm.text("[][]", base_icon_state, Dm.num(HAS_TRAIT(this, TRAIT_WIELDED)));
        icon_state = state; // icon_state = inhand_icon_state = ...
        inhand_icon_state = state;
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:401
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (attack_type == PROJECTILE_ATTACK || attack_type == LEAP_ATTACK || attack_type == OVERWHELMING_ATTACK)
            final_block_chance = 0; //Don't bring a staff to a gunfight, and also you aren't going to really block someone full body tackling you with a staff. Or a road roller, if one happened to hit you.
        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }
}

// tg: code/game/objects/items/weaponry/melee/misc.dm:406
public partial class obj_item_staff : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/misc.dm:424
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_walking_aid)));
        return dot;
    }
}

// tg: code/game/objects/items/weaponry/melee/misc.dm:471
public partial class obj_item_melee_flyswatter : obj_item_melee
{
    /// Things in this list will be instantly splatted.  Flyman weakness is handled in the flyman species weakness proc.
    // tg: code/game/objects/items/weaponry/melee/misc.dm:486  var/static/list/splattable
    private static DmList splattable
    {
        get => TgState.Current.obj_item_melee_flyswatter__splattable;
        set => TgState.Current.obj_item_melee_flyswatter__splattable = value;
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:488
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (splattable == null)
        {
            splattable = typecacheof(Dm.list(
                typeof(mob_living_basic_ant),
                typeof(mob_living_basic_butterfly),
                typeof(mob_living_basic_cockroach),
                typeof(mob_living_basic_cockroach_bloodroach),
                typeof(mob_living_basic_spider_growing_spiderling),
                typeof(mob_living_basic_bee),
                typeof(obj_effect_decal_cleanable_ants),
                typeof(obj_item_queen_bee))) as DmList;
        }
        Waiting.NoSleep(AddComponent(typeof(datum_component_bane), Dm.named("affected_biotypes", MOB_BUG), Dm.named("pre_bane_callback", Dm.@new<datum_callback>(this, nameof(bane_check)))));
        return dot;
    }

    // Different type of bug mobs get different amounts of damage multipliers
    // tg: code/game/objects/items/weaponry/melee/misc.dm:505
    public virtual void bane_check(mob_living target = null, mob_living attacker = null, DmList attack_modifiers = null)
    {
        float amount;
        if (isanimal_or_basicmob(target))
            amount = 24;
        else if (isflyperson(target))
            amount = 29;
        else if (ismoth(target))
            amount = 9;
        else // ?? Whatever
            amount = 14;
        attack_modifiers[FORCE_MODIFIER] = Dm.num(attack_modifiers[FORCE_MODIFIER]) + amount; // MODIFY_ATTACK_FORCE
    }

    // tg: code/game/objects/items/weaponry/melee/misc.dm:515
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.truthy(is_type_in_typecache(target, splattable)))
        {
            to_chat(user, span_warning(I18n.T("You easily splat [].", target)));
            if (QDELETED(target))
                return;
            if (isliving(target))
            {
                Dm.@new<obj_effect_decal_cleanable_insectguts>(target.drop_location());
                mob_living bug = (mob_living)target;
                bug.investigate_log("has been splatted by a flyswatter.", INVESTIGATE_DEATHS);
                await bug.gib(DROP_ALL_REMAINS);
            }
            else
                qdel(target);
        }
    }
}

// tg: code/game/objects/items/weaponry/melee/misc.dm:549
public partial class obj_item_melee_moonlight_greatsword : obj_item_melee
{
    // tg: code/game/objects/items/weaponry/melee/misc.dm:572
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/weaponry/melee/misc.dm:486  var/static/list/splattable
    internal DmList obj_item_melee_flyswatter__splattable;
    // tg: code/game/objects/items/weaponry/melee/misc.dm:116  var/static/list/ovens
    internal DmList obj_item_melee_roastingstick__ovens;
}
