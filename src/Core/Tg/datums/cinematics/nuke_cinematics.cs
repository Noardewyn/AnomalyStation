// Translated from tgstation (snapshot 1d99bd7f): code/datums/cinematics/nuke_cinematics.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1a, D316). The var blocks are generated (D111).

/// Simple, base cinematic for all animations based around a nuke detonating.
// tg: code/datums/cinematics/nuke_cinematics.dm:2
public partial class datum_cinematic_nuke : datum_cinematic
{
    // tg: code/datums/cinematics/nuke_cinematics.dm:6
    public override async DmTask play_cinematic()
    {
        flick("intro_nuke", screen);
        await stoplag(3.5f * SECONDS);
        await play_nuke_effect();
        if (special_callback != null)
            await special_callback.Invoke();
        if (Dm.truthy(after_nuke_summary_state))
            screen.icon_state = Dm.str(after_nuke_summary_state);
    }

    /// Specific effects for each type of cinematics goes here.
    // tg: code/datums/cinematics/nuke_cinematics.dm:16
    public virtual DmTask play_nuke_effect()
    {
        return DmTask.CompletedTask;
    }
}

/// The syndicate nuclear bomb was activated, and destroyed the station!
// tg: code/datums/cinematics/nuke_cinematics.dm:20
public partial class datum_cinematic_nuke_ops_victory : datum_cinematic_nuke
{
    // tg: code/datums/cinematics/nuke_cinematics.dm:23
    public override DmTask play_nuke_effect()
    {
        flick("station_explode_fade_red", screen);
        play_cinematic_sound(sound("sound/effects/explosion/explosion_distant.ogg"));
        return DmTask.CompletedTask;
    }
}

/// The syndicate nuclear bomb was activated, but just barely missed the station!
// tg: code/datums/cinematics/nuke_cinematics.dm:28
public partial class datum_cinematic_nuke_ops_miss : datum_cinematic_nuke
{
    // tg: code/datums/cinematics/nuke_cinematics.dm:31
    public override DmTask play_nuke_effect()
    {
        flick("station_intact_fade_red", screen);
        play_cinematic_sound(sound("sound/effects/explosion/explosion_distant.ogg"));
        return DmTask.CompletedTask;
    }
}

/// The self destruct, or another station-destroying entity like a blob, destroyed the station!
// tg: code/datums/cinematics/nuke_cinematics.dm:36
public partial class datum_cinematic_nuke_self_destruct : datum_cinematic_nuke
{
    // tg: code/datums/cinematics/nuke_cinematics.dm:39
    public override DmTask play_nuke_effect()
    {
        flick("station_explode_fade_red", screen);
        play_cinematic_sound(sound("sound/effects/explosion/explosion_distant.ogg"));
        return DmTask.CompletedTask;
    }
}

/// The self destruct was activated, yet somehow avoided destroying the station!
// tg: code/datums/cinematics/nuke_cinematics.dm:44
public partial class datum_cinematic_nuke_self_destruct_miss : datum_cinematic_nuke
{
    // tg: code/datums/cinematics/nuke_cinematics.dm:47
    public override async DmTask play_nuke_effect()
    {
        play_cinematic_sound(sound("sound/effects/explosion/explosion_distant.ogg"));
        if (special_callback != null)
            await special_callback.Invoke();
    }
}

/// The syndicate nuclear bomb was activated, and the nuclear operatives failed to extract on their shuttle before it detonated on the station!
// tg: code/datums/cinematics/nuke_cinematics.dm:52
public partial class datum_cinematic_nuke_mutual_destruction : datum_cinematic_nuke
{
    // tg: code/datums/cinematics/nuke_cinematics.dm:55
    public override DmTask play_nuke_effect()
    {
        flick("station_explode_fade_red", screen);
        play_cinematic_sound(sound("sound/effects/explosion/explosion_distant.ogg"));
        return DmTask.CompletedTask;
    }
}

/// A blood cult summoned Nar'sie, but central command deployed a nuclear package to stop them.
// tg: code/datums/cinematics/nuke_cinematics.dm:60
public partial class datum_cinematic_nuke_cult : datum_cinematic_nuke
{
    // tg: code/datums/cinematics/nuke_cinematics.dm:63
    public override DmTask play_nuke_effect()
    {
        flick("station_explode_fade_red", screen);
        play_cinematic_sound(sound("sound/effects/explosion/explosion_distant.ogg"));
        return DmTask.CompletedTask;
    }
}

/// A fake version of the nuclear detonation, where it winds up, but doesn't explode.
// tg: code/datums/cinematics/nuke_cinematics.dm:68
public partial class datum_cinematic_nuke_fake : datum_cinematic_nuke
{
    // tg: code/datums/cinematics/nuke_cinematics.dm:71
    public override DmTask play_nuke_effect()
    {
        play_cinematic_sound(sound("sound/items/bikehorn.ogg"));
        flick("summary_selfdes", screen); //???
        return DmTask.CompletedTask;
    }
}

/// The clown operative nuclear bomb was activated and clowned the station!
// tg: code/datums/cinematics/nuke_cinematics.dm:76
public partial class datum_cinematic_nuke_clown : datum_cinematic_nuke
{
    // tg: code/datums/cinematics/nuke_cinematics.dm:79
    public override DmTask play_nuke_effect()
    {
        play_cinematic_sound(sound("sound/items/airhorn/airhorn.ogg"));
        flick("summary_selfdes", screen); //???
        return DmTask.CompletedTask;
    }
}

/// A fake version of the nuclear detonation, where it winds up, but doesn't explode as the nuke core within was missing.
// tg: code/datums/cinematics/nuke_cinematics.dm:84
public partial class datum_cinematic_nuke_no_core : datum_cinematic_nuke
{
    // tg: code/datums/cinematics/nuke_cinematics.dm:87
    public override async DmTask play_nuke_effect()
    {
        flick("station_intact", screen);
        play_cinematic_sound(sound("sound/ambience/misc/signal.ogg"));
        await stoplag(10 * SECONDS);
    }
}

/// The syndicate nuclear bomb was activated, but just missed the station by a whole z-level!
// tg: code/datums/cinematics/nuke_cinematics.dm:93
public partial class datum_cinematic_nuke_far_explosion : datum_cinematic_nuke
{
    // tg: code/datums/cinematics/nuke_cinematics.dm:96
    public override async DmTask play_cinematic()
    {
        // This one has no intro sequence.
        // It's actually just a global sound, which makes you wonder why it's a cinematic.
        play_cinematic_sound(sound("sound/effects/explosion/explosion_distant.ogg"));
        if (special_callback != null)
            await special_callback.Invoke();
    }
}
