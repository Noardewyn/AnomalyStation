// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/mob_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only lowest_buckled_mob (the M7 projectiles slice), get_realname_string and is_antag (/mob's root; M1 P1: the player lists of mob_lists.dm call it at every
// Login and Logout), check_zone, ran_zone and get_random_valid_zone (M2 P2: the carbon damage procs' zone choice), and
// findname (M2 F1: a unique random name) and shake_camera (M5x slice 1: an explosion's shake; the client's animate path goes
// to the presentation's animate sink, which protocol 21 hands to the player's camera) and default_lighting_cutoff (the
// ghosts, 2026-10-16), stars (M9 slice A) and Gibberish (M9 slice D: the radio), get_player_client (syndicate A1), health_percentage (syndicate X2), the cyborg's is_antag and notify_ghosts (silicons SI2), copy_recent_speech (gaps P3: the mind reader), stifled (gaps SL1: cuffed sign language) and get_path_by_slot (gaps MO2: the MOD chameleon module) and is_touching_rust (the rust healing element) and isAdminObserver (the chapel's altar) translated; the rest of the helpers come with their callers.

// tg: code/modules/mob/mob_helpers.dm:4 (interleaved types, rules 1.9)
public partial class mob : atom_movable
{
    /// Assumes that the action will be initialized and held in the mob itself, which is typically standard.
    // tg: code/modules/mob/mob_helpers.dm:578
    public virtual void grant_actions_by_list(DmList input = null)
    {
        if (length(input) <= 0)
            return;

        foreach (object action in Dm.each<object>(input))
        {
            datum_action ability = Dm.@new((Type)action, this) as datum_action;
            Waiting.NoSleep(ability.Grant(this));

            object blackboard_key = input[action];
            if (blackboard_key == null)
                continue;

            (ai_controller as datum_ai_controller)?.set_blackboard_key(blackboard_key, ability);
        }
    }

    // tg: code/modules/mob/mob_helpers.dm:4  (the M7 projectiles slice: a projectile that hits prone targets, can_hit_target)
    public virtual object lowest_buckled_mob()
    {
        object dot = null;
        dot = this;
        if (Dm.truthy(buckled) && ismob(buckled))
        {
            mob Buckled = buckled as mob;
            dot = Buckled.lowest_buckled_mob();
        }
        return dot;
    }
}

