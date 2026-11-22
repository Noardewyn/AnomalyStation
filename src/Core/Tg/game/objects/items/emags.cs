// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/emags.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G1a (D316): the emag, its look-alikes, the fake emag and the doorjack. PARTIAL: the battlecruiser
// card (emags.dm:227-252) stays inert: it summons the Starfury (summon_battlecruiser, the battlecruiser team), which is not
// ported (not-translated.md section 134).

/*
 * EMAG AND SUBTYPES
 */
public partial class obj_item_card_emag : obj_item_card
{
    // tg: code/game/objects/items/emags.dm:20
    public override object get_displayed_name(float? honorifics = null)
    {
        return name; // That's Grey Tider (as "cryptographic sequencer")
    }

    // tg: code/game/objects/items/emags.dm:23
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra) //for traitors with balls of plastitanium
    {
        if (Dm.truthy(await Adjacent(user)))
        {
            // icon2html(src, viewers(user)), the card's picture in the chat line, is not declared (chat icons, M9), as the ID card's
            user.visible_message(span_notice(I18n.T("[] shows you: [].", user, name)), span_notice(I18n.T("You show [].", this)));
        }
        add_fingerprint(user);
        return null;
    }

    // tg: code/game/objects/items/emags.dm:28
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (user == null || emag_card == null)
            return FALSE;
        int emag_count = 0;
        foreach (obj_item_card_emag emag in Dm.each<obj_item_card_emag>(Dm.add(get_all_contents(), emag_card.get_all_contents()))) // This is including itself
            emag_count++;
        if (emag_count >= 128) // prevent overlay sprite overload allowed on a single item, incase someone manages to (somehow) emag an emag 128 times
            return FALSE;
        if (!Dm.eq(emag_card.loc, loc)) // Both have to be in your hand (or TK shenanigans)
            return FALSE;
        if (!Dm.truthy(await user.transferItemToLoc(emag_card, this, silent: FALSE)))
            return FALSE;

        user.visible_message(
            span_notice(I18n.T("[] holds [] to [], getting the two cards stuck together!", user, emag_card, this)),
            span_notice(I18n.T("As you hold [] to [], their magnets attract to one another, and they become stuck together!", emag_card, this)),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        playsound(this, "sound/effects/bang.ogg", 33, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(contemplation_period), user), 2 * SECONDS, TIMER_DELETE_ME, file: "code/game/objects/items/emags.dm", line: 45);
        emag_card.vis_flags = Dm.bitor(emag_card.vis_flags, Dm.bitor(VIS_INHERIT_ID, VIS_INHERIT_PLANE));
        Dm.append(vis_contents, emag_card);
        name = Dm.initial(this, (obj_item_card_emag p) => p.name);
        desc = Dm.initial(this, (obj_item_card_emag p) => p.desc);
        DmList all_emags = Dm.sub(get_all_contents_type(typeof(obj_item_card_emag)), this) as DmList;
        for (int i = 1; i <= Dm.length(all_emags); i++)
        {
            var other_emag = (obj_item_card_emag)all_emags[i];
            other_emag.pixel_x = pixel_x + (4 * i);
            other_emag.pixel_y = pixel_y + (4 * i);
            other_emag.layer = layer - (0.01f * i);
            name += Dm.text("-[]", Dm.initial(other_emag, (obj_item_card_emag p) => p.name));
            desc += " There seems to be another card stuck to it...pretty soundly.";
        }
        return TRUE;
    }

    // tg: code/game/objects/items/emags.dm:63
    public virtual async DmTask contemplation_period(mob user = null)
    {
        if (QDELETED(user))
            return;
        if (QDELETED(this))
            to_chat(user, span_notice(I18n.T("Oh, well.")));
        else
            to_chat(user, span_warning(I18n.T("Well, shit. Those are never coming apart now.")));
    }

    // tg: code/game/objects/items/emags.dm:71
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone is obj_item_card_emag)
        {
            // This is here so if(when) admins fish it out of contents it doesn't become glitchy
            gone.layer = Dm.initial(gone, (atom p) => p.layer);
            gone.vis_flags = Dm.initial(gone, (atom p) => p.vis_flags);
            Dm.remove(vis_contents, gone);
            name = Dm.initial(this, (obj_item_card_emag p) => p.name);
            desc = Dm.initial(this, (obj_item_card_emag p) => p.desc);
            gone.name = Dm.initial(this, (obj_item_card_emag p) => p.name);
            gone.desc = Dm.initial(this, (obj_item_card_emag p) => p.desc);
        }
        return dot;
    }

    // tg: code/game/objects/items/emags.dm:144
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        type_blacklist = Dm.list(Dm.sub(Dm.add(Dm.add(typesof(typeof(obj_machinery_door_airlock)), typesof(typeof(obj_machinery_door_window))), typesof(typeof(obj_machinery_door_firedoor))), typesof(typeof(obj_machinery_door_airlock_tram)))); //list of all typepaths that require a specialized emag to hack.
        return dot;
    }

    // tg: code/game/objects/items/emags.dm:148
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION) && Dm.truthy(user.combat_mode)) // SHOULD_SKIP_INTERACTION (__DEFINES/tools.dm:62), inline
            return NONE; // lets us put things in bags without trying to emag them
        if (!Dm.truthy(can_emag(interacting_with, user)))
            return ITEM_INTERACT_BLOCKING;
        log_combat(user, interacting_with, "attempted to emag");
        if (Dm.truthy(await interacting_with.emag_act(user, this)))
        {
            SSblackbox.record_feedback("tally", "atom_emagged", 1, interacting_with.type);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE; // In a perfect world this would be blocking, but this is not a perfect world
    }

    // tg: code/game/objects/items/emags.dm:159
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return Dm.truthy(prox_check) ? NONE : await interact_with_atom(interacting_with, user);
    }

    // tg: code/game/objects/items/emags.dm:162
    public virtual object can_emag(atom target = null, mob user = null)
    {
        foreach (object subtypelist in Dm.each(type_blacklist))
        {
            if (Dm.@in(target.type, subtypelist))
            {
                to_chat(user, span_warning(I18n.T("The [] cannot be affected by the []! A more specialized hacking device is required.", target, this)));
                return FALSE;
            }
        }
        return TRUE;
    }
}

