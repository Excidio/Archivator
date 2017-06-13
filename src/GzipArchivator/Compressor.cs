using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Archivator.GzipArchivator
{
    public class Compressor
    {
        private class Chunk
        {
            public byte[] Data { get; set; }

            public int Length { get; set; }
        }

        private const int ChunkBlocksCount = 64;
        private const int SliceBytes = 1048576;

        private readonly BoundedBuffer<Chunk> _toCompress = new BoundedBuffer<Chunk>(500);
        private readonly BoundedBuffer<Chunk> _toWrite = new BoundedBuffer<Chunk>(500);

        private int _totalChunks;

        public void Compress(Stream sourceStream, Stream destinationStream)
        {
            _totalChunks = (int)Math.Ceiling(sourceStream.Length / (double)SliceBytes);

            var readThread = new Thread(() => Read(sourceStream));
            var compressThread = new Thread(Compress);
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
            var bufferRead = new byte[SliceBytes];
            int read;
            while ((read = sourceStream.Read(bufferRead, 0, SliceBytes)) != 0)
            {
                _toCompress.Add(new Chunk { Data = bufferRead, Length = read });
                bufferRead = new byte[SliceBytes];
            }
        }

        private void Compress()
        {
            var processedChunks = 0;
            var index = 0;

            var compressedChunks = new Chunk[ChunkBlocksCount];
            var waitHandles = new WaitHandle[ChunkBlocksCount];

            while (processedChunks < _totalChunks)
            {
                var originalChunck = _toCompress.Take();
                var handle = new ManualResetEvent(false);
                var i = index;

                new Thread(() => Compress(originalChunck, compressedChunks, i, handle)).Start();

                waitHandles[index++] = handle;
                processedChunks++;

                if (index == ChunkBlocksCount || processedChunks == _totalChunks)
                {
                    WaitHandle.WaitAll(waitHandles);
                    foreach (var compressedChunk in compressedChunks)
                    {
                        _toWrite.Add(compressedChunk);
                    }
                    index = 0;
                    waitHandles = new WaitHandle[Math.Min(ChunkBlocksCount, _totalChunks - processedChunks)];
                }
            }
        }

        private static void Compress(Chunk originalChunck, IList<Chunk> compressedChunks, int index, EventWaitHandle handle)
        {
            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream();
                using (var gzStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                {
                    gzStream.Write(originalChunck.Data, 0, originalChunck.Length);
                }

                var data = memoryStream.ToArray();
                compressedChunks[index] = new Chunk { Data = data, Length = data.Length };
            }
            finally
            {
                memoryStream?.Dispose();
            }

            handle.Set();
        }

        private void Write(Stream destinationStream)
        {
            var processedChunks = 0;
            while (processedChunks < _totalChunks)
            {
                var chunk = _toWrite.Take();

                var lengthToStore = GetBytesToStore(chunk.Data.Length);
                destinationStream.Write(lengthToStore, 0, lengthToStore.Length);
                destinationStream.Write(chunk.Data, 0, chunk.Length);

                processedChunks++;
            }
        }

        private static byte[] GetBytesToStore(int length)
        {
            var lengthToStore = System.Net.IPAddress.HostToNetworkOrder(length);
            var lengthInBytes = BitConverter.GetBytes(lengthToStore);
            var base64Enc = Convert.ToBase64String(lengthInBytes);
            var finalStore = System.Text.Encoding.ASCII.GetBytes(base64Enc);

            return finalStore;
        }
    }
}
