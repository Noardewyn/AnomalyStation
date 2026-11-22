// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/decals/crayon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J3, D298). The var block is generated.

// tg: code/game/objects/effects/decals/crayon.dm:2
public partial class obj_effect_decal_cleanable_crayon : obj_effect_decal_cleanable
{
    // tg: code/game/objects/effects/decals/crayon.dm:15
    [DmParams("mapload", "main", "type", "e_name", "graf_rot", "alt_icon", "desc_override", "color_strength")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object main = Dm.arg(__extra, 0);
        object graffiti_type = Dm.arg(__extra, 1);
        object e_name = Dm.arg(__extra, 2);
        object graf_rot = Dm.arg(__extra, 3);
        object alt_icon = Dm.arg(__extra, 4);
        object desc_override = Dm.arg(__extra, 5);
        object color_strength = Dm.arg(__extra, 6);
        object dot = base.Initialize(mapload); // tg's ..() hands its arguments on as the decal's diseases (a colour there is no disease)
        if (isclosedturf(loc) && Dm.truthy(loc.density))
        {
            // allows for wall graffiti to be seen
            SET_PLANE_IMPLICIT(this, GAME_PLANE);
            layer = CLEANABLE_OBJECT_LAYER;
        }
        if (Dm.truthy(e_name))
            name = Dm.str(e_name);
        if (Dm.truthy(desc_override))
            desc = Dm.text("[]", desc_override);
        else
            desc = I18n.T("A [] vandalizing the station.", name);
        if (Dm.truthy(alt_icon))
            icon = alt_icon;
        if (Dm.truthy(graffiti_type))
            icon_state = Dm.str(graffiti_type);
        if (Dm.truthy(graf_rot))
            rotation = Dm.num(graf_rot);
        if (Dm.truthy(rotation) && Dm.truthy(do_icon_rotate))
        {
            matrix M = matrix();
            M.Turn(rotation);
            this.transform = M;
        }
        if (Dm.truthy(main))
            paint_colour = Dm.str(main);
        this.color_strength = color_strength;
        Waiting.NoSleep(add_atom_colour(paint_colour, FIXED_COLOUR_PRIORITY));
        RegisterSignal(this, COMSIG_OBJ_PAINTED, nameof(on_painted));
        return dot;
    }

    // tg: code/game/objects/effects/decals/crayon.dm:43
    public override object NeverShouldHaveComeHere(turf here_turf = null)
    {
        return isgroundlessturf(here_turf);
    }

    // tg: code/game/objects/effects/decals/crayon.dm:46
    [SignalHandler]
    public virtual object on_painted(datum source = null, mob user = null, obj_item_toy_crayon_spraycan spraycan = null, object is_dark_color = null)
    {
        float cost = Dm.num(obj_item_toy_crayon.all_drawables[icon_state] ?? CRAYON_COST_DEFAULT);
        if (HAS_TRAIT(user, TRAIT_TAGGER))
            cost *= 0.5f;
        Waiting.NoSleep(spraycan.use_charges(user, cost, requires_full: FALSE));
        return DONT_USE_SPRAYCAN_CHARGES;
    }

    /**
     * Using a given atom, gives this decal an outline of said atom, then masks the contents,
     * leaving us with solely the outline.
     * This also deletes the previous icon, so the decal turns into JUST an outline.
     * Args:
     * outlined_atom: Anything you wish to draw an outline of.
     * add_mouse_opacity: Boolean on whether you want mouse opacity, which allows the outline to be clickable/examinable without the context menu.
     */
    // tg: code/game/objects/effects/decals/crayon.dm:62
    public virtual void create_outline(atom outlined_atom = null, float? add_mouse_opacity = null)
    {
        add_mouse_opacity ??= FALSE;
        icon = null;
        icon_state = null;
        if (Dm.truthy(add_mouse_opacity))
            mouse_opacity = MOUSE_OPACITY_OPAQUE;

        if (ishuman(outlined_atom))
        {
            //humans are special, we want to exclude things like wounds so the outline isn't animated.
            mob_living_carbon_human human_outline = (mob_living_carbon_human)outlined_atom;
            add_overlay(human_outline.get_overlays_copy(Dm.list(WOUND_LAYER)));
        }
        else
        {
            icon = outlined_atom.icon;
            icon_state = outlined_atom.icon_state;
            Dm.Deferred("code/game/objects/effects/decals/crayon.dm:60", "create_outline: copy_overlays of an outlined non-human: only humans get an outline here (interact_with_atom)");
        }
        transform = outlined_atom.transform;
        dir = outlined_atom.dir;
        add_filter("batong_outline", 1, outline_filter(color_strength, paint_colour) as DmList);
        // add_filter("alpha_mask", 2, alpha_mask_filter(icon = getFlatIcon(outlined_atom.appearance, ...), flags = MASK_INVERSE)): the mask cuts the body out so only the outline stays
        Dm.Deferred("code/game/objects/effects/decals/crayon.dm:64", "create_outline: the alpha mask of the flat icon (getFlatIcon of a whole mob, the chat icons gap, section 76): the chalk outline shows the body in the chalk's colour");
    }
}
