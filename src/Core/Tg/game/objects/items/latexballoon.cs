// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/latexballoon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps TY1 (D333): the whole file. The latex glove balloon: a gas tank used on it blows it up with three litres of the tank's air (a
// balloon on a string in hand), a second blow, a sharp or hot tool, a bullet, an explosion or heat past 100 C bursts it and lets its
// air out where it is. The var block is generated (D111).

// tg: code/game/objects/items/latexballoon.dm:6
public partial class obj_item_latexballoon : obj_item
{
    // tg: code/game/objects/items/latexballoon.dm:1-3 (file-local, #undef at :116-118)
    private const string INFLATED = "inflated";
    private const string POPPED = "popped";
    private const string DEFLATED = "deflated";

    // tg: code/game/objects/items/latexballoon.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        return dot;
    }

    // tg: code/game/objects/items/latexballoon.dm:27
    public virtual void set_state(object state_to_set = null)
    {
        state = state_to_set as string;
        update_appearance(Dm.bitor(UPDATE_ICON, UPDATE_DESC));
    }

    // tg: code/game/objects/items/latexballoon.dm:31
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);

        switch (state)
        {
            case INFLATED:
                icon_state = "latexballoon_blow";
                lefthand_file = Dm.resource("icons/mob/inhands/items_lefthand.dmi");
                righthand_file = Dm.resource("icons/mob/inhands/items_righthand.dmi");
                inhand_icon_state = "latexballoon";
                break;
            case POPPED:
                icon_state = "latexballoon_bursted";
                inhand_icon_state = Dm.initial(this, (obj_item_latexballoon p) => p.inhand_icon_state) as string;
                lefthand_file = Dm.initial(this, (obj_item_latexballoon p) => p.lefthand_file);
                righthand_file = Dm.initial(this, (obj_item_latexballoon p) => p.righthand_file);
                break;
        }
        return dot;
    }

    // tg: code/game/objects/items/latexballoon.dm:46
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        switch (state)
        {
            case INFLATED:
                desc = I18n.T("It's a blown up latex glove on a string.");
                break;
            case POPPED:
                desc = I18n.T("The remains of a latex glove.");
                break;
        }
        return dot;
    }

    // tg: code/game/objects/items/latexballoon.dm:54
    public virtual async DmTask blow(obj_item_tank tank = null, mob user = null)
    {
        if (state == POPPED)
            return;

        air_contents = tank.remove_air_volume(3) as datum_gas_mixture;

        if (air_contents == null)
        {
            balloon_alert(user, I18n.T("tank is empty!"));
            return;
        }

        if (state == INFLATED)
        {
            await burst(); // too much air, pop it!
            return;
        }

        playsound(this, Dm.resource("sound/items/modsuit/inflate_bloon.ogg"), 50, TRUE);

        balloon_alert(user, I18n.T("you blow up the balloon!")); // because it's a balloon obviously

        set_state(INFLATED);
    }

    // tg: code/game/objects/items/latexballoon.dm:74
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(Dm.gt(exposed_temperature, T0C + 100));
    }

    // tg: code/game/objects/items/latexballoon.dm:77
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        await burst();
        return null;
    }

    // tg: code/game/objects/items/latexballoon.dm:80
    public virtual async DmTask burst()
    {
        if (air_contents == null || state != INFLATED)
            return;

        set_state(POPPED);
        playsound(this, Dm.resource("sound/items/balloon_pop.ogg"), 75, TRUE);
        await loc.assume_air(air_contents);
    }

    // tg: code/game/objects/items/latexballoon.dm:88  (set waitfor = FALSE, inherited from /atom/ex_act: the wrapper of waiting-model.md
    // 5.4; burst may sleep)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(ex_act__body(severity, target), null); // `.` is never assigned: no box (5.4)
    }

    private async DmTask<object> ex_act__body(object severity, object target)
    {
        await burst();
        object sw__1 = severity;
        if (Dm.eq(sw__1, EXPLODE_DEVASTATE))
        {
            qdel(this);
        }
        else if (Dm.eq(sw__1, EXPLODE_HEAVY))
        {
            if (prob(50))
                qdel(this);
        }

        return TRUE;
    }

    // tg: code/game/objects/items/latexballoon.dm:99
    public override async DmTask<object> bullet_act(obj_projectile projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        if (Dm.num(projectile.damage) > 0)
            await burst();
        return await base.bullet_act(projectile, def_zone, piercing_hit, blocked);
    }

    // tg: code/game/objects/items/latexballoon.dm:104
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_tank)
        {
            obj_item_tank air_tank = tool as obj_item_tank;
            await blow(air_tank, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(tool.get_sharpness()) || Dm.ge(tool.get_temperature(), FIRE_MINIMUM_TEMPERATURE_TO_EXIST))
        {
            await burst();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }
}
