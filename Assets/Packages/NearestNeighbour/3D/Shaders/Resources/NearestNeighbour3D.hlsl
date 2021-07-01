
// ---------------------
// Define Data structure (must be same as your particle data)
// ---------------------
/*
struct Data {
	float3 pos;
	float3 color;
};
*/
#include "Assets/MLS-MPM-Core/Shaders/MpmStruct.hlsl"


cbuffer grid {
	float3 _GridDim;
	float _GridH;
};

RWStructuredBuffer<uint2>	_GridIndicesBuffer;
float3 _CellStartPos;


// ��������Z����2�����C���f�b�N�X��Ԃ�
float3 GridCalculateCell(float3 pos) {
	return (pos - _CellStartPos) / _GridH;
}

// �Z����2�����C���f�b�N�X����1�����C���f�b�N�X��Ԃ�
uint GridKey(uint3 xyz) {
	return xyz.x + xyz.y * _GridDim.x + xyz.z * _GridDim.x * _GridDim.y;
}

// (�O���b�hID, �p�[�e�B�N��ID) �̃y�A���쐬����
uint2 MakeKeyValuePair(uint3 xyz, uint value) {
	// uint2([GridHash], [ParticleID]) 
	return uint2(GridKey(xyz), value);	// �t?
}

// �O���b�hID�ƃp�[�e�B�N��ID�̃y�A����O���b�hID�����𔲂��o��
uint GridGetKey(uint2 pair) {
	return pair.x;
}

// �O���b�hID�ƃp�[�e�B�N��ID�̃y�A����p�[�e�B�N��ID�����𔲂��o��
uint GridGetValue(uint2 pair) {
	return pair.y;
}

#define LOOP_AROUND_NEIGHBOUR(pos) int3 G_XYZ = (int3)GridCalculateCell(pos); for(int Z = max(G_XYZ.z - 1, 0); Z <= min(G_XYZ.z + 1, _GridDim.z - 1); Z++) for (int Y = max(G_XYZ.y - 1, 0); Y <= min(G_XYZ.y + 1, _GridDim.y - 1); Y++)  for (int X = max(G_XYZ.x - 1, 0); X <= min(G_XYZ.x + 1, _GridDim.x - 1); X++)