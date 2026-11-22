// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/organ_sets/carp_organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): the carp's infused organs (space lungs, big jaws that shed teeth and keep masks off, the gnashing
// tongue with the carp's language, the nomad brain that wants a new z-level, the cold-proof heart) and their set bonus, space swimming.
// The colour defines are file-local (#undef at the end); the var blocks are generated (D111).

///Carp lungs! You can breathe in space! Oh... you can't breathe on the station, you need low oxygen environments.
/// Inverts behavior of lungs. Bypasses suffocation due to space / lack of gas, but also allows Oxygen to suffocate.
// tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:20
public partial class obj_item_organ_lungs_carp : obj_item_organ_lungs
{
    // tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:35
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their neck has odd gills."), BODY_ZONE_HEAD));
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_carp)));
        return dot;
    }
}

///occasionally sheds carp teeth, stronger melee (bite) attacks, but you can't cover your mouth anymore.
// tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:40
public partial class obj_item_organ_fangs_carp : obj_item_organ_fangs
{
    // tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their teeth are big and sharp."), BODY_ZONE_PRECISE_MOUTH));
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_carp)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:56
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon fangs_owner = organ_owner;
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        if (!ishuman(fangs_owner))
            return dot;
        mob_living_carbon_human human_receiver = fangs_owner as mob_living_carbon_human;
        if (!Dm.truthy(human_receiver.can_mutate()))
            return dot;
        datum_species rec_species = human_receiver.dna.species as datum_species;
        await rec_species.update_no_equip_flags(fangs_owner, Dm.bitor(rec_species.no_equip_flags, ITEM_SLOT_MASK));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:65
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon fangs_owner = organ_owner as mob_living_carbon;
        await base.on_mob_remove(organ_owner, special, movement_flags);
        if (!ishuman(fangs_owner))
            return;
        mob_living_carbon_human human_receiver = fangs_owner as mob_living_carbon_human;
        if (!Dm.truthy(human_receiver.can_mutate()))
            return;
        datum_species rec_species = human_receiver.dna.species as datum_species;
        await rec_species.update_no_equip_flags(fangs_owner, Dm.initial(rec_species, (datum_species s) => s.no_equip_flags));
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:74
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (IS_UNCONSCIOUS_OR_CRIT(owner) || !prob(0.1f))
            return;
        await owner.emote("cough");
        turf tooth_fairy = get_turf(owner);
        if (tooth_fairy != null)
            Dm.@new<obj_item_knife_carp>(tooth_fairy);
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:85
public partial class obj_item_organ_tongue_carp : obj_item_organ_tongue
{
    // tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:95
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_carp)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:99
    public override object get_possible_languages()
    {
        object dot = base.get_possible_languages();
        Dm.append(ref dot, typeof(datum_language_carptongue));
        return dot;
    }
}

///carp brain. you need to occasionally go to a new zlevel. think of it as... walking your dog!
// tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:110
public partial class obj_item_organ_brain_carp : obj_item_organ_brain
{
    // tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:128
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_carp)));
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_They seem%PRONOUN_s unable to stay still.")));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:133
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon brain_owner = organ_owner;
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        cooldown_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(unsatisfied_nomad)), cooldown_time, TIMER_STOPPABLE | TIMER_OVERRIDE | TIMER_UNIQUE, file: "code/game/machinery/dna_infuser/organ_sets/carp_organs.dm", line: 135);
        RegisterSignal(brain_owner, COMSIG_MOVABLE_Z_CHANGED, nameof(satisfied_nomad));
        return dot;
    }

    //technically you could get around the mood issue by extracting and reimplanting the brain but it will be far easier to just go one z there and back
    // tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:139
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon brain_owner = organ_owner as mob_living_carbon;
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal(brain_owner, COMSIG_MOVABLE_Z_CHANGED);
        deltimer(cooldown_timer);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:144
    public virtual async DmTask unsatisfied_nomad()
    {
        await owner.add_mood_event("nomad", typeof(datum_mood_event_unsatisfied_nomad));
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:147
    [SignalHandler]
    public virtual void satisfied_nomad(params object[] __extra)
    {
        owner.clear_mood_event("nomad");
        cooldown_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(unsatisfied_nomad)), cooldown_time, TIMER_STOPPABLE | TIMER_OVERRIDE | TIMER_UNIQUE, file: "code/game/machinery/dna_infuser/organ_sets/carp_organs.dm", line: 150);
    }
}

/// makes you cold resistant, but heat-weak.
// tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:153
public partial class obj_item_organ_heart_carp : obj_item_organ_heart
{
    // tg: code/game/machinery/dna_infuser/organ_sets/carp_organs.dm:165
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their skin has small patches of scales growing on it."), BODY_ZONE_CHEST));
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_carp)));
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        return dot;
    }
}
