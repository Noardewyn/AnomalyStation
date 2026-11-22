// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/masks/hailer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298; the party horn: gaps CL1, D333). The phrase datums are generated; tg's phrase list and its markers are here.

// tg: code/modules/clothing/masks/hailer.dm:23  GLOBAL_LIST_INIT(hailer_phrases, ...)
public partial class datum_controller_global_vars
{
    public DmList hailer_phrases;

    // tg: code/modules/clothing/masks/hailer.dm:23  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhailer_phrases()
    {
        hailer_phrases = Dm.list(
            typeof(datum_hailer_phrase_emag),
            typeof(datum_hailer_phrase_halt),
            typeof(datum_hailer_phrase_bobby),
            typeof(datum_hailer_phrase_compliance),
            typeof(datum_hailer_phrase_justice),
            typeof(datum_hailer_phrase_running),
            typeof(datum_hailer_phrase_dontmove),
            typeof(datum_hailer_phrase_floor),
            typeof(datum_hailer_phrase_robocop),
            typeof(datum_hailer_phrase_god),
            typeof(datum_hailer_phrase_freeze),
            typeof(datum_hailer_phrase_imperial),
            typeof(datum_hailer_phrase_bash),
            typeof(datum_hailer_phrase_harry),
            typeof(datum_hailer_phrase_asshole),
            typeof(datum_hailer_phrase_stfu),
            typeof(datum_hailer_phrase_shutup),
            typeof(datum_hailer_phrase_super),
            typeof(datum_hailer_phrase_dredd));
        Dm.append(ref gvars_datum_init_order, nameof(hailer_phrases));
    }
}

// tg: code/modules/clothing/masks/hailer.dm:45
public partial class obj_item_clothing_mask_gas_sechailer : obj_item_clothing_mask_gas
{
    private const float PHRASE_COOLDOWN = 3 * SECONDS;
    private const float OVERUSE_COOLDOWN = 18 * SECONDS;
    private const int AGGR_GOOD_COP = 1;
    private const int AGGR_BAD_COP = 2;
    private const int AGGR_BROKEN = 4;
    private const int EMAG_PHRASE = 1; // index of emagged phrase
    private const int GOOD_COP_PHRASES = 6; // final index of good cop phrases
    private const int BAD_COP_PHRASES = 12; // final index of bad cop phrases
    private const int BROKE_PHRASES = 13; // starting index of broken phrases
    private const int ALL_PHRASES = 19; // total phrases

