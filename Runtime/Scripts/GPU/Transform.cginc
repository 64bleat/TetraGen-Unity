float3 transformPoint(float3 position, float4x4 transformMatrix)
{
	return mul(transformMatrix, float4(position, 1)).xyz;
}

	static const float3 v1 = float3( 1.0,-1.0,-1.0);
	static const float3 v2 = float3(-1.0,-1.0, 1.0);
	static const float3 v3 = float3(-1.0, 1.0,-1.0);
	static const float3 v4 = float3( 1.0, 1.0, 1.0);
	static const float e = 0.01;