using System;
using System.Collections.Generic;
using CamViewer.Models.ViewModels;
using CamViewerClient.Enums;

namespace CamViewer.Interfaces
{
    /// <summary>
    /// 캠뷰어 설정 화면에서 제공해야 하는 기능을 정의한다.
    ///
    /// 설정 화면은 NVR 목록과 계산대 등록 목록을 표시하고,
    /// 추가, 수정, 삭제, 저장 요청을 Presenter에 전달한다.
    /// </summary>
    public interface ISettingsView
    {
        /// <summary>
        /// 현재 선택된 NVR번호.
        /// 선택된 행이 없으면 null이다.
        /// </summary>
        int? SelectedNvrNo { get; }

        /// <summary>
        /// 현재 선택된 계산대 등록정보의 식별 키.
        /// 선택된 행이 없으면 null이다.
        /// </summary>
        CounterMapKey SelectedCounterMapKey { get; }

        /// <summary>
        /// 영상검색 기준 시각 이전에 조회할 시간(초).
        /// 설정 저장 시 화면 값을 읽고,
        /// 설정 화면 로드 시 기존 설정값을 표시한다.
        /// </summary>
        int? AdjustSecond { get; set; }

        /// <summary>
        /// 화면이 최초 표시될 때 발생한다.
        /// </summary>
        event EventHandler LoadViewEvent;

        /// <summary>
        /// NVR 추가 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler AddNvrEvent;

        /// <summary>
        /// NVR 수정 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler EditNvrEvent;

        /// <summary>
        /// NVR 삭제 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler DeleteNvrEvent;

        /// <summary>
        /// 계산대 추가 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler AddCounterMapEvent;

        /// <summary>
        /// 계산대 수정 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler EditCounterMapEvent;

        /// <summary>
        /// 계산대 삭제 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler DeleteCounterMapEvent;

        /// <summary>
        /// 설정 저장 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler SaveEvent;

        /// <summary>
        /// 설정 화면 닫기 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler CloseEvent;

        /// <summary>
        /// NVR 목록을 화면에 표시한다.
        /// </summary>
        /// <param name="items">표시할 NVR 목록.</param>
        void SetNvrList(IEnumerable<NvrListItem> items);

        /// <summary>
        /// 계산대 등록 목록을 화면에 표시한다.
        /// </summary>
        /// <param name="items">표시할 계산대 등록 목록.</param>
        void SetCounterMapList(IEnumerable<CounterMapListItem> items);

        /// <summary>
        /// 현재 설정 버전과 동기화 상태를 화면에 표시한다.
        /// </summary>
        /// <param name="configVersion">설정 버전.</param>
        /// <param name="syncStatus">설정 동기화 상태.</param>
        /// <param name="lastDownloadedAtUtc">마지막 다운로드 UTC 일시.</param>
        /// <param name="lastUploadedAtUtc">마지막 업로드 UTC 일시.</param>
        void SetConfigStatus(
            string configVersion,
            ViewerConfigSyncStatus syncStatus,
            DateTime? lastDownloadedAtUtc,
            DateTime? lastUploadedAtUtc);

        /// <summary>
        /// 사용자에게 메시지를 표시한다.
        /// </summary>
        /// <param name="message">표시할 메시지.</param>
        void ShowMessage(string message);



        /// <summary>
        /// 사용자에게 예/아니오 확인 메시지를 표시한다.
        /// </summary>
        /// <param name="message">확인할 메시지.</param>
        /// <returns>사용자가 예를 선택하면 true.</returns>
        bool Confirm(string message);

        /// <summary>
        /// 설정 화면을 표시한다.
        /// </summary>
        void ShowView();

        /// <summary>
        /// 설정 화면을 닫는다.
        /// </summary>
        void CloseView();
    }
}