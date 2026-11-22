// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/beekeeping/beebox.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file: the apiary, its bees, frames and honeycomb. GAP(lead): GLOB.chemical_reagents_list is not built
// (holder.cs): a premade random apiary's reagent is the reagent type's prototype, which stands in for the list's entry.

// tg: code/modules/hydroponics/beekeeping/beebox.dm:7
public partial class obj_structure_beebox : obj_structure
{
    // tg: code/modules/hydroponics/beekeeping/beebox.dm:1
    protected const float BEEBOX_MAX_FRAMES = 3; //Max frames per box
    // tg: code/modules/hydroponics/beekeeping/beebox.dm:2
    protected const float BEES_RATIO = 0.5f; //Multiplied by the max number of honeycombs to find the max number of bees
    // tg: code/modules/hydroponics/beekeeping/beebox.dm:3
    protected const float BEE_PROB_NEW_BEE = 20; //The chance for spare bee_resources to be turned into new bees
    // tg: code/modules/hydroponics/beekeeping/beebox.dm:4
    protected const float BEE_RESOURCE_HONEYCOMB_COST = 100; //The amount of bee_resources for a new honeycomb to be produced, percentage cost 1-100
    // tg: code/modules/hydroponics/beekeeping/beebox.dm:5
    protected const float BEE_RESOURCE_NEW_BEE_COST = 50; //The amount of bee_resources for a new bee to be produced, percentage cost 1-100

