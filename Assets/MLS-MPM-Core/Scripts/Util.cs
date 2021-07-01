using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Mathematics;

namespace MlsMpm {

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

        //
        // Debug Compute Buffer
        // When you define a struct/class,
        // please use override ToString(), public override string ToString() => $"({A}, {B})";
        //
        public static void DebugBuffer<T>(ComputeBuffer buffer, int N) where T  : struct
        {
            T[] array = new T[N];
            buffer.GetData(array);
            for (int i = 0; i < N; i++)
            {
                Debug.LogFormat("index={0}: {1}", i, array[i]);
            }
        }

        public static void ReleaseBuffer(ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }
        }

    }

}