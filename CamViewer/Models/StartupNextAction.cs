namespace CamViewer.Models
{
    /// <summary>
    /// CamViewer 시작 흐름 처리 후 Presenter가 수행해야 할 다음 동작을 나타낸다.
    /// 
    /// 이 enum은 인증, 설정 확인, 오프라인 실행 판단 등의 결과를
    /// 화면 흐름으로 변환하기 위해 사용한다.
    /// </summary>
    public enum StartupNextAction
    {
        /// <summary>
        /// 아직 다음 동작이 결정되지 않은 상태.
        /// 일반적으로 사용하지 않는다.
        /// </summary>
        None = 0,

        /// <summary>
        /// Player 화면으로 진행한다.
        /// 인증과 설정이 모두 준비된 상태이다.
        /// </summary>
        ContinueToPlayer = 1,

        /// <summary>
        /// 로그인 창을 표시해야 한다.
        /// 로컬 인증정보가 없거나 인증이 필요한 상태이다.
        /// </summary>
        ShowLogin = 2,

        /// <summary>
        /// <summary>
        /// 로그인 창을 표시해야 한다.
        /// 로컬 인증정보가 없거나 인증이 필요한 설정 화면을 표시해야 한다.
        /// 로컬 설정이 없거나 서버 설정 다운로드에 실패한 상태이다.
        /// </summary>
        OpenSettings = 3,

        /// <summary>
        /// 서버에 더 최신 설정이 있어 사용자에게 다운로드 여부를 확인해야 한다.
        /// </summary>
        ConfirmServerConfigDownload = 4,

        /// <summary>
        /// 프로그램을 종료해야 한다.
        /// 인증 또는 설정 조건을 만족하지 못한 상태이다.
        /// </summary>
        Close = 9
    }
}