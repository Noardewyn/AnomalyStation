// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/grenades/ghettobomb.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole, with the explosive lance (weaponry/melee/spear.dm) that carries one. The halved pipe: up to
// three stuffings (meat for gibs, paper for confetti, a shard for shrapnel, refined bluespace to scatter who stands near), five
// units of a fuel at least (welding fuel, plasma, gunpowder, nitroglycerin, TATP, each worth its share of the blast), fifteen
// lengths of cable, then a timer, a proximity sensor, a mousetrap, a signaler or a voice analyzer makes the IED. The IED: its
// assembly armed in hand (not a signaler), set up off hand, it goes off when its assembly pulses, drawn as tg's pipe bomb with its
// assembly's name and armed state, its blast set by its power (heavy a fifth, light seven tenths, flame its power give or take
// one). The admin-spawned one has a timer and 2.5 power. The wiremod assembly in the halved pipe's list is inert with the
// circuits (not-translated.md). The activator var holds a path or the assembly itself (the converter typed it as a path):
// hand-declared here, so hand mode keeps the generated constructor here too. The rest of the var blocks is generated (D111).

// tg: code/game/objects/items/grenades/ghettobomb.dm:1
public partial class obj_item_grenade_iedcasing : obj_item_grenade
{
    /// Our assembly that when activated causes us to explode
    // tg: code/game/objects/items/grenades/ghettobomb.dm:23  var/obj/item/assembly/activator (a path on the spawned kind)
    public object activator;

