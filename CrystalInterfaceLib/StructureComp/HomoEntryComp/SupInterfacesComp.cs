using System;
using System.Data;
using System.Collections.Generic;
using ProtCidSettingsLib;
using CrystalInterfaceLib.Contacts;
using CrystalInterfaceLib.Settings;

namespace CrystalInterfaceLib.StructureComp.HomoEntryComp
{
	/// <summary>
	/// Summary description for SupInterfacesCompPsiBlast.
	/// </summary>
	public class SupInterfacesComp : InterfacesComp
	{
		public SupInterfacesComp()
		{
		}
		
		#region Compare two interfaces by Dist-Weighted Q - superposed by Sequence alignment or structure alignment
		/// <summary>
		/// Similarity between two structures
		/// which need to be superposed
		/// 1. Superpose individual chains of structure1 to 
		/// inidividual chains of structure2 based on PSIBLAST sequence alignment
		/// including renaming the sequence id in second structure
		/// 2. apply Q function
		/// </summary>
		/// <param name="interChains1">structure1</param>
		/// <param name="interChains2">structure2</param>
		public InterfacePairInfo[] CompareSupStructures (InterfaceChains[] interfaceChains1, InterfaceChains[] interfaceChains2, DataTable alignInfoTable)
		{
            List<InterfacePairInfo> interfacesList = new List<InterfacePairInfo>();
			double identity = 1.0;
			bool isReverse = false;
			SuperpositionInterfaces supInterfaces = new SuperpositionInterfaces ();

            for (int i = 0; i < interfaceChains1.Length; i++)
            {
                for (int j = 0; j < interfaceChains2.Length; j++)
                {
                    try
                    {
                        isReverse = false;
                        identity = supInterfaces.SuperposeInterfaces
                            (interfaceChains1[i], interfaceChains2[j], alignInfoTable, isReverse);
                        if (identity < AppSettings.parameters.simInteractParam.identityCutoff)
                        {
                            identity = AppSettings.parameters.simInteractParam.identityCutoff;
                        }
                        // Q score
                        float qScore = (float)qFunc.WeightQFunc(interfaceChains1[i], interfaceChains2[j]);
                        supInterfaces.ReverseSupInterfaces(interfaceChains1[i], interfaceChains2[j], alignInfoTable, isReverse);
                        if (qScore >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
                        {
                            InterfacePairInfo interfacesInfo = new InterfacePairInfo((InterfaceInfo)interfaceChains1[i], (InterfaceInfo)interfaceChains2[j]);
                            interfacesInfo.qScore = qScore;
                            interfacesInfo.identity = identity;
                            interfacesInfo.isInterface2Reversed = false;
                            interfacesList.Add(interfacesInfo);
                        }
                        else
                        {
                            // try the other chain order
                            isReverse = true;
                            double reversedIdentity = supInterfaces.SuperposeInterfaces
                                (interfaceChains1[i], interfaceChains2[j], alignInfoTable, isReverse);
                            if (reversedIdentity < AppSettings.parameters.simInteractParam.identityCutoff)
                            {
                                reversedIdentity = AppSettings.parameters.simInteractParam.identityCutoff;
                            }
                            float reversedQScore = (float)qFunc.WeightQFunc(interfaceChains1[i], interfaceChains2[j]);
                            supInterfaces.ReverseSupInterfaces(interfaceChains1[i], interfaceChains2[j], alignInfoTable, isReverse);
                            if (reversedQScore >= qScore &&
                                reversedQScore > AppSettings.parameters.contactParams.minQScore)
                            {
                                InterfacePairInfo interfacesInfo = new InterfacePairInfo((InterfaceInfo)interfaceChains1[i], (InterfaceInfo)interfaceChains2[j]);
                                interfacesInfo.qScore = reversedQScore;
                                interfacesInfo.identity = reversedIdentity;
                                interfacesInfo.isInterface2Reversed = true;
                                interfacesList.Add(interfacesInfo);
                            }
                            else
                            {
                                if (qScore > AppSettings.parameters.contactParams.minQScore)
                                {
                                    InterfacePairInfo interfacesInfo = new InterfacePairInfo((InterfaceInfo)interfaceChains1[i], (InterfaceInfo)interfaceChains2[j]);
                                    interfacesInfo.qScore = qScore;
                                    interfacesInfo.identity = identity;
                                    interfacesInfo.isInterface2Reversed = false;
                                    interfacesList.Add(interfacesInfo);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue
                            (interfaceChains1[i].pdbId + "  " + interfaceChains2[j].pdbId +
                            "calculating Q error: " + ex.Message);
                    }
                }
            }

            return interfacesList.ToArray ();
		}

		/// <summary>
		/// Similarity between two structures
		/// which need to be superposed
		/// 1. Superpose individual chains of structure1 to 
		/// inidividual chains of structure2 based on PSIBLAST sequence alignment
		/// including renaming the sequence id in second structure
		/// 2. apply Q function
		/// </summary>
		/// <param name="interChains1">structure1</param>
		/// <param name="interChains2">structure2</param>
		public InterfacePairInfo[] CompareSupStructuresAll (InterfaceChains[] interfaceChains1, InterfaceChains[] interfaceChains2, DataTable alignInfoTable)
		{
			List<InterfacePairInfo> interfacesList = new List<InterfacePairInfo> ();
			double identity = 1.0;
			bool isReverse = false;
			SuperpositionInterfaces supInterfaces = new SuperpositionInterfaces ();			
			
			for (int i = 0; i < interfaceChains1.Length; i ++)
			{
				for (int j = 0; j < interfaceChains2.Length; j++)
				{
					try
					{
						//		interface2 = new InterfaceChains (interfaceChains2[j]);
						isReverse = false;
						identity = supInterfaces.SuperposeInterfaces 
							(interfaceChains1[i], interfaceChains2[j], alignInfoTable, isReverse);
						if (identity < AppSettings.parameters.simInteractParam.identityCutoff)
						{
							identity = AppSettings.parameters.simInteractParam.identityCutoff;
						}
						// Q score
						//		identity = GetInterfaceIdentity (interface1, interface2, alignInfoTable);
						float qScore = (float)qFunc.WeightQFunc (interfaceChains1[i], interfaceChains2[j]);	
						supInterfaces.ReverseSupInterfaces(interfaceChains1[i], interfaceChains2[j], alignInfoTable, isReverse);
						if (qScore >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
						{
							InterfacePairInfo interfacesInfo = new InterfacePairInfo ((InterfaceInfo)interfaceChains1[i], (InterfaceInfo)interfaceChains2[j]);
							interfacesInfo.qScore = qScore;
							interfacesInfo.identity = identity;
                            interfacesInfo.isInterface2Reversed = false;
							interfacesList.Add (interfacesInfo);
						}
						else
						{	
							// try the other chain order
							isReverse = true;
							//	interface2.Clear ();
							//	interface2 = new InterfaceChains (interfaceChains2[j]);
							double reversedIdentity = supInterfaces.SuperposeInterfaces 
								(interfaceChains1[i], interfaceChains2[j], alignInfoTable, isReverse);
							if (reversedIdentity < AppSettings.parameters.simInteractParam.identityCutoff)
							{
								reversedIdentity = AppSettings.parameters.simInteractParam.identityCutoff;
							}
							float reversedQScore = (float)qFunc.WeightQFunc (interfaceChains1[i], interfaceChains2[j]);
							supInterfaces.ReverseSupInterfaces (interfaceChains1[i], interfaceChains2[j], alignInfoTable, isReverse);
							if (reversedQScore >= qScore)
							{
								InterfacePairInfo interfacesInfo = new InterfacePairInfo ((InterfaceInfo)interfaceChains1[i], (InterfaceInfo)interfaceChains2[j]);
								interfacesInfo.qScore = reversedQScore;
								interfacesInfo.identity = reversedIdentity;
                                interfacesInfo.isInterface2Reversed = true;
								interfacesList.Add (interfacesInfo);
							}
							else
							{
								// go back to the original chain order
								//		interface2.Reverse ();
								InterfacePairInfo interfacesInfo = new InterfacePairInfo ((InterfaceInfo)interfaceChains1[i], (InterfaceInfo)interfaceChains2[j]);
								interfacesInfo.qScore = qScore;
								interfacesInfo.identity = identity;
                                interfacesInfo.isInterface2Reversed = false;
								interfacesList.Add (interfacesInfo);
							}
						}
					}
					catch (Exception ex)
					{
                        ProtCidSettings.progressInfo.progStrQueue.Enqueue
                            (interfaceChains1[i].pdbId + "  " + interfaceChains2[j].pdbId +
                            "calculating Q error: " + ex.Message);
					}
				}	
			}

            return interfacesList.ToArray ();
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="interfaceChains1"></param>
		/// <param name="interfaceChains2"></param>
		/// <param name="alignRow"></param>
		/// <returns></returns>
		public InterfacePairInfo CompareSupStructures (InterfaceChains interface1, InterfaceChains interface2, DataTable alignInfoTable)
		{
            InterfacePairInfo interfacePairInfo = new InterfacePairInfo((InterfaceInfo)interface1, (InterfaceInfo)interface2);
			SuperpositionInterfaces supInterface = new SuperpositionInterfaces ();	
			bool isReverse = false;

			double identity = supInterface.SuperposeInterfaces (interface1, interface2, alignInfoTable, isReverse);

			// Q score
			float qScore = (float)qFunc.WeightQFunc (interface1, interface2);
            // change the sequence number back.
            supInterface.ReverseSupInterfaces(interface1, interface2, alignInfoTable, isReverse);

            if (qScore >= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
            {
                interfacePairInfo.qScore = qScore;
                interfacePairInfo.identity = identity;
                interfacePairInfo.isInterface2Reversed = false;
            }
            else
            {
                isReverse = true;
                double reversedIdentity = supInterface.SuperposeInterfaces(interface1, interface2, alignInfoTable, isReverse);
                if (reversedIdentity < AppSettings.parameters.simInteractParam.identityCutoff)
                {
                    reversedIdentity = AppSettings.parameters.simInteractParam.identityCutoff;
                }
                float reversedQScore = (float)qFunc.WeightQFunc(interface1, interface2);
                supInterface.ReverseSupInterfaces(interface1, interface2, alignInfoTable, isReverse);
                if (reversedQScore >= qScore)
                {
                    interfacePairInfo.qScore = reversedQScore;
                    interfacePairInfo.identity = reversedIdentity;
                    interfacePairInfo.isInterface2Reversed = true;
                }
                else
                {
                    // go back to the original chain order
                    //		interface2.Reverse ();
                    interfacePairInfo.qScore = qScore;
                    interfacePairInfo.identity = identity;
                    interfacePairInfo.isInterface2Reversed = false;
                }
            }
		
			return interfacePairInfo;
		}
		#endregion

		#region Comare two interfaces by KLD 
		/// <summary>
		/// Similarity between two structures
		/// which need to be superposed
		/// 1. Superpose individual chains of structure1 to 
		/// inidividual chains of structure2 based on PSIBLAST sequence alignment
		/// including renaming the sequence id in second structure
		/// 2. apply KLD Q function
		/// </summary>
		/// <param name="interChains1">structure1</param>
		/// <param name="interChains2">structure2</param>
		public InterfacePairInfo[] CompareSupStructuresByKld (InterfaceChains[] interfaceChains1, InterfaceChains[] interfaceChains2, DataTable alignInfoTable)
		{
			List<InterfacePairInfo> interfacesList = new List<InterfacePairInfo> ();
		
			SuperpositionInterfaces supInterfaces = new SuperpositionInterfaces ();			
			supInterfaces.SuperposeInterfaces (interfaceChains2, alignInfoTable);

			for (int i = 0; i < interfaceChains1.Length; i ++)
			{
				for (int j = 0; j < interfaceChains2.Length; j++)
				{
					// Q score
					float qScore = (float)qFunc.KldQFunc (interfaceChains1[i], interfaceChains2[j]);	
					if (qScore <= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
					{
						InterfacePairInfo interfacesInfo = new InterfacePairInfo ((InterfaceInfo)interfaceChains1[i], (InterfaceInfo)interfaceChains2[j]);
						interfacesInfo.qScore = qScore;
						interfacesList.Add (interfacesInfo);
					}
					else
					{
						// try the other chain order
						interfaceChains2[j].Reverse ();
						float reversedQScore = (float)qFunc.KldQFunc (interfaceChains1[i], interfaceChains2[j]);
						if (reversedQScore < qScore)
						{
							InterfacePairInfo interfacesInfo = new InterfacePairInfo ((InterfaceInfo)interfaceChains1[i], (InterfaceInfo)interfaceChains2[j]);
							interfacesInfo.qScore = reversedQScore;
							interfacesList.Add (interfacesInfo);
						}
						else
						{
							// go back to the original chain order
							interfaceChains2[j].Reverse ();
							InterfacePairInfo interfacesInfo = new InterfacePairInfo ((InterfaceInfo)interfaceChains1[i], (InterfaceInfo)interfaceChains2[j]);
							interfacesInfo.qScore = qScore;
							interfacesList.Add (interfacesInfo);
						}
					}
				}	
			}
			// change the sequence number back.
			supInterfaces.ReverseSupInterfaces (interfaceChains2, alignInfoTable);

            return interfacesList.ToArray ();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="interfaceChains1"></param>
		/// <param name="interfaceChains2"></param>
		/// <param name="alignRow"></param>
		/// <returns></returns>
		public InterfacePairInfo CompareSupStructuresByKld (InterfaceChains interface1, InterfaceChains interface2, DataTable alignInfoTable)
		{	
			InterfacePairInfo interfacePairInfo = null;
			SuperpositionInterfaces supInterfaces = new SuperpositionInterfaces ();			
			supInterfaces.SuperposeInterfaces (interface2, alignInfoTable);

			// Q score
			float qScore = (float)qFunc.KldQFunc (interface1, interface2);					
			if (qScore <= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
			{
				interfacePairInfo = new InterfacePairInfo ((InterfaceInfo)interface1, (InterfaceInfo)interface2);
				interfacePairInfo.qScore = qScore;
			}		
			// change the sequence number back.
			supInterfaces.ReverseSupInterfaces (interface2, alignInfoTable);

			return interfacePairInfo;
		}
		#endregion

		#region Comare two interfaces by KLD -- Gaussian Fit Function
		/// <summary>
		/// Similarity between two structures
		/// which need to be superposed
		/// 1. Superpose individual chains of structure1 to 
		/// inidividual chains of structure2 based on PSIBLAST sequence alignment
		/// including renaming the sequence id in second structure
		/// 2. apply KLD Q function
		/// </summary>
		/// <param name="interChains1">structure1</param>
		/// <param name="interChains2">structure2</param>
		public InterfacePairInfo[] CompareSupStructuresByKldGaussFunc (InterfaceChains[] interfaceChains1, InterfaceChains[] interfaceChains2, DataTable alignInfoTable)
		{
			List<InterfacePairInfo> interfacesList = new List<InterfacePairInfo> ();
		
			SuperpositionInterfaces supInterfaces = new SuperpositionInterfaces ();			
			supInterfaces.SuperposeInterfaces(interfaceChains2, alignInfoTable);

			for (int i = 0; i < interfaceChains1.Length; i ++)
			{
				for (int j = 0; j < interfaceChains2.Length; j++)
				{
					// Q score
					float qScore = (float)qFunc.KLDQFunc_Gauss (interfaceChains1[i], interfaceChains2[j]);	
			//		if (qScore <= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
			//		{
						InterfacePairInfo interfacesInfo = new InterfacePairInfo ((InterfaceInfo)interfaceChains1[i], (InterfaceInfo)interfaceChains2[j]);
						interfacesInfo.qScore = qScore;
						interfacesList.Add (interfacesInfo);
			/*		}
					else
					{
						// try the other chain order
						interfaceChains2[j].Reverse ();
						float reversedQScore = (float)qFunc.KLDQFunc_Gauss (interfaceChains1[i], interfaceChains2[j]);
						if (reversedQScore < qScore)
						{
							InterfacePairInfo interfacesInfo = new InterfacePairInfo ((InterfaceInfo)interfaceChains1[i], (InterfaceInfo)interfaceChains2[j]);
							interfacesInfo.qScore = reversedQScore;
							interfacesList.Add (interfacesInfo);
						}
						else
						{
							// go back to the original chain order
							interfaceChains2[j].Reverse ();
							InterfacePairInfo interfacesInfo = new InterfacePairInfo ((InterfaceInfo)interfaceChains1[i], (InterfaceInfo)interfaceChains2[j]);
							interfacesInfo.qScore = qScore;
							interfacesList.Add (interfacesInfo);
						}
					}*/
				}	
			}
			// change the sequence number back.
			supInterfaces.ReverseSupInterfaces (interfaceChains2, alignInfoTable);

            return interfacesList.ToArray ();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="interfaceChains1"></param>
		/// <param name="interfaceChains2"></param>
		/// <param name="alignRow"></param>
		/// <returns></returns>
		public InterfacePairInfo CompareSupStructuresByKldGaussFunc (InterfaceChains interface1, 
			InterfaceChains interface2, DataTable alignInfoTable)
		{	
			InterfacePairInfo interfacePairInfo = null;
			SuperpositionInterfaces supInterfaces = new SuperpositionInterfaces ();			
			supInterfaces.SuperposeInterfaces (interface2, alignInfoTable);

			// Q score
			float qScore = (float)qFunc.KLDQFunc_Gauss (interface1, interface2);					
			if (qScore <= AppSettings.parameters.simInteractParam.interfaceSimCutoff)
			{
				interfacePairInfo = new InterfacePairInfo ((InterfaceInfo)interface1, (InterfaceInfo)interface2);
				interfacePairInfo.qScore = qScore;
			}		
			// change the sequence number back.
			supInterfaces.ReverseSupInterfaces (interface2, alignInfoTable);

			return interfacePairInfo;
		}
		#endregion
    }
}
