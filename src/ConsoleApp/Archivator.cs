using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Archivator.ConsoleApp
{
    public class Chunk
    {
        public byte[] Data { get; set; }

        public int Length { get; set; }
    }

    public class Archivator
    {
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
                _toCompress.Add(new Chunk {Data = bufferRead, Length = read});
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
                _toWrite.Add(new Chunk {Data = data, Length = data.Length});
                stream.Close();
            }
        }

        private void Write(Stream targetStream)
        {
            var processedChunks = 0;
            while (processedChunks++ < _fileSize)
            {
                var chunk = _toWrite.Take();

                byte[] lengthToStore = GetBytesToStore(chunk.Data.Length);

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

        //public void Decompress(FileStream targetStream, FileStream sourceStream)
        //{
        //    List<byte[]> listOfReadBytes = new List<byte[]>();

        //    int readLength = 0;
        //    byte[] buffToReadLength = new byte[SliceBytes];

        //    while (0 != (readLength = sourceStream.Read(buffToReadLength, 0, SliceBytes)))
        //    {
        //        listOfReadBytes.Add(buffToReadLength);
        //    }

        //    Thread[] tasks = new Thread[listOfReadBytes.Count];
        //    MemoryStream[] memStreamArr = new MemoryStream[listOfReadBytes.Count];
        //    AutoResetEvent signalToTrigger = new AutoResetEvent(false);

        //    for (int counter = 0; counter < listOfReadBytes.Count; counter++)
        //    {
        //        tasks[counter] = new Thread(() => UnCompressP(listOfReadBytes[counter], counter, signalToTrigger, ref memStreamArr));
        //        tasks[counter].Start();
        //        signalToTrigger.WaitOne(-1);
        //    }

        //    foreach (var t in tasks)
        //    {
        //        t.Join();
        //    }

        //    for (int counter = 0; counter < listOfReadBytes.Count; counter++)
        //    {
        //        int length = (int)memStreamArr[counter].Length;
        //        byte[] buffToWrite = new byte[length];

        //        memStreamArr[counter].Seek(0, 0);
        //        memStreamArr[counter].Read(buffToWrite, 0, length);
        //        targetStream.Write(buffToWrite, 0, length);

        //    }


        //}

        public void Decompress(FileStream targetStream, FileStream sourceStream)
        {
            List<byte[]> listOfReadBytes = new List<byte[]>();

            int readLength = 0;
            byte[] buffToReadLength = new byte[8];

            while (0 != (readLength = sourceStream.Read(buffToReadLength, 0, 8)))
            {
                int lengthToRead = GetLengthFromBytes(buffToReadLength);
                byte[] buffRead = new byte[lengthToRead];

                if (lengthToRead != sourceStream.Read(buffRead, 0, lengthToRead))
                {
                    throw new ApplicationException("Possible file corruption. Error uncomressing the file.  Contact BK");
                }
                listOfReadBytes.Add(buffRead);
            }

            Thread[] tasks = new Thread[listOfReadBytes.Count];
            MemoryStream[] memStreamArr = new MemoryStream[listOfReadBytes.Count];
            AutoResetEvent signalToTrigger = new AutoResetEvent(false);

            for (int counter = 0; counter < listOfReadBytes.Count; counter++)
            {
                tasks[counter] = new Thread(() => UnCompressP(listOfReadBytes[counter], counter, signalToTrigger, ref memStreamArr));
                tasks[counter].Start();
                signalToTrigger.WaitOne(-1);
            }

            foreach (var t in tasks)
            {
                t.Join();
            }

            for (int counter = 0; counter < listOfReadBytes.Count; counter++)
            {
                int length = (int)memStreamArr[counter].Length;
                byte[] buffToWrite = new byte[length];

                memStreamArr[counter].Seek(0, 0);
                memStreamArr[counter].Read(buffToWrite, 0, length);
                targetStream.Write(buffToWrite, 0, length);

            }
        }

        private static void UnCompressP(byte[] buffToUnCompress, int index, AutoResetEvent eventToTrigger, ref MemoryStream[] memStream)
        {
            eventToTrigger.Set();
            MemoryStream cmpStream = new MemoryStream(buffToUnCompress);

            GZipStream unCompZip = new GZipStream(cmpStream, CompressionMode.Decompress, true);

            byte[] unCompressedBuffer = new byte[buffToUnCompress.Length];

            MemoryStream msToAssign = new MemoryStream();
            int read = 0;
            while (0 != (read = unCompZip.Read(unCompressedBuffer, 0, buffToUnCompress.Length)))
            {
                msToAssign.Write(unCompressedBuffer, 0, read);
            }
            memStream[index] = msToAssign;

            unCompZip.Close();
            cmpStream.Close();
        }

        private static void CompressStream(byte[] bytesToCompress, int length, int index, ref MemoryStream[] listOfMemStream)
        {
            var stream = new MemoryStream();
            var gzStream = new GZipStream(stream, CompressionMode.Compress, true);
            gzStream.Write(bytesToCompress, 0, length);
            gzStream.Close();

            listOfMemStream[index] = stream;
        }

        private static byte[] GetBytesToStore(int length)
        {
            int lengthToStore = System.Net.IPAddress.HostToNetworkOrder(length);
            byte[] lengthInBytes = BitConverter.GetBytes(lengthToStore);
            string base64Enc = Convert.ToBase64String(lengthInBytes);
            byte[] finalStore = System.Text.Encoding.ASCII.GetBytes(base64Enc);

            return finalStore;
        }

        private static int GetLengthFromBytes(byte[] intToParse)
        {
            string base64Enc = System.Text.Encoding.ASCII.GetString(intToParse);
            byte[] normStr = Convert.FromBase64String(base64Enc);
            int length = BitConverter.ToInt32(normStr, 0);

            return System.Net.IPAddress.NetworkToHostOrder(length);
        }
    }
}
