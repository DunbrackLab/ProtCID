using System;
using System.Collections.Generic;
using System.IO;
using System.Data;
using ProtCidSettingsLib;
using ProgressLib;
using DbLib;
using CrystalInterfaceLib.Settings;
using PfamLib.Settings;

namespace InterfaceClusterLib.AuxFuncs
{
	/// <summary>
	/// Summary description for InterfaceDbBuilder.
	/// </summary>
	public class DbBuilderHelper
	{
        public static bool IsSettingInitialized = false;
        public DbBuilderHelper()
		{
		}

		#region recompute indexes
		// <summary>
		/// recompute indexes for selected table
		/// to speed up queries
		/// </summary>
		/// <returns></returns>
		public static void UpdateIndexes(string tableType, DbConnect dbConnect)
		{
			ProtCidSettings.progressInfo.ResetCurrentProgressInfo ();
			DbQuery dbQuery = new DbQuery ();
			try 
			{	
				
				System.Data.Odbc.OdbcCommand updateIndexCommand = dbConnect.CreateCommand ();

				// retrieve user-defined indexes
				string showIndexStr = @"select RDB$INDEX_NAME, RDB$RELATION_NAME from RDB$INDICES WHERE RDB$SYSTEM_FLAG = 0;";
				//string showIndexStr = @"select RDB$INDEX_NAME from RDB$INDICES;";
				updateIndexCommand.CommandText = showIndexStr;
				System.Data.Odbc.OdbcDataReader indexReader = updateIndexCommand.ExecuteReader ();
			//	ArrayList indexList = new ArrayList ();
                Dictionary<string, List<string>> relationIndexHash = new Dictionary<string,List<string>> ();
                string indexName = "";
                string relationName = "";
				if (indexReader.HasRows)
				{
					while(indexReader.Read ())
					{
                        relationName = indexReader.GetString(1).Trim().ToUpper();
                        if (relationName.IndexOf(tableType.ToUpper()) > -1)
                        {
                            indexName = indexReader.GetString(0).Trim().ToUpper();

                            if (relationIndexHash.ContainsKey (relationName))
                            {
                                relationIndexHash[relationName].Add(indexName);
                            }
                            else
                            {
                                List<string> relationIndexList = new List<string> ();
                                relationIndexList.Add(indexName);
                                relationIndexHash.Add(relationName, relationIndexList);
                            }
                        }
					}
					indexReader.Close ();
				}
                foreach (string relationTable in relationIndexHash.Keys)
                {
                    foreach (string index in relationIndexHash[relationTable])
                    {
                        // rebuild this index for cryst and interface
                        if (indexName.ToString().ToUpper().IndexOf("RDB$PRIMARY") == -1)
                        {
                            string inactiveIndexStr = string.Format("ALTER INDEX {0} INACTIVE;", index);
                            updateIndexCommand.CommandText = inactiveIndexStr;
                            updateIndexCommand.ExecuteNonQuery();
                            string activeIndexStr = string.Format("ALTER INDEX {0} ACTIVE;", index);
                            updateIndexCommand.CommandText = activeIndexStr;
                            updateIndexCommand.ExecuteNonQuery();
                        }
                        // recompute selectivity of this index
                        string updateSelectivityStr = string.Format("SET STATISTICS INDEX {0};", index);
                        updateIndexCommand.CommandText = updateSelectivityStr;
                        updateIndexCommand.ExecuteNonQuery();
                    }
                }
			}
			catch (Exception ex)
			{
				// Displays the Error Message in the progress label.
				ProtCidSettings.progressInfo.progStrQueue.Enqueue ("Update Indexes Errors: " + ex.Message);	
			} 
		}
		#endregion

		#region get file list
		/// <summary>
		/// a list of update entries
		/// </summary>
		/// <returns></returns>
		public static string[] GetPdbCodeListFromFile (string lsFile)
		{
			List<string> fileList = new List<string> ();
			StreamReader dataReader = new StreamReader (lsFile);
			string line = "";
            string entry = "";
			while ((line = dataReader.ReadLine ()) != null)
			{
                entry = line.Substring(0, 4);
                if (!fileList.Contains(entry))
                {
                    fileList.Add(entry);
                }
			}
			dataReader.Close ();
			return fileList.ToArray ();
		}
		#endregion

       
		/// <summary>
		/// initialize dbconnect
		/// </summary>
		public static void Initialize()
		{
            if (IsSettingInitialized)
            {
                return;
            }
            ProtCidSettings.dataType = "pfam";
			ProtCidSettings.LoadDirSettings ();
			AppSettings.LoadParameters ();
			AppSettings.LoadSymOps ();

            ProtCidSettings.tempDir = "C:\\xtal_temp0";

            if (ProtCidSettings.pdbfamDbConnection == null)
            {
                ProtCidSettings.pdbfamDbConnection = new DbConnect();
                ProtCidSettings.pdbfamDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                    ProtCidSettings.dirSettings.pdbfamDbPath;
                ProtCidSettings.pdbfamQuery = new DbQuery(ProtCidSettings.pdbfamDbConnection);
            }

            if (ProtCidSettings.protcidDbConnection == null)
            {
                ProtCidSettings.protcidDbConnection = new DbConnect();
                ProtCidSettings.protcidDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                    ProtCidSettings.dirSettings.protcidDbPath;
                ProtCidSettings.protcidQuery = new DbQuery(ProtCidSettings.protcidDbConnection);
            }

            if (ProtCidSettings.alignmentDbConnection == null)
            {
                ProtCidSettings.alignmentDbConnection = new DbConnect();
                ProtCidSettings.alignmentDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                    ProtCidSettings.dirSettings.alignmentDbPath;
                ProtCidSettings.alignmentQuery = new DbQuery(ProtCidSettings.alignmentDbConnection);
            }

            if (ProtCidSettings.buCompConnection == null)
            {
                ProtCidSettings.buCompConnection = new DbConnect();
                ProtCidSettings.buCompConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                    ProtCidSettings.dirSettings.baInterfaceDbPath;
                ProtCidSettings.buCompQuery = new DbQuery(ProtCidSettings.buCompConnection);
            }

            ProtCidSettings.logWriter.WriteLine(DateTime.Today.ToShortDateString () + "\r\n");

			DataTables.GroupDbTableNames.SetGroupDbTableNames (ProtCidSettings.dataType);
            
            PfamLibSettings.InitializeFromProtCidSettings();

            IsSettingInitialized = true;
		}

        /// <summary>
        /// 
        /// </summary>
        public static void Dispose ()
        {
            ProtCidSettings.logWriter.WriteLine("Processing done!");
            ProtCidSettings.logWriter.Close();
        }
	}
}
