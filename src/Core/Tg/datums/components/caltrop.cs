// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/caltrop.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file (the glass shard's step damage, glass.dm:348). The var block is
// generated (D111) but for the static crossed_connections, which lives in TgState (rules 10.6).

/**
 * Caltrop element; for hurting people when they walk over this.
 *
 * Used for broken glass, cactuses and four sided dice.
 */
// tg: code/datums/components/caltrop.dm:6
public partial class datum_component_caltrop : datum_component
{
    ///given to connect_loc to listen for something moving over target
    // tg: code/datums/components/caltrop.dm:26  var/static/list/crossed_connections (a type-level static, rules 10.6)
    private static ref DmList crossed_connections => ref TgState.Current.datum_component_caltrop__crossed_connections;

    // tg: code/datums/components/caltrop.dm:33
    [DmParams("min_damage", "max_damage", "probability", "paralyze_duration", "flags", "soundfile")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        float? min_damage = Dm.arg<float?>(__extra, 0);
        float? max_damage = Dm.arg<float?>(__extra, 1);
        float? probability = Dm.arg<float?>(__extra, 2);
        float? paralyze_duration = Dm.arg<float?>(__extra, 3);
        float? flags = Dm.arg<float?>(__extra, 4);
        object soundfile = Dm.arg(__extra, 5);
        min_damage ??= 0;
        max_damage ??= 0;
        probability ??= 100;
        paralyze_duration ??= 2 * SECONDS;
        flags ??= NONE;
        object dot = await base.Initialize(Dm.forward(__extra, min_damage, max_damage, probability, paralyze_duration, flags, soundfile));
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        this.min_damage = min_damage;
        this.max_damage = max(Dm.num(min_damage), Dm.num(max_damage));
        this.probability = probability;
        this.paralyze_duration = paralyze_duration;
        this.flags = flags;
        this.soundfile = soundfile;

        if (ismovable(parent))
            await AddComponent(typeof(datum_component_connect_loc_behalf), parent, crossed_connections);
        else
            RegisterSignal(get_turf(parent), COMSIG_ATOM_ENTERED, nameof(on_entered));
        return dot;
    }

    // Inherit the new values passed to the component
    // tg: code/datums/components/caltrop.dm:51
    [DmParams("new_comp", "original", "min_damage", "max_damage", "probability", "flags", "soundfile")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        object original = i_am_original;
        object min_damage = Dm.arg(__extra, 0);
        object max_damage = Dm.arg(__extra, 1);
        object probability = Dm.arg(__extra, 2);
        object flags = Dm.arg(__extra, 3);
        object soundfile = Dm.arg(__extra, 4);
        if (!Dm.truthy(original))
            return;
        if (Dm.truthy(min_damage))
            this.min_damage = min_damage;
        if (Dm.truthy(max_damage))
            this.max_damage = max_damage;
        if (Dm.truthy(probability))
            this.probability = probability;
        if (Dm.truthy(flags))
            this.flags = flags;
        if (Dm.truthy(soundfile))
            this.soundfile = soundfile;
    }

    // tg: code/datums/components/caltrop.dm:65
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (!prob(Dm.num(probability)))
            return;

        if (!ishuman(arrived))
            return;

        mob_living_carbon_human digitigrade_fan = arrived as mob_living_carbon_human;
        if (HAS_TRAIT(digitigrade_fan, TRAIT_PIERCEIMMUNE))
            return;

        if (Dm.truthy(Dm.bitand(flags, CALTROP_IGNORE_WALKERS)) && digitigrade_fan.move_intent == MOVE_INTENT_WALK)
            return;

        if (Dm.truthy(Dm.bitand(digitigrade_fan.movement_type, MOVETYPES_NOT_TOUCHING_GROUND))) //check if they are able to pass over us
            //gravity checking only our parent would prevent us from triggering they're using magboots / other gravity assisting items that would cause them to still touch us.
            return;

        if (Dm.truthy(digitigrade_fan.buckled)) //if they're buckled to something, that something should be checked instead.
            return;

        if (digitigrade_fan.body_position == LYING_DOWN && !Dm.truthy(Dm.bitand(flags, CALTROP_NOCRAWL))) //if we're not standing we cant step on the caltrop
            return;

        string picked_def_zone = (string)pick(BODY_ZONE_L_LEG, BODY_ZONE_R_LEG);
        obj_item_bodypart leg = digitigrade_fan.get_bodypart(picked_def_zone) as obj_item_bodypart;
        if (!istype(leg, typeof(obj_item_bodypart)))
            return;

        if (!Dm.truthy(IS_ORGANIC_LIMB(leg)))
            return;

        if (!Dm.truthy(Dm.bitand(flags, CALTROP_BYPASS_SHOES)))
        {
            if (Dm.truthy(Dm.bitand(Dm.bitor(Dm.bitor(Dm.num((digitigrade_fan.wear_suit as obj_item)?.body_parts_covered), Dm.num((digitigrade_fan.w_uniform as obj_item)?.body_parts_covered)), Dm.num((digitigrade_fan.shoes as obj_item)?.body_parts_covered)), FEET)))
                return;
        }

        float damage = rand(Dm.num(min_damage), Dm.num(max_damage));
        if (HAS_TRAIT(digitigrade_fan, TRAIT_LIGHT_STEP))
            damage *= 0.75f;

        if (Dm.truthy(Dm.bitand(flags, CALTROP_ANTS)) && HAS_TRAIT(digitigrade_fan, TRAIT_SPACE_ANT_IMMUNITY))
            damage = 0;

        if (!Dm.truthy(Dm.bitand(flags, CALTROP_SILENT)) && !Dm.truthy(digitigrade_fan.has_status_effect(typeof(datum_status_effect_caltropped))))
        {
            Waiting.NoSleep(digitigrade_fan.apply_status_effect(typeof(datum_status_effect_caltropped)));
            digitigrade_fan.visible_message(
                span_danger(I18n.T("[] steps on [].", digitigrade_fan, parent)),
                span_userdanger(I18n.T("You step on []!", parent))
            );
        }

        Waiting.NoSleep(digitigrade_fan.apply_damage(damage, BRUTE, picked_def_zone, wound_bonus: CANT_WOUND, attacking_item: parent));

        if (!Dm.truthy(Dm.bitand(flags, CALTROP_NOSTUN))) // Won't set off the paralysis.
        {
            if (!HAS_TRAIT(digitigrade_fan, TRAIT_LIGHT_STEP))
                Waiting.NoSleep(digitigrade_fan.Paralyze(paralyze_duration));
            else
                Waiting.NoSleep(digitigrade_fan.Knockdown(paralyze_duration));
        }
        if (!Dm.truthy(soundfile))
            return;
        playsound(digitigrade_fan, soundfile, 15, TRUE, -3);
    }

    // tg: code/datums/components/caltrop.dm:128
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        if (ismovable(parent))
            qdel(GetComponent(typeof(datum_component_connect_loc_behalf)));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/caltrop.dm:26  var/static/list/crossed_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList datum_component_caltrop__crossed_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(datum_component_caltrop.on_entered)));
}
