using System;
using System.Collections.Generic;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Crystal;
using CrystalInterfaceLib.Settings;
#if DEBUG
using System.IO;
#endif

namespace CrystalInterfaceLib.SimMethod
{
	/// <summary>
	/// Structure similarity score function
	/// </summary>
	public class SimScoreFunc
	{
		public SimScoreFunc()
		{
			
		}

		#region Weighted Q Function
		/* Algorithm:
		 * 1. Calculate all of the interatomic distances between the proteins in one structure
		 * and lable these Dij, Eij for the other
		 * 2. Wij = 1 - ((min(Dij, Eij) / Doff)^2) if min(Dij, Eij) < Doff
		 * Wij = 0 if min(Dij, Eij) >= Doff
		 * 3. Score function: 
		 * Q = Sum(Wij * exp(-k|Dij - Eij|)) / sum(Wij) (k = 0.5)
		 * Q is close to 1 if two structures are similar.
		*/
		/// <summary>
		/// weighted Q function 
		/// Input: two structures with exactly same sequences
		/// but may exist missing residues
		/// Output: weighted Q score
		/// </summary>
		/// <param name="interChain1"></param>
		/// <param name="interChain2"></param>
		public double WeightQFunc (ChainContact interfaceChain1, ChainContact interfaceChain2)
		{
			// match residue sequence numbers
			// in case there are missing residues in coordinates in a protein
			double weightDistSum = 0.0;
			double weightSum = 0.0;
			Dictionary<string, AtomPair> atomContactHash1 = interfaceChain1.ChainContactInfo.atomContactHash;
            Dictionary<string, AtomPair> atomContactHash2 = interfaceChain2.ChainContactInfo.atomContactHash;
			foreach (string conKey in atomContactHash1.Keys)
			{
				AtomPair atomPair = (AtomPair) atomContactHash1[conKey];
				double dist1 = atomPair.distance;
				
				if (atomContactHash2.ContainsKey (conKey))
				{
					double dist2 = ((AtomPair)atomContactHash2[conKey]).distance;
					
					double weightTemp = Math.Pow((Math.Min (dist1, dist2) / AppSettings.parameters.contactParams.cutoffResidueDist), 2);
					double weight = Math.Pow (1 - weightTemp, 2);
					weightSum += weight;
					weightDistSum += weight * Math.Exp (Math.Abs (dist1 - dist2) * -0.5);
				}
			}
			if (weightSum == 0)
			{
				return -1.0;
			}
			return (weightDistSum / weightSum);
		}

		#region Q score for two interface chains
		/// <summary>
		/// Q Score for two interfaces 
		/// based on residue pairs with minimum distance
		/// </summary>
		/// <param name="interfaceChain1"></param>
		/// <param name="interfaceChain2"></param>
		/// <returns></returns>
		public double WeightQFunc (InterfaceChains interfaceChain1, InterfaceChains interfaceChain2)
		{
			// must handle missing coordinates 
			// the order of sequence id may not be exactly same
			double weightDistSum = 0.0;
			double weightSum = 0.0;
			Dictionary<string, double> seqDistHash1 = interfaceChain1.seqDistHash;
            Dictionary<string, double> seqDistHash2 = new Dictionary<string, double>(interfaceChain2.seqDistHash);
			foreach (string conKey in seqDistHash1.Keys)
			{
				double dist1 = seqDistHash1[conKey];

				if (seqDistHash2.ContainsKey (conKey))
				{
					double dist2 = seqDistHash2[conKey];
					
					double weight = GetProbFromGauss (Math.Min (dist1, dist2));
					weightSum += weight;
					weightDistSum += weight * Math.Exp (Math.Abs (dist1 - dist2) * -0.5);
					// those remaining distances should find matching distance from 
					seqDistHash2.Remove (conKey);
				}
					// should get the distance from the whole proteins
				else
				{
					double dist2 = interfaceChain2.CbetaDistForSeqIds (conKey.ToString ());
					
					// no data available
					if (dist2 == -1)
					{
						continue;
					}
					// dist1 must be the minimum since dist1 < cutoff and dist2 > cutoff
					double weight = GetProbFromGauss (dist1);
					weightSum += weight;
					weightDistSum += weight * Math.Exp (Math.Abs (dist1 - dist2) * -0.5);
				}
			}
			// handle the remaining distances in disthash2
			// find corresponding distances from the whole proteins
			foreach (string seqIdPair in seqDistHash2.Keys)
			{
				double dist2 = seqDistHash2[seqIdPair];
				
				double dist1 = interfaceChain1.CbetaDistForSeqIds (seqIdPair.ToString ());
				
				// no data available
				if (dist1 == -1)
				{
					continue;
				}
				
				// dist2 must be the minimum since dist2 < cutoff and dist1 > cutoff
				double weight = GetProbFromGauss (dist2);
				weightSum += weight;
				weightDistSum += weight * Math.Exp (Math.Abs (dist1 - dist2) * -0.5);
			}
			if (weightSum == 0)
			{
				return -1.0;
			}
			return (weightDistSum / weightSum);
		}

