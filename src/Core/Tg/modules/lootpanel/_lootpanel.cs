// Translated from tgstation (snapshot 1d99bd7f): code/modules/lootpanel/_lootpanel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package G1 (D291): the whole lootpanel module (the list of a tile's contents an alt-click opens, tgui's LootPanel window,
// godot/scripts/Shared/Tgui/Interfaces/LootPanel.cs): this file, contents.cs, handlers.cs, misc.cs, search_object.cs, ss_looting.cs and
// ui.cs. The var block is generated.

/**
 * ## Loot panel
 * A datum that stores info containing the contents of a turf.
 * Handles opening the lootpanel UI and searching the turf for items.
 */
// tg: code/modules/lootpanel/_lootpanel.dm:6
[DmPath("/datum/lootpanel")]
public partial class datum_lootpanel : datum
{
    // tg: code/modules/lootpanel/_lootpanel.dm:20
    [DmParams("owner")]
    public override async DmTask New(params object[] __extra)
    {
        client owner = Dm.arg(__extra, 0) as client;
        await base.New(__extra);

        this.owner = owner;
    }

    // tg: code/modules/lootpanel/_lootpanel.dm:26
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref SSlooting.backlog, this);
        Dm.remove(ref SSlooting.processing, this);
        reset_contents();
        owner = null;
        source_turf = null;

        return base.Destroy(force);
    }

    // tg: code/modules/lootpanel/_lootpanel.dm:36
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "LootPanel");
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/lootpanel/_lootpanel.dm:44
    public override object ui_host(mob user = null)
    {
        return source_turf;
    }

    // tg: code/modules/lootpanel/_lootpanel.dm:48
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);

        UnregisterSignal(source_turf, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON));
        source_turf = null;
        reset_contents();
    }

    // tg: code/modules/lootpanel/_lootpanel.dm:56
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["contents"] = get_contents();
        data["searching"] = length(to_image);

        return data;
    }

    // tg: code/modules/lootpanel/_lootpanel.dm:65
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "grab"))
            return await grab(usr, @params);

        return FALSE;
    }

    // tg: code/modules/lootpanel/_lootpanel.dm:75
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/modules/lootpanel/_lootpanel.dm:78
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (!Dm.@in(user, viewers(source_turf)) || Dm.truthy(HAS_TRAIT(this, TRAIT_MOVE_VENTCRAWLING)))
            return UI_CLOSE;

        if (!Dm.truthy(Dm.bitand((user as mob_living)?.mobility_flags, Dm.bitor(MOBILITY_USE, MOBILITY_PICKUP))))
            return UI_UPDATE;

        // GAP(lead): check_mutation is not translated (genetics, M8; dna.cs) and no mob has a mutation, so the telekinesis branch is never
        // taken; it is not called here because its NotTranslated stub would report on every window update (as modules/tgui/states.cs).
        bool telekinesis__gap = false;
        if (telekinesis__gap && ishuman(user))
            return tkMaxRangeCheck__local(user, source_turf) ? UI_INTERACTIVE : UI_UPDATE; // Range check here is just a formality with the viewers check above.

        if (!Dm.truthy(await source_turf.IsReachableBy(user, (user.get_active_held_item() as obj_item)?.reach)))
            return (get_dist(user, source_turf) >= 3 && Dm.truthy(user.is_blind())) ? UI_CLOSE : UI_UPDATE;

        return UI_INTERACTIVE;
    }

    // tg: code/_onclick/telekinesis.dm:214 /proc/tkMaxRangeCheck (TK_MAXRANGE, telekinesis.dm:7, is 15). GAP(lead): not declared
    // (telekinesis is M8): a local copy, as modules/tgui/states.cs's.
    private static bool tkMaxRangeCheck__local(mob user, atom target)
    {
        float d = get_dist(user, target);
        if (d > 15)
        {
            user.balloon_alert(user, I18n.T("can't TK, too far!"));
            return false;
        }
        return true;
    }
}
