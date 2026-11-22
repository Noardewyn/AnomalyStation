// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/special/spit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the ink spit (gaps P3b, D327: the inky tongue's spit; spit.dm:20-end), its splat and blinding, stronger with the
// fish's set bonus, and (gaps X5) the xenomorphs' neurotoxin spit (:1-18). The var blocks are generated (D111).

// tg: code/modules/projectiles/projectile/special/spit.dm:1
public partial class obj_projectile_neurotoxin : obj_projectile
{
    // tg: code/modules/projectiles/projectile/special/spit.dm:10
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        if (isalien(target))
            damage = 0;
        return await base.on_hit(target, blocked, pierce_hit);
    }
}

// tg: code/modules/projectiles/projectile/special/spit.dm:20
public partial class obj_projectile_ink_spit : obj_projectile
{
    // tg: code/modules/projectiles/projectile/special/spit.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (isliving(firer))
        {
            mob_living living = firer as mob_living;
            datum_status_effect_organ_set_bonus_fish bonus = living?.has_status_effect(typeof(datum_status_effect_organ_set_bonus_fish)) as datum_status_effect_organ_set_bonus_fish;
            if (Dm.truthy(bonus?.bonus_active))
            {
                damage = 12;
                armour_penetration = 65;
            }
        }

        Waiting.NoSleep(AddComponent(typeof(datum_component_splat),
            Dm.named("memory_type", typeof(datum_memory_witnessed_inking)),
            Dm.named("smudge_type", typeof(obj_effect_decal_cleanable_food_squid_ink)),
            Dm.named("moodlet_type", typeof(datum_mood_event_inked)),
            Dm.named("splat_color", COLOR_NEARLY_ALL_BLACK),
            Dm.named("hit_callback", Dm.@new<datum_callback>(this, nameof(blind_em)))));
        return dot;
    }

    // tg: code/modules/projectiles/projectile/special/spit.dm:48
    public virtual async DmTask blind_em(mob_living victim = null, object can_splat_on = null)
    {
        if (!Dm.truthy(can_splat_on))
            return;
        bool powered_up = false;
        if (isliving(firer))
        {
            mob_living living = firer as mob_living;
            datum_status_effect_organ_set_bonus_fish bonus = living?.has_status_effect(typeof(datum_status_effect_organ_set_bonus_fish)) as datum_status_effect_organ_set_bonus_fish;
            powered_up = Dm.truthy(bonus?.bonus_active);
        }
        await victim.adjust_temp_blindness_up_to((powered_up ? 6.5f : 4.5f) * SECONDS, 10 * SECONDS);
        await victim.adjust_confusion_up_to((powered_up ? 3 : 1.5f) * SECONDS, 6 * SECONDS);
        if (powered_up)
            await victim.Knockdown(2 * SECONDS); //splat!
    }
}
