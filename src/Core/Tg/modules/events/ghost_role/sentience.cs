// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/ghost_role/sentience.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4 (D327): the whole file: a ghost who volunteers becomes a mindless animal or bot on the station (the pets and farm animals
// first), able to speak and understand the crew's languages; the admins' station-wide version wakes as many as there are ghosts. The var blocks
// are generated (D111).

public partial class datum_controller_global_vars
{
    // tg: code/modules/events/ghost_role/sentience.dm:1
    public DmList high_priority_sentience;
    public virtual void InitGlobalhigh_priority_sentience()
    {
        high_priority_sentience = typecacheof(Dm.list(
            typeof(mob_living_basic_bat),
            typeof(mob_living_basic_butterfly),
            typeof(mob_living_basic_carp_pet_cayenne),
            typeof(mob_living_basic_chicken),
            typeof(mob_living_basic_crab),
            typeof(mob_living_basic_cow),
            typeof(mob_living_basic_goat),
            typeof(mob_living_basic_goose_vomit),
            typeof(mob_living_basic_lizard),
            typeof(mob_living_basic_mouse_brown_tom),
            typeof(mob_living_basic_parrot),
            typeof(mob_living_basic_pet),
            typeof(mob_living_basic_pig),
            typeof(mob_living_basic_rabbit),
            typeof(mob_living_basic_sheep),
            typeof(mob_living_basic_sloth),
            typeof(mob_living_basic_snake),
            typeof(mob_living_basic_spider_giant_sgt_araneus),
            typeof(mob_living_basic_bot_secbot_beepsky),
            typeof(mob_living_basic_bear_snow_misha),
            typeof(mob_living_basic_mining_lobstrosity_juvenile)
        )) as DmList;
        Dm.append(ref gvars_datum_init_order, "high_priority_sentience");
    }
}

// tg: code/modules/events/ghost_role/sentience.dm:34
public partial class datum_round_event_ghost_role_sentience : datum_round_event_ghost_role
{
    // tg: code/modules/events/ghost_role/sentience.dm:42
    public override async DmTask announce__proc(object fake = null)
    {
        string sentience_report = "";

        object data = pick("scans from our long-range sensors", "our sophisticated probabilistic models", "our omnipotence", "the communications traffic on your station", "energy emissions we detected", "[REDACTED]");
        object pets = pick("animals/bots", "bots/animals", "pets", "simple animals", "lesser lifeforms", "[REDACTED]");
        object strength = pick("human", "moderate", "lizard", "security", "command", "clown", "low", "very low", "[REDACTED]");

        sentience_report += Dm.text("Based on [], we believe that [] of the station's [] has developed [] level intelligence, and the ability to communicate.", data, one, pets, strength);

        priority_announce(sentience_report, Dm.text("[] Medium-Priority Update", command_name()));
    }

    // tg: code/modules/events/ghost_role/sentience.dm:53
    public override async DmTask<object> spawn_role()
    {
        var candidates = await SSpolling.poll_ghost_candidates(check_jobban: ROLE_SENTIENCE, role: ROLE_SENTIENCE, alert_pic: typeof(obj_item_slimepotion_sentience), role_name_text: role_name) as DmList;
        if (Dm.length(candidates) == 0)
            return NOT_ENOUGH_PLAYERS;

        // find our chosen mob to breathe life into
        // Mobs have to be simple animals, mindless, on station, and NOT holograms.
        // prioritize starter animals that people will recognise

        var hi_pri = Dm.list();
        var low_pri = Dm.list();

        foreach (mob_living_simple_animal check_mob in Dm.each<mob_living_simple_animal>(GLOB.alive_mob_list))
            set_mob_priority(check_mob, hi_pri, low_pri);
        foreach (mob_living_basic check_mob in Dm.each<mob_living_basic>(GLOB.alive_mob_list))
            set_mob_priority(check_mob, hi_pri, low_pri);

        shuffle_inplace(hi_pri);
        shuffle_inplace(low_pri);

        DmList potential = Dm.bitor(hi_pri, low_pri) as DmList; // hi_pri + low_pri (two disjoint lists: the sum and the union are the same)

        if (potential.len == 0)
            return WAITING_FOR_SOMETHING;
        if (candidates.len == 0)
            return NOT_ENOUGH_PLAYERS;

        float spawned_animals = 0;
        while (spawned_animals < animals && candidates.len > 0 && potential.len > 0)
        {
            var selected = (mob_living)popleft(potential);
            var picked_candidate = (mob_dead_observer)pick_n_take(candidates);

            spawned_animals++;

            selected.PossessByPlayer(picked_candidate.key);

            selected.grant_all_languages(UNDERSTOOD_LANGUAGE, grant_omnitongue: FALSE, source: LANGUAGE_ATOM);

            if (isanimal(selected))
            {
                var animal_selected = (mob_living_simple_animal)selected;
                animal_selected.sentience_act();
                animal_selected.del_on_death = FALSE;
            }
            else if (isbasicmob(selected))
            {
                var animal_selected = (mob_living_basic)selected;
                animal_selected.basic_mob_flags = Dm.bitand(animal_selected.basic_mob_flags, Dm.bitnot(DEL_ON_DEATH));
            }

            selected.maxHealth = max(selected.maxHealth, 200);
            selected.health = selected.maxHealth;
            Dm.append(spawned_mobs, selected);

            to_chat(selected, span_userdanger(I18n.T("Hello world!")));
            to_chat(selected, span_warning(I18n.T("Due to freak radiation and/or chemicals and/or lucky chance, you have gained human level intelligence and the ability to speak and understand human language!")));
        }

        return SUCCESSFUL_SPAWN;
    }

    /// Adds a mob to either the high or low priority event list
    // tg: code/modules/events/ghost_role/sentience.dm:105
    public virtual void set_mob_priority(mob_living checked_mob = null, DmList high = null, DmList low = null)
    {
        turf mob_turf = get_turf(checked_mob);
        if (mob_turf == null || !Dm.truthy(is_station_level(mob_turf.z)))
            return;
        if (Dm.@in(checked_mob, GLOB.player_list) || checked_mob.mind != null || Dm.truthy(Dm.bitand(checked_mob.flags_1, HOLOGRAM_1)))
            return;
        if (Dm.truthy(is_type_in_typecache(checked_mob, GLOB.high_priority_sentience)))
            Dm.append(high, checked_mob);
        else
            Dm.append(low, checked_mob);
    }
}
