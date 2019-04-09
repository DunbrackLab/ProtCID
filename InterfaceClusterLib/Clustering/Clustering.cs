using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace InterfaceClusterLib.Clustering
{
    public class Clustering
    {
        #region member variables
        private double mergeQCutoff = 0.20;
        private double firstQCutoff = 0.80;
        private Dictionary<int, string> interfacePfamArchHash = null;
        private string[] pdbInterfaces = null;

        public double MergeQCutoff
        {
            get
            {
                return mergeQCutoff;
            }
            set
            {
                mergeQCutoff = value;
            }
        }

        public double FirstQCutoff
        {
            get
            {
                return firstQCutoff;
            }
            set
            {
                firstQCutoff = value;
            }
        }
        /// <summary>
        /// key: the index of distance matrix, 0:n-1
        /// value: pfam architecture
        /// </summary>
        public Dictionary<int, string> InterfacePfamArchHash
        {
            get
            {
                return interfacePfamArchHash;
            }
            set
            {
                interfacePfamArchHash = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public string[] PdbInterfaces
        {
            get
            {
                return pdbInterfaces;
            }
            set
            {
                pdbInterfaces = value;
            }
        }
        #endregion

        #region cluster based on distance matrix
        /// <summary>
        /// 
        /// </summary>
        /// <param name="qscoreMatrix"></param>
        public List<List<int>> Cluster(double[,] qscoreMatrix)
        {
            List<List<int>> clusterList = InitializeClusters(qscoreMatrix);
            MergeClusters(qscoreMatrix, clusterList);
            return clusterList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="qscoreMatrix"></param>
        public List<List<int>> ClusterInBig(double[,] qscoreMatrix)
        {
            List<List<int>> clusterList = ClusterInFirstStep(qscoreMatrix);
            MergeClusters(qscoreMatrix, clusterList);
            return clusterList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="qscoreMatrix"></param>
        /// <returns></returns>
        private List<List<int>> InitializeClusters(double[,] qscoreMatrix)
        {
            List<List<int>> clusterList = new List<List<int>>();
            int numOfInterfaces = qscoreMatrix.GetLength(0);
            List<int> cluster = null;

            for (int i = 0; i < numOfInterfaces; i++)
            {
                cluster = new List<int> ();
                cluster.Add(i);
                clusterList.Add(cluster);
            }
            return clusterList;
        }
        /// <summary>
        /// add those interfaces with similarity Q scores at least firstQCutoff = 0.8 together
        /// to reduce the depth of hierachical tree
        /// </summary>
        /// <param name="qscoreMatrix"></param>
        /// <returns></returns>
        private List<List<int>> ClusterInFirstStep(double[,] qscoreMatrix)
        {
            List<List<int>> clusterList = new List<List<int>>();
            int numOfInterfaces = qscoreMatrix.GetLength(0);
            List<int> addedIndexList = new List<int> ();
            List<int> cluster = null;

            for (int i = 0; i < numOfInterfaces; i++)
            {
                if (addedIndexList.Contains(i))
                {
                    continue;
                }
                cluster = new List<int> ();
                cluster.Add(i);
                addedIndexList.Add(i);
                for (int j = i + 1; j < numOfInterfaces; j++)
                {
                    if (addedIndexList.Contains(j))
                    {
                        continue;
                    }
                    if (qscoreMatrix[i, j] >= firstQCutoff &&
                        AreTwoInterfacesWithSameFamilyArch (i, j))
                    {
                        cluster.Add(j);
                        addedIndexList.Add(j);
                    }
                }
                clusterList.Add(cluster);
            }
            return clusterList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="qscoreMatrix"></param>
        /// <param name="clusterList"></param>
        private void MergeClusters (double[,] qscoreMatrix, List<List<int>> clusterList)
        {
            int[] twoClosestClusters = FindTwoClosestClusters (qscoreMatrix, clusterList);
            if (twoClosestClusters == null) // no two clusters are similar enough to be grouped
            {
                return;
            }
            List<int> clusterI = clusterList[twoClosestClusters[0]];
            List<int> clusterJ =  clusterList[twoClosestClusters[1]];
            clusterI.AddRange(clusterJ);
            clusterList.RemoveAt(twoClosestClusters[1]);

            MergeClusters(qscoreMatrix, clusterList);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="qscoreMatrix"></param>
        /// <param name="clusterList"></param>
        /// <returns></returns>
        private int[] FindTwoClosestClusters (double[,] qscoreMatrix, List<List<int>> clusterList)
        {
            string twoClusters = "";
            double maxQscore = 0;
            for (int i = 0; i < clusterList.Count; i++)
            {
                List<int> indexListI = clusterList[i];
                for (int j = i + 1; j < clusterList.Count; j++)
                {
                    List<int> indexListJ = clusterList[j];
                    double avgQscore = GetAvgQscoreBetweenTwoClusters(qscoreMatrix, indexListI, indexListJ);
                    if (maxQscore < avgQscore && 
                        AreTwoClustersWithSamePfamArch (indexListI, indexListJ))
                    {
                        maxQscore = avgQscore;
                        twoClusters = i.ToString() + "_" + j.ToString();
                    }
                }
            }
            if (maxQscore < mergeQCutoff)
            {
                return null;
            }
            int[] closestClusters = new int[2];
            string[] clusterFields = twoClusters.Split ('_');
            closestClusters[0] = Convert.ToInt32(clusterFields[0]);
            closestClusters[1] = Convert.ToInt32(clusterFields[1]);
            return closestClusters;
        }
        #endregion 

        #region add data points to existing clusters, and form new clusters
        /// <summary>
        /// 
        /// </summary>
        /// <param name="addIndexList">the indexes of data points which are in the entire data set</param>
        /// <param name="clusterList">the list of clusters, containing indexes of data points</param>
        /// <param name="simScoreMatrix">m*n: m=size of addList, n=size of all data points</param>
        public void UpdateClusters (int[] addIndexList, List<List<int>> clusterList, double[,] simScoreMatrix)
        {
            List<int> leftIndexList = AddSimilarDataToClusters (addIndexList, clusterList, simScoreMatrix);
            // initialize left data points
            List<List<int>> leftClusterList = new List<List<int>>();
            foreach (int index in leftIndexList)
            {
                List<int> cluster = new List<int> ();
                cluster.Add(index);
                leftClusterList.Add(cluster);
            }
            // cluster left data points
            MergeClusters(simScoreMatrix, leftClusterList);
            // merge left clusters into existing clusters
            List<List<int>> leftClusterLeftList = new List<List<int>> (leftClusterList);
            foreach (List<int> leftCluster in leftClusterList)
            {
                for (int i = 0; i < clusterList.Count; i ++ )
                {
                    List<int> existCluster = clusterList[i];
                    if (CanTwoClustersMerged(simScoreMatrix, leftCluster, clusterList[i]))
                    {
                        existCluster.AddRange(leftCluster);
                        leftClusterLeftList.Remove(leftCluster);
                    }
                }
            }
            clusterList.AddRange(leftClusterLeftList);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="qscoreMatrix"></param>
        /// <param name="clusterList"></param>
        /// <returns></returns>
        private bool CanTwoClustersMerged (double[,] qscoreMatrix, List<int> cluster1, List<int> cluster2)
        {
            double avgQscore = GetAvgQscoreBetweenTwoClusters(qscoreMatrix, cluster1, cluster2);
            if (avgQscore < mergeQCutoff)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addIndexList">the indexes of data points which are in the entire data set</param>
        /// <param name="clusterList">the list of clusters, containing indexes of data points</param>
        /// <param name="simScoreMatrix">m*n: m=size of addList, n=size of all data points</param>
        public List<int> AddSimilarDataToClusters(int[] addIndexList, List<List<int>> clusterList, double[,] simScoreMatrix)
        {
            List<int> leftIndexList = new List<int>();
            leftIndexList.AddRange(addIndexList);
            List<int> existCluster = null;
            for (int i = 0; i < addIndexList.Length; i++)
            {
                for (int j = 0; j < clusterList.Count; j ++ )
                {
                    existCluster = clusterList[j];
                    if (CanIndexBeAddedToCluster(addIndexList[i], existCluster, simScoreMatrix))
                    {
                        existCluster.Add(addIndexList[i]);
                        leftIndexList.Remove(addIndexList[i]);
                        break;
                    }
                }
            }
            return leftIndexList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addIndex"></param>
        /// <param name="clusterIndexes"></param>
        /// <param name="simScoreMatrix"></param>
        /// <returns></returns>
        public bool CanIndexBeAddedToCluster(int addIndex, List<int> clusterIndexes, double[,] simScoreMatrix)
        {
            double simScore = GetMaximumSimScore(addIndex, clusterIndexes, simScoreMatrix);
            if (simScore >= firstQCutoff)
            {
                return true;
            }
            simScore = GetAverageSimScore(addIndex, clusterIndexes, simScoreMatrix);
            if (simScore >= mergeQCutoff)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addIndex"></param>
        /// <param name="clusterIndexes"></param>
        /// <param name="simMatrix"></param>
        /// <returns></returns>
        public double GetAverageSimScore (int addIndex, List<int> clusterIndexes, double[,] simMatrix)
        {
            double totalScore = 0;
            double numOfDataPoints = (double)clusterIndexes.Count;
            foreach (int cIndex in clusterIndexes)
            {
                totalScore += simMatrix[addIndex, cIndex];
            }
            return totalScore / numOfDataPoints;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addIndex"></param>
        /// <param name="clusterIndexes"></param>
        /// <param name="simMatrix"></param>
        /// <returns></returns>
        public double GetMaximumSimScore (int addIndex, List<int> clusterIndexes, double[,] simMatrix)
        {
            double maxSimScore = 0;
            foreach (int cIndex in clusterIndexes)
            {
                if (maxSimScore < simMatrix[addIndex, cIndex])
                {
                    maxSimScore = simMatrix[addIndex, cIndex];
                }
            }
            return maxSimScore;
        }
        #endregion

        #region cluster with condition
        /// <summary>
        /// for peptide interface clustering
        /// distMatrix contains the numbers of common hmm sites
        /// conditionMatrix contains RMSD between peptides
        /// #commonHmmSites less than 10, while rmsd greater than 10, then the two clusters cannot be merged
        /// <param name="distMatrix">distance matrix for clustering</param>
        /// <param name="conditionMatrix">the condition matrix</param>
        /// <param name="distCutoff">used for merging</param>
        /// <param name="conditionCutoff">used for merging</param>
        /// <returns></returns>
        public List<List<int>> Cluster(double[,] distMatrix, double[,] conditionMatrix, double distCutoff, double conditionCutoff)
        {
            List<List<int>> clusterList = InitializeClusters(distMatrix);
            MergeClusters(distMatrix, clusterList, conditionMatrix, distCutoff, conditionCutoff);
            return clusterList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="qscoreMatrix"></param>
        /// <param name="clusterList"></param>
        private void MergeClusters(double[,] distMatrix, List<List<int>> clusterList, double[,] conditionMatrix, double distCutoff, double conditionCutoff)
        {
            int[] twoClosestClusters = FindTwoClosestClusters(distMatrix, clusterList);
            if (twoClosestClusters == null) // no two clusters are similar enough to be grouped
            {
                return;
            }
            List<int> clusterI = clusterList[twoClosestClusters[0]];
            List<int> clusterJ = clusterList[twoClosestClusters[1]];
            double avgQscore = GetAvgQscoreBetweenTwoClusters(distMatrix, clusterI, clusterJ);
            double avgCondition = GetAvgQscoreBetweenTwoClusters(conditionMatrix, clusterI, clusterJ);
            if (distCutoff > 0 && conditionCutoff > 0)
            {
                if (avgQscore < distCutoff && avgCondition > conditionCutoff)
                {
                    return;
                }
            }
            else if (distCutoff <= 0)  // no distance cutoff here, only check the condition
            {
                if (avgCondition > conditionCutoff)
                {
                    return;
                }
            }
           
            clusterI.AddRange(clusterJ);
            clusterList.RemoveAt(twoClosestClusters[1]);

            MergeClusters(distMatrix, clusterList, conditionMatrix, distCutoff, conditionCutoff);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterI"></param>
        /// <param name="clusterJ"></param>
        /// <returns></returns>
        private bool CanTwoClustersBeMergedOnCondition(List<int> clusterI, List<int> clusterJ, double[,] conditionMatrix, double conditionCutoff)
        {
            double avgCondition = GetAvgQscoreBetweenTwoClusters(conditionMatrix, clusterI, clusterJ);
            if (avgCondition < conditionCutoff)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region interfaces and clusters with same pfam architectures
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryInterface1"></param>
        /// <param name="entryInterface2"></param>
        /// <param name="entryInterfaceFamilyArchHash"></param>
        /// <returns></returns>
        private bool AreTwoInterfacesWithSameFamilyArch(int indexI, int indexJ)
        {
            if (interfacePfamArchHash == null || interfacePfamArchHash.Count == 0)
            {
                return true;
            }
            if ((string)interfacePfamArchHash[indexI] == (string)interfacePfamArchHash[indexJ])
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cluster1"></param>
        /// <param name="cluster2"></param>
        /// <returns></returns>
        private bool AreTwoClustersWithSamePfamArch(List<int> cluster1, List<int> cluster2)
        {
            if (interfacePfamArchHash == null || interfacePfamArchHash.Count == 0)
            {
                return true;
            }
            string pfamArch1 = (string)interfacePfamArchHash[(int)cluster1[0]];
            string pfamArch2 = (string)interfacePfamArchHash[(int)cluster2[0]];
            if (pfamArch1 == pfamArch2)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region Q between 2 clusters
        /// <summary>
        /// 
        /// </summary>
        /// <param name="qscoreMatrix"></param>
        /// <param name="indexListI"></param>
        /// <param name="indexListJ"></param>
        /// <returns></returns>
        private double GetMinQscoreBetweenClusters(double[,] qscoreMatrix, List<int> indexListI, List<int> indexListJ)
        {
            double minQscore = 1.0;
            foreach (int indexI in indexListI)
            {
                foreach (int indexJ in indexListJ)
                {
                    if (minQscore > qscoreMatrix[indexI, indexJ])
                    {
                        if (qscoreMatrix[indexI, indexJ] > 0)
                        {
                            minQscore = qscoreMatrix[indexI, indexJ];
                        }
                    }
                }
            }
            return minQscore;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="qscoreMatrix"></param>
        /// <param name="indexListI"></param>
        /// <param name="indexListJ"></param>
        /// <returns></returns>
        private double GetMaxQscoreBetweenClusters(double[,] qscoreMatrix, List<int> indexListI, List<int> indexListJ)
        {
            double maxQscore = 0;
            foreach (int indexI in indexListI)
            {
                foreach (int indexJ in indexListJ)
                {
                    if (maxQscore < qscoreMatrix[indexI, indexJ])
                    {
                        maxQscore = qscoreMatrix[indexI, indexJ];
                    }
                }
            }
            return maxQscore;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="qscoreMatrix"></param>
        /// <param name="indexListI"></param>
        /// <param name="indexListJ"></param>
        /// <returns></returns>
        private double GetAvgQscoreBetweenTwoClusters(double[,] qscoreMatrix, List<int> indexListI, List<int> indexListJ)
        {
            double avgQscore = 0;
            double totalQscore = 0;
            int numOfPairs = 0;
            foreach (int indexI in indexListI)
            {
                foreach (int indexJ in indexListJ)
                {
                    if (qscoreMatrix[indexI, indexJ] <= 0)
                    {
                        continue;
                    }
                    totalQscore += qscoreMatrix[indexI, indexJ];
                    numOfPairs++;
                }
            }
            avgQscore = totalQscore / (double)numOfPairs;
            return avgQscore;
        }
        #endregion

        #region the number of entries which have similar interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="qscoreMatrix"></param>
        /// <param name="cluster"></param>
        /// <param name="thisInterfaceIndex"></param>
        /// <returns></returns>
        private int GetNumOfEntriesWithSimilarInterfaces(double[,] qscoreMatrix, List<int> cluster, int thisInterfaceIndex)
        {
            List<string> entryList = new List<string> ();
            string entry = "";
            foreach (int interfaceIndex in cluster)
            {
                if (qscoreMatrix[interfaceIndex, thisInterfaceIndex] >= mergeQCutoff)
                {
                    entry = pdbInterfaces[interfaceIndex].Substring(0, 4);
                    if (!entryList.Contains(entry))
                    {
                        entryList.Add(entry);
                    }
                }
            }
            return entryList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="qscoreMatrix"></param>
        /// <param name="cluster1"></param>
        /// <param name="cluster2"></param>
        /// <returns></returns>
        private int GetMinimumNumOfEntriesWithSimilarInterfaces(double[,] qscoreMatrix, List<int> cluster1, List<int> cluster2)
        {
            int minNumOfEntries = 1000000;
            foreach (int interfaceIndex1 in cluster1)
            {
                int numOfEntries = GetNumOfEntriesWithSimilarInterfaces(qscoreMatrix, cluster2, interfaceIndex1);
                if (minNumOfEntries > numOfEntries)
                {
                    minNumOfEntries = numOfEntries;
                }
            }

            foreach (int interfaceIndex2 in cluster2)
            {
                int numOfEntries = GetNumOfEntriesWithSimilarInterfaces(qscoreMatrix, cluster1, interfaceIndex2);
                if (minNumOfEntries > numOfEntries)
                {
                    minNumOfEntries = numOfEntries;
                }
            }
            return minNumOfEntries;
        }
        #endregion

    }
}
