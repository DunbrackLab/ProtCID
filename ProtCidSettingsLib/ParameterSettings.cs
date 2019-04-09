using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace CrystalInterfaceLib.Settings
{
	/// <summary>
	/// Summary description for ParameterSettings.
	/// </summary>
	[XmlRoot("Xtal_Parameters")]
	public class ParameterSettings
	{
		public ParameterSettings()
		{
		}
		
		[XmlElement("KDops")] public KDopsParameters kDopsParam = new KDopsParameters ();
		[XmlElement("Contacts")] public ContactParameters contactParams = new ContactParameters ();
		[XmlElement("SimilarityOfInteractions")] public SimOfInteractParameters simInteractParam = new SimOfInteractParameters ();

		/// <summary>
		/// save parameter settings to a file
		/// </summary>
		/// <param name="pathFile"></param>
		public void Save(string paramFile)
		{
			ParameterSettings parameters = this;
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(ParameterSettings)); 
			TextWriter paramWriter = new StreamWriter (paramFile);
			xmlSerializer.Serialize (paramWriter, parameters);
			paramWriter.Close ();
		}

		/// <summary>
		/// load settings from a file
		/// </summary>
		/// <param name="pathFile"></param>
		public void Load(string pathFile)
		{
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(ParameterSettings));
			FileStream xmlFileStream = new FileStream(pathFile, FileMode.Open);
			ParameterSettings paramSettings = (ParameterSettings) xmlSerializer.Deserialize (xmlFileStream);
			xmlFileStream.Close ();
			this.contactParams = paramSettings.contactParams;
			this.kDopsParam = paramSettings.kDopsParam;
			this.simInteractParam = paramSettings.simInteractParam;
		}
	}

	/// <summary>
	/// container for Kdops parameters
	/// </summary>
	public class KDopsParameters
	{
		[XmlElement("K")]public int kParam = 0;
		[XmlElement("Methods")]public string bvTreeMethod = "Splatter";
		[XmlElement("AtomsInLeaf")] public int atomsInLeaf = 1;
	}

	/// <summary>
	/// container for contanct parameters
	/// </summary>
	public class ContactParameters
	{
		[XmlElement("AtomType")] public string atomType = "CA";
		[XmlElement("CutoffResidueDist")] public double cutoffResidueDist = 12.0;
		[XmlElement("CutoffNumResidueContacts")] public int numOfResidueContacts = 10;
		[XmlElement("CutoffAtomContact")] public double cutoffAtomDist = 6.0;
		[XmlElement("CutoffNumAtomContact")] public double numOfAtomContacts = 1;
        [XmlElement("DomainNumAtomContact")] public double domainNumOfAtomContacts = 5;
		[XmlElement("Steps")] public int steps = 1;
		// used to detect similar interface within a crystal form
		// the least Q score compared to random model
		[XmlElement("MinQScore")] public double minQScore = 0.1;
		// a chain with residues less than 15 is not used to build unit cell
		[XmlElement("MinNumResidueInChain")] public int minNumResidueInChain = 15;
	}

	/// <summary>
	/// container for similarity messurement of domain interactions
	/// </summary>
	public class SimOfInteractParameters
	{
		[XmlElement("Method")] public string simInteractMethod = "";
		[XmlElement("InterfaceSimilarityCutoff")] public double interfaceSimCutoff = 0.5;
		[XmlElement("UniqueInterfaceCutOff")] public double uniqueInterfaceCutoff = 0.95;
		[XmlElement("IdentityCutOff")]public double identityCutoff = 0.10;
	}
}