public partial class obj_item_card_emagfake : obj_item_card
{
    // tg: code/game/objects/items/emags.dm:107
    public override object get_displayed_name(float? honorifics = null)
    {
        return name; // That's Grey Tider (as "cryptographic sequencer")
    }

    // tg: code/game/objects/items/emags.dm:110
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra) //for assistants with balls of plasteel
    {
        if (Dm.truthy(await Adjacent(user)))
            user.visible_message(span_notice(I18n.T("[] shows you: [].", user, name)), span_notice(I18n.T("You show [].", this)));
        add_fingerprint(user);
        return null;
    }

    // tg: code/game/objects/items/emags.dm:115
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(exploding))
            playsound(this, "sound/items/bikehorn.ogg", 50, TRUE, frequency: 2);
        else if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            log_bomber(user, "triggered", this, "(rigged/emagged)");
            visible_message(span_boldwarning(I18n.T("[] begins to heat up!", this)));
            playsound(this, "sound/items/bikehorn.ogg", 100, TRUE, frequency: 0.25f);
            _addtimer(Dm.@new<datum_callback>(this, nameof(blow_up)), 1 * SECONDS, TIMER_DELETE_ME, file: "code/game/objects/items/emags.dm", line: 122);
            exploding = TRUE;
        }
        else
            playsound(this, "sound/items/bikehorn.ogg", 50, TRUE);
        return ITEM_INTERACT_SKIP_TO_ATTACK; // So it does the attack animation.
    }

    // tg: code/game/objects/items/emags.dm:128
    public virtual async DmTask blow_up()
    {
        visible_message(span_boldwarning(I18n.T("[] explodes!", this)));
        await explosion(this, light_impact_range: 1, explosion_cause: this);
        qdel(this);
    }

    // tg: code/game/objects/items/emags.dm:133
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        playsound(this, SFX_SPARKS, 50, TRUE, SILENCED_SOUND_EXTRARANGE);
        desc = Dm.initial(typeof(obj_item_card_emag), (obj_item_card_emag p) => p.desc);
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        if (user != null)
        {
            balloon_alert(user, "rigged to blow");
            log_bomber(user, "rigged to blow", this, "(emagging)");
        }
        return TRUE;
    }
}

/*
 * DOORMAG
 */
public partial class obj_item_card_emag_doorjack : obj_item_card_emag
{
    // tg: code/game/objects/items/emags.dm:183
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        type_whitelist = Dm.list(typesof(typeof(obj_machinery_door_airlock)), typesof(typeof(obj_machinery_door_window)), typesof(typeof(obj_machinery_door_firedoor))); //list of all acceptable typepaths that this device can affect
        return dot;
    }

    // tg: code/game/objects/items/emags.dm:187
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        // only for doorjacks since regular emags still need to be able to break locks on storage items
        if (interacting_with.atom_storage != null)
            return NONE;
        return await base.interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/emags.dm:193
    public virtual void use_charge(mob user = null)
    {
        charges--;
        to_chat(user, span_notice(I18n.T("You use []. It now has [] charge[] remaining.", this, charges, charges == 1 ? "" : "s")));
        charge_timers.Add(_addtimer(Dm.@new<datum_callback>(this, nameof(recharge)), charge_time, TIMER_STOPPABLE, file: "code/game/objects/items/emags.dm", line: 196));
    }

    // tg: code/game/objects/items/emags.dm:198
    public virtual async DmTask recharge(mob user = null)
    {
        charges = Math.Min(charges + 1, max_charges);
        playsound(this, "sound/machines/beep/twobeep.ogg", 10, TRUE, extrarange: SILENCED_SOUND_EXTRARANGE, falloff_distance: 0);
        charge_timers.Remove(charge_timers[1]);
    }

    // tg: code/game/objects/items/emags.dm:203
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(dot, span_notice(I18n.T("It has [] charges remaining.", charges)));
        if (Dm.length(charge_timers) > 0)
            Dm.append(dot, Dm.text("[]</b>", span_notice(I18n.T("<b>A small display on the back reads:"))));
        for (int i = 1; i <= Dm.length(charge_timers); i++)
        {
            float timeleft = Dm.num(GlobalProcs.timeleft(charge_timers[i]));
            object loadingbar = num2loadingbar(timeleft / charge_time);
            Dm.append(dot, span_notice(Dm.text("<b>CHARGE #[]: [] ([])</b>", i, loadingbar, DisplayTimeText(timeleft))));
        }
        return dot;
    }

    // tg: code/game/objects/items/emags.dm:213
    public override object can_emag(atom target = null, mob user = null)
    {
        if (charges <= 0)
        {
            to_chat(user, span_warning(I18n.T("[] is recharging!", this)));
            return FALSE;
        }
        foreach (DmList subtypelist in Dm.each<DmList>(type_whitelist))
        {
            if (Dm.@in(target.type, subtypelist))
                return TRUE;
        }
        to_chat(user, span_warning(I18n.T("[] is unable to interface with this. It only seems to fit into airlock electronics.", this)));
        return FALSE;
    }
}
