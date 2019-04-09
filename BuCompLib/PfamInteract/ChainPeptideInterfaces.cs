using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using ProtCidSettingsLib;
using BuCompLib.BuInterfaces;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.KDops;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.ProtInterfaces;

namespace BuCompLib.PfamInteract
{
    public class ChainPeptideInterfaces : EntryBuInterfaces
    {
        #region member variables
        private string[] tableNames = {"ChainPeptideInterfaces", "ChainPeptideAtomPairs", "ChainPeptideResiduePairs"};
        private DataTable[] chainPeptideTables = null;
        private string pfamPeptideDir = "";
        private int peptideLengthCutoff = 50;
        public enum TableIndex
        {
            CPInterface, CPAtomPairs, CPResiduePairs
        }
        #endregion

        /// <summary>
        /// chain-peptide interfaces from biological assemblies defined by the PDB
        /// </summary>
        public void RetrieveChainPeptideInterfaces()
        {
            string[] asuEntries = GetEntries();

            pfamPeptideDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide");
       //     InitializeDbTables();
            InitializeTables();

       /*     string queryString = "Select Distinct PdbID From BiolUnit;";
            DataTable entryTable = dbQuery.Query(queryString);
            string pdbId = "";
            ProtCidSettings.progressInfo.totalOperationNum = entryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = entryTable.Rows.Count; */
            ProtCidSettings.progressInfo.totalOperationNum = asuEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = asuEntries.Length;
            DataRow[] protChainRows = null;
            Dictionary<string, int> chainLengthHash = null;
            foreach (string pdbId in asuEntries)
            {
          //    pdbId = entryRow["PdbID"].ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                DataRow[] peptideInfoRows = GetEntryPeptideChains(pdbId, out protChainRows, out chainLengthHash);
                if (peptideInfoRows.Length == 0)
                {
                    continue;
                }
                string[] peptideAsymChains = GetAsymChains(peptideInfoRows);
                string[] protAsymChains = GetAsymChains (protChainRows);

            //    string[] peptideBuIds = GetEntryBiolAssemblyWithPeptides(pdbId, peptideAsymChains);
           /*   if (peptideBuIds.Length == 0)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " biol assemblies don't contain peptide, while asymmetric unit does.");
                    continue;
                }*/
                try
                {
                    /*    Hashtable asuBuPeptideInterfaceHash =
                            GetAsuBuChainPeptideInterfaces(pdbId, peptideBuIds, peptideAsymChains, chainLengthHash);
                     InsertDataIntoTables(pdbId, asuBuPeptideInterfaceHash, peptideInfoRows, protChainRows);*/

                    InterfaceChains[] chainPepInterfaces = GetAsuChainPeptideInterfaces(pdbId, peptideAsymChains, chainLengthHash);
                    InsertDataIntoTables(pdbId, chainPepInterfaces, peptideInfoRows, protChainRows);
                    InsertDataIntoDb ();
                    ClearDataTables();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId+  " errors: " + ex.Message);
                    BuCompBuilder.logWriter.WriteLine(pdbId + " Retrieve chain-peptide interfaces errors: " + ex.Message);
                    BuCompBuilder.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// update chain peptide interactions
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateChainPeptideInterfaces(string[] updateEntries)
        {
            pfamPeptideDir = Path.Combine(ProtCidSettings.dirSettings.pfamPath, "PfamPeptide");
            InitializeTables();

            BuCompBuilder.logWriter.WriteLine("Update chain-peptide interfaces.");
            BuCompBuilder.logWriter.WriteLine("#entries=" + updateEntries.Length);
            BuCompBuilder.logWriter.Flush();
            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;
            DataRow[] protChainRows = null;
            Dictionary<string, int> chainLengthHash = null; 
            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;

                DeleteObsoleteData(pdbId);

                DataRow[] peptideInfoRows = GetEntryPeptideChains(pdbId, out protChainRows, out chainLengthHash);
                if (peptideInfoRows.Length == 0)
                {
                    continue;
                }
                string[] peptideAsymChains = GetAsymChains(peptideInfoRows);
                string[] protAsymChains = GetAsymChains(protChainRows);
                string[] peptideBuIds = GetEntryBiolAssemblyWithPeptides(pdbId, peptideAsymChains);
                if (peptideBuIds.Length == 0)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " biol assemblies don't contain peptide, while asymmetric unit does.");
                    BuCompBuilder.logWriter.WriteLine(pdbId + " biol assemblies don't contain peptide, while asymmetric unit does.");
                    BuCompBuilder.logWriter.Flush();
     //               continue;
                }               
                try
                {
                    InterfaceChains[] chainPepInterfaces = GetAsuChainPeptideInterfaces(pdbId, peptideAsymChains, chainLengthHash);
                    InsertDataIntoTables(pdbId, chainPepInterfaces, peptideInfoRows, protChainRows);
                    InsertDataIntoDb();
                    ClearDataTables();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " errors: " + ex.Message);
                    BuCompBuilder.logWriter.WriteLine(pdbId + " Retrieve chain-peptide interfaces errors: " + ex.Message);
                    BuCompBuilder.logWriter.Flush();
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            BuCompBuilder.logWriter.WriteLine("Done!");
            BuCompBuilder.logWriter.Flush();
        }

