// Translated from tgstation (snapshot 1d99bd7f): code/datums/materials/basemats.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file. The var blocks are generated (D111). The fishing rods' rewards are
// translated. The combustible flooder of plasma and hot ice and mythril's fantasy component are not translated here: their
// calls stay Dm.NotTranslated with the reason. Bananium's squeak and slip are (M10 slice F: datums/components/squeak.cs).

// tg: code/datums/materials/basemats.dm:2
public partial class datum_material_iron : datum_material
{
    // tg: code/datums/materials/basemats.dm:28
    public override async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        object dot = await base.on_accidental_mat_consumption(victim, source_item);
        if (!HAS_TRAIT(victim, TRAIT_ROCK_EATER))
        {
            await victim.apply_damage(10, BRUTE, BODY_ZONE_HEAD, wound_bonus: 5);
            return TRUE;
        }
        return dot;
    }
}

///Breaks extremely easily but is transparent.
// tg: code/datums/materials/basemats.dm:35
public partial class datum_material_glass : datum_material
{
    // tg: code/datums/materials/basemats.dm:64
    public override async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        object dot = await base.on_accidental_mat_consumption(victim, source_item);
        if (!HAS_TRAIT(victim, TRAIT_ROCK_EATER))
        {
            await victim.apply_damage(10, BRUTE, BODY_ZONE_HEAD, wound_bonus: 5, sharpness: TRUE);
            return TRUE;
        }
        return dot;
    }
    // tg: code/datums/materials/basemats.dm:70
    public override async DmTask on_main_applied(atom source = null, object mat_amount = null, object multiplier = null)
    {
        await base.on_main_applied(source, mat_amount, multiplier);
        if (isobj(source) && !isstack(source) && Dm.truthy(Dm.bitand(source.material_flags, MATERIAL_AFFECT_STATISTICS)))
            await source.AddElement(typeof(datum_element_can_shatter), shard_type, Dm.round(Dm.num(mat_amount) / SHEET_MATERIAL_AMOUNT * Dm.num(multiplier)), SFX_SHATTER);
    }

    // tg: code/datums/materials/basemats.dm:75
    public override void on_main_removed(atom source = null, object mat_amount = null, object multiplier = null)
    {
        base.on_main_removed(source, mat_amount, multiplier);
        if (isobj(source) && !isstack(source) && Dm.truthy(Dm.bitand(source.material_flags, MATERIAL_AFFECT_STATISTICS)))
            source.RemoveElement(typeof(datum_element_can_shatter), shard_type, Dm.round(Dm.num(mat_amount) / SHEET_MATERIAL_AMOUNT * Dm.num(multiplier)), SFX_SHATTER);
    }
}

/// Gives weapons an edge against vampires and possibly other mobs with the UNHOLY_BANEABLE trait.
// tg: code/datums/materials/basemats.dm:81
public partial class datum_material_silver : datum_material
{
    // tg: code/datums/materials/basemats.dm:107
    public override async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        object dot = await base.on_accidental_mat_consumption(victim, source_item);
        if (!HAS_TRAIT(victim, TRAIT_ROCK_EATER))
        {
            await victim.apply_damage(10, BRUTE, BODY_ZONE_HEAD, wound_bonus: 5);
            return TRUE;
        }
        return dot;
    }
}

///Slight force increase
// tg: code/datums/materials/basemats.dm:114
public partial class datum_material_gold : datum_material
{
    // tg: code/datums/materials/basemats.dm:139
    public override async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        object dot = await base.on_accidental_mat_consumption(victim, source_item);
        if (!HAS_TRAIT(victim, TRAIT_ROCK_EATER))
        {
            await victim.apply_damage(10, BRUTE, BODY_ZONE_HEAD, wound_bonus: 5);
            return TRUE;
        }
        return dot;
    }
}

