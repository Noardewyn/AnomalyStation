// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/fedora.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the fedoras' Initialize (the hat's pockets, datums/storage/subtypes/pockets.cs); gaps CL1 (D333): the carpskin fedora (with its fishing
// difficulty element) and the fedora's suicide act. The var blocks are generated.

// tg: code/modules/clothing/head/fedora.dm:1
public partial class obj_item_clothing_head_fedora : obj_item_clothing_head
{
    // tg: code/modules/clothing/head/fedora.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_small_fedora)));
        return dot;
    }

    // tg: code/modules/clothing/head/fedora.dm:25
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (Dm.eq(user.gender, FEMALE))
            return null;
        mob_living_carbon_human H = (mob_living_carbon_human)user;
        user.visible_message(span_suicide(I18n.T("[] is donning []! It looks like [] trying to be nice to girls.", user, this, user.p_theyre())));
        await user.say(I18n.T("M'lady."), forced: "fedora suicide");
        await sleep(1 * SECONDS);
        H.facial_hairstyle = "Neckbeard";
        return BRUTELOSS;
    }
}

// tg: code/modules/clothing/head/fedora.dm:35
public partial class obj_item_clothing_head_fedora_carpskin : obj_item_clothing_head_fedora
{
    // tg: code/modules/clothing/head/fedora.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -6));
        return dot;
    }
}
