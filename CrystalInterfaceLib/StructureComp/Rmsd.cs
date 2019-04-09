using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.DomainInterfaces;

namespace CrystalInterfaceLib.StructureComp
{
    public class RmsdCalculator
    {
        #region member variables
        public int numOfOverlapCoordsCutoff = 3;
        public double atomContactCutoff = 3.0;
        public double maxAtomContactCutoff = 10.0;
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain1"></param>
        /// <param name="chain2"></param>
        /// <param name="atomType"></param>
        /// <returns></returns>
        public double CalculateRmsd(AtomInfo[] chain1, AtomInfo[] chain2, string atomType)
        {
            Coordinate[] atomCoords1 = GetAtomCoordinates(chain1, atomType);
            Coordinate[] atomCoords2 = GetAtomCoordinates(chain2, atomType);

            double rmsd = CalculateMinRmsd(atomCoords1, atomCoords2);

            return rmsd;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain1"></param>
        /// <param name="chain2"></param>
        /// <param name="atomType"></param>
        /// <returns></returns>
        public double CalculateLinearMinRmsd(AtomInfo[] chain1, AtomInfo[] chain2, string atomName, out Range[] alignedRanges)
        {
            alignedRanges = null;
            Coordinate[] atomCoords1 = GetAtomCoordinates(chain1, atomName);
            Coordinate[] atomCoords2 = GetAtomCoordinates(chain2, atomName );
            double rmsd = CalculateLinearMinRmsd(atomCoords1, atomCoords2, out alignedRanges);
            return rmsd;
        }
        #region linear minimum RMSD
        /// <summary>
        /// 
        /// </summary>
        /// <param name="atomCoords1"></param>
        /// <param name="atomCoords2"></param>
        /// <returns></returns>
        public double CalculateLinearMinRmsd(Coordinate[] atomCoords1, Coordinate[] atomCoords2, out Range[] alignedRanges)
        {
            alignedRanges = new Range[2];

            if (atomCoords1 == null || atomCoords2 == null || 
                atomCoords1.Length == 0 || atomCoords2.Length == 0)
            {
                return -1;
            }

            double rmsd = 0; 
            if (atomCoords1.Length < numOfOverlapCoordsCutoff || atomCoords2.Length < numOfOverlapCoordsCutoff)
            {
                rmsd = CalculateMinRmsd(atomCoords1, atomCoords2);
                alignedRanges[0] = new Range();
                alignedRanges[0].startPos = 1;
                alignedRanges[0].endPos = atomCoords1.Length;

                alignedRanges[1] = new Range();
                alignedRanges[1].startPos = 1;
                alignedRanges[1].endPos = atomCoords2.Length;
                return rmsd;
            }
            Coordinate[] refCoords = atomCoords1;
            Coordinate[] compCoords = atomCoords2;
            if (atomCoords1.Length < atomCoords2.Length)
            {
                refCoords = atomCoords2;
                compCoords = atomCoords1;
            }
            int numOfOverlap = 0;
            int refLength = refCoords.Length;
            int compLength = compCoords.Length;
            double minRmsd = -1;
            Range refRange = new Range();
            Range compRange = new Range();
            Range minRefRange = new Range();
            Range minCompRange = new Range();
            Coordinate[] alignRefCoords = null;
            Coordinate[] alignCompCoords = null;
            for (int start = numOfOverlapCoordsCutoff - compCoords.Length; start <= refCoords.Length - numOfOverlapCoordsCutoff; start++)
            {
                numOfOverlap = GetNumOfOverlap(start, compCoords.Length, refCoords.Length);
                alignRefCoords = new Coordinate[numOfOverlap];
                alignCompCoords = new Coordinate[numOfOverlap];
                if (start <= 0)
                {
                    Array.Copy(compCoords, start * (-1), alignCompCoords, 0, numOfOverlap);
                    compRange.startPos = start * (-1) + 1;
                    compRange.endPos = compCoords.Length;

                    Array.Copy(refCoords, 0, alignRefCoords, 0, numOfOverlap);
                    refRange.startPos = 1;
                    refRange.endPos = numOfOverlap;
                }
                else if (start > refLength - compLength)
                {
                    Array.Copy(compCoords, 0, alignCompCoords, 0, numOfOverlap);
                    compRange.startPos = 1;
                    compRange.endPos = numOfOverlap;

                    Array.Copy(refCoords, start, alignRefCoords, 0, numOfOverlap);
                    refRange.startPos = start + 1;
                    refRange.endPos = start + numOfOverlap;
                }
                else // numOfOverap = refCoords.Length
                {
                    Array.Copy(compCoords, 0, alignCompCoords, 0, numOfOverlap);
                    compRange.startPos = 1;
                    compRange.endPos = compCoords.Length;

                    Array.Copy(refCoords, start, alignRefCoords, 0, numOfOverlap);
                    refRange.startPos = start + 1;
                    refRange.endPos = start + numOfOverlap;
                }
                rmsd = CalculateRmsd(alignRefCoords, alignCompCoords);
                if (minRmsd == -1 || minRmsd > rmsd)
                {
                    minRmsd = rmsd;
                    minRefRange = refRange;
                    minCompRange = compRange;
                }
            }
            alignedRanges[0] = minCompRange;
            alignedRanges[1] = minRefRange;

            return minRmsd;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="refStart"></param>
        /// <param name="compLength"></param>
        /// <param name="refLength"></param>
        /// <returns></returns>
        private int GetNumOfOverlap(int refStart, int compLength, int refLength)
        {
            int numOfOverlap = 0;
            if (refStart <= 0)
            {
                numOfOverlap = compLength + refStart;
            }
            else if (refStart > refLength - compLength)
            {
           //     numOfOverlap = refLength - (refStart - (refLength - compLength));
                numOfOverlap = refLength - refStart;
            }
            else
            {
                numOfOverlap = compLength;
            }
            return numOfOverlap;
        }
        #endregion

        #region the maximum numbers of linear residue-pairs between two arrays of coordinates
        /// <summary>
        /// 
        /// </summary>
        /// <param name="atomCoords1"></param>
        /// <param name="atomCoords2"></param>
        /// <param name="seqIdPairs"></param>
        /// <returns></returns>
        public double[] CalculateRmsd(Coordinate[] atomCoords1, Coordinate[] atomCoords2, out int[] maxResiduePairs)
        {
            maxResiduePairs = new int[2];
            maxResiduePairs[0] = -1;
            maxResiduePairs[1] = -1;
            if (atomCoords1 == null || atomCoords2 == null ||
                atomCoords1.Length == 0 || atomCoords2.Length == 0)
            {
                return null;
            }

            double[] rmsds = null;
      //      int numOfResiduePairs = 0;
            if (atomCoords1.Length < numOfOverlapCoordsCutoff || atomCoords2.Length < numOfOverlapCoordsCutoff)
            {
                rmsds = CalculateMinRmsd(atomCoords1, atomCoords2, out maxResiduePairs);
                return rmsds;
            }

            Coordinate[] refCoords = atomCoords1;
            Coordinate[] compCoords = atomCoords2;
            if (atomCoords1.Length < atomCoords2.Length)
            {
                refCoords = atomCoords2;
                compCoords = atomCoords1;
            }
            int numOfOverlap = 0;
            int refLength = refCoords.Length;
            int compLength = compCoords.Length;
            int[] numOfResiduePairs = null;
            double[] rmsdWithMaxPair = null;
            Coordinate[] alignRefCoords = null;
            Coordinate[] alignCompCoords = null;
            for (int start = numOfOverlapCoordsCutoff - compCoords.Length; start <= refCoords.Length - numOfOverlapCoordsCutoff; start++)
            {
                numOfOverlap = GetNumOfOverlap(start, compCoords.Length, refCoords.Length);
                alignRefCoords = new Coordinate[numOfOverlap];
                alignCompCoords = new Coordinate[numOfOverlap];
                if (start <= 0)
                {
                    Array.Copy(compCoords, start * (-1), alignCompCoords, 0, numOfOverlap);
                    Array.Copy(refCoords, 0, alignRefCoords, 0, numOfOverlap);
                }
                else if (start > refLength - compLength)
                {
                    Array.Copy(compCoords, 0, alignCompCoords, 0, numOfOverlap);
                    Array.Copy(refCoords, start, alignRefCoords, 0, numOfOverlap);
                }
                else // numOfOverap = refCoords.Length
                {
                    Array.Copy(compCoords, 0, alignCompCoords, 0, numOfOverlap);
                    Array.Copy(refCoords, start, alignRefCoords, 0, numOfOverlap);
                }
                rmsds = CalculateRmsdWithMaxResiduePairs (alignRefCoords, alignCompCoords, out numOfResiduePairs);
                if (maxResiduePairs[1]  == -1 || maxResiduePairs[1] < numOfResiduePairs[1])
                {
                    maxResiduePairs = numOfResiduePairs;
                    rmsdWithMaxPair = rmsds;
                }
            }
            if (rmsdWithMaxPair == null)
            {
                rmsdWithMaxPair = new double[2];
                rmsdWithMaxPair[0] = -1;
                rmsdWithMaxPair[1] = -1;
            }
            return rmsdWithMaxPair;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atomCoords1"></param>
        /// <param name="atomCoords2"></param>
        /// <returns></returns>
        public double[] CalculateMinRmsd(Coordinate[] atomCoords1, Coordinate[] atomCoords2, out int[] maxResdiuePairs)
        {
            maxResdiuePairs = new int[2];
            maxResdiuePairs[0] = -1;
            maxResdiuePairs[1] = -1;
            if (atomCoords1 == null || atomCoords2 == null ||
                atomCoords1.Length == 0 || atomCoords2.Length == 0)
            {
                return null;
            }
            if (atomCoords1.Length > atomCoords2.Length)
            {
                Coordinate[] temp = atomCoords1;
                atomCoords1 = atomCoords2;
                atomCoords2 = temp;
                //     isSwapped = true;
            }
            Coordinate[] refCoordinates = new Coordinate[atomCoords1.Length];
            double[] rmsdsWithMaxPairs = null;
            double[] rmsds = null;
            int[] numOfResiduePairs = null;
            int minStart = 0;
            for (int refStart = 0; refStart <= atomCoords2.Length - atomCoords1.Length; refStart++)
            {
                Array.Copy(atomCoords2, refStart, refCoordinates, 0, refCoordinates.Length);
                rmsds = CalculateRmsdWithMaxResiduePairs(atomCoords1, atomCoords2, out numOfResiduePairs);
                if (maxResdiuePairs[1] == -1)
                {
                    maxResdiuePairs = numOfResiduePairs;
                    rmsdsWithMaxPairs = rmsds;
                    minStart = refStart + 1;
                }
                else if (maxResdiuePairs[1] < numOfResiduePairs[1])
                {
                    maxResdiuePairs = numOfResiduePairs;
                    rmsdsWithMaxPairs = rmsds;
                    minStart = refStart + 1;
                }
            }
            return rmsdsWithMaxPairs;
        }

        /// <summary>
        /// the length of two input arrays is same
        /// </summary>
        /// <param name="atomCoords1"></param>
        /// <param name="atomCoords2"></param>
        /// <param name="maxResiduePairs">the number of residue pairs which have calpha contacts less than atomContactCutoff</param>
        /// <returns></returns>
        private double[] CalculateRmsdWithMaxResiduePairs(Coordinate[] atomCoords1, Coordinate[] atomCoords2, out int[] numOfResiduePairs)
        {
            numOfResiduePairs = new int[2];
            numOfResiduePairs[0] = -1;
            numOfResiduePairs[1] = -1;
            if (atomCoords1 == null || atomCoords2 == null)
            {
                return null;
            }
            double rmsd = 0;
            double rmsdL10 = 0;
            double squareSum = 0;
            double squareSumL10 = 0;
            double distance = 0;
            double squareDistance = 0;
            int numOfResiduePairs3 = 0;
            int numOfResiduePairs10 = 0;
            for (int i = 0; i < atomCoords1.Length; i++)
            {
                squareDistance = Math.Pow((atomCoords1[i].X - atomCoords2[i].X), 2) +
                    Math.Pow((atomCoords1[i].Y - atomCoords2[i].Y), 2) +
                    Math.Pow((atomCoords1[i].Z - atomCoords2[i].Z), 2);
                distance = Math.Sqrt(squareDistance);
                if (distance <= atomContactCutoff)
                {
                    numOfResiduePairs3++;
                }
                squareSum += squareDistance;
                if (distance <= maxAtomContactCutoff)
                {
                    squareSumL10 += squareDistance;
                    numOfResiduePairs10++;
                }
            }
            numOfResiduePairs[0] = numOfResiduePairs10;
            numOfResiduePairs[1] = numOfResiduePairs3;
            rmsd = Math.Sqrt(squareSum / (double)atomCoords1.Length);
            rmsdL10 = Math.Sqrt(squareSumL10 / (double)atomCoords1.Length);
            double[] rmsds = new double[2];
            rmsds[0] = rmsd;
            rmsds[1] = rmsdL10;
            return rmsds;
        }
        #endregion

        #region Naive RMSD
        /// <summary>
        /// 
        /// </summary>
        /// <param name="atomCoords1"></param>
        /// <param name="atomCoords2"></param>
        /// <returns></returns>
        public double CalculateMinRmsd(Coordinate[] atomCoords1, Coordinate[] atomCoords2)
        {
            if (atomCoords1 == null || atomCoords2 == null || 
                atomCoords1.Length == 0 || atomCoords2.Length == 0)
            {
                return -1;
            }
            if (atomCoords1.Length > atomCoords2.Length)
            {
                Coordinate[] temp = atomCoords1;
                atomCoords1 = atomCoords2;
                atomCoords2 = temp;
           //     isSwapped = true;
            }
            Coordinate[] refCoordinates = new Coordinate[atomCoords1.Length];
            double rmsd = 0;
            double minRmsd = -1;
            int minStart = 0;
            for (int refStart = 0; refStart <= atomCoords2.Length - atomCoords1.Length; refStart++)
            {
                Array.Copy(atomCoords2, refStart, refCoordinates, 0, refCoordinates.Length);
                rmsd = CalculateRmsd(atomCoords1, refCoordinates);
                if (minRmsd == -1)
                {
                    minRmsd = rmsd;
                    minStart = refStart + 1;
                }
                else if (minRmsd > rmsd)
                {
                    minRmsd = rmsd;
                    minStart = refStart + 1;
                }
            }
            return minRmsd;
        }
        #endregion

        #region helper functions
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain"></param>
        /// <param name="atomType"></param>
        /// <returns></returns>
        private Coordinate[] GetAtomCoordinates(AtomInfo[] chain, string atomName)
        {
            List<Coordinate> selectedCoordinateList = new List<Coordinate> ();
            atomName = atomName.ToUpper();
            Dictionary<int, AtomInfo[]> residueAtomsDict = GetResidueAtoms(chain);
            List<int> seqIdList = new List<int>(residueAtomsDict.Keys);
            seqIdList.Sort();
            bool residueHasAtom = false;
            foreach (int seqId in seqIdList)
            {
                residueHasAtom = false;
                foreach (AtomInfo atom in residueAtomsDict[seqId])
                {
                    if (atom.atomName == atomName)
                    {
                        selectedCoordinateList.Add(atom.xyz);
                        residueHasAtom = true;
                        break;
                    }
                }
                if (! residueHasAtom) // if a residue doesn't have the specific atom, then use the first atom in the list. 
                {
                    selectedCoordinateList.Add(residueAtomsDict[seqId][0].xyz);
                }
            }
   /*         foreach (AtomInfo atom in chain)
            {
                if (atom.atomName  == atomName)
                {
                    selectedCoordinateList.Add(atom.xyz);
                }
            }
            */
            return selectedCoordinateList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain"></param>
        /// <returns></returns>
        private Dictionary<int, AtomInfo[]> GetResidueAtoms (AtomInfo[] chain)
        {
            Dictionary<int, List<AtomInfo>> residueAtomListDict = new Dictionary<int, List<AtomInfo>>();
            int seqId = 0;
            foreach (AtomInfo atom in chain)
            {
                seqId = Convert.ToInt32(atom.seqId);
                if (residueAtomListDict.ContainsKey(seqId))
                {
                    residueAtomListDict[seqId].Add(atom);
                }
                else
                {
                    List<AtomInfo> atomList = new List<AtomInfo>();
                    atomList.Add(atom);
                    residueAtomListDict.Add(seqId, atomList);
                }
            }
            Dictionary<int, AtomInfo[]> residueAtomsDict = new Dictionary<int, AtomInfo[]>();
            foreach (int lsSeqId in residueAtomListDict.Keys)
            {
                residueAtomsDict.Add(lsSeqId, residueAtomListDict[lsSeqId].ToArray());
            }
            return residueAtomsDict;
        }

        /// <summary>
        /// the length of atomCoords1 must be same as atomCoords2
        /// </summary>
        /// <param name="atomCoords1"></param>
        /// <param name="atomCoords2"></param>
        /// <returns></returns>
        public double CalculateRmsd(Coordinate[] atomCoords1, Coordinate[] atomCoords2)
        {
            if (atomCoords1 == null || atomCoords2 == null)
            {
                return -1;
            }
            double rmsd = 0;
            if (atomCoords1.Length != atomCoords2.Length)
            {
                rmsd = CalculateMinRmsd(atomCoords1, atomCoords2);
            }
            else
            {
                double squareSum = 0;
                for (int i = 0; i < atomCoords1.Length; i++)
                {
                    squareSum += Math.Pow((atomCoords1[i].X - atomCoords2[i].X), 2) +
                        Math.Pow((atomCoords1[i].Y - atomCoords2[i].Y), 2) +
                        Math.Pow((atomCoords1[i].Z - atomCoords2[i].Z), 2);
                }
                rmsd = Math.Sqrt(squareSum / (double)atomCoords1.Length);
            }
            return rmsd;
        }
        #endregion
    }
}
