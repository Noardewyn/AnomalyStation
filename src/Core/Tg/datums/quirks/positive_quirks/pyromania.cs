// Translated from tgstation (snapshot 1d99bd7f): code/datums/quirks/positive_quirks/pyromania.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate N3b, D316: the fear system's pyrophobia counts the fires nearby): /atom/proc/count_nearby_fire_sources; the
// pyromania quirk comes with the quirks; gaps MS1: its mood event's procs.

public partial class atom
{
    // tg: code/datums/quirks/positive_quirks/pyromania.dm:63
    public virtual object count_nearby_fire_sources(object radius = null, object weighted = null)
    {
        radius ??= 5;
        weighted ??= FALSE;
        float fire_size = 0;
        bool w = Dm.truthy(weighted);

        foreach (atom_movable thing in Dm.each<atom_movable>(view(radius, this)))
        {
            // scales based on fire stacks. also includes the mob itself.
            if (Dm.truthy((thing as mob_living)?.on_fire))
                fire_size += w ? (Dm.num(((mob_living)thing).fire_stacks) / 20) : 1;
            // giant pile of fire
            else if (Dm.truthy((thing as obj_structure_bonfire)?.burning))
                fire_size += w ? 1.0f : 1;
            // relatively safe, at least safer than a bonfire
            else if (Dm.truthy((thing as obj_structure_fireplace)?.lit))
                fire_size += w ? 0.5f : 1;
            // various small flames, but elevated if they are being held
            else if (Dm.truthy((thing as obj_item_flashlight_flare)?.light_on))
                fire_size += w ? (thing.loc == this ? 1.0f : 0.2f) : 1;
            else if (Dm.truthy((thing as obj_item_match)?.lit))
                fire_size += w ? (thing.loc == this ? 1.0f : 0.2f) : 1;
            else if (Dm.truthy((thing as obj_item_lighter)?.lit))
                fire_size += w ? (thing.loc == this ? 1.0f : 0.2f) : 1;
            // hotspots are huge walls of fire, but also spammed a lot, so weighted down a bit
            else if (thing is obj_effect_hotspot)
                fire_size += w ? 1.2f : 1;
            // any other random item being on fire, these are more uncontrolled, and potentially even being worn
            else if (Dm.truthy(Dm.bitand(thing.resistance_flags, ON_FIRE)))
                fire_size += w ? (thing.loc == this ? 2.0f : 0.2f) : 1;
        }

        return fire_size;
    }
}

// tg: code/datums/quirks/positive_quirks/pyromania.dm:92  (gaps MS1: the mood event's procs; the quirk is owner-deferred, D332)
public partial class datum_mood_event_pyromania : datum_mood_event
{
    // tg: code/datums/quirks/positive_quirks/pyromania.dm:96
    [DmParams("fire_size")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object fire_size = Dm.arg(__extra, 0);
        fire_size ??= 0f;
        mood_change = Dm.num(fire_size);
    }

    // tg: code/datums/quirks/positive_quirks/pyromania.dm:99
    [DmParams("home", "new_event", "fire_size")]
    public override object be_replaced(datum_mood home = null, datum_mood_event new_event = null, params object[] __extra)
    {
        object fire_size = Dm.arg(__extra, 0);
        fire_size ??= 0f;
        mood_change = max(mood_change, Dm.num(fire_size));
        return BLOCK_NEW_MOOD;
    }
}
