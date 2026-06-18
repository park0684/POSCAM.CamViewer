namespace CamViewer.Models
{
    /// <summary>
    /// 재생 실패 원인을 사용자 대응 방식에 따라 구분한다.
    /// </summary>
    public enum PlaybackFailureCategory
    {
        /// <summary>
        /// 오류가 없거나 성공 결과이다.
        /// </summary>
        None = 0,

        /// <summary>
        /// 네트워크 또는 일시적인 NVR 처리 실패로
        /// 사용자가 다시 시도할 수 있다.
        /// </summary>
        Retryable = 1,

        /// <summary>
        /// NVR 주소, 포트, 계정, 채널 또는 Provider 설정을
        /// 확인해야 하는 오류이다.
        /// </summary>
        Configuration = 2,

        /// <summary>
        /// 지정한 시간 구간에 녹화 영상이 없다.
        /// </summary>
        NoRecord = 3,

        /// <summary>
        /// 현재 Provider 또는 장비에서 기능을 지원하지 않는다.
        /// </summary>
        NotSupported = 4,

        /// <summary>
        /// 사용자 또는 프로그램 흐름에 의해 작업이 취소됐다.
        /// </summary>
        Cancelled = 5,

        /// <summary>
        /// SDK 초기화, 프로그램 내부 상태 등
        /// 단순 재시도로 해결되지 않을 수 있는 오류이다.
        /// </summary>
        System = 6
    }

    /// <summary>
    /// PlayerPlaybackService 처리 결과를 나타낸다.
    /// </summary>
    public sealed class PlayerPlaybackResult
    {
        /// <summary>
        /// 처리 성공 여부.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 사용자 표시 메시지.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 오류 코드.
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// 실패 원인 분류.
        /// </summary>
        public PlaybackFailureCategory FailureCategory { get; set; }

        /// <summary>
        /// 동일 요청을 다시 시도할 수 있는 오류인지 여부.
        /// </summary>
        public bool IsRetryable
        {
            get
            {
                return !Success
                    && FailureCategory
                        == PlaybackFailureCategory.Retryable;
            }
        }

        /// <summary>
        /// NVR 또는 채널 설정 확인이 필요한지 여부.
        /// </summary>
        public bool RequiresConfigurationReview
        {
            get
            {
                return !Success
                    && FailureCategory
                        == PlaybackFailureCategory.Configuration;
            }
        }

        /// <summary>
        /// 성공 결과를 생성한다.
        /// </summary>
        public static PlayerPlaybackResult Ok(
            string message)
        {
            return new PlayerPlaybackResult
            {
                Success = true,
                Message = message,
                ErrorCode = string.Empty,
                FailureCategory =
                    PlaybackFailureCategory.None
            };
        }

        /// <summary>
        /// 기존 호출부와의 호환성을 유지하는 실패 결과 생성 메서드.
        /// 오류 코드에 따라 기본 실패 유형을 결정한다.
        /// </summary>
        public static PlayerPlaybackResult Fail(
            string message,
            string errorCode)
        {
            return Fail(
                message,
                errorCode,
                ClassifyLocalErrorCode(
                    errorCode));
        }

        /// <summary>
        /// 실패 유형을 명시하여 실패 결과를 생성한다.
        /// </summary>
        public static PlayerPlaybackResult Fail(
            string message,
            string errorCode,
            PlaybackFailureCategory failureCategory)
        {
            return new PlayerPlaybackResult
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode,
                FailureCategory = failureCategory
            };
        }

        /// <summary>
        /// CamViewer 내부 오류 코드를 사용자 대응 유형으로 분류한다.
        /// </summary>
        private static PlaybackFailureCategory ClassifyLocalErrorCode(
            string errorCode)
        {
            if (string.IsNullOrWhiteSpace(
                errorCode))
            {
                return PlaybackFailureCategory.System;
            }

            switch (errorCode)
            {
                case "PLAYBACK_CANCELLED":
                case "REVERSE_PLAYBACK_CANCELLED":
                case "FORWARD_PLAYBACK_CANCELLED":
                case "PLAYBACK_SPEED_CANCELLED":
                    return PlaybackFailureCategory.Cancelled;

                case "PLAYBACK_REQUEST_REQUIRED":
                case "PLAYBACK_CHANNEL_REQUIRED":
                case "INVALID_PLAYBACK_RANGE":
                case "NVR_CONFIG_REQUIRED":
                case "NVR_PROVIDER_CREATE_FAILED":
                case "NVR_PROVIDER_NOT_FOUND":
                    return PlaybackFailureCategory.Configuration;

                case "REVERSE_PLAYBACK_NOT_SUPPORTED":
                case "REVERSE_PROVIDER_NOT_IMPLEMENTED":
                case "PLAYBACK_SPEED_NOT_SUPPORTED":
                    return PlaybackFailureCategory.NotSupported;

                default:
                    return PlaybackFailureCategory.System;
            }
        }
    }
}