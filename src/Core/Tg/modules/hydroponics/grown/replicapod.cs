// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/replicapod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the replica pod's procs (the blood sample, the analyzer line, harvesting its seeds). The pod person itself waits for
// the pod species (/datum/species/pod is inert: a Dm.Deferred leaf, not-translated.md section 119). `blood_type` is hand-declared
// (rule V3 types it Type; tg stores the sample's blood type datum in it).

///The actual replica pods themselves!
// tg: code/modules/hydroponics/grown/replicapod.dm:33
public partial class obj_item_seeds_replicapod : obj_item_seeds
{
    // tg: code/modules/hydroponics/grown/replicapod.dm:53  (hand-declared: generated as a path, rule V3)
    public object blood_type;

    // tg: code/modules/hydroponics/grown/replicapod.dm:34  (the generated constructor, kept with the hand-declared var: types-converter.md 3.2)
    public obj_item_seeds_replicapod()
    {
        name = "replica pod seed pack";
        desc = "These seeds grow into replica pods. They say these are used to harvest humans.";
        icon_state = "seed-replicapod";
        plant_icon_offset = 2;
        species = "replicapod";
        plantname = "Replica Pod";
        product = null;
        lifespan = 50;
        endurance = 8;
        maturation = 10;
        production = 1;
        yield = 1;
        instability = 15;
        potency = 30;
    }

