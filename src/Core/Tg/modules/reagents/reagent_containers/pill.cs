// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/pill.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file (swallowing, the sugar shell's layers digested in the stomach, dissolving in a container,
// coating and washing with sugar and water, the maintenance pills; the germs of a pill left on the floor since gaps VI1). Deferred
// leaves: the digestion's game log, the maintenance pill achievement (achievements, M10), the accidental pill's food
// taste reaction (food, D188/D194). The var blocks are generated.

// tg: code/modules/reagents/reagent_containers/pill.dm:1
public partial class obj_item_reagent_containers_applicator_pill : obj_item_reagent_containers_applicator
{
    // tg: code/modules/reagents/reagent_containers/pill.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(icon_state))
            icon_state = Dm.text("pill[]", rand(1, 20));
        Waiting.NoSleep(AddComponent(typeof(datum_component_germ_sensitive), mapload)); // gaps VI1
        RegisterSignal(this, COMSIG_ATOM_STOMACH_DIGESTED, nameof(on_digestion));
        RegisterSignal(this, COMSIG_ATOM_REAGENT_EXAMINE, nameof(reagent_special_examine));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/pill.dm:22
    [SignalHandler]
    public virtual void reagent_special_examine(datum source = null, mob user = null, DmList examine_list = null, object can_see_insides = null)
    {
        if (Dm.truthy(layers_remaining))
            examine_list.Add(span_notice(I18n.T("Its sugary shell will last approximately [] seconds in a human stomach.", layers_remaining)));
        else
            examine_list.Add(span_warning(I18n.T("Its shell is completely dissolved!")));
    }

