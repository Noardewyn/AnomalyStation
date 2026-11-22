// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/rag.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the damp rag, whole (the bartender's, the janitor's and the service cyborg's). The var block is generated (D111).

// tg: code/modules/reagents/reagent_containers/rag.dm:1
public partial class obj_item_rag : obj_item
{
    private datum_reagents holder => reagents as datum_reagents;

    // tg: code/modules/reagents/reagent_containers/rag.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(5, OPENCONTAINER);
        Waiting.NoSleep(AddComponent(typeof(datum_component_cleaner), 3 * SECONDS,
            Dm.named("pre_clean_callback", Dm.@new<datum_callback>(this, nameof(should_clean))),
            Dm.named("on_cleaned_callback", Dm.@new<datum_callback>(this, nameof(on_cleaned)))));
        Waiting.NoSleep(AddElement(typeof(datum_element_reagents_exposed_on_fire)));
        Waiting.NoSleep(AddElement(typeof(datum_element_reagents_item_heatable)));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/rag.dm:24
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Adding [] or [] to it would make it a fair bit better at scrubbing.",
            Dm.initial(typeof(datum_reagent_water), (datum_reagent p) => p.name), Dm.initial(typeof(datum_reagent_space_cleaner), (datum_reagent p) => p.name))));
        if (blood_level >= 1 && blood_level <= 4)
            Dm.append(ref dot, span_info(I18n.T("The [] is a bit dirty, but it should still be good for cleaning.", name)));
        else if (blood_level >= 5 && blood_level <= 9)
            Dm.append(ref dot, span_warning(I18n.T("This [] is dirty! But it still probably has a few wipes left in it.", name)));
        else if (blood_level >= 10)
            Dm.append(ref dot, span_warning(I18n.T("This [] is filthy! I couldn't clean a thing with it!", name)));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/rag.dm:35
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (loc != user || blood_level <= 4)
            return dot;

        balloon_alert(user, I18n.T("wringing out..."));
        if (!Dm.truthy(await do_after(user, (wrings + 2) * 1 * SECONDS, this)))
            return dot;

        wrings += 1;
        blood_level *= 0.75f;
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/rag.dm:47
    public override async DmTask<object> pickup(mob user = null)
    {
        object dot = await base.pickup(user);
        if (prob(5 * blood_level))
            await bloody_holder(user as mob_living);
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/rag.dm:52
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_reagent_containers_spray spray)
        {
            if (Dm.num(((datum_reagents)tool.reagents).total_volume) <= 0)
            {
                balloon_alert(user, I18n.T("spray is empty!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (Dm.truthy(holder.holder_full()))
            {
                balloon_alert(user, I18n.T("[] is full!", name));
                return ITEM_INTERACT_BLOCKING;
            }

            await ((datum_reagents)tool.reagents).trans_to(holder, Dm.num(((datum_reagents)tool.reagents).total_volume), transferred_by: user);
            balloon_alert(user, I18n.T("[] spritzed", name));
            playsound(this, spray.spray_sound, 33, TRUE, -6);
            return ITEM_INTERACT_SUCCESS;
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/modules/reagents/reagent_containers/rag.dm:70
    public virtual async DmTask bloody_holder(mob_living holder = null)
    {
        if (ishuman(holder))
        {
            mob_living_carbon_human human_holder = (mob_living_carbon_human)holder;
            await human_holder.add_blood_DNA_to_items(GET_ATOM_BLOOD_DNA(this), ITEM_SLOT_GLOVES);
        }
        else
            await holder.add_blood_DNA(GET_ATOM_BLOOD_DNA(this));
    }

    // tg: code/modules/reagents/reagent_containers/rag.dm:77
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is smothering []self with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        return OXYLOSS;
    }

    // tg: code/modules/reagents/reagent_containers/rag.dm:81
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!iscarbon(interacting_with) || !Dm.truthy(holder?.total_volume))
            return await base.interact_with_atom(interacting_with, user, modifiers);
        mob_living_carbon carbon_target = (mob_living_carbon)interacting_with;
        await carbon_target.add_blood_DNA(GET_ATOM_BLOOD_DNA(this));
        object reagentlist = pretty_string_from_reagent_list(holder.reagent_list);
        string log_object = Dm.text("containing []", reagentlist);
        if (!Dm.truthy(carbon_target.is_mouth_covered()))
        {
            await holder.trans_to(carbon_target, Dm.num(holder.total_volume), transferred_by: user, methods: INGEST);
            carbon_target.visible_message(span_danger(I18n.T("[] smothers \\the [] with \\the []!", user, carbon_target, this)), span_userdanger(I18n.T("[] smothers you with \\the []!", user, this)), span_hear(I18n.T("You hear some struggling and muffled cries of surprise.")));
            log_combat(user, carbon_target, "smothered", this, log_object);
        }
        else
        {
            await holder.expose(carbon_target, TOUCH);
            await holder.clear_reagents();
            carbon_target.visible_message(span_notice(I18n.T("[] touches \\the [] with \\the [].", user, carbon_target, this)));
            log_combat(user, carbon_target, "touched", this, log_object);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/rag.dm:99
    public override async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        object dot = await base.wash(clean_types, __extra);
        if (!Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_BLOOD)))
            return dot;
        wrings = 0;
        if (Dm.truthy(blood_level))
        {
            blood_level = 0;
            update_appearance();
            dot = Dm.bitor(dot, Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP));
        }
        return dot;
    }

    ///Checks whether or not we should clean.
    // tg: code/modules/reagents/reagent_containers/rag.dm:110
    public virtual object should_clean(datum cleaning_source = null, atom atom_to_clean = null, mob_living cleaner = null)
    {
        if (Dm.truthy(cleaner.combat_mode) && ismob(atom_to_clean))
            return Dm.bitor(CLEAN_BLOCKED, CLEAN_DONT_BLOCK_INTERACTION);
        if (blood_level >= 10)
        {
            // snowflakeeeee check to make it a bit more intuitive when cleaning the rag.
            if (atom_to_clean is obj_structure_sink)
                return Dm.bitor(CLEAN_BLOCKED, CLEAN_DONT_BLOCK_INTERACTION);
            atom_to_clean.balloon_alert(cleaner, I18n.T("[] is too dirty!", name));
            return CLEAN_BLOCKED;
        }
        if (loc == cleaner)
            return CLEAN_ALLOWED;
        return Dm.bitor(CLEAN_ALLOWED, CLEAN_NO_XP);
    }

    ///On cleaning, get the rag dirty
    // tg: code/modules/reagents/reagent_containers/rag.dm:124
    public virtual async DmTask on_cleaned(datum cleaning_source = null, atom clean_target = null, mob_living cleaner = null, object was_successful = null, DmList all_cleaned = null)
    {
        if (!Dm.truthy(was_successful))
            return;

        DmList all_blood_dna = Dm.list();
        foreach (atom_movable cleaned in Dm.each<atom_movable>(all_cleaned))
        {
            if (isturf(clean_target) && !HAS_TRAIT(cleaned, TRAIT_MOPABLE))
                continue;
            // collect dna FIRST
            if (all_cleaned[cleaned] != null) // Check if there was any blood on it, we don't want nulls in our list
                Dm.combine(ref all_blood_dna, all_cleaned[cleaned]);
            // THEN pass on dna (though in some cases the cleaned item is being deleted)
            if (blood_level > 0 && !QDELING(cleaned))
                await cleaned.add_blood_DNA(GET_ATOM_BLOOD_DNA(this));
            // THEN increment blood level
            if (Dm.truthy(Dm.length(all_cleaned[cleaned])))
            {
                float how_dirty = get_blood_level_of_movable(cleaned);
                if (!await remove_cleanable_reagents(how_dirty))
                    blood_level += how_dirty;
            }
            // you didn't think you could "clean" a burning person and escape scot-free did you?
            mob_living living_cleaned = cleaned as mob_living;
            if (Dm.truthy(Dm.bitand(cleaned.resistance_flags, ON_FIRE)) || (living_cleaned != null && Dm.truthy(living_cleaned.on_fire)))
                await fire_act(500, 100);
        }

        // FINALLY add the dna to us
        if (Dm.truthy(Dm.length(all_blood_dna)))
            await add_blood_DNA(all_blood_dna);
        update_appearance();
        if (blood_level >= 10)
            to_chat(cleaner, span_warning(I18n.T("[] is too dirty to clean anything else! Wash it first!", this)));
        if (prob(10 * blood_level))
            await bloody_holder(cleaner);
    }

    /// Checks an atom and returns how "dirty" it is scaled to our rag
    // tg: code/modules/reagents/reagent_containers/rag.dm:158
    private float get_blood_level_of_movable(atom_movable what = null)
    {
        // PRIVATE_PROC(TRUE)
        if (what is obj_item_rag friend_rag)
            return friend_rag.blood_level;
        if (what is obj_effect_decal_cleanable_blood mess)
            return round(mess.bloodiness / 20, 1);
        return 1;
    }

    /// Takes in a "dirty" amount and tries to "counteract" it with reagents, returning TRUE if successful
    // tg: code/modules/reagents/reagent_containers/rag.dm:169
    private async DmTask<bool> remove_cleanable_reagents(float how_dirty = 1)
    {
        // PRIVATE_PROC(TRUE)
        float amount_to_remove = how_dirty * 0.2f;
        // cleaner is the best at scrubbing blood
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_space_cleaner), amount_to_remove, check_subtypes: TRUE)))
        {
            await holder.remove_reagent(typeof(datum_reagent_space_cleaner), amount_to_remove);
            return true;
        }

        // rest of the stuff is generically worse
        amount_to_remove = how_dirty * 1.2f;
        foreach (datum_reagent other_reagent in Dm.each_cast<datum_reagent>(holder.reagent_list))
        {
            if (Dm.truthy(Dm.bitand(other_reagent.chemical_flags, REAGENT_CLEANS)) && Dm.num(other_reagent.volume) >= amount_to_remove)
            {
                await holder.remove_reagent(other_reagent.type, amount_to_remove);
                return true;
            }
        }

        return false;
    }

    // tg: code/modules/reagents/reagent_containers/rag.dm:186
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        // Gets closer to the mixed blood color as we get dirtier
        object blood_color = get_blood_dna_color();
        if (!Dm.truthy(blood_color))
            blood_color = COLOR_RED;
        DmList color_breakdown = Dm.rgb2num(blood_color as string);
        float v = max(1 - (0.1f * blood_level), 0);
        float r = Dm.num(color_breakdown[1]) / 255, g = Dm.num(color_breakdown[2]) / 255, b = Dm.num(color_breakdown[3]) / 255;
        DmList colormatrix = Dm.list(
            r + v * (1 - r), 0, 0, 0,
            0, g + v * (1 - g), 0, 0,
            0, 0, b + v * (1 - b), 0,
            0, 0, 0, 1);

        Waiting.NoSleep(add_atom_colour(colormatrix, FIXED_COLOUR_PRIORITY));
        return dot;
    }
}
