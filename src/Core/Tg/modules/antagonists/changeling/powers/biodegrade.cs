// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/biodegrade.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Biodegrade (the restraints, the shoes, the straitjacket and the container melted, the captor
// sprayed with bio-acid). The var block is generated (D111); its three statics are TgState fields (rules 10.6).

// tg: code/modules/antagonists/changeling/powers/biodegrade.dm:1
public partial class datum_action_changeling_biodegrade : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/biodegrade.dm:11  var/static/bio_acid_path
    public static ref Type bio_acid_path => ref TgState.Current.datum_action_changeling_biodegrade__bio_acid_path;
    // tg: code/modules/antagonists/changeling/powers/biodegrade.dm:12  var/static/bio_acid_amount_per_spray
    public static ref float bio_acid_amount_per_spray => ref TgState.Current.datum_action_changeling_biodegrade__bio_acid_amount_per_spray;
    // tg: code/modules/antagonists/changeling/powers/biodegrade.dm:13  var/static/bio_acid_color
    public static ref string bio_acid_color => ref TgState.Current.datum_action_changeling_biodegrade__bio_acid_color;

    // tg: code/modules/antagonists/changeling/powers/biodegrade.dm:15
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        object dot = null;
        dot = FALSE;
        DmList restraints = Dm.list();
        obj handcuffs = user.get_item_by_slot(ITEM_SLOT_HANDCUFFED) as obj;
        obj legcuffs = user.get_item_by_slot(ITEM_SLOT_LEGCUFFED) as obj;
        obj_item_clothing_suit straitjacket = user.get_item_by_slot(ITEM_SLOT_OCLOTHING) as obj_item_clothing_suit;
        obj_item_clothing_shoes_sneakers_orange prisoner_shoes = user.get_item_by_slot(ITEM_SLOT_FEET) as obj_item_clothing_shoes_sneakers_orange;
        obj_item_clothing_shoes knotted_shoes = user.get_item_by_slot(ITEM_SLOT_FEET) as obj_item_clothing_shoes;
        obj some_manner_of_cage = (user.loc as obj);
        // rules 3.4 (wider stored values): buckled is any atom the user is buckled to, not only a mob
        object space_invader = (user.pulledby is var or__1 && Dm.truthy(or__1) ? or__1 : (object)user.buckled);

        if (!(prisoner_shoes is obj_item_clothing_shoes_sneakers_orange) || !Dm.truthy(prisoner_shoes.attached_cuffs))
            prisoner_shoes = null;
        if (!(knotted_shoes is obj_item_clothing_shoes) || knotted_shoes.tied != SHOES_KNOTTED)
            knotted_shoes = null;
        if (!Dm.truthy(straitjacket?.breakouttime))
            straitjacket = null;

        if (handcuffs == null && legcuffs == null && straitjacket == null && prisoner_shoes == null && knotted_shoes == null && some_manner_of_cage == null && !Dm.truthy(space_invader))
        {
            user.balloon_alert(user, I18n.T("already free!"));
            return dot;
        }
        await base.sting_action(user, target);

        if (handcuffs != null)
            restraints.Add(handcuffs);
        if (legcuffs != null)
            restraints.Add(legcuffs);
        if (straitjacket != null)
            restraints.Add(straitjacket);
        if (prisoner_shoes != null)
            restraints.Add(prisoner_shoes);
        if (knotted_shoes != null)
            restraints.Add(knotted_shoes);
        if (some_manner_of_cage != null)
            restraints.Add(some_manner_of_cage);

        foreach (obj restraint in Dm.each_cast<obj>(restraints))
        {
            if (Dm.truthy(Dm.bitand(restraint.obj_flags, Dm.bitor(Dm.bitor(INDESTRUCTIBLE, ACID_PROOF), UNACIDABLE))))
            {
                to_chat(user, span_changeling(I18n.T("We cannot use bio-acid to destroy []!", restraint)));
                continue;
            }

            if (restraint == user.loc)
            {
                restraint.visible_message(span_warning(I18n.T("Bubbling acid start spewing out of []...", restraint)));
                _addtimer(Dm.@new<datum_callback>(restraint, nameof(atom.atom_destruction), ACID), 4 * SECONDS, file: "code/modules/antagonists/changeling/powers/biodegrade.dm", line: 58);
                for (float beat = 1, beat__end = 3; beat <= beat__end; beat++)
                {
                    _addtimer(Dm.@new<datum_callback>(this, nameof(make_puddle), restraint), beat * SECONDS, file: "code/modules/antagonists/changeling/powers/biodegrade.dm", line: 60);
                    _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.playsound), restraint, Dm.resource("sound/items/tools/welder.ogg"), 50, TRUE), beat * SECONDS, file: "code/modules/antagonists/changeling/powers/biodegrade.dm", line: 61);
                }
                log_combat(user: user, target: restraint, what_done: "melted restraining container", addition: "(biodegrade)");
                return dot;
            }
            //otherwise it's some kind of worn restraint
            _addtimer(Dm.@new<datum_callback>(restraint, nameof(atom.atom_destruction), ACID), 1.5f * SECONDS, file: "code/modules/antagonists/changeling/powers/biodegrade.dm", line: 65);
            log_combat(user: user, target: restraint, what_done: "melted restraining item", addition: "(biodegrade)");
            user.visible_message(
                span_warning(I18n.T("[] spews torrents of acid onto [], melting them with horrifying ease.", user, restraint)),
                user.balloon_alert(user, I18n.T("melting restraints...")),
                span_danger(I18n.T("You hear retching, then the sizzling of powerful acid, closer to the sound of hissing steam.")));
            playsound(user, Dm.resource("sound/items/tools/welder.ogg"), 50, TRUE);
            dot = TRUE;
        }

        if (Dm.truthy(space_invader))
        {
            await punish_with_acid(user as mob_living_carbon_human, space_invader as mob_living);
            dot = TRUE;
        }
        return dot;
    }

    /// Spawn green acid puddle underneath obj, used for callback
    // tg: code/modules/antagonists/changeling/powers/biodegrade.dm:80
    public virtual object make_puddle(obj melted_restraint = null)
    {
        if (melted_restraint != null) // incase obj gets qdel'd
            return Dm.@new<obj_effect_decal_cleanable_greenglow>(get_turf(melted_restraint));
        return null;
    }

    // tg: code/modules/antagonists/changeling/powers/biodegrade.dm:84
    public virtual async DmTask acid_blast(atom_movable user = null, atom_movable target = null)
    {
        datum_reagents ephemeral_acid = Dm.@new<datum_reagents>();
        await ephemeral_acid.add_reagent(bio_acid_path, bio_acid_amount_per_spray);
        mutable_appearance splash_animation = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "splash") as mutable_appearance;
        splash_animation.color = bio_acid_color;
        target.flick_overlay_view(splash_animation, 3 * SECONDS);
        await ephemeral_acid.expose(target, TOUCH);
    }

    // tg: code/modules/antagonists/changeling/powers/biodegrade.dm:92
    public virtual async DmTask punish_with_acid(mob_living_carbon_human user = null, mob_living hapless_manhandler = null)
    {
        await acid_blast(user, hapless_manhandler);
        playsound(user, Dm.resource("sound/mobs/non-humanoids/bileworm/bileworm_spit.ogg"), 50, TRUE);
        if (Dm.truthy(IS_CHANGELING(hapless_manhandler)))
        {
            user.visible_message(
                span_danger(I18n.T("[] spews a mist of sizzling acid onto []... but nothing happens!", user, hapless_manhandler)),
                span_changeling(I18n.T("We prepare our escape, spraying bio-acid on our captor... []", span_danger(I18n.T("But nothing happened?!")))),
                span_danger(I18n.T("You hear retching, then a sizzling that terminates quite abruptly.")));
            to_chat(hapless_manhandler, span_changeling(I18n.T("Our prey attempts to dissuade us with one of our biology's simplest adaptions. Quaint.")));
            return;
        }
        user.visible_message(
            span_danger(I18n.T("[] spews a mist of sizzling acid onto [], using the opportunity to wrestle away.", user, hapless_manhandler)),
            user.balloon_alert(user, I18n.T("dissuading captor...")),
            span_danger(I18n.T("You hear retching, then sizzling, quickly muffled by a loud keening of pain.")));
        await hapless_manhandler.Stun(2 * SECONDS);
        await hapless_manhandler.emote("scream");
        await hapless_manhandler.stop_pulling();
        log_combat(user: user, target: hapless_manhandler, what_done: "acid-spewed to escape a grab", addition: "(biodegrade)");
    }
}

public sealed partial class TgState
{
    // tg: code/modules/antagonists/changeling/powers/biodegrade.dm:11  var/static/bio_acid_path
    internal Type datum_action_changeling_biodegrade__bio_acid_path = typeof(datum_reagent_toxin_acid_bio_acid);
    // tg: code/modules/antagonists/changeling/powers/biodegrade.dm:12  var/static/bio_acid_amount_per_spray
    internal float datum_action_changeling_biodegrade__bio_acid_amount_per_spray = 6;
    // tg: code/modules/antagonists/changeling/powers/biodegrade.dm:13  var/static/bio_acid_color
    internal string datum_action_changeling_biodegrade__bio_acid_color = "#9455ff";
}