// tg: code/modules/mob/mob_helpers.dm:11 (global procs; interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    ///Is the passed in mob a ghost with admin powers, doesn't check for AI interact like isAdminGhost() used to
    // tg: code/modules/mob/mob_helpers.dm:343
    public static object isAdminObserver(mob user = null)
    {
        if (user == null) //Are they a mob? Auto interface updates call this with a null src
            return null;
        if (user.client == null) // Do they have a client?
            return null;
        if (!isobserver(user)) // Are they a ghost?
            return null;
        if (!Dm.truthy(check_rights_for(user.client, R_ADMIN))) // Are they allowed?
            return null;
        return TRUE;
    }

    /**
     * Convert random parts of a passed in message to stars
     *
     * * phrase - the string to convert
     * * probability - probability any character gets changed
     *
     * This proc is dangerously laggy, avoid it or die
     */
    // tg: code/modules/mob/mob_helpers.dm:103
    public static object stars(object phrase = null, float? probability = null)
    {
        probability ??= 25;
        if (probability <= 0)
            return phrase;
        phrase = Dm.html_decode(phrase);
        float leng = Dm.num(length(phrase));
        object dot = "";
        string @char = "";
        for (float i = 1; i <= leng; i += Dm.num(length(@char)))
        {
            @char = Dm.textchar(phrase, i);
            if (@char == " " || !Dm.prob(probability.Value))
                dot = Dm.text("[][]", dot, @char);
            else
                dot = Dm.text("[]*", dot);
        }
        return sanitize(dot);
    }

    /**
     * For when you're only able to speak a limited amount of words
     * phrase - the string to convert
     * definitive_limit - the amount of words to limit the phrase to, optional
    */
    // tg: code/modules/mob/mob_helpers.dm:123
    public static object stifled(object phrase = null, object definitive_limit = null)
    {
        phrase = Dm.html_decode(phrase);
        float num_words = 0;
        DmList words = splittext(phrase, " ");
        if (Dm.gt(definitive_limit, 0)) // in case someone passes a negative
            num_words = min(Dm.num(definitive_limit), length(words));
        else
            num_words = min(rand(3, 5), length(words));
        object dot = "";
        for (float i = 1; i <= num_words; i++)
        {
            if (num_words == i)
                dot = Dm.text("[][]", dot, Dm.add(words[i], "..."));
            else
                dot = Dm.text("[][]", dot, Dm.add(words[i], " ... "));
        }
        return sanitize(dot);
    }

    /**
     * Turn text into complete gibberish!
     *
     * text is the inputted message, replace_characters will cause original letters to be replaced and chance are the odds that a character gets modified.
     */
    // tg: code/modules/mob/mob_helpers.dm:144  (M9 slice D: the radio's compression)
    public static object Gibberish(object text = null, float? replace_characters = null, float? chance = null)
    {
        replace_characters ??= FALSE;
        chance ??= 50;
        text = Dm.html_decode(text);
        object dot = "";
        string rawchar = "";
        string letter = "";
        float lentext = Dm.num(length(text));
        for (float i = 1; i <= lentext; i += Dm.num(length(rawchar)))
        {
            rawchar = letter = Dm.textchar(text, i);
            if (Dm.prob(chance.Value))
            {
                if (Dm.truthy(replace_characters))
                    letter = "";
                for (float j = 1, j__end = Dm.rand(0, 2); j <= j__end; j++)
                    letter = Dm.text("[][]", letter, Dm.pick("#", "@", "*", "&", "%", "$", "/", "<", ">", ";", "*", "*", "*", "*", "*", "*", "*"));
            }
            dot = Dm.text("[][]", dot, letter);
        }
        return sanitize(dot);
    }

    ///Convert a PRECISE ZONE into the BODY_ZONE
    // tg: code/modules/mob/mob_helpers.dm:11
    public static object check_zone(object zone = null)
    {
        if (!Dm.truthy(zone))
            return BODY_ZONE_CHEST;
        object sw__1 = zone;
        if (Dm.eq(sw__1, BODY_ZONE_PRECISE_EYES))
            zone = BODY_ZONE_HEAD;
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_MOUTH))
            zone = BODY_ZONE_HEAD;
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_L_HAND))
            zone = BODY_ZONE_L_ARM;
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_R_HAND))
            zone = BODY_ZONE_R_ARM;
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_L_FOOT))
            zone = BODY_ZONE_L_LEG;
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_R_FOOT))
            zone = BODY_ZONE_R_LEG;
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_GROIN))
            zone = BODY_ZONE_CHEST;
        return zone;
    }

    /**
     * Return the zone or randomly, another valid zone
     *
     * probability controls the chance it chooses the passed in zone, or another random zone
     * defaults to 80
     */
    // tg: code/modules/mob/mob_helpers.dm:39
    public static object ran_zone(object zone = null, float? probability = null, DmList weighted_list = null)
    {
        probability ??= 80;
        if (prob(Dm.num(probability)))
            zone = check_zone(zone);
        else
            zone = pick_weight(Dm.truthy(weighted_list) ? weighted_list : Dm.list(Dm.kv(BODY_ZONE_HEAD, 1), Dm.kv(BODY_ZONE_CHEST, 1), Dm.kv(BODY_ZONE_L_ARM, 4), Dm.kv(BODY_ZONE_R_ARM, 4), Dm.kv(BODY_ZONE_L_LEG, 4), Dm.kv(BODY_ZONE_R_LEG, 4)));
        return zone;
    }
}

// tg: code/modules/mob/mob_helpers.dm:60 (interleaved types, rules 1.9)
public partial class mob : atom_movable
{
    /**
     * More or less ran_zone, but only returns bodyzones that the mob /actually/ has.
     *
     * * blacklisted_parts - allows you to specify zones that will not be chosen. eg: list(BODY_ZONE_CHEST, BODY_ZONE_R_LEG)
     * * * !!!! blacklisting BODY_ZONE_CHEST is really risky since it's the only bodypart guarunteed to ALWAYS exists  !!!!
     * * * !!!! Only do that if you're REALLY CERTAIN they have limbs, otherwise we'll CRASH() !!!!
     *
     * * ran_zone has a base prob(80) to return the base_zone (or if null, BODY_ZONE_CHEST) vs something in our generated list of limbs.
     * * this probability is overriden when either blacklisted_parts contains BODY_ZONE_CHEST and we aren't passed a base_zone (since the default fallback for ran_zone would be the chest in that scenario), or if even_weights is enabled.
     * * you can also manually adjust this probability by altering base_probability
     *
     * * even_weights - ran_zone has a 40% chance (after the prob(80) mentioned above) of picking a limb, vs the torso & head which have an additional 10% chance.
     * * Setting even_weight to TRUE will make it just a straight up pick() between all possible bodyparts.
     *
     */
    // tg: code/modules/mob/mob_helpers.dm:60
    public virtual object get_random_valid_zone(object base_zone = null, float? base_probability = null, DmList blacklisted_parts = null, object even_weights = null, object bypass_warning = null)
    {
        base_probability ??= 80;
        return BODY_ZONE_CHEST; //even though they don't really have a chest, let's just pass the default of check_zone to be safe.
    }
}

