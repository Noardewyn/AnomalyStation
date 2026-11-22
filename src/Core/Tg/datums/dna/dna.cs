// Translated from tgstation (snapshot 1d99bd7f): code/datums/dna/dna.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file since gaps P3 (D327): the DNA datum, its identity, enzymes and features hashes, its mutation blocks and their
// activation, the instability and its meltdown, the species change and the mobs' DNA procs. Before: generate_gene_sequence (wave 3),
// copy_dna, /datum/dna/stored and the brain mob's set_species (M6 slice 5), can_mutate, activate_mutation and mutation_in_sequence
// (syndicate G4a, D316).

public static partial class GlobalProcs
{
    // tg: code/datums/dna/dna.dm:225
    public static object generate_gene_sequence(float? length = null)
    {
        length ??= 4;
        DmList active_sequences = Dm.list("AT", "TA", "GC", "CG");
        string sequence = "";
        for (int i = 1, i__end = (int)length * DNA_SEQUENCE_LENGTH; i <= i__end; i++)
            sequence += Dm.str(pick(active_sequences));
        return sequence;
    }
}

// tg: code/datums/dna/dna.dm:9 (global procs)
public static partial class GlobalProcs
{
    // tg: code/datums/dna/dna.dm:9
    public static object populate_total_ui_len_by_block()
    {
        DmList dot = Dm.list();
        float total_block_len = 1;
        foreach (object block_path in Dm.each(GLOB.dna_identity_blocks))
        {
            datum_dna_block_identity block = GLOB.dna_identity_blocks[block_path] as datum_dna_block_identity;
            dot[block_path] = Dm.add(dot[block_path], total_block_len);
            total_block_len += block.block_length;
        }
        return dot;
    }

    ///Ditto but for unique features. Used by the datum/dna/set_uni_feature_block and datum/dna/get_uni_feature_block procs.
    // tg: code/datums/dna/dna.dm:20
    public static object populate_total_uf_len_by_block()
    {
        DmList dot = Dm.list();
        float total_block_len = 1;
        foreach (object block_path in Dm.each(GLOB.dna_feature_blocks))
        {
            datum_dna_block_feature block = GLOB.dna_feature_blocks[block_path] as datum_dna_block_feature;
            dot[block_path] = Dm.add(dot[block_path], total_block_len);
            total_block_len += block.block_length;
        }
        return dot;
    }

    //Used to create a chipped gene sequence
    // tg: code/datums/dna/dna.dm:233
    public static object create_sequence(object mutation = null, object active = null, object difficulty = null)
    {
        if (!Dm.truthy(difficulty))
        {
            datum_mutation A = GET_INITIALIZED_MUTATION(mutation) as datum_mutation; //leaves the possibility to change difficulty mid-round
            if (A == null)
                return null;
            difficulty = A.difficulty;
        }
        difficulty = Dm.add(difficulty, rand(-2, 4));
        string sequence = Dm.str(GET_SEQUENCE(mutation));
        if (Dm.truthy(active))
            return sequence;
        while (Dm.truthy(difficulty))
        {
            float randnum = rand(1, length(sequence));
            sequence = copytext(sequence, 1, randnum) + "X" + copytext(sequence, randnum + 1);
            difficulty = Dm.sub(difficulty, 1);
        }
        return sequence;
    }

    // tg: code/datums/dna/dna.dm:641
    public static async DmTask scramble_dna(mob_living_carbon M = null, float? ui = null, float? se = null, float? uf = null, float? probability = null)
    {
        ui ??= FALSE;
        se ??= FALSE;
        uf ??= FALSE;
        probability ??= 100;
        if (!Dm.truthy(M.has_dna()))
            throw CRASH(Dm.text("[] does not have DNA", M), "code/datums/dna/dna.dm", 643);
        if (HAS_TRAIT(M, TRAIT_NO_DNA_SCRAMBLE))
            return;
        if (Dm.truthy(se))
        {
            for (int i = 1; i <= DNA_MUTATION_BLOCKS; i++)
            {
                if (prob(probability.Value))
                    M.dna.generate_dna_blocks();
            }
            await M.domutcheck();
        }
        if (Dm.truthy(ui))
        {
            foreach (object block_id in Dm.each(GLOB.dna_identity_blocks))
            {
                datum_dna_block_identity block = GLOB.dna_identity_blocks[block_id] as datum_dna_block_identity;
                if (prob(probability.Value))
                    M.dna.unique_identity = block.modified_hash(M.dna.unique_identity, random_string(block.block_length, GLOB.hex_characters));
            }
        }
        if (Dm.truthy(uf))
        {
            foreach (object block_id in Dm.each(GLOB.dna_feature_blocks))
            {
                datum_dna_block_feature block = GLOB.dna_feature_blocks[block_id] as datum_dna_block_feature;
                // tg writes the feature block into the unique identity (dna.dm:661), a quirk kept as tg has it
                if (prob(probability.Value))
                    M.dna.unique_identity = block.modified_hash(M.dna.unique_identity, random_string(block.block_length, GLOB.hex_characters));
            }
        }
        if (Dm.truthy(ui) || Dm.truthy(uf))
            await M.updateappearance(mutcolor_update: uf, mutations_overlay_update: 1);
    }

    //value in range 1 to values. values must be greater than 0
    //all arguments assumed to be positive integers
    // tg: code/datums/dna/dna.dm:666
    public static object construct_block(object value = null, object values = null, float? blocksize = null)
    {
        blocksize ??= DNA_BLOCK_SIZE;
        float width = round(MathF.Pow(16, blocksize.Value) / Dm.num(values));
        float v = Dm.num(value);
        if (v < 1)
            v = 1;
        v = (v * width) - rand(1, width);
        return num2hex(v, blocksize.Value);
    }

    //value is hex
    // tg: code/datums/dna/dna.dm:674
    public static object deconstruct_block(object value = null, object values = null, float? blocksize = null)
    {
        blocksize ??= DNA_BLOCK_SIZE;
        float width = round(MathF.Pow(16, blocksize.Value) / Dm.num(values));
        float v = round(Dm.num(hex2num(value)) / width) + 1;
        if (v > Dm.num(values))
            v = Dm.num(values);
        return v;
    }
}

