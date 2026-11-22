// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/chaplain/chaplain.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the chaplain's spawn: the religion from the preferences, the bible and the holy role. The acolyte's sect conversion
// waits for the sects (rows in not-translated.md 106).

// tg: code/modules/jobs/job_types/chaplain/chaplain.dm:1
public partial class datum_job_chaplain : datum_job
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain.dm:43
    public override async DmTask after_spawn(mob_living spawned = null, client player_client = null)
    {
        await base.after_spawn(spawned, player_client);
        if (!ishuman(spawned))
            return;
        mob_living_carbon_human human_spawned = spawned as mob_living_carbon_human;
        var holy_bible = Dm.@new<obj_item_book_bible_booze>();
        if (Dm.truthy(GLOB.religion))
        {
            human_spawned.mind?.set_holy_role(HOLY_ROLE_PRIEST);
            holy_bible.deity_name = Dm.str(GLOB.deity);
            holy_bible.name = Dm.str(GLOB.bible_name);
            // These checks are important as there's no guarantee the "HOLY_ROLE_HIGHPRIEST" chaplain has selected a bible skin.
            if (Dm.truthy(GLOB.bible_icon_state))
                holy_bible.icon_state = Dm.str(GLOB.bible_icon_state);
            if (Dm.truthy(GLOB.bible_inhand_icon_state))
                holy_bible.inhand_icon_state = Dm.str(GLOB.bible_inhand_icon_state);
            to_chat(human_spawned, span_boldnotice(I18n.T("There is already an established religion onboard the station. You are an acolyte of []. Defer to the Chaplain.", GLOB.deity)));
            human_spawned.equip_to_storage(holy_bible, ITEM_SLOT_BACK, indirect_action: TRUE, del_on_fail: TRUE);
            Type nrt = GLOB.holy_weapon_type ?? typeof(obj_item_nullrod);
            obj_item nullrod = Dm.@new(nrt, human_spawned) as obj_item;
            await human_spawned.put_in_hands(nullrod);
            if (GLOB.religious_sect != null)
                Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain.dm:64", "after_spawn: GLOB.religious_sect.on_conversion, the sects (modules/religion)");
            return;
        }
        human_spawned.mind?.set_holy_role(HOLY_ROLE_HIGHPRIEST);

        object new_religion = player_client?.prefs?.read_preference(typeof(datum_preference_name_religion));
        if (!Dm.truthy(new_religion)) new_religion = DEFAULT_RELIGION;
        object new_deity = player_client?.prefs?.read_preference(typeof(datum_preference_name_deity));
        if (!Dm.truthy(new_deity)) new_deity = DEFAULT_DEITY;
        object new_bible = player_client?.prefs?.read_preference(typeof(datum_preference_name_bible));
        if (!Dm.truthy(new_bible)) new_bible = DEFAULT_BIBLE;

        holy_bible.deity_name = Dm.str(new_deity);
        switch (LOWER_TEXT(new_religion))
        {
            case "homosexuality":
            case "gay":
            case "penis":
            case "ass":
            case "cock":
            case "cocks":
                new_bible = pick("Guys Gone Wild", "Coming Out of The Closet", "War of Cocks");
                if (Dm.eq(new_bible, "War of Cocks"))
                    holy_bible.deity_name = Dm.str(pick("Dick Powers", "King Cock"));
                else
                    holy_bible.deity_name = Dm.str(pick("Gay Space Jesus", "Gandalf", "Dumbledore"));
                await human_spawned.adjust_organ_loss(ORGAN_SLOT_BRAIN, 100); // starts off brain damaged as fuck
                break;
            case "lol":
            case "wtf":
            case "poo":
            case "badmin":
            case "shitmin":
            case "deadmin":
            case "meme":
            case "memes":
            case "skibidi":
                new_bible = pick("Woody's Got Wood: The Aftermath", "Sweet Bro and Hella Jeff: Expanded Edition", "F.A.T.A.L. Rulebook", "Toilet Humor");
                switch (Dm.str(new_bible))
                {
                    case "Woody's Got Wood: The Aftermath":
                        holy_bible.deity_name = Dm.str(pick("Woody", "Andy", "Cherry Flavored Lube"));
                        break;
                    case "Sweet Bro and Hella Jeff: Expanded Edition":
                        holy_bible.deity_name = Dm.str(pick("Sweet Bro", "Hella Jeff", "Stairs", "AH"));
                        break;
                    case "F.A.T.A.L. Rulebook":
                        holy_bible.deity_name = "Twenty Ten-Sided Dice";
                        break;
                    case "Toilet Humor":
                        holy_bible.deity_name = Dm.str(pick("Skibidi Toilet", "Skibidi Wizard", "Skibidi Bathtub", "John Skibidi", "Skibidi Skibidi", "G-Toilet 1.0", "John Freeman"));
                        break;
                }
                await human_spawned.adjust_organ_loss(ORGAN_SLOT_BRAIN, 100); // also starts off brain damaged as fuck
                break;
            case "servicianism":
            case "partying":
                holy_bible.desc = "Happy, Full, Clean. Live it and give it.";
                break;
            case "weeaboo":
            case "kawaii":
                new_bible = pick("Fanfiction Compendium", "Japanese for Dummies", "The Manganomicon", "Establishing Your O.T.P");
                holy_bible.deity_name = "Anime";
                break;
            default:
                if (Dm.eq(new_bible, DEFAULT_BIBLE))
                    new_bible = Dm.text("The Holy Book of []", new_bible); // DEFAULT_BIBLE_REPLACE
                break;
        }

        holy_bible.name = Dm.str(new_bible);

        GLOB.religion = new_religion;
        GLOB.bible_name = new_bible;
        GLOB.deity = holy_bible.deity_name;

        human_spawned.equip_to_storage(holy_bible, ITEM_SLOT_BACK, indirect_action: TRUE, del_on_fail: TRUE);

        SSblackbox.record_feedback("text", "religion_name", 1, Dm.text("[]", new_religion), 1);
        SSblackbox.record_feedback("text", "religion_deity", 1, Dm.text("[]", new_deity), 1);
        SSblackbox.record_feedback("text", "religion_bible", 1, Dm.text("[]", new_bible), 1);
    }
}
