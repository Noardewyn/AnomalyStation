// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai_laws/state_laws_ui.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Basic UI that Silicons can use to state their laws.
// tg: code/datums/ai_laws/state_laws_ui.dm:2
public partial class datum_state_laws_ui : datum
{
    // tg: code/datums/ai_laws/state_laws_ui.dm:10
    [DmParams("owner")]
    public override async DmTask New(params object[] __extra)
    {
        mob_living_silicon owner = Dm.arg(__extra, 0) as mob_living_silicon;
        this.owner = owner;
        if (owner.laws != null)
            update_inherent_stated_laws(owner.laws);
    }

    /// Used to update the to_state list to the passed ai law's inherent laws
    /// Call this when changing the AI's lawset entirely
    // tg: code/datums/ai_laws/state_laws_ui.dm:17
    public virtual void update_inherent_stated_laws(datum_ai_laws laws = null)
    {
        to_state = assoc_to_values(laws.inherent) as DmList;
    }

    // tg: code/datums/ai_laws/state_laws_ui.dm:20
    public override object Destroy(float? force = null)
    {
        owner = null;
        return base.Destroy(force);
    }

    // tg: code/datums/ai_laws/state_laws_ui.dm:24
    public virtual object get_laws_ui_data()
    {
        DmList law_data = Dm.list();
        object zeroth = (iscyborg(owner) && Dm.truthy(owner.laws.zeroth_borg)) ? owner.laws.zeroth_borg : owner.laws.zeroth;
        if (Dm.truthy(zeroth))
            Dm.append(law_data, Dm.list(law_to_ui_data(zeroth, 0, "hacked")));
        foreach (object law in Dm.each(owner.laws.hacked))
            Dm.append(law_data, Dm.list(law_to_ui_data(law, ion_num(), "hacked")));
        float number = 1;
        foreach ((object law, object is_ioned) in Dm.pairs(owner.laws.inherent))
            Dm.append(law_data, Dm.list(law_to_ui_data(law, Dm.truthy(is_ioned) ? ion_num() : number++, "core")));
        foreach ((object law, object is_ioned) in Dm.pairs(owner.laws.supplied))
            Dm.append(law_data, Dm.list(law_to_ui_data(law, Dm.truthy(is_ioned) ? ion_num() : number++, "supplied")));
        return law_data;
    }

    // tg: code/datums/ai_laws/state_laws_ui.dm:38
    public virtual object law_to_ui_data(object law_text = null, object law_number = null, object law_type = null)
    {
        return Dm.list(
            Dm.kv("text", law_text),
            Dm.kv("number", law_number),
            Dm.kv("type", law_type));
    }

    // tg: code/datums/ai_laws/state_laws_ui.dm:45
    public override void ui_close(mob user = null)
    {
        update_inherent_stated_laws(owner.laws);
    }

    // tg: code/datums/ai_laws/state_laws_ui.dm:48
    public override object ui_state(mob user = null)
    {
        return GLOB.not_incapacitated_state;
    }