/////////////////////////// DNA DATUM
// tg: code/datums/dna/dna.dm:29
// The var block (:30-58) was written by the M2 contracts step (rules 10.2): the declarations, the object-creating
// defaults (dm_init_vars) and the statics nonfatal_meltdowns and fatal_meltdowns (TgState fields, rules 10.6).
[DmPath("/datum/dna")]
public partial class datum_dna : datum
{
    // tg: code/datums/dna/dna.dm:40, :49, :51  the object-creating defaults (rules 10.2; var/x[n] is new /list(n), 6.1)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        features = Dm.list(Dm.kv(FEATURE_MUTANT_COLOR, COLOR_WHITE));
        mutation_index = Dm.list_n(DNA_MUTATION_BLOCKS);
        default_mutation_genes = Dm.list_n(DNA_MUTATION_BLOCKS);
    }

    ///An md5 hash of the dna holder's real name
    // tg: code/datums/dna/dna.dm:31
    public object unique_enzymes;
    ///Stores the hashed values of traits such as skin tones, hair style, and gender
    // tg: code/datums/dna/dna.dm:33
    public object unique_identity;
    ///The blood type datum, usually a singleton
    // tg: code/datums/dna/dna.dm:35
    public datum_blood_type blood_type;
    ///The type of mutant race the player is if applicable (i.e. potato-man)
    // tg: code/datums/dna/dna.dm:37  var/datum/species/species = /datum/species/human: object (rules 3.2, typed vars
    // that hold wider values; 3.5): the default is a typepath and New() stores the instance, species = new species (:65)
    public object species = typeof(datum_species_human);
    /// Assoc list of feature keys to their value
    /// Note if you set these manually, and do not update [unique_features] afterwards, it will likely be reset.
    // tg: code/datums/dna/dna.dm:40
    public DmList features;
    ///Stores the hashed values of the person's non-human features
    // tg: code/datums/dna/dna.dm:42
    public object unique_features;
    ///Stores the real name of the person who originally got this dna datum. Used primarily for changelings
    // tg: code/datums/dna/dna.dm:44
    public object real_name;
    ///All mutations are from now on here
    // tg: code/datums/dna/dna.dm:46
    public DmList mutations;
    // tg: code/datums/dna/dna.dm:47
    public mob_living holder;
    ///List of which mutations this carbon has and its assigned block
    // tg: code/datums/dna/dna.dm:49  var/mutation_index[DNA_MUTATION_BLOCKS]
    public DmList mutation_index;
    ///List of the default genes from this mutation to allow DNA Scanner highlighting
    // tg: code/datums/dna/dna.dm:51  var/default_mutation_genes[DNA_MUTATION_BLOCKS]
    public DmList default_mutation_genes;
    // tg: code/datums/dna/dna.dm:52
    public float stability = 100;
    ///Did we take something like mutagen? In that case we can't get our genes scanned to instantly cheese all the powers.
    // tg: code/datums/dna/dna.dm:54
    public float scrambled = FALSE;
    /// Weighted list of nonlethal meltdowns
    // tg: code/datums/dna/dna.dm:56  var/static/list/nonfatal_meltdowns = list() (rules 10.6: a TgState field, below)
    public static ref DmList nonfatal_meltdowns => ref TgState.Current.datum_dna__nonfatal_meltdowns;
    /// Weighted list of lethal meltdowns
    // tg: code/datums/dna/dna.dm:58  var/static/list/fatal_meltdowns = list() (rules 10.6: a TgState field, below)
    public static ref DmList fatal_meltdowns => ref TgState.Current.datum_dna__fatal_meltdowns;
}

public sealed partial class TgState
{
    // tg: code/datums/dna/dna.dm:56  var/static/list/nonfatal_meltdowns = list()
    internal DmList datum_dna__nonfatal_meltdowns = Dm.list();
    // tg: code/datums/dna/dna.dm:58  var/static/list/fatal_meltdowns = list()
    internal DmList datum_dna__fatal_meltdowns = Dm.list();
}

// tg: code/datums/dna/dna.dm:60 (interleaved types, rules 1.9)
public partial class datum_dna : datum
{
    // tg: code/datums/dna/dna.dm:60
    [DmParams("new_holder", "mob_species")]
    public override async DmTask New(params object[] __extra)
    {
        mob_living new_holder = Dm.arg<mob_living>(__extra, 0);
        object mob_species = Dm.arg(__extra, 1);
        if (new_holder is mob_living)
            holder = new_holder;
        if (Dm.truthy(mob_species))
            species = mob_species;
        species = await Dm.new_async(species as Type);
    }

    // tg: code/datums/dna/dna.dm:67
    public override object Destroy(float? force = null)
    {
        if (iscarbon(holder))
        {
            mob_living_carbon as_carbon = holder as mob_living_carbon;
            foreach (datum_mutation mutation in Dm.each_cast<datum_mutation>(mutations))
            {
                Waiting.NoSleep(remove_mutation(mutation, mutation.sources)); // mutations hold a reference to the dna, we need to delete them.
            }
            if (as_carbon.dna == this)
                as_carbon.dna = null;
        }
        holder = null;

        QDEL_NULL(ref species);

        LAZYNULL(ref mutations); //This only references mutations, just dereference.

        return base.Destroy(force);
    }
}

