// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/designs/mechfabricator_designs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): this file's design procs (SSresearch makes every design); the designs are var blocks (generated, D111).

// tg: code/modules/research/designs/mechfabricator_designs.dm:1810
public partial class datum_design_mod_plating : datum_design
{
    // tg: code/modules/research/designs/mechfabricator_designs.dm:1827
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Type armor_type = build_path;
        datum_mod_theme theme = GLOB.mod_themes[Dm.initial(armor_type, (obj_item_mod_construction_plating p) => p.theme)] as datum_mod_theme;
        desc = Dm.text("External plating for a MODsuit. []", theme.desc);
    }
}

// tg: code/modules/research/designs/mechfabricator_designs.dm:1933
public partial class datum_design_module : datum_design
{
    // tg: code/modules/research/designs/mechfabricator_designs.dm:1947
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Type module = build_path;
        desc = Dm.text("[] It uses [] complexity.", Dm.initial(module, (obj_item_mod_module m) => m.desc), Dm.initial(module, (obj_item_mod_module m) => m.complexity));
    }
}
