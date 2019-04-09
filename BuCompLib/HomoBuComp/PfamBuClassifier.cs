using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Data;
using DbLib;
using ProtCidSettingsLib;

namespace BuCompLib.HomoBuComp
{
    public class PfamBuClassifier
    {
        #region member variables
        private DataTable groupTable = null;
        private DataTable buGroupTable = null;
        private DbQuery dbQuery = new DbQuery();
        private DbInsert dbInsert = new DbInsert();
        private StreamWriter logWriter = new StreamWriter("PfamBuClassifierLog.txt");
        private string chainLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        #endregion

        public void GetBuDomainContents()
        {
            InitializeTable();
            InitializeDbTable();
           
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "BU Domain Contents";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving domain contents in BUs.");

            string[] buEntries = GetBuEntries();
            ProtCidSettings.progressInfo.totalOperationNum = buEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = buEntries.Length;

            Dictionary<string, int> familyStringGroupIdHash = GetFamilyStringGroupHash();

            foreach (string buEntry in buEntries)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = buEntry;

                GetBuDomainContents(buEntry, ref familyStringGroupIdHash);
            }
            logWriter.Close();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        public void UpdateBuDomainContents(string[] updateEntries)
        {
            InitializeTable();

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "BU Domain Contents";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Retrieving domain contents in BUs.");

            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;

            Dictionary<string, int> familyStringGroupIdHash = GetFamilyStringGroupHash();

            foreach (string buEntry in updateEntries)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = buEntry;

                DeleteObsData(buEntry);

                GetBuDomainContents(buEntry, ref familyStringGroupIdHash);
            }
            logWriter.Close();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        public void GetBuDomainContents(string pdbId, ref Dictionary<string, int> familyStringGroupIdHash)
        {
            Dictionary<string, Dictionary<int, List<string>>> buProtChainsHash = GetProtChainComponentForEntry(pdbId);

            if (buProtChainsHash.Count == 0)
            {
                return;
            }
            int groupSeqId = -1;
            string buFamilyString = "";
            string protFamilyString = "";
            foreach (string buId in buProtChainsHash.Keys)
            {
                Dictionary<int, List<string>> buEntityChainsHash = buProtChainsHash[buId];
                buFamilyString = "";
                List<int> entityList = new List<int> (buEntityChainsHash.Keys);
                entityList.Sort ();
                foreach (int entityId in entityList)
                {
                    List<string> chainCountList = buEntityChainsHash[entityId];
                    string[] chainCountFields = chainCountList[0].ToString ().Split ('_');
                    protFamilyString = GetPfamFamiliesInChain(pdbId, chainCountFields[0]);
                    if (protFamilyString == "")
                    {
                        continue;
                    }
                    buFamilyString += protFamilyString + ";";
                }
                buFamilyString = buFamilyString.TrimEnd(';');
                groupSeqId = GetGroupSeqID(buFamilyString, ref familyStringGroupIdHash);
                AssignDataIntoTable(groupSeqId, pdbId, buId, buEntityChainsHash);
            }
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.buCompConnection, groupTable);
            dbInsert.InsertDataIntoDBtables(ProtCidSettings.buCompConnection, buGroupTable);
            groupTable.Clear();
            buGroupTable.Clear();
        }
        /// <summary>
        /// the entries have BU definition
        /// </summary>
        /// <returns></returns>
        private string[] GetBuEntries()
        {
            string queryString = "";
            if (BuCompBuilder.BuType == "pdb")
            {
                queryString = "Select Distinct PdbID From BiolUnit;";
            }
            else if (BuCompBuilder.BuType == "pisa")
            {
                queryString = "Select Distinct PdbID From PisaBuStatus Where Status = 'Ok';";
            }
            DataTable buEntryTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string[] buEntries = new string[buEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow buEntryRow in buEntryTable.Rows)
            {
                buEntries[count] = buEntryRow["PdbID"].ToString();
                count++;
            }
            return buEntries;
        }