// tg: code/datums/dna/dna.dm:83
public partial class datum_dna : datum
{
    // tg: code/datums/dna/dna.dm:83
    // (M6 slice 5: a gibbed player's brain mob keeps a copy of their DNA, transfer_identity)
    public virtual async DmTask copy_dna(datum_dna new_dna = null, float? transfer_flags = null)
    {
        transfer_flags ??= Dm.bitor(COPY_DNA_SE, COPY_DNA_SPECIES);
        new_dna.unique_enzymes = unique_enzymes;
        new_dna.unique_identity = unique_identity;
        new_dna.unique_features = unique_features;
        new_dna.features = features.Copy();
        new_dna.real_name = real_name;
        if (Dm.truthy(Dm.bitand(transfer_flags, COPY_DNA_SE)))
        {
            new_dna.mutation_index = mutation_index;
            new_dna.default_mutation_genes = default_mutation_genes;
        }
        //if the new DNA has a holder, transform them immediately, otherwise save it
        if (new_dna.holder != null)
        {
            if (iscarbon(new_dna.holder))
            {
                mob_living_carbon as_carbon = (mob_living_carbon)new_dna.holder;
                as_carbon.set_blood_type(blood_type);
            }
            if (Dm.truthy(Dm.bitand(transfer_flags, COPY_DNA_SPECIES)))
                new_dna.holder.set_species(((datum)species).GetType(), icon_update: FALSE);
        }
        else
        {
            new_dna.blood_type = blood_type;
            if (Dm.truthy(Dm.bitand(transfer_flags, COPY_DNA_SPECIES)))
                new_dna.species = Dm.@new(((datum)species).GetType());
        }
        if (Dm.truthy(Dm.bitand(transfer_flags, COPY_DNA_MUTATIONS)) && Dm.truthy(holder?.can_mutate()))
        {
            // Mutations aren't gc managed, but they still aren't templates
            // Let's do a proper copy
            foreach (datum_mutation mutation in Dm.each<datum_mutation>(mutations))
            {
                object valid_sources = Dm.bitand(mutation.sources, GLOB.standard_mutation_sources);
                if (!Dm.truthy(length(valid_sources)))
                    continue;
                await new_dna.add_mutation(mutation, valid_sources);
            }
        }
    }

    // tg: code/datums/dna/dna.dm:113
    public virtual async DmTask add_mutation(object mutation_to_add = null, object sources = null)
    {
        if (!Dm.islist(sources))
        {
            if (!Dm.truthy(sources))
                throw Dm.CRASH("add_mutation() called without set source(s)", "code/datums/dna/dna.dm", 116);
            sources = Dm.list(sources);
        }

        datum_mutation actual_mutation = get_mutation(mutation_to_add) as datum_mutation;
        DmList sources_to_add = ((DmList)sources).Copy(); //make sure not to modify the original if it's stored in a variable outside this proc
        if (actual_mutation == null)
        {
            if (mutation_to_add is datum_mutation mutation_instance)
                actual_mutation = await mutation_instance.make_copy() as datum_mutation;
            else
                actual_mutation = Dm.@new((Type)mutation_to_add) as datum_mutation;
            // SEND_SIGNAL(holder, COMSIG_CARBON_GAIN_MUTATION, actual_mutation.type, sources_to_add) (rules 11.1 (e), a statement)
            if (Dm.truthy(holder._listen_lookup?[COMSIG_CARBON_GAIN_MUTATION]))
                holder._SendSignal(COMSIG_CARBON_GAIN_MUTATION, Dm.list(holder, actual_mutation.type, sources_to_add));
        }
        else
        {
            Dm.remove(ref sources_to_add, actual_mutation.sources);
            if (!Dm.truthy(length(sources_to_add))) //no new sources to add, don't do anything.
                return;
        }

        if (!Dm.truthy(length(actual_mutation.sources)))
        {
            if (!Dm.truthy(await actual_mutation.on_acquiring(holder as mob_living_carbon_human)))
            {
                to_chat(holder, span_warning(I18n.T("You feel your genes resisting something.")));
                qdel(actual_mutation);
                return;
            }
            await actual_mutation.setup();
        }

        Dm.combine(ref actual_mutation.sources, sources);

        if (Dm.@in(MUTATION_SOURCE_ACTIVATED, (DmList)sources))
            set_se(1, actual_mutation);

        await update_instability();
    }

    // tg: code/datums/dna/dna.dm:147
    public virtual async DmTask<object> remove_mutation(object mutation_to_remove = null, object sources = null)
    {
        if (!Dm.islist(sources))
        {
            if (!Dm.truthy(sources))
                throw Dm.CRASH("remove_mutation() called without set source(s)", "code/datums/dna/dna.dm", 150);
            sources = Dm.list(sources);
        }

        datum_mutation actual_mutation = get_mutation(mutation_to_remove) as datum_mutation;

        if (actual_mutation == null || !Dm.truthy(length(Dm.bitand(sources, actual_mutation.sources))))
            return FALSE;

        Dm.remove(ref actual_mutation.sources, sources);

        if (Dm.@in(MUTATION_SOURCE_ACTIVATED, (DmList)sources))
            set_se(0, actual_mutation);

        // Check that it exists first before trying to remove it with mutadone
        if (!Dm.truthy(length(actual_mutation.sources)))
        {
            // SEND_SIGNAL(holder, COMSIG_CARBON_LOSE_MUTATION, actual_mutation.type) (rules 11.1 (e), a statement)
            if (Dm.truthy(holder._listen_lookup?[COMSIG_CARBON_LOSE_MUTATION]))
                holder._SendSignal(COMSIG_CARBON_LOSE_MUTATION, Dm.list(holder, actual_mutation.type));
            await actual_mutation.on_losing(holder as mob_living_carbon_human);
            qdel(actual_mutation);
        }

        await update_instability(FALSE);
        return TRUE;
    }

    // tg: code/datums/dna/dna.dm:172
    public virtual object check_mutation(object mutation_type = null)
    {
        return get_mutation(mutation_type);
    }

    // tg: code/datums/dna/dna.dm:175
    public virtual async DmTask remove_all_mutations(object sources = null, params object[] __extra)
    {
        await remove_mutation_group(mutations, sources ?? GLOB.standard_mutation_sources);
        scrambled = FALSE;
    }

    // tg: code/datums/dna/dna.dm:179
    public virtual async DmTask remove_mutation_group(DmList group = null, object sources = null)
    {
        if (group == null)
            return;
        foreach (object mutation in Dm.each(group))
            await remove_mutation(mutation, sources ?? GLOB.standard_mutation_sources);
    }

    // tg: code/datums/dna/dna.dm:185
    public virtual object generate_unique_identity()
    {
        string dot = "";
        foreach (object block_type in Dm.each(GLOB.dna_identity_blocks))
        {
            datum_dna_block_identity block = GLOB.dna_identity_blocks[block_type] as datum_dna_block_identity;
            dot += Dm.str(block.unique_block(holder as mob_living_carbon_human));
        }
        return dot;
    }

    // tg: code/datums/dna/dna.dm:191
    public virtual object generate_unique_features()
    {
        string dot = "";
        foreach (object block_type in Dm.each(GLOB.dna_feature_blocks))
        {
            datum_dna_block_feature block = GLOB.dna_feature_blocks[block_type] as datum_dna_block_feature;
            if (features[block.feature_key] == null)
            {
                dot += Dm.str(random_string(block.block_length, GLOB.hex_characters));
                continue;
            }
            dot += Dm.str(block.unique_block(holder as mob_living_carbon_human));
        }
        return dot;
    }

