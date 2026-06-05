using System.Collections.Generic;

namespace CamViewerClient.Validation
{
    /// <summary>
    /// 캠뷰어 설정 전체 검증 결과를 나타낸다.
    /// </summary>
    public sealed class ConfigValidationResult
    {
        /// <summary>
        /// 검증 결과를 초기화한다.
        /// </summary>
        public ConfigValidationResult()
        {
            Errors = new List<ConfigValidationError>();
        }

        /// <summary>
        /// 설정이 유효한지 여부.
        /// 오류가 하나도 없으면 true이다.
        /// </summary>
        public bool IsValid
        {
            get { return Errors.Count == 0; }
        }

        /// <summary>
        /// 설정에서 발견된 오류 목록.
        /// </summary>
        public IList<ConfigValidationError> Errors { get; private set; }

        /// <summary>
        /// 검증 오류를 추가한다.
        /// </summary>
        public void AddError(
            ConfigValidationErrorCode errorCode,
            string targetType,
            string propertyName,
            string message,
            int? nvrNo = null,
            int? counterNo = null)
        {
            Errors.Add(new ConfigValidationError
            {
                ErrorCode = errorCode,
                TargetType = targetType,
                PropertyName = propertyName,
                Message = message,
                NvrNo = nvrNo,
                CounterNo = counterNo
            });
        }
    }
}