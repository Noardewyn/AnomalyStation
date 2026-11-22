// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/falling_hazard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// An element that will make a target thing do damage to any mob that it falls on from a z-level above
// tg: code/datums/elements/falling_hazard.dm:2
public partial class datum_element_falling_hazard : datum_element
{
    // tg: code/datums/elements/falling_hazard.dm:17
    [DmParams("target", "damage", "wound_bonus", "hardhat_safety", "crushes", "impact_sound")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object damage = Dm.arg(__extra, 0);
        object wound_bonus = Dm.arg(__extra, 1);
        object hardhat_safety = Dm.arg(__extra, 2);
        object crushes = Dm.arg(__extra, 3);
        object impact_sound = Dm.arg(__extra, 4);
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        fall_damage = Dm.num(damage);
        fall_wound_bonus = Dm.num(wound_bonus);
        obeys_hardhats = Dm.num(hardhat_safety);
        crushes_people = Dm.num(crushes);
        this.impact_sound = impact_sound;

        RegisterSignal(target, COMSIG_ATOM_ON_Z_IMPACT, nameof(fall_onto_stuff));
        return dot;
    }

    // tg: code/datums/elements/falling_hazard.dm:30
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, COMSIG_ATOM_ON_Z_IMPACT);
        return dot;
    }

    /// Gathers every mob in the turf the target falls on, and does damage/crushes them/makes a message about the target falling on them
    // tg: code/datums/elements/falling_hazard.dm:35
    [SignalHandler]
    public virtual void fall_onto_stuff(datum source = null, turf impacted_turf = null, object levels = null)
    {
        mob_living poor_target = Dm.locate<mob_living>(impacted_turf) as mob_living;

        if (poor_target == null)
            return;

        float target_head_armor = Dm.num(poor_target.run_armor_check(BODY_ZONE_HEAD, MELEE, silent: TRUE));

        if (Dm.truthy(obeys_hardhats) && target_head_armor >= 15) // 15 melee armor is enough that most head items dont have this, but anything above a hardhat should protect you
        {
            poor_target.visible_message(
                span_warning(I18n.T("[] falls on [], thankfully [] had a helmet on!", source, poor_target, poor_target.p_they())),
                span_userdanger(I18n.T("You are hit on the head by [], good thing you had a helmet on!", source)),
                span_hear(I18n.T("You hear a []!", Dm.truthy(crushes_people) ? "crash" : "bonk")));

            if (Dm.truthy(crushes_people))
                Waiting.NoSleep(poor_target.Knockdown(0.25f * SECONDS * fall_damage)); // For a piano, that would be 15 seconds

            playsound(poor_target, Dm.resource("sound/items/weapons/parry.ogg"), 50, TRUE); // You PARRIED the falling object with your EPIC hardhat
            return;
        }

        obj_item_bodypart target_head = poor_target.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;

        // This does more damage the more levels the falling object has fallen
        if (!Dm.truthy(crushes_people) && target_head != null)
            Waiting.NoSleep(poor_target.apply_damage(fall_damage * Dm.num(levels), def_zone: BODY_ZONE_HEAD, wound_bonus: fall_wound_bonus));
        else
            Waiting.NoSleep(poor_target.apply_damage(fall_damage * Dm.num(levels), spread_damage: TRUE, wound_bonus: fall_wound_bonus));

        poor_target.visible_message(
            span_userdanger(I18n.T("[] falls on [], [] []", source, poor_target, Dm.truthy(crushes_people) ? Dm.text("crushing []", poor_target.p_them()) : Dm.text("hitting []", poor_target.p_them()), target_head != null ? "on the head!" : "!")),
            span_userdanger(I18n.T("You are [] by []!", Dm.truthy(crushes_people) ? "crushed" : "hit", source)),
            span_hear(I18n.T("You hear a []!", Dm.truthy(crushes_people) ? "crash" : "bonk")));

        playsound(poor_target, impact_sound, 50, TRUE);

        if (!Dm.truthy(crushes_people))
            return;

        if (iscarbon(poor_target))
            Waiting.NoSleep(poor_target.AddElement(typeof(datum_element_squish), 30 * SECONDS));
        Waiting.NoSleep(poor_target.Paralyze(0.5f * SECONDS * fall_damage)); // For a piano, that would be 30 seconds
        add_memory_in_range(poor_target, 7, typeof(datum_memory_witness_vendor_crush), Dm.named("protagonist", poor_target), Dm.named("antagonist", source));
    }
}
