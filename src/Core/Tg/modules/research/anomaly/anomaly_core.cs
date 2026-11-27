// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/anomaly/anomaly_core.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the suicide (manual_suicide: a docs/ai/specs/live-partial.txt row, the suicide verb): the anomaly cores, the
// signalers a neutralized anomaly leaves. A core neutralizes the anomalies on its tile when its own code reaches it, shows its
// frequency and code to a gas analyzer, and each kind does its own thing when it is signalled (pulsed in an assembly or by the
// items that hold one): the pyroclastic core lights the tiles around it, the gravitational one pulls what is loose within two
// tiles, the flux one zaps, the bluespace one blinks what holds it four tiles away, the vortex one pulls and breaks what is around
// it, the bioscrambler one scrambles the bodies by it, the hallucination one sends a hallucination pulse, the dimensional one
// turns the tiles around it into another dimension, the ectoplasm one haunts the objects around it and the weather one calls a
// thunderbolt down on a tile near it. The var blocks are generated (D111).

// Embedded signaller used in anomalies.
// tg: code/modules/research/anomaly/anomaly_core.dm:2
public partial class obj_item_assembly_signaler_anomaly : obj_item_assembly_signaler
{
    // tg: code/modules/research/anomaly/anomaly_core.dm:16  (receive_signal cannot sleep: the neutralizations run on their own, as
    // the signaler's own receive_signal does)
    public override object receive_signal(datum_signal signal = null)
    {
        if (signal == null)
            return FALSE;
        if (!Dm.eq(signal.data["code"], code))
            return FALSE;
        Waiting.Detach(receive_signal__body());
        return TRUE;
    }

    private async DmTask receive_signal__body()
    {
        if (suicider != null)
            await manual_suicide(suicider);
        foreach (obj_effect_anomaly A in Dm.each<obj_effect_anomaly>(get_turf(this)))
            await A.anomalyNeutralize();
    }

