using CamViewer.Models;
using CamViewerClient.Models.Auth;
using CamViewerClient.Models.Config;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Services
{
    /// <summary>
    /// CamViewer 시작 흐름에서 인증 이후 필요한 설정 확인,
    /// 서버 설정 다운로드, 서버 설정 버전 확인,
    /// 오프라인 실행 가능 여부 판단을 담당하는 서비스 인터페이스이다.
    /// 
    /// Presenter는 이 인터페이스를 통해 시작 흐름 결과만 받고,
    /// 실제 화면 이동, 로그인창 표시, 설정창 표시는 Presenter에서 처리한다.
    /// </summary>
    public interface ILandingStartupService
    {
        /// <summary>
        /// 로컬 설정 파일이 존재하고 정상적으로 불러와지는지 확인한다.
        /// </summary>
        /// <returns>로컬 설정을 사용할 수 있으면 true.</returns>
        bool CanUseLocalConfig();

        /// <summary>
        /// AuthServer에서 최신 캠뷰어 설정을 다운로드하고 로컬 설정 파일로 저장한다.
        /// </summary>
        Task<StartupFlowResult> DownloadServerConfigAsync(
            ViewerAuthToken token,
            string hwid,
            string programVersion,
            CancellationToken cancellationToken);

        /// <summary>
        /// 온라인 인증 성공 후 로컬 설정, 서버 설정 버전, 다운로드 필요 여부를 확인한다.
        /// </summary>
        Task<StartupFlowResult> CheckConfigAfterOnlineAuthAsync(
            ViewerAuthToken token,
            string hwid,
            string programVersion,
            CancellationToken cancellationToken);

        /// <summary>
        /// 서버 접속 실패 시 오프라인 실행 가능 여부를 확인한다.
        /// 
        /// 로컬 인증 토큰과 로컬 설정이 모두 유효해야 Player 화면으로 진행할 수 있다.
        /// </summary>
        /// <returns>시작 흐름 처리 결과.</returns>
        StartupFlowResult CheckOfflineStartup();
    }
}