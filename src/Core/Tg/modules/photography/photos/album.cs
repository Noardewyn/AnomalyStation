// Translated from tgstation (snapshot 1d99bd7f): code/modules/photography/photos/album.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The base album and the head's and the mapped albums (gaps J4, D298). Persistence: a picture has no image yet (section 107) and no album is saved between rounds; an album is a storage of 21 photos.

// tg: code/modules/photography/photos/album.dm:5
public partial class obj_item_storage_photo_album : obj_item_storage
{
    // tg: code/modules/photography/photos/album.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/photography/photos/album.dm:20", "Initialize: SSpersistence.queued_photo_albums and persistence_load: albums kept between rounds (persistence of pictures, whose image is section 107)");
        return dot;
    }

    // tg: code/modules/photography/photos/album.dm:26
    public override object Destroy(float? force = null)
    {
        return base.Destroy(force);
    }

    // tg: code/modules/photography/photos/album.dm:30
    public virtual object get_picture_id_list()
    {
        DmList L = Dm.list();
        foreach (object i in Dm.each(contents))
        {
            if (istype(i, typeof(obj_item_photo)))
                L.Add(i);
        }
        if (length(L) == 0)
            return null;
        DmList dot = Dm.list();
        foreach (obj_item_photo P in Dm.each<obj_item_photo>(L))
        {
            if (P.picture == null)
                continue;
            dot = Dm.bitor(dot, Dm.list(P.picture.id)) as DmList;
        }
        return dot;
    }

    //Manual loading, DO NOT USE FOR HARDCODED/MAPPED IN ALBUMS. This is for if an album needs to be loaded mid-round from an ID.
    // tg: code/modules/photography/photos/album.dm:45
    public virtual void persistence_load()
    {
        Dm.Deferred("code/modules/photography/photos/album.dm:45", "persistence_load: albums kept between rounds");
    }

    // tg: code/modules/photography/photos/album.dm:50
    public virtual void populate_from_id_list(DmList ids = null)
    {
        Dm.Deferred("code/modules/photography/photos/album.dm:50", "populate_from_id_list: load_photo_from_disk, albums kept between rounds");
    }
}

// tg: code/modules/photography/photos/album.dm:60
public partial class datum_storage_photo_album : datum_storage
{
    // tg: code/modules/photography/photos/album.dm:64
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_photo));
    }

    // tg: code/modules/photography/photos/album.dm:73
    public virtual void save_everything()
    {
        Dm.Deferred("code/modules/photography/photos/album.dm:75", "save_everything: SSpersistence.photo_albums_database, albums kept between rounds");
    }

    // tg: code/modules/photography/photos/album.dm:78
    public override void handle_enter(datum source = null, obj_item arrived = null)
    {
        base.handle_enter(source, arrived);
        save_everything();
    }

    // tg: code/modules/photography/photos/album.dm:82
    public override void handle_exit(datum source = null, obj_item gone = null)
    {
        base.handle_exit(source, gone);
        save_everything();
    }
}

// tg: code/modules/photography/photos/album.dm:91
public partial class obj_item_storage_photo_album_hos : obj_item_storage_photo_album
{
    // tg: code/modules/photography/photos/album.dm:91  (REGISTER_REQUIRED_MAP_ITEM is a unit test's)
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}

// tg: code/modules/photography/photos/album.dm:100
public partial class obj_item_storage_photo_album_rd : obj_item_storage_photo_album
{
    // tg: code/modules/photography/photos/album.dm:100  (REGISTER_REQUIRED_MAP_ITEM is a unit test's)
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}

// tg: code/modules/photography/photos/album.dm:109
public partial class obj_item_storage_photo_album_hop : obj_item_storage_photo_album
{
    // tg: code/modules/photography/photos/album.dm:109  (REGISTER_REQUIRED_MAP_ITEM is a unit test's)
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}

// tg: code/modules/photography/photos/album.dm:118
public partial class obj_item_storage_photo_album_captain : obj_item_storage_photo_album
{
    // tg: code/modules/photography/photos/album.dm:118  (REGISTER_REQUIRED_MAP_ITEM is a unit test's)
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}

// tg: code/modules/photography/photos/album.dm:127
public partial class obj_item_storage_photo_album_cmo : obj_item_storage_photo_album
{
    // tg: code/modules/photography/photos/album.dm:127  (REGISTER_REQUIRED_MAP_ITEM is a unit test's)
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}

// tg: code/modules/photography/photos/album.dm:136
public partial class obj_item_storage_photo_album_qm : obj_item_storage_photo_album
{
    // tg: code/modules/photography/photos/album.dm:136  (REGISTER_REQUIRED_MAP_ITEM is a unit test's)
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}

// tg: code/modules/photography/photos/album.dm:145
public partial class obj_item_storage_photo_album_ce : obj_item_storage_photo_album
{
    // tg: code/modules/photography/photos/album.dm:145  (REGISTER_REQUIRED_MAP_ITEM is a unit test's)
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}

// tg: code/modules/photography/photos/album.dm:154
public partial class obj_item_storage_photo_album_bar : obj_item_storage_photo_album
{
    // tg: code/modules/photography/photos/album.dm:154  (REGISTER_REQUIRED_MAP_ITEM is a unit test's)
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}

// tg: code/modules/photography/photos/album.dm:168
public partial class obj_item_storage_photo_album_library : obj_item_storage_photo_album
{
    // tg: code/modules/photography/photos/album.dm:168  (REGISTER_REQUIRED_MAP_ITEM is a unit test's)
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}

// tg: code/modules/photography/photos/album.dm:177
public partial class obj_item_storage_photo_album_chapel : obj_item_storage_photo_album
{
    // tg: code/modules/photography/photos/album.dm:177  (REGISTER_REQUIRED_MAP_ITEM is a unit test's)
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}

// tg: code/modules/photography/photos/album.dm:196
public partial class obj_item_storage_photo_album_prison : obj_item_storage_photo_album
{
    // tg: code/modules/photography/photos/album.dm:196  (REGISTER_REQUIRED_MAP_ITEM is a unit test's)
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}
