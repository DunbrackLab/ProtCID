using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using AuxFuncLib;
using ProtCidSettingsLib;

namespace InterfaceClusterLib.AuxFuncs
{
    public class FileCompress
    {
        public CmdOperations linuxOperation = new CmdOperations();
        public int maxNumOfFiles = 300;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="clusterId"></param>
        /// <param name="interfaceFiles"></param>
        public string RunTar(int groupId, int clusterId, string[] interfaceFiles, string clusterFileDir)
        {
            string clusterFile = groupId.ToString() + "_" + clusterId.ToString() + ".tar.gz";

            try
            {
                RunTar(clusterFile, interfaceFiles, clusterFileDir, true);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                clusterFile = "";
            }
            return clusterFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="clusterFiles"></param>
        public string RunTar(int groupId, string[] clusterFiles, string clusterFileDir)
        {
            string groupFile = groupId.ToString() + ".tar";
            try
            {
                RunTar(groupFile, clusterFiles, clusterFileDir, false);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                groupFile = "";
            }
            return groupFile;
        }

        /* to tar big groups with many files. 
         directly tar on the folder
         since the length of parameters of Cmd.exe cannot be longer than 8191
         * */
        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqFastaFiles"></param>
        /// <param name="srcFolder"></param>
        /// <param name="groupId"></param>
        /// <returns></returns>
        public void MoveFilesToGroupFolder(string[] filesToBeCompressed, string srcFolder, string destFolder)
        {
            if (Directory.Exists(destFolder))
            {
                Directory.Delete(destFolder, true);
            }
            Directory.CreateDirectory(destFolder);

            string srcFile = "";
            string destFile = "";
            foreach (string fileName in filesToBeCompressed)
            {
                srcFile = Path.Combine(srcFolder, fileName);
                destFile = Path.Combine(destFolder, fileName);
                if (File.Exists(srcFile))
                {
                    if (srcFile.IndexOf(".tar.gz") > -1) // keep the original tar file
                    {
                        File.Copy(srcFile, destFile, true);
                    }
                    else
                    {
                        File.Move(srcFile, destFile);
                    }
                }
            }
        }

        /// </summary>
        /// <param name="tarFileName">the file for tar with no path info</param>
        /// <param name="filesToBeCompressed">the file names to be tar, with no path info either</param>
        /// <param name="workingDir">the directory where the files to be compressed</param>
        /// <param name="toBeCompressed"> be compresse or not</param>
        /// <returns></returns>
        public string RunTar(string tarFileName, string[] filesToBeCompressed, string workingDir, bool toBeCompressed)
        {
            if (filesToBeCompressed.Length > maxNumOfFiles)
            {
                string tarRootName = tarFileName.Substring(0, tarFileName.IndexOf(".tar"));
                string tarFolder = Path.Combine(workingDir, tarRootName);
                try
                {
                    MoveFilesToGroupFolder(filesToBeCompressed, workingDir, tarFolder);
                    linuxOperation.RunTarOnFolder(tarFileName, tarRootName, workingDir, toBeCompressed);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                    tarFileName = "";
                }
            }
            else
            {
                try
                {
                    linuxOperation.RunTar(tarFileName, filesToBeCompressed, workingDir, toBeCompressed);
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(ex.Message);
                    tarFileName = "";
                }
            }
            return tarFileName;
        }
    }
}
