# Unity Hologram VFX (Shader & VFX Graph)

> Ready-to-Use Modular Shader Graph Functions, Materials, and VFX Graph Setup

This package lets you easily add a sci-fi hologram effect to any model. It's performant and perfect for games, cinematics, or technical art demos.

**Demo Video:** [YouTube](https://www.youtube.com/watch?v=8y3PgXoZJTM)

**More Renders:** [ArtStation](https://www.artstation.com/artwork/XJL4ml)

### Overview

The **Hologram VFX** aims to be:
- **Modular** – works with any 3D model
- **Customizable** – tweakable shader and VFX parameters
- **Lightweight** – fully procedural, no heavy volumetrics
- **Educational** – breakdown of core hologram techniques for learning Shader Graph and VFX Graph

This effect is divided into three main components:
1. **Pattern** – scanlines, grid patterns, fresnel edge highlights, and tinting
2. **Displacement** – vertex-based distortion driven by noise or direction
3. **Emitter** – volumetric-style light projection and particle dust  

### Usage

#### Using the Material

You can use provided materials or build your own using the provided Shader Graphs. To use this material, 
simply drag this material to the object in Scene view or attach this Material to the MeshRenderer's material section.

#### Making your own Hologram Shader

Within the Shader Graph folder, there is provided a template called `Template_Hologram` which includes all the functionality
provided within this package. I recommend to read the comments and adjust as needed as it will help you with a starting point.
After making adjustments, you can create a material from this Shader Graph and attach it to the object for use.

#### Tweaking Material Properties

The VFX is completely procedural, meaning that you can tweak every parameter to produce different interesting results to craft new patterns,
displacement properties, and extra functionalities. To tweak these parameters, go to the Material and adjust the values as needed.

#### Using the Hologram Projection Ray VFX

To use the Hologram Projection Ray, drag the VFX GameObject into the Scene (can be found in VFX folder). It spawns a particle system for the
rays of light and some dust specs for extra visual flair. Adjust the values of these VFX within the Inspector as needed.

### Contacts

- Developer Email: [sekochi.dev@gmail.com](mailto:sekochi.dev@gmail.com)
- Asset Store Links: [Unity Asset Store](https://assetstore.unity.com/preview/344340/1165384)

