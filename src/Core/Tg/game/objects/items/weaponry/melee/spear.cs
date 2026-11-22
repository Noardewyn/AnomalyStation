// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/spear.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole. The spear: two handed (10 force, 18 wielded; two tiles of reach in one hand, one wielded with
// twice the armour penetration), a jousting charge, a butcher and a walking aid; its tip and its handle are material slots (the
// tip's hardness and density set its wounds and armour penetration, the handle's its throw), the glass kinds drawn as tg's plasma,
// titanium and plastitanium spears and any other tip as the blank spear with its tip's colour; crafted from a rod and a shard, or a
// wireprod and a crystal; its suicide puts the head on a pike. The explosive lance (an IED on a spear: wielded, a hit shouts its
// war cry, lies its bearer down on the target and blows), the Grey Tide (illusions of its bearer), the military javelin, the
// giantslayer spear and its unfired kind (forged in fire), the bone and bamboo spears with their own pikes, and the Sky Bulge with
// its Dragoon Strike (a jump into the air and a fall on the tile that hits whoever stands there twice as hard). tg's spear tip
// removes its stats under the misspelt type /datum/material_slot/weapon_head/spearhead, so a tip's stats stay when its material
// is replaced: translated as written. The var blocks are generated (D111).

// tg: code/game/objects/items/weaponry/melee/spear.dm:1  #define SPEAR_CUSTOM_TIP_PREFIX "spearblank"
file static class SpearDefines
{
    internal const string SPEAR_CUSTOM_TIP_PREFIX = "spearblank";
}

