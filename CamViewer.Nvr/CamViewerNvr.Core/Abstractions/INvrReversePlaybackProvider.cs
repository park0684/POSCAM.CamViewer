using System;
using System.Threading;
using System.Threading.Tasks;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Core.Models;

namespace CamViewer.Nvr.Core.Abstractions
{
    /// <summary>
    /// NVR Provider가 역재생을 지원할 때 선택적으로 구현하는 인터페이스이다.
    /// 
    /// 모든 NVR Provider가 역재생을 지원하지 않을 수 있으므로,
    /// INvrProvider에 강제로 포함하지 않고 선택 기능으로 분리한다.
    /// </summary>
    public interface INvrReversePlaybackProvider
    {
        /// <summary>
        /// 지정된 재생 요청과 역재생 시작 시각을 기준으로 역재생을 시작한다.
        /// </summary>
        Task<NvrResult<INvrPlaybackSession>> PlayReverseByTimeAsync(
            NvrPlaybackRequest request,
            DateTime reverseStartTime,
            CancellationToken cancellationToken);
    }
}