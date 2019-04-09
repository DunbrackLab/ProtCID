using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using CrystalInterfaceLib.Crystal;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces
{
	/// <summary>
	/// Summary description for DomainAtoms.
	/// </summary>
	public class DomainAtomsReader
	{
		public DomainAtomsReader()
		{
		}

		/// <summary>
		/// atoms for each domain
		/// </summary>
		/// <param name="pdbId"></param>
		/// <param name="chainId"></param>
		/// <param name="domainRangesHash"></param>
		/// <returns></returns>
        public Dictionary<string, AtomInfo[]> ReadAtomsOfDomains(string pdbId, string chainId, Dictionary<string, string> domainRangesHash)
		{
			string xmlFile = Path.Combine (ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
			string crystalXmlFile = ParseHelper.UnZipFile (xmlFile, ProtCidSettings.tempDir);
			// read data from crystal xml file
			EntryCrystal thisEntryCrystal;
			XmlSerializer xmlSerializer = new XmlSerializer (typeof(EntryCrystal));
			FileStream xmlFileStream = new FileStream(crystalXmlFile, FileMode.Open);
			thisEntryCrystal = (EntryCrystal) xmlSerializer.Deserialize (xmlFileStream);
			xmlFileStream.Close ();

            Dictionary<string, AtomInfo[]> domainAtomsHash = new Dictionary<string, AtomInfo[]>();

			ChainAtoms chain = null;
			for (int i = 0; i < thisEntryCrystal.atomCat.ChainAtomList.Length; i ++)
			{
				if (thisEntryCrystal.atomCat.ChainAtomList[i].AsymChain == chainId)
				{
					chain = thisEntryCrystal.atomCat.ChainAtomList[i];
				}
			}
			foreach (string DomainID in domainRangesHash.Keys)
			{
				string rangeString = (string)domainRangesHash[DomainID];
				domainAtomsHash.Add (DomainID, GetDomainAtoms(chain, rangeString));
			}
			return domainAtomsHash;
		}

        /// <summary>
        /// atoms for each domain
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="chainId"></param>
        /// <param name="domainRangesHash"></param>
        /// <returns></returns>
        public Dictionary<long, AtomInfo[]> ReadAtomsOfDomains(string pdbId, string chainId, Dictionary<long, string> domainRangesHash, Dictionary<string, ChainAtoms> chainAtomsHash)
        {
            Dictionary<long, AtomInfo[]> domainAtomsHash = new Dictionary<long, AtomInfo[]>();

            ChainAtoms chain = null;
            foreach (string asymChain in chainAtomsHash.Keys)
            {
                if (asymChain == chainId)
                {
                    chain = chainAtomsHash[asymChain];
                }
            }
            foreach (long DomainID in domainRangesHash.Keys)
            {
                string rangeString = (string)domainRangesHash[DomainID];
                AtomInfo[] domainAtoms = GetDomainAtoms(chain, rangeString);
                if (domainAtoms != null && domainAtoms.Length > 0)
                {
                    domainAtomsHash.Add(DomainID, domainAtoms);
                }
            }
            return domainAtomsHash;
        }

        /// <summary>
        /// atoms for each asymmetric chain
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, ChainAtoms> ReadAtomsOfChains(string pdbId)
        {
            string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string crystalXmlFile = ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir);
            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(crystalXmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            Dictionary<string, ChainAtoms> chainAtomsHash = new Dictionary<string, ChainAtoms>();

            for (int i = 0; i < thisEntryCrystal.atomCat.ChainAtomList.Length; i++)
            {
                if (! chainAtomsHash.ContainsKey(thisEntryCrystal.atomCat.ChainAtomList[i].AsymChain))
                {
                    chainAtomsHash.Add(thisEntryCrystal.atomCat.ChainAtomList[i].AsymChain,
                        thisEntryCrystal.atomCat.ChainAtomList[i]);
                }
            }

            File.Delete(crystalXmlFile);
            return chainAtomsHash;
        }

        /// <summary>
        /// atoms for each asymmetric chain
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, ChainAtoms> ReadAtomsOfChains(string pdbId, string[] asymChains)
        {
            string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string crystalXmlFile = ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir);
            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(crystalXmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            Dictionary<string, ChainAtoms> chainAtomsHash = new Dictionary<string,ChainAtoms> ();

            foreach (string asymChain in asymChains)
            {
                for (int i = 0; i < thisEntryCrystal.atomCat.ChainAtomList.Length; i++)
                {
                    if (thisEntryCrystal.atomCat.ChainAtomList[i].AsymChain == asymChain)
                    {
                        chainAtomsHash.Add(asymChain, thisEntryCrystal.atomCat.ChainAtomList[i]);
                        break;
                    }
                }
            }

            File.Delete(crystalXmlFile);
            return chainAtomsHash;
        }

		/// <summary>
		/// atom coordinates for domains
        /// A SCOP domain may have multiple segmentations
		/// </summary>
		/// <param name="chain"></param>
		/// <param name="ranges"></param>
		/// <returns></returns>
		private AtomInfo[] GetDomainAtoms (ChainAtoms chain, string rangeString)
		{
			AtomInfo[] domainAtoms = null;
			List<AtomInfo> domainAtomList = new List<AtomInfo> ();
			int startPos = -1;
			int endPos = -1;
			int seqId = -1;
			string[] ranges = rangeString.Split (';');
			foreach (string range in ranges)
			{
				if (range == "-")
				{
					domainAtoms = chain.CartnAtoms;
				}
				else
				{
					string[] posFields = range.Split ('-');
					startPos = ParseHelper.ConvertSeqToInt (posFields[0]);
					endPos = ParseHelper.ConvertSeqToInt (posFields[1]);
					foreach (AtomInfo atom in chain.CartnAtoms)
					{
						seqId = ParseHelper.ConvertSeqToInt (atom.seqId);
						if ( seqId >= startPos && seqId <= endPos)
						{
							domainAtomList.Add (atom);
						}
					}
				}
			}
            return domainAtomList.ToArray();
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordFile"></param>
        /// <param name="remarkString"></param>
        /// <returns></returns>
        public AtomInfo[] ReadChainCoordFile(string coordFile, out string remarkString)
        {
            StreamReader atomReader = new StreamReader(coordFile);
            List<AtomInfo> atomInfoList = new List<AtomInfo> ();
            string line = "";
            remarkString = "";
            while ((line = atomReader.ReadLine()) != null)
            {
                if (line.IndexOf("REMARK") > -1)
                {
                    remarkString += (line + "\r\n");
                }
                if (line.IndexOf("ATOM  ") > -1)
                {
                    string[] fields = ParseHelper.ParsePdbAtomLine(line);
                    AtomInfo atom = new AtomInfo();
                    atom.atomId = Convert.ToInt32(fields[1]);
                    atom.atomName = fields[2];
                    atom.residue = fields[4];
                    atom.seqId = fields[6];
                    atom.xyz.X = Convert.ToDouble(fields[8]);
                    atom.xyz.Y = Convert.ToDouble(fields[9]);
                    atom.xyz.Z = Convert.ToDouble(fields[10]);
                    atomInfoList.Add(atom);
                }
            }
            atomReader.Close();
            return atomInfoList.ToArray ();
        }
	}
}