		/// <summary>
		/// Q Score for two interfaces 
		/// based on residue pairs with minimum distance
		/// </summary>
		/// <param name="interfaceChain1"></param>
		/// <param name="interfaceChain2"></param>
		/// <returns></returns>
		public double ProtBudWeightQFunc (InterfaceChains interfaceChain1, InterfaceChains interfaceChain2)
		{
			// must handle missing coordinates 
			// the order of sequence id may not be exactly same
			double weightDistSum = 0.0;
			double weightSum = 0.0;
            Dictionary<string, double> seqDistHash1 = interfaceChain1.seqDistHash;
            Dictionary<string, double> seqDistHash2 = new Dictionary<string, double>(interfaceChain2.seqDistHash);
			foreach (string conKey in seqDistHash1.Keys)
			{
				double dist1 = seqDistHash1[conKey];

				if (seqDistHash2.ContainsKey (conKey))
				{
					double dist2 = seqDistHash2[conKey];
					
					double weightTemp = Math.Pow((Math.Min (dist1, dist2) / AppSettings.parameters.contactParams.cutoffResidueDist), 2);
					double weight = Math.Pow (1 - weightTemp, 2);
					weightSum += weight;
					weightDistSum += weight * Math.Exp (Math.Abs (dist1 - dist2) * -0.5);
					// those remaining distances should find matching distance from 
					seqDistHash2.Remove (conKey);
				}
					// should get the distance from the whole proteins
				else
				{
					double dist2 = interfaceChain2.CbetaDistForSeqIds (conKey.ToString ());
					
					// no data available
					if (dist2 == -1)
					{
						continue;
					}
					// dist1 must be the minimum since dist1 < cutoff and dist2 > cutoff
					
					double weightTemp = Math.Pow((dist1 / AppSettings.parameters.contactParams.cutoffResidueDist), 2);
					double weight = Math.Pow (1 - weightTemp, 2);
					weightSum += weight;
					weightDistSum += weight * Math.Exp (Math.Abs (dist1 - dist2) * -0.5);
				}
			}
			// handle the remaining distances in disthash2
			// find corresponding distances from the whole proteins
			foreach (string seqIdPair in seqDistHash2.Keys)
			{
				double dist2 = seqDistHash2[seqIdPair];
				
				double dist1 = interfaceChain1.CbetaDistForSeqIds (seqIdPair.ToString ());
				
				// no data available
				if (dist1 == -1)
				{
					continue;
				}
				
				// dist2 must be the minimum since dist2 < cutoff and dist1 > cutoff
				double weightTemp = Math.Pow((dist2 / AppSettings.parameters.contactParams.cutoffResidueDist), 2);
				double weight = Math.Pow (1 - weightTemp, 2);
				weightSum += weight;
				weightDistSum += weight * Math.Exp (Math.Abs (dist1 - dist2) * -0.5);
			}
			if (weightSum == 0)
			{
				return -1.0;
			}
			
			return (weightDistSum / weightSum);
		}
		#endregion

