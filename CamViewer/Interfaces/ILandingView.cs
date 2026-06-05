using System;

namespace CamViewer.Interfaces
{
    /// <summary>
    /// 캠뷰어 랜딩페이지에서 제공해야 하는 기능을 정의한다.
    ///
    /// 랜딩페이지는 로그인 입력을 직접 받지 않는다.
    /// 프로그램 시작 상태, 인증 상태, 설정 확인 상태를 표시하고,
    /// 하단 설정 버튼 클릭 이벤트만 Presenter에 전달한다.
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
        /// <param name="status">상태 메시지.</param>
        void SetStatus(string status);

        /// <summary>
        /// 상세 안내 메시지를 표시한다.
        /// </summary>
        /// <param name="message">상세 메시지.</param>
        void SetDetailMessage(string message);

        /// <summary>
        /// 진행 표시 여부를 설정한다.
        /// </summary>
        /// <param name="visible">진행 표시 여부.</param>
        void SetProgressVisible(bool visible);

        /// <summary>
        /// 설정 버튼 활성화 여부를 설정한다.
        /// </summary>
        /// <param name="enabled">활성화 여부.</param>
        void SetSettingsEnabled(bool enabled);

        /// <summary>
        /// 사용자에게 안내 메시지를 표시한다.
        /// </summary>
        /// <param name="message">표시할 메시지.</param>
        void ShowMessage(string message);

        /// <summary>
        /// 사용자에게 예/아니오 확인 메시지를 표시한다.
        /// </summary>
        /// <param name="message">확인할 메시지.</param>
        /// <returns>예를 선택하면 true.</returns>
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