        #region insert data into tables and db
        /// <summary>
        /// 
        /// </summary>
        /// <param name="buPeptideInterfaceHash"></param>
        /// <param name="peptideInfoRows"></param>
        private void InsertDataIntoTables(string pdbId, Dictionary<string, InterfaceChains[]> buPeptideInterfaceHash, DataRow[] peptideInfoRows, DataRow[] protChainRows)
        {
            DataTable crystInterfaceTable = GetEntryCrystInterfaces(pdbId);
            int interfaceId = GetMaximumInterfaceId (crystInterfaceTable) + 1; // the next interface id
            List<string> buIdList = new List<string> (buPeptideInterfaceHash.Keys);
            buIdList.Sort();
            foreach (string buId in buIdList)
            {
                InsertChainPeptideInterfaceToTables(pdbId, buId, ref interfaceId, buPeptideInterfaceHash[buId], peptideInfoRows, protChainRows, crystInterfaceTable);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crystInterfaceTable"></param>
        /// <returns></returns>
        private int GetMaximumInterfaceId(DataTable crystInterfaceTable)
        {
            int maxInterfaceId = 0;
            int interfaceId = 0;
            foreach (DataRow interfaceRow in crystInterfaceTable.Rows)
            {
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString ());
                if (maxInterfaceId < interfaceId)
                {
                    maxInterfaceId = interfaceId;
                }
            }
            return maxInterfaceId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buPeptideInterfaceHash"></param>
        /// <param name="peptideInfoRows"></param>
        private void InsertDataIntoTables(string pdbId, InterfaceChains[] chainPeptideInterfaces, DataRow[] peptideInfoRows, DataRow[] protChainRows)
        {
            DataTable crystInterfaceTable = GetEntryCrystInterfaces(pdbId);
            string buId = "0"; // for asymmtric unit
            int interfaceId = GetMaximumInterfaceId(crystInterfaceTable) + 1;
            InsertChainPeptideInterfaceToTables(pdbId, buId, ref interfaceId, chainPeptideInterfaces, peptideInfoRows, protChainRows, crystInterfaceTable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="chainPeptideInterfaces"></param>
        /// <param name="interfaceId"></param>
        /// <param name="peptideInfoRows"></param>
        /// <param name="protChainRows"></param>
        /// <param name="peptideAsymChains"></param>
        private void InsertChainPeptideInterfaceToTables (string pdbId, string buId, ref int interfaceId, InterfaceChains[] chainPeptideInterfaces,
            DataRow[] peptideInfoRows, DataRow[] protChainRows, DataTable crystInterfaceTable)
        {
            int interfaceIdInDb = 0;
            string asymChain1 = "";
            string asymChain2 = "";
            int pepChainInterfaceId = 0;
            foreach (InterfaceChains chainPeptideInterface in chainPeptideInterfaces)
            {
                asymChain1 = GetAsymChain(chainPeptideInterface.firstSymOpString);
                asymChain2 = GetAsymChain(chainPeptideInterface.secondSymOpString);

                interfaceIdInDb = GetInterfaceIdInDb(asymChain1, asymChain2, crystInterfaceTable);
                if (interfaceIdInDb > -1)
                {
                    pepChainInterfaceId = interfaceIdInDb;
                }
                else
                {
                    pepChainInterfaceId = interfaceId;
                    interfaceId++;
                }
                InsertInterfaceDataToTable(pdbId, pepChainInterfaceId, buId, chainPeptideInterface,
                    peptideInfoRows, protChainRows);
                InsertContactInfoToTables(pdbId, pepChainInterfaceId, chainPeptideInterface);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainSymString"></param>
        /// <returns></returns>
        private string GetAsymChain(string chainSymString)
        {
            string[] symFields = chainSymString.Split('_');
            return symFields[0];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain1"></param>
        /// <param name="asymChain2"></param>
        /// <param name="crystInterfaceTable"></param>
        /// <returns></returns>
        private int GetInterfaceIdInDb(string asymChain1, string asymChain2, DataTable crystInterfaceTable)
        {
            DataRow[] interfaceRows = crystInterfaceTable.Select(string.Format ("AsymChain1 = '{0}' AND AsymChain2 = '{1}' " + 
                " AND SymmetryString1 = '1_555' AND SymmetryString2 = '1_555'", asymChain1, asymChain2));
            int interfaceId = -1;
            if (interfaceRows.Length > 0)
            {
                interfaceId = Convert.ToInt32(interfaceRows[0]["InterfaceID"].ToString ());
            }
            return interfaceId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryCrystInterfaces(string pdbId)
        {
            string queryString = string.Format("Select PdbID, InterfaceID, AsymChain1, AsymChain2, SymmetryString1, SymmetryString2 " +
                " From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable crystInterfaceTable = dbQuery.Query(ProtCidSettings.protcidDbConnection, queryString);
            return crystInterfaceTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="peptideInterface"></param>
        private void InsertInterfaceDataToTable(string pdbId, int interfaceId, string buId, 
            InterfaceChains peptideInterface, DataRow[] peptideInfoRows, DataRow[] protChainRows)
        {
            string[] chainSymStrings1 = GetChainSymmetryStringFields(peptideInterface.firstSymOpString);
            string[] chainSymStrings2 = GetChainSymmetryStringFields(peptideInterface.secondSymOpString);

       /*     if (Array.IndexOf(peptideAsymChains, chainSymStrings1[0]) > -1)
            {
                peptideInterface.Reverse();
                string[] temp = chainSymStrings1;
                chainSymStrings1 = chainSymStrings2;
                chainSymStrings2 = temp;
            }*/
            bool chainFound = true;
            string[] interfaceChainInfo = GetInterfaceChainInfo(chainSymStrings1[0], protChainRows, out chainFound);
            if (!chainFound)
            {
                interfaceChainInfo = GetInterfaceChainInfo(chainSymStrings1[0], peptideInfoRows, out chainFound);
            }
            string[] peptideChainInfo = GetInterfaceChainInfo(chainSymStrings2[0], peptideInfoRows, out chainFound);
            string symmetryString = chainSymStrings1[1];
            string pepSymmetryString = chainSymStrings2[1];

            DataRow interfaceRow = chainPeptideTables[(int)TableIndex.CPInterface].NewRow();
            interfaceRow["PdbID"] = pdbId;
            interfaceRow["InterfaceID"] = interfaceId;
            interfaceRow["BuID"] = buId;
            interfaceRow["AsymChain"] = interfaceChainInfo[0];
            interfaceRow["AuthChain"] = interfaceChainInfo[1];
            interfaceRow["EntityID"] = interfaceChainInfo[2];
            interfaceRow["SymmetryString"] = symmetryString;
            interfaceRow["ChainLength"] = interfaceChainInfo[3];
            interfaceRow["PepAsymChain"] = peptideChainInfo[0];
            interfaceRow["PepAuthChain"] = peptideChainInfo[1];
            interfaceRow["PepEntityID"] = peptideChainInfo[2];
            interfaceRow["PepSymmetryString"] = pepSymmetryString;
            interfaceRow["PepLength"] = peptideChainInfo[3];
            interfaceRow["NumOfAtomPairs"] = peptideInterface.seqContactHash.Count;
            interfaceRow["NumOfResiduePairs"] = peptideInterface.seqDistHash.Count;
            chainPeptideTables[(int)TableIndex.CPInterface].Rows.Add(interfaceRow);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="peptideInterface"></param>
        private void InsertContactInfoToTables(string pdbId, int interfaceId, InterfaceChains peptideInterface)
        {
            foreach (string seqPair in peptideInterface.seqContactHash.Keys)
            {
                AtomPair atomPair = (AtomPair)peptideInterface.atomContactHash[seqPair];
                DataRow contactRow = chainPeptideTables[(int)TableIndex.CPAtomPairs].NewRow  ();
                contactRow["PdbID"] = pdbId;
                contactRow["InterfaceID"] = interfaceId;
                contactRow["Residue"] = atomPair.firstAtom.residue;
                contactRow["SeqID"] = atomPair.firstAtom.seqId;
                contactRow["Atom"] = atomPair.firstAtom.atomName;
                contactRow["AtomSeqID"] = atomPair.firstAtom.atomId;
                contactRow["PepResidue"] = atomPair.secondAtom.residue ;
                contactRow["PepSeqID"] = atomPair.secondAtom.seqId;
                contactRow["PepAtom"] = atomPair.secondAtom.atomName;
                contactRow["PepAtomSeqID"] = atomPair.secondAtom.atomId;
                contactRow["Distance"] = ((AtomPair)peptideInterface.atomContactHash[seqPair]).distance;
                chainPeptideTables[(int)TableIndex.CPAtomPairs].Rows.Add(contactRow);
            }

            foreach (string seqPair in peptideInterface.seqDistHash.Keys)
            {
                string[] seqFields = seqPair.Split('_');
                DataRow contactRow = chainPeptideTables[(int)TableIndex.CPResiduePairs].NewRow();
                contactRow["PdbID"] = pdbId;
                contactRow["InterfaceID"] = interfaceId;
                contactRow["Residue"] = peptideInterface.GetAtom(seqFields[0], 1).residue;
                contactRow["SeqID"] = seqFields[0];
                contactRow["PepResidue"] = peptideInterface.GetAtom(seqFields[1], 2).residue;
                contactRow["PepSeqID"] = seqFields[1];
                contactRow["Distance"] = peptideInterface.seqDistHash[seqPair];
                chainPeptideTables[(int)TableIndex.CPResiduePairs].Rows.Add(contactRow);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain"></param>
        /// <param name="chainInfoRows"></param>
        /// <returns></returns>
        private string[] GetInterfaceChainInfo(string asymChain, DataRow[] chainInfoRows, out bool chainFound)
        {
            string[] chainInfo = new string[4];
            string asymChainId = "";
            chainFound = false;
            string origAsymChain = GetOrigAsymChain(asymChain);
            foreach (DataRow chainInfoRow in chainInfoRows)
            {
                asymChainId = chainInfoRow["AsymID"].ToString().TrimEnd();
                if (asymChainId == origAsymChain)
                {
                 //   chainInfo[0] = chainInfoRow["AsymID"].ToString().TrimEnd();
                    chainInfo[0] = asymChain;
                    chainInfo[1] = chainInfoRow["AuthorChain"].ToString().TrimEnd();
                    chainInfo[2] = chainInfoRow["EntityID"].ToString();
                    chainInfo[3] = chainInfoRow["Sequence"].ToString().Length.ToString ();
                    chainFound = true;
                }
            }
            return chainInfo;
        }

        /// <summary>
        /// after NCS, the asymmetric chain is the original asymmetric chain + a digit number
        /// </summary>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private string GetOrigAsymChain(string asymChain)
        {
            string origAsymChain = "";
            foreach (char ch in asymChain)
            {
                if (char.IsDigit(ch))
                {
                    break;
                }
                origAsymChain += ch.ToString();
            }
            return origAsymChain;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainSymmetryString"></param>
        /// <returns></returns>
        private string[] GetChainSymmetryStringFields(string chainSymmetryString)
        {
            string[] fields = chainSymmetryString.Split('_');
            string[] chainSymStrings = new string[2];
            chainSymStrings[0] = fields[0];
            if (fields.Length == 3)
            {
                chainSymStrings[1] = fields[1] + "_" + fields[2];
            }
            else if (fields.Length == 2)
            {
                chainSymStrings[1] = fields[1];
            }
            else
            {
                chainSymStrings[1] = "-";
            }
            return chainSymStrings;
        }

        /// <summary>
        /// 
        /// </summary>
        private void InsertDataIntoDb()
        {
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.buCompConnection, chainPeptideTables);
        }

        /// <summary>
        /// 
        /// </summary>
        private void ClearDataTables()
        {
            foreach (DataTable chainPeptideTable in chainPeptideTables)
            {
                chainPeptideTable.Clear();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteObsoleteData(string pdbId)
        {
            foreach (string tableName in tableNames)
            {
                DeleteObsoleteData(pdbId, tableName);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="tableName"></param>
        private void DeleteObsoleteData(string pdbId, string tableName)
        {
            string deleteString = string.Format("Delete From {0} Where PdbID = '{1}';", tableName, pdbId);
            dbUpdate.Delete(ProtCidSettings.buCompConnection, deleteString);
        }
        #endregion

        #region chain-peptide interaction
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="peptideBuIds"></param>
        /// <param name="peptideAsymChains"></param>
        /// <returns></returns>
        private Dictionary<string, InterfaceChains[]> GetAsuBuChainPeptideInterfaces(string pdbId, string[] peptideBuIds, string[] peptideAsymChains, Dictionary<string, int> chainLengthHash)
        {
            InterfaceChains[] asuPeptideInterfaces = GetAsuChainPeptideInterfaces(pdbId, peptideAsymChains, chainLengthHash);
            Dictionary<string, InterfaceChains[]> buChainPeptideInterfaceHash = GetBuChainPeptideInterfaces(pdbId, peptideBuIds, peptideAsymChains, chainLengthHash);
                       
            if (asuPeptideInterfaces.Length > 0)
            {
                buChainPeptideInterfaceHash.Add("0", asuPeptideInterfaces);
            }
            return buChainPeptideInterfaceHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="peptideAsymChains"></param>
        /// <returns></returns>
        private InterfaceChains[] GetAsuChainPeptideInterfaces(string pdbId, string[] peptideAsymChains, Dictionary<string, int> chainLengthHash)
        {
            Dictionary<string, Dictionary<int, int>> asuEntityContentHash = GetAsuEntityContent(pdbId);
            Dictionary<string, Dictionary<string, AtomInfo[]>> asymUnitHash = buRetriever.GetAsymUnit(pdbId);
            List<string> asuHashKeyList = new List<string> (asymUnitHash.Keys);
            InterfaceChains[] asuPeptideInterfaces = GetChainPeptideInterfacesInAsu(pdbId, asymUnitHash[asuHashKeyList[0]], peptideAsymChains, chainLengthHash);
            return asuPeptideInterfaces;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="peptideBuIds"></param>
        /// <param name="peptideAsymChains"></param>
        /// <returns></returns>
        private Dictionary<string, InterfaceChains[]> GetBuChainPeptideInterfaces(string pdbId, string[] peptideBuIds, string[] peptideAsymChains, Dictionary<string, int> chainLengthHash)
        {
            Dictionary<string, Dictionary<int, int>> buEntityContentHash = GetPdbEntryBUEntityContent(pdbId, peptideBuIds);
            string[] nonMonomerBUs = GetNonMonomerBUs(buEntityContentHash);
            Dictionary<string, Dictionary<string, AtomInfo[]>> entryBiolUnits = buRetriever.GetEntryBiolUnits(pdbId, nonMonomerBUs);
            Dictionary<string, InterfaceChains[]> buChainPeptideInterfaceHash = new Dictionary<string,InterfaceChains[]> ();
            foreach (string buId in peptideBuIds)
            {
                Dictionary<string, AtomInfo[]> biolUnit = entryBiolUnits[buId];
                if (biolUnit == null || biolUnit.Count == 0)
                {
                    continue;
                }
                InterfaceChains[] chainPeptideInterfaces =
                    GetChainPeptideInterfacesInBiolUnit(pdbId, buId, biolUnit, peptideAsymChains, chainLengthHash);
                if (chainPeptideInterfaces != null)
                {
                    buChainPeptideInterfaceHash.Add(buId, chainPeptideInterfaces);
                }
            }
            return buChainPeptideInterfaceHash;
        }

        /// <summary>
        /// get chain contacts (interfaces) in the biological unit
        /// </summary>
        /// <param name="biolUnit">key: chainid, value: atom list</param>
        /// <returns></returns>
        public InterfaceChains[] GetChainPeptideInterfacesInBiolUnit(string pdbId, string buId, Dictionary<string, AtomInfo[]> biolUnit, 
            string[] peptideAsymChains, Dictionary<string, int> chainLengthHash)
        {
            if (biolUnit == null || biolUnit.Count < 2)
            {
                return null;
            }
            // build trees for the biological unit
            Dictionary<string, BVTree> buChainTreesHash = BuildBVtreesForBiolUnit(biolUnit);

            InterfaceChains[] buPeptideInterfaces = GetChainPeptideInterfaces(buChainTreesHash, peptideAsymChains, chainLengthHash);
            return buPeptideInterfaces;
        }

        /// <summary>
        /// get chain contacts (interfaces) in the biological unit
        /// </summary>
        /// <param name="biolUnit">key: chainid, value: atom list</param>
        /// <returns></returns>
        public InterfaceChains[] GetChainPeptideInterfacesInAsu(string pdbId, Dictionary<string, AtomInfo[]> asymUnit, string[] peptideAsymChains, Dictionary<string, int> chainLengthHash)
        {
            if (asymUnit == null )
            {
                return null;
            }
            // build trees for the biological unit
            Dictionary<string, BVTree> asuChainTreesHash = BuildBVtreesForBiolUnit(asymUnit);
            InterfaceChains[] asuPeptideInterfaces = GetChainPeptideInterfaces(asuChainTreesHash, peptideAsymChains, chainLengthHash);
            return asuPeptideInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainTreesHash"></param>
        /// <param name="peptideAsymChains"></param>
        /// <returns></returns>
        private InterfaceChains[] GetChainPeptideInterfaces(Dictionary<string, BVTree> chainTreesHash, string[] peptideAsymChains, Dictionary<string, int> chainLengthHash)
        {
            // calculate interfaces
            List<InterfaceChains> interChainsList = new List<InterfaceChains> ();
            List<string> keyList = new List<string> (chainTreesHash.Keys);
            keyList.Sort();
            int interChainId = 0;
            bool isChainPeptide1 = false;
            bool isChainPeptide2 = false;
            string chainSymmString1 = "";
            string chainSymmString2 = "";
            string chain1 = "";
            string chain2 = "";
            string origChain1 = "";
            string origChain2 = "";
            for (int i = 0; i < keyList.Count - 1; i++)
            {
                chainSymmString1 = keyList[i].ToString();
                chain1 = GetChainFromSymmetryString(chainSymmString1);
                origChain1 = GetOrigAsymChain(chain1);
                isChainPeptide1 = IsBuChainPeptide(origChain1, peptideAsymChains);
                for (int j = i + 1; j < keyList.Count; j++)
                {
                    chainSymmString1 = keyList[i].ToString();
                    chainSymmString2 = keyList[j].ToString();
                    chain2 = GetChainFromSymmetryString(chainSymmString2);
                    origChain2 = GetOrigAsymChain(chain2);
                    isChainPeptide2 = IsBuChainPeptide(origChain2, peptideAsymChains);
                    if ((isChainPeptide1 && !isChainPeptide2) ||
                       (!isChainPeptide1 && isChainPeptide2) || 
                       (isChainPeptide1 &&  isChainPeptide2))  // interactions between all peptide chains, 
                        // since the length of peptides can be changed when retrieving the interactions
                    {
                        if (isChainPeptide1) // peptide is always the second chain
                        {
                            if (!isChainPeptide2)
                            {
                                string temp = chainSymmString2;
                                chainSymmString2 = chainSymmString1;
                                chainSymmString1 = temp;
                            }
                            else // if all peptide, then the shorter is the second chain
                            {
                                int chainLength1 = (int)chainLengthHash[origChain1];
                                int chainLength2 = (int)chainLengthHash[origChain2];
                                if (chainLength1 < chainLength2)
                                {
                                    string temp = chainSymmString2;
                                    chainSymmString2 = chainSymmString1;
                                    chainSymmString1 = temp;
                                }
                            }
                        }
                        ChainContact chainContact = new ChainContact(chainSymmString1, chainSymmString2);
                        ChainContactInfo contactInfo = chainContact.GetAnyChainContactInfo((BVTree)chainTreesHash[chainSymmString1],
                            (BVTree)chainTreesHash[chainSymmString2]);
                        if (contactInfo != null)
                        {
                            interChainId++;

                            InterfaceChains interfaceChains = new InterfaceChains(chainSymmString1, chainSymmString2);
                            // no need to change the tree node data
                            // only assign the refereces
                            interfaceChains.chain1 = ((BVTree)chainTreesHash[chainSymmString1]).Root.AtomList;
                            interfaceChains.chain2 = ((BVTree)chainTreesHash[chainSymmString2]).Root.AtomList;
                            interfaceChains.interfaceId = interChainId;
                            interfaceChains.seqDistHash = contactInfo.GetBbDistHash();
                            interfaceChains.seqContactHash = contactInfo.GetContactsHash();
                            interfaceChains.atomContactHash = contactInfo.atomContactHash; // atompair
                            interChainsList.Add(interfaceChains);
                            //chainContact = null;
                        }
                    }
                }
            }

            return interChainsList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymmetryString"></param>
        /// <param name="peptideAsymChains"></param>
        /// <returns></returns>
        private bool IsBuChainPeptide(string asymChain, string[] peptideAsymChains)
        {
            if (Array.IndexOf(peptideAsymChains, asymChain) > -1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainSymOpString"></param>
        /// <returns></returns>
        private string GetChainFromSymmetryString(string chainSymOpString)
        {
            string[] fields = chainSymOpString.Split('_');
            return fields[0];
        }
        #endregion

        #region peptide chains
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="peptideAsymChains"></param>
        /// <returns></returns>
        private string[] GetEntryBiolAssemblyWithPeptides(string pdbId, string[] peptideAsymChains)
        {
            string queryString = string.Format("Select * From BiolUnit Where PdbID = '{0}';", pdbId);
            DataTable biolUnitTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            List<string> buList = new List<string> ();
            string asymChain = "";
            string buId = "";
            foreach (DataRow buRow in biolUnitTable.Rows)
            {
                asymChain = buRow["AsymID"].ToString().TrimEnd();
                if (Array.IndexOf(peptideAsymChains, asymChain) > -1)
                {
                    buId = buRow["BiolUnitID"].ToString ().TrimEnd ();
                    if (! buList.Contains(buId))
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
        /// <returns></returns>
        private DataRow[] GetEntryPeptideChains(string pdbId, out DataRow[] protChainRows, out Dictionary<string,int> chainLengthHash)
        {
            string queryString = string.Format("Select AsymID, AuthorChain, EntityID, Sequence From AsymUnit " +
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable entryInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string sequence = "";
            List<DataRow> peptideRowList = new List<DataRow> ();
            List<DataRow> protChainRowList = new List<DataRow> ();
            chainLengthHash = new Dictionary<string,int> ();
            string asymChain = "";
            int seqLength = 0;
            foreach (DataRow infoRow in entryInfoTable.Rows)
            {
                sequence = infoRow["Sequence"].ToString().TrimEnd ();
                seqLength = sequence.Length;

                if (seqLength <= peptideLengthCutoff)
                {
                    peptideRowList.Add(infoRow);
                }
                else
                {
                    protChainRowList.Add (infoRow);
                }
                asymChain = infoRow["AsymID"].ToString ().TrimEnd ();
                chainLengthHash.Add(asymChain, seqLength);
            }
            protChainRows = protChainRowList.ToArray ();

            return peptideRowList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peptideInfoRows"></param>
        /// <returns></returns>
        private string[] GetAsymChains(DataRow[] chainInfoRows)
        {
            string[] asymChains = new string[chainInfoRows.Length];
            int count = 0;
            foreach (DataRow chainnfoRow in chainInfoRows)
            {
                asymChains[count] = chainnfoRow["AsymID"].ToString().TrimEnd();
                count++;
            }
            return asymChains;
        }
        #endregion

        #region initialize tables
        /// <summary>
        /// 
        /// </summary>
        private void InitializeTables()
        {
            chainPeptideTables = new DataTable[tableNames.Length];
            string tableName = tableNames[(int)TableIndex.CPInterface];
            string[] interfaceColumns = { "PdbID", "InterfaceID", "BuID", "AsymChain", "SymmetryString",
                                            "PepAsymChain", "PepSymmetryString",
                                        "EntityID", "PepEntityID", "AuthChain", "PepAuthChain", 
                                        "NumOfAtomPairs", "NumOfResiduePairs", "ChainLength", "PepLength"};
            chainPeptideTables[(int)TableIndex.CPInterface] = new DataTable(tableName);
            foreach (string interfaceCol in interfaceColumns)
            {
                chainPeptideTables[(int)TableIndex.CPInterface].Columns.Add(new DataColumn (interfaceCol));
            }

            tableName = tableNames[(int)TableIndex.CPAtomPairs];
            string[] atomPairColumns = {"PdbID", "InterfaceID", "Residue", "SeqID", "Atom", "AtomSeqID", 
                                           "PepResidue", "PepSeqID", "PepAtom", "PepAtomSeqID", "Distance"};
            chainPeptideTables[(int)TableIndex.CPAtomPairs] = new DataTable(tableName);
            foreach (string atomPairCol in atomPairColumns)
            {
                chainPeptideTables[(int)TableIndex.CPAtomPairs].Columns.Add(new DataColumn(atomPairCol));
            }

            tableName = tableNames[(int)TableIndex.CPResiduePairs];
            string[] residuePairColumns = { "PdbID", "InterfaceID", "Residue", "SeqID", "PepResidue", "PepSeqID", "Distance" };
            chainPeptideTables[(int)TableIndex.CPResiduePairs] = new DataTable(tableName);
            foreach (string residuePairCol in residuePairColumns)
            {
                chainPeptideTables[(int)TableIndex.CPResiduePairs].Columns.Add(new DataColumn(residuePairCol));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableName"></param>
        private void InitializeDbTables ()
        {
            string tableName = tableNames[(int)TableIndex.CPInterface];
            DbCreator dbCreate = new DbCreator();
            string createTableString = "CREATE TABLE " + tableName + " ( " +
                " PDBID CHAR(4) NOT NULL, " +
                " InterfaceID INTEGER NOT NULL, " +
                " BuID varchar(8) NOT NULL, " +
                " AsymChain CHAR(3) NOT NULL, " +
                " EntityID INTEGER NOT NULL, " +
                " AuthChain CHAR(3) NOT NULL, " +
                " SymmetryString VARCHAR(15), " +
                " ChainLength INTEGER NOT NULL, " +
                " PepAsymChain CHAR(3) NOT NULL, " +
                " PepEntityID INTEGER NOT NULL, " +
                " PepAuthChain CHAR(3) NOT NULL, " +
                " PepSymmetryString VARCHAR(15), " +
                " PepLength INTEGER NOT NULL, " + 
                " NumOfAtomPairs INTEGER NOT NULL, " +
                " NumOfResiduePairs INTEGER NOT NULL);";
            dbCreate.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, tableName);

            string indexString = "CREATE INDEX ChainPeptide_Index0 ON " + tableName + " (PdbID, InterfaceID);";
            dbCreate.CreateIndex(ProtCidSettings.buCompConnection, indexString, tableName);

            tableName = tableNames[(int)TableIndex.CPAtomPairs];
            createTableString= "CREATE TABLE " + tableName + " ( " +
                " PdbID CHAR(4) NOT NULL, " +
                " InterfaceID INTEGER NOT NULL, " +
                " Residue CHAR(3) NOT NULL, " +
                " SeqID INTEGER NOT NULL, " +
                " Atom CHAR(4) NOT NULL, " +
                " AtomSeqID INTEGER NOT NULL, " +
                " PepResidue CHAR(3) NOT NULL, " +
                " PepSeqID INTEGER NOT NULL, "  +
                " PepAtom CHAR(4) NOT NULL, " +
                " PepAtomSeqID INTEGER NOT NULL, " +
                " Distance FLOAT NOT NULL );";
            dbCreate.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, tableName);

            indexString = "CREATE INDEX ChainPeptideAtom_Idx0 ON " + tableName + " (PdbID, InterfaceID);";
            dbCreate.CreateIndex(ProtCidSettings.buCompConnection, indexString, tableName);

            tableName = tableNames[(int)TableIndex.CPResiduePairs];
            createTableString = "CREATE TABLE " + tableName + " ( " +
                " PdbID CHAR(4) NOT NULL, " +
                " InterfaceID INTEGER NOT NULL, " +
                " Residue CHAR(3) NOT NULL, " +
                " SeqID INTEGER NOT NULL, " +
                " PepResidue CHAR(3) NOT NULL, " +
                " PepSeqID INTEGER NOT NULL, " +
                " Distance FLOAT NOT NULL );";
            dbCreate.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, tableName);

            indexString = "CREATE INDEX ChainPeptideResidue_Idx0 ON " + tableName + " (PdbID, InterfaceID);";
            dbCreate.CreateIndex(ProtCidSettings.buCompConnection, indexString, tableName);
        }
        #endregion

        #region entries
        private string[] GetEntries()
        {
            List<string> entryList = new List<string> ();
            string entryFile = "AsuEntries.txt";
            if (File.Exists(entryFile))
            {
                StreamReader dataReader = new StreamReader(entryFile);
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    entryList.Add(line);
                }
                dataReader.Close();
            }
            else
            {
           /*     string queryString = "Select Distinct PdbID From ChainPeptideInterfaces;";
                DataTable entryTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
                */
                string queryString = "Select Distinct PdbID From AsymUnit Where PolymerType = 'polypeptide';";
                DataTable entryTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                StreamWriter dataWriter = new StreamWriter(entryFile);
                string pdbId = "";
                foreach (DataRow entryRow in entryTable.Rows)
                {
                    pdbId = entryRow["PdbID"].ToString();
                    entryList.Add(pdbId);
                    dataWriter.WriteLine(pdbId);
                }
                dataWriter.Close();
            }
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }      
        #endregion

        #region debug: update the interface id to interface id in crystentryinterfaces table
        public void AddCrystInterfaceIDToTable()
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Add cryst chain interface to chainpeptideinterfaces");
            string queryString = "Select Distinct PdbID From ChainPeptideInterfaces;";
            DataTable pepEntryTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            string pdbId = "";

            ProtCidSettings.progressInfo.totalStepNum = pepEntryTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = pepEntryTable.Rows.Count;

            foreach (DataRow entryRow in pepEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();

                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                AddCrystInterfaceIDToTable(pdbId);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void AddCrystInterfaceIDToTable(string pdbId)
        {
            string queryString = string.Format("Select PdbID, InterfaceID, AsymChain, PepAsymChain " + 
                " From ChainPeptideInterfaces Where PdbID = '{0}';", pdbId);
            DataTable peptideInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            queryString = string.Format("Select * From CrystEntryInterfaces Where PdbID = '{0}' AND " + 
                " SymmetryString1 = '1_555' AND SymmetryString2 = '1_555';", pdbId);
            DataTable asuCrystInterfaceTable = dbQuery.Query(ProtCidSettings.protcidDbConnection, queryString);

            int pepInterfaceId = 0;
            string asymChain = "";
            string pepAsymChain = "";
            int crystInterfaceId = 0;
            int maxInterfaceId = GetMaximumInterfaceId (pdbId);
            foreach (DataRow pepInterfaceRow in peptideInterfaceTable.Rows)
            {
                pdbId = pepInterfaceRow["PdbID"].ToString();
                pepInterfaceId = Convert.ToInt32(pepInterfaceRow["InterfaceID"].ToString());
                asymChain = pepInterfaceRow["AsymChain"].ToString().TrimEnd();
                pepAsymChain = pepInterfaceRow["PepAsymChain"].ToString().TrimEnd();

                crystInterfaceId = GetChainInterfaceId(asymChain, pepAsymChain, asuCrystInterfaceTable);
                if (crystInterfaceId == -1)
                {
                    crystInterfaceId = maxInterfaceId + 1;
                    maxInterfaceId++;
                }
                AddCrystChainInterfaceIdToTable(pdbId, pepInterfaceId, crystInterfaceId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pepInterfaceId"></param>
        /// <param name="chainInterfaceId"></param>
        private void AddCrystChainInterfaceIdToTable(string pdbId, int pepInterfaceId, int chainInterfaceId)
        {
            string updateString = string.Format("Update ChainPeptideInterfaces Set ChainInterfaceID = {0} Where PdbID = '{1}' AND InterfaceID = '{2}';", 
                chainInterfaceId, pdbId, pepInterfaceId);
            dbUpdate.Update(ProtCidSettings.buCompConnection, updateString);
            updateString = string.Format("Update ChainPeptideAtomPairs Set ChainInterfaceID = {0} Where PdbID = '{1}' AND InterfaceID = '{2}';",
                chainInterfaceId, pdbId, pepInterfaceId);
            dbUpdate.Update(ProtCidSettings.buCompConnection, updateString);
            updateString = string.Format("Update ChainPeptideResiduePairs Set ChainInterfaceID = {0} Where PdbID = '{1}' AND InterfaceID = '{2}';",
                chainInterfaceId, pdbId, pepInterfaceId);
            dbUpdate.Update(ProtCidSettings.buCompConnection, updateString);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int GetMaximumInterfaceId(string pdbId)
        {
            string queryString = string.Format("Select Max(InterfaceID) As MaxInterfaceID From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable maxInterfaceIdTable = dbQuery.Query(ProtCidSettings.protcidDbConnection, queryString);
            int maxInterfaceId = 0;
            string maxInterfaceIdString = maxInterfaceIdTable.Rows[0]["MaxInterfaceID"].ToString();
            if (maxInterfaceIdString != "")
            {
                maxInterfaceId = Convert.ToInt32(maxInterfaceIdString);
            }
            return maxInterfaceId;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="asymChain1"></param>
        /// <param name="asymChain2"></param>
        /// <param name="entryCrystInterfaceTable"></param>
        /// <returns></returns>
        private int GetChainInterfaceId(string pepAsymChain1, string pepAsymChain2, DataTable entryCrystInterfaceTable)
        {
            string asymChain1 = "";
            string asymChain2 = "";
            int interfaceId = -1;
            foreach (DataRow interfaceRow in entryCrystInterfaceTable.Rows)
            {
                asymChain1 = interfaceRow["AsymChain1"].ToString().TrimEnd();
                asymChain2 = interfaceRow["AsymChain2"].ToString().TrimEnd();
                if ((pepAsymChain1 == asymChain1 && pepAsymChain2 == asymChain2) ||
                    (pepAsymChain2 == asymChain1 && pepAsymChain1 == asymChain2))
                {
                    interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString ());
                    break;
                }
            }
            return interfaceId;
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateInterfaceIDsInContactsTable()
        {
            string queryString = "Select Distinct PdbID, InterfaceID, ChainInterfaceID From ChainPeptideInterfaces;";
            DataTable peptideInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            string pdbId = "";
            int interfaceId = 0;
            int chainInterfaceId = 0;
            foreach (DataRow pepInterfaceRow in peptideInterfaceTable.Rows)
            {
                pdbId = pepInterfaceRow["PdbID"].ToString();
                interfaceId = Convert.ToInt32(pepInterfaceRow["InterfaceID"].ToString ());
                chainInterfaceId = Convert.ToInt32(pepInterfaceRow["ChainInterfaceID"].ToString ());
                UpdatePeptideInterfaceContacts(pdbId, interfaceId, chainInterfaceId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pepInterfaceId"></param>
        /// <param name="chainInterfaceId"></param>
        private void UpdatePeptideInterfaceContacts(string pdbId, int pepInterfaceId, int chainInterfaceId)
        {
            string updateString = string.Format("Update ChainPeptideResiduePairs Set InterfaceID = {0} Where PdbID = '{1}' AND InterfaceID = {2};", 
                chainInterfaceId, pdbId, pepInterfaceId);
            dbUpdate.Update(ProtCidSettings.buCompConnection, updateString);

            updateString = string.Format("Update ChainPeptideAtomPairs Set InterfaceID = {0} Where PdbID = '{1}' AND InterfaceID = {2};",
                chainInterfaceId, pdbId, pepInterfaceId);
            dbUpdate.Update(ProtCidSettings.buCompConnection, updateString);

            updateString = string.Format("Update PfamPeptideInterfaces Set InterfaceID = {0} Where PdbID = '{1}' AND InterfaceID = {2};",
                chainInterfaceId, pdbId, pepInterfaceId);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);

            updateString = string.Format("Update PfamPeptideHmmSites Set InterfaceID = {0} Where PdbID = '{1}' AND InterfaceID = {2};",
                chainInterfaceId, pdbId, pepInterfaceId);
            dbUpdate.Update(ProtCidSettings.protcidDbConnection, updateString);
        }
        #endregion
    }
}