    ///Runs the consumption code, can be overriden for special effects
    // tg: code/modules/reagents/reagent_containers/pill.dm:30
    public override async DmTask<object> on_consumption(mob consumer = null, mob giver = null, DmList modifiers = null)
    {
        if (Dm.eq(icon_state, "pill4") && prob(5)) //you take the red pill - you stay in Wonderland, and I show you how deep the rabbit hole goes
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, nameof(to_chat), consumer, span_notice(I18n.T("[]", pick(strings(REDPILL_FILE, "redpill_questions"))))), 5 * SECONDS, file: "code/modules/reagents/reagent_containers/pill.dm", line: 32);

        if (Dm.truthy(consumer._listen_lookup?[COMSIG_LIVING_PILL_CONSUMED]))
            consumer._SendSignal(COMSIG_LIVING_PILL_CONSUMED, Dm.list(consumer, this, giver));
        if (Dm.truthy(_listen_lookup?[COMSIG_PILL_CONSUMED]))
            _SendSignal(COMSIG_PILL_CONSUMED, Dm.list(this, consumer, giver));

        if (iscarbon(consumer) && Dm.truthy(layers_remaining))
        {
            mob_living_carbon as_carbon = (mob_living_carbon)consumer;
            obj_item_organ_stomach stomach = as_carbon.get_organ_by_type(typeof(obj_item_organ_stomach)) as obj_item_organ_stomach;
            if (stomach != null)
            {
                // tg passes the holder itself (reagents); its reagent list is what the taste reads
                if (prob(max(0, 100 * (layers_remaining - PILL_MAX_TASTE_LAYERS))))
                    as_carbon.taste_list(((datum_reagents)reagents).reagent_list);
                else if (!Dm.truthy(as_carbon.check_tasting_blocks()))
                    as_carbon.send_taste_message("starchy sugar");
                await stomach.consume_thing(this);
                return null;
            }
        }

        if (Dm.truthy(((datum_reagents)reagents).total_volume))
            await ((datum_reagents)reagents).trans_to(consumer, ((datum_reagents)reagents).total_volume, transferred_by: giver, methods: INGEST);
        qdel(this);
        return null;
    }

    // tg: code/modules/reagents/reagent_containers/pill.dm:52
    public override async DmTask<object> interact_with_atom(atom target = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(target, user, modifiers);
        if (Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(target.is_refillable()))
            return NONE;

        datum_reagents target_holder = (datum_reagents)target.reagents;
        if (Dm.truthy(target.is_drainable()) && !Dm.truthy(target_holder.total_volume))
        {
            to_chat(user, span_warning(I18n.T("[] is empty! There's nothing to dissolve [] in.", target, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(target_holder.holder_full()))
        {
            to_chat(user, span_warning(I18n.T("[] is full.", target)));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_warning(I18n.T("[] slips something into []!", user, target)), span_notice(I18n.T("You dissolve [] in [].", this, target)), null, 2);
        await ((datum_reagents)reagents).trans_to(target, ((datum_reagents)reagents).total_volume, transferred_by: user);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/pill.dm:73
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        obj_item_reagent_containers container = null;
        string use_verb = null;
        if (istype(tool, typeof(obj_item_reagent_containers_dropper)))
        {
            container = (obj_item_reagent_containers)tool;
            use_verb = "squirt";
        }
        else if (istype(tool, typeof(obj_item_reagent_containers_cup)))
        {
            container = (obj_item_reagent_containers)tool;
            if (!Dm.truthy(container.is_drainable()))
            {
                to_chat(user, span_warning(I18n.T("You cannot pour []'s contents onto []!", container, this)));
                return ITEM_INTERACT_BLOCKING;
            }
            use_verb = "pour";
        }

        if (container == null)
            return NONE;

        datum_reagents container_holder = (datum_reagents)container.reagents;
        if (container_holder.has_reagent(typeof(datum_reagent_consumable_sugar)) is datum_reagent sugar)
        {
            if (layers_remaining >= PILL_MAX_LAYERS) // Full minute
            {
                to_chat(user, span_warning(I18n.T("[]'s coating is too thick for you to cover it in any more sugar!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            float to_apply = floor(min(container.amount_per_transfer_from_this, Dm.num(sugar.volume), PILL_MAX_LAYERS - layers_remaining));
            await container_holder.remove_reagent(typeof(datum_reagent_consumable_sugar), to_apply);
            layers_remaining += to_apply;
            to_chat(user, span_notice(I18n.T("You [] some of []'s contents onto [], thickening its sugary shell.", use_verb, container, this)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (container_holder.has_reagent(typeof(datum_reagent_water)) is not datum_reagent water)
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (!Dm.truthy(layers_remaining)) // No coating
        {
            to_chat(user, span_warning(I18n.T("[] doesn't have any more external layers to dissolve!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        float to_apply__1 = floor(min(container.amount_per_transfer_from_this, Dm.num(water.volume), layers_remaining));
        await container_holder.remove_reagent(typeof(datum_reagent_water), to_apply__1);
        layers_remaining -= to_apply__1;
        to_chat(user, span_notice(I18n.T("You [] some of []'s contents onto [], dissolving its sugary shell.", use_verb, container, this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/pill.dm:118
    [SignalHandler]
    public virtual object on_digestion(datum source = null, obj_item_organ_stomach stomach = null, mob_living_carbon owner = null, object seconds_per_tick = null)
    {
        layers_remaining -= Dm.num(seconds_per_tick);
        if (layers_remaining >= Dm.num(seconds_per_tick))
            return COMPONENT_CANCEL_DIGESTION;

        // SSmobs.wait is 2 seconds (as of writing this), so we can end up with a delay of 1 second. In this case, use a timer
        if (layers_remaining > 0)
        {
            // Using weakrefs because the mob can get deleted in the meanwhile and leave the pill behind
            _addtimer(Dm.@new<datum_callback>(this, nameof(finish_digesting), WEAKREF(stomach), WEAKREF(owner)), layers_remaining * SECONDS, file: "code/modules/reagents/reagent_containers/pill.dm", line: 127);
            return COMPONENT_CANCEL_DIGESTION;
        }

        // I think we should log this in case of horrible shenanigans
        owner.log_message(Dm.text("Had \\a [] pill dissolve in [] stomach, containing the following reagents: [].", this, owner.p_their(), english_list(((datum_reagents)reagents).reagent_list)), LOG_GAME);
        if (Dm.truthy(((datum_reagents)reagents).total_volume))
            Waiting.NoSleep(((datum_reagents)reagents).trans_to(owner, ((datum_reagents)reagents).total_volume, methods: INGEST, show_message: FALSE));
        qdel(this);
        return COMPONENT_CANCEL_DIGESTION;
    }

    // tg: code/modules/reagents/reagent_containers/pill.dm:137
    public virtual async DmTask finish_digesting(datum_weakref stomach_ref = null, datum_weakref owner_ref = null)
    {
        obj_item_organ_stomach stomach = stomach_ref.resolve() as obj_item_organ_stomach;
        mob_living_carbon owner = owner_ref.resolve() as mob_living_carbon;
        if (owner == null || stomach == null)
            return;

        // Whenever stomach is inside of a mob, its contents are also moved to the mob, so we check for owner as our loc
        if (loc != owner || stomach.owner != owner || stomach.loc != owner)
            return;

        owner.log_message(Dm.text("Had \\a [] pill dissolve in [] stomach, containing the following reagents: [].", this, owner.p_their(), english_list(((datum_reagents)reagents).reagent_list)), LOG_GAME);
        if (Dm.truthy(((datum_reagents)reagents).total_volume))
            await ((datum_reagents)reagents).trans_to(owner, ((datum_reagents)reagents).total_volume, methods: INGEST);
        qdel(this);
    }

    /*
     * On accidental consumption, consume the pill
     */
    // tg: code/modules/reagents/reagent_containers/pill.dm:155
    public override async DmTask<object> on_accidental_consumption(mob_living_carbon victim = null, mob_living_carbon user = null, obj_item source_item = null, float? discover_after = null)
    {
        discover_after ??= FALSE;
        // if(victim.get_food_taste_reaction(source_item) != FOOD_LIKED) to_chat(victim, "You swallow something small. ...")
        Dm.Deferred("code/modules/reagents/reagent_containers/pill.dm:156", "on_accidental_consumption: the eater's food taste reaction to the food the pill hid in (food, D188/D194)");
        await on_consumption(victim, user);
        return FALSE;
    }
}

// tg: code/modules/reagents/reagent_containers/pill.dm:339
public partial class obj_item_reagent_containers_applicator_pill_maintenance : obj_item_reagent_containers_applicator_pill
{
    // tg: code/modules/reagents/reagent_containers/pill.dm:364
    public static DmList names => TgState.Current.obj_item_reagent_containers_applicator_pill_maintenance__names ??= Dm.list(
        "maintenance pill",
        "floor pill",
        "mystery pill",
        "suspicious pill",
        "strange pill",
        "lucky pill",
        "ominous pill",
        "eerie pill"
    );
    // tg: code/modules/reagents/reagent_containers/pill.dm:374
    public static DmList descs => TgState.Current.obj_item_reagent_containers_applicator_pill_maintenance__descs ??= Dm.list(
        "Your feeling is telling you no, but...",
        "Drugs are expensive, you can't afford not to eat any pills that you find.",
        "Surely, there's no way this could go bad.",
        "Winners don't do dr- oh what the heck!",
        "Free pills? At no cost, how could I lose?"
    );

    // tg: code/modules/reagents/reagent_containers/pill.dm:382
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        list_reagents = Dm.list(Dm.kv(get_random_reagent_id(random_reagent_flag), rand(10, 50))); //list_reagents is called before init, because init generates the reagents using list_reagents
        object dot = base.Initialize(mapload, __extra);
        name = I18n.T("[]", pick(names));
        if (prob(30))
            desc = I18n.T("[]", pick(descs));
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/pill.dm:388
public partial class obj_item_reagent_containers_applicator_pill_maintenance_achievement : obj_item_reagent_containers_applicator_pill_maintenance
{
    // tg: code/modules/reagents/reagent_containers/pill.dm:394
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ON_REAGENT_SCAN, nameof(on_chemical_scan));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/pill.dm:398
    public override async DmTask<object> on_consumption(mob consumer = null, mob giver = null, DmList modifiers = null)
    {
        object dot = await base.on_consumption(consumer, giver, modifiers);
        if (Dm.truthy(count_towards_achievement))
        {
            // consumer.client?.give_award(/datum/award/score/maintenance_pill, consumer)
            Dm.Deferred("code/modules/reagents/reagent_containers/pill.dm:401", "on_consumption: the maintenance pill score (achievements, M10+)");
        }
        return dot;
    }

    ///called when we are chemically scanned, we no longer grant an achievement.
    // tg: code/modules/reagents/reagent_containers/pill.dm:404
    [SignalHandler]
    public virtual void on_chemical_scan(atom source = null, mob user = null)
    {
        count_towards_achievement = FALSE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/reagents/reagent_containers/pill.dm:364-380  the maintenance pills' static names and descriptions
    internal DmList obj_item_reagent_containers_applicator_pill_maintenance__names;
    internal DmList obj_item_reagent_containers_applicator_pill_maintenance__descs;
}