    // tg: code/game/objects/items/grenades/ghettobomb.dm:2  (the generated constructor, kept here: hand mode, see the header)
    public obj_item_grenade_iedcasing()
    {
        name = "improvised explosive";
        desc = "An improvised explosive device.";
        w_class = 2;
        icon = Dm.resource("icons/obj/weapons/grenade.dmi");
        base_icon_state = "pipebomb";
        icon_state = "slicedapart";
        inhand_icon_state = "flashbang";
        lefthand_file = Dm.resource("icons/mob/inhands/equipment/security_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/equipment/security_righthand.dmi");
        throw_speed = 3;
        throw_range = 7;
        obj_flags = 4096;
        slot_flags = 512;
        active = 0;
        shrapnel_type = typeof(obj_projectile_bullet_shrapnel_ied);
        det_time = 2250;
        display_timer = 0;
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:19  (the generated var initializer, kept here: hand mode)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), 165), Dm.kv(typeof(datum_material_glass), 20)); // tg: code/game/objects/items/grenades/ghettobomb.dm:19
        effects = Dm.list(Dm.kv(typeof(obj_item_food_meat_slab), 0), Dm.kv(typeof(obj_item_paper), 0), Dm.kv(typeof(obj_item_shard), 0), Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal_refined), 0)); // tg: code/game/objects/items/grenades/ghettobomb.dm:25
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (activator is Type activator_type)
        {
            obj_item_assembly new_activator = Dm.@new(activator_type, this) as obj_item_assembly;
            Waiting.NoSleep(new_activator.toggle_secure());
            activator = null;
            Waiting.NoSleep(attach_activator(new_activator));
        }
        return dot;
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:42
    public virtual void setup_effects_from_contents()
    {
        foreach (object item in Dm.each(contents))
        {
            foreach (object effect_type in Dm.each(effects))
            {
                if (!istype(item, (Type)effect_type))
                    continue;
                if (item is obj_item_stack as_stack)
                    effects[effect_type] = Dm.num(effects[effect_type]) + Dm.num(as_stack.amount);
                else
                    effects[effect_type] = Dm.num(effects[effect_type]) + 1;
                break;
            }
        }
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:54
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        Dm.append(dot, span_notice(I18n.T("Using it in-hand activates the assembly, which means timers start timing and so on.")));
        Dm.append(dot, span_notice(I18n.T("Using it off-hand allows you to configure the assembly, if possible.")));
        if (contents.len > 1) // above 1, so more than just the activator
            Dm.append(dot, span_warning(I18n.T("It seems to have something stuffed in it.")));
        if (activator is not obj_item_assembly assembly)
            return dot;
        Dm.append(dot, await assembly.examine(user));
        return dot;
    }

    // assembly handling

    // tg: code/game/objects/items/grenades/ghettobomb.dm:66
    public override object IsAssemblyHolder()
    {
        return TRUE;
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:69
    public override async DmTask<object> on_found(mob finder = null)
    {
        if (activator is obj_item_assembly assembly)
            await assembly.on_found(finder);
        return null;
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:73
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        if (activator is obj_item_assembly assembly)
            assembly.holder_movement();
        return dot;
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:78
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (activator is obj_item_assembly assembly)
            await assembly.dropped(user, silent);
        return dot;
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:83
    public virtual async DmTask process_activation(obj_item assembly = null)
    {
        await detonate();
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:86
    public virtual async DmTask attach_activator(obj_item_assembly new_one = null)
    {
        if (activator != null)
            return;
        activator = new_one;
        new_one.holder = this;
        await new_one.on_attach();
        await new_one.toggle_secure();
        await update_icon(UPDATE_ICON_STATE);
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:95
    public override object change_det_time(object time = null)
    {
        return null;
    }

    //assembly handling end

    // tg: code/game/objects/items/grenades/ghettobomb.dm:100
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item_assembly assembly = activator as obj_item_assembly;
        if (loc == user) //if we were picked up already, this opening whenever picked up is not ok
            await assembly.ui_interact(user); //if any
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (assembly == null)
            return dot;
        await assembly.attack_hand();
        return dot;
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:109
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (activator is not obj_item_assembly assembly)
        {
            icon_state = "slicedapart"; //this shouldnt happen but should prevent runtimes
            return await base.update_icon_state(__extra);
        }
        string suffix = "";
        obj_item_assembly_timer as_timer = assembly as obj_item_assembly_timer;
        obj_item_assembly_mousetrap as_mousetrap = assembly as obj_item_assembly_mousetrap;
        obj_item_assembly_prox_sensor as_prox = assembly as obj_item_assembly_prox_sensor;
        if ((as_timer != null && Dm.truthy(as_timer.timing)) || (as_mousetrap != null && Dm.truthy(as_mousetrap.armed))) //these shouldve just had a common "active" variable or something
            suffix = "-a";
        else if (as_prox != null)
            suffix = Dm.truthy(as_prox.timing) ? "-arming" : (Dm.truthy(as_prox.scanning) ? "-a" : "");
        icon_state = Dm.text("[]-[][]", base_icon_state, Dm.initial(assembly, (atom p) => p.name), suffix); //signalers detonate instantly so theyre not here
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:123
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        // COOLDOWN_FINISHED(src, spam_cd) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (activator is not obj_item_assembly assembly || !(spam_cd <= world.time))
        {
            balloon_alert(user, activator == null ? I18n.T("you shouldnt be seeing this") : I18n.T("on cooldown!"));
            return null;
        }
        if (assembly is obj_item_assembly_signaler)
            return null; //no signallers, signallers send a signal and i can imagine this having bad sideeffects if some has multiple of the same frequency in their backpack and uses them inhand by accident
        log_grenade(user);
        add_fingerprint(user);
        await assembly.activate();
        await update_icon(UPDATE_ICON_STATE);
        user.balloon_alert_to_viewers(I18n.T("arming!"));
        // COOLDOWN_START(src, spam_cd, 1 SECONDS) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        spam_cd = world.time + 1 * SECONDS;
        return null;
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:136
    public override async DmTask<object> detonate(mob_living lanced_by = null) //Blowing that can up
    {
        if (Dm.truthy(effects[typeof(obj_item_shard)])) //this has to be before so it initializes us a pellet cloud or something
            shrapnel_radius = Dm.num(effects[typeof(obj_item_shard)]);
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        await update_mob();
        for (float i = 1; i <= Dm.num(effects[typeof(obj_item_food_meat_slab)]); i++)
            Dm.@new<obj_effect_gibspawner_generic>(loc);
        if (Dm.truthy(effects[typeof(obj_item_paper)]))
        {
            foreach (turf_open floor in Dm.each<turf_open>(view(effects[typeof(obj_item_paper)], loc))) //this couldve been light impact range but fake pipebombs exploding into confetti is funny
                Dm.@new<obj_effect_decal_cleanable_confetti>(floor);
        }
        float heavy = Dm.num(floor(Dm.num(power) * 0.2f));
        float light = Dm.num(round(Dm.num(power) * 0.7f, 1));
        float flame = Dm.num(round(Dm.num(power) + rand(-1, 1), 1));
        await explosion(loc as atom, devastation_range: -1, heavy_impact_range: heavy, light_impact_range: light, flame_range: flame, explosion_cause: this);

        if (Dm.truthy(effects[typeof(obj_item_stack_ore_bluespace_crystal_refined)]))
        {
            foreach (mob_living victim in Dm.each<mob_living>(view(light, loc)))
                await do_teleport(victim, get_turf(victim), min(12, Dm.num(effects[typeof(obj_item_stack_ore_bluespace_crystal_refined)]) * 3), asoundin: Dm.resource("sound/effects/phasein.ogg"), channel: TELEPORT_CHANNEL_BLUESPACE);
        }

        qdel(this);
        return dot;
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:160
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        activator = null;
        return dot;
    }
}

// tg: code/game/objects/items/grenades/ghettobomb.dm:169  #define MAX_STUFFINGS 3
// tg: code/game/objects/items/grenades/ghettobomb.dm:172
public partial class obj_item_sliced_pipe : obj_item
{
    private const int MAX_STUFFINGS = 3;

    /// Typecache of items we are allowed to stuff into the pipebomb for effects, only add items with effects
    // tg: code/game/objects/items/grenades/ghettobomb.dm:181  var/static/list/allowed
    private static DmList allowed => TgState.Current.obj_item_sliced_pipe__allowed ??= typecacheof(Dm.list(
        typeof(obj_item_food_meat_slab),
        typeof(obj_item_paper),
        typeof(obj_item_shard),
        typeof(obj_item_stack_ore_bluespace_crystal_refined))) as DmList;
    //this probably shouldve been a blacklist instead but god do i not wanna update this anytime a new assembly is added
    /// A static list of types of assemblies that are allowed to be used to finish the bomb
    // tg: code/game/objects/items/grenades/ghettobomb.dm:189  var/static/list/allowed_activators
    private static DmList allowed_activators => TgState.Current.obj_item_sliced_pipe__allowed_activators ??= Dm.list(
        typeof(obj_item_assembly_signaler),
        typeof(obj_item_assembly_prox_sensor),
        typeof(obj_item_assembly_mousetrap),
        typeof(obj_item_assembly_mousetrap_armed),
        typeof(obj_item_assembly_timer),
        typeof(obj_item_assembly_wiremod),
        typeof(obj_item_assembly_voice));
    /// Static list of reagent to explosive power
    // tg: code/game/objects/items/grenades/ghettobomb.dm:199  var/static/list/fuel_power
    private static DmList fuel_power => TgState.Current.obj_item_sliced_pipe__fuel_power ??= Dm.list(
        Dm.kv(typeof(datum_reagent_fuel), 0.5f),
        Dm.kv(typeof(datum_reagent_toxin_plasma), 0.75f),
        Dm.kv(typeof(datum_reagent_gunpowder), 1),
        Dm.kv(typeof(datum_reagent_nitroglycerin), 2),
        Dm.kv(typeof(datum_reagent_tatp), 2.5f));

    // tg: code/game/objects/items/grenades/ghettobomb.dm:209
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(20, OPENCONTAINER);
        return dot;
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:213
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        if (!Dm.truthy(wires_are_in))
        {
            Dm.append(dot, span_notice(I18n.T("You could stuff something in, or fill it with fuel or some other volatile chemical..")));
            Dm.append(dot, span_notice(I18n.T("Afterwards, add some cable.")));
        }
        else
            Dm.append(dot, span_notice(I18n.T("The wires are just dangling from it, you need some sort of <i> activating assembly</i>.")));
        return dot;
    }

    // tg: code/game/objects/items/grenades/ghettobomb.dm:221
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(wires_are_in))
        {
            // here we can stuff in additional objects for a cooler effect
            if (Dm.truthy(is_type_in_typecache(tool, allowed)) && contents.len < MAX_STUFFINGS)
            {
                balloon_alert(user, I18n.T("stuffed in"));
                atom_movable to_put = tool;
                if (tool is obj_item_stack as_stack)
                {
                    obj_item_stack new_stack = await as_stack.split_stack(1) as obj_item_stack;
                    new_stack.merge_type = null; //prevent them from merging inside for contents.len
                    to_put = new_stack;
                }
                await to_put.forceMove(this);
                return ITEM_INTERACT_SUCCESS;
            }

            //if the item has reagents lets allow it to transfer
            if (tool.reagents != null)
                return NONE;
            datum_reagents holder = (datum_reagents)reagents;
            if (Dm.num(holder.total_volume) < 5)
            {
                balloon_alert(user, I18n.T("add more fuel!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (tool is not obj_item_stack_cable_coil coil)
                return ITEM_INTERACT_BLOCKING;
            if (Dm.num(coil.get_amount()) < 15)
            {
                balloon_alert(user, I18n.T("need 15 length!"));
                return ITEM_INTERACT_BLOCKING;
            }
            await coil.use(15);

            float cur_power = 0;
            foreach (datum_reagent reagent in Dm.each<datum_reagent>(holder.reagent_list))
            {
                if (!Dm.@in(reagent.type, fuel_power))
                    continue;
                cur_power += Dm.num(fuel_power[reagent.type]) * Dm.num(reagent.volume) / Dm.num(holder.maximum_volume);
            }

            power = Dm.num(power) * cur_power;
            power = Dm.num(power) - contents.len / 2f;

            balloon_alert(user, I18n.T("wires attached"));
            icon_state = Dm.text("[]-cable", icon_state);
            holder.flags = SEALED_CONTAINER;
            wires_are_in = TRUE;
            return ITEM_INTERACT_SUCCESS;
        }

        // wires are in, lets finish this up
        if (tool is not obj_item_assembly assembly || !Dm.@in(assembly.type, allowed_activators))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(assembly.secured))
        {
            balloon_alert(user, I18n.T("unsecure assembly first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(assembly, this)))
            return ITEM_INTERACT_BLOCKING;
        user.balloon_alert(user, I18n.T("attached"));

        obj_item_grenade_iedcasing pipebomb = Dm.@new<obj_item_grenade_iedcasing>(drop_location());
        foreach (atom_movable item_inside in Dm.each<atom_movable>(contents.Copy()))
            await item_inside.forceMove(pipebomb);

        pipebomb.power = power;
        await pipebomb.attach_activator(assembly);
        pipebomb.setup_effects_from_contents();
        bool was_in_hands = loc == user;
        qdel(this);
        if (was_in_hands)
            await user.put_in_hands(pipebomb);
        return ITEM_INTERACT_SUCCESS;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/grenades/ghettobomb.dm:181  var/static/list/allowed (/obj/item/sliced_pipe)
    internal DmList obj_item_sliced_pipe__allowed;
    // tg: code/game/objects/items/grenades/ghettobomb.dm:189  var/static/list/allowed_activators (/obj/item/sliced_pipe)
    internal DmList obj_item_sliced_pipe__allowed_activators;
    // tg: code/game/objects/items/grenades/ghettobomb.dm:199  var/static/list/fuel_power (/obj/item/sliced_pipe)
    internal DmList obj_item_sliced_pipe__fuel_power;
}
