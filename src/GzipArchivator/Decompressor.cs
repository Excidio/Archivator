using System;
using System.Collections.Generic;
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

        private const int ChunkBlocksCount = 64;

        private readonly BoundedBuffer<byte[]> _toDecompress = new BoundedBuffer<byte[]>(500);
        private readonly BoundedBuffer<byte[]> _toWrite = new BoundedBuffer<byte[]>(500);

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
            var index = 0;

            var decompressedChunks = new byte[ChunkBlocksCount][];
            var waitHandles = new List<WaitHandle>(ChunkBlocksCount);


            while (!_isFinished || processedChunks < _totalChunks)
            {
                var compressedChunk = _toDecompress.Take();
                var handle = new ManualResetEvent(false);
                var i = index;

                new Thread(() => Decompress(compressedChunk, decompressedChunks, i, handle)).Start();

                waitHandles.Add(handle);
                index++;
                processedChunks++;

                if (index == ChunkBlocksCount || (_isFinished && processedChunks == _totalChunks))
                {
                    WaitHandle.WaitAll(waitHandles.ToArray());
                    foreach (var decompressedChunk in decompressedChunks)
                    {
                        _toWrite.Add(decompressedChunk);
                    }
                    index = 0;

                    waitHandles = new List<WaitHandle>(ChunkBlocksCount);
                }
            }
        }

        private static void Decompress(byte[] compressedChunk, IList<byte[]> decompressedChunks, int index, EventWaitHandle handle)
        {
            using (var cmpStream = new MemoryStream(compressedChunk))
            using (var decomprStream = new GZipStream(cmpStream, CompressionMode.Decompress, true))
            using (var msToAssign = new MemoryStream())
            {
                var unCompressedBuffer = new byte[compressedChunk.Length];
                int read;
                while ((read = decomprStream.Read(unCompressedBuffer, 0, compressedChunk.Length)) > 0)
                {
                    msToAssign.Write(unCompressedBuffer, 0, read);
                }

                decompressedChunks[index] = msToAssign.ToArray();
            }

            handle.Set();
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
