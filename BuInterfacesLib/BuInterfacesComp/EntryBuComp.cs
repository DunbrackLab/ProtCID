using System;
using System.IO;
using System.Collections;
using System.Data;
using System.Xml;
using System.Xml.Serialization;
using DbLib;
using ProgressLib;
using XtalLib.Settings;
using XtalLib.ProtInterfaces;
using BuQueryLib;
using BuInterfacesLib.BuMatch;

namespace BuInterfacesLib.BuInterfacesComp
{
	/// <summary>
	/// Comparison of biological units from PDB/PQS/PISA
	/// </summary>
	public class EntryBuComp
	{
#if DEBUG
		StreamWriter fileWriter = new StreamWriter ("PdbPqsBuComp.txt", false);
#endif
		private DbInsert dbInsert = new DbInsert ();
		// save the list of BUs with matched chains
		private ArrayList buMatchList = new ArrayList ();
		public static DataTable pisaBuMatrixTable = null;

		public EntryBuComp()
		{
		}

		#region properties
		public BuChainMatch[] BuMatchList 
		{
			get
			{
				BuChainMatch[] buChainMatches = new BuChainMatch [buMatchList.Count];
				buMatchList.CopyTo (buChainMatches);
				return buChainMatches;
			}
		}
		#endregion

		#region compare same/sub entity BUs
		/// <summary>
		/// compare PDB and PQS entry biological units 
		/// in same Entity format
		/// </summary>
        public string[] CompareExistingEntryBUs(bool isUpdate)
        {
            //AppSettings.LoadDirSettings ();
            BuInterfaceDbBuilder.progressInfo.Reset();
            BuInterfaceDbBuilder.progressInfo.overallProgLabel = "Compare PDB/PQS/PISA BUs";
            BuInterfaceDbBuilder.progressInfo.currentOperationLabel = "Comparing PDB/PQS/PISA BUs";
            BuInterfaceDbBuilder.progressInfo.currentFileName = "Preparing comparison. Please wait ...";
            BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue("Comparing PDB/PQS/PISA biological units.");

            // database setting
            string connString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE="
                + AppSettings.dirSettings.dbPath;
            DbBuilder.dbConnect.ConnectString = connString;
            DbBuilder.dbConnect.ConnectToDatabase();

            GetPisaBuMatrices();

            BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue("Retrieving entries in same or sub entity-format.");
            // get chain matches between PDB and PQS 
            // for biological units in same entity format
            /*          try
                      {
                          FindSameAndSubEntityBUs(isUpdate);
                      }
                      catch (Exception ex)
                      {
                          BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue("Chain Matches errors: " + ex.Message);
                          return null;
                      }

                      // disconnect from database
                      DbBuilder.dbConnect.DisconnectFromDatabase();

                      BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue("Done!");

                      // save the BUs in same entity format into a xml file
                      // for debug purpose
          #if DEBUG
                      BuChainMatchCategory buMatchCat = new BuChainMatchCategory();
                      buMatchCat.BuChainMatchList = this.BuMatchList;
                      string buXmlFile = "PdbPqsBu_sameSubEntity1.xml";
                      buMatchCat.Save(buXmlFile);
          #endif

                      BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue("Comparing PDB/PQS/PISA biological units ...");
                      // compare biological units
                      PdbPqsPisaBuComp buComp = new PdbPqsPisaBuComp(AppSettings.dirSettings.coordXmlPath, AppSettings.dirSettings.pqsBuPath);
                      // change database to interfaces database
                      string[] pdbIds = buComp.CompareBiolUnits(this.BuMatchList, AppSettings.dirSettings.interfaceDbPath, AppSettings.paramFile, isUpdate);

                      BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue("Done!");
                      return pdbIds;
            */
            BuChainMatchCategory buMatchCat = new BuChainMatchCategory();
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(BuChainMatchCategory));
            FileStream xmlFileStream = new FileStream("PdbPqsBu_sameSubEntity.xml", FileMode.Open);
            buMatchCat = (BuChainMatchCategory)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue("Comparing PDB and PQS biological units ...");
            // compare biological units
            PdbPqsPisaBuComp buComp = new PdbPqsPisaBuComp(AppSettings.dirSettings.coordXmlPath, AppSettings.dirSettings.pqsBuPath);
            // change database to interfaces database
            string[] pdbIds = buComp.CompareBiolUnits(buMatchCat.BuChainMatchList, AppSettings.dirSettings.interfaceDbPath, AppSettings.paramFile, isUpdate);

            BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue("Done!");
            return pdbIds;
        }

		/// <summary>
		/// temporary used to build pisa assembly, should modify the database structures
		/// </summary>
		/// <returns></returns>
		private void GetPisaBuMatrices ()
		{
			string queryString = "Select * From PisaBuMatrix;";
			pisaBuMatrixTable = BuInterfaceDbBuilder.dbQuery.Query (queryString);
		}
		#endregion

		#region Same/Sub BUs in Entity Formats
		/// <summary>
		/// find All PDB and PQS biological units with same/Sub entity format
		/// biological units matched by asymmetric chain format
		/// and biological unit order
		/// </summary>
		/// <returns></returns>
		private void FindSameAndSubEntityBUs (bool isUpdate)
		{
			string[] entryList = GetProteinEntries (isUpdate);
 //           string[] entryList = GetProteinEntriesFromLog();

			BiolUnitQuery buQuery = new BiolUnitQuery ();
			DataTable buTable = new DataTable ();
			string pdbBuAsym = "";
			string pqsBuAsym = "";
			string pisaBuAsym = "";

			foreach (string pdbId in entryList)
			{				
				buTable = new DataTable ("BUs");
                try
                {
                    buQuery.GetBiolUnitForPdbEntry(pdbId, ref buTable, false);
                }
                catch (Exception ex)
                {
                    BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue(string.Format ("Get {0} BuTable error: {1}", pdbId, ex.Message));
                    continue;
                }

				// match PDB BUs to PQS BUs one by one
				foreach (DataRow buRow in buTable.Rows)
				{
					pdbBuAsym = buRow["PDBBU-AsymID"].ToString ();
					pqsBuAsym = buRow["PQSBU-AsymID"].ToString ();
					pisaBuAsym = buRow["PISABU-AsymID"].ToString ();
					// if all are monomer or no available data marked by "-", skip it
					if (pdbBuAsym.Trim ("()".ToCharArray()).Length <= 1 &&
						pqsBuAsym.Trim ("()".ToCharArray ()).Length <= 1 && 
						pisaBuAsym.Trim ("()".ToCharArray ()).Length <= 1)
					{						
						continue;
					}
					try
					{
						// PQS BU or PQS BU or PISA BU is not a monomer, need compute interfaces
						AddDataIntoBuMatch(buRow, pdbId);
					}
					catch (Exception ex)
					{
						BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue ("Format " + pdbId + " BU Match Errors: " + ex.Message);
					}
				}
			}
		}
		#endregion

		#region Format PDB/PQS/PISA BUs and match chains
		/// <summary>
		/// format data into BuChainMatch format
		/// </summary>
		/// <param name="buRow"></param>
		/// <param name="pdbId"></param>
		private void AddDataIntoBuMatch (DataRow buRow, string pdbId)
		{
			BuChainMatch buMatch = new BuChainMatch ();
			buMatch.pdbId = pdbId;
			
			if (buRow["PdbBuID"].ToString () == "-")
			{
				buMatch.pdbBuId = "-1";
			}
			else
			{
				buMatch.pdbBuId = buRow["PdbBuID"].ToString ();
			}

			if (buRow["PqsBuID"].ToString () == "-")
			{
				buMatch.pqsBuId = "-1";
			}
			else
			{
				buMatch.pqsBuId = buRow["PqsBuID"].ToString ();
			}

			if (buRow["PisaBuID"].ToString () == "-")
			{
				buMatch.pisaBuId = "-1";
			}
			else
			{
				buMatch.pisaBuId = buRow["PisaBuID"].ToString ();
			}
			buMatch.pdbBuAsym = buRow["PDBBU-AsymID"].ToString ();
			buMatch.pqsBuAsym = buRow["PQSBU-AsymID"].ToString ();
			buMatch.pisaBuAsym = buRow["PISABU-AsymID"].ToString ();

			buMatch.pdbBuEntity = buRow["PDBBU-Entity"].ToString ();
			buMatch.pqsBuEntity = buRow["PQSBU-Entity"].ToString ();
			buMatch.pisaBuEntity = buRow["PISABU-Entity"].ToString ();

			buMatch.pdbBuAuth = buRow["PDBBU-Auth"].ToString ();
			buMatch.pqsBuAuth = buRow["PQSBU-Auth"].ToString ();
			buMatch.pisaBuAuth = buRow["PISABU-Auth"].ToString ();

			buMatch.pdbBuAbc = buRow["PDBBU-Abc"].ToString ();
			buMatch.pqsBuAbc = buRow["PQSBU-Abc"].ToString ();
			buMatch.pisaBuAbc = buRow["PISABU-Abc"].ToString ();

			MatchPqsPdbChains (buRow, buMatch);
		}
		
