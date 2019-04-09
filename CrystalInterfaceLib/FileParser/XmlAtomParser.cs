using System;
using System.Data;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using CrystalInterfaceLib.Crystal;

namespace CrystalInterfaceLib.FileParser
{
	/// <summary>
	/// Summary description for PdbXmlAtomParser.
	/// </summary>
	public class XmlAtomParser
	{
		public XmlAtomParser()
		{
			
		}

		/// <summary>
		/// parse one XML file
		/// </summary>
		/// <param name="thisXmlFile"></param>
		public void ParseXmlFile(string thisXmlFile, ref EntryCrystal entryCrystal, string atomType)
		{
			int xmlIndex = thisXmlFile.LastIndexOf ("\\");
			// 4 character for Pdb entry ID
			string pdbId = thisXmlFile.Substring (xmlIndex + 1, 4);
			entryCrystal.PdbId = pdbId;

			// <PDBx:struct_biol_genCategory>
			// the category to generate the biological units

			// <PDBx:atom_siteCategory>
			// the coordinates of atoms, 

			// <PDBx:cellCategory>
			// the cystal1 record

			// <PDBx:struct_ncs_oper>
			// non-crystalgraphic symmetry operators
			
			XmlDocument xmlDoc = new XmlDocument();
			try
			{
				xmlDoc.Load (thisXmlFile);
				// Create an XmlNamespaceManager for resolving namespaces.
				XmlNamespaceManager nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
                string xmlNameSpace = xmlDoc.DocumentElement.Attributes["xmlns:PDBx"].InnerText;
                nsManager.AddNamespace("PDBx", xmlNameSpace);
			//	nsManager.AddNamespace("PDBx", "http://deposit.pdb.org/pdbML/pdbx.xsd");

				// if there are protein chains, no, return
				///// parse polymer type of an entity
				bool hasProtein = false;
                Dictionary<int, EntityInfo> entityInfoHash = ParseEntityInfoCategory(ref xmlDoc, ref entryCrystal.entityCat, ref nsManager, out hasProtein);
				if (! hasProtein)
				{
					entryCrystal = null;
					return ;
				}		

				///////////////
				// parse atom_sitescategory <PDBx:fract_transf_matrix11>
				ParseFractTransfMatrix(ref xmlDoc, ref entryCrystal.scaleCat, ref nsManager);
				
				//////////////
				// Parse Cryst1 record
				ParseCryst1 (ref xmlDoc, ref entryCrystal.crystal1, ref nsManager);

				///////////////
				// Parse PDBx:struct_biol_genCategory
				ParseBuGenCategory(ref xmlDoc, ref entryCrystal.buGenCat, ref nsManager);

				///////////////
				// Parse atom 
				ParseAtoms(ref xmlDoc, ref entryCrystal.atomCat, ref nsManager, entityInfoHash, atomType);
			
				//////////////
				// Parse NCS struct
				ParseNcsStruct (ref xmlDoc, ref entryCrystal.ncsCat, ref nsManager);
				
			}
			catch (Exception ex)
			{
				throw new Exception (string.Format ("Parse {0} Errors: {1}", pdbId, ex.Message));
			}
		}

        /// <summary>
        /// parse one XML file
        /// </summary>
        /// <param name="thisXmlFile"></param>
        public EntryCrystal ParseXmlFile(string thisXmlFile)
        {
            EntryCrystal entryCrystal = new EntryCrystal();
            int xmlIndex = thisXmlFile.LastIndexOf("\\");
            // 4 character for Pdb entry ID
            string pdbId = thisXmlFile.Substring(xmlIndex + 1, 4);
            entryCrystal.PdbId = pdbId;

            // <PDBx:struct_biol_genCategory>
            // the category to generate the biological units

            // <PDBx:atom_siteCategory>
            // the coordinates of atoms, 

            // <PDBx:cellCategory>
            // the cystal1 record

            // <PDBx:struct_ncs_oper>
            // non-crystalgraphic symmetry operators

            XmlDocument xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.Load(thisXmlFile);
                // Create an XmlNamespaceManager for resolving namespaces.
                XmlNamespaceManager nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
                string xmlNameSpace = xmlDoc.DocumentElement.Attributes["xmlns:PDBx"].InnerText;
                nsManager.AddNamespace("PDBx", xmlNameSpace);
                //	nsManager.AddNamespace("PDBx", "http://deposit.pdb.org/pdbML/pdbx.xsd");

                // if there are protein chains, no, return
                ///// parse polymer type of an entity
                Dictionary<int, EntityInfo> entityInfoHash = ParseEntityInfoCategory(ref xmlDoc, ref entryCrystal.entityCat, ref nsManager);

                ///////////////
                // parse atom_sitescategory <PDBx:fract_transf_matrix11>
                ParseFractTransfMatrix(ref xmlDoc, ref entryCrystal.scaleCat, ref nsManager);

                //////////////
                // Parse Cryst1 record
                ParseCryst1(ref xmlDoc, ref entryCrystal.crystal1, ref nsManager);

                ///////////////
                // Parse PDBx:struct_biol_genCategory
                ParseBuGenCategory(ref xmlDoc, ref entryCrystal.buGenCat, ref nsManager);

                ///////////////
                // Parse atom 
                ParseAllAtoms (ref xmlDoc, ref entryCrystal.atomCat, ref nsManager, entityInfoHash);

                //////////////
                // Parse NCS struct
                ParseNcsStruct(ref xmlDoc, ref entryCrystal.ncsCat, ref nsManager);

            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Parse {0} Errors: {1}", pdbId, ex.Message));
            }
            return entryCrystal;
        }

		#region parse cryst1
        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <param name="crystalInfo"></param>
        /// <param name="nsManager"></param>
		private void ParseCryst1(ref XmlDocument xmlDoc, ref CrystalInfo crystalInfo, ref XmlNamespaceManager nsManager)
		{
			// get cell information: Length, Angle, and Z_PDB
			string cellInfoNodePath = "descendant::PDBx:cellCategory/PDBx:cell";
			XmlNode cellInfoNode = xmlDoc.DocumentElement.SelectSingleNode (cellInfoNodePath, nsManager);
			if (cellInfoNode != null)
			{
				XmlNodeList cellInfoNodeList = cellInfoNode.ChildNodes;
                double cellVal = 0.0;
				foreach (XmlNode cellNode in cellInfoNodeList)
				{
					string nodeName = cellNode.Name.ToLower();
                    if (cellNode.InnerText == "")
                    {
                        cellVal = -1.0;
                    }
                    else
                    {
                        cellVal = System.Convert.ToDouble(cellNode.InnerText);
                    }
					switch (nodeName)
					{
						case "pdbx:length_a":
							crystalInfo.length_a = cellVal;
							break;
						case "pdbx:length_b":
                            crystalInfo.length_b = cellVal;
							break;
						case "pdbx:length_c":
                            crystalInfo.length_c = cellVal;
							break;
						case "pdbx:angle_alpha":
                            crystalInfo.angle_alpha = cellVal;
							break;
						case "pdbx:angle_beta":
                            crystalInfo.angle_beta = cellVal;
							break;
						case "pdbx:angle_gamma":
                            crystalInfo.angle_gamma = cellVal;
							break;
						case "pdbx:z_pdb":
                            crystalInfo.zpdb = (int)cellVal;
							break;
					}
				}
			}
			XmlNode spaceGroupNode = xmlDoc.DocumentElement.SelectSingleNode("descendant::PDBx:symmetryCategory/PDBx:symmetry/PDBx:space_group_name_H-M", nsManager);
			if (spaceGroupNode == null)
			{
				crystalInfo.spaceGroup = "-";
			}
			else
			{
				crystalInfo.spaceGroup = spaceGroupNode.InnerText.ToString ();
			}
		}
		#endregion

