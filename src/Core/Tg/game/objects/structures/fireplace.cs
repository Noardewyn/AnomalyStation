// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/fireplace.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST4 (D333): the file whole. The stone fireplace fed with wooden planks (15 seconds of fire each), paper bins and paper up to
// five minutes, lit by what ignites (a lighter's or a match's line), burning down in tg's five fire overlays and its glow, its light
// from 1 to 6 by the fuel left and pointed out of the wall, its hotspot while lit, put out by water with the fuel kept; tg's burning
// loop, and the chimney's shared smoke particles (set as tg sets them; the client does not draw BYOND particle sets yet, M10+
// (particles), not-translated.md 210). The var block is generated (D111).

// tg: code/game/objects/structures/fireplace.dm:5
public partial class obj_structure_fireplace : obj_structure
{
    // tg: code/game/objects/structures/fireplace.dm:1
    private const float LOG_BURN_TIMER = 150;
    // tg: code/game/objects/structures/fireplace.dm:2
    private const float PAPER_BURN_TIMER = 5;
    // tg: code/game/objects/structures/fireplace.dm:3
    private const float MAXIMUM_BURN_TIMER = 3000;

    // tg: code/game/objects/structures/fireplace.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        burning_loop = Dm.@new<datum_looping_sound_burning>(this);
        return dot;
    }

    // tg: code/game/objects/structures/fireplace.dm:30
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        QDEL_NULL(ref burning_loop);
        remove_shared_particles(typeof(particles_smoke_burning));
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/fireplace.dm:36
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        await set_light(l_dir: dir);
    }

    /// We're offset back into the wall, account for that
    // tg: code/game/objects/structures/fireplace.dm:41
    public override object get_light_offset()
    {
        DmList hand_back = base.get_light_offset() as DmList;
        DmList dir_offset = dir2offset(REVERSE_DIR(dir)) as DmList;
        Dm.append_at(hand_back, 1, Dm.mul(dir_offset[1], 0.5f));
        Dm.append_at(hand_back, 2, Dm.mul(dir_offset[2], 0.5f));
        return hand_back;
    }

    // tg: code/game/objects/structures/fireplace.dm:48
    public virtual async DmTask<object> try_light(obj_item O = null, mob user = null)
    {
        if (Dm.truthy(lit))
        {
            to_chat(user, span_warning(I18n.T("It's already lit!")));
            return FALSE;
        }
        if (!Dm.truthy(fuel_added))
        {
            to_chat(user, span_warning(I18n.T("[] needs some fuel to burn!", this)));
            return FALSE;
        }
        object msg = await O.ignition_effect(this, user);
        if (Dm.truthy(msg))
        {
            visible_message(msg);
            await ignite();
            return TRUE;
        }
        return null;
    }

    // tg: code/game/objects/structures/fireplace.dm:61
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stack_sheet_mineral_wood wood)
        {
            float space_remaining = MAXIMUM_BURN_TIMER - Dm.num(burn_time_remaining());
            float space_for_logs = Dm.num(round(space_remaining / LOG_BURN_TIMER));
            if (space_for_logs < 1)
            {
                to_chat(user, span_warning(I18n.T("You can't fit any more of [] in []!", tool, this)));
                return ITEM_INTERACT_BLOCKING;
            }

            float logs_used = Math.Min(space_for_logs, Dm.num(wood.amount));
            await wood.use(logs_used);
            adjust_fuel_timer(LOG_BURN_TIMER * logs_used);
            user.visible_message(span_notice(I18n.T("[] tosses some wood into [].", user, this)), span_notice(I18n.T("You add some fuel to [].", this)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_paper_bin paper_bin)
        {
            user.visible_message(span_notice(I18n.T("[] throws [] into [].", user, tool, this)), span_notice(I18n.T("You add [] to [].", tool, this)));
            adjust_fuel_timer(PAPER_BURN_TIMER * Dm.num(paper_bin.total_paper));
            qdel(paper_bin);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_paper)
        {
            user.visible_message(span_notice(I18n.T("[] throws [] into [].", user, tool, this)), span_notice(I18n.T("You throw [] into [].", tool, this)));
            adjust_fuel_timer(PAPER_BURN_TIMER);
            qdel(tool);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(await tool.ignition_effect(this, user)))
        {
            await try_light(tool, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/game/objects/structures/fireplace.dm:94
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList ?? Dm.list();
        if (!Dm.truthy(lit))
            return dot;

        float remaining = Dm.num(burn_time_remaining());
        if (remaining >= 0 && remaining <= 500)
            Dm.append(dot, "fireplace_fire0");
        else if (remaining >= 500 && remaining <= 1000)
            Dm.append(dot, "fireplace_fire1");
        else if (remaining >= 1000 && remaining <= 1500)
            Dm.append(dot, "fireplace_fire2");
        else if (remaining >= 1500 && remaining <= 2000)
            Dm.append(dot, "fireplace_fire3");
        else if (remaining >= 2000 && remaining <= MAXIMUM_BURN_TIMER)
            Dm.append(dot, "fireplace_fire4");
        Dm.append(dot, "fireplace_glow");
        return dot;
    }

    // tg: code/game/objects/structures/fireplace.dm:112
    public virtual async DmTask adjust_light()
    {
        if (!Dm.truthy(lit))
        {
            await set_light(0);
            return;
        }

        float remaining = Dm.num(burn_time_remaining());
        if (remaining >= 0 && remaining <= 500)
            await set_light(1);
        else if (remaining >= 500 && remaining <= 1000)
            await set_light(2);
        else if (remaining >= 1000 && remaining <= 1500)
            await set_light(3);
        else if (remaining >= 1500 && remaining <= 2000)
            await set_light(4);
        else if (remaining >= 2000 && remaining <= MAXIMUM_BURN_TIMER)
            await set_light(6);
    }

    // tg: code/game/objects/structures/fireplace.dm:129
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(lit))
            return null;
        if (world.time > Dm.num(flame_expiry_timer))
        {
            Waiting.NoSleep(put_out());
            return null;
        }

        turf T = get_turf(this);
        T.hotspot_expose(700, 2.5f * Dm.num(seconds_per_tick));
        update_appearance();
        Waiting.NoSleep(adjust_light());
        return null;
    }

    // tg: code/game/objects/structures/fireplace.dm:141
    public override async DmTask<object> extinguish()
    {
        object dot = await base.extinguish();
        if (Dm.truthy(lit))
        {
            float fuel = Dm.num(burn_time_remaining());
            flame_expiry_timer = 0;
            await put_out();
            adjust_fuel_timer(fuel);
        }
        return dot;
    }

    // tg: code/game/objects/structures/fireplace.dm:149
    public virtual void adjust_fuel_timer(float amount = 0)
    {
        if (Dm.truthy(lit))
        {
            flame_expiry_timer = Dm.num(flame_expiry_timer) + amount;
            if (Dm.num(burn_time_remaining()) < MAXIMUM_BURN_TIMER)
                flame_expiry_timer = world.time + MAXIMUM_BURN_TIMER;
        }
        else
            fuel_added = Dm.num(clamp(fuel_added + amount, 0, MAXIMUM_BURN_TIMER));
    }

    // tg: code/game/objects/structures/fireplace.dm:157
    public virtual object burn_time_remaining()
    {
        if (Dm.truthy(lit))
            return Math.Max(0, Dm.num(flame_expiry_timer) - world.time);
        else
            return Math.Max(0, fuel_added);
    }

    // tg: code/game/objects/structures/fireplace.dm:163
    public virtual async DmTask ignite()
    {
        START_PROCESSING(SSobj, this);
        burning_loop.start();
        lit = TRUE;
        desc = I18n.T("A large stone brick fireplace, warm and cozy.");
        flame_expiry_timer = world.time + fuel_added;
        fuel_added = 0;
        update_appearance();
        await adjust_light();
        obj_effect_abstract_shared_particle_holder smoke_particles = add_shared_particles(typeof(particles_smoke_burning), Dm.text("fireplace_[]", dir)) as obj_effect_abstract_shared_particle_holder;

        switch (Dm.num(dir))
        {
            case SOUTH:
                smoke_particles.pixel_w = 16;
                smoke_particles.pixel_z = 45;
                break;
            case EAST:
                smoke_particles.pixel_w = -4;
                smoke_particles.pixel_z = 25;
                break;
            case WEST:
                smoke_particles.pixel_w = 36;
                smoke_particles.pixel_z = 25;
                break;
            case NORTH: // there is no icon state for SOUTH
                remove_shared_particles(typeof(particles_smoke_burning));
                break;
        }
    }

    // tg: code/game/objects/structures/fireplace.dm:187
    public virtual async DmTask put_out()
    {
        STOP_PROCESSING(SSobj, this);
        burning_loop.stop();
        lit = FALSE;
        update_appearance();
        await adjust_light();
        desc = Dm.initial(this, nameof(desc)) as string;
        remove_shared_particles(typeof(particles_smoke_burning));
    }
}
