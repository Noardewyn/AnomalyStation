// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/fake_message.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The var block is generated (D111).

// tg: code/modules/hallucination/fake_message.dm:1
public partial class datum_hallucination_message : datum_hallucination
{
    // tg: code/modules/hallucination/fake_message.dm:5
    public override async DmTask<object> start()
    {
        if (IS_UNCONSCIOUS(hallucinator))
            return FALSE;

        DmList nearby_humans = Dm.list();
        bool adjacent_to_us = false;
        mob_living_carbon_human suspicious_personnel = null;
        foreach (mob_living_carbon_human nearby_human in Dm.each<mob_living_carbon_human>(oview(7, hallucinator)))
        {
            if (get_dist(nearby_human, hallucinator) <= 1)
            {
                suspicious_personnel = nearby_human;
                adjacent_to_us = true;
                break;
            }
            Dm.append(nearby_humans, nearby_human);
        }

        if (suspicious_personnel == null && length(nearby_humans) != 0)
            suspicious_personnel = pick(nearby_humans) as mob_living_carbon_human;

        DmList message_pool = Dm.list();
        if (suspicious_personnel != null)
        {
            if (adjacent_to_us)
                message_pool[span_warning("You feel a tiny prick!")] = 5;

            obj_item_storage equipped_backpack = suspicious_personnel.get_item_by_slot(ITEM_SLOT_BACK) as obj_item_storage;
            if (equipped_backpack != null)
            {
                // in the future, this could / should be de-harcoded and
                // just draw from a pool uplink, theft, and antag item typepaths
                Type stashed_item = pick(
                    typeof(obj_item_blueprints),
                    typeof(obj_item_assembly_flash),
                    typeof(obj_item_card_id_advanced_gold_captains_spare),
                    typeof(obj_item_card_emag),
                    typeof(obj_item_circular_saw),
                    typeof(obj_item_codex_cicatrix),
                    typeof(obj_item_grenade_c4),
                    typeof(obj_item_gun_ballistic_revolver),
                    typeof(obj_item_gun_energy_e_gun_hos),
                    typeof(obj_item_gun_energy_laser_captain),
                    typeof(obj_item_gun_energy_recharge_ebow),
                    typeof(obj_item_gun_syringe_syndicate),
                    typeof(obj_item_hand_tele),
                    typeof(obj_item_melee_baton_security),
                    typeof(obj_item_melee_cultblade_dagger),
                    typeof(obj_item_melee_energy),
                    typeof(obj_item_powersink), //this is a bulky item what
                    typeof(obj_item_reagent_containers_hypospray_cmo),
                    typeof(obj_item_spellbook)) as Type;
                message_pool[span_notice(Dm.text("[] puts the [] into [].", suspicious_personnel, Dm.initial(stashed_item, (obj_item p) => p.name), equipped_backpack))] = 5;
            }

            message_pool[Dm.text("[] [].", span_bold(Dm.text("[]", suspicious_personnel)), pick("sneezes", "coughs"))] = 1;
        }

        message_pool[span_notice("You hear something squeezing through the ducts...")] = 1;

        message_pool[span_warning(Dm.text("Your [] itches.", pick("arm", "leg", "back", "head")))] = 1;
        message_pool[span_warning(Dm.text("You feel [].", pick("hot", "cold", "dry", "wet", "woozy", "faint")))] = 1;
        message_pool[span_warning("Your stomach rumbles.")] = 1;
        message_pool[span_warning("Your head hurts.")] = 1;
        message_pool[span_warning("You hear a faint buzz in your head.")] = 1;

        if (prob(10))
        {
            message_pool[span_warning("Behind you.")] = 1;
            message_pool[span_warning("You hear a faint laughter.")] = 1;
            message_pool[span_warning("You hear skittering on the ceiling.")] = 1;
            message_pool[span_warning("You see an inhumanly tall silhouette moving in the distance.")] = 2;
        }

        if (prob(30))
        {
            object some_help = pick_list_replacements(HALLUCINATION_FILE, "advice");
            message_pool[some_help] = 4;
        }

        object chosen = pick_weight(message_pool);
        feedback_details += Dm.text("Message: []", chosen);
        to_chat(hallucinator, chosen);
        qdel(this);
        return TRUE;
    }
}