///Has no special properties
// tg: code/datums/materials/basemats.dm:146
public partial class datum_material_diamond : datum_material
{
    // tg: code/datums/materials/basemats.dm:172
    public override async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        object dot = await base.on_accidental_mat_consumption(victim, source_item);
        if (!HAS_TRAIT(victim, TRAIT_ROCK_EATER))
        {
            await victim.apply_damage(15, BRUTE, BODY_ZONE_HEAD, wound_bonus: 7);
            return TRUE;
        }
        return dot;
    }
}

/// Is slightly radioactive
// tg: code/datums/materials/basemats.dm:179
public partial class datum_material_uranium : datum_material
{
}

// tg: code/datums/materials/basemats.dm:206
public partial class datum_material_plasma : datum_material
{
    // tg: code/datums/materials/basemats.dm:230
    public override async DmTask on_applied(atom source = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        await base.on_applied(source, mat_amount, multiplier, from_slot);
        await source.AddComponent(typeof(datum_component_combustible_flooder), GAS_PLASMA, Dm.num(mat_amount) * 0.05f * Dm.num(multiplier)); //Empty temp arg, fully dependent on whatever ignited it.
        if (istype(source, typeof(obj_item_fishing_rod)))
            ADD_TRAIT(source, TRAIT_ROD_LAVA_USABLE, REF(this));
    }

    // tg: code/datums/materials/basemats.dm:236
    public override void on_removed(atom source = null, object mat_amount = null, object material_flags = null, object from_slot = null)
    {
        base.on_removed(source, mat_amount, material_flags, from_slot);
        qdel(source.GetComponent(typeof(datum_component_combustible_flooder)));
        if (istype(source, typeof(obj_item_fishing_rod)))
            ADD_TRAIT(source, TRAIT_ROD_LAVA_USABLE, REF(this));
    }
}

/// Can cause bluespace effects on use. (Teleportation)
// tg: code/datums/materials/basemats.dm:243
public partial class datum_material_bluespace : datum_material
{
    // tg: code/datums/materials/basemats.dm:272
    public override async DmTask on_main_applied(atom source = null, object mat_amount = null, object multiplier = null)
    {
        await base.on_main_applied(source, mat_amount, multiplier);
        if (istype(source, typeof(obj_item_fishing_rod)))
            RegisterSignal(source, COMSIG_ROD_BEGIN_FISHING, nameof(on_begin_fishing));
    }
    // tg: code/datums/materials/basemats.dm:277
    [SignalHandler]
    public virtual void on_begin_fishing(obj_item_fishing_rod rod = null, datum_fishing_challenge challenge = null)
    {
        if (prob(67))
            return;
        DmList elegible_fish_sources = assoc_to_values(GLOB.preset_fish_sources) as DmList;
        foreach (datum_fish_source source in Dm.each<datum_fish_source>(elegible_fish_sources.Copy()))
        {
            if (Dm.truthy(Dm.bitand(source.fish_source_flags, FISH_SOURCE_FLAG_NO_BLUESPACE_ROD)))
                Dm.remove(elegible_fish_sources, source);
        }
        datum_fish_source new_source = pick(elegible_fish_sources) as datum_fish_source;
        challenge.register_reward_signals(new_source);
    }
    // tg: code/datums/materials/basemats.dm:288
    public override void on_main_removed(atom source = null, object mat_amount = null, object multiplier = null)
    {
        base.on_main_removed(source, mat_amount, multiplier);
        if (istype(source, typeof(obj_item_fishing_rod)))
            UnregisterSignal(source, COMSIG_ROD_BEGIN_FISHING);
    }
}

