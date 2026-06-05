using System;

namespace CamViewer.Interfaces
{
    /// <summary>
    /// 캠뷰어 랜딩페이지에서 제공해야 하는 기능을 정의한다.
    ///
    /// 랜딩페이지는 로그인 입력을 직접 받지 않는다.
    /// 인증 상태, 설정 상태, 실행 상태를 표시하고 설정 버튼 이벤트만 전달한다.
    /// </summary>
    public interface ILandingView
    {
        /// <summary>
        /// 랜딩페이지가 최초 표시될 때 발생한다.
        /// </summary>
        event EventHandler LoadViewEvent;

        /// <summary>
        /// 설정 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler SettingsEvent;

        /// <summary>
        /// 현재 상태 메시지를 표시한다.
        /// </summary>
        void SetStatus(string status);

        /// <summary>
        /// 상세 메시지를 표시한다.
        /// </summary>
        void SetDetailMessage(string message);

        /// <summary>
        /// 진행 표시 여부를 설정한다.
        /// </summary>
        void SetProgressVisible(bool visible);

        /// <summary>
        /// 설정 버튼 활성화 여부를 설정한다.
        /// </summary>
        void SetSettingsEnabled(bool enabled);

        /// <summary>
        /// 사용자에게 안내 메시지를 표시한다.
        /// </summary>
        void ShowMessage(string message);

        /// <summary>
        /// 사용자에게 예/아니오 확인 메시지를 표시한다.
        /// </summary>
        bool Confirm(string message);

        /// <summary>
        /// 랜딩페이지를 표시한다.
        /// </summary>
        void ShowView();

        /// <summary>
        /// 랜딩페이지를 닫는다.
        /// </summary>
        void CloseView();
    }
}