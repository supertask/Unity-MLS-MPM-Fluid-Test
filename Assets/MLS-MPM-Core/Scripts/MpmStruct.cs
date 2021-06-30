using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Mathematics;

namespace MlsMpm
{
    public struct MpmParticle
    {
        public enum Type
        {
            Inactive = 0,
            Elastic,
            Snow,
            Liquid,
        }
        public Type type;
        public float3 position; //12 byte
        public float3 velocity; //12 byte
        public float mass; //4 byte
        public float volume; //4 byte
        public float3x3 C; //4 * 9 byte
        public float3x3 Fe; //4 * 9 byte
        public float Jp; //4 byte
    };

    public struct MpmCell
    {
        public float mass; //4 byte
        public float3 mass_x_velocity; //12 byte
        public float3 velocity; //12 byte
        public float3 force; //12 byte
        public float2 padding; //8 byte
    };


    // Mpm Cell for interlocked add
    public struct MpmCellInterlocked
    {
        public int mass;
        public int3 mass_x_velocity;
        public float3 velocity;
        public float3 force;
        public float2 padding;
    };

    //For lock-free based GPU Optimization
    public struct P2GMass {
        public float mass;
        public float3 mass_x_velocity;
    }; 

}