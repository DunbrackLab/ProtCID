using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using ProtCidSettingsLib;
using AuxFuncLib;
using CrystalInterfaceLib.DomainInterfaces;

namespace InterfaceClusterLib.PymolScript
{
    public class InterfaceImagePymolScript 
    {
        public int[] imageSizes = { 500, 250, 40 };
        public enum ImageSize
        {
            Big, Medium, Thumbnail
        }
        private InterfaceAlignPymolScript interfaceAlignPymolScript = new InterfaceAlignPymolScript();

        #region chain  interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        /// <returns></returns>
        public string FormatInterfaceImagePymolScript(string[] clusterInterfaces, string interfaceImageDir)
        {
            string pymolScriptFile = Path.Combine(interfaceImageDir, "ImageGen.pml");
            StreamWriter pymolScriptWriter = new StreamWriter(pymolScriptFile);
            string interfacePymolScript = "";
            string pngFile = "";
            foreach (string clusterInterface in clusterInterfaces)
            {
                pngFile = GetPngFileName(clusterInterface, imageSizes[(int)ImageSize.Big], interfaceImageDir);
                if (File.Exists(pngFile))
                {
                    continue;
                }
                try
                {
                    interfacePymolScript = FormatInterfacePngPymolScript(clusterInterface, interfaceImageDir);
                    pymolScriptWriter.WriteLine(interfacePymolScript);
                    pymolScriptWriter.WriteLine("reinitialize");
                    pymolScriptWriter.WriteLine();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(clusterInterface + ": " + ex.Message);
                }
            }
            pymolScriptWriter.WriteLine("quit");
            pymolScriptWriter.Close();
            return pymolScriptFile;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterface"></param>
        /// <returns></returns>
        private string FormatInterfacePngPymolScript(string clusterInterface, string interfaceImageDir)
        {
            string interfaceFile = GetInterfaceFile(clusterInterface);

            string pngFile = "";

            string pymolScript = "load " + interfaceFile + ", format=pdb, object=" + interfaceFile + "\r\n";
            pymolScript += "hide lines, " + clusterInterface + ".cryst\r\n";
            pymolScript += "show cartoon, " + clusterInterface + ".cryst\r\n";
            pymolScript += "spectrum count, rainbow, " + clusterInterface + ".cryst and chain A \r\n";
            pymolScript += "spectrum count, rainbow, " + clusterInterface + ".cryst and chain B \r\n";
            pymolScript += "bg_color white\r\n";
            pymolScript += string.Format("ray {0}, {0}\r\n", imageSizes[(int)ImageSize.Big]);
            pngFile = GetPngFileName(clusterInterface, imageSizes[(int)ImageSize.Big], interfaceImageDir);
            pymolScript += "png " + pngFile + "\r\n";
            //     pymolScript += string.Format("ray {0}, {0}\r\n", imageSizes[(int)ImageSize.Medium]);
            //     pngFile = GetPngFileName(clusterInterface, imageSizes[(int)ImageSize.Medium]);
            //     pymolScript += "png " + pngFile + "\r\n";
            //      pymolScript += "save " + InterfaceImageDir + "\\" + clusterInterface + ".pse\r\n";
            return pymolScript;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterface"></param>
        /// <param name="imagePixels"></param>
        /// <returns></returns>
        public string GetPngFileName(string clusterInterface, int imagePixels, string interfaceImageDir)
        {
            string pngFileName = clusterInterface + "_" + imagePixels.ToString() + ".png";
            pngFileName = Path.Combine(interfaceImageDir, pngFileName);
            return pngFileName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterface"></param>
        /// <param name="imagePixels"></param>
        /// <returns></returns>
        private string GetJpegFileName(string clusterInterface, int imagePixels, string interfaceImageDir)
        {
            string jpgFileName = clusterInterface + "_" + imagePixels.ToString() + ".jpg";
            jpgFileName = Path.Combine(interfaceImageDir, jpgFileName);
            return jpgFileName;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterface"></param>
        /// <returns></returns>
        public string GetInterfaceFile(string clusterInterface)
        {
            string hashDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath,
                "cryst\\" + clusterInterface.Substring(1, 2));
            string orgInterfaceFile = Path.Combine(hashDir, clusterInterface + ".cryst.gz");
            string decompInterfaceFile = ParseHelper.UnZipFile(orgInterfaceFile, ProtCidSettings.tempDir);
            return decompInterfaceFile;
        }
        #endregion

        #region domain interfaces
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        /// <returns></returns>
        public string FormatDomainInterfacePngPymolScript(string[] clusterInterfaces, string interfaceImageDir)
        {
            string pymolScriptFile = Path.Combine(interfaceImageDir, "ImageGen.pml");
            StreamWriter pymolScriptWriter = new StreamWriter(pymolScriptFile);
            string interfacePymolScript = "";
            string pngFile = "";
            foreach (string clusterInterface in clusterInterfaces)
            {
                pngFile = GetPngFileName(clusterInterface, imageSizes[(int)ImageSize.Big], interfaceImageDir);
                if (File.Exists(pngFile))
                {
                    continue;
                }
                try
                {
                    interfacePymolScript = FormatDomainInterfacePngPymolScript(clusterInterface, interfaceImageDir);
                    pymolScriptWriter.WriteLine(interfacePymolScript);
                    pymolScriptWriter.WriteLine("reinitialize");
                    pymolScriptWriter.WriteLine();
                }
                catch (Exception ex)
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(clusterInterface + ": " + ex.Message);
                }
            }
            pymolScriptWriter.WriteLine("quit");
            pymolScriptWriter.Close();
            return pymolScriptFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <param name="interfaceImageDir"></param>
        /// <returns></returns>
        private string FormatDomainInterfacePngPymolScript(string domainInterface, string interfaceImageDir)
        {
            string domainInterfaceFile = GetDomainInterfaceFile(domainInterface);

            string pngFile = "";

            string pymolScript = "load " + domainInterfaceFile + ", format=pdb, object=" + domainInterfaceFile + "\r\n";
            pymolScript += "hide lines, " + domainInterface + ".cryst\r\n";
            pymolScript += "show cartoon, " + domainInterface + ".cryst\r\n";
         //   pymolScript += "spectrum count, rainbow, " + domainInterface + ".cryst and chain A \r\n";
         //   pymolScript += "spectrum count, rainbow, " + domainInterface + ".cryst and chain B \r\n";
            pymolScript += "color green, " + domainInterface + ".cryst and chain A \r\n";
            pymolScript += "color red, " + domainInterface + ".cryst and chain B \r\n";
            pymolScript += "bg_color white\r\n";
            pymolScript += string.Format("ray {0}, {0}\r\n", imageSizes[(int)ImageSize.Big]);
            pngFile = GetPngFileName(domainInterface, imageSizes[(int)ImageSize.Big], interfaceImageDir);
            pymolScript += "png " + pngFile + "\r\n";
            return pymolScript;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="domainInterface"></param>
        /// <returns></returns>
        private string GetDomainInterfaceFile(string domainInterface)
        {
            string hashDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath,
                "PfamDomain\\" + domainInterface.Substring(1, 2));
            string orgInterfaceFile = Path.Combine(hashDir, domainInterface + ".cryst.gz");
            string decompInterfaceFile = ParseHelper.UnZipFile(orgInterfaceFile, ProtCidSettings.tempDir);
            return decompInterfaceFile;
        }
        /// <summary>
        /// show domain interface in chain interface
        /// </summary>
        /// <param name="clusterInterface"></param>
        /// <returns></returns>
        public string FormatDomainChainInterfacePngPymolScript(string clusterInterface, string interfaceImageDir)
        {
            string[] fields = clusterInterface.Split('_');
            string pdbId = fields[0];
            int domainInterfaceId = Convert.ToInt32(fields[1]);
            int chainInterfaceId = 0;
            int[] chainInterfaceIds = null;
            Range[][] interfaceDomainRanges = interfaceAlignPymolScript.GetInterfaceDomainRanges(pdbId, domainInterfaceId, out chainInterfaceIds);
            string pymolScript = "";
            string interfaceFileName = "";
            string interfaceFile = "";
            string pngFile = "";
            if (chainInterfaceIds.Length == 1)
            {
                chainInterfaceId = chainInterfaceIds[0];
                if (chainInterfaceId != 0) // should check multi-chain domain interface
                {
                    interfaceFileName = pdbId + "_" + chainInterfaceId.ToString() + ".cryst";
                    interfaceFile = GetInterfaceFile(pdbId + "_" + chainInterfaceId.ToString());

                    string domainResidueRangeStringA = " and chain A and resi " + interfaceAlignPymolScript.FormatDomainRanges(interfaceDomainRanges[0]);
                    string domainResidueRangeStringB = " and chain B and resi " + interfaceAlignPymolScript.FormatDomainRanges(interfaceDomainRanges[1]);

                    pymolScript = ("load " + interfaceFile + "\r\n");
                    pymolScript += ("hide lines, " + interfaceFileName + "\r\n");
                    pymolScript += ("show cartoon, " + interfaceFileName + "\r\n");
                    pymolScript += ("color gray30,  " + interfaceFileName + "\r\n");
                    pymolScript += ("spectrum count, rainbow, " + interfaceFileName + domainResidueRangeStringA + "\r\n");
                    pymolScript += ("spectrum count, rainbow, " + interfaceFileName + domainResidueRangeStringB + "\r\n");
                }
                else // for intra-chain
                {
                    interfaceFileName = pdbId + "_d" + domainInterfaceId.ToString() + ".cryst";
                    interfaceFile = GetNonChainInterfaceFile(pdbId + "_d" + domainInterfaceId.ToString());
                    pymolScript = ("load " + interfaceFile + "\r\n");
                    pymolScript += ("hide lines, " + interfaceFileName + "\r\n");
                    pymolScript += ("show cartoon, " + interfaceFileName + "\r\n");
                    //      pymolScript += ("color gray30,  " + interfaceFileName + "\r\n");
                    pymolScript += ("spectrum count, rainbow, " + interfaceFileName + " and chain A\r\n");
                    pymolScript += ("spectrum count, rainbow, " + interfaceFileName + " and chain B\r\n");
                }
            }
            else // for multi-chain domain interface
            {
                interfaceFileName = pdbId + "_m" + domainInterfaceId.ToString() + ".cryst";
                interfaceFile = GetNonChainInterfaceFile(pdbId + "_d" + domainInterfaceId.ToString());
                pymolScript = ("load " + interfaceFile + "\r\n");
                pymolScript += ("hide lines, " + interfaceFileName + "\r\n");
                pymolScript += ("show cartoon, " + interfaceFileName + "\r\n");
                //      pymolScript += ("color gray30,  " + interfaceFileName + "\r\n");
                pymolScript += ("spectrum count, rainbow, " + interfaceFileName + " and chain A \r\n");
                pymolScript += ("spectrum count, rainbow, " + interfaceFileName + " and chain B \r\n");
            }

            pymolScript += "bg_color white\r\n";
            pymolScript += string.Format("ray {0}, {0}\r\n", imageSizes[(int)ImageSize.Big]);
            pngFile = GetPngFileName(clusterInterface, imageSizes[(int)ImageSize.Big], interfaceImageDir);
            pymolScript += "png " + pngFile + "\r\n";

            return pymolScript;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterface"></param>
        /// <returns></returns>
        public string GetNonChainInterfaceFile(string clusterInterface)
        {
            string hashDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath,
                "PfamDomain\\" + clusterInterface.Substring(1, 2));
            string orgInterfaceFile = Path.Combine(hashDir, clusterInterface + ".cryst.gz");
            string decompInterfaceFile = ParseHelper.UnZipFile(orgInterfaceFile, ProtCidSettings.tempDir);
            return decompInterfaceFile;
        }
        #endregion
    }
}