		#region Q score for two interfaces 
		/// <summary>
		/// Q Score for two interfaces, only based on residue pairs 
		/// with Calpha or Cbeta distance less than some cutoff
		/// </summary>
		/// <param name="interfaceChain1"></param>
		/// <param name="interfaceChain2"></param>
		/// <returns></returns>
		public double ComputeInterfacesQscore_Min (InterfaceChains interfaceChain1, InterfaceChains interfaceChain2)
		{
			// must handle missing coordinates 
			// the order of sequence id may not be exactly same
			double weightDistSum = 0.0;
			double weightSum = 0.0;
            Dictionary<string, double> seqDistHash1 = interfaceChain1.seqDistHash;
            Dictionary<string, double> seqDistHash2 = new Dictionary<string, double>(interfaceChain2.seqDistHash);
			foreach (string conKey in seqDistHash1.Keys)
			{
				double dist1 = seqDistHash1[conKey];

				if (seqDistHash2.ContainsKey (conKey))
				{
					double dist2 = seqDistHash2[conKey];
					
					double weightTemp = Math.Pow((Math.Min (dist1, dist2) / AppSettings.parameters.contactParams.cutoffResidueDist), 2);
					double weight = Math.Pow (1 - weightTemp, 2);
					weightSum += weight;
					weightDistSum += weight * Math.Exp (Math.Abs (dist1 - dist2) * -1);
				}
			}
			if (weightSum == 0)
			{
				return -1.0;
			}
			
			return (weightDistSum / weightSum);
		}

		/// <summary>
		/// Q Score for two interfaces, only based on residue pairs 
		/// with Calpha or Cbeta distance less than some cutoff
		/// </summary>
		/// <param name="interfaceChain1"></param>
		/// <param name="interfaceChain2"></param>
		/// <returns></returns>
		public double ComputeInterfacesQscore_Max (InterfaceChains interfaceChain1, InterfaceChains interfaceChain2)
		{
			// must handle missing coordinates 
			// the order of sequence id may not be exactly same
			double weightDistSum = 0.0;
			double weightSum = 0.0;
            Dictionary<string, double> seqDistHash1 = interfaceChain1.seqDistHash;
            Dictionary<string, double> seqDistHash2 = new Dictionary<string, double>(interfaceChain2.seqDistHash);
			foreach (string conKey in seqDistHash1.Keys)
			{
				double dist1 = seqDistHash1[conKey];

				if (seqDistHash2.ContainsKey (conKey))
				{
					double dist2 = seqDistHash2[conKey];
					
					double weightTemp = Math.Pow((Math.Max (dist1, dist2) / AppSettings.parameters.contactParams.cutoffResidueDist), 2);
					double weight = Math.Pow (1 - weightTemp, 2);
					weightSum += weight;
					weightDistSum += weight * Math.Exp (Math.Abs (dist1 - dist2) * -1);
				}
			}
			if (weightSum == 0)
			{
				return -1.0;
			}
			
			return (weightDistSum / weightSum);
		}
		#endregion

		#endregion

		#region Kullback-Leibler Distance -- first version
		// the minimum distance between two Calpha or Cbeta is 4
		// normalize a distance starting 0
		private const double zeroNormValue = 4.0;
		// when distance is 12, the probability is close to 0
		// an arbitrary number
		private const double sigma1 = 1.0;
		/// <summary>
		/// Q Score for two interfaces 
		/// based on KLD distributions
		/// </summary>
		/// <param name="interfaceChain1"></param>
		/// <param name="interfaceChain2"></param>
		/// <returns></returns>
		public double KldQFunc (InterfaceChains interface1, InterfaceChains interface2)
		{
			/*		string[] commonResiduePairs = GetCommonResiduePairs (interface1, interface2);		
					double propSum1 = GetPropSum (interface1);
					double propSum2 = GetPropSum (interface2);
					double propSum1InCommon = 0.0;
					double propSum2InCommon = 0.0;
					foreach (string residuePair in commonResiduePairs)
					{
						string[] seqIds = residuePair.Split ('_');
						double prop1 = GetNormProp ((double)interface1.seqDistHash[residuePair], propSum1);
						double prop2 = GetNormProp ((double)interface2.seqDistHash[residuePair], propSum2);
						propSum1InCommon += prop1 * Math.Log (prop1 / prop2);
						propSum2InCommon += prop2 * Math.Log (prop2 / prop1);
					}
					double qScore = 1- (propSum1InCommon + propSum2InCommon);
					return qScore;
					*/
			string[] allResiduePairs = GetAllResiduePairs (interface1, interface2);
			Dictionary<string, double> probHash1 = null;
            Dictionary<string, double> probHash2 = null;
			double propSum1 = GetProbDistHash (interface1, allResiduePairs, ref probHash1, "KLD");
			double propSum2 = GetProbDistHash (interface2, allResiduePairs, ref probHash2, "KLD");
			double qScore = GetDistrSimScore (probHash1, probHash2);
			return qScore;
		}
		#endregion

