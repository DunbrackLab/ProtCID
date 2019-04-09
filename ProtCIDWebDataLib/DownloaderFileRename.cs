using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using DbLib;
using System.Data;
using InterfaceClusterLib.AuxFuncs;
using ProtCidSettingsLib;

namespace ProtCIDWebDataLib
{
    public class DownloaderFileRename
    {
        private StreamWriter logWriter = null;
        public DownloaderFileRename ()
        {
            ProtCidSettings.LoadDirSettings();
            if (ProtCidSettings.protcidDbConnection == null)
            {
                ProtCidSettings.protcidDbConnection = new DbConnect();
                ProtCidSettings.protcidDbConnection.ConnectString = "DRIVER=Firebird/InterBase(r) driver;UID=SYSDBA;PWD=fbmonkeyox;DATABASE=" +
                                    ProtCidSettings.dirSettings.protcidDbPath;
                ProtCidSettings.protcidQuery = new DbQuery(ProtCidSettings.protcidDbConnection);
            }
            logWriter = new StreamWriter("GroupNameRenameLog.txt", true);
            logWriter.WriteLine(DateTime.Today.ToLongDateString ());
        }

        /// <summary>
        /// 
        /// </summary>
        public string GenerateChainGroupIdFileNameMatch ()
        {
            string chainGroupIdNameLsFile = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "ChainGroupIdNameMap.txt");
            StreamWriter lsFileWriter = new StreamWriter(chainGroupIdNameLsFile);
            string queryString = "Select Distinct SuperGroupSeqID From PfamSuperGroups;";
            DataTable chainGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
            int chainGroupId = 0;
            string groupName = "";
            foreach (DataRow chainGroupRow in chainGroupTable.Rows)
            {
                chainGroupId = Convert.ToInt32(chainGroupRow["SuperGroupSeqID"].ToString ());
                groupName = DownloadableFileName.GetChainGroupTarGzFileName(chainGroupId);
                lsFileWriter.Write(chainGroupId + "\t" + groupName + "\n");
            }
            lsFileWriter.Close();

            return chainGroupIdNameLsFile;
        }

        /// <summary>
        /// 
        /// </summary>
        public string GenerateDomainGroupIdFileNameMatch()
        {
            string domainGroupIdNameLsFile = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "DomainGroupIdNameMap.txt");
            StreamWriter lsFileWriter = new StreamWriter(domainGroupIdNameLsFile);
            string queryString = "Select Distinct RelSeqID From PfamDomainFamilyRelation;";
            DataTable domainGroupTable = ProtCidSettings.protcidQuery.Query(queryString);
            int relSeqId = 0;
            string groupName = "";
            foreach (DataRow domainGroupRow in domainGroupTable.Rows)
            {
                relSeqId = Convert.ToInt32(domainGroupRow["RelSeqID"].ToString());
                groupName = DownloadableFileName.GetDomainRelationName(relSeqId);
                lsFileWriter.Write(relSeqId + "\t" + groupName + "\n");
            }
            lsFileWriter.Close();
            return domainGroupIdNameLsFile;
        }

        public void RenameGroupFiles ()
        {
 //           string chainGroupIdNameLsFile = GenerateChainGroupIdFileNameMatch();
 //           string domainGroupIdNameLsFile = GenerateDomainGroupIdFileNameMatch();

            string chainGroupIdNameLsFile = @"X:\Qifang\ProjectData\DbProjectData\InterfaceFiles\ChainGroupIdNameMap.txt";
 //           string domainGroupIdNameLsFile = @"X:\Qifang\ProjectData\DbProjectData\InterfaceFiles\DomainGroupIdNameMap.txt";

            string groupDir = @"D:\protcid\UpdateChainClusterInterfaces";
            string newGroupDir = groupDir + "_new";
            if (! Directory.Exists (newGroupDir))
            {
                Directory.CreateDirectory(newGroupDir);
            }

            RenameGroupFiles(groupDir, newGroupDir, chainGroupIdNameLsFile);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupDir"></param>
        /// <param name="newGroupDir"></param>
        /// <param name="groupIdNameMatchFile"></param>
        public void RenameGroupFiles (string groupDir, string newGroupDir, string groupIdNameMatchFile)
        {
            Dictionary<string, string> groupIdNameDict = ReadGroupIdNameDict(groupIdNameMatchFile);

            string[] groupFiles = Directory.GetFiles(groupDir, "*.tar.*");
            if (groupFiles.Length == 0)
            {
                groupFiles = Directory.GetFiles(groupDir, "*.gz");
            }
            string newGroupFileName = "";
            string newGroupFile = "";
            foreach (string groupFile in groupFiles)
            {
                FileInfo fileInfo = new FileInfo(groupFile);
                string[] fileFields = fileInfo.Name.Split("_.".ToCharArray ());
                if (! groupIdNameDict.ContainsKey (fileFields[0]))
                {
                    continue;
                }
                if (fileFields[0].IndexOf ("Seq") > -1)
                {
                    newGroupFileName = "Seq_" + groupIdNameDict[fileFields[0]];
                }
                else
                {
                    newGroupFileName = groupIdNameDict[fileFields[0]];
                }
                newGroupFileName = newGroupFileName + fileInfo.Name.Substring(fileFields[0].Length, fileInfo.Name.Length - fileFields[0].Length);
                newGroupFile = Path.Combine(newGroupDir, newGroupFileName);
                if (!File.Exists(newGroupFile))
                {
                    try
                    {
                        File.Move(groupFile, newGroupFile);
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine (groupFile + "\n" + newGroupFile +  "\n Error: " + ex.Message);
                        logWriter.Flush();
                    }
                }
            }
            logWriter.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupIdNameMatchFile"></param>
        /// <returns></returns>
        private Dictionary<string, string> ReadGroupIdNameDict (string groupIdNameMatchFile)
        {
            Dictionary<string, string> groupIdNameDict = new Dictionary<string, string>();
            StreamReader dataReader = new StreamReader(groupIdNameMatchFile);
            string line = "";
            while ((line = dataReader.ReadLine ()) != null)
            {
                string[] fields = line.Split('\t');
                groupIdNameDict.Add(fields[0], fields[1]);
            }
            dataReader.Close();

            return groupIdNameDict;
        }
    }
}
