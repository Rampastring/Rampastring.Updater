using SevenZip.Compression.LZMA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Rampastring.Updater.Compression
{
    /// <summary>
    /// A static class that makes using the 7-zip encoder and decoder easy.
    /// </summary>
    public static class CompressionHelper
    {
        // The code here has been written with the help of the following StackOverflow answer:
        // https://stackoverflow.com/questions/7646328/how-to-use-the-7z-sdk-to-compress-and-decompress-a-file
        // ..and also by looking at the LZMAAlone code that is included with the LZMA SDK.

        /// <summary>
        /// Compresses a file using the LZMA compressor.
        /// </summary>
        /// <param name="inputfilePath">The input file path.</param>
        /// <param name="outputFilePath">The output file path.</param>
        public static void CompressFile(string inputfilePath, string outputFilePath)
        {
            var encoder = new Encoder();
            
            using (FileStream inputStream = File.OpenRead(inputfilePath))
            {
                using (FileStream outputStream = File.Create(outputFilePath))
                {
                    encoder.WriteCoderProperties(outputStream);
                    outputStream.Write(BitConverter.GetBytes(inputStream.Length), 0, 8);

                    encoder.Code(inputStream, outputStream,
                        inputStream.Length, outputStream.Length, null);
                }
            }
        }

        /// <summary>
        /// Decompresses a file using the LZMA decompressor.
        /// </summary>
        /// <param name="inputFilePath">The input file path.</param>
        /// <param name="outputFilePath">The output file path.</param>
        public static void DecompressFile(string inputFilePath, string outputFilePath)
        {
            var decoder = new Decoder();

            using (FileStream inputStream = File.OpenRead(inputFilePath))
            {
                using (FileStream outputStream = File.Create(outputFilePath))
                {
                    byte[] properties = new byte[5];
                    inputStream.Read(properties, 0, properties.Length);

                    byte[] fileLengthArray = new byte[sizeof(long)];
                    inputStream.Read(fileLengthArray, 0, fileLengthArray.Length);
                    long fileLength = BitConverter.ToInt64(fileLengthArray, 0);

                    decoder.SetDecoderProperties(properties);

                    decoder.Code(inputStream, outputStream,
                        inputStream.Length, fileLength, null);
                }
            }
        }
    }
}
