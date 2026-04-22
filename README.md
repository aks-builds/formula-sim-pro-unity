# Formula Sim Pro — Unity Edition

Ultra-realistic top-down F1 mobile racing game built on **Unity 2023 LTS** with the **Universal Render Pipeline (URP)**.

---

## What's different from the Defold version

| Feature | Defold (v1) | Unity URP (v2) |
|---------|-------------|----------------|
| Rendering | GLSL ES 2.0, sprites | URP + PBR + HLSL |
| Post-processing | Manual shader passes | URP Volume stack (Bloom, Motion Blur, CA, Color Grading, DoF, Vignette) |
| Car materials | Flat sprites | PBR metallic/roughness + livery mask injection + emission maps |
| Wet track | Custom GLSL | HLSL ShaderLab with normal-mapped ripples + Fresnel reflections |
| Rain overlay | GLSL screen pass | URP Renderer Feature full-screen blit |
| Lighting | None | URP 2D Lights — sun, ambient fill, night floodlights |
| Shadows | None | URP real-time shadow maps |
| Camera | Manual follow | Cinemachine (transposer + lookahead + noise) |
| Particles | Defold particlefx | Unity VFX-ready Particle System with GPU instancing |
| Input | Defold input bindings | Unity Input System (touch, keyboard, gamepad) |
| Serialization | Lua tables | ScriptableObjects + JSON (Newtonsoft) |

---

## Architecture

```
Assets/
├── Scripts/
│   ├── Core/         GameManager (state machine), GameState
│   ├── Cars/         F1CarController, VehicleConfig (SO), AIDriver, TireManager
│   ├── Tracks/       CircuitData (SO), TrackRegistry (SO), CheckpointSystem
│   ├── Teams/        TeamRegistry (SO), DriverData, TeamData
│   ├── Weather/      WeatherSystem, VisualWeather (URP volumes + shader uniforms)
│   ├── Audio/        AudioManager (AudioMixer buses), CommentarySystem, CommentaryLines
│   ├── Championship/ SeasonManager, Standings
│   ├── Career/       CareerManager, ContractSystem
│   ├── Livery/       LiverySystem (HSV injection via CarBody.shader)
│   ├── Gameplay/     PitStopGame (rhythm mini-game)
│   ├── FX/           CameraRig (Cinemachine), RainRendererFeature (URP)
│   ├── Network/      ConnectivityManager (UnityWebRequest)
│   ├── Notifications/PushSystem (Unity Mobile Notifications)
│   ├── Backend/      APIClient (REST, Newtonsoft JSON)
│   └── UI/           HUDController, WeatherHUD, RaceStartSequence, LiveryEditorScreen
├── Shaders/
│   ├── WetTrack.shader        — PBR + ripple normals + Fresnel puddles + lightning
│   ├── CarBody.shader         — PBR + livery mask + brake glow + DRS shimmer + headlights
│   ├── RainOverlay.shader     — Full-screen rain streak compositing
│   ├── SpeedLines.shader      — Radial speed lines overlay (DRS / high speed)
│   └── NightTrackGlow.shader  — HDR emission for floodlights + pulse/flicker
└── Settings/
    └── URPPipelineAsset       — URP asset (configure in Project Settings → Graphics)
```

---

## URP Post-Processing Stack

Configured via a **Global Volume** in the race scene:

| Effect | Purpose |
|--------|---------|
| **Bloom** | Car headlights, DRS shimmer, floodlights scatter |
| **Motion Blur** | High-speed straights, DRS activation |
| **Chromatic Aberration** | Scales with rain intensity (visual stress) |
| **Color Adjustments** | Desaturate + cool tint in rain |
| **Vignette** | Tighter in extreme weather |
| **Lens Distortion** | Subtle rain screen distortion |
| **Depth of Field** | Optional: shallow focus on podium replay |

---

## Packages Used

| Package | Version | Purpose |
|---------|---------|---------|
| `com.unity.render-pipelines.universal` | 14.x | URP renderer, post-processing |
| `com.unity.cinemachine` | 2.10.x | Camera follow, shake, lookahead |
| `com.unity.inputsystem` | 1.8.x | Touch, keyboard, gamepad |
| `com.unity.textmeshpro` | 3.x | All in-game text |
| `com.unity.mobile.notifications` | 2.3.x | Local push notifications |
| `com.unity.addressables` | 1.21.x | Circuit asset streaming |
| `com.unity.nuget.newtonsoft-json` | 3.2.x | API JSON serialisation |
| `com.unity.burst` | 1.8.x | AI pathfinding hot paths |

---

## Getting Started

### Prerequisites

- Unity 2023.2 LTS (install via Unity Hub)
- Android Build Support + NDK (for Android builds)
- iOS Build Support + Xcode 15+ (for iOS builds)

### Open Project

1. Clone this repo: `git clone https://github.com/your-org/formula-sim-pro-unity`
2. Open Unity Hub → **Add project from disk** → select the repo root
3. Unity will import packages automatically
4. Open **Assets/Scenes/MainMenu.unity** and press Play

### Configure URP

1. Edit → Project Settings → **Graphics**
2. Set **Scriptable Render Pipeline Settings** to `Assets/Settings/URPPipelineAsset`
3. In the URP asset: enable **HDR**, **MSAA × 2**, **Post Processing**

### Build — Android

```
File → Build Settings → Android → Switch Platform
Player Settings:
  Company: FormulaSim
  Product: Formula Sim Pro
  Package: com.formulasimpro.racing
  Min API: 24  Target API: 34
  Scripting Backend: IL2CPP
  ARM64: ✓
Build and Run
```

### Build — iOS

```
File → Build Settings → iOS → Switch Platform → Build
Open Xcode project → set signing team → Archive → Distribute
```

---

## Internet Connectivity

- **Online:** Full career, championships, all 8 circuits, leaderboards
- **Offline:** Free practice on **Silverstone GP** only

---

## License

MIT — see [LICENSE](LICENSE)

## Contributing

See [CONTRIBUTORS.md](CONTRIBUTORS.md)
