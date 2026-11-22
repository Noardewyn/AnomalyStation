// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/special/facehugger.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the facehugger: its leap at a face, the impregnation with the embryo, its states (active,
// inactive, thrown, dead, impregnated: icon and worn states), Lamarr and the toy. The var blocks are generated (D111).

//TODO: Make these basic mobs

// tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:15
public partial class obj_item_clothing_mask_facehugger : obj_item_clothing_mask
{
    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:5  #define MIN_IMPREGNATION_TIME 100 (#undef at :339)
    private const int MIN_IMPREGNATION_TIME = 100; //time it takes to impregnate someone
    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:6  #define MAX_IMPREGNATION_TIME 150 (#undef at :340)
    private const int MAX_IMPREGNATION_TIME = 150;
    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:8  #define MIN_ACTIVE_TIME 200 (#undef at :336)
    private const int MIN_ACTIVE_TIME = 200; //time between being dropped and going idle
    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:9  #define MAX_ACTIVE_TIME 400 (#undef at :337)
    private const int MAX_ACTIVE_TIME = 400;
    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:11  #define FACEHUGGER_AWAKE 1 (#undef at :332)
    private const int FACEHUGGER_AWAKE = 1;
    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:12  #define FACEHUGGER_ASLEEP 2 (#undef at :333)
    private const int FACEHUGGER_ASLEEP = 2;
    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:13  #define FACEHUGGER_DEAD 3 (#undef at :334)
    internal const int FACEHUGGER_DEAD = 3;

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:42  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_item_clothing_mask_facehugger__Initialize__loc_connections;

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        Waiting.NoSleep(AddElement(typeof(datum_element_muffles_speech)));

