Put terrain tile textures in this folder as JPG files named after the TerrainType enum:

- `Forest.jpg`
- `Hills.jpg`
- `Mountains.jpg`
- `Plains.jpg`

Use square images (for example, 512x512). The renderer maps the full image onto each hexagon and clips it to the hex shape. Missing files use the existing solid terrain colors.
