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
        [SerializeField] public float startParticleSize = 0.02f;
        [SerializeField] public float endParticleSize = 0.06f;
        [SerializeField] public float upresExtendSize = 0.02f;
        [SerializeField] public Texture2D tex;
        [SerializeField] public Color startColor;
        [SerializeField] public Color endColor;
        GpuMpmParticleSystem mpmParticleSystem;
        public static class ShaderID
        {
            public static int StartParticleSize = Shader.PropertyToID("_StartParticleSize");
            public static int EndParticleSize = Shader.PropertyToID("_EndParticleSize");
            public static int InvViewMatrix = Shader.PropertyToID("_InvViewMatrix");
            public static int MainTex = Shader.PropertyToID("_MainTex");
            public static int StartColor = Shader.PropertyToID("_StartColor");
            public static int EndColor = Shader.PropertyToID("_EndColor");
            public static int UpresExtendSize = Shader.PropertyToID("_UpresExtendSize");
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
            this.material.SetFloat(ShaderID.StartParticleSize, this.startParticleSize);
            this.material.SetFloat(ShaderID.EndParticleSize, this.endParticleSize);
            this.material.SetFloat(ShaderID.UpresExtendSize, this.upresExtendSize);

            this.material.SetColor(ShaderID.StartColor, this.startColor);
            this.material.SetColor(ShaderID.EndColor, this.endColor);
            this.material.SetTexture(ShaderID.MainTex, this.tex);
            this.material.SetBuffer(GpuMpmParticleSystem.ShaderID.ParticlesBuffer, this.mpmParticleSystem.ParticlesBuffer);
            this.material.SetPass(0);

            Graphics.DrawProceduralNow(MeshTopology.Points, this.mpmParticleSystem.MaxNumOfParticles);
        }
    }
}