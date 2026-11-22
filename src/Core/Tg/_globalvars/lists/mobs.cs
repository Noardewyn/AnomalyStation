// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/mobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the W4.0 contracts completed it from the wave-3 partial: clients, admins, emote_list and
// init_emote_list; M9 slice B translated it). Deferred leaves (rules 1.4): (the DNA block types since gaps P3)
// (their lists stay null or empty: emotes and DNA blocks are later milestones, which nothing in M2 reads); M2 P2 translated
// init_blood_types, init_species_list and init_species_prototypes (the human's bodyparts and species read them).
// update_config_movespeed_type_lookup waits for its module. Syndicate A2: get_crewmember_minds (the manifest's locked records, M9;
// the objectives' targets).

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/lists/mobs.dm:1  GLOBAL_LIST_EMPTY(clients) //all clients
    public DmList clients;

    // tg: code/_globalvars/lists/mobs.dm:1  (GLOBAL_MANAGED(clients, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalclients()
    {
        clients = Dm.list();
        Dm.append(ref gvars_datum_init_order, "clients");
    }

    // tg: code/_globalvars/lists/mobs.dm:2  GLOBAL_LIST_EMPTY(admins) //all clients whom are admins
    public DmList admins;

    // tg: code/_globalvars/lists/mobs.dm:2  (GLOBAL_MANAGED(admins, list()); the earlier of two definitions on this type, rules 7.5)
    public void InitGlobaladmins__d0()
    {
        admins = Dm.list();
        Dm.append(ref gvars_datum_init_order, "admins");
    }

    // tg: code/_globalvars/lists/mobs.dm:3  GLOBAL_PROTECT(admins) (__DEFINES/__globals.dm:15)
    public virtual void InitGlobaladmins()
    {
        InitGlobaladmins__d0();
        gvars_datum_protected_varlist["admins"] = TRUE;
    }

    // tg: code/_globalvars/lists/mobs.dm:4  GLOBAL_LIST_EMPTY(deadmins) //all ckeys who have used the de-admin verb.
    public DmList deadmins;

    // tg: code/_globalvars/lists/mobs.dm:4  (GLOBAL_MANAGED(deadmins, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldeadmins()
    {
        deadmins = Dm.list();
        Dm.append(ref gvars_datum_init_order, "deadmins");
    }

    // tg: code/_globalvars/lists/mobs.dm:6  GLOBAL_LIST_EMPTY(directory) //all ckeys with associated client
    public DmList directory;

    // tg: code/_globalvars/lists/mobs.dm:6  (GLOBAL_MANAGED(directory, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldirectory()
    {
        directory = Dm.list();
        Dm.append(ref gvars_datum_init_order, "directory");
    }

    // tg: code/_globalvars/lists/mobs.dm:7  GLOBAL_LIST_EMPTY(stealthminID) //reference list with IDs that store ckeys, for stealthmins
    public DmList stealthminID;

    // tg: code/_globalvars/lists/mobs.dm:7  (GLOBAL_MANAGED(stealthminID, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstealthminID()
    {
        stealthminID = Dm.list();
        Dm.append(ref gvars_datum_init_order, "stealthminID");
    }

    /// List of types of abstract mob which shouldn't usually exist in the world on its own if we're spawning random mobs
    // tg: code/_globalvars/lists/mobs.dm:10  GLOBAL_LIST_INIT(abstract_mob_types, list(...))
    public DmList abstract_mob_types;

    // tg: code/_globalvars/lists/mobs.dm:10  (GLOBAL_MANAGED(abstract_mob_types, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalabstract_mob_types()
    {
        abstract_mob_types = Dm.list(
            typeof(mob_living_basic_construct),
            typeof(mob_living_basic_guardian),
            typeof(mob_living_basic_heretic_summon),
            typeof(mob_living_basic_mimic_copy), // Cannot exist if spawned without being passed an item reference
            typeof(mob_living_basic_soulscythe), // This is just a way for players to control the soulscythe item
            typeof(mob_living_carbon_human_consistent),
            typeof(mob_living_carbon_human_dummy),
            typeof(mob_living_carbon_human_dummy_consistent));
        Dm.append(ref gvars_datum_init_order, "abstract_mob_types");
    }

    // tg: code/_globalvars/lists/mobs.dm:25  GLOBAL_LIST_EMPTY(player_list) //all mobs **with clients attached**.
    public DmList player_list;

    // tg: code/_globalvars/lists/mobs.dm:25  (GLOBAL_MANAGED(player_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalplayer_list()
    {
        player_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "player_list");
    }

    // tg: code/_globalvars/lists/mobs.dm:26  GLOBAL_LIST_EMPTY(keyloop_list) //as above but can be limited to boost performance
    public DmList keyloop_list;

    // tg: code/_globalvars/lists/mobs.dm:26  (GLOBAL_MANAGED(keyloop_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalkeyloop_list()
    {
        keyloop_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "keyloop_list");
    }

    // tg: code/_globalvars/lists/mobs.dm:27  GLOBAL_LIST_EMPTY(mob_list) //all mobs, including clientless
    public DmList mob_list;

    // tg: code/_globalvars/lists/mobs.dm:27  (GLOBAL_MANAGED(mob_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmob_list()
    {
        mob_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "mob_list");
    }

    // tg: code/_globalvars/lists/mobs.dm:28  GLOBAL_LIST_EMPTY(alive_mob_list) //all alive mobs, including clientless. Excludes /mob/dead/new_player
    public DmList alive_mob_list;

    // tg: code/_globalvars/lists/mobs.dm:28  (GLOBAL_MANAGED(alive_mob_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalalive_mob_list()
    {
        alive_mob_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "alive_mob_list");
    }

    // tg: code/_globalvars/lists/mobs.dm:29  GLOBAL_LIST_EMPTY(suicided_mob_list) //contains a list of all mobs that suicided, including their associated ghosts.
    public DmList suicided_mob_list;

    // tg: code/_globalvars/lists/mobs.dm:29  (GLOBAL_MANAGED(suicided_mob_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsuicided_mob_list()
    {
        suicided_mob_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "suicided_mob_list");
    }

    // tg: code/_globalvars/lists/mobs.dm:30  GLOBAL_LIST_EMPTY(drones_list)
    public DmList drones_list;

    // tg: code/_globalvars/lists/mobs.dm:30  (GLOBAL_MANAGED(drones_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldrones_list()
    {
        drones_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "drones_list");
    }

    // tg: code/_globalvars/lists/mobs.dm:31  GLOBAL_LIST_EMPTY(dead_mob_list) //all dead mobs, including clientless. Excludes /mob/dead/new_player
    public DmList dead_mob_list;

    // tg: code/_globalvars/lists/mobs.dm:31  (GLOBAL_MANAGED(dead_mob_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldead_mob_list()
    {
        dead_mob_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "dead_mob_list");
    }

    // tg: code/_globalvars/lists/mobs.dm:32  GLOBAL_LIST_EMPTY(joined_player_list) //all ckeys that have joined the game at round-start or as a latejoin.
    public DmList joined_player_list;

    // tg: code/_globalvars/lists/mobs.dm:32  (GLOBAL_MANAGED(joined_player_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaljoined_player_list()
    {
        joined_player_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "joined_player_list");
    }

    // tg: code/_globalvars/lists/mobs.dm:33  GLOBAL_LIST_EMPTY(new_player_list) //all /mob/dead/new_player, in theory all should have clients and those that don't are in the process of spawning and get deleted when done.
    public DmList new_player_list;

    // tg: code/_globalvars/lists/mobs.dm:33  (GLOBAL_MANAGED(new_player_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnew_player_list()
    {
        new_player_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "new_player_list");
    }

    // tg: code/_globalvars/lists/mobs.dm:34  GLOBAL_LIST_EMPTY(pre_setup_antags) //minds that have been picked as antag by the gamemode. removed as antag datums are set.
    public DmList pre_setup_antags;

    // tg: code/_globalvars/lists/mobs.dm:34  (GLOBAL_MANAGED(pre_setup_antags, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpre_setup_antags()
    {
        pre_setup_antags = Dm.list();
        Dm.append(ref gvars_datum_init_order, "pre_setup_antags");
    }

    // tg: code/_globalvars/lists/mobs.dm:35  GLOBAL_LIST_EMPTY(silicon_mobs) //all silicon mobs
    public DmList silicon_mobs;

    // tg: code/_globalvars/lists/mobs.dm:35  (GLOBAL_MANAGED(silicon_mobs, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsilicon_mobs()
    {
        silicon_mobs = Dm.list();
        Dm.append(ref gvars_datum_init_order, "silicon_mobs");
    }

    // tg: code/_globalvars/lists/mobs.dm:36  GLOBAL_LIST_EMPTY(mob_living_list) //all instances of /mob/living and subtypes
    public DmList mob_living_list;

    // tg: code/_globalvars/lists/mobs.dm:36  (GLOBAL_MANAGED(mob_living_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmob_living_list()
    {
        mob_living_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "mob_living_list");
    }

    // tg: code/_globalvars/lists/mobs.dm:37  GLOBAL_LIST_EMPTY(carbon_list) //all instances of /mob/living/carbon and subtypes, notably does not contain brains or simple animals
    public DmList carbon_list;

    // tg: code/_globalvars/lists/mobs.dm:37  (GLOBAL_MANAGED(carbon_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcarbon_list()
    {
        carbon_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "carbon_list");
    }

    // tg: code/_globalvars/lists/mobs.dm:38  GLOBAL_LIST_EMPTY(human_list) //all instances of /mob/living/carbon/human and subtypes
    public DmList human_list;

    // tg: code/_globalvars/lists/mobs.dm:38  (GLOBAL_MANAGED(human_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhuman_list()
    {
        human_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "human_list");
    }

    // tg: code/_globalvars/lists/mobs.dm:39  GLOBAL_LIST_EMPTY(ai_list)
    public DmList ai_list;

    // tg: code/_globalvars/lists/mobs.dm:39  (GLOBAL_MANAGED(ai_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalai_list()
    {
        ai_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "ai_list");
    }

    // tg: code/_globalvars/lists/mobs.dm:40  GLOBAL_LIST_EMPTY(available_ai_shells)
    public DmList available_ai_shells;

    // tg: code/_globalvars/lists/mobs.dm:40  (GLOBAL_MANAGED(available_ai_shells, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalavailable_ai_shells()
    {
        available_ai_shells = Dm.list();
        Dm.append(ref gvars_datum_init_order, "available_ai_shells");
    }

    // tg: code/_globalvars/lists/mobs.dm:41  GLOBAL_LIST_INIT(simple_animals, list(list(),list(),list())) // One for each AI_* status define
    public DmList simple_animals;

    // tg: code/_globalvars/lists/mobs.dm:41  (GLOBAL_MANAGED(simple_animals, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsimple_animals()
    {
        simple_animals = Dm.list(Dm.list(), Dm.list(), Dm.list());
        Dm.append(ref gvars_datum_init_order, "simple_animals");
    }

    // tg: code/_globalvars/lists/mobs.dm:42  GLOBAL_LIST_EMPTY(spidermobs) //all sentient spider mobs
    public DmList spidermobs;

    // tg: code/_globalvars/lists/mobs.dm:42  (GLOBAL_MANAGED(spidermobs, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalspidermobs()
    {
        spidermobs = Dm.list();
        Dm.append(ref gvars_datum_init_order, "spidermobs");
    }

    // tg: code/_globalvars/lists/mobs.dm:43  GLOBAL_LIST_EMPTY(bots_list)
    public DmList bots_list;

    // tg: code/_globalvars/lists/mobs.dm:43  (GLOBAL_MANAGED(bots_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbots_list()
    {
        bots_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "bots_list");
    }

    // tg: code/_globalvars/lists/mobs.dm:44  GLOBAL_LIST_EMPTY(camera_eyes)
    public DmList camera_eyes;

    // tg: code/_globalvars/lists/mobs.dm:44  (GLOBAL_MANAGED(camera_eyes, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcamera_eyes()
    {
        camera_eyes = Dm.list();
        Dm.append(ref gvars_datum_init_order, "camera_eyes");
    }

    // tg: code/_globalvars/lists/mobs.dm:45  GLOBAL_LIST_EMPTY(suit_sensors_list) //all people with suit sensors on
    public DmList suit_sensors_list;

    // tg: code/_globalvars/lists/mobs.dm:45  (GLOBAL_MANAGED(suit_sensors_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsuit_sensors_list()
    {
        suit_sensors_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "suit_sensors_list");
    }

    /// All alive mobs with clients.
    // tg: code/_globalvars/lists/mobs.dm:48  GLOBAL_LIST_EMPTY(alive_player_list)
    public DmList alive_player_list;

    // tg: code/_globalvars/lists/mobs.dm:48  (GLOBAL_MANAGED(alive_player_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalalive_player_list()
    {
        alive_player_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "alive_player_list");
    }

    /// All dead mobs with clients. Does not include observers.
    // tg: code/_globalvars/lists/mobs.dm:51  GLOBAL_LIST_EMPTY(dead_player_list)
    public DmList dead_player_list;

    // tg: code/_globalvars/lists/mobs.dm:51  (GLOBAL_MANAGED(dead_player_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldead_player_list()
    {
        dead_player_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "dead_player_list");
    }

    /// All alive antags with clients.
    // tg: code/_globalvars/lists/mobs.dm:54  GLOBAL_LIST_EMPTY(current_living_antags)
    public DmList current_living_antags;

    // tg: code/_globalvars/lists/mobs.dm:54  (GLOBAL_MANAGED(current_living_antags, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcurrent_living_antags()
    {
        current_living_antags = Dm.list();
        Dm.append(ref gvars_datum_init_order, "current_living_antags");
    }

    /// All observers with clients that joined as observers.
    // tg: code/_globalvars/lists/mobs.dm:57  GLOBAL_LIST_EMPTY(current_observers_list)
    public DmList current_observers_list;

    // tg: code/_globalvars/lists/mobs.dm:57  (GLOBAL_MANAGED(current_observers_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcurrent_observers_list()
    {
        current_observers_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "current_observers_list");
    }

    /// All living mobs which can hear blob telepathy
    // tg: code/_globalvars/lists/mobs.dm:60  GLOBAL_LIST_EMPTY(blob_telepathy_mobs)
    public DmList blob_telepathy_mobs;

    // tg: code/_globalvars/lists/mobs.dm:60  (GLOBAL_MANAGED(blob_telepathy_mobs, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalblob_telepathy_mobs()
    {
        blob_telepathy_mobs = Dm.list();
        Dm.append(ref gvars_datum_init_order, "blob_telepathy_mobs");
    }

    /// All "living" (because revenants are in between mortal planes or whatever) mobs that can hear revenants
    // tg: code/_globalvars/lists/mobs.dm:63  GLOBAL_LIST_EMPTY(revenant_relay_mobs)
    public DmList revenant_relay_mobs;

    // tg: code/_globalvars/lists/mobs.dm:63  (GLOBAL_MANAGED(revenant_relay_mobs, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalrevenant_relay_mobs()
    {
        revenant_relay_mobs = Dm.list();
        Dm.append(ref gvars_datum_init_order, "revenant_relay_mobs");
    }

    ///underages who have been reported to security for trying to buy things they shouldn't, so they can't spam
    // tg: code/_globalvars/lists/mobs.dm:66  GLOBAL_LIST_EMPTY(narcd_underages)
    public DmList narcd_underages;

    // tg: code/_globalvars/lists/mobs.dm:66  (GLOBAL_MANAGED(narcd_underages, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnarcd_underages()
    {
        narcd_underages = Dm.list();
        Dm.append(ref gvars_datum_init_order, "narcd_underages");
    }

    /// A list of all the possible blood types, keyed by id (which is just the name in most cases)
    // tg: code/_globalvars/lists/mobs.dm:69  GLOBAL_LIST_INIT(blood_types, init_blood_types())
    public DmList blood_types;

    // tg: code/_globalvars/lists/mobs.dm:69  (GLOBAL_MANAGED(blood_types, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalblood_types()
    {
        blood_types = init_blood_types() as DmList;
        Dm.append(ref gvars_datum_init_order, "blood_types");
    }

    /// An assoc list of species IDs to type paths
    // tg: code/_globalvars/lists/mobs.dm:79  GLOBAL_LIST_INIT(species_list, init_species_list())
    public DmList species_list;

    // tg: code/_globalvars/lists/mobs.dm:79  (GLOBAL_MANAGED(species_list, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalspecies_list()
    {
        species_list = init_species_list() as DmList;
        Dm.append(ref gvars_datum_init_order, "species_list");
    }

    /// List of all species prototypes to reference, assoc [type] = prototype
    // tg: code/_globalvars/lists/mobs.dm:81  GLOBAL_LIST_INIT_TYPED(species_prototypes, /datum/species, init_species_prototypes())
    public DmList species_prototypes;

    // tg: code/_globalvars/lists/mobs.dm:81  (GLOBAL_MANAGED(species_prototypes, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalspecies_prototypes()
    {
        species_prototypes = init_species_prototypes() as DmList;
        Dm.append(ref gvars_datum_init_order, "species_prototypes");
    }

    // tg: code/_globalvars/lists/mobs.dm:95  GLOBAL_LIST_EMPTY(latejoin_ai_cores)
    public DmList latejoin_ai_cores;

    // tg: code/_globalvars/lists/mobs.dm:95  (GLOBAL_MANAGED(latejoin_ai_cores, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGloballatejoin_ai_cores()
    {
        latejoin_ai_cores = Dm.list();
        Dm.append(ref gvars_datum_init_order, "latejoin_ai_cores");
    }

    // tg: code/_globalvars/lists/mobs.dm:97  GLOBAL_LIST_EMPTY(mob_config_movespeed_type_lookup)
    public DmList mob_config_movespeed_type_lookup;

    // tg: code/_globalvars/lists/mobs.dm:97  (GLOBAL_MANAGED(mob_config_movespeed_type_lookup, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmob_config_movespeed_type_lookup()
    {
        mob_config_movespeed_type_lookup = Dm.list();
        Dm.append(ref gvars_datum_init_order, "mob_config_movespeed_type_lookup");
    }

    // tg: code/_globalvars/lists/mobs.dm:99  GLOBAL_LIST_EMPTY(emote_list)
    public DmList emote_list;

    // tg: code/_globalvars/lists/mobs.dm:99  (GLOBAL_MANAGED(emote_list, list()))
    public virtual void InitGlobalemote_list()
    {
        emote_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "emote_list");
    }
    // tg: code/_globalvars/lists/mobs.dm:101  GLOBAL_LIST_INIT(construct_radial_images, list(...))
    public DmList construct_radial_images;

    // tg: code/_globalvars/lists/mobs.dm:101  (GLOBAL_MANAGED(construct_radial_images, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalconstruct_radial_images()
    {
        construct_radial_images = Dm.list(
            Dm.kv(CONSTRUCT_JUGGERNAUT, image(icon: Dm.resource("icons/mob/nonhuman-player/cult.dmi"), icon_state: "juggernaut")),
            Dm.kv(CONSTRUCT_WRAITH, image(icon: Dm.resource("icons/mob/nonhuman-player/cult.dmi"), icon_state: "wraith")),
            Dm.kv(CONSTRUCT_ARTIFICER, image(icon: Dm.resource("icons/mob/nonhuman-player/cult.dmi"), icon_state: "artificer")));
        Dm.append(ref gvars_datum_init_order, "construct_radial_images");
    }

    /// A keyed list of identity block singletons, in a key:value group of typepath:block
    // tg: code/_globalvars/lists/mobs.dm:152  GLOBAL_LIST_INIT(dna_identity_blocks, init_identity_block_types())
    public DmList dna_identity_blocks;

    // tg: code/_globalvars/lists/mobs.dm:152  (GLOBAL_MANAGED(dna_identity_blocks, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldna_identity_blocks()
    {
        dna_identity_blocks = init_identity_block_types() as DmList;
        Dm.append(ref gvars_datum_init_order, "dna_identity_blocks");
    }

    /// A keyed list of feature block singletons, in a key:value group of typepath:block
    // tg: code/_globalvars/lists/mobs.dm:161  GLOBAL_LIST_INIT(dna_feature_blocks, init_feature_block_types())
    public DmList dna_feature_blocks;

    // tg: code/_globalvars/lists/mobs.dm:161  (GLOBAL_MANAGED(dna_feature_blocks, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldna_feature_blocks()
    {
        dna_feature_blocks = init_feature_block_types() as DmList;
        Dm.append(ref gvars_datum_init_order, "dna_feature_blocks");
    }
}