///Honks and slips
// tg: code/datums/materials/basemats.dm:294
public partial class datum_material_bananium : datum_material
{
    // tg: code/datums/materials/basemats.dm:318
    public override async DmTask on_applied(atom source = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        await base.on_applied(source, mat_amount, multiplier, from_slot);
        await source.LoadComponent(typeof(datum_component_squeak), Dm.list(Dm.kv(Dm.resource("sound/items/bikehorn.ogg"), 1)), 50 * Dm.num(multiplier), Dm.named("falloff_exponent", 20));
        await source.AddComponent(typeof(datum_component_slippery), min(Dm.num(mat_amount) / 10 * Dm.num(multiplier), 80 * Dm.num(multiplier)));
    }
    // tg: code/datums/materials/basemats.dm:323
    public override async DmTask on_main_applied(atom source = null, object mat_amount = null, object multiplier = null)
    {
        await base.on_main_applied(source, mat_amount, multiplier);
        if (istype(source, typeof(obj_item_fishing_rod)))
            RegisterSignal(source, COMSIG_ROD_BEGIN_FISHING, nameof(on_begin_fishing));
    }
    // tg: code/datums/materials/basemats.dm:328
    [SignalHandler]
    public virtual void on_begin_fishing(obj_item_fishing_rod rod = null, datum_fishing_challenge challenge = null)
    {
        if (prob(40))
            RegisterSignal(challenge, COMSIG_FISHING_CHALLENGE_ROLL_REWARD, nameof(roll_funny_fish));
    }
    // tg: code/datums/materials/basemats.dm:333
    [SignalHandler]
    public virtual void roll_funny_fish(datum source = null, obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null, DmList rewards = null)
    {
        DmList funny_fish = TgState.Current.datum_material_bananium_roll_funny_fish__funny_fish ??= Dm.list(
            Dm.kv(typeof(obj_item_fish_clownfish), 5),
            Dm.kv(typeof(obj_item_fish_clownfish_lube), 3),
            Dm.kv(typeof(obj_item_fish_soul), 2),
            Dm.kv(typeof(obj_item_fish_skin_crab), 2),
            Dm.kv(typeof(obj_item_fish_donkfish), 2));
        Dm.append(rewards, pick_weight(funny_fish));
    }
    // tg: code/datums/materials/basemats.dm:344
    public override void on_removed(atom source = null, object mat_amount = null, object material_flags = null, object from_slot = null)
    {
        base.on_removed(source, mat_amount, material_flags, from_slot);
        qdel(source.GetComponent(typeof(datum_component_slippery)));
        qdel(source.GetComponent(typeof(datum_component_squeak)));
    }
    // tg: code/datums/materials/basemats.dm:349
    public override void on_main_removed(atom source = null, object mat_amount = null, object multiplier = null)
    {
        base.on_main_removed(source, mat_amount, multiplier);
        if (istype(source, typeof(obj_item_fishing_rod)))
            UnregisterSignal(source, COMSIG_ROD_BEGIN_FISHING);
    }
}

///Mediocre force increase
// tg: code/datums/materials/basemats.dm:355
public partial class datum_material_titanium : datum_material
{
    // tg: code/datums/materials/basemats.dm:379
    public override async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        object dot = await base.on_accidental_mat_consumption(victim, source_item);
        if (!HAS_TRAIT(victim, TRAIT_ROCK_EATER))
        {
            await victim.apply_damage(15, BRUTE, BODY_ZONE_HEAD, wound_bonus: 7);
            return TRUE;
        }
        return dot;
    }
}

// tg: code/datums/materials/basemats.dm:385
public partial class datum_material_runite : datum_material
{
    // tg: code/datums/materials/basemats.dm:405
    public override async DmTask on_applied(atom source = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        await base.on_applied(source, mat_amount, multiplier, from_slot);
        if (istype(source, typeof(obj_item_fishing_rod)))
            ADD_TRAIT(source, TRAIT_ROD_REMOVE_FISHING_DUD, REF(this)); // light-absorbing, environment-cancelling fishing rod.
    }
    // tg: code/datums/materials/basemats.dm:410
    public override void on_removed(atom source = null, object mat_amount = null, object material_flags = null, object from_slot = null)
    {
        base.on_removed(source, mat_amount, material_flags, from_slot);
        if (istype(source, typeof(obj_item_fishing_rod)))
            REMOVE_TRAIT(source, TRAIT_ROD_REMOVE_FISHING_DUD, REF(this)); // light-absorbing, environment-cancelling fishing rod.
    }
    // tg: code/datums/materials/basemats.dm:415
    public override async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        object dot = await base.on_accidental_mat_consumption(victim, source_item);
        if (!HAS_TRAIT(victim, TRAIT_ROCK_EATER))
        {
            await victim.apply_damage(20, BRUTE, BODY_ZONE_HEAD, wound_bonus: 10);
            return TRUE;
        }
        return dot;
    }
}

