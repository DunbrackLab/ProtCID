using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using CrystalInterfaceLib.DomainInterfaces;
using InterfaceClusterLib.InterfaceImage;
using ProtCidSettingsLib;
using AuxFuncLib;

namespace InterfaceClusterLib.DomainInterfaces
{
    public class DomainInterfaceImageGen : InterfaceImageGen
    {

        #region generate domain cluster images
        /// <summary>
        /// 
        /// </summary>
        public void GenerateDomainInterfaceImages()
        {
            Initialize("Domain");

            string[] clusterInterfaces = GetClusterInterfaces();

            GenerateDomainInterfaceImages(clusterInterfaces);

            CollectWebImages(clusterInterfaces);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetClusterInterfaces()
        {
            string queryString = "Select Distinct RelSeqId, ClusterID, ClusterInterface From PfamDomainClusterSumInfo";
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            List<string> clusterInterfaceList = new List<string> ();
   //         string pngFile = "";
            string clusterInterface = "";
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                clusterInterface = interfaceRow["ClusterInterface"].ToString().TrimEnd();
                if (! IsClusterInterfacePmlImageExist (clusterInterface))
                {
                    clusterInterfaceList.Add(clusterInterface);
                }
            }
            return clusterInterfaceList.ToArray ();
        }

       

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        public void GenerateDomainInterfaceImages(string[] clusterInterfaces)
        {
            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Write the pymol script file");
            string pymolScriptFile = interfacePngPymolScript.FormatDomainInterfacePngPymolScript(clusterInterfaces, InterfaceImageDir);
            //   string pymolScriptFile = Path.Combine (InterfaceImageDir, "ImageGen.pml");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Run pymol, may take a long time");
            pymolLauncher.RunPymol(pymolScriptFile);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Resize images");
            ResizeImagesToMiddle(clusterInterfaces);

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get Thumbnail images.");
            GetThumbnailImages(clusterInterfaces);

            try
            {
                Directory.Delete(ProtCidSettings.tempDir, true);
            }
            catch { }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
        #endregion

        #region update
        /// <summary>
        /// update the cluster interfaceds for the domain clusters 
        /// </summary>
        /// <param name="relSeqIds"></param>
        public void UpdateDomainInterfaceImages(int[] relSeqIds)
        {
            Initialize("Domain");

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Update images for the clusters updated.");
            string[] updateClusterInterfaces = GetUpdateClusterInterfaces(relSeqIds);
            GenerateDomainInterfaceImages(updateClusterInterfaces);

            CollectUpdatedWebImages(updateClusterInterfaces);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relSeqIds"></param>
        /// <returns></returns>
        private string[] GetUpdateClusterInterfaces(int[] relSeqIds)
        {
            string queryString = "";
            List<string> clusterInterfaceList = new List<string> ();
            string clusterInterface = "";
            foreach (int relSeqId in relSeqIds)
            {
                queryString = string.Format("Select ClusterInterface From PfamDomainClusterSumInfo Where RelSeqID = {0};", relSeqId);
                DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
                foreach (DataRow clusterInterfaceRow in clusterInterfaceTable.Rows)
                {
                    clusterInterface = clusterInterfaceRow["ClusterInterface"].ToString().TrimEnd();
                    clusterInterfaceList.Add(clusterInterface);
                }
            }
            return clusterInterfaceList.ToArray ();
        }
        #endregion
    }
}
