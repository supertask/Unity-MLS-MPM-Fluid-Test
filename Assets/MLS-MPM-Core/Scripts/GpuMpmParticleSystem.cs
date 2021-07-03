using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;

using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Assertions;

using ComputeShaderUtil;

namespace MlsMpm
{
    public class GpuMpmParticleSystem : MonoBehaviour
    {
        #region Asset reference
        [SerializeField] public ComputeShader particlesManagerCS;
        [SerializeField] public ComputeShader initGridCS;
        [SerializeField] public ComputeShader p2gCS;
        [SerializeField] public ComputeShader p2gScatteringOptCS;
        [SerializeField] public ComputeShader updateGridCS;
        [SerializeField] public ComputeShader gridToParticlesCS;
        [SerializeField] public float gridSpacingH = 0.5f; //0.5m
        [SerializeField] public int gridWidth = 80, gridHeight = 80, gridDepth = 80;
        [SerializeField] public float YoungsModulusE = 500f; //ヤング率
        [SerializeField] public float PoissonRatioNu = 0.2f; //ポアソン比, Nu -> uppercase Ν, lowercase ν
        [SerializeField] public float HyperElasticHardening = 10.0f; //ポアソン比, Nu -> uppercase Ν, lowercase ν
        [SerializeField] public float SphereRadius = 3;
        [SerializeField] public int MaxNumOfParticles = 1024;

        //[SerializeField] protected Grid<Cell> grid;
        #endregion


        #region Public members
        public ComputeBuffer ParticlesBuffer => particlesBuffer;
        public ComputeBuffer GridBuffer => gridBuffer;
        public int NumOfCells => numOfCells;

        public static int MAX_EMIT_NUM = 8;
        #endregion

        #region Private members
        private Kernel initParticlesKernel, emitParticlesKernel, copyParticlesKernel;
        private Kernel initGridKernel;
        private Kernel updateGridKernel, gridToParticlesKernel;
        private ComputeBuffer gridBuffer;
        private ComputeBuffer particlesBuffer;
        private ComputeBuffer sortedParticlesBuffer;
        private ComputeBuffer waitingParticleIndexesBuffer;
        private ComputeBuffer particleCountBuffer;

        private P2GModel p2gModel;

        private int[] particleCounts;
        private int numOfCells;
        private int numOfEmitParticles;
        #endregion

        #region Shader property IDs
        public static class ShaderID
        {
            public static int GridSpacingH = Shader.PropertyToID("_GridSpacingH");
            public static int GridResolutionWidth = Shader.PropertyToID("_GridResolutionWidth");
            public static int GridResolutionHeight = Shader.PropertyToID("_GridResolutionHeight");
            public static int GridResolutionDepth = Shader.PropertyToID("_GridResolutionDepth");
            public static int CellSpacingSize = Shader.PropertyToID("_CellSpacingSize"); // = h
            public static int CellStartPos = Shader.PropertyToID("_CellStartPos"); // I Dont know the meaning
            public static int DeltaTime = Shader.PropertyToID("_DeltaTime");
            public static int ParticleType = Shader.PropertyToID("_ParticleType");
            public static int SphereRadius = Shader.PropertyToID("_SphereRadius");

            public static int GridBuffer = Shader.PropertyToID("_GridBuffer");
            public static int ParticlesBuffer = Shader.PropertyToID("_ParticlesBuffer");
            public static int ParticlesBufferRead = Shader.PropertyToID("_ParticlesBufferRead");
            public static int ParticlesBufferWrite = Shader.PropertyToID("_ParticlesBufferWrite");
            public static int WaitingParticleIndexesBuffer = Shader.PropertyToID("_WaitingParticleIndexesBuffer");
            public static int PoolParticleIndexesBuffer = Shader.PropertyToID("_PoolParticleIndexesBuffer");
        }
        #endregion

