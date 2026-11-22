# Suit storage unit (classic) v3

Blender MCP asset, metres, Godot +Z front (the door). Flat-coloured materials, no tg atlas. One GLB per state of tg's
suit_storage_unit.dm overlays: open, maintenance (panel), uv, uvstrong, locked, closed. The catalog draws the one whose
overlay the unit shows (`tools/art/catalog_wave_specs.py`, entry `suit_storage`).

`suit_storage_classic_v3.blend` is the source. It was built by the scripts in `tools/art/suit_storage_classic/`, run through
Blender MCP in this order: build_suit_storage, refine_suit_storage, fix_controls_front, refine_controls_scale; the GLBs are
exported from the scene per state. `topology_report.json` is the v1 topology audit.