		#region KLD - Probability Gaussian Fit
		private double sigma = 4.28;
//		private double area = 5.576;
		private double mean = 5.0;
        // I don't know where these two values are from
		private double preProb = 2.08;
		private double preExp = 9.159;
		/*
		 * Function: 
		 * 1. constant function
		 * y = 1 if x > 0 and x <= 5;
		 * 2. Gaussian distribution function
		 * y = (area / (sigma * sqrt(PI / 2))) * exp(-2 * ((x - mean) / sigma)^2) if x > 5
		 * */
		/// <summary>
		/// Q Score from KLD
		/// </summary>
		/// <param name="interface1"></param>
		/// <param name="interface2"></param>
		/// <returns></returns>
		public double KLDQFunc_Gauss (InterfaceChains interface1, InterfaceChains interface2)
		{
			bool needReversed = false;
	//		string[] allResiduePairs = GetCommonResiduePairs (interface1, interface2, out needReversed);
			string[] allResiduePairs = GetAllResiduePairs (interface1, interface2, out needReversed);
			// no common residue pairs	
			if (needReversed)
			{
				interface2.Reverse ();
				allResiduePairs = GetAllResiduePairs (interface1, interface2);
			}

            Dictionary<string, double> probDistHash1 = null;
            Dictionary<string, double> probDistHash2 = null;
			double propSum1 = GetProbDistHash (interface1, allResiduePairs, ref probDistHash1, "GAUSS");
			double propSum2 = GetProbDistHash (interface2, allResiduePairs, ref probDistHash2, "GAUSS");
			
			double qScore = GetSimScoreForDistributions (probDistHash1, probDistHash2);

			// reverse back to original order
			if (needReversed)
			{
				interface2.Reverse ();
			}
			return qScore;
		}
		#endregion

		#region Compute KLD score	
		/// <summary>
		/// compute the similarity score between two distributions
		/// </summary>
		/// <param name="probHash1"></param>
		/// <param name="probHash2"></param>
		/// <returns></returns>
        private double GetSimScoreForDistributions(Dictionary<string, double> probHash1, Dictionary<string, double> probHash2)
		{
			double probSum1 = GetInterfaceProbSumScore (probHash1, probHash2);
			double probSum2 = GetInterfaceProbSumScore (probHash2, probHash1);
			return probSum1 + probSum2;
		}
		/// <summary>
		/// get a list of common residue pairs from two interfaces
		/// </summary>
		/// <param name="interface1"></param>
		/// <param name="interface2"></param>
		/// <returns></returns>
		private string[] GetAllResiduePairs (InterfaceChains interface1, InterfaceChains interface2, out bool needReversed)
		{
			needReversed = false;
			List<string> residuePairList = new List<string> ();
			int commonResidueCount = 0;
			int reversedCommonResidueCount = 0;
			List<string> seqIdList2 = new List<string> (interface2.seqDistHash.Keys);
			foreach (string seqIds in interface1.seqDistHash.Keys)
			{
				if (seqIdList2.Contains (seqIds))
				{					
					residuePairList.Add (seqIds);
					seqIdList2.Remove (seqIds);
					commonResidueCount ++;
				}
				else
				{
					double dist = interface2.CbetaDistForSeqIds (seqIds);					
					// no data available
					if (dist == -1)
					{
						continue;
					}
					residuePairList.Add (seqIds);
				}
				string[] seqFields = seqIds.Split ('_');
				if (seqIdList2.Contains (seqFields[1] + "_" + seqFields[0]))
				{
					reversedCommonResidueCount ++;
				}
			}
			// for left contacts in interface2
			foreach (string seqIds in seqIdList2)
			{
				double dist = interface1.CbetaDistForSeqIds (seqIds);
					
				// no data available
				if (dist == -1)
				{
					continue;
				}
				residuePairList.Add (seqIds);
			}
			if (reversedCommonResidueCount > commonResidueCount)
			{
				needReversed = true;
			}
			string[] residuePairs = new string [residuePairList.Count];
			residuePairList.CopyTo (residuePairs);
			return residuePairs;
		}