public static partial class GlobalProcs
{
    /// Initializes the list of blood type singletons
    // tg: code/_globalvars/lists/mobs.dm:72
    public static object init_blood_types()
    {
        // M2 P2: translated (the bodyparts' Initialize reads GLOB.blood_types for their blood DNA, _bodyparts.dm:275).
        object dot = null;
        dot = Dm.list();
        // GAP(M3 P1 -> lead): generated-only types skipped until translated (DV25; types-converter.md 8).
        foreach (object blood_type_path__it in Dm.each(typesof_written(valid_subtypesof(typeof(datum_blood_type)))))
        {
            Type blood_type_path = blood_type_path__it as Type;
            datum_blood_type new_type = (datum_blood_type)Dm.@new(blood_type_path);
            ((DmList)dot)[new_type.id] = new_type;
        }
        return dot;
    }

    // tg: code/_globalvars/lists/mobs.dm:83
    public static object init_species_list()
    {
        // M2 P2: translated (species, scope.md 2e "human, species, dna"; only /datum/species/human is declared, Q7).
        DmList species_list = Dm.list();
        foreach (object species_path__it in Dm.each(subtypesof(typeof(datum_species))))
        {
            Type species_path = species_path__it as Type;
            species_list[Dm.initial(species_path, (datum_species p) => p.id)] = species_path;
        }
        return species_list;
    }

