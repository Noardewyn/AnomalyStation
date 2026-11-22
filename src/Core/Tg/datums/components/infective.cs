// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/infective.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): an infective atom (a contaminated decal, food, a cup, an organ, a blood splatter) gives its
// contact-fluid diseases to whoever crosses, buckles to, bumps, is hit or attacked by it, wears, eats or drinks it, and loses them
// when cleaned. The var block is generated (D111).

// tg: code/datums/components/infective.dm:1
public partial class datum_component_infective : datum_component
{
    // tg: code/datums/components/infective.dm:11
    [DmParams("diseases", "expire_in", "weak", "weak_infection_chance")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object diseases = Dm.arg(__extra, 0);
        object expire_in = Dm.arg(__extra, 1);
        object weak = Dm.arg(__extra, 2) ?? FALSE;
        object weak_infection_chance = Dm.arg(__extra, 3) ?? 10;
        object dot = await base.Initialize(Dm.forward(__extra, diseases, expire_in, weak, weak_infection_chance));
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        if (!islist(diseases))
            diseases = Dm.list(diseases);
        DmList disease_list = (DmList)diseases;

        ///Make sure the diseases list is populated with instances of diseases so that it doesn't have to be for each AddComponent call.
        foreach (object disease in Dm.each(disease_list))
        {
            if (disease == null) //empty entry, remove.
                Dm.remove(disease_list, disease);
            if (disease is Type disease_type && ispath(disease_type, typeof(datum_disease)))
            {
                datum_disease instance = (datum_disease)await Dm.new_async(disease_type);
                Dm.remove(disease_list, disease);
                Dm.append(disease_list, instance);
            }
            else if (!(disease is datum_disease))
            {
                stack_trace(Dm.text("found [] inside the diseases list argument for []", isdatum(disease) ? Dm.text("an instance of []", ((datum)disease).type) : disease, type), "code/datums/components/infective.dm", 26);
                Dm.remove(disease_list, disease);
            }
        }

        this.diseases = disease_list;

        if (Dm.truthy(expire_in))
        {
            expire_time = Dm.add(world.time, expire_in);
            float time__1 = Dm.num(expire_in);
            // QDEL_IN(src, expire_in) (__DEFINES/qdel.dm:60), inline (rules 11.1 (f))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), time__1 > GC_FILTER_QUEUE ? WEAKREF(this) : this), time__1,
                file: "code/datums/components/infective.dm", line: 33);
        }