		/// <summary>
		/// Match PQS chains to PDB author chains 
		/// which are used in biological unit files
		/// </summary>
		private void MatchPqsPdbChains(DataRow buRow, BuChainMatch buMatch)
		{
			DataTable[] buChainsTables = GetChainSymOp (buRow["PdbID"].ToString ());
			if (buRow["PdbBuID"].ToString () == "-" && buRow["PqsBuID"].ToString () == "-")
			{
				return;
			}
			
			if (buRow["PqsBuID"].ToString () == "-")
			{
				DataRow[] chainRows = buChainsTables[(int)BuInterfaceDbBuilder.DataType.PDB].Select 
					(string.Format ("PdbID = '{0}' AND BiolUnitID = '{1}'", 
					buRow["PdbID"].ToString (), buRow["PdbBuID"].ToString ()));
				foreach (DataRow chainRow in chainRows)
				{
					BuChainPair chainPair = new BuChainPair ();
					chainPair.asymChain = chainRow["AsymID"].ToString ().Trim ();
					chainPair.pqsChain = "-1";
					chainPair.symOpStr = "-";
					chainPair.authChain = chainRow["AuthorChain"].ToString ().Trim ();
					chainPair.entityId = Convert.ToInt32 (chainRow["EntityID"].ToString ());
					buMatch.AddChainPair (chainPair);
				}
				buMatchList.Add (buMatch);
			}
			else
			{
				// add chain match
				AddChainMatch (buRow, buChainsTables, buMatch);
			}
		}

