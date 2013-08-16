using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO; // for file IO

namespace IQLabsImageProcessor {
    class rawdataparser {

        public MainWindow.ImageInfo parseRawFileName(String fileName)
        {
            MainWindow.ImageInfo image;
            string[] nameInfo = fileName.Split(new string[] { "__" }, StringSplitOptions.RemoveEmptyEntries);
            image.rawName = nameInfo[0];
            string[] dataFields = nameInfo[1].Split(new char[] { 'x', '.' }, StringSplitOptions.RemoveEmptyEntries);
            image.rawWidth = System.Convert.ToInt32(dataFields[0]);
            image.rawHeight = System.Convert.ToInt32(dataFields[1]);
            image.rawBitwidth = System.Convert.ToInt32(dataFields[2]);

            char[] order = dataFields[3].ToCharArray();

            if (order[0] == 'R')
                image.rawBayerOrder = 0;
            else if ((order[0] == 'G') && (order[1] == 'R'))
                image.rawBayerOrder = 1;
            else if ((order[0] == 'G') && (order[1] == 'B'))
                image.rawBayerOrder = 2;
            else // B
                image.rawBayerOrder = 3;

            return image;
        }

        public int readRawFile(String path, ref byte[] rawData, MainWindow.ImageInfo image)
        {
            using (BinaryReader b = new BinaryReader(File.Open(path, FileMode.Open))) {
                // Position and length variables.

                // 2 bytes per pixel
                rawData = b.ReadBytes(image.rawWidth * image.rawHeight * 2);
                int temppixel = 0;

                // shift to MSB aligned according to precision
                for (int i = 0; i < image.rawWidth * image.rawHeight; i++) {
                    if (image.rawBitwidth == 10) {
                        temppixel = (rawData[i * 2 + 1] & 0x03) << 8;
                        temppixel += rawData[i * 2];
                        temppixel = temppixel << 6;
                    } else if (image.rawBitwidth == 12) {
                        temppixel = (rawData[i * 2 + 1] & 0x0f) << 8;
                        temppixel += rawData[i * 2];
                        temppixel = temppixel << 4;
                    } else if (image.rawBitwidth == 14) {
                        temppixel = (rawData[i * 2 + 1] & 0x3f) << 8;
                        temppixel += rawData[i * 2];
                        temppixel = temppixel << 2;
                    }

                    rawData[i * 2 + 1] = (byte)((temppixel & 0xff00) >> 8);
                    rawData[i * 2] = (byte)((temppixel & 0xff));
                }
            }
            return 0;
        }
    }
}
