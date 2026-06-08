using System.Threading;
using System.Threading.Tasks;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;

namespace CamViewer.Nvr.Core.Abstractions
{
    /// <summary>
    /// NVR Provider가 채널 영상 원본 정보를 조회할 수 있을 때 선택적으로 구현하는 인터페이스이다.
    /// 
    /// 모든 제조사 NVR이 같은 방식으로 해상도 정보를 제공하지 않으므로,
    /// INvrProvider에 강제로 포함하지 않고 선택 기능으로 분리한다.
    /// </summary>
    public interface INvrVideoSourceInfoProvider
    {
        /// <summary>
        /// 지정한 채널의 영상 원본 정보를 조회한다.
        /// </summary>
        Task<NvrResult<NvrVideoSourceInfo>> GetVideoSourceInfoAsync(
            int channelNo,
            CancellationToken cancellationToken);
    }
}