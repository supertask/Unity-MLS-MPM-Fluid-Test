﻿#ifndef GRID_INCLUDED
#define GRID_INCLUDED

#include "./Constant.hlsl"
#include "./Math.hlsl"

float3 _CellStartPos;
uint _GridResolutionWidth;
uint _GridResolutionHeight;
uint _GridResolutionDepth;
float _GridSpacingH;

//
//
//
// example:
//     v = 10.25609895412
//     (int)10.25609890412 * 10000 -> 102560.989541 -> 102560
//     (int)0.989041 * 10000 -> 9895.41 -> 9895
//
//     102560 / 10000 -> 10.256
//     9895 / 100000000 -> 0.00009895
//     10.256 + 0.00009895 -> 10.25609895
//     
// BTW max value of int: 2147483647
//
int2 ConvertFloatToInt2(float v)
{
	float f1 = v * F2I_DIGIT;
    int i1 = (int)f1;
	float f2 = (f1 - i1) * F2I_DIGIT; 
    int i2 = (int)f2;
	return int2( i1, i2 );
}

float ConvertInt2ToFloat(int2 v) {
	float f1 = ((float)v.x) / F2I_DIGIT;
	float f2 = ((float)v.y) / (F2I_DIGIT * F2I_DIGIT);
	return f1 + f2;
}

float3 ConvertInt3x2ToFloat3(int3 v1, int3 v2) {
	return float3(
		ConvertInt2ToFloat(int2(v1.x, v2.x)),
		ConvertInt2ToFloat(int2(v1.y, v2.y)),
		ConvertInt2ToFloat(int2(v1.z, v2.z))
	);
}

inline bool Is2D()
{
	return _GridResolutionDepth == 1;
}
void Set3DZero(inout float3x3 mat)
{
	mat[2] = mat[0][2] = mat[1][2] = 0;
}
inline float N(float x)
{
    x = abs(x);

    if (x < 0.5f) return 0.75f - x * x;
    if (x < 1.5f) return 0.5f * (1.5f - x) * (1.5f - x);
    return 0;
}

inline float DevN(float x)
{
    float absx = abs(x);
    if (absx < 0.5f) return -2 * x;
    if (absx < 1.5f) return x > 0 ? absx - 1.5f : -(absx - 1.5f);
    return 0;
}

// Inverse D matrix of APIC fluid
inline float3x3 InvApicD()
{
	//float3x3 apicD = (1 / 4.0f) * _GridSpacingH * _GridSpacingH * Identity3x3;
	//return inverse(apicD);

	// Same meaning with equation above
	float invH = 1 / _GridSpacingH;
	return 4.0f * invH * invH * Identity3x3; // identiy matrix 3x3
}

inline uint3 ParticlePositionToCellIndex3D(float3 pos)
{
	// return uint3(pos-_CellStartPos); // by Yuan
	 return uint3( (pos-_CellStartPos) / _GridSpacingH ); // by Tasuku
}

inline uint CellIndex3DTo1D(uint3 idx)
{
	return idx.x + idx.y * _GridResolutionWidth + idx.z * _GridResolutionWidth * _GridResolutionHeight;
}

inline uint3 CellIndex1DTo3D(uint idx)
{
	uint z = idx/(_GridResolutionWidth * _GridResolutionHeight);
	uint xy = idx%(_GridResolutionWidth * _GridResolutionHeight);

	return uint3(xy%_GridResolutionWidth, xy/_GridResolutionWidth, z);
}


// 動かない原因はここっぽい
inline float3 CellIndex3DToPositionWS(uint3 idx)
{
	//return _CellStartPos + (idx + 0.5f) * ; //by Yuan
	float halfH = _GridSpacingH / 2;
	return _CellStartPos + (idx + halfH) * _GridSpacingH;
	//return _CellStartPos + (idx + 0.5);
}

inline bool InGrid(uint3 idx)
{
	uint cdid = CellIndex3DTo1D(idx);
	return 0<= cdid && cdid < _GridResolutionWidth * _GridResolutionHeight *_GridResolutionDepth;
}
/*
inline float GetWeightWithCell(float3 pos, int3 cellIndex)
{
	if (!InGrid(cellIndex)) return 0;

	float3 gpos = CellIndex3DToPositionWS(cellIndex);
	float3 dis = pos - gpos;
	float3 invH = 1.0f / _GridSpacingH;
	dis *= invH;

	return  N(dis.x) * N(dis.y) *(Is2D()?1: N(dis.z));
}
*/
inline float GetWeight(float3 pos, int3 delta)
{
	int3 gindex = ParticlePositionToCellIndex3D(pos) + delta;
	if (!InGrid(gindex)) return 0;

	float3 gpos = CellIndex3DToPositionWS(gindex);
	float3 dis = pos - gpos;

	float3 invH = 1.0f / _GridSpacingH;
	dis *= invH;

	return  N(dis.x) * N(dis.y) * N(dis.z);
}
/*
inline float3 GetWeightGradient(float3 pos, int3 delta)
{
	int3 gindex = ParticlePositionToCellIndex3D(pos) + delta;
	if (!InGrid(gindex)) return 0;

	float3 gpos = CellIndex3DToPositionWS(gindex);
	float3 dis = pos - gpos;
	float3 invH = 1.0f / _GridSpacingH;
	dis *= invH;

	float wx = N(dis.x);
	float wy = N(dis.y);
	float wz = N(dis.z);

	float wdx = DevN(dis.x);
	float wdy = DevN(dis.y);
	float wdz = DevN(dis.z);

	return invH * float3(wdx * wy * wz, wx * wdy * wz, wx * wy * wdz);
}
*/
#endif