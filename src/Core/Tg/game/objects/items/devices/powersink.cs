// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/powersink.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): the power sink whole: wrenched onto an exposed cable node and switched on, it drains the
// powernet and the APC cells on it, heats the air, warns at 90% of its heat and explodes at the top. The admin messages and logs
// are rows.

// Powersink - used to drain station power
// tg: code/game/objects/items/devices/powersink.dm:11
public partial class obj_item_powersink : obj_item
{
    // tg: code/game/objects/items/devices/powersink.dm:1
    private const float DISCONNECTED = 0;
    private const float CLAMPED_OFF = 1;
    private const float OPERATING = 2;

    // tg: code/game/objects/items/devices/powersink.dm:5
    private const float FRACTION_TO_RELEASE = 25;
    private const float ALERT = 90;
    private const float MINIMUM_HEAT = 20000;

    // tg: code/game/objects/items/devices/powersink.dm:33
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("powersink[]", Dm.eq(mode, OPERATING) ? 1 : 0);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/devices/powersink.dm:37
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(mode))
            dot.Add(I18n.T("\\The [] is bolted to the floor.", this));
        if ((in_range(user, this) || isobserver(user)) && internal_heat > max_heat * 0.5f)
            dot.Add(span_danger(I18n.T("[] is warping the air above it. It must be very hot.", this)));
        return dot;
    }

    // tg: code/game/objects/items/devices/powersink.dm:44
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = await base.set_anchored(anchorvalue);
        set_density(anchorvalue);
        return dot;
    }

    // tg: code/game/objects/items/devices/powersink.dm:48
    public virtual async DmTask set_mode(object value = null)
    {
        if (Dm.eq(value, mode))
            return;
        float sw__1 = Dm.num(value);
        if (sw__1 == DISCONNECTED)
        {
            attached = null;
            if (Dm.eq(mode, OPERATING) && internal_heat < MINIMUM_HEAT)
            {
                STOP_PROCESSING(SSobj, this);
                internal_heat = 0;
            }
            await set_anchored(FALSE);
        }
        else if (sw__1 == CLAMPED_OFF)
        {
            if (attached == null)
                return;
            if (Dm.eq(mode, OPERATING) && internal_heat < MINIMUM_HEAT)
            {
                STOP_PROCESSING(SSobj, this);
                internal_heat = 0;
            }
            await set_anchored(TRUE);
        }
        else if (sw__1 == OPERATING)
        {
            if (attached == null)
                return;
            START_PROCESSING(SSobj, this);
            await set_anchored(TRUE);
        }

        mode = Dm.num_or_null(value);
        update_appearance();
        await set_light(0);
    }

    // tg: code/game/objects/items/devices/powersink.dm:77
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = TRUE;
        if (Dm.eq(mode, DISCONNECTED))
        {
            turf T = loc as turf;
            if (isturf(T) && T.underfloor_accessibility >= UNDERFLOOR_INTERACTABLE)
            {
                attached = Dm.locate_type(typeof(obj_structure_cable), T) as obj_structure_cable;
                if (attached == null)
                {
                    to_chat(user, span_warning(I18n.T("\\The [] must be placed over an exposed, powered cable node!", this)));
                }
                else
                {
                    await set_mode(CLAMPED_OFF);
                    user.visible_message(
                        I18n.T("[] attaches \\the [] to the cable.", user, this),
                        span_notice(I18n.T("You bolt \\the [] into the floor and connect it to the cable.", this)),
                        span_hear(I18n.T("You hear some wires being connected to something.")));
                }
            }
            else
            {
                to_chat(user, span_warning(I18n.T("\\The [] must be placed over an exposed, powered cable node!", this)));
            }
        }
        else
        {
            await set_mode(DISCONNECTED);
            user.visible_message(
                I18n.T("[] detaches \\the [] from the cable.", user, this),
                span_notice(I18n.T("You unbolt \\the [] from the floor and detach it from the cable.", this)),
                span_hear(I18n.T("You hear some wires being disconnected from something.")));
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/powersink.dm:100
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        user.visible_message(
            I18n.T("[] messes with \\the [] for a bit.", user, this),
            span_notice(I18n.T("You can't fit the screwdriver into \\the []'s bolts! Try using a wrench.", this)));
        return TRUE;
    }

    // tg: code/game/objects/items/devices/powersink.dm:106
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return null;
    }

    // tg: code/game/objects/items/devices/powersink.dm:112
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        float sw__1 = Dm.num(mode);
        if (sw__1 == DISCONNECTED)
        {
            await base.attack_hand(user, modifiers, __extra);
        }
        else if (sw__1 == CLAMPED_OFF)
        {
            user.visible_message(
                I18n.T("[] activates \\the []!", user, this),
                span_notice(I18n.T("You activate \\the [].", this)),
                span_hear(I18n.T("You hear a click.")));
            message_admins(Dm.text("Power sink activated by [] at []", ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(this)));
            user.log_message("activated a powersink", LOG_GAME);
            notify_ghosts(
                I18n.T("[] has activated a power sink!", user.real_name),
                source: this,
                header: I18n.T("Shocking News!"));
            await set_mode(OPERATING);
        }
        else if (sw__1 == OPERATING)
        {
            user.visible_message(
                I18n.T("[] deactivates \\the []!", user, this),
                span_notice(I18n.T("You deactivate \\the [].", this)),
                span_hear(I18n.T("You hear a click.")));
            user.log_message("deactivated the powersink", LOG_GAME);
            await set_mode(CLAMPED_OFF);
        }
        return dot;
    }

    /// Removes internal heat and shares it with the atmosphere.
    // tg: code/game/objects/items/devices/powersink.dm:143
    public virtual void release_heat()
    {
        turf our_turf = get_turf(this) as turf;
        float temp_to_give = internal_heat / FRACTION_TO_RELEASE;
        internal_heat -= temp_to_give;
        datum_gas_mixture environment = Waiting.NoSleep(our_turf.return_air()) as datum_gas_mixture;
        float delta_temperature = temp_to_give / Dm.num(environment.heat_capacity());
        if (Dm.truthy(delta_temperature))
        {
            environment.temperature += delta_temperature;
            air_update_turf(FALSE, FALSE);
        }
        if (Dm.truthy(warning_given) && internal_heat < max_heat * 0.75f)
        {
            warning_given = FALSE;
            message_admins(Dm.text("Power sink at ([],[],[] - <A href='byond://?_src_=holder;[];adminplayerobservecoodjump=1;X=[];Y=[];Z=[]'>JMP</a>) has cooled down and will not explode.", x, y, z, HrefToken(), x, y, z));
        }
        if (!Dm.eq(mode, OPERATING) && internal_heat < MINIMUM_HEAT)
        {
            internal_heat = 0;
            STOP_PROCESSING(SSobj, this);
        }
    }

    /// Drains power from the connected powernet, if any.
    // tg: code/game/objects/items/devices/powersink.dm:160
    public virtual async DmTask drain_power()
    {
        datum_powernet powernet = attached.powernet;
        float drained = 0;
        await set_light(5);

        // Drain as much as we can from the powernet.
        drained = Dm.num(attached.newavail());
        attached.add_delayedload(drained);

        // If tried to drain more than available on powernet, now look for APCs and drain their cells
        foreach (obj_machinery_power_terminal terminal in Dm.each<obj_machinery_power_terminal>(powernet.nodes))
        {
            if (istype(terminal.master, typeof(obj_machinery_power_apc)))
            {
                obj_machinery_power_apc apc = (obj_machinery_power_apc)terminal.master;
                if (Dm.truthy(apc.operating) && apc.cell != null)
                    drained += 0.001f * Dm.num(await apc.cell.use(0.1f * STANDARD_BATTERY_CHARGE, Dm.named("force", TRUE)));
            }
        }
        internal_heat += drained;
    }

    // tg: code/game/objects/items/devices/powersink.dm:177
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(), null); // set waitfor = FALSE, inherited from /datum/process (waiting-model.md 5.4)
    }

    private async DmTask<object> process__body()
    {
        if (attached == null)
            await set_mode(DISCONNECTED);

        release_heat();

        if (!Dm.eq(mode, OPERATING))
            return null;

        await drain_power();

        if (internal_heat > max_heat * ALERT / 100)
        {
            if (!Dm.truthy(warning_given))
            {
                warning_given = TRUE;
                message_admins(Dm.text("Power sink at ([],[],[] - <A href='byond://?_src_=holder;[];adminplayerobservecoodjump=1;X=[];Y=[];Z=[]'>JMP</a>) has reached []% of max heat. Explosion imminent.", x, y, z, HrefToken(), x, y, z, ALERT));
                notify_ghosts(
                    I18n.T("[] is about to reach critical heat capacity!", this),
                    source: this,
                    header: I18n.T("Power Sunk"));
            }
            playsound(this, Dm.resource("sound/effects/screech.ogg"), 100, TRUE, TRUE);
        }

        if (internal_heat >= max_heat)
        {
            STOP_PROCESSING(SSobj, this);
            await explosion(this, devastation_range: 4, heavy_impact_range: 8, light_impact_range: 16, flash_range: 32);
            qdel(this);
        }
        return null;
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/objects/items/devices/powersink.dm:109
public partial class obj_item_powersink : obj_item
{
    // tg: code/game/objects/items/devices/powersink.dm:109
    public override async DmTask<object> attack_ai(mob user = null)
    {
        return null;
    }
}
