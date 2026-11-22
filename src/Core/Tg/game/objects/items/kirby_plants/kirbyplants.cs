// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/kirby_plants/kirbyplants.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps ST1 (D333's station order: section 9's decor): the whole file. The potted plants: carried in both hands (two_handed), held
// up to hide behind (tactical), beautiful (beauty 500), with their pickup and drop sounds; a random plant picks its look at init, a
// plastic one among the four plastic looks; someone with the bonsai skill trims a plant with a sharp item into the next look; a dead
// plant shows its withered state until a seed is planted in it; Potty's light toggles with its action; the neglected fern is
// swabbable (the cell lines are inert, as the banana's). The organic and synthetic plants (organic_plants.dm,
// synthetic_plants.dm) are var-only. The var blocks are generated (D111).

// tg: code/game/objects/items/kirby_plants/kirbyplants.dm:1
public partial class obj_item_kirbyplants : obj_item
{
    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:23  var/static/list/random_plant_states
    public static ref DmList random_plant_states => ref TgState.Current.obj_item_kirbyplants__random_plant_states;

    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_tactical)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("require_twohands", TRUE), Dm.named("force_unwielded", 10), Dm.named("force_wielded", 10)));
        Waiting.NoSleep(AddElement(typeof(datum_element_beauty), 500));
        if (icon_state != Dm.str(base_icon_state) && icon_state != "plant-25") //mapedit support
            base_icon_state = icon_state;
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:34
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        if (Dm.truthy(custom_plant_name))
            return dot;
        name = I18n.T("[][]", Dm.truthy(dead) ? I18n.T("dead ") : null, Dm.initial(this, (atom p) => p.name));
        return dot;
    }

    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:40
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = Dm.truthy(dead) ? I18n.T("The unidentifiable plant remnants make you feel like planting something new in the pot.") : Dm.initial(this, (atom p) => p.desc);
        return dot;
    }

    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:44
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (Dm.eq(var_name, nameof(dead)))
            update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:49
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(dead) ? "plant-25" : Dm.str(base_icon_state);
        return dot;
    }

    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:53
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(dead) && Dm.truthy(trimmable) && HAS_TRAIT(user, TRAIT_BONSAI) && isturf(loc) && Dm.truthy(tool.get_sharpness()))
        {
            to_chat(user, span_notice(I18n.T("You start trimming [].", this)));
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("You finish trimming [].", this)));
            change_visual();
        }

        if (Dm.truthy(dead) && tool is obj_item_seeds)
        {
            to_chat(user, span_notice(I18n.T("You start planting a new seed into the pot.")));
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;
            qdel(tool);
            dead = FALSE;
            update_appearance();
        }

        return NONE;
    }

    /// Cycle basic plant visuals
    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:72
    public virtual void change_visual()
    {
        if (random_plant_states == null)
            random_plant_states = generate_states() as DmList;
        float current = random_plant_states.Find(icon_state);
        float next = WRAP(current + 1, 1, length(random_plant_states));
        base_icon_state = random_plant_states[next];
        update_appearance(UPDATE_ICON);
    }

    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:80
    public virtual object generate_states()
    {
        DmList plant_states = Dm.list();
        for (float i = 1, i__end = 24; i <= i__end; i++)
        {
            object number;
            if (i < 10)
                number = Dm.text("0[]", i);
            else
                number = Dm.text("[]", i);
            Dm.append(ref plant_states, Dm.text("plant-[]", number));
        }
        Dm.append(ref plant_states, "applebush");

        return plant_states;
    }
}

// tg: code/game/objects/items/kirby_plants/kirbyplants.dm:93
public partial class obj_item_kirbyplants_random : obj_item_kirbyplants
{
    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:97
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon = Dm.resource("icons/obj/fluff/flora/plants.dmi");
        randomize_base_icon_state();
        return dot;
    }

    //Handles randomizing the icon during initialize()
    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:103
    public virtual void randomize_base_icon_state()
    {
        if (random_plant_states == null)
            random_plant_states = generate_states() as DmList;
        base_icon_state = pick(random_plant_states);
        if (!Dm.truthy(dead)) //no need to update the icon if we're already dead.
            update_appearance(UPDATE_ICON);
    }
}

// tg: code/game/objects/items/kirby_plants/kirbyplants.dm:110
public partial class obj_item_kirbyplants_random_dead : obj_item_kirbyplants_random
{
    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:118
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = I18n.T("A gift from the botanical staff, presented after the RD's reassignment. There's a tag on it that says \"Y'all come back now, y'hear?\"[]", Dm.truthy(dead) ? I18n.T("\nIt doesn't look very healthy...") : null);
        return dot;
    }
}

// tg: code/game/objects/items/kirby_plants/kirbyplants.dm:122
public partial class obj_item_kirbyplants_random_fullysynthetic : obj_item_kirbyplants_random
{
    //Handles randomizing the icon during initialize()
    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:130
    public override void randomize_base_icon_state()
    {
        base_icon_state = Dm.text("plant-[]", rand(26, 29));
        update_appearance(UPDATE_ICON);
    }
}

// tg: code/game/objects/items/kirby_plants/kirbyplants.dm:141
public partial class obj_item_kirbyplants_potty : obj_item_kirbyplants
{
    //this is called by the action type as well
    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:160
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        light_enabled = Dm.num(!Dm.truthy(light_enabled));
        await set_light_on(light_enabled);
        await update_item_action_buttons();
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:169
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(dead))
            return dot;
        if (Dm.truthy(light_enabled))
            Dm.append(ref dot, Dm.text("[]_light", base_icon_state));
        return dot;
    }
}

// tg: code/game/objects/items/kirby_plants/kirbyplants.dm:176
public partial class obj_item_kirbyplants_fern : obj_item_kirbyplants
{
    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:182
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_ALGAE, CELL_VIRUS_TABLE_GENERIC, rand(2, 4), 5)); // inert until the cell lines (cytology)
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/kirby_plants/kirbyplants.dm:23
    internal DmList obj_item_kirbyplants__random_plant_states;
}
