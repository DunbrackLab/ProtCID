using System;

namespace XtalLib.KDops
{
	/// <summary>
	/// Summary description for KDops.
	/// </summary>
	public class KVector
	{
		public static int baseDim = 3;
		private int k = 0;
		private double [ , ] vectors = null;
		public KVector()
		{
			k = 3;
			DefineKdops (k);
		}

		public KVector (int kdir)
		{
			k = kdir;
			DefineKdops (k);
		}

		/// <summary>
		/// property of KVectors
		/// </summary>
		public double [ , ] Vectors
		{
			get
			{
				return vectors;
			}
			set
			{
				vectors = value;
			}
		}

		public int K 
		{
			get
			{
				return k;
			}
			set
			{
				k = value;
			}
		}
		/// <summary>
		/// define the vectors for kDops
		/// </summary>
		/// <param name="kdir"></param>
		private void DefineKdops(int kdir)
		{
			switch (kdir)
			{
				case 7:
					vectors = new double [7, 3] { {1, 0, 0}, {0, 1, 0}, {0, 0, 1}, 
												  {1, 1, 1}, {1, -1, 1}, {1, 1, -1}, {1, -1, -1} };
					break;	

				case 9:
					vectors = new double [9, 3] { {1, 0, 0}, {0, 1, 0}, {0, 0, 1}, 
							{1, 1, 0}, {1, 0, 1}, {0, 1, 1}, {1, -1, 0}, {1, 0, -1}, {0, 1, -1} };					
					break;

				case 13:
					vectors = new double [13, 3] { {1, 0, 0}, {0, 1, 0}, {0, 0, 1}, 
												{1, 1, 1}, {1, -1, 1}, {1, 1, -1}, {1, -1, -1},
							{1, 1, 0}, {1, 0, 1}, {0, 1, 1}, {1, -1, 0}, {1, 0, -1}, {0, 1, -1} };
					break;

				default:
					vectors = new double [3, 3] { {1, 0, 0}, {0, 1, 0}, {0, 0, 1}};					
					break;	
			}
		}


		/// <summary>
		/// a matrix with k*3 multiply a 3d point (3*1)
		/// return an array double k * 1
		/// </summary>
		/// <param name="vectors"></param>
		/// <param name="point3d"></param>
		/// <returns></returns>
		public static double [] operator * (KVector kVector, double [] point3d)
		{
			double [] projectedValues = new double [(int)(kVector.Vectors.Length / baseDim)];
			if (kVector.Vectors.Length % baseDim != 0)
			{
				throw new Exception ("Matrix dimensions not matched. ");
			}
			if (point3d.Length != baseDim)
			{
				throw new Exception ("Input point must be 3D.");
			}
			for (int row = 0; row < (int)((double)kVector.Vectors.Length / (double)baseDim); row ++)
			{
				double multSum = 0.0;
				double sqrSum = 0.0;
				for (int col = 0; col < baseDim; col ++)
				{
					multSum += kVector.Vectors[row, col] * point3d[col];
					sqrSum += (kVector.Vectors[row, col] * kVector.Vectors[row, col]);
				}
				projectedValues[row] = multSum / Math.Sqrt (sqrSum);
			}
			return projectedValues;
		}

	}
}
