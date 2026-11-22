// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/worn_feature_offset.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C5): the monkey's head and chest are the first limbs with offsets.

/// A datum for controlling how to position items on an unusually offset body part
/// For instance if you have an asymmetrical head, hats might need to be offset to one side
// tg: code/modules/surgery/bodyparts/worn_feature_offset.dm:3
[DmPath("/datum/worn_feature_offset")]
public partial class datum_worn_feature_offset : datum
{
    // tg: code/modules/surgery/bodyparts/worn_feature_offset.dm:15
    [DmParams("attached_part", "feature_key", "offset_x", "offset_y")]
    public override async DmTask New(params object[] __extra)
    {
        obj_item_bodypart attached_part = Dm.arg<obj_item_bodypart>(__extra, 0);
        object feature_key = Dm.arg(__extra, 1);
        DmList offset_x = Dm.arg<DmList>(__extra, 2) ?? Dm.list(Dm.kv("south", 0));
        DmList offset_y = Dm.arg<DmList>(__extra, 3) ?? Dm.list(Dm.kv("south", 0));
        attached_part.feature_offsets[feature_key] = this;
        this.attached_part = attached_part;
        this.feature_key = feature_key;
        this.offset_x = offset_x;
        this.offset_y = offset_y;

        if (Dm.num(length(offset_x)) <= 1 && Dm.num(length(offset_y)) <= 1)
            return; // We don't need to do any extra signal handling

        changed_owner(null, attached_part.owner);
        RegisterSignal(attached_part, COMSIG_BODYPART_CHANGED_OWNER, nameof(changed_owner));
    }

    // tg: code/modules/surgery/bodyparts/worn_feature_offset.dm:33
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref attached_part.feature_offsets, feature_key);
        attached_part = null;
        changed_owner(null, null);
        return base.Destroy(force);
    }

    /// Returns the current offset which should be used for this feature
    // tg: code/modules/surgery/bodyparts/worn_feature_offset.dm:40
    public virtual object get_offset()
    {
        float current_dir = owner != null ? Dm.num(owner.dir) : SOUTH;
        if (Dm.truthy(ISDIAGONALDIR(current_dir)))
            current_dir = Dm.bitand(current_dir, Dm.bitor(EAST, WEST));
        object dir_text = dir2text(current_dir);
        object x = Dm.truthy(length(offset_x)) ? (Dm.@in(dir_text, offset_x) ? offset_x[dir_text] : offset_x["south"]) : 0;
        object y = Dm.truthy(length(offset_y)) ? (Dm.@in(dir_text, offset_y) ? offset_y[dir_text] : offset_y["south"]) : 0;
        return Dm.list(Dm.kv("x", x), Dm.kv("y", y));
    }

    /// Applies the current offset to a provided overlay image
    // tg: code/modules/surgery/bodyparts/worn_feature_offset.dm:50
    public virtual void apply_offset(image overlay = null)
    {
        DmList offset = get_offset() as DmList;
        overlay.pixel_w += Dm.num(offset["x"]);
        overlay.pixel_z += Dm.num(offset["y"]);
    }

    /// When the owner of the bodypart changes, update our signal registrations
    // tg: code/modules/surgery/bodyparts/worn_feature_offset.dm:56
    public virtual void changed_owner(obj_item_bodypart part = null, mob_living new_owner = null, mob_living old_owner = null)
    {
        if (old_owner == null)
            old_owner = owner;
        owner = new_owner as mob_living_carbon;
        if (old_owner != null)
            UnregisterSignal(old_owner, Dm.list(COMSIG_ATOM_POST_DIR_CHANGE, COMSIG_QDELETING));
        if (new_owner != null)
        {
            RegisterSignal(new_owner, COMSIG_ATOM_POST_DIR_CHANGE, nameof(on_dir_change));
            RegisterSignal(new_owner, COMSIG_QDELETING, nameof(on_owner_deleted));
        }
    }

    /// If the owner is deleted, stop updating
    // tg: code/modules/surgery/bodyparts/worn_feature_offset.dm:68
    public virtual void on_owner_deleted(mob_living host = null)
    {
        owner = null;
    }

    /// When we change direction, re-apply the offset
    // tg: code/modules/surgery/bodyparts/worn_feature_offset.dm:73
    public virtual void on_dir_change(mob_living_carbon owner = null, object olddir = null, object newdir = null)
    {
        if (!Dm.eq(olddir, newdir))
            Waiting.NoSleep(owner.update_features(feature_key));
    }
}
