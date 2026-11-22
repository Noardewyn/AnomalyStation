// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/atoms.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/controllers/subsystem/atoms.dm:1 SUBSYSTEM_DEF(atoms)
[DmPath("/datum/controller/subsystem/atoms")]
public partial class datum_controller_subsystem_atoms : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/atoms.dm:2
    public datum_controller_subsystem_atoms()
    {
        name = "Atoms";
        ss_flags = SS_NO_FIRE;
        initialized = INITIALIZATION_INSSATOMS;
    }

    /// A stack of list(source, desired initialized state)
    /// We read the source of init changes from the last entry, and assert that all changes will come with a reset
    // tg: code/controllers/subsystem/atoms.dm:13
    public DmList initialized_state;
    // tg: code/controllers/subsystem/atoms.dm:14
    public object base_initialized;

    /// How many InitializeAtoms calls are still creating atoms, including ones asleep in CreateAtoms
    // tg: code/controllers/subsystem/atoms.dm:17
    public float active_initializations = 0;

    // tg: code/controllers/subsystem/atoms.dm:19
    public DmList late_loaders;

    // tg: code/controllers/subsystem/atoms.dm:21
    public DmList BadInitializeCalls;

    ///initAtom() adds the atom its creating to this list iff InitializeAtoms() has been given a list to populate as an argument
    // tg: code/controllers/subsystem/atoms.dm:24
    public DmList created_atoms;

    /// Atoms that will be deleted once the subsystem is initialized
    // tg: code/controllers/subsystem/atoms.dm:27
    public DmList queued_deletions;

    // tg: code/controllers/subsystem/atoms.dm:29
    public object init_start_time;

    // tg: #ifdef PROFILE_MAPLOAD_INIT_ATOM ... #endif not translated (inactive in production)

    // tg: code/controllers/subsystem/atoms.dm:3, :13, :19, :21, :27 (object-creating defaults, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        dependencies = Dm.list(
            typeof(datum_controller_subsystem_processing_reagents),
            typeof(datum_controller_subsystem_fluids),
            typeof(datum_controller_subsystem_mapping),
            typeof(datum_controller_subsystem_job));
        initialized_state = Dm.list();
        late_loaders = Dm.list();
        BadInitializeCalls = Dm.list();
        queued_deletions = Dm.list();
    }

    // The New() that SUBSYSTEM_DEF(atoms) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSatoms) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/atoms.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSatoms != this)
        {
            if (SSatoms is datum_controller_subsystem_atoms)
            {
                Recover();
                qdel(SSatoms);
            }
            SSatoms = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/atoms.dm:37
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        init_start_time = world.time;
        await setupGenetics(); //to set the mutations' sequence

        initialized = INITIALIZATION_INNEW_MAPLOAD;
        InitializeAtoms();
        initialized = INITIALIZATION_INNEW_REGULAR;

        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/atoms.dm:52  var/static/uid = 0 (a proc-level static, rules 10.6)
    private static ref float InitializeAtoms__uid => ref TgState.Current.datum_controller_subsystem_atoms__InitializeAtoms__uid;

    // tg: code/controllers/subsystem/atoms.dm:47
    public virtual void InitializeAtoms(DmList atoms = null, DmList atoms_to_return = null)
    {
        if (initialized == INITIALIZATION_INSSATOMS)
            return;

        // Generate a unique mapload source for this run of InitializeAtoms
        InitializeAtoms__uid = WRAP_UID(InitializeAtoms__uid + 1);
        string source = Dm.text("subsystem init []", InitializeAtoms__uid);
        set_tracked_initalized(INITIALIZATION_INNEW_MAPLOAD, source);

        // This may look a bit odd, but if the actual atom creation runtimes for some reason, we absolutely need to set initialized BACK
        active_initializations++;
        CreateAtoms(atoms, atoms_to_return, source);
        active_initializations--;
        clear_tracked_initalize(source);
        SSicon_smooth.free_deferred(source);

        if (Dm.truthy(late_loaders.len))
        {
            for (float I = 1, I__end = late_loaders.len; I <= I__end; I++)
            {
                atom A = late_loaders[I] as atom;
                //I hate that we need this
                if (QDELETED(A))
                    continue;
                try { A.LateInitialize(); }
                catch (Exception e) { World.Current.ReportRuntime(e); } // isolation boundary (translation-rules.md 9.2)
            }
            // testing("Late initialized [late_loaders.len] atoms") expands to nothing in production (__HELPERS/logging/_logging.dm:49)
            late_loaders.Cut();
        }

        if (Dm.truthy(created_atoms))
        {
            Dm.append(ref atoms_to_return, created_atoms);
            created_atoms = null;
        }

        foreach (object queued_deletion in Dm.each(queued_deletions))
            qdel(queued_deletion);

        // testing("[queued_deletions.len] atoms were queued for deletion.") expands to nothing in production (__HELPERS/logging/_logging.dm:49)
        queued_deletions.Cut();

        // tg: #ifdef PROFILE_MAPLOAD_INIT_ATOM ... #endif not translated (inactive in production)
    }

    /// Actually creates the list of atoms. Exists solely so a runtime in the creation logic doesn't cause initialized to totally break
    // tg: code/controllers/subsystem/atoms.dm:89
    public virtual void CreateAtoms(DmList atoms = null, DmList atoms_to_return = null, object mapload_source = null)
    {
        // DM: a runtime ends only this proc and InitializeAtoms goes on to put initialized back: CreateAtoms "Exists
        // solely so a runtime in the creation logic doesn't cause initialized to totally break" (atoms.dm:57, :88). The
        // proc-level catch of rules 9.3 keeps that under DV1.
        try
        {
            if (Dm.truthy(atoms_to_return))
                LAZYINITLIST(ref created_atoms);

            // tg: #ifdef TESTING ... #endif not translated (inactive in production)

            DmList mapload_arg = Dm.list(TRUE);

            if (Dm.truthy(atoms))
            {
                // tg: #ifdef TESTING ... #endif not translated (inactive in production)

                for (float I = 1, I__end = atoms.len; I <= I__end; I++)
                {
                    atom A = atoms[I] as atom;
                    if (!Dm.truthy(Dm.bitand(A.flags_1, INITIALIZED_1)))
                    {
                        // Unrolled CHECK_TICK setup to let us enable/disable mapload based off source
                        if (TICK_CHECK())
                        {
                            clear_tracked_initalize(mapload_source);
                            Waiting.NoSleep(stoplag());
                            if (Dm.truthy(mapload_source))
                                set_tracked_initalized(INITIALIZATION_INNEW_MAPLOAD, mapload_source);
                        }
                        // PROFILE_INIT_ATOM_BEGIN() expands to nothing (PROFILE_MAPLOAD_INIT_ATOM is undefined, __DEFINES/_atoms.dm:10)
                        InitAtom(A, TRUE, mapload_arg);
                        // PROFILE_INIT_ATOM_END(A) expands to nothing (__DEFINES/_atoms.dm:11)
                    }
                }
            }
            else
            {
                // tg: #ifdef TESTING ... #endif not translated (inactive in production)

                foreach (atom A in Dm.each_cast<atom>(world))
                {
                    if (!Dm.truthy(Dm.bitand(A.flags_1, INITIALIZED_1)))
                    {
                        // PROFILE_INIT_ATOM_BEGIN() expands to nothing (PROFILE_MAPLOAD_INIT_ATOM is undefined, __DEFINES/_atoms.dm:10)
                        InitAtom(A, FALSE, mapload_arg);
                        // PROFILE_INIT_ATOM_END(A) expands to nothing (__DEFINES/_atoms.dm:11)
                        // tg: #ifdef TESTING ... #endif not translated (inactive in production)
                        if (TICK_CHECK())
                        {
                            clear_tracked_initalize(mapload_source);
                            Waiting.NoSleep(stoplag());
                            if (Dm.truthy(mapload_source))
                                set_tracked_initalized(INITIALIZATION_INNEW_MAPLOAD, mapload_source);
                        }
                    }
                }
            }

            // testing("Initialized [count] atoms") expands to nothing in production (__HELPERS/logging/_logging.dm:49)
        }
        catch (Exception e) { World.Current.ReportRuntime(e); return; } // DM: a runtime ends only CreateAtoms (rules 9.3)
    }

    // tg: code/controllers/subsystem/atoms.dm:137
    public virtual void map_loader_begin(object source = null)
    {
        set_tracked_initalized(INITIALIZATION_INSSATOMS, source);
    }

    // tg: code/controllers/subsystem/atoms.dm:140
    public virtual void map_loader_stop(object source = null)
    {
        clear_tracked_initalize(source);
    }

    /// Returns the source currently modifying SSatom's init behavior
    // tg: code/controllers/subsystem/atoms.dm:144
    public virtual object get_initialized_source()
    {
        float state_length = length(initialized_state);
        if (!Dm.truthy(state_length))
            return null;
        return ((DmList)initialized_state[state_length])[1];
    }

    /// Use this to set initialized to prevent error states where the old initialized is overridden, and we end up losing all context
    /// Accepts a state and a source, the most recent state is used, sources exist to prevent overriding old values accidentally
    // tg: code/controllers/subsystem/atoms.dm:152
    public virtual void set_tracked_initalized(object state = null, object source = null)
    {
        if (!Dm.truthy(length(initialized_state)))
            base_initialized = initialized;
        Dm.append(ref initialized_state, Dm.list(Dm.list(source, state)));
        initialized = Dm.num(state);
    }

    // tg: code/controllers/subsystem/atoms.dm:158
    public virtual void clear_tracked_initalize(object source = null)
    {
        if (!Dm.truthy(length(initialized_state)))
            return;
        for (float i = length(initialized_state), i__end = 1; i >= i__end; i += -1)
        {
            if (Dm.eq(((DmList)initialized_state[i])[1], source))
            {
                initialized_state.Cut(i, i + 1);
                break;
            }
        }

        if (!Dm.truthy(length(initialized_state)))
        {
            initialized = Dm.num(base_initialized);
            base_initialized = INITIALIZATION_INNEW_REGULAR;
            return;
        }

        initialized = Dm.num(((DmList)initialized_state[length(initialized_state)])[2]);
    }

    /// Returns TRUE if anything is currently being initialized
    // tg: code/controllers/subsystem/atoms.dm:174
    public virtual object initializing_something()
    {
        return Dm.num(active_initializations > 0);
    }

    // tg: code/controllers/subsystem/atoms.dm:177
    public override void Recover()
    {
        Dm.NotTranslated("code/controllers/subsystem/atoms.dm:177", "Recover: not translated (translation-rules.md 13)");
    }

    // tg: code/controllers/subsystem/atoms.dm:184
    public virtual async DmTask setupGenetics()
    {
        // Gaps P3 (D327): every mutation type is translated, so the loop makes one of each, as tg does (DV25 no longer wraps it).
        DmList mutations = subtypesof(typeof(datum_mutation));
        shuffle_inplace(mutations);
        for (float i = 1, i__end = LAZYLEN(mutations); i <= i__end; i++)
        {
            object path = mutations[i]; //byond gets pissy when we do it in one line
            datum_mutation B = (await Dm.new_async(path as Type)) as datum_mutation;
            B.alias = Dm.text("Mutation []", i);
            GLOB.all_mutations[B.type] = B;
            GLOB.full_sequences[B.type] = generate_gene_sequence(B.blocks);
            GLOB.alias_mutations[B.alias] = B.type;
            if (Dm.truthy(B.locked))
                continue;
            if (Dm.eq(B.quality, POSITIVE))
                Dm.combine(ref GLOB.good_mutations, B);
            else if (Dm.eq(B.quality, NEGATIVE))
                Dm.combine(ref GLOB.bad_mutations, B);
            else if (Dm.eq(B.quality, MINOR_NEGATIVE))
                Dm.combine(ref GLOB.not_good_mutations, B);
            CHECK_TICK();
        }
    }

    // tg: code/controllers/subsystem/atoms.dm:204
    public virtual object InitLog()
    {
        object dot = null;
        dot = "";
        foreach (object path in Dm.each(BadInitializeCalls))
        {
            Dm.append(ref dot, Dm.text("Path : [] \n", path));
            object fails = BadInitializeCalls[path];
            if (Dm.truthy(Dm.bitand(fails, BAD_INIT_DIDNT_INIT)))
                Dm.append(ref dot, "- Didn't call atom/Initialize(mapload)\n");
            if (Dm.truthy(Dm.bitand(fails, BAD_INIT_NO_HINT)))
                Dm.append(ref dot, "- Didn't return an Initialize hint\n");
            if (Dm.truthy(Dm.bitand(fails, BAD_INIT_QDEL_BEFORE)))
                Dm.append(ref dot, "- Qdel'd before Initialize proc ran\n");
            if (Dm.truthy(Dm.bitand(fails, BAD_INIT_SLEPT)))
                Dm.append(ref dot, "- Slept during Initialize()\n");
        }
        return dot;
    }

    /// Prepares an atom to be deleted once the atoms SS is initialized.
    // tg: code/controllers/subsystem/atoms.dm:219
    public virtual void prepare_deletion(atom target = null)
    {
        if (initialized == INITIALIZATION_INNEW_REGULAR)
            // Atoms SS has already completed, just kill it now.
            qdel(target);
        else
            Dm.append(ref queued_deletions, WEAKREF(target));
    }

    // tg: code/controllers/subsystem/atoms.dm:226
    public override async DmTask Shutdown()
    {
        object initlog = InitLog();
        if (Dm.truthy(initlog))
            text2file(initlog, Dm.text("[]/initialize.log", GLOB.log_directory));
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/atoms.dm:1  SUBSYSTEM_DEF(atoms): GLOBAL_REAL(SSatoms, /datum/controller/subsystem/atoms)
    internal datum_controller_subsystem_atoms SSatoms;
    // tg: code/controllers/subsystem/atoms.dm:52  /datum/controller/subsystem/atoms/proc/InitializeAtoms: var/static/uid = 0
    internal float datum_controller_subsystem_atoms__InitializeAtoms__uid = 0;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_atoms SSatoms => ref TgState.Current.SSatoms;
}
