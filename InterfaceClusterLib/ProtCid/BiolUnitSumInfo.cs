using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using BuQueryLib;
using DbLib;
using ProtCidSettingsLib;
using PfamLib.PfamArch;

namespace InterfaceClusterLib.ProtCid
{
    /* Add  biolunit tables for PDB and PISA to the database
     * component of a bu is in the order of Chain PFAM Architecture in the entry pfam architecture
     * PDB BUs: author-defined first, 
     * if there are no author-defined, then software-defined either from pisa or pqs
     * PISA BUs: from pisa web site
     * */
    public class BiolUnitSumInfo
    {
        #region member variables
        private DbQuery dbQuery = new DbQuery();
        private PfamArchitecture pfamArch = new PfamArchitecture();
        private BiolUnitQuery buQuery = new BiolUnitQuery();
        private DbInsert dbInsert = new DbInsert();
        private DataTable pdbBuFormatTable = null;
        private DataTable pisaBuFormatTable = null;
        private DataTable pfamEntryArchTable = null;
        private DataTable asuBuFormatTable = null;
        private StreamWriter logWriter = null;

        public struct BuFormatInfo
        {
            public string entityFormat;
            public string abcFormat;
            public string asymFormat;
            public string abcFormatPfam;
            public string authorFormat;
        }

        public enum ChainType
        {
            asym, author
        }
        #endregion

        #region biolunits
        /// <summary>
        /// build bu format tables and pfam entryarch table
        /// </summary>
        public void RetrieveBiolUnits ()
        {
            bool isUpdate = false;
            InitializeTables(isUpdate);
            logWriter = new StreamWriter("BuSumInfoLog.txt");
            logWriter.WriteLine(DateTime.Today.ToShortDateString ());

            string queryString = "Select Distinct PdbID From PfamEntityPfamArch;";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] pdbIds = new string[entryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbIds[count] = entryRow["PdbID"].ToString();
                count++;
            }
            RetrieveBiolUnits(pdbIds);

            logWriter.Close();
        }


        /// <summary>
        /// update bu format tables and pfam entrypfamarch table
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateBiolUnits(string[] updateEntries)
        {
            logWriter = new StreamWriter("BuSumInfoLog.txt", true);
            logWriter.WriteLine(DateTime.Today.ToShortDateString());

            bool isUpdate = true;
            InitializeTables(isUpdate);

            DeleteObsData(updateEntries);
            RetrieveBiolUnits(updateEntries);

            logWriter.Close();
        }

        /// <summary>
        /// also insert or update the PfamEntryPfamArch table too.
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <param name="buType"></param>
        /// <param name="isUpdate"></param>
        public void RetrieveBiolUnits(string[] pdbIds)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve Biological units.");

            ProtCidSettings.progressInfo.totalOperationNum = pdbIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = pdbIds.Length;

            Dictionary<int, string> entityPfamArchHash = null;
            Dictionary<string, string> entityChainLetterHash = null;
            Dictionary<string, BuFormatInfo> pdbBuFormatStructHash = null;
            Dictionary<string, BuFormatInfo> pisaBuFormatStructHash = null;
            Dictionary<string, BuFormatInfo> asuFormatStructHash = null;
            string[] entryPfamArchUnpCodes = null;
            int[] entitiesWithNoPfam = null;
            foreach (string pdbId in pdbIds)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                entryPfamArchUnpCodes = GetEntryPfamArchAndUnpCodes (pdbId, ref entityPfamArchHash);
                entityChainLetterHash = GetEntityChainNameInPfamArchOrder(pdbId, entityPfamArchHash, out entitiesWithNoPfam);

                // entity id and asym id and author chains
                Dictionary<string, List<string>>[] entityChainInfoHashs = GetEntityAsymAuthorChainHashs(pdbId);  //0: entity-asym 1: entity-author

                //pdb
                try
                {
                    pdbBuFormatStructHash = GetPdbBuFormats(pdbId, entityChainLetterHash, entityChainInfoHashs);
                    InsertBuDataToTable(pdbId, pdbBuFormatStructHash, pdbBuFormatTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": pdb error: " + ex.Message);
                    logWriter.WriteLine(pdbId + ": pdb error: " + ex.Message);
                    logWriter.Flush();
                }

                //pisa
                try
                {
                    pisaBuFormatStructHash = GetPisaBuFormats(pdbId, entityChainLetterHash);
                    InsertBuDataToTable(pdbId, pisaBuFormatStructHash, pisaBuFormatTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": pisa error: " + ex.Message);
                    logWriter.WriteLine(pdbId + ": pisa error: " + ex.Message);
                    logWriter.Flush();
                }