        is_weak = Dm.num(weak);
        this.weak_infection_chance = Dm.num(weak_infection_chance);
        return dot;
    }

    // tg: code/datums/components/infective.dm:38
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(diseases);
        return base.Destroy(force);
    }

    // tg: code/datums/components/infective.dm:42
    public override async DmTask RegisterWithParent()
    {
        if (Dm.truthy(is_weak) && isitem(parent))
        {
            RegisterSignal(parent, COMSIG_FOOD_EATEN, nameof(try_infect_eat));
            RegisterSignal(parent, COMSIG_PILL_CONSUMED, nameof(try_infect_eat));
            return;
        }
        DmList disease_connections = TgState.Current.datum_component_infective__RegisterWithParent__disease_connections;
        await AddComponent(typeof(datum_component_connect_loc_behalf), parent, disease_connections);

        RegisterSignal(parent, COMSIG_COMPONENT_CLEAN_ACT, nameof(clean));
        RegisterSignal(parent, COMSIG_MOVABLE_BUCKLE, nameof(try_infect_buckle));
        RegisterSignal(parent, COMSIG_MOVABLE_BUMP, nameof(try_infect_collide));
        RegisterSignal(parent, COMSIG_MOVABLE_IMPACT_ZONE, nameof(try_infect_impact_zone));
        if (isitem(parent))
        {
            RegisterSignal(parent, COMSIG_ITEM_ATTACK_ZONE, nameof(try_infect_attack_zone));
            RegisterSignal(parent, COMSIG_ITEM_ATTACK, nameof(try_infect_attack));
            RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(try_infect_equipped));
            RegisterSignal(parent, COMSIG_FOOD_EATEN, nameof(try_infect_eat));
            RegisterSignal(parent, COMSIG_PILL_CONSUMED, nameof(try_infect_eat));
            if (parent is obj_item_reagent_containers_cup)
                RegisterSignal(parent, COMSIG_GLASS_DRANK, nameof(try_infect_drink));
            if (isorgan(parent))
                RegisterSignal(parent, COMSIG_ORGAN_IMPLANTED, nameof(on_organ_insertion));
        }
    }

    // tg: code/datums/components/infective.dm:69
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, Dm.list(
            COMSIG_FOOD_EATEN,
            COMSIG_PILL_CONSUMED,
            COMSIG_COMPONENT_CLEAN_ACT,
            COMSIG_MOVABLE_BUMP,
            COMSIG_MOVABLE_IMPACT_ZONE,
            COMSIG_ITEM_ATTACK_ZONE,
            COMSIG_ITEM_ATTACK,
            COMSIG_ITEM_EQUIPPED,
            COMSIG_GLASS_DRANK,
            COMSIG_ORGAN_IMPLANTED
        ));
        qdel(GetComponent(typeof(datum_component_connect_loc_behalf)));
    }

    // tg: code/datums/components/infective.dm:84
    [SignalHandler]
    public virtual void on_organ_insertion(obj_item_organ target = null, mob_living_carbon receiver = null)
    {
        // SIGNAL_HANDLER

        foreach (datum_disease disease in Dm.each<datum_disease>(diseases))
            Waiting.NoSleep(receiver.ForceContractDisease(disease));

        qdel(this); // once organ is implanted delete the infective component
    }

    // tg: code/datums/components/infective.dm:92
    [SignalHandler]
    public virtual void try_infect_eat(datum source = null, mob_living eater = null, mob_living feeder = null)
    {
        // SIGNAL_HANDLER

        if (HAS_TRAIT(eater, TRAIT_STRONG_STOMACH))
            return;

        Waiting.NoSleep(eater.add_mood_event("disgust", typeof(datum_mood_event_disgust_dirty_food)));

        foreach (datum_disease disease in Dm.each_cast<datum_disease>(diseases))
        {
            if (Dm.truthy(is_weak) && !prob(weak_infection_chance))
                continue;
            if (!Dm.truthy(disease.has_required_infectious_organ(eater as mob_living_carbon, ORGAN_SLOT_STOMACH)))
                continue;

            Waiting.NoSleep(eater.ForceContractDisease(disease));
        }

        if (!Dm.truthy(is_weak))
            Waiting.NoSleep(try_infect(feeder, BODY_ZONE_L_ARM));
    }

    // tg: code/datums/components/infective.dm:111
    [SignalHandler]
    public virtual void try_infect_drink(datum source = null, mob_living drinker = null, mob_living feeder = null)
    {
        // SIGNAL_HANDLER

        if (HAS_TRAIT(drinker, TRAIT_STRONG_STOMACH))
            return;

        if (!Dm.truthy(is_weak))
        {
            object appendage_zone = feeder.get_held_index_of_item(source as obj_item);
            appendage_zone = Dm.num(appendage_zone) == 0 ? BODY_ZONE_CHEST : (IS_RIGHT_INDEX(Dm.num(appendage_zone)) ? BODY_ZONE_R_ARM : BODY_ZONE_L_ARM);
            Waiting.NoSleep(try_infect(feeder, appendage_zone));
        }

        foreach (datum_disease disease in Dm.each_cast<datum_disease>(diseases))
        {
            if (Dm.truthy(is_weak) && !prob(weak_infection_chance))
                continue;
            if (!Dm.truthy(disease.has_required_infectious_organ(drinker as mob_living_carbon, ORGAN_SLOT_STOMACH)))
                continue;

            Waiting.NoSleep(drinker.ForceContractDisease(disease));
        }
    }

    // tg: code/datums/components/infective.dm:130
    [SignalHandler]
    public virtual object clean(datum source = null, object clean_types = null)
    {
        // SIGNAL_HANDLER

        object dot = NONE;
        if (Dm.truthy(Dm.bitand(clean_types, required_clean_types)))
        {
            qdel(this);
            return COMPONENT_CLEANED | COMPONENT_CLEANED_GAIN_XP;
        }
        return dot;
    }

    // tg: code/datums/components/infective.dm:138
    [SignalHandler]
    public virtual void try_infect_buckle(datum source = null, mob M = null, object force = null)
    {
        // SIGNAL_HANDLER

        if (isliving(M))
            Waiting.NoSleep(try_infect((mob_living)M));
    }

    // tg: code/datums/components/infective.dm:144
    [SignalHandler]
    public virtual void try_infect_collide(datum source = null, atom A = null)
    {
        // SIGNAL_HANDLER

        atom_movable P = (atom_movable)parent;
        if (Dm.truthy(P.throwing))
            //this will be handled by try_infect_impact_zone()
            return;
        if (isliving(A))
            Waiting.NoSleep(try_infect((mob_living)A));
    }

    // tg: code/datums/components/infective.dm:154
    [SignalHandler]
    public virtual void try_infect_impact_zone(datum source = null, mob_living target = null, object hit_zone = null)
    {
        // SIGNAL_HANDLER

        Waiting.NoSleep(try_infect(target, hit_zone));
    }

    // tg: code/datums/components/infective.dm:159
    [SignalHandler]
    public virtual void try_infect_attack_zone(obj_item source = null, mob_living_carbon target = null, mob_living user = null, object hit_zone = null)
    {
        // SIGNAL_HANDLER

        Waiting.NoSleep(try_infect(target, hit_zone));
    }

    // tg: code/datums/components/infective.dm:164
    [SignalHandler]
    public virtual void try_infect_attack(obj_item source = null, mob_living target = null, mob_living user = null)
    {
        // SIGNAL_HANDLER
        if (source.loc == user)
        {
            obj_item_bodypart hand = user.get_active_hand() as obj_item_bodypart;
            Waiting.NoSleep(try_infect(user, hand.body_zone));
        }
    }

    // tg: code/datums/components/infective.dm:170
    [SignalHandler]
    public virtual void try_infect_equipped(datum source = null, mob_living L = null, object slot = null)
    {
        // SIGNAL_HANDLER

        object old_bio_armor = null;
        if (isitem(parent))
        {
            //if you are putting an infective item on, it obviously will not protect you, so set its bio armor low enough that it will never block ContactContractDisease()
            obj_item equipped_item = (obj_item)parent;
            old_bio_armor = equipped_item.get_armor_rating(BIO);
            equipped_item.set_armor_rating(BIO, 0);
        }

        Waiting.NoSleep(try_infect(L, slot2body_zone(slot)));

        if (isitem(parent))
        {
            obj_item equipped_item = (obj_item)parent;
            equipped_item.set_armor_rating(BIO, old_bio_armor);
        }
    }

    // tg: code/datums/components/infective.dm:186
    [SignalHandler]
    public virtual void try_infect_crossed(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        // SIGNAL_HANDLER

        if (isliving(arrived))
            Waiting.NoSleep(try_infect((mob_living)arrived, BODY_ZONE_PRECISE_L_FOOT));
    }

    // tg: code/datums/components/infective.dm:192
    [SignalHandler]
    public virtual void try_infect_streak(datum source = null, DmList directions = null, DmList output_diseases = null)
    {
        // SIGNAL_HANDLER

        // This blood is not infectable / does not have a diseases list
        if (!islist(output_diseases))
            return;

        Dm.combine(ref output_diseases, diseases);
    }

    // tg: code/datums/components/infective.dm:201
    public virtual async DmTask try_infect(mob_living L = null, object target_zone = null)
    {
        foreach (object V in Dm.each(diseases))
            await L.ContactContractDisease((datum_disease)V, target_zone);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/infective.dm:48  /datum/component/infective/RegisterWithParent's var/static/list/disease_connections
    internal DmList datum_component_infective__RegisterWithParent__disease_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(datum_component_infective.try_infect_crossed)));
}
