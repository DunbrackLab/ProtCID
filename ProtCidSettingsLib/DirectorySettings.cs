using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace ProtCidSettingsLib
{
	/// <summary>
	/// Summary description for dataSettings.
	/// </summary>
	public class DirectorySettings
	{
        // data source
		[XmlElement("XML_Path")] 
        public string xmlPath = "";
		[XmlElement("XmlCoordinate_path")] 
        public string coordXmlPath = "";
		[XmlElement("InterfaceFile_path")] 
        public string interfaceFilePath = "";		
		[XmlElement("Pfam_Path")] 
        public string pfamPath = "";
		[XmlElement("Pisa_path")] 
        public string pisaPath = "";
        [XmlElement("Pisces_Path")] 
        public string piscesPath = "";
        [XmlElement("Fatcat_Path")] 
        public string fatcatPath = "";
        [XmlElement("Sifts_Path")] 
        public string siftsPath = "";
        [XmlElement("Fasta_Path")] 
        public string seqFastaPath = "";  // result fasta files from clutsers
        // for pdbfam
        [XmlElement("Psiblast_Path")] 
        public string psiblastPath = "";
        // the original sequences from PDB, unp, human
        [XmlElement("SeqFile_Path")]
        public string seqFilePath = "";
        [XmlElement("HH_Path")]
        public string hhPath = "";
        [XmlElement("Hmmer_Path")]
        public string hmmerPath = "";
        // databases
        [XmlElement("PdbfamDB_Path")]
        public string pdbfamDbPath = "";
        [XmlElement("BAInterfaceDB_Path")]
        public string baInterfaceDbPath = "";
        [XmlElement("ProtcidDB_Path")]
        public string protcidDbPath = "";
        [XmlElement("AlignmentDB_Path")]
        public string alignmentDbPath = "";

		public DirectorySettings()
		{
		}

		/// <summary>
		/// save this settings to a file
		/// </summary>
		/// <param name="pathFile"></param>
		public void Save(string dirFile)
		{
			DirectorySettings dirSettings = this;
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(DirectorySettings)); 
			TextWriter pathWriter = new StreamWriter (dirFile);
			xmlSerializer.Serialize (pathWriter, dirSettings);
			pathWriter.Close ();
		}

		/// <summary>
		/// load settings from a file
		/// </summary>
		/// <param name="pathFile"></param>
		public void Load(string pathFile)
		{
			if (! File.Exists (pathFile))
			{
				return ;
			}
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(DirectorySettings));
			FileStream xmlFileStream = new FileStream(pathFile, FileMode.Open);
			DirectorySettings dirSettings = (DirectorySettings) xmlSerializer.Deserialize (xmlFileStream);
			xmlFileStream.Close ();
			this.xmlPath = dirSettings.xmlPath;
			this.coordXmlPath = dirSettings.coordXmlPath;
			this.interfaceFilePath = dirSettings.interfaceFilePath;			
			this.pisaPath = dirSettings.pisaPath;
			this.pfamPath = dirSettings.pfamPath;
            this.pisaPath = dirSettings.pisaPath;
            this.piscesPath = dirSettings.piscesPath;           
            this.fatcatPath = dirSettings.fatcatPath;
            this.siftsPath = dirSettings.siftsPath;
            this.seqFastaPath = dirSettings.seqFastaPath;

            this.protcidDbPath = dirSettings.protcidDbPath;
            this.baInterfaceDbPath = dirSettings.baInterfaceDbPath;
            this.alignmentDbPath = dirSettings.alignmentDbPath;
            this.pdbfamDbPath = dirSettings.pdbfamDbPath;

            this.psiblastPath = dirSettings.psiblastPath;
            this.seqFilePath = dirSettings.seqFilePath;
            this.hhPath = dirSettings.hhPath;
            this.hmmerPath = dirSettings.hmmerPath;
		}
	}
}