// tg: code/modules/mob/mob_helpers.dm:63 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/mob_helpers.dm:63
    public override object get_random_valid_zone(object base_zone = null, float? base_probability = null, DmList blacklisted_parts = null, object even_weights = null, object bypass_warning = null)
    {
        base_probability ??= 80;
        DmList limbs = Dm.list();
        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            object limb_zone = part.body_zone; //cache the zone since we're gonna check it a ton.
            if (Dm.@in(limb_zone, blacklisted_parts))
                continue;
            if (Dm.truthy(even_weights))
            {
                limbs[limb_zone] = 1;
                continue;
            }
            if (Dm.eq(limb_zone, BODY_ZONE_CHEST) || Dm.eq(limb_zone, BODY_ZONE_HEAD))
                limbs[limb_zone] = 1;
            else
                limbs[limb_zone] = 4;
        }

        if (Dm.truthy(base_zone) && !Dm.@in(check_zone(base_zone), limbs))
            base_zone = null; //check if the passed zone is infact valid

        object chest_blacklisted = null;
        if ((Dm.@in(BODY_ZONE_CHEST, blacklisted_parts)))
        {
            chest_blacklisted = TRUE;
            if (Dm.truthy(bypass_warning) && !Dm.truthy(limbs.len))
                throw Dm.CRASH("limbs is empty and the chest is blacklisted. this may not be intended!", "code/modules/mob/mob_helpers.dm", 81);
        }
        return (((Dm.truthy(chest_blacklisted) && !Dm.truthy(base_zone)) || Dm.truthy(even_weights)) ? pick_weight(limbs) : ran_zone(base_zone, base_probability, limbs));
    }
}

public static partial class GlobalProcs
{
    // DEVIATION(DV592): a melee attack aimed at a limb the target has lost does not start (tg would hit the chest or another limb);
    // the attacker is told which limb is missing.
    public static bool aimed_limb_missing(mob user, atom target)
    {
        if (user == null || target is not mob_living_carbon carbon)
            return false;
        object zone = check_zone(user.zone_selected);
        if (Dm.truthy(carbon.get_bodypart(Dm.str(zone))))
            return false;
        carbon.balloon_alert(user, I18n.T("no []!", parse_zone(zone)));
        return true;
    }
}

// The [DmPath] of /mob is on its engine part Builtin/mob.builtin.cs (rules 2.3).
public partial class mob : atom_movable
{
    // tg: code/modules/mob/mob_helpers.dm:429
    public virtual object can_see_reagents()
    {
        return Dm.eq(stat, DEAD) || HAS_TRAIT(this, TRAIT_REAGENT_SCANNER) ? TRUE : FALSE;
    }

    ///Returns a mob's real name between brackets. Useful when you want to display a mob's name alongside their real name
    // tg: code/modules/mob/mob_helpers.dm:247
    public virtual object get_realname_string()
    {
        if (Dm.truthy(real_name) && !Dm.eq(real_name, name))
            return Dm.text(" \\[[]\\]", real_name);
        return "";
    }

