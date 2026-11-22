// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implantchair.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The mindshield implanter: an occupant dragged in (the door closes on them), implanted from tg's
// ImplantChair window (the client's Interfaces/ImplantChair.cs) with one of its five implants, a minute to be ready again and ten to
// make up each spent one; the "ready" light, the open and occupied pictures; an occupant kicks the door open in a minute. The
// genetic purifier makes its occupant a plain human without mutations; the neural imprinter's brainwashing is the brainwashed
// antagonist's (`brainwash()`, antagonists after the xenomorphs, D333: not-translated.md 216); the purifier's cat removal
// (`purrbation_remove`) came with gaps SP1's felinids. The var blocks are generated (D111).

// tg: code/game/objects/items/implants/implantchair.dm:1
public partial class obj_machinery_implantchair : obj_machinery
{
    // tg: code/game/objects/items/implants/implantchair.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(open_machine());
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/implants/implantchair.dm:30
    public override object ui_state(mob user = null) => GLOB.notcontained_state;

    // tg: code/game/objects/items/implants/implantchair.dm:33
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ImplantChair", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/implants/implantchair.dm:39
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        mob_living mob_occupant = occupant as mob_living;

        data["occupied"] = mob_occupant != null ? 1 : 0;
        data["open"] = state_open;

        data["occupant"] = Dm.list();
        if (mob_occupant != null)
        {
            ((DmList)data["occupant"])["name"] = mob_occupant.name;
            ((DmList)data["occupant"])["stat"] = mob_occupant.stat;
        }

        data["special_name"] = Dm.truthy(special) ? special_name : null;
        data["ready_implants"] = ready_implants;
        data["ready"] = ready;
        data["replenishing"] = replenishing;

        return data;
    }

    // tg: code/game/objects/items/implants/implantchair.dm:58
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        object sw__1 = action;
        if (Dm.eq(sw__1, "door"))
        {
            if (Dm.truthy(state_open))
                await close_machine();
            else
                await open_machine();
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "implant"))
        {
            await implant(occupant as mob_living, usr);
            dot = TRUE;
        }
        return dot;
    }

    // tg: code/game/objects/items/implants/implantchair.dm:73
    public virtual async DmTask implant(mob_living M = null, mob user = null)
    {
        if (M is not mob_living)
            return;
        if (!Dm.truthy(ready_implants) || !Dm.truthy(ready))
            return;
        if (Dm.truthy(await implant_action(M, user)))
        {
            ready_implants--;
            if (!Dm.truthy(replenishing) && Dm.truthy(auto_replenish))
            {
                replenishing = TRUE;
                _addtimer(Dm.@new<datum_callback>(this, nameof(replenish)), replenish_cooldown, file: "code/game/objects/items/implants/implantchair.dm", line: 82);
            }
            if (injection_cooldown > 0)
            {
                ready = FALSE;
                _addtimer(Dm.@new<datum_callback>(this, nameof(set_ready)), injection_cooldown, file: "code/game/objects/items/implants/implantchair.dm", line: 85);
            }
        }
        else
            playsound(get_turf(this), Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 25, TRUE);
        update_appearance();
    }

    // tg: code/game/objects/items/implants/implantchair.dm:90
    public virtual async DmTask<object> implant_action(mob_living M = null, params object[] __extra)
    {
        obj_item I = Dm.@new(implant_type) as obj_item;
        if (I is obj_item_implant P)
        {
            if (Dm.truthy(await P.implant(M)))
            {
                visible_message(span_warning(I18n.T("[] is implanted by [].", M, this)));
                return TRUE;
            }
        }
        else if (isorgan(I))
        {
            obj_item_organ P__1 = (obj_item_organ)I;
            await P__1.Insert(M as mob_living_carbon, FALSE, FALSE);
            visible_message(span_warning(I18n.T("[] is implanted by [].", M, this)));
            return TRUE;
        }
        return null;
    }

    // tg: code/game/objects/items/implants/implantchair.dm:103
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.initial(this, (obj_machinery_implantchair p) => p.icon_state);
        if (Dm.truthy(state_open))
            icon_state += "_open";
        if (occupant != null)
            icon_state += "_occupied";
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/implants/implantchair.dm:111
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(ready))
            Dm.append(ref dot, "ready");
        return dot;
    }

    // tg: code/game/objects/items/implants/implantchair.dm:116
    public virtual void replenish()
    {
        if (ready_implants < max_implants)
            ready_implants++;
        if (ready_implants < max_implants)
            _addtimer(Dm.@new<datum_callback>(this, "replenish"), replenish_cooldown, file: "code/game/objects/items/implants/implantchair.dm", line: 120);
        else
            replenishing = FALSE;
    }

    // tg: code/game/objects/items/implants/implantchair.dm:124
    public virtual void set_ready()
    {
        ready = TRUE;
        update_appearance();
    }

    // tg: code/game/objects/items/implants/implantchair.dm:128
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        user.change_next_special_move(CLICK_CD_BREAKOUT);
        user.visible_message(span_notice(I18n.T("You see [] kicking against the door of []!", user, this)),
            span_notice(I18n.T("You lean on the back of [] and start pushing the door open... (this will take about [].)", this, DisplayTimeText(breakout_time))),
            span_hear(I18n.T("You hear a metallic creaking from [].", this)));
        if (Dm.truthy(await do_after(user, breakout_time, target: this)))
        {
            if (user == null || IS_UNCONSCIOUS_OR_CRIT(user) || !ReferenceEquals(user.loc, this) || Dm.truthy(state_open))
                return null;
            user.visible_message(span_warning(I18n.T("[] successfully broke out of []!", user, this)),
                span_notice(I18n.T("You successfully break out of []!", this)));
            await open_machine();
        }
        return null;
    }

    // tg: code/game/objects/items/implants/implantchair.dm:140
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (Dm.num(message_cooldown) <= world.time)
        {
            message_cooldown = world.time + 50;
            to_chat(user, span_warning(I18n.T("[]'s door won't budge!", this)));
        }
        return null;
    }

    // tg: code/game/objects/items/implants/implantchair.dm:145
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        mob target = dropped as mob;
        if (!isliving(target))
            return null;
        await close_machine(target);
        return null;
    }

    // tg: code/game/objects/items/implants/implantchair.dm:150
    public override async DmTask<object> close_machine(atom_movable target = null, float? density_to_set = null)
    {
        mob_living user = target as mob_living;
        if ((target == null || target is mob_living) && Dm.truthy(state_open))
        {
            await base.close_machine(user);
            if (Dm.truthy(auto_inject) && Dm.truthy(ready) && ready_implants > 0)
                await implant(user, null);
        }
        return null;
    }
}

