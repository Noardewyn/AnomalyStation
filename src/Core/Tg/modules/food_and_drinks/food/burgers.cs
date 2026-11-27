// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/burgers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the burgers' own procs (the steamed ham, the burger named after its meat's donor, the ghost burger's haunting
// and the ghosts that eat it, the superbite's suicide, the cheeseburger's other look, the crazy burger's green smoke). The var
// blocks are generated.

// tg: code/modules/food_and_drinks/food/burgers.dm:17
public partial class obj_item_food_burger_plain : obj_item_food_burger
{
    // tg: code/modules/food_and_drinks/food/burgers.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!prob(1))
            return dot;
        Dm.@new<obj_effect_particle_effect_fluid_smoke>(get_turf(this));
        playsound(this, Dm.resource("sound/effects/smoke.ogg"), 50, TRUE);
        visible_message(span_warning(I18n.T("Oh, ye gods! [] is ruined! But what if...?", this)));
        name = "steamed ham";
        desc = Dm.str(pick("Ahh, Head of Personnel, welcome. I hope you're prepared for an unforgettable luncheon!",
            "And you call these steamed hams despite the fact that they are obviously microwaved?",
            "Aurora Station 13? At this time of shift, in this time of year, in this sector of space, localized entirely within your freezer?",
            "You know, these hamburgers taste quite similar to the ones they have at the Maltese Falcon."));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/burgers.dm:44
public partial class obj_item_food_burger_human : obj_item_food_burger
{
    // tg: code/modules/food_and_drinks/food/burgers.dm:58
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        foreach (datum_material_meat_mob_meat mob_meat_material in Dm.each<datum_material_meat_mob_meat>(custom_materials))
        {
            if (Dm.truthy(mob_meat_material.subjectname))
                name = Dm.text("[] burger", mob_meat_material.subjectname);
            else if (Dm.truthy(mob_meat_material.subjectjob))
                name = Dm.text("[] burger", mob_meat_material.subjectjob);
        }
    }
}

// tg: code/modules/food_and_drinks/food/burgers.dm:217
public partial class obj_item_food_burger_ghost : obj_item_food_burger
{
    // tg: code/modules/food_and_drinks/food/burgers.dm:236
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ghost_edible), Dm.named("bite_consumption", bite_consumption)));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/burgers.dm:241
    public override async DmTask make_germ_sensitive(object mapload = null)
    {
        await DmTask.CompletedTask;
        return; // This burger moves itself so it shouldn't pick up germs from walking onto the floor
    }

    // tg: code/modules/food_and_drinks/food/burgers.dm:244
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!isturf(loc)) //no floating out of bags
            return null;
        float paranormal_activity = rand(100);
        if (paranormal_activity >= 97 && paranormal_activity <= 100)
        {
            audible_message(I18n.T("[] rattles a length of chain.", this));
            playsound(loc, Dm.resource("sound/misc/chain_rattling.ogg"), 300, TRUE);
        }
        else if (paranormal_activity >= 91 && paranormal_activity <= 96)
            Waiting.NoSleep(say(pick("OoOoOoo.", "OoooOOooOoo!!")));
        else if (paranormal_activity >= 84 && paranormal_activity <= 90)
        {
            dir = Dm.num(pick(NORTH, SOUTH, EAST, WEST, NORTHEAST, NORTHWEST, SOUTHEAST, SOUTHWEST));
            Waiting.NoSleep(step(this, dir));
        }
        else if (paranormal_activity >= 71 && paranormal_activity <= 83)
            Waiting.NoSleep(step(this, dir));
        else if (paranormal_activity >= 65 && paranormal_activity <= 70)
        {
            obj_machinery_light light = Dm.locate<obj_machinery_light>(view(4, this));
            light?.flicker();
        }
        else if (paranormal_activity >= 62 && paranormal_activity <= 64)
            playsound(loc, SFX_HALLUCINATION_I_SEE_YOU, 50, TRUE, ignore_walls: FALSE);
        else if (paranormal_activity == 61)
        {
            visible_message(I18n.T("[] spews out a glob of ectoplasm!", this));
            Dm.@new<obj_effect_decal_cleanable_greenglow_ecto>(loc);
            playsound(loc, Dm.resource("sound/effects/splat.ogg"), 200, TRUE);
        }
        return null;
    }

    // tg: code/modules/food_and_drinks/food/burgers.dm:269
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }
}

// tg: code/modules/food_and_drinks/food/burgers.dm:456
public partial class obj_item_food_burger_superbite : obj_item_food_burger
{
    // tg: code/modules/food_and_drinks/food/burgers.dm:474
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] starts to eat [] in one bite, it looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        datum_component_edible component = GetComponent(typeof(datum_component_edible)) as datum_component_edible;
        if (component != null)
            await component.TakeBite(user, user);
        return OXYLOSS;
    }
}

// tg: code/modules/food_and_drinks/food/burgers.dm:653
public partial class obj_item_food_burger_cheese : obj_item_food_burger
{
    // tg: code/modules/food_and_drinks/food/burgers.dm:668
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(33))
            icon_state = "cheeseburgeralt";
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/burgers.dm:673
public partial class obj_item_food_burger_crazy : obj_item_food_burger
{
    // tg: code/modules/food_and_drinks/food/burgers.dm:694
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/burgers.dm:698
    public override object process(object seconds_per_tick = null, params object[] __extra) // DIT EES HORRIBLE
    {
        if (SPT_PROB(2.5f, seconds_per_tick))
            Waiting.NoSleep(do_smoke(0, this, loc, smoke_type: typeof(datum_effect_system_fluid_spread_smoke_bad_green)));
        return null;
    }
}
