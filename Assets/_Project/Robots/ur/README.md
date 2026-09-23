# cell_arm — de-branded UR5e

Source: https://github.com/ros-industrial/universal_robot, `ur_description`,
commit `39ad110d8f2e8f66856a201cca88aa7a7025e3eb` (20 Jul 2026).
Licence: BSD-3-Clause (see `LICENSE`); repository Apache-2.0. Credit is owed in the app's Credits panel.

## What we changed
1. `urdf/ur5e.xacro` was expanded to a plain URDF with the standalone `xacro` Python package
   (the importer cannot read xacro), saved as `cell_arm.urdf`.
2. Mesh references rewritten from `package://ur_description/meshes/...` to the relative `meshes/...`.
3. Robot name `ur5e_robot` → `cell_arm`.
4. **De-branded** per the build plan: in the visual meshes the material `URBlue` is renamed
   `CellAccent` and recoloured from UR's brand blue (0.49, 0.68, 0.80) to a neutral machine grey
   (0.753, 0.761, 0.769). Change that colour here if the demo wants a different accent.
   No UR logos, textures or decals exist in the UR5e meshes — only the newer UR15/18/20/30/8Long
   meshes carry branded texture maps, and those are excluded by the plan and not copied.

Geometry and kinematics are otherwise unmodified, so joint limits and link lengths stay true to a UR5e
(0.85 m reach), which is what the cell layout was sized against.
