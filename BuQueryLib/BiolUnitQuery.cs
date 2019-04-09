using System;
using System.Data;
using System.Data.Odbc;
using System.Collections.Generic;
using DbLib;
using ProtCidSettingsLib;

namespace BuQueryLib
{
	/// <summary>
	/// Summary description for Query.
	/// </summary>
	public class BiolUnitQuery
    {
        #region member variables
        private string[] pdbColumnNames = {"PDBID",  "PdbBuID", "PisaBuID",
										"ASU_Entity", "ASU_AsymID", "ASU_Auth", "ASU_ABC",
										"PDBBU_Entity", "PDBBU_AsymID", "PDBBU_Auth", "PDBBU_ABC", 
										"PISABU_Entity", "PISABU_AsymID", "PISABU_Auth", "PISABU_ABC",
										"SameBUs", "DNA", "RNA", "Ligands", "Resolution", "Names"};
		private string queryString = "";
		public string chainLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
		private DbQuery dbQuery = new DbQuery ();
        public enum BuContentType
        {
            entity, asym, author, abc
        }

        public string[] BuTableColumns
        {
            get
            {
                return pdbColumnNames;
            }          
        }

        public BiolUnitQuery()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        // property for query string
        public string QueryString
        {
            get
            {
                return queryString;
            }
        }

        /// <summary>
        /// initialize the table schema
        /// </summary>
        /// <param name="queryResultTable"></param>
        internal void CreateTableStructure(ref DataTable queryResultTable)
        {
            if (queryResultTable == null)
            {
                queryResultTable = new DataTable();
            }
            // set the queryResultTable 
            foreach (string colName in pdbColumnNames)
            {
                DataColumn dataCol = new DataColumn();
                dataCol.ColumnName = colName;
                dataCol.DataType = System.Type.GetType("System.String");
                queryResultTable.Columns.Add(dataCol);
            }
        }
        #endregion
      
		#region public interfaces
		
		/// <summary>
		/// get biolunit result from pdb entry and chain
		/// </summary>
		/// <returns></returns>
		public string GetBiolUnitForPdbEntry(string pdbId, string chainId, string chainType, ref DataTable queryResultTable)
		{
			string errorMsg = "";
			// set the queryResultTable 
			CreateTableStructure (ref queryResultTable);

			queryString = "";
			// set the queryResult table for PDB return
			//DataTable pdbResultTable = new DataTable ("PDB Result");

			string queryStringForPdb = GetPdbEntryQueryStringForPdb(pdbId, chainId, chainType);
			errorMsg = GetPdbBiolUnitForPdb(queryStringForPdb, ref queryResultTable);
			if (errorMsg.Length > 0)
				return errorMsg;

			queryString = queryStringForPdb;
			queryString += "\r\n\r\n";

			AddPisaBUInfoToTable (ref queryResultTable);

			CompareBUs (ref queryResultTable);

			return errorMsg;
		}

		/// <summary>
		/// get biolunit result from pdb entry and chain
		/// </summary>
		/// <returns></returns>
		public string GetBiolUnitForPdbEntry(string pdbId, ref DataTable queryResultTable, bool needBuComp)
		{
			string errorMsg = "";
			// set the queryResultTable 
			CreateTableStructure (ref queryResultTable);

			queryString = "";
			// set the queryResult table for PDB return
			//DataTable pdbResultTable = new DataTable ("PDB Result");

			string queryStringForPdb = GetPdbEntryQueryStringForPdb(pdbId, "", "");
			errorMsg = GetPdbBiolUnitForPdb(queryStringForPdb, ref queryResultTable);
			if (errorMsg.Length > 0)
				return errorMsg;

			queryString = queryStringForPdb;
			queryString += "\r\n\r\n";

			AddPisaBUInfoToTable (ref queryResultTable);

			if (needBuComp)
			{
				CompareBUs (ref queryResultTable);
			}

			return errorMsg;
		}

		/// <summary>
		/// retrieve ASU/BU for the list of PDB entries
		/// </summary>
		/// <param name="pdbIdList"></param>
		/// <param name="queryResultTable"></param>
		/// <param name="needBuComp">need bu comparison data or not</param>
		/// <returns></returns>
		public string GetBiolUnitForPdbEntries(List<string> pdbIdList, ref DataTable queryResultTable, bool needBuComp)
		{
			string errorMsg = "";
			// set the queryResultTable 
			CreateTableStructure (ref queryResultTable);

			queryString = "";
			// set the queryResult table for PDB return
			// DataTable pdbResultTable = new DataTable ("PDB Result");
			string queryStringForPdb = GetPdbEntryQueryStringForPdbList(pdbIdList);
			errorMsg = GetPdbBiolUnitForPdb(queryStringForPdb, ref queryResultTable);
			if (errorMsg.Length > 0)
				return errorMsg;

			queryString = queryStringForPdb;
			queryString += "\r\n\r\n";

			AddPisaBUInfoToTable (ref queryResultTable);

			if (needBuComp)
			{
				CompareBUs (ref queryResultTable);
			}

			return errorMsg;
		}
		#endregion

		#region Compare PDB/PISA BUs
		/// <summary>
		/// 
		/// </summary>
		/// <param name="queryResultTable"></param>
		internal void CompareBUs (ref DataTable queryResultTable)
		{
			foreach (DataRow dRow in queryResultTable.Rows)
			{
				try
				{
					CompareBUs (dRow);
				}
				catch (Exception ex)
				{
					string errorMsg = ex.Message;
				}
			}
			queryResultTable.AcceptChanges ();
		}


		/// <summary>
		/// comapre BUs in a row of the result table
		/// </summary>
		/// <param name="dataRow"></param>
		private void CompareBUs (DataRow dataRow)
		{
			CompareTwoBUs (ref dataRow, "pdb", "pisa");
		}
		
		/// <summary>
		/// compare PDB and PISA biological units
		/// </summary>
		/// <param name="dataRow"></param>
		/// <param name="type1"></param>
		/// <param name="type2"></param>
		internal void CompareTwoBUs (ref DataRow dataRow, string type1, string type2)
		{
            if (dataRow[type1 + "BU_Entity"].ToString() == "-" ||
                dataRow[type2 + "BU_Entity"].ToString() == "-")
            {
                dataRow["SameBUs"] = "-";
                return;
            }
            // check if it is XPack
            if (dataRow[type1 + "BU_Entity"].ToString() == dataRow[type2 + "BU_Entity"].ToString())
            {
                if (dataRow[type1 + "BU_ABC"].ToString() == "A")
                {
                    dataRow["SameBUs"] = "same";
                }
                else
                {
                    CompareSameEntityBUs(ref dataRow, type1, type2);
                }
            }
            else
            {
                if ((dataRow[type1 + "BU_ABC"].ToString() == "A" &&
                    dataRow[type2 + "BU_ABC"].ToString().IndexOf("A") > -1) ||
                    (dataRow[type2 + "BU_ABC"].ToString() == "A" &&
                    dataRow[type1 + "BU_ABC"].ToString().IndexOf("A") > -1))
                {
                    dataRow["SameBUs"] = "substruct";
                }
                else
                {
                    ComparePartEntityBUs(ref dataRow, type1, type2);
                }
            }			
		}

		/// <summary>
		/// compare BUs with same entity-format
		/// </summary>
		/// <param name="dRow"></param>
		private void CompareSameEntityBUs (ref DataRow dRow, string type1, string type2)
		{
			string queryString =string.Format ( "Select InterfaceNum1, InterfaceNum2, IsSame From {0}BuComp " + 
				" Where PdbID = '{1}' AND BuId1 = {2} AND BuId2 = {3};", type1 + type2, 
				dRow["PdbID"].ToString (), dRow[type1 + "BUID"], dRow[type2 + "BuID"]);
			DataTable resultTable = dbQuery.Query (ProtCidSettings.buCompConnection, queryString);

            // most likely, they are true           
            if (resultTable.Rows.Count == 0)
            {
                dRow["SameBUs"] = "same";
                return;
            }
			int interfaceNum1 = Convert.ToInt32 (resultTable.Rows[0]["InterfaceNum1"].ToString ());
			int interfaceNum2 = Convert.ToInt32 (resultTable.Rows[0]["InterfaceNum2"].ToString ());
			int isSame = Convert.ToInt32 (resultTable.Rows[0]["IsSame"].ToString ());
            if (interfaceNum1 != interfaceNum2)
            {
                dRow["SameBUs"] = "difNum";
            }
            else
            {
                if (isSame > 0)
                {
                    dRow["SameBUs"] = "same";
                }
                else
                {
                    dRow["SameBUs"] = "difOrient";
                }
            }          
		}

