namespace Anomaly.Core.DmShim;

// Owner: the M2 contracts step (complete; docs/ai/specs/m2-ownership.md). The overlay snapshot that an overlays list makes
// of a mutable appearance is M2 P3's.

/// <summary>
/// The BYOND <c>/mutable_appearance</c> (BYOND ref "mutable appearance", "vars (mutable appearance)"): an appearance that
/// can be changed var by var and then put into an <c>overlays</c> list or assigned as an appearance. "Technically a
/// descendant of <c>/image</c>" (BYOND ref), and tg says so too (<c>code/datums/mutable_appearance.dm:5</c>), so it
/// derives from <see cref="image"/> and has its vars plus the ones only a mutable appearance copies (<c>density</c>,
/// <c>gender</c>, <c>screen_loc</c>). A builtin value type: no world id, never registered, hashed by its fields. Created
/// with <c>Dm.@new&lt;mutable_appearance&gt;(source)</c> (DM <c>new /mutable_appearance(source)</c>), which calls
/// <see cref="New"/>; tg's global proc <c>mutable_appearance(...)</c> (<c>datums/mutable_appearance.dm:14</c>, M2 P3)
/// builds on it. The shim has no <c>Dm.mutable_appearance</c> factory: tg defines that name (rules 1.6).
/// </summary>
[DmPath("/mutable_appearance")]
public class mutable_appearance : image
{
    /// <summary>BYOND <c>density</c> (copied when a mutable appearance is assigned, BYOND ref "appearance var"). Default 0.</summary>
    public float density;

    /// <summary>BYOND <c>gender</c>. Default <c>"neuter"</c>.</summary>
    public string gender = "neuter";

    /// <summary>BYOND <c>screen_loc</c> (copied when a mutable appearance is assigned). Default null.</summary>
    public string screen_loc;

    /// <summary>
    /// A new mutable appearance starts from BYOND's default appearance, whose plane is 0, not an image's
    /// <c>FLOAT_PLANE</c> (tg: "Mutable appearances erase template vars on new, because they accept an appearance to copy
    /// as an arg", <c>code/datums/mutable_appearance.dm:6</c>); the other defaults are the image's.
    /// </summary>
    public mutable_appearance()
    {
        plane = 0;
    }

    /// <summary>
    /// BYOND's <c>/mutable_appearance/New(source)</c> ("A new mutable appearance is created via new/mutable_appearance, and
    /// giving it an atom, image, or appearance as a source object", BYOND ref "mutable appearance"; OpenDream
    /// <c>DreamObjectMutableAppearance</c>), followed by tg's override of it (<c>code/datums/mutable_appearance.dm:8-11</c>):
    /// a builtin value type has no virtual <c>New</c> slot that Tg could override, so tg's two lines are applied here.
    /// <para>
    /// The source's appearance vars are copied (an <see cref="IDmAppearance"/>: an atom, an image, another mutable
    /// appearance, an overlay snapshot); its overlay and underlay lists are copied too (new lists with the same items,
    /// BYOND: "Reading certain vars, such as overlays, will create a temporary list object"). Without a source the vars keep
    /// their defaults.
    /// </para>
    /// </summary>
    public void New(object to_copy = null)
    {
        // BYOND's builtin New: copy the source's appearance. A typepath is its prototype's appearance (OpenDream
        // AtomManager.TryCreateAppearanceFrom; tg's ghost poll alert shows a guardian type so, SSpolling, syndicate X2).
        object source_object = to_copy is Type prototypePath && DmTypes.PathOf(prototypePath) != null && DmPrototypes.Prototype(prototypePath) is IDmAppearance prototype ? prototype : to_copy;
        if (source_object is IDmAppearance source)
        {
            icon = source.icon;
            icon_state = source.icon_state;
            dir = source.dir;
            layer = source.layer;
            plane = source.plane;
            alpha = source.alpha;
            color = source.color;
            pixel_x = source.pixel_x;
            pixel_y = source.pixel_y;
            pixel_w = source.pixel_w;
            pixel_z = source.pixel_z;
            transform = source.transform is matrix m ? Dm.matrix(m) : source.transform;
            appearance_flags = source.appearance_flags;
            blend_mode = source.blend_mode;
            invisibility = source.invisibility;
            overlays = source.overlays.Copy();
            underlays = source.underlays.Copy();
            filters = source.filters; // an atom's or an appearance's too (the greenscreen copies a mob's look, status_display.dm:924)
            if (source_object is image other)
            {
                name = other.name;
                desc = other.desc;
                maptext = other.maptext;
                maptext_width = other.maptext_width;
                maptext_height = other.maptext_height;
                maptext_x = other.maptext_x;
                maptext_y = other.maptext_y;
                mouse_opacity = other.mouse_opacity;
                render_source = other.render_source;
                render_target = other.render_target;
                vis_flags = other.vis_flags;
                if (other is mutable_appearance ma)
                {
                    density = ma.density;
                    gender = ma.gender;
                    screen_loc = ma.screen_loc;
                }
            }
            else if (source_object is IDmAtom atom)
            {
                name = atom.name;
                gender = atom.gender;
                desc = Dm.get_var(atom, "desc") as string;
                density = Dm.num(Dm.get_var(atom, "density"));
                maptext = source.maptext;
                maptext_width = source.maptext_width;
                maptext_height = source.maptext_height;
                maptext_x = source.maptext_x;
                maptext_y = source.maptext_y;
            }
        }
        else if (to_copy is DmShim.icon or DmResource)
        {
            // An icon object or an icon file is an appearance that shows it, the rest default (OpenDream
            // AtomManager.TryCreateAppearanceFrom; tg's radial extract_image makes its pictures so, radial.dm:333).
            icon = to_copy;
        }
        else if (to_copy != null)
        {
            throw new DmRuntimeException("new /mutable_appearance(): cannot copy the appearance of " + Dm.KindOf(to_copy));
        }

        // tg: code/datums/mutable_appearance.dm:8  /mutable_appearance/New(mutable_appearance/to_copy)
        //     ..()
        //     if(!to_copy)
        //         plane = FLOAT_PLANE
        if (!Dm.truthy(to_copy))
            plane = Dm.FLOAT_PLANE;
    }
}
