using System.Linq;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.Mathematics;
using UnityEditor;

using ComputeShaderUtil;
using NearestNeighbour;

namespace MlsMpm
{
    public class P2GModel
    {
        public static class ShaderID
        {
            public static int CellNeighbourBuffer = Shader.PropertyToID("_CellNeighbourBuffer");
            public static int P2GMassBuffer = Shader.PropertyToID("_P2GMassBuffer");
            public static int SortedP2GMassBuffer = Shader.PropertyToID("_SortedP2GMassBuffer");
            public static int GridIndicesBuffer = Shader.PropertyToID("_GridIndicesBuffer");
            public static int GridAndMassIdsBuffer = Shader.PropertyToID("_GridAndMassIdsBuffer");
            public static int GridPingPongBuffer = Shader.PropertyToID("_GridPingPongBuffer");
            public static int BoundaryAndIntervalBuffer = Shader.PropertyToID("_BoundaryAndIntervalBuffer");

            public static int HyperElasticHardening = Shader.PropertyToID("_HyperElasticHardening");
            public static int HyperElasticMu = Shader.PropertyToID("_HyperElasticMu");
            public static int HyperElasticLambda = Shader.PropertyToID("_HyperElasticLambda");

            public static int NumOfParticles = Shader.PropertyToID("_NumOfParticles");
            public static int NumOfP2GMasses = Shader.PropertyToID("_NumOfP2GMasses");
            public static int CellNeighbourLength = Shader.PropertyToID("_CellNeighbourLength");
        }
        private GpuMpmParticleSystem mediator;

        private ComputeShader p2gCS;
        private ComputeShader p2gScatteringCS;
        private ComputeShader p2gScatteringOptCS;

        private ComputeBuffer cellNeighbourBuffer;
        private ComputeBuffer gridAndMassIdsBuffer;
        private ComputeBuffer gridPingPongBuffer;
        private ComputeBuffer p2gMassBuffer;
        private ComputeBuffer sortedP2gMassBuffer;
        private ComputeBuffer boundaryAndIntervalBuffer;
        private Kernel p2gGatheringKernel;
        private Kernel p2gScatteringKernel;
        private Kernel clearBuffersKernel, p2gScatteringOptKernel;
        private Kernel boundaryAndIntervalKernel, gatherAndWriteKernel;

        private GridOptimizer3D<P2GMass> gridOptimizer;

        private int numOfP2GMasses;
        private int maxNumOfParticles;

        public int NumOfP2GMasses => numOfP2GMasses;

        public static int CELL_NEIGHBOUR_LENGTH = 27;

        public P2GModel(
            GpuMpmParticleSystem mediator, int maxNumOfParticles,
            ComputeShader p2gCS, ComputeShader p2gScatteringCS, ComputeShader p2gScatteringOptCS)
        {
            this.SetMediator(mediator);
            this.p2gCS = p2gCS;
            this.p2gScatteringCS = p2gScatteringCS;
            this.p2gScatteringOptCS = p2gScatteringOptCS;
            this.maxNumOfParticles = maxNumOfParticles;
            this.numOfP2GMasses = Mathf.NextPowerOfTwo(maxNumOfParticles * CELL_NEIGHBOUR_LENGTH); // 2 ^ x
            this.initBuffer();

            // Bitonic sorter of grid
            this.gridOptimizer = new GridOptimizer3D<P2GMass>(
                this.numOfP2GMasses, this.mediator.GetGridBounds().size,
                this.mediator.GetGridDimension()
            );
        }

        public void SetMediator(GpuMpmParticleSystem mediator) {
            this.mediator = mediator;
        }

