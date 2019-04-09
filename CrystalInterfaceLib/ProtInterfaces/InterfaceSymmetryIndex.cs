using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Threading.Tasks;
using CrystalInterfaceLib.Contacts;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace CrystalInterfaceLib.ProtInterfaces
{
    public struct CrcSeqAlignInfo
    {
        public string crc1;
        public string crc2;
        public int seqStart1;
        public int seqEnd1;
        public string alignSequence1;
        public int seqStart2;
        public int seqEnd2;
        public string alignSequence2;
        public double identity;

        public void Reverse ()
        {
            string temp = crc1;
            crc1 = crc2;
            crc2 = temp;

            int intTemp = seqStart1;
            seqStart1 = seqStart2;
            seqStart2 = intTemp;

            intTemp = seqEnd1;
            seqEnd1 = seqEnd2;
            seqEnd2 = intTemp;

            temp = alignSequence1;
            alignSequence1 = alignSequence2;
            alignSequence2 = temp;
        }
    }
    /// <summary>
    /// the jaccard index of contacts of a homo-dimer
    /// </summary>
    public class InterfaceSymmetryIndex
    {
        #region member variables
        private double symSeqIdentityCutoff = 95.0;     
        #endregion

        /// <summary>
        /// for homo-dimer with sequence identity > 95%
        /// </summary>
        /// <param name="inInterface"></param>
        /// <returns></returns>
        public double CalculateInterfaceSymmetry (InterfaceChains inInterface)
        {
            bool homoSeqs = AreSameSequence (inInterface.pdbId, inInterface.entityId1, inInterface.entityId2);
            if (! homoSeqs)
            {
                return -1.0;
            }

            double jIndex = CalculateHomoInterfaceSymmetry(inInterface);
            return jIndex;
        }

        /// <summary>
        /// for homo-dimer with sequence identity > 95%
        /// </summary>
        /// <param name="inInterface"></param>
        /// <returns></returns>
        public double CalculateInterfaceSymmetry(InterfaceChains inInterface, Dictionary<string, string> entityCrcDict, Dictionary<Tuple<string, string>, CrcSeqAlignInfo> crcPairAlignInfoDict)
        {
            bool isReversed = false;
            CrcSeqAlignInfo seqAlignInfo = GetEntitySequenceAlignInfo(inInterface.pdbId, inInterface.entityId1, inInterface.entityId2, entityCrcDict, crcPairAlignInfoDict, out isReversed);
            if (seqAlignInfo.identity < symSeqIdentityCutoff)
            {
                return -1.0;
            }
            double jIndex = 0;
            if (inInterface.entityId1 == inInterface.entityId2)
            {
                jIndex = CalculateHomoInterfaceSymmetry(inInterface);
            }
            else
            {
               if (isReversed)
               {
                   seqAlignInfo.Reverse();
               }
               jIndex = CalculateHomoInterfaceSymmetry(inInterface, seqAlignInfo);
            }
            return jIndex;
        }

        /// <summary>
        /// for homo-dimer with sequence identity of two monomers > 95%
        /// </summary>
        /// <param name="inInterface">an interface with two homologous chains, with sequence identity >= 95%</param>
        /// <returns></returns>
        public double CalculateHomoInterfaceSymmetry(InterfaceChains inInterface)
        {
            if (inInterface.seqDistHash == null)
            {
                inInterface.GetInterfaceResidueDist();
            }
            List<string> seqIdPairListA = new List<string>();
            List<string> seqIdPairListB = new List<string>();
            foreach (string seqIdPair in inInterface.seqDistHash.Keys)
            {
                seqIdPairListA.Add(seqIdPair);
                string[] seqIds = seqIdPair.Split('_');
                seqIdPairListB.Add(seqIds[1] + "_" + seqIds[0]);
            }
            List<string> totalSeqPairList = new List<string>(seqIdPairListA);
            List<string> sameSeqPairList = new List<string>();
            foreach (string seqPair in seqIdPairListA)
            {
                if (seqIdPairListB.Contains(seqPair))
                {
                    sameSeqPairList.Add(seqPair);
                }
            }
            foreach (string seqPair in seqIdPairListB)
            {
                if (!totalSeqPairList.Contains(seqPair))
                {
                    totalSeqPairList.Add(seqPair);
                }
            }
            double jIndex = (double)sameSeqPairList.Count / (double)totalSeqPairList.Count;
            return jIndex;
        }

        /// <summary>
        /// for homo-dimer with sequence identity of two monomers > 95%
        /// </summary>
        /// <param name="inInterface">an interface with two homologous chains, with sequence identity >= 95%</param>
        /// <returns></returns>
        public double CalculateHomoInterfaceSymmetry(InterfaceChains inInterface, CrcSeqAlignInfo seqAlignInfo)
        {
            if (inInterface.seqDistHash == null)
            {
                inInterface.GetInterfaceResidueDist();
            }
            List<string>[] seqIdPairLists = GetSeqIdPairLists(inInterface.seqDistHash, seqAlignInfo);
            List<string> totalSeqPairList = new List<string>(seqIdPairLists[0]);
            List<string> sameSeqPairList = new List<string>();
            foreach (string seqPair in seqIdPairLists[0])
            {
                if (seqIdPairLists[1].Contains(seqPair))
                {
                    sameSeqPairList.Add(seqPair);
                }
            }
            foreach (string seqPair in seqIdPairLists[1])
            {
                if (!totalSeqPairList.Contains(seqPair))
                {
                    totalSeqPairList.Add(seqPair);
                }
            }
            double jIndex = (double)sameSeqPairList.Count / (double)totalSeqPairList.Count;
            return jIndex;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqDistDict"></param>
        /// <param name="seqAlignInfo"></param>
        /// <returns></returns>
        private List<string>[] GetSeqIdPairLists (Dictionary<string, double> seqDistDict, CrcSeqAlignInfo seqAlignInfo)
        {
            List<string> seqIdPairListA = new List<string>();
            List<string> seqIdPairListB = new List<string>();
            Dictionary<string, string> chainBToADict = GetChainBToAMap(seqAlignInfo); // the sequence numbers of chain B to A
            string newSeqIdPair = "";
            foreach (string seqIdPair in seqDistDict.Keys)
            {
                newSeqIdPair = UpdateSeqIdPair(seqIdPair, chainBToADict);
                seqIdPairListA.Add(newSeqIdPair);
                string[] seqIds = newSeqIdPair.Split('_');
                seqIdPairListB.Add(seqIds[1] + "_" + seqIds[0]);
            }
            List<string>[] seqIdPairLists = new List<string>[2];
            seqIdPairLists[0] = seqIdPairListA;
            seqIdPairLists[1] = seqIdPairListB;
            return seqIdPairLists;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqAlignInfo"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetChainBToAMap (CrcSeqAlignInfo seqAlignInfo)
        {
            int seqA = seqAlignInfo.seqStart1 - 1;
            int seqB = seqAlignInfo.seqStart2 - 1;
            Dictionary<string, string> chainBToADict = new Dictionary<string, string>();
            for (int i = 0; i < seqAlignInfo.alignSequence1.Length; i ++ )
            {
                if (i < seqAlignInfo.alignSequence2.Length)
                {
                    if (IsAlignCharValid (seqAlignInfo.alignSequence1[i]))
                    {
                        seqA++;
                    }

                    if (IsAlignCharValid(seqAlignInfo.alignSequence2[i]))
                    {
                        seqB++;
                    }

                    if (IsAlignCharValid (seqAlignInfo.alignSequence1[i]) && IsAlignCharValid(seqAlignInfo.alignSequence2[i]))
                    {
                        chainBToADict.Add(seqB.ToString (), seqA.ToString ());
                    }
                }
            }
            return chainBToADict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ch"></param>
        /// <returns></returns>
        private bool IsAlignCharValid (char ch)
        {
            if (ch == '-' || ch == '_' || ch == '.')
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqIdPair"></param>
        /// <param name="seqIdDict"></param>
        /// <returns></returns>
        private string UpdateSeqIdPair (string seqIdPair, Dictionary<string, string> seqIdDict)
        {
            string[] seqIds = seqIdPair.Split('_');
            string newSeqPair = seqIdPair;
            if (seqIdDict.ContainsKey (seqIds[1]))
            {
                newSeqPair = seqIds[0] + "_" + seqIdDict[seqIds[1]];
            }
            return newSeqPair;
        }

        /// <summary>
        /// for homo-dimer with sequence identity > 95%
        /// </summary>
        /// <param name="inInterface"></param>
        /// <returns></returns>
        public double CalculateInterfaceSymmetry(Dictionary<string, double> seqDistDict)
        {                       
            List<string> seqIdPairListA = new List<string>();
            List<string> seqIdPairListB = new List<string>();
            foreach (string seqIdPair in seqDistDict.Keys)
            {
                seqIdPairListA.Add(seqIdPair);
                string[] seqIds = seqIdPair.Split('_');
                seqIdPairListB.Add(seqIds[1] + "_" + seqIds[0]);
            }
            List<string> totalSeqPairList = new List<string>(seqIdPairListA);
            List<string> sameSeqPairList = new List<string>();
            foreach (string seqPair in seqIdPairListA)
            {
                if (seqIdPairListB.Contains(seqPair))
                {
                    sameSeqPairList.Add(seqPair);
                }
            }
            foreach (string seqPair in seqIdPairListB)
            {
                if (!totalSeqPairList.Contains(seqPair))
                {
                    totalSeqPairList.Add(seqPair);
                }
            }
            double jIndex = (double)sameSeqPairList.Count / (double)totalSeqPairList.Count;
            return jIndex;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId1"></param>
        /// <param name="entityId2"></param>
        /// <returns></returns>
        public bool AreSameSequence (string pdbId, int entityId1, int entityId2)
        {
            if (entityId1 == entityId2)
            {
                return true;
            }
            double seqIdentity = GetSequenceIdentity(pdbId, entityId1, entityId2);
            if (seqIdentity > symSeqIdentityCutoff)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// the sequence identity of two interface chains
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId1"></param>
        /// <param name="entityId2"></param>
        /// <returns></returns>
        public double GetSequenceIdentity(string pdbId, int entityId1, int entityId2)
        {
            if (entityId1 == entityId2)
            {
                return 100.0;
            }
            else
            {
                int[] entityIds = new int[2];
                entityIds[0] = entityId1;
                entityIds[1] = entityId2;
                double seqIdentity = 0;
                Dictionary<int, string> entityCrcDict = GetEntityCrc(pdbId, entityIds);
                if (entityCrcDict.ContainsKey(entityId1) && entityCrcDict.ContainsKey(entityId2))
                {
                    seqIdentity = GetSequenceHhIdentity(entityCrcDict[entityId1], entityCrcDict[entityId2]);
                }
                return seqIdentity; 
            }
        }

        /// <summary>
        /// the sequence identity of two interface chains
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId1"></param>
        /// <param name="entityId2"></param>
        /// <returns></returns>
        public CrcSeqAlignInfo GetEntitySequenceAlignInfo (string pdbId, int entityId1, int entityId2, Dictionary<string, string> entityCrcDict,
            Dictionary<Tuple<string, string>, CrcSeqAlignInfo> crcPairAlignInfoDict, out bool isReversed)
        {           
            CrcSeqAlignInfo seqAlignInfo = new CrcSeqAlignInfo();
            isReversed = false;
            if (entityId1 == entityId2)
            {
                seqAlignInfo.seqStart1 = 1;
                seqAlignInfo.seqEnd1 = 1;
                seqAlignInfo.alignSequence1 = "";
                seqAlignInfo.seqStart2 = 1;
                seqAlignInfo.seqEnd2 = 1;
                seqAlignInfo.alignSequence2 = "";
                seqAlignInfo.identity = 100;
            }
            else
            {               
                Tuple<string, string> crcPair = GetCrcPair(pdbId, entityId1, entityId2, entityCrcDict, out isReversed);
                if (crcPairAlignInfoDict.ContainsKey(crcPair))
                {
                    seqAlignInfo = crcPairAlignInfoDict[crcPair];
                }
                else
                {
                    if (entityCrcDict.ContainsKey(pdbId + entityId1) && entityCrcDict.ContainsKey(pdbId + entityId2))
                    {
                        if (isReversed)
                        {
                            seqAlignInfo = GetSequenceHhAlignInfo(entityCrcDict[pdbId + entityId2], entityCrcDict[pdbId + entityId1]);
                        }
                        else
                        {
                            seqAlignInfo = GetSequenceHhAlignInfo(entityCrcDict[pdbId + entityId1], entityCrcDict[pdbId + entityId2]);
                        }
                        crcPairAlignInfoDict.Add(crcPair, seqAlignInfo);
                    }
                }                
            }
            return  seqAlignInfo;;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId1"></param>
        /// <param name="entityId2"></param>
        /// <param name="entityCrcDict"></param>
        /// <returns></returns>
        private Tuple<string, string> GetCrcPair (string pdbId, int entityId1, int entityId2, Dictionary<string, string> entityCrcDict, out bool isReversed)
        {
            string crc1 = "";
            string crc2 = "";
            isReversed = false;
            if (entityCrcDict.ContainsKey(pdbId + entityId1))
            {
                crc1 = entityCrcDict[pdbId + entityId1];
            }
            else
            {
                crc2 = GetEntityCrc(pdbId, entityId1);
                entityCrcDict.Add(pdbId + entityId1, crc1);
            }
            if (entityCrcDict.ContainsKey(pdbId + entityId2))
            {
                crc2 = entityCrcDict[pdbId + entityId2];
            }
            else
            {
                crc2 = GetEntityCrc(pdbId, entityId2);
                entityCrcDict.Add(pdbId + entityId2, crc2);
            }
            Tuple<string, string> crcPair = new Tuple<string, string>(crc1, crc2);
            if (string.Compare(crc1, crc2) > 0)
            {
                crcPair = new Tuple<string, string>(crc2, crc1);
                isReversed = true;
            }
            return crcPair;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crc1"></param>
        /// <param name="crc2"></param>
        /// <returns></returns>
        private CrcSeqAlignInfo GetSequenceHhAlignInfo (string crc1, string crc2)
        {
            CrcSeqAlignInfo seqAlignInfo = new CrcSeqAlignInfo();
            seqAlignInfo.identity = -1.0;
            string queryString = string.Format("Select Query, Hit, QueryStart, QueryEnd, HitStart, HitEnd, QueryAlignment, HitAlignment, Identity From pdbcrchhalignments " + 
                " Where Query = '{0}' AND Hit = '{1}';", crc1, crc2);
            DataTable alignInfoTable = ProtCidSettings.alignmentQuery.Query(queryString);

            if (alignInfoTable.Rows.Count > 0)
            {
                seqAlignInfo.crc1 = alignInfoTable.Rows[0]["Query"].ToString().TrimEnd();
                seqAlignInfo.crc2 = alignInfoTable.Rows[0]["Hit"].ToString().TrimEnd();
                seqAlignInfo.seqStart1 = Convert.ToInt32(alignInfoTable.Rows[0]["QueryStart"].ToString());
                seqAlignInfo.seqEnd1 = Convert.ToInt32(alignInfoTable.Rows[0]["QueryEnd"].ToString());
                seqAlignInfo.alignSequence1 = alignInfoTable.Rows[0]["QueryAlignment"].ToString();
                seqAlignInfo.seqStart2 = Convert.ToInt32(alignInfoTable.Rows[0]["HitStart"].ToString());
                seqAlignInfo.seqEnd2 = Convert.ToInt32(alignInfoTable.Rows[0]["HitEnd"].ToString());
                seqAlignInfo.alignSequence2 = alignInfoTable.Rows[0]["HitAlignment"].ToString();
                seqAlignInfo.identity = Convert.ToDouble(alignInfoTable.Rows[0]["Identity"].ToString());
            }
            else
            {
                queryString = string.Format("Select Query, Hit, QueryStart, QueryEnd, HitStart, HitEnd, QueryAlignment, HitAlignment, Identity From pdbcrchhalignments " +
                " Where Query = '{0}' AND Hit = '{1}';", crc2, crc1);
                 alignInfoTable = ProtCidSettings.alignmentQuery.Query(queryString);
                 if (alignInfoTable.Rows.Count > 0)
                 {
                     seqAlignInfo.crc2 = alignInfoTable.Rows[0]["Query"].ToString().TrimEnd();
                     seqAlignInfo.crc1 = alignInfoTable.Rows[0]["Hit"].ToString().TrimEnd();
                     seqAlignInfo.seqStart2 = Convert.ToInt32(alignInfoTable.Rows[0]["QueryStart"].ToString());
                     seqAlignInfo.seqEnd2 = Convert.ToInt32(alignInfoTable.Rows[0]["QueryEnd"].ToString());
                     seqAlignInfo.alignSequence2 = alignInfoTable.Rows[0]["QueryAlignment"].ToString();
                     seqAlignInfo.seqStart1 = Convert.ToInt32(alignInfoTable.Rows[0]["HitStart"].ToString());
                     seqAlignInfo.seqEnd1 = Convert.ToInt32(alignInfoTable.Rows[0]["HitEnd"].ToString());
                     seqAlignInfo.alignSequence1 = alignInfoTable.Rows[0]["HitAlignment"].ToString();
                     seqAlignInfo.identity = Convert.ToDouble(alignInfoTable.Rows[0]["Identity"].ToString());
                 }
            }
            return seqAlignInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crc1"></param>
        /// <param name="crc2"></param>
        /// <returns></returns>
        private double GetSequenceHhIdentity(string crc1, string crc2)
        {
            double seqIdentity = -1.0;
            string queryString = string.Format("Select Identity From pdbcrchhalignments Where (Query = '{0}' AND Hit = '{1}') OR (Query = '{1}' AND Hit = '{0}');", crc1, crc2);
            DataTable seqIdentityTable = ProtCidSettings.alignmentQuery.Query(queryString);
            if (seqIdentityTable.Rows.Count > 0)
            {
               seqIdentity = Convert.ToDouble(seqIdentityTable.Rows[0]["Identity"].ToString());
            }
            return seqIdentity;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityIds"></param>
        /// <returns></returns>
        public Dictionary<int, string> GetEntityCrc (string pdbId, int[] entityIds)
        {
            string queryString = string.Format("Select Distinct PdbID, EntityID, Crc From PdbCrcMap Where PdbID = '{0}' AND EntityID IN ({1});", pdbId, ParseHelper.FormatSqlListString (entityIds));
            DataTable entityCrcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            Dictionary<int, string> entityCrcDict = new Dictionary<int, string>();
            string crc = "";
            int entityId = 0;
            foreach (DataRow crcRow in entityCrcTable.Rows)
            {
                crc = crcRow["Crc"].ToString();
                entityId = Convert.ToInt32(crcRow["EntityID"].ToString());
                entityCrcDict.Add(entityId, crc);
            }
            return entityCrcDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityIds"></param>
        /// <returns></returns>
        public string GetEntityCrc(string pdbId, int entityId)
        {
            string queryString = string.Format("Select Crc From PdbCrcMap Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable entityCrcTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string crc = "";
            if (entityCrcTable.Rows.Count > 0)
            {
                crc = entityCrcTable.Rows[0]["Crc"].ToString();
            }
            return crc;
        }
    }
}
