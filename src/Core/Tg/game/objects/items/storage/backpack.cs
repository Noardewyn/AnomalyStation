// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/backpack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed with its module (a later milestone). Holds the [DmPath] of a type that M2 F1's translated defaults
// name (the job outfit's slots, the cloth that repairs clothing, the prisoner suit get_random_jumpsuit leaves out), in the
// earliest .dme file that declares it (rules 2.3). Only the type is declared.

// tg: code/game/objects/items/storage/backpack.dm:12
[DmPath("/obj/item/storage/backpack")]
public partial class obj_item_storage_backpack : obj_item_storage
{
    // M4a P3: the backpack's Initialize; its elements are deferred leaves (equipping by clicking a mob with it and
    // the strip menu's storage: M9).
    // tg: code/game/objects/items/storage/backpack.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/items/storage/backpack.dm:33", "Initialize: AddElement(/datum/element/attack_equip) and AddElement(/datum/element/strip_accessible_storage) (equipping by click and the strip menu, M10+)");
        return dot;
    }

}

// tg: code/game/objects/items/storage/backpack.dm:300
[DmPath("/obj/item/storage/backpack/satchel")]
public partial class obj_item_storage_backpack_satchel : obj_item_storage_backpack
{
}

// tg: code/game/objects/items/storage/backpack.dm:306
[DmPath("/obj/item/storage/backpack/satchel/leather")]
public partial class obj_item_storage_backpack_satchel_leather : obj_item_storage_backpack_satchel
{
}

// tg: code/game/objects/items/storage/backpack.dm:419
[DmPath("/obj/item/storage/backpack/messenger")]
public partial class obj_item_storage_backpack_messenger : obj_item_storage_backpack
{
}

// M4a contracts: the roots of this file's procs that no part declared yet, with the signatures of
// docs/ai/specs/generated/slots.tsv (DmTask exactly where suspends = yes), Dm.NotTranslated until M4a P3 translates
// them or leaves them for their milestone (docs/ai/specs/m4a-ownership.md). Overrides are not declared, so the inherited
// code keeps running until then.

// Gaps R3 (D327): Santa's gift bag (a new present every 30 seconds while Santa carries it).
// tg: code/game/objects/items/storage/backpack.dm:90
public partial class obj_item_storage_backpack_santabag : obj_item_storage_backpack
{
    // tg: code/game/objects/items/storage/backpack.dm:98
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        regenerate_presents();
        return dot;
    }

    // tg: code/game/objects/items/storage/backpack.dm:102
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] places [] over [] head and pulls it tight! It looks like [] []n't in the Christmas spirit...", user, this, user.p_their(), user.p_they(), user.p_are())));
        await DmTask.CompletedTask;
        return OXYLOSS;
    }

    // tg: code/game/objects/items/storage/backpack.dm:106
    public virtual void regenerate_presents()
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(regenerate_presents)), 30 * SECONDS, file: "code/game/objects/items/storage/backpack.dm", line: 107);

        var user = get(loc, typeof(mob)) as mob;
        if (user == null)
            return;
        if (HAS_MIND_TRAIT(user, TRAIT_CANNOT_OPEN_PRESENTS))
        {
            turf floor = get_turf(this);
            var thing = Dm.@new<obj_item_gift_anything>(floor);
            if (!Dm.truthy(atom_storage.attempt_insert(thing, user, @override: TRUE, force: STORAGE_SOFT_LOCKED)))
                qdel(thing);
        }
    }
}

// M4a P3: the PopulateContents of Meta's containers in this file (D133), translated with the help of m4/p3w/popgen2.py
// (the pure `new /path(src)` bodies, their loops, prob and switch); the file's other procs are their milestones'.

// tg: code/game/objects/items/storage/backpack.dm:312
public partial class obj_item_storage_backpack_satchel_leather_withwallet : obj_item_storage_backpack_satchel_leather
{
    // tg: code/game/objects/items/storage/backpack.dm:312
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_storage_wallet_random>(this); // tg: code/game/objects/items/storage/backpack.dm:313
    }
}

