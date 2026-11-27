// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/tcgsetup.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: SStrading_card_game, which loads the trading card sets and keywords of tg's strings/tcg at boot and keeps the cards
// by series and rarity, the packs' shared rarity tables, and the admins' card checks. The var block is generated (D111).

// tg: code/controllers/subsystem/tcgsetup.dm:1 SUBSYSTEM_DEF(trading_card_game)
public partial class datum_controller_subsystem_trading_card_game : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(trading_card_game) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SStrading_card_game)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/tcgsetup.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SStrading_card_game != this)
        {
            if (SStrading_card_game is datum_controller_subsystem_trading_card_game)
            {
                Recover();
                qdel(SStrading_card_game);
            }
            SStrading_card_game = this;
        }
        PreInit();
        await DmTask.CompletedTask;
    }

    //Let's load the cards before the map fires, so we can load cards on the map safely
    // tg: code/controllers/subsystem/tcgsetup.dm:22
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        reloadAllCardFiles();
        return SS_INIT_SUCCESS;
    }

    ///Loads all the card files
    // tg: code/controllers/subsystem/tcgsetup.dm:27
    public virtual void loadAllCardFiles()
    {
        foreach (object keyword_file in Dm.each(keyword_files))
            loadKeywordFile(keyword_file, card_directory);
        styleKeywords();
        foreach (object card_file in Dm.each(card_files))
            loadCardFile(card_file, card_directory);
    }

    ///Empty the rarity cache so we can safely add new cards
    // tg: code/controllers/subsystem/tcgsetup.dm:35
    public virtual void clearCards()
    {
        loaded = FALSE;
        cached_cards = Dm.list();
        keywords = Dm.list();
    }

    ///Reloads all card files
    // tg: code/controllers/subsystem/tcgsetup.dm:41
    public virtual void reloadAllCardFiles()
    {
        clearCards();
        loadAllCardFiles();
        loaded = TRUE;
    }

    ///Loads the contents of a json file into our global card list
    // tg: code/controllers/subsystem/tcgsetup.dm:47
    public virtual void loadKeywordFile(object filename = null, string directory = null)
    {
        directory ??= "strings/tcg";
        var keyword_data = json_decode(file2text(Dm.text("[]/[]", directory, filename))) as DmList;
        foreach (object keyword in Dm.each(keyword_data))
        {
            if (Dm.truthy(keywords[keyword]))
            {
                stack_trace(Dm.text("Dupe detected, [] was defined by []/[] after it already had a value!", keyword, directory, filename), "code/controllers/subsystem/tcgsetup.dm", 51);
                continue;
            }
            keywords[keyword] = keyword_data[keyword];
        }
    }

    ///Styles our keywords, converting them from just the raw text to the output we want
    // tg: code/controllers/subsystem/tcgsetup.dm:56
    public virtual void styleKeywords()
    {
        // Add the tooltip component to our text, make it pretty
        foreach (object keyword in Dm.each(keywords))
        {
            object tooltip_text = keywords[keyword];
            keywords[keyword] = span_tooltip(Dm.str(tooltip_text), Dm.str(keyword));
        }
    }

    ///Takes a string as input. Searches it for keywords in the pattern {$keyword}, and replaces them with their expanded form, generated above
    // tg: code/controllers/subsystem/tcgsetup.dm:63
    public virtual object resolve_keywords(object search_through = null)
    {
        object starting_text = search_through;
        while (Dm.truthy(TRUE))
        {
            float fragment_start = findtext(search_through, "{$");
            if (!Dm.truthy(fragment_start))
                break;
            float fragment_end = findtext(search_through, "}");
            if (!Dm.truthy(fragment_end))
                throw CRASH(Dm.text("[] contains a {$ that denotes the start of a keyword replacement, but not a closing }!", starting_text), "code/controllers/subsystem/tcgsetup.dm", 71);
            ///Gets the keyword this string wants to use
            ///We offset the start by two indexes to account for
            string keyword = copytext(search_through, fragment_start + 2, fragment_end);
            object replacement = keywords[keyword];
            if (!Dm.truthy(replacement))
                throw CRASH(Dm.text("[] contains a non-existent keyword! \\[[]\\]", starting_text, keyword), "code/controllers/subsystem/tcgsetup.dm", 77);
            search_through = replacetext(search_through, Dm.text("{$[]}", keyword), replacement);
        }

        return search_through;
    }

    ///Loads the contents of a json file into our global card list
    // tg: code/controllers/subsystem/tcgsetup.dm:83
    public virtual void loadCardFile(object filename = null, string directory = null)
    {
        directory ??= "strings/tcg";
        var json = json_decode(file2text(Dm.text("[]/[]", directory, filename))) as DmList;
        var cards = json["cards"] as DmList;
        DmList templates = Dm.list();
        foreach (DmList data in Dm.each<DmList>(json["templates"]))
            templates[data["template"]] = data;
        foreach (DmList data in Dm.each<DmList>(cards))
        {
            var card = Dm.@new<datum_card>(data, templates);
            //Lets cache the id by rarity, for top speed lookup later
            if (!Dm.truthy(cached_cards[card.series]))
            {
                cached_cards[card.series] = Dm.list();
                ((DmList)cached_cards[card.series])["ALL"] = Dm.list();
            }
            if (!Dm.truthy(((DmList)cached_cards[card.series])[card.rarity]))
                ((DmList)cached_cards[card.series])[card.rarity] = Dm.list();
            Dm.append_at((DmList)cached_cards[card.series], card.rarity, card.id);
            //Let's actually store the datum here
            ((DmList)((DmList)cached_cards[card.series])["ALL"])[card.id] = card;
        }
    }

    ///Because old me wanted to keep memory costs down, each cardpack type shares a rarity list
    ///We do the spooky stuff in here to ensure we don't have too many lists lying around
    // tg: code/controllers/subsystem/tcgsetup.dm:104
    public virtual object get_rarity_table(object type = null, DmList sample_table = null)
    {
        //Pass by refrance moment
        //This lets us only have one rarity table per pack, badmins beware
        //Yes this is horribly overengineered. No I am not sorry
        if (!Dm.truthy(cached_rarity_table[type]))
            cached_rarity_table[type] = sample_table;
        return cached_rarity_table[type];
    }

    ///See above
    // tg: code/controllers/subsystem/tcgsetup.dm:113
    public virtual object get_guarenteed_rarity_table(object type = null, DmList sample_table = null)
    {
        if (!Dm.truthy(cached_guar_rarity[type]))
            cached_guar_rarity[type] = sample_table;
        return cached_guar_rarity[type];
    }

    ///Prints all the cards names
    // tg: code/controllers/subsystem/tcgsetup.dm:119
    public virtual void printAllCards()
    {
        foreach (object card_set in Dm.each(cached_cards))
        {
            message_admins(Dm.text("Printing the [] set", card_set));
            DmList all = ((DmList)cached_cards[card_set])["ALL"] as DmList;
            foreach (object card in Dm.each(all))
            {
                var toPrint = all[card] as datum_card;
                message_admins(toPrint.name);
            }
        }
    }

    ///Checks the passed type list for missing raritys, or raritys out of bounds
    // tg: code/controllers/subsystem/tcgsetup.dm:127
    public virtual object check_cardpacks(object card_pack_list = null)
    {
        string toReturn = "";
        foreach (Type cardPack in Dm.each<Type>(card_pack_list))
        {
            var pack = (obj_item_cardpack)Dm.@new(cardPack);
            //Lets see if someone made a type yeah?
            if (!Dm.truthy(cached_cards[pack.series]))
            {
                toReturn += Dm.text("[] does not have any cards in it\n", pack.series);
                continue;
            }
            DmList all = ((DmList)cached_cards[pack.series])["ALL"] as DmList;
            foreach (object card in Dm.each(all))
            {
                var template = all[card] as datum_card;
                if (template.rarity == "ALL")
                {
                    toReturn += Dm.text("[] has a rarity [] on the card [] that needs to be changed to something that isn't \"ALL\"\n", pack.type, template.rarity, template.id);
                    continue;
                }
                if (!Dm.@in(template.rarity, pack.rarity_table))
                {
                    toReturn += Dm.text("[] has a rarity [] on the card [] that does not exist\n", pack.type, template.rarity, template.id);
                    continue;
                }
            }
            //Lets run a check to see if all the rarities exist that we want to exist exist
            foreach (object pack_rarity in Dm.each(pack.rarity_table))
            {
                if (!Dm.truthy(((DmList)cached_cards[pack.series])[pack_rarity]))
                    toReturn += Dm.text("[] does not have the required rarity []\n", pack.type, pack_rarity);
            }
            qdel(pack);
        }

        return toReturn;
    }

    ///Checks the global card list for cards that don't override all the default values of the card datum
    // tg: code/controllers/subsystem/tcgsetup.dm:154
    public virtual object check_card_datums()
    {
        // tg walks each card's vars (target.vars ^ thing.vars) and compares each with its initial(): the port does not translate vars
        // (rules 10.7); the admins' card check is not reached by play
        Dm.NotTranslated("code/controllers/subsystem/tcgsetup.dm:154", "check_card_datums: the admins' check of every card's vars against their defaults (vars, rules 10.7)");
        return null;
    }

    ///Used to test open a large amount of cardpacks
    // tg: code/controllers/subsystem/tcgsetup.dm:175
    public virtual void check_card_distribution(object cardPack = null, object batchSize = null, object batchCount = null, object guaranteed = null)
    {
        float totalCards = 0;
        //Gotta make this look like an associated list so the implicit "does this exist" checks work proper later
        DmList cardsByCount = Dm.list(Dm.kv("", 0));
        var pack = (obj_item_cardpack)Dm.@new((Type)cardPack);
        for (float index = 1; index <= Dm.num(batchCount); index++)
        {
            var cards = pack.buildCardListWithRarity(batchSize, guaranteed) as DmList;
            foreach (object id in Dm.each(cards))
            {
                totalCards++;
                cardsByCount[id] = Dm.num(cardsByCount[id]) + 1;
            }
        }
        string toSend = Dm.text("Out of [] cards", totalCards);
        foreach (object id in Dm.each(sort_list(cardsByCount, "/proc/" + nameof(GlobalProcs.cmp_num_string_asc))))
        {
            if (Dm.truthy(id))
            {
                var template = ((DmList)((DmList)cached_cards[pack.series])["ALL"])[id] as datum_card;
                toSend += Dm.text("\nID:[] [] []% Total:[]", id, template.name, (Dm.num(cardsByCount[id]) * 100) / totalCards, cardsByCount[id]);
            }
        }
        message_admins(toSend);
        qdel(pack);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/tcgsetup.dm:1  SUBSYSTEM_DEF(trading_card_game): GLOBAL_REAL(SStrading_card_game, /datum/controller/subsystem/trading_card_game)
    internal datum_controller_subsystem_trading_card_game SStrading_card_game;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_trading_card_game SStrading_card_game => ref TgState.Current.SStrading_card_game;
}
