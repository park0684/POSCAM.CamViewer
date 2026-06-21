using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Nvr.Core.Abstractions
{
    /// <summary>
    /// Provider가 재생 세션을 지정된 영상 시각과 상태로
    /// 정렬할 수 있을 때 선택적으로 구현하는 인터페이스.
    ///
    /// 제조사별 구현 방식은 서로 다를 수 있다.
    ///
    /// 예:
    /// - 기존 재생 핸들 직접 Seek
    /// - 내부 재생 핸들 교체
    /// - API 재생 세션 재생성
    /// - 제조사 전용 동기 재생 기능
    /// </summary>
    public interface INvrPlaybackAlignmentProvider
    {
        /// <summary>
        /// 지정된 재생 세션을 요청된 영상 시각과 상태로 정렬한다.
        ///
        /// 반환 규칙:
        /// 1. 성공 시 Data에는 이후 사용할 유효한 재생 세션이 있어야 한다.
        /// 2. 기존 세션을 그대로 사용한 경우 동일한 session을 반환할 수 있다.
        /// 3. 세션을 재생성한 경우 새로운 세션을 반환할 수 있다.
        /// 4. 새로운 세션을 반환하는 경우 기존 세션의 네이티브 재생은
        ///    Provider 내부에서 안전하게 종료되어야 한다.
        /// 5. RemainPaused가 true이면 반환 시 영상 시간이 진행되지 않아야 한다.
        /// 6. 요청된 방향 또는 속도를 적용하지 못하면 성공으로 반환하지 않는다.
        /// 7. 지원하지 않는 경우 NotSupported 상태를 반환한다.
        /// </summary>
        Task<NvrResult<INvrPlaybackSession>> AlignPlaybackAsync(
            INvrPlaybackSession session,
            NvrPlaybackAlignmentRequest request,
            CancellationToken cancellationToken);
    }
}