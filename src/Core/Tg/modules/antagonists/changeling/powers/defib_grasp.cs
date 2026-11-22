// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/defib_grasp.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Defibrillator Grasp (the passive revival when a defibrillator is applied to the dead or stasis
// changeling, the defibber's arms torn off or the cyborg's sensors overloaded). The var block is generated (D111).

// tg: code/modules/antagonists/changeling/powers/defib_grasp.dm:1
public partial class datum_action_changeling_defib_grasp : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/defib_grasp.dm:17
    public override async DmTask on_purchase(mob user = null, object is_respec = null)
    {
        await base.on_purchase(user, is_respec);
        RegisterSignal(user, COMSIG_DEFIBRILLATOR_PRE_HELP_ZAP, nameof(on_defibbed));
    }

    /// Signal proc for [COMSIG_DEFIBRILLATOR_PRE_HELP_ZAP].
    // tg: code/modules/antagonists/changeling/powers/defib_grasp.dm:21
    [SignalHandler]
    public virtual object on_defibbed(mob_living_carbon source = null, mob_living defibber = null, obj_item_shockpaddles defib = null)
    {
        if (source.stat != DEAD && !HAS_TRAIT_FROM(source, TRAIT_FAKEDEATH, CHANGELING_TRAIT))
            return null;

        Waiting.Detach(execute_defib(source, defibber, defib));
        return COMPONENT_DEFIB_STOP;
    }

    /// Executes the defib action, causing the changeling to fully heal and get up.
    // tg: code/modules/antagonists/changeling/powers/defib_grasp.dm:31
    public virtual async DmTask execute_defib(mob_living_carbon changeling = null, mob_living defibber = null, obj_item_shockpaddles defib = null)
    {
        await remove_arms(changeling, defibber, defib);

        if (changeling.stat == DEAD)
            await changeling.revive(heal_flags);
        else
            await changeling.fully_heal(heal_flags);

        if (changeling.buckled != null)
            await changeling.buckled.unbuckle_mob(changeling); // get us off of stasis beds please
        await changeling.set_resting(FALSE);
        await changeling.adjust_jitter(20 * SECONDS);
        await changeling.emote("scream");
        playsound(changeling, Dm.resource("sound/effects/magic/demon_consume.ogg"), 50, TRUE);

        // Mimics some real defib stuff (wish this was more generalized)
        playsound(defib, SFX_BODYFALL, 50, TRUE);
        playsound(defib, Dm.resource("sound/machines/defib/defib_zap.ogg"), 75, TRUE, -1);
        playsound(defib, Dm.resource("sound/machines/defib/defib_success.ogg"), 50, FALSE); // I guess
        await defib.shock_pulling(30, changeling);
    }

    /// Removes the arms of the defibber if they're a carbon, and stuns them for a bit.
    /// If they're a cyborg, they'll just get stunned instead.
    // tg: code/modules/antagonists/changeling/powers/defib_grasp.dm:53
    public virtual async DmTask remove_arms(mob_living_carbon changeling = null, mob_living defibber = null, obj_item_shockpaddles defib = null)
    {
        if (iscyborg(defibber))
        {
            if (Dm.truthy(await defibber.flash_act(affect_silicon: TRUE)))
                to_chat(defibber, span_userdanger(I18n.T("[] awakens suddenly, overloading your sensors!", changeling)));
            // run default visible message regardless, no overt indication of the cyborg being overloaded to watchers
        }
        else
        {
            await defibber.Stun(4 * SECONDS); // stuck defibbing

            if (iscarbon(defibber))
            {
                float removed_arms = 0;
                mob_living_carbon carbon_defibber = defibber as mob_living_carbon;
                foreach (obj_item_bodypart_arm limb in Dm.each<obj_item_bodypart_arm>(carbon_defibber.get_bodyparts()))
                {
                    if (Dm.truthy(await limb.dismember(silent: FALSE)))
                    {
                        removed_arms++;
                        qdel(limb);
                    }
                }

                if (removed_arms != 0)
                {
                    // OH GOOD HEAVENS
                    await defibber.adjust_jitter(3 * MINUTES);
                    await defibber.adjust_dizzy(1 * MINUTES);
                    await defibber.adjust_stutter(1 * MINUTES);
                    await defibber.adjust_eye_blur(10 * SECONDS);
                    await defibber.emote("scream");

                    changeling.visible_message(
                        span_bolddanger(I18n.T("[] awakens suddenly, snatching [] out of []'s hands while ripping off [][] arms!", changeling, defib, defibber, removed_arms >= 2 ? "" : I18n.T("one of "), defibber.p_their())),
                        vision_distance: COMBAT_MESSAGE_RANGE,
                        ignored_mobs: Dm.list(changeling, defibber));
                    to_chat(changeling, span_changeling(I18n.T("The power of [] course through us, reviving us from our stasis! With this newfound energy, we snap [][]'s arms off!", defib, removed_arms >= 2 ? "" : I18n.T("one of "), defibber)));
                    to_chat(defibber, span_userdanger(I18n.T("[] awakens suddenly, snapping []your arms off!", changeling, removed_arms >= 2 ? "" : I18n.T("one of "))));
                    return; // no default message if we got an arm
                }
            }
        }

        changeling.visible_message(
            span_bolddanger(I18n.T("[] awakens suddenly!", changeling)),
            vision_distance: COMBAT_MESSAGE_RANGE,
            ignored_mobs: changeling);
        to_chat(changeling, span_changeling(I18n.T("The power of [] course through us, reviving us from our stasis!", defib)));
    }
}
