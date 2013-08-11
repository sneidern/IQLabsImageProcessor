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
//using Xceed.Wpf.Toolkit;

namespace IQLabsImageProcessor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string rawName;
        int rawWidth;
        int rawHeight;
        int rawBitwidth;
        int rawBayerOrder;
        byte[] rawData;
        double currentZoom = 1;
        ScaleTransform xform;
        BitmapSource bmpSource;
        int drawRectState = 0; // not drawing
        int rectStartX = 0;
        int rectStartY = 0;
        int rectWidth = 0;
        int rectHeight = 0;
        byte[] processData;
        bool loadingControls = true;
        int[] imageData16;
        int[] rgbData16;
        byte[] rgbData8;
        bool showingRaw;

        public MainWindow()
        {
            InitializeComponent();

            TransformGroup group = new TransformGroup();

            xform = new ScaleTransform();
            group.Children.Add(xform);

            //TranslateTransform tt = new TranslateTransform();
            //group.Children.Add(tt);

            image1.LayoutTransform = group;

            loadingControls = false; // now we start handling events from controls
        }

        private void MenuOpenRaw_Click(object sender, RoutedEventArgs e)
        {
            // Configure open file dialog box
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = "Raw File"; // Default file name
            dlg.DefaultExt = ".raw"; // Default file extension
            dlg.Filter = "Text documents (.rawt)|*.raw"; // Filter files by extension

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                try
                {
                    // Open document
                    string path = dlg.FileName;
                    string[] pathArr = path.Split('\\');
                    string fileName = pathArr.Last();

                    string[] nameInfo = fileName.Split(new string[] { "__" }, StringSplitOptions.RemoveEmptyEntries);
                    rawName = nameInfo[0];
                    string[] dataFields = nameInfo[1].Split(new char[] { 'x', '.' }, StringSplitOptions.RemoveEmptyEntries);
                    rawWidth = System.Convert.ToInt32(dataFields[0]);
                    rawHeight = System.Convert.ToInt32(dataFields[1]);
                    rawBitwidth = System.Convert.ToInt32(dataFields[2]);
                    rawBayerOrder = System.Convert.ToInt32(dataFields[3]);

                    rawData = new byte[rawWidth * rawHeight * 2];

                    using (BinaryReader b = new BinaryReader(File.Open(path, FileMode.Open)))
                    {
                        // Position and length variables.

                        // 2 bytes per pixel
                        rawData = b.ReadBytes(rawWidth * rawHeight * 2);
                        int temppixel = 0;

                        // shift to MSB aligned according to precision
                        for (int i = 0; i < rawWidth * rawHeight; i++)
                        {
                            if (rawBitwidth == 10)
                            {
                                temppixel = (rawData[i * 2 + 1] & 0x03) << 8;
                                temppixel += rawData[i * 2];
                                temppixel = temppixel << 6;
                                //temppixel -= 1024;
                            }
                            else if (rawBitwidth == 12)
                            {
                                temppixel = (rawData[i * 2 + 1] & 0x0f) << 8;
                                temppixel += rawData[i * 2];
                                temppixel = temppixel << 4;
                                //temppixel -= 1024;
                            }
                            else if (rawBitwidth == 14)
                            {
                                temppixel = (rawData[i * 2 + 1] & 0x3f) << 8;
                                temppixel += rawData[i * 2];
                                temppixel = temppixel << 2;
                                //temppixel -= 4096;
                            }

                            rawData[i * 2 + 1] = (byte)((temppixel & 0xff00) >> 8);
                            rawData[i * 2] = (byte)((temppixel & 0xff));
                        }

                        bmpSource = BitmapSource.Create(rawWidth, rawHeight, 96, 96, PixelFormats.Gray16, null, rawData, rawWidth * 2);
                        image1.Source = bmpSource;
                        showingRaw = true;
                        processData = new byte[rawWidth * rawHeight * 2]; // this will be used to modify pixel values if processing image
                        imageData16 = new int[rawWidth * rawHeight];
                        rgbData16 = new int[rawWidth * rawHeight * 3];
                        rgbData8 = new byte[rawWidth * rawHeight * 3];
                    }

                }
                catch
                {
                    return;
                }
            }

        }

        private void button_zoomIn_Click(object sender, RoutedEventArgs e)
        {
            currentZoom *= 2;
            xform.ScaleX = currentZoom;
            xform.ScaleY = currentZoom;
            label_zoomRatio.Content = Math.Round(currentZoom * 100) + "%";
            image1.LayoutTransform = xform;

            //scrollViewer1.ScrollableWidth = bmpSource.Width * currentZoom;
        }

        private void button_zoomOut_Click(object sender, RoutedEventArgs e)
        {
            currentZoom /= 2;
            xform.ScaleX = currentZoom;
            xform.ScaleY = currentZoom;
            label_zoomRatio.Content = Math.Round(currentZoom * 100) + "%";
            image1.LayoutTransform = xform;
        }

        private void image1_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            Point loc = e.GetPosition(image1);

            statusF1.Content = "x:" + (int)loc.X + " " + "y:" + (int)loc.Y; // jsut write x,y coordinates in status bar

            if (showingRaw == false)
            {
                byte[] pixels = new byte[4]; // allocate mem for current pixel data values
                //int[] col = new int[4]; // color values

                //int x = (int)(loc.X) / 2 * 2; // force even number, otherwise bayer order is not constant
                //int y = (int)(loc.Y) / 2 * 2;

                //x = Math.Min(x, rawWidth - 2); // force 2x2 region inside actual image
                //y = Math.Min(y, rawHeight - 2);

                CroppedBitmap chunk = new CroppedBitmap(bmpSource, new Int32Rect((int)(loc.X), (int)(loc.Y), 1, 1)); // get 2x2 region from source

                try
                {
                    chunk.CopyPixels(pixels, (bmpSource.Format.BitsPerPixel / 8), 0); // stuff data into 4 pixel (8 byte) array
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                statusF2.Content = "R: " + pixels[0] + " G: " + pixels[1] + " B: " + pixels[2];
            }
            else
            {

                byte[] pixels = new byte[8]; // allocate mem for current pixel data values
                int[] col = new int[4]; // color values

                int x = (int)(loc.X) / 2 * 2; // force even number, otherwise bayer order is not constant
                int y = (int)(loc.Y) / 2 * 2;

                x = Math.Min(x, rawWidth - 2); // force 2x2 region inside actual image
                y = Math.Min(y, rawHeight - 2);

                CroppedBitmap chunk = new CroppedBitmap(bmpSource, new Int32Rect(x, y, 2, 2)); // get 2x2 region from source

                try
                {
                    chunk.CopyPixels(pixels, 4, 0); // stuff data into 4 pixel (8 byte) array
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                //col[0] = (pixels[1] << 8) + pixels[0];
                //col[1] = (pixels[3] << 8) + pixels[2];
                //col[2] = (pixels[5] << 8) + pixels[4];
                //col[3] = (pixels[7] << 8) + pixels[6];

                // Shift from 16bit to actual bit precision
                for (int i = 0; i < 4; i++)
                {
                    col[i] = (pixels[i * 2 + 1] << 8) + pixels[i * 2];
                    col[i] = col[i] >> (16 - rawBitwidth);
                }

                if (rawBayerOrder == 0)
                { // BGGR
                    statusF2.Content = "R: " + col[3] + " Gr: " + col[2] + " Gb: " + col[1] + " B: " + col[0];
                }
                else if (rawBayerOrder == 1)
                { // GBRG
                    statusF2.Content = "R: " + col[2] + " Gr: " + col[3] + " Gb: " + col[0] + " B: " + col[1];
                }
                else if (rawBayerOrder == 2)
                { // RGGB
                    statusF2.Content = "R: " + col[0] + " Gr: " + col[1] + " Gb: " + col[2] + " B: " + col[3];
                }
                else if (rawBayerOrder == 3)
                { // GRBG
                    statusF2.Content = "R: " + col[1] + " Gr: " + col[0] + " Gb: " + col[3] + " B: " + col[2];
                }
            }
            // update rectangle if mouse is down
            if (drawRectState == 1)
            { // drawing                
                rectangle_cropRect.Visibility = Visibility.Visible;
                int rectPosX = (int)(((double)rectStartX * currentZoom - scrollViewer1.HorizontalOffset));
                int rectPosY = (int)(((double)rectStartY * currentZoom - scrollViewer1.VerticalOffset));

                if (bmpSource.Width * currentZoom < scrollViewer1.ActualWidth)
                    rectPosX += (int)(scrollViewer1.ActualWidth - (bmpSource.Width * currentZoom)) / 2;

                if (bmpSource.Height * currentZoom < scrollViewer1.ActualHeight)
                    rectPosY += (int)(scrollViewer1.ActualHeight - (bmpSource.Height * currentZoom)) / 2;


                int rectWidth = (int)(Math.Abs(e.GetPosition(image1).X - (double)rectStartX) * currentZoom) - 1; // -1 avoids the mouse up event captured by the rectangle itself
                int rectHeight = (int)(Math.Abs(e.GetPosition(image1).Y - (double)rectStartY) * currentZoom) - 1; // -1 avoids the mouse up event captured by the rectangle itself
                if (rectWidth < 0)
                    rectWidth = 0;
                if (rectHeight < 0)
                    rectHeight = 0;

                rectangle_cropRect.Margin = new Thickness(rectPosX, rectPosY, 0, 0);
                rectangle_cropRect.Width = rectWidth;
                rectangle_cropRect.Height = rectHeight;
            }
        }

        private void image1_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            drawRectState = 1; //drawing
            rectStartX = (int)e.GetPosition(image1).X;
            rectStartY = (int)e.GetPosition(image1).Y;

            rectangle_cropRect.Width = 4;
            rectangle_cropRect.Height = 4;
        }

        private void image1_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            drawRectState = 0; // not drawing
            rectHeight = (int)e.GetPosition(image1).Y - rectStartY;
            rectWidth = (int)e.GetPosition(image1).X - rectStartX;
            getStatsOfROI(rectStartX, rectStartY, rectWidth, rectHeight, 0);
        }

        private void getStatsOfROI(int x, int y, int width, int height, int measureType)
        {
            if (measureType == 1)
            { // Macbeth 24 values from Raw

                width = width / 2 * 2;
                height = height / 2 * 2;
                byte[] pixels = new byte[width * height * 2]; // allocate mem for current pixel data values
                int[] values = new int[width * height]; // color values
                double[] cumSquareDiff = new double[4];

                x = x / 2 * 2; // force even number, otherwise bayer order is not constant
                y = y / 2 * 2;

                //x = Math.Min(x, rawWidth - 2); // force 2x2 region inside actual image
                //y = Math.Min(y, rawHeight - 2);

                CroppedBitmap chunk = new CroppedBitmap(bmpSource, new Int32Rect(x, y, width, height)); // get 2x2 region from source

                try
                {
                    chunk.CopyPixels(pixels, width * 2, 0); // stuff data into 4 pixel (8 byte) array
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                // Shift from 16bit to 10 bits for analysis
                for (int i = 0; i < width * height; i++)
                {
                    values[i] = (pixels[i * 2 + 1] << 8) + pixels[i * 2];
                    values[i] = values[i] >> 6;
                }

                double[,] meanvalues = new double[24, 4];
                //double[] cumSquareDiff = new double[3];

                // reset averages
                for (int i = 0; i < 24 * 4; i++)
                    meanvalues[i / 4, i % 4] = 0;

                // get nr pixels in each patch
                int[] pixelcount = new int[24];

                // calculate average colors
                int patchrow = 0, patchcol, row;
                for (row = 0; row < height; row++)
                {

                    if ((row > (0.06 * height)) && (row < (0.15 * height)))
                        patchrow = 0;
                    else if ((row > (0.33 * height)) && (row < (0.42 * height)))
                        patchrow = 1;
                    else if ((row > (0.58 * height)) && (row < (0.67 * height)))
                        patchrow = 2;
                    else if ((row > (0.84 * height)) && (row < (0.93 * height)))
                        patchrow = 3;
                    else
                        patchrow = 4;


                    for (int col = 0; col < width; col++)
                    {

                        if ((col > (0.04 * width)) && (col < (0.1 * width)))
                            patchcol = 0;
                        else if ((col > (0.21 * width)) && (col < (0.27 * width)))
                            patchcol = 1;
                        else if ((col > (0.38 * width)) && (col < (0.44 * width)))
                            patchcol = 2;
                        else if ((col > (0.56 * width)) && (col < (0.62 * width)))
                            patchcol = 3;
                        else if ((col > (0.72 * width)) && (col < (0.78 * width)))
                            patchcol = 4;
                        else if ((col > (0.89 * width)) && (col < (0.95 * width)))
                            patchcol = 5;
                        else
                            patchcol = 6;

                        if ((patchcol < 6) && (patchrow < 4))
                        {
                            meanvalues[patchrow * 6 + patchcol, (row % 2) * 2 + col % 2] += values[row * width + col]; // assign index according to bayer position
                            pixelcount[patchrow * 6 + patchcol]++;
                        }
                    }
                }

                // divide by pixel count
                for (int i = 0; i < 24; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        meanvalues[i, j] /= pixelcount[i] / 4;
                        //meanvalues[i,j] = Math.Round(meanvalues[i,j]);
                    }
                }

                // apply OB offset and do gain correction
                int OBLevel = (int)num_OBLevel.Value;
                double OBComp = 4095.0 / (4095.0 - OBLevel);

                for (int i = 0; i < 24; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        meanvalues[i, j] -= OBLevel >> 2;
                        meanvalues[i, j] *= OBComp;
                    }
                }

                // reorder colors to B,G,G,R
                double temp;
                for (int i = 0; i < 24; i++)
                {
                    //for (int j = 0; j < 4; j++) {
                    if (rawBayerOrder == 2)
                    {
                        temp = meanvalues[i, 0];
                        meanvalues[i, 0] = meanvalues[i, 3];
                        meanvalues[i, 3] = temp;
                    }
                    //}
                }

                // measure WB correction
                double[,] WBRatios = new double[4, 2]; // 4 patches, 2 ratios (R/G, B/G)
                //for (int i = 0; i < 24; i++) {
                for (int j = 0; j < 4; j++)
                {
                    WBRatios[j, 0] = meanvalues[j + 19, 1] / meanvalues[j + 19, 0];
                    WBRatios[j, 1] = meanvalues[j + 19, 2] / meanvalues[j + 19, 3];
                }
                //}

                // calculate WB correction
                double RGain = 0, BGain = 0;
                for (int i = 0; i < 4; i++)
                {
                    BGain += WBRatios[i, 0];
                    RGain += WBRatios[i, 1];
                }
                RGain /= 4;
                BGain /= 4;

                // apply WB correction
                for (int i = 0; i < 24; i++)
                {
                    //for (int j = 0; j < 4; j++) {
                    meanvalues[i, 0] = meanvalues[i, 0] * BGain;
                    meanvalues[i, 3] = meanvalues[i, 3] * RGain;
                    //WBRatios[j, 0] = meanvalues[j + 19, 0] / meanvalues[j + 19, 1];
                    //WBRatios[j, 1] = meanvalues[j + 19, 3] / meanvalues[j + 19, 2];
                    //}
                }
                // measure exposure compensation
                double[] GainRatios = new double[4]; // 4 patches, green channel
                double[] macBethGreens = new double[4] { 148.4 * 4, 90.6 * 4, 47.8 * 4, 22.2 * 4 };
                for (int j = 0; j < 4; j++)
                {
                    GainRatios[j] = macBethGreens[j] / meanvalues[j + 19, 2];
                }

                // calculate exposure compensation
                double ExposureComp = 0;
                for (int i = 0; i < 4; i++)
                {
                    ExposureComp += GainRatios[i];
                }
                ExposureComp /= 4;

                // apply exposure compensation
                for (int i = 0; i < 24; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        meanvalues[i, j] = meanvalues[i, j] * ExposureComp;
                    }
                }

                // target macbeth color values (linear RGB from sRGB)

                double[][] macBethColors = 
                {new double[]{44,21,15},
                new double[]{140,76,55},
                new double[]{28,50,86},
                new double[]{27,38,13},
                new double[]{57,56,110},
                new double[]{31,132,103},
                new double[]{183,51,7},
                new double[]{17,27,100},
                new double[]{138,23,31},
                new double[]{27,11,36},
                new double[]{90,129,12},
                new double[]{199,90,6},
                new double[]{6,13,74},
                new double[]{17,77,16},
                new double[]{109,8,10},
                new double[]{219,147,2},
                new double[]{128,23,78},
                new double[]{0,63,98},
                new double[]{234,233,222},
                new double[]{148,151,149},
                new double[]{91,92,92},
                new double[]{48,49,49},
                new double[]{22,23,23},
                new double[]{8,8,8}};

                double[][] inputRGB = { new double[24], new double[24], new double[24] };
                for (int i = 0; i < 24; i++)
                {
                    inputRGB[0][i] = meanvalues[i, 3] / 4;
                    inputRGB[1][i] = meanvalues[i, 1] / 4;
                    inputRGB[2][i] = meanvalues[i, 0] / 4;
                }

                DotNetMatrix.GeneralMatrix RGBin = new DotNetMatrix.GeneralMatrix(inputRGB);
                RGBin = RGBin.Transpose();

                double[][] RedOut = { new double[24] };
                double[][] GreenOut = { new double[24] };
                double[][] BlueOut = { new double[24] };

                for (int i = 0; i < 24; i++)
                {
                    RedOut[0][i] = macBethColors[i][0];
                    GreenOut[0][i] = macBethColors[i][1];
                    BlueOut[0][i] = macBethColors[i][2];
                }

                DotNetMatrix.GeneralMatrix invA = RGBin.Inverse();

                DotNetMatrix.GeneralMatrix b = new DotNetMatrix.GeneralMatrix(RedOut);
                DotNetMatrix.GeneralMatrix bT = b.Transpose();
                DotNetMatrix.GeneralMatrix RGB2RGB_R = invA.Multiply(bT);

                b = new DotNetMatrix.GeneralMatrix(GreenOut);
                bT = b.Transpose();
                DotNetMatrix.GeneralMatrix RGB2RGB_G = invA.Multiply(bT);

                b = new DotNetMatrix.GeneralMatrix(BlueOut);
                bT = b.Transpose();
                DotNetMatrix.GeneralMatrix RGB2RGB_B = invA.Multiply(bT);

                loadingControls = true;
                num_exposuregain.Value = (decimal)ExposureComp;
                num_RGain.Value = (decimal)RGain;
                num_BGain.Value = (decimal)BGain;
                decimal_RGB_RR.Value = 1 - (decimal)RGB2RGB_R.Array[1][0] - (decimal)RGB2RGB_R.Array[2][0]; //(decimal)RGB2RGB_R.Array[0][0];
                decimal_RGB_RG.Value = (decimal)RGB2RGB_R.Array[1][0];
                decimal_RGB_RB.Value = (decimal)RGB2RGB_R.Array[2][0];
                decimal_RGB_GR.Value = (decimal)RGB2RGB_G.Array[0][0];
                decimal_RGB_GG.Value = 1 - (decimal)RGB2RGB_G.Array[0][0] - (decimal)RGB2RGB_G.Array[2][0]; //(decimal)RGB2RGB_G.Array[0][0];
                decimal_RGB_GB.Value = (decimal)RGB2RGB_G.Array[2][0];
                decimal_RGB_BR.Value = (decimal)RGB2RGB_B.Array[0][0];
                decimal_RGB_BG.Value = (decimal)RGB2RGB_B.Array[1][0];
                decimal_RGB_BB.Value = 1 - (decimal)RGB2RGB_B.Array[0][0] - (decimal)RGB2RGB_B.Array[1][0];//(decimal)RGB2RGB_B.Array[2][0];
                loadingControls = false;


                return;
            }
            if (showingRaw == false)
            {
                if (width < 1 || height < 1)
                    return;

                byte[] pixels = new byte[width * height * 4]; // allocate mem for current pixel data values


                CroppedBitmap chunk = new CroppedBitmap(bmpSource, new Int32Rect(x, y, width, height)); // get crop region from source

                try
                {
                    chunk.CopyPixels(pixels, width * (bmpSource.Format.BitsPerPixel / 8), 0); // stuff data into 4 pixel (8 byte) array
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                if (measureType == 0)
                {
                    int bpp = bmpSource.Format.BitsPerPixel / 8;

                    double[] meanvalues = new double[bpp];
                    double[] cumSquareDiff = new double[bpp];

                    // reset averages
                    for (int i = 0; i < bpp; i++)
                        meanvalues[i] = 0;

                    // calculate average colors
                    for (int i = 0; i < height * width * bpp; i++)
                    {
                        meanvalues[i % bpp] += pixels[i]; // assign index according to bayer position
                    }

                    // divide by pixel count
                    for (int i = 0; i < bpp; i++)
                    {
                        meanvalues[i] /= width * height;
                        meanvalues[i] = Math.Round(meanvalues[i]);
                    }

                    // reset cumulative square difference
                    for (int i = 0; i < bpp; i++)
                        cumSquareDiff[i] = 0;

                    // calculate cumulative variance
                    for (int i = 0; i < height * width * bpp; i++)
                    {
                        cumSquareDiff[i % bpp] += Math.Pow(pixels[i] - meanvalues[i % bpp], 2); // assign index according to color
                    }

                    // divide variance by pixel count
                    for (int i = 0; i < bpp; i++)
                        cumSquareDiff[i] /= width * height;

                    // get square root, for std dev.
                    for (int i = 0; i < bpp; i++)
                        cumSquareDiff[i] = Math.Round(Math.Sqrt(cumSquareDiff[i]), 2);

                    statusF3.Content = "Av R: " + meanvalues[0] + " Av G: " + meanvalues[1] + " Av B: " + meanvalues[2];
                    statusF4.Content = "SD R: " + cumSquareDiff[0] + " SD G: " + cumSquareDiff[1] + " SD B: " + cumSquareDiff[2];
                }

            }
            else
            {
                if (width < 2 || height < 2)
                    return;

                width = width / 2 * 2;
                height = height / 2 * 2;
                byte[] pixels = new byte[width * height * 2]; // allocate mem for current pixel data values
                int[] values = new int[width * height]; // color values
                double[] meanvalues = new double[4];
                double[] cumSquareDiff = new double[4];

                x = x / 2 * 2; // force even number, otherwise bayer order is not constant
                y = y / 2 * 2;

                //x = Math.Min(x, rawWidth - 2); // force 2x2 region inside actual image
                //y = Math.Min(y, rawHeight - 2);

                CroppedBitmap chunk = new CroppedBitmap(bmpSource, new Int32Rect(x, y, width, height)); // get 2x2 region from source

                try
                {
                    chunk.CopyPixels(pixels, width * 2, 0); // stuff data into 4 pixel (8 byte) array
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                // Shift from 16bit to actual bit precision
                for (int i = 0; i < width * height; i++)
                {
                    values[i] = (pixels[i * 2 + 1] << 8) + pixels[i * 2];
                    values[i] = values[i] >> (16 - rawBitwidth);
                }

                // reset averages
                for (int i = 0; i < 4; i++)
                    meanvalues[i] = 0;

                // calculate average colors
                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        meanvalues[(row % 2) * 2 + col % 2] += values[row * width + col]; // assign index according to bayer position
                    }
                }

                // divide by pixel count
                for (int i = 0; i < 4; i++)
                {
                    meanvalues[i] /= width * height / 4;
                    meanvalues[i] = Math.Round(meanvalues[i]);
                }

                // reset cumulative square difference
                for (int i = 0; i < 4; i++)
                    cumSquareDiff[i] = 0;

                // calculate cumulative variance
                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        cumSquareDiff[(row % 2) * 2 + col % 2] += Math.Pow(values[row * width + col] - meanvalues[(row % 2) * 2 + col % 2], 2); // assign index according to bayer position
                    }
                }

                // divide variance by pixel count
                for (int i = 0; i < 4; i++)
                    cumSquareDiff[i] /= width * height / 4;

                // get square root, for std dev.
                for (int i = 0; i < 4; i++)
                    cumSquareDiff[i] = Math.Round(Math.Sqrt(cumSquareDiff[i]), 1);

                if (rawBayerOrder == 0)
                { // BGGR
                    statusF3.Content = "Av R: " + meanvalues[3] + " Av Gr: " + meanvalues[2] + " Av Gb: " + meanvalues[1] + " Av B: " + meanvalues[0];
                    statusF4.Content = "SD R: " + cumSquareDiff[3] + " SD Gr: " + cumSquareDiff[2] + " SD Gb: " + cumSquareDiff[1] + " SD B: " + cumSquareDiff[0];
                }
                else if (rawBayerOrder == 1)
                { // GBRG
                    statusF3.Content = "Av R: " + meanvalues[2] + " Av Gr: " + meanvalues[3] + " Av Gb: " + meanvalues[0] + " Av B: " + meanvalues[1];
                    statusF4.Content = "SD R: " + cumSquareDiff[2] + " SD Gr: " + cumSquareDiff[3] + " SD Gb: " + cumSquareDiff[0] + " SD B: " + cumSquareDiff[1];
                }
                else if (rawBayerOrder == 2)
                { // RGGB
                    statusF3.Content = "Av R: " + meanvalues[0] + " Av Gr: " + meanvalues[1] + " Av Gb: " + meanvalues[2] + " Av B: " + meanvalues[3];
                    statusF4.Content = "SD R: " + cumSquareDiff[0] + " SD Gr: " + cumSquareDiff[1] + " SD Gb: " + cumSquareDiff[2] + " SD B: " + cumSquareDiff[3];
                }
                else if (rawBayerOrder == 3)
                { // GRBG
                    statusF3.Content = "Av R: " + meanvalues[1] + " Av Gr: " + meanvalues[0] + " Av Gb: " + meanvalues[3] + " Av B: " + meanvalues[2];
                    statusF4.Content = "SD R: " + cumSquareDiff[1] + " SD Gr: " + cumSquareDiff[0] + " SD Gb: " + cumSquareDiff[3] + " SD B: " + cumSquareDiff[2];
                }
            }
        }

        private void processRawImage()
        {
            // convert to ints for processing
            for (int i = 0; i < rawWidth * rawHeight; i++)
                imageData16[i] = (rawData[i * 2 + 1] << 8) + rawData[i * 2];

            if (radioButton_pureRaw.IsChecked == true)
            {
                updateDisplayImage();
                return;
            }

            // process

            // OB and gain compensation
            int OBGainComp = 4096; // no change
            int OBGainValue = (int)num_OBLevel.Value;
            if (checkBox_OBGainComp.IsChecked == true)
                OBGainComp = (4096 - (int)num_OBLevel.Value);

            for (int i = 0; i < rawWidth * rawHeight; i++)
            {
                imageData16[i] = imageData16[i] - (OBGainValue << 4);
                imageData16[i] = (imageData16[i] * 4096) / OBGainComp;

                if (imageData16[i] < 0)
                    imageData16[i] = 0;
                if (imageData16[i] > 65535)
                    imageData16[i] = 65535;
            }

            if (radioButton_OpticalBlack.IsChecked == true)
            {
                updateDisplayImage();
                return;
            }

            // White Balance and Flare
            int[] gains = new int[4];

            switch (rawBayerOrder)
            {
                case 0:
                    //gains[3] = (int)(num_RGain.Value * 512); gains[1] = (int)(num_GGain.Value * 512); gains[2] = (int)(num_GGain.Value * 512); gains[0] = (int)(num_BGain.Value * 512);
                    gains[3] = (int)(num_RGain.Value * num_exposuregain.Value * 512); gains[1] = (int)(num_GGain.Value * num_exposuregain.Value * 512); gains[2] = (int)(num_GGain.Value * num_exposuregain.Value * 512); gains[0] = (int)(num_BGain.Value * num_exposuregain.Value * 512);
                    break;
                case 1:
                    gains[0] = (int)(num_RGain.Value * 512); gains[1] = (int)(num_GGain.Value * 512); gains[2] = (int)(num_GGain.Value * 512); gains[3] = (int)(num_BGain.Value * 512);
                    break;
                case 2:
                    gains[0] = (int)(num_RGain.Value * num_exposuregain.Value * 512); gains[1] = (int)(num_GGain.Value * num_exposuregain.Value * 512); gains[2] = (int)(num_GGain.Value * num_exposuregain.Value * 512); gains[3] = (int)(num_BGain.Value * num_exposuregain.Value * 512);
                    break;
                case 3:
                    gains[0] = (int)(num_RGain.Value * 512); gains[1] = (int)(num_GGain.Value * 512); gains[2] = (int)(num_GGain.Value * 512); gains[3] = (int)(num_BGain.Value * 512);
                    break;
            }

            int color;
            int FlareLevel = (int)num_FlareLevel.Value;
            //int ExposureLevel = (int)num_exposuregain.Value;

            for (int row = 0; row < rawHeight; row++)
            {
                for (int col = 0; col < rawWidth; col++)
                {
                    color = ((row % 2) << 1) + col % 2;

                    imageData16[row * rawWidth + col] *= gains[color];
                    imageData16[row * rawWidth + col] = imageData16[row * rawWidth + col] >> 9;
                    imageData16[row * rawWidth + col] -= FlareLevel << 4;
                    if (imageData16[row * rawWidth + col] < 0)
                        imageData16[row * rawWidth + col] = 0;
                    if (imageData16[row * rawWidth + col] > 65535)
                        imageData16[row * rawWidth + col] = 65535;
                }
            }

            if (radioButton_WhiteBalance.IsChecked == true)
            {
                updateDisplayImage();
                return;
            }

            // Demosaic
            int mapR = 0, mapG = 0, mapB = 0;

            for (int row = 2; row < rawHeight - 2; row++)
            {
                for (int col = 2; col < rawWidth - 2; col++)
                {
                    color = ((row % 2) << 1) + col % 2;

                    int bpix = row * rawWidth + col;

                    if (rawBayerOrder == 1)
                    {
                        if (color == 0)
                            color = 1;
                        else if (color == 1)
                            color = 0;
                        else if (color == 2)
                            color = 3;
                        else if (color == 3)
                            color = 2;
                    }

                    else if (rawBayerOrder == 2)
                    {
                        if (color == 0)
                            color = 3;
                        else if (color == 1)
                            color = 2;
                        else if (color == 2)
                            color = 1;
                        else if (color == 3)
                            color = 0;
                    }

                    else if (rawBayerOrder == 3)
                    {
                        if (color == 0)
                            color = 2;
                        else if (color == 1)
                            color = 3;
                        else if (color == 2)
                            color = 0;
                        else if (color == 3)
                            color = 1;
                    }

                    switch (color)
                    {
                        case 0: //Red
                            rgbData16[bpix * 3 + 2] = Math.Min(imageData16[bpix], 65535); // Red from current pixel
                            rgbData16[bpix * 3 + 1] = Math.Min((imageData16[bpix - rawWidth] + imageData16[bpix + rawWidth] + imageData16[bpix - 1] + imageData16[bpix + 1] + 2) >> 2, 65535); // Green from 4 sides
                            rgbData16[bpix * 3 + 0] = Math.Min((imageData16[bpix - rawWidth - 1] + imageData16[bpix + rawWidth - 1] + imageData16[bpix - rawWidth + 1] + imageData16[bpix + rawWidth + 1] + 2) >> 2, 65535); // Blue from 4 diagonal
                            break;
                        case 1: // Green/Red
                            rgbData16[bpix * 3 + 2] = Math.Min((imageData16[bpix - 1] + imageData16[bpix + 1] + 1) >> 1, 65535); // Red from left/right
                            rgbData16[bpix * 3 + 1] = Math.Min(imageData16[bpix], 65535); // green from current pixel
                            rgbData16[bpix * 3 + 0] = Math.Min((imageData16[bpix - rawWidth] + imageData16[bpix + rawWidth] + 1) >> 1, 65535); // Blue from top/bottom
                            break;
                        case 2: // Green/Blue
                            rgbData16[bpix * 3 + 2] = Math.Min((imageData16[bpix - rawWidth] + imageData16[bpix + rawWidth] + 1) >> 1, 65535); // Red from top/bottom
                            rgbData16[bpix * 3 + 1] = Math.Min(imageData16[bpix], 65535); // green from current pixel
                            rgbData16[bpix * 3 + 0] = Math.Min((imageData16[bpix - 1] + imageData16[bpix + 1] + 1) >> 1, 65535); // Blue from left/right
                            break;
                        case 3: // Blue
                            rgbData16[bpix * 3 + 2] = Math.Min((imageData16[bpix - rawWidth - 1] + imageData16[bpix + rawWidth - 1] + imageData16[bpix - rawWidth + 1] + imageData16[bpix + rawWidth + 1] + 2) >> 2, 65535); // Red from 4 diagonal
                            rgbData16[bpix * 3 + 1] = Math.Min((imageData16[bpix - rawWidth] + imageData16[bpix + rawWidth] + imageData16[bpix - 1] + imageData16[bpix + 1] + 2) >> 2, 65535); // Green from 4 sides
                            rgbData16[bpix * 3 + 0] = Math.Min(imageData16[bpix], 65535); // blue from current pixel
                            break;
                        default:
                            break;
                    }
                }
            }

            if (radioButton_Demosaic.IsChecked == true)
            {
                updateDisplayImageRGB();
                return;
            }

            // RGB to RGB matrix
            int iRR = (int)(decimal_RGB_RR.Value * 512);
            int iRG = (int)(decimal_RGB_RG.Value * 512);
            int iRB = (int)(decimal_RGB_RB.Value * 512);
            int iGR = (int)(decimal_RGB_GR.Value * 512);
            int iGG = (int)(decimal_RGB_GG.Value * 512);
            int iGB = (int)(decimal_RGB_GB.Value * 512);
            int iBR = (int)(decimal_RGB_BR.Value * 512);
            int iBG = (int)(decimal_RGB_BG.Value * 512);
            int iBB = (int)(decimal_RGB_BB.Value * 512);
            int tempR, tempG, tempB;

            for (int i = 0; i < rawWidth * rawHeight; i++)
            {
                if (i == 1033911)
                    i = i;
                tempR = ((rgbData16[i * 3] * iRR) + (rgbData16[i * 3 + 1] * iRG) + (rgbData16[i * 3 + 2] * iRB)) >> 9;
                tempG = ((rgbData16[i * 3] * iGR) + (rgbData16[i * 3 + 1] * iGG) + (rgbData16[i * 3 + 2] * iGB)) >> 9;
                tempB = ((rgbData16[i * 3] * iBR) + (rgbData16[i * 3 + 1] * iBG) + (rgbData16[i * 3 + 2] * iBB)) >> 9;

                if (tempR < 0) tempR = 0;
                if (tempG < 0) tempG = 0;
                if (tempB < 0) tempB = 0;
                if (tempR > 65535) tempR = 65535;
                if (tempG > 65535) tempG = 65535;
                if (tempB > 65535) tempB = 65535;

                rgbData16[i * 3] = tempR;
                rgbData16[i * 3 + 1] = tempG;
                rgbData16[i * 3 + 2] = tempB;

            }

            // tonecurve - sRGB is enough here

            // generate sRGB tonecurve
            int[] tonecurveLUT = new int[4096];
            for (int i = 1; i < 4094; i++)
            {
                if (i < 13)
                    tonecurveLUT[i] = (int)(12.92 * (double)i);
                else
                    tonecurveLUT[i] = (int)((1.055 * Math.Pow((double)i / (double)4095, 1 / 2.4) - 0.055) * 4095);
            }
            tonecurveLUT[0] = 0;
            tonecurveLUT[4095] = 4095;

            for (int i = 0; i < rawWidth * rawHeight * 3; i++)
            {
                rgbData16[i] = tonecurveLUT[rgbData16[i] >> 4] << 4;
            }

            if (radioButton_Tonecurve.IsChecked == true)
            {
                updateDisplayImageRGB();
                return;
            }
        }

        private void updateDisplayImageRGB()
        {
            // convert back to byte array
            for (int i = 0; i < rawWidth * rawHeight * 3; i++)
            {
                rgbData8[i] = (byte)(rgbData16[i] >> 8);
            }

            // put back in bitmapsource
            bmpSource = BitmapSource.Create(rawWidth, rawHeight, 96, 96, PixelFormats.Rgb24, null, rgbData8, rawWidth * 3);
            image1.Source = bmpSource;
            showingRaw = false;

            getStatsOfROI(rectStartX, rectStartY, rectWidth, rectHeight, 0);
        }

        private void updateDisplayImage()
        {
            // convert back to byte array
            for (int i = 0; i < rawWidth * rawHeight; i++)
            {
                processData[i * 2 + 1] = (byte)(imageData16[i] >> 8);
                processData[i * 2] = (byte)(imageData16[i] & 0xff);
            }

            // put back in bitmapsource
            bmpSource = BitmapSource.Create(rawWidth, rawHeight, 96, 96, PixelFormats.Gray16, null, processData, rawWidth * 2);
            image1.Source = bmpSource;
            showingRaw = true;
            // refresh statistics
            getStatsOfROI(rectStartX, rectStartY, rectWidth, rectHeight, 0);
        }


        private void IntegerUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (loadingControls == true)
                return;

            processRawImage();
        }

        private void MenuSRGBCalibration_Click(object sender, RoutedEventArgs e)
        {
            getStatsOfROI(rectStartX, rectStartY, rectWidth, rectHeight, 1); // get Macbeth values in ROI
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

            processRawImage();
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
    }
}
