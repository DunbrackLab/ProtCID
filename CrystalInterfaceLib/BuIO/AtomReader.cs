using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.DomainInterfaces;
using AuxFuncLib;

namespace CrystalInterfaceLib.BuIO
{
    public class AtomReader
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        public AtomInfo[] ReadChainAtoms(string pdbId, string asymChain)
        {
            if (asymChain == "")
            {
                return null;
            }
            string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string crystalXmlFile = ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir);
            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(crystalXmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            ChainAtoms chain = null;
            for (int i = 0; i < thisEntryCrystal.atomCat.ChainAtomList.Length; i++)
            {
                if (thisEntryCrystal.atomCat.ChainAtomList[i].AsymChain == asymChain)
                {
                    chain = thisEntryCrystal.atomCat.ChainAtomList[i];
                    break;
                }
            }

            File.Delete(crystalXmlFile);
            if (chain == null)
            {
                return null;
            }
            AtomInfo[] chainAtoms = chain.CartnAtoms;
            return chainAtoms;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        public AtomInfo[] ReadChainAtoms(string pdbId, string asymChain, out string threeLetterSeq)
        {
            threeLetterSeq = "";

            if (asymChain == "")
            {
                return null;
            }
            string xmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string crystalXmlFile = ParseHelper.UnZipFile(xmlFile, ProtCidSettings.tempDir);
            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(crystalXmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            ChainAtoms chain = null;
            
            for (int i = 0; i < thisEntryCrystal.atomCat.ChainAtomList.Length; i++)
            {
                if (thisEntryCrystal.atomCat.ChainAtomList[i].AsymChain == asymChain)
                {
                    chain = thisEntryCrystal.atomCat.ChainAtomList[i];
                    break;
                }
            }
            for (int i = 0; i < thisEntryCrystal.entityCat.EntityInfoList.Length; i++)
            {
                EntityInfo entityInfo = thisEntryCrystal.entityCat.EntityInfoList[i];
                string[] asymChains = entityInfo.asymChains.Split(',');
                if (asymChains.Contains(asymChain))
                {
                    threeLetterSeq = entityInfo.threeLetterSeq;
                }
            }
            File.Delete(crystalXmlFile);
            if (chain == null)
            {
                return null;
            }
            AtomInfo[] chainAtoms = chain.CartnAtoms;
            return chainAtoms;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="ranges"></param>
        public AtomInfo[] ReadDomainAtoms(string pdbId, string asymChain, Range[] ranges)
        {
            AtomInfo[] chainAtoms = ReadChainAtoms(pdbId, asymChain);
            if (chainAtoms == null)
            {
                return null;
            }
            List<AtomInfo> atomList = new List<AtomInfo> ();
            int seqId = -1;
            foreach (AtomInfo atom in chainAtoms)
            {
                seqId = Convert.ToInt32(atom.seqId);
                if (IsSeqNumberInRanges(seqId, ranges))
                {
                    atomList.Add(atom);
                }
            }
            return atomList.ToArray ();
        }        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="ranges"></param>
        public AtomInfo[] ReadDomainAtoms(string pdbId, string asymChain, Range[] ranges, out string chainThreeLetterSeq)
        {
            chainThreeLetterSeq = "";
            AtomInfo[] chainAtoms = ReadChainAtoms(pdbId, asymChain, out chainThreeLetterSeq);
            if (chainAtoms == null)
            {
                return null;
            }
            List<AtomInfo> atomList = new List<AtomInfo> ();
            int seqId = -1;
            foreach (AtomInfo atom in chainAtoms)
            {
                seqId = Convert.ToInt32(atom.seqId);
                if (IsSeqNumberInRanges(seqId, ranges))
                {
                    atomList.Add(atom);
                }
            }
            return atomList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqNumber"></param>
        /// <param name="ranges"></param>
        /// <returns></returns>
        private bool IsSeqNumberInRanges(int seqNumber, Range[] ranges)
        {
            foreach (Range range in ranges)
            {
                if (seqNumber <= range.endPos && seqNumber >= range.startPos)
                {
                    return true;
                }
            }
            return false;
        }


        #region read atomic coordinates from input file
        // so it doesn't depend on the Appsetting
        // added on Sept. 22, 2016
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        public AtomInfo[] ReadChainAtomsFromFile(string coordXmlFile, string asymChain)
        {
            if (asymChain == "")
            {
                return null;
            }

            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(coordXmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            ChainAtoms chain = null;
            for (int i = 0; i < thisEntryCrystal.atomCat.ChainAtomList.Length; i++)
            {
                if (thisEntryCrystal.atomCat.ChainAtomList[i].AsymChain == asymChain)
                {
                    chain = thisEntryCrystal.atomCat.ChainAtomList[i];
                    break;
                }
            }

            if (chain == null)
            {
                return null;
            }
            AtomInfo[] chainAtoms = chain.CartnAtoms;
            return chainAtoms;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        public AtomInfo[] ReadChainAtomsFromFile(string coordXmlFile, string asymChain, out string threeLetterSeq)
        {
            threeLetterSeq = "";

            if (asymChain == "")
            {
                return null;
            }
            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(coordXmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            ChainAtoms chain = null;

            for (int i = 0; i < thisEntryCrystal.atomCat.ChainAtomList.Length; i++)
            {
                if (thisEntryCrystal.atomCat.ChainAtomList[i].AsymChain == asymChain)
                {
                    chain = thisEntryCrystal.atomCat.ChainAtomList[i];
                    break;
                }
            }
            for (int i = 0; i < thisEntryCrystal.entityCat.EntityInfoList.Length; i++)
            {
                EntityInfo entityInfo = thisEntryCrystal.entityCat.EntityInfoList[i];
                string[] asymChains = entityInfo.asymChains.Split(',');
                if (asymChains.Contains(asymChain))
                {
                    threeLetterSeq = entityInfo.threeLetterSeq;
                }
            }

            if (chain == null)
            {
                return null;
            }
            AtomInfo[] chainAtoms = chain.CartnAtoms;
            return chainAtoms;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="ranges"></param>
        public AtomInfo[] ReadDomainAtomsFromFile(string coordXmlFile, string asymChain, Range[] ranges)
        {
            AtomInfo[] chainAtoms = ReadChainAtomsFromFile(coordXmlFile, asymChain);
            if (chainAtoms == null)
            {
                return null;
            }
            List<AtomInfo> atomList = new List<AtomInfo> ();
            int seqId = -1;
            foreach (AtomInfo atom in chainAtoms)
            {
                seqId = Convert.ToInt32(atom.seqId);
                if (IsSeqNumberInRanges(seqId, ranges))
                {
                    atomList.Add(atom);
                }
            }
            return atomList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="ranges"></param>
        public AtomInfo[] ReadDomainAtomsFromFile(string coordXmlFile, string asymChain, Range[] ranges, out string chainThreeLetterSeq)
        {
            chainThreeLetterSeq = "";
            AtomInfo[] chainAtoms = ReadChainAtomsFromFile(coordXmlFile, asymChain, out chainThreeLetterSeq);
            if (chainAtoms == null)
            {
                return null;
            }
            List<AtomInfo> atomList = new List<AtomInfo> ();
            int seqId = -1;
            foreach (AtomInfo atom in chainAtoms)
            {
                seqId = Convert.ToInt32(atom.seqId);
                if (IsSeqNumberInRanges(seqId, ranges))
                {
                    atomList.Add(atom);
                }
            }
            return atomList.ToArray ();
        }
        #endregion

        #region read multiple chains from a coordinate xml file
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChains"></param>
        /// <returns></returns>
        public Dictionary<string, AtomInfo[]> ReadChainAtomsFromFile(string coordXmlFile, string[] asymChains)
        {
            Dictionary<string, AtomInfo[]> chainAtomInfoHash = new Dictionary<string,AtomInfo[]> ();
            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(coordXmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            foreach (string asymChain in asymChains)
            {
                AtomInfo[] chainAtoms = ReadChainAtoms(thisEntryCrystal, asymChain);
                chainAtomInfoHash.Add(asymChain, chainAtoms);
            }
            return chainAtomInfoHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="thisEntryCrystal"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        private AtomInfo[] ReadChainAtoms(EntryCrystal thisEntryCrystal, string asymChain)
        {
            ChainAtoms chain = null;
            for (int i = 0; i < thisEntryCrystal.atomCat.ChainAtomList.Length; i++)
            {
                if (thisEntryCrystal.atomCat.ChainAtomList[i].AsymChain == asymChain)
                {
                    chain = thisEntryCrystal.atomCat.ChainAtomList[i];
                    break;
                }
            }

            if (chain == null)
            {
                return null;
            }
            AtomInfo[] chainAtoms = chain.CartnAtoms;
            return chainAtoms;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <returns></returns>
        public Dictionary<string, AtomInfo[]> ReadChainAtomsFromFileWithSeq(string coordXmlFile, string[] asymChains, out Dictionary<string, string> chainSeqCodeHash)
        {
            string threeLetterSeq = "";
            Dictionary<string, AtomInfo[]> chainAtomsHash = new Dictionary<string,AtomInfo[]> ();
            chainSeqCodeHash = new Dictionary<string,string> ();
            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(coordXmlFile, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();

            foreach (string asymChain in asymChains )
            {
                AtomInfo[] chain = ReadChainAtoms(thisEntryCrystal, asymChain, out threeLetterSeq);
                chainAtomsHash.Add(asymChain, chain);
                chainSeqCodeHash.Add(asymChain, threeLetterSeq);
            }

            return chainAtomsHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="thisEntryCrystal"></param>
        /// <param name="asymChain"></param>
        /// <param name="threeLetterSeq"></param>
        /// <returns></returns>
        private AtomInfo[] ReadChainAtoms(EntryCrystal thisEntryCrystal, string asymChain, out string threeLetterSeq)
        {
            ChainAtoms chain = null;
            threeLetterSeq = "";
            for (int i = 0; i < thisEntryCrystal.atomCat.ChainAtomList.Length; i++)
            {
                if (thisEntryCrystal.atomCat.ChainAtomList[i].AsymChain == asymChain)
                {
                    chain = thisEntryCrystal.atomCat.ChainAtomList[i];
                    break;
                }
            }
            for (int i = 0; i < thisEntryCrystal.entityCat.EntityInfoList.Length; i++)
            {
                EntityInfo entityInfo = thisEntryCrystal.entityCat.EntityInfoList[i];
                string[] asymChains = entityInfo.asymChains.Split(',');
                if (asymChains.Contains(asymChain))
                {
                    threeLetterSeq = entityInfo.threeLetterSeq;
                }
            }

            if (chain == null)
            {
                return null;
            }
            AtomInfo[] chainAtoms = chain.CartnAtoms;
            return chainAtoms;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="ranges"></param>
        public Dictionary<string, AtomInfo[]> ReadDomainAtomsFromFile(string coordXmlFile, Dictionary<string, Range[]> asymChainRangesHash)
        {
            List<string> chainList = new List<string> (asymChainRangesHash.Keys);
            string[] asymChains = new string[chainList.Count];
            chainList.CopyTo(asymChains);
            Dictionary<string, AtomInfo[]> chainAtomHash = ReadChainAtomsFromFile(coordXmlFile, asymChains);
            Dictionary<string, AtomInfo[]> domainAtomHash = new Dictionary<string,AtomInfo[]> ();
            foreach (string asymChain in asymChains)
            {
                AtomInfo[] chainAtoms = chainAtomHash[asymChain];
                Range[] domainRanges = asymChainRangesHash[asymChain];
                AtomInfo[] domainAtoms = ReadDomainAtoms(chainAtoms, domainRanges);
                domainAtomHash.Add(asymChain, domainAtoms);
            }
            return domainAtomHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainAtoms"></param>
        /// <param name="ranges"></param>
        /// <returns></returns>
        public AtomInfo[] ReadDomainAtoms (AtomInfo[] chainAtoms, Range[] ranges)
        {
            List<AtomInfo> atomList = new List<AtomInfo> ();
            int seqId = -1;
            foreach (AtomInfo atom in chainAtoms)
            {
                seqId = Convert.ToInt32(atom.seqId);
                if (IsSeqNumberInRanges(seqId, ranges))
                {
                    atomList.Add(atom);
                }
            }
            return atomList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="asymChain"></param>
        /// <param name="ranges"></param>
        public Dictionary<string, AtomInfo[]> ReadDomainAtomsFromFileWithSeq(string coordXmlFile, Dictionary<string, Range[]> asymChainRangesHash, out Dictionary<string, string> chainThreeLetterSeqHash)
        {
            List<string> chainList = new List<string> (asymChainRangesHash.Keys);
            string[] asymChains = new string[chainList.Count];
            chainList.CopyTo(asymChains);
            chainThreeLetterSeqHash = null;
            Dictionary<string, AtomInfo[]> chainAtomHash = ReadChainAtomsFromFileWithSeq(coordXmlFile, asymChains, out chainThreeLetterSeqHash);
          
            Dictionary<string, AtomInfo[]> domainAtomHash = new Dictionary<string,AtomInfo[]>  ();
            foreach (string asymChain in asymChains)
            {
                AtomInfo[] chainAtoms = chainAtomHash[asymChain];
                Range[] domainRanges = asymChainRangesHash[asymChain];
                AtomInfo[] domainAtoms = ReadDomainAtoms(chainAtoms, domainRanges);
                domainAtomHash.Add (asymChain, domainAtoms);
            }
            return domainAtomHash;
        }     
        #endregion
    }
}
