using UnityEngine;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEditor;

namespace MlsMpm
{
    [RequireComponent(typeof(GpuMpmParticleSystem))]
    public class GpuMpmGridRenderer : MonoBehaviour
    {
        [SerializeField] public Material material;
        [SerializeField] public float debugObjectSize = 0.01f;
        private GpuMpmParticleSystem mediator;

        public static class ShaderID
        {
            public static int DebugObjectSize = Shader.PropertyToID("_DebugObjectSize");
            public static int InvViewMatrix = Shader.PropertyToID("_InvViewMatrix");
        }
        void Start()
        {
            this.mediator = this.GetComponent<GpuMpmParticleSystem>();
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


            if (this.mediator.implementationType == ImplementationType.Gathering)
            {
                this.material.EnableKeyword(GpuMpmParticleSystem.ShaderKeyword.Gathering);
                this.material.DisableKeyword(GpuMpmParticleSystem.ShaderKeyword.LockScattering);
                this.material.DisableKeyword(GpuMpmParticleSystem.ShaderKeyword.LockFreeScattering);
            }
            else if (this.mediator.implementationType == ImplementationType.LockScattering)
            {
                this.material.DisableKeyword(GpuMpmParticleSystem.ShaderKeyword.Gathering);
                this.material.EnableKeyword(GpuMpmParticleSystem.ShaderKeyword.LockScattering);
                this.material.DisableKeyword(GpuMpmParticleSystem.ShaderKeyword.LockFreeScattering);
            }
            else if (this.mediator.implementationType == ImplementationType.LockFreeScattering)
            {
                this.material.DisableKeyword(GpuMpmParticleSystem.ShaderKeyword.Gathering);
                this.material.DisableKeyword(GpuMpmParticleSystem.ShaderKeyword.LockScattering);
                this.material.EnableKeyword(GpuMpmParticleSystem.ShaderKeyword.LockFreeScattering);
            }

            this.material.SetFloat(ShaderID.DebugObjectSize, this.debugObjectSize);
            this.material.SetVector(GpuMpmParticleSystem.ShaderID.CellStartPos, this.mediator.GetCellStartPos());
            this.material.SetFloat(GpuMpmParticleSystem.ShaderID.GridSpacingH, this.mediator.gridSpacingH);
            this.material.SetInt(GpuMpmParticleSystem.ShaderID.GridResolutionWidth, this.mediator.gridWidth);
            this.material.SetInt(GpuMpmParticleSystem.ShaderID.GridResolutionHeight, this.mediator.gridHeight);
            this.material.SetInt(GpuMpmParticleSystem.ShaderID.GridResolutionDepth, this.mediator.gridDepth);

            this.material.SetMatrix(ShaderID.InvViewMatrix, inverseViewMatrix);
            this.material.SetBuffer(GpuMpmParticleSystem.ShaderID.GridBuffer, this.mediator.GridBuffer);
            this.material.SetPass(0);

            /*
            Graphics.DrawProcedural(
                this.material,
                new Bounds(Vector3.zero, Vector3.one * 100f),
                MeshTopology.Points,
                this.mediator.MaxNumOfParticles);
            */
            Graphics.DrawProceduralNow(MeshTopology.Points, this.mediator.NumOfCells);
        }
    }
}