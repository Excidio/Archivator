using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Archivator.ConsoleApp
{
    public class Compressor
    {
        private class Chunk
        {
            public byte[] Data { get; set; }

            public int Length { get; set; }
        }

        private const int SliceBytes = 1048576;

        private int _fileSize;
        private readonly BoundedBuffer<Chunk> _toCompress = new BoundedBuffer<Chunk>(100);
        private readonly BoundedBuffer<Chunk> _toWrite = new BoundedBuffer<Chunk>(100);

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
            while (processedChunks++ < _fileSize)
            {
                var chunk = _toCompress.Take();

                var stream = new MemoryStream();
                var gzStream = new GZipStream(stream, CompressionMode.Compress, true);
                gzStream.Write(chunk.Data, 0, chunk.Length);
                gzStream.Close();

                var data = stream.ToArray();
                _toWrite.Add(new Chunk { Data = data, Length = data.Length });
                stream.Close();
            }
        }

        private void Write(Stream targetStream)
        {
            var processedChunks = 0;
            while (processedChunks++ < _fileSize)
            {
                var chunk = _toWrite.Take();

                var lengthToStore = GetBytesToStore(chunk.Data.Length);
                targetStream.Write(lengthToStore, 0, lengthToStore.Length);

                targetStream.Write(chunk.Data, 0, chunk.Length);
            }
        }

        public void Compress(Stream targetStream, Stream sourceStream)
        {
            _fileSize = (int)(sourceStream.Length / SliceBytes + 1);

            var readThread = new Thread(() => Read(sourceStream));
            var compressThread = new Thread(Compress);
            var writeThread = new Thread(() => Write(targetStream));

            readThread.Start();
            compressThread.Start();
            writeThread.Start();

            readThread.Join();
            compressThread.Join();
            writeThread.Join();
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