    // tg: code/datums/ai_laws/state_laws_ui.dm:51
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (!(user == owner))
            throw Dm.CRASH(assertion_message("code/datums/ai_laws/state_laws_ui.dm", 52, "user == owner", Dm.text("Non-owner [] tried to access []'s [] UI.", user, owner, type)), "code/datums/ai_laws/state_laws_ui.dm", 52);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "StateLawUi");
            await ui.open();
        }
        return null;
    }

    // tg: code/datums/ai_laws/state_laws_ui.dm:58
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["locked"] = locked;
        data["stated_laws"] = to_state;
        data["all_laws"] = get_laws_ui_data();

        return data;
    }

    // tg: code/datums/ai_laws/state_laws_ui.dm:67
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        object sw__1 = action;
        if (Dm.eq(sw__1, "toggle_stated"))
        {
            object law = @params["law"];
            if (Dm.truthy(law))
            {
                if (Dm.@in(law, to_state))
                    Dm.remove(to_state, law);
                else
                    Dm.append(to_state, law);
            }
            return TRUE;
        }
        else if (Dm.eq(sw__1, "state_laws"))
        {
            state_laws();
            SStgui.close_uis(this);
            return TRUE;
        }
        return dot;
    }

    // tg: code/datums/ai_laws/state_laws_ui.dm:84
    public virtual void state_laws(float? force_all_laws = null)
    {
        Waiting.WaitforFalse(state_laws__body(force_all_laws));
    }

    private async DmTask state_laws__body(float? force_all_laws)
    {
        force_all_laws ??= FALSE;

        if (Dm.truthy(locked))
            return;

        locked = TRUE;

        string forced_log_message = Dm.text("stating laws[]", Dm.truthy(force_all_laws) ? ", forced" : "");
        // Create a cache of our laws before we start, so if they're changed mid-statement, nothing strange happens
        object lawcache_zeroth = (iscyborg(owner) && Dm.truthy(owner.laws.zeroth_borg)) ? owner.laws.zeroth_borg : owner.laws.zeroth;
        DmList lawcache_hacked = owner.laws.hacked.Copy();
        DmList lawcache_inherent = owner.laws.inherent.Copy();
        DmList lawcache_supplied = owner.laws.supplied.Copy();
        DmList to_state_cached = to_state.Copy();

        //"radiomod" is inserted before a hardcoded message to change if and how it is handled by an internal radio.
        await owner.say(Dm.text("[] Current Active Laws:", owner.radiomod), forced: forced_log_message);
        await sleep(1 * SECONDS);

        if (Dm.truthy(lawcache_zeroth) && (Dm.truthy(force_all_laws) || Dm.@in(lawcache_zeroth, to_state_cached)))
        {
            await owner.say(Dm.text("[] 0. []", owner.radiomod, lawcache_zeroth), forced: forced_log_message, message_mods: Dm.list(Dm.kv(MODE_SEQUENTIAL, TRUE), Dm.kv(SAY_MOD_VERB, "states")));
            await sleep(1 * SECONDS);
        }

        foreach (object law in Dm.each(lawcache_hacked))
        {
            if (Dm.truthy(force_all_laws) || Dm.@in(law, to_state_cached))
            {
                await owner.say(Dm.text("[] []. []", owner.radiomod, ion_num(), law), forced: forced_log_message, message_mods: Dm.list(Dm.kv(MODE_SEQUENTIAL, TRUE), Dm.kv(SAY_MOD_VERB, "states")));
                await sleep(1 * SECONDS);
            }
        }

        float number = 1;
        foreach ((object law_text, object is_ioned) in Dm.pairs(lawcache_inherent))
        {
            if (Dm.truthy(force_all_laws) || Dm.@in(law_text, to_state_cached))
            {
                await owner.say(Dm.text("[] []. []", owner.radiomod, Dm.truthy(is_ioned) ? ion_num() : number, law_text), forced: forced_log_message, message_mods: Dm.list(Dm.kv(MODE_SEQUENTIAL, TRUE), Dm.kv(SAY_MOD_VERB, "states")));
                if (!Dm.truthy(is_ioned))
                    number++;
                await sleep(1 * SECONDS);
            }
        }

        foreach ((object law_text, object is_ioned) in Dm.pairs(lawcache_supplied))
        {
            if (Dm.truthy(force_all_laws) || Dm.@in(law_text, to_state_cached))
            {
                await owner.say(Dm.text("[] []. []", owner.radiomod, Dm.truthy(is_ioned) ? ion_num() : number, law_text), forced: forced_log_message, message_mods: Dm.list(Dm.kv(MODE_SEQUENTIAL, TRUE), Dm.kv(SAY_MOD_VERB, "states")));
                if (!Dm.truthy(is_ioned))
                    number++;
                await sleep(1 * SECONDS);
            }
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(reset_locked)), 3 * SECONDS, file: "code/datums/ai_laws/state_laws_ui.dm", line: 128);
    }

    // tg: state_laws_ui.dm:128  VARSET_CALLBACK(src, locked, FALSE)
    public virtual void reset_locked() => locked = FALSE;
}
