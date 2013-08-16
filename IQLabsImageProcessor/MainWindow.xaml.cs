using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO; // for file IO

namespace IQLabsImageProcessor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public struct ROISelect {
            public int X;
            public int Y;
            public int width;
            public int height;
            public int drawRectState; // not drawing
        }

        public struct ImageInfo {
            public string rawName;
            public int rawWidth;
            public int rawHeight;
            public int rawBitwidth;
            public int rawBayerOrder;
        }

        // globals
        ScaleTransform xform;
        bool showingRaw;

        // global structs
        private ROISelect selection;
        private ImageInfo openImage;

        // image buffers        
        BitmapSource bmpSource;
        bool loadingControls = true;
        byte[] rawData;
        int[] rawData16;
        int[] rgbData16;
        byte[] rgbData8;
        
        // classes
        imagestats stats;
        imagepipeline pipe;
        rawdataparser rawparser;

        public MainWindow()
        {
            InitializeComponent();

            TransformGroup group = new TransformGroup();
            xform = new ScaleTransform();
            group.Children.Add(xform);
            image1.LayoutTransform = group;

            stats = new imagestats();
            pipe = new imagepipeline();
            rawparser = new rawdataparser();

            selection.drawRectState = 0;

            loadingControls = false; // now we start handling events from controls
        }

        private void MenuOpenRaw_Click(object sender, RoutedEventArgs e)
        {
            // Configure open file dialog box
            string fileName = null;

            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = "Raw File"; // Default file name
            dlg.DefaultExt = ".raw"; // Default file extension
            dlg.Filter = "Bayer Image (.raw)|*.raw"; // Filter files by extension

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                try // Open raw image
                {
                    string path = dlg.FileName;
                    string[] pathArr = path.Split('\\');
                    fileName = pathArr.Last();

                    openImage = rawparser.parseRawFileName(fileName);

                    // tell statistics engine what image was loaded
                    stats.BayerOrder = openImage.rawBayerOrder;
                    stats.bitDepth = openImage.rawBitwidth;
                    stats.imageType = 0;

                    rawData = new byte[openImage.rawWidth * openImage.rawHeight * 2];
                    rawparser.readRawFile(path, ref rawData, openImage);

                    bmpSource = BitmapSource.Create(openImage.rawWidth, openImage.rawHeight, 96, 96, PixelFormats.Gray16, null, rawData, openImage.rawWidth * 2);
                    image1.Source = bmpSource;
                    showingRaw = true;
                    rawData16 = new int[openImage.rawWidth * openImage.rawHeight];
                    rgbData8 = new byte[openImage.rawWidth * openImage.rawHeight * 3];
                    rgbData16 = new int[openImage.rawWidth * openImage.rawHeight * 3];
                }
                catch
                {
                    MessageBox.Show(fileName + " is not correct format. Should be name__WidthxHeightxPrecisionxOrder");
                    return;
                }
            }
        }

        private void button_zoomIn_Click(object sender, RoutedEventArgs e)
        {
            xform.ScaleX *= 2;
            xform.ScaleY *= 2;
            label_zoomRatio.Content = Math.Round(xform.ScaleX * 100) + "%";
            image1.LayoutTransform = xform;
            //scrollViewer1.ScrollableWidth = bmpSource.Width * currentZoom;
        }

        private void button_zoomOut_Click(object sender, RoutedEventArgs e)
        {
            xform.ScaleX /= 2;
            xform.ScaleY /= 2;
            label_zoomRatio.Content = Math.Round(xform.ScaleX * 100) + "%";
            image1.LayoutTransform = xform;
        }

        private void image1_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            Point loc = e.GetPosition(image1);

            statusF1.Content = "x:" + (int)loc.X + " " + "y:" + (int)loc.Y; // just write x,y coordinates in status bar

            if (showingRaw == false) // BMP file on screen
            {
                byte[] pixels = getPixelsFromBMPImage(loc, 1, 1);
                statusF2.Content = "R: " + pixels[0] + " G: " + pixels[1] + " B: " + pixels[2];
            } else // RAW file on screen
            {
                byte[] pixels = getPixelsFromRawImage(loc, 2, 2);
                stats.calculateStats(pixels, 2, 2);
                imagestats.colorvals pixelgroup = stats.getMeanValues();
                statusF2.Content = "R: " + pixelgroup.R + " Gr: " + pixelgroup.GR + " Gb: " + pixelgroup.GB + " B: " + pixelgroup.B;
            }

            // update rectangle if mouse is down
            if (selection.drawRectState == 1)
            { // drawing                
                rectangle_cropRect.Visibility = Visibility.Visible;
                int rectPosX = (int)(((double)selection.X * xform.ScaleX - scrollViewer1.HorizontalOffset));
                int rectPosY = (int)(((double)selection.Y * xform.ScaleY - scrollViewer1.VerticalOffset));

                if (bmpSource.Width * xform.ScaleX < scrollViewer1.ActualWidth)
                    rectPosX += (int)(scrollViewer1.ActualWidth - (bmpSource.Width * xform.ScaleX)) / 2;

                if (bmpSource.Height * xform.ScaleY < scrollViewer1.ActualHeight)
                    rectPosY += (int)(scrollViewer1.ActualHeight - (bmpSource.Height * xform.ScaleY)) / 2;

                int rectWidth = (int)(Math.Abs(e.GetPosition(image1).X - (double)selection.X) * xform.ScaleX) - 1; // -1 avoids the mouse up event captured by the rectangle itself
                int rectHeight = (int)(Math.Abs(e.GetPosition(image1).Y - (double)selection.Y) * xform.ScaleY) - 1; // -1 avoids the mouse up event captured by the rectangle itself
                
                if (rectWidth < 0)
                    rectWidth = 0;
                if (rectHeight < 0)
                    rectHeight = 0;

                rectangle_cropRect.Margin = new Thickness(rectPosX, rectPosY, 0, 0);
                rectangle_cropRect.Width = rectWidth - (rectWidth % xform.ScaleX); // this forces rectangle to end on pixel boundaries
                rectangle_cropRect.Height = rectHeight - (rectHeight % xform.ScaleY);

                statusF1.Content = rectWidth + "," + rectHeight;
            }
        }

        private void image1_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            selection.drawRectState = 1; //drawing
            selection.X = (int)e.GetPosition(image1).X;
            selection.Y = (int)e.GetPosition(image1).Y;

            rectangle_cropRect.Width = 4;
            rectangle_cropRect.Height = 4;
        }

        private void image1_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            selection.drawRectState = 0; // not drawing
            selection.height = (int)e.GetPosition(image1).Y - selection.Y;
            selection.width = (int)e.GetPosition(image1).X - selection.X;

            if (selection.width < 2 || selection.height < 2) {
                rectangle_cropRect.Visibility = Visibility.Hidden;
                return;
            }

            if (stats.imageType == 0) // RAW
            {
                selection.width = (selection.width / 2) * 2;
                selection.height = (selection.height / 2) * 2;
                byte[] pixels = getPixelsFromRawImage(new Point(selection.X, selection.Y), selection.width, selection.height);
                stats.calculateStats(pixels, selection.width, selection.height);
                imagestats.colorvals means = stats.getMeanValues();
                imagestats.colorvals stddevs = stats.getStdDev();
                statusF3.Content = "Av R: " + means.R + " Av Gr: " + means.GR + " Av Gb: " + means.GB + " Av B: " + means.B;
                statusF4.Content = "SD R: " + stddevs.R + " SD Gr: " + stddevs.GR + " SD Gb: " + stddevs.GB + " SD B: " + stddevs.B;
            }
            else // BMP
            {
                if (selection.width < 1 || selection.height < 1)
                    return;
                byte[] pixels = getPixelsFromBMPImage(new Point(selection.X, selection.Y), selection.width, selection.height);
                stats.calculateStats(pixels, selection.width, selection.height);
                imagestats.colorvals means = stats.getMeanValues();
                imagestats.colorvals stddevs = stats.getStdDev();
                statusF3.Content = "Av R: " + means.R + " Av G: " + means.G + " Av B: " + means.B;
                statusF4.Content = "SD R: " + stddevs.R + " SD G: " + stddevs.G + " SD B: " + stddevs.B;
            }
        }
        
        private void updateDisplayImageRGB()
        {
            if ((openImage.rawWidth == 0) || (openImage.rawHeight == 0) || (rgbData8 == null))
                return;

            showingRaw = false;
            // convert back to byte array
            for (int i = 0; i < openImage.rawWidth * openImage.rawHeight * 3; i++)
            {
                rgbData8[i] = (byte)(rgbData16[i] >> 8);
            }

            // put back in bitmapsource
            bmpSource = BitmapSource.Create(openImage.rawWidth, openImage.rawHeight, 96, 96, PixelFormats.Rgb24, null, rgbData8, openImage.rawWidth * 3);
            image1.Source = bmpSource;
            showingRaw = false;
            stats.bytesperpixel = bmpSource.Format.BitsPerPixel / 8;
            stats.imageType = 1;

            if (rectangle_cropRect.Visibility == Visibility.Visible) { // recalculate stats for ROI
                byte[] pixels = getPixelsFromBMPImage(new Point(selection.X, selection.Y), selection.width, selection.height); // allocate mem for current pixel data values
                stats.calculateStats(pixels, selection.width, selection.height);
            } else {
                byte[] pixels = getPixelsFromBMPImage(new Point(0, 0), openImage.rawWidth, openImage.rawHeight); // allocate mem for current pixel data values
                stats.calculateStats(pixels, openImage.rawWidth, openImage.rawHeight);
            }

            imagestats.colorvals means = stats.getMeanValues();
            imagestats.colorvals stddevs = stats.getStdDev();

            statusF3.Content = "Av R: " + means.R + " Av G: " + means.G + " Av B: " + means.B;
            statusF4.Content = "SD R: " + stddevs.R + " SD G: " + stddevs.G + " SD B: " + stddevs.B;
        }

        private void updateDisplayImageRAW()
        {
            if ((openImage.rawWidth == 0) || (openImage.rawHeight == 0))
                return;

            showingRaw = true;
            byte[] processData = new byte[openImage.rawWidth * openImage.rawHeight * 2];

            // convert back to byte array
            for (int i = 0; i < openImage.rawWidth * openImage.rawHeight; i++)
            {
                processData[i * 2 + 1] = (byte)(rawData16[i] >> 8);
                processData[i * 2] = (byte)(rawData16[i] & 0xff);
            }

            // put back in bitmapsource
            bmpSource = BitmapSource.Create(openImage.rawWidth, openImage.rawHeight, 96, 96, PixelFormats.Gray16, null, processData, openImage.rawWidth * 2);
            image1.Source = bmpSource;
            showingRaw = true;
            stats.imageType = 0;

            // refresh statistics
            if (openImage.rawWidth < 2 || openImage.rawHeight < 2)
                return;

            openImage.rawWidth = openImage.rawWidth / 2 * 2;
            openImage.rawHeight = openImage.rawHeight / 2 * 2;

            if (rectangle_cropRect.Visibility == Visibility.Visible) { // recalculate stats for ROI
                byte[] pixels = getPixelsFromBMPImage(new Point(selection.X, selection.Y), selection.width, selection.height); // allocate mem for current pixel data values
                stats.calculateStats(pixels, selection.width, selection.height);
            } else {
                stats.calculateStats(processData, openImage.rawWidth, openImage.rawHeight);
            }

            imagestats.colorvals means = stats.getMeanValues();
            imagestats.colorvals stddevs = stats.getStdDev();

            statusF3.Content = "Av R: " + means.R + " Av Gr: " + means.GR + " Av Gb: " + means.GB + " Av B: " + means.B;
            statusF4.Content = "SD R: " + stddevs.R + " SD Gr: " + stddevs.GR + " SD Gb: " + stddevs.GB + " SD B: " + stddevs.B;
        }

        private void IntegerUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            
        }

        private void MenuSRGBCalibration_Click(object sender, RoutedEventArgs e)
        {
            selection.width = selection.width / 2 * 2;
            selection.height = selection.height / 2 * 2;
            byte[] pixels = new byte[selection.width * selection.height * 2]; // allocate mem for current pixel data values
            int[] values = new int[selection.width * selection.height]; // color values
            double[] cumSquareDiff = new double[4];

            selection.X = selection.X / 2 * 2; // force even number, otherwise bayer order is not constant
            selection.Y = selection.Y / 2 * 2;

            CroppedBitmap chunk = new CroppedBitmap(bmpSource, new Int32Rect(selection.X, selection.Y, selection.width, selection.height)); // get 2x2 region from source

            try {
                chunk.CopyPixels(pixels, selection.width * 2, 0); // stuff data into 4 pixel (8 byte) array
            } catch (Exception ex) {
                MessageBox.Show(ex.Message);
            }

            stats.calcMacbethStats(pixels, selection.width, selection.height);

            imagestats.colorvals[] macbethMeans = new imagestats.colorvals[24];

            macbethMeans = stats.getMacbethMean();
        }

        private void MenuSaveImage_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".bmp";
            dlg.Filter = "Bitmap (.bmp)|*.bmp";
            if (dlg.ShowDialog() == true)
            {
                string filename = dlg.FileName;
                try
                {
                    BmpBitmapEncoder bmp = new BmpBitmapEncoder();
                    bmp.Frames.Add(BitmapFrame.Create(bmpSource));
                    FileStream s = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write);
                    bmp.Save(s);
                    s.Close();
                }
                catch { }
            }
        }

        private void radioButton_imageChanged_Click(object sender, RoutedEventArgs e)
        {
            if (loadingControls == true)
                return;

            pipe.fillPipeConfig(this);
            pipe.runPipeline(ref rawData, ref rawData16, ref rgbData16, openImage.rawWidth, openImage.rawHeight, openImage.rawBayerOrder);

            if (pipe.getFinalStage() < (int)imagepipeline.pipestage.DM)
                updateDisplayImageRAW();
            else
                updateDisplayImageRGB();
        }

        private void MenuOpenImage_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Image Files(*.BMP;*.JPG;*.GIF;*.TIF)|*.BMP;*.JPG;*.GIF;*.TIF|All files (*.*)|*.* ";
            if (dlg.ShowDialog() == true)
            {
                Stream stream = File.Open(dlg.FileName, FileMode.Open);
                BitmapImage imgsrc = new BitmapImage();
                imgsrc.BeginInit();
                imgsrc.StreamSource = stream;
                imgsrc.EndInit();
                image1.Source = imgsrc;
                bmpSource = (BitmapSource)image1.Source;
            }
        }

        private void MenuGetMacBethStats_Click(object sender, RoutedEventArgs e)
        {
            if (stats.imageType == 0) {
                byte[] pixels = getPixelsFromRawImage(new Point(selection.X, selection.Y), selection.width, selection.height);
                stats.calcMacbethStats(pixels, selection.width, selection.height);
                imagestats.colorvals[] macbethMeans = new imagestats.colorvals[24];
                macbethMeans = stats.getMacbethMean();
            } else // BMP
            {
                if (selection.width < 1 || selection.height < 1)
                    return;
                byte[] pixels = getPixelsFromBMPImage(new Point(selection.X, selection.Y), selection.width, selection.height);
                stats.calcMacbethStats(pixels, selection.width, selection.height);
                imagestats.colorvals[] macbethMeans = new imagestats.colorvals[24];
                macbethMeans = stats.getMacbethMean();
            }
        }

        private void IntegerUpDown_ValueChanged(object sender, KeyEventArgs e)
        {
            if (loadingControls == true)
                return;

            if (e.Key != Key.Return)
                return;

                pipe.fillPipeConfig(this);
                pipe.runPipeline(ref rawData, ref rawData16, ref rgbData16, openImage.rawWidth, openImage.rawHeight, openImage.rawBayerOrder);

            if (pipe.getFinalStage() < (int)imagepipeline.pipestage.DM)
                updateDisplayImageRAW();
            else
                updateDisplayImageRGB();
        }

        private byte[] getPixelsFromRawImage(Point loc, int width, int height)
        {
            
            //int[] col = new int[4]; // color values

            int x = (int)(loc.X) / 2 * 2; // force even number, otherwise bayer order is not constant
            int y = (int)(loc.Y) / 2 * 2;

            x = Math.Min(x, openImage.rawWidth - 2); // force 2x2 region inside actual image
            y = Math.Min(y, openImage.rawHeight - 2);

            width = width / 2 * 2;
            height = height / 2 * 2;

            byte[] pixels = new byte[width * height * 2]; // allocate mem for current pixel data values

            CroppedBitmap chunk = new CroppedBitmap(bmpSource, new Int32Rect(x, y, width, height)); // get 2x2 region from source

            try {
                chunk.CopyPixels(pixels, width*2, 0); // stuff data into 4 pixel (8 byte) array
            } catch (Exception ex) {
                MessageBox.Show(ex.Message);
            }
            return pixels;
        }

        private byte[] getPixelsFromBMPImage(Point loc, int width, int height)
        {
            byte[] pixels = new byte[width * height * 4]; // allocate mem for current pixel data values
            //int[] col = new int[4]; // color values

            CroppedBitmap chunk = new CroppedBitmap(bmpSource, new Int32Rect((int)loc.X, (int)loc.Y, width, height)); // get 2x2 region from source

            try {
                chunk.CopyPixels(pixels, width * 3, 0); // stuff data into 3 byte per pixel
            } catch (Exception ex) {
                MessageBox.Show(ex.Message);
            }
            return pixels;
        }
    }
}