    // tg: code/modules/hydroponics/grown/replicapod.dm:60
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(volume, Dm.bitor(INJECTABLE, DRAWABLE));
        return dot;
    }

    // tg: code/modules/hydroponics/grown/replicapod.dm:64
    public override void create_reagents(object max_vol = null, object flags = null)
    {
        base.create_reagents(max_vol, flags);
        RegisterSignal(reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(on_reagent_update));
    }

    /// Handles reagents getting added to this seed.
    // tg: code/modules/hydroponics/grown/replicapod.dm:69
    [SignalHandler]
    public virtual void on_reagent_update(datum_reagents reagents = null)
    {
        datum_reagent_blood blood = reagents.has_reagent(typeof(datum_reagent_blood)) as datum_reagent_blood;
        if (blood == null)
            return;

        DmList data = blood.data as DmList;
        if (!Dm.truthy(data?["mind"]) || !Dm.truthy(data?["cloneable"]))
        {
            visible_message(span_warning(I18n.T("The [] rejects the sample!", this)));
            return;
        }

        mind = data["mind"] as datum_mind;
        ckey = data["ckey"];
        realName = data["real_name"];
        blood_gender = data["gender"];
        blood_type = data[BLOOD_DATA_TYPE];
        features = data["features"] as DmList;
        factions = data["factions"];
        quirks = data["quirks"] as DmList;
        sampleDNA = data[BLOOD_DATA_DNA];
        contains_sample = TRUE;
        visible_message(span_notice(I18n.T("The [] is injected with a fresh blood sample.", this)));
        investigate_log(Dm.text("[]'s cloning record was added to []", key_name(mind), this), INVESTIGATE_BOTANY);
    }

    /// Handles reagents being deleted from these seeds.
    // tg: code/modules/hydroponics/grown/replicapod.dm:94
    [SignalHandler]
    public virtual void on_reagent_del(object changetype = null)
    {
        if (Dm.truthy(((datum_reagents)reagents).has_reagent(typeof(datum_reagent_blood))))
            return;

        mind = null;
        ckey = null;
        realName = null;
        blood_gender = null;
        blood_type = null;
        features = null;
        factions = null;
        sampleDNA = null;
        contains_sample = FALSE;
    }

    // tg: code/modules/hydroponics/grown/replicapod.dm:109
    public override object get_unique_analyzer_data()
    {
        if (Dm.truthy(contains_sample))
            return Dm.list(Dm.kv("Blood DNA", sampleDNA));
        return null;
    }

    // tg: code/modules/hydroponics/grown/replicapod.dm:114
    public override async DmTask<object> harvest(mob user = null) //now that one is fun -- Urist
    {
        obj_machinery_hydroponics parent = loc as obj_machinery_hydroponics;
        bool make_podman = false;
        object ckey_holder = null;
        DmList result = Dm.list();
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_revival_pod_plants))))
        {
            if (Dm.truthy(ckey))
            {
                foreach (mob corpse in Dm.each_cast<mob>(GLOB.player_list))
                {
                    if (!Dm.eq(corpse.ckey, ckey) || HAS_TRAIT(corpse, TRAIT_SUICIDED))
                        continue;

                    if (isobserver(corpse))
                    {
                        mob_dead_observer ghost = corpse as mob_dead_observer;
                        if (Dm.truthy(ghost.can_reenter_corpse))
                            make_podman = true;
                    }
                    else if (Dm.eq(corpse.stat, DEAD) && !HAS_TRAIT(corpse, TRAIT_MIND_TEMPORARILY_GONE))
                    {
                        make_podman = true;
                    }

                    break;
                }
            }
            else if (mind != null)
            {
                // If the player has ghosted from his corpse before blood was drawn, his ckey is no longer attached to the mob, so we need to match up the cloned player through the mind key
                foreach (mob corpse in Dm.each<mob>(GLOB.player_list))
                {
                    if (corpse.mind == null || !Dm.truthy(corpse.ckey) || corpse.client == null)
                        continue;

                    if (!Dm.eq(ckey(corpse.mind.key), ckey(mind.key)))
                        continue;

                    if (!Dm.eq(corpse.stat, DEAD) || HAS_TRAIT(corpse, TRAIT_SUICIDED))
                        continue;

                    if (isobserver(corpse))
                    {
                        mob_dead_observer ghost = corpse as mob_dead_observer;
                        if (!Dm.truthy(ghost.can_reenter_corpse))
                            break;
                    }

                    make_podman = true;
                    ckey_holder = corpse.ckey;
                    break;
                }
            }
        }

        // No podman player, give one or two seeds.
        if (!make_podman)
        {
            // Prevent accidental harvesting. Make sure the user REALLY wants to do this if there's a chance of this coming from a living creature.
            if (user.client != null && (mind != null || Dm.truthy(ckey)))
            {
                object choice = await tgui_alert(user, "The pod is currently devoid of soul. There is a possibility that a soul could claim this creature, or you could harvest it for seeds.", "Harvest Seeds?", Dm.list("Harvest Seeds", "Cancel"));
                if (!Dm.eq(choice, "Harvest Seeds"))
                    return result;
            }

            // If this plant has already been harvested, return early.
            // parent.update_tray() qdels this seed.
            if (QDELETED(this))
            {
                to_chat(user, text: "This pod has already had its seeds harvested!", type: MESSAGE_TYPE_INFO);
                return result;
            }

            // Make sure they can still interact with the parent hydroponics tray.
            if (!Dm.truthy(await user.can_perform_action(parent)))
            {
                to_chat(user, text: Dm.text("You are no longer able to harvest the seeds from []!", parent), type: MESSAGE_TYPE_INFO);
                return result;
            }

            float seed_count = 1;
            if (prob(Dm.num(getYield()) * 20))
                seed_count++;
            atom output_loc = (Dm.truthy(await parent.Adjacent(user)) ? user.loc : parent.loc) as atom; //needed for TK
            for (float i = 1; i <= seed_count; i++)
            {
                obj_item_seeds_replicapod harvestseeds = Copy() as obj_item_seeds_replicapod;
                result.Add(harvestseeds);
                await harvestseeds.forceMove(output_loc);
            }
            await parent.update_tray(user, seed_count);
            return result;
        }

        // Congratulations! %Do you want to build a pod man?%
        // new /mob/living/carbon/human(parent.loc), mind.transfer_to, hardset_dna(... new /datum/species/pod ...), the new blood type:
        // the pod person waits for the pod species (inert)
        Dm.Deferred("code/modules/hydroponics/grown/replicapod.dm:179", "harvest: the pod person (/datum/species/pod and the random chemical blood type are inert)");
        return result;
    }
}
