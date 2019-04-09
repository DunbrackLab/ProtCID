using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;
using InterfaceClusterLib.AuxFuncs;

namespace InterfaceClusterLib.DomainInterfaces.PfamPeptide
{
    public class PeptideInterfaceClusterStat
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        private EntryCrystForms entryCfs = new EntryCrystForms();
        #endregion

        #region peptide interface cluster stat
        /// <summary>
        /// 
        /// </summary>
        public void GetPepInterfaceClusterStat()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Peptide interface cluster summary";

            bool isUpdate = false;
            DataTable pepInterfaceClusterStatTable = CreateTables(isUpdate);

            string queryString = "Select Distinct PfamId From PfamPepInterfaceClusters;";
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.totalOperationNum = pfamIdTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = pfamIdTable.Rows.Count;

            string pfamId = "";
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                pfamId = pfamIdRow["PfamID"].ToString().TrimEnd();

                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    AddPeptideInterfaceClusterSumInfo(pfamId, pepInterfaceClusterStatTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " Retrieve peptide interface cluster table errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " Retrieve peptide interface cluster table errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdatePepInterfaceClusterStat(string[] updatePfams)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Peptide interface cluster summary";

            bool isUpdate = true;
            DataTable pepInterfaceClusterStatTable = CreateTables(isUpdate);

            ProtCidSettings.progressInfo.totalOperationNum = updatePfams.Length;
            ProtCidSettings.progressInfo.totalStepNum = updatePfams.Length;

            foreach (string pfamId in updatePfams)
            {
                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    DeletePeptideInterfaceClusterSumINfo(pfamId);
                    AddPeptideInterfaceClusterSumInfo(pfamId, pepInterfaceClusterStatTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pfamId + " Retrieve peptide interface cluster table errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pfamId + " Retrieve peptide interface cluster table errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        private void AddPeptideInterfaceClusterSumInfo(string pfamId, DataTable clusterStatInfoTable)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamPepInterfaceClusters Where PfamID = '{0}';", pfamId);
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] pepRelSeqIds = GetPfamPeptideRelSeqIds(pfamId);
            DataTable pepInterfaceTable = GetRelationPeptideInterfaceTable (pepRelSeqIds);
            DataTable pfamDomainTable = GetPfamDomainTable(pfamId);
            int clusterId = 0;
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString ());
                GetClusterSumInfo(pfamId, clusterId, pepInterfaceTable, pfamDomainTable, clusterStatInfoTable);
            }
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, clusterStatInfoTable);
            clusterStatInfoTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        private void DeletePeptideInterfaceClusterSumINfo(string pfamId)
        {
            string deleteString = string.Format("Delete From PfamPepClusterSumInfo Where PfamID = '{0}';", pfamId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetPfamPeptideRelSeqIds(string pfamId)
        {
            string queryString = string.Format("Select Distinct RelSeqID From PfamPepInterfaceClusters Where PfamID = '{0}';", pfamId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] pepRelSeqIds = new int[relSeqIdTable.Rows.Count];
            int count = 0;
            int relSeqId = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                pepRelSeqIds[count] = relSeqId;
                count++;
            }
            return pepRelSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIds"></param>
        /// <returns></returns>
        private DataTable GetRelationPeptideInterfaceTable(int[] relSeqIds)
        {
            DataTable pepInterfaceTable = null;
            foreach (int relSeqId in relSeqIds)
            {
                DataTable relPepInterfaceTable = GetRelationPeptideInterfaceTable(relSeqId);
                ParseHelper.AddNewTableToExistTable(relPepInterfaceTable, ref pepInterfaceTable);
            }
            return pepInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private DataTable GetRelationPeptideInterfaceTable(int relSeqId)
        {
            string queryString = string.Format("Select * From PfamPeptideInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable peptideInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return peptideInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private DataTable GetPfamDomainTable(string pfamId)
        {
            string queryString = string.Format("Select * From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return pfamDomainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private void GetClusterSumInfo(string pfamId, int clusterId, DataTable relationPepInterfaceTable, 
            DataTable pfamDomainTable, DataTable clusterSumInfoTable)
        {
            string queryString = string.Format("Select * From PfamPepInterfaceClusters Where PfamId = '{0}' AND ClusterID = {1};", pfamId, clusterId);
            DataTable pepInterfaceClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> entryList = new List<string>();
            List<string> protUnpSeqList = new List<string>();
            List<string> pepUnpSeqList = new List<string>();
            List<string> pepPfamList = new List<string>();
            List<string> pepSeqList = new List<string>();
            string pdbId = "";
            int domainInterfaceId = 0;
            string protUnpCode = "";
            string pepUnpCode = "";
            string pepPfamId = "";
            string pepSequence = "";
            double surfaceAreaSum = 0;
            double surfaceArea = 0;
            int numOfDomainInterfaces = 0;
            int numOfCFs = 0;
            List<long> protDomainIdList = new List<long> ();
            long domainId = 0;
            foreach (DataRow clusterInterfaceRow in pepInterfaceClusterTable.Rows)
            {
                pdbId = clusterInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(clusterInterfaceRow["DomainInterfaceID"].ToString ());
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
                protUnpCode = clusterInterfaceRow["UnpCode"].ToString().TrimEnd();
                if (protUnpCode != "-" && protUnpCode != "")
                {
                    if (!protUnpSeqList.Contains(protUnpCode))
                    {
                        protUnpSeqList.Add(protUnpCode);
                    }
                }
                pepUnpCode = clusterInterfaceRow["PepUnpCode"].ToString().TrimEnd();
                if (pepUnpCode != "-" && pepUnpCode != "")
                {
                    if (!pepUnpSeqList.Contains(pepUnpCode))
                    {
                        pepUnpSeqList.Add(pepUnpCode);
                    }
                }
                pepPfamId = clusterInterfaceRow["PepPfamId"].ToString().TrimEnd();
                if (pepPfamId != "-" && pepPfamId != "")
                {
                    if (!pepPfamList.Contains(pepPfamId))
                    {
                        pepPfamList.Add(pepPfamId);
                    }
                }
                surfaceArea = Convert.ToDouble (clusterInterfaceRow["SurfaceArea"].ToString ());
                domainId = GetDomainInterfaceDomainID(pdbId, domainInterfaceId, relationPepInterfaceTable);
              //  surfaceArea = GetDomainInterfaceSurfaceArea(pdbId, domainInterfaceId, relationDomainInterfaceTable, out domainId);
                if (domainId > -1)
                {
                    if (!protDomainIdList.Contains(domainId))
                    {
                        protDomainIdList.Add(domainId);
                    }
                }
                if (surfaceArea > 0)
                {
                    surfaceAreaSum += surfaceArea;
                    numOfDomainInterfaces++;
                }
                pepSequence = GetInterfacePeptideSequence(pdbId, domainInterfaceId, relationPepInterfaceTable);
                if (pepSequence != "")
                {
                    if (! pepSeqList.Contains (pepSequence))
                    {
                        pepSeqList.Add(pepSequence);
                    }
                }
            }
            double avgSurfaceArea = surfaceAreaSum / (double)numOfDomainInterfaces;
            long[] domainIds = new long[protDomainIdList.Count];
            protDomainIdList.CopyTo(domainIds);
            double minSeqIdentity = GetMinSequenceIdentity(domainIds, pfamDomainTable);
            string clusterInterface = GetClusterInterface (pepInterfaceClusterTable);

            DataRow clusterSumInfoRow = clusterSumInfoTable.NewRow();
            clusterSumInfoRow["PfamID"] = pfamId;
            clusterSumInfoRow["ClusterID"] = clusterId;
            clusterSumInfoRow["NumEntries"] = entryList.Count;
            numOfCFs = entryCfs.GetNumberOfCFs(entryList.ToArray());
            clusterSumInfoRow["NumCFs"] = numOfCFs;
            clusterSumInfoRow["NumUnpSeqs"] = protUnpSeqList.Count;
            clusterSumInfoRow["NumPepUnpSeqs"] = pepUnpSeqList.Count;
            clusterSumInfoRow["NumPepPfams"] = pepPfamList.Count;
            clusterSumInfoRow["NumPepSeqs"] = pepSeqList.Count;
            clusterSumInfoRow["SurfaceArea"] = avgSurfaceArea;
            clusterSumInfoRow["MinSeqIdentity"] = minSeqIdentity;
            clusterSumInfoRow["ClusterInterface"] = clusterInterface;
            clusterSumInfoTable.Rows.Add (clusterSumInfoRow );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private double GetDomainInterfaceSurfaceArea(string pdbId, int domainInterfaceId, DataTable domainInterfaceTable, out long domainId)
        {
            domainId = -1;
            DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            double surfaceArea = -1;
            if (domainInterfaceRows.Length > 0)
            {
                surfaceArea = Convert.ToDouble(domainInterfaceRows[0]["SurfaceArea"].ToString ());
                domainId = Convert.ToInt64(domainInterfaceRows[0]["DomainID"].ToString ());
            }
            return surfaceArea;
        }

         /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private long GetDomainInterfaceDomainID(string pdbId, int domainInterfaceId, DataTable domainInterfaceTable)
        {
            long domainId = -1;
            DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            if (domainInterfaceRows.Length > 0)
            {
                domainId = Convert.ToInt64(domainInterfaceRows[0]["DomainID"].ToString ());
            }
            return domainId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="interfaceTable"></param>
        /// <returns></returns>
        private string GetInterfacePeptideSequence (string pdbId, int domainInterfaceId, DataTable interfaceTable)
        {
            DataRow[] interfaceRows = interfaceTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            if (interfaceRows.Length > 0)
            {
                string pepAsymChain = interfaceRows[0]["PepAsymChain"].ToString().TrimEnd();
                string sequence = GetChainSequence(pdbId, pepAsymChain);
                return sequence;
            }
            return "";
        }

        private string GetChainSequence (string pdbId, string asymChain)
        {
            string queryString = string.Format("Select Sequence From AsymUnit Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymChain);
            DataTable seqTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (seqTable.Rows.Count > 0)
            {
                return seqTable.Rows[0]["Sequence"].ToString().TrimEnd();
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaceTable"></param>
        /// <returns></returns>
        private string GetClusterInterface (DataTable clusterInterfaceTable )
        {
            DataRow[] clusterInterfaceRows = clusterInterfaceTable.Select ("SurfaceArea > -1", "SurfaceArea ASC");
            int medianIndex = (int)((double)clusterInterfaceRows.Length / 2.0);
            string clusterInterface = clusterInterfaceRows[medianIndex]["PdbID"].ToString () + "_d" + clusterInterfaceRows[medianIndex]["DomainInterfaceID"].ToString ();
            return clusterInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable CreateTables(bool isUpdate)
        {
            DataTable pepInterfaceClusterStatTable = new DataTable("PfamPepClusterSumInfo");
            string[] sumColumns = {"PfamID", "ClusterID", "NumEntries", "NumPepPfams", "NumUnpSeqs", "NumCFs",
                                      "NumPepUnpSeqs", "NumPepSeqs", "SurfaceArea", "MinSeqIdentity", "ClusterInterface"};
            foreach (string col in sumColumns)
            {
                pepInterfaceClusterStatTable.Columns.Add(new DataColumn (col));
            }

            if (!isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string createTableString = "Create Table " + pepInterfaceClusterStatTable.TableName + " ( " +
                    " PfamID VARCHAR(40) NOT NULL, " +
                    " ClusterID Integer Not NULL, " +
                    " NumEntries Integer NOT NULL, " +
                    " NumCFs Integer  NOT NULL," +
                    " NumPepPfams Integer NOT NULL, " +
                    " NumUnpSeqs Integer NOT NULL, " +
                    " NumPepUnpSeqs Integer Not NULL, " +
                    " NumPepSeqs Integer Not NULL, " +
                    " SurfaceArea FLOAT, " +
                    " MinSeqIdentity FLOAT, " +
                    " ClusterInterface CHAR(10));";
                dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, pepInterfaceClusterStatTable.TableName);
            }
            return pepInterfaceClusterStatTable;
        }
        #endregion

        #region sequence identity aligned by hmm
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainIds"></param>
        /// <returns></returns>
        private double GetMinSequenceIdentity(long[] domainIds, DataTable pfamDomainTable)
        {
            double minIdentity = 100.0;
            double identity = 0;
            for (int i = 0; i < domainIds.Length; i++)
            {
                for (int j = i + 1; j < domainIds.Length; j++)
                {
                    identity = GetDomainIdentiy(domainIds[i], domainIds[j], pfamDomainTable);
                    if (identity > -1)
                    {
                        if (minIdentity > identity)
                        {
                            minIdentity = identity;
                        }
                    }
                }
            }
            return minIdentity;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId1"></param>
        /// <param name="domainId2"></param>
        /// <param name="domainTable"></param>
        /// <returns></returns>
        private double GetDomainIdentiy(long domainId1, long domainId2, DataTable domainTable)
        {
            DataRow[] domainRows1 = domainTable.Select(string.Format("DomainID = '{0}'", domainId1));
            DataRow[] domainRows2 = domainTable.Select(string.Format("DomainID = '{0}'", domainId2));

            double identity = -1.0;
            if (domainRows1.Length > 0 && domainRows2.Length > 0)
            {
                identity = GetDomainIdentity(domainRows1[0], domainRows2[0]);
            }
            return identity;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainRow1"></param>
        /// <param name="domainRow2"></param>
        /// <returns></returns>
        private double GetDomainIdentity(DataRow domainRow1, DataRow domainRow2)
        {
            string alignSeq1 = domainRow1["QueryAlignment"].ToString();
            string alignHmm1 = domainRow1["HmmAlignment"].ToString();
            int hmmStart1  = Convert.ToInt32(domainRow1["HmmStart"].ToString());
            int hmmEnd1 = Convert.ToInt32 (domainRow1["HmmEnd"].ToString ());
            Dictionary<int, char> hmmPosResidueHash1 = GetHmmPosSeqResidueHash(alignSeq1, alignHmm1, hmmStart1);

            string alignSeq2 = domainRow2["QueryAlignment"].ToString();
            string alignHmm2 = domainRow2["HmmAlignment"].ToString();
            int hmmStart2 = Convert.ToInt32(domainRow2["HmmStart"].ToString());
            int hmmEnd2 = Convert.ToInt32 (domainRow2["HmmEnd"].ToString ());
            Dictionary<int, char> hmmPosResidueHash2 = GetHmmPosSeqResidueHash(alignSeq2, alignHmm2, hmmStart2);

            int maxHmmStart = Math.Max (hmmStart1, hmmStart2);
            int minHmmEnd = Math.Min (hmmEnd1, hmmEnd2);
            int alignLength = minHmmEnd  - maxHmmStart + 1;
            int numSameResidues = 0;
            foreach (int hmmPos in hmmPosResidueHash1.Keys)
            {
                if (hmmPosResidueHash2.ContainsKey(hmmPos))
                {
                    if ((char)hmmPosResidueHash1[hmmPos] == (char)hmmPosResidueHash2[hmmPos])
                    {
                        numSameResidues++;
                    }
                }
            }
            double identity = ((double)numSameResidues / (double)alignLength) * 100.0;
            return identity;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignSequence"></param>
        /// <param name="alignHmm"></param>
        /// <param name="hmmStart"></param>
        /// <returns></returns>
        private Dictionary<int, char> GetHmmPosSeqResidueHash(string alignSequence, string alignHmm, int hmmStart)
        {
            Dictionary<int, char> hmmPosResidueHash = new Dictionary<int,char> ();
            int hmmPos = hmmStart;
            for (int i = 0; i < alignSequence.Length; i++)
            {
                if (alignHmm[i] != '.' && alignHmm[i] != '-')
                {
                    if (alignSequence[i] != '-' && alignSequence[i] != '.')
                    {
                        hmmPosResidueHash.Add(hmmPos, alignSequence[i]);
                    }
                    hmmPos++;
                }
            }
            return hmmPosResidueHash;
        }
        #endregion

        #region sequence identity aligned by structures
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainIds"></param>
        /// <returns></returns>
        private double GetMinSequenceIdentity(long[] domainIds)
        {
            double minIdentity = 100.0;
            double identity = 0;
            for (int i = 0; i < domainIds.Length; i++)
            {
                for (int j = i + 1; j < domainIds.Length; j++)
                {
                    identity = GetDomainStructAlignIdentity(domainIds[i], domainIds[j]);
                    if (identity > -1)
                    {
                        if (minIdentity > identity)
                        {
                            minIdentity = identity;
                        }
                    }
                }
            }
            return minIdentity;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId1"></param>
        /// <param name="domainId2"></param>
        /// <returns></returns>
        private double GetDomainStructAlignIdentity(long domainId1, long domainId2)
        {
            string queryString = string.Format("Select Identity From PfamDomainAlignments " +
                " Where (QueryDomainID = {0} AND HitDomainID = {1}) OR (QueryDomainID = {1} AND HitDomainID = {0});", domainId1, domainId2);
            DataTable identityTable = ProtCidSettings.alignmentQuery.Query( queryString);
            double identity = -1;
            if (identityTable.Rows.Count > 0)
            {
                identity = Convert.ToDouble(identityTable.Rows[0]["Identity"].ToString());
            }
            return identity;
        }
        #endregion

        #region debug: add pfam id to summary table
        private DbUpdate dbUpdate = new DbUpdate();
        public void AddNumCFs ()
        {
            string queryString = "Select PfamID, ClusterID From PfamPepClusterSumInfo;";
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query (queryString);
            string pfamId = "";
            int clusterId = 0;
            int numCfs = 0;
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                pfamId = clusterRow["PfamID"].ToString().TrimEnd ();
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString ());
                numCfs = GetNumCfs(pfamId, clusterId);
                AddNumCfs(pfamId, clusterId, numCfs);
            }
        }

        private void AddNumCfs (string pfamId, int clusterId, int numCfs)
        {
            string updateString = string.Format("Update PfamPepClusterSumInfo Set NumCFs = {0} Where PfamID = '{1}' AND ClusterID = {2};", numCfs, pfamId, clusterId);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private int GetNumCfs (string pfamId, int clusterId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamPepInterfaceClusters Where PfamID = '{0}' AND ClusterID = {1};", pfamId, clusterId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> entryList = new List<string>();
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entryList.Add(entryRow["PdbID"].ToString());
            }
            int numCfs = entryCfs.GetNumberOfCFs(entryList.ToArray());
            return numCfs;
        }


        /// <summary>
        /// 
        /// </summary>
        public void UpdatePepUnpCodeNumbers()
        {
            string queryString = "Select Distinct PfamId, ClusterID From PfamPepInterfaceClusters;";
            DataTable pepClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pfamId = "";
            int clusterId = 0;
            int numOfPepUnpCode = 0;
            foreach (DataRow pepClusterRow in pepClusterTable.Rows)
            {
                pfamId = pepClusterRow["PfamID"].ToString().TrimEnd ();
                clusterId = Convert.ToInt32(pepClusterRow["ClusterID"].ToString ());

                numOfPepUnpCode = GetNumOfPepUnpCode(pfamId, clusterId);
                UpdateNumOfPepUnpCodes(pfamId, clusterId, numOfPepUnpCode);
            }
        }

        private void UpdateNumOfPepUnpCodes(string pfamId, int clusterId, int numOfPepUnpCodes)
        {
            string updateString = string.Format("Update PfamPepClusterSumInfo Set NumPepUnpSeqs = {0} Where PfamID = '{1}' AND ClusterID = {2};", numOfPepUnpCodes, pfamId, clusterId);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private int GetNumOfPepUnpCode(string pfamId, int clusterId)
        {
            string queryString = string.Format("Select Distinct PepUnpCode From PfamPepInterfaceClusters Where PfamID = '{0}' AND ClusterID = {1} AND PepUnpCode <> '-';", pfamId, clusterId);
            DataTable pepUnpCodeTable = ProtCidSettings.protcidQuery.Query( queryString);
            return pepUnpCodeTable.Rows.Count;
        }

        public void AddPfamIdToSumInfoTable()
        {
            string queryString = "Select Distinct RelSeqID From PfamPepClusterSumInfo;";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            string pfamId = "";
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                pfamId = GetPfamIdFromRelSeqId(relSeqId);
                AddPfamIdToSumInfoTable(relSeqId, pfamId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string GetPfamIdFromRelSeqId(int relSeqId)
        {
            string queryString = string.Format("Select PfamID From PfamPepInterfaceClusters Where RelSeqID = {0};", relSeqId);
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pfamId = "";
            if (pfamIdTable.Rows.Count > 0)
            {
                pfamId = pfamIdTable.Rows[0]["PfamID"].ToString().TrimEnd();
            }
            return pfamId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pfamId"></param>
        private void AddPfamIdToSumInfoTable(int relSeqId, string pfamId)
        {
            string updateString = string.Format("Update PfamPepClusterSumInfo Set PfamID = '{0}' Where RelSeqID = {1};", pfamId, relSeqId);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }
        #endregion
    }
}
