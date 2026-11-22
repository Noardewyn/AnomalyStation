// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/secret_documents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298: the photocopier copies the documents). The documents' stealing objective (objective_items.dm) is a
// live-partial row (antagonists). The var blocks are generated but the photocopy's forgedseal, which holds a crayon's colour name.

/**
 * # secret documents (photocopy)
 *
 * Outcome of photocopying documents. Can be copied, and can have a blue/red seal forged.
*/
// tg: code/game/objects/items/secret_documents.dm:57
public partial class obj_item_documents_photocopy : obj_item_documents
{
    ///What seal was forged on the documents (color name string)
    // tg: code/game/objects/items/secret_documents.dm:60  (0, then a crayon's colour name: the hand-written member holds either)
    public object forgedseal = 0;

    // tg: code/game/objects/items/secret_documents.dm:58  (the generated constructor, kept with the hand-written var above)
    public obj_item_documents_photocopy()
    {
        desc = "A copy of some top-secret documents. Nobody will notice they aren't the originals... right?";
    }

    // tg: code/game/objects/items/secret_documents.dm:64
    [DmParams("mapload", "copy")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_documents copy = Dm.arg(__extra, 0) as obj_item_documents;
        object dot = base.Initialize(mapload, __extra);
        if (copy != null)
        {
            copy_type = copy.GetType();
            if (copy is obj_item_documents_photocopy C) // Copy Of A Copy Of A Copy
                copy_type = C.copy_type;
        }
        return dot;
    }

    // tg: code/game/objects/items/secret_documents.dm:72
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_toy_crayon_red)) && !istype(tool, typeof(obj_item_toy_crayon_blue)))
            return NONE;
        if (Dm.truthy(forgedseal))
        {
            to_chat(user, span_warning(I18n.T("You have already forged a seal on []!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item_toy_crayon C = (obj_item_toy_crayon)tool;
        name = I18n.T("[] secret documents", C.crayon_color);
        icon_state = Dm.text("docs_[]", C.crayon_color);
        forgedseal = C.crayon_color;
        to_chat(user, span_notice(I18n.T("You forge the official seal with a [] crayon. No one will notice... right?", C.crayon_color)));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }
}
