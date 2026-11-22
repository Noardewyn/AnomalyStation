// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/_common/antag_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A1 (D316): the whole file.

public static partial class GlobalProcs
{
    /// Returns MINDS of the assigned antags of given type/subtypes
    /// Supplying no antag type grants all minds with antag datums
    // tg: code/modules/antagonists/_common/antag_helpers.dm:3
    public static object get_antag_minds(object antag_type = null, float? specific = null)
    {
        specific ??= FALSE;
        DmList dot = Dm.list();
        foreach (datum_antagonist A in Dm.each<datum_antagonist>(GLOB.antagonists))
        {
            if (A.owner == null)
                continue;
            if (!Dm.truthy(antag_type) || !Dm.truthy(specific) && istype(A, antag_type as Type) || Dm.truthy(specific) && Dm.eq(A.type, antag_type))
                Dm.combine(ref dot, A.owner);
        }
        return dot;
    }

    /// From a list of players (minds, mobs or clients), finds the one with the highest playtime (either from a specific role or overall living) and returns it.
    /// If playtime tracking is disabled, just returns the first player in the list.
    // tg: code/modules/antagonists/_common/antag_helpers.dm:14
    public static object get_most_experienced(DmList players = null, object specific_role = null)
    {
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_use_exp_tracking)))) //woops
            return players[1];
        object most_experienced = null;
        foreach (object player in Dm.each(players))
        {
            if (!Dm.truthy(most_experienced))
            {
                most_experienced = player;
                continue;
            }
            client player_client = get_player_client(player) as client;
            if (player_client?.prefs == null || !Dm.truthy(length(player_client.prefs.exp)))
                continue;
            client most_played = get_player_client(most_experienced) as client;
            if (most_played?.prefs == null || !Dm.truthy(length(most_played.prefs.exp)))
            {
                most_experienced = player;
                continue;
            }
            float player_playtime;
            float most_playtime;
            if (Dm.truthy(specific_role))
            {
                player_playtime = Dm.truthy(player_client.prefs.exp[specific_role]) ? Dm.num(Dm.text2num(player_client.prefs.exp[specific_role])) : 0;
                most_playtime = Dm.truthy(most_played.prefs.exp[specific_role]) ? Dm.num(Dm.text2num(most_played.prefs.exp[specific_role])) : 0;
            }
            else
            {
                player_playtime = Dm.num(player_client.get_exp_living(TRUE));
                most_playtime = Dm.num(most_played.get_exp_living(TRUE));
            }
            if (player_playtime > most_playtime)
                most_experienced = player;
        }
        return most_experienced;
    }
}