    // tg: code/modules/research/anomaly/anomaly_core.dm:33
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/research/anomaly/anomaly_core.dm:36
    public override object analyzer_act(mob_living user = null, obj_item tool = null)
    {
        to_chat(user, span_notice(I18n.T("Analyzing... []'s stabilized field is fluctuating along frequency [], code [].", this, format_frequency(frequency), code)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/anomaly/anomaly_core.dm:40
    [SignalHandler]
    public override object on_mail_unwrap(atom source = null, mob user = null, obj_item_mail_traitor letter = null)
    {
        return NONE;
    }
}

//Anomaly cores
// tg: code/modules/research/anomaly/anomaly_core.dm:44
public partial class obj_item_assembly_signaler_anomaly_pyro : obj_item_assembly_signaler_anomaly
{
    // tg: code/modules/research/anomaly/anomaly_core.dm:51
    public override async DmTask signal()
    {
        turf our_turf = get_turf(this);
        if (our_turf == null)
            return;
        playsound(our_turf, "sound/effects/magic/fireball.ogg", 100, TRUE);
        foreach (turf turf in Dm.each<turf>(RANGE_TURFS(1, our_turf)))
            Dm.@new<obj_effect_hotspot>(turf);
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/research/anomaly/anomaly_core.dm:59
public partial class obj_item_assembly_signaler_anomaly_grav : obj_item_assembly_signaler_anomaly
{
    // tg: code/modules/research/anomaly/anomaly_core.dm:66
    public override async DmTask signal()
    {
        foreach (obj @object in Dm.each<obj>(orange(2, get_turf(this))))
        {
            if (!Dm.truthy(@object.anchored))
                await step_towards(@object, this);
        }
        foreach (mob_living living in Dm.each<mob_living>(orange(2, get_turf(this))))
        {
            if (!Dm.truthy(living.mob_negates_gravity()))
                await step_towards(living, this);
        }
    }
}

// tg: code/modules/research/anomaly/anomaly_core.dm:74
public partial class obj_item_assembly_signaler_anomaly_flux : obj_item_assembly_signaler_anomaly
{
    // tg: code/modules/research/anomaly/anomaly_core.dm:81
    public override async DmTask signal()
    {
        await tesla_zap(get_turf(this), 0, 10 * KILO * JOULES, 5 * KILO * JOULES, Dm.bitor(Dm.bitor(ZAP_MOB_DAMAGE, ZAP_OBJ_DAMAGE), ZAP_GENERATES_POWER));
    }
}

// tg: code/modules/research/anomaly/anomaly_core.dm:84
public partial class obj_item_assembly_signaler_anomaly_bluespace : obj_item_assembly_signaler_anomaly
{
    // tg: code/modules/research/anomaly/anomaly_core.dm:92
    public override async DmTask signal()
    {
        atom_movable to_teleport = get_loose_container(this, container_flags: LOOSE_CONTAINER_INCLUDE_SEALED_MODSUIT);
        if (to_teleport == null)
            return;
        turf teleportable_turf = get_turf(to_teleport);
        playsound(teleportable_turf, "sound/effects/magic/blink.ogg", 100, TRUE);
        await do_teleport(to_teleport, teleportable_turf, 4, channel: TELEPORT_CHANNEL_BLUESPACE);
    }
}

// tg: code/modules/research/anomaly/anomaly_core.dm:100
public partial class obj_item_assembly_signaler_anomaly_vortex : obj_item_assembly_signaler_anomaly
{
    // Causes a small vortex pulse
    // tg: code/modules/research/anomaly/anomaly_core.dm:112
    public override async DmTask signal()
    {
        turf our_turf = get_turf(this);
        if (our_turf == null)
            return;
        playsound(our_turf, "sound/effects/bamf.ogg", 100, TRUE);
        foreach (turf turf in Dm.each<turf>(RANGE_TURFS(1, our_turf)))
        {
            float explosion_power = rand(EXPLODE_NONE, max_explosion_force);
            if (prob(turf_contents_pull_chance))
            {
                foreach (obj @object in Dm.each<obj>(turf.contents))
                {
                    if (Dm.truthy(@object.anchored))
                    {
                        float sw__1 = explosion_power;
                        if (sw__1 == EXPLODE_DEVASTATE)
                            Dm.append(ref SSexplosions.high_mov_atom, @object);
                        else if (sw__1 == EXPLODE_HEAVY)
                            Dm.append(ref SSexplosions.med_mov_atom, @object);
                        else if (sw__1 == EXPLODE_LIGHT)
                            Dm.append(ref SSexplosions.low_mov_atom, @object);
                    }
                    else
                        await step_towards(@object, this);
                }
                foreach (mob_living living in Dm.each<mob_living>(turf.contents))
                    await step_towards(living, this);
            }
            if (prob(turf_destroy_chance))
            {
                float sw__2 = explosion_power;
                if (sw__2 == EXPLODE_DEVASTATE)
                    Dm.append(ref SSexplosions.highturf, turf);
                else if (sw__2 == EXPLODE_HEAVY)
                    Dm.append(ref SSexplosions.medturf, turf);
                else if (sw__2 == EXPLODE_LIGHT)
                    Dm.append(ref SSexplosions.lowturf, turf);
            }
        }
        Dm.@new<obj_effect_temp_visual_circle_wave_vortex>(our_turf);
    }
}

// tg: code/modules/research/anomaly/anomaly_core.dm:143
public partial class obj_item_assembly_signaler_anomaly_bioscrambler : obj_item_assembly_signaler_anomaly
{
    // tg: code/modules/research/anomaly/anomaly_core.dm:151
    public override async DmTask signal()
    {
        Dm.@new<obj_effect_temp_visual_circle_wave_bioscrambler>(get_turf(this));
        foreach (mob_living_carbon nearby in Dm.each<mob_living_carbon>(hearers(1, get_turf(this))))
            await nearby.bioscramble(name);
    }
}

// tg: code/modules/research/anomaly/anomaly_core.dm:156
public partial class obj_item_assembly_signaler_anomaly_hallucination : obj_item_assembly_signaler_anomaly
{
    // tg: code/modules/research/anomaly/anomaly_core.dm:164
    public override async DmTask signal()
    {
        await visible_hallucination_pulse(get_turf(this), 2, 20 * SECONDS, 1 * MINUTES);
    }
}

// tg: code/modules/research/anomaly/anomaly_core.dm:167
public partial class obj_item_assembly_signaler_anomaly_dimensional : obj_item_assembly_signaler_anomaly
{
    // tg: code/modules/research/anomaly/anomaly_core.dm:175
    public override async DmTask signal()
    {
        turf our_turf = get_turf(this);
        if (our_turf == null)
            return;
        Type new_theme_path = pick(subtypesof(typeof(datum_dimension_theme))) as Type;
        datum_dimension_theme theme = SSmaterials.dimensional_themes[new_theme_path] as datum_dimension_theme;
        foreach (turf turf in Dm.each<turf>(RANGE_TURFS(1, our_turf)))
            await theme.apply_theme(turf, show_effect: TRUE);
    }

    // tg: code/modules/research/anomaly/anomaly_core.dm:184
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), TgState.Current.obj_item_assembly_signaler_anomaly_dimensional__slapcraft_recipe_list));
        return dot;
    }
}

// tg: code/modules/research/anomaly/anomaly_core.dm:189
public partial class obj_item_assembly_signaler_anomaly_ectoplasm : obj_item_assembly_signaler_anomaly
{
    // tg: code/modules/research/anomaly/anomaly_core.dm:197
    public override async DmTask signal()
    {
        haunt_outburst(get_turf(this), 2, 33, 30 * SECONDS);
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/research/anomaly/anomaly_core.dm:200
public partial class obj_item_assembly_signaler_anomaly_weather : obj_item_assembly_signaler_anomaly
{
    // tg: code/modules/research/anomaly/anomaly_core.dm:210
    public override async DmTask signal()
    {
        turf our_turf = get_turf(this);
        if (our_turf == null)
            return;
        DmList possible_targets = Dm.list();
        foreach (turf turf in Dm.each<turf>(RANGE_TURFS(2, our_turf)))
        {
            if (isopenturf(turf))
                Dm.append(ref possible_targets, turf);
        }
        if (!Dm.truthy(length(possible_targets)))
            return;
        turf target = pick(possible_targets) as turf;
        Dm.@new<obj_effect_temp_visual_telegraphing_circle>(target);
        _addtimer(Dm.@new<datum_callback>(this, nameof(strike), target), 1 * SECONDS, file: "code/modules/research/anomaly/anomaly_core.dm", line: 222);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/research/anomaly/anomaly_core.dm:224
    public virtual async DmTask strike(turf target = null)
    {
        playsound(target, "sound/effects/magic/lightningbolt.ogg", 66, TRUE);
        Dm.@new<obj_effect_temp_visual_thunderbolt>(target);

        foreach (mob_living hit_mob in Dm.each<mob_living>(target))
        {
            to_chat(hit_mob, span_userdanger(I18n.T("You've been struck by lightning!")));
            await hit_mob.electrocute_act(20, this, flags: Dm.bitor(SHOCK_TESLA, SHOCK_NOSTUN));
        }

        foreach (obj hit_thing in Dm.each<obj>(target))
            await hit_thing.take_damage(15, BURN, ENERGY, FALSE);
    }
}

// The dimensional core's static slapcrafting recipes (rules 10.6, as the other slapcrafting hosts in slapcrafting.cs).
public sealed partial class TgState
{
    // tg: code/modules/research/anomaly/anomaly_core.dm:187
    internal DmList obj_item_assembly_signaler_anomaly_dimensional__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_dimensional_bombcore));
}
