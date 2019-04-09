using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.BuIO;
using AuxFuncLib;

namespace CrystalInterfaceLib.ProtInterfaces
{
    public class InterfaceAsa
    {
        #region member variables
        public InterfaceWriter interfaceWriter = new InterfaceWriter();
        public AsaCalculator asaCalculator = new AsaCalculator();
        #endregion

        #region surface area for input file
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainFile"></param>
        /// <param name="destSaFileDir"></param>
        public void GenerateSurfaceAreaFile (string chainFile, string destSaFileDir)
        {
            bool deleteChainFile = false;
            if (chainFile.IndexOf(".gz") > -1)
            {
                chainFile = ParseHelper.UnZipFile(chainFile, ProtCidSettings.tempDir);
                deleteChainFile = true;
            }
            asaCalculator.ComputeSurfacAreaFiles(chainFile, destSaFileDir);
            if (deleteChainFile)
            {
                File.Delete(chainFile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainFile"></param>
        /// <param name="destSaFileDir"></param>
        public string[] GenerateSurfaceAreaFile(string chainFile)
        {
            bool deleteChainFile = false;
            if (chainFile.IndexOf(".gz") > -1)
            {
                chainFile = ParseHelper.UnZipFile(chainFile, ProtCidSettings.tempDir);
                deleteChainFile = true;
            }
            string[] saOutputFiles = asaCalculator.ComputeSurfacAreaFiles(chainFile);
            if (deleteChainFile)
            {
                File.Delete(chainFile);
            }
            return saOutputFiles;
        }
        #endregion

        #region surface area for chain interface
        /// <summary>
        /// get the surface area for all chains in the biological unit
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="biolUnitId"></param>
        /// <param name="buChainsHash"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public Dictionary<string, double> GetChainsSurfaceAreaInBu(string pdbId, string biolUnitId, Dictionary<string, AtomInfo[]> buChainsHash, string type)
        {
            Dictionary<string, double> buChainSaHash = new Dictionary<string, double>();
            string chainFile = "";
            foreach (string buChainStr in buChainsHash.Keys)
            {
                if (buChainsHash.ContainsKey(buChainStr))
                {
                    chainFile = interfaceWriter.WriterOneInterfaceChainToFile(pdbId, biolUnitId, buChainStr, buChainsHash[buChainStr], type);
                    double chainSa = ComputeInterfaceSurfaceArea(chainFile);
                    buChainSaHash.Add(buChainStr, chainSa);
                }
            }
            return buChainSaHash;
        }

        /// <summary>
        /// get the surface area for all chains in the biological unit
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="biolUnitId"></param>
        /// <param name="buChainsHash"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public Dictionary<string, double> GetChainsSurfaceAreaInBu(string pdbId, string biolUnitId, Dictionary<string, AtomInfo[]> buChainsHash, string[] asaChains, string type)
        {
            Dictionary<string, double> buChainSaHash = new Dictionary<string, double>();
            string chainFile = "";
            foreach (string chain in asaChains)
            {
                if (buChainsHash.ContainsKey(chain))
                {
                    chainFile = interfaceWriter.WriterOneInterfaceChainToFile(pdbId, biolUnitId, chain, buChainsHash[chain], type);
                    double chainSa = ComputeInterfaceSurfaceArea(chainFile);
                    buChainSaHash.Add(chain, chainSa);
                }
            }
            return buChainSaHash;
        }


        /// <summary>
        /// get the surface area for all chains in the biological unit
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="biolUnitId"></param>
        /// <param name="buChainsHash"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public Dictionary<string, double> GetChainsSurfaceAreaInBu(string pdbId, Dictionary<string, AtomInfo[]> buChainsHash, string type)
        {
            Dictionary<string, double> buChainSaHash = new Dictionary<string,double> ();
            string chainFile = "";
            foreach (string buChainStr in buChainsHash.Keys)
            {
                chainFile = interfaceWriter.WriterOneInterfaceChainToFile(pdbId, buChainStr, buChainsHash[buChainStr], type);
                double chainSa = ComputeInterfaceSurfaceArea(chainFile);
                buChainSaHash.Add(buChainStr, chainSa);
            }
            return buChainSaHash;
        }

        /// <summary>
        /// get the surface area for all chains in the biological unit
        /// </summary>
        /// <param name="pdbId"></param>
        /// <param name="biolUnitId"></param>
        /// <param name="buChainsHash"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public Dictionary<string, double> GetChainsSurfaceAreaInBu(string pdbId, Dictionary<string, AtomInfo[]> buChainsHash, string[] asaChains, string type)
        {
            Dictionary<string, double> buChainSaHash = new Dictionary<string, double>();
            string chainFile = "";
            foreach (string chain in asaChains)
            {
                if (buChainsHash.ContainsKey(chain))
                {
                    chainFile = interfaceWriter.WriterOneInterfaceChainToFile(pdbId, chain,
                        (AtomInfo[])buChainsHash[chain], type);
                    double chainSa = ComputeInterfaceSurfaceArea(chainFile);
                    buChainSaHash.Add(chain, chainSa);
                }
            }
            return buChainSaHash;
        }

        /// <summary>
        /// return the surface area
        /// </summary>
        /// <param name="complexFile"></param>
        /// <param name="chainAFile"></param>
        /// <param name="chainBFile"></param>
        /// <returns></returns>
        public double ComputeInterfaceSurfaceArea(string interfaceFile)
        {
            double asa = -1;
            try
            {
                asa = asaCalculator.ComputeSurfaceArea(interfaceFile);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(interfaceFile + " calculate asa errors: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(interfaceFile + " calculate asa errors: " + ex.Message);
            }
            return asa;
        }

        /// <summary>
        /// return the surface area
        /// </summary>
        /// <param name="complexFile"></param>
        /// <param name="chainAFile"></param>
        /// <param name="chainBFile"></param>
        /// <returns></returns>
        public double ComputeInterfaceSurfaceAreaBySurfrace (string interfaceFile)
        {
            double asa = -1;
            try
            {
                asa = asaCalculator.ComputeSurfaceAreaBySurfrace(interfaceFile);
            }
            catch (Exception ex)
            {
                ProtCidSettings.progressInfo.progStrQueue.Enqueue(interfaceFile + " calculate asa errors: " + ex.Message);
                ProtCidSettings.logWriter.WriteLine(interfaceFile + " calculate asa errors: " + ex.Message);
            }
            return asa;
        }

        /// <summary>
        /// get chains without surface areas computed
        /// </summary>
        /// <param name="interfaceInfoList"></param>
        /// <returns></returns>
        public string[] GetChainsWithoutAsa(ProtInterfaceInfo[] interfaceInfoList, string type)
        {
            List<string> asaChainList = new List<string> ();
            string chainStr = "";
            foreach (ProtInterfaceInfo interfaceInfo in interfaceInfoList)
            {
                if (interfaceInfo.ASA < 0)
                {
                    if (type.ToLower() == "pqs")
                    {
                        chainStr = interfaceInfo.Chain1;
                    }
                    else
                    {
                        chainStr = interfaceInfo.Chain1 + "_" + interfaceInfo.SymmetryString1;
                    }
                    if (!asaChainList.Contains(chainStr))
                    {
                        asaChainList.Add(chainStr);
                    }
                    if (type.ToLower() == "pqs")
                    {
                        chainStr = interfaceInfo.Chain2;
                    }
                    else
                    {
                        chainStr = interfaceInfo.Chain2 + "_" + interfaceInfo.SymmetryString2;
                    }
                    if (!asaChainList.Contains(chainStr))
                    {
                        asaChainList.Add(chainStr);
                    }
                }
            }
            string[] asaChains = new string[asaChainList.Count];
            asaChainList.CopyTo(asaChains);
            return asaChains;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chain1Asa"></param>
        /// <param name="chain2Asa"></param>
        /// <param name="complexAsa"></param>
        /// <returns></returns>
        public double CalculateInterfaceBuriedSurfaceArea(double chain1Asa, double chain2Asa, double complexAsa)
        {
            double bsa = -1;
            if (chain1Asa == -1 || chain2Asa == -1 || complexAsa == -1)
            {
                bsa = -1;
            }
            else
            {
                bsa = (chain1Asa + chain2Asa - complexAsa) / 2;
            }
            return bsa;
        }
        #endregion

        #region calculate surface area
        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFile"></param>
        /// <returns></returns>
        public double CalculateInterfaceBSA(string interfaceFile)
        {
            string[] interfaceChainFiles = GetChainCoordinateFiles(interfaceFile);
            double probeSize = 0;
            double interfaceAsa = asaCalculator.ComputeSurfaceArea (interfaceFile, out probeSize); // out the right probe size used
            double chainAAsa = asaCalculator.ComputeSurfaceArea (interfaceChainFiles[0], probeSize); // use the same probe size as interface file for each chain 
            double chainBAsa = asaCalculator.ComputeSurfaceArea (interfaceChainFiles[1], probeSize);

            File.Delete(interfaceChainFiles[0]);
            File.Delete(interfaceChainFiles[1]);

            double interfaceBsa = CalculateInterfaceBuriedSurfaceArea(chainAAsa, chainBAsa, interfaceAsa);
            return interfaceBsa;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFile"></param>
        /// <returns></returns>
        public double CalculateInterfaceBSABySurfrace (string interfaceFile)
        {
            string[] interfaceChainFiles = GetChainCoordinateFiles(interfaceFile);
            double probeSize = 1.4;
            double interfaceBsa = -1.0;
            try
            {
                double interfaceAsa = asaCalculator.ComputeSurfaceAreaBySurfrace(interfaceFile, out probeSize);
                if (interfaceAsa < 0)
                {
                    return -1.0;
                }
                double chainAAsa = asaCalculator.ComputeSurfaceAreaBySurfrace(interfaceChainFiles[0], probeSize);
                double chainBAsa = asaCalculator.ComputeSurfaceAreaBySurfrace(interfaceChainFiles[1], probeSize);

                File.Delete(interfaceChainFiles[0]);
                File.Delete(interfaceChainFiles[1]);

                interfaceBsa = CalculateInterfaceBuriedSurfaceArea(chainAAsa, chainBAsa, interfaceAsa);
            }
            catch (Exception ex)
            {
                throw new Exception(interfaceFile + " surface area error: " + ex.Message);
            }
            return interfaceBsa;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="interfaceFile"></param>
        /// <returns></returns>
        private string[] GetChainCoordinateFiles(string interfaceFile)
        {
            FileInfo fileInfo = new FileInfo(interfaceFile);
            string interfaceChainAFile = Path.Combine(fileInfo.DirectoryName, fileInfo.Name.Replace(".", "_A."));
            StreamWriter interfaceChainAWriter = new StreamWriter(interfaceChainAFile);
            string interfaceChainBFile = Path.Combine(fileInfo.DirectoryName, fileInfo.Name.Replace(".", "_B."));
            StreamWriter interfaceChainBWriter = new StreamWriter(interfaceChainBFile);

            StreamReader dataReader = new StreamReader(interfaceFile);
            string line = "";
            string chainId = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                if (line.Length <= 21)
                {
                    continue;
                }
                chainId = line.Substring(21, 1);
                if (chainId == "A")
                {
                    interfaceChainAWriter.WriteLine(line);
                }
                else if (chainId == "B")
                {
                    interfaceChainBWriter.WriteLine(line);
                }
            }
            dataReader.Close();
            interfaceChainAWriter.Close();
            interfaceChainBWriter.Close();

            string[] interfaceChainFiles = new string[2];
            interfaceChainFiles[0] = interfaceChainAFile;
            interfaceChainFiles[1] = interfaceChainBFile;
            return interfaceChainFiles;
        }
        #endregion
    }
}
