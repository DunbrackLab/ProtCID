using System;
using System.Data;
using DbLib;
using ProtCidSettingsLib;

namespace InterfaceClusterLib.stat
{
	/// <summary>
	/// Summary description for InterfaceData.
	/// </summary>
	public class InterfaceStatData
    {
        #region data tables
        public DataTable interfaceDataTable = null;	
		public DataTable clusterDataTable = null;
		public DataTable clusterInterfaceCompTable = null;
		public DataTable clusterSumInfoTable = null;
        public DataTable dbStatInfoTable = null;
        #endregion

        public InterfaceStatData(string type)
		{
			string[] interfaceColNames = {"GroupSeqID", "ClusterID", "CfGroupID", "SpaceGroup", "CrystForm", "PdbID", "InterfaceID", "InterfaceUnit",
											"NumOfInterfaces", "SurfaceArea",  "InASU", "InPDB", "InPisa", "ASU", "PdbBU", "PdbBuID", 
											"PisaBU", "PisaBuID", "PdbPisa", "Name", "Species", "UnpCode"};
			string[] clusterColNames = {"GroupSeqID", "ClusterID", "#CFG/Cluster", "#CFG/Family", "#Entry/Family",
										"MinSeqIdentity", "Q(MinIdentity)", "OutMaxSeqIdentity", "InterfaceType", 
                                       "ClusterInterface", "MediumSurfaceArea"};
			string[] interfaceCompColNames = {"GroupSeqID", "ClusterID", "SpaceGroup1", "PdbID1", "InterfaceID1", 
												 "SpaceGroup2", "PdbID2", "InterfaceID2", "QScore", "Identity"};
			interfaceDataTable = new DataTable (type + "ClusterEntryInterfaces");	
			foreach (string interfaceCol in interfaceColNames)
			{
				interfaceDataTable.Columns.Add (new DataColumn (interfaceCol));
			}
			clusterDataTable = new DataTable (type + "Clusters");
			foreach (string clusterCol in clusterColNames)
			{
				clusterDataTable.Columns.Add (new DataColumn (clusterCol));
			}
			clusterInterfaceCompTable = new DataTable (type + "ClusterInterfaceComp");
			foreach (string qscoreCol in interfaceCompColNames)
			{
				clusterInterfaceCompTable.Columns.Add (new DataColumn (qscoreCol));
			}
			string[] sumInfoCols = {"GroupSeqID", "ClusterID", "SurfaceArea",
									"InASU", "InPDB", "InPISA", 
									"MaxASU", "MaxPdbBU", "MaxPisaBU",
								    "NumOfCfgCluster", "NumOfCfgFamily", "NumOfEntryFamily", "NumOfEntryCluster", 
									"MinSeqIdentity", "Q_MinIdentity", "OutMaxSeqIdentity", "NumOfNmr", "InterfaceType", 
                                   "ClusterInterface", "MediumSurfaceArea"};
			clusterSumInfoTable = new DataTable (type + "ClusterSumInfo");
			foreach (string sumInfoCol in sumInfoCols)
			{
				clusterSumInfoTable.Columns.Add (new DataColumn (sumInfoCol));
			}
		}

