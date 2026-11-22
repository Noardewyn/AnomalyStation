// Translated from tgstation (snapshot 1d99bd7f): code/modules/instruments/stationary.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6c (D326): the whole file (the space piano and the minimoog). The var blocks are generated (D111).

// tg: code/modules/instruments/stationary.dm:1
public partial class obj_structure_musician : obj_structure
{
    // tg: code/modules/instruments/stationary.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        song = Dm.@new<datum_song_stationary>(this, allowed_instrument_ids);
        allowed_instrument_ids = null;
        return dot;
    }

    // tg: code/modules/instruments/stationary.dm:18
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref song);
        return base.Destroy(force);
    }

    // tg: code/modules/instruments/stationary.dm:22
    public virtual object can_play(atom music_player = null)
    {
        if (!Dm.truthy(anchored) && !Dm.truthy(can_play_unanchored))
            return FALSE;
        if (!ismob(music_player))
            return FALSE;
        var user = (mob)music_player;
        if (!ISADVANCEDTOOLUSER(user))
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        if (!Dm.truthy(Waiting.NoSleep(Adjacent(user))))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/instruments/stationary.dm:36
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        return await song.ui_interact(user);
    }

    // tg: code/modules/instruments/stationary.dm:39
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool, time: 4 * SECONDS);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/instruments/stationary.dm:44
public partial class obj_structure_musician_piano : obj_structure_musician
{
    // tg: code/modules/instruments/stationary.dm:53
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_falling_hazard), Dm.named("damage", 60), Dm.named("wound_bonus", 10), Dm.named("hardhat_safety", FALSE), Dm.named("crushes", TRUE), Dm.named("impact_sound", Dm.resource("sound/effects/piano_hit.ogg"))));
        Waiting.NoSleep(AddElement(typeof(datum_element_climbable)));
        Waiting.NoSleep(AddElement(typeof(datum_element_elevation), Dm.named("pixel_shift", 10)));
        return dot;
    }

    // tg: code/modules/instruments/stationary.dm:59
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        switch (damage_type)
        {
            case BRUTE:
                playsound(this, Dm.resource("sound/effects/piano_hit.ogg"), 100, TRUE);
                break;
            case BURN:
                playsound(this, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
                break;
        }
        return null;
    }

    // tg: code/modules/instruments/stationary.dm:66
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (!Dm.truthy(broken))
        {
            broken = TRUE;
            icon_state = broken_icon_state;
        }
        return dot;
    }
}
