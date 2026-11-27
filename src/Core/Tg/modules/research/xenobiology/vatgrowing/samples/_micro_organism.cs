// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/vatgrowing/samples/_micro_organism.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated (D111).

///A single type of growth.
// tg: code/modules/research/xenobiology/vatgrowing/samples/_micro_organism.dm:2
public partial class datum_micro_organism : datum
{
    ///Returns a short description of the cell line
    // tg: code/modules/research/xenobiology/vatgrowing/samples/_micro_organism.dm:9
    public virtual object get_details(object show_details = null)
    {
        return span_notice(Dm.text("[]", desc));
    }
}

///A "mob" cell. Can grow into a mob in a growing vat.
// tg: code/modules/research/xenobiology/vatgrowing/samples/_micro_organism.dm:13
public partial class datum_micro_organism_cell_line : datum_micro_organism
{
    ///Handles growth of the micro_organism. This only runs if the micro organism is in the growing vat. Reagents is the growing vats reagents
    // tg: code/modules/research/xenobiology/vatgrowing/samples/_micro_organism.dm:36
    public virtual async DmTask<object> handle_growth(obj_machinery_vatgrower vat = null)
    {
        if (!Dm.truthy(await try_eat(vat.reagents as datum_reagents)))
            return FALSE;
        growth = Dm.max(growth, growth + Dm.num(await calculate_growth(vat.reagents as datum_reagents, vat.biological_sample))); //Prevent you from having minus growth.
        if (growth >= 100)
            await finish_growing(vat);
        return TRUE;
    }

    ///Tries to consume the required reagents. Can only do this if all of them are available. Reagents is the growing vats reagents
    // tg: code/modules/research/xenobiology/vatgrowing/samples/_micro_organism.dm:45
    public virtual async DmTask<object> try_eat(datum_reagents reagents = null)
    {
        foreach (object i in Dm.each(required_reagents))
        {
            if (!Dm.truthy(reagents.has_reagent(i)))
                return FALSE;
        }
        foreach (object i in Dm.each(required_reagents)) //Delete the required reagents if used
            await reagents.remove_reagent(i, consumption_rate);
        return TRUE;
    }

    ///Apply modifiers on growth_rate based on supplementary and supressive reagents. Reagents is the growing vats reagents
    // tg: code/modules/research/xenobiology/vatgrowing/samples/_micro_organism.dm:54
    public virtual async DmTask<object> calculate_growth(datum_reagents reagents = null, datum_biological_sample biological_sample = null)
    {
        float dot = growth_rate;

        //Handle growth based on supplementary reagents here.
        foreach (object i in Dm.each(supplementary_reagents))
        {
            if (!Dm.truthy(reagents.has_reagent(i, consumption_rate)))
                continue;
            dot += Dm.num(supplementary_reagents[i]);
            await reagents.remove_reagent(i, consumption_rate);
        }

        //Handle degrowth based on supressive reagents here.
        foreach (object i in Dm.each(suppressive_reagents))
        {
            if (!Dm.truthy(reagents.has_reagent(i, consumption_rate)))
                continue;
            dot += Dm.num(suppressive_reagents[i]);
            await reagents.remove_reagent(i, consumption_rate);
        }

        //Handle debuffing growth based on viruses here.
        foreach (datum_micro_organism_virus active_virus in Dm.each<datum_micro_organism_virus>(biological_sample.micro_organisms))
        {
            if (Dm.truthy(reagents.has_reagent(typeof(datum_reagent_medicine_spaceacillin), consumption_rate)))
            {
                await reagents.remove_reagent(typeof(datum_reagent_medicine_spaceacillin), consumption_rate);
                continue; //This virus is stopped, We have antiviral stuff
            }
            dot -= virus_suspectibility;
        }
        return dot;
    }

