using System;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Collections;
using XtalLib.Settings;
using AuxFuncLib;

namespace XtalLib.ProtInterfaces
{
	/// <summary>
	/// Summary description for AsaCalculator.
	/// </summary>
	public class AsaCalculator
	{
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
		/*	double complexAsa = ComputeInterfaceSurfaceArea (complexFile);
            // use the same probe size for each chain if the probe size changed for the complex
			double chainAAsa = ComputeChainSurfaceArea (chainAFile);
			double chainBAsa = ComputeChainSurfaceArea (chainBFile);*/

            double complexAsa = ComputeSurfaceArea(complexFile);
            double chainAAsa = ComputeSurfaceArea(chainAFile);
            double chainBAsa = ComputeSurfaceArea(chainBFile);

			return (chainAAsa + chainBAsa - complexAsa) / 2;
        }

        #region surfrace 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordFile"></param>
        /// <returns></returns>
        public double ComputeSurfaceAreaBySurfrace (string coordFile)
        {
            if (!File.Exists(coordFile))
            {
                throw new Exception("Interface file not exist.");
            }

            double probeSize = 1.40;
            int radSet = 1;
            int model = 1;
            double asa = -1.0;
            string fileCode = "";
            string resultFile = "result.txt";
            Directory.SetCurrentDirectory(AppSettings.applicationStartPath + "\\tools");
            FileInfo fileInfo = new FileInfo (coordFile);
            File.Copy(coordFile, fileInfo.Name);

            RunSurfrace(fileInfo.Name, probeSize, radSet, model);

            asa = ParseSurfraceResultFile(resultFile);

            int extensionIndex = fileInfo.Name.LastIndexOf(".");
            fileCode = fileInfo.Name.Substring(0, extensionIndex);
            string extensionName = fileInfo.Name.Substring(extensionIndex + 1, 
                fileInfo.Name.Length - extensionIndex - 1);
            string newExtensionName = extensionName.Replace(extensionName.Substring (0, 3), "txt");
            File.Delete(fileCode + "." + newExtensionName);
            File.Delete(fileCode + "_residue.txt");
            File.Delete(resultFile);
            File.Delete(fileInfo.Name);
            File.Delete(coordFile);
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
                string commandParam = "/C surfrace.exe " + radSet + " " + coordFile + " " + probeSize + " " + modelNo;
                processInfo.CreateNoWindow = true;
                processInfo.UseShellExecute = false;
                processInfo.FileName = "CMD.exe";
                processInfo.Arguments = commandParam;
                surfraceProcess = Process.Start(processInfo);
                surfraceProcess.WaitForExit();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resultFile"></param>
        /// <returns></returns>
        private double ParseSurfraceResultFile (string resultFile)
        {
            StreamReader dataReader = new StreamReader(resultFile);
            string line = "";
            double asa = -1;
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.IndexOf("TOTAL ASA") > -1)
                {
                    string[] fields = ParseHelper.SplitPlus(line, ' ');
                    int equalIndex = fields[1].IndexOf("=");
                    asa = Convert.ToDouble (fields[1].Substring(equalIndex + 1, fields[1].Length - equalIndex - 1));
                }
            }
            dataReader.Close();
            return asa;
        }
        #endregion


        #region NACCESS
        /// <summary>
        /// compute the surface area of a coordinates file by NACCESS
        /// </summary>
        /// <param name="coordFile"></param>
        /// <returns></returns>
  /*      public double ComputeInterfaceSurfaceArea(string coordFile)
        {
            if (!File.Exists(coordFile))
            {
                throw new Exception("Interface file not exist.");
            }
            /*     double probeSize = 1.40;
                 int sizeCount = 0;
                 double stepSize = 0.01;
                 double maxProbeSize = 2.0;
            double asa = -1.0;
            string fileCode = "";
            Directory.SetCurrentDirectory(AppSettings.applicationStartPath + "\\tools");
           */ 
           /*     while (asa < 0)
                  {
            probeSize = 1.40 + (double)sizeCount * stepSize;
            if (probeSize > maxProbeSize)
            {
                break;
            }
            sizeCount++;
            */
     /*       FormatParamFile("accall.input", coordFile);
            RunNaccess();
            fileCode = coordFile.Substring(0, coordFile.LastIndexOf("."));
            asa = ParseRsaFile(fileCode + ".rsa");
            //    }
            File.Delete(fileCode + ".rsa");
            File.Delete(fileCode + ".asa");
            File.Delete(fileCode + ".log");
            File.Delete(coordFile);
            return asa;
        }*/

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
            string fileCode = "";
            Directory.SetCurrentDirectory(AppSettings.applicationStartPath + "\\tools");

            FormatParamFile("accall.input", coordFile);
            RunNaccess();
            fileCode = coordFile.Substring(0, coordFile.LastIndexOf("."));
            asa = ParseRsaFile(fileCode + ".rsa");

            File.Delete(fileCode + ".rsa");
            File.Delete(fileCode + ".asa");
            File.Delete(fileCode + ".log");
            File.Delete(coordFile);

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
             //   string commandParam = "/C C: && cd " + AppSettings.applicationStartPath + "\\tools" + 
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
