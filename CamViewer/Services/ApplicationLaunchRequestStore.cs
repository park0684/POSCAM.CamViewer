using CamViewer.Models;
using System;

namespace CamViewer.Services
{
    /// <summary>
    /// 애플리케이션 실행 요청을 메모리에 보관하는 저장소이다.
    ///
    /// 역할:
    /// - Player가 준비되기 전 최초 실행 요청 보관
    /// - 실행 중 새로 전달된 외부 요청 보관
    /// - 새 요청 저장 이벤트 발생
    ///
    /// Named Pipe 수신 스레드와 WinForms UI 스레드에서
    /// 동시에 접근할 수 있으므로 내부 상태를 lock으로 보호한다.
    /// </summary>
    public sealed class ApplicationLaunchRequestStore
        : IApplicationLaunchRequestStore
    {
        private readonly object _syncRoot =
            new object();

        private ApplicationLaunchRequest
            _pendingRequest;

        /// <summary>
        /// 새로운 실행 요청이 저장되었을 때 발생한다.
        ///
        /// SetPendingRequest()를 호출한 스레드에서 발생한다.
        /// Pipe Server가 호출하면 백그라운드 스레드에서 발생하므로
        /// 이벤트 처리기에서 WinForms 컨트롤에 직접 접근하면 안 된다.
        /// </summary>
        public event Action<ApplicationLaunchRequest>
            PendingRequestStored;

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
        /// <param name="request">
        /// 보관할 실행 요청.
        /// </param>
        public void SetPendingRequest(
            ApplicationLaunchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    "request");
            }

            /*
             * 외부에서 전달된 객체가 저장 후 변경되지 않도록
             * 별도의 요청 인스턴스로 복제한다.
             */
            ApplicationLaunchRequest storedRequest =
                CloneRequest(
                    request);

            Action<ApplicationLaunchRequest>
                pendingRequestStoredHandler;

            lock (_syncRoot)
            {
                /*
                 * 기존 요청이 아직 처리되지 않았더라도
                 * 가장 최근 요청으로 교체한다.
                 */
                _pendingRequest =
                    storedRequest;

                /*
                 * 이벤트 처리기는 lock 내부에서 실행하지 않는다.
                 *
                 * 이벤트 처리기에서 다시 저장소 메서드를 호출할 수 있으므로
                 * lock 내부에서 이벤트를 실행하면 교착 또는 불필요한
                 * 잠금 대기가 발생할 수 있다.
                 */
                pendingRequestStoredHandler =
                    PendingRequestStored;
            }

            if (pendingRequestStoredHandler != null)
            {
                /*
                 * 이벤트 수신 측에서 요청 객체를 수정하더라도
                 * 저장소 내부 요청이 변경되지 않도록 다시 복제하여 전달한다.
                 */
                pendingRequestStoredHandler(
                    CloneRequest(
                        storedRequest));
            }
        }

        /// <summary>
        /// 보관된 실행 요청을 가져오고 저장소에서 제거한다.
        /// </summary>
        /// <param name="request">
        /// 가져온 실행 요청.
        /// </param>
        /// <returns>
        /// 보관된 실행 요청이 존재하면 true.
        /// </returns>
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
                    CloneRequest(
                        _pendingRequest);

                _pendingRequest = null;

                return true;
            }
        }

        /// <summary>
        /// 현재 보관된 실행 요청을 제거한다.
        ///
        /// Clear는 새 요청 저장이 아니므로
        /// PendingRequestStored 이벤트를 발생시키지 않는다.
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
        /// <param name="request">
        /// 복제할 요청.
        /// </param>
        /// <returns>
        /// 복제된 실행 요청.
        /// </returns>
        private static ApplicationLaunchRequest CloneRequest(
            ApplicationLaunchRequest request)
        {
            if (request == null)
            {
                return null;
            }

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