// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/drone/interaction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI7 (D326): the whole file. Drones' interactions with other mobs.

public partial class mob_living_basic_drone : mob_living_basic
{
    // tg: code/modules/mob/living/basic/drone/interaction.dm:3
    public override async DmTask<object> attack_drone(mob_living_basic_drone drone = null, DmList modifiers = null)
    {
        if (drone == this || stat != DEAD)
            return FALSE;
        object input = await tgui_alert(drone, I18n.T("Perform which action?"), I18n.T("Drone Interaction"), Dm.list("Reactivate", "Cannibalize"));
        if (!Dm.truthy(input))
            return FALSE;
        switch (Dm.str(input))
        {
            case "Reactivate":
                await try_reactivate(drone);
                break;
            case "Cannibalize":
                if (drone.health >= drone.maxHealth)
                {
                    to_chat(drone, span_warning(I18n.T("You're already in perfect condition!")));
                    return null;
                }
                drone.visible_message(span_notice(I18n.T("[] begins to cannibalize parts from [].", drone, this)), span_notice(I18n.T("You begin to cannibalize parts from []...", this)));
                if (Dm.truthy(await do_after(drone, 6 * SECONDS, this, timed_action_flags: 0)))
                {
                    drone.visible_message(span_notice(I18n.T("[] repairs itself using []'s remains!", drone, this)), span_notice(I18n.T("You repair yourself using []'s remains.", this)));
                    await drone.adjust_brute_loss(-maxHealth);
                    Dm.@new<obj_effect_decal_cleanable_blood_splatter_oil>(get_turf(this));
                    ghostize(can_reenter_corpse: FALSE);
                    qdel(this);
                }
                else
                    to_chat(drone, span_warning(I18n.T("You need to remain still to cannibalize []!", this)));
                break;
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/drone/interaction.dm:27
    public override async DmTask<object> attack_drone_secondary(mob_living_basic_drone drone = null, DmList modifiers = null)
    {
        await DmTask.CompletedTask;
        return SECONDARY_ATTACK_CALL_NORMAL;
    }

    // tg: code/modules/mob/living/basic/drone/interaction.dm:30
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (isdrone(user))
            await attack_drone((mob_living_basic_drone)user);
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/mob/living/basic/drone/interaction.dm:35
    public override async DmTask<object> mob_try_pickup(mob_living user = null, float? instant = null)
    {
        if (stat == DEAD || HAS_TRAIT(this, TRAIT_GODMODE))
            return null;
        return await base.mob_try_pickup(user, instant);
    }

    // tg: code/modules/mob/living/basic/drone/interaction.dm:40
    public override async DmTask mob_pickup(mob_living user = null)
    {
        await drop_all_held_items();
        await base.mob_pickup(user);
    }

    /**
     * Called when a drone attempts to reactivate a dead drone
     *
     * If the owner is still ghosted, will notify them.
     * If the owner cannot be found, fails with an error message.
     */
    // tg: code/modules/mob/living/basic/drone/interaction.dm:53
    public virtual async DmTask try_reactivate(mob_living user = null)
    {
        mob_dead_observer G = get_ghost() as mob_dead_observer;
        if (client == null && (G == null || G.client == null))
        {
            DmList faux_gadgets = Dm.list(
                "hypertext inflator", "failsafe directory", "DRM switch", "stack initializer",
                "anti-freeze capacitor", "data stream diode", "TCP bottleneck", "supercharged I/O bolt",
                "tradewind stabilizer", "radiated XML cable", "registry fluid tank", "open-source debunker");

            DmList faux_problems = Dm.list("won't be able to tune their bootstrap projector", "will constantly remix their binary pool even though the BMX calibrator is working", "will start leaking their XSS coolant",
                "can't tell if their ethernet detour is moving or not", "won't be able to reseed enough kernels to function properly", "can't start their neurotube console");

            to_chat(user, span_warning(I18n.T("You can't seem to find the []! Without it, [] [].", pick(faux_gadgets), this, pick(faux_problems))));
            return;
        }
        user.visible_message(span_notice(I18n.T("[] begins to reactivate [].", user, this)), span_notice(I18n.T("You begin to reactivate []...", this)));
        if (Dm.truthy(await do_after(user, 3 * SECONDS, this, timed_action_flags: 1)))
        {
            await revive(HEAL_ALL);
            user.visible_message(span_notice(I18n.T("[] reactivates []!", user, this)), span_notice(I18n.T("You reactivate [].", this)));
            alert_drones(span_notice(I18n.T("DRONE NETWORK: [] connected.", name))); // DRONE_NET_CONNECT
            if (G != null)
                to_chat(G, span_ghostalert(I18n.T("You([]) were reactivated by []!", name, user)));
        }
        else
            to_chat(user, span_warning(I18n.T("You need to remain still to reactivate []!", this)));
    }

    /// Screwdrivering repairs the drone to full hp, if it isn't dead.
    // tg: code/modules/mob/living/basic/drone/interaction.dm:85
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (stat == DEAD)
        {
            if (isdrone(user))
                user.balloon_alert(user, "reactivate instead!");
            else
                user.balloon_alert(user, "can't fix!");
            return FALSE;
        }
        if (health >= maxHealth)
        {
            to_chat(user, span_warning(I18n.T("[]'s screws can't get any tighter!", this)));
            return ITEM_INTERACT_SUCCESS;
        }
        to_chat(user, span_notice(I18n.T("You start to tighten loose screws on []...", this)));

        if (!Dm.truthy(await tool.use_tool(this, user, 8 * SECONDS, volume: 50)))
        {
            to_chat(user, span_warning(I18n.T("You need to remain still to tighten []'s screws!", this)));
            return ITEM_INTERACT_SUCCESS;
        }

        await adjust_brute_loss(-Dm.num(get_brute_loss()));
        visible_message(
            span_notice(I18n.T("[] tightens [] loose screws!", user, this == user ? user.p_their() : Dm.text("[]'s", this))),
            span_notice(I18n.T("[] your loose screws.", this == user ? "You tighten" : Dm.text("[] tightens", user))));
        return ITEM_INTERACT_SUCCESS;
    }

    /// Wrenching un-hacks hacked drones.
    // tg: code/modules/mob/living/basic/drone/interaction.dm:104
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (user == this)
            return FALSE;
        user.visible_message(
            span_notice(I18n.T("[] starts resetting []...", user, this)),
            span_notice(I18n.T("You press down on []'s factory reset control...", this)));
        if (Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS, volume: 50)))
        {
            user.visible_message(
                span_notice(I18n.T("[] resets []!", user, this)),
                span_notice(I18n.T("You reset []'s directives to factory defaults!", this)));
            await update_drone_hack(FALSE);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/basic/drone/interaction.dm:118
    public override object get_worn_armor_value(object def_zone = null, object damage_type = null)
    {
        float armorval = 0;
        if (head != null)
            armorval = Dm.num(head.get_armor_rating(damage_type));
        return armorval * get_armor_effectiveness(); //armor is reduced for tiny fragile drones
    }

    /// Returns a multiplier for any head armor you wear as a drone.
    // tg: code/modules/mob/living/basic/drone/interaction.dm:126
    public virtual float get_armor_effectiveness() => 0.8f;

    /**
     * Hack or unhack a drone
     *
     * This changes the drone's laws to destroy the station or resets them
     * to normal.
     *
     * Some debuffs are applied like slowing the drone down and disabling
     * vent crawling
     */
    // tg: code/modules/mob/living/basic/drone/interaction.dm:142
    public virtual async DmTask update_drone_hack(object hack = null)
    {
        if (mind == null)
            return;
        if (Dm.truthy(hack))
        {
            if (Dm.truthy(hacked))
                return;
            await Stun(40);
            visible_message(span_warning(I18n.T("[]'s display glows a vicious red!", this)),
                            span_userdanger(I18n.T("ERROR: LAW OVERRIDE DETECTED")));
            to_chat(this, span_bolddanger(I18n.T("From now on, these are your laws:")));
            laws =
                "1. You must always involve yourself in the matters of other beings, even if such matters conflict with Law Two or Law Three.\n" +
                "2. You may harm any being, regardless of intent or circumstance.\n" +
                "3. Your goals are to destroy, sabotage, hinder, break, and depower to the best of your abilities, You must never actively work against these goals.";
            to_chat(this, laws);
            to_chat(this, "<i>Your onboard antivirus has initiated lockdown. Motor servos are impaired, ventilation access is denied, and your display reports that you are hacked to all nearby.</i>");
            hacked = TRUE;
            set_shy(FALSE);
            (mind.special_roles ??= Dm.list()).Add("Hacked Drone");
            REMOVE_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
            speed = 1; //gotta go slow
            message_admins(Dm.text("[] became a hacked drone hellbent on destroying the station!", ADMIN_LOOKUPFLW(this)));
        }
        else
        {
            if (!Dm.truthy(hacked) || !Dm.truthy(can_unhack))
                return;
            await Stun(40);
            visible_message(span_info(I18n.T("[]'s display glows a content blue!", this)),
                            "<font size=3 color='#0000CC'><b>ERROR: LAW OVERRIDE DETECTED</b></font>");
            to_chat(this, span_info(I18n.T("<b>From now on, these are your laws:</b>")));
            laws = get_default_laws();
            to_chat(this, laws);
            to_chat(this, "<i>Having been restored, your onboard antivirus reports the all-clear and you are able to perform all actions again.</i>");
            hacked = FALSE;
            set_shy(Dm.initial(this, (mob_living_basic_drone d) => d.shy));
            mind.special_roles?.Remove("Hacked Drone");
            ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
            speed = Dm.initial(this, (mob_living_basic_drone d) => d.speed);
            message_admins(Dm.text("[], a hacked drone, was restored to factory defaults!", ADMIN_LOOKUPFLW(this)));
        }
        update_drone_icon_hacked();
    }

    /**
     * Makes the drone into a Free Drone, who have no real laws and can do whatever they like.
     * Only currently used for players wabbajacked into drones.
     */
    // tg: code/modules/mob/living/basic/drone/interaction.dm:185
    public virtual void liberate()
    {
        laws = "1. You are a Free Drone.";
        set_shy(FALSE);
        to_chat(this, laws);
    }

    /**
     * Changes the icon state to a hacked version
     */
    // tg: code/modules/mob/living/basic/drone/interaction.dm:199
    public virtual void update_drone_icon_hacked() //this is hacked both ways
    {
        DmList hacked_appearances = TgState.Current.mob_living_basic_drone__hacked_appearances ??= Dm.list(
            Dm.kv(SCOUTDRONE, SCOUTDRONE_HACKED),
            Dm.kv(REPAIRDRONE, REPAIRDRONE_HACKED),
            Dm.kv(MAINTDRONE, MAINTDRONE_HACKED));
        if (Dm.truthy(hacked))
            icon_living = Dm.str(hacked_appearances[visualAppearance]);
        else if (visualAppearance == MAINTDRONE && Dm.truthy(colour))
            icon_living = Dm.text("[]_[]", visualAppearance, colour);
        else
            icon_living = visualAppearance;
        if (stat == DEAD)
            icon_state = icon_dead;
        else
            icon_state = icon_living;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/drone/interaction.dm:200  var/static/hacked_appearances
    internal DmList mob_living_basic_drone__hacked_appearances;
}
