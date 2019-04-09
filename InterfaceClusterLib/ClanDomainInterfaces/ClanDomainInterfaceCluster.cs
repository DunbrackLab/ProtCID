using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using InterfaceClusterLib.DomainInterfaces;
using InterfaceClusterLib.DataTables;
using ProtCidSettingsLib;
using DbLib;

namespace InterfaceClusterLib.ClanDomainInterfaces
{

    public class ClanDomainInterfaceCluster : DomainInterfaceCluster
    {

        /// <summary>
        /// 
        /// </summary>
        public void ClusterSuperDomainInterfaces()
        {
            ProtCidSettings.dataType = "clan";
            ProtCidSettings.LoadDirSettings();
            ProtCidSettings.protcidDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                ProtCidSettings.dirSettings.protcidDbPath);
            ProtCidSettings.pdbfamDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                ProtCidSettings.dirSettings.pdbfamDbPath);

            InitializeTable();
            InitializeDbTable();
            isDomain = true;
            GroupDbTableNames.SetGroupDbTableNames(ProtCidSettings.dataType);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Cluster on Clans";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Cluster domain interfaces in clan");

            string queryString = "Select Distinct ClanSeqID From PfamDomainFamilyRelation;";
            DataTable clanSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.totalOperationNum = clanSeqIdTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = clanSeqIdTable.Rows.Count;

            int clanSeqId = 0;
            foreach (DataRow clanIdRow in clanSeqIdTable.Rows)
            {
                clanSeqId = Convert.ToInt32(clanIdRow["ClanSeqID"].ToString ());

                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = clanSeqId.ToString();

                ClusterClanDomainInterfaces(clanSeqId);
            }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanSeqId"></param>
        public void ClusterClanDomainInterfaces(int clanSeqId)
        {
            int[] clanRelSeqIds = GetClanRelSeqIDs(clanSeqId);
            try
            {
                DataTable clanDomainInterfaceCompTable = GetDomainInterfaceCompTable(clanSeqId, clanRelSeqIds);

                DataTable clanEntryDomainInterfaceCompTable = GetClanEntryDomainInterfaceCompTable(clanRelSeqIds);

                Dictionary<string, int> interfaceIndexHash = GetInterfacesIndexes(clanDomainInterfaceCompTable);
                GetPdbInterfacesFromIndex(interfaceIndexHash);

                bool canClustered = true;
                double[,] distMatrix = CreateDistMatrix(clanEntryDomainInterfaceCompTable, clanDomainInterfaceCompTable,
                    interfaceIndexHash, out canClustered);
                if (canClustered)
                {
                    Dictionary<int, int[]> clusterHash = ClusterThisGroupInterfaces(distMatrix, interfaceIndexHash);
                    if (clusterHash.Count > 0)
                    {
                        string[] clanRepEntries = GetClanRepEntries(clanDomainInterfaceCompTable);
                        Dictionary<string, int[]> entryCfGroupHash = GetEntryCfGroups(clanRepEntries);
                        Dictionary<string, string> entrySgAsuHash = GetEntrySgAsu(clanRepEntries);
                        AssignDataToTable(clanSeqId, clusterHash, entryCfGroupHash, entrySgAsuHash, clanEntryDomainInterfaceCompTable);
                        InsertDataToDb();
                    }
                }
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("Clustering group " + clanSeqId + " error: " + ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanSeqId"></param>
        /// <returns></returns>
        private int[] GetClanRelSeqIDs(int clanSeqId)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation Where ClanSeqID = {0};", clanSeqId);
            DataTable clanRelSeqTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] relSeqIds = new int[clanRelSeqTable.Rows.Count];
            int count = 0;
            int relSeqId = 0;
            foreach (DataRow relSeqIdRow in clanRelSeqTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString ());
                relSeqIds[count] = relSeqId;
                count++;
            }
            return relSeqIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanDomainInterfaceCompTable"></param>
        /// <returns></returns>
        private string[] GetClanRepEntries(DataTable clanDomainInterfaceCompTable)
        {
            List<string> repEntryList = new List<string> ();
            string pdbId1 = "";
            string pdbId2 = "";
            foreach (DataRow interfaceCompRow in clanDomainInterfaceCompTable.Rows)
            {
                pdbId1 = interfaceCompRow["PdbID1"].ToString();
                pdbId2 = interfaceCompRow["PdbID2"].ToString();

                if (!repEntryList.Contains(pdbId1))
                {
                    repEntryList.Add(pdbId1);
                }
                if (!repEntryList.Contains(pdbId2))
                {
                    repEntryList.Add(pdbId2);
                }
            }
            return repEntryList.ToArray ();
        }

        #region domain interface q score table
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanSeqId"></param>
        /// <param name="relSeqIds"></param>
        /// <returns></returns>
        private DataTable GetDomainInterfaceCompTable(int clanSeqId, int[] relSeqIds)
        {
            DataTable domainInterfaceCompTable = GetClanDomainInterfaceCompTable (clanSeqId);
            foreach (int relSeqId in relSeqIds)
            {
                DataTable relInterfaceCompTable = GetRelationDifEntryInterfaceComp(relSeqId);
                foreach (DataRow interfaceCompRow in relInterfaceCompTable.Rows)
                {
                    DataRow compRow = domainInterfaceCompTable.NewRow();
                    compRow.ItemArray = interfaceCompRow.ItemArray;
                    domainInterfaceCompTable.Rows.Add(compRow);
                }
            }
            return domainInterfaceCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanSeqId"></param>
        /// <returns></returns>
        private DataTable GetClanDomainInterfaceCompTable(int clanSeqId)
        {
            string queryString = string.Format("Select ClanSeqID As RelSeqID, PdbID1, DomainInterfaceId1 As InterfaceID1, " + 
                " PdbID2, DomainInterfaceId2 As InterfaceID2, Qscore " +
                " From ClanDomainInterfaceComp Where ClanSeqID = {0};", clanSeqId);
            DataTable clanDomainInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            return clanDomainInterfaceCompTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanRelSeqIds"></param>
        /// <returns></returns>
        private DataTable GetClanEntryDomainInterfaceCompTable(int[] clanRelSeqIds)
        {
            DataTable entryInterfaceCompTable = null;

            foreach (int relSeqId in clanRelSeqIds)
            {
                DataTable interfaceCompTable = GetRelationEntryInterfaceComp(relSeqId);
                if (entryInterfaceCompTable == null)
                {
                    entryInterfaceCompTable = interfaceCompTable.Copy();
                }
                else
                {
                    foreach (DataRow compRow in interfaceCompTable.Rows)
                    {
                        DataRow dataRow = entryInterfaceCompTable.NewRow();
                        dataRow.ItemArray = compRow.ItemArray;
                        entryInterfaceCompTable.Rows.Add(dataRow);
                    }
                }
            }
            return entryInterfaceCompTable;
        }
        #endregion

        #region initialize table
        /// <summary>
        /// 
        /// </summary>
        private void InitialzeTable()
        {
            tableName = ProtCidSettings.dataType + "DomainInterfaceCluster";
            string[] clusterCols = { "ClanSeqID", "ClusterID", "GroupSeqID", "CfGroupID", "SpaceGroup", "ASU", "PdbID", "DomainInterfaceID" };
            clusterTable = new DataTable(tableName);
            foreach (string clusterCol in clusterCols)
            {
                clusterTable.Columns.Add(new DataColumn(clusterCol));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void InitializeDbTable()
        {
            string createTableString = string.Format("CREATE Table {0} (" +
               " ClanSeqID INTEGER NOT NULL, " +
               " ClusterID INTEGER NOT NULL, " +
          //     " RelSeqID INTEGER NOT NULL, " +
               " GroupSeqID INTEGER NOT NULL, " +
               " CFGroupID INTEGER NOT NULL, " +
               " SpaceGroup VARCHAR(30) NOT NULL, " +
               " ASU VARCHAR(50) NOT NULL, " +
               " PDBID CHAR(4) NOT NULL, " +
               " DomainInterfaceID INTEGER NOT NULL);",
               tableName);
            DbCreator dbCreate = new DbCreator();
            dbCreate.CreateTableFromString(ProtCidSettings.protcidDbConnection, createTableString, tableName, true);

            string indexString = string.Format("Create INDEX {0}_Idx1 ON {0} (ClanSeqID, PdbID, DomainInterfaceID);",
               tableName);
            dbCreate.CreateIndex(ProtCidSettings.protcidDbConnection, indexString, tableName);
        }
        #endregion
    }
}
