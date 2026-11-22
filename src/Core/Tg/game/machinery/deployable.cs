// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/deployable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (the M5 leftovers, 2026-10-16): the barricade base, the wooden barricade and the crude plank barricade that
// can_barricade builds (and the wood recipes' "wooden barricade"). The sandbags (gaps J5) and the barrier grenade with the security
// barrier it raises (gaps SE1) came later; the folded heavy machine gun stays inert with the deployable turret (structures/deployable_turret.dm). The var blocks are generated.

//Barricades/cover

// tg: code/game/machinery/deployable.dm:11
public partial class obj_structure_barricade : obj_structure
{
    // tg: code/game/machinery/deployable.dm:5  #define METAL 1 (the file's own define)
    private const int METAL = 1;

    // tg: code/game/machinery/deployable.dm:22
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        make_debris();
    }

    /// Spawn debris & stuff upon deconstruction
    // tg: code/game/machinery/deployable.dm:26
    public virtual void make_debris()
    {
        // PROTECTED_PROC(TRUE)
        return;
    }

    // tg: code/game/machinery/deployable.dm:31
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (bar_material != METAL)
            return ITEM_INTERACT_BLOCKING;

        if (Dm.num(atom_integrity) >= max_integrity)
        {
            balloon_alert(user, I18n.T("already full integrity!"));
            return ITEM_INTERACT_BLOCKING;
        }

        user.balloon_alert_to_viewers(I18n.T("repairing []...", this), I18n.T("repairing..."));

        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, amount: 10, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("repaired"));
        await repair_damage(20);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/deployable.dm:51
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)//So bullets will fly over and stuff.
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(Dm.locate<obj_structure_barricade>(get_turf(mover))))
            return TRUE;
        else if (isprojectile(mover))
        {
            if (!Dm.truthy(anchored))
                return TRUE;
            var proj = (obj_projectile)mover;
            if (proj.firer != null && Dm.truthy(await Adjacent(proj.firer)))
                return TRUE;
            if (prob(proj_pass_rate))
                return TRUE;
            return FALSE;
        }
        return dot;
    }
}

/////BARRICADE TYPES///////
// tg: code/game/machinery/deployable.dm:66
public partial class obj_structure_barricade_wooden : obj_structure_barricade
{
    // tg: code/game/machinery/deployable.dm:77
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        // var/static/list/tool_behaviors = list(TOOL_CROWBAR = list(SCREENTIP_CONTEXT_LMB = "Deconstruct"))
        Dm.Deferred("code/game/machinery/deployable.dm:81", "Initialize: AddElement(/datum/element/contextual_screentip_tools, tool_behaviors): screentips (M10+)");
        register_context();

        Waiting.NoSleep(AddElement(typeof(datum_element_uses_girder_wall_recipes)));
        return dot;
    }

    // tg: code/game/machinery/deployable.dm:86
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        balloon_alert(user, I18n.T("deconstructing barricade..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50)))
            return null;
        (loc as atom)?.balloon_alert(user, I18n.T("barricade deconstructed"));
        tool.play_tool_sound(this);
        Dm.@new<obj_item_stack_sheet_mineral_wood>(get_turf(this), drop_amount);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/deployable.dm:110
    public override void make_debris()
    {
        Dm.@new<obj_item_stack_sheet_mineral_wood>(get_turf(this), drop_amount);
    }
}

// tg: code/game/machinery/deployable.dm:113  (gaps J5, D298)
public partial class obj_structure_barricade_sandbags : obj_structure_barricade
{
    // tg: code/game/machinery/deployable.dm:127
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_climbable)));
        Waiting.NoSleep(AddElement(typeof(datum_element_elevation), Dm.named("pixel_shift", 12)));
        return dot;
    }
}

// tg: code/game/machinery/deployable.dm:149  (gaps SE1, D333: the armory's barrier grenade; the security barrier it raises is vars only)
public partial class obj_item_grenade_barrier : obj_item_grenade
{
    // tg: code/game/machinery/deployable.dm:1-3  #define SINGLE "single", VERTICAL "vertical", HORIZONTAL "horizontal" (the file's own defines)
    private const string SINGLE = "single";
    private const string VERTICAL = "vertical";
    private const string HORIZONTAL = "horizontal";

    // tg: code/game/machinery/deployable.dm:158
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("Alt-click to toggle modes.")));
        return dot;
    }

    // tg: code/game/machinery/deployable.dm:162
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        toggle_mode(user);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/machinery/deployable.dm:166
    public virtual void toggle_mode(mob user = null)
    {
        switch (mode)
        {
            case SINGLE:
                mode = VERTICAL;
                break;
            case VERTICAL:
                mode = HORIZONTAL;
                break;
            case HORIZONTAL:
                mode = SINGLE;
                break;
        }

        to_chat(user, span_notice(I18n.T("[] is now in [] mode.", this, mode)));
    }

    // tg: code/game/machinery/deployable.dm:177
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        Dm.@new<obj_structure_barricade_security>(get_turf(loc));
        switch (mode)
        {
            case VERTICAL:
            {
                var target_turf = get_step(this, NORTH) as turf;
                if (!Dm.truthy(await target_turf.is_blocked_turf()))
                    Dm.@new<obj_structure_barricade_security>(target_turf);

                var target_turf2 = get_step(this, SOUTH) as turf;
                if (!Dm.truthy(await target_turf2.is_blocked_turf()))
                    Dm.@new<obj_structure_barricade_security>(target_turf2);
                break;
            }
            case HORIZONTAL:
            {
                var target_turf = get_step(this, EAST) as turf;
                if (!Dm.truthy(await target_turf.is_blocked_turf()))
                    Dm.@new<obj_structure_barricade_security>(target_turf);

                var target_turf2 = get_step(this, WEST) as turf;
                if (!Dm.truthy(await target_turf2.is_blocked_turf()))
                    Dm.@new<obj_structure_barricade_security>(target_turf2);
                break;
            }
        }
        qdel(this);
        return dot;
    }

    // tg: code/game/machinery/deployable.dm:202
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        toggle_mode(user);
        return null;
    }
}