		/// <summary>
		/// get a list of common residue pairs from two interfaces
		/// </summary>
		/// <param name="interface1"></param>
		/// <param name="interface2"></param>
		/// <returns></returns>
		private string[] GetAllResiduePairs (InterfaceChains interface1, InterfaceChains interface2)
		{
			List<string> residuePairList = new List<string> ();
			List<string> seqIdList2 = new List<string> (interface2.seqDistHash.Keys);
			foreach (string seqIds in interface1.seqDistHash.Keys)
			{
				if (seqIdList2.Contains (seqIds))
				{					
					residuePairList.Add (seqIds);
					seqIdList2.Remove (seqIds);
				}
				else
				{
					double dist = interface2.CbetaDistForSeqIds (seqIds);					
					// no data available
					if (dist == -1)
					{
						continue;
					}
					residuePairList.Add (seqIds);
				}
			}
			// for left contacts in interface2
			foreach (string seqIds in seqIdList2)
			{
				double dist = interface1.CbetaDistForSeqIds (seqIds);
					
				// no data available
				if (dist == -1)
				{
					continue;
				}
				residuePairList.Add (seqIds);
			}
			string[] residuePairs = new string [residuePairList.Count];
			residuePairList.CopyTo (residuePairs);
			return residuePairs;
		}

		/// <summary>
		/// the sum of propabilities in an interface
		/// </summary>
		/// <param name="theInterface"></param>
		/// <param name="residuePairList"></param>
		/// <returns></returns>
		private double GetProbDistHash (InterfaceChains theInterface, string[] residuePairList,
            ref Dictionary<string, double> probDistHash, string method)
		{
			probDistHash = new Dictionary<string,double> ();
			double probSum = 0.0;
			double dist = 0.0;
			double prob = 0.0;
			foreach (string residuePair in residuePairList)
			{
				if (theInterface.seqDistHash.ContainsKey (residuePair))
				{
					dist = (double)theInterface.seqDistHash[residuePair];
				}
				else
				{
					dist = theInterface.CbetaDistForSeqIds (residuePair);
				}
				switch (method.ToUpper ())
				{
					case "KLD":
						prob = GetProbFromKld (dist);
						break;
					case "GAUSS":
						prob = GetProbFromGauss (dist);
						break;
					default:
						break;
				}
				if (prob > 1.0)
				{
					prob = 1.0;
				}
				if (prob > 0.0)
				{
					probDistHash.Add (residuePair, prob);
					probSum += prob;
				}
			}
			NormalizeProbDistHash (ref probDistHash, probSum);
			return probSum;
		}

		/// <summary>
		/// similarity score between two interfaces
		/// between propabilities of distances in interfaces
		/// </summary>
		/// <param name="propHash1"></param>
		/// <param name="propHash2"></param>
        private double GetInterfaceProbSumScore(Dictionary<string, double> probHash1, Dictionary<string, double> probHash2)
		{
			double sumProb = 0.0; 
			bool hasCommon = false;
			foreach (string seqString in probHash1.Keys)
			{
				double prob = (double)probHash1[seqString];
				if (probHash2.ContainsKey (seqString))
				{
					double temp = prob * Math.Log (prob / probHash2[seqString]);
					if (Double.IsInfinity (temp))
					{
						continue;
					}
					if (Double.IsNaN (temp))
					{
						continue;
					}
					sumProb += temp;
					hasCommon = true;
				}
			}
			if (! hasCommon)
			{
				return -1.0;
			}
			return sumProb;
		}

