using System;
using System.Collections.Generic;
using System.Data;
using DbLib;
using ProtCidSettingsLib;

namespace AuxFuncLib
{
	/// <summary>
	/// Used to retrieve info for the ASU
	/// </summary>
    public class AsuInfoFinder
	{
        private DbConnect dbConnect = null;
	    private static DbQuery dbQuery = new DbQuery ();

		public AsuInfoFinder ()
		{
          
		}


        public AsuInfoFinder(string dbName)
        {
            if (dbConnect == null)
            {
                dbConnect = new DbConnect ("DRIVER=Firebird/InterBase(r) driver; UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" + dbName);
            }
        }
		/// <summary>
		/// 
		/// </summary>
		/// <param name="entityTable"></param>
		/// <returns></returns>
		public int[] GetEntities (DataTable entityTable)
		{
			List<int> entityList = new List<int>  ();
            int entityId = 0;
			foreach (DataRow dRow in entityTable.Rows)
			{
                entityId = Convert.ToInt32(dRow["EntityID"].ToString ());
				if (entityList.Contains (entityId))
				{
					continue;
				}
			/*	if (dRow["Sequence"].ToString ().Length < minSeqLength)
				{
					continue;
				}*/
				entityList.Add (entityId);
			}
            return entityList.ToArray ();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="authChain"></param>
		/// <returns></returns>
		public DataTable GetEntityAsymChainInfo (string pdbId, string authChain)
		{
			if (authChain == "-")
			{
				authChain = "_";
			}
			string queryString = string.Format ("Select EntityId, AsymID From AsymUnit " + 
				" Where PdbId = '{0}' AND AuthorChain = '{1}' AND PolymerType = 'polypeptide';", 
				pdbId, authChain);
			DataTable entityInfoTable = dbQuery.Query (dbConnect, queryString);
            entityInfoTable.CaseSensitive = true;
			if (entityInfoTable.Rows.Count == 0)
			{
				queryString = string.Format ("Select EntityId, AsymID, Sequence From AsymUnit " + 
					" Where PdbId = '{0}' AND PolymerType = 'polypeptide';", pdbId);
				DataTable protInfoTable = dbQuery.Query (dbConnect, queryString);
				int[] entities = GetEntities (protInfoTable);
				if (entities.Length == 1)
				{
					entityInfoTable = protInfoTable;
				}
			}
			return entityInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <returns></returns>
        public static DataTable GetEntityAuthChainInfo(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select EntityId, AuthorChain From AsymUnit " +
                " Where PdbId = '{0}' AND AsymID = '{1}' AND PolymerType = 'polypeptide';",
                pdbId, asymChain);
            DataTable entityInfoTable = dbQuery.Query (ProtCidSettings.pdbfamDbConnection, queryString);
            return entityInfoTable;
        }

        #region asymmetric chain and entity info 
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public DataTable GetProtAsuInfoTable()
        {
            string queryString = "Select PdbID, EntityID, AsymID, AuthorChain, PolymerStatus, Name " +
                " From AsymUnit Where PolymerType = 'polypeptide';";
            DataTable asuInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            asuInfoTable.CaseSensitive = true;
            return asuInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbIds"></param>
        /// <returns></returns>
        public DataTable GetProtAsuInfoTable(string[] pdbIds)
        {
            DataTable entryAsuInfoTable = null;
            foreach (string pdbId in pdbIds)
            {
                DataTable thisEntryAsuInfoTable = GetProtAsuInfoTable(pdbId);
                if (entryAsuInfoTable == null)
                {
                    entryAsuInfoTable = thisEntryAsuInfoTable.Clone ();
                }
                foreach (DataRow dataRow in thisEntryAsuInfoTable.Rows)
                {
                    DataRow newRow = entryAsuInfoTable.NewRow ();
                    newRow.ItemArray = dataRow.ItemArray;
                    entryAsuInfoTable.Rows.Add (newRow);
                }
            }
            entryAsuInfoTable.CaseSensitive = true;
            return entryAsuInfoTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public DataTable GetProtAsuInfoTable(string pdbId)
        {
            string queryString = string.Format("Select PdbID, EntityID, AsymID, AuthorChain, PolymerStatus, Name " +
                    "From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable thisEntryAsuInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            thisEntryAsuInfoTable.CaseSensitive = true;
            return thisEntryAsuInfoTable;
        }
        /// <summary>
        /// the asymmetric chains and entity IDs for the input entry and its author chains
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChains"></param>
        /// <returns></returns>
        public string[] FindProtAsymChainEntity(string pdbId, string[] authChains)
        {
            string queryString = string.Format("Select * From AsymUnit Where PdbID = '{0}';", pdbId);
            DataTable entryChainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            entryChainTable.CaseSensitive = true;

            string[] asymEntityInfos = new string[authChains.Length];
            int count = 0;
            string asymChain = "";
            string entityId = "";
            foreach (string authChain in authChains)
            {
                asymChain = GetAsymChain(entryChainTable, authChain);
                entityId = GetEntityID(entryChainTable, asymChain);
                asymEntityInfos[count] = asymChain + "," + entityId;
                count++;
            }
            return asymEntityInfos;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInfoTable">the chain info for multiple entries</param>
        /// <param name="pdbId"></param>
        /// <param name="authChains"></param>
        /// <returns></returns>
        public string[] FindProtAsymChainEntity (DataTable chainInfoTable, string pdbId, string[] authChains)
        {
            chainInfoTable.CaseSensitive = true;
            DataRow[] entryChainRows = chainInfoTable.Select(string.Format("PdbID = '{0}'", pdbId));
            DataTable entryChainTable = chainInfoTable.Clone();
            foreach (DataRow entryChainRow in entryChainRows)
            {
                DataRow dataRow = entryChainTable.NewRow();
                dataRow.ItemArray = entryChainRow.ItemArray;
                entryChainTable.Rows.Add(dataRow);
            }
            string[] asymChainEntityInfos = new string[authChains.Length];
            int count = 0;
            string asymChain = "";
            string entityId = "";
            foreach (string authChain in authChains)
            {
                asymChain = GetAsymChain(entryChainTable, authChain);
                entityId = GetEntityID(entryChainTable, asymChain);
                asymChainEntityInfos[count] = asymChain + "," + entityId;
                count++;
            }
            return asymChainEntityInfos;
        }

        /// <summary>
        /// the entityID for the asymmetric chain
        /// </summary>
        /// <param name="entryChainTable"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private string GetEntityID(DataTable entryChainTable, string asymChain)
        {
            DataRow[] dataRows = entryChainTable.Select(string.Format ("AsymID = '{0}'", asymChain));
            if (dataRows.Length > 0)
            {
                return dataRows[0]["EntityID"].ToString();
            }
            return "-1";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        public int GetEntityIdForAsymChain(string pdbId, string asymChain)
        {
            string queryString = string.Format("Select EntityID From AsymUnit Where PdbID = '{0}' AND AsymID = '{1}';",
                pdbId, asymChain );
            DataTable entityIdTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            int entityId = -1;
            if (entityIdTable.Rows.Count > 0)
            {
                entityId = Convert.ToInt32(entityIdTable.Rows[0]["EntityID"].ToString ());
            }
            return entityId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <returns></returns>
        public int GetEntityIdForAuthorChain(string pdbId, string authChain)
        {
            int entityId = -1;
            if (authChain == "-")
            {
                authChain = "_";
            }
            string queryString = string.Format("Select EntityId From AsymUnit " +
                " Where PdbId = '{0}' AND AuthorChain = '{1}' AND PolymerType = 'polypeptide';",
                pdbId, authChain);
            DataTable entityInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            entityInfoTable.CaseSensitive = true;
            if (entityInfoTable.Rows.Count == 0)
            {
                queryString = string.Format("Select EntityId From AsymUnit " +
                    " Where PdbId = '{0}' AND PolymerType = 'polypeptide';", pdbId);
                DataTable protInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                int[] entities = GetEntities(protInfoTable);
                if (entities.Length == 1)
                {
                    entityInfoTable = protInfoTable;
                }
            }
            if (entityInfoTable.Rows.Count > 0)
            {
                entityId = Convert.ToInt32(entityInfoTable.Rows[0]["EntityID"].ToString ());
            }
            return entityId;
        }
        #endregion

        #region asymmetric chains for author chains
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainInfoTable">the chain info for multiple entries</param>
        /// <param name="pdbId"></param>
        /// <param name="authChains"></param>
        /// <returns></returns>
        public string[] FindProteinAsymChain(DataTable chainInfoTable, string pdbId, string[] authChains)
        {
            chainInfoTable.CaseSensitive = true;
            DataRow[] entryChainRows = chainInfoTable.Select(string.Format ("PdbID = '{0}'", pdbId));
            DataTable entryChainTable = chainInfoTable.Clone();
            foreach (DataRow entryChainRow in entryChainRows)
            {
                DataRow dataRow = entryChainTable.NewRow();
                dataRow.ItemArray = entryChainRow.ItemArray;
                entryChainTable.Rows.Add(dataRow);
            }
            string[] asymChains = new string[authChains.Length];
            int count = 0;
            foreach (string authChain in authChains)
            {
                asymChains[count] = GetAsymChain (entryChainTable, authChain);
                count++;
            }
            return asymChains;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChains"></param>
        /// <returns></returns>
        public string[] FindProteinAsymChains(string pdbId, string[] authChains)
        {
            string queryString = string.Format("Select * From AsymUnit Where PdbID = '{0}';", pdbId);
            DataTable entryChainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            entryChainTable.CaseSensitive = true;
            string[] asymChains = new string[authChains.Length];
            int count = 0;
            foreach (string authChain in authChains)
            {
                asymChains[count] = GetAsymChain(entryChainTable, authChain);
                count++;
            }
            return asymChains;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <returns></returns>
        public string FindProteinAsymChain (string pdbId, string authChain)
        {
            string queryString = string.Format("Select * From AsymUnit Where PdbID = '{0}';", pdbId);
            DataTable entryChainTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            entryChainTable.CaseSensitive = true;
            string asymChain = GetAsymChain(entryChainTable, authChain);
            return asymChain;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryChainTable"></param>
        /// <param name="authChain"></param>
        /// <returns></returns>
        public string GetAsymChain(DataTable entryChainTable, string authChain)
        {
            entryChainTable.CaseSensitive = true;
            DataRow[] asymRows = null;
            string asymChain = "-";
            if (authChain == "-")
            {
                authChain = "_";
            }
            asymRows = entryChainTable.Select
                //		(string.Format ("AuthorChain = '{0}' AND PolymerType = 'polypeptide'", authChain));
                (string.Format("AuthorChain = '{0}' AND PolymerStatus = 'polymer'", authChain));
            if (asymRows.Length == 0)
            {
                if (HasOnePolymerNonSugarEntity(entryChainTable) && authChain == "A")
                {
                    asymRows = entryChainTable.Select("PolymerStatus = 'polymer'");
                }
            }

            if (asymRows.Length > 0)
            {
                asymChain = asymRows[0]["AsymID"].ToString().Trim();
            }
            else
            {
                // if only one non-water chain named this author chain id
                asymChain = GetNonWaterOneChainAsymChain(entryChainTable, authChain);
            }

            return asymChain;
        }

        /// <summary>
        /// this entry has only one polymer chain
        /// </summary>
        /// <param name="entryChainTable"></param>
        /// <returns></returns>
        private bool HasOnePolymerNonSugarEntity(DataTable entryChainTable)
        {
            List<int> polymerEntityList = new List<int> ();
            int entityId = -1;
            string polymerStatus = "";
            string name = "";
            foreach (DataRow dRow in entryChainTable.Rows)
            {
                entityId = Convert.ToInt32 (dRow["EntityID"].ToString());
                polymerStatus = dRow["PolymerStatus"].ToString().TrimEnd();
                name = dRow["Name"].ToString().TrimEnd();
                if (name.ToLower().IndexOf("sugar") > -1)
                {
                    continue;
                }
                if (polymerStatus == "polymer")
                {
                    if (!polymerEntityList.Contains(entityId))
                    {
                        polymerEntityList.Add(entityId);
                    }
                }
            }
            if (polymerEntityList.Count == 1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryChainTable"></param>
        /// <param name="authChain"></param>
        /// <returns></returns>
        private string GetNonWaterOneChainAsymChain(DataTable entryChainTable, string authChain)
        {
            string asymChain = "-";
            List<DataRow> chainRowList = new List<DataRow> ();
            string thisAuthChain = "";
            string name = "";
            foreach (DataRow chainRow in entryChainTable.Rows)
            {
                thisAuthChain = chainRow["AuthorChain"].ToString().TrimEnd();
                name = chainRow["Name"].ToString().TrimEnd();
                if (name.ToLower() == "water")
                {
                    continue;
                }
                if (thisAuthChain == authChain)
                {
                    chainRowList.Add(chainRow);
                }
            }
            if (chainRowList.Count == 1)
            {
                asymChain = ((DataRow)chainRowList[0])["AsymID"].ToString().TrimEnd();
            }
            return asymChain;
        }
        #endregion

        #region asymmetric chain for ligands
       /// <summary>
       /// The asymmetric chain for input ligand
       /// </summary>
       /// <param name="entryChainTable"></param>
       /// <param name="authChain"></param>
       /// <param name="ligandName">ligand name</param>
       /// <param name="seqNum">Pdb Seq ID for the ligand </param>
       /// <returns></returns>
        public string GetLigandAsymChain(DataTable entryChainTable, string authChain, string ligandName, string seqNum)
        {
            string asymChain = "-";
            if (authChain == "-")
            {
                authChain = "_";
            }
            string origAuthChain = authChain;
            string selectString = string.Format("AuthorChain = '{0}' AND Sequence = '{1}' AND pdbSeqNumbers = '{2}'",
                authChain, ligandName, seqNum);
            DataRow[] chainRows = entryChainTable.Select(selectString);
            // if the author chain is messed up for those blank chains
            if (chainRows.Length == 0)
            {
                if (authChain == "_")
                {
                    authChain = "A";
                    selectString = string.Format("AuthorChain = '{0}' AND Sequence = '{1}' AND pdbSeqNumbers = '{2}'",
                    authChain, ligandName, seqNum);
                    chainRows = entryChainTable.Select(selectString);
                }
            }

            if (chainRows.Length == 0) // if the sequence numbers messed up
            {
                selectString = string.Format("AuthorChain = '{0}' AND Sequence = '{1}'", origAuthChain, ligandName);
                chainRows = entryChainTable.Select(selectString);
            }
            if (chainRows.Length > 0)
            {
                asymChain = chainRows[0]["AsymID"].ToString().TrimEnd();
                return asymChain;
            }
            else
            {
                asymChain = GetAsymChainFromSequenceParsing(entryChainTable, origAuthChain, ligandName, seqNum);
            }
            return asymChain;
        }

        /// <summary>
        /// Asymmetric chain for the input ligand by parsing the sequence in database
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="authChain"></param>
        /// <param name="ligandName"></param>
        /// <param name="seqNum"></param>
        /// <returns></returns>
        public string GetAsymChainFromSequenceParsing(DataTable entryChainTable, string authChain, string ligandName, string seqNum)
        {
            if (authChain == "-")
            {
                authChain = "_";
            }
            string selectString = string.Format("AuthorChain = '{0}'", authChain);
            DataRow[] chainRows = entryChainTable.Select(selectString);
            if (chainRows.Length == 0)
            {
                if (authChain == "_")
                {
                    authChain = "A";
                    selectString = string.Format("AuthorChain = '{0}'", authChain);
                    chainRows = entryChainTable.Select(selectString);
                }
            }
            string asymChain = "-";
            string sequence = "";
            string authSeqNums = "";
            foreach (DataRow chainInfoRow in chainRows)
            {
                sequence = chainInfoRow["Sequence"].ToString().TrimEnd();
                authSeqNums = chainInfoRow["pdbSeqNumbers"].ToString().TrimEnd();
                string[] seqNums = authSeqNums.Split(',');
                if (Array.IndexOf(seqNums, seqNum) > -1 && sequence.IndexOf(ligandName) > -1)
                {
                    asymChain = chainInfoRow["AsymID"].ToString().TrimEnd();
                    break;
                }
            }
            if (asymChain == "-")
            {
                foreach (DataRow chainInfoRow in chainRows)
                {
                    sequence = chainInfoRow["Sequence"].ToString().TrimEnd();
                    authSeqNums = chainInfoRow["pdbSeqNumbers"].ToString().TrimEnd();
                    string[] seqNums = authSeqNums.Split(',');
                    if (Array.IndexOf(seqNums, seqNum) > -1 && sequence.IndexOf("X") > -1)
                    {
                        asymChain = chainInfoRow["AsymID"].ToString().TrimEnd();
                        break;
                    }
                }
            }
            return asymChain;
        }
        #endregion

        #region sequence info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public int GetEntitySequenceLength(string pdbId, int entityId)
        {
            string sequence = GetEntitySequence(pdbId, entityId);
            return sequence.Length;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public string GetEntitySequence(string pdbId, int entityId)
        {
            string queryString = string.Format("Select First 1 Sequence From AsymUnit Where PdbID = '{0}' AND EntityID = {1};",
                 pdbId, entityId);
            DataTable sequenceTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string sequence = "";
            if (sequenceTable.Rows.Count > 0)
            {
                sequence = sequenceTable.Rows[0]["Sequence"].ToString().TrimEnd();
            }
            return sequence;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="asuInfoTable"></param>
        /// <returns></returns>
        public string GetEntitySequence(string pdbId, int entityId, DataTable asuInfoTable)
        {
            DataRow[] sequenceRows = asuInfoTable.Select(string.Format ("PdbId = '{0}' AND EntityID = '{1}'", 
                pdbId, entityId));
            string sequence = "";
            if (sequenceRows.Length > 0)
            {
                sequence = sequenceRows[0]["Sequence"].ToString().TrimEnd();
            }
            return sequence;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <param name="asuInfoTable"></param>
        /// <returns></returns>
        public int GetEntitySequenceLength(string pdbId, int entityId, DataTable asuInfoTable)
        {
            string sequence = GetEntitySequence(pdbId, entityId, asuInfoTable);
            return sequence.Length;
        }
        #endregion

    }
}
