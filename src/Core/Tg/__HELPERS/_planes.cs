// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/_planes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only SET_PLANE_IMPLICIT, SET_PLANE_EXPLICIT and the macros they expand to (GET_Z_PLANE_OFFSET, GET_NEW_PLANE,
// PLANE_TO_OFFSET) translated (the atoms package, wave 3: /atom/Initialize), and (W4.0 contracts: the movable, turf and
// area code of wave 4) SET_PLANE, MUTATE_PLANE, GET_TURF_PLANE_OFFSET, GET_LOWEST_STACK_OFFSET, PLANE_TO_TRUE,
// GLOB.topdown_planes and IS_TOPDOWN_PLANE. SSmapping is an inert stub in M0 whose max_plane_offset stays 0, so these
// macros take their base case.

// This file contains helper macros for plane operations
// See the planes section of Visuals.md for more detail, but essentially
// When we render multiz, we do it by placing all atoms on lower levels on well, lower planes
// This is done with stacks of plane masters (things we use to apply effects to planes)
// These macros exist to facilitate working with this system, and other associated small bits
public static partial class GlobalProcs
{
    /// Takes an atom to change the plane of, a new plane value, and something that can be used as a reference to a z level as input
    /// Modifies the new value to match the plane we actually want. Note, if you pass in an already offset plane the offsets will add up
    /// Use PLANE_TO_TRUE() to avoid this
    // tg: code/__HELPERS/_planes.dm:10
    public static float SET_PLANE(object thing, float new_value, object z_reference) => (((atom)thing).plane = MUTATE_PLANE(new_value, z_reference));

    /// Takes a plane and a z reference, and offsets the plane by the mutation
    /// The SSmapping.max_plane_offset bit here is technically redundant, but saves a bit of work in the base case
    /// And the base case is important to me. Non multiz shouldn't get hit too bad by this code
    // tg: code/__HELPERS/_planes.dm:10  the same macro on an /image or a /mutable_appearance (rules 11.1: one overload per C#
    // type of the thing whose plane it sets; atoms_movable.dm:1268 and :1275 re-plane images and overlays)
    public static float SET_PLANE(image thing, float new_value, object z_reference) => (thing.plane = MUTATE_PLANE(new_value, z_reference));

    // tg: code/__HELPERS/_planes.dm:15
    // tg: code/__HELPERS/_planes.dm:35  #define SET_PLANE_W_SCALAR(thing, new_value, multiplier) (thing.plane = GET_NEW_PLANE(new_value, multiplier))
    // (gaps E2a: the particle weathers' holders)
    public static float SET_PLANE_W_SCALAR(atom thing, float new_value, object multiplier) => (thing.plane = GET_NEW_PLANE(new_value, Dm.num(multiplier)));

    public static float MUTATE_PLANE(float new_value, object z_reference) => ((Dm.truthy(SSmapping.max_plane_offset)) ? GET_NEW_PLANE(new_value, Dm.num(GET_TURF_PLANE_OFFSET(z_reference))) : (new_value));

    /// Takes a z reference that we are unsure of, sanity checks it
    /// Returns either its offset, or 0 if it's not a valid ref
    /// Will return the reference's PLANE'S offset if we can't get anything out of the z level. We do our best
    // tg: code/__HELPERS/_planes.dm:20
    public static object GET_TURF_PLANE_OFFSET(object z_reference) => ((Dm.truthy(SSmapping.max_plane_offset) && isatom(z_reference)) ? (Dm.truthy(((atom)z_reference).z) ? GET_Z_PLANE_OFFSET(((atom)z_reference).z) : PLANE_TO_OFFSET(((atom)z_reference).plane)) : 0);

    /// Essentially just an unsafe version of GET_TURF_PLANE_OFFSET()
    /// Takes a z value we returns its offset with a list lookup
    /// Will runtime during parts of init. Be careful :)
    // tg: code/__HELPERS/_planes.dm:24
    public static object GET_Z_PLANE_OFFSET(float z) => (SSmapping.z_level_to_plane_offset[z]);

    /// Takes a plane to offset, and the multiplier to use, and well, does the offsetting
    /// Respects a blacklist we use to remove redundant plane masters, such as hud objects
    // tg: code/__HELPERS/_planes.dm:28
    public static float GET_NEW_PLANE(float new_value, float multiplier) => (Dm.truthy(SSmapping.plane_offset_blacklist?[Dm.text("[]", new_value)]) ? new_value : (new_value) - (PLANE_RANGE * (multiplier)));

    /// Implicit plane set. We take the turf from the object we're changing the plane of, and use ITS z as a spokesperson for our plane value
    // tg: code/__HELPERS/_planes.dm:39
    public static void SET_PLANE_IMPLICIT(object thing, float new_value) => SET_PLANE_EXPLICIT(thing, new_value, thing);

