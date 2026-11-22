// Translated from tgstation (snapshot 1d99bd7f): code/modules/lighting/lighting_atom.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P3. The light vars and their setters are translated: they are state that other tg code and the replicator read
// (presentation-seam.md section 5). M7 lumcount: update_light creates, updates and deletes the server's
// /datum/light_source (tg's lumcount for gameplay), and beside it makes the presentation call IAppearanceSink.LightChanged;
// the client still computes the light it draws from the replicated vars (D24, docs/ai/specs/light-grid.md).

// tg: code/modules/lighting/lighting_atom.dm:3
public partial class atom : datum
{
    // The proc you should always use to set the light of this atom.
    // tg: code/modules/lighting/lighting_atom.dm:3
    // Appendix A (M3 P3, decided): l_color is object, not slots.tsv's float? (its default NONSENSICAL_VALUE is a number, but
    // every caller passes a colour text, a "-" cell of rules 3.5).
    public virtual async DmTask set_light(object l_range = null, object l_power = null, object l_color = null, object l_angle = null, object l_dir = null, object l_height = null, object l_on = null)
    {
        l_color ??= NONSENSICAL_VALUE;
        // We null everything but l_dir, because we don't want to allow for modifications while frozen
        if (Dm.truthy(Dm.bitand(light_flags, LIGHT_FROZEN)))
        {
            l_range = null;
            l_power = null;
            l_color = null;
            l_on = null;
            l_angle = null;
            l_height = null;
        }

        if (Dm.gt(l_range, 0) && Dm.lt(l_range, MINIMUM_USEFUL_LIGHT_RANGE))
            l_range = MINIMUM_USEFUL_LIGHT_RANGE; //Brings the range up to 1.4, which is just barely brighter than the soft lighting that surrounds players.

        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SET_LIGHT]) ? NONE : this._SendSignal(COMSIG_ATOM_SET_LIGHT, Dm.list(this, l_range, l_power, l_color, l_on))), COMPONENT_BLOCK_LIGHT_UPDATE)))
            return;

        if (l_power != null)
            set_light_power(l_power);

        if (l_range != null)
            set_light_range(l_range);

        if (!Dm.eq(l_color, NONSENSICAL_VALUE))
            set_light_color(l_color);

        if (l_angle != null)
            set_light_angle(l_angle);

        if (l_dir != null)
            set_light_dir(l_dir);

        if (l_on != null)
            await set_light_on(l_on);

        if (l_height != null)
            set_light_height(l_height);

        update_light();
    }

    /// Will update the light (duh).
    /// Creates or destroys it if needed, makes it update values, makes sure it's got the correct source turf...
    // tg: code/modules/lighting/lighting_atom.dm:44
    [ShouldNotSleep]
    public virtual object update_light()
    {
        object dot = null;
        if (light_system != COMPLEX_LIGHT)
            throw Dm.CRASH(Dm.text("update_light() for [] with following light_system value: []", this, light_system), "code/modules/lighting/lighting_atom.dm", 48);

        // the replication hint (the owner, 2026-10-26: an APC's or a socket's light came on about two seconds late, when the
        // replicator's sweep came round): a movable's light change goes out the tick it happens
        if (this is atom_movable && dm_id != 0 && World.CurrentOrNull is { } hinted)
            PresentationSinks.Of(hinted).Replication?.Moved(this);

        if (!Dm.truthy(light_power) || !Dm.truthy(light_range) || !Dm.truthy(light_on)) // We won't emit light anyways, destroy the light source.
        {
            QDEL_NULL(ref light);
            // The client drops the light (presentation-seam.md 5, D24)
            light_changed();
        }
        else
        {
            if (!ismovable(loc)) // We choose what atom should be the top atom of the light here.
                dot = this;
            else
                dot = loc;

            if (light != null) // Update the light or create it if it does not exist.
                light.update(dot as atom);
            else
                light = Dm.@new<datum_light_source>(this, dot);
            // The client draws the light from the replicated light vars (presentation-seam.md 5, D24)
            light_changed();
            return dot;
        }
        return dot;
    }

    // update_light's presentation call (presentation-seam.md section 5: tg's update_light() becomes LightChanged): the light
    // vars of a registered atom changed. Not a tg proc; it stands where the lighting module creates, updates or deletes the
    // light source.
    private void light_changed()
    {
        if (!PresentationSinks.Reportable(this))
            return;
        DmWorldState.Current.Sinks.Appearance.LightChanged(this);
    }

    /**
     * Updates the atom's opacity value.
     *
     * This exists to act as a hook for associated behavior.
     * It notifies (potentially) affected light sources so they can update (if needed).
     */
    // tg: code/modules/lighting/lighting_atom.dm:70
    public virtual async DmTask<object> set_opacity(object new_opacity = null)
    {
        object dot = null;
        if (Dm.eq(new_opacity, opacity) || Dm.truthy(Dm.bitand(light_flags, LIGHT_FROZEN)))
            return dot;
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SET_OPACITY]))
            this._SendSignal(COMSIG_ATOM_SET_OPACITY, Dm.list(this, new_opacity));
        dot = opacity;
        opacity = Dm.num(new_opacity);
        return dot;
    }
}

