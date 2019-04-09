using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.Contacts;
using AuxFuncLib;
using CrystalInterfaceLib.KDops;
using CrystalInterfaceLib.Settings;

namespace BuCompLib.BuInterfaces
{
    public class AsuInterfaces
    {
        public AsuInterfaces()
        {
        }

        /// <summary>
        /// interfaces from an asu file
        /// </summary>
        /// <param name="asuXmlFile"></param>
        public InterfaceChains[] GetAsuInterfacesFromXml(string asuXmlFile)
        {
            string coordXml = asuXmlFile;
            if (asuXmlFile.Substring(asuXmlFile.LastIndexOf(".") + 1, 2) == "gz")
            {
                coordXml = ParseHelper.UnZipFile(asuXmlFile, ProtCidSettings.tempDir);
            }
            string pdbId = coordXml.Substring(coordXml.LastIndexOf("\\") + 1, 4);

            Dictionary<string, AtomInfo[]> asymUnit = GetAsuFromXml(coordXml);
            File.Delete(coordXml);
            // monomer, no need to compute the interfaces
            if (asymUnit.Count < 2)
            {
                return null;
            }
            return GetInterfacesInAsu(pdbId, asymUnit);
            //		InsertDataToDb ();

        }
        /// <summary>
        /// retrieve the asu chains
        /// </summary>
        /// <param name="coordXml"></param>
        /// <returns></returns>
        public Dictionary<string, AtomInfo[]> GetAsuFromXml(string coordXml)
        {
            Dictionary<string, AtomInfo[]> asuChainsHash = new Dictionary<string, AtomInfo[]>();
            // read data from crystal xml file
            EntryCrystal thisEntryCrystal;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(EntryCrystal));
            FileStream xmlFileStream = new FileStream(coordXml, FileMode.Open);
            thisEntryCrystal = (EntryCrystal)xmlSerializer.Deserialize(xmlFileStream);
            xmlFileStream.Close();
            ChainAtoms[] chainAtomsList = thisEntryCrystal.atomCat.ChainAtomList;
            foreach (ChainAtoms chain in chainAtomsList)
            {
                if (chain.PolymerType == "polypeptide")
                {
                    if (!asuChainsHash.ContainsKey(chain.AsymChain + "_1_555"))
                    {
                        asuChainsHash.Add(chain.AsymChain + "_1_555", chain.CartnAtoms);
                    }
                }
            }
            return asuChainsHash;
        }

        /// <summary>
        /// get the interfaces in an ASU
        /// </summary>
        /// <param name="asuChainsHash"></param>
        public InterfaceChains[] GetInterfacesInAsu(string pdbId, Dictionary<string, AtomInfo[]> asymUnit)
        {
            // build trees for the biological unit
            Dictionary<string, BVTree> asuChainTreesHash = BuildBVtreesForAsu(asymUnit);
            // calculate interfaces
            List<InterfaceChains> interfaceList = new List<InterfaceChains> ();
            List<string> chainAndSymOpList = new List<string> (asuChainTreesHash.Keys);
            chainAndSymOpList.Sort();

            int interChainId = 0;
            for (int i = 0; i < chainAndSymOpList.Count - 1; i++)
            {
                for (int j = i + 1; j < chainAndSymOpList.Count; j++)
                {
                    ChainContact chainContact = new ChainContact(chainAndSymOpList[i].ToString(), chainAndSymOpList[j].ToString());
                    ChainContactInfo contactInfo = chainContact.GetChainContactInfo((BVTree)asuChainTreesHash[chainAndSymOpList[i]],
                        (BVTree)asuChainTreesHash[chainAndSymOpList[j]]);
                    if (contactInfo != null)
                    {
                        interChainId++;

                        InterfaceChains interfaceChains = new InterfaceChains(chainAndSymOpList[i].ToString(), chainAndSymOpList[j].ToString());
                        // no need to change the tree node data
                        // only assign the refereces
                        interfaceChains.chain1 = ((BVTree)asuChainTreesHash[chainAndSymOpList[i]]).Root.CalphaCbetaAtoms();
                        interfaceChains.chain2 = ((BVTree)asuChainTreesHash[chainAndSymOpList[j]]).Root.CalphaCbetaAtoms();
                        interfaceChains.interfaceId = interChainId;
                        interfaceChains.firstSymOpString = (string)chainAndSymOpList[i];
                        interfaceChains.secondSymOpString = (string)chainAndSymOpList[j];
                        interfaceChains.seqDistHash = chainContact.ChainContactInfo.GetBbDistHash();
                        interfaceList.Add(interfaceChains);
                        //chainContact = null;
                    }
                }
            }
            // return interface chains
            return interfaceList.ToArray ();

        }

