// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/armor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice B: /obj/item/clothing/suit/armor takes the security vest's allowed list, so a disabler hangs on the officer's vest. Gaps
// CL1 (D333): the rest of the file (the cuirass' and the SWAT suit's rustle, the formal coats' toggled icon, the balloon vest that
// pops, the reflective vest, the durathread and elder atmosian allowed lists, the durability suits and the holymelon's
// anti-magic). The fishing difficulty elements and the heavy suit's mesh texture wait for fishing and the bodypart textures, the
// press vest's and the security jacket's emissive overlays are DV12 (not-translated.md 209). The fantasy bonuses are live-partial
// stubs. The var blocks are generated (D111).

// tg: code/modules/clothing/suits/armor.dm:1
public partial class obj_item_clothing_suit_armor : obj_item_clothing_suit
{
    // tg: code/modules/clothing/suits/armor.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(allowed))
            allowed = GLOB.security_vest_allowed;
        return dot;
    }
}

// tg: code/modules/clothing/suits/armor.dm:64
public partial class obj_item_clothing_suit_armor_vest_press : obj_item_clothing_suit_armor_vest
{
    // tg: code/modules/clothing/suits/armor.dm:69
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        // DEVIATION(DV12): if(!isinhands) . += emissive_appearance(icon_file, "[icon_state]-emissive", src, alpha = src.alpha,
        // effect_type = EMISSIVE_SPECULAR): the press vest's lettering on the emissive plane is not created
        return dot;
    }
}

// tg: code/modules/clothing/suits/armor.dm:143
public partial class obj_item_clothing_suit_armor_vest_cuirass : obj_item_clothing_suit_armor_vest
{
    // tg: code/modules/clothing/suits/armor.dm:150
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_item_equipped_movement_rustle), SFX_PLATE_ARMOR_RUSTLE, 8));
        return dot;
    }
}

// tg: code/modules/clothing/suits/armor.dm:221
public partial class obj_item_clothing_suit_armor_vest_secjacket : obj_item_clothing_suit_armor_vest
{
    // tg: code/modules/clothing/suits/armor.dm:234
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        // DEVIATION(DV12): if(!isinhands) . += emissive_appearance(icon_file, "[icon_state]-emissive", src, alpha = src.alpha,
        // effect_type = EMISSIVE_SPECULAR): the jacket's reflective stripes on the emissive plane are not created
        return dot;
    }
}

// tg: code/modules/clothing/suits/armor.dm:286
public partial class obj_item_clothing_suit_armor_vest_capcarapace_captains_formal : obj_item_clothing_suit_armor_vest_capcarapace
{
    // tg: code/modules/clothing/suits/armor.dm:294
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_toggle_icon)));
        return dot;
    }
}

// tg: code/modules/clothing/suits/armor.dm:299
public partial class obj_item_clothing_suit_armor_riot : obj_item_clothing_suit_armor
{
    // tg: code/modules/clothing/suits/armor.dm:313
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/adjust_fishing_difficulty, 5): fishing difficulty (section 105)
        Dm.Deferred("code/modules/clothing/suits/armor.dm:315", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, 5), fishing difficulty; AddElement(/datum/element/equipment_bodypart_texture, BODY_ZONE_CHEST, texture_type), the mesh texture on the chest");
        init_rustle_component();
        return dot;
    }

    // tg: code/modules/clothing/suits/armor.dm:319
    public virtual void init_rustle_component()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_item_equipped_movement_rustle)));
    }
}

// tg: code/modules/clothing/suits/armor.dm:331
public partial class obj_item_clothing_suit_armor_balloon_vest : obj_item_clothing_suit_armor
{
    // tg: code/modules/clothing/suits/armor.dm:350
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (isitem(hitby))
        {
            obj_item item_hit = (obj_item)hitby;
            if (Dm.truthy(item_hit.get_sharpness()))
                await pop();
        }

        if (hitby is obj_projectile_bullet)
            await pop();

        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }

    // tg: code/modules/clothing/suits/armor.dm:361
    public virtual async DmTask pop()
    {
        playsound(this, Dm.resource("sound/effects/cartoon_sfx/cartoon_pop.ogg"), 50, vary: TRUE);
        qdel(this);
    }
}

// tg: code/modules/clothing/suits/armor.dm:386
public partial class obj_item_clothing_suit_armor_laserproof : obj_item_clothing_suit_armor
{
    // tg: code/modules/clothing/suits/armor.dm:408
    public override async DmTask<object> IsReflect(object def_zone = null)
    {
        if (!Dm.@in(def_zone, Dm.list(BODY_ZONE_CHEST, BODY_ZONE_PRECISE_GROIN, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM))) //If not shot where ablative is covering you, you don't get the reflection bonus!
            return FALSE;
        if (prob(hit_reflect_chance))
            return TRUE;
        return null;
    }
}