        #region parse atom
        /// <summary>
        /// parse the coordinate of C alphas 
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <param name="calphaInfoHash"></param>
        /// <param name="nsManager"></param>
        private void ParseAtoms(ref XmlDocument xmlDoc, ref AtomCategory atomCat, ref XmlNamespaceManager nsManager, Dictionary<int, EntityInfo> entityInfoHash, string retrievingAtomType)
        {
            XmlNodeList atomNodeList = xmlDoc.DocumentElement.SelectNodes("descendant::PDBx:atom_siteCategory/PDBx:atom_site", nsManager);
            int atomId = 0;
            string asymId = "";
            string preAsymId = "";
            int preEntityId = -1;
            string preResidue = "";
            int entityId = -1;
            string residue = "";
            string authResidue = "";
            string seqId = "";
            string authSeqId = "";
            double cartnX = 0.0;
            double cartnY = 0.0;
            double cartnZ = 0.0;
            string atomType = "-";
            string atomName = "-";
            string bfactor = "";
            string occupancy = "";
            int modelNum = 1;
            int firstModelNum = -9999;
            int heterResidueNum = 0;
            //      string polymerType = "";

            ChainAtoms chainAtoms = new ChainAtoms();
            List<AtomInfo> atomList = new List<AtomInfo> ();
            bool isAtomNeeded = false;
            Dictionary<int, List<string>> entityAsymIdHash = new Dictionary<int,List<string>> ();

            foreach (XmlNode atomNode in atomNodeList)
            {
                isAtomNeeded = false;
                atomId = System.Convert.ToInt32(atomNode.Attributes[0].InnerText.ToString());
                XmlNodeList atomInfoNodeList = atomNode.ChildNodes;
                bfactor = "0.00";
                occupancy = "1.00";
                foreach (XmlNode atomInfoNode in atomInfoNodeList)
                {
                    if (atomInfoNode.Name.ToLower() == "pdbx:type_symbol")
                    {
                        atomType = atomInfoNode.InnerText;
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:b_iso_or_equiv")
                    {
                        bfactor = atomInfoNode.InnerText;
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:occupancy")
                    {
                        occupancy = atomInfoNode.InnerText;
                        continue;
                    }

                    if (atomInfoNode.Name.ToLower() == "pdbx:label_atom_id")
                    {
                        atomName = atomInfoNode.InnerText;
                        if (retrievingAtomType == "CA" || retrievingAtomType == "CB")
                        {
                            if (atomInfoNode.InnerText.ToUpper() != retrievingAtomType)
                            {
                                isAtomNeeded = false;
                                break;
                            }
                            else
                            {
                                isAtomNeeded = true;
                                continue;
                            }
                        }
                        else if (retrievingAtomType == "CA_CB")
                        {
                            if (atomInfoNode.InnerText.ToUpper() != "CA" &&
                                atomInfoNode.InnerText.ToUpper() != "CB")
                            {
                                isAtomNeeded = false;
                                break;
                            }
                            else
                            {
                                isAtomNeeded = true;
                                continue;
                            }
                        }
                        else
                        {
                            isAtomNeeded = true;
                        }
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:pdbx_pdb_model_num")
                    {
                        modelNum = Convert.ToInt16(atomInfoNode.InnerText);
                        if (firstModelNum == -9999) // the first atom
                        {
                            firstModelNum = modelNum;
                        }
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:label_comp_id")
                    {
                        residue = atomInfoNode.InnerText;
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:auth_comp_id")
                    {
                        authResidue = atomInfoNode.InnerText;
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:label_asym_id")
                    {
                        asymId = atomInfoNode.InnerText;
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:label_entity_id")
                    {
                        entityId = Convert.ToInt16 (atomInfoNode.InnerText);
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:label_seq_id")
                    {
                        seqId = atomInfoNode.InnerText;
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:auth_seq_id")
                    {
                        authSeqId = atomInfoNode.InnerText;
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:cartn_x")
                    {
                        cartnX = System.Convert.ToDouble(atomInfoNode.InnerText);
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:cartn_y")
                    {
                        cartnY = System.Convert.ToDouble(atomInfoNode.InnerText);
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:cartn_z")
                    {
                        cartnZ = System.Convert.ToDouble(atomInfoNode.InnerText);
                        continue;
                    }
                }
                if (preAsymId != asymId && preAsymId != "" && atomList.Count > 0)
                {
                    chainAtoms.AsymChain = preAsymId;
                    chainAtoms.EntityID = preEntityId;
                    if (entityInfoHash.ContainsKey(preEntityId))
                    {
                        chainAtoms.PolymerType = ((EntityInfo)entityInfoHash[preEntityId]).type;
                    }
                    else
                    {
                        chainAtoms.PolymerType = "-";
                    }
                    AtomInfo[] atomArray = new AtomInfo[atomList.Count];
                    atomList.CopyTo(atomArray);
                    chainAtoms.CartnAtoms = atomArray;
                    atomCat.AddChainAtoms(chainAtoms);
                    atomList = new List<AtomInfo> ();
                    chainAtoms = new ChainAtoms();
                    heterResidueNum = 0;
                    preResidue = "";
                    if (entityAsymIdHash.ContainsKey(preEntityId))
                    {
                        entityAsymIdHash[preEntityId].Add(preAsymId);
                    }
                    else
                    {
                        List<string> asymIdList = new List<string> ();
                        asymIdList.Add(preAsymId);
                        entityAsymIdHash.Add(preEntityId, asymIdList);
                    }
                }
      //          if (modelNum > 1) // only pick up the model with model number 1
                if (modelNum != firstModelNum) // start different model
                {
                    break;
                }
                if (isAtomNeeded && residue.ToUpper() != "HOH")
                {
                    if (seqId == "")
                    {
                        if (preResidue != residue)
                        {
                            heterResidueNum++;
                        }
                        seqId = heterResidueNum.ToString();
                    }
                    AtomInfo atomInfo = new AtomInfo();
                    atomInfo.atomId = atomId;
                    atomInfo.atomType = atomType;
                    atomInfo.atomName = atomName;
                    atomInfo.seqId = seqId;
                    atomInfo.authSeqId = authSeqId;
                    atomInfo.residue = residue;
                    atomInfo.authResidue = authResidue;
                    atomInfo.xyz.X = cartnX;
                    atomInfo.xyz.Y = cartnY;
                    atomInfo.xyz.Z = cartnZ;
                    atomInfo.occupancy = occupancy;
                    atomInfo.bfactor = bfactor;
                    atomList.Add(atomInfo);
                }
                preAsymId = asymId;
                preEntityId = entityId;
                preResidue = residue;
            }
            // add the last one
            if (atomList.Count > 0)
            {
                chainAtoms.AsymChain = asymId;
                chainAtoms.EntityID = entityId;
                if (entityInfoHash.ContainsKey(entityId))
                {
                    chainAtoms.PolymerType = ((EntityInfo)entityInfoHash[entityId]).type;
                }
                else
                {
                    chainAtoms.PolymerType = "-";
                }
                AtomInfo[] atomArray = new AtomInfo[atomList.Count];
                atomList.CopyTo(atomArray);
                chainAtoms.CartnAtoms = atomArray;
                atomCat.AddChainAtoms(chainAtoms);

                if (entityAsymIdHash.ContainsKey(entityId))
                {
                    entityAsymIdHash[preEntityId].Add(asymId);
                }
                else
                {
                    List<string> asymIdList = new List<string> ();
                    asymIdList.Add(asymId);
                    entityAsymIdHash.Add(entityId, asymIdList);
                }
            }
            foreach (int keyEntityId in entityAsymIdHash.Keys)
            {
                if (entityInfoHash.ContainsKey(keyEntityId))
                {
                    EntityInfo entityInfo = (EntityInfo)entityInfoHash[keyEntityId];

                    entityInfo.asymChains = FormatChainList(entityAsymIdHash[keyEntityId]);
                }
            }
        }

        /// <summary>
        /// parse the coordinate of all atoms in the XML file, including water
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <param name="calphaInfoHash"></param>
        /// <param name="nsManager"></param>
        private void ParseAllAtoms(ref XmlDocument xmlDoc, ref AtomCategory atomCat, ref XmlNamespaceManager nsManager, Dictionary<int, EntityInfo> entityInfoHash)
        {
            XmlNodeList atomNodeList = xmlDoc.DocumentElement.SelectNodes("descendant::PDBx:atom_siteCategory/PDBx:atom_site", nsManager);
            int atomId = 0;
            string asymId = "";
            string preAsymId = "";
            int preEntityId = -1;
            string preResidue = "";
            int entityId = -1;
            string residue = "";
            string authResidue = "";
            string seqId = "";
            string authSeqId = "";
            double cartnX = 0.0;
            double cartnY = 0.0;
            double cartnZ = 0.0;
            string atomType = "-";
            string atomName = "-";
            int modelNum = 1;
            int firstModelNum = -9999;
            int heterResidueNum = 0;

            ChainAtoms chainAtoms = new ChainAtoms();
            List<AtomInfo> atomList =  new List<AtomInfo> ();
            Dictionary<int, List<string>> entityAsymIdHash = new Dictionary<int,List<string>> ();

            foreach (XmlNode atomNode in atomNodeList)
            {
                atomId = System.Convert.ToInt32(atomNode.Attributes[0].InnerText.ToString());
                XmlNodeList atomInfoNodeList = atomNode.ChildNodes;
                foreach (XmlNode atomInfoNode in atomInfoNodeList)
                {
                    if (atomInfoNode.Name.ToLower() == "pdbx:type_symbol")
                    {
                        atomType = atomInfoNode.InnerText;
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:label_atom_id")
                    {
                        atomName = atomInfoNode.InnerText;
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:pdbx_pdb_model_num")
                    {
                        modelNum = Convert.ToInt16(atomInfoNode.InnerText);
                        if (firstModelNum == -9999) // the first atom
                        {
                            firstModelNum = modelNum;
                        }
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:label_comp_id")
                    {
                        residue = atomInfoNode.InnerText;
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:auth_comp_id")
                    {
                        authResidue = atomInfoNode.InnerText;
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:label_asym_id")
                    {
                        asymId = atomInfoNode.InnerText;
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:label_entity_id")
                    {
                        entityId = Convert.ToInt16(atomInfoNode.InnerText);
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:label_seq_id")
                    {
                        seqId = atomInfoNode.InnerText;
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:auth_seq_id")
                    {
                        authSeqId = atomInfoNode.InnerText;
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:cartn_x")
                    {
                        cartnX = System.Convert.ToDouble(atomInfoNode.InnerText);
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:cartn_y")
                    {
                        cartnY = System.Convert.ToDouble(atomInfoNode.InnerText);
                        continue;
                    }
                    if (atomInfoNode.Name.ToLower() == "pdbx:cartn_z")
                    {
                        cartnZ = System.Convert.ToDouble(atomInfoNode.InnerText);
                        continue;
                    }
                }
                if (preAsymId != asymId && preAsymId != "" && atomList.Count > 0)
                {
                    chainAtoms.AsymChain = preAsymId;
                    chainAtoms.EntityID = preEntityId;
                    if (entityInfoHash.ContainsKey(preEntityId))
                    {
                        chainAtoms.PolymerType = ((EntityInfo)entityInfoHash[preEntityId]).type;
                    }
                    else
                    {
                        chainAtoms.PolymerType = "-";
                    }
                    AtomInfo[] atomArray = new AtomInfo[atomList.Count];
                    atomList.CopyTo(atomArray);
                    chainAtoms.CartnAtoms = atomArray;
                    atomCat.AddChainAtoms(chainAtoms);
                    atomList = new List<AtomInfo> ();
                    chainAtoms = new ChainAtoms();
                    heterResidueNum = 0;
                    preResidue = "";
                    if (entityAsymIdHash.ContainsKey(preEntityId))
                    {
                        entityAsymIdHash[preEntityId].Add(preAsymId);
                    }
                    else
                    {
                        List<string> asymIdList = new List<string> ();
                        asymIdList.Add(preAsymId);
                        entityAsymIdHash.Add(preEntityId, asymIdList);
                    }
                }
                //          if (modelNum > 1) // only pick up the model with model number 1
                if (modelNum != firstModelNum) // start different model
                {
                    break;
                }
                
                if (seqId == "")
                {
                    if (preResidue != residue)
                    {
                        heterResidueNum++;
                    }
                    seqId = heterResidueNum.ToString();
                }
                AtomInfo atomInfo = new AtomInfo();
                atomInfo.atomId = atomId;
                atomInfo.atomType = atomType;
                atomInfo.atomName = atomName;
                atomInfo.seqId = seqId;
                atomInfo.authSeqId = authSeqId;
                atomInfo.residue = residue;
                atomInfo.authResidue = authResidue;
                atomInfo.xyz.X = cartnX;
                atomInfo.xyz.Y = cartnY;
                atomInfo.xyz.Z = cartnZ;
                atomList.Add(atomInfo);

                preAsymId = asymId;
                preEntityId = entityId;
                preResidue = residue;
            }
            // add the last one
            if (atomList.Count > 0)
            {
                chainAtoms.AsymChain = asymId;
                chainAtoms.EntityID = entityId;
                if (entityInfoHash.ContainsKey(entityId))
                {
                    chainAtoms.PolymerType = ((EntityInfo)entityInfoHash[entityId]).type;
                }
                else
                {
                    chainAtoms.PolymerType = "-";
                }
                AtomInfo[] atomArray = new AtomInfo[atomList.Count];
                atomList.CopyTo(atomArray);
                chainAtoms.CartnAtoms = atomArray;
                atomCat.AddChainAtoms(chainAtoms);

                if (entityAsymIdHash.ContainsKey(entityId))
                {
                    entityAsymIdHash[preEntityId].Add(asymId);
                }
                else
                {
                    List<string> asymIdList = new List<string> ();
                    asymIdList.Add(asymId);
                    entityAsymIdHash.Add(entityId, asymIdList);
                }
            }
            foreach (int keyEntityId in entityAsymIdHash.Keys)
            {
                if (entityInfoHash.ContainsKey(keyEntityId))
                {
                    EntityInfo entityInfo = (EntityInfo)entityInfoHash[keyEntityId];
                    entityInfo.asymChains = FormatChainList(entityAsymIdHash[keyEntityId]);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainList"></param>
        /// <returns></returns>
        private string FormatChainList(List<string> chainList)
        {
            string listChains = "";
            foreach (string chainId in chainList)
            {
                listChains += chainId + ",";
            }
            return listChains.TrimEnd(',');
        }
        #endregion

        #region parse BuGen category
        /// <summary>
        /// retrieve data from Bu_gen category in XML file
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <param name="buGenHash"></param>
        /// <param name="nsManager"></param>
        private void ParseBuGenCategory(ref XmlDocument xmlDoc, ref BuGenCategory buGenCat, ref XmlNamespaceManager nsManager)
        {
            BuGenStruct buGenStruct = null;

            XmlNode buGenCatNode = xmlDoc.DocumentElement.SelectSingleNode("descendant::PDBx:struct_biol_genCategory", nsManager);
            if (buGenCatNode != null)
            {
                foreach (XmlNode buGenNode in buGenCatNode.ChildNodes)
                {
                    buGenStruct = new BuGenStruct();
                    buGenStruct.biolUnitId = buGenNode.Attributes["biol_id"].InnerText;
                    buGenStruct.asymId = buGenNode.Attributes["asym_id"].InnerText.ToString();
          //          buGenStruct.symmetryString = buGenNode.Attributes["symmetry"].InnerText.ToString();
                    XmlNodeList fullSymNodeList = buGenNode.ChildNodes;
                    string fullSymStr = "";
                    foreach (XmlNode fullSymNode in fullSymNodeList)
                    {
                        if (fullSymNode.Name.ToLower() == "pdbx_full_symmetry_operation")
                        {
                            fullSymStr = fullSymNode.InnerText.ToString();
                            break;
                        }
                    }
         //           buGenStruct.symmetryMatrix = fullSymStr;
                    buGenCat.AddBuGenStruct(buGenStruct);
                }
            }
            else
            {
                XmlNode assemblyCatNode = xmlDoc.DocumentElement.SelectSingleNode("descendant::PDBx:pdbx_struct_assemblyCategory", nsManager);
                if (assemblyCatNode != null)
                {
                    GetAssemblySumInfo(assemblyCatNode, ref buGenCat, nsManager);
                    XmlNode symOperCatNode = xmlDoc.DocumentElement.SelectSingleNode("descendant::PDBx:pdbx_struct_oper_listCategory", nsManager);
                    if (symOperCatNode != null)
                    {
                        GetSymOpMatrixHash(symOperCatNode, ref buGenCat);
                        buGenCatNode = xmlDoc.DocumentElement.SelectSingleNode("descendant::PDBx:pdbx_struct_assembly_genCategory", nsManager);
                        if (buGenCatNode != null)
                        {
                            ParseAssemblyGenInfo(buGenCatNode, ref buGenCat);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <param name="buGenCat"></param>
        /// <param name="nsManager"></param>
        private void GetAssemblySumInfo(XmlNode assemblyCatNode, ref BuGenCategory buGenCat, XmlNamespaceManager nsManager)
        {
            BuStatusInfo buStatusInfo = null;
            if (assemblyCatNode != null)
            {
                foreach (XmlNode assemblyNode in assemblyCatNode.ChildNodes)
                {
                    buStatusInfo = new BuStatusInfo();
                    if (assemblyNode.Attributes["id"] != null)
                    {
                        buStatusInfo.biolUnitId = assemblyNode.Attributes["id"].Value;
                    }
                    if (assemblyNode["PDBx:details"] != null)
                    {
                        buStatusInfo.details = assemblyNode["PDBx:details"].InnerText;
                    }
                    if (assemblyNode["PDBx:method_details"] != null)
                    {
                        buStatusInfo.method_details = assemblyNode["PDBx:method_details"].InnerText;
                    }
                    if (assemblyNode["PDBx:oligomeric_details"] != null)
                    {
                        buStatusInfo.oligomeric_details = assemblyNode["PDBx:oligomeric_details"].InnerText;
                    }
                    buGenCat.AddBuStatusInfo(buStatusInfo);
                }
            }
        }

        /// <summary>
        /// the asymmetric chains and the symmetry operators for each assembly
        /// </summary>
        /// <param name="assemblyGenCatNode"></param>
        /// <param name="biolUnitAsymCountHash"></param>
        /// <param name="buSymOpStrings"></param>
        private void ParseAssemblyGenInfo(XmlNode assemblyGenCatNode, ref BuGenCategory buGenCat/*, Hashtable symOperHash*/)
        {
            string[] asymChains = null;
            string[] symOpers = null;
            string assemblyId = "";
            foreach (XmlNode assemblyGenNode in assemblyGenCatNode.ChildNodes)
            {
                assemblyId = assemblyGenNode.Attributes["assembly_id"].Value;
                asymChains = assemblyGenNode.Attributes["asym_id_list"].Value.Split(',');
                //         symOpers = assemblyGenNode.Attributes["oper_expression"].Value.Split (',');
                symOpers = GetSymOpers(assemblyGenNode.Attributes["oper_expression"].Value);
                foreach (string asymChain in asymChains)
                {
                    foreach (string symOper in symOpers)
                    {
                        BuGenStruct buGenStruct = new BuGenStruct();
                        buGenStruct.biolUnitId = assemblyId;
                        buGenStruct.asymId = asymChain;
                  //      buGenStruct.symmetryMatrix = symOpInfo.symmetryMatrix;
                        buGenStruct.symOperId = symOper;
                 //       buGenStruct.symmetryString = symOpInfo.symmetryString;
                        buGenCat.AddBuGenStruct(buGenStruct);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="operExpression"></param>
        /// <returns></returns>
        private string[] GetSymOpers(string operExpression)
        {
            // 2r6p
            /*       <PDBx:pdbx_struct_assembly_genCategory>
            <PDBx:pdbx_struct_assembly_gen assembly_id="1" asym_id_list="A,B,C,D,E,F,G" oper_expression="(1-60)"></PDBx:pdbx_struct_assembly_gen>
            <PDBx:pdbx_struct_assembly_gen assembly_id="2" asym_id_list="A,B,C,D,E,F,G" oper_expression="1"></PDBx:pdbx_struct_assembly_gen>
            <PDBx:pdbx_struct_assembly_gen assembly_id="3" asym_id_list="A,B,C,D,E,F,G" oper_expression="(1-5)"></PDBx:pdbx_struct_assembly_gen>
            <PDBx:pdbx_struct_assembly_gen assembly_id="4" asym_id_list="A,B,C,D,E,F,G" oper_expression="(1,2,6,10,23,24)"></PDBx:pdbx_struct_assembly_gen>
            <PDBx:pdbx_struct_assembly_gen assembly_id="PAU" asym_id_list="A,B,C,D,E,F,G" oper_expression="P"></PDBx:pdbx_struct_assembly_gen>
               
             * 3cji
             <PDBx:pdbx_struct_assembly_genCategory>
      <PDBx:pdbx_struct_assembly_gen assembly_id="1" asym_id_list="A,B,C,D,E,F,G,H,I" oper_expression="(1-60)"></PDBx:pdbx_struct_assembly_gen>
      <PDBx:pdbx_struct_assembly_gen assembly_id="2" asym_id_list="A,B,C,D,E,F,G,H,I" oper_expression="1"></PDBx:pdbx_struct_assembly_gen>
      <PDBx:pdbx_struct_assembly_gen assembly_id="3" asym_id_list="A,B,C,D,E,F,G,H,I" oper_expression="(1-5)"></PDBx:pdbx_struct_assembly_gen>
      <PDBx:pdbx_struct_assembly_gen assembly_id="4" asym_id_list="A,B,C,D,E,F,G,H,I" oper_expression="(1,2,6,10,23,24)"></PDBx:pdbx_struct_assembly_gen>
      <PDBx:pdbx_struct_assembly_gen assembly_id="PAU" asym_id_list="A,B,C,D,E,F,G,H,I" oper_expression="P"></PDBx:pdbx_struct_assembly_gen>
      <PDBx:pdbx_struct_assembly_gen assembly_id="XAU" asym_id_list="A,B,C,D,E,F,G,H,I" oper_expression="(X0)(1-5,11-15,21-25,36-40)"></PDBx:pdbx_struct_assembly_gen>
      <PDBx:pdbx_struct_assembly_gen assembly_id="XAU" asym_id_list="A,B,C,D,E,F,G,H,I" oper_expression="(X1)(1-5,11-15,21-25,36-40)"></PDBx:pdbx_struct_assembly_gen>
   </PDBx:pdbx_struct_assembly_genCategory>
             */
            /// remove "(" and ")", replaced by ","
            if (operExpression.IndexOf("(") > -1)
            {
                string tempExpression = "";
                foreach (char ch in operExpression)
                {
                    if (ch == '(') // remove "("
                    {
                        continue;
                    }
                    if (ch == ')') // replace ")" by ","
                    {
                        tempExpression += ",";
                        continue;
                    }
                    tempExpression += ch.ToString();
                }
                operExpression = tempExpression.TrimEnd(',');
            }

            string[] fields = operExpression.Split(',');
            List<string> symOperList = new List<string> ();
            foreach (string field in fields)
            {
                if (field.IndexOf("-") > -1)
                {
                    string[] items = field.Split('-');
                    int startPos = Convert.ToInt16(items[0]);
                    int endPos = Convert.ToInt16(items[1]);
                    for (int i = startPos; i <= endPos; i++)
                    {
                        symOperList.Add(i.ToString());
                    }
                }
                else
                {
                    symOperList.Add(field);
                }
            }
            string[] symOpers = new string[symOperList.Count];
            symOperList.CopyTo(symOpers);
            return symOpers;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="operCatNode"></param>
        /// <returns></returns>
        private void GetSymOpMatrixHash(XmlNode operCatNode, ref BuGenCategory buGenCat)
        {
 //           Hashtable symOperHash = new Hashtable();
            string symOpId = "";
            foreach (XmlNode operNode in operCatNode.ChildNodes)
            {
                symOpId = operNode.Attributes["id"].Value;

                SymOpMatrix symOpMatrix = new SymOpMatrix();
                symOpMatrix.symmetryOpNum = symOpId;

                if (operNode["PDBx:name"] != null)
                {
                    symOpMatrix.symmetryString = operNode["PDBx:name"].InnerText;
                }
                else
                {
                    symOpMatrix.symmetryString = "-";
                }

                symOpMatrix.Add (0, 0, Convert.ToDouble (operNode["PDBx:matrix11"].InnerText));
                symOpMatrix.Add (0, 1, Convert.ToDouble (operNode["PDBx:matrix12"].InnerText));
                symOpMatrix.Add (0, 2, Convert.ToDouble (operNode["PDBx:matrix13"].InnerText));
                symOpMatrix.Add (0, 3, Convert.ToDouble (operNode["PDBx:vector1"].InnerText));
                symOpMatrix.Add (1, 0, Convert.ToDouble (operNode["PDBx:matrix21"].InnerText));
                symOpMatrix.Add (1, 1, Convert.ToDouble (operNode["PDBx:matrix22"].InnerText));
                symOpMatrix.Add (1, 2, Convert.ToDouble (operNode["PDBx:matrix23"].InnerText));
                symOpMatrix.Add (1, 3, Convert.ToDouble (operNode["PDBx:vector2"].InnerText));
                symOpMatrix.Add (2, 0, Convert.ToDouble (operNode["PDBx:matrix31"].InnerText));
                symOpMatrix.Add (2, 1, Convert.ToDouble (operNode["PDBx:matrix32"].InnerText));
                symOpMatrix.Add (2, 2, Convert.ToDouble (operNode["PDBx:matrix33"].InnerText));
                symOpMatrix.Add(2, 3, Convert.ToDouble(operNode["PDBx:vector3"].InnerText));

                buGenCat.AddBuSymMatrix(symOpMatrix);
            }
        }
        #endregion

		#region parse polymer type
		/// <summary>
		/// get polymer type for each entity number
		/// </summary>
		/// <param name="entityPolyCategoryNode"></param>
		/// <param name="entityIdPolyTypeHash"></param>
		private Dictionary<int, EntityInfo> ParseEntityInfoCategory(ref XmlDocument xmlDoc, ref EntityInfoCategory entityInfoCat, ref XmlNamespaceManager nsManager, out bool hasProtein)
		{
            Dictionary<int, EntityInfo> entityInfoHash = new Dictionary<int, EntityInfo>();
			bool entryHasProtein = false;
			// get polymer type for each entity number
			// Polytype can be polypeptide, polydeoxyribonucleotide, polyribonucleotide or polysaccharide
			XmlNode entityPolyCategoryNode = xmlDoc.DocumentElement.SelectSingleNode("descendant::PDBx:entity_polyCategory", nsManager);
            string entityPolyType = "";
            string oneLetterSeq = "";
            string authorChains = "";
			if (entityPolyCategoryNode != null)
			{
				XmlNodeList entityPolyNodeList = entityPolyCategoryNode.ChildNodes;				
				foreach (XmlNode entityPolyNode in entityPolyNodeList)
				{
                    entityPolyType = "";
					int entityId = Convert.ToInt32 (entityPolyNode.Attributes["entity_id"].InnerText.ToString ());
                    foreach (XmlNode polyTypeNode in entityPolyNode.ChildNodes)
                    {
                        if (polyTypeNode.Name.ToLower()== "pdbx:type")
                        {
                            entityPolyType = polyTypeNode.InnerText;
                        }
                        if (polyTypeNode.Name.ToLower().IndexOf("pdbx:pdbx_seq_one_letter_code") > -1)
                        {
                            oneLetterSeq = polyTypeNode.InnerText;
                        }
                        if (polyTypeNode.Name.ToLower().IndexOf("pdbx:pdbx_strand_id") > -1)
                        {
                            authorChains = polyTypeNode.InnerText;
                        }
                    }
					if (entityPolyType.Length == 0)
					{
						entityPolyType = "-";
					}
					if (entityPolyType.ToLower ().IndexOf ("polypeptide") > -1)
					{
						entityPolyType = "polypeptide";
						entryHasProtein = true;
					}
                    EntityInfo entityInfo = new EntityInfo();
                    entityInfo.authorChains = authorChains;
                    entityInfo.entityId = entityId;
                    entityInfo.type = entityPolyType;
                    entityInfo.oneLetterSeq = oneLetterSeq;
					if (! entityInfoHash.ContainsKey (entityId))
					{
                        entityInfoHash.Add(entityId, entityInfo);
					}
				}
			}
            // add the name for entity
            Dictionary<int, string> entityNameHash = GetEntityNameHash(ref xmlDoc, ref nsManager);
            List<int> entityList = new List<int> (entityInfoHash.Keys);
            entityList.Sort();
            foreach (int entityId in entityList)
            {
                EntityInfo entityInfo = (EntityInfo)entityInfoHash[entityId];
                if (entityNameHash.ContainsKey(entityId))
                {
                    entityInfo.name = (string)entityNameHash[entityId];
                }
                else
                {
                    entityInfo.name = "-";
                }
            }

            Dictionary<int, string> entitySeqHash = GetEntityThreeLetterSequences(ref xmlDoc, ref nsManager);
            foreach (int entityId in entityList)
            {
                EntityInfo entityInfo = (EntityInfo)entityInfoHash[entityId];
                if (entitySeqHash.ContainsKey(entityId))
                {
                    entityInfo.threeLetterSeq = (string)entitySeqHash[entityId];
                }
                else
                {
                    entityInfo.threeLetterSeq = "-";
                }
                entityInfoCat.Add(entityInfo);
            }
            if (entryHasProtein)
            {
                Dictionary<string, AsymInfo> nonpolyAsymIdInfoHash = ParseEntityNonPolymerCategory(ref xmlDoc, ref nsManager);
                Dictionary<int, EntityInfo> nonpolyEntityInfoHash = new Dictionary<int,EntityInfo> ();
                int nonPolyEntityId = 0;
                foreach (string asymId in nonpolyAsymIdInfoHash.Keys)
                {
                    AsymInfo asymInfo = (AsymInfo)nonpolyAsymIdInfoHash[asymId];
                    nonPolyEntityId = Convert.ToInt32(asymInfo.entity);
                    if (nonpolyEntityInfoHash.ContainsKey(nonPolyEntityId))
                    {
                        EntityInfo entityInfo = (EntityInfo)nonpolyEntityInfoHash[nonPolyEntityId];
                        entityInfo.asymChains = entityInfo.asymChains + "," + asymId;
                        entityInfo.authorChains = entityInfo.authorChains + "," + asymInfo.strandId;
                        nonpolyEntityInfoHash[nonPolyEntityId] = entityInfo;
                    }
                    else
                    {
                        EntityInfo nonpolyEntityInfo = new EntityInfo();
                        nonpolyEntityInfo.entityId = nonPolyEntityId;
                        nonpolyEntityInfo.authorChains = asymInfo.strandId;
                        nonpolyEntityInfo.asymChains = asymId;
                        nonpolyEntityInfo.oneLetterSeq = "";
                        nonpolyEntityInfo.threeLetterSeq = asymInfo.residueSeq;
                        nonpolyEntityInfo.name = "";
                        nonpolyEntityInfo.type = "non-polymer";
                        nonpolyEntityInfoHash.Add(nonPolyEntityId, nonpolyEntityInfo);
                    }
                }
                foreach (int nonpolyEntity in nonpolyEntityInfoHash.Keys)
                {
                    entityInfoCat.Add(nonpolyEntityInfoHash[nonpolyEntity]);
                }
            }
			hasProtein = entryHasProtein;
            return entityInfoHash;
		}

        /// <summary>
        /// get polymer type for each entity number
        /// </summary>
        /// <param name="entityPolyCategoryNode"></param>
        /// <param name="entityIdPolyTypeHash"></param>
        private Dictionary<int, EntityInfo> ParseEntityInfoCategory(ref XmlDocument xmlDoc, ref EntityInfoCategory entityInfoCat, ref XmlNamespaceManager nsManager)
        {
            Dictionary<int, EntityInfo> entityInfoHash = new Dictionary<int,EntityInfo> ();
            // get polymer type for each entity number
            // Polytype can be polypeptide, polydeoxyribonucleotide, polyribonucleotide or polysaccharide
            XmlNode entityPolyCategoryNode = xmlDoc.DocumentElement.SelectSingleNode("descendant::PDBx:entity_polyCategory", nsManager);
            string entityPolyType = "";
            string oneLetterSeq = "";
            string authorChains = "";
            if (entityPolyCategoryNode != null)
            {
                XmlNodeList entityPolyNodeList = entityPolyCategoryNode.ChildNodes;
                foreach (XmlNode entityPolyNode in entityPolyNodeList)
                {
                    entityPolyType = "";
                    int entityId = Convert.ToInt32(entityPolyNode.Attributes["entity_id"].InnerText.ToString());
                    foreach (XmlNode polyTypeNode in entityPolyNode.ChildNodes)
                    {
                        if (polyTypeNode.Name.ToLower().IndexOf("pdbx:type") > -1)
                        {
                            entityPolyType = polyTypeNode.InnerText;
                        }
                        if (polyTypeNode.Name.ToLower().IndexOf("pdbx:pdbx_seq_one_letter_code") > -1)
                        {
                            oneLetterSeq = polyTypeNode.InnerText;
                        }
                        if (polyTypeNode.Name.ToLower().IndexOf("pdbx:pdbx_strand_id") > -1)
                        {
                            authorChains = polyTypeNode.InnerText;
                        }
                    }
                    if (entityPolyType.Length == 0)
                    {
                        entityPolyType = "-";
                    }
                    if (entityPolyType.ToLower().IndexOf("polypeptide") > -1)
                    {
                        entityPolyType = "polypeptide";
                    }
                    EntityInfo entityInfo = new EntityInfo();
                    entityInfo.authorChains = authorChains;
                    entityInfo.entityId = entityId;
                    entityInfo.type = entityPolyType;
                    entityInfo.oneLetterSeq = oneLetterSeq;
                    if (!entityInfoHash.ContainsKey(entityId))
                    {
                        entityInfoHash.Add(entityId, entityInfo);
                    }
                }
            }
            // add the name for entity
            Dictionary<int, string> entityNameHash = GetEntityNameHash(ref xmlDoc, ref nsManager);
            foreach (int entityId in entityInfoHash.Keys)
            {
                entityInfoHash[entityId].name = (string)entityNameHash[entityId];
            }

            Dictionary<int, string> entitySeqHash = GetEntityThreeLetterSequences(ref xmlDoc, ref nsManager);
            foreach (int entityId in entityInfoHash.Keys)
            {
                entityInfoHash[entityId].threeLetterSeq = entitySeqHash[entityId];
                entityInfoCat.Add(entityInfoHash[entityId]);
            }

            Dictionary<string, AsymInfo> nonpolyAsymIdInfoHash = ParseEntityNonPolymerCategory(ref xmlDoc, ref nsManager);
            Dictionary<int, EntityInfo> nonpolyEntityInfoHash = new Dictionary<int,EntityInfo> ();
            int nonPolyEntityId = 0;
            foreach (string asymId in nonpolyAsymIdInfoHash.Keys)
            {
                AsymInfo asymInfo = (AsymInfo)nonpolyAsymIdInfoHash[asymId];
                nonPolyEntityId = Convert.ToInt32(asymInfo.entity);
                if (nonpolyEntityInfoHash.ContainsKey(nonPolyEntityId))
                {
                    EntityInfo entityInfo = nonpolyEntityInfoHash[nonPolyEntityId];
                    entityInfo.asymChains = entityInfo.asymChains + "," + asymId;
                    entityInfo.authorChains = entityInfo.authorChains + "," + asymInfo.strandId;
                    nonpolyEntityInfoHash[nonPolyEntityId] = entityInfo;
                }
                else
                {
                    EntityInfo nonpolyEntityInfo = new EntityInfo();
                    nonpolyEntityInfo.entityId = nonPolyEntityId;
                    nonpolyEntityInfo.authorChains = asymInfo.strandId;
                    nonpolyEntityInfo.asymChains = asymId;
                    nonpolyEntityInfo.oneLetterSeq = "";
                    nonpolyEntityInfo.threeLetterSeq = asymInfo.residueSeq;
                    nonpolyEntityInfo.name = "";
                    nonpolyEntityInfo.type = "non-polymer";
                    nonpolyEntityInfoHash.Add(nonPolyEntityId, nonpolyEntityInfo);
                }
            }
            foreach (int nonpolyEntity in nonpolyEntityInfoHash.Keys)
            {
                entityInfoCat.Add(nonpolyEntityInfoHash[nonpolyEntity]);
            }

            return entityInfoHash;
        }

        /// <summary>
        /// the name for each entity
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <param name="nsManager"></param>
        /// <returns></returns>
        private Dictionary<int, string> GetEntityNameHash(ref XmlDocument xmlDoc, ref XmlNamespaceManager nsManager)
        {
            Dictionary<int, string> entityIdNameHash = new Dictionary<int, string>();
            XmlNode nameCategoryNode = xmlDoc.DocumentElement.SelectSingleNode("descendant::PDBx:entity_name_comCategory", nsManager);
            if (nameCategoryNode != null)
            {
                XmlNodeList entityNameNodeList = nameCategoryNode.ChildNodes;
                string name = "";
                foreach (XmlNode entityNameNode in entityNameNodeList)
                {
                    int entityId = Convert.ToInt32(entityNameNode.Attributes["entity_id"].InnerText);
                    name = "";
                    if (entityNameNode["PDBx:name"] != null)
                    {
                        name = entityNameNode["PDBx:name"].InnerText.ToUpper();
                    }
                    if (!entityIdNameHash.ContainsKey(entityId))
                    {
                        entityIdNameHash.Add(entityId, name);
                    }
                }
            }
            else
            {
                XmlNode entityCategoryNode = xmlDoc.DocumentElement.SelectSingleNode("descendant::PDBx:entityCategory", nsManager);
                XmlNodeList entityNodeList = entityCategoryNode.ChildNodes;
                string descript = "";
                foreach (XmlNode entityNode in entityNodeList)
                {
                    int entityId = Convert.ToInt32(entityNode.Attributes["id"].InnerText);
                    descript = "-";
                    if (entityNode["PDBx:pdbx_description"] != null)
                    {
                        descript = entityNode["PDBx:pdbx_description"].InnerText;
                    }
                    if (! entityIdNameHash.ContainsKey(entityId))
                    {
                        entityIdNameHash.Add(entityId, descript);
                    }
                }
            }
            return entityIdNameHash;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <param name="nsManager"></param>
        /// <returns></returns>
        private Dictionary<int, string> GetEntityThreeLetterSequences(ref XmlDocument xmlDoc, ref XmlNamespaceManager nsManager)
        {
            Dictionary<int, string> entitySeqHash = new Dictionary<int, string>();
            XmlNode seqCategoryNode = xmlDoc.DocumentElement.SelectSingleNode("descendant::PDBx:entity_poly_seqCategory", nsManager);
            int entityId = -1;
            int preEntityId = -1;
            string residue = "";
            string residueList = "";
            if (seqCategoryNode != null)
            {
                XmlNodeList seqNodeList = seqCategoryNode.ChildNodes;
                foreach (XmlNode seqNode in seqNodeList)
                {
                    entityId = Convert.ToInt32(seqNode.Attributes["entity_id"].InnerText);
                    if (entityId != preEntityId && preEntityId != -1)
                    {
                        entitySeqHash.Add(preEntityId, residueList.TrimEnd (' '));
                        residueList = "";
                        residue = "";
                    }
                    residue = seqNode.Attributes["mon_id"].InnerText;
                    residueList += (residue + " ");
                    preEntityId = entityId;
                }
                entitySeqHash.Add(entityId, residueList.TrimEnd (' '));
            }
            return entitySeqHash;
        }
		#endregion

        #region parse non-polymer type
        struct AsymInfo
        {
            public string entity;
            public string strandId;
            public string residueSeq;
            public string residueCoordinate;
            public string authSeqNums;
            public string ndbSeqNums;
            public string pdbSeqNums;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <param name="entityInfoCat"></param>
        /// <param name="nsManager"></param>
        /// <returns></returns>
        private Dictionary<string, AsymInfo> ParseEntityNonPolymerCategory(ref XmlDocument xmlDoc, ref XmlNamespaceManager nsManager)
        {
            Dictionary<string, AsymInfo> asymIdInfoHash = new Dictionary<string, AsymInfo>();
            XmlNode nonPolySchemeNode = xmlDoc.DocumentElement.SelectSingleNode("descendant::PDBx:pdbx_nonpoly_schemeCategory", nsManager);
            if (nonPolySchemeNode != null)
            {
                string preAsymId = "";
                string asymId = "";
                string entityId = "";
                string strandId = "";
                string pdbMonId = "";
                string resSequence = "";
                string resCoordinate = "";
                string authSeqNumbers = "";
                string ndbSeqNumbers = "";
                string pdbSeqNumbers = "";

                XmlNodeList asymNonPolyNodeList = nonPolySchemeNode.ChildNodes;
                if (asymNonPolyNodeList.Count > 0)
                {
                    preAsymId = asymNonPolyNodeList[0].Attributes["asym_id"].InnerText.ToString();
                }

                foreach (XmlNode asymNonPolyNode in asymNonPolyNodeList)
                {
                    asymId = asymNonPolyNode.Attributes["asym_id"].InnerText.ToString();

                    if (preAsymId != asymId) // start new asymId
                    {
                        AsymInfo asymInfo = new AsymInfo();
                        asymInfo.entity = entityId;
                        asymInfo.strandId = strandId;
                        asymInfo.residueCoordinate = resCoordinate;
                        asymInfo.residueSeq = resSequence;
                        asymInfo.authSeqNums = authSeqNumbers.TrimEnd(',');
                        asymInfo.ndbSeqNums = ndbSeqNumbers.TrimEnd(',');
                        asymInfo.pdbSeqNums = pdbSeqNumbers.TrimEnd(',');
                        // add info for this asymmetric chain
                        AddAsymInfoToHash(preAsymId, asymInfo, ref asymIdInfoHash);
                        // reset
                        preAsymId = asymId;
                        resSequence = "";
                        resCoordinate = "";
                        authSeqNumbers = "";
                        ndbSeqNumbers = "";
                        pdbSeqNumbers = "";
                    }

                    strandId = "_"; // defualt value is underscore

                    ndbSeqNumbers += asymNonPolyNode.Attributes["ndb_seq_num"].InnerText.ToString();

                    XmlNodeList nonpolyNodeList = asymNonPolyNode.ChildNodes;
                    pdbMonId = "-";
                    foreach (XmlNode nonpolyNode in nonpolyNodeList)
                    {
                        if (nonpolyNode.Name.ToLower().IndexOf("entity_id") > -1)
                        {
                            entityId = nonpolyNode.InnerText.ToString();
                        }
                        if (nonpolyNode.Name.ToLower().IndexOf("strand_id") > -1)
                        {
                            strandId = nonpolyNode.InnerText.ToString();
                            if (strandId.Length == 0)
                                strandId = "_";
                        }
                        if (nonpolyNode.Name.ToLower().IndexOf("pdbx:mon_id") > -1)
                        {
                            resSequence += "(" + nonpolyNode.InnerText + ")";
                        }
                        if (nonpolyNode.Name.ToLower().IndexOf("pdb_mon_id") > -1)
                        {
                            pdbMonId = nonpolyNode.InnerText;
                        }
                        if (nonpolyNode.Name.ToLower().IndexOf("pdb_seq_num") > -1)
                        {
                            pdbSeqNumbers += nonpolyNode.InnerText;
                        }
                        if (nonpolyNode.Name.ToLower().IndexOf("auth_seq_num") > -1)
                        {
                            authSeqNumbers += nonpolyNode.InnerText;
                        }
                    }
                    authSeqNumbers += ",";
                    ndbSeqNumbers += ",";
                    pdbSeqNumbers += ",";
                    resCoordinate += pdbMonId;
                }
                // add the last asymid
                AsymInfo lastAsymInfo = new AsymInfo();
                lastAsymInfo.entity = entityId;
                lastAsymInfo.strandId = strandId;
                lastAsymInfo.residueCoordinate = resCoordinate;
                lastAsymInfo.residueSeq = resSequence;
                lastAsymInfo.authSeqNums = authSeqNumbers.TrimEnd(',');
                lastAsymInfo.pdbSeqNums = pdbSeqNumbers.TrimEnd(',');
                lastAsymInfo.ndbSeqNums = ndbSeqNumbers.TrimEnd(',');
                /*	if (! asymIdInfoHash.ContainsKey (preAsymId))
                    {
                        asymIdInfoHash.Add (preAsymId, lastAsymInfo);
                    }*/
                AddAsymInfoToHash(asymId, lastAsymInfo, ref asymIdInfoHash);
            }
            return asymIdInfoHash;
        }

        /// <summary>
        /// asymmetric chain ID is not unique
        /// e.g. 2V46, 2J00, 2V48: MG and/or ZN have same asymmtric chain
        /// lost entity info, MG has entityID = 26 while ZN has entityID = 27
        /// </summary>
        /// <param name="asymInfo"></param>
        /// <param name="asymIdInfoHash"></param>
        private void AddAsymInfoToHash(string preAsymId, AsymInfo asymInfo, ref Dictionary<string, AsymInfo> asymIdInfoHash)
        {
            if (!asymIdInfoHash.ContainsKey(preAsymId))
            {
                asymIdInfoHash.Add(preAsymId, asymInfo);
            }
            else
            {
                AsymInfo preAsymInfo = (AsymInfo)asymIdInfoHash[preAsymId];
                preAsymInfo.residueCoordinate = preAsymInfo.residueCoordinate + asymInfo.residueCoordinate;
                preAsymInfo.residueSeq = preAsymInfo.residueSeq + asymInfo.residueSeq;
                preAsymInfo.authSeqNums = preAsymInfo.authSeqNums + "," + asymInfo.authSeqNums;
                preAsymInfo.ndbSeqNums = preAsymInfo.ndbSeqNums + "," + asymInfo.ndbSeqNums;
                preAsymInfo.pdbSeqNums = preAsymInfo.pdbSeqNums + "," + asymInfo.pdbSeqNums;
                asymIdInfoHash[preAsymId] = preAsymInfo;
            }
        }
        #endregion

        #region parse Fractional Transfer matrix
        /// <summary>
		/// 
		/// </summary>
		/// <param name="xmlDoc"></param>
		/// <param name="crystalInfo"></param>
		/// <param name="nsManager"></param>
		private void ParseFractTransfMatrix(ref XmlDocument xmlDoc, ref ScaleCategory scalCat, ref XmlNamespaceManager nsManager)
		{
			const string matrixString = "fract_transf_matrix";
			const string vectorString = "fract_transf_vector";
			// get cell information: Length, Angle, and Z_PDB
			string fractNodePath = "descendant::PDBx:atom_sitesCategory/PDBx:atom_sites";
			XmlNode fractNode = xmlDoc.DocumentElement.SelectSingleNode (fractNodePath, nsManager);
			Matrix fractMatrix = new Matrix ();
			if (fractNode != null)
			{
				XmlNodeList fractNodeList = fractNode.ChildNodes;
				int rowId = 0; // [0, 1, 2]
				int colId = 0; // [0, 1, 2, 3]
				foreach (XmlNode fractMatrixNode in fractNodeList)
				{
					string nodeName = fractMatrixNode.Name.ToLower();
					if (nodeName.IndexOf (matrixString) > -1)
					{
						double fractMatrixVal = Convert.ToDouble (fractMatrixNode.InnerText.ToString ());
						rowId = Convert.ToInt32 (nodeName.Substring (nodeName.IndexOf (matrixString) + matrixString.Length, 1)) - 1;
						colId = Convert.ToInt32 (nodeName.Substring (nodeName.IndexOf (matrixString) + matrixString.Length + 1, 1)) - 1;
						fractMatrix.Add (rowId, colId, fractMatrixVal);
					}
					if (nodeName.IndexOf (vectorString) > -1)
					{
						double vectorVal = Convert.ToDouble (fractMatrixNode.InnerText.ToString ());
						int dimNo = Convert.ToInt32 (nodeName.Substring (nodeName.IndexOf (vectorString) + vectorString.Length, 1)) - 1;
						fractMatrix.Add (dimNo, Matrix.elemColCount - 1, vectorVal);
					}
				}
			}
			scalCat.ScaleMatrix = fractMatrix;
		}
		#endregion

		#region parse NCS struct
		/// <summary>
		/// parse non-crsytalgraphic symmetry operators
		/// </summary>
		/// <param name="xmlDoc"></param>
		/// <param name="nsManager"></param>
		private void ParseNcsStruct (ref XmlDocument xmlDoc, ref NcsCategory ncsCat, ref XmlNamespaceManager nsManager)
		{
			XmlNodeList ncsNodeList = xmlDoc.DocumentElement.SelectNodes ("descendant::PDBx:struct_ncs_oper", nsManager);
			foreach (XmlNode ncsNode in ncsNodeList)
			{
				NcsOperator ncsOp = new NcsOperator ();
				ncsOp.ncsId = Convert.ToInt32 (ncsNode.Attributes["id"].Value);				
				ncsOp.code = ncsNode["PDBx:code"].InnerText;
				ncsOp.ncsMatrix.Add (0, 0, Convert.ToDouble (ncsNode["PDBx:matrix11"].InnerText));
				ncsOp.ncsMatrix.Add (0, 1, Convert.ToDouble (ncsNode["PDBx:matrix12"].InnerText));
				ncsOp.ncsMatrix.Add (0, 2, Convert.ToDouble (ncsNode["PDBx:matrix13"].InnerText));
				ncsOp.ncsMatrix.Add (1, 0, Convert.ToDouble (ncsNode["PDBx:matrix21"].InnerText));
				ncsOp.ncsMatrix.Add (1, 1, Convert.ToDouble (ncsNode["PDBx:matrix22"].InnerText));
				ncsOp.ncsMatrix.Add (1, 2, Convert.ToDouble (ncsNode["PDBx:matrix23"].InnerText));
				ncsOp.ncsMatrix.Add (2, 0, Convert.ToDouble (ncsNode["PDBx:matrix31"].InnerText));
				ncsOp.ncsMatrix.Add (2, 1, Convert.ToDouble (ncsNode["PDBx:matrix32"].InnerText));
				ncsOp.ncsMatrix.Add (2, 2, Convert.ToDouble (ncsNode["PDBx:matrix33"].InnerText));
				
				ncsOp.ncsMatrix.Add (0, 3, Convert.ToDouble (ncsNode["PDBx:vector1"].InnerText));
				ncsOp.ncsMatrix.Add (1, 3, Convert.ToDouble (ncsNode["PDBx:vector2"].InnerText));
				ncsOp.ncsMatrix.Add (2, 3, Convert.ToDouble (ncsNode["PDBx:vector3"].InnerText));
				ncsCat.Add (ncsOp);
			}
		}
		#endregion
	}
}