// tg: code/modules/lighting/lighting_atom.dm:78
public partial class atom_movable : atom
{
    // tg: code/modules/lighting/lighting_atom.dm:78
    public override async DmTask<object> set_opacity(object new_opacity = null)
    {
        object dot = null;
        dot = await base.set_opacity(new_opacity);
        if (dot == null || !isturf(loc))
            return dot;

        if (Dm.truthy(opacity))
            await AddElement(typeof(datum_element_light_blocking));
        else
            RemoveElement(typeof(datum_element_light_blocking));
        // Change in opacity could change camera visibility
        Dm.Deferred("code/modules/lighting/lighting_atom.dm:88", "set_opacity: SScameras.update_visibility(src) (security cameras and their visibility are translated in M10+)");
        return dot;
    }
}

// tg: code/modules/lighting/lighting_atom.dm:90
public partial class turf : atom
{
    // tg: code/modules/lighting/lighting_atom.dm:90
    public override async DmTask<object> set_opacity(object new_opacity = null)
    {
        object dot = null;
        dot = await base.set_opacity(new_opacity);
        if (dot == null)
            return dot;
        recalculate_directional_opacity();
        return dot;
    }
}

// tg: code/modules/lighting/lighting_atom.dm:96
public partial class atom : datum
{
    // tg: code/modules/lighting/lighting_atom.dm:96
    public virtual object flash_lighting_fx(float? range = null, float? power = null, string color = null, float? duration = null, Type light_type = null)
    {
        range ??= FLASH_LIGHT_RANGE;
        power ??= FLASH_LIGHT_POWER;
        color ??= COLOR_WHITE;
        duration ??= FLASH_LIGHT_DURATION;
        light_type ??= typeof(obj_effect_dummy_lighting_obj);
        if (!Dm.truthy(duration))
            stack_trace(Dm.text("Lighting FX obj created on \\[[]\\] without a duration", type), "code/modules/lighting/lighting_atom.dm", 98);
        obj_effect_dummy light_obj = Dm.@new(light_type, get_turf(this), range, power, color, duration) as obj_effect_dummy;
        return light_obj;
    }
}

// tg: code/modules/lighting/lighting_atom.dm:102
public partial class mob_living : mob
{
    // tg: code/modules/lighting/lighting_atom.dm:102
    public override object flash_lighting_fx(float? range = null, float? power = null, string color = null, float? duration = null, Type light_type = null)
    {
        range ??= FLASH_LIGHT_RANGE;
        power ??= FLASH_LIGHT_POWER;
        color ??= COLOR_WHITE;
        duration ??= FLASH_LIGHT_DURATION;
        light_type ??= typeof(obj_effect_dummy_lighting_obj_moblight);
        return mob_light(range, power, color, duration);
    }

    // tg: code/modules/lighting/lighting_atom.dm:105
    public virtual object mob_light(object range = null, object power = null, object color = null, object duration = null, Type light_type = null)
    {
        light_type ??= typeof(obj_effect_dummy_lighting_obj_moblight);
        obj_effect_dummy_lighting_obj_moblight mob_light_obj = Dm.@new(light_type, this, range, power, color, duration) as obj_effect_dummy_lighting_obj_moblight;
        return mob_light_obj;
    }
}

