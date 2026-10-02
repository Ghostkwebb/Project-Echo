# Paragon Wraith - Unity Ready Asset Package

This folder contains the complete assets of **Paragon Wraith** exported from Unreal Engine `.uasset` format into normal, industry-standard formats ready for use and testing in **Unity**.

The original Unreal Engine directory structure has been preserved 1:1.

---

## Asset Breakdown

| Asset Type | Unreal Format | Exported Normal Format | Count | Details |
| :--- | :--- | :--- | :--- | :--- |
| **Skeletal Meshes** | `.uasset` | `.fbx` | 3 | Full character rigs (Wraith Base, LunarOps, ODGreen) with bones & skin weights |
| **Static Meshes** | `.uasset` | `.fbx` | 37 | Environment platforms, weapons, ability attachments |
| **Animations** | `.uasset` | `.fbx` | 229 | Combat locomotion, idle, run, sprint, jump, ability casts, death, recall |
| **Textures** | `.uasset` | `.png` | 201 | Lossless 4K, 2K, and 1K RGBA textures (Albedo, Normal, Masks, Emissive) |
| **HDRI Cubemap** | `.uasset` | `.hdr` | 1 | High Dynamic Range Environment map |
| **Audio** | `.uasset` | `.wav` | 362 | 48kHz 16-bit uncompressed PCM dialogue lines and ability sound FX |
| **Materials** | `.uasset` | `.mat.json` | 90 | Complete parameter mappings (texture bindings, scalars, colors) |
| **Dialogue Meta** | `.uasset` | `.dialogue.json` | 362 | Dialogue wave references |

Total Files Exported: **1,286+** (~1.5 GB)

---

## Directory Structure

```
~/Desktop/ParagonWraith/
├── Audio/
│   ├── DialogueWaves/           # Dialogue wave mapping (.dialogue.json)
│   └── Wavs/                    # All spoken dialogue lines and ability SFX (.wav)
├── Characters/
│   ├── Global/                  # Global material layers and shared textures (.png)
│   ├── Heroes/
│   │   └── Wraith/
│   │       ├── Animations/      # 229 baked animation clips (.fbx)
│   │       │   ├── AimOffset/
│   │       │   ├── Blendspaces/
│   │       │   └── Locomotion_Combat/
│   │       ├── Materials/       # Material definitions and parameter maps (.mat.json)
│   │       ├── Meshes/          # Wraith character skeletal mesh (.fbx)
│   │       ├── Skins/
│   │       │   ├── LunarOps/    # LunarOps skin mesh (.fbx), textures (.png), materials (.mat.json)
│   │       │   └── ODGreen/     # ODGreen skin mesh (.fbx), textures (.png), materials (.mat.json)
│   │       └── Textures/        # Character 4K/2K diffuse, normal, and mask maps (.png)
│   └── Maps/
│       └── BackGroundAssets/    # Platform static meshes (.fbx) & HDRI (.hdr)
├── FX/
│   ├── Materials/               # FX material definitions (.mat.json)
│   ├── Meshes/                  # FX static meshes (.fbx)
│   └── Textures/                # FX masks, noise, trails, smoke, spark textures (.png)
├── Editor/
│   └── ParagonWraith_UnitySetup.cs   # Automated Unity setup helper script
├── manifest.json                # Complete export manifest and asset registry map
└── README.md
```

---

## How to Use in Unity

1. **Importing into Unity**:
   - Drag the entire `ParagonWraith` folder into your Unity project's `Assets/` directory (e.g. `Assets/ParagonWraith`).
2. **One-Click Automated Setup**:
   - In Unity's top menu bar, click **`Window > Paragon Wraith > Setup Assets for Unity`**.
   - Click **`Run Complete One-Click Setup`**:
     * Automatically sets `Wraith.fbx`, `Wraith_LunarOps.fbx`, and `Wraith_ODGreen.fbx` Rig type to **Humanoid** and creates Avatars.
     * Retargets all 229 `.fbx` animation clips to the Wraith Humanoid Avatar.
     * Configures all `*_N.png` and normal map textures as **Texture Type: Normal Map**.
3. **Materials**:
   - Each material has a corresponding `.mat.json` in the same folder listing the required texture maps (`BakedNormal`, `Albedo/BaseColor`, `EmissiveMask`, `Roughness/Metallic`).
   - Create a standard Unity Material (Standard or URP/Lit) and assign the matching textures.
4. **Animations**:
   - Drag `Wraith.fbx` into your scene, add an `Animator` component with an Animator Controller, and drag any of the 229 animations from `Characters/Heroes/Wraith/Animations/` into your Animator state machine.
