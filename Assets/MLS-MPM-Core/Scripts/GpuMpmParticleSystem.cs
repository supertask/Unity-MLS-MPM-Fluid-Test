using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;

using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Assertions;

using ComputeShaderUtil;


namespace UnityMPM
{
    //[StructLayout(LayoutKind.Sequential, Size = 108)]
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

    //[StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct MpmCell
    {
        public float mass; //4 byte
        public float3 mass_x_velocity; //12 byte
        public float3 velocity; //12 byte
        public float3 force; //12 byte
        public float2 padding; //8 byte
    };

    public class GpuMpmParticleSystem : MonoBehaviour
    {
        #region Asset reference
        [SerializeField] protected ComputeShader particlesManagerCS;
        [SerializeField] protected ComputeShader initGridCS;
        [SerializeField] protected ComputeShader particlesToGridCS;
        [SerializeField] protected ComputeShader updateGridCS;
        [SerializeField] protected ComputeShader gridToParticlesCS;
        [SerializeField] public float gridSpacingH = 0.5f; //0.5m
        [SerializeField] public int gridWidth = 80, gridHeight = 80, gridDepth = 80;
        [SerializeField] public float youngsModulusE = 1.4e4f; //ヤング率
        [SerializeField] public float poissonRatioNu = 0.2f; //ポアソン比, Nu -> uppercase Ν, lowercase ν
        [SerializeField] public float hyperElasticHardening = 10.0f; //ポアソン比, Nu -> uppercase Ν, lowercase ν
        [SerializeField] public float sphereRadius = 3;

        //[SerializeField] protected Grid<Cell> grid;
        #endregion

        #region Private members
        private Kernel initParticlesKernel, emitParticlesKernel;
        private Kernel initGridKernel, particlesToGridKernel, updateGridKernel, gridToParticlesKernel;
        private ComputeBuffer gridBuffer;
        private ComputeBuffer particlesBuffer;
        private ComputeBuffer waitingParticleIndexesBuffer;
        private ComputeBuffer particleCountBuffer;
        private int[] particleCounts;
        private int numOfCells;
        private int maxNumOfParticles;
        private Bounds gridBounds;
        #endregion

        #region Public members
        public ComputeBuffer ParticlesBuffer => particlesBuffer;
        public ComputeBuffer GridBuffer => gridBuffer;
        public int MaxNumOfParticles => maxNumOfParticles;
        public int NumOfCells => numOfCells;
        public Bounds GridBounds => gridBounds;
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


            public static int NumOfParticles = Shader.PropertyToID("_NumOfParticles");
            public static int ParticleType = Shader.PropertyToID("_ParticleType");


            public static int HyperElasticHardening = Shader.PropertyToID("_HyperElasticHardening");
            public static int HyperElasticMu = Shader.PropertyToID("_HyperElasticMu");
            public static int HyperElasticLambda = Shader.PropertyToID("_HyperElasticLambda");

            public static int SphereRadius = Shader.PropertyToID("_SphereRadius");

            public static int GridBuffer = Shader.PropertyToID("_GridBuffer");
            public static int ParticlesBuffer = Shader.PropertyToID("_ParticlesBuffer");
            public static int ParticlesBufferRead = Shader.PropertyToID("_ParticlesBufferRead");
            public static int WaitingParticleIndexesBuffer = Shader.PropertyToID("_WaitingParticleIndexesBuffer");
            public static int PoolParticleIndexesBuffer = Shader.PropertyToID("_PoolParticleIndexesBuffer");
        }
        #endregion

