using System;

namespace CamViewer.Interfaces
{
    /// <summary>
    /// 캠뷰어 로그인 화면에서 제공해야 하는 기능을 정의한다.
    ///
    /// 로그인 화면은 매장코드와 비밀번호만 입력받는다.
    /// HWID, 장비명, 프로그램 버전은 Presenter 또는 Client 계층에서 자동 생성한다.
    /// </summary>
    public interface ILoginView
    {
        /// <summary>
        /// 사용자가 입력한 매장코드.
        /// 서버 요청에서는 StoreId로 전달한다.
        /// </summary>
        string StoreId { get; }

        /// <summary>
        /// 사용자가 입력한 매장 비밀번호.
        /// </summary>
        string StorePassword { get; }

        /// <summary>
        /// 사용자가 입력하거나 수정한 캠뷰어 장비명.
        /// 기본값은 PC 이름이다.
        /// </summary>
        string DeviceName { get; }


        /// <summary>
        /// 로그인 화면이 최초 표시될 때 발생한다.
        /// </summary>
        event EventHandler LoadViewEvent;

        /// <summary>
        /// 로그인 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler LoginEvent;

        /// <summary>
        /// 종료 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler ExitEvent;

        /// <summary>
        /// 로그인 진행 상태를 표시한다.
        /// </summary>
        void SetBusy(bool isBusy, string message);

        /// <summary>
        /// 화면 메시지를 표시한다.
        /// </summary>
        void SetMessage(string message);

        /// <summary>
        /// 로그인 성공 상태로 화면을 닫는다.
        /// </summary>
        void CloseWithSuccess();

        /// <summary>
        /// 로그인 취소 또는 종료 상태로 화면을 닫는다.
        /// </summary>
        void CloseWithCancel();

        /// <summary>
        /// 로그인 화면을 표시한다.
        /// </summary>
        void ShowView();

        /// <summary>
        /// 로그인 화면을 소유자 창 기준으로 표시한다.
        /// </summary>
        bool ShowDialogView();

        /// <summary>
        /// 로그인 화면의 장비명 기본값을 설정한다.
        /// </summary>
        void SetDeviceName(string deviceName);
    }
}