"""Original atlas inventory for the remaining station props."""
import sys
sys.dont_write_bytecode = True
from prepare_computers_construction import prepare, ROOT, REVIEW
OUT=ROOT/'godot/assets/models/station_props/station_extras'
sources={
 'suit':('icons/obj/machines/suit_storage.dmi',['industrial']),
 'drone':('icons/obj/machines/drone_dispenser.dmi',['']),
 'gate':('icons/obj/machines/scangate.dmi',['']),
 'bomb':('icons/obj/devices/assemblies.dmi',['training-bomb']),
 'chair':('icons/obj/machines/implant_chair.dmi',['implantchair']),
 'wall':('icons/obj/machines/wallmounts.dmi',['gulag']),
 'kitchen':('icons/obj/machines/kitchen.dmi',['grinder']),
 'hoop':('icons/obj/fluff/basketball_hoop.dmi',['hoop']),
 'mannequin':('icons/mob/human/mannequin.dmi',['mannequin_skeleton']),
 'statue':('icons/obj/art/statue.dmi',['qm','snowman']),
 'cloning':('icons/obj/machines/cloning.dmi',['pod_0']),
 'sleeper':('icons/obj/machines/sleeper.dmi',['sleeper-open']),
 'structures':('icons/obj/structures.dmi',['barrel']),
 'spike':('icons/obj/service/kitchen.dmi',['spike']),
 'mecha':('icons/mob/rideables/mecha.dmi',['ripley-broken']),
 'research':('icons/obj/machines/research.dmi',['tdoppler','ecto_sniffer']),
 'exploration':('icons/obj/exploration.dmi',['scanner']),
 'fishing':('icons/obj/fishing.dmi',['portal']),
 'camera':('icons/obj/machines/stationary_camera.dmi',['camera']),
 'portal':('icons/obj/machines/restaurant_portal.dmi',['portal']),
 'tv':('icons/obj/machines/computer.dmi',['television']),
}
if __name__=='__main__':prepare(sources,OUT,REVIEW.parent/'station_art_extras')
