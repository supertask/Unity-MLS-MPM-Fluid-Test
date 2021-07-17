using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Mathematics;

namespace MlsMpm
{
    public enum ImplementationType
    {
        Gathering = 0,
        LockScattering,
        LockFreeScattering,
    }

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
        public override string ToString() => $"MpmParticle(type={type}, position={position}, velocity={velocity}, mass={mass})";
    };

    public struct MpmCell
    {
        public float mass; //4 byte
        public float3 mass_x_velocity; //12 byte
        public float3 velocity; //12 byte
        public float3 force; //12 byte
        public float2 padding; //8 byte

        public override string ToString() {
            return $"MpmCell(mass={mass}, mass_x_velocity={mass_x_velocity}, velocity={velocity}, force={force}, padding={padding})";
        }
    };


    // Mpm Cell for interlocked add
    public struct LockMpmCell
    {
        public int mass;
        public int mass2;
        public int3 mass_x_velocity;
        public int3 mass_x_velocity2;
        public float3 velocity;
        public float3 force;
        public float2 padding;

        public override string ToString() {
            return $"LockMpmCell(mass={mass}, mass2={mass2}, mass_x_velocity={mass_x_velocity}, mass_x_velocity2={mass_x_velocity2}, velocity={velocity}, force={force}, padding={padding})";
        }
    };

    //For lock-free based GPU Optimization
    public struct P2GMass {
        public float mass;
        public float3 mass_x_velocity;
        public override string ToString() => $"P2GMass(mass={mass}, mass_x_velocity={mass_x_velocity})";
    }; 

}