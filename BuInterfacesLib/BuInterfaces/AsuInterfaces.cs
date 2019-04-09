using System;
using System.Collections;
using System.IO;
using System.Data;
using System.Xml.Serialization;
using XtalLib.KDops;
using XtalLib.Settings;
using XtalLib.Crystal;
using XtalLib.Contacts;
using DbLib;
using AuxFuncLib;
using ProgressLib;

namespace BuInterfacesLib.BuInterfaces
{
	/// <summary>
	/// Summary description for AsuInterfaces.
	/// </summary>
	public class AsuInterfaces
	{
		private DataTable asuInterfaceTable = null;
		private DbInsert dbInsert = new DbInsert ();

		public AsuInterfaces()
		{
		}

		/// <summary>
		/// Interfaces for each ASU file
		/// </summary>
		/// <param name="isUpdate"></param>
		public void GetAsuInterfacesFromXmlFiles (bool isUpdate)
		{
			if (AppSettings.dirSettings == null)
			{
				AppSettings.LoadDirSettings ();
			}
			if (AppSettings.parameters == null)
			{
				AppSettings.LoadParameters ();
			}
			BuInterfaceDbBuilder.progressInfo.Reset ();
			BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue ("Compute interfaces in asymmetric units...");

			DbBuilder.dbConnect.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE=" + 
				AppSettings.dirSettings.interfaceDbPath;
			DbBuilder.dbConnect.ConnectToDatabase ();

			string[] fileList = GetFileList (isUpdate);
			if (fileList == null)
			{
				if (isUpdate)
				{
					BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue ("No update needed (no newls.txt file).");
					return ;
				}
				else
				{
					BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue 
						("No XML files listed in the directory : " + AppSettings.dirSettings.coordXmlPath);
					return;
				}
			}
			// if isUpdate, clear old data in database
			if (isUpdate)
			{
				DeleteObseleteAsuDataInDb (fileList);
			}
			BuInterfaceDbBuilder.progressInfo.totalOperationNum = fileList.Length;
			BuInterfaceDbBuilder.progressInfo.totalStepNum = fileList.Length;
			BuInterfaceDbBuilder.progressInfo.currentOperationLabel = "ASU Interfaces";

			InitializeTable (isUpdate);
			foreach (string file in fileList)
			{
				BuInterfaceDbBuilder.progressInfo.currentOperationNum ++;
				BuInterfaceDbBuilder.progressInfo.currentStepNum ++;
				try
				{
					InterfaceChains[] asuInterfaces = 
						GetAsuInterfacesFromXml (Path.Combine (AppSettings.dirSettings.coordXmlPath, file));
					if (asuInterfaces == null || asuInterfaces.Length == 0)
					{
						continue;
					}
					string pdbId = file.Substring (file.LastIndexOf ("\\") + 1, 4);
					// Add interface data into data tables
					AddInterfaceInfoToTables (pdbId, asuInterfaces);
					InsertDataToDb ();
				}
				catch (Exception ex)
				{
					BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue 
						("Errors in computing ASU interfaces in " + file + ". " + ex.Message);
					continue;
				}
			}
			DbBuilder.dbConnect.DisconnectFromDatabase ();
		}

		/// <summary>
		/// interfaces from an asu file
		/// </summary>
		/// <param name="asuXmlFile"></param>
		public InterfaceChains[] GetAsuInterfacesFromXml (string asuXmlFile)
		{
			string coordXml = asuXmlFile;
			if (asuXmlFile.Substring (asuXmlFile.LastIndexOf (".") + 1, 2) == "gz")
			{
				coordXml = ParseHelper.UnZipFile (asuXmlFile, AppSettings.tempDir);
			}
			string pdbId = coordXml.Substring (coordXml.LastIndexOf ("\\") + 1, 4);

			BuInterfaceDbBuilder.progressInfo.currentFileName = pdbId;

			Hashtable asymUnit = GetAsuFromXml (coordXml);
			File.Delete (coordXml);
			// monomer, no need to compute the interfaces
			if (asymUnit.Count < 2)
			{
				return null;
			}
			return GetInterfacesInAsu (pdbId, asymUnit);			
	//		InsertDataToDb ();
			
		}
		/// <summary>
		/// retrieve the asu chains
		/// </summary>
		/// <param name="coordXml"></param>
		/// <returns></returns>
		private Hashtable GetAsuFromXml (string coordXml)
		{
			Hashtable asuChainsHash = new Hashtable ();
			// read data from crystal xml file
			EntryCrystal thisEntryCrystal;
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(EntryCrystal));
			FileStream xmlFileStream = new FileStream(coordXml, FileMode.Open);
			thisEntryCrystal = (EntryCrystal) xmlSerializer.Deserialize (xmlFileStream);
			xmlFileStream.Close ();
			ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
			foreach (ChainAtoms chain in chainAtomsList)
			{
				if (chain.PolymerType == "polypeptide")
				{
					if (! asuChainsHash.ContainsKey (chain.AsymChain + "_1_555"))
					{
						asuChainsHash.Add (chain.AsymChain + "_1_555", chain.CartnAtoms);
					}
				}
			}
			return asuChainsHash;
		}

