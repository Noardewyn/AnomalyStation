// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/resonator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the file whole, for the kinetic accelerator's resonator blast modkit and the miners' resonator. The log is a leaf.

/**********************Resonator**********************/
// tg: code/modules/mining/equipment/resonator.dm:4
public partial class obj_item_resonator : obj_item
{
    // tg: code/modules/mining/equipment/resonator.dm:33
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.eq(mode, RESONATOR_MODE_AUTO))
        {
            to_chat(user, span_info(I18n.T("You set the resonator's fields to detonate only after you hit one with it.")));
            mode = RESONATOR_MODE_MANUAL;
        }
        else
        {
            to_chat(user, span_info(I18n.T("You set the resonator's fields to automatically detonate after 2 seconds.")));
            mode = RESONATOR_MODE_AUTO;
        }
        return null;
    }

    // tg: code/modules/mining/equipment/resonator.dm:41
    public virtual void create_resonance(object target = null, mob user = null)
    {
        turf target_turf = get_turf(target as atom);
        obj_effect_temp_visual_resonance resonance_field = Dm.locate<obj_effect_temp_visual_resonance>(target_turf);
        if (resonance_field != null)
        {
            resonance_field.damage_multiplier = quick_burst_mod;
            resonance_field.burst();
            return;
        }
        if (LAZYLEN(fields) < fieldlimit)
        {
            Dm.@new<obj_effect_temp_visual_resonance>(target_turf, user, this, mode, adding_failure);
            user.changeNext_move(CLICK_CD_MELEE);
        }
    }

    // tg: code/modules/mining/equipment/resonator.dm:52
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.truthy(check_allowed_items(target, not_inside: TRUE)))
            create_resonance(target, user);
        return await base.pre_attack(target, user, modifiers, attack_modifiers);
    }
}