    // tg: code/modules/clothing/masks/hailer.dm:113
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.screwdriver_act(user, tool);
        if (aggressiveness == AGGR_BROKEN)
        {
            to_chat(user, span_danger(I18n.T("You adjust the restrictor but nothing happens, probably because it's broken.")));
            return dot;
        }
        string position = aggressiveness == AGGR_GOOD_COP ? I18n.T("middle") : aggressiveness == AGGR_BAD_COP ? I18n.T("last") : I18n.T("first");
        to_chat(user, span_notice(I18n.T("You set the restrictor to the [] position.", position)));
        aggressiveness = aggressiveness % 3 + 1; // loop AGGR_GOOD_COP -> AGGR_SHIT_COP
        return dot;
    }

    // tg: code/modules/clothing/masks/hailer.dm:122
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wirecutter_act(user, tool);
        if (aggressiveness != AGGR_BROKEN)
        {
            to_chat(user, span_danger(I18n.T("You broke the restrictor!")));
            aggressiveness = AGGR_BROKEN;
        }
        return dot;
    }

    // tg: code/modules/clothing/masks/hailer.dm:129
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        await halt();
        return null;
    }

    // tg: code/modules/clothing/masks/hailer.dm:132
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(safety))
        {
            safety = FALSE;
            balloon_alert(user, I18n.T("vocal circuit fried"));
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/clothing/masks/hailer.dm:139  GAME_VERB_SRC(/obj/item/clothing/mask/gas/sechailer, halt, usr, "HALT", null)
    public virtual async DmTask __gvb_halt(DmList structured_args = null)
    {
        if (!isliving(usr) || !Dm.truthy(await can_use(usr)) || hailer_cooldown > Dm.num(world.time)) // COOLDOWN_FINISHED, inline
            return;
        if (Dm.truthy(broken_hailer))
        {
            to_chat(usr, span_warning(I18n.T("\\The []'s hailing system is broken.", this)));
            return;
        }

        // handle recent uses for overuse
        recent_uses++;
        if (!Dm.truthy(overuse_cooldown)) // check if we can reset recent uses
        {
            recent_uses = 0;
            overuse_cooldown = TRUE;
            _addtimer(Dm.@new<datum_callback>(this, nameof(reset_overuse_cooldown)), OVERUSE_COOLDOWN, file: "code/modules/clothing/masks/hailer.dm", line: 152);
        }

        switch (recent_uses)
        {
            case 3:
                to_chat(usr, span_warning(I18n.T("\\The [] is starting to heat up.", this)));
                break;
            case 4:
                to_chat(usr, span_userdanger(I18n.T("\\The [] is heating up dangerously from overuse!", this)));
                break;
            case 5: // overload
                broken_hailer = TRUE;
                to_chat(usr, span_userdanger(I18n.T("\\The []'s power modulator overloads and breaks.", this)));
                return;
        }

        // select phrase to play
        play_phrase(usr, GLOB.hailer_phrases[select_phrase()] as Type);
    }

    // tg: code/modules/clothing/masks/hailer.dm:139  (the verb's body)
    public virtual async DmTask halt()
    {
        await __gvb_halt(Dm.list());
    }

    // tg: code/modules/clothing/masks/hailer.dm:167
    public virtual int select_phrase()
    {
        if (!Dm.truthy(safety))
            return EMAG_PHRASE;
        int upper_limit;
        switch (aggressiveness)
        {
            case AGGR_GOOD_COP:
                upper_limit = GOOD_COP_PHRASES;
                break;
            case AGGR_BAD_COP:
                upper_limit = BAD_COP_PHRASES;
                break;
            default:
                upper_limit = ALL_PHRASES;
                break;
        }
        return (int)Dm.num(rand(aggressiveness == AGGR_BROKEN ? BROKE_PHRASES : EMAG_PHRASE + 1, upper_limit));
    }

    // tg: code/modules/clothing/masks/hailer.dm:181
    public virtual object play_phrase(mob user = null, Type phrase = null)
    {
        if (hailer_cooldown > Dm.num(world.time)) // COOLDOWN_FINISHED, inline
            return null;
        hailer_cooldown = Dm.num(world.time) + PHRASE_COOLDOWN; // COOLDOWN_START, inline
        datum_hailer_phrase the_phrase = Dm.@new(phrase) as datum_hailer_phrase; // initial(phrase.phrase_text): the phrase's vars
        user.audible_message(Dm.text("[]'s Compli-o-Nator: <font color='red' size='4'><b>[]</b></font>", user, the_phrase.phrase_text));
        playsound(this, Dm.resource(Dm.text("sound/runtime/complionator/[].ogg", the_phrase.phrase_sound)), 100, FALSE, 4);
        qdel(the_phrase);
        return TRUE;
    }

    // tg: code/modules/clothing/masks/hailer.dm:189
    public virtual void reset_overuse_cooldown()
    {
        overuse_cooldown = FALSE;
    }
}

// tg: code/modules/clothing/masks/hailer.dm:192
public partial class obj_item_clothing_mask_whistle : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/hailer.dm:204
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (whistle_cooldown > Dm.num(world.time)) // COOLDOWN_FINISHED, inline
            return null;
        whistle_cooldown = Dm.num(world.time) + 10 * SECONDS; // COOLDOWN_START, inline
        user.audible_message("<font color='red' size='5'><b>HALT!</b></font>");
        playsound(this, Dm.resource("sound/items/whistle/whistle.ogg"), 50, FALSE, 4);
        return null;
    }
}

// tg: code/modules/clothing/masks/hailer.dm:214
public partial class obj_item_clothing_mask_party_horn : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/hailer.dm:223
    [DmParams("user", "action")]
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (!(horn_cooldown <= world.time)) // !COOLDOWN_FINISHED(src, horn_cooldown)
            return null;
        horn_cooldown = world.time + 10 * SECONDS; // COOLDOWN_START(src, horn_cooldown, 10 SECONDS)
        playsound(this, Dm.resource("sound/items/party_horn.ogg"), 75, FALSE);
        flick("party_horn_animated", this);
        await DmTask.CompletedTask;
        return null;
    }
}
