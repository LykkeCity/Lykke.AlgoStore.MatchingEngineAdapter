using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Helpers;
using ProtoBuf;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain
{
    [ProtoContract]
    [ProtoInclude(7, typeof(ResponseModel<double>))]
    public class ResponseModel
    {
        [ProtoMember (1, IsRequired = false)]
        public ErrorModel Error { get; set; }

        [ProtoContract]
        public class ErrorModel
        {
            [ProtoMember(1, IsRequired = true)]
            public ErrorCodeType Code { get; set; }

            /// <summary>
            /// In case ErrorCoderType = 0
            /// </summary>
            [ProtoMember(2, IsRequired = false)]           
            public string Field { get; set; }

            /// <summary>
            /// Localized Error message
            /// </summary>
            [ProtoMember(3, IsRequired = true)]
            public string Message { get; set; }
        }

        public static ResponseModel CreateInvalidFieldError(string field, string message)
        {
            return new ResponseModel
            {
                Error = new ErrorModel
                {
                    Code = ErrorCodeType.InvalidInputField,
                    Field = field,
                    Message = message
                }
            };
        }

        public static ResponseModel CreateFail(ErrorCodeType errorCodeType, string message = null)
        {
            if (message == null)
            {
                message = errorCodeType.GetErrorMessage();
            }

            return new ResponseModel
            {
                Error = new ErrorModel
                {
                    Code = errorCodeType,
                    Message = message
                }
            };
        }

        private static readonly ResponseModel OkInstance = new ResponseModel();

        public static ResponseModel CreateOk()
        {
            return OkInstance;
        }
    }

    [ProtoContract]
    public class ResponseModel<T> : ResponseModel
    {
        [ProtoMember(2, IsRequired = true)]
        public T Result { get; set; }

        public static ResponseModel<T> CreateOk(T result)
        {
            return new ResponseModel<T>
            {
                Result = result
            };
        }

        public new static ResponseModel<T> CreateInvalidFieldError(string field, string message)
        {
            return new ResponseModel<T>
            {
                Error = new ErrorModel
                {
                    Code = ErrorCodeType.InvalidInputField,
                    Field = field,
                    Message = message
                }
            };
        }

        public new static ResponseModel<T> CreateFail(ErrorCodeType errorCodeType, string message = null)
        {
            if (message == null)
            {
                message = errorCodeType.GetErrorMessage();
            }

            return new ResponseModel<T>
            {
                Error = new ErrorModel
                {
                    Code = errorCodeType,
                    Message = message
                }
            };
        }
    }
}