        RegisterSignal(this, COMSIG_LIVING_TRYING_TO_PULL, nameof(react_to_mob));
        RegisterSignal(this, COMSIG_ITEM_IN_UNWRAPPED_TRAITOR_MAIL, nameof(on_mail_unwrap));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:52
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        sound_effect ??= 1;
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.truthy(dot) && Dm.num(atom_integrity) < 90 && !QDELETED(this))
            await die();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:57
    [DmParams("attacked_item", "user", "modifiers", "attack_modifiers")]
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        obj_item attacked_item = attacking_item;
        return await attacked_item.attack_atom(this, user as mob_living, modifiers);
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:60
    [SignalHandler]
    public virtual object react_to_mob(datum source = null, mob user = null)
    {
        if ((facehugger_state == FACEHUGGER_AWAKE && !Dm.truthy(sterile)) && !isalien(user))
        {
            if (Dm.truthy(Waiting.NoSleep(leap_to(user as mob_living))))
                return COMSIG_LIVING_CANCEL_PULL;
        }
        return null;
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:67
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if ((facehugger_state == FACEHUGGER_AWAKE && !Dm.truthy(sterile)) && !isalien(user))
        {
            if (Dm.truthy(await leap_to(user as mob_living)))
                return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:73
    [DmParams("M", "user")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living M = target_mob;
        await base.attack__proc(M, user, modifiers, attack_modifiers, __extra);
        if (Dm.truthy(await user.transferItemToLoc(this, get_turf(M))))
            await leap_to(M);
        return null;
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:78
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(real))//So that giant red text about probisci doesn't show up.
            return dot;
        switch (facehugger_state)
        {
            case FACEHUGGER_DEAD or FACEHUGGER_ASLEEP:
                Dm.append(ref dot, span_bolddanger(I18n.T("[] is not moving.", this)));
                break;
            case FACEHUGGER_AWAKE:
                Dm.append(ref dot, span_bolddanger(I18n.T("[] seems to be active!", this)));
                break;
        }
        if (Dm.truthy(sterile))
            Dm.append(ref dot, span_bolddanger(I18n.T("It looks like the proboscis has been removed.")));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:90
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(Dm.num(exposed_temperature) > 300);
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:93
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        await die();
        return null;
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:96
    [DmParams("M")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        mob M = user;
        await base.equipped__proc(M, slot, initial);
        await attach_to_victim(M as mob_living);
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:100
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom target = null)
    {
        Waiting.NoSleep(HasProximity(target as atom_movable));
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:104
    public override async DmTask<object> on_found(mob finder = null)
    {
        if (facehugger_state == FACEHUGGER_AWAKE)
            return await HasProximity(finder);
        return null;
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:108
    [DmParams("AM")]
    public override async DmTask<object> HasProximity(atom_movable proximity_check_mob = null)
    {
        atom_movable AM = proximity_check_mob;
        if (Dm.truthy(CanHug(AM as mob_living)) && Dm.truthy(await Adjacent(AM)))
            return await leap_to(AM as mob_living);
        return null;
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:112  (tg declares gentle, quickstart and throw_type_path where the
    // slot has force, gentle and quickstart: DM's ..() passes them by position, so the parent gets the facehugger's gentle as its
    // force, its quickstart (TRUE by default) as its gentle and its throw_type_path as its quickstart, as here)
    [DmParams("target", "range", "speed", "thrower", "spin", "diagonals_first", "callback", "gentle", "quickstart", "throw_type_path")]
    public override async DmTask<object> throw_at(atom target = null, object range = null, object speed = null, atom thrower = null, float? spin = null, float? diagonals_first = null, datum_callback callback = null, float? force = null, float? gentle = null, object quickstart = null, Type throw_datum_typepath = null, params object[] __extra)
    {
        spin ??= 1;
        diagonals_first ??= 0;
        gentle ??= TRUE; // the facehugger's quickstart = TRUE, in the slot's gentle place
        quickstart ??= typeof(datum_thrownthing); // the facehugger's throw_type_path = /datum/thrownthing, in the slot's quickstart place
        object dot = await base.throw_at(target, range, speed, thrower, spin, diagonals_first, callback, force, gentle, quickstart, throw_datum_typepath, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (facehugger_state == FACEHUGGER_AWAKE)
        {
            icon_state = Dm.text("[]_thrown", base_icon_state);
            _addtimer(Dm.@new<datum_callback>(this, nameof(clear_throw_icon_state)), 1.5f * SECONDS, file: "code/modules/mob/living/carbon/alien/special/facehugger.dm", line: 118);
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:120
    public virtual void clear_throw_icon_state()
    {
        if (icon_state == Dm.text("[]_thrown", base_icon_state))
            icon_state = Dm.text("[]", base_icon_state);
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:124  (set waitfor = FALSE, inherited from /atom/movable/throw_impact:
    // the wrapper of waiting-model.md 5.4; the leap may sleep)
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(throw_impact__body(hit_atom, throwingdatum, __extra), null);
    }

    private async DmTask<object> throw_impact__body(atom hit_atom, datum_thrownthing throwingdatum, object[] __extra)
    {
        base.throw_impact(hit_atom, throwingdatum, __extra);
        if (facehugger_state == FACEHUGGER_AWAKE)
        {
            icon_state = Dm.text("[]", base_icon_state);
            await leap_to(hit_atom as mob_living);
        }
        return null;
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:130
    public virtual object valid_to_attach(mob_living hit_mob = null)
    {
        // valid targets: carbons except aliens and devils
        // facehugger state early exit checks (Note: Melbert does not want dead people to be huggable)
        if (facehugger_state != FACEHUGGER_AWAKE)
            return FALSE;
        if (Dm.truthy(attached))
            return FALSE;
        if (!ishuman(hit_mob))
            return FALSE;
        mob_living_carbon_human target = hit_mob as mob_living_carbon_human;
        // gotta have a head to be implanted (no changelings or sentient plants)
        if (!Dm.truthy(target.get_bodypart(BODY_ZONE_HEAD)))
            return FALSE;
        // gotta be able to have the xeno implanted
        if (HAS_TRAIT(hit_mob, TRAIT_XENO_IMMUNE))
            return FALSE;
        // carbon, has head, not an alien nor has an hivenode or embryo: valid
        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:149
    public virtual async DmTask<object> leap_to(mob_living hit_mob = null)
    {
        //check if not carbon/alien/has facehugger already/ect.
        if (!Dm.truthy(valid_to_attach(hit_mob)))
            return FALSE;
        mob_living_carbon_human target = hit_mob as mob_living_carbon_human;
        if (target.wear_mask is obj_item_clothing_mask_facehugger)
            return FALSE;
        // passed initial checks - time to leap!
        target.visible_message(span_danger(I18n.T("[] leaps at []'s face!", this, target)),
                            span_userdanger(I18n.T("[] leaps at your face!", this)));

        // probiscis-blocker handling
        if (Dm.truthy(target.is_mouth_covered(ITEM_SLOT_HEAD)))
        {
            target.visible_message(span_danger(I18n.T("[] smashes against []'s []!", this, target, target.head)),
                                span_userdanger(I18n.T("[] smashes against your []!", this, target.head)));
            await die();
            return FALSE;
        }

        if (target.wear_mask != null)
        {
            obj_item worn_mask = target.wear_mask;
            if (Dm.truthy(await target.dropItemToGround(worn_mask)))
            {
                target.visible_message(span_danger(I18n.T("[] tears [] off of []'s face!", this, worn_mask, target)),
                                    span_userdanger(I18n.T("[] tears [] off of your face!", this, worn_mask)));
            }
        }

        if (!Dm.truthy(await target.equip_to_slot_if_possible(this, ITEM_SLOT_MASK, 0, 1, 1)))
        {
            log_combat(target, this, "failed facehugged by");
            return FALSE;
        }
        log_combat(target, this, "was facehugged by");
        return TRUE; // time for a smoke
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:179
    public virtual async DmTask attach_to_victim(mob_living victim = null)
    {
        if (!Dm.truthy(valid_to_attach(victim)))
            return;

        if (!IS_UNCONSCIOUS(victim)) //sorry bro you gotta be awake
            await victim.say("AAAA!!"); //triggers muffled speech and also visual feedback i guess
        // early returns and validity checks done: attach.
        attached = Dm.num(attached) + 1;
        //ensure we detach once we no longer need to be attached
        _addtimer(Dm.@new<datum_callback>(this, nameof(detach)), MAX_IMPREGNATION_TIME, file: "code/modules/mob/living/carbon/alien/special/facehugger.dm", line: 188);


        if (!Dm.truthy(sterile))
            await victim.take_bodypart_damage(strength, 0); //done here so that humans in helmets take damage
        if (Dm.truthy(real) && !Dm.truthy(sterile))
        {
            await victim.Paralyze(1 * SECONDS);
            await victim.adjust_confusion(20 * SECONDS);
            await victim.Knockdown(10 * SECONDS);
        }
        go_idle(); //so it doesn't jump the people that tear it off

        _addtimer(Dm.@new<datum_callback>(this, nameof(impregnate_target), victim), rand(MIN_IMPREGNATION_TIME, MAX_IMPREGNATION_TIME), file: "code/modules/mob/living/carbon/alien/special/facehugger.dm", line: 199);
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:201
    public virtual void detach()
    {
        attached = 0;
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:204
    public virtual async DmTask impregnate_target(mob_living target = null)
    {
        if (target == null || target.stat == DEAD || target.get_item_by_slot(ITEM_SLOT_MASK) != this) //was taken off or something
            return;

        if (!Dm.truthy(sterile))
        {
            target.visible_message(span_danger(I18n.T("[] falls limp after violating []'s face!", this, target)),
                                    span_userdanger(I18n.T("[] falls limp after violating your face!", this)));

            await die();
            icon_state = Dm.text("[]_impregnated", base_icon_state);
            worn_icon_state = Dm.text("[]_impregnated", base_icon_state);

            obj_item_bodypart_chest LC = target.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
            if ((LC == null || Dm.truthy(IS_ORGANIC_LIMB(LC))) && target.get_organ_by_type(typeof(obj_item_organ_body_egg_alien_embryo)) == null)
            {
                Dm.@new<obj_item_organ_body_egg_alien_embryo>(target);
                target.log_message("was impregnated by a facehugger", LOG_GAME);
                target.log_message("was impregnated by a facehugger", LOG_VICTIM, log_globally: FALSE);
                if (target.stat != DEAD && target.buckled is obj_structure_bed_nest) //Handles toggling the nest sustenance status effect if the user was already buckled to a nest.
                    await target.apply_status_effect(typeof(datum_status_effect_nest_sustenance));
            }
        }

        else
        {
            target.visible_message(span_danger(I18n.T("[] violates []'s face!", this, target)),
                                    span_userdanger(I18n.T("[] violates your face!", this)));
        }
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:228
    public virtual void go_active()
    {
        if (facehugger_state == FACEHUGGER_DEAD || facehugger_state == FACEHUGGER_AWAKE)
            return;

        facehugger_state = FACEHUGGER_AWAKE;
        icon_state = Dm.text("[]", base_icon_state);
        worn_icon_state = Dm.text("[]", base_icon_state);
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:236
    public virtual void go_idle()
    {
        if (facehugger_state == FACEHUGGER_DEAD || facehugger_state == FACEHUGGER_ASLEEP)
            return;

        facehugger_state = FACEHUGGER_ASLEEP;
        icon_state = Dm.text("[]_inactive", base_icon_state);
        worn_icon_state = Dm.text("[]_inactive", base_icon_state);

        _addtimer(Dm.@new<datum_callback>(this, nameof(go_active)), rand(MIN_ACTIVE_TIME, MAX_ACTIVE_TIME), file: "code/modules/mob/living/carbon/alien/special/facehugger.dm", line: 244);
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:246
    public virtual async DmTask die()
    {
        if (facehugger_state == FACEHUGGER_DEAD)
            return;

        icon_state = Dm.text("[]_dead", base_icon_state);
        worn_icon_state = Dm.text("[]_dead", base_icon_state);
        inhand_icon_state = "facehugger_inactive";
        facehugger_state = FACEHUGGER_DEAD;

        visible_message(span_danger(I18n.T("[] curls up into a ball!", this)));

        // chest maybe because getting slammed in the chest would knock it off your face while dead
        await AddComponent(typeof(datum_component_knockoff), Dm.named("knockoff_chance", 40), Dm.named("target_zones", Dm.list(BODY_ZONE_HEAD, BODY_ZONE_CHEST)), Dm.named("slots_knockoffable", slot_flags));
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:260
    public override object can_mob_unequip(mob user = null)
    {
        if (!Dm.truthy(real) || Dm.truthy(sterile) || facehugger_state == FACEHUGGER_DEAD || user.get_organ_by_type(typeof(obj_item_organ_body_egg_alien_embryo)) != null)
            return base.can_mob_unequip(user);
        if (user.get_item_by_slot(slot_flags) == this)
        {
            to_chat(user, span_userdanger(I18n.T("[] is latched on too tight! Get help or wait for it to let go!", this)));
            return FALSE;
        }
        return base.can_mob_unequip(user);
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:268
    public override async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        mob_living_carbon_human wearer = loc as mob_living_carbon_human;
        if (!(wearer is mob_living_carbon_human) || user != wearer)
            return null;
        if (!Dm.truthy(real) || Dm.truthy(sterile) || user.get_organ_by_type(typeof(obj_item_organ_body_egg_alien_embryo)) != null)
            return await base.mouse_drop_dragged(over, user, src_location, over_location, @params);
        if (wearer.get_item_by_slot(slot_flags) == this && facehugger_state != FACEHUGGER_DEAD)
        {
            to_chat(user, span_userdanger(I18n.T("[] is latched on too tight! Get help or wait for it to let go!", this)));
            return null;
        }
        return await base.mouse_drop_dragged(over, user, src_location, over_location, @params);
    }

    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:294
    [SignalHandler]
    public virtual object on_mail_unwrap(atom source = null, mob user = null, obj_item_mail_traitor letter = null)
    {
        if (facehugger_state != FACEHUGGER_AWAKE)
            return NONE;
        to_chat(user, span_danger(I18n.T("There's something moving inside of \\the []!", letter)));
        Waiting.NoSleep(leap_to(user as mob_living));
        return COMPONENT_TRAITOR_MAIL_HANDLED;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:279
    public static object CanHug(mob_living M = null)
    {
        if (!(M is mob_living))
            return FALSE;
        if (M.stat == DEAD)
            return FALSE;
        if (M.get_organ_by_type(typeof(obj_item_organ_alien_hivenode)) != null)
            return FALSE;
        mob_living_carbon C = M as mob_living_carbon;
        if (ishuman(C) && !Dm.truthy(Dm.bitand(((datum_species)C.dna.species).no_equip_flags, ITEM_SLOT_MASK)))
        {
            mob_living_carbon_human H = C as mob_living_carbon_human;
            if (Dm.truthy(H.is_mouth_covered(ITEM_SLOT_HEAD)))
                return FALSE;
            return TRUE;
        }
        return FALSE;
    }
}

// tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:320
public partial class obj_item_clothing_mask_facehugger_toy : obj_item_clothing_mask_facehugger
{
    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:329
    public override async DmTask die()
    {
        return;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/carbon/alien/special/facehugger.dm:42
    internal DmList obj_item_clothing_mask_facehugger__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_item_clothing_mask_facehugger.on_entered)));
}