    /**
     * Checks if this mob is an antag
     * By default excludes antags like Valentines, which are "fake antags"
     */
    // tg: code/modules/mob/mob_helpers.dm:256
    public virtual object is_antag(float? blacklisted_antag_flags = null)
    {
        blacklisted_antag_flags ??= ANTAG_FAKE;
        foreach (datum_antagonist antag_datum in Dm.each<datum_antagonist>(mind?.antag_datums))
        {
            if (!Dm.truthy(blacklisted_antag_flags) || !Dm.truthy(Dm.bitand(antag_datum.antag_flags, blacklisted_antag_flags)))
                return TRUE;
        }

        return FALSE;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/mob/mob_helpers.dm:160  #define TILES_PER_SECOND 0.7 (file-local, #undef at :209)
    private const float TILES_PER_SECOND = 0.7f;

    ///Shake the camera of the person viewing the mob SO REAL!
    ///Takes the mob to shake, the time span to shake for, and the amount of tiles we're allowed to shake by in tiles
    ///Duration isn't taken as a strict limit, since we don't trust our coders to not make things feel shitty. So it's more like a soft cap.
    // tg: code/modules/mob/mob_helpers.dm:164  (the steps animate the client's pixel_x/y: the shim applies them to the client's
    // vars and reports them to the animate sink, IAppearanceSink.AnimateStep, which the 3D client's camera follows)
    public static void shake_camera(mob M = null, object duration = null, float? strength = null)
    {
        strength ??= 1;
        if (!Dm.truthy(M) || !Dm.truthy(M.client) || Dm.num(duration) < 1)
            return;
        client C = M.client;
        float oldx = C.pixel_x;
        float oldy = C.pixel_y;
        float max_x = strength.Value * ICON_SIZE_X;
        float max_y = strength.Value * ICON_SIZE_Y;
        float min_x = -(strength.Value * ICON_SIZE_X);
        float min_y = -(strength.Value * ICON_SIZE_Y);

        // GAP(lead, modules/client/preferences.cs): C.prefs?.read_preference(/datum/preference/toggle/screen_shake_darken):
        // /datum/preferences/read_preference is not translated and the client's prefs stay null until the M9 preferences
        // (client_procs.cs:105), so the null-conditional read is null.
        object screen_shake_darken__gap = null;
        if (Dm.truthy(screen_shake_darken__gap))
        {
            Type type = typeof(atom_movable_screen_fullscreen_flash_black);
            float shake_dur = max(Dm.num(duration), 2 * SECONDS);

            M.overlay_fullscreen("flash", type);
            _addtimer(Dm.@new<datum_callback>(M, nameof(mob.clear_fullscreen), "flash", 1 * SECONDS), shake_dur, file: "code/modules/mob/mob_helpers.dm", line: 179);
            return;
        }

        // DEVIATION(DV96): the chain that follows is a shake (the replicator sends the client's last chain with its kind)
        C.dm_camera_recoil = false;

        //How much time to allot for each pixel moved
        float time_scalar = (1f / ICON_SIZE_ALL) * TILES_PER_SECOND;
        float last_x = oldx;
        float last_y = oldy;

        float time_spent = 0;
        while (time_spent < Dm.num(duration))
        {
            //Get a random pos in our box
            float x_pos = rand(min_x, max_x) + oldx;
            float y_pos = rand(min_y, max_y) + oldy;

            //We take the smaller of our two distances so things still have the propencity to feel somewhat jerky
            float time = round(max(min(abs(last_x - x_pos), abs(last_y - y_pos)) * time_scalar, 1));

            if (time_spent == 0)
                animate(C, Dm.named("pixel_x", x_pos), Dm.named("pixel_y", y_pos), Dm.named("time", time));
            else
                animate(Dm.named("pixel_x", x_pos), Dm.named("pixel_y", y_pos), Dm.named("time", time));

            last_x = x_pos;
            last_y = y_pos;
            //We go based on time spent, so there is a chance we'll overshoot our duration. Don't care
            time_spent += time;
        }
        animate(Dm.named("pixel_x", oldx), Dm.named("pixel_y", oldy), Dm.named("time", 3));
    }

    /// Helper proc for a similar function to shake_camera, expect this one kicks the camera back at an opposite angle rather then skake_camera's irratic jittering.
    // tg: code/modules/mob/mob_helpers.dm:212  (M7x slice A: the steps animate the client's pixel_x/y as shake_camera's do; the
    // replicator sends the chain as a CameraShake of kind Recoil, which the first-person client turns into a camera rotation, DV96)
    public static void recoil_camera(mob recoiled_mob = null, object duration = null, object backtime_duration = null, object strength = null, object angle = null)
    {
        if (!Dm.truthy(recoiled_mob) || !Dm.truthy(recoiled_mob.client) || Dm.num(duration) < 1)
            return;
        client my_client = recoiled_mob.client;
        strength = Dm.num(strength) * ICON_SIZE_ALL; // world.icon_size: 32, tg's world.dm
        client client_to_shake = recoiled_mob.client;
        float oldx = client_to_shake.pixel_x;
        float oldy = client_to_shake.pixel_y;

        //get pixels to move the camera in an angle
        float mpx = sin(Dm.num(angle)) * Dm.num(strength);
        float mpy = cos(Dm.num(angle)) * Dm.num(strength);

        // GAP(lead, modules/client/preferences.cs): my_client.prefs?.read_preference(/datum/preference/toggle/screen_shake_darken):
        // the client's prefs stay null until the M9 preferences (as in shake_camera), so the null-conditional read is null.
        object screen_shake_darken__gap = null;
        if (Dm.truthy(screen_shake_darken__gap))
        {
            Type type = typeof(atom_movable_screen_fullscreen_flash_black);
            float shake_dur = max(Dm.num(duration), 2 * SECONDS);
            recoiled_mob.overlay_fullscreen("flash", type);
            _addtimer(Dm.@new<datum_callback>(recoiled_mob, nameof(mob.clear_fullscreen), "flash", 1 * SECONDS), shake_dur, file: "code/modules/mob/mob_helpers.dm", line: 230);
            return;
        }

        // DEVIATION(DV96): the chain that follows is a recoil kick
        client_to_shake.dm_camera_recoil = true;
        animate(client_to_shake, Dm.named("pixel_x", oldx + mpx), Dm.named("pixel_y", oldy + mpy), Dm.named("time", duration), Dm.named("flags", ANIMATION_RELATIVE));
        animate(Dm.named("pixel_x", oldx), Dm.named("pixel_y", oldy), Dm.named("time", backtime_duration), Dm.named("easing", BACK_EASING));
    }

    ///Find if the message has the real name of any user mob in the mob_list
    // tg: code/modules/mob/mob_helpers.dm:237
    public static object findname(object msg = null)
    {
        if (!istext(msg))
            msg = Dm.text("[]", msg);
        foreach (object i in Dm.each(GLOB.mob_list))
        {
            mob M = i as mob;
            if (Dm.eq(M.real_name, msg))
                return M;
        }
        return 0;
    }
}

// tg: code/modules/mob/mob_helpers.dm:433
public partial class mob : atom_movable
{
    ///Can this mob hold items
    // tg: code/modules/mob/mob_helpers.dm:433
    public virtual object can_hold_items(obj_item I = null)
    {
        return length(held_items);
    }

