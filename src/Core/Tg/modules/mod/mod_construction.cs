// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/mod_construction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. Building a MODsuit: a MOD shell takes a core (screwed in), then the helmet, chestplate,
// gauntlets and boots, is wrenched and screwed shut, and a theme's external plating finishes it into a control unit of that
// theme (each step undone with the tool that did it, or a crowbar for the parts); a broken core is repaired with a screwdriver
// into a standard core, a lavaland plasma flower wired with two cables into a plasma flower core; a plating is named, described
// and drawn after its theme. The var blocks are generated (D111).

// tg: code/modules/mod/mod_construction.dm:6
public partial class obj_item_mod_construction_helmet : obj_item_mod_construction
{
    // tg: code/modules/mod/mod_construction.dm:11
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You could insert it into a <b>MOD shell</b>...")));
        return dot;
    }
}

// tg: code/modules/mod/mod_construction.dm:15
public partial class obj_item_mod_construction_chestplate : obj_item_mod_construction
{
    // tg: code/modules/mod/mod_construction.dm:20
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You could insert it into a <b>MOD shell</b>...")));
        return dot;
    }
}

// tg: code/modules/mod/mod_construction.dm:24
public partial class obj_item_mod_construction_gauntlets : obj_item_mod_construction
{
    // tg: code/modules/mod/mod_construction.dm:29
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You could insert these into a <b>MOD shell</b>...")));
        return dot;
    }
}

// tg: code/modules/mod/mod_construction.dm:33
public partial class obj_item_mod_construction_boots : obj_item_mod_construction
{
    // tg: code/modules/mod/mod_construction.dm:38
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You could insert these into a <b>MOD shell</b>...")));
        return dot;
    }
}

// tg: code/modules/mod/mod_construction.dm:42
public partial class obj_item_mod_construction_broken_core : obj_item_mod_construction
{
    // tg: code/modules/mod/mod_construction.dm:47
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You could repair it with a <b>screwdriver</b>...")));
        return dot;
    }

    // tg: code/modules/mod/mod_construction.dm:51
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.screwdriver_act(user, tool);
        balloon_alert(user, I18n.T("repairing..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS, volume: 30)))
        {
            balloon_alert(user, I18n.T("interrupted!"));
            return dot;
        }
        Dm.@new<obj_item_mod_core_standard>(drop_location());
        qdel(this);
        return dot;
    }
}

// tg: code/modules/mod/mod_construction.dm:60
public partial class obj_item_mod_construction_lavalandcore : obj_item_mod_construction
{
    // tg: code/modules/mod/mod_construction.dm:70
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You could probably attach some <b>wires</b> to it...")));
        return dot;
    }

    // tg: code/modules/mod/mod_construction.dm:74
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_stack_cable_coil)))
            return NONE;

        if (!Dm.truthy(tool.tool_start_check(user, amount: 2)))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("installing wires..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS, amount: 2, volume: 30)))
        {
            balloon_alert(user, I18n.T("interrupted!"));
            return ITEM_INTERACT_BLOCKING;
        }

        Dm.@new<obj_item_mod_core_plasma_lavaland>(drop_location());
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/mod/mod_construction.dm:89
public partial class obj_item_mod_construction_plating : obj_item_mod_construction
{
    // tg: code/modules/mod/mod_construction.dm:97
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_mod_theme used_theme = GLOB.mod_themes[theme] as datum_mod_theme;
        name = I18n.T("\\improper MOD [] external plating", used_theme.name);
        desc = Dm.text("[] []", desc, used_theme.desc);
        icon_state = Dm.text("[]-plating", used_theme.default_skin);
        return dot;
    }
}

// tg: code/modules/mod/mod_construction.dm:136
public partial class obj_item_mod_construction_shell : obj_item_mod_construction
{
    // tg: code/modules/mod/mod_construction.dm:127-135 (file-local, #undef at :420-428)
    private const string START_STEP = "start";
    private const string CORE_STEP = "core";
    private const string SCREWED_CORE_STEP = "screwed_core";
    private const string HELMET_STEP = "helmet";
    private const string CHESTPLATE_STEP = "chestplate";
    private const string GAUNTLETS_STEP = "gauntlets";
    private const string BOOTS_STEP = "boots";
    private const string WRENCHED_ASSEMBLY_STEP = "wrenched_assembly";
    private const string SCREWED_ASSEMBLY_STEP = "screwed_assembly";

