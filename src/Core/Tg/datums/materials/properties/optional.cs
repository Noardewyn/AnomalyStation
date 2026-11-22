// Translated from tgstation (snapshot 1d99bd7f): code/datums/materials/properties/optional.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file. The var blocks are generated (D111). The radioactive element
// (radioactivity, uranium structures) and do_teleport (teleporting, bluespace) are not translated: their calls stay
// Dm.NotTranslated.

// Optional properties, not guaranteed to be present on all materials

/// If a material has this property, it is flammable and has reduced fire protection.
// tg: code/datums/materials/properties/optional.dm:7
public partial class datum_material_property_flammability : datum_material_property
{
    /// Minimum flammability value required to make an object made out of this material flammable
    // tg: code/datums/materials/properties/optional.dm:4  #define MINIMUM_FLAMMABILITY 4 (a file-local define, rules 11.4)
    private const float MINIMUM_FLAMMABILITY = 4;

    // tg: code/datums/materials/properties/optional.dm:12
    public override object get_descriptor(object value = null)
    {
        object sw__1 = value;
        if (Dm.eq(sw__1, 0))
            return I18n.T("fireproof");
        else if (Dm.in_to(sw__1, 0, 1))
            return I18n.T("nonflammable");
        else if (Dm.in_to(sw__1, 1, 2))
            return I18n.T("mostly nonflammable");
        else if (Dm.in_to(sw__1, 2, 3))
            return I18n.T("slightly fire-resistant");
        else if (Dm.in_to(sw__1, 3, 4))
            return I18n.T("flammable");
        else if (Dm.in_to(sw__1, 4, 6))
            return I18n.T("highly flammable");
        else if (Dm.in_to(sw__1, 6, 8))
            return I18n.T("extremely flammable");
        else if (Dm.in_to(sw__1, 8, INFINITY))
            return I18n.T("insanely flammable");
        return null;
    }

    // tg: code/datums/materials/properties/optional.dm:31
    public override void attach_to(datum_material material = null)
    {
        base.attach_to(material);
        RegisterSignal(material, COMSIG_MATERIAL_APPLIED, nameof(on_applied));
        RegisterSignal(material, COMSIG_MATERIAL_REMOVED, nameof(on_removed));
    }

    // tg: code/datums/materials/properties/optional.dm:36
    [SignalHandler]
    public virtual void on_applied(datum_material source = null, atom new_atom = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        if (isobj(new_atom) && Dm.truthy(Dm.bitand(new_atom.material_flags, MATERIAL_AFFECT_STATISTICS)) && Dm.num(source.get_property(id)) >= MINIMUM_FLAMMABILITY)
            Dm.combine(ref new_atom.resistance_flags, FLAMMABLE);
    }

    // tg: code/datums/materials/properties/optional.dm:42
    [SignalHandler]
    public virtual void on_removed(datum_material source = null, atom old_atom = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        if (isobj(old_atom) && Dm.truthy(Dm.bitand(old_atom.material_flags, MATERIAL_AFFECT_STATISTICS)) && Dm.num(source.get_property(id)) >= MINIMUM_FLAMMABILITY && !Dm.truthy(Dm.bitand(Dm.initial(old_atom, (atom a) => a.resistance_flags), FLAMMABLE)))
            Dm.mask(ref old_atom.resistance_flags, Dm.bitnot(FLAMMABLE));
    }
}

// tg: code/datums/materials/properties/optional.dm:53
public partial class datum_material_property_radioactivity : datum_material_property
{
    // An average value of 4 would equate to URANIUM_IRRADIATION_CHANCE radioactivity
    // tg: code/datums/materials/properties/optional.dm:51  #define URANIUM_RADIOACTIVITY 4 (a file-local define, rules 11.4)
    private const float URANIUM_RADIOACTIVITY = 4;

    // tg: code/datums/materials/properties/optional.dm:57
    public override object get_descriptor(object value = null)
    {
        object sw__1 = value;
        if (Dm.eq(sw__1, 0))
            return null;
        else if (Dm.in_to(sw__1, 0, 2))
            return I18n.T("slightly radioactive");
        else if (Dm.in_to(sw__1, 2, 4))
            return I18n.T("radioactive");
        else if (Dm.in_to(sw__1, 4, 6))
            return I18n.T("highly radioactive");
        else if (Dm.in_to(sw__1, 6, 8))
            return I18n.T("extremely radioactive");
        else if (Dm.in_to(sw__1, 8, INFINITY))
            return I18n.T("insanely radioactive");
        return null;
    }

    // tg: code/datums/materials/properties/optional.dm:72
    public override void attach_to(datum_material material = null)
    {
        base.attach_to(material);
        RegisterSignal(material, COMSIG_MATERIAL_APPLIED, nameof(on_applied));
        RegisterSignal(material, COMSIG_MATERIAL_REMOVED, nameof(on_removed));
    }

