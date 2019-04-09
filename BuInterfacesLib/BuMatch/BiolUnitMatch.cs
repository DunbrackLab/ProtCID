using System;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;

namespace BuInterfacesLib.BuMatch
{
	/// <summary>
	/// Summary description for BuMatch.
	/// </summary>
	public class BiolUnitMatch
	{
		[XmlAttribute("PdbID")] public string pdbId = "";
		[XmlAttribute("PdbBuID")] public string pdbBuId = "-1";
		[XmlAttribute("PqsBuID")] public string pqsBuId = "-1";
		[XmlAttribute("PisaBuID")] public string pisaBuId = "-1";

		[XmlElement("PdbBU_Asym")] public string pdbBuAsym = "";
		[XmlElement("PqsBU_Asym")] public string pqsBuAsym = "";
		[XmlElement("PisaBU_Asym")] public string pisaBuAsym = "";

		[XmlElement("PdbBU_Entity")] public string pdbBuEntity = "";
		[XmlElement("PqsBU_Entity")] public string pqsBuEntity = "";
		[XmlElement("PisaBU_Entity")] public string pisaBuEntity = "";

		[XmlElement("PdbBU_Auth")] public string pdbBuAuth = "";
		[XmlElement("PqsBU_Auth")] public string pqsBuAuth = "";
		[XmlElement("PisaBU_Auth")] public string pisaBuAuth = "";

		[XmlElement("PdbBU_Abc")] public string pdbBuAbc = "";
		[XmlElement("PqsBU_Abc")] public string pqsBuAbc = "";
		[XmlElement("PisaBU_Abc")] public string pisaBuAbc = "";

		[XmlElement("PqsBU_Pqs")] public string pqsBuPqs = "";

		public BiolUnitMatch()
		{
		}

		/// <summary>
		/// check if two BUs have the same entity contents
		/// </summary>
		/// <returns>0: same, 1: substruct, 2: dif</returns>
		public int AreSameBUsInEntityFormat (int buPairType)
		{
			int compResult = -1;

			switch (buPairType)
			{
				case (int)BuInterfaceDbBuilder.CompType.PDBPQS:
					compResult = CompareBUsInEntityFormat (pdbBuEntity, pqsBuEntity);
					break;

				case (int)BuInterfaceDbBuilder.CompType.PISAPDB:
					compResult = CompareBUsInEntityFormat (pisaBuEntity, pdbBuEntity);
					break;
					
				case (int)BuInterfaceDbBuilder.CompType.PISAPQS:
					compResult = CompareBUsInEntityFormat (pisaBuEntity, pqsBuEntity);
					break;

				default:
					compResult = -1;
					break;
			}
			return compResult;
		}

		/// <summary>
		/// check if two BUs have the same entity contents for a given type pair
		/// </summary>
		/// <param name="entityBu1"></param>
		/// <param name="entityBu2"></param>
		/// <returns></returns>
		public int CompareBUsInEntityFormat (string entityBu1, string entityBu2)
		{
			if (entityBu1 == entityBu2)
			{
				return (int)BuInterfaceDbBuilder.BuEntityType.Same; // 0
			}
			if (IsBUsContains (entityBu1, entityBu2))
			{
				return (int)BuInterfaceDbBuilder.BuEntityType.SubStruct; // 1
			}
			return (int)BuInterfaceDbBuilder.BuEntityType.Dif; // 2
		}
		/// <summary>
		/// does one biological unit contain another one
		/// e.g. (1.4) contains (1.2)
		/// (1.4)(2.4) contains (1.2)(2.2)
		/// </summary>
		/// <param name="entityBu1"></param>
		/// <param name="entityBu2"></param>
		/// <returns></returns>
		public bool IsBUsContains (string entityBu1, string entityBu2)
		{
			if (entityBu1 == "-" || entityBu2 == "-")
			{
				return true;
			}
			string[] fields1 = entityBu1.Trim (')').Split (')');
			string[] fields2 = entityBu2.Trim (')').Split (')');
			Hashtable entityHash1 = new Hashtable ();
			Hashtable entityHash2 = new Hashtable ();
			foreach (string field in fields1)
			{
				string entityStr = field.Trim ('(');
				string[] entityFields = entityStr.Split ('.');
				entityHash1.Add (Convert.ToInt32 (entityFields[0]), Convert.ToInt32 (entityFields[1]));
			}
			foreach (string field in fields2)
			{
				string entityStr = field.Trim ('(');
				string[] entityFields = entityStr.Split ('.');
				entityHash2.Add (Convert.ToInt32 (entityFields[0]), Convert.ToInt32 (entityFields[1]));
			}
			// cases: (1.2)(2.2) and (1.4)(2.4)
			// (1.2) and (1.4)
			if (entityHash1.Count == entityHash2.Count)
			{
				foreach (int entity in entityHash1.Keys)
				{
					bool found = false;
					foreach (int entity2 in entityHash2.Keys)
					{
						if (entity == entity2)
						{
							found = true;
							break;
						}
					}
					// BUs contain different entities
					if (! found)
					{
						return false;
					}
				}
				ArrayList entityList1 = new ArrayList (entityHash1.Keys);
				ArrayList  entityList2 = new ArrayList (entityHash2.Keys);
				entityList1.Sort ();
				entityList2.Sort ();
				bool firstSmall = false;
				bool firstLarge = false;
				for (int i = 0; i < entityList1.Count; i ++)
				{
					if ((int)entityHash1[entityList1[i]] < (int)entityHash2[entityList2[i]])
					{
						firstSmall = true;
					}
					else
					{
						firstLarge = true;
					}
				}
				// should not be this case: (1.2)(2.3) and (1.3)(2.2)
				if (firstSmall && firstLarge)
				{
					return false;
				}
				else
				{
					return true;
				}
			}
				// cases: (1.1)(1.2) and (1.1)(2.2)(3.1)
				// (1.1)(2.1)(3.1)(4.1) and (1.2)(2.2)
				// have to check the chain numbers
			else
			{
				if (entityHash1.Count < entityHash2.Count)
				{
					foreach (int entity in entityHash1.Keys)
					{
						bool found = false;
						foreach (int entity2 in entityHash2.Keys)
						{
							if (entity == entity2 && (int)entityHash1[entity] <= (int)entityHash2[entity2])
							{
								found = true;
								break;
							}
						}
						if (! found)
						{
							return false;
						}
					}
					return true;
				}
				else
				{
					foreach (int entity2 in entityHash2.Keys)
					{
						bool found = false;
						foreach (int entity in entityHash1.Keys)
						{
							if (entity == entity2 && (int)entityHash2[entity2] <= (int)entityHash1[entity])
							{
								found = true;
								break;
							}
						}
						if (! found)
						{
							return false;
						}
					}
					return true;
				}
			}
		}

	}
}