    // tg: code/modules/mod/mod_construction.dm:153
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        string display_text = null;
        switch (step)
        {
            case START_STEP:
                display_text = I18n.T("It looks like it's missing a <b>MOD core</b>...");
                break;
            case CORE_STEP:
                display_text = I18n.T("The core seems <b>loose</b>...");
                break;
            case SCREWED_CORE_STEP:
                display_text = I18n.T("It looks like it's missing a <b>helmet</b>...");
                break;
            case HELMET_STEP:
                display_text = I18n.T("It looks like it's missing a <b>chestplate</b>...");
                break;
            case CHESTPLATE_STEP:
                display_text = I18n.T("It looks like it's missing <b>gauntlets</b>...");
                break;
            case GAUNTLETS_STEP:
                display_text = I18n.T("It looks like it's missing <b>boots</b>...");
                break;
            case BOOTS_STEP:
                display_text = I18n.T("The assembly seems <b>unsecured</b>...");
                break;
            case WRENCHED_ASSEMBLY_STEP:
                display_text = I18n.T("The assembly seems <b>loose</b>...");
                break;
            case SCREWED_ASSEMBLY_STEP:
                display_text = I18n.T("All it's missing is <b>external plating</b>...");
                break;
        }
        Dm.append(ref dot, span_notice(display_text));
        return dot;
    }

    // tg: code/modules/mod/mod_construction.dm:177
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        switch (step)
        {
            case START_STEP:
                if (!istype(tool, typeof(obj_item_mod_core)))
                    return NONE;

                if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                {
                    balloon_alert(user, I18n.T("it's stuck!"));
                    return ITEM_INTERACT_BLOCKING;
                }

                playsound(this, Dm.resource("sound/machines/click.ogg"), 30, TRUE);
                balloon_alert(user, I18n.T("core inserted"));
                core = tool;
                step = CORE_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;

            case SCREWED_CORE_STEP:
                if (!istype(tool, typeof(obj_item_mod_construction_helmet))) //Construct
                    return NONE;

                if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                {
                    balloon_alert(user, I18n.T("it's stuck!"));
                    return ITEM_INTERACT_BLOCKING;
                }

                playsound(this, Dm.resource("sound/machines/click.ogg"), 30, TRUE);
                balloon_alert(user, I18n.T("helmet added"));
                helmet = tool;
                step = HELMET_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;

            case HELMET_STEP:
                if (!istype(tool, typeof(obj_item_mod_construction_chestplate))) //Construct
                    return NONE;

                if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                {
                    balloon_alert(user, I18n.T("it's stuck!"));
                    return ITEM_INTERACT_BLOCKING;
                }

                playsound(this, Dm.resource("sound/machines/click.ogg"), 30, TRUE);
                balloon_alert(user, I18n.T("chestplate added"));
                chestplate = tool;
                step = CHESTPLATE_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;

            case CHESTPLATE_STEP:
                if (!istype(tool, typeof(obj_item_mod_construction_gauntlets))) //Construct
                    return NONE;

                if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                {
                    balloon_alert(user, I18n.T("it's stuck!"));
                    return ITEM_INTERACT_BLOCKING;
                }

                playsound(this, Dm.resource("sound/machines/click.ogg"), 30, TRUE);
                balloon_alert(user, I18n.T("gauntlets added"));
                gauntlets = tool;
                step = GAUNTLETS_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;

            case GAUNTLETS_STEP:
                if (!istype(tool, typeof(obj_item_mod_construction_boots))) //Construct
                    return NONE;

                if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                {
                    balloon_alert(user, I18n.T("it's stuck!"));
                    return ITEM_INTERACT_BLOCKING;
                }

                playsound(this, Dm.resource("sound/machines/click.ogg"), 30, TRUE);
                balloon_alert(user, I18n.T("boots added"));
                boots = tool;
                step = BOOTS_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;

            case SCREWED_ASSEMBLY_STEP:
            {
                if (!istype(tool, typeof(obj_item_mod_construction_plating))) //Construct
                    return NONE;

                obj_item_mod_construction_plating external_plating = tool as obj_item_mod_construction_plating;
                if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                {
                    balloon_alert(user, I18n.T("it's stuck!"));
                    return ITEM_INTERACT_BLOCKING;
                }

                playsound(this, Dm.resource("sound/machines/click.ogg"), 30, TRUE);
                obj_item mod = Dm.@new<obj_item_mod_control>(drop_location(), external_plating.theme, null, core);
                core = null;
                qdel(this);
                await user.put_in_hands(mod);
                mod.balloon_alert(user, I18n.T("unit finished"));
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;
            }
        }

        return NONE;
    }

    // tg: code/modules/mod/mod_construction.dm:274
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        switch (step)
        {
            case CORE_STEP:
                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 30)))
                    return ITEM_INTERACT_BLOCKING;

                balloon_alert(user, I18n.T("core screwed"));
                step = SCREWED_CORE_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;

            case SCREWED_CORE_STEP:
                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 30)))
                    return ITEM_INTERACT_BLOCKING;

                balloon_alert(user, I18n.T("core unscrewed"));
                step = CORE_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;

            case WRENCHED_ASSEMBLY_STEP:
                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 30)))
                    return ITEM_INTERACT_BLOCKING;

                balloon_alert(user, I18n.T("assembly screwed"));
                step = SCREWED_ASSEMBLY_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;

            case SCREWED_ASSEMBLY_STEP:
                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 30)))
                    return ITEM_INTERACT_BLOCKING;

                balloon_alert(user, I18n.T("assembly unscrewed"));
                step = WRENCHED_ASSEMBLY_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/mod/mod_construction.dm:315
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        switch (step)
        {
            case CORE_STEP:
                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 30)))
                    return ITEM_INTERACT_SUCCESS;

                await core.forceMove(drop_location() as atom);
                balloon_alert(user, I18n.T("core taken out"));
                step = START_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;

            case HELMET_STEP:
                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 30)))
                    return ITEM_INTERACT_BLOCKING;

                await helmet.forceMove(drop_location() as atom);
                balloon_alert(user, I18n.T("helmet removed"));
                helmet = null;
                step = SCREWED_CORE_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;

            case CHESTPLATE_STEP:
                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 30)))
                    return ITEM_INTERACT_BLOCKING;

                await chestplate.forceMove(drop_location() as atom);
                balloon_alert(user, I18n.T("chestplate removed"));
                chestplate = null;
                step = HELMET_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;

            case GAUNTLETS_STEP:
                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 30)))
                    return ITEM_INTERACT_BLOCKING;

                await gauntlets.forceMove(drop_location() as atom);
                balloon_alert(user, I18n.T("gauntlets removed"));
                gauntlets = null;
                step = CHESTPLATE_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;

            case BOOTS_STEP:
                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 30)))
                    return ITEM_INTERACT_BLOCKING;

                await boots.forceMove(drop_location() as atom);
                balloon_alert(user, I18n.T("boots removed"));
                boots = null;
                step = GAUNTLETS_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/mod/mod_construction.dm:373
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        switch (step)
        {
            case BOOTS_STEP:
                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 30)))
                    return ITEM_INTERACT_BLOCKING;

                balloon_alert(user, I18n.T("assembly secured"));
                step = WRENCHED_ASSEMBLY_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;

            case WRENCHED_ASSEMBLY_STEP:
                if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 30)))
                    return ITEM_INTERACT_BLOCKING;

                balloon_alert(user, I18n.T("assembly unsecured"));
                step = BOOTS_STEP;
                await update_icon_state();
                return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/mod/mod_construction.dm:395
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("mod-construction_[]", step);
        return dot;
    }

    // tg: code/modules/mod/mod_construction.dm:399
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref core);
        QDEL_NULL(ref helmet);
        QDEL_NULL(ref chestplate);
        QDEL_NULL(ref gauntlets);
        QDEL_NULL(ref boots);
        return base.Destroy(force);
    }

    // tg: code/modules/mod/mod_construction.dm:407
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == core)
            core = null;
        if (gone == helmet)
            helmet = null;
        if (gone == chestplate)
            chestplate = null;
        if (gone == gauntlets)
            gauntlets = null;
        if (gone == boots)
            boots = null;
        return dot;
    }
}