        protected void OnEnable()
        {
            this.maxNumOfParticles = 1000;
            this.numOfCells = this.gridWidth * this.gridHeight * this.gridDepth;

            // Particles used on MPM
            this.particlesBuffer = new ComputeBuffer(this.maxNumOfParticles,
                Marshal.SizeOf(typeof(MpmParticle)));
            this.particlesBuffer.SetData(Enumerable.Range(0, this.maxNumOfParticles)
                .Select(_ => new MpmParticle()).ToArray());
            this.waitingParticleIndexesBuffer = new ComputeBuffer(
                this.maxNumOfParticles, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
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
            this.initGridKernel = new Kernel(this.initGridCS, "InitGrid");
            this.particlesToGridKernel = new Kernel(this.particlesToGridCS, "ParticleToGrid");
            this.updateGridKernel = new Kernel(this.updateGridCS, "UpdateGrid");
            this.gridToParticlesKernel = new Kernel(this.gridToParticlesCS, "GridToParticle");

            this.ComputeInitParticles();
        }
        protected void Update()
        {
            this.gridBounds = this.GetGridBounds();
            this.ComputeEmitParticles();
            this.ComputeInitGrid();
            this.ComputeParticlesToGrid();
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
                Mathf.CeilToInt(this.maxNumOfParticles / (float)this.initParticlesKernel.ThreadX),
                (int)this.initParticlesKernel.ThreadY,
                (int)this.initParticlesKernel.ThreadZ);

            // Particle Counter
            particleCountBuffer.SetData(particleCounts);
            ComputeBuffer.CopyCount(this.waitingParticleIndexesBuffer, particleCountBuffer, 0);
            particleCountBuffer.GetData(particleCounts);
            //Debug.LogFormat("particle count: {0}, {1}, {2}, {3} ", particleCounts[0], particleCounts[1], particleCounts[2], particleCounts[3]);


            //this.DebugParticleBuffer();
            //this.DebugParticleIndexBuffer();
        }


        void ComputeEmitParticles()
        {
            this.particlesManagerCS.SetFloat(ShaderID.SphereRadius, this.sphereRadius);
            this.particlesManagerCS.SetInt(ShaderID.ParticleType, (int)MpmParticle.Type.Elastic);
            this.particlesManagerCS.SetBuffer(this.emitParticlesKernel.Index,
                ShaderID.PoolParticleIndexesBuffer, this.waitingParticleIndexesBuffer);
            this.particlesManagerCS.SetBuffer(this.emitParticlesKernel.Index,
                ShaderID.ParticlesBuffer, this.particlesBuffer);
            this.particlesManagerCS.Dispatch(this.emitParticlesKernel.Index,
                Mathf.CeilToInt(this.maxNumOfParticles / (float)this.emitParticlesKernel.ThreadX),
                (int)this.emitParticlesKernel.ThreadY,
                (int)this.emitParticlesKernel.ThreadZ);
            //Debug.Log("num: " + Mathf.CeilToInt(this.maxNumOfParticles / (float)this.emitParticlesKernel.ThreadX) );

            //this.DebugParticleBuffer();
        }

