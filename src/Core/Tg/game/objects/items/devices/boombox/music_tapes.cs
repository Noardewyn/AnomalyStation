// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/boombox/music_tapes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The music tapes the boombox plays: each holds its song, a `/datum/looping_sound/boombox` kind made
// at Initialize (tg keeps the type and then the sound in one var, `song_inside`, which the converter typed as a path: hand-declared
// here as tg's wider value, rules 3.2, so hand mode keeps the generated constructor here too); the jazz tape's lore; the box of five
// tapes. The var block is generated (D111).

// tg: code/game/objects/items/devices/boombox/music_tapes.dm:2
public partial class obj_item_music_tape : obj_item
{
    // tg: code/game/objects/items/devices/boombox/music_tapes.dm:17  var/datum/looping_sound/song_inside (a type, then the sound)
    public object song_inside = typeof(datum_looping_sound_boombox);

    // tg: code/game/objects/items/devices/boombox/music_tapes.dm:3  (the generated constructor, kept here: hand mode, see the header)
    public obj_item_music_tape()
    {
        name = "tapedeck";
        desc = "A special tape that holds music.";
        icon_state = "tape_white";
        icon = Dm.resource("icons/obj/devices/circuitry_n_data.dmi");
        inhand_icon_state = "analyzer";
        lefthand_file = Dm.resource("icons/mob/inhands/equipment/tools_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/equipment/tools_righthand.dmi");
        w_class = 1;
        force = 1;
        throwforce = 0;
        obj_flags = 8;
        drop_sound = Dm.resource("sound/items/handling/tape_drop.ogg");
        pickup_sound = Dm.resource("sound/items/handling/tape_pickup.ogg");
    }

    // tg: code/game/objects/items/devices/boombox/music_tapes.dm:11  (the generated var initializer, kept here: hand mode)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), 2), Dm.kv(typeof(datum_material_glass), 2)); // tg: code/game/objects/items/devices/boombox/music_tapes.dm:11
    }

    // tg: code/game/objects/items/devices/boombox/music_tapes.dm:21
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You could probably play this in a Nanotrasen Brand [] Nanomusic [] boombox, at least until they trademark the concept of a boombox, too.",
            span_smallnoticeital(I18n.T("tm")), span_smallnoticeital(I18n.T("tm")))));
        if (Dm.truthy(song_name) && Dm.truthy(song_inside))
            Dm.append(ref dot, span_notice(I18n.T("The track is labled [].", span_boldnotice(song_name))));
        return dot;
    }

    // tg: code/game/objects/items/devices/boombox/music_tapes.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(song_inside))
            throw Dm.CRASH("We spawned a tape deck without any looping audio inside.", "code/game/objects/items/devices/boombox/music_tapes.dm", 31);
        song_inside = Dm.@new((Type)song_inside, this);
        return dot;
    }
}

// tg: code/game/objects/items/devices/boombox/music_tapes.dm:44
public partial class obj_item_music_tape_jazz : obj_item_music_tape
{
    // tg: code/game/objects/items/devices/boombox/music_tapes.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(add_deep_lore());
        return dot;
    }

    // tg: code/game/objects/items/devices/boombox/music_tapes.dm:54
    public virtual async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore", I18n.T("Jazz is, by all definitions, completely legal in the spinward sector. After all, sounds tend not to be the sort of thing that a major corporation can effectively police. Freeform Jazz, on the other hand, had taken on a scandalous identity in the late 2590s, following the legendary crime spree left by freeform jazz musician and criminal mastermind []. In an attempt to prevent a repeat of what happened on Rhozz Alpha, which we ALL remember, TerraGov and a multinational coalition of planets deemed Freeform Jazz to be an active threat to public safety. The more free the jazz, the more serious the crime.",
                span_bold(I18n.T("Rezz 'The Tips' C'thoni")))));
    }
}

// tg: code/game/objects/items/devices/boombox/music_tapes.dm:76  This exists mostly for testing
public partial class obj_item_storage_box_music_tapes : obj_item_storage_box
{
    // tg: code/game/objects/items/devices/boombox/music_tapes.dm:81
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_music_tape), 1),
            Dm.kv(typeof(obj_item_music_tape_rock), 1),
            Dm.kv(typeof(obj_item_music_tape_hiphop), 1),
            Dm.kv(typeof(obj_item_music_tape_pop), 1),
            Dm.kv(typeof(obj_item_music_tape_robocop), 1));
        generate_items_inside(items_inside, this);
    }
}
