// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/vibebot/vibebot_abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

/**
 * Vibebot's vibe ability
 *
 * Given to vibebots so sentient ones can change/reset thier colors at will.
 */
// tg: code/modules/mob/living/basic/bots/vibebot/vibebot_abilities.dm:7
public partial class datum_action_cooldown_mob_cooldown_bot_vibe : datum_action_cooldown_mob_cooldown_bot
{
    // tg: code/modules/mob/living/basic/bots/vibebot/vibebot_abilities.dm:16
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        if (grant_to == null)
            return dot;
        RegisterSignal(grant_to, COMSIG_BOT_RESET, nameof(remove_colors));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/vibebot/vibebot_abilities.dm:22
    public override async DmTask<object> Activate(atom target = null)
    {
        if (target == owner)
        {
            remove_colors();
            return TRUE;
        }
        await vibe();
        StartCooldown();
        return TRUE;
    }

    ///Gives a random color
    // tg: code/modules/mob/living/basic/bots/vibebot/vibebot_abilities.dm:31
    public virtual async DmTask vibe()
    {
        mob_living_basic_bot bot_owner = (mob_living_basic_bot)owner;
        bool emagged = Dm.truthy(Dm.bitand(bot_owner.bot_access_flags, BOT_COVER_EMAGGED));
        string final_color = emagged ? COLOR_GRAY : Dm.text("#[]", random_color());
        await owner.remove_atom_colour(TEMPORARY_COLOUR_PRIORITY);
        await owner.add_atom_colour(final_color, TEMPORARY_COLOUR_PRIORITY);
        owner.set_light_color(owner.color);
        if (!(change_mood <= world.time)) // COOLDOWN_FINISHED, inline
            return;
        Type mood_to_add = emagged ? typeof(datum_mood_event_depressing_party) : typeof(datum_mood_event_festive_party);
        foreach (mob_living_carbon_human human_target in Dm.each<mob_living_carbon_human>(oview(1, owner)))
            await human_target.add_mood_event("vibebot_party", mood_to_add);
        change_mood = world.time + 30 * SECONDS; // COOLDOWN_START, VIBE_MOOD_TIMER
    }

    ///Removes all colors
    // tg: code/modules/mob/living/basic/bots/vibebot/vibebot_abilities.dm:45
    [SignalHandler]
    public virtual void remove_colors()
    {
        Waiting.NoSleep(owner.remove_atom_colour(TEMPORARY_COLOUR_PRIORITY));
        owner.set_light_color(null);
    }
}
