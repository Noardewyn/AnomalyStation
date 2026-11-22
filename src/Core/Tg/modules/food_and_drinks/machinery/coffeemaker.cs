// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/machinery/coffeemaker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the coffeemaker, the impressa coffeemaker, the coffee cartridges and the cartridge rack.
// The var blocks are generated (D111); the type-level static radial pictures are TgState fields (rules 10.6).

// tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:3
public partial class obj_machinery_coffeemaker : obj_machinery
{
    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:1  #define BEAN_CAPACITY 10 (file-local, #undef at :705)
    //amount of coffee beans that can fit inside the impressa coffeemaker
    internal const float BEAN_CAPACITY = 10;

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:34-41  the type's var/static radial pictures (rules 10.6)
    internal static object radial_examine => TgState.Current.obj_machinery_coffeemaker__radial_examine ??= image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_examine");
    internal static object radial_brew => TgState.Current.obj_machinery_coffeemaker__radial_brew ??= image(icon: Dm.resource("icons/hud/radial_coffee.dmi"), icon_state: "radial_brew");
    internal static object radial_eject_pot => TgState.Current.obj_machinery_coffeemaker__radial_eject_pot ??= image(icon: Dm.resource("icons/hud/radial_coffee.dmi"), icon_state: "radial_eject_pot");
    internal static object radial_eject_cartridge => TgState.Current.obj_machinery_coffeemaker__radial_eject_cartridge ??= image(icon: Dm.resource("icons/hud/radial_coffee.dmi"), icon_state: "radial_eject_cartridge");
    internal static object radial_take_cup => TgState.Current.obj_machinery_coffeemaker__radial_take_cup ??= image(icon: Dm.resource("icons/hud/radial_coffee.dmi"), icon_state: "radial_take_cup");
    internal static object radial_take_sugar => TgState.Current.obj_machinery_coffeemaker__radial_take_sugar ??= image(icon: Dm.resource("icons/hud/radial_coffee.dmi"), icon_state: "radial_take_sugar");
    internal static object radial_take_sweetener => TgState.Current.obj_machinery_coffeemaker__radial_take_sweetener ??= image(icon: Dm.resource("icons/hud/radial_coffee.dmi"), icon_state: "radial_take_sweetener");
    internal static object radial_take_creamer => TgState.Current.obj_machinery_coffeemaker__radial_take_creamer ??= image(icon: Dm.resource("icons/hud/radial_coffee.dmi"), icon_state: "radial_take_creamer");

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
        {
            coffeepot = Dm.@new<obj_item_reagent_containers_cup_coffeepot>(this);
            cartridge = Dm.@new<obj_item_coffee_cartridge>(this);
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:51
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (coffeepot != null)
            await coffeepot.forceMove(drop_location() as atom);
        if (cartridge != null)
            await cartridge.forceMove(drop_location() as atom);
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:55
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref coffeepot);
        QDEL_NULL(ref cartridge);
        remove_shared_particles(typeof(particles_smoke));
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:61
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == coffeepot)
        {
            coffeepot = null;
            update_appearance(UPDATE_OVERLAYS);
        }
        if (gone == cartridge)
        {
            cartridge = null;
            update_appearance(UPDATE_OVERLAYS);
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:70
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        speed = 0;
        foreach (datum_stock_part_micro_laser laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            speed += Dm.num(laser.tier);
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:76
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!in_range(user, this) && !issilicon(user) && !isobserver(user))
        {
            Dm.append(ref dot, span_warning(I18n.T("You're too far away to examine []'s contents and display!", this)));
            return dot;
        }

        if (Dm.truthy(brewing))
        {
            Dm.append(ref dot, span_warning(I18n.T("\\The [] is brewing.", this)));
            return dot;
        }

        if (Dm.truthy(panel_open))
        {
            Dm.append(ref dot, span_notice(I18n.T("[]'s maintenance hatch is open!", this)));
            return dot;
        }

        if (coffeepot != null || cartridge != null)
        {
            Dm.append(ref dot, span_notice(I18n.T("\\The [] contains:", this)));
            if (coffeepot != null)
                Dm.append(ref dot, span_notice(I18n.T("- \\A [].", coffeepot)));
            if (cartridge != null)
                Dm.append(ref dot, span_notice(I18n.T("- \\A [].", cartridge)));
            return dot;
        }

        if (!Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
        {
            Dm.append(ref dot, Dm.text("[]\n", span_notice(I18n.T("The status display reads:"))) +
                span_notice(I18n.T("- Brewing coffee at <b>[]%</b>.", speed * 100)));
            if (coffeepot != null)
            {
                foreach (datum_reagent_consumable cawfee in Dm.each_cast<datum_reagent_consumable>(((datum_reagents)coffeepot.reagents).reagent_list))
                    Dm.append(ref dot, span_notice(I18n.T("- [] units of coffee in pot.", cawfee.volume)));
            }
            if (cartridge != null)
            {
                if (cartridge.charges < 1)
                    Dm.append(ref dot, span_notice(I18n.T("- grounds cartridge is empty.")));
                else
                    Dm.append(ref dot, span_notice(I18n.T("- grounds cartridge has [] charges remaining.", cartridge.charges)));
            }
        }

        // tg renders `x != 1 && "s"` in place: a single cup or packet reads "cup0" ("[FALSE]" is "0"), kept as tg has it
        if (coffee_cups >= 1)
            Dm.append(ref dot, span_notice(I18n.T("There [] [] coffee cup[] left.", coffee_cups == 1 ? I18n.T("is") : I18n.T("are"), coffee_cups, Dm.num(coffee_cups != 1) is var and__1 && !Dm.truthy(and__1) ? (object)and__1 : "s")));
        else
            Dm.append(ref dot, span_notice(I18n.T("There are no cups left.")));

        if (sugar_packs >= 1)
            Dm.append(ref dot, span_notice(I18n.T("There [] [] packet[] of sugar left.", sugar_packs == 1 ? I18n.T("is") : I18n.T("are"), sugar_packs, Dm.num(sugar_packs != 1) is var and__2 && !Dm.truthy(and__2) ? (object)and__2 : "s")));
        else
            Dm.append(ref dot, span_notice(I18n.T("There is no sugar left.")));

        if (sweetener_packs >= 1)
            Dm.append(ref dot, span_notice(I18n.T("There [] [] packet[] of sweetener left.", sweetener_packs == 1 ? I18n.T("is") : I18n.T("are"), sweetener_packs, Dm.num(sweetener_packs != 1) is var and__3 && !Dm.truthy(and__3) ? (object)and__3 : "s")));
        else
            Dm.append(ref dot, span_notice(I18n.T("There is no sweetener left.")));

        if (creamer_packs > 1)
            Dm.append(ref dot, span_notice(I18n.T("There [] [] packet[] of creamer left.", creamer_packs == 1 ? I18n.T("is") : I18n.T("are"), creamer_packs, Dm.num(creamer_packs != 1) is var and__4 && !Dm.truthy(and__4) ? (object)and__4 : "s")));
        else
            Dm.append(ref dot, span_notice(I18n.T("There is no creamer left.")));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:130
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        if (!Dm.truthy(await can_interact(user)) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH | SILENT_ADJACENCY)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        if (Dm.truthy(brewing))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        await replace_pot(user as mob_living);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:141
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:144
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:147
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, overlay_checks());
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:151
    public virtual object overlay_checks()
    {
        object dot = Dm.list();
        if (coffeepot != null)
        {
            if (coffeepot is obj_item_reagent_containers_cup_coffeepot_bluespace)
                Dm.append(ref dot, "coffeemaker_pot_bluespace");
            else
                Dm.append(ref dot, Dm.text("coffeemaker_pot_[]", Dm.truthy(((datum_reagents)coffeepot.reagents).total_volume) ? "full" : "empty"));
        }
        if (cartridge != null)
            Dm.append(ref dot, "coffeemaker_cartidge");
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:162
    public virtual async DmTask<object> replace_pot(mob_living user = null, obj_item_reagent_containers_cup_coffeepot new_coffeepot = null)
    {
        if (user == null)
            return FALSE;

        // If we're trying to eject/remove the current pot
        if (new_coffeepot == null)
        {
            if (coffeepot == null)
            {
                balloon_alert(user, I18n.T("no coffeepot to remove!"));
                return FALSE;
            }
            await try_put_in_hand(coffeepot, user);
            balloon_alert(user, I18n.T("coffeepot returned"));
            coffeepot = null;
        }
        else
        {
            // If we're replacing with a new pot
            if (coffeepot != null)
                await try_put_in_hand(coffeepot, user);
            coffeepot = new_coffeepot;
            balloon_alert(user, I18n.T("coffeepot inserted"));
        }

        update_appearance(UPDATE_OVERLAYS);
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:184
    public virtual async DmTask<object> replace_cartridge(mob_living user = null, obj_item_coffee_cartridge new_cartridge = null)
    {
        if (user == null)
            return FALSE;
        if (cartridge != null)
            await try_put_in_hand(cartridge, user);
        if (new_cartridge != null)
            cartridge = new_cartridge;
        update_appearance(UPDATE_OVERLAYS);
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:194
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:199
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return coffeepot == null ? default_deconstruction_screwdriver(user, tool) : NONE;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:202
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:205
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = null;
        if (Dm.truthy(panel_open)) //Can't insert objects when its screwed open
            return NONE;

        if (tool is obj_item_reagent_containers_cup_coffeepot && !Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)) && Dm.truthy(tool.is_open_container()))
        {
            obj_item_reagent_containers_cup_coffeepot new_pot = (obj_item_reagent_containers_cup_coffeepot)tool;
            dot = ITEM_INTERACT_SUCCESS; //no afterattack
            if (!Dm.truthy(await user.transferItemToLoc(new_pot, this)))
                return ITEM_INTERACT_BLOCKING;
            await replace_pot(user, new_pot);
            update_appearance(UPDATE_OVERLAYS);
            return ITEM_INTERACT_SUCCESS; //no afterattack
        }

        if (istype(tool, cup_type) && !Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)) && Dm.truthy(tool.is_open_container()))
        {
            // tg reads the reagents through a coffee_cup typed var (new_cup) whatever cup_type is; the impressa's cups are another type
            if (((datum_reagents)tool.reagents).total_volume > 0)
            {
                balloon_alert(user, I18n.T("the cup must be empty!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (coffee_cups >= max_coffee_cups)
            {
                balloon_alert(user, I18n.T("the cup holder is full!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            coffee_cups++;
            update_appearance(UPDATE_OVERLAYS);
            return ITEM_INTERACT_SUCCESS; //no afterattack
        }

        if (tool is obj_item_reagent_containers_condiment_pack_sugar)
        {
            obj_item_reagent_containers_condiment_pack_sugar new_pack = (obj_item_reagent_containers_condiment_pack_sugar)tool;
            if (((datum_reagents)new_pack.reagents).total_volume < ((datum_reagents)new_pack.reagents).maximum_volume)
            {
                balloon_alert(user, I18n.T("the pack must be full!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (sugar_packs >= max_sugar_packs)
            {
                balloon_alert(user, I18n.T("the sugar compartment is full!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            sugar_packs++;
            update_appearance(UPDATE_OVERLAYS);
            return ITEM_INTERACT_SUCCESS; //no afterattack
        }

        if (tool is obj_item_reagent_containers_condiment_creamer)
        {
            obj_item_reagent_containers_condiment_creamer new_pack = (obj_item_reagent_containers_condiment_creamer)tool;
            if (((datum_reagents)new_pack.reagents).total_volume < ((datum_reagents)new_pack.reagents).maximum_volume)
            {
                balloon_alert(user, I18n.T("the pack must be full!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (creamer_packs >= max_creamer_packs)
            {
                balloon_alert(user, I18n.T("the creamer compartment is full!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            creamer_packs++;
            update_appearance(UPDATE_OVERLAYS);
            return ITEM_INTERACT_SUCCESS; //no afterattack
        }

        if (tool is obj_item_reagent_containers_condiment_pack_astrotame)
        {
            obj_item_reagent_containers_condiment_pack_astrotame new_pack = (obj_item_reagent_containers_condiment_pack_astrotame)tool;
            if (((datum_reagents)new_pack.reagents).total_volume < ((datum_reagents)new_pack.reagents).maximum_volume)
            {
                balloon_alert(user, I18n.T("the pack must be full!"));
                return ITEM_INTERACT_BLOCKING;
            }
            else if (sweetener_packs >= max_sweetener_packs)
            {
                balloon_alert(user, I18n.T("the sweetener compartment is full!"));
                return ITEM_INTERACT_BLOCKING;
            }
            else if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            sweetener_packs++;
            update_appearance(UPDATE_OVERLAYS);
            return ITEM_INTERACT_SUCCESS; //no afterattack
        }

        if (initial_cartridge != null && tool is obj_item_coffee_cartridge && !Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)))
        {
            obj_item_coffee_cartridge new_cartridge = (obj_item_coffee_cartridge)tool;
            if (!Dm.truthy(await user.transferItemToLoc(new_cartridge, this)))
                return ITEM_INTERACT_BLOCKING;
            await replace_cartridge(user, new_cartridge);
            balloon_alert(user, I18n.T("added cartridge"));
            update_appearance(UPDATE_OVERLAYS);
            return ITEM_INTERACT_SUCCESS; //no afterattack
        }

        return NONE; // Allow normal attack processing if no special interaction occurred
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:285
    public virtual object try_brew()
    {
        if (cartridge == null)
        {
            balloon_alert(usr, I18n.T("no coffee cartidge inserted!"));
            return FALSE;
        }
        if (cartridge.charges < 1)
        {
            balloon_alert(usr, I18n.T("coffee cartidge empty!"));
            return FALSE;
        }
        if (coffeepot == null)
        {
            balloon_alert(usr, I18n.T("no coffeepot inside!"));
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
        {
            balloon_alert(usr, I18n.T("machine unpowered!"));
            return FALSE;
        }
        if (((datum_reagents)coffeepot.reagents).total_volume >= ((datum_reagents)coffeepot.reagents).maximum_volume)
        {
            balloon_alert(usr, I18n.T("the coffeepot is already full!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:303
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra) // The microwave Menu //I am reasonably certain that this is not a microwave //I am positively certain that this is not a microwave
    {
        object dot = await base.ui_interact(user, ui, __extra);

        if (Dm.truthy(brewing) || !Dm.truthy(await user.can_perform_action(this, SILENT_ADJACENCY)))
            return dot;

        DmList options = Dm.list();

        if (coffeepot != null)
            options["Eject Pot"] = radial_eject_pot;

        if (cartridge != null)
            options["Eject Cartridge"] = radial_eject_cartridge;

        options["Brew"] = radial_brew; //brew is always available as an option, when the machine is unable to brew the player is told by balloon alerts whats exactly wrong

        if (coffee_cups > 0)
            options["Take Cup"] = radial_take_cup;

        if (sugar_packs > 0)
            options["Take Sugar"] = radial_take_sugar;

        if (sweetener_packs > 0)
            options["Take Sweetener"] = radial_take_sweetener;

        if (creamer_packs > 0)
            options["Take Creamer"] = radial_take_creamer;

        if (isAI(user))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
                return dot;
            options["Examine"] = radial_examine;
        }

        object choice;

        if (Dm.length(options) < 1)
            return dot;
        if (Dm.length(options) == 1)
            choice = options[1];
        else
            choice = await show_radial_menu(user, this, options, require_near: !HAS_SILICON_ACCESS(user) ? TRUE : FALSE);

        // post choice verification
        if (Dm.truthy(brewing) || (isAI(user) && Dm.truthy(Dm.bitand(machine_stat, NOPOWER))) || !Dm.truthy(await user.can_perform_action(this, SILENT_ADJACENCY)))
            return dot;

        object sw__1 = choice;
        if (Dm.eq(sw__1, "Brew"))
            await brew();
        else if (Dm.eq(sw__1, "Eject Pot"))
            await eject_pot(user);
        else if (Dm.eq(sw__1, "Eject Cartridge"))
            await eject_cartridge(user);
        else if (Dm.eq(sw__1, "Examine"))
            user.examinate(this);
        else if (Dm.eq(sw__1, "Take Cup"))
            await take_cup(user);
        else if (Dm.eq(sw__1, "Take Sugar"))
            await take_sugar(user);
        else if (Dm.eq(sw__1, "Take Sweetener"))
            await take_sweetener(user);
        else if (Dm.eq(sw__1, "Take Creamer"))
            await take_creamer(user);
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:367
    public virtual async DmTask eject_pot(mob user = null)
    {
        if (coffeepot != null)
            await replace_pot(user as mob_living);
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:371
    public virtual async DmTask eject_cartridge(mob user = null)
    {
        if (cartridge != null)
            await replace_cartridge(user as mob_living);
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:375
    public virtual async DmTask take_cup(mob user = null)
    {
        if (!Dm.truthy(coffee_cups)) //shouldn't happen, but we all know how stuff manages to break
        {
            balloon_alert(user, I18n.T("no cups left!"));
            return;
        }
        obj_item_reagent_containers_cup_glass_coffee_cup new_cup = Dm.@new<obj_item_reagent_containers_cup_glass_coffee_cup>(get_turf(this));
        await user.put_in_hands(new_cup);
        coffee_cups--;
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:384
    public virtual async DmTask take_sugar(mob user = null)
    {
        if (!Dm.truthy(sugar_packs))
        {
            balloon_alert(user, I18n.T("no sugar left!"));
            return;
        }
        obj_item_reagent_containers_condiment_pack_sugar new_pack = Dm.@new<obj_item_reagent_containers_condiment_pack_sugar>(get_turf(this));
        await user.put_in_hands(new_pack);
        sugar_packs--;
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:393
    public virtual async DmTask take_sweetener(mob user = null)
    {
        if (!Dm.truthy(sweetener_packs))
        {
            balloon_alert(user, I18n.T("no sweetener left!"));
            return;
        }
        obj_item_reagent_containers_condiment_pack_astrotame new_pack = Dm.@new<obj_item_reagent_containers_condiment_pack_astrotame>(get_turf(this));
        await user.put_in_hands(new_pack);
        sweetener_packs--;
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:402
    public virtual async DmTask take_creamer(mob user = null)
    {
        if (!Dm.truthy(creamer_packs))
        {
            balloon_alert(user, I18n.T("no creamer left!"));
            return;
        }
        obj_item_reagent_containers_condiment_creamer new_pack = Dm.@new<obj_item_reagent_containers_condiment_creamer>(drop_location());
        await user.put_in_hands(new_pack);
        creamer_packs--;
        update_appearance(UPDATE_OVERLAYS);
    }

    ///Updates the smoke state to something else, setting particles if relevant
    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:412
    public virtual void toggle_steam()
    {
        if (!Dm.truthy(brewing))
        {
            remove_shared_particles("smoke_coffeemaker");
            return;
        }

        obj_effect_abstract_shared_particle_holder smoke_particles = add_shared_particles(typeof(particles_smoke_steam_mild), "smoke_coffeemaker") as obj_effect_abstract_shared_particle_holder;
        // smoke_particles.particles.position = list(-6, 0, 0): BYOND's /particles vars are not declared (particles.builtin.cs) and
        // particles are not drawn
        Dm.Deferred("code/modules/food_and_drinks/machinery/coffeemaker.dm:418", "toggle_steam: the steam particles' position over the pot (BYOND's /particles vars are not declared; particles are not drawn)");
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:420
    public virtual async DmTask operate_for(object time = null, float? silent = null)
    {
        silent ??= FALSE;
        brewing = TRUE;
        if (!Dm.truthy(silent))
            playsound(this, Dm.resource("sound/machines/coffeemaker_brew.ogg"), 20, vary: TRUE);
        toggle_steam();
        await use_energy(Dm.div(Dm.mul(active_power_usage, time), 1 * SECONDS)); // .1 needed here to convert time (in deciseconds) to seconds such that watts * seconds = joules
        _addtimer(Dm.@new<datum_callback>(this, nameof(stop_operating)), Dm.num(Dm.div(time, speed)), file: "code/modules/food_and_drinks/machinery/coffeemaker.dm", line: 426);
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:428
    public virtual void stop_operating()
    {
        brewing = FALSE;
        toggle_steam();
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:432
    public virtual async DmTask brew()
    {
        power_change();
        if (!Dm.truthy(try_brew()))
            return;
        await operate_for(brew_time);
        await ((datum_reagents)coffeepot.reagents).add_reagent_list(cartridge.drink_type);
        cartridge.charges--;
        update_appearance(UPDATE_OVERLAYS);
    }
}

//Coffee Cartridges: like toner, but for your coffee!
// tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:442
public partial class obj_item_coffee_cartridge : obj_item
{
    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:451
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(charges))
            Dm.append(ref dot, span_warning(I18n.T("The cartridge has [] portions of grounds remaining.", charges)));
        else
            Dm.append(ref dot, span_warning(I18n.T("The cartridge has no unspent grounds remaining.")));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:458
public partial class obj_item_coffee_cartridge_fancy : obj_item_coffee_cartridge
{
    //Here's the joke before I get 50 issue reports: they're all the same, and that's intentional
    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:464
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object coffee_type = pick("blend", "blue_mountain", "kilimanjaro", "mocha");
        object sw__1 = coffee_type;
        if (Dm.eq(sw__1, "blend"))
        {
            name = I18n.T("coffeemaker cartridge - Miscela di Piccione");
            icon_state = "cartridge_blend";
        }
        else if (Dm.eq(sw__1, "blue_mountain"))
        {
            name = I18n.T("coffeemaker cartridge - Montagna Blu");
            icon_state = "cartridge_blue_mtn";
        }
        else if (Dm.eq(sw__1, "kilimanjaro"))
        {
            name = I18n.T("coffeemaker cartridge - Kilimangiaro");
            icon_state = "cartridge_kilimanjaro";
        }
        else if (Dm.eq(sw__1, "mocha"))
        {
            name = I18n.T("coffeemaker cartridge - Moka Arabica");
            icon_state = "cartridge_mocha";
        }
        return dot;
    }
}

/*
 * impressa coffee maker
 * its supposed to be a premium line product, so its cargo-only, the board cant be therefore researched
 */
// tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:518
public partial class obj_machinery_coffeemaker_impressa : obj_machinery_coffeemaker
{
    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:534
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
        {
            coffeepot = Dm.@new<obj_item_reagent_containers_cup_coffeepot>(this);
            cartridge = null;
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:540
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref coffeepot);
        QDEL_LIST(coffee);
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:545
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(coffee))
            Dm.append(ref dot, span_notice(I18n.T("The internal grinder contains [] scoop\\s of coffee beans", Dm.length(coffee))));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:550
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, overlay_checks());
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:554
    public override object overlay_checks()
    {
        object dot = Dm.list();
        if (coffeepot != null)
        {
            if (coffeepot is obj_item_reagent_containers_cup_coffeepot_bluespace)
                Dm.append(ref dot, "pot_bluespace");
            else
                Dm.append(ref dot, Dm.text("pot_[]", Dm.truthy(((datum_reagents)coffeepot.reagents).total_volume) ? "full" : "empty"));
        }
        if (coffee_cups > 0)
        {
            if (coffee_cups >= max_coffee_cups / 3)
            {
                if (coffee_cups > max_coffee_cups / 1.5f)
                    Dm.append(ref dot, "cups_3");
                else
                    Dm.append(ref dot, "cups_2");
            }
            else
                Dm.append(ref dot, "cups_1");
        }
        if (Dm.truthy(sugar_packs))
            Dm.append(ref dot, "extras_1");
        if (Dm.truthy(creamer_packs))
            Dm.append(ref dot, "extras_2");
        if (Dm.truthy(sweetener_packs))
            Dm.append(ref dot, "extras_3");
        if (Dm.truthy(coffee_amount))
        {
            if (coffee_amount < 0.7f * BEAN_CAPACITY)
                Dm.append(ref dot, "grinder_half");
            else
                Dm.append(ref dot, "grinder_full");
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:582
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (Dm.@in(gone, coffee))
        {
            Dm.remove(ref coffee, gone);
            update_appearance(UPDATE_OVERLAYS);
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:588
    public override object try_brew()
    {
        if (coffee_amount <= 0)
        {
            balloon_alert_to_viewers(I18n.T("no coffee beans added!"));
            return FALSE;
        }
        if (coffeepot == null)
        {
            balloon_alert_to_viewers(I18n.T("no coffeepot inside!"));
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
        {
            balloon_alert_to_viewers(I18n.T("machine unpowered!"));
            return FALSE;
        }
        if (((datum_reagents)coffeepot.reagents).total_volume >= ((datum_reagents)coffeepot.reagents).maximum_volume)
        {
            balloon_alert_to_viewers(I18n.T("the coffeepot is already full!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:603
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(panel_open))
            return NONE;

        if (tool is obj_item_food_grown_coffee && !Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)))
        {
            if (coffee_amount >= BEAN_CAPACITY)
            {
                balloon_alert(user, I18n.T("the coffee container is full!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!HAS_TRAIT(tool, TRAIT_DRIED))
            {
                balloon_alert(user, I18n.T("coffee beans must be dry!"));
                return ITEM_INTERACT_BLOCKING;
            }
            obj_item_food_grown_coffee new_coffee = (obj_item_food_grown_coffee)tool;
            if (!Dm.truthy(await user.transferItemToLoc(new_coffee, this)))
                return ITEM_INTERACT_BLOCKING;
            Dm.append(ref coffee, new_coffee);
            coffee_amount++;
            balloon_alert(user, I18n.T("added coffee"));
            update_appearance(UPDATE_OVERLAYS);
            return ITEM_INTERACT_SUCCESS; //no afterattack
        }

        if (tool is obj_item_storage_box_coffeepack && !Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)))
        {
            if (coffee_amount >= BEAN_CAPACITY)
            {
                balloon_alert(user, I18n.T("the coffee container is full!"));
                return ITEM_INTERACT_BLOCKING;
            }
            obj_item_storage_box_coffeepack new_coffee_pack = (obj_item_storage_box_coffeepack)tool;
            float added_any = FALSE;
            float had_nondried = FALSE;
            foreach (obj_item_food_grown_coffee new_coffee in Dm.each<obj_item_food_grown_coffee>(new_coffee_pack.contents))
            {
                if (!HAS_TRAIT(new_coffee, TRAIT_DRIED)) //the coffee beans inside must be dry
                {
                    had_nondried = TRUE;
                    return ITEM_INTERACT_BLOCKING;
                }
                if (coffee_amount >= BEAN_CAPACITY)
                    break;
                if (!Dm.truthy(await user.transferItemToLoc(new_coffee, this)))
                    break;
                Dm.append(ref coffee, new_coffee);
                coffee_amount++;
                await new_coffee.forceMove(this);
                added_any = TRUE;
            }

            if (Dm.truthy(added_any))
            {
                balloon_alert(user, I18n.T("added coffee"));
                update_appearance(UPDATE_OVERLAYS);
            }
            else if (Dm.truthy(had_nondried))
                balloon_alert(user, I18n.T("non-dried beans inside of coffee pack!"));
            else
                balloon_alert(user, I18n.T("no beans added!"));
            return ITEM_INTERACT_SUCCESS; //no afterattack
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:654
    public override async DmTask take_cup(mob user = null)
    {
        if (!Dm.truthy(coffee_cups)) //shouldn't happen, but we all know how stuff manages to break
        {
            balloon_alert(user, I18n.T("no cups left!"));
            return;
        }
        balloon_alert_to_viewers(I18n.T("took cup"));
        obj_item_reagent_containers_cup_glass_coffee_no_lid new_cup = Dm.@new<obj_item_reagent_containers_cup_glass_coffee_no_lid>(get_turf(this));
        await user.put_in_hands(new_cup);
        coffee_cups--;
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:664
    public override void toggle_steam()
    {
        if (!Dm.truthy(brewing))
        {
            remove_shared_particles("smoke_impressa");
            return;
        }

        obj_effect_abstract_shared_particle_holder smoke_particles = add_shared_particles(typeof(particles_smoke_steam_mild), "smoke_impressa") as obj_effect_abstract_shared_particle_holder;
        // smoke_particles.particles.position = list(-2, 1, 0): BYOND's /particles vars are not declared (particles.builtin.cs) and
        // particles are not drawn
        Dm.Deferred("code/modules/food_and_drinks/machinery/coffeemaker.dm:670", "toggle_steam: the steam particles' position over the impressa's pot (BYOND's /particles vars are not declared; particles are not drawn)");
    }

    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:672
    public override async DmTask brew()
    {
        power_change();
        if (!Dm.truthy(try_brew()))
            return;
        await operate_for(brew_time);

        // create a reference bean reagent list
        DmList reference_bean_reagents = Dm.list();
        obj_item_food_grown_coffee reference_bean = Dm.@new<obj_item_food_grown_coffee>(this);
        foreach (datum_reagent ref_bean_reagent in Dm.each_cast<datum_reagent>(((datum_reagents)reference_bean.reagents).reagent_list))
            Dm.append(ref reference_bean_reagents, ref_bean_reagent.name);

        // add all the reagents from the coffee beans to the coffeepot (ommit the ones from the reference bean)
        DmList reagent_delta = Dm.list();
        obj_item_food_grown_coffee bean = coffee[coffee_amount] as obj_item_food_grown_coffee;
        foreach (datum_reagent substance in Dm.each_cast<datum_reagent>(((datum_reagents)bean.reagents).reagent_list))
        {
            if (!Dm.truthy(reference_bean_reagents.Find(substance.name))) // we only add the reagent if it's a non-standard for coffee beans
                Dm.append(ref reagent_delta, Dm.list(Dm.kv(substance.type, substance.volume)));
        }
        await ((datum_reagents)coffeepot.reagents).add_reagent_list(reagent_delta);

        qdel(reference_bean);

        // remove the coffee beans from the machine
        coffee.Cut(1, 2);
        coffee_amount--;

        // fill the rest of the pot with coffee
        if (((datum_reagents)coffeepot.reagents).total_volume < 120)
        {
            float extra_coffee_amount = 120 - ((datum_reagents)coffeepot.reagents).total_volume;
            await ((datum_reagents)coffeepot.reagents).add_reagent(typeof(datum_reagent_consumable_coffee), extra_coffee_amount);
        }

        update_appearance(UPDATE_OVERLAYS);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/food_and_drinks/machinery/coffeemaker.dm:34-41  /obj/machinery/coffeemaker's var/static radial pictures
    internal object obj_machinery_coffeemaker__radial_examine;
    internal object obj_machinery_coffeemaker__radial_brew;
    internal object obj_machinery_coffeemaker__radial_eject_pot;
    internal object obj_machinery_coffeemaker__radial_eject_cartridge;
    internal object obj_machinery_coffeemaker__radial_take_cup;
    internal object obj_machinery_coffeemaker__radial_take_sugar;
    internal object obj_machinery_coffeemaker__radial_take_sweetener;
    internal object obj_machinery_coffeemaker__radial_take_creamer;
}
