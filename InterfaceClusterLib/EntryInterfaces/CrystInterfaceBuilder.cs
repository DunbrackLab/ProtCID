using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using ProtCidSettingsLib;
using ProgressLib;
using DbLib;
using InterfaceClusterLib.stat;
using InterfaceClusterLib.InterfaceComp;
using InterfaceClusterLib.Clustering;
using InterfaceClusterLib.InterfaceProcess;
using CrystalInterfaceLib.Settings;

namespace InterfaceClusterLib.EntryInterfaces
{
	/// <summary>
	/// Interface to crystal structures
	/// </summary>
	public class CrystInterfaceBuilder
	{
		#region member variables
		public static double familyLeastIdentity = 50.0;
		public static double familyLeastCoverage = 80.0;
		#endregion

		#region constructors
		public CrystInterfaceBuilder()
		{
	//		ProtCidSettings.dataType = "id" + ((int)familyLeastIdentity).ToString ();
		}
		#endregion

		#region Generate Cryst Interface coordinate Files
		/// <summary>
		/// generate interface files for all represenative entries
		/// </summary>
		public void GenerateRepEntryInterfaceFiles ()
		{
			Initialize ();
			CrystInterfaceProcessor interfaceFileGen = new CrystInterfaceProcessor ();
			interfaceFileGen.GenerateRepEntryInterfaceFiles ();
		}

		/// <summary>
		/// generate interface files for a list representative entries
		/// </summary>
		/// <param name="pdbList"></param>
		public void GenerateRepEntryInterfaceFiles (string[] pdbList)
		{
			Initialize ();
			CrystInterfaceProcessor interfaceFileGen = new CrystInterfaceProcessor ();
			interfaceFileGen.GenerateRepEntryInterfaceFiles (pdbList);
		}

		/// <summary>
		/// generate interface files for a list input entries
		/// </summary>
		/// <param name="pdbList"></param>
		public void GenerateEntryInterfaceFiles (string[] pdbList)
		{
			Initialize ();
			if (pdbList == null)
			{
				DbQuery dbQuery = new DbQuery ();
				string queryString = "Select Distinct PdbID From CrystEntryInterfaces Where SurfaceArea < 0;";
                DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
				pdbList = new string [entryTable.Rows.Count];
				int i = 0;
				foreach (DataRow dRow in entryTable.Rows)
				{
					pdbList[i] = dRow["PdbID"].ToString ();
					i ++;
				}
			}
			CrystInterfaceProcessor interfaceFileGen = new CrystInterfaceProcessor ();
            interfaceFileGen.GenerateEntryInterfaceFiles(pdbList);
		} 
        #endregion

        #region compute interfaces in crystals
        /// <summary>
        /// retrieve interfaces from the list of pdb entries, 
        /// and save the data into db
        /// </summary>
        /// <param name="pdbList"></param>
		public void ComputeEntryCrystInterfaces (string[] pdbList)
		{
			Initialize ();
            CrystInterfaceRetriever crystInterfaceRetriever = new CrystInterfaceRetriever();
            crystInterfaceRetriever.FindUniqueInterfaces(pdbList);
		}

        /// <summary>
        /// retrieve interfaces from the list of pdb entries, 
        /// and save the data into db
        /// </summary>
        /// <param name="pdbList"></param>
        public void ComputeAsuCrystInterfaces(string[] pdbList)
        {
            Initialize();
            CrystInterfaceRetriever crystInterfaceRetriever = new CrystInterfaceRetriever();
            crystInterfaceRetriever.FindUniqueInterfacesFromAsu (pdbList);
        }
        /// <summary>
        /// retrieve interfaces from the list of pdb entries
        /// </summary>
        /// <param name="pdbList"></param>
		public void ComputeNmrEntryCrystInterfaces (string[] pdbList)
		{
			Initialize ();
            CrystInterfaceRetriever crystInterfaceRetriever = new CrystInterfaceRetriever();
            crystInterfaceRetriever.GetNmrEntryInterfaces(pdbList);
		}

        /// <summary>
        /// retrieve interfaces from representative entry and its homologies
        /// then compare the interfaces, and save data into db
        /// </summary>
        /// <param name="repEntry"></param>
        /// <param name="pdbList"></param>
		public void ComputeEntryCrystInterfaces (string repEntry, string[] pdbList)
		{
			Initialize ();
            CrystInterfaceRetriever crystInterfaceRetriever = new CrystInterfaceRetriever();
            crystInterfaceRetriever.FindUniqueInterfaces(repEntry, pdbList);
		}
        #endregion

		#region aux functions
		/// <summary>
		/// initialize dbconnect
		/// </summary>
		internal void Initialize()
		{
			ProtCidSettings.LoadDirSettings ();
			AppSettings.LoadParameters ();
			AppSettings.LoadSymOps ();

            ProtCidSettings.protcidDbConnection = new DbConnect ("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" + 
				ProtCidSettings.dirSettings.protcidDbPath);
            ProtCidSettings.pdbfamDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
               ProtCidSettings.dirSettings.pdbfamDbPath);

			DataTables.GroupDbTableNames.SetGroupDbTableNames (ProtCidSettings.dataType);

            ProtCidSettings.tempDir = "C:\\xtal_temp0";
		}
		#endregion
	}
}
