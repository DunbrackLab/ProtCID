using System;
using System.Collections.Generic;
using ProtCidSettingsLib;
using InterfaceClusterLib.stat;
using InterfaceClusterLib.InterfaceComp;
using InterfaceClusterLib.Clustering;
using InterfaceClusterLib.EntryInterfaces;
using InterfaceClusterLib.InterfaceProcess;
using CrystalInterfaceLib.Settings;
using DbLib;

namespace InterfaceClusterLib.UserGroupInterfaces
{
	/// <summary>
	/// Common interfaces within a group
	/// </summary>
	public class CommonInterfacesFinder
	{
		public CommonInterfacesFinder()
		{
			// use PFAM definition
			ProtCidSettings.dataType = "pfam";
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        /// <param name="userGroupName"></param>
		public void FindCommonInterfaceInGroup (string[] pdbList, string userGroupName)
		{
			Initialize();

			ProtCidSettings.progressInfo.Reset ();
			int groupId = 111111;
			int[] updateGroups = new int [1];
			updateGroups[0] = groupId;

			ClearPreviousUserData (groupId);

			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("1. Classify PDB entries into homologous groups");
			HomoEntryClassifier entryClassifier = new HomoEntryClassifier (groupId);
			entryClassifier.ClassifyHomoEntries (pdbList, userGroupName);
			ProtCidSettings.progressInfo.currentOperationIndex ++;
			
			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("2. Build crystal, detect common interfaces in a group");
			HomoGroupInterfacesFinder interfaceFinder = new HomoGroupInterfacesFinder ();
			HomoGroupInterfacesFinder.modifyType = "update";
			interfaceFinder.DetectHomoGroupInterfaces (updateGroups);
			ProtCidSettings.progressInfo.currentOperationIndex ++;

			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("3. Generate interface files, calculate ASA in interfaces");
			CrystInterfaceProcessor interfaceProcessor = new CrystInterfaceProcessor ();
            interfaceProcessor.GenerateEntryInterfaceFiles(pdbList);
            ProtCidSettings.progressInfo.currentOperationIndex++;

			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("4. Compare cryst interfaces with PDB/PISA BUs and ASU Interfaces");
			EntryCrystBuInterfaceComp buInterfaceComp = new EntryCrystBuInterfaceComp ();
			buInterfaceComp.CompareEntryCrystBuInterfaces (pdbList);
			ProtCidSettings.progressInfo.currentOperationIndex ++;

			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("5. Deal With Redundant Crystal Forms.");
			RedundantCrystForms reduntCf = new RedundantCrystForms ();
			reduntCf.UpdateReduntCrystForms (updateGroups);
			NonredundantCfGroups nonreduntCfGroups = new NonredundantCfGroups ();
			nonreduntCfGroups.UpdateCfGroupInfo (updateGroups);
			ProtCidSettings.progressInfo.currentOperationIndex ++; 
			
			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("6. Cluster cryst common interfaces");
			InterfaceCluster interfaceCluster = new InterfaceCluster ();
			interfaceCluster.UpdateInterfaceClusters (updateGroups);
			ProtCidSettings.progressInfo.currentOperationIndex ++;

			ProtCidSettings.progressInfo.progStrQueue.Enqueue ("7. Print cryst common interfaces");
			ClusterStat clusterStat = new ClusterStat ();
            clusterStat.PrintCrystInterfaceClusters(groupId, ProtCidSettings.dataType);

			ProtCidSettings.progressInfo.threadFinished = true;
        }

        #region common interfaces for specific chains
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbList"></param>
        /// <param name="userGroupName"></param>
        public void FindCommonInterfaceInChainGroup(string[] pdbChainList, string userGroupName)
        {
            Initialize();

            ProtCidSettings.progressInfo.Reset();
            int groupId = 111111;
            int[] updateGroups = new int[1];
            updateGroups[0] = groupId;

            ClearPreviousUserData(groupId);

            string[] pdbList = GetEntriesFromChainList(pdbChainList);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("1. Classify PDB entries into homologous groups");
            HomoEntryClassifier entryClassifier = new HomoEntryClassifier(groupId);
            entryClassifier.ClassifyHomoEntries(pdbList, userGroupName);
            ProtCidSettings.progressInfo.currentOperationIndex++;

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("2. Build crystal, detect common interfaces in a group");
            HomoGroupInterfacesFinder interfaceFinder = new HomoGroupInterfacesFinder();
            HomoGroupInterfacesFinder.modifyType = "update";
            interfaceFinder.DetectHomoGroupInterfaces(updateGroups);
            ProtCidSettings.progressInfo.currentOperationIndex++;

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("3. Generate interface files, calculate ASA in interfaces");
            CrystInterfaceProcessor interfaceProcessor = new CrystInterfaceProcessor();
            interfaceProcessor.GenerateEntryInterfaceFiles(pdbList);
            ProtCidSettings.progressInfo.currentOperationIndex++;

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("4. Compare cryst interfaces with PDB/PQS/PISA BUs and ASU Interfaces");
            EntryCrystBuInterfaceComp buInterfaceComp = new EntryCrystBuInterfaceComp();
            buInterfaceComp.CompareEntryCrystBuInterfaces(pdbList);
            ProtCidSettings.progressInfo.currentOperationIndex++;

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("5. Deal With Redundant Crystal Forms.");
            RedundantCrystForms reduntCf = new RedundantCrystForms();
            reduntCf.UpdateReduntCrystForms(updateGroups);
            NonredundantCfGroups nonreduntCfGroups = new NonredundantCfGroups();
            nonreduntCfGroups.UpdateCfGroupInfo(updateGroups);
            ProtCidSettings.progressInfo.currentOperationIndex++;
            
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("6. Cluster cryst common interfaces");
            InterfaceCluster interfaceCluster = new InterfaceCluster();
            interfaceCluster.UpdateUserGroupInterfaceClusters(groupId, pdbChainList);
            ProtCidSettings.progressInfo.currentOperationIndex++;
           
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("7. Print cryst common interfaces");
            ClusterStat clusterStat = new ClusterStat();
            clusterStat.PrintCrystInterfaceClusters (groupId, ProtCidSettings.dataType);
          /*  int[] groups = new int[2];
            groups[0] = 111111;
            groups[1] = 111112;
            clusterStat.PrintUpdateCrystInterfaceClusters (groups, ProtCidSettings.dataType);*/
 
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("8. Copy interface files.");
            clusterStat.CopyInterfaceFilesWithSpecificChains (pdbChainList);

            ProtCidSettings.progressInfo.threadFinished = true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainList"></param>
        /// <returns></returns>
        private string[] GetEntriesFromChainList(string[] chainList)
        {
            List<string> entryList = new List<string> ();
            foreach (string chain in chainList)
            {
                if (! entryList.Contains(chain.Substring(0, 4)))
                {
                    entryList.Add(chain.Substring (0, 4));
                }
            }
            return entryList.ToArray ();
        }
        #endregion



		/// <summary>
		/// initialize dbconnect
		/// </summary>
        internal void Initialize()
        {
            ProtCidSettings.LoadDirSettings();
            AppSettings.LoadParameters();
            AppSettings.LoadSymOps();

            ProtCidSettings.pdbfamDbConnection = new DbConnect();
            ProtCidSettings.pdbfamDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                ProtCidSettings.dirSettings.pdbfamDbPath;

            ProtCidSettings.protcidDbConnection = new DbConnect();
            ProtCidSettings.protcidDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                ProtCidSettings.dirSettings.protcidDbPath;

            ProtCidSettings.buCompConnection = new DbConnect();
            ProtCidSettings.buCompConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                ProtCidSettings.dirSettings.baInterfaceDbPath;

            DataTables.GroupDbTableNames.SetGroupDbTableNames(ProtCidSettings.dataType);
        }

		/// <summary>
		/// clear the user-defined group 
		/// which are not supposed to stored in the database
		/// </summary>
		/// <param name="groupId"></param>
		private void ClearPreviousUserData (int groupId)
		{
			string[] tables = {"SgInterfaces", "ReduntCrystForms", "NonRedundantCfGroups", 
			"InterfaceClusters", "HomoSeqInfo", "HomoRepEntryAlign", "homoGroupEntryAlign", "Groups"};
			string deleteString = "";
			DbQuery dbQuery = new DbQuery ();
			foreach (string table in tables)
			{
				deleteString = string.Format ("DELETE FROM {0} Where GroupSeqID = {1};", 
					ProtCidSettings.dataType + table, groupId);
                ProtCidSettings.protcidQuery.Query( deleteString);
			}
		}
	}
}
