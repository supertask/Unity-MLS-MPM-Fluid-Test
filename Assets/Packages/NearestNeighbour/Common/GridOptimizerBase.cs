using UnityEngine;
using Unity.Mathematics;

using Sorting.BitonicSort;
using MlsMpm;

public abstract class GridOptimizerBase {

    protected ComputeBuffer gridBuffer;
    protected ComputeBuffer gridPingPongBuffer;
    protected ComputeBuffer gridIndicesBuffer;
    protected ComputeBuffer sortedObjectsBufferOutput;

    protected int numObjects;

    BitonicSort bitonicSort;


    protected ComputeShader GridSortCS;
    protected static readonly int SIMULATION_BLOCK_SIZE_FOR_GRID = 32;
    

    protected int threadGroupSize;
    protected int numGrid;
    protected float gridH;
    protected Vector3 cellStartPos;

    public GridOptimizerBase(int numObjects) {
        this.numObjects = numObjects;

        this.threadGroupSize = numObjects / SIMULATION_BLOCK_SIZE_FOR_GRID;

        this.bitonicSort = new BitonicSort(numObjects);
    }

    #region Accessor
    public void SetNumObjects(int numObjects) {
        this.bitonicSort.SetNumElements(numObjects);
    }
    public float GetGridH() {
        return gridH;
    }

    public ComputeBuffer GetGridBuffer() {
        return this.gridBuffer;
    }

    public ComputeBuffer GetGridIndicesBuffer() {
        return this.gridIndicesBuffer;
    }
    #endregion

    public void Release() {
        DestroyBuffer(gridBuffer);
        DestroyBuffer(gridIndicesBuffer);
        DestroyBuffer(gridPingPongBuffer);
        DestroyBuffer(sortedObjectsBufferOutput);
    }

    void DestroyBuffer(ComputeBuffer buffer) {
        if (buffer != null) {
            buffer.Release();
            buffer = null;
        }
    }

    //public void GridSort(ref ComputeBuffer particlesBuffer) {
    //output: gridAndMassIdsBuffer, sortedP2gMassBuffer
    public void GridSort(
        ComputeBuffer gridAndMassIdsBuffer,
        ComputeBuffer gridPingPongBuffer2,
        ComputeBuffer p2gMassBuffer,
        ComputeBuffer sortedP2gMassBuffer) {

        GridSortCS.SetInt("_NumParticles", numObjects);
        SetCSVariables();

        int kernel = 0;

        #region GridOptimization

        /*
        // Build Grid
        // Convert Particle position to grid index and
        // insert the grid index into a grid cell.
        kernel = GridSortCS.FindKernel("BuildGridCS");
        GridSortCS.SetBuffer(kernel, "_ParticlesBufferRead", p2gMassBuffer);
        GridSortCS.SetBuffer(kernel, "_GridBufferWrite", gridBuffer);
        GridSortCS.Dispatch(kernel, threadGroupSize, 1, 1);
        */

        /*
        MlsMpm.MpmParticle[] particleData = new MlsMpm.MpmParticle[1024];
        p2gMassBuffer.GetData(particleData);

        uint2[] gridData = new uint2[1024];
        gridBuffer.GetData(gridData);
        for (int i = 512; i < 512+4; i++) {
            //if (512 <= gridData[i].y && gridData[i].y < 512+4) {
            //    Debug.LogFormat("GGGGGGG, gridIndex={0}, particleIndex={1}", gridData[i].x, gridData[i].y);
            //}
            uint2 gridAndParticlePair = gridData[i];
            float3 pos = particleData[gridAndParticlePair.y].position;
            uint3 index3d = MlsMpm.Util.ParticlePositionToCellIndex3D(pos,
                    this.cellStartPos, this.gridH);
            uint cellIndex = MlsMpm.Util.CellIndex3DTo1D(index3d, 64, 64);

            Debug.LogFormat("i={0}, gridCellIndex={1}, particleIndex={2}, particlePos={3}, cellIndex={4}: ",
                i, gridAndParticlePair.x, gridAndParticlePair.y, pos, cellIndex );
        }
        */

        int startIndex = 1024*4+300;
        //Util.DebugBuffer<uint2>(gridAndMassIdsBuffer, startIndex, startIndex+5);
        //Debug.Log("-----");

        //
        // Sort by grid index
        // output: gridAndMassIdsBuffer
        bitonicSort.Sort(ref gridAndMassIdsBuffer, ref gridPingPongBuffer);

        //startIndex = 1024*20;
        //Util.DebugBuffer<uint2>(gridAndMassIdsBuffer, startIndex, startIndex+5);
        //Debug.Log("==========");

        //
        // Build Grid Indices
        // input: gridAndMassIdsBuffer
        // output: gridIndicesBuffer
        //
        kernel = GridSortCS.FindKernel("ClearGridIndicesCS");
        GridSortCS.SetBuffer(kernel, "_GridIndicesBuffer", gridIndicesBuffer);
        GridSortCS.Dispatch(kernel, (int)(numGrid / SIMULATION_BLOCK_SIZE_FOR_GRID), 1, 1);
        kernel = GridSortCS.FindKernel("BuildGridIndicesCS");
        GridSortCS.SetBuffer(kernel, "_GridAndMassIdsBuffer", gridAndMassIdsBuffer);
        GridSortCS.SetBuffer(kernel, "_GridIndicesBuffer", gridIndicesBuffer);
        GridSortCS.Dispatch(kernel, threadGroupSize, 1, 1);

        //
        // Rearrange
        // input: gridAndMassIdsBuffer
        // output: gridIndicesBuffer
        //
        kernel = GridSortCS.FindKernel("RearrangeParticlesCS");
        GridSortCS.SetBuffer(kernel, "_GridAndMassIdsBuffer", gridAndMassIdsBuffer);
        GridSortCS.SetBuffer(kernel, "_P2GMassBuffer", p2gMassBuffer);
        GridSortCS.SetBuffer(kernel, "_SortedP2GMassBuffer", sortedP2gMassBuffer);
        GridSortCS.Dispatch(kernel, threadGroupSize, 1, 1);
        #endregion GridOptimization

        /*
        // Copy buffer
        kernel = GridSortCS.FindKernel("CopyBuffer");
        GridSortCS.SetBuffer(kernel, "_ParticlesBufferRead", sortedObjectsBufferOutput);
        GridSortCS.SetBuffer(kernel, "_ParticlesBufferWrite", p2gMassBuffer);
        GridSortCS.Dispatch(kernel, threadGroupSize, 1, 1);
        */

        //Util.DebugBuffer<uint2>(gridAndMassIdsBuffer, startIndex, startIndex+3);
    }

    #region GPUSort
    
    #endregion GPUSort 

    protected abstract void InitializeBuffer();

    protected abstract void SetCSVariables();
}