    /**
     * Picks what mutations this DNA has innate and generates DNA blocks for them
     *
     * * mutation_blacklist - Optional list of mutation typepaths to exclude from generation.
     */
    // tg: code/datums/dna/dna.dm:205
    public virtual void generate_dna_blocks(DmList mutation_blacklist = null)
    {
        DmList mutations_temp = Dm.add(Dm.add(Dm.add(Dm.list(), GLOB.good_mutations), GLOB.bad_mutations), GLOB.not_good_mutations) as DmList;
        datum_species current_species = species as datum_species;
        if (Dm.truthy(current_species?.inert_mutation))
            Dm.combine(ref mutations_temp, GET_INITIALIZED_MUTATION(current_species.inert_mutation));
        foreach (object mutation_type in Dm.each(mutation_blacklist))
            Dm.remove(ref mutations_temp, GET_INITIALIZED_MUTATION(mutation_type));
        if (!Dm.truthy(length(mutations_temp)))
            return;
        mutation_index.Cut();
        default_mutation_genes.Cut();
        shuffle_inplace(mutations_temp);
        mutation_index[typeof(datum_mutation_race)] = create_sequence(typeof(datum_mutation_race), FALSE);
        default_mutation_genes[typeof(datum_mutation_race)] = mutation_index[typeof(datum_mutation_race)];
        for (int i = 2; i <= DNA_MUTATION_BLOCKS; i++)
        {
            datum_mutation M = mutations_temp[i] as datum_mutation;
            mutation_index[M.type] = create_sequence(M.type, FALSE, M.difficulty);
            default_mutation_genes[M.type] = mutation_index[M.type];
        }
        shuffle_inplace(mutation_index);
    }

    // tg: code/datums/dna/dna.dm:249
    public virtual object generate_unique_enzymes()
    {
        string dot = "";
        if (holder is mob_living)
        {
            real_name = holder.real_name;
            dot += md5(holder.real_name);
        }
        else
            dot += Dm.str(random_string(DNA_UNIQUE_ENZYMES_LEN, GLOB.hex_characters));
        return dot;
    }

    ///Setter macro used to modify unique features blocks.
    // tg: code/datums/dna/dna.dm:259
    public virtual void set_uni_feature_block(object blocknum = null, object input = null)
    {
        string precesing_blocks = copytext(unique_features, 1, Dm.num(GLOB.total_uf_len_by_block[blocknum]));
        string succeeding_blocks = Dm.num(blocknum) < length(GLOB.total_uf_len_by_block) ? copytext(unique_features, Dm.num(GLOB.total_uf_len_by_block[Dm.add(blocknum, 1)])) : "";
        unique_features = precesing_blocks + Dm.str(input) + succeeding_blocks;
    }

    // tg: code/datums/dna/dna.dm:264
    public virtual void update_ui_block(object blocktype = null)
    {
        if (blocktype == null)
            throw CRASH("UI block type is null", "code/datums/dna/dna.dm", 266);
        if (!ishuman(holder))
            throw CRASH("Non-human mobs shouldn't have DNA", "code/datums/dna/dna.dm", 268);
        datum_dna_block_identity block = GLOB.dna_identity_blocks[blocktype] as datum_dna_block_identity;
        unique_identity = block.modified_hash(unique_identity, block.unique_block(holder as mob_living_carbon_human));
    }

    // tg: code/datums/dna/dna.dm:272
    public virtual void update_uf_block(object blocktype = null)
    {
        if (!Dm.truthy(blocktype))
            throw CRASH("UF block type is null", "code/datums/dna/dna.dm", 274);
        if (!ishuman(holder))
            throw CRASH("Non-human mobs shouldn't have DNA", "code/datums/dna/dna.dm", 276);
        datum_dna_block_feature block = GLOB.dna_feature_blocks[blocktype] as datum_dna_block_feature;
        unique_features = block.modified_hash(unique_features, block.unique_block(holder as mob_living_carbon_human));
    }

    /**
     * Checks if two DNAs are practically the same by comparing their most defining features
     *
     * Arguments:
     * * target_dna The DNA that we are comparing to
     */
    // tg: code/datums/dna/dna.dm:286
    public virtual object is_same_as(datum_dna target_dna = null)
    {
        if (Dm.eq(unique_identity, target_dna.unique_identity)
            && mutation_index == target_dna.mutation_index
            && Dm.eq(real_name, target_dna.real_name)
            && ((datum)species).type == ((datum)target_dna.species).type
            && Dm.truthy(compare_list(features, target_dna.features))
            && blood_type.type == target_dna.blood_type.type)
            return TRUE;

        return FALSE;
    }

    // tg: code/datums/dna/dna.dm:299
    public virtual async DmTask update_instability(float? alert = null)
    {
        alert ??= TRUE;
        float old_stability = stability;
        stability = 100;
        foreach (datum_mutation mutation in Dm.each<datum_mutation>(mutations))
        {
            if (Dm.@in(MUTATION_SOURCE_MUTATOR, mutation.sources) || mutation.instability < 0)
                stability -= mutation.instability * (Dm.num(mutation.stabilizer_coeff) < 0 ? 1 : Dm.num(mutation.stabilizer_coeff)); // GET_MUTATION_STABILIZER
        }
        if (holder != null)
        {
            string message = null;
            if (Dm.truthy(alert))
            {
                if (stability >= 70 && stability <= 90)
                    message = span_warning(I18n.T("You shiver."));
                else if (stability >= 60 && stability <= 69)
                    message = span_warning(I18n.T("You feel cold."));
                else if (stability >= 40 && stability <= 59)
                    message = span_warning(I18n.T("You feel sick."));
                else if (stability >= 20 && stability <= 39)
                    message = span_warning(I18n.T("It feels like your skin is moving."));
                else if (stability >= 1 && stability <= 19)
                    message = span_warning(I18n.T("You can feel your cells burning."));
                else if (stability <= 0)
                    message = span_boldwarning(I18n.T("You can feel your DNA exploding, we need to do something fast!"));
            }
            if (stability <= 0)
                await holder.apply_status_effect(typeof(datum_status_effect_dna_melt));
            if (message != null && stability < old_stability)
                to_chat(holder, message);
        }
    }

