using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Version 40 (M9 slice E, protocol.md section 49): the radio prefixes of the player's worn headset, the big chat window's Radio list
// (chat.md 3). tg's say window has no such list (its radio prefixes are typed); the port's lists the channels the headset in the ears
// has on (radio.channels, its keys' order), each with the first key of tg's GLOB.department_radio_keys for it (":e " for Engineering).
// The list is read after the tick and sent when it changes, and in a join.

public sealed partial class Replicator
{
    private sealed partial class SessionState
    {
        private List<string> _radioSent;

        /// <summary>The RadioChannels message of the last <see cref="Update"/>, or null when the list did not change.</summary>
        public RadioChannelsMessage PendingRadioChannels { get; set; }

        private void UpdateRadioChannels(Replicator owner, client player, bool full)
        {
            // a list not yet taken stays pending (the join's is taken after the join's tick)
            List<string> prefixes = owner._world == null ? new List<string>() : RadioPrefixesOf(owner._world, player?.mob);
            if (!full && _radioSent != null && _radioSent.SequenceEqual(prefixes))
                return;
            _radioSent = prefixes;
            var message = new RadioChannelsMessage();
            message.Prefixes.AddRange(prefixes);
            PendingRadioChannels = message;
        }
    }

    // The prefixes of the channels that mob's worn radio has on (a human's ears slot), in the radio's order.
    // Read after the tick, outside the world: GLOB is the world's (TgState.Of), not the ambient one.
    internal static List<string> RadioPrefixesOf(Anomaly.Core.Kernel.World world, mob mob)
    {
        var prefixes = new List<string>();
        if (mob is not mob_living_carbon_human carbon || carbon.ears is not obj_item_radio radio || radio.channels == null)
            return prefixes;
        DmList keys = TgState.Of(world).GLOB?.department_radio_keys;
        foreach (object channel in Dm.each(radio.channels))
        {
            if (!Dm.truthy(radio.channels[channel]) || keys == null)
                continue;
            foreach (object key in Dm.each(keys))
            {
                string prefix = ":" + Dm.str(key) + " ";
                if (Dm.eq(keys[key], channel) && RadioPrefixes.IsValid(prefix))
                {
                    if (!prefixes.Contains(prefix) && prefixes.Count < Limits.MaxRadioChannels)
                        prefixes.Add(prefix);
                    break;
                }
            }
        }
        return prefixes;
    }

    /// <inheritdoc/>
    public RadioChannelsMessage TakeRadioChannels(int session)
    {
        if (!_sessions.TryGetValue(session, out SessionState state) || state.PendingRadioChannels == null)
            return null;
        RadioChannelsMessage taken = state.PendingRadioChannels;
        state.PendingRadioChannels = null;
        return taken;
    }
}