		/// <summary>
		/// similarity score between two interfaces (two distributions)
		/// </summary>
		/// <param name="propHash1"></param>
		/// <param name="propHash2"></param>
		/// <returns></returns>
        private double GetDistrSimScore(Dictionary<string, double> probHash1, Dictionary<string, double> probHash2)
		{
			double sumProb1 = GetInterfaceProbSumScore (probHash1, probHash2);
			double sumProb2 = GetInterfaceProbSumScore (probHash2, probHash1);
			//	return 1 - (sumProp1 + sumProp2);
			return sumProb1 + sumProb2;
		}

		/// <summary>
		/// normalized all probabilities so the sum is 1
		/// </summary>
		/// <param name="probDistHash"></param>
		/// <param name="probSum"></param>
        private void NormalizeProbDistHash(ref Dictionary<string, double> probDistHash, double probSum)
		{
			List<string> seqIdList = new List<string> (probDistHash.Keys);
			foreach (string seqStr in seqIdList)
			{
				probDistHash[seqStr] = (double)probDistHash[seqStr] / probSum;
			}
		}
		
		/// <summary>
		/// probability computed from Roland's description
		/// </summary>
		/// <param name="dist"></param>
		/// <returns></returns>
		private double GetProbFromKld (double dist)
		{
			double expValue = (dist - zeroNormValue) / (2 * sigma1 * sigma1);
			double prob = Math.Exp ((-1) *  expValue);

			return prob;
		}
		/// <summary>
		/// the probability given the distance
		/// probability = p(interface|dist)
		/// </summary>
		/// <param name="dist"></param>
		/// <returns></returns>
		private double GetProbFromGauss (double dist)
		{
			if (dist <= mean)
			{
                return 1.0;
			}
			else
			{
                // if we set prob = 1.0 when distance < atomic contact cutoff
                // then we should not multiply preProb from the Gaussian fit 
                // it should be normalized to 1 too. 
                // fixed on April 27, 2017
		//		return preProb * Math.Exp (-1 * Math.Pow ((dist - mean), 2.0) / preExp);
        //        return Math.Exp(-1 * Math.Pow((dist - mean), 2.0) / preExp);
                return Math.Exp(-2 * Math.Pow((dist - mean) / sigma, 2));
                // exp(-2 * ((x - mean) / sigma)^2)
			}
		}
		#endregion

		#region common contacts
		/// <summary>
		/// get normalized propability
		/// </summary>
		/// <param name="dist"></param>
		/// <param name="propSum"></param>
		/// <returns></returns>
		private double GetNormProp (double dist, double propSum)
		{
			double expValue = (dist - zeroNormValue) / (2 * sigma1 * sigma1);
			return Math.Exp ((-1) *  expValue) / propSum;
		}

		/// <summary>
		/// the sum of probabilities in the interface
		/// </summary>
		/// <param name="interfaceChain"></param>
		/// <returns></returns>
		private double GetPropSum (InterfaceChains interfaceChain)
		{
			double propSum = 0.0;
			foreach (string seqIds in interfaceChain.seqDistHash.Keys)
			{
				double expValue = ((double)interfaceChain.seqDistHash[seqIds] - zeroNormValue) / (2 * sigma1 * sigma1);
				propSum += Math.Exp ((-1) *  expValue);
			}
			return propSum;
		}

