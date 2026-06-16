using CamViewer.Models;
using System;

namespace CamViewer.Services
{
    /// <summary>
    /// CamViewer 실행 요청을 Player 화면이 준비될 때까지 보관한다.
    ///
    /// 최초 실행 요청은 저장소에서 직접 꺼내 처리하고,
    /// 프로그램 실행 중 전달된 새 요청은 이벤트를 통해 알린다.
    /// </summary>
    public interface IApplicationLaunchRequestStore
    {
        /// <summary>
        /// 새로운 실행 요청이 저장되었을 때 발생한다.
        ///
        /// 최초 실행 시 PlayerPresenter가 생성되기 전에 저장된 요청은
        /// 이벤트 수신 대상이 아니며 TryTakePendingRequest()로 처리한다.
        ///
        /// Pipe Server에서 요청을 저장할 경우 백그라운드 스레드에서
        /// 이벤트가 발생할 수 있으므로 이벤트 처리기는
        /// UI 스레드 전환을 고려해야 한다.
        /// </summary>
        event Action<ApplicationLaunchRequest>
            PendingRequestStored;

        /// <summary>
        /// 현재 처리되지 않은 실행 요청이 있는지 확인한다.
        /// </summary>
        bool HasPendingRequest { get; }

        /// <summary>
        /// 실행 요청을 저장한다.
        ///
        /// 이미 요청이 있으면 가장 최근 요청으로 교체한다.
        /// </summary>
        /// <param name="request">
        /// 저장할 실행 요청.
        /// </param>
        void SetPendingRequest(
            ApplicationLaunchRequest request);

        /// <summary>
        /// 보관된 요청을 가져오고 저장소에서 제거한다.
        /// </summary>
        /// <param name="request">
        /// 저장소에서 가져온 실행 요청.
        /// </param>
        /// <returns>
        /// 처리할 요청이 존재하면 true.
        /// </returns>
        bool TryTakePendingRequest(
            out ApplicationLaunchRequest request);

        /// <summary>
        /// 보관된 요청을 제거한다.
        /// </summary>
        void Clear();
    }
}