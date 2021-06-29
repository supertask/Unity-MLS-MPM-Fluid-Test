﻿using UnityEngine;
using System.Runtime.InteropServices;

namespace NearestNeighbor {

    public class GridOptimizer3D<T> : GridOptimizerBase where T : struct {
        
        private Vector3 gridDim;

        public GridOptimizer3D(int numObjects, Vector3 range, Vector3 dimension) : base(numObjects) {
            this.gridDim = dimension;
            this.numGrid = (int)(dimension.x * dimension.y * dimension.z);
            this.gridH = range.x / gridDim.x;
            this.cellStartPos = Vector3.zero;

            this.GridSortCS = (ComputeShader)Resources.Load("GridSort3D");

            InitializeBuffer();

            Debug.Log("=== Instantiated Grid Sort === \nRange : " + range + "\nNumGrid : " + numGrid + "\nGridDim : " + gridDim + "\nGridH : " + gridH);
        }

        protected override void InitializeBuffer() {
            gridBuffer = new ComputeBuffer(numObjects, Marshal.SizeOf(typeof(Uint2)));
            gridPingPongBuffer = new ComputeBuffer(numObjects, Marshal.SizeOf(typeof(Uint2)));
            gridIndicesBuffer = new ComputeBuffer(numGrid, Marshal.SizeOf(typeof(Uint2)));
            sortedObjectsBufferOutput = new ComputeBuffer(numObjects, Marshal.SizeOf(typeof(T)));
        }

        public void SetCellStartPos(Vector3 cellStartPos) {
            this.cellStartPos = cellStartPos;
        }

        protected override void SetCSVariables() {
            GridSortCS.SetVector("_GridDim", gridDim);
            GridSortCS.SetFloat("_GridH", gridH);
            GridSortCS.SetVector("_CellStartPos", cellStartPos);
        }

    }
}