// tg: code/game/objects/items/weaponry/melee/spear.dm:3
public partial class obj_item_spear : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/spear.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        force = Dm.num(force_unwielded);
        //decent in a pinch, but pretty bad.
        Waiting.NoSleep(AddComponent(typeof(datum_component_jousting), Dm.named("max_tile_charge", 9), Dm.named("min_tile_charge", 6)));

        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering), Dm.named("speed", 10 * SECONDS), Dm.named("effectiveness", 70)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed),
            Dm.named("force_unwielded", force_unwielded),
            Dm.named("force_wielded", force_wielded),
            Dm.named("icon_wielded", Dm.text("[]1", icon_prefix)),
            Dm.named("wield_callback", Dm.@new<datum_callback>(this, nameof(on_wield))),
            Dm.named("unwield_callback", Dm.@new<datum_callback>(this, nameof(on_unwield)))));
        Waiting.NoSleep(AddComponent(typeof(datum_component_walking_aid)));
        add_headpike_component();
        update_appearance();
        return dot;
    }

    // I dunno man
    // tg: code/game/objects/items/weaponry/melee/spear.dm:75
    public virtual void add_headpike_component()
    {
        DmList slapcraft_recipe_list = TgState.Current.obj_item_spear__add_headpike_component__slapcraft_recipe_list ??= Dm.list(typeof(datum_crafting_recipe_headpike));

        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), Dm.named("slapcraft_recipes", slapcraft_recipe_list)));
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:83
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]0", icon_prefix);
        if (icon_prefix == SpearDefines.SPEAR_CUSTOM_TIP_PREFIX)
            worn_icon_state = "spearglass0";
        else
            worn_icon_state = null;
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:91
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins to sword-swallow \\the []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
            return SHAME;
        obj_item_bodypart_head head = user.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (head == null)
            return BRUTELOSS; // No head?
        obj_structure_headpike pike = Dm.@new(pike_type, drop_location()) as obj_structure_headpike;
        if (!Dm.truthy(await head.dismember(silent: FALSE, wounding_type: WOUND_PIERCE)))
            return BRUTELOSS; // Guess your neck is too strong
        await head.forceMove(pike);
        pike.victim = head;
        await forceMove(pike);
        pike.spear = this;
        pike.update_appearance(Dm.bitor(UPDATE_NAME, UPDATE_OVERLAYS));
        return BRUTELOSS; // Just in case they survived losing the head
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:108
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        obj_item_stack_rods rod = Dm.locate<obj_item_stack_rods>(components);
        if (rod != null)
            await set_material_slot(typeof(datum_material_slot_handle_spear), await rod.get_master_material());

        obj_item_shard tip = Dm.locate<obj_item_shard>(components);
        if (tip == null)
        {
            await base.on_craft_completion(components, current_recipe, crafter);
            return;
        }

        datum_material tip_material = await tip.get_master_material() as datum_material;
        await set_material_slot(typeof(datum_material_slot_weapon_head_speartip), tip_material);
        await base.on_craft_completion(components, current_recipe, crafter);
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:121
    public override async DmTask set_material_slot(object slot_type = null, object new_material = null)
    {
        await base.set_material_slot(slot_type, new_material);
        if (!Dm.eq(slot_type, typeof(datum_material_slot_weapon_head_speartip)))
            return;

        if (new_material is datum_material as_material)
            new_material = as_material.type;

        if (Dm.eq(new_material, typeof(datum_material_alloy_plasmaglass)))
            icon_prefix = "spearplasma";
        else if (Dm.eq(new_material, typeof(datum_material_alloy_titaniumglass)))
            icon_prefix = "speartitanium";
        else if (Dm.eq(new_material, typeof(datum_material_alloy_plastitaniumglass)))
            icon_prefix = "spearplastitanium";
        else
            icon_prefix = SpearDefines.SPEAR_CUSTOM_TIP_PREFIX;

        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed),
            Dm.named("icon_wielded", Dm.text("[]1", icon_prefix)),
            Dm.named("wield_callback", Dm.@new<datum_callback>(this, nameof(on_wield))),
            Dm.named("unwield_callback", Dm.@new<datum_callback>(this, nameof(on_unwield)))));
        update_appearance();
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:147
    public override async DmTask finalize_material_effects(DmList materials = null)
    {
        await base.finalize_material_effects(materials);
        update_appearance();
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:151
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList ?? Dm.list();
        if (icon_prefix != SpearDefines.SPEAR_CUSTOM_TIP_PREFIX)
            return dot;
        datum_material tip_material = await get_master_material() as datum_material;
        mutable_appearance tip_overlay = (mutable_appearance)mutable_appearance(icon, "speartip", appearance_flags: Dm.bitor(KEEP_APART, RESET_COLOR));
        tip_overlay.color = tip_material.color;
        Dm.append(dot, tip_overlay);
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:160
    public override async DmTask<object> separate_worn_overlays(mutable_appearance standing = null, mutable_appearance draw_target = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        DmList dot = await base.separate_worn_overlays(standing, draw_target, isinhands, icon_file, bodyshape) as DmList ?? Dm.list();
        if (icon_prefix != SpearDefines.SPEAR_CUSTOM_TIP_PREFIX || !Dm.truthy(isinhands))
            return dot;
        datum_material tip_material = await get_master_material() as datum_material;
        mutable_appearance tip_overlay = (mutable_appearance)mutable_appearance(icon_file, Dm.text("speartip[]", HAS_TRAIT(this, TRAIT_WIELDED) ? 1 : 0), appearance_flags: RESET_COLOR);
        tip_overlay.color = tip_material.color;
        Dm.append(dot, tip_overlay);
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:169
    public override async DmTask<object> get_master_material()
    {
        datum_material tip_material = await get_material_from_slot(typeof(datum_material_slot_weapon_head_speartip)) as datum_material;
        if (tip_material == null)
            return await base.get_master_material();
        return Dm.truthy(custom_materials?[tip_material]) ? tip_material : await base.get_master_material();
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:175
    public override async DmTask<object> get_material_prefixes(DmList materials = null)
    {
        datum_material material = await get_material_from_slot(typeof(datum_material_slot_weapon_head_speartip)) as datum_material;
        return material?.name;
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:179
    public virtual object on_wield(obj_item source = null, mob_living_carbon user = null)
    {
        reach = 1;
        armour_penetration *= 2;
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:183
    public virtual object on_unwield(obj_item source = null, mob_living_carbon user = null)
    {
        reach = 2;
        armour_penetration /= 2;
        return null;
    }
}

// tg: code/game/objects/items/weaponry/melee/spear.dm:187
public partial class datum_material_slot_weapon_head_speartip : datum_material_slot_weapon_head
{
    // tg: code/game/objects/items/weaponry/melee/spear.dm:191
    public override async DmTask<object> on_applied(atom target = null, datum_material material = null, object amount = null, object multiplier = null)
    {
        await base.on_applied(target, material, amount, multiplier);
        if (!Dm.truthy(Dm.bitand(target.material_flags, MATERIAL_AFFECT_STATISTICS)))
            return FALSE;

        obj_item_spear spear = (obj_item_spear)target; // tg's parameter is obj/item/spear/target (rules 3.3)
        float density = Dm.num(material.get_property(MATERIAL_DENSITY));
        float hardness = Dm.num(material.get_property(MATERIAL_HARDNESS));
        // If a spear is too hard its unwieldy, if it is too light it doesn't have enough weight behind it
        float material_effect = (hardness - 4) - max(0, density - 4) - max(0, 4 - density) * 2;
        spear.wound_bonus += material_effect * 5;
        // These try to keep parity with titanium/plastitanium spears as armorpen boost was exclusive to them
        spear.armour_penetration += MATERIAL_PROPERTY_DIVERGENCE(hardness, 4, 6) * 5;
        spear.exposed_wound_bonus += (MATERIAL_PROPERTY_DIVERGENCE(hardness, 4, 6) - (density - 4)) * 5;
        return FALSE;
    }
}

// tg defines this proc on /datum/material_slot/weapon_head/spearhead, a path no spear uses (the tip is speartip): translated as
// written, so it never runs for a spear's tip.
// tg: code/game/objects/items/weaponry/melee/spear.dm:206
public partial class datum_material_slot_weapon_head_spearhead : datum_material_slot_weapon_head
{
    // tg: code/game/objects/items/weaponry/melee/spear.dm:206
    public override async DmTask<object> on_removed(atom target = null, datum_material material = null, object amount = null, object multiplier = null)
    {
        await base.on_removed(target, material, amount, multiplier);
        if (!Dm.truthy(Dm.bitand(target.material_flags, MATERIAL_AFFECT_STATISTICS)))
            return FALSE;

        obj_item_spear spear = (obj_item_spear)target; // tg's parameter is obj/item/spear/target (rules 3.3)
        float density = Dm.num(material.get_property(MATERIAL_DENSITY));
        float hardness = Dm.num(material.get_property(MATERIAL_HARDNESS));
        float material_effect = (hardness - 4) - max(0, density - 4) - max(0, 4 - density) * 2;
        spear.wound_bonus -= material_effect * 5;
        spear.armour_penetration -= MATERIAL_PROPERTY_DIVERGENCE(hardness, 4, 6) * 5;
        spear.exposed_wound_bonus -= (MATERIAL_PROPERTY_DIVERGENCE(hardness, 4, 6) - (density - 4)) * 5;
        return FALSE;
    }
}

// tg: code/game/objects/items/weaponry/melee/spear.dm:217
public partial class datum_material_slot_handle_spear : datum_material_slot_handle
{
    // tg: code/game/objects/items/weaponry/melee/spear.dm:219
    public override async DmTask<object> on_applied(atom target = null, datum_material material = null, object amount = null, object multiplier = null)
    {
        await base.on_applied(target, material, amount, multiplier);
        if (!Dm.truthy(Dm.bitand(target.material_flags, MATERIAL_AFFECT_STATISTICS)))
            return FALSE;

        obj_item item = (obj_item)target; // tg's parameter is obj/item/target (rules 3.3)
        float density = Dm.num(material.get_property(MATERIAL_DENSITY));
        float hardness = Dm.num(material.get_property(MATERIAL_HARDNESS));
        item.throw_range += (hardness - 4) - (density - 4) * 2;
        item.throw_speed += Dm.num(floor((hardness - 4) / 2)) - (density - 4) * 2;
        return FALSE;
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:230
    public override async DmTask<object> on_removed(atom target = null, datum_material material = null, object amount = null, object multiplier = null)
    {
        await base.on_removed(target, material, amount, multiplier);
        if (!Dm.truthy(Dm.bitand(target.material_flags, MATERIAL_AFFECT_STATISTICS)))
            return FALSE;

        obj_item item = (obj_item)target; // tg's parameter is obj/item/target (rules 3.3)
        float density = Dm.num(material.get_property(MATERIAL_DENSITY));
        float hardness = Dm.num(material.get_property(MATERIAL_HARDNESS));
        item.throw_range -= (hardness - 4) - (density - 4) * 2;
        item.throw_speed -= Dm.num(floor((hardness - 4) / 2)) - (density - 4) * 2;
        return FALSE;
    }
}

// tg: code/game/objects/items/weaponry/melee/spear.dm:243
public partial class obj_item_wireprod : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/spear.dm:259
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        datum_material shard_mat = null;
        if (tool is obj_item_shard)
            shard_mat = await tool.get_master_material() as datum_material;
        else if (tool is obj_item_stack)
        {
            shard_mat = await tool.get_master_material() as datum_material;
            if (!Dm.truthy(Dm.bitand(shard_mat.mat_flags, MATERIAL_CLASS_CRYSTAL)))
                shard_mat = null;
        }

        if (shard_mat == null)
            return NONE;

        obj_item_spear spear = Dm.@new<obj_item_spear>(drop_location());
        datum_material rod_material = await get_master_material() as datum_material;
        Dm.combine(ref spear.material_flags, MATERIAL_ADD_PREFIX);
        await spear.set_material_slot(typeof(datum_material_slot_handle_spear), await get_master_material());
        await spear.set_material_slot(typeof(datum_material_slot_weapon_head_speartip), shard_mat);
        await spear.set_custom_materials(Dm.list(Dm.kv(rod_material, custom_materials[rod_material]), Dm.kv(shard_mat, tool.custom_materials[shard_mat])));
        to_chat(user, span_notice(I18n.T("You attach [] to []'s tip.", tool, this)));

        if (tool is obj_item_stack stack)
            await stack.use(1);
        else
            qdel(tool);

        object was_holding = user.get_held_index_of_item(this);
        qdel(this);
        if (Dm.truthy(was_holding))
            await user.put_in_hands(spear);
        return null;
    }
}

// tg: code/game/objects/items/weaponry/melee/spear.dm:292
public partial class obj_item_spear_explosive : obj_item_spear
{
    // tg: code/game/objects/items/weaponry/melee/spear.dm:302
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(set_explosive(Dm.@new<obj_item_grenade_iedcasing_spawned>())); //For admin-spawned explosive lances
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:306
    public virtual async DmTask set_explosive(obj_item_grenade G = null)
    {
        if (explosive != null)
            QDEL_NULL(ref explosive);
        await G.forceMove(this);
        explosive = G;
        desc = I18n.T("A makeshift spear with [] attached to it", G);
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:313
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        obj_item_grenade nade = Dm.locate<obj_item_grenade>(components);
        if (nade != null)
        {
            obj_item_spear lancePart = Dm.locate<obj_item_spear>(components);
            throwforce = lancePart.throwforce;
            icon_prefix = lancePart.icon_prefix;
            await set_explosive(nade);
        }
        await base.on_craft_completion(components, current_recipe, crafter);
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:322
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins to sword-swallow \\the []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        await user.say(Dm.text("[]", war_cry), forced: "spear warcry");
        await explosive.forceMove(user);
        await explosive.detonate();
        await user.gib(DROP_ALL_REMAINS);
        qdel(this);
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:331
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        Dm.append(dot, span_notice(I18n.T("Alt-click to set your war cry.")));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:335
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        object input = await tgui_input_text(user, I18n.T("What do you want your war cry to be? You will shout it when you hit someone in melee."), I18n.T("War Cry"), max_length: 50);
        if (Dm.truthy(input))
            war_cry = input as string;
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:342
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!HAS_TRAIT(this, TRAIT_WIELDED) || target is not atom_movable)
            return;
        if (Dm.truthy(Dm.bitand(target.resistance_flags, INDESTRUCTIBLE))) //due to the lich incident of 2021, embedding grenades inside of indestructible structures is forbidden
            return;
        if (HAS_TRAIT(target, TRAIT_GODMODE))
            return;
        if (iseffect(target)) //and no accidentally wasting your moment of glory on graffiti
            return;
        await user.say(Dm.text("[]", war_cry), forced: "spear warcry");
        if (user is mob_living living_user)
        {
            await living_user.set_resting(new_resting: TRUE, silent: TRUE, instant: TRUE);
            await living_user.Move(get_turf(target));
            await explosive.forceMove(get_turf(living_user));
            await explosive.detonate(lanced_by: living_user);
            if (!QDELETED(living_user))
                await living_user.set_resting(new_resting: FALSE, silent: TRUE, instant: TRUE);
        }
        qdel(this);
    }
}

//GREY TIDE
// tg: code/game/objects/items/weaponry/melee/spear.dm:363
public partial class obj_item_spear_grey_tide : obj_item_spear
{
    // tg: code/game/objects/items/weaponry/melee/spear.dm:371
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        user.add_ally(Dm.text("greytide([])", REF(user)));
        if (target is not mob_living stabbed)
            return;
        if (stabbed is mob_living_basic_illusion)
            return;
        if (!IS_UNCONSCIOUS_OR_CRIT(stabbed) && prob(50))
        {
            mob_living_basic_illusion fake_clone = Dm.@new<mob_living_basic_illusion>(user.loc);
            await fake_clone.full_setup((mob_living)user, target_mob: stabbed, life: 10 * SECONDS, hp: ((mob_living)user).health / 2.5f, damage: 12, replicate: 30);
        }
    }
}

//MILITARY
// tg: code/game/objects/items/weaponry/melee/spear.dm:383
public partial class obj_item_spear_military : obj_item_spear
{
    // tg: code/game/objects/items/weaponry/melee/spear.dm:401
    public override void add_headpike_component()
    {
        DmList slapcraft_recipe_list = TgState.Current.obj_item_spear_military__add_headpike_component__slapcraft_recipe_list ??= Dm.list(typeof(datum_crafting_recipe_headpikemilitary));

        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), Dm.named("slapcraft_recipes", slapcraft_recipe_list)));
    }
}

