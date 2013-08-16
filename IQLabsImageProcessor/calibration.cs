using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IQLabsImageProcessor {
    class calibration {

        public struct calibrationvalues {
            public double exposuregain;
            public double RGain;
            public double BGain;
            public double RGB_RR;
            public double RGB_RG;
            public double RGB_RB;
            public double RGB_GR;
            public double RGB_GG;
            public double RGB_GB;
            public double RGB_BR;
            public double RGB_BG;
            public double RGB_BB;
        }

        public calibrationvalues sensorToTarget(int target, int chart, imagestats.colorvals[] meanvalues, int OBLevel)
        {
            // apply OB offset and do gain correction
            //int OBLevel = (int)num_OBLevel.Value;
            double OBComp = 4095.0 / (4095.0 - OBLevel);

            for (int i = 0; i < 24; i++)
            {
                meanvalues[i].R -= OBLevel >> 2;
                meanvalues[i].R *= OBComp;
                meanvalues[i].GR -= OBLevel >> 2;
                meanvalues[i].GR *= OBComp;
                meanvalues[i].GB -= OBLevel >> 2;
                meanvalues[i].GB *= OBComp;
                meanvalues[i].B -= OBLevel >> 2;
                meanvalues[i].B *= OBComp;
            }

            // reorder colors to B,G,G,R
            /*double temp;
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
            }*/

            // measure WB correction
            double[,] WBRatios = new double[4, 2]; // 4 patches, 2 ratios (R/G, B/G)
            //for (int i = 0; i < 24; i++) {
            for (int j = 0; j < 4; j++)
            {
                WBRatios[j, 0] = meanvalues[j + 19].GR / meanvalues[j + 19].R;
                WBRatios[j, 1] = meanvalues[j + 19].GB / meanvalues[j + 19].B;
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
                meanvalues[i].R = meanvalues[i].R * BGain;
                meanvalues[i].B = meanvalues[i].B * RGain;
            }
            // measure exposure compensation
            double[] GainRatios = new double[4]; // 4 patches, green channel
            double[] macBethGreens = new double[4] { 148.4 * 4, 90.6 * 4, 47.8 * 4, 22.2 * 4 };
            for (int j = 0; j < 4; j++)
            {
                GainRatios[j] = macBethGreens[j] / ((meanvalues[j + 19].GR + meanvalues[j + 19].GB)/2);
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
                meanvalues[i].R = meanvalues[i].R * ExposureComp;
                meanvalues[i].GR = meanvalues[i].GR * ExposureComp;
                meanvalues[i].GB = meanvalues[i].GB * ExposureComp;
                meanvalues[i].B = meanvalues[i].B * ExposureComp;
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
                inputRGB[0][i] = meanvalues[i].B / 4;
                inputRGB[1][i] = (meanvalues[i].GB + meanvalues[i].GR) / 8;
                inputRGB[2][i] = meanvalues[i].R / 4;
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

            calibrationvalues output = new calibrationvalues();

            //loadingControls = true;
            output.exposuregain = ExposureComp;
            output.RGain = RGain;
            output.BGain = BGain;
            output.RGB_RR = 1 - (double)RGB2RGB_R.Array[1][0] - (double)RGB2RGB_R.Array[2][0];
            output.RGB_RG = (double)RGB2RGB_R.Array[1][0];
            output.RGB_RB = (double)RGB2RGB_R.Array[2][0];
            output.RGB_GR = (double)RGB2RGB_G.Array[0][0];
            output.RGB_GG = 1 - (double)RGB2RGB_G.Array[0][0] - (double)RGB2RGB_G.Array[2][0];
            output.RGB_GB = (double)RGB2RGB_G.Array[2][0];
            output.RGB_BR = (double)RGB2RGB_B.Array[0][0];
            output.RGB_BG = (double)RGB2RGB_B.Array[1][0];
            output.RGB_BB = 1 - (double)RGB2RGB_B.Array[0][0] - (double)RGB2RGB_B.Array[1][0];
            //loadingControls = false;

            return output;
        }
    }
}
