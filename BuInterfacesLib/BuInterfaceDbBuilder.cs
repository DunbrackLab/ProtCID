using System;
using System.IO;
using System.Collections;
using System.Data;
using DbLib;
using ProgressLib;
using XtalLib.Settings;
using AuxFuncLib;
using BuInterfacesLib.BuInterfaces;
using BuInterfacesLib.BuInterfacesComp;
using BuInterfacesLib.BuMatch;

namespace BuInterfacesLib
{
	/// <summary>
	/// Interfaces to build or update biological interfaces database
	/// </summary>
	public class BuInterfaceDbBuilder
	{
		#region member variables
		public static ProgressInfo progressInfo = new ProgressInfo ();
		public static DbQuery dbQuery = new DbQuery ();
		#endregion

		#region enum 
		public enum DataType
		{
			PDB, PQS, PISA
		}

		public  enum CompType
		{
			PDBPQS, PISAPDB, PISAPQS
		}

		public enum BuEntityType 
		{
			Same, SubStruct, Dif
		}
		#endregion

		#region constructor
		public BuInterfaceDbBuilder()
		{
			AppSettings.LoadDirSettings ();
			AppSettings.LoadParameters ();
		}
		#endregion
		
		/// <summary>
		/// build the database of biological unit interfaces 
		/// from scratch
		/// </summary>
		public void BuildBuInterfaceDb ()
		{
			progressInfo.progStrQueue.Enqueue ("Building biological unit interfaces database includes 3 steps: ");
			progressInfo.progStrQueue.Enqueue ("1. Compute PDB/PQS biological unit unique interfaces, and compare them");
			progressInfo.progStrQueue.Enqueue ("2. Generate interface files, calculate ASA and contacts in interfaces");
			progressInfo.progStrQueue.Enqueue ("3. Compute ASU interfaces.");
			/* Compare PDB/PQS biological units
			 * 1. get PDB/PQS chain match for each biological units
			 * 2. generate PDB BU and Read PQS BU
			 * 3. get unique interfaces in PDB and PQS 
			 * (same interfaces is defined as those interfaces with same asymmetric chains and QScore >= 0.95)
			 * 4. compare unique interfaces
			 * 5. same BU or not based on the QScores
			 * */
			try
			{
				EntryBuComp entryBuComp = new EntryBuComp ();
				entryBuComp.CompareExistingEntryBUs (false); 
				progressInfo.currentOperationIndex ++; 

				// Add full symmetry strings to PDBBuSameInterfaces table
				AddFullSymmetryStrings ();  
	
				/* Process interfaces in all biological units in PDB and PQS
				 * including 
				 * 1. generate PDB formatted interface files
				 * 2. calculate ASA by NACCESS
				 * 3. compute contacts in interfaces
				 * */
				BuInterfaceProcessor interfaceProcessor = new BuInterfaceProcessor ();
				interfaceProcessor.ProcessInterfacesFromDb (null);  
				progressInfo.currentOperationIndex ++;
				/* compute interfaces in Asymmetric unit from coordinate XML files
				 * which are generated from PDB XML file
				 * all interfaces in an asu are unique due to different asymmetric chains
				 * */
				AsuInterfaces asuInterfaceProcessor = new AsuInterfaces ();
				asuInterfaceProcessor.GetAsuInterfacesFromXmlFiles (false); 
				progressInfo.currentOperationIndex ++;
				
				progressInfo.progStrQueue.Enqueue ("Updating database indexes. Please wait ...");
				UpdateIndexes ();
				progressInfo.progStrQueue.Enqueue ("Database built.");

				progressInfo.progStrQueue.Enqueue ("Compress database. Please wait ...");
				ParseHelper.ZipPdbFile (Path.Combine (AppSettings.dirSettings.interfaceDbPath, "buinterfaces.fdb"));
				progressInfo.progStrQueue.Enqueue ("Compressed.");
			}
			catch (Exception ex)
			{
				progressInfo.progStrQueue.Enqueue ("Build BU Interface Database Errors: " + ex.Message);
			}
			finally 
			{	
				DbBuilder.dbConnect.DisconnectFromDatabase ();
				progressInfo.threadFinished = true;
			}			
		}

