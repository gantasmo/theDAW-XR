# EDM Effects - Quick Reference Card

## Effect Selection Guide

### What sound do you want?

**🌊 SPACE & DEPTH**
- Reverb → Add room/hall ambience
- Spatial Position → Move sound in 3D space
- Delay → Echoes and rhythmic repeats

**✨ MOVEMENT & MODULATION**
- Flanger → Sweeping jet-plane whoosh
- Chorus → Thicken and widen
- Tremolo → Rhythmic volume pulsing
- Phaser → (use existing Filter with modulation)

**🔊 DYNAMICS & ENERGY**
- Compressor → Punch, loudness, glue
- Distortion → Aggression, saturation
- BitCrush → Digital degradation

**🎛️ TONE SHAPING**
- Filter → Wah, sweep, cut frequencies
- Pitch → Transpose, growl
- Ring Modulator → Metallic, robotic

**🎵 RHYTHM & TEXTURE**
- Stutter → Rhythmic gating
- Tremolo → Volume pulsing
- Vinyl Crackle → Lo-fi noise

---

## Best 3-Axis Combinations

### 🎧 Spatial Dubstep Bass
```
X-Axis: Spatial Position (left-right)
Y-Axis: Filter (frequency sweep)
Z-Axis: Distortion (aggression)
→ Stir hand = bass wobbles around head
```

### 🌌 Ambient Soundscape
```
X-Axis: Spatial Position (3D movement)
Y-Axis: Reverb (space depth)
Z-Axis: Chorus (width)
→ Create evolving 3D atmospheres
```

### 🔥 Festival Drop
```
X-Axis: Filter (sweep build-up)
Y-Axis: Distortion (energy)
Z-Axis: Compressor (punch)
→ High energy, controlled chaos
```

### 💫 Trance Lead
```
X-Axis: Pitch (melody variation)
Y-Axis: Flanger (sweep)
Z-Axis: Reverb (space)
→ Classic trance lead sound
```

### 🎹 Lo-Fi Keys
```
X-Axis: Tremolo (sidechain pump)
Y-Axis: Vinyl Crackle (texture)
Z-Axis: Chorus (width)
→ Vintage, warm character
```

### 🥁 Drum Punch
```
X-Axis: Compressor (punch)
Y-Axis: BitCrush (grit)
Z-Axis: Filter (tone)
→ Controlled, impactful drums
```

### 🌀 Psychedelic Vocal
```
X-Axis: Ring Modulator (robotic)
Y-Axis: Flanger (movement)
Z-Axis: Reverb (space)
→ Trippy, otherworldly vocals
```

---

## Effect Parameter Cheat Sheet

### Spatial Position ⭐
- **Control**: 0=2D/Stereo, 1=Full 3D
- **Sweet Spot**: 0.6-0.8 for subtle 3D
- **Pro Tip**: Assign to X-axis for pan control

### Reverb
- **Control**: 0=Dry, 1=Wet/Spacious
- **Sweet Spot**: 0.3-0.5 for most EDM
- **Pro Tip**: Automate on build-ups

### Flanger
- **Control**: 0=Minimal, 1=Extreme Sweep
- **Sweet Spot**: 0.5-0.7 for classic sound
- **Pro Tip**: Square wave LFO for dubstep

### Tremolo
- **Control**: 0=Slow (1Hz), 1=Fast (16Hz)
- **Sweet Spot**: 4Hz = house sidechain
- **Pro Tip**: Square wave = hard gate

### Ring Modulator
- **Control**: 0=Low Freq, 1=High Freq
- **Sweet Spot**: 0.3 for musical tones
- **Pro Tip**: Sine wave = clean, Square = harsh

### Chorus
- **Control**: 0=Subtle, 1=Thick
- **Sweet Spot**: 0.4-0.6 for pads
- **Pro Tip**: 3 voices = best width

### Vinyl Crackle
- **Control**: 0=Clean, 1=Heavy Lo-Fi
- **Sweet Spot**: 0.2-0.4 for warmth
- **Pro Tip**: Combine with filter

### Compressor
- **Control**: 0=Light, 1=Heavy Compression
- **Sweet Spot**: 0.5-0.7 for glue
- **Pro Tip**: High value = pumping

---

## Performance Workflow

### 1. Setup (Edit Mode)
1. Create StemData assets
2. Assign audio clips
3. Choose 3 effects per stem
4. Generate stems in editor
5. Adjust positions with gizmos
6. Save as prefab

### 2. Live Performance (Play Mode)
1. Enter stem with hand
2. Move hand on X/Y/Z axes
3. Flip palm up → Freeze effects
4. Move to another stem
5. Flip palm up → Unfreeze

### 3. Creative Techniques
- **Freeze & Layer**: Lock one stem, control another
- **Spatial Stirring**: Circular motion with Spatial Position
- **Build & Release**: Increase reverb/filter, freeze at peak
- **Contrast**: Clean stem ↔ Heavy processed stem

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Spatial Position not working | Enable 3D audio on AudioSource |
| Effects too subtle | Increase wet/dry mix parameters |
| Audio crackling | Reduce effect count or CPU load |
| Hand tracking laggy | Lower update rate in settings |
| Freeze not working | Check palm angle threshold |
| Multiple stems duplicating | Set Generation Mode to UseExistingOnly |

---

## CPU Optimization

**Light Load (Use Freely)**
- Tremolo, Vinyl Crackle, Compressor, Pitch, Filter

**Medium Load (3-4 instances)**  
- Spatial Position, Reverb, Distortion, BitCrush

**Heavy Load (1-2 instances)**
- Flanger, Chorus, Ring Modulator, Stutter

**Recommendation**: Max 12 total effects (4 stems × 3 effects) for smooth performance.

---

## Genre-Specific Presets

### House
- Drums: Compressor + Tremolo + Filter
- Bass: Chorus + Compressor + Filter
- Pads: Reverb + Chorus + Tremolo

### Dubstep
- Bass: Spatial Position + Filter + Distortion
- Drums: BitCrush + Compressor + Stutter
- FX: Ring Mod + Flanger + Reverb

### Trance
- Lead: Pitch + Flanger + Reverb
- Bass: Filter + Compressor + Delay
- Pads: Chorus + Reverb + Tremolo

### Lo-Fi
- All: Vinyl Crackle + Chorus + Filter
- Drums: BitCrush + Compressor + Vinyl
- Keys: Tremolo + Vinyl + Reverb

---

**Remember**: Start subtle, build intensity, use freeze gesture to lock sweet spots!
