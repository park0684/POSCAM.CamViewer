namespace CamViewer.Models
{
    /// <summary>
    /// CamViewer 시작 흐름 처리 결과를 나타낸다.
    /// 
    /// Presenter는 이 결과를 보고
    /// 로그인 창 표시, 설정 창 표시, Player 화면 이동, 종료 등의
    /// 화면 흐름만 처리한다.
    /// </summary>
    public sealed class StartupFlowResult
    {
        /// <summary>
        /// 시작 흐름 처리 성공 여부.
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// Presenter가 수행해야 할 다음 동작.
        /// </summary>
        public StartupNextAction NextAction { get; private set; }

        /// <summary>
        /// 사용자에게 표시할 주요 메시지.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// 상태 표시용 상세 메시지.
        /// </summary>
        public string DetailMessage { get; private set; }

        /// <summary>
        /// 오프라인 모드로 실행되는지 여부.
        /// </summary>
        public bool IsOfflineMode { get; private set; }

        /// <summary>
        /// 서버 설정 다운로드를 사용자가 취소했을 때
        /// 로컬 설정으로 계속 실행할 수 있는지 여부.
        /// </summary>
        public bool CanContinueWithoutDownload { get; private set; }

        /// <summary>
        /// StartupFlowResult 생성을 제한한다.
        /// 외부에서는 정적 생성 메서드를 사용한다.
        /// </summary>
        private StartupFlowResult()
        {
            Message = string.Empty;
            DetailMessage = string.Empty;
        }

        public static StartupFlowResult RequireLogin(
            string message)
        {
            return new StartupFlowResult
            {
                Success = false,
                NextAction = StartupNextAction.ShowLogin,
                Message = message ?? string.Empty,
                DetailMessage = message ?? message ?? string.Empty,
                IsOfflineMode = false
            };
        }

        /// <summary>
        /// Player 화면으로 진행 가능한 결과를 생성한다.
        /// </summary>
        public static StartupFlowResult ContinueToPlayer(
            string detailMessage,
            bool isOfflineMode)
        {
            return new StartupFlowResult
            {
                Success = true,
                NextAction = StartupNextAction.ContinueToPlayer,
                Message = string.Empty,
                DetailMessage = detailMessage ?? string.Empty,
                IsOfflineMode = isOfflineMode
            };
        }

        /// <summary>
        /// 설정 화면 표시가 필요한 결과를 생성한다.
        /// 사용자 표시 메시지와 상세 상태 메시지를 분리해서 지정한다.
        /// </summary>
        public static StartupFlowResult RequireSettings(
            string message,
            string detailMessage)
        {
            return new StartupFlowResult
            {
                Success = false,
                NextAction = StartupNextAction.OpenSettings,
                Message = message ?? string.Empty,
                DetailMessage = detailMessage ?? message ?? string.Empty,
                IsOfflineMode = false
            };
        }

        /// <summary>
        /// 설정 화면 표시가 필요한 결과를 생성한다.
        /// </summary>
        public static StartupFlowResult RequireSettings(
            string message)
        {
            return new StartupFlowResult
            {
                Success = false,
                NextAction = StartupNextAction.OpenSettings,
                Message = message ?? string.Empty,
                DetailMessage = message ?? string.Empty,
                IsOfflineMode = false
            };
        }

        /// <summary>
        /// 서버 설정 다운로드 확인이 필요한 결과를 생성한다.
        /// </summary>
        public static StartupFlowResult RequireServerConfigDownloadConfirm(
            string message)
        {
            return RequireServerConfigDownloadConfirm(
                message,
                message,
                true);
        }

        /// <summary>
        /// 서버 설정 다운로드 확인이 필요한 결과를 생성한다.
        /// 사용자 표시 메시지와 상세 상태 메시지를 분리해서 지정한다.
        /// </summary>
        public static StartupFlowResult RequireServerConfigDownloadConfirm(
            string message,
            string detailMessage)
        {
            return RequireServerConfigDownloadConfirm(
                message,
                detailMessage,
                true);
        }

        /// <summary>
        /// 서버 설정 다운로드 확인이 필요한 결과를 생성한다.
        /// </summary>
        /// <param name="message">사용자에게 표시할 확인 메시지.</param>
        /// <param name="detailMessage">상태 표시용 상세 메시지.</param>
        /// <param name="canContinueWithoutDownload">
        /// 사용자가 다운로드를 취소했을 때 로컬 설정으로 계속 실행할 수 있는지 여부.
        /// </param>
        public static StartupFlowResult RequireServerConfigDownloadConfirm(
            string message,
            string detailMessage,
            bool canContinueWithoutDownload)
        {
            return new StartupFlowResult
            {
                Success = true,
                NextAction = StartupNextAction.ConfirmServerConfigDownload,
                Message = message ?? string.Empty,
                DetailMessage = detailMessage ?? message ?? string.Empty,
                IsOfflineMode = false,
                CanContinueWithoutDownload = canContinueWithoutDownload
            };
        }

        /// <summary>
        /// 프로그램 종료가 필요한 결과를 생성한다.
        /// </summary>
        public static StartupFlowResult Close(
            string message)
        {
            return new StartupFlowResult
            {
                Success = false,
                NextAction = StartupNextAction.Close,
                Message = message ?? string.Empty,
                DetailMessage = message ?? string.Empty,
                IsOfflineMode = false
            };
        }


    }
}