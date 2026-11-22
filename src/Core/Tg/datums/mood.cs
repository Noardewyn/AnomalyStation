// Translated from tgstation (snapshot 1d99bd7f): code/datums/mood.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MS1 (D333, D339): the whole file. The var block is generated (D111) but for mood, which the converter types as a typepath
// (rule V3: tg assigns typepaths to other vars named mood) while it holds the sum of the moodlets.

/**
 * Mood datum
 *
 * Contains the logic for controlling a living mob's mood and sanity.
 */
// tg: code/datums/mood.dm:11
[DmPath("/datum/mood")]
public partial class datum_mood : datum
{
    // tg: code/datums/mood.dm:1-4  #define MINOR_INSANITY_PEN 5, MAJOR_INSANITY_PEN 10, MOOD_CATEGORY_NUTRITION "nutrition",
    // MOOD_CATEGORY_AREA_BEAUTY "area_beauty" (file-local, #undef at :697-700)
    private const float MINOR_INSANITY_PEN = 5;
    private const float MAJOR_INSANITY_PEN = 10;
    private const string MOOD_CATEGORY_NUTRITION = "nutrition";
    private const string MOOD_CATEGORY_AREA_BEAUTY = "area_beauty";

    /// The total combined value of all moodlets for the mob
    // tg: code/datums/mood.dm:16
    public float? mood;

    // tg: code/datums/mood.dm:43
    public override async DmTask New(params object[] __extra)
    {
        object mob_to_make_moody = Dm.arg(__extra, 0);
        if (!(mob_to_make_moody is mob_living))
        {
            stack_trace("Tried to apply mood to a non-living atom!", "code/datums/mood.dm", 45);
            qdel(this);
            return;
        }
        mob_living moody = (mob_living)mob_to_make_moody;

        // PORT: a world whose subsystem filter leaves SSmood out (a test's toy world) has no mood processing (rules: SS?. at Initialize)
        if (SSmood != null)
            START_PROCESSING(SSmood, this);

        mob_parent = moody;

        RegisterSignal(moody, COMSIG_MOB_HUD_CREATED, nameof(modify_hud));
        RegisterSignal(moody, COMSIG_ENTER_AREA, nameof(check_area_mood));
        RegisterSignal(moody, COMSIG_EXIT_AREA, nameof(exit_area));
        RegisterSignal(moody, COMSIG_LIVING_REVIVE, nameof(on_revive));
        RegisterSignal(moody, COMSIG_MOB_STATCHANGE, nameof(handle_mob_death));
        RegisterSignal(moody, COMSIG_QDELETING, nameof(clear_parent_ref));

        area our_area = get_area(moody) as area;
        if (Dm.truthy(our_area))
            check_area_mood(moody, our_area);

        moody.become_area_sensitive(MOOD_DATUM_TRAIT);
        if (Dm.truthy(moody.hud_used))
        {
            modify_hud();
            datum_hud hud = moody.hud_used;
            await hud.show_hud(hud.hud_version);
        }
    }

    // tg: code/datums/mood.dm:70
    [SignalHandler]
    public virtual void clear_parent_ref()
    {
        unmodify_hud();
        mob_parent.lose_area_sensitivity(MOOD_DATUM_TRAIT);
        UnregisterSignal(mob_parent, Dm.list(COMSIG_MOB_HUD_CREATED, COMSIG_ENTER_AREA, COMSIG_EXIT_AREA, COMSIG_LIVING_REVIVE, COMSIG_MOB_STATCHANGE, COMSIG_QDELETING));
        area our_area = get_area(mob_parent) as area;
        if (Dm.truthy(our_area))
            UnregisterSignal(our_area, COMSIG_AREA_BEAUTY_UPDATED);

        mob_parent = null;
    }

    // tg: code/datums/mood.dm:83
    public override object Destroy(float? force = null)
    {
        if (SSmood != null) // PORT: as in New
            STOP_PROCESSING(SSmood, this);
        QDEL_LIST_ASSOC_VAL(mood_events);
        return base.Destroy(force);
    }

