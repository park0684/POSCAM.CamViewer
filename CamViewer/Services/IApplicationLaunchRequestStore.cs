using CamViewer.Models;

namespace CamViewer.Services
{
    /// <summary>
    /// CamViewer 실행 요청을 Player 화면이 준비될 때까지 보관한다.
    /// </summary>
    public interface IApplicationLaunchRequestStore
    {
        /// <summary>
        /// 현재 처리되지 않은 실행 요청이 있는지 확인한다.
        /// </summary>
        bool HasPendingRequest { get; }

        /// <summary>
        /// 실행 요청을 저장한다.
        /// </summary>
        void SetPendingRequest(
            ApplicationLaunchRequest request);

        /// <summary>
        /// 보관된 요청을 가져오고 저장소에서 제거한다.
        /// </summary>
        bool TryTakePendingRequest(
            out ApplicationLaunchRequest request);

        /// <summary>
        /// 보관된 요청을 제거한다.
        /// </summary>
        void Clear();
    }
}