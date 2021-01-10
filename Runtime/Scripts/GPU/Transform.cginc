float3 transformPoint(float3 position, float4x4 transformMatrix)
{
	return mul(transformMatrix, float4(position, 1)).xyz;
}