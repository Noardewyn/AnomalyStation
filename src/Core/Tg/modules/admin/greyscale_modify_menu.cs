// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/greyscale_modify_menu.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The MetaStation leftovers: the colour menu as players use it (a canister's recolour, a vending machine's colourable products), in
// the GreyscaleModifyMenu window. DEVIATION(DV686): a configuration comes from GagsSeam (its icon states and colour count from the
// client's copy of its JSON), and the preview is the GAGS icon's name (gags:<type>|<colours>, DV19) with its icon state, which the
// window draws, in place of tg's server-built icon sent by icon2html; the full preview's steps (GenerateDebug) are not made. The
// unlocked menu's file actions (refresh_file, save_dmi, toggle_mass_refresh: an admin's view-variables menu, owner-deferred) keep tg's
// guards and are deferred leaves; the spray can's subtype waits with the GAGS recolouring component.

/// The controller for the ui in charge of all runtime greyscale configuration/debug.
/// If `Unlock()` is not called the menu is safe for players to use.
// tg: code/modules/admin/greyscale_modify_menu.dm:3
public partial class datum_greyscale_modify_menu : datum
{
    /// The client that opened this menu. tg declares it a client and is handed a mob (usr, ui.user); the vending machine compares it
    /// with its user (ui_data.dm:180), so it is declared here as what it holds.
    // tg: code/modules/admin/greyscale_modify_menu.dm:7
    public object user;

    // tg: code/modules/admin/greyscale_modify_menu.dm:42
    [DmParams("target", "user", "allowed_configs", "apply_callback", "starting_icon_state", "starting_config", "starting_colors", "unlocked")]
    public override async DmTask New(params object[] __extra)
    {
        datum target = Dm.arg(__extra, 0) as datum;
        object user = Dm.arg(__extra, 1);
        DmList allowed_configs = Dm.arg<DmList>(__extra, 2);
        datum_callback apply_callback = Dm.arg(__extra, 3) as datum_callback;
        object starting_icon_state = Dm.arg(__extra, 4) ?? "";
        object starting_config = Dm.arg(__extra, 5);
        object starting_colors = Dm.arg(__extra, 6);
        object unlocked = Dm.arg(__extra, 7) ?? FALSE;

        this.target = target;
        atom atom_target = null;
        if (isatom(target))
            atom_target = (atom)target;
        this.user = user;
        this.apply_callback = apply_callback;
        if (!Dm.truthy(apply_callback))
        {
            if (Dm.truthy(atom_target))
                this.apply_callback = Dm.@new<datum_callback>(this, nameof(DefaultApply));
            else
                stack_trace("A geyscale modify menu was instantiated with a non-atom target and no specified apply callback (DefaultApply won't do).", "code/modules/admin/greyscale_modify_menu.dm", 54);
        }

        icon_state = starting_icon_state;

        SetupConfigOwner();

        // "[starting_config]" || "[atom_target?.greyscale_config]": the first is a text, never false
        string current_config = Dm.text("[]", starting_config);
        if (current_config.Length == 0)
            current_config = Dm.text("[]", atom_target?.greyscale_config);
        datum_greyscale_config new_config = GagsSeam.Configuration(current_config);
        if (!Dm.@in(current_config, allowed_configs))
            new_config = GagsSeam.Configuration(Dm.text("[]", allowed_configs[pick(allowed_configs)]));
        change_config(new_config);

        if (Dm.truthy(unlocked))
            Unlock();
        else
        {
            DmList config_choices = Dm.list();
            foreach (object config_string in Dm.each(allowed_configs))
            {
                Type allowed_config = text2path(Dm.text("[]", config_string));
                config_choices[Dm.initial(allowed_config, nameof(datum_greyscale_config.name))] = config_string;
            }

            this.allowed_configs = config_choices;
        }

        ReadColorsFromString(Dm.truthy(starting_colors) ? starting_colors : atom_target?.greyscale_colors);

        if (Dm.truthy(target))
            RegisterSignal(target, COMSIG_QDELETING, nameof(ui_close));

        refresh_preview();
        await DmTask.CompletedTask;
    }

    // tg: code/modules/admin/greyscale_modify_menu.dm:83
    public override object Destroy(float? force = null)
    {
        target = null;
        user = null;
        apply_callback = null;
        config = null;
        return base.Destroy(force);
    }

    // tg: code/modules/admin/greyscale_modify_menu.dm:90
    public override object ui_state(mob user = null)
    {
        return Dm.truthy(unlocked) ? GLOB.always_state : GLOB.greyscale_menu_state;
    }

    // tg: code/modules/admin/greyscale_modify_menu.dm:93
    public override void ui_close(mob user = null)
    {
        qdel(this);
    }

