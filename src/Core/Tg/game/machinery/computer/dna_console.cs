// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/dna_console.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the DNA console: the scanner link, the genetic sequencer (gene pulses, discovery, the joker, the
// chromosomes, CRISPR), the mutation storage of the console, the disk and the advanced injectors, combining, the injectors it
// prints, the genetic makeup buffers, their injectors, the delayed transfer and the enzyme pulse, and the DnaConsole window's data.
// CRISPR's random viruses came with the diseases (gaps VI1). The var block is generated (D111).

// tg: code/game/machinery/computer/dna_console.dm:75
public partial class obj_machinery_computer_dna_console : obj_machinery_computer
{
    // The file-local defines (dna_console.dm:1-73, #undef at :2380-2422).
    /// Base timeout for creating mutation activators
    private const float MIN_ACTIVATOR_TIMEOUT = 5 * SECONDS;
    /// Base cooldown multiplier for activator upgrades
    private const float ACTIVATOR_COOLDOWN_MULTIPLIER = 0.25f;
    /// Base timeout for creating mutation injectors
    private const float MIN_INJECTOR_TIMEOUT = 10 * SECONDS;
    /// Base cooldown multiplier for injector upgrades
    private const float INJECTOR_COOLDOWN_MULTIPLIER = 0.15f;
    /// Base timeout for creating advanced injectors
    private const float MIN_ADVANCED_TIMEOUT = 15 * SECONDS;
    /// Base cooldown multiplier for advanced injector upgrades
    private const float ADVANCED_COOLDOWN_MULTIPLIER = 0.1f;
    /// Hard cap for advanced injector cooldown
    private const float ADVANCED_INJECTOR_MAX_COOLDOWN = 90 * SECONDS;
    /// Used for other things like UI/UE/Initial CD
    private const float MISC_INJECTOR_TIMEOUT = 60 * SECONDS;
    /// Maximum number of genetic makeup storage slots in DNA Console
    private const int NUMBER_OF_BUFFERS = 3;
    /// Timeout for DNA Scramble in DNA Consoles
    private const float SCRAMBLE_TIMEOUT = 600;
    /// Timeout for using the Joker feature to solve a gene in DNA Console
    private const float JOKER_TIMEOUT = 12000;
    /// How much time DNA Scanner upgrade tiers remove from JOKER_TIMEOUT
    private const float JOKER_UPGRADE = 3000;
    /// Maximum value for genetic damage strength when pulsing enzymes
    private const float GENETIC_DAMAGE_STRENGTH_MAX = 15;
    /// Larger multipliers will affect the range of values when pulsing enzymes
    private const float GENETIC_DAMAGE_STRENGTH_MULTIPLIER = 1;
    /// Maximum value for the genetic damage pulse duration when pulsing enzymes
    private const float GENETIC_DAMAGE_DURATION_MAX = 30;
    /// Large values reduce pulse accuracy and may pulse other enzymes than selected
    private const float GENETIC_DAMAGE_ACCURACY_MULTIPLIER = 3;
    /// Special status indicating a scanner occupant is transforming eg. from monkey to human
    private const int STATUS_TRANSFORMING = 5;
    /// Multiplier for how much genetic damage received from DNA Console functionality
    private const float GENETIC_DAMAGE_IRGENETIC_DAMAGE_MULTIPLIER = 1;
    /// Flag for the mutation ref search system. Search will include scanner occupant
    private const int SEARCH_OCCUPANT = 1;
    /// Flag for the mutation ref search system. Search will include console storage
    private const int SEARCH_STORED = 2;
    /// Flag for the mutation ref search system. Search will include diskette storage
    private const int SEARCH_DISKETTE = 4;
    /// Flag for the mutation ref search system. Search will include advanced injector mutations
    private const int SEARCH_ADV_INJ = 8;
    /// The base cooldown of the ability to copy enzymes and genetic makeup to people.
    private const float ENZYME_COPY_BASE_COOLDOWN = 60 * SECONDS;
    private const string GENETIC_DAMAGE_PULSE_UNIQUE_IDENTITY = "ui";
    private const string GENETIC_DAMAGE_PULSE_UNIQUE_FEATURES = "uf";
    /// Input from tgui interface. X the gene out.
    private const int CLEAR_GENE = 0;
    /// Input from tgui interface. Progress to the next gene.
    private const int NEXT_GENE = 1;
    /// Input from tgui interface. Progress to previous gene.
    private const int PREV_GENE = 2;
    ///This mutation has been activated by an activator or by completing the sequence
    private const int SCANNER_MUTATION_CLASS_ACTIVATOR = 1;
    ///This mutation has been added via mutator or random mutations (eg. mutagen)
    private const int SCANNER_MUTATION_CLASS_MUTATOR = 2;
    ///This mutation has been added by other means that cannot be undone by the console (or mutadone)
    private const int SCANNER_MUTATION_CLASS_OTHER = 3;
    // tg: code/game/machinery/computer/dna_console.dm:1785-1793 (#undef at :1849-1853)
    /// Copy UI to the dna injector
    private const int DNA_INJECTOR_FLAG_UI = 1 << 0;
    /// Copy UE to the dna injector
    private const int DNA_INJECTOR_FLAG_UE = 1 << 1;
    /// Copy UF to the dna injector
    private const int DNA_INJECTOR_FLAG_UF = 1 << 2;
    /// Copy name to the dna injector
    private const int DNA_INJECTOR_FLAG_NAME = 1 << 3;
    /// Copy blood type to the dna injector
    private const int DNA_INJECTOR_FLAG_BLOOD = 1 << 4;

    // tg: code/game/machinery/computer/dna_console.dm:176
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.process(seconds_per_tick, __extra);

