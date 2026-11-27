// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/xenobiology.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (cytology's weight tables of the swabable element).

///These global lists exist to allow our element to have weight tables without having to be separate instances.

public partial class datum_controller_global_vars
{
    ///Assoc list of cell line define | assoc list of datum | cell_line
    // tg: code/_globalvars/lists/xenobiology.dm:4  GLOBAL_LIST_INIT_TYPED(cell_line_tables, /list, list(...))
    public DmList cell_line_tables;

    // tg: code/_globalvars/lists/xenobiology.dm:4  (GLOBAL_MANAGED(cell_line_tables, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcell_line_tables()
    {
        cell_line_tables = Dm.list(
            Dm.kv(CELL_LINE_TABLE_SLUDGE, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_blobbernaut), 1), Dm.kv(typeof(datum_micro_organism_cell_line_chicken), 1), Dm.kv(typeof(datum_micro_organism_cell_line_cockroach), 2), Dm.kv(typeof(datum_micro_organism_cell_line_cow), 1), Dm.kv(typeof(datum_micro_organism_cell_line_mouse), 1), Dm.kv(typeof(datum_micro_organism_cell_line_sholean_grapes), 2), Dm.kv(typeof(datum_micro_organism_cell_line_slime), 2), Dm.kv(typeof(datum_micro_organism_cell_line_goat), 1), Dm.kv(typeof(datum_micro_organism_cell_line_sheep), 1), Dm.kv(typeof(datum_micro_organism_cell_line_lizard), 1))),
            Dm.kv(CELL_LINE_TABLE_MOIST, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_carp), 1), Dm.kv(typeof(datum_micro_organism_cell_line_cockroach), 1), Dm.kv(typeof(datum_micro_organism_cell_line_gelatinous_cube), 2), Dm.kv(typeof(datum_micro_organism_cell_line_megacarp), 1), Dm.kv(typeof(datum_micro_organism_cell_line_slime), 2), Dm.kv(typeof(datum_micro_organism_cell_line_snake), 1), Dm.kv(typeof(datum_micro_organism_cell_line_glockroach), 1), Dm.kv(typeof(datum_micro_organism_cell_line_hauberoach), 1), Dm.kv(typeof(datum_micro_organism_cell_line_stoat), 1), Dm.kv(typeof(datum_micro_organism_cell_line_mothroach), 1), Dm.kv(typeof(datum_micro_organism_cell_line_penguin), 1))),
            Dm.kv(CELL_LINE_TABLE_BLOB, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_blob_spore), 1), Dm.kv(typeof(datum_micro_organism_cell_line_blobbernaut), 1))),
            Dm.kv(CELL_LINE_TABLE_MOLD, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_bear), 1), Dm.kv(typeof(datum_micro_organism_cell_line_blob_spore), 1), Dm.kv(typeof(datum_micro_organism_cell_line_cat), 1), Dm.kv(typeof(datum_micro_organism_cell_line_cockroach), 1), Dm.kv(typeof(datum_micro_organism_cell_line_corgi), 1), Dm.kv(typeof(datum_micro_organism_cell_line_mouse), 2), Dm.kv(typeof(datum_micro_organism_cell_line_slime), 1), Dm.kv(typeof(datum_micro_organism_cell_line_vat_beast), 2), Dm.kv(typeof(datum_micro_organism_cell_line_pig), 1), Dm.kv(typeof(datum_micro_organism_cell_line_fox), 1), Dm.kv(typeof(datum_micro_organism_cell_line_rabbit), 1))),
            Dm.kv(CELL_LINE_TABLE_BEAR, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_bear), 1))),
            Dm.kv(CELL_LINE_TABLE_BLOBBERNAUT, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_blobbernaut), 1))),
            Dm.kv(CELL_LINE_TABLE_BLOBSPORE, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_blob_spore), 1))),
            Dm.kv(CELL_LINE_TABLE_CARP, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_carp), 1))),
            Dm.kv(CELL_LINE_TABLE_CAT, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_cat), 1))),
            Dm.kv(CELL_LINE_TABLE_CHICKEN, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_chicken), 1))),
            Dm.kv(CELL_LINE_TABLE_COCKROACH, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_cockroach), 1))),
            Dm.kv(CELL_LINE_TABLE_CORGI, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_corgi), 1))),
            Dm.kv(CELL_LINE_TABLE_COW, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_cow), 1))),
            Dm.kv(CELL_LINE_TABLE_MOONICORN, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_moonicorn), 1))),
            Dm.kv(CELL_LINE_TABLE_GELATINOUS, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_gelatinous_cube), 1))),
            Dm.kv(CELL_LINE_TABLE_GLOCKROACH, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_glockroach), 1))),
            Dm.kv(CELL_LINE_TABLE_GRAPE, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_sholean_grapes), 1))),
            Dm.kv(CELL_LINE_TABLE_HAUBEROACH, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_hauberoach), 1))),
            Dm.kv(CELL_LINE_TABLE_MEGACARP, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_megacarp), 1))),
            Dm.kv(CELL_LINE_TABLE_PENGUIN, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_penguin), 1))),
            Dm.kv(CELL_LINE_TABLE_MOUSE, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_mouse), 1))),
            Dm.kv(CELL_LINE_TABLE_PINE, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_pine), 1))),
            Dm.kv(CELL_LINE_TABLE_PUG, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_pug), 1))),
            Dm.kv(CELL_LINE_TABLE_FOX, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_fox), 1))),
            Dm.kv(CELL_LINE_TABLE_STOAT, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_stoat), 1))),
            Dm.kv(CELL_LINE_TABLE_RABBIT, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_rabbit), 1))),
            Dm.kv(CELL_LINE_TABLE_MOTHROACH, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_mothroach), 1))),
            Dm.kv(CELL_LINE_TABLE_SLIME, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_slime), 1))),
            Dm.kv(CELL_LINE_TABLE_SNAKE, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_snake), 1))),
            Dm.kv(CELL_LINE_TABLE_LIZARD, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_lizard), 1))),
            Dm.kv(CELL_LINE_TABLE_SNAIL, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_snail), 1))),
            Dm.kv(CELL_LINE_TABLE_VATBEAST, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_vat_beast), 1))),
            Dm.kv(CELL_LINE_TABLE_NETHER, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_netherworld), 1))),
            Dm.kv(CELL_LINE_TABLE_CLOWN, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_clown_bananaclown), 1), Dm.kv(typeof(datum_micro_organism_cell_line_clown_glutton), 1), Dm.kv(typeof(datum_micro_organism_cell_line_clown_longclown), 1))),
            Dm.kv(CELL_LINE_TABLE_GLUTTON, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_clown_glutton), 1))),
            Dm.kv(CELL_LINE_TABLE_CLOWNANA, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_clown_bananaclown), 1))),
            Dm.kv(CELL_LINE_TABLE_LONGFACE, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_clown_longclown), 1))),
            Dm.kv(CELL_LINE_TABLE_FROG, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_frog), 1))),
            Dm.kv(CELL_LINE_TABLE_AXOLOTL, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_axolotl), 1))),
            Dm.kv(CELL_LINE_TABLE_WALKING_MUSHROOM, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_walking_mushroom), 1))),
            Dm.kv(CELL_LINE_TABLE_QUEEN_BEE, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_queen_bee), 1))),
            Dm.kv(CELL_LINE_TABLE_BUTTERFLY, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_butterfly), 1))),
            Dm.kv(CELL_LINE_TABLE_MEGA_ARACHNID, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_mega_arachnid), 1))),
            Dm.kv(CELL_LINE_TABLE_GOAT, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_goat), 1))),
            Dm.kv(CELL_LINE_TABLE_SHEEP, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_sheep), 1))),
            Dm.kv(CELL_LINE_TABLE_PIG, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_pig), 1))),
            Dm.kv(CELL_LINE_TABLE_ALGAE, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_frog), 2), Dm.kv(typeof(datum_micro_organism_cell_line_mega_arachnid), 1), Dm.kv(typeof(datum_micro_organism_cell_line_queen_bee), 1), Dm.kv(typeof(datum_micro_organism_cell_line_butterfly), 1), Dm.kv(typeof(datum_micro_organism_cell_line_snake), 1), Dm.kv(typeof(datum_micro_organism_cell_line_walking_mushroom), 2), Dm.kv(typeof(datum_micro_organism_cell_line_axolotl), 1))),
            Dm.kv(CELL_LINE_ORGAN_HEART, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_organs_heart), 3), Dm.kv(typeof(datum_micro_organism_cell_line_organs_heart_evolved), 1))),
            Dm.kv(CELL_LINE_ORGAN_HEART_CURSED, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_organs_heart_corrupt), 1), Dm.kv(typeof(datum_micro_organism_cell_line_organs_heart_sacred), 1))),
            Dm.kv(CELL_LINE_ORGAN_LUNGS, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_organs_lungs), 3), Dm.kv(typeof(datum_micro_organism_cell_line_organs_lungs_evolved), 1))),
            Dm.kv(CELL_LINE_ORGAN_LIVER, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_organs_liver), 3), Dm.kv(typeof(datum_micro_organism_cell_line_organs_liver_evolved), 1), Dm.kv(typeof(datum_micro_organism_cell_line_organs_liver_bloody), 1), Dm.kv(typeof(datum_micro_organism_cell_line_organs_liver_distillery), 1))),
            Dm.kv(CELL_LINE_ORGAN_STOMACH, Dm.list(Dm.kv(typeof(datum_micro_organism_cell_line_organs_stomach), 3), Dm.kv(typeof(datum_micro_organism_cell_line_organs_stomach_evolved), 1)))
        );
    }

    ///Assoc list of cell virus define | assoc list of datum | cell_virus
    // tg: code/_globalvars/lists/xenobiology.dm:132  GLOBAL_LIST_INIT(cell_virus_tables, list(...))
    public DmList cell_virus_tables;

    // tg: code/_globalvars/lists/xenobiology.dm:132  (GLOBAL_MANAGED(cell_virus_tables, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcell_virus_tables()
    {
        cell_virus_tables = Dm.list(
            Dm.kv(CELL_VIRUS_TABLE_GENERIC, Dm.list(Dm.kv(typeof(datum_micro_organism_virus), 1))),
            Dm.kv(CELL_VIRUS_TABLE_GENERIC_MOB, Dm.list(Dm.kv(typeof(datum_micro_organism_virus), 1)))
        );
    }

    ///List of all possible sample colors
    // tg: code/_globalvars/lists/xenobiology.dm:138  GLOBAL_LIST_INIT(xeno_sample_colors, list(...))
    public DmList xeno_sample_colors;

    // tg: code/_globalvars/lists/xenobiology.dm:138  (GLOBAL_MANAGED(xeno_sample_colors, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalxeno_sample_colors()
    {
        xeno_sample_colors = Dm.list(
            COLOR_SAMPLE_BROWN,
            COLOR_SAMPLE_GRAY,
            COLOR_SAMPLE_GREEN,
            COLOR_SAMPLE_PURPLE,
            COLOR_SAMPLE_YELLOW
        );
    }
}