// tg: code/datums/materials/basemats.dm:422
public partial class datum_material_plastic : datum_material
{
}

/// Force decrease and mushy sound effect. (Not yet implemented)
// tg: code/datums/materials/basemats.dm:446
public partial class datum_material_biomass : datum_material
{
}

// tg: code/datums/materials/basemats.dm:452
public partial class datum_material_wood : datum_material
{
    // tg: code/datums/materials/basemats.dm:473
    public override async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        object dot = await base.on_accidental_mat_consumption(victim, source_item);
        if (!HAS_TRAIT(victim, TRAIT_ROCK_EATER))
        {
            await victim.apply_damage(5, BRUTE, BODY_ZONE_HEAD);
            return TRUE;
        }
        return dot;
    }
}

///Force decrease
// tg: code/datums/materials/basemats.dm:480
public partial class datum_material_adamantine : datum_material
{
    // tg: code/datums/materials/basemats.dm:500
    public override async DmTask on_applied(atom source = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        await base.on_applied(source, mat_amount, multiplier, from_slot);
        if (istype(source, typeof(obj_item_fishing_rod)))
            ADD_TRAIT(source, TRAIT_ROD_REMOVE_FISHING_DUD, REF(this)); // light-absorbing, environment-cancelling fishing rod.
    }
    // tg: code/datums/materials/basemats.dm:505
    public override void on_removed(atom source = null, object mat_amount = null, object material_flags = null, object from_slot = null)
    {
        base.on_removed(source, mat_amount, material_flags, from_slot);
        if (istype(source, typeof(obj_item_fishing_rod)))
            REMOVE_TRAIT(source, TRAIT_ROD_REMOVE_FISHING_DUD, REF(this)); // light-absorbing, environment-cancelling fishing rod.
    }
    // tg: code/datums/materials/basemats.dm:510
    public override async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        object dot = await base.on_accidental_mat_consumption(victim, source_item);
        if (!HAS_TRAIT(victim, TRAIT_ROCK_EATER))
        {
            await victim.apply_damage(20, BRUTE, BODY_ZONE_HEAD, wound_bonus: 10);
            return TRUE;
        }
        return dot;
    }
}

/// Stronk force increase
// tg: code/datums/materials/basemats.dm:517
public partial class datum_material_mythril : datum_material
{
    // tg: code/datums/materials/basemats.dm:539
    public override async DmTask on_applied(atom source = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        await base.on_applied(source, mat_amount, multiplier, from_slot);
        if (isitem(source))
            // a deferred leaf (not-translated.md section 1): nothing reads a fantasy affix, and every world that has a slot machine makes a
            // mythril coin (slotmachine.dm:99-102)
            Dm.Deferred("code/datums/materials/basemats.dm:541", "on_applied: AddComponent(/datum/component/fantasy) and TRAIT_INNATELY_FANTASTICAL_ITEM (the fantasy affixes)");
    }

    // tg: code/datums/materials/basemats.dm:545
    public override void on_removed(atom source = null, object mat_amount = null, object material_flags = null, object from_slot = null)
    {
        base.on_removed(source, mat_amount, material_flags, from_slot);
        if (isitem(source))
            Dm.Deferred("code/datums/materials/basemats.dm:547", "on_removed: REMOVE_TRAIT(TRAIT_INNATELY_FANTASTICAL_ITEM) and the fantasy component (the fantasy affixes)");
    }
    // tg: code/datums/materials/basemats.dm:551
    public override async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        object dot = await base.on_accidental_mat_consumption(victim, source_item);
        if (!HAS_TRAIT(victim, TRAIT_ROCK_EATER))
        {
            await victim.apply_damage(20, BRUTE, BODY_ZONE_HEAD, wound_bonus: 10);
            return TRUE;
        }
        return dot;
    }
}