                // asu
                try
                {
                    asuFormatStructHash = GetAsuFormats (pdbId, entityChainLetterHash, entityChainInfoHashs);
                    InsertBuDataToTable(pdbId, asuFormatStructHash, asuBuFormatTable);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": pisa error: " + ex.Message);
                    logWriter.WriteLine(pdbId + ": pisa error: " + ex.Message);
                    logWriter.Flush();
                }

            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            logWriter.WriteLine("Done!");
        }
        #endregion

        #region BU info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityChainLetterHash"></param>
        /// <returns></returns>
        private Dictionary<string, BuFormatInfo> GetPdbBuFormats(string pdbId, Dictionary<string, string> entityChainLetterHash, Dictionary<string, List<string>>[] entityChainInfoHashs)
        {
            Dictionary<string, Dictionary<string, int>>[] buContentHashs = buQuery.GetPdbBuEntityAsymAuthContentHashs(pdbId);

            Dictionary<string, BuFormatInfo> buFormatInfoHash = FormatPdbBuAsuFormats(buContentHashs, entityChainLetterHash, entityChainInfoHashs);
            return buFormatInfoHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buContentHashs"></param>
        /// <param name="entityChainLetterHash"></param>
        /// <param name="entityChainInfoHashs"></param>
        /// <returns></returns>
        private Dictionary<string, BuFormatInfo> FormatPdbBuAsuFormats(Dictionary<string, Dictionary<string, int>>[] buContentHashs, Dictionary<string, string> entityChainLetterHash, Dictionary<string, List<string>>[] entityChainInfoHashs)
        {
            Dictionary<string, Dictionary<string, int>> buEntityContentHash = buContentHashs[(int)BiolUnitQuery.BuContentType.entity];
            // abc-pfam format         
            Dictionary<string, string> buAbcFormatPfamHash = FormatEntryBuPfamAbcFormat(buEntityContentHash, entityChainLetterHash);
            // entity format
            Dictionary<string, string> buEntityFormatHash = new Dictionary<string,string> ();
            foreach (string buId in buEntityContentHash.Keys)
            {
                string buEntityFormat = buQuery.GetEntityFormattedString(buEntityContentHash[buId]);
                buEntityFormatHash.Add(buId, buEntityFormat);
            }
            // abc format
            Dictionary<string, string> buAbcFormatHash = buQuery.ConvertEntityContentToAbcFormat(buEntityContentHash);
            // asym format
            Dictionary<string, string> buAsymFormatHash = new Dictionary<string,string> ();
            Dictionary<string, Dictionary<string, int>> buAsymContentHash = buContentHashs[(int)BiolUnitQuery.BuContentType.asym];
            Dictionary<string, List<string>> entityAsymHash = entityChainInfoHashs[(int)ChainType.asym];
            foreach (string buId in buAsymContentHash.Keys)
            {
                string buAsymFormat = buQuery.GetAsymFormattedString(buAsymContentHash[buId], entityAsymHash);
                buAsymFormatHash.Add(buId, buAsymFormat);
            }

            // author format
            Dictionary<string, string> buAuthFormatHash = new Dictionary<string,string> ();
            Dictionary<string, Dictionary<string, int>> buAuthContentHash = buContentHashs[(int)BiolUnitQuery.BuContentType.author];
            Dictionary<string, List<string>> entityAuthorHash = entityChainInfoHashs[(int)ChainType.author];
            foreach (string buId in buAuthContentHash.Keys)
            {
                string buAuthorFormat = buQuery.GetAuthorChainFormattedString(buAuthContentHash[buId], entityAuthorHash);
                buAuthFormatHash.Add(buId, buAuthorFormat);
            }
            Dictionary<string, BuFormatInfo> buFormatInfoHash = new Dictionary<string,BuFormatInfo> ();
            foreach (string buId in buEntityContentHash.Keys)
            {
                BuFormatInfo buFormatInfo = new BuFormatInfo();
                buFormatInfo.abcFormat = (string)buAbcFormatHash[buId];
                buFormatInfo.abcFormatPfam = (string)buAbcFormatPfamHash[buId];
                buFormatInfo.entityFormat = (string)buEntityFormatHash[buId];
                buFormatInfo.asymFormat = (string)buAsymFormatHash[buId];
                buFormatInfo.authorFormat = (string)buAuthFormatHash[buId];
                buFormatInfoHash.Add(buId, buFormatInfo);
            }
            return buFormatInfoHash;
        }

        /// <summary>
        /// PISA
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, BuFormatInfo> GetPisaBuFormats(string pdbId, Dictionary<string, string> entityChainLetterHash)
        {
            Dictionary<string, string>[] pisaBuFormatHashs = buQuery.GetPisaBuFormatHashs(pdbId);
            Dictionary<string, string> buEntityFormatHash = pisaBuFormatHashs[(int)BiolUnitQuery.BuContentType.entity];

            Dictionary<string, Dictionary<string, int>> buEntityContentHash = new Dictionary<string,Dictionary<string,int>> ();
            foreach (string buId in buEntityFormatHash.Keys)
            {
                string buEntityFormat = (string)buEntityFormatHash[buId];
                Dictionary<string, int> entityContentHash = buQuery.GetEntityContentHashFromEntityFormat (buEntityFormat);
                buEntityContentHash.Add(buId, entityContentHash);
            }
            Dictionary<string, string> buAbcFormatPfamHash = FormatEntryBuPfamAbcFormat(buEntityContentHash, entityChainLetterHash);

            Dictionary<string, BuFormatInfo> buFormatInfoHash = new Dictionary<string,BuFormatInfo> ();
            foreach (string buId in buEntityContentHash.Keys)
            {
                if (buAbcFormatPfamHash.ContainsKey(buId))
                {
                    BuFormatInfo buFormatInfo = new BuFormatInfo();
                    buFormatInfo.abcFormat = (string)pisaBuFormatHashs[(int)BiolUnitQuery.BuContentType.abc][buId];
                    buFormatInfo.abcFormatPfam = (string)buAbcFormatPfamHash[buId];
                    buFormatInfo.entityFormat = (string)pisaBuFormatHashs[(int)BiolUnitQuery.BuContentType.entity][buId];
                    buFormatInfo.asymFormat = (string)pisaBuFormatHashs[(int)BiolUnitQuery.BuContentType.asym][buId];
                    buFormatInfo.authorFormat = (string)pisaBuFormatHashs[(int)BiolUnitQuery.BuContentType.author][buId];
                    buFormatInfoHash.Add(buId, buFormatInfo);
                }
            }
            return buFormatInfoHash;
        }

        /// <summary>
        /// ASU
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityChainLetterHash"></param>
        /// <returns></returns>
        public Dictionary<string, BuFormatInfo> GetAsuFormats(string pdbId, Dictionary<string, string> entityChainLetterHash, Dictionary<string, List<string>>[] entityChainInfoHashes)
        {
            Dictionary<string,Dictionary<string, int>>[] asuContentHashes = buQuery.GetAsuEntityAsymAuthContentHashs(pdbId);
            Dictionary<string, BuFormatInfo> buFormatInfoHash = FormatPdbBuAsuFormats(asuContentHashes, entityChainLetterHash, entityChainInfoHashes);

            return buFormatInfoHash;
        }
        #endregion

        #region pfam arch info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityPfamArchHash"></param>
        /// <returns></returns>
        public string[] GetEntryPfamArchAndUnpCodes(string pdbId, ref Dictionary<int, string> entityPfamArchHash)
        {
            entityPfamArchHash = pfamArch.GetEntryEntityPfamArchHash (pdbId);

            List<string> entityPfamArchList = new List<string> ();
            string entryPfamArch = "";
            string entityPfamArch = "";
            List<int> entityList = new List<int> ();
            foreach (int entityId in entityPfamArchHash.Keys)
            {
                entityPfamArch = entityPfamArchHash[entityId].ToString();
                if (entityPfamArch == "-" || entityPfamArch == "")
                {
                    continue;
                }
                entityPfamArchList.Add(entityPfamArch);
                entityList.Add(entityId);
            }
        //    entityPfamArchList.Sort();
            SortEntityPfamArchs(entityPfamArchList, entityList);

            foreach (string lsPfamArch in entityPfamArchList)
            {
                entryPfamArch += lsPfamArch + ";";
            }
            entryPfamArch = entryPfamArch.TrimEnd(';');

            string entryUnpCodes = "";
            string entityUnpCodes = "";
            foreach (int entityId in entityList)
            {
                entityUnpCodes = GetEntityUnpCodes(pdbId, entityId);
                if (entityUnpCodes == "")
                {
                    entityUnpCodes = "-";
                }
                entryUnpCodes += (entityUnpCodes + ";");
            }
            entryUnpCodes = entryUnpCodes.TrimEnd(';');
            string[] entryPfamArchUnpCodes = new string[2];
            entryPfamArchUnpCodes[0] = entryPfamArch;
            entryPfamArchUnpCodes[1] = entryUnpCodes;
            return entryPfamArchUnpCodes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityPfamArchList"></param>
        /// <param name="entityList"></param>
        private void SortEntityPfamArchs(List<string> entityPfamArchList, List<int> entityList)
        {
            for (int i = 0; i < entityPfamArchList.Count; i++)
            {
                for (int j = i + 1; j < entityPfamArchList.Count; j++)
                {
                    if (string.Compare((string)entityPfamArchList[i], (string)entityPfamArchList[j]) > 0)
                    {
                        string temp = (string)entityPfamArchList[i];
                        entityPfamArchList[i] = entityPfamArchList[j];
                        entityPfamArchList[j] = temp;
                        int tempId = (int)entityList[i];
                        entityList[i] = entityList[j];
                        entityList[j] = tempId;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string GetEntityUnpCodes(string pdbId, int entityId)
        {
            string queryString = "";
            DataTable unpCodeTable = null;
            DataRow[] unpCodeRows = null;
            if (pdbDbRefSiftsTable != null)
            {
                unpCodeRows = pdbDbRefSiftsTable.Select(string.Format ("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId));
                if (unpCodeRows.Length == 0)
                {
                    if (pdbDbRefXmlTable != null)
                    {
                        unpCodeRows = pdbDbRefXmlTable.Select(string.Format("PdbID = '{0}' AND EntityID = '{1}'", pdbId, entityId));
                    }
                    else
                    {
                        queryString = string.Format("Select DbCode From PdbDbRefXml " +
                            " Where PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP';", pdbId, entityId);
                        unpCodeTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                        unpCodeRows = unpCodeTable.Select();
                    }                   
                }
            }
            else
            {
                queryString = string.Format("Select DbCode From PdbDbRefSifts " +
                    " Where PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP';", pdbId, entityId);
                unpCodeTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                if (unpCodeTable.Rows.Count == 0)
                {
                    queryString = string.Format("Select DbCode From PdbDbRefXml " +
                        " Where PdbID = '{0}' AND EntityID = {1} AND DbName = 'UNP';", pdbId, entityId);
                    unpCodeTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                }
                unpCodeRows = unpCodeTable.Select();
            }
            string entityUnpCodes = "";
            foreach (DataRow unpCodeRow in unpCodeRows)
            {
                entityUnpCodes += ("(" + unpCodeRow["DbCode"].ToString().TrimEnd() + ")_");
            }
            entityUnpCodes = entityUnpCodes.TrimEnd('_');
            return entityUnpCodes;
        }
        #endregion

        #region format in the order of pfam arch in the entrypfamarch
        /// <summary>
        /// 
        /// </summary>
        /// <param name="buEntityContentHash"></param>
        /// <param name="entityChainLetterHash"></param>
        /// <returns></returns>
        private Dictionary<string, string> FormatEntryBuPfamAbcFormat(Dictionary<string, Dictionary<string, int>> buEntityContentHash, Dictionary<string, string> entityChainLetterHash)
        {
            string chainLetter = "";
            string abcFormatPfamArchOrder = "";
            Dictionary<string, string> buAbcFormatHash = new Dictionary<string,string> ();
            foreach (string buId in buEntityContentHash.Keys)
            {
                Dictionary<string, int> entityContentHash = buEntityContentHash[buId];
                Dictionary<string, int> chainContentHash = new Dictionary<string,int> ();
                try
                {
                    foreach (string entityId in entityContentHash.Keys)
                    {
                        if (entityChainLetterHash.ContainsKey(entityId))
                        {
                            chainLetter = entityChainLetterHash[entityId];
                            chainContentHash.Add(chainLetter, entityContentHash[entityId]);
                        }
                    }
                }
                catch 
                {
                    // too many chains, not suitable for abc format
                    chainContentHash.Clear();
                }
          /*      if (chainContentHash.Count == 0)
                {
                    continue;
                }*/
                List<string> chainList = new List<string> (chainContentHash.Keys);
                chainList.Sort();
                abcFormatPfamArchOrder = "";
                int numOfCopies = 0;
                foreach (string chainName in chainList)
                {
                    numOfCopies = (int)chainContentHash[chainName];
                    if (numOfCopies > 1)
                    {
                        abcFormatPfamArchOrder += (chainName + numOfCopies.ToString());
                    }
                    else
                    {
                        abcFormatPfamArchOrder += chainName;
                    }
                }
                buAbcFormatHash.Add(buId, abcFormatPfamArchOrder);
            }
            return buAbcFormatHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityPfamArchHash"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetEntityChainNameInPfamArchOrder(string pdbId, Dictionary<int, string> entityPfamArchHash, out int[] entitiesWithNoPfam)
        {
            List<int> entityList =  new List<int> (entityPfamArchHash.Keys);
            entityList.Sort();
            int[] entityIds = new int[entityList.Count];
            entityList.CopyTo(entityIds);
            string[] entityPfamArchs = new string[entityList.Count];
            int count = 0;
            foreach (int entityId in entityIds)
            {
                entityPfamArchs[count] = (string)entityPfamArchHash[entityId];
                count++;
            }
            for (int i = 0; i < entityPfamArchs.Length; i++)
            {
                for (int j = i + 1; j < entityPfamArchs.Length; j++)
                {
                    if (string.Compare(entityPfamArchs[i], entityPfamArchs[j]) > 0)
                    {
                        string temp = entityPfamArchs[i];
                        entityPfamArchs[i] = entityPfamArchs[j];
                        entityPfamArchs[j] = temp;
                        int tempEntity = entityIds[i];
                        entityIds[i] = entityIds[j];
                        entityIds[j] = tempEntity;
                    }
                }
            }
            // add those entities without pfam defined
            entitiesWithNoPfam = GetLeftEntryEntities(pdbId, entityIds);
           
            List<int> allEntityList = new List<int> (entityIds);
            allEntityList.AddRange(entitiesWithNoPfam);
            Dictionary<string, string> entityChainLetterHash = new Dictionary<string,string> ();
            int chainCount = 0;
            int entityCount = 0;
            foreach (int entityId in allEntityList)
            {
                chainCount = entityCount % buQuery.chainLetters.Length;
                entityChainLetterHash.Add(entityId.ToString (), buQuery.chainLetters[chainCount].ToString());
                entityCount++;
            }
            return entityChainLetterHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entitiesWithPfam"></param>
        /// <returns></returns>
        private int[] GetLeftEntryEntities(string pdbId, int[] entitiesWithPfam)
        {
            List<int> entryEntityList = new List<int> ();
            int entityId = 0;
            if (asuTable != null)
            {
                DataRow[] entityRows = asuTable.Select(string.Format ("PdbID = '{0}'", pdbId));
                foreach (DataRow entityRow in entityRows)
                {
                    entityId = Convert.ToInt32 (entityRow["EntityID"].ToString ());
                    if (! entryEntityList.Contains (entityId))
                    {
                        entryEntityList.Add(entityId);
                    }
                }
            }
            else
            {
                string queryString = string.Format("Select Distinct EntityID From AsymUnit " +
                    " WHERE PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
                DataTable protEntityTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                foreach (DataRow entityRow in protEntityTable.Rows)
                {
                    entityId = Convert.ToInt32(entityRow["EntityID"].ToString ());
                    entryEntityList.Add(entityId);
                }
            }
            List<int> leftEntityList = new List<int> ();
            foreach (int lsEntityId in entryEntityList)
            {
                if (Array.IndexOf(entitiesWithPfam, lsEntityId) < 0)
                {
                    leftEntityList.Add(lsEntityId);
                }
            }
            return leftEntityList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<string, List<string>>[] GetEntityAsymAuthorChainHashs(string pdbId)
        {
            DataRow[] chainRows = null;
            if (asuTable != null)
            {
                chainRows = asuTable.Select(string.Format ("PdbID = '{0}'", pdbId));
            }
            else
            {
                string queryString = string.Format("Select EntityID, AsymID, AuthorChain From AsymUnit " +
                    " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
                DataTable entityChainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                chainRows = entityChainTable.Select();
            }
            Dictionary<string, List<string>> entityAsymChainHash = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> entityAuthChainHash = new Dictionary<string, List<string>>();
            string entityId = "";
            string asymChain = "";
            string authChain = "";
            foreach (DataRow entityRow in chainRows)
            {
                entityId = entityRow["EntityID"].ToString ();
                asymChain = entityRow["AsymID"].ToString().TrimEnd();
                authChain = entityRow["AuthorChain"].ToString().TrimEnd();
                if (entityAsymChainHash.ContainsKey(entityId))
                {
                    entityAsymChainHash[entityId].Add(asymChain);
                }
                else
                {
                    List<string> asymChainList = new List<string> ();
                    asymChainList.Add(asymChain);
                    entityAsymChainHash.Add(entityId, asymChainList);
                }
                if (entityAuthChainHash.ContainsKey(entityId))
                {
                    entityAuthChainHash[entityId].Add(authChain);
                }
                else
                {
                    List<string> authChainList = new List<string> ();
                    authChainList.Add(authChain);
                    entityAuthChainHash.Add(entityId, authChainList);
                }
            }
            Dictionary<string, List<string>>[] entityChainInfoHashs = new Dictionary<string, List<string>>[2];
            entityChainInfoHashs[(int)ChainType.asym] = entityAsymChainHash;
            entityChainInfoHashs[(int)ChainType.author] = entityAuthChainHash;
            return entityChainInfoHashs;
        }
        #endregion

        #region initialize tables in memory and db
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private void InitializeTables (bool isUpdate)
        {
            string buType = "pdb";
            pdbBuFormatTable = InitializeBuTables (isUpdate, buType);
            buType = "pisa";
            pisaBuFormatTable = InitializeBuTables(isUpdate, buType);
            buType = "asu";
            asuBuFormatTable = InitializeBuTables(isUpdate, buType);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUpdate"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        private DataTable InitializeBuTables (bool isUpdate, string buType)
        {
            string tableName = "";
            if (buType == "asu")
            {
                tableName = "PdbAsu";
            }
            else
            {
                tableName = buType + "BiolUnits";
            }
            if (!isUpdate)
            {
                InitializeBuDbTable(buType);
            }
            string[] tableColumns = {"PdbID", "BuID", "EntityFormat", "AsymFormat", "AbcFormat", "AuthorFormat", 
                                      "AbcFormatPfam"};
            DataTable buFormatTable = new DataTable(tableName);
            foreach (string col in tableColumns)
            {
                buFormatTable.Columns.Add(new DataColumn(col));
            }
            return buFormatTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buType"></param>
        private void InitializeBuDbTable(string buType)
        {
            DbCreator dbCreate = new DbCreator();
            string tableName = "";
            if (buType == "asu")
            {
                tableName = "PdbAsu";
            }
            else
            {
                tableName = buType + "BiolUnits";
            }
            string createTableString = "CREATE TABLE " + tableName + " ( " + 
                " PdbID CHAR(4) NOT NULL, " + 
                " BuID VARCHAR(8) NOT NULL, " +
                " EntityFormat BLOB Sub_Type TEXT NOT NULL,  " +
                " AsymFormat BLOB Sub_Type TEXT NOT NULL, " +
                " AbcFormat BLOB Sub_Type TEXT NOT NULL, " +
                " AuthorFormat BLOB Sub_Type TEXT NOT NULL, " + 
          //      " EntryPfamArch VARCHAR(1200) NOT NULL, " + 
                " AbcFormatPfam BLOB Sub_Type TEXT NOT NULL );";
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName);

            string createIndexString = "CREATE INDEX " + tableName + "_pdb ON " + tableName + "(PdbID, BuID);";
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, tableName);
        }      
        #endregion

        #region insert data into table and db
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entryPfamArch"></param>
        /// <param name="buFormatStructHash"></param>
        /// <param name="buFormatTable"></param>
        private void InsertBuDataToTable(string pdbId, Dictionary<string, BuFormatInfo> buFormatStructHash, DataTable buFormatTable)
        {
            foreach (string buId in buFormatStructHash.Keys)
            {
                BuFormatInfo buFormatInfo = buFormatStructHash[buId];
                DataRow buRow = buFormatTable.NewRow();
                buRow["PdbID"] = pdbId;
                buRow["BuID"] = buId;
                buRow["EntityFormat"] = buFormatInfo.entityFormat;
                buRow["AsymFormat"] = buFormatInfo.asymFormat;
                buRow["AuthorFormat"] = buFormatInfo.authorFormat;
                buRow["AbcFormat"] = buFormatInfo.abcFormat;
                buRow["AbcFormatPfam"] = buFormatInfo.abcFormatPfam;
          //      buRow["EntryPfamArch"] = entryPfamArch;
                buFormatTable.Rows.Add(buRow);
            }
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.protcidDbConnection, buFormatTable);
            buFormatTable.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private void DeleteObsData(string[] updateEntries)
        {
            foreach (string pdbId in updateEntries)
            {
                DeleteEntryData(pdbId, pdbBuFormatTable.TableName);
                DeleteEntryData(pdbId, pisaBuFormatTable.TableName);
                DeleteEntryData(pdbId, asuBuFormatTable.TableName);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="tableName"></param>
        private void DeleteEntryData(string pdbId, string tableName)
        {
            string deleteString = string.Format("Delete From {0} WHERE PdbID = '{1}';", tableName, pdbId);
            ProtCidSettings.protcidQuery.Query( deleteString);
        }
        #endregion

        #region for debug
        /// <summary>
        ///  these tables are for error message:
        ///  too many open file handlers to database.
        /// </summary>
        private DataTable pdbDbRefSiftsTable = null;
        private DataTable asuTable = null;
        private DataTable pdbDbRefXmlTable = null;
        /// <summary>
        /// update bu format tables and pfam entrypfamarch table
        /// </summary>
        /// <param name="updateEntries"></param>
        public void UpdateBiolUnits()
        {
            logWriter = new StreamWriter("BuSumInfoLog.txt");
            logWriter.WriteLine(DateTime.Today.ToShortDateString());

            bool isUpdate = true;
            InitializeTables(isUpdate);

            //      string[] missingEntries = GetMissingEntriesInBuTables();
            //     string[] entriesWithZeroBu = GetEntriesWithBuZero();

            string queryString = "Select Distinct PdbID From PfamEntityPfamArch;";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            queryString = "Select PdbID, EntityID, DbCode From PdbDbRefSifts Where DbName = 'UNP'";
            pdbDbRefSiftsTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            queryString = "Select PdbID, EntityID, DbCode From PdbDbRefXml Where DbName = 'UNP'";
            pdbDbRefXmlTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            queryString = "Select PdbID, EntityID, AsymID, AuthorChain From AsymUnit Where PolymerType = 'polypeptide';";
            asuTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            List<string> pdbList = new List<string> ();
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                pdbList.Add(pdbId);
            }
            string[] pdbIds = pdbList.ToArray();

            RetrieveBiolUnits(pdbIds);

            logWriter.Close();
        
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        private void DeleteObsData(string pdbId, string buType)
        {
            switch (buType)
            {
                case "pdb":
                    DeleteEntryData(pdbId, pdbBuFormatTable.TableName);
                    break;

                case "pisa":
                    DeleteEntryData(pdbId, pisaBuFormatTable.TableName);
                    break;

                case "asu":
                    DeleteEntryData(pdbId, pfamEntryArchTable.TableName);
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// also insert or update the PfamEntryPfamArch table too.
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <param name="buType"></param>
        /// <param name="isUpdate"></param>
        public void RetrieveBiolUnits(string[] pdbIds, string buType)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieve Biological units.");

            ProtCidSettings.progressInfo.totalOperationNum = pdbIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = pdbIds.Length;

            Dictionary<int, string> entityPfamArchHash = null;
            Dictionary<string, string> entityChainLetterHash = null;
            Dictionary<string, BuFormatInfo> buFormatStructHash = null;
            string[] entryPfamArchUnpCodes = null;
            int[] entitiesWithNoPfam = null;
            foreach (string pdbId in pdbIds)
            {
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                entryPfamArchUnpCodes = GetEntryPfamArchAndUnpCodes(pdbId, ref entityPfamArchHash);
                entityChainLetterHash = GetEntityChainNameInPfamArchOrder(pdbId, entityPfamArchHash, out entitiesWithNoPfam);

                // entity id and asym id and author chains
                Dictionary<string, List<string>>[] entityChainInfoHashs = GetEntityAsymAuthorChainHashs(pdbId);  //0: entity-asym 1: entity-author

                switch (buType)
                {
                    case "pdb":
                        //pdb
                        try
                        {
                            buFormatStructHash = GetPdbBuFormats(pdbId, entityChainLetterHash, entityChainInfoHashs);

                            DeleteEntryData(pdbId, pdbBuFormatTable.TableName);
                            InsertBuDataToTable(pdbId, buFormatStructHash, pdbBuFormatTable);
                        }
                        catch (Exception ex)
                        {
                            ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": pdb error: " + ex.Message);
                            logWriter.WriteLine(pdbId + ": pdb error: " + ex.Message);
                            logWriter.Flush();
                        }
                        break;

                    case "pisa":
                        //pisa
                        try
                        {
                            buFormatStructHash = GetPisaBuFormats(pdbId, entityChainLetterHash);

                            DeleteEntryData(pdbId, pisaBuFormatTable.TableName);
                            InsertBuDataToTable(pdbId, buFormatStructHash, pisaBuFormatTable);
                        }
                        catch (Exception ex)
                        {
                            ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": pisa error: " + ex.Message);
                            logWriter.WriteLine(pdbId + ": pisa error: " + ex.Message);
                            logWriter.Flush();
                        }
                        break;

                    case "asu":
                        // asu
                        try
                        {
                            buFormatStructHash = GetAsuFormats(pdbId, entityChainLetterHash, entityChainInfoHashs);

                            DeleteEntryData(pdbId, pfamEntryArchTable.TableName);
                            InsertBuDataToTable(pdbId, buFormatStructHash, asuBuFormatTable);
                        }
                        catch (Exception ex)
                        {
                            ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + ": pisa error: " + ex.Message);
                            logWriter.WriteLine(pdbId + ": pisa error: " + ex.Message);
                            logWriter.Flush();
                        }
                        break;

                    default:
                        break;
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }


        private string[] GetMissingEntriesInBuTables()
        {
            string queryString = "Select Distinct PdbID From PfamEntityPfamArch;";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pdbId = "";
            List<string> missingEntryList = new List<string> ();
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                if (!IsEntryBuInTables(pdbId))
                {
                    missingEntryList.Add(pdbId);
                }
            }
            string[] missingEntries = new string[missingEntryList.Count];
            missingEntryList.CopyTo(missingEntries);
            return missingEntries;
        }

        private bool IsEntryBuInTables(string pdbId)
        {
            string queryString = string.Format("Select * From PdbBiolUnits Where PdbID = '{0}';", pdbId);
            DataTable pdbBuTable = ProtCidSettings.protcidQuery.Query( queryString);

            if (pdbBuTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetEntriesWithBuZero ()
        {
            string queryString = "Select Distinct PdbID From PdbBiolUnits Where BuID = '0';";
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
        #endregion

        #region add unpcodes to pfamentrypfamarch table
        private DbUpdate dbUpdate = new DbUpdate();
        /// <summary>
        /// 
        /// </summary>
        public void AddEntryUnpCodes()
        {
            string queryString = "Select Distinct PdbID From PfamEntryPfamArch Where EntryUnpCodes is null;";
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                string[] entryPfamArchUnpCodes = GetEntryPfamArchAndUnpCodes(pdbId);
                UpdateEntryUnpCodes(pdbId, entryPfamArchUnpCodes[1]);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entryUnpCodes"></param>
        private void UpdateEntryUnpCodes(string pdbId, string entryUnpCodes)
        {
            string updateString = string.Format("Update PfamEntryPfamArch " + 
                " Set EntryUnpCodes = '{0}' Where PdbID = '{1}';", entryUnpCodes, pdbId);
            dbUpdate.Update(ProtCidSettings.pdbfamDbConnection, updateString);   
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityPfamArchHash"></param>
        /// <returns></returns>
        public string[] GetEntryPfamArchAndUnpCodes(string pdbId)
        {
            Dictionary<int, string> entityPfamArchHash = pfamArch.GetEntryEntityPfamArchHash(pdbId);

            List<string> entityPfamArchList = new List<string> ();
            string entryPfamArch = "";
            string entityPfamArch = "";
            List<int> entityList = new List<int> ();
            foreach (int entityId in entityPfamArchHash.Keys)
            {
                entityPfamArch = entityPfamArchHash[entityId].ToString();
                if (entityPfamArch == "-" || entityPfamArch == "")
                {
                    continue;
                }
                entityPfamArchList.Add(entityPfamArch);
                entityList.Add(entityId);
            }
            //    entityPfamArchList.Sort();
            SortEntityPfamArchs(entityPfamArchList, entityList);

            foreach (string lsPfamArch in entityPfamArchList)
            {
                entryPfamArch += lsPfamArch + ";";
            }
            entryPfamArch = entryPfamArch.TrimEnd(';');

            string entryUnpCodes = "";
            string entityUnpCodes = "";
            foreach (int entityId in entityList)
            {
                entityUnpCodes = GetEntityUnpCodes(pdbId, entityId);
                if (entityUnpCodes == "")
                {
                    entityUnpCodes = "-";
                }
                entryUnpCodes += (entityUnpCodes + ";");
            }
            entryUnpCodes = entryUnpCodes.TrimEnd(';');
            string[] entryPfamArchUnpCodes = new string[2];
            entryPfamArchUnpCodes[0] = entryPfamArch;
            entryPfamArchUnpCodes[1] = entryUnpCodes;
            return entryPfamArchUnpCodes;
        }
        #endregion

        #region get biological assemblies
        /// <summary>
        /// 
        /// </summary>
        public void CollectTrueBiologyAssemblies ()
        {
            StreamWriter buClusterInfoWriter = new StreamWriter("CompiledBiolAssemblies.txt");
            buClusterInfoWriter.WriteLine("PdbID\tBiolAssemblyID\tBiolAssembly\tSameAsu\t" + 
                "GroupID\tClusterID\tM\t#Entries/Cluster\tN\t#Entries/Family\tSurfaceArea\t#PDBBiolAsseblies\tMinSeqIdentity\tPfamArch");
            StreamWriter buBestClusterInfoWriter = new StreamWriter("CompiledBiolAssemblies_bestM.txt");
            buBestClusterInfoWriter.WriteLine("PdbID\tBiolAssemblyID\tBiolAssembly\tSameAsu\t" + 
                "GroupID\tClusterID\tM\t#Entries/Cluster\tN\t#Entries/Family\tSurfaceArea\t#PDBBiolAsseblies\tMinSeqIdentity\tPfamArch");
            string queryString = "Select Distinct PdbID, PdbBU, PdbBuID From PfamSuperClusterEntryInterfaces " + 
                " Where InPdb = '1';";
            DataTable buTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            string buId = "";
            string pdbBu = "";
            bool isAuthorDefined = false;
            foreach (DataRow buRow in buTable.Rows)
            {
                pdbId = buRow["PdbID"].ToString();
                buId = buRow["PdbBuID"].ToString().TrimEnd();
                pdbBu = buRow["PdbBu"].ToString().TrimEnd();
                isAuthorDefined = IsBuAuthorDefined(pdbId, buId);
                if (! isAuthorDefined)
                {
                    continue;
                }
                GetBuClusterSumInfo(pdbId, buId, pdbBu, buClusterInfoWriter, buBestClusterInfoWriter);
            }
            buClusterInfoWriter.Close();
            buBestClusterInfoWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <returns></returns>
        private void GetBuClusterSumInfo(string pdbId, string buId, string pdbBu, StreamWriter buClusterInfoWriter, 
            StreamWriter buBestClusterInfoWriter)
        {
            bool isSameAsu = IsBuSameAsASU(pdbId, buId);
            string sameAsu = "F";
            if (isSameAsu)
            {
                sameAsu = "T";
            }
            string[] groupClusters = GetGroupClusterInfo(pdbId, buId);
            int groupId = 0;
            int clusterId = 0;
            string buClusterInfoString = "";
            string clusterInfoString = "";
            int bestM = 0;
            string bestBuClusterInfoString = "";
            int M = 0;
            string pfamArchRel = "";
            foreach (string groupCluster in groupClusters)
            {
                string[] fields = groupCluster.Split('_');
                groupId = Convert.ToInt32(fields[0]);
                clusterId = Convert.ToInt32(fields[1]);
                DataTable clusterSumInfoTable = GetClusterSumInfo(groupId, clusterId);

                M = Convert.ToInt32(clusterSumInfoTable.Rows[0]["NumOfCfgCluster"].ToString());

                pfamArchRel = GetGroupPfamArch(groupId);
                clusterInfoString = pdbId + "\t" + buId + "\t" + pdbBu + "\t" + sameAsu + "\t" +
                    groupId.ToString() + "\t" + clusterId.ToString() + "\t" +
                    clusterSumInfoTable.Rows[0]["NumOfCfgCluster"].ToString() + "\t" +
                    clusterSumInfoTable.Rows[0]["NumOfEntryCluster"].ToString() + "\t" +
                    clusterSumInfoTable.Rows[0]["NumOfCfgFamily"].ToString() + "\t" +
                    clusterSumInfoTable.Rows[0]["NumOfEntryFamily"].ToString() + "\t" +
                    clusterSumInfoTable.Rows[0]["SurfaceArea"].ToString() + "\t" +
                    clusterSumInfoTable.Rows[0]["InPdb"].ToString() + "\t" +
                    clusterSumInfoTable.Rows[0]["MinSeqIdentity"].ToString() + "\t" +
                    pfamArchRel;
                buClusterInfoString += (clusterInfoString + "\r\n");

                if (M > bestM)
                {
                    bestM = M;
                    bestBuClusterInfoString = clusterInfoString;
                }
            }
            buClusterInfoWriter.WriteLine(buClusterInfoString);
            buBestClusterInfoWriter.WriteLine(bestBuClusterInfoString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private DataTable GetClusterSumInfo(int groupId, int clusterId)
        {
            string queryString = string.Format("Select SurfaceArea, InPdb, NumOfCfgCluster, NumOfCfgFamily, " + 
                "NumOfEntryCluster, NumOfEntryFamily, MinSeqIdentity From PfamSuperClusterSumInfo " + 
                " Where SuperGroupSeqID = {0} AND ClusterID = {1};", groupId, clusterId);
            DataTable clusterSumInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            return clusterSumInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private string GetGroupPfamArch(int groupId)
        {
            string queryString = string.Format("Select ChainRelPfamArch From PfamSuperGroups Where SuperGroupSeqID = {0};", groupId);
            DataTable pfamArchRelTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pfamArchRel = pfamArchRelTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
            return pfamArchRel;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <returns></returns>
        private string[] GetGroupClusterInfo(string pdbId, string buId)
        {
            string queryString = string.Format("Select Distinct SuperGroupSeqID, ClusterID " +
                " From PfamSuperClusterEntryInterfaces Where PdbID = '{0}' AND PdbBuID = '{1}' AND InPdb = '1';", pdbId, buId);
            DataTable groupClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] groupClusters = new string[groupClusterTable.Rows.Count ];
            int count = 0;
            foreach (DataRow clusterRow in groupClusterTable.Rows)
            {
                groupClusters[count] = clusterRow["SuperGroupSeqID"].ToString() + "_" +
                    clusterRow["ClusterID"].ToString();
                count++;
            }
            return groupClusters;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <returns></returns>
        private bool IsBuAuthorDefined(string pdbId, string buId)
        {
            string queryString = string.Format("Select * From PdbBuStat Where PdbID= '{0}' AND BiolUnitID = '{1}';",
                pdbId, buId);
            DataTable buStatTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (buStatTable.Rows.Count > 0)
            {
                string details = buStatTable.Rows[0]["Details"].ToString().TrimEnd();
                if (details.IndexOf("author") > -1)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <returns></returns>
        private bool IsBuSameAsASU(string pdbId, string buId)
        {
            string queryString = string.Format("Select PdbBuGen.* From AsymUnit, PdbBuGen " + 
                " Where PdbBuGen.PdbID = '{0}' AND PdbBuGen.BiolUnitID = '{1}' AND " + 
                " AsymUnit.PdbID = PdbBuGen.PdbID AND AsymUnit.AsymID = PdbBuGen.AsymID AND " + 
                " PolymerType = 'polypeptide';", pdbId, buId);
            DataTable buGenTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string symmetryString = "";
            foreach (DataRow buGenRow in buGenTable.Rows)
            {
                symmetryString = buGenRow["SymmetryString"].ToString().TrimEnd();
                if (symmetryString != "1_555")
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        public void WriteSumInfoAboutBiolAssemblies()
        {
            StreamWriter dataWriter = new StreamWriter("CompiledBiolAssemblySumInfo.txt");
            string biolAssemblyFile = "CompiledBiolAssemblies_bestM.txt";
            dataWriter.WriteLine("M >= 4 and SeqIdentity <= 90%");
            string dataLine = WriteSumInfoAboutBiolAssemblies(biolAssemblyFile, false);
            dataWriter.WriteLine(dataLine);
            dataLine = WriteSumInfoAboutBiolAssemblies(biolAssemblyFile, true);
            dataWriter.WriteLine(dataLine);
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="biolAssemblyFile"></param>
        /// <param name="isSameAsu"></param>
        /// <returns></returns>
        public string WriteSumInfoAboutBiolAssemblies(string biolAssemblyFile, bool isSameAsu)
        {
            int MCutoff = 4;
            double seqIdentityCutoff = 90.0;
            string sameAsu = "F";
            if (isSameAsu)
            {
                sameAsu = "T";
            }
            int numOfHomoDimer = 0;
            int numOfHomooligomer = 0;
            int numOfHeterodimer = 0;
            int numOfHeterooligomer = 0;
            int m = 0;
            double seqIdentity = 0;
            string pdbBuFormat = "";
            string buType = "";
            List<string> pfamArchRelList = new List<string>();
            List<string> chainPfamArchList = new List<string>();
            string pfamArchRel = "";
            StreamReader dataReader = new StreamReader(biolAssemblyFile);
            string line = dataReader.ReadLine (); // header line
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                if (fields[3] == sameAsu)
                {
                    m = Convert.ToInt32(fields[6]);
                    seqIdentity = Convert.ToDouble(fields[12]);
                    if (m >= MCutoff && seqIdentity <= seqIdentityCutoff)
                    {
                        pdbBuFormat = fields[2];
                        buType = GetBiolAssemblyType(pdbBuFormat);
                        switch (buType)
                        {
                            case "Homodimer":
                                numOfHomoDimer++;
                                break;

                            case "Homooligomer":
                                numOfHomooligomer++;
                                break;

                            case "Heterodimer":
                                numOfHeterodimer++;
                                break;

                            case "Heterooligomer":
                                numOfHeterooligomer++;
                                break;

                            default: break;
                        }
                        pfamArchRel = fields[13];
                        string[] pfamArchFields = pfamArchRel.Split(';');
                        if (! pfamArchRelList.Contains(pfamArchRel))
                        {
                            pfamArchRelList.Add(pfamArchRel);
                        }
                        foreach (string pfamArchField in pfamArchFields)
                        {
                            if (!chainPfamArchList.Contains(pfamArchField))
                            {
                                chainPfamArchList.Add(pfamArchField);
                            }
                        }
                    }
                }
            }
            dataReader.Close();
            string dataLine = "";
            if (isSameAsu)
            {
                dataLine = "Summary info for Biological Assemblies not same as ASU\r\n";
            }
            else
            {
                dataLine = "Summary info for Biological Assemblies same as ASU\r\n";
            }
       //     dataLine += "#Monomers: " + numOfMonomer.ToString () + "\r\n";
            dataLine += "#Homodimers: " + numOfHomoDimer.ToString () + "\r\n";
            dataLine += "#Homooligomers: " + numOfHomooligomer.ToString() + "\r\n";
            dataLine += "#Heterodimers: " + numOfHeterodimer.ToString() + "\r\n";
            dataLine += "#Heterooligomers: " + numOfHeterooligomer.ToString() + "\r\n";
            dataLine += "#Dif PfamArch Rel: " + pfamArchRelList.Count.ToString() + "\r\n";
            dataLine += "#Dif Chain PfamArch: " + chainPfamArchList.Count.ToString();
            return dataLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buAbcFormat"></param>
        /// <returns></returns>
        private string GetBiolAssemblyType(string buAbcFormat)
        {
            if (buAbcFormat == "A")
            {
                return "Monomer";
            }
            if (buAbcFormat.IndexOf("B") > -1) // hetero
            {
                if (buAbcFormat == "AB")
                {
                    return "Heterodimer";
                }
                else
                {
                    return "Heterooligomer";
                }
            }
            else // homo
            {
                if (buAbcFormat == "A2")
                {
                    return "Homodimer";
                }
                else
                {
                    return "Homooligomer";
                }
            }
        }

        #region piqsi
        /// <summary>
        /// 
        /// </summary>
        public void WriteSumInfoAboutPiqsiBAs()
        {
            StreamWriter buTypeWriter = new StreamWriter(@"D:\DbProjectData\Piqsi\PiqsiBuType.txt");
            buTypeWriter.WriteLine("PdbID\tBuID\tBuAbc\tBuType\tSameAsu");
            StreamReader dataReader = new StreamReader(@"D:\DbProjectData\Piqsi\PiQSi_list_2012-5-29_17_46_6.txt");
            string line = dataReader.ReadLine (); // header line
            string pdbId = "";
            string buId = "";
            string prePdbId = "";
            List<string> buIdList = new List<string>();           
            int[] numOfBuTypes = new int[5];
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                if (fields[1] == "NO") // error flag
                {
                    string[] buFields = fields[0].Split('_');
                    if (buFields.Length == 2)
                    {
                        pdbId = buFields[0];
                        buId = buFields[1];
                    }
                    else
                    {
                        pdbId = fields[0];
                        buId = "1";
                    }

                    if (pdbId != prePdbId)
                    {
                        string[] entryBuIds = new  string[buIdList.Count];
                        buIdList.CopyTo(entryBuIds);
                        WriteEntryBuSumInfoToFile(prePdbId, entryBuIds, ref numOfBuTypes, buTypeWriter);
                        buIdList.Clear();
                        prePdbId = pdbId;
                    }
                    buIdList.Add(buId);
                }
                if (buIdList.Count > 0)
                {
                    string[] entryBuIds = new string[buIdList.Count];
                    buIdList.CopyTo(entryBuIds);

                    WriteEntryBuSumInfoToFile(pdbId, entryBuIds, ref numOfBuTypes, buTypeWriter);
                }
            }
            buTypeWriter.WriteLine("#Monomers: " + numOfBuTypes[0].ToString());
            buTypeWriter.WriteLine("#Homodimers: " + numOfBuTypes[1].ToString());
            buTypeWriter.WriteLine("#Homooligomers: " + numOfBuTypes[2].ToString());
            buTypeWriter.WriteLine("#Heterodimers: " + numOfBuTypes[3].ToString());
            buTypeWriter.WriteLine("#Heterooligomers: " + numOfBuTypes[4].ToString());
            dataReader.Close();
            buTypeWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buIds"></param>
        private void WriteEntryBuSumInfoToFile(string pdbId, string[] entryBuIds, ref int[] numOfBuTypes, StreamWriter buInfoWriter)
        {
            bool isSameAsu = false;
            string buType = "";
            string dataLine = "";
            try
            {
                Dictionary<string, string> buAbcFormatHash = GetBuAbcFormatHash(pdbId, entryBuIds);
                Dictionary<string, string> buAbcTypeHash = GetBuAbcType(buAbcFormatHash);
                Dictionary<string, bool> buSameAsuHash = GetBuSameAsuHash(pdbId, entryBuIds);
                foreach (string entryBu in entryBuIds)
                {
                    buType = (string)buAbcTypeHash[entryBu];
                    dataLine = pdbId + "\t" + entryBu + "\t" +
                        (string)buAbcFormatHash[entryBu] + "\t" +
                        buType + "\t";
                    isSameAsu = buSameAsuHash[entryBu];
                    if (isSameAsu)
                    {
                        dataLine += "T";
                    }
                    else
                    {
                        dataLine += "F";
                    }
                    buInfoWriter.WriteLine(dataLine);

                    switch (buType)
                    {
                        case "Monomer":
                            numOfBuTypes[0]++;
                            break;

                        case "Homodimer":
                            numOfBuTypes[1]++;
                            break;

                        case "Homooligomer":
                            numOfBuTypes[2]++;
                            break;

                        case "Heterodimer":
                            numOfBuTypes[3]++;
                            break;

                        case "Heterooligomer":
                            numOfBuTypes[4]++;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " Retreiving Bu Info error: " + ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buIds"></param>
        /// <returns></returns>
        private Dictionary<string,string> GetBuAbcType (Dictionary<string,string> entryBuAbcHash)
        {
            Dictionary<string, string> buAbcTypeHash = new Dictionary<string,string> ();
            foreach (string buId in entryBuAbcHash.Keys)
            {
                string buAbcFormat = entryBuAbcHash[buId];
                string buType = GetBiolAssemblyType(buAbcFormat);
                buAbcTypeHash.Add(buId, buType);
            }
            return buAbcTypeHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buIds"></param>
        /// <returns></returns>
        private Dictionary<string, bool> GetBuSameAsuHash(string pdbId, string[] buIds)
        {
            Dictionary<string, bool> entryBuSameAsuHash = new Dictionary<string, bool>();
            foreach (string buId in buIds)
            {
                bool sameAsu = IsBuSameAsASU(pdbId, buId);
                entryBuSameAsuHash.Add(buId, sameAsu);
            }
            return entryBuSameAsuHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buIds"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetBuAbcFormatHash(string pdbId, string[] buIds)
        {
            Dictionary<string, string> entryBuAbcHash = buQuery.GetPdbBuAbcFormatHash(pdbId);
            Dictionary<string, string> buAbcFormatHash = new Dictionary<string,string> ();
            foreach (string buId in buIds)
            {
                string buAbcFormat = (string)entryBuAbcHash[buId];
                buAbcFormatHash.Add(buId, buAbcFormat);
            }
            return buAbcFormatHash;
        }
        #endregion
        #endregion

        #region first biological assembly and asu
        public void CompareFirstBAASU()
        {
            StreamWriter dataWriter = new StreamWriter("PdbFirstBAsAsuComp_xray.txt", true);
            string queryString = "Select PdbID, BuID, AbcFormat From PdbBiolUnits Where BuID = '1';";
            DataTable firstBuTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            queryString = "Select PdbID, AsymID, EntityID, AuthorChain From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable asuTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = firstBuTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = firstBuTable.Rows.Count;

            string pdbId = "";
            string buId = "";
            int totalFirstBAs = firstBuTable.Rows.Count;
            int numOfSame = 0;
            int numOfBig = 0;
            int numOfSmall= 0;
            int numOfDif = 0;
            string buAbcFormat = "";
            string asuAbcFormat = "";
            string dataLine = "";
            int buAsuComp = 0;
            foreach (DataRow buRow in firstBuTable.Rows)
            {
                pdbId = buRow["PdbID"].ToString();
                buId = buRow["BuID"].ToString().TrimEnd();

                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                if (! IsEntryXray(pdbId))
                {
                    continue;
                }

                try
                {
                    DataTable entryAsuTable = GetEntryAsuTable(pdbId, asuTable);

                    Dictionary<string, int> buEntityCountHash = GetPdbBuEntityContentHash(pdbId, buId, entryAsuTable);
                    buAbcFormat = buQuery.GetAbcFormatFromEntityHash(buEntityCountHash);

                    Dictionary<string, Dictionary<string, int>> buAsuEntityCountHash = buQuery.GetAsuEntityContentHash(entryAsuTable);
                    Dictionary<string, int> asuEntityCountHash = buAsuEntityCountHash["0"];
                    asuAbcFormat = buQuery.GetAbcFormatFromEntityHash(asuEntityCountHash);

                    if (buAbcFormat == asuAbcFormat)
                    {
                        dataLine = pdbId + "\t" + buAbcFormat + "\t" + asuAbcFormat + "\tsame";
                        numOfSame++;
                    }
                    else
                    {
                        buAsuComp = CompareTwoComplexes(buEntityCountHash, asuEntityCountHash);
                        if (buAsuComp == 1)
                        {
                            dataLine = pdbId + "\t" + buAbcFormat + "\t" + asuAbcFormat + "\tbig";
                            numOfBig++;
                        }
                        else if (buAsuComp == -1)
                        {
                            dataLine = pdbId + "\t" + buAbcFormat + "\t" + asuAbcFormat + "\tsmall";
                            numOfSmall++;
                        }
                        else
                        {
                            dataLine = pdbId + "\t" + buAbcFormat + "\t" + asuAbcFormat + "\tdif";
                            numOfDif++;
                        }
                    }
                    dataWriter.WriteLine(dataLine);
                    dataWriter.Flush();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(pdbId + " error: " + ex.Message);
                }
            }
            dataWriter.WriteLine("Total # of entries: " + totalFirstBAs.ToString ());
            dataWriter.WriteLine("# of same: " + numOfSame.ToString ());
            dataWriter.WriteLine("# of big: " + numOfBig.ToString ());
            dataWriter.WriteLine("# of small: " + numOfSmall.ToString ());
            dataWriter.WriteLine("# of dif: " + numOfDif.ToString ());
            dataWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        private bool IsEntryXray (string pdbId)
        {
            string queryString = string.Format("Select Method From PdbEntry Where PdbID = '{0}';", pdbId);
            DataTable methodTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (methodTable.Rows.Count > 0)
            {
                string method = methodTable.Rows[0]["Method"].ToString().TrimEnd();
                if (method == "X-RAY DIFFRACTION")
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asuTable"></param>
        /// <returns></returns>
        private DataTable GetEntryAsuTable(string pdbId, DataTable asuTable)
        {
            DataTable entryAsuTable = asuTable.Clone();
            DataRow[] asuRows = asuTable.Select(string.Format ("PdbID = '{0}'", pdbId));
            foreach (DataRow asuRow in asuRows)
            {
                DataRow dataRow = entryAsuTable.NewRow();
                dataRow.ItemArray = asuRow.ItemArray;
                entryAsuTable.Rows.Add(dataRow );
            }
            return entryAsuTable;
        }
         /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, int> GetPdbBuEntityContentHash(string pdbId, string buId, DataTable entryAsuTable)
        {
            string queryString = string.Format("SELECT PdbID, AsymID, NumOfAsymIDs" +
                            " FROM BiolUnit WHERE PdbID = '{0}' AND BiolUnitID = '{1}';", pdbId, buId);
            DataTable buTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            Dictionary<string, int> entityContentHash = new Dictionary<string, int>();
            string entityId = "";
            string asymChain = "";
            int numOfCopies = -1;
            foreach (DataRow dataRow in buTable.Rows)
            {
                asymChain = dataRow["AsymID"].ToString().TrimEnd();
                DataRow[] entityRows = entryAsuTable.Select (string.Format ("AsymID = '{0}'", asymChain));
                if (entityRows.Length == 0)
                {
                    continue;
                }
                entityId = entityRows[0]["EntityID"].ToString ();
                numOfCopies = Convert.ToInt32(dataRow["NumOfAsymIDs"].ToString()); 
                if (entityContentHash.ContainsKey(entityId))
                {
                    int count = (int)entityContentHash[entityId];
                    count += numOfCopies;
                    entityContentHash[entityId] = count;
                }
                else
                {
                    entityContentHash.Add(entityId, numOfCopies);
                }
            }
            return entityContentHash;
        }


        /// <summary>
        /// 1: abcFormat1 > abcFormat2
        /// 0: identical
        /// -1: abcFormat1 less than abcFormat2
        /// 2: dif structure, like A2 and AB
        /// </summary>
        /// <param name="abcFormat1"></param>
        /// <param name="abcFormat2"></param>
        /// <returns></returns>
        private int CompareTwoComplexes(Dictionary<string, int> entityCountHash1, Dictionary<string, int> entityCountHash2)
        {
            List<string> entityIdList = new List<string> (entityCountHash1.Keys);
            foreach (string entityId in entityCountHash2.Keys)
            {
                if (!entityIdList.Contains(entityId))
                {
                    entityIdList.Add(entityId);
                }
            }
            int entityCount1 = 0;
            int entityCount2 = 0;
            entityIdList.Sort();
            List<int> compTypeList = new List<int>();
            int compType = 0;
            foreach (string entityId in entityIdList)
            {
                entityCount1 = -1;
                entityCount2 = -1;
                if (entityCountHash1.ContainsKey(entityId))
                {
                    entityCount1 = (int)entityCountHash1[entityId];
                }
                if (entityCountHash2.ContainsKey(entityId))
                {
                    entityCount2 = (int)entityCountHash2[entityId];
                }
                if (entityCount1 > 0 && entityCount2 > 0)
                {
                    if (entityCount1 < entityCount2)
                    {
                        compType = -1;
                    }
                    else if (entityCount1 == entityCount2)
                    {
                        compType = 0;
                    }
                    else
                    {
                        compType = 1;
                    }
                }
                else if (entityCount1 == -1 && entityCount2 > 0)
                {
                    compType = -1;
                }
                else if (entityCount1 > 0 && entityCount2 == -1)
                {
                    compType = 1;
                }
                if (! compTypeList.Contains(compType))
                {
                    compTypeList.Add(compType);
                }
            }
            compTypeList.Sort ();
            int[] compTypes = new int[compTypeList.Count];
            compTypeList.CopyTo(compTypes);
            if (compTypes.Length == 1)
            {
                compType = (int)compTypes[0];
            }
            else if (compTypes.Length == 2)
            {
                if (compTypes[0] == 0 && compTypes[1] == 1)
                {
                    compType = 1;
                }
                else if (compTypes[0] == -1 && compTypes[1] == 0)
                {
                    compType = -1;
                }
                else
                {
                    compType = 2;
                }
            }
            else
            {
                compType = 2;
            }
            return compType;
        }
        #endregion
    }
}
