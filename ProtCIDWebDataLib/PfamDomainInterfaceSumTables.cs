using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using DbLib;
using ProtCidSettingsLib;
using PfamLib.PfamArch;
using PfamLib.Settings;
using InterfaceClusterLib.AuxFuncs;
using AuxFuncLib;


namespace ProtCIDWebDataLib
{
    public class PfamDomainInterfaceSumTables
    {
        private DbUpdate protcidUpdate = null;
        private DbInsert dbInsert = null;
        private PfamArchitecture pfamArch = new PfamArchitecture();
        private EntryCrystForms entryCfs = new EntryCrystForms();
        private string chainArchTableName = "PfamDomainChainArchRelation";
        private string ipfamInPdbTableName = "IPfamInPdb";

        public PfamDomainInterfaceSumTables ()
        {
            ProtCidSettings.LoadDirSettings();
            if (ProtCidSettings.pdbfamDbConnection == null)
            {
                ProtCidSettings.pdbfamDbConnection = new DbConnect();
                ProtCidSettings.pdbfamDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                    ProtCidSettings.dirSettings.pdbfamDbPath;
                ProtCidSettings.pdbfamQuery = new DbQuery(ProtCidSettings.pdbfamDbConnection);
            }

            if (ProtCidSettings.protcidDbConnection == null)
            {
                ProtCidSettings.protcidDbConnection = new DbConnect();
                ProtCidSettings.protcidDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                    ProtCidSettings.dirSettings.protcidDbPath;
                ProtCidSettings.protcidQuery = new DbQuery(ProtCidSettings.protcidDbConnection);
            }
            protcidUpdate = new DbUpdate(ProtCidSettings.protcidDbConnection);
            dbInsert = new DbInsert();

            PfamLibSettings.pdbfamConnection = ProtCidSettings.pdbfamDbConnection;
            PfamLibSettings.pdbfamDbQuery = new DbQuery(PfamLibSettings.pdbfamConnection);
        }

