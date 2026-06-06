using System;
using CamViewer.Models;

namespace CamViewer.Services
{
    /// <summary>
    /// 실제 NVR 재생 연결 전 단계에서 사용하는 디버그용 재생 서비스이다.
    ///
    /// 실제 영상 재생은 하지 않고,
    /// PlayerPlaybackRequest를 받아 재생 상태만 관리한다.
    /// 이후 Dahua SDK 연결 서비스로 교체한다.
    /// </summary>
    public sealed class DebugPlayerPlaybackService : IPlayerPlaybackService
    {
        private PlayerPlaybackRequest _currentRequest;

        /// <summary>
        /// 현재 재생 상태.
        /// </summary>
        public PlaybackState CurrentState { get; private set; }

        /// <summary>
        /// DebugPlayerPlaybackService를 초기화한다.
        /// </summary>
        public DebugPlayerPlaybackService()
        {
            CurrentState = PlaybackState.Stopped;
        }

        /// <summary>
        /// 재생 요청을 시작한다.
        /// </summary>
        public PlayerPlaybackResult Play(PlayerPlaybackRequest request)
        {
            if (request == null)
            {
                return PlayerPlaybackResult.Fail(
                    "재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_REQUIRED");
            }

            if (request.Channels == null || request.Channels.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "재생할 채널 정보가 없습니다.",
                    "PLAYBACK_CHANNEL_REQUIRED");
            }

            _currentRequest = request;
            CurrentState = PlaybackState.Playing;

            return PlayerPlaybackResult.Ok(
                "재생 요청이 준비되었습니다. "
                + request.PlayStartTime.ToString("yyyy-MM-dd HH:mm:ss")
                + " ~ "
                + request.PlayEndTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        /// <summary>
        /// 재생을 일시정지한다.
        /// </summary>
        public PlayerPlaybackResult Pause()
        {
            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "일시정지할 재생 요청이 없습니다.",
                    "PLAYBACK_NOT_STARTED");
            }

            CurrentState = PlaybackState.Paused;

            return PlayerPlaybackResult.Ok("일시정지 요청이 처리되었습니다.");
        }

        /// <summary>
        /// 일시정지 상태에서 재생을 재개한다.
        /// </summary>
        public PlayerPlaybackResult Resume()
        {
            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "재개할 재생 요청이 없습니다.",
                    "PLAYBACK_NOT_STARTED");
            }

            CurrentState = PlaybackState.Playing;

            return PlayerPlaybackResult.Ok("재생 재개 요청이 처리되었습니다.");
        }

        /// <summary>
        /// 현재 재생 위치를 지정 초만큼 이동한다.
        /// </summary>
        public PlayerPlaybackResult SeekSeconds(int seconds)
        {
            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "이동할 재생 요청이 없습니다.",
                    "PLAYBACK_NOT_STARTED");
            }

            return PlayerPlaybackResult.Ok(
                seconds + "초 이동 요청이 처리되었습니다.");
        }

        /// <summary>
        /// 빠른재생을 요청한다.
        /// </summary>
        public PlayerPlaybackResult FastForward()
        {
            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "빠른재생할 재생 요청이 없습니다.",
                    "PLAYBACK_NOT_STARTED");
            }

            CurrentState = PlaybackState.FastForward;

            return PlayerPlaybackResult.Ok("빠른재생 요청이 처리되었습니다.");
        }

        /// <summary>
        /// 빠른 역재생을 요청한다.
        /// </summary>
        public PlayerPlaybackResult FastReverse()
        {
            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "빠른 역재생할 재생 요청이 없습니다.",
                    "PLAYBACK_NOT_STARTED");
            }

            CurrentState = PlaybackState.FastReverse;

            return PlayerPlaybackResult.Ok("빠른 역재생 요청이 처리되었습니다.");
        }

        /// <summary>
        /// 재생을 중지한다.
        /// </summary>
        public PlayerPlaybackResult Stop()
        {
            _currentRequest = null;
            CurrentState = PlaybackState.Stopped;

            return PlayerPlaybackResult.Ok("재생 중지 요청이 처리되었습니다.");
        }
    }
}