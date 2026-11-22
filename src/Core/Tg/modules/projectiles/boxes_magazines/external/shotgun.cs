// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/boxes_magazines/external/shotgun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2a, D316: the Bulldog's drum magazines). The var blocks are generated (D111).

// tg: code/modules/projectiles/boxes_magazines/external/shotgun.dm:1
public partial class obj_item_ammo_box_magazine_m12g : obj_item_ammo_box_magazine
{
    // tg: code/modules/projectiles/boxes_magazines/external/shotgun.dm:11
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]-[]", base_icon_state, ceil(ammo_count(FALSE) / 8) * 8);
        return dot;
    }
}

// tg: code/modules/projectiles/boxes_magazines/external/shotgun.dm:51
public partial class obj_item_ammo_box_magazine_m12g_donk : obj_item_ammo_box_magazine_m12g
{
    // tg: code/modules/projectiles/boxes_magazines/external/shotgun.dm:59
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        if (ishuman(user)) // tg's own test: a human reads nothing more
            return dot;

        // tg reads any mob as a human here; a mob that is not living has no hands to scratch with (can_perform_action is FALSE)
        if (user is not mob_living human_sniffer)
            return dot;
        if (!HAS_TRAIT(human_sniffer, TRAIT_ANOSMIA) && Dm.truthy(await human_sniffer.can_perform_action(this, NEED_DEXTERITY | NEED_HANDS | ALLOW_RESTING | FORBID_TELEKINESIS_REACH)))
        {
            Dm.append(ref dot, span_notice(I18n.T("You scratch and sniff the stickers.")));
            Dm.append(ref dot, span_warning(I18n.T("<i>Oh god, where did they pull this from, a landfill?</i>")));
            await human_sniffer.add_mood_event("stink-pocket", typeof(datum_mood_event_disgusted));
        }
        return dot;
    }
}
