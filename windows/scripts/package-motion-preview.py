"""Encode captured WPF UI frames for review; no generated/retouched artwork."""
from pathlib import Path
from PIL import Image
import sys

folder = Path(sys.argv[1])
files = sorted((folder / 'arrival-frames').glob('*.png'))
frames = []
durations = []
for i, file in enumerate(files):
    source = Image.open(file).convert('RGBA')
    frame = Image.new('RGBA', source.size, '#263340')
    frame.alpha_composite(source)
    frame = frame.convert('RGB').resize((1280, 360), Image.Resampling.LANCZOS)
    frames.append(frame)
    duration = (files[i+1].stat().st_mtime - file.stat().st_mtime) * 1000 if i+1 < len(files) else 900
    durations.append(max(40, min(1000, round(duration))))
frames[0].save(folder / 'arrival-preview.gif', save_all=True, append_images=frames[1:], duration=durations, loop=0, optimize=False)
print(folder / 'arrival-preview.gif')
