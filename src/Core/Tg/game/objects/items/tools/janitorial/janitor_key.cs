// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/janitorial/janitor_key.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J3, D298). The department's access comes from the keycard authentication device, inert until the heads of staff's package (a row). The var blocks are generated.

// tg: code/game/objects/items/tools/janitorial/janitor_key.dm:5
public partial class obj_item_access_key : obj_item
{
    // GAP(types): tg's `var/list/department_access` (:15) holds the region's name, not a list (department_access_given takes region_access[1]); the generated var is a DmList, so the value lives here.
    public object department_access_value;

    ///The time limit on the keys before the access it's been given clears itself.
    private const float ACCESS_TIMER_LIMIT = 10 * MINUTES;

    // tg: code/game/objects/items/tools/janitorial/janitor_key.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(SSdcs, COMSIG_ON_DEPARTMENT_ACCESS, nameof(department_access_given));
        GLOB.janitor_devices.Add(this);
        return dot;
    }

    // tg: code/game/objects/items/tools/janitorial/janitor_key.dm:31
    public override object Destroy(float? force = null)
    {
        GLOB.janitor_devices.Remove(this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tools/janitorial/janitor_key.dm:35
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(department_access_value))
            dot.Add(I18n.T("It currently holds access to the [] region.", department_access_value));
        return dot;
    }

    // tg: code/game/objects/items/tools/janitorial/janitor_key.dm:40
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        dot.Add(span_notice(I18n.T("Access can be granted through a Keycard Authentication Device.")));
        dot.Add(span_notice(I18n.T("This access is limited to one department at a time.")));
        return dot;
    }

    /**
     * Called when attempting to open an airlock.
     * Checks if the keys have access, returns try_to_activate_door if it does, returns FALSE otherwise.
     * Args:
     * user - The person attempting to open the door
     * airlock - The door we're attempting to open
     */
    // tg: code/game/objects/items/tools/janitorial/janitor_key.dm:52
    public virtual async DmTask<object> attempt_open_door(mob_living user = null, obj_machinery_door airlock = null)
    {
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, airlock)))
            return FALSE;
        user.balloon_alert_to_viewers(I18n.T("fumbles with keys..."), I18n.T("finding key..."));
        user.playsound_local(this, Dm.resource("sound/items/rattling_keys.ogg"), 25, TRUE);
        if (!Dm.truthy(await do_after(user, key_speed, airlock)))
            return FALSE;
        if (!Dm.truthy(department_access_value) || !Dm.truthy(airlock.check_access_list(SSid_access.accesses_by_region[department_access_value] as DmList)))
        {
            airlock.balloon_alert(user, I18n.T("no access!"));
            return FALSE;
        }
        return await airlock.try_to_activate_door(user, access_bypass: TRUE);
    }

    /**
     * Called when a keycard authenticator sends region access
     * Stores it on the key to use for access, then sets a timer to clear it.
     * Args:
     * source - the keycard authenticator giving us the access
     * region_access - the list of access we're being sent, we only take the first entry in the list as there should only have one department at a time.
     */
    // tg: code/game/objects/items/tools/janitorial/janitor_key.dm:71
    [SignalHandler]
    public virtual void department_access_given(obj_machinery_keycard_auth source = null, DmList region_access = null)
    {
        department_access_value = region_access[1];
        Waiting.Detach(say(I18n.T("Access granted to [] area.", department_access_value)));
        playsound(this, Dm.resource("sound/machines/ding.ogg"), 25, TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_access)), ACCESS_TIMER_LIMIT, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/game/objects/items/tools/janitorial/janitor_key.dm", line: 76);
        log_game(Dm.text("Access to the [] department was given to [] []", department_access_value, this, ismob(loc) ? Dm.text("held by []", loc) : "which is not being held"));
        investigate_log(Dm.text("Access to the [] department was given to [] []", department_access_value, this, ismob(loc) ? Dm.text("held by []", loc) : "which is not being held"), INVESTIGATE_ACCESSCHANGES);
    }

    /**
     * Called when a keycard authenticator runs out of time
     * Clears the department access and alerts nearby people of such.
     */
    // tg: code/game/objects/items/tools/janitorial/janitor_key.dm:84
    public virtual async DmTask clear_access()
    {
        log_game(Dm.text("Access to the [] department on [] has expired.", department_access_value, this));
        investigate_log(Dm.text("Access to the [] department on [] has expired.]", department_access_value, this), INVESTIGATE_ACCESSCHANGES);
        department_access_value = null;
        await say(I18n.T("Access revoked, time ran out."));
        playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE);
    }
}
