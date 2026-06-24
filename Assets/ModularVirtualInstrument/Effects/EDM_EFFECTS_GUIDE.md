# EDM Effects Library - Complete Guide

## New Effects Overview

### 8 New EDM-Focused Effects
1. **Spatial Position** - 3D audio positioning that follows hand movement
2. **Reverb** - Space and depth simulation
3. **Flanger** - Sweeping jet-plane whoosh
4. **Tremolo** - Rhythmic volume modulation
5. **Ring Modulator** - Metallic/robotic tones
6. **Chorus** - Thickening and widening
7. **Vinyl Crackle** - Lo-fi texture
8. **Compressor** - Loudness and punch

---

## 1. Spatial Position Effect ⭐ SPECIAL

### Description
Creates immersive 3D audio that moves with your hand position in space. As you move your hand around the interaction cube, the audio source physically moves to that position in the virtual space around you.

### How It Works
- **X/Y/Z Position**: Hand position maps directly to 3D audio source position
- **Spatial Blend**: Control value determines 2D vs 3D spatialization (0 = stereo, 1 = full 3D)
- **Center = Centered**: Hand in cube center → audio in center of your head
- **Corners = Directional**: Hand in back-right corner → audio comes from back-right
- **Stirring Motion**: Move hand in circles → audio swirls around you

### Parameters
```
Max Spatial Blend: 0-1 (how 3D the sound becomes)
Min Distance: 1m (close audio range)
Max Distance: 10m (far audio range)
Doppler Level: 0-5 (pitch shift when moving)
Spread: 0-360° (how audio spreads in 3D)
Position Scale: 2.0 (movement range multiplier)
```

### Best Use Cases
- **Pads**: Create swirling atmospheric soundscapes
- **Bass**: Move sub-bass around the room for dramatic effect
- **Leads**: Dynamic positioning for live performance
- **Drums**: Spatial percussion (hi-hat left, kick center, snare right)

### EDM Genre Applications
- Dubstep bass wobbles that move spatially
- Trance pads that orbit around listener
- Progressive house builds with approaching sound
- Ambient soundscapes with 3D placement

### Tips
- Assign to X-axis for left-right panning
- Assign to Y-axis for up-down movement
- Assign to Z-axis for front-back depth
- Combine with reverb for ultimate spatial immersion

---

## 2. Reverb Effect

### Description
Adds spaciousness and depth using Unity's AudioReverbFilter. Essential for creating atmosphere in EDM productions.

### Parameters
```
Reverb Preset: Hangar (or choose from Unity presets)
Min Reverb Level: -10000dB (dry)
Max Reverb Level: -200dB (wet)
Room Size: -1000dB (adjusts with control)
```

### Best Use Cases
- **Pads**: Massive ambient washes
- **Vocals**: Ethereal, spacious vocals
- **Snares/Claps**: Big room sound
- **Breakdown builds**: Increasing space before drop

### EDM Applications
- Festival-style huge reverbs on drops
- Dub delays with reverb tails
- Ambient interludes
- Riser effects with reverb build

---

## 3. Flanger Effect

### Description
Classic phase modulation creating sweeping, jet-plane-like sounds. Uses delay with LFO modulation.

### Parameters
```
Min/Max Delay: 0.5-10ms
LFO Rate: 0.5Hz (modulation speed)
LFO Depth: 0.7 (how much sweep)
Feedback: 0.5 (intensity)
Wet Mix: 0.5
```

### Best Use Cases
- **Synth leads**: Sweeping, evolving tones
- **Pads**: Moving textures
- **Vocals**: Psychedelic effects
- **Transitions**: Sweep into drops

### EDM Applications
- Psytrance sweeping basslines
- Electro house lead sweeps
- Dubstep mid-range wobbles
- Build-up risers

---

## 4. Tremolo Effect

### Description
Rhythmic volume modulation perfect for EDM rhythmic effects and builds.

### Parameters
```
Min/Max Rate: 1-16Hz
Depth: 0.8 (how much volume change)
Waveform: Sine/Square/Triangle/Sawtooth
```

### Best Use Cases
- **Pads**: Pulsing, breathing textures
- **Synths**: Rhythmic gating
- **Builds**: Increasing tempo before drop
- **Breakdowns**: Slow pulsing effects

### EDM Applications
- House sidechain simulation
- Trance gated pads
- Dubstep rhythmic effects
- Progressive builds (speed up rate to drop)

### Rate Guide
- **1-4Hz**: Slow breathing effects
- **4-8Hz**: Rhythmic pulsing (quarter/eighth notes at 120 BPM)
- **8-16Hz**: Fast tremolo, almost ring mod territory
- **Square wave**: Hard gating (EDM sidechain effect)

---

## 5. Ring Modulator Effect

### Description
Multiplies audio with carrier frequency for metallic, robotic, or bell-like tones. Essential for experimental EDM.

### Parameters
```
Min/Max Frequency: 100-2000Hz
Wet Mix: 0.7
Carrier Waveform: Sine/Square/Triangle/Saw
```

### Best Use Cases
- **Vocals**: Robotic voice effects
- **Synths**: Metallic/bell-like tones
- **Percussion**: Inharmonic, aggressive hits
- **Bass**: Distorted, aggressive low-end

### EDM Applications
- Industrial techno percussion
- Glitch hop effects
- Dubstep alien sounds
- Experimental bass music

### Waveform Guide
- **Sine**: Classic ring mod, musical harmonics
- **Square**: Harsh, digital distortion
- **Triangle**: Softer than square, more harmonics
- **Saw**: Bright, buzzy character

---

## 6. Chorus Effect