//resonance field, crushes rock, damages mobs
// tg: code/modules/mining/equipment/resonator.dm:59
public partial class obj_effect_temp_visual_resonance : obj_effect_temp_visual
{
    // tg: code/modules/mining/equipment/resonator.dm:80
    [DmParams("mapload", "set_creator", "set_resonator", "mode", "set_failure")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob set_creator = Dm.arg<mob>(__extra, 0);
        obj_item_resonator set_resonator = Dm.arg<obj_item_resonator>(__extra, 1);
        object mode = Dm.arg(__extra, 2);
        object set_failure = Dm.arg(__extra, 3) ?? 50;
        if (Dm.eq(mode, RESONATOR_MODE_AUTO))
            duration = 2 * SECONDS;
        if (Dm.eq(mode, RESONATOR_MODE_MATRIX))
        {
            icon_state = "shield2";
            name = "resonance matrix";
            RegisterSignal(this, COMSIG_ATOM_ENTERED, nameof(burst));
            Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), TgState.Current.obj_effect_temp_visual_resonance__Initialize__loc_connections));
        }
        object dot = base.Initialize(mapload);
        creator = set_creator;
        parent_resonator = set_resonator;
        if (parent_resonator != null)
            Dm.append(ref parent_resonator.fields, this);
        adding_failure = set_failure;
        playsound(this, Dm.resource("sound/items/weapons/resonator_fire.ogg"), 50, TRUE);
        if (Dm.eq(mode, RESONATOR_MODE_AUTO))
        {
            transform = Dm.@new<matrix>().Multiply(0.75f);
            animate(this, Dm.named("transform", Dm.@new<matrix>().Multiply(1.5f)), Dm.named("time", duration));
        }
        deltimer(timerid);
        timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(burst)), duration, TIMER_STOPPABLE, file: "code/modules/mining/equipment/resonator.dm", line: 104);
        return dot;
    }

    // tg: code/modules/mining/equipment/resonator.dm:104
    public override object Destroy(float? force = null)
    {
        if (parent_resonator != null)
        {
            parent_resonator.fields.Remove(this);
            parent_resonator = null;
        }
        creator = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mining/equipment/resonator.dm:111
    public virtual void check_pressure(turf proj_turf = null)
    {
        if (proj_turf == null)
            proj_turf = get_turf(this);
        resonance_damage = Dm.num(Dm.initial(type, (obj_effect_temp_visual_resonance r) => r.resonance_damage));
        string initial_name = Dm.str(Dm.initial(type, (obj_effect_temp_visual_resonance r) => r.name));
        if (Dm.truthy(lavaland_equipment_pressure_check(proj_turf)))
        {
            name = Dm.text("strong []", initial_name);
            resonance_damage *= 3;
        }
        else
            name = initial_name;
        resonance_damage *= damage_multiplier;
    }

    // tg: code/modules/mining/equipment/resonator.dm:122
    public virtual void burst(params object[] __extra)
    {
        if (Dm.truthy(rupturing))
            return;
        rupturing = TRUE;
        turf src_turf = get_turf(this);
        Dm.@new<obj_effect_temp_visual_resonance_crush>(src_turf);
        if (ismineralturf(src_turf))
        {
            turf_closed_mineral mineral_turf = src_turf as turf_closed_mineral;
            Waiting.Detach(mineral_turf.gets_drilled(creator));
            src_turf = locate(src_turf.x, src_turf.y, src_turf.z) as turf; // TURF-IDENTITY: the turf the drill left
        }
        check_pressure(src_turf);
        playsound(src_turf, Dm.resource("sound/items/weapons/resonator_blast.ogg"), 50, TRUE);
        foreach (mob_living attacked_living in Dm.each<mob_living>(src_turf))
        {
            if (creator != null)
            {
                log_combat(creator, attacked_living, "used a resonator field on", "resonator");
                if (Dm.truthy(creator._listen_lookup?[COMSIG_LIVING_RESONATOR_BURST]))
                    creator._SendSignal(COMSIG_LIVING_RESONATOR_BURST, Dm.list(creator, creator, attacked_living));
            }
            to_chat(attacked_living, span_userdanger(I18n.T("[] ruptured with you in it!", this)));
            Waiting.Detach(attacked_living.apply_damage(resonance_damage, BRUTE));
            if (!QDELETED(attacked_living))
            {
                Waiting.Detach(attacked_living.add_movespeed_modifier(typeof(datum_movespeed_modifier_resonance)));
                _addtimer(Dm.@new<datum_callback>(attacked_living, nameof(mob.remove_movespeed_modifier), typeof(datum_movespeed_modifier_resonance)), 10 * SECONDS, TIMER_UNIQUE | TIMER_OVERRIDE, file: "code/modules/mining/equipment/resonator.dm", line: 141);
            }
        }
        foreach (obj_effect_temp_visual_resonance field in Dm.each<obj_effect_temp_visual_resonance>(orange(1, this)))
        {
            if (Dm.truthy(field.rupturing))
                continue;
            field.burst();
        }
        if (!prob(failure_prob) && parent_resonator != null)
        {
            foreach (turf_closed_mineral mineral_spread in Dm.each<turf_closed_mineral>(orange(1, this)))
            {
                if (Dm.locate<obj_effect_temp_visual_resonance>(mineral_spread) != null)
                    continue;
                obj_effect_temp_visual_resonance new_field = Dm.@new<obj_effect_temp_visual_resonance>(mineral_spread, creator, parent_resonator, parent_resonator.mode);
                new_field.failure_prob = failure_prob + Dm.num(adding_failure);
            }
        }
        qdel(this);
    }
}

// tg: code/modules/mining/equipment/resonator.dm:156
public partial class obj_effect_temp_visual_resonance_crush : obj_effect_temp_visual
{
    // tg: code/modules/mining/equipment/resonator.dm:161
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        transform = Dm.@new<matrix>().Multiply(1.5f);
        animate(this, Dm.named("transform", Dm.@new<matrix>().Multiply(0.1f)), Dm.named("alpha", 50), Dm.named("time", 4));
        return dot;
    }
}

// tg: code/modules/mining/equipment/resonator.dm:167
public partial class obj_item_resonator_upgraded : obj_item_resonator
{
    // tg: code/modules/mining/equipment/resonator.dm:175
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.eq(mode, RESONATOR_MODE_AUTO))
        {
            to_chat(user, span_info(I18n.T("You set the resonator's fields to detonate only after you hit one with it.")));
            mode = RESONATOR_MODE_MANUAL;
        }
        else if (Dm.eq(mode, RESONATOR_MODE_MANUAL))
        {
            to_chat(user, span_info(I18n.T("You set the resonator's fields to work as matrix traps.")));
            mode = RESONATOR_MODE_MATRIX;
        }
        else
        {
            to_chat(user, span_info(I18n.T("You set the resonator's fields to automatically detonate after 2 seconds.")));
            mode = RESONATOR_MODE_AUTO;
        }
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/equipment/resonator.dm:89  var/static/list/loc_connections
    internal DmList obj_effect_temp_visual_resonance__Initialize__loc_connections = Dm.list(Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_effect_temp_visual_resonance.burst)));
}
