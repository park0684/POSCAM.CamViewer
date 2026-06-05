namespace CamViewerClient.Validation
{
    /// <summary>
    /// 캠뷰어 설정 검증 중 발견된 개별 오류 정보를 나타낸다.
    /// </summary>
    public sealed class ConfigValidationError
    {
        /// <summary>
        /// 검증 오류 종류.
        /// </summary>
        public ConfigValidationErrorCode ErrorCode { get; set; }

        /// <summary>
        /// 오류가 발생한 설정 영역.
        /// 예: ViewerConfig, NvrConfig, CounterMap, PlaybackOption
        /// </summary>
        public string TargetType { get; set; }

        /// <summary>
        /// 오류가 발생한 속성명.
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// 오류 대상 NVR번호.
        /// NVR 관련 오류가 아니면 null이다.
        /// </summary>
        public int? NvrNo { get; set; }

        /// <summary>
        /// 오류 대상 계산대번호.
        /// 계산대 관련 오류가 아니면 null이다.
        /// </summary>
        public int? CounterNo { get; set; }

        /// <summary>
        /// 사용자에게 표시할 수 있는 오류 설명.
        /// </summary>
        public string Message { get; set; }
    }
}