    // tg: code/modules/admin/greyscale_modify_menu.dm:96
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "GreyscaleModifyMenu");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/admin/greyscale_modify_menu.dm:102
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["greyscale_config"] = Dm.text("[]", config?.name);

        DmList color_data = Dm.list();
        data["colors"] = color_data;
        for (float i = 1, i__end = config?.expected_colors ?? 0; i <= i__end; i++)
        {
            color_data.Add(Dm.list(Dm.list(
                Dm.kv("index", i),
                Dm.kv("value", split_colors?[i]))));
        }

        data["generate_full_preview"] = generate_full_preview;
        data["unlocked"] = unlocked;
        data["refreshing"] = refreshing;
        data["monitoring_files"] = FALSE; // !!(config.datum_flags & DF_ISPROCESSING): a configuration never processes here (DV686)
        data["sprites_dir"] = dir2text(sprite_dir);
        data["icon_state"] = icon_state;
        data["sprites"] = sprite_data;
        await DmTask.CompletedTask;
        return data;
    }

    // tg: code/modules/admin/greyscale_modify_menu.dm:121
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action as string)
        {
            case "select_config":
            {
                object chosen = await input(usr, I18n.T("Choose a new greyscale configuration to use"), I18n.T("Greyscale Modification Menu"), Dm.text("[]", config?.type), allowed_configs); // as anything in allowed_configs
                if (chosen == null)
                    break;
                object new_config_key = allowed_configs[chosen];
                datum_greyscale_config new_config = GagsSeam.Configuration(new_config_key);
                if (new_config != null && config != new_config)
                {
                    change_config(new_config);
                    queue_refresh();
                }
                break;
            }

            case "load_config_from_string":
            {
                if (!Dm.@in(@params["config_string"], allowed_configs))
                    return dot;
                datum_greyscale_config new_config = GagsSeam.Configuration(@params["config_string"]);
                if (new_config != null && config != new_config)
                {
                    change_config(new_config);
                    queue_refresh();
                }
                break;
            }

            case "toggle_full_preview":
                if (!Dm.truthy(generate_full_preview) && !Dm.truthy(unlocked))
                    return dot;
                generate_full_preview = Dm.truthy(generate_full_preview) ? FALSE : TRUE;
                queue_refresh();
                break;

            case "recolor":
            {
                float index = Dm.num(text2num(@params["color_index"]));
                string new_color = LOWER_TEXT(@params["new_color"]);
                if (!Dm.eq(split_colors[index], new_color) && (IsColor(new_color) || (Dm.truthy(unlocked) && IsAlphaColor(new_color))))
                {
                    split_colors[index] = new_color;
                    queue_refresh();
                }
                break;
            }

            case "recolor_from_string":
            {
                string full_color_string = LOWER_TEXT(@params["color_string"]);
                if (!Dm.eq(full_color_string, split_colors.Join("")) && Dm.truthy(ReadColorsFromString(full_color_string)))
                    queue_refresh();
                break;
            }

            case "pick_color":
            {
                // tg indexes split_colors with the text it is sent (params["color_index"]); the window sends the group's number
                float group = Dm.num(text2num(@params["color_index"]));
                object new_color = await tgui_color_picker(
                    usr,
                    I18n.T("Choose color for greyscale color group []:", group),
                    I18n.T("Greyscale Modification Menu"),
                    split_colors[group]);
                if (Dm.truthy(new_color))
                {
                    split_colors[group] = new_color;
                    queue_refresh();
                }
                break;
            }

            case "random_color":
            {
                float group = Dm.num(text2num(@params["color_index"]));
                randomize_color(group);
                queue_refresh();
                break;
            }

            case "random_all_colors":
                for (float i = 1, i__end = length(split_colors); i <= i__end; i++)
                    randomize_color(i);
                queue_refresh();
                break;

            case "select_icon_state":
            {
                object new_icon_state = @params["new_icon_state"];
                if (!Dm.truthy(config.icon_states[new_icon_state]))
                    return dot;
                icon_state = new_icon_state;
                queue_refresh();
                break;
            }

            case "apply":
                await apply_callback.Invoke(this);
                break;

            case "refresh_file":
                if (!Dm.truthy(unlocked) || !Dm.truthy(check_rights(R_DEBUG)))
                    return dot;
                Dm.Deferred("code/modules/admin/greyscale_modify_menu.dm:190", "ui_act: refresh_file, the unlocked menu's reload of a configuration from disk (an admin's view-variables menu, owner-deferred; DV686)");
                break;

            case "save_dmi":
                if (!Dm.truthy(unlocked))
                    return dot;
                Dm.Deferred("code/modules/admin/greyscale_modify_menu.dm:208", "ui_act: save_dmi, the unlocked menu's icon saved to tmp/ (an admin's view-variables menu, owner-deferred; DV686)");
                break;

            case "change_dir":
                sprite_dir = Dm.num(text2dir(@params["new_sprite_dir"]));
                queue_refresh();
                break;

            case "toggle_mass_refresh":
                if (!Dm.truthy(unlocked) || !Dm.truthy(check_rights(R_DEBUG)))
                    return dot;
                Dm.Deferred("code/modules/admin/greyscale_modify_menu.dm:217", "ui_act: toggle_mass_refresh, the unlocked menu's auto-refresh of the configurations (an admin's view-variables menu, owner-deferred; DV686)");
                break;
        }
        return dot;
    }

    // findtext(color, GLOB.is_color): the regex (a shim gap) is its pattern, ^#[0-9a-fA-F]{6}$ (_globalvars/_regexes.dm:8)
    private static bool IsColor(object color) => color is string text && text.Length == 7 && text[0] == '#' && System.Linq.Enumerable.All(text[1..], char.IsAsciiHexDigit);

    // findtext(color, GLOB.is_alpha_color): ^#[0-9a-fA-F]{8}$ (_globalvars/_regexes.dm:9)
    private static bool IsAlphaColor(object color) => color is string text && text.Length == 9 && text[0] == '#' && System.Linq.Enumerable.All(text[1..], char.IsAsciiHexDigit);

    // tg: code/modules/admin/greyscale_modify_menu.dm:235
    public virtual object ReadColorsFromString(object colorString = null)
    {
        //length validation
        DmList colors = splittext(colorString, "#") as DmList;
        if (length(colors) <= 1) //doesn't even begin with a # so isn't even a color
            return FALSE;
        colors.Cut(1, 2); //removes the white space as a consequence of the string beginning with a #
        if (config == null || colors.len != config.expected_colors) //not the expected length
            return FALSE;

        //value validation
        DmList new_split_colors = Dm.list();
        for (float index = 1; index <= config.expected_colors; index++)
        {
            string color = Dm.text("#[]", colors[index]);
            if (!IsColor(color) && (!Dm.truthy(unlocked) || !IsAlphaColor(color)))
                return FALSE;
            new_split_colors.Add(color);
        }
        split_colors = new_split_colors;

        //all good
        return TRUE;
    }

    // tg: code/modules/admin/greyscale_modify_menu.dm:257
    public virtual void randomize_color(float color_index)
    {
        string new_color = "#";
        for (int i = 1; i <= 3; i++)
            new_color += num2hex(rand(0, 255), 2);
        split_colors[color_index] = new_color;
    }

    // tg: code/modules/admin/greyscale_modify_menu.dm:263
    public virtual void change_config(datum_greyscale_config new_config = null)
    {
        if (config != null)
            UnregisterSignal(config, COMSIG_GREYSCALE_CONFIG_REFRESHED);
        config = new_config;
        if (config != null)
            RegisterSignal(config, COMSIG_GREYSCALE_CONFIG_REFRESHED, nameof(queue_refresh));
    }

    // tg: code/modules/admin/greyscale_modify_menu.dm:269
    [SignalHandler]
    public virtual void queue_refresh()
    {
        refreshing = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(refresh_preview)), 1 * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/admin/greyscale_modify_menu.dm", line: 272);
    }

    // tg: code/modules/admin/greyscale_modify_menu.dm:274
    public virtual void refresh_preview()
    {
        if (config == null)
            return;
        for (float i = length(split_colors) + 1; i <= config.expected_colors; i++)
            LAZYADD(ref split_colors, rgb(100, 100, 100));
        DmList used_colors = split_colors.Copy(1, config.expected_colors + 1);

        sprite_data = Dm.list();

        DmList generated_icon_states = Dm.list();
        foreach (object state in Dm.each(config.icon_states))
            generated_icon_states.Add(state); // We don't want the values from this keyed list
        sprite_data["icon_states"] = generated_icon_states;

        if (!Dm.@in(icon_state, generated_icon_states))
        {
            if (isatom(target))
            {
                atom atom_target = (atom)target;
                icon_state = atom_target.icon_state;
                if (!Dm.@in(icon_state, generated_icon_states))
                    icon_state = pick(generated_icon_states);
            }
            else
                icon_state = pick(generated_icon_states);
        }

        // DEVIATION(DV686): image(config.GenerateBundle(used_colors), icon_state=icon_state) sent with icon2html becomes the GAGS icon's
        // name and its state, drawn by the window; the full preview's steps (GenerateDebug) are not made.
        sprite_data["time_spent"] = 0;
        sprite_data["finished"] = SSgreyscale.GetColoredIconByType(config.type, used_colors);
        sprite_data["finished_icon_state"] = icon_state;
        refreshing = FALSE;
    }

    // tg: code/modules/admin/greyscale_modify_menu.dm:322
    public virtual void Unlock()
    {
        // allowed_configs = SSgreyscale.configurations: the configurations made so far (DV686: they are made on first use)
        allowed_configs = SSgreyscale.configurations;
        unlocked = TRUE;
    }

    // tg: code/modules/admin/greyscale_modify_menu.dm:326
    public virtual async DmTask DefaultApply()
    {
        atom atom_target = (atom)target;
        await atom_target.set_greyscale(split_colors, config.type);
    }

    /// Gets the top level type that first uses the configuration in this type path
    // tg: code/modules/admin/greyscale_modify_menu.dm:331
    public virtual void SetupConfigOwner()
    {
        if (!isatom(target))
            return;

        Type current = target.type;
        Type parent = Dm.parent_type(current);
        if (!Dm.truthy(Dm.initial(current, nameof(atom.greyscale_config))))
            return;
        while (Dm.eq(Dm.initial(current, nameof(atom.greyscale_config)), Dm.initial(parent, nameof(atom.greyscale_config))))
        {
            current = parent;
            parent = Dm.parent_type(current);
        }
        config_owner_type = current;
    }
}
