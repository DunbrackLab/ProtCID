using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.IO;
using AuxFuncLib;
using DbLib;
using CrystalInterfaceLib.Settings;

namespace DataCollectorLib.Uniprot
{
    public class BlastUnpAlignments
    {
        #region member variables
//       private string alignResultDir = @"E:\DbProjectData\UniProt\PdbUnpAlignFiles";
//        private string unpSeqFileDir = @"E:\DbProjectData\UniProt\UnpSequencesInPdb1";

        private DbQuery dbQuery = new DbQuery();
        #endregion

        public void BlastPdbUnpSeqAlignments()
        {

        }


        #region run bl2seq
        /// <summary>
        /// 
        /// </summary>
        /// <param name="commandParam"></param>
        /// <returns></returns>
        public void RunBl2Seq(string commandParam)
        {
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo();
                Process naccessProcess = null;

                // set properties for the process
                processInfo.CreateNoWindow = true;
                processInfo.UseShellExecute = false;
                processInfo.FileName = "tools\bl2seq.exe";
                processInfo.Arguments = commandParam;
                naccessProcess = Process.Start(processInfo);
                naccessProcess.WaitForExit();
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputFile1"></param>
        /// <param name="inputFile2"></param>
        /// <param name="outputFile"></param>
        /// <returns></returns>
        private string SetBl2seqCommandParam(string inputFile1, string inputFile2, out string outputFile)
        {
            string commandParam = "";
            outputFile = GetTheFileName(inputFile1) + "_" + GetTheFileName(inputFile2) + ".out";
            commandParam = "-i " + inputFile1 + " -j " + inputFile2 + " -p blastp -F F -o " + outputFile;
            return commandParam;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="seqFile"></param>
        /// <returns></returns>
        private string GetTheFileName(string seqFile)
        {
            int lastDashIndex = seqFile.LastIndexOf("\\");
            int exeIndex = seqFile.IndexOf(".");
            return seqFile.Substring(lastDashIndex + 1, exeIndex - lastDashIndex - 1);
        }
        #endregion

        #region parse pdb unp blast alignments
        public void ParseBlastAlignment(string alignFile)
        {

        }
        #endregion
    }
}
