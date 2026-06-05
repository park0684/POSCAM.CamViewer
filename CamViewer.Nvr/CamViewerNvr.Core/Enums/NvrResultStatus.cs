namespace CamViewer.Nvr.Core.Enums
{
    /// <summary>
    /// NVR 처리 결과의 공통 상태를 정의한다.
    /// 제조사별 오류는 Provider에서 이 상태로 변환하여 반환한다.
    /// </summary>
    public enum NvrResultStatus
    {
        Success = 0,
        PartialSuccess = 1,
        Failed = 2,
        NotSupported = 3,
        ConnectionFailed = 4,
        LoginFailed = 5,
        NoRecordFound = 6,
        InvalidChannel = 7,
        ProviderNotFound = 8,
        SdkError = 9,
        ApiError = 10,
        Cancelled = 11,
        UnknownError = 99
    }
}