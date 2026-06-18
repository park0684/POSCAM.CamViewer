using CamViewer.Models;
using CamViewerClient.Enums;
using CamViewerClient.Models.Config;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Interfaces
{
    /// <summary>
    /// 영상 재생 화면에서 제공해야 하는 기능을 정의한다.
    /// </summary>
    public interface IPlayerView
    {
        /// <summary>
        /// 선택된 계산대번호.
        /// </summary>
        int? SelectedCounterNo { get; }

        /// <summary>
        /// 사용자가 선택한 영상검색일시.
        /// </summary>
        DateTime SearchDateTime { get; }

        /// <summary>
        /// 영상검색일시 기준 이전 검색 시간. 단위는 초.
        /// </summary>
        int SearchAdjustSeconds { get; }

        /// <summary>
        /// 영상 재생 시간. 단위는 초.
        /// </summary>
        int PlayAdjustSeconds { get; }

        /// <summary>
        /// 조회 시작시간.
        /// </summary>
        DateTime SearchStartTime { get; }

        /// <summary>
        /// 조회 종료시간.
        /// </summary>
        DateTime SearchEndTime { get; }

        /// <summary>
        /// 좌측 영상 출력 패널 Handle.
        /// NVR SDK 재생 시 사용한다.
        /// </summary>
        IntPtr LeftVideoHandle { get; }

        /// <summary>
        /// 우측 영상 출력 패널 Handle.
        /// NVR SDK 재생 시 사용한다.
        /// </summary>
        IntPtr RightVideoHandle { get; }

        /// <summary>
        /// 사용자 확인 메시지를 표시한다.
        /// </summary>
        bool Confirm(string message);

        /// <summary>
        /// 선택된 재생속도.
        /// </summary>
        PlaybackSpeed SelectedPlaybackSpeed { get; }

        /// <summary>
        /// 10초 전/뒤 버튼에서 사용할 이동 간격 초.
        /// </summary>
        int TimeAdjustSeconds { get; }

        /// <summary>
        /// 현재 선택된 영상 표시 방식.
        /// </summary>
        VideoRenderMode SelectedVideoRenderMode { get; }

        /// <summary>
        /// PlayerView가 최초 표시될 때 발생한다.
        /// </summary>
        event EventHandler LoadViewEvent;

        /// <summary>
        /// 계산대번호 선택값이 변경될 때 발생한다.
        /// </summary>
        event EventHandler CounterChangedEvent;

        /// <summary>
        /// 영상 검색 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler SearchEvent;


        /// <summary>
        /// 10초 전 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler SeekBackward10Event;

        /// <summary>
        /// 재생/일시정지 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler PlayPauseEvent;

        /// <summary>
        /// 10초 뒤 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler SeekForward10Event;

        /// <summary>
        /// 역재생 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler RewindEvent;

        /// <summary>
        /// 정지 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler StopEvent;

        /// <summary>
        /// 설정 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler SettingsEvent;

        /// <summary>
        /// 최소화 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler MinimizeEvent;

        /// <summary>
        /// 종료 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler CloseEvent;

        /// <summary>
        /// 재생속도 선택값이 변경될 때 발생한다.
        /// </summary>
        event EventHandler PlaybackSpeedChangedEvent;

        /// <summary>
        /// 재생 시간 갱신 타이머가 Tick될 때 발생한다.
        /// </summary>
        event EventHandler PlaybackTimerTickEvent;

        /// <summary>
        /// 영상 동기화 버튼 클릭 시 발생한다.
        /// </summary>
        event EventHandler SyncEvent;

        /// <summary>
        /// 타임라인에서 특정 위치 이동 요청 시 발생한다.
        /// </summary>
        event EventHandler<TimelineSeekRequestedEventArgs> TimelineSeekRequestedEvent;

        /// <summary>
        /// 계산대번호 목록을 설정한다.
        /// </summary>
        void SetCounterNumbers(IEnumerable<int> counterNumbers);

        /// <summary>
        /// 계산대번호를 선택한다.
        /// </summary>
        void SelectCounterNo(int counterNo);

        /// <summary>
        /// 영상검색일시를 설정한다.
        /// </summary>
        void SetSearchDateTime(DateTime searchDateTime);

        /// <summary>
        /// 좌측 영상 제목 또는 안내 문구를 설정한다.
        /// </summary>
        void SetLeftVideoTitle(string title);

        /// <summary>
        /// 우측 영상 제목 또는 안내 문구를 설정한다.
        /// </summary>
        void SetRightVideoTitle(string title);

        /// <summary>
        /// 현재 영상재생시간을 표시한다.
        /// 
        /// SetPalybackTime으로 대체
        /// </summary>
        //void SetPlaybackDateTime(DateTime? playBackTime);

        /// <summary>
        /// 재생 상태에 맞게 버튼 표시를 변경한다.
        /// </summary>
        void SetPlaybackState(PlaybackState state);

        /// <summary>
        /// 상태 메시지를 표시한다.
        /// </summary>
        void SetStatus(string message);

        /// <summary>
        /// 사용자 안내 메시지를 표시한다.
        /// </summary>
        void ShowMessage(string message);

        /// <summary>
        /// PlayerView를 표시한다.
        /// </summary>
        void ShowView();

        /// <summary>
        /// PlayerView를 닫는다.
        /// </summary>
        void CloseView();

        /// <summary>
        /// PlayerView를 최소화한다.
        /// </summary>
        void MinimizeView();
        /// <summary>
        /// 현재 영상재생시간을 표시한다.
        /// </summary>
        void SetPlaybackTime(DateTime? playbackTime);


        /// <summary>
        /// 재생 시간 갱신 타이머를 시작한다.
        /// </summary>
        void StartPlaybackTimer();

        /// <summary>
        /// 재생 시간 갱신 타이머를 중지한다.
        /// </summary>
        void StopPlaybackTimer();

        /// <summary>
        /// 조회 시작시간과 종료시간을 설정한다.
        /// </summary>
        void SetSearchRange(
            DateTime startTime,
            DateTime endTime);

        /// <summary>
        /// 재생속도 목록을 초기화한다.
        /// </summary>
        void SetPlaybackSpeedOptions();

        /// <summary>
        /// 재생속도를 선택한다.
        /// </summary>
        void SelectPlaybackSpeed(PlaybackSpeed speed);

        /// <summary>
        /// 좌/우 영상 동기화 상태를 표시한다.
        /// </summary>
        void SetPlaybackSyncStatus(string statusText);

        /// <summary>
        /// 타임라인의 전체 조회 구간을 설정한다.
        /// </summary>
        void SetTimelineRange(DateTime? startTime, DateTime? endTime);

        /// <summary>
        /// 타임라인의 현재 재생 위치를 설정한다.
        /// </summary>
        void SetTimelinePlaybackTime(DateTime? playbackTime);

        /// <summary>
        /// 좌측/우측 영상의 원본 크기를 설정한다.
        /// 원본 비율 표시 모드에서 사용한다.
        /// </summary>
        void SetVideoSourceSize(
            ScreenPosition screenPosition,
            int width,
            int height);

        /// <summary>
        /// 영상 렌더링 대상 패널의 크기와 위치를 현재 View 크기에 맞게 갱신한다.
        /// </summary>
        void UpdateVideoLayout();

        /// <summary>
        /// 영상 표시 방식을 선택한다.
        /// </summary>
        void SelectVideoRenderMode(VideoRenderMode renderMode);

        /// <summary>
        /// 이전/다음 상대시간 이동 버튼의 사용 가능 여부를 설정한다.
        /// </summary>
        void SetSeekButtonsEnabled(
            bool enabled);

        /// <summary>
        /// 타임라인 위치 선택 입력의 사용 가능 여부를 설정한다.
        /// 현재 위치 표시는 비활성 상태에서도 유지한다.
        /// </summary>
        void SetTimelineSeekEnabled(
            bool enabled);

        /// <summary>
        /// 수동 영상 동기화 버튼의 사용 가능 여부를 설정한다.
        /// </summary>
        void SetPlaybackSyncEnabled(
            bool enabled);
    }
}