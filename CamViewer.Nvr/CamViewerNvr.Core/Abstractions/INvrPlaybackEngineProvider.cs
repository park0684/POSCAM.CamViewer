using CamViewer.Nvr.Core.Results;

namespace CamViewer.Nvr.Core.Abstractions
{
    /// <summary>
    /// 제조사 Provider가 고수준 재생 엔진을 제공할 때
    /// 선택적으로 구현하는 인터페이스.
    ///
    /// CamViewer 공통 프로젝트는 제조사별 SDK나 API를 알지 않고,
    /// 이 인터페이스를 통해 재생 엔진만 전달받는다.
    /// </summary>
    public interface INvrPlaybackEngineProvider
    {
        /// <summary>
        /// 현재 로그인된 Provider에 연결된 재생 엔진을 생성한다.
        ///
        /// 재생 엔진은 제조사별 다음 처리를 책임진다.
        /// - 다중채널 재생 세션 생성
        /// - Seek 및 키프레임 처리
        /// - 채널 간 동기화와 보정
        /// - 역재생 및 정방향 전환
        /// - 제조사별 오류 복구
        /// </summary>
        NvrResult<INvrPlaybackEngine> CreatePlaybackEngine();
    }
}