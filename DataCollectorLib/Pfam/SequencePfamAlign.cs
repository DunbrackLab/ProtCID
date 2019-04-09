using System;
using System.Net;
using System.Xml;
using System.Threading;
using System.IO;
using System.Collections;

namespace DataCollectorLib.Pfam
{
	/// <summary>
	/// Summary description for SequencePfamAlign.
	/// </summary>
	public class SequencePfamAlign
	{
		public SequencePfamAlign()
		{
		}

		public struct PfamJobInfo
		{
			public string resultUrl; // where the result file to be downloaded
			public int estimatedTime; // in seconds
		}

		#region alignment to pfam families
		/// <summary>
		/// align the sequence into a pfam family using the input cutoff for the Evalue
		/// </summary>
		/// <param name="sequence"></param>
		/// <param name="evalue"></param>
		/// <returns></returns>
		public PfamAlignInfo[] AlignSequenceToPfamFamily (string sequence, double evalue)
		{
			string requestString = string.Format ("http://pfam.sanger.ac.uk/search/sequence?" + 
				"evalue={0}&output=xml&seq={1}", evalue, sequence);
			PfamAlignInfo[] alignInfos = GetPfamFamilyAlignment (requestString);
			return alignInfos;
		}
		/// <summary>
		/// align the sequence into a pfam family using the default evalue (1.0)
		/// </summary>
		/// <param name="sequence"></param>
		/// <returns></returns>
		public PfamAlignInfo[] AlignSequenceToPfamFamily (string sequence)
		{
			string requestString = "http://pfam.sanger.ac.uk/search/sequence?output=xml&seq=" + sequence;
			PfamAlignInfo[] alignInfos = GetPfamFamilyAlignment (requestString);
			return alignInfos;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="requestString"></param>
		/// <returns></returns>
		private PfamAlignInfo[] GetPfamFamilyAlignment (string requestString)
		{
			string jobInfoXml = "pfamjobInfo.xml";
			WebClient webClient = new WebClient ();

//			Thread.Sleep (3000);

			webClient.DownloadFile (requestString, jobInfoXml);

			PfamJobInfo jobInfo = ParseJobInfoXml (jobInfoXml);

			string resultXml = "pfamAlignResult.xml";
			long fileSize = 0;
			FileInfo resultFileInfo = null;
			int numOfTrials = 0;
			while (fileSize == 0 && numOfTrials <= 10)
			{
				numOfTrials ++;
				Thread.Sleep (jobInfo.estimatedTime * 10000); 
				webClient.DownloadFile (jobInfo.resultUrl, resultXml);
				resultFileInfo = new FileInfo (resultXml);
				fileSize = resultFileInfo.Length;
			}

			resultFileInfo = new FileInfo (resultXml);
			fileSize = resultFileInfo.Length;
			if (fileSize == 0)
			{
				throw new Exception ("The alginment file is not on PFAM");
			}

			//	string resultXml = "pfamAlignResult0.xml";
			PfamAlignInfo[] pfamAlignInfos = ParsePfamResultXmlFile (resultXml);
			return pfamAlignInfos;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="jobInfoXml"></param>
		/// <returns></returns>
		public PfamJobInfo ParseJobInfoXml (string jobInfoXml)
		{
			PfamJobInfo pfamJobInfo = new PfamJobInfo ();

			XmlDocument xmlDoc = new XmlDocument ();
			xmlDoc.Load (jobInfoXml);
			// Create an XmlNamespaceManager for resolving namespaces.
			XmlNamespaceManager nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
			nsManager.AddNamespace("pfam", "http://pfam.sanger.ac.uk/");

			XmlNodeList jobNodeList = xmlDoc.DocumentElement.ChildNodes;	
			string resultUrl = "";
			if (jobNodeList != null)
			{
				XmlNode jobNode = jobNodeList[0];
				XmlNodeList jobInfoNodes = jobNode.ChildNodes;
				foreach (XmlNode infoNode in jobInfoNodes)
				{
					if (infoNode.Name.ToLower () == "estimated_time")
					{
						pfamJobInfo.estimatedTime = Convert.ToInt32 (infoNode.InnerText);
					}

					if (infoNode.Name.ToLower () == "result_url")
					{
						resultUrl = infoNode.InnerText.TrimEnd ();
						resultUrl = resultUrl.Trim ("\n".ToCharArray ());
						pfamJobInfo.resultUrl = resultUrl;
					}
				}
				
			}
			return pfamJobInfo;
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="resultXml"></param>
		public PfamAlignInfo[] ParsePfamResultXmlFile (string resultXml)
		{
			XmlDocument xmlDoc = new XmlDocument ();
			xmlDoc.Load (resultXml);
			// Create an XmlNamespaceManager for resolving namespaces.
			XmlNamespaceManager nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
			nsManager.AddNamespace("pfam", "http://pfam.sanger.ac.uk/");
			
			XmlNode resultsNode = xmlDoc.DocumentElement.FirstChild;	
			XmlNode proteinNode = resultsNode.FirstChild;
			if (proteinNode == null)
			{
				throw new Exception ("No Pfam alignments found.");
			}
			XmlNode dbNode = proteinNode.FirstChild;
			XmlNode matchesNode = dbNode.FirstChild;
			XmlNodeList matchsNodeList = matchesNode.ChildNodes;
			ArrayList alignInfoList = new ArrayList ();
			string pfamAcc = "";
			string pfamId = "";
			string type = "";
			string pfamClass = "";
			foreach (XmlNode matchNode  in matchesNode.ChildNodes)
			{
				pfamAcc = matchNode.Attributes["accession"].InnerText;
				pfamId = matchNode.Attributes["id"].InnerText;
				type = matchNode.Attributes["type"].InnerText;
				pfamClass = matchNode.Attributes["class"].InnerText;
				foreach (XmlNode locationNode in matchNode.ChildNodes)
				{
					PfamAlignInfo alignInfo = new  PfamAlignInfo ();
					alignInfo.pfamAcc = pfamAcc;
					alignInfo.pfamId = pfamId;
					alignInfo.type = type;
					alignInfo.pfamClass = pfamClass;
					alignInfo.startPos = Convert.ToInt32 (locationNode.Attributes["start"].InnerText);
					alignInfo.endPos = Convert.ToInt32 (locationNode.Attributes["end"].InnerText);
					alignInfo.hmmStartPos = Convert.ToInt32 (locationNode.Attributes["hmm_start"].InnerText);
					alignInfo.hmmEndPos = Convert.ToInt32 (locationNode.Attributes["hmm_end"].InnerText);
					alignInfo.bitScore = Convert.ToDouble (locationNode.Attributes ["bitscore"].InnerText);
					alignInfo.evalue = Convert.ToDouble (locationNode.Attributes["evalue"].InnerText);
					alignInfo.mode = locationNode.Attributes["mode"].InnerText;
					alignInfoList.Add (alignInfo);
				}
			}
			PfamAlignInfo[] alignInfos = new PfamAlignInfo [alignInfoList.Count];
			alignInfoList.CopyTo (alignInfos);
			return alignInfos;
		}
		#endregion
	}
}