    /// Returns this mob's default lighting alpha
    // tg: code/modules/mob/mob_helpers.dm:437  (the ghosts, 2026-10-16: the observer's Login sets its lighting cutoff from it)
    public virtual object default_lighting_cutoff()
    {
        if (Dm.truthy(client?.combo_hud_enabled) && Dm.truthy(Dm.bitand(client?.prefs?.toggles, COMBOHUD_LIGHTING)))
            return LIGHTING_CUTOFF_FULLBRIGHT;
        return Dm.initial(this, (mob p) => p.lighting_cutoff);
    }
}

public static partial class GlobalProcs
{
    /// Returns a client from a mob, mind or client
    // tg: code/modules/mob/mob_helpers.dm:482
    public static object get_player_client(object player = null)
    {
        if (ismob(player))
        {
            mob player_mob = (mob)player;
            player = player_mob.client;
        }
        else if (istype(player, typeof(datum_mind)))
        {
            datum_mind player_mind = (datum_mind)player;
            player = player_mind.current.client;
        }
        if (!istype(player, typeof(client)))
            return null;
        return player;
    }

    // tg: code/modules/mob/mob_helpers.dm:493  (syndicate X2: a guardian's health display shows its summoner's)
    public static float health_percentage(mob_living mob = null)
    {
        float divided_health = mob.health / mob.maxHealth;
        if (iscyborg(mob) || islarva(mob))
            divided_health = (mob.health + mob.maxHealth) / (mob.maxHealth * 2);
        else if (iscarbon(mob) || isAI(mob) || isbrain(mob))
            divided_health = Math.Abs(mob.dead_threshold - mob.health) / Math.Abs(mob.dead_threshold - mob.maxHealth);
        return divided_health * 100;
    }

