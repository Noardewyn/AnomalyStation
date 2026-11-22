// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/space_vines/vine_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4b (D327): the whole file, the space vines' controller (the vines it owns, their growth queue, the spread rate and cap, the
// mutation chance and the severity cap; the last vine's death leaves a kudzu seed pack with the cluster's stats) but its view
// variables menu (vv_get_dropdown, vv_do_topic: the admins' Delete Vines, rules 13; live-partial.txt rows). The var block is
// generated (D111).

///A list of the possible mutations for a vine
// tg: code/modules/events/space_vines/vine_controller.dm:2  GLOBAL_LIST_INIT(vine_mutations_list, init_vine_mutation_list())
public partial class datum_controller_global_vars
{
    // tg: code/modules/events/space_vines/vine_controller.dm:2  GLOBAL_LIST_INIT(vine_mutations_list, init_vine_mutation_list())
    public DmList vine_mutations_list;

    // tg: code/modules/events/space_vines/vine_controller.dm:2  (GLOBAL_MANAGED(vine_mutations_list, init_vine_mutation_list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalvine_mutations_list()
    {
        vine_mutations_list = init_vine_mutation_list() as DmList;
        Dm.append(ref gvars_datum_init_order, "vine_mutations_list");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/events/space_vines/vine_controller.dm:4
    public static object init_vine_mutation_list()
    {
        DmList mutation_list = Dm.list();

        foreach (Type subtype in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_spacevine_mutation))))
        {
            datum_spacevine_mutation mutation = Dm.@new(subtype) as datum_spacevine_mutation;
            mutation_list[mutation] = IDEAL_MAX_SEVERITY - mutation.severity; // the ideal maximum potency is used for weighting
        }

        return mutation_list;
    }

    /**
     * Used to determine whether the mob is immune to actions by the vine.
     * Use cases: Stops vine from attacking itself, other plants.
     */
    // tg: code/modules/events/space_vines/vine_controller.dm:162
    public static object isvineimmune(atom target = null)
    {
        if (isliving(target))
        {
            mob_living victim = target as mob_living;
            // tg passes FACTION_PLANTS as has_faction's match_all (a truthy text): the test is the vines' faction alone
            if (Dm.truthy(victim.has_faction(FACTION_VINES, FACTION_PLANTS)))
                return TRUE;
        }
        return FALSE;
    }
}

// tg: code/modules/events/space_vines/vine_controller.dm:13
public partial class datum_spacevine_controller : datum
{
    // tg: code/modules/events/space_vines/vine_controller.dm:31
    [DmParams("location", "muts", "potency", "production", "event")]
    public override async DmTask New(params object[] __extra)
    {
        turf location = Dm.arg<turf>(__extra, 0);
        DmList muts = Dm.arg<DmList>(__extra, 1);
        object potency = Dm.arg(__extra, 2);
        object production = Dm.arg(__extra, 3);
        datum_round_event @event = Dm.arg<datum_round_event>(__extra, 4);
        vines = Dm.list();
        growth_queue = Dm.list();
        queue_end = Dm.list();
        obj_structure_spacevine vine = await spawn_spacevine_piece(location, null, muts) as obj_structure_spacevine;
        if (@event != null)
            @event.announce_to_ghosts(vine);
        START_PROCESSING(SSobj, this);
        if (potency != null)
        {
            mutativeness = Dm.num(potency) * MUTATIVENESS_SCALE_FACTOR; // If potency is 100, 20 mutativeness; if 1: 0.2 mutativeness
            max_mutation_severity = round(Dm.num(potency) * MAX_SEVERITY_LINEAR_COEFF + MAX_SEVERITY_CONSTANT_TERM); // If potency is 100, 25 max mutation severity; if 1, 10 max mutation severity
        }
        if (production != null && Dm.num(production) <= MAX_POSSIBLE_PRODUCTIVITY_VALUE) //Prevents runtime in case production is set to 11.
        {
            spread_cap = SPREAD_CAP_LINEAR_COEFF * (MAX_POSSIBLE_PRODUCTIVITY_VALUE + 1 - Dm.num(production)) + SPREAD_CAP_CONSTANT_TERM; //Best production speed of 1 increases spread_cap to 60, worst production speed of 10 lowers it to 24, even distribution
            spread_multiplier = Dm.div(SPREAD_MULTIPLIER_MAX, MAX_POSSIBLE_PRODUCTIVITY_VALUE + 1 - Dm.num(production)); // Best production speed of 1: 10% of total vines will spread per second, worst production speed of 10: 1% of total vines (with minimum of 1) will spread per second
        }
        if (@event != null) // spawned by space vine event
        {
            max_mutation_severity += MAX_SEVERITY_EVENT_BONUS;
            minimum_spread_rate = 3;
        }
    }

    // tg: code/modules/events/space_vines/vine_controller.dm:49  vv_get_dropdown and :54 vv_do_topic (the Delete Vines option of
    // the view variables menu): admin tooling (rules 13), live-partial.txt rows.

    // tg: code/modules/events/space_vines/vine_controller.dm:64
    public virtual void DeleteVines() //this is kill
    {
        QDEL_LIST(vines); //this will also qdel us
    }

