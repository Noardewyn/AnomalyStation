// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/changeling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the changeling antagonist (its name from the hive, the cellular emporium and its action, the
// innate powers, the first DNA profile, the objectives, the chemical HUD and its regeneration on Life, the click stings, the brain
// decoy, the purchase and readaptation of powers, the DNA profiles and their absorption checks, the transformation into a profile
// with its flesh disguise items, skillchips and scars, the admins' "Transform to initial appearance", the round-end report, the
// preview picture and the AntagInfoChangeling window's data), the changeling profile, the headslug and space changelings and their
// outfits. The var blocks are generated (D111); the type statics (all_powers, possible_changeling_IDs, slot2type) and transform's
// slot2slot are TgState fields (rules 10.6). The athletics skill of a profile came with gaps SL1 (D333).

// tg: code/modules/antagonists/changeling/changeling.dm:6
public partial class datum_antagonist_changeling : datum_antagonist
{
    // tg: code/modules/antagonists/changeling/changeling.dm:2  #define FORMAT_CHEM_MAX_TEXT(charges) (file-local, #undef at :1072)
    private static string FORMAT_CHEM_MAX_TEXT(object charges) => MAPTEXT(Dm.text("<div align='center' valign='middle' style='position:relative; top:0px; left:6px'><font color='#dd2828'>[]</font></div>", round(Dm.num(charges))));
    // tg: code/modules/antagonists/changeling/changeling.dm:4  #define FORMAT_CHEM_CHARGES_TEXT(charges) (file-local, #undef at :1073)
    private static string FORMAT_CHEM_CHARGES_TEXT(object charges) => MAPTEXT(Dm.text("<div align='center' valign='middle' style='position:relative; top:0px; left:6px'><font color='#dd66dd'>[]</font></div>", round(Dm.num(charges))));

    /// Static typecache of all changeling powers that are usable.
    // tg: code/modules/antagonists/changeling/changeling.dm:78  var/static/list/all_powers = typecacheof(/datum/action/changeling, ignore_root_path = TRUE)
    public static DmList all_powers
    {
        get => TgState.Current.datum_antagonist_changeling__all_powers ??= typecacheof(typeof(datum_action_changeling), ignore_root_path: TRUE) as DmList;
        set => TgState.Current.datum_antagonist_changeling__all_powers = value;
    }

    /// Static list of possible ids. Initialized into the greek alphabet the first time it is used
    // tg: code/modules/antagonists/changeling/changeling.dm:81  var/static/list/possible_changeling_IDs
    public static DmList possible_changeling_IDs
    {
        get => TgState.Current.datum_antagonist_changeling__possible_changeling_IDs;
        set => TgState.Current.datum_antagonist_changeling__possible_changeling_IDs = value;
    }

