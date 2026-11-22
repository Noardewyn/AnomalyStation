// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/courtroom.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DE1 (D333): the whole file: the gavel (a knee tapper, datums/elements/kneejerk.dm) bangs on the sound block with tg's gavel
// sound and a melee click's cooldown. The var blocks are generated (D111).

// Contains:
// Gavel
// Sound Block

// tg: code/game/objects/items/courtroom.dm:5
public partial class obj_item_gavelhammer : obj_item
{
    // tg: code/game/objects/items/courtroom.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_kneejerk)));
        return dot;
    }

    // tg: code/game/objects/items/courtroom.dm:23
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] has sentenced []self to death with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        playsound(loc, Dm.resource("sound/items/gavel.ogg"), 50, TRUE, -1);
        return BRUTELOSS;
    }
}

// tg: code/game/objects/items/courtroom.dm:28
public partial class obj_item_gavelblock : obj_item
{
    // tg: code/game/objects/items/courtroom.dm:39
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_gavelhammer))
            return NONE;
        playsound(loc, Dm.resource("sound/items/gavel.ogg"), 100, TRUE);
        user.visible_message(span_warning(I18n.T("[] strikes [] with [].", user, this, tool)));
        user.changeNext_move(CLICK_CD_MELEE);
        return ITEM_INTERACT_SUCCESS;
    }
}
