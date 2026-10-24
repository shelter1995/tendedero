"""Offline audition drafts only. This script never edits runtime sound assets/code."""
import math, random, struct, wave
from pathlib import Path

root = Path(__file__).resolve().parents[1] / 'artifacts' / 'sound-auditions'
root.mkdir(parents=True, exist_ok=True)
rate = 44100
names = ['hang', 'copy', 'remove', 'edit']
labels = ['夹上图片', '复制成功', '取下 / 删除', '进入编辑']

def cue(style, action):
    duration = [.12, .11, .18, .18][action]
    rng = random.Random(731 + action)
    low = previous = 0.0
    result = []
    for i in range(int(rate * duration)):
        t = i / rate
        raw = rng.uniform(-1, 1)
        low += .13 * (raw - low)
        textured = low - previous
        previous = low
        attack = min(1, t / .004)
        tail = min(1, (duration - t) / .025)
        if style == 'A':
            if action == 0:
                value = .15 * math.exp(-65*t) * (math.sin(2*math.pi*690*t) + .25*math.sin(2*math.pi*1630*t)) + .11*low*math.exp(-45*t)
            elif action == 1:
                value = .3*textured*math.sin(math.pi*t/duration)*math.exp(-16*t) + .025*math.exp(-65*t)*math.sin(2*math.pi*1120*t)
            elif action == 2:
                value = .14*math.exp(-32*t)*math.sin(2*math.pi*(420*t-350*t*t)) + .1*low*math.exp(-24*t)
            else:
                value = .23*textured*math.sin(math.pi*t/duration)*math.exp(-8*t) + .025*low
        else:
            pitch = [950, 1260, 460, 780][action]
            decay = [70, 80, 38, 50][action]
            value = .115 * math.sin(2*math.pi*pitch*t) * math.exp(-decay*t) + .025*low*math.exp(-decay*t)
        result.append(max(-.3, min(.3, value * attack * tail)))
    return result

def save(path, samples):
    assert max(map(abs, samples), default=0) <= .3
    with wave.open(str(path), 'wb') as f:
        f.setparams((1, 2, rate, 0, 'NONE', 'not compressed'))
        f.writeframes(b''.join(struct.pack('<h', round(x*32767)) for x in samples))

for style in ['A', 'B']:
    sequence = [0.0] * int(.3 * rate)
    for action, name in enumerate(names):
        sound = cue(style, action)
        save(root / f'{style}-{name}.wav', sound)
        sequence += sound + [0.0] * int(.8 * rate)
    save(root / f'{style}-sequence.wav', sequence)

rows = ''.join(f'<tr><th>{label}</th><td><audio controls preload="none" src="A-{name}.wav"></audio></td><td><audio controls preload="none" src="B-{name}.wav"></audio></td></tr>' for name, label in zip(names, labels))
(root/'index.html').write_text('''<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>晾图 · 音效试听</title>
<style>body{margin:40px auto;padding:0 24px;max-width:920px;background:#202124;color:#f2f2f4;font:15px "Microsoft YaHei UI",sans-serif}h1{font-size:26px}p{color:#b6bac4;line-height:1.8}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:18px 12px;border-bottom:1px solid #454850}th{font-weight:500}audio{max-width:100%;width:280px}small{color:#9bb8ff}button{background:#2e3c59;color:#d8e4ff;border:0;border-radius:5px;padding:10px 16px;cursor:pointer}</style>
<h1>先听，再决定</h1><p>两组独立样稿，尚未接入软件。每个操作只有一个音效。可以整组选择，也可以逐项搭配；“进入编辑”也可以选择静音。</p>
<table><tr><th>操作</th><th>A · 木夹与纸张</th><th>B · 更轻的单次短音</th></tr>'''+rows+'''</table>
<p>连续试听顺序：夹上 → 复制 → 取下 / 删除 → 进入编辑。每段之间留有停顿。</p><audio controls src="A-sequence.wav"></audio> <audio controls src="B-sequence.wav"></audio>
<p><small>当前程序的音效保持不变，等待确认后再替换。</small></p>''', encoding='utf-8')
print(root)
