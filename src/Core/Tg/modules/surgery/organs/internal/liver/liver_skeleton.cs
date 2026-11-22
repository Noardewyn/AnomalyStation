// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/liver/liver_skeleton.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1, D339, D341): the skeleton's mass of bones, which works in any carbon. The var block is generated (D111).

/**
 * Bone liver
 * Gives the owner liverless metabolism, makes them vulnerable to bone hurting juice and
 * makes milk heal them through meme magic.
 **/
// tg: code/modules/surgery/organs/internal/liver/liver_skeleton.dm:6
public partial class obj_item_organ_liver_bone : obj_item_organ_liver
{
    // tg: code/modules/surgery/organs/internal/liver/liver_skeleton.dm:17
    public override object handle_chemical(mob_living_carbon organ_owner = null, datum_reagent chem = null, object seconds_per_tick = null, object metabolization_rate = null)
    {
        object dot = base.handle_chemical(organ_owner, chem, seconds_per_tick, metabolization_rate);
        // parent returned COMSIG_MOB_STOP_REAGENT_TICK or we are failing
        if (Dm.truthy(Dm.bitand(dot, COMSIG_MOB_STOP_REAGENT_TICK)) || Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
            return dot;
        if (istype(chem, typeof(datum_reagent_toxin_bonehurtingjuice)))
        {
            Waiting.NoSleep(organ_owner.adjust_stamina_loss(3.25f * Dm.num(seconds_per_tick), updating_stamina: FALSE));
            Waiting.NoSleep(organ_owner.adjust_brute_loss(0.25f * Dm.num(seconds_per_tick), updating_health: FALSE));
            if (SPT_PROB(10, seconds_per_tick))
            {
                float sw__1 = Dm.num(rand(1, 3));
                if (sw__1 == 1)
                    Waiting.Detach(organ_owner.say(pick(I18n.T("oof."), I18n.T("ouch."), I18n.T("my bones."), I18n.T("oof ouch."), I18n.T("oof ouch my bones.")), forced: chem.type)); // INVOKE_ASYNC
                else if (sw__1 == 2)
                    organ_owner.manual_emote(pick(I18n.T("oofs silently."), I18n.T("looks like [] bones hurt.", organ_owner.p_their()), I18n.T("grimaces, as though [] bones hurt.", organ_owner.p_their())));
                else if (sw__1 == 3)
                    to_chat(organ_owner, span_warning(I18n.T("Your bones hurt!")));
            }
            if (Dm.truthy(chem.overdosed))
            {
                if (SPT_PROB(2, seconds_per_tick)) //big oof
                {
                    object selected_part = pick(BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG); //God help you if the same limb gets picked twice quickly...
                    obj_item_bodypart bodypart = organ_owner.get_bodypart(Dm.str(selected_part)) as obj_item_bodypart; //We're so sorry skeletons, you're so misunderstood
                    if (bodypart != null)
                    {
                        playsound(organ_owner, SFX_DESECRATION, 50, vary: TRUE); //You just want to socialize
                        organ_owner.visible_message(span_warning(I18n.T("[] rattles loudly and flails around!!", organ_owner)), span_danger(I18n.T("Your bones hurt so much that your missing muscles spasm!!")));
                        Waiting.Detach(organ_owner.say(I18n.T("OOF!!"), forced: chem.type)); // INVOKE_ASYNC
                        Waiting.NoSleep(organ_owner.apply_damage(200, BRUTE, bodypart));
                    }
                    else
                    {
                        to_chat(organ_owner, span_warning(I18n.T("Your missing [] aches from wherever you left it.", parse_zone(selected_part))));
                        Waiting.Detach(organ_owner.emote("sigh")); // INVOKE_ASYNC
                    }
                }
            }
            Waiting.NoSleep(((datum_reagents)organ_owner.reagents).remove_reagent(chem.type, Dm.num(chem.metabolization_rate) * Dm.num(seconds_per_tick)));
            return COMSIG_MOB_STOP_REAGENT_TICK; // Stop metabolism
        }
        if (chem.type == typeof(datum_reagent_consumable_milk))
        {
            if (Dm.num(chem.volume) > 50)
            {
                Waiting.NoSleep(((datum_reagents)organ_owner.reagents).remove_reagent(chem.type, Dm.num(chem.volume) - 50));
                to_chat(organ_owner, span_warning(I18n.T("The excess milk is dripping off your bones!")));
            }
            Waiting.NoSleep(organ_owner.heal_bodypart_damage(0.5f * milk_brute_healing * Dm.num(seconds_per_tick), 0.5f * milk_burn_healing * Dm.num(seconds_per_tick)));
            foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(organ_owner.all_wounds))
                Waiting.NoSleep(iter_wound.on_xadone(0.5f * Dm.num(seconds_per_tick)));
            return dot; // Do normal metabolism
        }
        return dot;
    }
}
