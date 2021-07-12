using System;
using UnityEngine;
using Unity.Mathematics;
using ComputeShaderUtil;

namespace Sorting.BitonicSort
{

    public class BitonicSort
    {
        //protected static readonly uint BITONIC_BLOCK_SIZE = 512;
        //protected static readonly uint TRANSPOSE_BLOCK_SIZE = 16;
        protected uint BITONIC_BLOCK_SIZE;
        protected uint TRANSPOSE_BLOCK_SIZE;

        protected ComputeShader BitonicCS;
        private Kernel bitonicSortKernel, matrixTransposeKernel;

        int numElements;

        public BitonicSort(int numElements)
        {
            this.BitonicCS = (ComputeShader)Resources.Load("BitonicSortCS");
            this.SetNumElements(numElements);
            this.bitonicSortKernel = new Kernel(this.BitonicCS, "BitonicSort");
            this.matrixTransposeKernel = new Kernel(this.BitonicCS, "MatrixTranspose");
            this.BITONIC_BLOCK_SIZE = this.bitonicSortKernel.ThreadX;
            this.TRANSPOSE_BLOCK_SIZE = this.matrixTransposeKernel.ThreadX;
            Debug.LogFormat("BITONIC_BLOCK_SIZE = {0}, TRANSPOSE_BLOCK_SIZE = {1}", BITONIC_BLOCK_SIZE, TRANSPOSE_BLOCK_SIZE);
        }

        public void SetNumElements(int numElements) {
            if (numElements % 2 == 0 && numElements > 0) {
                this.numElements = numElements;
            } else {
                throw new Exception("A num of object has be '2 ^ x' !!");
            }
        }

        public void Sort(ref ComputeBuffer inBuffer, ref ComputeBuffer tempBuffer)
        {
            ComputeShader sortCS = BitonicCS;

            //int KERNEL_ID_BITONICSORT = sortCS.FindKernel("BitonicSort");
            //int KERNEL_ID_TRANSPOSE = sortCS.FindKernel("MatrixTranspose");
            int KERNEL_ID_BITONICSORT = this.bitonicSortKernel.Index;
            int KERNEL_ID_TRANSPOSE = this.matrixTransposeKernel.Index;

            uint NUM_ELEMENTS = (uint)numElements;
            uint MATRIX_WIDTH = BITONIC_BLOCK_SIZE;
            uint MATRIX_HEIGHT = (uint)NUM_ELEMENTS / BITONIC_BLOCK_SIZE;
            //Debug.LogFormat("elments = {0}, blockH = {1}, blockW {2}", NUM_ELEMENTS, MATRIX_HEIGHT, MATRIX_WIDTH);

            for (uint level = 2; level <= BITONIC_BLOCK_SIZE; level <<= 1)
            {
                SetGPUSortConstants(sortCS, level, level, MATRIX_HEIGHT, MATRIX_WIDTH);

                // Sort the row data
                sortCS.SetBuffer(KERNEL_ID_BITONICSORT, "Data", inBuffer);
                sortCS.Dispatch(KERNEL_ID_BITONICSORT, (int)(NUM_ELEMENTS / BITONIC_BLOCK_SIZE), 1, 1);
            }

            // Then sort the rows and columns for the levels > than the block size
            // Transpose. Sort the Columns. Transpose. Sort the Rows.
            for (uint level = (BITONIC_BLOCK_SIZE << 1); level <= NUM_ELEMENTS; level <<= 1)
            {
                // Transpose the data from buffer 1 into buffer 2
                SetGPUSortConstants(sortCS, level / BITONIC_BLOCK_SIZE, (level & ~NUM_ELEMENTS) / BITONIC_BLOCK_SIZE, MATRIX_WIDTH, MATRIX_HEIGHT);
                sortCS.SetBuffer(KERNEL_ID_TRANSPOSE, "Input", inBuffer);
                sortCS.SetBuffer(KERNEL_ID_TRANSPOSE, "Data", tempBuffer);
                sortCS.Dispatch(KERNEL_ID_TRANSPOSE, (int)(MATRIX_WIDTH / TRANSPOSE_BLOCK_SIZE), (int)(MATRIX_HEIGHT / TRANSPOSE_BLOCK_SIZE), 1);

                //MlsMpm.Util.DebugBuffer<uint2>(tempBuffer, 1024, 1024+3);
                //MlsMpm.Util.DebugBuffer<uint2>(inBuffer, 1024, 1024+3);

                // Sort the transposed column data
                sortCS.SetBuffer(KERNEL_ID_BITONICSORT, "Data", tempBuffer);
                sortCS.Dispatch(KERNEL_ID_BITONICSORT, (int)(NUM_ELEMENTS / BITONIC_BLOCK_SIZE), 1, 1);

                // Transpose the data from buffer 2 back into buffer 1
                SetGPUSortConstants(sortCS, BITONIC_BLOCK_SIZE, level, MATRIX_HEIGHT, MATRIX_WIDTH);
                sortCS.SetBuffer(KERNEL_ID_TRANSPOSE, "Input", tempBuffer);
                sortCS.SetBuffer(KERNEL_ID_TRANSPOSE, "Data", inBuffer);
                sortCS.Dispatch(KERNEL_ID_TRANSPOSE, (int)(MATRIX_HEIGHT / TRANSPOSE_BLOCK_SIZE), (int)(MATRIX_WIDTH / TRANSPOSE_BLOCK_SIZE), 1);

                // Sort the row data
                sortCS.SetBuffer(KERNEL_ID_BITONICSORT, "Data", inBuffer);
                sortCS.Dispatch(KERNEL_ID_BITONICSORT, (int)(NUM_ELEMENTS / BITONIC_BLOCK_SIZE), 1, 1);
            }

        }

        void SetGPUSortConstants(ComputeShader cs, uint level, uint levelMask, uint width, uint height)
        {
            cs.SetInt("_Level", (int)level);
            cs.SetInt("_LevelMask", (int)levelMask);
            cs.SetInt("_Width", (int)width);
            cs.SetInt("_Height", (int)height);
        }

    }
}