// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/_mutations.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the mutation base's procs, for the clown's clumsiness. SKELETON before: only the type /datum/mutation and the vars that SSatoms.setupGenetics reads and writes (locked, quality, blocks, alias); it has no subtypes yet, so setupGenetics creates none (wave 3, the atoms package),
// declared with the slot signatures of docs/ai/specs/generated/slots.tsv; bodies are Dm.NotTranslated until then.

// tg: code/datums/mutations/_mutations.dm:23
[DmPath("/datum/mutation")]
public partial class datum_mutation : datum
{
    // tg: code/datums/mutations/_mutations.dm:28
    public float locked = FALSE;
    // tg: code/datums/mutations/_mutations.dm:30
    public object quality;
    // tg: code/datums/mutations/_mutations.dm:58
    public float blocks = 4;
    // tg: code/datums/mutations/_mutations.dm:62
    public object alias;

    // tg: code/datums/mutations/_mutations.dm:101
    public override object Destroy(float? force = null)
    {
        power_path = null;
        dna = null;
        owner = null;
        return base.Destroy(force);
    }

    // tg: code/datums/mutations/_mutations.dm:107
    public virtual async DmTask<object> make_copy()
    {
        datum_mutation copy = Dm.@new(type) as datum_mutation;

        copy.chromosome_name = chromosome_name;
        copy.stabilizer_coeff = stabilizer_coeff;
        copy.synchronizer_coeff = synchronizer_coeff;
        copy.power_coeff = power_coeff;
        copy.energy_coeff = energy_coeff;
        copy.can_chromosome = can_chromosome;
        copy.valid_chrom_list = valid_chrom_list;
        update_valid_chromosome_list();

        return copy;
    }

    // tg: code/datums/mutations/_mutations.dm:120
    public virtual async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        if (acquirer == null || Dm.eq(acquirer.stat, DEAD) || Dm.@in(this, acquirer.dna.mutations))
            return FALSE;
        if (Dm.truthy(LAZYLEN(species_allowed)) && !Dm.truthy(LAZYFIND(species_allowed, ((datum_species)acquirer.dna.species).id)))
            return FALSE;
        if (Dm.truthy(health_req) && Dm.num(acquirer.health) < Dm.num(health_req))
            return FALSE;
        if (Dm.truthy(limb_req) && acquirer.get_bodypart(Dm.str(limb_req)) == null)
            return FALSE;
        foreach (datum_mutation mewtayshun in Dm.each<datum_mutation>(acquirer.dna.mutations)) //check for conflicting powers
        {
            if (!Dm.@in(mewtayshun.type, conflicts) && !Dm.@in(type, mewtayshun.conflicts))
                continue;
            return FALSE;
        }
        if (!Dm.truthy(can_acquire(acquirer)))
            return FALSE;

