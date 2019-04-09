using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.IO;
using InterfaceClusterLib.stat;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;
using CrystalInterfaceLib.DomainInterfaces;
using CrystalInterfaceLib.Settings;
using InterfaceClusterLib.AuxFuncs;

namespace InterfaceClusterLib.DomainInterfaces
{
    public class DomainClusterStat : ClusterStat
    {
        #region member variables
        public struct ClusterSumInfo
        {
            public string relationString;
            public int relSeqId;
            public int clusterId;
            public double surfaceArea;
            public int numOfPdb;
            public int numOfPisa;
            public int numOfAsu;
            public string pdbBu;
            public string pisaBu;
            public string asu;
            public int numOfCfgCluster;
            public int numOfEntryCluster;
            public int numOfCfgRelation;
            public int numOfEntryRelation;
            public double minSeqId;
            public string clusterInterface;
            public double MediumSurfaceArea;
        }
        private DataTable clusterStatTable = null;
        private DataTable clusterSumInfoTable = null;       
        public string domainInterfaceCompTable = "PfamDomainInterfaceComp";
        private string resultDir = "";
        private double surfaceAreaCutoff = 100;
        private double homoSimQscoreCutoff = 0.5;
        private double rangeCoverage = 0.50;
        private string[] headerCols = { "RelSeqID", "ClusterID", "RelCfGroupID", "SpaceGroup", "CrystForm", "PdbID", "DomainInterfaceID", "SurfaceArea", "InterfaceUnit", 
                                      "ChainPfamArch", "InAsu", "InPdb", "InPisa", "ASU", "PDBBU", "PDBBUID", "PISABU", "PISABUID", "UnpCode", 
                                      "NumOfCfgCluster", "NumOfEntryCluster", "MinSeqIdentity", "NumOfCfgRelation", "NumOfEntryRelation", "NumOfEntryHomo", "NumOfEntryHetero", 
                                      "NumOfEntryIntra", "ClusterInterface", "MediumSurfaceArea", "Name", "Species"};
        #endregion

        #region print domain cluster info
        /// <summary>
        /// 
        /// </summary>
        public void PrintDomainClusterInfo()
        {
            InitializeTable();
            InitializeDbTables();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Print PFAM domain interface cluster info.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Cluster Info";

            resultDir = Path.Combine(ProtCidSettings.applicationStartPath, "HomoSeq\\result_domain_" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(resultDir))
            {
                Directory.CreateDirectory(resultDir);
            }

            StreamWriter clusterInfoWriter = new StreamWriter(Path.Combine(resultDir, "PfamDomainInterfaceClusterInfo.txt"));
            StreamWriter clusterSumInfoWriter = new StreamWriter(Path.Combine(resultDir, "PfamDomainInterfaceSumInfo.txt"));
            // write the header line of the summary info
            string sumInfoHeaderLine = GetTableHeaderLine(clusterSumInfoTable);
            sumInfoHeaderLine += "\t#CFGs-Ration\t#Entry-Ratio\t#PDB-Ratio\t#PISA-Ratio";
            clusterSumInfoWriter.WriteLine("RelationString\t" + sumInfoHeaderLine);

            string queryString = string.Format("Select Distinct RelSeqID From PfamDomainInterfaceCluster");
            DataTable relationTable = protcidQuery.Query(queryString);
            ProtCidSettings.progressInfo.totalOperationNum = relationTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = relationTable.Rows.Count;

            int relSeqId = -1;
            foreach (DataRow relationRow in relationTable.Rows)
            {
                relSeqId = Convert.ToInt32(relationRow["RelSeqID"].ToString());

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();

                try
                {
                    GetRelationDomainClusterInfo(relSeqId, clusterInfoWriter, clusterSumInfoWriter);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue
                        ("Output " + relSeqId.ToString() + " relation cluster info error: " + ex.Message);
                }
            }
            clusterInfoWriter.Close();
            clusterSumInfoWriter.Close();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Divide the result file into smaller files in order to fit into Excel.");
            DivideDomainClusterResultOutputFile();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get Pfam Domain Cluster statistical info table.");
            PrintDomainDbSumInfo("PfamDomain");
                     
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
            //    ProtCidSettings.progressInfo.threadFinished = true;

        }

         /// <summary>
        /// 
        /// </summary>
        public void PrintPartialDomainClusterInfo()
        {
            InitializeTable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Print PFAM domain interface cluster info.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Cluster Info";

            resultDir = Path.Combine(ProtCidSettings.applicationStartPath, "HomoSeq\\result_domain_" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(resultDir))
            {
                Directory.CreateDirectory(resultDir);
            }

            StreamWriter clusterInfoWriter = new StreamWriter(Path.Combine(resultDir, "PfamDomainInterfaceClusterInfo.txt"), true);
            StreamWriter clusterSumInfoWriter = new StreamWriter(Path.Combine(resultDir, "PfamDomainInterfaceSumInfo.txt"), true);
            // write the header line of the summary info
            string sumInfoHeaderLine = GetTableHeaderLine(clusterSumInfoTable);
            sumInfoHeaderLine += "\t#CFGs-Ration\t#Entry-Ratio\t#PDB-Ratio\t#PISA-Ratio";
            clusterSumInfoWriter.WriteLine("RelationString\t" + sumInfoHeaderLine);

            int[] relSeqIds = { 2986, 3175, 17915 };
            ProtCidSettings.progressInfo.totalOperationNum = relSeqIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIds.Length;

/*            string queryString = string.Format("Select Distinct RelSeqID From PfamDomainInterfaceCluster");
            DataTable relationTable = protcidQuery.Query(queryString);
            ProtCidSettings.progressInfo.totalOperationNum = relationTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = relationTable.Rows.Count;            
            int relSeqId = -1;
            foreach (DataRow relationRow in relationTable.Rows)*/
            foreach (int relSeqId in relSeqIds)
            {
    //            relSeqId = Convert.ToInt32(relationRow["RelSeqID"].ToString());

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();

                try
                {
                    GetRelationDomainClusterInfo(relSeqId, clusterInfoWriter, clusterSumInfoWriter);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue
                        ("Output " + relSeqId.ToString() + " relation cluster info error: " + ex.Message);
                }
            }
            clusterInfoWriter.Close();
            clusterSumInfoWriter.Close();
        }

        public void GetPfamDomainSumInfo ()
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get Pfam Domain Cluster statistical info table.");
            PrintDomainDbSumInfo("PfamDomain");         
        }

        /// <summary>
        /// 
        /// </summary>
        //   public void UpdateDomainClusterInfo(int[] updateRelSeqIds)
        public void UpdateDomainClusterInfo(Dictionary<int, string[]> updateRelEntryDict)
        {
            InitializeTable();
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update PFAM domain interface cluster info.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Cluster Info";

            List<int> updateRelSeqIdList = new List<int>(updateRelEntryDict.Keys);
            updateRelSeqIdList.Sort();
            int[] updateRelSeqIds = updateRelSeqIdList.ToArray ();

            resultDir = Path.Combine(ProtCidSettings.applicationStartPath, "HomoSeq\\newresult_domain_" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(resultDir))
            {
                Directory.CreateDirectory(resultDir);
            }

            StreamWriter clusterInfoWriter = new StreamWriter(Path.Combine(resultDir, "newPfamDomainInterfaceClusterInfo.txt"));
            StreamWriter clusterSumInfoWriter = new StreamWriter(Path.Combine(resultDir, "newPfamDomainInterfaceSumInfo.txt"));
            // write the header line of the summary info
            clusterSumInfoWriter.WriteLine("RelationString\tRelSeqID\tClusterID\tInPDB\tInPISA\tPDBBU\tPISABU\t" +
                "#CFGs/Cluster\t#Entry/Cluster\t#CFGS/Relation\t#Entry/Relation\tMinSeqID\t" +
                "#CFGs-Ration\t#Entry-Ratio\t#PDB-Ratio\t#PISA-Ratio");


            ProtCidSettings.progressInfo.totalOperationNum = updateRelSeqIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateRelSeqIds.Length;

            foreach (int relSeqId in updateRelSeqIds)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();


                try
                {
                    DeleteClusterSumInfoData(relSeqId);
                    GetRelationDomainClusterInfo(relSeqId, clusterInfoWriter, clusterSumInfoWriter);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue
                        ("Output " + relSeqId.ToString() + " relation cluster info error: " + ex.Message);
                }
            }
            clusterInfoWriter.Close();
            clusterSumInfoWriter.Close();
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get Pfam Domain Cluster statistical info table.");
            ProtCidSettings.logWriter.WriteLine("Get Pfam domain cluster statistical info table.");
            PrintDomainDbSumInfo("PfamDomain");
            ProtCidSettings.logWriter.WriteLine("done!");
            ProtCidSettings.logWriter.Flush();           

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateDomainClusterInfo(int[] updateRelSeqIds)
        {
            InitializeTable();
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update PFAM domain interface cluster info.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Cluster Info";

            resultDir = Path.Combine(ProtCidSettings.applicationStartPath, "HomoSeq\\newresult_domain_" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(resultDir))
            {
                Directory.CreateDirectory(resultDir);
            }

            StreamWriter clusterInfoWriter = new StreamWriter(Path.Combine(resultDir, "newPfamDomainInterfaceClusterInfo.txt"));
            StreamWriter clusterSumInfoWriter = new StreamWriter(Path.Combine(resultDir, "newPfamDomainInterfaceSumInfo.txt"));
            // write the header line of the summary info
            clusterSumInfoWriter.WriteLine("RelationString\tRelSeqID\tClusterID\tInPDB\tInPISA\tPDBBU\tPISABU\t" +
                "#CFGs/Cluster\t#Entry/Cluster\t#CFGS/Relation\t#Entry/Relation\tMinSeqID\t" +
                "#CFGs-Ration\t#Entry-Ratio\t#PDB-Ratio\t#PISA-Ratio");


            ProtCidSettings.progressInfo.totalOperationNum = updateRelSeqIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateRelSeqIds.Length;

            foreach (int relSeqId in updateRelSeqIds)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();


                try
                {
                    DeleteClusterSumInfoData(relSeqId);
                    GetRelationDomainClusterInfo(relSeqId, clusterInfoWriter, clusterSumInfoWriter);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue
                        ("Output " + relSeqId.ToString() + " relation cluster info error: " + ex.Message);
                }
            }
            clusterInfoWriter.Close();
            clusterSumInfoWriter.Close();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        private void DeleteClusterSumInfoData(int relSeqId)
        {
            string deleteString = string.Format("Delete From {0} WHere RelSeqID = {1};", clusterStatTable.TableName, relSeqId);
            protcidUpdate.Delete(deleteString);

            deleteString = string.Format("Delete From {0} Where RelSeqID = {1};", clusterSumInfoTable.TableName, relSeqId);
            protcidUpdate.Delete( deleteString);
        }
        #endregion

