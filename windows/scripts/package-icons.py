"""Encode imagegen artwork as multi-resolution Windows icons (no repainting)."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1] / 'src/Liangtu/Assets'
for name, targets, sizes in [
    ('app-generated.png', ['app.ico'], (16, 20, 24, 32, 40, 48, 64, 128, 256)),
]:
    source = Image.open(root / 'IconSource' / name).convert('RGBA')
    source = source.resize((256, 256), Image.Resampling.LANCZOS)
    for target in targets:
        source.save(root / target, sizes=[(n, n) for n in sizes])
    source.save(root / ('app.png' if name.startswith('app-') else 'tray.png'))
exec(Path(__file__).with_name('tray-art.py').read_text(encoding='utf-8'))
print('Application artwork encoded; separate pixel-grid tray icons generated for light/dark themes.')