        owner = acquirer;
        dna = acquirer.dna;
        LAZYADD(ref dna.mutations, this);
        // SEND_SIGNAL(src, COMSIG_MUTATION_GAINED, acquirer) (rules 11.1 (e), a statement)
        if (Dm.truthy(_listen_lookup?[COMSIG_MUTATION_GAINED]))
            _SendSignal(COMSIG_MUTATION_GAINED, Dm.list(this, acquirer));
        if (Dm.truthy(text_gain_indication))
            to_chat(owner, text_gain_indication);
        if (Dm.truthy(LAZYLEN(species_allowed)))
            RegisterSignal(owner, COMSIG_SPECIES_GAIN, nameof(on_species_change));
        if (Dm.truthy(mutation_icon) && Dm.truthy(mutation_icon_state))
        {
            RegisterSignal(owner, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlay));
            owner.update_appearance(UPDATE_OVERLAYS);
        }
        await grant_power(); //we do checks here so nothing about hulk getting magic
        if (Dm.truthy(mutation_traits))
            owner.add_traits(mutation_traits, GENETIC_MUTATION);
        return TRUE;
    }

    /// Checks if the mob can acquire the mutation, returning FALSE prevents it
    // tg: code/datums/mutations/_mutations.dm:154
    public virtual object can_acquire(mob_living_carbon_human acquirer = null)
    {
        return TRUE;
    }

    // tg: code/datums/mutations/_mutations.dm:157
    public virtual void get_visual_indicator()
    {
        return;
    }

    // tg: code/datums/mutations/_mutations.dm:160
    public virtual DmTask on_life(object seconds_per_tick = null)
    {
        return DmTask.CompletedTask;
    }

    // tg: code/datums/mutations/_mutations.dm:163
    public virtual async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        if (owner == null || owner.dna.mutations == null || !Dm.truthy(owner.dna.mutations.Remove(this)))
            return TRUE;
        // SEND_SIGNAL(src, COMSIG_MUTATION_LOST, owner) (rules 11.1 (e), a statement)
        if (Dm.truthy(_listen_lookup?[COMSIG_MUTATION_LOST]))
            _SendSignal(COMSIG_MUTATION_LOST, Dm.list(this, owner));
        UnregisterSignal(owner, Dm.list(COMSIG_SPECIES_GAIN, COMSIG_ATOM_UPDATE_OVERLAYS));
        if (Dm.truthy(mutation_icon) && Dm.truthy(mutation_icon_state))
            owner.update_appearance(UPDATE_OVERLAYS);
        if (Dm.truthy(text_lose_indication) && !Dm.eq(owner.stat, DEAD))
            to_chat(owner, text_lose_indication);
        if (Dm.truthy(mutation_traits))
            owner.remove_traits(mutation_traits, GENETIC_MUTATION);
        return FALSE;
    }

    // tg: code/datums/mutations/_mutations.dm:176
    [SignalHandler]
    public virtual void on_species_change(datum source = null, datum_species new_species = null)
    {
        if (!Dm.truthy(LAZYFIND(species_allowed, new_species.id)) || !Dm.truthy(can_acquire(owner)))
            Waiting.NoSleep(dna.remove_mutation(this, sources)); // shouldn't have this mutation at all
    }

    // tg: code/datums/mutations/_mutations.dm:182
    [SignalHandler]
    public virtual void on_update_overlay(datum source = null, DmList overlay_list = null)
    {
        mutable_appearance mutation_appearance = mutable_appearance(mutation_icon, mutation_icon_state, -layer_used) as mutable_appearance;
        owner.apply_height(mutation_appearance, offset_location);

        Dm.append(ref overlay_list, mutation_appearance);
    }

    /**
     * Called after on_aquiring, or when a chromosome is applied.
     * returns the instance of 'power_path' for children calls to use without calling locate() again.
     */
    // tg: code/datums/mutations/_mutations.dm:194
    public virtual async DmTask<object> setup()
    {
        if (power_path == null || QDELETED(owner))
            return null;
        datum_action_cooldown modified_power = Dm.locate_type(power_path, owner.actions) as datum_action_cooldown;
        if (modified_power == null)
            throw Dm.CRASH(Dm.text("Genetic mutation [] called setup(), but could not find a action to modify!", type), "code/datums/mutations/_mutations.dm", 199);
        modified_power.cooldown_time = Dm.num(Dm.initial(modified_power, "cooldown_time")) * (Dm.num(energy_coeff) < 0 ? 1 : Dm.num(energy_coeff)); // GET_MUTATION_ENERGY
        return modified_power;
    }

    /// Called before we apply a chromosome
    // tg: code/datums/mutations/_mutations.dm:204
    public virtual DmTask pre_apply_chromosome()
    {
        return DmTask.CompletedTask;
    }

    /// Called after we apply a chromosome
    // tg: code/datums/mutations/_mutations.dm:208
    public virtual async DmTask post_apply_chromosome()
    {
        await setup();
    }

    // tg: code/datums/mutations/_mutations.dm:211
    public virtual async DmTask<object> grant_power()
    {
        if (!ispath(power_path) || owner == null)
            return FALSE;

        datum_action_cooldown new_power = await Dm.new_async(power_path, this) as datum_action_cooldown;
        new_power.background_icon_state = "bg_tech_blue";
        new_power.base_background_icon_state = new_power.background_icon_state;
        new_power.active_background_icon_state = Dm.text("[]_active", new_power.base_background_icon_state);
        new_power.overlay_icon_state = "bg_tech_blue_border";
        new_power.active_overlay_icon_state = "bg_spell_border_active_blue";
        await new_power.Grant(owner);

        return new_power;
    }

    // Runs through all the coefficients and uses this to determine which chromosomes the
    // mutation can take. Stores these as text strings in a list.
    // tg: code/datums/mutations/_mutations.dm:227
    public virtual void update_valid_chromosome_list()
    {
        valid_chrom_list.Cut();

        if (Dm.eq(can_chromosome, CHROMOSOME_NEVER))
        {
            Dm.append(ref valid_chrom_list, "none");
            return;
        }

        if (stabilizer_coeff != MUTATION_COEFFICIENT_UNMODIFIABLE)
            Dm.append(ref valid_chrom_list, "Stabilizer");
        if (synchronizer_coeff != MUTATION_COEFFICIENT_UNMODIFIABLE)
            Dm.append(ref valid_chrom_list, "Synchronizer");
        if (power_coeff != MUTATION_COEFFICIENT_UNMODIFIABLE)
            Dm.append(ref valid_chrom_list, "Power");
        if (energy_coeff != MUTATION_COEFFICIENT_UNMODIFIABLE)
            Dm.append(ref valid_chrom_list, "Energetic");
    }
}