        #region clustering domain interfaces in a relation
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterInfoWriter"></param>
        public void GetRelationDomainClusterInfo(int relSeqId, StreamWriter clusterInfoWriter, StreamWriter clusterSumInfoWriter)
        {
            string queryString = string.Format("Select distinct ClusterID From PfamDomainInterfaceCluster Where RelSeqID = {0};",
                relSeqId);
            DataTable clusterIdTable = protcidQuery.Query(queryString);

            Dictionary<string, string[]> repHomoEntryHash = GetHomoEntriesForRelationClusters(relSeqId);

            DataTable domainInterfaceTable = GetDomainInterfaceDefTable(relSeqId);
            DataTable domainSpeciesUnpInfoTable = GetRelationDomainInterfaceSourceInfo(domainInterfaceTable);

            int clusterId = -1;
            string pdbId = "";
            int domainInterfaceId = -1;
            string homoPdbId = "";
            int homoDomainInterfaceId = -1;
            string relationString = "";

            //       string headerLine = GetHeaderLine();
            string headerLine = FormatHeaderLine();
            clusterInfoWriter.WriteLine(headerLine);

            Dictionary<string, Dictionary<string, string>> pdbEntryBuFormatHash = new Dictionary<string,Dictionary<string,string>> ();
            Dictionary<string, Dictionary<string,string>> pisaEntryBuFormatHash = new Dictionary<string,Dictionary<string,string>> ();

            int[] relationNums = GetRelationNumbers(relSeqId);
 //           ArrayList addedHomoInterfaceList = new ArrayList();
            List<string> addedInterfaceList = new List<string>();

            string clusterInfoLine = "";
 //           string relationInfoLine = "";
            double surfaceArea = 0;
            long[] interfaceDomainIds = null;

            string relationName = DownloadableFileName.GetDomainRelationName(relSeqId);
            string relationClusterInfoFile = Path.Combine(resultDir, relationName + ".txt");
            StreamWriter relationClusterInfoWriter = new StreamWriter(relationClusterInfoFile);
            relationClusterInfoWriter.WriteLine(headerLine);

            foreach (DataRow clusterRow in clusterIdTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());
                DataTable clusterInfoTable = GetDomainInterfacesInCluster(relSeqId, clusterId);
                foreach (DataRow clusterInfoRow in clusterInfoTable.Rows)
                {
                    pdbId = clusterInfoRow["PdbID"].ToString();
                    domainInterfaceId = Convert.ToInt32(clusterInfoRow["DomainInterfaceID"].ToString());
                    if (addedInterfaceList.Contains (pdbId + "_" + domainInterfaceId.ToString ()))
                    {
                        continue;
                    }
                    addedInterfaceList.Add(pdbId + "_" + domainInterfaceId.ToString());

                    surfaceArea = GetDomainInterfaceSurfaceArea(pdbId, domainInterfaceId, domainInterfaceTable);
                    if (surfaceArea < surfaceAreaCutoff)
                    {
                        continue;
                    }

                    DataRow dataRow = clusterStatTable.NewRow();
                    dataRow["RelSeqID"] = relSeqId;
                    relationString = GetFamilyCodesForRelation(relSeqId);
                    dataRow["ClusterID"] = clusterInfoRow["ClusterID"];
                    dataRow["RelCfGroupId"] = clusterInfoRow["RelCfGroupId"];
                    dataRow["SpaceGroup"] = clusterInfoRow["SpaceGroup"];
                    dataRow["CrystForm"] = clusterInfoRow["ASU"];
                    dataRow["PdbID"] = clusterInfoRow["PdbID"];
                    dataRow["DomainInterfaceID"] = clusterInfoRow["DomainInterfaceID"];
                    dataRow["ChainPfamArch"] =
                        GetDomainChainPfamArch(pdbId, Convert.ToInt32(clusterInfoRow["DomainInterfaceID"].ToString()));
                    dataRow["InterfaceUnit"] = GetCrystDomainInterfaceAbcFormat(pdbId, domainInterfaceId, relationString, domainInterfaceTable);

                    // add asu and pdb BA and Pisa BA info to data row
                    AddBAInfoToRow(pdbId, domainInterfaceId, dataRow, "pdb", pdbEntryBuFormatHash);
                    AddBAInfoToRow(pdbId, domainInterfaceId, dataRow, "pisa", pisaEntryBuFormatHash);
                    AddAsuInfoToRow(pdbId, domainInterfaceId, dataRow, domainInterfaceTable);

                    // add surface area
                    dataRow["SurfaceArea"] = surfaceArea;

                    interfaceDomainIds = GetDomainInterfaceDomainIds(pdbId, domainInterfaceId, domainInterfaceTable);
                    string[] speciesNameUnp = GetDomainInterfaceSpeciesNameUnpCode(pdbId, interfaceDomainIds, domainSpeciesUnpInfoTable);

                    dataRow["Species"] = speciesNameUnp[0];
                    dataRow["Name"] = speciesNameUnp[1];
                    dataRow["UnpCode"] = speciesNameUnp[2];

                    clusterStatTable.Rows.Add(dataRow);

                    if (repHomoEntryHash.ContainsKey(pdbId))
                    {
                        string[] homoEntries = (string[])repHomoEntryHash[pdbId];
                      
                        if (homoEntries.Length > 0)
                        {
                            string[] homoInterfaces = GetSimilarHomoEntryInterfaces(relSeqId, pdbId, domainInterfaceId, homoEntries);
                            foreach (string homoInterface in homoInterfaces)
                            {
                                if (addedInterfaceList.Contains(homoInterface))
                                {
                                    continue;
                                }
                                string[] homoInterfaceFields = homoInterface.Split('_');
                                homoPdbId = homoInterfaceFields[0];
                                homoDomainInterfaceId = Convert.ToInt32(homoInterfaceFields[1]);
                                surfaceArea = GetDomainInterfaceSurfaceArea(homoPdbId, homoDomainInterfaceId, domainInterfaceTable);
                                if (surfaceArea < surfaceAreaCutoff)
                                {
                                    continue;
                                }

                                DataRow homoDataRow = clusterStatTable.NewRow();
                                homoDataRow.ItemArray = dataRow.ItemArray;
                                homoDataRow["PdbID"] = homoPdbId;
                                homoDataRow["DomainInterfaceID"] = homoDomainInterfaceId;

                                // add asu and pdb BA and Pisa BA info to data row
                                AddBAInfoToRow(homoPdbId, homoDomainInterfaceId, dataRow, "pdb", pdbEntryBuFormatHash);
                                AddBAInfoToRow(homoPdbId, homoDomainInterfaceId, dataRow, "pisa", pisaEntryBuFormatHash);
                                AddAsuInfoToRow(homoPdbId, homoDomainInterfaceId, dataRow, domainInterfaceTable);

                                // add surface area
                                dataRow["SurfaceArea"] = surfaceArea;

                                interfaceDomainIds = GetDomainInterfaceDomainIds(homoPdbId, homoDomainInterfaceId, domainInterfaceTable);

                                string[] homoSpeciesNameUnp = GetDomainInterfaceSpeciesNameUnpCode(homoPdbId, interfaceDomainIds, domainSpeciesUnpInfoTable);

                                dataRow["Species"] = homoSpeciesNameUnp[0];
                                dataRow["Name"] = homoSpeciesNameUnp[1];
                                dataRow["UnpCode"] = homoSpeciesNameUnp[2];

                                clusterStatTable.Rows.Add(homoDataRow);
                                addedInterfaceList.Add(homoInterface);
                            }
                        }
                    }
                }
                AddClusterSumInfo(relSeqId, clusterId, relationNums[0], relationNums[1]);
                clusterInfoLine = WriteRelationDomainClusterInfo(clusterInfoWriter, clusterSumInfoWriter);
     //           relationInfoLine += clusterInfoLine;
                relationClusterInfoWriter.Write(clusterInfoLine);
                relationClusterInfoWriter.Flush();
            }
    //        relationInfoLine = relationInfoLine.TrimEnd("\r\n".ToCharArray());                      
    //        relationClusterInfoWriter.WriteLine(relationInfoLine);
            relationClusterInfoWriter.Close();
            ParseHelper.ZipPdbFile(relationClusterInfoFile);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private Dictionary<string, string[]> GetHomoEntriesForRelationClusters(int relSeqId)
        {
            string queryString = string.Format("Select distinct PdbID From PfamDomainInterfaceCluster Where RelSeqID = {0};",
                relSeqId);
            DataTable entryTable = protcidQuery.Query(queryString);
            string repEntry = "";
            Dictionary<string, string[]> repHomoEntryHash = new Dictionary<string, string[]>();
            foreach (DataRow entryRow in entryTable.Rows)
            {
                repEntry = entryRow["PdbID"].ToString();
                string[] homoEntries = GetHomoEntries(repEntry);
                repHomoEntryHash.Add(repEntry, homoEntries);
            }
            return repHomoEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private long[] GetDomainInterfaceDomainIds(string pdbId, int domainInterfaceId, DataTable domainInterfaceTable)
        {
            DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            long[] domainIds = new long[2];
            domainIds[0] = Convert.ToInt64(domainInterfaceRows[0]["DomainID1"].ToString());
            domainIds[1] = Convert.ToInt64(domainInterfaceRows[0]["DomainID2"].ToString());
            return domainIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetHomoEntries(string pdbId)
        {
            string queryString = string.Format("Select Distinct PdbID2 From PfamHomoRepEntryAlign Where PdbID1 = '{0}';", pdbId);
            DataTable homoEntryTable = protcidQuery.Query(queryString);
            string[] homoEntries = new string[homoEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow homoEntryRow in homoEntryTable.Rows)
            {
                homoEntries[count] = homoEntryRow["PdbID2"].ToString();
                count++;
            }
            return homoEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="repPdb"></param>
        /// <param name="repDomainInterfaceId"></param>
        /// <param name="homoEntries"></param>
        /// <returns></returns>
        private string[] GetSimilarHomoEntryInterfaces(int relSeqId, string repPdb, int repDomainInterfaceId, string[] homoEntries)
        {
            List<string> homoInterfaceList = new List<string> ();
            string homoInterface = "";
            string queryString = string.Format("Select Distinct PdbID2 As HomoPdbID, DomainInterfaceID2 As HomoDomainInterfaceID " +
                   " From {0} Where RelSeqID = {1} AND PdbID1 = '{2}' AND " +
                   " DomainInterfaceID1 = {3} AND PdbID2 In ({4}) AND QScore >= {5};",
                   domainInterfaceCompTable, relSeqId, repPdb, repDomainInterfaceId, ParseHelper.FormatSqlListString(homoEntries), AppSettings.parameters.simInteractParam.interfaceSimCutoff);
            DataTable simHomoInterfaceTable = protcidQuery.Query(queryString);

            foreach (DataRow simInterfaceRow in simHomoInterfaceTable.Rows)
            {
                homoInterface = simInterfaceRow["HomoPdbID"].ToString() + "_" + simInterfaceRow["HomoDomainInterfaceID"].ToString();
                homoInterfaceList.Add(homoInterface);
            }

            queryString = string.Format("Select Distinct PdbID1 As HomoPdbID, DomainInterfaceID1 As HomoDomainInterfaceID " +
                " From {0} Where RelSeqID = {1} AND PdbID2 = '{2}' AND " +
                " DomainInterfaceID2 = {3} AND PdbID1 IN ({4}) AND QScore >= {5};",
                domainInterfaceCompTable, relSeqId, repPdb, repDomainInterfaceId, ParseHelper.FormatSqlListString(homoEntries), AppSettings.parameters.simInteractParam.interfaceSimCutoff);
            simHomoInterfaceTable = protcidQuery.Query(queryString);

            foreach (DataRow simInterfaceRow in simHomoInterfaceTable.Rows)
            {
                homoInterface = simInterfaceRow["HomoPdbID"].ToString() + "_" + simInterfaceRow["HomoDomainInterfaceID"].ToString();
                homoInterfaceList.Add(homoInterface);
            }

            queryString = string.Format("Select Distinct PdbID1, DomainInterfaceID1, PdbId2, DomainInterfaceID2 " +
                " From {0} Where RelSeqID = {1} AND PdbID1 IN ({2}) AND PdbID2 IN ({2}) AND QScore >= {3};",
                domainInterfaceCompTable, relSeqId, ParseHelper.FormatSqlListString(homoEntries), homoSimQscoreCutoff);
            simHomoInterfaceTable = protcidQuery.Query(queryString);

            string homoInterface2 = "";
            List<string> newAddedSimHomoInterfaceList = new List<string> ();
            foreach (DataRow simInterfaceRow in simHomoInterfaceTable.Rows)
            {
                homoInterface = simInterfaceRow["PdbID1"].ToString() + "_" + simInterfaceRow["DomainInterfaceID1"].ToString();
                homoInterface2 = simInterfaceRow["PdbID2"].ToString() + "_" + simInterfaceRow["DomainInterfaceID2"].ToString();
                if (homoInterfaceList.Contains (homoInterface))
                {
                    if (!newAddedSimHomoInterfaceList.Contains(homoInterface2))
                    {
                        newAddedSimHomoInterfaceList.Add(homoInterface2);
                    }
                }
                else if (homoInterfaceList.Contains(homoInterface2))
                {
                    if (! newAddedSimHomoInterfaceList.Contains(homoInterface))
                    {
                        newAddedSimHomoInterfaceList.Add(homoInterface);
                    }
                }
            }
            foreach (string newHomoInterface in newAddedSimHomoInterfaceList)
            {
                if (! homoInterfaceList.Contains (newHomoInterface))
                {
                    homoInterfaceList.Add(newHomoInterface);
                }
            }
            return homoInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private DataTable GetDomainInterfaceDefTable(int relSeqId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where RelSeqID = {0} AND SurfaceArea > {1};", relSeqId, surfaceAreaCutoff);
            DataTable domainInterfaceTable = protcidQuery.Query(queryString);
            return domainInterfaceTable;
        }
        #endregion

        #region BA and ASU info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="dataRow"></param>
        /// <param name="buType"></param>
        /// <param name="entryBuFormatHash"></param>
        public void AddBAInfoToRow(string pdbId, int domainInterfaceId, DataRow dataRow, string buType, Dictionary<string, Dictionary<string, string>> entryBuFormatHash)
        {
            string[] entryBAs = IsDomainInterfaceInBU(pdbId, domainInterfaceId, buType);
            string buId = "";
            if (entryBAs.Length > 0)
            {
                buId = entryBAs[0];
                dataRow["In" + buType] = 1;
            }
            else
            {
                dataRow["In" + buType] = 0;
                buId = "-1";
            }
            dataRow[buType + "BU"] = GetBuAbcFormat(pdbId, ref buId, buType, ref entryBuFormatHash);
            dataRow[buType + "BuID"] = buId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="dataRow"></param>
        /// <param name="buType"></param>
        /// <param name="entryBuFormatHash"></param>
        private void AddAsuInfoToRow(string pdbId, int domainInterfaceId, DataRow dataRow, DataTable domainInterfaceTable)
        {
            bool inAsu = IsDomainInterfaceInAsu(pdbId, domainInterfaceId, domainInterfaceTable);
            if (inAsu)
            {
                dataRow["InAsu"] = 1;
            }
            else
            {
                dataRow["InASU"] = 0;
            }
            dataRow["ASU"] = GetAsuFromCrystForm(dataRow["CrystForm"].ToString());
        }      

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crystForm"></param>
        /// <returns></returns>
        private string GetAsuFromCrystForm(string crystForm)
        {
            int parenthesisIndex = crystForm.IndexOf('(');
            string asu = "";
            if (parenthesisIndex > -1)
            {
                asu = crystForm.Substring(0, parenthesisIndex);
            }
            else
            {
                asu = crystForm;
            }
            return asu;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private double GetDomainInterfaceSurfaceArea(string pdbId, int domainInterfaceId, DataTable domainInterfaceTable)
        {
            DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            if (domainInterfaceRows.Length > 0)
            {
                return Convert.ToDouble(domainInterfaceRows[0]["SurfaceArea"].ToString());
            }
            return -1;
        }

         /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private double GetDomainInterfaceSurfaceArea(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format ("Select SurfaceArea From PfamDomainInterfaces Where PdbID = '{0}' AND DomainInterfaceId = {1};", pdbId, domainInterfaceId);
            DataTable saTable = ProtCidSettings.protcidQuery.Query (queryString);
            if (saTable.Rows.Count > 0)
            {
                return Convert.ToDouble(saTable.Rows[0]["SurfaceArea"].ToString());
            }
            return -1;
        }
        #endregion

        #region species, name and uniprot code of domain interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainIds"></param>
        /// <param name="domainSpeciesUnpInfoTable"></param>
        /// <returns></returns>
        private string[] GetDomainInterfaceSpeciesNameUnpCode(string pdbId, long[] domainIds, DataTable domainSpeciesUnpInfoTable)
        {
            string species = "";
            string name = "";
            string unpCode = "";
            DataRow[] domainRows = domainSpeciesUnpInfoTable.Select(string.Format("PdbID = '{0}' AND DomainID = '{1}'", pdbId, domainIds[0]));
            if (domainRows.Length > 0)
            {
                species = domainRows[0]["Species"].ToString().TrimEnd();
                name = domainRows[0]["Name"].ToString().TrimEnd();
                unpCode = domainRows[0]["UnpCode"].ToString().TrimEnd();
            }
            if (domainIds[1] != domainIds[0])
            {
                domainRows = domainSpeciesUnpInfoTable.Select(string.Format("PdbID = '{0}' AND DomainID = '{1}'", pdbId, domainIds[1]));
                if (domainRows.Length > 0)
                {
                    species = species + ";" + domainRows[0]["Species"].ToString().TrimEnd();
                    name = name + ";" + domainRows[0]["Name"].ToString().TrimEnd();
                    unpCode = unpCode + ";" + domainRows[0]["UnpCode"].ToString().TrimEnd();
                }
            }
            if (species == "" || species == ";")
            {
                species = "-";
            }
            if (name == "" || species == ";")
            {
                name = "-";
            }
            if (unpCode == "" || unpCode == ";")
            {
                unpCode = "-";
            }
            string[] spNameUnp = new string[3];
            spNameUnp[0] = species.Replace("\'", "\'\'");  // Skipped single Quot 
            spNameUnp[1] = name.Replace("\'", "\'\'");
            spNameUnp[2] = unpCode;
            return spNameUnp;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="relationDomainInterfaceTable"></param>
        /// <returns></returns>
        private DataTable GetRelationDomainInterfaceSourceInfo(DataTable relationDomainInterfaceTable)
        {
            DataTable domainSourceInfoTable = new DataTable();
            string[] sourceInfoColumns = { "PdbID", "DomainID", "EntityID", "Name", "Species", "UnpCode" };
            foreach (string srcInfoCol in sourceInfoColumns)
            {
                domainSourceInfoTable.Columns.Add(new DataColumn(srcInfoCol));
            }
            string[] entryDomains = GetEntryDomains(relationDomainInterfaceTable);
            string pdbId = "";
            long domainId = 0;
            int entityId = 0;
            string unpCode = "";
            foreach (string entryDomain in entryDomains)
            {
                pdbId = entryDomain.Substring(0, 4);

                domainId = Convert.ToInt64(entryDomain.Substring(4, entryDomain.Length - 4));
                DataTable domainInfoTable = GetDomainInfo(pdbId, domainId);
                entityId = -1;
                if (domainInfoTable.Rows.Count > 0)
                {
                    entityId = Convert.ToInt32(domainInfoTable.Rows[0]["EntityID"].ToString());
                }
                Range[] domainRanges = FormatDomainRanges(domainInfoTable);
                DataRow dataRow = domainSourceInfoTable.NewRow();
                dataRow["PdbID"] = pdbId;
                dataRow["DomainID"] = domainId;
                dataRow["EntityID"] = entityId;
                string[] speciesName = GetSpeciesNameInfo(pdbId, entityId);
                dataRow["Species"] = speciesName[0];
                dataRow["Name"] = speciesName[1];
                unpCode = GetDomainUniprotCode(pdbId, entityId, domainRanges);
                dataRow["UnpCode"] = unpCode;
                domainSourceInfoTable.Rows.Add(dataRow);
            }
            return domainSourceInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private DataTable GetDomainInfo(string pdbId, long domainId)
        {
            string queryString = string.Format("Select EntityID, SeqStart, SeqEnd From PdbPfam Where PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);
            DataTable domainInfoTable = pdbfamQuery.Query(queryString);
            return domainInfoTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInfoTable"></param>
        /// <returns></returns>
        private Range[] FormatDomainRanges(DataTable domainInfoTable)
        {
            Range[] domainRanges = new Range[domainInfoTable.Rows.Count];
            int count = 0;
            foreach (DataRow domainRow in domainInfoTable.Rows)
            {
                domainRanges[count] = new Range();
                domainRanges[count].startPos = Convert.ToInt32(domainRow["SeqStart"].ToString());
                domainRanges[count].endPos = Convert.ToInt32(domainRow["SeqEnd"].ToString());
                count++;
            }
            return domainRanges;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private int GetDomainEntity(string pdbId, long domainId)
        {
            string queryString = string.Format("Select Distinct EntityID From PdbPfam Where PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);
            DataTable entityTable = pdbfamQuery.Query(queryString);
            int entityId = -1;
            if (entityTable.Rows.Count > 0)
            {
                entityId = Convert.ToInt32(entityTable.Rows[0]["EntityID"].ToString());
            }
            return entityId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="domainRange"></param>
        /// <returns></returns>
        private string GetDomainUniprotCode(string pdbId, int entityId, Range[] domainRanges)
        {
            string queryString = string.Format("Select PdbDbRefSifts.RefID, AlignID, SeqAlignBeg, SeqAlignEnd, DbCode " +
                " From PdbDbRefSifts, PdbDbRefSeqSifts Where PdbDbRefSifts.PdbID = '{0}' AND PdbDbRefSifts.EntityID = {1} AND " +
                " PdbDbRefSifts.DbName = 'UNP' AND " +
                " PdbDbRefSifts.PdbID = PdbDbRefSeqSifts.PdbID AND " +
                " PdbDbRefSifts.RefID = PdbDbRefSeqSifts.RefID Order By AlignID;", pdbId, entityId);
            DataTable dbRefTable = pdbfamQuery.Query(queryString);
            if (dbRefTable.Rows.Count == 0)
            {
                queryString = string.Format("Select PdbDbRefXml.RefID, AlignID, SeqAlignBeg, SeqAlignEnd, DbCode " +
                       " From PdbDbRefXml, PdbDbRefSeqXml Where PdbDbRefXml.PdbID = '{0}' AND PdbDbRefXml.EntityID = {1} AND " +
                       " PdbDbRefXml.DbName = 'UNP' AND " +
                       " PdbDbRefXml.PdbID = PdbDbRefSeqXml.PdbID AND " +
                       " PdbDbRefXml.RefID = PdbDbRefSeqXml.RefID Order By AlignID;", pdbId, entityId);
                dbRefTable = pdbfamQuery.Query(queryString);
            }
            Dictionary<int, Range[]> refUnpRangeHash = GetUnpRanges(dbRefTable);
            string unpCode = "";
            foreach (int refId in refUnpRangeHash.Keys)
            {
                Range[] unpRanges = (Range[])refUnpRangeHash[refId];
                if (IsDomainUnpSeqOverlap(domainRanges, unpRanges))
                {
                    unpCode = GetRefUnpCode(refId, dbRefTable);
                }
            }
            return unpCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="refId"></param>
        /// <param name="dbRefTable"></param>
        /// <returns></returns>
        private string GetRefUnpCode(int refId, DataTable dbRefTable)
        {
            DataRow[] refRows = dbRefTable.Select(string.Format("RefID = '{0}'", refId));
            string unpCode = "";
            if (refRows.Length > 0)
            {
                unpCode = refRows[0]["DbCode"].ToString().TrimEnd();
            }
            return unpCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbRefTable"></param>
        /// <returns></returns>
        private Dictionary<int, Range[]> GetUnpRanges(DataTable dbRefTable)
        {
            List<int> refIdList = new List<int> ();
            int refId = 0;
            foreach (DataRow dbRefRow in dbRefTable.Rows)
            {
                refId = Convert.ToInt32(dbRefRow["RefID"].ToString());
                if (!refIdList.Contains(refId))
                {
                    refIdList.Add(refId);
                }
            }
            Dictionary<int, Range[]> entityUnpRangeHash = new Dictionary<int,Range[]> ();
            foreach (int lsRefId in refIdList)
            {
                DataRow[] refIdRows = dbRefTable.Select(string.Format("RefID = '{0}'", lsRefId));
                int alignId = Convert.ToInt32(refIdRows[0]["AlignID"].ToString());
                DataRow[] dbRefRows = dbRefTable.Select(string.Format("AlignID = '{0}'", alignId));
                Range[] unpRanges = new Range[dbRefRows.Length];
                for (int i = 0; i < dbRefRows.Length; i++)
                {
                    unpRanges[i] = new Range();
                    unpRanges[i].startPos = Convert.ToInt32(dbRefRows[i]["SeqAlignBeg"].ToString());
                    unpRanges[i].endPos = Convert.ToInt32(dbRefRows[i]["SeqAlignEnd"].ToString());
                }
                entityUnpRangeHash.Add(lsRefId, unpRanges);
            }
            return entityUnpRangeHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainRanges"></param>
        /// <param name="unpRanges"></param>
        /// <returns></returns>
        private bool IsDomainUnpSeqOverlap(Range[] domainRanges, Range[] unpRanges)
        {
            int overlap_all = 0;
            int rangeOverlap = 0;
            foreach (Range domainRange in domainRanges)
            {
                foreach (Range unpRange in unpRanges)
                {
                    rangeOverlap = GetRangeOverlap(domainRange, unpRange);
                    overlap_all += rangeOverlap;
                }
            }
            int domainLength = GetRangeLength(domainRanges);
            int unpLength = GetRangeLength(unpRanges);
            double domainCoverage = (double)overlap_all / (double)(domainLength);
            double unpCoverage = (double)overlap_all / (double)(unpLength);
            if (domainCoverage >= rangeCoverage || unpCoverage >= rangeCoverage)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ranges"></param>
        /// <returns></returns>
        private int GetRangeLength(Range[] ranges)
        {
            int rangeLength = 0;
            foreach (Range range in ranges)
            {
                rangeLength += (range.endPos - range.startPos + 1);
            }
            return rangeLength;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="range1"></param>
        /// <param name="range2"></param>
        /// <returns></returns>
        private int GetRangeOverlap(Range range1, Range range2)
        {
            int maxStart = Math.Max(range1.startPos, range2.startPos);
            int minEnd = Math.Min(range1.endPos, range2.endPos);
            int overlap = minEnd - maxStart + 1;
            if (overlap < 0)
            {
                overlap = 0;
            }
            return overlap;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetSpeciesNameInfo(string pdbId, int entityId)
        {
            string queryString = string.Format("Select Species, Name From AsymUnit WHERE PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable speciesNameTable = pdbfamQuery.Query(queryString);
            string[] speciesName = new string[2];
            if (speciesNameTable.Rows.Count > 0)
            {
                speciesName[0] = speciesNameTable.Rows[0]["Species"].ToString().TrimEnd();
                speciesName[1] = speciesNameTable.Rows[0]["Name"].ToString().TrimEnd();
            }
            else
            {
                speciesName[0] = "-";
                speciesName[1] = "-";
            }
            return speciesName;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relationDomainInterfaceTable"></param>
        /// <returns></returns>
        private string[] GetEntryDomains(DataTable relationDomainInterfaceTable)
        {
            List<string> entryDomainList = new List<string> ();
            string entryDomain = "";
            foreach (DataRow domainInterfaceRow in relationDomainInterfaceTable.Rows)
            {
                entryDomain = domainInterfaceRow["PdbID"].ToString() + domainInterfaceRow["DomainID1"].ToString();
                if (!entryDomainList.Contains(entryDomain))
                {
                    entryDomainList.Add(entryDomain);
                }
                entryDomain = domainInterfaceRow["PdbID"].ToString() + domainInterfaceRow["DomainID2"].ToString();
                if (!entryDomainList.Contains(entryDomain))
                {
                    entryDomainList.Add(entryDomain);
                }
            }
            return entryDomainList.ToArray ();
        }
        #endregion

        #region print cluster info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="numOfCfgRelation"></param>
        /// <param name="numOfEntryRelation"></param>
        /// <param name="clusterInfoWriter"></param>
        private string WriteRelationDomainClusterInfo(StreamWriter clusterInfoWriter, StreamWriter clusterSumInfoWriter)
        {
            int relSeqId = Convert.ToInt32(clusterStatTable.Rows[0]["RelSeqID"].ToString());
            int clusterId = Convert.ToInt32(clusterStatTable.Rows[0]["ClusterID"].ToString());
            string relationString = GetFamilyCodesForRelation(relSeqId);

            string clusterInfoLine = "";
            string dataLine = "";

            string sumLine = FormatSumInfoLineForClusterInterfaces(relSeqId, clusterId, relationString, clusterSumInfoTable.Rows[0]);
            clusterInfoWriter.WriteLine(sumLine);
            clusterInfoLine = sumLine + "\r\n";

            foreach (DataRow interfaceRow in clusterStatTable.Rows)
            {
                dataLine = "";
                foreach (string headerCol in headerCols)
                {
                    if (clusterStatTable.Columns.Contains(headerCol))
                    {
                        dataLine += (interfaceRow[headerCol].ToString() + "\t");
                    }
                    else
                    {
                        dataLine += "\t";
                    }
                }
                dataLine = dataLine.TrimEnd('\t');
                clusterInfoWriter.WriteLine(dataLine);
                clusterInfoLine += (dataLine + "\r\n");
            }
            clusterInfoWriter.WriteLine("");
            clusterInfoWriter.Flush();

            //      string clusterSumInfoLine = ParseHelper.FormatDataRow(clusterSumInfoTable.Rows[0]);
            string clusterSumInfoLine = FormatSumInfoForSummaryInfo(relationString, clusterSumInfoTable.Rows[0]);
            clusterSumInfoWriter.WriteLine(clusterSumInfoLine);
            clusterSumInfoWriter.Flush();

            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, clusterStatTable);
            clusterStatTable.Clear();

            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, clusterSumInfoTable);
            clusterSumInfoTable.Clear();

            return clusterInfoLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="clusterSumInfo"></param>
        /// <returns></returns>
        private string FormatSumInfoLineForClusterInterfaces(int relSeqId, int clusterId, string relationString, DataRow clusterSumInfoRow)
        {
            string sumInfoLine = "";
            foreach (string headerCol in headerCols)
            {
                if (headerCol == "RelSeqID")
                {
                    sumInfoLine += (relationString + "\t");
                }
                else
                {
                    if (clusterSumInfoRow.Table.Columns.Contains(headerCol))
                    {
                        sumInfoLine += (clusterSumInfoRow[headerCol] + "\t");
                    }
                    else
                    {
                        sumInfoLine += "\t";
                    }
                }
            }

            return sumInfoLine;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="clusterSumInfo"></param>
        /// <returns></returns>
        private string FormatSumInfoForSummaryInfo(string relationString, DataRow clusterSumInfoRow)
        {
            string sumInfoLine = relationString + "\t" +
                ParseHelper.FormatDataRow(clusterSumInfoRow) + "\t";

            /*     foreach (DataColumn dCol in clusterStatTable.Columns)
                 {
                     switch (dCol.ColumnName.ToLower ())
                     {
                         case "relseqid":
                             sumInfoLine += (relSeqId.ToString() + "\t");
                             break;

                         case "clusterid":
                             sumInfoLine += (clusterId.ToString() + "\t");
                             break;

                         case "inasu":
                             sumInfoLine += (clusterSumInfoRow["InAsu"].ToString() + "\t");
                             break;

                         case "inpdb":
                             sumInfoLine += (clusterSumInfoRow["InPdb"].ToString() + "\t");
                             break;

                         case "inpisa":
                             sumInfoLine += (clusterSumInfoRow["InPisa"].ToString() + "\t");
                             break;
                        
                         default:
                             break;
                     }
                 }
                 sumInfoLine += (clusterSumInfo.numOfCfgCluster.ToString() + "\t" +
                     clusterSumInfoRow["numOfEntryCluster"].ToString() + "\t" +
                     clusterSumInfoRow["numOfCfgRelation"].ToString() + "\t" +
                     clusterSumInfoRow["numOfEntryRelation"].ToString() + "\t" + 
                     clusterSumInfoRow["minSeqId"].ToString () + "\t");*/
            sumInfoLine += (string.Format("{0:0.###}", Convert.ToDouble(clusterSumInfoRow["numOfCfgCluster"].ToString()) /
                    Convert.ToDouble(clusterSumInfoRow["numOfCfgRelation"].ToString())) + "\t");
            sumInfoLine += (string.Format("{0:0.###}", Convert.ToDouble(clusterSumInfoRow["numOfEntryCluster"].ToString()) /
                Convert.ToDouble(clusterSumInfoRow["numOfEntryRelation"].ToString())) + "\t");
            sumInfoLine += (string.Format("{0:0.###}", Convert.ToDouble(clusterSumInfoRow["InPdb"].ToString()) /
                Convert.ToDouble(clusterSumInfoRow["numOfEntryCluster"].ToString())) + "\t");
            sumInfoLine += (string.Format("{0:0.###}", Convert.ToDouble(clusterSumInfoRow["InPisa"].ToString()) /
                Convert.ToDouble(clusterSumInfoRow["numOfEntryCluster"].ToString())) + "\t");

            return sumInfoLine;
        }
        #endregion

        #region get data from db
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private DataTable GetDomainInterfacesInCluster(int relSeqId, int clusterId)
        {
            string queryString = string.Format("Select * From {0}DomainInterfaceCluster " +
                " Where RelSeqID = {1} AND ClusterID = {2};", ProtCidSettings.dataType, relSeqId, clusterId);
            DataTable clusterTable = protcidQuery.Query(queryString);
            //      GetHomoEntryDomainInterfaces(ref clusterTable);
            return clusterTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterTable"></param>
        private void GetHomoEntryDomainInterfaces(ref DataTable clusterTable)
        {
            Dictionary<string, List<int>> entryDomainInterfaceHash = new Dictionary<string,List<int>> ();
            string pdbId = "";
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                pdbId = clusterRow["PdbID"].ToString();
                if (entryDomainInterfaceHash.ContainsKey(pdbId))
                {
                    entryDomainInterfaceHash[pdbId].Add(Convert.ToInt32(clusterRow["DomainInterfaceID"].ToString()));
                }
                else
                {
                    List<int> dInterfaceList = new List<int> ();
                    dInterfaceList.Add(Convert.ToInt32(clusterRow["DomainInterfaceID"].ToString()));
                    entryDomainInterfaceHash.Add(pdbId, dInterfaceList);
                }
            }
            foreach (string entry in entryDomainInterfaceHash.Keys)
            {
                List<int> repDomainInterfaceList = entryDomainInterfaceHash[entry];
                DataRow[] entryInterfaceRows = clusterTable.Select(string.Format("PdbID = '{0}'", entry));
                string[] homoEntries = GetHomoEntries(entry);
                foreach (string homoEntry in homoEntries)
                {
                    int[] simDomainInterfaceIds = GetSimilarDomainInterfacesFromHomoEntry(entry, repDomainInterfaceList.ToArray (), homoEntry);
                    foreach (int simDomainInterfaceId in simDomainInterfaceIds)
                    {
                        DataRow clusterRow = clusterTable.NewRow();
                        clusterRow.ItemArray = entryInterfaceRows[0].ItemArray;
                        clusterRow["PdbID"] = homoEntry;
                        clusterRow["DomainInterfaceID"] = simDomainInterfaceId;
                        clusterTable.Rows.Add(clusterRow);
                    }
                }
            }
            clusterTable.AcceptChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repEntry"></param>
        /// <param name="homoEntry"></param>
        /// <returns></returns>
        private int[] GetSimilarDomainInterfacesFromHomoEntry(string repEntry, int[] clusterRepDInterfaceIds, string homoEntry)
        {
             List<int> simDInterfaceList = new List<int> ();
            int domainInterfaceId = -1;

            string queryString = string.Format("Select * From pfamdomaininterfacecomp " +
                " Where PdbID1 = '{0}' AND PdbID2 = '{1}' " +
                " AND DomainInterfaceID1 IN ({2}) AND QScore >= {3};",
                repEntry, homoEntry, ParseHelper.FormatSqlListString (clusterRepDInterfaceIds),
                AppSettings.parameters.simInteractParam.interfaceSimCutoff);
            DataTable domainInterfaceCompTable = protcidQuery.Query(queryString);

            foreach (DataRow interfaceCompRow in domainInterfaceCompTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(interfaceCompRow["DomainInterfaceID2"].ToString());
                if (!simDInterfaceList.Contains(domainInterfaceId))
                {
                    simDInterfaceList.Add(domainInterfaceId);
                }
            }
            return simDInterfaceList.ToArray ();
        }
        #endregion

        #region domain interface ABC format
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private string GetCrystDomainInterfaceAbcFormat(string pdbId, int domainInterfaceId, string relationString, DataTable domainInterfaceTable)
        {
            /*       string queryString = string.Format("Select * From PfamDomainInterfaces " +
                       " Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
                   DataTable domainInterfaceTable = dbQuery.Query(queryString);*/
            DataRow[] domainInterfaceRows = domainInterfaceTable.Select
                (string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            int interfaceId = -1;
            string[] familyCodes = GetPfamCodesFromRelation(relationString);
            if (domainInterfaceRows.Length > 0)
            {
                interfaceId = Convert.ToInt32(domainInterfaceRows[0]["InterfaceID"]);
                if (interfaceId == 0) // intra
                {
                    if (familyCodes[0] == familyCodes[1])
                    {
                        return "A-A";
                    }
                    else
                    {
                        return "A-B";
                    }
                }
                else
                {
                    if (familyCodes[0] == familyCodes[1])
                    {
                        if (IsInterfaceHomodimer(pdbId, interfaceId))
                        {
                            return "A2";
                        }
                        else
                        {
                            return "AB";
                        }
                    }
                    else
                    {
                        return "AB";
                    }

                }
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private string GetCrystDomainInterfaceAbcFormat(string pdbId, int domainInterfaceId, string relationString)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces " +
                " Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);

            int interfaceId = -1;
            string[] familyCodes = GetPfamCodesFromRelation(relationString);
            if (domainInterfaceTable.Rows.Count > 0)
            {
                interfaceId = Convert.ToInt32(domainInterfaceTable.Rows[0]["InterfaceID"]);
                if (interfaceId == 0) // intra
                {
                    if (familyCodes[0] == familyCodes[1])
                    {
                        return "A-A";
                    }
                    else
                    {
                        return "A-B";
                    }
                }
                else
                {
                    if (familyCodes[0] == familyCodes[1])
                    {
                        if (IsInterfaceHomodimer(pdbId, interfaceId))
                        {
                            return "A2";
                        }
                        else
                        {
                            return "AB";
                        }
                    }
                    else
                    {
                        return "AB";
                    }

                }
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relationString"></param>
        /// <returns></returns>
        private string[] GetPfamCodesFromRelation(string relationString)
        {
            string[] fields = relationString.Split(')');
            string[] relationPfamCodes = new string[2];
            relationPfamCodes[0] = fields[0] + ")";
            if (fields[1] == "")
            {
                relationPfamCodes[1] = fields[0] + ")";
            }
            else
            {
                relationPfamCodes[1] = fields[1].TrimStart('_') + ")";
            }
            return relationPfamCodes;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private bool IsInterfaceHomodimer(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select EntityID1, EntityID2 From CrystEntryInterfaces " +
                " Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable entityTable = protcidQuery.Query(queryString);
            if (entityTable.Rows.Count > 0)
            {
                int entityId1 = Convert.ToInt32(entityTable.Rows[0]["EntityID1"].ToString());
                int entityId2 = Convert.ToInt32(entityTable.Rows[0]["EntityID2"].ToString());
                if (entityId1 == entityId2)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region chain pfam arch
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        public string GetDomainChainPfamArch(string pdbId, int domainInterfaceId)
        {
            string domainChainPfamArch = "";
            string queryString = string.Format("SELECT * FROM PfamDomainInterfaces " +
                " WHERE PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable domainTable = protcidQuery.Query(queryString);
            if (domainTable.Rows.Count > 0)
            {
                string asymChain1 = domainTable.Rows[0]["AsymChain1"].ToString().TrimEnd();
                string asymChain2 = domainTable.Rows[0]["AsymChain2"].ToString().TrimEnd();
                int entityId1 = -1;
                int entityId2 = -1;
                entityId1 = GetEntityIDForAsymChain(pdbId, asymChain1);
                string chainPfamArch1 = GetChainPfamArch(pdbId, entityId1);
                if (asymChain1 == asymChain2)
                {
                    domainChainPfamArch = chainPfamArch1;
                }
                else
                {
                    entityId2 = GetEntityIDForAsymChain(pdbId, asymChain2);
                    if (entityId1 == entityId2)
                    {
                        domainChainPfamArch = chainPfamArch1;
                    }
                    else
                    {
                        string chainPfamArch2 = GetChainPfamArch(pdbId, entityId2);
                        domainChainPfamArch = chainPfamArch1 + ";" + chainPfamArch2;
                    }
                }
            }
            return domainChainPfamArch;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private int GetEntityIDForAsymChain(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select EntityID From AsymUnit Where PdbId = '{0}' AND AsymID = '{1}';",
                pdbId, asymChain);
            DataTable entityTable = pdbfamQuery.Query(queryString);
            int entityId = -1;
            if (entityTable.Rows.Count > 0)
            {
                entityId = Convert.ToInt32(entityTable.Rows[0]["EntityID"].ToString());
            }
            return entityId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetChainPfamArch(string pdbId, int entityId)
        {
            string queryString = string.Format("Select PfamArch From PfamEntityPfamArch Where PdbID = '{0}' AND EntityID = {1};",
                pdbId, entityId);
            DataTable pfamArchTable = pdbfamQuery.Query(queryString);
            if (pfamArchTable.Rows.Count > 0)
            {
                return pfamArchTable.Rows[0]["PfamArch"].ToString().TrimEnd();
            }
            return "";
        }
        #endregion

        #region domain interfaces in BUs
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceInAsu(string pdbId, int domainInterfaceId, DataTable domainInterfaceTable)
        {
            //     string queryString = string.Format("Select InterfaceID From PfamDomainInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            //     DataTable interfaceIdTable = dbQuery.Query(queryString);
            DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            if (domainInterfaceRows.Length > 0)
            {
                int interfaceId = Convert.ToInt32(domainInterfaceRows[0]["InterfaceID"].ToString());
                if (interfaceId == 0)
                {
                    return true; // intra-chain domain interfaces, added on June 9, 2014
                }
                string queryString = string.Format("Select AsymChain1, SymmetryString1, AsymChain2, SymmetryString2 From CrystEntryInterfaces  " +
                    " WHere PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
                DataTable chainSymOpTable = protcidQuery.Query(queryString);
                if (chainSymOpTable.Rows.Count > 0)
                {
                    string symmetryString1 = chainSymOpTable.Rows[0]["SymmetryString1"].ToString().TrimEnd();
                    string symmetryString2 = chainSymOpTable.Rows[0]["SymmetryString2"].ToString().TrimEnd();
                    if (symmetryString1 == "1_555" && symmetryString2 == "1_555")
                    {
                        return true;
                    }
                }
            }
            return false;
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        public string[] IsDomainInterfaceInBU(string pdbId, int domainInterfaceId, string buType)
        {
            DataTable crystBuCompTable = GetCrystBuDomainInterfaceCompTable(pdbId, domainInterfaceId, buType);
            double qScore = 0.0;
            List<string> buList = new List<string> ();
            string buId = "";
            foreach (DataRow compRow in crystBuCompTable.Rows)
            {
                qScore = Convert.ToDouble(compRow["QScore"].ToString());
                if (qScore >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
                {
                    buId = compRow["BuID"].ToString();
                    if (!buList.Contains(buId))
                    {
                        buList.Add(buId);
                    }
                }
            }
            return buList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="buIDs"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private string IsDomainInterfaceInPqsPisaBU(string pdbId, int domainInterfaceId, string[] pdbBuIDs, string buType)
        {
            if (pdbBuIDs.Length > 0)
            {
                string sameBU = GetSameBUAsPdbBUs(pdbId, pdbBuIDs, buType);
                if (sameBU != "")
                {
                    return sameBU;
                }
            }
            string[] buIDs = IsDomainInterfaceInBU(pdbId, domainInterfaceId, buType);
            if (buIDs.Length > 0)
            {
                return buIDs[0];
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pdbBuIDs"></param>
        /// <returns></returns>
        private string GetSameBUAsPdbBUs(string pdbId, string[] pdbBuIDs, string buType)
        {
            string queryString = "";
            if (buType == "pisa")
            {
                queryString = string.Format("Select * From PdbPisaBuComp Where PdbID = '{0}' AND " +
                    " BuID1 In ({1});", pdbId, ParseHelper.FormatSqlListString(pdbBuIDs));
            }
            DataTable buCompTable = bucompQuery.Query(queryString);
            int interfaceNum1 = -1;
            int interfaceNum2 = -1;
            string isSame = "";
            foreach (DataRow buCompRow in buCompTable.Rows)
            {
                interfaceNum1 = Convert.ToInt32(buCompRow["InterfaceNum1"].ToString());
                interfaceNum2 = Convert.ToInt32(buCompRow["InterfaceNum2"].ToString());
                isSame = buCompRow["IsSame"].ToString();
                if (interfaceNum1 == interfaceNum2 && isSame == "1")
                {
                    return buCompRow["BuID2"].ToString().TrimEnd();
                }
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private DataTable GetCrystBuDomainInterfaceCompTable(string pdbId, int domainInterfaceId, string buType)
        {
            string queryString = string.Format("Select * From Cryst" + buType + "BuDomainInterfaceComp " +
                " Where PdbID = '{0}' AND DomainInterfaceID = {1} ORDER BY BuID;", pdbId, domainInterfaceId);
            DataTable crystBuCompTable = protcidQuery.Query(queryString);
            return crystBuCompTable;
        }
        #endregion

        #region initialize table
        /// <summary>
        /// 
        /// </summary>
        private void InitializeTable()
        {
            clusterStatTable = new DataTable(ProtCidSettings.dataType + "DomainClusterInterfaces");
            string[] statColumns = {"RelSeqID", "ClusterID", "RelCfGroupID", "SpaceGroup", "CrystForm", 
                                    "PdbID", "DomainInterfaceID", "InterfaceUnit", "ChainPfamArch", "SurfaceArea",
                                    "InPDB", "InPISA", "InAsu", "ASU", "PDBBU", "PdbBuID", "PISABU", "PisaBuID", "Name", "Species", "UnpCode"};
            foreach (string col in statColumns)
            {
                clusterStatTable.Columns.Add(new DataColumn(col));
            }

            clusterSumInfoTable = new DataTable(ProtCidSettings.dataType + "DomainClusterSumInfo");
            string[] sumInfoColumns = {"RelSeqID", "ClusterID", "SurfaceArea", "InPDB", "InPisa", "InAsu", 
                                      "NumOfCfgCluster", "NumOfEntryCluster", "NumOfCfgRelation", "NumOfEntryRelation", "MinSeqIdentity", 
                                      "NumOfHetero", "NumOfHomo", "NumOfIntra", "NumOfEntryHetero", "NumOfEntryHomo", "NumOfEntryIntra",
                                      "ClusterInterface", "MediumSurfaceArea"};
            foreach (string col in sumInfoColumns)
            {
                clusterSumInfoTable.Columns.Add(new DataColumn(col));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void InitializeDbTables()
        {
            DbCreator dbCreate = new DbCreator();
            string createTableString = "CREATE TABLE " + clusterStatTable.TableName + " ( " +
                " RelSeqID INTEGER NOT NULL, " +
                " ClusterID INTEGER NOT NULL, " +
                //    " CfGroupID VARCHAR(12) NOT NULL, " +
                " RelCfGroupId INTEGER NOT NULL, " +
                " SpaceGroup VARCHAR(40) NOT NULL, " +
                " CrystForm BLOB SUB_TYPE 1 SEGMENT SIZE 80 NOT NULL, " +
                " PDBID CHAR(4) NOT NULL, " +
                " DomainInterfaceID INTEGER NOT NULL, " +
                " SurfaceArea Float, " +
                " InterfaceUnit CHAR(3) NOT NULL, " +
                " ChainPfamArch VARCHAR(524), " +
                " InPDB CHAR(1), " +
                " InPisa CHAR(1), " +
                " InAsu CHAR(1), " +
                " ASU BLOB SUB_TYPE 1 SEGMENT SIZE 80 NOT NULL, " +
                " PDBBU BLOB SUB_TYPE 1 SEGMENT SIZE 80 NOT NULL, " +
                " PdbBuID VARCHAR(10), " +
                " PISABU BLOB SUB_TYPE 1 SEGMENT SIZE 80 NOT NULL, " +
                " PisaBuID INTEGER, " +
                " Name BLOB Sub_Type TEXT, " +
                " Species VARCHAR(524), " +
                " UnpCode VARCHAR(524) );";

            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, clusterStatTable.TableName);

            string createIndexString = string.Format("CREATE INDEX DomainClusterInterface_idx1 ON {0} (RelSeqID, ClusterID);", clusterStatTable.TableName);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, clusterStatTable.TableName);

            createIndexString = string.Format("CREATE INDEX DomainClusterInterface_idx2 ON {0}(PdbID);", clusterStatTable.TableName);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, clusterStatTable.TableName);

            createTableString = "CREATE TABLE " + clusterSumInfoTable.TableName + " ( " +
                " RelSeqID INTEGER NOT NULL, " +
                " ClusterID INTEGER NOT NULL, " +
                " SurfaceArea Float, " +  // to be added
                " InPDB INTEGER NOT NULL, " +
                " InPisa INTEGER NOT NULL, " +
                " InAsu INTEGER NOT NULL, " + // to be added
                " NumOfCfgCluster INTEGER NOT NULL, " +
                " NumOfEntryCluster INTEGER NOT NULL, " +
                " NumOfCfgRelation INTEGER NOT NULL, " +
                " NumOfEntryRelation INTEGER NOT NULL, " +
                " NumOfHomo INTEGER NOT NULL, " +
                " NumOfHetero INTEGER NOT NULL, " +
                " NumOfIntra INTEGER NOT NULL, " +
                " NumOfEntryHomo INTEGER NOT NULL, " +
                " NumOfEntryHetero INTEGER NOT NULL, " +
                " NumOfEntryIntra INTEGER NOT NULL, " +
                " MinSeqIdentity FLOAT, " +
                //     " InterfaceType CHAR(1), " +   // to be added
                " ClusterInterface VARCHAR(12), " + // to be added
                " MediumSurfaceArea FLOAT " + // to be added
            " );";
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, clusterSumInfoTable.TableName);

            createIndexString = string.Format("CREATE INDEX DomainClusterSumInfo_idx1 ON {0}(RelSeqID, ClusterID);", clusterSumInfoTable.TableName);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, clusterSumInfoTable.TableName);
        }
        #endregion

        #region aux functions
        private string FormatHeaderLine()
        {
            string headerLine = "";
            foreach (string headerCol in headerCols)
            {
                if (headerCol == "RelSeqID")
                {
                    headerLine += "Relation\t";
                }
                else if (headerCol == "RelCfGroupID")
                {
                    headerLine += "CFID\t";
                }
                else if (headerCol.IndexOf("NumOf") > -1)
                {
                    headerLine += (headerCol.Replace("NumOf", "#") + "\t");
                }
                else if (headerCol == "PDBBU" || headerCol == "PISABU" || headerCol == "PDBBUID" || headerCol == "PISABUID")
                {
                    headerLine += (headerCol.Replace("BU", "BA") + "\t");
                }
                else
                {
                    headerLine += (headerCol + "\t");
                }
            }
            return headerLine.TrimEnd('\t');
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string GetHeaderLine()
        {
            string headerLine = "";
            foreach (DataColumn dCol in clusterStatTable.Columns)
            {
                headerLine += (dCol.ColumnName + "\t");
            }
            headerLine += "SurfaceArea\t#CFG/Cluster\t#Entry/Cluster\t#CFG/Relation\t#Entry/Relation\tMinSeqID";
            return headerLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string GetTableHeaderLine(DataTable dataTable)
        {
            string headerLine = "";
            foreach (DataColumn dCol in dataTable.Columns)
            {
                headerLine += (dCol.ColumnName + "\t");
            }
            return headerLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string GetFamilyCodesForRelation(int relSeqId)
        {
            string queryString = string.Format("Select * From PfamDomainFamilyRelation Where RelSeqID = {0};",
                relSeqId);
            DataTable relationTable = protcidQuery.Query(queryString);
            string pfamId1 = relationTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
            string pfamId2 = relationTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            string pfamFamilyIdString = "";
            if (pfamId1 == pfamId2)
            {
                pfamFamilyIdString = "(" + pfamId1 + ")";
            }
            else
            {
                pfamFamilyIdString = "(" + pfamId1 + ");" + "(" + pfamId2 + ")";
            }
            return pfamFamilyIdString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAcc"></param>
        /// <returns></returns>
        private string GetPfamFamilyIDFromAcc(string pfamAcc)
        {
            string queryString = string.Format("Select Pfam_ID From PfamHmm Where Pfam_ACC = '{0}';", pfamAcc);
            DataTable pfamIdTable = pdbfamQuery.Query(queryString);
            return pfamIdTable.Rows[0]["Pfam_ID"].ToString().TrimEnd();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterStatTable"></param>
        /// <returns></returns>
        private void AddClusterSumInfo(int relSeqId, int clusterId, int numOfCfgRelation, int numOfEntryRelation)
        {
            string pdbId = "";
            //    string cfgString = "";
            int relCfGroupId = 0;
            List<string> clusterEntryList = new List<string>();
            List<int> clusterCfgList = new List<int>();
            List<string> inPdbList = new List<string>();
            List<string> inPisaList = new List<string>();
            List<string> inAsuList = new List<string>();
            int[] maxPdbBuNums = null;
            int[] maxPisaBuNums = null;
            double totalSurfaceArea = 0;
            int numOfSAs = 0;
            double surfaceArea = 0;
            int numOfHetero = 0;
            int numOfHomo = 0;
            int numOfIntra = 0;
            List<string> heteroEntryList = new List<string>();
            List<string> homoEntryList = new List<string>();
            List<string> intraEntryList = new List<string>();
            string interfaceUnit = "";

            foreach (DataRow statRow in clusterStatTable.Rows)
            {
                UpdateIntraChainDomainInterfaceInPisaBu(statRow);

                pdbId = statRow["PdbID"].ToString();

                if (!clusterEntryList.Contains(pdbId))
                {
                    clusterEntryList.Add(pdbId);
                }

                relCfGroupId = Convert.ToInt32(statRow["RelCfGroupId"].ToString());
                if (!clusterCfgList.Contains(relCfGroupId))
                {
                    clusterCfgList.Add(relCfGroupId);
                }

                if (Convert.ToInt32(statRow["InPDB"].ToString()) == 1)
                {
                    if (!inPdbList.Contains(pdbId))
                    {
                        inPdbList.Add(pdbId);
                    }
                }
                if (Convert.ToInt32(statRow["inPisa"].ToString()) == 1)
                {
                    if (!inPisaList.Contains(pdbId))
                    {
                        inPisaList.Add(pdbId);
                    }
                }
                if (Convert.ToInt32(statRow["InAsu"].ToString()) == 1)
                {
                    if (!inAsuList.Contains(pdbId))
                    {
                        inAsuList.Add(pdbId);
                    }
                }

                GetMaxCopyNumFromAsuBu(statRow["PdbBu"].ToString(), ref maxPdbBuNums);
                //            GetMaxCopyNumFromAsuBu(statRow["PqsBu"].ToString(), ref maxPqsBuNums);
                GetMaxCopyNumFromAsuBu(statRow["PisaBu"].ToString(), ref maxPisaBuNums);

                surfaceArea = Convert.ToDouble(statRow["SurfaceArea"].ToString());
                if (surfaceArea > 0)
                {
                    totalSurfaceArea += surfaceArea;
                    numOfSAs++;
                }

                interfaceUnit = statRow["InterfaceUnit"].ToString().TrimEnd();
                int interfaceType = GetInterfaceType(interfaceUnit);
                switch (interfaceType)
                {
                    case 0:
                        numOfHomo++;
                        if (!homoEntryList.Contains(pdbId))
                        {
                            homoEntryList.Add(pdbId);
                        }
                        break;

                    case 1:
                        numOfIntra++;
                        if (!intraEntryList.Contains(pdbId))
                        {
                            intraEntryList.Add(pdbId);
                        }
                        break;

                    case 2:
                        numOfHetero++;
                        if (!heteroEntryList.Contains(pdbId))
                        {
                            heteroEntryList.Add(pdbId);
                        }
                        break;

                    default:
                        break;
                }
            }
            int minSeqId = GetMinSeqIdInCluster();

            DataRow sumInfoRow = clusterSumInfoTable.NewRow();
            sumInfoRow["RelSeqID"] = relSeqId;
            sumInfoRow["ClusterID"] = clusterId;
            sumInfoRow["SurfaceArea"] = totalSurfaceArea / (double)numOfSAs;
            sumInfoRow["InPdb"] = inPdbList.Count; ;
            sumInfoRow["InPisa"] = inPisaList.Count;
            sumInfoRow["InAsu"] = inAsuList.Count;
            sumInfoRow["NumOfCfgCluster"] = clusterCfgList.Count;
            sumInfoRow["NumOfEntryCluster"] = clusterEntryList.Count;
            sumInfoRow["NumOfCfgRelation"] = numOfCfgRelation;
            sumInfoRow["NumOfEntryRelation"] = numOfEntryRelation;
            sumInfoRow["MinSeqIdentity"] = minSeqId;
            sumInfoRow["NumOfEntryHomo"] = homoEntryList.Count;
            sumInfoRow["NumOfEntryHetero"] = heteroEntryList.Count;
            sumInfoRow["NumOfEntryIntra"] = intraEntryList.Count;
            sumInfoRow["NumOfHomo"] = numOfHomo;
            sumInfoRow["NumOfHetero"] = numOfHetero;
            sumInfoRow["NumOfIntra"] = numOfIntra;
            double mediumSurfaceArea = 0;
            /*    DataRow[] clusterInterfaceRows = clusterStatTable.Select("", "SurfaceArea ASC");
                string clusterInterface = GetDomainClusterInterface(clusterInterfaceRows, out mediumSurfaceArea);*/
            string clusterInterface = GetDomainClusterInterface(relSeqId, clusterId, out mediumSurfaceArea);
            sumInfoRow["ClusterInterface"] = clusterInterface;
            sumInfoRow["MediumSurfaceArea"] = mediumSurfaceArea;
            clusterSumInfoTable.Rows.Add(sumInfoRow);
        }

        /// <summary>
        /// parsing the pisa assemblies and compared domain interfaces in pisa complexes is a headache
        /// so do a little trick
        /// </summary>
        /// <param name="clusterInterfaceRow"></param>
        private void UpdateIntraChainDomainInterfaceInPisaBu(DataRow clusterInterfaceRow)
        {
            string interfaceUnit = clusterInterfaceRow["InterfaceUnit"].ToString();
            if (interfaceUnit.IndexOf("-") > -1)  // intra-chain
            {
                string pdbBa = clusterInterfaceRow["PdbBU"].ToString();
                string pisaBa = clusterInterfaceRow["PisaBu"].ToString();
                if (pisaBa != "-")
                {
                    string asu = clusterInterfaceRow["Asu"].ToString();
                    string inPisa = clusterInterfaceRow["InPisa"].ToString();
                    string inPdb = clusterInterfaceRow["InPdb"].ToString();
                    if (inPisa == "0" && inPdb == "1")
                    {
                        if (pdbBa == pisaBa)
                        {
                            clusterInterfaceRow["InPISA"] = "1";
                        }
                        else if (asu.IndexOf("B") < 0) // only one sequence (entity)
                        {
                            clusterInterfaceRow["InPISA"] = "1";
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private int[] GetRelationNumbers(int relSeqId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamDomainInterfaces " +
                " Where RelSeqID = {0};", relSeqId);
            DataTable relationEntryTable = protcidQuery.Query(queryString);
            int[] relationNums = new int[2];
            relationNums[1] = relationEntryTable.Rows.Count; // the number of entries in the relation
            queryString = string.Format("Select Distinct RelCfGroupId From PfamDomainCfGroups Where RelSeqID = {0};", relSeqId);
            DataTable relCfGroupTable = protcidQuery.Query(queryString);
            relationNums[0] = relCfGroupTable.Rows.Count; // the number of CFGs in the relation
            return relationNums;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterRows"></param>
        /// <param name="clusterInterfaceArea"></param>
        /// <returns></returns>
        private string GetDomainClusterInterface(DataRow[] clusterRows, out double clusterInterfaceArea)
        {
            int mediumIndex = (int)(clusterRows.Length / 2);
            string clusterInterface = clusterRows[mediumIndex]["PdbID"].ToString() + "_d" +
                clusterRows[mediumIndex]["DomainInterfaceID"].ToString();
            clusterInterfaceArea = Convert.ToDouble(clusterRows[mediumIndex]["SurfaceArea"].ToString());
            return clusterInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="clusterInterfaceArea"></param>
        /// <returns></returns>
        private string GetDomainClusterInterface(int relSeqId, int clusterId, out double clusterInterfaceArea)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaceCluster " +
                " Where RelSeqID = {0} AND ClusterID = {1} Order By PdbID, DomainInterfaceID;", relSeqId, clusterId);
            DataTable clusterTable = protcidQuery.Query(queryString);
            List<DataRow> repDataRowList = new List<DataRow> ();
            List<string> entryList = new List<string>();
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (DataRow interfaceRow in clusterTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());
                if (entryList.Contains(pdbId))
                {
                    continue;
                }
                entryList.Add(pdbId);
                DataRow[] statRows = clusterStatTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
                if (statRows.Length > 0)
                {
                    //      repDataRowList.Add(statRows[0]);
                    AddDataRowToListInSAorder(repDataRowList, statRows[0]);
                }
            }
            DataRow[] repDataRows = new DataRow[repDataRowList.Count];
            repDataRowList.CopyTo(repDataRows);

            string clusterInterface = GetDomainClusterInterface(repDataRows, out clusterInterfaceArea);
            return clusterInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataRowList"></param>
        /// <param name="dataRowToBeAdded"></param>
        private void AddDataRowToListInSAorder(List<DataRow> dataRowList, DataRow dataRowToBeAdded)
        {
            double lsSurfaceArea = 0;
            double surfaceArea = Convert.ToDouble(dataRowToBeAdded["SurfaceArea"].ToString());
            int index = 0;
            foreach (DataRow dataRow in dataRowList)
            {
                lsSurfaceArea = Convert.ToDouble(dataRow["SurfaceArea"].ToString());
                if (surfaceArea < lsSurfaceArea)
                {
                    break;
                }
                index++;
            }
            dataRowList.Insert(index, dataRowToBeAdded);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceUnit"></param>
        /// <returns></returns>
        private int GetInterfaceType(string interfaceUnit)
        {
            if (interfaceUnit.IndexOf('-') > -1)
            {
                return 1;  // intra-chain
            }
            else
            {
                if (interfaceUnit == "A2")
                {
                    return 0;  // homodimer
                }
                else
                {
                    return 2;   // heterodimer
                }
            }
        }
        #endregion

        #region Minimum Seq ID
        /// <summary>
        /// get minimum sequence identity from pfamdomaininterfacecomp table
        /// </summary>
        /// <returns></returns>
        private int GetMinSeqIdInCluster()
        {            
            List<string> clusterDomainInterfaceList = new List<string> ();
            int minSeqIdentity = 100;
            int identity = 0;
            List<string> addedPdbList = new List<string> ();
            string pdbId = "";
            foreach (DataRow clusterInterfaceRow in clusterStatTable.Rows)
            {
                pdbId = clusterInterfaceRow["PdbID"].ToString();
                if (addedPdbList.Contains (pdbId))
                {
                    continue;
                }
                addedPdbList.Add(pdbId);
                clusterDomainInterfaceList.Add (pdbId + clusterInterfaceRow["DomainInterfaceID"].ToString());
            }
            for (int i = 0; i < clusterDomainInterfaceList.Count; i ++ )
            {
                for (int j = i + 1; j < clusterDomainInterfaceList.Count; j ++)
                {
                   identity =  GetDomainInterfaceIdentity(clusterDomainInterfaceList[i], clusterDomainInterfaceList[j]);
                    if (minSeqIdentity > identity && identity > 0)
                    {
                        minSeqIdentity = identity;
                    }
                }
            }
            return minSeqIdentity;  
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface1"></param>
        /// <param name="domainInterface2"></param>
        /// <returns></returns>
        private int GetDomainInterfaceIdentity (string domainInterface1, string domainInterface2)
        {
            string pdbId1 = domainInterface1.Substring(0, 4);
            int domainInterfaceId1 = Convert.ToInt32(domainInterface1.Substring (4, domainInterface1.Length - 4));
            string pdbId2 = domainInterface2.Substring(0, 4);
            int domainInterfaceId2 = Convert.ToInt32(domainInterface2.Substring (4, domainInterface2.Length - 4));

            string queryString = string.Format("Select Identity From PfamDomainInterfaceComp Where (PdbID1 = '{0}' AND DomainInterfaceID1 = {1} AND " +
                " PdbID2 = '{2}' AND DomainInterfaceID2 = {3}) OR (PdbID1 = '{2}' AND DomainInterfaceID1 = {3} AND " +
                " PdbID2 = '{0}' AND DomainInterfaceID2 = {1});", pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
            DataTable identityTable = ProtCidSettings.protcidQuery.Query(queryString);
            double identity = -1.0;
            if (identityTable.Rows.Count > 0)
            {
                identity = Convert.ToDouble(identityTable.Rows[0]["Identity"].ToString ());
            }
            return (int)identity;
        }

        #region minseqidentity from domain list in the cluster 
        //before adding identity to pfamdomaininterfacecomp table which is done on Janurary 16, 2018
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int GetMinDomainSeqIdInCluster()
        {
            List<long> domainIdList1 = new List<long>();
            List<long> domainIdList2 = new List<long>();
            string pdbId = "";
            int domainInterfaceId = -1;
            foreach (DataRow clusterInterfaceRow in clusterStatTable.Rows)
            {
                pdbId = clusterInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(clusterInterfaceRow["DomainInterfaceID"].ToString());
                long[] domainIds = GetDomainIDsForInterface(pdbId, domainInterfaceId);
                if (!domainIdList1.Contains(domainIds[0]))
                {
                    domainIdList1.Add(domainIds[0]);
                }
                if (!domainIdList2.Contains(domainIds[1]))
                {
                    domainIdList2.Add(domainIds[1]);
                }
            }

            int minSeqId1 = GetMinSeqIdInDomains(domainIdList1.ToArray ());

            int minSeqId2 = GetMinSeqIdInDomains(domainIdList2.ToArray ());

            if (minSeqId1 < minSeqId2)
            {
                return minSeqId1;
            }
            else
            {
                return minSeqId2;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private long[] GetDomainIDsForInterface(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces " +
                " Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable domainInterfaceDefTable = protcidQuery.Query(queryString);

            if (domainInterfaceDefTable.Rows.Count > 0)
            {
                long[] domainIds = new long[2];
                string isReversed = domainInterfaceDefTable.Rows[0]["IsReversed"].ToString();
                if (isReversed == "0")
                {
                    domainIds[0] = Convert.ToInt64(domainInterfaceDefTable.Rows[0]["DomainID1"].ToString());
                    domainIds[1] = Convert.ToInt64(domainInterfaceDefTable.Rows[0]["DomainID2"].ToString());
                }
                else
                {
                    domainIds[1] = Convert.ToInt64(domainInterfaceDefTable.Rows[0]["DomainID1"].ToString());
                    domainIds[0] = Convert.ToInt64(domainInterfaceDefTable.Rows[0]["DomainID2"].ToString());
                }
                return domainIds;
            }
            return null;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainIdsInFamily"></param>
        /// <returns></returns>
        private int GetMinSeqIdInDomains(long[] domainIdsInFamily)
        {
            int minDomainSeqId = 100;
            int domainSeqId = -1;
            for (int i = 0; i < domainIdsInFamily.Length; i++)
            {
                for (int j = i + 1; j < domainIdsInFamily.Length; j++)
                {
                    domainSeqId = GetSeqIdBetweenDomains(domainIdsInFamily[i], domainIdsInFamily[j]);
                    if (domainSeqId > -1)
                    {
                        if (minDomainSeqId > domainSeqId)
                        {
                            minDomainSeqId = domainSeqId;
                        }
                    }
                }
            }
            return minDomainSeqId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainId1"></param>
        /// <param name="domainId2"></param>
        /// <returns></returns>
        private int GetSeqIdBetweenDomains(long domainId1, long domainId2)
        {
            string queryString = string.Format("Select Identity From PfamDomainAlignments " +
                " Where (QueryDomainID = {0} AND HitDomainID = {1}) OR " +
                " (QueryDomainID = {1} AND HitDomainID = {0});", domainId1, domainId2);
            DataTable identityTable = alignQuery.Query(queryString);
            if (identityTable.Rows.Count > 0)
            {
                return (int)(Convert.ToDouble(identityTable.Rows[0]["Identity"].ToString()));
            }
            return -1;
        }
        #endregion
        #endregion

        #region for results output file
        /// <summary>
        /// divide the result file into several small files in order to display in the Excel.
        /// </summary>
        public void DivideDomainClusterResultOutputFile()
        {
            string resultDir = Path.Combine(ProtCidSettings.applicationStartPath, "HomoSeq\\result_" + DateTime.Today.ToString("yyyyMMdd"));
            //   string resultDir = @"C:\ProtBuDProject\xtal\XtalInterfaceProject\Debug250less400\HomoSeq\result_20100119";
            if (!Directory.Exists(resultDir))
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("No such result directory exist: " + resultDir);
                return;
            }
            int fileNum = 0;
            int familyLineCount = 0;
            int fileLineCount = 0;
            int fileLineCountTotal = 65000;
            StreamReader dataReader = new StreamReader(Path.Combine(resultDir, "PfamDomainInterfaceClusterInfo.txt"));
            StreamWriter dataWriter = new StreamWriter(Path.Combine(resultDir, "PfamDomainInterfaceClusterInfo" + fileNum.ToString() + ".txt"));
            string line = "";
            string relationString = "";
            string preRelationString = "";
            string familyLines = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    familyLines += (line + "\r\n");
                    familyLineCount++;
                    continue;
                }
                string[] fields = line.Split('\t');
                relationString = fields[0];

                if (relationString == "RelSeqID" && preRelationString == "") // for first header line
                {
                    familyLines += (line + "\r\n");
                    familyLineCount++;
                    continue;
                }

                if (relationString == "RelSeqID") // for new family
                {
                    if (fileLineCount + familyLineCount <= fileLineCountTotal)
                    {
                        dataWriter.Write(familyLines);
                        fileLineCount += familyLineCount;
                    }
                    else
                    {
                        dataWriter.Close();
                        fileNum++;
                        fileLineCount = familyLineCount;
                        dataWriter = new StreamWriter(Path.Combine(resultDir, "PfamDomainInterfaceClusterInfo" + fileNum.ToString() + ".txt"));
                        dataWriter.Write(familyLines);
                    }
                    familyLines = "";
                    familyLineCount = 0;
                }

                familyLines += (line + "\r\n");
                familyLineCount++;
                preRelationString = relationString;
            }
            dataReader.Close();
            dataWriter.Close();
        }
        #endregion

        #region domain cluster info for the statistics page in protcid
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type">pfam domain</param>
        public void PrintDomainDbSumInfo(string type)
        {
            if (interfaceStatData == null)
            {
                interfaceStatData = new InterfaceStatData(type);
            }
            interfaceStatData.InitializeStatInfoTable(type);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Domain cluster summary info");

            StreamWriter dataWriter = new StreamWriter("DomainInterfaceDbStatInfo.txt");

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Domain Group Sum Info.");
            GetRelationSumInfo(dataWriter);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#CFs >= 2");
            GetClusterSumInfo(2, 101, dataWriter);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#CFs >= 2, SeqID <= 90");
            GetClusterSumInfo(2, 90, dataWriter);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#CFs >= 5, SeqID <= 90");
            GetClusterSumInfo(5, 90, dataWriter);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#CFs >= 10, SeqID <= 90");
            GetClusterSumInfo(10, 90, dataWriter);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("#CFs >= 20, SeqID <= 90");
            GetClusterSumInfo(20, 90, dataWriter);

            dataWriter.Close();
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, interfaceStatData.dbStatInfoTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataWriter"></param>
        private void GetRelationSumInfo(StreamWriter dataWriter)
        {
            string queryString = "Select Distinct RelSeqID, FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation;";
            DataTable pfamRelationTable = protcidQuery.Query(queryString);

            int numOfSamePfamGroups = 0;
            int numOfDiffPfamGroups = 0;
            List<string> samePfamEntryList = new List<string> ();
            List<string> diffPfamEntryList = new List<string> ();

            int relSeqId = 0;
            string pfamId1 = "";
            string pfamId2 = "";
            foreach (DataRow relationRow in pfamRelationTable.Rows)
            {
                relSeqId = Convert.ToInt32(relationRow["RelSeqID"].ToString());
                pfamId1 = relationRow["FamilyCode1"].ToString().TrimEnd();
                pfamId2 = relationRow["FamilyCode2"].ToString().TrimEnd();
                string[] groupEntries = GetRelationEntries(relSeqId);

                if (pfamId1 == pfamId2)
                {
                    numOfSamePfamGroups++;
                    foreach (string entry in groupEntries)
                    {
                        if (!samePfamEntryList.Contains(entry))
                        {
                            samePfamEntryList.Add(entry);
                        }
                    }
                }
                else
                {
                    numOfDiffPfamGroups++;
                    foreach (string entry in groupEntries)
                    {
                        if (!diffPfamEntryList.Contains(entry))
                        {
                            diffPfamEntryList.Add(entry);
                        }
                    }
                }
            }
            dataWriter.WriteLine("# single pfam arch groups: " + numOfSamePfamGroups.ToString());
            dataWriter.WriteLine("# double pfam arch groups: " + numOfDiffPfamGroups.ToString());
            int totalNumGroups = numOfSamePfamGroups + numOfDiffPfamGroups;
            dataWriter.WriteLine("# both pfam arch groups: " + totalNumGroups.ToString());

            dataWriter.WriteLine("# entries single pfam arch: " + samePfamEntryList.Count.ToString());
            dataWriter.WriteLine("# entries double pfam arch: " + diffPfamEntryList.Count.ToString());
            int totalNumEntries = GetDistinctEntryList(samePfamEntryList, diffPfamEntryList);
            dataWriter.WriteLine("# total entries both pfam arch: " + totalNumEntries.ToString());

            dataWriter.Flush();

            DataRow statInfoRow = interfaceStatData.dbStatInfoTable.NewRow();
            statInfoRow["Category"] = "#Relations";
            statInfoRow["Single"] = numOfSamePfamGroups;
            statInfoRow["Pair"] = numOfDiffPfamGroups;
            statInfoRow["Total"] = totalNumGroups;
            interfaceStatData.dbStatInfoTable.Rows.Add(statInfoRow);

            DataRow entryInfoRow = interfaceStatData.dbStatInfoTable.NewRow();
            entryInfoRow["Category"] = "#Entries";
            entryInfoRow["Single"] = samePfamEntryList.Count;
            entryInfoRow["Pair"] = diffPfamEntryList.Count;
            entryInfoRow["Total"] = totalNumEntries;
            interfaceStatData.dbStatInfoTable.Rows.Add(entryInfoRow);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Mcutoff"></param>
        /// <param name="seqIdCutoff"></param>
        /// <param name="dataWriter"></param>
        private void GetClusterSumInfo(int Mcutoff, int seqIdCutoff, StreamWriter dataWriter)
        {
            string queryString = "Select Distinct RelSeqID, FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation;";
            DataTable domainRelationTable = protcidQuery.Query(queryString);

            int numOfSamePfamGroups = 0;
            int numOfDiffPfamGroups = 0;
            int numOfSamePfamClusters = 0;
            int numOfDiffPfamClusters = 0;
            List<string> samePfamEntryList = new List<string>();
            List<string> diffPfamEntryList = new List<string>();
            List<string> samePfamPdbBuEntryList = new List<string>();
            List<string> diffPfamPdbBuEntryList = new List<string>();
            List<string> samePfamPisaBuEntryList = new List<string>();
            List<string> diffPfamPisaBuEntryList = new List<string>();

            int relSeqId = 0;
            string pfamId1 = "";
            string pfamId2 = "";
            bool isSame = false;
            foreach (DataRow relationRow in domainRelationTable.Rows)
            {
                relSeqId = Convert.ToInt32(relationRow["RelSeqID"].ToString());
                pfamId1 = relationRow["FamilyCode1"].ToString().TrimEnd();
                pfamId2 = relationRow["FamilyCode2"].ToString().TrimEnd();

                isSame = false;
                if (pfamId1 == pfamId2)
                {
                    isSame = true;
                }

                int[] clustersMGreater = GetClustersMGreater(relSeqId, Mcutoff, seqIdCutoff);
                string[] entriesInClusters = GetDistinctEntryInClusters(relSeqId, clustersMGreater);
                string[] pdbBuEntriesInClusters = GetDistinctBuEntryWithClusterInterfaces(relSeqId, clustersMGreater, "pdb");
                string[] pisaBuEntriesInClusters = GetDistinctBuEntryWithClusterInterfaces(relSeqId, clustersMGreater, "pisa");

                if (isSame)
                {
                    foreach (string entryInCluster in entriesInClusters)
                    {
                        if (!samePfamEntryList.Contains(entryInCluster))
                        {
                            samePfamEntryList.Add(entryInCluster);
                        }
                    }
                    foreach (string entry in pdbBuEntriesInClusters)
                    {
                        if (!samePfamPdbBuEntryList.Contains(entry))
                        {
                            samePfamPdbBuEntryList.Add(entry);
                        }
                    }
                    foreach (string entry in pisaBuEntriesInClusters)
                    {
                        if (!samePfamPisaBuEntryList.Contains(entry))
                        {
                            samePfamPisaBuEntryList.Add(entry);
                        }
                    }

                    numOfSamePfamClusters += clustersMGreater.Length;
                    if (clustersMGreater.Length > 0)
                    {
                        numOfSamePfamGroups++;
                    }
                }
                else
                {
                    foreach (string entryInCluster in entriesInClusters)
                    {
                        if (!diffPfamEntryList.Contains(entryInCluster))
                        {
                            diffPfamEntryList.Add(entryInCluster);
                        }
                    }
                    foreach (string entry in pdbBuEntriesInClusters)
                    {
                        if (!diffPfamPdbBuEntryList.Contains(entry))
                        {
                            diffPfamPdbBuEntryList.Add(entry);
                        }
                    }
                    foreach (string entry in pisaBuEntriesInClusters)
                    {
                        if (!diffPfamPisaBuEntryList.Contains(entry))
                        {
                            diffPfamPisaBuEntryList.Add(entry);
                        }
                    }
                    numOfDiffPfamClusters += clustersMGreater.Length;
                    if (clustersMGreater.Length > 0)
                    {
                        numOfDiffPfamGroups++;
                    }
                }
            }

            dataWriter.WriteLine("M = " + Mcutoff.ToString() + "    SeqIdentity = " + seqIdCutoff.ToString());
            dataWriter.WriteLine("# same pfam arch relations M>=" + Mcutoff.ToString() + ": " + numOfSamePfamGroups.ToString());
            dataWriter.WriteLine("# diff pfam arch relations M>=" + Mcutoff.ToString() + ": " + numOfDiffPfamGroups.ToString());
            int totalNumGroups = numOfSamePfamGroups + numOfDiffPfamGroups;
            dataWriter.WriteLine("# both pfam arch relations M>=" + Mcutoff.ToString() + ": " + totalNumGroups.ToString());

            dataWriter.WriteLine("# same pfam arch clusters M>=" + Mcutoff.ToString() + ": " + numOfSamePfamClusters.ToString());
            dataWriter.WriteLine("# diff pfam arch clusters M>=" + Mcutoff.ToString() + ": " + numOfDiffPfamClusters.ToString());
            int tatalNumClusters = numOfSamePfamClusters + numOfDiffPfamClusters;
            dataWriter.WriteLine("# both clusters M>=" + Mcutoff.ToString() + ": " + tatalNumClusters.ToString());

            dataWriter.WriteLine("# distinct entries with same pfam arch M>=" + Mcutoff.ToString() + ": " + samePfamEntryList.Count.ToString());
            dataWriter.WriteLine("# distinct entries with diff pfam arch M>=" + Mcutoff.ToString() + ": " + diffPfamEntryList.Count.ToString());
            int totalNumEntries = GetDistinctEntryList(samePfamEntryList, diffPfamEntryList);
            dataWriter.WriteLine("# distinct entries with both M>=" + Mcutoff.ToString() + ": " + totalNumEntries.ToString());

            dataWriter.WriteLine("# distinct PDB BU entries with same pfam arch M>=" + Mcutoff.ToString() + ": " + samePfamPdbBuEntryList.Count.ToString());
            dataWriter.WriteLine("# distinct PDB BU entries with diff pfam arch M>=" + Mcutoff.ToString() + ": " + diffPfamPdbBuEntryList.Count.ToString());
            int totalPdbBuEntries = GetDistinctEntryList(samePfamPdbBuEntryList, diffPfamPdbBuEntryList);
            dataWriter.WriteLine("# distinct PDB BU entries with both M>=" + Mcutoff.ToString() + ": " + totalPdbBuEntries.ToString());

            dataWriter.WriteLine("# distinct PISA BU entries with same pfam arch M>=" + Mcutoff.ToString() + ": " + samePfamPisaBuEntryList.Count.ToString());
            dataWriter.WriteLine("# distinct PISA BU entries with diff pfam arch M>=" + Mcutoff.ToString() + ": " + diffPfamPisaBuEntryList.Count.ToString());
            int totalPisaBuEntries = GetDistinctEntryList(samePfamPisaBuEntryList, diffPfamPisaBuEntryList);
            dataWriter.WriteLine("# distinct PISA BU entries with both M>=" + Mcutoff.ToString() + ": " + totalPisaBuEntries.ToString());

            dataWriter.Flush();

            string mSeqString = " with M>=" + Mcutoff.ToString();
            if (seqIdCutoff < 99)
            {
                mSeqString += (",seqid<" + seqIdCutoff.ToString());
            }
            DataRow groupStatInfoRow = interfaceStatData.dbStatInfoTable.NewRow();
            groupStatInfoRow["Category"] = "#Relations" + mSeqString;
            groupStatInfoRow["Single"] = numOfSamePfamGroups;
            groupStatInfoRow["Pair"] = numOfDiffPfamGroups;
            groupStatInfoRow["Total"] = totalNumGroups;
            interfaceStatData.dbStatInfoTable.Rows.Add(groupStatInfoRow);

            DataRow clusterStatInfoRow = interfaceStatData.dbStatInfoTable.NewRow();
            clusterStatInfoRow["Category"] = "#Clusters" + mSeqString;
            clusterStatInfoRow["Single"] = numOfSamePfamClusters;
            clusterStatInfoRow["Pair"] = numOfDiffPfamClusters;
            clusterStatInfoRow["Total"] = tatalNumClusters;
            interfaceStatData.dbStatInfoTable.Rows.Add(clusterStatInfoRow);

            DataRow entryStatInfoRow = interfaceStatData.dbStatInfoTable.NewRow();
            entryStatInfoRow["Category"] = "#Entries" + mSeqString;
            entryStatInfoRow["Single"] = samePfamEntryList.Count;
            entryStatInfoRow["Pair"] = diffPfamEntryList.Count;
            entryStatInfoRow["Total"] = totalNumEntries;
            interfaceStatData.dbStatInfoTable.Rows.Add(entryStatInfoRow);

            DataRow pdbBuStatInfoRow = interfaceStatData.dbStatInfoTable.NewRow();
            pdbBuStatInfoRow["Category"] = "#PDBBU" + mSeqString;
            pdbBuStatInfoRow["Single"] = samePfamPdbBuEntryList.Count;
            pdbBuStatInfoRow["Pair"] = diffPfamPdbBuEntryList.Count;
            pdbBuStatInfoRow["Total"] = totalPdbBuEntries;
            interfaceStatData.dbStatInfoTable.Rows.Add(pdbBuStatInfoRow);

            DataRow pisaBuStatInfoRow = interfaceStatData.dbStatInfoTable.NewRow();
            pisaBuStatInfoRow["Category"] = "#PISABU" + mSeqString;
            pisaBuStatInfoRow["Single"] = samePfamPisaBuEntryList.Count;
            pisaBuStatInfoRow["Pair"] = diffPfamPisaBuEntryList.Count;
            pisaBuStatInfoRow["Total"] = totalPisaBuEntries;
            interfaceStatData.dbStatInfoTable.Rows.Add(pisaBuStatInfoRow);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="Mcutoff"></param>
        /// <param name="clusterSumInfoTable"></param>
        /// <returns></returns>
        private bool IsGroupMGreater(DataTable clusterSumInfoTable, int Mcutoff)
        {
            int Mcluster = 0;
            foreach (DataRow clusterSumInfoRow in clusterSumInfoTable.Rows)
            {
                Mcluster = Convert.ToInt32(clusterSumInfoRow["NumOfCfgCluster"].ToString());
                if (Mcluster >= Mcutoff)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupClusterSumInfoTable"></param>
        /// <param name="Mcutoff"></param>
        /// <returns></returns>
        private int[] GetClustersMGreater(int relSeqId, int Mcutoff, int seqIdCutoff)
        {
            int Mcluster = 0;
            int seqId = 0;
            List<int> clusterList = new List<int> ();

            string queryString = string.Format("Select * From PfamDomainClusterSumInfo Where RelSeqID = {0};", relSeqId);
            DataTable clusterSumInfoTable = protcidQuery.Query(queryString);

            foreach (DataRow clusterSumInfoRow in clusterSumInfoTable.Rows)
            {
                Mcluster = Convert.ToInt32(clusterSumInfoRow["NumOfCfgCluster"].ToString());
                seqId = (int)(Convert.ToDouble(clusterSumInfoRow["MinSeqIdentity"].ToString()));
                if (Mcluster >= Mcutoff && seqId <= seqIdCutoff)
                {
                    clusterList.Add(Convert.ToInt32(clusterSumInfoRow["ClusterID"].ToString()));
                }
            }
            return clusterList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterIds"></param>
        /// <returns></returns>
        private string[] GetDistinctEntryInClusters(int superGroupId, int[] clusterIds)
        {
            if (clusterIds.Length == 0)
            {
                return new string[0];
            }
            string queryString = string.Format("Select Distinct PdbID From PfamDomainClusterInterfaces " +
                " Where RelSeqID = {0} AND ClusterID in ({1});",
                superGroupId, ParseHelper.FormatSqlListString(clusterIds));
            DataTable entriesInClustersTable = protcidQuery.Query(queryString);
            string[] entriesInClusters = new string[entriesInClustersTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entriesInClustersTable.Rows)
            {
                entriesInClusters[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return entriesInClusters;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterIds"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private string[] GetDistinctBuEntryWithClusterInterfaces(int relSeqId, int[] clusterIds, string buType)
        {
            if (clusterIds.Length == 0)
            {
                return new string[0];
            }
            string queryString = "";
            if (buType == "pdb")
            {
                queryString = string.Format("Select Distinct PdbID From PfamDomainClusterInterfaces " +
                    " Where RelSeqID = {0} AND ClusterID IN ({1}) AND INPDB = 1;",
                    relSeqId, ParseHelper.FormatSqlListString(clusterIds));
            }
            else
            {
                queryString = string.Format("Select Distinct PdbID From PfamDomainClusterInterfaces " +
                    " Where RelSeqID = {0} AND ClusterID IN ({1}) AND INPISA = 1;",
                    relSeqId, ParseHelper.FormatSqlListString(clusterIds));
            }
            DataTable inBuEntryTable = protcidQuery.Query(queryString);
            string[] buEntries = new string[inBuEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow buEntryRow in inBuEntryTable.Rows)
            {
                buEntries[count] = buEntryRow["PdbID"].ToString();
                count++;
            }
            return buEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetRelationEntries(int relSeqId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamDomainInterfaces WHere RelSeqId = {0};", relSeqId);
            DataTable entryTable = protcidQuery.Query(queryString);
            string[] entries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return entries;
        }
        #endregion

        #region print domain cluster stat info from db table        
        /// <summary>
        /// print relation cluster interface data
        /// </summary>
        public void PrintDomainClusteStatInfo()
        {
            InitializeTable();

            resultDir = Path.Combine(ProtCidSettings.applicationStartPath, "HomoSeq\\result_domain_" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(resultDir))
            {
                Directory.CreateDirectory(resultDir);
            }

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            string queryString = "Select Distinct RelSeqID From PfamDomainClusterSumInfo;";
            DataTable relSeqIdTable = protcidQuery.Query(queryString);

            ProtCidSettings.progressInfo.totalOperationNum = relSeqIdTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIdTable.Rows.Count;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Output Pfam domain interface cluster info to files");

            int relSeqId = 0;
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());

                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                PrintRelationClusterStatInfo(relSeqId);
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// print relation cluster interface data
        /// </summary>
        public void PrintDomainClusteStatInfo(int[] relSeqIds)
        {
            InitializeTable();

            resultDir = Path.Combine(ProtCidSettings.applicationStartPath, "HomoSeq\\result_domain_" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(resultDir))
            {
                Directory.CreateDirectory(resultDir);
            }

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            ProtCidSettings.progressInfo.totalOperationNum = relSeqIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIds.Length;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Output Pfam domain interface cluster info to files");

            foreach (int relSeqId in relSeqIds)
            {

                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                PrintRelationClusterStatInfo(relSeqId);
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        private void PrintRelationClusterStatInfo(int relSeqId)
        {
            string groupName = DownloadableFileName.GetDomainRelationName(relSeqId);
            string relationStatInfoFile = Path.Combine(resultDir, groupName + ".txt");
            StreamWriter dataWriter = new StreamWriter(relationStatInfoFile);

            string headerLine = "";
            string queryString = string.Format("Select * From PfamDomainClusterSumInfo Where RelSeqId = {0} Order by ClusterID;", relSeqId);
            DataTable sumInfoTable = protcidQuery.Query(queryString);
            queryString = string.Format("Select * From PfamDomainClusterInterfaces Where RelSeqId = {0};", relSeqId);
            DataTable clusterInterfaceTable = protcidQuery.Query(queryString);

            headerLine = FormatHeaderLine();
            dataWriter.WriteLine(headerLine);

            string relationString = "";
            int clusterId = 0;
            string clusterSumInfoLine = "";
            string clusterInterfaceInfoLine = "";

            foreach (DataRow sumInfoRow in sumInfoTable.Rows)
            {
                relSeqId = Convert.ToInt32(sumInfoRow["RelSeqID"].ToString());
                clusterId = Convert.ToInt32(sumInfoRow["ClusterID"].ToString());
                relationString = GetFamilyCodesForRelation(relSeqId);

                DataRow[] clusterInterfaceRows = clusterInterfaceTable.Select(string.Format("RelSeqID = '{0}' AND ClusterID = '{1}'", relSeqId, clusterId), "RelCfGroupID ASC, PdbID ASC, DomainInterfaceID ASC");
                clusterSumInfoLine = FormatSumInfoLineForClusterInterfaces(relSeqId, clusterId, relationString, sumInfoRow);
                dataWriter.WriteLine(clusterSumInfoLine);

                foreach (DataRow clusterInterfaceRow in clusterInterfaceRows)
                {
                    clusterInterfaceInfoLine = "";
                    foreach (string headerCol in headerCols)
                    {
                        if (clusterInterfaceTable.Columns.Contains(headerCol))
                        {
                            clusterInterfaceInfoLine += (clusterInterfaceRow[headerCol] + "\t");
                        }
                        else
                        {
                            clusterInterfaceInfoLine += "\t";
                        }
                    }
                    dataWriter.WriteLine(clusterInterfaceInfoLine.TrimEnd('\t'));
                }
                dataWriter.WriteLine();
            }
            dataWriter.Close();
            ParseHelper.ZipPdbFile(relationStatInfoFile);
        }    
        #endregion

        #region debug
        #region add missing domain interfaces from redundant entries  
        double simInterfaceCutoff = 0.75;
        public void RemoveDunplicateInterfaces ()
        {
            dbInsert = new DbInsert(ProtCidSettings.protcidDbConnection);
            string queryString = "Select * From (Select pdbid, domaininterfaceid, count(*) as rownum From pfamdomainclusterinterfaces Group By pdbid, domaininterfaceid) Where rownum >= 2;";
            DataTable dupInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            string deleteString = "";
            foreach (DataRow interfaceRow in dupInterfaceTable.Rows)
            {
                queryString = string.Format("Select * From PfamDomainClusterInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", interfaceRow["PdbID"], interfaceRow["DomainInterfaceID"]);
                DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
                clusterInterfaceTable.TableName = "PfamDomainClusterInterfaces";
                deleteString = string.Format("Delete From PfamDomainClusterInterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};", interfaceRow["PdbID"], interfaceRow["DomainInterfaceID"]);
                protcidUpdate.Delete(deleteString);
                dbInsert.InsertDataIntoDb(clusterInterfaceTable.Rows[0]);
            }
        }
        public void AddMissingRedundantInterfacesToClusterTable ()
        {          
            InitializeTable();

            string queryString = "Select RelSeqID, ClusterID From PfamDomainClusterSumInfo Where NumOfCfgCluster >= 3 Order By RelSeqID, ClusterID;";
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            int relSeqId = 0;
            int clusterId = 0;
            int preRelSeqId = 0;
            List<string> relClusterInterfaceList = new List<string>();
            List<string> updateClusterList = new List<string>();
            string[] addedInterfaces = null;
            StreamWriter logWriter = new StreamWriter("AddedInterfacesClustersInfo.txt", true);
            List<string> addedInterfaceList = new List<string>();
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                relSeqId = Convert.ToInt32(clusterRow["RelSeqID"].ToString());
                if (relSeqId != 14511)
                {
                    continue;
                }
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString ());

                if (preRelSeqId != relSeqId)
                {
                    relClusterInterfaceList = GetRelClusterInterfaceList(relSeqId);
                }
                string[] missingInterfaces = GetMissingHomoInterfaces(relSeqId, clusterId, relClusterInterfaceList, addedInterfaceList);
                if (missingInterfaces.Length > 0)
                {
                    addedInterfaces = AddDomainInterfacesToTable(relSeqId, clusterId, missingInterfaces);
                    if (addedInterfaces.Length > 0)
                    {
                        updateClusterList.Add(relSeqId + "_" + clusterId);
                        UpdateClusterSumInfo(relSeqId, clusterId);
                        logWriter.WriteLine(relSeqId + " " + clusterId + " " + ParseHelper.FormatStringFieldsToString(addedInterfaces));
                        logWriter.Flush();
                        addedInterfaceList.AddRange(addedInterfaces);
                    }
                }
                preRelSeqId = relSeqId;
            }
            logWriter.Close();
        }

        public void InsertLogData (string logFile)
        {
            StreamWriter insertLogWriter = new StreamWriter ("InsertErrorLog_1.txt");
            StreamReader insertLogReader = new StreamReader ("InsertErrorLog.txt");
            List<string> errorInsertLineList = new List<string>();
            string line = "";
            while ((line = insertLogReader.ReadLine ()) != null)
            {
                errorInsertLineList.Add(line.Replace("INSERT INTO PfamDomainClusterInterfaces ", "INSERT INTO"));
            }
            insertLogReader.Close();

            dbInsert = new DbInsert(ProtCidSettings.protcidDbConnection);
            StreamReader dataReader = new StreamReader(logFile);
            string insertLine = "";
            
            while ((line = dataReader.ReadLine ()) != null)
            {
                if (line.IndexOf("INSERT INTO") > -1 && errorInsertLineList.Contains (line))
                {
                    insertLine = line.Replace("INSERT INTO", "INSERT INTO PfamDomainClusterInterfaces ");
                    while ((line = dataReader.ReadLine()) != null)
                    {
                        if (line.IndexOf(");") > -1)
                        {
                            insertLine += line;
                            break;
                        }
                        insertLine += line;
                    }
                    try
                    {
                        dbInsert.InsertDataIntoDb(insertLine);
                    }
                    catch
                    {
                        insertLogWriter.WriteLine(insertLine);
                    }
                }
            }
            dataReader.Close();
            insertLogWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private List<string> GetRelClusterInterfaceList (int relSeqId)
        {
            string queryString = string.Format("Select PdbID, DomainInterfaceID From PfamDomainClusterInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable relClusterInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> relClusterInterfaceList = new List<string>();
            foreach (DataRow interfaceRow in relClusterInterfaceTable.Rows)
            {
                relClusterInterfaceList.Add(interfaceRow["PdbID"].ToString() + interfaceRow["DomainInterfaceID"].ToString());
            }
            relClusterInterfaceList.Sort();
            return relClusterInterfaceList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string[] GetMissingHomoInterfaces (int relSeqId, int clusterId)
        {
            List<string> relClusterInterfaceList = GetRelClusterInterfaceList(relSeqId);
            List<string> addedInterfaceList = new List<string>();

            string[] missingClusterInterfaces = GetMissingHomoInterfaces(relSeqId, clusterId, relClusterInterfaceList, addedInterfaceList);
           
            return missingClusterInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="relClusterInterfaceList"></param>
        /// <returns></returns>
        private string[] GetMissingHomoInterfaces(int relSeqId, int clusterId, List<string> relClusterInterfaceList, List<string> addedInterfaceList)
        {
            string queryString = string.Format("Select Distinct PdbID, DomainInterfaceID From PfamDomainClusterInterfaces Where RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            int domainInterfaceId = 0;           
            List<string> missingInterfaceList = new List<string>();
            foreach (DataRow repInterfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = repInterfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(repInterfaceRow["DomainInterfaceID"].ToString());
                string[] simInterfaceList = GetSimilarHomoEntryInterfaces(relSeqId, pdbId, domainInterfaceId);
                foreach (string simInterface in simInterfaceList)
                {
                    if (relClusterInterfaceList.BinarySearch(simInterface) > -1)  // this interface is already in the cluster
                    {
                        continue;
                    }
                    if (addedInterfaceList.Contains (simInterface))  // this interface already added
                    {
                        continue;
                    }
                    if (!missingInterfaceList.Contains(simInterface))
                    {
                        missingInterfaceList.Add(simInterface);
                    }
                }
            }
            return missingInterfaceList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="repPdb"></param>
        /// <param name="repDomainInterfaceId"></param>
        /// <param name="homoEntries"></param>
        /// <returns></returns>
        private string[] GetSimilarHomoEntryInterfaces(int relSeqId, string repPdb, int repDomainInterfaceId)
        {
            List<string> homoInterfaceList = new List<string>();
            string homoInterface = "";
            string queryString = string.Format("Select Distinct PdbID2 As HomoPdbID, DomainInterfaceID2 As HomoDomainInterfaceID " +
                   " From {0} Where RelSeqID = {1} AND PdbID1 = '{2}' AND DomainInterfaceID1 = {3} AND QScore >= {4};",
                   domainInterfaceCompTable, relSeqId, repPdb, repDomainInterfaceId, simInterfaceCutoff);
            DataTable simHomoInterfaceTable = protcidQuery.Query(queryString);

            foreach (DataRow simInterfaceRow in simHomoInterfaceTable.Rows)
            {
                homoInterface = simInterfaceRow["HomoPdbID"].ToString() + simInterfaceRow["HomoDomainInterfaceID"].ToString();
                homoInterfaceList.Add(homoInterface);
            }

            queryString = string.Format("Select Distinct PdbID1 As HomoPdbID, DomainInterfaceID1 As HomoDomainInterfaceID " +
                " From {0} Where RelSeqID = {1} AND PdbID2 = '{2}' AND DomainInterfaceID2 = {3} AND QScore >= {4};",
                domainInterfaceCompTable, relSeqId, repPdb, repDomainInterfaceId, simInterfaceCutoff);
            simHomoInterfaceTable = protcidQuery.Query(queryString);

            foreach (DataRow simInterfaceRow in simHomoInterfaceTable.Rows)
            {
                homoInterface = simInterfaceRow["HomoPdbID"].ToString() + simInterfaceRow["HomoDomainInterfaceID"].ToString();
                homoInterfaceList.Add(homoInterface);
            }          
            return homoInterfaceList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="domainInterfaces"></param>
        /// <returns>are there any interfaces added</returns>
        private string[] AddDomainInterfacesToTable(int relSeqId,  int clusterId, string[] domainInterfaces)
        {
            List<string> addEntryList = new List<string>();
            foreach (string domainInterface in domainInterfaces)
            {
                if (! addEntryList.Contains (domainInterface.Substring (0, 4)))
                {
                    addEntryList.Add(domainInterface.Substring(0, 4));
                }
            }
            DataTable domainInterfaceTable = GetDomainInterfaceDefTable(relSeqId, addEntryList.ToArray());
            DataTable domainSpeciesUnpInfoTable = GetRelationDomainInterfaceSourceInfo(domainInterfaceTable);
            Dictionary<string, Dictionary<string, string>> pdbEntryBuFormatHash = new Dictionary<string, Dictionary<string, string>>();
            Dictionary<string, Dictionary<string, string>> pisaEntryBuFormatHash = new Dictionary<string, Dictionary<string, string>>();
            List<string> addedInterfaceList = new List<string>();
            string pdbId = "";
            int domainInterfaceId = 0;
            Dictionary<string, string[]> entryCfDict = new Dictionary<string, string[]>();
            foreach (string domainInterface in domainInterfaces)
            {
                pdbId = domainInterface.Substring(0, 4);
                domainInterfaceId = Convert.ToInt32(domainInterface.Substring(4, domainInterface.Length - 4));
                double surfaceArea = GetDomainInterfaceSurfaceArea(pdbId, domainInterfaceId, domainInterfaceTable);
                if (surfaceArea < surfaceAreaCutoff)
                {
                    continue;
                }
                addedInterfaceList.Add(domainInterface);
   //             DataRow clusterInfoRow = GetDomainInterfaceClusterInfoRow(pdbId, domainInterfaceId);               
                DataRow dataRow = clusterStatTable.NewRow();
                dataRow["RelSeqID"] = relSeqId;
                string relationString = GetFamilyCodesForRelation(relSeqId);
                dataRow["ClusterID"] = clusterId;
                if (entryCfDict.ContainsKey(pdbId))
                {
                    dataRow["RelCfGroupId"] = entryCfDict[pdbId][0];
                    dataRow["SpaceGroup"] = entryCfDict[pdbId][1];
                    dataRow["CrystForm"] = entryCfDict[pdbId][2];
                }
                else
                {
                    string[] cfFields = GetEntryInterfaceCrystForm(relSeqId, pdbId);
                    entryCfDict.Add(pdbId, cfFields);
                    dataRow["RelCfGroupId"] = cfFields[0];
                    dataRow["SpaceGroup"] = cfFields[1];
                    dataRow["CrystForm"] = cfFields[2];
                }
                dataRow["PdbID"] = pdbId;
                dataRow["DomainInterfaceID"] = domainInterfaceId;
                dataRow["ChainPfamArch"] = GetDomainChainPfamArch(pdbId, domainInterfaceId);
                dataRow["InterfaceUnit"] = GetCrystDomainInterfaceAbcFormat(pdbId, domainInterfaceId, relationString);

                // add asu and pdb BA and Pisa BA info to data row
                AddBAInfoToRow(pdbId, domainInterfaceId, dataRow, "pdb", pdbEntryBuFormatHash);
                AddBAInfoToRow(pdbId, domainInterfaceId, dataRow, "pisa", pisaEntryBuFormatHash);
                AddAsuInfoToRow(pdbId, domainInterfaceId, dataRow, domainInterfaceTable);

                // add surface area
                dataRow["SurfaceArea"] = surfaceArea;

                long[] interfaceDomainIds = GetDomainInterfaceDomainIds(pdbId, domainInterfaceId, domainInterfaceTable);
                string[] speciesNameUnp = GetDomainInterfaceSpeciesNameUnpCode(pdbId, interfaceDomainIds, domainSpeciesUnpInfoTable);

                dataRow["Species"] = speciesNameUnp[0];
                dataRow["Name"] = speciesNameUnp[1];
                dataRow["UnpCode"] = speciesNameUnp[2];

                clusterStatTable.Rows.Add(dataRow);
            }
            if (clusterStatTable.Rows.Count > 0)
            {
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, clusterStatTable);
                clusterStatTable.Clear();
            }
            return addedInterfaceList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryInterfaceCrystForm (int relSeqId, string pdbId)
        {
            string queryString = string.Format("Select RelCfGroupId, SpaceGroup, Asu From PfamNonRedundantCfGroups, PfamDomainCfGroups " +
                " Where RelSeqID = {0}  AND PdbID = '{1}' AND PfamNonRedundantCfGroups.GroupSeqID = PfamDomainCfGroups.GroupSeqID AND " + 
                " PfamNonRedundantCfGroups.CfGroupID = PfamDomainCfGroups.CfGroupID;", relSeqId, pdbId);
            DataTable relCfTable = ProtCidSettings.protcidQuery.Query(queryString);

            if (relCfTable.Rows.Count == 0)
            {
                queryString = string.Format("Select RelCfGroupId, SpaceGroup, Asu From PfamNonRedundantCfGroups, PfamDomainCfGroups, PfamHomoRepEntryAlign" +
                        " Where RelSeqID = {0}  AND PfamHomoRepEntryAlign.PdbID2 = '{1}' AND PfamHomoRepEntryAlign.PdbID1 = PfamNonRedundantCfGroups.PdbID " + 
                        " AND PfamNonRedundantCfGroups.GroupSeqID = PfamDomainCfGroups.GroupSeqID AND " +
                        " PfamNonRedundantCfGroups.CfGroupID = PfamDomainCfGroups.CfGroupID;", relSeqId, pdbId);
                relCfTable = ProtCidSettings.protcidQuery.Query(queryString);
            }
            string[] relCfFields = new string[3];
            if (relCfTable.Rows.Count > 0)
            {
                relCfFields[0] = relCfTable.Rows[0]["RelCfGroupID"].ToString();
                relCfFields[1] = relCfTable.Rows[0]["SpaceGroup"].ToString();
                relCfFields[2] = relCfTable.Rows[0]["Asu"].ToString();
            }
            return relCfFields;
        }
        /// <summary>
        /// 
        /// </summary>
        public void AddMissingDomainInterfacesToClusterInterfaceTable()
        {
            DbUpdate dbUpdate = new DbUpdate();
            InitializeTable();
            int relSeqId = 15457;
            int clusterId = 3;
            int[] relationNums = GetRelationNumbers(relSeqId);

            string queryString = string.Format("Select PdbID, DomainInterfaceID From PfamDomainInterfaceCluster " +
                " Where RelSeqId = {0} AND ClusterId = {1} AND Not Exists " +
                "(Select PdbID, DomainInterfaceId From PfamDomainClusterInterfaces Where RelSeqId = 14511 and ClusterId = 1 " +
                " AND PdbID = PfamDomainInterfaceCluster.PdbID and DomainInterfaceID = PfamDomainInterfaceCluster.DomainInterfaceID);",
                relSeqId, clusterId);
            DataTable missingRepInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            int domainInterfaceId = 0;
            List<string> clusterDomainInterfaceList = new List<string>();
            List<string> updateEntryList = new List<string>();
            Dictionary<string, string[]> repHomoEntryDict = new Dictionary<string, string[]>();
            foreach (DataRow interfaceRow in missingRepInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());
                clusterDomainInterfaceList.Add(pdbId + interfaceRow["DomainInterfaceID"].ToString());
                if (!updateEntryList.Contains(pdbId))
                {
                    updateEntryList.Add(pdbId);
                    string[] homoEntries = GetHomoEntries(pdbId);
                    repHomoEntryDict.Add(pdbId, homoEntries);
                    updateEntryList.AddRange(homoEntries);
                }
            }
            DataTable domainInterfaceTable = GetDomainInterfaceDefTable(relSeqId, updateEntryList.ToArray());
            DataTable domainSpeciesUnpInfoTable = GetRelationDomainInterfaceSourceInfo(domainInterfaceTable);

            AddRepHomoDomainInterfacesToTable(clusterDomainInterfaceList.ToArray(), repHomoEntryDict, domainInterfaceTable, domainSpeciesUnpInfoTable);
        
            UpdateClusterSumInfo(relSeqId, clusterId);

            AddClusterSumInfo(relSeqId, clusterId, relationNums[0], relationNums[1]);
            string deleteString = string.Format("Delete From PfamDomainClusterSumInfo Where RelSeqID = {0} AND ClusterId = {1};", relSeqId, clusterId);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, deleteString);
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, clusterSumInfoTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="repHomoEntryDict"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <param name="domainSpeciesUnpInfoTable"></param>
        private void AddRepHomoDomainInterfacesToTable(string[] repDomainInterfaces, Dictionary<string, string[]> repHomoEntryDict, 
            DataTable domainInterfaceTable, DataTable domainSpeciesUnpInfoTable)
        {
            Dictionary<string, Dictionary<string, string>> pdbEntryBuFormatHash = new Dictionary<string, Dictionary<string, string>>();
            Dictionary<string, Dictionary<string, string>> pisaEntryBuFormatHash = new Dictionary<string, Dictionary<string, string>>();
            List<string> addedInterfaceList = new List<string>();
            string pdbId = "";
            int domainInterfaceId = 0;
            int relSeqId = 0;
            foreach (string domainInterface in repDomainInterfaces)
            {
                pdbId = domainInterface.Substring(0, 4);
                domainInterfaceId = Convert.ToInt32(domainInterface.Substring (4, domainInterface.Length - 4));
                double surfaceArea = GetDomainInterfaceSurfaceArea(pdbId, domainInterfaceId, domainInterfaceTable);
                if (surfaceArea < surfaceAreaCutoff)
                {
                    continue;
                }
                addedInterfaceList.Add(domainInterface);
                DataRow clusterInfoRow = GetDomainInterfaceClusterInfoRow(pdbId, domainInterfaceId);
                relSeqId = Convert.ToInt32(clusterInfoRow["RelSeqID"].ToString ());
                DataRow dataRow = clusterStatTable.NewRow();
                dataRow["RelSeqID"] = relSeqId;
                string relationString = GetFamilyCodesForRelation(relSeqId);
                dataRow["ClusterID"] = clusterInfoRow["ClusterId"];
                dataRow["RelCfGroupId"] = clusterInfoRow["RelCfGroupId"];
                dataRow["SpaceGroup"] = clusterInfoRow["SpaceGroup"];
                dataRow["CrystForm"] = clusterInfoRow["ASU"];
                dataRow["PdbID"] = clusterInfoRow["PdbID"];
                dataRow["DomainInterfaceID"] = clusterInfoRow["DomainInterfaceID"];
                dataRow["ChainPfamArch"] =
                    GetDomainChainPfamArch(pdbId, Convert.ToInt32(clusterInfoRow["DomainInterfaceID"].ToString()));
                dataRow["InterfaceUnit"] = GetCrystDomainInterfaceAbcFormat(pdbId, domainInterfaceId, relationString);

                // add asu and pdb BA and Pisa BA info to data row
                AddBAInfoToRow(pdbId, domainInterfaceId, dataRow, "pdb", pdbEntryBuFormatHash);
                AddBAInfoToRow(pdbId, domainInterfaceId, dataRow, "pisa", pisaEntryBuFormatHash);
                AddAsuInfoToRow(pdbId, domainInterfaceId, dataRow, domainInterfaceTable);

                // add surface area
                dataRow["SurfaceArea"] = surfaceArea;

                long[] interfaceDomainIds = GetDomainInterfaceDomainIds(pdbId, domainInterfaceId, domainInterfaceTable);
                string[] speciesNameUnp = GetDomainInterfaceSpeciesNameUnpCode(pdbId, interfaceDomainIds, domainSpeciesUnpInfoTable);

                dataRow["Species"] = speciesNameUnp[0];
                dataRow["Name"] = speciesNameUnp[1];
                dataRow["UnpCode"] = speciesNameUnp[2];

                clusterStatTable.Rows.Add(dataRow);

                string homoPdbId = "";
                int homoDomainInterfaceId = 0;
                if (repHomoEntryDict.ContainsKey(pdbId))
                {
                    string[] homoEntries = repHomoEntryDict[pdbId];

                    if (homoEntries.Length > 0)
                    {
                        string[] homoInterfaces = GetSimilarHomoEntryInterfaces(relSeqId, pdbId, domainInterfaceId, homoEntries);
                        foreach (string homoInterface in homoInterfaces)
                        {
                            if (addedInterfaceList.Contains(homoInterface))
                            {
                                continue;
                            }
                            string[] homoInterfaceFields = homoInterface.Split('_');
                            homoPdbId = homoInterfaceFields[0];
                            homoDomainInterfaceId = Convert.ToInt32(homoInterfaceFields[1]);
                            surfaceArea = GetDomainInterfaceSurfaceArea(homoPdbId, homoDomainInterfaceId, domainInterfaceTable);
                            if (surfaceArea < surfaceAreaCutoff)
                            {
                                continue;
                            }

                            DataRow homoDataRow = clusterStatTable.NewRow();
                            homoDataRow.ItemArray = dataRow.ItemArray;
                            homoDataRow["PdbID"] = homoPdbId;
                            homoDataRow["DomainInterfaceID"] = homoDomainInterfaceId;

                            // add asu and pdb BA and Pisa BA info to data row
                            AddBAInfoToRow(homoPdbId, homoDomainInterfaceId, dataRow, "pdb", pdbEntryBuFormatHash);
                            AddBAInfoToRow(homoPdbId, homoDomainInterfaceId, dataRow, "pisa", pisaEntryBuFormatHash);
                            AddAsuInfoToRow(homoPdbId, homoDomainInterfaceId, dataRow, domainInterfaceTable);

                            // add surface area
                            dataRow["SurfaceArea"] = surfaceArea;

                            interfaceDomainIds = GetDomainInterfaceDomainIds(homoPdbId, homoDomainInterfaceId, domainInterfaceTable);

                            string[] homoSpeciesNameUnp = GetDomainInterfaceSpeciesNameUnpCode(homoPdbId, interfaceDomainIds, domainSpeciesUnpInfoTable);

                            dataRow["Species"] = homoSpeciesNameUnp[0];
                            dataRow["Name"] = homoSpeciesNameUnp[1];
                            dataRow["UnpCode"] = homoSpeciesNameUnp[2];

                            clusterStatTable.Rows.Add(homoDataRow);
                            addedInterfaceList.Add(homoInterface);
                        }
                    }
                }
            }
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, clusterStatTable);
            clusterStatTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private DataTable GetDomainInterfaceDefTable(int relSeqId, string[] updateEntries)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where RelSeqID = {0} AND PdbID IN ({1}) AND SurfaceArea > {2};", 
                relSeqId, ParseHelper.FormatSqlListString (updateEntries), surfaceAreaCutoff);
            DataTable domainInterfaceTable = protcidQuery.Query(queryString);
            return domainInterfaceTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private DataRow GetDomainInterfaceClusterInfoRow (string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaceCluster Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            if (clusterInfoTable.Rows.Count > 0)
            {
                return clusterInfoTable.Rows[0];
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterStatTable"></param>
        /// <returns></returns>
        public void UpdateClusterSumInfo(int relSeqId, int clusterId)
        {
            InitializeTable();
            string queryString = string.Format("Select * From PfamDomainClusterInterfaces Where RelSeqId = {0} AND ClusterID = {1};", relSeqId, clusterId);
            clusterStatTable = ProtCidSettings.protcidQuery.Query(queryString);
            int[] clusterCfEntryNumbers = GetRelationNumbers(relSeqId);
            string pdbId = "";
            //    string cfgString = "";
            int relCfGroupId = 0;
            List<string> clusterEntryList = new List<string>();
            List<int> clusterCfgList = new List<int>();
            List<string> inPdbList = new List<string>();
            List<string> inPisaList = new List<string>();
            List<string> inAsuList = new List<string>();
            int[] maxPdbBuNums = null;
            int[] maxPisaBuNums = null;
            double totalSurfaceArea = 0;
            int numOfSAs = 0;
            double surfaceArea = 0;
            int numOfHetero = 0;
            int numOfHomo = 0;
            int numOfIntra = 0;
            List<string> heteroEntryList = new List<string>();
            List<string> homoEntryList = new List<string>();
            List<string> intraEntryList = new List<string>();
            string interfaceUnit = "";

            foreach (DataRow statRow in clusterStatTable.Rows)
            {
                UpdateIntraChainDomainInterfaceInPisaBu(statRow);

                pdbId = statRow["PdbID"].ToString();

                if (!clusterEntryList.Contains(pdbId))
                {
                    clusterEntryList.Add(pdbId);
                }

                relCfGroupId = Convert.ToInt32(statRow["RelCfGroupId"].ToString());
                if (!clusterCfgList.Contains(relCfGroupId))
                {
                    clusterCfgList.Add(relCfGroupId);
                }

                if (Convert.ToInt32(statRow["InPDB"].ToString()) == 1)
                {
                    if (!inPdbList.Contains(pdbId))
                    {
                        inPdbList.Add(pdbId);
                    }
                }
                if (Convert.ToInt32(statRow["inPisa"].ToString()) == 1)
                {
                    if (!inPisaList.Contains(pdbId))
                    {
                        inPisaList.Add(pdbId);
                    }
                }
                if (Convert.ToInt32(statRow["InAsu"].ToString()) == 1)
                {
                    if (!inAsuList.Contains(pdbId))
                    {
                        inAsuList.Add(pdbId);
                    }
                }

                GetMaxCopyNumFromAsuBu(statRow["PdbBu"].ToString(), ref maxPdbBuNums);
                //            GetMaxCopyNumFromAsuBu(statRow["PqsBu"].ToString(), ref maxPqsBuNums);
                GetMaxCopyNumFromAsuBu(statRow["PisaBu"].ToString(), ref maxPisaBuNums);

                surfaceArea = Convert.ToDouble(statRow["SurfaceArea"].ToString());
                if (surfaceArea > 0)
                {
                    totalSurfaceArea += surfaceArea;
                    numOfSAs++;
                }

                interfaceUnit = statRow["InterfaceUnit"].ToString().TrimEnd();
                int interfaceType = GetInterfaceType(interfaceUnit);
                switch (interfaceType)
                {
                    case 0:
                        numOfHomo++;
                        if (!homoEntryList.Contains(pdbId))
                        {
                            homoEntryList.Add(pdbId);
                        }
                        break;

                    case 1:
                        numOfIntra++;
                        if (!intraEntryList.Contains(pdbId))
                        {
                            intraEntryList.Add(pdbId);
                        }
                        break;

                    case 2:
                        numOfHetero++;
                        if (!heteroEntryList.Contains(pdbId))
                        {
                            heteroEntryList.Add(pdbId);
                        }
                        break;

                    default:
                        break;
                }
            }
            int minSeqId = GetMinSeqIdInCluster();

            DataRow sumInfoRow = clusterSumInfoTable.NewRow();
            sumInfoRow["RelSeqID"] = relSeqId;
            sumInfoRow["ClusterID"] = clusterId;
            sumInfoRow["SurfaceArea"] = totalSurfaceArea / (double)numOfSAs;
            sumInfoRow["InPdb"] = inPdbList.Count; ;
            sumInfoRow["InPisa"] = inPisaList.Count;
            sumInfoRow["InAsu"] = inAsuList.Count;
            sumInfoRow["NumOfCfgCluster"] = clusterCfgList.Count;
            sumInfoRow["NumOfEntryCluster"] = clusterEntryList.Count;
            sumInfoRow["NumOfCfgRelation"] = clusterCfEntryNumbers[0];
            sumInfoRow["NumOfEntryRelation"] = clusterCfEntryNumbers[1];
            sumInfoRow["MinSeqIdentity"] = minSeqId;
            sumInfoRow["NumOfEntryHomo"] = homoEntryList.Count;
            sumInfoRow["NumOfEntryHetero"] = heteroEntryList.Count;
            sumInfoRow["NumOfEntryIntra"] = intraEntryList.Count;
            sumInfoRow["NumOfHomo"] = numOfHomo;
            sumInfoRow["NumOfHetero"] = numOfHetero;
            sumInfoRow["NumOfIntra"] = numOfIntra;
            double mediumSurfaceArea = 0;
            /*    DataRow[] clusterInterfaceRows = clusterStatTable.Select("", "SurfaceArea ASC");
                string clusterInterface = GetDomainClusterInterface(clusterInterfaceRows, out mediumSurfaceArea);*/
            string clusterInterface = GetDomainClusterInterface(relSeqId, clusterId, out mediumSurfaceArea);
            sumInfoRow["ClusterInterface"] = clusterInterface;
            sumInfoRow["MediumSurfaceArea"] = mediumSurfaceArea;
            clusterSumInfoTable.Rows.Add(sumInfoRow);

            string deleteString = string.Format("Delete From PfamDomainClusterSumInfo Where RelSeqID = {0} AND ClusterId = {1};", relSeqId, clusterId);
            protcidUpdate.Update(deleteString);
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, clusterSumInfoTable);
        }
        #endregion

        #region min seq identity
        /// <summary>
        /// 
        /// </summary>
        public void UpdateDomainClusterMinSeqIdentity()
        {
            string queryString = "Select RelSeqID, ClusterId From PfamDomainClusterSumInfo;";
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            int relSeqId = 0;
            int clusterId = 0;
            int clusterMinSeqIdentity = 0;
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                relSeqId = Convert.ToInt32(clusterRow["RelSeqID"].ToString ());
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString ());
                clusterMinSeqIdentity = GetClusterMinSeqIdentity(relSeqId, clusterId);
                UpdateClusterMinSeqIdentity(relSeqId, clusterId, clusterMinSeqIdentity);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="minSeqIdentity"></param>
        private void UpdateClusterMinSeqIdentity (int relSeqId, int clusterId, int minSeqIdentity)
        {
            string updateString = string.Format("Update PfamDomainClusterSumInfo Set MinSeqIdentity = {0} Where RelSeqId = {1} AND ClusterID = {2};", minSeqIdentity, relSeqId, clusterId);
            protcidUpdate.Update(updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private int GetClusterMinSeqIdentity (int relSeqId, int clusterId)
        {
            string queryString = string.Format("Select PdbID, DomainInterfaceID From PfamDomainInterfaceCluster Where RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> addedEntryList = new List<string>();
            List<string> clusterDomainInterfaceList = new List<string>();
            string pdbId = "";
            foreach (DataRow domainInterfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = domainInterfaceRow["PdbID"].ToString();
                if (addedEntryList.Contains(pdbId))
                {
                    continue;
                }
                addedEntryList.Add(pdbId);
                clusterDomainInterfaceList.Add (pdbId + domainInterfaceRow["DomainInterfaceID"].ToString ());
            }
            double identity = 0;
            double minSeqIdentity = 100.0;
            for (int i = 0; i < clusterDomainInterfaceList.Count; i++)
            {
                for (int j = i + 1; j < clusterDomainInterfaceList.Count; j++)
                {
                    identity = GetDomainInterfaceIdentity(clusterDomainInterfaceList[i], clusterDomainInterfaceList[j]);
                    if (minSeqIdentity > identity && identity > 0)
                    {
                        minSeqIdentity = identity;
                    }
                }
            }
            return (int)minSeqIdentity;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadInConsistentInPdbPisaInterfaces()
        {
            List<string> updateBaEntryInterfaceList = new List<string>();
            string baErrorFile = @"X:\Qifang\Paper\protcid_update\data_v31\DomainInPdbPisaCheck2.txt";
            StreamReader dataReader = new StreamReader(baErrorFile);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                if (!updateBaEntryInterfaceList.Contains(fields[1] + fields[2]))
                {
                    updateBaEntryInterfaceList.Add(fields[1] + fields[2]);
                }
            }
            dataReader.Close();
            updateBaEntryInterfaceList.Sort();
            return updateBaEntryInterfaceList.ToArray();
        }
        #endregion

        #region update BA comp info
        /// <summary>
        /// 
        /// </summary>
        public void UpdateBACompInfo()
        {
            Dictionary<string, Dictionary<string, string>> pdbEntryBuFormatHash = new Dictionary<string,Dictionary<string,string>> ();
            Dictionary<string, Dictionary<string, string>> pisaEntryBuFormatHash = new Dictionary<string, Dictionary<string, string>>();

            // store update groups, for update summary data of groups.
            StreamWriter updateClusterWriter = new StreamWriter("BaUpdatedDomainClustersList1.txt");
            string[] updateInterfaceList = ReadInConsistentInPdbPisaInterfaces();
            string pdbId = "";
            int domainInterfaceId = 0;
            string orgInPdb = "";
            string orgInPisa = "";
            string updateString = "";
            string queryString = "";
            List<string> updateDomainClusterList = new List<string>();
            string chainCluster = "";

            foreach (string entryInterface in updateInterfaceList)
            {
                
                pdbId = entryInterface.Substring(0, 4);
                domainInterfaceId = Convert.ToInt32(entryInterface.Substring(4, entryInterface.Length - 4));
                queryString = string.Format("Select RelSeqId, ClusterID, PdbID, DomainInterfaceID, InPdb, InPisa, PdbBuID, PdbBu, PisaBuId, PisaBu " +
                    "From PfamDomainClusterInterfaces where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
                DataTable interfaceTable = protcidQuery.Query(queryString);
                if (interfaceTable.Rows.Count > 0)
                {
                    DataRow interfaceStatRow = interfaceTable.Rows[0];
                    chainCluster = interfaceStatRow["RelSeqID"].ToString() + "_" +
                        interfaceStatRow["ClusterID"].ToString();

                    orgInPdb = interfaceStatRow["InPdb"].ToString();
                    orgInPisa = interfaceStatRow["InPisa"].ToString();

                    try
                    {
                        AddBAInfoToRow(pdbId, domainInterfaceId, interfaceStatRow, "pdb", pdbEntryBuFormatHash);
                        AddBAInfoToRow(pdbId, domainInterfaceId, interfaceStatRow, "pisa", pisaEntryBuFormatHash);

                        if (orgInPdb != interfaceStatRow["InPdb"].ToString() ||
                            orgInPisa != interfaceStatRow["InPisa"].ToString())
                        {
                             updateString = string.Format("Update PfamDomainClusterInterfaces Set InPdb = '{0}', InPisa = '{1}'," +
                                " PdbBuID = '{2}', PdbBu = '{3}', PisaBuId = '{4}', PisaBu = '{5}' " +
                                " Where PdbID = '{6}' AND DomainInterfaceID = {7};",
                                interfaceStatRow["InPdb"], interfaceStatRow["InPisa"], interfaceStatRow["PdbBuID"],
                                interfaceStatRow["PdbBu"], interfaceStatRow["PisaBuId"], interfaceStatRow["PisaBu"],
                                pdbId, domainInterfaceId);
                            protcidUpdate.Update(updateString);

                            if (!updateDomainClusterList.Contains(chainCluster))
                            {
                                updateDomainClusterList.Add(chainCluster);
                                updateClusterWriter.WriteLine(chainCluster.ToString());
                                updateClusterWriter.Flush();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.logWriter.WriteLine(entryInterface + " Update InPDB/InPisa error: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
            }
            updateClusterWriter.Close();
            UpdateClusterBaSummaryInfo(updateDomainClusterList.ToArray());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateClusters"></param>
        public void UpdateClusterBaSummaryInfo(string[] updateClusters)
        {
            string queryString = "";
            string updateString = "";
            foreach (string updateCluster in updateClusters)
            {
                string[] groupCluster = updateCluster.Split('_');
                queryString = string.Format("Select Distinct PdbID From PfamDomainClusterInterfaces " +
                    "Where RelSeqID = {0} AND ClusterID = {1} AND InPdb = '1';",
                    groupCluster[0], groupCluster[1]);
                DataTable inPdbEntryTable = protcidQuery.Query(queryString);

                queryString = string.Format("Select Distinct PdbID From PfamDomainClusterInterfaces " +
                    "Where RelSeqID = {0} AND ClusterID = {1} AND InPisa = '1';",
                    groupCluster[0], groupCluster[1]);
                DataTable inPisaEntryTable = protcidQuery.Query(queryString);

                updateString = string.Format("Update PfamDomainClusterSumInfo Set InPdb = {0}, InPisa = {1} " +
                    "Where RelSeqID = {2} AND ClusterID = {3};", inPdbEntryTable.Rows.Count,
                    inPisaEntryTable.Rows.Count, groupCluster[0], groupCluster[1]);
                protcidUpdate.Update(updateString);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="inPdb"></param>
        /// <returns></returns>
        private bool IsInterfaceInBA(string pdbId, int domainInterfaceId, bool isPdb, out string buId)
        {
            string queryString = "";
            buId = "-1";
            if (isPdb)
            {
                queryString = string.Format("Select * From CrystPdbBuDomainInterfaceComp Where PdbID = '{0}' AND DomainInterfaceID = {1} Order By Qscore Desc;", pdbId, domainInterfaceId);
            }
            else
            {
                queryString = string.Format("Select * From CrystPisaBuDomainInterfaceComp Where PdbID = '{0}' AND DomainInterfaceID = {1} Order By Qscore Desc;", pdbId, domainInterfaceId);
            }
            DataTable interfaceBACompTable = protcidQuery.Query(queryString);
            double qScore = 0;
            if (interfaceBACompTable.Rows.Count > 0)
            {
                qScore = Convert.ToDouble(interfaceBACompTable.Rows[0]["QScore"].ToString());
                if (qScore >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
                {
                    buId = interfaceBACompTable.Rows[0]["BuID"].ToString();
                    return true;
                }
            }
            return false;
        }
        #endregion
        #endregion
    }
}
