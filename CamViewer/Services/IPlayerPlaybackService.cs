using System.Threading;
using System.Threading.Tasks;
using CamViewer.Models;

namespace CamViewer.Services
{
    /// <summary>
    /// PlayerPresenter에서 사용할 재생 서비스 인터페이스이다.
    ///
    /// 실제 NVR Provider가 비동기 방식으로 동작하므로,
    /// PlayerPlaybackService도 비동기 방식으로 정의한다.
    /// </summary>
    public interface IPlayerPlaybackService
    {
        /// <summary>
        /// 현재 재생 상태.
        /// </summary>
        PlaybackState CurrentState { get; }

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
    }
}