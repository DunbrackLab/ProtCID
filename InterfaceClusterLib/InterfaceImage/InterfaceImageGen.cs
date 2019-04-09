using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using DbLib;
using ProtCidSettingsLib;
using AuxFuncLib;
using InterfaceClusterLib.PymolScript;

namespace InterfaceClusterLib.InterfaceImage
{
    public class InterfaceImageGen
    {
        #region member variables
        public DbQuery dbQuery = new DbQuery();
        public string InterfaceImageDir = "";
        public string webImageDir = "";
        public string updateWebImageDir = "";
        public InterfaceImagePymolScript interfacePngPymolScript = new InterfaceImagePymolScript ();
        public CmdOperations pymolLauncher = new CmdOperations ();
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void GenerateInterfaceImages()
        {
            Initialize("Chain");

            string[] clusterInterfaces = GetAllClusterInterfaces();
    //        string[] clusterInterfaces = GetMissingImageClusters();

            GenerateInterfaceImages(clusterInterfaces);

            CollectWebImages(clusterInterfaces);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetAllClusterInterfaces()
        {
            string queryString = "Select ClusterInterface From PfamSuperClusterSumInfo";
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] clusterInterfaces = new string[clusterInterfaceTable.Rows.Count];
            string clusterInterface = "";
            int count = 0;
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                clusterInterface = interfaceRow["ClusterInterface"].ToString().TrimEnd();
                clusterInterfaces[count] = clusterInterface;
                count++;
            }
            return clusterInterfaces;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        public void GenerateInterfaceImages(string[] clusterInterfaces)
        {
            if (!Directory.Exists(ProtCidSettings.tempDir))
            {
                Directory.CreateDirectory(ProtCidSettings.tempDir);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Write the pymol script file");
            ProtCidSettings.logWriter.WriteLine("Write PyMOL script file");
            string pymolScriptFile = interfacePngPymolScript.FormatInterfaceImagePymolScript (clusterInterfaces, InterfaceImageDir);
            ProtCidSettings.logWriter.Flush();

        //     string pymolScriptFile = Path.Combine (InterfaceImageDir, "ImageGen.pml");
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Run pymol, may take a long time");
            ProtCidSettings.logWriter.WriteLine("Run PyMOL, may take long time");
            pymolLauncher.RunPymol(pymolScriptFile);
            ProtCidSettings.logWriter.Flush();
            
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Resize images");
            ProtCidSettings.logWriter.WriteLine("Resize images");
            ResizeImagesToMiddle (clusterInterfaces);
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Get Thumbnail images.");
            ProtCidSettings.logWriter.WriteLine("Resize images to Thumbnail images");
            GetThumbnailImages(clusterInterfaces);
            ProtCidSettings.logWriter.Flush();
            
            try
            {
                Directory.Delete(ProtCidSettings.tempDir, true);
            }
            catch { }

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
            ProtCidSettings.logWriter.WriteLine("Update cluster interface images done!");
            ProtCidSettings.logWriter.Flush();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateGroups"></param>
        public void UpdateClusterInterfaceImages(int[] updateGroups)
        {
            Initialize("Chain");

            ProtCidSettings.logWriter.WriteLine("Get update cluster interfaces");
            string[] updateClusterInterfaces = GetUpdateClusterInterfaces(updateGroups);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.logWriter.WriteLine("Generate cluster interface images");
            GenerateInterfaceImages(updateClusterInterfaces);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();

            ProtCidSettings.logWriter.WriteLine("Collect ProtCID web cluster interface images");
            CollectUpdatedWebImages(updateClusterInterfaces);
            ProtCidSettings.logWriter.WriteLine("Done!");
            ProtCidSettings.logWriter.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateGroups"></param>
        /// <returns></returns>
        private string[] GetUpdateClusterInterfaces(int[] updateGroups)
        {
            List<string> clusterInterfaceList = new List<string> ();
            foreach (int updateGroup in updateGroups)
            {
                string[] groupClusterInterfaces = GetGroupClusterInterfaces(updateGroup);
                clusterInterfaceList.AddRange(groupClusterInterfaces);
            }
            return clusterInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="superGroupId"></param>
        /// <returns></returns>
        private string[] GetGroupClusterInterfaces(int superGroupId)
        {
            string queryString = string.Format("Select ClusterInterface From PfamSuperClusterSumInfo Where SuperGroupSeqID = {0};", superGroupId);
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] clusterInterfaces = new string[clusterInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow clusterInterfaceRow in clusterInterfaceTable.Rows)
            {
                clusterInterfaces[count] = clusterInterfaceRow["ClusterInterface"].ToString().TrimEnd();
                count++;
            }
            return clusterInterfaces;
        }

        #region thumbnail images
        public void GetThumbnailImages(string[] clusterInterfaces)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.totalOperationNum = clusterInterfaces.Length;
            ProtCidSettings.progressInfo.totalStepNum = clusterInterfaces.Length;
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Resize image to 40");

            string interfaceImageFile = "";
            string thumbnailImageFile = "";
            int thumbnailPixels = interfacePngPymolScript.imageSizes[(int)InterfaceImagePymolScript.ImageSize.Thumbnail];
            foreach (string clusterInterface in clusterInterfaces)
            {
                ProtCidSettings.progressInfo.currentFileName = clusterInterface;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentOperationNum++;

                interfaceImageFile = interfacePngPymolScript.GetPngFileName(clusterInterface,
                    interfacePngPymolScript.imageSizes[(int)InterfaceImagePymolScript.ImageSize.Big], InterfaceImageDir);
                if (!File.Exists(interfaceImageFile))
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Image File not exit: " + interfaceImageFile);
                    ProtCidSettings.logWriter.WriteLine("Image File not exit: " + interfaceImageFile);
                    continue;
                }
                thumbnailImageFile = interfacePngPymolScript.GetPngFileName(clusterInterface, thumbnailPixels, webImageDir);
                if (File.Exists(thumbnailImageFile))
                {
                    continue;
                }
                Image interfaceImage = Image.FromFile(interfaceImageFile);
                Image thumbnailImage = interfaceImage.GetThumbnailImage(thumbnailPixels, thumbnailPixels, null, IntPtr.Zero);
                thumbnailImage.Save(thumbnailImageFile);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        public void ResizeImagesToMiddle (string[] clusterInterfaces)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Resize Images to 250");
            ProtCidSettings.progressInfo.totalOperationNum = clusterInterfaces.Length;
            ProtCidSettings.progressInfo.totalStepNum = clusterInterfaces.Length;

            string interfaceImageFile = "";
            string mediumImageFile = "";
            int mediumImagePixels = interfacePngPymolScript.imageSizes[(int)InterfaceImagePymolScript.ImageSize.Medium];
            Size mediumSize = new Size (mediumImagePixels, mediumImagePixels);
            foreach (string clusterInterface in clusterInterfaces)
            {
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;
                ProtCidSettings.progressInfo.currentFileName = clusterInterface;

                interfaceImageFile = interfacePngPymolScript.GetPngFileName(clusterInterface,
                    interfacePngPymolScript.imageSizes[(int)InterfaceImagePymolScript.ImageSize.Big], InterfaceImageDir);
                if (! File.Exists(interfaceImageFile))
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue("Image File not exit: " + interfaceImageFile);
                    ProtCidSettings.logWriter.WriteLine("Image File not exit: " + interfaceImageFile);
                    continue;
                }
                mediumImageFile = interfacePngPymolScript.GetPngFileName(clusterInterface, mediumImagePixels, webImageDir);
                if (File.Exists(mediumImageFile))
                {
                    continue;
                }
                Image interfaceImage = Image.FromFile(interfaceImageFile);
                Image mediumImage = ResizeImage(interfaceImage, mediumSize);
                mediumImage.Save(mediumImageFile);
            }
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done!");
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="imgToResize"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        private Image ResizeImage(Image imgToResize, Size size)
        {
            int sourceWidth = imgToResize.Width;
            int sourceHeight = imgToResize.Height;

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)size.Width / (float)sourceWidth);
            nPercentH = ((float)size.Height / (float)sourceHeight);

            if (nPercentH < nPercentW)
            {
                nPercent = nPercentH;
            }
            else
            {
                nPercent = nPercentW;
            }

            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);