    // tg: code/modules/hydroponics/beekeeping/beebox.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/hydroponics/beekeeping/beebox.dm:26
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        queen_bee = null;
        QDEL_LIST(bees);
        QDEL_LIST(honey_frames);
        QDEL_LIST(honeycombs);
        return base.Destroy(force);
    }

    // tg: code/modules/hydroponics/beekeeping/beebox.dm:69
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (queen_bee != null)
        {
            if (bee_resources >= BEE_RESOURCE_HONEYCOMB_COST)
            {
                if (honeycombs.len < Dm.num(get_max_honeycomb()))
                {
                    bee_resources = Dm.max(bee_resources - BEE_RESOURCE_HONEYCOMB_COST, 0);
                    obj_item_food_honeycomb new_comb = Dm.@new<obj_item_food_honeycomb>(this);
                    if (queen_bee.beegent != null)
                        Waiting.NoSleep(new_comb.set_reagent(queen_bee.beegent.type));
                    honeycombs.Add(new_comb);
                }
            }

            if (bees.len < Dm.num(get_max_bees()))
            {
                bool freebee = false; //a freebee, geddit?, hahaha HAHAHAHA
                if (bees.len <= 1) //there's always one set of worker bees, this isn't colony collapse disorder its 2d spessmen
                    freebee = true;
                if ((bee_resources >= BEE_RESOURCE_NEW_BEE_COST && prob(BEE_PROB_NEW_BEE)) || freebee)
                {
                    if (!freebee)
                        bee_resources = Dm.max(bee_resources - BEE_RESOURCE_NEW_BEE_COST, 0);
                    mob_living_basic_bee new_bee = Dm.@new<mob_living_basic_bee>(get_turf(this));
                    new_bee.beehome = this;
                    Waiting.NoSleep(new_bee.assign_reagent(queen_bee.beegent));
                    bees.Add(new_bee);
                }
            }
        }
        return null;
    }

    // tg: code/modules/hydroponics/beekeeping/beebox.dm:92
    public virtual object get_max_honeycomb()
    {
        float dot = 0;
        foreach (obj_item_honey_frame frame in Dm.each_cast<obj_item_honey_frame>(honey_frames))
            dot += frame.honeycomb_capacity;
        return dot;
    }

    // tg: code/modules/hydroponics/beekeeping/beebox.dm:98
    public virtual object get_max_bees()
    {
        return Dm.num(get_max_honeycomb()) * BEES_RATIO;
    }

    // tg: code/modules/hydroponics/beekeeping/beebox.dm:102
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (queen_bee == null)
            Dm.append(ref dot, span_warning(I18n.T("There is no queen bee! There won't bee any honeycomb without a queen!")));

        float half_bee = Dm.num(get_max_bees()) * 0.5f;
        if (Dm.truthy(half_bee) && (bees.len >= half_bee))
            Dm.append(ref dot, span_notice(I18n.T("This place is aBUZZ with activity... there are lots of bees!")));

        Dm.append(ref dot, span_notice(I18n.T("[]/100 resource supply.", bee_resources)));
        Dm.append(ref dot, span_notice(I18n.T("[]% towards a new honeycomb.", bee_resources)));
        Dm.append(ref dot, span_notice(I18n.T("[]% towards a new bee.", bee_resources * 2)));

        if (Dm.truthy(honeycombs.len))
        {
            bool plural = honeycombs.len > 1;
            Dm.append(ref dot, span_notice(I18n.T("There [] [] uncollected honeycomb[] in the apiary.", plural ? "are" : "is", honeycombs.len, plural ? "s" : "")));
        }

        if (honeycombs.len >= Dm.num(get_max_honeycomb()))
            Dm.append(ref dot, span_warning(I18n.T("There's no room for more honeycomb!")));
        return dot;
    }

    // tg: code/modules/hydroponics/beekeeping/beebox.dm:123
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/hydroponics/beekeeping/beebox.dm:128
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_honey_frame frame)
        {
            if (honey_frames.len == BEEBOX_MAX_FRAMES)
            {
                to_chat(user, span_warning(I18n.T("There's no room for any more frames in the apiary!")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(frame, this)))
                return ITEM_INTERACT_BLOCKING;

            visible_message(span_notice(I18n.T("[] adds a frame to the apiary.", user)));
            honey_frames.Add(frame);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_queen_bee new_queen)
        {
            if (queen_bee != null)
            {
                to_chat(user, span_warning(I18n.T("This hive already has a queen!")));
                return ITEM_INTERACT_BLOCKING;
            }

            await user.temporarilyRemoveItemFromInventory(new_queen);

            bees.Add(new_queen.queen);
            queen_bee = new_queen.queen;

            await new_queen.queen.forceMove(this);

            if (queen_bee == null)
            {
                to_chat(user, span_warning(I18n.T("The queen bee disappeared! Disappearing bees have been in the news lately...")));
                return ITEM_INTERACT_BLOCKING;
            }

            visible_message(span_notice(I18n.T("[] sets [] down inside the apiary, making it their new home.", user, queen_bee)));
            float relocated = 0;
            foreach (mob_living_basic_bee relocating_bee in Dm.each_cast<mob_living_basic_bee>(bees))
            {
                if (Dm.truthy(relocating_bee.reagent_incompatible(queen_bee)))
                {
                    bees.Remove(relocating_bee);
                    relocating_bee.beehome = null;
                    if (relocating_bee.loc == this)
                        await relocating_bee.forceMove(drop_location() as atom);
                    relocated++;
                }
            }
            if (Dm.truthy(relocated))
                to_chat(user, span_warning(I18n.T("This queen has a different reagent to some of the bees who live here, those bees will not return to this apiary!")));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/hydroponics/beekeeping/beebox.dm:174
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (!Dm.truthy((user as mob_living).bee_friendly()))
        {
            //Time to get stung!
            bool bees_attack = false;
            foreach (mob_living_basic_bee worker in Dm.each_cast<mob_living_basic_bee>(bees)) //everyone who's ever lived here now instantly hates you, suck it assistant!
            {
                if (Dm.truthy(worker.is_queen))
                    continue;
                if (worker.loc == this)
                    await worker.forceMove(drop_location() as atom);
                bees_attack = true;
            }
            if (bees_attack)
                visible_message(span_danger(I18n.T("[] disturbs the bees!", user)));
            else
                visible_message(span_danger(I18n.T("[] disturbs \\the [] to no effect!", user, this)));
        }
        else
        {
            object option = await tgui_alert(user, "Which piece do you wish to remove?", "Apiary Adjustment", Dm.list("Honey Frame", "Queen Bee"));
            if (!Dm.truthy(option) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY)))
                return dot;
            switch (option as string)
            {
                case "Honey Frame":
                {
                    if (!Dm.truthy(honey_frames.len))
                    {
                        to_chat(user, span_warning(I18n.T("There are no honey frames to remove!")));
                        return dot;
                    }

                    obj_item_honey_frame frame = pick_n_take(honey_frames) as obj_item_honey_frame;
                    if (frame != null)
                    {
                        if (!Dm.truthy(await user.put_in_active_hand(frame)))
                            await frame.forceMove(drop_location() as atom);
                        visible_message(span_notice(I18n.T("[] removes a frame from the apiary.", user)));

                        float amtH = frame.honeycomb_capacity;
                        float fallen = 0;
                        while (Dm.truthy(honeycombs.len) && Dm.truthy(amtH)) //let's pretend you always grab the frame with the most honeycomb on it
                        {
                            obj_item_food_honeycomb comb = pick_n_take(honeycombs) as obj_item_food_honeycomb;
                            if (comb != null)
                            {
                                await comb.forceMove(drop_location() as atom);
                                amtH--;
                                fallen++;
                            }
                        }
                        if (Dm.truthy(fallen))
                        {
                            bool multiple = fallen > 1;
                            visible_message(span_notice(I18n.T("[] scrapes [] honeycomb[] off of the frame.", user, multiple ? Dm.text("[]", fallen) : "a", multiple ? "s" : "")));
                        }
                    }
                    break;
                }
                case "Queen Bee":
                {
                    if (queen_bee == null || queen_bee.loc != this)
                    {
                        to_chat(user, span_warning(I18n.T("There is no queen bee to remove!")));
                        return dot;
                    }
                    obj_item_queen_bee queen = Dm.@new<obj_item_queen_bee>();
                    await queen_bee.forceMove(queen);
                    bees.Remove(queen_bee);
                    queen.queen = queen_bee;
                    queen.name = queen_bee.name;
                    if (!Dm.truthy(await user.put_in_active_hand(queen)))
                        await queen.forceMove(drop_location() as atom);
                    visible_message(span_notice(I18n.T("[] removes the queen from the apiary.", user)));
                    queen_bee = null;
                    break;
                }
            }
        }
        return dot;
    }

    // tg: code/modules/hydroponics/beekeeping/beebox.dm:231
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_mineral_wood>(loc, 20);
        foreach (mob_living_basic_bee worker in Dm.each_cast<mob_living_basic_bee>(bees.Copy()))
        {
            if (worker.loc == this)
                await worker.forceMove(get_turf(this));
            bees.Remove(worker);
            worker.beehome = null;
        }
        foreach (obj_item_honey_frame frame in Dm.each_cast<obj_item_honey_frame>(honey_frames.Copy()))
        {
            if (frame.loc == this)
                await frame.forceMove(get_turf(this));
            honey_frames.Remove(frame);
        }
    }

    // tg: code/modules/hydroponics/beekeeping/beebox.dm:246
    public virtual object habitable(mob_living_basic target = null)
    {
        if (target is not mob_living_basic_bee citizen)
            return FALSE;
        if (Dm.truthy(citizen.reagent_incompatible(queen_bee)) || bees.len >= Dm.num(get_max_bees()))
            return FALSE;
        return TRUE;
    }
}

