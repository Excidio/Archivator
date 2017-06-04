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
            while (sourceStream.Read(bufferRead, 0, SliceBytes) != 0)
            {
                var read1 = bufferRead;
                var counter = taskCounter;
                tasks[taskCounter] = new Thread(() => CompressStream(read1, counter, ref listOfMemStream));
                tasks[taskCounter].Start();
                taskCounter++;
                bufferRead = new byte[SliceBytes];
            }

            foreach (var t in tasks)
            {
                t.Join();
            }

            for (taskCounter = 0; taskCounter < tasks.Length; taskCounter++)
            {
                var compressedBytes = listOfMemStream[taskCounter].ToArray();
                listOfMemStream[taskCounter].Close();
                listOfMemStream[taskCounter] = null;
                targetStream.Write(compressedBytes, 0, compressedBytes.Length);
            }
        }

        private static void CompressStream(byte[] bytesToCompress, int index, ref MemoryStream[] listOfMemStream)
        {
            var stream = new MemoryStream();
            var gzStream = new GZipStream(stream, CompressionMode.Compress, true);
            gzStream.Write(bytesToCompress, 0, bytesToCompress.Length);
            gzStream.Close();

            listOfMemStream[index] = stream;
        }
    }
}
