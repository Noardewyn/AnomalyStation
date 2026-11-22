# SMES

Flat-colour model with a shared service-door hinge, internal cells, bus rails and independent display parts. Source: tgstation `code/modules/power/smes.dm` and the port in `src/Core/Tg/modules/power/smes.cs`.

The asset contains 552 Blender vertices, 882 triangles, five authoring animation clips and three punctual lights. Its native dimensions are 0.98 × 0.98 × 0.973 m, with its origin on the floor and its front along Godot +Z. The runtime catalog uses a uniform scale of 1.6. The GLB omits normals; the reader generates the flat face normals required by glTF. `flatColors` preserves the authored palette and face shading.

## Runtime states

- `smes`: closed service panel.
- `smes-o`: the door and its controls swing together by 112 degrees over 0.5 seconds; internal components become visible. No active display parts or light remain because the source sends no display overlays.
- `smes-og1`: the bottom red segment blinks for 1 second on, 1 second off. `smes-og2` through `smes-og5` show two through five segments steadily: red, orange, yellow, lime and green, sampled from the original DMI's gauge face pixels.
- `smes-op1` and `smes-op0`: independent green/red output highlights.
- `smes-oc1`: red lower input pulse; `smes-oc0`: green upper and red lower pulses. Both follow the source's 0.5 second on/off timing.
- Broken SMES or `show_display_lights=0`: the source sends no display overlays; the housing and passive lenses remain, with all active segments and local lights off.

All subtypes share one catalog entry and mesh family. Charge, input and output are selected from replicated appearance overlays rather than inferred from switches or client-side charge calculations. `chargedisplay()` and the actual `inputting`/`outputting` fields remain server mechanics.

`pivot` parts share the exported Service_Door_Hinge transform. The client uses its existing hinge animation system and part blink timelines instead of playing competing imported GLB clips. Local lights follow the corresponding parts, blink with them and are removed when their source overlays disappear. They also follow movement and are cleaned up on rebuild/despawn. Passive lenses emit no light.

Inactive control lenses retain dark green (`#005200`) or dark red (`#520000`). Inactive charge segments retain the dim gauge colours from the base 2D sprite. Active control lenses and frames use red/green emission, while charge segments use the five-colour source palette. A steady output signal keeps its lens lit while independent input pulses blink around it. Pulse-only lenses retain their dark material during blink pauses, with no emission or local light. Every local light inherits its indicator's runtime colour. These runtime overrides also replace the GLB's original white input pulses. The neighbouring power terminal is a separate entity.

## Source charge thresholds

`chargedisplay()` is `clamp(floor(5 * total_charge / total_capacity), 0, 5)`: DM's one-argument `round` rounds down. The server already supplies the resulting `smes-ogN` overlay; the renderer does not recompute charge.

| Charge | Active segments | Animation |
|---|---|---|
| 0 to less than 20% | none | dim source colours only |
| 20 to less than 40% | red | 1 second on / 1 second off |
| 40 to less than 60% | red + orange | steady |
| 60 to less than 80% | + yellow | steady |
| 80 to less than 100% | + lime | steady |
| 100% | + green | steady |

Charging does not animate a travelling gauge segment in the source. Its separate input indicators pulse every 0.5 seconds. `source_states.json` records the original gauge palette, inactive colours, frame counts and delays; `audit_smes_source.py --metadata-only` verifies these without regenerating the reference image.

## Files and verification

- `smes.glb`: runtime geometry and materials.
- `smes.blend`: editable asset and authoring preview.
- `model_info.json`, `source_states.json`, `mesh_validation.json`, `export_validation.json`: source/export reports.
- `tools/art/build_smes_concept.py`, `tools/art/audit_smes_source.py`: authoring tools. Rebuilding the asset must preserve its runtime node names and hinge.

Verified with state-combination tests, shared-pivot and flat-normal checks, and the real Godot ModelShowcase in darkness: low/full charge, input/output combinations, opening/closing and disabled display. Repeated changes retain the body transform and switch the indicators and their local light together.
