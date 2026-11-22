// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/his_grace.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4c, D316). The ascension achievement (the achievements) is a leaf.
// The var block is generated (D111).

//His Grace is a very special weapon granted only to traitor chaplains.
//When awakened, He thirsts for blood and begins ticking a "bloodthirst" counter.
//The wielder of His Grace is immune to stuns and gradually heals.
//If the wielder fails to feed His Grace in time, He will devour them and become incredibly aggressive.
//Leaving His Grace alone for some time will reset His thirst and put Him to sleep.
//Using His Grace effectively requires extreme speed and care.
// tg: code/game/objects/items/weaponry/melee/his_grace.dm:7
public partial class obj_item_his_grace : obj_item
{
    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:32
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_slimepotion_genderchange)
        {
            turf T = get_turf(this);
            mob_living holder = null;
            if (loc is mob_living)
                holder = loc as mob_living;
            if (gender == MALE)
            {
                gender = FEMALE;
                if (Dm.truthy(awakened))
                    name = "Her Grace";
                else if (!Dm.truthy(ascended))
                    desc = "A toolbox painted bright pink. Looking at it makes you feel uneasy.";
                icon_state = "pink";
                inhand_icon_state = "toolbox_pink";
                T.visible_message(span_boldwarning(I18n.T("[] starts to look a little... girly?", this)));
            }
            else if (gender == FEMALE)
            {
                gender = MALE;
                if (Dm.truthy(awakened))
                    name = "His Grace";
                else if (!Dm.truthy(ascended))
                    desc = "A toolbox painted bright green. Looking at it makes you feel uneasy.";
                icon_state = "green";
                inhand_icon_state = "toolbox_green";
                T.visible_message(span_boldwarning(I18n.T("[] begins to look a little more... manly?", this)));
            }
            if (holder != null)
                holder.remove_status_effect(typeof(datum_status_effect_his_grace));
            qdel(tool);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:61
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSprocessing, this);
        Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this));
        RegisterSignal(this, COMSIG_MOVABLE_POST_THROW, nameof(move_gracefully));
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:68
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSprocessing, this);
        foreach (mob_living L in Dm.each<mob_living>(contents))
            Waiting.NoSleep(L.forceMove(get_turf(this)));
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:74
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(ascended))
        {
            icon_state = "gold";
            inhand_icon_state = "toolbox_gold";
        }
        else if (gender == MALE)
        {
            icon_state = "green";
            inhand_icon_state = "toolbox_green";
        }
        else if (gender == FEMALE)
        {
            icon_state = "pink";
            inhand_icon_state = "toolbox_pink";
        }
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:86
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(ascended))
            Dm.append(ref dot, "triple_latch");
        else if (Dm.truthy(awakened))
            Dm.append(ref dot, "single_latch_open");
        else
            Dm.append(ref dot, "single_latch");
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:95
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(awakened))
            Waiting.Detach(awaken(user));
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:99
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living M = target_mob;
        if (Dm.truthy(awakened) && IS_UNCONSCIOUS_OR_CRIT(M))
        {
            if (gender == FEMALE)
            {
                float dx = M.x - user.x;
                float dy = M.y - user.y;
                if (dx != 0 && dy != 0)
                {
                    obj_item_reagent_containers_spray_chemsprayer_party party_popper = Dm.@new<obj_item_reagent_containers_spray_chemsprayer_party>(get_turf(user));
                    dx = dx / abs(dx);
                    dy = dy / abs(dy);
                    await party_popper.spray(locate(M.x + dx * 2, M.y + dy * 2, M.z), user);
                    qdel(party_popper);
                }
            }
            await consume(M);
            return null;
        }
        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:114
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        //you can't pull his grace
        return NONE;
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:118
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(awakened))
        {
            // DM's switch takes the first range that holds the value
            if (bloodthirst >= HIS_GRACE_SATIATED && bloodthirst <= HIS_GRACE_PECKISH)
                Dm.append(ref dot, span_his_grace(I18n.T("[] isn't very hungry. Not yet.", this)));
            else if (bloodthirst >= HIS_GRACE_PECKISH && bloodthirst <= HIS_GRACE_HUNGRY)
                Dm.append(ref dot, span_his_grace(I18n.T("[] would like a snack.", this)));
            else if (bloodthirst >= HIS_GRACE_HUNGRY && bloodthirst <= HIS_GRACE_FAMISHED)
                Dm.append(ref dot, span_his_grace(I18n.T("[] is quite hungry now.", this)));
            else if (bloodthirst >= HIS_GRACE_FAMISHED && bloodthirst <= HIS_GRACE_STARVING)
                Dm.append(ref dot, span_his_grace(I18n.T("[] is openly salivating at the sight of you. Be careful.", this)));
            else if (bloodthirst >= HIS_GRACE_STARVING && bloodthirst <= HIS_GRACE_CONSUME_OWNER)
                Dm.append(ref dot, "<span class='his_grace bold'>" + I18n.T("You walk a fine line. [] is very close to devouring you.", this) + "</span>");
            else if (bloodthirst >= HIS_GRACE_CONSUME_OWNER && bloodthirst <= HIS_GRACE_FALL_ASLEEP)
                Dm.append(ref dot, "<span class='his_grace bold'>" + I18n.T("[] is shaking violently and staring directly at you.", this) + "</span>");
        }
        else
            Dm.append(ref dot, span_his_grace(I18n.T("[] is latched closed.", this)));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:137
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null) //Allows changelings, etc. to climb out of Him after they revive, provided He isn't active
    {
        if (!Dm.truthy(awakened))
        {
            await user.forceMove(get_turf(this));
            user.visible_message(span_warning(I18n.T("[] scrambles out of []!", user, this)), span_notice(I18n.T("You climb out of []!", this)));
        }
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:142
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        float seconds = Dm.num(seconds_per_tick);
        if (!Dm.truthy(bloodthirst))
        {
            drowse();
            return null;
        }
        if (bloodthirst < HIS_GRACE_CONSUME_OWNER && !Dm.truthy(ascended))
            adjust_bloodthirst((1 + FLOOR(Dm.num(LAZYLEN(contents)) * 0.5f, 1)) * seconds); //Maybe adjust this?
        else
            adjust_bloodthirst(1 * seconds); //don't cool off rapidly once we're at the point where His Grace consumes all.
        // tg types the result as a mob; the topmost thing that is no mob (His Grace on the floor, a locker) holds no hands
        mob_living master = get_atom_on_turf(this, typeof(mob_living)) as mob_living;
        if (master != null && Dm.@in(this, master.held_items))
        {
            if (bloodthirst >= HIS_GRACE_CONSUME_OWNER && bloodthirst <= HIS_GRACE_FALL_ASLEEP)
            {
                master.visible_message(span_boldwarning(I18n.T("[] turns on []!", this, master)), "<span class='his_grace big bold'>" + I18n.T("[] turns on you!", this) + "</span>");
                Waiting.NoSleep(do_attack_animation(master, null, this));
                Waiting.NoSleep(master.emote("scream"));
                master.remove_status_effect(typeof(datum_status_effect_his_grace));
                REMOVE_TRAIT(this, TRAIT_NODROP, HIS_GRACE_TRAIT);
                Waiting.NoSleep(master.Paralyze(60));
                Waiting.NoSleep(master.adjust_brute_loss(master.maxHealth));
                playsound(master, "sound/effects/splat.ogg", 100, FALSE);
            }
            else
                Waiting.NoSleep(master.apply_status_effect(typeof(datum_status_effect_his_grace), gender));
            return null;
        }
        Waiting.NoSleep(forceMove(get_turf(this))); //no you can't put His Grace in a locker you just have to deal with Him
        if (bloodthirst < HIS_GRACE_CONSUME_OWNER)
            return null;
        if (bloodthirst >= HIS_GRACE_FALL_ASLEEP)
        {
            drowse();
            return null;
        }
        DmList targets = Dm.list();
        foreach (mob_living each in Dm.each<mob_living>(oview(2, this)))
            targets.Add(each);
        if (!Dm.truthy(LAZYLEN(targets)))
            return null;
        mob_living L = pick(targets) as mob_living;
        Waiting.NoSleep(step_to(this, L));
        if (Dm.truthy(Waiting.NoSleep(Adjacent(L))))
        {
            if (!IS_UNCONSCIOUS_OR_CRIT(L))
            {
                L.visible_message(span_warning(I18n.T("[] lunges at []!", this, L)), "<span class='his_grace big bold'>" + I18n.T("[] lunges at you!", this) + "</span>");
                Waiting.NoSleep(do_attack_animation(L, null, this));
                playsound(L, "sound/items/weapons/smash.ogg", 50, TRUE);
                playsound(L, "sound/effects/desecration/desecration-01.ogg", 50, TRUE);
                Waiting.NoSleep(L.adjust_brute_loss(force));
                adjust_bloodthirst(-5); //Don't stop attacking they're right there!
            }
            else
                Waiting.NoSleep(consume(L));
        }
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:189
    public virtual async DmTask awaken(mob user) //Good morning, Mr. Grace.
    {
        if (Dm.truthy(awakened))
            return;
        awakened = TRUE;
        user.visible_message(span_boldwarning(I18n.T("[] begins to rattle. [] thirsts.", this, p_They())), span_his_grace(I18n.T("You flick []'s latch up. You hope this is a good idea.", this)));
        name = Dm.text("[]", p_Their()) + " Grace";
        desc = "A bloodthirsty artifact created by a profane rite.";
        adjust_bloodthirst(1);
        force_bonus = HIS_GRACE_FORCE_BONUS * Dm.num(LAZYLEN(contents));
        notify_ghosts(
            I18n.T("[] has awoken []!", user.real_name, this),
            source: this,
            header: I18n.T("All Hail []!", this));
        playsound(user, "sound/effects/pope_entry.ogg", 100);
        update_appearance();
        move_gracefully();
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:207
    [SignalHandler]
    public virtual void move_gracefully()
    {
        if (!Dm.truthy(awakened))
            return;

        spasm_animation();
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:215
    public virtual void drowse() //Good night, Mr. Grace.
    {
        if (!Dm.truthy(awakened) || Dm.truthy(ascended))
            return;
        turf T = get_turf(this);
        T.visible_message(span_boldwarning(I18n.T("[] slowly stops rattling and falls still, [] latch snapping shut.", this, p_Their())));
        playsound(loc, "sound/items/weapons/batonextend.ogg", 100, TRUE);
        name = Dm.initial(this, (obj_item_his_grace p) => p.name);
        if (gender == MALE)
            desc = "A toolbox painted bright green. Looking at it makes you feel uneasy.";
        else if (gender == FEMALE)
            desc = "A toolbox painted bright pink. Looking at it makes you feel uneasy.";
        animate(this, Dm.named("transform", Dm.matrix()));
        force = Dm.initial(this, (obj_item_his_grace p) => p.force);
        force_bonus = Dm.initial(this, (obj_item_his_grace p) => p.force_bonus);
        awakened = FALSE;
        bloodthirst = 0;
        update_appearance();
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:233
    public virtual async DmTask consume(mob_living meal) //Here's your dinner, Mr. Grace.
    {
        if (meal == null)
            return;
        int victims = 0;
        meal.visible_message(span_warning(I18n.T("[] swings open and devours []!", this, meal)), "<span class='his_grace big bold'>" + I18n.T("[] consumes you!", this) + "</span>");
        await meal.adjust_brute_loss(200);
        playsound(meal, "sound/effects/desecration/desecration-02.ogg", 75, TRUE);
        playsound(this, "sound/items/eatfood.ogg", 100, TRUE);
        await meal.forceMove(this);
        force_bonus += HIS_GRACE_FORCE_BONUS;
        prev_bloodthirst = bloodthirst;
        if (prev_bloodthirst < HIS_GRACE_CONSUME_OWNER)
            bloodthirst = max(Dm.num(LAZYLEN(contents)), 1); //Never fully sated, and His hunger will only grow.
        else
            bloodthirst = HIS_GRACE_CONSUME_OWNER;
        foreach (mob_living C in Dm.each<mob_living>(contents))
        {
            if (C.mind != null)
                victims++;
        }
        if (victims >= victims_needed)
            await ascend();
        update_stats();
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:255
    public virtual void adjust_bloodthirst(float amt)
    {
        prev_bloodthirst = bloodthirst;
        if (prev_bloodthirst < HIS_GRACE_CONSUME_OWNER && !Dm.truthy(ascended))
            bloodthirst = clamp(bloodthirst + amt, HIS_GRACE_SATIATED, HIS_GRACE_CONSUME_OWNER);
        else if (!Dm.truthy(ascended))
            bloodthirst = clamp(bloodthirst + amt, HIS_GRACE_CONSUME_OWNER, HIS_GRACE_FALL_ASLEEP);
        update_stats();
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:263
    public virtual void update_stats()
    {
        REMOVE_TRAIT(this, TRAIT_NODROP, HIS_GRACE_TRAIT);
        mob_living master = get_atom_on_turf(this, typeof(mob_living)) as mob_living;
        if (master == null)
            return;
        // DM's switch takes the first range that holds the value
        if (bloodthirst >= HIS_GRACE_CONSUME_OWNER && bloodthirst <= HIS_GRACE_FALL_ASLEEP)
        {
            if (HIS_GRACE_CONSUME_OWNER > prev_bloodthirst)
                master.visible_message(span_userdanger(I18n.T("[] enters a frenzy!", this)));
        }
        else if (bloodthirst >= HIS_GRACE_STARVING && bloodthirst <= HIS_GRACE_CONSUME_OWNER)
        {
            ADD_TRAIT(this, TRAIT_NODROP, HIS_GRACE_TRAIT);
            if (HIS_GRACE_STARVING > prev_bloodthirst)
            {
                master.visible_message(span_boldwarning(I18n.T("[] is starving!", this)), "<span class='his_grace big'>" + I18n.T("[]'s bloodlust overcomes you. [] must be fed, or you will become [] meal.", this, this, p_Their())
                    + (force_bonus < 15 ? I18n.T(" And still, [] power grows.", p_Their()) : "") + "</span>");
                force_bonus = max(force_bonus, 15);
            }
        }
        else if (bloodthirst >= HIS_GRACE_FAMISHED && bloodthirst <= HIS_GRACE_STARVING)
        {
            ADD_TRAIT(this, TRAIT_NODROP, HIS_GRACE_TRAIT);
            if (HIS_GRACE_FAMISHED > prev_bloodthirst)
            {
                master.visible_message(span_warning(I18n.T("[] is very hungry!", this)), "<span class='his_grace big'>" + I18n.T("Spines sink into your hand. [] must feed immediately.", this)
                    + (force_bonus < 10 ? I18n.T(" [] power grows.", p_Their()) : "") + "</span>");
                force_bonus = max(force_bonus, 10);
            }
            if (prev_bloodthirst >= HIS_GRACE_STARVING)
                master.visible_message(span_warning(I18n.T("[] is now only very hungry!", this)), "<span class='his_grace big'>" + I18n.T("Your bloodlust recedes.") + "</span>");
        }
        else if (bloodthirst >= HIS_GRACE_HUNGRY && bloodthirst <= HIS_GRACE_FAMISHED)
        {
            if (HIS_GRACE_HUNGRY > prev_bloodthirst)
            {
                master.visible_message(span_warning(I18n.T("[] is getting hungry.", this)), "<span class='his_grace big'>" + I18n.T("You feel []'s hunger within you.", this)
                    + (force_bonus < 5 ? I18n.T(" [] power grows.", p_Their()) : "") + "</span>");
                force_bonus = max(force_bonus, 5);
            }
            if (prev_bloodthirst >= HIS_GRACE_FAMISHED)
                master.visible_message(span_warning(I18n.T("[] is now only somewhat hungry.", this)), span_his_grace(I18n.T("[]'s hunger recedes a little...", this)));
        }
        else if (bloodthirst >= HIS_GRACE_PECKISH && bloodthirst <= HIS_GRACE_HUNGRY)
        {
            if (HIS_GRACE_PECKISH > prev_bloodthirst)
                master.visible_message(span_warning(I18n.T("[] is feeling snackish.", this)), span_his_grace(I18n.T("[] begins to hunger.", this)));
            if (prev_bloodthirst >= HIS_GRACE_HUNGRY)
                master.visible_message(span_warning(I18n.T("[] is now only a little peckish.", this)), "<span class='his_grace big'>" + I18n.T("[]'s hunger recedes somewhat...", this) + "</span>");
        }
        else if (bloodthirst >= HIS_GRACE_SATIATED && bloodthirst <= HIS_GRACE_PECKISH)
        {
            if (prev_bloodthirst >= HIS_GRACE_PECKISH)
                master.visible_message(span_warning(I18n.T("[] is satiated.", this)), "<span class='his_grace big'>" + I18n.T("[]'s hunger recedes...", this) + "</span>");
        }
        force = Dm.initial(this, (obj_item_his_grace p) => p.force) + force_bonus;
    }

    // tg: code/game/objects/items/weaponry/melee/his_grace.dm:303
    public virtual async DmTask ascend()
    {
        if (Dm.truthy(ascended))
            return;
        mob_living_carbon_human master = loc as mob_living_carbon_human;
        force_bonus += ascend_bonus;
        desc = "A legendary toolbox and a distant artifact from The Age of Three Powers. On its three latches engraved are the words \"The Sun\", \"The Moon\", and \"The Stars\". The entire toolbox has the words \"The World\" engraved into its sides.";
        ascended = TRUE;
        update_appearance();
        playsound(this, "sound/effects/his_grace/his_grace_ascend.ogg", 100);
        if (master != null)
        {
            await master.update_held_items();
            master.visible_message("<span class='his_grace big bold'>" + I18n.T("Gods will be watching.") + "</span>");
            name = I18n.T("[]'s mythical toolbox of three powers", master);
            // master.client?.give_award(/datum/award/achievement/misc/ascension, master)
            Dm.Deferred("code/game/objects/items/weaponry/melee/his_grace.dm:316", "ascend: client.give_award(/datum/award/achievement/misc/ascension) (the achievements)");
        }
    }
}
