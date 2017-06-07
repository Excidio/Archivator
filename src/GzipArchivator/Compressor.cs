using System;
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

        private const int SliceBytes = 1048576;

        private readonly BoundedBuffer<Chunk> _toCompress = new BoundedBuffer<Chunk>(100);
        private readonly BoundedBuffer<Chunk> _toWrite = new BoundedBuffer<Chunk>(100);

        private bool _isFinished;
        private int _totalChunks;

        public void Compress(Stream sourceStream, Stream destinationStream)
        {
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
                _totalChunks++;
            }

            _isFinished = true;
        }

        private void Compress()
        {
            var processedChunks = 0;
            while (!_isFinished || processedChunks < _totalChunks)
            {
                var chunk = _toCompress.Take();

                MemoryStream memoryStream = null;
                try
                {
                    memoryStream = new MemoryStream();
                    using (var gzStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                    {
                        gzStream.Write(chunk.Data, 0, chunk.Length);
                    }

                    var data = memoryStream.ToArray();
                    _toWrite.Add(new Chunk { Data = data, Length = data.Length });
                }
                finally
                {
                    memoryStream?.Dispose();
                }

                processedChunks++;
            }
        }

        private void Write(Stream destinationStream)
        {
            var processedChunks = 0;
            while (!_isFinished || processedChunks < _totalChunks)
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