		/// <summary>
		/// get the interfaces in an ASU
		/// </summary>
		/// <param name="asuChainsHash"></param>
		private InterfaceChains[] GetInterfacesInAsu (string pdbId, Hashtable asymUnit)
		{
			// build trees for the biological unit
			Hashtable asuChainTreesHash = BuildBVtreesForAsu (asymUnit);
			// calculate interfaces
			ArrayList interfaceList = new ArrayList ();
			ArrayList keyList = new ArrayList (asuChainTreesHash.Keys);			
			keyList.Sort ();

			int interChainId = 0;
			for (int i = 0; i < keyList.Count - 1; i ++)
			{
				for (int j = i + 1; j < keyList.Count; j ++)
				{
					ChainContact chainContact = new ChainContact (keyList[i].ToString (), keyList[j].ToString ());
					ChainContactInfo contactInfo = chainContact.GetChainContactInfo ((BVTree)asuChainTreesHash[keyList[i]], 
						(BVTree)asuChainTreesHash[keyList[j]]);
					if (contactInfo != null)
					{
						interChainId ++;
						
						InterfaceChains interfaceChains = new InterfaceChains (keyList[i].ToString (), keyList[j].ToString ());
						// no need to change the tree node data
						// only assign the refereces
						interfaceChains.chain1 = ((BVTree)asuChainTreesHash[keyList[i]]).Root.CalphaCbetaAtoms ();
						interfaceChains.chain2 = ((BVTree)asuChainTreesHash[keyList[j]]).Root.CalphaCbetaAtoms ();
						interfaceChains.interfaceId = interChainId;
						interfaceChains.seqDistHash = chainContact.ChainContactInfo.GetBbDistHash();
						interfaceList.Add (interfaceChains);	
						//chainContact = null;
					}
				}
			}						
			// return interface chains
			InterfaceChains[] asuInterfaces = new InterfaceChains [interfaceList.Count];
			interfaceList.CopyTo (asuInterfaces);
			return asuInterfaces;

		}
		/// <summary>
		/// organize data into data table
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="interfaceList"></param>
		private void AddInterfaceInfoToTables (string pdbId, InterfaceChains[] interfaceList)
		{
			foreach (InterfaceChains theInterface in interfaceList)
			{
				DataRow interfaceRow = asuInterfaceTable.NewRow ();
				interfaceRow["PdbID"] = pdbId;
				interfaceRow["InterfaceID"] = theInterface.interfaceId;
				int chainIndex = theInterface.firstSymOpString.IndexOf ("_");
				interfaceRow["Chain1"] = theInterface.firstSymOpString.Substring (0, chainIndex);
				interfaceRow["SymmetryString1"] = theInterface.firstSymOpString.Substring (chainIndex + 1, 
					theInterface.firstSymOpString.Length - chainIndex - 1);
				chainIndex = theInterface.secondSymOpString.IndexOf ("_");
				interfaceRow["Chain2"] = theInterface.secondSymOpString.Substring (0, chainIndex);
				interfaceRow["SymmetryString2"] = theInterface.secondSymOpString.Substring (chainIndex + 1, 
					theInterface.secondSymOpString.Length - chainIndex - 1);
				asuInterfaceTable.Rows.Add (interfaceRow);
			}
		}
		
