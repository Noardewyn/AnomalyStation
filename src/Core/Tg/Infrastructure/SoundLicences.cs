using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Anomaly.Core.Tg;

// D344: tg sounds whose licence tg's own attribution text gets wrong or leaves out. Plain BCL so that tools/Anomaly.Tools.Sounds
// compiles this same file (a link in its .csproj): the sound tool decides from it what it copies, and the server's lobby
// playlist leaves out what the tool does not copy.

/// <summary>Licence facts about tg's sounds that tg's attribution files do not state (checked at the source, 2026-11-20).</summary>
public static class SoundLicences
{
    /// <summary>
    /// Sounds with no licence at all to use them (D344): the sound tool never copies them and the server does not pick them as
    /// lobby music. Key: tg's path; value: why.
    /// </summary>
    public static readonly FrozenDictionary<string, string> Unlicensed = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["sound/music/lobby_music/title1.mod"] = "Flip-Flap by AceMan, taken from aminet.net with no licence (lobby_music/license.txt)",
        ["sound/music/lobby_music/title2.ogg"] = "a remix of the Robocop theme (lobby_music/license.txt)",
        ["sound/music/lobby_music/title3.ogg"] = "a remix of Tintin On The Moon, licence only assumed (lobby_music/license.txt)",
        ["sound/music/elevator/robocop-short.ogg"] = "the Robocop theme, no attribution",
        ["sound/announcer/alarm/clownops.ogg"] = "taken from YouTube (announcer/alarm/attribution.txt)",
        ["sound/announcer/alarm/airraid.ogg"] = "its Freesound page (Jwade722, 534550) is gone; no licence stated",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Sounds whose source page names a non-commercial licence that tg's attribution text omits or gets wrong (D344).
    /// Key: tg's path; value: the licence label.
    /// </summary>
    public static readonly FrozenDictionary<string, string> NonCommercialAtSource = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["sound/effects/compressed_air/tank_insert_clunky.ogg"] = "CC BY-NC 4.0 (Freesound 682952, Geoff-Bremner-Audio)",
        ["sound/items/can/can_shake.ogg"] = "CC BY-NC 4.0 (Freesound 456703, mcmast)",
        ["sound/items/handling/standard_stamp.ogg"] = "CC BY-NC 4.0 (Freesound 348316, tom_woysky)",
        // tg's attributions.txt says "Attribution 4.0"; the source page says BY-NC 4.0
        ["sound/mobs/humanoids/shadow/shadow_wail.ogg"] = "CC BY-NC 4.0 (Freesound 585631, Robinhood76)",
    }.ToFrozenDictionary(StringComparer.Ordinal);
}
