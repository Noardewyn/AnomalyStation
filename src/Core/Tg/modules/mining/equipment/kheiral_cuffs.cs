// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/kheiral_cuffs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The Kheiral cuffs: worn on the wrist (or on the jumpsuit) off the station's level they switch on a GPS signal named after the
// wearer's ID and let the crew monitor see the wearer's suit sensors across levels; back on the station both go off. Their glow
// (the emissive overlays) is not drawn (DV12).

/**********************Kheiral Cuffs**********************/
/// Acts as a GPS beacon & connects to station crew monitors from lavaland
// tg: code/modules/mining/equipment/kheiral_cuffs.dm:3
public partial class obj_item_clothing_accessory_kheiral_cuffs : obj_item_clothing_accessory
{
    // tg: code/modules/mining/equipment/kheiral_cuffs.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(update_icon(UPDATE_OVERLAYS));
        RegisterSignal(this, COMSIG_MOVABLE_Z_CHANGED, nameof(check_z));

        check_z(new_turf: loc as turf);
        return dot;
    }

    // tg: code/modules/mining/equipment/kheiral_cuffs.dm:36
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        var dot = (DmList)await base.examine(user, __extra);
        if (Dm.truthy(gps_enabled))
            Dm.append(ref dot, span_notice(I18n.T("The cuff's GPS signal is on.")));
        return dot;
    }

    // tg: code/modules/mining/equipment/kheiral_cuffs.dm:41
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_GLOVES)) && !Dm.truthy(Dm.bitand(slot, ITEM_SLOT_ICLOTHING)))
            return;
        on_wrist = TRUE;
        playsound(loc, Dm.resource("sound/items/weapons/handcuffs.ogg"), 30, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        await connect_kheiral_network(user as mob_living);
    }

    // tg: code/modules/mining/equipment/kheiral_cuffs.dm:49
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (Dm.truthy(on_wrist))
            playsound(loc, Dm.resource("sound/items/weapons/handcuffs.ogg"), 30, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        on_wrist = FALSE;
        remove_kheiral_network(user);
        return dot;
    }

    /// Enables the GPS and adds the multiz trait
    // tg: code/modules/mining/equipment/kheiral_cuffs.dm:57
    public virtual async DmTask connect_kheiral_network(mob_living user = null)
    {
        if (Dm.truthy(gps_enabled))
            return;
        if (!Dm.truthy(on_wrist) || !Dm.truthy(far_from_home))
            return;
        object gps_name = "Unknown";
        var id_card = user.get_idcard(hand_first: FALSE) as obj_item_card_id;
        if (id_card != null)
            gps_name = id_card.registered_name;
        await AddComponent(typeof(datum_component_gps_kheiral_cuffs), Dm.text("*[]'s Kheiral Link", gps_name));
        balloon_alert(user, "gps activated");
        ADD_TRAIT(user, TRAIT_MULTIZ_SUIT_SENSORS, REF(this));
        gps_enabled = TRUE;
    }

    /// Disables the GPS and removes the multiz trait
    // tg: code/modules/mining/equipment/kheiral_cuffs.dm:72
    public virtual void remove_kheiral_network(mob user = null)
    {
        if (!Dm.truthy(gps_enabled))
            return;
        if (Dm.truthy(on_wrist) && Dm.truthy(far_from_home))
            return;
        balloon_alert(user, "gps de-activated"); // GPS component deletes itself when we get on-Z
        REMOVE_TRAIT(user, TRAIT_MULTIZ_SUIT_SENSORS, REF(this));
        gps_enabled = FALSE;
    }

    /// If we're off the Z-level, set far_from_home = TRUE. If being worn, trigger kheiral_network proc
    // tg: code/modules/mining/equipment/kheiral_cuffs.dm:82
    [SignalHandler]
    public virtual void check_z(datum source = null, turf old_turf = null, turf new_turf = null)
    {
        if (!isturf(new_turf))
            return;

        if (Dm.truthy(is_station_level(new_turf.z)))
        {
            far_from_home = FALSE;
            // for the "worn on glove slot" case
            if (isliving(loc))
                remove_kheiral_network((mob)loc);
            else if (isliving(loc.loc)) // for the "worn as accessory" case
                remove_kheiral_network((mob)loc.loc);
        }
        else
        {
            far_from_home = TRUE;
            // for the "worn on glove slot" case
            if (isliving(loc))
                Waiting.NoSleep(connect_kheiral_network((mob_living)loc));
            else if (isliving(loc.loc)) // for the "worn as accessory" case
                Waiting.NoSleep(connect_kheiral_network((mob_living)loc.loc));
        }
    }

    // tg: code/modules/mining/equipment/kheiral_cuffs.dm:103
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        // DEVIATION(DV12): if(!isinhands) . += emissive_appearance(icon_file, "strandcuff_emissive", src, alpha = src.alpha), the worn cuffs'
        // glow on the emissive plane, is not created
        return dot;
    }

    // tg: code/modules/mining/equipment/kheiral_cuffs.dm:108
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // DEVIATION(DV12): . += emissive_appearance(icon, "strand_light", src, alpha = src.alpha), the cuffs' glow on the emissive plane,
        // is not created
        return dot;
    }

    // tg: code/modules/mining/equipment/kheiral_cuffs.dm:112
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (!ishuman(user))
        {
            user.visible_message(span_suicide(I18n.T("[] locks [] around their neck, wrinkles forming across their face. It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
            return OXYLOSS;
        }

        var victim = (mob_living_carbon_human)user;
        victim.visible_message(span_suicide(I18n.T("[] locks [] around their neck, wrinkles forming across their face. It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        for (float mult = 1; mult <= 5; mult++) // Rapidly age
        {
            if (!Dm.truthy(await do_after(victim, 0.5f * SECONDS))) // just to space out the aging, either way you still dust.
                break;
            float before_age = Dm.num(victim.age);
            victim.age = Dm.round(Dm.num(victim.age) * 1.5f, 1);
            to_chat(victim, span_danger(I18n.T("You age [] years!", Dm.num(victim.age) - before_age)));
        }

        to_chat(victim, span_danger(I18n.T("At the ripe age of [], your cells fail their cycle of mitosis, allowing the sands of time to wash over you.", victim.age)));
        await victim.dust(TRUE, TRUE, TRUE);
        return MANUAL_SUICIDE;
    }
}