		/// <summary>
		/// insert data into database
		/// </summary>
		private void InsertDataToDb ()
		{
			if (asuInterfaceTable.Rows.Count > 0)
			{
				dbInsert.InsertDataIntoDBtables (asuInterfaceTable);
				asuInterfaceTable.Clear ();
			}
		}
		/// <summary>
		/// build BVtrees for chains in a biological unit
		/// </summary>
		/// <param name="biolUnit"></param>
		/// <returns></returns>
		private Hashtable BuildBVtreesForAsu (Hashtable asymUnit)
		{
			Hashtable chainTreesHash = new Hashtable ();
			// for each chain in the biological unit
			// build BVtree
			foreach (object chainAndSymOp in asymUnit.Keys)
			{
				BVTree chainTree = new BVTree ();
				chainTree.BuildBVTree ((AtomInfo[])asymUnit[chainAndSymOp], AppSettings.parameters.kDopsParam.bvTreeMethod, true);
				chainTreesHash.Add (chainAndSymOp, chainTree);
			}
			return chainTreesHash;
		}

		/// <summary>
		/// initialize table in memory and database
		/// </summary>
		private void InitializeTable (bool isUpdate)
		{
			string[] asuColumns = {"PdbID", "InterfaceID", "Chain1", "SymmetryString1", "Chain2", "SymmetryString2"};
			asuInterfaceTable = new DataTable ("AsuInterfaces");
			foreach (string asuCol in asuColumns)
			{
				asuInterfaceTable.Columns.Add (new DataColumn (asuCol));
			}
			if (! isUpdate)
			{
				string tableCreateString = "CREATE TABLE AsuInterfaces (" + 
					" PdbID CHAR(4) NOT NULL, " + 
					" InterfaceID INTEGER NOT NULL, " + 
					" Chain1 CHAR(2) NOT NULL, " +
					" SymmetryString1 VARCHAR(30) NOT NULL, " + 
					" Chain2 CHAR(2) NOT NULL, " + 
					" SymmetryString2 VARCHAR(30) NOT NULL);";
				DbCreator dbCreate = new DbCreator ();
				dbCreate.CreateTableFromString (tableCreateString, "AsuInterfaces", true);
			}
		}

		/// <summary>
		/// delete obselete asu interfaces data
		/// </summary>
		/// <param name="fileList"></param>
		private void DeleteObseleteAsuDataInDb (string[] fileList)
		{
			string[] updateList = new string [fileList.Length];
			for (int i = 0; i < fileList.Length; i ++)
			{
				updateList[i] = fileList[i].Substring (fileList[i].LastIndexOf ("\\") + 1, 4);
			}
			System.Data.Odbc.OdbcCommand deleteCommand = DbBuilder.dbConnect.CreateCommand ();
			string deleteString = "";
			foreach (string pdbId in updateList)
			{
				deleteString = string.Format ("DELETE FROM ASUInterfaces Where PDBID = '{0}';", pdbId);
				deleteCommand.CommandText = deleteString;
				deleteCommand.ExecuteNonQuery ();
			}
		}
		/// <summary>
		/// get the file list needed to be parsed 
		/// </summary>
		/// <param name="isUpdate"></param>
		/// <returns></returns>
		private string[] GetFileList (bool isUpdate)
		{
			string[] files = null;
			if (! isUpdate)
			{
				files = Directory.GetFiles (AppSettings.dirSettings.coordXmlPath, "*.xml.gz");
			}
			else
			{
			/*	DbQuery dbQuery = new DbQuery ();
				string queryString = "Select Distinct PdbID FROM AsuInterfaces;";
				DataTable asuPdbTable = dbQuery.Query (queryString);
				string[] filesInDir = Directory.GetFiles (AppSettings.dirSettings.coordXmlPath, "*.xml.gz");
				ArrayList leftPdbList = new ArrayList ();
				foreach (string file in filesInDir)
				{
					string pdbId = file.Substring (file.LastIndexOf ("\\") + 1, 4);
					DataRow[] pdbRows = asuPdbTable.Select (string.Format ("PdbID = '{0}'", pdbId));
					if (pdbRows.Length == 0)
					{
						leftPdbList.Add (file);
					}
				}
				files = new string[leftPdbList.Count];
				leftPdbList.CopyTo (files);*/
				string newFile = Path.Combine (AppSettings.dirSettings.coordXmlPath, "newls.txt");
				if (File.Exists (newFile))
				{
					ArrayList fileList = new ArrayList ();
					StreamReader fileReader = new StreamReader (newFile);
					string line = "";
					while ((line = fileReader.ReadLine ()) != null)
					{
						fileList.Add (line);
					}
					fileReader.Close ();
					files = new string [fileList.Count];
					fileList.CopyTo (files);
				}
			}
			return files;
		} 
		// end of functions
	}
}