    // tg: code/datums/dna/dna.dm:327
    public virtual void update_dna_identity()
    {
        if (!Dm.truthy(holder.has_dna()))
            return;
        unique_identity = generate_unique_identity();
        unique_enzymes = generate_unique_enzymes();
        unique_features = generate_unique_features();
    }

    // tg: code/datums/dna/dna.dm:341
    public virtual void initialize_dna(object newblood_type = null, float? create_mutation_blocks = null, float? randomize_features = null)
    {
        newblood_type ??= random_human_blood_type();
        create_mutation_blocks ??= TRUE;
        randomize_features ??= TRUE;
        if (Dm.truthy(newblood_type))
            blood_type = newblood_type as datum_blood_type;
        if (Dm.truthy(create_mutation_blocks)) //I hate this
            generate_dna_blocks(mutation_blacklist: Dm.list(typeof(datum_mutation_headless)));
        if (Dm.truthy(randomize_features))
        {
            foreach (object species_type in Dm.each(GLOB.species_prototypes))
            {
                DmList new_features = ((datum_species)GLOB.species_prototypes[species_type]).randomize_features() as DmList;
                foreach (object feature in Dm.each(new_features))
                    features[feature] = new_features[feature];
            }

            features[FEATURE_MUTANT_COLOR] = Dm.text("#[]", random_color());
        }

        update_dna_identity();
    }

    // tg: code/datums/dna/dna.dm:516
    public virtual async DmTask check_block(object mutation_path = null)
    {
        datum_mutation mutation = get_mutation(mutation_path) as datum_mutation;
        if (Dm.truthy(check_block_string(mutation_path)))
        {
            if (mutation == null)
                await add_mutation(mutation_path, MUTATION_SOURCE_ACTIVATED);
            return;
        }
        if (Dm.@in(MUTATION_SOURCE_ACTIVATED, mutation?.sources))
            await remove_mutation(mutation, MUTATION_SOURCE_ACTIVATED);
    }

    // tg: code/datums/dna/dna.dm:526
    public virtual object get_mutation(object mutation_path = null)
    {
        if (mutation_path is datum_mutation mutation)
            mutation_path = mutation.type;
        foreach (datum_mutation each in Dm.each_cast<datum_mutation>(mutations))
        {
            if (each.type == mutation_path as Type)
                return each;
        }
        return null;
    }

    // tg: code/datums/dna/dna.dm:535
    public virtual object check_block_string(object mutation = null)
    {
        if ((LAZYLEN(mutation_index) > DNA_MUTATION_BLOCKS) || !Dm.@in(mutation, mutation_index))
            return FALSE;
        return is_gene_active(mutation);
    }

    // tg: code/datums/dna/dna.dm:540
    public virtual object is_gene_active(object mutation = null)
    {
        return Dm.eq(mutation_index[mutation], GET_SEQUENCE(mutation)) ? TRUE : FALSE;
    }

    // tg: code/datums/dna/dna.dm:543
    public virtual object set_se(float? on = null, datum_mutation HM = null)
    {
        on ??= TRUE;
        if (HM == null || !Dm.@in(HM.type, mutation_index) || (LAZYLEN(mutation_index) < DNA_MUTATION_BLOCKS))
            return null;
        object dot = TRUE;
        if (Dm.truthy(on))
        {
            mutation_index[HM.type] = GET_SEQUENCE(HM.type);
            default_mutation_genes[HM.type] = mutation_index[HM.type];
        }
        else if (Dm.eq(GET_SEQUENCE(HM.type), mutation_index[HM.type]))
        {
            mutation_index[HM.type] = create_sequence(HM.type, FALSE, HM.difficulty);
            default_mutation_genes[HM.type] = mutation_index[HM.type];
        }
        return dot;
    }

    // tg: code/datums/dna/dna.dm:555  (syndicate G4a, D316: the DNA injectors)
    public virtual async DmTask<object> activate_mutation(object mutation = null) //note that this returns a boolean and not a new mob
    {
        if (!Dm.truthy(mutation))
            return FALSE;
        object mutation_type = mutation;
        if (mutation is datum_mutation instance)
            mutation_type = instance.type;
        if (!Dm.truthy(mutation_in_sequence(mutation_type))) //can't activate what we don't have, use add_mutation
            return FALSE;
        await add_mutation(mutation, MUTATION_SOURCE_ACTIVATED);
        return TRUE;
    }

    /////////////////////////// DNA HELPER-PROCS //////////////////////////////

    // tg: code/datums/dna/dna.dm:569  (syndicate G4a, D316: the DNA injectors)
    public virtual object mutation_in_sequence(object mutation = null)
    {
        if (!Dm.truthy(mutation))
            return null;
        if (mutation is datum_mutation HM)
        {
            if (Dm.@in(HM.type, mutation_index))
                return TRUE;
        }
        else if (Dm.@in(mutation, mutation_index))
            return TRUE;
        return null;
    }
}

//subtype used by brain mob's stored_dna and the crew manifest
// tg: code/datums/dna/dna.dm:356  (M6 slice 5: a gibbed player's brain mob; the type is generated)
public partial class datum_dna_stored : datum_dna
{
    //no mutation changes on stored dna.
    // tg: code/datums/dna/dna.dm:358
    public override async DmTask add_mutation(object mutation_to_add = null, object sources = null)
    {
        return;
    }

    // tg: code/datums/dna/dna.dm:361
    public override async DmTask<object> remove_mutation(object mutation_to_remove = null, object sources = null)
    {
        return null;
    }

    // tg: code/datums/dna/dna.dm:364
    public override object check_mutation(object mutation_type = null)
    {
        return null;
    }

    // tg: code/datums/dna/dna.dm:367
    public override async DmTask remove_all_mutations(object sources = null, params object[] __extra)
    {
        return;
    }

    // tg: code/datums/dna/dna.dm:370
    public override async DmTask remove_mutation_group(DmList group = null, object sources = null)
    {
        return;
    }
}

