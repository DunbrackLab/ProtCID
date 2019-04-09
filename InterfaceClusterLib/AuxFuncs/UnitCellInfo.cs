using System;

namespace InterfaceClusterLib.AuxFuncs
{
	/// <summary>
	/// Summary description for UnitCellInfo.
	/// </summary>
	public class UnitCellInfo
	{
		public string pdbId;
		public string spaceGroup;
		public double length_a;
		public double length_b;
		public double length_c;
		public double angle_alpha;
		public double angle_beta;
		public double angle_gamma;
		public static double length_dev = 0.01;

		public UnitCellInfo()
		{
			
		}	
	
		public UnitCellInfo (string pdbId, double length_a, double length_b, double length_c,
			double angle_alpha, double angle_beta, double angle_gamma)
		{
			double[] lengthList = new double [3];
			lengthList[0] = length_a;
			lengthList[1] = length_b;
			lengthList[2] = length_c;
			double[] angleList = new double [3];
			angleList[0] = angle_alpha;
			angleList[1] = angle_beta;
			angleList[2] = angle_gamma;
			// sort the lengthes
			SortCellParams (ref lengthList, ref angleList);

			this.pdbId = pdbId;
			this.length_a = lengthList[0];
			this.length_b = lengthList[1];
			this.length_c = lengthList[2];
			this.angle_alpha = angleList[0];
			this.angle_beta = angleList[1];
			this.angle_gamma = angleList[2];
		}

		public void SetUnitCellInfo (string pdbId, double length_a, double length_b, double length_c,
			double angle_alpha, double angle_beta, double angle_gamma)
		{
			this.pdbId = pdbId;
			this.length_a = length_a;
			this.length_b = length_b;
			this.length_c = length_c;
			this.angle_alpha = angle_alpha;
			this.angle_beta = angle_beta;
			this.angle_gamma = angle_gamma;
		}

		/// <summary>
		/// change space group string based on unit cell lengthes
		/// biggest length is on X axis,
		/// right-handed circle permutation: x y z; y z x; z x y
		/// </summary>
		/// <param name="spaceGroup"></param>
		/// <param name="lengthList"></param>
		/// <param name="angleList"></param>
		/// <returns></returns>
		public UnitCellInfo (string pdbId, string sgString, double length_a, double length_b, 
			double length_c, double angle_alpha, double angle_beta, double angle_gamma)
		{
			string[] sgFields = sgString.Split (' ');
			double[] lengthList = new double [3];
			lengthList[0] = length_a;
			lengthList[1] = length_b;
			lengthList[2] = length_c;
			double maxLen = lengthList[0];
			int maxIdx = 0;
			for (int i = 1; i < lengthList.Length; i ++)
			{
				if (maxLen < lengthList[i])
				{
					maxLen = lengthList[i];
					maxIdx = i;
				}
			}
			
			if (maxIdx == 1 && sgFields.Length == 4) // y z x
			{
				this.spaceGroup = sgFields[0] + " " + sgFields[2] + " " + sgFields[3] + " " + sgFields[1];
				this.length_a = length_b;
				this.length_b = length_c;
				this.length_c = length_a;
				this.angle_alpha = angle_beta;
				this.angle_beta = angle_gamma;
				this.angle_gamma = angle_alpha;
			}
			else if (maxIdx == 2 && sgFields.Length == 4) // z x y
			{
				this.spaceGroup = sgFields[0] + " " + sgFields[3] + " " + sgFields[1] + " " + sgFields[2];	
				this.length_a = length_c;
				this.length_b = length_a;
				this.length_c = length_b;
				this.angle_alpha = angle_gamma;
				this.angle_beta = angle_alpha;
				this.angle_gamma = angle_beta;
			}
			else   // space group dimensions don't need to be changed, x y z
			{
                this.spaceGroup = sgString;
				this.length_a = length_a;
				this.length_b = length_b;
				this.length_c = length_c;
				this.angle_alpha = angle_alpha;
				this.angle_beta = angle_beta;
				this.angle_gamma = angle_gamma;
			}
		}
		/// <summary>
		/// bubble sort unit cell parameters
		/// </summary>
		/// <param name="lengthList"></param>
		/// <param name="angleList"></param>
		private void SortCellParams (ref double[] lengthList, ref double[] angleList)
		{
			for (int i = 0; i < lengthList.Length - 1; i ++)
			{
				for (int j = i + 1; j < lengthList.Length; j ++)
				{
					if (lengthList[j] > lengthList[i])
					{
						double length = lengthList[i];
						lengthList[i] = lengthList[j];
						lengthList[j] = length;
						double angle = angleList[i];
						angleList[i] = angleList[j];
						angleList[j] = angle;
					}
				}
			}
		}
/*
		/// <summary>
		/// bubble sort unit cell parameters
		/// </summary>
		/// <param name="lengthList"></param>
		/// <param name="angleList"></param>
		private void SortCellParams (ref double[] lengthList, ref double[] angleList)
		{
			for (int i = 0; i < angleList.Length - 1; i ++)
			{
				for (int j = i + 1; j < angleList.Length; j ++)
				{
					if (angleList[j] > angleList[i])
					{
						double angle = angleList[i];
						angleList[i] = angleList[j];
						angleList[j] = angle;
						double length = lengthList[i];
						lengthList[i] = lengthList[j];
						lengthList[j] = length;
					}
				}
			}
		}
*/
		/// <summary>
		/// if three lengths are within the ranges of the other unit cell
		/// consider them to be same
		/// otherwise different
		/// </summary>
		/// <param name="unitCell1"></param>
		/// <param name="unitCell2"></param>
		/// <returns></returns>
		public bool AreTwoUnitCellsSame (UnitCellInfo unitCell2)
		{
			if (IsInRange (this.length_a, unitCell2.length_a ) &&
				IsInRange (this.length_b, unitCell2.length_b) &&
				IsInRange (this.length_c, unitCell2.length_c) &&
				IsInRange (this.angle_alpha, unitCell2.angle_alpha) &&
				IsInRange (this.angle_beta, unitCell2.angle_beta) &&
				IsInRange (this.angle_gamma, unitCell2.angle_gamma))
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// the value within the range of the second one
		/// </summary>
		/// <param name="length1"></param>
		/// <param name="length2"></param>
		/// <returns></returns>
		private bool IsInRange (double length1, double length2)
		{            
			if (length1 > length2 * (1.0 + length_dev) || length1 < length2 * (1.0 - length_dev))
			{
				return false;
			}
			return true;
		}
	}
}
