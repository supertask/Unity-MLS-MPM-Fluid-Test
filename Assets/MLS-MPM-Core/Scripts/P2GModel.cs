using System.Linq;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.Mathematics;
using UnityEditor;

using ComputeShaderUtil;

namespace MlsMpm
{
    public class P2GModel
    {
        public static class ShaderID
        {
            public static int CellNeighborBuffer = Shader.PropertyToID("_CellNeighborBuffer");
            public static int P2GMassBuffer = Shader.PropertyToID("_P2GMassBuffer");
            public static int GridAndMassIdsBuffer = Shader.PropertyToID("_GridAndMassIdsBuffer");

            public static int HyperElasticHardening = Shader.PropertyToID("_HyperElasticHardening");
            public static int HyperElasticMu = Shader.PropertyToID("_HyperElasticMu");
            public static int HyperElasticLambda = Shader.PropertyToID("_HyperElasticLambda");

            public static int NumOfParticles = Shader.PropertyToID("_NumOfParticles");
            public static int NumOfP2GMasses = Shader.PropertyToID("_NumOfP2GMasses");
            public static int CellNeighborLength = Shader.PropertyToID("_CellNeighborLength");
        }
        private GpuMpmParticleSystem mediator;

        private ComputeShader p2gCS;
        private ComputeShader p2gScatteringOptCS;

        private ComputeBuffer cellNeighborBuffer;
        private ComputeBuffer gridAndMassIdsBuffer;
        private ComputeBuffer p2gMassBuffer;
        private Kernel p2gKernel, p2gScatteringOptKernel, p2gScatteringKernel;

        private int numOfP2GMasses;
        private int maxNumOfParticles;

        public int NumOfP2GMasses => numOfP2GMasses;

        public static int CELL_NEIGHBOR_LENGTH = 27;

        public P2GModel(int maxNumOfParticles, ComputeShader p2gCS, ComputeShader p2gScatteringOptCS)
        {
            this.p2gCS = p2gCS;
            this.p2gScatteringOptCS = p2gScatteringOptCS;
            this.maxNumOfParticles = maxNumOfParticles;
            this.numOfP2GMasses = maxNumOfParticles * CELL_NEIGHBOR_LENGTH;
            this.initBuffer();
        }

        public void SetMediator(GpuMpmParticleSystem mediator) {
            this.mediator = mediator;
        }

        private void initBuffer()
        {
            this.p2gKernel = new Kernel(this.p2gCS, "ParticleToGridGathering");
            this.p2gScatteringOptKernel = new Kernel(this.p2gScatteringOptCS, "ParticleToGridScatteringOpt");

            // Neighbor cell indexes
            int3[] cellNeighbors = new int3[CELL_NEIGHBOR_LENGTH];
            int index = 0;
            for(int x = -1; x <= 1; x++) for(int y = -1; y <= 1; y++) for(int z = -1; z <= 1; z++) {
                cellNeighbors[index] = new int3(x,y,z);
                //Debug.LogFormat("cellNeighbors: index, {0}", cellNeighbors[index]);
                index++;
            }
            this.cellNeighborBuffer = new ComputeBuffer(CELL_NEIGHBOR_LENGTH, Marshal.SizeOf(typeof(int3)));
            this.cellNeighborBuffer.SetData(cellNeighbors);

            //
            P2GMass[] p2gMasses = Enumerable.Range(0, this.numOfP2GMasses)
                .Select(_ => new P2GMass()).ToArray();
            this.p2gMassBuffer = new ComputeBuffer(this.numOfP2GMasses,
                Marshal.SizeOf(typeof(P2GMass)));
            this.p2gMassBuffer.SetData(p2gMasses);

            int2[] gridAndMassIds = Enumerable.Range(0, this.numOfP2GMasses)
                .Select(_ => new int2(-1,-1)).ToArray();
            this.gridAndMassIdsBuffer = new ComputeBuffer(this.numOfP2GMasses,
                Marshal.SizeOf(typeof(int2)));
            this.gridAndMassIdsBuffer.SetData(gridAndMassIds);

        }
        

        private void SetCommonP2GParameters(
            ComputeShader target
        ) {
            target.SetInt(ShaderID.NumOfParticles, this.maxNumOfParticles);
            target.SetFloat(ShaderID.HyperElasticHardening, this.mediator.HyperElasticHardening);
            target.SetFloat(ShaderID.HyperElasticMu,
                P2GModel.GetMu(this.mediator.YoungsModulusE, this.mediator.PoissonRatioNu));
            target.SetFloat(ShaderID.HyperElasticLambda,
                P2GModel.GetLambda(this.mediator.YoungsModulusE, this.mediator.PoissonRatioNu));
        }


        public void ComputeParticlesToGridGathering() {
            this.mediator.SetCommonParameters(this.p2gCS);
            this.SetCommonP2GParameters(this.p2gCS);

            this.p2gCS.SetBuffer(this.p2gKernel.Index,
                GpuMpmParticleSystem.ShaderID.ParticlesBufferRead, this.mediator.ParticlesBuffer);
            this.p2gCS.SetBuffer(this.p2gKernel.Index,
                GpuMpmParticleSystem.ShaderID.GridBuffer, this.mediator.GridBuffer);
            this.p2gCS.Dispatch(this.p2gKernel.Index,
                Mathf.CeilToInt(this.mediator.NumOfCells / (float)this.p2gKernel.ThreadX),
                (int)this.p2gKernel.ThreadY,
                (int)this.p2gKernel.ThreadZ);
            //this.DebugParticleBuffer();
        }


        public void ComputeParticlesToGridScatteringOpt() {
            // 1. Compute Particle to Grid
            this.mediator.SetCommonParameters(p2gScatteringOptCS);
            this.SetCommonP2GParameters(p2gScatteringOptCS);

            this.p2gScatteringOptCS.SetInt(ShaderID.CellNeighborLength, CELL_NEIGHBOR_LENGTH);
            this.p2gScatteringOptCS.SetBuffer(this.p2gScatteringOptKernel.Index,
                ShaderID.CellNeighborBuffer, this.cellNeighborBuffer);
            this.p2gScatteringOptCS.SetBuffer(this.p2gScatteringOptKernel.Index,
                ShaderID.GridAndMassIdsBuffer, this.gridAndMassIdsBuffer);
            this.p2gScatteringOptCS.SetBuffer(this.p2gScatteringOptKernel.Index,
                ShaderID.P2GMassBuffer, this.p2gMassBuffer);
            this.p2gScatteringOptCS.Dispatch(this.p2gScatteringOptKernel.Index,
                Mathf.CeilToInt(this.maxNumOfParticles / (float)this.p2gScatteringOptKernel.ThreadX),
                (int)this.p2gScatteringOptKernel.ThreadY,
                (int)this.p2gScatteringOptKernel.ThreadZ);
            //this.DebugParticleBuffer();

            /*
            //
            // 2. bitonic sort for Particles To Grid
            // particles will be sorted by grid index
            //
            this.gridOptimizer.SetCellStartPos(this.gridCellStartPos); //When the grid has moved by user, it will change
            this.gridOptimizer.GridSort(ref this.particlesBuffer); 

            //
            // 3. Calc boundary and region interval for No.4
            //

            //
            // 4. Calc parallel reduction sum and insert into grid
            //

            */
        }
        public void ReleaseAll()
        {
            Util.ReleaseBuffer(this.cellNeighborBuffer);
            Util.ReleaseBuffer(this.gridAndMassIdsBuffer);
            Util.ReleaseBuffer(this.p2gMassBuffer);
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