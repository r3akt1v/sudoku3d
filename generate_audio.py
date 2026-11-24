"""
Generate procedural WAV audio for Sudoku3D.
Output: Assets/Resources/Audio/*.wav (44.1kHz 16-bit)
"""
import numpy as np
import os
import wave

SR = 44100
OUT = "/Users/aleksandrsmirnov/Documents/sudoku/Sudoku3D/Assets/Resources/Audio"
os.makedirs(OUT, exist_ok=True)


def save_wav(name, data, stereo=False):
    """data: float array [-1,1], shape (n,) or (n,2)"""
    if data.ndim == 1:
        data = np.stack([data, data], axis=1)
    pcm = (np.clip(data, -1, 1) * 32767).astype(np.int16)
    path = os.path.join(OUT, name)
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(f"  {name}: {len(data)/SR:.2f}s")


def env_adsr(n, a=0.005, d=0.05, s=0.0, r=0.05, sr=SR):
    """Simple attack/decay/release envelope, peak=1"""
    e = np.zeros(n)
    na, nd, nr = int(a * sr), int(d * sr), int(r * sr)
    ns = n - na - nd - nr
    if na > 0: e[:na] = np.linspace(0, 1, na)
    if nd > 0: e[na:na+nd] = np.linspace(1, s, nd)
    if ns > 0: e[na+nd:na+nd+ns] = s
    if nr > 0: e[-nr:] = np.linspace(s if ns <= 0 else e[-nr-1] if nr > 0 else s, 0, nr)
    return e


def tone(freq, dur, vol=0.5, attack=0.005, decay=None, sr=SR, phase=0.0):
    n = int(dur * sr)
    t = np.arange(n) / sr
    sig = np.sin(2 * np.pi * freq * t + phase)
    # slight harmonic for warmth
    sig += 0.15 * np.sin(2 * np.pi * freq * 2 * t + phase)
    if decay is None:
        decay = dur * 0.7
    e = env_adsr(n, a=attack, d=decay, r=min(0.05, dur * 0.3), sr=sr)
    return sig * e * vol


def add_into(buf, sig, offset=0):
    end = min(len(buf), offset + len(sig))
    if end > offset:
        buf[offset:end] += sig[:end - offset]
    return buf


def mix(*parts):
    """Mix signals of different lengths into one buffer"""
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def noise_burst(dur, vol=0.3, freq=2000.0, q=1.0, sr=SR):
    """Bandpassed noise click"""
    n = int(dur * sr)
    rng = np.random.default_rng(42)
    x = rng.standard_normal(n)
    # simple one-pole lowpass + highpass mix for 'wood' feel
    alpha = min(0.9, freq / sr * 2 * np.pi)
    y = np.zeros(n)
    for i in range(1, n):
        y[i] = y[i-1] + alpha * (x[i] - y[i-1])
    e = env_adsr(n, a=0.001, d=dur * 0.5, r=dur * 0.4, sr=sr)
    return y * e * vol


print("Generating SFX...")

# --- pickup: short soft tick ---
pickup = mix(tone(880, 0.07, vol=0.35, attack=0.002, decay=0.04),
             noise_burst(0.05, vol=0.15, freq=3000))
save_wav("pickup.wav", pickup * 0.9)

# --- place: wooden thock (low knock + noise) ---
place = mix(tone(180, 0.16, vol=0.55, attack=0.002, decay=0.09),
            tone(320, 0.08, vol=0.2, attack=0.001, decay=0.05),
            noise_burst(0.07, vol=0.28, freq=1200))
save_wav("place.wav", place * 0.85)

# --- error: soft dull buzz (two low tones, minor) ---
err = np.zeros(int(0.35 * SR))
for f, st in [(196.0, 0.0), (185.0, 0.12)]:
    n = int(0.2 * SR)
    t = np.arange(n) / SR
    sig = np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * f * 1.5 * t)
    e = env_adsr(n, a=0.01, d=0.1, r=0.06, sr=SR)
    off = int(st * SR)
    err[off:off+n] += sig * e * 0.3
save_wav("error.wav", err)

# --- hint: gentle rising chime ---
hint = np.zeros(int(0.6 * SR))
for i, f in enumerate([523.25, 659.25, 783.99]):
    n = int(0.22 * SR)
    t = np.arange(n) / SR
    sig = np.sin(2 * np.pi * f * t) + 0.2 * np.sin(2 * np.pi * f * 2 * t)
    e = env_adsr(n, a=0.01, d=0.12, r=0.08, sr=SR)
    off = int(i * 0.09 * SR)
    hint[off:off+n] += sig * e * 0.32
save_wav("hint.wav", hint)

