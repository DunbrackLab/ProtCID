using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrystalInterfaceLib.Crystal;

namespace CrystalInterfaceLib.StructureComp
{
    /* Align two chains with rotations and translation
     * Align two chains by dynamic programming
     * local alignment: Smith-Waterman algorithm
     * global alignment: Needleman/Wunsch algorithm
     * based on Calpha distance
     * Si,j = ln(3/d) + 1;
     * */
    public class StructAlign
    {
        #region member variables
        private const double gapPenalty = -3.0;
        private const double pseudocount = 1.0;
        private RmsdCalculator rmsdCalculator = new RmsdCalculator();
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain1"></param>
        /// <param name="chain2"></param>
        /// <param name="modelType">local, or global</param>
        /// <returns></returns>
        public StructAlignOutput AlignTwoChains(ChainAtoms chain1, ChainAtoms chain2, string modelType)
        {
            AtomInfo[] calphaAtoms1 = chain1.CalphaAtoms();
            AtomInfo[] calphaAtoms2 = chain2.CalphaAtoms();
            StructAlignOutput alignOutput = AlignTwoSetsAtoms(calphaAtoms1, calphaAtoms2, modelType);
            return alignOutput;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain1"></param>
        /// <param name="chain2"></param>
        public StructAlignOutput AlignTwoSetsAtoms(AtomInfo[] atoms1, AtomInfo[] atoms2, string modelType)
        {
            Coordinate[] coords1 = GetCoordinates(atoms1);
            Coordinate[] coords2 = GetCoordinates(atoms2);

            StructAlignOutput alignOutput = AlignTwoSetsCoordinates(coords1, coords2, modelType);
            if (alignOutput != null)
            {
                int startPos = -1;
                int endPos = -1;
                alignOutput.alignment1 = ChangeOutputAlignmentToResidues(atoms1, alignOutput.alignment1, out startPos, out endPos);
                alignOutput.startPos1 = startPos;
                alignOutput.endPos1 = endPos;
                alignOutput.alignment2 = ChangeOutputAlignmentToResidues(atoms2, alignOutput.alignment2, out startPos, out endPos);
                alignOutput.startPos2 = startPos;
                alignOutput.endPos2 = endPos;
            }
           
            return alignOutput;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atoms"></param>
        /// <param name="indexAlignment"></param>
        /// <returns></returns>
        private string ChangeOutputAlignmentToResidues(AtomInfo[] atoms, string indexAlignment, out int startPos, out int endPos)
        {
            string[] indexFields = indexAlignment.Split(',');
            startPos = -1;
            endPos = -1;
            string residueAlignment = "";
            int index = 0;
            for (int i = 0; i < indexFields.Length; i++)
            {
                if (indexFields[i] != "-")
                {
                    index = Convert.ToInt32(indexFields[i]);
                    residueAlignment += (atoms[index].residue + ",");  // should contain one letter code
                    if (startPos == -1)
                    {
                        startPos = Convert.ToInt32(atoms[index].seqId);
                    }
                    endPos = Convert.ToInt32(atoms[index].seqId);
                }
                else
                {
                    residueAlignment += "-,";
                }
            }
            residueAlignment = residueAlignment.TrimEnd(',');
            return residueAlignment;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atoms"></param>
        /// <returns></returns>
        private Coordinate[] GetCoordinates(AtomInfo[] atoms)
        {
            Coordinate[] coords = new Coordinate[atoms.Length];
            for (int i = 0; i < atoms.Length; i++)
            {
                coords[i] = atoms[i].xyz;
            }
            return coords;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain1"></param>
        /// <param name="chain2"></param>
        public StructAlignOutput AlignTwoSetsCoordinates(Coordinate[] coords1, Coordinate[] coords2, string modelType)
        {
            double bestScore = -1;
            int endI = -1;
            int endJ = -1;
            double[,] distanceMatrix = CreateDistanceMatrix(coords1, coords2);
            List<int> indexListI = new List<int>();
            List<int> indexListJ = new List<int>();

            // the dp matrix is one more than for both lists of the coords
            if (modelType == "local")
            {
                double[,] dpMatrix = CreateLocalMatrix(coords1.Length, coords2.Length, distanceMatrix, out bestScore, out endI, out endJ);
                AlignLocal(dpMatrix, endI, endJ, distanceMatrix, ref indexListI, ref indexListJ);
            }
            else
            {
                double[,] dpMatrix = CreateGlobalMatrix(coords1.Length, coords2.Length, distanceMatrix, out bestScore, out endI, out endJ);
                AlignGlobal(dpMatrix, endI, endJ, distanceMatrix, ref indexListI, ref indexListJ);
            }
            if (indexListI.Count == 0 || indexListJ.Count == 0)  // no local alignment
            {
                return null;
            }

            int[] indexesI = new int[indexListI.Count];
            indexListI.CopyTo(indexesI);
            int[] indexesJ = new int[indexListJ.Count];
            indexListJ.CopyTo(indexesJ);

            StructAlignOutput alignOutput = new StructAlignOutput();
            alignOutput.startPos1 = indexesI[0];
            alignOutput.endPos1 = indexesI[indexesI.Length - 1];
            alignOutput.startPos2 = indexesJ[0];
            alignOutput.endPos2 = indexesJ[indexesJ.Length - 1];
            alignOutput.alignScore = bestScore;
            alignOutput.rmsd = 0;
            string alignment1 = "";
            string alignment2 = "";
            int coordI = 0;
            int coordJ = 0;
            List<Coordinate> rmsdCoordsList1 = new List<Coordinate> ();
            List<Coordinate> rmsdCoordsList2 = new List<Coordinate> ();
            for (int i = 0; i < indexesI.Length; i++)
            {
                if (indexesI[i] > -1)
                {
                    coordI = indexesI[i] - 1;
                    alignment1 += (coordI.ToString () + ",");
                }
                else
                {
                    alignment1 += "-,";
                }
                if (indexesJ[i] > -1)
                {
                    coordJ = indexesJ[i] - 1;
                    alignment2 += (coordJ.ToString() + ",");
                }
                else
                {
                    alignment2 += "-,";
                }

                if (indexesI[i] > -1 && indexesJ[i] > -1)
                {
                    rmsdCoordsList1.Add(coords1[coordI]);
                    rmsdCoordsList2.Add(coords2[coordJ]);
                }
            }
            double rmsd = rmsdCalculator.CalculateRmsd(rmsdCoordsList1.ToArray(), rmsdCoordsList2.ToArray ());
            alignOutput.rmsd = rmsd;
            alignOutput.alignment1 = alignment1.TrimEnd (',');
            alignOutput.alignment2 = alignment2.TrimEnd (',');
            return alignOutput;
        }

        #region create matrix
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain1"></param>
        /// <param name="chain2"></param>
        /// <returns></returns>
        private double[,] CreateDistanceMatrix(Coordinate[] chain1, Coordinate[] chain2)
        {
            double[,] distanceMatrix = new double[chain1.Length, chain2.Length];
            double sij = 0;
            for (int i = 0; i < chain1.Length; i++)
            {
                for (int j = 0; j < chain2.Length; j++)
                {
                    sij = GetSIJ (chain1[i], chain2[j]);
                    distanceMatrix[i, j] = sij;
                }
            }
            return distanceMatrix;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain1"></param>
        /// <param name="chain2"></param>
        /// <returns></returns>
        public double[,] CreateLocalMatrix(int length1, int length2, double[,] distanceMatrix, out double bestScore, out int endI, out int endJ)
        {
            double[,] dpMatrix = new double[length1 + 1, length2 + 1];
            double mij = 0;
            double sij = 0;
            bestScore = -1;
            endI = 0;
            endJ = 0;
            for (int i = 0; i < length1 + 1; i++)
            {
                for (int j = 0; j < length2 + 1; j++)
                {
                    if (i == 0 || j == 0)
                    {
                        dpMatrix[i, j] = 0;
                    }
                    else
                    {
                        sij = distanceMatrix[i - 1, j - 1];
                        mij = CalculateLocalCellValue(dpMatrix, i, j, sij);
                        dpMatrix[i, j] = mij;

                        if (mij > bestScore)
                        {
                            bestScore = mij;
                            endI = i;
                            endJ = j;
                        }
                    }
                }
            }
            return dpMatrix;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbMatrix"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="sij"></param>
        /// <returns></returns>
        private double CalculateLocalCellValue(double[,] dpMatrix, int i, int j, double sij)
        {
            double mij = 0;
            double mij_diag = dpMatrix[i - 1, j - 1] + sij;
            double mij_i = dpMatrix[i, j - 1] + gapPenalty;
            double mij_j = dpMatrix[i - 1, j] + gapPenalty;

            mij = Math.Max(mij_diag, mij_i);
            mij = Math.Max(mij, mij_j);
            mij = Math.Max(mij, 0);
            return mij;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain1"></param>
        /// <param name="chain2"></param>
        /// <returns></returns>
        public double[,] CreateGlobalMatrix(int length1, int length2, double[,] distanceMatrix, out double bestScore, out int endI, out int endJ)
        {
            double[,] dpMatrix = new double[length1, length2];
            double mij = 0;
            double sij = 0;
            bestScore = -9999999.0;
            endI = 0;
            endJ = 0;
            for (int i = 0; i < length1; i++)
            {
                for (int j = 0; j < length2; j++)
                {
                /*    if (i == 0)
                    {
                        dpMatrix[i, j] = j * gapPenalty;
                    }
                    else if (j == 0)
                    {
                        dpMatrix[i, j] = i * gapPenalty;
                    }*/
                    if (i == 0 || j == 0)  // no opening penalty for the gap, since peptide can be aligned to any starting of a chain
                    {
                        dpMatrix[i, j] = 0;
                    }
                    else
                    {
                        sij = distanceMatrix[i - 1, j - 1];
                        mij = CalculateGlobalCellValue(dpMatrix, i, j, sij);
                        dpMatrix[i, j] = mij;

                        if (mij > bestScore)
                        {
                            bestScore = mij;
                            endI = i;
                            endJ = j;
                        }
                    }
                }
            }
            return dpMatrix;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbMatrix"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="sij"></param>
        /// <returns></returns>
        private double CalculateGlobalCellValue(double[,] dpMatrix, int i, int j, double sij)
        {
            double mij = 0;
            double mij_diag = dpMatrix[i - 1, j - 1] + sij;
            double mij_i = dpMatrix[i, j - 1] + gapPenalty;
            double mij_j = dpMatrix[i - 1, j] + gapPenalty;

            mij = Math.Max(mij_diag, mij_i);
            mij = Math.Max(mij, mij_j);
            return mij;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coord1"></param>
        /// <param name="coord2"></param>
        /// <returns></returns>
        private double GetSIJ(Coordinate coord1, Coordinate coord2)
        {
            double distance = GetDistance (coord1, coord2);
            double sij = Math.Log(3 / distance) + pseudocount;
            return sij;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coord1"></param>
        /// <param name="coord2"></param>
        /// <returns></returns>
        private double GetDistance(Coordinate coord1, Coordinate coord2)
        {
            double squareSum =  Math.Pow((coord1.X - coord2.X), 2) +
                        Math.Pow((coord1.Y - coord2.Y), 2) +
                        Math.Pow((coord1.Z - coord2.Z), 2);
            double distance = Math.Sqrt(squareSum);
            return distance;
        }
        #endregion

        #region back tracking, local
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dpMatrix"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <returns></returns>
        private void AlignLocal(double[,] dpMatrix, int i, int j, double[,] distanceMatrix, ref List<int> indexListI, ref List<int> indexListJ)
        {
            if (dpMatrix[i, j] == 0)
            {
                return;
            }
            // since it is backtracking, so put the indexes in the order
            AddNextIndexToList(indexListI, i);
            AddNextIndexToList(indexListJ, j);
            int[] nextIJ = GetNextIJ(dpMatrix, i, j, distanceMatrix);
            AlignLocal(dpMatrix, nextIJ[0], nextIJ[1], distanceMatrix, ref indexListI, ref indexListJ);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="indexList"></param>
        /// <param name="index"></param>
        private void AddNextIndexToList(List<int> indexList, int index)
        {
            if (indexList.Count == 0)  // it is the first index
            {
                indexList.Insert(0, index);
            }
            else
            {
                int firstIndex = indexList[0];
                if (firstIndex == index)  // it is a gap
                {
                    indexList.Insert(0, -1);
                }
                else
                {
                    indexList.Insert(0, index);
                }
            }
        }
        /// <summary>
        /// distance matrix is one row and one column shorter that the dynamic matrix
        /// </summary>
        /// <param name="dpMatrix"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="distanceMatrix"></param>
        /// <returns></returns>
        private int[] GetNextIJ(double[,] dpMatrix, int i, int j, double[,] distanceMatrix)
        {
            double mij = dpMatrix[i, j];
            double mij_diag = dpMatrix[i - 1, j - 1] + distanceMatrix[i - 1, j - 1];
            double mij_up = dpMatrix[i - 1, j] + gapPenalty;
            double mij_left = dpMatrix[i, j - 1] + gapPenalty;

            int[] nextIJ = new int[2];
            if (mij == mij_diag)
            {
                nextIJ[0] = i - 1;
                nextIJ[1] = j - 1;
            }
            else if (mij == mij_up)
            {
                nextIJ[0] = i - 1;
                nextIJ[1] = j;
            }
            else if (mij == mij_left)
            {
                nextIJ[0] = i;
                nextIJ[1] = j - 1;
            }
            return nextIJ;
         }
        #endregion

        #region back tracking - global        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dpMatrix"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="indexListI"></param>
        /// <param name="indexListJ"></param>
        private void AlignGlobal(double[,] dpMatrix, int i, int j, double[,] distanceMatrix, ref List<int> indexListI, ref List<int> indexListJ)
        {
            if (i == 0 || j == 0)
            {
                return;
            }
            // since it is backtracking, so put the indexes in the order
            AddNextIndexToList(indexListI, i);
            AddNextIndexToList(indexListJ, j);
            int[] nextIJ = GetNextIJ(dpMatrix, i, j, distanceMatrix);
            AlignGlobal(dpMatrix, nextIJ[0], nextIJ[1], distanceMatrix, ref indexListI, ref indexListJ);
        }
        #endregion
    }
}