// tg: code/datums/dna/dna.dm:375
public partial class mob : atom_movable
{
    // tg: code/datums/dna/dna.dm:375
    [ShouldNotSleep]
    public virtual void set_species(object mrace = null, float? icon_update = null, params object[] __extra)
    {
        icon_update ??= 1;
        return;
    }
}

// tg: code/datums/dna/dna.dm:379 (interleaved types, rules 1.9; M6 slice 5)
public partial class mob_living_brain : mob_living
{
    // tg: code/datums/dna/dna.dm:379
    public override void set_species(object mrace = null, float? icon_update = null, params object[] __extra)
    {
        icon_update ??= 1;
        if (Dm.truthy(mrace))
        {
            if (ispath(mrace))
                stored_dna.species = Dm.@new((Type)mrace);
            else
                stored_dna.species = mrace; //not calling any species update procs since we're a brain, not a monkey/human
        }
    }
}

// tg: code/datums/dna/dna.dm:387 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/datums/dna/dna.dm:387
    [DmParams("mrace", "icon_update", "pref_load", "replace_missing")]
    public override void set_species(object mrace = null, float? icon_update = null, params object[] __extra)
    {
        float? pref_load = Dm.arg<float?>(__extra, 0);
        float? replace_missing = Dm.arg<float?>(__extra, 1);
        icon_update ??= TRUE;
        pref_load ??= FALSE;
        replace_missing ??= TRUE;
        if (QDELETED(this))
            throw Dm.CRASH("You're trying to change your species post deletion, this is a recipe for madness", "code/datums/dna/dna.dm", 389);
        if (mrace == null)
            throw Dm.CRASH("set_species called without a species to set to", "code/datums/dna/dna.dm", 391);
        if (!Dm.truthy(has_dna()))
            return;

        datum_species new_race = null;
        if (ispath(mrace))
            new_race = Dm.@new(mrace as Type) as datum_species;
        else if (mrace is datum_species)
        {
            if (QDELING((datum_species)mrace))
                throw Dm.CRASH("someone is calling set_species() and is passing it a qdeling species datum, this is VERY bad, stop it", "code/datums/dna/dna.dm", 400);
            new_race = mrace as datum_species;
        }
        else
            throw Dm.CRASH(Dm.text("set_species called with an invalid mrace []", mrace), "code/datums/dna/dna.dm", 403);

        datum_species old_species = dna.species as datum_species;
        dna.species = new_race;

        if (Dm.truthy(old_species.properly_gained))
            Waiting.NoSleep(old_species.on_species_loss(this as mob_living_carbon_human, new_race, pref_load));

        Waiting.NoSleep(((datum_species)dna.species).on_species_gain(this as mob_living_carbon_human, old_species, pref_load, icon_update, replace_missing));
        log_mob_tag(Dm.text("TAG: [] SPECIES: [] \\[[]\\]", tag, key_name(this), mrace));
    }
}

// tg: code/datums/dna/dna.dm:414 (interleaved types, rules 1.9)
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/datums/dna/dna.dm:414
    [DmParams("mrace", "icon_update", "pref_load", "replace_missing")]
    public override void set_species(object mrace = null, float? icon_update = null, params object[] __extra)
    {
        float? pref_load = Dm.arg<float?>(__extra, 0);
        float? replace_missing = Dm.arg<float?>(__extra, 1);
        icon_update ??= TRUE;
        pref_load ??= FALSE;
        replace_missing ??= TRUE;
        base.set_species(mrace, icon_update, Dm.forward(__extra, pref_load, replace_missing));
        if (Dm.truthy(icon_update))
        {
            Waiting.NoSleep(update_body(TRUE)); // update_body(is_creating = TRUE)
        }
    }
}

// tg: code/datums/dna/dna.dm:419 (interleaved types, rules 1.9)
public partial class mob : atom_movable
{
    // tg: code/datums/dna/dna.dm:419
    public virtual object has_dna()
    {
        return null;
    }
}

// tg: code/datums/dna/dna.dm:422 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/datums/dna/dna.dm:422
    public override object has_dna()
    {
        return dna;
    }
}

// tg: code/datums/dna/dna.dm:427 (interleaved types, rules 1.9)
public partial class mob : atom_movable
{

    // tg: code/datums/dna/dna.dm:427
    public virtual object can_mutate()
    {
        return FALSE;
    }

    // tg: code/datums/dna/dna.dm:504
    public virtual async DmTask domutcheck()
    {
        await DmTask.CompletedTask;
        return;
    }
}

// tg: code/datums/dna/dna.dm:430 (interleaved types, rules 1.9; syndicate G4a, D316: the DNA injectors)
public partial class mob_living_carbon : mob_living
{
    // tg: code/datums/dna/dna.dm:430
    public override object can_mutate()
    {
        if (!Dm.truthy(Dm.bitand(mob_biotypes, MOB_ORGANIC)))
            return FALSE;
        if (Dm.truthy(has_dna()) && !HAS_TRAIT(this, TRAIT_GENELESS) && !HAS_TRAIT(this, TRAIT_BADDNA))
            return TRUE;
        return null;
    }
}

