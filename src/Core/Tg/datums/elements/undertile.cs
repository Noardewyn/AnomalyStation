// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/undertile.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P4: the whole file. The pipes, cables and disposal pipes under floor tiles get tg's invisibility through it, so the
// entity rule of D115 (invisibility <= 25) does not show them. The var block is generated.

///Add to an object if you want to be able to be hidden under tiles
// tg: code/datums/elements/undertile.dm:5
public partial class datum_element_undertile : datum_element
{
    /// The alpha we give to stuff under tiles, if they want it
    // tg: code/datums/elements/undertile.dm:2  #define ALPHA_UNDERTILE 128 (file-local, #undef at :111)
    private const int ALPHA_UNDERTILE = 128;

    // tg: code/datums/elements/undertile.dm:22
    [DmParams("target", "invisibility_trait", "invisibility_level", "tile_overlay", "use_alpha", "use_anchor", "tilt_tile")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object invisibility_trait = Dm.arg(__extra, 0);
        object invisibility_level = Dm.arg(__extra, 1);
        object tile_overlay = Dm.arg(__extra, 2);
        object use_alpha = Dm.arg(__extra, 3);
        object use_anchor = Dm.arg(__extra, 4);
        object tilt_tile = Dm.arg(__extra, 5);
        invisibility_level ??= INVISIBILITY_MAXIMUM;
        use_alpha ??= TRUE;
        use_anchor ??= FALSE;
        tilt_tile ??= FALSE;
        object dot = null;
        dot = await base.Attach(target, Dm.forward(__extra, invisibility_trait, invisibility_level, tile_overlay, use_alpha, use_anchor, tilt_tile));

        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_OBJ_HIDE, nameof(hide));

        this.invisibility_trait = invisibility_trait;
        this.invisibility_level = invisibility_level;
        this.tile_overlay = tile_overlay;
        this.use_alpha = use_alpha;
        this.use_anchor = use_anchor;
        this.tilt_tile = tilt_tile;
        return dot;
    }

    ///called when a tile has been covered or uncovered
    // tg: code/datums/elements/undertile.dm:38
    [SignalHandler]
    public virtual void hide(atom_movable source = null, object underfloor_accessibility = null)
    {
        // DEVIATION(DV504): a map editor's world that shows what its floor tiles cover leaves a covered thing visible (MapEditSeam.UnderFloorShown)
        if (Dm.lt(underfloor_accessibility, UNDERFLOOR_VISIBLE) && !MapEditSeam.UnderFloorShown)
            source.SetInvisibility(invisibility_level, id: type);
        else
            source.RemoveInvisibility(type);

        turf T = get_turf(source);

        if (Dm.lt(underfloor_accessibility, UNDERFLOOR_INTERACTABLE))
        {
            // We only want to change the layer/plane for things that aren't already on the floor plane,
            // as overriding the settings for those would cause layering issues
            if (!Dm.eq(PLANE_TO_TRUE(source.plane), FLOOR_PLANE))
            {
                // We do this so that turfs that allow you to see what's underneath them don't have to be on the game plane (which causes ambient occlusion weirdness)
                SET_PLANE_IMPLICIT(source, FLOOR_PLANE);
                source.layer = BELOW_CATWALK_LAYER;
            }

            ADD_TRAIT(source, TRAIT_UNDERFLOOR, REF(this));

            if (Dm.truthy(tile_overlay))
                T.add_overlay(tile_overlay);

            if (Dm.truthy(tilt_tile))
            {
                T.transform = ((matrix)T.transform).Turn(2);
                T.layer = (T.layer + 0.1f); // prettier
                T.appearance_flags = Dm.bitor(T.appearance_flags, PIXEL_SCALE);
            }

            if (Dm.truthy(use_anchor))
                Waiting.NoSleep(source.set_anchored(TRUE));

            if (Dm.lt(underfloor_accessibility, UNDERFLOOR_VISIBLE))
            {
                if (Dm.truthy(use_alpha))
                    source.alpha = ALPHA_UNDERTILE;

                if (Dm.truthy(invisibility_trait))
                    ADD_TRAIT(source, invisibility_trait, ELEMENT_TRAIT(type));
            }
        }
        else
        {
            if (!HAS_TRAIT(source.loc, TRAIT_UNCOVERED_TURF))
            {
                SET_PLANE_IMPLICIT(source, Dm.num(Dm.initial(source, (atom_movable p) => p.plane)));
                source.layer = Dm.num(Dm.initial(source, (atom_movable p) => p.layer));
            }
            else
            {
                SET_PLANE_IMPLICIT(source, FLOOR_PLANE);
                source.layer = BELOW_CATWALK_LAYER;
            }

            REMOVE_TRAIT(source, TRAIT_UNDERFLOOR, REF(this));

            if (Dm.truthy(invisibility_trait))
                REMOVE_TRAIT(source, invisibility_trait, ELEMENT_TRAIT(type));

            if (Dm.truthy(tile_overlay))
                Dm.remove(T.overlays, tile_overlay);

            if (Dm.truthy(tilt_tile))
            {
                T.transform = Dm.matrix();
                T.layer = Dm.num(Dm.initial(T, (turf p) => p.layer));
                T.appearance_flags = Dm.bitand(T.appearance_flags, PIXEL_SCALE);
            }

            if (Dm.truthy(use_alpha))
                source.alpha = Dm.num(Dm.initial(source, (atom_movable p) => p.alpha));

            if (Dm.truthy(use_anchor))
                Waiting.NoSleep(source.set_anchored(FALSE));
        }

        if (Dm.truthy(source._listen_lookup?[COMSIG_UNDERTILE_UPDATED]))
            source._SendSignal(COMSIG_UNDERTILE_UPDATED, Dm.list(source));
    }

    // tg: code/datums/elements/undertile.dm:104
    [DmParams("source", "visibility_trait", "invisibility_level")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        object visibility_trait = Dm.arg(__extra, 0);
        object invisibility_level = Dm.arg(__extra, 1);
        invisibility_level ??= INVISIBILITY_MAXIMUM;
        object dot = null;
        dot = base.Detach(source, Dm.forward(__extra, visibility_trait, invisibility_level));

        hide(source as atom_movable, UNDERFLOOR_INTERACTABLE);
        ((atom_movable)source).RemoveInvisibility(type);
        return dot;
    }
}