		/// <summary>
		/// add chain match into the list
		/// </summary>
		/// <param name="buRow"></param>
		/// <param name="pqsChainsTable"></param>
		private void AddChainMatch (DataRow buRow, DataTable[] buChainsTables, BuChainMatch buMatch)
		{
			DataRow[] pdbBuRows = null;
			ArrayList asymChainList = null;
			// get the list of asymmetric chains from PDB
			if (buRow["PdbBuID"].ToString () != "-")
			{
				pdbBuRows = buChainsTables[(int)BuInterfaceDbBuilder.DataType.PDB].Select 
					                       (string.Format ("PdbID = '{0}' AND BiolUnitID = '{1}'", 
					                           buRow["PdbID"], buRow["PdbBuID"].ToString ()));
				asymChainList = new ArrayList ();
				foreach (DataRow pdbRow in pdbBuRows)
				{
					if (! asymChainList.Contains (pdbRow["AsymID"].ToString ().Trim ()))
					{
						asymChainList.Add (pdbRow["AsymID"].ToString ().Trim ());
					}
				}
			}
			
			int pqsBuId = Convert.ToInt32 (buRow["PqsBuID"].ToString ());

			DataRow[] pqsBuRows = buChainsTables[(int)BuInterfaceDbBuilder.DataType.PQS].
				Select (string.Format ("PdbID = '{0}' AND PqsBiolUnitID = '{1}'", buRow["PdbID"], pqsBuId));

			// match PQS chains to PDB chains
			foreach (DataRow chainRow in pqsBuRows)
			{
				BuChainPair chainPair = new BuChainPair ();
				chainPair.asymChain = chainRow["AsymID"].ToString ().Trim ();
				if (asymChainList != null)
				{
					if (asymChainList.Contains (chainPair.asymChain))
					{
						asymChainList.Remove (chainPair.asymChain);
					}
				}
				chainPair.pqsChain = chainRow["PqsChainID"].ToString ().Trim ();
				chainPair.symOpStr = chainRow["FullSymString"].ToString ().Trim ();
				chainPair.authChain = chainRow["PdbChainID"].ToString ().Trim ();
				chainPair.symOpNum = Convert.ToInt32 (chainRow["SymOpNum"].ToString ());
				chainPair.entityId = Convert.ToInt32 (chainRow["EntityID"].ToString ());
				buMatch.AddChainPair (chainPair);
			}
			// match left PDB AsymChain, in case PDB BU is larger than PQS
			// blank PQS chains
			if (asymChainList != null)
			{
				foreach (string chain in asymChainList)
				{
					foreach (DataRow pdbRow in pdbBuRows)
					{
						if (pdbRow["AsymID"].ToString ().Trim () == chain)
						{
							BuChainPair chainPair = new BuChainPair ();
							chainPair.asymChain = chain;
							chainPair.pqsChain = "-1";
							chainPair.symOpStr = "-";
							chainPair.authChain = pdbRow["AuthorChain"].ToString ().Trim ();
							chainPair.entityId = Convert.ToInt32 (pdbRow["EntityID"].ToString ());
							buMatch.AddChainPair (chainPair);
						}
					}
				}
			}
			
			// set the PQS chain format for BU
			string pqsBuAsym = buMatch.pqsBuAsym;
			string pqsBuPqs = "";
			foreach (char asymId in pqsBuAsym)
			{
				string pqsChains = "";
				if (Char.IsLetter (asymId))
				{
					foreach (BuChainPair chainPair in buMatch.ChainPairList)
					{
						if (chainPair.asymChain == asymId.ToString ())
						{
							pqsChains += chainPair.pqsChain;
						}
					}
					pqsBuPqs += pqsChains;
				}
				else if (! Char.IsDigit (asymId))
				{
					pqsBuPqs += asymId.ToString ();
				}
			}
			buMatch.pqsBuPqs = RemoveRedundantCommonInBu ( pqsBuPqs );
			buMatchList.Add (buMatch);
		}

		/// <summary>
		/// remove redundant ' in the biological unit string
		/// when write bu in PQS chains
		/// </summary>
		/// <param name="buString"></param>
		/// <returns></returns>
		private string RemoveRedundantCommonInBu (string buString)
		{
			string newBuString = "(";
			for(int i = 1; i < buString.Length - 1; i ++)
			{
				if (buString[i] == ',')
				{
					if (char.IsLetter (buString[i - 1]) && char.IsLetter (buString[i + 1]) )
					{
						newBuString += buString[i];
					}
				}
				else
				{
					newBuString += buString[i];
				}
			}
			newBuString += ")";
			return newBuString;
		}

		
		/// <summary>
		/// query string of PQS ASU/BU for a list of PDB entries
		/// </summary>
		/// <param name="pdbIdList"></param>
		/// <returns></returns>
		private DataTable[] GetChainSymOp(string pdbId)
		{
			string pqsQueryString = string.Format ("SELECT distinct PqsPdbChainMap.*, AsymID, EntityID FROM PqsPdbChainMap, AsymUnit " + 
				" WHERE AsymUnit.PdbID = '{0}'" + 
				" and AsymUnit.PdbID = PqsPdbChainMap.PdbID and PqsPdbChainMap.PdbChainID = AsymUnit.AuthorChain " + 
				" and AsymUnit.PolymerType = 'polypeptide';", pdbId);
			string pdbQueryString = string.Format ("SELECT AsymUnit.PdbID, BiolUnitID, AsymUnit.AsymID, AuthorChain, EntityID" + 
				" From AsymUnit, BiolUnit " + 
				" WHERE AsymUnit.PdbID = '{0}' AND AsymUnit.PdbID = BiolUnit.PdbID " +
				" AND AsymUnit.AsymID = BiolUnit.AsymID AND PolymerType = 'polypeptide';", pdbId);
				
			DataTable[] buChainsTables = new DataTable [2];	
			buChainsTables[(int)BuInterfaceDbBuilder.DataType.PDB] = BuInterfaceDbBuilder.dbQuery.Query(pdbQueryString);																  
			buChainsTables[(int)BuInterfaceDbBuilder.DataType.PQS] = BuInterfaceDbBuilder.dbQuery.Query(pqsQueryString);
			return buChainsTables;
		}
		#endregion

