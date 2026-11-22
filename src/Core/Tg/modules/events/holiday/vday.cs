// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/holiday/vday.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file: the Valentine's Day event (the poll for a date, the pairs, their gifts and objectives, the third
// wheels and the heartbreaker), the valentine card and the candy heart. The var blocks are generated (D111).

// Valentine's Day events //
// why are you playing spessmens on valentine's day you wizard //

// tg: code/modules/events/holiday/vday.dm:8
public partial class datum_round_event_control_valentines : datum_round_event_control
{
    // tg: code/modules/events/holiday/vday.dm:4  #define VALENTINE_FILE "valentines.json" (a file-local define the file's three classes use, rules 11.4)
    internal const string VALENTINE_FILE = "valentines.json";
}

// tg: code/modules/events/holiday/vday.dm:29
public partial class datum_round_event_valentines : datum_round_event
{
    // tg: code/modules/events/holiday/vday.dm:29
    public virtual object is_valid_valentine(mob_living guy = null)
    {
        if (guy.stat == DEAD)
            return FALSE;
        if (guy.mind == null)
            return FALSE;
        if (Dm.truthy(guy.onCentCom()))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/events/holiday/vday.dm:38
    public virtual async DmTask give_valentines_things(mob_living guy = null)
    {
        if (control is not datum_round_event_control_valentines controller)
            return;

        var bag = Dm.locate<obj_item_storage_backpack>(guy.contents);
        if (bag == null)
            return;

        var drop_loc = guy.drop_location() as atom;
        foreach (object thing_type in Dm.each(controller.items_to_give_out))
        {
            var thing = (obj_item)Dm.@new((Type)thing_type, drop_loc);
            if (!Dm.truthy(bag.atom_storage.attempt_insert(thing, @override: TRUE, force: STORAGE_SOFT_LOCKED)))
                await guy.put_in_hands(thing);
        }
    }

    // tg: code/modules/events/holiday/vday.dm:53
    public virtual async DmTask forge_valentines_objective(mob_living lover = null, mob_living date = null)
    {
        var valentine = Dm.@new<datum_antagonist_valentine>();
        valentine.date = date.mind;
        await lover.mind.add_antag_datum(valentine); //These really should be teams but i can't be assed to incorporate third wheels right now
    }

    // tg: code/modules/events/holiday/vday.dm:58
    public virtual async DmTask forge_third_wheel(mob_living sad_one = null, mob_living date_one = null, mob_living date_two = null)
    {
        var third_wheel = Dm.@new<datum_antagonist_valentine_third_wheel>();
        third_wheel.date = pick(date_one.mind, date_two.mind) as datum_mind;
        await sad_one.mind.add_antag_datum(third_wheel);
    }

    // tg: code/modules/events/holiday/vday.dm:63
    public override async DmTask<object> start()
    {
        if (control is not datum_round_event_control_valentines controller)
            return null;

        DmList candidates = Dm.list();
        foreach (mob_living player in Dm.each<mob_living>(GLOB.player_list))
        {
            if (!Dm.truthy(is_valid_valentine(player)))
                continue;
            Dm.append(ref candidates, player);
        }

        var candidates_pruned = await SSpolling.poll_candidates(
            question: I18n.T("Do you want a Valentine?"),
            group: candidates,
            poll_time: 30 * SECONDS,
            flash_window: FALSE,
            start_signed_up: TRUE,
            alert_pic: typeof(obj_item_storage_fancy_heart_box),
            custom_response_messages: Dm.list(
                Dm.kv(POLL_RESPONSE_SIGNUP, I18n.T("You have signed up for a date!")),
                Dm.kv(POLL_RESPONSE_ALREADY_SIGNED, I18n.T("You are already signed up for a date.")),
                Dm.kv(POLL_RESPONSE_NOT_SIGNED, I18n.T("You aren't signed up for a date.")),
                Dm.kv(POLL_RESPONSE_TOO_LATE_TO_UNREGISTER, I18n.T("It's too late to decide against going on a date.")),
                Dm.kv(POLL_RESPONSE_UNREGISTERED, I18n.T("You decide against going on a date."))),
            chat_text_border_icon: typeof(obj_item_storage_fancy_heart_box)) as DmList;

        foreach (mob_living second_check in Dm.each_cast<mob_living>(candidates_pruned))
        {
            if (Dm.truthy(is_valid_valentine(second_check)))
                continue;
            Dm.remove(ref candidates_pruned, second_check);
        }

        if (Dm.length(candidates_pruned) == 0)
            return null;
        if (Dm.length(candidates_pruned) == 1)
        {
            to_chat(candidates_pruned[1], span_warning(I18n.T("You are the only one who wanted a Valentine...")));
            return null;
        }

        while (Dm.length(candidates_pruned) >= 2)
        {
            var date_one = (mob_living)pick_n_take(candidates_pruned);
            var date_two = (mob_living)pick_n_take(candidates_pruned);
            await give_valentines_things(date_one);
            await give_valentines_things(date_two);
            await forge_valentines_objective(date_one, date_two);
            await forge_valentines_objective(date_two, date_one);

            if ((Dm.length(candidates_pruned) == 1 && !Dm.truthy(controller.heartbreaker)) || (Dm.length(candidates_pruned) > 0 && Dm.truthy(prob(controller.third_wheel_chance))))
            {
                var third_wheel = (mob_living)pick_n_take(candidates_pruned);
                await give_valentines_things(third_wheel);
                await forge_third_wheel(third_wheel, date_one, date_two);
                // Third wheel starts with a bouquet because that's funny
                object third_wheel_bouquet = pick(typesof(typeof(obj_item_bouquet)));
                var bouquet = (obj_item)Dm.@new((Type)third_wheel_bouquet, third_wheel.loc);
                await third_wheel.put_in_hands(bouquet);
            }
        }

        if (Dm.truthy(controller.heartbreaker) && Dm.length(candidates_pruned) == 1)
            await ((mob)candidates_pruned[1]).mind.add_antag_datum(typeof(datum_antagonist_heartbreaker));
        return null;
    }

    // tg: code/modules/events/holiday/vday.dm:122
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce(I18n.T("It's Valentine's Day! Give a valentine to that special someone!"));
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/events/holiday/vday.dm:125
public partial class obj_item_paper_valentine : obj_item_paper
{
    // tg: code/modules/events/holiday/vday.dm:132
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object picked = pick_list(datum_round_event_control_valentines.VALENTINE_FILE, "valentines");
        default_raw_text = Dm.truthy(picked) ? picked : I18n.T("A generic message of love or whatever.");
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/events/holiday/vday.dm:136
public partial class obj_item_food_candyheart : obj_item_food
{
    // tg: code/modules/events/holiday/vday.dm:144
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        desc = Dm.str(pick(strings(datum_round_event_control_valentines.VALENTINE_FILE, "candyhearts")));
        icon_state = Dm.str(pick("candyheart", "candyheart2", "candyheart3", "candyheart4"));
        return dot;
    }
}