// tg: code/datums/dna/dna.dm:437
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/datums/dna/dna.dm:437
    public virtual async DmTask hardset_dna(object unique_identity = null, DmList mutation_index = null, DmList default_mutation_genes = null, object newreal_name = null, object newblood_type = null, datum_species mrace = null, object newfeatures = null, DmList mutations = null, object force_transfer_mutations = null)
    {
        if (Dm.truthy(newfeatures))
        {
            dna.features = newfeatures as DmList;
            dna.generate_unique_features();
        }

        if (mrace != null)
        {
            datum_species newrace = Dm.@new(mrace.type) as datum_species;
            newrace.copy_properties_from(mrace);
            set_species(newrace, icon_update: 0);
        }

        if (Dm.truthy(newreal_name))
        {
            dna.real_name = newreal_name;
            dna.generate_unique_enzymes();
        }

        if (Dm.truthy(newblood_type))
            set_blood_type(newblood_type);

        if (Dm.truthy(unique_identity))
        {
            dna.unique_identity = unique_identity;
            await updateappearance(icon_update: 0);
        }

        if (Dm.truthy(LAZYLEN(mutation_index)))
        {
            dna.mutation_index = mutation_index.Copy();
            if (Dm.truthy(LAZYLEN(default_mutation_genes)))
                dna.default_mutation_genes = default_mutation_genes.Copy();
            else
                dna.default_mutation_genes = mutation_index.Copy();
            await domutcheck();
        }

        if (mrace != null || Dm.truthy(newfeatures) || Dm.truthy(unique_identity))
            await update_body(TRUE); // update_body(is_creating = TRUE)

        if (Dm.truthy(LAZYLEN(mutations)) && Dm.truthy(force_transfer_mutations) && Dm.truthy(can_mutate()))
        {
            foreach (datum_mutation mutation in Dm.each_cast<datum_mutation>(mutations))
            {
                object allowed_sources = Dm.bitand(mutation.sources, GLOB.standard_mutation_sources);
                if (Dm.truthy(allowed_sources))
                    await dna.add_mutation(mutation, allowed_sources);
            }
        }
    }

    // tg: code/datums/dna/dna.dm:683
    public virtual async DmTask something_horrible(object ignore_stability = null)
    {
        if (!Dm.truthy(has_dna())) //shouldn't ever happen anyway so it's just in really weird cases
            return;
        if (!Dm.truthy(ignore_stability) && (dna.stability > 0))
            return;
        float instability = -dna.stability;
        await dna.remove_all_mutations();
        dna.stability = 100;

        bool nonfatal = prob(Dm.max(70 - instability, 0));

        if (!Dm.truthy(length(datum_dna.nonfatal_meltdowns)))
        {
            foreach (Type meltdown_type in Dm.each_cast<Type>(typecacheof(typeof(datum_instability_meltdown), ignore_root_path: TRUE)))
            {
                if (Dm.initial(meltdown_type, (datum_instability_meltdown m) => m.abstract_type) == meltdown_type)
                    continue;

                if (Dm.truthy(Dm.initial(meltdown_type, (datum_instability_meltdown m) => m.fatal)))
                {
                    datum_dna.fatal_meltdowns[meltdown_type] = Dm.initial(meltdown_type, (datum_instability_meltdown m) => m.meltdown_weight);
                    continue;
                }

                datum_dna.nonfatal_meltdowns[meltdown_type] = Dm.initial(meltdown_type, (datum_instability_meltdown m) => m.meltdown_weight);
            }
        }

        object picked_type = pick_weight(nonfatal ? datum_dna.nonfatal_meltdowns : datum_dna.fatal_meltdowns);
        datum_instability_meltdown meltdown = Dm.@new(picked_type as Type) as datum_instability_meltdown;
        await meltdown.meltdown(this);
    }

    // tg: code/datums/dna/dna.dm:709
    public virtual async DmTask something_horrible_mindmelt()
    {
        if (!Dm.truthy(is_blind()))
        {
            obj_item_organ_eyes eyes = Dm.locate_type(typeof(obj_item_organ_eyes), organs) as obj_item_organ_eyes;
            if (eyes == null)
                return;
            await eyes.Remove(this);
            qdel(eyes);
            visible_message(span_notice(I18n.T("[] looks up and their eyes melt away!", this)), span_userdanger(I18n.T("I understand now.")));
            _addtimer(Dm.@new<datum_callback>(this, nameof(adjust_organ_loss), ORGAN_SLOT_BRAIN, 200), 2 * SECONDS, file: "code/datums/dna/dna.dm", line: 717);
        }
    }

    //proc used to update the mob's appearance after its dna UI has been changed
    // tg: code/datums/dna/dna.dm:485
    public override async DmTask updateappearance(float? icon_update = null, float? mutcolor_update = null, float? mutations_overlay_update = null)
    {
        icon_update ??= TRUE;
        mutcolor_update ??= FALSE;
        mutations_overlay_update ??= FALSE;
        await base.updateappearance(icon_update, mutcolor_update, mutations_overlay_update);
        foreach (object block_type in Dm.each(GLOB.dna_identity_blocks))
        {
            datum_dna_block_identity block_to_apply = GLOB.dna_identity_blocks[block_type] as datum_dna_block_identity;
            await block_to_apply.apply_to_mob(this, dna.unique_identity);
        }

        foreach (object block_type in Dm.each(GLOB.dna_feature_blocks))
        {
            datum_dna_block_feature block_to_apply = GLOB.dna_feature_blocks[block_type] as datum_dna_block_feature;
            if (Dm.truthy(dna.features[block_to_apply.feature_key]))
                await block_to_apply.apply_to_mob(this, dna.unique_features);
        }

        foreach (obj_item_organ organ in Dm.each<obj_item_organ>(organs))
            organ.mutate_feature(dna.unique_features, this);

        if (Dm.truthy(icon_update))
            await update_body(mutcolor_update); // update_body(is_creating = mutcolor_update)
        if (Dm.truthy(mutations_overlay_update))
            update_appearance(UPDATE_OVERLAYS);
    }
}

// tg: code/datums/dna/dna.dm:475
public partial class mob_living_carbon : mob_living
{
    // tg: code/datums/dna/dna.dm:475
    public virtual void create_dna(object species = null)
    {
        dna = Dm.@new<datum_dna>(this, species);
    }

    // tg: code/datums/dna/dna.dm:481
    public virtual async DmTask updateappearance(float? icon_update = null, float? mutcolor_update = null, float? mutations_overlay_update = null)
    {
        icon_update ??= TRUE;
        mutcolor_update ??= FALSE;
        mutations_overlay_update ??= FALSE;
        if (!Dm.truthy(has_dna()))
            return;
    }