            Bitmap b = new Bitmap(destWidth, destHeight);
            Graphics g = Graphics.FromImage((Image)b);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            g.DrawImage(imgToResize, 0, 0, destWidth, destHeight);
            g.Dispose();

            return (Image)b;
        }
        #endregion

        #region collect web images
        /// <summary>
        /// collect the images needed by the protcid web site
        /// </summary>
        /// <param name="clusterInterfaces"></param>
        public void CollectWebImages(string[] clusterInterfaces)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Collect Web Images.");
            ProtCidSettings.progressInfo.totalStepNum = clusterInterfaces.Length;
            ProtCidSettings.progressInfo.totalOperationNum = clusterInterfaces.Length;

            StreamWriter logWriter = new StreamWriter("InterfaceWithNoImages.txt");
          
            string webImageFile = "";
            string srcImageFile = "";
            foreach (string clusterInterface in clusterInterfaces)
            {
                ProtCidSettings.progressInfo.currentFileName = clusterInterface;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                webImageFile = Path.Combine (webImageDir, clusterInterface + "_250.png");
                srcImageFile = Path.Combine (InterfaceImageDir, clusterInterface + "_250.png");
                if (File.Exists(srcImageFile))
                {
                    if (File.Exists(webImageFile))
                    {
                        File.Delete(webImageFile);
                    }
                    File.Move(srcImageFile, webImageFile);
                }
                else
                {
                    logWriter.WriteLine(clusterInterface + " no 250 image.");
                }
                
                webImageFile = Path.Combine(webImageDir, clusterInterface + "_40.png");
                srcImageFile = Path.Combine(InterfaceImageDir, clusterInterface + "_40.png");
                if (File.Exists(srcImageFile))
                {
                    if (File.Exists(webImageFile))
                    {
                        File.Delete(webImageFile);
                    }
                    File.Move(srcImageFile, webImageFile);
                }
                else
                {
                    logWriter.WriteLine(clusterInterface + " no  40 image.");
                }
            }
            logWriter.Close();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done! ");
        }

        /// <summary>
        /// collect the updated interface images
        /// </summary>
        /// <param name="updatedClusterInterfaces"></param>
        public void CollectUpdatedWebImages(string[] updatedClusterInterfaces)
        {
            ProtCidSettings.progressInfo.ResetCurrentProgressInfo();
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Collect Web Images.");
            ProtCidSettings.progressInfo.totalStepNum = updatedClusterInterfaces.Length;
            ProtCidSettings.progressInfo.totalOperationNum = updatedClusterInterfaces.Length;

            StreamWriter logWriter = new StreamWriter("InterfaceWithNoImages.txt");

            string webImageFile = "";
            string updateWebImageFile = "";
            foreach (string clusterInterface in updatedClusterInterfaces)
            {
                ProtCidSettings.progressInfo.currentFileName = clusterInterface;
                ProtCidSettings.progressInfo.currentOperationNum++;
                ProtCidSettings.progressInfo.currentStepNum++;

                webImageFile = Path.Combine(webImageDir, clusterInterface + "_250.png");

                if (File.Exists(webImageFile))
                {
                    updateWebImageFile = Path.Combine(updateWebImageDir, clusterInterface + "_250.png");
                    File.Copy(webImageFile, updateWebImageFile, true);
                }
                else
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(clusterInterface + "_250.png not exist.");
                    logWriter.WriteLine(clusterInterface + " no 250 image.");
                }

                webImageFile = Path.Combine(webImageDir, clusterInterface + "_40.png");
                if (File.Exists(webImageFile))
                {
                    updateWebImageFile = Path.Combine(updateWebImageDir, clusterInterface + "_40.png");
                    File.Copy(webImageFile, updateWebImageFile, true);
                }
                else
                {
                    ProtCidSettings.progressInfo.progStrQueue.Enqueue(clusterInterface + "_40.png not exist.");
                    logWriter.WriteLine(clusterInterface + " no 40 image.");
                }
            }
            logWriter.Close();

            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Done! ");
        }
        #endregion

        #region initialize
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataType"></param>
        public void Initialize(string dataType)
        {
            InterfaceImageDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, dataType + "ClusterImages");
            if (!Directory.Exists(InterfaceImageDir))
            {
                Directory.CreateDirectory(InterfaceImageDir);
            }
            webImageDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, dataType + "WebClusterImages");
            if (!Directory.Exists(webImageDir))
            {
                Directory.CreateDirectory(webImageDir);
            }
            updateWebImageDir = Path.Combine(ProtCidSettings.dirSettings.interfaceFilePath, "Update" + dataType + "WebClusterImages");
            if (Directory.Exists(updateWebImageDir))
            {
                Directory.Delete(updateWebImageDir, true);
            }
            Directory.CreateDirectory(updateWebImageDir);
        }
        #endregion

        #region for debug
        /// <summary>
        /// 
        /// </summary>
        public void FindErrorImages()
        {
            string queryString = "Select Distinct SuperGroupSeqID, ClusterID, ClusterInterface From PfamSuperClusterSumInfo";
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string[] clusterInterfaces = new string[clusterInterfaceTable.Rows.Count];
            int count = 0;
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                clusterInterfaces[count] = interfaceRow["ClusterInterface"].ToString().TrimEnd();
                count++;
            }
            StreamWriter dataWriter = new StreamWriter("ErrorImageFiles.txt");
            long image500FileSize = 0;
            long image250FileSize = 0;
            long image80FileSize = 0;
            long imageFileSize = 0;
            List<string> interfaceWithErrorImageList = new List<string> ();
            foreach (string clusterInterface in clusterInterfaces)
            {
                string[] imageFiles = Directory.GetFiles(InterfaceImageDir, clusterInterface + "*.png");
                foreach (string imageFile in imageFiles)
                {
                    imageFileSize = GetImageFileSize (imageFile);
                    if (imageFile.IndexOf("_500") > -1)
                    {
                        image500FileSize = imageFileSize;
                    }
                    else if (imageFile.IndexOf("_250") > -1)
                    {
                        image250FileSize = imageFileSize;
                    }
                    else if (imageFile.IndexOf("_80") > -1)
                    {
                        image80FileSize = imageFileSize;
                    }
                }
                if (image500FileSize < image250FileSize || image250FileSize < image80FileSize 
                    || image500FileSize == 2151 || image250FileSize == 2151 || image80FileSize == 2151)
                {
                    dataWriter.WriteLine(clusterInterface + "\t" + image500FileSize.ToString () + 
                        "\t" + image250FileSize.ToString () + "\t" + image80FileSize.ToString ());
                    interfaceWithErrorImageList.Add(clusterInterface);
                }
            }
            dataWriter.Close();
            
       //     string[] interfacesWithErrorImages = ReadClusterInterfacesWithErrorImages();

            string pymolScriptFile = interfacePngPymolScript.FormatInterfaceImagePymolScript(interfaceWithErrorImageList.ToArray (), InterfaceImageDir);
            //    string pymolScriptFile = @"E:\DbProjectData\InterfaceFiles_update\clusterImages\ImageGen.pml";
            ProtCidSettings.progressInfo.progStrQueue.Enqueue("Run pymol, may take a long time");
            pymolLauncher.RunPymol(pymolScriptFile);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] ReadClusterInterfacesWithErrorImages()
        {
            StreamReader dataReader = new StreamReader("ErrorImageFiles0.txt");
            List<string> clusterInterfaceList = new List<string> ();
            string line = "";
            while ((line = dataReader.ReadLine()) != null)
            {
                string[] fields = line.Split('\t');
                clusterInterfaceList.Add(fields[0]);
            }
            dataReader.Close();
            return clusterInterfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="imageFile"></param>
        /// <returns></returns>
        private long GetImageFileSize(string imageFile)
        {
            FileInfo fileInfo = new FileInfo(imageFile);
            return fileInfo.Length;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string[] GetMissingImageClusters()
        {
            List<string> interfaceList = new List<string> ();
            string queryString = "Select ClusterInterface From PfamSuperClusterSumInfo;";
            DataTable clusterInterfaceTable = ProtCidSettings.protcidQuery.Query( queryString);
            string clusterInterface = "";
            foreach (DataRow interfaceRow in clusterInterfaceTable.Rows)
            {
                clusterInterface = interfaceRow["ClusterInterface"].ToString().TrimEnd();
                if (IsClusterInterfaceImageExist(clusterInterface))
                {
                    continue;
                }
                interfaceList.Add(clusterInterface);
            }
            return interfaceList.ToArray ();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterface"></param>
        /// <returns></returns>
        public bool IsClusterInterfaceImageExist (string clusterInterface)
        {
            string imageFile = Path.Combine(webImageDir, clusterInterface + "_250.png");
            if (File.Exists (imageFile))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterInterface"></param>
        /// <returns></returns>
        public bool IsClusterInterfacePmlImageExist(string clusterInterface)
        {
            string imageFile = Path.Combine(InterfaceImageDir, clusterInterface + "_500.png");
            if (File.Exists(imageFile))
            {
                return true;
            }
            return false;
        }

        #endregion
    }
}
