// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/ballistic/bows/bow_arrows.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the arrows (dipped in a chemical through the envenomable casing element, reused unless they are
// plastic or blazing), the poison arrow's cyanide (an untranslated toxin metabolizes as the base toxin, D188), the holy and ashen arrows'
// banes, the blazing star arrow that sets a man alight and blows up on a burning one. The sticky and plastic arrows, the projectiles'
// embedding datums and the base projectile only set vars. The var blocks are generated (D111).

///base arrow
// tg: code/modules/projectiles/guns/ballistic/bows/bow_arrows.dm:2
public partial class obj_item_ammo_casing_arrow : obj_item_ammo_casing
{
    // tg: code/modules/projectiles/guns/ballistic/bows/bow_arrows.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_envenomable_casing)));
        Waiting.NoSleep(AddElement(typeof(datum_element_caseless), reusable));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/bows/bow_arrows.dm:23
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]", base_icon_state);
        return dot;
    }
}

/// poison arrow projctile
// tg: code/modules/projectiles/guns/ballistic/bows/bow_arrows.dm:88
public partial class obj_projectile_bullet_arrow_poison : obj_projectile_bullet_arrow
{
    // tg: code/modules/projectiles/guns/ballistic/bows/bow_arrows.dm:95
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!ishuman(target))
            return dot;

        if (target.reagents != null)
            await ((datum_reagents)target.reagents).add_reagent(typeof(datum_reagent_toxin_cyanide), 8);
        if (target.reagents != null)
            await ((datum_reagents)target.reagents).add_reagent(typeof(datum_reagent_toxin_staminatoxin), 1);
        return dot;
    }
}

/// holy arrow projectile
// tg: code/modules/projectiles/guns/ballistic/bows/bow_arrows.dm:113
public partial class obj_projectile_bullet_arrow_holy : obj_projectile_bullet_arrow
{
    // tg: code/modules/projectiles/guns/ballistic/bows/bow_arrows.dm:118
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // 50 damage to revenants
        Waiting.NoSleep(AddComponent(typeof(datum_component_bane), Dm.named("affected_biotypes", MOB_SPIRIT), Dm.named("added_damage", 30)));
        return dot;
    }
}

// tg: code/modules/projectiles/guns/ballistic/bows/bow_arrows.dm:156
public partial class obj_projectile_bullet_arrow_blazing : obj_projectile_bullet_arrow
{
    // tg: code/modules/projectiles/guns/ballistic/bows/bow_arrows.dm:163
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!ishuman(target))
            return dot;
        mob_living_carbon_human human_target = (mob_living_carbon_human)target;
        if (!Dm.truthy(human_target.on_fire))
        {
            to_chat(human_target, span_danger(I18n.T("[] explodes into flames which quickly envelop you!", this)));
            await human_target.adjust_fire_stacks(2);
            human_target.ignite_mob();
            return dot;
        }
        to_chat(human_target, span_danger(I18n.T("[] reacts with the flames enveloping you! Oh shit!", this)));
        await explosion(this, light_impact_range: 1, flame_range: 2); //ow
        return dot;
    }
}

/// ashen arrow projectile
// tg: code/modules/projectiles/guns/ballistic/bows/bow_arrows.dm:187
public partial class obj_projectile_bullet_arrow_ashen : obj_projectile_bullet_arrow
{
    // tg: code/modules/projectiles/guns/ballistic/bows/bow_arrows.dm:191
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_bane), Dm.named("affected_biotypes", MOB_MINING), Dm.named("added_damage", 40)));
        return dot;
    }
}