		/// <summary>
		/// update the database of biological unit interfaces
		/// </summary>
		public void UpdateBuInterfaceDb ()
		{
			try
			{
				EntryBuComp entryBuComp = new EntryBuComp ();
				string[] updatedPdbIds = entryBuComp.CompareExistingEntryBUs (true);

				// add full symmetry strings
				AddFullSymmetryStrings ();

		//		string[] updatedPdbIds = GetUpdateEntries ();
				if (updatedPdbIds != null)
				{
					BuInterfaceProcessor interfaceProcessor = new BuInterfaceProcessor ();
					interfaceProcessor.ProcessInterfacesFromDb (updatedPdbIds);
				}

				AsuInterfaces asuInterfaceProcessor = new AsuInterfaces ();
				asuInterfaceProcessor.GetAsuInterfacesFromXmlFiles (true);

				progressInfo.progStrQueue.Enqueue ("Updating database indexes. Please wait ...");
				UpdateIndexes ();

				progressInfo.progStrQueue.Enqueue ("Database updated.");

				progressInfo.progStrQueue.Enqueue ("Compress database. Please wait ...");
				ParseHelper.ZipPdbFile (AppSettings.dirSettings.interfaceDbPath);
				progressInfo.progStrQueue.Enqueue ("Compressed.");
			}
			catch (Exception ex)
			{
				progressInfo.progStrQueue.Enqueue ("Update BU Interface database errors: " + ex.Message);
			}
			finally
			{
				DbBuilder.dbConnect.DisconnectFromDatabase ();
				progressInfo.threadFinished = true;
			}		
		}

		private string[] GetUpdateEntries ()
		{
			BuChainMatchCategory buMatchCat = new BuChainMatchCategory ();
			System.Xml.Serialization.XmlSerializer xmlSerializer = 
				new System.Xml.Serialization.XmlSerializer (typeof(BuChainMatchCategory));
			FileStream xmlFileStream = new FileStream("PdbPqsBu_sameSubEntity.xml", FileMode.Open);
			buMatchCat = (BuChainMatchCategory) xmlSerializer.Deserialize (xmlFileStream);
			xmlFileStream.Close ();
			ArrayList entryList = new ArrayList ();
			foreach (BiolUnitMatch buMatch in buMatchCat.BuChainMatchList)
			{
				if (! entryList.Contains (buMatch.pdbId))
				{
					entryList.Add (buMatch.pdbId);
				}
			}
			string[] updateEntries = new string [entryList.Count];
			entryList.CopyTo (updateEntries);
			return updateEntries;
		}
		/// <summary>
		/// add the full symmetry strings to pdbbusameinterfaces table
		/// </summary>
		private void AddFullSymmetryStrings ()
		{
			DbBuilder.dbConnect.dbConnection = null;
			DbBuilder.dbConnect.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE=" + AppSettings.dirSettings.dbPath;
			DbBuilder.dbConnect.ConnectToDatabase ();
			DbQuery dbQuery = new DbQuery ();
			string queryString = "SELECT * FROM PDBBUGEN;";
			DataTable pdbBuGenTable = dbQuery.Query (queryString);
			DbBuilder.dbConnect.DisconnectFromDatabase ();

			DbBuilder.dbConnect.dbConnection = null;
			DbBuilder.dbConnect.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=masterkey;DATABASE=" + AppSettings.dirSettings.interfaceDbPath;
			DbBuilder.dbConnect.ConnectToDatabase ();
			string fullSymString1 = "";
			string fullSymString2 = "";
			string conditionString = "";
			string updateString = "";
			DbUpdate dbUpdate = new DbUpdate ();
			updateString = "Update PdbBuSameInterfaces SET FullSymmetryString1 = 'X,Y,Z' " + 
				"WHERE SymmetryString1 = '1_555' AND FullSymmetryString1 is NULL;";
			dbUpdate.Update (updateString);
			updateString = "Update PdbBuSameInterfaces SET FullSymmetryString2 = 'X,Y,Z' " + 
				"WHERE SymmetryString2 = '1_555' AND FullSymmetryString2 is NULL;";
			dbUpdate.Update (updateString);

			queryString = "Select * From PdbBuSameInterfaces Where FullSymmetryString1 is NULL OR FullSymmetryString2 is NULL;";
			DataTable nullFullSymTable = dbQuery.Query (queryString);
			string pdbId = "";
			int buId = -1;
			string chain = "";
			string symString = "";
			string fullSymSelectString = "";
			DataRow[] fullSymRows = null;
			foreach (DataRow dRow in nullFullSymTable.Rows)
			{
				pdbId = dRow["PdbID"].ToString ();
				buId = Convert.ToInt32 (dRow["BuID"].ToString ());
				chain = dRow["Chain1"].ToString ().Trim ();
				symString = dRow["SymmetryString1"].ToString ().Trim ();
				string[] symFields = symString.Split ('_');
				fullSymSelectString = string.Format ("PdbID = '{0}' AND BiolUnitID = '{1}' " + 
					" AND AsymID = '{2}' AND SymOpNum = '{3}' AND SymString = '{4}'", 
					pdbId,  buId, chain, symFields[0], symFields[1]);
				fullSymRows = pdbBuGenTable.Select (string.Format (fullSymSelectString));
				if (fullSymRows.Length == 0)
				{
					progressInfo.progStrQueue.Enqueue ("No fullsymmetry string for chain1" + ConvertDataRowToText (dRow));
					continue;
				}
				fullSymString1 = fullSymRows[0]["FullSymString"].ToString ().Trim ();

				chain = dRow["Chain2"].ToString ().Trim ();
				symString = dRow["SymmetryString2"].ToString ().Trim ();
				symFields = symString.Split ('_');
				fullSymSelectString = string.Format ("PdbID = '{0}' AND BiolUnitID = '{1}' " + 
					" AND AsymID = '{2}' AND SymOpNum = '{3}' AND SymString = '{4}'", 
					pdbId, buId, chain, symFields[0], symFields[1]);
				fullSymRows = pdbBuGenTable.Select (string.Format (fullSymSelectString));
				if (fullSymRows.Length == 0)
				{
					progressInfo.progStrQueue.Enqueue ("No fullsymmetry string for chain2" + ConvertDataRowToText (dRow));
					continue;
				}
				fullSymString2 = fullSymRows[0]["FullSymString"].ToString ().Trim ();

				conditionString = string.Format ("PdbID = '{0}' AND BuID = {1} AND " + 
					" InterfaceID = {2} AND SameInterfaceID = {3}" , 
					pdbId, buId, dRow["InterfaceID"], dRow["SameInterfaceID"]);
				updateString = string.Format ("Update PdbBuSameInterfaces " + 
					" SET FullSymmetryString1 = '{0}',  FullSymmetryString2 = '{1}'" + 
					" Where {2};", fullSymString1, fullSymString2, conditionString);
				dbUpdate.Update (updateString);
			}
		}
		/// <summary>
		/// output a data row to text string 
		/// </summary>
		/// <param name="dRow"></param>
		/// <returns></returns>
		private string ConvertDataRowToText (DataRow dRow)
		{
			string rowText = "";
			foreach (object item in dRow.ItemArray)
			{
				rowText += item.ToString ().Trim ();
				rowText += "	";
			}
			return rowText.TrimEnd ('	');
		}

