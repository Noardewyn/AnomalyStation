// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/energy/net_snare.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SE1 (D333, the security gear): the whole file: the DRAGnet's energy snare (it staggers the living it hits and opens a
// teleportation field on every open turf around them), the field (three seconds later it sends the living on it to the DRAGnet's
// synced beacon, or scatters them up to 15 tiles away without one) and the DRAGnet beacon (synced with a DRAGnet, wrenched down,
// locked by a security ID, unlocked by an emag). The var blocks are generated (D111).

// tg: code/modules/projectiles/projectile/energy/net_snare.dm:1
public partial class obj_projectile_energy_snare : obj_projectile_energy
{
    // tg: code/modules/projectiles/projectile/energy/net_snare.dm:9
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        SpinAnimation();
        return dot;
    }

    // tg: code/modules/projectiles/projectile/energy/net_snare.dm:13
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        obj_item_dragnet_beacon destination_beacon = null;
        var our_dragnet = fired_from as obj_item_gun_energy_e_gun_dragnet;
        if (our_dragnet != null)
            destination_beacon = our_dragnet.linked_beacon;

        if (isliving(target))
        {
            var living_target = (mob_living)target;
            await living_target.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * 0.5f, 5 * SECONDS);
            foreach (turf trapped_turf in Dm.each<turf>(range(1, get_turf(living_target))))
            {
                if (Dm.truthy(trapped_turf.density))
                    continue;
                Dm.@new<obj_effect_nettingportal>(trapped_turf, destination_beacon);
            }
        }

        return await base.on_hit(target, blocked, pierce_hit);
    }

    // tg: code/modules/projectiles/projectile/energy/net_snare.dm:29
    public override async DmTask on_range()
    {
        do_sparks(1, TRUE, this);
        await base.on_range();
    }
}

// tg: code/modules/projectiles/projectile/energy/net_snare.dm:33
public partial class obj_effect_nettingportal : obj_effect
{
    // tg: code/modules/projectiles/projectile/energy/net_snare.dm:41
    [DmParams("mapload", "destination_beacon")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object destination_beacon = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        var teletarget = destination_beacon as obj_item_dragnet_beacon;
        _addtimer(Dm.@new<datum_callback>(this, nameof(pop), teletarget), 3 * SECONDS, file: "code/modules/projectiles/projectile/energy/net_snare.dm", line: 44);
        return dot;
    }

    // tg: code/modules/projectiles/projectile/energy/net_snare.dm:46
    public virtual async DmTask pop(object teletarget = null)
    {
        if (Dm.truthy(teletarget))
        {
            foreach (mob_living living_mob in Dm.each<mob_living>(get_turf(this)))
                await do_teleport(living_mob, get_turf((atom)teletarget), 1, channel: TELEPORT_CHANNEL_BLUESPACE); //Teleport what's in the tile to the beacon
        }
        else
        {
            foreach (mob_living living_mob in Dm.each<mob_living>(get_turf(this)))
                await do_teleport(living_mob, get_turf(living_mob), 15, channel: TELEPORT_CHANNEL_BLUESPACE); //Otherwise it just warps you off somewhere.
        }

        qdel(this);
    }

    // tg: code/modules/projectiles/projectile/energy/net_snare.dm:56
    public override async DmTask<object> singularity_act()
    {
        return null;
    }

    // tg: code/modules/projectiles/projectile/energy/net_snare.dm:59
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }
}

// tg: code/modules/projectiles/projectile/energy/net_snare.dm:62
public partial class obj_item_dragnet_beacon : obj_item
{
    // tg: code/modules/projectiles/projectile/energy/net_snare.dm:74
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_gun_energy_e_gun_dragnet dragnet_to_link)
        {
            dragnet_to_link.link_beacon(user, this);
            return null;
        }

        if (isidcard(tool))
        {
            if (!Dm.truthy(anchored))
            {
                balloon_alert(user, I18n.T("wrench the beacon first!"));
                return null;
            }

            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            {
                balloon_alert(user, I18n.T("the access control is fried!"));
                return null;
            }

            var id_card = (obj_item_card_id)tool;
            if (Dm.@in(ACCESS_SECURITY, id_card.GetAccess()))
            {
                locked = Dm.truthy(locked) ? 0 : 1;
                balloon_alert(user, I18n.T("beacon []", Dm.truthy(locked) ? I18n.T("locked") : I18n.T("unlocked")));
            }
            else
                balloon_alert(user, I18n.T("no access!"));
        }
        return null;
    }

    // tg: code/modules/projectiles/projectile/energy/net_snare.dm:96
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.is_holding(this)))
        {
            balloon_alert(user, I18n.T("put it down first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(anchored) && Dm.truthy(locked))
        {
            balloon_alert(user, I18n.T("must be unlocked first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(isinspace()) && !Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("nothing to anchor to!"));
            return ITEM_INTERACT_BLOCKING;
        }

        await set_anchored(Dm.truthy(anchored) ? 0 : 1);
        tool.play_tool_sound(this, 75);
        user.balloon_alert_to_viewers(Dm.truthy(anchored) ? I18n.T("anchored") : I18n.T("unanchored"));
        return null;
    }

    // tg: code/modules/projectiles/projectile/energy/net_snare.dm:113
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        locked = 0;
        await set_anchored(FALSE);
        do_sparks(3, TRUE, this);
        balloon_alert(user, I18n.T("beacon unlocked"));
        return TRUE;
    }
}
