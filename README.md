# TetraGen isosurface mesh generation for Unity
Generate isosurface terrain and effects!
![img](.github/screenshot3.png)

## Features
* Generate infinite, streaming, procedural terrain in all directions
* Generate and export static meshes
* Fast enough to generate shape-changing meshes in real time
* Build your own organic geometry using signed distance field shapes
* **Fully customizeable algorithms!** Easily write your own compute shaders for:
  * Marching cube algorithms
  * Signed-distance field lattice positions
  * Signed-distance field shapes
  * Signed-distance field shape blend modes
 
## How it Works
* **TetraGenMaster** is paired with a TetraGenChunk to convert sets of shapes into meshes.
Its job is to determine which chunks need to be generated. The meshes can be generated and exported,
or left as-is while letting TetraGenMaster run uninterrupted.
* **TetraGenChunk** Everything needed to generate a mesh is contained in this Component.
It runs independently from TetraGenMaster and can be controlled by custom scripts.
* **TetraGenShapes** are Components that store signed-distance field shape information. The Transform's
position, rotation, and scale are incorporated into the shape by passing its *world2local* and *local2world*
matrices to the compute shader along with the rest of the information.
  * TetraGenMasters are given a transform containing a number of these in its children and are added in
    the order they appear in the GameObject heirarchy. 
  * TetraGenChunk is fed a list of these to generate the mesh.
  * Example: Attach TetraGenShapes to rigidbodies and pass them to a realtime TetraGenMaster for making fun moving shapes!
* **TGKernels** are used to point to kernels in compute shaders used for the four aspects of the generation.

## How the realtime TetraGenChunk generation procedure works
```csharp
Pseudocode:

class BasicallyWhatTetraGenMasterDoes : Monobehaviour
{
    TetraGenChunk chunk;

    void Start()
    {
        chunk.GenerationStart();
    }

    void Update()
    {
        chunk.SetTriangleBuffer(TetraGenShape[] shapes, ...)
        {
            chunk.TGPositionKernel.Dispatch(...);

            foreach(TetraGenShape shape in shapes)
            {
                shape.TGShapeKernel.Dispatch(...);
                shape.TGBlendKernel.Dispatch(...);
            }

            chunk.TGMeshKernel.Dispatch(...);
        }

        chunk.GenerateMeshes(...)
    }

    void OnDestroy()
    {
        chunk.GenerationEnd();
    }
}
```
