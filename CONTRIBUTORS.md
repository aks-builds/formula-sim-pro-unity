# Contributors

## Core Team

| Name | Role |
|------|------|
| Aditya Singh | Founder |

## How to Contribute

1. Fork and create a feature branch: `git checkout -b feature/your-feature`
2. Follow C# code style: PascalCase methods/properties, _camelCase private fields, no Hungarian notation
3. No `Update()` polling for one-shot events — use events/coroutines
4. Test on both Android (API 24+) and iOS (14+) before submitting
5. Open a PR using `.github/pull_request_template.md`

## Shader Contributions

- All shaders target HLSL (URP ShaderLab syntax)
- Test on low-end mobile (Adreno 610 equivalent) and high-end (Apple A15+)
- Avoid branching in fragment shaders on mobile — prefer `lerp` / `step`
- Use `#pragma multi_compile` for feature variants rather than `if` checks

## Audio Asset Guidelines

- Engine samples: 24-bit / 44.1 kHz WAV, loop points precisely set
- Music: OGG Vorbis q6, loopable
- UI sounds: 16-bit / 44.1 kHz WAV, < 0.5s
- All assets must be original or CC0 licensed

## Code of Conduct

Constructive, respectful collaboration. Focus on gameplay quality, performance on mobile, and accessibility.
