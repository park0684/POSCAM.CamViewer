using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Nvr.Core.Abstractions
{
    /// <summary>
    /// 하나의 제조사 Provider가 다중채널 재생을 처리하기 위한
    /// 고수준 재생 명령 인터페이스.
    ///
    /// CamViewer 공통 서비스는 이 인터페이스에 명령만 전달하며,
    /// 채널별 SDK 호출 순서나 동기화 방법에는 관여하지 않는다.
    /// </summary>
    public interface INvrPlaybackEngine
    {
        /// <summary>
        /// 제조사별 다중채널 재생 그룹을 준비한다.
        ///
        /// 구현체는 필요에 따라 내부적으로 재생, 디코딩,
        /// Seek 및 Pause를 수행할 수 있다.
        ///
        /// 성공 반환 시 그룹은 StartAsync를 호출할 수 있는
        /// 준비 상태여야 한다.
        /// </summary>
        Task<NvrResult<INvrPlaybackGroupSession>> OpenAsync(
            NvrPlaybackGroupRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// 준비된 재생 그룹의 모든 채널을 재생한다.
        /// </summary>
        Task<NvrResult> StartAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken);

        /// <summary>
        /// 재생 그룹의 모든 채널을 일시정지한다.
        /// </summary>
        Task<NvrResult> PauseAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken);

        /// <summary>
        /// 일시정지된 재생 그룹을 재개한다.
        /// </summary>
        Task<NvrResult> ResumeAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken);

        /// <summary>
        /// 재생 그룹을 지정된 공통 영상 시각으로 이동한다.
        ///
        /// Seek 순서, 키프레임 처리, 채널 간 보정 및
        /// 기존 재생 상태 복원은 제조사 구현체가 책임진다.
        /// </summary>
        Task<NvrResult> SeekAsync(
            INvrPlaybackGroupSession session,
            DateTime targetTime,
            CancellationToken cancellationToken);

        /// <summary>
        /// 재생 그룹의 방향을 변경한다.
        ///
        /// 제조사 SDK가 방향 변경을 직접 지원하지 않으면
        /// 구현체 내부에서 세션 재생성 등의 방법을 사용할 수 있다.
        /// </summary>
        Task<NvrResult> SetDirectionAsync(
            INvrPlaybackGroupSession session,
            NvrPlaybackDirection direction,
            CancellationToken cancellationToken);

        /// <summary>
        /// 재생속도를 변경한다.
        ///
        /// 속도 변경 전의 Playing 또는 Paused 상태는 유지해야 한다.
        /// </summary>
        Task<NvrResult> SetSpeedAsync(
            INvrPlaybackGroupSession session,
            NvrPlaybackSpeed speed,
            CancellationToken cancellationToken);

        /// <summary>
        /// 제조사에 적합한 방식으로 재생 그룹을 동기화한다.
        ///
        /// 공통 서비스는 목표 시각, 허용오차, 재시도 횟수,
        /// Pause 및 Seek 순서를 결정하지 않는다.
        /// </summary>
        Task<NvrResult> SynchronizeAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken);

        /// <summary>
        /// 재생 그룹의 현재 상태를 조회한다.
        /// </summary>
        Task<NvrResult<NvrPlaybackGroupStatus>> GetStatusAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken);

        /// <summary>
        /// 재생 그룹의 모든 채널을 중지하고
        /// 그룹이 보유한 재생 리소스를 정리한다.
        ///
        /// Provider 로그인은 유지할 수 있다.
        /// </summary>
        Task<NvrResult> StopAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken);
    }
}