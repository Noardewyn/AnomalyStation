// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/banana.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the banana and its peel; the other bananas of the file have only vars or wait for hydroponics.

// tg: code/modules/hydroponics/grown/banana.dm:20
public partial class obj_item_food_grown_banana : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/banana.dm:30
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_banana);
    }

    // tg: code/modules/hydroponics/grown/banana.dm:33
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("check_liked", Dm.@new<datum_callback>(this, nameof(check_liked))));
    }

    // tg: code/modules/hydroponics/grown/banana.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(1))
            Waiting.NoSleep(AddComponent(typeof(datum_component_boomerang), Dm.named("boomerang_throw_range", throw_range + 4), Dm.named("thrower_easy_catch_enabled", TRUE), Dm.named("examine_message", span_green(I18n.T("The curve on this one looks particularly acute.")))));
        return dot;
    }

    ///Clowns will always like bananas.
    // tg: code/modules/hydroponics/grown/banana.dm:43
    public virtual object check_liked(mob_living_carbon_human consumer = null)
    {
        obj_item_organ_liver liver = consumer.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (!HAS_TRAIT(consumer, TRAIT_AGEUSIA) && liver != null && HAS_TRAIT(liver, TRAIT_COMEDY_METABOLISM))
            return FOOD_LIKED;
        return null;
    }

    // tg: code/modules/hydroponics/grown/banana.dm:48
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is aiming [] at []self! It looks like [] trying to commit suicide!", user, this, user.p_them(), user.p_theyre())));
        playsound(loc, Dm.resource("sound/items/bikehorn.ogg"), 50, TRUE, -1);
        await sleep(2.5f * SECONDS);
        if (user == null)
            return OXYLOSS;
        await user.say("BANG!", forced: typeof(datum_reagent_consumable_banana));
        await sleep(2.5f * SECONDS);
        if (user == null)
            return OXYLOSS;
        user.visible_message(I18n.T("<B>[]</B> laughs so hard they begin to suffocate!", user));
        return OXYLOSS;
    }
}

//Banana Peel
// tg: code/modules/hydroponics/grown/banana.dm:63
public partial class obj_item_grown_bananapeel : obj_item_grown
{
    // tg: code/modules/hydroponics/grown/banana.dm:75
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(40))
        {
            if (prob(60))
                icon_state = Dm.text("[]_2", icon_state);
            else
                icon_state = Dm.text("[]_3", icon_state);
        }
        return dot;
    }

    // tg: code/modules/hydroponics/grown/banana.dm:83
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_medicine_coagulant_banana_peel), ((obj_item_seeds)seed).potency * 0.2f));
    }

    // tg: code/modules/hydroponics/grown/banana.dm:86
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is deliberately slipping on []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        playsound(loc, Dm.resource("sound/misc/slip.ogg"), 50, TRUE, -1);
        return BRUTELOSS;
    }
}

// Gaps P2 (D298): the rest of the file.

// tg: code/modules/hydroponics/grown/banana.dm:154
public partial class obj_item_grown_bananapeel_specialpeel : obj_item_grown_bananapeel
{
    // tg: code/modules/hydroponics/grown/banana.dm:157
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_slippery), 40));
        return dot;
    }
}

// tg: code/modules/hydroponics/grown/banana.dm:162
public partial class obj_item_food_grown_banana_bunch : obj_item_food_grown_banana
{
    // tg: code/modules/hydroponics/grown/banana.dm:168
    [DmParams("mapload", "new_seed")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_reagents holder = reagents as datum_reagents;
        Waiting.NoSleep(holder.clear_reagents());
        Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_consumable_monkey_energy), 10));
        Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_consumable_banana), 10));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_CLOWNANA, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5));
        return dot;
    }

    // tg: code/modules/hydroponics/grown/banana.dm:175
    public virtual void start_ripening()
    {
        if (Dm.truthy(is_ripening))
            return;
        playsound(this, Dm.resource("sound/effects/fuse.ogg"), 80);

        animate(this, Dm.named("time", 1), Dm.named("pixel_z", 12), Dm.named("easing", ELASTIC_EASING));
        animate(Dm.named("time", 1), Dm.named("pixel_z", 0), Dm.named("easing", BOUNCE_EASING));
        _addtimer(Dm.@new<datum_callback>(this, nameof(explosive_ripening)), 3 * SECONDS, file: "code/modules/hydroponics/grown/banana.dm", line: 182);
        for (int i = 1; i <= 32; i++)
            animate(Dm.named("color", (i % 2) != 0 ? "#ffffff" : "#ff6739"), Dm.named("time", 1), Dm.named("easing", QUAD_EASING));
    }

    // tg: code/modules/hydroponics/grown/banana.dm:186
    public virtual async DmTask explosive_ripening()
    {
        await honkerblast(this, light_range: 3, medium_range: 1);
        foreach (mob shook_boi in Dm.each<mob>(Builtins.range(6, loc)))
            shake_camera(shook_boi, 3, 5);
        obj_effect_decal_cleanable_food_plant_smudge banana_smudge = Dm.@new<obj_effect_decal_cleanable_food_plant_smudge>(loc);
        banana_smudge.color = "#ffe02f";
        qdel(this);
    }
}

// tg: code/modules/hydroponics/grown/banana.dm:195
public partial class obj_item_food_grown_banana_bunch_monkeybomb : obj_item_food_grown_banana_bunch
{
    // tg: code/modules/hydroponics/grown/banana.dm:197
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!HAS_TRAIT(user, TRAIT_SIMIAN))
        {
            Dm.append(ref dot, span_notice(I18n.T("There's a banana label on one of the 'nanas you can't quite make out the details of.")));
            return dot;
        }
        Dm.append(ref dot, span_notice(I18n.T("The banana label on this bunch indicates that monkeys can use this as a sonic grenade with a 3 second timer!")));
        return dot;
    }

    // tg: code/modules/hydroponics/grown/banana.dm:204
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!HAS_TRAIT(user, TRAIT_SIMIAN))
        {
            to_chat(user, span_notice(I18n.T("You don't really know what to do with this.")));
            return null;
        }
        else
            start_ripening();
        return null;
    }
}

// tg: code/modules/hydroponics/grown/banana.dm:211
public partial class obj_item_grown_bananapeel_gros_michel : obj_item_grown_bananapeel
{
    // tg: code/modules/hydroponics/grown/banana.dm:214
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_germ_sensitive), mapload)); // inert until the germs (the kitchen)
        transform = Dm.mul(transform, 1.25f);
        // AddComponent(/datum/component/decomposition, mapload, decomp_req_handle = TRUE, custom_time = 1 MINUTES, decomp_result = ...): rotting
        // is not ported (the decomposition component is inert, S1's row)
        Dm.Deferred("code/modules/hydroponics/grown/banana.dm:218", "Initialize: the Gros Michel peel's decomposition (rotting, inert)");
        return dot;
    }
}
