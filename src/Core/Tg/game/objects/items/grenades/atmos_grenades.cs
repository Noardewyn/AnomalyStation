// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/grenades/atmos_grenades.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps F1): the crystallizer's gas crystals, which arm when crushed: healium, proto-nitrate, nitrous oxide and
// the foam crystal (smart metal foam through chem_splash).

// tg: code/game/objects/items/grenades/atmos_grenades.dm:1
public partial class obj_item_grenade_gas_crystal : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/atmos_grenades.dm:10
    public override async DmTask arm_grenade(mob user = null, object delayoverride = null, float? msg = null, float? volume = null)
    {
        msg ??= TRUE;
        volume ??= 60;
        log_grenade(user); //Inbuilt admin procs already handle null users
        if (user != null)
        {
            add_fingerprint(user);
            if (Dm.truthy(msg))
                to_chat(user, span_warning(I18n.T("You crush the []! []!", this, capitalize(DisplayTimeText(det_time)))));
        }
        if (shrapnel_type != null && Dm.truthy(shrapnel_radius))
        {
            // shrapnel_initialized = TRUE; AddComponent(/datum/component/pellet_cloud, ...): no gas crystal has shrapnel (the
            // pellet clouds come with the guns and grenades after the core version)
            Dm.NotTranslated("code/game/objects/items/grenades/atmos_grenades.dm:17", "arm_grenade: the pellet cloud of a shrapnel grenade (the guns and grenades' pellet clouds, after the core version)");
        }
        active = TRUE;
        icon_state = Dm.text("[]_active", Dm.initial(GetType(), (obj_item_grenade_gas_crystal g) => g.icon_state));
        playsound(this, Dm.resource("sound/effects/hit_on_shattered_glass.ogg"), volume, TRUE);
        if (Dm.truthy(_listen_lookup?[COMSIG_GRENADE_ARMED]))
            _SendSignal(COMSIG_GRENADE_ARMED, Dm.list(this, det_time, delayoverride));
        if (user != null && Dm.truthy(_listen_lookup?[COMSIG_MOB_GRENADE_ARMED]))
            _SendSignal(COMSIG_MOB_GRENADE_ARMED, Dm.list(this, user, this, det_time, delayoverride)); // tg sends the mob's signal on the grenade itself (atmos_grenades.dm:23)
        _addtimer(Dm.@new<datum_callback>(this, nameof(detonate)), delayoverride == null ? det_time : Dm.num(delayoverride), file: "code/game/objects/items/grenades/atmos_grenades.dm", line: 25);
    }
}

// tg: code/game/objects/items/grenades/atmos_grenades.dm:27
public partial class obj_item_grenade_gas_crystal_healium_crystal : obj_item_grenade_gas_crystal
{
    // tg: code/game/objects/items/grenades/atmos_grenades.dm:34
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        await update_mob();
        playsound(this, Dm.resource("sound/effects/spray2.ogg"), 100, TRUE);
        DmList turf_list = RANGE_TURFS(fix_range, this);
        datum_gas_mixture base_mix = SSair.parse_gas_string(OPENTURF_DEFAULT_ATMOS) as datum_gas_mixture;
        foreach (turf_open turf_fix in Dm.each<turf_open>(turf_list))
        {
            if (Dm.truthy(turf_fix.blocks_air))
                continue;
            turf_fix.copy_air(base_mix.copy() as datum_gas_mixture);
            turf_fix.air_update_turf(update: FALSE, remove: FALSE);
        }
        qdel(this);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/atmos_grenades.dm:50
public partial class obj_item_grenade_gas_crystal_proto_nitrate_crystal : obj_item_grenade_gas_crystal
{
    // tg: code/game/objects/items/grenades/atmos_grenades.dm:61
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        await update_mob();
        playsound(this, Dm.resource("sound/effects/spray2.ogg"), 100, TRUE);
        foreach (turf turf_loc in Dm.each<turf>(view(refill_range, loc)))
        {
            if (!isopenturf(turf_loc))
                continue;
            float distance_from_center = Dm.max(get_dist(turf_loc, loc), 1);
            turf_open floor_loc = turf_loc as turf_open;
            floor_loc.atmos_spawn_air(Dm.text("[]=[];[]=[];[]", GAS_N2, n2_gas_amount / distance_from_center, GAS_O2, o2_gas_amount / distance_from_center, TURF_TEMPERATURE(273)));
        }
        qdel(this);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/atmos_grenades.dm:76
public partial class obj_item_grenade_gas_crystal_nitrous_oxide_crystal : obj_item_grenade_gas_crystal
{
    // tg: code/game/objects/items/grenades/atmos_grenades.dm:85
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        await update_mob();
        playsound(this, Dm.resource("sound/effects/spray2.ogg"), 100, TRUE);
        foreach (turf turf_loc in Dm.each<turf>(view(fill_range, loc)))
        {
            if (!isopenturf(turf_loc))
                continue;
            float distance_from_center = Dm.max(get_dist(turf_loc, loc), 1);
            turf_open floor_loc = turf_loc as turf_open;
            floor_loc.atmos_spawn_air(Dm.text("[]=[];[]", GAS_N2O, n2o_gas_amount / distance_from_center, TURF_TEMPERATURE(273)));
        }
        qdel(this);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/atmos_grenades.dm:101
public partial class obj_item_grenade_gas_crystal_crystal_foam : obj_item_grenade_gas_crystal
{
    // tg: code/game/objects/items/grenades/atmos_grenades.dm:106
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);

        datum_reagents first_batch = Dm.@new<datum_reagents>();
        datum_reagents second_batch = Dm.@new<datum_reagents>();
        DmList reactants = Dm.list();

        await first_batch.add_reagent(typeof(datum_reagent_aluminium), 75);
        await second_batch.add_reagent(typeof(datum_reagent_smart_foaming_agent), 25);
        await second_batch.add_reagent(typeof(datum_reagent_toxin_acid_fluacid), 25);
        Dm.append(ref reactants, first_batch);
        Dm.append(ref reactants, second_batch);

        turf detonation_turf = get_turf(this);

        await chem_splash(detonation_turf, null, breach_range, reactants);

        playsound(this, Dm.resource("sound/effects/spray2.ogg"), 100, TRUE);
        log_game(Dm.text("A grenade detonated at []", AREACOORD(detonation_turf)));

        await update_mob();

        qdel(this);
        return dot;
    }
}
