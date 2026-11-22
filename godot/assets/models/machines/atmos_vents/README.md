# Vent pumps and scrubbers

Model batch for MetaStation floor vents: six vent-pump variants and five scrubber variants. Each variant is a single thin, closed sprite slab cut along the sprite's opaque pixel silhouette (pumps: a square with 1-pixel corner notches, 24 vertices; scrubbers: the stepped round housing, 88 vertices), following the simple floor-housing approach used by the holopad. The slab measures 0.534375 x 0.534375 m, and is 0.055 m tall. It has no grille bars, fan/rotor, indicators, pipe cap, overlays, or other mechanical geometry.

The top surface uses the existing `unary_devices.png` atlas unchanged, with each top pixel mapped to its own source pixel and nearest filtering. The material carries sprite alpha. Sides and bottom sample one dark metal rim pixel inside the same 32 x 32 frame, so every UV lies in one frame and the client draws tg's live animated picture on top. Directional variants reuse the source's first sprite direction; rotate the model around Z for other cardinal directions.

Pump states: off, out, in, welded, overclocked, broken. Scrubber states: off, on, wide, purge, welded. The overclocked and broken pump presets use the existing `vent_out` base sprite; separate fan-status overlays were removed with the multi-part geometry so each model remains one sprite mesh.

No source sprites, game integration, collision, or animation were changed.
