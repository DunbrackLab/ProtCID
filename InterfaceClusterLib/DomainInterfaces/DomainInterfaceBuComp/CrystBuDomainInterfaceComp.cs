using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using CrystalInterfaceLib.DomainInterfaces;
using BuCompLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Settings;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces.DomainInterfaceBuComp
{
    /// <summary>
    /// compare inter-chain domain interfaces in crystal and biological units
    /// </summary>
    public class CrystBuDomainInterfaceComp
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        private BuDomainInterfaceRetriever buDomainInterfaceRetriever = new BuDomainInterfaceRetriever();
        private DomainInterfaceRetriever crystDomainInterfaceRetriever = new DomainInterfaceRetriever();
        private DomainInterfaceComp domainInterfaceComp = new  DomainInterfaceComp ();
        private DataTable[] crystBuDomainInterfaceCompTables = null;
        private StreamWriter logWriter = new StreamWriter("CrystBuCompLog.txt");
        private Dictionary<string, string> pfamIdAccHash = new Dictionary<string,string> ();
        private Dictionary<string, string> pfamAccIdHash = new Dictionary<string,string> ();
        #endregion

        public CrystBuDomainInterfaceComp()
        {
            if (ProtCidSettings.buCompConnection == null)
            {
                ProtCidSettings.buCompConnection = new DbConnect();
                ProtCidSettings.buCompConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                               ProtCidSettings.dirSettings.baInterfaceDbPath;
            }

            ProtCidSettings.buCompConnection.ConnectToDatabase();
            domainInterfaceComp.nonAlignDomainWriter = new StreamWriter("CrystBuCompDomainAlignLog.txt", true);

            BuCompBuilder.logWriter = new StreamWriter("CrystBuCompLog_temp.txt");
        }

        #region public interfaces for comparing cryst and BU domain interfaces
        /// <summary>
        /// 
        /// </summary>
        public void CompareCrystBuDomainInterfaces()
        {
            InitializeTable();
            // if tables not in db, create them
  //          InitializeDbTable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Compare Cryst and Bu Domain Interfaces";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare Crystal and BU domain interfaces.");


          //   string[] entriesNotInDb = GetEntriesNotInDb();
            string[] entriesNotInDb = GetEntries();

            ProtCidSettings.progressInfo.totalStepNum = entriesNotInDb.Length;
            ProtCidSettings.progressInfo.totalOperationNum = entriesNotInDb.Length;

            foreach (string pdbId in entriesNotInDb)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
               
 /*               if (string.Compare (pdbId, "1dn2") <= 0)
                {
                    continue;
                }*/

                if (ProtCidSettings.progressInfo.currentOperationNum < 72887)
                {
                    continue;
                }

    /*            if (ProtCidSettings.progressInfo.currentOperationNum > 72887)
                {
                    break;
                }*/

                try
                {
                    CompareCrystBuDomainInterfaces(pdbId);
                    // add the intra-chain domain interfaces info to tables
                    AddIntraChainDomainInterfaces(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare errors in " + pdbId +
                        ". Error message: " + ex.Message);
                    logWriter.WriteLine("Compare errors in " + pdbId + ". Error message: " + ex.Message);
                    logWriter.Flush();
                }
            }

            ProtCidSettings.buCompConnection.DisconnectFromDatabase();
            logWriter.Close();

            BuCompBuilder.logWriter.Close();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.progressInfo.threadFinished = true;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIds"></param>
        public void UpdateCrystBuDomainInterfaceComp(int[] relSeqIds)
        {
            InitializeTable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Compare Cryst and Bu Domain Interfaces";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update comparing Crystal and BU domain interfaces.");

            string[] updateEntries = GetEntriesOfRelations(relSeqIds);

            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    DeleteObsCrystBuDomainInterfaceCompData(pdbId);
                    CompareCrystBuDomainInterfaces(pdbId);
                    AddIntraChainDomainInterfaces(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare errors in " + pdbId +
                        ". Error message: " + ex.Message);
                    logWriter.WriteLine("Compare errors in " + pdbId + ". Error message: " + ex.Message);
                }
            }

            ProtCidSettings.buCompConnection.DisconnectFromDatabase();
            logWriter.Close();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>;
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateCrystBuDomainInterfaceComp (string[] updateEntries)
        {
            InitializeTable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Compare Cryst and Bu Domain Interfaces";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update comparing Crystal and BU domain interfaces.");

            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    DeleteObsCrystBuDomainInterfaceCompData(pdbId);
                    CompareCrystBuDomainInterfaces(pdbId);
                    AddIntraChainDomainInterfaces(pdbId);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compare errors in " + pdbId +
                        ". Error message: " + ex.Message);
                    logWriter.WriteLine("Compare errors in " + pdbId + ". Error message: " + ex.Message);
                }
            }

            ProtCidSettings.buCompConnection.DisconnectFromDatabase();
            logWriter.Close();

            BuCompBuilder.logWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
       //     ProtCidSettings.progressInfo.threadFinished = true;
        }
  
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public void CompareCrystBuDomainInterfaces(string pdbId)
        {
        /*    if (! ExistBuDomainInterfaces(pdbId))
            {
                return;
            }
            */
            string[] familyCodeStrings = null;
            Dictionary<string, DomainInterface[]> crystDomainInterfacesHash = GetCrystEntryDomainInterfaces(pdbId, out familyCodeStrings);         

            string buType = "";
            if (crystDomainInterfacesHash.Count > 0)
            {
                // for pdb
                buType = "pdb";
                string[] difBUs = buDomainInterfaceRetriever.GetEntryDifBUs(pdbId, buType);
                if (difBUs.Length > 0)
                {
                    Dictionary<string, Dictionary<string, DomainInterface[]>> pdbBuDomainInterfacesHash = GetBuEntryDomainInterfaces(pdbId, difBUs, familyCodeStrings, buType);
                    if (pdbBuDomainInterfacesHash.Count > 0)
                    {
                        CompareEntryCrystBuDomainInterfaces(pdbId, crystDomainInterfacesHash, pdbBuDomainInterfacesHash, buType);
                    }
                }
                // for pisa
                buType = "pisa";
                Dictionary<string, string> pisaSameBUsHash = null;
                string[] pisaNotSameNonMonomerBUs = GetNonMonomerBUsNotSameAsPdb(pdbId, ref pisaSameBUsHash, buType);
                if (pisaNotSameNonMonomerBUs != null && pisaNotSameNonMonomerBUs.Length > 0)
                {
                    Dictionary<string, Dictionary<string, DomainInterface[]>> pisaBuDomainInterfacesHash = GetBuEntryDomainInterfaces(pdbId, pisaNotSameNonMonomerBUs, familyCodeStrings, buType);
                    if (pisaBuDomainInterfacesHash.Count > 0)
                    {
                        CompareEntryCrystBuDomainInterfaces(pdbId, crystDomainInterfacesHash, pisaBuDomainInterfacesHash, buType);
                    }
                }
                int pdbBuTableNum = GetTableNumFromBuType ("pdb");
                if (pisaSameBUsHash != null)
                {
                    AddSamePdbBUsCompToDb(pisaSameBUsHash, buType, crystBuDomainInterfaceCompTables[pdbBuTableNum]);
                }
                dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, crystBuDomainInterfaceCompTables);
                ClearTables();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="samePdbBUsHash"></param>
        /// <param name="buType"></param>
        /// <param name="pdbCrystBuDomainInterfaceCompTable"></param>
        private void AddSamePdbBUsCompToDb(Dictionary<string, string> samePdbBUsHash, string buType, DataTable pdbCrystBuDomainInterfaceCompTable)
        {
            string pdbBuId = "";
            int tableNum = GetTableNumFromBuType(buType);
            foreach (string buId in samePdbBUsHash.Keys)
            {
                pdbBuId = (string)samePdbBUsHash[buId];
                DataRow[] crystPdbBuCompRows = pdbCrystBuDomainInterfaceCompTable.Select(string.Format ("BuID = '{0}'", pdbBuId));
                foreach (DataRow pdbCompRow in crystPdbBuCompRows)
                {
                    DataRow buCompRow = crystBuDomainInterfaceCompTables[tableNum].NewRow();
                    buCompRow.ItemArray = pdbCompRow.ItemArray;
                    buCompRow["BuID"] = buId;
                    crystBuDomainInterfaceCompTables[tableNum].Rows.Add(buCompRow);
                }
            }
        }
        #endregion

        #region compare domain interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="crystDomainInterfaceHash"></param>
        /// <param name="buDomainInterfaceHash"></param>
        private void CompareEntryCrystBuDomainInterfaces(string pdbId, Dictionary<string, DomainInterface[]> crystDomainInterfaceHash, Dictionary<string, Dictionary<string, DomainInterface[]>> entryBuDomainInterfaceHash, string buType)
        {
            foreach (string familyCodeString in crystDomainInterfaceHash.Keys)
            {
                DomainInterface[] crystDomainInterfaces = (DomainInterface[])crystDomainInterfaceHash[familyCodeString];
                if (entryBuDomainInterfaceHash.ContainsKey(familyCodeString))
                {
                    foreach (string buId in entryBuDomainInterfaceHash[familyCodeString].Keys)
                    {
                        DomainInterface[] buDomainInterfaces = (DomainInterface[])entryBuDomainInterfaceHash[familyCodeString][buId];
                        DomainInterfacePairInfo[] compPairInfo =
                            CompareCrystBuDomainInterfaces(crystDomainInterfaces, buDomainInterfaces);
                        InsertDataIntoTable(pdbId, buId, compPairInfo, buType);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crystDomainInterfaces"></param>
        /// <param name="buDomainInterfaces"></param>
        private DomainInterfacePairInfo[] CompareCrystBuDomainInterfaces(DomainInterface[] crystDomainInterfaces, DomainInterface[] buDomainInterfaces)
        {
            DomainInterfacePairInfo[] compPairInfo = domainInterfaceComp.CompareEntryDomainInterfaces (crystDomainInterfaces, buDomainInterfaces);
            return compPairInfo;
        }
        #endregion

        #region get domain interfaces from cryst and BUs
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns>familyCodeString:domainInterfaces</returns>
        private Dictionary<string, DomainInterface[]> GetCrystEntryDomainInterfaces(string pdbId, out string[] familyCodeStrings)
        {
            // only compare inter-chain interfaces
            string queryString = string.Format("Select Distinct RelSeqID From PfamDomainInterfaces Where PdbID = '{0}' AND InterfaceID > 0;", pdbId);
            DataTable relationTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = -1;
            string familyCodeString = "";
            Dictionary<string, DomainInterface[]> entryRelationDomainInterfacesHash = new Dictionary<string,DomainInterface[]> (); // familyCodes: DomainInterfaces (key:value)
            List<string> familyCodeList = new List<string> ();
            DomainInterface[] crystDomainInterfaces = null;
            foreach (DataRow relationRow in relationTable.Rows)
            {
                relSeqId = Convert.ToInt32(relationRow["RelSeqID"].ToString ());
                familyCodeString = GetFamilyCodeString(relSeqId, "PfamDomainFamilyRelation", ProtCidSettings.protcidDbConnection);
                try
                {
                    crystDomainInterfaces = crystDomainInterfaceRetriever.GetInterChainDomainInterfaces(pdbId, relSeqId, "cryst");
                    entryRelationDomainInterfacesHash.Add(familyCodeString, crystDomainInterfaces);

                    familyCodeList.Add (familyCodeString);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " " + relSeqId.ToString () + " " + familyCodeString + " " +
                        ex.Message);
                    ProtCidSettings.logWriter.WriteLine(pdbId + " " + relSeqId.ToString() + " " + familyCodeString + " " +
                        ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            familyCodeStrings = new string[familyCodeList.Count];
            familyCodeList.CopyTo(familyCodeStrings);

            return entryRelationDomainInterfacesHash;
        }

        /// <summary>
        /// All domain interfaces from each BUs including inter-chain, intra-chain
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns>familyCodeString:BuID:DomainInterfaces</returns>
        private Dictionary<string, Dictionary<string, DomainInterface[]>> GetBuEntryDomainInterfaces(string pdbId, string[] familyCodeStrings, string buType)
        {
            BuCompBuilder.BuType = buType;
            int[] relSeqIds = GetRelationSeqIDs(familyCodeStrings, "PfamRelations", ProtCidSettings.buCompConnection);
            Dictionary<string, Dictionary<string, DomainInterface[]>> entryBuDomainInterfaceHash = new Dictionary<string, Dictionary<string, DomainInterface[]>>(); // FamilyCodes : BuID : DomainInterfaces, 3 levels
            for (int i = 0; i < relSeqIds.Length; i ++ )
            {
                if (relSeqIds[i] == -1)
                {
                    continue;
                }
                Dictionary<string, DomainInterface[]> buDomainInterfacesHash = buDomainInterfaceRetriever.RetrieveDomainInterfaces(relSeqIds[i], pdbId, buType);
                if (buDomainInterfacesHash.Count > 0)
                {
                    entryBuDomainInterfaceHash.Add(familyCodeStrings[i], buDomainInterfacesHash);
                }
            }
            return entryBuDomainInterfaceHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns>familyCodeString:BuID:DomainInterfaces</returns>
        private Dictionary<string, Dictionary<string, DomainInterface[]>> GetBuEntryDomainInterfaces(string pdbId, string[] buIDs, string[] familyCodeStrings, string buType)
        {
            BuCompBuilder.BuType = buType;
            int[] relSeqIds = GetBuRelationSeqIDs(familyCodeStrings);
            Dictionary<string, Dictionary<string, DomainInterface[]>> entryBuDomainInterfaceHash = new Dictionary<string, Dictionary<string, DomainInterface[]>>(); // FamilyCodes : BuID : DomainInterfaces, 3 levels
            for (int i = 0; i < relSeqIds.Length; i++)
            {
                if (relSeqIds[i] == -1)
                {
                    continue;
                }
                Dictionary<string, DomainInterface[]> buDomainInterfacesHash = buDomainInterfaceRetriever.RetrieveDomainInterfaces(relSeqIds[i], pdbId, buIDs);
                if (buDomainInterfacesHash.Count > 0)
                {
                    UpdateBuDomainInterfaceFamilyCodes(buDomainInterfacesHash);
                    entryBuDomainInterfaceHash.Add(familyCodeStrings[i], buDomainInterfacesHash);
                }
            }
            return entryBuDomainInterfaceHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buDomainInterfacesHash"></param>
        private void UpdateBuDomainInterfaceFamilyCodes(Dictionary<string, DomainInterface[]> buDomainInterfacesHash)
        {
            string pfamId1 = "";
            string pfamId2 = "";
            foreach (string buId in buDomainInterfacesHash.Keys)
            {
                DomainInterface[] buDomainInterfaces = buDomainInterfacesHash[buId];
                for (int i = 0; i < buDomainInterfaces.Length; i++)
                {
                    pfamId1 = GetPfamId(buDomainInterfaces[i].familyCode1);
                    pfamId2 = GetPfamId(buDomainInterfaces[i].familyCode2);
                    buDomainInterfaces[i].familyCode1 = pfamId1;
                    buDomainInterfaces[i].familyCode2 = pfamId2;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAcc"></param>
        /// <returns></returns>
        private string GetPfamId(string pfamAcc)
        {
            string pfamId = "";
            if (pfamAccIdHash.ContainsKey(pfamAcc))
            {
                pfamId = (string)pfamAccIdHash[pfamAcc];
            }
            else
            {
                string querystring = string.Format("Select Pfam_ID From PfamHmm Where Pfam_Acc = '{0}';", pfamAcc);
                DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query( querystring);
                pfamId = pfamIdTable.Rows[0]["Pfam_ID"].ToString().TrimEnd();
                pfamAccIdHash.Add(pfamAcc, pfamId);
                if (!pfamIdAccHash.ContainsKey(pfamId))
                {
                    pfamIdAccHash.Add(pfamId, pfamAcc);
                }
            }
            return pfamId;
        }
        #endregion

        #region relSeqID, family_family code
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIds"></param>
        /// <returns></returns>
        private string[] GetFamilyCodesForRelations(int[] relSeqIds, string tableName, DbConnect dbConnect)
        {
            string[] familyCodeStrings = new string[relSeqIds.Length];
            for (int i = 0; i < relSeqIds.Length; i++)
            {
                string familyCodeString = GetFamilyCodeString(relSeqIds[i], tableName, dbConnect);
                familyCodeStrings[i] = familyCodeString;
            }
            return familyCodeStrings;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="familyCodeStrings"></param>
        /// <param name="tableName"></param>
        /// <param name="dbConnect"></param>
        /// <returns></returns>
        private int[] GetRelationSeqIDs(string[] familyCodeStrings, string tableName, DbConnect dbConnect)
        {
            int[] relSeqIds = new int[familyCodeStrings.Length];
            for (int i = 0; i < familyCodeStrings.Length; i++)
            {
                relSeqIds[i] = GetRelationSeqId(familyCodeStrings[i], tableName, dbConnect);
            }
            return relSeqIds;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="tableName"></param>
        /// <param name="dbConnect"></param>
        /// <returns></returns>
        private string GetFamilyCodeString (int relSeqId, string tableName, DbConnect dbConnect)
        {
            string queryString = string.Format("Select * From {0} Where RelSeqID = {1};", tableName, relSeqId);
            DataTable familyCodeTable = dbQuery.Query(dbConnect, queryString);
            string familyCodeString = familyCodeTable.Rows[0]["FamilyCode1"].ToString().TrimEnd() + ";" +
                familyCodeTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            return familyCodeString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="familyCodeString"></param>
        /// <param name="tableName"></param>
        /// <param name="dbConnect"></param>
        /// <returns></returns>
        private int GetRelationSeqId(string familyCodeString, string tableName, DbConnect dbConnect)
        {
            string[] familyCodes = familyCodeString.Split(';');
            string queryString = string.Format("Select RelSeqID From {0} " + 
                " Where FamilyCode1 = '{1}' AND FamilyCode2 = '{2}';", 
                tableName, familyCodes[0], familyCodes[1]);
            DataTable relSeqIdTable = dbQuery.Query(dbConnect, queryString);
            if (relSeqIdTable.Rows.Count > 0)
            {
                int relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString());
                return relSeqId;
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="familyCodeStrings"></param>
        /// <param name="tableName"></param>
        /// <param name="dbConnect"></param>
        /// <returns></returns>
        private int[] GetBuRelationSeqIDs(string[] familyCodeStrings)
        {
            int[] relSeqIds = new int[familyCodeStrings.Length];
            for (int i = 0; i < familyCodeStrings.Length; i++)
            {
                relSeqIds[i] = GetBuRelationSeqId(familyCodeStrings[i]);
            }
            return relSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="familyCodeString"></param>
        /// <param name="tableName"></param>
        /// <param name="dbConnect"></param>
        /// <returns></returns>
        private int GetBuRelationSeqId(string familyCodeString)
        {
            string[] familyCodes = familyCodeString.Split(';');
            string pfamAcc1 = GetPfamAcc (familyCodes[0]);
            string pfamAcc2 = "";
            if (familyCodes[0] == familyCodes[1])
            {
                pfamAcc2 = pfamAcc1;
            }
            else
            {
                pfamAcc2 = GetPfamAcc(familyCodes[1]);
            }
            
            string queryString = string.Format("Select RelSeqID From PfamRelations " +
                " Where (FamilyCode1 = '{0}' AND FamilyCode2 = '{1}') OR " +
                " (FamilyCode1 = '{1}' AND FamilyCode2 = '{0}');", pfamAcc1, pfamAcc2);
            DataTable relSeqIdTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            if (relSeqIdTable.Rows.Count > 0)
            {
                int relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString());
                return relSeqId;
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string GetPfamAcc(string pfamId)
        {
            string pfamAcc = "";
            if (pfamIdAccHash.ContainsKey(pfamId))
            {
                pfamAcc = (string)pfamIdAccHash[pfamId];
            }
            else
            {
                string querystring = string.Format("Select Pfam_Acc From PfamHmm Where Pfam_Id = '{0}';", pfamId);
                DataTable pfamAccTable = ProtCidSettings.pdbfamQuery.Query( querystring);
                pfamAcc = pfamAccTable.Rows[0]["Pfam_Acc"].ToString().TrimEnd();
                pfamIdAccHash.Add(pfamId, pfamAcc);
                if (! pfamAccIdHash.ContainsKey(pfamAcc))
                {
                    pfamAccIdHash.Add(pfamAcc, pfamId);
                }
            }
            return pfamAcc;
        }
        #endregion

        #region add intra-chain domain interfaces
        /// <summary>
        /// check if the intra-chain domain interfaces are in the PDB/PISA Biological assemblies
        /// </summary>
        /// <param name="pdbId"></param>
        private void AddIntraChainDomainInterfaces(string pdbId)
        {
            string queryString = string.Format("Select PdbID, DomainInterfaceID, AsymChain1 As AsymChain From PfamDomainInterfaces Where PdbID = '{0}' AND InterfaceID = 0;", pdbId);
            DataTable intraChainDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (intraChainDomainInterfaceTable.Rows.Count == 0)
            {
                return;
            }

            DataTable pdbBATable = GetPdbBiolAssemlbies (pdbId);

            string asymChain = "";
            int domainInterfaceId = 0;
            int tableNum = 0;
            foreach (DataRow intraChainInterfaceRow in intraChainDomainInterfaceTable.Rows)
            {
                asymChain = intraChainInterfaceRow["AsymChain"].ToString().TrimEnd();
                string[] pdbBaIds = GetPdbBiolAssembliesWithChain(pdbBATable, asymChain);
                string[] pisaBaIds = GetPisaBiologicalAssemblies(pdbId, asymChain);
                
                domainInterfaceId = Convert.ToInt32(intraChainInterfaceRow["DomainInterfaceID"].ToString ());

                if (pdbBaIds.Length > 0)
                {
                    tableNum = GetTableNumFromBuType("pdb");
                    foreach (string pdbBaId in pdbBaIds)
                    {
                        DataRow dataRow = crystBuDomainInterfaceCompTables[tableNum].NewRow();
                        dataRow["PdbID"] = pdbId;
                        dataRow["DomainInterfaceID"] = domainInterfaceId;
                        dataRow["BuID"] = pdbBaId;
                        dataRow["BuDomainInterfaceID"] = 0;
                        dataRow["Qscore"] = 1.0;
                        crystBuDomainInterfaceCompTables[tableNum].Rows.Add(dataRow);
                        dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, crystBuDomainInterfaceCompTables[tableNum]);
                        crystBuDomainInterfaceCompTables[tableNum].Clear();
                    }
                }
                if (pisaBaIds.Length > 0)
                {
                    tableNum = GetTableNumFromBuType("pisa");
                    foreach (string pisaBaId in pisaBaIds)
                    {
                        DataRow dataRow = crystBuDomainInterfaceCompTables[tableNum].NewRow();
                        dataRow["PdbID"] = pdbId;
                        dataRow["DomainInterfaceID"] = domainInterfaceId;
                        dataRow["BuID"] = pisaBaId;
                        dataRow["BuDomainInterfaceID"] = 0;
                        dataRow["Qscore"] = 1.0;
                        crystBuDomainInterfaceCompTables[tableNum].Rows.Add(dataRow);
                        dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, crystBuDomainInterfaceCompTables[tableNum]);
                        crystBuDomainInterfaceCompTables[tableNum].Clear();
                    }
                }
            }
        }

        #region pdb biological assemblies with the chain
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChian"></param>
        /// <returns></returns>
        private string[] GetPdbBiolAssembliesWithChain(DataTable pdbBATable, string asymChain)
        {
            List<string> baIdList = new List<string> ();
            if (pdbBATable != null)
            {
                DataRow[] baRows = pdbBATable.Select(string.Format("AsymID = '{0}'", asymChain));
                string baId = "";
                foreach (DataRow baRow in baRows)
                {
                    baId = baRow["BiolUnitID"].ToString().TrimEnd();
                    if (!baIdList.Contains(baId))
                    {
                        baIdList.Add(baId);
                    }
                }
            }
            return baIdList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetPdbBiolAssemlbies(string pdbId)
        {
            string[] selectBAIds = GetAuthorDefinedBiolAssemblies(pdbId);
            if (selectBAIds.Length > 0)
            {
                string queryString = string.Format("Select * From BiolUnit Where PdbID = '{0}' AND BiolUnitID IN ({1});", pdbId, ParseHelper.FormatSqlListString(selectBAIds));
                DataTable biolUnitTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                return biolUnitTable;
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetAuthorDefinedBiolAssemblies(string pdbId)
        {
            string queryString = string.Format("Select * From PdbBuStat Where PdbID = '{0}';", pdbId);
            DataTable buStatTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string details = "";
            string buId = "";
            List<string> authorDefinedBAIdList = new List<string> ();
            foreach (DataRow buStatRow in buStatTable.Rows)
            {
                details = buStatRow["Details"].ToString().TrimEnd();
                buId = buStatRow["BiolUnitID"].ToString();
                if (details.IndexOf("author_") > -1)
                {
                    authorDefinedBAIdList.Add(buId);
                } 
            }
            if (authorDefinedBAIdList.Count == 0) // only software_defined_assembly
            {
                foreach (DataRow buStatRow in buStatTable.Rows)
                {
                    buId = buStatRow["BiolUnitID"].ToString();
                    authorDefinedBAIdList.Add(buId);
                }
            }

            return authorDefinedBAIdList.ToArray ();
        }
        #endregion

        #region pisa biological assemblies with the chain
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private string[] GetPisaBiologicalAssemblies(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select Distinct AssemblySeqID From PisaBuMatrix Where PdbID = '{0}' AND AsymChain = '{1}';", pdbId, asymChain);
            DataTable pisaBaIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] pisaBaIds = new string[pisaBaIdTable.Rows.Count];
            int count = 0;
            string baId = "";
            foreach (DataRow pisaBaIdRow in pisaBaIdTable.Rows)
            {
                baId = pisaBaIdRow["AssemblySeqID"].ToString();
                pisaBaIds[count] = baId;
                count++;
            }
            return pisaBaIds;
        }
        #endregion
        #endregion

        #region initialize tables
        private void InitializeTable()
        {
            crystBuDomainInterfaceCompTables = new DataTable[ProtCidSettings.buTypes.Length];
            string[] compColumns = { "PdbID", "DomainInterfaceID", "BuID", "BuDomainInterfaceID", "Qscore" };
            for (int i = 0; i < ProtCidSettings.buTypes.Length; i++)
            {
                crystBuDomainInterfaceCompTables[i] = new DataTable("Cryst" + ProtCidSettings.buTypes[i] + "BuDomainInterfaceComp");
               foreach (string compCol in compColumns)
                {
                    crystBuDomainInterfaceCompTables[i].Columns.Add(new DataColumn(compCol));
                }
            }
        }

        private void InitializeDbTable()
        {
            DbCreator dbCreate = new DbCreator();
            string createTableString = "";

            foreach (string buType in ProtCidSettings.buTypes)
            {
     //           if (!dbCreate.IsTableExist(ProtCidSettings.protcidDbConnection, "Cryst" + buType + "BuDomainInterfaceComp"))
     //           {
                    createTableString = "CREATE TABLE Cryst" + buType + "BuDomainInterfaceComp ( " +
                        "PdbID CHAR(4) NOT NULL, " +
                        "DomainInterfaceID INTEGER NOT NULL, " +
                        "BuID VARCHAR(8) NOT NULL, " +
                        "BuDomainInterfaceID INTEGER NOT NULL, " +
                        "QScore FLOAT NOT NULL);";
                    dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, "Cryst" + buType + "BuDomainInterfaceComp");

                    string indexString = string.Format("CREATE INDEX {0}crystbucomp_idx1 " +
                        "ON Cryst{0}BuDomainInterfaceComp (PdbID, DomainInterfaceID);", buType);
                    dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, "Cryst" + buType + "BuDomainInterfaceComp");
        //        }
            }
        }
        #endregion

        #region insert data into table
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="compPairInfo"></param>
        private void InsertDataIntoTable(string pdbId, string buId, DomainInterfacePairInfo[] compPairInfo, string buType)
        {
            int tableNum = GetTableNumFromBuType(buType);
            foreach (DomainInterfacePairInfo compPair in compPairInfo)
            {
                if (compPair.qScore >= AppSettings.parameters.contactParams.minQScore)
                {
                    DataRow dataRow = crystBuDomainInterfaceCompTables[tableNum].NewRow();
                    dataRow["PdbID"] = pdbId;
                    dataRow["DomainInterfaceID"] = compPair.interfaceInfo1.domainInterfaceId;
                    dataRow["BuID"] = buId;
                    dataRow["BuDomainInterfaceID"] = compPair.interfaceInfo2.domainInterfaceId;
                    dataRow["Qscore"] = compPair.qScore;
                    crystBuDomainInterfaceCompTables[tableNum].Rows.Add(dataRow);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buType"></param>
        /// <returns></returns>
        private int GetTableNumFromBuType(string buType)
        {
            int tableNum = -1;
            switch (buType)
            {
                case "pdb":
                    tableNum = (int)ProtCidSettings.BuType.PDB;
                    break;

        /*        case "pqs":
                    tableNum = (int)AppSettings.BuType.PQS;
                    break;*/

                case "pisa":
                    tableNum = (int)ProtCidSettings.BuType.PISA;
                    break;

                default:
                    break;
            }
            return tableNum;
        }
    
        private void ClearTables()
        {
            foreach (DataTable crystBuCompTable in crystBuDomainInterfaceCompTables)
            {
                crystBuCompTable.Clear();
            }
        }
        #endregion

        #region data exist in DB
        private string[] GetEntries()
        {
            string queryString = string.Format("Select Distinct PdbID From {0}DomainInterfaces;", ProtCidSettings.dataType);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
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
        /// <returns></returns>
        private string[] GetEntriesNotInDb()
        {
            string queryString = string.Format("Select Distinct PdbID From {0}DomainInterfaces;",
       //     string queryString = string.Format("Select Distinct PdbID From {0}DomainInterfaces Where RelSeqID IN (11619, 11642);",
             ProtCidSettings.dataType);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);

            List<string> entryList = new List<string> ();
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (IsEntryCompared(pdbId))
                {
                    continue;
                }
                entryList.Add(pdbId);
            }
            string[] entriesNotInDb = new string[entryList.Count];
            entryList.CopyTo(entriesNotInDb);
            return entriesNotInDb;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIds"></param>
        /// <returns></returns>
        private string[] GetEntriesOfRelations(int[] relSeqIds)
        {
            List<string> entryList = new List<string> ();
            foreach (int relSeqId in relSeqIds)
            {
                string[] entriesInRelation = GetEntriesInRelation(relSeqId);
                foreach (string entry in entriesInRelation)
                {
                    if (!entryList.Contains(entry))
                    {
                        entryList.Add(entry);
                    }
                }
            }

            return entryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetEntriesInRelation(int relSeqId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamDomainInterfaces " + 
                " Where RelSeqID = {0};", relSeqId);
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
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
        /// The Domain interfaces from BUs and crystal structures are already compared,
        /// and save in the database
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryCompared(string pdbId)
        {
            string queryString = "";
            foreach (DataTable crystBuDomainInterfaceCompTable in crystBuDomainInterfaceCompTables)
            {
                queryString = string.Format("Select * From {0} Where PdbID = '{1}';", 
                    crystBuDomainInterfaceCompTable.TableName, pdbId);
                DataTable crystBuCompTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (crystBuCompTable.Rows.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// check if there are any domain interfaces in three BUs
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool ExistBuDomainInterfaces(string pdbId)
        {
            foreach (string buType in ProtCidSettings.buTypes)
            {
                bool existThisTypeBuDomainInterfaces = ExistBuDomainInterfaces(pdbId, buType);
                if (existThisTypeBuDomainInterfaces)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Check if there are domain-domain interfaces from BUs of the entry
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool ExistBuDomainInterfaces(string pdbId, string buType)
        {
            string queryString = string.Format("Select * From {0}PfamBuDomainInterfaces " + 
                " Where PdbID = '{1}';", buType, pdbId);
            DataTable buDomainInterfaceTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            if (buDomainInterfaceTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        private void DeleteObsCrystBuDomainInterfaceCompData(string pdbId)
        {
            string deleteString = "";
            foreach (string buType in ProtCidSettings.buTypes)
            {
                deleteString = string.Format("Delete From Cryst{0}BuDomainInterfaceComp " + 
                    " Where PdbId = '{1}';", buType, pdbId);
                ProtCidSettings.protcidQuery.Query( deleteString);
            }
        }
        #endregion

        #region PQS/PISA BUs not same as PDB BUs
        private BuCompLib.BuInterfaces.EntryBuInterfaces entryBuInterfaces = new BuCompLib.BuInterfaces.EntryBuInterfaces();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="buType">"pqs" or "pisa"</param>
        /// <returns>key: pdbid, value: a list BUs not same as BUs from PDB</returns>
        private Dictionary<string, string[]> GetEntriesNotSameAsPdbBUs(string buType)
        {
            string queryString = "Select Distinct PdbID From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable protEntryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            List<string> notSameEntryList = new List<string> ();
            string pdbId = "";
            Dictionary<string, string[]> notSameEntryBuHash = new Dictionary<string,string[]> ();
            Dictionary<string, string> sameBuHash = null;
            foreach (DataRow entryRow in protEntryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString ();
                string[] notSameBUs = GetNonMonomerBUsNotSameAsPdb(pdbId, ref sameBuHash, buType);
                if (notSameBUs != null && notSameBUs.Length > 0)
                {
                    notSameEntryBuHash.Add(pdbId, notSameBUs);
                }
            }
            return notSameEntryBuHash;
        }

        /// <summary>
        /// the BUs of the entry are not same as BUs from PDB BUs
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private string[] GetNonMonomerBUsNotSameAsPdb(string pdbId, ref Dictionary<string, string> sameBUsHash, string type)
        {
            string[] pdbNonMonomerBUs = GetEntryNonMonomerBUs(pdbId, "pdb");
            string[] nonMonomerBUs = null;
            string compType = "";

            switch (type)
            {
                case "pqs":
                    nonMonomerBUs = GetEntryNonMonomerBUs(pdbId, "pqs");
                    compType = "pdbpqs";
                    break;

                case "pisa":
                    nonMonomerBUs = GetEntryNonMonomerBUs(pdbId, "pisa");
                    compType = "pdbpisa";
                    break;

                default:
                    break;
            }
         
            if (pdbNonMonomerBUs.Length == 0 && nonMonomerBUs.Length > 0)
            {
                return nonMonomerBUs;
            }
            else if (pdbNonMonomerBUs.Length > 0 && nonMonomerBUs.Length > 0)
            {
                string[] notSameBUs = GetBUsNotSameAsPdb(pdbId, compType, pdbNonMonomerBUs, nonMonomerBUs, ref sameBUsHash);
                return notSameBUs;
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="compType"></param>
        /// <param name="pdbNonMonomerBUs"></param>
        /// <param name="nonMonomerBUs"></param>
        /// <returns></returns>
        private string[] GetBUsNotSameAsPdb(string pdbId, string compType, 
            string[] pdbNonMonomerBUs, string[] nonMonomerBUs, ref Dictionary<string, string> sameBUsHash)
        {
            string queryString = string.Format("Select * From {0}BuComp Where PdbID = '{1}' Order By BuID1;", 
                compType, pdbId);
            DataTable buCompTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            List<string> notSameBuList = new List<string> (nonMonomerBUs);
            int interfaceNum1 = -1;
            int interfaceNum2 = -1;
            string buId1 = "";
            string buId2 = "";
            string isSame = "";
            sameBUsHash = new Dictionary<string, string> ();
            foreach (DataRow compRow in buCompTable.Rows)
            {
                buId1 = compRow["BuID1"].ToString().TrimEnd();
                buId2 = compRow["BuID2"].ToString().TrimEnd();
                interfaceNum1 = Convert.ToInt32(compRow["InterfaceNum1"].ToString ());
                interfaceNum2 = Convert.ToInt32(compRow["InterfaceNum2"].ToString ());
                isSame = compRow["IsSame"].ToString();
                if (interfaceNum1 == interfaceNum2 && isSame == "1")
                {
                    notSameBuList.Remove (buId2);
                    if (! sameBUsHash.ContainsKey(buId2))
                    {
                        sameBUsHash.Add(buId2, buId1);
                    }
                }
            }
            string[] notSameBUs = new string[notSameBuList.Count];
            notSameBuList.CopyTo(notSameBUs);
            return notSameBUs;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private string[] GetEntryNonMonomerBUs (string pdbId, string buType)
        {
            Dictionary<string, Dictionary<int, int>> entryBuContentHash = null;
            switch (buType)
            {
                case "pdb":
                    entryBuContentHash = entryBuInterfaces.GetPdbEntryBUEntityContent(pdbId);
                    break;

                case "pisa":
                    entryBuContentHash = entryBuInterfaces.GetPisaEntryBUEntityContent(pdbId);
                    break;

                default:
                    break;
            }
            string[] nonMonomerBUs = entryBuInterfaces.GetNonMonomerBUs(entryBuContentHash);
            return nonMonomerBUs;
        }
        #endregion

        #region debug add same PDB/PISA BU Comp to db
        /// <summary>
        /// 
        /// </summary>
        public void AddSamePdbPisaBAsDomainCompInfo()
        {
            string queryString = "Select * From PdbPisaBuComp Where InterfaceNum1 = InterfaceNum2 AND IsSame = '1';";
            DataTable pdbPisaCompTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);

            string pdbId = "";
            string pdbBuId = "";
            string pisaBuId = "";
            foreach (DataRow buCompRow in pdbPisaCompTable.Rows)
            {
                pdbId = buCompRow["PdbID"].ToString();
                pdbBuId = buCompRow["BuID1"].ToString().TrimEnd();
                pisaBuId = buCompRow["BuID2"].ToString().TrimEnd();

                if (!IsCrystPisaBuDomainCompExist(pdbId, pisaBuId))
                {
                    DataTable pdbBuCrystDomainCompTable = GetPdbBuDomainInterfaceCompTable(pdbId, pdbBuId);
                    if (pdbBuCrystDomainCompTable.Rows.Count > 0)
                    {
                        DataTable pisaBuCrystDomainCompTable = pdbBuCrystDomainCompTable.Copy();
                        pisaBuCrystDomainCompTable.TableName = "CrystPisaBuDomainInterfaceComp";
                        pisaBuCrystDomainCompTable.Columns["BuID"].DefaultValue = pisaBuId;
                        dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, pisaBuCrystDomainCompTable);
                        pisaBuCrystDomainCompTable.Clear();
                    }
                }
            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pisaBuId"></param>
        /// <returns></returns>
        private bool IsCrystPisaBuDomainCompExist(string pdbId, string pisaBuId)
        {
            string queryString = string.Format("Select * From CrystPisaBuDomainInterfaceComp Where PdbID = '{0}' AND BUID = '{1}' AND BuDomainInterfaceID > 0;", pdbId, pisaBuId);
            DataTable pisaBuCrystCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (pisaBuCrystCompTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pdbBuId"></param>
        /// <returns></returns>
        private DataTable GetPdbBuDomainInterfaceCompTable(string pdbId, string pdbBuId)
        {
            string queryString = string.Format("Select * From CrystPdbBuDomainInterfaceComp WHere PdbID = '{0}' AND BuID = '{1}' AND BuDomainInterfaceID > 0;", pdbId, pdbBuId);
            DataTable pdbBuCrystDomainCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            pdbBuCrystDomainCompTable.TableName = "CrystPdbBuDomainInterfaceComp";
            return pdbBuCrystDomainCompTable;
        }
        #endregion
    }
}