        // This is for pulsing the UI element with genetic damage as part of genetic makeup
        // If genetic_damage_pulse_index > 0 then it means we're attempting a pulse
        if ((genetic_damage_pulse_index > 0) && (genetic_damage_pulse_timer <= world.time) && (Dm.eq(genetic_damage_pulse_type, GENETIC_DAMAGE_PULSE_UNIQUE_IDENTITY) || Dm.eq(genetic_damage_pulse_type, GENETIC_DAMAGE_PULSE_UNIQUE_FEATURES)))
        {
            Waiting.NoSleep(genetic_damage_pulse());
            return dot;
        }
        return dot;
    }

    // tg: code/game/machinery/computer/dna_console.dm:185
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // Store chromosomes in the console if there's room
        if (istype(tool, typeof(obj_item_chromosome)))
        {
            await tool.forceMove(this);
            Dm.append(ref stored_chromosomes, tool);
            to_chat(user, span_notice(I18n.T("You insert [].", tool)));
            return ITEM_INTERACT_SUCCESS;
        }

        // Insert data disk if console disk slot is empty
        // Swap data disk if there is one already a disk in the console
        if (istype(tool, typeof(obj_item_disk_data))) //INSERT SOME DISKETTES
        {
            // Insert disk into DNA Console
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            // If insertion was successful and there's already a diskette in the console, eject the old one.
            if (diskette != null)
                await eject_disk(user);
            // Set the new diskette.
            diskette = tool as obj_item_disk_data;
            to_chat(user, span_notice(I18n.T("You insert [].", tool)));
            return ITEM_INTERACT_SUCCESS;
        }

        // Recycle non-activator used injectors
        // Turn activator used injectors (aka research injectors) to chromosomes
        if (istype(tool, typeof(obj_item_dnainjector_activator)))
        {
            obj_item_dnainjector_activator activator = tool as obj_item_dnainjector_activator;
            if (!Dm.truthy(activator.used))
            {
                //recycle unused activators
                qdel(tool);
                to_chat(user, span_notice(I18n.T("Recycled unused [].", tool)));
                return ITEM_INTERACT_SUCCESS;
            }
            if (Dm.truthy(activator.research) && Dm.truthy(activator.filled))
            {
                if (prob(60))
                {
                    object c_typepath = generate_chromosome();
                    obj_item_chromosome CM = Dm.@new(c_typepath as Type, this) as obj_item_chromosome;
                    Dm.append(ref stored_chromosomes, CM);
                    to_chat(user, span_notice(I18n.T("[] added to storage.", capitalize(CM.name))));
                }
                else
                    to_chat(user, span_notice(I18n.T("There was not enough genetic data to extract a viable chromosome.")));
            }
            if (Dm.truthy(activator.crispr_charge))
            {
                crispr_charges++;
                to_chat(user, span_notice(I18n.T("CRISPR charge added.")));
            }
            qdel(tool);
            to_chat(user, span_notice(I18n.T("Recycled [].", tool)));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/machinery/computer/dna_console.dm:231
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        obj_item_multitool multitool = tool as obj_item_multitool;
        if (!QDELETED(multitool?.buffer) && istype(multitool.buffer, typeof(datum_techweb)))
            stored_research = (datum_techweb)multitool.buffer;
        return TRUE;
    }

    // tg: code/game/machinery/computer/dna_console.dm:236
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await eject_disk(user);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/machinery/computer/dna_console.dm:240
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        // Connect with a nearby DNA Scanner on init
        connect_to_scanner();

        // Set appropriate ready timers and limits for machines functions
        injector_ready = world.time + MISC_INJECTOR_TIMEOUT;
        scramble_ready = world.time + SCRAMBLE_TIMEOUT;
        joker_ready = world.time + JOKER_TIMEOUT;
        enzyme_copy_timer = world.time + ENZYME_COPY_BASE_COOLDOWN; // COOLDOWN_START, inline (rules 11.1 (a))

        // Set the default tgui state
        set_default_state();
        return dot;
    }

    // tg: code/game/machinery/computer/dna_console.dm:255
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        // Link machine with research techweb. Used for discovering and accessing
        // already discovered mutations
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_default_techweb_link))) && stored_research == null) /* CONFIG_GET(flag/no_default_techweb_link) */
            CONNECT_TO_RND_SERVER_ROUNDSTART(ref stored_research, this);
    }

    // tg: code/game/machinery/computer/dna_console.dm:263
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        // Most of ui_interact is spent setting variables for passing to the tgui
        //  interface.
        // We can also do some general state processing here too as it's a good
        //  indication that a player is using the console.

        object scanner_op = scanner_operational();
        bool can_modify_occ = can_modify_occupant();

        // Check for connected AND operational scanner.
        if (Dm.truthy(scanner_op))
            can_use_scanner = TRUE;
        else
        {
            can_use_scanner = FALSE;
            set_connected_scanner(null);
            is_viable_occupant = FALSE;
        }

        // Check for a viable occupant in the scanner.
        if (can_modify_occ)
            is_viable_occupant = TRUE;
        else
            is_viable_occupant = FALSE;

        // Populates various buffers for passing to tgui
        build_mutation_list(can_modify_occ);
        build_genetic_makeup_list();

        // Populate variables for passing to tgui interface
        is_scramble_ready = scramble_ready < world.time ? TRUE : FALSE;
        time_to_scramble = round((scramble_ready - world.time) / 10);

        is_joker_ready = joker_ready < world.time ? TRUE : FALSE;
        time_to_joker = round((joker_ready - world.time) / 10);

        is_injector_ready = injector_ready < world.time ? TRUE : FALSE;
        time_to_injector = round((injector_ready - world.time) / 10);

        is_pulsing = (genetic_damage_pulse_index > 0) && (genetic_damage_pulse_timer > world.time) ? TRUE : FALSE;
        time_to_pulse = round((genetic_damage_pulse_timer - world.time) / 10);

        is_crispr_ready = crispr_charges > 0 ? TRUE : FALSE;

        // Attempt to update tgui ui, open and update if needed.
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "DnaConsole");
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/dna_console.dm:316
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // . += get_asset_datum(/datum/asset/simple/genetics): the asset transport is the client's (DV44); the client draws the
        // sequencer's three gene pictures (dna_discovered.gif, dna_undiscovered.gif, dna_extra.gif) itself
        return await base.ui_assets(user);
    }

    // tg: code/game/machinery/computer/dna_console.dm:320
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        DmList storage = Dm.list();

        data["view"] = tgui_view_state;
        data["storage"] = storage;

        // This block of code generates the huge data structure passed to the tgui
        // interface for displaying all the various bits of console/scanner data
        // Should all be very self-explanatory
        data["isScannerConnected"] = can_use_scanner;
        if (Dm.truthy(can_use_scanner))
        {
            data["scannerOpen"] = connected_scanner.state_open;
            data["scannerLocked"] = connected_scanner.locked;
            data["pulseStrength"] = pulse_strength;
            data["pulseDuration"] = pulse_duration;
            data["stdDevStr"] = pulse_strength * GENETIC_DAMAGE_STRENGTH_MULTIPLIER;
            float accuracy = GENETIC_DAMAGE_ACCURACY_MULTIPLIER / (pulse_duration + Dm.pow(connected_scanner.precision_coeff, 2)); //hardcoded values from a z-table for a normal distribution
            if (accuracy >= 0 && accuracy <= 0.25f)
                data["stdDevAcc"] = ">95 %";
            else if (accuracy >= 0.25f && accuracy <= 0.5f)
                data["stdDevAcc"] = "68-95 %";
            else if (accuracy >= 0.5f && accuracy <= 0.75f)
                data["stdDevAcc"] = "55-68 %";
            else
                data["stdDevAcc"] = "<38 %";
        }

        data["isViableSubject"] = is_viable_occupant;
        if (Dm.truthy(is_viable_occupant))
        {
            data["subjectName"] = scanner_occupant.name;
            if (Dm.truthy(scanner_occupant.transformation_timer))
                data["subjectStatus"] = STATUS_TRANSFORMING;
            else
                data["subjectStatus"] = scanner_occupant.stat;
            data["subjectHealth"] = scanner_occupant.health;
            data["subjectEnzymes"] = scanner_occupant.dna.unique_enzymes;
            data["isMonkey"] = HAS_TRAIT(scanner_occupant, TRAIT_LESSER_HUMANOID) ? TRUE : FALSE;
            data["subjectUNI"] = scanner_occupant.dna.unique_identity;
            data["subjectUF"] = scanner_occupant.dna.unique_features;
            storage["occupant"] = tgui_occupant_mutations;

            datum_status_effect_genetic_damage genetic_damage = scanner_occupant.has_status_effect(typeof(datum_status_effect_genetic_damage)) as datum_status_effect_genetic_damage;
            data["subjectDamage"] = genetic_damage != null ? round((genetic_damage.total_damage / genetic_damage.minimum_before_tox_damage) * 100, 0.1f) : 0;
        }
        else
        {
            data["subjectName"] = null;
            data["subjectStatus"] = null;
            data["subjectHealth"] = null;
            data["subjectDamage"] = null;
            data["subjectEnzymes"] = null;
            storage["occupant"] = null;
        }

        data["hasDelayedAction"] = delayed_action != null ? TRUE : FALSE;
        data["isScrambleReady"] = is_scramble_ready;
        data["isJokerReady"] = is_joker_ready;
        data["isInjectorReady"] = is_injector_ready;
        data["isCrisprReady"] = is_crispr_ready;
        data["crisprCharges"] = crispr_charges;
        data["scrambleSeconds"] = time_to_scramble;
        data["jokerSeconds"] = time_to_joker;
        data["injectorSeconds"] = time_to_injector;
        data["isPulsing"] = is_pulsing;
        data["timeToPulse"] = time_to_pulse;
        data["geneticMakeupCooldown"] = max(0, enzyme_copy_timer - world.time) / 10; // COOLDOWN_TIMELEFT, inline (rules 11.1 (a))

        if (diskette != null)
        {
            data["hasDisk"] = TRUE;
            data["diskCapacity"] = diskette.max_mutations - LAZYLEN(diskette.mutations);
            data["diskReadOnly"] = diskette.read_only;
            //data["diskMutations"] = tgui_diskette_mutations
            storage["disk"] = tgui_diskette_mutations;
            data["diskHasMakeup"] = LAZYLEN(diskette.genetic_makeup_buffer) > 0 ? TRUE : FALSE;
            data["diskMakeupBuffer"] = diskette.genetic_makeup_buffer.Copy();
        }
        else
        {
            data["hasDisk"] = FALSE;
            data["diskCapacity"] = 0;
            data["diskReadOnly"] = TRUE;
            //data["diskMutations"] = null
            storage["disk"] = null;
            data["diskHasMakeup"] = FALSE;
            data["diskMakeupBuffer"] = null;
        }

        //data["mutationStorage"] = tgui_console_mutations
        storage["console"] = tgui_console_mutations;
        data["chromoStorage"] = tgui_console_chromosomes;
        data["makeupCapacity"] = NUMBER_OF_BUFFERS;
        data["makeupStorage"] = tgui_genetic_makeup;

        //data["advInjectors"] = tgui_advinjector_mutations
        storage["injector"] = tgui_advinjector_mutations;
        data["maxAdvInjectors"] = max_injector_selections;

        data["heldScannerBuffer"] = null;
        foreach (obj_item_sequence_scanner scanner in Dm.each<obj_item_sequence_scanner>(user.held_items)) //We got one or more scanners in our hands, lets get the data from them.
        {
            if (!Dm.truthy(LAZYLEN(scanner.buffer)))
                continue;
            DmList scanner_data = Dm.list();
            foreach (object mutation_type in Dm.each(scanner.buffer))
            {
                datum_mutation mutation = GET_INITIALIZED_MUTATION(mutation_type) as datum_mutation;
                if (mutation != null)
                    scanner_data[mutation.alias] = scanner.buffer[mutation_type];
            }
            data["heldScannerBuffer"] = scanner_data;
        }

        return data;
    }

    // tg: code/game/machinery/computer/dna_console.dm:423  var/static/list/gene_letters = list("A", "T", "C", "G") (a proc-level
    // static, rules 10.6)
    private static DmList ui_act__gene_letters => TgState.Current.obj_machinery_computer_dna_console__ui_act__gene_letters ??= Dm.list("A", "T", "C", "G");

    // tg: code/game/machinery/computer/dna_console.dm:424  var/static/gene_letter_count = length(gene_letters)
    private static float ui_act__gene_letter_count => length(ui_act__gene_letters);

    // tg: code/game/machinery/computer/dna_console.dm:422
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        DmList gene_letters = ui_act__gene_letters;
        float gene_letter_count = ui_act__gene_letter_count;

        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        dot = TRUE;

        add_fingerprint(usr);

        object sw = action;
        // Connect this DNA Console to a nearby DNA Scanner
        // Usually only activate as an option if there is no connected scanner
        if (Dm.eq(sw, "connect_scanner"))
        {
            connect_to_scanner();
            return dot;
        }

        // Toggle the door open/closed status on attached DNA Scanner
        if (Dm.eq(sw, "toggle_door"))
        {
            // GUARD CHECK - Scanner still connected and operational?
            if (!Dm.truthy(scanner_operational()))
                return dot;

            await connected_scanner.toggle_open(usr);
            return dot;
        }

        // Toggle the door bolts on the attached DNA Scanner
        if (Dm.eq(sw, "toggle_lock"))
        {
            // GUARD CHECK - Scanner still connected and operational?
            if (!Dm.truthy(scanner_operational()))
                return dot;

            connected_scanner.locked = Dm.truthy(connected_scanner.locked) ? FALSE : TRUE;
            return dot;
        }

        // Scramble scanner occupant's DNA
        if (Dm.eq(sw, "scramble_dna"))
        {
            // GUARD CHECK - Can we genetically modify the occupant? Includes scanner
            //  operational guard checks.
            // GUARD CHECK - Is scramble DNA actually ready?
            if (!can_modify_occupant() || !(scramble_ready < world.time) || HAS_TRAIT(scanner_occupant, TRAIT_NO_DNA_SCRAMBLE))
                return dot;

            await scanner_occupant.dna.remove_all_mutations();
            scanner_occupant.dna.generate_dna_blocks();
            scramble_ready = world.time + SCRAMBLE_TIMEOUT;
            to_chat(usr, span_notice(I18n.T("DNA scrambled.")));
            await scanner_occupant.apply_status_effect(typeof(datum_status_effect_genetic_damage), GENETIC_DAMAGE_STRENGTH_MULTIPLIER * 50 / Dm.pow(connected_scanner.damage_coeff, 2));
            await use_scanner_or_own_energy();
            return dot;
        }

        // Check whether a specific mutation is eligible for discovery within the
        //  scanner occupant
        // This is additionally done when a mutation's tab is selected in the tgui
        //  interface. This is because some mutations, such as Monkified on monkeys,
        //  are infact completed by default but not yet discovered. Likewise, all
        //  mutations can have their sequence completed while Monkified is still an
        //  active mutation and thus won't immediately be discovered but could be
        //  discovered when Monkified is removed
        // ---------------------------------------------------------------------- //
        // params["alias"] - Alias of a mutation. The alias is the "hidden" name of
        //                   the mutation, for example "Mutation 5" or "Mutation 33"
        if (Dm.eq(sw, "check_discovery"))
        {
            // GUARD CHECK - Can we genetically modify the occupant? Includes scanner
            //  operational guard checks.
            if (!can_modify_occupant())
                return dot;

            // GUARD CHECK - Have we somehow cheekily swapped occupants? This is
            //  unexpected.
            if (scanner_occupant != connected_scanner.occupant)
                return dot;

            await check_discovery(@params["alias"]);
            return dot;
        }

        // Check all mutations of the occupant and check if any are discovered.
        // This is called when the Genetic Sequencer is selected. It'll do things
        //  like immediately discover Monkified without needing to click through
        //  the mutation tabs and handle cases where mutations are solved but not
        //  discovered due to the Monkified mutation being active then removed.
        if (Dm.eq(sw, "all_check_discovery"))
        {
            // GUARD CHECK - Can we genetically modify the occupant? Includes scanner
            //  operational guard checks.
            if (!can_modify_occupant())
                return dot;

            // GUARD CHECK - Have we somehow cheekily swapped occupants? This is
            //  unexpected.
            if (scanner_occupant != connected_scanner.occupant)
                return dot;

            // Go over all standard mutations and check if they've been discovered.
            foreach (object mutation_type in Dm.each(scanner_occupant.dna.mutation_index))
            {
                datum_mutation HM = GET_INITIALIZED_MUTATION(mutation_type) as datum_mutation;
                await check_discovery(HM.alias);
            }

            return dot;
        }

        // Set a gene in a mutation's genetic sequence. Will also check for mutations
        //  discovery as part of the process.
        // ---------------------------------------------------------------------- //
        // params["alias"] - Alias of a mutation. The alias is the "hidden" name of
        //  the mutation, for example "Mutation 5" or "Mutation 33"
        // params["pulseAction"] - The action to perform on this gene.
        // params["pos"] - The BYOND index of the letter in the gene sequence to be
        //  changed.
        if (Dm.eq(sw, "pulse_gene"))
        {
            // GUARD CHECK - Can we genetically modify the occupant? Includes scanner
            //  operational guard checks.
            if (!can_modify_occupant())
                return dot;

            // GUARD CHECK - Have we somehow cheekily swapped occupants? This is
            //  unexpected.
            if (scanner_occupant != connected_scanner.occupant)
                return dot;

            // GUARD CHECK - Is the occupant currently undergoing some form of
            //  transformation? If so, we don't want to be pulsing genes.
            if (Dm.truthy(scanner_occupant.transformation_timer))
            {
                to_chat(usr, span_warning(I18n.T("Gene pulse failed: The scanner occupant undergoing a transformation.")));
                return dot;
            }

            // Resolve mutation's BYOND path from the alias
            object alias = @params["alias"];
            object path = GET_MUTATION_TYPE_FROM_ALIAS(alias);

            // Make sure the occupant still has this mutation
            if (!Dm.@in(path, scanner_occupant.dna.mutation_index))
                return dot;

            // Resolve BYOND path to genome sequence of scanner occupant
            string sequence = Dm.str(GET_GENE_STRING(path, scanner_occupant.dna));

            object newgene = null;
            object pulse_action = @params["pulseAction"];
            float genepos = Dm.num(text2num(@params["pos"]));

            if (genepos > length(sequence))
                throw CRASH($"Unexpected input for [\"pos\"] param sent to {Dm.str(type)} tgui interface. Consult tgui logs for error.", "code/game/machinery/computer/dna_console.dm", 590);

            // X out the gene.
            if (Dm.eq(pulse_action, CLEAR_GENE))
            {
                newgene = "X";
                string defaultseq = Dm.str(scanner_occupant.dna.default_mutation_genes[path]);
                scanner_occupant.dna.default_mutation_genes[path] = Dm.text("[]X[]", copytext(defaultseq, 1, genepos), copytext(defaultseq, genepos + 1));
            }
            // Either try to apply a joker if selected in the interface, or iterate the next gene.
            else if (Dm.eq(pulse_action, NEXT_GENE))
            {
                if (Dm.truthy(tgui_view_state["jokerActive"]) && (joker_ready < world.time))
                {
                    string truegenes = Dm.str(GET_SEQUENCE(path));
                    newgene = Dm.textchar(truegenes, genepos);
                    joker_ready = world.time + JOKER_TIMEOUT - (JOKER_UPGRADE * (connected_scanner.precision_coeff - 1));
                    tgui_view_state["jokerActive"] = FALSE;
                }
                else
                {
                    float current_letter = gene_letters.Find(Dm.textchar(sequence, genepos));
                    newgene = (current_letter == gene_letter_count) ? gene_letters[1] : gene_letters[current_letter + 1];
                }
            }
            // Iterate previous gene.
            else if (Dm.eq(pulse_action, PREV_GENE))
            {
                float current_letter = gene_letters.Find(Dm.textchar(sequence, genepos));
                if (current_letter == 0)
                    current_letter = 1;
                newgene = (current_letter == 1) ? gene_letters[gene_letter_count] : gene_letters[current_letter - 1];
            }
            // Unknown input.
            else
                throw CRASH($"Unexpected input for [\"pulseAction\"] param sent to {Dm.str(type)} tgui interface. Consult tgui logs for error.", "code/game/machinery/computer/dna_console.dm", 615);

            // Copy genome to scanner occupant and do some basic mutation checks as
            //  we've increased the occupant genetic damage
            scanner_occupant.dna.mutation_index[path] = Dm.text("[][][]", copytext(sequence, 1, genepos), newgene, copytext(sequence, genepos + 1));
            await scanner_occupant.apply_status_effect(typeof(datum_status_effect_genetic_damage), GENETIC_DAMAGE_STRENGTH_MULTIPLIER / connected_scanner.damage_coeff);
            await scanner_occupant.domutcheck();

            // GUARD CHECK - Modifying genetics can lead to edge cases where the
            //  scanner occupant is qdel'd and replaced with a different entity.
            //  Examples of this include adding/removing the Monkified mutation which
            //  qdels the previous entity and creates a brand new one in its place.
            // We should redo all of our occupant modification checks again, although
            //  it is less than ideal.
            if (!can_modify_occupant())
                return dot;

            // Check if we cracked a mutation
            await check_discovery(alias);
            await use_scanner_or_own_energy();
            return dot;
        }

        // Apply a chromosome to a specific mutation.
        // ---------------------------------------------------------------------- //
        // params["mutref"] - ATOM Ref of specific mutation to apply the chromo to
        // params["chromo"] - Name of the chromosome to apply to the mutation
        if (Dm.eq(sw, "apply_chromo"))
        {
            // GUARD CHECK - Can we genetically modify the occupant? Includes scanner
            //  operational guard checks.
            if (!can_modify_occupant())
                return dot;

            // GUARD CHECK - Have we somehow cheekily swapped occupants? This is
            //  unexpected.
            if (scanner_occupant != connected_scanner.occupant)
                return dot;

            object bref = @params["mutref"];

            // GUARD CHECK - Only search occupant for this specific ref, since your
            //  can only apply chromosomes to mutations occupants.
            datum_mutation mutation = get_mut_by_ref(bref, SEARCH_OCCUPANT);

            // GUARD CHECK - This should not be possible. Unexpected result
            if (mutation == null)
                return dot;

            // Look through our stored chromos and compare names to find a
            // stored chromo we can apply.
            foreach (obj_item_chromosome chromosome in Dm.each<obj_item_chromosome>(stored_chromosomes.Copy()))
            {
                if (Dm.truthy(chromosome.can_apply(mutation)) && Dm.eq(chromosome.name, @params["chromo"]))
                {
                    // tg removes the mutation from the chromosome list here (stored_chromosomes -= mutation, a typo that leaves the
                    // applied, qdel'd chromosome listed until its hard delete); the port removes the chromosome (DV406)
                    Dm.remove(ref stored_chromosomes, chromosome);
                    await chromosome.apply(mutation);
                }
            }
            await use_scanner_or_own_energy();
            return dot;
        }

        // Attempt overwriting Base DNA : The pairs are instead the top row vs the top row of the new code.
        // So AA means the AT pair stays the same, AT means AT becomes TA. This requires both knowing the
        // solved full DNA of the subject mutation and the full DNA of the replacement genes. Applies probable disease
        // of probable strengths as well. If you mess it up, you might end up getting undesirable genes, including
        // unstable DNA. This could lead to permanent monkey. When you get it right, some will be swapped out, on a
        // probability scale.
        // ---------------------------------------------------------------------- //
        // params["mutref"] - ATOM Ref of specific mutation to swap out
        // params["source"] - The source the request came from.
        // Expected results:
        //   "occupant" - From genetic sequencer
        //   "console" - From DNA Console storage
        //   "disk" - From inserted diskette
        if (Dm.eq(sw, "crispr"))
        {
            // GUARD CHECK - Can we genetically modify the occupant? Includes scanner
            //  operational guard checks.
            if (!can_modify_occupant())
                return dot;

            // GUARD CHECK - Have we somehow cheekily swapped occupants? This is
            //  unexpected.
            if (scanner_occupant != connected_scanner.occupant)
                return dot;

            //GUARD CHECK
            //Make sure there's charges available.
            if (crispr_charges < 1)
                return dot;
            int search_flags = 0;

            // Only continue if applying to occupant - all replacements in-vitro.
            object source = @params["source"];
            if (Dm.eq(source, "occupant"))
            {
                if (can_modify_occupant())
                    search_flags |= SEARCH_OCCUPANT;
            }
            else if (Dm.eq(source, "console"))
            {
                search_flags |= SEARCH_STORED;
                return dot;
            }
            else if (Dm.eq(source, "disk"))
            {
                search_flags |= SEARCH_DISKETTE;
                return dot;
            }

            //Currently selected mutation
            object bref = @params["mutref"];

            //Valid gene-pairs
            string at_str = "AT";
            string cg_str = "CG";

            // GUARD CHECK - Only search occupant for this specific ref, since you
            //  can only CRISPR existing mutations in a target
            datum_mutation target_mutation = get_mut_by_ref(bref, search_flags);

            // Prompt for modifier string
            string new_sequence_input = Dm.str(await tgui_input_text(usr, I18n.T("Enter a replacement sequence"), I18n.T("Inherent Gene Replacement"), max_length: 32, encode: FALSE));
            // Drop out if the string is the wrong length
            if (length(new_sequence_input) != 32)
                return dot;

            //Generate the original and new gene sequences from the CRISPR string
            //vars to hold the 2 sequences
            string old_sequence = null;
            string new_sequence = null;

            //Unzip the modification string
            for (int i = 1; i <= length(new_sequence_input); i++)
            {
                string @char = Dm.textchar(new_sequence_input, i);
                string pair_str;
                string new_pair;
                //figure out which pair type the character belongs to
                pair_str = (Dm.textchar(at_str, 1) == @char || Dm.textchar(at_str, 2) == @char) ? at_str : ((Dm.textchar(cg_str, 1) == @char || Dm.textchar(cg_str, 2) == @char) ? cg_str : null);
                //Valid pair from character
                new_pair = pair_str != null ? @char + (Dm.textchar(pair_str, 1) == @char ? Dm.textchar(pair_str, 2) : Dm.textchar(pair_str, 1)) : null;
                // every second letter in the sequence represents a valid pair of the new sequence, otherwise it belongs to old
                if (new_pair != null)
                {
                    if (i % 2 == 0)
                        new_sequence += new_pair;
                    else
                        old_sequence += new_pair;
                }
                else
                    return dot; //drop out, no pair
            }

            //decrement CRISPR charge
            crispr_charges--;

            //Apply sequence
            if (Dm.truthy(new_sequence))
            {
                //to hold the found mutation, if found
                object matched_mutation = null;
                //Go through all sequences for matching gene, and set the mutation
                foreach (object M in Dm.each(subtypesof(typeof(datum_mutation))))
                {
                    object true_sequence = GET_SEQUENCE(M);
                    if (Dm.eq(new_sequence, true_sequence))
                        matched_mutation = M;
                }
                //First check is for the more-likely, weaker random virus. Second is for a tougher one. There's a chance both checks fail and you get nothing.
                //This change was to bring it more in line with what I originally imagined, that the virus risk was from the virus misbehaving somehow - it
                //should be a "sometimes" thing, not an "always" thing, but risky enough to force the need for precautions to isolate the subject
                if (prob(60))
                    await scanner_occupant.ContactContractDisease(await Dm.new_async<datum_disease_advance_random>(2, 2)); // gaps VI1
                else if (prob(30))
                    await scanner_occupant.ContactContractDisease(await Dm.new_async<datum_disease_advance_random>(3, 4)); // gaps VI1
                //Instantiate list to hold resulting mutation_index
                DmList mutation_data = Dm.list();
                //Start with the bad mutation, overwrite with the desired mutation if it passes the check
                //assures BAD END is the natural state if things go wrong
                //I think this should be like with viruses, probability cascade or switch/case on random?
                object result_mutation = typeof(datum_mutation_acidflesh);
                //If we found the replacement mutation
                if (Dm.truthy(matched_mutation))
                {
                    //and the old sequence matches the real sequence of the old mutation
                    if (Dm.eq(old_sequence, GET_SEQUENCE(target_mutation.type)))
                    {
                        //Set the replacement mutation to the desired mutation
                        result_mutation = matched_mutation;
                    }
                }
                //Remove the current active mutations - let's say doing this triggers DNA repair or something
                //This is admittedly because I couldn't figure out how to only remove the targeted mutation
                //Not touching MUTATION_SOURCE_MUTATOR will hopefully leave the added mutations alone
                await scanner_occupant.dna.remove_all_mutations(Dm.list(MUTATION_SOURCE_ACTIVATED));
                //Add the resulting mutation to the active mutations
                await scanner_occupant.dna.add_mutation(result_mutation, MUTATION_SOURCE_ACTIVATED);
                //Rebuild the mutation_index into mutation_data, replacing the sequence entry with the solved
                //entry for the result mutation
                foreach (object mutation_type in Dm.each(scanner_occupant.dna.mutation_index))
                {
                    if (Dm.eq(mutation_type, target_mutation.type))
                        mutation_data[result_mutation] = new_sequence;
                    else
                        mutation_data[mutation_type] = scanner_occupant.dna.mutation_index[mutation_type];
                }
                //Overwrite the mutation_index list with the rebuild mutation_data
                scanner_occupant.dna.mutation_index = mutation_data;
                //Not sure what this does but it seems to be a sanity check and this needs a sanity check
                await scanner_occupant.domutcheck();
            }

            await use_scanner_or_own_energy();

            return dot;
        }

        // Print any type of standard injector, limited right now to activators that
        //  activate a dormant mutation and mutators that forcibly create a new
        //  MUTATION_SOURCE_MUTATOR mutation
        // ---------------------------------------------------------------------- //
        // params["mutref"] - ATOM Ref of specific mutation to create an injector of
        // params["is_activator"] - Is this an "Activator" style injector, also
        //  referred to as a "Research" type. Expects a string with 0 or 1, which
        //  then gets converted to a number.
        // params["source"] - The source the request came from.
        // Expected results:
        //   "occupant" - From genetic sequencer
        //   "console" - From DNA Console storage
        //   "disk" - From inserted diskette
        if (Dm.eq(sw, "print_injector"))
        {
            // Because printing mutators and activators share a bunch of code,
            //  it makes sense to keep them both together and set unique vars
            //  later in the code

            // As a side note, because mutations can contain unique metadata,
            //  this system uses BYOND Atom Refs to safely and accurately
            //  identify mutations from big ol' lists

            // GUARD CHECK - Is the injector actually ready?
            if (world.time < injector_ready)
                return dot;

            int search_flags = 0;

            object source = @params["source"];
            if (Dm.eq(source, "occupant"))
            {
                // GUARD CHECK - Make sure we can modify the occupant before we
                //  attempt to search them for any given mutation refs. This could
                //  lead to no search flags being passed to get_mut_by_ref and this
                //  is intended functionality to prevent any cheese or abuse
                if (can_modify_occupant())
                    search_flags |= SEARCH_OCCUPANT;
            }
            else if (Dm.eq(source, "console"))
                search_flags |= SEARCH_STORED;
            else if (Dm.eq(source, "disk"))
                search_flags |= SEARCH_DISKETTE;

            object bref = @params["mutref"];
            datum_mutation mutation = get_mut_by_ref(bref, search_flags);

            // GUARD CHECK - This should not be possible. Unexpected result
            if (mutation == null)
                return dot;

            if (Dm.truthy(length(mutation.sources)) && Dm.eq(get_mutation_class(mutation), SCANNER_MUTATION_CLASS_OTHER))
            {
                await say(I18n.T("ERROR: This mutation is anomalous, and cannot be printed."));
                return dot;
            }

            // Create a new DNA Injector and add the appropriate mutations to it
            obj_item_dnainjector_activator injector = Dm.@new<obj_item_dnainjector_activator>(loc);
            LAZYADD(ref injector.add_mutations, await mutation.make_copy());

            object is_activator = text2num(@params["is_activator"]);

            // Activators are also called "research" injectors and are used to create
            //  chromosomes by recycling at the DNA Console
            if (Dm.truthy(is_activator))
            {
                injector.name = Dm.text("[] activator", mutation.name);
                injector.research = TRUE;
                // If there's an operational connected scanner, we can use its upgrades
                //  to improve our injector's genetic damage generation
                float cd_reduction_mult = 1 + ACTIVATOR_COOLDOWN_MULTIPLIER;
                float base_cd_time = max(MIN_ACTIVATOR_TIMEOUT, abs(mutation.instability) * SECONDS);

                if (Dm.truthy(scanner_operational()))
                {
                    injector.damage_coeff = connected_scanner.damage_coeff * 4;
                    // T1: 1.25 - 0.25: 1: 100%
                    // T4: 1.25 - 1: 0.25 = 25%
                    // 25% reduction per tier
                    cd_reduction_mult -= ACTIVATOR_COOLDOWN_MULTIPLIER * (connected_scanner.precision_coeff);
                }

                injector_ready = world.time + (base_cd_time * cd_reduction_mult);
            }
            else
            {
                injector.name = Dm.text("[] mutator", mutation.name);
                injector.force_mutate = TRUE;
                // If there's an operational connected scanner, we can use its upgrades
                //  to improve our injector's genetic damage generation
                float cd_reduction_mult = 1 + INJECTOR_COOLDOWN_MULTIPLIER;
                float base_cd_time = max(MIN_INJECTOR_TIMEOUT, abs(mutation.instability) * 1 * SECONDS);

                if (Dm.truthy(scanner_operational()))
                {
                    injector.damage_coeff = connected_scanner.damage_coeff * 4;
                    // T1: 1.15 - 0.15: 1: 100%
                    // T4: 1.15 - 0.60: 0.55: 55%
                    // 15% reduction per tier
                    cd_reduction_mult -= (INJECTOR_COOLDOWN_MULTIPLIER * connected_scanner.precision_coeff);
                }

                injector_ready = world.time + (base_cd_time * cd_reduction_mult);
            }
            await use_scanner_or_own_energy();
            return dot;
        }

        // Save a mutation to the console's storage buffer.
        // ---------------------------------------------------------------------- //
        // params["mutref"] - ATOM Ref of specific mutation to store
        // params["source"] - The source the request came from.
        // Expected results:
        //   "occupant" - From genetic sequencer
        //   "disk" - From inserted diskette
        if (Dm.eq(sw, "save_console"))
        {
            int search_flags = 0;

            object source = @params["source"];
            if (Dm.eq(source, "occupant"))
            {
                // GUARD CHECK - Make sure we can modify the occupant before we
                //  attempt to search them for any given mutation refs. This could
                //  lead to no search flags being passed to get_mut_by_ref and this
                //  is intended functionality to prevent any cheese or abuse
                if (can_modify_occupant())
                    search_flags |= SEARCH_OCCUPANT;
            }
            else if (Dm.eq(source, "disk"))
                search_flags |= SEARCH_DISKETTE;

            object bref = @params["mutref"];
            datum_mutation mutation = get_mut_by_ref(bref, search_flags);

            // GUARD CHECK - This should not be possible. Unexpected result
            if (mutation == null)
                return dot;

            // Saving temporary or unobtainable mutations leads to gratuitous abuse
            if (Dm.truthy(length(mutation.sources)) && Dm.eq(get_mutation_class(mutation), SCANNER_MUTATION_CLASS_OTHER))
            {
                await say(I18n.T("ERROR: This mutation is anomalous, and cannot be saved."));
                return dot;
            }

            object stored = await mutation.make_copy();
            Dm.append(ref stored_mutations, stored);
            to_chat(usr, span_notice(I18n.T("Mutation successfully stored.")));
            return dot;
        }

        // Save a mutation to the diskette's storage buffer.
        // ---------------------------------------------------------------------- //
        // params["mutref"] - ATOM Ref of specific mutation to store
        // params["source"] - The source the request came from
        // Expected results:
        //   "occupant" - From genetic sequencer
        //   "console" - From DNA Console storage
        if (Dm.eq(sw, "save_disk"))
        {
            // GUARD CHECK - This code shouldn't even be callable without a diskette
            //  inserted. Unexpected result
            if (diskette == null)
                return dot;

            // GUARD CHECK - Make sure the disk is not full
            if (LAZYLEN(diskette.mutations) >= diskette.max_mutations)
            {
                to_chat(usr, span_warning(I18n.T("Disk storage is full.")));
                return dot;
            }

            // GUARD CHECK - Make sure the disk isn't set to read only, as we're
            //  attempting to write to it
            if (Dm.truthy(diskette.read_only))
            {
                to_chat(usr, span_warning(I18n.T("Disk is set to read only mode.")));
                return dot;
            }

            int search_flags = 0;

            object source = @params["source"];
            if (Dm.eq(source, "occupant"))
            {
                // GUARD CHECK - Make sure we can modify the occupant before we
                //  attempt to search them for any given mutation refs. This could
                //  lead to no search flags being passed to get_mut_by_ref and this
                //  is intended functionality to prevent any cheese or abuse
                if (can_modify_occupant())
                    search_flags |= SEARCH_OCCUPANT;
            }
            else if (Dm.eq(source, "console"))
                search_flags |= SEARCH_STORED;

            object bref = @params["mutref"];
            datum_mutation original = get_mut_by_ref(bref, search_flags);

            // GUARD CHECK - This should not be possible. Unexpected result
            if (original == null)
                return dot;

            if (Dm.truthy(length(original.sources)) && Dm.eq(get_mutation_class(original), SCANNER_MUTATION_CLASS_OTHER))
            {
                await say(I18n.T("ERROR: This mutation is anomalous, and cannot be saved."));
                return dot;
            }

            Dm.append(ref diskette.mutations, await original.make_copy());
            to_chat(usr, span_notice(I18n.T("Mutation successfully stored to disk.")));
            return dot;
        }

        // Completely removes a MUTATION_SOURCE_MUTATOR mutation or mutation with corrupt gene
        //  sequence from the scanner occupant
        // ---------------------------------------------------------------------- //
        // params["mutref"] - ATOM Ref of specific mutation to nullify
        if (Dm.eq(sw, "nullify"))
        {
            // GUARD CHECK - Can we genetically modify the occupant? Includes scanner
            //  operational guard checks.
            if (!can_modify_occupant())
                return dot;

            object bref = @params["mutref"];
            datum_mutation mutation = get_mut_by_ref(bref, SEARCH_OCCUPANT);

            // GUARD CHECK - This should not be possible. Unexpected result
            if (mutation == null)
                return dot;

            // GUARD CHECK - Nullify should only be used on scrambled or "extra"
            //  mutations.
            if (!Dm.truthy(mutation.scrambled) && !Dm.@in(MUTATION_SOURCE_MUTATOR, mutation.sources))
                return dot;

            DmList types_to_remove = Dm.list(MUTATION_SOURCE_MUTATOR);
            if (Dm.truthy(mutation.scrambled))
                Dm.append(ref types_to_remove, MUTATION_SOURCE_ACTIVATED);
            await scanner_occupant.dna.remove_mutation(mutation.type, types_to_remove);
            if (!QDELETED(mutation))
                mutation.scrambled = FALSE;
            return dot;
        }

        // Deletes saved mutation from console buffer.
        // ---------------------------------------------------------------------- //
        // params["mutref"] - ATOM Ref of specific mutation to delete
        if (Dm.eq(sw, "delete_console_mut"))
        {
            object bref = @params["mutref"];
            datum_mutation HM = get_mut_by_ref(bref, SEARCH_STORED);

            if (HM != null)
            {
                stored_mutations.Remove(HM);
                qdel(HM);
            }

            return dot;
        }

        // Deletes saved mutation from disk buffer.
        // ---------------------------------------------------------------------- //
        // params["mutref"] - ATOM Ref of specific mutation to delete
        if (Dm.eq(sw, "delete_disk_mut"))
        {
            // GUARD CHECK - This code shouldn't even be callable without a diskette
            //  inserted. Unexpected result
            if (diskette == null)
                return dot;

            // GUARD CHECK - Make sure the disk isn't set to read only, as we're
            //  attempting to write to it (via deletion)
            if (Dm.truthy(diskette.read_only))
            {
                to_chat(usr, span_warning(I18n.T("Disk is set to read only mode.")));
                return dot;
            }

            object bref = @params["mutref"];
            datum_mutation HM = get_mut_by_ref(bref, SEARCH_DISKETTE);

            if (HM != null)
            {
                diskette.mutations.Remove(HM);
                qdel(HM);
            }

            return dot;
        }

        // Ejects a stored chromosome from the DNA Console
        // ---------------------------------------------------------------------- //
        // params["chromo"] - Text string of the chromosome name
        if (Dm.eq(sw, "eject_chromo"))
        {
            object chromname = @params["chromo"];

            foreach (obj_item_chromosome CM in Dm.each<obj_item_chromosome>(stored_chromosomes))
            {
                if (Dm.eq(chromname, CM.name))
                {
                    await CM.forceMove(drop_location() as atom);
                    adjust_item_drop_location(CM);
                    Dm.remove(ref stored_chromosomes, CM);
                    return dot;
                }
            }

            return dot;
        }

        // Combines two mutations from the console to try and create a new mutation
        // ---------------------------------------------------------------------- //
        // params["firstref"] - ATOM Ref of first mutation for combination
        // params["secondref"] - ATOM Ref of second mutation for combination
        //  mutation
        if (Dm.eq(sw, "combine_console"))
        {
            // GUARD CHECK - We're running a research-type operation. If, for some
            //  reason, somehow the DNA Console has been disconnected from the research
            //  network - Or was never in it to begin with - don't proceed
            if (stored_research == null)
                return dot;

            object first_bref = @params["firstref"];
            object second_bref = @params["secondref"];

            // GUARD CHECK - Find the source and destination mutations on the console
            // and make sure they actually exist.
            datum_mutation source_mut = get_mut_by_ref(first_bref, SEARCH_STORED | SEARCH_DISKETTE);
            if (source_mut == null)
                return dot;

            datum_mutation dest_mut = get_mut_by_ref(second_bref, SEARCH_STORED | SEARCH_DISKETTE);
            if (dest_mut == null)
                return dot;

            // Attempt to mix the two mutations to get a new type
            object result_path = get_mixed_mutation(source_mut.type, dest_mut.type);

            if (!Dm.truthy(result_path))
                return dot;

            // If we got a new type, add it to our storage
            Dm.append(ref stored_mutations, Dm.@new(result_path as Type));
            to_chat(usr, span_boldnotice(I18n.T("Success! New mutation has been added to console storage.")));

            // If it's already discovered, end here. Otherwise, add it to the list of
            //  discovered mutations.
            // We've already checked for stored_research earlier
            if (Dm.@in(result_path, stored_research.discovered_mutations))
                return dot;

            datum_mutation HM = GET_INITIALIZED_MUTATION(result_path) as datum_mutation;
            Dm.append(ref stored_research.discovered_mutations, result_path);
            await say(I18n.T("Successfully mutated [].", HM.name));
            await use_scanner_or_own_energy();
            return dot;
        }

        // Combines two mutations from the disk to try and create a new mutation
        // ---------------------------------------------------------------------- //
        // params["firstref"] - ATOM Ref of first mutation for combination
        // params["secondref"] - ATOM Ref of second mutation for combination
        //  mutation
        if (Dm.eq(sw, "combine_disk"))
        {
            // GUARD CHECK - This code shouldn't even be callable without a diskette
            //  inserted. Unexpected result
            if (diskette == null)
                return dot;

            // GUARD CHECK - Make sure the disk is not full.
            if (LAZYLEN(diskette.mutations) >= diskette.max_mutations)
            {
                to_chat(usr, span_warning(I18n.T("Disk storage is full.")));
                return dot;
            }

            // GUARD CHECK - Make sure the disk isn't set to read only, as we're
            //  attempting to write to it
            if (Dm.truthy(diskette.read_only))
            {
                to_chat(usr, span_warning(I18n.T("Disk is set to read only mode.")));
                return dot;
            }

            // GUARD CHECK - We're running a research-type operation. If, for some
            // reason, somehow the DNA Console has been disconnected from the research
            // network - Or was never in it to begin with - don't proceed
            if (stored_research == null)
                return dot;

            object first_bref = @params["firstref"];
            object second_bref = @params["secondref"];

            // GUARD CHECK - Find the source and destination mutations on the console
            // and make sure they actually exist.
            datum_mutation source_mut = get_mut_by_ref(first_bref, SEARCH_STORED | SEARCH_DISKETTE);
            if (source_mut == null)
                return dot;

            datum_mutation dest_mut = get_mut_by_ref(second_bref, SEARCH_STORED | SEARCH_DISKETTE);
            if (dest_mut == null)
                return dot;

            // Attempt to mix the two mutations to get a new type
            object result_path = get_mixed_mutation(source_mut.type, dest_mut.type);

            if (!Dm.truthy(result_path))
                return dot;

            // If we got a new type, add it to our storage
            Dm.append(ref diskette.mutations, Dm.@new(result_path as Type));
            to_chat(usr, span_boldnotice(I18n.T("Success! New mutation has been added to the disk.")));

            // If it's already discovered, end here. Otherwise, add it to the list of
            //  discovered mutations
            // We've already checked for stored_research earlier
            if (Dm.@in(result_path, stored_research.discovered_mutations))
                return dot;

            datum_mutation HM = GET_INITIALIZED_MUTATION(result_path) as datum_mutation;
            Dm.append(ref stored_research.discovered_mutations, result_path);
            await say(I18n.T("Successfully mutated [].", HM.name));
            await use_scanner_or_own_energy();
            return dot;
        }

        // Sets the Genetic Makeup pulse strength.
        // ---------------------------------------------------------------------- //
        // params["val"] - New strength value as text string, converted to number
        //  later on in code
        if (Dm.eq(sw, "set_pulse_strength"))
        {
            float value = round(Dm.num(text2num(@params["val"])));
            pulse_strength = WRAP(value, 1, GENETIC_DAMAGE_STRENGTH_MAX + 1);
            return dot;
        }

        // Sets the Genetic Makeup pulse duration
        // ---------------------------------------------------------------------- //
        // params["val"] - New strength value as text string, converted to number
        //  later on in code
        if (Dm.eq(sw, "set_pulse_duration"))
        {
            float value = round(Dm.num(text2num(@params["val"])));
            pulse_duration = WRAP(value, 1, GENETIC_DAMAGE_DURATION_MAX + 1);
            return dot;
        }

        // Saves Genetic Makeup information to disk
        // ---------------------------------------------------------------------- //
        // params["index"] - The BYOND index of the console genetic makeup buffer to
        //  copy to disk
        if (Dm.eq(sw, "save_makeup_disk"))
        {
            // GUARD CHECK - This code shouldn't even be callable without a diskette
            //  inserted. Unexpected result
            if (diskette == null)
                return dot;

            // GUARD CHECK - Make sure the disk isn't set to read only, as we're
            //  attempting to write to it
            if (Dm.truthy(diskette.read_only))
            {
                to_chat(usr, span_warning(I18n.T("Disk is set to read only mode.")));
                return dot;
            }

            // Convert the index to a number and clamp within the array range
            float buffer_index = buffer_index_of(@params["index"]);

            // GUARD CHECK - This should not be possible to activate on a buffer slot
            //  that doesn't have any genetic data. Unexpected result
            if (genetic_makeup_buffer[buffer_index] is not DmList buffer_slot)
                return dot;

            diskette.genetic_makeup_buffer = buffer_slot.Copy();
            return dot;
        }

        // Loads Genetic Makeup from disk to a console buffer
        // ---------------------------------------------------------------------- //
        // params["index"] - The BYOND index of the console genetic makeup buffer to
        //  copy to. Expected as text string, converted to number later
        if (Dm.eq(sw, "load_makeup_disk"))
        {
            // GUARD CHECK - This code shouldn't even be callable without a diskette
            //  inserted. Unexpected result
            if (diskette == null)
                return dot;

            // GUARD CHECK - This should not be possible to activate on a diskette
            //  that doesn't have any genetic data. Unexpected result
            if (LAZYLEN(diskette.genetic_makeup_buffer) == 0)
                return dot;

            // Convert the index to a number and clamp within the array range, then
            //  copy the data from the disk to that buffer
            float buffer_index = buffer_index_of(@params["index"]);
            genetic_makeup_buffer[buffer_index] = diskette.genetic_makeup_buffer.Copy();
            return dot;
        }

        // Deletes genetic makeup buffer from the inserted diskette
        if (Dm.eq(sw, "del_makeup_disk"))
        {
            // GUARD CHECK - This code shouldn't even be callable without a diskette
            //  inserted. Unexpected result
            if (diskette == null)
                return dot;

            // GUARD CHECK - Make sure the disk isn't set to read only, as we're
            //  attempting to write (via deletion) to it
            if (Dm.truthy(diskette.read_only))
            {
                to_chat(usr, span_warning(I18n.T("Disk is set to read only mode.")));
                return dot;
            }

            diskette.genetic_makeup_buffer.Cut();
            return dot;
        }

        // Saves the scanner occupant's genetic makeup to a given console buffer
        // ---------------------------------------------------------------------- //
        // params["index"] - The BYOND index of the console genetic makeup buffer to
        //  save the new genetic data to. Expected as text string, converted to
        //  number later
        if (Dm.eq(sw, "save_makeup_console"))
        {
            // GUARD CHECK - Can we genetically modify the occupant? Includes scanner
            //  operational guard checks.
            if (!can_modify_occupant())
                return dot;

            // Convert the index to a number and clamp within the array range, then
            //  copy the data from the disk to that buffer
            float buffer_index = buffer_index_of(@params["index"]);

            // Set the new information
            genetic_makeup_buffer[buffer_index] = Dm.list(
                Dm.kv("label", Dm.text("Slot []:[]", buffer_index, scanner_occupant.real_name)),
                Dm.kv("UI", scanner_occupant.dna.unique_identity),
                Dm.kv("UE", scanner_occupant.dna.unique_enzymes),
                Dm.kv("UF", scanner_occupant.dna.unique_features),
                Dm.kv("name", scanner_occupant.real_name),
                Dm.kv("blood_type", (scanner_occupant.get_bloodtype() as datum_blood_type)?.name));

            return dot;
        }

        // Deleted genetic makeup data from a console buffer slot
        // ---------------------------------------------------------------------- //
        // params["index"] - The BYOND index of the console genetic makeup buffer to
        //  delete the genetic data from. Expected as text string, converted to
        //  number later
        if (Dm.eq(sw, "del_makeup_console"))
        {
            // Convert the index to a number and clamp within the array range, then
            //  copy the data from the disk to that buffer
            float buffer_index = buffer_index_of(@params["index"]);

            // GUARD CHECK - This shouldn't be possible to execute this on a null
            //  buffer. Unexpected resut
            if (genetic_makeup_buffer[buffer_index] is not DmList)
                return dot;

            genetic_makeup_buffer[buffer_index] = null;
            return dot;
        }

        // Eject stored diskette from console
        if (Dm.eq(sw, "eject_disk"))
        {
            await eject_disk(usr);
            return dot;
        }

        // Create a Genetic Makeup injector. These injectors are timed and thus are
        //  only temporary
        // ---------------------------------------------------------------------- //
        // params["index"] - The BYOND index of the console genetic makeup buffer to
        //  create the makeup injector from. Expected as text string, converted to
        //  number later
        // params["type"] - Type of injector to create
        //  Expected results:
        //  "ue" - Unique Enzyme, changes name and blood type
        //  "ui" - Unique Identity, changes looks
        //  "uf" - Unique Features, changes mutant bodyparts and mutcolors
        //  "mixed" - Combination of both ue and ui
        if (Dm.eq(sw, "makeup_injector"))
        {
            if (!(enzyme_copy_timer <= world.time)) // COOLDOWN_FINISHED, inline (rules 11.1 (a))
                return dot;
            // Convert the index to a number and clamp within the array range, then copy the data from the disk to that buffer
            float buffer_index = buffer_index_of(@params["index"]);
            if (!make_cosmetic_dna_injector(dna_injector_type_to_flag(@params["type"]), genetic_makeup_buffer[buffer_index] as DmList))
            {
                to_chat(usr, span_warning(I18n.T("Genetic data corrupted, unable to create injector.")));
                return dot;
            }
            injector_ready = world.time + MISC_INJECTOR_TIMEOUT;
            await use_scanner_or_own_energy();
            return dot;
        }

        // Applies a genetic makeup buffer to the scanner occupant
        // ---------------------------------------------------------------------- //
        // params["index"] - The BYOND index of the console genetic makeup buffer to
        //  apply to the scanner occupant. Expected as text string, converted to
        //  number later
        // params["type"] - Type of genetic makeup copy to implement
        //  Expected results:
        //   "ue" - Unique Enzyme, changes name and blood type
        //  "ui" - Unique Identity, changes looks
        //  "uf" - Unique Features, changes mutant bodyparts and mutcolors
        //  "mixed" - Combination of ue, ui, and uf
        if (Dm.eq(sw, "makeup_apply"))
        {
            // GUARD CHECK - Can we genetically modify the occupant? Includes scanner
            //  operational guard checks.
            if (!can_modify_occupant())
                return dot;

            if (!(enzyme_copy_timer <= world.time)) // COOLDOWN_FINISHED, inline (rules 11.1 (a))
                return dot;

            // Convert the index to a number and clamp within the array range, then
            //  copy the data from the disk to that buffer
            float buffer_index = buffer_index_of(@params["index"]);

            // GUARD CHECK - This shouldn't be possible to execute this on a null
            //  buffer. Unexpected resut
            if (genetic_makeup_buffer[buffer_index] is not DmList buffer_slot)
                return dot;

            object makeup_type = @params["type"];

            await apply_genetic_makeup(makeup_type, buffer_slot);
            await use_scanner_or_own_energy();
            return dot;
        }

        // Applies a genetic makeup buffer to the next scanner occupant. This sets
        //  some code that will run when the connected DNA Scanner door is next
        //  closed
        // This allows people to self-modify their genetic makeup, as tgui
        //  interfaces can not be accessed while inside the DNA Scanner and genetic
        //  makeup injectors are only temporary
        // ---------------------------------------------------------------------- //
        // params["index"] - The BYOND index of the console genetic makeup buffer to
        //  apply to the scanner occupant. Expected as text string, converted to
        //  number later
        // params["type"] - Type of genetic makeup copy to implement
        //  Expected results:
        //   "ue" - Unique Enzyme, changes name and blood type
        //  "ui" - Unique Identity, changes looks
        //  "uf" - Unique Features, changes mutant bodyparts and mutcolors
        //  "mixed" - Combination of ue, ui, and uf
        if (Dm.eq(sw, "makeup_delay"))
        {
            // Convert the index to a number and clamp within the array range, then
            //  copy the data from the disk to that buffer
            float buffer_index = buffer_index_of(@params["index"]);

            // GUARD CHECK - This shouldn't be possible to execute this on a null
            //  buffer. Unexpected resut
            if (genetic_makeup_buffer[buffer_index] is not DmList buffer_slot)
                return dot;

            object makeup_type = @params["type"];

            // Set the delayed action. The next time the scanner door is closed,
            //  unless this is cancelled in the UI, the action will happen
            delayed_action = Dm.list(Dm.kv("type", makeup_type), Dm.kv("buffer_slot", buffer_slot));
            return dot;
        }

        // Attempts to modify the indexed element of the Unique Identity string
        // This is a time delayed action that is handled in process()
        // ---------------------------------------------------------------------- //
        // params["type"] - Type of genetic makeup string to edit
        //  Expected results:
        //  "ui" - Unique Identity, changes looks
        //  "uf" - Unique Features, changes mutant bodyparts and mutcolors
        // params["index"] - The BYOND index of the Unique Identity string to
        //  attempt to modify
        if (Dm.eq(sw, "makeup_pulse"))
        {
            // GUARD CHECK - Can we genetically modify the occupant? Includes scanner
            //  operational guard checks.
            if (!can_modify_occupant())
                return dot;

            // Set the appropriate timer, string, and index to pulse. This is then managed
            //  later on in process()
            object makeup_type = @params["type"];
            genetic_damage_pulse_type = makeup_type;
            float len = 0;
            if (Dm.eq(makeup_type, "ui"))
                len = length(scanner_occupant.dna.unique_identity);
            else if (Dm.eq(makeup_type, "uf"))
                len = length(scanner_occupant.dna.unique_features);
            genetic_damage_pulse_timer = world.time + (pulse_duration * 10);
            genetic_damage_pulse_index = WRAP(Dm.num(text2num(@params["index"])), 1, len + 1);
            begin_processing();
            await use_scanner_or_own_energy();
            return dot;
        }

        // Cancels the delayed action - In this context it is not the genetic damage
        //  pulse from "makeup_pulse", which can not be cancelled. It is instead
        //  the delayed genetic transfer from "makeup_delay"
        if (Dm.eq(sw, "cancel_delay"))
        {
            delayed_action = null;
            return dot;
        }

        // Creates a new advanced injector storage buffer in the console
        // ---------------------------------------------------------------------- //
        // params["name"] - The name to apply to the new injector
        if (Dm.eq(sw, "new_adv_inj"))
        {
            // GUARD CHECK - Make sure we can make a new injector. This code should
            //  not be called if we're already maxed out and this is an Unexpected
            //  result
            if (!(LAZYLEN(injector_selection) < max_injector_selections))
                return dot;

            // GUARD CHECK - Sanitise and trim the proposed name. This prevents HTML
            //  injection and equivalent as tgui input is not stripped
            object inj_name = @params["name"];
            inj_name = trim(sanitize(inj_name));

            // GUARD CHECK - If the name is null or blank, or the name is already in
            //  the list of advanced injectors, we want to reject it as we can't have
            //  duplicate named advanced injectors
            if (!Dm.truthy(inj_name) || Dm.@in(inj_name, injector_selection))
                return dot;

            injector_selection[inj_name] = Dm.list();
            return dot;
        }

        // Deleted an advanced injector storage buffer from the console
        // ---------------------------------------------------------------------- //
        // params["name"] - The name of the injector to delete
        if (Dm.eq(sw, "del_adv_inj"))
        {
            object inj_name = @params["name"];

            // GUARD CHECK - If the name is null or blank, reject.
            // GUARD CHECK - If the name isn't in the list of advanced injectors, we
            //  want to reject this as it shouldn't be possible ever do this.
            // Unexpected result
            if (!Dm.truthy(inj_name) || !Dm.@in(inj_name, injector_selection))
                return dot;

            injector_selection.Remove(inj_name);
            return dot;
        }

        // Creates an injector from an advanced injector buffer
        // ---------------------------------------------------------------------- //
        // params["name"] - The name of the injector to print
        if (Dm.eq(sw, "print_adv_inj"))
        {
            // As a side note, because mutations can contain unique metadata,
            // this system uses BYOND Atom Refs to safely and accurately
            // identify mutations from big ol' lists.

            // GUARD CHECK - Is the injector actually ready?
            if (world.time < injector_ready)
                return dot;

            object inj_name = @params["name"];

            // GUARD CHECK - If the name is null or blank, reject.
            // GUARD CHECK - If the name isn't in the list of advanced injectors, we
            //  want to reject this as it shouldn't be possible ever do this.
            // Unexpected result
            if (!Dm.truthy(inj_name) || !Dm.@in(inj_name, injector_selection))
                return dot;

            obj_item_dnainjector_activator injector = Dm.@new<obj_item_dnainjector_activator>(loc);

            // Run through each mutation in our Advanced Injector and add them to a
            //  new injector
            float total_stability = 0;
            foreach (datum_mutation mutation in Dm.each_cast<datum_mutation>(injector_selection[inj_name]))
            {
                LAZYADD(ref injector.add_mutations, await mutation.make_copy());
                total_stability += mutation.instability;
            }

            // Force apply any mutations, this is functionality similar to mutators
            injector.force_mutate = TRUE;
            injector.name = Dm.text("Advanced [] injector", inj_name);

            // If there's an operational connected scanner, we can use its upgrades
            //  to improve our injector's genetic damage generation
            float cd_reduction_mult = 1 + ADVANCED_COOLDOWN_MULTIPLIER;
            float base_cd_time = max(MIN_ADVANCED_TIMEOUT, abs(total_stability) * SECONDS);

            if (Dm.truthy(scanner_operational()))
            {
                injector.damage_coeff = connected_scanner.damage_coeff * 4;
                // T1: 1.1 - 0.1: 1: 100%
                // T4: 1.1 - 0.4: 0.7 = 70%
                // 10% reduction per tier
                cd_reduction_mult -= ADVANCED_COOLDOWN_MULTIPLIER * (connected_scanner.precision_coeff);
            }

            // Applies a hard cap to advanced injector cooldowns to avoid excessive wait times
            float adv_cd_time = min(ADVANCED_INJECTOR_MAX_COOLDOWN, (base_cd_time * cd_reduction_mult));
            injector_ready = world.time + adv_cd_time;
            return dot;
        }

        // Adds a mutation to an advanced injector
        // ---------------------------------------------------------------------- //
        // params["mutref"] - ATOM Ref of specific mutation to add to the injector
        // params["advinj"] - Name of the advanced injector to add the mutation to
        if (Dm.eq(sw, "add_advinj_mut"))
        {
            if (!Dm.truthy(scanner_operational()))
                return dot;
            object adv_inj = @params["advinj"];

            // GUARD CHECK - Make sure our advanced injector actually exists. This
            //  should not be possible. Unexpected result
            if (!Dm.@in(adv_inj, injector_selection))
                return dot;

            // GUARD CHECK - Make sure we limit the number of mutations appropriately
            if (LAZYLEN(injector_selection[adv_inj]) >= max_injector_mutations)
            {
                to_chat(usr, span_warning(I18n.T("Advanced injector mutation storage is full.")));
                return dot;
            }

            object mut_source = @params["source"];
            int search_flag = 0;

            if (Dm.eq(mut_source, "disk"))
                search_flag = SEARCH_DISKETTE;
            else if (Dm.eq(mut_source, "occupant"))
                search_flag = SEARCH_OCCUPANT;
            else if (Dm.eq(mut_source, "console"))
                search_flag = SEARCH_STORED;

            if (search_flag == 0)
                return dot;

            object bref = @params["mutref"];
            if ((search_flag & SEARCH_OCCUPANT) != 0)
            {
                if (!can_modify_occupant())
                    return dot;
            }
            // We've already made sure we can modify the occupant, so this is safe to
            //  call
            datum_mutation original = get_mut_by_ref(bref, search_flag);

            // GUARD CHECK - This should not be possible. Unexpected result
            if (original == null)
                return dot;

            // We want to make sure we stick within the instability limit.
            // We start with the instability of the mutation we're intending to add.
            float instability_total = original.instability;

            // We then add the instabilities of all other mutations in the injector,
            //  remembering to apply the Stabilizer chromosome modifiers
            foreach (datum_mutation mutation in Dm.each<datum_mutation>(injector_selection[adv_inj]))
                instability_total += mutation.instability * GET_MUTATION_STABILIZER(mutation);

            // If this would take us over the max instability, we inform the user.
            if (instability_total > max_injector_instability)
            {
                to_chat(usr, span_warning(I18n.T("Extra mutation would make the advanced injector too instable.")));
                return dot;
            }

            // If we've got here, all our checks are passed and we can successfully
            // add the mutation to the advanced injector.
            (injector_selection[adv_inj] as DmList).Add(await original.make_copy());
            to_chat(usr, span_notice(I18n.T("Mutation successfully added to advanced injector.")));
            await use_scanner_or_own_energy();
            return dot;
        }

        // Deletes a mutation from an advanced injector
        // ---------------------------------------------------------------------- //
        // params["mutref"] - ATOM Ref of specific mutation to del from the injector
        if (Dm.eq(sw, "delete_injector_mut"))
        {
            object bref = @params["mutref"];

            datum_mutation HM = get_mut_by_ref(bref, SEARCH_ADV_INJ);

            // GUARD CHECK - This should not be possible. Unexpected result
            if (HM == null)
                return dot;

            // Check Advanced Injectors to find and remove the mutation
            foreach (object I in Dm.each(injector_selection))
            {
                if (Dm.truthy((injector_selection[Dm.text("[]", I)] as DmList).Remove(HM)))
                {
                    qdel(HM);
                    return dot;
                }
            }

            return dot;
        }

        // Sets a new tgui view state
        // ---------------------------------------------------------------------- //
        // params["id"] - Key for the state to set
        // params[...] - Every other element is used to set state variables
        if (Dm.eq(sw, "set_view"))
        {
            foreach (object key in Dm.each(@params))
            {
                if (Dm.eq(key, "src"))
                    continue;
                tgui_view_state[key] = @params[key];
            }
            return TRUE;
        }
        return FALSE;
    }

    // The ui_act tail every machine operation ends with (dna_console.dm:466 and its copies): the connected scanner pays the
    // energy, else the console.
    private async DmTask use_scanner_or_own_energy()
    {
        if (connected_scanner != null)
            await connected_scanner.use_energy(connected_scanner.active_power_usage);
        else
            await use_energy(active_power_usage);
    }

    // var/buffer_index = text2num(params["index"]); buffer_index = clamp(buffer_index, 1, NUMBER_OF_BUFFERS) (dna_console.dm:1205
    // and its copies)
    private static float buffer_index_of(object index)
    {
        return clamp(Dm.num(text2num(index)), 1, NUMBER_OF_BUFFERS);
    }

    /**
     * Applies the enzyme buffer to the current scanner occupant
     *
     * Applies the type of a specific genetic makeup buffer to the current scanner
     * occupant
     *
     * Arguments:
     * * type - "ui"/"ue"/"mixed" - Which part of the enzyme buffer to apply
     * * buffer_slot - Index of the enzyme buffer to apply
     */
    // tg: code/game/machinery/computer/dna_console.dm:1690
    public virtual async DmTask<object> apply_genetic_makeup(object type = null, DmList buffer_slot = null)
    {
        // Note - This proc is only called from code that has already performed the
        //  necessary occupant guard checks. If you call this code yourself, please
        //  apply can_modify_occupant() or equivalent checks first.

        // Pre-calc the damage increase since we'll be using it in all the possible
        //  operations
        float damage_increase = rand(100 / Dm.pow(connected_scanner.damage_coeff, 2), 250 / Dm.pow(connected_scanner.damage_coeff, 2));

        if (Dm.eq(type, "ui"))
        {
            // GUARD CHECK - There's currently no way to save partial genetic data.
            //  However, if this is the case, we can't make a complete injector and
            //  this catches that edge case
            if (!Dm.truthy(buffer_slot["UI"]))
            {
                to_chat(usr, span_warning(I18n.T("Genetic data corrupted, unable to apply genetic data.")));
                return FALSE;
            }
            enzyme_copy_timer = world.time + ENZYME_COPY_BASE_COOLDOWN; // COOLDOWN_START, inline (rules 11.1 (a))
            scanner_occupant.dna.unique_identity = buffer_slot["UI"];
            await scanner_occupant.updateappearance(mutations_overlay_update: 1);
            await scanner_occupant.apply_status_effect(typeof(datum_status_effect_genetic_damage), damage_increase);
            await scanner_occupant.domutcheck();
            return TRUE;
        }
        if (Dm.eq(type, "uf"))
        {
            // GUARD CHECK - There's currently no way to save partial genetic data.
            //  However, if this is the case, we can't make a complete injector and
            //  this catches that edge case
            if (!Dm.truthy(buffer_slot["UF"]))
            {
                to_chat(usr, span_warning(I18n.T("Genetic data corrupted, unable to apply genetic data.")));
                return FALSE;
            }
            enzyme_copy_timer = world.time + ENZYME_COPY_BASE_COOLDOWN; // COOLDOWN_START, inline (rules 11.1 (a))
            scanner_occupant.dna.unique_features = buffer_slot["UF"];
            await scanner_occupant.updateappearance(mutcolor_update: 1, mutations_overlay_update: 1);
            await scanner_occupant.apply_status_effect(typeof(datum_status_effect_genetic_damage), damage_increase);
            await scanner_occupant.domutcheck();
            return TRUE;
        }
        if (Dm.eq(type, "ue"))
        {
            // GUARD CHECK - There's currently no way to save partial genetic data.
            //  However, if this is the case, we can't make a complete injector and
            //  this catches that edge case
            if (!Dm.truthy(buffer_slot["name"]) || !Dm.truthy(buffer_slot["UE"]) || !Dm.truthy(buffer_slot["blood_type"]))
            {
                to_chat(usr, span_warning(I18n.T("Genetic data corrupted, unable to apply genetic data.")));
                return FALSE;
            }
            enzyme_copy_timer = world.time + ENZYME_COPY_BASE_COOLDOWN; // COOLDOWN_START, inline (rules 11.1 (a))
            scanner_occupant.real_name = Dm.str(buffer_slot["name"]);
            scanner_occupant.name = Dm.str(buffer_slot["name"]);
            scanner_occupant.dna.unique_enzymes = buffer_slot["UE"];
            scanner_occupant.set_blood_type(buffer_slot["blood_type"]);
            await scanner_occupant.apply_status_effect(typeof(datum_status_effect_genetic_damage), damage_increase);
            await scanner_occupant.domutcheck();
            return TRUE;
        }
        if (Dm.eq(type, "mixed"))
        {
            // GUARD CHECK - There's currently no way to save partial genetic data.
            //  However, if this is the case, we can't make a complete injector and
            //  this catches that edge case
            if (!Dm.truthy(buffer_slot["UI"]) || !Dm.truthy(buffer_slot["name"]) || !Dm.truthy(buffer_slot["UE"]) || !Dm.truthy(buffer_slot["UF"]) || !Dm.truthy(buffer_slot["blood_type"]))
            {
                to_chat(usr, span_warning(I18n.T("Genetic data corrupted, unable to apply genetic data.")));
                return FALSE;
            }
            enzyme_copy_timer = world.time + ENZYME_COPY_BASE_COOLDOWN; // COOLDOWN_START, inline (rules 11.1 (a))
            scanner_occupant.dna.unique_identity = buffer_slot["UI"];
            scanner_occupant.dna.unique_features = buffer_slot["UF"];
            await scanner_occupant.updateappearance(mutcolor_update: 1, mutations_overlay_update: 1);
            scanner_occupant.real_name = Dm.str(buffer_slot["name"]);
            scanner_occupant.name = Dm.str(buffer_slot["name"]);
            scanner_occupant.dna.unique_enzymes = buffer_slot["UE"];
            scanner_occupant.set_blood_type(buffer_slot["blood_type"]);
            await scanner_occupant.apply_status_effect(typeof(datum_status_effect_genetic_damage), damage_increase);
            await scanner_occupant.domutcheck();
            return TRUE;
        }

        return FALSE;
    }

    /**
     * Checks if there is a connected DNA Scanner that is operational
     */
    // tg: code/game/machinery/computer/dna_console.dm:1769
    public virtual object scanner_operational()
    {
        return connected_scanner?.is_operational;
    }

    /**
     * Gets the damage coefficient of the connected DNA Scanner, or 1 if there isn't an operational one
     */
    // tg: code/game/machinery/computer/dna_console.dm:1775
    public virtual float get_injector_damage_coeff()
    {
        if (Dm.truthy(scanner_operational()))
            return connected_scanner.damage_coeff;
        return 1;
    }

    /**
     * Converts a string (from tgui) to a series of flags determine what we should put in a DNA Injector
     */
    // tg: code/game/machinery/computer/dna_console.dm:1798
    public virtual int dna_injector_type_to_flag(object injector_type = null)
    {
        if (Dm.eq(injector_type, "ui"))
            return DNA_INJECTOR_FLAG_UI;
        if (Dm.eq(injector_type, "ue"))
            return DNA_INJECTOR_FLAG_UE | DNA_INJECTOR_FLAG_NAME | DNA_INJECTOR_FLAG_BLOOD;
        if (Dm.eq(injector_type, "uf"))
            return DNA_INJECTOR_FLAG_UF | DNA_INJECTOR_FLAG_NAME;
        if (Dm.eq(injector_type, "mixed"))
            return (int)ALL;
        return (int)NONE;
    }

    /**
     * Pass an injector flag and a genetic makeup buffer slot to create a DNA Injector
     */
    // tg: code/game/machinery/computer/dna_console.dm:1813
    public virtual bool make_cosmetic_dna_injector(int dna_flag = 0, DmList buffer_slot = null)
    {
        buffer_slot ??= Dm.list();
        if (dna_flag == 0 || !Dm.truthy(length(buffer_slot)))
            return false;

        datum_dna stored_dna = Dm.@new<datum_dna>();

        if ((dna_flag & DNA_INJECTOR_FLAG_NAME) != 0)
        {
            if (!Dm.truthy(buffer_slot["name"]))
                return false;
            stored_dna.real_name = buffer_slot["name"];
        }

        if ((dna_flag & DNA_INJECTOR_FLAG_BLOOD) != 0)
        {
            if (!Dm.truthy(buffer_slot["blood_type"]))
                return false;
            // stored_dna.blood_type = buffer_slot["blood_type"]: the buffer keeps the blood type's name (:1253), which tg's
            // /datum/dna var holds untyped; the port's dna.blood_type is a /datum/blood_type, so the name is resolved here
            // (get_blood_type, as the disk's Initialize does at dna_scanner.dm:172; DV407)
            stored_dna.blood_type = get_blood_type(buffer_slot["blood_type"]) as datum_blood_type;
        }

        if ((dna_flag & DNA_INJECTOR_FLAG_UI) != 0)
        {
            if (!Dm.truthy(buffer_slot["UI"]))
                return false;
            stored_dna.unique_identity = buffer_slot["UI"];
        }

        if ((dna_flag & DNA_INJECTOR_FLAG_UE) != 0)
        {
            if (!Dm.truthy(buffer_slot["UE"]))
                return false;
            stored_dna.unique_enzymes = buffer_slot["UE"];
        }

        if ((dna_flag & DNA_INJECTOR_FLAG_UF) != 0)
        {
            if (!Dm.truthy(buffer_slot["UF"]))
                return false;
            stored_dna.unique_features = buffer_slot["UF"];
        }

        Dm.@new<obj_item_dnainjector_timed>(loc, stored_dna, get_injector_damage_coeff());
        return true;
    }

    /**
     * Checks if there is a valid DNA Scanner occupant for genetic modification
     *
     * Checks if there is a valid subject in the DNA Scanner that can be genetically
     * modified. Will set the scanner occupant var as part of this check.
     * Requires that the scanner can be operated and will return early if it can't
     */
    // tg: code/game/machinery/computer/dna_console.dm:1862
    public virtual bool can_modify_occupant()
    {
        // GUARD CHECK - We always want to perform the scanner operational check as
        //  part of checking if we can modify the occupant.
        // We can never modify the occupant of a broken scanner.
        if (!Dm.truthy(scanner_operational()))
            return false;

        if (connected_scanner.occupant == null)
            return false;

        // scanner_occupant = connected_scanner.occupant: a severed head or a brain (the scanner's occupant_typecache) is no
        // carbon, and tg's has_dna() call on it below is a runtime that ends the proc; the port answers FALSE for it (DV407)
        scanner_occupant = connected_scanner.occupant as mob_living_carbon;
        if (scanner_occupant == null)
            return false;

        // Check validity of occupent for DNA Modification
        // DNA Modification:
        //   requires DNA
        //    this DNA can not be bad
        //   is done via genetic damage bursts, so genetic damage immune carbons are not viable
        // And the DNA Scanner itself must have a valid scan level
        if (Dm.truthy(scanner_occupant.has_dna()) && !HAS_TRAIT(scanner_occupant, TRAIT_GENELESS) && !HAS_TRAIT(scanner_occupant, TRAIT_BADDNA) || Dm.eq(connected_scanner.scan_level, 3))
            return true;

        return false;
    }

    /**
     * Checks for adjacent DNA scanners and connects when it finds a viable one
     *
     * Seearches cardinal directions in order. Stops when it finds a viable DNA Scanner.
     * Will connect to a broken scanner if no functional scanner is available.
     * Links itself to the DNA Scanner to receive door open and close events.
     */
    // tg: code/game/machinery/computer/dna_console.dm:1893
    public virtual void connect_to_scanner()
    {
        obj_machinery_dna_scannernew test_scanner = null;
        obj_machinery_dna_scannernew broken_scanner = null;

        // Look in each cardinal direction and try and find a DNA Scanner
        //   If you find a DNA Scanner, check to see if it broken or working
        //   If it's working, set the current scanner and return early
        //   If it's not working, remember it anyway as a broken scanner
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            test_scanner = Dm.locate<obj_machinery_dna_scannernew>(get_step(this, Dm.num(direction)));
            if (test_scanner != null)
            {
                if (Dm.truthy(test_scanner.is_operational))
                {
                    set_connected_scanner(test_scanner);
                    return;
                }
                else
                    broken_scanner = test_scanner;
            }
        }

        // Ultimately, if we have a broken scanner, we'll attempt to connect to it as
        // a fallback case, but the code above will prefer a working scanner
        if (broken_scanner != null)
            set_connected_scanner(broken_scanner);
    }

    /**
     * Called by connected DNA Scanners when their doors close.
     *
     * Sets the new scanner occupant and completes delayed enzyme transfer if one
     * is queued.
     */
    // tg: code/game/machinery/computer/dna_console.dm:1922
    public virtual async DmTask on_scanner_close()
    {
        // Set the appropriate occupant now the scanner is closed
        if (connected_scanner.occupant != null)
            scanner_occupant = connected_scanner.occupant as mob_living_carbon;
        else
            scanner_occupant = null;

        // If we have a delayed action - In this case the only delayed action is
        //  applying a genetic makeup buffer the next time the DNA Scanner is closed -
        //  we want to perform it.
        // GUARD CHECK - Make sure we can modify the occupant, apply_genetic_makeup()
        //  assumes we've already done this.
        if (delayed_action != null && can_modify_occupant() && enzyme_copy_timer <= world.time) // COOLDOWN_FINISHED, inline (rules 11.1 (a))
        {
            object type = delayed_action["type"];
            DmList buffer_slot = delayed_action["buffer_slot"] as DmList;
            if (Dm.truthy(await apply_genetic_makeup(type, buffer_slot)))
                to_chat(connected_scanner.occupant, span_notice(I18n.T("[] activates!", this)));
            delayed_action = null;
        }
    }

    /**
     * Called by connected DNA Scanners when their doors open.
     *
     * Clears enzyme pulse operations, stops processing and nulls the current
     * scanner occupant var.
     */
    // tg: code/game/machinery/computer/dna_console.dm:1946
    public virtual void on_scanner_open()
    {
        // If we had a genetic damage pulse action ongoing, we want to stop this.
        // Imagine it being like a microwave stopping when you open the door.
        genetic_damage_pulse_index = 0;
        genetic_damage_pulse_timer = 0;
        end_processing();
        scanner_occupant = null;
    }

    /**
     * Builds the genetic makeup list which will be sent to tgui interface.
     */
    // tg: code/game/machinery/computer/dna_console.dm:1957
    public virtual void build_genetic_makeup_list()
    {
        // No code will ever null this list, we can safely Cut it.
        tgui_genetic_makeup.Cut();

        for (int i = 1; i <= NUMBER_OF_BUFFERS; i++)
        {
            if (genetic_makeup_buffer[i] is DmList slot)
                tgui_genetic_makeup[Dm.text("[]", i)] = slot.Copy();
            else
                tgui_genetic_makeup[Dm.text("[]", i)] = null;
        }
    }

    /**
     * Builds the genetic makeup list which will be sent to tgui interface.
     *
     * Will iterate over the connected scanner occupant, DNA Console, inserted
     * diskette and chromosomes and any advanced injectors, building the main data
     * structures which get passed to the tgui interface.
     */
    // tg: code/game/machinery/computer/dna_console.dm:1975
    public virtual void build_mutation_list(bool can_modify_occ = false)
    {
        // No code will ever null these lists. We can safely Cut them.
        tgui_occupant_mutations.Cut();
        tgui_diskette_mutations.Cut();
        tgui_console_mutations.Cut();
        tgui_console_chromosomes.Cut();
        tgui_advinjector_mutations.Cut();

        // ------------------------------------------------------------------------ //
        // GUARD CHECK - Can we genetically modify the occupant? This check will have
        //  previously included checks to make sure the DNA Scanner is still
        //  operational
        if (can_modify_occ)
        {
            // ---------------------------------------------------------------------- //
            // Start cataloguing all mutations that the occupant has by default
            foreach (object mutation_type in Dm.each(scanner_occupant.dna.mutation_index))
            {
                datum_mutation mutation = GET_INITIALIZED_MUTATION(mutation_type) as datum_mutation;

                DmList mutation_data = Dm.list();
                object text_sequence = scanner_occupant.dna.mutation_index[mutation_type];
                object default_sequence = scanner_occupant.dna.default_mutation_genes[mutation_type];
                bool discovered = stored_research != null && Dm.@in(mutation_type, stored_research.discovered_mutations);

                mutation_data["Alias"] = mutation.alias;
                mutation_data["Sequence"] = text_sequence;
                mutation_data["DefaultSeq"] = default_sequence;
                mutation_data["Discovered"] = discovered ? TRUE : FALSE;
                mutation_data["Source"] = "occupant";

                // We only want to pass this information along to the tgui interface if
                //  the mutation has been discovered. Prevents people being able to cheese
                //  or "hack" their way to figuring out what undiscovered mutations are
                if (discovered)
                {
                    mutation_data["Name"] = mutation.name;
                    mutation_data["Description"] = mutation.desc;
                    mutation_data["Instability"] = mutation.instability * GET_MUTATION_STABILIZER(mutation);
                    mutation_data["Quality"] = mutation.quality;
                }

                // Assume the mutation is normal unless assigned otherwise.
                object mut_class = SCANNER_MUTATION_CLASS_ACTIVATOR;

                // Check if the mutation is currently activated. If it is, we can add even
                //  MORE information to send to tgui.
                datum_mutation stored = scanner_occupant.dna.get_mutation(mutation_type) as datum_mutation;
                if (stored != null)
                {
                    mutation_data["Active"] = TRUE;
                    mutation_data["Scrambled"] = stored.scrambled;
                    mut_class = get_mutation_class(stored);
                    mutation_data["CanChromo"] = stored.can_chromosome;
                    mutation_data["ByondRef"] = REF(stored);
                    mutation_data["Type"] = stored.type;
                    if (Dm.truthy(stored.can_chromosome))
                    {
                        mutation_data["ValidChromos"] = jointext(stored.valid_chrom_list, ", ");
                        mutation_data["AppliedChromo"] = stored.chromosome_name;
                        mutation_data["ValidStoredChromos"] = build_chrom_list(stored);
                    }
                }
                else
                {
                    mutation_data["Active"] = FALSE;
                    mutation_data["Scrambled"] = FALSE;
                }
                mutation_data["Class"] = mut_class;

                // Technically NONE of these mutations should be MUTATION_SOURCE_MUTATOR but this will
                //  catch any weird edge cases
                // Assign icons by priority - MUTATION_SOURCE_MUTATOR will ALSO be discovered, so it
                //  has a higher priority for icon/image assignment
                if (Dm.eq(mut_class, SCANNER_MUTATION_CLASS_MUTATOR))
                    mutation_data["Image"] = "dna_extra.gif";
                else if (discovered)
                    mutation_data["Image"] = "dna_discovered.gif";
                else
                    mutation_data["Image"] = "dna_undiscovered.gif";

                tgui_occupant_mutations.Add(Dm.list(mutation_data));
            }

            // ---------------------------------------------------------------------- //
            // Now get additional/"extra" mutations that they shouldn't have by default
            foreach (datum_mutation mutation in Dm.each<datum_mutation>(scanner_occupant.dna.mutations))
            {
                // If it's in the mutation index array, we've already catalogued this
                //  mutation and can safely skip over it. It really shouldn't be, but this
                //  will catch any weird edge cases
                if (Dm.@in(mutation.type, scanner_occupant.dna.mutation_index))
                    continue;

                DmList mutation_data = Dm.list();
                object text_sequence = GET_SEQUENCE(mutation.type);

                // These will all be active mutations. They're added by injector and their
                //  sequencing code can't be changed. They can only be nullified, which
                //  completely removes them.
                datum_mutation initialized = GET_INITIALIZED_MUTATION(mutation.type) as datum_mutation;
                object mut_class = get_mutation_class(mutation);

                mutation_data["Alias"] = initialized.alias;
                mutation_data["Sequence"] = text_sequence;
                mutation_data["Discovered"] = TRUE;
                mutation_data["Quality"] = mutation.quality;
                mutation_data["Source"] = "occupant";

                mutation_data["Name"] = mutation.name;
                mutation_data["Description"] = mutation.desc;
                mutation_data["Instability"] = mutation.instability * GET_MUTATION_STABILIZER(mutation);

                mutation_data["Active"] = TRUE;
                mutation_data["Scrambled"] = mutation.scrambled;
                mutation_data["Class"] = mut_class;
                mutation_data["CanChromo"] = mutation.can_chromosome;
                mutation_data["ByondRef"] = REF(mutation);
                mutation_data["Type"] = mutation.type;

                if (Dm.truthy(mutation.can_chromosome))
                {
                    mutation_data["ValidChromos"] = jointext(mutation.valid_chrom_list, ", ");
                    mutation_data["AppliedChromo"] = mutation.chromosome_name;
                    mutation_data["ValidStoredChromos"] = build_chrom_list(mutation);
                }

                // Nothing in this list should be undiscovered. Technically nothing
                // should be anything but EXTRA. But we're just handling some edge cases.
                if (Dm.eq(mut_class, SCANNER_MUTATION_CLASS_MUTATOR))
                    mutation_data["Image"] = "dna_extra.gif";
                else
                    mutation_data["Image"] = "dna_discovered.gif";

                tgui_occupant_mutations.Add(Dm.list(mutation_data));
            }
        }

        // ------------------------------------------------------------------------ //
        // Build the list of mutations stored within the DNA Console
        foreach (datum_mutation mutation in Dm.each<datum_mutation>(stored_mutations))
        {
            DmList mutation_data = Dm.list();

            datum_mutation active = GET_INITIALIZED_MUTATION(mutation.type) as datum_mutation;

            mutation_data["Alias"] = active.alias;
            mutation_data["Name"] = mutation.name;
            mutation_data["Source"] = "console";
            mutation_data["Active"] = TRUE;
            mutation_data["Description"] = mutation.desc;
            mutation_data["Instability"] = mutation.instability * GET_MUTATION_STABILIZER(mutation);
            mutation_data["ByondRef"] = REF(mutation);
            mutation_data["Type"] = mutation.type;

            mutation_data["CanChromo"] = mutation.can_chromosome;
            if (Dm.truthy(mutation.can_chromosome))
            {
                mutation_data["ValidChromos"] = jointext(mutation.valid_chrom_list, ", ");
                mutation_data["AppliedChromo"] = mutation.chromosome_name;
                mutation_data["ValidStoredChromos"] = build_chrom_list(mutation);
            }

            tgui_console_mutations.Add(Dm.list(mutation_data));
        }

        // ------------------------------------------------------------------------ //
        // Build the list of chromosomes stored within the DNA Console
        int chrom_index = 1;
        foreach (obj_item_chromosome CM in Dm.each<obj_item_chromosome>(stored_chromosomes))
        {
            DmList chromo_data = Dm.list();

            chromo_data["Name"] = CM.name;
            chromo_data["Description"] = CM.desc;
            chromo_data["Index"] = chrom_index;

            tgui_console_chromosomes.Add(Dm.list(chromo_data));
            ++chrom_index;
        }

        // ------------------------------------------------------------------------ //
        // Build the list of mutations stored on any inserted diskettes
        if (diskette != null)
        {
            foreach (datum_mutation HM in Dm.each<datum_mutation>(diskette.mutations))
            {
                DmList mutation_data = Dm.list();

                datum_mutation A = GET_INITIALIZED_MUTATION(HM.type) as datum_mutation;

                mutation_data["Alias"] = A.alias;
                mutation_data["Name"] = HM.name;
                mutation_data["Active"] = TRUE;
                //mutation_data["Sequence"] = GET_SEQUENCE(HM.type)
                mutation_data["Source"] = "disk";
                mutation_data["Description"] = HM.desc;
                mutation_data["Instability"] = HM.instability * GET_MUTATION_STABILIZER(HM);
                mutation_data["ByondRef"] = REF(HM);
                mutation_data["Type"] = HM.type;

                mutation_data["CanChromo"] = HM.can_chromosome;
                if (Dm.truthy(HM.can_chromosome))
                {
                    mutation_data["ValidChromos"] = jointext(HM.valid_chrom_list, ", ");
                    mutation_data["AppliedChromo"] = HM.chromosome_name;
                    mutation_data["ValidStoredChromos"] = build_chrom_list(HM);
                }

                tgui_diskette_mutations.Add(Dm.list(mutation_data));
            }
        }

        // ------------------------------------------------------------------------ //
        // Build the list of mutations stored within any Advanced Injectors
        if (Dm.truthy(LAZYLEN(injector_selection)))
        {
            foreach (object I in Dm.each(injector_selection))
            {
                DmList mutations = Dm.list();
                foreach (datum_mutation HM in Dm.each<datum_mutation>(injector_selection[I]))
                {
                    DmList mutation_data = Dm.list();

                    datum_mutation A = GET_INITIALIZED_MUTATION(HM.type) as datum_mutation;

                    mutation_data["Alias"] = A.alias;
                    mutation_data["Name"] = HM.name;
                    mutation_data["Active"] = TRUE;
                    //mutation_data["Sequence"] = GET_SEQUENCE(HM.type)
                    mutation_data["Source"] = "injector";
                    mutation_data["Description"] = HM.desc;
                    mutation_data["Instability"] = HM.instability * GET_MUTATION_STABILIZER(HM);
                    mutation_data["ByondRef"] = REF(HM);
                    mutation_data["Type"] = HM.type;

                    if (Dm.truthy(HM.can_chromosome))
                        mutation_data["AppliedChromo"] = HM.chromosome_name;

                    mutations.Add(Dm.list(mutation_data));
                }
                tgui_advinjector_mutations.Add(Dm.list(Dm.list(
                    Dm.kv("name", Dm.text("[]", I)),
                    Dm.kv("mutations", mutations))));
            }
        }
    }

    /**
     * Takes any given chromosome and calculates chromosome compatibility
     *
     * Will iterate over the stored chromosomes in the DNA Console and will check
     * whether it can be applied to the supplied mutation. Then returns a list of
     * names of chromosomes that were compatible.
     *
     * Arguments:
     * * mutation - The mutation to check chromosome compatibility with
     */
    // tg: code/game/machinery/computer/dna_console.dm:2172
    public virtual DmList build_chrom_list(datum_mutation mutation = null)
    {
        DmList chromosomes = Dm.list();

        foreach (obj_item_chromosome CM in Dm.each<obj_item_chromosome>(stored_chromosomes))
        {
            if (Dm.truthy(CM.can_apply(mutation)))
                Dm.append(ref chromosomes, CM.name);
        }

        return chromosomes;
    }

    /// Returns a class declarations based on what sources the mutation has
    // tg: code/game/machinery/computer/dna_console.dm:2182
    public virtual object get_mutation_class(datum_mutation mutation = null)
    {
        if (Dm.truthy(length(Dm.sub(mutation.sources, Dm.list(MUTATION_SOURCE_MUTATOR, MUTATION_SOURCE_ACTIVATED))))) //It has other sources as well...
            return SCANNER_MUTATION_CLASS_OTHER;
        if (Dm.@in(MUTATION_SOURCE_ACTIVATED, mutation.sources)) //activated mutation priority over added ones.
            return SCANNER_MUTATION_CLASS_ACTIVATOR;
        if (Dm.@in(MUTATION_SOURCE_MUTATOR, mutation.sources))
            return SCANNER_MUTATION_CLASS_MUTATOR;
        return null;
    }

    /**
     * Checks whether a mutation alias has been discovered
     *
     * Checks whether a given mutation's genetic sequence has been completed and
     * discovers it if appropriate
     *
     * Arguments:
     * * alias - Alias of the mutation to check (ie "Mutation 51" or "Mutation 12")
     */
    // tg: code/game/machinery/computer/dna_console.dm:2200
    public virtual async DmTask<object> check_discovery(object alias = null)
    {
        // Note - All code paths that call this have already done checks on the
        //  current occupant to prevent cheese and other abuses. If you call this
        //  proc please also do the following checks first:
        // if(!can_modify_occupant())
        //   return
        // if(!(scanner_occupant == connected_scanner.occupant))
        //   return

        // Turn the alias ("Mutation 1", "Mutation 35") into a mutation path
        object path = GET_MUTATION_TYPE_FROM_ALIAS(alias);

        // Check to see if this mutation is in the active mutation list. If it isn't,
        //  then the mutation isn't eligible for discovery. If it is but is scrambled,
        //  then the mutation isn't eligible for discovery. Finally, check if the
        //  mutation is in discovered mutations - If it isn't, add it to discover.
        datum_mutation M = scanner_occupant.dna.get_mutation(path) as datum_mutation;
        if (M == null)
            return FALSE;
        if (Dm.truthy(M.scrambled))
            return FALSE;
        if (stored_research != null && !Dm.@in(path, stored_research.discovered_mutations))
        {
            datum_mutation HM = GET_INITIALIZED_MUTATION(path) as datum_mutation;
            Dm.append(ref stored_research.discovered_mutations, path);
            await say(I18n.T("Successfully discovered [].", HM.name));
            return TRUE;
        }

        return FALSE;
    }

    /**
     * Find a mutation from various storage locations via ATOM ref
     *
     * Takes an ATOM Ref and searches the appropriate mutation buffers and storage
     * vars to try and find the associated mutation.
     *
     * Arguments:
     * * ref - ATOM ref of the mutation to locate
     * * target_flags - Flags for storage mediums to search, see #defines
     */
    // tg: code/game/machinery/computer/dna_console.dm:2240
    public virtual datum_mutation get_mut_by_ref(object @ref = null, int target_flags = 0)
    {
        datum_mutation mutation;

        // Assume the occupant is valid and the check has been carried out before
        // calling this proc with the relevant flags.
        if ((target_flags & SEARCH_OCCUPANT) != 0)
        {
            mutation = locate_in(@ref, scanner_occupant.dna.mutations);
            if (mutation != null)
                return mutation;
        }

        if ((target_flags & SEARCH_STORED) != 0)
        {
            mutation = locate_in(@ref, stored_mutations);
            if (mutation != null)
                return mutation;
        }

        if (diskette != null && (target_flags & SEARCH_DISKETTE) != 0)
        {
            mutation = locate_in(@ref, diskette.mutations);
            if (mutation != null)
                return mutation;
        }

        if (injector_selection != null && (target_flags & SEARCH_ADV_INJ) != 0)
        {
            foreach (object I in Dm.each(injector_selection))
            {
                mutation = locate_in(@ref, injector_selection[Dm.text("[]", I)] as DmList);
                if (mutation != null)
                    return mutation;
            }
        }

        return null;
    }

    // (locate(ref) in L): the mutation whose ref is the text, when the list holds it
    private static datum_mutation locate_in(object @ref, DmList list)
    {
        return Dm.locate_ref(@ref) is datum_mutation found && Dm.@in(found, list) ? found : null;
    }

    /**
     * Creates a randomised accuracy value for the enzyme pulse functionality.
     *
     * Donor code from previous DNA Console iteration.
     *
     * Arguments:
     * * position - Index of the intended enzyme element to pulse
     * * pulse_duration - Duration of intended genetic damage pulse
     * * number_of_blocks - Number of individual data blocks in the pulsed enzyme
     */
    // tg: code/game/machinery/computer/dna_console.dm:2283
    public virtual float randomize_GENETIC_DAMAGE_accuracy(float position = 0, float pulse_duration = 0, float number_of_blocks = 0)
    {
        float val = round(gaussian(0, GENETIC_DAMAGE_ACCURACY_MULTIPLIER / pulse_duration) + position, 1);
        return WRAP(val, 1, number_of_blocks + 1);
    }

    /**
     * Scrambles an enzyme element value for the enzyme pulse functionality.
     *
     * Donor code from previous DNA Console iteration.
     *
     * Arguments:
     * * input - Enzyme identity element to scramble, expected hex value
     * * rs - Strength of genetic damage pulse, increases the range of possible outcomes
     */
    // tg: code/game/machinery/computer/dna_console.dm:2296
    public virtual string scramble(object input = null, float rs = 0, params object[] __extra)
    {
        float len = length(input);
        float ran = gaussian(0, rs * GENETIC_DAMAGE_STRENGTH_MULTIPLIER);
        if (ran == 0)
            ran = Dm.num(pick(-1, 1)); //hacky, statistically should almost never happen. 0-chance makes people mad though
        else if (ran < 0)
            ran = round(ran); //negative, so floor it
        else
            ran = -round(-ran); //positive, so ceiling it
        return num2hex(WRAP(Dm.num(hex2num(input)) + ran, 0, Dm.pow(16, len)), len);
    }

    /**
     * Performs the enzyme genetic damage pulse.
     *
     * Donor code from previous DNA Console iteration. Called from process() when
     * there is a genetic damage pulse in progress. Ends processing.
     */
    // tg: code/game/machinery/computer/dna_console.dm:2313
    public virtual async DmTask genetic_damage_pulse()
    {
        // GUARD CHECK - Can we genetically modify the occupant? Includes scanner
        //  operational guard checks.
        // If we can't, abort the procedure.
        if (!can_modify_occupant() || (!Dm.eq(genetic_damage_pulse_type, GENETIC_DAMAGE_PULSE_UNIQUE_IDENTITY) && !Dm.eq(genetic_damage_pulse_type, GENETIC_DAMAGE_PULSE_UNIQUE_FEATURES)))
        {
            genetic_damage_pulse_index = 0;
            end_processing();
            return;
        }

        float len = 0;
        if (Dm.eq(genetic_damage_pulse_type, GENETIC_DAMAGE_PULSE_UNIQUE_IDENTITY))
            len = length(scanner_occupant.dna.unique_identity);
        else if (Dm.eq(genetic_damage_pulse_type, GENETIC_DAMAGE_PULSE_UNIQUE_FEATURES))
            len = length(scanner_occupant.dna.unique_features);

        float num = randomize_GENETIC_DAMAGE_accuracy(genetic_damage_pulse_index, pulse_duration + Dm.pow(connected_scanner.precision_coeff, 2), len); //Each manipulator level above 1 makes randomization as accurate as selected time + manipulator lvl^2  //Value is this high for the same reason as with laser - not worth the hassle of upgrading if the bonus is low

        string hex = null;
        if (Dm.eq(genetic_damage_pulse_type, GENETIC_DAMAGE_PULSE_UNIQUE_IDENTITY))
            hex = copytext(scanner_occupant.dna.unique_identity, num, num + 1);
        else if (Dm.eq(genetic_damage_pulse_type, GENETIC_DAMAGE_PULSE_UNIQUE_FEATURES))
            hex = copytext(scanner_occupant.dna.unique_features, num, num + 1);

        hex = scramble(hex, pulse_strength, pulse_duration);

        if (Dm.eq(genetic_damage_pulse_type, GENETIC_DAMAGE_PULSE_UNIQUE_IDENTITY))
            scanner_occupant.dna.unique_identity = Dm.text("[][][]", copytext(scanner_occupant.dna.unique_identity, 1, num), hex, copytext(scanner_occupant.dna.unique_identity, num + 1));
        else if (Dm.eq(genetic_damage_pulse_type, GENETIC_DAMAGE_PULSE_UNIQUE_FEATURES))
            scanner_occupant.dna.unique_features = Dm.text("[][][]", copytext(scanner_occupant.dna.unique_features, 1, num), hex, copytext(scanner_occupant.dna.unique_features, num + 1));
        await scanner_occupant.updateappearance(mutcolor_update: 1, mutations_overlay_update: 1);

        genetic_damage_pulse_index = 0;
        genetic_damage_pulse_type = null;
        end_processing();
    }

    /**
     * Sets the default state for the tgui interface.
     */
    // tg: code/game/machinery/computer/dna_console.dm:2352
    public virtual void set_default_state()
    {
        tgui_view_state["consoleMode"] = "storage";
        tgui_view_state["storageMode"] = "console";
        tgui_view_state["storageConsSubMode"] = "mutations";
        tgui_view_state["storageDiskSubMode"] = "mutations";
    }

    /**
     * Ejects the DNA Disk from the console.
     *
     * Will insert into the user's hand if possible, otherwise will drop it at the
     * console's location.
     *
     * Arguments:
     * * user - The mob that is attempting to eject the diskette.
     */
    // tg: code/game/machinery/computer/dna_console.dm:2368
    public virtual async DmTask eject_disk(mob user = null)
    {
        // Check for diskette.
        if (diskette == null)
            return;

        to_chat(user, span_notice(I18n.T("You eject [] from [].", diskette, this)));

        // Reset the state to console storage.
        tgui_view_state["storageMode"] = "console";

        // If the disk shouldn't pop into the user's hand for any reason, drop it on the console instead.
        if (user == null || !Dm.truthy(await Adjacent(user)) || !Dm.truthy(await user.put_in_active_hand(diskette)))
            await diskette.forceMove(drop_location() as atom);
        diskette = null;
    }

    // tg: code/game/machinery/computer/dna_console.dm:2384
    public virtual void set_connected_scanner(obj_machinery_dna_scannernew new_scanner = null)
    {
        if (connected_scanner != null)
        {
            UnregisterSignal(connected_scanner, COMSIG_QDELETING);
            if (connected_scanner.linked_console == this)
                connected_scanner.set_linked_console(null);
        }
        connected_scanner = new_scanner;
        if (connected_scanner != null)
        {
            RegisterSignal(connected_scanner, COMSIG_QDELETING, nameof(react_to_scanner_del));
            connected_scanner.set_linked_console(this);
        }
    }

    // tg: code/game/machinery/computer/dna_console.dm:2394
    [SignalHandler]
    public virtual void react_to_scanner_del(datum source = null)
    {
        set_connected_scanner(null);
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/computer/dna_console.dm:423  var/static/list/gene_letters
    internal DmList obj_machinery_computer_dna_console__ui_act__gene_letters;
}