    // This is an unrolled and optimized version of SET_PLANE, for use anywhere where you are unsure of a source's "turfness"
    // We do also try and guess at what the thing's z level is, even if it's not a z
    // The plane is cached to allow for fancy stuff to be eval'd once, rather then often
    // tg: code/__HELPERS/_planes.dm:44
    public static void SET_PLANE_EXPLICIT(object thing, float new_value, object source)
    {
        do
        {
            if (Dm.truthy(SSmapping.max_plane_offset))
            {
                float _cached_plane = new_value;
                turf _our_turf = get_turf(source);
                if (Dm.truthy(_our_turf))
                    ((atom)thing).plane = GET_NEW_PLANE(_cached_plane, Dm.num(GET_Z_PLANE_OFFSET(_our_turf.z)));
                else if (Dm.truthy(source))
                    ((atom)thing).plane = GET_NEW_PLANE(_cached_plane, Dm.num(PLANE_TO_OFFSET(((atom)source).plane)));
                else
                    ((atom)thing).plane = _cached_plane;
            }
            else
                ((atom)thing).plane = new_value;
        }
        while (Dm.truthy(FALSE));
    }

    // tg: code/__HELPERS/_planes.dm:44  the same macro on an /image or a /mutable_appearance (rules 11.1: one overload per C#
    // type of the thing whose plane it sets; tg's progress bar sets its image's plane, datums/progressbar.dm:47)
    public static void SET_PLANE_EXPLICIT(image thing, float new_value, object source)
    {
        do
        {
            if (Dm.truthy(SSmapping.max_plane_offset))
            {
                float _cached_plane = new_value;
                turf _our_turf = get_turf(source);
                if (Dm.truthy(_our_turf))
                    thing.plane = GET_NEW_PLANE(_cached_plane, Dm.num(GET_Z_PLANE_OFFSET(_our_turf.z)));
                else if (Dm.truthy(source))
                    thing.plane = GET_NEW_PLANE(_cached_plane, Dm.num(PLANE_TO_OFFSET(((atom)source).plane)));
                else
                    thing.plane = _cached_plane;
            }
            else
                thing.plane = new_value;
        }
        while (Dm.truthy(FALSE));
    }

    /// Takes a z level, gets the lowest plane offset in its "stack"
    // tg: code/__HELPERS/_planes.dm:69
    public static object GET_LOWEST_STACK_OFFSET(float z) => ((Dm.truthy(SSmapping.max_plane_offset)) ? SSmapping.z_level_to_lowest_plane_offset[z] : 0);

    /// Takes a plane, returns the canonical, unoffset plane it represents
    // tg: code/__HELPERS/_planes.dm:71
    public static object PLANE_TO_TRUE(float plane) => ((Dm.truthy(SSmapping.plane_offset_to_true)) ? SSmapping.plane_offset_to_true[Dm.text("[]", plane)] : plane);

    /// Takes a plane, returns the offset it uses
    // tg: code/__HELPERS/_planes.dm:73
    public static object PLANE_TO_OFFSET(float plane) => ((Dm.truthy(SSmapping.plane_to_offset)) ? SSmapping.plane_to_offset[Dm.text("[]", plane)] : plane);

    // tg: code/__HELPERS/_planes.dm:95
    public static object IS_TOPDOWN_PLANE(float plane) => GLOB.topdown_planes[Dm.text("[]", PLANE_TO_TRUE(plane))];

    /**
     * Compares two layers, accounting for topdown layers
     * (tg: returns whether layer_a is above layer_b; a topdown layer, at or above TOPDOWN_LAYER, is above every other)
     */
    // tg: code/__HELPERS/_planes.dm:115  #define COMPARE_LAYERS(layer_a, layer_b) (M2 integration: /atom/IsObscured expanded it inline)
    public static bool COMPARE_LAYERS(float layer_a, float layer_b)
        => ((layer_a) >= TOPDOWN_LAYER
            ? ((layer_b) >= TOPDOWN_LAYER
                ? ((layer_a) > (layer_b))
                : false
            )
            : ((layer_b) >= TOPDOWN_LAYER
                ? true
                : ((layer_a) > (layer_b))
            )
        );
}

public partial class datum_controller_global_vars
{
    /// Whitelist of planes allowed to use TOPDOWN_LAYER
    // tg: code/__HELPERS/_planes.dm:91  GLOBAL_LIST_INIT(topdown_planes, list("[FLOOR_PLANE]" = TRUE))
    public DmList topdown_planes;

    // tg: code/__HELPERS/_planes.dm:91  (GLOBAL_MANAGED(topdown_planes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltopdown_planes()
    {
        topdown_planes = Dm.list(
            Dm.kv(Dm.text("[]", FLOOR_PLANE), TRUE));
        Dm.append(ref gvars_datum_init_order, "topdown_planes");
    }
}
