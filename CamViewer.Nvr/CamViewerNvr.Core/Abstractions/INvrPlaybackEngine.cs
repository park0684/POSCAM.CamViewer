using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Nvr.Core.Abstractions
{
    /// <summary>
    /// 하나의 NVR에 속한 여러 채널을 제조사 방식으로
    /// 묶어서 제어하는 고수준 재생 엔진 계약이다.
    /// </summary>
    public interface INvrPlaybackEngine
    {
        Task<NvrResult<INvrPlaybackGroupSession>> OpenAsync(
            NvrPlaybackGroupRequest request,
            CancellationToken cancellationToken);

        Task<NvrResult> StartAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken);

        Task<NvrResult> PauseAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken);

        Task<NvrResult> ResumeAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken);

        Task<NvrResult> StopAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken);

        Task<NvrResult> SeekAsync(
            INvrPlaybackGroupSession session,
            DateTime targetTime,
            CancellationToken cancellationToken);

        Task<NvrResult> SetDirectionAsync(
            INvrPlaybackGroupSession session,
            NvrPlaybackDirection direction,
            CancellationToken cancellationToken);

        Task<NvrResult> SetSpeedAsync(
            INvrPlaybackGroupSession session,
            NvrPlaybackSpeed speed,
            CancellationToken cancellationToken);

        Task<NvrResult> SynchronizeAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken);

        Task<NvrResult<NvrPlaybackGroupStatus>> GetStatusAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken);
    }
}
