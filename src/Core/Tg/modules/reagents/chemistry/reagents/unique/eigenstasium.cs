// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/unique/eigenstasium.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RG3 (D339): the whole file (eigenstasium: the cyan hologram left at the point a drinker returns to, the pure batch's teleport to
// where it was made, the sparks, the trip back home when it is gone or the fading spirit with the stabilizing agent, the overdose's
// random teleports and the eigenstasium wild ride, datums/status_effects/neutral.cs). The var block is generated (D111).

/*eigenstate themed Chem
 *Teleports you to the creation location on consumption and back when the reagent is removed from you
 *OD teleports you randomly around the Station and gives you a status effect
 *The status effect slowly send you on a wild ride and replaces you with an alternative reality version of yourself unless you consume eigenstasium/bluespace dust/stabilising agent.
 *During the process you get really hungry,
 *Then some of your items slowly start teleport around you,
 *then alternative versions of yourself are brought in from a different universe and they yell at you.
 *and finally you yourself get teleported to an alternative universe, and character your playing is replaced with said alternative
 *Spraying this on lockers allows you to create eigenlinked lockers - see the eigenstate subsystem for using this to create your own links
*/
// tg: code/modules/reagents/chemistry/reagents/unique/eigenstasium.dm:12
public partial class datum_reagent_eigenstate : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/unique/eigenstasium.dm:32
    public override async DmTask on_new(object data = null)
    {
        await base.on_new(data);
        if (!Dm.truthy(data))
            return;
        location_created = ((DmList)data)["location_created"] as turf;
    }

    // tg: code/modules/reagents/chemistry/reagents/unique/eigenstasium.dm:38
    public override async DmTask expose_mob(mob_living living_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        await base.expose_mob(living_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!Dm.truthy(Dm.bitand(methods, INGEST)) || !iscarbon(living_mob))
            return;
        //This looks rediculous, but expose is usually called from the donor reagents datum - we want to edit the post exposure version present in the mob.
        var carby = (mob_living_carbon)living_mob;
        //But because carbon mobs have stomachs we have to search in there because we're ingested
        var stomach = carby.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach;
        datum_reagent_eigenstate eigen = null;
        if (stomach != null)
            eigen = (stomach.reagents as datum_reagents)?.has_reagent(typeof(datum_reagent_eigenstate)) as datum_reagent_eigenstate;
        if (eigen == null) //But what if they have no stomach! I want to get off expose_mob's wild ride
            eigen = (carby.reagents as datum_reagents)?.has_reagent(typeof(datum_reagent_eigenstate)) as datum_reagent_eigenstate;
        //Because expose_mob and on_mob_add() across all of the different things call them in different orders, so I want to make sure whatever is the first one to call it sets up the location correctly.
        ((DmList)eigen.data)["ingested"] = TRUE;
    }

    //Main functions
    // tg: code/modules/reagents/chemistry/reagents/unique/eigenstasium.dm:55
    public virtual obj_effect_overlay_holo_pad_hologram make_appearance(mob_living living_mob = null, object spawn_loc = null)
    {
        obj_effect_overlay_holo_pad_hologram spirit = Dm.@new<obj_effect_overlay_holo_pad_hologram>(spawn_loc);
        spirit.appearance = living_mob.appearance;
        spirit.alpha = 170;
        Waiting.NoSleep(spirit.add_atom_colour(LIGHT_COLOR_LIGHT_CYAN, FIXED_COLOUR_PRIORITY));
        spirit.mouse_opacity = MOUSE_OPACITY_TRANSPARENT; //So you can't click on it
        spirit.layer = FLY_LAYER; //Above all the other objects/mobs. Or the vast majority of them.
        Waiting.NoSleep(spirit.set_anchored(TRUE)); //So space wind cannot drag it.
        spirit.name = Dm.text("[]'s Eigenstate", living_mob.name); //If someone decides to right click.
        Waiting.NoSleep(spirit.set_light(2)); //hologram lighting
        return spirit;
    }

    // tg: code/modules/reagents/chemistry/reagents/unique/eigenstasium.dm:67
    public override async DmTask on_mob_add(mob_living living_mob = null, object amount = null)
    {
        //make hologram at return point to indicate where someone will go back to
        eigenstate = make_appearance(living_mob, living_mob.loc);

        location_return = get_turf(living_mob) as turf_open; //sets up return point
        to_chat(living_mob, span_userdanger(I18n.T("You feel like part of yourself has split off!")));

        //Teleports you home if it's pure enough
        if (creation_purity > 0.9f && location_created != null && Dm.truthy(((DmList)data)?["ingested"]))
            await do_teleport(living_mob, location_created, 3, asoundin: Dm.resource("sound/effects/phasein.ogg"));

        await base.on_mob_add(living_mob, amount);
    }

    // tg's signature here is (living_mob, metabolization_ratio): its second argument is really seconds_per_tick; neither is read.
    // tg: code/modules/reagents/chemistry/reagents/unique/eigenstasium.dm:80
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        if (prob(20))
            do_sparks(5, FALSE, (atom)affected_mob);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/unique/eigenstasium.dm:85
    public override async DmTask on_mob_delete(mob_living living_mob = null) //returns back to original location
    {
        await base.on_mob_delete(living_mob);
        to_chat(living_mob, span_userdanger(I18n.T("You feel strangely whole again.")));
        if (Dm.truthy((living_mob.reagents as datum_reagents)?.has_reagent(typeof(datum_reagent_stabilizing_agent))))
        {
            obj_effect_overlay_holo_pad_hologram remaining_spirit = make_appearance(living_mob, eigenstate.loc);
            float spirit_duration = Dm.max(5 * MINUTES - (current_cycle * 5 * SECONDS), 10 * SECONDS);
            remaining_spirit.fade_into_nothing(spirit_duration, spirit_duration);
        }
        else
            await do_teleport(living_mob, location_return, 3, asoundin: Dm.resource("sound/effects/phasein.ogg")); //Teleports home

        qdel(eigenstate);
    }

    // tg calls the parent twice here (`. = ..()` and `return ..()`), so the overdose message and mood come twice; kept.
    // tg: code/modules/reagents/chemistry/reagents/unique/eigenstasium.dm:97
    public override async DmTask overdose_start(mob_living living_mob = null, object metabolization_ratio = null) //Overdose, makes you teleport randomly
    {
        await base.overdose_start(living_mob, metabolization_ratio);
        to_chat(living_mob, span_userdanger(I18n.T("You feel like your perspective is being ripped apart as you begin flitting in and out of reality!")));
        await living_mob.set_jitter_if_lower(40 * SECONDS);
        metabolization_rate += 0.5f; //So you're not stuck forever teleporting.
        if (iscarbon(living_mob))
        {
            var carbon_mob = (mob_living_carbon)living_mob;
            await carbon_mob.apply_status_effect(typeof(datum_status_effect_eigenstasium));
        }
        await base.overdose_start(living_mob, metabolization_ratio);
    }

    // tg: code/modules/reagents/chemistry/reagents/unique/eigenstasium.dm:107
    public override async DmTask<object> overdose_process(mob_living living_mob = null, object seconds_per_tick = null, object metabolization_ratio = null) //Overdose, makes you teleport randomly
    {
        object dot = await base.overdose_process(living_mob, seconds_per_tick, metabolization_ratio);
        await do_teleport(living_mob, get_turf(living_mob), 10, asoundin: Dm.resource("sound/effects/phasein.ogg"));
        return dot;
    }
}

//FOR ADDICTION-LIKE EFFECTS, SEE datum/status_effect/eigenstasium
