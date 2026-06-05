using System;
using System.Collections.Generic;
using CamViewer.Models.ViewModels;

namespace CamViewer.Interfaces
{
    /// <summary>
    /// NVR 등록/수정 팝업에서 제공해야 하는 기능을 정의한다.
    ///
    /// 사용자는 제조사만 선택하며,
    /// 접속방식과 ProviderKey는 제조사에 따라 자동으로 결정된다.
    ///
    /// 연결 테스트는 저장된 설정이 아니라 현재 화면에 입력된 값을 기준으로 수행한다.
    /// </summary>
    public interface INvrEditView
    {
        /// <summary>
        /// 선택된 제조사명.
        /// 제조사가 선택되지 않았으면 null이다.
        /// </summary>
        string SelectedVendor { get; set; }

        /// <summary>
        /// 입력된 NVR IP 주소 또는 도메인.
        /// </summary>
        string Host { get; set; }

        /// <summary>
        /// 입력된 NVR 포트.
        /// </summary>
        int? Port { get; set; }

        /// <summary>
        /// 입력된 NVR 전체 채널 수.
        /// </summary>
        int? ChannelCount { get; set; }

        /// <summary>
        /// 입력된 NVR 로그인 ID.
        /// </summary>
        string UserId { get; set; }

        /// <summary>
        /// 입력된 NVR 로그인 비밀번호.
        /// </summary>
        string Password { get; set; }

        /// <summary>
        /// Provider별 추가 설정값.
        /// 초기 화면에서는 별도 입력 UI를 제공하지 않지만,
        /// 향후 제조사별 옵션 확장을 위해 유지한다.
        /// </summary>
        IDictionary<string, string> ProviderSettings { get; set; }

        /// <summary>
        /// NVR 등록/수정 팝업이 최초 표시될 때 발생한다.
        /// </summary>
        event EventHandler LoadViewEvent;

        /// <summary>
        /// 제조사 선택값이 변경되었을 때 발생한다.
        /// </summary>
        event EventHandler VendorChangedEvent;

        /// <summary>
        /// 연결 테스트 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler TestConnectionEvent;

        /// <summary>
        /// 저장 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler SaveEvent;

        /// <summary>
        /// 닫기 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler CloseEvent;

        /// <summary>
        /// 팝업 제목을 설정한다.
        /// 예: NVR 등록, NVR 수정
        /// </summary>
        /// <param name="title">화면 제목.</param>
        void SetTitle(string title);

        /// <summary>
        /// 신규 등록 또는 수정 대상 NVR번호를 화면에 표시한다.
        /// 신규 등록 시에는 "자동 지정"을 표시한다.
        /// </summary>
        /// <param name="displayText">화면에 표시할 NVR번호 문자열.</param>
        void SetNvrNoDisplay(string displayText);

        /// <summary>
        /// 선택할 수 있는 제조사 목록을 설정한다.
        /// </summary>
        /// <param name="items">제조사 선택 목록.</param>
        void SetVendorOptions(IEnumerable<VendorOptionItem> items);

        /// <summary>
        /// 선택된 제조사에 고정된 접속방식을 표시한다.
        /// </summary>
        /// <param name="connectionType">접속방식 문자열.</param>
        void SetConnectionType(string connectionType);

        /// <summary>
        /// 연결 테스트 진행 상태를 화면에 표시한다.
        /// 테스트 중에는 중복 클릭을 방지하기 위해 버튼을 비활성화할 수 있다.
        /// </summary>
        /// <param name="isRunning">연결 테스트 진행 여부.</param>
        /// <param name="statusText">표시할 상태 문자열.</param>
        void SetConnectionTestState(bool isRunning, string statusText);

        /// <summary>
        /// 사용자에게 메시지를 표시한다.
        /// </summary>
        /// <param name="message">표시할 메시지.</param>
        void ShowMessage(string message);

        /// <summary>
        /// NVR 등록/수정 팝업을 표시한다.
        /// </summary>
        void ShowView();

        /// <summary>
        /// NVR 등록/수정 팝업을 닫는다.
        /// </summary>
        void CloseView();
    }
}