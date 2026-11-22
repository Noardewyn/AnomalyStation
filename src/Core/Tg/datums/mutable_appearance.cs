// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutable_appearance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Mutable appearances are an inbuilt byond datastructure. Read the documentation on them by hitting F1 in DM.
// Basically use them instead of images for overlays/underlays and when changing an object's appearance if you're doing so with any regularity.
// Unless you need the overlay/underlay to have a different direction than the base object. Then you have to use an image due to a bug.

// Mutable appearances are children of images, just so you know.

// Mutable appearances erase template vars on new, because they accept an appearance to copy as an arg
// If we have nothin to copy, we set the float plane
// tg: code/datums/mutable_appearance.dm:9  /mutable_appearance/New(mutable_appearance/to_copy): applied inside the shim's
// BYOND New (src/Core/DmShim/mutable_appearance.cs): a builtin value type has no virtual New slot that Tg could override
// (M2 contracts decision).

// tg: code/datums/mutable_appearance.dm:26 (global procs)
public static partial class GlobalProcs
{
    /** Helper similar to image()
     *
     * icon - Our appearance's icon
     * icon_state - Our appearance's icon state
     * layer - Our appearance's layer
     * atom/offset_spokesman - An atom to use as reference for the z position of this appearance.
     * 	Only required if a plane is passed in. If this is not passed in we accept offset_const as a substitute
     * plane - The plane to use for the appearance. If this is not FLOAT_PLANE we require context for the offset to use
     * alpha - Our appearance's alpha
     * appearance_flags - Our appearance's appearance_flags
     * offset_const - A constant to offset our plane by, so it renders on the right "z layer"
    **/
    // tg: code/datums/mutable_appearance.dm:26
    public static object mutable_appearance(object icon = null, string icon_state = null, float? layer = null, object offset_spokesman = null, float? plane = null, float? alpha = null, float? appearance_flags = null, object offset_const = null)
    {
        icon_state ??= "";
        layer ??= FLOAT_LAYER;
        plane ??= FLOAT_PLANE;
        alpha ??= 255;
        appearance_flags ??= NONE;
        mutable_appearance appearance = Dm.@new<mutable_appearance>();
        appearance.icon = icon;
        appearance.icon_state = icon_state;
        appearance.layer = Dm.num(layer);
        appearance.alpha = Dm.num(alpha);
        Dm.combine(ref appearance.appearance_flags, appearance_flags);
        if (Dm.num(plane) != FLOAT_PLANE)
        {
            // You need to pass in some non null object to reference
            if (isatom(offset_spokesman))
            {
                // Note, we are ok with null turfs, that's not an error condition we'll just default to 0, the error would be
                // Not passing ANYTHING in, key difference
                // GAP(lead, __HELPERS/_planes.cs): SET_PLANE_EXPLICIT's member casts its thing to /atom, while tg's macro only
                // assigns thing.plane (__HELPERS/_planes.dm:44) and is used on mutable appearances here; expanded inline for
                // the mutable appearance (rules 11.1), the same statements as the member.
                do
                {
                    if (Dm.truthy(SSmapping.max_plane_offset))
                    {
                        float _cached_plane = Dm.num(plane);
                        turf _our_turf = get_turf(offset_spokesman);
                        if (Dm.truthy(_our_turf))
                            appearance.plane = GET_NEW_PLANE(_cached_plane, Dm.num(GET_Z_PLANE_OFFSET(_our_turf.z)));
                        else if (Dm.truthy(offset_spokesman))
                            appearance.plane = GET_NEW_PLANE(_cached_plane, Dm.num(PLANE_TO_OFFSET(((atom)offset_spokesman).plane)));
                        else
                            appearance.plane = _cached_plane;
                    }
                    else
                        appearance.plane = Dm.num(plane);
                }
                while (Dm.truthy(FALSE));
            }
            // That or I'll let you pass in a static offset. Don't be stupid now
            else if (offset_const != null)
                appearance.plane = GET_NEW_PLANE(Dm.num(plane), Dm.num(offset_const)); // SET_PLANE_W_SCALAR(appearance, plane, offset_const) (__HELPERS/_planes.dm:35), inline (rules 11.1 (a))
            // otherwise if you're setting plane you better have the guts to back it up
            else
                stack_trace("No plane offset passed in as context for a non floating mutable appearance, things are gonna go to hell on multiz maps", "code/datums/mutable_appearance.dm", 44);
        }
        else if (offset_spokesman != null && !isatom(offset_spokesman))
            stack_trace(Dm.text("Why did you pass in offset_spokesman as []? We need an atom to properly offset planes", offset_spokesman), "code/datums/mutable_appearance.dm", 46);

        // tg: :48  if(PERFORM_ALL_TESTS(focus_only/topdown_filtering)) check_topdown_validity(appearance): FALSE in production (rules 1.7)
        if (Dm.truthy(FALSE))
            Dm.NotTranslated("code/datums/mutable_appearance.dm:49", "mutable_appearance: check_topdown_validity (a CI focus test, never in production)");

        return appearance;
    }

    /// Takes an input mutable appearance, returns a copy of it with the hidden flag flipped to avoid inheriting dir from what it's drawn on
    /// This inheriting thing is handled by a hidden flag on the /image (MAs are subtypes of /image)
    // tg: code/datums/mutable_appearance.dm:55
    public static object make_mutable_appearance_directional(mutable_appearance to_process = null, float? dir = null)
    {
        dir ??= NORTH;
        // We use the image() proc in combo with a manually set dir to flip this flag
        // We can then copy the image's appearance to retain the flag, even on MAs and such
        image holder = image(to_process, dir: dir);
        return Dm.@new<mutable_appearance>(holder);
    }
}