    /// Returns a generic path of the object based on the slot
    // tg: code/modules/mob/mob_helpers.dm:443  (gaps MO2: the MOD chameleon module's disguises)
    public static Type get_path_by_slot(object slot_id = null)
    {
        switch (Dm.num(slot_id))
        {
            case ITEM_SLOT_BACK:
                return typeof(obj_item_storage_backpack);
            case ITEM_SLOT_MASK:
                return typeof(obj_item_clothing_mask);
            case ITEM_SLOT_NECK:
                return typeof(obj_item_clothing_neck);
            case ITEM_SLOT_HANDCUFFED:
                return typeof(obj_item_restraints_handcuffs);
            case ITEM_SLOT_LEGCUFFED:
                return typeof(obj_item_restraints_legcuffs);
            case ITEM_SLOT_BELT:
                return typeof(obj_item_storage_belt);
            case ITEM_SLOT_ID:
                return typeof(obj_item_card_id_advanced);
            case ITEM_SLOT_EARS:
                return typeof(obj_item_clothing_ears);
            case ITEM_SLOT_EYES:
                return typeof(obj_item_clothing_glasses);
            case ITEM_SLOT_GLOVES:
                return typeof(obj_item_clothing_gloves);
            case ITEM_SLOT_HEAD:
                return typeof(obj_item_clothing_head);
            case ITEM_SLOT_FEET:
                return typeof(obj_item_clothing_shoes);
            case ITEM_SLOT_OCLOTHING:
                return typeof(obj_item_clothing_suit);
            case ITEM_SLOT_ICLOTHING:
                return typeof(obj_item_clothing_under);
            case ITEM_SLOT_LPOCKET:
                return typeof(obj_item);
            case ITEM_SLOT_RPOCKET:
                return typeof(obj_item);
            case ITEM_SLOT_SUITSTORE:
                return typeof(obj_item);
        }
        return null;
    }
}

// tg: code/modules/mob/mob_helpers.dm:263
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/modules/mob/mob_helpers.dm:263
    public override object is_antag(float? blacklisted_antag_flags = null)
    {
        return FALSE;
    }
}

// tg: code/modules/mob/mob_helpers.dm:285 (silicons SI2: the positronic brain's call for a ghost)
public static partial class GlobalProcs
{
    /**
     * Fancy notifications for ghosts
     *
     * The kitchen sink of notification procs
     *
     * Arguments:
     * * message: The message displayed in chat.
     * * source: The source of the notification. This is required for an icon
     * * header: The title text to display on the icon tooltip.
     * * alert_overlay: Optional. Create a custom overlay if you'd like, otherwise it will use the source
     * * click_interact: If true, adds a link + clicking the icon will attack_ghost the source
     * * custom_link: Optional. If you want to add a custom link to the chat notification
     * * ghost_sound: sound to play
     * * ignore_key: Ignore keys if they're in the GLOB.poll_ignore list
     * * notify_volume: How loud the sound should be to spook the user
     */
    // tg: code/modules/mob/mob_helpers.dm:285
    public static void notify_ghosts(object message = null, atom source = null, string header = null, mutable_appearance alert_overlay = null, float? click_interact = null, string custom_link = null, object ghost_sound = null, object ignore_key = null, float? notify_flags = null, float? notify_volume = null)
    {
        header ??= "Something Interesting!";
        click_interact ??= FALSE;
        custom_link ??= "";
        notify_flags ??= NOTIFY_CATEGORY_DEFAULT;
        notify_volume ??= 100;

        if (Dm.truthy(Dm.bitand(notify_flags, GHOST_NOTIFY_IGNORE_MAPLOAD)) && !Dm.eq(SSatoms.initialized, INITIALIZATION_INNEW_REGULAR)) //don't notify for objects created during a map load
            return;

        if (source != null)
        {
            if (alert_overlay == null)
                alert_overlay = get_small_overlay(source) as mutable_appearance;

            alert_overlay.appearance_flags = Dm.bitor(alert_overlay.appearance_flags, TILE_BOUND);
            alert_overlay.layer = FLOAT_LAYER;
            alert_overlay.plane = FLOAT_PLANE;
        }

        foreach (mob_dead_observer ghost in Dm.each<mob_dead_observer>(GLOB.player_list))
        {
            if (!Dm.truthy(Dm.bitand(notify_flags, GHOST_NOTIFY_NOTIFY_SUICIDERS)) && HAS_TRAIT(ghost, TRAIT_SUICIDED))
                continue;
            if (Dm.truthy(ignore_key) && Dm.@in(ghost.ckey, GLOB.poll_ignore[ignore_key]))
                continue;

            if (Dm.truthy(Dm.bitand(notify_flags, GHOST_NOTIFY_FLASH_WINDOW)))
                window_flash(ghost.client);

            if (Dm.truthy(ghost_sound))
                Dm.output_to(ghost, sound(ghost_sound, volume: notify_volume)); // SEND_SOUND

            if (source == null)
            {
                to_chat(ghost, span_ghostalert(Dm.text("[]", message)));
                continue;
            }

            string interact_link = Dm.truthy(click_interact) ? Dm.text(" <a href='byond://?src=[];play=[]'>([])</a>", REF(ghost), REF(source), I18n.T("Play")) : "";
            string view_link = Dm.text(" <a href='byond://?src=[];view=[]'>([])</a>", REF(ghost), REF(source), I18n.T("View"));

            to_chat(ghost, span_ghostalert(Dm.text("[][][][]", message, custom_link, interact_link, view_link)));

            atom_movable_screen_alert_notify_action toast = ghost.throw_alert(
                category: Dm.text("[]_notify_action", REF(source)),
                type: typeof(atom_movable_screen_alert_notify_action)
            ) as atom_movable_screen_alert_notify_action;
            toast.add_overlay(alert_overlay);
            toast.click_interact = Dm.num(click_interact);
            toast.desc = Dm.truthy(click_interact) ? I18n.T("Click to play.") : I18n.T("Click to view.");
            toast.name = header;
            toast.target_ref = WEAKREF(source) as datum_weakref;
        }
    }
}

