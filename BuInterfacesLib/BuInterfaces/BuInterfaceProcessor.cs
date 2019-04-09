using System;
using System.IO;
using System.Data;
using System.Collections;
using System.Xml.Serialization;
using XtalLib.Settings;
using XtalLib.Crystal;
using XtalLib.BuIO;
using XtalLib.FileParser;
using AuxFuncLib;
using DbLib;
using ProgressLib;
using XtalLib.ProtInterfaces;

namespace BuInterfacesLib.BuInterfaces
{
	/// <summary>
	/// Process interfaces
	/// 1. generate interface file (PDB formatted)
	/// 2. compute interface accessible surface area
	/// 3. compute inter-atomic contacts
	/// </summary>
	public class BuInterfaceProcessor
	{
		#region member variables
		private BuInterfaceFileWriter interfaceWriter = new BuInterfaceFileWriter ();
		#endregion

		public BuInterfaceProcessor()
		{
		}

		#region process interfaces from database

		/// <summary>
		/// generate interface files in PDB format
		/// </summary>
		/// <param name="updatePdbIds"></param>
		public void ProcessInterfacesFromDb(string[] updatePdbIds)
		{
			BuInterfaceDbBuilder.progressInfo.Reset ();
			BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue ("Generate Biological unit interface files.");
			BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue ("Step 1: Generate interface files.");
			BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue ("Step 2: Calculate Surface Area of an interface");
			BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue ("Step 3: Calculate contacts of an interfaces");
			BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue ("Step 4: Insert data into database");

			if (updatePdbIds != null && updatePdbIds.Length == 0)
			{
				BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue ("No update needed (Input PDB list 0).");
			}
			InterfaceFileWriter interfaceWriter = new InterfaceFileWriter ();

			if (! Directory.Exists (AppSettings.tempDir))
			{
				Directory.CreateDirectory (AppSettings.tempDir);
			}			

			string connString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE=" 
				+ AppSettings.dirSettings.interfaceDbPath;
			DbBuilder.dbConnect.ConnectString = connString;
			DbQuery dbQuery = new DbQuery ();
			DbBuilder.dbConnect.ConnectToDatabase ();

			DataTable interfaceTable = null;
			string queryString = "";
			string type = "";

			BuInterfaceDbBuilder.progressInfo.currentOperationLabel = "PDB Interface Files";
			if (updatePdbIds == null)
			{
		//		queryString = string.Format ("SELECT * From PdbBuInterfaces ORDER BY PdbID, BuID;");
				queryString = string.Format ("SELECT * From PdbBuInterfaces WHERE SurfaceArea = -1 ORDER BY PdbID, BuID;");
			}
			else
			{
				queryString = string.Format ("SELECT * From PdbBuInterfaces WHERE PdbID IN ({0}) " + 
					" ORDER BY PdbID, BuID;", ParseHelper.FormatSqlListString (updatePdbIds));
				DeleteObsInterfaceFiles (updatePdbIds, "pdb");
			}
			interfaceTable = dbQuery.Query (queryString);
			type = "pdb";
			ProcessInterfaces (interfaceTable, type);
			BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue ("Generate PDB interface files done!");

			BuInterfaceDbBuilder.progressInfo.ResetCurrentProgressInfo ();
			BuInterfaceDbBuilder.progressInfo.currentOperationLabel = "PQS Interface Files";
			if (updatePdbIds == null)
			{
		//		queryString = string.Format ("SELECT * FROM PqsBuInterfaces ORDER BY PdbID, BuID;");	
				queryString = string.Format ("SELECT * From PqsBuInterfaces WHERE SurfaceArea = -1 ORDER BY PdbID, BuID;");
			}
			else
			{
				queryString = string.Format ("SELECT * From PqsBuInterfaces WHERE PdbID IN ({0}) " + 
					" ORDER BY PdbID, BuID;", ParseHelper.FormatSqlListString (updatePdbIds));
				DeleteObsInterfaceFiles (updatePdbIds, "pqs");
			}
			interfaceTable = dbQuery.Query (queryString);
			type = "pqs";
			ProcessInterfaces (interfaceTable, type);
			BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue ("Generate PQS interface files done!");

			DbBuilder.dbConnect.DisconnectFromDatabase ();
			if (Directory.Exists (AppSettings.tempDir))
			{
				try
				{
					Directory.Delete (AppSettings.tempDir, true);
				}
				catch {}
			}
		}