    // tg: code/modules/events/space_vines/vine_controller.dm:67
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        vines.Cut();
        growth_queue.Cut();
        queue_end.Cut();
        return base.Destroy(force);
    }

    // tg: code/modules/events/space_vines/vine_controller.dm:74
    public virtual async DmTask<object> spawn_spacevine_piece(turf location = null, obj_structure_spacevine parent = null, DmList muts = null)
    {
        obj_structure_spacevine vine = Dm.@new<obj_structure_spacevine>(location);
        Dm.append(ref growth_queue, vine);
        Dm.append(ref vines, vine);
        vine.master = this;
        foreach (object mutation_type in Dm.each(muts))
        {
            // DM's istype takes a type or an instance as its type: the event passes typepaths, a kudzu seed pack its mutations
            Type mutation_path = mutation_type as Type ?? mutation_type?.GetType();
            foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(GLOB.vine_mutations_list))
            {
                if (istype(mutation, mutation_path))
                {
                    await mutation.add_mutation_to_vinepiece(vine);
                    break;
                }
            }
        }
        if (parent != null)
        {
            Dm.combine(ref vine.mutations, parent.mutations);

            object parentcolor = parent.atom_colours[FIXED_COLOUR_PRIORITY];
            await vine.add_atom_colour(parentcolor, FIXED_COLOUR_PRIORITY);
            if (prob(mutativeness))
            {
                datum_spacevine_mutation random_mutate = pick_weight(Dm.sub(GLOB.vine_mutations_list, vine.mutations) as DmList) as datum_spacevine_mutation;
                if (random_mutate != null) //If this vine has every single mutation don't attempt to add a null mutation.
                {
                    float total_severity = random_mutate.severity;
                    foreach (datum_spacevine_mutation mutation in Dm.each_cast<datum_spacevine_mutation>(vine.mutations))
                        total_severity += mutation.severity;
                    if (total_severity <= max_mutation_severity)
                        await random_mutate.add_mutation_to_vinepiece(vine);
                }
            }
        }

        foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(vine.mutations))
            await mutation.on_birth(vine);
        await location.Entered(vine, null);
        return vine;
    }

    // tg: code/modules/events/space_vines/vine_controller.dm:103
    public virtual void VineDestroyed(obj_structure_spacevine vine = null)
    {
        vine.master = null;
        Dm.remove(ref vines, vine);
        Dm.remove(ref growth_queue, vine);
        Dm.remove(ref queue_end, vine);
        if (Dm.truthy(length(vines)))
            return;
        obj_item_seeds_kudzu seed = Dm.@new<obj_item_seeds_kudzu>(vine.loc);
        Dm.combine(ref seed.mutations, vine.mutations);
        seed.set_potency(mutativeness / MUTATIVENESS_SCALE_FACTOR);
        // Mathematical notes:
        // The formula for spread_multiplier is SPREAD_MULTIPLIER_MAX / (MAX_POSSIBLE_PRODUCTIVITY_VALUE + 1 - production)
        // So (MAX_POSSIBLE_PRODUCTIVITY_VALUE + 1 - production) = SPREAD_MULTIPLIER_MAX / spread_multiplier
        // ie. production = MAX_POSSIBLE_PRODUCTIVITY_VALUE + 1 - SPREAD_MULTIPLIER_MAX / spread_multiplier
        seed.set_production(MAX_POSSIBLE_PRODUCTIVITY_VALUE + 1 - Dm.div(SPREAD_MULTIPLIER_MAX, spread_multiplier)); //Reverts spread_multiplier formula so resulting seed gets original production stat or equivalent back.
        qdel(this);
    }

    /// Life cycle of a space vine
    // tg: code/modules/events/space_vines/vine_controller.dm:122
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        float vine_count = length(vines);
        if (!Dm.truthy(vine_count))
        {
            qdel(this); //space vines exterminated. Remove the controller
            return null;
        }

        /// Bonus spread for kudzu that has just started out (ie. with low vine count)
        float start_spread_bonus = max(5 - spread_multiplier * Dm.pow(vine_count, 2) / 400, 0);
        /// Base spread rate, depends solely on spread multiplier and vine count
        float spread_base = Dm.div(0.5f * vine_count, spread_multiplier);
        /// Actual maximum spread rate for this process tick
        float spread_max = round(clamp(Dm.num(seconds_per_tick) * (spread_base + start_spread_bonus), max(Dm.num(seconds_per_tick) * minimum_spread_rate, 1), spread_cap));
        float amount_processed = 0;
        foreach (obj_structure_spacevine vine in Dm.each<obj_structure_spacevine>(growth_queue))
        {
            if (!Dm.truthy(vine.can_spread))
                continue;
            Dm.remove(ref growth_queue, vine);
            Dm.append(ref queue_end, vine);
            foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(vine.mutations))
                mutation.process_mutation(vine);

            if (vine.growth_stage >= 2) //If tile is fully grown
                await vine.entangle_mob();
            else if (SPT_PROB(10, seconds_per_tick)) //If tile isn't fully grown
                await vine.grow();

            await vine.spread();

            amount_processed++;
            if (amount_processed >= spread_max)
                break;
        }

        //We can only do so much work per process, but we still want to process everything at some point
        //So we shift the queue a bit
        Dm.append(ref growth_queue, queue_end);
        queue_end = Dm.list();
        return null;
    }
}