// tg: code/modules/mob/mob_helpers.dm:265  (silicons SI4)
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/modules/mob/mob_helpers.dm:265
    public override object is_antag(float? blacklisted_antag_flags = null)
    {
        return (Dm.truthy(base.is_antag(blacklisted_antag_flags)) && Dm.truthy(laws?.zeroth)) ? TRUE : FALSE; // AIs only count as antags if they have a zeroth law (apparently)
    }
}

// tg: code/modules/mob/mob_helpers.dm:549 (interleaved types, rules 1.9; gaps P3, D327: the mind reader's and the changelings' recent speech)
public partial class mob : atom_movable
{
    // tg: code/modules/mob/mob_helpers.dm:549
    public virtual DmList copy_recent_speech(object copy_amount = null, object line_chance = null)
    {
        copy_amount ??= LING_ABSORB_RECENT_SPEECH;
        line_chance ??= 100;
        DmList recent_speech = Dm.list();
        DmList say_log = Dm.list();
        DmList log_source = logging;
        foreach (object log_type in Dm.each(log_source))
        {
            float nlog_type = Dm.num(text2num(log_type));
            if (Dm.truthy(Dm.bitand(nlog_type, LOG_SAY)))
            {
                if (log_source[log_type] is DmList reversed)
                {
                    say_log = reverse_range(reversed.Copy()) as DmList;
                    break;
                }
            }
        }

        foreach (object spoken_memory in Dm.each(say_log))
        {
            if (length(recent_speech) >= Dm.num(copy_amount))
                break;
            if (!prob(Dm.num(line_chance)))
                continue;
            recent_speech[spoken_memory] = splittext(say_log[spoken_memory], "\"", 1, 0, TRUE)[3];
        }

        DmList raw_lines = Dm.list();
        foreach (object key in Dm.each(recent_speech))
            Dm.append(ref raw_lines, recent_speech[key]);
        return raw_lines;
    }
}

// tg: code/modules/mob/mob_helpers.dm:593 (the rust healing element)
public partial class mob : atom_movable
{
    /// Returns true if the mob is on a rusty tile, really low level just because we call it in a bunch of unrelated places
    // tg: code/modules/mob/mob_helpers.dm:593
    public virtual object is_touching_rust(float? check_flying = null)
    {
        check_flying ??= FALSE;
        if (Dm.truthy(check_flying) && Dm.truthy(Dm.bitand(movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
            return FALSE;
        if (HAS_TRAIT(this, TRAIT_MAGICALLY_PHASED) || Dm.truthy(Dm.bitand(movement_type, VENTCRAWLING)))
            return FALSE;
        turf our_turf = get_turf(this);
        return Dm.num(HAS_TRAIT(our_turf, TRAIT_RUSTY));
    }
}
