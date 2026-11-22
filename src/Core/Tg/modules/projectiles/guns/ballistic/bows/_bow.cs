// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/ballistic/bows/_bow.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the bow, its arrow nocked from the bowstring, drawn and loosed (the drawn state and the nocked
// arrow's overlay), the arrow taken off with alt-click and dropped when the bow leaves the hands. The bowstring
// (/obj/item/ammo_box/magazine/internal/bow) only sets vars. The var blocks are generated (D111).

// tg: code/modules/projectiles/guns/ballistic/bows/_bow.dm:2
public partial class obj_item_gun_ballistic_bow : obj_item_gun_ballistic
{
    // tg: code/modules/projectiles/guns/ballistic/bows/_bow.dm:30
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(drawn) ? "_drawn" : "");
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/bows/_bow.dm:34
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (chambered != null)
            Dm.append(ref dot, Dm.text("[][]", chambered.base_icon_state, Dm.truthy(drawn) ? "_drawn" : ""));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/bows/_bow.dm:39
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (chambered == null)
            return CLICK_ACTION_BLOCKING;

        await user.put_in_hands(chambered);
        chambered = magazine.get_round();
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/ballistic/bows/_bow.dm:48
    public virtual async DmTask drop_arrow()
    {
        await chambered.forceMove(drop_location() as atom);
        chambered = magazine.get_round();
        update_appearance();
    }

    // tg: code/modules/projectiles/guns/ballistic/bows/_bow.dm:53
    public override void chamber_round(float? spin_cylinder = null, float? replace_new_round = null)
    {
        if (chambered != null || magazine == null)
            return;
        chambered = magazine.get_round();
        RegisterSignal(chambered, COMSIG_MOVABLE_MOVED, nameof(clear_chambered));
        update_appearance();
    }

    // tg: code/modules/projectiles/guns/ballistic/bows/_bow.dm:60
    [SignalHandler]
    public override void clear_chambered(datum source = null)
    {
        base.clear_chambered(source);
        drawn = FALSE;
    }

    // tg: code/modules/projectiles/guns/ballistic/bows/_bow.dm:64
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (chambered == null)
        {
            balloon_alert(user, I18n.T("no arrow nocked!"));
            return null;
        }
        balloon_alert(user, Dm.truthy(drawn) ? I18n.T("string released") : I18n.T("string drawn"));
        drawn = Dm.not(drawn);
        playsound(this, Dm.resource("sound/items/weapons/gun/bow/bow_draw.ogg"), 25, TRUE);
        update_appearance();
        return null;
    }

    // tg: code/modules/projectiles/guns/ballistic/bows/_bow.dm:73
    public override async DmTask<object> try_fire_gun(atom target = null, mob_living user = null, object @params = null)
    {
        if (chambered == null)
            return FALSE;
        if (!Dm.truthy(drawn))
        {
            to_chat(user, span_warning(I18n.T("Without drawing the bow, the arrow uselessly falls to the ground.")));
            await drop_arrow();
            return FALSE;
        }
        return await base.try_fire_gun(target, user, @params); //fires, removing the arrow
    }

    // tg: code/modules/projectiles/guns/ballistic/bows/_bow.dm:82
    public override void postfire_empty_checks(object last_shot_succeeded = null)
    {
        if (chambered == null && get_ammo() == 0)
        {
            drawn = FALSE;
            update_appearance();
        }
    }

    // tg: code/modules/projectiles/guns/ballistic/bows/_bow.dm:87
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.eq(slot, ITEM_SLOT_HANDS) && chambered != null)
        {
            balloon_alert(user, I18n.T("the arrow falls out!"));
            if (Dm.truthy(drawn))
                playsound(this, Dm.resource("sound/items/weapons/gun/bow/bow_fire.ogg"), 25, TRUE);
            await drop_arrow();
        }
    }

    // tg: code/modules/projectiles/guns/ballistic/bows/_bow.dm:96
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        _addtimer(Dm.@new<datum_callback>(this, nameof(drop_arrow_if_not_held)), 0.1f * SECONDS, file: "code/modules/projectiles/guns/ballistic/bows/_bow.dm", line: 98);
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/bows/_bow.dm:100
    public virtual async DmTask drop_arrow_if_not_held()
    {
        if (ismob(loc) || chambered == null)
            return;
        if (Dm.truthy(drawn))
            playsound(this, Dm.resource("sound/items/weapons/gun/bow/bow_fire.ogg"), 25, TRUE);
        await drop_arrow();
    }

    // tg: code/modules/projectiles/guns/ballistic/bows/_bow.dm:107
    public override async DmTask shoot_with_empty_chamber(mob_living user = null)
    {
        return; //no clicking sounds please
    }
}
