using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using System.Xml.Serialization;
using DbLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Settings;
using AuxFuncLib;

namespace BuCompLib.PfamInteract
{
    public class ChainDnaRna : ChainLigands
    {
        #region member variables
        private PdbBuGenerator buGenerator = new PdbBuGenerator ();
        private DataTable chainDnaRnaTable = null;
        private ChainContact atomContact = new ChainContact();
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void RetrieveProtDnaRnaInteractions()
        {
            atomContact.ContactCutoff = contactCutoff;

            chainDnaRnaTable = InitializeTable(true);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving Chain-DNA/RNA interactions.");
            ProtCidSettings.progressInfo.currentOperationLabel = "Pfam-DNA/RNA";

            string queryString = "Select Distinct PdbID From AsymUnit Where PolymerType = 'polydeoxyribonucleotide' OR PolymerType = 'polyribonucleotide';";
            DataTable dnaRnaTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            ProtCidSettings.progressInfo.totalStepNum = dnaRnaTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = dnaRnaTable.Rows.Count;

            string pdbId = "";
            foreach (DataRow entryRow in dnaRnaTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                if (IsEntryChainDnaRnasExist(pdbId))
                {
                    continue;
                }

                try
                {
                    RetrieveChainDnaRnaInteractions(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " Retrieving Chain-DNA/RNA error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " Retrieving Chain-DNA/RNA error: " + ex.Message);
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryChainDnaRnasExist(string pdbId)
        {
            string queryString = string.Format("Select * From ChainDnaRnas Where PdbID = '{0}';", pdbId);
            DataTable chainDnaRnaTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            if (chainDnaRnaTable.Rows.Count > 0)
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
        private bool IsEntryAsuChainDnaRnaExist(string pdbId)
        {
            string queryString = string.Format("Select * From ChainDnaRnas Where PdbID = '{0}' AND BuID = '0';", pdbId);
            DataTable chainDnaRnaTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            if (chainDnaRnaTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        #region update
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateProtDnaRnaInteractions(string[] updateEntries)
        {
            atomContact.ContactCutoff = contactCutoff;

            chainDnaRnaTable = InitializeTable(true);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Pfam-DNA/RNA";

            BuCompBuilder.logWriter.WriteLine("Update Chain-DNA/RNA interfaces.");
            BuCompBuilder.logWriter.WriteLine("#entries = " + updateEntries.Length);
            BuCompBuilder.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Chain-DNA/RNA interactions");

            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    DeleteObsData(pdbId);
                    RetrieveChainDnaRnaInteractions(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " Retrieving Chain-DNA/RNA error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " Retrieving Chain-DNA/RNA error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            BuCompBuilder.logWriter.WriteLine("Done!");
            BuCompBuilder.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteObsData(string pdbId)
        {
            string deleteString = string.Format("Delete From ChainDnaRnas Where PdbID = '{0}';", pdbId);
            dbUpdate.Delete(ProtCidSettings.buCompConnection, deleteString);
        }
        #endregion

        #region chain - DNA/RNA interactions
        /// <summary>
        /// check the interactions between protein chain and DNA/RNA interactions
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pfamLigandTable"></param>
        private void RetrieveChainDnaRnaInteractionsInAsu(string pdbId, string[] protChainNames, string[] dnaRnaChainNames)
        {
            string gzCoordXmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            if (!File.Exists(gzCoordXmlFile))
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ".xml.gz file not exist");
                return;
            }
            string coordXmlFile = ParseHelper.UnZipFile(gzCoordXmlFile, ProtCidSettings.tempDir);
            // read data from crystal xml file
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(coordXmlFile, FileMode.Open);
            EntryCrystal thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();
            File.Delete(coordXmlFile);

            // no coordinates for waters
            ChainAtoms[] chains = thisEntryCrystal.atomCat.ChainAtomList;

            foreach (string dnaRnaChain in dnaRnaChainNames)
            {
                AtomInfo[] dnaRnaAtoms = GetChainAtoms(chains, dnaRnaChain);
                foreach (string protChainName in protChainNames)
                {
                    AtomInfo[] protChainAtoms = GetChainAtoms(chains, protChainName);

                    ChainContactInfo contactInfo = atomContact.GetAllChainContactInfo (dnaRnaAtoms, protChainAtoms);
                    if (contactInfo == null)
                    {
                        continue;
                    }
                    foreach (string seqPair in contactInfo.atomContactHash.Keys)
                    {
                        string[] seqIds = seqPair.Split('_');
                        AtomPair atomPair = (AtomPair)contactInfo.atomContactHash[seqPair];
                        double distance = atomPair.distance;
                        DataRow chainDnaRnaRow = chainDnaRnaTable.NewRow();
                        chainDnaRnaRow["PdbID"] = pdbId;
                        chainDnaRnaRow["BuID"] = "0";
                        chainDnaRnaRow["AsymID"] = dnaRnaChain;
                        chainDnaRnaRow["SymmetryString"] = "1_555";
                        chainDnaRnaRow["ChainAsymID"] = protChainName;
                        chainDnaRnaRow["ChainSymmetryString"] = "1_555"; 
                        chainDnaRnaRow["SeqID"] = seqIds[0];
                        chainDnaRnaRow["ChainSeqID"] = seqIds[1];
                        chainDnaRnaRow["Distance"] = distance;
                        chainDnaRnaRow["Atom"] = atomPair.firstAtom.atomName;
                        chainDnaRnaRow["ChainAtom"] = atomPair.secondAtom.atomName;
                        chainDnaRnaRow["Residue"] = atomPair.firstAtom.residue;
                        chainDnaRnaRow["ChainResidue"] = atomPair.secondAtom.residue;
                        chainDnaRnaTable.Rows.Add(chainDnaRnaRow);
                    }
                }
            }
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.buCompConnection, chainDnaRnaTable);
            chainDnaRnaTable.Clear();
        }

      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDnaRnaTable"></param>
        public void RetrieveChainDnaRnaInteractions(string pdbId)
        {
            string[][] protDnaRnaChains = GetProtDnaRnaChains(pdbId);
            string[] protChains = protDnaRnaChains[0];
            string[] dnaRnaChains = protDnaRnaChains[1];

            if (protChains.Length == 0 || dnaRnaChains.Length == 0)
            {
                return;
            }
            string[] protDnaRnaBuIds = GetBiolAssembliesWithBothChains(pdbId, protChains, dnaRnaChains);

            Dictionary<string, Dictionary<string, AtomInfo[]>> protDnaRnaBuHash = buGenerator.BuildPdbBus(pdbId, protDnaRnaBuIds, true);
            // protein chain - DNA/RNA interactios from biological assemblies in the PDB
            foreach (string buId in protDnaRnaBuIds)
            {
                Dictionary<string, AtomInfo[]> buHash = protDnaRnaBuHash[buId];
                RetrieveProtDnaRnaInteractions(pdbId, buId, buHash, protChains, dnaRnaChains);
            }
            // retrieve protein-DNA/RNA interactions
            RetrieveChainDnaRnaInteractionsInAsu(pdbId, protChains, dnaRnaChains);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDnaRnaTable"></param>
        public void RetrieveChainDnaRnaInteractionsInAsu(string pdbId)
        {
            string[][] protDnaRnaChains = GetProtDnaRnaChains(pdbId);
            string[] protChains = protDnaRnaChains[0];
            string[] dnaRnaChains = protDnaRnaChains[1];

            if (protChains.Length == 0 || dnaRnaChains.Length == 0)
            {
                return;
            }
           
            // retrieve protein-DNA/RNA interactions
            RetrieveChainDnaRnaInteractionsInAsu(pdbId, protChains, dnaRnaChains);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="buHash"></param>
        /// <param name="protChains"></param>
        /// <param name="dnaRnaChains"></param>
        private void RetrieveProtDnaRnaInteractions(string pdbId, string buId, Dictionary<string, AtomInfo[]> buHash, string[] protChains, string[] dnaRnaChains)
        {
            string[] dnaRnaChainsWithSymOp = GetChainsWithSymOps(dnaRnaChains, buHash);
            string[] protChainsWithSymOp = GetChainsWithSymOps(protChains, buHash);


            foreach (string dnaRnaChainSymOp in dnaRnaChainsWithSymOp)
            {
                AtomInfo[] dnaRnaAtoms = buHash[dnaRnaChainSymOp];
                string[] dnaRnaChainSymOpFields = GetChainSymOpFields(dnaRnaChainSymOp);
                foreach (string protChainSymOp in protChainsWithSymOp)
                {
                    string[] protChainSymOpFields = GetChainSymOpFields(protChainSymOp);

                    AtomInfo[] protAtoms = (AtomInfo[])buHash[protChainSymOp];

                    ChainContactInfo contactInfo = atomContact.GetAllChainContactInfo (dnaRnaAtoms, protAtoms);
                    if (contactInfo == null)
                    {
                        continue;
                    }
                    foreach (string seqPair in contactInfo.atomContactHash.Keys)
                    {
                        string[] seqIds = seqPair.Split('_');
                        AtomPair atomPair = (AtomPair)contactInfo.atomContactHash[seqPair];
                        double distance = atomPair.distance;
                        DataRow chainDnaRnaRow = chainDnaRnaTable.NewRow();
                        chainDnaRnaRow["PdbID"] = pdbId;
                        chainDnaRnaRow["BuID"] = buId;
                        chainDnaRnaRow["AsymID"] = dnaRnaChainSymOpFields[0];
                        chainDnaRnaRow["SymmetryString"] = dnaRnaChainSymOpFields[1];
                        chainDnaRnaRow["ChainAsymID"] = protChainSymOpFields[0];
                        chainDnaRnaRow["ChainSymmetryString"] = protChainSymOpFields[1];
                        chainDnaRnaRow["SeqID"] = seqIds[0];
                        chainDnaRnaRow["ChainSeqID"] = seqIds[1];
                        chainDnaRnaRow["Distance"] = distance;
                        chainDnaRnaRow["Atom"] = atomPair.firstAtom.atomName;
                        chainDnaRnaRow["ChainAtom"] = atomPair.secondAtom.atomName;
                        chainDnaRnaRow["Residue"] = atomPair.firstAtom.residue;
                        chainDnaRnaRow["ChainResidue"] = atomPair.secondAtom.residue;
                        chainDnaRnaTable.Rows.Add(chainDnaRnaRow);
                    }
                }
            }
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.buCompConnection, chainDnaRnaTable);
            chainDnaRnaTable.Clear();
        }

       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dnaRnaChains"></param>
        /// <param name="buChainAtomsHash"></param>
        /// <returns></returns>
        private string[] GetChainsWithSymOps(string[] dnaRnaChains, Dictionary<string, AtomInfo[]> buChainAtomsHash)
        {
            List<string> symOpChainList = new List<string> ();

            foreach (string chainSymOp in buChainAtomsHash.Keys)
            {
                string[] fields = chainSymOp.Split('_');
                if (dnaRnaChains.Contains(fields[0]))
                {
                    symOpChainList.Add(chainSymOp);
                }
            }
            return symOpChainList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainSymOp"></param>
        /// <returns></returns>
        private string[] GetChainSymOpFields(string chainSymOp)
        {
            string[] fields = chainSymOp.Split('_');
            string[] chainSymOpFields = new string[2];
            chainSymOpFields[0] = fields[0];
            if (fields.Length == 3)
            {
                chainSymOpFields[1] = fields[1] + "_" + fields[2];
            }
            else if (fields.Length == 2)
            {
                chainSymOpFields[1] = fields[1];
            }
            else
            {
                chainSymOpFields[1] = "-";
            }
            return chainSymOpFields;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="protChains"></param>
        /// <param name="dnaRnaChains"></param>
        /// <returns></returns>
        private string[] GetBiolAssembliesWithBothChains(string pdbId, string[] protChains, string[] dnaRnaChains)
        {
            string queryString = string.Format("Select * From BiolUnit Where PdbID = '{0}';", pdbId);
            DataTable biolUnitTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            List<string> buIdList = new List<string> ();
            string buId = "";
            foreach (DataRow buRow in biolUnitTable.Rows)
            {
                buId = buRow["BiolUnitID"].ToString().TrimEnd();
                if (!buIdList.Contains(buId))
                {
                    buIdList.Add(buId);
                }
            }
            List<string> protDnaRnaBuIdList = new List<string> ();
            foreach (string lsBuId in buIdList)
            {
                DataRow[] buRows = biolUnitTable.Select(string.Format ("BiolUnitID = '{0}'", lsBuId));
                if (DoesBiolAssemblyWithBothChains(buRows, protChains, dnaRnaChains))
                {
                    protDnaRnaBuIdList.Add(lsBuId);
                }
            }
            string[] protDnaRnaBuIds = new string[protDnaRnaBuIdList.Count];
            protDnaRnaBuIdList.CopyTo(protDnaRnaBuIds);
            return protDnaRnaBuIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buRows"></param>
        /// <param name="protChains"></param>
        /// <param name="dnaRnaChains"></param>
        /// <returns></returns>
        private bool DoesBiolAssemblyWithBothChains(DataRow[] buRows, string[] protChains, string[] dnaRnaChains)
        {
            string asymChain = "";
            bool protChainExist = false;
            bool dnaRnaChainExist = false;
            foreach (DataRow buRow in buRows)
            {
                asymChain = buRow["AsymID"].ToString().TrimEnd();
                if (Array.IndexOf(protChains, asymChain) > -1)
                {
                    protChainExist = true;
                }
                if (Array.IndexOf(dnaRnaChains, asymChain) > -1)
                {
                    dnaRnaChainExist = true;
                }
            }
            if (protChainExist && dnaRnaChainExist)
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
        private string[][] GetProtDnaRnaChains (string pdbId)
        {
            string queryString = string.Format("Select AsymID, PolymerStatus, PolymerType From AsymUnit Where PdbID = '{0}';", pdbId);
            DataTable chainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            List<string> protChainList = new List<string> ();
            List<string> dnaRnaChainList = new List<string> ();

            string polymerType = "";
            string asymChain = "";
            foreach (DataRow chainRow in chainTable.Rows)
            {
                polymerType = chainRow["PolymerType"].ToString().TrimEnd();
                asymChain = chainRow["AsymID"].ToString().TrimEnd();
                if (polymerType == "polypeptide")
                {
                    protChainList.Add(asymChain);
                }
                else if (polymerType == "polydeoxyribonucleotide" ||  polymerType == "polyribonucleotide")
                {
                    dnaRnaChainList.Add(asymChain);
                }
            }
            string[][] protDnaRnaChains = new string[2][];
            protChainList.Sort();
            protDnaRnaChains[0] = new string[protChainList.Count];
            protChainList.CopyTo(protDnaRnaChains[0]);

            dnaRnaChainList.Sort();
            protDnaRnaChains[1] = new string[dnaRnaChainList.Count];
            dnaRnaChainList.CopyTo(protDnaRnaChains[1]);

            return protDnaRnaChains;
        }
        #endregion

        #region table
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        /// <returns></returns>
        private DataTable InitializeTable(bool isUpdate)
        {
            string[] pfamDnaRnaColumns = {"PdbID", "BuID", "AsymID", "ChainAsymID", "SeqID", "ChainSeqID", "Distance", "SymmetryString",
                                    "ChainSymmetryString", "Atom", "ChainAtom", "Residue", "ChainResidue"};
            DataTable pfamDnaRnaTable = new DataTable("ChainDnaRnas");
            foreach (string dnaRnaCol in pfamDnaRnaColumns)
            {
                pfamDnaRnaTable.Columns.Add(new DataColumn(dnaRnaCol));
            }
            if (!isUpdate)
            {
                DbCreator dbCreate = new DbCreator();
                string createTableString = "Create Table " + pfamDnaRnaTable.TableName + " (" +
                    " PdbID CHAR(4) NOT NULL, " +
                    " BUID VARCHAR(8) NOT NULL, " +
                    " AsymID CHAR(3) NOT NULL, " +
                    " SymmetryString VARCHAR(15), " +
                    " ChainAsymID CHAR(3), " +
                    " ChainSymmetryString VARCHAR(15), " +
                    " SeqID INTEGER NOT NULL, " +
                    " ChainSeqID INTEGER NOT NULL, " +
                    " ATOM CHAR(4) NOT NULL, " +
                    " ChainATom CHAR(4), " +
                    " Residue CHAR(3), " +
                    " ChainResidue CHAR(3), " +
                    " Distance FLOAT" +
                " );";
                dbCreate.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, pfamDnaRnaTable.TableName);
                string createIndexString = "CREATE INDEX " + pfamDnaRnaTable.TableName + "_idx1 ON " + pfamDnaRnaTable.TableName + "(PdbID, AsymID)";
                dbCreate.CreateIndex(ProtCidSettings.buCompConnection, createIndexString, pfamDnaRnaTable.TableName);

                createIndexString = "CREATE INDEX " + pfamDnaRnaTable.TableName + "_idx2 ON " + pfamDnaRnaTable.TableName + "(PdbID, BuID)";
                dbCreate.CreateIndex(ProtCidSettings.buCompConnection, createIndexString, pfamDnaRnaTable.TableName);
            }
            return pfamDnaRnaTable;
        }
        #endregion


        #region delete DNA/RNA interactions from chain-ligands table
        public void MoveAsuDnaRnaInteractions()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            string queryString = "Select Distinct PdbID From AsymUnit Where PolymerType = 'polydeoxyribonucleotide' OR PolymerType = 'polyribonucleotide';";
            DataTable dnaRnaTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            ProtCidSettings.progressInfo.currentOperationLabel = "Move DNA/RNA interactions";
            ProtCidSettings.progressInfo.totalOperationNum = dnaRnaTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = dnaRnaTable.Rows.Count;

            queryString = "Select First 1 * From ChainDnaRnas;";
            DataTable chainDnaRnaTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            chainDnaRnaTable.TableName = "ChainDnaRnas";
            chainDnaRnaTable.Clear();

            string pdbId = "";
            foreach (DataRow dnaRnaRow in dnaRnaTable.Rows)
            {
                pdbId = dnaRnaRow["PdbID"].ToString();

                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    MoveDnaRnaInteractions(pdbId, chainDnaRnaTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " Move DNA/RNA Interactions errors: " + ex.Message);
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void MoveDnaRnaInteractions(string pdbId, DataTable chainDnaRnaTable)
        {
            string[][] protDnaRnaChains = GetProtDnaRnaChains(pdbId);
            string[] protChains = protDnaRnaChains[0];
            string[] dnaRnaChains = protDnaRnaChains[1];

            if (protChains.Length == 0 || dnaRnaChains.Length == 0)
            {
                return;
            }

            string queryString = string.Format("Select * From ChainLigands Where PdbID = '{0}' AND  AsymID IN ({1});", pdbId, ParseHelper.FormatSqlListString (dnaRnaChains));
            DataTable dnaRnaInteractTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            if (dnaRnaInteractTable.Rows.Count > 0)
            {
                foreach (DataRow interactRow in dnaRnaInteractTable.Rows)
                {
                    DataRow dnaRnaRow = chainDnaRnaTable.NewRow();
                    foreach (DataColumn dCol in dnaRnaInteractTable.Columns)
                    {
                        dnaRnaRow[dCol.ColumnName] = interactRow[dCol.ColumnName];
                    }
                    dnaRnaRow["SymmetryString"] = "1_555";
                    dnaRnaRow["ChainSymmetryString"] = "1_555";
                    dnaRnaRow["BuID"] = "0";  // asymmetry unit

                    chainDnaRnaTable.Rows.Add(dnaRnaRow);
                }
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.buCompConnection, chainDnaRnaTable);
                chainDnaRnaTable.Clear();
                DeleteDnaRnaInteractInChainLigands(pdbId, dnaRnaChains);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="dnaRnaChains"></param>
        private void DeleteDnaRnaInteractInChainLigands(string pdbId, string[] dnaRnaChains)
        {
            string deleteString = string.Format("Delete From ChainLigands Where PdbID = '{0}' AND AsymID IN ({1});", pdbId, ParseHelper.FormatSqlListString (dnaRnaChains));
            dbUpdate.Delete(ProtCidSettings.buCompConnection, deleteString);
        }

        public void InsertDataFromLogFile()
        {
            StreamReader dataReader = new StreamReader("dbInsertChainDnaRnas.txt");
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("INSERT INTO") > -1)
                {
              //      dbInsert.InsertDataIntoDb(ProtCidSettings.buCompConnection, line);
                    dbUpdate.Update(ProtCidSettings.buCompConnection, line);
                }
               
            }
            dataReader.Close();
        }
        #endregion
    }
}
