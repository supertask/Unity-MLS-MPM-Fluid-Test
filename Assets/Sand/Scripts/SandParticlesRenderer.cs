using UnityEngine;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEditor;

namespace MlsMpm.Sand
{
    [RequireComponent(typeof(GpuMpmParticleSystem))]
    public class SandParticlesRenderer : MonoBehaviour
    {
        [SerializeField] public Material material;
        [SerializeField] public float particleSize = 0.12f;
        [SerializeField] public Texture2D tex;
        [SerializeField] public Color color;
        GpuMpmParticleSystem mpmParticleSystem;
        public static class ShaderID
        {
            public static int ParticleSize = Shader.PropertyToID("_ParticleSize");
            public static int InvViewMatrix = Shader.PropertyToID("_InvViewMatrix");
            public static int MainTex = Shader.PropertyToID("_MainTex");
            public static int Color = Shader.PropertyToID("_Color");
        }

        void Start()
        {
            this.mpmParticleSystem = this.GetComponent<GpuMpmParticleSystem>();
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

            this.material.SetColor(ShaderID.Color, this.color);
            this.material.SetTexture(ShaderID.MainTex, this.tex);
            this.material.SetBuffer(GpuMpmParticleSystem.ShaderID.ParticlesBuffer, this.mpmParticleSystem.ParticlesBuffer);
            this.material.SetPass(0);

            Graphics.DrawProceduralNow(MeshTopology.Points, this.mpmParticleSystem.MaxNumOfParticles);
        }
    }
}