"""Generate gameplay SFX for MemoryCorridor (self-made CC0).

Outputs (44.1kHz 16-bit mono WAV):
  sewing.wav      - rhythmic mechanical "ta-da" loop (frag_01)
  bell.wav        - bicycle bell "ding-ling" x2 (frag_07)
  sfx_hover_tick.wav - short soft hover tick (UI)
  sfx_page.wav    - paper flip swish (UI)

Usage: python gen_sfx.py <out_dir>
"""
import math
import os
import random
import struct
import sys
import wave

SR = 44100


def write_wav(path, samples):
    frames = bytearray()
    for v in samples:
        frames += struct.pack("<h", int(max(-1.0, min(1.0, v)) * 32767))
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(bytes(frames))
    print("written:", os.path.basename(path), len(samples) // SR, "s")


def fade_tail(samples, sec=0.05):
    n = int(SR * sec)
    for i in range(min(n, len(samples))):
        samples[-1 - i] *= i / max(1, n)
    return samples


def gen_sewing(dur=5.0):
    """Mechanical loop: noise burst pairs at ~5 Hz, resonant body ~180Hz."""
    rnd = random.Random(7)
    out = [0.0] * int(SR * dur)
    period = SR / 5.0  # 5 stitches per second
    pos = 0.0
    while pos < len(out):
        idx = int(pos)
        for k, (gain, decay) in enumerate(((0.5, 220.0), (0.35, 400.0))):
            for j in range(int(SR * 0.045)):
                t = j / SR
                i2 = idx + j
                if i2 >= len(out):
                    break
                click = math.exp(-decay * t) * (rnd.uniform(-0.4, 0.4) + math.sin(2 * math.pi * 900 * t) * 0.2)
                out[i2] += click * gain
        pos += period
    return fade_tail(out)


def gen_bell(dur=2.2):
    """Bicycle bell: two bright strikes ~0.35s apart, inharmonic partials."""
    out = [0.0] * int(SR * dur)
    for start, gain in ((0.05, 0.6), (0.42, 0.5)):
        idx = int(start * SR)
        partials = ((2093, 1.0), (2637, 0.6), (3136, 0.4), (4187, 0.25))
        for j in range(int(SR * 0.9)):
            t = j / SR
            i2 = idx + j
            if i2 >= len(out):
                break
            v = 0.0
            for f, g in partials:
                v += math.sin(2 * math.pi * f * t) * g * math.exp(-6.0 * t)
            out[i2] += v * gain * 0.4
    return fade_tail(out)


def gen_hover_tick(dur=0.18):
    """Very short soft tick: filtered click with fast decay."""
    rnd = random.Random(11)
    out = []
    for i in range(int(SR * dur)):
        t = i / SR
        v = math.sin(2 * math.pi * 1800 * t) * math.exp(-45 * t) * 0.5
        out.append(v + rnd.uniform(-0.02, 0.02))
    return out


def gen_page(dur=0.45):
    """Paper swish: band-passed noise with fast attack/slow release."""
    rnd = random.Random(13)
    out = []
    last = 0.0
    for i in range(int(SR * dur)):
        t = i / SR
        env = math.sin(math.pi * min(1.0, t / dur))
        white = rnd.uniform(-1, 1)
        last = 0.92 * last + 0.08 * white  # soften (lowpass)
        out.append(last * env * 0.8)
    return out


def main(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    write_wav(os.path.join(out_dir, "sewing.wav"), gen_sewing())
    write_wav(os.path.join(out_dir, "bell.wav"), gen_bell())
    write_wav(os.path.join(out_dir, "sfx_hover_tick.wav"), gen_hover_tick())
    write_wav(os.path.join(out_dir, "sfx_page.wav"), gen_page())


if __name__ == "__main__":
    main(sys.argv[1])