    // tg: code/_globalvars/lists/mobs.dm:89
    public static object init_species_prototypes()
    {
        // M2 P2: translated; GLOB.species_prototypes is narrowed to the human by the declared species types (scope.md 2e, Q7:
        // the other 21 species bodies are left out, so subtypesof(/datum/species) is /datum/species/human alone).
        DmList species_list = Dm.list();
        // GAP(M3 P1 -> lead): generated-only types skipped until translated (DV25; types-converter.md 8).
        foreach (object species_type in Dm.each(subtypesof_written(typeof(datum_species))))
            species_list[species_type] = Dm.@new(species_type as Type);
        return species_list;
    }

    // tg: code/_globalvars/lists/mobs.dm:107
    public static async DmTask update_config_movespeed_type_lookup(float? update_mobs = null)
    {
        update_mobs ??= TRUE;
        Dm.NotTranslated("code/_globalvars/lists/mobs.dm:107", "update_config_movespeed_type_lookup: the multiplicative_movespeed config entry (game_options) is translated with the configuration of M1");
    }

    // tg: code/_globalvars/lists/mobs.dm:120
    public static async DmTask update_mob_config_movespeeds()
    {
        foreach (object i in Dm.each(GLOB.mob_list))
        {
            mob M = i as mob;
            await M.update_config_movespeed();
        }
    }

