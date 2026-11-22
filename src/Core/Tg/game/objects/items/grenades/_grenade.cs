// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/grenades/_grenade.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5x slice 4 (D149: C4, the one explosive of M5x, is a grenade): the grenade base. Left in docs/ai/specs/live-partial.txt: the
// suicide (M10), the fantasy bonuses (M10) and the projectile that sets off a held grenade (hit_reaction: projectiles, M6). The
// grenade subtypes (flashbangs, chem and frag grenades, ...) stay inert with their own procs. The var block is generated.

/**
 * Base class for all grenades.
 */
// tg: code/game/objects/items/grenades/_grenade.dm:4
[DmPath("/obj/item/grenade")]
public partial class obj_item_grenade : obj_item
{
    // tg: code/game/objects/items/grenades/_grenade.dm:62
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_ODD_CUSTOMIZABLE_FOOD_INGREDIENT, type);
        RegisterSignal(this, COMSIG_ITEM_USED_AS_INGREDIENT, nameof(on_used_as_ingredient));
        return dot;
    }

    // tg: code/game/objects/items/grenades/_grenade.dm:75
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (!Dm.truthy(disassembled))
            await detonate();
    }

    /**
     * Checks for various ways to botch priming a grenade.
     *
     * Arguments:
     * * mob/living/carbon/human/user - who is priming our grenade?
     */
    // tg: code/game/objects/items/grenades/_grenade.dm:108
    public virtual async DmTask<object> botch_check(mob_living_carbon_human user = null)
    {
        if (Dm.truthy(sticky) && prob(50)) // to add risk to sticky tape grenade cheese, no return cause we still prime as normal after.
        {
            to_chat(user, span_warning(I18n.T("What the... [] is stuck to your hand!", this)));
            ADD_TRAIT(this, TRAIT_NODROP, STICKY_NODROP);
        }

        bool clumsy = HAS_TRAIT(user, TRAIT_CLUMSY);
        if (clumsy && (clumsy_check == GRENADE_CLUMSY_FUMBLE) && prob(50))
        {
            to_chat(user, span_warning(I18n.T("Huh? How does this thing work?")));
            await arm_grenade(user, 5, FALSE);
            return TRUE;
        }
        else if (!clumsy && (clumsy_check == GRENADE_NONCLUMSY_FUMBLE))
        {
            to_chat(user, span_warning(I18n.T("You pull the pin on []. Attached to it is a pink ribbon that says, \"[]\"", this, span_clown("HONK"))));
            await arm_grenade(user, 5, FALSE);
            return TRUE;
        }
        return null;
    }

    // tg: code/game/objects/items/grenades/_grenade.dm:123
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(display_timer))
        {
            if (det_time > 0)
                Dm.append(dot, I18n.T("The timer is set to [].", DisplayTimeText(det_time)));
            else
                Dm.append(dot, I18n.T("\\The [] is set for instant detonation.", this));
        }
        if (Dm.truthy(Dm.bitand(dud_flags, GRENADE_USED)))
            Dm.append(dot, span_warning(I18n.T("It looks like [] already been used.", p_theyve())));
        return dot;
    }

    // tg: code/game/objects/items/grenades/_grenade.dm:133
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (HAS_TRAIT(this, TRAIT_NODROP))
        {
            to_chat(user, span_notice(I18n.T("You try prying [] off your hand...", this)));
            if (Dm.truthy(await do_after(user, 7 * SECONDS, target: this)))
            {
                to_chat(user, span_notice(I18n.T("You manage to remove [] from your hand.", this)));
                REMOVE_TRAIT(this, TRAIT_NODROP, STICKY_NODROP);
            }
            return null;
        }

        if (Dm.truthy(active))
            return null;
        if (!Dm.truthy(await botch_check(user as mob_living_carbon_human))) // if they botch the prime, it'll be handled in botch_check
            await arm_grenade(user);
        return null;
    }

    // tg: code/game/objects/items/grenades/_grenade.dm:146
    public virtual void log_grenade(mob user = null)
    {
        if (!Dm.truthy(type_cluster))
        {
            log_bomber(user, "has primed a", this, "for detonation", message_admins: dud_flags == NONE ? TRUE : FALSE);
        }
    }

    /**
     * arm_grenade (formerly preprime) refers to when a grenade with a standard time fuze is activated, making it go beepbeepbeep and then detonate a few seconds later.
     * Grenades with other triggers like remote igniters probably skip this step and go straight to [/obj/item/grenade/proc/detonate]
     */
    // tg: code/game/objects/items/grenades/_grenade.dm:154
    public virtual async DmTask arm_grenade(mob user = null, object delayoverride = null, float? msg = null, float? volume = null)
    {
        msg ??= TRUE;
        volume ??= 60;
        log_grenade(user); //Inbuilt admin procs already handle null users
        if (user != null)
        {
            // tg: code/game/objects/items/grenades/_grenade.dm:157
            add_fingerprint(user);
            if (Dm.truthy(msg))
                to_chat(user, span_warning(I18n.T("You prime []! []!", this, capitalize(DisplayTimeText(det_time)))));
        }
        if (shrapnel_type != null && Dm.truthy(shrapnel_radius))
        {
            shrapnel_initialized = TRUE;
            await AddComponent(typeof(datum_component_pellet_cloud), Dm.named("projectile_type", shrapnel_type), Dm.named("magnitude", shrapnel_radius));
        }
        playsound(this, grenade_arm_sound, volume, grenade_sound_vary);
        if (istype(user, typeof(mob)))
            ((mob)user).add_mob_memory(typeof(datum_memory_bomb_planted), Dm.named("antagonist", this));
        active = TRUE;
        icon_state = Dm.add(Dm.truthy(base_icon_state) ? base_icon_state : Dm.initial<obj_item_grenade, string>(this, g => g.icon_state), "_active") as string;
        if (Dm.truthy(_listen_lookup?[COMSIG_GRENADE_ARMED]))
            _SendSignal(COMSIG_GRENADE_ARMED, Dm.list(this, det_time, delayoverride));
        _addtimer(Dm.@new<datum_callback>(this, nameof(detonate)), delayoverride == null ? det_time : Dm.num(delayoverride), file: "code/game/objects/items/grenades/_grenade.dm", line: 169);
    }

    /**
     * detonate (formerly prime) refers to when the grenade actually delivers its payload (whether or not a boom/bang/detonation is involved)
     *
     * Arguments:
     * * lanced_by- If this grenade was detonated by an elance, we need to pass that along with the COMSIG_GRENADE_DETONATE signal for pellet clouds
     */
    // tg: code/game/objects/items/grenades/_grenade.dm:177
    public virtual async DmTask<object> detonate(mob_living lanced_by = null)
    {
        if (Dm.truthy(dud_flags))
        {
            active = FALSE;
            update_appearance();
            return FALSE;
        }

        // tg: :183-188  for(var/obj/effect/forcefield/cosmic_field/potential_field as anything in GLOB.active_cosmic_fields): a heretic's
        // cosmic field within 2 tiles turns the grenade into a dud. GAP(lead): GLOB.active_cosmic_fields is not declared; no
        // cosmic field exists without the heretics (antagonists, M10), so the loop finds none.
        Dm.Deferred("code/game/objects/items/grenades/_grenade.dm:183", "detonate: a heretic's cosmic field near the grenade makes it a dud (antagonists, M10+)");

        dud_flags = Dm.bitor(dud_flags, GRENADE_USED); // Don't detonate if we have already detonated.
        if (shrapnel_type != null && Dm.truthy(shrapnel_radius) && !Dm.truthy(shrapnel_initialized)) // add a second check for adding the component in case whatever triggered the grenade went straight to prime (badminnery for example)
        {
            shrapnel_initialized = TRUE;
            await AddComponent(typeof(datum_component_pellet_cloud), Dm.named("projectile_type", shrapnel_type), Dm.named("magnitude", shrapnel_radius));
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_GRENADE_DETONATE]))
            _SendSignal(COMSIG_GRENADE_DETONATE, Dm.list(this, lanced_by));
        if (Dm.truthy(ex_dev) || Dm.truthy(ex_heavy) || Dm.truthy(ex_light) || Dm.truthy(ex_flame))
            await explosion(this, ex_dev, ex_heavy, ex_light, ex_flame);

        return TRUE;
    }

    // tg: code/game/objects/items/grenades/_grenade.dm:201
    public virtual async DmTask update_mob()
    {
        if (ismob(loc))
        {
            mob mob = (mob)loc;
            await mob.dropItemToGround(this);
        }
    }

    ///Signal sent by someone putting the grenade in as an ingredient. Registers signals onto what it was put into so it can explode.
    // tg: code/game/objects/items/grenades/_grenade.dm:207
    [SignalHandler]
    public virtual void on_used_as_ingredient(datum source = null, atom used_in = null)
    {
        RegisterSignal(used_in, COMSIG_FOOD_EATEN, nameof(ingredient_detonation));
        RegisterSignal(used_in, COMSIG_TOOL_ATOM_ACTED_PRIMARY(TOOL_KNIFE), nameof(ingredient_detonation));
        RegisterSignal(used_in, COMSIG_TOOL_ATOM_ACTED_PRIMARY(TOOL_ROLLINGPIN), nameof(ingredient_detonation));
    }

    ///Signal sent by someone eating food this is an ingredient in "used_in". Makes the grenade go kerblooey, destroying the food.
    // tg: code/game/objects/items/grenades/_grenade.dm:215
    [SignalHandler]
    public virtual object ingredient_detonation(atom used_in = null, mob_living target = null, mob_living user = null, object bitecount = null, object bitesize = null)
    {
        // a SIGNAL_HANDLER's call runs without waiting (waitfor = FALSE): explosion() may sleep
        Waiting.Detach(detonate());
        //can't remove it as an ingredient so we need to get rid of the food. deleted ingredients not good
        return DESTROY_FOOD;
    }

    // tg: code/game/objects/items/grenades/_grenade.dm:222
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(active))
            return FALSE;
        if (Dm.truthy(change_det_time()))
        {
            tool.play_tool_sound(this);
            if (det_time == 0)
                to_chat(user, span_notice(I18n.T("You modify the time delay. It's set to be instantaneous.")));
            else
                to_chat(user, span_notice(I18n.T("You modify the time delay. It's set for [].", DisplayTimeText(det_time))));
            return TRUE;
        }
        return null;
    }

    // tg: code/game/objects/items/grenades/_grenade.dm:233
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        if (Dm.truthy(active))
            return FALSE;

        dot = TRUE;

        object newtime = await tgui_input_list(user, "Please enter a new detonation time", "Detonation Timer", possible_fuse_time);
        if (newtime == null)
            return dot;
        if (!Dm.truthy(await user.can_perform_action(this)))
            return dot;
        if (Dm.eq(newtime, "Instant") && Dm.truthy(change_det_time(0)))
        {
            to_chat(user, span_notice(I18n.T("You modify the time delay. It's set to be instantaneous.")));
            return dot;
        }
        newtime = round(Dm.num(text2num(newtime)));
        if (Dm.truthy(change_det_time(newtime)))
            to_chat(user, span_notice(I18n.T("You modify the time delay. It's set for [].", DisplayTimeText(det_time))));
        return dot;
    }

    /**
     * Sets det_time to a number in SECONDS
     *
     * if time is passed as an argument, `det_time` will be `time SECONDS`
     *
     * Cycles the duration of the fuse of the grenade `det_time` based on the options provided in list/possible_fuse_time
    */
    // tg: code/game/objects/items/grenades/_grenade.dm:259
    public virtual object change_det_time(object time = null)
    {
        object dot = TRUE;
        //Multitool
        if (time != null)
        {
            det_time = round(Math.Clamp(Dm.num(time) * SECONDS, 0, 5 * SECONDS)); //This is fine for now but consider making this a variable if you want >5s fuse
            return dot;
        }

        //Screwdriver
        // (tg keeps the fuse's text in det_time while it cycles; a local holds it here, det_time being a number var in C#)
        object fuse;
        if (det_time == 0)
            fuse = "Instant";
        else
            fuse = num2text(det_time * 0.1f);

        float old_selection = possible_fuse_time.Find(fuse); //Position of det_time in the list
        if (old_selection >= possible_fuse_time.len)
            fuse = possible_fuse_time[1];
        else
            fuse = possible_fuse_time[old_selection + 1];

        if (Dm.eq(fuse, "Instant"))
        {
            det_time = 0; //String to num conversion because I hate coders
            return dot;
        }
        det_time = Dm.num(text2num(fuse)) * SECONDS;
        return dot;
    }

    // tg: code/game/objects/items/grenades/_grenade.dm:283
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/items/grenades/_grenade.dm:300
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(active))
        {
            await user.throw_item(interacting_with);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }
}
