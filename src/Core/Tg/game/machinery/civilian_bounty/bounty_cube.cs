// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/civilian_bounty/bounty_cube.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file, the bounty cube with its nags and the debug cube; the announcement entries are var blocks. The
// cube's set_up proc is set_up__proc (the debug cube's var of the same name, rules 2.4). The var blocks are generated (D111).

/**
 * Upon completion of a civilian bounty, one of these is created.
 * It is sold to cargo to give the cargo budget bounty money, and the person who completed it cash.
 */
// tg: code/game/machinery/civilian_bounty/bounty_cube.dm:5
public partial class obj_item_bounty_cube : obj_item
{
    // tg: code/game/machinery/civilian_bounty/bounty_cube.dm:36
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_BARCODES, INNATE_TRAIT); // Don't allow anyone to override our pricetag component with a barcode
        return dot;
    }

    // tg: code/game/machinery/civilian_bounty/bounty_cube.dm:40
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(speed_bonus))
            Dm.append(ref dot, span_notice(I18n.T("<b>[]</b> remains until <b>[]</b> [] speedy delivery bonus lost.", time2text(Dm.num(next_nag_time) - world.time, "mm:ss", NO_TIMEZONE), bounty_value * speed_bonus, MONEY_NAME_SINGULAR)));
        if (Dm.truthy(handler_tip) && bounty_handler_account == null)
            Dm.append(ref dot, span_notice(I18n.T("Scan this in the cargo shuttle with an export scanner to register your bank account for the <b>[]</b> [] handling tip.", bounty_value * handler_tip, MONEY_NAME_SINGULAR)));
        return dot;
    }

    // tg: code/game/machinery/civilian_bounty/bounty_cube.dm:47
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        //if our nag cooldown has finished and we aren't on Centcom or in transit, then nag
        if (world.time >= Dm.num(next_nag_time) && !Dm.truthy(is_centcom_level(z)) && !Dm.truthy(is_reserved_level(z))) // COOLDOWN_FINISHED
        {
            //set up our fallback message, in case of AAS being broken it will be sent to card holders
            object nag_message = I18n.T("[] is unsent in [].", this, get_area(this));

            //nag on Supply channel and reduce the speed bonus multiplier to nothing
            obj_machinery_announcement_system aas = get_announcement_system(typeof(datum_aas_config_entry_bounty_cube_unsent), this, Dm.list(RADIO_CHANNEL_SUPPLY));
            if (aas != null)
            {
                nag_message = aas.compile_config_message(typeof(datum_aas_config_entry_bounty_cube_unsent), Dm.list(Dm.kv("LOCATION", get_area_name(this)), Dm.kv("COST", bounty_value)), "Regular Message");
                if (Dm.truthy(speed_bonus))
                    Waiting.NoSleep(aas.announce(typeof(datum_aas_config_entry_bounty_cube_unsent), Dm.list(Dm.kv("LOCATION", get_area_name(this)), Dm.kv("COST", bounty_value), Dm.kv("BONUSLOST", bounty_value * speed_bonus)), Dm.list(RADIO_CHANNEL_SUPPLY), "When Bonus Lost"));
                else
                    Waiting.NoSleep(aas.broadcast(Dm.text("[]", nag_message), Dm.list(RADIO_CHANNEL_SUPPLY)));
            }
            speed_bonus = 0;

            //alert the holder
            foreach (datum_bank_account bounty_holder_account in Dm.each<datum_bank_account>(bounty_holder_accounts))
                Waiting.NoSleep(bounty_holder_account.bank_card_talk(Dm.text("[]", nag_message)));

            //if someone has registered for the handling tip, nag them
            if (bounty_handler_account != null)
                Waiting.NoSleep(bounty_handler_account.bank_card_talk(Dm.text("[]", nag_message)));

            //increase our cooldown length and start it again
            nag_cooldown = nag_cooldown * nag_cooldown_multiplier;
            next_nag_time = world.time + nag_cooldown; // COOLDOWN_START
        }
        return null;
    }

    /**
     * Configures the bounty cube's name, value, and annouces to the crew
     */
    // tg: code/game/machinery/civilian_bounty/bounty_cube.dm:77
    public virtual async DmTask set_up__proc(datum_bounty my_bounty = null, obj_item_card_id holder_id = null)
    {
        bounty_value = Dm.num(my_bounty.get_bounty_reward());
        bounty_name = my_bounty.name;
        bounty_holder = holder_id.registered_name;
        bounty_holder_job = holder_id.assignment;
        bounty_holder_accounts = my_bounty.contribution;

        name = I18n.T("\\improper [] [] []", bounty_value, MONEY_SYMBOL, name);
        desc = Dm.text("[][]", desc, I18n.T(" The sales tag indicates it was <i>[] ([])</i>'s reward for completing the <i>[]</i> bounty.", bounty_holder, bounty_holder_job, bounty_name));
        await AddComponent(typeof(datum_component_pricetag), bounty_holder_accounts.Copy(), holder_cut, FALSE);
        await AddComponent(typeof(datum_component_gps), Dm.text("[]", this));

        START_PROCESSING(SSobj, this);
        next_nag_time = world.time + nag_cooldown; // COOLDOWN_START
        await aas_config_announce(typeof(datum_aas_config_entry_bounty_cube_created), Dm.list(
            Dm.kv("LOCATION", get_area_name(this)),
            Dm.kv("PERSON", bounty_holder),
            Dm.kv("RANK", bounty_holder_job),
            Dm.kv("BONUSTIME", time2text(Dm.num(next_nag_time) - world.time, "mm:ss", NO_TIMEZONE)),
            Dm.kv("COST", bounty_value)), this, Dm.list(RADIO_CHANNEL_SUPPLY));
    }
}

//for when you need a REAL bounty cube to test with and don't want to do a bounty each time your code changes
// tg: code/game/machinery/civilian_bounty/bounty_cube.dm:100
public partial class obj_item_bounty_cube_debug_cube : obj_item_bounty_cube
{
    // tg: code/game/machinery/civilian_bounty/bounty_cube.dm:106
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!isliving(user))
        {
            to_chat(user, span_warning(I18n.T("You aren't eligible to use this!")));
            return await base.attack_self(user, modifiers, __extra);
        }

        if (!Dm.truthy(set_up))
        {
            var squeezer = (mob_living)user;
            if (Dm.truthy(squeezer.get_bank_account()))
            {
                await set_up__proc(random_bounty(), squeezer.get_idcard() as obj_item_card_id);
                set_up = TRUE;
                return await base.attack_self(user, modifiers, __extra);
            }
            to_chat(user, span_notice(I18n.T("It can't detect your bank account.")));
        }

        return await base.attack_self(user, modifiers, __extra);
    }
}
