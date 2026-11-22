// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/holiday/xmas.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the christmas tree spawner, which Meta maps (gaps G3, D291), and Gaps R3 (D327): the christmas crackers and the
// Santa event (a ghost poll for Santa, who spawns at a blob start with his antagonist datum). The var blocks are generated (D111).

// tg: code/modules/events/holiday/xmas.dm:1
public partial class obj_item_toy_xmas_cracker : obj_item_toy
{
    // tg: code/modules/events/holiday/xmas.dm:10
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(cracked) && ishuman(target) && !IS_UNCONSCIOUS_OR_CRIT(target) && target.get_active_held_item() == null)
        {
            target.visible_message(span_notice(I18n.T("[] and [] pop \\an []! *pop*", user, target, this)), span_notice(I18n.T("You pull \\an [] with []! *pop*", this, target)), span_hear(I18n.T("You hear a pop.")));
            var joke_paper = Dm.@new<obj_item_paper>(user.loc);
            joke_paper.name = I18n.T("[] joke", pick(I18n.T("awful"), I18n.T("terrible"), I18n.T("unfunny")));
            joke_paper.add_raw_text(pick(I18n.T("What did one snowman say to the other?\n\n<i>'Is it me or can you smell carrots?'</i>"),
                I18n.T("Why couldn't the snowman get laid?\n\n<i>He was frigid!</i>"),
                I18n.T("Where are Santa's helpers educated?\n\n<i>Nowhere, they're ELF-taught.</i>"),
                I18n.T("What happened to the man who stole advent calanders?\n\n<i>He got 25 days.</i>"),
                I18n.T("What does Santa get when he gets stuck in a chimney?\n\n<i>Claus-trophobia.</i>"),
                I18n.T("Where do you find chili beans?\n\n<i>The north pole.</i>"),
                I18n.T("What do you get from eating tree decorations?\n\n<i>Tinsilitis!</i>"),
                I18n.T("What do snowmen wear on their heads?\n\n<i>Ice caps!</i>"),
                I18n.T("Why is Christmas just like life on ss13?\n\n<i>You do all the work and the fat guy gets all the credit.</i>"),
                I18n.T("Why doesn't Santa have any children?\n\n<i>Because he only comes down the chimney.</i>")));
            joke_paper.update_appearance();
            Dm.@new<obj_item_clothing_head_costume_festive>(target.loc);
            await user.update_icons();
            cracked = TRUE;
            icon_state = "cracker1";
            var other_half = Dm.@new<obj_item_toy_xmas_cracker>(target);
            other_half.cracked = 1;
            other_half.icon_state = "cracker2";
            await target.put_in_active_hand(other_half);
            playsound(user, Dm.resource("sound/effects/snap.ogg"), 50, TRUE);
            return TRUE;
        }
        return await base.attack__proc(target, user, modifiers, attack_modifiers, __extra);
    }
}

// tg: code/modules/events/holiday/xmas.dm:46
public partial class obj_effect_spawner_xmastree : obj_effect_spawner
{
    // tg: code/modules/events/holiday/xmas.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(check_holidays(CHRISTMAS)) && christmas_tree != null)
            Dm.@new(christmas_tree, get_turf(this));
        else if (Dm.truthy(check_holidays(FESTIVE_SEASON)) && festive_tree != null)
            Dm.@new(festive_tree, get_turf(this));
        return dot;
    }
}

// tg: code/modules/events/holiday/xmas.dm:80
public partial class datum_round_event_ghost_role_santa : datum_round_event_ghost_role
{
    // tg: code/modules/events/holiday/xmas.dm:84
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce(I18n.T("Santa is coming to town!"), I18n.T("Unknown Transmission"));
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/holiday/xmas.dm:87
    public override async DmTask<object> start()
    {
        var chosen_one = await SSpolling.poll_ghost_candidates(I18n.T("Santa is coming to town! Do you want to be []?", span_notice(I18n.T("Santa"))), poll_time: 15 * SECONDS, alert_pic: typeof(obj_item_clothing_head_costume_santa), role_name_text: I18n.T("Santa"), amount_to_pick: 1) as mob;
        if (chosen_one == null)
            return NOT_ENOUGH_PLAYERS;
        santa = Dm.@new<mob_living_carbon_human>(pick(GLOB.blobstart));
        santa.PossessByPlayer(chosen_one.key);
        var A = Dm.@new<datum_antagonist_santa>();
        await santa.mind.add_antag_datum(A);
        return null;
    }
}
