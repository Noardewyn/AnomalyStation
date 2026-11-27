// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/drug_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole. The street drugs are contraband: the saturnX glob, the moon rock (one of its three pictures),
// the crystal meth (one of five, ten units of methamphetamine, and an embedding that injects the meth into an organic limb it
// sticks in) and the opium (one of five pictures; raw opium from a poppy has morphine by its potency and presses together with
// other raw opium up to ten units). The bLaSToFF ampoule snaps open in hand (its "_open" and "_empty" pictures) and shatters into a
// shard when thrown, splashing its contents. The var blocks are generated (D111).

// tg: code/game/objects/items/drug_items.dm:16
public partial class obj_item_food_drug_saturnx : obj_item_food_drug
{
    // tg: code/game/objects/items/drug_items.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/game/objects/items/drug_items.dm:26
public partial class obj_item_food_drug_moon_rock : obj_item_food_drug
{
    // tg: code/game/objects/items/drug_items.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = (string)pick("moon_rock1", "moon_rock2", "moon_rock3");
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_MOONICORN, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5));
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/game/objects/items/drug_items.dm:38
public partial class obj_item_reagent_containers_cup_blastoff_ampoule : obj_item_reagent_containers_cup
{
    // tg: code/game/objects/items/drug_items.dm:50
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (!Dm.truthy(((datum_reagents)reagents).total_volume))
            icon_state = Dm.text("[]_empty", base_icon_state);
        else if (Dm.truthy(is_open_container()))
            icon_state = Dm.text("[]_open", base_icon_state);
        else
            icon_state = (string)base_icon_state;
        return dot;
    }

    // tg: code/game/objects/items/drug_items.dm:59
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY)) || Dm.truthy(is_open_container()))
            return await base.attack_self(user, modifiers, __extra);
        add_container_flags(OPENCONTAINER);
        playsound(this, Dm.resource("sound/items/ampoule_snap.ogg"), 40);
        update_appearance();
        return null;
    }

    // tg: code/game/objects/items/drug_items.dm:66
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object do_splash = Dm.arg(__extra, 0);
        do_splash ??= TRUE;
        object dot = base.throw_impact(hit_atom, throwingdatum, Dm.forward(__extra, do_splash));
        if (Dm.truthy(dot))
            return dot;
        if (QDELING(this) || !Dm.truthy(hit_atom)) //Invalid loc
            return dot;
        obj_item_shard ampoule_shard = Dm.@new<obj_item_shard>(drop_location());
        playsound(this, SFX_SHATTER, 40, TRUE);
        transfer_fingerprints_to(ampoule_shard);
        Waiting.NoSleep(splash_reagents(hit_atom, throwingdatum?.get_thrower() as mob, was_thrown: TRUE, allow_closed_splash: FALSE));
        qdel(this);
        return dot;
    }

    // tg: code/game/objects/items/drug_items.dm:78
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object vol = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, Dm.forward(__extra, vol));
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/game/objects/items/drug_items.dm:91
public partial class datum_embedding_meth : datum_embedding
{
    // tg: code/game/objects/items/drug_items.dm:98
    public override async DmTask<object> process_effect(object seconds_per_tick = null)
    {
        obj_item rock = parent;
        if (!(rock is obj_item_food_drug_meth_crystal))
            return null;

        if (!Dm.truthy(IS_ORGANIC_LIMB(owner_limb)))
            return null;

        if (!Dm.truthy(owner?.reagents) || !Dm.truthy(((datum_reagents)rock.reagents)?.total_volume))
            return null;

        await ((datum_reagents)rock.reagents).trans_to(
            owner,
            transfer_per_second * Dm.num(seconds_per_tick),
            methods: INJECT,
            show_message: Dm.num(SPT_PROB(15, seconds_per_tick))
        );
        return null;
    }
}

// tg: code/game/objects/items/drug_items.dm:82
public partial class obj_item_food_drug_meth_crystal : obj_item_food_drug
{
    // tg: code/game/objects/items/drug_items.dm:116
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = (string)pick("meth_crystal1", "meth_crystal2", "meth_crystal3", "meth_crystal4", "meth_crystal5");
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_drug_methamphetamine), 10));
        return dot;
    }
}

// tg: code/game/objects/items/drug_items.dm:122
public partial class obj_item_food_drug_opium : obj_item_food_drug
{
    // tg: code/game/objects/items/drug_items.dm:133
    [DmParams()]
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.num(((datum_reagents)reagents).get_reagent_amount(typeof(datum_reagent_medicine_morphine))) >= 10)
            Dm.append(ref dot, span_notice(I18n.T("The opium is large and rich in fragrance; it needs no further refinement.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("The opium is still small, and can be pressed together with more to increase its potency and richness.")));
        return dot;
    }

    // tg: code/game/objects/items/drug_items.dm:140
    public override object Initialize(object mapload = null, params object[] __extra) // For narcotics and black market purchases, pure and proper.
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = (string)pick("opium1", "opium2", "opium3", "opium4", "opium5");
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/game/objects/items/drug_items.dm:145
public partial class obj_item_food_drug_opium_raw : obj_item_food_drug_opium
{
    // tg: code/game/objects/items/drug_items.dm:145
    public override object Initialize(object mapload = null, params object[] __extra) // Randomizes amount depending upon potency.
    {
        object potency = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, Dm.forward(__extra, potency));
        Waiting.NoSleep(((datum_reagents)reagents).clear_reagents());
        float mult = max(Dm.num(potency) / 20, 1); // 20 = base, 100 = 5x, 200 = 10x
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_medicine_morphine), (rand(4, 25) / 10) * mult));
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_consumable_sugar), rand(1, 7) / 10));
        return dot;
    }

    // tg: code/game/objects/items/drug_items.dm:152
    public override async DmTask<object> interact_with_atom(atom I = null, mob_living user = null, DmList modifiers = null) // allows for combining opium up to 10u, refining it until rich and fragrant.
    {
        if (I is obj_item_food_drug_opium_raw)
        {
            obj_item_food_drug_opium_raw other = I as obj_item_food_drug_opium_raw;

            float current = Dm.num(((datum_reagents)reagents).get_reagent_amount(typeof(datum_reagent_medicine_morphine)));
            if (current >= 10)
            {
                to_chat(user, span_notice(I18n.T("This chunk can't hold any more.")));
                return TRUE;
            }

            float capacity_left = 10 - current;
            object transferred = await ((datum_reagents)other.reagents).trans_to(this, capacity_left);

            if (Dm.num(transferred) > 0)
            {
                float overflow = Dm.num(((datum_reagents)reagents).get_reagent_amount(typeof(datum_reagent_medicine_morphine))) - 10;
                if (overflow > 0)
                    await ((datum_reagents)reagents).trans_to(other, overflow);

                to_chat(user, span_notice(I18n.T("You press the chunks of opium together, enriching them.")));
                if (!Dm.truthy(((datum_reagents)other.reagents).total_volume))
                    qdel(other);
            }
            else
                to_chat(user, span_notice(I18n.T("The opium cannot be pressed together further.")));

            return TRUE;
        }

        return await base.interact_with_atom(I, user, modifiers);
    }
}
