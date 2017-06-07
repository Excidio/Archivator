using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;

namespace Archivator.GzipArchivator
{
    public class Decompressor
    {
        private bool _isFinished;
        private int _totalChunks;

        private readonly BoundedBuffer<byte[]> _toDecompress = new BoundedBuffer<byte[]>(100);
        private readonly BoundedBuffer<byte[]> _toWrite = new BoundedBuffer<byte[]>(100);

        public void Decompress(Stream sourceStream, Stream destinationStream)
        {
            var readThread = new Thread(() => Read(sourceStream));
            var compressThread = new Thread(Decompress);
            var writeThread = new Thread(() => Write(destinationStream));

            readThread.Start();
            compressThread.Start();
            writeThread.Start();

            readThread.Join();
            compressThread.Join();
            writeThread.Join();
        }

        private void Read(Stream sourceStream)
        {
            var buffToReadLength = new byte[8];

            while (sourceStream.Read(buffToReadLength, 0, 8) > 0)
            {
                var lengthToRead = GetLengthFromBytes(buffToReadLength);
                var buffRead = new byte[lengthToRead];

                if (lengthToRead != sourceStream.Read(buffRead, 0, lengthToRead))
                {
                    throw new ApplicationException("Possible file corruption. Error uncomressing the file.");
                }

                _toDecompress.Add(buffRead);
                _totalChunks++;
            }

            _isFinished = true;
        }

        private void Decompress()
        {
            var processedChunks = 0;
            while (!_isFinished || processedChunks < _totalChunks)
            {
                var dataToDecompress = _toDecompress.Take();

                using (var cmpStream = new MemoryStream(dataToDecompress))
                using (var decomprStream = new GZipStream(cmpStream, CompressionMode.Decompress, true))
                using (var msToAssign = new MemoryStream())
                {
                    var unCompressedBuffer = new byte[dataToDecompress.Length];
                    int read;
                    while ((read = decomprStream.Read(unCompressedBuffer, 0, dataToDecompress.Length)) > 0)
                    {
                        msToAssign.Write(unCompressedBuffer, 0, read);
                    }

                    _toWrite.Add(msToAssign.ToArray());
                }

                processedChunks++;
            }
        }

        private void Write(Stream destinationStream)
        {
            var processedChunks = 0;
            while (!_isFinished || processedChunks < _totalChunks)
            {
                var data = _toWrite.Take();
                destinationStream.Write(data, 0, data.Length);
                processedChunks++;
            }
        }

        private static int GetLengthFromBytes(byte[] intToParse)
        {
            var base64Enc = Encoding.ASCII.GetString(intToParse);
            var normStr = Convert.FromBase64String(base64Enc);
            var length = BitConverter.ToInt32(normStr, 0);

            return IPAddress.NetworkToHostOrder(length);
        }
    }
}
