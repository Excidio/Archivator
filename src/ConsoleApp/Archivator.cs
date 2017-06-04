using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Archivator.ConsoleApp
{
    public class Archivator
    {
        private const int SliceBytes = 1048576;

        public void Compress(Stream targetStream, Stream sourceStream)
        {
            var listOfMemStream = new MemoryStream[(int)(sourceStream.Length / SliceBytes + 1)];
            var bufferRead = new byte[SliceBytes];

            var noOfTasksF = (float)sourceStream.Length / SliceBytes;
            var noOfTasksI = sourceStream.Length / SliceBytes;
            float toComp = noOfTasksI;
            var tasks = toComp < noOfTasksF ? new Thread[sourceStream.Length / SliceBytes + 1] : new Thread[sourceStream.Length / SliceBytes];

            var taskCounter = 0;
            var read = 0;
            while ((read = sourceStream.Read(bufferRead, 0, SliceBytes)) != 0)
            {
                var read1 = bufferRead;
                var read2 = read;
                var counter = taskCounter;
                tasks[taskCounter] = new Thread(() => CompressStream(read1, read2, counter, ref listOfMemStream));
                tasks[taskCounter].Start();
                taskCounter++;
                bufferRead = new byte[SliceBytes];
            }

            foreach (var t in tasks)
            {
                t.Join();
            }

            for (var i = 0; i < tasks.Length; i++)
            {
                byte[] lengthToStore = GetBytesToStore((int)listOfMemStream[i].Length);

                targetStream.Write(lengthToStore, 0, lengthToStore.Length);

                var compressedBytes = listOfMemStream[i].ToArray();
                listOfMemStream[i].Close();
                listOfMemStream[i] = null;
                targetStream.Write(compressedBytes, 0, compressedBytes.Length);
            }

            sourceStream.Close();
            targetStream.Close();
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
