// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/ammunition/ballistic/rifle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4b, D316: the rebar crossbow's rods, the sharpened and the syndicate jagged ones; gaps GU1, D333: the .310
// Strilka rounds that crumble as they fire, the healium rod's heals carried from casing to bolt and its embedding that heals and puts
// its man to sleep). The other casings only set vars. The var blocks are generated (D111).

// .310 Strilka (Sakhno Rifle)

// tg: code/modules/projectiles/ammunition/ballistic/rifle.dm:3
public partial class obj_item_ammo_casing_strilka310 : obj_item_ammo_casing
{
    // tg: code/modules/projectiles/ammunition/ballistic/rifle.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(AddElement(typeof(datum_element_caseless)));
        return dot;
    }
}

// Rebar Bolts

// tg: code/modules/projectiles/ammunition/ballistic/rifle.dm:87
public partial class obj_item_ammo_casing_rebar : obj_item_ammo_casing
{
    // tg: code/modules/projectiles/ammunition/ballistic/rifle.dm:97
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_caseless), TRUE));
        return dot;
    }

    // tg: code/modules/projectiles/ammunition/ballistic/rifle.dm:101
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]", base_icon_state);
        return dot;
    }
}

// tg: code/modules/projectiles/ammunition/ballistic/rifle.dm:131
public partial class obj_item_ammo_casing_rebar_healium : obj_item_ammo_casing_rebar
{
    // tg: code/modules/projectiles/ammunition/ballistic/rifle.dm:142
    public override async DmTask ready_proj(atom target = null, mob_living user = null, object quiet = null, string zone_override = null, atom fired_from = null)
    {
        if (loaded_projectile != null)
        {
            obj_projectile_bullet_rebar_healium bolt = loaded_projectile as obj_projectile_bullet_rebar_healium;
            bolt.heals_left = heals_left;
        }
        await base.ready_proj(target, user, quiet, zone_override, fired_from);
    }
}

// tg: code/modules/projectiles/ammunition/ballistic/rifle.dm:148
public partial class datum_embedding_rebar_healium : datum_embedding
{
    // tg: code/modules/projectiles/ammunition/ballistic/rifle.dm:163
    public override async DmTask on_successful_embed(mob_living_carbon victim = null, obj_item_bodypart target_limb = null)
    {
        await base.on_successful_embed(victim, target_limb);
        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(victim.get_bodyparts()))
        {
            foreach (obj_item_ammo_casing_rebar_healium other_rebar in Dm.each<obj_item_ammo_casing_rebar_healium>(limb.embedded_objects))
            {
                if (other_rebar == parent)
                    continue;
                datum_embedding_rebar_healium embed_data = other_rebar.get_embed() as datum_embedding_rebar_healium;
                if (embed_data != null && Dm.truthy(embed_data.can_sleep))
                {
                    can_sleep = FALSE;
                    return;
                }
            }
        }
    }

    // tg: code/modules/projectiles/ammunition/ballistic/rifle.dm:174
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process__body(dot, seconds_per_tick, __extra), dot); // the slot's set waitfor = FALSE (datum process, processing.dm:48)
    }

    private async DmTask<object> process__body(DotBox<object> dot, object seconds_per_tick, object[] __extra)
    {
        dot.Value = base.process(seconds_per_tick, __extra);
        obj_item_ammo_casing_rebar_healium casing = parent as obj_item_ammo_casing_rebar_healium;
        casing.heals_left -= Dm.num(seconds_per_tick) * 1 * SECONDS;
        float update_health = FALSE;
        float healing = -healing_per_second * Dm.num(seconds_per_tick);
        update_health += Dm.num(await owner.adjust_brute_loss(healing, updating_health: FALSE, required_bodytype: BODYTYPE_ORGANIC));
        update_health += Dm.num(await owner.adjust_fire_loss(healing, updating_health: FALSE, required_bodytype: BODYTYPE_ORGANIC));
        update_health += Dm.num(await owner.adjust_tox_loss(healing, updating_health: FALSE, required_biotype: MOB_ORGANIC));
        update_health += Dm.num(await owner.adjust_oxy_loss(healing, updating_health: FALSE, required_biotype: MOB_ORGANIC));
        if (Dm.truthy(update_health))
            await owner.updatehealth();
        if (Dm.truthy(can_sleep) && Dm.truthy(Dm.bitand(owner.mob_biotypes, MOB_ORGANIC)))
        {
            await owner.adjust_drowsiness(drowsy_per_second * Dm.num(seconds_per_tick));
            datum_status_effect_drowsiness drowsiness = owner.has_status_effect(typeof(datum_status_effect_drowsiness)) as datum_status_effect_drowsiness;
            if (Dm.num(drowsiness?.duration) >= drowsy_knockout)
                await owner.Sleeping(3 * SECONDS);
        }
        if (casing.heals_left <= 0)
            await fall_out();
        return dot.Value;
    }

    // tg: code/modules/projectiles/ammunition/ballistic/rifle.dm:194
    public override async DmTask remove_embedding(mob_living to_hands = null)
    {
        await base.remove_embedding(to_hands);
        obj_item_ammo_casing_rebar_healium casing = parent as obj_item_ammo_casing_rebar_healium;
        casing.heals_left = Dm.initial(casing, (obj_item_ammo_casing_rebar_healium p) => p.heals_left);
        can_sleep = TRUE;
    }
}
