using UnityEngine;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEditor;

namespace UnityMPM
{
    [RequireComponent(typeof(GpuMpmParticleSystem))]
    public class GpuMpmGridRenderer : MonoBehaviour
    {
        [SerializeField] public Material material;
        [SerializeField] public float debugObjectSize = 0.01f;
        GpuMpmParticleSystem mpmParticleSystem;

        public static class ShaderID
        {
            public static int DebugObjectSize = Shader.PropertyToID("_DebugObjectSize");
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

            this.material.SetFloat(ShaderID.DebugObjectSize, this.debugObjectSize);
            this.material.SetVector(GpuMpmParticleSystem.ShaderID.CellStartPos,
                this.mpmParticleSystem.GridBounds.center - this.mpmParticleSystem.GridBounds.extents);
            this.material.SetFloat(GpuMpmParticleSystem.ShaderID.GridSpacingH, this.mpmParticleSystem.gridSpacingH);
            this.material.SetInt(GpuMpmParticleSystem.ShaderID.GridResolutionWidth, this.mpmParticleSystem.gridWidth);
            this.material.SetInt(GpuMpmParticleSystem.ShaderID.GridResolutionHeight, this.mpmParticleSystem.gridHeight);
            this.material.SetInt(GpuMpmParticleSystem.ShaderID.GridResolutionDepth, this.mpmParticleSystem.gridDepth);

            this.material.SetMatrix("_InvViewMatrix", inverseViewMatrix);
            this.material.SetBuffer(GpuMpmParticleSystem.ShaderID.GridBuffer, this.mpmParticleSystem.GridBuffer);
            this.material.SetPass(0);

            /*
            Graphics.DrawProcedural(
                this.material,
                new Bounds(Vector3.zero, Vector3.one * 100f),
                MeshTopology.Points,
                this.mpmParticleSystem.MaxNumOfParticles);
            */
            Graphics.DrawProceduralNow(MeshTopology.Points, this.mpmParticleSystem.NumOfCells);
        }
    }
}