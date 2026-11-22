// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/mass_hallucination.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4 (D327): the whole file but the admins' forced hallucination (/datum/event_admin_setup, as the other events'): every living
// carbon on the station hallucinates the same thing (a sound, a station message, a delusion, or a generic hallucination). The var blocks
// are generated (D111).

// tg: code/modules/events/mass_hallucination.dm:13
public partial class datum_round_event_mass_hallucination : datum_round_event
{
    // tg: code/modules/events/mass_hallucination.dm:20
    public override async DmTask<object> start()
    {
        if (!Dm.truthy(admin_forced_hallucination))
        {
            float category_to_pick_from = rand(1, 10);
            switch ((int)category_to_pick_from)
            {
                case 1:
                    // Send the same sound to everyone
                    admin_forced_hallucination = get_random_valid_hallucination_subtype(typeof(datum_hallucination_fake_sound_normal));
                    break;
                case 2:
                    // Send the same sound to everyone, but weird
                    admin_forced_hallucination = get_random_valid_hallucination_subtype(typeof(datum_hallucination_fake_sound_weird));
                    break;
                case 3:
                    // Send the same message to everyone
                    admin_forced_hallucination = get_random_valid_hallucination_subtype(typeof(datum_hallucination_station_message));
                    break;
                case 4:
                    // Send the same delusion to everyone, but...
                    admin_forced_hallucination = get_random_valid_hallucination_subtype(typeof(datum_hallucination_delusion_preset));
                    // The delusion will affect everyone BUT the hallucinator.
                    admin_forced_args = Dm.list(
                        Dm.kv("duration", 30 * SECONDS),
                        Dm.kv("skip_nearby", FALSE),
                        Dm.kv("affects_us", FALSE),
                        Dm.kv("affects_others", TRUE),
                        Dm.kv("play_wabbajack", FALSE)
                    );
                    break;
                case 5:
                    // Send the same delusion to everyone, but...
                    admin_forced_hallucination = get_random_valid_hallucination_subtype(typeof(datum_hallucination_delusion_preset));
                    // The delusion will affect only the hallucinator.
                    admin_forced_args = Dm.list(
                        Dm.kv("duration", 45 * SECONDS),
                        Dm.kv("skip_nearby", FALSE),
                        Dm.kv("affects_us", TRUE),
                        Dm.kv("affects_others", FALSE),
                        Dm.kv("play_wabbajack", TRUE)
                    );
                    break;
                default: // 6 to 10
                    // Send the same generic hallucination type to everyone
                    var generic_hallucinations = TgState.Current.datum_round_event_mass_hallucination__generic_hallucinations ??= Dm.list(
                        typeof(datum_hallucination_bolts),
                        typeof(datum_hallucination_chat),
                        typeof(datum_hallucination_death),
                        typeof(datum_hallucination_fake_flood),
                        typeof(datum_hallucination_fire),
                        typeof(datum_hallucination_message),
                        typeof(datum_hallucination_oh_yeah),
                        typeof(datum_hallucination_xeno_attack)
                    );
                    admin_forced_hallucination = pick(generic_hallucinations);
                    break;
            }

            if (!Dm.truthy(admin_forced_hallucination))
                throw Dm.CRASH(Dm.text("[] couldn't find a hallucination to play. (Got: [], Picked category: [])", type, admin_forced_hallucination, category_to_pick_from), "code/modules/events/mass_hallucination.dm", 74);
        }

        var hallucination_args = Dm.list(admin_forced_hallucination, "mass hallucination");
        if (admin_forced_args != null)
            Dm.append(hallucination_args, admin_forced_args);

        // We'll only hallucinate for carbons now, even though livings can hallucinate just fine in most cases.
        foreach (mob_living_carbon hallucinating in Dm.each_cast<mob_living_carbon>(GLOB.carbon_list.Copy()))
        {
            // If they're on centcom, skip them entirely.
            if (Dm.truthy(is_centcom_level(hallucinating.z)))
                continue;
            // We can skip dead carbons as well
            if (hallucinating.stat == DEAD)
                continue;
            // Hallucinations can have side effects on mobs, like being stunned,
            // so we'll play the hallucination to clientless mobs as well.
            // Unless the mob is off the station z-level. It's unlikely anyone will notice.
            if (!Dm.eq(hallucinating.z, 0) && !Dm.truthy(is_station_level(hallucinating.z)) && hallucinating.client == null)
                continue;
            // Not using the wrapper here because we already have a list / arglist
            await hallucinating._cause_hallucination(hallucination_args);
        }
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/events/mass_hallucination.dm:61  var/static/list/generic_hallucinations
    internal DmList datum_round_event_mass_hallucination__generic_hallucinations;
}
