// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/vatgrowing/samples/_sample.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated (D111).

///This datum is a simple holder for the micro_organisms in a sample.
// tg: code/modules/research/xenobiology/vatgrowing/samples/_sample.dm:2
public partial class datum_biological_sample : datum
{
    ///Generate a sample from a specific weighted list, and a specific amount of cell line with a chance for a virus
    // tg: code/modules/research/xenobiology/vatgrowing/samples/_sample.dm:11
    public virtual async DmTask GenerateSample(object cell_line_define = null, object virus_define = null, object cell_line_amount = null, object virus_chance = null)
    {
        sample_color = pick(GLOB.xeno_sample_colors) as string;

        DmList temp_weight_list = (GLOB.cell_line_tables[cell_line_define] as DmList).Copy(); //Temp list to prevent double picking
        for (float i = 1, i_end = Dm.num(cell_line_amount); i <= i_end; i++)
        {
            Type chosen_type = pick_weight(temp_weight_list) as Type;
            Dm.remove(ref temp_weight_list, chosen_type);
            Dm.append(ref micro_organisms, await Dm.new_async(chosen_type));
        }
        if (prob(Dm.num(virus_chance)))
        {
            if (!Dm.truthy(GLOB.cell_virus_tables[virus_define]))
                return;
            Type chosen_type = pick_weight(GLOB.cell_virus_tables[virus_define] as DmList) as Type;
            Dm.append(ref micro_organisms, await Dm.new_async(chosen_type));
        }
    }

    ///Takes another sample and merges it into use. This can cause one very big sample but we limit it to 3 layers.
    // tg: code/modules/research/xenobiology/vatgrowing/samples/_sample.dm:26
    public virtual object Merge(datum_biological_sample other_sample = null)
    {
        if (sample_layers >= 3)//No more than 3 layers, at that point you're entering danger zone.
            return FALSE;
        Dm.append(ref micro_organisms, other_sample.micro_organisms);
        qdel(other_sample);
        return TRUE;
    }

    ///Call handle_growth on all our microorganisms.
    // tg: code/modules/research/xenobiology/vatgrowing/samples/_sample.dm:34
    public virtual async DmTask handle_growth(obj_machinery_vatgrower vat = null)
    {
        foreach (datum_micro_organism_cell_line organism in Dm.each<datum_micro_organism_cell_line>(micro_organisms)) //Types because we don't grow viruses.
            await organism.handle_growth(vat);
    }

    ///resets the progress of all cell ines
    // tg: code/modules/research/xenobiology/vatgrowing/samples/_sample.dm:39
    public virtual void reset_sample()
    {
        foreach (datum_micro_organism_cell_line organism in Dm.each<datum_micro_organism_cell_line>(micro_organisms)) //Types because we don't grow viruses.
            organism.growth = 0;
    }
}
