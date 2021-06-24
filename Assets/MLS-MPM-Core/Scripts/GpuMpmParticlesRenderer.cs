using UnityEngine;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEditor;

namespace UnityMPM
{
    [RequireComponent(typeof(GpuMpmParticleSystem))]
    public class GpuMpmParticlesRenderer : MonoBehaviour
    {
        [SerializeField] public Material material;
        [SerializeField] public float particleSize = 0.03f;
        GpuMpmParticleSystem mpmParticles;
        public static class ShaderID
        {
            public static int ParticleSize = Shader.PropertyToID("_ParticleSize");
            public static int InvViewMatrix = Shader.PropertyToID("_InvViewMatrix");
        }

        void Start()
        {
            this.mpmParticles = this.GetComponent<GpuMpmParticleSystem>();
        }


        void OnRenderObject()
        {
            if (this.material == null) { return; }

            Matrix4x4 inverseViewMatrix;
            if (SceneView.lastActiveSceneView.hasFocus)
            {
                // If scene view is active, get foward direction of scene view
                inverseViewMatrix = SceneView.lastActiveSceneView.camera.worldToCameraMatrix.inverse;
            }
            else
            {
                // If game view is active, get foward direction of game view
                inverseViewMatrix = Camera.main.worldToCameraMatrix.inverse;
            }
            this.material.SetMatrix(ShaderID.InvViewMatrix, inverseViewMatrix);
            this.material.SetFloat(ShaderID.ParticleSize, this.particleSize);
            this.material.SetBuffer(GpuMpmParticleSystem.ShaderID.ParticlesBuffer, this.mpmParticles.ParticlesBuffer);
            this.material.SetPass(0);

            /*
            Graphics.DrawProcedural(
                this.material,
                new Bounds(Vector3.zero, Vector3.one * 100f),
                MeshTopology.Points,
                this.mpmParticles.MaxNumOfParticles);
            */
            Graphics.DrawProceduralNow(MeshTopology.Points, this.mpmParticles.MaxNumOfParticles);
        }
    }
}