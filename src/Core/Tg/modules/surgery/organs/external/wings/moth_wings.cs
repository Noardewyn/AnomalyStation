// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/external/wings/moth_wings.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b: the moth wings the DNA infuser grows from a mothroach): the flightless wings that drift in low gravity,
// burn off in a fire and heal back with a full heal, and their overlay with the burnt sprite.

// tg: code/modules/surgery/organs/external/wings/moth_wings.dm:1  #define MOTH_WING_FORCE 1 NEWTONS (the var default, emitted)

///Moth wings! They can flutter in low-grav and burn off in heat
// tg: code/modules/surgery/organs/external/wings/moth_wings.dm:5
public partial class obj_item_organ_wings_moth : obj_item_organ_wings
{
    // tg: code/modules/surgery/organs/external/wings/moth_wings.dm:25
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignal(organ_owner, COMSIG_HUMAN_BURNING, nameof(try_burn_wings));
        RegisterSignal(organ_owner, COMSIG_LIVING_POST_FULLY_HEAL, nameof(heal_wings));
        return dot;
    }

    // tg: code/modules/surgery/organs/external/wings/moth_wings.dm:30
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal(organ_owner as datum, Dm.list(COMSIG_HUMAN_BURNING, COMSIG_LIVING_POST_FULLY_HEAL));
    }

    // tg: code/modules/surgery/organs/external/wings/moth_wings.dm:34
    public override object can_soften_fall()
    {
        return !Dm.truthy(burnt) ? TRUE : FALSE;
    }

    // tg: code/modules/surgery/organs/external/wings/moth_wings.dm:37
    public override object allow_flight()
    {
        if (Dm.truthy(burnt))
            return FALSE;
        return base.allow_flight();
    }

    ///check if our wings can burn off ;_;
    // tg: code/modules/surgery/organs/external/wings/moth_wings.dm:44
    [SignalHandler]
    public virtual void try_burn_wings(mob_living_carbon_human human = null)
    {
        if (!Dm.truthy(burnt) && Dm.num(human.bodytemperature) >= 800 && Dm.num(human.fire_stacks) > 0) //do not go into the extremely hot light. you will not survive
        {
            to_chat(human, span_danger(I18n.T("Your precious wings burn to a crisp!")));
            Waiting.NoSleep(human.add_mood_event("burnt_wings", typeof(datum_mood_event_burnt_wings)));

            burn_wings();
            Waiting.NoSleep(human.update_body_parts());
        }
    }

    ///burn the wings off
    // tg: code/modules/surgery/organs/external/wings/moth_wings.dm:55
    public virtual void burn_wings()
    {
        var wings = bodypart_overlay as datum_bodypart_overlay_mutant_wings_moth;
        wings.burnt = TRUE;
        burnt = TRUE;
    }

    ///heal our wings back up!!
    // tg: code/modules/surgery/organs/external/wings/moth_wings.dm:61
    [SignalHandler]
    public virtual void heal_wings(datum source = null, object heal_flags = null)
    {
        if (!Dm.truthy(burnt))
            return;

        if (Dm.truthy(Dm.bitand(heal_flags, Dm.bitor(HEAL_LIMBS, HEAL_ORGANS))))
        {
            var wings = bodypart_overlay as datum_bodypart_overlay_mutant_wings_moth;
            wings.burnt = FALSE;
            burnt = FALSE;
        }
    }

    // tg: code/modules/surgery/organs/external/wings/moth_wings.dm:72
    public override object feel_for_damage(object self_aware = null)
    {
        if (Dm.truthy(burnt))
            return I18n.T("Your wings are all burnt up!");
        return base.feel_for_damage(self_aware);
    }
}

///Moth wing bodypart overlay, including burn functionality!
// tg: code/modules/surgery/organs/external/wings/moth_wings.dm:78
public partial class datum_bodypart_overlay_mutant_wings_moth : datum_bodypart_overlay_mutant_wings
{
    ///Accessory datum of the burn sprite
    // tg: code/modules/surgery/organs/external/wings/moth_wings.dm:86  var/datum/sprite_accessory/burn_datum: object (rules 3.2, a
    // typed var that holds wider values): the type sets a typepath, New (:90) stores the singleton fetched from it
    public object burn_datum = typeof(datum_sprite_accessory_moth_wings_burnt_off);

    // tg: code/modules/surgery/organs/external/wings/moth_wings.dm:79
    public datum_bodypart_overlay_mutant_wings_moth()
    {
        feature_key = FEATURE_MOTH_WINGS;
        slot_blocker = HIDEMUTWINGS;
    }

    // tg: code/modules/surgery/organs/external/wings/moth_wings.dm:80  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        layers = Dm.list(Dm.kv(EXTERNAL_FRONT, BODY_FRONT_LAYER), Dm.kv(EXTERNAL_BEHIND, BODY_BEHIND_LAYER));
    }

    // tg: code/modules/surgery/organs/external/wings/moth_wings.dm:90
    [DmParams("attached_organ")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        burn_datum = fetch_sprite_datum(burn_datum);
    }

    // tg: code/modules/surgery/organs/external/wings/moth_wings.dm:94
    public override object get_base_icon_state()
    {
        return Dm.truthy(burnt) ? ((datum_sprite_accessory)burn_datum).icon_state : sprite_datum.icon_state;
    }
}
