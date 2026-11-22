// Translated from tgstation (snapshot 1d99bd7f): code/modules/photography/_pictures.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J3, D298): the picture and its small icon; the logging and persistence of pictures on the disk are rows (no image to log). The var blocks are generated.

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
        if (psize_x != null)
            psize_x = size_x == null ? null : Dm.num(size_x);
        if (psize_y != null)
            psize_y = size_y == null ? null : Dm.num(size_y);
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
        Dm.Deferred("code/modules/photography/_pictures.dm:65", "regenerate_small_icon: the photo's small picture (no image: camera_get_icon)");
    }

    // tg: code/modules/photography/_pictures.dm:195  (gaps S3: the photocopier's copies; the image and icon are null until the camera's
    // picture exists, so their copy, greyscale and crop do not run)
    public virtual object Copy(float? greyscale = null, float? cropx = null, float? cropy = null)
    {
        greyscale ??= FALSE;
        cropx ??= 0;
        cropy ??= 0;
        datum_picture P = Dm.@new<datum_picture>();
        P.picture_name = picture_name;
        P.picture_desc = picture_desc;
        if (picture_image != null)
            Dm.Deferred("code/modules/photography/_pictures.dm:200", "Copy: P.picture_image = icon(picture_image), the image's copy (the picture has no image: camera_get_icon)");
        if (picture_icon != null)
            Dm.Deferred("code/modules/photography/_pictures.dm:202", "Copy: P.picture_icon = icon(picture_icon), the icon's copy (no image)");
        P.psize_x = psize_x - cropx * 2;
        P.psize_y = psize_y - cropy * 2;
        P.has_blueprints = has_blueprints;
        // greyscale: picture_image.GrayScale() and picture_icon.GrayScale() on the copies (no image)
        if (Dm.truthy(cropx) || Dm.truthy(cropy))
            P.regenerate_small_icon();
        return P;
    }
}
