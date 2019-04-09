using System;
using System.Collections.Generic;
using System.Text;
using AuxFuncLib;

namespace InterfaceClusterLib.Alignments
{
    /// <summary>
    /// The BLOSUM62 score for alignments
    /// </summary>
    public class AlignmentBlosumScore
    {
        private const string blosum62Matrix = "   A  R  N  D  C  Q  E  G  H  I  L  K  M  F  P  S  T  W  Y  V  B  Z  X  * " +
                                              "A  4 -1 -2 -2  0 -1 -1  0 -2 -1 -1 -1 -1 -2 -1  1  0 -3 -2  0 -2 -1  0 -4 " +
                                              "R -1  5  0 -2 -3  1  0 -2  0 -3 -2  2 -1 -3 -2 -1 -1 -3 -2 -3 -1  0 -1 -4 " +
                                              "N -2  0  6  1 -3  0  0  0  1 -3 -3  0 -2 -3 -2  1  0 -4 -2 -3  3  0 -1 -4 " +
                                              "D -2 -2  1  6 -3  0  2 -1 -1 -3 -4 -1 -3 -3 -1  0 -1 -4 -3 -3  4  1 -1 -4 " +
                                              "C  0 -3 -3 -3  9 -3 -4 -3 -3 -1 -1 -3 -1 -2 -3 -1 -1 -2 -2 -1 -3 -3 -2 -4 " +
                                              "Q -1  1  0  0 -3  5  2 -2  0 -3 -2  1  0 -3 -1  0 -1 -2 -1 -2  0  3 -1 -4 " +
                                              "E -1  0  0  2 -4  2  5 -2  0 -3 -3  1 -2 -3 -1  0 -1 -3 -2 -2  1  4 -1 -4 " +
                                              "G  0 -2  0 -1 -3 -2 -2  6 -2 -4 -4 -2 -3 -3 -2  0 -2 -2 -3 -3 -1 -2 -1 -4 " +
                                              "H -2  0  1 -1 -3  0  0 -2  8 -3 -3 -1 -2 -1 -2 -1 -2 -2  2 -3  0  0 -1 -4 " +
                                              "I -1 -3 -3 -3 -1 -3 -3 -4 -3  4  2 -3  1  0 -3 -2 -1 -3 -1  3 -3 -3 -1 -4 " +
                                              "L -1 -2 -3 -4 -1 -2 -3 -4 -3  2  4 -2  2  0 -3 -2 -1 -2 -1  1 -4 -3 -1 -4 " +
                                              "K -1  2  0 -1 -3  1  1 -2 -1 -3 -2  5 -1 -3 -1  0 -1 -3 -2 -2  0  1 -1 -4 " +
                                              "M -1 -1 -2 -3 -1  0 -2 -3 -2  1  2 -1  5  0 -2 -1 -1 -1 -1  1 -3 -1 -1 -4 " +
                                              "F -2 -3 -3 -3 -2 -3 -3 -3 -1  0  0 -3  0  6 -4 -2 -2  1  3 -1 -3 -3 -1 -4 " +
                                              "P -1 -2 -2 -1 -3 -1 -1 -2 -2 -3 -3 -1 -2 -4  7 -1 -1 -4 -3 -2 -2 -1 -2 -4 " +
                                              "S  1 -1  1  0 -1  0  0  0 -1 -2 -2  0 -1 -2 -1  4  1 -3 -2 -2  0  0  0 -4 " +
                                              "T  0 -1  0 -1 -1 -1 -1 -2 -2 -1 -1 -1 -1 -2 -1  1  5 -2 -2  0 -1 -1  0 -4 " +
                                              "W -3 -3 -4 -4 -2 -2 -3 -2 -2 -3 -2 -3 -1  1 -4 -3 -2 11  2 -3 -4 -3 -2 -4 " +
                                              "Y -2 -2 -2 -3 -2 -1 -2 -3  2 -1 -1 -2 -1  3 -3 -2 -2  2  7 -1 -3 -2 -1 -4 " +
                                              "V  0 -3 -3 -3 -1 -2 -2 -3 -3  3  1 -2  1 -1 -2 -2  0 -3 -1  4 -3 -2 -1 -4 " +
                                              "B -2 -1  3  4 -3  0  1 -1  0 -3 -4  0 -3 -3 -2  0 -1 -4 -3 -3  4  1 -1 -4 " +
                                              "Z -1  0  0  1 -3  3  4 -2  0 -3 -3  1 -1 -3 -1  0 -1 -3 -2 -2  1  4 -1 -4 " +
                                              "X  0 -1 -1 -1 -2 -1 -1 -1 -1 -1 -1 -1 -1 -1 -2  0  0 -2 -1 -1 -1 -1 -1 -4 " +
                                              "* -4 -4 -4 -4 -4 -4 -4 -4 -4 -4 -4 -4 -4 -4 -4 -4 -4 -4 -4 -4 -4 -4 -4  1 ";

        private Dictionary<string, int> blosum62Hash = new Dictionary<string,int> ();

        public AlignmentBlosumScore()
        {
            ReadBloSum62();
        }

        /// <summary>
        /// read-only property
        /// </summary>
        public Dictionary<string, int> Blosum62Hash
        {
            get
            {
                return blosum62Hash;
            }
        }
        
        #region read BloSum62
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private void ReadBloSum62()
        {
            int numOfCharLine = 74; // the number of characters in one row is 74 including space

            string firstLine = blosum62Matrix.Substring(0, numOfCharLine);
            string[] aaLetters = ParseHelper.SplitPlus(firstLine, ' ');

            string rowLine = "";
            for (int i = numOfCharLine; i < blosum62Matrix.Length; i += numOfCharLine)
            {
                rowLine = blosum62Matrix.Substring(i, numOfCharLine);
                string[] fields = ParseHelper.SplitPlus(rowLine, ' ');
                for (int j = 1; j < fields.Length; j++)
                {
                    blosum62Hash.Add(aaLetters[j - 1] + fields[0], Convert.ToInt32(fields[j]));
                }
            }
        }
        #endregion

        
        #region Score From BloSum62
        /// <summary>
        /// find the score of the alignment from BloSum62
        /// </summary>
        /// <param name="alignmentPair"></param>
        /// <param name="blosum62Hash"></param>
        /// <returns></returns>
        public int GetScoreForTheAlignment(string alignSequence1, string alignSequence2)
        {
            int score = 0;
            string aaPair = "";
            // probably the wrong alignment
            if (alignSequence1.Length != alignSequence2.Length)
            {
                return -40000; // the possible least score
            }
            for (int i = 0; i < alignSequence1.Length; i++)
            {
                aaPair = alignSequence1[i].ToString() + alignSequence2[i].ToString();
                if (blosum62Hash.ContainsKey(aaPair))
                {
                    score += (int)blosum62Hash[aaPair];
                    continue;
                }
                aaPair = "*" + alignSequence2[i].ToString();
                if (blosum62Hash.ContainsKey(aaPair))
                {
                    score += (int)blosum62Hash[aaPair];
                    continue;
                }
                aaPair = alignSequence1[i].ToString() + "*";
                if (blosum62Hash.ContainsKey(aaPair))
                {
                    score += (int)blosum62Hash[aaPair];
                    continue;
                }
                aaPair = "**";
                if (blosum62Hash.ContainsKey(aaPair))
                {
                    score += (int)blosum62Hash[aaPair];
                    continue;
                }
            }
            return score;
        }
        #endregion
    }
}