		/// <summary>
		/// process all interfaces in PDB or PQS
		/// </summary>
		/// <param name="interfaceTable"></param>
		/// <param name="type"></param>
		private void ProcessInterfaces (DataTable interfaceTable, string type)
		{
			string prePdbBu = "";
			string pdbBu = "";
			string pdbId = "";
			string buId = "-1";
			bool needAsaUpdated = true;
			ArrayList pdbBuList = new ArrayList (); // pdbId_buId
			foreach (DataRow dRow in interfaceTable.Rows)
			{
				pdbId = dRow["PdbID"].ToString ();
				buId = dRow["BuID"].ToString ();
				pdbBu = pdbId + "_" + buId;
				if (pdbBu != prePdbBu)
				{
					pdbBuList.Add (pdbBu);
				}
				prePdbBu = pdbBu;
			}
			BuInterfaceDbBuilder.progressInfo.totalOperationNum = pdbBuList.Count;
			BuInterfaceDbBuilder.progressInfo.totalStepNum = pdbBuList.Count;

			foreach (string pdbBuStr in pdbBuList)
			{
				BuInterfaceDbBuilder.progressInfo.currentOperationNum ++;
				BuInterfaceDbBuilder.progressInfo.currentStepNum ++;
				BuInterfaceDbBuilder.progressInfo.currentFileName = pdbBuStr;

				string[] pdbBuStrFields = pdbBuStr.Split ('_');
				// delete interface files 
		//		DeleteObsBuInterfaceFiles (pdbBuStrFields[0] + pdbBuStrFields[1], type);

                try
                {
                    pdbId = pdbBuStrFields[0];
                    buId = pdbBuStrFields[1];

                    DataRow[] interfaceRows = interfaceTable.Select(string.Format("PdbID = '{0}' AND BuID = '{1}'", pdbId, buId));
                    // generate interface files
                    ProtInterfaceInfo[] interfaceInfoList = interfaceWriter.GetInterfaces(interfaceRows, type);
                    needAsaUpdated = false;
                    ProtInterface[] interfaces = interfaceWriter.GenerateInterfaceFiles(pdbId, buId, interfaceInfoList, type, out needAsaUpdated);
                    // update surface area in the database
                    if (needAsaUpdated)
                    {
                        UpdateSurfaceAreaFieldInDb(pdbId, buId, interfaceInfoList, type);
                    }
                }
                catch (Exception ex)
                {
                    BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue("Errors: " + pdbBuStr);
                    BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue(ex.Message);
                }
			}
			BuInterfaceDbBuilder.progressInfo.progStrQueue.Enqueue ("Done!");
		}
		#endregion

		#region update surface area
		/// <summary>
		/// add the surface area value to database
		/// </summary>
		private void UpdateSurfaceAreaFieldInDb (string pdbId, string biolUnitId, ProtInterfaceInfo[] interfaceList, string type)
		{
			string updateString = "";
			string pdbTableName = "";
			string pqsTableName = "";
			DbUpdate dbUpdate = new DbUpdate ();
			if (type == "pdb")
			{
				pdbTableName = "PdbBuInterfaces";
			}
			else if (type == "pqs")
			{
				pqsTableName = "PqsBuInterfaces";
			}
			foreach (ProtInterfaceInfo protInterface in interfaceList)
			{
				if (pdbTableName != "")
				{
					updateString = string.Format ("Update {0} SET SurfaceArea = {1} " + 
						" Where PdbID = '{2}' and BuID = '{3}' AND InterfaceID = {4};", 
						pdbTableName, protInterface.ASA, pdbId, biolUnitId, protInterface.InterfaceId);
					dbUpdate.Update (updateString);
				}
				if (pqsTableName != "")
				{
					updateString = string.Format ("Update {0} SET SurfaceArea = {1} " + 
						" Where PdbID = '{2}' and BuID = '{3}' AND InterfaceID = {4};", 
						pqsTableName, protInterface.ASA, pdbId, biolUnitId, protInterface.InterfaceId);
					dbUpdate.Update (updateString);
				}
			}
		}
		#endregion

		#region delete interface files from the directory
		/// <summary>
		/// delete interface files for this list of files
		/// </summary>
		/// <param name="updatePdbIds"></param>
		/// <param name="type"></param>
		private void DeleteObsInterfaceFiles (string[] updatePdbIds, string type)
		{
            string fileDir = "";
			foreach (string pdbId in updatePdbIds)
			{
				string searchMode = pdbId + "*." + type + ".gz";
                fileDir = AppSettings.dirSettings.interfaceFilePath + "\\" + type + "\\" + pdbId.Substring (1, 2);
                if (Directory.Exists(fileDir))
                {
                    string[] interfaceFiles = Directory.GetFiles(fileDir, searchMode);
                    foreach (string interfaceFile in interfaceFiles)
                    {
                        File.Delete(interfaceFile);
                    }
                }
			}
		}

		/// <summary>
		/// delete interface files for this BU
		/// </summary>
		/// <param name="pdbBuString"></param>
		/// <param name="type"></param>
		private void DeleteObsBuInterfaceFiles (string pdbBuStr, string type)
		{
            string searchMode = pdbBuStr + "*." + type + ".gz";
            string fileDir = AppSettings.dirSettings.interfaceFilePath + "\\" + type + pdbBuStr.Substring(1, 2);
            if (Directory.Exists(fileDir))
            {
                string[] interfaceFiles = Directory.GetFiles(fileDir, searchMode);
                foreach (string interfaceFile in interfaceFiles)
                {
                    File.Delete(interfaceFile);
                }
            }
		}
		#endregion
	}
}
