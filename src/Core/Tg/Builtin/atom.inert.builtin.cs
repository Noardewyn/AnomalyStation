// Engine ("BYOND builtin") part of /atom, /turf and /area: what an inert atom runs at init (D112). Not translated from a tg
// file; it runs tg's own lines as translated elsewhere. The contract (these signatures and the meaning in the comments) is
// the M3 contracts step's (frozen, docs/ai/specs/m3-ownership.md, docs/ai/specs/types-converter.md section 7); M3 P4 implements it.
using System.Reflection;
using System.Reflection.Emit;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public partial class atom
{
    /// <summary>
    /// What <c>SSatoms.InitAtom</c> runs for an inert atom (<see cref="InertSeam.IsInert"/>) instead of tg's
    /// <c>Initialize(arglist(arguments))</c> (<paramref name="arguments"/>[1] is tg's <c>mapload</c>): the presentation part of
    /// tg's <c>/atom/Initialize</c> (<c>atoms_initializing_EXPENSIVE.dm:115-159</c>) in tg's order, the <c>INITIALIZED_1</c>
    /// flag, <c>SET_PLANE_IMPLICIT</c>, <c>hud_possible</c>, <c>update_greyscale</c>, <c>add_atom_colour</c>,
    /// <c>update_light</c> for a <c>COMPLEX_LIGHT</c>, <c>post_init_icon_state</c>, <c>SETUP_SMOOTHING</c> and the integrity;
    /// not the materials, the <c>ai_controller</c>, the reskins, nor any <c>Initialize</c> override of the atom's types.
    /// Returns <c>INITIALIZE_HINT_NORMAL</c>. Never sleeps. A turf and an area override it (below).
    /// </summary>
    internal virtual object dm_inert_initialize(DmList arguments)
    {
        // The lines below are tg's /atom/Initialize (atoms_initializing_EXPENSIVE.dm:115-159) as atom.Initialize translates
        // them, without the materials (:151-152, M4a), the ai_controller (:154-155) and setup_reskins (:157): D112,
        // docs/ai/specs/types-converter.md section 7.
        // tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:119
        if (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
            stack_trace(Dm.text("Warning: []([]) initialized multiple times!", this, type), "code/game/atom/atoms_initializing_EXPENSIVE.dm", 120);
        Dm.combine(ref flags_1, INITIALIZED_1);

        // tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:123
        SET_PLANE_IMPLICIT(this, plane);

        // tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:125
        if (Dm.truthy(LAZYLEN(hud_possible)))
            hud_possible = string_assoc_list(hud_possible) as DmList;

        // tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:128
        if (Dm.truthy(greyscale_config) && Dm.truthy(greyscale_colors)) //we'll check again at item/init for inhand/belt/worn configs.
            Waiting.NoSleep(update_greyscale());

        //atom color stuff
        // tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:132
        if (Dm.truthy(color))
            Waiting.NoSleep(add_atom_colour(color, FIXED_COLOUR_PRIORITY));

        // tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:135
        if (light_system == COMPLEX_LIGHT && Dm.truthy(light_power) && Dm.truthy(light_range))
            update_light();

        // tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:138
        if (Dm.truthy(post_init_icon_state))
            icon_state = (string)post_init_icon_state;

        // tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:141
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

        // tg: code/game/atom/atoms_initializing_EXPENSIVE.dm:143
        if (Dm.truthy(uses_integrity))
            atom_integrity = max_integrity;

        // Wall support is part of displaying an inert wall fixture, even while its machinery is unported.
        // These families call find_and_mount_on_atom in their tg Initialize; no device gameplay runs here.
        if (this is obj mounted && Dm.truthy(arguments?[1]) && InertWallMounts.Applies(mounted))
            Waiting.NoSleep(mounted.find_and_mount_on_atom());

        // An inert machine still shows its powered state (a computer's screen, keyboard and light) and follows its area's
        // power, as tg's post_machine_initialize (_machinery.dm:212) sets up; its power use stays unported.
        if (this is obj_machinery machine)
        {
            if (get_area(machine) is area machine_area)
                machine.RegisterSignal(machine_area, COMSIG_AREA_POWER_CHANGE, nameof(obj_machinery.power_change));
            machine.power_change();
        }

        return INITIALIZE_HINT_NORMAL;
    }

    /// <summary>
    /// <paramref name="arguments"/> (what <c>SSatoms.InitAtom</c> passes on, <c>mapload</c> first) as the positional
    /// arguments of an <c>Initialize(mapload, ...)</c> call: the first item and the rest (the <c>__extra</c> array).
    /// </summary>
    private protected static object[] dm_inert_extra(DmList arguments, out object mapload)
    {
        float count = arguments == null ? 0 : arguments.len;
        mapload = count >= 1 ? arguments[1] : null;
        if (count <= 1)
            return Array.Empty<object>();
        object[] extra = new object[(int)count - 1];
        for (int i = 2; i <= count; i++)
            extra[i - 2] = arguments[i];
        return extra;
    }
}

public partial class turf
{
    /// <summary>
    /// An inert turf runs tg's <c>/turf/Initialize</c> as translated (<c>game/turfs/turf.cs</c>; for a <c>/turf/open/space</c>
    /// type tg's <c>/turf/open/space/Initialize</c>, <c>space_EXPENSIVE.cs</c>), called non-virtually on the instance, so the
    /// map's structure (baseturfs, air, smoothing) initializes and no untranslated subtype code runs.
    /// </summary>
    internal override object dm_inert_initialize(DmList arguments)
    {
        object[] extra = dm_inert_extra(arguments, out object mapload);
        // An open turf enters /turf/Initialize through /turf/open/Initialize (LINDA_turf_tile.dm:59), whose only work before
        // its ..() is the turf's air: the "air" of this contract (types-converter.md 7). A space turf runs space's own
        // Initialize, which does not call its parent (space_EXPENSIVE.dm:13).
        if (this is turf_open_space)
            return InertInitializers.SpaceInitialize(this, mapload, extra);
        if (this is turf_open)
            return InertInitializers.OpenInitialize(this, mapload, extra);
        return InertInitializers.TurfInitialize(this, mapload, extra);
    }
}

public partial class area
{
    /// <summary>An inert area runs tg's <c>/area/Initialize</c> as translated (<c>game/area/areas.cs</c>), called non-virtually on the instance (its registration in <c>GLOB.areas</c>, the power flags, the base lighting).</summary>
    internal override object dm_inert_initialize(DmList arguments)
    {
        object[] extra = dm_inert_extra(arguments, out object mapload);
        return InertInitializers.AreaInitialize(this, mapload, extra);
    }
}

/// <summary>
/// The non-virtual calls of the inert init (types-converter.md section 7): tg's <c>/turf/Initialize</c>,
/// <c>/turf/open/Initialize</c>, <c>/turf/open/space/Initialize</c> and <c>/area/Initialize</c> as their translations declare them, called on an instance
/// of a subtype without dispatching to the subtype's overrides (C# reaches a base implementation only through <c>base.</c>
/// on <c>this</c>, so each is a small dynamic method with a <c>call</c>, not a <c>callvirt</c>). Built once per process;
/// the delegates are immutable (no mutable static state, object-model.md section 10).
/// </summary>
internal static class InertInitializers
{
    internal static readonly Func<atom, object, object[], object> TurfInitialize = NonVirtualInitialize(typeof(turf));
    internal static readonly Func<atom, object, object[], object> OpenInitialize = NonVirtualInitialize(typeof(turf_open));
    internal static readonly Func<atom, object, object[], object> SpaceInitialize = NonVirtualInitialize(typeof(turf_open_space));
    internal static readonly Func<atom, object, object[], object> AreaInitialize = NonVirtualInitialize(typeof(area));

    private static Func<atom, object, object[], object> NonVirtualInitialize(Type declaring)
    {
        MethodInfo method = declaring.GetMethod(nameof(atom.Initialize), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, [typeof(object), typeof(object[])])
            ?? throw new InvalidOperationException(declaring.Name + " declares no Initialize(object, object[])");
        DynamicMethod dynamicMethod = new("dm_inert_" + declaring.Name + "_Initialize", typeof(object), [typeof(atom), typeof(object), typeof(object[])], typeof(atom).Module, skipVisibility: true);
        ILGenerator il = dynamicMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, declaring);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, method); // call, not callvirt: the declaring class's own body
        il.Emit(OpCodes.Ret);
        return dynamicMethod.CreateDelegate<Func<atom, object, object[], object>>();
    }
}
