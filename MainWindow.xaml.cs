using System;
using System.Linq;
using nQuant;
using System.Windows;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace ImageCompressor
{
    public class ImageData
    {
        public string FileName { get; set; }
        public string Extension { get; set; }
        public string BasePath { get; set; }
        public string FullPath { get; set; }
        public string SizeKb { get; set; }
        public string SizeMb { get; set; }
    }

    public partial class MainWindow : Window
    {
        // Properties Here
        private string _compressedDirPath;
        private bool _isBusy;
        private List<ImageData> _imageData;
        private List<ImageData> _compressableImages;

        public MainWindow()
        {
            InitializeComponent();
            //
            _imageData = new List<ImageData>();
            _compressableImages = new List<ImageData>();
            // Delete Existing Directory
            _compressedDirPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Compressed");

        }

        // Image Related Methods

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        protected void CompressAndSave(System.Drawing.Image img, string path, string extension)
        {
            //try
            //{
            //    // If jpg is extension then remove png equivalent of it
            //    // because if extension is same it'll be overridden
            //    string delFilePath = path;
            //    if (extension == ".png")
            //    {
            //        delFilePath = delFilePath.Substring(0, delFilePath.Length - 3) + "jpg";
            //    }
            //    else
            //    {
            //        delFilePath = delFilePath.Substring(0, delFilePath.Length - 3) + "png";
            //    }
            //    if (System.IO.File.Exists(delFilePath))
            //    {
            //        System.IO.File.Delete(delFilePath);
            //    }
            //}
            //catch { }
            if (extension == ".jpg" || extension == ".jpeg")
            {
                using (Bitmap bitmap = new Bitmap(img))
                {
                    ImageCodecInfo imageEncoder = null;
                    imageEncoder = GetEncoder(ImageFormat.Jpeg);
                    // Create an Encoder object based on the GUID  
                    // for the Quality parameter category.  
                    Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;

                    // Create an EncoderParameters object.  
                    // An EncoderParameters object has an array of EncoderParameter  
                    // objects. In this case, there is only one  
                    // EncoderParameter object in the array.  
                    EncoderParameters encodingParams = new EncoderParameters(1);

                    EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, 50L);
                    encodingParams.Param[0] = myEncoderParameter;
                    bitmap.Save(path, imageEncoder, encodingParams);
                }
            }
            else
            {
                var quantizer = new WuQuantizer();
                using (var bitmap = new Bitmap(img))
                {
                    using (var quantized = quantizer.QuantizeImage(bitmap)) //, alphaTransparency, alphaFader))
                    {
                        quantized.Save(path, ImageFormat.Png);
                    }
                }
            }

        }

        // Get Images in Directory

        private List<ImageData> GetImages(string path)
        {
            List<ImageData> images = new List<ImageData>();
            //
            string[] fileNames = Directory.GetFiles(path);
            fileNames = fileNames.Where(x => x.EndsWith(".jpg") || x.EndsWith(".jpeg") || x.EndsWith(".png")).ToArray();
            foreach (var fileName in fileNames)
            {
                ImageData fData = new ImageData();
                string[] paths = fileName.Split('\\');
                string nameWithExtension = paths.Last();
                string nameWithOutExtension = nameWithExtension.Split('.').First();
                //
                fData.FileName = nameWithOutExtension;
                fData.Extension = fileName.Split('.').Last();
                int index = fileName.IndexOf(nameWithExtension);
                fData.BasePath = fileName.Substring(0, index);
                fData.FullPath = fileName;
                //
                FileInfo fInfo = new FileInfo(fData.FullPath);
                fData.SizeKb = Math.Round((fInfo.Length * 1.0) / 1024, 2).ToString();
                fData.SizeMb = Math.Round((fInfo.Length * 1.0) / (1024 * 1024), 2).ToString();
                //
                images.Add(fData);
            }
            return images;
        }

        // Button Events

        private void btnSelect_Click(object sender, RoutedEventArgs e)
        {
            dgAll.ItemsSource = null;
            dgAll.Items.Refresh();
            lblMessage.Content = "";
            //
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    // Clear All
                    _imageData.Clear();
                    _compressableImages.Clear();
                    //
                    txtFolder.Text = fbd.SelectedPath;
                    //
                    _imageData = GetImages(fbd.SelectedPath);
                    //
                    dgAll.ItemsSource = _imageData;
                    lblImagesCount.Content = _imageData.Count;
                }
            }
        }

        private void btnFilter_Click(object sender, RoutedEventArgs e)
        {
            lblMessage.Content = "";
            //
            string strBytes = txtSizeKb.Text;
            double bytes = 0;
            if (double.TryParse(strBytes, out bytes))
            {
                _compressableImages = _imageData.Where(x => double.Parse(x.SizeKb) > bytes).ToList();
                dgCompressable.ItemsSource = _compressableImages;
            }
        }

        private void btnCompress_Click(object sender, RoutedEventArgs e)
        {
            _isBusy = true;
            btnCompress.IsEnabled = false;
            btnFilter.IsEnabled = false;
            btnSelect.IsEnabled = false;
            //
            lblMessage.Content = "** Busy, Compressing images";
            //
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += Bw_DoWork;
            bw.RunWorkerCompleted += Bw_RunWorkerCompleted;
            bw.RunWorkerAsync();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy == false)
            {
                this.Close();
            }
        }

        // Background Worker Events

        private void Bw_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                _isBusy = true;
                //
                if (_compressableImages != null)
                {
                    if (_compressableImages.Count > 0)
                    {
                        if (System.IO.Directory.Exists(_compressedDirPath))
                        {
                            DeleteDirectory(_compressedDirPath);
                        }
                        // Then Create New Directory
                        System.IO.Directory.CreateDirectory(_compressedDirPath);
                        //
                        foreach (var file in _compressableImages)
                        {
                            Image image = Image.FromFile(file.FullPath);
                            // Images to be Added to Compressed Folder
                            string newFilePath = System.IO.Path.Combine(_compressedDirPath, file.FileName + "." + file.Extension);
                            CompressAndSave(image, newFilePath, "." + file.Extension);
                        }
                        return;
                    }
                }
            }
            catch (Exception exp)
            {
                e.Result = "Error";
            }
        }

        private void Bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result != null)
            {
                _isBusy = false;
                //
                btnCompress.IsEnabled = true;
                btnFilter.IsEnabled = true;
                btnSelect.IsEnabled = true;
                //
                lblMessage.Content = "** Failure, Compression failed";
                return;
            }
            _isBusy = false;
            btnCompress.IsEnabled = true;
            btnFilter.IsEnabled = true;
            btnSelect.IsEnabled = true;
            //
            lblMessage.Content = "** Success, Compression completed";
            //
            _compressableImages = GetImages(_compressedDirPath);
            dgCompressable.ItemsSource = _compressableImages;
        }

        public void DeleteDirectory(string path)
        {
            foreach (string directory in Directory.GetDirectories(path))
            {
                DeleteDirectory(directory);
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                Directory.Delete(path, true);
            }
        }

        private void btnShowCompressed_Click(object sender, RoutedEventArgs e)
        {
            lblMessage.Content = "";
            if (System.IO.Directory.Exists(_compressedDirPath))
            {
                Process.Start(_compressedDirPath);
                return;
            }
            lblMessage.Content = "** Alert, No file exists";
        }
    }
}