    // tg: code/datums/dna/dna.dm:505
    public override async DmTask domutcheck()
    {
        if (!Dm.truthy(has_dna()))
            return;

        foreach (object mutation in Dm.each(dna.mutation_index))
            await dna.check_block(mutation);

        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/datums/dna/dna.dm:580
    public virtual async DmTask<object> random_mutate(DmList candidates = null)
    {
        if (!Dm.truthy(has_dna()))
            throw CRASH(Dm.text("[] does not have DNA", this), "code/datums/dna/dna.dm", 582);
        object mutation = pick(candidates);
        await dna.add_mutation(mutation, MUTATION_SOURCE_MUTATOR);
        return null;
    }

    ///Returns a random mutation typepath based on the given arguments. By default, all available mutations in the dna sequence but the monkey one.
    // tg: code/datums/dna/dna.dm:587
    public virtual object get_random_mutation_path(float? quality = null, float? scrambled = null, float? sequence = null, DmList excluded_mutations = null)
    {
        quality ??= POSITIVE | NEGATIVE | MINOR_NEGATIVE;
        scrambled ??= TRUE;
        sequence ??= TRUE;
        excluded_mutations ??= Dm.list(typeof(datum_mutation_race));
        if (!Dm.truthy(has_dna()))
            return null;
        DmList mutations = Dm.list();
        if (Dm.truthy(Dm.bitand(quality, POSITIVE)))
            Dm.append(ref mutations, GLOB.good_mutations);
        if (Dm.truthy(Dm.bitand(quality, NEGATIVE)))
            Dm.append(ref mutations, GLOB.bad_mutations);
        if (Dm.truthy(Dm.bitand(quality, MINOR_NEGATIVE)))
            Dm.append(ref mutations, GLOB.not_good_mutations);
        DmList possible = Dm.list();
        foreach (datum_mutation mutation in Dm.each<datum_mutation>(mutations))
        {
            if ((!Dm.truthy(sequence) || Dm.truthy(dna.mutation_in_sequence(mutation.type))) && dna.get_mutation(mutation.type) == null)
                Dm.append(ref possible, mutation.type);
        }
        Dm.remove(ref possible, excluded_mutations);
        return Dm.truthy(length(possible)) ? pick(possible) : null; //prevent runtimes from picking null
    }

    ///Gives the mob a random mutation based on the given arguments.
    // tg: code/datums/dna/dna.dm:605
    public virtual async DmTask easy_random_mutate(float? quality = null, float? scrambled = null, float? sequence = null, DmList excluded_mutations = null)
    {
        quality ??= POSITIVE | NEGATIVE | MINOR_NEGATIVE;
        scrambled ??= TRUE;
        sequence ??= TRUE;
        excluded_mutations ??= Dm.list(typeof(datum_mutation_race));
        object mutation_path = get_random_mutation_path(quality, scrambled, sequence, excluded_mutations);
        if (!Dm.truthy(mutation_path))
            return;
        await dna.add_mutation(mutation_path, MUTATION_SOURCE_ACTIVATED);
        if (!Dm.truthy(scrambled))
            return;
        datum_mutation mutation = dna.get_mutation(mutation_path) as datum_mutation;
        if (mutation != null)
            mutation.scrambled = FALSE; //set to FALSE to allow easy_random_mutate obtained genes to be saved in DNA consoles
    }

    // tg: code/datums/dna/dna.dm:616
    public virtual async DmTask random_mutate_unique_identity()
    {
        if (!Dm.truthy(has_dna()))
            throw CRASH(Dm.text("[] does not have DNA", this), "code/datums/dna/dna.dm", 618);
        object mutblock_path = pick(GLOB.dna_identity_blocks);
        datum_dna_block_identity mutblock = GLOB.dna_identity_blocks[mutblock_path] as datum_dna_block_identity;
        dna.unique_identity = mutblock.modified_hash(dna.unique_identity, random_string(mutblock.block_length, GLOB.hex_characters));
        await updateappearance(mutations_overlay_update: TRUE);
    }

    // tg: code/datums/dna/dna.dm:624
    public virtual async DmTask random_mutate_unique_features()
    {
        if (!Dm.truthy(has_dna()))
            throw CRASH(Dm.text("[] does not have DNA", this), "code/datums/dna/dna.dm", 626);
        object mutblock_path = pick(GLOB.dna_feature_blocks);
        datum_dna_block_feature mutblock = GLOB.dna_feature_blocks[mutblock_path] as datum_dna_block_feature;
        dna.unique_features = mutblock.modified_hash(dna.unique_features, random_string(mutblock.block_length, GLOB.hex_characters));
        await updateappearance(mutcolor_update: TRUE, mutations_overlay_update: TRUE);
    }

    // tg: code/datums/dna/dna.dm:632
    public virtual async DmTask clean_dna()
    {
        if (!Dm.truthy(has_dna()))
            throw CRASH(Dm.text("[] does not have DNA", this), "code/datums/dna/dna.dm", 634);
        await dna.remove_all_mutations();
    }

    // tg: code/datums/dna/dna.dm:637
    public virtual async DmTask clean_random_mutate(DmList candidates = null, float? difficulty = null)
    {
        difficulty ??= 2;
        await clean_dna();
        await random_mutate(candidates); // tg passes difficulty too, which random_mutate does not take
    }
}

// tg: code/datums/dna/dna.dm:5
public partial class datum_controller_global_vars : datum_controller
{
    /**
     * A list of numbers that keeps track of where ui blocks start in the unique_identity string variable of the dna datum.
     * Commonly used by the datum/dna/set_uni_identity_block and datum/dna/get_uni_identity_block procs.
     */
    // tg: code/datums/dna/dna.dm:5  GLOBAL_LIST_INIT(total_ui_len_by_block, populate_total_ui_len_by_block())
    public DmList total_ui_len_by_block;

    // tg: code/datums/dna/dna.dm:5
    public virtual void InitGlobaltotal_ui_len_by_block()
    {
        total_ui_len_by_block = populate_total_ui_len_by_block() as DmList;
        Dm.append(ref gvars_datum_init_order, "total_ui_len_by_block");
    }

    // tg: code/datums/dna/dna.dm:18  GLOBAL_LIST_INIT(total_uf_len_by_block, populate_total_uf_len_by_block())
    public DmList total_uf_len_by_block;

    // tg: code/datums/dna/dna.dm:18
    public virtual void InitGlobaltotal_uf_len_by_block()
    {
        total_uf_len_by_block = populate_total_uf_len_by_block() as DmList;
        Dm.append(ref gvars_datum_init_order, "total_uf_len_by_block");
    }

    // tg: code/datums/dna/dna.dm:7  GLOBAL_LIST_INIT(standard_mutation_sources, list(...))
    public DmList standard_mutation_sources;

    // tg: code/datums/dna/dna.dm:7  (GLOBAL_MANAGED(standard_mutation_sources, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstandard_mutation_sources()
    {
        standard_mutation_sources = Dm.list(MUTATION_SOURCE_ACTIVATED, MUTATION_SOURCE_MUTATOR);
        Dm.append(ref gvars_datum_init_order, "standard_mutation_sources");
    }
}
