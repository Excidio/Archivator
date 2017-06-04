using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;

namespace Archivator.ConsoleApp
{
    public class Decompressor
    {
        private bool _isFinished;
        private int _totalChunks;

        private readonly BoundedBuffer<byte[]> _toDecompress = new BoundedBuffer<byte[]>(100);
        private readonly BoundedBuffer<byte[]> _toWrite = new BoundedBuffer<byte[]>(100);

        public void Decompress(Stream targetStream, Stream sourceStream)
        {
            var readThread = new Thread(() => Read(sourceStream));
            var compressThread = new Thread(Decompress);
            var writeThread = new Thread(() => Write(targetStream));

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

            while (0 != sourceStream.Read(buffToReadLength, 0, 8))
            {
                var lengthToRead = GetLengthFromBytes(buffToReadLength);
                var buffRead = new byte[lengthToRead];

                if (lengthToRead != sourceStream.Read(buffRead, 0, lengthToRead))
                {
                    throw new ApplicationException("Possible file corruption. Error uncomressing the file.  Contact BK");
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

                var cmpStream = new MemoryStream(dataToDecompress);
                var unCompZip = new GZipStream(cmpStream, CompressionMode.Decompress, true);

                var unCompressedBuffer = new byte[dataToDecompress.Length];

                var msToAssign = new MemoryStream();
                int read;
                while (0 != (read = unCompZip.Read(unCompressedBuffer, 0, dataToDecompress.Length)))
                {
                    msToAssign.Write(unCompressedBuffer, 0, read);
                }

                _toWrite.Add(msToAssign.ToArray());

                unCompZip.Close();
                cmpStream.Close();
                msToAssign.Close();

                processedChunks++;
            }
        }

        private void Write(Stream targetStream)
        {
            var processedChunks = 0;
            while (!_isFinished || processedChunks < _totalChunks)
            {
                var data = _toWrite.Take();
                targetStream.Write(data, 0, data.Length);
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