/*
 * Anti-big monster spear
 * "WHERES MY DRAGONATOR?!"
 */
// tg: code/game/objects/items/weaponry/melee/spear.dm:413
public partial class obj_item_spear_dragonator : obj_item_spear
{
    // tg: code/game/objects/items/weaponry/melee/spear.dm:442
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_bane), Dm.named("affected_biotypes", MOB_MINING), Dm.named("added_damage", 80))); //For killing really big monsters.
        return dot;
    }
}

/*
 * Untreated Giantslayer , needs to be thrown into lava
 */
// tg: code/game/objects/items/weaponry/melee/spear.dm:449
public partial class obj_item_spear_dragonator_untreated : obj_item_spear
{
    // tg: code/game/objects/items/weaponry/melee/spear.dm:471
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        obj_item_spear_dragonator dragonator = Dm.@new<obj_item_spear_dragonator>(loc);
        await dragonator.set_material_slots(material_slots);
        await dragonator.set_custom_materials(custom_materials.Copy());
        playsound(dragonator.loc, Dm.resource("sound/effects/magic/staff_change.ogg"), 5);
        qdel(this);
        return null;
    }
}

/*
 * Bone Spear
 */
// tg: code/game/objects/items/weaponry/melee/spear.dm:481
public partial class obj_item_spear_bonespear : obj_item_spear
{
    // tg: code/game/objects/items/weaponry/melee/spear.dm:496
    public override void add_headpike_component()
    {
        DmList slapcraft_recipe_list = TgState.Current.obj_item_spear_bonespear__add_headpike_component__slapcraft_recipe_list ??= Dm.list(typeof(datum_crafting_recipe_headpikebone));

        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), Dm.named("slapcraft_recipes", slapcraft_recipe_list)));
    }
}