        protected void OnEnable()
        {
            this.numOfCells = this.gridWidth * this.gridHeight * this.gridDepth;
            this.p2gModel = new P2GModel(this, this.MaxNumOfParticles, this.p2gCS, this.p2gScatteringOptCS);
            this.p2gModel.SetMediator(this);

            // Particles used on MPM
            MpmParticle[] mpmParticles = Enumerable.Range(0, this.MaxNumOfParticles)
                .Select(_ => new MpmParticle()).ToArray();
            this.particlesBuffer = new ComputeBuffer(this.MaxNumOfParticles, Marshal.SizeOf(typeof(MpmParticle)));
            this.particlesBuffer.SetData(mpmParticles);
            this.sortedParticlesBuffer = new ComputeBuffer(this.MaxNumOfParticles, Marshal.SizeOf(typeof(MpmParticle)));
            this.sortedParticlesBuffer.SetData(mpmParticles);

            // Dead list
            this.waitingParticleIndexesBuffer = new ComputeBuffer(
                this.MaxNumOfParticles, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
            this.waitingParticleIndexesBuffer.SetCounterValue(0); // IMPORTANT: Append/Consumeの追加削除位置を0に設定する

            // Grid used on MPM
            this.gridBuffer = new ComputeBuffer(this.numOfCells, Marshal.SizeOf(typeof(MpmCell)));
            this.gridBuffer.SetData(Enumerable.Range(0, this.numOfCells)
                .Select(_ => new MpmCell()).ToArray());

            // Particle's counter
            this.particleCountBuffer = new ComputeBuffer(4, Marshal.SizeOf(typeof(int)), ComputeBufferType.IndirectArguments);
            this.particleCounts = new int[] { 0, 1, 0, 0 };
            this.particleCountBuffer.SetData(particleCounts);


            this.initParticlesKernel = new Kernel(this.particlesManagerCS, "InitParticles");
            this.emitParticlesKernel = new Kernel(this.particlesManagerCS, "EmitParticles");
            this.copyParticlesKernel = new Kernel(this.particlesManagerCS, "CopyParticles");
            this.initGridKernel = new Kernel(this.initGridCS, "InitGrid");
            this.updateGridKernel = new Kernel(this.updateGridCS, "UpdateGrid");
            this.gridToParticlesKernel = new Kernel(this.gridToParticlesCS, "GridToParticle");

            this.numOfEmitParticles = (MAX_EMIT_NUM / (int) this.emitParticlesKernel.ThreadX) * (int)this.emitParticlesKernel.ThreadX;

            this.ComputeInitParticles();

        }


        protected void Update()
        {
            // Particle Counter
            particleCountBuffer.SetData(this.particleCounts);
            ComputeBuffer.CopyCount(this.waitingParticleIndexesBuffer, particleCountBuffer, 0);
            particleCountBuffer.GetData(this.particleCounts);

            this.ComputeEmitParticles();
            this.ComputeInitGrid();

            this.p2gModel.ComputeParticlesToGridGathering();
            //this.p2gModel.ComputeParticlesToGridScatteringOpt();

            this.ComputeUpdateGrid();
            this.ComputeGridToParticles();
        }

        // Inititialize entire particles and create waiting particle index list
        void ComputeInitParticles()
        {
            particlesManagerCS.SetBuffer(this.initParticlesKernel.Index,
                ShaderID.ParticlesBuffer, this.particlesBuffer);
            particlesManagerCS.SetBuffer(this.initParticlesKernel.Index,
                ShaderID.WaitingParticleIndexesBuffer, this.waitingParticleIndexesBuffer);
            particlesManagerCS.Dispatch(this.initParticlesKernel.Index,
                Mathf.CeilToInt(this.MaxNumOfParticles / (float)this.initParticlesKernel.ThreadX),
                (int)this.initParticlesKernel.ThreadY,
                (int)this.initParticlesKernel.ThreadZ);

        }

        void ComputeEmitParticles()
        {
            if (this.particleCounts[0] < this.numOfEmitParticles) { return; }

            this.particlesManagerCS.SetFloat(ShaderID.SphereRadius, this.SphereRadius);
            this.particlesManagerCS.SetInt(ShaderID.ParticleType, (int)MpmParticle.Type.Elastic);
            this.particlesManagerCS.SetBuffer(this.emitParticlesKernel.Index,
                ShaderID.PoolParticleIndexesBuffer, this.waitingParticleIndexesBuffer);
            this.particlesManagerCS.SetBuffer(this.emitParticlesKernel.Index,
                ShaderID.ParticlesBuffer, this.particlesBuffer);
            this.particlesManagerCS.Dispatch(this.emitParticlesKernel.Index,
                Mathf.CeilToInt(this.numOfEmitParticles / (float)this.emitParticlesKernel.ThreadX),
                (int)this.emitParticlesKernel.ThreadY,
                (int)this.emitParticlesKernel.ThreadZ);
            //Debug.Log("num: " + Mathf.CeilToInt(this.MaxNumOfParticles / (float)this.emitParticlesKernel.ThreadX) );

        }

        /* void ComputeCopyParticles(ref ComputeBuffer inBuffer, ref ComputeBuffer outBuffer)
        {
            this.particlesManagerCS.SetBuffer(this.copyParticlesKernel.Index,
                ShaderID.ParticlesBufferRead, inBuffer);
            this.particlesManagerCS.SetBuffer(this.copyParticlesKernel.Index,
                ShaderID.ParticlesBufferWrite, outBuffer);
            this.particlesManagerCS.Dispatch(this.copyParticlesKernel.Index,
                Mathf.CeilToInt(this.MaxNumOfParticles / (float)this.copyParticlesKernel.ThreadX),
                (int)this.copyParticlesKernel.ThreadY,
                (int)this.copyParticlesKernel.ThreadZ);
        }*/

        void ComputeSortByGridIndex()
        {
            //Debug.LogFormat("particle count: {0}, {1}, {2}, {3} ", this.particleCounts[0], this.particleCounts[1], this.particleCounts[2], this.particleCounts[3]);

            //Debug.Log("------------------");

            //this.ComputeCopyParticles(ref this.particlesBuffer, ref this.sortedParticlesBuffer);

            /*
            //this.sortedParticlesBuffer.GetData(particleData);
            for (int i = 512; i < 512+4; i++) {
                float3 pos = particleData[i].position;
                uint3 index3d = Util.ParticlePositionToCellIndex3D(pos, this.gridCellStartPos, this.gridSpacingH);
                uint cellIndex = Util.CellIndex3DTo1D(index3d, this.gridWidth, this.gridHeight);

                Debug.LogFormat("i={0}, particleData={1}, cellIndex={2}", i, particleData[i].position, cellIndex);
            }
            */

            // bitonic sort for Particles To Grid
            // particles will be sorted by grid index
            //this.gridOptimizer.SetCellStartPos(this.gridCellStartPos); //When the grid has moved by user, it will change
            //this.gridOptimizer.GridSort(ref this.particlesBuffer); 

            /*
            MpmParticle[] particleData = new MpmParticle[1024];
            //this.sortedParticlesBuffer.GetData(particleData);
            this.particlesBuffer.GetData(particleData);

            ComputeBuffer gridBuffer = this.gridOptimizer.GetGridBuffer();
            Uint2[] gridData = new Uint2[1024];
            gridBuffer.GetData(gridData);
            for (int i = 0; i < 100; i++) {
                if (0 < gridData[i].y && gridData[i].y < 512+4) {
                    Debug.LogFormat("GGGGGGG, gridIndex={0}, particleIndex={1}", gridData[i].x, gridData[i].y);

                    Uint2 gridAndParticlePair = gridData[i];
                    float3 pos = particleData[gridAndParticlePair.y].position;
                    uint3 index3d = Util.ParticlePositionToCellIndex3D(pos,
                            this.gridCellStartPos, this.gridSpacingH);
                    uint cellIndex = Util.CellIndex3DTo1D(index3d, this.gridWidth, this.gridHeight);

                    Debug.LogFormat("i={0}, gridCellIndex={1}, particleIndex={2}, particlePos={3}, cellIndex={4}: ",
                        i, gridAndParticlePair.x, gridAndParticlePair.y, pos, cellIndex );

                }
            }
            */

        }

        public void SetCommonParameters(ComputeShader target)
        {
            target.SetFloat(ShaderID.DeltaTime, Time.deltaTime);
            target.SetVector(ShaderID.CellStartPos, this.GetCellStartPos());
            target.SetFloat(ShaderID.GridSpacingH, this.gridSpacingH);
            target.SetInt(ShaderID.GridResolutionWidth, this.gridWidth);
            target.SetInt(ShaderID.GridResolutionHeight, this.gridHeight);
            target.SetInt(ShaderID.GridResolutionDepth, this.gridDepth);
        }

        void ComputeInitGrid()
        {
            this.SetCommonParameters(this.initGridCS);
            this.initGridCS.SetBuffer(this.initGridKernel.Index, ShaderID.GridBuffer, this.gridBuffer);
            this.initGridCS.Dispatch(this.initGridKernel.Index,
                Mathf.CeilToInt(this.numOfCells / (float)this.initGridKernel.ThreadX),
                (int)this.initGridKernel.ThreadY,
                (int)this.initGridKernel.ThreadZ);
        }



        void ComputeUpdateGrid()
        {
            this.SetCommonParameters(this.updateGridCS);
            this.updateGridCS.SetBuffer(this.updateGridKernel.Index, ShaderID.GridBuffer, this.gridBuffer);
            this.updateGridCS.Dispatch(this.updateGridKernel.Index,
                Mathf.CeilToInt(this.numOfCells / (float)this.updateGridKernel.ThreadX),
                (int)this.updateGridKernel.ThreadY,
                (int)this.updateGridKernel.ThreadZ);
        }


        void ComputeGridToParticles()
        {
            this.SetCommonParameters(this.gridToParticlesCS);
            this.gridToParticlesCS.SetBuffer(this.gridToParticlesKernel.Index, ShaderID.ParticlesBufferRead, this.particlesBuffer);
            this.gridToParticlesCS.SetBuffer(this.gridToParticlesKernel.Index, ShaderID.GridBuffer, this.gridBuffer);
            this.gridToParticlesCS.Dispatch(this.gridToParticlesKernel.Index,
                Mathf.CeilToInt(this.MaxNumOfParticles / (float)this.gridToParticlesKernel.ThreadX),
                (int)this.gridToParticlesKernel.ThreadY,
                (int)this.gridToParticlesKernel.ThreadZ);
        }

        public Bounds GetGridBounds()
        {
            return new Bounds(
                this.transform.position,
                this.gridSpacingH * this.GetGridDimension()
            );
        }

        public Vector3 GetGridDimension()
        {
            return new Vector3(this.gridWidth, this.gridHeight, this.gridDepth);
        }

        public Vector3 GetCellStartPos()
        {
            Bounds gridBounds = this.GetGridBounds();
            return gridBounds.center - gridBounds.extents;
        }

        protected void OnDisable()
        {
            this.ReleaseAll();
        }

        protected void OnDestroy()
        {
            this.ReleaseAll();
        }

        void OnDrawGizmosSelected()
        {
            // Draw sphere emitter as a test
            Gizmos.color = new Color(0, 1, 0, 1F);
            Gizmos.DrawWireSphere(this.transform.position, this.SphereRadius);

            // Draw MPM Grid
            Gizmos.color = new Color(0, 0, 1, 1F);

            Bounds bounds = this.GetGridBounds();
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        public void ReleaseAll()
        {
            Util.ReleaseBuffer(this.waitingParticleIndexesBuffer);
            Util.ReleaseBuffer(this.gridBuffer);
            Util.ReleaseBuffer(this.particlesBuffer);
            Util.ReleaseBuffer(this.sortedParticlesBuffer);
            Util.ReleaseBuffer(this.particleCountBuffer);

            this.p2gModel.ReleaseAll();
        }

    }
}