// tg: code/game/objects/items/implants/implantchair.dm:156
public partial class obj_machinery_implantchair_genepurge : obj_machinery_implantchair
{
    // tg: code/game/objects/items/implants/implantchair.dm:164
    [DmParams("human", "user")]
    public override async DmTask<object> implant_action(mob_living M = null, params object[] __extra)
    {
        mob_living_carbon_human human = M as mob_living_carbon_human;
        if (human == null)
            return FALSE;
        await human.dna.remove_all_mutations();//hulks out
        human.set_species(typeof(datum_species_human), 1);//lizards go home
        await purrbation_remove(human);//remove cats (gaps SP1)
        return TRUE;
    }
}

// tg: code/game/objects/items/implants/implantchair.dm:173
public partial class obj_machinery_implantchair_brainwash : obj_machinery_implantchair
{
    // tg: code/game/objects/items/implants/implantchair.dm:184
    [DmParams("C", "user")]
    public override async DmTask<object> implant_action(mob_living M = null, params object[] __extra)
    {
        mob_living C = M;
        mob user = Dm.arg<mob>(__extra, 0);
        if (C is not mob_living || !Dm.truthy(C.mind)) // I don't know how this makes any sense for silicons but laws trump objectives anyway.
            return FALSE;
        if (Dm.truthy(custom))
        {
            if (!Dm.truthy(user) || !Dm.truthy(await user.Adjacent(this)))
                return FALSE;
            objective = await tgui_input_text(user, I18n.T("What order do you want to imprint on []?", C), I18n.T("Brainwashing"), max_length: 120) as string;
            message_admins(Dm.text("[] set brainwash machine objective to '[]'.", ADMIN_LOOKUPFLW(user), objective));
            user.log_message(Dm.text("set brainwash machine objective to '[]'.", objective), LOG_GAME);
        }
        if (HAS_MIND_TRAIT(C, TRAIT_UNCONVERTABLE))
            return FALSE;
        // brainwash(C, objective): the brainwashed antagonist (antagonists/brainwashing, after the xenomorphs, D333)
        Dm.NotTranslated("code/game/objects/items/implants/implantchair.dm:195", "implant_action: brainwash(), the brainwashed antagonist (D333)");
        message_admins(Dm.text("[] brainwashed [] with objective '[]'.", ADMIN_LOOKUPFLW(user), key_name_admin(C), objective));
        user.log_message(Dm.text("has brainwashed [] with the objective '[]' using \\the []", key_name(C), objective, this), LOG_ATTACK);
        C.log_message(Dm.text("has been brainwashed with the objective '[]' by [] using \\the []", objective, key_name(user), this), LOG_VICTIM, log_globally: FALSE);
        log_game(Dm.text("[] brainwashed [] with objective '[]'.", key_name(user), key_name(C), objective));
        return TRUE;
    }
}
