using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataCollectorLib.FatcatAlignment
{
    public class FatcatUpdate
    {
        /// <summary>
        /// the file is generated from entryalignments.cs
        /// </summary>
        /// <param name="missingAlignedEntryPairsFile"></param>
        public void UpdateFatcatAlignments(string missingAlignedEntryPairsFile)
        {
            AlignmentsRetriever alignmentsRetriever = new AlignmentsRetriever();
            string alignmentFile = alignmentsRetriever.GetFatcatAlignmentsOnLinux(missingAlignedEntryPairsFile);

            FatcatAlignmentParser alignmentsParser = new FatcatAlignmentParser();
            alignmentsParser.ParseFatcatAlignmentFile(alignmentFile);
        }
    }
}
