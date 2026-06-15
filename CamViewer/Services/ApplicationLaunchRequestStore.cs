using CamViewer.Models;
using System;

namespace CamViewer.Services
{
    /// <summary>
    /// 애플리케이션 실행 요청을 메모리에 보관하는 저장소이다.
    ///
    /// 향후 단일 실행 프로세스 간 통신에서
    /// 백그라운드 스레드로 요청이 들어올 수 있으므로
    /// 내부 접근을 lock으로 보호한다.
    /// </summary>
    public sealed class ApplicationLaunchRequestStore
        : IApplicationLaunchRequestStore
    {
        private readonly object _syncRoot =
            new object();

        private ApplicationLaunchRequest _pendingRequest;

        /// <summary>
        /// 현재 처리되지 않은 실행 요청이 있는지 확인한다.
        /// </summary>
        public bool HasPendingRequest
        {
            get
            {
                lock (_syncRoot)
                {
                    return _pendingRequest != null;
                }
            }
        }

        /// <summary>
        /// 처리할 실행 요청을 저장한다.
        ///
        /// 이미 요청이 존재하면 가장 최근 요청으로 교체한다.
        /// 외부 요청이 연속으로 전달될 경우 오래된 요청을 순차 재생하지 않고
        /// 마지막으로 전달된 요청만 재생하기 위한 정책이다.
        /// </summary>
        /// <param name="request">보관할 실행 요청.</param>
        public void SetPendingRequest(
            ApplicationLaunchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    "request");
            }

            lock (_syncRoot)
            {
                _pendingRequest =
                    CloneRequest(request);
            }
        }

        /// <summary>
        /// 보관된 실행 요청을 가져오고 저장소에서 제거한다.
        /// </summary>
        /// <param name="request">가져온 실행 요청.</param>
        /// <returns>보관된 실행 요청이 존재하면 true.</returns>
        public bool TryTakePendingRequest(
            out ApplicationLaunchRequest request)
        {
            lock (_syncRoot)
            {
                if (_pendingRequest == null)
                {
                    request = null;
                    return false;
                }

                request =
                    CloneRequest(_pendingRequest);

                _pendingRequest = null;

                return true;
            }
        }

        /// <summary>
        /// 현재 보관된 실행 요청을 제거한다.
        /// </summary>
        public void Clear()
        {
            lock (_syncRoot)
            {
                _pendingRequest = null;
            }
        }

        /// <summary>
        /// 외부에서 전달된 요청 객체가 저장 이후 변경되지 않도록
        /// 별도의 요청 인스턴스를 생성한다.
        /// </summary>
        /// <param name="request">복제할 요청.</param>
        /// <returns>복제된 실행 요청.</returns>
        private static ApplicationLaunchRequest CloneRequest(
            ApplicationLaunchRequest request)
        {
            if (request.IsExternalPlaybackRequest
                && request.ReferenceTime.HasValue)
            {
                return ApplicationLaunchRequest
                    .CreateExternalPlayback(
                        request.ReferenceTime.Value,
                        request.CounterNo);
            }

            return ApplicationLaunchRequest
                .CreateDirectLaunch();
        }
    }
}