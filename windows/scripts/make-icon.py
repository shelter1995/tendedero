"""Reproducible vector-like application icon; no external assets or fonts."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1] / 'src/Liangtu/Assets'
root.mkdir(parents=True, exist_ok=True)

def icon(size):
    scale = 4
    im = Image.new('RGBA', (256*scale, 256*scale))
    d = ImageDraw.Draw(im)
    def box(b): return tuple(round(v*scale) for v in b)
    def line(points, fill, width): d.line([(int(x*scale),int(y*scale)) for x,y in points], fill=fill, width=width*scale, joint='curve')
    d.rounded_rectangle(box((10,10,246,246)), radius=54*scale, fill='#24272E', outline='#393D47', width=2*scale)
    rope=[(28+200*t/100,62+68*(t/100)*(1-t/100)) for t in range(101)]
    line(rope, '#AEB6C5', 5)
    # Offset back sheet and a crisp foreground frame: a hanging image stack.
    d.rounded_rectangle(box((48,96,174,214)), radius=19*scale, fill='#677184')
    d.rounded_rectangle(box((72,82,202,200)), radius=19*scale, fill='#F5F7FB')
    d.rounded_rectangle(box((87,100,187,176)), radius=9*scale, fill='#24272E')
    # One geometric diagonal, with no tiny landscape illustration at tray size.
    d.polygon([(87*scale,176*scale),(87*scale,153*scale),(120*scale,120*scale),(160*scale,160*scale),(187*scale,133*scale),(187*scale,176*scale)], fill='#96B4F6')
    d.rounded_rectangle(box((120,54,144,112)), radius=9*scale, fill='#5B86EB')
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