// tg: code/modules/clothing/suits/armor.dm:425
public partial class obj_item_clothing_suit_armor_swat : obj_item_clothing_suit_armor
{
    // tg: code/modules/clothing/suits/armor.dm:443
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/suits/armor.dm:445", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, 5): fishing (M10+)");
        init_rustle_component();
        return dot;
    }

    // tg: code/modules/clothing/suits/armor.dm:448
    public virtual void init_rustle_component()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_item_equipped_movement_rustle)));
    }
}

// tg: code/modules/clothing/suits/armor.dm:465
public partial class obj_item_clothing_suit_armor_heavy : obj_item_clothing_suit_armor
{
    // tg: code/modules/clothing/suits/armor.dm:479
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/suits/armor.dm:481", "Initialize: AddElement(/datum/element/equipment_bodypart_texture, BODY_ZONE_CHEST, texture_type): the mesh texture on the chest (bodypart textures, M10+)");
        return dot;
    }
}

// tg: code/modules/clothing/suits/armor.dm:549
public partial class obj_item_clothing_suit_armor_riot_knight : obj_item_clothing_suit_armor_riot
{
    // tg: code/modules/clothing/suits/armor.dm:557
    public override void init_rustle_component()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_item_equipped_movement_rustle), SFX_PLATE_ARMOR_RUSTLE, 8));
    }
}

// tg: code/modules/clothing/suits/armor.dm:415
public partial class obj_item_clothing_suit_armor_vest_det_suit : obj_item_clothing_suit_armor_vest
{
    // tg: code/modules/clothing/suits/armor.dm:421
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        allowed = GLOB.detective_vest_allowed;
        return dot;
    }
}

// tg: code/modules/clothing/suits/armor.dm:187  (gaps J4: the head of security's garment bag)
public partial class obj_item_clothing_suit_armor_hos_hos_formal : obj_item_clothing_suit_armor_hos
{
    // tg: code/modules/clothing/suits/armor.dm:198
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_toggle_icon)));
        return dot;
    }
}

// tg: code/modules/clothing/suits/armor.dm:590
public partial class obj_item_clothing_suit_armor_vest_durathread : obj_item_clothing_suit_armor_vest
{
    // tg: code/modules/clothing/suits/armor.dm:602
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.combine(ref allowed, Dm.initial(typeof(obj_item_clothing_suit_apron), (obj_item_clothing_suit_apron p) => p.allowed));
        return dot;
    }
}

// tg: code/modules/clothing/suits/armor.dm:656
public partial class obj_item_clothing_suit_armor_elder_atmosian : obj_item_clothing_suit_armor
{
    // tg: code/modules/clothing/suits/armor.dm:667
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, Dm.list(
            typeof(obj_item_fireaxe_metal_h2_axe),
            typeof(obj_item_gun_ballistic_rifle_rebarxbow)
        ));
        return dot;
    }
}

// tg: code/modules/clothing/suits/armor.dm:685
public partial class obj_item_clothing_suit_armor_centcom_formal : obj_item_clothing_suit_armor
{
    // tg: code/modules/clothing/suits/armor.dm:704
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_toggle_icon)));
        return dot;
    }
}

// tg: code/modules/clothing/suits/armor.dm:737
public partial class obj_item_clothing_suit_armor_vest_military : obj_item_clothing_suit_armor_vest
{
    // tg: code/modules/clothing/suits/armor.dm:752
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/suits/armor.dm:754", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, 5): fishing (M10+)");
        return dot;
    }
}

// tg: code/modules/clothing/suits/armor.dm:786
public partial class obj_item_clothing_suit_armor_durability : obj_item_clothing_suit_armor
{
    // tg: code/modules/clothing/suits/armor.dm:789
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        await take_damage(1, BRUTE, 0, 0);
        return null;
    }
}

// tg: code/modules/clothing/suits/armor.dm:826
public partial class obj_item_clothing_suit_armor_durability_holymelon : obj_item_clothing_suit_armor_durability
{
    // tg: code/modules/clothing/suits/armor.dm:842
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(AddComponent(
            typeof(datum_component_anti_magic),
            Dm.named("antimagic_flags", Dm.bitor(MAGIC_RESISTANCE, MAGIC_RESISTANCE_HOLY)),
            Dm.named("inventory_flags", ITEM_SLOT_OCLOTHING),
            Dm.named("charges", 1),
            Dm.named("block_magic", Dm.@new<datum_callback>(this, nameof(drain_antimagic))),
            Dm.named("expiration", Dm.@new<datum_callback>(this, nameof(decay)))
        ));
        return dot;
    }

    // tg: code/modules/clothing/suits/armor.dm:854
    public virtual void drain_antimagic(mob user = null)
    {
        to_chat(user, span_warning(I18n.T("[] looses a bit of its shimmer and glossiness...", this)));
    }

    // tg: code/modules/clothing/suits/armor.dm:857
    public virtual async DmTask decay()
    {
        await take_damage(8, BRUTE, 0, 0);
    }
}
