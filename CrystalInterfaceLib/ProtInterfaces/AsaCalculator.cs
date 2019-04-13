using System;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Management;
using System.Collections.Generic;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace CrystalInterfaceLib.ProtInterfaces
{
	/// <summary>
	/// Summary description for AsaCalculator.
	/// </summary>
	public class AsaCalculator
	{
        private double[] probeSizes = { 1.4, 1.5, 1.6, 1.3 };

		public AsaCalculator()
		{
		}

		/// <summary>
		/// get the interface surface area by NACCESS
		/// have to compute surface area three times
		/// </summary>
		/// <param name="complexFile"></param>
		/// <param name="chainAFile"></param>
		/// <param name="chainBFile"></param>
		/// <returns></returns>
		public double ComputeInterfaceSurfaceArea (string complexFile, string chainAFile, string chainBFile)
		{
            // change probe size if necessary
            double probeSize = 1.4;
            double complexAsa = ComputeSurfaceArea(complexFile, out probeSize);
            double chainAAsa = ComputeSurfaceArea(chainAFile, probeSize);
            double chainBAsa = ComputeSurfaceArea(chainBFile, probeSize);

			return (chainAAsa + chainBAsa - complexAsa) / 2;
        }

        /// <summary>
        /// get the interface surface area by Surfrace5
        /// have to compute surface area three times
        /// </summary>
        /// <param name="complexFile"></param>
        /// <param name="chainAFile"></param>
        /// <param name="chainBFile"></param>
        /// <returns></returns>
        public double ComputeInterfaceSurfaceAreaBySurfrace (string complexFile, string chainAFile, string chainBFile)
        {
            // change probe size if necessary
            double probeSize = 0;
            double complexAsa = ComputeSurfaceAreaBySurfrace(complexFile, out probeSize);
            double chainAAsa = ComputeSurfaceAreaBySurfrace (chainAFile, probeSize);
            double chainBAsa = ComputeSurfaceAreaBySurfrace (chainBFile, probeSize);

            return (chainAAsa + chainBAsa - complexAsa) / 2;
        }

        #region surfrace 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordFile"></param>
        /// <returns></returns>
        public double ComputeSurfaceAreaBySurfrace (string coordFile, out double usedProbeSize)
        {
            if (!File.Exists(coordFile))
            {
                throw new Exception("Interface file not exist.");
            }

            int radSet = 1;
            int model = 1;
            double asa = -1.0;
            string fileCode = "";
            string resultFile = "result.txt";
            Directory.SetCurrentDirectory(ProtCidSettings.applicationStartPath + "\\tools\\Surfrace");
            FileInfo fileInfo = new FileInfo (coordFile);
            File.Copy(coordFile, fileInfo.Name, true);
            usedProbeSize = 1.4;
            try
            {
                foreach (double probeSize in probeSizes)
                {
                    RunSurfrace(fileInfo.Name, probeSize, radSet, model);
                    FileInfo resFileInfo = new FileInfo(resultFile);
                    if (resFileInfo.Length > 0)
                    {
                        usedProbeSize = probeSize;
                        break;
                    }
                }

                asa = ParseSurfraceResultFile(resultFile);
            }
            catch (Exception ex)
            {
                throw new Exception(coordFile + " surface area error: " + ex.Message);
            }
            finally
            {
                int extensionIndex = fileInfo.Name.LastIndexOf(".");
                fileCode = fileInfo.Name.Substring(0, extensionIndex);
                string extensionName = fileInfo.Name.Substring(extensionIndex + 1,
                    fileInfo.Name.Length - extensionIndex - 1);
                string newExtensionName = extensionName.Replace(extensionName.Substring(0, 3), "txt");
                try
                {
                    File.Delete(fileCode + "." + newExtensionName);
                    File.Delete(fileCode + "_residue.txt");
                    File.Delete(fileInfo.Name);
                    File.Delete(coordFile);
                    File.Delete(resultFile);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                }
                catch { }
            }
            return asa;
        }
     
        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordFile"></param>
        /// <returns></returns>
        public double ComputeSurfaceAreaBySurfrace(string coordFile, double probeSize)
        {
            if (!File.Exists(coordFile))
            {
                throw new Exception("Interface file not exist.");
            }

      //      double probeSize = 1.40;
            int radSet = 1;
            int model = 1;
            double asa = -1.0;
            string fileCode = "";
            string resultFile = "result.txt";
            Directory.SetCurrentDirectory(ProtCidSettings.applicationStartPath + "\\tools\\Surfrace");
            FileInfo fileInfo = new FileInfo(coordFile);
            File.Copy(coordFile, fileInfo.Name, true);
            try
            {
                RunSurfrace(fileInfo.Name, probeSize, radSet, model);
                asa = ParseSurfraceResultFile(resultFile);
            }
            catch (Exception ex)
            {
                throw new Exception(coordFile + " surface area error: " + ex.Message);
            }
            finally
            {
                int extensionIndex = fileInfo.Name.LastIndexOf(".");
                fileCode = fileInfo.Name.Substring(0, extensionIndex);
                string extensionName = fileInfo.Name.Substring(extensionIndex + 1,
                    fileInfo.Name.Length - extensionIndex - 1);
                string newExtensionName = extensionName.Replace(extensionName.Substring(0, 3), "txt");
                try
                {
                    File.Delete(fileCode + "." + newExtensionName);
                    File.Delete(fileCode + "_residue.txt");
                    File.Delete(fileInfo.Name);
                    File.Delete(coordFile);
                    File.Delete(resultFile);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                }
                catch { }
            }
            return asa;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordFile"></param>
        /// <returns></returns>
        public double ComputeSurfaceAreaBySurfrace(string coordFile)
        {
            if (!File.Exists(coordFile))
            {
                throw new Exception("Interface file not exist.");
            }

            int radSet = 1;
            int model = 1;
            double asa = -1.0;
            string fileCode = "";
            string resultFile = "result.txt";
            Directory.SetCurrentDirectory(ProtCidSettings.applicationStartPath + "\\tools\\Surfrace");
            FileInfo fileInfo = new FileInfo(coordFile);
            File.Copy(coordFile, fileInfo.Name, true);
            try
            {
                foreach (double probeSize in probeSizes)
                {
                    RunSurfrace(fileInfo.Name, probeSize, radSet, model);
                    FileInfo resFileInfo = new FileInfo(resultFile);
                    if (resFileInfo.Length > 0)
                    {
                        break;
                    }
                }
                asa = ParseSurfraceResultFile(resultFile);
            }
            catch (Exception ex)
            {
                throw new Exception(coordFile + " surface area error: " + ex.Message);
            }
            finally
            {
                int extensionIndex = fileInfo.Name.LastIndexOf(".");
                fileCode = fileInfo.Name.Substring(0, extensionIndex);
                string extensionName = fileInfo.Name.Substring(extensionIndex + 1,
                    fileInfo.Name.Length - extensionIndex - 1);
                string newExtensionName = extensionName.Replace(extensionName.Substring(0, 3), "txt");
                try
                {
                    File.Delete(fileCode + "." + newExtensionName);
                    File.Delete(fileCode + "_residue.txt");
                    File.Delete(fileInfo.Name);
                    File.Delete(coordFile);
                    File.Delete(resultFile);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                }
                catch { }
            }
            return asa;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordFile"></param>
        /// <param name="probeSize"></param>
        /// <param name="radSet"></param>
        /// <param name="modelNo"></param>
        private void RunSurfrace(string coordFile, double probeSize, int radSet, int modelNo)
        {
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo();
                Process surfraceProcess = null;

                // set properties for the process
                string commandParam = "/C surfrace5_0cmd.exe " + radSet + " " + coordFile + " " + probeSize + " " + modelNo;
    //            string commandParam = radSet + " " + coordFile + " " + probeSize + " " + modelNo;
                processInfo.CreateNoWindow = true;
                processInfo.UseShellExecute = false;
                processInfo.FileName = "CMD.exe";
      //          processInfo.FileName = "surfrace5_0cmd.exe";
                processInfo.Arguments = commandParam;
                surfraceProcess = Process.Start(processInfo);
                if (!surfraceProcess.WaitForExit(60000)) // 60 seconds
                {
                    KillProcessAndChildren(surfraceProcess.Id);
                    throw new Exception("Surfrace process is killed due running time > 60s");
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pid"></param>
        public void KillProcessAndChildren(int pid)
        {
            using (var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid))
            {
                var moc = searcher.Get();
                foreach (ManagementObject mo in moc)
                {
                    KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
                }
                try
                {
                    var proc = Process.GetProcessById(pid);
                    proc.Kill();
                }
                catch (Exception e)
                {
                    // Process already exited.
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resultFile"></param>
        /// <returns></returns>
        private double ParseSurfraceResultFile (string resultFile)
        {
            double asa = -1;
            int count = 0;
            while (IsFileLocked(resultFile) && count <= 100)
            {
                Thread.Sleep(1000);
                count++;
            }
            using (StreamReader dataReader = new StreamReader(resultFile))
            {
                string line = "";
                while ((line = dataReader.ReadLine()) != null)
                {
                    if (line.IndexOf("TOTAL ASA") > -1)
                    {
                        string[] fields = ParseHelper.SplitPlus(line, ' ');
                        int equalIndex = fields[1].IndexOf("=");
                        asa = Convert.ToDouble(fields[1].Substring(equalIndex + 1, fields[1].Length - equalIndex - 1));
                    }
                }
            }
            return asa;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private bool IsFileLocked(string resultFile)
        {
            FileStream stream = null;

            try
            {
                stream = File.Open(resultFile, FileMode.Open);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
            }

            //file is not locked
            return false;
        }
        #endregion

        #region NACCESS
        /// <summary>
        /// compute the surface area of a coordinates file by NACCESS
        /// </summary>
        /// <param name="coordFile"></param>
        /// <returns></returns>
        public double ComputeSurfaceArea(string coordFile, out double usedProbeSize)
        {
            if (!File.Exists(coordFile))
            {
                throw new Exception("Interface file not exist.");
            }

            double asa = -1.0;
            FileInfo fileInfo = new FileInfo(coordFile);
            string fullPathCoordFile = Path.Combine(fileInfo.DirectoryName, fileInfo.Name);
            string fileCode = fullPathCoordFile.Substring(0, fullPathCoordFile.LastIndexOf("."));

            string currentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(ProtCidSettings.applicationStartPath + "\\tools");
            usedProbeSize = 1.4;
            foreach (double probeSize in probeSizes)
            {
                FormatParamFile("accall.input", fullPathCoordFile, probeSize);
                
                RunNaccess();
                asa = ParseRsaFile(fileCode + ".rsa");
                if (asa > 0)
                {
                    usedProbeSize = probeSize;
                    break;
                }
            }
            File.Delete(fileCode + ".rsa");
            File.Delete(fileCode + ".asa");
            File.Delete(fileCode + ".log");
            File.Delete(coordFile);
            Directory.SetCurrentDirectory(currentDirectory);
            return asa;
        }

        /// <summary>
        /// compute the surface area of a coordinates file by NACCESS
        /// </summary>
        /// <param name="coordFile"></param>
        /// <returns></returns>
        public double ComputeSurfaceArea(string coordFile, double probeSize)
        {
            if (!File.Exists(coordFile))
            {
                throw new Exception("Interface file not exist.");
            }

            double asa = -1.0;
            FileInfo fileInfo = new FileInfo(coordFile);
            string fullPathCoordFile = Path.Combine(fileInfo.DirectoryName, fileInfo.Name);
            string fileCode = fullPathCoordFile.Substring(0, fullPathCoordFile.LastIndexOf("."));

            string currentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(ProtCidSettings.applicationStartPath + "\\tools");

            FormatParamFile("accall.input", fullPathCoordFile, probeSize);
            RunNaccess();
            
            asa = ParseRsaFile(fileCode + ".rsa");

            File.Delete(fileCode + ".rsa");
            File.Delete(fileCode + ".asa");
            File.Delete(fileCode + ".log");
            File.Delete(coordFile);
            Directory.SetCurrentDirectory(currentDirectory);
            return asa;
        }

        /// <summary>
        /// compute the surface area for the input file
        /// keep the output file from NACCESS
        /// <param name="coordFile"></param>
        /// <param name="destSaFileDir"></param>
        /// <returns></returns>
        public void ComputeSurfacAreaFiles (string coordFile, string destSaFileDir)
        {
            if (!File.Exists(coordFile))
            {
                throw new Exception("Interface file not exist.");
            }

            string currentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(ProtCidSettings.applicationStartPath + "\\tools");

            FormatParamFile("accall.input", coordFile);
            RunNaccess();

            // move the output files to the specified folder
            string fileNameWithPath = coordFile.Substring(0, coordFile.LastIndexOf("."));
            FileInfo fileInfo = new FileInfo (coordFile);
            string fileNameNoExetension  = fileInfo.Name.Substring (0, fileInfo.Name.IndexOf ("."));
            string destFileName = "";
            destFileName = Path.Combine(destSaFileDir, fileNameNoExetension + ".rsa");
            if (File.Exists (destFileName))
            {
                File.Delete (destFileName);
            }
            File.Move(fileNameWithPath + ".rsa", destFileName);
            destFileName =  Path.Combine(destSaFileDir, fileNameNoExetension + ".asa");
            if (File.Exists (destFileName))
            {
                File.Delete (destFileName);
            }
            File.Move(fileNameWithPath + ".asa", destFileName);
            destFileName = Path.Combine(destSaFileDir, fileNameNoExetension + ".log");
            if (File.Exists (destFileName))
            {
                File.Delete (destFileName );
            }
            File.Move(fileNameWithPath + ".log", destFileName);

            Directory.SetCurrentDirectory(currentDirectory);
        }

        /// <summary>
        /// compute the surface area for the input file
        /// keep the output file from NACCESS
        /// <param name="coordFile"></param>
        /// <param name="destSaFileDir"></param>
        /// <returns></returns>
        public string[] ComputeSurfacAreaFiles(string coordFile)
        {
            if (!File.Exists(coordFile))
            {
                throw new Exception("Interface file not exist.");
            }

            FileInfo fileInfo = new FileInfo(coordFile);
            string fullPathCoordFile = Path.Combine(fileInfo.DirectoryName, fileInfo.Name);
            string fileNameWithPath = fullPathCoordFile.Substring(0, fullPathCoordFile.LastIndexOf("."));

            string currentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(ProtCidSettings.applicationStartPath + "\\tools");

            FormatParamFile("accall.input", coordFile);
            RunNaccess();

            string[] outputFiles = new string[3];
            outputFiles[0] = fileNameWithPath + ".rsa";
            outputFiles[1] = fileNameWithPath + ".asa";
            outputFiles[2] = fileNameWithPath + ".log";

            Directory.SetCurrentDirectory(currentDirectory);
            return outputFiles;
        }

        /// <summary>
        /// compute the surface area of a coordinates file by NACCESS
        /// </summary>
        /// <param name="coordFile"></param>
        /// <returns></returns>
        public double ComputeSurfaceArea(string coordFile)
        {
            if (!File.Exists(coordFile))
            {
                throw new Exception("Interface file not exist.");
            }
            double asa = -1.0;

            FileInfo fileInfo = new FileInfo(coordFile);
            string fullPathCoordFile = Path.Combine(fileInfo.DirectoryName, fileInfo.Name);
            string fileCode = fullPathCoordFile.Substring(0, fullPathCoordFile.LastIndexOf("."));

            string currentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(ProtCidSettings.applicationStartPath + "\\tools");
            foreach (double probeSize in probeSizes)
            {
                FormatParamFile("accall.input", fullPathCoordFile, probeSize);

                RunNaccess();

                asa = ParseRsaFile(fileCode + ".rsa");
                if (asa > 0)
                {                   
                    break;
                }
            }          
            try
            {
                File.Delete(fileCode + ".rsa");
                File.Delete(fileCode + ".asa");
                File.Delete(fileCode + ".log");
                File.Delete(coordFile);
            }
            catch { }
            Directory.SetCurrentDirectory(currentDirectory);

            return asa;
        }

		/// <summary>
		/// format the parameter input file by changing the PDB file name
		/// </summary>
		/// <param name="interfaceFile"></param>
		private void FormatParamFile (string paramFile, string interfaceFile, double probeSize)
		{
			StreamReader reader = new StreamReader (paramFile);
            string line = "";
            string dataLine = "";
            while ((line = reader.ReadLine()) != null)
            {
                if (line.IndexOf("PDBFILE") > -1)
                {
                    dataLine += ("PDBFILE " + interfaceFile + "\r\n");
                    continue;
                }
                if (line.IndexOf("PROBE") > -1)
                {
                    dataLine += ("PROBE " + probeSize.ToString () + "\r\n");
                    continue;
                }
                dataLine += (line + "\r\n");
            }
            reader.Close();

            dataLine = dataLine.TrimEnd("\r\n".ToCharArray ());

			StreamWriter writer = new StreamWriter (paramFile, false);
			writer.Write (dataLine);
			writer.Close ();
		}

        /// <summary>
        /// format the parameter input file by changing the PDB file name
        /// </summary>
        /// <param name="interfaceFile"></param>
        private void FormatParamFile(string paramFile, string interfaceFile)
        {
            StreamReader reader = new StreamReader(paramFile);
            string line = "";
            string dataLine = "";
            while ((line = reader.ReadLine()) != null)
            {
                if (line.IndexOf("PDBFILE") > -1)
                {
                    dataLine += ("PDBFILE " + interfaceFile + "\r\n");
                    continue;
                }
                dataLine += (line + "\r\n");
            }
            reader.Close();

            dataLine = dataLine.TrimEnd("\r\n".ToCharArray());

            StreamWriter writer = new StreamWriter(paramFile, false);
            writer.Write(dataLine);
            writer.Close();
        }

		/// <summary>
		/// Run naccess to get the output files
		/// </summary>
		private void RunNaccess ()
		{
			try
			{
				ProcessStartInfo processInfo = new ProcessStartInfo();
				Process naccessProcess = null;	
				
				// set properties for the process
				string commandParam = "/C naccess.exe < accall.input";
             //   string commandParam = "/C C: && cd " + ProtCidSettings.applicationStartPath + "\\tools" + 
             //       " && naccess.exe < accall.input";
				processInfo.CreateNoWindow = true;
				processInfo.UseShellExecute = false;
				processInfo.FileName = "CMD.exe";
            //    processInfo.FileName = "naccess.exe";
				processInfo.Arguments = commandParam;
				naccessProcess = Process.Start( processInfo );
				naccessProcess.WaitForExit ();
			}
			catch (Exception e)
			{
				throw e;
			}
		}
		
		/// <summary>
		/// read the total surface area for the interface
		/// </summary>
		/// <param name="rsaFile"></param>
		/// <returns></returns>
		private double ParseRsaFile (string rsaFile)
		{
			double asa = -1.0;
			
			StreamReader fileReader = new StreamReader (rsaFile);
			string line = "";
			while ((line = fileReader.ReadLine ()) != null)
			{
				if (line.IndexOf ("TOTAL") > -1)
				{
					string[] fields = ParseHelper.SplitPlus (line, ' ');
					asa = Convert.ToDouble (fields[1]);
					break;
				}	
			}
			fileReader.Close ();
			return asa;
        }
        #endregion
    }
}