		/// <summary>
		/// the sum of probabilities for common contacts 
		/// from the input interface
		/// </summary>
		/// <param name="interace1"></param>
		/// <param name="interface2"></param>
		/// <returns></returns>
		private double GetPropSumInCommon (InterfaceChains theInterface, string[] commonResiduePairs)
		{
			double propSum = 0.0;
			foreach (string seqIds in commonResiduePairs)
			{
				double expValue = ((double)theInterface.seqDistHash[seqIds] - zeroNormValue) / (2 * sigma1 * sigma1);
				propSum += Math.Exp ((-1) *  expValue);
			}
			return propSum;
		}
		/// <summary>
		/// get a list of common residue pairs from two interfaces
		/// </summary>
		/// <param name="interface1"></param>
		/// <param name="interface2"></param>
		/// <returns></returns>
		private string[] GetCommonResiduePairs (InterfaceChains interface1, InterfaceChains interface2)
		{
			List<string> commonResiduePairList = new List<string> ();
			foreach (string seqIds in interface1.seqDistHash.Keys)
			{
				if (interface2.seqDistHash.ContainsKey (seqIds))
				{
					commonResiduePairList.Add (seqIds);
				}				
			}
			string[] commonResiduePairs = new string [commonResiduePairList.Count];
			commonResiduePairList.CopyTo (commonResiduePairs);
			return commonResiduePairs;
		}

		/// <summary>
		/// get a list of common residue pairs from two interfaces
		/// </summary>
		/// <param name="interface1"></param>
		/// <param name="interface2"></param>
		/// <returns></returns>
		private string[] GetCommonResiduePairs (InterfaceChains interface1, InterfaceChains interface2, out bool needReversed)
		{
			needReversed = false;
			List<string> commonResiduePairList = new List<string> ();
			List<string> reversedCommonResiduePairList = new List<string> ();
			foreach (string seqIds in interface1.seqDistHash.Keys)
			{
				if (interface2.seqDistHash.ContainsKey (seqIds))
				{
					commonResiduePairList.Add (seqIds);
				}	
				string[] seqIdFields = seqIds.Split ('_');
				string reversedSeqString = seqIdFields[1] + "_" + seqIdFields[0];
				if (interface2.seqDistHash.ContainsKey (reversedSeqString))
				{
					reversedCommonResiduePairList.Add (seqIds);
				}
			}
			if (reversedCommonResiduePairList.Count > commonResiduePairList.Count)
			{
				commonResiduePairList = reversedCommonResiduePairList;
				needReversed = true;
			}
			string[] commonResiduePairs = new string [commonResiduePairList.Count];
			commonResiduePairList.CopyTo (commonResiduePairs);
			return commonResiduePairs;
		}
		#endregion

		#region Count Q function
		/// <summary>
		/// 
		/// </summary>
		/// <param name="interChain1"></param>
		/// <param name="interChain2"></param>
		/// <returns></returns>
		public double CountQFunc (ChainContact contactInfo1, ChainContact contactInfo2)
		{
			int matchPairNum = 0;
			Dictionary<string, AtomPair> contactHash1 = contactInfo1.ChainContactInfo.atomContactHash;
            Dictionary<string, AtomPair> contactHash2 = contactInfo2.ChainContactInfo.atomContactHash;
			int totalPairNum = contactHash1.Count + contactHash2.Count;
			foreach (string seqIdPair in contactHash1.Keys)
			{
				if (contactHash2.ContainsKey (seqIdPair))
				{
					matchPairNum ++;
					// for those pairs in both structures
					// only count once
					totalPairNum --;
				}
			}
			if (totalPairNum == 0)
			{
				return -1.0;
			}
			return (double)matchPairNum / (double)totalPairNum;
		}
		#endregion

