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
        // debugging range is startIndex <= x < endIndex
        // example: 
        //    Util.DebugBuffer<uint2>(this.gridAndMassIdsBuffer, 1024, 1027); 
        //
        public static void DebugBuffer<T>(ComputeBuffer buffer, int startIndex, int endIndex) where T  : struct
        {
            int N = endIndex - startIndex;
            T[] array = new T[N];
            buffer.GetData(array, 0, startIndex, N);
            for (int i = 0; i < N; i++)
            {
                Debug.LogFormat("index={0}: {1}", startIndex + i, array[i]);
            }
        }


        /*
        // Mathf.NextPowerOfTwo
        //
        // Find the smallest power of two more than any number
        // example:
        //     n = 10
        //     Mathf.Log(n, 2) -> 3.3219
        //     Mathf.CeilToInt(3.3219) -> 4
        //     Mathf.Pow(2, 4) -> 16
        //     return 16
        //
        public static int NextPowerOfTwo(int n) {
            if (n <= 0) { return 0; }
            int k = Mathf.CeilToInt(Mathf.Log(n, 2));
            return Mathf.Pow(2, k);
        }
        */

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