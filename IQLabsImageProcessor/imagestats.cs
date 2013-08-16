using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IQLabsImageProcessor
{
    class imagestats
    {
        public struct colorvals
        {
            public double R;
            public double G;
            public double B;
            public double GR;
            public double GB;
        }

        public int BayerOrder;
        public int bitDepth;
        public int imageType; // raw=0, bmp=1
        public int bytesperpixel;
        
        private double[] meanvalues = new double[4];
        private double[] cumSquareDiff = new double[4];
        private colorvals meanvals = new colorvals();
        private colorvals stddevvals = new colorvals();
        private colorvals[] MacbethMean = new colorvals[24];

        public int calculateStats(byte[] pixels, int width, int height)
        {
            if (imageType == 0)
            {
                int[] values = new int[width * height]; // color values

                // Shift from 16bit to actual bit precision
                for (int i = 0; i < width * height; i++)
                {
                    values[i] = (pixels[i * 2 + 1] << 8) + pixels[i * 2];
                    values[i] = values[i] >> (16 - bitDepth);
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

                if (BayerOrder == 0)
                { // RGGB
                    //statusF3.Content = "Av R: " + meanvalues[3] + " Av Gr: " + meanvalues[2] + " Av Gb: " + meanvalues[1] + " Av B: " + meanvalues[0];
                    //statusF4.Content = "SD R: " + cumSquareDiff[3] + " SD Gr: " + cumSquareDiff[2] + " SD Gb: " + cumSquareDiff[1] + " SD B: " + cumSquareDiff[0];
                    meanvals.R = meanvalues[0]; meanvals.GR = meanvalues[1]; meanvals.GB = meanvalues[2]; meanvals.B = meanvalues[3];
                    stddevvals.R = cumSquareDiff[0]; stddevvals.GR = cumSquareDiff[1]; stddevvals.GB = cumSquareDiff[2]; stddevvals.B = cumSquareDiff[3];
                }
                else if (BayerOrder == 1)
                { // GBRG
                    //statusF3.Content = "Av R: " + meanvalues[2] + " Av Gr: " + meanvalues[3] + " Av Gb: " + meanvalues[0] + " Av B: " + meanvalues[1];
                    //statusF4.Content = "SD R: " + cumSquareDiff[2] + " SD Gr: " + cumSquareDiff[3] + " SD Gb: " + cumSquareDiff[0] + " SD B: " + cumSquareDiff[1];
                    meanvals.R = meanvalues[1]; meanvals.GR = meanvalues[0]; meanvals.GB = meanvalues[3]; meanvals.B = meanvalues[2];
                    stddevvals.R = cumSquareDiff[1]; stddevvals.GR = cumSquareDiff[0]; stddevvals.GB = cumSquareDiff[3]; stddevvals.B = cumSquareDiff[2];
                }
                else if (BayerOrder == 2)
                { // RGGB
                    //statusF3.Content = "Av R: " + meanvalues[0] + " Av Gr: " + meanvalues[1] + " Av Gb: " + meanvalues[2] + " Av B: " + meanvalues[3];
                    //statusF4.Content = "SD R: " + cumSquareDiff[0] + " SD Gr: " + cumSquareDiff[1] + " SD Gb: " + cumSquareDiff[2] + " SD B: " + cumSquareDiff[3];
                    meanvals.R = meanvalues[2]; meanvals.GR = meanvalues[3]; meanvals.GB = meanvalues[0]; meanvals.B = meanvalues[1];
                    stddevvals.R = cumSquareDiff[2]; stddevvals.GR = cumSquareDiff[3]; stddevvals.GB = cumSquareDiff[0]; stddevvals.B = cumSquareDiff[1];
                }
                else if (BayerOrder == 3)
                { // GRBG
                    //statusF3.Content = "Av R: " + meanvalues[1] + " Av Gr: " + meanvalues[0] + " Av Gb: " + meanvalues[3] + " Av B: " + meanvalues[2];
                    //statusF4.Content = "SD R: " + cumSquareDiff[1] + " SD Gr: " + cumSquareDiff[0] + " SD Gb: " + cumSquareDiff[3] + " SD B: " + cumSquareDiff[2];
                    meanvals.R = meanvalues[3]; meanvals.GR = meanvalues[2]; meanvals.GB = meanvalues[1]; meanvals.B = meanvalues[0];
                    stddevvals.R = cumSquareDiff[3]; stddevvals.GR = cumSquareDiff[2]; stddevvals.GB = cumSquareDiff[1]; stddevvals.B = cumSquareDiff[0];
                }
            }
            else
            {
                //int bpp = bmpSource.Format.bytesperpixel / 8;

                //double[] meanvalues = new double[bytesperpixel];
                //double[] cumSquareDiff = new double[bytesperpixel];

                // reset averages
                for (int i = 0; i < bytesperpixel; i++)
                    meanvalues[i] = 0;

                // calculate average colors
                for (int i = 0; i < height * width * bytesperpixel; i++)
                {
                    meanvalues[i % bytesperpixel] += pixels[i]; // assign index according to bayer position
                }

                // divide by pixel count
                for (int i = 0; i < bytesperpixel; i++)
                {
                    meanvalues[i] /= width * height;
                    meanvalues[i] = Math.Round(meanvalues[i]);
                }

                // reset cumulative square difference
                for (int i = 0; i < bytesperpixel; i++)
                    cumSquareDiff[i] = 0;

                // calculate cumulative variance
                for (int i = 0; i < height * width * bytesperpixel; i++)
                {
                    cumSquareDiff[i % bytesperpixel] += Math.Pow(pixels[i] - meanvalues[i % bytesperpixel], 2); // assign index according to color
                }

                // divide variance by pixel count
                for (int i = 0; i < bytesperpixel; i++)
                    cumSquareDiff[i] /= width * height;

                // get square root, for std dev.
                for (int i = 0; i < bytesperpixel; i++)
                    cumSquareDiff[i] = Math.Round(Math.Sqrt(cumSquareDiff[i]), 2);

                //statusF3.Content = "Av R: " + meanvalues[0] + " Av G: " + meanvalues[1] + " Av B: " + meanvalues[2];
                //statusF4.Content = "SD R: " + cumSquareDiff[0] + " SD G: " + cumSquareDiff[1] + " SD B: " + cumSquareDiff[2];
                meanvals.R = meanvalues[0]; meanvals.G = meanvalues[1]; meanvals.B = meanvalues[2];
                stddevvals.R = cumSquareDiff[0]; stddevvals.G = cumSquareDiff[1]; stddevvals.B = cumSquareDiff[2];
            }
            return 0;
        }

        public int calcMacbethStats(byte[] pixels, int width, int height)
        {
            // //create array of rectangles
            System.Windows.Int32Rect[] macbethRects = new System.Windows.Int32Rect[24];

            

            for (int i = 0; i < 24; i++) {
                macbethRects[i].Width = (int)(width * 0.06);
                macbethRects[i].Height = (int)(height * 0.09);

                if (i % 6 == 0)
                    macbethRects[i].X = (int)(width * 0.04);
                else if (i % 6 == 1)
                    macbethRects[i].X = (int)(width * 0.21);
                else if (i % 6 == 2)
                    macbethRects[i].X = (int)(width * 0.38);
                else if (i % 6 == 3)
                    macbethRects[i].X = (int)(width * 0.56);
                else if (i % 6 == 4)
                    macbethRects[i].X = (int)(width * 0.72);
                else if (i % 6 == 5)
                    macbethRects[i].X = (int)(width * 0.89);
            }

            for (int i = 0; i < 24; i++) {
                if (i < 6)
                    macbethRects[i].Y = (int)(height * 0.06);
                else if (i < 12)
                    macbethRects[i].Y = (int)(height * 0.33);
                else if (i < 18)
                    macbethRects[i].Y = (int)(height * 0.58);
                else if (i < 24)
                    macbethRects[i].Y = (int)(height * 0.84);
            }

            // get patch stats for each rectangle
            for (int i = 0; i < 24; i++){
                byte[] patch = new byte[macbethRects[0].Width * macbethRects[0].Height*2];
                BitmapSource bmpSource = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray16, null, pixels, width * 2);
                //image1.Source = bmpSource;
                CroppedBitmap chunk = new CroppedBitmap(bmpSource, macbethRects[i]);

                try
                {
                    chunk.CopyPixels(patch, macbethRects[i].Width * 2, 0); // stuff data into 4 pixel (8 byte) array
                    calculateStats(patch, macbethRects[i].Width, macbethRects[i].Height);
                    MacbethMean[i] = getMeanValues();
                }
                catch (Exception ex)
                {
                    //MessageBox.Show(ex.Message);
                }
            }

            // calculate average colors
            /*int patchrow = 0, patchcol, row;
            for (row = 0; row < height; row++) {

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


                for (int col = 0; col < width; col++) {

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

                    if ((patchcol < 6) && (patchrow < 4)) {
                        meanvalues[patchrow * 6 + patchcol, (row % 2) * 2 + col % 2] += values[row * width + col]; // assign index according to bayer position
                        pixelcount[patchrow * 6 + patchcol]++;
                    }
                }
            }*/
            return 0;
        }

        public colorvals getMeanValues() {
            return meanvals;
        }

        public colorvals getStdDev() {
            return stddevvals;
        }

        public colorvals[] getMacbethMean()
        {
            return MacbethMean;
        }
    }
}
