// Translated from tgstation (snapshot 1d99bd7f): code/datums/weather/weather_types/rain_storm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the whole file: the rain storms (water, blood, plasma, deep fried oil and acid rain) and the wizard's magical rain with
// its reagents, which the weather anomaly and the radar tower summon too, with their looping sounds per area. The var blocks are
// generated (D111).

// tg: code/datums/weather/weather_types/rain_storm.dm:1
public partial class datum_weather_particle_rain_storm : datum_weather_particle
{
    // tg: code/datums/weather/weather_types/rain_storm.dm:33
    [DmParams("z_levels", "weather_data")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (weather_reagent == null || weather_reagent is datum_reagent_water || !Dm.truthy(weather_color))
            return;

        foreach ((object _z_level, object holder_list) in Dm.pairs(weather_objects))
        {
            foreach (obj_effect_abstract_weather_holder holder in Dm.each_cast<obj_effect_abstract_weather_holder>(holder_list))
            {
                if (holder.particles is particles particle_set)
                    particle_set.color = weather_color;
            }
        }
    }

    // tg: code/datums/weather/weather_types/rain_storm.dm:42
    public override DmList get_playlist_ref() => GLOB.rain_storm_sounds;

    // tg: code/datums/weather/weather_types/rain_storm.dm:45
    public override object telegraph(DmList weather_data = null)
    {
        GLOB.rain_storm_sounds.Cut();
        foreach (area impacted_area in Dm.each_cast<area>(impacted_areas))
            GLOB.rain_storm_sounds[impacted_area] = typeof(datum_looping_sound_rain_start);

        foreach (object z in Dm.each(impacted_z_levels))
        {
            if (Dm.truthy(is_station_level(z)))
            {
                telegraph_message = span_warning(I18n.T("Thunder rumbles from above. You hear droplets hitting the floor around you."));
                break;
            }
        }
        return base.telegraph(weather_data);
    }

    // tg: code/datums/weather/weather_types/rain_storm.dm:56
    public override object start()
    {
        GLOB.rain_storm_sounds.Cut();
        foreach (area impacted_area in Dm.each_cast<area>(impacted_areas))
            GLOB.rain_storm_sounds[impacted_area] = typeof(datum_looping_sound_rain_middle);
        return base.start();
    }

    // tg: code/datums/weather/weather_types/rain_storm.dm:62
    public override object wind_down()
    {
        GLOB.rain_storm_sounds.Cut();
        foreach (area impacted_area in Dm.each_cast<area>(impacted_areas))
            GLOB.rain_storm_sounds[impacted_area] = typeof(datum_looping_sound_rain_end);
        return base.wind_down();
    }

    // tg: code/datums/weather/weather_types/rain_storm.dm:68
    public override object end()
    {
        GLOB.rain_storm_sounds.Cut();
        return base.end();
    }
}

// tg: code/datums/weather/weather_types/rain_storm.dm:132
public partial class datum_weather_particle_rain_storm_wizard : datum_weather_particle_rain_storm
{
    // tg: code/datums/weather/weather_types/rain_storm.dm:147
    [DmParams("z_levels", "weather_data")]
    public override async DmTask New(params object[] __extra)
    {
        if (Dm.truthy(Dm.length(GLOB.wizard_rain_reagents))) // the wizard event has already been run once and setup the whitelist
        {
            whitelist_weather_reagents = GLOB.wizard_rain_reagents;
            await base.New(__extra);
            return;
        }

        DmList allowed_medicine = Dm.list(
            typeof(datum_reagent_medicine_c2_synthflesh),
            typeof(datum_reagent_medicine_adminordrazine),
            typeof(datum_reagent_medicine_strange_reagent),
            pick(subtypesof(typeof(datum_reagent_medicine))));
        DmList rain = GLOB.wizard_rain_reagents;
        Dm.combine(rain, allowed_medicine);
        Dm.combine(rain, pick(subtypesof(typeof(datum_reagent_mutationtoxin))));
        Dm.combine(rain, pick(subtypesof(typeof(datum_reagent_plantnutriment))));
        Dm.combine(rain, pick(subtypesof(typeof(datum_reagent_impurity))));
        Dm.combine(rain, pick(subtypesof(typeof(datum_reagent_drug))));
        Dm.combine(rain, pick(typesof(typeof(datum_reagent_glitter_random))));
        Dm.combine(rain, pick(typesof(typeof(datum_reagent_uranium))));
        Dm.combine(rain, pick(typesof(typeof(datum_reagent_luminescent_fluid))));
        Dm.combine(rain, pick(typesof(typeof(datum_reagent_carpet))));
        Dm.combine(rain, pick(typesof(typeof(datum_reagent_water))));
        Dm.combine(rain, pick(typesof(typeof(datum_reagent_fuel))));
        Dm.combine(rain, pick(typesof(typeof(datum_reagent_colorful_reagent))));
        Dm.combine(rain, pick(typesof(typeof(datum_reagent_ants))));
        Dm.combine(rain, pick(typesof(typeof(datum_reagent_lube))));
        Dm.combine(rain, pick(typesof(typeof(datum_reagent_space_cleaner))));

        DmList allowed_toxins = Dm.list(
            typeof(datum_reagent_toxin_itching_powder),
            typeof(datum_reagent_toxin_polonium), // radiation
            typeof(datum_reagent_toxin_mutagen),
            typeof(datum_reagent_toxin_acid),
            typeof(datum_reagent_toxin_acid_fluacid),
            typeof(datum_reagent_toxin_acid_nitracid),
            pick(subtypesof(typeof(datum_reagent_toxin))));
        Dm.combine(rain, allowed_toxins);

        DmList allowed_food_drinks = Dm.list(
            typeof(datum_reagent_consumable_ethanol_wizz_fizz),
            typeof(datum_reagent_consumable_condensedcapsaicin),
            typeof(datum_reagent_consumable_frostoil),
            pick(subtypesof(typeof(datum_reagent_consumable))),
            pick(obj_machinery_chem_dispenser_drinks_beer.beer_dispensable_reagents));
        Dm.combine(rain, allowed_food_drinks);

        DmList allowed_exotic_reagents = Dm.list(
            typeof(datum_reagent_clf3),
            typeof(datum_reagent_phlogiston),
            typeof(datum_reagent_napalm),
            typeof(datum_reagent_hair_dye),
            typeof(datum_reagent_barbers_aid),
            typeof(datum_reagent_baldium),
            typeof(datum_reagent_mulligan),
            typeof(datum_reagent_growthserum),
            typeof(datum_reagent_romerol),
            typeof(datum_reagent_gondola_mutation_toxin),
            typeof(datum_reagent_metalgen),
            typeof(datum_reagent_flightpotion),
            typeof(datum_reagent_eigenstate),
            typeof(datum_reagent_magillitis),
            typeof(datum_reagent_pax),
            typeof(datum_reagent_gluttonytoxin),
            typeof(datum_reagent_aslimetoxin),
            typeof(datum_reagent_blood),
            typeof(datum_reagent_hauntium),
            typeof(datum_reagent_copper));
        Dm.combine(rain, allowed_exotic_reagents);

        Dm.combine(rain, pick(subtypesof(typeof(datum_reagent))));
        Dm.combine(rain, pick(subtypesof(typeof(datum_reagent))));
        Dm.combine(rain, pick(subtypesof(typeof(datum_reagent))));

        whitelist_weather_reagents = GLOB.wizard_rain_reagents;
        await base.New(__extra);
    }
}
