// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_fluff.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file (the themes: magic, tech, miner, carp). The var blocks are generated.

/**
 * Defines a theme used by guardian mobs for visuals and some text output
 * The default is used for ones created by wizards
 */
// tg: code/modules/mob/living/basic/guardian/guardian_fluff.dm:5
public partial class datum_guardian_fluff : datum
{
    /// Applies relevant visual properties to our guardian
    // tg: code/modules/mob/living/basic/guardian/guardian_fluff.dm:44
    public virtual void apply(mob_living_basic_guardian guardian = null)
    {
        guardian.name = name;
        guardian.real_name = name;
        guardian.bubble_icon = bubble_icon;
        guardian.icon_living = icon_state;
        guardian.icon_state = icon_state;

        guardian.speak_emote = speak_emote;
        guardian.attack_verb_continuous = attack_verb_continuous;
        guardian.attack_verb_simple = attack_verb_simple;
        guardian.attack_sound = attack_sound;
        guardian.attack_vis_effect = attack_vis_effect;

        guardian.overlay = mutable_appearance(guardian.icon, overlay_state) as mutable_appearance;
    }

    /// Output an appropriate fluff string for our guardian when it is created
    // tg: code/modules/mob/living/basic/guardian/guardian_fluff.dm:60
    public virtual object get_fluff_string(object guardian_type = null)
    {
        return span_holoparasite(Dm.str(guardian_fluff[guardian_type] is var or__1 && Dm.truthy(or__1) ? or__1 : "You bring forth a glitching abomination, something which should not be! Please contact a coder about it."));
    }
}