    // tg: code/_globalvars/lists/mobs.dm:125
    public static object init_emote_list()
    {
        DmList dot = Dm.list();
        foreach (Type path in Dm.each<Type>(valid_subtypesof(typeof(datum_emote))))
        {
            // GAP(M9 slice B -> the mobs that own them): the emotes of mobs the port does not have yet (silicons, aliens, slimes, the
            // animals, the brain, the imaginary friend) are inert and are not created, so their keys are absent (DV25).
            if (InertSeam.IsInertType(path))
                continue;
            var E = Dm.@new(path) as datum_emote;
            if (Dm.truthy(E.key))
            {
                if (!Dm.truthy(dot[E.key]))
                    dot[E.key] = Dm.list(E);
                else
                    Dm.append_at(dot, E.key, E);
            }
            else if (Dm.truthy(E.message)) //Assuming all non-base emotes have this
                stack_trace(Dm.text("Keyless emote: []", E.type), "code/_globalvars/lists/mobs.dm", 135);

            if (Dm.truthy(E.key_third_person)) //This one is optional
            {
                if (!Dm.truthy(dot[E.key_third_person]))
                    dot[E.key_third_person] = Dm.list(E);
                else
                    Dm.combine_at(dot, E.key_third_person, E);
            }
        }
        return dot;
    }

    // tg: code/_globalvars/lists/mobs.dm:143
    public static object get_crewmember_minds()
    {
        DmList minds = Dm.list();
        foreach (datum_record_locked target in Dm.each<datum_record_locked>(GLOB.manifest.locked))
        {
            datum_mind mind = target.mind_ref?.resolve() as datum_mind;
            if (mind != null)
                Dm.append(minds, mind);
        }
        return minds;
    }

    // tg: code/_globalvars/lists/mobs.dm:154
    public static object init_identity_block_types()
    {
        DmList dot = Dm.list();
        foreach (Type block_path in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_dna_block_identity))))
        {
            datum_dna_block_identity new_block = Dm.@new(block_path) as datum_dna_block_identity;
            dot[block_path] = new_block;
        }
        return dot;
    }

    // tg: code/_globalvars/lists/mobs.dm:163
    public static object init_feature_block_types()
    {
        DmList dot = Dm.list();
        foreach (Type block_path in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_dna_block_feature))))
        {
            datum_dna_block_feature new_block = Dm.@new(block_path) as datum_dna_block_feature;
            dot[block_path] = new_block;
        }
        return dot;
    }
}
