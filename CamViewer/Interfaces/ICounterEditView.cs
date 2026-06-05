using System;
using System.Collections.Generic;
using CamViewer.Models.ViewModels;
using CamViewerClient.Enums;

namespace CamViewer.Interfaces
{
    /// <summary>
    /// 계산대 등록/수정 팝업에서 제공해야 하는 기능을 정의한다.
    /// </summary>
    public interface ICounterEditView
    {
        /// <summary>
        /// 입력된 계산대번호.
        /// 숫자로 변환할 수 없으면 null이다.
        /// </summary>
        int? CounterNo { get; set; }

        /// <summary>
        /// 선택된 NVR번호.
        /// 선택되지 않았으면 null이다.
        /// </summary>
        int? SelectedNvrNo { get; set; }

        /// <summary>
        /// 입력된 채널번호.
        /// 숫자로 변환할 수 없으면 null이다.
        /// </summary>
        int? ChannelNo { get; set; }

        /// <summary>
        /// 선택된 스크린위치.
        /// </summary>
        ScreenPosition ScreenPosition { get; set; }

        /// <summary>
        /// NVR 선택값이 변경되었을 때 발생한다.
        /// </summary>
        event EventHandler NvrChangedEvent;

        /// <summary>
        /// 저장 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler SaveEvent;

        /// <summary>
        /// 닫기 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler CloseEvent;

        /// <summary>
        /// 계산대 등록/수정 팝업이 최초 표시될 때 발생한다.
        /// </summary>
        event EventHandler LoadViewEvent;

        /// <summary>
        /// 선택할 수 있는 NVR 목록을 설정한다.
        /// </summary>
        /// <param name="items">NVR 선택 목록.</param>
        void SetNvrOptions(IEnumerable<NvrOptionItem> items);

        /// <summary>
        /// 선택된 NVR의 채널번호 입력 가능 범위를 표시한다.
        /// </summary>
        /// <param name="minimum">최소 채널번호.</param>
        /// <param name="maximum">최대 채널번호.</param>
        void SetChannelRange(int minimum, int maximum);

        /// <summary>
        /// 사용자에게 메시지를 표시한다.
        /// </summary>
        void ShowMessage(string message);

        /// <summary>
        /// 계산대 등록/수정 팝업을 표시한다.
        /// </summary>
        void ShowView();

        /// <summary>
        /// 계산대 등록/수정 팝업을 닫는다.
        /// </summary>
        void CloseView();
    }
}