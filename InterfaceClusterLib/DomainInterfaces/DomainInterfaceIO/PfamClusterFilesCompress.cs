using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using InterfaceClusterLib.ChainInterfaces;
using InterfaceClusterLib.AuxFuncs;
using ProtCidSettingsLib;
using CrystalInterfaceLib.DomainInterfaces;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces
{
    public class PfamClusterFilesCompress : ChainClusterCompress
    {
        #region member variables
        public string chainInterfaceFileDir = "";
        public string domainInterfaceFileDir = "";
        public Dictionary<string, long[]> pfamMultiChainDomainHash = null;

        public PfamClusterFilesCompress()
        {
            chainInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "cryst");
            domainInterfaceFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "pfamDomain");
            clusterFileDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "clusters_domain" + DateTime.Today.ToString("yyyyMMdd"));
            if (! Directory.Exists(clusterFileDir))
            {
                Directory.CreateDirectory(clusterFileDir);
            }

            pfamMultiChainDomainHash = interfacePymolScript.GetPfamMultiChainDomainHash();
        }
        #endregion

        #region cluster domain interface files
        /// <summary>
        /// 
        /// </summary>
        public void CompressPfamClusterInterfaceFiles()
        {
            if (!Directory.Exists(clusterFileDir))
            {
                Directory.CreateDirectory(clusterFileDir);
            }
            else
            {
                Directory.Delete(clusterFileDir, true);
                Directory.CreateDirectory(clusterFileDir);
            }

            string queryString = "Select Distinct RelSeqId From PfamDomainInterfaceCluster;";
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = clusterTable.Rows.Count;
            ProtCidSettings.progressInfo.totalStepNum = clusterTable.Rows.Count;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress cluster domain interface files.");
            
            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                relSeqId = Convert.ToInt32(clusterRow["RelSeqID"].ToString());

                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                CompressRelationClusterChainInterfaceFiles(relSeqId);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        public void CompressRelationClusterInterfaceFiles(int relSeqId)
        {
            int[] clusterIds = GetRelationClusterIDs(relSeqId);
            string relationName = DownloadableFileName.GetDomainRelationName(relSeqId);

            List<string> clusterFileList = new List<string> ();
            string clusterFile = "";

            foreach (int clusterId in clusterIds)
            {
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString() + "_" + clusterId.ToString();
                try
                {
                    clusterFile = CompressClusterDomainInterfaceFiles(relSeqId, clusterId, relationName);
                    if (clusterFile != "")
                    {
                        clusterFileList.Add(clusterFile);
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() + "_" + clusterId.ToString() +
                        "Compress cluster interface files errors: " + ex.Message);
                }
            }
            // tar cluster files to group
            fileCompress.RunTar(relationName + ".tar", clusterFileList.ToArray (), clusterFileDir, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        public string CompressClusterDomainInterfaceFiles(int relSeqId, int clusterId, string relationName)
        {
            string queryString = string.Format("Select * From PfamDomainInterfaceCluster " +
               " Where RelSeqId= {0} AND ClusterID = {1} Order By PdbID, DomainInterfaceID;", relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            List<string> entryList = new List<string> ();
            List<string> domainInterfaceList = new List<string> ();
            foreach (DataRow entryRow in clusterInterfaceTable.Rows)
            {
                string pdbId = entryRow["PdbID"].ToString();
                if (!entryList.Contains(pdbId))
                {
                    entryList.Add(pdbId);
                    domainInterfaceList.Add(pdbId + "_d" + entryRow["DomainInterfaceID"].ToString());
                }
            }
            string interfaceFile = "";
            string hashDir = "";
            List<string> clusterInterfaceFileList = new List<string> ();
            foreach (string domainInterface in domainInterfaceList)
            {
                hashDir = Path.Combine(domainInterfaceFileDir, domainInterface.Substring(1, 2));
                interfaceFile = Path.Combine(hashDir, domainInterface + ".cryst.gz");
                try
                {
                    File.Copy(interfaceFile, Path.Combine(clusterFileDir, domainInterface + ".cryst.gz"), true);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                    continue;
                }
                ParseHelper.UnZipFile(Path.Combine(clusterFileDir, domainInterface + ".cryst.gz"));
                clusterInterfaceFileList.Add(domainInterface + ".cryst");
            }
            string[] clusterInterfaceFiles = clusterInterfaceFileList.ToArray ();

            string clusterFileName = relationName + "_" + clusterId + ".tar.gz";
            string clusterFile = fileCompress.RunTar(clusterFileName, clusterInterfaceFiles, clusterFileDir, true);

            foreach (string clusterInterfaceFile in clusterInterfaceFiles)
            {
                File.Delete(Path.Combine(clusterFileDir, clusterInterfaceFile));
            }
            return clusterFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private int[] GetRelationClusterIDs(int relSeqId)
        {
            string queryString = string.Format("Select Distinct ClusterID From PfamDomainInterfaceCluster " + 
                " Where RelSeqID = {0};", relSeqId);
            DataTable clusterIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int[] clusterIds = new int[clusterIdTable.Rows.Count];
            int count = 0;
            int clusterId = 0;
            foreach (DataRow clusterIdRow in clusterIdTable.Rows)
            {
                clusterId = Convert.ToInt32(clusterIdRow["clusterID"].ToString ());
                clusterIds[count] = clusterId;
                count++;
            }
            return clusterIds;
        }
        #endregion

        #region chain interface files
        /// <summary>
        /// 
        /// </summary>
        public void CompressPfamClusterChainInterfaceFiles()
        {
            string queryString = "Select Distinct RelSeqId From PfamDomainInterfaceCluster;";
            DataTable clusterTable = ProtCidSettings.protcidQuery.Query( queryString);
            int relSeqId = 0;

            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalStepNum = clusterTable.Rows.Count;
            ProtCidSettings.progressInfo.totalOperationNum = clusterTable.Rows.Count;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Compress cluster domain interface files.");

            foreach (DataRow clusterRow in clusterTable.Rows)
            {
                relSeqId = Convert.ToInt32(clusterRow["RelSeqID"].ToString());

                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString();
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                CompressRelationClusterChainInterfaceFiles(relSeqId);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }       

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIds"></param>
        public void UpdateRelationClusterChainInterfaceFiles(int[] relSeqIds)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            foreach (int relSeqId in relSeqIds)
            {
                CompressRelationClusterChainInterfaceFiles(relSeqId);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Compress cluster interface files done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        public void CompressRelationClusterChainInterfaceFiles(int relSeqId)
        {
            string relationName = DownloadableFileName.GetDomainRelationName(relSeqId);
            string groupTarFile = Path.Combine(clusterFileDir, relationName + ".tar");

            if (File.Exists(groupTarFile))
            {
                return;
            }
            
            ProtCidSettings.logWriter.WriteLine (relSeqId.ToString ());
          
            int[] clusterIds = GetRelationClusterIDs(relSeqId);
            string queryString = string.Format("Select * From PfamDomainInterfaces Where RelSeqID = {0};", relSeqId);
            DataTable domainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = string.Format("Select * From PfamDomainInterfaceComp Where RelSeqID = {0};", relSeqId);
            DataTable domainInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);

            List<string> clusterFileList = new List<string> ();
            string clusterFile = "";
            foreach (int clusterId in clusterIds)
            { 
                ProtCidSettings.progressInfo.currentFileName = relSeqId.ToString() + "_" + clusterId.ToString();
                try
                {
                    clusterFile = CompressClusterChainInterfaceFiles(relSeqId, clusterId, relationName, domainInterfaceTable, domainInterfaceCompTable);
                    if (clusterFile != "")
                    {
                        clusterFileList.Add(clusterFile);
                    }
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(relSeqId.ToString() + "_" + clusterId.ToString() +
                        "Compress cluster interface files errors: " + ex.Message);
                    ProtCidSettings.logWriter.WriteLine(relSeqId.ToString() + "_" + clusterId.ToString() +
                        "Compress cluster interface files errors: " + ex.Message);
                    ProtCidSettings.logWriter.Flush();
                }
            }
            string[] clusterFiles = new string[clusterFileList.Count];
            clusterFileList.CopyTo(clusterFiles);
            // tar cluster files to group
            fileCompress.RunTar(relationName + ".tar", clusterFiles, clusterFileDir, false);

            ProtCidSettings.logWriter.Flush();
        }     

        /// <summary>
        /// use chain interfaces, but change the file names to domain interface file names,
        /// modified on May 22, 2013
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        public string CompressClusterChainInterfaceFiles(int relSeqId, int clusterId, string relationName, DataTable domainInterfaceTable, DataTable domainInterfaceCompTable)
        {
            string queryString = string.Format("Select * From PfamDomainClusterInterfaces " +
               " Where RelSeqId= {0} AND ClusterID = {1} Order By PdbID, DomainInterfaceID;", relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            // for pymol script files
            // the center interface
            // one interface of each entry in the cluster, the smallest number
            string[] clusterRepInterfaces = GetClusterRepDomainInterfaces(relSeqId, clusterId);
        //    string centerInterface = GetTheCenterInterfaceFile(relSeqId, clusterRepInterfaces, out centerDomainInterfaceId);
            string centerDomainInterface = GetDomainInterfaceWithBestAvgQ(relSeqId, clusterRepInterfaces, domainInterfaceCompTable);
            if (centerDomainInterface == "")
            {
                centerDomainInterface = clusterRepInterfaces[0];
            }
            string[] domainInterfaceFields = centerDomainInterface.Split ('_');
            string centerPdbId = domainInterfaceFields[0] ;
            int centerDomainInterfaceId = Convert.ToInt32 (domainInterfaceFields[1]);
            string centerInterface = centerPdbId + "_d" + centerDomainInterfaceId.ToString();

            string centerDomainRangeString = "";

            string domainInterface = centerPdbId + "_d" + centerDomainInterfaceId.ToString ();
            string[] centerPymolScript = interfacePymolScript.FormatDomainInterfacePymolScript(centerPdbId, centerDomainInterfaceId,
                domainInterface, out centerDomainRangeString);

            string pymolScriptChainFile = relSeqId.ToString() + "_" + clusterId.ToString() + "_chain.pml";
            StreamWriter pymolScriptChainWriter = new StreamWriter(Path.Combine (clusterFileDir, pymolScriptChainFile));
            pymolScriptChainWriter.WriteLine(centerPymolScript[0]);
            pymolScriptChainWriter.WriteLine();

            string pymolScriptDomainFile = relSeqId.ToString() + "_" + clusterId.ToString() + "_domain.pml";
            StreamWriter pymolScriptDomainWriter = new StreamWriter(Path.Combine(clusterFileDir, pymolScriptDomainFile));
            pymolScriptDomainWriter.WriteLine(centerPymolScript[1]);
            pymolScriptDomainWriter.WriteLine();

            string[] interfacePymolScriptLines = null; // 0: whole chain aligned, 1: domain aligned

            string[] clusterDomainInterfacesToBeAligned = GetClusterDomainInterfacesToBeAligned(clusterInterfaceTable);

            List<string> clusterFileList = new List<string> ();
            string[] fileReverseDomainInterfaces = null;
            string[] pfamPair = GetRelationPfamPair(relSeqId);
            string[] clusterInterfaceFiles = CopyDomainInterfaceFilesFromChain(clusterDomainInterfacesToBeAligned, pfamPair[0], pfamPair[1], out fileReverseDomainInterfaces);
            clusterFileList.AddRange(clusterInterfaceFiles);

            string[] QReversedDomainInterfaces = GetClusterReversedDomainInterfaces(centerDomainInterface, clusterRepInterfaces, clusterDomainInterfacesToBeAligned);

            List<string> reversedDomainInterfaceList = new List<string> (fileReverseDomainInterfaces);
            foreach (string reverseDomainInterface in QReversedDomainInterfaces)
            {
                if (!reversedDomainInterfaceList.Contains(reverseDomainInterface))
                {
                    reversedDomainInterfaceList.Add(reverseDomainInterface);
                }
            }
            string[] reversedDomainInterfaces = reversedDomainInterfaceList.ToArray();
            
            string[] multiChainDomainInterfaces = GetMultiChainDomainInterfaces(relSeqId, clusterDomainInterfacesToBeAligned, domainInterfaceTable, pfamMultiChainDomainHash);

            List<string> chainInterfaceList = new List<string> ();
            List<string> intraChainInterfaceList = new List<string> ();
            int interfaceId = -1;
            int domainInterfaceId = -1;
            string clusterInterface = "";
            string pdbId = "";
            bool isReversed = false;
            bool isMultiChain = false;

         //   foreach (DataRow entryRow in clusterInterfaceTable.Rows)
            foreach (string alignDomainInterface in clusterDomainInterfacesToBeAligned)
            {
                string[] interfaceFields = alignDomainInterface.Split('_');
                pdbId = interfaceFields[0];
                interfaceId = Convert.ToInt32(interfaceFields[1]);
                domainInterfaceId = Convert.ToInt32(interfaceFields[2]);
                isMultiChain = false;

                clusterInterface = pdbId + "_d"  + domainInterfaceId.ToString();

                if (Array.IndexOf(multiChainDomainInterfaces, alignDomainInterface) > -1)
                {
                    isMultiChain = true;
                }
           
                if (clusterInterface != centerInterface)
                {
                    isReversed = false;
                    if (reversedDomainInterfaces.Contains(alignDomainInterface))
                    {
                        isReversed = true;
                    }
                    if (isMultiChain)
                    {
                        interfacePymolScriptLines = interfacePymolScript.FormatMultiChainDomainInterfacePymolScript(pdbId, domainInterfaceId, 
                                                centerInterface, centerDomainRangeString, isReversed);
                    }
                    else
                    {
                        interfacePymolScriptLines = interfacePymolScript.FormatDomainInterfacePymolScript(pdbId, domainInterfaceId,
                                                centerInterface, centerDomainRangeString, isReversed);
                    }
                    pymolScriptChainWriter.WriteLine(interfacePymolScriptLines[0]);
                    pymolScriptChainWriter.WriteLine();
                    pymolScriptDomainWriter.WriteLine(interfacePymolScriptLines[1]);
                    pymolScriptDomainWriter.WriteLine();
                }
            }
            pymolScriptChainWriter.WriteLine("center " + centerInterface + ".cryst");
            pymolScriptDomainWriter.WriteLine("center " + centerInterface + ".cryst");
            pymolScriptChainWriter.Close();
            pymolScriptDomainWriter.Close();

            string pymolScriptRepChainFile = FormatClusterRepInterfacesPymolScript(pymolScriptChainFile, clusterRepInterfaces);
            string pymolScriptRepDomainFile = FormatClusterRepInterfacesPymolScript(pymolScriptDomainFile, clusterRepInterfaces);

           // add pymol script files in order to be compressed into the cluster file
            clusterFileList.Add(pymolScriptChainFile);
            clusterFileList.Add(pymolScriptDomainFile);
            clusterFileList.Add(pymolScriptRepChainFile);
            clusterFileList.Add(pymolScriptRepDomainFile);

            string[] clusterFiles = new string[clusterFileList.Count];
            clusterFileList.CopyTo(clusterFiles);

            string clusterFileName = relationName + "_" + clusterId + ".tar.gz";
            string clusterFile = fileCompress.RunTar(clusterFileName, clusterFiles, clusterFileDir, true);

            foreach (string clusterInterfaceFile in clusterFiles)
            {
                File.Delete(Path.Combine(clusterFileDir, clusterInterfaceFile));
            }
            return clusterFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterDomainInterfacesToBeAligned"></param>
        /// <returns></returns>
        private string[] CopyDomainInterfaceFilesFromChain(string[] clusterDomainInterfacesToBeAligned, 
            string pfamIdA, string pfamIdB, out string[] reverseDomainInterfaces)
        {
            string srcInterfaceFile = "";
            string destInterfaceFile = "";
            string hashDir = "";
            string pdbId = "";
            int interfaceId = 0;
            int domainInterfaceId = 0;
            List<string> clusterInterfaceFileList = new List<string> ();
            List<string> reverseDomainInterfaceList = new List<string> ();
            //      foreach (string chainInterface in chainInterfaceList)
            foreach (string alignDomainInterface in clusterDomainInterfacesToBeAligned)
            {
                string[] interfaceFields = alignDomainInterface.Split('_');
                pdbId = interfaceFields[0];
                interfaceId = Convert.ToInt32(interfaceFields[1]);
                domainInterfaceId = Convert.ToInt32(interfaceFields[2]);
                destInterfaceFile = Path.Combine(clusterFileDir, pdbId + "_d" + domainInterfaceId.ToString() + ".cryst.gz");

                if (interfaceId > 0)  // chain interface
                {
                    hashDir = Path.Combine(chainInterfaceFileDir, pdbId.Substring(1, 2));
                    srcInterfaceFile = Path.Combine(hashDir, pdbId + "_" + interfaceId.ToString() + ".cryst.gz");
                    // should change chain interface file name to domain interface file name when copying
                }
                else  // domain interface
                {
                    hashDir = Path.Combine(domainInterfaceFileDir, pdbId.Substring(1, 2));
                    srcInterfaceFile = Path.Combine(hashDir, pdbId + "_d" + domainInterfaceId.ToString() + ".cryst.gz");
                }
                try
                {
                    File.Copy(srcInterfaceFile, destInterfaceFile, true);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                    continue;
                }
                ParseHelper.UnZipFile(destInterfaceFile);
                clusterInterfaceFileList.Add(pdbId + "_d" + domainInterfaceId.ToString() + ".cryst");
                if (IsDomainInterfaceNeedReversed(destInterfaceFile.Replace (".gz", ""), pfamIdA, pfamIdB))
                {
                    reverseDomainInterfaceList.Add(alignDomainInterface);
                }
            }
            reverseDomainInterfaces = new string[reverseDomainInterfaceList.Count];
            reverseDomainInterfaceList.CopyTo(reverseDomainInterfaces);

            string[] clusterInterfaceFiles = new string[clusterInterfaceFileList.Count];
            clusterInterfaceFileList.CopyTo(clusterInterfaceFiles);
            return clusterInterfaceFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScript"></param>
        /// <param name="clusterRepInterfaces"></param>
        /// <returns></returns>
        private string FormatClusterRepInterfacesPymolScript(string pymolScript, string[] clusterRepInterfaces)
        {
            string pymolScriptFile = Path.Combine(clusterFileDir, pymolScript);
            string repPymolScript = pymolScript.Replace(".pml", "_rep.pml");
            string repPymolScriptFile = Path.Combine(clusterFileDir, repPymolScript);
            StreamReader dataReader = new StreamReader(pymolScriptFile);
            StreamWriter dataWriter = new StreamWriter(repPymolScriptFile);
            string line = "";
            string domainInterface = "";
            bool domainInterfaceIn = false;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("load") > -1)
                {
                    string[] fields = line.Split (' ');
                    domainInterface = fields[1].Replace(".cryst", "");
                    domainInterface = domainInterface.Replace("_d", "_");
                    if (clusterRepInterfaces.Contains(domainInterface))
                    {
                        domainInterfaceIn = true;
                    }
                    else
                    {
                        domainInterfaceIn = false;
                    }
                }
                if (domainInterfaceIn)
                {
                    dataWriter.WriteLine(line);
                }
                if (line.IndexOf("center") > -1 && !domainInterfaceIn)
                {
                    dataWriter.WriteLine(line);
                }
            }
            dataReader.Close();
            dataWriter.Close();
            return repPymolScript;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterTable"></param>
        /// <returns></returns>
        private string[] GetClusterDomainInterfacesToBeAligned(DataTable clusterTable)
        {
            List<string> entryList = new List<string> ();
            List<string> clusterDomainInterfaceList = new List<string> ();
            string pdbId = "";
            int domainInterfaceId = 0;
            int interfaceId = 0;
            int relSeqId = 0;
            string clusterInterface = "";
            foreach (DataRow interfaceRow in clusterTable.Rows)
            {
                pdbId = interfaceRow["PdbID"].ToString();
                relSeqId = Convert.ToInt32 (interfaceRow["RelSeqID"].ToString ());
                if (entryList.Contains(pdbId))  // only use one domain interface for each entry
                {
                    continue;
                }
                entryList.Add(pdbId);
                domainInterfaceId = Convert.ToInt32(interfaceRow["DomainInterfaceID"].ToString ());
                interfaceId = GetChainInterfaceId(relSeqId, pdbId, domainInterfaceId);
                clusterInterface = pdbId + "_" + interfaceId.ToString() + "_" + domainInterfaceId.ToString();
                clusterDomainInterfaceList.Add(clusterInterface);
            }
            string[] alignDomainInterfaces = new string[clusterDomainInterfaceList.Count];
            clusterDomainInterfaceList.CopyTo(alignDomainInterfaces);
            return alignDomainInterfaces;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private int GetChainInterfaceId(int relSeqId, string pdbId, int domainInterfaceId)
        {
            string queryString = string.Format("Select InterfaceId From PfamDomainInterfaces " +
                "Where RelSeqID = {0} AND PdbID = '{1}' AND DomainInterfaceID = {2};",
                relSeqId, pdbId, domainInterfaceId);
            DataTable interfaceIdTable = ProtCidSettings.protcidQuery.Query( queryString);
            int interfaceId = -1;
            if (interfaceIdTable.Rows.Count > 0)
            {
                interfaceId = Convert.ToInt32(interfaceIdTable.Rows[0]["InterfaceId"].ToString());
            }
            return interfaceId;
        }
        #endregion

        #region for pymol script file
        /// <summary>
        /// the first domain interface in the cluster in the alphabet order
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private string GetTheCenterInterfaceFile(int relSeqId, string[] clusterRepInterfaces, out int domainInterfaceId)
        {
            string[] centerFields = clusterRepInterfaces[0].Split('_');
            string centerInterface = "";
            string centerPdbId = centerFields[0];
            domainInterfaceId = Convert.ToInt32(centerFields[1]);
     /*       int centerInterfaceId = GetChainInterfaceId(relSeqId, centerPdbId, domainInterfaceId);
            if (centerInterfaceId == 0)
            {
                centerInterface = centerPdbId + "_d" + domainInterfaceId.ToString();
            }
            else
            {
                centerInterface = centerPdbId + "_" + centerInterfaceId.ToString();
            }*/
            centerInterface = centerPdbId + "_d" + domainInterfaceId.ToString();
            return centerInterface;
        }

        /// <summary>
        /// the domain interface with best average Q score with the other domain interfaces in a cluster
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterRepInterfaces"></param>
        /// <returns></returns>
        private string GetDomainInterfaceWithBestAvgQ (int relSeqId, string[] clusterDomainInterfaces, DataTable domainInterfaceCompTable)
        {
            double maxAvgQ = 0;
            double avgQ = 0;
            string bestQDomainInterface = "";
            foreach (string domainInterface in clusterDomainInterfaces)
            {
                avgQ = GetAverageQScore(domainInterface, clusterDomainInterfaces, domainInterfaceCompTable);
                if (maxAvgQ < avgQ)
                {
                    maxAvgQ = avgQ;
                    bestQDomainInterface = domainInterface;
                }
            }
            return bestQDomainInterface;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <param name="otherDomainInterfaces"></param>
        /// <param name="domainInterfaceCompTable"></param>
        /// <returns></returns>
        private double GetAverageQScore(string domainInterface, string[] clusterDomainInterfaces, DataTable domainInterfaceCompTable)
        {
            double sumQscore = 0;
            int numOfQ = 0;
            string[] interfaceFields = domainInterface.Split('_');
            string pdbId = interfaceFields[0];
            int domainInterfaceId = Convert.ToInt32(interfaceFields[1]);
            string compDomainInterface = "";
            double qscore = 0;
            DataRow[] interfaceCompRows = domainInterfaceCompTable.Select(string.Format ("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}'", pdbId, domainInterfaceId));

            foreach (DataRow compRow in interfaceCompRows)
            {
                compDomainInterface = compRow["PdbID2"].ToString() + "_" + compRow["DomainInterfaceID2"].ToString();
                if (clusterDomainInterfaces.Contains(compDomainInterface))
                {
                    qscore = Convert.ToDouble(compRow["Qscore"].ToString ());
                    sumQscore += qscore;
                    numOfQ++;
                }
            }

            interfaceCompRows = domainInterfaceCompTable.Select(string.Format("PdbID2 = '{0}' AND DomainInterfaceID2 = '{1}'", pdbId, domainInterfaceId));
            foreach (DataRow compRow in interfaceCompRows)
            {
                compDomainInterface = compRow["PdbID1"].ToString() + "_" + compRow["DomainInterfaceID1"].ToString();
                if (clusterDomainInterfaces.Contains(compDomainInterface))
                {
                    qscore = Convert.ToDouble(compRow["Qscore"].ToString ());
                    sumQscore += qscore;
                    numOfQ++;
                }
            }
         
            double avgQ = sumQscore / (double)numOfQ;
            return avgQ;
        }
        
        /// <summary>
        /// get one interface for each entry in the cluster
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        private string[] GetClusterRepDomainInterfaces(int relSeqId, int clusterId)
        {
            string queryString = string.Format("Select PdbID, DomainInterfaceID From PfamDomainInterfaceCluster " +
                " Where RelSeqID = {0} AND ClusterID = {1} Order By PdbID, DomainInterfaceID;", relSeqId, clusterId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> clusterDomainInterfaceList = new List<string> ();
            List<string> entryList = new List<string> ();
            string pdbId = "";
            string domainInterface = "";
            foreach (DataRow clusterRow in clusterInterfaceTable.Rows)
            {
                pdbId = clusterRow["PdbID"].ToString();
                if (entryList.Contains(pdbId))  // take the interface with the smallest interface number
                {
                    continue;
                }
                entryList.Add(pdbId);
                domainInterface = pdbId + "_" + clusterRow["DomainInterfaceID"].ToString();
                clusterDomainInterfaceList.Add(domainInterface);
            }
            string[] clusterDomainInterfaces = new string[clusterDomainInterfaceList.Count];
            clusterDomainInterfaceList.CopyTo(clusterDomainInterfaces);
            return clusterDomainInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterDomainInterfaces"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <param name="domainNo"></param>
        /// <returns></returns>
        private string[] GetClusterChainDomains(string[] clusterDomainInterfaces, DataTable domainInterfaceTable, int domainNo)
        {
            List<string> chainDomainList = new List<string> ();
            string pdbId = "";
            int domainInterfaceId = 0;
            string isReversed = "0";
            string chainDomain = "";
            foreach (string domainInterface in clusterDomainInterfaces)
            {
                string[] fields = domainInterface.Split('_');
                pdbId = fields[0];
                domainInterfaceId = Convert.ToInt32(fields[2]);
                DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
                isReversed = domainInterfaceRows[0]["IsReversed"].ToString();
                if (domainNo == 1 && isReversed == "0")
                {
                    chainDomain = pdbId + domainInterfaceRows[0]["ChainDomainID1"].ToString();
                }
                else if (domainNo == 1 && isReversed == "1")
                {
                    chainDomain = pdbId + domainInterfaceRows[0]["ChainDomainID2"].ToString();
                }
                else if (domainNo == 2 && isReversed == "0")
                {
                    chainDomain = pdbId + domainInterfaceRows[0]["ChainDomainID2"].ToString();
                }
                else if (domainNo == 2 && isReversed == "1")
                {
                    chainDomain = pdbId + domainInterfaceRows[0]["ChainDomainID1"].ToString();
                }
                if (!chainDomainList.Contains(chainDomain))
                {
                    chainDomainList.Add(chainDomain);
                }
            }
            string[] chainDomains = new string[chainDomainList.Count];
            chainDomainList.CopyTo(chainDomains);
            return chainDomains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFiles"></param>
        /// <returns></returns>
        public Dictionary<string, int[]> ReadInterfaceCalphaCoordSeqIds(string[] interfaceFileNames, string interfaceFileDir)
        {
            Dictionary<string, int[]> interfaceChainCoordSeqIdHash = new Dictionary<string,int[]> ();
            string interfaceFile = "";
            string interfaceRootName = "";
            foreach (string interfaceFileName in interfaceFileNames)
            {
                interfaceFile = Path.Combine(interfaceFileDir, interfaceFileName);
                interfaceRootName = interfaceFileName.Replace(".cryst", "");
                int[][] chainCoordSeqIds = ReadInterfaceChainCalphaCoordinates(interfaceFile);
                interfaceChainCoordSeqIdHash.Add(interfaceRootName + "_A", chainCoordSeqIds[0]);
                interfaceChainCoordSeqIdHash.Add(interfaceRootName + "_B", chainCoordSeqIds[1]);
            }
            return interfaceChainCoordSeqIdHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFile"></param>
        /// <returns></returns>
        public int[][] ReadInterfaceChainCalphaCoordinates(string interfaceFile)
        {
            List<int> chainACoordList = new List<int> ();
            List<int> chainBCoordList = new List<int> ();
            StreamReader dataReader = new StreamReader(interfaceFile);
            string line = "";
            int seqId = 0;
            string chainId = "";
            string atomName = "";
            bool isAtomDunplicate = false;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("ATOM  ") > -1)
                {
                    string[] items = ParseHelper.ParsePdbAtomLine(line);
                    seqId = Convert.ToInt32(items[6]);
                    chainId = items[5];
                    atomName = items[2];
                    if (atomName == "CA")
                    {
                        if (chainId == "A")
                        {
                            if (!chainACoordList.Contains(seqId))
                            {
                                chainACoordList.Add(seqId);
                            }
                            else
                            {
                                isAtomDunplicate = true;
                            }
                        }
                        else if (chainId == "B")
                        {
                            if (!chainBCoordList.Contains(seqId))
                            {
                                chainBCoordList.Add(seqId);
                            }
                            else
                            {
                                isAtomDunplicate = true;
                            }
                        }
                    }
                }
            }
            dataReader.Close();
            if (isAtomDunplicate)
            {
                RemoveDuplicateAtoms(interfaceFile);
            }
            int[][] chainCoordSeqIds = new int[2][];
            chainCoordSeqIds[0] = chainACoordList.ToArray ();
            chainCoordSeqIds[1] = chainBCoordList.ToArray ();
            return chainCoordSeqIds;
        }
        #endregion

        #region check the domain interface needed to be reversed when aligning
        /// <summary>
        /// check if the chain interface file needed to be reversed based on the pfam ids of the entity chains
        /// works for interfaces with different pfam ids
        /// </summary>
        /// <param name="domainInterfaceFile"></param>
        /// <param name="pfamIdA"></param>
        /// <param name="pfamIdB"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceNeedReversed(string domainInterfaceFile, string pfamIdA, string pfamIdB)
        {
            FileInfo fileInfo = new FileInfo(domainInterfaceFile);
            string pdbId = fileInfo.Name.Substring(0, 4);
            StreamReader dataReader = new StreamReader(domainInterfaceFile);
            string line = "";
            int entityIdA = 0;
            int entityIdB = 0;
            int entityIndex = 0;
            int symOpIndex = 0;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("Interface Chain A") > -1)
                {
                    entityIndex = line.IndexOf("Entity") + "Entity".Length;
                    symOpIndex = line.IndexOf("Symmetry");
                    entityIdA = Convert.ToInt32(line.Substring (entityIndex, symOpIndex - entityIndex).Trim ());
                }
                else if (line.IndexOf("Interface Chain B") > -1)
                {
                    entityIndex = line.IndexOf("Entity") + "Entity".Length;
                    symOpIndex = line.IndexOf("Symmetry");
                    entityIdB = Convert.ToInt32(line.Substring (entityIndex, symOpIndex - entityIndex).Trim ());
                }
            }
            dataReader.Close();
            if (entityIdA != entityIdB)
            {
                string[] entityAPfams = GetEntityPfams(pdbId, entityIdA);
                string[] entityBPfams = GetEntityPfams(pdbId, entityIdB);
                if (Array.IndexOf(entityAPfams, pfamIdA) < 0 && Array.IndexOf (entityBPfams, pfamIdA) > -1 && 
                    Array.IndexOf(entityBPfams, pfamIdB) < 0 && Array.IndexOf (entityAPfams, pfamIdB) > -1)
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
        /// <param name="entityId"></param>
        /// <returns></returns>
        private string[] GetEntityPfams(string pdbId, int entityId)
        {
            string queryString = string.Format("Select distinct Pfam_ID From PdbPfam Where PdbID = '{0}' AND EntityID = {1};", pdbId, entityId);
            DataTable pfamIdTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string[] entityPfamIds = new string[pfamIdTable.Rows.Count];
            int count = 0;
            string pfamId = "";
            foreach (DataRow pfamIdRow in pfamIdTable.Rows)
            {
                pfamId = pfamIdRow["Pfam_ID"].ToString().Trim();
                entityPfamIds[count] = pfamId;
                count++;
            }
            return entityPfamIds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerDomainInterface">pdbid _ domain interface id</param>
        /// <param name="clusterRepInterfaces">pdbid_domain interface id</param>
        /// <param name="alignClusterInterfaces">pdbid_chain interface id _ domain interface id</param>
        /// <returns></returns>
        private string[] GetClusterReversedDomainInterfaces(string centerDomainInterface, string[] clusterRepInterfaces, string[] alignClusterInterfaces)
        {
            string[] repEntries = GetClusterRepEntries(clusterRepInterfaces);
            Dictionary<string, string> homoRepEntryHash = GetHomoRepEntryHash (repEntries);
            string[] reversedRepInterfaces = GetReversedRepDomainInterfaces(centerDomainInterface, clusterRepInterfaces);

            string[] centerFields = centerDomainInterface.Split ('_');
            string centerPdbId = centerFields[0];
            int centerDomainInterfaceId = Convert.ToInt32(centerFields[1]);
            string clusterEntry = "";
            int domainInterfaceId = 0;
            string repEntry = "";
            List<string> reversedClusterInterfaceList = new List<string> ();
            bool isReversed = false;
            int bestRepDomainInterfaceId = 0;
            foreach (string clusterInterface in alignClusterInterfaces)
            {
                string[] interfaceFields = clusterInterface.Split('_');
                clusterEntry = interfaceFields[0];
                domainInterfaceId = Convert.ToInt32(interfaceFields[2]);

                if (reversedRepInterfaces.Contains(clusterEntry + "_" + domainInterfaceId.ToString()))
                {
                    reversedClusterInterfaceList.Add(clusterInterface);
                }
                // it is a homo entry in same CF
                if (homoRepEntryHash.ContainsKey(clusterEntry))
                {
                    repEntry = (string)homoRepEntryHash[clusterEntry];
                    int[] repDomainInterfaceIds = GetRepClusterInterfaceId(repEntry, clusterRepInterfaces);
                    isReversed = IsDomainInterfaceReversedByQ(repEntry, repDomainInterfaceIds, clusterEntry, domainInterfaceId, out bestRepDomainInterfaceId);
                    if (isReversed)
                    {
                        if (!reversedRepInterfaces.Contains(repEntry + "_" + bestRepDomainInterfaceId.ToString()))
                        {
                            reversedClusterInterfaceList.Add(clusterInterface);
                        }
                    }
                    else
                    {
                        if (reversedRepInterfaces.Contains(repEntry + "_" + bestRepDomainInterfaceId.ToString()))
                        {
                            reversedClusterInterfaceList.Add(clusterInterface);
                        }
                    }
                }
            }
            return reversedClusterInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="clusterDomainInterfaces"></param>
        /// <returns></returns>
        private int[] GetRepClusterInterfaceId(string pdbId, string[] clusterDomainInterfaces)
        {
            List<int> clusterDomainInterfaceIdList = new List<int> ();
            foreach (string domainInterface in clusterDomainInterfaces)
            {
                if (domainInterface.Substring(0, 4) == pdbId)
                {
                    string[] fields = domainInterface.Split('_');
                    clusterDomainInterfaceIdList.Add(Convert.ToInt32 (fields[1]));
                }
            }
            return clusterDomainInterfaceIdList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerDomainInterface"></param>
        /// <param name="clusterRepInterfaces"></param>
        /// <returns></returns>
        private string[] GetReversedRepDomainInterfaces(string centerDomainInterface, string[] clusterRepInterfaces)
        {
            List<string> reversedRepInterfaceList = new List<string> ();

            string[] centerFields = centerDomainInterface.Split('_');
            string centerPdbId = centerFields[0];
            int centerDomainInterfaceId = Convert.ToInt32(centerFields[1]);

            string clusterRepEntry = "";
            int repDomainInterfaceId = 0;
            foreach (string clusterInterface in clusterRepInterfaces)
            {
                string[] interfaceFields = clusterInterface.Split('_');
                clusterRepEntry = interfaceFields[0];
                repDomainInterfaceId = Convert.ToInt32(interfaceFields[1]);
                if (clusterInterface != centerDomainInterface)
                {
                    if (IsDomainInterfaceReversedByQ (centerPdbId, centerDomainInterfaceId, clusterRepEntry, repDomainInterfaceId))
                    {
                        reversedRepInterfaceList.Add(clusterInterface);
                    }
                }
            }
            return reversedRepInterfaceList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerPdbId"></param>
        /// <param name="centerDomainInterfaceId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceReversedByQ (string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2)
        {
            bool isReversed = false;
            string queryString = string.Format("Select * From PfamDomainInterfaceComp Where (PdbID1 = '{0}' AND DomainInterfaceID1 = {1} " +
                " AND PdbID2 = '{2}' AND DomainInterfaceID2 = {3}) OR (PdbID1 = '{2}' AND DomainInterfaceID1 = {3} " +
                " AND PdbID2 = '{0}' AND DomainInterfaceID2 = {1});", pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
            DataTable domainCompTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (domainCompTable.Rows.Count > 0)
            {
                if (domainCompTable.Rows[0]["IsReversed"].ToString() == "1")
                {
                    isReversed = true;
                }
            }
            return isReversed;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centerPdbId"></param>
        /// <param name="centerDomainInterfaceId"></param>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceReversedByQ(string pdbId1, int[] domainInterfaceIds1, string pdbId2, int domainInterfaceId2, out int bestRepDomainInterfaceId)
        {
            bool isReversed = false;
            string queryString = "";
            double maxQscore = 0;
            double qscore = 0;
            bestRepDomainInterfaceId = -1;
            foreach (int domainInterfaceId1 in domainInterfaceIds1)
            {
                queryString = string.Format("Select * From PfamDomainInterfaceComp Where (PdbID1 = '{0}' AND DomainInterfaceID1 = {1} " +
                    " AND PdbID2 = '{2}' AND DomainInterfaceID2 = {3}) OR (PdbID1 = '{2}' AND DomainInterfaceID1 = {3} " +
                    " AND PdbID2 = '{0}' AND DomainInterfaceID2 = {1});", pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
                DataTable domainCompTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (domainCompTable.Rows.Count > 0)
                {
                    qscore = Convert.ToDouble(domainCompTable.Rows[0]["Qscore"].ToString ());
                    if (qscore > maxQscore)
                    {
                        maxQscore = qscore;
                        if (domainCompTable.Rows[0]["IsReversed"].ToString() == "1")
                        {
                            isReversed = true;
                        }
                        else
                        {
                            isReversed = false;
                        }
                        bestRepDomainInterfaceId = domainInterfaceId1;
                    }
                }
            }
            return isReversed;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterRepInterfaces"></param>
        /// <returns></returns>
        private string[] GetClusterRepEntries(string[] clusterRepInterfaces)
        {
            List<string> repEntryList = new List<string> ();
            string pdbId = "";
            foreach (string repInterface in clusterRepInterfaces)
            {
                pdbId = repInterface.Substring(0, 4);
                if (!repEntryList.Contains(pdbId))
                {
                    repEntryList.Add(pdbId);
                }
            }
            return repEntryList.ToArray ();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterRepEntries"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetHomoRepEntryHash(string[] clusterRepEntries)
        {
            Dictionary<string, string> homoRepEntryHash = new Dictionary<string, string>();
            foreach (string repEntry in clusterRepEntries)
            {
                string[] homoEntries = GetRepHomoEntries(repEntry);
                foreach (string homoEntry in homoEntries)
                {
                    if (!homoRepEntryHash.ContainsKey(homoEntry))
                    {
                        homoRepEntryHash.Add(homoEntry, repEntry);
                    }
                }
            }
            return homoRepEntryHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repEntry"></param>
        /// <returns></returns>
        private string[] GetRepHomoEntries(string repEntry)
        {
            string queryString = string.Format("Select Distinct PdbID2 From PfamHomoRepEntryAlign Where PdbID1 = '{0}';", repEntry);
            DataTable homoEntryTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] homoEntries = new string[homoEntryTable.Rows.Count];
            int count = 0;
            foreach (DataRow homoEntryRow in homoEntryTable.Rows)
            {
                homoEntries[count] = homoEntryRow["PdbID2"].ToString();
                count++;
            }
            return homoEntries;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignClusterInterfaces"></param>
        /// <returns></returns>
        private Dictionary<string, bool> GetDomainInterfaceAlignReversHash(string[] alignClusterInterfaces)
        {
            Dictionary<string, bool> interfaceAlignReverseHash = new Dictionary<string, bool>();
            interfaceAlignReverseHash.Add(alignClusterInterfaces[0], false);
            string[] leftAlignClusterInterfaces = new string[alignClusterInterfaces.Length - 1];
            Array.Copy(alignClusterInterfaces, 1, leftAlignClusterInterfaces, 0, leftAlignClusterInterfaces.Length);
            GetDomainInterfaceReverseHash(leftAlignClusterInterfaces, ref interfaceAlignReverseHash);
            return interfaceAlignReverseHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alignClusterInterfaces"></param>
        /// <param name="interfaceAlignReverseHash"></param>
        private void GetDomainInterfaceReverseHash(string[] alignClusterInterfaces, ref Dictionary<string, bool> interfaceAlignReverseHash)
        {
            if (alignClusterInterfaces.Length == 0)
            {
                return;
            }

            int compReversed = -1;
            List<string> leftClusterInterfaceList = new List<string> (alignClusterInterfaces);
            List<string> currentClusterInterfaceList = new List<string> (alignClusterInterfaces);
            bool isHashInterfaceReversed = false;
            bool isAlignReversed = false;
            List<string> hashInterfaceList = new List<string> (interfaceAlignReverseHash.Keys);
            foreach (string hashInterface in hashInterfaceList)
            {
                string[] interfaceFields1 = hashInterface.Split('_');
                isHashInterfaceReversed = (bool)interfaceAlignReverseHash[hashInterface];
                foreach (string clusterInterface in currentClusterInterfaceList)
                {
                    //    string[] interfaceFields2 = alignClusterInterfaces[j].Split('_');
                    string[] interfaceFields2 = clusterInterface.Split('_');
                    compReversed = GetDomainInterfaceCompReversed(interfaceFields1[0], Convert.ToInt32(interfaceFields1[2]),
                        interfaceFields2[0], Convert.ToInt32(interfaceFields2[2]));
                    if (compReversed > -1)
                    {
                        if (isHashInterfaceReversed)
                        {
                            if (compReversed == 1)
                            {
                                isAlignReversed = false;
                            }
                            else
                            {
                                isAlignReversed = true;
                            }
                        }
                        else
                        {
                            if (compReversed == 1)
                            {
                                isAlignReversed = true;
                            }
                            else
                            {
                                isAlignReversed = false;
                            }
                        }
                        leftClusterInterfaceList.Remove(clusterInterface);
                        interfaceAlignReverseHash.Add(clusterInterface, isAlignReversed);
                    }
                }
            }

            string[] leftClusterInterfaces = leftClusterInterfaceList.ToArray();
            GetDomainInterfaceReverseHash(leftClusterInterfaces, ref interfaceAlignReverseHash);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId1"></param>
        /// <param name="domainInterfaceId1"></param>
        /// <param name="pdbId2"></param>
        /// <param name="domainInterfaceId2"></param>
        /// <returns></returns>
        private int GetDomainInterfaceCompReversed(string pdbId1, int domainInterfaceId1, string pdbId2, int domainInterfaceId2)
        {
            string queryString = string.Format("Select IsReversed From PfamDomainInterfaceComp " +
                    " Where ((PdbId1 = '{0}' AND DomainInterfaceID1 = {1} AND " +
                    " PdbID2 = '{2}' AND DomainInterfaceID2 = {3}) Or " +
                    " (PdbId1 = '{2}' AND DomainInterfaceID1 = {3} AND " +
                    " PdbID2 = '{0}' AND DomainInterfaceID2 = {1}));",
                    pdbId1, domainInterfaceId1, pdbId2, domainInterfaceId2);
            DataTable compReverseTable = ProtCidSettings.protcidQuery.Query( queryString);
            if (compReverseTable.Rows.Count > 0)
            {
                return Convert.ToInt32(compReverseTable.Rows[0]["IsReversed"].ToString());
            }
            return -1;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceReverseHash"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceReversedWithCenterInterface(string pdbId, int domainInterfaceId, Dictionary<string, bool> domainInterfaceReverseHash)
        {
            string queryString = "";
            List<string> domainInterfaceList = new List<string> (domainInterfaceReverseHash.Keys);
            domainInterfaceList.Sort();
            int hashDomainInterfaceId = 0;
            bool isCompReversed = false;
            bool isAlignReversed = false;
            foreach (string domainInterface in domainInterfaceList)
            {
                hashDomainInterfaceId = Convert.ToInt32(domainInterface.Substring(5, domainInterface.Length - 5));
                queryString = string.Format("Select Qscore From PfamDomainInterfaceComp " +
                    " Where ((PdbId1 = '{0}' AND DomainInterfaceID1 = {1} AND " +
                    " PdbID2 = '{2}' AND DomainInterfaceID2 = {3}) Or " +
                    " (PdbId1 = '{2}' AND DomainInterfaceID1 = {3} AND " +
                    " PdbID2 = '{0}' AND DomainInterfaceID2 = {1})) AND Qscore > 0.2;",
                    domainInterface.Substring(0, 4), hashDomainInterfaceId, pdbId, domainInterfaceId);
                DataTable domainInterfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);
                if (domainInterfaceCompTable.Rows.Count > 0)
                {
                    if (domainInterfaceCompTable.Rows[0]["IsReversed"].ToString() == "1")
                    {
                        isCompReversed = true;
                    }
                    bool hashDomainInterfaceReversed = (bool)domainInterfaceReverseHash[domainInterface];
                    if (hashDomainInterfaceReversed)
                    {
                        if (isCompReversed)
                        {
                            isAlignReversed = false;
                        }
                        else
                        {
                            isAlignReversed = true;
                        }
                    }
                    else
                    {
                        if (isCompReversed)
                        {
                            isAlignReversed = true;
                        }
                        else
                        {
                            isAlignReversed = false;
                        }
                    }
                    domainInterfaceReverseHash.Add(pdbId + "_" + domainInterfaceId.ToString(), isAlignReversed);
                    break;
                }
            }
            return isAlignReversed;
        }
        #endregion
      
        #region peptide interface files
        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <param name="clusterId"></param>
        /// <param name="clusterReverseFilesHash"></param>
        /// <returns></returns>
        public string CompressClusterPeptideInterfaceFiles(string pfamId, int clusterId, string[] clusterInterfaces, DataTable interfaceDefTable, 
            DataTable hmmSiteCompTable, string destFileDir)
        {
            string interfaceFile = "";
            string hashDir = "";
            string groupFileName = pfamId + "_" + clusterId.ToString();
            string pfamNumber = GetPfamNumberForPfamId(pfamId);
            List<string> clusterInterfaceFileList = new List<string> ();
       //     ArrayList clusterInterfaceToBeAlignedList = new ArrayList();
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (string clusterInterface in clusterInterfaces)
            {
                string[] interfaceInfo = GetClusterInterfaceInfo(clusterInterface);
                pdbId = interfaceInfo[0];
                domainInterfaceId = Convert.ToInt32(interfaceInfo[1]);

                if (clusterInterface.IndexOf("_d") > -1)
                {
                    hashDir = Path.Combine(domainInterfaceFileDir, clusterInterface.Substring(1, 2));
                }
                else
                {
                    hashDir = Path.Combine(chainInterfaceFileDir, clusterInterface.Substring(1, 2));
                }
                interfaceFile = Path.Combine(hashDir, clusterInterface + ".cryst.gz");
                
                try
                {
                    File.Copy(interfaceFile, Path.Combine(destFileDir, clusterInterface + ".cryst.gz"), true);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                    continue;  // skip those 
                }
                ParseHelper.UnZipFile(Path.Combine(destFileDir, clusterInterface + ".cryst.gz"));
                clusterInterfaceFileList.Add(clusterInterface + ".cryst");
                if (IsClusterInterfaceNeedReverse(pfamNumber, pdbId, domainInterfaceId, interfaceDefTable))
                {
                    if (clusterInterface.IndexOf("_d") > -1)
                    {
                        ReverseDomainInterfaceFile(Path.Combine(destFileDir, clusterInterface + ".cryst"));
                    }
                    else
                    {
                        ReverseChainInterfaceFile(Path.Combine(destFileDir, clusterInterface + ".cryst"));
                    }
                    UpdatePfamDomainInterfaceDataRow(pdbId, domainInterfaceId, interfaceDefTable);
                }
            }
            clusterInterfaceFileList.Sort();
            string[] clusterInterfaceFiles = new string[clusterInterfaceFileList.Count];
            clusterInterfaceFileList.CopyTo(clusterInterfaceFiles);

            string pymolScriptFileName = groupFileName;
            string[] pymolScriptFiles = WritePeptideInterfaceClusterPymolScriptFiles(clusterInterfaceFiles, interfaceDefTable, hmmSiteCompTable,
                destFileDir, pymolScriptFileName, pfamId);

            // add pymol script files into cluster tar filed
            clusterInterfaceFileList.AddRange(pymolScriptFiles);
            string[] pymolClusterInterfaceFiles = new string[clusterInterfaceFileList.Count];
            clusterInterfaceFileList.CopyTo(pymolClusterInterfaceFiles);

            string groupTarFileName = groupFileName + ".tar.gz";
            string clusterFile = fileCompress.RunTar(groupTarFileName, pymolClusterInterfaceFiles, destFileDir, true);

            foreach (string clusterInterfaceFile in pymolClusterInterfaceFiles)
            {
                File.Delete(Path.Combine(destFileDir, clusterInterfaceFile));
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
        public string CompressClusterPeptideInterfaceFiles(string pfamId, int clusterId, string[] clusterInterfaces, DataTable interfaceDefTable, string destFileDir)
        {
            string interfaceFile = "";
            string hashDir = "";
            string groupFileName = pfamId + "_" + clusterId.ToString();
            string pfamNumber = GetPfamNumberForPfamId(pfamId);
            List<string> clusterInterfaceFileList = new List<string> ();
            //     ArrayList clusterInterfaceToBeAlignedList = new ArrayList();
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (string clusterInterface in clusterInterfaces)
            {
                string[] interfaceInfo = GetClusterInterfaceInfo(clusterInterface);
                pdbId = interfaceInfo[0];
                domainInterfaceId = Convert.ToInt32(interfaceInfo[1]);

                if (clusterInterface.IndexOf("_d") > -1)
                {
                    hashDir = Path.Combine(domainInterfaceFileDir, clusterInterface.Substring(1, 2));
                }
                else
                {
                    hashDir = Path.Combine(chainInterfaceFileDir, clusterInterface.Substring(1, 2));
                }
                interfaceFile = Path.Combine(hashDir, clusterInterface + ".cryst.gz");

                try
                {
                    File.Copy(interfaceFile, Path.Combine(destFileDir, clusterInterface + ".cryst.gz"), true);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                    continue;  // skip those 
                }
                ParseHelper.UnZipFile(Path.Combine(destFileDir, clusterInterface + ".cryst.gz"));
                clusterInterfaceFileList.Add(clusterInterface + ".cryst");
                if (IsClusterInterfaceNeedReverse(pfamNumber, pdbId, domainInterfaceId, interfaceDefTable))
                {
                    if (clusterInterface.IndexOf("_d") > -1)
                    {
                        ReverseDomainInterfaceFile(Path.Combine(destFileDir, clusterInterface + ".cryst"));
                    }
                    else
                    {
                        ReverseChainInterfaceFile(Path.Combine(destFileDir, clusterInterface + ".cryst"));
                    }
                    UpdatePfamDomainInterfaceDataRow(pdbId, domainInterfaceId, interfaceDefTable);
                }
            }

            string[] clusterInterfaceFiles = new string[clusterInterfaceFileList.Count];
            clusterInterfaceFileList.CopyTo(clusterInterfaceFiles);

            //        string pymolScriptFileName = groupSeqId.ToString() + "_" + clusterId.ToString();
            string pymolScriptFileName = groupFileName;
            string[] pymolScriptFiles = WritePeptideInterfaceClusterPymolScriptFiles(clusterInterfaceFiles, interfaceDefTable, destFileDir, pymolScriptFileName, pfamId);

            // add pymol script files into cluster tar filed
            clusterInterfaceFileList.AddRange(pymolScriptFiles);
            string[] pymolClusterInterfaceFiles = new string[clusterInterfaceFileList.Count];
            clusterInterfaceFileList.CopyTo(pymolClusterInterfaceFiles);

            string groupTarFileName = groupFileName + ".tar.gz";
            string clusterFile = fileCompress.RunTar(groupTarFileName, pymolClusterInterfaceFiles, destFileDir, true);

            foreach (string clusterInterfaceFile in pymolClusterInterfaceFiles)
            {
                File.Delete(Path.Combine(destFileDir, clusterInterfaceFile));
            }
            return clusterFile;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        /// <param name="interfaceDefTable"></param>
        /// <returns></returns>
        private string[] GetFirstChainDomainsFromInterfaces(string[] clusterInterfaces, DataTable interfaceDefTable)
        {
            List<string> chainDomainList = new List<string> ();
            string pdbId = "";
            int domainInterfaceId = 0;
            string chainDomain = "";
            foreach (string clusterInterface in clusterInterfaces)
            {
                string[] fields = clusterInterface.Split('_');
                pdbId = fields[0];
                domainInterfaceId = Convert.ToInt32(fields[1].Substring (1, fields.Length - 1));
                chainDomain = GetInterfaceChainDomain(pdbId, domainInterfaceId, interfaceDefTable);
                chainDomainList.Add(chainDomain);
            }
            return chainDomainList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="interfaceDefTable"></param>
        /// <returns></returns>
        private string GetInterfaceChainDomain(string pdbId, int domainInterfaceId, DataTable interfaceDefTable)
        {
            DataRow[] domainInterfaceRows = interfaceDefTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceID= '{1}'", pdbId, domainInterfaceId));
            string entryChainDomain = "";
            if (domainInterfaceRows.Length > 0)
            {
                entryChainDomain = pdbId + domainInterfaceRows[0]["ChainDomainID1"].ToString();
            }
            return entryChainDomain;
        }

        #region pair_fit pymol script
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaceFiles"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <param name="interfaceFileDir"></param>
        /// <param name="pymolScriptFileName"></param>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] WritePeptideInterfaceClusterPymolScriptFiles(string[] clusterInterfaceFiles, DataTable domainInterfaceTable,
                            DataTable hmmSiteCompTable, string interfaceFileDir, string pymolScriptFileName, string pfamId)
        {
            DataTable pfamChainDomainTable = interfacePymolScript.GetPfamChainDomainTable(pfamId);
            if (pfamMultiChainDomainHash.ContainsKey(pfamId))
            {
                interfacePymolScript.UpdatePfamMultiChainDomains(pfamChainDomainTable, (long[])pfamMultiChainDomainHash[pfamId]);
            }
            
            Dictionary<string, int[]> domainInterfaceChainCoordSeqIdHash = ReadInterfaceCalphaCoordSeqIds(clusterInterfaceFiles, interfaceFileDir);
            string[] pymolScriptFiles = interfacePymolScript.FormatPeptideInterfacePymolScriptFiles(clusterInterfaceFiles, domainInterfaceTable, 
                pfamChainDomainTable, hmmSiteCompTable, domainInterfaceChainCoordSeqIdHash, pymolScriptFileName, interfaceFileDir);
            return pymolScriptFiles;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaceFiles"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <param name="interfaceFileDir"></param>
        /// <param name="pymolScriptFileName"></param>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string[] WritePeptideInterfaceClusterPymolScriptFiles(string[] clusterInterfaceFiles, DataTable domainInterfaceTable,
                             string interfaceFileDir, string pymolScriptFileName, string pfamId)
        {
            DataTable pfamChainDomainTable = interfacePymolScript.GetPfamChainDomainTable(pfamId);
            if (pfamMultiChainDomainHash.ContainsKey(pfamId))
            {
                interfacePymolScript.UpdatePfamMultiChainDomains(pfamChainDomainTable, (long[])pfamMultiChainDomainHash[pfamId]);
            }
            Dictionary<string, int[]> domainInterfaceChainCoordSeqIdHash = ReadInterfaceCalphaCoordSeqIds(clusterInterfaceFiles, interfaceFileDir);
            string[] pymolScriptFiles = interfacePymolScript.FormatPeptideInterfacePymolScriptFiles(clusterInterfaceFiles, domainInterfaceTable,
                pfamChainDomainTable, domainInterfaceChainCoordSeqIdHash, pymolScriptFileName, interfaceFileDir);
            return pymolScriptFiles;
        }
        #endregion
        #endregion

        #region multi-chain domain interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <param name="domainInterfaces"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private string[] GetMultiChainDomainInterfaces(int relSeqId, string[] domainInterfaces, DataTable domainInterfaceTable, Dictionary<string, long[]> pfamMultiChainDomainHash)
        {
            string[] pfamPair = GetRelationPfamPair(relSeqId);
            List<long> relMultiChainDomainList = new List<long> ();
            if (pfamMultiChainDomainHash.ContainsKey(pfamPair[0]))
            {
                long[] multiChainDomainIds0 = pfamMultiChainDomainHash[pfamPair[0]];
                relMultiChainDomainList.AddRange(multiChainDomainIds0);
            }
            if (pfamPair[1] != pfamPair[1] && pfamMultiChainDomainHash.ContainsKey(pfamPair[1]))
            {
                long[] multiChainDomainIds1 = pfamMultiChainDomainHash[pfamPair[1]];
                relMultiChainDomainList.AddRange(multiChainDomainIds1);
            }
            long[] multiChainDomainIds = new long[relMultiChainDomainList.Count];
            relMultiChainDomainList.CopyTo (multiChainDomainIds);

            List<string> multiChainDomainInterfaceList = new List<string> ();
            string pdbId = "";
            int domainInterfaceId = 0;
            foreach (string domainInterface in domainInterfaces)
            {
                string[] fields = domainInterface.Split('_'); // pdbid + interface id + domain interface id
                pdbId = fields[0];
                domainInterfaceId = Convert.ToInt32(fields[2]);
                if (IsDomainInterfaceMultiChain(pdbId, domainInterfaceId, domainInterfaceTable, multiChainDomainIds))
                {
                    multiChainDomainInterfaceList.Add(domainInterface);
                }
            }
            return multiChainDomainInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <param name="multiChainDomainIds"></param>
        /// <returns></returns>
        private bool IsDomainInterfaceMultiChain(string pdbId, int domainInterfaceId, DataTable domainInterfaceTable, long[] multiChainDomainIds)
        {
            DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format("PdbId = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            long domainId = Convert.ToInt64(domainInterfaceRows[0]["DomainID1"].ToString());
            if (Array.IndexOf(multiChainDomainIds, domainId) > -1)
            {
                return true;
            }
            domainId = Convert.ToInt64(domainInterfaceRows[0]["DomainId2"].ToString ());
            if (Array.IndexOf(multiChainDomainIds, domainId) > -1)
            {
                return true;
            }
            return false;
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqId"></param>
        /// <returns></returns>
        private string[] GetRelationPfamPair(int relSeqId)
        {
            string queryString = string.Format("Select FamilyCode1, FamilyCode2 From PfamDomainFamilyRelation Where RelSeqID = {0};", relSeqId);
            DataTable pfamPairTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] pfamPair = new string[2];
            pfamPair[0] = pfamPairTable.Rows[0]["FamilyCode1"].ToString().TrimEnd();
            pfamPair[1] = pfamPairTable.Rows[0]["FamilyCode2"].ToString().TrimEnd();
            return pfamPair;
        }
        #endregion

        #region remove duplicate atoms from interface file
        public void RemoveDuplicateAtoms(string interfaceFile)
        {
            FileInfo fileInfo = new FileInfo (interfaceFile);
            string newInterfaceFile = Path.Combine(fileInfo.DirectoryName, fileInfo.Name.Replace(".cryst", "_new.cryst"));
            StreamWriter dataWriter = new StreamWriter(newInterfaceFile);
            StreamReader dataReader = new StreamReader(interfaceFile);
            string line = "";
            int seqId = 0;
            string chainId = "";
            string atomName = "";
            List<string> addedAtomList = new List<string> ();
            string chainSeqAtomName = "";
            int atomId = 1;
            bool atomIdReplaceStart = false;
            string newAtomLine = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("ATOM  ") > -1)
                {
                    string[] items = ParseHelper.ParsePdbAtomLine(line);
                    seqId = Convert.ToInt32(items[6]);
                    chainId = items[5];
                    atomName = items[2];
                    chainSeqAtomName = chainId + "_" + items[6] + "_" + atomName;
                    if (addedAtomList.Contains(chainSeqAtomName))
                    {
                        atomIdReplaceStart = true;
                        continue;
                    }
                    if (atomIdReplaceStart)
                    {
                        newAtomLine = ReplaceTheAtomId(line, atomId);
                        dataWriter.WriteLine(newAtomLine);
                    }
                    else
                    {
                        dataWriter.WriteLine(line);
                    }
                    addedAtomList.Add(chainSeqAtomName);
                    atomId++;
                }
                else if (line.IndexOf("TER   ") > -1)
                {
                    if (atomIdReplaceStart)
                    {
                        newAtomLine = ReplaceTheAtomId(line, atomId);
                        dataWriter.WriteLine(newAtomLine);
                    }
                    else
                    {
                        dataWriter.WriteLine(line);
                    }
                    atomId++;
                }
                else
                {
                    dataWriter.WriteLine(line);
                }
                
            }
            dataReader.Close();
            dataWriter.Close();
            // make the new file have the interface file name
            File.Delete(interfaceFile);
            File.Move(newInterfaceFile, interfaceFile);
            // update the original interface files for future usage
      /*      string originalInterfaceFile = GetTheOriginalInterfaceFile(fileInfo.Name);
            File.Copy(interfaceFile, originalInterfaceFile);
            ParseHelper.ZipPdbFile(originalInterfaceFile);*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFileName"></param>
        /// <returns></returns>
        private string GetTheOriginalInterfaceFile (string interfaceFileName)
        {
            string originalInterfaceFile = "";
            string fileHashFolder = "";
            if (interfaceFileName.IndexOf("_d") > -1)
            {
                fileHashFolder = Path.Combine(domainInterfaceFileDir, interfaceFileName.Substring(1, 2));
            }
            else
            {
                fileHashFolder = Path.Combine(chainInterfaceFileDir, interfaceFileName.Substring(1, 2));
            }
            originalInterfaceFile = Path.Combine(fileHashFolder, interfaceFileName);
            return originalInterfaceFile;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="atomLine"></param>
        /// <param name="atomId"></param>
        /// <returns></returns>
        private string ReplaceTheAtomId(string atomLine, int atomId)
        {
            string newAtomLine = atomLine;
            string atomIdString = atomId.ToString().PadLeft(5, ' ');
            newAtomLine = newAtomLine.Remove(6, 5);
            newAtomLine = newAtomLine.Insert(6, atomIdString);
            return newAtomLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="atomLine"></param>
        /// <param name="chainId"></param>
        /// <returns></returns>
        private string ReplaceTheChainId(string atomLine, string chainId)
        {
            string newAtomLine = atomLine;
            newAtomLine = newAtomLine.Remove(21, 1);
            newAtomLine = newAtomLine.Insert(21, chainId);
            return newAtomLine;
        }
        #endregion

        #region reverse interface files
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterface"></param>
        /// <param name="domainInterfaceTable"></param>
        /// <returns></returns>
        private bool IsClusterInterfaceNeedReverse(string pfamNumber, string pdbId, int domainInterfaceId, DataTable domainInterfaceTable)
        {
            DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId,  domainInterfaceId));
            if (domainInterfaceRows.Length > 0)
            {
                string domainId1 = domainInterfaceRows[0]["DomainID1"].ToString();
                string domainId2 = domainInterfaceRows[0]["DomainID2"].ToString();
                if (domainId1 == "-1" || domainId1 == "")
                {
                    domainId1 = domainId1.PadRight(pfamNumber.Length);
                }
                if (domainId2 == "-1" || domainId2 == "")
                {
                    domainId2 = domainId2.PadRight(pfamNumber.Length);
                }
                if (domainId1.Substring(0, pfamNumber.Length) != pfamNumber &&
                    domainId2.Substring(0, pfamNumber.Length) == pfamNumber)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string GetPfamNumberForPfamId (string pfamId)
        {
            string pfamAcc = GetPfamAccessionCodeForPfamId(pfamId);
            string pfamType = pfamAcc.Substring(0, 2);
            string pfamNumber = "";
            if (pfamType == "PF")
            {
                pfamNumber = pfamAcc.Replace("PF", "1");
            }
            else if (pfamType == "PB")
            {
                pfamNumber = pfamAcc.Replace ("PB", "2");
            }
            return pfamNumber;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pfamId"></param>
        /// <returns></returns>
        private string GetPfamAccessionCodeForPfamId(string pfamId)
        {
            string queryString = string.Format("Select Pfam_Acc From PfamHmm Where Pfam_ID = '{0}';", pfamId);
            DataTable pfamAccTable = ProtCidSettings.pdbfamQuery.Query( queryString);
            string pfamAcc = "";
            if (pfamAccTable.Rows.Count > 0)
            {
                pfamAcc = pfamAccTable.Rows[0]["Pfam_Acc"].ToString().TrimEnd();
            }
            else
            {
                pfamAcc = pfamId;
            }
            return pfamAcc;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterface"></param>
        /// <returns></returns>
        private string[] GetClusterInterfaceInfo(string clusterInterface)
        {
            string[] interfaceInfo = new string[2];
            clusterInterface = clusterInterface.Replace(".cryst", "");  // remove the extension
            int interfaceIdIndex = clusterInterface.IndexOf('_');
            if (interfaceIdIndex > -1)  // cluster interface format: 1xxx_d3 for domain interface, 1xxx_3 for chain interface
            {
                interfaceInfo[0] = clusterInterface.Substring(0, 4);
                interfaceInfo[1] = clusterInterface.Substring(interfaceIdIndex + 1, clusterInterface.Length - interfaceIdIndex - 1).Replace("d", "");
            }
            else  // format: 1xxx3
            {
                interfaceInfo[0] = clusterInterface.Substring(0, 4);
                interfaceInfo[1] = clusterInterface.Substring(4, clusterInterface.Length - 4);
            }
            return interfaceInfo;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="domainInterfaceId"></param>
        /// <param name="domainInterfaceTable"></param>
        private void UpdatePfamDomainInterfaceDataRow(string pdbId, int domainInterfaceId, DataTable  domainInterfaceTable)
        {
            DataRow[] domainInterfaceRows = domainInterfaceTable.Select(string.Format ("PdbID = '{0}' AND DomainInterfaceID = '{1}'", pdbId, domainInterfaceId));
            DataRow domainInterfaceRow = domainInterfaceRows[0];
            object temp = domainInterfaceRow["DomainID1"];
            domainInterfaceRow["DomainID1"] = domainInterfaceRow["DomainID2"];
            domainInterfaceRow["DomainID2"] = temp;
            temp = domainInterfaceRow["AsymChain1"];
            domainInterfaceRow["AsymChain1"] = domainInterfaceRow["AsymChain2"];
            domainInterfaceRow["AsymChain2"] = temp;
            temp = domainInterfaceRow["ChainDomainID1"];
            domainInterfaceRow["ChainDomainID1"] = domainInterfaceRow["ChainDomainID2"];
            domainInterfaceRow["ChainDomainID2"] = temp;
            domainInterfaceTable.AcceptChanges();
            // the domain interface row is updated
    /*        string updateString = string.Format("Update PfamDomainInterfaces Set DomainID1 = {0}, AsymChain1 = '{1}', ChainDomainID1 = {2}, " +
                " DomainID2 = {3}, AsymChain2 = '{4}', ChainDomainID2 = {5} Where PdbID = '{6}' AND DomainInterfaceID = {7};",
                domainInterfaceRow["DomainID1"], domainInterfaceRow["AsymChain1"], domainInterfaceRow["ChainDomainID1"], 
                domainInterfaceRow["DomainID2"], domainInterfaceRow["AsymChain2"], domainInterfaceRow["ChainDomainID2"], pdbId, domainInterfaceId);
            dbUpdate.Update(updateString);*/
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFile"></param>
        public void ReverseDomainInterfaceFile(string interfaceFile)
        {
            FileInfo fileInfo = new FileInfo (interfaceFile);
            string newInterfaceFile = Path.Combine(fileInfo.DirectoryName, fileInfo.Name.Replace(".cryst", "_new.cryst"));
            StreamWriter dataWriter = new StreamWriter(newInterfaceFile);
            StreamReader dataReader = new StreamReader(interfaceFile);
            string line = "";
            string chainId = "";
            List<string> addedAtomList = new List<string> ();
            int atomId = 1;
            List<string> chainAAtomLineList = new List<string>();
            List<string> chainBAtomLineList = new List<string>();
            string updatePfamDomainRemarkString = "";
            string updateRemarkString = "";
            string updateLine = "";
            string updateAtomLine = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("ATOM  ") > -1)
                {
                    string[] items = ParseHelper.ParsePdbAtomLine(line);

                    chainId = items[5];

                    if (chainId == "A")
                    {
                        chainAAtomLineList.Add(line);
                    }
                    else if (chainId == "B")
                    {
                        chainBAtomLineList.Add(line);
                    }
                }
                else if (line.IndexOf("TER  ") > -1)
                {
                    chainId = line.Substring(21, 1);
                    if (chainId == "A")
                    {
                        chainAAtomLineList.Add(line);
                    }
                    else if (chainId == "B")
                    {
                        chainBAtomLineList.Add(line);
                    }
                }
                else if (line == "END")
                {
                    continue;
                }
                else if (line.IndexOf("PFAM Domain") > -1)
                {
                    updateLine = ReversePfamDomainRemark(line);
                    if (updatePfamDomainRemarkString == "")
                    {
                        updatePfamDomainRemarkString = updateLine;
                    }
                    else
                    {
                        updatePfamDomainRemarkString = updateLine + "\r\n" + updatePfamDomainRemarkString;
                        dataWriter.WriteLine(updatePfamDomainRemarkString);
                    }
                }
                else
                {
                    updateRemarkString = ReverseRemark(line);
                    dataWriter.WriteLine(updateRemarkString);
                }
            }
            dataReader.Close();
           // write original chain B atoms first
            foreach (string atomLine in chainBAtomLineList)
            {
                updateAtomLine = ReplaceTheChainId(atomLine, "A"); // change the chain id from B to A
                updateAtomLine = ReplaceTheAtomId(updateAtomLine, atomId);
                dataWriter.WriteLine(updateAtomLine);
                atomId++;
            }
            foreach (string atomLine in chainAAtomLineList)
            {
                updateAtomLine = ReplaceTheChainId(atomLine, "B"); // change the chain id from A to B
                updateAtomLine = ReplaceTheAtomId(updateAtomLine, atomId);
                dataWriter.WriteLine(updateAtomLine);
                atomId++;
            }
            dataWriter.WriteLine("END");
            dataWriter.Close();
            // make the new file have the interface file name
            File.Delete(interfaceFile);
            File.Move(newInterfaceFile, interfaceFile);
            // update the original interface files for future usage, 
            // should also update the info in the db, otherwise, just mess up the data, so comment out
     /*       string originalInterfaceFile = GetTheOriginalInterfaceFile(fileInfo.Name);
            File.Copy(interfaceFile, originalInterfaceFile);
            ParseHelper.ZipPdbFile(originalInterfaceFile);*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remarkString"></param>
        /// <returns></returns>
        private string ReversePfamDomainRemark (string remarkString)
        {
            string updateLine = "";
            if (remarkString.IndexOf("PFAM Domain 1") > -1)
            {
                updateLine = remarkString.Replace("PFAM Domain 1", "PFAM Domain 2");
            }
            else if (remarkString.IndexOf ("PFAM Domain 2") > -1)
            {
                updateLine = remarkString.Replace("PFAM Domain 2", "PFAM Domain 1");
            }
            return updateLine;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="remarkString"></param>
        /// <returns></returns>
        private string ReverseRemark(string remarkString)
        {
            string pattern = "";
            int chainIndex = remarkString.IndexOf("Asymmetric Chain");
            if (chainIndex > -1)
            {
                pattern = "Asymmetric Chain";
            }
            else if (remarkString.IndexOf("Entity ID") > -1)
            {
                pattern = "Entity ID";
            }
            if (pattern == "")
            {
                return remarkString;
            }
            string updateLine = ReverseRemark(remarkString, pattern);
            return updateLine;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remarkString"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
        private string ReverseRemark(string remarkString, string pattern)
        {
            int patternIndex = remarkString.IndexOf(pattern);
            string prePatternLine = remarkString.Substring(0, patternIndex);
            string patternLine = remarkString.Substring(patternIndex, remarkString.Length - patternIndex);
            string[] patternFields = patternLine.Split(';');
            string updateLine = "";
            if (patternFields.Length == 2)
            {
                string[] pattern1Fields = patternFields[0].Split(':');
                string[] pattern2Fields = patternFields[1].Split(':');
                updateLine = prePatternLine + " " + pattern1Fields[0] + ":" + pattern2Fields[1] + "; " +
                    pattern2Fields[0] + ":" + pattern1Fields[1];
            }
            else
            {
                updateLine = remarkString;
            }

            return updateLine;
        }


        #region reverse chain interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFile"></param>
        public void ReverseChainInterfaceFile(string interfaceFile)
        {
            FileInfo fileInfo = new FileInfo(interfaceFile);
            string newInterfaceFile = Path.Combine(fileInfo.DirectoryName, fileInfo.Name.Replace(".cryst", "_new.cryst"));
            StreamWriter dataWriter = new StreamWriter(newInterfaceFile);
            StreamReader dataReader = new StreamReader(interfaceFile);
            string line = "";
            string chainId = "";
            List<string> addedAtomList = new List<string>();
            int atomId = 1;
            List<string> chainAAtomLineList = new List<string>();
            List<string> chainBAtomLineList = new List<string>();
            string updateRemarkString = "";
            string updateAtomLine = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("ATOM  ") > -1)
                {
                    string[] items = ParseHelper.ParsePdbAtomLine(line);

                    chainId = items[5];

                    if (chainId == "A")
                    {
                        chainAAtomLineList.Add(line);
                    }
                    else if (chainId == "B")
                    {
                        chainBAtomLineList.Add(line);
                    }
                }
                else if (line.IndexOf("TER  ") > -1)
                {
                    chainId = line.Substring(21, 1);
                    if (chainId == "A")
                    {
                        chainAAtomLineList.Add(line);
                    }
                    else if (chainId == "B")
                    {
                        chainBAtomLineList.Add(line);
                    }
                }
                else if (line == "END")
                {
                    continue;
                }
                else if (line.IndexOf("Interface Chain A") > -1)
                {
                    string chainBRemarkString = dataReader.ReadLine();
                    updateRemarkString = chainBRemarkString.Replace("Interface Chain B", "Interface Chain A");
                    dataWriter.WriteLine(updateRemarkString);
                    updateRemarkString = line.Replace("Interface Chain A", "Interface Chain B");
                    dataWriter.WriteLine(updateRemarkString);
                }
                else
                {
                    dataWriter.WriteLine(line);
                }
            }
            dataReader.Close();
            // write original chain B atoms first
            foreach (string atomLine in chainBAtomLineList)
            {
                updateAtomLine = ReplaceTheChainId(atomLine, "A"); // change the chain id from B to A
                updateAtomLine = ReplaceTheAtomId(updateAtomLine, atomId);
                dataWriter.WriteLine(updateAtomLine);
                atomId++;
            }
            foreach (string atomLine in chainAAtomLineList)
            {
                updateAtomLine = ReplaceTheChainId(atomLine, "B"); // change the chain id from A to B
                updateAtomLine = ReplaceTheAtomId(updateAtomLine, atomId);
                dataWriter.WriteLine(updateAtomLine);
                atomId++;
            }
            dataWriter.WriteLine("END");
            dataWriter.Close();
            // make the new file have the interface file name
            File.Delete(interfaceFile);
            File.Move(newInterfaceFile, interfaceFile);
        }
        #endregion
        #endregion     

        #region for debug
        public void CheckModifiedInterfaceFiles()
        {
       //     string interfaceFileDir = @"D:\DbProjectData\InterfaceFiles_update\cryst";
            string changeFileDir = @"D:\DbProjectData\InterfaceFiles_update\updateFileDir"; ;
        //    DateTime dtCutoff = new DateTime(2013, 7, 1);
        //    ParseHelper.CopyNewFiles(interfaceFileDir, changeFileDir, dtCutoff);
           
            string ungzInterfaceFile = "";
            string gzInterfaceFile = "";
            List<string> moveInterfaceFileList = new List<string>();
            string[] hashFolders = Directory.GetDirectories(changeFileDir);
            foreach (string hashFolder in hashFolders)
            {
                string[] changedInterfaceFiles = Directory.GetFiles(hashFolder);
                foreach (string interfaceFile in changedInterfaceFiles)
                {
                    ungzInterfaceFile = ParseHelper.UnZipFile(interfaceFile);
                    if (IsInterfaceFileReversed(ungzInterfaceFile))
                    {
                        ReverseChainInterfaceFile(ungzInterfaceFile);

                        ParseHelper.ZipPdbFile(ungzInterfaceFile);
                        gzInterfaceFile = ungzInterfaceFile + ".gz";

                        moveInterfaceFileList.Add(gzInterfaceFile);
                    }
                    else
                    {
                        File.Delete(ungzInterfaceFile);
                    }
                }
            }
        }

        private bool IsInterfaceFileReversed(string interfaceFile)
        {
            FileInfo fileInfo = new FileInfo(interfaceFile);
            string chainInterface = fileInfo.Name.Replace(".cryst", "");
            string pdbId = chainInterface.Substring(0, 4);
            int interfaceId = Convert.ToInt32(chainInterface.Substring(5, chainInterface.Length - 5));
            string[] dbAsymChains = GetInterfaceChains(pdbId, interfaceId);
            string[] fileAsymChains = ReadInterfaceChains(interfaceFile);
            if (dbAsymChains[0] == fileAsymChains[1] && dbAsymChains[1] == fileAsymChains[0])
            {
                return true;
            }
            return false;
        }

        private string[] ReadInterfaceChains(string interfaceFile)
        {
            string[] interfaceAsymChains = new string[2];
            StreamReader dataReader = new StreamReader(interfaceFile);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("Interface Chain A") > -1)
                {
                    interfaceAsymChains[0] = ReadAsymChainFromChainLine(line);
                    string chainBRemarkString = dataReader.ReadLine();
                    interfaceAsymChains[1] = ReadAsymChainFromChainLine(chainBRemarkString);
                }
            }
            dataReader.Close(); 
            return interfaceAsymChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataLine"></param>
        /// <returns></returns>
        private string ReadAsymChainFromChainLine(string dataLine)
        {
            int asymChainIndex = dataLine.IndexOf("Asymmetric Chain");
            int asymChainEndIndex = dataLine.IndexOf("Author Chain");
            string chain = dataLine.Substring(asymChainIndex + "Asymmetric Chain".Length + 1,
                asymChainEndIndex - asymChainIndex - "Asymmetric Chain".Length - 1).Trim();
            int symOpIndex = dataLine.IndexOf("Symmetry Operator") + "Symmetry Operator".Length;
            int symOpEndIndex = dataLine.IndexOf("Full Symmetry Operator");
            if (symOpEndIndex < 0)
            {
                symOpEndIndex = dataLine.Length;
            }
            string symOpString = dataLine.Substring(symOpIndex, symOpEndIndex - symOpIndex).Trim();
            string asymChainSymString = chain + "_" + symOpString;
            return asymChainSymString;
        }

        private string[] GetInterfaceChains(string pdbId, int interfaceId)
        {
            string queryString = string.Format("Select * From CrystEntryInterfaces Where PdbID = '{0}' AND InterfaceID = {1};", pdbId, interfaceId);
            DataTable chainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] asymChains = new string[2];
            asymChains[0] = chainInterfaceTable.Rows[0]["AsymChain1"].ToString().TrimEnd() + "_" + chainInterfaceTable.Rows[0]["SymmetryString1"].ToString().TrimEnd();
            asymChains[1] = chainInterfaceTable.Rows[0]["AsymChain2"].ToString().TrimEnd() + "_" + chainInterfaceTable.Rows[0]["SymmetryString2"].ToString().TrimEnd();
            return asymChains;
        }
        public void FindCommonInterface()
        {
            StreamWriter dataWriter = new StreamWriter("SimilarCompInterfaceRows.txt", true);
            int relSeqId = 11619;
            string pdbId1 = "2ac3";
            int domainInterfaceId1 = 5;
            string pdbId2 = "2jam";
            int domainInterfaceId2 = 1;

            string queryString = string.Format("Select * From PfamDomainInterfaceComp " + 
                " Where RelSeqID = {0} AND ((PdbID1 = '{1}' AND DomainInterfaceID1 = {2}) OR " + 
                " (PdbID2 = '{1}' AND DomainInterfaceID2 = {2})) AND Qscore >= 0.2;", relSeqId, pdbId1, domainInterfaceId1);
            DataTable interfaceCompTable1 = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = string.Format("Select * From PfamDomainInterfaceComp " +
                " Where RelSeqID = {0} AND ((PdbID1 = '{1}' AND DomainInterfaceID1 = {2}) OR " +
                " (PdbID2 = '{1}' AND DomainInterfaceID2 = {2})) AND Qscore >= 0.2;", relSeqId, pdbId2, domainInterfaceId2);
            DataTable interfaceCompTable2 = ProtCidSettings.protcidQuery.Query( queryString);

            string compPdbId = "";
            int compDomainInterfaceId = 0;

            foreach (DataRow compRow1 in interfaceCompTable1.Rows )
            {
                compPdbId = compRow1["PdbID1"].ToString();
                compDomainInterfaceId = Convert.ToInt32 (compRow1["DomainInterfaceID1"].ToString ());
                if (compPdbId == pdbId1)
                {
                    compPdbId = compRow1["PdbID2"].ToString();
                    compDomainInterfaceId = Convert.ToInt32 (compRow1["DomainInterfaceID2"].ToString ());
                }
                DataRow compRow2 = GetCompRowInTable(compPdbId, compDomainInterfaceId, interfaceCompTable2);
                if (compRow2 != null)
                {
                    dataWriter.WriteLine(ParseHelper.FormatDataRow (compRow1 ));
                    dataWriter.WriteLine(ParseHelper.FormatDataRow (compRow2 ));
                }
            }
            dataWriter.Close();
        }

        private DataRow GetCompRowInTable(string compPdbId, int compDomainInterfaceId, DataTable interfaceCompTable)
        {
            DataRow[] compRows = interfaceCompTable.Select(string.Format ("PdbID1 = '{0}' AND DomainInterfaceID1 = '{1}'", compPdbId, compDomainInterfaceId));
            if (compRows.Length == 0)
            {
                compRows = interfaceCompTable.Select(string.Format("PdbID2 = '{0}' AND DomainInterfaceID2 = '{1}'", compPdbId, compDomainInterfaceId));
            }
            if (compRows.Length > 0)
            {
                return compRows[0];
            }
            return null;
        }
        /// <summary>
        /// 
        /// </summary>
        public void GetClusterQScores ()
        {
            int relSeqId = 11619;
            int clusterId = 3;
            string queryString = string.Format("Select PdbID, DomainInterfaceID From PfamDomainInterfaceCluster Where RelSeqId= {0} AND ClusterID = {1};", relSeqId, clusterId);
            DataTable repDomainInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);

            queryString = string.Format("Select * From PfamDomainInterfaceComp Where RelSeqID = {0};", relSeqId);
            DataTable interfaceCompTable = ProtCidSettings.protcidQuery.Query( queryString);

            string[] clusterDomainInterfaces = new string[repDomainInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow clusterInterfaceRow in repDomainInterfaceTable.Rows)
            {
                clusterDomainInterfaces[count] = clusterInterfaceRow["PdbID"].ToString() + "_" + clusterInterfaceRow["DomainInterfaceID"].ToString();
                count++;
            }
            StreamWriter dataWriter = new StreamWriter(relSeqId.ToString () + "_" + clusterId.ToString () + "_Qscores.txt");
            string domainInterfaceId1 = "";
            string domainInterfaceId2 = "";
            foreach (DataRow interfaceCompRow in interfaceCompTable.Rows)
            {
                domainInterfaceId1 = interfaceCompRow["PdbID1"].ToString() + "_" + interfaceCompRow["DomainInterfaceID1"].ToString();
                domainInterfaceId2 = interfaceCompRow["PdbID2"].ToString() + "_" + interfaceCompRow["DomainInterfaceID2"].ToString();
                if (clusterDomainInterfaces.Contains(domainInterfaceId1) && clusterDomainInterfaces.Contains(domainInterfaceId2))
                {
                    dataWriter.WriteLine(ParseHelper.FormatDataRow (interfaceCompRow));
                }
            }
            dataWriter.Close();
        }
        #endregion

    }
}
