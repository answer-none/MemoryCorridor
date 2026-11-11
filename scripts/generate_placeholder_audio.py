"""Generate CC0 (self-made) placeholder ambient audio for MemoryCorridor.

Usage: python generate_placeholder_audio.py <output_dir>
Writes 44.1kHz 16-bit stereo WAVs with pure stdlib (wave/math/random/struct).
Replace with real recordings later; keep filenames stable.
"""
import math
import os
import random
import struct
import sys
import wave

SAMPLE_RATE = 44100


def write_wav(path, channels, duration_s):
    """channels: list of float lists (one per channel), values in [-1, 1]."""
    n = int(SAMPLE_RATE * duration_s)
    frames = bytearray()
    for i in range(n):
        for ch in channels:
            v = max(-1.0, min(1.0, ch[i]))
            frames += struct.pack("<h", int(v * 32767))
    with wave.open(path, "wb") as w:
        w.setnchannels(len(channels))
        w.setsampwidth(2)
        w.setframerate(SAMPLE_RATE)
        w.writeframes(bytes(frames))


def fade(samples, fade_s=1.5):
    n = len(samples)
    f = int(SAMPLE_RATE * fade_s)
    for i in range(min(f, n)):
        samples[i] *= i / f
        samples[n - 1 - i] *= i / f
    return samples


def brown_noise(duration_s):
    out = []
    last = 0.0
    for _ in range(int(SAMPLE_RATE * duration_s)):
        white = random.uniform(-1, 1)
        last = (last + 0.02 * white) / 1.02
        out.append(last * 3.5)
    return out


def lowpass(samples, alpha=0.98):
    out = []
    y = 0.0
    for x in samples:
        y = alpha * y + (1 - alpha) * x
        out.append(y)
    return out


def slow_modulation(samples, period_s=7.0, depth=0.25):
    out = []
    n = len(samples)
    for i, x in enumerate(samples):
        m = 1.0 - depth + depth * 0.5 * (1 + math.sin(2 * math.pi * i / (SAMPLE_RATE * period_s)))
        out.append(x * m)
    _ = n
    return out


def chime(freqs, duration_s, decay=3.0):
    """Bell-like decaying sine stack."""
    n = int(SAMPLE_RATE * duration_s)
    out = []
    for i in range(n):
        t = i / SAMPLE_RATE
        v = 0.0
        for k, f in enumerate(freqs):
            v += math.sin(2 * math.pi * f * t) * math.exp(-decay * (1 + k * 0.5) * t) / (k + 1)
        out.append(v / len(freqs) * 0.8)
    return fade(out, 0.01)


def stereoize(mono, width=0.15, seed=7):
    rnd = random.Random(seed)
    right = []
    delay = int(SAMPLE_RATE * 0.012)
    for i, x in enumerate(mono):
        r = mono[i - delay] if i >= delay else 0.0
        jitter = 1.0 + rnd.uniform(-width, width)
        right.append(r * jitter)
    return mono, right


def main(out_dir):
    os.makedirs(out_dir, exist_ok=True)

    # 1) Evening room tone: soft filtered brown noise, slow swell (living room)
    d = 30.0
    tone = fade(slow_modulation(lowpass(brown_noise(d), 0.985), 8.0))
    l, r = stereoize(tone, 0.1)
    write_wav(os.path.join(out_dir, "amb_evening_roomtone.wav"), [l, r], d)

    # 2) Courtyard dusk: windier, wider stereo, occasional soft chirps
    d = 30.0
    wind = fade(slow_modulation(lowpass(brown_noise(d), 0.96), 5.0, 0.45))
    chirps = [0.0] * len(wind)
    for start_s in (4.2, 11.7, 19.3, 26.1):
        s = int(start_s * SAMPLE_RATE)
        for i in range(int(0.35 * SAMPLE_RATE)):
            t = i / SAMPLE_RATE
            env = math.exp(-14 * t) * math.sin(2 * math.pi * t * 90)  # amplitude gate
            v = math.sin(2 * math.pi * (2600 + 900 * math.sin(2 * math.pi * 14 * t)) * t) * env * 0.12
            if s + i < len(chirps):
                chirps[s + i] = v
    mix = [a + b for a, b in zip(wind, chirps)]
    l, r = stereoize(mix, 0.3, seed=11)
    write_wav(os.path.join(out_dir, "amb_courtyard_dusk.wav"), [l, r], d)

    # 3) Light-point chime: hover/select feedback, warm bell
    d = 2.5
    c = chime([523.25, 659.25, 783.99], d, decay=2.2)
    l, r = stereoize(c, 0.05)
    write_wav(os.path.join(out_dir, "sfx_lightpoint_chime.wav"), [l, r], d)

    print("written:", sorted(os.listdir(out_dir)))


if __name__ == "__main__":
    main(sys.argv[1])