		/// <summary>
		/// compare BUs with same entity-format
		/// </summary>
		/// <param name="dRow"></param>
		private void ComparePartEntityBUs (ref DataRow dRow, string type1, string type2)
		{
			string queryString =string.Format ( "Select InterfaceNum1, InterfaceNum2, IsSame " + 
				" From {0}BuComp Where PdbID = '{0}' and BuId1 = {1} and BuId2 = {2};", type1 + type2, 
				dRow["PdbID"].ToString (), dRow[type1 + "BUID"], dRow[type2 + "BuID"]);
            DataTable resultTable = dbQuery.Query(ProtCidSettings.buCompConnection, queryString);
			
			if (resultTable.Rows.Count == 0)
			{
                dRow["SameBUs"] = "dif";
				return;
			}
			int isSame = Convert.ToInt32 (resultTable.Rows[0]["IsSame"].ToString ());
			if ( isSame > 0)
			{
                dRow["SameBUs"] = "substruct";
			}
			else
			{
                dRow["SameBUs"] = "dif";
			}
		}
		#endregion

        #region add PISA BU info to table
        /// <summary>
        /// add pisa bu info to table
        /// </summary>
        /// <param name="queryResultTable"></param>
        public void AddPisaBUInfoToTable(ref DataTable queryResultTable)
        {
            List<string> entryList = new List<string> ();
            string queryString = "";
            foreach (DataRow dRow in queryResultTable.Rows)
            {
                if (entryList.Contains(dRow["PdbID"].ToString()))
                {
                    continue;
                }
                entryList.Add(dRow["PdbID"].ToString());
            }
            int i = 0;
            foreach (string pdbId in entryList)
            {
                queryString = string.Format("Select * From PisaAssembly Where PdbID = '{0}' ORDER BY AssemblySeqID;", pdbId);
                DataTable pisaBuInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection,  queryString);
                if (pisaBuInfoTable.Rows.Count == 0)
                {
                    GetPisaMonomerInfo(pdbId, ref pisaBuInfoTable);
                }
                DataRow[] dataRows = queryResultTable.Select(string.Format("PdbID = '{0}'", pdbId));
                i = 0;
                foreach (DataRow dataRow in dataRows)
                {
                    if (i < pisaBuInfoTable.Rows.Count)
                    {
                        dataRow["PisaBuID"] = pisaBuInfoTable.Rows[i]["AssemblySeqID"];
                        dataRow["PISABU_Entity"] = pisaBuInfoTable.Rows[i]["Formula_Entity"];
                        dataRow["PisaBu_AsymID"] = pisaBuInfoTable.Rows[i]["Formula_Asym"];
                        dataRow["PisaBu_Abc"] = pisaBuInfoTable.Rows[i]["Formula_Abc"];
                        dataRow["PisaBu_Auth"] = pisaBuInfoTable.Rows[i]["Formula_Auth"];
                        i++;
                    }
                    else
                    {
                        dataRow["PisaBuID"] = "-";
                        dataRow["PISABU_Entity"] = "-";
                        dataRow["PisaBu_AsymID"] = "-";
                        dataRow["PisaBu_Abc"] = "-";
                        dataRow["PisaBu_Auth"] = "-";
                    }
                }
                // pisa has more bu assemblies
                while (i < pisaBuInfoTable.Rows.Count)
                {
                    DataRow newRow = queryResultTable.NewRow();
                    foreach (DataColumn col in queryResultTable.Columns)
                    {
                        newRow[col] = "-";
                    }
                    newRow["PdbID"] = pdbId;
                    newRow["Names"] = queryResultTable.Rows[0]["Names"];
                    newRow["PisaBuID"] = pisaBuInfoTable.Rows[i]["AssemblySeqID"];
                    newRow["PISABU_Entity"] = pisaBuInfoTable.Rows[i]["Formula_Entity"];
                    newRow["PisaBu_AsymID"] = pisaBuInfoTable.Rows[i]["Formula_Asym"];
                    newRow["PisaBu_ABC"] = pisaBuInfoTable.Rows[i]["Formula_Abc"];
                    newRow["PisaBu_Auth"] = pisaBuInfoTable.Rows[i]["Formula_Auth"];
                    newRow["SameBUs"] = "-";
                    //	newRow["PdbPisa"] = "-";
                    //	newRow["PqsPisa"] = "-";
                    newRow["DNA"] = queryResultTable.Rows[0]["DNA"];
                    newRow["RNA"] = queryResultTable.Rows[0]["RNA"];
                    newRow["Ligands"] = queryResultTable.Rows[0]["Ligands"];
                    newRow["Resolution"] = queryResultTable.Rows[0]["Resolution"];
                    queryResultTable.Rows.Add(newRow);
                    i++;
                }
            }
        }

        private void GetPisaMonomerInfo(string pdbId, ref DataTable pisaBuInfoTable)
        {
            string queryString = string.Format("Select * From PisaBuStatus Where PdbID = '{0}';", pdbId);
            DataTable statusTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            if (statusTable.Rows.Count == 0)
            {
                return;
            }
            if (statusTable.Rows[0]["Status"].ToString().TrimEnd().ToLower() == "ok")
            {
                int assemblyId = 1;
                queryString = string.Format("Select * From AsymUnit Where PdbID = '{0}' AND " +
                    " PolymerType = 'polypeptide';", pdbId);
                DataTable chainInfoTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
                foreach (DataRow chainInfoRow in chainInfoTable.Rows)
                {
                    DataRow newRow = pisaBuInfoTable.NewRow();
                    newRow["PdbID"] = pdbId;
                    newRow["AssemblySeqID"] = assemblyId;
                    newRow["Formula_Entity"] = "(" + chainInfoRow["EntityID"].ToString() + ".1)";
                    newRow["Formula_Asym"] = "(" + chainInfoRow["AsymID"].ToString().TrimEnd() + ")";
                    newRow["Formula_Abc"] = "A";
                    newRow["Formula_Auth"] = "(" + chainInfoRow["AuthorChain"].ToString().TrimEnd() + ")";
                    pisaBuInfoTable.Rows.Add(newRow);
                    assemblyId++;
                }
            }
        }
        #endregion

		#region Query string
		/// <summary>
		/// query string on PDB biol units for input pdb code
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="chainId"></param>
		/// <param name="chainType"></param>
		/// <returns></returns>
		private string GetPdbEntryQueryStringForPdb(string pdbId, string chainId, string chainType)
		{
			string queryStr = "";
			if (pdbId == "") // no input 
				return "";
			try
			{
				switch (chainType)
				{	
					case "author-chain":
						queryStr = string.Format ("SELECT DISTINCT BiolUnit.PdbID  as PdbID, BiolUnit.AsymID as AsymID, BiolUnitID," + 
							" NumOfAsymIDs, AuthorChain, EntityID, PolymerType, Name, NumOfLigandAtoms, Resolution, Method" + 
							" FROM BiolUnit, AsymUnit, PdbEntry" +
							" WHERE AsymUnit.PdbID = '{0}' and AsymUnit.AuthorChain = '{1}'" +  
							" and BiolUnit.PdbID = AsymUnit.PdbID and PdbEntry.PdbID = BiolUnit.PdbID" + 
							" and BiolUnit.AsymID = AsymUnit.AsymID" +
							" ORDER BY BiolUnitID, BiolUnit.AsymID;", pdbId, chainId);
						break;
					case "asym":
						queryStr = string.Format ("SELECT DISTINCT BiolUnit.PdbID  as PdbID, BiolUnit.AsymID as AsymID, BiolUnitID," + 
							" NumOfAsymIDs, AuthorChain, EntityID, PolymerType, Name, NumOfLigandAtoms, Resolution, Method" + 
							" FROM BiolUnit, AsymUnit, PdbEntry" +
							" WHERE AsymUnit.PdbID = '{0}' and AsymUnit.AsymID = '{1}'" +  
							" and BiolUnit.PdbID = AsymUnit.PdbID and PdbEntry.PdbID = BiolUnit.PdbID" + 
							" and BiolUnit.AsymID = AsymUnit.AsymID" +
							" ORDER BY BiolUnitID, BiolUnit.AsymID;", pdbId, chainId);
						break;
					case "entity":
						queryStr = string.Format ("SELECT DISTINCT BiolUnit.PdbID  as PdbID, BiolUnit.AsymID as AsymID, BiolUnitID," + 
							" NumOfAsymIDs, AuthorChain, EntityID, PolymerType, Name, NumOfLigandAtoms, Resolution, Method" + 
							" FROM BiolUnit, AsymUnit, PdbEntry" +
							" WHERE AsymUnit.PdbID = '{0}' and AsymUnit.EntityID = '{1}'" +  
							" and BiolUnit.PdbID = AsymUnit.PdbID and PdbEntry.PdbID = BiolUnit.PdbID" + 
							" and BiolUnit.AsymID = AsymUnit.AsymID" +
							" ORDER BY BiolUnitID, BiolUnit.AsymID;", pdbId, chainId);
						break;
					default:
						queryStr = string.Format ("SELECT DISTINCT BiolUnit.PdbID  as PdbID, BiolUnit.AsymID as AsymID, BiolUnitID," + 
							" NumOfAsymIDs, AuthorChain, EntityID, PolymerType, Name, NumOfLigandAtoms, Resolution, Method" + 
							" FROM BiolUnit, AsymUnit, PdbEntry" +
							" WHERE AsymUnit.PdbID = '{0}'" +  
							" and BiolUnit.PdbID = AsymUnit.PdbID and PdbEntry.PdbID = BiolUnit.PdbID" + 
							" and BiolUnit.AsymID = AsymUnit.AsymID" +
							" ORDER BY BiolUnitID, BiolUnit.AsymID;", pdbId);
						break;
				}
			}
			catch (System.FormatException formatError)
			{
				throw formatError;
			}
			return queryStr;
		}
		

