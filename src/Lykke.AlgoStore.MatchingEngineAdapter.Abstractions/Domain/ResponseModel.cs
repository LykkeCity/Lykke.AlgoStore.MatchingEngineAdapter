using System.Collections.Generic;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Strings;
using ProtoBuf;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain
{
    [ProtoContract]
    public class ResponseModel
    {
        protected static readonly Dictionary<ErrorCodeType, string> StatusCodesMap = new Dictionary<ErrorCodeType, string>
        {
            {ErrorCodeType.LowBalance, ErrorMessages.LowBalance},
            {ErrorCodeType.AlreadyProcessed, ErrorMessages.AlreadyProcessed},
            {ErrorCodeType.UnknownAsset, ErrorMessages.UnknownAsset},
            {ErrorCodeType.NoLiquidity, ErrorMessages.NoLiquidity},
            {ErrorCodeType.NotEnoughFunds, ErrorMessages.NotEnoughFunds},
            {ErrorCodeType.Dust, ErrorMessages.Dust},
            {ErrorCodeType.ReservedVolumeHigherThanBalance, ErrorMessages.ReservedVolumeHigherThanBalance},
            {ErrorCodeType.NotFound, ErrorMessages.NotFound},
            {ErrorCodeType.BalanceLowerThanReserved, ErrorMessages.BalanceLowerThanReserved},
            {ErrorCodeType.LeadToNegativeSpread, ErrorMessages.LeadToNegativeSpread},
            {ErrorCodeType.Runtime, ErrorMessages.RuntimeError},
        };

        [ProtoMember (0, IsRequired = false)]
        public ErrorModel Error { get; set; }

        public enum ErrorCodeType
        {
            InvalidInputField = 0,
            LowBalance = 401,
            AlreadyProcessed = 402,
            UnknownAsset = 410,
            NoLiquidity = 411,
            NotEnoughFunds = 412,
            Dust = 413,
            ReservedVolumeHigherThanBalance = 414,
            NotFound = 415,
            BalanceLowerThanReserved = 416,
            LeadToNegativeSpread = 417,
            Runtime = 500
        }

        [ProtoContract]
        public class ErrorModel
        {
            [ProtoMember(0, IsRequired = true)]
            public ErrorCodeType Code { get; set; }

            /// <summary>
            /// In case ErrorCoderType = 0
            /// </summary>
            [ProtoMember(1, IsRequired = false)]           
            public string Field { get; set; }

            /// <summary>
            /// Localized Error message
            /// </summary>
            [ProtoMember(2, IsRequired = true)]
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
                StatusCodesMap.TryGetValue(errorCodeType, out message);
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
        [ProtoMember(1, IsRequired = false)]
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
                StatusCodesMap.TryGetValue(errorCodeType, out message);
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