    /// Satic list of what each slot associated with (in regard to changeling flesh items).
    // tg: code/modules/antagonists/changeling/changeling.dm:84  var/static/list/slot2type
    public static DmList slot2type
    {
        get => TgState.Current.datum_antagonist_changeling__slot2type ??= Dm.list(
            Dm.kv("head", typeof(obj_item_clothing_head_changeling)),
            Dm.kv("wear_mask", typeof(obj_item_clothing_mask_changeling)),
            Dm.kv("wear_neck", typeof(obj_item_changeling)),
            Dm.kv("back", typeof(obj_item_changeling)),
            Dm.kv("wear_suit", typeof(obj_item_clothing_suit_changeling)),
            Dm.kv("w_uniform", typeof(obj_item_clothing_under_changeling)),
            Dm.kv("shoes", typeof(obj_item_clothing_shoes_changeling)),
            Dm.kv("belt", typeof(obj_item_changeling)),
            Dm.kv("gloves", typeof(obj_item_clothing_gloves_changeling)),
            Dm.kv("glasses", typeof(obj_item_clothing_glasses_changeling)),
            Dm.kv("ears", typeof(obj_item_changeling)),
            Dm.kv("wear_id", typeof(obj_item_changeling_id)),
            Dm.kv("s_store", typeof(obj_item_changeling)));
        set => TgState.Current.datum_antagonist_changeling__slot2type = value;
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:757  transform's var/static/list/slot2slot
    private static DmList transform__slot2slot => TgState.Current.datum_antagonist_changeling__transform__slot2slot ??= Dm.list(
        Dm.kv("head", ITEM_SLOT_HEAD),
        Dm.kv("wear_mask", ITEM_SLOT_MASK),
        Dm.kv("wear_neck", ITEM_SLOT_NECK),
        Dm.kv("back", ITEM_SLOT_BACK),
        Dm.kv("wear_suit", ITEM_SLOT_OCLOTHING),
        Dm.kv("w_uniform", ITEM_SLOT_ICLOTHING),
        Dm.kv("shoes", ITEM_SLOT_FEET),
        Dm.kv("belt", ITEM_SLOT_BELT),
        Dm.kv("gloves", ITEM_SLOT_GLOVES),
        Dm.kv("glasses", ITEM_SLOT_EYES),
        Dm.kv("ears", ITEM_SLOT_EARS),
        Dm.kv("wear_id", ITEM_SLOT_ID),
        Dm.kv("s_store", ITEM_SLOT_SUITSTORE));

    // tg: code/modules/antagonists/changeling/changeling.dm:106
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        hive_name = GlobalProcs.hive_name();
        foreach (datum_antagonist_changeling other_ling in Dm.each<datum_antagonist_changeling>(GLOB.antagonists))
        {
            if (other_ling.owner == null || other_ling.owner == owner)
                continue;
            competitive_objectives = TRUE;
            break;
        }
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:115
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref emporium_action);
        QDEL_NULL(ref cellular_emporium);
        current_profile = null;
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:121
    public override async DmTask on_gain()
    {
        generate_name();
        await create_emporium();
        await create_innate_actions();
        await create_initial_profile();
        if (Dm.truthy(give_objectives))
            await forge_objectives();
        owner.current.get_language_holder().omnitongue = TRUE;
        await base.on_gain();
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:131
    public override async DmTask<object> apply_innate_effects(mob_living mob_override = null)
    {
        mob mob_to_tweak = Dm.truthy(mob_override) ? mob_override : owner.current;
        if (!isliving(mob_to_tweak))
            return null;

        mob_living living_mob = mob_to_tweak as mob_living;
        await handle_clown_mutation(living_mob, I18n.T("You have evolved beyond your clownish nature, allowing you to wield weapons without harming yourself."));
        RegisterSignal(living_mob, COMSIG_MOB_LOGIN, nameof(on_login));
        RegisterSignal(living_mob, COMSIG_LIVING_LIFE, nameof(on_life));
        RegisterSignal(living_mob, COMSIG_LIVING_POST_FULLY_HEAL, nameof(on_fullhealed));
        RegisterSignal(living_mob, COMSIG_CARBON_GAIN_ORGAN, nameof(new_brain));
        RegisterSignal(living_mob, COMSIG_MOB_HUD_CREATED, nameof(on_hud_created));
        RegisterSignals(living_mob, Dm.list(COMSIG_MOB_MIDDLECLICKON, COMSIG_MOB_ALTCLICKON), nameof(on_click_sting));
        ADD_TRAIT(living_mob, TRAIT_FAKE_SOULLESS, CHANGELING_TRAIT);
        ADD_TRAIT(living_mob, TRAIT_BRAINLESS_CARBON, CHANGELING_TRAIT);
        ADD_TRAIT(living_mob, TRAIT_CHANGELING_HIVEMIND, CHANGELING_TRAIT);

        if (living_mob.hud_used != null)
            on_hud_created();

        await make_brain_decoy(living_mob);
        return null;
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:153
    public virtual async DmTask make_brain_decoy(mob_living ling = null)
    {
        obj_item_organ_brain our_ling_brain = ling.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (our_ling_brain == null || Dm.truthy(our_ling_brain.decoy_override))
            return;

        // Brains are optional for lings.
        // This is automatically cleared if the ling is.
        await our_ling_brain.AddComponent(typeof(datum_component_ling_decoy_brain), this);
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:162
    public virtual void generate_name()
    {
        string honorific = null;
        if (Dm.eq(owner.current.gender, FEMALE))
            honorific = "Ms.";
        else if (Dm.eq(owner.current.gender, MALE))
            honorific = "Mr.";
        else
            honorific = "Mx.";

        if (!Dm.truthy(possible_changeling_IDs))
            possible_changeling_IDs = GLOB.greek_letters.Copy();
        if (Dm.truthy(possible_changeling_IDs.len))
            changelingID = Dm.text("[] []", honorific, pick_n_take(possible_changeling_IDs));
        else
            changelingID = Dm.text("[] []", honorific, rand(1, 999));
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:178
    [SignalHandler]
    public virtual void on_hud_created(datum source = null)
    {
        datum_hud ling_hud = owner.current.hud_used;
        Waiting.NoSleep(ling_hud.add_screen_object(typeof(atom_movable_screen_ling_chems), HUD_CHANGELING_CHEMS, HUD_GROUP_INFO));
        Waiting.NoSleep(ling_hud.add_screen_object(typeof(atom_movable_screen_ling_sting), HUD_CHANGELING_STING, HUD_GROUP_INFO, update_screen: TRUE));
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:185
    [SignalHandler]
    public virtual void new_brain(mob_living_carbon ling = null, obj_item_organ new_brain = null)
    {
        if (new_brain is not obj_item_organ_brain)
            return;
        Waiting.NoSleep(make_brain_decoy(ling));
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:192
    public override async DmTask remove_innate_effects(mob_living mob_override = null)
    {
        mob_living living_mob = Dm.truthy(mob_override) ? mob_override : owner.current as mob_living;
        await handle_clown_mutation(living_mob, removing: FALSE);
        UnregisterSignal(living_mob, Dm.list(
            COMSIG_MOB_LOGIN,
            COMSIG_LIVING_LIFE,
            COMSIG_LIVING_POST_FULLY_HEAL,
            COMSIG_MOB_MIDDLECLICKON,
            COMSIG_MOB_ALTCLICKON,
            COMSIG_MOB_HUD_CREATED,
            COMSIG_CARBON_GAIN_ORGAN));
        REMOVE_TRAIT(living_mob, TRAIT_FAKE_SOULLESS, CHANGELING_TRAIT);
        REMOVE_TRAIT(living_mob, TRAIT_BRAINLESS_CARBON, CHANGELING_TRAIT);
        REMOVE_TRAIT(living_mob, TRAIT_CHANGELING_HIVEMIND, CHANGELING_TRAIT);

        foreach (mob_eye_imaginary_friend_hivemind hivemind_member in Dm.each<mob_eye_imaginary_friend_hivemind>(living_mob.imaginary_group))
            qdel(hivemind_member);

        if (living_mob.hud_used == null)
            return;

        datum_hud hud_used = living_mob.hud_used;
        await hud_used.remove_screen_object(HUD_CHANGELING_CHEMS, update: FALSE);
        await hud_used.remove_screen_object(HUD_CHANGELING_STING);
        // The old body's brain still remains a decoy, I guess?
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:219
    public override async DmTask on_removal()
    {
        remove_changeling_powers(include_innate: TRUE);
        await base.on_removal();
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:223
    public override async DmTask farewell()
    {
        if (owner.current != null)
            to_chat(owner.current, span_userdanger(I18n.T("You grow weak and lose your powers! You are no longer a changeling and are stuck in your current form!")));
        await DmTask.CompletedTask;
    }

    /*
     * Instantiate the cellular emporium for the changeling.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:230
    public virtual async DmTask create_emporium()
    {
        cellular_emporium = Dm.@new<datum_cellular_emporium>(this);
        emporium_action = Dm.@new<datum_action_cellular_emporium>(cellular_emporium);
        await emporium_action.Grant(owner.current);
    }

    /*
     * Instantiate all the default actions of a ling (transform, dna sting, absorb, etc)
     * Any Changeling action with dna_cost = CHANGELING_POWER_INNATE will be added here automatically
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:239
    public virtual async DmTask create_innate_actions()
    {
        foreach (Type path in Dm.each_cast<Type>(all_powers))
        {
            if (Dm.initial(path, (datum_action_changeling p) => p.dna_cost) != CHANGELING_POWER_INNATE)
                continue;

            datum_action_changeling innate_ability = await Dm.new_async(path) as datum_action_changeling;
            Dm.append(ref innate_powers, innate_ability);
            await innate_ability.on_purchase(owner.current, TRUE);
        }
    }

    /*
     * Signal proc for [COMSIG_MOB_LOGIN].
     * Gives us back our action buttons if we lose them on log-in.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:252
    [SignalHandler]
    public virtual void on_login(datum source = null)
    {
        if (!isliving(source))
            return;
        mob_living living_source = source as mob_living;
        if (living_source.mind == null)
            return;

        Waiting.NoSleep(regain_powers());
    }

    /**
     * Signal proc for [COMSIG_LIVING_LIFE].
     * Handles regenerating chemicals on life ticks.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:267
    [SignalHandler]
    public virtual void on_life(datum source = null, object seconds_per_tick = null)
    {
        float delta_time = DELTA_WORLD_TIME(SSmobs);
        mob_living living_owner = owner.current as mob_living;

        // If dead, we only regenerate up to half chem storage.
        if (owner.current.stat == DEAD)
            adjust_chemicals((chem_recharge_rate - chem_recharge_slowdown) * delta_time, total_chem_storage * 0.5f);

        // If we're not dead and not on fire - we go up to the full chem cap at normal speed. If on fire we only regenerate at 1/4th the normal speed
        else
        {
            if (Dm.truthy(living_owner.fire_stacks) && Dm.truthy(living_owner.on_fire))
                adjust_chemicals((chem_recharge_rate - 0.75f) * delta_time);
            else
                adjust_chemicals((chem_recharge_rate - chem_recharge_slowdown) * delta_time);
        }
    }

    /**
     * Signal proc for [COMSIG_LIVING_POST_FULLY_HEAL]
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:287
    [SignalHandler]
    public virtual void on_fullhealed(mob_living source = null, object heal_flags = null)
    {
        // Aheal restores all chemicals
        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_ADMIN)))
            adjust_chemicals(INFINITY);

        // Makes sure the brain, if recreated, is a decoy as expected
        Waiting.NoSleep(make_brain_decoy(source));
    }

    /**
     * Signal proc for [COMSIG_MOB_MIDDLECLICKON] and [COMSIG_MOB_ALTCLICKON].
     * Allows the changeling to sting people with a click.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:301
    [SignalHandler]
    public virtual object on_click_sting(mob_living ling = null, atom clicked = null)
    {
        // nothing to handle
        if (chosen_sting == null)
            return null;
        if (!isliving(ling) || clicked == ling || IS_UNCONSCIOUS_OR_CRIT(ling))
            return null;
        // sort-of hack done here: we use in_given_range here because it's quicker.
        // actual ling stings do pathfinding to determine whether the target's "in range".
        // however, this is "close enough" preliminary checks to not block click
        if (!isliving(clicked) || !IN_GIVEN_RANGE(ling, clicked, sting_range))
            return null;

        Waiting.Detach(chosen_sting.try_to_sting(ling, clicked as mob_living));

        return COMSIG_MOB_CANCEL_CLICKON;
    }

    /*
     * Adjust the chem charges of the ling by [amount]
     * and clamp it between 0 and override_cap (if supplied) or total_chem_storage (if no override supplied)
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:323
    public virtual void adjust_chemicals(object amount = null, object override_cap = null)
    {
        if (!isnum(amount))
            return;
        float cap_to = isnum(override_cap) ? Dm.num(override_cap) : total_chem_storage;
        chem_charges = clamp(chem_charges + Dm.num(amount), 0, cap_to);
        update_chemical_hud(chem_charges);
    }

    ///Updates the Changeling's chemical HUD to display the information we want it to (chem charges, or max if hovered over).
    // tg: code/modules/antagonists/changeling/changeling.dm:331
    public virtual void update_chemical_hud(object amount = null)
    {
        atom_movable_screen_ling_chems chems = owner.current?.hud_used?.screen_objects[HUD_CHANGELING_CHEMS] as atom_movable_screen_ling_chems;
        if (chems == null)
            return;
        if (Dm.truthy(chems.hovering))
            chems.maptext = FORMAT_CHEM_MAX_TEXT(total_chem_storage);
        else
            chems.maptext = FORMAT_CHEM_CHARGES_TEXT(amount);
    }

    /*
     * Remove changeling powers from the current Changeling's purchased_powers list.
     *
     * if [include_innate] = TRUE, will also remove all powers from the Changeling's innate_powers list.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:345
    public virtual void remove_changeling_powers(float? include_innate = null)
    {
        include_innate ??= FALSE;
        if (!isliving(owner.current))
            return;

        if (chosen_sting != null)
            chosen_sting.unset_sting(owner.current);

        QDEL_LIST_ASSOC_VAL(purchased_powers);
        if (Dm.truthy(include_innate))
            QDEL_LIST(innate_powers);

        genetic_points = total_genetic_points;
        chem_charges = min(chem_charges, total_chem_storage);
        chem_recharge_rate = Dm.initial(this, (datum_antagonist_changeling p) => p.chem_recharge_rate);
        chem_recharge_slowdown = Dm.initial(this, (datum_antagonist_changeling p) => p.chem_recharge_slowdown);
    }

    /*
     * For resetting all of the changeling's action buttons. (IE, re-granting them all.)
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:364
    public virtual async DmTask regain_powers()
    {
        await emporium_action.Grant(owner.current);
        foreach (datum_action_changeling power in Dm.each_cast<datum_action_changeling>(innate_powers))
            await power.on_purchase(owner.current);

        foreach (object power_path in Dm.each(purchased_powers))
        {
            datum_action_changeling power = purchased_powers[power_path] as datum_action_changeling;
            if (power is datum_action_changeling)
                await power.on_purchase(owner.current);
        }
    }

    /*
     * The act of purchasing a certain power for a changeling.
     *
     * [sting_path] - the power that's being purchased / evolved.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:379
    public virtual async DmTask<object> purchase_power(object sting_path = null)
    {
        if (!ispath(sting_path, typeof(datum_action_changeling)))
            throw Dm.CRASH(Dm.text("Changeling purchase_power attempted to purchase an invalid typepath! (got: [])", sting_path), "code/modules/antagonists/changeling/changeling.dm", 381);

        Type sting_type = sting_path as Type;
        if (Dm.truthy(purchased_powers[sting_path]))
        {
            to_chat(owner.current, span_warning(I18n.T("We have already evolved this ability!")));
            return FALSE;
        }

        if (genetic_points < Dm.initial(sting_type, (datum_action_changeling p) => p.dna_cost))
        {
            to_chat(owner.current, span_warning(I18n.T("We have reached our capacity for abilities!")));
            return FALSE;
        }

        if (absorbed_count < Dm.initial(sting_type, (datum_action_changeling p) => p.req_dna))
        {
            to_chat(owner.current, span_warning(I18n.T("We lack the DNA to evolve this ability!")));
            return FALSE;
        }

        if (true_absorbs < Dm.initial(sting_type, (datum_action_changeling p) => p.req_absorbs))
        {
            to_chat(owner.current, span_warning(I18n.T("We lack the absorbed DNA to evolve this ability!")));
            return FALSE;
        }

        if (Dm.initial(sting_type, (datum_action_changeling p) => p.dna_cost) < 0)
        {
            to_chat(owner.current, span_warning(I18n.T("We cannot evolve this ability!")));
            return FALSE;
        }

        //To avoid potential exploits by buying new powers while in stasis, which clears your verblist. // Probably not a problem anymore, but whatever.
        if (HAS_TRAIT(owner.current, TRAIT_DEATHCOMA))
        {
            to_chat(owner.current, span_warning(I18n.T("We lack the energy to evolve new abilities right now!")));
            return FALSE;
        }

        object success = await give_power(sting_path);
        if (Dm.truthy(success))
            genetic_points -= Dm.initial(sting_type, (datum_action_changeling p) => p.dna_cost);
        return success;
    }

    /**
     * Gives a passed changeling power datum to the player
     *
     * Is passed a path to a changeling power, and applies it to the user.
     * If successful, we return TRUE, otherwise not.
     *
     * Arguments:
     * * power_path - The path of the power we will be giving to our attached player.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:423
    public virtual async DmTask<object> give_power(object power_path = null)
    {
        datum_action_changeling new_action = await Dm.new_async(power_path as Type) as datum_action_changeling;

        if (new_action == null)
        {
            to_chat(owner.current, I18n.T("This is awkward. Changeling power purchase failed, please report this bug to a coder!"));
            throw Dm.CRASH(Dm.text("Changeling give_power was unable to grant a new changeling action for path []!", power_path), "code/modules/antagonists/changeling/changeling.dm", 428);
        }

        purchased_powers[power_path] = new_action;
        await new_action.on_purchase(owner.current); // Grant() is ran in this proc, see changeling_powers.dm.
        log_changeling_power(Dm.text("[] adapted the [] power", key_name(owner), new_action.name));
        SSblackbox.record_feedback("tally", "changeling_power_purchase", 1, new_action.name);

        return TRUE;
    }

    /*
     * Changeling's ability to re-adapt all of their learned powers.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:440
    public virtual object readapt()
    {
        if (!ishuman(owner.current) || HAS_TRAIT(owner.current, TRAIT_LESSER_HUMANOID))
        {
            to_chat(owner.current, span_warning(I18n.T("We can't remove our evolutions in this form!")));
            return FALSE;
        }

        if (HAS_TRAIT_FROM(owner.current, TRAIT_DEATHCOMA, CHANGELING_TRAIT))
        {
            to_chat(owner.current, span_warning(I18n.T("We are too busy reforming ourselves to readapt right now!")));
            return FALSE;
        }

        if (!Dm.truthy(can_respec))
        {
            to_chat(owner.current, span_warning(I18n.T("You lack the power to readapt your evolutions!")));
            return FALSE;
        }

        to_chat(owner.current, span_notice(I18n.T("We have removed our evolutions from this form, and are now ready to readapt.")));
        remove_changeling_powers();
        can_respec -= 1;
        SSblackbox.record_feedback("tally", "changeling_power_purchase", 1, "Readapt");
        log_changeling_power(Dm.text("[] readapted their changeling powers", key_name(owner)));
        return TRUE;
    }

    /*
     * Get the corresponding changeling profile for the passed name.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:463
    public virtual object get_dna(object searched_dna_name = null)
    {
        foreach (datum_changeling_profile found_profile in Dm.each_cast<datum_changeling_profile>(stored_profiles))
        {
            if (Dm.eq(searched_dna_name, found_profile.name))
                return found_profile;
        }
        return null;
    }

    /*
     * Checks if we have a changeling profile with the passed DNA.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:471
    public virtual object has_profile_with_dna(datum_dna searched_dna = null)
    {
        foreach (datum_changeling_profile found_profile in Dm.each_cast<datum_changeling_profile>(stored_profiles))
        {
            if (Dm.truthy(searched_dna.is_same_as(found_profile.dna)))
                return TRUE;
        }
        return FALSE;
    }

    /*
     * Checks if this changeling can absorb the DNA of [target].
     * if [verbose] = TRUE, give feedback as to why they cannot absorb the DNA.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:481
    public virtual object can_absorb_dna(mob_living_carbon_human target = null, float? verbose = null)
    {
        verbose ??= TRUE;
        if (target == null)
            return FALSE;
        if (!iscarbon(owner.current))
            return FALSE;
        mob_living_carbon user = owner.current as mob_living_carbon;

        if (Dm.truthy(stored_profiles.len))
        {
            // If our current DNA is the stalest, we gotta ditch it before absorbing more.
            datum_changeling_profile top_profile = stored_profiles[1] as datum_changeling_profile;
            if (Dm.truthy(top_profile.dna.is_same_as(user.dna)) && stored_profiles.len > dna_max)
            {
                if (Dm.truthy(verbose))
                    to_chat(user, span_warning(I18n.T("We have reached our capacity to store genetic information! We must transform before absorbing more.")));
                return FALSE;
            }
        }

        if (!Dm.truthy(target.has_dna()))
        {
            if (Dm.truthy(verbose))
                to_chat(user, span_warning(I18n.T("[] is not compatible with our biology.", target)));
            return FALSE;
        }
        if (Dm.truthy(has_profile_with_dna(target.dna)))
        {
            if (Dm.truthy(verbose))
                to_chat(user, span_warning(I18n.T("We already have this DNA in storage!")));
            return FALSE;
        }
        if (HAS_TRAIT(target, TRAIT_NO_DNA_COPY))
        {
            if (Dm.truthy(verbose))
                to_chat(user, span_warning(I18n.T("[] is not compatible with our biology.", target)));
            return FALSE;
        }
        if (HAS_TRAIT(target, TRAIT_BADDNA))
        {
            if (Dm.truthy(verbose))
                to_chat(user, span_warning(I18n.T("[]'s DNA is ruined beyond usability!", target)));
            return FALSE;
        }
        if (HAS_TRAIT(target, TRAIT_HUSK))
        {
            if (Dm.truthy(verbose))
                to_chat(user, span_warning(I18n.T("[]'s body is ruined beyond usability!", target)));
            return FALSE;
        }
        if (!ishuman(target) || HAS_TRAIT(target, TRAIT_LESSER_HUMANOID))//Absorbing monkeys is entirely possible, but it can cause issues with transforming. That's what lesser form is for anyway!
        {
            if (Dm.truthy(verbose))
                to_chat(user, span_warning(I18n.T("We could gain no benefit from absorbing a lesser creature.")));
            return FALSE;
        }

        return TRUE;
    }

    /*
     * Create a new changeling profile datum based off of [target].
     *
     * target - the human we're basing the new profile off of.
     * protect - if TRUE, set the new profile to protected, preventing it from being removed (without force).
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:529
    public virtual async DmTask<object> create_profile(mob_living_carbon_human target = null, float? protect = null)
    {
        protect ??= 0;
        datum_changeling_profile new_profile = Dm.@new<datum_changeling_profile>();

        target.dna.real_name = target.real_name; //Set this again, just to be sure that it's properly set.

        // Set up a copy of their DNA in our profile.
        datum_dna new_dna = await Dm.new_async(target.dna.type) as datum_dna;
        await target.dna.copy_dna(new_dna);
        new_profile.dna = new_dna;
        new_profile.name = Dm.str(target.real_name);
        new_profile.@protected = Dm.num(protect);

        new_profile.age = target.age;
        new_profile.physique = target.physique;
        new_profile.athletics_level = (target.mind?.get_skill_level(typeof(datum_skill_athletics)) is var or__1 && Dm.truthy(or__1) ? or__1 : SKILL_LEVEL_NONE);

        // Grab the target's quirks.
        foreach (datum_quirk target_quirk in Dm.each_cast<datum_quirk>(target.quirks))
            LAZYADD(ref new_profile.quirks, await Dm.new_async(target_quirk.type));

        // Clothes, of course
        new_profile.underwear = target.underwear;
        new_profile.underwear_color = target.underwear_color;
        new_profile.undershirt = target.undershirt;
        new_profile.socks = target.socks;

        // Grab skillchips they have
        new_profile.skillchips = target.clone_skillchip_list(TRUE) as DmList;

        // Get any scars they may have
        foreach (datum_scar target_scar in Dm.each_cast<datum_scar>(target.all_scars))
            LAZYADD(ref new_profile.stored_scars, target_scar.format());

        // Make an icon snapshot of what they currently look like
        datum_icon_snapshot entry = Dm.@new<datum_icon_snapshot>();
        entry.name = target.name;
        entry.icon = target.icon;
        entry.icon_state = target.icon_state;
        entry.overlays = target.get_overlays_copy(Dm.list(HANDS_LAYER, HANDCUFF_LAYER, LEGCUFF_LAYER)) as DmList;
        new_profile.profile_snapshot = entry;

        // Grab the target's sechut icon.
        new_profile.id_icon = target.wear_id?.get_sechud_job_icon();
        new_profile.id_icon_state = target.wear_id?.get_sechud_job_icon_state();

        DmList slots = Dm.list("head", "wear_mask", "wear_neck", "back", "wear_suit", "w_uniform", "shoes", "belt", "gloves", "glasses", "ears", "wear_id", "s_store");
        foreach (object slot in Dm.each(slots))
        {
            if (!Dm.has_var(target, (string)slot)) // slot in target.vars
                continue;
            // DM declares it obj/item/clothing, but the slot holds any item (a backpack, an ID): typed by the vars it reads (rules 3.4)
            obj_item clothing_item = Dm.get_var(target, (string)slot) as obj_item;
            if (clothing_item == null)
                continue;
            new_profile.name_list[slot] = clothing_item.name;
            new_profile.appearance_list[slot] = clothing_item.appearance;
            new_profile.flags_cover_list[slot] = clothing_item.flags_cover;
            new_profile.lefthand_file_list[slot] = clothing_item.lefthand_file;
            new_profile.righthand_file_list[slot] = clothing_item.righthand_file;
            new_profile.inhand_icon_state_list[slot] = clothing_item.inhand_icon_state;
            new_profile.worn_icon_list[slot] = clothing_item.worn_icon;
            new_profile.worn_icon_state_list[slot] = clothing_item.worn_icon_state;
            new_profile.exists_list[slot] = 1;
        }

        new_profile.voice = target.voice;
        new_profile.voice_filter = target.voice_filter;

        return new_profile;
    }

    /*
     * Add a new profile to our changeling's profile list.
     * Pops the first profile in the list if we're above our limit of profiles.
     *
     * new_profile - the profile being added.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:602
    public virtual void add_profile(datum_changeling_profile new_profile = null)
    {
        if (stored_profiles.len > dna_max)
        {
            if (!Dm.truthy(push_out_profile()))
                return;
        }

        if (first_profile == null)
        {
            first_profile = new_profile;
            current_profile = first_profile;
        }

        Dm.append(ref stored_profiles, new_profile);
        absorbed_count++;
    }

    /*
     * Create a new profile from the given [profile_target]
     * and add it to our profile list via add_profile.
     *
     * profile_target - the human we're making a profile based off of
     * protect - if TRUE, mark the new profile as protected. If protected, it cannot be removed / popped from the profile list (without force).
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:621
    public virtual async DmTask<object> add_new_profile(mob_living_carbon_human profile_target = null, float? protect = null)
    {
        protect ??= FALSE;
        datum_changeling_profile new_profile = await create_profile(profile_target, protect) as datum_changeling_profile;
        add_profile(new_profile);
        return new_profile;
    }

    /*
     * Remove a given profile from the profile list.
     *  *
     * profile_target - the human we want to remove from our profile list (looks for a profile with a matching name)
     * force - if TRUE, removes the profile even if it's protected.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:632
    public virtual void remove_profile(mob_living_carbon_human profile_target = null, float? force = null)
    {
        force ??= FALSE;
        foreach (datum_changeling_profile found_profile in Dm.each_cast<datum_changeling_profile>(stored_profiles))
        {
            if (Dm.eq(profile_target.real_name, found_profile.name))
            {
                if (Dm.truthy(found_profile.@protected) && !Dm.truthy(force))
                    continue;
                Dm.remove(ref stored_profiles, found_profile);
                qdel(found_profile);
            }
        }
    }

    /*
     * Removes the highest changeling profile from the list
     * that isn't protected and returns TRUE if successful.
     *
     * Returns TRUE if a profile was removed, FALSE otherwise.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:646
    public virtual object push_out_profile()
    {
        datum_changeling_profile profle_to_remove = null;
        foreach (datum_changeling_profile found_profile in Dm.each_cast<datum_changeling_profile>(stored_profiles))
        {
            if (!Dm.truthy(found_profile.@protected))
            {
                profle_to_remove = found_profile;
                break;
            }
        }

        if (profle_to_remove != null)
        {
            Dm.remove(ref stored_profiles, profle_to_remove);
            return TRUE;
        }
        return FALSE;
    }

    /*
     * Create a profile based on the changeling's initial appearance.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:661
    public virtual async DmTask create_initial_profile()
    {
        if (!ishuman(owner.current))
            return;

        await add_new_profile(owner.current as mob_living_carbon_human);
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:667
    public override async DmTask forge_objectives(params object[] __extra)
    {
        float escape_objective_possible = TRUE;

        float sw__1 = Dm.truthy(competitive_objectives) ? rand(1, 3) : 1;
        switch (sw__1)
        {
            case 1:
            {
                datum_objective_absorb absorb_objective = Dm.@new<datum_objective_absorb>();
                absorb_objective.owner = owner;
                absorb_objective.gen_amount_goal(6, 8);
                Dm.append(ref objectives, absorb_objective);
                break;
            }
            case 2:
            {
                datum_objective_absorb_most ac = Dm.@new<datum_objective_absorb_most>();
                ac.owner = owner;
                Dm.append(ref objectives, ac);
                break;
            }
            case 3:
            {
                datum_objective_absorb_changeling ac = Dm.@new<datum_objective_absorb_changeling>();
                ac.owner = owner;
                Dm.append(ref objectives, ac);
                break;
            }
        }

        if (prob(60))
        {
            datum_objective_steal steal_objective = Dm.@new<datum_objective_steal>();
            steal_objective.owner = owner;
            await steal_objective.find_target();
            Dm.append(ref objectives, steal_objective);
        }

        DmList active_ais = GlobalProcs.active_ais() as DmList;
        if (Dm.truthy(active_ais.len) && prob(Dm.div(100, GLOB.joined_player_list.len)))
        {
            datum_objective_destroy destroy_objective = Dm.@new<datum_objective_destroy>();
            destroy_objective.owner = owner;
            await destroy_objective.find_target();
            Dm.append(ref objectives, destroy_objective);
        }
        else
        {
            if (prob(70))
            {
                datum_objective_assassinate kill_objective = Dm.@new<datum_objective_assassinate>();
                kill_objective.owner = owner;
                await kill_objective.find_target();
                Dm.append(ref objectives, kill_objective);
            }
            else
            {
                datum_objective_maroon maroon_objective = Dm.@new<datum_objective_maroon>();
                maroon_objective.owner = owner;

                if (!Dm.truthy(Dm.locate<datum_objective_escape>(objectives)) && Dm.truthy(escape_objective_possible))
                {
                    datum_objective_escape_escape_with_identity identity_theft = Dm.@new<datum_objective_escape_escape_with_identity>();
                    identity_theft.owner = owner;
                    await identity_theft.find_target();
                    identity_theft.update_explanation_text();
                    escape_objective_possible = FALSE;
                    maroon_objective.target = (identity_theft.target is var or__1 && Dm.truthy(or__1) ? (object)or__1 : await maroon_objective.find_target()) as datum_mind;
                    maroon_objective.update_explanation_text();
                    Dm.append(ref objectives, maroon_objective);
                    Dm.append(ref objectives, identity_theft);
                }
                else
                {
                    await maroon_objective.find_target();
                    Dm.append(ref objectives, maroon_objective);
                }
            }
        }

        if (!Dm.truthy(Dm.locate<datum_objective_escape>(objectives)) && Dm.truthy(escape_objective_possible))
        {
            if (prob(50))
            {
                datum_objective_escape escape_objective = Dm.@new<datum_objective_escape>();
                escape_objective.owner = owner;
                Dm.append(ref objectives, escape_objective);
            }
            else
            {
                datum_objective_escape_escape_with_identity identity_theft = Dm.@new<datum_objective_escape_escape_with_identity>();
                identity_theft.owner = owner;
                await identity_theft.find_target();
                Dm.append(ref objectives, identity_theft);
            }
            escape_objective_possible = FALSE;
        }
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:734
    public override object get_admin_commands()
    {
        object dot = null;
        dot = base.get_admin_commands();
        if (Dm.truthy(stored_profiles.len) && !Dm.eq(owner.current.real_name, first_profile.name))
            ((DmList)dot)["Transform to initial appearance."] = Dm.@new<datum_callback>(this, nameof(admin_restore_appearance));
        return dot;
    }

    /*
     * Restores the appearance of the changeling to the original DNA.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:742
    public virtual async DmTask admin_restore_appearance(mob admin = null)
    {
        if (!Dm.truthy(stored_profiles.len) || !iscarbon(owner.current))
        {
            to_chat(admin, span_danger(I18n.T("Resetting DNA failed!")));
            return;
        }

        mob_living_carbon carbon_owner = owner.current as mob_living_carbon;
        await first_profile.dna.copy_dna(carbon_owner.dna, Dm.bitor(COPY_DNA_SE, COPY_DNA_SPECIES));
        carbon_owner.real_name = first_profile.name;
        await carbon_owner.updateappearance(mutcolor_update: TRUE);
        await carbon_owner.domutcheck();
    }

    /*
     * Transform the currentc hangeing [user] into the [chosen_profile].
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:756
    public virtual async DmTask transform(mob_living_carbon_human user = null, datum_changeling_profile chosen_profile = null)
    {
        datum_dna chosen_dna = chosen_profile.dna;
        user.real_name = chosen_profile.name;
        user.underwear = (string)chosen_profile.underwear;
        user.underwear_color = (string)chosen_profile.underwear_color;
        user.undershirt = (string)chosen_profile.undershirt;
        user.socks = (string)chosen_profile.socks;
        user.age = Dm.num_or_null(chosen_profile.age);
        user.physique = (string)chosen_profile.physique;
        user.mind?.set_level(typeof(datum_skill_athletics), chosen_profile.athletics_level, silent: TRUE);
        user.voice = chosen_profile.voice;
        user.voice_filter = chosen_profile.voice_filter;

        await chosen_dna.copy_dna(user.dna, Dm.bitor(COPY_DNA_SE, COPY_DNA_SPECIES));

        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(user.get_bodyparts()))
            await limb.update_limb(is_creating: TRUE);

        await user.updateappearance(mutcolor_update: TRUE);
        await user.domutcheck();

        // Get rid of any scars from previous Changeling-ing
        foreach (datum_scar old_scar in Dm.each_cast<datum_scar>(user.all_scars))
        {
            if (Dm.truthy(old_scar.fake))
            {
                Dm.remove(ref user.all_scars, old_scar);
                qdel(old_scar);
            }
        }

        // Now, we do skillchip stuff, AFTER DNA code.
        // (There's a mutation that increases max chip complexity available, even though we force-implant skillchips.)

        // Remove existing skillchips.
        await user.destroy_all_skillchips(silent: FALSE);

        // Add new set of skillchips.
        foreach (object chip in Dm.each(chosen_profile.skillchips))
        {
            object chip_type = ((DmList)chip)["type"];
            // typed obj/item/skillchip in tg; object here, since a non-skillchip is qdel'd below (rules 3.4)
            object skillchip = await Dm.new_async(chip_type as Type, user);

            if (skillchip is not obj_item_skillchip)
            {
                stack_trace(Dm.text("Failure to implant changeling from [] with skillchip []. Tried to implant with non-skillchip type []", chosen_profile, skillchip, chip_type), "code/modules/antagonists/changeling/changeling.dm", 811);
                qdel(skillchip);
                continue;
            }

            // Try force-implanting and activating. If it doesn't work, there's nothing much we can do. There may be some
            // incompatibility out of our hands
            object implant_msg = await user.implant_skillchip((obj_item_skillchip)skillchip, TRUE);
            if (Dm.truthy(implant_msg))
            {
                // Hopefully recording the error message will help debug it.
                stack_trace(Dm.text("Failure to implant changeling from [] with skillchip []. Error msg: []", chosen_profile, skillchip, implant_msg), "code/modules/antagonists/changeling/changeling.dm", 820);
                qdel(skillchip);
                continue;
            }

            // Time to set the metadata. This includes trying to activate the chip.
            object set_meta_msg = await ((obj_item_skillchip)skillchip).set_metadata(chip as DmList);

            if (Dm.truthy(set_meta_msg))
            {
                // Hopefully recording the error message will help debug it.
                stack_trace(Dm.text("Failure to activate changeling skillchip from [] with skillchip [] using [] metadata. Error msg: []", chosen_profile, skillchip, chip, set_meta_msg), "code/modules/antagonists/changeling/changeling.dm", 829);
                continue;
            }
        }

        //vars hackery. not pretty, but better than the alternative.
        foreach (object slot in Dm.each(slot2type))
        {
            string slot_name = (string)slot;
            Type flesh_type = slot2type[slot] as Type;
            if (istype(Dm.get_var(user, slot_name), flesh_type) && !Dm.truthy(chosen_profile.exists_list[slot])) // Remove unnecessary flesh items
            {
                qdel(Dm.get_var(user, slot_name));
                continue;
            }

            if ((Dm.truthy(Dm.get_var(user, slot_name)) && !istype(Dm.get_var(user, slot_name), flesh_type)) || !Dm.truthy(chosen_profile.exists_list[slot]))
                continue;

            if (istype(Dm.get_var(user, slot_name), flesh_type) && slot_name == "wear_id") // Always remove old flesh IDs - so they get properly updated.
                qdel(Dm.get_var(user, slot_name));

            obj_item new_flesh_item = null;
            float equip = FALSE;
            if (!Dm.truthy(Dm.get_var(user, slot_name)))
            {
                object slot_type = slot2type[slot];
                equip = TRUE;
                new_flesh_item = await Dm.new_async(slot_type as Type, user) as obj_item;
            }

            else if (istype(Dm.get_var(user, slot_name), flesh_type))
                new_flesh_item = Dm.get_var(user, slot_name) as obj_item;

            new_flesh_item.appearance = chosen_profile.appearance_list[slot];
            new_flesh_item.name = Dm.str(chosen_profile.name_list[slot]);
            new_flesh_item.flags_cover = Dm.num(chosen_profile.flags_cover_list[slot]);
            new_flesh_item.lefthand_file = chosen_profile.lefthand_file_list[slot];
            new_flesh_item.righthand_file = chosen_profile.righthand_file_list[slot];
            new_flesh_item.inhand_icon_state = chosen_profile.inhand_icon_state_list[slot];
            new_flesh_item.worn_icon = chosen_profile.worn_icon_list[slot];
            new_flesh_item.worn_icon_state = chosen_profile.worn_icon_state_list[slot];

            if (new_flesh_item is obj_item_changeling_id && Dm.truthy(chosen_profile.id_icon))
            {
                obj_item_changeling_id flesh_id = new_flesh_item as obj_item_changeling_id;
                flesh_id.hud_icon = chosen_profile.id_icon;
                flesh_id.hud_icon_state = chosen_profile.id_icon_state;
            }

            if (Dm.truthy(equip))
            {
                await user.equip_to_slot_or_del(new_flesh_item, transform__slot2slot[slot], indirect_action: TRUE);
                if (!QDELETED(new_flesh_item))
                    ADD_TRAIT(new_flesh_item, TRAIT_NODROP, CHANGELING_TRAIT);
            }
        }

        foreach (object stored_scar_line in Dm.each(chosen_profile.stored_scars))
        {
            datum_scar attempted_fake_scar = user.load_scar(stored_scar_line) as datum_scar;
            if (attempted_fake_scar != null)
                attempted_fake_scar.fake = TRUE;
        }

        await user.regenerate_icons();
        user.name = Dm.str(user.get_visible_name());
        current_profile = chosen_profile;
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:978
    public override object roundend_report()
    {
        DmList parts = Dm.list();

        float changeling_win = TRUE;
        if (owner.current == null)
            changeling_win = FALSE;

        Dm.append(ref parts, printplayer(owner));
        Dm.append(ref parts, I18n.T("<b>Genomes Extracted:</b> []<br>", absorbed_count));

        if (Dm.truthy(objectives.len))
        {
            float count = 1;
            foreach (datum_objective objective in Dm.each<datum_objective>(objectives))
            {
                if (!Dm.truthy(objective.check_completion()))
                    changeling_win = FALSE;
                Dm.append(ref parts, I18n.T("<b>Objective #[]</b>: [] []", count, objective.explanation_text, objective.get_roundend_success_suffix()));
                count++;
            }
        }

        if (Dm.truthy(changeling_win))
            Dm.append(ref parts, span_greentext(I18n.T("The changeling was successful!")));
        else
            Dm.append(ref parts, span_redtext(I18n.T("The changeling has failed.")));

        return parts.Join("<br>");
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:1003
    public override async DmTask<object> get_preview_icon()
    {
        icon final_icon = await render_preview_outfit(typeof(datum_outfit_changeling)) as icon;
        icon split_icon = await render_preview_outfit(typeof(datum_outfit_job_engineer)) as icon;

        final_icon.Shift(WEST, ICON_SIZE_X / 2);
        final_icon.Shift(EAST, ICON_SIZE_X / 2);

        split_icon.Shift(EAST, ICON_SIZE_X / 2);
        split_icon.Shift(WEST, ICON_SIZE_X / 2);

        final_icon.Blend(split_icon, ICON_OVERLAY);

        return finish_preview_icon(final_icon);
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:1017
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList memories = Dm.list();

        foreach (object memory_key in Dm.each(stolen_memories))
            Dm.append(ref memories, Dm.list(Dm.list(Dm.kv("name", memory_key), Dm.kv("story", stolen_memories[memory_key]))));

        data["memories"] = memories;
        data["true_name"] = changelingID;
        data["hive_name"] = hive_name;
        data["stolen_antag_info"] = antag_memory;
        data["objectives"] = get_objectives();
        data["absorbed_dna"] = absorbed_count;
        await DmTask.CompletedTask;
        return data;
    }
}

// Changeling profile themselves. Store a data to store what every DNA instance looked like.
// tg: code/modules/antagonists/changeling/changeling.dm:883
public partial class datum_changeling_profile : datum
{
    // tg: code/modules/antagonists/changeling/changeling.dm:939
    public override object Destroy(float? force = null)
    {
        qdel(dna);
        LAZYCLEARLIST(stored_scars);
        QDEL_LAZYLIST(ref quirks);
        return base.Destroy(force);
    }

    /*
     * Copy every aspect of this file into a new instance of a profile.
     * Must be suppied with an instance.
     */
    // tg: code/modules/antagonists/changeling/changeling.dm:949
    public virtual async DmTask copy_profile(datum_changeling_profile new_profile = null)
    {
        new_profile.name = name;
        new_profile.@protected = @protected;
        new_profile.dna = await Dm.new_async(dna.type) as datum_dna;
        await dna.copy_dna(new_profile.dna);
        new_profile.name_list = name_list.Copy();
        new_profile.appearance_list = appearance_list.Copy();
        new_profile.flags_cover_list = flags_cover_list.Copy();
        new_profile.exists_list = exists_list.Copy();
        new_profile.lefthand_file_list = lefthand_file_list.Copy();
        new_profile.righthand_file_list = righthand_file_list.Copy();
        new_profile.inhand_icon_state_list = inhand_icon_state_list.Copy();
        new_profile.underwear = underwear;
        new_profile.underwear_color = underwear_color;
        new_profile.undershirt = undershirt;
        new_profile.socks = socks;
        new_profile.worn_icon_list = worn_icon_list.Copy();
        new_profile.worn_icon_state_list = worn_icon_state_list.Copy();
        new_profile.skillchips = skillchips.Copy();
        new_profile.stored_scars = stored_scars.Copy();
        new_profile.profile_snapshot = profile_snapshot;
        new_profile.id_icon = id_icon;
        new_profile.age = age;
        new_profile.physique = physique;
        new_profile.athletics_level = athletics_level;
        new_profile.quirks = quirks.Copy();
        new_profile.voice = voice;
        new_profile.voice_filter = voice_filter;
    }
}

// Changelings spawned from non-changeling headslugs (IE, due to being transformed into a headslug as a non-ling). Weaker than a normal changeling.
// tg: code/modules/antagonists/changeling/changeling.dm:1033
public partial class datum_antagonist_changeling_headslug : datum_antagonist_changeling
{
    // tg: code/modules/antagonists/changeling/changeling.dm:1044
    public override async DmTask greet()
    {
        play_stinger();
        to_chat(owner, span_bolddanger(I18n.T("You are a fresh changeling birthed from a headslug! You aren't as strong as a normal changeling, as you are newly born.")));
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/antagonists/changeling/changeling.dm:1050
public partial class datum_antagonist_changeling_space : datum_antagonist_changeling
{
    // tg: code/modules/antagonists/changeling/changeling.dm:1053
    public override async DmTask<object> get_preview_icon()
    {
        object final_icon = await render_preview_outfit(typeof(datum_outfit_changeling_space));
        return finish_preview_icon(final_icon);
    }

    // tg: code/modules/antagonists/changeling/changeling.dm:1057
    public override async DmTask greet()
    {
        play_stinger();
        // DEVIATION(DV450): tg sends the greeting to the antagonist datum itself (to_chat(src, ...)), which reaches no client; the
        // port sends it to the changeling's mind, as every other antagonist greeting does.
        to_chat(owner, span_changeling(I18n.T("Our mind stirs to life, from the depths of an endless slumber...")));
        await DmTask.CompletedTask;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/antagonists/changeling/changeling.dm:78  var/static/list/all_powers
    internal DmList datum_antagonist_changeling__all_powers;
    // tg: code/modules/antagonists/changeling/changeling.dm:81  var/static/list/possible_changeling_IDs
    internal DmList datum_antagonist_changeling__possible_changeling_IDs;
    // tg: code/modules/antagonists/changeling/changeling.dm:84  var/static/list/slot2type
    internal DmList datum_antagonist_changeling__slot2type;
    // tg: code/modules/antagonists/changeling/changeling.dm:757  transform's var/static/list/slot2slot
    internal DmList datum_antagonist_changeling__transform__slot2slot;
}
