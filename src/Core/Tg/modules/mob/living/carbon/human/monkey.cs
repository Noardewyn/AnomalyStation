// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/monkey.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C5); the var overrides are generated.

// tg: code/modules/mob/living/carbon/human/monkey.dm:2
public partial class mob_living_carbon_human_species_monkey : mob_living_carbon_human_species
{
    // tg: code/modules/mob/living/carbon/human/monkey.dm:6
    [DmParams("mapload", "species", "cubespawned")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object cubespawned = Dm.arg(__extra, 1);
        ADD_TRAIT(this, TRAIT_BORN_MONKEY, INNATE_TRAIT);
        if (Dm.truthy(cubespawned))
            Dm.append(ref datum_controller_subsystem_mobs.cubemonkeys, this);
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/mob/living/carbon/human/monkey.dm:12
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref datum_controller_subsystem_mobs.cubemonkeys, this);
        return base.Destroy(force);
    }
}

// tg: code/modules/mob/living/carbon/human/monkey.dm:17
public partial class mob_living_carbon_human_species_monkey_angry : mob_living_carbon_human_species_monkey
{
    // tg: code/modules/mob/living/carbon/human/monkey.dm:19
    [DmParams("mapload", "species", "cubespawned")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(10))
            Waiting.Detach(give_ape_escape_helmet());
        return dot;
    }

    /// Gives our funny monkey an Ape Escape hat reference
    // tg: code/modules/mob/living/carbon/human/monkey.dm:25
    public virtual async DmTask give_ape_escape_helmet()
    {
        obj_item_clothing_head_helmet_toggleable_justice_escape helmet = Dm.@new<obj_item_clothing_head_helmet_toggleable_justice_escape>(this);
        await equip_to_slot_or_del(helmet, ITEM_SLOT_HEAD);
        await helmet.attack_self(this); // todo encapsulate toggle
    }
}

/// The one true Pun Pun. Either the bar's monkey mob, or the bar gorilla when the Big Pun Pun trait is active.
// tg: code/modules/mob/living/carbon/human/monkey.dm:32
public partial class datum_controller_global_vars : datum_controller
{
    public mob_living the_one_and_only_punpun;
}

