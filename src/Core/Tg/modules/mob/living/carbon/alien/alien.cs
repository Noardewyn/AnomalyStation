// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/alien.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the carbon alien base. Its var block is hand-written (a class of M2, rules 10.2: the type block's
// overrides below; its own vars leaping, alien_speed and default_organ_types_by_slot are generated). The infection images go to
// the alien's client.images as in tg (the 3D client draws them over the host, required-models.md).

// The [DmPath] of /mob/living/carbon/alien is on _onclick/other_mobs.cs, the earliest .dme file that declares it (rules 2.3).
// tg: code/modules/mob/living/carbon/alien/alien.dm:1
public partial class mob_living_carbon_alien : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/alien/alien.dm:25  var/static/regex/alien_name_regex
    private static ref regex alien_name_regex => ref TgState.Current.mob_living_carbon_alien__alien_name_regex;
    // tg: code/modules/mob/living/carbon/alien/alien.dm:26  var/static/list/xeno_allowed_items
    public static ref DmList xeno_allowed_items => ref TgState.Current.mob_living_carbon_alien__xeno_allowed_items;

    // tg: code/modules/mob/living/carbon/alien/alien.dm:1 (the type block's overrides, rules 10.2)
    public mob_living_carbon_alien()
    {
        abstract_type = typeof(mob_living_carbon_alien);
        name = "alien";
        icon = Dm.resource("icons/mob/nonhuman-player/alien.dmi");
        gender = FEMALE; //All xenos are girls!!
        dna = null;
        sight = SEE_MOBS;
        verb_say = "hisses";
        initial_language_holder = typeof(datum_language_holder_alien);
        bubble_icon = "alien";
        type_of_meat = typeof(obj_item_food_meat_slab_xeno);
        blocks_emissive = EMISSIVE_BLOCK_UNIQUE;
        status_flags = Dm.bitor(CANUNCONSCIOUS, CANPUSH);
        heat_protection = 0.5f; // minor heat insulation
        unique_name = TRUE;
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:7, :36 (the list defaults, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        faction = Dm.list(ROLE_ALIEN);
        default_organ_types_by_slot = Dm.list(
            Dm.kv(ORGAN_SLOT_BRAIN, typeof(obj_item_organ_brain_alien)),
            Dm.kv(ORGAN_SLOT_XENO_HIVENODE, typeof(obj_item_organ_alien_hivenode)),
            Dm.kv(ORGAN_SLOT_TONGUE, typeof(obj_item_organ_tongue_alien)),
            Dm.kv(ORGAN_SLOT_EYES, typeof(obj_item_organ_eyes_alien)),
            Dm.kv(ORGAN_SLOT_LIVER, typeof(obj_item_organ_liver_alien)),
            Dm.kv(ORGAN_SLOT_EARS, typeof(obj_item_organ_ears)));
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        SSverbs.assign_verb(this, typeof(datum_verb_metadata_mob_living_mob_sleep)); // ASSIGN_GAME_VERB(src, /mob/living, mob_sleep)
        add_verb(this, "/mob/living/proc/toggle_resting");

        Waiting.NoSleep(create_bodyparts()); //initialize bodyparts

        Waiting.NoSleep(create_internal_organs());

        add_traits(Dm.list(TRAIT_NEVER_WOUNDED, TRAIT_VENTCRAWLER_ALWAYS), INNATE_TRAIT);

        dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(alien_speed))
            Waiting.NoSleep(update_alien_speed());
        Waiting.NoSleep(LoadComponent(
            typeof(datum_component_itempicky),
            xeno_allowed_items,
            span_alien(I18n.T("Your claws lack the dexterity to hold %TARGET.")),
            Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs._has_trait), this, TRAIT_ADVANCEDTOOLUSER)));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:64
    public virtual async DmTask create_internal_organs()
    {
        foreach (object slot in Dm.each(default_organ_types_by_slot))
        {
            Type organ_type = default_organ_types_by_slot[slot] as Type;
            obj_item_organ organ = Dm.@new(organ_type) as obj_item_organ;
            await organ.Insert(this, special: TRUE);
        }
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:70
    public override object assess_threat(object judgement_criteria = null, string lasercolor = null, datum_callback weaponcheck = null) // beepsky won't hunt aliums
    {
        lasercolor ??= "";
        return -10;
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:73
    public override async DmTask handle_environment(datum_gas_mixture environment = null, object seconds_per_tick = null, params object[] __extra)
    {
        // Run base mob body temperature proc before taking damage
        // this balances body temp to the environment and natural stabilization
        await base.handle_environment(environment, seconds_per_tick, __extra);
        if (bodytemperature <= BODYTEMP_HEAT_DAMAGE_LIMIT)
        {
            clear_alert(ALERT_XENO_FIRE);
            return;
        }

        //Body temperature is too hot.
        throw_alert(ALERT_XENO_FIRE, typeof(atom_movable_screen_alert_alien_fire));
        float sw__1 = bodytemperature;
        if (sw__1 >= 360 && sw__1 <= 400)
            await apply_damage(HEAT_DAMAGE_LEVEL_1 * Dm.num(seconds_per_tick), BURN);
        else if (sw__1 >= 400 && sw__1 <= 460)
            await apply_damage(HEAT_DAMAGE_LEVEL_2 * Dm.num(seconds_per_tick), BURN);
        else if (sw__1 >= 460 && sw__1 <= INFINITY)
        {
            if (Dm.truthy(on_fire))
                await apply_damage(HEAT_DAMAGE_LEVEL_3 * Dm.num(seconds_per_tick), BURN);
            else
                await apply_damage(HEAT_DAMAGE_LEVEL_2 * Dm.num(seconds_per_tick), BURN);
        }
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:94
    public override object get_bloodtype()
    {
        return get_blood_type(typeof(datum_blood_type_xeno));
    }

    /*----------------------------------------
    Proc: AddInfectionImages()
    Des: Gives the client of the alien an image on each infected mob.
    ----------------------------------------*/
    // tg: code/modules/mob/living/carbon/alien/alien.dm:101
    public virtual void AddInfectionImages()
    {
        if (client == null)
            return;

        foreach (mob_living target in Dm.each_cast<mob_living>(GLOB.mob_living_list))
        {
            if (HAS_TRAIT(target, TRAIT_XENO_HOST))
            {
                obj_item_organ_body_egg_alien_embryo embryo = target.get_organ_by_type(typeof(obj_item_organ_body_egg_alien_embryo)) as obj_item_organ_body_egg_alien_embryo;
                if (embryo != null)
                    Dm.append(client.images, Dm.image(Dm.resource("icons/mob/nonhuman-player/alien.dmi"), loc: target, icon_state: Dm.text("infected[]", embryo.stage)));
            }
        }
    }

    /*----------------------------------------
    Proc: RemoveInfectionImages()
    Des: Removes all infected images from the alien.
    ----------------------------------------*/
    // tg: code/modules/mob/living/carbon/alien/alien.dm:115
    public virtual void RemoveInfectionImages()
    {
        if (client != null)
        {
            DmList to_remove = null;
            foreach (image client_image in Dm.each_cast<image>(client.images))
            {
                string searchfor = "infected";
                if (Dm.truthy(findtext(client_image.icon_state, searchfor, 1, Dm.length(searchfor) + 1)))
                    Dm.append(ref to_remove, client_image);
            }
            Dm.remove(client.images, to_remove);
        }
        return;
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:125
    public override object canBeHandcuffed()
    {
        if (num_hands < 2)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:130
    public override object get_visible_suicide_message()
    {
        return I18n.T("[] is thrashing wildly! It looks like [] trying to commit suicide.", this, p_theyre());
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:133
    public override object get_blind_suicide_message()
    {
        return I18n.T("You hear thrashing.");
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:136
    public virtual async DmTask alien_evolve(mob_living_carbon_alien new_xeno = null)
    {
        visible_message(
            span_alertalien(I18n.T("[] begins to twist and contort!", this)),
            span_noticealien(I18n.T("You begin to evolve!")));

        await new_xeno.setDir(dir);
        new_xeno.change_name(name, real_name, identifier);

        if (mind != null)
        {
            mind.name = new_xeno.real_name;
            await mind.transfer_to(new_xeno);
        }

        foreach (object slot in Dm.each(Dm.bitor(organs_slot, default_organ_types_by_slot)))
        {
            obj_item_organ old_organ = get_organ_slot(slot) as obj_item_organ;
            obj_item_organ new_organ = new_xeno.get_organ_slot(slot) as obj_item_organ;
            if (old_organ != null)
            {
                // Transfer any organs that differ from their intended original type
                // Also transfer brains regardless - if we somehow put skillchips or traumas into xeno brains, those should carry over
                if (old_organ is obj_item_organ_brain || (old_organ.type != default_organ_types_by_slot[slot] as Type))
                {
                    if (new_organ != null)
                    {
                        await new_organ.Remove(new_xeno, special: TRUE, movement_flags: NO_ID_TRANSFER);
                        qdel(new_organ);
                    }
                    await old_organ.Remove(this, special: TRUE, movement_flags: NO_ID_TRANSFER);
                    await old_organ.Insert(new_xeno, special: TRUE, movement_flags: NO_ID_TRANSFER);
                }
            }
            else
            {
                // If we don't have an organ in a slot that should have one in both the old and new xeno, remove the organ from that slot in the new xeno
                if (Dm.truthy(default_organ_types_by_slot[slot]) && Dm.truthy(new_xeno.default_organ_types_by_slot[slot]) && new_organ != null)
                {
                    await new_organ.Remove(new_xeno, special: TRUE);
                    qdel(new_organ);
                }
            }
        }

        obj_item_organ_stomach_alien melting_pot = get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach_alien;
        obj_item_organ_stomach_alien frying_pan = new_xeno.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach_alien;
        if (melting_pot is obj_item_organ_stomach_alien && frying_pan is obj_item_organ_stomach_alien)
        {
            foreach (atom_movable poor_sod in Dm.each_cast<atom_movable>(melting_pot.stomach_contents))
                await frying_pan.consume_thing(poor_sod);
        }
        qdel(this);
    }

    /// Changes the name of the xeno we are evolving into in order to keep the same numerical identifier the old xeno had.
    // tg: code/modules/mob/living/carbon/alien/alien.dm:175
    public virtual void change_name(object old_name = null, object old_real_name = null, object old_identifier = null)
    {
        if (!Dm.truthy(alien_name_regex.Find(old_name))) // check to make sure there's no admins doing funny stuff with naming these aliens
        {
            name = Dm.str(old_name);
            real_name = Dm.str(old_real_name);
            return;
        }

        if (!Dm.truthy(unique_name))
            return;

        if (!Dm.eq(old_identifier, 0))
        {
            identifier = Dm.num(old_identifier);
            name = Dm.initial(this, (atom p) => p.name); // prevent chicanery like two different numerical identifiers tied to the same mob
        }

        set_name();
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:190
    public override async DmTask on_lying_down(object new_lying_angle = null)
    {
        await base.on_lying_down(new_lying_angle);
        await update_icons();
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:194
    public override async DmTask on_standing_up(params object[] __extra)
    {
        await base.on_standing_up(__extra);
        await update_icons();
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:198
    public virtual async DmTask update_alien_speed()
    {
        await add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_alien_speed), multiplicative_slowdown: alien_speed);
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:201
    public override object get_footprint_sprite()
    {
        return FOOTPRINT_SPRITE_CLAWS;
    }

    // tg: code/modules/mob/living/carbon/alien/alien.dm:204
    public override object get_fire_overlay(object stacks = null, object on_fire = null)
    {
        return make_generic_fire_overlay();
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/carbon/alien/alien.dm:25
    internal regex mob_living_carbon_alien__alien_name_regex = Dm.regex("alien (larva|sentinel|drone|hunter|praetorian|princess|queen)( \\(\\d+\\))?");
    // tg: code/modules/mob/living/carbon/alien/alien.dm:26
    internal DmList mob_living_carbon_alien__xeno_allowed_items = (DmList)typecacheof(Dm.list(
        typeof(obj_item_clothing_mask_facehugger),
        typeof(obj_item_toy_basketball), // playing ball against a xeno is rigged since they cannot be disarmed, their game is out of this world
        typeof(obj_item_toy_toy_xeno),
        typeof(obj_item_sticker), //funny ~Jimmyl
        typeof(obj_item_toy_plush_rouny),
        typeof(obj_item_hand_item),
        typeof(obj_item_queen_promotion)));
}
