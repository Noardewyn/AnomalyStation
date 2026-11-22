// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/relics.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps RD4, D333: the experimentor): the strange objects of maintenance and the Lavaland boulders, a random themed
// look and name until the experimentor's Discover (or a gift's opening) reveals one hidden power, used in hand once per cooldown: the
// prototypes' corgi cannon, cleaning foam, flashbang, animal summons, teleport, bomb, self-duplicates, free drink, organ scramble,
// recharge, hugs, dimensional shift and costume disguise; the necrotech relics' monster summons, shields, rock spire, area teleport,
// acid foam and blood spray and suck. The monsters of the necrotech summons that are still inert (the constructs, the Lavaland fauna,
// the raptors) appear as inert props (not-translated.md 202). The var block is generated (D111).

// tg: code/modules/research/relics.dm:1  #define RELIC_PROTOTYPE "prototype" (file-local, #undef at :679)
// tg: code/modules/research/relics.dm:2  #define RELIC_NECROTECH "necrotech" (file-local, #undef at :680)

// tg: code/modules/research/relics.dm:4
public partial class obj_item_assembly_relic : obj_item_assembly
{
    private const string RELIC_PROTOTYPE = "prototype";
    private const string RELIC_NECROTECH = "necrotech";

    // tg: code/modules/research/relics.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        random_themed_appearance();
        RegisterSignal(this, COMSIG_ITEM_OPENED_FROM_GIFT, nameof(auto_reveal));
        return dot;
    }

    // tg: code/modules/research/relics.dm:32
    [SignalHandler]
    public virtual void auto_reveal(params object[] __extra)
    {
        reveal();
    }

    // tg: code/modules/research/relics.dm:36
    public virtual void random_themed_appearance()
    {
        object themed_name_prefix;
        object themed_name_suffix;
        if (Dm.eq(artifact_theme, RELIC_PROTOTYPE))
        {
            icon_state = Dm.text("[][]", RELIC_PROTOTYPE, rand(1, 9));
            themed_name_prefix = pick("experimental", "prototype", "artificial", "handcrafted", "ramshackle", "odd");
            themed_name_suffix = pick("device", "assembly", "gadget", "gizmo", "contraption", "machine", "widget", "object");
            real_name = Dm.text("[] []", pick(themed_name_prefix), pick(themed_name_suffix));
            name = I18n.T("strange []", pick(themed_name_suffix));
        }
        if (Dm.eq(artifact_theme, RELIC_NECROTECH))
        {
            icon_state = Dm.text("[][]", RELIC_NECROTECH, rand(1, 6));
            themed_name_prefix = pick("dark", "bloodied", "unholy", "archeotechnological", "dismal", "ruined", "thrumming");
            themed_name_suffix = pick("instrument", "shard", "fetish", "bibelot", "trinket", "offering", "relic");
            real_name = Dm.text("[] []", pick(themed_name_prefix), pick(themed_name_suffix));
            name = I18n.T("strange relic");
        }
        update_appearance();
    }

    // tg: code/modules/research/relics.dm:57
    public virtual void reveal()
    {
        if (Dm.truthy(activated)) //no rerolling
            return;
        activated = TRUE;
        name = real_name;
        if (!Dm.truthy(activation_cooldown))
            activation_cooldown = rand(min_cooldown, max_cooldown);
        if (!Dm.truthy(hidden_power))
        {
            if (Dm.eq(artifact_theme, RELIC_PROTOTYPE))
                hidden_power = pick(
                    nameof(corgi_cannon),
                    nameof(cleaning_foam),
                    nameof(flashbanger),
                    nameof(summon_animals),
                    nameof(uncontrolled_teleport),
                    nameof(heat_and_explode),
                    nameof(rapid_self_dupe),
                    nameof(drink_dispenser),
                    nameof(tummy_ache),
                    nameof(charger),
                    nameof(hugger),
                    nameof(dimensional_shift),
                    nameof(disguiser)
                );
            if (Dm.eq(artifact_theme, RELIC_NECROTECH))
                hidden_power = pick(
                    nameof(dimensional_shift),
                    nameof(summon_animals_monsters),
                    nameof(heat_and_explode),
                    nameof(t1_shield_holder),
                    nameof(t2_shield_holder),
                    nameof(uncontrolled_teleport),
                    nameof(uncontrolled_aoe_teleport),
                    nameof(charger),
                    nameof(place_rocks),
                    nameof(yeet_blood),
                    nameof(suck_blood),
                    nameof(cleaning_foam),
                    nameof(cleaning_foam_acid)
                );
        }
        obj_flags = Dm.bitor(obj_flags, UNIQUE_RENAME);
    }

    // tg: code/modules/research/relics.dm:96
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (!Dm.truthy(activated))
        {
            to_chat(user, span_notice(I18n.T("You aren't quite sure what this is. Maybe R&D knows what to do with it?")));
            return dot;
        }
        await activate(user);
        return dot;
    }

    // tg: code/modules/research/relics.dm:103
    public override async DmTask<object> activate(mob activator = null)
    {
        // COOLDOWN_FINISHED(src, next_activate) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(next_activate <= world.time))
        {
            to_chat(activator, span_warning(I18n.T("[] does not react!", this)));
            return FALSE;
        }
        if (!Dm.truthy(hidden_power))
            return FALSE;
        await Dm.Call(this, Dm.str(hidden_power), activator);
        return await base.activate(activator);
    }

    /// Helper to spawn smoke somewhere
    // tg: code/modules/research/relics.dm:113
    public virtual async DmTask throw_smoke(atom where = null)
    {
        await do_smoke(0, this, get_turf(where));
    }

    /// Helper to show a message to people around the relic
    // tg: code/modules/research/relics.dm:117
    public virtual void relic_message(object message = null)
    {
        atom message_source = (get(this, typeof(mob_living)) as atom) ?? this;
        message_source.visible_message(message);
    }

    // Artefact Powers \\

    /// Throws a corgi somewhere
    // tg: code/modules/research/relics.dm:124
    public virtual async DmTask corgi_cannon(mob user = null)
    {
        playsound(this, SFX_SPARKS, rand(25, 50), TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        var sad_corgi = relic_mob_spawn(typeof(mob_living_basic_pet_dog_corgi)) as mob_living_basic_pet_dog_corgi;
        if (sad_corgi != null)
        {
            await sad_corgi.throw_at(pick(oview(10, user)) as atom, 10, rand(3, 8), callback: Dm.@new<datum_callback>(this, nameof(throw_smoke), sad_corgi));
            warn_admins(user, "Corgi Cannon", FALSE);
        }
        else
            to_chat(user, span_notice(I18n.T("[] vibrates a bit, but nothing happens.", this)));
    }

    /// Spawns cleaning foam
    // tg: code/modules/research/relics.dm:134
    public virtual async DmTask cleaning_foam(mob user = null)
    {
        playsound(this, SFX_SPARKS, rand(25, 50), TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        var spawned_foamer = Dm.@new<obj_item_grenade_chem_grenade_cleaner>(get_turf(this));
        await spawned_foamer.detonate();
        qdel(spawned_foamer);
        warn_admins(user, "Foam", FALSE);
    }

    /// Similar to cleaning foam but spawns the acid variant
    // tg: code/modules/research/relics.dm:142
    public virtual async DmTask cleaning_foam_acid(mob user = null)
    {
        playsound(this, SFX_SPARKS, rand(25, 50), TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        var spawned_foamer = Dm.@new<obj_item_grenade_chem_grenade_ez_clean>(get_turf(this));
        await spawned_foamer.detonate();
        qdel(spawned_foamer);
        warn_admins(user, "Acid Foam", TRUE);
        if (prob(80))
        {
            to_chat(user, span_warning(I18n.T("[] melts apart!", this)));
            await acid_melt();
        }
    }

    /// Flashbangs anyone nearby
    // tg: code/modules/research/relics.dm:153
    public virtual async DmTask flashbanger(mob user = null)
    {
        playsound(this, SFX_SPARKS, rand(25, 50), TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        var spawned_flashbang = Dm.@new<obj_item_grenade_flashbang>(get_turf(this));
        await spawned_flashbang.detonate();
        warn_admins(user, "Flash");
    }

    /// Summon a bunch of random animals, some of which are dangerous
    // tg: code/modules/research/relics.dm:160
    public virtual async DmTask summon_animals(mob user = null)
    {
        DmList valid_animals = TgState.Current.obj_item_assembly_relic__summon_animals__valid_animals ??= Dm.list(
            typeof(mob_living_basic_bear),
            typeof(mob_living_basic_bee),
            typeof(mob_living_basic_butterfly),
            typeof(mob_living_basic_carp),
            typeof(mob_living_basic_crab),
            typeof(mob_living_basic_lizard),
            typeof(mob_living_basic_mouse),
            typeof(mob_living_basic_parrot),
            typeof(mob_living_basic_pet_cat),
            typeof(mob_living_basic_pet_dog_corgi),
            typeof(mob_living_basic_pet_dog_pug),
            typeof(mob_living_basic_pet_fox)
        ); // var/static/list/valid_animals
        float actually_spawned = 0;
        float count = rand(1, 25);
        for (float counter = 1; counter <= count; counter++)
        {
            object animal_spawn = pick(valid_animals);
            var animal = relic_mob_spawn(animal_spawn as Type);
            if (animal != null)
                actually_spawned++;
        }
        object message = actually_spawned != 0
            ? span_danger(I18n.T("[] begins to shake, and in the distance the sound of rampaging animals arises!", this))
            : span_warning(I18n.T("[] begins to shake, but nothing happens...", this));
        visible_message(message);
        to_chat(user, message);
        if (actually_spawned != 0)
        {
            warn_admins(user, "Mass Mob Spawn");
            if (prob(60))
            {
                relic_message(span_warning(I18n.T("[] falls apart!", this)));
                await deconstruct(FALSE);
            }
        }
    }

    /// Version of summon_animals that spawns mostly lavaland monsters
    // tg: code/modules/research/relics.dm:191
    public virtual async DmTask summon_animals_monsters(mob user = null)
    {
        DmList valid_monsters = TgState.Current.obj_item_assembly_relic__summon_animals_monsters__valid_monsters ??= Dm.list(
            typeof(mob_living_basic_construct_artificer_hostile),
            typeof(mob_living_basic_construct_juggernaut_hostile),
            typeof(mob_living_basic_construct_proteon_hostile),
            typeof(mob_living_basic_construct_wraith_hostile),
            typeof(mob_living_basic_mining_brimdemon),
            typeof(mob_living_basic_mining_goldgrub),
            typeof(mob_living_basic_mining_goliath),
            typeof(mob_living_basic_mining_hivelord),
            typeof(mob_living_basic_mining_ice_demon),
            typeof(mob_living_basic_mining_legion),
            typeof(mob_living_basic_mining_lobstrosity),
            typeof(mob_living_basic_mining_watcher),
            typeof(mob_living_basic_raptor_blue),
            typeof(mob_living_basic_raptor_green),
            typeof(mob_living_basic_raptor_purple),
            typeof(mob_living_basic_raptor_red),
            typeof(mob_living_basic_raptor_white),
            typeof(mob_living_basic_raptor_yellow)
        ); // var/static/list/valid_monsters
        float actually_spawned = 0;
        float count = rand(3, 9);
        for (float counter = 1; counter <= count; counter++)
        {
            object animal_spawn = pick(valid_monsters);
            var animal = relic_mob_spawn(animal_spawn as Type);
            if (animal != null)
                actually_spawned++;
        }
        object message = actually_spawned != 0
            ? span_danger(I18n.T("[] begins to shake, and in the distance the sound of roaring arises!", this))
            : span_warning(I18n.T("[] begins to shake, but nothing happens...", this));
        visible_message(message);
        to_chat(user, message);
        if (actually_spawned != 0)
        {
            warn_admins(user, "Mass Mob Spawn (Monster)");
            if (prob(80))
            {
                relic_message(span_warning(I18n.T("[] falls apart!", this)));
                await deconstruct(FALSE);
            }
        }
    }

    /// Spawns a bunch of mimics of the relic which also can spawn relics, but despawn shortly
    // tg: code/modules/research/relics.dm:228
    public virtual async DmTask rapid_self_dupe(mob user = null)
    {
        audible_message(I18n.T("[] emits a loud pop!", this));
        DmList dummy_artifacts = Dm.list();
        float count = rand(5, 10);
        for (float counter = 1; counter <= count; counter++)
        {
            var duped = Dm.@new(type, get_turf(this)) as obj_item_assembly_relic;
            duped.name = name;
            duped.desc = desc;
            duped.real_name = real_name;
            duped.hidden_power = hidden_power;
            duped.activated = TRUE;
            Dm.append(ref dummy_artifacts, duped);
            await duped.throw_at(pick(oview(7, get_turf(this))) as atom, 10, 1);
        }
        // QDEL_LIST_IN(dummy_artifacts, rand(1 SECONDS, 10 SECONDS)) (__DEFINES/qdel.dm:65), inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.______qdel_list_wrapper), dummy_artifacts), rand(1 * SECONDS, 10 * SECONDS), TIMER_STOPPABLE, file: "code/modules/research/relics.dm", line: 241);
        warn_admins(user, "Rapid duplicator", 0);
    }

    /// Explodes after a few seconds
    // tg: code/modules/research/relics.dm:245
    public virtual void heat_and_explode(mob user = null)
    {
        to_chat(user, span_danger(I18n.T("[] begins to heat up!", this)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(blow_up), user), rand(3.5f * SECONDS, 10 * SECONDS), file: "code/modules/research/relics.dm", line: 247);
    }

    // tg: code/modules/research/relics.dm:249
    public virtual async DmTask blow_up(mob user = null)
    {
        if (user != null && get(this, typeof(mob_living)) != user)
            return;
        visible_message(span_notice(I18n.T("\\The []'s top opens, releasing a powerful blast!", this)));
        await explosion(this, heavy_impact_range: rand(1, 5), light_impact_range: rand(1, 5), flame_range: 2, flash_range: rand(1, 5), adminlog: TRUE);
        warn_admins(user, "Explosion");
        await deconstruct(FALSE); //Comment this line to produce a light grenade (the bomb that keeps on exploding when used)!!
    }

    /// Teleports the relic, and anyone holding it, to a random location nearby
    // tg: code/modules/research/relics.dm:258
    public virtual void uncontrolled_teleport(mob user = null)
    {
        to_chat(user, span_notice(I18n.T("[] begins to vibrate!", this)));

        float teleport_time = rand(1 * SECONDS, 3 * SECONDS);
        _addtimer(Dm.@new<datum_callback>(this, nameof(do_the_teleport), user), teleport_time, file: "code/modules/research/relics.dm", line: 262);
        Shake(1, 1, teleport_time, 0.05f * SECONDS);
    }

    // tg: code/modules/research/relics.dm:265
    public virtual async DmTask do_the_teleport(mob user = null)
    {
        turf userturf = get_turf(this);
        atom_movable to_teleport = get_loose_container(this, container_flags: ALL);
        to_teleport.visible_message(span_notice(I18n.T("[] twists and bends, relocating itself!", to_teleport)));
        await throw_smoke(get_turf(to_teleport));
        await do_teleport(to_teleport, userturf, 8, asoundin: Dm.resource("sound/effects/phasein.ogg"), channel: TELEPORT_CHANNEL_BLUESPACE);
        await throw_smoke(get_turf(to_teleport));
        warn_admins(user, "Teleport", FALSE);
    }

    /// Version of uncontrolled_teleport with cult theming, and that affects all nearby movables rather than just the relic
    // tg: code/modules/research/relics.dm:275
    public virtual void uncontrolled_aoe_teleport(mob user = null)
    {
        to_chat(user, span_notice(I18n.T("[] begins to vibrate intensely!", this)));

        float teleport_time = rand(1 * SECONDS, 3 * SECONDS);
        _addtimer(Dm.@new<datum_callback>(this, nameof(do_the_aoe_teleport), user), teleport_time, file: "code/modules/research/relics.dm", line: 279);
        Shake(2, 2, teleport_time, 0.03f * SECONDS);
    }

    // tg: code/modules/research/relics.dm:282
    public virtual async DmTask do_the_aoe_teleport(mob user = null)
    {
        visible_message(span_notice(I18n.T("[] twists and bends, relocating anything nearby!", this)));
        turf teleturf = get_turf(this);
        foreach (atom_movable nearby in Dm.each<atom_movable>(view(2, teleturf)))
        {
            if (Dm.truthy(nearby.anchored) || Dm.truthy(nearby.invisibility) || HAS_TRAIT(nearby, TRAIT_UNDERFLOOR))
                continue;
            if (isliving(nearby))
            {
                var nearby_living = (mob_living)nearby;
                if (Dm.truthy(nearby_living.can_block_magic(MAGIC_RESISTANCE_HOLY, 1)))
                    continue;
            }

            if (isliving(nearby))
                Dm.@new<obj_effect_temp_visual_dir_setting_cult_phase_out>(nearby.loc, nearby.dir);
            else
                Dm.@new<obj_effect_temp_visual_cult_sparks>(nearby.loc);
            await do_teleport(
                teleatom: nearby,
                destination: teleturf,
                precision: 5,
                asoundin: Dm.resource("sound/effects/phasein.ogg"),
                channel: TELEPORT_CHANNEL_CULT
            );
            if (isliving(nearby))
                Dm.@new<obj_effect_temp_visual_dir_setting_cult_phase>(nearby.loc, nearby.dir);
            else
                Dm.@new<obj_effect_temp_visual_cult_sparks>(nearby.loc);
        }

        warn_admins(user, "AOE Teleport");
        if (prob(30))
        {
            relic_message(span_warning(I18n.T("[] teleports away, never to be seen again!", this)));
            qdel(this);
        }
    }

    // Creates a glass and fills it up with a drink.
    // tg: code/modules/research/relics.dm:314
    public virtual void drink_dispenser(mob user = null)
    {
        var freebie = Dm.@new<obj_item_reagent_containers_cup_glass_drinkingglass>(get_step_rand(this));
        playsound(freebie, SFX_SPARKS, rand(25, 50), TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        do_sparks(5, TRUE, this, this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(dispense_drink), freebie), 0.5f * SECONDS, file: "code/modules/research/relics.dm", line: 319);
    }

    // tg: code/modules/research/relics.dm:321
    public virtual async DmTask dispense_drink(obj_item_reagent_containers_cup_glass glasser = null)
    {
        playsound(glasser, Dm.resource("sound/effects/phasein.ogg"), rand(25, 50), TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        await (glasser.reagents as datum_reagents).add_reagent(get_random_reagent_id(whitelist: subtypesof(typeof(datum_reagent_consumable_ethanol))), rand(Dm.num(glasser.volume) * 0.3f, Dm.num(glasser.volume)));
        await throw_smoke(get_turf(glasser));
    }

    /// Scrambles your organs. 33% chance to delete after use.
    // tg: code/modules/research/relics.dm:328
    public virtual void tummy_ache(mob user = null)
    {
        Dm.@new<obj_effect_temp_visual_circle_wave_bioscrambler_light>(get_turf(this));
        to_chat(user, span_notice(I18n.T("Your stomach starts growling...")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(scrambliticus), user), rand(1 * SECONDS, 3 * SECONDS), file: "code/modules/research/relics.dm", line: 332); // throw it away!
    }

    // tg: code/modules/research/relics.dm:334
    public virtual async DmTask scrambliticus(mob user = null)
    {
        Dm.@new<obj_effect_temp_visual_circle_wave_bioscrambler_light>(get_turf(this));
        playsound(this, Dm.resource("sound/effects/magic/cosmic_energy.ogg"), vol: 50, vary: TRUE);
        foreach (mob_living_carbon nearby in Dm.each<mob_living_carbon>(range(2, get_turf(this)))) //needs get_turf() to work
        {
            await nearby.bioscramble(name);
            playsound(nearby, SFX_SPARKS, rand(25, 50), TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            await throw_smoke(get_turf(nearby));
            to_chat(nearby, span_notice(I18n.T("You feel weird.")));
        }
        if (prob(33))
        {
            relic_message(span_warning(I18n.T("[] falls apart!", this)));
            await deconstruct(FALSE);
        }
    }

    /// Charges an item or two in your inventory. Also yourself.
    // tg: code/modules/research/relics.dm:347
    public virtual async DmTask charger(mob_living user = null)
    {
        playsound(this, SFX_SPARKS, rand(25, 50), TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        var mob_to_charge = get(this, typeof(mob_living)) as mob_living;
        if (mob_to_charge == null)
            return;
        to_chat(mob_to_charge, span_danger(I18n.T("You're recharged!")));
        float stunner = 1.25f * SECONDS;
        if (iscarbon(mob_to_charge))
        {
            var carboner = (mob_living_carbon)mob_to_charge;
            await carboner.electrocute_act(15, this, null, SHOCK_NOGLOVES, null, null, stunner);
        }
        else
            await mob_to_charge.electrocute_act(15, this, flags: SHOCK_NOGLOVES);

        DmList chargeable_items = mob_to_charge.get_all_cells(max_percent: 0.95f) as DmList; // otherwise the PDA always gets recharged

        lightning_fx(mob_to_charge, stunner);
        float recharges = rand(1, 2);
        if (Dm.length(chargeable_items) == 0)
        {
            to_chat(mob_to_charge, span_notice(I18n.T("You have a strange feeling for a moment, but then it passes.")));
            return;
        }
        while (Dm.length(chargeable_items) != 0 && recharges != 0)
        {
            recharges--;
            var to_charge_base = pick(chargeable_items) as obj_item;
            var to_charge = chargeable_items[to_charge_base] as obj_item_stock_parts_power_store;
            chargeable_items.Remove(to_charge_base);
            to_charge.charge = to_charge.maxcharge;
            to_charge_base.update_appearance(Dm.bitor(UPDATE_ICON, UPDATE_OVERLAYS));
            to_chat(mob_to_charge, span_notice(I18n.T("[] feels energized!", to_charge_base)));
            lightning_fx(to_charge_base, 0.8f * SECONDS);
        }
    }

    // tg: code/modules/research/relics.dm:377
    public virtual void lightning_fx(atom shocker = null, object time = null)
    {
        object lightning = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "electricity3", layer: ABOVE_MOB_LAYER);
        shocker.add_overlay(lightning);
        _addtimer(Dm.@new<datum_callback>(this, nameof(cut_the_overlay), shocker, lightning), Dm.num(time), file: "code/modules/research/relics.dm", line: 380);
    }

    // tg: code/modules/research/relics.dm:382
    public virtual void cut_the_overlay(atom shocker = null, object lightning = null)
    {
        shocker.cut_overlay(lightning);
    }

    /// Hugs/shakes everyone in range!
    // tg: code/modules/research/relics.dm:386
    public virtual async DmTask hugger(mob user = null)
    {
        var hugger = get(this, typeof(mob_living)) as mob_living;
        DmList huggeds = oviewers(3, hugger);
        foreach (mob_living_carbon victim in Dm.each<mob_living_carbon>(huggeds))
        {
            await victim.help_shake_act(hugger as mob_living_carbon, force_friendly: TRUE);
            Dm.@new<obj_effect_temp_visual_heart>(victim.loc);
        }
        if (Dm.length(huggeds) != 0)
            to_chat(hugger, span_nicegreen(I18n.T("You feel friendly!")));
        else
            to_chat(hugger, pick(span_notice(I18n.T("You hug yourself, for some reason.")), span_notice(I18n.T("You have a strange feeling for a moment, but then it passes."))));
    }

    /// Converts a 3x3 area into a random dimensional theme.
    // tg: code/modules/research/relics.dm:398
    public virtual async DmTask dimensional_shift(mob user = null)
    {
        object new_theme_path = pick(subtypesof(typeof(datum_dimension_theme)));
        var shifter = SSmaterials.dimensional_themes[new_theme_path] as datum_dimension_theme;
        foreach (turf shiftee in Dm.each<turf>(range(1, get_turf(this))))
            await shifter.apply_theme(shiftee, show_effect: TRUE);
        // prevent *total* spam conversion
        activation_cooldown = Dm.num(activation_cooldown) + 2 * SECONDS;
    }

    /// Replaces your clothing with a random costume, and your ID with a cardboard one.
    /// TODO: make them part of the same kit (lobster hat, lobster suit)
    // tg: code/modules/research/relics.dm:409
    public virtual async DmTask disguiser(mob user = null)
    {
        var to_disguise = get(this, typeof(mob_living)) as mob_living;
        if (!iscarbon(to_disguise))
        {
            to_chat(to_disguise, span_notice(I18n.T("You have a strange feeling for a moment, but then it passes.")));
            return;
        }

        if (prob(80)) // >:)
            ADD_TRAIT(to_disguise, TRAIT_NO_JUMPSUIT, REF(this)); // prevent dropping pockets & belt

        // magic trick!
        playsound(to_disguise, SFX_SPARKS, rand(25, 50), TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        await throw_smoke(to_disguise);

        if (!ishuman(to_disguise))
        {
            to_chat(to_disguise, span_notice(I18n.T("You have a peculiar feeling for a moment, but then it passes.")));
            return;
        }

        var humerus = (mob_living_carbon_human)to_disguise;
        //hat
        var disguise_hat = roll_costume(ITEM_SLOT_HEAD, HIDEMASK);
        await humerus.dropItemToGround(humerus.head);
        await humerus.equip_to_slot_or_del(disguise_hat, ITEM_SLOT_HEAD);
        // uniform
        var disguise_uniform = roll_costume(ITEM_SLOT_ICLOTHING);
        await humerus.dropItemToGround(humerus.w_uniform);
        await humerus.equip_to_slot_or_del(disguise_uniform, ITEM_SLOT_ICLOTHING);
        // suit
        var disguise_suit = roll_costume(ITEM_SLOT_OCLOTHING);
        await humerus.dropItemToGround(humerus.wear_suit);
        await humerus.equip_to_slot_or_del(disguise_suit, ITEM_SLOT_OCLOTHING);
        // id
        var card_id = Dm.@new<obj_item_card_cardboard>();
        await humerus.dropItemToGround(humerus.wear_id);
        await humerus.equip_to_slot_or_del(card_id, ITEM_SLOT_ID);

        // edit the card to a random job & name
        if (card_id == null || QDELETED(card_id))
            return;
        card_id.scribbled_name = Dm.text("[] []", pick(GLOB.first_names), pick(GLOB.last_names));
        card_id.details_colors = Dm.list(ready_random_color(), ready_random_color(), ready_random_color());
        card_id.item_flags = Dm.num(Dm.bitor(card_id.item_flags, DROPDEL));

        Type random_trim_type = pick(subtypesof(typeof(datum_id_trim))) as Type;
        var random_trim = Dm.@new(random_trim_type) as datum_id_trim;
        if (Dm.truthy(random_trim.trim_state) && Dm.truthy(random_trim.assignment))
            card_id.scribbled_trim = replacetext(random_trim.trim_state, "trim_", "cardboard_");
        card_id.scribbled_assignment = random_trim.assignment;
        card_id.update_appearance();
        REMOVE_TRAIT(to_disguise, TRAIT_NO_JUMPSUIT, REF(this));
    }

    // tg: code/modules/research/relics.dm:460
    public virtual obj_item roll_costume(object slot = null, object flagcheck = null)
    {
        DmList candidates = Dm.list();
        foreach (Type costume in Dm.each<Type>(GLOB.all_autodrobe_items))
        {
            if (Dm.truthy(flagcheck) && !Dm.truthy(Dm.bitand(Dm.initial(costume, (obj_item c) => c.flags_inv), flagcheck)))
                continue;
            if (Dm.truthy(slot) && !Dm.truthy(Dm.bitand(Dm.initial(costume, (obj_item c) => c.slot_flags), slot)))
                continue;
            Dm.combine(ref candidates, costume);
        }
        Type new_costume_type = pick(candidates) as Type;
        var new_costume = Dm.@new(new_costume_type) as obj_item;
        new_costume.item_flags = Dm.num(Dm.bitor(new_costume.item_flags, DROPDEL));
        return new_costume;
    }

    /// Makes the relic holder have a shield that blocks 3 common attacks
    // tg: code/modules/research/relics.dm:474
    public virtual async DmTask t1_shield_holder(mob user = null)
    {
        var outermost_item = (obj_item)get_loose_container(this, container_flags: NONE);
        var shield = await outermost_item.AddComponent(
            typeof(datum_component_shielded),
            Dm.named("max_charges", 3),
            Dm.named("recharge_start_delay", 0),
            Dm.named("shield_inhand", TRUE),
            Dm.named("show_charge_as_alpha", TRUE),
            Dm.named("can_block_overwhelming", FALSE),
            Dm.named("shield_icon_file", Dm.resource("icons/effects/effects.dmi")),
            Dm.named("shield_icon", "at_shield1"),
            Dm.named("run_hit_callback", Dm.@new<datum_callback>(this, nameof(shield_hit), outermost_item))
        ) as datum_component;
        object old_light_system = outermost_item.light_system;
        outermost_item.light_system = OVERLAY_LIGHT;
        var light = await outermost_item.AddComponent(
            typeof(datum_component_overlay_lighting),
            Dm.named("_range", 2.5f),
            Dm.named("_power", 1.5f),
            Dm.named("_color", COLOR_BIOLUMINESCENCE_YELLOW),
            Dm.named("starts_on", TRUE)
        ) as datum_component;

        _addtimer(Dm.@new<datum_callback>(this, nameof(remove_shield), outermost_item, old_light_system, Dm.list(shield, light)), Dm.num(activation_cooldown) * 0.5f, file: "code/modules/research/relics.dm", line: 500);
        outermost_item.add_filter("block_shield", 1, outline_filter(0, COLOR_BIOLUMINESCENCE_YELLOW) as DmList);
        outermost_item.transition_filter("block_shield", outline_filter(2, COLOR_BIOLUMINESCENCE_YELLOW) as DmList, Dm.num(activation_cooldown) * 0.5f);

        relic_message(span_notice(I18n.T("[] starts to glow a bright yellow!", outermost_item)));
        warn_admins(user, "Shield", FALSE);
    }

    /// Makes the relic holder have a shield that blocks 1 powerful attack
    // tg: code/modules/research/relics.dm:508
    public virtual async DmTask t2_shield_holder(mob user = null)
    {
        var outermost_item = (obj_item)get_loose_container(this, container_flags: NONE);
        var shield = await outermost_item.AddComponent(
            typeof(datum_component_shielded),
            Dm.named("max_charges", 1),
            Dm.named("recharge_start_delay", 0),
            Dm.named("shield_inhand", TRUE),
            Dm.named("show_charge_as_alpha", TRUE),
            Dm.named("can_block_overwhelming", TRUE),
            Dm.named("shield_icon_file", Dm.resource("icons/effects/effects.dmi")),
            Dm.named("shield_icon", "at_shield2"),
            Dm.named("run_hit_callback", Dm.@new<datum_callback>(this, nameof(shield_hit), outermost_item))
        ) as datum_component;
        object old_light_system = outermost_item.light_system;
        outermost_item.light_system = OVERLAY_LIGHT;
        var light = await outermost_item.AddComponent(
            typeof(datum_component_overlay_lighting),
            Dm.named("_range", 3),
            Dm.named("_power", 1.5f),
            Dm.named("_color", COLOR_BIOLUMINESCENCE_YELLOW),
            Dm.named("starts_on", TRUE)
        ) as datum_component;

        _addtimer(Dm.@new<datum_callback>(this, nameof(remove_shield), outermost_item, old_light_system, Dm.list(shield, light)), Dm.num(activation_cooldown) * 0.5f, file: "code/modules/research/relics.dm", line: 534);
        outermost_item.add_filter("block_shield", 1, outline_filter(0, COLOR_BIOLUMINESCENCE_YELLOW) as DmList);
        outermost_item.transition_filter("block_shield", outline_filter(2, COLOR_BIOLUMINESCENCE_YELLOW) as DmList, Dm.num(activation_cooldown) * 0.5f);

        relic_message(span_notice(I18n.T("[] starts to glow a bright yellow!", outermost_item)));
        warn_admins(user, "Shield", FALSE);
    }

    // tg: code/modules/research/relics.dm:541
    public virtual async DmTask shield_hit(obj_item comp_parent = null, mob_living owner = null, object attack_text = null, object current_charges = null)
    {
        playsound(comp_parent, Dm.resource("sound/items/weapons/marauder.ogg"), 20, TRUE, frequency: 1.25f);
        if (Dm.truthy(owner.is_holding(comp_parent)))
            owner.visible_message(span_danger(I18n.T("[] holds [] up, blocking [] with a projected shield!", owner, comp_parent, attack_text)));
        else
            owner.visible_message(span_danger(I18n.T("[]'s [] blocks [] with a projected shield!", owner, comp_parent.name, attack_text)));
        if (Dm.num(current_charges) <= 0)
        {
            await comp_parent.set_light_on(FALSE);
            relic_message(span_notice(I18n.T("[] stops glowing.", comp_parent)));
            comp_parent.remove_filter("block_shield");
        }
    }

    // tg: code/modules/research/relics.dm:553
    public virtual void remove_shield(obj_item affected = null, object old_light_system = null, DmList cleanup_components = null)
    {
        foreach (datum_component comp in Dm.each<datum_component>(cleanup_components))
            qdel(comp);
        affected.light_system = Dm.initial(GetType(), (atom a) => a.light_system); // tg: initial(light_system), the relic's own
        if (Dm.truthy(affected.light_on))
            relic_message(span_notice(I18n.T("[] stops glowing.", affected)));
        affected.remove_filter("block_shield");
    }

    /// Places rock turfs around the relic
    // tg: code/modules/research/relics.dm:562
    public virtual async DmTask place_rocks(mob user = null)
    {
        relic_message(span_notice(I18n.T("A spire of rock erupts from the ground beneath []!", this)));
        playsound(this, Dm.resource("sound/effects/rock/rock_break.ogg"), 50, TRUE);
        turf spawnloc = get_turf(this);
        foreach (turf_open open_turf in Dm.each<turf_open>(RANGE_TURFS(rand(1, 2), spawnloc)))
        {
            var rock = await open_turf.place_on_top(typeof(turf_closed_mineral_asteroid)) as turf;
            if (rock is turf_closed_mineral_asteroid asteroid)
            {
                asteroid.name = I18n.T("fragile rock");
                asteroid.weak_turf = TRUE;
                asteroid.tool_mine_speed = 2 * SECONDS;
                asteroid.hand_mine_speed = 10 * SECONDS;
            }
            foreach (mob_living within_rock in Dm.each<mob_living>(rock?.contents))
            {
                await within_rock.Paralyze(1 * SECONDS);
                await within_rock.Knockdown(3 * SECONDS);
                await within_rock.apply_damage(10, BRUTE, BODY_ZONE_CHEST, blocked: Dm.num(within_rock.getarmor(BODY_ZONE_CHEST, MELEE)), wound_bonus: 10, exposed_wound_bonus: 10);
                to_chat(within_rock, span_danger(I18n.T("You are smashed by []!", rock)));
            }
        }
        warn_admins(user, "Rocks", FALSE);
        if (prob(20))
        {
            relic_message(span_warning(I18n.T("[] crumbles into dust!", this)));
            await deconstruct(FALSE);
        }
    }

    /// User sprays out blood in all directions
    /// Has a small chance of changing the power to suck_blood
    // tg: code/modules/research/relics.dm:585
    public virtual void yeet_blood(mob_living user = null)
    {
        float yeet_time = rand(1 * SECONDS, 3 * SECONDS);
        add_filter("blood_outgoing", 1, outline_filter(0, COLOR_DARK) as DmList);
        transition_filter("blood_outgoing", outline_filter(2, BLOOD_COLOR_RED) as DmList, yeet_time);
        if (user is mob_living && Dm.truthy(CAN_HAVE_BLOOD(user)))
            to_chat(user, span_danger(I18n.T("[] starts glowing an ominous red!", this)));
        else
            to_chat(user, span_danger(I18n.T("[] starts glowing an ominous red...", this)));

        _addtimer(Dm.@new<datum_callback>(this, nameof(actually_yeet_blood)), yeet_time, file: "code/modules/research/relics.dm", line: 594);
    }

    // tg: code/modules/research/relics.dm:596
    public virtual async DmTask actually_yeet_blood()
    {
        var user = get_loose_container(this, container_flags: LOOSE_CONTAINER_INCLUDE_INVENTORY) as mob_living;
        float splatcount = 0;
        if (user != null && Dm.truthy(CAN_HAVE_BLOOD(user)) && !Dm.truthy(user.can_block_magic(MAGIC_RESISTANCE_HOLY, 1)))
        {
            foreach (object splatdir in Dm.each(GLOB.alldirs))
            {
                if (prob(splatcount * 5))
                    continue;
                float strength = rand(2, 3);
                await user.spray_blood(splatdir, strength);
                await user.bleed(Dm.pow(strength, 2));
                splatcount += strength;
            }
        }

        if (splatcount > 0)
        {
            relic_message(span_warning(I18n.T("Blood sprays out from []!", this)));
            warn_admins(user, "Blood Dispersal", FALSE);
            playsound(this, Dm.resource("sound/effects/wounds/blood3.ogg"), 50, TRUE);
            if (prob(20))
                hidden_power = nameof(suck_blood);
        }
        else
            relic_message(span_warning(I18n.T("[] pulses ominously, but nothing happens!", this)));

        transition_filter("blood_outgoing", outline_filter(0, COLOR_DARK) as DmList, 1 * SECONDS);
        _addtimer(Dm.@new<datum_callback>(this, nameof(remove_filter), "blood_outgoing"), 2 * SECONDS, file: "code/modules/research/relics.dm", line: 618);
    }

    /// Nearby mobs transfer blood to the user
    /// Has a small chance of changing the power to yeet_blood
    // tg: code/modules/research/relics.dm:622
    public virtual void suck_blood(mob_living user = null)
    {
        float suck_time = rand(1 * SECONDS, 3 * SECONDS);
        add_filter("blood_incoming", 1, outline_filter(0, COLOR_DARK) as DmList);
        transition_filter("blood_incoming", outline_filter(2, BLOOD_COLOR_RED) as DmList, suck_time);
        if (user is mob_living && Dm.truthy(CAN_HAVE_BLOOD(user)))
            to_chat(user, span_danger(I18n.T("[] starts glowing an ominous red!", this)));
        else
            to_chat(user, span_danger(I18n.T("[] starts glowing an ominous red...", this)));

        _addtimer(Dm.@new<datum_callback>(this, nameof(actually_suck_blood)), suck_time, file: "code/modules/research/relics.dm", line: 631);
    }

    // tg: code/modules/research/relics.dm:633
    public virtual async DmTask actually_suck_blood()
    {
        var user = get_loose_container(this, container_flags: LOOSE_CONTAINER_INCLUDE_INVENTORY) as mob_living;
        bool any_affected = false;
        if (user != null && Dm.truthy(CAN_HAVE_BLOOD(user)) && !Dm.truthy(user.can_block_magic(MAGIC_RESISTANCE_HOLY, 1)))
        {
            foreach (mob_living nearby in Dm.each<mob_living>(view(2, user)))
            {
                if (nearby == user || !Dm.truthy(CAN_HAVE_BLOOD(nearby)) || Dm.truthy(nearby.can_block_magic(MAGIC_RESISTANCE_HOLY, 1)))
                    continue;
                await nearby.transfer_blood_to(user, rand(6, 10), ignore_low_blood: TRUE, ignore_incompatibility: TRUE, transfer_viruses: FALSE);
                to_chat(nearby, span_danger(I18n.T("You feel a sudden weakness as blood is drawn out of you []!", Dm.truthy(nearby.is_blind()) ? "" : I18n.T(" and into []", user))));
                any_affected = true;
                nearby.Beam(user, icon_state: "blood", time: 1 * SECONDS);
                Dm.@new<obj_effect_temp_visual_cult_sparks>(nearby.loc);
            }
        }

        if (any_affected)
        {
            playsound(this, Dm.resource("sound/effects/magic/enter_blood.ogg"), 50, TRUE);
            relic_message(span_warning(I18n.T("Blood from nearby creatures is drawn towards [], and into []!", this, user)));
            warn_admins(user, "Blood Suck");
            if (prob(10))
                hidden_power = nameof(yeet_blood);
        }
        else
            relic_message(span_warning(I18n.T("[] pulses ominously, but nothing happens!", this)));

        transition_filter("blood_incoming", outline_filter(0, COLOR_DARK) as DmList, 1 * SECONDS);
        _addtimer(Dm.@new<datum_callback>(this, nameof(remove_filter), "blood_incoming"), 2 * SECONDS, file: "code/modules/research/relics.dm", line: 658);
    }

    /// Alerts admins on usage of dagnerous relics
    // tg: code/modules/research/relics.dm:661
    public virtual void warn_admins(mob user = null, object relic_type = null, object priority = null)
    {
        priority ??= TRUE;
        turf location = get_turf(this);
        string log_msg = Dm.text("[] relic used by [] in []", relic_type, key_name(user), AREACOORD(location));
        if (Dm.truthy(priority))
            message_admins(Dm.text("[] relic activated by [] in []", relic_type, ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(location)));
        log_game(log_msg);
        investigate_log(log_msg, "experimentor");
    }

    // tg: code/modules/research/relics.dm:670
    public virtual mob_living relic_mob_spawn(Type mob_type = null)
    {
        if (Dm.length(datum_controller_subsystem_mobs.relicmobs) >= Dm.num(config.Get(typeof(datum_config_entry_number_relicmobcap))))
            return null;
        var spawned = Dm.@new(mob_type, get_turf(this)) as mob_living;
        ADD_TRAIT(spawned, TRAIT_SPAWNED_MOB, INNATE_TRAIT);
        SSmobs.register_relic_mob(spawned);
        return spawned;
    }
}

// Subtypes that spawn revealed, primarily for debug/testing/badmin purposes
// tg: code/modules/research/relics.dm:686
public partial class obj_item_assembly_relic_revealed : obj_item_assembly_relic
{
    // tg: code/modules/research/relics.dm:688
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        auto_reveal();
        return dot;
    }
}

// tg: code/modules/research/relics.dm:694
public partial class obj_item_assembly_relic_lavaland_revealed : obj_item_assembly_relic_lavaland
{
    // tg: code/modules/research/relics.dm:694
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        auto_reveal();
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/research/relics.dm:161  var/static/list/valid_animals (summon_animals)
    internal DmList obj_item_assembly_relic__summon_animals__valid_animals;
    // tg: code/modules/research/relics.dm:192  var/static/list/valid_monsters (summon_animals_monsters)
    internal DmList obj_item_assembly_relic__summon_animals_monsters__valid_monsters;
}