# --- win: little victory arpeggio ---
win = np.zeros(int(1.4 * SR))
notes = [523.25, 659.25, 783.99, 1046.5]
for i, f in enumerate(notes):
    n = int(0.35 * SR)
    t = np.arange(n) / SR
    sig = np.sin(2 * np.pi * f * t) + 0.25 * np.sin(2 * np.pi * f * 2 * t) + 0.1 * np.sin(2 * np.pi * f * 3 * t)
    e = env_adsr(n, a=0.008, d=0.2, r=0.12, sr=SR)
    off = int(i * 0.14 * SR)
    win[off:off+n] += sig * e * 0.3
# final chord
n = int(0.6 * SR)
t = np.arange(n) / SR
chord = sum(np.sin(2 * np.pi * f * t) for f in [523.25, 659.25, 783.99, 1046.5]) / 4
e = env_adsr(n, a=0.02, d=0.3, r=0.25, sr=SR)
off = int(4 * 0.14 * SR)
win[off:off+n] += chord * e * 0.35
save_wav("win.wav", win)

# --- music: ambient loop, seamless ---
# 24s loop: slow pad chords (Am - F - C - G) + soft plucked arpeggio
BPM = 60
BEAT = 60.0 / BPM          # 1s per beat
BAR = BEAT * 4             # 4s per bar
LOOP_BARS = 6
LOOP = BAR * LOOP_BARS     # 24s
n = int(LOOP * SR)
music = np.zeros((n, 2))

rng = np.random.default_rng(7)

# chord progression: Am, F, C, G, Am, F  (freqs in Hz)
chords = [
    [220.0, 261.63, 329.63],   # Am
    [174.61, 220.0, 261.63],   # F
    [261.63, 329.63, 392.0],   # C
    [196.0, 246.94, 293.66],   # G
    [220.0, 261.63, 329.63],   # Am
    [174.61, 220.0, 261.63],   # F
]

# pad: detuned saw-ish pads via sum of sines + slow tremolo
t = np.arange(n) / SR
for bar in range(LOOP_BARS):
    start = int(bar * BAR * SR)
    end = min(n, start + int(BAR * SR))
    tt = np.arange(end - start) / SR
    chord = chords[bar % len(chords)]
    pad = np.zeros(end - start)
    for f in chord:
        for det in (-0.3, 0.0, 0.3):
            ff = f * (1 + det / 100.0)
            # soft attack/release per bar with overlap
            sig = np.sin(2 * np.pi * ff * tt) * 0.5 + np.sin(2 * np.pi * ff * 1.005 * tt) * 0.5
            # slow amplitude swell within the bar
            swell = 0.6 + 0.4 * np.sin(2 * np.pi * tt / BAR + bar)
            pad += sig * swell
    pad /= (len(chord) * 3)
    # bar envelope: crossfade edges for seamless loop
    fade = int(0.8 * SR)
    if fade > 0 and end - start > 2 * fade:
        pad[:fade] *= np.linspace(0, 1, fade)
        pad[-fade:] *= np.linspace(1, 0, fade)
    else:
        pad *= 0.8
    music[start:end, 0] += pad * 0.16
    music[start:end, 1] += pad * 0.16

# sparse plucked melody (pentatonic-ish), one note per 2 beats, quiet
scale = [440.0, 523.25, 587.33, 659.25, 783.99]
melody_idx = [0, 2, 4, 3, 1, 2, 0, 4, 3, 2, 1, 0]
for i, mi in enumerate(melody_idx):
    beat_pos = i * 2.0  # every 2 beats = every 2s
    start = int(beat_pos * BEAT * SR)
    dur = 1.6
    nn = int(dur * SR)
    if start + nn > n:
        break
    tt = np.arange(nn) / SR
    f = scale[mi % len(scale)]
    sig = np.sin(2 * np.pi * f * tt) + 0.3 * np.sin(2 * np.pi * f * 2 * tt) + 0.1 * np.sin(2 * np.pi * f * 3 * tt)
    # pluck envelope: fast attack, exponential decay
    e = np.exp(-tt * 2.2)
    e[:int(0.005 * SR)] *= np.linspace(0, 1, int(0.005 * SR))
    # stereo: slightly different L/R via phase
    l = sig * e * 0.11
    r = np.sin(2 * np.pi * f * tt + 0.15) * e * 0.11 + 0.05 * np.sin(2 * np.pi * f * 2 * tt) * e
    music[start:start+nn, 0] += l
    music[start:start+nn, 1] += r

# gentle noise-free "air": very quiet high sine shimmer, loop-aligned
shimmer_f = 1760.0
nn = n
tt = np.arange(nn) / SR
sh = np.sin(2 * np.pi * shimmer_f * tt) * 0.008
sh *= 0.5 + 0.5 * np.sin(2 * np.pi * tt / LOOP * 4)  # 4 swells per loop
music[:, 0] += sh
music[:, 1] += sh * 0.8

# soft limiter
def limit(x, ceiling=0.85):
    return np.tanh(x / ceiling) * ceiling

music = limit(music)
# fade very ends to avoid click (loop is seamless since content periodic)
save_wav("music.wav", music)

print("=== AUDIO DONE ===")
