using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// D290 (DV222): the own mob's burning, heat, cold, toxic gas, radiation (a film's grain), electric shock and hits drawn over the 3D view (under the HUD),
/// each easing in and out, all of them at most as strong as the fullscreen strength option (D70).
/// </summary>
public partial class ScreenEffectsOverlay : ColorRect
{
    private const string ShaderCode = @"
shader_type canvas_item;
uniform float burning; uniform float heat; uniform float cold; uniform float toxic;
uniform float radiation; uniform float shock; uniform float hit; uniform float strength = 1.0;

float hash(vec2 p) { // a sin-free hash: sin's large arguments lose precision on GPUs and the noise turns faceted
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}
float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

float fbm(vec2 p) {
    float v = 0.0, a = 0.5;
    mat2 turn = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)); // each octave turned, so the value noise's grid does not show
    for (int i = 0; i < 4; i++) { v += a * noise(p); p = turn * p * 2.03; a *= 0.5; }
    return v;
}

void fragment() {
    vec2 c = UV - 0.5;
    float edge = smoothstep(0.25, 0.75, length(c * vec2(1.0, 1.25)) * 1.25); // 0 in the middle, 1 at the corners
    vec3 sum = vec3(0.0); float keep = 1.0;
    // flames lick up from the bottom and the sides
    float flame = fbm(vec2(UV.x * 7.0, UV.y * 4.0 + TIME * 1.6));
    float reach = clamp(edge * 1.1 + smoothstep(0.6, 1.0, UV.y) * 0.6, 0.0, 1.0);
    float a = burning * (smoothstep(0.55, 1.0, reach * (0.5 + 0.9 * flame)) * 0.6 + edge * 0.12);
    sum += vec3(1.0, mix(0.2, 0.7, flame), 0.05) * a; keep *= 1.0 - a;
    a = heat * edge * 0.45 * (0.8 + 0.2 * sin(TIME * 3.0));
    sum += vec3(1.0, 0.45, 0.15) * a; keep *= 1.0 - a;
    a = cold * edge * (0.3 + 0.4 * fbm(UV * 12.0));
    sum += vec3(0.7, 0.88, 1.0) * a; keep *= 1.0 - a;
    a = toxic * edge * (0.3 + 0.1 * sin(TIME * 2.2)) * (0.6 + 0.4 * fbm(UV * 4.0 + TIME * 0.2));
    sum += vec3(0.35, 0.85, 0.15) * a; keep *= 1.0 - a;
    // radiation (the owner, 2026-10-30): faint grey grain, new every frame, and a few white sparks, as on a camera filming radiation
    vec2 tick = mod(floor(TIME * 30.0), 997.0) * vec2(13.7, 71.3); // kept small: the hash loses its precision on large numbers
    float grain = hash(floor(FRAGCOORD.xy) + tick);
    a = radiation * 0.08;
    sum += vec3(grain) * a; keep *= 1.0 - a;
    float spark = step(0.9993, hash(floor(FRAGCOORD.xy / 2.0) + tick * 0.37));
    a = radiation * spark * 0.5;
    sum += vec3(1.0) * a; keep *= 1.0 - a;
    // the shock: a blue-white flicker, brighter at the edges
    float flick = step(0.45, hash(vec2(floor(TIME * 18.0), 3.0)));
    a = shock * (0.12 + edge * 0.45) * (0.4 + 0.6 * flick);
    sum += vec3(0.75, 0.85, 1.0) * a; keep *= 1.0 - a;
    a = hit * edge * 0.75;
    sum += vec3(0.75, 0.0, 0.0) * a; keep *= 1.0 - a;
    float alpha = (1.0 - keep) * strength;
    COLOR = vec4(alpha > 0.0001 ? sum / max(1.0 - keep, 0.0001) : vec3(0.0), alpha);
}
";

    private ShaderMaterial _material;
    private float _burning, _heat, _cold, _toxic, _radiation, _shock, _hit;

    public override void _Ready()
    {
        Name = "ScreenEffects";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        Material = _material;
        Visible = false;
    }

    /// <summary>The health doll showed more damage: a red flash at the edges that fades in about half a second.</summary>
    public void Hit() => _hit = 1f;

    /// <summary>Each frame: eases the effects toward the replica's (<see cref="ScreenEffects.Read"/>).</summary>
    public void Update(ReplicaWorld replica, float strength, float delta)
    {
        ScreenEffectLevels target = replica != null ? ScreenEffects.Read(replica) : default;
        float k = 1f - MathF.Exp(-delta * 4f);
        _burning += (target.Burning - _burning) * k;
        _heat += (target.Heat - _heat) * k;
        _cold += (target.Cold - _cold) * k;
        _toxic += (target.Toxic - _toxic) * k;
        _radiation += (target.Radiation - _radiation) * k;
        _shock += (target.Shock - _shock) * (1f - MathF.Exp(-delta * 20f)); // tg's shock lasts a moment: in and out quickly
        _hit = Math.Max(0f, _hit - delta * 2f);
        float most = Math.Max(Math.Max(Math.Max(_burning, _heat), Math.Max(_cold, _toxic)), Math.Max(Math.Max(_radiation, _shock), _hit));
        Visible = strength > 0.001f && most > 0.005f;
        if (!Visible)
            return;
        _material.SetShaderParameter("burning", _burning);
        _material.SetShaderParameter("heat", _heat);
        _material.SetShaderParameter("cold", _cold);
        _material.SetShaderParameter("toxic", _toxic);
        _material.SetShaderParameter("radiation", _radiation);
        _material.SetShaderParameter("shock", _shock);
        _material.SetShaderParameter("hit", _hit);
        _material.SetShaderParameter("strength", strength);
    }
}
