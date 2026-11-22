// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Initialize and Destroy (M3 P4: icon smoothing of the structures, scope S1); M5 slice 8: used_in_craft; M7 zaps: zap_act; gaps ST5
// (D333): the rest of the file (ui_act's fingerprint, the rust heretic's touch, the staff of animation's mimic, smash_and_injure for
// the window smash element). The var block is generated.

// tg: code/game/objects/structures.dm:2
public partial class obj_structure : obj
{
    // tg: code/game/objects/structures.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
        {
            QUEUE_SMOOTH(this);
            QUEUE_SMOOTH_NEIGHBORS(this);
        }
        return dot;
    }

    // tg: code/game/objects/structures.dm:27
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
            QUEUE_SMOOTH_NEIGHBORS(this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures.dm:32  (gaps ST5)
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        add_fingerprint(usr);
        return await base.ui_act(action, @params, ui, state);
    }

    // tg: code/game/objects/structures.dm:36
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
        {
            if (Dm.truthy(Dm.bitand(resistance_flags, ON_FIRE)))
                Dm.append(ref dot, span_warning(I18n.T("It's on fire!")));
            if (Dm.truthy(broken))
                Dm.append(ref dot, span_notice(I18n.T("It appears to be broken.")));
            object examine_status = this.examine_status(user);
            if (Dm.truthy(examine_status))
                Dm.append(ref dot, examine_status);
        }
        return dot;
    }

    //An overridable proc, mostly for falsewalls.
    // tg: code/game/objects/structures.dm:47
    public virtual object examine_status(mob user = null)
    {
        float healthpercent = Dm.num(Dm.div(atom_integrity, max_integrity)) * 100;
        if (healthpercent >= 50 && healthpercent <= 99)
            return I18n.T("It looks slightly damaged.");
        if (healthpercent >= 25 && healthpercent <= 50)
            return I18n.T("It appears heavily damaged.");
        if (healthpercent >= 0 && healthpercent <= 25)
        {
            if (!Dm.truthy(broken))
                return span_warning(I18n.T("It's falling apart!"));
        }
        return null;
    }

    // tg: code/game/objects/structures.dm:58
    public override object examine_descriptor(mob user = null)
    {
        return I18n.T("structure");
    }

    // tg: code/game/objects/structures.dm:61  (gaps ST5)
    public override async DmTask<object> rust_heretic_act(object rust_strength = null)
    {
        await take_damage(500, BRUTE, "melee", 1);
        return TRUE;
    }

    // tg: code/game/objects/structures.dm:65  (the M7 zaps slice)
    public override async DmTask<object> zap_act(object power = null, object zap_flags = null)
    {
        object dot = null;
        // DEVIATION(DV27): the interaction guard (D132) on a structure whose own tg zap_act is not translated (a blob)
        if (InertSeam.MissingOverride(this, "zap_act"))
            return null;
        if (Dm.truthy(Dm.bitand(zap_flags, ZAP_OBJ_DAMAGE)))
            await take_damage(Dm.mul(power, 2.5e-4f), BURN, "energy");
        power = Dm.sub(power, Dm.mul(power, 5e-4f)); //walls take a lot out of ya
        dot = await base.zap_act(power, zap_flags);
        return dot;
    }

    // tg: code/game/objects/structures.dm:71  (gaps ST5)
    public override async DmTask<object> animate_atom_living(mob_living owner = null)
    {
        await DmTask.CompletedTask;
        return Dm.@new<mob_living_basic_mimic_copy>(drop_location(), this, owner);
    }

    /// For when a mob comes flying through the window, smash it and damage the mob
    // tg: code/game/objects/structures.dm:75  (gaps ST5)
    public virtual async DmTask smash_and_injure(mob_living flying_mob = null, atom oldloc = null, object direction = null)
    {
        flying_mob.balloon_alert_to_viewers(I18n.T("smashed through!"));
        await flying_mob.apply_damage(damage: rand(5, 15), damagetype: BRUTE, wound_bonus: 15, exposed_wound_bonus: 25, sharpness: SHARP_EDGED, attack_direction: get_dir(this, oldloc));
        Dm.@new<obj_effect_decal_cleanable_glass>(get_step(flying_mob, flying_mob.dir));
        await deconstruct(disassembled: FALSE);
    }

    // tg: code/game/objects/structures.dm:81
    public override async DmTask used_in_craft(atom result = null, datum_crafting_recipe current_recipe = null)
    {
        await base.used_in_craft(result, current_recipe);
        // If we consumed in crafting, we should dump contents out before qdeling them.
        if (!Dm.truthy(is_type_in_list(this, current_recipe.parts)))
            await dump_contents();
    }
}
