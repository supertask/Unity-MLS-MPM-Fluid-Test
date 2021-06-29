using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Mathematics;

namespace UnityMPM {

    public class Util
    {

        public static uint3 ParticlePositionToCellIndex3D(float3 pos,
            float3 _CellStartPos, float _GridSpacingH)
        {
            // return uint3(pos-_CellStartPos); // by Yuan
            float3 localPos = (pos-_CellStartPos) / _GridSpacingH;
            return (uint3)  ( localPos ); // by Yuan
        }

        public static uint CellIndex3DTo1D(uint3 idx,
            int _GridResolutionWidth, int _GridResolutionHeight)
        {
            return (uint) (idx.x + idx.y * _GridResolutionWidth +
                idx.z * _GridResolutionWidth * _GridResolutionHeight);
        }
    }

}