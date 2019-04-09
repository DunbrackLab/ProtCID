using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using ProtCidSettingsLib;
using InterfaceClusterLib.DomainInterfaces.PfamPeptide;
using DbLib;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces.PfamLigand
{
    public class PfamLigandInteractions
    {
        #region member variables
        public DbQuery dbQuery = new DbQuery();
        public PfamPeptideInterfaces pepInteract = new PfamPeptideInterfaces();
        public DbInsert dbInsert = new DbInsert();
        public DbUpdate dbUpdate = new DbUpdate();
        #endregion

        #region pfam ligands interactions in db
        /// <summary>
        /// 
        /// </summary>
        public void RetrievePfamLigandInteractions()
        {
            bool isUpdate = false;
            DataTable pfamLigandTable = CreatePfamLigandInteractTable(isUpdate);

            string queryString = "Select Distinct PdbID From PdbPfamChain";
            DataTable pfamEntryTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = pfamEntryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = pfamEntryTable.Rows.Count;
            ProtCidSettings.logWriter.WriteLine("Retrieve Pfam-ligands info.");

            string pdbId = "";
            foreach (DataRow entryRow in pfamEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString ();

                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    RetrieveChainDomainLigandInteractions(pdbId, pfamLigandTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdatePfamLigandInteractions(string[] updateEntries)
        {
            bool isUpdate = true;
            DataTable pfamLigandTable = CreatePfamLigandInteractTable(isUpdate);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.logWriter.WriteLine("Update Pfam-ligands data!");

           foreach (string pdbId in updateEntries )
           {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                try
                {
                    DeletePfamLigandInfo (pdbId);
                    RetrieveChainDomainLigandInteractions(pdbId, pfamLigandTable);
                    ProtCidSettings.logWriter.WriteLine(pdbId);
                    ProtCidSettings.logWriter.Flush();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeletePfamLigandInfo(string pdbId)
        {
            string deleteString = string.Format("Delete From PfamLigands Where PdbID = '{0}';", pdbId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pfamLigandTable"></param>
        private void RetrieveChainDomainLigandInteractions (string pdbId, DataTable pfamLigandTable)
        {
            DataTable chainLigandTable = GetChainLigandInteractions(pdbId);
            if (chainLigandTable.Rows.Count == 0)
            {
                return;
            }
            // outside key: asymchain, inside key: uniprot, value: string[][] pdb sequential numbers, uniprot seq numbers
            Dictionary<string, Dictionary<string, string[][]>> entryChainUnpSeqMapDict = GetEntryChainSeqMapDict(pdbId);
            DataTable chainPfamTable = GetChainDomainTable(pdbId);
            int[] chainDomainIds = GetChainDomainIDs (chainPfamTable );
            string asymChain = "";
            int domainStart = 0;
            int domainEnd = 0;
            int seqStart = 0;
            int seqEnd = 0;
            int chainInteractSeqId = 0;
            foreach (int chainDomainId in chainDomainIds)
            {
                DataRow[] chainDomainRows = chainPfamTable.Select(string.Format ("ChainDomainID = '{0}'", chainDomainId));
                foreach (DataRow chainDomainRow in chainDomainRows)
                {
                    domainStart = Convert.ToInt32(chainDomainRow["AlignStart"].ToString ());
                    domainEnd = Convert.ToInt32(chainDomainRow["AlignEnd"].ToString ());
                    seqStart = Convert.ToInt32(chainDomainRow["SeqStart"].ToString ());
                    seqEnd = Convert.ToInt32(chainDomainRow["SeqEnd"].ToString ());

                    asymChain = chainDomainRow["AsymChain"].ToString().TrimEnd();
                    DataRow[] chainLigandRows = GetChainLigandInteractions(pdbId, asymChain, chainLigandTable);
                    
                    foreach (DataRow chainLigandRow in chainLigandRows)
                    {
                        chainInteractSeqId = Convert.ToInt32(chainLigandRow["ChainSeqID"].ToString ());
                        if (chainInteractSeqId <= seqEnd && chainInteractSeqId >= seqStart)
                        {
                            DataRow pfamLigandRow = pfamLigandTable.NewRow();
                            pfamLigandRow["PdbID"] = pdbId;
                            pfamLigandRow["ChainDomainID"] = chainDomainId;
                            pfamLigandRow["PfamID"] = chainDomainRow["Pfam_ID"];
                            pfamLigandRow["LigandChain"] = chainLigandRow["AsymID"].ToString().TrimEnd();
                            pfamLigandRow["AsymChain"] = asymChain;
                            pfamLigandRow["LigandSeqID"] = chainLigandRow["SeqID"];
                            pfamLigandRow["SeqID"] = chainInteractSeqId;
                            pfamLigandRow["HmmSeqID"] = pepInteract.MapSequenceSeqIdToHmmSeqId(chainInteractSeqId, chainDomainRow);
                            string[] unpSeq = GetUnpSeqId(asymChain, chainInteractSeqId.ToString (), entryChainUnpSeqMapDict);
                            pfamLigandRow["UnpCode"] = unpSeq[0];
                            pfamLigandRow["UnpSeqID"] = unpSeq[1];
                            pfamLigandTable.Rows.Add(pfamLigandRow);
                        }
                    }
                }
            }
            dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, pfamLigandTable);
            pfamLigandTable.Clear();
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="chainLigandTable"></param>
        /// <returns></returns>
        public DataRow[] GetChainLigandInteractions(string pdbId, string asymChain, DataTable chainLigandTable)
        {
            DataRow[] chainLigandRows = chainLigandTable.Select(string.Format ("PdbID = '{0}' AND ChainAsymID = '{1}'", pdbId, asymChain));
            return chainLigandRows;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainPfamTable"></param>
        /// <returns></returns>
        public int[] GetChainDomainIDs(DataTable chainPfamTable)
        {
            List<int> chainDomainIdList = new List<int> ();
            int chainDomainId = 0;
            foreach (DataRow chainDomainRow in chainPfamTable.Rows)
            {
                chainDomainId = Convert.ToInt32(chainDomainRow["ChainDomainID"].ToString ());
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
        /// <returns></returns>
        public DataTable GetChainDomainTable(string pdbId)
        {
            string queryString = string.Format("Select PdbPfam.EntityID, Pfam_ID, ChainDomainID, AsymChain, SeqStart, SeqEnd, AlignStart, AlignEnd, " + 
                " HmmStart, HmmEnd, QueryAlignment, HmmAlignment " +
                " From PdbPfam, PdbPfamChain " +
                " Where PdbPfamChain.PdbID = '{0}' " +
                " AND PdbPfamChain.PdbID = PdbPfam.PdbID AND PdbPfamChain.EntityID = PdbPfam.EntityID AND PdbPfamChain.DomainID = PdbPfam.DomainID;",
                pdbId);
            DataTable chainDomainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            return chainDomainTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetChainLigandInteractions(string pdbId)
        {
            string queryString = string.Format("Select * From ChainLigands Where PdbID = '{0}';", pdbId);
            DataTable chainLigandTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            return chainLigandTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        /// <returns></returns>
        private DataTable CreatePfamLigandInteractTable(bool isUpdate)
        {
            string[] pfamLigandCols = {"PdbID", "ChainDomainID", "PfamID", "LigandChain", "AsymChain", "LigandSeqID", "SeqID", "HmmSeqID", "UnpCode", "UnpSeqID"};
            DataTable pfamLigandTable = new DataTable("pfamLigands");
            foreach (string col in pfamLigandCols)
            {
                pfamLigandTable.Columns.Add(new DataColumn (col));
            }
            if (!isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string createTableString = "Create Table " + pfamLigandTable.TableName + " ( " +
                    " PdbID CHAR(4) NOT NULL, " +
                    " PfamID VARCHAR(40) NOT NULL, " +
                    " ChainDomainID INTEGER NOT NULL, " +
                    " LigandChain CHAR(3) NOT NULL, " +
                    " AsymChain CHAR(3) NOT NULL, " +
                    " LigandSeqID INTEGER NOT NULL, " +
                    " SeqID INTEGER NOT NULL, " +
                    " HmmSeqID INTEGER NOT NULL, " +
                    " UnpCode VARCHAR(40), " +
                    " UnpSeqID INTEGER);";
                dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, pfamLigandTable.TableName);

                string createIndexString = "CREATE INDEX " + pfamLigandTable.TableName + "_idx1 ON " + pfamLigandTable.TableName + "(PdbID, ChainDomainID);";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, pfamLigandTable.TableName);
            }
            return pfamLigandTable;
        }
        #endregion

        #region add uniprot info to table 
        private void AddUnpSeqInfoToPfamLigandsTable(string pdbId)
        {
            string asymChain = "";
            string seqId = "";
            string queryString = string.Format("Select PdbID, AsymChain, SeqID From PfamLigands Where PdbID = '{0}';", pdbId);
            DataTable pdbChainSeqTable = ProtCidSettings.protcidQuery.Query(queryString);
            DataTable ligandChainUnpSeqTable = pdbChainSeqTable.Clone();
            ligandChainUnpSeqTable.Columns.Add(new DataColumn("UnpCode"));
            ligandChainUnpSeqTable.Columns.Add(new DataColumn("UnpSeqID"));
            Dictionary<string, Dictionary<string, string[][]>> entryChainUnpSeqMapDict = GetEntryChainSeqMapDict(pdbId);

            foreach (DataRow seqRow in pdbChainSeqTable.Rows)
            {
                asymChain = seqRow["AsymChain"].ToString().TrimEnd();
                seqId = seqRow["SeqID"].ToString();
                string[] unpSeq = GetUnpSeqId(asymChain, seqId, entryChainUnpSeqMapDict);
                if (unpSeq[0] != "" && unpSeq[1] != "")
                {
                    AddUnpSeqInfo(pdbId, asymChain, seqId, unpSeq[0], unpSeq[1]);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="seqId"></param>
        /// <param name="unpCode"></param>
        /// <param name="unpSeqId"></param>
        private void AddUnpSeqInfo(string pdbId, string asymChain, string seqId, string unpCode, string unpSeqId)
        {
            string updateString = string.Format("Update PfamLigands Set UnpCode = '{0}', UnpSeqId = {1} " +
                " Where PdbId = '{2}' AND AsymChain = '{3}' AND SeqID = {4};", unpCode, unpSeqId, pdbId, asymChain, seqId);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns>key: protein asymchain, value: dictionary, key: uniprot, value: string[][]: pdb seq numbers and uniprot seq numbers</returns>
        private Dictionary<string, Dictionary<string, string[][]>> GetEntryChainSeqMapDict(string pdbId)
        {
            string asymChain = "";
            string unpCode = "";
            string seqNumbers = "";
            string dbSeqNumbers = "";
            int orgSeqNumberLen = 0;
            string queryString = string.Format("Select PdbDbRefSifts.PdbID, DbCode, AsymID, SeqNumbers, DbSeqNumbers " +
                " From PdbDbRefSifts, PdbDbRefSeqAlignSifts " +
                " Where PdbDbRefSifts.PdbID = '{0}' AND DbName = 'UNP' AND PdbDbRefSifts.PdbID = PDbDbRefSeqAlignSifts.PdbID AND " +
                " PdbDbRefSifts.RefID = PdbDbRefSeqAlignSifts.RefID;", pdbId);
            DataTable pdbDbSeqAlignTable = ProtCidSettings.pdbfamQuery.Query(queryString);
            Dictionary<string, Dictionary<string, string[][]>> entryChainSeqMapDict = new Dictionary<string, Dictionary<string, string[][]>>();
            foreach (DataRow seqAlignRow in pdbDbSeqAlignTable.Rows)
            {
                asymChain = seqAlignRow["AsymID"].ToString().TrimEnd();
                unpCode = seqAlignRow["DbCode"].ToString().TrimEnd();
                seqNumbers = seqAlignRow["SeqNumbers"].ToString();
                dbSeqNumbers = seqAlignRow["DbSeqNumbers"].ToString();
                string[][] seqDbSeqNumbers = new string[2][];
                seqDbSeqNumbers[0] = seqNumbers.Split(',');
                seqDbSeqNumbers[1] = dbSeqNumbers.Split(',');
                if (entryChainSeqMapDict.ContainsKey(asymChain))
                {
                    Dictionary<string, string[][]> chainSeqMapDict = entryChainSeqMapDict[asymChain];
                    if (chainSeqMapDict.ContainsKey(unpCode))
                    {
                        string[][] chainSeqNumbers = chainSeqMapDict[unpCode];
                        orgSeqNumberLen = chainSeqNumbers[0].Length;
                        Array.Resize(ref chainSeqNumbers[0], orgSeqNumberLen + seqDbSeqNumbers[0].Length);
                        Array.Copy(seqDbSeqNumbers[0], 0, chainSeqNumbers[0], orgSeqNumberLen, seqDbSeqNumbers[0].Length);
                        orgSeqNumberLen = chainSeqNumbers[1].Length;
                        Array.Resize(ref chainSeqNumbers[1], chainSeqNumbers[1].Length + seqDbSeqNumbers[1].Length);
                        Array.Copy(seqDbSeqNumbers[1], 0, chainSeqNumbers[1], orgSeqNumberLen, seqDbSeqNumbers[1].Length);
                    }
                    else
                    {
                        chainSeqMapDict.Add(unpCode, seqDbSeqNumbers);
                    }
                }
                else
                {
                    Dictionary<string, string[][]> chainSeqMapDict = new Dictionary<string, string[][]>();
                    chainSeqMapDict.Add(unpCode, seqDbSeqNumbers);
                    entryChainSeqMapDict.Add(asymChain, chainSeqMapDict);
                }
            }
            return entryChainSeqMapDict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain"></param>
        /// <param name="seqId"></param>
        /// <param name="entryChainUnpSeqMapDict"></param>
        /// <returns></returns>
        private string[] GetUnpSeqId(string asymChain, string seqId, Dictionary<string, Dictionary<string, string[][]>> entryChainUnpSeqMapDict)
        {
            int seqIndex = -1;
            string dbSeqId = "";
            int intDbSeqId = 0;
            string chainUnpCode = "";
            if (entryChainUnpSeqMapDict.ContainsKey(asymChain))
            {
                Dictionary<string, string[][]> chainSeqMapDict = entryChainUnpSeqMapDict[asymChain];
                foreach (string unpCode in chainSeqMapDict.Keys)
                {
                    string[][] chainDbSeqNumbers = chainSeqMapDict[unpCode];
                    seqIndex = Array.IndexOf(chainDbSeqNumbers[0], seqId);
                    if (seqIndex > -1)
                    {
                        dbSeqId = chainDbSeqNumbers[1][seqIndex];
                        if (int.TryParse(dbSeqId, out intDbSeqId))
                        {
                            chainUnpCode = unpCode;
                            break;
                        }
                    }
                }
            }
            string[] unpSeq = new string[2];
            if (chainUnpCode == "")
            {
                unpSeq[0] = "-";
            }
            else
            {
                unpSeq[0] = chainUnpCode;
            }
            if (dbSeqId == "")
            {
                unpSeq[1] = "-1";
            }
            else
            {
                unpSeq[1] = dbSeqId;
            }
            return unpSeq;
        }
        #endregion

        #region ligands in pdb
        /// <summary>
        /// 
        /// </summary>
        public void GetLigandsInPdb()
        {
            bool isUpdate = false;
            DataTable pdbLigandTable = CreatePdbLigandsTable(isUpdate);

            string queryString = "Select PdbID, AsymID, AuthorChain, Sequence, Name, PolymerType, " + 
                " PolymerStatus, NdbSeqNumbers, PdbSeqNumbers, AuthSeqNumbers From AsymUnit;";
            DataTable asuLigandTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "PDB Ligands";
            ProtCidSettings.logWriter.WriteLine("Retrieve PDB-ligand info.");

            DataRow pdbLigandRow = pdbLigandTable.NewRow ();
            ParseLigandInPdb(asuLigandTable, pdbLigandRow);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("PDB-ligands Done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateLigandsInPdb(string[] updateEntries)
        {
            bool isUpdate = true;
            DataTable pdbLigandTable = CreatePdbLigandsTable(isUpdate);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "PDB Ligands";
            ProtCidSettings.logWriter.WriteLine("Update PDB-ligand info.");

            string queryString = "";
            foreach (string pdbId in updateEntries)
            {
                queryString = string.Format ("Select PdbID, AsymID, AuthorChain, Sequence, Name, PolymerType, " +
                            " PolymerStatus, NdbSeqNumbers, PdbSeqNumbers, AuthSeqNumbers From AsymUnit Where PdbID = '{0}';", pdbId);
                DataTable asuLigandTable = ProtCidSettings.pdbfamQuery.Query( queryString);

                DeletePdbLigands(pdbId);

                DataRow pdbLigandRow = pdbLigandTable.NewRow();
                ParseLigandInPdb(asuLigandTable, pdbLigandRow);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("PDB-ligands Update Done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeletePdbLigands(string pdbId)
        {
            string deleteString = string.Format("Delete From PdbLigands WHere PdbID = '{0}';", pdbId);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="asuLigandTable"></param>
        /// <param name="pdbLigandRow"></param>
        private void ParseLigandInPdb(DataTable asuLigandTable, DataRow pdbLigandRow)
        {
            string polymerType = "";
            string polymerStatus = "";
            string ligand = "";
            string sequence = "";
            string ndbSeqNumberString = "";
            string pdbSeqNumberString = "";
            string authSeqNumberString = "";

            foreach (DataRow asuRow in asuLigandTable.Rows)
            {
                polymerType = asuRow["PolymerType"].ToString().TrimEnd().ToLower();
                if (polymerType == "polypeptide")
                {
                    continue;
                }
                polymerStatus = asuRow["PolymerStatus"].ToString().TrimEnd();
                if (polymerStatus == "water")
                {
                    continue;
                }
                ligand = "";
                try
                {
                    if (polymerType == "polydeoxyribonucleotide")
                    {
                        ligand = "DNA";
                        pdbLigandRow["PdbID"] = asuRow["PdbID"];
                        pdbLigandRow["AsymChain"] = asuRow["AsymID"];
                        pdbLigandRow["AuthorChain"] = asuRow["AuthorChain"];
                        pdbLigandRow["Ligand"] = ligand;
                        pdbLigandRow["Name"] = asuRow["Name"];
                        pdbLigandRow["SeqID"] = -1;
                        pdbLigandRow["PdbSeqID"] = -1;
                        pdbLigandRow["AuthSeqID"] = -1;
                        dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, pdbLigandRow);
                    }
                    else if (polymerType == "polyribonucleotide")
                    {
                        ligand = "RNA";

                        pdbLigandRow["PdbID"] = asuRow["PdbID"];
                        pdbLigandRow["AsymChain"] = asuRow["AsymID"];
                        pdbLigandRow["AuthorChain"] = asuRow["AuthorChain"];
                        pdbLigandRow["Ligand"] = ligand;
                        pdbLigandRow["Name"] = asuRow["Name"];
                        pdbLigandRow["SeqID"] = -1;
                        pdbLigandRow["PdbSeqID"] = -1;
                        pdbLigandRow["AuthSeqID"] = -1;
                        dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, pdbLigandRow);
                    }
                    else
                    {
                        sequence = asuRow["Sequence"].ToString();
                        ndbSeqNumberString = asuRow["NdbSeqNumbers"].ToString().TrimEnd();
                        string[] ndbSeqNumbers = ndbSeqNumberString.Split(',');
                        pdbSeqNumberString = asuRow["PdbSeqNumbers"].ToString().TrimEnd();
                        string[] pdbSeqNumbers = pdbSeqNumberString.Split(',');
                        authSeqNumberString = asuRow["AuthSeqNumbers"].ToString().TrimEnd();
                        string[] authSeqNumbers = authSeqNumberString.Split(',');
                        if (sequence.IndexOf("HOH") > -1) // error from pdb, skip the entry: 3N55, 4FFH
                        {
                            continue;
                        }

                        int ligandLength = sequence.Length / ndbSeqNumbers.Length;
                        int count = 0;
                        for (int i = 0; i < sequence.Length; i += ligandLength)
                        {
                            try
                            {
                                ligand = sequence.Substring(i, ligandLength);
                                pdbLigandRow["PdbID"] = asuRow["PdbID"];
                                pdbLigandRow["AsymChain"] = asuRow["AsymID"];
                                pdbLigandRow["AuthorChain"] = asuRow["AuthorChain"];
                                pdbLigandRow["Ligand"] = ligand;
                                pdbLigandRow["Name"] = asuRow["Name"];
                                pdbLigandRow["SeqID"] = ndbSeqNumbers[count];
                                pdbLigandRow["PdbSeqID"] = pdbSeqNumbers[count];
                                pdbLigandRow["AuthSeqID"] = authSeqNumbers[count];
                                dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, pdbLigandRow);
                                count++;
                            }
                            catch (Exception ex)
                            {
                                ProtCidSettings.progressInfo.progStrQueue.Enqueue(ParseHelper.FormatDataRow(asuRow) + "\r\nRetrieving non-DNA/RNA ligand info error: " + ex.Message);
                                ProtCidSettings.logWriter.WriteLine(ParseHelper.FormatDataRow(asuRow) + "\r\nRetrieving non-DNA/RNA ligand info error: " + ex.Message);
                                ProtCidSettings.logWriter.Flush();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ParseHelper.FormatDataRow (asuRow) + "\r\nRetrieving ligand info error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(ParseHelper.FormatDataRow(asuRow) + "\r\nRetrieving ligand info error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        private DataTable CreatePdbLigandsTable(bool isUpdate)
        {
            string[] pdbLigandCols = {"PdbID", "AsymChain", "AuthorChain", "Ligand", "Name", "SeqID", "PdbSeqID", "AuthSeqID"};
            DataTable pdbLigandTable = new DataTable("PdbLigands");
            foreach (string ligandCol in pdbLigandCols)
            {
                pdbLigandTable.Columns.Add(new DataColumn (ligandCol));
            }

            if (!isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string createTableString = "Create Table " + pdbLigandTable.TableName + "( " +
                    "PdbID CHAR(4) NOT NULL, " +
                    "Ligand CHAR(3) NOT NULL, " +
                    "AsymChain CHAR(3) NOT NULL, " +
                    "AuthorChain CHAR(4) NOT NULL, " +
                    "Name BLOB SUB_TYPE 1 SEGMENT SIZE 80 NOT NULL, " +
                    "SeqID INTEGER NOT NULL, " + 
                    "PdbSeqID INTEGER NOT NULL, " +
                    "AuthSeqID INTEGER NOT NULL " +
                    " );";
                dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, pdbLigandTable.TableName);

                string indexString = "CREATE INDEX " + pdbLigandTable.TableName + "_idx1 ON " + pdbLigandTable.TableName + "(PdbID, SeqID);";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, pdbLigandTable.TableName);

                indexString = "CREATE INDEX " + pdbLigandTable.TableName + "_idx2 ON " + pdbLigandTable.TableName + "(Ligand);";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, pdbLigandTable.TableName);
            }
            return pdbLigandTable;
        }
        #endregion

        #region summary info -- ligands
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable CreatePfamLigandSumTable(bool isUpdate)
        {
            string[] pfamLigandSumCols = { "Ligand", "Name", "NumEntry", "NumIEntry", "NumIPfam" };
            DataTable pfamLigandSumTable = new DataTable("PfamLigandsSumInfo");
            foreach (string col in pfamLigandSumCols)
            {
                pfamLigandSumTable.Columns.Add(new DataColumn(col));
            }


            if (!isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string createTableString = "Create Table " + pfamLigandSumTable.TableName + " ( " +
                    "Ligand CHAR(7) NOT NULL, " +
                    "Name BLOB SUB_TYPE 1 SEGMENT SIZE 80 NOT NULL, " +
                    "NumEntry INTEGER NOT NULL, " +
                    "NumIEntry INTEGER NOT NULL, " +
                    "NumIPfam INTEGER NOT NULL);";
                dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, pfamLigandSumTable.TableName);

                string createIndexString = "CREATE INDEX " + pfamLigandSumTable.TableName + "_idx1 ON " + pfamLigandSumTable.TableName + "(ligand);";
                dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, pfamLigandSumTable.TableName);
            }
            return pfamLigandSumTable;
        }

        /// <summary>
        /// 
        /// </summary>
        public void GetPfamLigandInteractionSumInfo()
        {
            DataTable pfamLigandSumTable = CreatePfamLigandSumTable(false);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Pfam-Ligand Sum info";
            ProtCidSettings.logWriter.WriteLine("Pfam-Ligand sum info.");

            DataTable ligandTable = GetLigandTable(null);
            string[] pdbLigands = GetPdbLigands(ligandTable);
         //   string[] pdbLigands = {"DNA", "RNA"};

            ProtCidSettings.progressInfo.totalStepNum = pdbLigands.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pdbLigands.Length;

            DataRow sumInfoRow = pfamLigandSumTable.NewRow();
            int[] numbersEntryPfam = null;

            string ligandName = "";
            foreach (string ligandId in pdbLigands)
            {
                ProtCidSettings.progressInfo.currentFileName = ligandId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    numbersEntryPfam = GetEntryPfamNumbersWithLigand(ligandId, ligandTable);
                    ligandName = GetLigandName(ligandId, ligandTable);

                    sumInfoRow["Ligand"] = ligandId;
                    sumInfoRow["Name"] = ligandName;
                    sumInfoRow["NumEntry"] = numbersEntryPfam[0];
                    sumInfoRow["NumIEntry"] = numbersEntryPfam[1];
                    sumInfoRow["NumIPfam"] = numbersEntryPfam[2];
                    dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, sumInfoRow);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ligandId + " get pfam-ligand interaction sum info error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(ligandId + " get pfam-ligand interaction sum info error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Pfam-Ligand sum info done!");
            ProtCidSettings.logWriter.Flush();
       }

        /// <summary>
        /// 
        /// </summary>
        public void GetPfamLigandInteractionSumInfo(string[] pdbLigands)
        {
            DataTable pfamLigandSumTable = CreatePfamLigandSumTable(true);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Pfam-Ligand Sum info";
            ProtCidSettings.logWriter.WriteLine("Pfam-Ligand sum info.");

            DataTable ligandTable = GetLigandTable(null);

            ProtCidSettings.progressInfo.totalStepNum = pdbLigands.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pdbLigands.Length;

            DataRow sumInfoRow = pfamLigandSumTable.NewRow();
            int[] numbersEntryPfam = null;

            string ligandName = "";
            foreach (string ligandId in pdbLigands)
            {
                ProtCidSettings.progressInfo.currentFileName = ligandId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    numbersEntryPfam = GetEntryPfamNumbersWithLigand(ligandId, ligandTable);
                    ligandName = GetLigandName(ligandId, ligandTable);

                    sumInfoRow["Ligand"] = ligandId;
                    sumInfoRow["Name"] = ligandName;
                    sumInfoRow["NumEntry"] = numbersEntryPfam[0];
                    sumInfoRow["NumIEntry"] = numbersEntryPfam[1];
                    sumInfoRow["NumIPfam"] = numbersEntryPfam[2];
                    dbInsert.InsertDataIntoDb(ProtCidSettings.protcidDbConnection, sumInfoRow);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ligandId + " get pfam-ligand interaction sum info error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(ligandId + " get pfam-ligand interaction sum info error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Pfam-Ligand sum info done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligand"></param>
        /// <returns></returns>
        private bool IsLigandInDb(string ligand)
        {
            string queryString = string.Format("Select * From PfamLigandsSumInfo Where Ligand = '{0}';", ligand);
            DataTable pfamLigandTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (pfamLigandTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandId"></param>
        /// <param name="ligandTable"></param>
        /// <returns></returns>
        private string GetLigandName(string ligandId, DataTable ligandTable)
        {
            DataRow[] ligandRows = ligandTable.Select (string.Format ("Ligand = '{0}'", ligandId));
            if (ligandRows.Length > 0)
            {
                return ligandRows[0]["Name"].ToString().TrimEnd();
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligand"></param>
        /// <param name="ligandInfoTable"></param>
        /// <returns></returns>
        private int[] GetEntryPfamNumbersWithLigand(string ligand, DataTable ligandInfoTable)
        {
            DataRow[] ligandRows = ligandInfoTable.Select(string.Format ("Ligand = '{0}'", ligand));
            List<string> entryList = new List<string>();
            List<string> iPfamList = new List<string>();
            List<string> iEntryList = new List<string>();
            string pdbId = "";
            string asymId = "";
            string[] IPfamIds = null;
            int ligandSeqId = 0;
            foreach (DataRow ligandRow in ligandRows)
            {
                pdbId = ligandRow["PdbID"].ToString();
                asymId = ligandRow["AsymChain"].ToString().TrimEnd();
                ligandSeqId = Convert.ToInt32(ligandRow["SeqID"].ToString ());

                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }

                if (ligand == "DNA" || ligand == "RNA")
                {
                    IPfamIds = GetInteractingDnaRnaPfamIds (pdbId, asymId);
                }
                else
                {
                    IPfamIds = GetInteractingPfamIds(pdbId, asymId, ligandSeqId);
                }
               
                foreach (string iPfamId in IPfamIds)
                {
                    if (!iPfamList.Contains(iPfamId))
                    {
                        iPfamList.Add(iPfamId);
                    }
                }

                if (IPfamIds.Length > 0)
                {
                    if (!iEntryList.Contains(pdbId))
                    {
                        iEntryList.Add(pdbId);
                    }
                }
            }
            int[] numbersEntryPfam = new int[3];
            numbersEntryPfam[0] = entryList.Count;
            numbersEntryPfam[1] = iEntryList.Count;
            numbersEntryPfam[2] = iPfamList.Count;
            return numbersEntryPfam;
        }
    
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="ligandAsymId"></param>
        /// <returns></returns>
        private string[] GetInteractingPfamIds(string pdbId, string ligandAsymId, int ligandSeqId )
        {
            string queryString = string.Format("Select Distinct PfamId From PfamLigands Where PdbID = '{0}' AND LigandChain = '{1}' AND LigandSeqId = {2};",
                 pdbId, ligandAsymId, ligandSeqId);
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] pfamIds = new string[pfamIdTable.Rows.Count];
            int count = 0;
            string pfamId = "";
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                pfamId = pfamIdRow["PfamID"].ToString().TrimEnd();
                pfamIds[count] = pfamId;
                count++;
            }
            return pfamIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="dnaRnaAsymId"></param>
        /// <returns></returns>
        private string[] GetInteractingDnaRnaPfamIds(string pdbId, string dnaRnaAsymId)
        {
            string queryString = string.Format("Select Distinct PfamId From PfamDnaRnas Where PdbID = '{0}' AND DnaRnaChain = '{1}';",
                 pdbId, dnaRnaAsymId);
            DataTable pfamIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] pfamIds = new string[pfamIdTable.Rows.Count];
            int count = 0;
            string pfamId = "";
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                pfamId = pfamIdRow["PfamID"].ToString().TrimEnd();
                pfamIds[count] = pfamId;
                count++;
            }
            return pfamIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <returns></returns>
        private DataTable GetLigandTable(string[] updateEntries)
        {
            string queryString = "";
            DataTable ligandTable = null;
            if (updateEntries == null)
            {
                queryString = "Select PdbID, AsymChain, Ligand, Name, SeqID From PdbLigands;";
                ligandTable = ProtCidSettings.protcidQuery.Query( queryString);
            }
            else
            {
                foreach (string pdbId in updateEntries)
                {
                    queryString = string.Format("Select PdbID, AsymChain, Ligand, Name, SeqID From PdbLigands Where PdbID = '{0}';", pdbId);
                    DataTable entryLigandTable = ProtCidSettings.protcidQuery.Query( queryString);
                    ParseHelper.AddNewTableToExistTable(entryLigandTable, ref ligandTable);
                }
            }
            return ligandTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbLigandTable"></param>
        /// <returns></returns>
        private string[] GetPdbLigands(DataTable pdbLigandTable)
        {
            List<string> ligandList = new List<string>();
            string ligandId = "";
            foreach (DataRow ligandRow in pdbLigandTable.Rows)
            {
                ligandId = ligandRow["Ligand"].ToString().TrimEnd();
                if (!ligandList.Contains(ligandId))
                {
                    ligandList.Add(ligandId);
                }
            }
            string[] pdbLigands = new string[ligandList.Count];
            ligandList.CopyTo(pdbLigands);
            return pdbLigands;
        }

        #region update
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdatePfamLigandInteractionSumInfo(string[] updateEntries)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Retrieving Pfam Ligand sum info";
            DataTable pfamLigandSumTable = CreatePfamLigandSumTable(false);

            DataTable ligandTable = GetLigandTable(null);
            DataTable updateLigandTable = GetLigandTable(updateEntries);
            string[] pdbLigands = GetPdbLigands(updateLigandTable);

            ProtCidSettings.progressInfo.totalOperationNum = pdbLigands.Length;
            ProtCidSettings.progressInfo.totalStepNum = pdbLigands.Length;
            ProtCidSettings.logWriter.WriteLine ("Retrieving Pfam Ligand Sum Info");

            string ligandName = "";
            int[] numbersEntryPfam = null;

            foreach (string ligandId in pdbLigands)
            {
                DeleteLigandSumInfo(ligandId);

                numbersEntryPfam = GetEntryPfamNumbersWithLigand(ligandId, ligandTable);
                ligandName = GetLigandName(ligandId, ligandTable);

                DataRow sumInfoRow = pfamLigandSumTable.NewRow();
                sumInfoRow["Ligand"] = ligandId;
                sumInfoRow["Name"] = ligandName;
                sumInfoRow["NumEntry"] = numbersEntryPfam[0];
                sumInfoRow["NumIEntry"] = numbersEntryPfam[1];
                sumInfoRow["NumIPfam"] = numbersEntryPfam[2];
                pfamLigandSumTable.Rows.Add(sumInfoRow);
            }
            dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, pfamLigandSumTable);
            pfamLigandSumTable.Clear();
            ProtCidSettings.logWriter.WriteLine ("Done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligand"></param>
        private void DeleteLigandSumInfo(string ligand)
        {
            string deleteString = string.Format("Delete From PfamLigandsSumInfo Where Ligand = '{0}';", ligand);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }
        #endregion
        #endregion

        #region summary info - ligand and pfam pairs
        /// <summary>
        /// 
        /// </summary>
        public void PrintLigandInteractingPfamsSumInfo()
        {
            CreateLigandPfamPairTableInDb();
            DataTable ligandPfamPairTable = CreatLIgandPfamPairSumInfoTable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Pfam-Ligand Pair Sum info";
            ProtCidSettings.logWriter.WriteLine("Pfam-Ligand pair sum info");

            DataTable ligandTable = GetLigandTable(null);
            string[] pdbLigands = GetPdbLigands(ligandTable);

            ProtCidSettings.progressInfo.totalStepNum = pdbLigands.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pdbLigands.Length;

            Dictionary<string, string[]> pfamIdPfamAccHash = new Dictionary<string,string[]> ();

            foreach (string ligandId in pdbLigands)
            {
                ProtCidSettings.progressInfo.currentFileName = ligandId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    GetLigandPairsSumInfo(ligandId, ligandTable, ligandPfamPairTable, pfamIdPfamAccHash);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ligandId + " get pfam-ligand interaction sum info error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(ligandId + " get pfam-ligand interaction sum info error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Pfam-Ligand pair sum info done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandId"></param>
        /// <param name="ligandInfoTable"></param>
        /// <param name="ligandPfamPairTable"></param>
        private void GetLigandPairsSumInfo(string ligandId, DataTable ligandInfoTable, DataTable ligandPfamPairTable, Dictionary<string, string[]> pfamIdPfamAccHash)
        {
            string ligandName = GetLigandName(ligandId, ligandInfoTable);
            Dictionary<string, List<string>> ligandPfamPairIEntryHash = GetLigandPfamPairIEntry(ligandId, ligandInfoTable);
            Dictionary<string, List<string>> ligandPfamPairEntryHash = GetLigandPfamPairsEntry(ligandId, ligandInfoTable);
            string[] pfamAccDescript = null;
            foreach (string pfamId in ligandPfamPairIEntryHash.Keys)
            {
                pfamAccDescript = GetPfamAccForPfamId(pfamId, pfamIdPfamAccHash);
                DataRow sumInfoRow = ligandPfamPairTable.NewRow();
                sumInfoRow["Ligand"] = ligandId;
                sumInfoRow["Name"] = ligandName;
                sumInfoRow["PfamID"] = pfamId;
                sumInfoRow["PfamAcc"] = pfamAccDescript[0];
                sumInfoRow["Description"] = pfamAccDescript[1];
                if (ligandPfamPairEntryHash.ContainsKey(pfamId) && ligandPfamPairIEntryHash.ContainsKey(pfamId))
                {
                    sumInfoRow["NumEntry"] = ligandPfamPairEntryHash[pfamId].Count;
                    sumInfoRow["NumIEntry"] = ligandPfamPairIEntryHash[pfamId].Count;
                    ligandPfamPairTable.Rows.Add(sumInfoRow);
                }
            }

            dbInsert.BatchInsertDataIntoDBtables (ProtCidSettings.protcidDbConnection, ligandPfamPairTable);
            ligandPfamPairTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <param name="pfamIdPfamAccHash"></param>
        /// <returns></returns>
        private string[] GetPfamAccForPfamId(string pfamId, Dictionary<string, string[]> pfamIdPfamAccHash)
        {
            string[] pfamAccDescript = null;
            if (pfamIdPfamAccHash.ContainsKey(pfamId))
            {
                pfamAccDescript = pfamIdPfamAccHash[pfamId];
            }
            else
            {
                string queryString = string.Format("Select Pfam_Acc, Description From PfamHmm Where Pfam_ID = '{0}';", pfamId);
                DataTable pfamAccTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                pfamAccDescript = new string[2];
                if (pfamAccTable.Rows.Count > 0)
                {
                    pfamAccDescript[0] = pfamAccTable.Rows[0]["Pfam_Acc"].ToString().TrimEnd();
                    pfamAccDescript[1] = pfamAccTable.Rows[0]["Description"].ToString().TrimEnd();
                }
                else  // pfam id is out of date, should check
                {
                    pfamAccDescript[0] = "-";
                    pfamAccDescript[1] = "-";
                 }
                pfamIdPfamAccHash.Add(pfamId, pfamAccDescript);
            }
            return pfamAccDescript;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligand"></param>
        /// <param name="ligandInfoTable"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetLigandPfamPairIEntry(string ligand, DataTable ligandInfoTable)
        {
            DataRow[] ligandRows = ligandInfoTable.Select(string.Format("Ligand = '{0}'", ligand));
            Dictionary<string, List<string>> pfamEntryListHash = new Dictionary<string,List<string>> ();
            string pdbId = "";
            string asymId = "";
            string[] IPfamIds = null;
            int ligandSeqId = 0;
            foreach (DataRow ligandRow in ligandRows)
            {
                pdbId = ligandRow["PdbID"].ToString();
                asymId = ligandRow["AsymChain"].ToString().TrimEnd();
                ligandSeqId = Convert.ToInt32(ligandRow["SeqID"].ToString());

                if (ligand == "DNA" || ligand == "RNA")
                {
                    IPfamIds = GetInteractingDnaRnaPfamIds(pdbId, asymId);
                }
                else
                {
                    IPfamIds = GetInteractingPfamIds(pdbId, asymId, ligandSeqId);
                }

                foreach (string iPfamId in IPfamIds)
                {
                    if (pfamEntryListHash.ContainsKey(iPfamId))
                    {
                        if (!pfamEntryListHash[iPfamId].Contains(pdbId))
                        {
                            pfamEntryListHash[iPfamId].Add(pdbId);
                        }
                    }
                    else
                    {
                        List<string> entryList = new List<string> ();
                        entryList.Add(pdbId);
                        pfamEntryListHash.Add(iPfamId, entryList);
                    }
                }

            }
            return pfamEntryListHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligandRows"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>> GetLigandPfamPairsEntry(string ligand, DataTable ligandInfoTable)
        {
            DataRow[] ligandRows = ligandInfoTable.Select(string.Format("Ligand = '{0}'", ligand));
            List<string> entryList = new List<string> ();
            string pdbId = "";
            foreach (DataRow ligandRow in ligandRows)
            {
                pdbId = ligandRow["PdbID"].ToString();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                }
            }
            Dictionary<string, List<string>> ligandPfamEntryListHash = new Dictionary<string, List<string>>();
            foreach (string lsPdbId in entryList)
            {
                string[] entryPfamIds = GetPfamInEntry(lsPdbId);
                foreach (string pfamId in entryPfamIds)
                {
                    if (ligandPfamEntryListHash.ContainsKey(pfamId))
                    {
                        if (!ligandPfamEntryListHash[pfamId].Contains(lsPdbId))
                        {
                            ligandPfamEntryListHash[pfamId].Add(lsPdbId);
                        }
                    }
                    else
                    {
                        List<string> pfamEntryList = new List<string> ();
                        pfamEntryList.Add(lsPdbId);
                        ligandPfamEntryListHash.Add(pfamId, pfamEntryList);
                    }
                }
            }
            return ligandPfamEntryListHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetPfamInEntry(string pdbId)
        {
            string queryString = string.Format("Select Distinct Pfam_ID From PdbPfam Where PdbID = '{0}';", pdbId);
            DataTable entryPfamIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            List<string> pfamList = new List<string> ();
            foreach (DataRow pfamRow in entryPfamIdTable.Rows)
            {
                pfamList.Add(pfamRow["Pfam_ID"].ToString ().TrimEnd ());
            }
            return pfamList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateLigandInteractingPfamsSumInfo(string[] updateEntries)
        {           
            DataTable ligandTable = GetLigandTable(null);
            DataTable updateLigandTable = GetLigandTable (updateEntries);
            string[] pdbLigands = GetPdbLigands(updateLigandTable);

            UpdateLigandIPfamsSumInfo(pdbLigands);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateLigandIPfamsSumInfo(string[] pdbLigands)
        {
            DataTable ligandPfamPairTable = CreatLIgandPfamPairSumInfoTable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Pfam-Ligand Sum info";

            DataTable ligandTable = GetLigandTable(null);

            ProtCidSettings.progressInfo.totalStepNum = pdbLigands.Length;
            ProtCidSettings.progressInfo.totalOperationNum = pdbLigands.Length;

            ProtCidSettings.logWriter.WriteLine ("Pfam-Ligand Sum Info");

            Dictionary<string, string[]> pfamIdPfamAccHash = new Dictionary<string,string[]> ();

            foreach (string ligandId in pdbLigands)
            {
                ProtCidSettings.progressInfo.currentFileName = ligandId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    DeleteLigandPfamPairRows(ligandId);
                    GetLigandPairsSumInfo(ligandId, ligandTable, ligandPfamPairTable, pfamIdPfamAccHash);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ligandId + " get pfam-ligand interaction sum info error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(ligandId + " get pfam-ligand interaction sum info error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine ("Done!");
            ProtCidSettings.logWriter.Flush ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ligand"></param>
        private void DeleteLigandPfamPairRows(string ligand)
        {
            string deleteString = string.Format("Delete From PfamLigandsPairSumInfo Where Ligand = '{0}';", ligand);
            dbUpdate.Delete(ProtCidSettings.protcidDbConnection, deleteString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable CreatLIgandPfamPairSumInfoTable()
        {
            string[] sumTableColumns = {"Ligand", "Name", "PfamID", "PfamAcc", "Description", "NumEntry", "NumIEntry"};
            DataTable ligandPfamPairTable = new DataTable("PfamLigandsPairSumInfo");
            foreach (string col in sumTableColumns)
            {
                ligandPfamPairTable.Columns.Add(new DataColumn(col));
            }
            return ligandPfamPairTable;
        }

        /// <summary>
        /// 
        /// </summary>
        private void CreateLigandPfamPairTableInDb()
        {
            DbCreator dbCreat = new DbCreator();
            string createTableString = "CREATE TABLE PFAMLIGANDSPAIRSUMINFO (" +
                            "LIGAND       CHAR(7) NOT NULL," +
                            "NAME         BLOB SUB_TYPE 1 SEGMENT SIZE 80 NOT NULL," +
                            "PFAMID       VARCHAR(40)," +
                            "PFAMACC      VARCHAR(10)," +
                            "DESCRIPTION  VARCHAR(100)," +
                            "NUMENTRY     INTEGER NOT NULL," +
                            "NUMIENTRY    INTEGER NOT NULL);";
            dbCreat.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, "PFAMLIGANDSPAIRSUMINFO");

            string createIndexString = "CREATE INDEX PFAMLIGANDSPAIRSUMINFO_IDX1 ON PFAMLIGANDSPAIRSUMINFO (LIGAND);";
            dbCreat.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, "PFAMLIGANDSPAIRSUMINFO");
            createIndexString = "CREATE INDEX PFAMLIGANDSPAIRSUMINFO_IDX2 ON PFAMLIGANDSPAIRSUMINFO (PFAMID);";
            dbCreat.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, "PFAMLIGANDSPAIRSUMINFO");
        }
        #endregion
    }
}