		#region Q for homodimers with possible domain swapping 
		private int numOfInterContacts = 5;
		/// <summary>
		/// only for identical sequences
		/// Q score for two interfaces with possible domain swapping
		/// We assume interface with domain swapping has larger surface area
		/// </summary>
		/// <param name="interfaceChain1">the interface with possible domain swapping, larger surface area</param>
		/// <param name="interfaceChain2">the interface with smaller surface area</param>
		/// <returns>Q</returns>
		public double ComputeQForDomainSwappingInterfaces (InterfaceChains interfaceChain1, InterfaceChains interfaceChain2, 
			string[] shiftSeqIdStrings)
		{
			if (! AreSameContactsInInterfaces (interfaceChain1, interfaceChain2))
			{
				return -1.0;
			}
			double weightDistSum = 0.0;
			double swapWeightDistSum = 0.0;
			double weightSum = 0.0;
			double weight = 0.0;
			double minDistanceDif = 0;
			double distIntra = 0.0;
			double dist1 = 0.0;
			double dist2 = 0.0;
            Dictionary<string, double> seqDistHash1 = interfaceChain1.seqDistHash;
            Dictionary<string, double> seqDistHash2 = new Dictionary<string, double>(interfaceChain2.seqDistHash);
			string reverseConKey = "";
			string[] seqIds = null;
			foreach (string conKey in seqDistHash1.Keys)
			{
				dist1 = seqDistHash1[conKey];
				seqIds = conKey.ToString ().Split ('_');
				reverseConKey = seqIds[1] + "_" + seqIds[0];

				if (seqDistHash2.ContainsKey (conKey))
				{
					dist2 = seqDistHash2[conKey];	
				}
					// should get the distance from the whole proteins
				else
				{
					dist2 = interfaceChain2.CbetaDistForSeqIds (conKey.ToString ());					
					// no data available
					if (dist2 == -1)
					{
						continue;
					}
				}			
				weight = GetProbFromGauss (Math.Min (dist1, dist2));
	/*			weightSum += weight;*/

				if (seqDistHash2.ContainsKey (conKey))
				{
					weightSum += weight;
					weightDistSum += weight * Math.Exp (Math.Abs (dist1 - dist2) * -0.5);
				}
				else if (Array.BinarySearch (shiftSeqIdStrings, conKey) > -1 
					|| Array.BinarySearch (shiftSeqIdStrings, reverseConKey) > -1)
				{
					distIntra =  GetIntraProtCbetaDistance (Convert.ToInt32 (seqIds[0]), 
						Convert.ToInt32 (seqIds[1]), interfaceChain2);
					minDistanceDif = GetMinDistanceDif (dist1, dist2, distIntra);
					swapWeightDistSum += weight * Math.Exp (minDistanceDif * -0.5);
				}
				else 
				{
					weightSum += weight;
					weightDistSum += weight * Math.Exp (Math.Abs (dist1 - dist2) * -0.5);			
				}
				// remove the matched one
				seqDistHash2.Remove (conKey);
			}
			// handle the remaining distances in disthash2
			// find corresponding distances from the whole proteins
			foreach (string seqIdPair in seqDistHash2.Keys)
			{
				dist2 = seqDistHash2[seqIdPair];
				
				dist1 = interfaceChain1.CbetaDistForSeqIds (seqIdPair.ToString ());
				
				// no data available
				if (dist1 == -1)
				{
					continue;
				}
				
				// dist2 must be the minimum since dist2 < cutoff and dist1 > cutoff
				weight = GetProbFromGauss (dist2);
				weightSum += weight;
				weightDistSum += weight * Math.Exp (Math.Abs (dist1 - dist2) * -0.5);
			}
			if (weightSum == 0)
			{
				return -1.0;
			}
			if (swapWeightDistSum == 0)
			{
				return -1.0;
			}
//			return (weightDistSum + swapWeightDistSum) / weightSum;
			return weightDistSum / weightSum;
		}

		private double GetMinDistanceDif (double dist1, double dist2, double dist2Intra)
		{
			double interDistDif = Math.Abs (dist1 - dist2);
			double intraDistDif = Math.Abs (dist1 - dist2Intra);
			return Math.Min (interDistDif, intraDistDif);
		}
		private double GetIntraProtCbetaDistance (int i, int j, InterfaceChains theInterface)
		{
			if (i == j)
			{
				return 0.0;
			}
			double dist = theInterface.GetIntraProtDistance (i, j);
			return dist;
		}

		private bool AreSameContactsInInterfaces (InterfaceChains interfaceChain1, InterfaceChains interfaceChain2)
		{
            Dictionary<string, double> seqDistHash1 = interfaceChain1.seqDistHash;
            Dictionary<string, double> seqDistHash2 = interfaceChain2.seqDistHash;
			int numOfContacts = 0;
			foreach (string seqIdString in seqDistHash1.Keys)
			{
				if (seqDistHash2.ContainsKey (seqIdString))
				{
					numOfContacts ++;
				}
			}
			if (numOfContacts > numOfInterContacts)
			{
				return true;
			}
			return false;
		}
		#endregion
	}
}
