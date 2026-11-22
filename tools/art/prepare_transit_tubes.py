from pathlib import Path
from PIL import Image,ImageDraw
import re,json
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/transit_tubes'
(OUT/'textures').mkdir(parents=True,exist_ok=True)
sources={'transit':('icons/obj/pipes_n_cables/transit_tube.dmi',[''])}
code=(ROOT/'tools/art/prepare_furniture_wave.py').read_text().split('meta={};refs=[]')[1]
exec('meta={};refs=[]'+code.replace('furniture_wave_reference_','transit_tubes_reference_'))
