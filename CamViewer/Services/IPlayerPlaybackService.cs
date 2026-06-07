using CamViewer.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Services
{
    /// <summary>
    /// PlayerPresenter에서 사용할 재생 서비스 인터페이스이다.
    ///
    /// 실제 NVR Provider가 비동기 방식으로 동작하므로,
    /// PlayerPlaybackService도 비동기 방식으로 정의한다.
    /// </summary>
    public interface IPlayerPlaybackService : IDisposable
    {
        /// <summary>
        /// 현재 재생 상태.
        /// </summary>
        PlaybackState CurrentState { get; }

        /// <summary>
        /// 현재 재생 중인 영상 시각.
        /// 재생 중이면 경과 시간을 반영한 추정 시각을 반환한다.
        /// </summary>
        DateTime? CurrentPlaybackTime { get; }

        /// <summary>
        /// 재생 요청을 시작한다.
        /// </summary>
        Task<PlayerPlaybackResult> PlayAsync(
            PlayerPlaybackRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// 재생을 일시정지한다.
        /// </summary>
        Task<PlayerPlaybackResult> PauseAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// 일시정지 상태에서 재생을 재개한다.
        /// </summary>
        Task<PlayerPlaybackResult> ResumeAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// 현재 재생 위치를 지정 초만큼 이동한다.
        /// </summary>
        Task<PlayerPlaybackResult> SeekSecondsAsync(
            int seconds,
            CancellationToken cancellationToken);

        /// <summary>
        /// 빠른재생을 요청한다.
        /// </summary>
        Task<PlayerPlaybackResult> FastForwardAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// 빠른 역재생을 요청한다.
        /// </summary>
        Task<PlayerPlaybackResult> FastReverseAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// 재생을 중지한다.
        /// </summary>
        Task<PlayerPlaybackResult> StopAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// 재생속도를 변경한다.
        /// 재생 중이면 즉시 적용하고, 재생 전이면 다음 재생부터 적용한다.
        /// </summary>
        Task<PlayerPlaybackResult> SetPlaybackSpeedAsync(
            PlaybackSpeed speed,
            CancellationToken cancellationToken);
    }
}