/// RPG Magic.
// tg: code/datums/materials/basemats.dm:558
public partial class datum_material_hot_ice : datum_material
{
    // tg: code/datums/materials/basemats.dm:580
    public override async DmTask on_applied(atom source = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        await base.on_applied(source, mat_amount, multiplier, from_slot);
        await source.AddComponent(typeof(datum_component_combustible_flooder), GAS_PLASMA, Dm.num(mat_amount) * 1.5f * Dm.num(multiplier), (Dm.num(mat_amount) * 0.2f + 300) * Dm.num(multiplier));
    }

    // tg: code/datums/materials/basemats.dm:584
    public override void on_removed(atom source = null, object mat_amount = null, object material_flags = null, object from_slot = null)
    {
        base.on_removed(source, mat_amount, material_flags, from_slot);
        qdel(source.GetComponent(typeof(datum_component_combustible_flooder)));
    }
}

//formed when freon react with o2, emits a lot of plasma when heated
// tg: code/datums/materials/basemats.dm:589
public partial class datum_material_metalhydrogen : datum_material
{
    // tg: code/datums/materials/basemats.dm:608
    public override async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        object dot = await base.on_accidental_mat_consumption(victim, source_item);
        if (!HAS_TRAIT(victim, TRAIT_ROCK_EATER))
        {
            await victim.apply_damage(15, BRUTE, BODY_ZONE_HEAD, wound_bonus: 7);
            return TRUE;
        }
        return dot;
    }
}

// It's basically adamantine, but it isn't!
// tg: code/datums/materials/basemats.dm:615
public partial class datum_material_sand : datum_material
{
    // tg: code/datums/materials/basemats.dm:636
    public override async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        await base.on_accidental_mat_consumption(victim, source_item);
        victim.adjust_disgust(17);
        return TRUE;
    }
}

//I don't like sand. It's coarse, and rough, and irritating, and it gets everywhere.
// tg: code/datums/materials/basemats.dm:642
public partial class datum_material_sandstone : datum_material
{
}

// tg: code/datums/materials/basemats.dm:663
public partial class datum_material_snow : datum_material
{
}

// tg: code/datums/materials/basemats.dm:683
public partial class datum_material_runedmetal : datum_material
{
    // tg: code/datums/materials/basemats.dm:702
    public override async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        object dot = await base.on_accidental_mat_consumption(victim, source_item);
        if (!HAS_TRAIT(victim, TRAIT_ROCK_EATER))
        {
            await victim.apply_damage(10, BRUTE, BODY_ZONE_HEAD, wound_bonus: 5);
        }
        return TRUE;
    }
}

// tg: code/datums/materials/basemats.dm:708
public partial class datum_material_bronze : datum_material
{
}