		/// <summary>
		/// query string of ASU/BU for a list of PDB entries
		/// </summary>
		/// <param name="pdbIdList"></param>
		/// <returns></returns>
		private string GetPdbEntryQueryStringForPdbList(List<string> pdbIdList)
		{
			string queryStr = string.Format ("SELECT DISTINCT BiolUnit.PdbID  as PdbID, BiolUnit.AsymID as AsymID, BiolUnitID," + 
				" NumOfAsymIDs, AuthorChain, EntityID, PolymerType, Name, NumOfLigandAtoms, Resolution, Method" + 
				" FROM BiolUnit, AsymUnit, PdbEntry" +
				" WHERE AsymUnit.PdbID IN ({0}) " +  
				" and BiolUnit.PdbID = AsymUnit.PdbID and PdbEntry.PdbID = BiolUnit.PdbID" + 
				" and BiolUnit.AsymID = AsymUnit.AsymID" +
				" ORDER BY BiolUnit.PdbID, BiolUnitID, BiolUnit.AsymID;", FormatListString(pdbIdList));
			return queryStr;
		}

		/// <summary>
		/// format a list of pdbid into a sql string
		/// </summary>
		/// <param name="pdbIdList"></param>
		/// <returns></returns>
		private string FormatListString (List<string> pdbIdList)
		{
			string listString = "";
			foreach (object pdbId in pdbIdList)
			{
				listString += "'";
				listString += pdbId.ToString ();
				listString += "',";
			}
			return listString.TrimEnd (',');
		}
	    #endregion

