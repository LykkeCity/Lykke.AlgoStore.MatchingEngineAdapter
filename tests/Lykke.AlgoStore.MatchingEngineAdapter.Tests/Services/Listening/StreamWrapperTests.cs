using Common.Log;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using Moq;
using NUnit.Framework;
using ProtoBuf;
using System;
using System.IO;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Tests.Services.Listening
{
    [TestFixture]
    public class StreamWrapperTests
    {
        [Test]
        public void StreamWrapper_Ctor_ThrowsArgumentNull_WhenStreamNull()
        {
            Assert.Throws<ArgumentNullException>(() => new StreamWrapper(null, null));
        }

        [Test]
        public void StreamWrapper_Ctor_ThrowsArgumentNull_WhenLogNull()
        {
            using (var ms = new MemoryStream())
            {
                Assert.Throws<ArgumentNullException>(() => new StreamWrapper(ms, null));
            }
        }

        [Test]
        public void StreamWrapper_BeginReadMessage_ThrowsObjectDisposed_WhenWrapperDisposed()
        {
            AssertMethodThrowsObjectDisposed((sw) => sw.BeginReadMessage(null, null));
        }

        [Test]
        public void StreamWrapper_EndReadMessage_ThrowsObjectDisposed_WhenWrapperDisposed()
        {
            AssertMethodThrowsObjectDisposed((sw) => sw.EndReadMessage(null));
        }

        [Test]
        public void StreamWrapper_ReadMessage_ThrowsObjectDisposed_WhenWrapperDisposed()
        {
            AssertMethodThrowsObjectDisposed((sw) => sw.ReadMessage());
        }

        [Test]
        public void StreamWrapper_WriteMessage_ThrowsObjectDisposed_WhenWrapperDisposed()
        {
            AssertMethodThrowsObjectDisposed((sw) => sw.WriteMessage(0, 0, new object()));
        }

        [Test]
        public void StreamWrapper_ReadMessage_ThrowsInvalidData_WhenUnknownMessage()
        {
            var log = Given_Correct_Log();
            var memoryStream = new MemoryStream(new byte[] { 255 });
            var streamWrapper = new StreamWrapper(memoryStream, log);

            Assert.Throws<InvalidDataException>(() => streamWrapper.ReadMessage());
        }

        [Test]
        public void StreamWrapper_ReadMessage_ThrowsInvalidData_WhenInvalidProtobufFormat()
        {
            var log = Given_Correct_Log();
            var memoryStream = new MemoryStream(new byte[] { (byte)MeaRequestType.Ping, // Message type
                                                             0, 0, 0, 0,                // Request ID
                                                             1, 0,                      // Payload length
                                                             1 });                      // Junk payload data

            var streamWrapper = new StreamWrapper(memoryStream, log);

            Assert.Throws<InvalidDataException>(() => streamWrapper.ReadMessage());
        }

        [Test]
        public void StreamWrapper_ApmReadMessage_ThrowsInvalidData_WhenInvalidProtobufFormat()
        {
            var log = Given_Correct_Log();
            var memoryStream = new MemoryStream(new byte[] { (byte)MeaRequestType.Ping, // Message type
                                                             0, 0, 0, 0,                // Request ID
                                                             1, 0,                      // Payload length
                                                             1 });                      // Junk payload data

            var streamWrapper = new StreamWrapper(memoryStream, log);

            var asyncResult = streamWrapper.BeginReadMessage(null, null);
            asyncResult.AsyncWaitHandle.WaitOne();

            Assert.Throws<InvalidDataException>(() => streamWrapper.EndReadMessage(asyncResult));
        }

        [Test]
        public void StreamWrapper_ReadMessage_ReturnsCorrectMessage_ForCorrectData()
        {
            var log = Given_Correct_Log();
            var request = new PingRequest { Message = "test" };
            var memoryStream = Given_MemoryStream_WithWrittenRequest(1, request);
            var streamWrapper = new StreamWrapper(memoryStream, log);

            var result = streamWrapper.ReadMessage();
            var resultMessage = result.Message as PingRequest;

            Assert.AreEqual(1, result.Id);
            Assert.AreEqual(request.Message, resultMessage.Message);
        }

        [Test]
        public void StreamWrapper_ApmReadMessage_ReturnsCorrectMessage_ForCorrectData()
        {
            var log = Given_Correct_Log();
            var request = new PingRequest { Message = "test" };
            var memoryStream = Given_MemoryStream_WithWrittenRequest(1, request);
            var streamWrapper = new StreamWrapper(memoryStream, log);

            var asyncResult = streamWrapper.BeginReadMessage(null, null);

            asyncResult.AsyncWaitHandle.WaitOne();

            var result = streamWrapper.EndReadMessage(asyncResult);
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

        private void AssertMethodThrowsObjectDisposed(Action<StreamWrapper> codeToTest)
        {
            var log = Given_Correct_Log();
            var memoryStream = new MemoryStream();
            var streamWrapper = new StreamWrapper(memoryStream, log);

            streamWrapper.Dispose();

            Assert.Throws<ObjectDisposedException>(() => codeToTest(streamWrapper));
        }

        private ILog Given_Correct_Log()
        {
            var logMock = new Mock<ILog>();
            return logMock.Object;
        }
    }
}