//And now for our lavaland dwelling friends, sand, but in stone form! Truly revolutionary.
// tg: code/datums/materials/basemats.dm:726
public partial class datum_material_paper : datum_material
{
    // tg: code/datums/materials/basemats.dm:748
    public override async DmTask on_main_applied(atom source = null, object mat_amount = null, object multiplier = null)
    {
        await base.on_main_applied(source, mat_amount, multiplier);
        if (!isobj(source) || !Dm.truthy(Dm.bitand(source.material_flags, MATERIAL_AFFECT_STATISTICS)))
            return;
        obj paper = source as obj;
        Dm.combine(ref paper.obj_flags, UNIQUE_RENAME);
        if (istype(paper, typeof(obj_item_fishing_rod)))
            RegisterSignal(paper, COMSIG_ROD_BEGIN_FISHING, nameof(on_begin_fishing));
    }
    // tg: code/datums/materials/basemats.dm:757
    [SignalHandler]
    public virtual void on_begin_fishing(obj_item_fishing_rod rod = null, datum_fishing_challenge challenge = null)
    {
        if (prob(40)) //consider the default reward and it's 15%
            RegisterSignal(challenge, COMSIG_FISHING_CHALLENGE_ROLL_REWARD, nameof(roll_stickman));
    }
    // tg: code/datums/materials/basemats.dm:762
    [SignalHandler]
    public virtual void roll_stickman(datum source = null, obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null, DmList rewards = null)
    {
        Dm.append(rewards, pick(typeof(mob_living_basic_stickman), typeof(mob_living_basic_stickman_dog), typeof(mob_living_basic_stickman_ranged)));
    }
    // tg: code/datums/materials/basemats.dm:766
    public override void on_main_removed(atom source = null, object mat_amount = null, object multiplier = null)
    {
        base.on_main_removed(source, mat_amount, multiplier);
        if (istype(source, typeof(obj_item_fishing_rod)) && Dm.truthy(Dm.bitand(source.material_flags, MATERIAL_AFFECT_STATISTICS)))
            UnregisterSignal(source, COMSIG_ROD_BEGIN_FISHING);
    }
}

// tg: code/datums/materials/basemats.dm:771
public partial class datum_material_cardboard : datum_material
{
    // tg: code/datums/materials/basemats.dm:790
    public override async DmTask on_main_applied(atom source = null, object mat_amount = null, object multiplier = null)
    {
        await base.on_main_applied(source, mat_amount, multiplier);
        if (isobj(source) && Dm.truthy(Dm.bitand(source.material_flags, MATERIAL_AFFECT_STATISTICS)))
        {
            obj cardboard = source as obj;
            Dm.combine(ref cardboard.obj_flags, UNIQUE_RENAME);
        }
    }
}

// tg: code/datums/materials/basemats.dm:796
public partial class datum_material_bone : datum_material
{
    // tg: code/datums/materials/basemats.dm:814
    public override async DmTask on_main_applied(atom source = null, object mat_amount = null, object multiplier = null)
    {
        await base.on_main_applied(source, mat_amount, multiplier);
        if (istype(source, typeof(obj_item_fishing_rod)))
            RegisterSignal(source, COMSIG_ROD_BEGIN_FISHING, nameof(on_begin_fishing));
        else if (istype(source, typeof(obj_item_fish)))
            ADD_TRAIT(source, TRAIT_FISH_MADE_OF_BONE, REF(this));
    }
    // tg: code/datums/materials/basemats.dm:821
    [SignalHandler]
    public virtual void on_begin_fishing(obj_item_fishing_rod rod = null, datum_fishing_challenge challenge = null)
    {
        if (prob(40))
            RegisterSignal(challenge, COMSIG_FISHING_CHALLENGE_ROLL_REWARD, nameof(roll_bones));
    }
    // tg: code/datums/materials/basemats.dm:826
    [SignalHandler]
    public virtual void roll_bones(datum source = null, obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null, DmList rewards = null)
    {
        DmList bones = TgState.Current.datum_material_bone_roll_bones__bones ??= Dm.list(
            Dm.kv(typeof(obj_item_fish_boned), 65),
            Dm.kv(typeof(obj_item_fish_mastodon), 8),
            Dm.kv(typeof(mob_living_basic_skeleton), 6),
            Dm.kv(typeof(mob_living_basic_skeleton_ice), 6),
            Dm.kv(typeof(mob_living_basic_skeleton_templar), 6),
            Dm.kv(typeof(obj_item_instrument_trumpet_spectral_one_doot), 3),
            Dm.kv(typeof(obj_item_instrument_saxophone_spectral_one_doot), 3),
            Dm.kv(typeof(obj_item_instrument_trombone_spectral_one_doot), 3));
        Dm.append(rewards, pick_weight(bones));
    }
    // tg: code/datums/materials/basemats.dm:840
    public override void on_main_removed(atom source = null, object mat_amount = null, object multiplier = null)
    {
        base.on_main_removed(source, mat_amount, multiplier);
        if (istype(source, typeof(obj_item_fishing_rod)))
            UnregisterSignal(source, COMSIG_ROD_BEGIN_FISHING);
        else if (istype(source, typeof(obj_item_fish)))
            REMOVE_TRAIT(source, TRAIT_FISH_MADE_OF_BONE, REF(this));
    }
}

