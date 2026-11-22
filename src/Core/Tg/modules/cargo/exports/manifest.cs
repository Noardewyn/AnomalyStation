// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/exports/manifest.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the whole file: a stamped manifest earns its handling charge back, an approved one with errors costs, a correctly denied one refunds the order. The var blocks are generated.

// The manifest charges (file-local, #undef at :86-91)
internal static class ManifestCharges
{
    // tg: code/modules/cargo/exports/manifest.dm:1
    internal const float MANIFEST_HANDLING_RATE = 0.12f;
    // tg: code/modules/cargo/exports/manifest.dm:2
    internal const float MANIFEST_CORRECT_RATE = MANIFEST_HANDLING_RATE * 2;
    // tg: code/modules/cargo/exports/manifest.dm:3
    internal const float MANIFEST_ERRONEOUS_RATE = MANIFEST_HANDLING_RATE * 4;
    // tg: code/modules/cargo/exports/manifest.dm:4
    internal const float MAX_HANDLING_CHARGE = CARGO_CRATE_VALUE * 0.6f;
    // tg: code/modules/cargo/exports/manifest.dm:5
    internal const float MAX_CORRECT_CHARGE = MAX_HANDLING_CHARGE * 2;
    // tg: code/modules/cargo/exports/manifest.dm:6
    internal const float MAX_ERRONEOUS_CHARGE = MAX_HANDLING_CHARGE * 4;
}

// tg: code/modules/cargo/exports/manifest.dm:9
public partial class datum_export_manifest_correct : datum_export
{
    // tg: code/modules/cargo/exports/manifest.dm:15
    public override object applies_to(obj exported_item = null, object apply_elastic = null, DmList export_markets = null)
    {
        if (!Dm.truthy(base.applies_to(exported_item, apply_elastic, export_markets)))
            return FALSE;
        var export_manifest = (obj_item_paper_fluff_jobs_cargo_manifest)exported_item;
        if (export_manifest.is_approved() && !Dm.truthy(export_manifest.errors))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/cargo/exports/manifest.dm:24
    public override object get_base_cost(obj exported_item = null)
    {
        var manifest = (obj_item_paper_fluff_jobs_cargo_manifest)exported_item;
        return min(manifest.order_cost * ManifestCharges.MANIFEST_HANDLING_RATE, ManifestCharges.MAX_HANDLING_CHARGE);
    }
}

// tg: code/modules/cargo/exports/manifest.dm:28
public partial class datum_export_manifest_error_denied : datum_export
{
    // tg: code/modules/cargo/exports/manifest.dm:34
    public override object applies_to(obj exported_item = null, object apply_elastic = null, DmList export_markets = null)
    {
        if (!Dm.truthy(base.applies_to(exported_item, apply_elastic, export_markets)))
            return FALSE;
        var export_manifest = (obj_item_paper_fluff_jobs_cargo_manifest)exported_item;
        if (export_manifest.is_denied() && Dm.truthy(export_manifest.errors))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/cargo/exports/manifest.dm:43
    public override object get_base_cost(obj exported_item = null)
    {
        var manifest = (obj_item_paper_fluff_jobs_cargo_manifest)exported_item;
        return manifest.order_cost + min(manifest.order_cost * ManifestCharges.MANIFEST_CORRECT_RATE, ManifestCharges.MAX_CORRECT_CHARGE);
    }
}

// tg: code/modules/cargo/exports/manifest.dm:47
public partial class datum_export_manifest_error : datum_export
{
    // tg: code/modules/cargo/exports/manifest.dm:54
    public override object applies_to(obj exported_item = null, object apply_elastic = null, DmList export_markets = null)
    {
        if (!Dm.truthy(base.applies_to(exported_item, apply_elastic, export_markets)))
            return FALSE;
        var export_manifest = (obj_item_paper_fluff_jobs_cargo_manifest)exported_item;
        if (export_manifest.is_approved() && Dm.truthy(export_manifest.errors))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/cargo/exports/manifest.dm:63
    public override object get_base_cost(obj exported_item = null)
    {
        var manifest = (obj_item_paper_fluff_jobs_cargo_manifest)exported_item;
        return -min(manifest.order_cost * ManifestCharges.MANIFEST_ERRONEOUS_RATE, ManifestCharges.MAX_ERRONEOUS_CHARGE);
    }
}

// tg: code/modules/cargo/exports/manifest.dm:67
public partial class datum_export_manifest_correct_denied : datum_export
{
    // tg: code/modules/cargo/exports/manifest.dm:74
    public override object applies_to(obj exported_item = null, object apply_elastic = null, DmList export_markets = null)
    {
        if (!Dm.truthy(base.applies_to(exported_item, apply_elastic, export_markets)))
            return FALSE;
        var export_manifest = (obj_item_paper_fluff_jobs_cargo_manifest)exported_item;
        if (export_manifest.is_denied() && !Dm.truthy(export_manifest.errors))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/cargo/exports/manifest.dm:83
    public override object get_base_cost(obj exported_item = null)
    {
        var manifest = (obj_item_paper_fluff_jobs_cargo_manifest)exported_item;
        return -min(manifest.order_cost * ManifestCharges.MANIFEST_ERRONEOUS_RATE, ManifestCharges.MAX_ERRONEOUS_CHARGE);
    }
}
