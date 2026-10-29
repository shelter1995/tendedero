"""Flat code-native tray glyph: optical geometry on a 16px grid, two themes."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1] / 'src/Liangtu/Assets'
def draw_tray(light, size=256):
    im = Image.new('RGBA', (size, size)); draw = ImageDraw.Draw(im)
    unit = size / 16
    def box(values): return tuple(v * unit for v in values)
    ink = '#234862' if light else '#F4FAFF'
    coral = '#DB674A' if light else '#FF9778'
    cyan = '#087E99' if light else '#59DCEC'
    rope = [(x * unit, (3.3 + 1.3 * (1 - ((x - 8) / 7) ** 2)) * unit) for x in [1 + n / 16 for n in range(225)]]
    draw.line(rope, fill=ink, width=round(1.5 * unit))
    draw.rounded_rectangle(box((2.8, 5, 13.2, 14.8)), radius=1.7 * unit, outline=ink, width=round(1.7 * unit))
    draw.polygon([tuple(v * unit for v in p) for p in [(4.6,12.8),(7,9.3),(9,11.3),(10.8,9.9),(11.4,12.8)]], fill=cyan)
    # Front blade overlaps a darker rear blade in the same oblique view as app.png.
    rear = '#B64E38' if light else '#D3694C'
    draw.polygon([tuple(v*unit for v in p) for p in [(8.0,.7),(10.2,1.1),(9.3,6.6),(7.4,6.3)]], fill=rear)
    draw.polygon([tuple(v*unit for v in p) for p in [(7.2,.6),(9.2,.9),(8.2,6.5),(6.2,6.2)]], fill=coral)
    draw.ellipse(box((8.2,2.7,9.35,4.2)), fill=ink)
    draw.ellipse(box((8.55,3.1,9.0,3.8)), fill=rear)
    return im
for light in (True, False):
    name = 'tray-light' if light else 'tray-dark'
    icon = draw_tray(light)
    icon.save(root / (name + '.png'))
    if not light: icon.save(root / 'tray.png')
    icon.save(root / (name + '.ico'), sizes=[(n, n) for n in (16,20,24,32,40,48,64)])

# QA at actual tray size, followed by a nearest-neighbor zoom for pixel inspection.
sheet = Image.new('RGB', (560, 160), '#E8EDF2'); draw = ImageDraw.Draw(sheet)
draw.rectangle((280, 0, 560, 160), fill='#242831')
for column, light in enumerate((True, False)):
    for index, size in enumerate((16,20,24,32)):
        icon = draw_tray(light).resize((size,size), Image.Resampling.LANCZOS)
        sheet.paste(icon, (column*280 + 22 + index*62, 22), icon)
        zoom = icon.resize((size*2,size*2), Image.Resampling.NEAREST)
        sheet.paste(zoom, (column*280 + 14 + index*62, 80), zoom)
sheet.save(root / 'IconSource/tray-size-preview.png')
