using CamViewer.Nvr.Core.Results;

namespace CamViewer.Nvr.Core.Abstractions
{
    /// <summary>
    /// 제조사 Provider가 고수준 다중채널 재생 엔진을
    /// 생성할 수 있을 때 구현하는 선택 인터페이스다.
    /// </summary>
    public interface INvrPlaybackEngineProvider
    {
        NvrResult<INvrPlaybackEngine> CreatePlaybackEngine();
    }
}
