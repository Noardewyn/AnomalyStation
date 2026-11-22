# Fabrication machines

Six base models, GLB and Blender sources.
Grid 2 m, front Godot +Z, heights below 2 m. Original atlas pixels preserved;
nearest sampling, no repainted textures. `models.json` contains triangle counts.

`protolathe_techfab` is shared by protolathes and techfabs. The separate original
Department_Stripe material can be tinted: engineering #EFB341, service #83CA41,
medical #52B4E9, cargo #956929, science #D381C9, security #DE3A3A.
Enable department_stripe or no_stripe independently of work animations.

AnimationPlayer clips: maintenance_open/close; working/idle on printers;
scan/print/idle on module duplicator. Frame timings come from the original DMI.
Fabricator hood motion is a 3D interpretation of the production cycle.
Blender NLA tracks are muted for the neutral saved pose; unmute a track to preview.

Source audit: tgstation code/game/machinery/autolathe.dm;
code/modules/research/machinery/{_production,protolathe,techfab,
departmental_techfab,circuit_imprinter}.dm;
code/modules/vehicles/mecha/mech_fabricator.dm;
code/modules/wiremod/core/component_printer.dm.

Scope limitations: material-specific loading effects retained in source_states.json
but not yet exported as clips. Runtime state dispatch, item spawning and
catalog registration are not implemented. These are base assets, not complete
gameplay devices. Product/Input anchors define attachment positions.

Service revision (2026-11-04): six real 160 mm deep service recesses with finished walls, original open-state circuit artwork and shallow raised circuit details. Housings are continuous closed surfaces; original maintenance and production clips are preserved. Rear and side panels use restrained original-palette metal instead of stretched front details. Closed poses passed manifold/degenerate-face and coplanar visible-overlap checks; open hatches and rear/side views reviewed in Blender.

Autolathe surface revision (2026-11-06): side and rear panel borders and square fasteners now use original TG atlas pixels. Blender and GLB reference assets/tg/icons/obj/machines/lathes.png directly. Housing closure and coplanar overlap checks passed; front, rear, both sides, top and open service hatch reviewed in Blender. Existing four animation clips preserved. Backup and review renders are outside the repository in AnomalyStation3D_offrepo.

Protolathe/techfab surface revision (2026-11-06): side panels now have borders, square fasteners and two restrained vent marks; rear has paired access panels. Uses canonical assets/tg/icons/obj/machines/research.png directly, nearest sampling. Shared departmental geometry and tintable stripe retained. Front/rear/both sides/top and open service hatch reviewed; closed, halfway and open poses passed closure and coplanar overlap audit. Backups and renders reside offrepo.

Component printer surface revision (2026-11-06): side and rear panel borders, fasteners and restrained vent marks use original atlas samples. Direct reference to TG wiremod_fab.png replaces the pixel-identical local copy. Hood and service hatch remain separate; all four clips retained. Front, rear, both sides, top, maintenance-open and working poses reviewed in Blender. Three maintenance and three working samples had no coplanar overlap; physical parts are closed, status-light planes are intentional overlays. Backup and audits stored offrepo.