/*
 * Bamboo Spear
 */
// tg: code/game/objects/items/weaponry/melee/spear.dm:507
public partial class obj_item_spear_bamboospear : obj_item_spear
{
    // tg: code/game/objects/items/weaponry/melee/spear.dm:520
    public override void add_headpike_component()
    {
        DmList slapcraft_recipe_list = TgState.Current.obj_item_spear_bamboospear__add_headpike_component__slapcraft_recipe_list ??= Dm.list(typeof(datum_crafting_recipe_headpikebamboo));

        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), Dm.named("slapcraft_recipes", slapcraft_recipe_list)));
    }
}

/**
 * Skybulge
 *
 * Gives a special ability that allows you to enter the skies an drop down upon a target.
 * Other than that ability, is a default spear with extra throw force, but no embedding.
 */
// tg: code/game/objects/items/weaponry/melee/spear.dm:534
public partial class obj_item_spear_skybulge : obj_item_spear
{
}

///The action button the spear gives, usable once a minute.
// tg: code/game/objects/items/weaponry/melee/spear.dm:552
public partial class datum_action_item_action_skybulge : datum_action_item_action
{
    // tg: code/game/objects/items/weaponry/melee/spear.dm:569
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        if (!HAS_TRAIT(target, TRAIT_WIELDED))
        {
            owner.balloon_alert(owner, I18n.T("not dual-wielded!"));
            return null;
        }
        object time_left = S_TIMER_COOLDOWN_TIMELEFT(target, COOLDOWN_SKYBULGE_JUMP);
        if (Dm.truthy(time_left))
        {
            owner.balloon_alert(owner, I18n.T("[]s cooldown!", FLOOR(Dm.num(time_left) * 0.1f, 0.1f)));
            return null;
        }
        //do after shows the progress bar as feedback, so nothing here.
        if (Dm.truthy(LAZYACCESS(owner.do_afters, target)))
            return null;