        private void initBuffer()
        {
            this.p2gGatheringKernel = new Kernel(this.p2gCS, "P2GGathering");
            this.p2gScatteringKernel = new Kernel(this.p2gScatteringCS, "P2GScattering");
            this.clearBuffersKernel = new Kernel(this.p2gScatteringOptCS, "ClearBuffers");
            this.p2gScatteringOptKernel = new Kernel(this.p2gScatteringOptCS, "P2GScatteringOpt");
            this.boundaryAndIntervalKernel = new Kernel(this.p2gScatteringOptCS, "BoundaryAndInterval");
            this.gatherAndWriteKernel = new Kernel(this.p2gScatteringOptCS, "GatherAndWrite");

            // Neighbour cell indexes
            int3[] cellNeighbours = new int3[CELL_NEIGHBOUR_LENGTH];
            int index = 0;
            for(int x = -1; x <= 1; x++) for(int y = -1; y <= 1; y++) for(int z = -1; z <= 1; z++) {
                cellNeighbours[index] = new int3(x,y,z);
                //Debug.LogFormat("cellNeighbours: index, {0}", cellNeighbours[index]);
                index++;
            }
            this.cellNeighbourBuffer = new ComputeBuffer(CELL_NEIGHBOUR_LENGTH, Marshal.SizeOf(typeof(int3)));
            this.cellNeighbourBuffer.SetData(cellNeighbours);
            //Util.DebugBuffer<int3>(this.cellNeighbourBuffer, 21, 27);

            //P2GMass[] p2gMasses = Enumerable.Range(0, this.numOfP2GMasses)
            //    .Select(_ => new P2GMass()).ToArray();
            this.p2gMassBuffer = new ComputeBuffer(this.numOfP2GMasses,
                Marshal.SizeOf(typeof(P2GMass)));
            //this.p2gMassBuffer.SetData(p2gMasses);

            this.sortedP2gMassBuffer = new ComputeBuffer(this.numOfP2GMasses,
                Marshal.SizeOf(typeof(P2GMass)));
            //this.sortedP2gMassBuffer.SetData(p2gMasses);

            //uint2[] gridAndMassIds = Enumerable.Range(0, this.numOfP2GMasses)
            //    .Select(_ => new uint2(uint.MaxValue, uint.MaxValue)).ToArray();
            this.gridAndMassIdsBuffer = new ComputeBuffer(this.numOfP2GMasses,
                Marshal.SizeOf(typeof(uint2)));
            //this.gridAndMassIdsBuffer.SetData(gridAndMassIds);
            this.gridPingPongBuffer = new ComputeBuffer(this.numOfP2GMasses,
                Marshal.SizeOf(typeof(uint2)));

            //uint2[] boundaryAndIntervals = Enumerable.Range(0, this.numOfP2GMasses)
            //    .Select(_ => new uint2(0,0)).ToArray();
            this.boundaryAndIntervalBuffer = new ComputeBuffer(this.numOfP2GMasses,
                Marshal.SizeOf(typeof(uint2)));
            //this.boundaryAndIntervalBuffer.SetData(boundaryAndIntervals);
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


        public void ComputeParticlesToGridGathering()
        {
            this.mediator.SetCommonParameters(this.p2gCS);
            this.SetCommonP2GParameters(this.p2gCS);

            this.p2gCS.SetBuffer(this.p2gGatheringKernel.Index,
                GpuMpmParticleSystem.ShaderID.ParticlesBufferRead, this.mediator.ParticlesBuffer);
            this.p2gCS.SetBuffer(this.p2gGatheringKernel.Index,
                GpuMpmParticleSystem.ShaderID.GridBuffer, this.mediator.GridBuffer);
            this.p2gCS.SetBuffer(this.p2gGatheringKernel.Index,
                GpuMpmParticleSystem.ShaderID.LockGridBuffer, this.mediator.LockGridBuffer);
            this.p2gCS.Dispatch(this.p2gGatheringKernel.Index,
                Mathf.CeilToInt(this.mediator.NumOfCells / (float)this.p2gGatheringKernel.ThreadX),
                (int)this.p2gGatheringKernel.ThreadY,
                (int)this.p2gGatheringKernel.ThreadZ);
        }


        public void ComputeParticlesToGridScattering()
        {
            this.mediator.SetCommonParameters(this.p2gScatteringCS);
            this.SetCommonP2GParameters(this.p2gScatteringCS);

            this.p2gScatteringCS.SetBuffer(this.p2gScatteringKernel.Index,
                GpuMpmParticleSystem.ShaderID.ParticlesBufferRead, this.mediator.ParticlesBuffer);
            this.p2gScatteringCS.SetBuffer(this.p2gScatteringKernel.Index,
                GpuMpmParticleSystem.ShaderID.GridBuffer, this.mediator.GridBuffer);
            this.p2gScatteringCS.SetBuffer(this.p2gScatteringKernel.Index,
                GpuMpmParticleSystem.ShaderID.LockGridBuffer, this.mediator.LockGridBuffer);
            this.p2gScatteringCS.Dispatch(this.p2gScatteringKernel.Index,
                Mathf.CeilToInt(this.mediator.MaxNumOfParticles / (float)this.p2gScatteringKernel.ThreadX),
                (int)this.p2gScatteringKernel.ThreadY,
                (int)this.p2gScatteringKernel.ThreadZ);

            if (Input.GetKeyDown(KeyCode.Space)) {
                Util.DebugBuffer<LockMpmCell>(this.mediator.LockGridBuffer, 0, this.mediator.NumOfCells);
                Debug.Log("---");
            }

        }
        public void ComputeParticlesToGridScatteringOpt()
        {
            //
            // 1. Compute Particle to Grid
            //
            this.mediator.SetCommonParameters(p2gScatteringOptCS);
            this.SetCommonP2GParameters(p2gScatteringOptCS);

            // 1.1. Clear buffers
            this.ClearBuffers();

            //存在するやつ
            //Util.DebugBuffer<MpmParticle>(this.mediator.ParticlesBuffer, 0, 3);
            //Util.DebugBuffer<int3>(this.cellNeighbourBuffer, 1, 3);
            this.ComputeP2GMass();

            //存在しないやつ
            int startIndex = 1024*20;
            //Util.DebugBuffer<uint2>(this.gridAndMassIdsBuffer, startIndex, startIndex+3);
            //Util.DebugBuffer<P2GMass>(this.p2gMassBuffer, startIndex, startIndex+3);

            //
            // 2. bitonic sort for Particles To Grid
            // particles will be sorted by grid index
            // output: gridAndMassIdsBuffer, sortedP2gMassBuffer
            //
            this.gridOptimizer.SetCellStartPos(this.mediator.GetCellStartPos()); //When the grid has moved by user, it will change

            // ここが原因でgridAndMassIdsなどが空になっている
            // ここが原因でプログラムが落ちたりしている
            this.gridOptimizer.GridSort(
                this.gridAndMassIdsBuffer, this.gridPingPongBuffer,
                this.p2gMassBuffer, this.sortedP2gMassBuffer); 


            //Debug.Log(numOfP2GMasses);
            //Util.DebugBuffer<uint2>(this.gridAndMassIdsBuffer, startIndex, startIndex+3);
            //Util.DebugBuffer<P2GMass>(this.sortedP2gMassBuffer, startIndex, startIndex+3);

            // 3. Calc boundary and region interval for No.4
            this.ComputeBoundaryAndInterval();

            //Util.DebugBuffer<uint2>(this.boundaryAndIntervalBuffer, startIndex, startIndex+10);

            // 4. Calc parallel reduction sum and insert into grid
            this.GatherAndWriteP2G();


            if (Input.GetKeyDown(KeyCode.Space)) {
                //index=0: uint2(0, 5357)
                // 0 ~ 5357個も空の状態がある <- 予想通り
                //Util.DebugBuffer<MpmParticle>(this.mediator.ParticlesBuffer, 100, 113);
                //Debug.Log("---");

                //Util.DebugBuffer<uint2>(this.gridOptimizer.GetGridIndicesBuffer(), 0, 30);
                //Debug.Log("---");

                //Util.DebugBuffer<LockMpmCell>(this.mediator.GridBuffer, 0, 30);
                //Debug.Log("---");

                //Util.DebugBuffer<uint2>(this.gridAndMassIdsBuffer, 0, numOfP2GMasses);
                //Debug.Log("---");
                //Util.DebugBuffer<uint2>(this.boundaryAndIntervalBuffer, 0, numOfP2GMasses);
                //Debug.Log("---");
                //Util.DebugBuffer<P2GMass>(this.p2gMassBuffer, 0, numOfP2GMasses);
                //Debug.Log("=========");
            }
        }

        public void ClearBuffers()
        {
            this.p2gScatteringOptCS.SetBuffer(this.clearBuffersKernel.Index,
                ShaderID.GridAndMassIdsBuffer, this.gridAndMassIdsBuffer);
            this.p2gScatteringOptCS.SetBuffer(this.clearBuffersKernel.Index,
                ShaderID.GridPingPongBuffer, this.gridPingPongBuffer);
            this.p2gScatteringOptCS.SetBuffer(this.clearBuffersKernel.Index,
                ShaderID.P2GMassBuffer, this.p2gMassBuffer);
            this.p2gScatteringOptCS.SetBuffer(this.clearBuffersKernel.Index,
                ShaderID.BoundaryAndIntervalBuffer, this.boundaryAndIntervalBuffer);
            this.p2gScatteringOptCS.Dispatch(this.clearBuffersKernel.Index,
                Mathf.CeilToInt(this.numOfP2GMasses / (float)this.clearBuffersKernel.ThreadX),
                (int)this.clearBuffersKernel.ThreadY,
                (int)this.clearBuffersKernel.ThreadZ);
        }

        public void ComputeP2GMass()
        {
            this.p2gScatteringOptCS.SetInt(ShaderID.CellNeighbourLength, CELL_NEIGHBOUR_LENGTH);
            this.p2gScatteringOptCS.SetInt(ShaderID.NumOfParticles, this.maxNumOfParticles);
            this.p2gScatteringOptCS.SetBuffer(this.p2gScatteringOptKernel.Index,
                GpuMpmParticleSystem.ShaderID.ParticlesBufferRead, this.mediator.ParticlesBuffer);
            this.p2gScatteringOptCS.SetBuffer(this.p2gScatteringOptKernel.Index,
                ShaderID.CellNeighbourBuffer, this.cellNeighbourBuffer);
            this.p2gScatteringOptCS.SetBuffer(this.p2gScatteringOptKernel.Index,
                ShaderID.GridAndMassIdsBuffer, this.gridAndMassIdsBuffer);
            this.p2gScatteringOptCS.SetBuffer(this.p2gScatteringOptKernel.Index,
                ShaderID.P2GMassBuffer, this.p2gMassBuffer);
            //this.p2gScatteringOptCS.Dispatch(this.p2gScatteringOptKernel.Index,
            //    Mathf.CeilToInt(this.numOfP2GMasses / (float)this.p2gScatteringOptKernel.ThreadX),
            //    (int)this.p2gScatteringOptKernel.ThreadY,
            //    (int)this.p2gScatteringOptKernel.ThreadZ);
            this.p2gScatteringOptCS.Dispatch(this.p2gScatteringOptKernel.Index,
                Mathf.CeilToInt(this.maxNumOfParticles / (float)this.p2gScatteringOptKernel.ThreadX),
                (int)this.p2gScatteringOptKernel.ThreadY,
                (int)this.p2gScatteringOptKernel.ThreadZ);
        }

        public void ComputeBoundaryAndInterval()
        {
            this.p2gScatteringOptCS.SetBuffer(this.boundaryAndIntervalKernel.Index,
                ShaderID.GridAndMassIdsBuffer, this.gridAndMassIdsBuffer);
            this.p2gScatteringOptCS.SetBuffer(this.boundaryAndIntervalKernel.Index,
                ShaderID.GridIndicesBuffer, this.gridOptimizer.GetGridIndicesBuffer());
            this.p2gScatteringOptCS.SetBuffer(this.boundaryAndIntervalKernel.Index,
                ShaderID.BoundaryAndIntervalBuffer, this.boundaryAndIntervalBuffer);
            this.p2gScatteringOptCS.Dispatch(this.boundaryAndIntervalKernel.Index,
                Mathf.CeilToInt(this.numOfP2GMasses / (float)this.boundaryAndIntervalKernel.ThreadX),
                (int)this.boundaryAndIntervalKernel.ThreadY,
                (int)this.boundaryAndIntervalKernel.ThreadZ);
        }


        public void GatherAndWriteP2G()
        {
            this.p2gScatteringOptCS.SetBuffer(this.gatherAndWriteKernel.Index,
                ShaderID.GridIndicesBuffer, this.gridOptimizer.GetGridIndicesBuffer());
            this.p2gScatteringOptCS.SetBuffer(this.gatherAndWriteKernel.Index,
                ShaderID.GridAndMassIdsBuffer, this.gridAndMassIdsBuffer);
            this.p2gScatteringOptCS.SetBuffer(this.gatherAndWriteKernel.Index,
                ShaderID.BoundaryAndIntervalBuffer, this.boundaryAndIntervalBuffer);

            //Here
            //たぶんここはバグってる
            this.p2gScatteringOptCS.SetBuffer(this.gatherAndWriteKernel.Index,
                ShaderID.SortedP2GMassBuffer, this.sortedP2gMassBuffer);
            //this.p2gScatteringOptCS.SetBuffer(this.gatherAndWriteKernel.Index,
            //    ShaderID.SortedP2GMassBuffer, this.p2gMassBuffer);

            this.p2gScatteringOptCS.SetBuffer(this.gatherAndWriteKernel.Index,
                GpuMpmParticleSystem.ShaderID.GridBuffer, this.mediator.GridBuffer);
            this.p2gScatteringOptCS.SetBuffer(this.gatherAndWriteKernel.Index,
                GpuMpmParticleSystem.ShaderID.LockGridBuffer, this.mediator.LockGridBuffer);
            this.p2gScatteringOptCS.Dispatch(this.gatherAndWriteKernel.Index,
                Mathf.CeilToInt(this.numOfP2GMasses / (float)this.gatherAndWriteKernel.ThreadX),
                (int)this.gatherAndWriteKernel.ThreadY,
                (int)this.gatherAndWriteKernel.ThreadZ);
            //Debug.LogFormat("numOfmasss: {0}", this.numOfP2GMasses / (float)this.gatherAndWriteKernel.ThreadX);
        }

        public void ReleaseAll()
        {
            Util.ReleaseBuffer(this.cellNeighbourBuffer);
            Util.ReleaseBuffer(this.gridAndMassIdsBuffer);
            Util.ReleaseBuffer(this.gridPingPongBuffer);
            Util.ReleaseBuffer(this.p2gMassBuffer);
            Util.ReleaseBuffer(this.sortedP2gMassBuffer);
            Util.ReleaseBuffer(this.boundaryAndIntervalBuffer);
            this.gridOptimizer.Release();
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