// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/objects/byteforge.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the byteforge that prints the domain's decrypted rewards: its idle particles overlay, the
// flicker of its print (the on_overlay flicked for a second, the light) and the flash and sparks when the reward appears. The var
// block is generated (D111).

// tg: code/modules/bitrunning/objects/byteforge.dm:1
public partial class obj_machinery_byteforge : obj_machinery
{
    // tg: code/modules/bitrunning/objects/byteforge.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        register_context();
        return dot;
    }

    // tg: code/modules/bitrunning/objects/byteforge.dm:18
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();

        setup_particles();
    }

    // tg: code/modules/bitrunning/objects/byteforge.dm:23
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;
        if (held_item == null)
            return dot;

        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(panel_open) ? I18n.T("Close Panel") : I18n.T("Open Panel");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR) && Dm.truthy(panel_open))
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Deconstruct");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/bitrunning/objects/byteforge.dm:35
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;

        Dm.append(ref dot, span_notice(I18n.T("Must be within 4 tiles of the quantum server.")));

        Dm.append(ref dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT(I18n.T("screwed")), Dm.truthy(panel_open) ? I18n.T("close") : I18n.T("open"))));
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("It can be [] apart.", EXAMINE_HINT(I18n.T("pried")))));
        return dot;
    }

    // tg: code/modules/bitrunning/objects/byteforge.dm:44
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);

        setup_particles();
        return dot;
    }

    // tg: code/modules/bitrunning/objects/byteforge.dm:49
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/bitrunning/objects/byteforge.dm:52
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/bitrunning/objects/byteforge.dm:55
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]_panel", base_icon_state) : Dm.str(base_icon_state);
        return dot;
    }

    /// Does some sparks after it's done
    // tg: code/modules/bitrunning/objects/byteforge.dm:60
    public virtual async DmTask flash(atom_movable thing = null)
    {
        playsound(this, Dm.resource("sound/effects/magic/blink.ogg"), 50, TRUE);

        do_sparks(5, TRUE, loc as atom, spark_type: typeof(datum_effect_system_basic_spark_spread_quantum));
        await set_light(l_on: FALSE);
    }

    /// Forge begins to process
    // tg: code/modules/bitrunning/objects/byteforge.dm:67
    public virtual async DmTask flicker(float? angry = null)
    {
        angry ??= FALSE;
        mutable_appearance lighting = mutable_appearance(Dm.initial(this, nameof(icon)), Dm.text("on_overlay[]", Dm.truthy(angry) ? "_angry" : "")) as mutable_appearance;
        flick_overlay_view(lighting, 1 * SECONDS);

        await set_light(l_range: 2, l_power: 1.5f, l_color: Dm.truthy(angry) ? LIGHT_COLOR_BUBBLEGUM : LIGHT_COLOR_BABY_BLUE, l_on: TRUE);
    }

    /// Adds the particle overlays to the byteforge
    // tg: code/modules/bitrunning/objects/byteforge.dm:74
    public virtual void setup_particles(float? angry = null)
    {
        angry ??= FALSE;
        cut_overlay(byteforge_particles);

        byteforge_particles = mutable_appearance(Dm.initial(this, nameof(icon)), Dm.text("on_particles[]", Dm.truthy(angry) ? "_angry" : ""), ABOVE_MOB_LAYER) as mutable_appearance;

        if (Dm.truthy(is_operational))
            add_overlay(byteforge_particles);
    }

    /// Forge is done processing
    // tg: code/modules/bitrunning/objects/byteforge.dm:83
    public virtual async DmTask spawn_cache(obj cache = null)
    {
        if (QDELETED(cache))
            return;

        await flash();

        await cache.forceMove(loc as atom);
    }

    /// Timed flash
    // tg: code/modules/bitrunning/objects/byteforge.dm:92
    public virtual async DmTask start_to_spawn(obj cache = null)
    {
        await flicker();

        _addtimer(Dm.@new<datum_callback>(this, nameof(spawn_cache), cache), 1 * SECONDS, file: "code/modules/bitrunning/objects/byteforge.dm", line: 95);
    }
}
