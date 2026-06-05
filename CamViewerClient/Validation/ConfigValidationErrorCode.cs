namespace CamViewerClient.Validation
{
    /// <summary>
    /// 캠뷰어 설정 검증 오류의 종류를 정의한다.
    /// 화면에서는 오류 코드와 대상 정보를 기준으로 사용자 메시지를 표시할 수 있다.
    /// </summary>
    public enum ConfigValidationErrorCode
    {
        ConfigRequired = 1,
        StoreCodeRequired = 2,

        NvrNoInvalid = 100,
        NvrNoDuplicated = 101,
        NvrVendorRequired = 102,
        NvrConnectionTypeRequired = 103,
        NvrProviderKeyRequired = 104,
        NvrHostRequired = 105,
        NvrPortInvalid = 106,
        NvrChannelCountInvalid = 107,
        NvrUserIdRequired = 108,
        NvrPasswordRequired = 109,

        CounterNoInvalid = 200,
        CounterNvrNoInvalid = 201,
        CounterNvrNotFound = 202,
        CounterChannelNoInvalid = 203,
        CounterChannelOutOfRange = 204,
        CounterScreenPositionInvalid = 205,
        CounterScreenPositionDuplicated = 206,

        PlaybackOptionRequired = 300,
        BeforeSecondsInvalid = 301,
        AfterCompleteSecondsInvalid = 302
    }
}