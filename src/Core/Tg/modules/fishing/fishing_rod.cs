// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fishing_rod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: holds the [DmPath] of a type that the signatures of package B's turf and area files name (rules 2.3: the earliest .dme file that declares it), written by package B (wave 4); the file is translated with its module. X3: examine, examine_more and get_stat_info (the equipped slots and the cast range wait for the rod's Initialize and slots, fishing).

// tg: code/modules/fishing/fishing_rod.dm:1
[DmPath("/obj/item/fishing_rod")]
public partial class obj_item_fishing_rod : obj_item
{
    // tg: code/modules/fishing/fishing_rod.dm:119
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        // tg: the "It has \a [line], [hook] and [bait] equipped." line (each with its icon2html): the rod's line, hook and bait slots
        // hold their paths until its Initialize puts the parts in (set_slot), which comes with fishing.
        Dm.Deferred("code/modules/fishing/fishing_rod.dm:121", "examine: the equipped line, hook and bait (the slots are filled by the rod's Initialize and set_slot: fishing; their icon2html are the chat icons)");
        if (bait == null)
            Dm.append(ref dot, span_warning(I18n.T("It doesn't have a bait attached to it. Fishing will be more tedious!")));
        if (HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FISH))
        {
            Dm.append(ref dot, ""); //add a new line
            Dm.append(ref dot, span_notice(I18n.T("Thanks to your fishing skills, you can examine it again for more in-depth information.")));
            return dot;
        }
        // IS_HERETIC(user) (__DEFINES/antagonists.dm:293), expanded inline (rules 11.1)
        bool user_is_heretic = Dm.truthy(user.mind?.has_antag_datum(typeof(datum_antagonist_heretic))) || HAS_TRAIT(user, TRAIT_ACT_AS_HERETIC);
        if (HAS_TRAIT(this, TRAIT_ROD_MANSUS_INFUSED))
        {
            if (user_is_heretic)
                Dm.append(ref dot, span_purple(I18n.T("This rod has been <b>infused</b> by a heretic, improving its ability to catch glimpses of the Mansus. And fish.")));
            else
                Dm.append(ref dot, span_purple(I18n.T("It's glowing an eerie purple...")));
        }
        else if (user_is_heretic)
            Dm.append(ref dot, span_purple(I18n.T("As a Heretic, you can infuse this fishing rod with your <b>Mansus Grasp</b> by activating the spell while wielding it, to enhance its fishing power.")));
        return dot;
    }

    // tg: code/modules/fishing/fishing_rod.dm:144
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        if (!HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FISH))
            return dot;

        DmList block = Dm.list();
        bool get_percent = HAS_MIND_TRAIT(user, TRAIT_EXAMINE_DEEPER_FISH);
        // tg: block += span_info("You think you can cast it up to [get_cast_range()] tiles away."): get_cast_range reads the line slot
        // and the fishing skill (fishing)
        Dm.Deferred("code/modules/fishing/fishing_rod.dm:150", "examine_more: the cast range line (get_cast_range reads the line slot and the fishing skill: fishing)");
        Dm.append(ref block, get_stat_info(get_percent, difficulty_modifier * 0.01f, I18n.T("Fishing will be"), I18n.T("easier"), I18n.T("harder"), I18n.T("with this fishing rod")));
        Dm.append(ref block, get_stat_info(get_percent, experience_multiplier - 1, I18n.T("You will gain experience"), I18n.T("faster"), I18n.T("slower")));
        Dm.append(ref block, get_stat_info(get_percent, completion_speed_mult - 1, I18n.T("The minigame completion speed is"), I18n.T("faster"), I18n.T("slower")));
        Dm.append(ref block, get_stat_info(get_percent, bait_speed_mult - 1, I18n.T("Reeling is"), I18n.T("faster"), I18n.T("slower")));
        Dm.append(ref block, get_stat_info(get_percent, deceleration_mult - 1, I18n.T("Deceleration is"), I18n.T("faster"), I18n.T("slower")));
        Dm.append(ref block, get_stat_info(get_percent, bounciness_mult - 1, I18n.T("This fishing rod is "), I18n.T("bouncier"), I18n.T("less bouncy"), I18n.T("than a normal one"), less_is_better: true));
        Dm.append(ref block, get_stat_info(get_percent, gravity_mult - 1, I18n.T("The lure will sink"), I18n.T("faster"), I18n.T("slower"), span_info: true));

        list_clear_nulls(block);
        Dm.append(ref dot, boxed_message(jointext(block, "\n")));

        block = Dm.list();
        if (HAS_TRAIT(this, TRAIT_ROD_IGNORE_ENVIRONMENT))
            Dm.append(ref block, span_info(I18n.T("Environment and light shouldn't be an issue with this rod.")));
        if (HAS_TRAIT_NOT_FROM(this, TRAIT_ROD_REMOVE_FISHING_DUD, INNATE_TRAIT)) // Duds are innately removed by baits, we all know that.
            Dm.append(ref block, span_info(I18n.T("You won't catch duds with this rod.")));
        if (HAS_TRAIT(this, TRAIT_ROD_LAVA_USABLE))
            Dm.append(ref block, span_info(I18n.T("This fishing rod can be used to fish on lava.")));
        if (Dm.truthy(Dm.length(block)))
            Dm.append(ref dot, boxed_message(jointext(block, "\n")));
        return dot;
    }

    ///Used in examine_more to reduce all the copypasta when getting more information about the various stats of the fishing rod.
    // tg: code/modules/fishing/fishing_rod.dm:174
    public virtual object get_stat_info(bool get_percent, float value, string prefix, string easier, string harder, string suffix = null, bool span_info = false, bool less_is_better = false)
    {
        suffix ??= I18n.T("with this fishing rod");
        if (value == 0)
            return null;
        string percent = get_percent ? Dm.text("[]% ", abs(value * 100)) : "";
        string harder_easier = value > 0 ? easier : harder;
        string dot = Dm.text("[] [][] [].", prefix, percent, harder_easier, suffix);
        if (span_info)
            return GlobalProcs.span_info(dot);
        if (less_is_better ? value < 0 : value > 0)
            return span_nicegreen(dot);
        return span_danger(dot);
    }
}
