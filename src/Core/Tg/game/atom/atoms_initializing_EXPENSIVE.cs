// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/atoms_initializing_EXPENSIVE.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:2 (interleaved types, rules 1.9: SSatoms, then /atom)
public partial class datum_controller_subsystem_atoms : datum_controller_subsystem
{
    /// Init this specific atom
    // tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:2
    public virtual object InitAtom(atom A = null, float? from_template = null, DmList arguments = null)
    {
        from_template ??= FALSE;

        object the_type = A.type;

        if (QDELING(A))
        {
            // Check init_start_time to not worry about atoms created before the atoms SS that are cleaned up before this
            if (Dm.gt(A.gc_destroyed, init_start_time))
                Dm.combine_at(BadInitializeCalls, the_type, BAD_INIT_QDEL_BEFORE);
            return TRUE;
        }

        // This is handled and battle tested by dreamchecker. Limit to UNIT_TESTS just in case that ever fails.
        // tg: #ifdef UNIT_TESTS ... #endif not translated (inactive in production)

        object result = null;
        // PORT(D112): an inert atom (InertSeam, docs/ai/specs/types-converter.md section 7) runs only the presentation part
        // of /atom/Initialize (atom.dm_inert_initialize) where tg calls its Initialize.
        try { result = InertSeam.IsInert(A) ? A.dm_inert_initialize(arguments) : Waiting.NoSleep(Dm.Call(A, nameof(atom.Initialize), Dm.arglist(arguments))); }
        catch (Exception e) { World.Current.ReportRuntime(e); result = null; } // isolation boundary (translation-rules.md 9.2)

        // tg: #ifdef UNIT_TESTS ... #endif not translated (inactive in production)

        float qdeleted = FALSE;

        object sw__1 = result;
        if (Dm.eq(sw__1, INITIALIZE_HINT_NORMAL))
        {
            ; // EMPTY_BLOCK_GUARD (__DEFINES/_helpers.dm:51) // Pass
        }
        else if (Dm.eq(sw__1, INITIALIZE_HINT_LATELOAD))
        {
            if (Dm.truthy(arguments[1])) //mapload
                Dm.append(ref late_loaders, A);
            else
                A.LateInitialize();
        }
        else if (Dm.eq(sw__1, INITIALIZE_HINT_QDEL))
        {
            qdel(A);
            qdeleted = TRUE;
        }
        else
            Dm.combine_at(BadInitializeCalls, the_type, BAD_INIT_NO_HINT);

        // The port has no hard delete (DV7), so A is never null here (scope R18); the check stays as tg wrote it.
        if (!Dm.truthy(A)) //possible harddel
            qdeleted = TRUE;
        else if (!Dm.truthy(Dm.bitand(A.flags_1, INITIALIZED_1)))
            Dm.combine_at(BadInitializeCalls, the_type, BAD_INIT_DIDNT_INIT);
        else
        {
            if (Dm.truthy(A._listen_lookup?[COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZE]))
                A._SendSignal(COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZE, Dm.list(A));
            if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_ATOM_AFTER_POST_INIT]))
                SSdcs._SendSignal(COMSIG_GLOB_ATOM_AFTER_POST_INIT, Dm.list(SSdcs, A));
            atom location = A.loc;
            if (Dm.truthy(location))
            {
                /// Sends a signal that the new atom `src`, has been created at `loc`
                if (Dm.truthy(location._listen_lookup?[COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON]))
                    location._SendSignal(COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, Dm.list(location, A, arguments[1]));
            }
            if (Dm.truthy(created_atoms) && Dm.truthy(from_template) && ispath(the_type, typeof(atom_movable)))//we only want to populate the list with movables
                Dm.append(ref created_atoms, A.get_all_contents());
        }

        return (qdeleted is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.num(QDELING(A)));
    }
}

// tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:65
public partial class atom : datum
{
    /**
     * Called when an atom is created in byond (built in engine proc)
     *
     * Not a lot happens here in SS13 code, as we offload most of the work to the
     * [Initialization][/atom/proc/Initialize] proc, mostly we run the preloader
     * if the preloader is being used and then call [InitAtom][/datum/controller/subsystem/atoms/proc/InitAtom] of which the ultimate
     * result is that the Initialize proc is called.
     *
     */
    // tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:65
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        //atom creation method that preloads variables at creation
        if (Dm.truthy(GLOB.use_preloader) && Dm.eq(this.type, GLOB._preloader_path))//in case the instantiated atom is creating other atoms in New()
            world.preloader_load(this);

        // rules 10.6 (as in meta_gas_list): tg's Master creates every subsystem before GLOB, so SSatoms is never null here; the
        // port's test harnesses build GLOB first, and GLOB's gas overlays (generate_gas_overlays) are atoms made then: with no
        // SSatoms the atom waits for SSatoms' init, as it would before SSatoms' Initialize (INITIALIZATION_INSSATOMS).
        float do_initialize = SSatoms?.initialized ?? INITIALIZATION_INSSATOMS;
        DmList args = Dm.args(Dm.forward(__extra, loc));
        if (do_initialize != INITIALIZATION_INSSATOMS)
        {
            args[1] = Dm.num(do_initialize == INITIALIZATION_INNEW_MAPLOAD);
            if (Dm.truthy(SSatoms.InitAtom(this, FALSE, args)))
                //we were deleted
                return;
        }
    }

    /**
     * The primary method that objects are setup in SS13 with
     *
     * we don't use New as we have better control over when this is called and we can choose
     * to delay calls or hook other logic in and so forth
     *
     * During roundstart map parsing, atoms are queued for initialization in the base atom/New(),
     * After the map has loaded, then Initialize is called on all atoms one by one. NB: this
     * is also true for loading map templates as well, so they don't Initialize until all objects
     * in the map file are parsed and present in the world
     *
     * If you're creating an object at any point after SSInit has run then this proc will be
     * immediately be called from New.
     *
     * mapload: This parameter is true if the atom being loaded is either being initialized during
     * the Atom subsystem initialization, or if the atom is being loaded from the map template.
     * If the item is being created at runtime any time after the Atom subsystem is initialized then
     * it's false.
     *
     * The mapload argument occupies the same position as loc when Initialize() is called by New().
     * loc will no longer be needed after it passed New(), and thus it is being overwritten
     * with mapload at the end of atom/New() before this proc (atom/Initialize()) is called.
     *
     * You must always call the parent of this proc, otherwise failures will occur as the item
     * will not be seen as initialized (this can lead to all sorts of strange behaviour, like
     * the item being completely unclickable)
     *
     * You must not sleep in this proc, or any subprocs
     *
     * Any parameters from new are passed through (excluding loc), naturally if you're loading from a map
     * there are no other arguments
     *
     * Must return an [initialization hint][INITIALIZE_HINT_NORMAL] or a runtime will occur.
     *
     * Note: the following functions don't call the base for optimization and must copypasta handling:
     * * [/turf/proc/Initialize]
     * * [/turf/open/space/proc/Initialize]
     */
    // tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:115
    [ShouldNotSleep, ShouldCallParent]
    public virtual object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
            stack_trace(Dm.text("Warning: []([]) initialized multiple times!", this, type), "code/game/atom/atoms_initializing_EXPENSIVE.dm", 120);
        Dm.combine(ref flags_1, INITIALIZED_1);

        SET_PLANE_IMPLICIT(this, plane);

        if (Dm.truthy(LAZYLEN(hud_possible)))
            hud_possible = string_assoc_list(hud_possible) as DmList;

        if (Dm.truthy(greyscale_config) && Dm.truthy(greyscale_colors)) //we'll check again at item/init for inhand/belt/worn configs.
            Waiting.NoSleep(update_greyscale());

        //atom color stuff
        if (Dm.truthy(color))
            Waiting.NoSleep(add_atom_colour(color, FIXED_COLOUR_PRIORITY));

        if (light_system == COMPLEX_LIGHT && Dm.truthy(light_power) && Dm.truthy(light_range))
            update_light();

        if (Dm.truthy(post_init_icon_state))
            icon_state = (string)post_init_icon_state;

        // SETUP_SMOOTHING() (__DEFINES/icon_smoothing.dm:236), expanded inline (rules 11.1 (d): it reads src's vars)
        if (Dm.truthy(smoothing_groups))
        {
            // PARSE_SMOOTHING_GROUPS(smoothing_groups, smoothing_groups) (__DEFINES/icon_smoothing.dm:243)
            if (Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(focus_only/sorted_smoothing_groups) is FALSE in production (rules 1.7)
            {
                // ASSERT_SORTED_SMOOTHING_GROUPS(smoothing_groups) (__DEFINES/icon_smoothing.dm:265)
                DmList unwrapped = json_decode(Dm.text("\\[[]0\\]", smoothing_groups)) as DmList;
                unwrapped.len--;
                assert_sorted(unwrapped, Dm.text("smoothing_groups ([])", type));
            }
            SET_SMOOTHING_GROUPS(smoothing_groups, ref smoothing_groups);
        }
        if (Dm.truthy(canSmoothWith))
        {
            // PARSE_CAN_SMOOTH_WITH(canSmoothWith, canSmoothWith, smoothing_flags) (__DEFINES/icon_smoothing.dm:250)
            if (Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(focus_only/sorted_smoothing_groups) is FALSE in production (rules 1.7)
            {
                // ASSERT_SORTED_SMOOTHING_GROUPS(canSmoothWith) (__DEFINES/icon_smoothing.dm:265)
                DmList unwrapped = json_decode(Dm.text("\\[[]0\\]", canSmoothWith)) as DmList;
                unwrapped.len--;
                assert_sorted(unwrapped, Dm.text("canSmoothWith ([])", type));
            }
            /* S_OBJ is always negative, and we are guaranteed to be sorted. */
            // SHIM-GAP: BYOND 515 text indexing (canSmoothWith[1], the character at a position) is not in the shim;
            // copytext(canSmoothWith, 1, 2) gives the same one-character text.
            if (Dm.eq(copytext(canSmoothWith, 1, 2), "-"))
                Dm.combine(ref smoothing_flags, SMOOTH_OBJ);
            SET_SMOOTHING_GROUPS(canSmoothWith, ref canSmoothWith);
        }

        if (Dm.truthy(uses_integrity))
            atom_integrity = max_integrity;
        // TEST_ONLY_ASSERT((!armor || istype(armor)), ...) expands to nothing in production (__DEFINES/unit_tests.dm:19)

        // apply materials properly from the default custom_materials value
        // This MUST come after atom_integrity is set above, as if old materials get removed,
        // atom_integrity is checked against max_integrity and can BREAK the atom.
        // The integrity to max_integrity ratio is still preserved.
        if (Dm.truthy(custom_materials))
            Waiting.NoSleep(initialize_materials(custom_materials));

        if (ispath(ai_controller))
            ai_controller = Dm.@new(ai_controller as Type, this);

        Waiting.NoSleep(setup_reskins());

        return INITIALIZE_HINT_NORMAL;
    }

    /**
     * Late Initialization, for code that should run after all atoms have run Initialization
     *
     * To have your LateIntialize proc be called, your atoms [Initialization][/atom/proc/Initialize]
     *  proc must return the hint
     * [INITIALIZE_HINT_LATELOAD] otherwise it will never be called.
     *
     * useful for doing things like finding other machines on GLOB.machines because you can guarantee
     * that all atoms will actually exist in the "WORLD" at this time and that all their Initialization
     * code has been run
     */
    // tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:172
    // set waitfor = FALSE: this body cannot sleep, so it needs no wrapper (waiting-model.md 5.4); the overrides that may
    // sleep get one. SHOULD_CALL_PARENT(FALSE) is dropped (rules 7.9).
    public virtual void LateInitialize(params object[] __extra)
    {
        stack_trace(Dm.text("[] ([]) called LateInitialize but has nothing on it!", this, type), "code/game/atom/atoms_initializing_EXPENSIVE.dm", 175);
    }
}