// tg: code/datums/materials/basemats.dm:847
public partial class datum_material_bamboo : datum_material
{
}

// tg: code/datums/materials/basemats.dm:869
public partial class datum_material_zaukerite : datum_material
{
    // tg: code/datums/materials/basemats.dm:887
    public override async DmTask on_applied(atom source = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        await base.on_applied(source, mat_amount, multiplier, from_slot);
        if (istype(source, typeof(obj_item_fishing_rod)))
            ADD_TRAIT(source, TRAIT_ROD_IGNORE_ENVIRONMENT, REF(this)); // light-absorbing, environment-cancelling fishing rod.
    }
    // tg: code/datums/materials/basemats.dm:892
    public override void on_removed(atom source = null, object mat_amount = null, object material_flags = null, object from_slot = null)
    {
        base.on_removed(source, mat_amount, material_flags, from_slot);
        if (istype(source, typeof(obj_item_fishing_rod)))
            REMOVE_TRAIT(source, TRAIT_ROD_IGNORE_ENVIRONMENT, REF(this)); // light-absorbing, environment-cancelling fishing rod.
    }
    // tg: code/datums/materials/basemats.dm:897
    public override async DmTask<object> on_accidental_mat_consumption(mob_living_carbon victim = null, obj_item source_item = null)
    {
        object dot = await base.on_accidental_mat_consumption(victim, source_item);
        if (!HAS_TRAIT(victim, TRAIT_ROCK_EATER))
        {
            await victim.apply_damage(30, BURN, BODY_ZONE_HEAD, wound_bonus: 5);
        }
        return TRUE;
    }
}

/// Evil and very unstable version of bluespace crystals
// tg: code/datums/materials/basemats.dm:904
public partial class datum_material_telecrystal : datum_material
{
    // tg: code/datums/materials/basemats.dm:928
    public override async DmTask on_main_applied(atom source = null, object mat_amount = null, object multiplier = null)
    {
        await base.on_main_applied(source, mat_amount, multiplier);
        if (istype(source, typeof(obj_item_fishing_rod)))
            RegisterSignal(source, COMSIG_ROD_BEGIN_FISHING, nameof(on_begin_fishing));
    }
    // tg: code/datums/materials/basemats.dm:933
    public override void on_main_removed(atom source = null, object mat_amount = null, object multiplier = null)
    {
        base.on_main_removed(source, mat_amount, multiplier);
        if (istype(source, typeof(obj_item_fishing_rod)))
            UnregisterSignal(source, COMSIG_ROD_BEGIN_FISHING);
    }
    // tg: code/datums/materials/basemats.dm:938
    [SignalHandler]
    public virtual void on_begin_fishing(obj_item_fishing_rod rod = null, datum_fishing_challenge challenge = null)
    {
        // Oops, all chainsawfish!
        challenge.register_reward_signals(GLOB.preset_fish_sources[typeof(datum_fish_source_portal_syndicate)] as datum_fish_source);
    }
}

// tg: code/datums/materials/basemats.dm:943
public partial class datum_material_rock : datum_material
{
}

public sealed partial class TgState
{
    // tg: code/datums/materials/basemats.dm:335  roll_funny_fish's var/static/list/funny_fish
    internal DmList datum_material_bananium_roll_funny_fish__funny_fish;
    // tg: code/datums/materials/basemats.dm:828  roll_bones's var/static/list/bones
    internal DmList datum_material_bone_roll_bones__bones;
}