		#region entries
		/// <summary>
		/// Get PDB entry list
		/// </summary>
		private string[] GetProteinEntries (bool isUpdate)
		{
			string[] entryList = null;
			try
			{	
				if (! isUpdate)
				{
					string queryString = string.Format ("SELECT Distinct PdbID From AsymUnit " + 
						" WHERE AsymUnit.PolymerType = 'polypeptide'");
					DataTable entryTable = BuInterfaceDbBuilder.dbQuery.Query (queryString);

					entryList = new string[entryTable.Rows.Count];
					for (int i = 0; i < entryTable.Rows.Count; i ++)
					{
						entryList[i] = entryTable.Rows[i]["PdbID"].ToString ();
					}
				}
				else
				{
					// the new list of PDB entries
					string newPdblsFile = Path.Combine (AppSettings.dirSettings.xmlPath, "newls-pdb.txt");
					// protbud change the newls-pdb by appending the date
					// should find the latest file
					if (! File.Exists (newPdblsFile))
					{
						string[] pdbLsFiles = Directory.GetFiles (AppSettings.dirSettings.xmlPath, "newls-pdb*.txt");
						Array.Sort (pdbLsFiles);
						newPdblsFile = Path.Combine (AppSettings.dirSettings.xmlPath, pdbLsFiles[pdbLsFiles.Length - 1]);
					}
					
					// the new list of pqs entries
					string newPqslsFile = Path.Combine (AppSettings.dirSettings.pqsBuPath, "newls-pqs.txt");
			
					ArrayList newEntryList = new ArrayList ();
					StreamReader fileReader = null;
					if (File.Exists (newPdblsFile))
					{
						fileReader = new StreamReader (newPdblsFile);
						string line = "";
						string pdbId = "";
						while ((line = fileReader.ReadLine ()) != null)
						{
							pdbId = line.Substring (0, 4);
							if (! newEntryList.Contains (pdbId))
							{
								newEntryList.Add (pdbId);
							}
						}
						fileReader.Close ();
					}
					if (File.Exists (newPqslsFile))
					{
						string line = "";
						string pdbId = "";
						fileReader = new StreamReader (newPqslsFile);
						while ((line = fileReader.ReadLine ()) != null)
						{
							pdbId = line.Substring (0, 4);
							if (! newEntryList.Contains (pdbId))
							{
								newEntryList.Add (pdbId);
							}
						}
						fileReader.Close ();
					}
					entryList = new string [newEntryList.Count];
					newEntryList.CopyTo (entryList);
				}
			}
			catch(Exception ex)
			{
				throw ex;
			}

			return entryList;
		}

#if DEBUG
        private string[] GetProteinEntriesFromLog()
        {
            BuChainMatchCategory buMatchCat = new BuChainMatchCategory();
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(BuChainMatchCategory));
            FileStream xmlFileStream = new FileStream("PdbPqsBu_sameSubEntity.xml", FileMode.Open);
            buMatchCat = (BuChainMatchCategory)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            ArrayList updatePdbList = new ArrayList();
            foreach (BuChainMatch buFile in buMatchCat.BuChainMatchList)
            {
                if (!updatePdbList.Contains(buFile.pdbId))
                {
                    updatePdbList.Add(buFile.pdbId);
                }
            }
            string[] updateEntries = new string[updatePdbList.Count];
            updatePdbList.CopyTo(updateEntries);
            return updateEntries;
        }
#endif
		#endregion
	}
}
