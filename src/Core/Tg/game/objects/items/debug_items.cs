// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/debug_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole. The debug items (made by admins or the debug maps): the human spawner makes a human on a clicked
// turf, of the species picked in hand (DV532: tg's BYOND input() list is tgui's list prompt); the omnitool takes any tool's
// behaviour from its radial menu (GLOB.tool_to_image) and says its mode on examine; the spawntool spawns every type (or every
// subtype) of a typed path, and gibs a user without admin rights who insists. The var blocks are generated (D111); the spawner's
// species is the species type (Generator.Members.cs TypeHoldingVars).

/* This file contains standalone items for debug purposes. */
// tg: code/game/objects/items/debug_items.dm:5
public partial class obj_item_debug_human_spawner : obj_item_debug
{
    // tg: code/game/objects/items/debug_items.dm:17
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/debug_items.dm:20
    public override DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (isturf(interacting_with))
        {
            mob_living_carbon_human H = Dm.@new<mob_living_carbon_human>(interacting_with);
            if (selected_species != null)
                H.set_species(selected_species);
            return DmTask<object>.FromResult(ITEM_INTERACT_SUCCESS);
        }
        return DmTask<object>.FromResult(NONE);
    }

    // tg: code/game/objects/items/debug_items.dm:28
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (!Dm.truthy(user.client))
            return dot;
        // DEVIATION(DV532): input("Select a species", "Human Spawner", null) in sortTim(...) is tgui's list prompt (as DV217)
        object choice = await tgui_input_list(user, I18n.T("Select a species"), I18n.T("Human Spawner"), sortTim(GLOB.species_list, "/proc/" + nameof(GlobalProcs.cmp_text_asc)) as DmList);
        selected_species = GLOB.species_list[choice] as Type;
        return dot;
    }
}

// tg: code/game/objects/items/debug_items.dm:35
public partial class obj_item_debug_omnitool : obj_item_debug
{
    // tg: code/game/objects/items/debug_items.dm:47
    [DmParams()]
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T(" The mode is: []", tool_behaviour));
        return dot;
    }

    // tg: code/game/objects/items/debug_items.dm:51
    public virtual async DmTask<object> check_menu(mob user = null)
    {
        if (!(user is mob))
            return FALSE;
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(await user.Adjacent(this)))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/debug_items.dm:58
    public override object get_all_tool_behaviours()
    {
        return GLOB.all_tool_behaviours;
    }

    // tg: code/game/objects/items/debug_items.dm:61
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(user))
            return null;
        object tool_result = await show_radial_menu(user, this, GLOB.tool_to_image, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(await check_menu(user)))
            return null;
        tool_behaviour = tool_result;
        return null;
    }
}

// tg: code/game/objects/items/debug_items.dm:69
public partial class obj_item_debug_omnitool_item_spawner : obj_item_debug_omnitool
{
    // tg: code/game/objects/items/debug_items.dm:73
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(user) || !Dm.truthy(user.client))
            return null;
        Type path = text2path((string)await tgui_input_text(user, I18n.T("Insert an item typepath to spawn"), I18n.T("ADMINS ONLY. FUCK AROUND AND FIND OUT.")));
        if (path == null)
            return null;
        object choice = await tgui_alert(user, I18n.T("Subtypes only?"), null, Dm.list(I18n.T("Yes"), I18n.T("No")));
        if (!Dm.truthy(choice))
            return null;
        if (!Dm.truthy(user.client.holder))
        {
            if (!isliving(user))
                return null;
            mob_living living_user = user as mob_living;
            to_chat(user, span_warning(I18n.T("As you try to use [], you hear strange tearing sounds, as if the coder gods were attempting to reach out and choke you themselves.", this)));
            playsound(this, Dm.resource("sound/effects/dimensional_rend.ogg"));
            await sleep(4 * SECONDS);
            object confirmation = await tgui_alert(user, I18n.T("Are you certain you want to do that?"), I18n.T("Admins Only. Last Chance."), Dm.list(I18n.T("Yes"), I18n.T("No")));
            if (!Dm.truthy(confirmation) || Dm.eq(confirmation, I18n.T("No")))
                return null;
            if (!Dm.truthy(user.client.holder)) //safety if the admin readmined to save their ass lol.
            {
                to_chat(user, span_reallybig(I18n.T("You shouldn't have done that...")));
                playsound(this, Dm.resource("sound/mobs/non-humanoids/cyborg/borg_deathsound.ogg"));
                await sleep(3 * SECONDS);
                living_user.investigate_log(Dm.text("has been gibbed by [].", this), INVESTIGATE_DEATHS);
                await living_user.gib(DROP_ALL_REMAINS);
                return null;
            }
        }
        turf loc_turf = get_turf(this) as turf;
        foreach (object spawn_atom in Dm.each(Dm.eq(choice, I18n.T("No")) ? typesof(path) : subtypesof(path)))
            Dm.@new((Type)spawn_atom, loc_turf);
        return null;
    }
}
