// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/wizard/equipment/soulstone.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The soulstone itself (capturing a soul into a shade, releasing it, the exorcism by a bible, the soulful toolbox), the ectoplasm a
// ghost drops and the chaplain's sparring shard. Left for the cult and the wizard: corrupting a stone, putting a shade into a
// construct shell and the construct-making procs (make_new_construct_from_class, make_new_construct); those wait for their
// antagonists (D333). The var blocks are generated (D111).

// tg: code/modules/antagonists/wizard/equipment/soulstone.dm:1
public partial class obj_item_soulstone : obj_item
{
    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:33  var/static/list/slapcraft_recipe_list (Initialize's)
    private static DmList slapcraft_recipe_list => TgState.Current.obj_item_soulstone_Initialize__slapcraft_recipe_list ??= Dm.list(typeof(datum_crafting_recipe_mod_core_soul));

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.eq(theme, THEME_HOLY))
            RegisterSignal(this, COMSIG_BIBLE_SMACKED, nameof(on_bible_smacked));
        if (!Dm.truthy(base_name))
            base_name = Dm.initial(this, (obj_item_soulstone p) => p.name);

        Waiting.NoSleep(AddElement(
            typeof(datum_element_slapcrafting),
            Dm.named("slapcraft_recipes", slapcraft_recipe_list)));
        return dot;
    }

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:41
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_hauntium), 25f), Dm.kv(typeof(datum_reagent_silicon), 10f)); //can be ground into hauntium
    }

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:44
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_appearance(updates);
        foreach (mob_living_basic_shade sharded_shade in Dm.each<mob_living_basic_shade>(contents))
        {
            if (Dm.eq(theme, THEME_HOLY))
                sharded_shade.name = I18n.T("Purified []", sharded_shade.real_name);
            else
                sharded_shade.name = Dm.str(sharded_shade.real_name);
            sharded_shade.theme = theme;
            sharded_shade.update_appearance(UPDATE_ICON_STATE);
        }
        return dot;
    }

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:55
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.eq(theme, THEME_HOLY))
            icon_state = "purified_soulstone";
        else if (Dm.eq(theme, THEME_CULT))
            icon_state = "soulstone";
        else if (Dm.eq(theme, THEME_WIZARD))
            icon_state = "mystic_soulstone";

        if (Dm.truthy(contents.len))
            icon_state = Dm.text("[]2", icon_state);
        return dot;
    }

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:68
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        name = Dm.text("[]", base_name);
        if (Dm.truthy(spent))
        {
            // "dull soulstone"
            name = I18n.T("dull []", name);
        }

        mob_living_basic_shade shade = Dm.locate<mob_living_basic_shade>(contents);
        if (shade != null)
        {
            // "(dull) soulstone: Urist McCaptain"
            name = Dm.text("[]: []", name, shade.real_name);
        }
        return dot;
    }

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:80
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        if (Dm.truthy(spent))
            desc = I18n.T("A fragment of the legendary treasure known simply as the 'Soul Stone'. The shard lies still, dull and lifeless; whatever spark it once held long extinguished.");
        return dot;
    }

    ///signal called whenever a soulstone is smacked by a bible
    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:88
    [SignalHandler]
    public virtual void on_bible_smacked(datum source = null, mob_living user = null, params object[] __extra)
    {
        Waiting.Detach(attempt_exorcism(user));
    }

    /**
     * attempt_exorcism: called from on_bible_smacked, takes time and if successful
     * resets the item to a pre-possessed state
     *
     * Arguments:
     * * exorcist: user who is attempting to remove the spirit
     */
    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:99
    public virtual async DmTask attempt_exorcism(mob exorcist = null)
    {
        if (Dm.truthy(IS_CULTIST(exorcist)) || Dm.eq(theme, THEME_HOLY))
            return;
        balloon_alert(exorcist, I18n.T("exorcising..."));
        playsound(this, Dm.resource("sound/effects/hallucinations/veryfar_noise.ogg"), 40, TRUE);
        if (!Dm.truthy(await do_after(exorcist, 4 * SECONDS, target: this)))
            return;
        playsound(this, Dm.resource("sound/effects/pray_chaplain.ogg"), 60, TRUE);
        required_role = null;
        theme = THEME_HOLY;

        update_appearance();
        foreach (mob shade_to_deconvert in Dm.each<mob>(contents))
            await assign_master(shade_to_deconvert, exorcist);

        exorcist.visible_message(span_notice(I18n.T("[] purifies []!", exorcist, this)));
        UnregisterSignal(this, COMSIG_BIBLE_SMACKED);
    }

    /// Checks if the passed mob has the required antag datum set on the soulstone.
    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:136
    public virtual object role_check(mob who = null)
    {
        return required_role != null ? (who.mind != null && Dm.truthy(who.mind.has_antag_datum(required_role, TRUE)) ? TRUE : FALSE) : TRUE;
    }

    /// Called whenever the soulstone releases a shade from it.
    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:140
    public virtual void on_release_spirits()
    {
        if (!Dm.truthy(one_use))
            return;

        spent = TRUE;
        update_appearance();
    }

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:147
    public override async DmTask<object> pickup(mob user = null)
    {
        await base.pickup(user);
        if (!Dm.truthy(role_check(user)))
            to_chat(user, span_danger(I18n.T("An overwhelming feeling of dread comes over you as you pick up []. It would be wise to be rid of this quickly.", this)));
        return null;
    }

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:152
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(role_check(user)) || isobserver(user))
        {
            if (!Dm.truthy(grab_sleeping))
                Dm.append(ref dot, span_cult(I18n.T("A soulstone, used to capture a soul, either from dead humans or from freed shades.")));
            else
                Dm.append(ref dot, span_cult(I18n.T("A soulstone, used to capture souls, either from unconscious or sleeping humans or from freed shades.")));
            Dm.append(ref dot, span_cult(I18n.T("The captured soul can be placed into a construct shell to produce a construct, or released from the stone as a shade.")));
            if (Dm.truthy(spent))
                Dm.append(ref dot, span_cult(I18n.T("This shard is spent; it is now just a creepy rock.")));
        }
        return dot;
    }

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:163
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        if (!isliving(user) || user.mind == null)
            return dot;
        return dot;
    }

    //Stops the shade from being qdel'd immediately and their ghost being sent back to the arrival shuttle.
    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:168
    public override object Destroy(float? force = null)
    {
        foreach (mob_living_basic_shade shade in Dm.each<mob_living_basic_shade>(contents))
            Waiting.Detach(shade.death());
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:173
    public virtual void hot_potato(mob_living user = null)
    {
        to_chat(user, span_userdanger(I18n.T("Holy magics residing in [] burn your hand!", this)));
        Waiting.NoSleep(user.apply_damage(10, BURN, user.get_active_hand()));
        Waiting.Detach(user.emote("scream"));
        Waiting.NoSleep(user.update_damage_overlays());
        Waiting.NoSleep(user.dropItemToGround(this));
    }

    //////////////////////////////Capturing////////////////////////////////////////////////////////

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:182
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living M = target_mob;
        if (!Dm.truthy(role_check(user)))
        {
            await user.Unconscious(10 * SECONDS);
            to_chat(user, span_userdanger(I18n.T("Your body is wracked with debilitating pain!")));
            return null;
        }
        if (Dm.truthy(spent))
        {
            to_chat(user, span_warning(I18n.T("There is no power left in [].", this)));
            return null;
        }
        if (!ishuman(M))//If target is not a human.
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (M == user)
            return null;
        if (Dm.truthy(IS_CULTIST(M)) && Dm.truthy(IS_CULTIST(user)))
        {
            to_chat(user, span_cult_large(I18n.T("\"Come now, do not capture your bretheren's soul.\"")));
            return null;
        }
        if (Dm.eq(theme, THEME_HOLY) && Dm.truthy(IS_CULTIST(user)))
        {
            hot_potato(user);
            return null;
        }
        if (HAS_TRAIT(M, TRAIT_NO_SOUL))
        {
            to_chat(user, span_warning(I18n.T("This body does not possess a soul to capture.")));
            return null;
        }
        log_combat(user, M, Dm.text("captured []'s soul", M.name), this);
        await capture_soul(M, user);
        return null;
    }

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:206
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        await base.suicide_act(user);
        user.visible_message(span_suicide(I18n.T("[] is capturing [] own soul with []! It looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
        return Dm.truthy(await capture_soul(user, null, TRUE)) ? MANUAL_SUICIDE : BRUTELOSS;
    }

    ///////////////////Options for using captured souls///////////////////////////////////////

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:213
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(in_range(this, user)))
            return null;
        mob_living living_user = user as mob_living;
        if (!Dm.truthy(role_check(user)))
        {
            if (living_user != null)
                await living_user.Unconscious(100);
            to_chat(user, span_userdanger(I18n.T("Your body is wracked with debilitating pain!")));
            return null;
        }
        if (Dm.eq(theme, THEME_HOLY) && Dm.truthy(IS_CULTIST(user)))
        {
            hot_potato(living_user);
            return null;
        }
        await release_shades(user);
        return null;
    }

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:225
    public virtual async DmTask release_shades(mob user = null, float? silent = null)
    {
        silent ??= FALSE;
        foreach (mob_living_basic_shade captured_shade in Dm.each<mob_living_basic_shade>(contents))
        {
            await captured_shade.forceMove(get_turf(user));
            await captured_shade.cancel_camera();
            update_appearance();
            if (!Dm.truthy(silent))
            {
                if (Dm.truthy(IS_CULTIST(user)))
                {
                    to_chat(captured_shade, span_bold(I18n.T("You have been released from your prison, but you are still bound to the cult's will. Help them succeed in their goals at all costs.")));
                }
                else if (Dm.truthy(role_check(user)))
                {
                    to_chat(captured_shade, span_bold(I18n.T("You have been released from your prison, but you are still bound to []'s will. Help [] succeed in [] goals at all costs.", user.real_name, user.p_them(), user.p_their())));
                }
            }
            datum_antagonist_cult_shade shade_datum = captured_shade.mind?.has_antag_datum(typeof(datum_antagonist_cult_shade)) as datum_antagonist_cult_shade;
            if (shade_datum != null)
                shade_datum.release_time = world.time;
            on_release_spirits();
        }
    }

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:244
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        mob_living_basic_shade occupant = Dm.locate<mob_living_basic_shade>(contents);
        obj_item_storage_toolbox_mechanical target_toolbox = interacting_with as obj_item_storage_toolbox_mechanical;
        if (occupant == null || target_toolbox == null || Dm.truthy(target_toolbox.has_soul))
            return NONE;

        if (Dm.eq(theme, THEME_HOLY) && Dm.truthy(IS_CULTIST(user)))
        {
            hot_potato(user);
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(role_check(user)))
        {
            await user.Unconscious(10 * SECONDS);
            to_chat(user, span_userdanger(I18n.T("Your body is wracked with debilitating pain!")));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_notice(I18n.T("[] holds [] above [] head and forces it into [] with a flash of light!", user, this, user.p_their(), target_toolbox)),
            span_notice(I18n.T("You hold [] above your head briefly, then force it into [], transferring the []'s soul!", this, target_toolbox, occupant)), ignored_mobs: occupant);
        to_chat(occupant, span_userdanger(I18n.T("[] holds you up briefly, then forces you into []!", user, target_toolbox)));
        to_chat(occupant, span_deadsay(I18n.T("<b>Your eternal soul has been sacrificed to restore the soul of a toolbox. Them's the breaks!</b>")));

        Dm.Deferred("code/modules/antagonists/wizard/equipment/soulstone.dm:265", "occupant.client?.give_award(/datum/award/achievement/misc/toolbox_soul, occupant): the achievements (D332)");
        occupant.death_message = I18n.T("shrieks out in unholy pain as [] soul is absorbed into []!", occupant.p_their(), target_toolbox);
        await release_shades(user, TRUE);
        await occupant.death();

        target_toolbox.name = I18n.T("soulful toolbox");
        target_toolbox.icon = Dm.resource("icons/obj/storage/toolbox.dmi");
        target_toolbox.icon_state = "toolbox_blue_old";
        target_toolbox.has_soul = TRUE;
        target_toolbox.has_latches = FALSE;
        return ITEM_INTERACT_SUCCESS;
    }

    /// Procs for moving soul in and out off stone

    /// Transfer the mind of a carbon mob (which is then dusted) into a shade mob inside src.
    /// If forced, sacrificial and stat checks are skipped.
    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:313
    public virtual async DmTask<object> capture_soul(mob_living victim = null, mob user = null, float? forced = null)
    {
        forced ??= FALSE;
        if (!iscarbon(victim)) //TODO: Add sacrifice stoning for non-organics, just because you have no body doesn't mean you don't have a soul
            return FALSE;
        if (Dm.truthy(contents.len))
            return FALSE;

        if (!Dm.truthy(forced))
        {
            if (user?.mind?.has_antag_datum(typeof(datum_antagonist_cult)) != null)
                Dm.NotTranslated("code/modules/antagonists/wizard/equipment/soulstone.dm:318", "capture_soul: a cultist's capture refuses the cult's sacrifice target (the cult team); waits for the cult (D333)");

            if (Dm.truthy(grab_sleeping) ? !IS_UNCONSCIOUS_OR_CRIT(victim) : victim.stat != DEAD)
            {
                to_chat(user, span_userdanger(I18n.T("Capture failed!")));
                to_chat(user, span_danger(I18n.T("Kill or maim the victim first!")));
                return FALSE;
            }
        }

        victim.grab_ghost();
        if (victim.client != null)
        {
            await init_shade(victim, user);
            return TRUE;
        }

        to_chat(user, span_userdanger(I18n.T("Capture failed!")));
        to_chat(user, span_warning(I18n.T("The soul has already fled its mortal frame. You attempt to bring it back...")));
        mob chosen_one = await SSpolling.poll_ghosts_for_target(
            check_jobban: ROLE_CULTIST,
            poll_time: 20 * SECONDS,
            checked_target: this,
            ignore_category: POLL_IGNORE_SHADE,
            alert_pic: typeof(mob_living_basic_shade),
            jump_target: this,
            role_name_text: "a shade",
            chat_text_border_icon: typeof(mob_living_basic_shade)) as mob;
        await on_poll_concluded(user as mob_living, victim, chosen_one as mob_dead_observer);
        return TRUE; //it'll probably get someone ;)
    }

    ///captures a shade that was previously released from a soulstone.
    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:353
    public virtual async DmTask<object> capture_shade(mob_living_basic_shade shade = null, mob_living user = null)
    {
        if (isliving(user) && !Dm.truthy(role_check(user)))
        {
            await user.Unconscious(10 * SECONDS);
            to_chat(user, span_userdanger(I18n.T("Your body is wracked with debilitating pain!")));
            return null;
        }
        if (Dm.truthy(contents.len))
        {
            to_chat(user, span_userdanger(I18n.T("Capture failed!")));
            to_chat(user, span_danger(I18n.T("[] is full! Free an existing soul to make room.", this)));
            return FALSE;
        }
        await shade.AddComponent(typeof(datum_component_soulstoned), this);
        update_appearance();

        to_chat(shade, span_notice(I18n.T("Your soul has been captured by []. Its arcane energies are reknitting your ethereal form.", this)));

        datum_antagonist_cult_shade shade_datum = shade.mind?.has_antag_datum(typeof(datum_antagonist_cult_shade)) as datum_antagonist_cult_shade;
        if (shade_datum != null)
            shade_datum.release_time = null;

        if (user != shade)
        {
            to_chat(user, Dm.text("[] []", span_info(I18n.T("<b>Capture successful!</b>:")), I18n.T("[]'s soul has been captured and stored within [].", shade.real_name, this)));
            await assign_master(shade, user);
        }

        return TRUE;
    }

    /**
     * Creates a new shade mob to inhabit the stone.
     *
     * victim - the body that's being shaded
     * user - the person doing the shading. Optional.
     * message_user - if TRUE, we send the user (if present) a message that a shade has been created / captured.
     * shade_controller - the mob (usually, a ghost) that will take over control of the victim / new shade. Optional, if not passed the victim itself will take control.
     */
    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:409
    public virtual async DmTask init_shade(mob_living victim = null, mob user = null, float? message_user = null, mob shade_controller = null)
    {
        message_user ??= FALSE;
        if (shade_controller == null)
            shade_controller = victim;
        victim.stop_sound_channel(CHANNEL_HEARTBEAT);
        mob_living_basic_shade soulstone_spirit = Dm.@new<mob_living_basic_shade>(this);
        await soulstone_spirit.AddComponent(typeof(datum_component_soulstoned), this);
        soulstone_spirit.name = I18n.T("Shade of []", victim.real_name);
        soulstone_spirit.real_name = I18n.T("Shade of []", victim.real_name);
        soulstone_spirit.PossessByPlayer(shade_controller.key);
        soulstone_spirit.copy_languages(victim, LANGUAGE_MIND);//Copies the old mobs languages into the new mob holder.
        if (user != null)
            soulstone_spirit.copy_languages(user, LANGUAGE_MASTER);
        soulstone_spirit.get_language_holder().omnitongue = TRUE; //Grants omnitongue
        if (user != null)
        {
            soulstone_spirit.add_ally(user); //Add the master as a faction, allowing inter-mob cooperation
            if (Dm.truthy(IS_CULTIST(user)))
                Dm.NotTranslated("code/modules/antagonists/wizard/equipment/soulstone.dm:425", "init_shade: a cultist's shade joins the cult (/datum/antagonist/cult/shade); waits for the cult (D333)");
        }

        await soulstone_spirit.cancel_camera();
        update_appearance();
        if (user != null)
        {
            if (Dm.truthy(IS_CULTIST(user)))
            {
                to_chat(soulstone_spirit, span_bold(I18n.T("Your soul has been captured! You are now bound to the cult's will. Help them succeed in their goals at all costs.")));
            }
            else if (Dm.truthy(role_check(user)))
            {
                to_chat(soulstone_spirit, span_bold(I18n.T("Your soul has been captured! You are now bound to []'s will. Help [] succeed in [] goals at all costs.", user.real_name, user.p_them(), user.p_their())));
                await assign_master(soulstone_spirit, user);
            }

            if (Dm.truthy(message_user))
                to_chat(user, Dm.text("[] []", span_info(I18n.T("<b>Capture successful!</b>:")), I18n.T("[]'s soul has been ripped from [] body and stored within [].", victim.real_name, victim.p_their(), this)));
        }

        await victim.dust(drop_items: TRUE);
    }

    /**
     * Assigns the bearer as the new master of a shade.
     */
    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:448
    public virtual async DmTask assign_master(mob shade = null, mob user = null)
    {
        if (shade == null || user == null || shade.mind == null)
            return;

        // Cult shades get cult datum
        if (Dm.truthy(user.mind.has_antag_datum(typeof(datum_antagonist_cult))))
        {
            Dm.NotTranslated("code/modules/antagonists/wizard/equipment/soulstone.dm:453", "assign_master: a cultist master turns the shade into a cult shade (/datum/antagonist/cult/shade); waits for the cult (D333)");
            return;
        }

        // Only blessed soulstones can de-cult shades
        if (Dm.eq(theme, THEME_HOLY))
            await shade.mind.remove_antag_datum(typeof(datum_antagonist_cult));

        datum_antagonist_shade_minion shade_datum = shade.mind.has_antag_datum(typeof(datum_antagonist_shade_minion)) as datum_antagonist_shade_minion;
        if (shade_datum == null)
            shade_datum = await shade.mind.add_antag_datum(typeof(datum_antagonist_shade_minion)) as datum_antagonist_shade_minion;
        await shade_datum.update_master(Dm.str(user.real_name));
    }

    /// Called when a ghost is chosen to become a shade.
    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:468
    public virtual async DmTask<object> on_poll_concluded(mob_living master = null, mob_living victim = null, mob_dead_observer ghost = null)
    {
        if (victim == null || Dm.truthy(master.incapacitated) || !Dm.truthy(master.is_holding(this)) || !Dm.truthy(await victim.IsReachableBy(master, reach)))
            return FALSE;
        if (ghost?.client == null)
        {
            to_chat(master, span_danger(I18n.T("There were no spirits willing to become a shade.")));
            return FALSE;
        }
        if (Dm.truthy(Dm.length(contents))) //If they used the soulstone on someone else in the meantime
            return FALSE;
        to_chat(master, Dm.text("[] []", span_info(I18n.T("<b>Capture successful!</b>:")), I18n.T("A spirit has entered [], taking upon the identity of [].", this, victim)));
        await init_shade(victim, master, shade_controller: ghost);

        return TRUE;
    }
}

// tg: code/modules/antagonists/wizard/equipment/soulstone.dm:592
public partial class obj_item_ectoplasm : obj_item
{
    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:599
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_hauntium), 25f));
    }

    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:602
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is inhaling []! It looks like [] trying to visit the astral plane!", user, this, user.p_theyre())));
        return OXYLOSS;
    }
}

// tg: code/modules/antagonists/wizard/equipment/soulstone.dm:631
public partial class obj_item_soulstone_anybody_chaplain_sparring : obj_item_soulstone_anybody_chaplain
{
    // tg: code/modules/antagonists/wizard/equipment/soulstone.dm:637
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = I18n.T("[]'s punishment", GLOB.deity);
        base_name = name;
        desc = I18n.T("A prison for those who lost []'s game.", GLOB.deity);
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList obj_item_soulstone_Initialize__slapcraft_recipe_list;
}
