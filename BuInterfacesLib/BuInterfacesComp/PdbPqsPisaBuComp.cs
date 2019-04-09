using System;
using System.IO;
using System.Collections;
using System.Data;
using XtalLib.Settings;
using XtalLib.Crystal;
using XtalLib.BuIO;
using XtalLib.FileParser;
using XtalLib.ProtInterfaces;
using XtalLib.StructureComp;
using DbLib;
using AuxFuncLib;
using ProgressLib;
using BuInterfacesLib.BuMatch;
using BuInterfacesLib.BuInterfaces;

namespace BuInterfacesLib.BuInterfacesComp
{
	/// <summary>
	/// Compare RCSB and PQS biological units of same entry
	/// </summary>
	public class PdbPqsPisaBuComp 
	{
		#region member varialbes
		private string pdbBuPath = "";
		private string pqsBuPath = "";

#if DEBUG
		StreamWriter buWriter = new StreamWriter (AppSettings.applicationStartPath + "\\EntryBUComp\\PdbPqsPisaBusComp.txt");
		StreamWriter logWriter = new StreamWriter (AppSettings.applicationStartPath + "\\EntryBUComp\\log.txt");
#endif
		string line = "";
		private StreamWriter buCompWriter = null;
		private StreamWriter interfaceWriter = null;
		private BuComp buComp = new BuComp ();
		private InterfacesInBu buInterfaces = new InterfacesInBu ();
		#endregion

		#region constructors
		public PdbPqsPisaBuComp()
		{
		}
		
		public PdbPqsPisaBuComp (string pdbPath, string pqsPath)
		{
			pdbBuPath = pdbPath;
			pqsBuPath = pqsPath;
		}
		#endregion

		#region initialize
		/// <summary>
		/// initialize table in DB and memory tables
		/// </summary>
		/// <param name="isUpdate"></param>
		private void InitializeTables (bool isUpdate)
		{
			BuCompTables.InitializeTables ();

			/*	if (! isUpdate)
				{
					// create table structures in the database
					DbCreator dbCreate = new DbCreator ();
					dbCreate.CreateTablesFromFile (AppSettings.applicationStartPath + "\\dbSchema\\interfaceDbSchema.txt");
					buCompWriter = new StreamWriter (AppSettings.applicationStartPath + "\\EntryBUComp\\bucomp");
					interfaceWriter = new StreamWriter (AppSettings.applicationStartPath + "\\EntryBUComp\\buinterfaces");
			
			}
				else
				{*/
			buCompWriter = new StreamWriter (AppSettings.applicationStartPath + "\\EntryBUComp\\newbucomp");
			interfaceWriter = new StreamWriter (AppSettings.applicationStartPath + "\\EntryBUComp\\newbuinterfacecomp");
			//	}
		}

		/// <summary>
		/// clear data in memory tables
		/// </summary>
		private void ClearTables ()
		{
			foreach (DataTable dataTable in BuCompTables.buCompTables)
			{
				dataTable.Clear ();
			}
		}
		#endregion