        /// <summary>
        /// build BVtrees for chains in a biological unit
        /// </summary>
        /// <param name="biolUnit"></param>
        /// <returns></returns>
        private Dictionary<string, BVTree> BuildBVtreesForAsu(Dictionary<string, AtomInfo[]> asymUnit)
        {
            Dictionary<string, BVTree> chainTreesHash = new Dictionary<string,BVTree> ();
            // for each chain in the biological unit
            // build BVtree
            foreach (string chainAndSymOp in asymUnit.Keys)
            {
                BVTree chainTree = new BVTree();
                chainTree.BuildBVTree(asymUnit[chainAndSymOp], AppSettings.parameters.kDopsParam.bvTreeMethod, true);
                chainTreesHash.Add(chainAndSymOp, chainTree);
            }
            return chainTreesHash;
        }
        // end of functions

        public InterfaceChains[] GetAllChainInteractions(string asuXmlFile)
        {
            string coordXml = asuXmlFile;
            if (asuXmlFile.Substring(asuXmlFile.LastIndexOf(".") + 1, 2) == "gz")
            {
                coordXml = ParseHelper.UnZipFile(asuXmlFile, ProtCidSettings.tempDir);
            }
            string pdbId = coordXml.Substring(coordXml.LastIndexOf("\\") + 1, 4);

            Dictionary<string, AtomInfo[]> asymUnit = GetAsuFromXml(coordXml);
            File.Delete(coordXml);
            // monomer, no need to compute the interfaces
            if (asymUnit.Count < 2)
            {
                return null;
            }
            return GetAllInteractionsInAsu(pdbId, asymUnit);
            //		InsertDataToDb ();
        }


        /// <summary>
        /// get the interfaces in an ASU
        /// </summary>
        /// <param name="asuChainsHash"></param>
        public InterfaceChains[] GetAllInteractionsInAsu (string pdbId, Dictionary<string, AtomInfo[]> asymUnit)
        {
            // build trees for the biological unit
            Dictionary<string, BVTree> asuChainTreesHash = BuildBVtreesForAsu(asymUnit);
            // calculate interfaces
            List<InterfaceChains> interfaceList = new List<InterfaceChains> ();
            List<string> chainAndSymOpList = new List<string> (asuChainTreesHash.Keys);
            chainAndSymOpList.Sort();

            int interChainId = 0;
            for (int i = 0; i < chainAndSymOpList.Count - 1; i++)
            {
                for (int j = i + 1; j < chainAndSymOpList.Count; j++)
                {
                    ChainContact chainContact = new ChainContact(chainAndSymOpList[i].ToString(), chainAndSymOpList[j].ToString());
                    ChainContactInfo contactInfo = chainContact.GetAnyChainContactInfo((BVTree)asuChainTreesHash[chainAndSymOpList[i]],
                        (BVTree)asuChainTreesHash[chainAndSymOpList[j]]);
                    if (contactInfo != null)
                    {
                        interChainId++;

                        InterfaceChains interfaceChains = new InterfaceChains(chainAndSymOpList[i].ToString(), chainAndSymOpList[j].ToString());
                        // no need to change the tree node data
                        // only assign the refereces
                        interfaceChains.chain1 = ((BVTree)asuChainTreesHash[chainAndSymOpList[i]]).Root.AtomList;
                        interfaceChains.chain2 = ((BVTree)asuChainTreesHash[chainAndSymOpList[j]]).Root.AtomList;
                        interfaceChains.interfaceId = interChainId;
                        interfaceChains.firstSymOpString = (string)chainAndSymOpList[i];
                        interfaceChains.secondSymOpString = (string)chainAndSymOpList[j];
                        interfaceChains.seqDistHash = chainContact.ChainContactInfo.GetBbDistHash();
                        interfaceList.Add(interfaceChains);
                        //chainContact = null;
                    }
                }
            }
            // return interface chains
            return interfaceList.ToArray ();
        }
    }
}
