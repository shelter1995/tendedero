"""Reproducible vector-like application icon; no external assets or fonts."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1] / 'src/Liangtu/Assets'
root.mkdir(parents=True, exist_ok=True)
if (root / 'IconSource' / 'app-generated.png').exists():
    # Keep the public regeneration command, but never overwrite approved AI art.
    exec((Path(__file__).with_name('package-icons.py')).read_text(encoding='utf-8'))
    raise SystemExit(0)

def icon(size):
    scale = 4
    im = Image.new('RGBA', (256*scale, 256*scale))
    d = ImageDraw.Draw(im)
    def box(b): return tuple(round(v*scale) for v in b)
    def line(points, fill, width): d.line([(int(x*scale),int(y*scale)) for x,y in points], fill=fill, width=width*scale, joint='curve')
    # Layered, inset base plate. Retain the established photo/clip silhouette;
    # a lower rim anchors it at desktop icon sizes without changing the tray.
    d.rounded_rectangle(box((10,13,246,249)), radius=54*scale, fill='#141923')
    d.rounded_rectangle(box((10,8,246,241)), radius=54*scale, fill='#424C5E')
    d.rounded_rectangle(box((12,10,244,239)), radius=52*scale, fill='#272E3B')
    d.rounded_rectangle(box((22,20,234,229)), radius=44*scale, outline='#343E50', width=1*scale)
    d.arc(box((22,149,234,231)), start=0, end=180, fill='#525F75', width=2*scale)
    rope=[(28+200*t/100,62+68*(t/100)*(1-t/100)) for t in range(101)]
    line(rope, '#AEB6C5', 5)
    # Offset back sheet and a crisp foreground frame: a hanging image stack.
    d.rounded_rectangle(box((49,99,175,217)), radius=19*scale, fill='#1D2430')
    d.rounded_rectangle(box((48,94,174,210)), radius=19*scale, fill='#69788F')
    d.rounded_rectangle(box((73,86,203,204)), radius=19*scale, fill='#171E2B')
    d.rounded_rectangle(box((72,80,202,198)), radius=19*scale, fill='#F2F5FB', outline='#FFFFFF', width=1*scale)
    d.rounded_rectangle(box((87,100,187,176)), radius=9*scale, fill='#24272E')
    # One geometric diagonal, with no tiny landscape illustration at tray size.
    d.polygon([(87*scale,176*scale),(87*scale,153*scale),(120*scale,120*scale),(160*scale,160*scale),(187*scale,133*scale),(187*scale,176*scale)], fill='#96B4F6')
    d.rounded_rectangle(box((120,54,144,112)), radius=9*scale, fill='#5B86EB')
    d.rounded_rectangle(box((123,57,141,61)), radius=2*scale, fill='#8EAFFD')
    return im.resize((size,size), Image.Resampling.LANCZOS)

def tray(size, light=False):
    scale=4
    im=Image.new('RGBA',(256*scale,256*scale)); d=ImageDraw.Draw(im)
    ink='#292D36' if light else '#ECF0F8'
    def box(b): return tuple(round(v*scale) for v in b)
    rope=[(20+216*t/100,52+72*(t/100)*(1-t/100)) for t in range(101)]
    d.line([(int(x*scale),int(y*scale)) for x,y in rope],fill=ink,width=12*scale)
    d.rounded_rectangle(box((50,86,206,216)),radius=20*scale,outline=ink,width=15*scale)
    d.rounded_rectangle(box((115,45,141,114)),radius=8*scale,fill=ink)
    d.line([(78*scale,182*scale),(114*scale,146*scale),(140*scale,172*scale),(179*scale,133*scale)],fill=ink,width=12*scale,joint='curve')
    return im.resize((size,size),Image.Resampling.LANCZOS)

icon(256).save(root/'app.png')
icon(256).save(root/'app.ico', sizes=[(n,n) for n in (16,20,24,32,40,48,64,128,256)])
for light in (False,True):
    tray(256,light).save(root/('tray-light.ico' if light else 'tray-dark.ico'),sizes=[(n,n) for n in (16,20,24,32,40,48,64)])
