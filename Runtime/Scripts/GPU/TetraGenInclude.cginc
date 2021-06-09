// In from CPU
struct ShapeData
{
	float blendFactor;
	float bevelRadius;
	float4x4 world2Shape;
	float4x4 shape2World;
};

// Out to CPU
struct Triangle
{
	float3 a;
	float3 b;
	float3 c;
	float3 n;
	float3 nb;
	float3 nc;
};

// GPU internal
struct Cell
{
	float weight;
	float signWeight;
	float3 position;
	float3 normal;
};

// GPU internal
struct BlendCell
{
	float distance;
	float3 normal;
};

///BUFFERS (not shared between kernels)						// Used in
extern uniform StructuredBuffer<ShapeData> shapeBuffer;		//			shape			blend
//extern uniform RWStructuredBuffer<Cell> weightBuffer;		// lattice	shape	mesh	blend
//extern uniform RWStructuredBuffer<BlendCell> blendBuffer;       //			shape			blend
extern uniform RWStructuredBuffer<Triangle> tBuffer;		//					mesh
extern uniform RWStructuredBuffer<int> cellTriangleCount;	//					mesh

extern uniform RWTexture3D<float4> sdf_Position;
extern uniform RWTexture3D<float4> sdf_NormalDistance;
extern uniform RWTexture3D<float4> sdf_NormalDistance_Blend;

////GLOBAL UNIFORM (shared between kernels)					// Used in
extern uniform float4x4 chunk2World;						// lattice	shape
extern uniform float4x4 world2Master;						// lattice  shape
extern uniform int yBound, zBound;							// lattice	shape	mesh   blend
extern uniform int flipNormal;								//					mesh
extern uniform float3 cellScale;							// lattice

// Inverse Lerp specialized to find position of 0
inline float invLerp(float from, float to)
{
	return -from / (to - from);
}

inline float3 invLerp(float3 from, float3 to)
{
	return -from / (to - from);
}

// Summation used to easily convert 3d array index to 1d
inline uint summate(uint3 a)
{
	return a.x + a.y + a.z;
}

// Sets a value to 0 or 1
inline uint bin(float val)
{
	return saturate(sign(val));
}