		#region Query and parse 
		/// <summary>
		/// get PDB result from Scop query
		/// </summary>
		/// <returns></returns>
		internal string GetPdbBiolUnit(string queryStr, ref DataTable queryResultTable)
		{
			string errorMsg = "";
			BiolUnitInfo biolUnitInfo = new BiolUnitInfo ();
			AsymUnitInfo asymUnitInfo = new AsymUnitInfo ();
			Dictionary<string, BiolUnitInfo> biolUnitHash = new Dictionary<string,BiolUnitInfo> ();
			// used to format the asym format and authorchain format
            Dictionary<string, List<string>> entityAsymIdHash = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> entityAuthorChainHash = new Dictionary<string, List<string>>();
			DataTable thisPdbQueryResultTable = new DataTable ();

			string polymerType = "";
			int numOfLigandAtoms = 0;
			double resolution = 0;
			string prePdbId = "";
			string currentPdbId = "";
			string preBiolUnit = "";
			string currentBiolUnit = "";
			// add to asym unit, 
			// used in case where multiple biological units contain same asym chain group
			bool addAsym = true;

			try
			{
                thisPdbQueryResultTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryStr);

				if (thisPdbQueryResultTable.Rows.Count > 0)
				{
					// then insert the data into datatable
					foreach (DataRow dRow in thisPdbQueryResultTable.Rows)
					{
						currentPdbId = dRow["PdbID"].ToString ().Trim ();
						currentBiolUnit = dRow["BiolUnitID"].ToString ();
						// new pdb entry
						if (currentPdbId != prePdbId)
						{
							if (prePdbId != "") // finish one pdb entry, add the last biolunit of previous entry
							{
								if (! biolUnitHash.ContainsKey (preBiolUnit))
									biolUnitHash.Add (preBiolUnit, biolUnitInfo);

								AddBiolUnitInfoToTable (prePdbId, biolUnitHash, asymUnitInfo, ref queryResultTable, entityAsymIdHash, entityAuthorChainHash);

								biolUnitInfo = new BiolUnitInfo ();
								asymUnitInfo = new AsymUnitInfo ();
								addAsym = true;
								biolUnitHash.Clear ();
								preBiolUnit = ""; // new entry
                                entityAsymIdHash = new Dictionary<string, List<string>>();
                                entityAuthorChainHash = new Dictionary<string, List<string>>();
							}
							// entry level
							numOfLigandAtoms = Int32.Parse (dRow["NumOfLigandAtoms"].ToString ());
							if (numOfLigandAtoms > 0)
							{
								asymUnitInfo.hasLigands = "yes";
							}
							resolution = System.Convert.ToDouble (dRow["Resolution"].ToString ());
							if (resolution > 0.0)
							{
								asymUnitInfo.resolution = resolution.ToString ();
							}
							else
							{
								asymUnitInfo.resolution = dRow["Method"].ToString ().Trim ();
							}

						}
						else
						{
							if (preBiolUnit != currentBiolUnit)
							{
								if (preBiolUnit != "") // finish one biol unit
								{
									if (! biolUnitHash.ContainsKey (preBiolUnit))
										biolUnitHash.Add (preBiolUnit, biolUnitInfo);
									
									biolUnitInfo = new BiolUnitInfo();
								}
							}
						}
						polymerType = dRow["PolymerType"].ToString ().Trim ();
						if (polymerType.ToLower ().IndexOf ("polyribonucleotide") > -1)
							biolUnitInfo.hasRNA = "yes";
						else if (polymerType.ToLower ().IndexOf ("polydeoxyribonucleotide") > -1)
							biolUnitInfo.hasDNA = "yes";
						else if (polymerType.ToLower ().IndexOf ("polypeptide") > -1)
						{
							string asymId = dRow["AsymID"].ToString ().Trim ();
							if (! asymUnitInfo.asymUnit_asym.ContainsKey (asymId))
							{
								asymUnitInfo.asymUnit_asym.Add (asymId, 1);
								addAsym = true;
							}
							else
							{
								addAsym = false;
							}
							biolUnitInfo.biolUnit_asym.Add (asymId, Convert.ToInt32 (dRow["NumOfAsymIDs"].ToString ()));
							
							string entityId = dRow["EntityID"].ToString ().Trim ();
							
							if (addAsym)
							{
								if (asymUnitInfo.asymUnit_entityHash.ContainsKey (entityId))
								{
									int count = Int32.Parse ((asymUnitInfo.asymUnit_entityHash)[entityId].ToString ());	
									count ++;
									asymUnitInfo.asymUnit_entityHash[entityId] = count;
								}
								else
									asymUnitInfo.asymUnit_entityHash.Add (entityId, 1);
							}
							
							int asymCount = Int32.Parse (dRow["NumOfAsymIDs"].ToString ());
							if ((biolUnitInfo.biolUnit_entityHash).ContainsKey (entityId))
							{
								int count = Int32.Parse ((biolUnitInfo.biolUnit_entityHash)[entityId].ToString ());
								count += asymCount;
								biolUnitInfo.biolUnit_entityHash[entityId] = count;
							}
							else
								biolUnitInfo.biolUnit_entityHash.Add (entityId, asymCount);

							string entityName = dRow["Name"].ToString ().Trim ();
							if (! (biolUnitInfo.biolUnit_namesHash).ContainsKey (entityId))
							{
								biolUnitInfo.biolUnit_namesHash.Add (entityId, entityName);
							}

							string authorchain = dRow["AuthorChain"].ToString ().Trim ();
							if (addAsym)
							{
								if (asymUnitInfo.asymUnit_authorchainHash.ContainsKey (authorchain))
								{
									int count = Int32.Parse ((asymUnitInfo.asymUnit_authorchainHash)[authorchain].ToString ());
									count ++;
									asymUnitInfo.asymUnit_authorchainHash[authorchain] = count;
								}
								else
									asymUnitInfo.asymUnit_authorchainHash.Add (authorchain, 1);
							}

							if (biolUnitInfo.biolUnit_authorchainHash.ContainsKey (authorchain))
							{
								int count = Int32.Parse ((biolUnitInfo.biolUnit_authorchainHash)[authorchain].ToString ());
								count += asymCount;
								biolUnitInfo.biolUnit_authorchainHash[authorchain] = count;
							}
							else
								biolUnitInfo.biolUnit_authorchainHash.Add (authorchain, asymCount);

							// get the asymId for this entity id
							if (entityAsymIdHash.ContainsKey (entityId))
							{
                                if (!SearchChain(entityAsymIdHash[entityId], asymId))
                                {
                                    entityAsymIdHash[entityId].Add(asymId);
                                }

							}
							else
							{
								List<string> asymIdList = new List<string> ();
								asymIdList.Add (asymId);
								entityAsymIdHash.Add (entityId, asymIdList);
							}
							
							// get the asymId for this entity id
							if (entityAuthorChainHash.ContainsKey (entityId))
							{
                                if (!SearchChain(entityAuthorChainHash[entityId], authorchain))
                                    entityAuthorChainHash[entityId].Add(authorchain);
	
							}
							else
							{
								List<string> chainIdList = new List<string>  ();
								chainIdList.Add (authorchain);
								entityAuthorChainHash.Add (entityId, chainIdList);
							}
						}

						prePdbId = currentPdbId;
						preBiolUnit = currentBiolUnit;
						
					}
					// add the last pdb entry info
					if (! biolUnitHash.ContainsKey (preBiolUnit))
						biolUnitHash.Add (preBiolUnit, biolUnitInfo);
					AddBiolUnitInfoToTable(currentPdbId, biolUnitHash, asymUnitInfo, ref queryResultTable, entityAsymIdHash, entityAuthorChainHash);
				}
				else
				{
					errorMsg = "empty set.";
				}
			}
			catch (Exception exception)
			{
				throw exception;
			}			
			return errorMsg;
		}

		/// <summary>
		/// get PDB result from PDB query
		/// </summary>
		/// <returns></returns>
		private string GetPdbBiolUnitForPdb(string queryStr, ref DataTable queryResultTable)
		{
			string errorMsg = "";
			BiolUnitInfo biolUnitInfo = new BiolUnitInfo ();
			AsymUnitInfo asymUnitInfo = new AsymUnitInfo ();
			Dictionary<string, BiolUnitInfo> biolUnitHash = new Dictionary<string,BiolUnitInfo> ();
			// used to format the asym format and authorchain format
			Dictionary<string, List<string>> entityAsymIdHash = new Dictionary<string,List<string>> ();
			Dictionary<string, List<string>> entityAuthorChainHash = new Dictionary<string,List<string>> ();
			DataTable thisPdbQueryResultTable = new DataTable ();

			string polymerType = "";
			int numOfLigandAtoms = 0;
			double resolution = 0;
			string prePdbId = "";
			string currentPdbId = "";
			string preBiolUnit = "";
		    string currentBiolUnit = "";
			bool addAsym = true;

			try
			{
                thisPdbQueryResultTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryStr);

				if (thisPdbQueryResultTable.Rows.Count > 0)
				{
					// then insert the data into datatable
					foreach (DataRow dRow in thisPdbQueryResultTable.Rows)
					{
						currentPdbId = dRow["PdbID"].ToString ().Trim ();
						currentBiolUnit = dRow["BiolUnitID"].ToString ();
						// new pdb entry
						if (currentPdbId != prePdbId)
						{
							if (prePdbId != "") // finish one pdb entry, add the last biolunit of previous entry
							{
								if (! biolUnitHash.ContainsKey (preBiolUnit))
									biolUnitHash.Add (preBiolUnit, biolUnitInfo);

								AddBiolUnitInfoToTable(prePdbId, biolUnitHash, asymUnitInfo, ref queryResultTable, entityAsymIdHash, entityAuthorChainHash);

								biolUnitInfo = new BiolUnitInfo ();
								asymUnitInfo = new AsymUnitInfo ();
								biolUnitHash.Clear ();
								preBiolUnit = ""; // new entry
								entityAsymIdHash = new Dictionary<string,List<string>> ();
								entityAuthorChainHash = new Dictionary<string,List<string>> ();
							}
							// entry level
							numOfLigandAtoms = Int32.Parse (dRow["NumOfLigandAtoms"].ToString ());
							if (numOfLigandAtoms > 0)
							{
								asymUnitInfo.hasLigands = "yes";
							}
							resolution = System.Convert.ToDouble (dRow["Resolution"].ToString ());
							if (resolution > 0.0)
							{
								asymUnitInfo.resolution = resolution.ToString ();
							}
							else
							{
								asymUnitInfo.resolution = dRow["Method"].ToString ().Trim ();
							}

						}
						else
						{
							if (preBiolUnit != currentBiolUnit)
							{
								if (preBiolUnit != "") // finish one biol unit
								{
									if (! biolUnitHash.ContainsKey (preBiolUnit))
										biolUnitHash.Add (preBiolUnit, biolUnitInfo);
									
									biolUnitInfo = new BiolUnitInfo();
								}
							}
						}
						polymerType = dRow["PolymerType"].ToString ().Trim ();
						if (polymerType.ToLower ().IndexOf ("polyribonucleotide") > -1)
							biolUnitInfo.hasRNA = "yes";
						else if (polymerType.ToLower ().IndexOf ("polydeoxyribonucleotide") > -1)
							biolUnitInfo.hasDNA = "yes";

						if (polymerType.ToLower ().IndexOf ("polypeptide") > -1/* || 
							polymerType.ToLower ().IndexOf ("polyribonucleotide") > -1 ||
							polymerType.ToLower ().IndexOf ("polydeoxyribonucleotide") > -1 ||
							polymerType.ToLower ().IndexOf ("other") > -1*/)
						{
							string asymId = dRow["AsymID"].ToString ().Trim ();
							if (! asymUnitInfo.asymUnit_asym.ContainsKey (asymId))
							{
								asymUnitInfo.asymUnit_asym.Add (asymId, 1);
								addAsym = true;
							}
							else
							{
								addAsym = false;
							}
							biolUnitInfo.biolUnit_asym.Add (asymId, Convert.ToInt32 (dRow["NumOfAsymIDs"].ToString ()));
							
							string entityId = dRow["EntityID"].ToString ().Trim ();
							
							if (addAsym)
							{
								if (asymUnitInfo.asymUnit_entityHash.ContainsKey (entityId))
								{
									int count = Int32.Parse ((asymUnitInfo.asymUnit_entityHash)[entityId].ToString ());	
									count ++;
									asymUnitInfo.asymUnit_entityHash[entityId] = count;
								}
								else
									asymUnitInfo.asymUnit_entityHash.Add (entityId, 1);
							}

							int asymCount = Int32.Parse (dRow["NumOfAsymIDs"].ToString ());
							if ((biolUnitInfo.biolUnit_entityHash).ContainsKey (entityId))
							{
								int count = Int32.Parse ((biolUnitInfo.biolUnit_entityHash)[entityId].ToString ());
								count += asymCount;
								biolUnitInfo.biolUnit_entityHash[entityId] = count;
							}
							else
								biolUnitInfo.biolUnit_entityHash.Add (entityId, asymCount);

							string entityName = dRow["Name"].ToString ().Trim ();
							if (! (biolUnitInfo.biolUnit_namesHash).ContainsKey (entityId))
							{
								biolUnitInfo.biolUnit_namesHash.Add (entityId, entityName);
							}

							string authorchain = dRow["AuthorChain"].ToString ().Trim ();
							if (asymUnitInfo.asymUnit_authorchainHash.ContainsKey (authorchain))
							{
								int count = Int32.Parse ((asymUnitInfo.asymUnit_authorchainHash)[authorchain].ToString ());
								count ++;
								asymUnitInfo.asymUnit_authorchainHash[authorchain] = count;
							}
							else
								asymUnitInfo.asymUnit_authorchainHash.Add (authorchain, 1);

							if (biolUnitInfo.biolUnit_authorchainHash.ContainsKey (authorchain))
							{
								int count = Int32.Parse ((biolUnitInfo.biolUnit_authorchainHash)[authorchain].ToString ());
								count += asymCount;
								biolUnitInfo.biolUnit_authorchainHash[authorchain] = count;
							}
							else
								biolUnitInfo.biolUnit_authorchainHash.Add (authorchain, asymCount);

							// get the asymId for this entity id
							if (entityAsymIdHash.ContainsKey (entityId))
							{
                                if (!SearchChain(entityAsymIdHash[entityId], asymId))
                                    entityAsymIdHash[entityId].Add(asymId);
							}
							else
							{
								List<string> asymIdList = new List<string> ();
								asymIdList.Add (asymId);
								entityAsymIdHash.Add (entityId, asymIdList);
							}
							
							// get the asymId for this entity id
							if (entityAuthorChainHash.ContainsKey (entityId))
							{
                                if (!SearchChain(entityAuthorChainHash[entityId], authorchain))
                                    entityAuthorChainHash[entityId].Add(authorchain);
							}
							else
							{
								List<string> chainIdList  = new List<string> ();
								chainIdList.Add (authorchain);
								entityAuthorChainHash.Add (entityId, chainIdList);
							}
						}

						prePdbId = currentPdbId;
						preBiolUnit = currentBiolUnit;
						
					}
					// add the last pdb entry info
					if (! biolUnitHash.ContainsKey (preBiolUnit))
						biolUnitHash.Add (preBiolUnit, biolUnitInfo);
					AddBiolUnitInfoToTable(currentPdbId, biolUnitHash, asymUnitInfo, ref queryResultTable, entityAsymIdHash, entityAuthorChainHash);
				}
				else
				{
					errorMsg = "empty set.";
				}
			}
			catch (Exception exception)
			{
				throw exception;
			}			
			return errorMsg;
		}
		#endregion
		
		#region Parse query result
		/// <summary>
		/// add pdb data into table first
		/// </summary>
		/// <param name="thisPdbId"></param>
		/// <param name="biolUnitHash"></param>
		/// <param name="asymUnitInfo"></param>
		/// <param name="pdbResultTable"></param>
		private void AddBiolUnitInfoToTable(string thisPdbId, Dictionary<string, BiolUnitInfo> biolUnitHash, AsymUnitInfo asymUnitInfo, ref DataTable pdbResultTable, 
			Dictionary<string, List<string>> entityAsymIdHash, Dictionary<string, List<string>> entityAuthorChainHash)
		{
			string asymUnit_asym = GetAsymFormattedString(asymUnitInfo.asymUnit_asym, entityAsymIdHash);
			string asymUnit_entity = GetEntityFormattedString(asymUnitInfo.asymUnit_entityHash);
			string asymUnit_abc = GetAbcFormatFromEntityHash(asymUnitInfo.asymUnit_entityHash);
			string asymUnit_chain = GetAuthorChainFormattedString(asymUnitInfo.asymUnit_authorchainHash, entityAuthorChainHash);
			
			List<string> biolUnitList = new List<string> (biolUnitHash.Keys);
			biolUnitList.Sort ();

			foreach (string biolUnitId in biolUnitList)
			{
				BiolUnitInfo biolUnitInfo = (BiolUnitInfo) biolUnitHash[biolUnitId]; 

				DataRow biolUnitRow = pdbResultTable.NewRow ();
				biolUnitRow["PDBID"] = thisPdbId;
				biolUnitRow["PdbBuID"] = biolUnitId.ToString ();
				biolUnitRow["DNA"] = biolUnitInfo.hasDNA;
				biolUnitRow["RNA"] = biolUnitInfo.hasRNA;
				biolUnitRow["Ligands"] = asymUnitInfo.hasLigands;
				biolUnitRow["Resolution"] = asymUnitInfo.resolution;
				biolUnitRow["ASU_Entity"] = asymUnit_entity;
				biolUnitRow["ASU_AsymID"] = asymUnit_asym;
				biolUnitRow["ASU_Auth"] = asymUnit_chain;
				biolUnitRow["ASU_ABC"] = asymUnit_abc; // default "-"
				biolUnitRow["PDBBU_AsymID"] = GetAsymFormattedString(biolUnitInfo.biolUnit_asym, entityAsymIdHash);
				biolUnitRow["PDBBU_Entity"] = GetEntityFormattedString(biolUnitInfo.biolUnit_entityHash);
				biolUnitRow["PDBBU_Auth"] = GetAuthorChainFormattedString(biolUnitInfo.biolUnit_authorchainHash, entityAuthorChainHash);				
				biolUnitRow["PDBBU_ABC"] = GetAbcFormatFromEntityHash(biolUnitInfo.biolUnit_entityHash);
				biolUnitRow["Names"] = GetNamesFromNameHash (biolUnitInfo.biolUnit_namesHash);			
				pdbResultTable.Rows.Add (biolUnitRow);
			}
		}	
		#endregion

		#region BU formats
		/// <summary>
		/// get abc format from entity format
		/// i.e. entity format (1.1)(2.2), PQS format A2B
		/// </summary>
		/// <param name="entityHash"></param>
		/// <returns></returns>
        public string GetAbcFormatFromEntityHash(Dictionary<string, int> entityHash)
        {
            string abcUnitString = "";
            List<int> chainNumList =new List<int> ();
            foreach (string entityId in entityHash.Keys)
            {
                chainNumList.Add(entityHash[entityId]);
            }
            chainNumList.Sort();
            // number of entitis is not always less than 26
            for (int i = 0; i < chainNumList.Count; i++)
            {
                if (i < 26)
                {
                    abcUnitString += chainLetters[i];
                }
                else
                {
                    int firstIndex = (int)Math.Floor((double)i / 26.0) - 1;
                    int secondIndex = (int)(i % 26);
                    abcUnitString += ("(" + chainLetters[firstIndex] + chainLetters[secondIndex] + ")");
                }
                if (System.Convert.ToInt32(chainNumList[chainNumList.Count - i - 1]) > 1)
                {
                    abcUnitString += chainNumList[chainNumList.Count - i - 1].ToString();
                }
            }
            if (abcUnitString == "")
            {
                return "-";
            }

            return abcUnitString;
        }

		/// <summary>
		/// get the asym format for asym unit and biol unit
		/// chains with the same sequence enclosed in one set of parentheses
		/// example Homodimer: ASU (A, B) BU(A2, B2) 
		/// heterodimer: ASU (A)(B), BU (A2)(B2)
		/// </summary>
		/// <param name="asymHash"></param>
		/// <param name="entityAsymIdHash"></param>
		/// <returns></returns>
		public string GetAsymFormattedString(Dictionary<string, int> asymHash, Dictionary<string, List<string>> entityAsymIdHash)
		{
			string formattedString = "";
			List<string> entityIdList = new List<string> (entityAsymIdHash.Keys);
			entityIdList.Sort ();
			foreach (string entityId in entityIdList)
			{
				formattedString += "(";
				List<string> asymIdList = entityAsymIdHash[entityId];
				asymIdList.Sort ();
				foreach(string asymId in asymIdList)
				{
					if (asymHash.ContainsKey (asymId))
					{
						formattedString += asymId.ToString ();
                        if (asymHash[asymId] > 1)
                        {
                            formattedString += asymHash[asymId].ToString();
                        }
						formattedString += ",";
					}
				}
				formattedString = formattedString.TrimEnd (',');
				formattedString += ")";
                if (formattedString.IndexOf("()") > -1)
                {
                    formattedString = formattedString.Replace("()", "");
                }
			}
			if (formattedString == "")
				return "-";
			return formattedString;
		}
		/// <summary>
		///  entity format string for asymmetric unit and biological unit, 
		///  example: (1.2)(2.2)
		/// </summary>
		/// <param name="entityHash"></param>
		/// <returns></returns>
		public string GetEntityFormattedString(Dictionary<string, int> entityCountHash)
		{
			string entityFormat = "";
            List<string> entityList = new List<string> (entityCountHash.Keys);
            entityList.Sort();
            foreach (string entityId in entityList)
			{
                int thisCount = entityCountHash[entityId];
                entityFormat += string.Format("({0}.{1})", entityId.ToString(), thisCount); 		
			}
            if (entityFormat == "")
            {
                return "-";
            }
            else
            {
                return entityFormat;
            }
		}

		/// <summary>
		/// return author chain formatted unit strings
		/// example (author chain can be a number)
		/// Asymmetric unit
		/// Homodimer (L, K); (1, 2) Heterodimer (L)(K); (1)(2)
		/// Biological unit
		/// Homotetramer (L2, K2); (1.2, 2.2), Heterotetramer (L2)(K2); (1.2)(2.2)
		/// </summary>
		/// <param name="authorchainHash"></param>
		/// <param name="entityAuthorChainHash"></param>
		/// <returns></returns>
		public string GetAuthorChainFormattedString(Dictionary<string, int> authorchainHash, Dictionary<string, List<string>> entityAuthorChainHash)
		{
			string formattedString = "";
			List<string> entityIdList = new List<string> (entityAuthorChainHash.Keys);
			entityIdList.Sort ();
			foreach (string entityId in entityIdList)
			{
				formattedString += "(";
				List<string> authorChainIdList = entityAuthorChainHash[entityId];
				authorChainIdList.Sort ();
				foreach(string chainId in authorChainIdList)
				{	
					if (authorchainHash.ContainsKey (chainId))
					{
						formattedString += chainId.ToString ();
						if (authorchainHash[chainId] > 1)
						{
							if (IsStringNumber(chainId.ToString ()))
							{
								formattedString += ".";
							}
							formattedString += authorchainHash[chainId].ToString ();
						}
						formattedString += ",";
					}
				}
				formattedString = formattedString.TrimEnd (',');
				formattedString += ")";
				if (formattedString.IndexOf ("()") > -1)
					formattedString = formattedString.Replace ("()", "");
			}
			if (formattedString == "")
				return "-";
			return formattedString;
		}
		
		/// <summary>
		/// format names for this biolunit
		/// </summary>
		/// <param name="namesHash"></param>
		/// <returns></returns>
		private string GetNamesFromNameHash (Dictionary<string, string> namesHash)
		{
			string nameString = "";
			List<string> entityList = new List<string> (namesHash.Keys);
			entityList.Sort ();
			foreach (string entity in entityList)
			{
				/*nameString += entity.ToString ();
				nameString += ":";*/
				nameString += namesHash[entity];
				nameString += "; ";
			}
			return nameString.TrimEnd ("; ".ToCharArray ());
		}

		/// <summary>
		/// check if the input string is numeric
		/// </summary>
		/// <param name="aString"></param>
		/// <returns></returns>
		private bool IsStringNumber(string aString)
		{
			foreach(char ch in aString)
				if (!Char.IsDigit (ch))
					return false;
			return true;
		}

		// there is a bug in BinarySearch function in ArrayList class
		// fortunately, chainlist is short enough to use a sequence search
		private bool SearchChain(List<string> chainList, object chainId)
		{
			foreach (object thisChainId in chainList)
				if (thisChainId.ToString ().ToLower () == chainId.ToString ().ToLower ())
					return true;
			return false;
		}
		#endregion

        #region for individual BU - polypeptide chains only
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buType"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetEntryBUFormats(string pdbId, string buType)
        {
            Dictionary<string, string> buAbcFormatHash = null;
            switch (buType)
            {
                case "pdb":
                    buAbcFormatHash = GetPdbBuAbcFormatHash(pdbId);
                    break;              

                case "pisa":
                    buAbcFormatHash = GetPisaBuAbcFormatHash(pdbId);
                    break;

                default:
                    buAbcFormatHash = GetAsuAbcFormatHash(pdbId);
                    break;
            }
            return buAbcFormatHash;
        }

        #region PDB
        // PDB
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetPdbBuAbcFormatHash(string pdbId)
        {
            Dictionary<string, Dictionary<string, int>> buEntityContentHash = GetPdbBuEntityContentHash(pdbId);
            if (buEntityContentHash.Count == 0)
            {
                if (IsEntryNmr(pdbId)) // NMR entry, PDB BU = ASU
                {
                    Dictionary<string, Dictionary<string, int>> asuEntityContentHash = GetAsuEntityContentHash(pdbId);
                    if (!buEntityContentHash.ContainsKey("1"))
                    {
                        buEntityContentHash.Add("1", asuEntityContentHash["0"]);
                    }
                }
            }
            Dictionary<string, string> buAbcFormatHash = ConvertEntityContentToAbcFormat(buEntityContentHash);
            return buAbcFormatHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryNmr(string pdbId)
        {
            string queryString = string.Format("Select Method From PdbEntry Where PdbID = '{0}';", pdbId);
            DataTable methodTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            if (methodTable.Rows.Count > 0)
            {
                string method = methodTable.Rows[0]["Method"].ToString().TrimEnd();
                if (method.IndexOf("NMR") > -1)
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
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, int>> GetPdbBuEntityContentHash(string pdbId)
        {
            string queryString = string.Format("SELECT DISTINCT BiolUnit.PdbID  as PdbID, BiolUnit.AsymID as AsymID, BiolUnitID," +
                            " NumOfAsymIDs, AuthorChain, EntityID " +
                            " FROM BiolUnit, AsymUnit" +
                            " WHERE AsymUnit.PdbID = '{0}' AND BiolUnit.PdbID = AsymUnit.PdbID " +
                            " AND BiolUnit.AsymID = AsymUnit.AsymID AND PolymerType = 'polypeptide'" +
                            " ORDER BY BiolUnitID, BiolUnit.AsymID;", pdbId);
            DataTable buTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            Dictionary<string, Dictionary<string, int>> buEntityContentHash = new Dictionary<string,Dictionary<string, int>> ();
            string[] pdbBuIds = GetPdbDefinedBiolUnits(pdbId);
            string entityId = "";
            int numOfCopies = -1;
            string buId = "";
            foreach (DataRow dataRow in buTable.Rows)
            {
                buId = dataRow["BiolUnitID"].ToString().TrimEnd();
                if (Array.IndexOf(pdbBuIds, buId) < 0)
                {
                    continue;
                }

                entityId = dataRow["EntityID"].ToString ();
                numOfCopies = Convert.ToInt32(dataRow["NumOfAsymIDs"].ToString ());
                if (buEntityContentHash.ContainsKey(buId))
                {
                    if (buEntityContentHash[buId].ContainsKey(entityId))
                    {
                        int count = buEntityContentHash[buId][entityId];
                        count += numOfCopies;
                        buEntityContentHash[buId][entityId] = count;
                    }
                    else
                    {
                        buEntityContentHash[buId].Add(entityId, numOfCopies);
                    }
                }
                else
                {
                    Dictionary<string, int> entityContentHash = new Dictionary<string, int> ();
                    entityContentHash.Add(entityId, numOfCopies);
                    buEntityContentHash.Add(buId, entityContentHash);
                }
            }
            return buEntityContentHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, int>>[] GetPdbBuEntityAsymAuthContentHashs(string pdbId)
        {
            string queryString = string.Format("SELECT DISTINCT BiolUnit.PdbID  as PdbID, BiolUnit.AsymID as AsymID, BiolUnitID," +
                            " NumOfAsymIDs, AuthorChain, EntityID " +
                            " FROM BiolUnit, AsymUnit" +
                            " WHERE AsymUnit.PdbID = '{0}' AND BiolUnit.PdbID = AsymUnit.PdbID " +
                            " AND BiolUnit.AsymID = AsymUnit.AsymID AND PolymerType = 'polypeptide'" +
                            " ORDER BY BiolUnitID, BiolUnit.AsymID;", pdbId);
            DataTable buTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

             string[] pdbBuIds = GetPdbDefinedBiolUnits(pdbId);

             Dictionary<string, Dictionary<string, int>>[] buContentHashs = new Dictionary<string, Dictionary<string, int>>[3];
            if (pdbBuIds.Length > 0)
            {
                buContentHashs[(int)BuContentType.entity] = GetPdbBuEntityContentHash(buTable, pdbBuIds);
                buContentHashs[(int)BuContentType.asym] = GetPdbBuAsymContentHash(buTable, pdbBuIds);
                buContentHashs[(int)BuContentType.author] = GetPdbBuAuthorContentHash(buTable, pdbBuIds);
            }
            else
            {
                buContentHashs = GetAsuEntityAsymAuthContentHashs(pdbId);
            }
            
            return buContentHashs;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buTable"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, int>> GetPdbBuEntityContentHash(DataTable buTable, string[] pdbBuIds)
        {
            Dictionary<string, Dictionary<string, int>> buEntityContentHash = new Dictionary<string,Dictionary<string,int>> ();
            string entityId = "";
            int numOfCopies = -1;
            string buId = "";

            foreach (DataRow dataRow in buTable.Rows)
            {
                buId = dataRow["BiolUnitID"].ToString().TrimEnd();
                if (Array.IndexOf(pdbBuIds, buId) < 0)
                {
                    continue;
                }

                entityId = dataRow["EntityID"].ToString();
               
                numOfCopies = Convert.ToInt32(dataRow["NumOfAsymIDs"].ToString());
                if (buEntityContentHash.ContainsKey(buId))
                {
                    if (buEntityContentHash[buId].ContainsKey(entityId))
                    {
                        int count = buEntityContentHash[buId][entityId];
                        count += numOfCopies;
                        buEntityContentHash[buId][entityId] = count;
                    }
                    else
                    {
                        buEntityContentHash[buId].Add(entityId, numOfCopies);
                    }
                }
                else
                {
                    Dictionary<string, int> entityContentHash = new Dictionary<string,int> ();
                    entityContentHash.Add(entityId, numOfCopies);
                    buEntityContentHash.Add(buId, entityContentHash);
                }
            }
            return buEntityContentHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buTable"></param>
        /// <param name="pdbBuIds"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, int>> GetPdbBuAsymContentHash(DataTable buTable, string[] pdbBuIds)
        {
            Dictionary<string, Dictionary<string, int>> buAsymContentHash = new Dictionary<string, Dictionary<string, int>>();
            string asymChain = "";
            int numOfCopies = -1;
            string buId = "";
            foreach (DataRow dataRow in buTable.Rows)
            {
                buId = dataRow["BiolUnitID"].ToString().TrimEnd();
                if (Array.IndexOf(pdbBuIds, buId) < 0)
                {
                    continue;
                }

                asymChain = dataRow["AsymID"].ToString().TrimEnd();
                if (asymChain == "_")
                {
                    asymChain = "A";
                }
                numOfCopies = Convert.ToInt32(dataRow["NumOfAsymIDs"].ToString());
                if (buAsymContentHash.ContainsKey(buId))
                {
                    if (buAsymContentHash[buId].ContainsKey(asymChain))
                    {
                        int count = buAsymContentHash[buId][asymChain];
                        count += numOfCopies;
                        buAsymContentHash[buId][asymChain] = count;
                    }
                    else
                    {
                        buAsymContentHash[buId].Add(asymChain, numOfCopies);
                    }
                }
                else
                {
                    Dictionary<string, int> asymContentHash = new Dictionary<string,int> ();
                    asymContentHash.Add(asymChain, numOfCopies);
                    buAsymContentHash.Add(buId, asymContentHash);
                }
            }
            return buAsymContentHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buTable"></param>
        /// <param name="pdbBuIds"></param>
        /// <returns></returns>
        private Dictionary<string, Dictionary<string, int>> GetPdbBuAuthorContentHash(DataTable buTable, string[] pdbBuIds)
        {
            Dictionary<string, Dictionary<string,int>> buAuthContentHash = new Dictionary<string,Dictionary<string,int>> ();
            string authChain = "";
            int numOfCopies = -1;
            string buId = "";
            foreach (DataRow dataRow in buTable.Rows)
            {
                buId = dataRow["BiolUnitID"].ToString().TrimEnd();
                if (Array.IndexOf(pdbBuIds, buId) < 0)
                {
                    continue;
                }

                authChain = dataRow["AuthorChain"].ToString().TrimEnd();
                if (authChain == "_")
                {
                    authChain = "A";
                }
                numOfCopies = Convert.ToInt32(dataRow["NumOfAsymIDs"].ToString());
                if (buAuthContentHash.ContainsKey(buId))
                {
                    if (buAuthContentHash[buId].ContainsKey(authChain))
                    {
                        int count = buAuthContentHash[buId][authChain];
                        count += numOfCopies;
                        buAuthContentHash[buId][authChain] = count;
                    }
                    else
                    {
                        buAuthContentHash[buId].Add(authChain, numOfCopies);
                    }
                }
                else
                {
                    Dictionary<string,int> authContentHash = new Dictionary<string,int> ();
                    authContentHash.Add(authChain, numOfCopies);
                    buAuthContentHash.Add(buId, authContentHash);
                }
            }
            return buAuthContentHash;
        }

        /// <summary>
        /// the biological units defined by PDB in the order as following
        /// 1. Author_defined
        /// 2. Author_and_Software_defined
        /// 3. if only software_defined, pick the BUs
        /// 4. all other non software_defined
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public string[] GetPdbDefinedBiolUnits(string pdbId)
        {
            List<string> pdbBuList = new List<string> ();

            string queryString = string.Format("Select * From PdbBuStat Where PdbID = '{0}';", pdbId);
            DataTable buStatTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            string details = "";
            string buId = "";
            // take the author-defined biol units first
            foreach (DataRow buStatRow in buStatTable.Rows)
            {
                details = buStatRow["Details"].ToString().TrimEnd().ToLower ();
                buId = buStatRow["BiolUnitID"].ToString();
                if (details.IndexOf("author_") > -1)
                {
                    if (!pdbBuList.Contains(buId))
                    {
                        pdbBuList.Add(buId);
                    }
                }
            }
            if (pdbBuList.Count == 0) // only software_defined_assembly
            {
                foreach (DataRow buStatRow in buStatTable.Rows)
                {
                    buId = buStatRow["BiolUnitID"].ToString();
                    if (! pdbBuList.Contains(buId))
                    {
                        pdbBuList.Add(buId);
                    }
                }
            }
            string[] pdbBUs = new string[pdbBuList.Count];
            pdbBuList.CopyTo(pdbBUs);
            return pdbBUs;
        }
        #endregion

        #region ABC format, originated from PQS
        public Dictionary<string, string> ConvertEntityContentToAbcFormat(Dictionary<string, Dictionary<string, int>> buEntityContentHash)
        {
            Dictionary<string, string> buAbcFormatHash = new Dictionary<string, string>();
            string buAbcFormat = "";
            foreach (string buId in buEntityContentHash.Keys)
            {
                buAbcFormat = GetAbcFormatFromEntityHash(buEntityContentHash[buId]);
                buAbcFormatHash.Add(buId, buAbcFormat);
            }
            return buAbcFormatHash;
        }
        #endregion

        #region PISA
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, string>[] GetPisaBuFormatHashs(string pdbId)
        {
            Dictionary<string, string>[] pisaBuFormatHashs = new Dictionary<string, string>[4];
            string queryString = string.Format("Select AssemblySeqID, Formula_ABC, Formula_Entity, " +
                " Formula_Asym, Formula_Auth From PisaAssembly " +
               " Where PdbID = '{0}';", pdbId);
            DataTable pisaBuTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            Dictionary<string, string> buAbcFormatHash = GetPisaBuAbcFormatHash(pisaBuTable);
            Dictionary<string, string> buAsymFormatHash = GetPisaBuAsymFormatHash(pisaBuTable);
            Dictionary<string, string> buEntityFormatHash = GetPisaBuEntityFormatHash(pisaBuTable);
            Dictionary<string, string> buAuthFormatHash = GetPisaBuAuthFormatHash(pisaBuTable);

            if (buEntityFormatHash.Count == 0)
            {
                if (IsPisaBuStatOk(pdbId))
                {
                    buAbcFormatHash.Add("1", "A");
                    DataTable asuTable = GetEntryAsuTable(pdbId);
                    if (asuTable.Rows.Count > 0)
                    {
                        buAsymFormatHash.Add("1", asuTable.Rows[0]["AsymID"].ToString().TrimEnd());
                        buAuthFormatHash.Add("1", asuTable.Rows[0]["AuthorChain"].ToString().TrimEnd());
                        buEntityFormatHash.Add("1", "(" + asuTable.Rows[0]["EntityID"].ToString() + ".1)");
                    }
                }
            }
            Dictionary<string, string>[] buFormatHashs = new Dictionary<string, string>[4];
            buFormatHashs[(int)BuContentType.entity] = buEntityFormatHash;
            buFormatHashs[(int)BuContentType.asym] = buAsymFormatHash;
            buFormatHashs[(int)BuContentType.author] = buAuthFormatHash;
            buFormatHashs[(int)BuContentType.abc] = buAbcFormatHash;

            return buFormatHashs;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetPisaBuAbcFormatHash(DataTable pisaBuTable)
        {
            Dictionary<string, string> buAbcFormatHash = new Dictionary<string, string>();
            string buId = "";
            string abcFormat = "";
            foreach (DataRow buRow in pisaBuTable.Rows)
            {
                buId = buRow["AssemblySeqID"].ToString();
                abcFormat = buRow["Formula_ABC"].ToString().TrimEnd();
                if (abcFormat == "-")
                {
                    continue;
                }
                if (!buAbcFormatHash.ContainsKey(buId))
                {
                    buAbcFormatHash.Add(buId, abcFormat);
                }
            }
            return buAbcFormatHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetPisaBuAuthFormatHash(DataTable pisaBuTable)
        {
            Dictionary<string, string> buAuthFormatHash = new Dictionary<string, string>();
            string buId = "";
            string authFormat = "";
            foreach (DataRow buRow in pisaBuTable.Rows)
            {
                buId = buRow["AssemblySeqID"].ToString();
                authFormat = buRow["Formula_Auth"].ToString().TrimEnd();
                if (authFormat == "-")
                {
                    continue;
                }
                if (! buAuthFormatHash.ContainsKey(buId))
                {
                    buAuthFormatHash.Add(buId, authFormat);
                }
            }
            return buAuthFormatHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetPisaBuAsymFormatHash(DataTable pisaBuTable)
        {
            Dictionary<string, string> buAsymFormatHash = new Dictionary<string, string>();
            string buId = "";
            string asymFormat = "";
            foreach (DataRow buRow in pisaBuTable.Rows)
            {
                buId = buRow["AssemblySeqID"].ToString(); 
                asymFormat = buRow["Formula_Asym"].ToString().TrimEnd();
                if (asymFormat == "-")
                {
                    continue;
                }
                if (! buAsymFormatHash.ContainsKey(buId))
                {
                    buAsymFormatHash.Add(buId, asymFormat);
                }
            }
            return buAsymFormatHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetPisaBuEntityFormatHash(DataTable pisaBuTable)
        {
            Dictionary<string, string> buEntityFormatHash = new Dictionary<string, string>();
            string buId = "";
            string entityFormat = "";
            foreach (DataRow buRow in pisaBuTable.Rows)
            {
                buId = buRow["AssemblySeqID"].ToString();
                entityFormat = buRow["Formula_Entity"].ToString().TrimEnd();
                if (entityFormat == "-")
                {
                    continue;
                }
                if (! buEntityFormatHash.ContainsKey(buId))
                {
                    buEntityFormatHash.Add(buId, entityFormat);
                }
            }
            return buEntityFormatHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryAsuTable(string pdbId)
        {
            string queryString = string.Format("Select EntityID, AsymID, AuthorChain From AsymUnit " +
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide' Order By EntityID;", pdbId);
            DataTable asuTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            return asuTable;
        }

        //PISA
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetPisaBuAbcFormatHash(string pdbId)
        {
            string queryString = string.Format("Select AssemblySeqID, Formula_ABC From PisaAssembly " + 
                " Where PdbID = '{0}';", pdbId);
            DataTable pisaBuTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            Dictionary<string, string> buAbcFormatHash = new Dictionary<string, string>();
            string buId = "";
            string abcFormat = "";
            if (pisaBuTable.Rows.Count > 0)
            {
                foreach (DataRow buRow in pisaBuTable.Rows)
                {
                    buId = buRow["AssemblySeqID"].ToString();
                    abcFormat = buRow["Formula_ABC"].ToString().TrimEnd();
                    if (abcFormat == "-")
                    {
                        continue;
                    }
                    if (! buAbcFormatHash.ContainsKey(buId))
                    {
                        buAbcFormatHash.Add(buId, abcFormat);
                    }
                }
            }
            else
            {
                if (IsPisaBuStatOk(pdbId))
                {
                    if (!buAbcFormatHash.ContainsKey("1"))
                    {
                        buAbcFormatHash.Add("1", "A");
                    }
                }
            }
            return buAbcFormatHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, int>> GetPisaBuEntityContentHash(string pdbId)
        {
            string queryString = string.Format("Select AssemblySeqID, Formula_Entity From PisaAssembly " +
                " Where PdbID = '{0}' AND AsmSetID = 1;", pdbId); // the stable assemblies only with set id = 1
            DataTable pisaBuTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            Dictionary<string, Dictionary<string, int>> buEntityContentHash = new Dictionary<string,Dictionary<string,int>> ();
            string buId = "";
            string entityFormat = "";
            if (pisaBuTable.Rows.Count > 0)
            {
                foreach (DataRow buRow in pisaBuTable.Rows)
                {
                    buId = buRow["AssemblySeqID"].ToString();
                    entityFormat = buRow["Formula_Entity"].ToString().TrimEnd();
                    if (entityFormat == "-")
                    {
                        continue;
                    }
                    Dictionary<string, int> entityContentHash = GetEntityContentHashFromEntityFormat (entityFormat);

                    buEntityContentHash.Add(buId, entityContentHash);
                }
            }
            else
            {
                if (IsPisaBuStatOk(pdbId))
                {
                    Dictionary<string, int> entityBuContentHash = new Dictionary<string,int> ();
                    int entityId = GetMonomerEntity(pdbId);
                    entityBuContentHash.Add(entityId.ToString (), 1);
                    buEntityContentHash.Add("1", entityBuContentHash);
                }
            }
            return buEntityContentHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int GetMonomerEntity(string pdbId)
        {
            string queryString = string.Format("Select EntityID From AsymUnit " + 
                " Where PdbID = '{0}' AND PolymerType = 'polypeptide' Order By EntityID;", pdbId);
            DataTable entityTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            if (entityTable.Rows.Count > 0)
            {
                return Convert.ToInt32(entityTable.Rows[0]["EntityID"].ToString ());
            }
            return -1;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="buEntityFormat"></param>
        /// <returns></returns>
        public Dictionary<string, int> GetEntityContentHashFromEntityFormat(string buEntityFormat)
        {
            buEntityFormat = buEntityFormat.Trim(' ');
            string[] entityContentFields = buEntityFormat.Split(')');
            Dictionary<string, int> entityContentHash = new Dictionary<string,int> ();
            string entityContent = "";
            string entityId = "";
            int numOfCopies = 0;
            foreach (string entityContentField in entityContentFields)
            {
                if (entityContentField == "")
                {
                    continue;
                }
                entityContent = entityContentField.Remove (0, 1); // remove "("
                string[] contentFields = entityContent.Split('.');
                entityId = contentFields[0];
                numOfCopies = Convert.ToInt32(contentFields[1]);
                entityContentHash.Add(entityId, numOfCopies);
            }
            return entityContentHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsPisaBuStatOk(string pdbId)
        {
            string queryString = string.Format("Select * From PisaBuStatus Where PdbID = '{0}';", pdbId);
            DataTable pisaBuStatusTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            if (pisaBuStatusTable.Rows.Count == 0)
            {
                return false;
            }
            else
            {
                string status = pisaBuStatusTable.Rows[0]["Status"].ToString ().TrimEnd ();
                if (status.ToLower() == "ok")
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region ASU
        public Dictionary<string, string> GetAsuAbcFormatHash(string pdbId)
        {
            Dictionary<string, Dictionary<string, int>> asuEntityContentHash = GetAsuEntityContentHash(pdbId);
            Dictionary<string, string> asuAbcFormatHash = ConvertEntityContentToAbcFormat(asuEntityContentHash);
            return asuAbcFormatHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public string GetAsuAbcFormat(string pdbId)
        {
            Dictionary<string, Dictionary<string, int>> asuEntityContentHash = GetAsuEntityContentHash(pdbId);
            Dictionary<string, string> asuAbcFormatHash = ConvertEntityContentToAbcFormat(asuEntityContentHash);
            return (string)asuAbcFormatHash["0"];
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, int>> GetAsuEntityContentHash(string pdbId)
        {
            string queryString = string.Format("SELECT * FROM AsymUnit WHere PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable asuTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            Dictionary<string, Dictionary<string, int>> asuEntityContentHash = new Dictionary<string, Dictionary<string, int>>();
            Dictionary<string, int> entityContentHash = new Dictionary<string,int> ();
            string entityId = "";
            string buId = "0";
            foreach (DataRow dataRow in asuTable.Rows)
            {
                entityId = dataRow["EntityID"].ToString();

                if (entityContentHash.ContainsKey(entityId))
                {
                    int count = entityContentHash[entityId];
                    count ++;
                    entityContentHash[entityId] = count;
                }
                else
                {
                    entityContentHash.Add(entityId, 1);
                }
            }
            asuEntityContentHash.Add(buId, entityContentHash);
            return asuEntityContentHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, int>>[] GetAsuEntityAsymAuthContentHashs(string pdbId)
        {
            string queryString = string.Format("SELECT  EntityID, AsymID, AuthorChain FROM AsymUnit WHere PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable asuTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);

            Dictionary<string, Dictionary<string, int>>[] asuContentHashs = new Dictionary<string, Dictionary<string, int>>[3];
            asuContentHashs[(int)BuContentType.entity] = GetAsuEntityContentHash(asuTable);
            asuContentHashs[(int)BuContentType.asym] = GetAsuAsymContentHash(asuTable);
            asuContentHashs[(int)BuContentType.author] = GetAsuAuthorContentHash(asuTable);

            return asuContentHashs;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, int>> GetAsuEntityContentHash(DataTable asuTable)
        {
            Dictionary<string, Dictionary<string, int>> asuEntityContentHash = new Dictionary<string, Dictionary<string, int>>();
            Dictionary<string, int> entityContentHash = new Dictionary<string,int> ();
            string entityId = "";
            string buId = "0";
            foreach (DataRow dataRow in asuTable.Rows)
            {
                entityId = dataRow["EntityID"].ToString();

                if (entityContentHash.ContainsKey(entityId))
                {
                    int count = entityContentHash[entityId];
                    count++;
                    entityContentHash[entityId] = count;
                }
                else
                {
                    entityContentHash.Add(entityId, 1);
                }
            }
            asuEntityContentHash.Add(buId, entityContentHash);
            return asuEntityContentHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, int>> GetAsuAsymContentHash(DataTable asuTable)
        {
            Dictionary<string, Dictionary<string, int>> asuAsymContentHash = new Dictionary<string, Dictionary<string, int>>();
            Dictionary<string, int> asymContentHash = new Dictionary<string,int> ();
            string asymChain = "";
            string buId = "0";
            foreach (DataRow dataRow in asuTable.Rows)
            {
                asymChain = dataRow["AsymID"].ToString().TrimEnd ();

                asymContentHash.Add(asymChain, 1);
            }
            asuAsymContentHash.Add(buId, asymContentHash);
            return asuAsymContentHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, int>> GetAsuAuthorContentHash(DataTable asuTable)
        {
            Dictionary<string, Dictionary<string, int>> asuAuthorContentHash = new Dictionary<string, Dictionary<string, int>>();
            Dictionary<string, int> authorContentHash = new Dictionary<string,int> ();
            string authorChain = "";
            string buId = "0";
            foreach (DataRow dataRow in asuTable.Rows)
            {
                authorChain = dataRow["AuthorChain"].ToString().TrimEnd ();

                authorContentHash.Add(authorChain, 1);
            }
            asuAuthorContentHash.Add(buId, authorContentHash);
            return asuAuthorContentHash;
        }
        #endregion

        #endregion
    }
}
