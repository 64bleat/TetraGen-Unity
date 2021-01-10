static const uint3 cubeMapNoise[] =
{
	uint3(0,0,0),
	uint3(1,0,0),
	uint3(0,1,0),
	uint3(1,1,0),
	uint3(0,0,1),
	uint3(1,0,1),
	uint3(0,1,1),
	uint3(1,1,1)
};

float3 smoothFrac(float3 value)
{
	return lerp(value * value, 1 - (1 - value) * (1 - value), value);
}

float rand3d1d(float3 position, float3 dotDir = float3(12.9898, 78.233, 37.719))
{
	return frac(sin(dot(sin(position), dotDir)) * 143758.5453);
}

float3 rand3d3d(float3 value)
{
	return float3(
		rand3d1d(value, float3(12.989, 78.233, 37.719)),
		rand3d1d(value, float3(39.346, 11.135, 83.155)),
		rand3d1d(value, float3(73.156, 52.235, 09.151)));
}

float3 noise3d3d(float3 value)
{
	float3 fracVal = frac(value);
	float3 floorVal = floor(value);
	float3 interpolator = smoothFrac(fracVal);
	float3 cellNoiseX0;
	float3 cellNoiseX1;
	float3 cellNoiseY0;
	float3 cellNoiseY1;
	float3 cellNoiseZ0;
	float3 cellNoiseZ1;

	//for x{0,1} for y{0,1} for z{0,1}
	cellNoiseX0 = rand3d3d(floorVal + cubeMapNoise[0]); //000
	cellNoiseX1 = rand3d3d(floorVal + cubeMapNoise[1]); //100
	cellNoiseY0 = lerp(cellNoiseX0, cellNoiseX1, interpolator.x);
	cellNoiseX0 = rand3d3d(floorVal + cubeMapNoise[2]); //010
	cellNoiseX1 = rand3d3d(floorVal + cubeMapNoise[3]); //110
	cellNoiseY1 = lerp(cellNoiseX0, cellNoiseX1, interpolator.x);
	cellNoiseZ0 = lerp(cellNoiseY0, cellNoiseY1, interpolator.y);
	cellNoiseX0 = rand3d3d(floorVal + cubeMapNoise[4]); //001
	cellNoiseX1 = rand3d3d(floorVal + cubeMapNoise[5]); //101
	cellNoiseY0 = lerp(cellNoiseX0, cellNoiseX1, interpolator.x);
	cellNoiseX0 = rand3d3d(floorVal + cubeMapNoise[6]); //011
	cellNoiseX1 = rand3d3d(floorVal + cubeMapNoise[7]); //111
	cellNoiseY1 = lerp(cellNoiseX0, cellNoiseX1, interpolator.x);
	cellNoiseZ1 = lerp(cellNoiseY0, cellNoiseY1, interpolator.y);
	return          lerp(cellNoiseZ0, cellNoiseZ1, interpolator.z);
}