        void SetCommonParameters(ComputeShader target)
        {
            target.SetFloat(ShaderID.DeltaTime, Time.deltaTime);
            target.SetVector(ShaderID.CellStartPos, this.gridBounds.center - this.gridBounds.extents);
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

        void ComputeParticlesToGrid()
        {
            this.SetCommonParameters(this.particlesToGridCS);
            this.particlesToGridCS.SetInt(ShaderID.NumOfParticles, this.maxNumOfParticles);
            this.particlesToGridCS.SetFloat(ShaderID.HyperElasticHardening, this.hyperElasticHardening);
            this.particlesToGridCS.SetFloat(ShaderID.HyperElasticMu, GpuMpmParticleSystem.GetMu(youngsModulusE, poissonRatioNu));
            this.particlesToGridCS.SetFloat(ShaderID.HyperElasticLambda, GpuMpmParticleSystem.GetLambda(youngsModulusE, poissonRatioNu));

            this.particlesToGridCS.SetBuffer(this.particlesToGridKernel.Index, ShaderID.ParticlesBufferRead, this.particlesBuffer);
            this.particlesToGridCS.SetBuffer(this.particlesToGridKernel.Index, ShaderID.GridBuffer, this.gridBuffer);
            this.particlesToGridCS.Dispatch(this.particlesToGridKernel.Index,
                Mathf.CeilToInt(this.numOfCells / (float)this.particlesToGridKernel.ThreadX),
                (int)this.particlesToGridKernel.ThreadY,
                (int)this.particlesToGridKernel.ThreadZ);
            //this.DebugParticleBuffer();
        }

        void ComputeUpdateGrid()
        {
            this.SetCommonParameters(this.updateGridCS);
            this.updateGridCS.SetBuffer(this.updateGridKernel.Index, ShaderID.GridBuffer, this.gridBuffer);
            this.updateGridCS.Dispatch(this.updateGridKernel.Index,
                Mathf.CeilToInt(this.numOfCells / (float)this.updateGridKernel.ThreadX),
                (int)this.updateGridKernel.ThreadY,
                (int)this.updateGridKernel.ThreadZ);
            //this.DebugGridBuffer();
        }


        void ComputeGridToParticles()
        {
            this.SetCommonParameters(this.gridToParticlesCS);
            this.gridToParticlesCS.SetBuffer(this.particlesToGridKernel.Index, ShaderID.ParticlesBufferRead, this.particlesBuffer);
            this.gridToParticlesCS.SetBuffer(this.gridToParticlesKernel.Index, ShaderID.GridBuffer, this.gridBuffer);
            this.gridToParticlesCS.Dispatch(this.gridToParticlesKernel.Index,
                Mathf.CeilToInt(this.maxNumOfParticles / (float)this.gridToParticlesKernel.ThreadX),
                (int)this.gridToParticlesKernel.ThreadY,
                (int)this.gridToParticlesKernel.ThreadZ);

        }

        private Bounds GetGridBounds()
        {
            return new Bounds(
                this.transform.position,
                this.gridSpacingH * new Vector3(this.gridWidth, this.gridHeight, this.gridDepth)
            );
        }

        void DebugGridBuffer()
        {
            //int N = this.numOfCells;
            int N = 4;
            MpmCell[] cells = new MpmCell[N];
            this.gridBuffer.GetData(cells);
            for (int i = 0; i < N; i++)
            {
                Debug.LogFormat("cell: i = {0}, velocity = {1}, mass x velocity = {2}: ", i, cells[i].velocity, cells[i].mass_x_velocity);
            }
        }

        void DebugParticleBuffer()
        {
            int N = 4; //this.maxNumOfParticles
            MpmParticle[] particles = new MpmParticle[this.maxNumOfParticles];
            this.particlesBuffer.GetData(particles);
            for (int i = 0; i < N; i++)
            {
                Debug.LogFormat("particle: position = {0}, type = {1}, ", particles[i].position, particles[i].type);
            }
        }

        void DebugParticleIndexBuffer()
        {
            //int N = this.maxNumOfParticles;
            int N = 3;
            int[] particleIndexes = new int[N];
            this.waitingParticleIndexesBuffer.GetData(particleIndexes);
            for (int i = 0; i < N; i++)
            {
                Debug.Log("particle index: " + particleIndexes[i]);
            }
        }

        protected void OnDisable()
        {
            this.ReleaseAll();
        }

        void OnDrawGizmosSelected()
        {
            // Draw sphere emitter as a test
            Gizmos.color = new Color(0, 1, 0, 1F);
            Gizmos.DrawWireSphere(this.transform.position, this.sphereRadius);

            // Draw MPM Grid
            Gizmos.color = new Color(0, 0, 1, 1F);

            Bounds bounds = this.GetGridBounds();
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        public void ReleaseAll()
        {
            if (this.waitingParticleIndexesBuffer != null)
            {
                this.waitingParticleIndexesBuffer.Release();
                this.waitingParticleIndexesBuffer = null;
            }
            if (this.gridBuffer != null)
            {
                this.gridBuffer.Release();
                this.gridBuffer = null;
            }
            if (this.particlesBuffer != null)
            {
                this.particlesBuffer.Release();
                this.particlesBuffer = null;
            }
            if (this.particleCountBuffer != null)
            {
                this.particleCountBuffer.Release();
                this.particleCountBuffer = null;
            }
        }

        // Get the parameter, mu, of hyper elasticity
        // from youngsModulus(E) and poissonRatio(Nu)
        public static float GetMu(float E, float nu)
        {
            return E / (2f * (1f + nu));
        }

        // Get the parameter, lambda, of hyper elasticity
        // from youngsModulus(E) and poissonRatio(Nu)
        public static float GetLambda(float E, float nu)
        {
            return (E * nu) / ((1f + nu) * (1f - 2f * nu));
        }
    }
}