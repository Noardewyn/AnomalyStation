// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/plushes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3c, D316: the uplink's carp plushie and dehydrated carp). The plushes' cuffing to a wrist (the
// cuffable_item element) and the rule-8 achievement are leaves (not-translated.md section 136). The var blocks are generated (D111).

// tg: code/game/objects/items/plushes.dm:1
public partial class obj_item_toy_plush : obj_item_toy
{
    /*
    ** If you add a new plushie please add it to the lists at both:
    ** /obj/effect/spawner/random/entertainment/plushie
    ** /obj/effect/spawner/random/entertainment/plushie_delux
    */

    // tg: code/game/objects/items/plushes.dm:46
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_squeak), squeak_override));
        Waiting.NoSleep(AddElement(typeof(datum_element_bed_tuckable), mapload, 6, -5, 90));
        Waiting.NoSleep(AddElement(typeof(datum_element_toy_talk)));
        Dm.Deferred("code/game/objects/items/plushes.dm:51", "Initialize: AddElement(/datum/element/cuffable_item) (cuffing an item to a wrist: restraints, M10+)");

        //have we decided if Pinocchio goes in the blue or pink aisle yet?
        if (Dm.eq(gender, NEUTER))
            gender = prob(50) ? FEMALE : MALE;

        offspring_type ??= GetType();

        love_message = Dm.list(I18n.T("\n[] is so happy, \\he could rip a seam!", this));
        partner_message = Dm.list(I18n.T("\n[] has a ring on \\his finger! It says bound to my dear [].", this, partner));
        heartbroken_message = Dm.list(I18n.T("\n[] looks so sad.", this));
        vowbroken_message = Dm.list(I18n.T("\n[] lost \\his ring...", this));
        parent_message = Dm.list(I18n.T("\n[] can't remember what sleep is.", this));

        normal_desc = desc;
        return dot;
    }

    // tg: code/game/objects/items/plushes.dm:71
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref grenade);

        //inform next of kin and... acquaintances
        if (partner != null)
        {
            partner.bad_news(this);
            partner = null;
            lover = null;
        }
        else if (lover != null)
        {
            lover.bad_news(this);
            lover = null;
        }

        if (paternal_parent != null)
        {
            paternal_parent.bad_news(this);
            paternal_parent = null;
        }

        if (maternal_parent != null)
        {
            maternal_parent.bad_news(this);
            maternal_parent = null;
        }

        if (plush_child != null)
        {
            plush_child.bad_news(this);
            plush_child = null;
        }

        obj_item_toy_plush P;
        for (int i = 1; i <= scorned.len; i++)
        {
            P = scorned[i] as obj_item_toy_plush;
            P.bad_news(this);
        }
        scorned = null;

        for (int i = 1; i <= scorned_by.len; i++)
        {
            P = scorned_by[i] as obj_item_toy_plush;
            P.bad_news(this);
        }
        scorned_by = null;

        //null remaining lists
        squeak_override = null;

        love_message = null;
        partner_message = null;
        heartbroken_message = null;
        vowbroken_message = null;
        parent_message = null;

        return base.Destroy(force);
    }

    // tg: code/game/objects/items/plushes.dm:116
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == grenade)
            grenade = null;
        return dot;
    }

    // tg: code/game/objects/items/plushes.dm:121
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(stuffed) || grenade != null)
        {
            to_chat(user, span_notice(I18n.T("You pet []. D'awww.", this)));
            if (grenade != null && !Dm.truthy(grenade.active))
            {
                user.log_message(Dm.text("activated a hidden grenade in [].", this), LOG_VICTIM);
                await grenade.arm_grenade(user, msg: FALSE, volume: 10);
            }
        }
        else
            to_chat(user, span_notice(I18n.T("You try to pet [], but it has no stuffing. Aww...", this)));
        return dot;
    }

    // tg: code/game/objects/items/plushes.dm:131
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(tool.get_sharpness()))
        {
            if (grenade != null)
            {
                to_chat(user, span_notice(I18n.T("You remove the grenade from [].", this)));
                await user.put_in_hands(grenade);
                return ITEM_INTERACT_SUCCESS;
            }
            if (!Dm.truthy(stuffed))
            {
                to_chat(user, span_warning(I18n.T("You already murdered it!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(divine))
            {
                user.visible_message(span_notice(I18n.T("[] tears out the stuffing from []!", user, this)), span_notice(I18n.T("You rip a bunch of the stuffing from []. Murderer.", this)));
                tool.play_tool_sound(this);
                stuffed = FALSE;
                return ITEM_INTERACT_SUCCESS;
            }

            to_chat(user, span_notice(I18n.T("What a fool you are. [] is a god, how can you kill a god? What a grand and intoxicating innocence.", this)));
            await user.adjust_drunk_effect(20, up_to: 50);

            turf current_location = get_turf(user);
            area current_area = current_location?.loc as area; //copied from hand tele code
            if (current_location != null && current_area != null && ((int)Dm.num(current_area.area_flags) & NOTELEPORT) != 0)
                to_chat(user, span_notice(I18n.T("There is no escape. No recall or intervention can work in this place.")));
            else
                to_chat(user, span_notice(I18n.T("There is no escape. Although recall or intervention can work in this place, attempting to flee from []'s immense power would be futile.", this)));
            user.visible_message(span_notice(I18n.T("[] lays down their weapons and begs for []'s mercy!", user, this)), span_notice(I18n.T("You lay down your weapons and beg for []'s mercy.", this)));
            await user.drop_all_held_items();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_grenade) // isgrenade
        {
            if (Dm.truthy(stuffed))
            {
                to_chat(user, span_warning(I18n.T("You need to remove some stuffing first!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (grenade != null)
            {
                to_chat(user, span_warning(I18n.T("[] already has a grenade!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            // tg names [grenade] here before setting it (null: an empty name)
            user.visible_message(span_warning(I18n.T("[] slides [] into [].", user, grenade, this)), span_danger(I18n.T("You slide [] into [].", tool, this)));
            grenade = (obj_item_grenade)tool;
            user.log_message(Dm.text("added a grenade ([]) to []", tool.name, this), LOG_GAME);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_toy_plush plush)
        {
            love(plush, user);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    //~<3
    // tg: code/game/objects/items/plushes.dm:184
    public virtual void love(obj_item_toy_plush Kisser = null, mob_living user = null)
    {
        float chance = 100; //to steal a kiss, surely there's a 100% chance no-one would reject a plush such as I?
        float concern = 20; //perhaps something might cloud true love with doubt
        float loyalty = 30; //why should another get between us?
        float duty = 50; //conquering another's is what I live for

        //we are not catholic
        if (young == TRUE || Kisser.young == TRUE)
        {
            user.show_message(span_notice(I18n.T("[] plays tag with [].", this, Kisser)), MSG_VISUAL, span_notice(I18n.T("They're happy.")), NONE);
            Kisser.cheer_up();
            cheer_up();
        }

        //never again
        else if (Dm.@in(Kisser, scorned))
        {
            //message, visible, alternate message, neither visible nor audible
            user.show_message(span_notice(I18n.T("[] rejects the advances of []!", this, Kisser)), MSG_VISUAL, span_notice(I18n.T("That didn't feel like it worked.")), NONE);
        }
        else if (Dm.@in(this, Kisser.scorned))
        {
            user.show_message(span_notice(I18n.T("[] realises who [] is and turns away.", Kisser, this)), MSG_VISUAL, span_notice(I18n.T("That didn't feel like it worked.")), NONE);
        }

        //first comes love
        else if (Kisser.lover != this && Kisser.partner != this) //cannot be lovers or married
        {
            if (Kisser.lover != null) //if the initiator has a lover
            {
                Kisser.lover.heartbreak(Kisser); //the old lover can get over the kiss-and-run whilst the kisser has some fun
                chance -= concern; //one heart already broken, what does another mean?
            }
            if (lover != null) //if the recipient has a lover
                chance -= loyalty; //mustn't... but those lips
            if (partner != null) //if the recipient has a partner
                chance -= duty; //do we mate for life?

            if (prob(chance)) //did we bag a date?
            {
                user.visible_message(span_notice(I18n.T("[] makes [] kiss []!", user, Kisser, this)), span_notice(I18n.T("You make [] kiss []!", Kisser, this)));
                if (lover != null) //who cares for the past, we live in the present
                    lover.heartbreak(this);
                new_lover(Kisser);
                Kisser.new_lover(this);
            }
            else
                user.show_message(span_notice(I18n.T("[] rejects the advances of [], maybe next time?", this, Kisser)), MSG_VISUAL, span_notice(I18n.T("That didn't feel like it worked, this time.")), NONE);
        }

        //then comes marriage
        else if (Kisser.lover == this && Kisser.partner != this) //need to be lovers (assumes loving is a two way street) but not married (also assumes similar)
        {
            user.visible_message(span_notice(I18n.T("[] pronounces [] and [] married! D'aw.", user, Kisser, this)), span_notice(I18n.T("You pronounce [] and [] married!", Kisser, this)));
            new_partner(Kisser);
            Kisser.new_partner(this);
        }

        //then comes a baby in a baby's carriage, or an adoption in an adoption's orphanage
        else if (Kisser.partner == this && plush_child == null) //the one advancing does not take ownership of the child and we have a one child policy in the toyshop
        {
            user.visible_message(span_notice(I18n.T("[] is going to break [] and [] by bashing them like that.", user, Kisser, this)), span_notice(I18n.T("[] passionately embraces [] in your hands. Look away you perv!", Kisser, this)));
            // user.client.give_award(/datum/award/achievement/misc/rule8, user): the achievements are not ported
            Dm.Deferred("code/game/objects/items/plushes.dm:237", "love: user.client.give_award(/datum/award/achievement/misc/rule8, user) (the achievements)");
            if (plop(Kisser))
                user.visible_message(span_notice(I18n.T("Something drops at the feet of [].", user)), span_notice(I18n.T("The miracle of oh god did that just come out of []?!", this)));
        }

        //then comes protection, or abstinence if we are catholic
        else if (Kisser.partner == this && plush_child != null)
        {
            user.visible_message(span_notice(I18n.T("[] makes [] nuzzle []!", user, Kisser, this)), span_notice(I18n.T("You make [] nuzzle []!", Kisser, this)));
        }

        //then oh fuck something unexpected happened
        else
            user.show_message(span_warning(I18n.T("[] and [] don't know what to do with one another.", Kisser, this)), NONE);
    }

    // tg: code/game/objects/items/plushes.dm:250
    public virtual void heartbreak(obj_item_toy_plush Brutus = null)
    {
        if (lover != Brutus)
            throw Dm.CRASH("plushie heartbroken by a plushie that is not their lover", "code/game/objects/items/plushes.dm", 252);

        scorned.Add(Brutus);
        Brutus.scorned_by__proc(this);

        lover = null;
        Brutus.lover = null; //feeling's mutual

        heartbroken = TRUE;
        mood_message = pick(heartbroken_message);

        if (partner == Brutus) //oh dear...
        {
            partner = null;
            Brutus.partner = null; //it'd be weird otherwise
            vowbroken = TRUE;
            mood_message = pick(vowbroken_message);
        }

        update_desc();
    }

    // tg: code/game/objects/items/plushes.dm:271
    public virtual void scorned_by__proc(obj_item_toy_plush Outmoded = null)
    {
        scorned_by.Add(Outmoded);
    }

    // tg: code/game/objects/items/plushes.dm:274
    public virtual void new_lover(obj_item_toy_plush Juliet = null)
    {
        if (lover == Juliet)
            return; //nice try
        lover = Juliet;

        cheer_up();
        lover.cheer_up();

        mood_message = pick(love_message);
        update_desc();

        if (partner != null) //who?
            partner = null; //more like who cares
    }

    // tg: code/game/objects/items/plushes.dm:288
    public virtual void new_partner(obj_item_toy_plush Apple_of_my_eye = null)
    {
        if (partner == Apple_of_my_eye)
            return; //double marriage is just insecurity
        if (lover != Apple_of_my_eye)
            return; //union not born out of love will falter

        partner = Apple_of_my_eye;

        heal_memories();
        partner.heal_memories();

        mood_message = pick(partner_message);
        update_desc();
    }

    // tg: code/game/objects/items/plushes.dm:302
    public virtual bool plop(obj_item_toy_plush daddy = null)
    {
        if (partner != daddy)
            return false; //we do not have bastards in our toyshop

        // Ask the RNG if it looks more like mommy or daddy.
        Type chosen_type = pick(offspring_type, daddy.offspring_type) as Type;

        plush_child = Dm.@new(chosen_type, get_turf(loc as atom)) as obj_item_toy_plush;
        plush_child.make_young(this, daddy);
        // tg's proc ends without a return: null, so love() never says "Something drops"
        return false;
    }

    // tg: code/game/objects/items/plushes.dm:312
    public virtual void make_young(obj_item_toy_plush Mama = null, obj_item_toy_plush Dada = null)
    {
        if (Mama == Dada)
            return; //cloning is reserved for plants and spacemen

        maternal_parent = Mama;
        paternal_parent = Dada;
        young = TRUE;
        name = Dm.text("[] Jr", Mama); //Icelandic naming convention pending
        normal_desc = I18n.T("[] is a little baby of [] and []!", this, maternal_parent, paternal_parent); //original desc won't be used so the child can have moods
        transform = Dm.mul(transform, 0.75f);
        update_desc();

        Mama.mood_message = pick(Mama.parent_message);
        Mama.update_desc();
        Dada.mood_message = pick(Dada.parent_message);
        Dada.update_desc();
    }

    //cotton to cotton, sawdust to sawdust
    // tg: code/game/objects/items/plushes.dm:329
    public virtual void bad_news(obj_item_toy_plush Deceased = null)
    {
        bool is_that_letter_for_me = false;
        if (partner == Deceased) //covers marriage
        {
            is_that_letter_for_me = true;
            partner = null;
            lover = null;
        }
        else if (lover == Deceased) //covers lovers
        {
            is_that_letter_for_me = true;
            lover = null;
        }

        //covers children
        if (maternal_parent == Deceased)
        {
            is_that_letter_for_me = true;
            maternal_parent = null;
        }

        if (paternal_parent == Deceased)
        {
            is_that_letter_for_me = true;
            paternal_parent = null;
        }

        //covers parents
        if (plush_child == Deceased)
        {
            is_that_letter_for_me = true;
            plush_child = null;
        }

        //covers bad memories
        if (Dm.@in(Deceased, scorned))
        {
            scorned.Remove(Deceased);
            cheer_up(); //what cold button eyes you have
        }

        if (Dm.@in(Deceased, scorned_by))
            scorned_by.Remove(Deceased);

        //all references to the departed should be cleaned up by now

        if (is_that_letter_for_me)
        {
            heartbroken = TRUE;
            mood_message = pick(heartbroken_message);
            update_desc();
        }
    }

    //it'll be all right
    // tg: code/game/objects/items/plushes.dm:371
    public virtual void cheer_up()
    {
        if (!Dm.truthy(heartbroken))
            return; //you cannot make smile what is already
        if (Dm.truthy(vowbroken))
            return; //it's a pretty big deal

        heartbroken = Dm.truthy(heartbroken) ? FALSE : TRUE;

        if (Dm.@in(mood_message, heartbroken_message))
            mood_message = null;
        update_desc();
    }

    //time fixes all wounds
    // tg: code/game/objects/items/plushes.dm:383
    public virtual void heal_memories()
    {
        if (!Dm.truthy(vowbroken))
        {
            vowbroken = Dm.truthy(vowbroken) ? FALSE : TRUE;
            if (Dm.@in(mood_message, vowbroken_message))
                mood_message = null;
        }
        cheer_up();
    }

    // tg: code/game/objects/items/plushes.dm:389
    public override object update_desc(float? updates = null)
    {
        desc = Dm.str(normal_desc);
        object dot = base.update_desc(updates);
        if (Dm.truthy(mood_message))
            desc += Dm.str(mood_message);
        return dot;
    }
}

// tg: code/game/objects/items/plushes.dm:416
public partial class obj_item_toy_plush_ratplush : obj_item_toy_plush
{
    // tg: code/game/objects/items/plushes.dm:425
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (clash_target != null)
            return dot;
        obj_item_toy_plush_narplush P = Dm.locate<obj_item_toy_plush_narplush>(range(1, this));
        if (P != null && P.loc is turf_open && !Dm.truthy(P.clashing))
            Waiting.Detach(clash_of_the_plushies(P)); // INVOKE_ASYNC
        return dot;
    }

    // tg: code/game/objects/items/plushes.dm:433
    public virtual async DmTask clash_of_the_plushies(obj_item_toy_plush_narplush P = null)
    {
        clash_target = P;
        P.clashing = TRUE;
        Waiting.Detach(say("YOU."));
        Waiting.Detach(P.say("Ratvar?!"));
        obj_item_toy_plush a_winnar_is = null;
        float victory_chance = 10;
        for (int i = 1; i <= 10; i++) //We only fight ten times max
        {
            if (QDELETED(this))
            {
                P.clashing = FALSE;
                return;
            }
            if (QDELETED(P))
            {
                clash_target = null;
                return;
            }
            if (!Dm.truthy(await Adjacent(P)))
            {
                visible_message(span_warning(I18n.T("The two plushies angrily flail at each other before giving up.")));
                clash_target = null;
                P.clashing = FALSE;
                return;
            }
            playsound(this, Dm.resource("sound/effects/magic/clockwork/ratvar_attack.ogg"), 50, TRUE, frequency: 2);
            await sleep(0.24f * SECONDS);
            if (QDELETED(this))
            {
                P.clashing = FALSE;
                return;
            }
            if (QDELETED(P))
            {
                clash_target = null;
                return;
            }
            if (prob(victory_chance))
            {
                a_winnar_is = this;
                break;
            }
            P.SpinAnimation(5, 0);
            await sleep(0.5f * SECONDS);
            if (QDELETED(this))
            {
                P.clashing = FALSE;
                return;
            }
            if (QDELETED(P))
            {
                clash_target = null;
                return;
            }
            playsound(P, Dm.resource("sound/effects/magic/clockwork/narsie_attack.ogg"), 50, TRUE, frequency: 2);
            await sleep(0.33f * SECONDS);
            if (QDELETED(this))
            {
                P.clashing = FALSE;
                return;
            }
            if (QDELETED(P))
            {
                clash_target = null;
                return;
            }
            if (prob(victory_chance))
            {
                a_winnar_is = P;
                break;
            }
            SpinAnimation(5, 0);
            victory_chance += 10;
            await sleep(0.5f * SECONDS);
        }
        a_winnar_is ??= pick(this, P) as obj_item_toy_plush;
        if (a_winnar_is == this)
        {
            Waiting.Detach(say(pick("DIE.", "ROT.")));
            Waiting.Detach(P.say(pick("Nooooo...", "Not die. To y-", "Die. Ratv-", "Sas tyen re-")));
            playsound(this, Dm.resource("sound/effects/magic/clockwork/anima_fragment_attack.ogg"), 50, TRUE, frequency: 2);
            playsound(P, Dm.resource("sound/effects/magic/demon_dies.ogg"), 50, TRUE, frequency: 2);
            await explosion(P, light_impact_range: 1);
            qdel(P);
            clash_target = null;
        }
        else
        {
            Waiting.Detach(say("NO! I will not be banished again..."));
            Waiting.Detach(P.say(pick("Ha.", "Ra'sha fonn dest.", "You fool. To come here.")));
            playsound(this, Dm.resource("sound/effects/magic/clockwork/anima_fragment_death.ogg"), 62, TRUE, frequency: 2);
            playsound(P, Dm.resource("sound/effects/magic/demon_attack1.ogg"), 50, TRUE, frequency: 2);
            await explosion(this, light_impact_range: 1);
            qdel(this);
            P.clashing = FALSE;
        }
    }
}

// tg: code/game/objects/items/plushes.dm:504
public partial class obj_item_toy_plush_narplush : obj_item_toy_plush
{
    // tg: code/game/objects/items/plushes.dm:513
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        obj_item_toy_plush_ratplush P = Dm.locate<obj_item_toy_plush_ratplush>(range(1, this));
        if (P != null && P.loc is turf_open && P.clash_target == null && !Dm.truthy(clashing))
            Waiting.Detach(P.clash_of_the_plushies(this)); // INVOKE_ASYNC
        return dot;
    }
}

// tg: code/game/objects/items/plushes.dm:519
public partial class obj_item_toy_plush_lizard_plushie : obj_item_toy_plush
{
    // tg: code/game/objects/items/plushes.dm:528
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(greyscale_colors))
        {
            // Generate a random valid lizard color for our plushie friend
            string generated_lizard_color = "#" + random_color();
            // tg's rgb2hsv (__HELPERS/colors.dm) is rgb2num(rgb, COLORSPACE_HSV)
            DmList lizard_hsv = rgb2num(generated_lizard_color, COLORSPACE_HSV);

            // If our color is too dark, use the classic green lizard plush color
            if (Dm.num(lizard_hsv[3]) < 50)
                generated_lizard_color = "#66ff33";

            // Set our greyscale colors to the lizard color we made + black eyes
            Waiting.NoSleep(set_greyscale(colors: Dm.list(generated_lizard_color, COLOR_BLACK)));
        }
        return dot;
    }
}

// This is supposed to be only in the bus ruin, don't spawn it elsewhere
// tg: code/game/objects/items/plushes.dm:620
public partial class obj_item_toy_plush_awakenedplushie : obj_item_toy_plush
{
    // tg: code/game/objects/items/plushes.dm:626
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_edit_complainer)));
        return dot;
    }
}

// tg: code/game/objects/items/plushes.dm:630
public partial class obj_item_toy_plush_whiny_plushie : obj_item_toy_plush
{
    /// static list of cry messages it picks from to speak when it is insecure from no movement
    // tg: code/game/objects/items/plushes.dm:636  var/static/list/cry_still_messages
    public DmList cry_still_messages
    {
        get => TgState.Current.obj_item_toy_plush_whiny_plushie__cry_still_messages;
        set => TgState.Current.obj_item_toy_plush_whiny_plushie__cry_still_messages = value;
    }

    /// static list of cry messages it picks from to speak when it is insecure from no holder
    // tg: code/game/objects/items/plushes.dm:638  var/static/list/cry_alone_messages
    public DmList cry_alone_messages
    {
        get => TgState.Current.obj_item_toy_plush_whiny_plushie__cry_alone_messages;
        set => TgState.Current.obj_item_toy_plush_whiny_plushie__cry_alone_messages = value;
    }

    // tg: code/game/objects/items/plushes.dm:642
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(cry_still_messages))
        {
            cry_still_messages = Dm.list(
                "WHY DID WE STOP MOVING?! ARE YOU GOING TO LEAVE ME?!!",
                "WE COULD GET ATTACKED WE'RE SITTING DUCKS MOVE MOOOOOOOVE!!",
                "YOU'RE PLANNING ON DROPPING ME AREN'T YOU I KNOW YOU AAAAAAAREE!!",
                "THE SYNDICATE ARE TRIANGULATING OUR LOCAAAAAAAATIONNN!!",
                "THIS PLACE IS SCARY I WANNA LEEEEEAAAAAVVVEEEEEE!!",
                "CHELP, CHELP CCCCHHHHEEEEEEEEEEEEEEELLLLLLLPPPPPP!!");
            cry_alone_messages = Dm.list(
                "NOOOOOOOOOOOOOOOOOO DON'T LEAVE MEEEEE!!",
                "WUH WHERE DID EVERYONE GOOOOOOHHHHHHH WAHHH!!",
                "SOMEONE, ANYONEEEEEEEEE PICK ME UPP!!",
                "I DIDN'T DESERVE ITTTTTTTTTT!!",
                "I WILL DIE TO JUST ONE ATTTTTTAAAAACKKKKKK!!",
                "I WILLLLLL NOT DROP GOOOD IITITTEEEEEMMMS!!");
        }
        Waiting.NoSleep(AddComponent(typeof(datum_component_keep_me_secure), Dm.@new<datum_callback>(this, nameof(secured_process)), Dm.@new<datum_callback>(this, nameof(unsecured_process)), 0));
        return dot;
    }

    // tg: code/game/objects/items/plushes.dm:663
    public virtual void secured_process(object last_move = null)
    {
        icon_state = Dm.initial(this, (obj_item_toy_plush_whiny_plushie p) => p.icon_state);
    }

    // tg: code/game/objects/items/plushes.dm:666
    public virtual void unsecured_process(object last_move = null)
    {
        if (!(cry_cooldown <= world.time)) // COOLDOWN_FINISHED
            return;
        cry_cooldown = world.time + 10 * SECONDS; // COOLDOWN_START
        icon_state = "plushie_whiny_crying";
        if (isturf(loc))
            Waiting.Detach(say(pick(cry_alone_messages)));
        else
            Waiting.Detach(say(pick(cry_still_messages)));
        playsound(this, Dm.resource("sound/items/intents/Help.ogg"), 50, FALSE);
    }
}

// tg: code/game/objects/items/plushes.dm:687
public partial class obj_item_toy_plush_moth : obj_item_toy_plush
{
    // tg: code/game/objects/items/plushes.dm:698
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] stares deeply into the eyes of [] and it begins consuming []!  It looks like [] trying to commit suicide!", user, this, user.p_them(), user.p_theyre())));
        suicide_count++;
        if (suicide_count < 3)
            desc = I18n.T("A plushie depicting an unsettling mothperson. After killing [] [] it's not looking so huggable now...", suicide_count, suicide_count == 1 ? I18n.T("person") : I18n.T("people"));
        else
        {
            desc = I18n.T("A plushie depicting a creepy mothperson. It's killed [] people! I don't think I want to hug it any more!", suicide_count);
            divine = TRUE;
            resistance_flags = INDESTRUCTIBLE | FIRE_PROOF | ACID_PROOF | LAVA_PROOF;
        }
        playsound(this, Dm.resource("sound/effects/hallucinations/wail.ogg"), 50, TRUE, -1);
        DmList available_spots = get_adjacent_open_turfs(loc as atom) as DmList;
        if (Dm.truthy(available_spots?.len)) //If the user is in a confined space the plushie will drop normally as the user dies, but in the open the plush is placed one tile away from the user to prevent squeak spam
        {
            turf_open random_open_spot = pick(available_spots) as turf_open;
            await forceMove(random_open_spot);
        }
        await user.dust(just_ash: FALSE, drop_items: TRUE);
        return MANUAL_SUICIDE;
    }
}

