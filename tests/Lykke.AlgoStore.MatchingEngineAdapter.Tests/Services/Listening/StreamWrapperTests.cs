using Common.Log;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using Moq;
using NUnit.Framework;
using ProtoBuf;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Tests.Services.Listening
{
    [TestFixture]
    public class StreamWrapperTests
    {
        [Test]
        public void StreamWrapper_Ctor_ThrowsArgumentNull_WhenStreamNull()
        {
            Assert.Throws<ArgumentNullException>(() => new StreamWrapper(null, null, null));
        }

        [Test]
        public void StreamWrapper_Ctor_ThrowsArgumentNull_WhenLogNull()
        {
            using (var ms = new MemoryStream())
            {
                Assert.Throws<ArgumentNullException>(() => new StreamWrapper(ms, null, null));
            }
        }

        [Test]
        public void StreamWrapper_ReadMessageAsync_ThrowsObjectDisposed_WhenWrapperDisposed()
        {
            AssertMethodThrowsObjectDisposed((sw) => sw.ReadMessageAsync());
        }

        [Test]
        public void StreamWrapper_WriteMessageAsync_ThrowsObjectDisposed_WhenWrapperDisposed()
        {
            AssertMethodThrowsObjectDisposed((sw) => sw.WriteMessageAsync(0, 0, new object()));
        }

        [Test]
        public void StreamWrapper_ReadMessageAsync_ThrowsInvalidData_WhenUnknownMessage()
        {
            var log = Given_Correct_Log();
            var memoryStream = new MemoryStream(new byte[] { 255, 0, 0, 0, 0, 0, 0, 0 });
            var endPoint = Given_Correct_IPEndPoint();
            var streamWrapper = new StreamWrapper(memoryStream, log, endPoint);

            var task = streamWrapper.ReadMessageAsync();

            task.ContinueWith(
                t => Assert.AreEqual(typeof(InvalidDataException), t.Exception.InnerExceptions[0].GetType())).Wait();
        }

        [Test]
        public void StreamWrapper_ReadMessageAsync_ThrowsInvalidData_WhenInvalidProtobufFormat()
        {
            var log = Given_Correct_Log();
            var memoryStream = new MemoryStream(new byte[] { (byte)MeaRequestType.Ping, // Message type
                                                             0, 0, 0, 0,                // Request ID
                                                             1, 0,                      // Payload length
                                                             1 });                      // Junk payload data
            var endPoint = Given_Correct_IPEndPoint();

            var streamWrapper = new StreamWrapper(memoryStream, log, endPoint);

            var task = streamWrapper.ReadMessageAsync();

            task.ContinueWith(
                t => Assert.AreEqual(typeof(InvalidDataException), t.Exception.InnerExceptions[0].GetType())).Wait();
        }

        [Test]
        public void StreamWrapper_ReadMessageAsync_ReturnsCorrectMessage_ForCorrectData()
        {
            var log = Given_Correct_Log();
            var request = new PingRequest { Message = "test" };
            var memoryStream = Given_MemoryStream_WithWrittenRequest(1, request);
            var endPoint = Given_Correct_IPEndPoint();
            var streamWrapper = new StreamWrapper(memoryStream, log, endPoint);

            var result = streamWrapper.ReadMessageAsync().Result;
            var resultMessage = result.Message as PingRequest;

            Assert.AreEqual(1, result.Id);
            Assert.AreEqual(request.Message, resultMessage.Message);
        }

        private MemoryStream Given_MemoryStream_WithWrittenRequest<T>(uint requestId, T message)
        {
            var memoryStream = new MemoryStream();

            memoryStream.WriteByte((byte)MeaRequestType.Ping);
            memoryStream.Write(BitConverter.GetBytes(requestId), 0, 4);

            using (var tempMs = new MemoryStream())
            {
                Serializer.Serialize(tempMs, message);
                var payload = tempMs.ToArray();
                memoryStream.Write(BitConverter.GetBytes((ushort)payload.Length), 0, 2);
                memoryStream.Write(payload, 0, payload.Length);
            }

            memoryStream.Seek(0, SeekOrigin.Begin);

            return memoryStream;
        }

        private void AssertMethodThrowsObjectDisposed(Func<StreamWrapper, Task> codeToTest)
        {
            var log = Given_Correct_Log();
            var endPoint = Given_Correct_IPEndPoint();
            var memoryStream = new MemoryStream();
            var streamWrapper = new StreamWrapper(memoryStream, log, endPoint);

            streamWrapper.Dispose();

            var task = codeToTest(streamWrapper);

            task.ContinueWith(
                t => Assert.AreEqual(typeof(ObjectDisposedException),
                                     t.Exception.InnerExceptions[0].GetType())).Wait();
        }

        private ILog Given_Correct_Log()
        {
            var logMock = new Mock<ILog>();
            return logMock.Object;
        }

        private IPEndPoint Given_Correct_IPEndPoint()
        {
            return new IPEndPoint(IPAddress.Loopback, 0);
        }
    }
}