		#region compare entry BUs from PDB/PQS/PISA
		/// <summary>
		/// compare PDB and PQS biological untis
		/// biolgoical units are from PDB biological unit files
		/// and PQS mmol files
		/// </summary>
		/// <param name="buFiles"></param>
		public string[] CompareBiolUnits (BiolUnitMatch[] buFiles, string dbPath, string paramFile, bool isUpdate)
		{
			DbBuilder.dbConnect.dbConnection = null;
			DbBuilder.dbConnect.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE=" + 
				dbPath;			
			DbBuilder.dbConnect.ConnectToDatabase ();
			InitializeTables (isUpdate);
			if (isUpdate)
			{
				ArrayList updatePdbList = new ArrayList ();
				foreach (BuChainMatch buFile in buFiles)
				{
					if (! updatePdbList.Contains (buFile.pdbId))
					{
						updatePdbList.Add (buFile.pdbId);
					}
				}
				string[] updatePdbIDs = new string [updatePdbList.Count];
				updatePdbList.CopyTo (updatePdbIDs);
				DeleteObsDbEntries (updatePdbIDs);
			}

			string line = "PdbID   BuID1   BuID2   InterfaceID1		InterfaceID2	QScore	CompType";
			interfaceWriter.WriteLine (line);
#if DEBUG
			line = "PdbID	BuID1	BuID2	AsymBu1	AsymBu2	EntityBu1	EntityBu2	AuthBu1	AuthBu2	PqsBuPqs	InterfaceNum1	InterfaceNum2	IsSame	CompType";
			buWriter.WriteLine (line);
#endif
			//PdbBuFileParser pdbBuParser = new PdbBuFileParser ();
			PdbBuGenerator pdbBuBuilder = new PdbBuGenerator ();
			PqsBuFileParser pqsBuParser = new PqsBuFileParser ();
			PisaBuGenerator pisaBuBuilder = new PisaBuGenerator ();
			ArrayList pdbIdList = new ArrayList ();

			DbInsert dbInsert = new DbInsert ();
			BuInterfaceDbBuilder.progressInfo.totalOperationNum = buFiles.Length;
			BuInterfaceDbBuilder.progressInfo.totalStepNum = buFiles.Length;

			if (! Directory.Exists (AppSettings.tempDir))
			{
				Directory.CreateDirectory (AppSettings.tempDir);
			}	
			
			foreach (BuChainMatch buFile in buFiles)
			{
				BuInterfaceDbBuilder.progressInfo.currentFileName = buFile.pdbId;
				BuInterfaceDbBuilder.progressInfo.currentOperationNum ++;
				BuInterfaceDbBuilder.progressInfo.currentStepNum ++;

             /*   if (buFile.pdbId != "2e0z")
                {
                    continue;
                }*/
				
				if (isUpdate)
				{
					if (! pdbIdList.Contains (buFile.pdbId))
					{
						pdbIdList.Add (buFile.pdbId);
					}
				}
		                
				Hashtable[] biolUnits = new Hashtable [3];
                try
                {
                    biolUnits[(int)BuInterfaceDbBuilder.DataType.PDB] = GetPdbBiolUnit(buFile, pdbBuBuilder);
                }
                catch (Exception ex)
                {
                    BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue("Retrieving PDB biological units errors: " + ex.Message);
                }
                try
                {
                    biolUnits[(int)BuInterfaceDbBuilder.DataType.PQS] =
                        GetPqsBiolUnit(buFile, pqsBuParser, biolUnits[(int)BuInterfaceDbBuilder.DataType.PDB]);
                }
                catch (Exception ex)
                {
                    BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue("Retrieving PQS biological units errors: " + ex.Message);
                }
                try
                {
                    biolUnits[(int)BuInterfaceDbBuilder.DataType.PISA] = GetPisaBiolUnit(buFile, pisaBuBuilder);
                }
                catch (Exception ex)
                {
                    BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue("Retrieving PISA biological units errors: " + ex.Message);
                }

				
				// otherwise, compare PDB and PQS BUs
				try
				{
					// interfaces number in PDB BU
					int[] interfaceNums = null;
					int[] compResults = new int [3];
					SetCompResults (buFile, ref compResults);
					buComp.CompareEntryBiolUnits (biolUnits, buFile, out interfaceNums, ref compResults);
					
					AddDataToTables (buFile, interfaceNums, compResults);

					// insert data into database
					try
					{
						//	UpdateBuCompTable ();
						dbInsert.InsertDataIntoDBtables (BuCompTables.buCompTables);
					}
					catch (Exception ex)
					{
						throw ex;
					}
					finally
					{
						// clear memory data tables for next entry
						ClearTables();
					}
				}
				catch (Exception ex)
				{
					string errorMsg = buFile.pdbId + "	" + buFile.pdbBuId + "	" + buFile.pqsBuId + "	" + buFile.pisaBuId + ":" + ex.Message;
					BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue ("Comparing BUs errors: " + errorMsg);
#if DEBUG
					logWriter.WriteLine (errorMsg);
#endif
				}
			} 
			try
			{	
				// add left data into db
				dbInsert.InsertDataIntoDBtables (BuCompTables.buCompTables);
				DbBuilder.dbConnect.DisconnectFromDatabase (); 
			}
			catch {}

#if DEBUG
			buWriter.Close ();
			logWriter.Close ();
#endif
			buCompWriter.Close ();
			interfaceWriter.Close ();
            // Copy and compress the result files
            if (AppSettings.dirSettings == null)
            {
                AppSettings.LoadDirSettings();
            }
            File.Copy(AppSettings.applicationStartPath + "\\EntryBUComp\\newbucomp", AppSettings.dirSettings.piscesPath + "\\newbucomp");
            ParseHelper.ZipPdbFile(AppSettings.dirSettings.piscesPath + "\\newbucomp");
            File.Copy(AppSettings.applicationStartPath + "\\EntryBUComp\\newbuinterfacecomp",
                AppSettings.dirSettings.piscesPath + "\\newbuinterfacecomp");
            ParseHelper.ZipPdbFile(AppSettings.dirSettings.piscesPath + "\\newbuinterfacecomp");
			try
			{
				Directory.Delete (AppSettings.tempDir, true);
			}
			catch {} 

			if (pdbIdList.Count == 0)
			{
				return null;
			}
			string[] pdbIds = new string [pdbIdList.Count];
			pdbIdList.CopyTo(pdbIds);
			return pdbIds;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="compResults"></param>
		private void SetCompResults (BuChainMatch buMatch, ref int[] compResults)
		{
			string queryString = string.Format ("Select * From PdbPqsBuComp " + 
				" Where PdbID = '{0}' AND PdbBuID = '{1}' AND PqsBUID = '{2}';", 
				buMatch.pdbId, buMatch.pdbBuId, buMatch.pqsBuId);
			DataTable pdbPqsBuCompTable = BuInterfaceDbBuilder.dbQuery.Query (queryString);
			if (pdbPqsBuCompTable.Rows.Count == 0)
			{
				compResults[(int)BuInterfaceDbBuilder.CompType.PDBPQS] = -1; // not in the db
			}
			else
			{
				if (Convert.ToInt32 (pdbPqsBuCompTable.Rows[0]["IsSame"].ToString ()) == 1 &&
					Convert.ToInt32 (pdbPqsBuCompTable.Rows[0]["PdbInterfaceNum"].ToString ()) == 
					Convert.ToInt32 (pdbPqsBuCompTable.Rows[0]["PqsInterfaceNum"].ToString ()))
				{
					compResults[(int)BuInterfaceDbBuilder.CompType.PDBPQS] = 0; // same BUs
				}
				else
				{
					compResults[(int)BuInterfaceDbBuilder.CompType.PDBPQS] = 1; // dif BUs
				}
			}
			compResults[(int)BuInterfaceDbBuilder.CompType.PISAPDB] = -1;
			compResults[(int)BuInterfaceDbBuilder.CompType.PISAPQS] = -1;
		}

		#region biological units
		/// <summary>
		/// update the sequence id to be author chain id
		/// so that can compare PDB and PQS biological units
		/// based on sequence id
		/// </summary>
		/// <param name="chainAtomList"></param>
		private void UpdateSeqIdToAuthSeqId (ref AtomInfo[] chainAtomList)
		{
			foreach (AtomInfo atom in chainAtomList)
			{
				atom.seqId = atom.authSeqId;
			}
		}

		/// <summary>
		/// update sequence number of a residue to author sequence number,
		/// so that the numbers are same as PQS when compared
		/// From PQS file, the sequence numbers can not be derived
		/// </summary>
		/// <param name="buChains"></param>
		private void UpdateAtomSeqIds (ref Hashtable buChains)
		{
			foreach (string chainSym in buChains.Keys)
			{
				AtomInfo[] chainAtoms = (AtomInfo[]) buChains[chainSym]; 
				UpdateSeqIdToAuthSeqId (ref chainAtoms);
			}
		}
		#region PDB Biol Unit
		/// <summary>
		/// 
		/// </summary>
		/// <param name="buFile"></param>
		/// <param name="pdbBuBuilder"></param>
		/// <returns></returns>
		private Hashtable GetPdbBiolUnit (BuChainMatch buFile, PdbBuGenerator pdbBuBuilder)
		{
			if (buFile.pdbBuId == "-1")
			{
				return null;
			}
			string pdbFile = "";
			pdbFile = pdbBuPath + "\\" + buFile.pdbId + ".xml.gz";
			if (! File.Exists (pdbFile))
			{
				BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue (string.Format ("PDB file {0} not exist. ", pdbFile));
#if DEBUG
				logWriter.WriteLine (string.Format ("PDB file {0} not exist. ", pdbFile));
#endif
				return null;
			}
			// key: chainid
			// value: the list of atoms with specified atom type
			Hashtable pdbBuChainsHash = null;
			
			string unzippedXmlFile = ParseHelper.UnZipFile (pdbFile, AppSettings.tempDir);
			try
			{
		//		pdbBuChainsHash = pdbBuBuilder.BuildPdbBUForPqsComp (unzippedXmlFile, buFile.pdbBuId.ToString (), 
		//			AppSettings.parameters.contactParams.atomType);
			}
			catch (Exception ex)
			{
				string errorMsg = string.Format ("Errors in building PDB biological unit: {0}_{1}.", 
					buFile.pdbId, buFile.pdbBuId);
				BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue (errorMsg);
				BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue (ex.Message);
#if DEBUG
				logWriter.WriteLine (ex.Message);
#endif
				return null;;
			}
			File.Delete (unzippedXmlFile);
			UpdateAtomSeqIds (ref pdbBuChainsHash);
			return pdbBuChainsHash;
		}
		#endregion

		#region PQS Biol Unit
		/// <summary>
		/// Retrieve Biological Unit From PQS
		/// </summary>
		/// <param name="buFile"></param>
		/// <param name="pqsBuParser"></param>
		/// <param name="pdbBuChainsHash"></param>
		/// <returns></returns>
		private Hashtable GetPqsBiolUnit (BuChainMatch buFile, PqsBuFileParser pqsBuParser, Hashtable pdbBuChainsHash)
		{
			if (buFile.pqsBuId == "-1")
			{
				return null;
			}
			string pqsFile = "";
			if (buFile.pqsBuId == "1")
			{
				pqsFile = pqsBuPath + "\\" + buFile.pdbId + ".mmol.gz";
				if (! File.Exists (pqsFile))
				{
					pqsFile = pqsBuPath + "\\" + buFile.pdbId + "_" + buFile.pqsBuId + ".mmol.gz";
                    if (! File.Exists(pqsFile))
                    {
                        pqsFile = pqsBuPath + "\\" + buFile.pdbId + ".water.gz";
                    }
				}
			}
			else
			{
				pqsFile = pqsBuPath + "\\" + buFile.pdbId + "_" + buFile.pqsBuId + ".mmol.gz";
			}
			if (! File.Exists (pqsFile))
			{
				BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue (string.Format ("PQS file {0} not exist. ", pqsFile));
#if DEBUG
				logWriter.WriteLine (string.Format ("PQS file {0} not exist. ", pqsFile));
#endif
				return null;
			}
			string[] pqsChains = null;
			if (pdbBuChainsHash != null)
			{
				pqsChains = FindPqsChains (pdbBuChainsHash, buFile);
			}
			else
			{
				pqsChains = FindPqsChains (buFile);
			}
			string unzippedPqsFile = ParseHelper.UnZipFile (pqsFile, AppSettings.tempDir);
			Hashtable pqsBuChainsHash = pqsBuParser.ParsePqsFile (unzippedPqsFile, 
				AppSettings.parameters.contactParams.atomType, pqsChains);

			File.Delete (unzippedPqsFile);
			return pqsBuChainsHash;
		}
		/// <summary>
		/// a list of corresponding PQS chains
		/// </summary>
		/// <param name="pdbBuHash"></param>
		/// <param name="buFile"></param>
		/// <returns></returns>
		private string[] FindPqsChains (Hashtable pdbBuHash, BuChainMatch buFile)
		{
			ArrayList pdbChainList = new ArrayList ();
			foreach (object keyString in pdbBuHash.Keys)
			{
				string pdbChain = "";
				int chainIndex = keyString.ToString ().IndexOf ("_");				
				pdbChain = keyString.ToString ().Substring (0, chainIndex);
				pdbChainList.Add (pdbChain);
			}
			ArrayList pqsChains = new ArrayList ();
			foreach (string pdbChain in pdbChainList)
			{
				foreach (BuChainPair buPair in buFile.ChainPairList)
				{
					string pairPdbChain = buPair.asymChain;
					int chainIndex = pairPdbChain.IndexOf ("_");
					if (chainIndex > -1)
					{
						pairPdbChain = pairPdbChain.Substring (0, pairPdbChain.IndexOf ("_"));
					}
					if (pairPdbChain == pdbChain)
					{
						if (! pqsChains.Contains (buPair.pqsChain))
						{
							pqsChains.Add (buPair.pqsChain);
						}
					}
				}
			}
			pqsChains.Sort ();
			string[] pqsStrChains = new string [pqsChains.Count];
			pqsChains.CopyTo (pqsStrChains);
			return pqsStrChains;
		}

		/// <summary>
		/// a list of corresponding PQS chains
		/// </summary>
		/// <param name="pdbBuHash"></param>
		/// <param name="buFile"></param>
		/// <returns></returns>
		private string[] FindPqsChains (BuChainMatch buFile)
		{
			ArrayList pqsChains = new ArrayList ();
			
			foreach (BuChainPair buPair in buFile.ChainPairList)
			{				
				if (! pqsChains.Contains (buPair.pqsChain))
				{
					pqsChains.Add (buPair.pqsChain);
				}				
			}
			pqsChains.Sort ();
			string[] pqsStrChains = new string [pqsChains.Count];
			pqsChains.CopyTo (pqsStrChains);
			return pqsStrChains;
		}
		#endregion

		#region PISA Biol Unit
		/// <summary>
		/// 
		/// </summary>
		/// <param name="buFile"></param>
		/// <param name="pdbBuBuilder"></param>
		/// <returns></returns>
		private Hashtable GetPisaBiolUnit (BuChainMatch buFile, PisaBuGenerator pisaBuBuilder)
		{
			if (buFile.pisaBuId == "-1")
			{
				return null;
			}
			string coordXmlFile = pdbBuPath + "\\" + buFile.pdbId + ".xml.gz";
			if (! File.Exists (coordXmlFile))
			{
				BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue (string.Format ("PDB file {0} not exist. ", coordXmlFile));
#if DEBUG
				logWriter.WriteLine (string.Format ("PDB file {0} not exist. ", coordXmlFile));
#endif
				return null;
			}
			// key: chainid
			// value: the list of atoms with specified atom type
			Hashtable pisaBuChainsHash = null;
			
			string unzippedXmlFile = ParseHelper.UnZipFile (coordXmlFile, AppSettings.tempDir);
			try
			{
				pisaBuChainsHash = pisaBuBuilder.BuildOnePisaAssembly (unzippedXmlFile, buFile.pdbId, buFile.pisaBuId, EntryBuComp.pisaBuMatrixTable);
			}
			catch (Exception ex)
			{
				string errorMsg = string.Format ("Errors in building PISA biological unit: {0}_{1}.", 
					buFile.pdbId, buFile.pdbBuId);
				BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue (errorMsg);
				BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue (ex.Message);
#if DEBUG
				logWriter.WriteLine (ex.Message);
#endif
				return null;;
			}
			File.Delete (unzippedXmlFile);
			
			UpdateAtomSeqIds (ref pisaBuChainsHash);
			return pisaBuChainsHash;
		}
		#endregion
		#endregion

		#region add data to tables
		/// <summary>
		/// add data into table
		/// </summary>
		/// <param name="buFile"></param>
		/// <param name="pdbNum"></param>
		/// <param name="pqsNum"></param>
		/// <param name="isSame"></param>
		private void AddDataToTables (BuChainMatch buFile, int[] interfaceNums, int[] compResults)
		{
			if (compResults[(int)BuInterfaceDbBuilder.CompType.PDBPQS] > -1)
			{
				AddDataToCompTable (buFile, interfaceNums[(int)BuInterfaceDbBuilder.DataType.PDB],
					interfaceNums[(int)BuInterfaceDbBuilder.DataType.PQS], 
					compResults[(int)BuInterfaceDbBuilder.CompType.PDBPQS], 
					(int)BuInterfaceDbBuilder.CompType.PDBPQS);
				AddDataToInterfaceCompTable (buFile, 
					buComp.compPairInfos[(int)BuInterfaceDbBuilder.CompType.PDBPQS], 
					(int)BuInterfaceDbBuilder.CompType.PDBPQS);
			}

			if (compResults[(int)BuInterfaceDbBuilder.CompType.PISAPDB] > -1)
			{
				AddDataToCompTable (buFile, interfaceNums[(int)BuInterfaceDbBuilder.DataType.PISA],
					interfaceNums[(int)BuInterfaceDbBuilder.DataType.PDB], 
					compResults[(int)BuInterfaceDbBuilder.CompType.PISAPDB], 
					(int)BuInterfaceDbBuilder.CompType.PISAPDB);
				AddDataToInterfaceCompTable (buFile, 
					buComp.compPairInfos[(int)BuInterfaceDbBuilder.CompType.PISAPDB], 
					(int)BuInterfaceDbBuilder.CompType.PISAPDB);
			}

			if (compResults[(int)BuInterfaceDbBuilder.CompType.PISAPQS] > -1)
			{
				AddDataToCompTable (buFile, interfaceNums[(int)BuInterfaceDbBuilder.DataType.PISA],
					interfaceNums[(int)BuInterfaceDbBuilder.DataType.PQS], 
					compResults[(int)BuInterfaceDbBuilder.CompType.PISAPQS], 
					(int)BuInterfaceDbBuilder.CompType.PISAPQS);
				AddDataToInterfaceCompTable (buFile, 
					buComp.compPairInfos[(int)BuInterfaceDbBuilder.CompType.PISAPQS], 
					(int)BuInterfaceDbBuilder.CompType.PISAPQS);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="buFile"></param>
		/// <param name="interfaceNum1"></param>
		/// <param name="interfaceNum2"></param>
		/// <param name="compResult"></param>
		/// <param name="type"></param>
		private void AddDataToCompTable (BuChainMatch buFile, int interfaceNum1, int interfaceNum2, 
			int compResult, int type)
		{
			string line = "";
			string compType = "";
			int isSame = 0;
			if (compResult == 0)
			{
				isSame = 1;
			}
			switch (type)
			{
				case (int)BuInterfaceDbBuilder.CompType.PDBPQS:
					DataRow pdbPqsBuCompRow = BuCompTables.buCompTables[BuCompTables.PdbPqsBUsComp].NewRow ();
					pdbPqsBuCompRow["PdbID"] = buFile.pdbId;
					pdbPqsBuCompRow["PdbBuID"] = buFile.pdbBuId;
					pdbPqsBuCompRow["PqsBuID"] = buFile.pqsBuId;
					pdbPqsBuCompRow["PdbInterfaceNum"] = interfaceNum1;
					pdbPqsBuCompRow["PqsInterfaceNum"] = interfaceNum2;
					pdbPqsBuCompRow["IsSame"] = isSame;
					BuCompTables.buCompTables[BuCompTables.PdbPqsBUsComp].Rows.Add (pdbPqsBuCompRow);

					line = buFile.pdbId + "   " + buFile.pdbBuId.ToString () + "   " + buFile.pqsBuId.ToString () + "   ";
					compType = "pdbpqs";
					break;

				case (int)BuInterfaceDbBuilder.CompType.PISAPDB:
					DataRow pisapdbBuCompRow = BuCompTables.buCompTables[BuCompTables.PisaPdbBUsComp].NewRow ();
					pisapdbBuCompRow["PdbID"] = buFile.pdbId;
					pisapdbBuCompRow["PisaBuID"] = buFile.pisaBuId;
					pisapdbBuCompRow["PdbBuID"] = buFile.pdbBuId;
					pisapdbBuCompRow["PisaInterfaceNum"] = interfaceNum1;
					pisapdbBuCompRow["PdbInterfaceNum"] = interfaceNum2;
					pisapdbBuCompRow["IsSame"] = isSame;
					BuCompTables.buCompTables[BuCompTables.PisaPdbBUsComp].Rows.Add (pisapdbBuCompRow);

					line = buFile.pdbId + "   " + buFile.pisaBuId.ToString () + "   " + buFile.pdbBuId.ToString () + "   ";
					compType = "pisapdb";
					break;

				case (int)BuInterfaceDbBuilder.CompType.PISAPQS:
					DataRow pisaPqsBuCompRow = BuCompTables.buCompTables[BuCompTables.PisaPqsBUsComp].NewRow ();
					pisaPqsBuCompRow["PdbID"] = buFile.pdbId;
					pisaPqsBuCompRow["PisaBuID"] = buFile.pisaBuId;
					pisaPqsBuCompRow["PqsBuID"] = buFile.pqsBuId;
					pisaPqsBuCompRow["PisaInterfaceNum"] = interfaceNum1;
					pisaPqsBuCompRow["PqsInterfaceNum"] = interfaceNum2;
					pisaPqsBuCompRow["IsSame"] = isSame;
					BuCompTables.buCompTables[BuCompTables.PisaPqsBUsComp].Rows.Add (pisaPqsBuCompRow);

					line = buFile.pdbId + "   " + buFile.pisaBuId.ToString () + "   " + buFile.pqsBuId.ToString () + "   ";
					compType = "pisapqs";
					break;

				default:
					break;
			}
			line += (interfaceNum1.ToString () + "   " + interfaceNum2.ToString () + "   " + isSame.ToString () + "   " + compType);
			buCompWriter.WriteLine (line);
			buCompWriter.Flush ();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="buFile"></param>
		/// <param name="compPairs"></param>
		/// <param name="type"></param>
		private void AddDataToInterfaceCompTable (BuChainMatch buFile, InterfacePairInfo[] compPairs, int type)
		{
			if (compPairs == null)
			{
				return;
			}
			switch (type)
			{
				case (int)BuInterfaceDbBuilder.CompType.PDBPQS:
					foreach (InterfacePairInfo pairInfo in compPairs)
					{
						if (pairInfo.qScore < AppSettings.parameters.contactParams.minQScore)
						{
							continue;
						}
						DataRow pdbPqsInterfaceCompRow = BuCompTables.buCompTables[BuCompTables.PdbPqsBuInterfaceComp].NewRow ();
						pdbPqsInterfaceCompRow["PdbID"] = buFile.pdbId;
						pdbPqsInterfaceCompRow["PdbBuID"] = buFile.pdbBuId;
						pdbPqsInterfaceCompRow["PqsBuID"] = buFile.pqsBuId;
						// symopstring is actually a chain id in the file
						pdbPqsInterfaceCompRow["PdbInterfaceID"] = pairInfo.interfaceInfo1.interfaceId;
						pdbPqsInterfaceCompRow["PqsInterfaceID"] = pairInfo.interfaceInfo2.interfaceId;
						pdbPqsInterfaceCompRow["QScore"] = pairInfo.qScore;
						BuCompTables.buCompTables[BuCompTables.PdbPqsBuInterfaceComp].Rows.Add (pdbPqsInterfaceCompRow);

						line = buFile.pdbId + "   " + buFile.pdbBuId.ToString () + "   " + buFile.pqsBuId.ToString () + "   ";
						line += pairInfo.interfaceInfo1.interfaceId;
						line += "   ";
						line += pairInfo.interfaceInfo2.interfaceId;
						line += "   ";
						line += pairInfo.qScore.ToString ();
						line += "   ";
						line += "pdbpqs";
						interfaceWriter.WriteLine (line);
						interfaceWriter.Flush ();
					}
					break;

				case (int)BuInterfaceDbBuilder.CompType.PISAPDB:
					foreach (InterfacePairInfo pairInfo in compPairs)
					{
						DataRow pisaPdbInterfaceCompRow = BuCompTables.buCompTables[BuCompTables.PisaPdbBuInterfaceComp].NewRow ();
						pisaPdbInterfaceCompRow["PdbID"] = buFile.pdbId;
						pisaPdbInterfaceCompRow["PisaBuID"] = buFile.pisaBuId;
						pisaPdbInterfaceCompRow["PdbBuID"] = buFile.pdbBuId;
						// symopstring is actually a chain id in the file
						pisaPdbInterfaceCompRow["PisaInterfaceID"] = pairInfo.interfaceInfo1.interfaceId;
						pisaPdbInterfaceCompRow["PdbInterfaceID"] = pairInfo.interfaceInfo2.interfaceId;
						pisaPdbInterfaceCompRow["QScore"] = pairInfo.qScore;
						BuCompTables.buCompTables[BuCompTables.PisaPdbBuInterfaceComp].Rows.Add (pisaPdbInterfaceCompRow);

						line = buFile.pdbId + "   " + buFile.pdbBuId.ToString () + "   " + buFile.pqsBuId.ToString () + "   ";
						line += pairInfo.interfaceInfo1.interfaceId;
						line += "   ";
						line += pairInfo.interfaceInfo2.interfaceId;
						line += "   ";
						line += pairInfo.qScore.ToString ();
						line += "   ";
						line += "pisapdb";
						interfaceWriter.WriteLine (line);
						interfaceWriter.Flush ();
					}
					break;

				case (int)BuInterfaceDbBuilder.CompType.PISAPQS:
					foreach (InterfacePairInfo pairInfo in compPairs)
					{
						DataRow pisaPqsInterfaceCompRow = BuCompTables.buCompTables[BuCompTables.PisaPqsBuInterfaceComp].NewRow ();
						pisaPqsInterfaceCompRow["PdbID"] = buFile.pdbId;
						pisaPqsInterfaceCompRow["PisaBuID"] = buFile.pisaBuId;
						pisaPqsInterfaceCompRow["PqsBuID"] = buFile.pqsBuId;
						// symopstring is actually a chain id in the file
						pisaPqsInterfaceCompRow["PisaInterfaceID"] = pairInfo.interfaceInfo1.interfaceId;
						pisaPqsInterfaceCompRow["PqsInterfaceID"] = pairInfo.interfaceInfo2.interfaceId;
						pisaPqsInterfaceCompRow["QScore"] = pairInfo.qScore;
						BuCompTables.buCompTables[BuCompTables.PisaPqsBuInterfaceComp].Rows.Add (pisaPqsInterfaceCompRow);

						line = buFile.pdbId + "   " + buFile.pdbBuId.ToString () + "   " + buFile.pqsBuId.ToString () + "   ";
						line += pairInfo.interfaceInfo1.interfaceId;
						line += "   ";
						line += pairInfo.interfaceInfo2.interfaceId;
						line += "   ";
						line += pairInfo.qScore.ToString ();
						line += "   ";
						line += "pisapqs";
						interfaceWriter.WriteLine (line);
						interfaceWriter.Flush ();
					}
					break;

				default:
					break;
			}
		}
		#endregion
		#endregion

		#region delete DB obsolete entries
		/// <summary>
		/// delete old data from database
		/// </summary>
		private void DeleteObsDbEntries(string[] updatePdbList)
		{
			// remove the obsolete entries
			System.Data.Odbc.OdbcCommand deleteCommand = DbBuilder.dbConnect.CreateCommand ();
			string deleteString = "";
			foreach (DataTable dataTable in BuCompTables.buCompTables)
			{
				foreach (string pdbId in updatePdbList)
				{
					deleteString = string.Format ("Delete From {0} Where PdbID = '{1}';", dataTable.TableName, pdbId);
					deleteCommand.CommandText = deleteString;
					deleteCommand.ExecuteNonQuery ();
				}
			}
		}
		#endregion

	}
}
