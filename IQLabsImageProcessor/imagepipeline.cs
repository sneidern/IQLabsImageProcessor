using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IQLabsImageProcessor
{
    class imagepipeline
    {
        public enum pipestage { Raw, OB, WB, Flare, DM, RGB3x3, Tonecurve };

        private struct pipeconfig
        {
            public int endstage;
            public int OBLevel;
            public bool OBGainCompEn;
            public int OBGainValue;
            public int RGain;
            public int GGain;
            public int BGain;
            public int exposuregain;
            public int FlareLevel;
            public int RGB_RR;
            public int RGB_RG;
            public int RGB_RB;
            public int RGB_GR;
            public int RGB_GG;
            public int RGB_GB;
            public int RGB_BR;
            public int RGB_BG;
            public int RGB_BB;
        }

        private pipeconfig config;

        public void fillPipeConfig(MainWindow app)
        {
            config.OBLevel = (int)app.num_OBLevel.Value;
            config.OBGainCompEn = (bool)app.checkBox_OBGainComp.IsChecked;
            config.RGain = (int)(app.num_RGain.Value * 512);
            config.GGain = (int)(app.num_GGain.Value * 512);
            config.BGain = (int)(app.num_BGain.Value * 512);
            config.FlareLevel = (int)app.num_FlareLevel.Value;
            config.exposuregain = (int)app.num_exposuregain.Value;
            config.RGB_RR = (int)(app.decimal_RGB_RR.Value * 512);
            config.RGB_RG = (int)(app.decimal_RGB_RG.Value * 512);
            config.RGB_RB = (int)(app.decimal_RGB_RB.Value * 512);
            config.RGB_GR = (int)(app.decimal_RGB_GR.Value * 512);
            config.RGB_GG = (int)(app.decimal_RGB_GG.Value * 512);
            config.RGB_GB = (int)(app.decimal_RGB_GB.Value * 512);
            config.RGB_BR = (int)(app.decimal_RGB_BR.Value * 512);
            config.RGB_BG = (int)(app.decimal_RGB_BG.Value * 512);
            config.RGB_BB = (int)(app.decimal_RGB_BB.Value * 512);
            if (app.radioButton_pureRaw.IsChecked == true)
                config.endstage = (int)imagepipeline.pipestage.Raw;
            else if (app.radioButton_OpticalBlack.IsChecked == true)
                config.endstage = (int)imagepipeline.pipestage.OB;
            else if (app.radioButton_WhiteBalance.IsChecked == true)
                config.endstage = (int)imagepipeline.pipestage.WB;
            else if (app.radioButton_Demosaic.IsChecked == true)
                config.endstage = (int)imagepipeline.pipestage.DM;
            else if (app.radioButton_RGBColor.IsChecked == true)
                config.endstage = (int)imagepipeline.pipestage.RGB3x3;
            else if (app.radioButton_Tonecurve.IsChecked == true)
                config.endstage = (int)imagepipeline.pipestage.Tonecurve;
        }

        public int getFinalStage()
        {
            return config.endstage;
        }

        public int runPipeline(ref byte[] rawData, ref int[] imageData16, ref int[] rgbData16, int rawWidth, int rawHeight, int rawBayerOrder)
        {
            //imageData16 = new int[rawWidth * rawHeight];

            // convert to ints for processing
            for (int i = 0; i < rawWidth * rawHeight; i++)
                imageData16[i] = (rawData[i * 2 + 1] << 8) + rawData[i * 2];

            if (config.endstage == (int)pipestage.Raw)
            {
                return 0;
            }

            // process

            // OB and gain compensation
            int OBGainComp = 4096; // no change
            //int OBGainValue = (int)config.OBLevel;
            if (config.OBGainCompEn == true)
                OBGainComp = (4096 - config.OBLevel);

            for (int i = 0; i < rawWidth * rawHeight; i++)
            {
                imageData16[i] = imageData16[i] - (config.OBLevel << 4);
                imageData16[i] = (imageData16[i] * 4096) / OBGainComp;

                if (imageData16[i] < 0)
                    imageData16[i] = 0;
                if (imageData16[i] > 65535)
                    imageData16[i] = 65535;
            }

            //if (radioButton_OpticalBlack.IsChecked == true)
            if (config.endstage == (int)pipestage.OB)
            {
                //updateDisplayImageRAW();
                return 0;
            }

            // White Balance and Flare
            int[] gains = new int[4];

            switch (rawBayerOrder)
            {
                case 0:
                    gains[0] = (int)(config.RGain * config.exposuregain ); gains[1] = (int)(config.GGain * config.exposuregain ); gains[2] = (int)(config.GGain * config.exposuregain ); gains[3] = (int)(config.BGain * config.exposuregain );
                    break;
                case 1:
                    gains[0] = (int)(config.GGain * config.exposuregain ); gains[1] = (int)(config.RGain * config.exposuregain ); gains[2] = (int)(config.BGain * config.exposuregain ); gains[3] = (int)(config.GGain * config.exposuregain );
                    break;
                case 2:
                    gains[0] = (int)(config.GGain * config.exposuregain ); gains[1] = (int)(config.BGain * config.exposuregain ); gains[2] = (int)(config.RGain * config.exposuregain ); gains[3] = (int)(config.GGain * config.exposuregain );
                    break;
                case 3:
                    gains[0] = (int)(config.BGain * config.exposuregain ); gains[1] = (int)(config.GGain * config.exposuregain ); gains[2] = (int)(config.GGain * config.exposuregain ); gains[3] = (int)(config.RGain * config.exposuregain );
                    break;
            }

            int color;
            int FlareLevel = (int)config.FlareLevel;
            //int ExposureLevel = (int)config.exposuregain;

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

            //if (radioButton_WhiteBalance.IsChecked == true)
            if (config.endstage == (int)pipestage.WB)
            {
                //updateDisplayImageRAW();
                return 0;
            }

            // Demosaic
            //int mapR = 0, mapG = 0, mapB = 0;

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
                            rgbData16[bpix * 3 + 0] = Math.Min(imageData16[bpix], 65535); // Red from current pixel
                            rgbData16[bpix * 3 + 1] = Math.Min((imageData16[bpix - rawWidth] + imageData16[bpix + rawWidth] + imageData16[bpix - 1] + imageData16[bpix + 1] + 2) >> 2, 65535); // Green from 4 sides
                            rgbData16[bpix * 3 + 2] = Math.Min((imageData16[bpix - rawWidth - 1] + imageData16[bpix + rawWidth - 1] + imageData16[bpix - rawWidth + 1] + imageData16[bpix + rawWidth + 1] + 2) >> 2, 65535); // Blue from 4 diagonal
                            break;
                        case 1: // Green/Red
                            rgbData16[bpix * 3 + 0] = Math.Min((imageData16[bpix - 1] + imageData16[bpix + 1] + 1) >> 1, 65535); // Red from left/right
                            rgbData16[bpix * 3 + 1] = Math.Min(imageData16[bpix], 65535); // green from current pixel
                            rgbData16[bpix * 3 + 2] = Math.Min((imageData16[bpix - rawWidth] + imageData16[bpix + rawWidth] + 1) >> 1, 65535); // Blue from top/bottom
                            break;
                        case 2: // Green/Blue
                            rgbData16[bpix * 3 + 0] = Math.Min((imageData16[bpix - rawWidth] + imageData16[bpix + rawWidth] + 1) >> 1, 65535); // Red from top/bottom
                            rgbData16[bpix * 3 + 1] = Math.Min(imageData16[bpix], 65535); // green from current pixel
                            rgbData16[bpix * 3 + 2] = Math.Min((imageData16[bpix - 1] + imageData16[bpix + 1] + 1) >> 1, 65535); // Blue from left/right
                            break;
                        case 3: // Blue
                            rgbData16[bpix * 3 + 0] = Math.Min((imageData16[bpix - rawWidth - 1] + imageData16[bpix + rawWidth - 1] + imageData16[bpix - rawWidth + 1] + imageData16[bpix + rawWidth + 1] + 2) >> 2, 65535); // Red from 4 diagonal
                            rgbData16[bpix * 3 + 1] = Math.Min((imageData16[bpix - rawWidth] + imageData16[bpix + rawWidth] + imageData16[bpix - 1] + imageData16[bpix + 1] + 2) >> 2, 65535); // Green from 4 sides
                            rgbData16[bpix * 3 + 2] = Math.Min(imageData16[bpix], 65535); // blue from current pixel
                            break;
                        default:
                            break;
                    }
                }
            }

            if (config.endstage == (int)pipestage.DM)
            {
                return 0;
            }

            // RGB to RGB matrix
            int iRR = (int)(config.RGB_RR );
            int iRG = (int)(config.RGB_RG );
            int iRB = (int)(config.RGB_RB );
            int iGR = (int)(config.RGB_GR );
            int iGG = (int)(config.RGB_GG );
            int iGB = (int)(config.RGB_GB );
            int iBR = (int)(config.RGB_BR );
            int iBG = (int)(config.RGB_BG );
            int iBB = (int)(config.RGB_BB );
            int tempR, tempG, tempB;

            for (int i = 0; i < rawWidth * rawHeight; i++)
            {
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

            if (config.endstage == (int)pipestage.RGB3x3)
            {
                return 0;
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

            if (config.endstage == (int)pipestage.Tonecurve)
            {
                return 0;
            }

            return 0;
        }
    }
}
