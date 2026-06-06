using CamViewer.Models;

namespace CamViewer.Services
{
    /// <summary>
    /// PlayerPresenter에서 사용할 재생 서비스 인터페이스이다.
    ///
    /// Presenter는 실제 NVR 제조사 SDK를 직접 알지 않고,
    /// 이 인터페이스를 통해 재생/일시정지/이동/정지 명령만 전달한다.
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
        PlayerPlaybackResult Play(PlayerPlaybackRequest request);

        /// <summary>
        /// 재생을 일시정지한다.
        /// </summary>
        PlayerPlaybackResult Pause();

        /// <summary>
        /// 일시정지 상태에서 재생을 재개한다.
        /// </summary>
        PlayerPlaybackResult Resume();

        /// <summary>
        /// 현재 재생 위치를 지정 초만큼 이동한다.
        /// </summary>
        PlayerPlaybackResult SeekSeconds(int seconds);

        /// <summary>
        /// 빠른재생을 요청한다.
        /// </summary>
        PlayerPlaybackResult FastForward();

        /// <summary>
        /// 빠른 역재생을 요청한다.
        /// </summary>
        PlayerPlaybackResult FastReverse();

        /// <summary>
        /// 재생을 중지한다.
        /// </summary>
        PlayerPlaybackResult Stop();
    }
}