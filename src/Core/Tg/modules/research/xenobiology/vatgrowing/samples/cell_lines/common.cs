// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/vatgrowing/samples/cell_lines/common.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: every cell line is var-only but these few. A cell line whose mob is not ported yet grows that
// inert mob (not-translated.md section 242 names each one's package). The var blocks are generated (D111).

// tg: code/modules/research/xenobiology/vatgrowing/samples/cell_lines/common.dm:623
public partial class datum_micro_organism_cell_line_vat_beast : datum_micro_organism_cell_line
{
    // tg: code/modules/research/xenobiology/vatgrowing/samples/cell_lines/common.dm:651
    public override async DmTask succeed_growing(obj_machinery_vatgrower vat = null)
    {
        await base.succeed_growing(vat);
        qdel(vat);
    }
}

//randomizes from the netherworld pool!
// tg: code/modules/research/xenobiology/vatgrowing/samples/cell_lines/common.dm:656
public partial class datum_micro_organism_cell_line_netherworld : datum_micro_organism_cell_line
{
    // tg: code/modules/research/xenobiology/vatgrowing/samples/cell_lines/common.dm:678
    public override async DmTask succeed_growing(obj_machinery_vatgrower vat = null)
    {
        resulting_atom = pick(typeof(mob_living_basic_creature), typeof(mob_living_basic_migo), typeof(mob_living_basic_blankbody)) as Type; //i looked myself, pretty much all of them are reasonably strong and somewhat on the same level. except migo is the jackpot and the blank body is whiff.
        await base.succeed_growing(vat);
    }
}

// tg: code/modules/research/xenobiology/vatgrowing/samples/cell_lines/common.dm:682
public partial class datum_micro_organism_cell_line_clown : datum_micro_organism_cell_line
{
    // tg: code/modules/research/xenobiology/vatgrowing/samples/cell_lines/common.dm:684
    public override async DmTask succeed_growing(obj_machinery_vatgrower vat = null)
    {
        if (Dm.truthy((vat.reagents as datum_reagents).has_reagent(typeof(datum_reagent_toxin_mutagen))) && prob(CYTO_SHINY_CHANCE))
        {
            resulting_atom = pick(typeof(mob_living_basic_clown_mutant), typeof(mob_living_basic_clown_fleshclown)) as Type;
            vat.visible_message(span_warning(I18n.T("The biological sample in [] mutates into something horrific!", vat)));
        }
        else
            resulting_atom = Dm.initial(this, (datum_micro_organism_cell_line_clown p) => p.resulting_atom);
        await base.succeed_growing(vat);
    }

    // tg: code/modules/research/xenobiology/vatgrowing/samples/cell_lines/common.dm:692
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // all clown mutations get a huge boost from mutagen, at the cost of causing occasional mutations
        supplementary_reagents[typeof(datum_reagent_toxin_mutagen)] = 8;
    }
}

// tg: code/modules/research/xenobiology/vatgrowing/samples/cell_lines/common.dm:838
public partial class datum_micro_organism_cell_line_queen_bee : datum_micro_organism_cell_line
{
    // tg: code/modules/research/xenobiology/vatgrowing/samples/cell_lines/common.dm:863
    public override async DmTask succeed_growing(obj_machinery_vatgrower vat = null)
    {
        if (Dm.truthy(Dm.mod(grow_count, 2))) // every other growth cycle spawns a horde of bees insteads
        {
            resulting_atom_count = 5;
            resulting_atom = typeof(mob_living_basic_bee);
            vat.visible_message(span_warning(I18n.T("You hear angry buzzing coming from the inside of the vat!")));
        }
        else
        {
            resulting_atom_count = Dm.initial(this, (datum_micro_organism_cell_line_queen_bee p) => p.resulting_atom_count);
            resulting_atom = Dm.initial(this, (datum_micro_organism_cell_line_queen_bee p) => p.resulting_atom);
        }
        await base.succeed_growing(vat);
    }
}
