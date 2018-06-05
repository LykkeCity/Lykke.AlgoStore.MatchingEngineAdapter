using System;
using System.IO;
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening
{
    internal static class StreamExtensions
    {
        public static async Task FillBufferAsync(this Stream stream, byte[] buffer)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            var bytesReadTotal = 0;

            while(bytesReadTotal < buffer.Length)
            {
                var bytesRead = await stream.ReadAsync(buffer, bytesReadTotal, buffer.Length - bytesReadTotal);

                if (bytesRead == 0)
                    throw new EndOfStreamException();

                bytesReadTotal += bytesRead;
            }
        }
    }
}