        /// <summary>
        /// 
        /// </summary>
        public void CreatePfamDomainSumTables ()
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get Pfam chain Arch relations for protcid web site.");
            BuildPfamPfamEntryChainArchs ();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get meta data for IPFam in the PDB for protcid web site.");
            BuildIPfamInPdbMetaData ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateRelEntryDict"></param>
        public void UpdatePfamDomainSumTables(string[] updateEntries)
        {
            Dictionary<int, List<string>> updateRelEntryDict = GetUpdateRelEntryDict(updateEntries);
            Dictionary<string, List<string>> updatePfamPepEntryDict = GetUpdatePepPfamEntryDict(updateEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Pfam chain Arch relations for protcid web site.");
            ProtCidSettings.logWriter.WriteLine("Update Pfam chain Arch relations for protcid web site.");

            UpdateDomainInterfaceChainArchs(updateEntries, updateRelEntryDict, updatePfamPepEntryDict);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            List<int> updateRelSeqIdList = new List<int>(updateRelEntryDict.Keys);
            updateRelSeqIdList.Sort();
            List<string> updatePepPfamList = new List<string>(updatePfamPepEntryDict.Keys);
            updatePepPfamList.Sort();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update meta data for IPFam in the PDB for protcid web site.");
            ProtCidSettings.logWriter.WriteLine("Update meta data for Ipfam in the PDB fro protcid.");
            UpdateIPfamInPdbMetaData (updateRelSeqIdList.ToArray(), updatePepPfamList.ToArray());
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();
        }

        #region chain arch relations
        #region build
        /// <summary>
        /// 
        /// </summary>
        public void BuildPfamPfamEntryChainArchs()
        {
            bool isUpdate = false;
            DataTable entryChainArchStatInfoTable = CreateChainArchStatInfoTable(isUpdate);

            string queryString = "Select RelSeqID From PfamDomainFamilyRelation Where FamilyCode2 <> 'peptide';";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = relSeqIdTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIdTable.Rows.Count;

            int relSeqId = 0;
            foreach (DataRow relSeqRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqRow["RelSeqID"].ToString());               

                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                DataTable relationDomainInterfaceTable = GetRelationDomainInterfaces(relSeqId);
                string[] entries = GetRelationEntries(relationDomainInterfaceTable);
                foreach (string pdbId in entries)
                {
                    GetChainArchStatInfo(pdbId, relSeqId, relationDomainInterfaceTable, entryChainArchStatInfoTable);
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Add Peptide binding Pfams!");
            AddPfamPeptideEntryChainArchs ();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        public void AddPfamPeptideEntryChainArchs()
        {
            string queryString = "Select First 1 * From  " + chainArchTableName;
            DataTable chainArchStatInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            chainArchStatInfoTable.Clear();
            chainArchStatInfoTable.TableName = chainArchTableName;

            queryString = "Select Distinct PfamID From PfamPeptideInterfaces;";
            DataTable pepPfamTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pepPfamId = "";
            DataTable pfamPepInterfaceTable = null;
            foreach (DataRow pfamRow in pepPfamTable.Rows)
            {
                pepPfamId = pfamRow["PfamID"].ToString().TrimEnd();
                string[] pepEntries = GetPepPfamEntries(pepPfamId, out pfamPepInterfaceTable);
                foreach (string pdbId in pepEntries)
                {
                    GetPeptideChainArchStatInfo(pdbId, pepPfamId, pfamPepInterfaceTable, chainArchStatInfoTable);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pfamPepInterfaceTable"></param>
        /// <returns></returns>
        private string[] GetPepPfamEntries(string pfamId, out DataTable pfamPepInterfaceTable)
        {
            string queryString = string.Format("Select PfamID, PdbID, DomainInterfaceID, AsymChain, PepAsymChain From PfamPeptideInterfaces Where PfamID = '{0}';", pfamId);
            pfamPepInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            List<string> entryList = new List<string>();
            string pdbId = "";
            foreach (DataRow entryRow in pfamPepInterfaceTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            return entryList.ToArray();
        }
        #endregion

        #region update
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateDomainInterfaceChainArchs(string[] updateEntries, Dictionary<int, List<string>> updateRelEntryDict, Dictionary<string, List<string>> updatePfamPepEntryDict)
        {
            bool isUpdate = true;
            DataTable entryChainArchStatInfoTable = CreateChainArchStatInfoTable(isUpdate);

            DeleteEntryChainArchInfo(updateEntries);

            UpdateDomainInterfaceChainArchs(updateRelEntryDict, entryChainArchStatInfoTable);
            UpdatePeptideInterfaceChainArchs(updatePfamPepEntryDict, entryChainArchStatInfoTable);           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private Dictionary<int, List<string>> GetUpdateRelEntryDict (string[] updateEntries)
        {
            string queryString = "";
            Dictionary<int, List<string>> updateRelEntryDict = new Dictionary<int, List<string>>();
            int relSeqId = 0;
            string pdbId = "";
            for (int i = 0; i < updateEntries.Length; i += 100)
            {
                string[] subEntries = ParseHelper.GetSubArray(updateEntries, i, 100);
                queryString = string.Format("Select Distinct RelSeqID, PdbID From PfamDomainInterfaces Where PdbID IN ({0});", ParseHelper.FormatSqlListString (subEntries));
                DataTable subRelEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow relEntryRow in subRelEntryTable.Rows)
                {
                    relSeqId = Convert.ToInt32(relEntryRow["RelSeqID"].ToString());
                    pdbId = relEntryRow["PdbID"].ToString();
                    if (updateRelEntryDict.ContainsKey (relSeqId))
                    {
                        updateRelEntryDict[relSeqId].Add(pdbId);
                    }
                    else
                    {
                        List<string> entryList = new List<string>();
                        entryList.Add(pdbId);
                        updateRelEntryDict.Add(relSeqId, entryList);
                    }
                }
            }
            return updateRelEntryDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetUpdatePepPfamEntryDict(string[] updateEntries)
        {
            string queryString = "";
            Dictionary<string, List<string>> updateRelEntryDict = new Dictionary<string, List<string>>();
            string pfamId = "";
            string pdbId = "";
            for (int i = 0; i < updateEntries.Length; i += 100)
            {
                string[] subEntries = ParseHelper.GetSubArray(updateEntries, i, 100);
                queryString = string.Format("Select Distinct PfamID, PdbID From PfamPeptideInterfaces Where PdbID IN ({0});", ParseHelper.FormatSqlListString(subEntries));
                DataTable subRelEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
                foreach (DataRow relEntryRow in subRelEntryTable.Rows)
                {
                    pfamId  = relEntryRow["PfamID"].ToString();
                    pdbId = relEntryRow["PdbID"].ToString();
                    if (updateRelEntryDict.ContainsKey(pfamId ))
                    {
                        updateRelEntryDict[pfamId].Add(pdbId);
                    }
                    else
                    {
                        List<string> entryList = new List<string>();
                        entryList.Add(pdbId);
                        updateRelEntryDict.Add(pfamId, entryList);
                    }
                }
            }
            return updateRelEntryDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private void DeleteEntryChainArchInfo (string[] updateEntries)
        {
            string deleteString = "";
            for (int i = 0; i < updateEntries.Length; i += 100)
            {
                string[] subEntries = ParseHelper.GetSubArray(updateEntries, i, 100);
                deleteString = string.Format("Delete From {0} Where PdbID IN ({1});", chainArchTableName, ParseHelper.FormatSqlListString(subEntries));
                protcidUpdate.Delete(deleteString);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void UpdateDomainInterfaceChainArchs(Dictionary<int, List<string>> updateRelEntryDict, DataTable entryChainArchStatInfoTable)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = updateRelEntryDict.Count;
            ProtCidSettings.progressInfo.totalStepNum = updateRelEntryDict.Count;

            List<int> updateRelSeqList = new List<int>(updateRelEntryDict.Keys);
            updateRelSeqList.Sort();
            foreach (int relSeqId in updateRelSeqList)
            {
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                foreach (string pdbId in updateRelEntryDict[relSeqId])
                {
                    DataTable relationDomainInterfaceTable = GetRelationDomainInterfaces(relSeqId, pdbId);
                    try
                    {
                        GetChainArchStatInfo(pdbId, relSeqId, relationDomainInterfaceTable, entryChainArchStatInfoTable);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.logWriter.WriteLine(relSeqId + " " + pdbId + " Chain Arch stat info table error: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updatePfamPepEntryDict"></param>
        /// <param name="entryChainArchStatInfoTable"></param>
        private void UpdatePeptideInterfaceChainArchs(Dictionary<string, List<string>> updatePfamPepEntryDict, DataTable entryChainArchStatInfoTable)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = updatePfamPepEntryDict.Count;
            ProtCidSettings.progressInfo.totalStepNum = updatePfamPepEntryDict.Count;

            List<string> updatePfamList = new List<string>(updatePfamPepEntryDict.Keys);
            updatePfamList.Sort();
            foreach (string pepPfamId in updatePfamList)
            {
                ProtCidSettings.progressInfo.currentFileName = pepPfamId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                foreach (string pdbId in updatePfamPepEntryDict[pepPfamId])
                {
                    DataTable pfamPepInterfaceTable = GetPfamEntryPeptideInterfaces (pepPfamId, pdbId);
                    try
                    {
                        GetPeptideChainArchStatInfo(pdbId, pepPfamId, pfamPepInterfaceTable, entryChainArchStatInfoTable);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.logWriter.WriteLine(pepPfamId + " " + pdbId + " peptide Chain Arch stat info table error: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetPfamEntryPeptideInterfaces (string pfamId, string pdbId)
        {
            string queryString = string.Format("Select PdbID, DomainInterfaceID, AsymChain From PfamPeptideInterfaces Where PfamID = '{0}' AND PdbID = '{1}';", pfamId, pdbId);
            DataTable pfamPepInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            return pfamPepInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        private void DeleteEntryChainArchInfo(int relSeqId, string pdbId)
        {
            string deleteString = string.Format("Delete From {0} Where RelSeqID = {1} AND PdbID = '{2}';", chainArchTableName, relSeqId, pdbId);
            protcidUpdate.Delete(deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteEntryChainArchInfo(string pdbId)
        {
            string deleteString = string.Format("Delete From {0} Where PdbID = '{1}';", chainArchTableName, pdbId);
            protcidUpdate.Delete(deleteString);
        }
        #endregion

        #region chain arch stat info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="relSeqId"></param>
        /// <param name="relationDomainInterfaceTable"></param>
        private void GetChainArchStatInfo(string pdbId, int relSeqId, DataTable relationDomainInterfaceTable, DataTable chainArchStatInfoTable)
        {
            DataRow[] entryDomainInterfaceRows = relationDomainInterfaceTable.Select(string.Format("RelSeqID = '{0}' AND PdbID = '{1}'", relSeqId, pdbId));
            Dictionary<int, string> entityPfamArchHash = pfamArch.GetEntryEntityGroupPfamArchHash(pdbId);
            Dictionary<int, string> entityUnpCodeDict = GetEntryEntityUnpCodeDict(pdbId);
            //  Hashtable entityPfamArchHash = GetEntryEntityPfamArchHash(pdbId);
            DataTable chainDomainTable = GetEntryChainDomains(pdbId);
            Dictionary<string, int> chainArchNumOfInterfacesHash = new Dictionary<string, int>();
            Dictionary<string, int> chainArchIntraHash = new Dictionary<string, int>();
            Dictionary<string, int> chainArchHeteroHash = new Dictionary<string, int>();
            Dictionary<string, string> chainArchUnpCodeHash = new Dictionary<string, string>();
            string chainPfamArch1 = "";
            string chainPfamArch2 = "";
            string unpCodes1 = "";
            string unpCodes2 = "";
            string unpCodesRelation = "";
            int entityId1 = 0;
            int entityId2 = 0;
            string asymChain1 = "";
            string asymChain2 = "";
            string chainPfamArchRelation = "";
            int chainInterfaceId = 0;
            int numOfIntra = 0;
            int numOfHetero = 0;
            foreach (DataRow domainInterfaceRow in entryDomainInterfaceRows)
            {
                if (domainInterfaceRow["IsReversed"].ToString() == "1")
                {
                    asymChain1 = domainInterfaceRow["AsymChain2"].ToString().TrimEnd();
                    asymChain2 = domainInterfaceRow["AsymChain1"].ToString().TrimEnd();
                }
                else
                {
                    asymChain1 = domainInterfaceRow["AsymChain1"].ToString().TrimEnd();
                    asymChain2 = domainInterfaceRow["AsymChain2"].ToString().TrimEnd();
                }
                entityId1 = GetAsymChainEntityId(asymChain1, chainDomainTable);
                if (!entityPfamArchHash.ContainsKey(entityId1))
                {
                    ProtCidSettings.logWriter.WriteLine(relSeqId + " " + pdbId + domainInterfaceRow["DomainInterfaceID"].ToString() +
                        " entityID1 = " + entityId1 + " not in entityPfamArchHash. Should fix.");
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
                chainPfamArch1 = entityPfamArchHash[entityId1];
                if (asymChain1 == asymChain2)
                {
                    entityId2 = entityId1;
                    chainPfamArch2 = chainPfamArch1;
                }
                else
                {
                    entityId2 = GetAsymChainEntityId(asymChain2, chainDomainTable);
                    if (!entityPfamArchHash.ContainsKey(entityId2))
                    {
                        ProtCidSettings.logWriter.WriteLine(relSeqId + " " + pdbId + domainInterfaceRow["DomainInterfaceID"].ToString() +
                        " entityID2 = " + entityId2 + " not in entityPfamArchHash. Should fix");
                        ProtCidSettings.logWriter.Flush();
                        continue;
                    }
                    chainPfamArch2 = entityPfamArchHash[entityId2];
                }
                if (entityUnpCodeDict.ContainsKey (entityId1))
                {
                    unpCodes1 = entityUnpCodeDict[entityId1];
                }
                else
                {
                    unpCodes1 = "-";
                }
                if (entityUnpCodeDict.ContainsKey(entityId2))
                {
                    unpCodes2 = entityUnpCodeDict[entityId2];
                }
                else
                {
                    unpCodes2 = "-";
                }
                chainPfamArchRelation = chainPfamArch1 + ";" + chainPfamArch2;
                unpCodesRelation = unpCodes1 + ";" + unpCodes2;
                if (string.Compare (chainPfamArch1, chainPfamArch2) > 0)
                {
                    chainPfamArchRelation = chainPfamArch2 + ";" + chainPfamArch1;
                    unpCodesRelation = unpCodes2 + ";" + unpCodes1;
                }
                
                if (! chainArchUnpCodeHash.ContainsKey (chainPfamArchRelation ))
                {
                    chainArchUnpCodeHash.Add(chainPfamArchRelation, unpCodesRelation);
                }
                if (chainArchNumOfInterfacesHash.ContainsKey(chainPfamArchRelation))
                {
                    int count = chainArchNumOfInterfacesHash[chainPfamArchRelation];
                    count++;
                    chainArchNumOfInterfacesHash[chainPfamArchRelation] = count;
                }
                else
                {
                    chainArchNumOfInterfacesHash.Add(chainPfamArchRelation, 1);
                }
                if (entityId1 != entityId2)
                {
                    if (chainArchHeteroHash.ContainsKey(chainPfamArchRelation))
                    {
                        int count = chainArchHeteroHash[chainPfamArchRelation];
                        count++;
                        chainArchHeteroHash[chainPfamArchRelation] = count;
                    }
                    else
                    {
                        chainArchHeteroHash.Add(chainPfamArchRelation, 1);
                    }
                }
                chainInterfaceId = Convert.ToInt32(domainInterfaceRow["InterfaceID"].ToString());
                if (chainInterfaceId == 0)
                {
                    if (chainArchIntraHash.ContainsKey(chainPfamArchRelation))
                    {
                        int count = chainArchIntraHash[chainPfamArchRelation];
                        count++;
                        chainArchIntraHash[chainPfamArchRelation] = count;
                    }
                    else
                    {
                        chainArchIntraHash.Add(chainPfamArchRelation, 1);
                    }
                }
            }
            foreach (string chainPfamArchRel in chainArchNumOfInterfacesHash.Keys)
            {
                string[] chainPfamArchFields = chainPfamArchRel.Split(';');
                int numOfInterfaces = chainArchNumOfInterfacesHash[chainPfamArchRel];
                DataRow dataRow = chainArchStatInfoTable.NewRow();
                dataRow["RelSeqID"] = relSeqId;
                dataRow["PdbID"] = pdbId;
                dataRow["ChainArch1"] = chainPfamArchFields[0];
                dataRow["ChainArch2"] = chainPfamArchFields[1];
                if (chainArchIntraHash.ContainsKey(chainPfamArchRel))
                {
                    numOfIntra = chainArchIntraHash[chainPfamArchRel];
                }
                else
                {
                    numOfIntra = 0;
                }
                if (chainArchHeteroHash.ContainsKey(chainPfamArchRel))
                {
                    numOfHetero = chainArchHeteroHash[chainPfamArchRel];
                }
                else
                {
                    numOfHetero = 0;
                }
                dataRow["NumOfIntra"] = numOfIntra;
                dataRow["NumOfHetero"] = numOfHetero;
                dataRow["NumOfInteractions"] = numOfInterfaces;
                dataRow["NumOfHomo"] = numOfInterfaces - numOfIntra - numOfHetero;
                dataRow["UnpCode1"] = "-";
                dataRow["UnpCode2"] = "-";
                if (chainArchUnpCodeHash.ContainsKey (chainPfamArchRel ))
                {
                    string[] unpCodeFields = chainArchUnpCodeHash[chainPfamArchRel].Split(';');
                    dataRow["UnpCode1"] = unpCodeFields[0];
                    dataRow["UnpCode2"] = unpCodeFields[1];
                }
                dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, dataRow);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pfamId"></param>
        /// <param name="pfamPepInterfaceTable"></param>
        /// <param name="chainArchStatInfoTable"></param>
        private void GetPeptideChainArchStatInfo(string pdbId, string pfamId, DataTable pfamPepInterfaceTable, DataTable chainArchStatInfoTable)
        {
            DataRow[] entryPeptideInterfaceRows = pfamPepInterfaceTable.Select(string.Format("PfamID = '{0}' AND PdbID = '{1}'", pfamId, pdbId));
            Dictionary<int, string> entityPfamArchHash = pfamArch.GetEntryEntityGroupPfamArchHash(pdbId);
            Dictionary<int, string> entityUnpCodeDict = GetEntryEntityUnpCodeDict(pdbId);
            DataTable chainDomainTable = GetEntryChainDomains(pdbId);
            Dictionary<string, int> chainArchNumOfInterfacesHash = new Dictionary<string, int>();
            Dictionary<string, int> chainArchIntraHash = new Dictionary<string, int>();
            Dictionary<string, int> chainArchHeteroHash = new Dictionary<string, int>();
            Dictionary<string, string> chainArchUnpCodeHash = new Dictionary<string, string>();
            string chainPfamArch = "";
            string chainUnpCodes = "";
            int entityId = 0;
            int pepEntityId = 0;
            string asymChain = "";
            string pepAsymChain = "";
            string chainPfamArchRelation = "";
            int numOfIntra = 0;
            int numOfHetero = 0;
            int numOfInterfaces = 0;
            foreach (DataRow pepInterfaceRow in entryPeptideInterfaceRows)
            {
                asymChain = pepInterfaceRow["AsymChain"].ToString().TrimEnd();
                pepAsymChain = pepInterfaceRow["PepAsymChain"].ToString().TrimEnd();
                entityId = GetAsymChainEntityId(asymChain, chainDomainTable);
                pepEntityId = GetAsymChainEntityId(pepAsymChain, chainDomainTable);
                if (!entityPfamArchHash.ContainsKey(entityId))
                {
                    ProtCidSettings.logWriter.WriteLine(pfamId + " " + pdbId + pepInterfaceRow["DomainInterfaceID"].ToString() +
                        " entityID1 = " + entityId + " not in entityPfamArchHash. Should fix.");
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
                chainPfamArch = entityPfamArchHash[entityId];
                if (entityUnpCodeDict.ContainsKey (entityId))
                {
                    chainUnpCodes = entityUnpCodeDict[entityId];
                }
                else
                {
                    chainUnpCodes = "-";
                }
                if (entityUnpCodeDict.ContainsKey(pepEntityId))
                {
                    chainUnpCodes += (";" + entityUnpCodeDict[pepEntityId]);
                }
                else
                {
                    chainUnpCodes += ";-";
                }

                chainPfamArchRelation = chainPfamArch + ";peptide";
                if (!chainArchUnpCodeHash.ContainsKey(chainPfamArchRelation))
                {
                    chainArchUnpCodeHash.Add(chainPfamArchRelation, chainUnpCodes);
                }
                if (chainArchNumOfInterfacesHash.ContainsKey(chainPfamArchRelation))
                {
                    int count = chainArchNumOfInterfacesHash[chainPfamArchRelation];
                    count++;
                    chainArchNumOfInterfacesHash[chainPfamArchRelation] = count;
                }
                else
                {
                    chainArchNumOfInterfacesHash.Add(chainPfamArchRelation, 1);
                }

                if (chainArchHeteroHash.ContainsKey(chainPfamArchRelation))
                {
                    int count = chainArchHeteroHash[chainPfamArchRelation];
                    count++;
                    chainArchHeteroHash[chainPfamArchRelation] = count;
                }
                else
                {
                    chainArchHeteroHash.Add(chainPfamArchRelation, 1);
                }
            }
            foreach (string chainPfamArchRel in chainArchNumOfInterfacesHash.Keys)
            {
                string[] chainPfamArchFields = chainPfamArchRel.Split(';');
                numOfInterfaces = chainArchNumOfInterfacesHash[chainPfamArchRel];
                DataRow dataRow = chainArchStatInfoTable.NewRow();
                dataRow["RelSeqID"] = -1;
                dataRow["PdbID"] = pdbId;
                dataRow["ChainArch1"] = chainPfamArchFields[0];
                dataRow["ChainArch2"] = chainPfamArchFields[1];
                if (chainArchIntraHash.ContainsKey(chainPfamArchRel))
                {
                    numOfIntra = chainArchIntraHash[chainPfamArchRel];
                }
                else
                {
                    numOfIntra = 0;
                }
                if (chainArchHeteroHash.ContainsKey(chainPfamArchRel))
                {
                    numOfHetero = chainArchHeteroHash[chainPfamArchRel];
                }
                else
                {
                    numOfHetero = 0;
                }
                dataRow["NumOfIntra"] = numOfIntra;
                dataRow["NumOfHetero"] = numOfHetero;
                dataRow["NumOfInteractions"] = numOfInterfaces;
                dataRow["NumOfHomo"] = numOfInterfaces - numOfIntra - numOfHetero;
                if (chainArchUnpCodeHash.ContainsKey (chainPfamArchRel))
                {
                    string[] unpCodesFields = chainArchUnpCodeHash[chainPfamArchRel].Split (';');
                    dataRow["UnpCode1"] = unpCodesFields[0];
                    dataRow["UnpCode2"] = unpCodesFields[1];
                }
                else
                {
                    dataRow["UnpCode1"] = "-";
                    dataRow["UnpCode2"] = "-";
                }
                dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, dataRow);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain"></param>
        /// <param name="chainDomainTable"></param>
        /// <returns></returns>
        private int GetAsymChainEntityId(string asymChain, DataTable chainDomainTable)
        {
            DataRow[] entityRows = chainDomainTable.Select(string.Format("AsymChain = '{0}'", asymChain));
            if (entityRows.Length == 0)
            {
                string orgAsymChain = RemoveDigitsFromAsymChain(asymChain);
                entityRows = chainDomainTable.Select(string.Format("AsymChain = '{0}'", orgAsymChain));
            }
            if (entityRows.Length > 0)
            {
                return Convert.ToInt32(entityRows[0]["EntityID"].ToString());
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private string RemoveDigitsFromAsymChain(string asymChain)
        {
            string orgAsymChain = "";
            foreach (char ch in asymChain)
            {
                if (!char.IsDigit(ch))
                {
                    orgAsymChain += ch.ToString();
                }
            }
            return orgAsymChain;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryChainDomains(string pdbId)
        {
            string queryString = string.Format("Select * From PdbPfamChain Where PdbId = '{0}';", pdbId);
            DataTable chainDomainTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            return chainDomainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<int, string> GetEntryEntityPfamArchHash(string pdbId)
        {
            string queryString = string.Format("Select EntityId, SupPfamArch From PfamEntityPfamArch Where PdbID = '{0}';", pdbId);
            DataTable entityPfamArchTable = ProtCidSettings.pdbfamQuery.Query(queryString);

            int entityId = 0;
            string entityPfamArch = "";
            Dictionary<int, string> entityPfamArchHash = new Dictionary<int, string>();
            foreach (DataRow entityPfamArchRow in entityPfamArchTable.Rows)
            {
                entityId = Convert.ToInt32(entityPfamArchRow["EntityID"].ToString());
                entityPfamArch = entityPfamArchRow["SupPfamArch"].ToString().TrimEnd();
                entityPfamArchHash.Add(entityId, entityPfamArch);
            }
            return entityPfamArchHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private DataTable GetRelationDomainInterfaces(int relSeqId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable relationDomainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);

            queryString = string.Format("Select RelSeqID, PfamID, PepPfamID, PdbID, InterfaceID, DomainInterfaceID, " +
                " DomainID As DomainID1, AsymChain As AsymChain1, ChainDomainID As ChainDomainID1, " +
                " PepDomainID As DomainID2, PepAsymChain As AsymChain2, PepChainDomainID As ChainDomainID2, SurfaceArea " +
                " From PfamPeptideInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable relationPepInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);

            SetIsReversedColumnInPeptideInterfaces(relationPepInterfaceTable, relSeqId);

            ParseHelper.AddNewTableToExistTable(relationPepInterfaceTable, ref relationDomainInterfaceTable);

            return relationDomainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private DataTable GetRelationDomainInterfaces(int relSeqId, string pdbId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where RelSeqID = {0} AND PdbID = '{1}';", relSeqId, pdbId);
            DataTable relationDomainInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            
             queryString = string.Format("Select RelSeqID, PfamID, PepPfamID, PdbID, InterfaceID, DomainInterfaceID, " + 
                 " DomainID As DomainID1, AsymChain As AsymChain1, ChainDomainID As ChainDomainID1, " + 
                 " PepDomainID As DomainID2, PepAsymChain As AsymChain2, PepChainDomainID As ChainDomainID2, SurfaceArea " +
                 " From PfamPeptideInterfaces Where RelSeqID = {0} AND PdbID = '{1}';", relSeqId, pdbId);
            DataTable relationPepInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);

            SetIsReversedColumnInPeptideInterfaces(relationPepInterfaceTable, relSeqId);

            ParseHelper.AddNewTableToExistTable(relationPepInterfaceTable, ref relationDomainInterfaceTable);

            return relationDomainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pepInterfaceTable"></param>
        /// <param name="relSeqId"></param>
        private void SetIsReversedColumnInPeptideInterfaces (DataTable pepInterfaceTable, int relSeqId)
        {
            if (! pepInterfaceTable.Columns.Contains ("IsReversed"))
            {
                DataColumn pepInterfaceCol = new DataColumn("IsReversed");
                pepInterfaceCol.DefaultValue = '0';
                pepInterfaceTable.Columns.Add(pepInterfaceCol);
            }
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable pfamPairTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pfamId1 = "";
            string pfamId2 = "";
            string pfamId = "";
            string pepPfamId = "";
            if (pfamPairTable.Rows.Count > 0)
            {
                pfamId1 = pfamPairTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
                pfamId2 = pfamPairTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            }
            for (int i = 0; i < pepInterfaceTable.Rows.Count; i ++)
            {
                pfamId = pepInterfaceTable.Rows[i]["PfamID"].ToString().TrimEnd();
                pepPfamId = pepInterfaceTable.Rows[i]["PepPfamID"].ToString().TrimEnd();
                if (pfamId1 == pepPfamId && pfamId2 == pfamId)
                {
                    pepInterfaceTable.Rows[i]["IsReversed"] = '1';
                }
            }
            pepInterfaceTable.Columns.Remove("PfamID");
            pepInterfaceTable.Columns.Remove("PepPfamID");
            pepInterfaceTable.AcceptChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relationDomainInterfaceTable"></param>
        /// <returns></returns>
        private string[] GetRelationEntries(DataTable relationDomainInterfaceTable)
        {
            List<string> entryList = new List<string>();
            string pdbId = "";
            foreach (DataRow entryRow in relationDomainInterfaceTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            return entryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<int, string> GetEntryEntityUnpCodeDict (string pdbId)
        {
            string queryString = string.Format("Select Distinct EntityID, DbCode From PdbDbRefSifts Where PdbID = '{0}' AND DbName = 'UNP' Order By EntityID, RefID;", pdbId);
            DataTable entityUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (entityUnpTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct EntityID, DbCode From PdbDbRefXml Where PdbID = '{0}' AND DbName = 'UNP' Order By EntityID, RefID;", pdbId);
                entityUnpTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            }
            Dictionary<int, List<string>> entityUnpListDict = new Dictionary<int, List<string>>();
            Dictionary<int, string> entityUnpDict = new Dictionary<int, string>();
            int entityId = 0;
            string unpCode = "";
            foreach (DataRow unpRow in entityUnpTable.Rows)
            {
                entityId = Convert.ToInt32(unpRow["EntityID"].ToString ());
                unpCode = unpRow["DbCode"].ToString().TrimEnd();
                if (entityUnpListDict.ContainsKey(entityId))
                {
                    entityUnpListDict[entityId].Add (unpCode);
                }
                else
                {
                    List<string> unpList = new List<string> ();
                    unpList.Add (unpCode);
                    entityUnpListDict.Add(entityId, unpList);
                }
            }
            string entityUnpCodes = "";
            foreach (int lsEntityId  in entityUnpListDict.Keys)
            {
                entityUnpCodes = "";
                if (entityUnpListDict[lsEntityId].Count == 1)
                {
                    entityUnpCodes = entityUnpListDict[lsEntityId][0];
                }
                else
                {
                    foreach (string lsUnp in entityUnpListDict[lsEntityId])
                    {
                        entityUnpCodes += ("(" + lsUnp + ")_");
                    }
                    entityUnpCodes = entityUnpCodes.TrimEnd('_');
                }
                entityUnpDict.Add(lsEntityId, entityUnpCodes);
            }
            return entityUnpDict;
        }
        #endregion

        #region tables
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable CreateChainArchStatInfoTable(bool isUpdate)
        {
            string[] statInfoColumns = { "RelSeqID", "PdbID", "ChainArch1", "ChainArch2", "NumOfIntra", "NumOfHetero", "NumOfInteractions", "NumOfHomo", "UnpCode1", "UnpCode2"};
            DataTable chainArchStatInfoTable = new DataTable(chainArchTableName);
            foreach (string infoCol in statInfoColumns)
            {
                chainArchStatInfoTable.Columns.Add(new DataColumn(infoCol));
            }

            if (!isUpdate)
            {
                DbCreator dbCreat = new DbCreator();
                string createTableString = "CREATE TABLE " + chainArchTableName + " ( " +
                    " RelSeqID INTEGER NOT NULL, " +
                    " PdbID CHAR(4) NOT NULL, " +
                    " ChainArch1 VARCHAR(1200) NOT NULL,  " +
                    " ChainArch2 VARCHAR(1200) NOT NULL, " +
                    " NumOfIntra INTEGER NOT NULL, " +
                    " NumOfHetero INTEGER NOT NULL, " +
                    " NumOfHomo Integer NOT NULL, " +
                    " NumOfInteractions INTEGER NOT NULL, " +
                    " UnpCode1 VARCHAR(524), " +
                    " UnpCode2 VARCHAR(524)" +
                " );";
                dbCreat.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, chainArchStatInfoTable.TableName);
                string createIndexString = "CREATE INDEX DomainChainRel_idx1 ON " + chainArchStatInfoTable.TableName + "(RelSeqID, PdbID);";
                dbCreat.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, chainArchStatInfoTable.TableName);
            }
            return chainArchStatInfoTable;
        }
        #endregion

        #region add unpcodes to table -- debug
        /// <summary>
        /// 
        /// </summary>
        public void AddMissingPfamPfamEntryChainArchs()
        {
       //     string[] updateEntries = { "1aoi" };
            string queryString = "Select Distinct PdbID From PfamDomainCHainArchRelation Where ChainArch1 = '-' OR ChainArch2 = '-';";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query (queryString);
            string[] updateEntries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                updateEntries[count]= entryRow["PdbID"].ToString ();
                count ++;
            }
            UpdatePfamDomainSumTables(updateEntries);
           
        /*    
            bool isUpdate = true;
            DataTable entryChainArchStatInfoTable = CreateChainArchStatInfoTable(isUpdate);

            string queryString = "Select RelSeqID From PfamDomainFamilyRelation Where FamilyCode2 <> 'peptide';";
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);

            queryString = "Select Distinct RelSeqID From PfamDomainChainArchRelation;";
            DataTable chainArchTable = ProtCidSettings.protcidQuery.Query(queryString);


            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = relSeqIdTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = relSeqIdTable.Rows.Count;

            int relSeqId = 0;
            foreach (DataRow relSeqRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqRow["RelSeqID"].ToString());

                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                DataRow[] chainArchRows = chainArchTable.Select(string.Format ("RelSeqID = '{0}'", relSeqId));
                if (chainArchRows.Length > 0)
                {
                    continue;
                }

                DataTable relationDomainInterfaceTable = GetRelationDomainInterfaces(relSeqId);
                string[] entries = GetRelationEntries(relationDomainInterfaceTable);
                foreach (string pdbId in entries)
                {
                    GetChainArchStatInfo(pdbId, relSeqId, relationDomainInterfaceTable, entryChainArchStatInfoTable);
                }
            }
        */
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        public void GetEntriesWithInConsistentPfams ()
        {
            string queryString = "Select Distinct PdbID, DomainID From PfamPeptideInterfaces;";
            DataTable protDomainTable = ProtCidSettings.protcidQuery.Query(queryString);
            string pdbId = "";
            long domainId = 0;
            StreamWriter domainEntryWriter = new StreamWriter("DomainInConsistentPeptideEntries.txt");
            foreach (DataRow domainRow in protDomainTable.Rows)
            {
                pdbId = domainRow["PdbID"].ToString();
                domainId = Convert.ToInt64(domainRow["DomainID"].ToString ());
                queryString = string.Format("Select Pfam_ID, DomainID From PdbPfam Where PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);
                DataTable pfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                if (pfamTable.Rows.Count > 0)
                {
                    continue;
                }
                domainEntryWriter.WriteLine(pdbId + domainId);
            }

            queryString = "Select Distinct PdbID, PepDomainID From PfamPeptideInterfaces Where PepDomainID > 0;";
            DataTable pepDomainTable = ProtCidSettings.protcidQuery.Query(queryString);
            domainEntryWriter.WriteLine("PepDomainID");
            foreach (DataRow domainRow in pepDomainTable.Rows)
            {
                pdbId = domainRow["PdbID"].ToString();
                domainId = Convert.ToInt64(domainRow["PepDomainID"].ToString());
                queryString = string.Format("Select Pfam_ID, DomainID From PdbPfam Where PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);
                DataTable pfamTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                if (pfamTable.Rows.Count > 0)
                {
                    continue;
                }
                domainEntryWriter.WriteLine(pdbId + domainId);
            }
            domainEntryWriter.Close();
        }
 

        public void ReformatEntityUnpCodesToTable ()
        {
            DbUpdate dbUpdate = new DbUpdate(ProtCidSettings.protcidDbConnection);
            string queryString = "Select Distinct UnpCode1 As UnpCode From PfamDomainChainArchRelation Where UnpCode1 <> '-';";
            DataTable unpCodeTable1 = ProtCidSettings.protcidQuery.Query(queryString);
            string orgUnpCodes = "";
            string newUnpCodes = "";
            string updateString = "";
            foreach (DataRow unpCodeRow  in unpCodeTable1.Rows)
            {
                orgUnpCodes = unpCodeRow["UnpCode"].ToString().TrimEnd();
                newUnpCodes = ReformatUnpCodes(orgUnpCodes);
                if (orgUnpCodes != newUnpCodes)
                {
                    updateString = string.Format("Update PfamDomainChainArchRelation Set UnpCode1 = '{0}' Where UnpCode1 = '{1}';", newUnpCodes, orgUnpCodes);
                    dbUpdate.Update(updateString);
                }
            }

            queryString = "Select Distinct UnpCode2 As UnpCode From PfamDomainChainArchRelation Where UnpCode2 <> '-';";
            DataTable unpCodeTable2 = ProtCidSettings.protcidQuery.Query(queryString);
            foreach (DataRow unpCodeRow in unpCodeTable2.Rows)
            {
                orgUnpCodes = unpCodeRow["UnpCode"].ToString().TrimEnd();
                newUnpCodes = ReformatUnpCodes(orgUnpCodes);
                if (orgUnpCodes != newUnpCodes)
                {
                    updateString = string.Format("Update PfamDomainChainArchRelation Set UnpCode2 = '{0}' Where UnpCode2 = '{1}';", newUnpCodes, orgUnpCodes);
                    dbUpdate.Update(updateString);
                }
            }
        }

        private string ReformatUnpCodes (string orgUnpCodes)
        {
            string newUnpCodes = "";
            string[] fields = orgUnpCodes.Split('_');
            if (fields.Length == 2)
            {
                newUnpCodes = orgUnpCodes;
            }
            else
            {
                for (int i = 0; i < fields.Length; i += 2)
                {
                    newUnpCodes += ("(" + fields[i] + "_" + fields[i + 1] + ")_");
                }
                newUnpCodes = newUnpCodes.TrimEnd('_');
            }
            return newUnpCodes;
        }

        public void AddEntityUnpCodesToTableFromXml ()
        {
            string[] noSiftsEntries = GetEntriesNoDbRefSifts();
            Dictionary<int, List<string>> updateRelEntryDict = GetUpdateRelEntryDict(noSiftsEntries);
            Dictionary<string, List<string>> updatePfamPepEntryDict = GetUpdatePepPfamEntryDict(noSiftsEntries);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update Pfam chain Arch relations for protcid web site.");
            ProtCidSettings.logWriter.WriteLine("Update Pfam chain Arch relations for protcid web site.");

            UpdateDomainInterfaceChainArchs(noSiftsEntries, updateRelEntryDict, updatePfamPepEntryDict);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetEntriesNoDbRefSifts()
        {
            string queryString = "Select Distinct PdbID From PdbDbRefSifts Where DbName = 'UNP';";
            DataTable siftsUnpEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> siftsUnpEntryList = new List<string>();
            foreach (DataRow entryRow in siftsUnpEntryTable.Rows)
            {
                siftsUnpEntryList.Add(entryRow["PdbID"].ToString());
            }
            siftsUnpEntryList.Sort();
            queryString = "Select Distinct PdbID From PdbDbRefXml Where DbName = 'UNP';";
            DataTable xmlUnpEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> noSiftsUnpEntryList = new List<string>();
            string pdbId = "";
            foreach (DataRow entryRow in xmlUnpEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (siftsUnpEntryList.BinarySearch(pdbId) < 0)
                {
                    noSiftsUnpEntryList.Add(pdbId);
                }
            }
            return noSiftsUnpEntryList.ToArray();
        }
        #endregion
        #endregion

        #region IPfam in the PDB
        #region build
        /// <summary>
        /// 
        /// </summary>
        public void BuildIPfamInPdbMetaData()
        {
            SetIPfamInPdbMetaData ();
            AddIPfamPeptideInPdbMetaData();
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetIPfamInPdbMetaData()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            bool isUpdate = false;
            DataTable ipfamInfoTable = CreatePfamPairMetaDataTable(ipfamInPdbTableName, isUpdate);
            string queryString = "Select * From PfamDomainFamilyRelation;";
            DataTable pfamRelTable = ProtCidSettings.protcidQuery.Query(queryString);

            ProtCidSettings.progressInfo.totalStepNum = pfamRelTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = pfamRelTable.Rows.Count;

            int relSeqId = 0;
            string pfamId1 = "";
            string pfamAccession1 = "";
            string pfamId2 = "";
            string pfamAccession2 = "";
            int[] numOfEntries = null;
            int[] numOfEntriesIPfam = null;
            int numOfEntriesSameChain = 0;
            Dictionary<string, string> pfamIdPfamAccHash = new Dictionary<string, string>();
            foreach (DataRow pfamRelRow in pfamRelTable.Rows)
            {
                relSeqId = Convert.ToInt32(pfamRelRow["RelSeqID"].ToString());

                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                DataRow ipfamRow = ipfamInfoTable.NewRow();
                ipfamRow["RelSeqID"] = relSeqId;
                pfamId1 = pfamRelRow["FamilyCode1"].ToString().TrimEnd();
                pfamAccession1 = GetPfamAccessionFromPfamID(pfamId1, pfamIdPfamAccHash);
                ipfamRow["PfamID1"] = pfamId1;
                ipfamRow["PfamAcc1"] = pfamAccession1;
                pfamId2 = pfamRelRow["FamilyCode2"].ToString().TrimEnd();
                pfamAccession2 = GetPfamAccessionFromPfamID(pfamId2, pfamIdPfamAccHash);
                ipfamRow["PfamID2"] = pfamId2;
                ipfamRow["PfamAcc2"] = pfamAccession2;
                numOfEntriesIPfam = GetNumOfEntriesWithIPfam(relSeqId);
                numOfEntries = GetNumOfEntriesWithIPfamContent(pfamId1, pfamId2);
                // for possible intra-chain pfam-pfam interactions
                numOfEntriesSameChain = GetNumOfEntriesWithPfamsSameChain(pfamId1, pfamId2);
                ipfamRow["NumEntries"] = numOfEntries[0];
                ipfamRow["NumEntriesIPfam"] = numOfEntriesIPfam[0];
                ipfamRow["NumCFs"] = numOfEntries[1];
                ipfamRow["NumCFsIPfam"] = numOfEntriesIPfam[1];
                ipfamRow["NumEntriesSameChain"] = numOfEntriesSameChain;
                //    ipfamInfoTable.Rows.Add(ipfamRow);
                dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, ipfamRow);
            }
            //     dbInsert.InsertDataIntoDBtables(ipfamInfoTable);
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        public void AddIPfamPeptideInPdbMetaData()
        {
            string queryString = "Select Distinct PfamID From PfamPeptideInterfaces;";
            DataTable pepPfamTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] pepPfams = new string[pepPfamTable.Rows.Count];
            int count = 0;
            foreach (DataRow pfamRow in pepPfamTable.Rows)
            {
                pepPfams[count] = pfamRow["PfamID"].ToString().TrimEnd();
                count++;
            }
            queryString = "Select Distinct PfamID1 From IPfamInPdb Where PfamID2 = 'peptide'";
            DataTable metaPepPfamTable = ProtCidSettings.protcidQuery.Query(queryString);
            string metaPfam = "";
            foreach (DataRow pfamRow in metaPepPfamTable.Rows)
            {
                metaPfam = pfamRow["PfamID1"].ToString().TrimEnd();
                if (pepPfams.Contains(metaPfam))
                {
                    continue;
                }
                DeletePfamPeptideMetaData(metaPfam);
            }
            UpdateIPfamPeptideInPdbMetaData(pepPfams);
        }
        #endregion

        #region update
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateRelSeqIds"></param>
        /// <param name="updatePepPfams"></param>
        public void UpdateIPfamInPdbMetaData  (int[] updateRelSeqIds, string[] updatePepPfams)
        {
            UpdateIPfamInPdbMetaData(updateRelSeqIds);
            UpdateIPfamPeptideInPdbMetaData(updatePepPfams);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateRelSeqIds"></param>
        public void UpdateIPfamInPdbMetaData(int[] updateRelSeqIds)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            bool isUpdate = true;
            DataTable ipfamInfoTable = CreatePfamPairMetaDataTable("IPfamInPdb", isUpdate);

            ProtCidSettings.progressInfo.totalStepNum = updateRelSeqIds.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updateRelSeqIds.Length;

            string pfamId1 = "";
            string pfamAccession1 = "";
            string pfamId2 = "";
            string pfamAccession2 = "";
            int[] numOfEntries = null;
            int[] numOfEntriesIPfam = null;
            int numOfEntriesSameChain = 0;
            Dictionary<string, string> pfamIdPfamAccHash = new Dictionary<string, string>();
            //   foreach (DataRow pfamRelRow in pfamRelTable.Rows)
            foreach (int relSeqId in updateRelSeqIds)
            {
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                DeleteIPfamMetaData(relSeqId);

                DataTable pfamRelTable = GetRelationInfo(relSeqId);
                DataRow pfamRelRow = pfamRelTable.Rows[0];

                DataRow ipfamRow = ipfamInfoTable.NewRow();
                ipfamRow["RelSeqID"] = relSeqId;
                pfamId1 = pfamRelRow["FamilyCode1"].ToString().TrimEnd();
                pfamAccession1 = GetPfamAccessionFromPfamID(pfamId1, pfamIdPfamAccHash);
                ipfamRow["PfamID1"] = pfamId1;
                ipfamRow["PfamAcc1"] = pfamAccession1;
                pfamId2 = pfamRelRow["FamilyCode2"].ToString().TrimEnd();
                pfamAccession2 = GetPfamAccessionFromPfamID(pfamId2, pfamIdPfamAccHash);
                ipfamRow["PfamID2"] = pfamId2;
                ipfamRow["PfamAcc2"] = pfamAccession2;
                numOfEntriesIPfam = GetNumOfEntriesWithIPfam(relSeqId);
                numOfEntries = GetNumOfEntriesWithIPfamContent(pfamId1, pfamId2);
                numOfEntriesSameChain = GetNumOfEntriesWithPfamsSameChain(pfamId1, pfamId2);
                ipfamRow["NumEntries"] = numOfEntries[0];
                ipfamRow["NumEntriesIPfam"] = numOfEntriesIPfam[0];
                ipfamRow["NumCFs"] = numOfEntries[1];
                ipfamRow["NumCFsIPfam"] = numOfEntriesIPfam[1];
                ipfamRow["NumEntriesSameChain"] = numOfEntriesSameChain;
                dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, ipfamRow);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateRelSeqIds"></param>
        public void UpdateIPfamPeptideInPdbMetaData(string[] updatePfams)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            string queryString = "Select First 1 * From " + ipfamInPdbTableName;
            DataTable ipfamInfoTable = ProtCidSettings.protcidQuery.Query(queryString);
            ipfamInfoTable.TableName = ipfamInPdbTableName;
            ipfamInfoTable.Clear();
            DataRow ipfamRow = ipfamInfoTable.NewRow();

            ProtCidSettings.progressInfo.totalStepNum = updatePfams.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updatePfams.Length;

            string pfamAccession = "";
            int[] numOfEntries = null;
            int[] numOfEntriesIPfam = null;
            int numOfEntriesSameChain = 0;
            Dictionary<string, string> pfamIdPfamAccHash = new Dictionary<string, string>();
            foreach (string pfamId in updatePfams)
            {
                ProtCidSettings.progressInfo.currentFileName = pfamId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                DeletePfamPeptideMetaData(pfamId);

                ipfamRow["RelSeqID"] = -1;
                pfamAccession = GetPfamAccessionFromPfamID(pfamId);
                ipfamRow["PfamID1"] = pfamId;
                ipfamRow["PfamAcc1"] = pfamAccession;
                ipfamRow["PfamID2"] = "peptide";
                ipfamRow["PfamAcc2"] = "peptide";
                numOfEntriesIPfam = GetNumOfPeptideEntriesIPfam(pfamId);
                numOfEntries = GetNumOfEntriesWithPfamPeptide(pfamId);
                numOfEntriesSameChain = 0;
                ipfamRow["NumEntries"] = numOfEntries[0];
                ipfamRow["NumEntriesIPfam"] = numOfEntriesIPfam[0];
                ipfamRow["NumCFs"] = numOfEntries[1];
                ipfamRow["NumCFsIPfam"] = numOfEntriesIPfam[1];
                ipfamRow["NumEntriesSameChain"] = numOfEntriesSameChain; // should not be same chains
                dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, ipfamRow);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
        #endregion

        #region pfam-pfam info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="parsedPfamPairList"></param>
        /// <returns></returns>
        private string[] GetEntryPfamPairs(string pdbId, List<string> parsedPfamPairList)
        {
            string[] entryPfams = GetEntryPfams(pdbId);
            string pfamPair = "";
            List<string> unparsedPfamPairList = new List<string>();
            for (int i = 0; i < entryPfams.Length; i++)
            {
                for (int j = i; j < entryPfams.Length; j++)
                {
                    pfamPair = entryPfams[i] + ";" + entryPfams[j];
                    if (parsedPfamPairList.Contains(pfamPair))
                    {
                        continue;
                    }
                    unparsedPfamPairList.Add(pfamPair);
                }
            }
            return unparsedPfamPairList.ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryPfams(string pdbId)
        {
            string queryString = string.Format("Select Distinct Pfam_ID From Pdbpfam Where PdbID = '{0}';", pdbId);
            DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] entryPfams = new string[pfamIdTable.Rows.Count];
            int count = 0;

            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                entryPfams[count] = pfamIdRow["Pfam_ID"].ToString().TrimEnd();
                count++;
            }
            return entryPfams;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId1"></param>
        /// <param name="pfamId2"></param>
        /// <returns></returns>
        private int GetRelSeqId(string pfamId1, string pfamId2)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation " +
                " Where (FamilyCode1 = '{0}' AND FamilyCode2 = '{1}') OR " +
                " (FamilyCode2 = '{0}' AND FamilyCode1 = '{1}');", pfamId1, pfamId2);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query(queryString);
            int relSeqId = -1;
            if (relSeqIdTable.Rows.Count > 0)
            {
                relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString());
            }
            return relSeqId;
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private DataTable GetRelationInfo(int relSeqId)
        {
            string queryString = string.Format("Select * From  PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable relationTable = ProtCidSettings.protcidQuery.Query(queryString);
            return relationTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        public void DeleteIPfamMetaData(int relSeqId)
        {
            string deleteString = string.Format("Delete From IPfamInPDb Where RelSeqID = {0};", relSeqId);
            protcidUpdate.Delete(deleteString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable CreatePfamPairMetaDataTable(string tableName, bool isUpdate)
        {
            string[] pfamPairColumns = { "RelSeqID", "PfamID1", "PfamID2", "PfamAcc1", "PfamAcc2", "NumEntriesIPfam", "NumEntries", "NumEntriesSameChain", 
                                       "NumCFs", "NumCFsIPfam"};
            DataTable pfamPairInfoTable = new DataTable(tableName);
            foreach (string ipfamCol in pfamPairColumns)
            {
                pfamPairInfoTable.Columns.Add(new DataColumn(ipfamCol));
            }

            if (!isUpdate)
            {
                DbCreator dbCreat = new DbCreator();
                string createTableString = "CREATE TABLE " + tableName + " ( " +
                    " RelSeqID INTEGER NOT NULL, " +
                    " PfamID1 VARCHAR(40) NOT NULL, " +
                    " PfamID2 VARCHAR(40) NOT NULL, " +
                    " PfamAcc1 VARCHAR(10) NOT NULL, " +
                    " PfamAcc2 VARCHAR(10) NOT NULL, " +
                    " NumEntriesIPfam INTEGER NOT NULL, " +
                    " NumEntriesSameChain Integer NOT NULL, " +
                    " NumCFs INTEGER NOT NULL, " +
                    " NumCFsIPfam INTEGER NOT NULL, " +
                    " NumEntries INTEGER NOT NULL);";
                dbCreat.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);
                string createIndexString = "CREATE INDEX " + tableName + "_idx1 ON " + tableName + "(RelSeqID);";
                dbCreat.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
                createIndexString = "CREATE INDEX " + tableName + "_idx2 ON " + tableName + "(PfamID1, PfamID2);";
                dbCreat.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
                createIndexString = "CREATE INDEX " + tableName + "_idx3 ON " + tableName + "(PfamAcc1, PfamAcc2);";
                dbCreat.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
            }
            return pfamPairInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pfamIdPfamAccHash"></param>
        /// <returns></returns>
        public string GetPfamAccessionFromPfamID(string pfamId, Dictionary<string, string> pfamIdPfamAccHash)
        {
            string pfamAccession = "";
            if (pfamIdPfamAccHash.ContainsKey(pfamId))
            {
                pfamAccession = pfamIdPfamAccHash[pfamId];
            }
            else
            {
                string queryString = string.Format("Select Pfam_Acc From PfamHmm Where Pfam_ID = '{0}';", pfamId);
                DataTable pfamAccTable = ProtCidSettings.pdbfamQuery.Query(queryString);
                if (pfamAccTable.Rows.Count == 0)
                {
                    pfamAccession = pfamId;
                }
                else
                {
                    pfamAccession = pfamAccTable.Rows[0]["Pfam_Acc"].ToString().TrimEnd();
                }
                pfamIdPfamAccHash.Add(pfamId, pfamAccession);
            }
            return pfamAccession;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private int[] GetNumOfEntriesWithIPfam(int relSeqId)
        {
            string[] relEntries = GetRelationEntries(relSeqId);
            int numCfWithIPfam = entryCfs.GetNumberOfCFs(relEntries);
            int[] numbersOfEntryCf = new int[2];
            numbersOfEntryCf[0] = relEntries.Length;
            numbersOfEntryCf[1] = numCfWithIPfam;
            return numbersOfEntryCf;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetRelationEntries(int relSeqId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamDomainInterfaces WHere RelSeqId = {0};", relSeqId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] entries = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return entries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pfamId1"></param>
        /// <param name="pfamId2"></param>
        /// <returns></returns>
        public int[] GetNumOfEntriesWithIPfamContent(string pfamId1, string pfamId2)
        {
            string[] entriesWithIPfamContent = GetEntriesWithIPfamContent(pfamId1, pfamId2);
            int numCfWithIPfamContent = entryCfs.GetNumberOfCFs(entriesWithIPfamContent);
            int[] numbersOfEntryCf = new int[2];
            numbersOfEntryCf[0] = entriesWithIPfamContent.Length;
            numbersOfEntryCf[1] = numCfWithIPfamContent;
            return numbersOfEntryCf;
        }      
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId1"></param>
        /// <param name="pfamId2"></param>
        /// <returns></returns>
        public string[] GetEntriesWithIPfamContent(string pfamId1, string pfamId2)
        {
            if (pfamId1 == pfamId2)
            {
                string[] entriesInIpfam = GetPfamEntries(pfamId1);
                return entriesInIpfam;
            }
            string queryString = string.Format("Select Distinct PdbID From PdbPfam Where Pfam_ID = '{0}';", pfamId1);
            DataTable pfamEntryTable1 = ProtCidSettings.pdbfamQuery.Query(queryString);
            queryString = string.Format("Select Distinct PdbID From PdbPfam Where Pfam_ID = '{0}';", pfamId2);
            DataTable pfamEntryTable2 = ProtCidSettings.pdbfamQuery.Query(queryString);

            string pdbId = "";
            List<string> entryList = new List<string>();
            foreach (DataRow pfamEntryRow in pfamEntryTable1.Rows)
            {
                pdbId = pfamEntryRow["PdbID"].ToString();
                DataRow[] pfamEntryRows2 = pfamEntryTable2.Select(string.Format("PdbID = '{0}'", pdbId));
                if (pfamEntryRows2.Length > 0)
                {
                    entryList.Add(pdbId);
                }
            }
            return entryList.ToArray();
        }

        /// <summary>
        /// the number of entries where a chain contain these two pfams in same chain
        /// that is, there are likely to have intra-chain interactions between these two pfams. 
        /// </summary>
        /// <param name="pfamId1"></param>
        /// <param name="pfamId2"></param>
        /// <returns></returns>
        private int GetNumOfEntriesWithPfamsSameChain(string pfamId1, string pfamId2)
        {
            int numOfEntriesSameChain = 0;

            if (pfamId1 != pfamId2)
            {
                numOfEntriesSameChain = GetNumOfEntriesWithDifPfamsSameChain(pfamId1, pfamId2);
            }
            else
            {
                numOfEntriesSameChain = GetNumOfEntriesWithSamePfamsSameChain(pfamId1);
            }
            return numOfEntriesSameChain;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int GetNumOfEntriesWithSamePfamsSameChain(string pfamId)
        {
            string queryString = string.Format("Select PdbID, EntityID, Count(Distinct DomainID) As DomainCount From PdbPfam Where Pfam_ID  = '{0}' Group By PdbID, EntityID;", pfamId);
            DataTable multiDomainEntityTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> entryWithPfamsInSameChainList = new List<string>();
            string pdbId = "";
            int domainCount = 0;
            foreach (DataRow entityRow in multiDomainEntityTable.Rows)
            {
                domainCount = Convert.ToInt32(entityRow["DomainCount"].ToString());
                if (domainCount > 1)
                {
                    pdbId = entityRow["PdbID"].ToString();
                    if (!entryWithPfamsInSameChainList.Contains(pdbId))
                    {
                        entryWithPfamsInSameChainList.Add(pdbId);
                    }
                }
            }
            return entryWithPfamsInSameChainList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId1"></param>
        /// <param name="pfamId2"></param>
        /// <returns></returns>
        private int GetNumOfEntriesWithDifPfamsSameChain(string pfamId1, string pfamId2)
        {
            string queryString = string.Format("Select Distinct PdbID, EntityID From PdbPfam Where Pfam_ID = '{0}';", pfamId1);
            DataTable entityTable1 = ProtCidSettings.pdbfamQuery.Query(queryString);
            queryString = string.Format("Select Distinct PdbID, EntityID From PdbPfam Where Pfam_ID = '{0}';", pfamId2);
            DataTable entityTable2 = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> entryWithPfamsSameChainList = new List<string>();
            string pdbId = "";
            string entityId = "";
            foreach (DataRow entityRow1 in entityTable1.Rows)
            {
                pdbId = entityRow1["PdbID"].ToString();
                entityId = entityRow1["EntityID"].ToString();
                DataRow[] entityRows2 = entityTable2.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId));
                if (entityRows2.Length > 0)
                {
                    if (!entryWithPfamsSameChainList.Contains(pdbId))
                    {
                        entryWithPfamsSameChainList.Add(pdbId);
                    }
                }
            }
            return entryWithPfamsSameChainList.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetPfamEntries(string pfamId)
        {
            string queryString = string.Format("Select Distinct PdbID From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string[] pfamEntries = new string[pfamEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in pfamEntryTable.Rows)
            {
                pfamEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return pfamEntries;
        }
        #endregion

        #region pfam-peptide
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        private void DeletePfamPeptideMetaData(string pfamId)
        {
            string deleteString = string.Format("Delete From IPfamInPdb Where PfamId1 = '{0}' AND PfamId2 = 'peptide';", pfamId);
            protcidUpdate.Delete(deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetNumOfPeptideEntriesIPfam(string pfamId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamPeptideInterfaces Where PfamID = '{0}';", pfamId);
            DataTable pfamPepEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] pepEntries = new string[pfamPepEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in pfamPepEntryTable.Rows)
            {
                pepEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            int numCfWithIPfam = entryCfs.GetNumberOfCFs(pepEntries);
            int[] pepEntryCfNumbers = new int[2];
            pepEntryCfNumbers[0] = pfamPepEntryTable.Rows.Count;
            pepEntryCfNumbers[1] = numCfWithIPfam;
            return pepEntryCfNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private int[] GetNumOfEntriesWithPfamPeptide(string pfamId)
        {
            string queryString = string.Format("Select Distinct PdbID From PdbPfam Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamPepEntryTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            List<string> pepEntryList = new List<string>();
            string pdbId = "";
            foreach (DataRow entryRow in pfamPepEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (DoesEntryContainPeptides(pdbId))
                {
                    pepEntryList.Add(pdbId);
                }
            }
            int numCfWithPfamPep = entryCfs.GetNumberOfCFs(pepEntryList.ToArray());
            int[] pepEntryCfNumbers = new int[2];
            pepEntryCfNumbers[0] = pepEntryList.Count;
            pepEntryCfNumbers[1] = numCfWithPfamPep;
            return pepEntryCfNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool DoesEntryContainPeptides(string pdbId)
        {
            string queryString = string.Format("Select AsymID, Sequence From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable protSeqTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            string sequence = "";
            foreach (DataRow seqRow in protSeqTable.Rows)
            {
                sequence = seqRow["Sequence"].ToString();
                if (sequence.Length <= ProtCidSettings.peptideLengthCutoff)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pfamIdPfamAccHash"></param>
        /// <returns></returns>
        public string GetPfamAccessionFromPfamID(string pfamId)
        {
            string pfamAccession = "";

            string queryString = string.Format("Select Pfam_Acc From PfamHmm Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamAccTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            if (pfamAccTable.Rows.Count == 0)
            {
                pfamAccession = pfamId;
            }
            else
            {
                pfamAccession = pfamAccTable.Rows[0]["Pfam_Acc"].ToString().TrimEnd();
            }

            return pfamAccession;
        }
        #endregion
        #endregion
    }
}
