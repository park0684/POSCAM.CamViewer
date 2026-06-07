using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Nvr.Core.Abstractions
{
    /// <summary>
    /// 제조사별 NVR Provider가 구현해야 하는 공통 인터페이스.
    /// CamViewer 본체는 제조사별 SDK나 API를 직접 호출하지 않고 이 인터페이스만 사용한다.
    /// </summary>
    public interface INvrProvider : IDisposable
    {
        /// <summary>
        /// Provider 식별 정보.
        /// </summary>
        ProviderMetadata Metadata { get; }

        /// <summary>
        /// Provider 초기화 여부.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// NVR 로그인 여부.
        /// </summary>
        bool IsLoggedIn { get; }

        /// <summary>
        /// Provider가 지원하는 기능 목록을 반환한다.
        /// </summary>
        ProviderCapabilities GetCapabilities();

        /// <summary>
        /// SDK 또는 API Provider를 초기화한다.
        /// </summary>
        NvrResult Initialize();

        /// <summary>
        /// NVR에 로그인한다.
        /// </summary>
        Task<NvrResult> LoginAsync(
            NvrConnectionInfo connectionInfo,
            CancellationToken cancellationToken);

        /// <summary>
        /// 지정된 시각 기준으로 녹화 영상을 재생한다.
        /// </summary>
        Task<NvrResult<INvrPlaybackSession>> PlayByTimeAsync(
            NvrPlaybackRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// 지정된 재생 세션을 중지한다.
        /// </summary>
        Task<NvrResult> StopAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken);

        /// <summary>
        /// 지정된 재생 세션을 일시정지한다.
        /// 지원하지 않는 Provider는 NotSupported 상태를 반환한다.
        /// </summary>
        Task<NvrResult> PauseAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken);

        /// <summary>
        /// 일시정지된 재생 세션을 재개한다.
        /// 지원하지 않는 Provider는 NotSupported 상태를 반환한다.
        /// </summary>
        Task<NvrResult> ResumeAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken);

        /// <summary>
        /// 지정된 재생 세션을 특정 시각으로 이동한다.
        /// 지원하지 않는 Provider는 NotSupported 상태를 반환한다.
        /// </summary>
        Task<NvrResult> SeekAsync(
            INvrPlaybackSession session,
            DateTime targetTime,
            CancellationToken cancellationToken);

        /// <summary>
        /// NVR 접속 가능 여부를 확인한다.
        /// 지원하지 않는 Provider는 NotSupported 상태를 반환한다.
        /// </summary>
        Task<NvrResult> TestConnectionAsync(
            NvrConnectionInfo connectionInfo,
            CancellationToken cancellationToken);

        /// <summary>
        /// 지정된 시간 구간에 녹화 영상이 존재하는지 확인한다.
        /// 지원하지 않는 Provider는 NotSupported 상태를 반환한다.
        /// </summary>
        Task<NvrResult<bool>> QueryRecordExistsAsync(
            NvrRecordQueryRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// NVR에서 로그아웃한다.
        /// </summary>
        Task<NvrResult> LogoutAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Provider에서 마지막으로 발생한 오류 정보를 반환한다.
        /// </summary>
        NvrErrorInfo GetLastError();

        /// <summary>
        /// 재생속도를 변경한다.
        /// Provider가 지원하지 않으면 NotSupported를 반환한다.
        /// </summary>
        Task<NvrResult> SetPlaybackSpeedAsync(
            INvrPlaybackSession session,
            NvrPlaybackSpeed speed,
            CancellationToken cancellationToken);
    }
}