    // tg: code/datums/mood.dm:88
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        float seconds = Dm.num(seconds_per_tick);
        switch (mood_level)
        {
            case MOOD_LEVEL_SAD4:
                Waiting.NoSleep(adjust_sanity(-0.3f * seconds, SANITY_INSANE));
                break;
            case MOOD_LEVEL_SAD3:
                Waiting.NoSleep(adjust_sanity(-0.15f * seconds, SANITY_CRAZY));
                break;
            case MOOD_LEVEL_SAD2:
                Waiting.NoSleep(adjust_sanity(-0.1f * seconds, SANITY_UNSTABLE));
                break;
            case MOOD_LEVEL_SAD1:
                Waiting.NoSleep(adjust_sanity(-0.05f * seconds, SANITY_UNSTABLE));
                break;
            case MOOD_LEVEL_NEUTRAL:
                Waiting.NoSleep(adjust_sanity(0, SANITY_UNSTABLE)); //This makes sure that mood gets increased should you be below the minimum.
                break;
            case MOOD_LEVEL_HAPPY1:
                Waiting.NoSleep(adjust_sanity(0.2f * seconds, SANITY_UNSTABLE));
                break;
            case MOOD_LEVEL_HAPPY2:
                Waiting.NoSleep(adjust_sanity(0.3f * seconds, SANITY_UNSTABLE));
                break;
            case MOOD_LEVEL_HAPPY3:
                Waiting.NoSleep(adjust_sanity(0.4f * seconds, SANITY_NEUTRAL, SANITY_MAXIMUM));
                break;
            case MOOD_LEVEL_HAPPY4:
                Waiting.NoSleep(adjust_sanity(0.6f * seconds, SANITY_NEUTRAL, SANITY_MAXIMUM));
                break;
        }
        return null;
    }

    // tg: code/datums/mood.dm:108
    [SignalHandler]
    public virtual void handle_mob_death(datum source = null, object new_stat = null, object old_stat = null)
    {
        if (Dm.eq(old_stat, DEAD) && !Dm.eq(new_stat, DEAD))
        {
            if (SSmood != null) // PORT: as in New
                START_PROCESSING(SSmood, this);
        }
        else if (!Dm.eq(old_stat, DEAD) && Dm.eq(new_stat, DEAD))
        {
            if (SSmood != null)
                STOP_PROCESSING(SSmood, this);
        }
    }

    /// Handles mood given by nutrition
    // tg: code/datums/mood.dm:117
    public virtual async DmTask<object> update_nutrition_moodlets()
    {
        if (HAS_TRAIT(mob_parent, TRAIT_NOHUNGER))
        {
            clear_mood_event(MOOD_CATEGORY_NUTRITION);
            return FALSE;
        }

        if (HAS_TRAIT(mob_parent, TRAIT_GLUTTON))
        {
            await add_mood_event(MOOD_CATEGORY_NUTRITION, typeof(datum_mood_event_hungry)); //you'll never get enough
            return TRUE;
        }

        if (HAS_TRAIT(mob_parent, TRAIT_FAT) && !HAS_TRAIT(mob_parent, TRAIT_VORACIOUS))
        {
            await add_mood_event(MOOD_CATEGORY_NUTRITION, typeof(datum_mood_event_fat));
            return TRUE;
        }

        float sw__1 = mob_parent.nutrition;
        if (sw__1 >= NUTRITION_LEVEL_FULL)
            await add_mood_event(MOOD_CATEGORY_NUTRITION, HAS_TRAIT(mob_parent, TRAIT_VORACIOUS) ? typeof(datum_mood_event_wellfed) : typeof(datum_mood_event_too_wellfed));
        else if (sw__1 >= NUTRITION_LEVEL_WELL_FED && sw__1 <= NUTRITION_LEVEL_FULL)
            await add_mood_event(MOOD_CATEGORY_NUTRITION, typeof(datum_mood_event_wellfed));
        else if (sw__1 >= NUTRITION_LEVEL_FED && sw__1 <= NUTRITION_LEVEL_WELL_FED)
            await add_mood_event(MOOD_CATEGORY_NUTRITION, typeof(datum_mood_event_fed));
        else if (sw__1 >= NUTRITION_LEVEL_HUNGRY && sw__1 <= NUTRITION_LEVEL_FED)
            clear_mood_event(MOOD_CATEGORY_NUTRITION);
        else if (sw__1 >= NUTRITION_LEVEL_VERY_HUNGRY && sw__1 <= NUTRITION_LEVEL_HUNGRY)
            await add_mood_event(MOOD_CATEGORY_NUTRITION, typeof(datum_mood_event_hungry));
        else if (sw__1 >= NUTRITION_LEVEL_STARVING && sw__1 <= NUTRITION_LEVEL_VERY_HUNGRY)
            await add_mood_event(MOOD_CATEGORY_NUTRITION, typeof(datum_mood_event_hungry_very));
        else if (sw__1 >= 0 && sw__1 <= NUTRITION_LEVEL_STARVING)
            await add_mood_event(MOOD_CATEGORY_NUTRITION, typeof(datum_mood_event_starving));

        return TRUE;
    }

    /**
     * Adds a mood event to the mob
     *
     * Arguments:
     * * category - (text) category of the mood event - see /datum/mood_event for category explanation
     * * type - (path) any /datum/mood_event (besides /datum/mood_event/conditional)
     */
    // tg: code/datums/mood.dm:155
    public virtual async DmTask add_mood_event(object category = null, object new_type = null, params object[] __extra)
    {
        if (!ispath(new_type, typeof(datum_mood_event)))
        {
            throw Dm.CRASH(Dm.text("A non path ([]), was used to add a mood event. This shouldn't be happening.", new_type), "code/datums/mood.dm", 157);
        }
        if (ispath(new_type, typeof(datum_mood_event_conditional)))
        {
            throw Dm.CRASH(Dm.text("A conditional mood event ([]) was used in add_mood_event. Use add_conditional_mood_event instead.", new_type), "code/datums/mood.dm", 159);
        }
        if (!istext(category))
            category = REF(category);

        DmList args = Dm.args(category, new_type, __extra);
        DmList @params = args.Copy(3);
        datum_mood_event new_event = await Dm.new_async((Type)new_type, category) as datum_mood_event;
        if (!Dm.truthy(await Dm.Call(new_event, nameof(datum_mood_event.can_effect_mob), Dm.arglist(Dm.add(Dm.list(this, mob_parent), @params)))))
        {
            qdel(new_event);
            return;
        }

        await add_mood_event_instance(new_event, @params);
    }

    /**
     * Handles adding a mood event instance, including replacing or refreshing existing events
     */
    // tg: code/datums/mood.dm:174
    [DmPrivate]
    public virtual async DmTask add_mood_event_instance(datum_mood_event new_event = null, DmList @params = null)
    {
        object category = new_event.category;
        datum_mood_event existing_event = mood_events[category] as datum_mood_event;
        if (Dm.truthy(existing_event))
        {
            object continue_adding = FALSE;
            if (existing_event.type == new_event.type)
                continue_adding = await Dm.Call(existing_event, nameof(datum_mood_event.be_refreshed), Dm.arglist(Dm.add(Dm.list(this), @params)));
            else
                continue_adding = await Dm.Call(existing_event, nameof(datum_mood_event.be_replaced), Dm.arglist(Dm.add(Dm.list(this, new_event), @params)));
            if (!Dm.truthy(continue_adding))
            {
                update_mood();
                qdel(new_event);
                return;
            }
            clear_mood_event(category);
        }

        await new_event.on_add(this, mob_parent, @params);
        mood_events[category] = new_event;
        update_mood();
        if (new_event.mood_change == 0 || Dm.truthy(new_event.hidden))
            return;
        if (new_event.mood_change > 0)
        {
            await add_personality_mood_to_viewers(mob_parent, "other_good_moodlet", Dm.list(
                Dm.kv(typeof(datum_personality_empathetic), typeof(datum_mood_event_empathetic_happy)),
                Dm.kv(typeof(datum_personality_misanthropic), typeof(datum_mood_event_misanthropic_sad))
            ), range: 4);
        }
        else
        {
            await add_personality_mood_to_viewers(mob_parent, "other_bad_moodlet", Dm.list(
                Dm.kv(typeof(datum_personality_empathetic), typeof(datum_mood_event_empathetic_sad)),
                Dm.kv(typeof(datum_personality_misanthropic), typeof(datum_mood_event_misanthropic_happy))
            ), range: 4);
        }
    }

    /**
     * Adds a conditional mood event to the mob
     *
     * Arguments:
     * * category - (text) category of the mood event - see /datum/mood_event for category explanation
     * * base_type - (path) any /datum/mood_event/conditional
     */
    // tg: code/datums/mood.dm:213
    public virtual async DmTask add_conditional_mood_event(object category = null, object base_type = null, params object[] __extra)
    {
        if (!ispath(base_type, typeof(datum_mood_event_conditional)))
        {
            if (ispath(base_type, typeof(datum_mood_event)))
            {
                throw Dm.CRASH(Dm.text("A non-conditional mood event ([]) was used in add_conditional_mood_event. Use add_mood_event instead.", base_type), "code/datums/mood.dm", 216);
            }
            throw Dm.CRASH(Dm.text("A non path ([]), was used to add a mood event. This shouldn't be happening.", base_type), "code/datums/mood.dm", 217);
        }
        if (!istext(category))
            category = REF(category);

        DmList args = Dm.args(category, base_type, __extra);
        DmList @params = args.Copy(3);
        DmList all_valid_conditional_events = Dm.list();
        foreach (object event_subtype in Dm.each(valid_typesof((Type)base_type)))
        {
            datum_mood_event potential_event = await Dm.new_async((Type)event_subtype, category) as datum_mood_event;
            if (!Dm.truthy(await Dm.Call(potential_event, nameof(datum_mood_event.can_effect_mob), Dm.arglist(Dm.add(Dm.list(this, mob_parent), @params)))))
            {
                qdel(potential_event);
                continue;
            }

            Dm.append(ref all_valid_conditional_events, potential_event);
        }

        if (!Dm.truthy(length(all_valid_conditional_events)))
            return; //no valid events to add

        datum_mood_event_conditional highest_priority_event = null;
        foreach (datum_mood_event_conditional checked_event in Dm.each_cast<datum_mood_event_conditional>(all_valid_conditional_events))
        {
            if (!Dm.truthy(highest_priority_event) || checked_event.priority > highest_priority_event.priority)
                highest_priority_event = checked_event;
        }

        await add_mood_event_instance(highest_priority_event, @params);
        Dm.remove(ref all_valid_conditional_events, highest_priority_event); // you are the chosen one
        QDEL_LIST(all_valid_conditional_events); // clean up the losers
    }

    /**
     * Removes a mood event from the mob
     *
     * Arguments:
     * * category - (Text) Removes the mood event with the given category
     */
    // tg: code/datums/mood.dm:249
    public virtual void clear_mood_event(object category = null)
    {
        if (!istext(category))
            category = REF(category);

        datum_mood_event @event = mood_events[category] as datum_mood_event;
        if (!Dm.truthy(@event))
            return;

        Dm.remove(ref mood_events, category);
        qdel(@event);
        update_mood();
    }

    // tg: code/datums/mood.dm:261
    public virtual object get_mood_event(object category = null)
    {
        return mood_events[category];
    }

    /// Updates the mobs mood.
    /// Called after mood events have been added/removed.
    // tg: code/datums/mood.dm:266
    public virtual void update_mood()
    {
        if (QDELETED(mob_parent)) //don't bother updating their mood if they're about to be salty anyway. (in other words, we're about to be destroyed too anyway.)
            return;
        mood = 0;
        shown_mood = 0;

        if (!HAS_TRAIT(mob_parent, TRAIT_APATHETIC))
        {
            foreach (object category in Dm.each(mood_events))
            {
                datum_mood_event the_event = mood_events[category] as datum_mood_event;
                float event_mood = the_event.mood_change;
                event_mood *= max((event_mood > 0) ? positive_mood_modifier : negative_mood_modifier, 0);
                mood += event_mood;
                if (!Dm.truthy(the_event.hidden))
                    shown_mood = Dm.add(shown_mood, event_mood);
            }

            mood *= max(mood_modifier, 0);
            shown_mood = Dm.mul(shown_mood, max(mood_modifier, 0));
        }

        float sw__1 = Dm.num(mood);
        if (sw__1 <= MOOD_SAD4)
            mood_level = MOOD_LEVEL_SAD4;
        else if (sw__1 >= MOOD_SAD4 && sw__1 <= MOOD_SAD3)
            mood_level = MOOD_LEVEL_SAD3;
        else if (sw__1 >= MOOD_SAD3 && sw__1 <= MOOD_SAD2)
            mood_level = MOOD_LEVEL_SAD2;
        else if (sw__1 >= MOOD_SAD2 && sw__1 <= MOOD_SAD1)
            mood_level = MOOD_LEVEL_SAD1;
        else if (sw__1 >= MOOD_SAD1 && sw__1 <= MOOD_HAPPY1)
            mood_level = MOOD_LEVEL_NEUTRAL;
        else if (sw__1 >= MOOD_HAPPY1 && sw__1 <= MOOD_HAPPY2)
            mood_level = MOOD_LEVEL_HAPPY1;
        else if (sw__1 >= MOOD_HAPPY2 && sw__1 <= MOOD_HAPPY3)
            mood_level = MOOD_LEVEL_HAPPY2;
        else if (sw__1 >= MOOD_HAPPY3 && sw__1 <= MOOD_HAPPY4)
            mood_level = MOOD_LEVEL_HAPPY3;
        else if (sw__1 >= MOOD_HAPPY4)
            mood_level = MOOD_LEVEL_HAPPY4;

        update_mood_icon();
        mob_parent.SEND_SIGNAL(COMSIG_CARBON_MOOD_UPDATE, Dm.list(mob_parent));
    }

    /// Updates the mob's mood icon
    // tg: code/datums/mood.dm:308
    public virtual void update_mood_icon()
    {
        if (!Dm.truthy(mob_parent.client) || !Dm.truthy(mob_parent.hud_used))
            return;

        atom_movable_screen_mood mood_screen_object = mob_parent.hud_used.screen_objects?[HUD_MOB_MOOD] as atom_movable_screen_mood;
        if (!(mood_screen_object is atom_movable_screen_mood))
            return;

        mood_screen_object.cut_overlays();
        mood_screen_object.color = Dm.initial(mood_screen_object, (atom p) => p.color);

        // lets see if we have an special icons to show instead of the normal mood levels
        DmList conflicting_moodies = Dm.list();
        float highest_absolute_mood = 0;
        foreach (object category in Dm.each(mood_events))
        {
            datum_mood_event the_event = mood_events[category] as datum_mood_event;
            if (!Dm.truthy(the_event.special_screen_obj))
                continue;
            if (!Dm.truthy(the_event.special_screen_replace))
                mood_screen_object.add_overlay(the_event.special_screen_obj);
            else
            {
                Dm.append(ref conflicting_moodies, the_event);
                float absmood = abs(the_event.mood_change);
                highest_absolute_mood = absmood > highest_absolute_mood ? absmood : highest_absolute_mood;
            }
        }

        switch (sanity_level)
        {
            case SANITY_LEVEL_GREAT:
                mood_screen_object.color = "#2eeb9a";
                break;
            case SANITY_LEVEL_NEUTRAL:
                mood_screen_object.color = "#86d656";
                break;
            case SANITY_LEVEL_DISTURBED:
                mood_screen_object.color = "#4b96c4";
                break;
            case SANITY_LEVEL_UNSTABLE:
                mood_screen_object.color = "#dfa65b";
                break;
            case SANITY_LEVEL_CRAZY:
                mood_screen_object.color = "#f38943";
                break;
            case SANITY_LEVEL_INSANE:
                mood_screen_object.color = "#f15d36";
                break;
        }

        if (!Dm.truthy(conflicting_moodies.len)) // there's no special icons, use the normal icon states
        {
            mood_screen_object.icon_state = Dm.text("mood[]", mood_level);
            return;
        }

        foreach (datum_mood_event conflicting_event in Dm.each_cast<datum_mood_event>(conflicting_moodies))
        {
            if (abs(conflicting_event.mood_change) == highest_absolute_mood)
            {
                mood_screen_object.icon_state = Dm.text("[]", conflicting_event.special_screen_obj);
                break;
            }
        }
    }

    /// Sets up the mood HUD object
    // tg: code/datums/mood.dm:357
    [SignalHandler]
    public virtual void modify_hud(datum source = null)
    {
        datum_hud hud = mob_parent.hud_used;
        atom_movable_screen_mood mood_screen_object = Waiting.NoSleep(hud.add_screen_object(typeof(atom_movable_screen_mood), HUD_MOB_MOOD, HUD_GROUP_INFO, update_screen: TRUE)) as atom_movable_screen_mood;
        mood_screen_object.color = "#4b96c4";
        RegisterSignal(hud, COMSIG_QDELETING, nameof(unmodify_hud));
        RegisterSignal(mood_screen_object, COMSIG_SCREEN_ELEMENT_CLICK, nameof(hud_click));
    }

    /// Removes the mood HUD object
    // tg: code/datums/mood.dm:367
    [SignalHandler]
    public virtual void unmodify_hud(datum source = null)
    {
        datum_hud hud = mob_parent.hud_used;
        if (!Dm.truthy(hud))
            return;

        atom_movable_screen_mood mood_screen_object = hud.screen_objects?[HUD_MOB_MOOD] as atom_movable_screen_mood;
        if (!Dm.truthy(mood_screen_object))
            return;

        QDEL_NULL(ref mood_screen_object);
        if (!QDELETED(hud))
            Waiting.NoSleep(hud.show_hud(hud.hud_version));
        UnregisterSignal(hud, COMSIG_QDELETING);
    }

    /// Handles clicking on the mood HUD object
    // tg: code/datums/mood.dm:384
    [SignalHandler]
    public virtual void hud_click(datum source = null, object location = null, object control = null, object @params = null, mob user = null)
    {
        if (user != mob_parent)
            return;
        if (IS_UNCONSCIOUS(user))
            return;
        print_mood(user);
    }

    /// Prints the users mood, sanity, and moodies to chat
    // tg: code/datums/mood.dm:394
    public virtual void print_mood(mob user = null)
    {
        string msg = I18n.T("[]<br>", span_info(I18n.T("<EM>My current mental status:</EM>")));

        if (!HAS_TRAIT(this, TRAIT_NOHUNGER)) // tg reads the trait off the mood datum, never off its mob (kept)
        {
            msg += span_notice(I18n.T("My hunger: "));
            float nutrition = mob_parent.nutrition;
            if (nutrition >= NUTRITION_LEVEL_FULL)
                msg += I18n.T("[]<br>", span_info(I18n.T("I'm completely stuffed!")));
            else if (nutrition >= NUTRITION_LEVEL_WELL_FED && nutrition <= NUTRITION_LEVEL_FULL)
                msg += I18n.T("[]<br>", span_info(I18n.T("I'm well fed!")));
            else if (nutrition >= NUTRITION_LEVEL_FED && nutrition <= NUTRITION_LEVEL_WELL_FED)
                msg += I18n.T("[]<br>", span_info(I18n.T("I'm not hungry.")));
            else if (nutrition >= NUTRITION_LEVEL_HUNGRY && nutrition <= NUTRITION_LEVEL_FED)
                msg += I18n.T("[]<br>", span_info(I18n.T("I could use a bite to eat.")));
            else if (nutrition >= NUTRITION_LEVEL_VERY_HUNGRY && nutrition <= NUTRITION_LEVEL_HUNGRY)
                msg += I18n.T("[]<br>", span_warning(I18n.T("I'm feeling hungry.")));
            else if (nutrition >= NUTRITION_LEVEL_STARVING && nutrition <= NUTRITION_LEVEL_VERY_HUNGRY)
                msg += I18n.T("[]<br>", span_warning(I18n.T("I feel quite hungry.")));
            else if (nutrition >= 0 && nutrition <= NUTRITION_LEVEL_STARVING)
                msg += I18n.T("[]<br>", span_boldwarning(I18n.T("I'm starving!")));
        }

        float drunkness = Dm.num(mob_parent.get_drunk_amount());
        if (drunkness >= 1)
        {
            msg += span_notice(I18n.T("My current drunkenness: "));
            if (drunkness >= 1 && drunkness <= 10)
                msg += I18n.T("[]<br>", span_info(I18n.T("I'm feeling a little tipsy.")));
            else if (drunkness >= 11 && drunkness <= 21)
                msg += I18n.T("[]<br>", span_info(I18n.T("I'm feeling a bit drunk.")));
            else if (drunkness >= 21 && drunkness <= 41)
                msg += I18n.T("[]<br>", span_info(I18n.T("I'm feeling quite drunk.")));
            else if (drunkness >= 41 && drunkness <= 61)
                msg += I18n.T("[]<br>", span_info(I18n.T("I'm feeling very drunk.")));
            else if (drunkness >= 61 && drunkness <= 81)
                msg += I18n.T("[]<br>", span_warning(I18n.T("I'm feeling like a mess.")));
            else if (drunkness >= 81)
                msg += I18n.T("[]<br>", span_boldwarning(I18n.T("I'm completely wasted.")));
        }

        if (HAS_TRAIT(mob_parent, TRAIT_APATHETIC))
            msg += span_notice(I18n.T("My mood: []<br>", span_grey(I18n.T("I don't feel anything."))));
        else
        {
            msg += span_notice(I18n.T("My current sanity: ")); //Long term
            if (sanity >= SANITY_GREAT)
                msg += I18n.T("[]<br>", span_boldnicegreen(I18n.T("My mind feels like a temple!")));
            else if (sanity >= SANITY_NEUTRAL && sanity <= SANITY_GREAT)
                msg += I18n.T("[]<br>", span_nicegreen(I18n.T("I have been feeling great lately!")));
            else if (sanity >= SANITY_DISTURBED && sanity <= SANITY_NEUTRAL)
                msg += I18n.T("[]<br>", span_nicegreen(I18n.T("I have felt quite decent lately.")));
            else if (sanity >= SANITY_UNSTABLE && sanity <= SANITY_DISTURBED)
                msg += I18n.T("[]<br>", span_warning(I18n.T("I'm feeling a little bit unhinged...")));
            else if (sanity >= SANITY_CRAZY && sanity <= SANITY_UNSTABLE)
                msg += I18n.T("[]<br>", span_warning(I18n.T("I'm freaking out!!")));
            else if (sanity >= SANITY_INSANE && sanity <= SANITY_CRAZY)
                msg += I18n.T("[]<br>", span_boldwarning(I18n.T("AHAHAHAHAHAHAHAHAHAH!!")));

            msg += span_notice(I18n.T("My current mood: ")); //Short term
            switch (mood_level)
            {
                case MOOD_LEVEL_SAD4:
                    msg += I18n.T("[]<br>", span_boldwarning(I18n.T("I wish I was dead!")));
                    break;
                case MOOD_LEVEL_SAD3:
                    msg += I18n.T("[]<br>", span_boldwarning(I18n.T("I feel terrible...")));
                    break;
                case MOOD_LEVEL_SAD2:
                    msg += I18n.T("[]<br>", span_boldwarning(I18n.T("I feel very upset.")));
                    break;
                case MOOD_LEVEL_SAD1:
                    msg += I18n.T("[]<br>", span_warning(I18n.T("I'm a bit sad.")));
                    break;
                case MOOD_LEVEL_NEUTRAL:
                    msg += I18n.T("[]<br>", span_grey(I18n.T("I'm alright.")));
                    break;
                case MOOD_LEVEL_HAPPY1:
                    msg += I18n.T("[]<br>", span_nicegreen(I18n.T("I feel pretty okay.")));
                    break;
                case MOOD_LEVEL_HAPPY2:
                    msg += I18n.T("[]<br>", span_boldnicegreen(I18n.T("I feel pretty good.")));
                    break;
                case MOOD_LEVEL_HAPPY3:
                    msg += I18n.T("[]<br>", span_boldnicegreen(I18n.T("I feel amazing!")));
                    break;
                case MOOD_LEVEL_HAPPY4:
                    msg += I18n.T("[]<br>", span_boldnicegreen(I18n.T("I love life!")));
                    break;
            }
        }

        DmList additional_lines = Dm.list();
        user.SEND_SIGNAL(COMSIG_CARBON_MOOD_CHECK, Dm.list(user, additional_lines));
        if (Dm.truthy(length(additional_lines)))
            msg += I18n.T("[]<br>", additional_lines.Join("<br>"));

        msg += I18n.T("[]<br>", span_notice(I18n.T("Moodlets:"))); //All moodlets
        if (Dm.truthy(mood_events.len))
        {
            foreach (object category in Dm.each(mood_events))
            {
                datum_mood_event @event = mood_events[category] as datum_mood_event;
                msg += "&bull; ";
                string description = Dm.str(@event.description);
                float sw__1 = @event.mood_change;
                if (sw__1 <= MOOD_SAD2)
                    msg += I18n.T("[]<br>", span_boldwarning(description));
                else if (sw__1 >= MOOD_SAD2 && sw__1 <= MOOD_SAD1)
                    msg += I18n.T("[]<br>", span_warning(description));
                else if (sw__1 >= MOOD_SAD1 && sw__1 <= MOOD_NEUTRAL)
                    msg += I18n.T("[]<br>", span_grey(description));
                else if (sw__1 >= MOOD_NEUTRAL && sw__1 <= MOOD_HAPPY1)
                    msg += I18n.T("[]<br>", span_info(description));
                else if (sw__1 >= MOOD_HAPPY1 && sw__1 <= MOOD_HAPPY2)
                    msg += I18n.T("[]<br>", span_nicegreen(description));
                else if (sw__1 >= MOOD_HAPPY2)
                    msg += I18n.T("[]<br>", span_boldnicegreen(description));
            }
        }
        else
            msg += I18n.T("&bull; []<br>", span_grey(I18n.T("I don't have much of a reaction to anything right now.")));

        if (Dm.truthy(LAZYLEN(mob_parent.personalities)))
            msg += span_notice(I18n.T("You know yourself to be [].<br>", mob_parent.get_parsonality_string()));

        if (Dm.truthy(LAZYLEN(mob_parent.quirks)))
            msg += span_notice(I18n.T("You have these quirks: [].", mob_parent.get_quirk_string(FALSE, CAT_QUIRK_ALL)));

        to_chat(user, boxed_message(msg));
    }

    /// Updates the mob's moodies, if the area provides a mood bonus
    // tg: code/datums/mood.dm:507
    [SignalHandler]
    public virtual void check_area_mood(datum source = null, area new_area = null)
    {
        RegisterSignal(new_area, COMSIG_AREA_BEAUTY_UPDATED, nameof(update_beauty));

        update_beauty(new_area);
        if (Dm.truthy(new_area.mood_bonus) && (!Dm.truthy(new_area.mood_trait) || HAS_TRAIT(source, new_area.mood_trait)))
            Waiting.NoSleep(add_mood_event("area", typeof(datum_mood_event_area), new_area.mood_bonus, new_area.mood_message));
        else
            clear_mood_event("area");
    }

    /// Updates the mob's given beauty moodie, based on the area
    // tg: code/datums/mood.dm:519
    [SignalHandler]
    public virtual void update_beauty(area area_to_beautify = null)
    {
        if (Dm.truthy(area_to_beautify.outdoors)) // if we're outside, we don't care
        {
            clear_mood_event(MOOD_CATEGORY_AREA_BEAUTY);
            return;
        }

        float beauty = area_to_beautify.beauty;
        if (HAS_MIND_TRAIT(mob_parent, TRAIT_MORBID))
        {
            if (HAS_TRAIT(mob_parent, TRAIT_SNOB))
            {
                if (beauty >= BEAUTY_LEVEL_DECENT && beauty <= BEAUTY_LEVEL_GOOD)
                {
                    Waiting.NoSleep(add_mood_event(MOOD_CATEGORY_AREA_BEAUTY, typeof(datum_mood_event_ehroom)));
                    return;
                }
                else if (beauty >= BEAUTY_LEVEL_GOOD && beauty <= BEAUTY_LEVEL_GREAT)
                {
                    Waiting.NoSleep(add_mood_event(MOOD_CATEGORY_AREA_BEAUTY, typeof(datum_mood_event_badroom)));
                    return;
                }
                else if (beauty >= BEAUTY_LEVEL_GREAT)
                {
                    Waiting.NoSleep(add_mood_event(MOOD_CATEGORY_AREA_BEAUTY, typeof(datum_mood_event_horridroom)));
                    return;
                }
            }
            if (beauty <= BEAUTY_LEVEL_HORRID)
                Waiting.NoSleep(add_mood_event(MOOD_CATEGORY_AREA_BEAUTY, typeof(datum_mood_event_greatroom)));
            else if (beauty >= BEAUTY_LEVEL_HORRID && beauty <= BEAUTY_LEVEL_BAD)
                Waiting.NoSleep(add_mood_event(MOOD_CATEGORY_AREA_BEAUTY, typeof(datum_mood_event_goodroom)));
            else if (beauty >= BEAUTY_LEVEL_BAD && beauty <= BEAUTY_LEVEL_DECENT)
                clear_mood_event(MOOD_CATEGORY_AREA_BEAUTY);
            return;
        }

        if (HAS_TRAIT(mob_parent, TRAIT_SNOB))
        {
            if (beauty <= BEAUTY_LEVEL_HORRID)
            {
                Waiting.NoSleep(add_mood_event(MOOD_CATEGORY_AREA_BEAUTY, typeof(datum_mood_event_horridroom)));
                return;
            }
            else if (beauty >= BEAUTY_LEVEL_HORRID && beauty <= BEAUTY_LEVEL_BAD)
            {
                Waiting.NoSleep(add_mood_event(MOOD_CATEGORY_AREA_BEAUTY, typeof(datum_mood_event_badroom)));
                return;
            }
        }
        if (beauty >= BEAUTY_LEVEL_BAD && beauty <= BEAUTY_LEVEL_DECENT)
            clear_mood_event(MOOD_CATEGORY_AREA_BEAUTY);
        else if (beauty >= BEAUTY_LEVEL_DECENT && beauty <= BEAUTY_LEVEL_GOOD)
            Waiting.NoSleep(add_mood_event(MOOD_CATEGORY_AREA_BEAUTY, typeof(datum_mood_event_decentroom)));
        else if (beauty >= BEAUTY_LEVEL_GOOD && beauty <= BEAUTY_LEVEL_GREAT)
            Waiting.NoSleep(add_mood_event(MOOD_CATEGORY_AREA_BEAUTY, typeof(datum_mood_event_goodroom)));
        else if (beauty >= BEAUTY_LEVEL_GREAT)
            Waiting.NoSleep(add_mood_event(MOOD_CATEGORY_AREA_BEAUTY, typeof(datum_mood_event_greatroom)));
    }

    // tg: code/datums/mood.dm:564
    [SignalHandler]
    public virtual void exit_area(datum source = null, area old_area = null)
    {
        UnregisterSignal(old_area, COMSIG_AREA_BEAUTY_UPDATED);
    }

    /// Called when parent is ahealed.
    // tg: code/datums/mood.dm:569
    [SignalHandler]
    public virtual void on_revive(datum source = null, object full_heal = null)
    {
        if (!Dm.truthy(full_heal))
            return;
        remove_temp_moods();
        Waiting.NoSleep(set_sanity(Dm.initial(this, (datum_mood p) => p.sanity), @override: TRUE));
    }

    /// Sets sanity to the specified amount and applies effects.
    // tg: code/datums/mood.dm:578
    public virtual async DmTask set_sanity(object amount = null, float? minimum = null, float? maximum = null, float? @override = null)
    {
        minimum ??= SANITY_INSANE;
        maximum ??= SANITY_GREAT;
        @override ??= FALSE;
        float new_amount = Dm.num(amount);
        // If we're out of the acceptable minimum-maximum range move back towards it in steps of 0.7
        // If the new amount would move towards the acceptable range faster then use it instead
        if (new_amount < minimum && sanity < minimum)
            new_amount = sanity + 0.7f;

        if (!Dm.truthy(@override) && HAS_TRAIT(mob_parent, TRAIT_UNSTABLE))
            new_amount = min(sanity, new_amount);

        if (new_amount > maximum)
            new_amount = min(new_amount, Dm.num(maximum));

        if (HAS_TRAIT(mob_parent, TRAIT_APATHETIC))
            new_amount = SANITY_NEUTRAL;

        if (new_amount == sanity) //Prevents stuff from flicking around.
            return;

        sanity = new_amount;
        mob_parent.SEND_SIGNAL(COMSIG_CARBON_SANITY_UPDATE, Dm.list(mob_parent, new_amount));
        if (sanity >= SANITY_INSANE && sanity <= SANITY_CRAZY)
            sanity_level = SANITY_LEVEL_INSANE;
        else if (sanity >= SANITY_CRAZY && sanity <= SANITY_UNSTABLE)
            sanity_level = SANITY_LEVEL_CRAZY;
        else if (sanity >= SANITY_UNSTABLE && sanity <= SANITY_DISTURBED)
            sanity_level = SANITY_LEVEL_UNSTABLE;
        else if (sanity >= SANITY_DISTURBED && sanity <= SANITY_NEUTRAL)
            sanity_level = SANITY_LEVEL_DISTURBED;
        else if (sanity >= SANITY_NEUTRAL + 1 && sanity <= SANITY_GREAT + 1) //shitty hack but +1 to prevent it from responding to super small differences
            sanity_level = SANITY_LEVEL_NEUTRAL;
        else if (sanity >= SANITY_GREAT + 1)
            sanity_level = SANITY_LEVEL_GREAT;

        set_crit_threshold();
        await set_movespeed_effect();
        await set_actionspeed_effect();

        // Crazy or insane = add some uncommon hallucinations
        if (sanity_level >= SANITY_LEVEL_CRAZY)
            await mob_parent.apply_status_effect(typeof(datum_status_effect_hallucination_sanity));
        else
            mob_parent.remove_status_effect(typeof(datum_status_effect_hallucination_sanity));

        update_mood_icon();
    }

    /// Sets sanity to a specific amount, useful for callbacks
    // tg: code/datums/mood.dm:625
    public virtual async DmTask reset_sanity(object amount = null)
    {
        await set_sanity(amount, @override: TRUE);
    }

    /// Adjusts sanity by a value
    // tg: code/datums/mood.dm:629
    public virtual async DmTask adjust_sanity(object amount = null, float? minimum = null, float? maximum = null, float? @override = null)
    {
        minimum ??= SANITY_INSANE;
        maximum ??= SANITY_GREAT;
        @override ??= FALSE;
        await set_sanity(sanity + Dm.num(amount), minimum, maximum, @override);
    }

    /// Sets the crit threshold of the mob
    // tg: code/datums/mood.dm:633
    public virtual void set_crit_threshold()
    {
        float newval = 0;
        switch (sanity_level)
        {
            case SANITY_LEVEL_INSANE:
                newval = MAJOR_INSANITY_PEN;
                break;
            case SANITY_LEVEL_CRAZY:
                newval = MINOR_INSANITY_PEN;
                break;
        }
        if (newval == insanity_effect)
            return;
        mob_parent.crit_threshold = (mob_parent.crit_threshold - insanity_effect) + newval;
        insanity_effect = newval;
    }

    // tg: code/datums/mood.dm:645
    public virtual async DmTask set_movespeed_effect()
    {
        switch (sanity_level)
        {
            case SANITY_LEVEL_INSANE:
                await mob_parent.add_movespeed_modifier(typeof(datum_movespeed_modifier_sanity_insane));
                break;
            case SANITY_LEVEL_CRAZY:
                await mob_parent.add_movespeed_modifier(typeof(datum_movespeed_modifier_sanity_crazy));
                break;
            case SANITY_LEVEL_UNSTABLE:
                await mob_parent.add_movespeed_modifier(typeof(datum_movespeed_modifier_sanity_disturbed));
                break;
            default:
                mob_parent.remove_movespeed_modifier(MOVESPEED_ID_SANITY);
                break;
        }
    }

    // tg: code/datums/mood.dm:656
    public virtual async DmTask set_actionspeed_effect()
    {
        switch (sanity_level)
        {
            case SANITY_LEVEL_INSANE or SANITY_LEVEL_CRAZY or SANITY_LEVEL_UNSTABLE:
                await mob_parent.add_actionspeed_modifier(typeof(datum_actionspeed_modifier_low_sanity));
                break;
            case SANITY_LEVEL_NEUTRAL or SANITY_LEVEL_GREAT:
                await mob_parent.add_actionspeed_modifier(typeof(datum_actionspeed_modifier_high_sanity));
                break;
            default:
                mob_parent.remove_actionspeed_modifier(ACTIONSPEED_ID_SANITY);
                break;
        }
    }

    /// Removes all temporary moods
    // tg: code/datums/mood.dm:666
    public virtual void remove_temp_moods()
    {
        foreach (object category in Dm.each(mood_events))
        {
            datum_mood_event moodlet = mood_events[category] as datum_mood_event;
            if (!Dm.truthy(moodlet) || !Dm.truthy(moodlet.timeout))
                continue;
            Dm.remove(ref mood_events, moodlet.category);
            qdel(moodlet);
        }
        update_mood();
    }

    /// Helper to forcefully drain sanity
    // tg: code/datums/mood.dm:676
    public virtual async DmTask direct_sanity_drain(object amount = null)
    {
        await adjust_sanity(amount, @override: TRUE);
    }

    /**
     * Returns true if you already have a mood from a provided category.
     *
     * Arguments
     * * category - Mood category to validate against.
     */
    // tg: code/datums/mood.dm:685
    public virtual object has_mood_of_category(object category = null)
    {
        foreach (object i in Dm.each(mood_events))
        {
            datum_mood_event moodlet = mood_events[i] as datum_mood_event;
            if (Dm.eq(moodlet.category, category))
                return TRUE;
        }
        return FALSE;
    }
}

// tg: code/datums/mood.dm:695
public partial class datum_mood_dummy : datum_mood
{
    // tg: code/datums/mood.dm:697
    public override async DmTask set_sanity(object amount = null, float? minimum = null, float? maximum = null, float? @override = null)
    {
        return;
    }
}