        owner.balloon_alert(owner, I18n.T("charging up..."));
        ADD_TRAIT(target, TRAIT_NEEDS_TWO_HANDS, ACTION_TRAIT);
        Waiting.Detach(jump_up());
        return null;
    }

    ///Sends the owner up in the air and calls them back down, calling land() for aftereffects.
    // tg: code/game/objects/items/weaponry/melee/spear.dm:586
    public virtual async DmTask jump_up()
    {
        if (!Dm.truthy(await do_after(owner, 2 * SECONDS, target: owner, timed_action_flags: IGNORE_USER_LOC_CHANGE)))
        {
            REMOVE_TRAIT(target, TRAIT_NEEDS_TWO_HANDS, ACTION_TRAIT);
            return;
        }
        playsound(owner, Dm.resource("sound/effects/footstep/heavy1.ogg"), 50, 1);
        // S_TIMER_COOLDOWN_START(target, COOLDOWN_SKYBULGE_JUMP, jump_cooldown_time) (__DEFINES/cooldowns.dm:94, it expands addtimer: inline, rules 11.1 (f))
        LAZYSET(ref target.cooldowns, COOLDOWN_SKYBULGE_JUMP, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), target, COOLDOWN_SKYBULGE_JUMP), jump_cooldown_time, TIMER_STOPPABLE, file: "code/game/objects/items/weaponry/melee/spear.dm", line: 593));
        Dm.@new<obj_effect_temp_visual_telegraphing_exclamation_following>(get_turf(owner), 2.5f * SECONDS, owner);

        RegisterSignal(target, COMSIG_ITEM_ATTACK, nameof(on_attack_during_jump));
        ADD_TRAIT(target, TRAIT_NODROP, ACTION_TRAIT);
        owner.add_traits(Dm.list(TRAIT_SILENT_FOOTSTEPS, TRAIT_MOVE_FLYING), ACTION_TRAIT);

        if (Dm.truthy(Dm.bitand(owner.pass_flags, PASSTABLE)))
            gave_pass_flags = FALSE;
        else
        {
            gave_pass_flags = TRUE;
            Dm.combine(ref owner.pass_flags, PASSTABLE);
        }

        owner.set_density(FALSE);
        owner.layer = ABOVE_ALL_MOB_LAYER;

        animate(owner, Dm.named("pixel_y", owner.pixel_y + 60), Dm.named("time", 2 * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_OUT)));
        animate(Dm.named("pixel_y", Dm.initial(owner, (atom p) => p.pixel_y)), Dm.named("time", 1 * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_IN)));

        jump_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(land), /*do_effects = */TRUE, /*mob_override = */owner), 3 * SECONDS, TIMER_STOPPABLE, file: "code/game/objects/items/weaponry/melee/spear.dm", line: 610);
    }

    // tg: code/game/objects/items/weaponry/melee/spear.dm:612
    public override void update_status_on_signal(datum source = null, object new_stat = null, object old_stat = null)
    {
        if (jump_timer != null && !Dm.truthy(Waiting.NoSleep(IsAvailable())))
        {
            Waiting.Detach(land(/*do_effects = */FALSE, /*mob_override = */source as mob_living));
            deltimer(jump_timer);
        }
        base.update_status_on_signal(source, new_stat, old_stat);
    }

    /**
     * ## land()
     *
     * Called by jump_up, this is the post-jump effects, damaging objects and mobs it lands on.
     * Args:
     * do_effects - Whether we'll do the attacking effects of the land (damaging mobs & sound),
     * we set this to false if we were forced out of the jump, they lost their ability to do the hit.
     * mob_doing_effects - This is who we use for aftereffects, passing the mob using the ability, with owner as fallback.
     * ourselves.
     */
    // tg: code/game/objects/items/weaponry/melee/spear.dm:628
    public virtual async DmTask land(object do_effects = null, mob_living mob_doing_effects = null)
    {
        do_effects ??= TRUE;
        if (mob_doing_effects == null)
            mob_doing_effects = owner as mob_living;
        turf landed_on = get_turf(mob_doing_effects);

        UnregisterSignal(target, COMSIG_ITEM_ATTACK);
        target.remove_traits(Dm.list(TRAIT_NEEDS_TWO_HANDS, TRAIT_NODROP), ACTION_TRAIT);
        mob_doing_effects.remove_traits(Dm.list(TRAIT_SILENT_FOOTSTEPS, TRAIT_MOVE_FLYING), ACTION_TRAIT);
        if (Dm.truthy(gave_pass_flags))
            Dm.mask(ref mob_doing_effects.pass_flags, Dm.bitnot(PASSTABLE));
        mob_doing_effects.set_density(TRUE);
        mob_doing_effects.layer = Dm.num(Dm.initial(mob_doing_effects, (atom p) => p.layer));
        SET_PLANE(mob_doing_effects, Dm.initial(mob_doing_effects, (atom p) => p.plane), landed_on);

        if (!Dm.truthy(do_effects))
            return;

        playsound(mob_doing_effects, Dm.resource("sound/effects/explosion/explosion1.ogg"), 40, 1);
        obj_item skybulge_item = target as obj_item;

        foreach (atom thing in Dm.each<atom>(landed_on.contents.Copy()))
        {
            if (thing == mob_doing_effects)
                continue;

            if (thing is obj thing_obj)
            {
                await thing_obj.take_damage(150);
                continue;
            }

            if (thing is mob_living living_target)
            {
                await skybulge_item.melee_attack_chain(owner, living_target, Dm.list(Dm.kv(Dm.text("[]", FORCE_MULTIPLIER), 2)));
                await skybulge_item.attack__proc(living_target, owner as mob_living);
                await living_target.SetKnockdown(1 * SECONDS);
            }
        }
    }

    ///Called when the person holding us is trying to attack something mid-jump.
    ///You're technically in mid-air, so block any attempts at getting extra hits in.
    // tg: code/game/objects/items/weaponry/melee/spear.dm:664
    [SignalHandler]
    public virtual object on_attack_during_jump(atom source = null, mob_living target_mob = null, mob_living user = null, object @params = null)
    {
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/weaponry/melee/spear.dm:76  var/static/list/slapcraft_recipe_list (/obj/item/spear/add_headpike_component)
    internal DmList obj_item_spear__add_headpike_component__slapcraft_recipe_list;
    // tg: code/game/objects/items/weaponry/melee/spear.dm:402  var/static/list/slapcraft_recipe_list (the military javelin's)
    internal DmList obj_item_spear_military__add_headpike_component__slapcraft_recipe_list;
    // tg: code/game/objects/items/weaponry/melee/spear.dm:497  var/static/list/slapcraft_recipe_list (the bone spear's)
    internal DmList obj_item_spear_bonespear__add_headpike_component__slapcraft_recipe_list;
    // tg: code/game/objects/items/weaponry/melee/spear.dm:521  var/static/list/slapcraft_recipe_list (the bamboo spear's)
    internal DmList obj_item_spear_bamboospear__add_headpike_component__slapcraft_recipe_list;
}
