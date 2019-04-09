using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using System.Net;
using DbLib;
using ProtCidSettingsLib;
using InterfaceClusterLib.AuxFuncs;
using AuxFuncLib;
using CrystalInterfaceLib.DomainInterfaces;


namespace ProtCIDPaperDataLib
{
    public class ExternalClusterData
    {
        public DbQuery dbQuery = new DbQuery();
        private string dataDir = @"D:\Qifang\externalData\Hebrew_Yuval";
        private StreamWriter logWriter = null;
        private int numOfCfs = 5;
        private double minClusterSeqId = 90;
        private string webHttpAddress = "http://dunbrack2.fccc.edu/ProtCid/";
        private WebClient webClient = new WebClient();
        private double surfaceAreaCutoff = 300.0;
        private CmdOperations tarOperations = new CmdOperations();
        private string srcInterfaceFileDir = @"X:\Qifang\DbProjectData\InterfaceFiles\cryst";
        private string srcDomainInterfaceFileDir = @"X:\Qifang\DbProjectData\InterfaceFiles\pfamDomain";

        public ExternalClusterData()
        {
            Initialize();
        }

        #region yale_Jieming data
        public void PrintEntryProtCidClusterInfo()
        {
            logWriter = new StreamWriter("yaleLog.txt");
            StreamWriter entryClusterInfoWriter = new StreamWriter(Path.Combine (dataDir, "Jieming_protcidInfo.txt"));
            entryClusterInfoWriter.WriteLine("PDBID\tEntryPfamArch");
            entryClusterInfoWriter.WriteLine("PDBID\tGroup_Annotation\tGroupSeqID\tClusterID\t#CFs/Cluster\t" +
               "#CFs/Group\t#Entries/Cluster\t#Entries/Group\tInPDB\tInPISA\tInASU\tInterfaceType\tMinSeqIdentity\t" +
               "SurfaceArea_Avg\tInterfaceSurfaceArea");
            string[] entries = ReadEntries();
            string chainPfamArch = "";
            int superGroupId = 0;
            int clusterId = 0;
            Dictionary<int, string> chainPfamArchHash = new Dictionary<int,string> ();
            string dataLine = "";
            string entryPfamArch = "";
            foreach (string pdbId in entries)
            {
                entryPfamArch = GetEntryPfamArch(pdbId);
                entryClusterInfoWriter.WriteLine(pdbId + "\t" + entryPfamArch);
                if (entryPfamArch == "") // somehow, the entry is missing
                {
                    entryClusterInfoWriter.WriteLine("No common interfaces identified.");
                    continue;
                }
                DataRow[] clusterSumInfoRows = GetEntryClusterInfo(pdbId);
                if (clusterSumInfoRows == null || clusterSumInfoRows.Length == 0)
                {
                    entryClusterInfoWriter.WriteLine("No common interfaces identified.");
                    continue;
                }
                foreach (DataRow clusterSumInfoRow in clusterSumInfoRows)
                {
                    superGroupId = Convert.ToInt32(clusterSumInfoRow["SuperGroupSeqID"].ToString ());
                    clusterId = Convert.ToInt32(clusterSumInfoRow["ClusterID"].ToString ());
                    chainPfamArch = GetChainPfamArch(superGroupId, ref chainPfamArchHash);
                    dataLine = pdbId + "\t" + chainPfamArch + "\t" +
                        FormatClusterSumInfoLine(clusterSumInfoRow) + "\t" +
                        GetInterfaceSurfaceArea(superGroupId, clusterId, pdbId);
                    entryClusterInfoWriter.WriteLine(dataLine);
                }
                entryClusterInfoWriter.Flush();
            }
            entryClusterInfoWriter.Close();
        }

        private string GetInterfaceSurfaceArea(int superGroupSeqId, int clusterId, string pdbId)
        {
            string queryString = string.Format("Select SurfaceArea From PfamSuperClusterEntryInterfaces " + 
                " Where SuperGroupSeqID = {0} AND ClusterID = {1} AND PdbID = '{2}';", 
                superGroupSeqId, clusterId, pdbId);
            DataTable surfaceAreaTable = ProtCidSettings.protcidQuery.Query( queryString);
            double surfaceArea = 0;
            if (surfaceAreaTable.Rows.Count > 0)
            {
                surfaceArea = Convert.ToDouble(surfaceAreaTable.Rows[0]["SurfaceArea"].ToString ());
            }
            int intSurfaceArea = (int)surfaceArea;
            return intSurfaceArea.ToString();
        }
        private string GetChainPfamArch(int superGroupId, ref Dictionary<int, string> chainPfamArchHash)
        {
            string chainPfamArch = "";
            if (chainPfamArchHash.ContainsKey(superGroupId))
            {
                chainPfamArch = (string)chainPfamArchHash[superGroupId];
            }
            else
            {
                chainPfamArch = GetChainPfamArchRelationName (superGroupId);
                chainPfamArchHash.Add(superGroupId, chainPfamArch);
            }
            return chainPfamArch;
        }

        private DataRow[] GetEntryClusterInfo(string pdbId)
        {
            DataTable entryClusterInfoTable = GetEntryInterfaceClusterInfo (pdbId);
            int superGroupId = 0;
            int clusterId = 0;
            DataTable clusterSumInfoTable = null;
            foreach (DataRow clusterInfoRow in entryClusterInfoTable.Rows)
            {
                superGroupId = Convert.ToInt32(clusterInfoRow["SuperGroupSeqID"].ToString ());
                clusterId = Convert.ToInt32(clusterInfoRow["ClusterID"].ToString ());
                DataTable thisClusterSumInfoTable = GetClusterInfo(superGroupId, clusterId);
                if (clusterSumInfoTable == null)
                {
                    clusterSumInfoTable = thisClusterSumInfoTable.Copy();
                }
                else
                {
                    foreach (DataRow sumInfoRow in thisClusterSumInfoTable.Rows)
                    {
                        DataRow dataRow = clusterSumInfoTable.NewRow();
                        dataRow.ItemArray = sumInfoRow.ItemArray;
                        clusterSumInfoTable.Rows.Add(dataRow);
                    }
                }
            }
            if (clusterSumInfoTable == null)
            {
                return null;
            }
            DataRow[] clusterRows = GetSatisfiedClusters(clusterSumInfoTable);
            return clusterRows;
        }

