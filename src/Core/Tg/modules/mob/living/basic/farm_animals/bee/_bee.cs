// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/bee/_bee.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file: bees and queens, pollinating trays, living in an apiary, their reagent, the queen bee item and the
// dead bee. GAP(lead): GLOB.chemical_reagents_list is not built (holder.cs): a bee's reagent is the reagent type's prototype (its
// initial values, read only), which stands in for the list's entry as add_reagent's does; bees only read its type, name and colour.
// The mutate slot is synchronous in the port (basic.cs), so its toxin is assigned without waiting.

// tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:8
public partial class mob_living_basic_bee : mob_living_basic
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:1
    private const float BEE_TRAY_RECENT_VISIT = 20 * SECONDS; //! How long in deciseconds until a tray can be visited by a bee again
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:2
    private const string BEE_DEFAULT_COLOUR = "#e5e500"; //! the colour we make the stripes of the bee if our reagent has no colour (or we have no reagent)
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:3
    private const float BEE_POLLINATE_YIELD_CHANCE = 33; //! chance to increase yield of plant
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:4
    private const float BEE_POLLINATE_PEST_CHANCE = 33; //! chance to decrease pest of plant
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:5
    private const float BEE_POLLINATE_POTENCY_CHANCE = 50; //! chance to increase potancy of plant
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:6
    internal static float BEE_FOODGROUPS => Dm.bitor(Dm.bitor(Dm.bitor(RAW, MEAT), GORE), BUGS); //! the bee food contents

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:63
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_SPACEWALK, TRAIT_VENTCRAWLER_ALWAYS), INNATE_TRAIT);
        generate_bee_visuals();
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        // AddComponent(/datum/component/clickbox, x_offset = -2, y_offset = -2): the clickbox (a bigger 2D click target) is not ported
        Dm.Deferred("code/modules/mob/living/basic/farm_animals/bee/_bee.dm:67", "Initialize: the clickbox component (a 2D click target, inert)");
        Waiting.NoSleep(AddComponent(typeof(datum_component_swarming)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), pet_commands));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_QUEEN_BEE, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_allergenic_attack), Dm.named("allergen", BUGS), Dm.named("allergen_chance", 33), Dm.named("histamine_add", 5)));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:75
    public override async DmTask mob_pickup(mob_living user = null)
    {
        mob_living picker = user;
        if (Dm.truthy(Dm.bitand(Dm.num(flags_1), HOLOGRAM_1)))
            return;
        obj_item_mob_holder_destructible holder = Dm.@new<obj_item_mob_holder_destructible>(get_turf(this), this, held_state, head_icon, held_lh, held_rh, worn_slot_flags);
        DmList reee = Dm.list(Dm.kv(typeof(datum_reagent_consumable_nutriment_vitamin), 5));
        if (beegent != null)
            reee[beegent.type] = 5;
        await holder.AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), reee, null, BEE_FOODGROUPS, 10, 0, Dm.list("bee"), null, 10);
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_SCOOPED_UP]))
            _SendSignal(COMSIG_LIVING_SCOOPED_UP, Dm.list(this, picker, holder));
        picker.visible_message(span_warning(I18n.T("[] scoops up []!", picker, this)));
        await picker.put_in_hands(holder);
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:87
    public override object will_escape_storage()
    {
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:90
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (beehome == null)
            Dm.append(ref dot, span_warning(I18n.T("This bee is homeless!")));
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:96
    public override object Destroy(float? force = null)
    {
        if (beehome != null)
        {
            beehome.bees.Remove(this);
            beehome = null;
        }
        beegent = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:103
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (!Dm.truthy(Dm.bitand(Dm.num(flags_1), HOLOGRAM_1)) && !Dm.truthy(gibbed))
            spawn_corpse();
        return await base.death(gibbed, __extra);
    }

    /// Leave something to remember us by
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:109
    public virtual void spawn_corpse()
    {
        Dm.@new<obj_item_trash_bee>(loc, this);
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:112
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot))
            return dot;

        if (target is obj_machinery_hydroponics hydro)
        {
            pollinate(hydro);
            return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
        }

        if (target is obj_structure_beebox hive)
        {
            await handle_habitation(hive);
            return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:127
    public virtual async DmTask handle_habitation(obj_structure_beebox hive = null)
    {
        if (hive == beehome) //if its our home, we enter or exit it
        {
            atom drop_location = Dm.@in(this, beehome.contents) ? get_turf(beehome) : beehome;
            await forceMove(drop_location);
            return;
        }
        if (hive.queen_bee != null && Dm.truthy(is_queen)) //if we are queen and house already have a queen, dont inhabit
            return;
        if (!Dm.truthy(hive.habitable(this)) || beehome != null) //if not habitable or we alrdy have a home
            return;
        beehome = hive;
        beehome.bees.Add(this);
        if (Dm.truthy(is_queen))
            beehome.queen_bee = this as mob_living_basic_bee_queen;
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:141
    public virtual object reagent_incompatible(mob_living_basic_bee ruler = null)
    {
        if (ruler == null)
            return FALSE;
        if (ruler.beegent?.type != beegent?.type)
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:148
    public virtual void generate_bee_visuals()
    {
        cut_overlays();

        object bee_color = BEE_DEFAULT_COLOUR;
        if (Dm.truthy(beegent?.color))
            bee_color = beegent.color;

        icon_state = Dm.text("[]_base", icon_base);
        add_overlay(Dm.text("[]_base", icon_base));

        ref mutable_appearance greyscale_overlay = ref TgState.Current.mob_living_basic_bee__generate_bee_visuals__greyscale_overlay;
        greyscale_overlay ??= mutable_appearance(Dm.resource("icons/mob/simple/bees.dmi")) as mutable_appearance;
        greyscale_overlay.icon_state = Dm.text("[]_grey", icon_base);
        greyscale_overlay.color = bee_color;
        add_overlay(greyscale_overlay);

        add_overlay(Dm.text("[]_wings", icon_base));
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:166
    public virtual object pollinate(obj_machinery_hydroponics hydro = null)
    {
        if (!Dm.truthy(hydro.can_bee_pollinate()))
            return FALSE;
        hydro.recent_bee_visit = TRUE;
        _addtimer(Dm.@new<datum_callback>(hydro, nameof(obj_machinery_hydroponics.recent_bee_visit__varset), FALSE), BEE_TRAY_RECENT_VISIT, file: "code/modules/mob/living/basic/farm_animals/bee/_bee.dm", line: 170);

        float growth = Dm.num(health); //Health also means how many bees are in the swarm, roughly.
        //better healthier plants!
        hydro.adjust_plant_health(growth * 0.5f);
        if (prob(BEE_POLLINATE_PEST_CHANCE))
            hydro.adjust_pestlevel(-10);
        if (prob(BEE_POLLINATE_YIELD_CHANCE))
        {
            hydro.myseed.adjust_yield(1);
            hydro.yieldmod = 2;
        }
        if (prob(BEE_POLLINATE_POTENCY_CHANCE))
            hydro.myseed.adjust_potency(1);

        if (beehome != null)
            beehome.bee_resources = Dm.min(beehome.bee_resources + growth, 100);
        return null;
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:186
    public virtual async DmTask assign_reagent(datum_reagent toxin = null)
    {
        if (toxin == null)
            return;
        ref DmList injection_range = ref TgState.Current.mob_living_basic_bee__assign_reagent__injection_range;
        if (injection_range == null)
            injection_range = string_numbers_list(Dm.list(1, 5)) as DmList;
        if (beegent != null) //clear the old since this one is going to have some new value
            RemoveElement(typeof(datum_element_venomous), beegent.type, injection_range, Dm.named("thrown_effect", TRUE));
        beegent = toxin;
        name = Dm.text("[] ([])", Dm.initial(this, (atom a) => a.name), toxin.name);
        real_name = name;
        await AddElement(typeof(datum_element_venomous), beegent.type, injection_range, Dm.named("thrown_effect", TRUE));
        generate_bee_visuals();
    }

    /// Picks a random toxin and assigns it to the bee
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:201
    public virtual async DmTask assign_random_toxin_reagent()
    {
        // GLOB.chemical_reagents_list[...]: the reagent type's prototype stands in for the entry (the header's GAP)
        Type toxin = get_random_reagent_id(whitelist: subtypesof(typeof(datum_reagent_toxin))) as Type;
        await assign_reagent(toxin == null ? null : Dm.initial(toxin, (datum_reagent p) => p));
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:204
    public override object mutate()
    {
        object dot = base.mutate();
        if (!Dm.truthy(dot))
        {
            Waiting.NoSleep(assign_random_toxin_reagent());
            dot = TRUE;
        }
        return dot;
    }
}

// tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:210
public partial class mob_living_basic_bee_queen : mob_living_basic_bee
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:217
    public override object will_escape_storage()
    {
        return FALSE;
    }
}

// tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:220
public partial class mob_living_basic_bee_toxin : mob_living_basic_bee
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:223
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(assign_random_toxin_reagent());
        return dot;
    }
}

/// A bee which despawns after a short amount of time (beespawns?)
// tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:228
public partial class mob_living_basic_bee_timed : mob_living_basic_bee
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:235
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        _addtimer(Dm.@new<datum_callback>(this, nameof(death)), lifespan, file: "code/modules/mob/living/basic/farm_animals/bee/_bee.dm", line: 237);
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:239
    public override void spawn_corpse()
    {
        Dm.@new<obj_effect_temp_visual_despawn_effect>(get_turf(this), /* copy_from = */ this);
    }
}

// tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:242
public partial class obj_item_queen_bee : obj_item
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:251
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_QUEEN_BEE, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5));
        RegisterSignal(this, COMSIG_ATOM_ATTACKBY, nameof(handle_needle));
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:256
    public override object Destroy(float? force = null)
    {
        qdel(queen);
        queen = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:260
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone != queen)
            return dot;
        queen = null;
        // the bee should not exist without a bee.
        if (!QDELETED(this))
            qdel(this);
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:269
    [SignalHandler]
    public virtual void handle_needle(obj_item source = null, obj_item syringe = null, mob_living user = null, object @params = null)
    {
        if (syringe is not obj_item_reagent_containers_syringe needle)
            return;
        datum_reagents needle_reagents = needle.reagents as datum_reagents;
        if (Dm.truthy(needle_reagents.has_reagent(typeof(datum_reagent_royal_bee_jelly), 5))) //checked twice, because I really don't want royal bee jelly to be duped
        {
            Waiting.NoSleep(needle_reagents.remove_reagent(typeof(datum_reagent_royal_bee_jelly), 5));
            obj_item_queen_bee new_bee = Dm.@new<obj_item_queen_bee>(get_turf(this));
            new_bee.queen = Dm.@new<mob_living_basic_bee_queen>(new_bee);
            if (queen?.beegent != null)
                Waiting.NoSleep(new_bee.queen.assign_reagent(queen.beegent)); //Bees use the global singleton instances of reagents, so we don't need to worry about one bee being deleted and her copies losing their reagents.
            Waiting.NoSleep(user.put_in_active_hand(new_bee));
            user.visible_message(span_notice(I18n.T("[] injects [] with royal bee jelly, causing it to split into two bees, MORE BEES!", user, this)), span_warning(I18n.T("You inject [] with royal bee jelly, causing it to split into two bees, MORE BEES!", this)));
            return;
        }
        datum_reagent chemical = needle_reagents.get_master_reagent() as datum_reagent;
        if (chemical == null)
            return;
        if (!Dm.truthy(Dm.bitand(chemical.chemical_flags, REAGENT_CAN_BE_SYNTHESIZED)))
        {
            to_chat(user, span_warning(I18n.T("[] cannot be inserted into a bee's genome!", chemical.name)));
            return;
        }
        if (chemical.type == queen.beegent?.type)
        {
            to_chat(user, span_warning(I18n.T("[] already has this chemical!", queen)));
            return;
        }
        if (!Dm.truthy(needle_reagents.has_reagent(chemical.type, 5)))
        {
            to_chat(user, span_warning(I18n.T("You don't have enough units of that chemical to modify the bee's DNA!")));
            return;
        }
        Waiting.NoSleep(needle_reagents.remove_reagent(chemical.type, 5));
        // GLOB.chemical_reagents_list[chemical.type]: the reagent type's prototype stands in for the entry (the header's GAP)
        datum_reagent bee_chem = Dm.initial(chemical.type, (datum_reagent p) => p);
        Waiting.NoSleep(queen.assign_reagent(bee_chem));
        user.visible_message(span_warning(I18n.T("[] injects []'s genome with [], mutating its DNA!", user, this, chemical.name)), span_warning(I18n.T("You inject []'s genome with [], mutating its DNA!", this, chemical.name)));
        name = queen.name;
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:302
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] eats []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        await user.say("IT'S HIP TO EAT BEES!");
        qdel(this);
        return TOXLOSS;
    }
}

// tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:308
public partial class obj_item_queen_bee_bought : obj_item_queen_bee
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:310
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        queen = Dm.@new<mob_living_basic_bee_queen>(this);
        return dot;
    }
}

// tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:314
public partial class obj_item_trash_bee : obj_item_trash
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:324
    [DmParams("mapload", "dead_bee")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob_living_basic_bee dead_bee = Dm.arg(__extra, 0) as mob_living_basic_bee;
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.list(Dm.kv(typeof(datum_reagent_consumable_nutriment_vitamin), 5)), null, mob_living_basic_bee.BEE_FOODGROUPS, 10, 0, Dm.list("bee"), null, 10));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_QUEEN_BEE, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5));
        RegisterSignal(this, COMSIG_ATOM_ON_LAZARUS_INJECTOR, nameof(use_lazarus));
        if (dead_bee == null)
            return dot;
        pixel_x = dead_bee.pixel_x;
        pixel_y = dead_bee.pixel_y;
        bee_type = dead_bee.type;
        if (dead_bee.beegent != null)
        {
            beegent = dead_bee.beegent;
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(beegent.type, 5));
        }
        update_appearance();
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:340
    public override object Destroy(float? force = null)
    {
        beegent = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:344
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        mutable_appearance body_overlay = mutable_appearance(icon: icon, icon_state: "bee_item_overlay") as mutable_appearance;
        body_overlay.color = beegent != null ? beegent.color : "#e5e500"; // BEE_DEFAULT_COLOUR
        Dm.append(ref dot, body_overlay);
        return dot;
    }

    ///Spawn a new bee from this trash item when hit by a lazarus injector and conditions are met.
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:351
    [SignalHandler]
    public virtual object use_lazarus(datum source = null, obj_item_lazarus_injector injector = null, mob user = null)
    {
        if (!Dm.eq(injector.revive_type, SENTIENCE_ORGANIC))
        {
            balloon_alert(user, I18n.T("invalid creature!"));
            return null;
        }
        mob_living_basic_bee revived_bee = Dm.@new(bee_type, drop_location()) as mob_living_basic_bee;
        if (beegent != null)
            Waiting.NoSleep(revived_bee.assign_reagent(beegent));
        Waiting.NoSleep(revived_bee.lazarus_revive(user as mob_living, injector.malfunctioning));
        injector.expend(revived_bee, user);
        qdel(this);
        return LAZARUS_INJECTOR_USED;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:157  var/static/mutable_appearance/greyscale_overlay (made on first use, as tg's)
    internal mutable_appearance mob_living_basic_bee__generate_bee_visuals__greyscale_overlay;
    // tg: code/modules/mob/living/basic/farm_animals/bee/_bee.dm:189  var/static/list/injection_range (made on first use, as tg's)
    internal DmList mob_living_basic_bee__assign_reagent__injection_range;
}

// tg: code/modules/hydroponics/hydroponics.dm:2
public partial class obj_machinery_hydroponics : obj_machinery
{
    /// VARSET_CALLBACK(hydro, recent_bee_visit, FALSE)'s target (the GAP(lead) of modules/assembly/doorcontrol.cs): ___callbackvarset's datum branch for this var.
    public virtual void recent_bee_visit__varset(object var_value = null)
    {
        recent_bee_visit = Dm.num(var_value);
    }
}