    // tg: code/datums/materials/properties/optional.dm:77
    [SignalHandler]
    public virtual void on_applied(datum_material source = null, atom new_atom = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        // Uranium structures should irradiate, but not items, because item irradiation is a lot more annoying.
        if (!isitem(new_atom))
            Waiting.NoSleep(new_atom.AddElement(typeof(datum_element_radioactive), Dm.named("chance", Dm.num(source.get_property(id)) / URANIUM_RADIOACTIVITY * URANIUM_IRRADIATION_CHANCE * Dm.num(multiplier))));
    }

    // tg: code/datums/materials/properties/optional.dm:83
    [SignalHandler]
    public virtual void on_removed(datum_material source = null, atom old_atom = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        if (!isitem(old_atom))
            old_atom.RemoveElement(typeof(datum_element_radioactive), Dm.named("chance", Dm.num(source.get_property(id)) / URANIUM_RADIOACTIVITY * URANIUM_IRRADIATION_CHANCE * Dm.num(multiplier)));
    }
}

/// Applies firestacks to affected mobs
// tg: code/datums/materials/properties/optional.dm:92
public partial class datum_material_property_firestacker : datum_material_property
{
    // tg: code/datums/materials/properties/optional.dm:96
    public override object get_descriptor(object value = null)
    {
        return I18n.T("igniting");
    }

    // tg: code/datums/materials/properties/optional.dm:99
    public override object get_tooltip(object value = null)
    {
        return I18n.T("Applies [] firestacks to affected mobs", value);
    }

    // tg: code/datums/materials/properties/optional.dm:102
    public override void attach_to(datum_material material = null)
    {
        base.attach_to(material);
        Dm.combine(ref material.track_flags, MATERIAL_TRACK_CONTACT | MATERIAL_TRACK_IMPACT);
        attach_to__interaction_signals ??= Dm.list(
            COMSIG_MATERIAL_EFFECT_TOUCH,
            COMSIG_MATERIAL_EFFECT_STEP,
            COMSIG_MATERIAL_EFFECT_HIT,
            COMSIG_MATERIAL_EFFECT_THROW_IMPACT
        );
        RegisterSignals(material, attach_to__interaction_signals, nameof(on_contact));
    }

    // tg: code/datums/materials/properties/optional.dm:105  var/static/list/interaction_signals (a proc-level static, rules 10.6)
    private static ref DmList attach_to__interaction_signals => ref TgState.Current.datum_material_property_firestacker__attach_to__interaction_signals;

    // tg: code/datums/materials/properties/optional.dm:113
    [SignalHandler]
    public virtual void on_contact(datum_material source = null, atom @object = null, mob_living target = null, mob_living user = null, object def_zone = null, object skin_contact = null)
    {
        // Floors don't trigger if you're wearing shoes because it'd be too cancer
        if (isfloorturf(@object) && !Dm.truthy(skin_contact) && !Dm.truthy(source.get_property(MATERIAL_PENETRATING)))
            return;

        if (isliving(target))
            Waiting.NoSleep(target.adjust_fire_stacks(source.get_property(id)));
    }
}

/// Deals additional burn damage to mobs with the UNHOLY_BANEABLE trait (just vampires right now), property times material volume value determines damage
// tg: code/datums/materials/properties/optional.dm:124
public partial class datum_material_property_unholy_bane : datum_material_property
{
    // tg: code/datums/materials/properties/optional.dm:128
    public override object get_descriptor(object value = null)
    {
        return I18n.T("unholy creatures' bane");
    }

    // tg: code/datums/materials/properties/optional.dm:131
    public override object get_tooltip(object value = null)
    {
        return I18n.T("Deals [] to [] additional burn damage to unholy creatures such as vampires (based on material volume) on contact", Dm.num(value) * 0.5f, Dm.num(value) * 2);
    }

    // tg: code/datums/materials/properties/optional.dm:134
    public override void attach_to(datum_material material = null)
    {
        base.attach_to(material);
        Dm.combine(ref material.track_flags, MATERIAL_TRACK_CONTACT | MATERIAL_TRACK_IMPACT);
        attach_to__interaction_signals ??= Dm.list(
            COMSIG_MATERIAL_EFFECT_TOUCH,
            COMSIG_MATERIAL_EFFECT_STEP,
            COMSIG_MATERIAL_EFFECT_HIT,
            COMSIG_MATERIAL_EFFECT_THROW_IMPACT
        );
        RegisterSignals(material, attach_to__interaction_signals, nameof(on_contact));
    }

    // tg: code/datums/materials/properties/optional.dm:137  var/static/list/interaction_signals (a proc-level static, rules 10.6)
    private static ref DmList attach_to__interaction_signals => ref TgState.Current.datum_material_property_unholy_bane__attach_to__interaction_signals;