// tg: code/modules/mob/living/carbon/human/monkey.dm:34
public partial class mob_living_carbon_human_species_monkey_punpun : mob_living_carbon_human_species_monkey
{
    // tg: code/modules/mob/living/carbon/human/monkey.dm:46
    [DmParams("mapload", "species")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        // REGISTER_REQUIRED_MAP_ITEM(1, 1): the unit tests' map item check (UNIT_TESTS)
        if (Dm.truthy(mapload) && Dm.locate<datum_station_trait_job_pun_pun>(SSstation.station_traits) != null)
        {
            Dm.@new<obj_effect_landmark_start_pun_pun>(loc); //Pun Pun is a crewmember, and may late-join.
            return INITIALIZE_HINT_QDEL;
        }

        if (Dm.truthy(mapload) && HAS_TRAIT(SSstation, STATION_TRAIT_PUN_PUN_GYM_DAY))
        {
            Dm.@new<mob_living_basic_gorilla_bar>(loc); //gym day get buff get swole
            return INITIALIZE_HINT_QDEL;
        }

        Waiting.NoSleep(equip_to_slot_or_del(Dm.@new<obj_item_clothing_under_suit_waiter>(this), ITEM_SLOT_ICLOTHING));

        if (GLOB.the_one_and_only_punpun == null && Dm.truthy(mapload))
        {
            GLOB.the_one_and_only_punpun = this;
            Read_Memory();
        }
        else if (GLOB.the_one_and_only_punpun != null)
            ADD_TRAIT(this, TRAIT_DONT_WRITE_MEMORY, INNATE_TRAIT); //faaaaaaake!

        // Everything past here MUST be called AFTER memory has been read
        give_special_name();
        give_scars();
        give_special_equipment();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/monkey.dm:72
    public override object Destroy(float? force = null)
    {
        if (GLOB.the_one_and_only_punpun == this)
            GLOB.the_one_and_only_punpun = null;

        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/carbon/human/monkey.dm:78
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        if (!IS_UNCONSCIOUS_OR_CRIT(this) && Dm.eq(SSticker.current_state, GAME_STATE_FINISHED) && !Dm.truthy(memory_saved))
        {
            Write_Memory(FALSE, FALSE);
            memory_saved = TRUE;
        }

        return base.Life(seconds_per_tick, __extra);
    }

    // tg: code/modules/mob/living/carbon/human/monkey.dm:85
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (!Dm.truthy(memory_saved))
            Write_Memory(TRUE, gibbed);

        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/carbon/human/monkey.dm:91
    public virtual void Read_Memory()
    {
        // the legacy savefile branch (data/npc_saves/Punpun.sav, tg's old format) has no port: no such file exists here
        DmFileRef json_file = Dm.file("data/npc_saves/Punpun.json");
        if (!Dm.truthy(Dm.fexists(json_file)))
            return;
        DmList json = Dm.json_decode(Dm.file2text(json_file)) as DmList;
        ancestor_name = json["ancestor_name"];
        ancestor_chain = json["ancestor_chain"] == null ? 0 : Dm.num(json["ancestor_chain"]);
        relic_hat = json["relic_hat"] is string hat_path ? Dm.text2path(hat_path) : json["relic_hat"];
        relic_mask = json["relic_hat"] is string mask_path ? Dm.text2path(mask_path) : json["relic_hat"]; // tg's own: the mask reads the hat's key
    }

    // tg: code/modules/mob/living/carbon/human/monkey.dm:109
    public override object Write_Memory(object dead = null, object gibbed = null)
    {
        object dot = base.Write_Memory(dead, gibbed);
        if (!Dm.truthy(dot))
            return dot;
        DmFileRef json_file = Dm.file("data/npc_saves/Punpun.json");
        DmList file_data = Dm.list();
        if (Dm.truthy(gibbed))
        {
            file_data["ancestor_name"] = null;
            file_data["ancestor_chain"] = null;
            file_data["relic_hat"] = null;
            file_data["relic_mask"] = null;
        }
        else
        {
            file_data["ancestor_name"] = Dm.truthy(ancestor_name) ? ancestor_name : real_name;
            file_data["ancestor_chain"] = Dm.truthy(dead) ? ancestor_chain + 1 : ancestor_chain;
            file_data["relic_hat"] = head != null ? ((datum)head).type : null;
            file_data["relic_mask"] = wear_mask != null ? ((datum)wear_mask).type : null;
        }
        Dm.fdel(json_file);
        Dm.output_to(json_file, Dm.json_encode(file_data));
        return dot;
    }

    /// Gives pun pun a special name based on various factors such as their past
    // tg: code/modules/mob/living/carbon/human/monkey.dm:130
    public virtual void give_special_name()
    {
        object name_to_use = Dm.initial(typeof(mob_living_carbon_human_species_monkey_punpun), "name");

        if (Dm.truthy(ancestor_name))
        {
            name_to_use = ancestor_name;
            if (ancestor_chain > 1)
                name_to_use = Dm.text("[] []", name_to_use, Roman((int)ancestor_chain));
        }
        else if (prob(10))
        {
            name_to_use = pick(Dm.list("Professor Bobo", "Deempisi's Revenge", "Furious George", "King Louie", "Dr. Zaius", "Jimmy Rustles", "Dinner", "Lanky"));
            if (Dm.eq(name_to_use, "Furious George"))
            {
                qdel(ai_controller as datum);
                ai_controller = Dm.@new<datum_ai_controller_monkey_angry>(this); //hes always mad
            }
        }

        Waiting.NoSleep(fully_replace_character_name(real_name, name_to_use));
    }

    /// Gives pun pun scars based on how many times he's died in the past
    // tg: code/modules/mob/living/carbon/human/monkey.dm:149
    public virtual void give_scars()
    {
        if (ancestor_chain > 1)
            Waiting.NoSleep(generate_fake_scars(rand(ancestor_chain, ancestor_chain * 4)));
    }

    /// Gives pun pun special equipment from their past
    // tg: code/modules/mob/living/carbon/human/monkey.dm:154
    public virtual void give_special_equipment()
    {
        if (Dm.truthy(relic_hat))
            Waiting.NoSleep(equip_to_slot_or_del(Dm.@new((Type)relic_hat) as obj_item, ITEM_SLOT_HEAD));
        if (Dm.truthy(relic_mask))
            Waiting.NoSleep(equip_to_slot_or_del(Dm.@new((Type)relic_mask) as obj_item, ITEM_SLOT_MASK));
    }
}
