// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/special/curse.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps RG2, D339): the curse hand that helgrasp (helbital's inverse) and the necropolis curse throw, with its arm
// beam. The var blocks are generated (D111).

// tg: code/modules/projectiles/projectile/special/curse.dm:5
public partial class obj_projectile_curse_hand : obj_projectile
{
    // tg: code/modules/projectiles/projectile/special/curse.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_FREE_HYPERSPACE_MOVEMENT, INNATE_TRAIT);
        handedness = Dm.num(prob(50));
        icon_state = Dm.text("[][]", base_icon_state, handedness);
        return dot;
    }

    // tg: code/modules/projectiles/projectile/special/curse.dm:24
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref arm);
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/projectile/special/curse.dm:28
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]0[]", base_icon_state, handedness);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/projectiles/projectile/special/curse.dm:32
    public override async DmTask fire(object fire_angle = null, atom direct_target = null)
    {
        if (QDELETED(this)) //I'm going to try returning nothing because if it's being deleted, surely we don't want anything to happen?
            return;
        if (starting != null)
            arm = starting.Beam(this, icon_state: Dm.text("curse[]", handedness), beam_type: typeof(obj_effect_ebeam_curse_arm)) as datum_beam;
        await base.fire(fire_angle, direct_target);
    }

    // tg: code/modules/projectiles/projectile/special/curse.dm:39
    public override async DmTask<object> prehit_pierce(atom target = null)
    {
        return (target == original) ? PROJECTILE_PIERCE_NONE : PROJECTILE_PIERCE_PHASE;
    }

    /// The visual effect for the hand disappearing
    // tg: code/modules/projectiles/projectile/special/curse.dm:43
    public virtual void finale()
    {
        if (arm != null)
            QDEL_NULL(ref arm);
        if (Dm.truthy(Dm.bitand(movement_type, PHASING)))
            playsound(this, Dm.resource("sound/effects/curse/curse3.ogg"), 25, TRUE, -1);
        turf T = get_step(this, dir) as turf;
        obj_effect_temp_visual_dir_setting_curse_hand leftover = Dm.@new<obj_effect_temp_visual_dir_setting_curse_hand>(T, dir);
        leftover.icon_state = icon_state;
        foreach (obj_effect_temp_visual_dir_setting_curse_grasp_portal G in Dm.each<obj_effect_temp_visual_dir_setting_curse_grasp_portal>(starting))
            qdel(G);
        if (T == null) //T can be in nullspace when src is set to QDEL
            return;
        Dm.@new<obj_effect_temp_visual_dir_setting_curse_grasp_portal_fading>(starting, dir);
        datum_beam D = starting.Beam(T, icon_state: Dm.text("curse[]", handedness), time: 32, beam_type: typeof(obj_effect_ebeam_curse_arm)) as datum_beam;
        Dm.animate(D.visuals, Dm.named("alpha", 0), Dm.named("time", 32));
    }

    // tg: code/modules/projectiles/projectile/special/curse.dm:59
    public override async DmTask on_range()
    {
        finale();
        await base.on_range();
    }

    // tg: code/modules/projectiles/projectile/special/curse.dm:63
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (Dm.eq(dot, BULLET_ACT_HIT))
            finale();
        return dot;
    }
}