		/// <summary>
		/// recompute indexes for selected table
		/// to speed up queries
		/// </summary>
		/// <returns></returns>
		public static void UpdateIndexes()
		{
			DbBuilder.dbConnect.ConnectToDatabase ();
			AppSettings.progressInfo.ResetCurrentProgressInfo ();
			DbQuery dbQuery = new DbQuery ();
			try 
			{	
				
				System.Data.Odbc.OdbcCommand updateIndexCommand = DbBuilder.dbConnect.CreateCommand ();

				// retrieve user-defined indexes
				string showIndexStr = @"select RDB$INDEX_NAME from RDB$INDICES WHERE RDB$SYSTEM_FLAG is NULL;";
				//string showIndexStr = @"select RDB$INDEX_NAME from RDB$INDICES;";
				updateIndexCommand.CommandText = showIndexStr;
				System.Data.Odbc.OdbcDataReader indexReader = updateIndexCommand.ExecuteReader ();
				ArrayList indexList = new ArrayList ();
				if (indexReader.HasRows)
				{
					while(indexReader.Read ())
					{
						indexList.Add (indexReader.GetString (0).Trim ());
					}
					indexReader.Close ();
				}
				foreach (object indexName in indexList)
				{					
					// rebuild this index for cryst and interface
					if (indexName.ToString ().ToUpper ().IndexOf("RDB$PRIMARY") == -1)
					{
						string inactiveIndexStr = string.Format("ALTER INDEX {0} INACTIVE;", indexName.ToString ());
						updateIndexCommand.CommandText = inactiveIndexStr;
						updateIndexCommand.ExecuteNonQuery ();
						string activeIndexStr = string.Format("ALTER INDEX {0} ACTIVE;", indexName.ToString ());
						updateIndexCommand.CommandText = activeIndexStr;
						updateIndexCommand.ExecuteNonQuery ();
					}
					// recompute selectivity of this index
					string updateSelectivityStr = string.Format("SET STATISTICS INDEX {0};", indexName.ToString ());
					updateIndexCommand.CommandText = updateSelectivityStr;
					updateIndexCommand.ExecuteNonQuery ();
				}
			}
			catch (Exception ex)
			{
				// Displays the Error Message in the progress label.
				AppSettings.progressInfo.progStrQueue.Enqueue ("Update Indexes Errors: " + ex.Message);	
			} 
		}
	}
}
