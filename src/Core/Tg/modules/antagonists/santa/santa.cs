// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/santa/santa.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file: Santa's antagonist datum (his outfit and teleport, the presents he cannot open and sees into,
// the prayers that mention him, his objective). The var block is generated (D111).

// tg: code/modules/antagonists/santa/santa.dm:1
public partial class datum_antagonist_santa : datum_antagonist
{
    // tg: code/modules/antagonists/santa/santa.dm:9
    public override async DmTask on_gain()
    {
        await base.on_gain();
        await give_equipment();
        give_objective();

        owner.add_traits(Dm.list(TRAIT_CANNOT_OPEN_PRESENTS, TRAIT_PRESENT_VISION), TRAIT_SANTA);

        santa_prayers = await owner.AddComponent(typeof(datum_component_listen_prayers), Dm.@new<datum_callback>(this, nameof(check_if_santa_prayer)), "Santa Claus", I18n.T("Allows you to listen for prayers that mention you or Christmas.")) as datum_component_listen_prayers;
    }

    // tg: code/modules/antagonists/santa/santa.dm:18
    public virtual object check_if_santa_prayer(DmList arguments = null)
    {
        object message = arguments[ARG_PRAYER_MSG];
        var boy_girl = arguments[ARG_PRAYING_MOB] as mob;
        var santa_regex = Dm.regex("(santa|claus|christmas|xmas)", "i");
        if (!Dm.truthy(santa_regex.Find(message)) && (!Dm.truthy(prob(60)) || !Dm.truthy(findtext(message, "satan"))))
            return FALSE; //The message doesn't mention us (or satan, cuz the names are so similar, accidents may happen)
        bool is_good_boy_girl = !Dm.truthy(boy_girl.is_antag()) && !HAS_TRAIT(boy_girl, TRAIT_EVIL);
        arguments[ARG_PRAYER_TYPE] = is_good_boy_girl ? SANTA_PRAYER : SANTA_NAUGHTY_PRAYER;
        arguments[ARG_PRAYER_SYMBOL] = Dm.icon(Dm.resource("icons/obj/storage/wrapping.dmi"), "giftdeliverypackage4");
        return TRUE;
    }

    // tg: code/modules/antagonists/santa/santa.dm:30
    public override async DmTask on_removal()
    {
        if (owner == null)
        {
            await base.on_removal();
            return;
        }
        owner.remove_traits(Dm.list(TRAIT_CANNOT_OPEN_PRESENTS, TRAIT_PRESENT_VISION), TRAIT_SANTA);
        QDEL_NULL(ref santa_prayers);
        if (owner.current != null)
        {
            var teleport = Dm.locate<datum_action_cooldown_spell_teleport_area_teleport_wizard_santa>(owner.current.actions);
            qdel(teleport);
        }
        await base.on_removal();
    }

    // tg: code/modules/antagonists/santa/santa.dm:40
    public override async DmTask greet()
    {
        await base.greet();
        to_chat(owner, span_bolddanger(I18n.T("Your objective is to bring joy to the people on this station. You have a magical bag, which generates presents as long as you have it! You can examine the presents to take a peek inside, to make sure that you give the right gift to the right person.")));
    }

    // tg: code/modules/antagonists/santa/santa.dm:44
    public virtual async DmTask give_equipment()
    {
        var H = owner.current as mob_living_carbon_human;
        if (H != null)
        {
            await H.equipOutfit(typeof(datum_outfit_santa));
            H.dna.update_dna_identity();
        }

        var teleport = Dm.@new<datum_action_cooldown_spell_teleport_area_teleport_wizard_santa>(owner);
        await teleport.Grant(H);
    }

    // tg: code/modules/antagonists/santa/santa.dm:53
    public virtual void give_objective()
    {
        var santa_objective = Dm.@new<datum_objective>();
        santa_objective.explanation_text = I18n.T("Bring joy and presents to the station!");
        santa_objective.completed = TRUE; //lets cut our santas some slack.
        santa_objective.owner = owner;
        Dm.combine(ref objectives, santa_objective);
    }
}
