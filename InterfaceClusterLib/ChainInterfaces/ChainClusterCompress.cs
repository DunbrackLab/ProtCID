using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using DbLib;
using AuxFuncLib;
using ProtCidSettingsLib;
using InterfaceClusterLib.PymolScript;
using InterfaceClusterLib.AuxFuncs;

namespace InterfaceClusterLib.ChainInterfaces
{
    public class ChainClusterCompress
    {
        #region member variables
        public DbQuery dbQuery = new DbQuery();
        public DbUpdate dbUpdate = new DbUpdate();
        public  string clusterFileDir = "";
        public string reverseInterfaceFileDir = "";
        public FileCompress fileCompress = new FileCompress();
        public InterfaceAlignPymolScript interfacePymolScript = new InterfaceAlignPymolScript();        
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterReverseInterfaceFileList"></param>
        public void CompressClusterInterfaceFiles(string clusterReverseInterfaceFileList)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Compress Cluster Interface Files";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress cluster interface files");

            reverseInterfaceFileDir = Path.Combine (ProtCidSettings.dirSettings.interfaceFilePath, "ReversedInterfaces\\cryst");
            clusterFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "clusters" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(clusterFileDir))
            {
                Directory.CreateDirectory(clusterFileDir);
            }
            else
            {
                Directory.Delete(clusterFileDir, true);
                Directory.CreateDirectory(clusterFileDir);
            }
            string queryString = "Select Distinct SuperGroupSeqID From PfamSuperInterfaceClusters;";
            DataTable groupTable = ProtCidSettings.protcidQuery.Query( queryString);
            ProtCidSettings.progressInfo.totalOperationNum = groupTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = groupTable.Rows.Count;

            Dictionary<int, Dictionary<int, string[]>> groupClusterReverseFileHash = null;

            try
            {
                groupClusterReverseFileHash = ReadGroupClusterReverseInterfaceHash(clusterReverseInterfaceFileList);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                groupClusterReverseFileHash = new Dictionary<int,Dictionary<int,string[]>> ();
            }
            int superGroupId = -1;
            foreach (DataRow groupRow in groupTable.Rows)
            {
                superGroupId = Convert.ToInt32(groupRow["SuperGroupSeqID"].ToString());
               
                if (groupClusterReverseFileHash.ContainsKey(superGroupId))
                {
                    Dictionary<int, string[]> clusterReverseFilesHash = groupClusterReverseFileHash[superGroupId];
                    CompressGroupClustersFiles(superGroupId, clusterReverseFilesHash);
                }
                else
                {
                    CompressGroupClustersFiles(superGroupId);
                }
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupIds"></param>
        /// <param name="updateClusterReverseFileList"></param>
        public void CompressGroupClusterInterfaceFiles(int[] superGroupIds, string updateClusterReverseFileList)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.currentOperationLabel = "Compress Cluster Interface Files";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress cluster interface files");
            ProtCidSettings.logWriter.WriteLine("Compress cluster interface files");

            reverseInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "ReversedInterfaces\\cryst");
            clusterFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "newclusters" + DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(clusterFileDir))
            {
                Directory.CreateDirectory(clusterFileDir);
            }

            Dictionary<int, Dictionary<int, string[]>> groupClusterReverseFileHash = ReadGroupClusterReverseInterfaceHash(updateClusterReverseFileList);

            ProtCidSettings.progressInfo.totalOperationNum = superGroupIds.Length;
            ProtCidSettings.progressInfo.totalStepNum = superGroupIds.Length;

            foreach (int superGroupId in superGroupIds)
            {
                if (File.Exists (Path.Combine(clusterFileDir, superGroupId + ".tar")))
                {
                    continue;
                }
                ProtCidSettings.logWriter.WriteLine(superGroupId.ToString ());
                if (groupClusterReverseFileHash.ContainsKey(superGroupId))
                {
                    CompressGroupClustersFiles(superGroupId, groupClusterReverseFileHash[superGroupId]);
                }
                else
                {
                    CompressGroupClustersFiles(superGroupId);
                }
                ProtCidSettings.logWriter.Flush();
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterReverseFilesHash"></param>
        public void CompressGroupClustersFiles(int superGroupId, Dictionary<int, string[]> clusterReverseFilesHash)
        {
            ProtCidSettings.progressInfo.currentOperationNum++;
            ProtCidSettings.progressInfo.currentStepNum++;
            ProtCidSettings.progressInfo.currentFileName = superGroupId.ToString();

            string groupName = DownloadableFileName.GetChainGroupTarGzFileName(superGroupId);

            int[] groupClusters = GetClustersForGroup(superGroupId);
            List<string> clusterFileList = new List<string> ();
            string clusterFile = "";
            foreach (int clusterId in groupClusters)
            {
                ProtCidSettings.progressInfo.currentFileName = superGroupId.ToString() + "_" + clusterId.ToString();
                try
                {                   
                    clusterFile = CompressGroupClusterInterfaceFiles(superGroupId, clusterId, groupName, clusterReverseFilesHash);
                    if (clusterFile != "")
                    {
                        clusterFileList.Add(clusterFile);
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString() + "_" + clusterId.ToString() +
                        "Compress cluster interface files errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(superGroupId.ToString() + "_" + clusterId.ToString() +
                        "Compress cluster interface files errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            // tar cluster files to group
            string groupTarGzFile = groupName + ".tar";
            string groupFile = fileCompress.RunTar(groupTarGzFile, clusterFileList.ToArray (), clusterFileDir, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterReverseFilesHash"></param>
        public void CompressGroupClustersFiles(int superGroupId)
        {
            ProtCidSettings.progressInfo.currentOperationNum++;
            ProtCidSettings.progressInfo.currentStepNum++;
            ProtCidSettings.progressInfo.currentFileName = superGroupId.ToString();

            int[] groupClusters = GetClustersForGroup(superGroupId);
            string groupName = DownloadableFileName.GetChainGroupTarGzFileName(superGroupId);
            List<string> clusterFileList = new List<string> ();
            string clusterFile = "";
            foreach (int clusterId in groupClusters)
            {
                ProtCidSettings.progressInfo.currentFileName = superGroupId.ToString() + "_" + clusterId.ToString();
                try
                {
                    clusterFile = CompressGroupClusterInterfaceFiles(superGroupId, clusterId, groupName);
                    if (clusterFile != "")
                    {
                        clusterFileList.Add(clusterFile);
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(superGroupId.ToString() + "_" + clusterId.ToString() +
                        "Compress cluster interface files errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(superGroupId.ToString() + "_" + clusterId.ToString() +
                        "Compress cluster interface files errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            // tar cluster files to group
            string groupTarFileName = groupName + ".tar";
            fileCompress.RunTar(groupTarFileName, clusterFileList.ToArray (), clusterFileDir, false);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <param name="clusterReverseFilesHash"></param>
        /// <returns></returns>
        public string CompressGroupClusterInterfaceFiles(int superGroupId, int clusterId, string groupName, Dictionary<int, string[]> clusterReverseFilesHash)
        {
           string queryString = string.Format("Select PdbID, InterfaceID From PfamSuperClusterEntryInterfaces " + 
                " Where SuperGroupSeqID = {0} AND ClusterID = {1} Order By PdbID, InterfaceID;", superGroupId, clusterId);
            /* string queryString = string.Format("Select PdbID, InterfaceID From PfamSuperInterfaceClusters " +
               " Where SuperGroupSeqID = {0} AND ClusterID = {1} Order By PdbID, InterfaceID;", superGroupId, clusterId);
            */
           DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> entryList = new List<string> ();
            List<string> entryInterfaceList = new List<string> ();
            foreach (DataRow entryRow in clusterInterfaceTable.Rows)
            {
                string pdbId = entryRow["PdbID"].ToString();
                if (! entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                    entryInterfaceList.Add(pdbId + "_" + entryRow["InterfaceID"].ToString ());
                }
            }
            string interfaceFile = "";
            string hashDir = "";
            List<string> clusterInterfaceFileList = new List<string> ();
            foreach (string entryInterface in entryInterfaceList)
            {
                hashDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst\\" + entryInterface.Substring (1, 2));
                interfaceFile = Path.Combine (hashDir, entryInterface + ".cryst.gz");
                try
                {
                    File.Copy(interfaceFile, Path.Combine(clusterFileDir, entryInterface + ".cryst.gz"), true);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                    ProtCidSettings.logWriter.WriteLine(ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
                ParseHelper.UnZipFile (Path.Combine(clusterFileDir, entryInterface + ".cryst.gz"));
                clusterInterfaceFileList.Add (entryInterface + ".cryst");
            }
            AddClusterReverseInterfaceFiles(clusterId, clusterReverseFilesHash);
            string[] clusterInterfaces = new string[clusterInterfaceFileList.Count];
            clusterInterfaceFileList.CopyTo(clusterInterfaces);
       //     string[] pymolScriptFiles = PrecompilePymolScriptFiles (clusterFileDir, superGroupId, clusterId);
            string[] pymolScriptFiles = interfacePymolScript.FormatChainInterfacePymolScriptFiles (clusterFileDir, superGroupId, clusterId, clusterInterfaces);
            // add pymol script files into cluster tar filed
            clusterInterfaceFileList.AddRange(pymolScriptFiles);
            string[] clusterInterfaceFiles = new string[clusterInterfaceFileList.Count];
            clusterInterfaceFileList.CopyTo(clusterInterfaceFiles);

            string clusterFileName = groupName + "_" + clusterId + ".tar.gz";
            string clusterFile = fileCompress.RunTar(clusterFileName, clusterInterfaceFiles, clusterFileDir, true);

            foreach (string clusterInterfaceFile in clusterInterfaceFiles)
            {
                File.Delete(Path.Combine (clusterFileDir, clusterInterfaceFile));
            }
            return clusterFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <param name="clusterReverseFilesHash"></param>
        /// <returns></returns>
        public string CompressGroupClusterInterfaceFiles(int superGroupId, int clusterId, string groupName)
        {
            string queryString = string.Format("Select PdbID, InterfaceID From PfamSuperClusterEntryInterfaces " +
                " Where SuperGroupSeqID = {0} AND ClusterID = {1} Order By PdbID, InterfaceID;", superGroupId, clusterId);
        /*    string queryString = string.Format("Select PdbID, InterfaceID From PfamSuperInterfaceClusters " +
                " Where SuperGroupSeqID = {0} AND ClusterID = {1} Order By PdbID, InterfaceID;", superGroupId, clusterId);
        */
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> entryList = new List<string> ();
            List<string> entryInterfaceList = new List<string> ();
            foreach (DataRow entryRow in clusterInterfaceTable.Rows)
            {
                string pdbId = entryRow["PdbID"].ToString();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                    entryInterfaceList.Add(pdbId + "_" + entryRow["InterfaceID"].ToString());
                }
            }
            string interfaceFile = "";
            string hashDir = "";
            List<string> clusterInterfaceFileList = new List<string> ();
            foreach (string entryInterface in entryInterfaceList)
            {
                hashDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst\\" + entryInterface.Substring(1, 2));
                interfaceFile = Path.Combine(hashDir, entryInterface + ".cryst.gz");
                try
                {
                    File.Copy(interfaceFile, Path.Combine(clusterFileDir, entryInterface + ".cryst.gz"), true);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                    ProtCidSettings.logWriter.WriteLine(ex.Message);
                    ProtCidSettings.logWriter.Flush();
                    continue;
                }
                ParseHelper.UnZipFile(Path.Combine(clusterFileDir, entryInterface + ".cryst.gz"));
                clusterInterfaceFileList.Add(entryInterface + ".cryst");
            }

            string[] pymolScriptFiles = interfacePymolScript.FormatChainInterfacePymolScriptFiles(clusterFileDir, superGroupId, clusterId, clusterInterfaceFileList.ToArray ());
           
            // add pymol script files into cluster tar filed
            clusterInterfaceFileList.AddRange(pymolScriptFiles);

            string clusterFileName = groupName + "_" + clusterId + ".tar.gz";
            string clusterFile = fileCompress.RunTar(clusterFileName, clusterInterfaceFileList.ToArray (), clusterFileDir, true);

            foreach (string clusterInterfaceFile in clusterInterfaceFileList)
            {
                File.Delete(Path.Combine(clusterFileDir, clusterInterfaceFile));
            }
            return clusterFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private int[] GetClustersForGroup(int superGroupId)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamSuperInterfaceClusters " + 
                " Where SuperGroupSeqID = {0};", superGroupId);
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] groupClusters = new int[clusterTable.Rows.Count];
            int count = 0;
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                groupClusters[count] = Convert.ToInt32(clusterRow["ClusterID"].ToString ());
                count++;
            }
            return groupClusters;
        }

        #region precompile interface align pymol
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScriptFile"></param>
        private void RemoveInterfaceFilePath(string pymolScriptFile)
        {
            StreamReader dataReader = new StreamReader(Path.Combine (clusterFileDir, pymolScriptFile));
            string line = "";
            string dataLine = "";
            string load = "load ";
            string fileName = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.Length > load.Length)
                {
                    if (line.Substring(0, load.Length) == load)
                    {
                        fileName = RemoveFilePath(line);
                        dataLine += (load + fileName + "\r\n");
                    }
                    else
                    {
                        dataLine += (line + "\r\n");
                    }
                }
                else
                {
                    dataLine += (line + "\r\n");
                }
            }
            dataReader.Close();
            StreamWriter dataWriter = new StreamWriter(Path.Combine (clusterFileDir, pymolScriptFile));
            dataWriter.Write(dataLine);
            dataWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private string RemoveFilePath(string filePath)
        {
            int exeIndex = filePath.LastIndexOf("/");
            string fileName = filePath.Substring(exeIndex + 1, filePath.Length - exeIndex - 1);
            return fileName;
        }
        #endregion

        #region add reversed interface files to cluster files
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <param name="clusterFileHash"></param>
        /// <param name="reverseInterfaceFileDir"></param>
        private void AddClusterReverseInterfaceFiles(int clusterId, Dictionary<int, string[]> clusterFileHash)
        {
            string reverseInterfaceFile = "";
            string hashDir = "";
            string fileReverseInterface = "";
            List<string> clusterFileList = new List<string> ();

            if (!clusterFileHash.ContainsKey(clusterId))
            {
                return;
            }
            foreach (string reverseInterface in clusterFileHash[clusterId])
            {
                if (reverseInterface == "")
                {
                    continue;
                }
                fileReverseInterface =
                    reverseInterface.Substring(0, 4) + "_" + reverseInterface.Substring(4, reverseInterface.Length - 4) + ".cryst.gz";
                hashDir = Path.Combine(reverseInterfaceFileDir, reverseInterface.Substring(1, 2));
                reverseInterfaceFile = Path.Combine(hashDir, fileReverseInterface);
                if (!File.Exists(reverseInterfaceFile))
                {
                    continue;
                }
                File.Copy(reverseInterfaceFile, Path.Combine(clusterFileDir, fileReverseInterface), true);
                ParseHelper.UnZipFile(Path.Combine(clusterFileDir, fileReverseInterface));
            }
        }
        #endregion

        #region Update clusters with reversed interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<int, Dictionary<int, string[]>> ReadGroupClusterReverseInterfaceHash(string clusterReverseFileList)
        {
            Dictionary<int, Dictionary<int, string[]>> groupClusterFileHash =  new Dictionary<int,Dictionary<int,string[]>> ();
            if (!File.Exists(clusterReverseFileList))
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue("No reverseInterfacecInCluster file exist.");
                return groupClusterFileHash;
            }
            StreamReader dataReader = new StreamReader(clusterReverseFileList);
            string line = "";

            int superGroupId = 0;
            int clusterId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split(':');
                if (fields.Length < 2 || fields[1] == "")
                {
                    continue;
                }
                string[] groupClusterFields = ParseHelper.SplitPlus(fields[0], ' ');
                superGroupId = Convert.ToInt32(groupClusterFields[0]);
                clusterId = Convert.ToInt32(groupClusterFields[1]);
                string[] reverseInterfaces = fields[1].Split(',');
                if (reverseInterfaces.Length > 0)
                {
                    if (groupClusterFileHash.ContainsKey(superGroupId))
                    {
                        groupClusterFileHash[superGroupId].Add(clusterId, reverseInterfaces);
                    }
                    else
                    {
                        Dictionary<int, string[]> clusterFileHash = new Dictionary<int,string[]> ();
                        clusterFileHash.Add(clusterId, reverseInterfaces);
                        groupClusterFileHash.Add(superGroupId, clusterFileHash);
                    }
                }
            }
            dataReader.Close();
            return groupClusterFileHash;
        }
        #endregion

        #region Retrieve individual interface files for protcid web server
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isDomain"></param>
        public void RetrieveCrystInterfaceFilesNotInClusters(bool isDomain)
        {
            string webCrystDir = Path.Combine (ProtCidSettings.dirSettings.interfaceFilePath, "webCryst");
            if (isDomain)
            {
                webCrystDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "webDomain");
            }
            if (Directory.Exists(webCrystDir))
            {
                Directory.Delete(webCrystDir, true);
            }
            Directory.CreateDirectory(webCrystDir);
        //    string oldWebCrystDir = webCrystDir + "0";

            string crystDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
            if (isDomain)
            {
                crystDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "PfamDomain");
            }
            string queryString = "Select Distinct PdbID From CrystEntryInterfaces;";
            if (isDomain)
            {
                queryString = "Select Distinct PdbID From PfamDomainInterfaces;";
            }
            DataTable entryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string pdbId = "";
            foreach (DataRow entryRow in entryTable.Rows)
            {
                pdbId = entryRow["PdbID"].ToString();
               
                int[] interfacesNotInCluster = GetEntryInterfacesNotInCluster(pdbId, isDomain);
                CopyEntryInterfaceFiles(pdbId, interfacesNotInCluster, crystDir, webCrystDir, isDomain);
            }
        }

        private bool IsEntryInterfaceCopied(string pdbId, string destFileDir)
        {
            string hashFolder = Path.Combine(destFileDir, pdbId.Substring(1, 2));
            if (Directory.Exists(hashFolder))
            {
                string[] interfaceFiles = Directory.GetFiles(hashFolder, pdbId + "*");
                if (interfaceFiles.Length > 0)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="isDomain"></param>
        /// <returns></returns>
        private int[] GetEntryInterfacesNotInCluster(string pdbId, bool isDomain)
        {
            string queryString = string.Format("Select InterfaceID From PfamSuperClusterEntryInterfaces Where PdbID = '{0}';", pdbId);
            if (isDomain)
            {
                queryString = string.Format("Select DomainInterfaceID As InterfaceID From PfamDomainClusterInterfaces Where PdbID = '{0}';", pdbId);
            }
            DataTable entryClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            queryString = string.Format("Select InterfaceID From CrystEntryInterfaces Where PdbID = '{0}';", pdbId);
            if (isDomain)
            {
                queryString = string.Format("Select DomainInterfaceID As InterfaceID From PfamDomainInterfaces Where PdbID = '{0}';", pdbId);
            }
            DataTable entryInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            List<int> clusterInterfaceList = new List<int> ();
            int interfaceId = 0;
            foreach (DataRow clusterInterfaceRow in entryClusterTable.Rows)
            {
                interfaceId = Convert.ToInt32(clusterInterfaceRow["InterfaceID"].ToString ());
                clusterInterfaceList.Add(interfaceId);
            }
            List<int> leftInterfaceList = new List<int> ();
            foreach (DataRow crystInterfaceRow in entryInterfaceTable.Rows)
            {
                interfaceId = Convert.ToInt32(crystInterfaceRow["InterfaceID"].ToString ());
                if (! clusterInterfaceList.Contains(interfaceId))
                {
                    leftInterfaceList.Add(interfaceId);
                }
            }
            return leftInterfaceList.ToArray ();
        }       
    
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isDomain">is domain-based</param>
        public void UpdateCrystInterfaceFilesNotInClusters(bool isDomain)
        {
            string[] updateEntries = GetUpdateEntries();
            UpdateCrystInterfaceFilesNotInClusters(updateEntries, isDomain);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isDomain">is domain-based</param>
        public void UpdateCrystInterfaceFilesNotInClusters(int[] updateGroupIds, bool isDomain)
        {
            string[] updateEntries = GetChainDomainGroupEntries(updateGroupIds, isDomain);
            UpdateCrystInterfaceFilesNotInClusters(updateEntries, isDomain);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateEntries"></param>
        /// <param name="isDomain"></param>
        public void UpdateCrystInterfaceFilesNotInClusters(string[] updateEntries, bool isDomain)
        {
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Copy chain/domain interfaces not in any clusters");
            ProtCidSettings.logWriter.WriteLine("Copy chain/domain interfaces not in any clusters");
            string webCrystDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "webCryst");
            if (isDomain)
            {
                webCrystDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "webDomain");
            }
            if (Directory.Exists(webCrystDir))
            {
                Directory.Delete(webCrystDir, true);
            }
            Directory.CreateDirectory(webCrystDir);

            string crystDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
            if (isDomain)
            {
                crystDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "PfamDomain");
            }

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = updateEntries.Length;
            ProtCidSettings.progressInfo.totalStepNum = updateEntries.Length;

            foreach (string pdbId in updateEntries)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = pdbId;
                ProtCidSettings.logWriter.WriteLine(pdbId);
                try
                {
                    int[] interfacesNotInCluster = GetEntryInterfacesNotInCluster(pdbId, isDomain);
                    CopyEntryInterfaceFiles(pdbId, interfacesNotInCluster, crystDir, webCrystDir, isDomain);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Copy " + pdbId + " interfaces error: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine("Copy " + pdbId + " interfaces error: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
                ProtCidSettings.logWriter.Flush();
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();
        }

       
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetUpdateEntries()
        {
            StreamReader entryReader = new StreamReader(Path.Combine (ProtCidSettings.dirSettings.xmlPath, "newls-pdb.txt"));
            string line = "";
            List<string> entryList = new List<string> ();
            while ((line = entryReader.ReadLine()) != null)
            {
                entryList.Add(line.Substring (0, 4));
            }
            entryReader.Close(); 
            return entryList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupIds"></param>
        /// <param name="isDomain"></param>
        /// <returns></returns>
        private string[] GetChainDomainGroupEntries (int[] groupIds, bool isDomain)
        {
            List<string> entryList = new List<string>();
            string[] groupEntries = null;
            foreach (int groupId in groupIds)
            {
                if (isDomain)
                {
                    groupEntries = GetDomainGroupEntries(groupId);
                }
                else
                {
                    groupEntries = GetChainGroupEntries(groupId);
                }
                foreach (string pdbId in groupEntries)
                {
                    if (! entryList.Contains (pdbId))
                    {
                        entryList.Add(pdbId);
                    }
                }
            }
            return entryList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetDomainGroupEntries(int relSeqId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamDomainInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable relEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] relEntries = new string[relEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in relEntryTable.Rows)
            {
                relEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return relEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private string[] GetChainGroupEntries (int groupId)
        {
            string queryString = string.Format("Select Distinct PdbID From PfamHomoSeqInfo, PfamSuperGroups Where SuperGroupSeqID = {0} AND " + 
                " PfamHomoSeqInfo.GroupSeqID = PfamSuperGroups.GroupSeqID;", groupId);
            DataTable repEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            queryString = string.Format("Select Distinct PdbID2 As PdbID From PfamHomoRepEntryAlign, PfamSuperGroups Where SuperGroupSeqID = {0} AND " + 
                " PfamHomoRepEntryAlign.GroupSeqID = PfamSuperGroups.GroupSeqID;", groupId);
            DataTable homoEntryTable = ProtCidSettings.protcidQuery.Query(queryString);
            string[] groupEntries = new string[repEntryTable.Rows.Count + homoEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow entryRow in repEntryTable.Rows)
            {
                groupEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            foreach (DataRow entryRow in homoEntryTable.Rows)
            {
                groupEntries[count] = entryRow["PdbID"].ToString();
                count++;
            }
            return groupEntries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryInCluster(string pdbId, bool isDomain)
        {
            string queryString = string.Format("Select * From PfamSuperClusterEntryInterfaces Where PdbID = '{0}';", pdbId);
            if (isDomain)
            {
                queryString = string.Format("Select * From PfamDomainClusterInterfaces Where PdbID = '{0}';", pdbId);
            }
            DataTable entryClusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (entryClusterTable.Rows.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="srcFileDir"></param>
        /// <param name="destFileDir"></param>
        private void CopyEntryInterfaceFiles(string pdbId, string srcFileDir, string destFileDir)
        {
            string hashFolderSrc = Path.Combine(srcFileDir, pdbId.Substring(1, 2));
            string hashFolderDest = Path.Combine(destFileDir, pdbId.Substring (1, 2));
            string interfaceFileDest = "";
            if (!Directory.Exists(hashFolderDest))
            {
                Directory.CreateDirectory(hashFolderDest);
            }
            string[] interfaceFiles = Directory.GetFiles(hashFolderSrc, pdbId + "*");
            foreach (string interfaceFile in interfaceFiles)
            {
                FileInfo fileInfo = new FileInfo (interfaceFile);
                interfaceFileDest = Path.Combine (hashFolderDest, fileInfo.Name);
                File.Copy(interfaceFile, interfaceFileDest, true);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="srcFileDir"></param>
        /// <param name="destFileDir"></param>
        private void CopyEntryInterfaceFiles(string pdbId, int[] interfaceIds, string srcFileDir, string destFileDir, bool isDomain)
        {
            string hashFolderSrc = Path.Combine(srcFileDir, pdbId.Substring(1, 2));
            string hashFolderDest = Path.Combine(destFileDir, pdbId.Substring(1, 2));
            string destInterfaceFile = "";
            string orgInterfaceFile = "";
            if (!Directory.Exists(hashFolderDest))
            {
                Directory.CreateDirectory(hashFolderDest);
            }
            foreach (int interfaceId in interfaceIds)
            {
                orgInterfaceFile = Path.Combine (hashFolderSrc, pdbId + "_" + interfaceId.ToString () + ".cryst.gz");
                destInterfaceFile = Path.Combine(hashFolderDest, pdbId + "_" + interfaceId.ToString() + ".cryst.gz");
                if (isDomain)
                {
                    orgInterfaceFile = Path.Combine(hashFolderSrc, pdbId + "_d" + interfaceId.ToString() + ".cryst.gz");
                    destInterfaceFile = Path.Combine(hashFolderDest, pdbId + "_d" + interfaceId.ToString() + ".cryst.gz");
                }
                if (File.Exists(orgInterfaceFile))
                {
                    File.Copy(orgInterfaceFile, destInterfaceFile, true);
                }
            }
        }
        #endregion       

        #region for debug

        public void UpdateGroupClusterInterfaceFiles(string updateClusterReverseFileList)
        {
            int[] superGroupIds = GetUpdateSuperGroups();
      //      DeleteGroupInterfaceFiles(superGroupIds);

            CompressGroupClusterInterfaceFiles(superGroupIds, updateClusterReverseFileList);
        }

        private void DeleteGroupInterfaceFiles(int[] superGroupIds)
        {
            string groupFileDir = @"D:\DbProjectData\InterfaceFiles_update\clusters20120801_done";
            string groupFile = "";
            foreach (int superGroup in superGroupIds)
            {
                groupFile = Path.Combine(groupFileDir, superGroup.ToString () + ".tar");
                File.Delete(groupFile);
                int[] clusterIds = GetSuperGroupClusters(superGroup);
                foreach (int clusterId in clusterIds)
                {
                    groupFile = Path.Combine(groupFileDir, superGroup.ToString () + "_" + clusterId.ToString () + ".tar.gz");
                    File.Delete(groupFile);
                }
            }
        }

        private int[] GetSuperGroupClusters(int superGroupId)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamSuperClusterSumInfo " + 
                " Where SuperGroupSeqID = {0};", superGroupId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] clusterIds = new int[clusterIdTable.Rows.Count];
            int count = 0;
            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                clusterIds[count] = Convert.ToInt32(clusterIdRow["ClusterID"].ToString ());
                count++;
            }
            return clusterIds;
        }
        private int[] GetUpdateSuperGroups()
        {
            StreamReader dataReader = new StreamReader("ReverseInterfacesInCluster.txt");
            List<int> superGroupIdList = new List<int> ();
            string line = "";
            int superGroupId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = AuxFuncLib.ParseHelper.SplitPlus(line, ' ');
                superGroupId = Convert.ToInt32(fields[0]);
                if (!superGroupIdList.Contains(superGroupId))
                {
                    superGroupIdList.Add(superGroupId);
                }
            }
            dataReader.Close();

            dataReader = new StreamReader("missCrystInterfaces.txt");
            int fileIndex = 0;
            int exeIndex = 0;
            string pdbId = "";
            int interfaceId = 0;
            string interfaceName = "";
            int superGroupSeqId = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("Could not find file") > -1)
                {
                    fileIndex = line.LastIndexOf("\\");
                    exeIndex = line.IndexOf(".cryst.gz");
                    interfaceName = line.Substring(fileIndex + 1, exeIndex - fileIndex - 1);
                    pdbId = interfaceName.Substring(0, 4);
                    interfaceId = Convert.ToInt32(interfaceName.Substring(5, interfaceName.Length - 5));
                    superGroupSeqId = GetSuperGroupID(pdbId, interfaceId);
                    if (!superGroupIdList.Contains(superGroupSeqId))
                    {
                        superGroupIdList.Add (superGroupSeqId);
                    }
                }
            }
            dataReader.Close();
            return superGroupIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="interfaceId"></param>
        /// <returns></returns>
        private int GetSuperGroupID(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select SuperGroupSeqID From PfamSuperClusterEntryInterfaces " +
                " Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable superGroupIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int superGroupId = -1;
            if (superGroupIdTable.Rows.Count > 0)
            {
                superGroupId = Convert.ToInt32(superGroupIdTable.Rows[0]["SuperGroupSeqID"].ToString ());
            }
            return superGroupId;
        }
        #endregion
    }
}
