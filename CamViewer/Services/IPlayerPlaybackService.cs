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
        /// 현재 선택된 재생속도.
        /// </summary>
        PlaybackSpeed CurrentPlaybackSpeed { get; }

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

        ///// <summary>
        ///// 빠른재생을 요청한다.
        ///// </summary>
        //Task<PlayerPlaybackResult> FastForwardAsync(
        //    CancellationToken cancellationToken);

        ///// <summary>
        ///// 빠른 역재생을 요청한다.
        ///// </summary>
        //Task<PlayerPlaybackResult> FastReverseAsync(
        //    CancellationToken cancellationToken);

        /// <summary>
        /// 역재생을 요청한다.
        /// 현재 Provider가 지원하지 않으면 실패 결과를 반환한다.
        /// </summary>
        Task<PlayerPlaybackResult> RewindAsync(
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

        /// <summary>
        /// Provider가 실제 재생시간 조회를 지원하면 실제 시간을 동기화한다.
        /// 지원하지 않거나 실패하면 추정 시간을 반환한다.
        /// </summary>
        Task<DateTime?> SyncPlaybackTimeAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// 현재 재생 중인 채널들의 시간 동기화 상태를 조회한다.
        /// Provider가 실제 재생시간을 지원하면 실제 시간을 사용하고,
        /// 지원하지 않으면 추정 시간을 사용한다.
        /// </summary>
        Task<PlaybackSyncStatus> GetPlaybackSyncStatusAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// 현재 재생 세션을 수동으로 동기화한다.
        /// </summary>
        Task<PlayerPlaybackResult> ResyncPlaybackSessionsAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// 지정한 영상재생시간으로 이동한다.
        /// </summary>
        Task<PlayerPlaybackResult> SeekToTimeAsync(
            DateTime targetTime,
            CancellationToken cancellationToken);

        /// <summary>
        /// 타임라인에서 선택한 절대 시각으로 이동한다.
        ///
        /// 현재 배속이 1배속이 아니면 기존 세션에 별도로
        /// 1배속 명령을 보내지 않고, 논리 속도를 1배속으로 변경한 뒤
        /// 새 재생 핸들을 선택한 시각에서 생성한다.
        /// </summary>
        Task<PlayerPlaybackResult> SeekTimelineToTimeAsync(
            DateTime targetTime,
            CancellationToken cancellationToken);

        /// <summary>
        /// 재생 대상 채널의 영상 원본 정보를 조회한다.
        /// Provider가 지원하지 않으면 실패 결과를 반환한다.
        /// </summary>
        Task<PlayerVideoSourceInfoResult> GetVideoSourceInfoAsync(
            PlayerChannelTarget channel,
            CancellationToken cancellationToken);

        /// <summary>
        /// 현재 영상재생시간을 기준으로 정방향 재생으로 전환한다.
        /// 역재생 중 재생 버튼을 눌렀을 때 사용한다.
        /// </summary>
        Task<PlayerPlaybackResult> PlayForwardFromCurrentTimeAsync(
            CancellationToken cancellationToken);
    }
}