// The syndicate campaign G1c (D316): the smuggler's satchel (hides under a floor tile, seen only by T-rays; contraband scanners
// cannot see into it).
// tg: code/game/objects/items/storage/backpack.dm:391
public partial class obj_item_storage_backpack_satchel_flat : obj_item_storage_backpack_satchel
{
    // tg: code/game/objects/items/storage/backpack.dm:399
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_undertile), TRAIT_T_RAY_VISIBLE, INVISIBILITY_OBSERVER, Dm.named("use_anchor", TRUE)));
        ADD_TRAIT(this, TRAIT_CONTRABAND_BLOCKER, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/items/storage/backpack.dm:404
    public override async DmTask PopulateContents()
    {
        for (int items = 1; items <= 4; items++)
            Dm.@new<obj_effect_spawner_random_contraband>(this);
    }
}

// tg: code/game/objects/items/storage/backpack.dm:408
public partial class obj_item_storage_backpack_satchel_flat_with_tools : obj_item_storage_backpack_satchel_flat
{
    // tg: code/game/objects/items/storage/backpack.dm:408
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_stack_tile_iron_base>(this);
        Dm.@new<obj_item_crowbar>(this);

        await base.PopulateContents();
    }
}

// tg: code/game/objects/items/storage/backpack.dm:414
public partial class obj_item_storage_backpack_satchel_flat_empty : obj_item_storage_backpack_satchel_flat
{
    // tg: code/game/objects/items/storage/backpack.dm:414
    public override async DmTask PopulateContents()
    {
        return;
    }
}

// Gaps SR1 (D333): the bag of holding (its bluespace, the inert frame slapcrafted into one with an anomaly core, the rift of a
// recursive insertion in datums/storage/subtypes/others/bag_of_holding.cs) and the MEAT backpack.

// tg: code/game/objects/items/storage/backpack.dm:40
public partial class obj_item_bag_of_holding_inert : obj_item
{
    // tg: code/game/objects/items/storage/backpack.dm:54
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // tg: code/game/objects/items/storage/backpack.dm:56  var/static/list/recipes (a proc-level static, rules 10.6)
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), TgState.Current.obj_item_bag_of_holding_inert__Initialize__recipes));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/storage/backpack.dm:56  var/static/list/recipes = list(/datum/crafting_recipe/boh)
    internal DmList obj_item_bag_of_holding_inert__Initialize__recipes = Dm.list(typeof(datum_crafting_recipe_boh));
}

// tg: code/game/objects/items/storage/backpack.dm:59
public partial class obj_item_storage_backpack_holding : obj_item_storage_backpack
{
    // tg: code/game/objects/items/storage/backpack.dm:72
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_BLUESPACE_INTERFERENCE, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/items/storage/backpack.dm:80
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is jumping into []! It looks like [] trying to commit suicide.", user, this, user.p_theyre())));
        await user.dropItemToGround(this, TRUE);
        await user.Stun(100, ignore_canstun: TRUE);
        await sleep(2 * SECONDS);
        playsound(this, SFX_RUSTLE, 50, TRUE, -5);
        user.suicide_log();
        qdel(user);
        return null;
    }
}

// tg: code/game/objects/items/storage/backpack.dm:261
public partial class obj_item_storage_backpack_meat : obj_item_storage_backpack
{
    // tg: code/game/objects/items/storage/backpack.dm:279
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponentFrom(
            SOURCE_EDIBLE_INNATE,
            typeof(datum_component_edible),
            Dm.named("initial_reagents", meat_reagents),
            Dm.named("tastes", Dm.list(Dm.kv("meat", 1))),
            Dm.named("eatverbs", eatverbs)));

        Waiting.NoSleep(AddComponent(typeof(datum_component_squeak), meat_sounds));
        return dot;
    }

    // tg: code/game/objects/items/storage/backpack.dm:291
    public override void change_material_strength(datum_material material = null, object mat_amount = null, object multiplier = null, float? remove = null)
    {
        // Our base 15 force includes the implied meat force
        if (!istype(material, typeof(datum_material_meat)))
            base.change_material_strength(material, mat_amount, multiplier, remove);
    }
}
