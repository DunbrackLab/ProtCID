using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Data;
using CrystalInterfaceLib.FileParser;
using CrystalInterfaceLib.Crystal;
using AuxFuncLib;
using DbLib;
using ProtCidSettingsLib;

namespace BuCompLib
{
    public class BiolUnitRetriever
    {
        private DbQuery dbQuery = new DbQuery();

        #region generate BUs
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> GetEntryBiolUnits(string pdbId, string[] nonMonomerBUs)
        {
            Dictionary<string, Dictionary<string, AtomInfo[]>> entryBiolUnits = null;
            switch (BuCompBuilder.BuType)
            {
                case "pdb":
                    entryBiolUnits = GetPdbBiolUnits(pdbId, nonMonomerBUs);
                    break;

                case "pisa":
                    entryBiolUnits = GetPisaBoilUnits(pdbId, nonMonomerBUs);
                    break;

                case "asu":
                    entryBiolUnits = GetAsymUnit(pdbId); // use the same variable
                    break;

                default:
                    break;
            }
            return entryBiolUnits;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> GetPdbBiolUnits(string pdbId, string[] nonMonomerBUs)
        {
            PdbBuGenerator pdbBuBuilder = new PdbBuGenerator();
            string zippedXmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string xmlFile = ParseHelper.UnZipFile(zippedXmlFile, ProtCidSettings.tempDir);
    //        Hashtable pdbBiolUnits = pdbBuBuilder.BuildPdbBusFromCoordFile(xmlFile, nonMonomerBUs, "ALL");
            Dictionary<string, Dictionary<string, AtomInfo[]>> pdbBiolUnits = pdbBuBuilder.BuildPdbBus(pdbId, nonMonomerBUs, false);
            if (pdbBiolUnits.Count == 0)
            {
                if (IsEntryNmrStructure(pdbId))
                {
                    Dictionary<string, Dictionary<string, AtomInfo[]>> asymUnitHash = GetAsymUnit(pdbId);
                    // the BuID is set to be 1 for NMR structure
                    // the NMR biological unit is same as asymunit
                    pdbBiolUnits.Add("1", asymUnitHash["0"]);
                }
            }
            File.Delete(xmlFile);
            return pdbBiolUnits;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        private bool IsEntryNmrStructure(string pdbId)
        {
            string queryString = string.Format("Select Method From PdbEntry Where PdbID = '{0}';", pdbId);
            DataTable methodTable = dbQuery.Query(ProtCidSettings.pdbfamDbConnection, queryString);
            if (methodTable.Rows.Count > 0)
            {
                string method = methodTable.Rows[0]["Method"].ToString().TrimEnd();
                if (method.IndexOf("NMR") > -1)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbId"></param>
        /// <returns></returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> GetPisaBoilUnits(string pdbId, string[] nonMonomerBUs)
        {
            PisaBuGenerator pisaBuBuilder = new PisaBuGenerator();
            string coordXmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string unzippedXmlFile = ParseHelper.UnZipFile(coordXmlFile, ProtCidSettings.tempDir);
            Dictionary<string, Dictionary<string, AtomInfo[]>> pisaBiolUnits = pisaBuBuilder.BuildPisaAssemblies(pdbId, nonMonomerBUs);
            File.Delete(unzippedXmlFile);
            return pisaBiolUnits;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbid"></param>
        /// <returns>the asymunit id is set to be 0</returns>
        public Dictionary<string, Dictionary<string, AtomInfo[]>> GetAsymUnit(string pdbId)
        {
            string coordXmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string coordXml = ParseHelper.UnZipFile(coordXmlFile, ProtCidSettings.tempDir);

            Dictionary<string, AtomInfo[]> asuChainsHash =  new Dictionary<string,AtomInfo[]> ();
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
            File.Delete(coordXml);
            // try to fit the format for biological units by setting biolunitid = "1";
            Dictionary<string, Dictionary<string, AtomInfo[]>> asymUnitHash = new Dictionary<string, Dictionary<string, AtomInfo[]>>();
            asymUnitHash.Add("0", asuChainsHash);
            return asymUnitHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdbid"></param>
        /// <returns>the asymunit id is set to be 0</returns>
        public Dictionary<string, AtomInfo[]> GetAsymUnitChainHash(string pdbId)
        {
            string coordXmlFile = Path.Combine(ProtCidSettings.dirSettings.coordXmlPath, pdbId + ".xml.gz");
            string coordXml = ParseHelper.UnZipFile(coordXmlFile, ProtCidSettings.tempDir);

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
                    if (!asuChainsHash.ContainsKey(chain.AsymChain))
                    {
                        asuChainsHash.Add(chain.AsymChain, chain.CartnAtoms);
                    }
                }
            }
            File.Delete(coordXml);

            return asuChainsHash;
        }
        #endregion
    }
}