// tg: code/modules/lighting/lighting_atom.dm:110
public partial class atom : datum
{
    /// Setter for the light power of this atom.
    // tg: code/modules/lighting/lighting_atom.dm:110
    public virtual object set_light_power(object new_power = null)
    {
        object dot = null;
        if (Dm.eq(new_power, light_power) || Dm.truthy(Dm.bitand(light_flags, LIGHT_FROZEN)))
            return dot;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SET_LIGHT_POWER]) ? NONE : this._SendSignal(COMSIG_ATOM_SET_LIGHT_POWER, Dm.list(this, new_power))), COMPONENT_BLOCK_LIGHT_UPDATE)))
            return dot;
        dot = light_power;
        light_power = Dm.num(new_power);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_LIGHT_POWER]))
            this._SendSignal(COMSIG_ATOM_UPDATE_LIGHT_POWER, Dm.list(this, dot));
        return dot;
    }

    /// Setter for the light range of this atom.
    // tg: code/modules/lighting/lighting_atom.dm:121
    public virtual object set_light_range(object new_range = null)
    {
        object dot = null;
        if (Dm.eq(new_range, light_range) || Dm.truthy(Dm.bitand(light_flags, LIGHT_FROZEN)))
            return dot;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SET_LIGHT_RANGE]) ? NONE : this._SendSignal(COMSIG_ATOM_SET_LIGHT_RANGE, Dm.list(this, new_range))), COMPONENT_BLOCK_LIGHT_UPDATE)))
            return dot;
        dot = light_range;
        light_range = Dm.num(new_range);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_LIGHT_RANGE]))
            this._SendSignal(COMSIG_ATOM_UPDATE_LIGHT_RANGE, Dm.list(this, dot));
        return dot;
    }

    /// Setter for the light color of this atom.
    // tg: code/modules/lighting/lighting_atom.dm:132
    public virtual object set_light_color(object new_color = null)
    {
        object dot = null;
        if (Dm.eq(new_color, light_color) || Dm.truthy(Dm.bitand(light_flags, LIGHT_FROZEN)))
            return dot;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SET_LIGHT_COLOR]) ? NONE : this._SendSignal(COMSIG_ATOM_SET_LIGHT_COLOR, Dm.list(this, new_color))), COMPONENT_BLOCK_LIGHT_UPDATE)))
            return dot;
        dot = light_color;
        light_color = (string)new_color;
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_LIGHT_COLOR]))
            this._SendSignal(COMSIG_ATOM_UPDATE_LIGHT_COLOR, Dm.list(this, dot));
        return dot;
    }

    /// Setter for whether or not this atom's light is on.
    // tg: code/modules/lighting/lighting_atom.dm:143
    public virtual async DmTask<object> set_light_on(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(new_value, light_on) || Dm.truthy(Dm.bitand(light_flags, LIGHT_FROZEN)))
            return dot;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SET_LIGHT_ON]) ? NONE : this._SendSignal(COMSIG_ATOM_SET_LIGHT_ON, Dm.list(this, new_value))), COMPONENT_BLOCK_LIGHT_UPDATE)))
            return dot;
        dot = light_on;
        light_on = Dm.num(new_value);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_LIGHT_ON]))
            this._SendSignal(COMSIG_ATOM_UPDATE_LIGHT_ON, Dm.list(this, dot));
        return dot;
    }

    /// Setter for the light flags of this atom.
    // tg: code/modules/lighting/lighting_atom.dm:154
    public virtual object set_light_flags(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(new_value, light_flags) || (Dm.truthy(Dm.bitand(light_flags, LIGHT_FROZEN)) && Dm.truthy(Dm.bitand(new_value, LIGHT_FROZEN))))
            return dot;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SET_LIGHT_FLAGS]) ? NONE : this._SendSignal(COMSIG_ATOM_SET_LIGHT_FLAGS, Dm.list(this, new_value))), COMPONENT_BLOCK_LIGHT_UPDATE)))
            return dot;
        dot = light_flags;
        light_flags = Dm.num(new_value);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_LIGHT_FLAGS]))
            this._SendSignal(COMSIG_ATOM_UPDATE_LIGHT_FLAGS, Dm.list(this, dot));
        return dot;
    }

    // procs that only apply to OVERLAY_LIGHT

    /// Setter for an optional render_source to apply to this atom's light overlay
    // tg: code/modules/lighting/lighting_atom.dm:167
    public virtual object set_light_render_source(object new_source = null)
    {
        object dot = null;
        if (Dm.eq(new_source, light_flags) || Dm.truthy(Dm.bitand(light_flags, LIGHT_FROZEN)))
            return dot;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SET_LIGHT_RENDER_SOURCE]) ? NONE : this._SendSignal(COMSIG_ATOM_SET_LIGHT_RENDER_SOURCE, Dm.list(this, new_source))), COMPONENT_BLOCK_LIGHT_UPDATE)))
            return dot;
        dot = light_render_source;
        light_render_source = (string)new_source;
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_LIGHT_RENDER_SOURCE]))
            this._SendSignal(COMSIG_ATOM_UPDATE_LIGHT_RENDER_SOURCE, Dm.list(this, dot));
        return dot;
    }

    // procs that only apply to COMPLEX_LIGHT

    /// Setter for the light angle of this atom
    // tg: code/modules/lighting/lighting_atom.dm:180
    public virtual object set_light_angle(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(new_value, light_angle) || Dm.truthy(Dm.bitand(light_flags, LIGHT_FROZEN)))
            return dot;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SET_LIGHT_ANGLE]) ? NONE : this._SendSignal(COMSIG_ATOM_SET_LIGHT_ANGLE, Dm.list(this, new_value))), COMPONENT_BLOCK_LIGHT_UPDATE)))
            return dot;
        dot = light_angle;
        light_angle = Dm.num(new_value);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_LIGHT_ANGLE]))
            this._SendSignal(COMSIG_ATOM_UPDATE_LIGHT_ANGLE, Dm.list(this, dot));
        return dot;
    }

    /// Setter for the light direction of this atom
    // tg: code/modules/lighting/lighting_atom.dm:191
    public virtual object set_light_dir(object new_value = null)
    {
        object dot = null;
        // No frozen check here because we allow direction changes in a freeze
        if (Dm.eq(new_value, light_dir))
            return dot;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SET_LIGHT_DIR]) ? NONE : this._SendSignal(COMSIG_ATOM_SET_LIGHT_DIR, Dm.list(this, new_value))), COMPONENT_BLOCK_LIGHT_UPDATE)))
            return dot;
        dot = light_dir;
        light_dir = Dm.num(new_value);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_LIGHT_DIR]))
            this._SendSignal(COMSIG_ATOM_UPDATE_LIGHT_DIR, Dm.list(this, dot));
        return dot;
    }

    /// Setter for the height of our light
    // tg: code/modules/lighting/lighting_atom.dm:203
    public virtual object set_light_height(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(new_value, light_height) || Dm.truthy(Dm.bitand(light_flags, LIGHT_FROZEN)))
            return dot;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_SET_LIGHT_HEIGHT]) ? NONE : this._SendSignal(COMSIG_ATOM_SET_LIGHT_HEIGHT, Dm.list(this, new_value))), COMPONENT_BLOCK_LIGHT_UPDATE)))
            return dot;
        dot = light_height;
        light_height = Dm.num(new_value);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UPDATE_LIGHT_HEIGHT]))
            this._SendSignal(COMSIG_ATOM_UPDATE_LIGHT_HEIGHT, Dm.list(this, dot));
        return dot;
    }

    // tg: code/modules/lighting/lighting_atom.dm:214
    public virtual object get_light_offset()
    {
        return Dm.list(0, 0);
    }
}

// tg: code/modules/lighting/lighting_atom.dm:218  (the global procs, rules 7.8)
public static partial class GlobalProcs
{
    /// Returns a list of x and y offsets to apply to our visual lighting position
    // tg: code/modules/lighting/lighting_atom.dm:218
    public static object calculate_light_offset(atom get_offset = null)
    {
        DmList hand_back;
        if (!Dm.truthy(Dm.bitand(get_offset.light_flags, LIGHT_IGNORE_OFFSET)))
        {
            hand_back = get_visual_offset(get_offset) as DmList;
            hand_back[1] = -Dm.num(hand_back[1]) / ICON_SIZE_X;
            hand_back[2] = -Dm.num(hand_back[2]) / ICON_SIZE_Y;
        }
        else
            hand_back = Dm.list(0f, 0f);

        DmList atoms_opinion = get_offset.get_light_offset() as DmList;
        hand_back[1] = Dm.add(hand_back[1], atoms_opinion[1]);
        hand_back[2] = Dm.add(hand_back[2], atoms_opinion[2]);
        return hand_back;
    }
}
