// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/apple.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/apple.dm:3
public partial class obj_item_seeds_apple : obj_item_seeds
{
    // tg: code/modules/hydroponics/grown/apple.dm:19
    public override async DmTask<object> harvest(mob user = null)
    {
        DmList result = await base.harvest(user) as DmList;
        obj_machinery_hydroponics tray = loc as obj_machinery_hydroponics;
        float worm_chance = tray.pestlevel;
        if (Dm.truthy(Dm.bitand(tray.tray_flags, WORM_HABITAT)))
            worm_chance = 80; // Wormy soil isn't good for apples probably
        foreach (obj_item_food_grown_apple applum in Dm.each<obj_item_food_grown_apple>(result))
        {
            if (prob(worm_chance))
            {
                applum.appleworm = Dm.@new<obj_item_food_bait_worm>(applum); // There is a worm in this apple!
                applum.tastes = Dm.list(Dm.kv("apple", 1), Dm.kv("worms", 2));
                applum.ediblecomponent = applum.GetComponent(typeof(datum_component_edible)) as datum_component_edible; // IS_EDIBLE
                if (applum.ediblecomponent != null)
                    applum.ediblecomponent.foodtypes = Dm.bitor(Dm.num(applum.ediblecomponent.foodtypes), Dm.bitor(Dm.bitor(GROSS, MEAT), BUGS));
            }
        }
        return result;
    }
}

// tg: code/modules/hydroponics/grown/apple.dm:34
public partial class obj_item_food_grown_apple : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/apple.dm:48
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(found_worm) && !isobserver(user) && appleworm != null)
        {
            balloon_alert(user, I18n.T("there is a hole in this []!", name));
            found_worm = TRUE;
            desc = "It's a little piece of Eden. Serpent not included, contains a worm as a replacement.";
        }
        return dot;
    }

    // tg: code/modules/hydroponics/grown/apple.dm:55
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (appleworm == null)
            return null;
        balloon_alert(user, I18n.T("pulling out []...", appleworm.name));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: this)))
            return null;
        await appleworm.forceMove(drop_location() as atom);
        appleworm = null;
        tastes = Dm.list(Dm.kv("apple", 1));
        datum_component_edible edible = GetComponent(typeof(datum_component_edible)) as datum_component_edible; // IS_EDIBLE
        desc = Dm.text("It's a little piece of Eden. The [] is gone.", pick("serpent", "worm", "extra protein", "friendly neighbor"));
        if (edible == null)
            return null;
        edible.foodtypes = Dm.bitand(Dm.num(edible.foodtypes), Dm.bitnot(Dm.bitor(Dm.bitor(GROSS, MEAT), BUGS)));
        return null;
    }

    // tg: code/modules/hydroponics/grown/apple.dm:70
    public virtual void on_consume(mob_living eater = null)
    {
        if (!ishuman(eater) && appleworm == null)
            return;
        to_chat(eater, span_alert(I18n.T("That apple was wormy!")));
    }

    // tg: code/modules/hydroponics/grown/apple.dm:81
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (Dm.truthy(dot)) //it's been caught.
            return dot;
        if (!ishuman(hit_atom))
            return dot;
        mob_living_carbon_human einstein = hit_atom as mob_living_carbon_human;
        obj_item_organ_liver liver_organ = einstein.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver_organ == null)
            return dot;
        if (HAS_TRAIT(liver_organ, TRAIT_MEDICAL_METABOLISM))
            Waiting.NoSleep(einstein.apply_damage(2, BRUTE, throwingdatum.target_zone));
        else if (HAS_TRAIT(liver_organ, TRAIT_SCIENTIST_LIVER) && Dm.eq(throwingdatum.target_zone, BODY_ZONE_HEAD) && prob(2))
            Waiting.Detach(gravity_reminder(einstein));
        return dot;
    }

    // tg: code/modules/hydroponics/grown/apple.dm:96
    public override async DmTask<object> onZImpact(turf impacted_turf = null, object levels = null, float? impact_flags = null)
    {
        object dot = await base.onZImpact(impacted_turf, levels, impact_flags);
        mob_living_carbon_human einstein = Dm.locate<mob_living_carbon_human>(impacted_turf);
        if (einstein == null)
            return dot;
        obj_item_organ_liver liver_organ = einstein.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver_organ != null && HAS_TRAIT(liver_organ, TRAIT_SCIENTIST_LIVER) && prob(40))
            await gravity_reminder(einstein);
        return dot;
    }

    // tg: code/modules/hydroponics/grown/apple.dm:106
    public virtual async DmTask gravity_reminder(mob_living einstein = null)
    {
        einstein.do_alert_animation();
        playsound(einstein, Dm.resource("sound/machines/chime.ogg"), 50, TRUE);
        await einstein.say(pick_list_replacements(VISTA_FILE, "ballmer_good_msg"), forced: "apple inspiration");
    }

    // tg: code/modules/hydroponics/grown/apple.dm:75
    public override object juice_typepath()
    {
        return typeof(datum_reagent_consumable_applejuice);
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_grown_apple : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/apple.dm:78
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_appleslice), 5, 20, Dm.named("screentip_verb", "Slice"), Dm.named("sound_to_play", SFX_KNIFE_SLICE));
    }
}

public partial class obj_item_food_grown_apple_gold : obj_item_food_grown_apple
{
    // tg: code/modules/hydroponics/grown/apple.dm:126
    public override async DmTask make_processable()
    {
        return; // You're going to break your knife!
    }
}
