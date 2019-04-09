using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using ProtCidSettingsLib;
using DbLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamLigand
{
    public class PfamDnaRnaInteractions : PfamLigandInteractions
    {
        /// <summary>
        /// 
        /// </summary>
        public void RetrievePfamDnaRnaInteractions()
        {
            bool isUpdate = false;
            DataTable pfamDnaRnaTable = CreatePfamDnaRnaInteractTable(isUpdate);

            string queryString = "Select Distinct PdbID From ChainDnaRnas";
            DataTable dnaRnaEntryTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = dnaRnaEntryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = dnaRnaEntryTable.Rows.Count;
            ProtCidSettings.logWriter.WriteLine("Pfam-DNA/RNA");

            string pdbId = "";
            foreach (DataRow entryRow in dnaRnaEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    RetrieveChainDomainDnaRnaInteractions(pdbId, pfamDnaRnaTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Pfam-DNA/RNA done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        public string[] UpdatePfamDnaRnaInteractions(string[] updateEntriesWithDnaRna)
        {
            bool isUpdate = true;
            DataTable pfamDnaRnaTable = CreatePfamDnaRnaInteractTable(isUpdate);

 //           string[] updateEntriesWithDnaRna = GetUpdateEntriesWithDnaRna(updateEntries);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = updateEntriesWithDnaRna.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updateEntriesWithDnaRna.Length;
            ProtCidSettings.logWriter.WriteLine("Update Pfam-DNA/RNA!");

            foreach (string pdbId in updateEntriesWithDnaRna)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    DeleteEntryDnaRnasData(pfamDnaRnaTable.TableName, pdbId);
                    RetrieveChainDomainDnaRnaInteractions(pdbId, pfamDnaRnaTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Pfam-DNA/RNA update done!");
            ProtCidSettings.logWriter.Flush();

            return updateEntriesWithDnaRna;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="pdbId"></param>
        private void DeleteEntryDnaRnasData (string tableName, string pdbId)
        {
            string deleteString = string.Format("Delete From {0} Where PdbID = '{1}';", tableName, pdbId);
            dbUpdate.Delete (ProtCidSettings.protcidDbConnection, deleteString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        public string[] GetUpdateEntriesWithDnaRna(string[] updateEntries)
        {
            string queryString = "Select Distinct PdbID From ChainDnaRnas";
            DataTable dnaRnaEntryTable = ProtCidSettings.buCompQuery.Query(queryString);

            List<string> entryList = new List<string> ();
            string pdbId = "";
            foreach (DataRow entryRow in dnaRnaEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (Array.IndexOf(updateEntries, pdbId) > -1)
                {
                    entryList.Add(pdbId);
                }
            }
            entryList.Sort();
            return entryList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pfamLigandTable"></param>
        private void RetrieveChainDomainDnaRnaInteractions(string pdbId, DataTable pfamDnaRnaTable)
        {
            DataTable chainDnaRnaTable = GetChainDnaRnaInteractions (pdbId, true);
            if (chainDnaRnaTable.Rows.Count == 0)
            {
                return;
            }
            DataTable chainPfamTable = GetChainDomainTable(pdbId);
            int[] chainDomainIds = GetChainDomainIDs(chainPfamTable);
            string asymChain = "";
            int domainStart = 0;
            int domainEnd = 0;
            int seqStart = 0;
            int seqEnd = 0;
            int chainInteractSeqId = 0;
            foreach (int chainDomainId in chainDomainIds)
            {
                DataRow[] chainDomainRows = chainPfamTable.Select(string.Format("ChainDomainID = '{0}'", chainDomainId));
                foreach (DataRow chainDomainRow in chainDomainRows)
                {
                    domainStart = Convert.ToInt32(chainDomainRow["AlignStart"].ToString());
                    domainEnd = Convert.ToInt32(chainDomainRow["AlignEnd"].ToString());
                    seqStart = Convert.ToInt32(chainDomainRow["SeqStart"].ToString());
                    seqEnd = Convert.ToInt32(chainDomainRow["SeqEnd"].ToString());

                    asymChain = chainDomainRow["AsymChain"].ToString().TrimEnd();
                    DataRow[] chainDnaRnaRows = GetChainLigandInteractions(pdbId, asymChain, chainDnaRnaTable);

                    foreach (DataRow chainDnaRnaRow in chainDnaRnaRows)
                    {
                        chainInteractSeqId = Convert.ToInt32(chainDnaRnaRow["ChainSeqID"].ToString());
                        if (chainInteractSeqId <= seqEnd && chainInteractSeqId >= seqStart)
                        {
                            DataRow pfamDnaRnaRow = pfamDnaRnaTable.NewRow();
                            pfamDnaRnaRow["PdbID"] = pdbId;
                            pfamDnaRnaRow["ChainDomainID"] = chainDomainId;
                            pfamDnaRnaRow["PfamID"] = chainDomainRow["Pfam_ID"];
                            pfamDnaRnaRow["BuID"] = chainDnaRnaRow["BuID"];
                            pfamDnaRnaRow["DnaRnaChain"] = chainDnaRnaRow["AsymID"].ToString().TrimEnd();
                            pfamDnaRnaRow["DnaRnaSymmetryString"] = chainDnaRnaRow["SymmetryString"];
                            pfamDnaRnaRow["AsymChain"] = asymChain;
                            pfamDnaRnaRow["SymmetryString"] = chainDnaRnaRow["ChainSymmetryString"];
                            pfamDnaRnaRow["DnaRnaSeqID"] = chainDnaRnaRow["SeqID"];
                            pfamDnaRnaRow["SeqID"] = chainInteractSeqId;
                            pfamDnaRnaRow["HmmSeqID"] = pepInteract.MapSequenceSeqIdToHmmSeqId(chainInteractSeqId, chainDomainRow);
                            pfamDnaRnaTable.Rows.Add(pfamDnaRnaRow);
                        }
                    }
                }
            }
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, pfamDnaRnaTable);
            pfamDnaRnaTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buOnly">whether only using biological assemblies</param>
        /// <returns></returns>
        private DataTable GetChainDnaRnaInteractions(string pdbId, bool buOnly)
        {
            string queryString = "";
            DataTable chainDnaRnaTable = null;
            if (buOnly)
            {
                queryString = string.Format("Select * From ChainDnaRnas Where PdbID = '{0}' and BUID <> '0';", pdbId);
                chainDnaRnaTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            }
            else
            {
                queryString = string.Format("Select * From ChainDnaRnas Where PdbID = '{0}';", pdbId);
                chainDnaRnaTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            }
            return chainDnaRnaTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        /// <returns></returns>
        private DataTable CreatePfamDnaRnaInteractTable (bool isUpdate)
        {
            string[] pfamDnaRnaCols = { "PdbID", "BuID", "ChainDomainID", "PfamID", "DnaRnaChain", "DnaRnaSymmetryString", 
                                          "AsymChain", "SymmetryString", "DnaRnaSeqID", "SeqID", "HmmSeqID" };
            DataTable pfamDnaRnaTable = new DataTable("pfamDnaRnas");
            foreach (string col in pfamDnaRnaCols)
            {
                pfamDnaRnaTable.Columns.Add(new DataColumn(col));
            }
            if (!isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string createTableString = "Create Table " + pfamDnaRnaTable.TableName + " ( " +
                    " PdbID CHAR(4) NOT NULL, " +
                    " BuID VARCHAR(8) NOT NULL, " +
                    " PfamID VARCHAR(40) NOT NULL, " +
                    " ChainDomainID INTEGER NOT NULL, " +
                    " DnaRnaChain CHAR(3) NOT NULL, " +
                    " DnaRnaSymmetryString VARCHAR(15), " + 
                    " DnaRnaSeqID INTEGER NOT NULL, " +
                    " AsymChain CHAR(3) NOT NULL, " +
                    " SymmetryString VARCHAR(15), " +
                    " SeqID INTEGER NOT NULL, " +
                    " HmmSeqID INTEGER NOT NULL);";
                dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, pfamDnaRnaTable.TableName);

                string createIndexString = "CREATE INDEX " + pfamDnaRnaTable.TableName + "_idx1 ON " + pfamDnaRnaTable.TableName + "(PdbID, ChainDomainID);";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, pfamDnaRnaTable.TableName);
            }
            return pfamDnaRnaTable;
        }

    }
}
