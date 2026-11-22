"""Compatibility entry point for the original-atlas locker builder."""
from pathlib import Path
p = Path(__file__).with_name('build_atlas_lockers.py')
exec(compile(p.read_text(encoding='utf-8-sig'), str(p), 'exec'), {'__file__': str(p)})
