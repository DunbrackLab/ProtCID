using System;
using System.Collections.Generic;
using CrystalInterfaceLib.BuIO;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Crystal;
using System.Data;
using System.IO;
using DbLib;
using CrystalInterfaceLib.DomainInterfaces;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces
{
	/// <summary>
	/// Writer two interacted domains into a PDB formatted file
	/// </summary>
	public class DomainInterfaceWriter : InterfaceWriter
    {
        #region member variables
        public DbQuery dbQuery = new DbQuery();
        public DomainInterfaceRetriever domainInterfaceReader = new DomainInterfaceRetriever();
        public DomainAtomsReader atomReader = new DomainAtomsReader();
        public string pfamDomainInterfaceFileDir = "";
        public string pfamDomainFileDir = "";
        private CrystalBuilder crystalBuilder = new CrystalBuilder ();
        #endregion

        public DomainInterfaceWriter()
		{
            pfamDomainInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "pfamDomain");
            pfamDomainFileDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "domainFiles");
        }

        // it is hard for multi-chain domain interface files
        // intra-chain domain interfaces is output when checking the domain interactions
        #region generate domain interface files from chain interface files
        #region public interfaces
        /// <summary>
        /// 
        /// </summary>
        public void WriteDomainInterfaceFiles()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain Interface Files";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Write domain interface files");

            string queryString = "Select RelSeqID From PfamDomainFamilyRelation Order by RelSeqID;";
            DataTable relationTable = ProtCidSettings.protcidQuery.Query( queryString);


            int relSeqId = 0;
            foreach (DataRow relSeqRow in relationTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqRow["RelSeqID"].ToString());
               
                ProtCidSettings.progressInfo.progStrQueue.Enqueue (relSeqId.ToString());

                string[] entriesInRelation = GetEntriesInRelation(relSeqId);

                ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
                ProtCidSettings.progressInfo.totalOperationNum = entriesInRelation.Length;
                ProtCidSettings.progressInfo.totalStepNum = entriesInRelation.Length;

               
                foreach (string pdbId in entriesInRelation)
                {
                    ProtCidSettings.progressInfo.currentFileName = pdbId;
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    ProtCidSettings.progressInfo.currentStepNum++;

                    try
                    {
                        WriteDomainInterfaceFiles(pdbId, relSeqId, false);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId + " " + pdbId + " generate domain interface files errors: " +
                            ex.Message);
                        ProtCidSettings.logWriter.WriteLine(relSeqId + " " + pdbId + " generate domain interface files errors: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIds"></param>
        public void WriteDomainInterfaceFiles(Dictionary<int, string[]> updateRelEntryDict)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain Interface Files";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Write domain interface files");
            ProtCidSettings.progressInfo.totalOperationNum = updateRelEntryDict.Count;
            ProtCidSettings.progressInfo.totalStepNum = updateRelEntryDict.Count;

            List<string> entryList = new List<string> ();

            foreach (int relSeqId in updateRelEntryDict.Keys)
            {
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                foreach (string pdbId in updateRelEntryDict[relSeqId])
                {
                    ProtCidSettings.progressInfo.currentFileName = pdbId;

                    if (!entryList.Contains(pdbId))
                    {
                        entryList.Add(pdbId);
                    }                  
                    try
                    {
                        WriteDomainInterfaceFiles (pdbId, relSeqId, false);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId + " " + pdbId + " " + ex.Message);
                        ProtCidSettings.logWriter.WriteLine(relSeqId + " " + pdbId + " " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public string[] GetEntriesInRelation(int relSeqId)
        {
            string queryString = string.Format("Select Distinct PdbId From PfamDomainInterfaces Where RelSeqID = {0};", 
                relSeqId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] entriesInRelation = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entriesInRelation[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return entriesInRelation;
        }
        #endregion

        #region update
        /// <summary>
        /// 
        /// </summary>
        public void UpdateDomainInterfaceFiles(Dictionary<int, string[]> updateRelEntryDict)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain Interface Files";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Write domain interface files");
        
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Delete domain interface files for the updating entries");
            string[] updateEntries = GetUpdateEntries(updateRelEntryDict);
            DeleteObsDomainInterfaceFiles(updateEntries);

            WriteDomainInterfaceFiles(updateRelEntryDict);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateRelEntryHash"></param>
        /// <returns></returns>
        private string[] GetUpdateEntries(Dictionary<int, string[]> updateRelEntryDict)
        {
            List<string> updateEntryList = new List<string>();
            foreach (int relSeqId in updateRelEntryDict.Keys)
            {
                foreach (string entry in updateRelEntryDict[relSeqId])
                {
                    if (!updateEntryList.Contains(entry))
                    {
                        updateEntryList.Add(entry);
                    }
                }
            }
            string[] updateEntries = new string[updateEntryList.Count];
            updateEntryList.CopyTo(updateEntries);
            return updateEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private void DeleteObsDomainInterfaceFiles(string[] updateEntries)
        {
            string hashFolder = "";
            foreach (string updateEntry in updateEntries)
            {
                hashFolder = Path.Combine(pfamDomainInterfaceFileDir, updateEntry.Substring (1, 2));
                string[] domainInterfaceFiles = Directory.GetFiles(hashFolder, updateEntry + ".*");
                foreach (string domainInterfaceFile in domainInterfaceFiles)
                {
                    File.Delete(domainInterfaceFile);
                }
            }
        }
        #endregion

        #region domain interface file       
        /// <summary>
        /// generate all domain interfaces of an entry
        /// including inter-chain, intra-chain and multi-chain
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="relSeqId"></param>
        /// <param name="deleted"></param>
        public void WriteDomainInterfaceFiles(string pdbId, int relSeqId, bool deleted)
        {
            int[][] difTypeDomainInterfaceIds = GetDifDomainInterfaceIds(relSeqId, pdbId);

            if (deleted)
            {
                DeleteObsoleteDomainInterfaceFiles(pdbId, difTypeDomainInterfaceIds[0]);
                DeleteObsoleteDomainInterfaceFiles(pdbId, difTypeDomainInterfaceIds[1]);
                DeleteObsoleteDomainInterfaceFiles(pdbId, difTypeDomainInterfaceIds[2]);
            }

            DataTable domainDefTable = GetEntryDomainDefTable(pdbId);

            DomainInterface[] interChainDomainInterfaces = domainInterfaceReader.GetInterChainDomainInterfaces(pdbId, relSeqId, difTypeDomainInterfaceIds[1], "cryst");
            DomainInterface[] intraChainDomainInterfaces = domainInterfaceReader.GetIntraChainDomainInterfaces(pdbId, difTypeDomainInterfaceIds[0]);
            DomainInterface[] domainInterfaces = new DomainInterface[interChainDomainInterfaces.Length + intraChainDomainInterfaces.Length];
            Array.Copy(interChainDomainInterfaces, 0, domainInterfaces, 0, interChainDomainInterfaces.Length);
            Array.Copy(intraChainDomainInterfaces, 0, domainInterfaces, interChainDomainInterfaces.Length, intraChainDomainInterfaces.Length);

            string hashDir = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
            if (!Directory.Exists(hashDir))
            {
                Directory.CreateDirectory(hashDir);
            }
            string domainInterfaceFile = "";
            string remark = "";
            foreach (DomainInterface domainInterface in domainInterfaces)
            {
                domainInterfaceFile = Path.Combine(hashDir, pdbId + "_d" + domainInterface.domainInterfaceId + ".cryst");
                if (File.Exists (domainInterfaceFile) || File.Exists (domainInterface + ".gz"))
                {
                    continue;
                }
                remark = FormatRemark(pdbId, domainInterface, domainDefTable);
                WriteDomainInterfaceToFile(domainInterfaceFile, remark, domainInterface.chain1, domainInterface.chain2);
                ParseHelper.ZipPdbFile(domainInterfaceFile);
            }

            try
            {
                // for multiChain domain interface files
                WriteMultiChainDomainInterfaces(pdbId, difTypeDomainInterfaceIds[2]);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() + " " + pdbId + " generate multi-chain domain interface files errors:  " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(relSeqId.ToString() + " " + pdbId + " generate multi-chain domain interface files errors:  " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="relSeqId"></param>
        /// <param name="deleted"></param>
        public void WriteInterChainDomainInterfaceFile(string pdbId, int relSeqId, bool deleted)
        {
            DataTable domainDefTable = GetEntryDomainDefTable(pdbId);
            DomainInterface[] domainInterfaces = null;
            int[] domainInterfaceIds = null;
            if (deleted)
            {
                int[] relDomainInterfaceIds = GetInterDomainInterfaceIds(relSeqId, pdbId);
                DeleteDomainInterfaces(pdbId, relDomainInterfaceIds);
                domainInterfaceIds = relDomainInterfaceIds; 
            }
            else
            {
                domainInterfaceIds = GetDomainInterfaceIDsWithNoFiles(pdbId, relSeqId);
            }
            if (domainInterfaceIds.Length == 0)
            {
                return;
            }
            domainInterfaces = domainInterfaceReader.GetInterChainDomainInterfaces(pdbId, relSeqId, domainInterfaceIds, "cryst");

            string hashDir = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
            if (!Directory.Exists(hashDir))
            {
                Directory.CreateDirectory(hashDir);
            }
            string domainInterfaceFile = "";
            string remark = "";
            foreach (DomainInterface domainInterface in domainInterfaces)
            {
                domainInterfaceFile = Path.Combine(hashDir, pdbId + "_d" + domainInterface.domainInterfaceId + ".cryst");
                remark = FormatRemark(pdbId, domainInterface, domainDefTable);
                WriteDomainInterfaceToFile(domainInterfaceFile, remark, domainInterface.chain1, domainInterface.chain2);
                ParseHelper.ZipPdbFile(domainInterfaceFile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="relSeqId"></param>
        /// <param name="deleted"></param>
        public void WriteIntraChainDomainInterfaceFile(string pdbId, int relSeqId, bool deleted)
        {
            DataTable domainDefTable = GetEntryDomainDefTable(pdbId);
            
            int[] intraDomainInterfaceIds = GetIntraDomainInterfaceIds (relSeqId, pdbId);
            if (deleted)
            {
                DeleteDomainInterfaces(pdbId, intraDomainInterfaceIds);
            }

            if (intraDomainInterfaceIds.Length == 0)
            {
                return;
            }
            DomainInterface[] intraDomainInterfaces = domainInterfaceReader.GetIntraChainDomainInterfaces(pdbId, intraDomainInterfaceIds);

            string hashDir = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
            if (!Directory.Exists(hashDir))
            {
                Directory.CreateDirectory(hashDir);
            }
            string domainInterfaceFile = "";
            string remark = "";
            foreach (DomainInterface domainInterface in intraDomainInterfaces)
            {
                domainInterfaceFile = Path.Combine(hashDir, pdbId + "_d" + domainInterface.domainInterfaceId + ".cryst");
                remark = FormatRemark(pdbId, domainInterface, domainDefTable);
                WriteDomainInterfaceToFile(domainInterfaceFile, remark, domainInterface.chain1, domainInterface.chain2);
                ParseHelper.ZipPdbFile(domainInterfaceFile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        public void WriteDomainInterfaceFiles(string pdbId, int[] domainInterfaceIds, int relSeqId, bool deleted)
        {
            if (deleted)
            {
                DeleteObsoleteDomainInterfaceFiles(pdbId, domainInterfaceIds);
            }


            int[][] difTypeDomainInterfaceIds = GetDifDomainInterfaceIds(relSeqId, pdbId, domainInterfaceIds);

            DataTable domainDefTable = GetEntryDomainDefTable(pdbId);

            DomainInterface[] interChainDomainInterfaces = domainInterfaceReader.GetInterChainDomainInterfaces(pdbId, relSeqId, difTypeDomainInterfaceIds[1], "cryst");
            DomainInterface[] intraChainDomainInterfaces = domainInterfaceReader.GetIntraChainDomainInterfaces(pdbId, difTypeDomainInterfaceIds[0]);
            DomainInterface[] domainInterfaces = new DomainInterface[interChainDomainInterfaces.Length + intraChainDomainInterfaces.Length];
            Array.Copy(interChainDomainInterfaces, 0, domainInterfaces, 0, interChainDomainInterfaces.Length);
            Array.Copy(intraChainDomainInterfaces, 0, domainInterfaces, interChainDomainInterfaces.Length, intraChainDomainInterfaces.Length);

            string hashDir = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
            if (!Directory.Exists(hashDir))
            {
                Directory.CreateDirectory(hashDir);
            }
            string domainInterfaceFile = "";
            string remark = "";
            foreach (DomainInterface domainInterface in domainInterfaces)
            {
                domainInterfaceFile = Path.Combine(hashDir, pdbId + "_d" + domainInterface.domainInterfaceId + ".cryst");
                remark = FormatRemark(pdbId, domainInterface, domainDefTable);
                WriteDomainInterfaceToFile(domainInterfaceFile, remark, domainInterface.chain1, domainInterface.chain2);
                ParseHelper.ZipPdbFile(domainInterfaceFile);
            }

            try
            {
                // for multiChain domain interface files
                WriteMultiChainDomainInterfaces(pdbId, difTypeDomainInterfaceIds[2]);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() + " " + pdbId + " generate multi-chain domain interface files errors:  " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(relSeqId.ToString() + " " + pdbId + " generate multi-chain domain interface files errors:  " + ex.Message);
                ProtCidSettings.logWriter.Flush();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        /// <returns></returns>
        private int[][] GetDifDomainInterfaceIds(int relSeqId, string pdbId, int[] domainInterfaceIds)
        {
            string queryString = string.Format("Select InterfaceID, DomainInterfaceID, DomainID1, DomainID2 From PfamDomainInterfaces " +
                " Where RelSeqID = {0} AND  PdbID = '{1}';", relSeqId, pdbId);
            DataTable entryDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> intraDomainInterfaceIdList = new List<int>();
            List<int> interDomainInterfaceIdList = new List<int>();
            List<int> multiDomainInterfaceIdList = new List<int>();
            int domainInterfaceId = 0;
            int interfaceId = 0;
            long domainId1 = 0;
            long domainId2 = 0;
            long[] multiChainDomainIds = GetEntryMultiChainDomainIds(pdbId);
            foreach (DataRow domainInterfaceRow in entryDomainInterfaceTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                interfaceId = Convert.ToInt32(domainInterfaceRow["InterfaceID"].ToString());
                if (Array.IndexOf(domainInterfaceIds, domainInterfaceId) < 0)
                {
                    continue;
                }
                if (interfaceId == 0)
                {
                    intraDomainInterfaceIdList.Add(domainInterfaceId);
                }
                else
                {
                    domainId1 = Convert.ToInt64(domainInterfaceRow["DomainID1"].ToString());
                    domainId2 = Convert.ToInt64(domainInterfaceRow["DomainID2"].ToString());
                    if (Array.IndexOf(multiChainDomainIds, domainId1) > -1 ||
                        Array.IndexOf(multiChainDomainIds, domainId2) > -1)
                    {
                        if (!multiDomainInterfaceIdList.Contains(domainInterfaceId))
                        {
                            multiDomainInterfaceIdList.Add(domainInterfaceId);
                        }
                    }
                    else
                    {
                        interDomainInterfaceIdList.Add(domainInterfaceId);
                    }
                }
            }
            int[][] difTypeDomainInterfaceIds = new int[3][];
            difTypeDomainInterfaceIds[0] = intraDomainInterfaceIdList.ToArray (); 
            difTypeDomainInterfaceIds[1] = interDomainInterfaceIdList.ToArray (); 
            difTypeDomainInterfaceIds[2] = multiDomainInterfaceIdList.ToArray ();
            return difTypeDomainInterfaceIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        /// <returns></returns>
        private int[][] GetDifDomainInterfaceIds(int relSeqId, string pdbId)
        {
            string queryString = string.Format("Select InterfaceID, DomainInterfaceID, DomainID1, DomainID2 From PfamDomainInterfaces " +
                " Where RelSeqID = {0} AND  PdbID = '{1}';", relSeqId, pdbId);
            DataTable entryDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> intraDomainInterfaceIdList = new List<int>();
            List<int> interDomainInterfaceIdList = new List<int>();
            List<int> multiDomainInterfaceIdList = new List<int>();
            int domainInterfaceId = 0;
            int interfaceId = 0;
            long domainId1 = 0;
            long domainId2 = 0;
            long[] multiChainDomainIds = GetEntryMultiChainDomainIds(pdbId);
            foreach (DataRow domainInterfaceRow in entryDomainInterfaceTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                interfaceId = Convert.ToInt32(domainInterfaceRow["InterfaceID"].ToString());
                if (interfaceId == 0)
                {
                    intraDomainInterfaceIdList.Add(domainInterfaceId);
                }
                else
                {
                    domainId1 = Convert.ToInt64(domainInterfaceRow["DomainID1"].ToString());
                    domainId2 = Convert.ToInt64(domainInterfaceRow["DomainID2"].ToString());
                    if (Array.IndexOf(multiChainDomainIds, domainId1) > -1 ||
                        Array.IndexOf(multiChainDomainIds, domainId2) > -1)
                    {
                        if (!multiDomainInterfaceIdList.Contains(domainInterfaceId))
                        {
                            multiDomainInterfaceIdList.Add(domainInterfaceId);
                        }
                    }
                    else
                    {
                        interDomainInterfaceIdList.Add(domainInterfaceId);
                    }
                }
            }
            int[][] difTypeDomainInterfaceIds = new int[3][];
            difTypeDomainInterfaceIds[0] = intraDomainInterfaceIdList.ToArray ();
            difTypeDomainInterfaceIds[1] = interDomainInterfaceIdList.ToArray (); 
            difTypeDomainInterfaceIds[2] = multiDomainInterfaceIdList.ToArray ();
            return difTypeDomainInterfaceIds;
        }
        /// <summary>
        /// format the remark field
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chain"></param>
        /// <param name="DomainID1"></param>
        /// <param name="DomainID2"></param>
        /// <returns></returns>
        public string FormatRemark(string pdbId, DomainInterface domainInterface, DataTable domainDefTable)
        {
            DataRow[] domainRows1 = domainDefTable.Select(string.Format("DomainID = '{0}'", domainInterface.domainId1), "SeqStart ASC");
            DataRow[] domainRows2 = domainDefTable.Select(string.Format("DomainID = '{0}'", domainInterface.domainId2), "SeqStart ASC");
            string remark = "";
            remark = "HEADER    " + pdbId + "                     " + DateTime.Today;
            remark += "\r\nREMARK  2 Domain Interface ID:" + domainInterface.domainInterfaceId.ToString();
            if (domainInterface.interfaceId == 0)
            {
                remark += "\r\nREMARK    Asymmetric Chain   " + domainInterface.firstSymOpString.Replace ("_1_555", "");
            }
            else
            {
                remark += "\r\nREMARK  2 Interface ID:" + domainInterface.interfaceId.ToString();
                remark += "\r\nREMARK  3 Asymmetric Chain1:" + domainInterface.firstSymOpString +
                    "; Asymmetric Chain2:" + domainInterface.secondSymOpString;
                remark += "\r\nREMARK  3 Entity ID1:" + domainRows1[0]["EntityID"].ToString() +
                    "; Entity ID2:" + domainRows2[0]["EntityID"].ToString();
            }
            remark += "\r\nREMARK  4 PFAM Domain 1:" + domainInterface.domainId1.ToString() + "  Domain Ranges:" + FormatDomainRange(domainRows1);
            remark += "\r\nREMARK  4 PFAM Domain 2:" + domainInterface.domainId2.ToString() + "  Domain Ranges:" + FormatDomainRange(domainRows2);
            return remark;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainRows"></param>
        /// <returns></returns>
        public string FormatDomainRange(DataRow[] domainRows)
        {
            string domainRangeString = "";
            foreach (DataRow domainRow in domainRows)
            {
                domainRangeString += (domainRow["SeqStart"].ToString().Trim() +
                                    "-" + domainRow["SeqEnd"].ToString().Trim() + ";");
            }
            return domainRangeString.TrimEnd (';');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainRanges"></param>
        /// <returns></returns>
        public string FormatDomainRange(Range[] domainRanges)
        {
            string domainRangeString = "";
            foreach (Range domainRange in domainRanges)
            {
                domainRangeString += (domainRange.startPos.ToString () +
                                    "-" + domainRange.endPos.ToString () + ";");
            }
            return domainRangeString.TrimEnd (';');
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public DataTable GetEntryDomainDefTable(string pdbId)
        {
            string queryString = string.Format("Select * From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable entryDomainDefTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return entryDomainDefTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public DataTable GetEntryChainDomainDefTable(string pdbId)
        {
            string queryString = string.Format("Select * From PdbPfamChain Where PdbID = '{0}';", pdbId);
            DataTable entryDomainChainDefTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return entryDomainChainDefTable;
        }

        /// <summary>
        /// the inter-chain domain interfaces
        /// the intra-chain interfaces updated when retrieving the domain interactions
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetInterDomainInterfaceIds (int relSeqId, string pdbId)
        {
            string queryString = string.Format("Select Distinct DomainInterfaceID From PfamDomainInterfaces " +
                " Where RelSeqID = {0} AND PdbID = '{1}' AND InterfaceID > 0;", relSeqId, pdbId);
            DataTable relDomainInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] relDomainInterfaceIds = new int[relDomainInterfaceIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow domainInterafceIdRow in relDomainInterfaceIdTable.Rows)
            {
                relDomainInterfaceIds[count] = Convert.ToInt32(domainInterafceIdRow["DomainInterfaceId"].ToString ());
                count++;
            }
            return relDomainInterfaceIds;
        }

        /// <summary>
        /// the inter-chain domain interfaces
        /// the intra-chain interfaces updated when retrieving the domain interactions
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetIntraDomainInterfaceIds(int relSeqId, string pdbId)
        {
            string queryString = string.Format("Select Distinct DomainInterfaceID From PfamDomainInterfaces " +
                " Where RelSeqID = {0} AND PdbID = '{1}' AND InterfaceID = 0;", relSeqId, pdbId);
            DataTable intraDomainInterfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] intraDomainInterfaceIds = new int[intraDomainInterfaceIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow domainInterafceIdRow in intraDomainInterfaceIdTable.Rows)
            {
                intraDomainInterfaceIds[count] = Convert.ToInt32(domainInterafceIdRow["DomainInterfaceId"].ToString());
                count++;
            }
            return intraDomainInterfaceIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteDomainInterfaces(string pdbId, int[] domainInterfaceIds)
        {
            string hashFolder = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
            string domainInterfaceFile = "";
            foreach (int domainInterfaceId in domainInterfaceIds)
            {
                domainInterfaceFile = Path.Combine(hashFolder, pdbId + "_d" + domainInterfaceId.ToString() + ".cryst.gz");
                File.Delete(domainInterfaceFile);
            }
        }
        #endregion
     
        #region get domain interface ids for coordinate files
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private int[] GetDomainInterfaceIDs(string pdbId, int relSeqId)
        {
            string queryString = string.Format("Select Distinct DomainInterfaceID From {0}DomainInterfaces " +
                " Where RelSeqID = {1} AND PdbID = '{2}';", ProtCidSettings.dataType, relSeqId, pdbId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] domainInterfaceIds = new int[domainInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow domainInterfaceRow in domainInterfaceTable.Rows)
            {
                domainInterfaceIds[count] = Convert.ToInt32(domainInterfaceRow["DomainInterfaceID"].ToString());
                count++;
            }
            return domainInterfaceIds;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private int[] GetDomainInterfaceIDsWithNoFiles(string pdbId, int relSeqId)
        {
            string queryString = string.Format("Select * From {0}DomainInterfaces " +
                " Where RelSeqID = {1} AND PdbID = '{2}';", ProtCidSettings.dataType, relSeqId, pdbId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            int domainInterfaceId = 0;
            List<int> leftDomainInterfaceIdList = new List<int> ();
            foreach (DataRow interfaceRow in domainInterfaceTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());
                if (IsDomainInterfaceFileExist(pdbId, domainInterfaceId))
                {
                    continue;
                }
                leftDomainInterfaceIdList.Add(domainInterfaceId);
            }
            return leftDomainInterfaceIdList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceFileExist(string pdbId, int domainInterfaceId)
        {
            string hashDir = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
            if (!Directory.Exists(hashDir))
            {
                return false;
            }
            string domainInterfaceFile = Path.Combine(hashDir, pdbId + "_d" + domainInterfaceId.ToString() + ".cryst.gz");
            if (File.Exists(domainInterfaceFile))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceFileExist (string pdbId)
        {
            string hashFolder = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
            string[] domainInterfaceFiles = Directory.GetFiles(hashFolder, pdbId + "*");
            if (domainInterfaceFiles.Length > 0)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region write domain atoms
        /// <summary>
		/// write domain-domain interaction to a pdb formatted file
		/// </summary>
		/// <param name="fileName"></param>
		/// <param name="remark"></param>
		/// <param name="domain1"></param>
		/// <param name="domain2"></param>
		public void WriteDomainInterfaceToFile (string fileName, string remark, AtomInfo[] domain1, AtomInfo[] domain2)
		{
			WriteInterfaceToFile (fileName, remark, domain1, domain2);
		}

		/// <summary>
		/// write a domain to a file
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="chainId"></param>
		/// <param name="DomainID"></param>
		/// <param name="domainAtoms"></param>
		/// <param name="remark"></param>
		public void WriteDomainToFile (string pdbId, string chainId, int DomainID, AtomInfo[] domainAtoms, string remark)
		{
			WriteAtoms (pdbId, chainId, domainAtoms, DomainID, remark);
        }
        #endregion
        #endregion

        /** end of generating domain interface files for protcid
         * */

        /*  generate the domain interface files from domain coordinate files and symmetry operators.
            the domain file may have the different sequence numbers from the original chain sequence numbers.
            for the multi-chain domain, the sequence numbers are in the order of hmm model.
         * This procedure is more time-consuming, but more straight-forward, hopefully less bugs.
         * Written on Jan. 30-31, 2013
            this procedure may use for the future work
         * */
        #region generate domain interface files from domain coordinate files and symmetry operators
        #region all domain interface files
        /// <summary>
        /// 
        /// </summary>
        public void WriteDomainInterfaceFilesBySymOp ()
        {
            string queryString = "Select Distinct PdbID From PfamDomainInterfaces;";
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = entryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = entryTable.Rows.Count;

            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                try
                {
                    WriteEntryDomainInterfaces(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " write multi-chain domain interface errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " write multi-chain domain interface file errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateDomainInterfaceFiles(string[] updateEntries)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
        //            DeleteObsoleteEntryDomainInterfaces(pdbId);

                    WriteEntryDomainInterfaces(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " write  domain interface errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " write domain interface file errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
        /// <summary>
        /// can be applied on all the domain interfaces
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="multiChainDomainIds"></param>
        public void WriteEntryDomainInterfaces(string pdbId)
        {
            string hashDir = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
            if (!Directory.Exists(hashDir))
            {
                Directory.CreateDirectory(hashDir);
            }
            DataTable domainDefTable = GetEntryDomainDefTable(pdbId);
            DataTable chainDomainDefTable = GetEntryChainDomainDefTable(pdbId);
            DataTable chainInterfaceTable = GetChainInterfaceTable(pdbId);
            DataTable domainInterfaceTable = GetEntryDomainInterfaces(pdbId);
            int[] domainInterfaceIds = GetEntryDomainInterfaceIds(domainInterfaceTable);

            Dictionary<int, string[]> domainSymOpHash = GetDomainSymmetryStrings(domainInterfaceTable, chainInterfaceTable);
            Dictionary<string, AtomInfo[]> domainSymOpAtomHash = GetSymOpDomainCoords(pdbId, domainSymOpHash);

            string domainInterfaceFile = "";
            string remark = "";
            foreach (int domainInterfaceId in domainInterfaceIds)
            {
                DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format("DomainInterfaceID = '{0}'", domainInterfaceId));
                string[] domainSymmetryStrings = GetDomainSymmetryStrings(domainInterfaceRows, chainInterfaceTable);
                SortDomainSymmetryStringsInPfamOrder(domainSymmetryStrings, chainDomainDefTable, domainDefTable);

                remark = "HEADER " + pdbId + " " + domainInterfaceId.ToString() + "  " + DateTime.Today.ToShortDateString();
                AtomInfo[] interfaceChainAAtoms = (AtomInfo[])domainSymOpAtomHash[domainSymmetryStrings[0]];
                remark += "REMARK 3  Interface Chain A\r\n";
                remark += FormatDomainFileInfoRemark(pdbId, domainSymmetryStrings[0]);
                remark += "\r\n";
                AtomInfo[] interfaceChainBAtoms = (AtomInfo[])domainSymOpAtomHash[domainSymmetryStrings[1]];
                remark += "REMARK 3  Interface Chain B\r\n";
                remark += FormatDomainFileInfoRemark(pdbId, domainSymmetryStrings[1]);

                domainInterfaceFile = Path.Combine(hashDir, pdbId + "_d" + domainInterfaceId.ToString() + ".cryst");
                WriteInterfaceToFile(domainInterfaceFile, remark, interfaceChainAAtoms, interfaceChainBAtoms);
                ParseHelper.ZipPdbFile(domainInterfaceFile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteObsoleteEntryDomainInterfaces(string pdbId)
        {
            string hashFolder = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
            string[] domainInterfaceFiles = Directory.GetFiles(hashFolder, pdbId + "*");
            foreach (string domainInterfaceFile in domainInterfaceFiles)
            {
                File.Delete(domainInterfaceFile);
            }
        }      
        #endregion

        #region multi-chain domain interface files
        /// <summary>
        /// 
        /// </summary>
        public void WriteMultiChainDomainInterfaces()
        {
            Dictionary<string, long[]> entryMultiChainDomainHash = GetEntryMultiChainDomainIds();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = entryMultiChainDomainHash.Count;
            ProtCidSettings.progressInfo.totalOperationNum = entryMultiChainDomainHash.Count;

            List<string> entryList = new List<string> (entryMultiChainDomainHash.Keys);
            entryList.Sort();

            foreach (string pdbId in entryMultiChainDomainHash.Keys)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    long[] multiChainDomainIds = entryMultiChainDomainHash[pdbId];
                    WriteMultiChainDomainInterfaces(pdbId, multiChainDomainIds);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " write multi-chain domain interface errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " write multi-chain domain interface file errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateMultiChainDomainInterfaces(string[] updateEntries)
        {
            Dictionary<string, long[]> entryMultiChainDomainHash = GetEntryMultiChainDomainIds(updateEntries);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = entryMultiChainDomainHash.Count;
            ProtCidSettings.progressInfo.totalOperationNum = entryMultiChainDomainHash.Count;

            foreach (string pdbId in entryMultiChainDomainHash.Keys)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    long[] multiChainDomainIds = (long[])entryMultiChainDomainHash[pdbId];
                    WriteMultiChainDomainInterfaces(pdbId, multiChainDomainIds);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " write multi-chain domain interface errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " write multi-chain domain interface file errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public void WriteMultiChainDomainInterfaces(string pdbId)
        {
            long[] multiChainDomainIds = GetEntryMultiChainDomainIds(pdbId);
            if (multiChainDomainIds.Length > 0)
            {
                WriteMultiChainDomainInterfaces(pdbId, multiChainDomainIds);
            }
        }

        /// <summary>
        /// for entries with multi-chain domains
        /// the multi-chain domain interfaces are generated from the domain files and symmetry operators
        /// the sequence numbers may be different from the original chain sequence numbers
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="multiChainDomainIds"></param>
        public void WriteMultiChainDomainInterfaces(string pdbId, long[] multiChainDomainIds)
        {
            string hashDir = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
            if (!Directory.Exists(hashDir))
            {
                Directory.CreateDirectory(hashDir);
            }
            DataTable domainDefTable = GetEntryDomainDefTable(pdbId);
            DataTable chainDomainTable = GetEntryChainDomainDefTable (pdbId);
            DataTable chainInterfaceTable = GetChainInterfaceTable(pdbId);
            DataTable multiChainDomainInterfaceTable = GetEntryDomainInterfaceTable(pdbId, multiChainDomainIds);
            int[] multiChainDomainInterfaceIds = GetEntryDomainInterfaceIds(multiChainDomainInterfaceTable);

            Dictionary<int, string[]> domainSymOpHash = GetDomainSymmetryStrings(multiChainDomainInterfaceTable, chainInterfaceTable);
            Dictionary<string, AtomInfo[]> domainSymOpAtomHash = GetSymOpDomainCoords(pdbId, domainSymOpHash);
            /*
             *  change the sequential numbers in the domain file into residue numbers in the XML files.
                since all other domain interface files are using residue numbers in the XML files,
                except the multi-chain domain interface files which use the sequential numbers in the domain files.
                so if the interface file contains one single-chain domain, use the XML sequential numbers instead.
             * */
            ChangeSingleChainDomainFileSeqIdsToXml(pdbId, domainSymOpAtomHash, chainDomainTable, multiChainDomainIds);

            string domainInterfaceFile = "";
            string remark = "";
            
            foreach (int domainInterfaceId in multiChainDomainInterfaceIds)
            {
                domainInterfaceFile = Path.Combine(hashDir, pdbId + "_d" + domainInterfaceId.ToString() + ".cryst");
                if (File.Exists(domainInterfaceFile + ".gz"))
                {
                    continue;
                }
                DataRow[] domainInterfaceRows = multiChainDomainInterfaceTable.Select(string.Format("DomainInterfaceID = '{0}'", domainInterfaceId));
                string[] domainSymmetryStrings = GetDomainSymmetryStrings(domainInterfaceRows, chainInterfaceTable);
                SortDomainSymmetryStringsInPfamOrder(domainSymmetryStrings, chainDomainTable, domainDefTable);

                remark = "HEADER " + pdbId + " " + domainInterfaceId.ToString() + "  " + DateTime.Today.ToShortDateString() + "\r\n";
                AtomInfo[] interfaceChainAAtoms = (AtomInfo[])domainSymOpAtomHash[domainSymmetryStrings[0]];
               
                remark += "REMARK 3  Interface Chain A\r\n";
                remark += FormatDomainFileInfoRemark(pdbId, domainSymmetryStrings[0]);
                remark += "\r\n";

                AtomInfo[] interfaceChainBAtoms = (AtomInfo[])domainSymOpAtomHash[domainSymmetryStrings[1]];

                remark += "REMARK 3  Interface Chain B\r\n";
                remark += FormatDomainFileInfoRemark(pdbId, domainSymmetryStrings[1]);

                WriteInterfaceToFile(domainInterfaceFile, remark, interfaceChainAAtoms, interfaceChainBAtoms);
                ParseHelper.ZipPdbFile(domainInterfaceFile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainSymOpAtomHash"></param>
        /// <param name="chainDomainTable"></param>
        /// <param name="mulitChainDomainIds"></param>
        private void ChangeSingleChainDomainFileSeqIdsToXml(string pdbId, Dictionary<string, AtomInfo[]> domainSymOpAtomHash, DataTable chainDomainTable, long[] multiChainDomainIds)
        {
            List<string> domainSymOpList = new List<string> (domainSymOpAtomHash.Keys);
            string[] domainSymOpStrings = new string[domainSymOpList.Count];
            domainSymOpList.CopyTo(domainSymOpStrings);
            int[] chainDomainIds = GetChainDomainIds(domainSymOpStrings);
            long domainId = 0;
            int hashChainDomainId = 0;
            foreach (int chainDomainId in chainDomainIds)
            {
                domainId = GetDomainId(pdbId, chainDomainId, chainDomainTable);
                if (IsDomainMultiChain(domainId, multiChainDomainIds))
                {
                    continue;
                }
                DataTable domainFileInfoTable = GetDomainFileInfoTable(pdbId, chainDomainId, domainId);
                foreach (string domainSymOp in domainSymOpAtomHash.Keys)
                {
                    hashChainDomainId = GetChainDomainIdFromDomainSymString(domainSymOp);
                    if (hashChainDomainId == chainDomainId)
                    {
                        AtomInfo[] domainAtoms = (AtomInfo[])domainSymOpAtomHash[domainSymOp];
                        ChangeSingleChainDomainFileSeqIdsToXml(domainAtoms, domainFileInfoTable);
                    }
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainSymOpStrings"></param>
        /// <returns></returns>
        private int[] GetChainDomainIds(string[] domainSymOpStrings)
        {
            List<int> chainDomainIdList = new List<int> ();
            int chainDomainId = 0;
            foreach (string domainSymOp in domainSymOpStrings)
            {
                chainDomainId = GetChainDomainIdFromDomainSymString(domainSymOp);
                if (!chainDomainIdList.Contains(chainDomainId))
                {
                    chainDomainIdList.Add(chainDomainId);
                }
            }

            return chainDomainIdList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <param name="chainDomainTable"></param>
        /// <returns></returns>
        private bool IsDomainMultiChain(long domainId, long[] multiChainDomainIds)
        {
            if (Array.IndexOf(multiChainDomainIds, domainId) > -1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <param name="chainDomainTable"></param>
        /// <returns></returns>
        private long GetDomainId(string pdbId, int chainDomainId, DataTable chainDomainTable)
        {
            DataRow[] chainDomainRows = chainDomainTable.Select(string.Format("ChainDomainID = '{0}'", chainDomainId));
            long domainId = Convert.ToInt64(chainDomainRows[0]["DomainID"].ToString());
            return domainId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainSymmetryString"></param>
        /// <returns></returns>
        private int GetChainDomainIdFromDomainSymString(string domainSymmetryString)
        {
            string[] fields = domainSymmetryString.Split('_');
            int chainDomainId = Convert.ToInt32(fields[0]);
            return chainDomainId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private DataTable GetDomainFileInfoTable(string pdbId, int chainDomainId, long domainId)
        {
            string queryString = string.Format("Select Distinct * From PdbPfamDomainFileInfo Where PdbID = '{0}' AND ChainDomainID = {1};", pdbId, chainDomainId);
            DataTable domainFileInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (domainFileInfoTable.Rows.Count == 0)
            {
                queryString = string.Format("Select Distinct * From PdbPfamDomainFileInfo Where PdbID = '{0}' AND DomainID = {1};", pdbId, domainId);
                domainFileInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            }
            return domainFileInfoTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainAtoms"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        private void ChangeSingleChainDomainFileSeqIdsToXml (AtomInfo[] chainAtoms, DataTable domainFileInfoTable)
        {
            int seqStart = 0;
            int fileStart = 0;
            if (domainFileInfoTable.Rows.Count == 1)
            {
                seqStart = Convert.ToInt32(domainFileInfoTable.Rows[0]["SeqStart"].ToString ());
                fileStart = Convert.ToInt32(domainFileInfoTable.Rows[0]["FileStart"].ToString ());
                if (seqStart == fileStart)
                {
                    return;
                }
            }
            Range[][] seqFileRanges = GetFileSeqDomainRanges(domainFileInfoTable);
            int fileSeqId = 0;
            int domainSeqId = 0;
            for (int i = 0; i < chainAtoms.Length; i ++ )
            {
                AtomInfo atom = chainAtoms[i];
                fileSeqId = Convert.ToInt32(atom.seqId);
                domainSeqId = GetDomainXmlSeqId(fileSeqId, seqFileRanges);
                atom.seqId = domainSeqId.ToString();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileSeqId"></param>
        /// <param name="seqFileRanges"></param>
        /// <returns></returns>
        private int GetDomainXmlSeqId(int fileSeqId, Range[][] seqFileRanges)
        {
            Range[] domainSeqRanges = seqFileRanges[0];
            Range[] domainFileRanges = seqFileRanges[1];

            int rangeCount = 0;
            int domainSeqId = 0;
            foreach (Range fileRange in domainFileRanges)
            {
                if (fileSeqId >= fileRange.startPos && fileSeqId <= fileRange.endPos)
                {
                    Range domainRange = domainSeqRanges[rangeCount];
                    int startDif = fileSeqId - fileRange.startPos;
                    domainSeqId = domainRange.startPos + startDif;
                    break;
                }
                rangeCount++;
            }
            return domainSeqId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainFileInfoTable"></param>
        /// <returns></returns>
        private Range[][] GetFileSeqDomainRanges(DataTable domainFileInfoTable)
        {
            Range[] fileRanges = new  Range[domainFileInfoTable.Rows.Count];
            Range[] seqRanges = new Range[domainFileInfoTable.Rows.Count];
            int count = 0;
            foreach (DataRow fileInfoRow in domainFileInfoTable.Rows)
            {
                Range fileRange = new Range();
                fileRange.startPos = Convert.ToInt32(fileInfoRow["FileStart"].ToString ());
                fileRange.endPos = Convert.ToInt32(fileInfoRow["FileEnd"].ToString ());
                fileRanges[count] = fileRange;

                Range seqRange = new Range();
                seqRange.startPos = Convert.ToInt32(fileInfoRow["SeqStart"].ToString ());
                seqRange.endPos = Convert.ToInt32(fileInfoRow["SeqEnd"].ToString ());
                seqRanges[count] = seqRange;

                count++;
            }
            Range[][] fileSeqRanges = new Range[2][];
            fileSeqRanges[0] = seqRanges;
            fileSeqRanges[1] = fileRanges;
            return fileSeqRanges;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="multiChainDomainInterfaceIds"></param>
        public void WriteMultiChainDomainInterfaces(string pdbId, int[] multiChainDomainInterfaceIds)
        {
            if (multiChainDomainInterfaceIds.Length == 0)
            {
                return;
            }
            string hashDir = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
            if (!Directory.Exists(hashDir))
            {
                Directory.CreateDirectory(hashDir);
            }
            DataTable domainDefTable = GetEntryDomainDefTable(pdbId);
            DataTable chainDomainTable = GetEntryChainDomainDefTable(pdbId);
            DataTable chainInterfaceTable = GetChainInterfaceTable(pdbId);
            DataTable multiChainDomainInterfaceTable = GetEntryDomainInterfaceTable(pdbId, multiChainDomainInterfaceIds);
      //      int[] multiChainDomainInterfaceIds = GetEntryDomainInterfaceIds(multiChainDomainInterfaceTable);

            Dictionary<int, string[]> domainSymOpHash = GetDomainSymmetryStrings(multiChainDomainInterfaceTable, chainInterfaceTable);
            Dictionary<string, AtomInfo[]> domainSymOpAtomHash = GetSymOpDomainCoords(pdbId, domainSymOpHash);

            string domainInterfaceFile = "";
            string remark = "";
            foreach (int domainInterfaceId in multiChainDomainInterfaceIds)
            {
                DataRow[] domainInterfaceRows = multiChainDomainInterfaceTable.Select(string.Format("DomainInterfaceID = '{0}'", domainInterfaceId));
                string[] domainSymmetryStrings = GetDomainSymmetryStrings(domainInterfaceRows, chainInterfaceTable);
                SortDomainSymmetryStringsInPfamOrder(domainSymmetryStrings, chainDomainTable, domainDefTable);

                remark = "HEADER " + pdbId + " " + domainInterfaceId.ToString() + "  " + DateTime.Today.ToShortDateString() + "\r\n";
                AtomInfo[] interfaceChainAAtoms = (AtomInfo[])domainSymOpAtomHash[domainSymmetryStrings[0]];
                remark += "REMARK 3  Interface Chain A\r\n";
                remark += FormatDomainFileInfoRemark(pdbId, domainSymmetryStrings[0]);
                remark += "\r\n";
                AtomInfo[] interfaceChainBAtoms = (AtomInfo[])domainSymOpAtomHash[domainSymmetryStrings[1]];
                remark += "REMARK 3  Interface Chain B\r\n";
                remark += FormatDomainFileInfoRemark(pdbId, domainSymmetryStrings[1]);

                domainInterfaceFile = Path.Combine(hashDir, pdbId + "_d" + domainInterfaceId.ToString() + ".cryst");
                WriteInterfaceToFile(domainInterfaceFile, remark, interfaceChainAAtoms, interfaceChainBAtoms);
                ParseHelper.ZipPdbFile(domainInterfaceFile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, long[]> GetEntryMultiChainDomainIds()
        {
            string queryString = "Select PdbID, DomainID, Count(Distinct EntityID) As EntityCount From PdbPfam Group By PdbID, DomainID;";
            DataTable domainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pdbId = "";
            long domainId = 0;
            int entityCount = 0;
            Dictionary<string, List<long>> entryMultiChainDomainListHash = new Dictionary<string,List<long>> ();
            foreach (DataRow domainRow in domainTable.Rows)
            {
                entityCount = Convert.ToInt32(domainRow["EntityCount"].ToString());
                if (entityCount > 1)
                {
                    pdbId = domainRow["PdbID"].ToString();
                    domainId = Convert.ToInt64(domainRow["DomainID"].ToString());
                    if (entryMultiChainDomainListHash.ContainsKey(pdbId))
                    {
                        entryMultiChainDomainListHash[pdbId].Add(domainId);
                    }
                    else
                    {
                        List<long> multiChainDomainList = new List<long> ();
                        multiChainDomainList.Add(domainId);
                        entryMultiChainDomainListHash.Add(pdbId, multiChainDomainList);
                    }

                }
            }
            Dictionary<string, long[]> entryMultiChainDomainHash = new Dictionary<string, long[]>();
            foreach (string lsEntry in entryMultiChainDomainListHash.Keys)
            {
                entryMultiChainDomainHash.Add (lsEntry, entryMultiChainDomainListHash[lsEntry].ToArray ());
            }
            return entryMultiChainDomainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public long[] GetEntryMultiChainDomainIds(string pdbId)
        {
            string queryString = string.Format("Select DomainID, Count(Distinct EntityID) As EntityCount " +
                            " From PdbPfam Where PdbID = '{0}' Group By DomainID;", pdbId);
            DataTable domainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            List<long> multiChainDomainIdList = new List<long> ();
            long domainId = 0;
            int entityCount = 0;
            foreach (DataRow domainRow in domainTable.Rows)
            {
                entityCount = Convert.ToInt32(domainRow["EntityCount"].ToString());
                if (entityCount > 1)
                {
                    domainId = Convert.ToInt64(domainRow["DomainID"].ToString());
                    multiChainDomainIdList.Add(domainId);

                }
            }
            return multiChainDomainIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, long[]> GetEntryMultiChainDomainIds(string[] entries)
        {
            string queryString = "";
            DataTable domainTable = null;
            foreach (string entry in entries)
            {
                queryString = string.Format ("Select PdbID, DomainID, Count(Distinct EntityID) As EntityCount From PdbPfam " + 
                    " Where PdbID = '{0}' Group By PdbID, DomainID;", entry);
                DataTable entryDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                if (domainTable == null)
                {
                    domainTable = entryDomainTable.Copy();
                }
                else
                {
                    foreach (DataRow dataRow in entryDomainTable.Rows)
                    {
                        DataRow newRow = domainTable.NewRow();
                        newRow.ItemArray = dataRow.ItemArray;
                        domainTable.Rows.Add(newRow);
                    }
                }
            }
            string pdbId = "";
            long domainId = 0;
            int entityCount = 0;
            Dictionary<string, List<long>> entryMultiChainDomainListHash = new Dictionary<string,List<long>> ();
            foreach (DataRow domainRow in domainTable.Rows)
            {
                entityCount = Convert.ToInt32(domainRow["EntityCount"].ToString());
                if (entityCount > 1)
                {
                    pdbId = domainRow["PdbID"].ToString();
                    domainId = Convert.ToInt64(domainRow["DomainID"].ToString());
                    if (entryMultiChainDomainListHash.ContainsKey(pdbId))
                    {
                        entryMultiChainDomainListHash[pdbId].Add(domainId);
                    }
                    else
                    {
                        List<long> multiChainDomainList = new List<long> ();
                        multiChainDomainList.Add(domainId);
                        entryMultiChainDomainListHash.Add(pdbId, multiChainDomainList);
                    }

                }
            }
            Dictionary<string, long[]> entryMultiChainDomainHash = new Dictionary<string, long[]>();
            foreach (string lsEntry in entryMultiChainDomainListHash.Keys)
            {
                entryMultiChainDomainHash.Add (lsEntry, entryMultiChainDomainListHash[lsEntry].ToArray ());
            }
            return entryMultiChainDomainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        private void DeleteObsoleteDomainInterfaceFiles(string pdbId, int[] domainInterfaceIds)
        {
            string hashFolder = Path.Combine(pfamDomainInterfaceFileDir, pdbId.Substring(1, 2));
            string domainInterfaceFile = "";
            foreach (int domainInterfaceId in domainInterfaceIds)
            {
                domainInterfaceFile = Path.Combine(hashFolder, pdbId + "_d" + domainInterfaceId.ToString() + ".cryst.gz");
                File.Delete(domainInterfaceFile);
            }
        }
        #endregion

        #region info for domain and symmetry operators
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainSymmetryString"></param>
        /// <returns></returns>
        public string FormatDomainFileInfoRemark(string pdbId, string domainSymmetryString)
        {
            string[] fields = domainSymmetryString.Split('_');
            int chainDomainId = Convert.ToInt32 (fields[0]);
            string queryString = string.Format("Select Distinct * From PdbPfamDomainFileInfo Where PdbID = '{0}' AND ChainDomainID = {1} Order By FileStart;", pdbId, chainDomainId);
            DataTable domainFileInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string asymChain = "";
            string remark = "";
            if (domainFileInfoTable.Rows.Count == 0)
            {
                // get the file info by domain Id, it is only for single chain domain
                long domainId = GetDomainID(pdbId, chainDomainId, out asymChain);
                queryString = string.Format("Select Distinct * From PdbPfamDomainFileInfo Where PdbID = '{0}' AND DomainID = {1} Order By FileStart;", pdbId, domainId);
                domainFileInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                if (domainFileInfoTable.Rows.Count == 1)
                {
                    domainFileInfoTable.Rows[0]["AsymChain"] = asymChain;
                }
            }
            if (domainFileInfoTable.Rows.Count > 0)
            {
                remark = "REMARK 3  Chain DomainID " + chainDomainId.ToString() +
                    " DomainID " + domainFileInfoTable.Rows[0]["DomainID"].ToString() + " " +
                    " Symmetry Operator: " + fields[1] + "_" + fields[2];
                foreach (DataRow fileInfoRow in domainFileInfoTable.Rows)
                {
                    remark += ("\r\nREMARK 3  EntityID: " + fileInfoRow["EntityID"].ToString() +
                        " Asymmetric Chain: " + fileInfoRow["AsymChain"].ToString().TrimEnd() + "\r\n" +
                        "REMARK 3  Sequence Start: " + fileInfoRow["SeqStart"].ToString() +
                        " Sequence End: " + fileInfoRow["SeqEnd"].ToString() +
                        " File Start: " + fileInfoRow["FileStart"].ToString() +
                        " File End: " + fileInfoRow["FileEnd"].ToString());
                }
            }
            return remark;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        private long GetDomainID(string pdbId, int chainDomainId, out string asymChain)
        {
            string queryString = string.Format("Select DomainID, asymChain From PdbPfamChain Where PdbID = '{0}' AND ChainDomainID = {1};", pdbId, chainDomainId);
            DataTable domainIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            long domainId = 0;
            asymChain = "";
            if (domainIdTable.Rows.Count > 0)
            {
                domainId = Convert.ToInt64(domainIdTable.Rows[0]["DomainID"].ToString ());
                asymChain = domainIdTable.Rows[0]["AsymChain"].ToString().TrimEnd();
            }
            return domainId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        public string FormatDomainFileInfoRemark(string pdbId, int chainDomainId)
        {
            string queryString = string.Format("Select Distinct * From PdbPfamDomainFileInfo Where PdbID = '{0}' AND ChainDomainID = {1} Order By FileStart;", pdbId, chainDomainId);
            DataTable domainFileInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string remark = "REMARK 3  Chain DomainID " + chainDomainId.ToString() +
                " DomainID " + domainFileInfoTable.Rows[0]["DomainID"].ToString() + " " +
                " Symmetry Operator: 1_555";
            foreach (DataRow fileInfoRow in domainFileInfoTable.Rows)
            {
                remark += ("\r\nREMARK 3  EntityID: " + fileInfoRow["EntityID"].ToString() +
                    " Asymmetric Chain: " + fileInfoRow["AsymChain"].ToString().TrimEnd() + "\r\n" +
                    "REMARK 3  Sequence Start: " + fileInfoRow["SeqStart"].ToString() +
                    " Sequence End: " + fileInfoRow["SeqEnd"].ToString() +
                    " File Start: " + fileInfoRow["FileStart"].ToString() +
                    " File End: " + fileInfoRow["FileEnd"].ToString());
            }
            return remark;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <returns></returns>
        public string FormatDomainFileInfoRemark(string pdbId, long domainId, int chainDomainId)
        {
            string queryString = string.Format("Select Distinct * From PdbPfamDomainFileInfo Where PdbID = '{0}' AND ChainDomainID = {1} Order By FileStart;", pdbId, chainDomainId);
            DataTable domainFileInfoTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string remark = "REMARK 3  Chain DomainID " + chainDomainId.ToString() +
                " DomainID " + domainId + " Symmetry Operator: 1_555";
            foreach (DataRow fileInfoRow in domainFileInfoTable.Rows)
            {
                remark += ("\r\nREMARK 3  EntityID: " + fileInfoRow["EntityID"].ToString() +
                    " Asymmetric Chain: " + fileInfoRow["AsymChain"].ToString().TrimEnd() + "\r\n" +
                    "REMARK 3  Sequence Start: " + fileInfoRow["SeqStart"].ToString() +
                    " Sequence End: " + fileInfoRow["SeqEnd"].ToString() +
                    " File Start: " + fileInfoRow["FileStart"].ToString() +
                    " File End: " + fileInfoRow["FileEnd"].ToString());
            }
            return remark;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainSymmetryStrings"></param>
        /// <param name="domainDefTable"></param>
        private void SortDomainSymmetryStringsInPfamOrder(string[] domainSymmetryStrings, DataTable chainDomainTable, DataTable domainDefTable)
        {
            string domainSymString1 = domainSymmetryStrings[0];
            string[] fields = domainSymString1.Split('_');
            int chainDomainId1 = Convert.ToInt32 (fields[0]);
            DataRow[] chainDomainDefRows = chainDomainTable.Select(string.Format ("ChainDomainID = '{0}'", chainDomainId1));
            long domainId1 = Convert.ToInt64(chainDomainDefRows[0]["DomainID"].ToString ());
            DataRow[] domainDefRows = domainDefTable.Select(string.Format("DomainID = '{0}'", domainId1));
            string pfamId1 = domainDefRows[0]["Pfam_ID"].ToString().TrimEnd();

            string domainSymString2 = domainSymmetryStrings[1];
            fields = domainSymString2.Split('_');
            int chainDomainId2 = Convert.ToInt32 (fields[0]);
            chainDomainDefRows = chainDomainTable.Select(string.Format ("ChainDomainID = '{0}'", chainDomainId2));
            long domainId2 = Convert.ToInt64(chainDomainDefRows[0]["DomainID"].ToString ());
            domainDefRows = domainDefTable.Select(string.Format("DomainID = '{0}'", domainId2));
            string pfamId2 = domainDefRows[0]["Pfam_ID"].ToString().TrimEnd();

            if (string.Compare(pfamId1, pfamId2) > 0)
            {
                string temp = domainSymmetryStrings[0];
                domainSymmetryStrings[0] = domainSymmetryStrings[1];
                domainSymmetryStrings[1] = temp;
            }
            else if (pfamId1 == pfamId2)
            {
                if (string.Compare(domainSymmetryStrings[0], domainSymmetryStrings[1]) > 0)
                {
                    string temp = domainSymmetryStrings[0];
                    domainSymmetryStrings[0] = domainSymmetryStrings[1];
                    domainSymmetryStrings[1] = temp;
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryDomainInterfaces(string pdbId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return domainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetChainInterfaceTable(string pdbId)
        {
            string queryString = string.Format("Select PdbId, InterfaceID, AsymChain1, SymmetryString1, AsymChain2, SymmetryString2 " + 
                " From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable chainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return chainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private Dictionary<int, string[]> GetDomainSymmetryStrings(DataTable domainInterfaceTable, DataTable chainInterfaceTable)
        {
            Dictionary<int, List<string>> domainSymOpListHash = new Dictionary<int, List<string>>();
            int chainInterfaceId = 0;
            string pdbId = "";
            int chainDomainId = 0;
            foreach (DataRow interfaceRow in domainInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                chainInterfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                string[] symmetryStrings = GetSymmetryStrings(pdbId, chainInterfaceId, chainInterfaceTable);
                if (symmetryStrings == null)
                {
                    symmetryStrings = new string[2];
                    symmetryStrings[0] = "1_555";
                    symmetryStrings[1] = "1_555";
                }
                chainDomainId = Convert.ToInt32 (interfaceRow["ChainDomainID1"].ToString());
                if (domainSymOpListHash.ContainsKey(chainDomainId))
                {
                    if (!domainSymOpListHash[chainDomainId].Contains(symmetryStrings[0]))
                    {
                        domainSymOpListHash[chainDomainId].Add(symmetryStrings[0]);
                    }
                }
                else
                {
                    List<string> symmetryList = new List<string> ();
                    symmetryList.Add(symmetryStrings[0]);
                    domainSymOpListHash.Add(chainDomainId, symmetryList);
                }
                chainDomainId = Convert.ToInt32 (interfaceRow["ChainDomainID2"].ToString());
                if (domainSymOpListHash.ContainsKey(chainDomainId))
                {
                    if (!domainSymOpListHash[chainDomainId].Contains(symmetryStrings[1]))
                    {
                        domainSymOpListHash[chainDomainId].Add(symmetryStrings[1]);
                    }
                }
                else
                {
                    List<string> symmetryList = new List<string> ();
                    symmetryList.Add(symmetryStrings[1]);
                    domainSymOpListHash.Add(chainDomainId, symmetryList);
                }
            }
            Dictionary<int, string[]> domainSymOpHash = new Dictionary<int, string[]>();
            foreach (int lsDomainId in domainSymOpListHash.Keys)
            {
                domainSymOpHash.Add (lsDomainId, domainSymOpListHash[lsDomainId].ToArray ());
            }
            return domainSymOpHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private string[] GetDomainSymmetryStrings(DataRow[] domainInterfaceRows, DataTable chainInterfaceTable)
        {
            string[] domainSymmetryStrings = new string[2];
            int chainInterfaceId = Convert.ToInt32(domainInterfaceRows[0]["InterfaceID"].ToString());
            if (chainInterfaceId == 0)
            {
                domainSymmetryStrings[0] = domainInterfaceRows[0]["ChainDomainID1"].ToString() + "_1_555";
                domainSymmetryStrings[1] = domainInterfaceRows[0]["ChainDomainID2"].ToString() + "_1_555";
            }
            else
            {
                string pdbId = domainInterfaceRows[0]["PdbID"].ToString();
                string[] symmetryStrings = GetSymmetryStrings(pdbId, chainInterfaceId, chainInterfaceTable);

                domainSymmetryStrings[0] = domainInterfaceRows[0]["ChainDomainID1"].ToString() + "_" + symmetryStrings[0];
                domainSymmetryStrings[1] = domainInterfaceRows[0]["ChainDomainID2"].ToString() + "_" + symmetryStrings[1];
            }

            return domainSymmetryStrings;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainInterfaceId"></param>
        /// <param name="chainInterfaceTable"></param>
        /// <returns></returns>
        private string[] GetSymmetryStrings(string pdbId, int chainInterfaceId, DataTable chainInterfaceTable)
        {
            DataRow[] chainInterfaceRows = chainInterfaceTable.Select(string.Format("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, chainInterfaceId));
            if (chainInterfaceRows.Length == 0)
            {
                return null;
            }
            string[] symmetryStrings = new string[2];
            symmetryStrings[0] = chainInterfaceRows[0]["SymmetryString1"].ToString().TrimEnd();
            symmetryStrings[1] = chainInterfaceRows[0]["SymmetryString2"].ToString().TrimEnd();
            return symmetryStrings;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="multiChainDomainIds"></param>
        /// <returns></returns>
        private DataTable GetEntryDomainInterfaceTable(string pdbId, long[] domainIds)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces " +
                " Where PdbID = '{0}' AND (DomainID1 IN ({1}) OR DomainID2 IN ({1})) " + 
                " AND ChainDomainID1 > 0 AND ChainDomainID2 > 0;", pdbId, ParseHelper.FormatSqlListString(domainIds));
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return domainInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="multiChainDomainIds"></param>
        /// <returns></returns>
        private DataTable GetEntryDomainInterfaceTable(string pdbId, int[] domainInterfaceIds)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces " +
                " Where PdbID = '{0}' AND DomainInterfaceID IN ({1});", pdbId, ParseHelper.FormatSqlListString(domainInterfaceIds));
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return domainInterfaceTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryMultiChainDomainInterfaceTable"></param>
        /// <returns></returns>
        private int[] GetEntryDomainInterfaceIds(DataTable entryDomainInterfaceTable)
        {
            List<int> domainInterfaceList = new List<int> ();
            int domainInterfaceId = 0;
            foreach (DataRow interfaceRow in entryDomainInterfaceTable.Rows)
            {
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());
                if (!domainInterfaceList.Contains(domainInterfaceId))
                {
                    domainInterfaceList.Add(domainInterfaceId);
                }
            }
            return domainInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainSymOps"></param>
        /// <returns></returns>
        private Dictionary<string, AtomInfo[]> GetSymOpDomainCoords(string pdbId, Dictionary<int, string[]> domainSymOpHash)
        {
            string gzDomainFile = "";
            string domainFile = "";
            string remarkString = "";
            Dictionary<string, AtomInfo[]> domainSymOpAtomHash = new Dictionary<string,AtomInfo[]> ();
            Dictionary<int, string> domainRemarkHash = new Dictionary<int,string> ();
            string spaceGroup = GetEntrySpaceGroup(pdbId);
            // set the transformation matrix, read from coordinate xml files
            string xmlFile = Path.Combine (ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string coordXmlFile = ParseHelper.UnZipFile (xmlFile, ProtCidSettings.tempDir );
            crystalBuilder.SetCartn2fractMatrix (coordXmlFile);
            File.Delete (coordXmlFile);

            string domainSymmetryString = "";
            string domainFileHashFolder = Path.Combine(pfamDomainFileDir, pdbId.Substring(1, 2));
            foreach (int domainId in domainSymOpHash.Keys)
            {
                if (domainId == 0)
                {
                    continue;
                }
                gzDomainFile = Path.Combine(domainFileHashFolder, pdbId + domainId.ToString() + ".pfam.gz");
                domainFile = ParseHelper.UnZipFile(gzDomainFile, ProtCidSettings.tempDir);
                AtomInfo[] domainAtoms = atomReader.ReadChainCoordFile(domainFile, out remarkString);
                File.Delete(domainFile);
                domainRemarkHash.Add(domainId, remarkString);
                string[] symmetryStrings = domainSymOpHash[domainId];
                foreach (string symmetryString in symmetryStrings)
                {
                    AtomInfo[] symOpDomainAtoms = crystalBuilder.TransformChainBySpecificSymOp(domainAtoms, spaceGroup, symmetryString);
                    domainSymmetryString = domainId.ToString() + "_" + symmetryString;
                    domainSymOpAtomHash.Add(domainSymmetryString, symOpDomainAtoms);
                }
            }
            return domainSymOpAtomHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEntrySpaceGroup(string pdbId)
        {
            string queryString = string.Format("Select SpaceGroup From PdbEntry Where PdbID = '{0}';", pdbId);
            DataTable sgTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string spaceGroup = "";
            if (sgTable.Rows.Count > 0)
            {
                spaceGroup = sgTable.Rows[0]["SpaceGroup"].ToString().TrimEnd();
            }
            if (spaceGroup == "" || spaceGroup == "-")
            {
                spaceGroup = "P 1";
            }
            return spaceGroup;
        }
        #endregion
        #endregion

        #region write intra-chain chain files
        private string intraChainFileDir = @"D:\DbProjectData\InterfaceFiles_update\intraChainFiles";
        /// <summary>
        /// the chain files for the intra-chain domain interactions
        /// this is one chain file, not for the domain interface files
        /// </summary>
        public void WriteIntraChainDomainInterfaceFiles()
        {
            int relSeqId = 14492;
            int clusterId = 1;
            string queryString = string.Format("Select Distinct PdbID, DomainInterfaceID From PfamDomainClusterInterfaces" +
                " WHere RelSeqID = {0} AND ClusterID = {1} AND InterfaceUnit = 'A-B';", relSeqId, clusterId );
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            Dictionary<string, List<string>> entryChainHash = GetEntryChainHash(domainInterfaceTable);
            string remark = "";
            string fileName = "";

            foreach (string pdbId in entryChainHash.Keys)
            {
                Dictionary<string, ChainAtoms> chainAtomsHash = atomReader.ReadAtomsOfChains(pdbId, entryChainHash[pdbId].ToArray ());
                foreach (string asymChain in entryChainHash[pdbId])
                {
                    remark = "REMARK  2  AsymChain " + asymChain;
                    fileName = Path.Combine(intraChainFileDir, pdbId + asymChain + ".pdb");
                    ChainAtoms chainAtoms = chainAtomsHash[asymChain];
                    WriteAtoms(fileName, pdbId, "A", chainAtoms.CartnAtoms, remark);
                }
            } 
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="intraChainDomainInterfaceTable"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetEntryChainHash(DataTable intraChainDomainInterfaceTable)
        {
            string pdbId = "";
            int domainInterfaceId = 0;
            string asymChain = "";
            Dictionary<string, List<string>> entryChainHash = new Dictionary<string,List<string>>  ();
            foreach (DataRow interfaceRow in intraChainDomainInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString ());
                asymChain = GetIntraChain(pdbId, domainInterfaceId);
                if (entryChainHash.ContainsKey(pdbId))
                {
                    if (!entryChainHash[pdbId].Contains(asymChain))
                    {
                        entryChainHash[pdbId].Add(asymChain);
                    }
                }
                else
                {
                    List<string> chainList = new List<string> ();
                    chainList.Add(asymChain);
                    entryChainHash.Add(pdbId, chainList);
                }
            }
            return entryChainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private string GetIntraChain(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select AsymChain1 From PfamDomainINterfaces Where PdbID = '{0}' AND DomainInterfaceID = {1};",
                pdbId, domainInterfaceId);
            DataTable chainTable = ProtCidSettings.protcidQuery.Query( queryString);
            string asymChain = chainTable.Rows[0]["AsymChain1"].ToString().TrimEnd ();
            return asymChain;
        }

        /// <summary>
        /// 
        /// </summary>
        public void WritePymolScriptForIntraChainDomainInterfaces()
        {
            int relSeqId = 14492;
            int clusterId = 1;
            string queryString = string.Format("Select Distinct PdbID, DomainInterfaceID From PfamDomainClusterInterfaces" +
                " WHere RelSeqID = {0} AND ClusterID = {1} AND InterfaceUnit = 'A-B' Order By PdbID, DomainInterfaceID;", relSeqId, clusterId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            StreamWriter pymolScriptWriter = new StreamWriter(Path.Combine(intraChainFileDir, "Trypsin_thrombin.pml"));
            string pdbId = "";
            int domainInterfaceId = 0;
            List<string> entryList = new List<string> ();
            string pymolScriptLine = "";
            string[] centerDomainRanges = new string[2];
            string[] domainRanges = new string[2];
            string alignLine = "";
            string intraChainFileName = "";
            string centerChainFileName = "";
            string centerPymolScriptLine = GetPymolScriptLine("1jwt", 7, out centerDomainRanges, out centerChainFileName);
            entryList.Add("1jwt");
            pymolScriptWriter.WriteLine(centerPymolScriptLine);
            foreach (DataRow interfaceRow in domainInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                if (entryList.Contains(pdbId))
                {
                    continue;
                }
                entryList.Add(pdbId);
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());

                pymolScriptLine = GetPymolScriptLine(pdbId, domainInterfaceId, out domainRanges, out intraChainFileName);

                pymolScriptWriter.WriteLine(pymolScriptLine);

                alignLine = "align " + intraChainFileName + " and chain A and resi " + domainRanges[0] + ", " +
                    centerChainFileName + " and chain A and resi " + centerDomainRanges[0] + "\r\n";
                alignLine = alignLine + "align " + intraChainFileName + " and chain A and resi " + domainRanges[1] + ", " +
                    centerChainFileName + " and chain A and resi " + centerDomainRanges[1];
                pymolScriptWriter.WriteLine(alignLine);

                pymolScriptWriter.WriteLine();
            }
            pymolScriptWriter.WriteLine("center " + centerChainFileName  + ".pdb");
            pymolScriptWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="chainFileName"></param>
        /// <returns></returns>
        private string GetPymolScriptLine(string pdbId, int domainInterfaceId, out string[] domainRanges, out string intraChainFileName)
        {
            string queryString = string.Format("Select * From PfamDomainINterfaces " + 
                " Where PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable interfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            long[] domainIds = new long[2];
            domainIds[0] = Convert.ToInt64 (interfaceTable.Rows[0]["DomainID1"].ToString ());
            domainIds[1] = Convert.ToInt64 (interfaceTable.Rows[0]["DomainID2"].ToString ());
            domainRanges = GetDomainRanges (pdbId, domainIds);
            string asymChain = interfaceTable.Rows[0]["AsymChain1"].ToString().TrimEnd();
       //     intraChainFileName = pdbId + asymChain + ".pdb";
            intraChainFileName = pdbId + asymChain;
            string pymolScriptLine = "";
            pymolScriptLine = "load " + intraChainFileName + ".pdb";
            pymolScriptLine += ("\r\n" + "hide lines, " + intraChainFileName);
            pymolScriptLine += ("\r\n" + "show cartoon, " + intraChainFileName);
            pymolScriptLine += ("\r\n" + "color gray60, " + intraChainFileName);
            pymolScriptLine += ("\r\n" + "color cyan, " + intraChainFileName + " and chain A and resi " + domainRanges[0]);
            pymolScriptLine += ("\r\n" + "color green, " + intraChainFileName + " and chain A and resi " + domainRanges[1]);
            return pymolScriptLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
        private string[] GetDomainRanges (string pdbId, long[] domainIds)
        {
            string queryString = string.Format("Select SeqStart, SeqEnd From PdbPfam " + 
                " Where PdbID = '{0}' AND DomainID IN ({1}) Order By SeqStart;", pdbId, ParseHelper.FormatSqlListString (domainIds));
            DataTable rangeTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] domainRanges = new string[2];
            domainRanges[0] = rangeTable.Rows[0]["SeqStart"].ToString() + "-" + rangeTable.Rows[0]["SeqEnd"].ToString();
            domainRanges[1] = rangeTable.Rows[1]["SeqStart"].ToString() + "-" + rangeTable.Rows[1]["SeqEnd"].ToString();
            return domainRanges;
        }
        #endregion

        #region for debug
        /// <summary>
        /// 
        /// </summary>
        public void WriteMissingDomainInterfaceFiles()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain Interface Files";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Write domain interface files");

            string queryString = "Select RelSeqID From PfamDomainFamilyRelation Order by RelSeqID;";
            DataTable relationTable = ProtCidSettings.protcidQuery.Query(queryString);


            int relSeqId = 0;
            foreach (DataRow relSeqRow in relationTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqRow["RelSeqID"].ToString());

                ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString());

                string[] entriesInRelation = GetMissingInterfacesEntriesInRelation (relSeqId);

                ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
                ProtCidSettings.progressInfo.totalOperationNum = entriesInRelation.Length;
                ProtCidSettings.progressInfo.totalStepNum = entriesInRelation.Length;


                foreach (string pdbId in entriesInRelation)
                {
                    ProtCidSettings.progressInfo.currentFileName = pdbId;
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    ProtCidSettings.progressInfo.currentStepNum++;

                    try
                    {
                        WriteDomainInterfaceFiles(pdbId, relSeqId, false);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId + " " + pdbId + " generate domain interface files errors: " +
                            ex.Message);
                        ProtCidSettings.logWriter.WriteLine(relSeqId + " " + pdbId + " generate domain interface files errors: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        public string[] GetMissingInterfacesEntriesInRelation(int relSeqId)
        {
            string queryString = string.Format("Select Distinct PdbId From PfamDomainInterfaces Where RelSeqID = {0} AND SurfaceArea < 0;",
                relSeqId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] entriesInRelation = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                entriesInRelation[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return entriesInRelation;
        }
        /// <summary>
        /// 
        /// </summary>
        public void WriteIntraDomainInterfaceFiles()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Domain Interface Files";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Write domain interface files");

            string queryString = "Select RelSeqID From PfamDomainFamilyRelation Order by RelSeqID;";
            DataTable relationTable = ProtCidSettings.protcidQuery.Query( queryString);


            int relSeqId = 0;
            foreach (DataRow relSeqRow in relationTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqRow["RelSeqID"].ToString());

                ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString());

                string[] entriesInRelation = GetEntriesInRelation(relSeqId);

                ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
                ProtCidSettings.progressInfo.totalOperationNum = entriesInRelation.Length;
                ProtCidSettings.progressInfo.totalStepNum = entriesInRelation.Length;


                foreach (string pdbId in entriesInRelation)
                {
                    ProtCidSettings.progressInfo.currentFileName = pdbId;
                    ProtCidSettings.progressInfo.currentOperationNum++;
                    ProtCidSettings.progressInfo.currentStepNum++;

                    try
                    {
                        WriteIntraChainDomainInterfaceFile (pdbId, relSeqId, false);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId + " " + pdbId + " generate domain interface files errors: " +
                            ex.Message);
                        ProtCidSettings.logWriter.WriteLine(relSeqId + " " + pdbId + " generate domain interface files errors: " + ex.Message);
                        ProtCidSettings.logWriter.Flush();
                    }
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        private DbUpdate dbUpdate = new DbUpdate();
        public void UpdateDomainInterfaceFiles()
        {
            Dictionary<int, Dictionary<string, List<int>>> updateRelDomainInterfaceHash = ReadUpdateRelationDomainInterfaces();
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update domain interfaces");
            ProtCidSettings.progressInfo.totalStepNum = updateRelDomainInterfaceHash.Count;
            ProtCidSettings.progressInfo.totalOperationNum = updateRelDomainInterfaceHash.Count;

            List<int> relSeqIdList = new List<int> (updateRelDomainInterfaceHash.Keys);
            relSeqIdList.Sort();
            foreach (int relSeqId in relSeqIdList)
            {
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                foreach (string pdbId in updateRelDomainInterfaceHash[relSeqId].Keys)
                {
                    ProtCidSettings.progressInfo.currentFileName = pdbId;

                    int[] domainInterfaceIds = updateRelDomainInterfaceHash[relSeqId][pdbId].ToArray();

                    try
                    {
                        ResetSurfaceArea(relSeqId, pdbId, domainInterfaceIds);

                        WriteDomainInterfaceFiles(pdbId, domainInterfaceIds, relSeqId, true);
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString () + " Generate domain interface files error: " + ex.Message);
                        ProtCidSettings.logWriter.WriteLine(relSeqId.ToString() + " Generate domain interface files error: " + ex.Message);
                    } 
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceIds"></param>
        private void ResetSurfaceArea(int relSeqId, string pdbId, int[] domainInterfaceIds)
        {
            string updateString = "";
            foreach (int domainInterfaceId in domainInterfaceIds)
            {
                updateString = string.Format("Update PfamDomainInterfaces Set SurfaceArea = -1 " + 
                    " Where RelSeqID = {0} AND PdbID = '{1}' AND DomainInterfaceID = {2};", relSeqId, pdbId, domainInterfaceId);
                dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<int, Dictionary<string, List<int>>> ReadUpdateRelationDomainInterfaces()
        {
            StreamReader dataReader = new StreamReader("RelationUpdateDomainInterfaces.txt");
            string line = "";
            Dictionary<int, Dictionary<string, List<int>>> relationEntryDomainInterfaceHash = new Dictionary<int,Dictionary<string,List<int>>> ();
            int relSeqId = 0;
            string pdbId = "";
            int domainInterfaceId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                relSeqId = Convert.ToInt32(fields[0]);
                for (int i = 1; i < fields.Length; i++)
                {
                    pdbId = fields[i].Substring(0, 4);
                    domainInterfaceId = Convert.ToInt32(fields[i].Substring (6, fields[i].Length - 6));
                    if (relationEntryDomainInterfaceHash.ContainsKey(relSeqId))
                    {
                        if (relationEntryDomainInterfaceHash[relSeqId].ContainsKey(pdbId))
                        {
                            if (!relationEntryDomainInterfaceHash[relSeqId][pdbId].Contains(domainInterfaceId))
                            {
                                relationEntryDomainInterfaceHash[relSeqId][pdbId].Add(domainInterfaceId);
                            }
                        }
                        else
                        {
                            List<int> domainInterfaceIdList = new List<int> ();
                            domainInterfaceIdList.Add(domainInterfaceId);
                            relationEntryDomainInterfaceHash[relSeqId].Add(pdbId, domainInterfaceIdList);
                        }
                    }
                    else
                    {
                        Dictionary<string, List<int>> entryDomainInterfaceHash = new Dictionary<string,List<int>> ();
                        List<int> domainInterfaceIdList = new List<int> ();
                        domainInterfaceIdList.Add(domainInterfaceId);
                        entryDomainInterfaceHash.Add(pdbId, domainInterfaceIdList);
                        relationEntryDomainInterfaceHash.Add(relSeqId, entryDomainInterfaceHash);
                    }
                }
            }
            dataReader.Close();
            return relationEntryDomainInterfaceHash;
        }
      
        #endregion

    }
}
