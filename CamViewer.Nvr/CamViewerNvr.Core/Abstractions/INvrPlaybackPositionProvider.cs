using System;
using System.Threading;
using System.Threading.Tasks;
using CamViewer.Nvr.Core.Results;

namespace CamViewer.Nvr.Core.Abstractions
{
    /// <summary>
    /// NVR Provider가 실제 재생 위치 시간을 조회할 수 있을 때 선택적으로 구현하는 인터페이스이다.
    ///
    /// 모든 NVR이 현재 재생 중인 영상 시간을 제공하는 것은 아니므로,
    /// INvrProvider에 강제로 포함하지 않고 선택 기능으로 분리한다.
    /// </summary>
    public interface INvrPlaybackPositionProvider
    {
        /// <summary>
        /// 현재 재생 세션의 실제 영상재생시간을 조회한다.
        /// </summary>
        Task<NvrResult<DateTime>> GetPlaybackTimeAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken);
    }
}