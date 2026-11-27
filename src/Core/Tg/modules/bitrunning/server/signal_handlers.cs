// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/server/signal_handlers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file is translated. The megafauna made virtual (make_virtual_megafauna, util/virtual_megafauna.dm) wait for
// Lavaland's mobs: those mobs are inert, and a domain that holds one is not hosted yet.

// tg: code/modules/bitrunning/server/signal_handlers.dm:1
public partial class obj_machinery_quantum_server : obj_machinery
{
    /// If broken via signal, disconnects all users
    // tg: code/modules/bitrunning/server/signal_handlers.dm:2
    [SignalHandler]
    public virtual void on_broken(datum source = null)
    {
        sever_connections();
    }

    /// Whenever a corpse spawner makes a new corpse, add it to the list of potential mutations
    // tg: code/modules/bitrunning/server/signal_handlers.dm:9
    [SignalHandler]
    public virtual void on_corpse_spawned(datum source = null, mob_living corpse = null)
    {
        mutation_candidate_refs.Add(WEAKREF(corpse));
    }

    /// Being qdeleted - make sure the circuit and connected mobs go with it
    // tg: code/modules/bitrunning/server/signal_handlers.dm:16
    [SignalHandler]
    public virtual void on_delete(datum source = null)
    {
        sever_connections();

        if (generated_domain != null)
            scrub_vdom();

        if (Dm.truthy(is_ready))
            return;
        // in case they're trying to cheese cooldown
        obj_item_circuitboard_machine_quantum_server circuit = Dm.locate<obj_item_circuitboard_machine_quantum_server>(contents);
        if (circuit != null)
            qdel(circuit);
    }

    /// Whenever something enters the send tiles, check if it's a loot crate. If so, alert players.
    // tg: code/modules/bitrunning/server/signal_handlers.dm:33
    [SignalHandler]
    public virtual void on_goal_turf_entered(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        obj_machinery_byteforge chosen_forge = get_random_nearby_forge() as obj_machinery_byteforge;
        if (chosen_forge == null)
            return;

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && isliving(arrived))
        {
            mob_living creature = arrived as mob_living;

            if (!Dm.truthy(creature.mind?.has_antag_datum(typeof(datum_antagonist_bitrunning_glitch), check_subtypes: TRUE)))
                return;

            Waiting.Detach(station_spawn(arrived as mob_living, chosen_forge, source as turf));
            return;
        }

        if (arrived is obj_structure_closet_crate_secure_bitrunning_encrypted)
        {
            Waiting.NoSleep(generate_loot(arrived as obj, chosen_forge));
            return;
        }

        if (arrived is obj_item_storage_lockbox_bitrunning_encrypted)
        {
            Waiting.NoSleep(generate_secondary_loot(arrived as obj, chosen_forge));
            return;
        }
    }

    /// Handles examining the server. Shows cooldown time and efficiency.
    // tg: code/modules/bitrunning/server/signal_handlers.dm:59
    [SignalHandler]
    public virtual void on_goal_turf_examined(datum source = null, mob examiner = null, DmList examine_text = null)
    {
        Dm.append(examine_text, span_info(I18n.T("Beneath your gaze, the floor pulses subtly with streams of encoded data.")));
        Dm.append(examine_text, span_info(I18n.T("It seems to be part of the location designated for retrieving encrypted payloads.")));
    }

    /// Scans over the inbound created_atoms from lazy templates
    // tg: code/modules/bitrunning/server/signal_handlers.dm:67
    [SignalHandler]
    public virtual void on_template_loaded(datum_lazy_template source = null, DmList created_atoms = null)
    {
        foreach (object thing in Dm.each(created_atoms))
        {
            if (isliving(thing)) // so we can mutate them
            {
                mob_living creature = thing as mob_living;

                if (creature is mob_living_simple_animal_hostile_megafauna)
                {
                    // make_virtual_megafauna (util/virtual_megafauna.dm:2) waits for the megafauna, Lavaland's mobs
                    Dm.NotTranslated("code/modules/bitrunning/util/virtual_megafauna.dm:2", "make_virtual_megafauna: the megafauna (Lavaland's mobs)");
                    continue;
                }

                if (creature is mob_living_basic_boss)
                {
                    // make_virtual_megafauna (util/virtual_megafauna.dm:31) waits for the bosses, Lavaland's mobs
                    Dm.NotTranslated("code/modules/bitrunning/util/virtual_megafauna.dm:31", "make_virtual_megafauna: the bosses (Lavaland's mobs)");
                    continue;
                }

                mutation_candidate_refs.Add(WEAKREF(creature));
                continue;
            }

            if (thing is obj_effect_mob_spawn_ghost_role) // so we get threat alerts
            {
                RegisterSignal(thing as datum, COMSIG_GHOSTROLE_SPAWNED, nameof(on_threat_created));
                continue;
            }

            if (thing is obj_effect_mob_spawn_corpse) // corpses are valid targets too
            {
                obj_effect_mob_spawn_corpse spawner = thing as obj_effect_mob_spawn_corpse;

                mutation_candidate_refs.Add(spawner.spawned_mob_ref);
                continue;
            }

            if (thing is obj_machinery_suit_storage_unit)
            {
                obj_machinery_suit_storage_unit storage = thing as obj_machinery_suit_storage_unit;
                storage.disable_modlink();
                continue;
            }

            if (thing is obj_item_mod_control)
            {
                obj_item_mod_control modsuit = thing as obj_item_mod_control;
                modsuit.disable_modlink();
            }
        }

        UnregisterSignal(source, COMSIG_LAZY_TEMPLATE_LOADED);

        /// Just in case there's any special handling for the domain
        Waiting.NoSleep(generated_domain.setup_domain(created_atoms));
    }

    /// Handles when cybercops are summoned into the area or ghosts click a ghost role spawner
    // tg: code/modules/bitrunning/server/signal_handlers.dm:113
    [SignalHandler]
    public virtual void on_threat_created(datum source = null, mob_living threat = null)
    {
        Waiting.NoSleep(add_threats(threat));
    }
}
