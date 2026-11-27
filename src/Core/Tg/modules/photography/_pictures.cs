// Translated from tgstation (snapshot 1d99bd7f): code/modules/photography/_pictures.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J3, D298): the picture, its small icon and its copies; its image is the photographer's client's render (DV685, PhotoSeam). The logging and
// persistence of pictures on the disk are rows (D332). The var blocks are generated.

// tg: code/modules/photography/_pictures.dm:2
public partial class datum_picture : datum
{
    // tg: code/modules/photography/_pictures.dm:24
    [DmParams("name", "desc", "mobs_spotted", "dead_spotted", "names", "image", "icon", "size_x", "size_y", "bp", "caption_", "autogenerate_icon", "can_see_ghosts", "author_ckey_")]
    public override async DmTask New(params object[] __extra)
    {
        object name = Dm.arg(__extra, 0);
        object desc = Dm.arg(__extra, 1);
        object mobs_spotted = Dm.arg(__extra, 2);
        object dead_spotted = Dm.arg(__extra, 3);
        object names = Dm.arg(__extra, 4);
        object image = Dm.arg(__extra, 5);
        object icon_ = Dm.arg(__extra, 6);
        object size_x = Dm.arg(__extra, 7);
        object size_y = Dm.arg(__extra, 8);
        object bp = Dm.arg(__extra, 9);
        object caption_ = Dm.arg(__extra, 10);
        object autogenerate_icon = Dm.arg(__extra, 11);
        object can_see_ghosts = Dm.arg(__extra, 12);
        if (name != null)
            picture_name = Dm.str(name);
        if (desc != null)
            picture_desc = Dm.str(desc);
        if (mobs_spotted != null)
        {
            foreach (mob seen in Dm.each<mob>(mobs_spotted))
                mobs_seen.Add(WEAKREF(seen));
        }
        if (dead_spotted != null)
        {
            foreach (mob seen in Dm.each<mob>(dead_spotted))
                dead_seen.Add(WEAKREF(seen));
        }
        if (names != null)
        {
            foreach (object seen in Dm.each(names))
                names_seen.Add(seen);
        }
        if (image != null)
            picture_image = image as icon;
        if (icon_ != null)
            picture_icon = icon_ as icon;
        if (size_x != null)
            psize_x = Dm.num(size_x);
        if (size_y != null)
            psize_y = Dm.num(size_y);
        if (bp != null)
            has_blueprints = Dm.num(bp);
        if (caption_ != null)
            caption = caption_;
        if (Dm.truthy(autogenerate_icon) && picture_icon == null && picture_image != null)
            regenerate_small_icon();
        if (Dm.truthy(can_see_ghosts))
            see_ghosts = Dm.num(can_see_ghosts);
        // if(!isnull(author_ckey)) author_ckey = author_ckey: tg's own no-op (a typo of author_ckey_), kept out
    }

    // tg: code/modules/photography/_pictures.dm:57
    public virtual object get_small_icon(object iconstate = null)
    {
        if (picture_icon == null)
            regenerate_small_icon(iconstate);
        return picture_icon;
    }

    // tg: code/modules/photography/_pictures.dm:62
    public virtual void regenerate_small_icon(object iconstate = null)
    {
        if (picture_image == null)
            return;
        icon small_img = Dm.icon(picture_image);
        icon ic = Dm.icon("icons/obj/art/camera.dmi", Dm.truthy(iconstate) ? iconstate : "photo");
        small_img.Scale(8, 8);
        ic.Blend(small_img, ICON_OVERLAY, 13, 13);
        picture_icon = ic;
    }

    // tg: code/modules/photography/_pictures.dm:195  (gaps S3: the photocopier's copies)
    public virtual object Copy(float? greyscale = null, float? cropx = null, float? cropy = null)
    {
        greyscale ??= FALSE;
        cropx ??= 0;
        cropy ??= 0;
        datum_picture P = Dm.@new<datum_picture>();
        P.picture_name = picture_name;
        P.picture_desc = picture_desc;
        if (picture_image != null)
            P.picture_image = Dm.icon(picture_image); //Copy, not reference.
        if (picture_icon != null)
            P.picture_icon = Dm.icon(picture_icon);
        P.psize_x = psize_x - cropx * 2;
        P.psize_y = psize_y - cropy * 2;
        P.has_blueprints = has_blueprints;
        if (Dm.truthy(greyscale))
        {
            P.picture_image?.GrayScale();
            P.picture_icon?.GrayScale();
        }
        if (Dm.truthy(cropx) || Dm.truthy(cropy))
        {
            P.picture_image?.Crop(cropx, cropy, psize_x - cropx, psize_y - cropy);
            P.regenerate_small_icon();
        }
        return P;
    }
}