### Description
Creates multiple delayed copies with pitch modulation to thicken and widen sounds. EDM essential for pads and synths.

### Parameters
```
Base Delay: 20ms
LFO Rate: 1.5Hz
Min/Max Depth: 0.1-0.8
Voice Count: 1-4
Wet Mix: 0.5
```

### Best Use Cases
- **Pads**: Wide, lush textures
- **Synth leads**: Thicker, more present
- **Vocals**: Subtle doubling
- **Bass**: Wider sub-bass (careful!)

### EDM Applications
- Trance supersaws (already chorused in many synths)
- House pad widening
- Progressive layered leads
- Ambient textures

### Voice Count Guide
- **1 voice**: Subtle thickening
- **2 voices**: Classic chorus
- **3 voices**: Lush, wide (recommended)
- **4 voices**: Ultra-thick, can get muddy

---

## 7. Vinyl Crackle Effect

### Description
Adds lo-fi noise and crackle for vintage, tape-saturated aesthetic. Simple but effective for EDM texture.

### Parameters
```
Noise Amount: 0-0.2
Crackle Rate: 0-1
Enable Filtering: true
Cutoff Frequency: 4000Hz
```

### Best Use Cases
- **Lo-fi beats**: Vintage aesthetic
- **Intros/outros**: Nostalgic feel
- **Breakdowns**: Texture and warmth
- **Minimal techno**: Subtle grit

### EDM Applications
- Lo-fi house productions
- Tape stop effects
- Vinyl simulation
- Warmth and character on clean digital sounds

---

## 8. Compressor Effect

### Description
Dynamic range compression for loudness, punch, and glue. Essential for professional EDM sound.

### Parameters
```
Min/Max Threshold: -20dB to -5dB
Ratio: 4:1
Attack: 0.005s (fast)
Release: 0.1s
Makeup Gain: 6dB
```

### Best Use Cases
- **Drums**: Punch and impact
- **Bass**: Consistent levels
- **Full mix**: Glue and loudness
- **Vocals**: Control dynamics

### EDM Applications
- Sidechaining simulation (fast attack/release)
- Drum bus compression (glue)
- Master bus limiting (loudness)
- Individual stem control

### Settings Guide
- **Fast attack (0.001-0.01s)**: Catch transients, reduce punch
- **Slow attack (0.01-0.1s)**: Let transients through, add punch
- **Fast release (0.01-0.1s)**: Pumping, sidechain effect
- **Slow release (0.1-1s)**: Smooth, transparent
- **High ratio (>8:1)**: Limiting, aggressive
- **Low ratio (2-4:1)**: Gentle, musical

---

## EDM Preset Combinations

### Build-Up to Drop
1. **Stem 1 (Riser)**: X=Tremolo (speed up), Y=Reverb (increase), Z=Flanger (sweep)
2. **Stem 2 (Bass)**: X=Filter, Y=Compressor, Z=Spatial Position
3. **At drop**: Freeze all stems, release bass for impact

### Spatial Dubstep Wobble
- **X-Axis**: Spatial Position (left-right movement)
- **Y-Axis**: Filter (wah effect)
- **Z-Axis**: Distortion (aggression)
- **Move hand in circles**: Bass wobbles around your head

### Ambient Soundscape
- **Pad 1**: X=Spatial Position, Y=Reverb, Z=Chorus
- **Pad 2**: X=Tremolo (slow), Y=Reverb, Z=Vinyl Crackle
- **Stir hands**: Create evolving 3D ambient space

### Lo-Fi House Groove
- **Drums**: X=Compressor, Y=Vinyl Crackle, Z=Filter
- **Bass**: X=Chorus, Y=Compressor, Z=Spatial Position
- **Keys**: X=Tremolo, Y=Reverb, Z=Vinyl Crackle

---

## Performance Tips

### Spatial Position Pro Tips
- **Use headphones** or good stereo speakers for best 3D effect
- **Center calibration**: Start hand in center, adjust if off
- **Smooth movements**: Jerky hand motion = jarring audio jumps
- **Freeze position**: Lock spatial position while adjusting other effects

### Effect Stacking
- **Reverb + Chorus**: Huge, wide ambient sounds
- **Flanger + Tremolo**: Rhythmic sweeping
- **Ring Mod + Distortion**: Extreme aggression
- **Compressor + Reverb**: Tight, controlled space

### Live Performance
1. Start with subtle settings
2. Build intensity gradually
3. Use freeze gesture to lock sweet spots
4. Combine spatial movement with effect changes
5. Practice transitions between stems

---

## Technical Notes

### Sample Rate Considerations
All effects assume 44.1kHz sample rate. If using 48kHz, some delay-based effects may sound slightly different.

### CPU Usage
- **Light**: Tremolo, Vinyl Crackle, Compressor
- **Medium**: Spatial Position, Reverb, Filter
- **Heavy**: Flanger, Chorus, Ring Modulator (lots of DSP)

### Multiple Stems Performance
With 4 stems and 3 effects each (12 total effects), prioritize:
1. Spatial Position (unique feature)
2. Filter, Distortion (musical control)
3. Reverb, Delay (space)
4. Others as needed

---

## Troubleshooting

### Spatial Position Not Moving
- Check AudioSource is 3D (Spatial Blend > 0)
- Verify Audio Listener is in scene
- Ensure hand tracking is active

### Effects Too Subtle
- Increase wet/dry mix values
- Adjust min/max parameter ranges
- Check effect is assigned to axis

### Audio Distorting
- Reduce wet mix on multiple effects
- Lower makeup gain on compressor
- Check input levels aren't clipping

### Performance Issues
- Reduce voice count on chorus
- Disable unused effects
- Lower update rate on hand tracking

