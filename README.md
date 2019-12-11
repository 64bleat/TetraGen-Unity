# TetraGen Isosurface Project
Generate isosurfaces from a 3D distance value array using the marching tetrahedra algorithm.

If you want to drop it into Unity and try it for yourself, check the releases!

How it Works
-
TetraGenShapes are placed as children under a TetraGenMaster, which will create a distance value
array and use the marching tetrahedra algorithm, located in the compute shader, to create a mesh.