//Premade apiaries can spawn with a random reagent
// tg: code/modules/hydroponics/beekeeping/beebox.dm:35
public partial class obj_structure_beebox_premade : obj_structure_beebox
{
    // tg: code/modules/hydroponics/beekeeping/beebox.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        icon_state = "beebox";
        datum_reagent custom_reagent = null;
        if (Dm.truthy(random_reagent))
        {
            // GLOB.chemical_reagents_list[custom_reagent]: the reagent type's prototype stands in for the entry (the header's GAP)
            Type custom_type = get_random_reagent_id() as Type;
            custom_reagent = custom_type == null ? null : Dm.initial(custom_type, (datum_reagent p) => p);
        }

        queen_bee = Dm.@new<mob_living_basic_bee_queen>(this);
        queen_bee.beehome = this;
        bees.Add(queen_bee);
        Waiting.NoSleep(queen_bee.assign_reagent(custom_reagent));

        for (float i = 1; i <= BEEBOX_MAX_FRAMES; i++)
        {
            obj_item_honey_frame new_frame = Dm.@new<obj_item_honey_frame>(this);
            honey_frames.Add(new_frame);
        }

        float max_bees = Dm.num(get_max_bees());
        for (float i = 1; i <= max_bees; i++)
        {
            mob_living_basic_bee new_bee = Dm.@new<mob_living_basic_bee>(this);
            bees.Add(new_bee);
            new_bee.beehome = this;
            Waiting.NoSleep(new_bee.assign_reagent(custom_reagent));
        }
        return dot;
    }
}
