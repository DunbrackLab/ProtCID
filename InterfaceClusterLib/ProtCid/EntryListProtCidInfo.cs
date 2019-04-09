using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Settings;

namespace InterfaceClusterLib.ProtCid
{
    public class EntryListProtCidInfo
    {
        private DbQuery dbQuery = new DbQuery();
        private DbConnect dbConnectv26 = new DbConnect();

        /// <summary>
        /// 
        /// </summary>
        public void PrintEntryBuClusterInfo()
        {
            Initialize();

            string dataSrcDir = @"D:\DbProjectData\RolandData";
            string entryListFile = Path.Combine (dataSrcDir,  "ssgcid_pdblist.txt");
            string[] entries = ReadEntries(entryListFile);
            string outputFile = Path.Combine(dataSrcDir, "ssgcid_bucluster.txt");
            PrintEntryBuClusterInfo (entries, outputFile);
        }

        public void PrintEntryBuClusterInfo(string[] entries, string buOutputFile)
        {
            StreamWriter buInfoWriter = new StreamWriter(buOutputFile);
            buInfoWriter.WriteLine("PdbID\tPdbBuID\tPdbBU\tAuthOrSoftware\tPisaBuID\tPisaBU\t" +
                "InPdb\tInPisa\t" +
                "ChainPfamArchGroup\tClusterID\tM\tN\tASA\tMinSeqIdentity\t#PDB_M\t#PDB_N\t" +
                "#PdbBUs\t#PisaBUs");

     /*       StreamWriter clusterInfoWriter = new StreamWriter(clusterSumInfoFile);
            clusterInfoWriter.WriteLine("ChainPfamArchGroup\tClusterID\tM\tN\tASA\tMinSeqIdentity\t" + 
                "#PDB_M\t#PDB_N\t#PdbBUs\t#PisaBUs");*/

            string dataLine = "";
            foreach (string entry in entries)
            {
                dataLine = GetEntryBuClusterInfo(entry);
                buInfoWriter.WriteLine(dataLine);
                buInfoWriter.Flush();
            }
            buInfoWriter.Close();
         //   clusterInfoWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEntryBuClusterInfo(string pdbId)
        {
            DataTable entryBuTable = GetEntryBuInfoTable(pdbId);
            DataRow largestClusterRow = GetEntryLargestClusterInfo(pdbId);
            string clusterSumInfo = "";
            int[] clusterInterfaces = null;
            if (largestClusterRow != null)
            {
                clusterSumInfo = GetLargestClusterSumInfo(largestClusterRow);

                int superGroupId = Convert.ToInt32(largestClusterRow["SuperGroupSeqID"].ToString());
                int clusterId = Convert.ToInt32(largestClusterRow["ClusterID"].ToString());
                clusterInterfaces = GetEntryClusterInterfaces(superGroupId, clusterId, pdbId);
            }
            string buDataLine = "";
            string dataLine = "";
            string buStat = "";
            bool inPdb = false;
            bool inPisa = false;
            foreach (DataRow buRow in entryBuTable.Rows)
            {
                buStat = GetBuStatus(pdbId, buRow["PdbBuId"].ToString().TrimEnd());
                buDataLine = pdbId + "\t" + buRow["PdbBuID"].ToString().TrimEnd() + "\t" +
                    buRow["PdbBu_Abc"].ToString().TrimEnd() + "\t" +
                    buStat + "\t" +
                    buRow["PisaBuID"].ToString().TrimEnd() + "\t" +
                    buRow["PisaBu_Abc"].ToString().TrimEnd();

                inPdb = AreClusterInterfacesInBu(pdbId, buRow["PdbBuID"].ToString().TrimEnd(),
                    clusterInterfaces, "pdb");
                inPisa = AreClusterInterfacesInBu(pdbId, buRow["PisaBuID"].ToString().TrimEnd(),
                    clusterInterfaces, "pisa");
                if (inPdb)
                {
                    buDataLine = buDataLine + "\t1";
                }
                else
                {
                    buDataLine = buDataLine + "\t0";
                }
                if (inPisa)
                {
                    buDataLine = buDataLine + "\t1";
                }
                else
                {
                    buDataLine = buDataLine + "\t0";
                }
                if (clusterSumInfo != "")
                {
                    buDataLine = buDataLine + "\t" + clusterSumInfo;
                }
                dataLine += buDataLine + "\r\n";
            }
            return dataLine.TrimEnd("\r\n".ToCharArray ()); ;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetEntryClusterInterfaces(int superGroupId, int clusterId, string pdbId)
        {
            string queryString = string.Format("Select Distinct InterfaceID From PfamSuperClusterEntryInterfaces " + 
                " Where SuperGroupSeqID = {0} AND ClusterID = {1} AND PdbID = '{2}';",
                superGroupId, clusterId, pdbId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] clusterInterfaceIds = new int[clusterInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                clusterInterfaceIds[count] = Convert.ToInt32(interfaceRow["InterfaceID"].ToString ());
                count++;
            }
            return clusterInterfaceIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string GetChainRelation(int superGroupId)
        {
            string queryString = string.Format("Select ChainRelPfamArch From PfamSuperGroups Where SuperGroupSeqID = {0};",
                superGroupId);
            DataTable chainRelTable = ProtCidSettings.protcidQuery.Query( queryString);
            string chainRelPfamArch = "";
            if (chainRelTable.Rows.Count > 0)
            {
                chainRelPfamArch = chainRelTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
            }
            return chainRelPfamArch;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="buId"></param>
        /// <param name="clusterInterfaces"></param>
        /// <param name="pdbOrPisa"></param>
        /// <returns></returns>
        private bool AreClusterInterfacesInBu(string pdbId, string buId, int[] clusterInterfaces, string pdbOrPisa)
        {
            if (clusterInterfaces == null)
            {
                return false;
            }
            string queryString = string.Format("Select * From Cryst{0}BuInterfaceComp " + 
                " Where PdbId = '{1}' AND BuID = '{2}' AND InterfaceID IN ({3}) AND QScore >= {4};", 
                pdbOrPisa, pdbId, buId, ParseHelper.FormatSqlListString (clusterInterfaces),
                AppSettings.parameters.simInteractParam.interfaceSimCutoff);
            DataTable interfaceCompTable = dbQuery.Query(dbConnectv26, queryString);
            if (interfaceCompTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="pdbBuId"></param>
        /// <returns></returns>
        private string GetBuStatus(string pdbId, string pdbBuId)
        {
            string queryString = string.Format("Select * From PdbBuStat " + 
                " Where PdbID = '{0}' AND BiolUnitID = '{1}';", pdbId, pdbBuId);
            DataTable buStatTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (buStatTable.Rows.Count > 0)
            {
                return buStatTable.Rows[0]["Details"].ToString().TrimEnd();
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataRow GetEntryLargestClusterInfo(string pdbId)
        {
            string queryString = string.Format("Select Distinct SuperGroupSeqId, ClusterID From PfamSuperClusterEntryInterfaces " + 
                " Where PdbID = '{0}';", pdbId);
            DataTable clustersTable = ProtCidSettings.protcidQuery.Query( queryString);
            int superGroupId = 0;
            int clusterId = 0;
            DataRow largestClusterSumInfoRow = null;
            int largestM = 0;
            int m = 0;
            foreach (DataRow clusterRow in clustersTable.Rows)
            {
                superGroupId = Convert.ToInt32(clusterRow["SuperGroupSeqID"].ToString ());
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString ());
                DataTable clusterSumInfoTable = GetClusterSumInfoTable(superGroupId, clusterId);
                if (largestClusterSumInfoRow == null)
                {
                    largestClusterSumInfoRow = clusterSumInfoTable.Rows[0];
                }
                else
                {
                    largestM = Convert.ToInt32(largestClusterSumInfoRow["NumOfCfgCluster"].ToString ());
                    m = Convert.ToInt32(clusterSumInfoTable.Rows[0]["NumOfCfgCluster"].ToString());
                    if (largestM < m)
                    {
                        largestClusterSumInfoRow = clusterSumInfoTable.Rows[0];
                    }
                }
            }
            return largestClusterSumInfoRow;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="largestClusterRow"></param>
        /// <returns></returns>
        private string GetLargestClusterSumInfo(DataRow largestClusterRow)
        {
            string clusterSumInfo = "";
            if (largestClusterRow != null)
            {
                int superGroupId = Convert.ToInt32(largestClusterRow["SuperGroupSeqID"].ToString());

                string groupPfamRelation = GetChainRelation(superGroupId);
                clusterSumInfo = groupPfamRelation + "\t" +
                        largestClusterRow["ClusterID"].ToString() + "\t" +
                        largestClusterRow["NumOfCfgCluster"].ToString() + "\t" +
                        largestClusterRow["NumOfCfgFamily"].ToString() + "\t" +
                        largestClusterRow["SurfaceArea"].ToString() + "\t" +
                        largestClusterRow["MinSeqIdentity"].ToString() + "\t" +
                        largestClusterRow["NumOfEntryCluster"].ToString() + "\t" +
                        largestClusterRow["NumOfEntryFamily"].ToString() + "\t" +
                        largestClusterRow["InPdb"].ToString() + "\t" +
                        largestClusterRow["InPisa"].ToString();
            }
            return clusterSumInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private DataTable GetClusterSumInfoTable(int superGroupId, int clusterId)
        {
            string queryString = string.Format("Select * From PfamSuperClusterSumInfo " + 
                " Where SuperGroupSeqID = {0} AND ClusterID = {1};", superGroupId, clusterId);
            DataTable clusterSumInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            return clusterSumInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetEntryBuInfoTable(string pdbId)
        {
            string queryString = string.Format("Select * From ProtBuDBiolUnits Where PdbID = '{0}';", pdbId);
            DataTable entryBuInfoTable = dbQuery.Query(dbConnectv26, queryString);
            return entryBuInfoTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryListFile"></param>
        /// <returns></returns>
        private string[] ReadEntries(string entryListFile)
        {
            StreamReader entryReader = new StreamReader(entryListFile);
            string line = "";
            List<string> entryList = new List<string> ();
            while ((line = entryReader.ReadLine()) != null)
            {
                entryList.Add(line.ToLower ());
            }
            entryReader.Close();

            return entryList.ToArray ();
        }

        private void Initialize()
        {
            ProtCidSettings.LoadDirSettings();
            AppSettings.LoadParameters();

            if (ProtCidSettings.protcidDbConnection == null)
            {
                ProtCidSettings.protcidDbConnection = new DbConnect ("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.protcidDbPath);
            }
         
            string dbv26Path = @"F:\Firebird\Xtal\Pfam\protbud.fdb";
            dbConnectv26.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    dbv26Path;

        }
    }
}