        private DataTable GetClusterInfo(int superGroupId, int clusterId)
        {
            string queryString = string.Format("Select SuperGroupSeqID, ClusterID, InAsu, InPDB, InPISA, NumOfCfgCluster, " + 
                " NumOfEntryCluster, NumOfCfgFamily, NumOfEntryFamily, SurfaceArea, MinSeqIdentity, InterfaceType, ClusterInterface" + 
                " From PfamSuperClusterSumInfo " + 
                " Where SuperGroupSeqID = {0} AND ClusterID = {1};", superGroupId, clusterId);
            DataTable clusterSumInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            return clusterSumInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private DataTable GetClusterEntryInterfaceTable(string pdbId)
        {
            string queryString = string.Format("Select * From PfamSuperClusterEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return clusterInterfaceTable;
        }


        private DataTable GetEntryInterfaceClusterInfo(string pdbId)
        {
            string queryString = string.Format("Select Distinct SuperGroupSeqID, ClusterID From " + 
                " PfamSuperClusterEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable entryClusterInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            return entryClusterInfoTable;
        }

        public void PrintProtCidClusterInfo()
        {
            Initialize();

            logWriter = new StreamWriter("yaleLog.txt");
            StreamWriter entryClusterInfoWriter = new StreamWriter(Path.Combine (dataDir, "Jieming_Protcid.txt"));
            entryClusterInfoWriter.WriteLine("Group_Annotation\tGroupSeqID\tClusterID\t#CFs/Cluster\t" +
                "#CFs/Group\t#Entries/Cluster\t#Entries/Group\tInPDB\tInPISA\tInASU\tInterfaceType\t" + 
                "MinSeqIdentity\tSurfaceArea");
            StreamWriter entryProtCidInfoWriter = new StreamWriter(Path.Combine (dataDir, "EntryPfamCluster_yale.txt"));
            entryProtCidInfoWriter.WriteLine("PDBID\tGroup_Annotation\tGroupSeqID\tClusterID\t#CFs/Cluster\t" + 
                "#CFs/Group\t#Entries/Cluster\t#Entries/Group\tInPDB\tInPISA\tInASU\tInterfaceType\tMinSeqIdentity\t" + 
                "SurfaceArea\tEntryPfamArch\tInterfaceSurfaceArea");
            string[] entries = ReadEntries();

            Dictionary<int, List<string>> groupEntriesHash = GetEntryGroupHash(entries);
            string chainRelPfamArch = "";
           
            string clusterSumInfo = "";
            string entryClusterInfo = "";
            string pdbId = "";
            string entryPfamArch = "";
            List<string> entryWithClusterInfoList = new List<string>();
            List<string> addedEntryList = new List<string>();
            foreach (int superGroupSeqId in groupEntriesHash.Keys)
            {
                chainRelPfamArch = GetChainPfamArchRelationName(superGroupSeqId);
                DataTable clusterSumInfoTable = GetSuperGroupClusterInfo(superGroupSeqId);

                List<string> entryList = groupEntriesHash[superGroupSeqId];

                DataRow[] clusterRows = GetSatisfiedClusters(clusterSumInfoTable);
                foreach (DataRow clusterRow in clusterRows)
                {
                    addedEntryList.Clear();

                    clusterSumInfo = FormatClusterSumInfoLine(clusterRow);
                    clusterSumInfo = chainRelPfamArch + "\t" + clusterSumInfo;
                    entryClusterInfoWriter.WriteLine(clusterSumInfo);

                    DataTable clusterEntryInterfaceTable = GetClusterEntryInterfaces(superGroupSeqId);
                    foreach (DataRow interfaceRow in clusterEntryInterfaceTable.Rows)
                    {
                        pdbId = interfaceRow["PdbID"].ToString();
                        if (entryList.Contains(pdbId))
                        {
                            if (!entryWithClusterInfoList.Contains(pdbId))
                            {
                                entryWithClusterInfoList.Add(pdbId);
                            }
                            if (!addedEntryList.Contains(pdbId))
                            {
                                entryPfamArch = GetEntryPfamArch(pdbId);

                                addedEntryList.Add(pdbId);
                                entryClusterInfo = pdbId + "\t" +clusterSumInfo + "\t" + entryPfamArch + "\t" +
                                    GetSurfaceAreaString(Convert.ToDouble(interfaceRow["SurfaceArea"].ToString()));
                                entryProtCidInfoWriter.WriteLine(entryClusterInfo);
                            }
                        }
                    }
                }
            }
            entryClusterInfoWriter.Close();
            entryProtCidInfoWriter.Close();
            logWriter.WriteLine("Entries with no cluster info");
            foreach (string entry in entries)
            {
                if (!entryWithClusterInfoList.Contains(entry))
                {
                    logWriter.WriteLine(entry);
                }
            }
            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterSumInfoTable"></param>
        /// <returns></returns>
        private DataRow[] GetSatisfiedClusters(DataTable clusterSumInfoTable)
        {
            int numOfCfgsCluster = 0;
            double minSeqId = 0;
            List<DataRow> clusterRowList = new List<DataRow> ();
            foreach (DataRow clusterRow in clusterSumInfoTable.Rows)
            {
                numOfCfgsCluster = Convert.ToInt32(clusterRow["NumOfCfgCluster"].ToString());
                minSeqId = Convert.ToDouble(clusterRow["MinSeqIdentity"].ToString());
                if (numOfCfgsCluster >= 3 && minSeqId <= 90.0)
                {
                    clusterRowList.Add(clusterRow);
                }
            }
            if (clusterRowList.Count == 0)
            {
                if (clusterSumInfoTable.Rows.Count > 0)
                {
                    clusterRowList.Add(clusterSumInfoTable.Rows[0]);
                }
            }
            return clusterRowList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterRow"></param>
        /// <returns></returns>
        private string FormatClusterSumInfoLine(DataRow clusterRow)
        {
            double surfaceArea = Convert.ToDouble(clusterRow["SurfaceArea"].ToString ());
            string clusterSumInfo = clusterRow["SuperGroupSeqID"].ToString () + "\t" +
                clusterRow["ClusterID"].ToString () + "\t" +
                clusterRow["NumOfCfgCluster"].ToString () + "\t" +
                clusterRow["NumOfCfgFamily"].ToString () + "\t" +
                clusterRow["NumOfEntryCluster"].ToString () + "\t" +
                clusterRow["NumOfEntryFamily"].ToString () + "\t" +
                clusterRow["InPdb"].ToString () + "\t" +
                clusterRow["InPisa"].ToString () + "\t" +
                clusterRow["InAsu"].ToString () + "\t" +
                clusterRow["InterfaceType"].ToString () + "\t" +
                clusterRow["MinSeqIdentity"].ToString () + "\t" +
                GetSurfaceAreaString(surfaceArea);
            return clusterSumInfo;

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="surfaceArea"></param>
        /// <returns></returns>
        private string GetSurfaceAreaString(double surfaceArea)
        {
            int intSurfaceArea = (int)surfaceArea;
            return intSurfaceArea.ToString();
        }

        private string FormatClusterEntryInterface(DataRow clusterInterfaceRow)
        {
            string entryPfamArch = GetEntryPfamArch (clusterInterfaceRow["PdbID"].ToString ());

            string interfaceClusterInfo = clusterInterfaceRow["CfGroupID"].ToString () + "\t" +
                entryPfamArch + "\t" +
                clusterInterfaceRow["SpaceGroup"].ToString ().TrimEnd () + "\t" +
                clusterInterfaceRow["CrystForm"].ToString ().TrimEnd () + "\t" +
                clusterInterfaceRow["PdbID"].ToString () + "\t" + 
                clusterInterfaceRow["InPdb"].ToString () + "\t" +
                clusterInterfaceRow["InPisa"].ToString () + "\t" +
                clusterInterfaceRow["InAsu"].ToString () + "\t" +
                clusterInterfaceRow["PdbBu"].ToString () + "\t" +
                clusterInterfaceRow["PisaBu"].ToString () + "\t" +
                clusterInterfaceRow["SurfaceArea"].ToString () + "\t" +
                clusterInterfaceRow["Name"].ToString ().TrimEnd () + "\t" +
                clusterInterfaceRow["Species"].ToString ().TrimEnd () + "\t" +
                clusterInterfaceRow["UnpCode"].ToString ().TrimEnd ();
            return interfaceClusterInfo;  
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupSeqId"></param>
        /// <returns></returns>
        private DataTable GetSuperGroupClusterInfo(int superGroupSeqId)
        {
            string queryString = string.Format("Select * From PfamSuperClusterSumInfo " + 
                " WHERE SuperGroupSeqID = {0} Order By NumOfCfgCluster;", superGroupSeqId);
            DataTable clusterSumInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            return clusterSumInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupSeqId"></param>
        /// <returns></returns>
        private DataTable GetClusterEntryInterfaces(int superGroupSeqId)
        {
            string queryString = string.Format("Select * From PfamSuperClusterEntryInterfaces " + 
                " Where SuperGroupSeqID = {0} ORDER BY CfGroupID, PdbID, InterfaceID;", superGroupSeqId);
            DataTable clusterEntryInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return clusterEntryInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEntryPfamArch(string pdbId)
        {
            string entryPfamArch = "";
            /*    int groupSeqId = GetGroupSeqId(pdbId);
                string queryString = string.Format("Select EntryPfamArch From PfamGroups Where GroupSeqID = {0};", groupSeqId);
                DataTable entryPfamArchTable = dbQuery.Query(queryString);
                
                if (entryPfamArchTable.Rows.Count == 0)
                {*/
            string queryString = string.Format("Select * From PfamEntryPfamArch Where PdbID = '{0}';", pdbId);
            DataTable entryPfamArchTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (entryPfamArchTable.Rows.Count > 0)
            {
                entryPfamArch = entryPfamArchTable.Rows[0]["EntryPfamArch"].ToString().TrimEnd();
            }

            return entryPfamArch;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadEntries()
        {
          //  StreamReader dataReader = new StreamReader(Path.Combine (dataDir, "dimers.txt"));
            StreamReader dataReader = new StreamReader(Path.Combine(dataDir, "1636structures-new-oldverified.list"));
            string line = dataReader.ReadLine();
            List<string> entryList = new List<string>();
            while ((line = dataReader.ReadLine()) != null)
            {
                entryList.Add(line.Substring (0, 4).ToLower ());
            }
            dataReader.Close();
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupSeqId"></param>
        /// <returns></returns>
        private string GetChainPfamArchRelationName(int superGroupSeqId)
        {
            string queryString = string.Format("Select ChainRelPfamArch From PfamSuperGroups Where SuperGroupSeqID = {0};", superGroupSeqId);
            DataTable pfamArchRelTable = ProtCidSettings.protcidQuery.Query( queryString);
            string chainRelPfamArch = "";
            if (pfamArchRelTable.Rows.Count > 0)
            {
                chainRelPfamArch = pfamArchRelTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
            }
            return chainRelPfamArch;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private Dictionary<int, List<string>> GetEntryGroupHash(string[] entries)
        {
            Dictionary<int, List<string>> groupEntriesHash = new Dictionary<int,List<string>> ();
            if (File.Exists("SuperGroupEntries.txt"))
            {
                int superGroupId = 0;
                StreamReader dataReader = new StreamReader("SuperGroupEntries.txt");
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    string[] fields = line.Split(' ');
                    superGroupId = Convert.ToInt32(fields[0]);
                    List<string> entryList = new List<string> (fields);
                    entryList.RemoveAt(0);
                    groupEntriesHash.Add(superGroupId, entryList);
                }
                dataReader.Close();
            }
            else
            {
                StreamWriter dataWriter = new StreamWriter("SuperGroupEntries.txt");
                foreach (string pdbId in entries)
                {
                    int[] superGroupSeqIds = GetSuperGroupSeqId(pdbId);
                    if (superGroupSeqIds == null)
                    {
                        continue;
                    }
                    foreach (int superGroupSeqId in superGroupSeqIds)
                    {
                        if (groupEntriesHash.ContainsKey(superGroupSeqId))
                        {
                            groupEntriesHash[superGroupSeqId].Add(pdbId);
                        }
                        else
                        {
                            List<string> entryList = new List<string> ();
                            entryList.Add(pdbId);
                            groupEntriesHash.Add(superGroupSeqId, entryList);
                        }
                    }
                }
                string dataLine = "";
                foreach (int superGroupId in groupEntriesHash.Keys)
                {
                    dataLine = superGroupId + " ";
                    foreach (string entry in groupEntriesHash[superGroupId])
                    {
                        dataLine += (entry + " ");
                    }
                    dataWriter.WriteLine(dataLine.TrimEnd(' '));
                }
            }
            return groupEntriesHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int[] GetSuperGroupSeqId(string pdbId)
        {
            int groupSeqId = GetGroupSeqId(pdbId);
            if (groupSeqId == -1)
            {
                return null;
            }
            string queryString = string.Format("Select SuperGroupSeqID FROM PfamSuperGroups " + 
                " Where GroupSeqId = {0};", groupSeqId);
            DataTable superGroupSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<int> superGroupSeqIdList = new List<int> ();
            int superGroupSeqId = -1;
            foreach (DataRow superGroupSeqIdRow in superGroupSeqIdTable.Rows)
            {
                superGroupSeqId = Convert.ToInt32(superGroupSeqIdRow["SuperGroupSeqID"].ToString ());
                superGroupSeqIdList.Add(superGroupSeqId);
            }
            return superGroupSeqIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int GetGroupSeqId(string pdbId)
        {
            string queryString = string.Format("Select GroupSeqID From PfamHomoSeqInfo Where PdbID = '{0}';", pdbId);
            DataTable groupSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (groupSeqIdTable.Rows.Count == 0)
            {
                queryString = string.Format("Select GroupSeqID From PfamHomoRepEntryAlign Where PdbID2 = '{0}';", pdbId);
                groupSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            }
            int groupSeqId = -1;
            if (groupSeqIdTable.Rows.Count > 0)
            {
                groupSeqId = Convert.ToInt32(groupSeqIdTable.Rows[0]["GroupSeqID"].ToString ());
            }
            return groupSeqId;
        }


        /// <summary>
        /// initialize dbconnect
        /// </summary>
        private void Initialize()
        {
            if (ProtCidSettings.dirSettings == null)
            {
                ProtCidSettings.LoadDirSettings();

                ProtCidSettings.protcidDbConnection = new DbConnect ("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.protcidDbPath);
                ProtCidSettings.protcidQuery = new DbQuery(ProtCidSettings.protcidDbConnection);
                ProtCidSettings.pdbfamDbConnection = new DbConnect("DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                    ProtCidSettings.dirSettings.pdbfamDbPath);
                ProtCidSettings.pdbfamQuery = new DbQuery(ProtCidSettings.pdbfamDbConnection);
            }
        }

        public void CompressClusterData()
        {
      /*      string clusterCoordFileDir = @"D:\DbProjectData\InterfaceFiles_update\clusters20120706";
            string clusterSeqFileDir = @"D:\DbProjectData\PhylogeneticTree\fasta";
            StreamReader dataReader = new StreamReader(@"D:\externalData\yale\Jieming_protcidInfo.txt");
            string line = dataReader.ReadLine ();
            line = dataReader.ReadLine();
            string groupId = "";
            string clusterId = "";
            string interfaceClusterFileSrc = "";
            string interfaceClusterFileDest = "";
            string seqClusterFile = "";
            ArrayList clusterSeqFileList = new ArrayList();
            ArrayList clusterInterfaceFileList = new ArrayList();
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                
                if (fields.Length == 15)
                {
                    groupId = fields[2];
                    clusterId = fields[3];
                    interfaceClusterFileSrc = Path.Combine(clusterCoordFileDir, groupId + "_" + clusterId + ".tar.gz");
                    interfaceClusterFileDest = Path.Combine(dataDir, groupId + "_" + clusterId + ".tar.gz");
                    File.Copy(interfaceClusterFileSrc, interfaceClusterFileDest, true);
                    clusterInterfaceFileList.Add(groupId + "_" + clusterId + ".tar.gz");

                    string[] clusterSeqFiles = GetClusterSeqFiles(clusterSeqFileDir, groupId, clusterId);
                    clusterSeqFileList.AddRange(clusterSeqFiles);
                } 
            }
            dataReader.Close();
            string[] interfaceClusterFiles = new string[clusterInterfaceFileList.Count];
            clusterInterfaceFileList.CopyTo (interfaceClusterFiles);*/
            string[] interfaceClusterFullFiles = Directory.GetFiles(dataDir, "*.tar.gz");
            string[] interfaceClusterFiles = GetFileNames(interfaceClusterFullFiles);
            tarOperations.RunTar("Jieming_ClusterInterfaces.tar", interfaceClusterFiles, dataDir, false);

        //    string[] allClusterSeqFiles = new string[clusterSeqFileList.Count];
       //     clusterSeqFileList.CopyTo(allClusterSeqFiles);
            string[] allClusterSeqFullFiles = Directory.GetFiles(dataDir, "*.fasta");
            string[] allClusterSeqFiles = GetFileNames(allClusterSeqFullFiles);
            tarOperations.RunTar("Jieming_ClusterSeq.tar.gz", allClusterSeqFiles, dataDir, true);
        }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fullFileNames"></param>
    /// <returns></returns>
        private string[] GetFileNames(string[] fullFileNames)
        {
            string[] fileNames = new string[fullFileNames.Length];
            for (int i = 0; i < fullFileNames.Length; i++)
            {
                FileInfo fileInfo = new FileInfo (fullFileNames[i]);
                fileNames[i] = fileInfo.Name;
            }
            return fileNames;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqFileDir"></param>
        /// <param name="groupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string[] GetClusterSeqFiles (string seqFileDir, string groupId, string clusterId)
        {
            List<string> clusterSeqFileList = new List<string>();
            string clusterSeqFile  = Path.Combine (seqFileDir, "Cluster" + groupId + "A_" + clusterId + ".fasta");
            string clusterSeqFileDest = Path.Combine (dataDir, "Cluster" + groupId + "A_" + clusterId + ".fasta");
            if (File.Exists(clusterSeqFile))
            {
                File.Copy(clusterSeqFile, clusterSeqFileDest, true);
                clusterSeqFileList.Add("Cluster" + groupId + "A_" + clusterId + ".fasta");
            }
            clusterSeqFile = Path.Combine(seqFileDir, "Cluster" + groupId + "B_" + clusterId + ".fasta");
            if (File.Exists(clusterSeqFile))
            {
                clusterSeqFileDest = Path.Combine(dataDir, "Cluster" + groupId + "B_" + clusterId + ".fasta");
                File.Copy(clusterSeqFile, clusterSeqFileDest, true);
                clusterSeqFileList.Add("Cluster" + groupId + "B_" + clusterId + ".fasta");
            }
            return clusterSeqFileList.ToArray ();
        }
        #endregion

        #region MIT-Jeremy data
        public void PrintClusterBestInterfaces()
        {
            string dataDir = @"D:\externalData\MIT_Jeremy";

            string srcInterfaceFileDir = @"D:\DbProjectData\InterfaceFiles_update\cryst"; // hash
            string coordInterfaceFileDir = Path.Combine(dataDir, "ClusterRepInterfaceFiles");
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "ClusterRepEntryInterfaces.txt"));
            dataWriter.WriteLine("GroupID\tGroupDescription\tClusterID\tRepInterface\tSurfaceArea\tEntryPfamArch");
            string queryString = "Select SuperGroupSeqID, ClusterID From PfamSuperClusterSumInfo Where NumOfCfgCluster >= 4 AND MinSeqIdentity < 90 Order By SuperGroupSeqID, ClusterID;";
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            int superGroupSeqID = 0;
            int clusterId = 0;
            string clusterRepInterface = "";
            string groupDescript = "";
            Dictionary<int, string> groupStringHash = new Dictionary<int,string> ();
            string dataLine = "";
            double surfaceArea = -1;
            string entryPfamArch = "";
            string repInterfaceFile = "";
            string srcInterfaceFile = "";
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                superGroupSeqID = Convert.ToInt32(clusterRow["SuperGroupSeqID"].ToString());
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());
                clusterRepInterface = GetRepresentativeInterface(superGroupSeqID, clusterId);

                groupDescript = GetSuperGroupString(superGroupSeqID, groupStringHash);
                surfaceArea = GetInterfaceSurfaceArea(clusterRepInterface);
                entryPfamArch = GetEntryPfamArch(clusterRepInterface.Substring(0, 4));
                dataLine = superGroupSeqID.ToString() + "\t" + groupDescript + "\t" + clusterId.ToString() + "\t" +
                    clusterRepInterface + "\t" + string.Format("{0:0.##}", surfaceArea) + "\t" + entryPfamArch;
                dataWriter.WriteLine(dataLine);

                repInterfaceFile = Path.Combine(coordInterfaceFileDir, clusterRepInterface + ".cryst.gz");
                srcInterfaceFile = Path.Combine(srcInterfaceFileDir, clusterRepInterface.Substring(1, 2) + "\\" + clusterRepInterface + ".cryst.gz");
                File.Copy(srcInterfaceFile, repInterfaceFile, true);
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void AddClusterInfoToFile()
        {
       //     string dataDir = @"D:\externalData\MIT_Jeremy";
            string clusterRepInterfaceFile = Path.Combine(dataDir, "ClusterRepEntryInterfaces.txt");
            StreamReader dataReader = new StreamReader(clusterRepInterfaceFile);
            StreamWriter dataWriter = new StreamWriter(Path.Combine(dataDir, "ClusterRepEntryInterfacesClusterInfo.txt"));
            string line = dataReader.ReadLine(); // header line
            dataWriter.WriteLine(line + "\t#CfCluster\t#EntriesCluster\t#CfgGroup\t#EntriesGroup\t#PdbBAs\t#PisaBAs");
            int groupId = 0;
            int clusterId = 0;
            string dataLine = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                groupId = Convert.ToInt32(fields[0]);
                clusterId = Convert.ToInt32(fields[2]);
                DataRow clusterSumInfoRow = GetClusterSumInfoRow(groupId, clusterId);
                dataLine = line + "\t" + clusterSumInfoRow["NumOfCfgCluster"].ToString() + "\t" +
                    clusterSumInfoRow["NumOfEntryCluster"].ToString() + "\t" +
                    clusterSumInfoRow["NumOfCfgFamily"].ToString() + "\t" +
                    clusterSumInfoRow["NumOfEntryFamily"].ToString() + "\t" +
                    clusterSumInfoRow["InPdb"].ToString() + "\t" +
                    clusterSumInfoRow["InPisa"].ToString();
                dataWriter.WriteLine(dataLine);
            }
            dataReader.Close();
            dataWriter.Close();
        }

        private DataRow GetClusterSumInfoRow(int groupId, int clusterId)
        {
            string queryString = string.Format("Select * From PfamSuperClusterSumInfo Where SuperGroupSeqID = {0} AND ClusterID = {1};", groupId, clusterId);
            DataTable clusterSumInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (clusterSumInfoTable.Rows.Count > 0)
            {
                return clusterSumInfoTable.Rows[0];
            }
            return null;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string GetSuperGroupString(int superGroupId, Dictionary<int, string> groupStringHash)
        {
            string relChainPfamArch = "";
            if (groupStringHash.ContainsKey(superGroupId))
            {
                relChainPfamArch = (string)groupStringHash[superGroupId];
            }
            else
            {
                string querystring = string.Format("Select ChainRelPfamArch From PfamSuperGroups Where SuperGroupSeqID = {0};", superGroupId);
                DataTable chainRelTable = ProtCidSettings.protcidQuery.Query( querystring);
                if (chainRelTable.Rows.Count > 0)
                {
                    relChainPfamArch = chainRelTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
                }
                groupStringHash.Add(superGroupId, relChainPfamArch);
            }
            return relChainPfamArch;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private double GetInterfaceSurfaceArea(string entryInterface)
        {
            string[] fields = entryInterface.Split('_');
            string pdbId = fields[0];
            int interfaceId = Convert.ToInt32(fields[1]);
            string queryString = string.Format("Select SurfaceArea From CrystEntryInterfaces Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable saTable = ProtCidSettings.protcidQuery.Query( queryString);
            double surfaceArea = -1;
            if (saTable.Rows.Count > 0)
            {
                surfaceArea = Convert.ToDouble(saTable.Rows[0]["SurfaceArea"].ToString());
            }
            return surfaceArea;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
/*        private string GetEntryPfamArch(string pdbId)
        {
            string queryString = string.Format("Select EntryPfamArch From PfamEntryPfamArch Where PdbID = '{0}';", pdbId);
            DataTable entryPfamArchTable = dbQuery.Query(queryString);
            string entryPfamArch = "";
            if (entryPfamArchTable.Rows.Count > 0)
            {
                entryPfamArch = entryPfamArchTable.Rows[0]["EntryPfamArch"].ToString().TrimEnd();
            }
            return entryPfamArch;
        }*/
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string GetRepresentativeInterface(int superGroupId, int clusterId)
        {
            string queryString = string.Format("Select * From PfamSuperInterfaceClusters Where SuperGroupSeqID = {0} AND ClusterID = {1} Order By PdbID, InterfaceID;", superGroupId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> entryList = new List<string>();
            List<string> interfaceList = new List<string>();
            string pdbId = "";
            int interfaceId = 0;
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                if (entryList.Contains(pdbId))
                {
                    continue;
                }
                entryList.Add(pdbId);
                interfaceList.Add(pdbId + "_" + interfaceId.ToString());
            }
            string[] clusterInterfaces = new string[interfaceList.Count];
            interfaceList.CopyTo(clusterInterfaces);
            double bestQscore = 0;
            double qscore = 0;
            string bestEntryInterface = "";
            foreach (string entryInterface in clusterInterfaces)
            {
                qscore = GetEntryInterfaceQScoreSum(entryInterface, clusterInterfaces);
                if (bestQscore < qscore)
                {
                    bestQscore = qscore;
                    bestEntryInterface = entryInterface;
                }
            }
            return bestEntryInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryInterface"></param>
        /// <param name="clusterInterfaces"></param>
        /// <returns></returns>
        private double GetEntryInterfaceQScoreSum(string entryInterface, string[] clusterInterfaces)
        {
            string[] fields = entryInterface.Split('_');
            string pdbId = fields[0];
            int interfaceId = Convert.ToInt32(fields[1]);
            DataTable entryInterfaceCompTable = GetEntryInterfaceQscoreTable(pdbId, interfaceId);

            string clusterPdbId = "";
            int clusterInterfaceId = 0;

            double qscore = -1;
            double qscoreSum = 0;
            foreach (string clusterInterface in clusterInterfaces)
            {
                string[] clusterFields = clusterInterface.Split('_');
                clusterPdbId = clusterFields[0];
                if (clusterPdbId == pdbId)
                {
                    continue;
                }
                clusterInterfaceId = Convert.ToInt32(clusterFields[1]);
                qscore = GetInterfaceCompQscore(pdbId, interfaceId, clusterPdbId, clusterInterfaceId, entryInterfaceCompTable);
                if (qscore > -1)
                {
                    qscoreSum += qscore;
                }
            }
            return qscoreSum;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="interfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="interfaceId2"></param>
        /// <param name="interfaceCompTable"></param>
        /// <returns></returns>
        private double GetInterfaceCompQscore(string pdbId1, int interfaceId1, string pdbId2, int interfaceId2, DataTable interfaceCompTable)
        {
            double qscore = -1;
            DataRow[] interfaceCompRows = interfaceCompTable.Select(string.Format("PdbID1 = '{0}' AND InterfaceID1 = '{1}' " +
                " AND PdbID2 = '{2}' AND InterfaceID2 = '{3}'", pdbId1, interfaceId1, pdbId2, interfaceId2));
            if (interfaceCompRows.Length == 0)
            {
                interfaceCompRows = interfaceCompTable.Select(string.Format("PdbID1 = '{0}' AND InterfaceID1 = '{1}' " +
                " AND PdbID2 = '{2}' AND InterfaceID2 = '{3}'", pdbId2, interfaceId2, pdbId1, interfaceId1));
            }
            if (interfaceCompRows.Length > 0)
            {
                qscore = Convert.ToDouble(interfaceCompRows[0]["Qscore"]);
            }
            return qscore;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private DataTable GetEntryInterfaceQscoreTable(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select * From DifEntryInterfaceComp " +
                " Where (PdbID1 = '{0}' AND InterfaceID1 = {1}) OR (PdbID2='{0}' AND InterfaceID2 = {1});", pdbId, interfaceId);
            DataTable interfaceQscoreTable = ProtCidSettings.protcidQuery.Query( queryString);
            return interfaceQscoreTable;
        }
        #endregion

        #region clan entry list -- sam
        public void PrintEntryOfClans()
        {
            Initialize();

            StreamWriter dataWriter = new StreamWriter("ClanEntries_Sam.txt");
            string[] clanAccs = { "CL0016", "CL0072", "CL0361"};
            foreach (string clanAcc in clanAccs)
            {
                string[] clanEntries = GetClanEntries(clanAcc);
                dataWriter.WriteLine("#" + clanAcc);
                foreach (string pdbId in clanEntries)
                {
                    dataWriter.WriteLine(pdbId);
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clanAcc"></param>
        /// <returns></returns>
        private string[] GetClanEntries(string clanAcc)
        {
            string queryString = string.Format("Select Distinct PdbID From PdbPfam Where Pfam_ACC IN " +
                " (Select Distinct Pfam_ACC From PfamClanFamily Where Clan_Acc = '{0}');", clanAcc);
            DataTable clanEntryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] pdbIds = new string[clanEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in clanEntryTable.Rows)
            {
                pdbIds[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return pdbIds;
        }
        #endregion

        #region rosetta monomers
        /// <summary>
        /// monomers in all author-defined and pisa-defined biological assemblies
        /// </summary>
        public void PrintMonomers()
        {
            Initialize();

            StreamWriter dataWriter = new StreamWriter (Path.Combine (dataDir, "MonomersInPdb.txt"));
            dataWriter.WriteLine("PdbID\tMethod\tResolution\tDNA/RNA");
            List<string> nmrMonomerList = new List<string>();
            List<string> nonProtMonomerList = new List<string>();
            string pdbId = "";
            string method = "";
            string queryString = "Select PdbID, Method, Resolution From PdbEntry Where NumOfLigandAtoms = 0;"; // pdb structures with no ligands except water
            DataTable entryTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            bool isPisaMonomer = false;
            bool isPdbMonomer = false;
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString ();
                method = entryRow["Method"].ToString().TrimEnd();
                isPdbMonomer = IsEntryMonomerInPdb(pdbId);
                isPisaMonomer = IsEntryMonomerInPisa(pdbId);

                if (isPdbMonomer && isPisaMonomer)
                {
                    if (IsEntryProteinStructure(pdbId))
                    {
                  /*      if (method.IndexOf("NMR") > -1)
                        {
                            nmrMonomerList.Add(pdbId);
                        }
                        else
                        {
                            dataWriter.WriteLine(pdbId + "\t" + method);
                        }*/
                        dataWriter.WriteLine(pdbId + "\t" + method + "\t" + entryRow["Resolution"].ToString () + "\tProt");
                    }
                    else
                    {
                       /* nonProtMonomerList.Add(pdbId);*/
                        dataWriter.WriteLine(pdbId + "\t" + method + "\t" + entryRow["Resolution"].ToString () + "\tDNA/RNA");
                    }
                }
            }
    /*        if (nmrMonomerList.Count > 0)
            {
                dataWriter.WriteLine("NMR structures");
                foreach (string nmrPdbId in nmrMonomerList)
                {
                    dataWriter.WriteLine(nmrPdbId + "\tNMR");
                }
            }
            if (nonProtMonomerList.Count > 0)
            {
                dataWriter.WriteLine("DNA/RNA structures");
                foreach (string nonProtPdbId in nonProtMonomerList)
                {
                    dataWriter.WriteLine(nonProtPdbId);
                }
            }*/
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryProteinStructure (string pdbId)
        {
            string queryString = string.Format("Select PdbID, AsymID From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable asuTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            if (asuTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryMonomerInPdb (string pdbId)
        {
            string queryString = string.Format("Select * From PdbBuStat Where PdbID = '{0}';", pdbId);
            DataTable pdbBuStatTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            bool isMonomer = true;
            string oligomerStatus = "";
            foreach (DataRow buStatRow in pdbBuStatTable.Rows)
            {
                oligomerStatus = buStatRow["Oligomeric_details"].ToString().TrimEnd();
                if (oligomerStatus != "monomeric")  
                {
                    isMonomer = false;
                    break;
                }
            }
            return isMonomer;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryMonomerInPisa(string pdbId)
        {
            string querystring = string.Format("Select Formula_Abc From PisaAssembly Where PdbID = '{0}';", pdbId);
            DataTable entryAbcTable = ProtCidSettings.pdbfamQuery.Query( querystring);
            bool isMonomer = true;
            string abcFormat = "";
            foreach (DataRow abcRow in entryAbcTable.Rows)
            {
                abcFormat = abcRow["Formula_Abc"].ToString().TrimEnd();
                if (abcFormat != "A")
                {
                    isMonomer = false;
                    break;
                }
            }
            return isMonomer;
        }
        #endregion

        #region Hebrew-Yuval data
        /// <summary>
        /// 
        /// </summary>
        public void PrintClusterInfoForEntries()
        {
            StreamWriter chainDataWriter = new StreamWriter(Path.Combine(dataDir, "tanya_clusterInfo_chain.txt"));
            StreamWriter domainDataWriter = new StreamWriter(Path.Combine(dataDir, "tanya_clusterInfo_domain.txt"));
            string entryListFile = Path.Combine(dataDir, "tanya_list.txt");
            string[] entries = ReadEntries(entryListFile);
            foreach (string pdbId in entries)
            {
                string[] clusterInfoes = GetEntryClusterInfoes(pdbId);
                if (clusterInfoes.Length > 0)
                {
                    foreach (string clusterInfo in clusterInfoes)
                    {
                        chainDataWriter.WriteLine(pdbId + "\t" + clusterInfo);
                    }
                }
                else
                {
                    chainDataWriter.WriteLine(pdbId + "\t-");
                }
                string[] domainClusterInfos = GetEntryDomainClusterInfos(pdbId);
                if (domainClusterInfos.Length > 0)
                {
                    foreach (string clusterInfo in domainClusterInfos)
                    {
                        domainDataWriter.WriteLine(pdbId + "\t" + clusterInfo);
                    }
                }
                else
                {
                    domainDataWriter.WriteLine(pdbId + "\t-");
                }
                /*       for (clusterCount = 0; clusterCount < clusterInfoes.Length; clusterCount++)
                       {
                           dataLine = pdbId + "\t" + clusterInfoes[clusterCount] + "\t";
                           if (clusterCount < domainClusterInfos.Length)
                           {
                               dataLine += domainClusterInfos[clusterCount];
                           }
                           else
                           {
                               dataLine += "-";
                           }
                           dataWriter.WriteLine (dataLine);
                       }
                       while (clusterCount < domainClusterInfos.Length)
                       {
                           dataLine = pdbId + "\t-\t" + domainClusterInfos[clusterCount];
                           dataWriter.WriteLine(dataLine);
                           clusterCount++;
                       }*/
            }
            chainDataWriter.Close();
            domainDataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryClusterInfoes(string pdbId)
        {
            string queryString = string.Format("Select Distinct SuperGroupSeqID, ClusterID From PfamSuperClusterEntryInterfaces " +
                " Where PdbID = '{0}';", pdbId);
            DataTable groupClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            int superGroupId = 0;
            int clusterId = 0;
            string[] clusterInfoes = new string[groupClusterTable.Rows.Count];
            int count = 0;
            string groupPfamArch = "";
            Dictionary<int, string> groupPfamArchHash = new Dictionary<int,string> ();
            foreach (DataRow clusterRow in groupClusterTable.Rows)
            {
                superGroupId = Convert.ToInt32(clusterRow["SuperGroupSeqID"].ToString());
                groupPfamArch = GetSuperGroupString(superGroupId, groupPfamArchHash);
                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());
                clusterInfoes[count] = groupPfamArch + "\t" + GetClusterInfoString(superGroupId, clusterId);
                count++;
            }
            return clusterInfoes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryDomainClusterInfos(string pdbId)
        {
            string queryString = string.Format("Select Distinct RelSeqID, ClusterId From PfamDomainClusterInterfaces Where PdbID = '{0}';", pdbId);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;
            int clusterId = 0;
            List<string> relClusterInfoList = new List<string> ();
            Dictionary<int, string> relPfamArchHash = new Dictionary<int,string> ();
            string relPfamArch = "";
            foreach (DataRow relSeqIdRow in relSeqIdTable.Rows)
            {
                relSeqId = Convert.ToInt32(relSeqIdRow["RelSeqID"].ToString());
                clusterId = Convert.ToInt32(relSeqIdRow["ClusterID"].ToString());
                relPfamArch = GetRelationName(relSeqId, ref relPfamArchHash);
                string domainClusterInfo = relPfamArch + "\t" + GetDomainClusterInfo(relSeqId, clusterId);
                relClusterInfoList.Add(domainClusterInfo);
            }

            return relClusterInfoList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string GetDomainClusterInfo(int relSeqId, int clusterId)
        {
            DataTable domainClusterTable = GetRelationDomainClusterTable(relSeqId, clusterId);
            string domainClusterInfo = "-";
            if (domainClusterTable.Rows.Count > 0)
            {
                domainClusterInfo = ParseHelper.FormatDataRow(domainClusterTable.Rows[0]);
            }
            return domainClusterInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private DataTable GetRelationDomainClusterTable(int relSeqId, int clusterId)
        {
            string queryString = string.Format("Select RelSeqID As GroupID, ClusterId, SurfaceArea, NumOfCfgCluster, NumOfEntryCluster, " +
                " NumOfCfgRelation As NumOfCfgFamily, NumOfEntryRelation As NumOfEntryFamily, cast(InPdb as float)/cast(numOfEntryCluster as float)*100 AS PdbPercent, " +
                " cast(InPisa as float)/cast(numOfEntryCluster As float)*100 As PisaPercent, MinSeqIdentity, ClusterInterface From PfamDomainClusterSumInfo " +
                " Where RelSeqID = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable domainClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            return domainClusterTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string GetClusterInfoString(int superGroupId, int clusterId)
        {
            string clusterInfo = "-";
            DataTable clusterSumInfoTable = GetClusterInfo(superGroupId, clusterId);
            if (clusterSumInfoTable.Rows.Count > 0)
            {
                clusterInfo = ParseHelper.FormatDataRow(clusterSumInfoTable.Rows[0]);
            }
            return clusterInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryListFile"></param>
        /// <returns></returns>
        private string[] ReadEntries(string entryListFile)
        {
            List<string> entryList = new List<string>();
            StreamReader dataReader = new StreamReader(entryListFile);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                entryList.Add(line);
            }
            dataReader.Close();
            entryList.Sort();
            string[] entries = new string[entryList.Count];
            entryList.CopyTo(entries);
            return entries;
        }

        #region chain interface info
        /// <summary>
        /// /
        /// </summary>
        public void PrintPfamBioClusterInterfaces()
        {
            Initialize();
            logWriter = new StreamWriter(Path.Combine(dataDir, "HebrewLog.txt"));

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            string coordInterfaceFileDir = Path.Combine(dataDir, "ClusterCoordInterfaces");
            if (!Directory.Exists(coordInterfaceFileDir))
            {
                Directory.CreateDirectory(coordInterfaceFileDir);
            }
            string repInterfaceFileDir = Path.Combine(dataDir, "ClusterRepInterfaces");
            if (!Directory.Exists(repInterfaceFileDir))
            {
                Directory.CreateDirectory(repInterfaceFileDir);
            }
            StreamWriter clusterRepInterfaceDataWriter = new StreamWriter(Path.Combine(dataDir, "ClusterRepEntryInterfaces_all.txt"), true);
            StreamWriter clusterInterfaceDataWriter = new StreamWriter(Path.Combine(dataDir, "ClusterInterfaces_all.txt"), true);
            clusterRepInterfaceDataWriter.WriteLine("GroupID\tGroupDescription\tClusterID\tCfGroupID\t" +
                "RepInterface\tAuthorChain1_SymmetryOperator\tAuthorChain2_SymmetryOperator\tSurfaceArea\tEntryPfamArch\tDimerType\t" +
                "#Cf/Cluster\t#Entries/Cluster\t#Cf/Group\t#Entries/Group\t#PdbBAs\t#PisaBAs\tClusterDimerType\t");
            clusterInterfaceDataWriter.WriteLine("GroupID\tGroupDescription\tClusterID\tCfGroupID\tInterface\tAuthorChain1_SymOp\tAuthorChain2_SymOp" +
                "\tSurfaceArea\tEntryPfamArch\tDimerType\t#CF\tInPDB\tInPISA\tInASU");
            string queryString = "Select SuperGroupSeqID, ClusterID From PfamSuperClusterSumInfo Order by SuperGroupSeqID, ClusterID;";
            //  string queryString = string.Format("Select SuperGroupSeqID, ClusterID From PfamSuperClusterSumInfo " +
            //   " Where NumOfCfgCluster >= {0} AND MinSeqIdentity < {1} AND SurfaceArea > {2} Order By SuperGroupSeqID, ClusterID;", 
            //   " Where NumOfCfgCluster >= {0} AND MinSeqIdentity < {1} Order By SuperGroupSeqID, ClusterID;", numOfCfs, minClusterSeqId);
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.totalOperationNum = clusterTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = clusterTable.Rows.Count;
            ProtCidSettings.progressInfo.currentOperationLabel = "ProtCid benchmarch data";


            int superGroupSeqID = 0;
            int clusterId = 0;
            string clusterRepInterface = "";
            string groupDescript = "";
            Dictionary<int, string> groupStringHash = new Dictionary<int,string> ();
            string clusterInterfaceDataLine = "";
            string clusterRepInterfaceDataLine = "";
            double surfaceArea = -1;
            string entryPfamArch = "";
            //   string repInterfaceFile = "";
            //   string srcInterfaceFile = "";
            string[] clusterInterfaces = null;
            List<string> repInterfaceFileList = new List<string>();
            List<string> addedEntryList = new List<string>();
            string pdbId = "";
            int interfaceId = 0;
            string dimerType = "";
            string dimerUnit = "";
            string clusterInterface = "";
            string interfaceChainDef = "";
            string inBAinfo = "";
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                addedEntryList.Clear();
                superGroupSeqID = Convert.ToInt32(clusterRow["SuperGroupSeqID"].ToString());

                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());

                ProtCidSettings.progressInfo.currentFileName = superGroupSeqID.ToString() + "_" + clusterId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    clusterRepInterface = GetRepresentativeInterface(superGroupSeqID, clusterId, out clusterInterfaces);

                    DataTable clusterInterfaceTable = GetClusterInterfacesTable(superGroupSeqID, clusterId);

                    groupDescript = GetSuperGroupString(superGroupSeqID, groupStringHash);

                    // exclude antibody groups
         /*           if (InterfaceClusterLib.ChainInterfaces.ChainInterfaceBuilder.antibodyGroups.Contains(groupDescript))
                    {
                        continue;
                    }*/

                    /*   DownloadClusterInterfaceCoordFiles(superGroupSeqID, clusterId, coordInterfaceFileDir);

                       
                       repInterfaceFile = Path.Combine(repInterfaceFileDir, clusterRepInterface + ".cryst.gz");
                       srcInterfaceFile = Path.Combine(srcInterfaceFileDir, clusterRepInterface.Substring(1, 2) + "\\" + clusterRepInterface + ".cryst.gz");
                       File.Copy(srcInterfaceFile, repInterfaceFile, true);
                    
                       repInterfaceFileList.Add(clusterRepInterface + ".cryst.gz");
    */
                    //   foreach (string clusterInterface in clusterInterfaces)
                    foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
                    {
                        pdbId = interfaceRow["PdbID"].ToString();
                        interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                        if (addedEntryList.Contains(pdbId))  // only use one interface from a structure in the same cluster
                        {
                            continue;
                        }
                        clusterInterface = pdbId + "_" + interfaceId.ToString();
                        interfaceChainDef = GetInterfaceDefintion(pdbId, interfaceId);
                        addedEntryList.Add(pdbId);
                        surfaceArea = GetInterfaceSurfaceArea(pdbId, interfaceId, clusterInterfaceTable);
                        entryPfamArch = GetEntryPfamArch(pdbId);
                        dimerUnit = interfaceRow["InterfaceUnit"].ToString().TrimEnd();
                        dimerType = GetDimerType(dimerUnit);
                        inBAinfo = GetInBAInfo(pdbId, interfaceId, clusterInterfaceTable);

                        DataRow clusterSumInfoRow = GetClusterSumInfoRow(superGroupSeqID, clusterId);

                        clusterInterfaceDataLine = superGroupSeqID.ToString() + "\t" + groupDescript + "\t" + clusterId.ToString() + "\t" +
                                interfaceRow["CfGroupID"].ToString() + "\t" +
                                clusterInterface + "\t" + interfaceChainDef + "\t" + string.Format("{0:0.##}", surfaceArea) + "\t" +
                                entryPfamArch + "\t" + dimerType + "\t" +
                                clusterSumInfoRow["NumOfCfgCluster"].ToString();

                        clusterInterfaceDataWriter.WriteLine(clusterInterfaceDataLine + "\t" + inBAinfo);

                        if (clusterInterface == clusterRepInterface)
                        {
                            clusterRepInterfaceDataLine = clusterInterfaceDataLine + "\t" +
                                clusterSumInfoRow["NumOfEntryCluster"].ToString() + "\t" +
                                clusterSumInfoRow["NumOfCfgFamily"].ToString() + "\t" +
                                clusterSumInfoRow["NumOfEntryFamily"].ToString() + "\t" +
                                clusterSumInfoRow["InPdb"].ToString() + "\t" +
                                clusterSumInfoRow["InPisa"].ToString() + "\t" +
                                //    dimerType + "\t" +
                                clusterSumInfoRow["InterfaceType"].ToString().TrimEnd();
                            clusterRepInterfaceDataWriter.WriteLine(clusterRepInterfaceDataLine);
                        }

                    }
                    clusterRepInterfaceDataWriter.WriteLine();
                    clusterInterfaceDataWriter.WriteLine();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupSeqID.ToString() + " " + clusterId.ToString() + " : " + ex.Message);
                    logWriter.WriteLine(superGroupSeqID.ToString() + " " + clusterId.ToString() + " : " + ex.Message);
                    logWriter.Flush();
                }
            }
            clusterRepInterfaceDataWriter.Close();
            clusterInterfaceDataWriter.Close();

            string[] repInterfaces = new string[repInterfaceFileList.Count];
            repInterfaceFileList.CopyTo(repInterfaces);

            tarOperations.RunTarOnFolder("ClusterRepInterfaces.tar", "ClusterRepInterfaces", dataDir, false);
            //     tarOperations.RunTarOnFolder("ClusterCoordInterfaces.tar", "ClusterCoordInterfaces", dataDir, false);

            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="clusterInterfaceTable"></param>
        /// <returns></returns>
        private string GetInBAInfo(string pdbId, int interfaceId, DataTable clusterInterfaceTable)
        {
            DataRow[] interfaceRows = clusterInterfaceTable.Select(string.Format("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
            string inBAInfo = "";
            if (interfaceRows.Length > 0)
            {
                inBAInfo = interfaceRows[0]["InPDB"].ToString() + "\t" +
                    interfaceRows[0]["InPISA"].ToString() + "\t" + interfaceRows[0]["InASU"].ToString();
            }
            return inBAInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="clusterInterfaceTable"></param>
        /// <returns></returns>
        private string GetDomainInterfaceInBAInfo(string pdbId, int domainInterfaceId, DataTable clusterInterfaceTable)
        {
            DataRow[] interfaceRows = clusterInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            string inBAInfo = "";
            if (interfaceRows.Length > 0)
            {
                inBAInfo = interfaceRows[0]["InPDB"].ToString() + "\t" +
                    interfaceRows[0]["InPISA"].ToString() + "\t" + interfaceRows[0]["InASU"].ToString();
            }
            return inBAInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private string GetInterfaceDefintion(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select AuthChain1, SymmetryString1, AuthChain2, SymmetryString2 " +
                " From CrystEntryInterfaces Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( queryString);
            string interfaceDef = "";
            if (interfaceDefTable.Rows.Count > 0)
            {
                interfaceDef = interfaceDefTable.Rows[0]["AuthChain1"].ToString().TrimEnd() + "_" +
                    interfaceDefTable.Rows[0]["SymmetryString1"].ToString().TrimEnd() + "\t" +
                    interfaceDefTable.Rows[0]["AuthChain2"].ToString().TrimEnd() + "_" +
                    interfaceDefTable.Rows[0]["SymmetryString2"].ToString().TrimEnd();
            }
            return interfaceDef;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private string[] GetSymmetryOperators(string pdbId, int interfaceId)
        {
            string[] symmetryStrings = new string[2];
            if (interfaceId == 0)
            {
                symmetryStrings[0] = "1_555";
                symmetryStrings[1] = "1_555";
            }
            else
            {
                string queryString = string.Format("Select SymmetryString1, SymmetryString2 " +
                    " From CrystEntryInterfaces Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
                DataTable interfaceDefTable = ProtCidSettings.protcidQuery.Query( queryString);

                if (interfaceDefTable.Rows.Count > 0)
                {
                    symmetryStrings[0] = interfaceDefTable.Rows[0]["SymmetryString1"].ToString().TrimEnd();
                    symmetryStrings[1] = interfaceDefTable.Rows[0]["SymmetryString2"].ToString().TrimEnd();
                }
            }
            return symmetryStrings;
        }

        /// <summary>
        /// /
        /// </summary>
        public void PrintPfamBioClusterInterfacesNoCoord()
        {
            Initialize();
            logWriter = new StreamWriter(Path.Combine(dataDir, "HebrewLog.txt"));

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            string coordInterfaceFileDir = Path.Combine(dataDir, "ClusterCoordInterfaces");
            if (!Directory.Exists(coordInterfaceFileDir))
            {
                Directory.CreateDirectory(coordInterfaceFileDir);
            }
            string repInterfaceFileDir = Path.Combine(dataDir, "ClusterRepInterfaces");
            if (!Directory.Exists(repInterfaceFileDir))
            {
                Directory.CreateDirectory(repInterfaceFileDir);
            }
            StreamWriter clusterRepInterfaceDataWriter = new StreamWriter(Path.Combine(dataDir, "ClusterRepEntryInterfaces.txt"), true);
            StreamWriter clusterInterfaceDataWriter = new StreamWriter(Path.Combine(dataDir, "ClusterInterfaces.txt"), true);
            clusterRepInterfaceDataWriter.WriteLine("GroupID\tGroupDescription\tClusterID\tCfGroupID\tRepInterface\tSurfaceArea\tEntryPfamArch\tDimerType\t" +
                "#Cf/Cluster\t#Entries/Cluster\t#Cf/Group\t#Entries/Group\t#PdbBAs\t#PisaBAs\tClusterDimerType");
            clusterInterfaceDataWriter.WriteLine("GroupID\tGroupDescription\tClusterID\tCfGroupID\tInterface\tSurfaceArea\tEntryPfamArch\tDimerType");
            string queryString = string.Format("Select SuperGroupSeqID, ClusterID From PfamSuperClusterSumInfo " +
                //   " Where NumOfCfgCluster >= {0} AND MinSeqIdentity < {1} AND SurfaceArea > {2} Order By SuperGroupSeqID, ClusterID;", 
             " Where NumOfCfgCluster >= {0} AND MinSeqIdentity < {1} Order By SuperGroupSeqID, ClusterID;", numOfCfs, minClusterSeqId);
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.totalOperationNum = clusterTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = clusterTable.Rows.Count;
            ProtCidSettings.progressInfo.currentOperationLabel = "ProtCid benchmarch data";


            int superGroupSeqID = 0;
            int clusterId = 0;
            string clusterRepInterface = "";
            string groupDescript = "";
            Dictionary<int, string> groupStringHash = new Dictionary<int,string> ();
            string clusterInterfaceDataLine = "";
            string clusterRepInterfaceDataLine = "";
            double surfaceArea = -1;
            string entryPfamArch = "";
            string repInterfaceFile = "";
            string srcInterfaceFile = "";
            string[] clusterInterfaces = null;
            List<string> repInterfaceFileList = new List<string>();
            List<string> addedEntryList = new List<string>();
            string pdbId = "";
            int interfaceId = 0;
            string dimerType = "";
            string dimerUnit = "";
            string clusterInterface = "";
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                addedEntryList.Clear();
                superGroupSeqID = Convert.ToInt32(clusterRow["SuperGroupSeqID"].ToString());

                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());

                ProtCidSettings.progressInfo.currentFileName = superGroupSeqID.ToString() + "_" + clusterId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    clusterRepInterface = GetRepresentativeInterface(superGroupSeqID, clusterId, out clusterInterfaces);

                    DataTable clusterInterfaceTable = GetClusterInterfacesTable(superGroupSeqID, clusterId);

                    groupDescript = GetSuperGroupString(superGroupSeqID, groupStringHash);

                    // exclude antibody groups
   /*                 if (InterfaceClusterLib.ChainInterfaces.ChainInterfaceBuilder.antibodyGroups.Contains(groupDescript))
                    {
                        continue;
                    }*/

                    DownloadClusterInterfaceCoordFiles(superGroupSeqID, clusterId, coordInterfaceFileDir);


                    repInterfaceFile = Path.Combine(repInterfaceFileDir, clusterRepInterface + ".cryst.gz");
                    srcInterfaceFile = Path.Combine(srcInterfaceFileDir, clusterRepInterface.Substring(1, 2) + "\\" + clusterRepInterface + ".cryst.gz");
                    File.Copy(srcInterfaceFile, repInterfaceFile, true);

                    repInterfaceFileList.Add(clusterRepInterface + ".cryst.gz");


                    //   foreach (string clusterInterface in clusterInterfaces)
                    foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
                    {
                        pdbId = interfaceRow["PdbID"].ToString();
                        interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                        if (addedEntryList.Contains(pdbId))  // only use one interface from a structure in the same cluster
                        {
                            continue;
                        }
                        clusterInterface = pdbId + "_" + interfaceId.ToString();
                        addedEntryList.Add(pdbId);
                        surfaceArea = GetInterfaceSurfaceArea(pdbId, interfaceId, clusterInterfaceTable);
                        entryPfamArch = GetEntryPfamArch(pdbId);
                        dimerUnit = interfaceRow["InterfaceUnit"].ToString().TrimEnd();
                        dimerType = GetDimerType(dimerUnit);
                        clusterInterfaceDataLine = superGroupSeqID.ToString() + "\t" + groupDescript + "\t" + clusterId.ToString() + "\t" +
                                 interfaceRow["CfGroupID"].ToString() + "\t" +
                                 clusterInterface + "\t" + string.Format("{0:0.##}", surfaceArea) + "\t" +
                                 entryPfamArch + "\t" + dimerType;

                        if (clusterInterface == clusterRepInterface)
                        {
                            DataRow clusterSumInfoRow = GetClusterSumInfoRow(superGroupSeqID, clusterId);
                            clusterRepInterfaceDataLine = clusterInterfaceDataLine + "\t" + clusterSumInfoRow["NumOfCfgCluster"].ToString() + "\t" +
                                clusterSumInfoRow["NumOfEntryCluster"].ToString() + "\t" +
                                clusterSumInfoRow["NumOfCfgFamily"].ToString() + "\t" +
                                clusterSumInfoRow["NumOfEntryFamily"].ToString() + "\t" +
                                clusterSumInfoRow["InPdb"].ToString() + "\t" +
                                clusterSumInfoRow["InPisa"].ToString() + "\t" +
                                //    dimerType + "\t" +
                                clusterSumInfoRow["InterfaceType"].ToString().TrimEnd();
                            clusterRepInterfaceDataWriter.WriteLine(clusterRepInterfaceDataLine);
                        }
                        clusterInterfaceDataWriter.WriteLine(clusterInterfaceDataLine);
                    }
                    clusterRepInterfaceDataWriter.WriteLine();
                    clusterInterfaceDataWriter.WriteLine();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupSeqID.ToString() + " " + clusterId.ToString() + " : " + ex.Message);
                    logWriter.WriteLine(superGroupSeqID.ToString() + " " + clusterId.ToString() + " : " + ex.Message);
                    logWriter.Flush();
                }
            }
            clusterRepInterfaceDataWriter.Close();
            clusterInterfaceDataWriter.Close();

            string[] repInterfaces = new string[repInterfaceFileList.Count];
            repInterfaceFileList.CopyTo(repInterfaces);

            tarOperations.RunTarOnFolder("ClusterRepInterfaces.tar", "ClusterRepInterfaces", dataDir, false);
            tarOperations.RunTarOnFolder("ClusterCoordInterfaces.tar", "ClusterCoordInterfaces", dataDir, false);

            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private DataTable GetClusterInterfacesTable(int superGroupId, int clusterId)
        {
            string queryString = string.Format("Select * From PfamSuperClusterEntryInterfaces " +
                " Where SuperGroupSeqID = {0} AND ClusterID = {1} Order By PdbID, InterfaceID;",
                superGroupId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return clusterInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private DataTable GetClusterDomainInterfacesTable(int relSeqId, int clusterId)
        {
            string queryString = string.Format("Select * From PfamDomainClusterInterfaces " +
                " Where RelSeqID = {0} AND ClusterID = {1} Order By PdbID, DomainInterfaceID;",
                relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            return clusterInterfaceTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dimerUnit"></param>
        /// <returns></returns>
        private string GetDimerType(string dimerUnit)
        {
            if (dimerUnit == "A2")
            {
                return "S";
            }
            else if (dimerUnit.IndexOf("-") > -1)
            {
                return "I";
            }
            else
            {
                return "D";
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="clusterInterfaceTable"></param>
        /// <returns></returns>
        private double GetInterfaceSurfaceArea(string pdbId, int interfaceId, DataTable clusterInterfaceTable)
        {
            DataRow[] interfaceRows = clusterInterfaceTable.Select(string.Format("PdbID = '{0}' AND InterfaceID = '{1}'", pdbId, interfaceId));
            double surfaceArea = -1;
            if (interfaceRows.Length > 0)
            {
                surfaceArea = Convert.ToDouble(interfaceRows[0]["SurfaceArea"].ToString());
            }
            return surfaceArea;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="clusterId"></param>
        /// <param name="destDir"></param>
        private void DownloadClusterInterfaceCoordFiles(int groupId, int clusterId, string destDir)
        {
            string fileHttpAddress = webHttpAddress + "CrystInterfaces/clusters/" + groupId.ToString() + "_" + clusterId.ToString() + ".tar.gz";
            string destFile = Path.Combine(destDir, groupId.ToString() + "_" + clusterId.ToString() + ".tar.gz");
            webClient.DownloadFile(fileHttpAddress, destFile);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string GetRepresentativeInterface(int superGroupId, int clusterId, out string[] clusterInterfaces)
        {
            string queryString = string.Format("Select * From PfamSuperInterfaceClusters Where SuperGroupSeqID = {0} AND ClusterID = {1} Order By PdbID, InterfaceID;", superGroupId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> entryList = new List<string>();
            List<string> interfaceList = new List<string>();
            string pdbId = "";
            int interfaceId = 0;
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                interfaceId = Convert.ToInt32(interfaceRow["InterfaceID"].ToString());
                if (entryList.Contains(pdbId))
                {
                    continue;
                }
                entryList.Add(pdbId);
                interfaceList.Add(pdbId + "_" + interfaceId.ToString());
            }
            clusterInterfaces = new string[interfaceList.Count];
            interfaceList.CopyTo(clusterInterfaces);
            double bestQscore = 0;
            double qscore = 0;
            string bestEntryInterface = "";
            foreach (string entryInterface in clusterInterfaces)
            {
                qscore = GetEntryInterfaceQScoreSum(entryInterface, clusterInterfaces);
                if (bestQscore < qscore)
                {
                    bestQscore = qscore;
                    bestEntryInterface = entryInterface;
                }
            }
            return bestEntryInterface;
        }
        #endregion

        #region domain info
        DataTable asymAuthSeqTable = null;
        /// <summary>
        /// /
        /// </summary>
        public void PrintPfamDomainClusterInterfaces()
        {
            Initialize();
            logWriter = new StreamWriter(Path.Combine(dataDir, "HebrewLog.txt"));
            string queryString = "";

  //          queryString = "Select PdbID, AsymID, AuthorChain, NdbSeqNumbers, AuthSeqNumbers From AsymUnit Where PolymerType = 'polypeptide';";
  //          asymAuthSeqTable = dbQuery.Query(queryString);

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();

            StreamWriter clusterRepInterfaceDataWriter = new StreamWriter(Path.Combine(dataDir, "ClusterRepEntryInterfaces_domain.txt"));
            StreamWriter clusterInterfaceDataWriter = new StreamWriter(Path.Combine(dataDir, "ClusterInterfaces_domain.txt"));
            clusterRepInterfaceDataWriter.WriteLine("GroupID\tGroupDescription\tClusterID\tCfGroupID\t" +
                "RepInterface\tAuthorChain1_SymOp_SeqRange\tAuthorChain2_SymOp_SeqRange\tSurfaceArea\tEntryPfamArch\tDimerType\t" +
                "#Cf/Cluster\t#Entries/Cluster\t#Cf/Group\t#Entries/Group\t#PdbBAs\t#PisaBAs\tClusterDimerType\tAsymChain1_SymOp_SeqRange\tAsymChain2_SymOp_SeqRange");
            clusterInterfaceDataWriter.WriteLine("GroupID\tGroupDescription\tClusterID\tCfGroupID\tInterface\tAuthorChain1_SymOp_SeqRange\tAuthorChain2_SymOp_SeqRange" +
                "\tSurfaceArea\tEntryPfamArch\tDimerType\t#CF\tInPDB\tInPISA\tInASU\tAsymChain1_SymOp_SeqRange\tAsymChain2_SymOp_SeqRange");

            queryString = "Select RelSeqID, ClusterID From PfamDomainClusterSumInfo Order by RelSeqID, ClusterID;";
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);

            ProtCidSettings.progressInfo.totalOperationNum = clusterTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = clusterTable.Rows.Count;
            ProtCidSettings.progressInfo.currentOperationLabel = "ProtCid benchmarch data";


            int relSeqId = 0;
            int clusterId = 0;
            string clusterRepInterface = "";
            string relationDescript = "";
            Dictionary<int, string> relationStringHash = new Dictionary<int,string> ();
            string clusterInterfaceDataLine = "";
            string clusterRepInterfaceDataLine = "";
            double surfaceArea = -1;
            string entryPfamArch = "";
            string[] clusterInterfaces = null;
            List<string> addedEntryList = new List<string>();
            string pdbId = "";
            int domainInterfaceId = 0;
            string dimerType = "";
            string dimerUnit = "";
            string clusterInterface = "";
            string[] interfaceDomainDef = null;
            string inBAinfo = "";
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                addedEntryList.Clear();
                relSeqId = Convert.ToInt32(clusterRow["RelSeqId"].ToString());

                clusterId = Convert.ToInt32(clusterRow["ClusterID"].ToString());

                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString() + "_" + clusterId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                try
                {
                    clusterRepInterface = GetRepresentativeDomainInterface(relSeqId, clusterId, out clusterInterfaces);

                    DataTable clusterInterfaceTable = GetClusterDomainInterfacesTable(relSeqId, clusterId);

                    relationDescript = GetDomainRelationString(relSeqId, relationStringHash);

                    //   foreach (string clusterInterface in clusterInterfaces)
                    foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
                    {
                        pdbId = interfaceRow["PdbID"].ToString();
                        domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());
                       
                        if (addedEntryList.Contains(pdbId))  // only use one interface from a structure in the same cluster
                        {
                            continue;
                        }
                        clusterInterface = pdbId + "_" + domainInterfaceId.ToString();
                     
                        interfaceDomainDef = GetDomainInterfaceDefintion(pdbId, domainInterfaceId); // asym: 0; auth: 1
                        addedEntryList.Add(pdbId);
                        surfaceArea = GetDomainInterfaceSurfaceArea(pdbId, domainInterfaceId, clusterInterfaceTable);
                        entryPfamArch = GetEntryPfamArch(pdbId);
                        dimerUnit = interfaceRow["InterfaceUnit"].ToString().TrimEnd();
                        dimerType = GetDimerType(dimerUnit);
                        inBAinfo = GetDomainInterfaceInBAInfo(pdbId, domainInterfaceId, clusterInterfaceTable);

                        DataRow clusterSumInfoRow = GetDomainClusterSumInfoRow(relSeqId, clusterId);

                        clusterInterfaceDataLine = relSeqId.ToString() + "\t" + relationDescript + "\t" + clusterId.ToString() + "\t" +
                                interfaceRow["RelCfGroupID"].ToString() + "\t" +
                                clusterInterface + "\t" + interfaceDomainDef[1] + "\t" + string.Format("{0:0.##}", surfaceArea) + "\t" +
                                entryPfamArch + "\t" + dimerType + "\t" +
                                clusterSumInfoRow["NumOfCfgCluster"].ToString();

                        clusterInterfaceDataWriter.WriteLine(clusterInterfaceDataLine + "\t" + inBAinfo + "\t" + interfaceDomainDef[0]);

                        if (clusterInterface == clusterRepInterface)
                        {
                            clusterRepInterfaceDataLine = clusterInterfaceDataLine + "\t" +
                                clusterSumInfoRow["NumOfEntryCluster"].ToString() + "\t" +
                                clusterSumInfoRow["NumOfCfgRelation"].ToString() + "\t" +
                                clusterSumInfoRow["NumOfEntryRelation"].ToString() + "\t" +
                                clusterSumInfoRow["InPdb"].ToString() + "\t" +
                                clusterSumInfoRow["InPisa"].ToString() + "\t" +
                                //    dimerType + "\t" +
                            GetDomainClusterDimerTypes(clusterSumInfoRow);
                            clusterRepInterfaceDataWriter.WriteLine(clusterRepInterfaceDataLine + "\t" + interfaceDomainDef[0]);
                        }
                        addedEntryList.Add(pdbId);
                    }
                    clusterRepInterfaceDataWriter.WriteLine();
                    clusterInterfaceDataWriter.WriteLine();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() + " " + clusterId.ToString() + " : " + ex.Message);
                    logWriter.WriteLine(relSeqId.ToString() + " " + clusterId.ToString() + " : " + ex.Message);
                    logWriter.Flush();
                }
            }
            clusterRepInterfaceDataWriter.Close();
            clusterInterfaceDataWriter.Close();

            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainClusterSumRow"></param>
        /// <returns></returns>
        private string GetDomainClusterDimerTypes(DataRow domainClusterSumRow)
        {
            string dimerTypes = "";
            int numOfHomo = Convert.ToInt32(domainClusterSumRow["NumOfHomo"].ToString());
            int numOfHetero = Convert.ToInt32(domainClusterSumRow["NumOfHetero"].ToString());
            int numOfIntra = Convert.ToInt32(domainClusterSumRow["NumOfIntra"].ToString());
            if (numOfHomo > 0)
            {
                dimerTypes += "S,";
            }
            if (numOfHetero > 0)
            {
                dimerTypes += "D,";
            }
            if (numOfIntra > 0)
            {
                dimerTypes += "I";
            }
            return dimerTypes.TrimEnd(',');
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string GetDomainRelationString(int relSeqId, Dictionary<int, string> groupStringHash)
        {
            string relationString = "";
            if (groupStringHash.ContainsKey(relSeqId))
            {
                relationString = (string)groupStringHash[relSeqId];
            }
            else
            {
                string querystring = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
                DataTable chainRelTable = ProtCidSettings.protcidQuery.Query( querystring);
                string familyCode1 = "";
                string familyCode2 = "";
                if (chainRelTable.Rows.Count > 0)
                {
                    familyCode1 = chainRelTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
                    familyCode2 = chainRelTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
                    if (familyCode1 == familyCode2)
                    {
                        relationString = "(" + familyCode1 + ")";
                    }
                    else
                    {
                        relationString = "(" + familyCode1 + ");(" + familyCode2 + ")";
                    }
                }
                groupStringHash.Add(relSeqId, relationString);
            }
            return relationString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private DataRow GetDomainClusterSumInfoRow(int relSeqId, int clusterId)
        {
            string queryString = string.Format("Select * From PfamDomainClusterSumInfo Where RelSeqId = {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable clusterSumInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (clusterSumInfoTable.Rows.Count > 0)
            {
                return clusterSumInfoTable.Rows[0];
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="interfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="interfaceId2"></param>
        /// <param name="interfaceCompTable"></param>
        /// <returns></returns>
        private double GetDomainInterfaceCompQscore(string pdbId1, int interfaceId1, string pdbId2, int interfaceId2, DataTable interfaceCompTable)
        {
            double qscore = -1;
            DataRow[] interfaceCompRows = interfaceCompTable.Select(string.Format("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}' " +
                " AND PdbID2 = '{2}' AND DomainInterfaceID2 = '{3}'", pdbId1, interfaceId1, pdbId2, interfaceId2));
            if (interfaceCompRows.Length == 0)
            {
                interfaceCompRows = interfaceCompTable.Select(string.Format("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}' " +
                " AND PdbID2 = '{2}' AND DomainInterfaceID2 = '{3}'", pdbId2, interfaceId2, pdbId1, interfaceId1));
            }
            if (interfaceCompRows.Length > 0)
            {
                qscore = Convert.ToDouble(interfaceCompRows[0]["Qscore"]);
            }
            return qscore;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private string[] GetDomainInterfaceDefintion(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaces WHere PdbID = '{0}' AND DomainInterfaceID = {1};", pdbId, domainInterfaceId);
            DataTable domainInterfaceDefTable = ProtCidSettings.protcidQuery.Query( queryString);

            int interfaceId = 0;
            string[] symmetryOperators = null;
            int chainDomainId1 = 0;
            int chainDomainId2 = 0;
            string asymChain1 = "";
            string asymChain2 = "";
            string authChain1 = "";
            string authChain2 = "";
            string[] chainDomainRange1 = null;
            string[] chainDomainRange2 = null;
            string authDomainInterfaceChainA = "";
            string authDomainInterfaceChainB = "";
            string asymDomainInterfaceChainA = "";
            string asymDomainInterfaceChainB = "";
            List<string> asymDomainInterfaceChainAList = new List<string>();
            List<string> asymDomainInterfaceChainBList = new List<string>();
            foreach (DataRow domainInterfaceRow in domainInterfaceDefTable.Rows)
            {
                interfaceId = Convert.ToInt32(domainInterfaceRow["InterfaceID"].ToString());
                symmetryOperators = GetSymmetryOperators(pdbId, interfaceId);

                chainDomainId1 = Convert.ToInt32(domainInterfaceRow["ChainDomainID1"].ToString());
                chainDomainId2 = Convert.ToInt32(domainInterfaceRow["ChainDomainID2"].ToString());

                asymChain1 = domainInterfaceRow["AsymChain1"].ToString().TrimEnd();
                asymChain2 = domainInterfaceRow["AsymChain2"].ToString().TrimEnd();

                chainDomainRange1 = GetDomainAuthorRange (pdbId, chainDomainId1, asymChain1, out authChain1);
                if (! asymDomainInterfaceChainAList.Contains(asymChain1 + "_" + symmetryOperators[0] + ":" + chainDomainRange1[0]))
                {
                    asymDomainInterfaceChainA += (asymChain1 + "_" + symmetryOperators[0] + ":" + chainDomainRange1[0] + ";");
                    authDomainInterfaceChainA += (authChain1 + "_" + symmetryOperators[0] + ":" + chainDomainRange1[1] + ";");
                    asymDomainInterfaceChainAList.Add(asymChain1 + "_" + symmetryOperators[0] + ":" + chainDomainRange1[0]);
                }
                
                if (chainDomainId2 == chainDomainId1 && asymChain1 == asymChain2)
                {
                    if (!asymDomainInterfaceChainBList.Contains(asymChain1 + "_" + symmetryOperators[0] + ":" + chainDomainRange1[0]))
                    {
                        asymDomainInterfaceChainB += (asymChain1 + "_" + symmetryOperators[1] + ":" + chainDomainRange1[0] + ";");
                        authDomainInterfaceChainB += (authChain1 + "_" + symmetryOperators[1] + ":" + chainDomainRange1[1] + ";");
                        asymDomainInterfaceChainBList.Add(asymChain1 + "_" + symmetryOperators[0] + ":" + chainDomainRange1[0]);
                    }
                }
                else
                {
                    chainDomainRange2 = GetDomainAuthorRange(pdbId, chainDomainId2, asymChain2, out authChain2);
                    if (!asymDomainInterfaceChainBList.Contains(asymChain2 + "_" + symmetryOperators[1] + ":" + chainDomainRange2[0]))
                    {
                        asymDomainInterfaceChainB += (asymChain2 + "_" + symmetryOperators[1] + ":" + chainDomainRange2[0] + ";");
                        authDomainInterfaceChainB += (authChain2 + "_" + symmetryOperators[1] + ":" + chainDomainRange2[1] + ";");
                        asymDomainInterfaceChainBList.Add (asymChain2 + "_" + symmetryOperators[1] + ":" + chainDomainRange2[0]);
                    }
                }
            }
            string[] domainInterfaceDef = new string[2];
            domainInterfaceDef[0] = asymDomainInterfaceChainA.TrimEnd(';') + "\t" + asymDomainInterfaceChainB.TrimEnd(';');
            domainInterfaceDef[1] = authDomainInterfaceChainA.TrimEnd(';') + "\t" + authDomainInterfaceChainB.TrimEnd(';');
            return domainInterfaceDef;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private int[] GetDomainInterfaceChainDomainIds(DataTable domainInterfaceTable)
        {
            List<int> chainDomainList = new List<int>();
            int chainDomainId = 0;
            foreach (DataRow chainDomainRow in domainInterfaceTable.Rows)
            {
                chainDomainId = Convert.ToInt32(chainDomainRow["ChainDomainID"].ToString());
                if (!chainDomainList.Contains(chainDomainId))
                {
                    chainDomainList.Add(chainDomainId);
                }
            }
            return chainDomainList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainId"></param>
        /// <returns></returns>
 /*       private string GetDomainRange(string pdbId, int chainDomainId, string asymId, out string authChain)
        {
            string queryString = string.Format("Select AuthChain, SeqStart, SeqEnd From PdbPfam, PdbPfamChain" +
                " Where PdbPfam.PdbID = '{0}' AND PdbPfamChain.ChainDomainId = {1} AND PdbPfamChain.AsymChain = '{2}' AND PdbPfam.PdbID = PdbPfamChain.PdbID AND " +
                " PdbPfam.DomainID = PdbPfamChain.DomainID AND PdbPfam.EntityID = PdbPfamChain.EntityID;", pdbId, chainDomainId, asymId);
            DataTable seqRangeTable = dbQuery.Query(queryString);

            string domainRange = "";
            authChain = "";
            if (seqRangeTable.Rows.Count > 0)
            {
                authChain = seqRangeTable.Rows[0]["AuthChain"].ToString().TrimEnd();
            }
            foreach (DataRow seqRangeRow in seqRangeTable.Rows)
            {
                domainRange += ("[" + seqRangeRow["SeqStart"].ToString() + "-" + seqRangeRow["SeqEnd"].ToString() + "]");
            }
            return domainRange;
        }
        */
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainDomainId"></param>
        /// <param name="asymId"></param>
        /// <param name="authChain"></param>
        /// <returns></returns>
        private string[] GetDomainAuthorRange(string pdbId, int chainDomainId, string asymId, out string authChain)
        {
            string queryString = string.Format("Select AuthChain, SeqStart, SeqEnd From PdbPfam, PdbPfamChain" +
                " Where PdbPfam.PdbID = '{0}' AND PdbPfamChain.ChainDomainId = {1} AND PdbPfamChain.AsymChain = '{2}' AND PdbPfam.PdbID = PdbPfamChain.PdbID AND " +
                " PdbPfam.DomainID = PdbPfamChain.DomainID AND PdbPfam.EntityID = PdbPfamChain.EntityID;", pdbId, chainDomainId, asymId);
            DataTable seqRangeTable = ProtCidSettings.pdbfamQuery.Query( queryString);

            string[][] seqAuthNumbers = GetChainSeqNumberMap(pdbId, asymId);

            string authDomainRange = "";
            string asymDomainRange = "";
            authChain = "";
            if (seqRangeTable.Rows.Count > 0)
            {
                authChain = seqRangeTable.Rows[0]["AuthChain"].ToString().TrimEnd();
            }
            string seqStart = "";
            string seqEnd = "";
            string authSeqStart = "";
            string authSeqEnd = "";
            foreach (DataRow seqRangeRow in seqRangeTable.Rows)
            {
                seqStart = seqRangeRow["SeqStart"].ToString();
                seqEnd = seqRangeRow["SeqEnd"].ToString();
                authSeqStart = GetAuthSeqNumber(seqAuthNumbers, seqStart);
                authSeqEnd = GetAuthSeqNumber(seqAuthNumbers, seqEnd);
                authDomainRange += ("[" + authSeqStart + "-" + authSeqEnd + "]");
                asymDomainRange += ("[" + seqStart + "-" + seqEnd + "]");
            }
            string[] domainRanges = new string[2];
            domainRanges[0] = asymDomainRange;
            domainRanges[1] = authDomainRange;
            return domainRanges;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqAuthNumbers"></param>
        /// <param name="seqNumber"></param>
        /// <returns></returns>
        private string GetAuthSeqNumber(string[][] seqAuthNumbers, string seqNumber)
        {
            string authSeqNumber = "";
            int i = 0;
            for (i = 0; i < seqAuthNumbers[0].Length; i++)
            {
                if (seqAuthNumbers[0][i] == seqNumber)
                {
                    if (i < seqAuthNumbers[1].Length)
                    {
                        authSeqNumber = seqAuthNumbers[1][i];
                        while (authSeqNumber == "" && i - 1 < seqAuthNumbers[1].Length)
                        {
                            i++;
                            authSeqNumber = seqAuthNumbers[1][i];
                        }
                        break;
                    }
                    else
                    {
                        authSeqNumber = seqAuthNumbers[1][seqAuthNumbers[1].Length - 1];
                    }
                }
            }
            if (i == seqAuthNumbers[0].Length)
            {
                if (i > seqAuthNumbers[1].Length)
                {
                    i = seqAuthNumbers[1].Length;
                }
                authSeqNumber = seqAuthNumbers[1][i - 1];
            }
            return authSeqNumber;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymId"></param>
        /// <returns></returns>
        private string[][] GetChainSeqNumberMap(string pdbId, string asymId)
        {
            DataRow[] chainSeqNumberRows = null;
            if (asymAuthSeqTable != null)
            {
                chainSeqNumberRows = asymAuthSeqTable.Select(string.Format("PdbID = '{0}' AND AsymID = '{1}'", pdbId, asymId));
            }
            else
            {
                string queryString = string.Format("Select NDBSeqNumbers, AuthSeqNumbers From AsymUnit Where PdbID = '{0}' AND AsymID = '{1}';", pdbId, asymId);
                DataTable chainSeqNumbersTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                chainSeqNumberRows = chainSeqNumbersTable.Select();
            }
            string[] ndbNumbers = null;
            string[] authNumbers = null;
            string[][] seqNumbers = new string[2][];
            if (chainSeqNumberRows.Length > 0)
            {
                ndbNumbers = chainSeqNumberRows[0]["NDBSeqNumbers"].ToString().Split(',');
                authNumbers = chainSeqNumberRows[0]["AuthSeqNumbers"].ToString().Split(',');
                seqNumbers[0] = ndbNumbers;
                seqNumbers[1] = authNumbers;
            }
            return seqNumbers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <param name="clusterInterfaceTable"></param>
        /// <returns></returns>
        public double GetDomainInterfaceSurfaceArea(string pdbId, int domainInterfaceId, DataTable clusterInterfaceTable)
        {
            DataRow[] interfaceRows = clusterInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            double surfaceArea = -1;
            if (interfaceRows.Length > 0)
            {
                surfaceArea = Convert.ToDouble(interfaceRows[0]["SurfaceArea"].ToString());
            }
            return surfaceArea;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string GetRepresentativeDomainInterface(int relSeqId, int clusterId, out string[] clusterInterfaces)
        {
            string queryString = string.Format("Select * From PfamDomainClusterInterfaces Where RelSeqId = {0} AND ClusterID = {1} Order By PdbID, DomainInterfaceID;", relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> entryList = new List<string>();
            List<string> interfaceList = new List<string>();
            string pdbId = "";
            int interfaceId = 0;
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                interfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString());
                if (entryList.Contains(pdbId))
                {
                    continue;
                }
                entryList.Add(pdbId);
                interfaceList.Add(pdbId + "_" + interfaceId.ToString());
            }
            clusterInterfaces = new string[interfaceList.Count];
            interfaceList.CopyTo(clusterInterfaces);
            double bestQscore = 0;
            double qscore = 0;
            string bestEntryInterface = "";
            foreach (string entryInterface in clusterInterfaces)
            {
                qscore = GetDomainInterfaceQScoreSum(entryInterface, clusterInterfaces);
                if (bestQscore < qscore)
                {
                    bestQscore = qscore;
                    bestEntryInterface = entryInterface;
                }
            }
            return bestEntryInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryInterface"></param>
        /// <param name="clusterInterfaces"></param>
        /// <returns></returns>
        private double GetDomainInterfaceQScoreSum(string entryInterface, string[] clusterInterfaces)
        {
            string[] fields = entryInterface.Split('_');
            string pdbId = fields[0];
            int interfaceId = Convert.ToInt32(fields[1]);
            DataTable domainInterfaceCompTable = GetDomainInterfaceQscoreTable(pdbId, interfaceId);

            string clusterPdbId = "";
            int clusterInterfaceId = 0;

            double qscore = -1;
            double qscoreSum = 0;
            foreach (string clusterInterface in clusterInterfaces)
            {
                string[] clusterFields = clusterInterface.Split('_');
                clusterPdbId = clusterFields[0];
                if (clusterPdbId == pdbId)
                {
                    continue;
                }
                clusterInterfaceId = Convert.ToInt32(clusterFields[1]);
                qscore = GetDomainInterfaceCompQscore(pdbId, interfaceId, clusterPdbId, clusterInterfaceId, domainInterfaceCompTable);
                if (qscore > -1)
                {
                    qscoreSum += qscore;
                }
            }
            return qscoreSum;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private DataTable GetDomainInterfaceQscoreTable(string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaceComp " +
                " Where (PdbID1 = '{0}' AND DomainInterfaceID1 = {1}) OR (PdbID2='{0}' AND DomainInterfaceID2 = {1});", pdbId, domainInterfaceId);
            DataTable interfaceQscoreTable = ProtCidSettings.protcidQuery.Query( queryString);
            return interfaceQscoreTable;
        }
        #endregion
        
        #endregion

        #region Bronx-Jeff
        /// <summary>
        /// /
        /// </summary>
        /// <param name="clusterInfoRow"></param>
        /// <returns></returns>
        private string FormatClusterInfoRow(DataRow clusterInfoRow)
        {
            string clusterInfoString = "";
            int intNumber = 0;
            foreach (DataColumn dCol in clusterInfoRow.Table.Columns)
            {
                if (dCol.ColumnName == "PDBPERCENT" || dCol.ColumnName == "PISAPERCENT" || dCol.ColumnName == "SURFACEAREA")
                {
                    intNumber = (int)Convert.ToDouble(clusterInfoRow[dCol.ColumnName].ToString());
                    clusterInfoString += (intNumber.ToString() + "\t");
                }
                else
                {
                    clusterInfoString += clusterInfoRow[dCol.ColumnName].ToString() + "\t";
                }
            }
            clusterInfoString = clusterInfoString.TrimEnd('\t');
            return clusterInfoString;
        }
      

        #region sequence pfam info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="userSeqsAssigned"></param>
        /// <param name="userSeqs_all"></param>
        /// <returns></returns>
        private string[] GetSequencesNotAssigned(string[] userSeqsAssigned, string[] userSeqs_all)
        {
            StreamWriter notAssignSeqListWriter = new StreamWriter(Path.Combine(dataDir, "NotPfamAssignedSeqs.txt"));
            List<string> notAssignedSeqList = new List<string>();
            foreach (string userSeq in userSeqs_all)
            {
                if (!userSeqsAssigned.Contains(userSeq))
                {
                    notAssignedSeqList.Add(userSeq);
                    notAssignSeqListWriter.WriteLine(userSeq);
                }
            }
            notAssignSeqListWriter.Close();

            return notAssignedSeqList.ToArray ();
        }
          
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAssignRows"></param>
        /// <returns></returns>
        private string GetSequencePfamArch(DataRow[] pfamAssignRows)
        {
            string seqPfamArch = "";
            string pfamId = "";
            foreach (DataRow pfamAssignRow in pfamAssignRows)
            {
                pfamId = pfamAssignRow["Pfam_ID"].ToString().TrimEnd();
                seqPfamArch += ("(" + pfamId + ")_");
            }
            seqPfamArch = seqPfamArch.TrimEnd('_');
            return seqPfamArch;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ConcateSequences()
        {
            dataDir = @"D:\externalData\Bronx_Jeff\CE-sequences";

            string[] seqFiles = Directory.GetFiles(dataDir, "*.fasta");
            StreamWriter dataWriter = new StreamWriter(Path.Combine (dataDir, "CEsequences.txt"));
            StreamWriter lsFileWriter = new StreamWriter(Path.Combine (dataDir, "CESeqListFile.txt"));
           
            string seqLine = "";
            foreach (string seqFile in seqFiles)
            {
                seqLine = ReadSequenceFile(seqFile);
                dataWriter.WriteLine(seqLine);
                FileInfo fileInfo = new FileInfo(seqFile);
                lsFileWriter.WriteLine(fileInfo.Name);
            }
            dataWriter.Close();
            lsFileWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        public void RenameSeqFiles()
        {
            dataDir = @"D:\externalData\Bronx_Jeff\CE-sequences";
            string[] seqFiles = Directory.GetFiles(dataDir, "*.fa");
            string newSeqFile = "";
            foreach (string seqFile in seqFiles)
            {
                FileInfo fileInfo = new FileInfo(seqFile);
                string[] fileNameFields = fileInfo.Name.Split('.');
                if (fileNameFields.Length == 2)
                {
                    newSeqFile = fileNameFields[0] + ".fasta"; 
                }
                else if (fileNameFields.Length == 3)
                {
                    newSeqFile = fileNameFields[0] + "-" + fileNameFields[1] + ".fasta";
                }
                newSeqFile = Path.Combine(dataDir, newSeqFile);
                File.Copy(seqFile, newSeqFile);
                File.Delete(seqFile);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqFile"></param>
        /// <returns></returns>
        private string ReadSequenceFile(string seqFile)
        {
            StreamReader dataReader = new StreamReader(seqFile);
            string line = dataReader.ReadToEnd ();
            dataReader.Close();
            return line;
        }
        #endregion

        #region chain group ids and cluster info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="groupNameHash"></param>
        /// <returns></returns>
        private string GetGroupName(int superGroupId, ref Dictionary<int, string> groupNameHash)
        {
            string groupName = "-";
            if (groupNameHash.ContainsKey(superGroupId))
            {
                groupName = groupNameHash[superGroupId];
            }
            else
            {
                string queryString = string.Format("Select * From PfamSuperGroups Where SuperGroupSeqId = {0};", superGroupId);
                DataTable chainGroupTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (chainGroupTable.Rows.Count > 0)
                {
                    groupName = chainGroupTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
                }
                groupNameHash.Add(superGroupId, groupName);
            }
            return groupName;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private DataTable GetChainPfamGroupClusterInfo(int groupId)
        {
            string queryString = string.Format("Select SuperGroupSeqId As GroupID, ClusterId, SurfaceArea, NumOfCfgCluster, NumOfEntryCluster, " +
                " NumOfCfgFamily, NumOfEntryFamily, cast(InPdb as float)/cast(numOfEntryCluster as float)*100 AS PdbPercent, " +
                " cast(InPisa as float)/cast(numOfEntryCluster As float)*100 As PisaPercent, MinSeqIdentity, ClusterInterface From PfamSuperClusterSumInfo " +
                " Where SuperGroupSeqId = {0} AND NumOfCfgCluster >= {1} AND MinSeqIdentity <= {2}  AND SurfaceArea >= {3} " +
                " Order By NumOfCfgCluster DESC, NumOfEntryCluster DESC;", groupId, numOfCfs, minClusterSeqId, surfaceAreaCutoff);
            DataTable clusterInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (clusterInfoTable.Rows.Count == 0)
            {
                queryString = string.Format("Select First 1 SuperGroupSeqId As GroupID, ClusterId, SurfaceArea, NumOfCfgCluster, NumOfEntryCluster, " +
                        " NumOfCfgFamily, NumOfEntryFamily, cast(InPdb as float)/cast(numOfEntryCluster as float)*100 AS PdbPercent, " +
                        " cast(InPisa as float)/cast(numOfEntryCluster As float)*100 As PisaPercent, MinSeqIdentity, ClusterInterface From PfamSuperClusterSumInfo " +
                        " Where SuperGroupSeqId = {0} Order By NumOfCfgCluster DESC, NumOfEntryCluster DESC;", groupId);
                clusterInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            }
            return clusterInfoTable;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamArch"></param>
        /// <param name="seqPfamIds"></param>
        /// <returns></returns>
        private int[] FindPfamGroups(string pfamArch, string[] seqPfamIds)
        {
            int samePfamArchGroupId = FindPfamGroup(pfamArch);
            string[] combPfamArches = GetAllCombinations(seqPfamIds);
            List<int> combGroupIdList = new List<int> ();
            if (samePfamArchGroupId > 0)
            {
                combGroupIdList.Add(samePfamArchGroupId);
            }
            int combGroupId = 0;
            foreach (string combPfamArch in combPfamArches)
            {
                combGroupId = FindPfamGroup(combPfamArch);
                if (combGroupId > -1)
                {
                    if (!combGroupIdList.Contains(combGroupId))
                    {
                        combGroupIdList.Add(combGroupId);
                    }
                }
            }

            return combGroupIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqPfamIds"></param>
        /// <returns></returns>
        private string[] GetAllCombinations(string[] seqPfamIds)
        {
            List<string> pfamArchList = new List<string> ();
            GetAllCombinations(seqPfamIds, ref pfamArchList);
            string[] combPfamArches = new string[pfamArchList.Count];
            pfamArchList.CopyTo(combPfamArches);
            return combPfamArches;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqPfamIds"></param>
        /// <param name="pfamArchList"></param>
        private void GetAllCombinations(string[] seqPfamIds, ref List<string> pfamArchList)
        {
            if (seqPfamIds.Length == 1)
            {
                return;
            }
            string pfamArch = "";
            List<string> leftPfamIdList = new List<string> ();
            for (int i = 0; i < seqPfamIds.Length; i++)
            {
                leftPfamIdList.Clear();
                leftPfamIdList.AddRange(seqPfamIds);
                leftPfamIdList.RemoveAt(i);
                pfamArch = FormatSequencePfamArch(leftPfamIdList.ToArray ());
                pfamArchList.Add(pfamArch);
                GetAllCombinations(leftPfamIdList.ToArray (), ref pfamArchList);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamArch"></param>
        /// <returns></returns>
        private int FindPfamGroup(string pfamArch)
        {
            string queryString = string.Format("Select * From PfamSuperGroups Where ChainRelPfamArch = '{0}';", pfamArch);
            DataTable groupTable = ProtCidSettings.protcidQuery.Query( queryString);
            int groupId = -1;
            if (groupTable.Rows.Count > 0)
            {
                groupId = Convert.ToInt32(groupTable.Rows[0]["SuperGroupSeqID"].ToString());
            }
            return groupId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamAssignRows"></param>
        /// <returns></returns>
        private string[] GetSequencePfams(DataRow[] pfamAssignRows)
        {
            string pfamId = "";
            List<string> pfamIdList = new List<string> ();
            foreach (DataRow pfamAssignRow in pfamAssignRows)
            {
                pfamId = pfamAssignRow["Pfam_ID"].ToString();
                pfamIdList.Add(pfamId);
            }

            return pfamIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqPfamIds"></param>
        /// <returns></returns>
        private string FormatSequencePfamArch(string[] seqPfamIds)
        {
            string pfamArch = "";
            foreach (string pfamId in seqPfamIds)
            {
                pfamArch += ("(" + pfamId + ")_");
            }
            pfamArch = pfamArch.TrimEnd('_');
            return pfamArch;
        }
        #endregion

        #region domain group ids and cluster info
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="relationNameHash"></param>
        /// <returns></returns>
        private string GetRelationName(int relSeqId, ref Dictionary<int, string> relationNameHash)
        {
            string relationName = "-";
            if (relationNameHash.ContainsKey(relSeqId))
            {
                relationName = (string)relationNameHash[relSeqId];
            }
            else
            {
                string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
                DataTable pfamPairTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (pfamPairTable.Rows.Count > 0)
                {
                    relationName = "(" + pfamPairTable.Rows[0]["FamilyCode1"].ToString().TrimEnd() + ");(" +
                        pfamPairTable.Rows[0]["FamilyCode2"].ToString().TrimEnd() + ")";
                }
                relationNameHash.Add(relSeqId, relationName);
            }
            return relationName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private DataTable GetDomainClusterInfo(int relSeqId)
        {
            string queryString = string.Format("Select RelSeqID As GroupID, ClusterId, SurfaceArea, NumOfCfgCluster, NumOfEntryCluster, " +
                " NumOfCfgRelation As NumOfCfgFamily, NumOfEntryRelation As NumOfEntryFamily, cast(InPdb as float)/cast(numOfEntryCluster as float)*100 AS PdbPercent, " +
                " cast(InPisa as float)/cast(numOfEntryCluster As float)*100 As PisaPercent, MinSeqIdentity, ClusterInterface From PfamDomainClusterSumInfo " + 
                " Where RelSeqID = {0} AND NumOfCfgCluster >= {1} AND MinSeqIdentity <= {2}  AND SurfaceArea >= {3} " +
                " Order By NumOfCfgCluster DESC, NumOfEntryCluster DESC;", relSeqId, numOfCfs, minClusterSeqId, surfaceAreaCutoff);
            DataTable domainClusterTable = ProtCidSettings.protcidQuery.Query( queryString);

            if (domainClusterTable.Rows.Count == 0)
            {
                queryString = string.Format("Select First 1 RelSeqId As GroupID, ClusterId, SurfaceArea, NumOfCfgCluster, NumOfEntryCluster, " +
                        " NumOfCfgRelation As NumOfCfgFamily, NumOfEntryRelation As NumOfEntryFamily, cast(InPdb as float)/cast(numOfEntryCluster as float)*100 AS PdbPercent, " +
                        " cast(InPisa as float)/cast(numOfEntryCluster As float)*100 As PisaPercent, MinSeqIdentity, ClusterInterface From PfamDomainClusterSumInfo " +
                        " Where RelSeqID = {0} Order By NumOfCfgCluster DESC, NumOfEntryCluster DESC;", relSeqId);
                domainClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            }
            return domainClusterTable;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqPfamIds"></param>
        /// <returns></returns>
        private int[] GetDomainRelSeqIds(string[] seqPfamIds)
        {
            List<string> uniquePfamIdList = new List<string>();
            foreach (string pfamId in seqPfamIds)
            {
                if (!uniquePfamIdList.Contains(pfamId))
                {
                    uniquePfamIdList.Add(pfamId);
                }
            }
            List<int> relSeqIdList = new List<int> ();
            int relSeqId = 0;
            for (int i = 0; i < uniquePfamIdList.Count; i++)
            {
                for (int j = i; j < uniquePfamIdList.Count; j++)
                {
                    relSeqId = GetRelSeqId(uniquePfamIdList[i], uniquePfamIdList[j]);
                    if (relSeqId > 0)
                    {
                        if (!relSeqIdList.Contains(relSeqId))
                        {
                            relSeqIdList.Add(relSeqId);
                        }
                    }
                }
            }
            return relSeqIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId1"></param>
        /// <param name="pfamId2"></param>
        /// <returns></returns>
        private int GetRelSeqId(string pfamId1, string pfamId2)
        {
            string queryString = string.Format("Select RelSeqID From PfamDomainFamilyRelation Where (FamilyCode1 = '{0}' AND FamilyCode2 = '{1}') OR " + 
                " (FamilyCode1 = '{1}' AND FamilyCode2 = '{0}');", pfamId1, pfamId2);
            DataTable relSeqIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = -1;
            if (relSeqIdTable.Rows.Count > 0)
            {
                relSeqId = Convert.ToInt32(relSeqIdTable.Rows[0]["RelSeqID"].ToString ());
            }
            return relSeqId;
        }
        #endregion
        #endregion

        #region casp11
        private string httpAddress = "http://www.uniprot.org/uniprot/";
        private string unpTextFileDir = @"F:\Qifang\DbProjectData\Psiblast\unp\UnpTextFiles";
        string caspSeqFileAddress = "http://www.predictioncenter.org/casp11/target.cgi?target=T0759&view=sequence";
        public void FindSequenceForCaspStructures()
        {
            dataDir = @"C:\Paper\CASP11";
            StreamReader dataReader = new StreamReader(Path.Combine (dataDir, "target_all.txt"));
            StreamWriter seqWriter = new StreamWriter(Path.Combine (dataDir, "casp11_target_seqs.txt"));
            StreamWriter dataWriter = new StreamWriter(Path.Combine (dataDir, "Casp11_str_sequences.txt"));
            string line = "";
            bool isTarget = false;
            string dataLine = "";
            string pdbId = "";
            string[] unpSeqInfo = null;
            string targetSeq = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line == "")
                {
                    continue;
                }
                if (char.IsDigit(line[0]) && line.IndexOf (".") > -1)
                {
                    isTarget = true;
                    string[] numbers = GetTargetNumbers(line);
                    targetSeq = GetTargetSequence(numbers[1]);
                    seqWriter.WriteLine(targetSeq);
                    dataLine = numbers[0] + "\t" + numbers[1];
                    pdbId = "";
                }
                if (isTarget && line.IndexOf ("PDB code") > -1)
                {
                    pdbId = line.Substring(line.IndexOf("PDB code") + "PDB code".Length + 1, 4);
                    unpSeqInfo = GetStructureUnpSequences(pdbId);

                    if (unpSeqInfo[0] != "")
                    {
                        dataLine = ">" + unpSeqInfo[0] + "|" + unpSeqInfo[1] + " " + dataLine + "\t" + pdbId;
                    }
                    else
                    {
                        dataLine = ">" + dataLine + "\t" + pdbId;
                    }
                    dataWriter.WriteLine(dataLine);
                    dataWriter.WriteLine(unpSeqInfo[2]);

                    isTarget = false;
                }
            }
            dataWriter.Close();
            dataReader.Close();
            seqWriter.Close();
        }


        private string GetTargetSequence(string target)
        {
            string webSeqFile = caspSeqFileAddress + string.Format("target={0}&view=sequence", target);
            string seqFile = Path.Combine(dataDir, target + ".txt");
            byte[] data = webClient.DownloadData(webSeqFile);
            return data.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetStructureUnpSequences(string pdbId)
        {
            string queryString = string.Format("Select DBCode, DbAccession From PdbDbRefXml Where PdbID = '{0}' AND DBName = 'UNP';", pdbId);
            DataTable dbCodeTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string unpAccession = "";
            string unpCode = "";
            string sequence = "";
            if (dbCodeTable.Rows.Count > 0)
            {
                unpAccession = dbCodeTable.Rows[0]["DbAccession"].ToString();
                unpCode = dbCodeTable.Rows[0]["DbCode"].ToString();
                string unpFile = DownloadUnpTextFile(unpAccession);
                sequence = ParseUnpSequence(unpFile);
            }
            string[] unpSeqInfo = new string[3];
            unpSeqInfo[0] = unpAccession;
            unpSeqInfo[1] = unpCode;
            unpSeqInfo[2] = sequence;
            return unpSeqInfo;
        }

        private string ParseUnpSequence(string unpTextFile)
        {
            StreamReader dataReader = new StreamReader(unpTextFile);
            string line = "";
            bool isSeqStart = false;
            string sequence = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("SQ") > -1)
                {
                    isSeqStart = true;
                    continue;
                }
                if (line.Substring (0, 2) == "//")
                {
                    isSeqStart = false;
                }
                if (isSeqStart)
                {
                    sequence += line.Replace (" ", string.Empty);
                }
            }
            dataReader.Close();
            return sequence;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unpAccessionCode"></param>
        /// <returns></returns>
        public string DownloadUnpTextFile(string unpAccessionCode)
        {
            string unpTextFile = Path.Combine(unpTextFileDir, unpAccessionCode + ".txt");
            if (!File.Exists(unpTextFile))
            {
                webClient.DownloadFile(httpAddress + unpAccessionCode + ".txt\r\n", unpTextFile);
            }
            return unpTextFile;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private string[] GetTargetNumbers(string line)
        {
            string[] fields = line.Split("\t ".ToCharArray ());
            string[] numbers = new string[2];
            numbers[0] = fields[0].TrimEnd('.');
            int i = 1;
            while (fields[i] == "")
            {
                i++;
                continue;
            }
            numbers[1] = fields[i];
            return numbers;
        }
    
        /// <summary>
        /// 
        /// </summary>
        /// <param name="userSeqs"></param>
        /// <param name="pfamAssignTable"></param>
        public void FindPfamGroups(string[] userSeqs, DataTable pfamAssignTable, StreamWriter dataWriter, string destInterfaceFileDir)
        {
            string pfamArch = "";
            string dataLine = "";
            Dictionary<int, string> groupNameHash = new Dictionary<int,string> ();
            Dictionary<int, string> relationNameHash = new Dictionary<int,string> ();
            string groupName = "";
            string relationName = "";
            string srcInterfaceFile = "";
            string destInterfaceFile = "";
            string hashFolder = "";
            string clusterInterface = "";
            bool clusterExist = false;
            foreach (string userSeq in userSeqs)
            {
                DataRow[] pfamAssignRows = pfamAssignTable.Select(string.Format("SeqName = '{0}'", userSeq), "AlignStart ASC");
                string[] seqPfamIds = GetSequencePfams(pfamAssignRows);
                pfamArch = FormatSequencePfamArch(seqPfamIds);
                int[] pfamGroupSeqIds = FindPfamGroups(pfamArch, seqPfamIds);
                clusterExist = false;
                foreach (int groupId in pfamGroupSeqIds)
                {
                    DataTable chainClusterInfoTable = GetChainPfamGroupClusterInfo(groupId);
                    groupName = GetGroupName(groupId, ref groupNameHash);
                    foreach (DataRow clusterInfoRow in chainClusterInfoTable.Rows)
                    {
                        dataLine = userSeq + "\t" + pfamArch + "\t" + groupName + "\t" +
                            FormatClusterInfoRow(clusterInfoRow) + "\tChain";
                        dataWriter.WriteLine(dataLine);
                        clusterInterface = clusterInfoRow["ClusterInterface"].ToString().TrimEnd();
                        hashFolder = Path.Combine(srcInterfaceFileDir, clusterInterface.Substring(1, 2));
                        srcInterfaceFile = Path.Combine(hashFolder, clusterInterface + ".cryst.gz");
                        destInterfaceFile = Path.Combine(destInterfaceFileDir, clusterInterface + ".cryst.gz");
                        File.Copy(srcInterfaceFile, destInterfaceFile, true);
                    }
                    clusterExist = true;
                }

                int[] domainRelSeqIds = GetDomainRelSeqIds(seqPfamIds);
                foreach (int relSeqId in domainRelSeqIds)
                {
                    DataTable domainClusterInfoTable = GetDomainClusterInfo(relSeqId);
                    relationName = GetRelationName(relSeqId, ref relationNameHash);
                    foreach (DataRow clusterInfoRow in domainClusterInfoTable.Rows)
                    {
                        dataLine = userSeq + "\t" + pfamArch + "\t" + relationName + "\t" +
                            FormatClusterInfoRow(clusterInfoRow) + "\tDomain";
                        dataWriter.WriteLine(dataLine);

                        clusterInterface = clusterInfoRow["ClusterInterface"].ToString().TrimEnd();
                        hashFolder = Path.Combine(srcDomainInterfaceFileDir, clusterInterface.Substring(1, 2));
                        srcInterfaceFile = Path.Combine(hashFolder, clusterInterface + ".cryst.gz");
                        destInterfaceFile = Path.Combine(destInterfaceFileDir, clusterInterface + ".cryst.gz");
                        File.Copy(srcInterfaceFile, destInterfaceFile, true);
                    }
                    clusterExist = true;
                }
                if (!clusterExist)
                {
                    dataWriter.WriteLine(userSeq);
                }

                dataWriter.WriteLine();
                dataWriter.Flush();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadUserSequences(string seqListFile)
        {
            StreamReader dataReader = new StreamReader(seqListFile);
            string line = "";
            List<string> seqList = new List<string> ();
            string seqName = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                seqName = line.Replace(".fasta", "");
                seqList.Add(seqName);
            }
            dataReader.Close();
            return seqList.ToArray ();
        }
        #endregion

        #region get PDZ substrate proteins
        public void GetPDZSubstrateProteins()
        {
            Initialize();

            string dataDir = @"D:\DbProjectData\pfam\PfamPeptide\PepClusters_chain\PDZ\PDZ_1";
            string[] interfaceFiles = Directory.GetFiles(dataDir, "*.cryst");
            StreamWriter dataWriter = new StreamWriter(Path.Combine (dataDir, "HomoHeteroInterfaces_PDZ.txt"));
            string pdbId = "";
            foreach (string interfaceFile in interfaceFiles)
            {
                FileInfo fileInfo = new FileInfo(interfaceFile);
                if (fileInfo.Name.IndexOf("_d") > -1)
                {
                    continue;
                }
                pdbId = fileInfo.Name.Substring(0, 4);
                int[] chainEntities = GetInterfaceEntities(interfaceFile);
                if (chainEntities[0] == chainEntities[1])
                {
                    string[] protNames = GetUniProtPfamNames(pdbId, chainEntities[0]);
                    dataWriter.WriteLine(fileInfo.Name + "\thomo\t" + protNames[0] + "\t" + protNames[1]);
                }
                else
                {
                    string[] protNames1 = GetUniProtPfamNames(pdbId, chainEntities[0]);
                    string[] protNames2 = GetUniProtPfamNames(pdbId, chainEntities[1]);
                    dataWriter.WriteLine(fileInfo.Name + "\thetero\t" + 
                        protNames1[0] + "\t" + protNames1[1] + "\t" + protNames2[0] + "\t" + protNames2[1]);
                }
            }
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetUniProtPfamNames(string pdbId, int entityId)
        {
            string querystring = string.Format("Select DbCode From PdbDbRefSifts Where PdbID = '{0}' AND ENtityID = {1} AND DbName = 'UNP';", pdbId, entityId);
            DataTable dbCodeTable = ProtCidSettings.pdbfamQuery.Query( querystring);
            string unpCode = "";
            if (dbCodeTable.Rows.Count > 0)
            {
                unpCode = dbCodeTable.Rows[0]["DBCode"].ToString().TrimEnd();
            }

            querystring = string.Format("Select PfamArch From PfamEntityPfamArch Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable pfamArchTable = ProtCidSettings.pdbfamQuery.Query( querystring);
            string pfamArch = "";
            if (pfamArchTable.Rows.Count > 0)
            {
                pfamArch = pfamArchTable.Rows[0]["PfamArch"].ToString().TrimEnd();
            }

            string[] protNames = new string[2];
            protNames[0] = unpCode;
            protNames[1] = pfamArch;
            return protNames;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFile"></param>
        /// <returns></returns>
        private int[] GetInterfaceEntities(string interfaceFile)
        {
            int[] chainEntities = new int[2];
            StreamReader dataReader = new StreamReader(interfaceFile);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("Interface Chain A") > -1)
                {
                    chainEntities[0] = GetChainEntity(line);
                }
                else if (line.IndexOf("Interface Chain B") > -1)
                {
                    chainEntities[1] = GetChainEntity(line);
                    break;
                }
            }
            dataReader.Close();
            return chainEntities;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainLine"></param>
        /// <returns></returns>
        private int GetChainEntity(string chainLine)
        {
            int entityIndex = chainLine.IndexOf("Entity") + "Entity".Length;
            int entityEndIndex = chainLine.IndexOf("Symmetry Operator");
            int entityId = Convert.ToInt32(chainLine.Substring (entityIndex, entityEndIndex - entityIndex).Trim ());
            return entityId;
        }
        #endregion

        #region capri
        string[] capriStructures = {"4q28", "4q34", "4pwu", "4oju", "4q69", "4qhz", "4qb7", "4q9a", "4qdy", 
                                   "4qvu", "4piw", "4ojk", "4u13", "4wbt", "4urj", "4w66", "4wb1", "4w9r"};
        string[] capriStructsNoClusters = {"4q28", "4pwu", "4oju", "4q69", "4qb7", "4qdy", "4qvu", "4urj", "4w9r"};
        public void PrintFatcatAlignPair()
        {
            string alignFile = "CapriAlignPairsClan_sel.txt";
            StreamWriter dataWriter = new StreamWriter(alignFile);
            int superGroupId = 0;
            foreach (string capriStruct in capriStructsNoClusters)
            {
                superGroupId = GetEntrySuperGroupSeqId(capriStruct);
                if (superGroupId > -1)
                {
             //       GetComparedEntries(capriStruct, superGroupId, dataWriter);
                    GetComparedEntriesAtClan(capriStruct, superGroupId, dataWriter);
                }
            }
            dataWriter.Close();

            DivideNonAlignedChainPairs(alignFile, 1000);
            
        }

        public void FindClustersForEntries()
        {
            DbBuilderHelper.Initialize();
          /*  string nonAlignedPairFile = @"D:\DbProjectData\Fatcat\ChainAlignments\CapriAlignPairsClan.txt";
            InterfaceClusterLib.SuperInterfaceCluster.SuperGroupRepEntryComp repEntryComp = new InterfaceClusterLib.SuperInterfaceCluster.SuperGroupRepEntryComp();
            repEntryComp.CompareSpecificEntryPairs(nonAlignedPairFile);*/
           
            StreamWriter dataWriter = new StreamWriter("CapriClusterSumInfo_addclan.txt");
            string clusterSumInfo = "";
            string groupPfamArch = "";
            int superGroupId = 0;
            Dictionary<int, string> groupStringHash = new Dictionary<int,string> ();
            foreach (string capriStruct in capriStructures)
            {
                int[] superGroupIds = GetSuperGroupSeqId(capriStruct);
                superGroupId = superGroupIds[0];
                groupPfamArch = GetSuperGroupString(superGroupId, groupStringHash);
             //   clusterSumInfo = GetEntryClusterSumInfo(capriStruct);
                clusterSumInfo = GetSimEntryClusterSumInfo(capriStruct);
                dataWriter.WriteLine(capriStruct + "\t" + groupPfamArch);
                dataWriter.WriteLine(clusterSumInfo);
            }
            dataWriter.Close();
        }

        private string GetSimEntryClusterSumInfo(string pdbId)
        {
            string[] simInterfaces = GetSimilarInterfaces(pdbId);
            List<string> clusterList = new List<string>();
            string simPdbId = "";
            int simInterfaceId = 0;
            foreach (string simInterface in simInterfaces)
            {
                simPdbId = simInterface.Substring(0, 4);
                simInterfaceId = Convert.ToInt32(simInterface.Substring (4, simInterface.Length - 4));
                string clusterInfo = GetInterfaceCluster(simPdbId, simInterfaceId);
                if (clusterInfo != "")
                {
                    if (!clusterList.Contains(clusterInfo))
                    {
                        clusterList.Add(clusterInfo);
                    }
                }
            }
            int superGroupId = 0;
            int clusterId = 0;
            string dataLines = "";
            foreach (string clusterInfo in clusterList)
            {
                string[] fields = clusterInfo.Split('_');
                superGroupId = Convert.ToInt32(fields[0]);
                clusterId = Convert.ToInt32(fields[1]);
                DataTable clusterInfoTable =  GetClusterInfo(superGroupId, clusterId);
                dataLines += ParseHelper.FormatDataRows(clusterInfoTable.Select ());
                dataLines += "\r\n";
            }
            return dataLines;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private string GetInterfaceCluster(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select SuperGroupSeqId, ClusterID From PfamSuperClusterEntryInterfaces Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (clusterTable.Rows.Count > 0)
            {
                return clusterTable.Rows[0]["SuperGroupSeqID"].ToString() + "_" + clusterTable.Rows[0]["ClusterID"].ToString();
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetSimilarInterfaces(string pdbId)
        {
            List<string> simInterfaceList = new List<string> ();
            string queryString = string.Format("Select PdbID2, InterfaceID2 From DifEntryInterfaceComp WHere PdbID1 = '{0}' AND Qscore > 0.2;", pdbId);
            DataTable simInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            foreach (DataRow interfaceRow in simInterfaceTable.Rows)
            {
                simInterfaceList.Add(interfaceRow["PdbID2"].ToString() + interfaceRow["InterfaceID2"].ToString());
            }

            queryString = string.Format("Select PdbID1, InterfaceID1 From DifEntryInterfaceComp WHere PdbID2 = '{0}' AND Qscore > 0.2;", pdbId);
            simInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            foreach (DataRow interfaceRow in simInterfaceTable.Rows)
            {
                simInterfaceList.Add(interfaceRow["PdbID1"].ToString() + interfaceRow["InterfaceID1"].ToString());
            }

            return simInterfaceList.ToArray ();
        }

       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetEntryClusterSumInfo(string pdbId)
        {
            string queryString = string.Format ("Select Distinct SuperGroupSeqID, CLusterID From PfamSuperClusterEntryInterfaces Where PdbID = '{0}';", pdbId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int superGroupId = 0;
            int clusterId = 0;
            string dataLines = "";
            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                superGroupId = Convert.ToInt32 (clusterIdRow["SuperGroupSeqID"].ToString ());
                clusterId = Convert.ToInt32(clusterIdRow["ClusterID"].ToString ());
                dataLines += GetClusterInfo(superGroupId, clusterId);
            }
            return dataLines;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string GetClusterInfo(string pdbId)
        {
            int superGroupId = GetEntrySuperGroupSeqId(pdbId);
            string queryString = string.Format ("Select * From PfamSuperClusterSumInfo Where SuperGroupSeqID = {0};", superGroupId);
            DataTable sumInfoTable = ProtCidSettings.protcidQuery.Query( queryString);
            string dataLines = ParseHelper.FormatDataRows(sumInfoTable.Select ());
            return dataLines;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="nonAlignedChainPairsFile"></param>
        /// <param name="numOfChainPairsCutoff"></param>
        public void DivideNonAlignedChainPairs(string nonAlignedChainPairsFile, int numOfChainPairsCutoff)
        {
            StreamReader dataReader = new StreamReader(nonAlignedChainPairsFile);
            FileInfo fileInfo = new FileInfo(nonAlignedChainPairsFile);
            string pairFileName = fileInfo.Name.Replace(".txt", "");
            string lsFileName = Path.Combine(fileInfo.DirectoryName, "fileList.txt");
            StreamWriter lsFileWriter = new StreamWriter(lsFileName);
            string line = "";
            int fileNum = 0;
            int numOfChainPairs = 0;
            string dividedChainPairsFilePrefix = nonAlignedChainPairsFile.Remove(nonAlignedChainPairsFile.IndexOf(".txt"));
            StreamWriter dataWriter = new StreamWriter(dividedChainPairsFilePrefix + "0.txt");
            lsFileWriter.WriteLine(pairFileName + "0.txt");
            while ((line = dataReader.ReadLine()) != null)
            {
                numOfChainPairs++;
                if (numOfChainPairs > numOfChainPairsCutoff)
                {
                    dataWriter.Close();
                    fileNum++;
                    dataWriter = new StreamWriter(dividedChainPairsFilePrefix + fileNum.ToString() + ".txt");
                    lsFileWriter.WriteLine(pairFileName + fileNum.ToString() + ".txt");
                    numOfChainPairs = 0;
                }
                dataWriter.WriteLine(line);
            }
            dataReader.Close();
            dataWriter.Close();
            lsFileWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="superGroupId"></param>
        /// <param name="dataWriter"></param>
        private void GetComparedEntries(string pdbId, int superGroupId, StreamWriter dataWriter)
        {
            string queryString = string.Format("Select Distinct GroupSeqID From PfamSuperGroups Where SuperGroupSeqID = {0};", superGroupId);
            DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int groupId = 0;
            string[] capriChains = GetEntryAsymChains(pdbId);
            foreach (DataRow groupIdRow in groupIdTable.Rows)
            {
                groupId = Convert.ToInt32(groupIdRow["GroupSeqID"].ToString ());
                string[] groupRepEntries = GetGroupRepEntries(groupId);
                foreach (string repEntry in groupRepEntries)
                {
                    string[] entryChains = GetEntryAsymChains(repEntry);
                    foreach (string capriChain in capriChains)
                    {
                        foreach (string entryChain in entryChains)
                        {
                            dataWriter.WriteLine(pdbId + capriChain + "   " + repEntry + entryChain);
                        }
                    }
                }
            }
            dataWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="superGroupId"></param>
        /// <param name="dataWriter"></param>
        private void GetComparedEntriesAtClan (string pdbId, int superGroupId, StreamWriter dataWriter)
        {
            int[] clanSuperGroupIds = GetClanSuperGroupIds(superGroupId);
            foreach (int clanSuperGroupId in clanSuperGroupIds)
            {
                GetComparedEntries(pdbId, clanSuperGroupId, dataWriter);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private int[] GetClanSuperGroupIds(int superGroupId)
        {
            string queryString = string.Format("Select ChainRelPfamArch From PfamSuperGroups WHere SuperGroupSeqID = {0};", superGroupId);
            DataTable repPfamArchTable = ProtCidSettings.protcidQuery.Query( queryString);

            string chainPfamArch = repPfamArchTable.Rows[0]["ChainRelPfamArch"].ToString().TrimEnd();
            string[] fields = chainPfamArch.Split(')');
            List<string> pfamIdList = new List<string> ();
            string pfamId = "";
            foreach (string field in fields)
            {
                if (field == "")
                {
                    continue;
                }
                pfamId = field.TrimStart("_(".ToCharArray());
                if (pfamId.IndexOf("Pfam-B") < 0)
                {
                    pfamIdList.Add(pfamId);
                }
            }
            List<int> clanSuperGroupIdList = new List<int>  ();
            if (pfamIdList.Count == 1)
            {
                string[] clanPfams = GetClanPfams((string)pfamIdList[0]);
                foreach (string clanPfam in clanPfams)
                {
                    queryString = string.Format("Select * From PfamSuperGroups Where ChainRelPfamArch = '{0}';", "(" + clanPfam + ")");
                    DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
                    if (superGroupIdTable.Rows.Count > 0)
                    {
                        clanSuperGroupIdList.Add(Convert.ToInt32(superGroupIdTable.Rows[0]["SuperGroupSeqID"].ToString()));
                    }
                }
            }
            return clanSuperGroupIdList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] GetClanPfams(string pfamId)
        {
            string queryString = string.Format("Select Clan_Acc From PfamHmm, PfamClanFamily Where Pfam_ID = '{0}' AND PfamHmm.Pfam_Acc = PfamClanFamily.Pfam_Acc;", pfamId);
            DataTable clanAccTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string clanAcc = "";
            List<string> clanPfamIdList = new List<string> ();
            if (clanAccTable.Rows.Count > 0)
            {
                clanAcc = clanAccTable.Rows[0]["Clan_Acc"].ToString().TrimEnd();
                queryString = string.Format("Select Pfam_ID From PfamHmm, PfamClanFamily WHere Clan_Acc = '{0}' AND PfamHmm.Pfam_Acc = PfamClanFamily.Pfam_Acc;", clanAcc);
                DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
                foreach (DataRow pfamIdRow in pfamIdTable.Rows)
                {
                    if (pfamIdRow["Pfam_ID"].ToString().TrimEnd() == pfamId)
                    {
                        continue;
                    }
                    clanPfamIdList.Add(pfamIdRow["Pfam_ID"].ToString ().TrimEnd ());
                }

            }
            return clanPfamIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private int GetEntrySuperGroupSeqId(string pdbId)
        {
            string queryString = string.Format("Select GroupSeqID From PfamHomoSeqInfo Where PdbID = '{0}';", pdbId);
            DataTable groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (groupIdTable.Rows.Count == 0)
            {
                queryString = string.Format("Select GroupSeqID From PfamHomoRepEntryAlign Where PdbID2 = '{0}';", pdbId);
                groupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            }
            int groupId = -1;
            int superGroupId = -1;
            if (groupIdTable.Rows.Count > 0)
            {
                groupId = Convert.ToInt32(groupIdTable.Rows[0]["GroupSeqID"].ToString());
                queryString = string.Format("Select SuperGroupSeqID From PfamSuperGroups Where GroupSeqID = {0};", groupId);
                DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (superGroupIdTable.Rows.Count > 0)
                {
                    superGroupId = Convert.ToInt32(superGroupIdTable.Rows[0]["SuperGroupSeqID"].ToString ());
                }
            }
            return superGroupId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private string[] GetEntryAsymChains(string pdbId)
        {
            string queryString = string.Format("Select AsymID From AsymUnit Where PdbID = '{0}' AND PolymerType = 'polypeptide';", pdbId);
            DataTable chainTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] asymChains = new string[chainTable.Rows.Count];
            int count = 0;
            foreach (DataRow chainRow in chainTable.Rows)
            {
                asymChains[count] = chainRow["AsymID"].ToString().TrimEnd();
                count++;
            }
            return asymChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private string[] GetGroupRepEntries(int groupId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamHomoSeqInfo Where GroupSeqID = {0};", groupId);
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] groupRepEntries = new string[repEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in repEntryTable.Rows)
            {
                groupRepEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return groupRepEntries;
        }
        #endregion

        #region Li Xue -- uu.nl 
        // #clusters, M>=5 and seqid < 90, diff-Pfam 2823 entries
        public void CompileDifPfamClusterCoordinatesFiles ()
        {
            string srcClusterDir1 = @"D:\protcid_update31Fromv30\UpdateDomainClusterInterfaces";
            string srcClusterDir2 = @"D:\protcid\UpdateDomainClusterInterfaces";
            string liXueDataDir = @"X:\Qifang\externalProjects\LiXue_uu_nl";
            string liXueInterfaceFileDir = Path.Combine(liXueDataDir, "DifPfamClusterInterfaces");
            string difPfamClusterSumInfoFile = Path.Combine (liXueDataDir, "DifPfamClustersInfo.txt");
            StreamWriter difPfamClusterWriter = new StreamWriter(difPfamClusterSumInfoFile);
            difPfamClusterWriter.WriteLine("Pfam1\tPfam2\tRelSeqID\tClusterID\tSurfaceArea\tInPDB\tInPisa\t" + 
                "#CFs/cluster\t#Entries/cluster\t#CFs/group\t#Entries/group\tMinSeqIdentity");
            string difPfamClusterInterfaceFile = Path.Combine (liXueDataDir, "DifPfamClustersInterfaces.txt");
            StreamWriter difPfamInterfaceWriter = new StreamWriter(difPfamClusterInterfaceFile);
            difPfamInterfaceWriter.WriteLine("Pfam1\tPfam2\tRelSeqID\tClusterID\tSpaceGroup\tCrystForm\tPdbID\tDomainInterfaceID\tSurfaceArea\t" +
                " ChainPfamArch\tInPdb\tInPisa\tPdbBu\tPisaBu\tName\tSpecies\tUnpCode");
            string queryString = "Select FamilyCode1, FamilyCode2, PfamDomainClusterSumInfo.RelSeqID, ClusterID, SurfaceArea, " + 
                " InPdb, InPisa, NumOfCfgCluster, NumOfEntryCluster, NumOfCfgRelation, NumOfEntryRelation, MinSeqIdentity" + 
                " From PfamDomainClusterSumInfo, PfamDomainFamilyRelation " +
                " Where NumOfCfgCluster >= 5 AND MinSeqIdentity < 90 AND PfamDomainClusterSumInfo.RelSeqID = PfamDomainFamilyRelation.RelSeqID " +
                " AND FamilyCode1 <> FamilyCode2;";
            DataTable difPfamClusterTable = ProtCidSettings.protcidQuery.Query(queryString);
            int relSeqId = 0;
            int clusterId = 0;
            string dataLine = "";
            string clusterSrcFile = "";
            string clusterDestFile = "";
            foreach (DataRow clusterRow in difPfamClusterTable.Rows)
            {
                dataLine = ParseHelper.FormatDataRow(clusterRow);
                difPfamClusterWriter.WriteLine(dataLine);
                relSeqId = Convert.ToInt32 (clusterRow["RelSeqID"].ToString ());
                clusterId = Convert.ToInt32 (clusterRow["ClusterID"].ToString ());
                DataTable clusterInterfaceTable = GetClusterInterfaceTable(relSeqId, clusterId);
                foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
                {                   
                    dataLine = clusterRow["FamilyCode1"].ToString().TrimEnd() + "\t" + clusterRow["FamilyCode2"].ToString().TrimEnd() + "\t" +
                        ParseHelper.FormatDataRow(interfaceRow);
                    difPfamInterfaceWriter.WriteLine(dataLine);
                }
                clusterSrcFile = Path.Combine(srcClusterDir2, relSeqId + "_" + clusterId + ".tar.gz");
                if (! File.Exists (clusterSrcFile))
                {
                    clusterSrcFile = Path.Combine(srcClusterDir1, relSeqId + "_" + clusterId + ".tar.gz");
                }
                if (! File.Exists (clusterSrcFile))
                {
                    ProtCidSettings.logWriter.WriteLine(clusterSrcFile + "File not exists. Should check the error.");
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
                clusterDestFile = Path.Combine(liXueInterfaceFileDir, relSeqId + "_" + clusterId + ".tar.gz");
                File.Copy(clusterSrcFile, clusterDestFile, true);
            }
            difPfamClusterWriter.Close();
            difPfamInterfaceWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private DataTable GetClusterInterfaceTable (int relSeqId, int clusterId)
        {
            string queryString = string.Format("Select RelSeqID, ClusterID, SpaceGroup, CrystForm, PdbID, DomainInterfaceID, SurfaceArea, " + 
                " ChainPfamArch, InPdb, InPisa, PdbBu, PisaBu, Name, Species, UnpCode" + 
                " From PfamDomainClusterInterfaces Where RelSeqID = {0}  AND ClusterID = {1};", relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query(queryString);
            return clusterInterfaceTable;
        }
        #endregion

    }
}