    // tg: code/datums/materials/properties/optional.dm:145
    [SignalHandler]
    public virtual void on_contact(datum_material source = null, atom @object = null, mob_living target = null, mob_living user = null, object def_zone = null, object skin_contact = null)
    {
        if (!HAS_TRAIT(target, TRAIT_UNHOLY_BANEABLE))
            return;

        float burn_damage = Dm.num(source.get_property(id)) * clamp((Dm.num(@object.custom_materials[source]) / (2 * SHEET_MATERIAL_AMOUNT)), 0.5f, 2);
        float armor_block = 0;
        if (!Dm.truthy(skin_contact) && !Dm.truthy(source.get_property(MATERIAL_PENETRATING)))
        {
            object or__1 = (astype(@object, typeof(obj_item)) as obj_item)?.armour_penetration;
            armor_block = Dm.num(target.run_armor_check(def_zone, WOUND, armour_penetration: Dm.truthy(or__1) ? or__1 : 0, silent: TRUE, weak_against_armour: TRUE));
        }

        if (armor_block > 50)
            return;

        to_chat(target, span_userdanger(I18n.T("Contact with [] sears your undead flesh!", @object)));
        playsound(target, SFX_SIZZLE, 33, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        Waiting.NoSleep(target.apply_damage(burn_damage, BURN, def_zone, armor_block, wound_bonus: 10, attacking_item: @object, attack_direction: get_dir(user, target), wound_clothing: FALSE));
    }
}

/// Teleports targets who come into active contact with the material around, property value determines teleport radius and damage taken per teleport
// tg: code/datums/materials/properties/optional.dm:164
public partial class datum_material_property_teleporting : datum_material_property
{
    // tg: code/datums/materials/properties/optional.dm:168
    public override object get_descriptor(object value = null)
    {
        return I18n.T("dimensionally unstable");
    }

    // tg: code/datums/materials/properties/optional.dm:171
    public override object get_tooltip(object value = null)
    {
        return I18n.T("Randomly teleports whoever comes into contact with it in a [] tile radius", value);
    }

    // tg: code/datums/materials/properties/optional.dm:174
    public override void attach_to(datum_material material = null)
    {
        base.attach_to(material);
        Dm.combine(ref material.track_flags, MATERIAL_TRACK_CONTACT | MATERIAL_TRACK_IMPACT);
        attach_to__interaction_signals ??= Dm.list(
            COMSIG_MATERIAL_EFFECT_TOUCH,
            COMSIG_MATERIAL_EFFECT_STEP,
            COMSIG_MATERIAL_EFFECT_HIT,
            COMSIG_MATERIAL_EFFECT_THROW_IMPACT
        );
        RegisterSignals(material, attach_to__interaction_signals, nameof(on_contact));
    }

    // tg: code/datums/materials/properties/optional.dm:177  var/static/list/interaction_signals (a proc-level static, rules 10.6)
    private static ref DmList attach_to__interaction_signals => ref TgState.Current.datum_material_property_teleporting__attach_to__interaction_signals;

    // tg: code/datums/materials/properties/optional.dm:185
    [SignalHandler]
    public virtual void on_contact(datum_material source = null, atom @object = null, atom target = null, mob_living user = null, object def_zone = null, object skin_contact = null)
    {
        if (!ismovable(target))
            return;

        atom_movable as_movable = target as atom_movable;
        // Don't teleport airlocks around please
        if (Dm.truthy(as_movable.anchored) || as_movable.move_resist >= INFINITY)
            return;

        // Floors don't trigger if you're wearing shoes because it'd be too cancer
        if (isfloorturf(@object) && !Dm.truthy(skin_contact) && !Dm.truthy(source.get_property(MATERIAL_PENETRATING)))
            return;

        // var/value = source.get_property(id); do_teleport(target, get_turf(target), value, channel = TELEPORT_CHANNEL_BLUESPACE)
        // and the stack's use(1) or the object's take_damage (:202-207): do_teleport is not translated.
        Dm.NotTranslated("code/datums/materials/properties/optional.dm:202", "on_contact: do_teleport of whoever touches a bluespace material (teleportation, M10+)");
    }
}

/// Makes all contact count as skin contact
// tg: code/datums/materials/properties/optional.dm:210
public partial class datum_material_property_penetrating : datum_material_property
{
    // tg: code/datums/materials/properties/optional.dm:213
    public override object get_descriptor(object value = null)
    {
        return I18n.T("dimensionally penetrating");
    }

    // tg: code/datums/materials/properties/optional.dm:216
    public override object get_tooltip(object value = null)
    {
        return I18n.T("Ignores all means of skin protection when triggering other material effects");
    }
}

public sealed partial class TgState
{
    // tg: code/datums/materials/properties/optional.dm:105  var/static/list/interaction_signals = list(...)
    internal DmList datum_material_property_firestacker__attach_to__interaction_signals;
    // tg: code/datums/materials/properties/optional.dm:137  var/static/list/interaction_signals = list(...)
    internal DmList datum_material_property_unholy_bane__attach_to__interaction_signals;
    // tg: code/datums/materials/properties/optional.dm:177  var/static/list/interaction_signals = list(...)
    internal DmList datum_material_property_teleporting__attach_to__interaction_signals;
}
