// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/_weapons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the crossbreeds' weapons (the slimy boneblade, the rainbow knife with its random damage type, the adamantine
// shield, the blood chiller that shoots its wielder's blood). The var blocks are generated (D111).

/*
Slimecrossing Weapons
	Weapons added by the slimecrossing system.
	Collected here for clarity.
*/

//Boneblade - Burning Green
// tg: code/modules/research/xenobiology/crossbreeding/_weapons.dm:9
public partial class obj_item_melee_arm_blade_slime : obj_item_melee_arm_blade
{
    // tg: code/modules/research/xenobiology/crossbreeding/_weapons.dm:14
    [DmParams("L", "user")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (prob(20))
            await user.emote("scream");
        return dot;
    }
}

//Rainbow knife - Burning Rainbow
// tg: code/modules/research/xenobiology/crossbreeding/_weapons.dm:21
public partial class obj_item_knife_rainbowknife : obj_item_knife
{
    // tg: code/modules/research/xenobiology/crossbreeding/_weapons.dm:30
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        await DmTask.CompletedTask;
        if (isliving(target))
            damtype = Dm.str(pick(BRUTE, BURN, TOX, OXY));
        switch (damtype)
        {
            case BRUTE:
                hitsound = "sound/items/weapons/bladeslice.ogg";
                attack_verb_continuous = string_list(Dm.list("slashes", "slices", "cuts")) as DmList;
                attack_verb_simple = string_list(Dm.list("slash", "slice", "cut")) as DmList;
                break;
            case BURN:
                hitsound = "sound/items/weapons/sear.ogg";
                attack_verb_continuous = string_list(Dm.list("burns", "singes", "heats")) as DmList;
                attack_verb_simple = string_list(Dm.list("burn", "singe", "heat")) as DmList;
                break;
            case TOX:
                hitsound = "sound/items/weapons/pierce.ogg";
                attack_verb_continuous = string_list(Dm.list("poisons", "doses", "toxifies")) as DmList;
                attack_verb_simple = string_list(Dm.list("poison", "dose", "toxify")) as DmList;
                break;
            case OXY:
                hitsound = "sound/effects/space_wind.ogg";
                attack_verb_continuous = string_list(Dm.list("suffocates", "winds", "vacuums")) as DmList;
                attack_verb_simple = string_list(Dm.list("suffocate", "wind", "vacuum")) as DmList;
                break;
        }
    }
}

//Adamantine shield - Chilling Adamantine
// tg: code/modules/research/xenobiology/crossbreeding/_weapons.dm:53
public partial class obj_item_shield_adamantineshield : obj_item_shield
{
    // tg: code/modules/research/xenobiology/crossbreeding/_weapons.dm:79
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("require_twohands", TRUE), Dm.named("force_wielded", 15)));
        return dot;
    }
}

//Bloodchiller - Chilling Green
// tg: code/modules/research/xenobiology/crossbreeding/_weapons.dm:85
public partial class obj_item_gun_magic_bloodchill : obj_item_gun_magic
{
    // tg: code/modules/research/xenobiology/crossbreeding/_weapons.dm:103
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT);
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_weapons.dm:107
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        charge_timer += Dm.num(seconds_per_tick);
        if (charge_timer < recharge_rate || charges >= max_charges)
            return FALSE;
        charge_timer = 0;
        mob_living M = loc as mob_living;
        if (M is mob_living && !Dm.truthy(CAN_HAVE_BLOOD(M)) && !IS_UNCONSCIOUS_OR_CRIT(M))
        {
            charges++;
            Waiting.NoSleep(M.apply_damage(5, BRUTE));
        }
        else
        {
            if (M is mob_living && Dm.num(M.get_blood_volume()) >= 20)
            {
                charges++;
                M.adjust_blood_volume(-20);
            }
        }
        if (charges == 1)
            Waiting.NoSleep(recharge_newshot());
        return TRUE;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_weapons.dm:128
public partial class obj_projectile_magic_bloodchill : obj_projectile_magic
{
    // tg: code/modules/research/xenobiology/crossbreeding/_weapons.dm:132
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (isliving(target))
            await ((mob_living)target).apply_status_effect(typeof(datum_status_effect_bloodchill));
        return dot;
    }
}
