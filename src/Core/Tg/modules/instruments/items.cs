// Translated from tgstation (snapshot 1d99bd7f): code/modules/instruments/items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J2, D298: the items; silicons SI6c, D326: their songs, the harmonica's speech, the spectral instruments and the
// instrument action). The var blocks are generated (D111).

//copy pasta of the space piano, don't hurt me -Pete
// tg: code/modules/instruments/items.dm:2
public partial class obj_item_instrument : obj_item
{
    // tg: code/modules/instruments/items.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        song = Dm.@new<datum_song_handheld>(this, allowed_instrument_ids, instrument_range);
        allowed_instrument_ids = null; //We don't need this clogging memory after its used.
        return dot;
    }

    // tg: code/modules/instruments/items.dm:23
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref song);
        return base.Destroy(force);
    }

    // tg: code/modules/instruments/items.dm:27
    public virtual object can_play(atom music_player = null)
    {
        if (!ismob(music_player))
            return FALSE;
        mob user = music_player as mob;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        if (user.get_item_by_slot(slot_flags) == this) //can't play while wearing the instrument
            return FALSE;
        if (!Dm.truthy(Waiting.NoSleep(Adjacent(user))))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/instruments/items.dm:39
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins to play 'Gloomy Sunday'! It looks like [] trying to commit suicide!", user, user.p_theyre())));
        await DmTask.CompletedTask;
        return BRUTELOSS;
    }

    // tg: code/modules/instruments/items.dm:43
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        return await song.ui_interact(user);
    }
}

// tg: code/modules/instruments/items.dm:117
public partial class obj_item_instrument_trumpet_spectral : obj_item_instrument_trumpet
{
    // tg: code/modules/instruments/items.dm:127
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_spooky), Dm.named("too_spooky", !Dm.truthy(single_use) ? TRUE : FALSE), Dm.named("single_use", single_use)));
        return dot;
    }

    // tg: code/modules/instruments/items.dm:131
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        playsound(this, Dm.resource("sound/runtime/instruments/trombone/En4.mid"), 1000, 1, -1);
        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }
}

// tg: code/modules/instruments/items.dm:145
public partial class obj_item_instrument_saxophone_spectral : obj_item_instrument_saxophone
{
    // tg: code/modules/instruments/items.dm:155
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_spooky), Dm.named("too_spooky", !Dm.truthy(single_use) ? TRUE : FALSE), Dm.named("single_use", single_use)));
        return dot;
    }

    // tg: code/modules/instruments/items.dm:159
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        playsound(this, Dm.resource("sound/runtime/instruments/saxophone/En4.mid"), 1000, 1, -1);
        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }
}

// tg: code/modules/instruments/items.dm:173
public partial class obj_item_instrument_trombone_spectral : obj_item_instrument_trombone
{
    // tg: code/modules/instruments/items.dm:183
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_spooky), Dm.named("too_spooky", !Dm.truthy(single_use) ? TRUE : FALSE), Dm.named("single_use", single_use)));
        return dot;
    }

    // tg: code/modules/instruments/items.dm:190
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        playsound(this, Dm.resource("sound/runtime/instruments/trombone/Cn4.mid"), 1000, 1, -1);
        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }
}

// tg: code/modules/instruments/items.dm:202
public partial class obj_item_instrument_harmonica : obj_item_instrument
{
    // tg: code/modules/instruments/items.dm:214
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, slot_flags)))
            return;
        RegisterSignal(user, COMSIG_MOB_SAY, nameof(handle_speech));
    }

    // tg: code/modules/instruments/items.dm:220
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        UnregisterSignal(user, COMSIG_MOB_SAY);
        return dot;
    }

    // tg: code/modules/instruments/items.dm:224
    public virtual void handle_speech(datum source = null, DmList speech_args = null)
    {
        if (!Dm.truthy(song.playing))
            return;
        if (!ismob(loc))
            throw Dm.CRASH(Dm.text("[] was still registered to listen in on [] but was not found to be on their mob.", this, source), "code/modules/instruments/items.dm", 229);
        to_chat(loc, span_warning(I18n.T("You stop playing the harmonica to talk...")));
        song.playing = FALSE;
    }
}

// tg: code/modules/instruments/items.dm:233
public partial class datum_action_item_action_instrument : datum_action_item_action
{
    // tg: code/modules/instruments/items.dm:237
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        if (target is not obj_item_instrument instrument)
            return FALSE;
        await instrument.interact(World.Current.usr as mob);
        return TRUE;
    }
}