        private Dictionary<string, Dictionary<int, List<string>>> GetProtChainComponentForEntry(string pdbId)
        {
            if (BuCompBuilder.BuType == "pdb")
            {
                Dictionary<string, Dictionary<int, List<string>>> pdbBuProtChainCountHash = GetProtChainComponentForPdbEntry(pdbId);
                return pdbBuProtChainCountHash;
            }
            else if (BuCompBuilder.BuType == "pisa")
            {
                Dictionary<string, Dictionary<int, List<string>>> pisaBuProtChainCountHash = GetProtChainComponentForPisaEntry(pdbId);
                return pisaBuProtChainCountHash;
            }
            return null;
        }
        /// <summary>
        /// BUs and its protein chains for the entry
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<int, List<string>>> GetProtChainComponentForPdbEntry(string pdbId)
        {
            string queryString = string.Format("Select BiolUnitID, BiolUnit.AsymID, NumOfAsymIDs, EntityID " + 
                " From BiolUnit, AsymUnit " + 
                " Where BiolUnit.PdbID = '{0}' AND AsymUnit.PdbID = '{0}' " + 
                " AND BiolUnit.PdbID = AsymUnit.PdbID " + 
                " AND BiolUnit.AsymID = AsymUnit.AsymID AND PolymerType = 'polypeptide'" + 
                " Order By BiolUnitID, BiolUnit.AsymID;", pdbId);
            DataTable buProtChainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            Dictionary<string, Dictionary<int, List<string>>> buProtChainHash = new Dictionary<string, Dictionary<int, List<string>>> ();
            string buId = "";
            string asymChainCount = "";
            int entityId = -1;
            foreach (DataRow buProtChainRow in buProtChainTable.Rows)
            {
                buId = buProtChainRow["BiolUnitID"].ToString ().TrimEnd ();
                asymChainCount = buProtChainRow["AsymID"].ToString().TrimEnd() + "_" + 
                    buProtChainRow["NumOfAsymIDs"].ToString ();
                entityId = Convert.ToInt32(buProtChainRow["EntityID"].ToString ());
                if (buProtChainHash.ContainsKey (buId))
                {
                    if (buProtChainHash[buId].ContainsKey(entityId))
                    {
                        buProtChainHash[buId][entityId].Add(asymChainCount);
                    }
                    else
                    {
                        List<string> asymChainCountList = new List<string> ();
                        asymChainCountList.Add(asymChainCount);
                        buProtChainHash[buId].Add(entityId, asymChainCountList);
                    }
                }
                else
                {
                    Dictionary<int, List<string>> protChainHash = new Dictionary<int,List<string>> ();
                    List<string> asymChainCountList = new List<string> ();
                    asymChainCountList.Add(asymChainCount);
                    protChainHash.Add(entityId, asymChainCountList);
                    buProtChainHash.Add(buId, protChainHash);
                }
            }
            return buProtChainHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<int, List<string>>> GetProtChainComponentForPisaEntry(string pdbId)
        {
            string queryString = string.Format("Select AssemblySeqId, PisaBuMatrix.AsymChain, EntityID " + 
                " From PisaBuMatrix, AsymUnit " + 
                " Where PisaBuMatrix.PdbID = '{0}' AND AsymUnit.PdbID = '{0}' " + 
                " AND PisaBuMatrix.PdbID = AsymUnit.PdbID AND " + 
                " PisaBuMatrix.AsymChain = AsymUnit.AsymID AND " + 
                " PolymerType = 'polypeptide' ORDER BY AssemblySeqId, AsymChain;", pdbId);
            DataTable pisaBuTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            Dictionary<string, Dictionary<int, List<string>>> buProtChainHash = new Dictionary<string, Dictionary<int, List<string>>>();
            string buId = "";
            string asymChain = "";
            int entityId = -1;
            foreach (DataRow buProtChainRow in pisaBuTable.Rows)
            {
                buId = buProtChainRow["AssemblySeqId"].ToString().TrimEnd();
                asymChain = buProtChainRow["AsymChain"].ToString().TrimEnd();
                entityId = Convert.ToInt32(buProtChainRow["EntityID"].ToString());
                if (buProtChainHash.ContainsKey (buId))
                {
                    if (buProtChainHash[buId].ContainsKey (entityId))
                    {
                        buProtChainHash[buId][entityId].Add(asymChain);
                    }
                    else
                    {
                        List<string> asymChainList = new List<string> ();
                        asymChainList.Add(asymChain);
                        buProtChainHash[buId].Add(entityId, asymChainList);
                    }
                }
                else
                {
                    Dictionary<int, List<string>> protChainHash = new Dictionary<int,List<string>> ();
                    List<string> asymChainList = new List<string> ();
                    asymChainList.Add(asymChain);
                    protChainHash.Add(entityId, asymChainList);
                    buProtChainHash.Add(buId, protChainHash);
                }
            }
            Dictionary<string, Dictionary<int, List<string>>> buProtChainCountHash = new Dictionary<string,Dictionary<int,List<string>>> ();
            foreach (string keyBuId in buProtChainHash.Keys)
            {
                Dictionary<int, List<string>> protChainCountHash = new Dictionary<int,List<string>> ();
                foreach (int keyEntityId in buProtChainHash[keyBuId].Keys)
                {
                    List<string> asymChainList = buProtChainHash[keyBuId][keyEntityId];
                    List<string> asymChainCountList = ChangeTheChainListToChainCountList(asymChainList);
                    protChainCountHash.Add(keyEntityId, asymChainCountList);
                }
                buProtChainCountHash.Add(keyBuId, protChainCountHash);
            }
            return buProtChainCountHash;
        }

        /// <summary>
        /// change the chain list into chain_count list
        /// in order to fit the format of the PDB 
        /// </summary>
        /// <param name="asymChainList"></param>
        /// <returns></returns>
        private List<string> ChangeTheChainListToChainCountList (List<string> asymChainList)
        {
            Dictionary<string, int> asymChainCountHash = new Dictionary<string,int> ();
            foreach (string asymChain in asymChainList)
            {
                if (asymChainCountHash.ContainsKey (asymChain))
                {
                    int count = (int)asymChainCountHash[asymChain];
                    count++;
                    asymChainCountHash[asymChain] = count;
                }
                else
                {
                    asymChainCountHash.Add(asymChain, 1);
                }
            }
            List<string> asymChainCountList = new List<string> ();
            string asymChainCount = "";
            foreach (string asymChain in asymChainCountHash.Keys)
            {
                asymChainCount = asymChain + "_" + asymChainCountHash[asymChain].ToString ();
                asymChainCountList.Add(asymChainCount);
            }
            asymChainCountList.Sort();
            return asymChainCountList;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private string GetPfamFamiliesInChain(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select Distinct * From PdbPfam " + 
                " Where PdbID = '{0}' AND AsymChain = '{1}' ORDER By SeqStart;", pdbId, asymChain);
            DataTable pfamDomainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string pfamFamilyString = "";
            foreach (DataRow pfamDomainRow in pfamDomainTable.Rows)
            {
                pfamFamilyString += "(" + pfamDomainRow["Pfam_ID"].ToString().TrimEnd() + ")" + "_";
            }
            return pfamFamilyString.TrimEnd('_');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buFamilyString"></param>
        /// <param name="familyStringGroupIdHash"></param>
        /// <returns></returns>
        private int GetGroupSeqID(string buFamilyString, ref Dictionary<string, int> familyStringGroupIdHash)
        {
            int groupSeqId = -1;
            if (familyStringGroupIdHash.ContainsKey (buFamilyString))
            {
                groupSeqId = familyStringGroupIdHash[buFamilyString];
            }
            else
            {
                groupSeqId = familyStringGroupIdHash.Count + 1;
                familyStringGroupIdHash.Add(buFamilyString, groupSeqId);
                DataRow groupIdRow = groupTable.NewRow();
                groupIdRow["GroupSeqID"] = groupSeqId;
                groupIdRow["FamilyString"] = buFamilyString;
                groupTable.Rows.Add(groupIdRow);
            }
            return groupSeqId;
        }

        private void AssignDataIntoTable(int groupSeqId, string pdbId, string buId, Dictionary<int, List<string>> buEntityChainsHash)
        {
            DataRow dataRow = buGroupTable.NewRow();
            dataRow["GroupSeqID"] = groupSeqId;
            dataRow["PdbID"] = pdbId;
            dataRow["BuID"] = buId;
            dataRow["EntityFormat"] = GetEntityFormat(buEntityChainsHash);
            dataRow["AsymFormat"] = GetAsymFormat(buEntityChainsHash);
            dataRow["AbcFormat"] = GetAbcFormat(dataRow["EntityFormat"].ToString ());
            buGroupTable.Rows.Add(dataRow);
        }

        #region BU format
        /// <summary>
        /// 
        /// </summary>
        /// <param name="buEntityChainsHash"></param>
        /// <returns></returns>
        private string GetEntityFormat(Dictionary<int, List<string>> buEntityChainsHash)
        {
            List<int> entityIdList = new List<int> (buEntityChainsHash.Keys);
            entityIdList.Sort();
            string entityFormat = "";
            foreach (int entityId in entityIdList)
            {
                string[] chainCountStrings = buEntityChainsHash[entityId].ToArray ();
                int numOfChains = GetNumOfChains(chainCountStrings);
                entityFormat += "(" + entityId.ToString () + "." + numOfChains.ToString () + ")";
            }
            return entityFormat;
        }
        /// <summary>
        /// the number of chains for this entity
        /// </summary>
        /// <param name="chainCountStrings"></param>
        /// <returns></returns>
        private int GetNumOfChains(string[] chainCountStrings)
        {
            int numOfChains = 0;
            foreach (string chainCount in chainCountStrings)
            {
                string[] chainCountFields = chainCount.Split('_');
                numOfChains += Convert.ToInt32(chainCountFields[1]);
            }
            return numOfChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buEntityChainsHash"></param>
        /// <returns></returns>
        private string GetAsymFormat(Dictionary<int, List<string>> buEntityChainsHash)
        {
            List<int> entityIdList = new List<int> (buEntityChainsHash.Keys);
            entityIdList.Sort();
            string asymFormat = "";
            foreach (int entityId in entityIdList)
            {
                List<string> chainCountList = buEntityChainsHash[entityId];
                asymFormat += "(";
                foreach (string chainCount in chainCountList)
                {
                    string[] chainCountFields = chainCount.Split('_');
                    if (chainCountFields[1] == "1")
                    {
                        asymFormat += (chainCountFields[0]);
                    }
                    else
                    {
                        asymFormat += (chainCountFields[0] + chainCountFields[1]);
                    }
                    asymFormat += ",";
                }
                asymFormat = asymFormat.TrimEnd(',');
                asymFormat = asymFormat + ")";
            }
            return asymFormat;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buEntityChainsHash"></param>
        /// <returns></returns>
        private string GetAbcFormat(string entityFormat)
        {
            string[] fields = entityFormat.Split(')');
            string entityField = "";
            List<int> countList = new List<int> ();
            foreach (string field in fields)
            {
                if (field == "")
                {
                    continue;
                }
                entityField = field.TrimStart('(');
                string[] entityCountFields = entityField.Split('.');
                countList.Add(Convert.ToInt32 (entityCountFields[1]));
            }
            countList.Sort();
            int letterIndex = 0;
            string abcFormat = "";
            // assume the number of chains not greater than 62
            for(int i = countList.Count - 1; i >= 0; i--)
            {
                if ((int)countList[i] == 1)
                {
                    abcFormat += chainLetters[letterIndex].ToString();
                }
                else
                {
                    abcFormat += chainLetters[letterIndex].ToString() + countList[i].ToString();
                }
                letterIndex++;
            }
            return abcFormat;
        }
        #endregion 

        #region initialize

        private void InitializeTable()
        {
            string[] buGroupColumns = {"GroupSeqID", "PdbID", "BuID", "EntityFormat", "AsymFormat", "AbcFormat"};
            buGroupTable = new DataTable(BuCompBuilder.BuType + "Bu" + ProtCidSettings.dataType + "Groups");
            foreach (string groupCol in buGroupColumns)
            {
                buGroupTable.Columns.Add(new DataColumn (groupCol));
            }

            string[] groupColumns = {"GroupSeqID", "FamilyString"};
            groupTable = new DataTable(ProtCidSettings.dataType + "Groups");
            foreach (string groupCol in groupColumns)
            {
                groupTable.Columns.Add(new DataColumn (groupCol));
            }
        }

        private void InitializeDbTable()
        {
            DbCreator dbCreator = new DbCreator();
            if (!dbCreator.IsTableExist(ProtCidSettings.buCompConnection, buGroupTable.TableName))
            {
                string createTableString = "Create Table " + buGroupTable.TableName + " ( " +
                    " GroupSeqID INTEGER NOT NULL, " +
                    " PdbID CHAR(4) NOT NULL, " +
                    " BuID VARCHAR(8) NOT NULL, " + 
                " EntityFormat VARCHAR(255) NOT NULL, " + 
                " AsymFormat VARCHAR(255) NOT NULL, " + 
                " AbcFormat VARCHAR(255) NOT NULL );";
                dbCreator.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, buGroupTable.TableName);

                string createIndexString = "Create INDEX " + buGroupTable.TableName + "_Idx1 ON " +
                    buGroupTable.TableName + "(PdbID, BuID);";
                dbCreator.CreateIndex(ProtCidSettings.buCompConnection, createIndexString, buGroupTable.TableName);

                createIndexString = "Create INDEX " + buGroupTable.TableName + "_idx2 ON " +
                    buGroupTable.TableName + "(GroupSeqID);";
                dbCreator.CreateIndex(ProtCidSettings.buCompConnection, createIndexString, buGroupTable.TableName);
            }

            if (!dbCreator.IsTableExist(ProtCidSettings.buCompConnection, groupTable.TableName))
            {
                string createTableString = "Create Table " + groupTable.TableName + " ( " +
                    " GroupSeqID INTEGER NOT NULL, " +
                    " FamilyString BLOB Sub_Type Text NOT NULL);";
                dbCreator.CreateTableFromString(ProtCidSettings.buCompConnection, createTableString, groupTable.TableName);

      /*          string createIndexString = "Create INDEX " + groupTable.TableName + "_idx1 ON " +
                    groupTable.TableName + "(GroupSeqID);";
                dbCreator.CreateTable(ProtCidSettings.buCompConnection, createIndexString, groupTable.TableName);*/
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, int> GetFamilyStringGroupHash()
        {
            string queryString = string.Format ("Select * From {0};", groupTable.TableName);
            DataTable familyGroupTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            Dictionary<string, int> familyStringGroupIdHash = new Dictionary<string,int> ();
            foreach (DataRow familyGroupRow in familyGroupTable.Rows)
            {
                familyStringGroupIdHash.Add(familyGroupRow["FamilyString"].ToString ().TrimEnd (), 
                    Convert.ToInt32 (familyGroupRow["GroupSeqID"].ToString ()));
            }
            return familyStringGroupIdHash;
        }
        #endregion

        #region delete
        private void DeleteObsData(string pdbId)
        {
            string deleteString = string.Format("Delete From {0} Where PdbID = '{1}';",
                buGroupTable.TableName, pdbId);
            dbQuery.Query(ProtCidSettings.buCompConnection, deleteString);
        }
        #endregion

        #region missing pfam Bu entries -- for debug
        public void FindMissingPfamBuEntries()
        {
            string queryString = "Select Distinct PdbID From PdbBuPfamGroups Where GroupSeqID = 33;";
            DataTable entryTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
            StreamWriter dataWriter = new StreamWriter("MissingPfamPdbBuEntries.txt");
            int entityId = -1;
            string pdbId = "";
            string sequence = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
                queryString = string.Format("Select * From AsymUnit " + 
                    " Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
                DataTable sequenceTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                List<int> entityList = new List<int> ();
                foreach (DataRow dataRow in sequenceTable.Rows)
                {
                    entityId = Convert.ToInt32 (dataRow["EntityID"].ToString ());
                    if (! entityList.Contains(entityId))
                    {
                        entityList.Add(entityId);
                    }
                }
                foreach (int protEntityId in entityList)
                {
                    DataRow[] sequenceRows = sequenceTable.Select(string.Format ("EntityID = '{0}'", protEntityId));
                    sequence = sequenceRows[0]["Sequence"].ToString().TrimEnd();
                    if (sequence.Length <= 15)
                    {
                        continue;
                    }

                    if (IsSequenceAllXs(sequence))
                    {
                        continue;
                    }
                    dataWriter.WriteLine(">" + pdbId + "_" + protEntityId.ToString ());
                    dataWriter.WriteLine(sequence);
                }
            }
            dataWriter.Close();
        }
        private bool IsSequenceAllXs(string sequence)
        {
            foreach (char ch in sequence)
            {
                if (ch == 'X')
                {
                    continue;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }
        #endregion
    }
}