		/// <summary>
		/// create summary information for the interface clusters
		/// </summary>
		public void InitializeSumInfoTablesInDb (string type)
		{
			DbCreator dbCreate = new DbCreator ();
			// details for interfaces in each cluster, including representative entry and homologous entries
            string createTableString = "";
            type = type.ToUpper();
            createTableString = "CREATE TABLE " + type + "ClusterEntryInterfaces ( ";
            if (type.IndexOf("SUPER") > -1)
            {
                createTableString += "SuperGroupSeqID INTEGER NOT NULL, ";
            }
                createTableString = createTableString + 
                    " GroupSeqID INTEGER NOT NULL, " +
                 " ClusterID INTEGER NOT NULL, " +
                 " CfGroupID INTEGER NOT NULL, " +
                 " SpaceGroup VARCHAR(40) NOT NULL, " +
                 " CrystForm BLOB Sub_Type TEXT NOT NULL, " +
                 " PdbID CHAR(4) NOT NULL, " +
                 " InterfaceID INTEGER NOT NULL, " +
                 " InterfaceUnit CHAR(3) NOT NULL, " +
                 " NumOfInterfaces INTEGER NOT NULL, " +
                 " SurfaceArea FLOAT, " +
                 " InASU CHAR, InPDB CHAR, InPISA CHAR, " +
                 " ASU BLOB Sub_Type TEXT NOT NULL,  " +
                 " PDBBU BLOB Sub_Type TEXT NOT NULL, PdbBuID VARCHAR(20), " +
                 " PISABU BLOB Sub_Type TEXT NOT NULL, PisaBUID INTEGER, " +
                 " pdbpisa VARCHAR(20), " +
                 " Name BLOB Sub_Type TEXT, " +
                 " Species VARCHAR(255), " + 
                 " UnpCode VARCHAR(255) );";
                dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, type + "ClusterEntryInterfaces");
            string createIndexString = "";
            if (type.IndexOf("SUPER") > -1)
            {
                createIndexString = string.Format("CREATE INDEX {0}Cluster_Idx1 ON {1} (SuperGroupSeqID, ClusterID);",
                    type, type + "ClusterEntryInterfaces");
            }
            else
            {
                createIndexString = string.Format("CREATE INDEX {0}Cluster_Idx1 ON {1} (GroupSeqID, ClusterID);",
                     type, type + "ClusterEntryInterfaces");
            }
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, type + "ClusterEntryInterfaces");
            createIndexString = string.Format("CREATE INDEX {0}Cluster_Idx2 ON {1} (PdbID);",
                type, type + "ClusterEntryInterfaces");
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, type + "ClusterEntryInterfaces");

			// the summary information for each cluster
            createTableString = "CREATE TABLE " + type + "ClusterSumInfo (";
            if (type.IndexOf ("SUPER") > -1)
            {
                createTableString += " SuperGroupSeqID INTEGER NOT NULL, ";
            }
            else
            {
                createTableString += " GroupSeqID INTEGER NOT NULL, ";
            }
            createTableString = createTableString +
				" ClusterID INTEGER NOT NULL, " + 
				" SurfaceArea FLOAT NOT NULL, " + 
				" InASU INTEGER, InPDB INTEGER, InPISA INTEGER, " +
                " MaxASU BLOB Sub_Type TEXT, MaxPdbBU BLOB Sub_Type TEXT, MaxPisaBU BLOB Sub_Type TEXT," + 
				" NumOfCfgCluster INTEGER NOT NULL, " + 
				" NumOfCfgFamily INTEGER NOT NULL, " + 
				" NumOfEntryCluster INTEGER NOT NULL, " + 
				" NumOfEntryFamily INTEGER NOT NULL, " + 
				" MinSeqIdentity FLOAT, " + 
				" Q_MinIdentity FLOAT, " + 
				" OutMaxSeqIdentity FLOAT, " + 
                " InterfaceType VARCHAR(3), " +
				" NumOfNMR INTEGER, " + 
                " ClusterInterface VARCHAR(12), " + // the interface with maximum surface area
                " MediumSurfaceArea FLOAT );" ;
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, type + "ClusterSumInfo");
            if (type.IndexOf("SUPER") > -1)
            {
                createIndexString = string.Format("CREATE INDEX {0}ClusterSum_Idx1 ON {1} (SuperGroupSeqID, ClusterID);",
                   type, type + "ClusterSumInfo");
            }
            else
            {
                createIndexString = string.Format("CREATE INDEX {0}ClusterSum_Idx1 ON {1} (GroupSeqID, ClusterID);",
                    type, type + "ClusterSumInfo");
            }
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, createIndexString, type + "ClusterSumInfo");
		}

        public void InitializeStatInfoTable(string type)
        {
            string[] statInfoCols = { "Category", "Single", "Pair", "Total" };
            dbStatInfoTable = new DataTable(type + "StatInfo");
            foreach (string statInfoCol in statInfoCols)
            {
                dbStatInfoTable.Columns.Add(new DataColumn(statInfoCol));
            }

            DbCreator dbCreate = new DbCreator();
            string createTableString = "Create Table " + type + "StatInfo ( " +
                " Category VARCHAR(30) NOT NULL, " +
                " Single Integer NOT NULL, " +
                " Pair Integer NOT NULL, " +
                " Total Integer NOT NULL );";
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, type + "StatInfo");
        }

        /// <summary>
		/// clear tables
		/// </summary>
		public void Clear ()
		{
			interfaceDataTable.Clear ();
			clusterDataTable.Clear ();
			clusterInterfaceCompTable.Clear ();
			clusterSumInfoTable.Clear ();
		}
	}
}