// tg: code/game/objects/items/plushes.dm:796
public partial class obj_item_toy_plush_monkey : obj_item_toy_plush
{
    // tg: code/game/objects/items/plushes.dm:810
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        mob_living feeder = user;
        if (tool is not obj_item_food_grown_banana nana)
            return await base.item_interaction(user, tool, modifiers, __extra);
        await nana.forceMove(this); // go into the cotton stomach
        to_chat(feeder, span_notice(I18n.T("You hand over the [] to [] and watch as it eats...", nana, this)));
        playsound(this, Dm.resource("sound/items/eatfood.ogg"), 75, TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(eat), feeder, nana), 3 * SECONDS, file: "code/game/objects/items/plushes.dm", line: 816);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/plushes.dm:819
    public virtual async DmTask eat(mob_living feeder = null, obj_item_food_grown_banana nana = null)
    {
        if (nana is obj_item_food_grown_banana_bluespace)
            await do_teleport(this, get_turf(this), 15, channel: TELEPORT_CHANNEL_BLUESPACE);
        else if (nana is obj_item_food_grown_banana_mime && !Dm.truthy(french))
        {
            name = "peluche de singe";
            desc = "L'étiquette indique: 'Oop eek! Je suis un chimpanzé!', avec 'Maintenant en TAILLE JUMBO!' sur l'autre face.";
            french = TRUE;
        }
        // throw the peel at a random mob, or a random turf if there are none
        obj_item_grown_bananapeel peel = await Dm.new_async(nana.trash_type as Type, get_turf(this)) as obj_item_grown_bananapeel;
        DmList oviewers = Builtins.oviewers(peel.throw_range, this);
        atom throw_src = this;
        // if a player holds the plushie, the throw source will be the player
        if (isliving(loc))
        {
            oviewers = Builtins.oviewers(Center: loc);
            throw_src = loc as atom;
        }
        if (oviewers.len > 0 && Dm.@in(feeder, oviewers))
            oviewers.Remove(feeder); // remove feeder from targetables
        await peel.throw_at(oviewers.len == 0 ? get_ranged_target_turf(throw_src, pick(GLOB.alldirs), peel.throw_range) as atom : pick(oviewers) as atom, peel.throw_range, peel.throw_speed, quickstart: FALSE);
        playsound(this, Dm.resource("sound/mobs/non-humanoids/gorilla/gorilla.ogg"), 100, FALSE);
        spasm_animation(5 * SECONDS);
        qdel(nana);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/plushes.dm:636  var/static/list/cry_still_messages
    internal DmList obj_item_toy_plush_whiny_plushie__cry_still_messages;
    // tg: code/game/objects/items/plushes.dm:638  var/static/list/cry_alone_messages
    internal DmList obj_item_toy_plush_whiny_plushie__cry_alone_messages;
}