    ///Called once a cell line reaches 100 growth. Then we check if any cell_line is too far so we can perform an epic fail roll
    // tg: code/modules/research/xenobiology/vatgrowing/samples/_micro_organism.dm:79
    public virtual async DmTask<object> finish_growing(obj_machinery_vatgrower vat = null)
    {
        playsound(vat, "sound/effects/splat.ogg", 50, TRUE);
        await succeed_growing(vat);
        vat.biological_sample?.SEND_SIGNAL(COMSIG_SAMPLE_GROWTH_COMPLETED, Dm.list(vat.biological_sample));
        grow_count += 1;
        return TRUE;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/samples/_micro_organism.dm:86
    public virtual async DmTask succeed_growing(obj_machinery_vatgrower vat = null)
    {
        await do_smoke(0, vat, vat.loc);
        for (float x = 1, x_end = resulting_atom_count; x <= x_end; x++)
        {
            atom thing = await Dm.new_async(resulting_atom, get_turf(vat)) as atom;
            ADD_TRAIT(thing, TRAIT_VATGROWN, "vatgrowing");
            vat.visible_message(span_nicegreen(I18n.T("[] pops out of []!", thing, vat)));
            //We maybe add some color. the chance is static for now, but idewally we would be able to manipulate it in the future.
            if (prob(CYTO_SHINY_CHANCE))
            {
                mob_living_basic vat_creature = thing as mob_living_basic;
                //if the mob has a special mutation interaction don't do any other mutations.
                // Once we add more mutations that are stackable with shiny we should probably roll for each type independently.
                if (!isbasicmob(vat_creature) || !Dm.truthy(vat_creature.mutate()))
                    mutate_color(thing);
            }

            vat.biological_sample?.SEND_SIGNAL(COMSIG_SAMPLE_DEPOSITED, Dm.list(vat.biological_sample, thing));
        }
    }

    ///Overriden to show more info like needs, supplementary and supressive reagents and also growth.
    // tg: code/modules/research/xenobiology/vatgrowing/samples/_micro_organism.dm:103
    public override object get_details(object show_details = null)
    {
        object dot = null;
        dot = Dm.add(dot, Dm.text("[]", span_notice(I18n.T("[] - growth progress: []%", desc, growth))));
        if (Dm.truthy(show_details))
        {
            dot = Dm.add(dot, "\n- " + Dm.str(return_reagent_text(I18n.T("Requires:"), required_reagents)));
            dot = Dm.add(dot, "\n- " + Dm.str(return_reagent_text(I18n.T("Likes:"), supplementary_reagents)));
            dot = Dm.add(dot, "\n- " + Dm.str(return_reagent_text(I18n.T("Hates:"), suppressive_reagents)));
        }
        return dot;
    }

    ///Return a nice list of all the reagents in a specific category with a specific prefix. This needs to be reworked because the formatting sucks ass.
    // tg: code/modules/research/xenobiology/vatgrowing/samples/_micro_organism.dm:111
    public virtual object return_reagent_text(string prefix_text = null, DmList reagentlist = null)
    {
        prefix_text ??= "It requires:";
        if (!Dm.truthy(reagentlist.len))
            return null;
        DmList reagent_names = Dm.list();
        foreach (object i in Dm.each(reagentlist))
        {
            Type reagent = i as Type;
            Dm.append(ref reagent_names, Dm.initial(reagent, (datum_reagent r) => r.name));
        }
        return span_notice(Dm.text("[] []", prefix_text, jointext(reagent_names, ", ")));
    }

    //Adds a hue shift filter and affix to an atom.
    // tg: code/modules/research/xenobiology/vatgrowing/samples/_micro_organism.dm:121
    public virtual object mutate_color(atom beautiful_mutant = null)
    {
        //This determines how much the hue of the atom is shifted, 0.5 is the most extreme, respresenting a 180° hue shift.
        float hue_shift = 1;
        //This affix is added to the name of the atom, to help players understand and converse about the different rarity levels.
        string rarity_affix = "glitched";
        // for adding some unique effects for subtypes, should you wish to do so
        float strength_factor = 1;

        float sw__1 = rand(1, 100);
        if (sw__1 >= 1 && sw__1 <= 35) //35% chance : painted
        {
            hue_shift = 0.15f;
            rarity_affix = "painted";
            strength_factor = 1.2f;
        }
        else if (sw__1 >= 36 && sw__1 <= 70) //35% chance : mutant
        {
            hue_shift = 0.85f; //this value is equivalent in distance as the painted tier, just in the other direction.
            rarity_affix = "mutant";
            strength_factor = 1.4f;
        }
        else if (sw__1 >= 71 && sw__1 <= 83) //13% chance : rare
        {
            hue_shift = 0.3f;
            rarity_affix = "rare";
            strength_factor = 1.6f;
        }
        else if (sw__1 >= 84 && sw__1 <= 95) //12% chance : shiny
        {
            hue_shift = 0.7f;
            rarity_affix = "shiny";
            strength_factor = 2;
        }
        else if (sw__1 >= 96 && sw__1 <= 100) //5% chance : mutant king
        {
            hue_shift = 0.5f; //Best in show, most extreme color change.
            rarity_affix = "mutant king"; //The name is up to debate, since cyto is all about freaky creatures I thought it was good choice over something like "cosmic" or "regal".
            strength_factor = 2.5f;
        }

        DmList mutant_shift_matrix = Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, hue_shift, 0, 0, 0); //matrix for our colour shifting.

        //Here we make the changes for the atom, we apply a filter and change the name to indicate rarity.
        beautiful_mutant.add_filter("shiny mutation", 15, color_matrix_filter(mutant_shift_matrix, FILTER_COLOR_HSL) as DmList);
        beautiful_mutant.name = Dm.text("[] []", rarity_affix, beautiful_mutant.name);
        if (isliving(beautiful_mutant)) //update the real name var if it's actually a living mob
        {
            mob_living living_mutie = (mob_living)beautiful_mutant;
            living_mutie.real_name = living_mutie.name;
        }

        return strength_factor;
    }
}
