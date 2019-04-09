using System;
using System.IO;
using System.Xml.Serialization;
using System.Collections.Generic;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.KDops;
using ProgressLib;
using DbLib;
using ProtCidSettingsLib;

namespace CrystalInterfaceLib.Settings
{
	/// <summary>
	/// Summary description for AppSettings.
	/// </summary>
	public class AppSettings
	{		
        // abbreviation for each crystalization method 
        public static Dictionary<string, string> crystMethodHash = null;
        public static string[] abbrevNonXrayCrystMethods = null;

		// used by the library
		public static SymOps symOps = null;
		// paramters to build BVTree and detect chain contacts
		public static KVector kVector = null;		
        public static KVector kVectors3D = null;

		public static ParameterSettings parameters = null;

        public static string paramFile = ProtCidSettings.paramFile;
        public static string symOpsFile = ProtCidSettings.symOpsFile;
        public static string crystMethodFile = ProtCidSettings.crystMethodFile;

		public AppSettings()
		{
		}

		#region setting files provided by parameters
		/// <summary>
		/// load all parameters
		/// </summary>
		public static void LoadParameters(string paramFile)
		{
			if (! File.Exists (paramFile))
			{
				throw new Exception ("Parameters file not exist");
			}
			parameters = new ParameterSettings ();
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(ParameterSettings));
			FileStream xmlFileStream = new FileStream(paramFile, FileMode.Open);
			parameters = (ParameterSettings) xmlSerializer.Deserialize (xmlFileStream);
			xmlFileStream.Close ();
			
			kVector = new KVector (parameters.kDopsParam.kParam);
			kVectors3D = new KVector ();
		}

		/// <summary>
		/// load all parameters and symmetry operators
		/// </summary>
		public static void LoadSymOps(string symOpsFile)
		{
			symOps = new SymOps ();
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(SymOps));
			FileStream symOpsFileStream = new FileStream(symOpsFile, FileMode.Open);
			symOps = (SymOps) xmlSerializer.Deserialize (symOpsFileStream);
		}
		#endregion

		#region Setting files provided in application
		/// <summary>
		/// load all parameters
		/// </summary>
		public static void LoadParameters()
		{
			if (! File.Exists (paramFile))
			{
				throw new Exception ("Parameters file not exist");
			}
			parameters = new ParameterSettings ();
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(ParameterSettings));
			FileStream xmlFileStream = new FileStream(paramFile, FileMode.Open);
			parameters = (ParameterSettings) xmlSerializer.Deserialize (xmlFileStream);
			xmlFileStream.Close ();

			kVector = new KVector (parameters.kDopsParam.kParam);
			kVectors3D = new KVector ();
		}

		/// <summary>
		/// load all parameters and symmetry operators
		/// </summary>
		public static void LoadSymOps()
		{
			if (! File.Exists (symOpsFile))
			{
				throw new Exception ("Symmetry operators file not exist");
			}
			symOps = new SymOps ();
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(SymOps));
			FileStream symOpsFileStream = new FileStream(symOpsFile, FileMode.Open);
			symOps = (SymOps) xmlSerializer.Deserialize (symOpsFileStream);
			symOpsFileStream.Close ();
		}

        /*
         *  ECR	ELECTRON CRYSTALLOGRAPHY
            EMI	ELECTRON MICROSCOPY	
            FDI	FIBER DIFFRACTION
            FTR	FLUORESCENCE TRANSFER
            ISP	INFRARED SPECTROSCOPY
            NDI	NEUTRON DIFFRACTION
            PDI	POWDER DIFFRACTION
            NMR	SOLID-STATE NMR
            NMR	SOLUTION NMR
            SSC	SOLUTION SCATTERING
            XRAY	X-RAY DIFFRACTION
         * */
        /// <summary>
        ///  the crystalization method in PDB XML files. 
        ///  crystMethodFile location on the start location, settings folder, CrystMethods.txt file
        /// </summary>
        public static void LoadCrystMethods()
        {
            if (!File.Exists(crystMethodFile))
            {
                throw new Exception("Crystal method file not exist");
            }
            crystMethodHash = new Dictionary<string,string> ();
            StreamReader dataReader = new StreamReader(crystMethodFile);
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                crystMethodHash.Add(fields[1], fields[0]);
            }
            dataReader.Close();

            abbrevNonXrayCrystMethods = new string[crystMethodHash.Count - 1]; // except 
            int count = 0;
            foreach (string crystMethod in crystMethodHash.Keys)
            {
                if (crystMethodHash[crystMethod] == "XRAY")
                {
                    continue;
                }
                abbrevNonXrayCrystMethods[count] = crystMethodHash[crystMethod];
                count++;
            }
        }
		#endregion
	}
}
