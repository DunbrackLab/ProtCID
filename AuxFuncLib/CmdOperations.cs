using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace AuxFuncLib
{
    public class CmdOperations
    {
        #region member variables
        private string toolPath = "tools";
        private string linuxMachine = "10.132.8.198";  // original 10.40.16.33 fourpaws
        private string linuxUserName = "qifang";
        private string linuxUserPsw = "qifang00";
        private string keypairFile = @"D:\kits\PuTTY\Abs2_keypair.ppk";
        private string tarToolPath = @"tools\bsdtar.exe";
        private string pymolExeFile = @"C:\Program Files\PyMOL\PyMOL\PyMol.exe";
        #endregion 

        #region properties
        /// <summary>
        /// 
        /// </summary>
        public string ToolPath
        {
            get
            {
                return toolPath;
            }
            set
            {
                toolPath = value;
            }
        }

        // the public-private key pair file for connecting to linux machines
        // the public key of the machine must be copied into the related linux machines
        public string PPK
        {
            get
            {
                return keypairFile;
            }
            set
            {
                keypairFile = value;
            }
        }
        /// <summary>
        /// The linux machine: either IP address or machine name
        /// </summary>
        public string LinuxMachine
        {
            get
            {
                return linuxMachine;
            }
            set
            {
                linuxMachine = value;
            }
        }

        /// <summary>
        /// the user name for linux machine
        /// </summary>
        public string LinuxUserName
        {
            get
            {
                return linuxUserName;
            }
            set
            {
                linuxUserName = value;
            }
        }

        /// <summary>
        /// the password
        /// </summary>
        public string LinuxUserPsw
        {
            get
            {
                return linuxUserPsw;
            }
            set
            {
                linuxUserPsw = value;
            }
        }

        /// <summary>
        /// the path to tar
        /// </summary>
        public string TarToolPath
        {
            get
            {
                return tarToolPath;
            }
            set
            {
                tarToolPath = value;
            }
        }

        /// <summary>
        /// path to pymol executable file
        /// </summary>
        public string PymolExeFile
        {
            get
            {
                return pymolExeFile;
            }
            set
            {
                pymolExeFile = value;
            }
        }
        #endregion

        #region run linux programs
        /// <summary>
        /// 
        /// </summary>
        /// <param name="linuxProgram"></param>
        public void RunPlink(string cmdFile)
        {
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo();
                Process plinkProcess = null;

                // set properties for the process
                string commandParam = "-pw " + linuxUserPsw + " " +
                    linuxUserName + "@" + linuxMachine + " -ssh -batch -m " + cmdFile;
                processInfo.CreateNoWindow = true;
                processInfo.UseShellExecute = false;
                processInfo.FileName = Path.Combine(toolPath, "plink.exe");
                processInfo.Arguments = commandParam;
                plinkProcess = Process.Start(processInfo);
                plinkProcess.WaitForExit();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="linuxProgram"></param>
        public void RunPlinkByPpk(string cmdFile)
        {
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo();
                Process plinkProcess = null;

                // set properties for the process
                string commandParam = "-i " + keypairFile  + " " +
                    linuxUserName + "@" + linuxMachine + " -ssh -batch -m " + cmdFile;
                processInfo.CreateNoWindow = true;
                processInfo.UseShellExecute = false;
                processInfo.FileName = Path.Combine(toolPath, "plink.exe");
                processInfo.Arguments = commandParam;
                plinkProcess = Process.Start(processInfo);
                plinkProcess.WaitForExit();
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        #endregion

        #region copy files -- PSCP
        /// <summary>
        /// copy windows data to linux machine
        /// </summary>
        /// <param name="windownsFile"></param>
        /// <param name="linuxFile"></param>
        public void CopyWindowsDataToLinux(string windownsFile, string linuxFile)
        {
            string commandParam = "-pw " + linuxUserPsw + " \"" + windownsFile + "\" " +
                linuxUserName + "@" + linuxMachine + ":" + linuxFile;
            RunPscp(commandParam);
        }

        /// <summary>
        /// copy data files from linux machine to windows
        /// </summary>
        /// <param name="linuxFile"></param>
        /// <param name="windowsFile"></param>
        public void CopyLinuxDataToWindows(string linuxFile, string windowsFile)
        {
            string commandParam = "-pw " + linuxUserPsw + " " + linuxUserName + "@" + linuxMachine + ":" + linuxFile +
                " \"" + windowsFile + "\"";
            RunPscp(commandParam);
        }

        /// <summary>
        /// copy windows data to linux machine
        /// </summary>
        /// <param name="windownsFile"></param>
        /// <param name="linuxFile"></param>
        public void CopyWindowsDataToLinuxByPpk (string windownsFile, string linuxFile)
        {
            string commandParam = "-i " + keypairFile + " \"" + windownsFile + "\" " +
                linuxUserName + "@" + linuxMachine + ":" + linuxFile;
            RunPscp(commandParam);
        }

        /// <summary>
        /// copy data files from linux machine to windows
        /// </summary>
        /// <param name="linuxFile"></param>
        /// <param name="windowsFile"></param>
        public void CopyLinuxDataToWindowsByPpk (string linuxFile, string windowsFile)
        {
            string commandParam = "-i " + keypairFile + " " + linuxUserName + "@" + linuxMachine + ":" + linuxFile +
                " \"" + windowsFile + "\"";
            RunPscp(commandParam);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pscpPath"></param>
        /// <param name="commandParam"></param>
        private void RunPscp(string commandParam)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo();
            Process pscpProcess = null;

            try
            {
                processInfo.CreateNoWindow = true;
                processInfo.UseShellExecute = false;
                processInfo.FileName = Path.Combine (toolPath, "pscp.exe");
                processInfo.Arguments = commandParam;
                pscpProcess = Process.Start(processInfo);
                pscpProcess.WaitForExit();
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        #endregion

        #region tar
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tarFile"></param>
        /// <param name="srcFiles"></param>
        public void RunTar(string tarFile, string[] srcFiles, string workingDir, bool toBeCompress)
        {
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo();
                Process tarProcess = null;

                string currentDirectory = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(workingDir);
                string commandParam = "";
                // set properties for the process
                // change the directory to the current 
      /*          string driverName = GetWorkingDriver (workingDir);
                // change the current disk driver to working driver
                string commandParam = "/C" + driverName + ": && cd " + workingDir + " && ";
                commandParam += tarToolPath;*/
                if (toBeCompress)
                {
                    commandParam += " -czf " + tarFile;
                }
                else
                {
                    commandParam += " -cf " + tarFile;
                }
                foreach (string srcFile in srcFiles)
                {
                    commandParam += (" " + srcFile);
                }
                processInfo.CreateNoWindow = true;
                processInfo.UseShellExecute = false;
         //       processInfo.FileName = "CMD.exe";
                processInfo.FileName = Path.Combine (currentDirectory, tarToolPath);
                processInfo.Arguments = commandParam;
                tarProcess = Process.Start(processInfo);
                tarProcess.WaitForExit();

                Directory.SetCurrentDirectory(currentDirectory);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Compress and tar a folder
        /// </summary>
        /// <param name="tarFile">the tar file name with no path info</param>
        /// <param name="fileFolder">the folder to be compressed with no path info</param>
        /// <param name="workingDir">where all files stored</param>
        /// <param name="toBeCompress">to be gzipped or not</param>
        public void RunTarOnFolder (string tarFile, string fileFolder, string workingDir, bool toBeCompress)
        {
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo();
                Process tarProcess = null;

                // change the directory to the current 
                string currentDirectory = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(workingDir);

                string commandParam = "";

                // set properties for the process
             /*   
                string driverName = GetWorkingDriver(workingDir);
                // change the current disk driver to working driver
                string commandParam = "/C" + driverName + ": && cd " + workingDir + " && ";
                commandParam += tarToolPath;*/
                if (toBeCompress)
                {
                    commandParam += " -czf " + tarFile;
                }
                else
                {
                    commandParam += " -cf " + tarFile;
                }
                commandParam += (" " + fileFolder);
                processInfo.CreateNoWindow = true;
                processInfo.UseShellExecute = false;
        //        processInfo.FileName = "CMD.exe";
                processInfo.FileName = Path.Combine(currentDirectory, tarToolPath);
                processInfo.Arguments = commandParam;
                tarProcess = Process.Start(processInfo);
                tarProcess.WaitForExit();

                Directory.SetCurrentDirectory(currentDirectory);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="workingDir"></param>
        /// <returns></returns>
        private string GetWorkingDriver (string workingDir)
        {
            int driverIndex = workingDir.IndexOf (":\\");
            string driverName = "";
            if ( driverIndex > -1)
            {
                driverName = workingDir.Substring (0, driverIndex);
            }
            return driverName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tarFile"></param>
        /// <param name="workingDir"></param>
        public void UnTar(string tarFile, string workingDir)
        {
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo();
                Process tarProcess = null;

                // change the directory to the current 
                string currentDirectory = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(workingDir);
                string commandParam = "";

                // set properties for the process
                // change the directory to the current 
       //         string driverName = GetWorkingDriver(workingDir);
                // change the current disk driver to working driver
      //          string commandParam = "/C" + driverName + ": && cd " + workingDir + " && ";
        //        commandParam += tarToolPath;
                commandParam += " -xf " + tarFile;
                
                processInfo.CreateNoWindow = true;
                processInfo.UseShellExecute = false;
          //      processInfo.FileName = "CMD.exe";
                processInfo.FileName = Path.Combine(currentDirectory, tarToolPath);
                processInfo.Arguments = commandParam;
                tarProcess = Process.Start(processInfo);
                tarProcess.WaitForExit();

                Directory.SetCurrentDirectory(currentDirectory);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tarFile"></param>
        /// <param name="workingDir"></param>
        public void UnTarToFolder (string tarFile, string workingDir, string destFolder)
        {
            try
            {
                if (! Directory.Exists(destFolder))
                {
                    Directory.CreateDirectory(destFolder);
                }
                ProcessStartInfo processInfo = new ProcessStartInfo();
                Process tarProcess = null;

                // change the directory to the current 
                string currentDirectory = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(workingDir);
                string commandParam = "";

                // set properties for the process
                // change the directory to the current 
     //           string driverName = GetWorkingDriver(workingDir);
                // change the current disk driver to working driver
       //         string commandParam = "/C" + driverName + ": && cd " + workingDir + " && ";
      //          commandParam += tarToolPath;
                commandParam += " -xf " + tarFile + " -C " + destFolder;

                processInfo.CreateNoWindow = true;
                processInfo.UseShellExecute = false;
   //             processInfo.FileName = "CMD.exe";
                processInfo.FileName = Path.Combine(currentDirectory, tarToolPath);
                processInfo.Arguments = commandParam;
                tarProcess = Process.Start(processInfo);
                tarProcess.WaitForExit();

                Directory.SetCurrentDirectory(currentDirectory);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        #endregion

        #region run perl script
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tarFile"></param>
        /// <param name="workingDir"></param>
        public void RunProtCidPerlScript(string workingDir, string outputFile, string protcidScriptFile)
        {
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo();
                Process perlProcess = null;

                // change the directory to the current 
                string currentDirectory = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(workingDir);
                string commandParam = "";

                // set properties for the process
                // change the directory to the current 
   //             string driverName = GetWorkingDriver(workingDir);
                // change the current disk driver to working driver
                commandParam = protcidScriptFile + " > " + outputFile;
                processInfo.CreateNoWindow = true;
                processInfo.UseShellExecute = false;
                processInfo.FileName = "CMD.exe";
                processInfo.Arguments = commandParam;
                perlProcess = Process.Start(processInfo);
                perlProcess.WaitForExit();

                Directory.SetCurrentDirectory(currentDirectory);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        #endregion

        #region run pymol
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pymolScriptFile"></param>
        public void RunPymol(string pymolScriptFile)
        {
            FileInfo fileInfo = new FileInfo(pymolScriptFile);

            string workingDir = fileInfo.DirectoryName;
            string driverName = GetWorkingDriver(workingDir);

            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo();
                Process cmdProcess = null;

                string currentDirectory = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(workingDir);
                string commandParam = "";

                // set properties for the process
                //    string commandParam = "\"" + pymolExeFile + "\"  " + pymolScriptFile;
      //          string commandParam = "/C" + driverName + ": && cd " + workingDir + " && ";
      //          commandParam += "\"" + pymolExeFile + "\"";
                commandParam += " -c " + pymolScriptFile + " &";
                //      string commandParam = pymolScriptFile;
                //    processInfo.CreateNoWindow = true;
                //    processInfo.UseShellExecute = false;
                //    processInfo.FileName = "CMD.exe";
                //    processInfo.FileName = pymolExeFile;
           //     processInfo.FileName = "CMD.exe";
                processInfo.FileName = "\"" + pymolExeFile + "\"";
                processInfo.Arguments = commandParam;
                processInfo.UseShellExecute = false;
                processInfo.CreateNoWindow = true;
                cmdProcess = Process.Start(processInfo);
                cmdProcess.WaitForExit();

                Directory.SetCurrentDirectory(currentDirectory);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        #endregion

        #region create directory in linux
        /// <summary>
        /// 
        /// </summary>
        /// <param name="directory"></param>
        public void CreateDirectoryInLinux(string directory)
        {
            string cmdFile = "createDirCmd.txt";
            StreamWriter dirCmdWriter = new StreamWriter(cmdFile);
            dirCmdWriter.WriteLine("mkdir " + directory);
            dirCmdWriter.Close();
            RunPlink(cmdFile);
        }
        #endregion
    }
}
