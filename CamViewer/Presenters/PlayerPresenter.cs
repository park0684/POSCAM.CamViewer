using CamViewer.Interfaces;
using CamViewer.Models;
using CamViewer.Services;
using CamViewerClient.Enums;
using CamViewerClient.Models.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

namespace CamViewer.Presenters
{
    /// <summary>
    /// PlayerView의 계산대 선택, 영상검색일시, 재생 제어 버튼 흐름을 처리한다.
    ///
    /// 현재 단계에서는 실제 NVR 재생을 수행하지 않고,
    /// 로컬 설정 기준으로 좌/우 영상 채널 매핑을 확인하고 임시 메시지만 표시한다.
    /// </summary>
    public sealed class PlayerPresenter
    {
        private readonly IPlayerView _view;
        private ViewerConfig _viewerConfig;
        private readonly IPlayerPlaybackService _playbackService;
        private readonly Func<bool> _openSettingsFunc;
        private readonly Func<ViewerConfig> _reloadConfigFunc;

        private PlaybackState _playbackState;
        private DateTime? _currentPlaybackDateTime;

        /// <summary>
        /// PlayerPresenter를 초기화한다.
        /// </summary>
        /// <param name="view">영상 재생 View.</param>
        /// <param name="viewerConfig">로컬 또는 서버에서 불러온 캠뷰어 설정.</param>
public PlayerPresenter(
    IPlayerView view,
    ViewerConfig viewerConfig,
    IPlayerPlaybackService playbackService,
    Func<bool> openSettingsFunc,
    Func<ViewerConfig> reloadConfigFunc)
        {
            if (view == null)
            {
                throw new ArgumentNullException("view");
            }

            if (viewerConfig == null)
            {
                throw new ArgumentNullException("viewerConfig");
            }

            if (playbackService == null)
            {
                throw new ArgumentNullException("playbackService");
            }

            _view = view;
            _viewerConfig = viewerConfig;
            _playbackService = playbackService;
            _openSettingsFunc = openSettingsFunc;
            _reloadConfigFunc = reloadConfigFunc;
            _playbackState = PlaybackState.Stopped;

            _view.LoadViewEvent += OnLoadView;
            _view.CounterChangedEvent += OnCounterChanged;
            _view.SearchEvent += OnSearch;
            _view.FastReverseEvent += OnFastReverse;
            _view.SeekBackward10Event += OnSeekBackward10;
            _view.PlayPauseEvent += OnPlayPause;
            _view.SeekForward10Event += OnSeekForward10;
            _view.FastForwardEvent += OnFastForward;

            _view.SettingsEvent += OnSettings;
            _view.MinimizeEvent += OnMinimize;
            _view.CloseEvent += OnClose;
        }

        /// <summary>
        /// PlayerView를 표시한다.
        /// </summary>
        public void Show()
        {
            _view.ShowView();
        }

        /// <summary>
        /// PlayerView가 표시될 때 초기 화면 상태를 구성한다.
        /// </summary>
        private void OnLoadView(object sender, EventArgs e)
        {
            List<int> counterNumbers = GetCounterNumbers();

            _view.SetCounterNumbers(counterNumbers);
            _view.SetSearchDateTime(DateTime.Now);
            _view.SetPlaybackDateTime(null);
            _view.SetPlaybackState(_playbackState);

            if (counterNumbers.Count == 0)
            {
                _view.SetLeftVideoTitle("좌측 영상 설정 없음");
                _view.SetRightVideoTitle("우측 영상 설정 없음");
                _view.SetStatus("등록된 계산대 설정이 없습니다.");
                return;
            }

            _view.SelectCounterNo(counterNumbers[0]);
            UpdateSelectedCounterInfo();

            _view.SetStatus("캠뷰어 실행 준비 완료");
        }

        /// <summary>
        /// 계산대 선택값이 변경되면 좌/우 채널 정보를 갱신한다.
        /// </summary>
        private void OnCounterChanged(object sender, EventArgs e)
        {
            UpdateSelectedCounterInfo();
        }

        /// <summary>
        /// 영상 검색 버튼 클릭 시 재생 요청 정보를 생성한다.
        ///
        /// 현재 단계에서는 실제 NVR 재생을 수행하지 않고,
        /// 생성된 재생 요청 정보를 메시지로 확인한다.
        /// </summary>
        private void OnSearch(object sender, EventArgs e)
        {
            PlayerPlaybackRequest request;

            try
            {
                request = BuildPlaybackRequest();
            }
            catch (Exception ex)
            {
                _view.ShowMessage(ex.Message);
                return;
            }

            PlayerPlaybackResult playResult =
                _playbackService.Play(request);

            if (!playResult.Success)
            {
                _view.ShowMessage(playResult.Message);
                return;
            }

            _currentPlaybackDateTime = request.PlayStartTime;
            _playbackState = _playbackService.CurrentState;

            _view.SetPlaybackDateTime(_currentPlaybackDateTime);
            _view.SetPlaybackState(_playbackState);

            _view.SetStatus(playResult.Message);

            _view.ShowMessage(
                BuildPlaybackRequestDebugMessage(request));
        }

        /// <summary>
        /// 빠른 역재생 버튼 요청을 처리한다.
        /// </summary>
        private void OnFastReverse(object sender, EventArgs e)
        {
            PlayerPlaybackResult result =
                _playbackService.FastReverse();

            HandlePlaybackCommandResult(result);
        }

        /// <summary>
        /// 10초 전 이동 요청을 처리한다.
        /// </summary>
        private void OnSeekBackward10(object sender, EventArgs e)
        {
            PlayerPlaybackResult result =
                _playbackService.SeekSeconds(-10);

            if (result.Success)
            {
                MovePlaybackTime(-10);
            }

            HandlePlaybackCommandResult(result);
        }

        /// <summary>
        /// 재생/일시정지 전환 요청을 처리한다.
        /// </summary>
        private void OnPlayPause(object sender, EventArgs e)
        {
            PlayerPlaybackResult result;

            if (_playbackService.CurrentState == PlaybackState.Playing
                || _playbackService.CurrentState == PlaybackState.FastForward
                || _playbackService.CurrentState == PlaybackState.FastReverse)
            {
                result = _playbackService.Pause();
            }
            else
            {
                result = _playbackService.Resume();
            }

            HandlePlaybackCommandResult(result);
        }

        /// <summary>
        /// 10초 뒤 이동 요청을 처리한다.
        /// </summary>
        private void OnSeekForward10(object sender, EventArgs e)
        {
            PlayerPlaybackResult result =
                _playbackService.SeekSeconds(10);

            if (result.Success)
            {
                MovePlaybackTime(10);
            }

            HandlePlaybackCommandResult(result);
        }

        /// <summary>
        /// 빠른재생 버튼 요청을 처리한다.
        /// </summary>
        private void OnFastForward(object sender, EventArgs e)
        {
            PlayerPlaybackResult result =
                _playbackService.FastForward();

            HandlePlaybackCommandResult(result);
        }

        /// <summary>
        /// 재생 서비스 처리 결과를 화면 상태에 반영한다.
        /// </summary>
        private void HandlePlaybackCommandResult(
            PlayerPlaybackResult result)
        {
            if (result == null)
            {
                return;
            }

            if (!result.Success)
            {
                _view.ShowMessage(result.Message);
                return;
            }

            _playbackState = _playbackService.CurrentState;

            _view.SetPlaybackState(_playbackState);
            _view.SetStatus(result.Message);
        }

        /// <summary>
        /// 설정에 등록된 계산대번호 목록을 반환한다.
        /// </summary>
        private List<int> GetCounterNumbers()
        {
            if (_viewerConfig.CounterMapList == null)
            {
                return new List<int>();
            }

            return _viewerConfig.CounterMapList
                .Where(x => x != null)
                .Select(x => x.CounterNo)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        /// <summary>
        /// 현재 선택된 계산대의 좌/우 채널 정보를 화면에 표시한다.
        /// </summary>
        private void UpdateSelectedCounterInfo()
        {
            if (!_view.SelectedCounterNo.HasValue)
            {
                _view.SetLeftVideoTitle("좌측 영상 설정 없음");
                _view.SetRightVideoTitle("우측 영상 설정 없음");
                return;
            }

            int counterNo = _view.SelectedCounterNo.Value;

            CounterMap leftMap =
                FindCounterMap(counterNo, ScreenPosition.Left);

            CounterMap rightMap =
                FindCounterMap(counterNo, ScreenPosition.Right);

            _view.SetLeftVideoTitle(
                BuildVideoTitle("좌측", counterNo, leftMap));

            _view.SetRightVideoTitle(
                BuildVideoTitle("우측", counterNo, rightMap));
        }

        /// <summary>
        /// 계산대번호와 스크린위치에 해당하는 채널 매핑을 찾는다.
        /// </summary>
        private CounterMap FindCounterMap(
            int counterNo,
            ScreenPosition screenPosition)
        {
            if (_viewerConfig.CounterMapList == null)
            {
                return null;
            }

            return _viewerConfig.CounterMapList
                .FirstOrDefault(x =>
                    x != null
                    && x.CounterNo == counterNo
                    && x.ScreenPosition == screenPosition);
        }

        /// <summary>
        /// 화면에 표시할 좌/우 영상 제목을 생성한다.
        /// </summary>
        private string BuildVideoTitle(
            string title,
            int counterNo,
            CounterMap map)
        {
            if (map == null)
            {
                return title + " 영상 설정 없음";
            }

            return title
                + " 영상 - 계산대 "
                + counterNo
                + " / NVR "
                + map.NvrNo
                + " / 채널 "
                + map.ChannelNo;
        }

        /// <summary>
        /// 현재 재생일시를 지정 초만큼 이동한다.
        /// </summary>
        private void MovePlaybackTime(int seconds)
        {
            if (!_currentPlaybackDateTime.HasValue)
            {
                _currentPlaybackDateTime = _view.SearchDateTime;
            }

            _currentPlaybackDateTime =
                _currentPlaybackDateTime.Value.AddSeconds(seconds);

            _view.SetPlaybackDateTime(_currentPlaybackDateTime);
        }
        
        private void OnClose(object sender, EventArgs e)
        {
            _playbackService.Stop();
            _view.CloseView();
        }

        /// <summary>
        /// PlayerView에서 설정 버튼 클릭 시 설정 화면을 열고,
        /// 저장되었으면 로컬 설정을 다시 불러와 화면을 갱신한다.
        /// </summary>
        private void OnSettings(object sender, EventArgs e)
        {
            if (_openSettingsFunc == null)
            {
                _view.ShowMessage("설정 화면 실행 구성이 없습니다.");
                return;
            }

            bool saved = _openSettingsFunc();

            if (!saved)
            {
                return;
            }

            if (_reloadConfigFunc == null)
            {
                return;
            }

            ViewerConfig reloadedConfig = _reloadConfigFunc();

            if (reloadedConfig == null)
            {
                _view.ShowMessage("변경된 설정을 다시 불러오지 못했습니다.");
                return;
            }

            _viewerConfig = reloadedConfig;

            ReloadViewByConfig();

            _view.SetStatus("설정이 변경되어 화면을 갱신했습니다.");
        }

        /// <summary>
        /// PlayerView 최소화 요청을 처리한다.
        /// </summary>
        private void OnMinimize(object sender, EventArgs e)
        {
            _view.MinimizeView();
        }

        /// <summary>
        /// 현재 ViewerConfig 기준으로 계산대 목록과 좌/우 영상 표시를 다시 구성한다.
        /// </summary>
        private void ReloadViewByConfig()
        {
            List<int> counterNumbers = GetCounterNumbers();

            _view.SetCounterNumbers(counterNumbers);

            if (counterNumbers.Count == 0)
            {
                _view.SetLeftVideoTitle("좌측 영상 설정 없음");
                _view.SetRightVideoTitle("우측 영상 설정 없음");
                _view.SetStatus("등록된 계산대 설정이 없습니다.");
                return;
            }

            _view.SelectCounterNo(counterNumbers[0]);
            UpdateSelectedCounterInfo();
        }
        /// <summary>
        /// 검색 요청 상태를 확인하기 위한 임시 메시지를 생성한다.
        /// </summary>
        private string BuildSearchDebugMessage(
            int counterNo,
            DateTime searchDateTime,
            int beforeSeconds,
            CounterMap leftMap,
            CounterMap rightMap)
        {
            string leftText = leftMap == null
                ? "좌측: 설정 없음"
                : "좌측: NVR "
                  + leftMap.NvrNo
                  + " / 채널 "
                  + leftMap.ChannelNo;

            string rightText = rightMap == null
                ? "우측: 설정 없음"
                : "우측: NVR "
                  + rightMap.NvrNo
                  + " / 채널 "
                  + rightMap.ChannelNo;

            DateTime startTime =
                searchDateTime.AddSeconds(-beforeSeconds);

            return "영상 조회 요청"
                + Environment.NewLine
                + "계산대번호: "
                + counterNo
                + Environment.NewLine
                + "영상검색일시: "
                + searchDateTime.ToString("yyyy-MM-dd tt hh:mm:ss")
                + Environment.NewLine
                + "검색 시작 기준: "
                + startTime.ToString("yyyy-MM-dd tt hh:mm:ss")
                + Environment.NewLine
                + leftText
                + Environment.NewLine
                + rightText;
        }

        /// <summary>
        /// 현재 화면 입력값과 ViewerConfig를 기준으로 재생 요청 정보를 생성한다.
        /// </summary>
        private PlayerPlaybackRequest BuildPlaybackRequest()
        {
            if (!_view.SelectedCounterNo.HasValue)
            {
                throw new InvalidOperationException(
                    "계산대번호를 선택해 주세요.");
            }

            int counterNo = _view.SelectedCounterNo.Value;
            DateTime searchDateTime = _view.SearchDateTime;
            int beforeSeconds = _view.SearchAdjustSeconds;
            int afterCompleteSeconds = GetAfterCompleteSeconds();

            CounterMap leftMap =
                FindCounterMap(counterNo, ScreenPosition.Left);

            CounterMap rightMap =
                FindCounterMap(counterNo, ScreenPosition.Right);

            if (leftMap == null && rightMap == null)
            {
                throw new InvalidOperationException(
                    "선택한 계산대번호에 연결된 좌/우 영상 설정이 없습니다.");
            }

            var request = new PlayerPlaybackRequest
            {
                CounterNo = counterNo,
                SearchDateTime = searchDateTime,
                PlayStartTime = searchDateTime.AddSeconds(-beforeSeconds),
                PlayEndTime = searchDateTime.AddSeconds(afterCompleteSeconds)
            };

            AddChannelTargetIfExists(
                request,
                ScreenPosition.Left,
                leftMap,
                _view.LeftVideoHandle);

            AddChannelTargetIfExists(
                request,
                ScreenPosition.Right,
                rightMap,
                _view.RightVideoHandle);

            if (request.Channels.Count == 0)
            {
                throw new InvalidOperationException(
                    "재생할 채널 정보를 생성하지 못했습니다.");
            }

            return request;
        }

        /// <summary>
        /// CounterMap이 존재하면 PlayerPlaybackRequest에 재생 대상 채널로 추가한다.
        /// </summary>
        private void AddChannelTargetIfExists(
            PlayerPlaybackRequest request,
            ScreenPosition screenPosition,
            CounterMap counterMap,
            IntPtr outputHandle)
        {
            if (request == null || counterMap == null)
            {
                return;
            }

            NvrConfig nvrConfig =
                FindNvrConfig(counterMap.NvrNo);

            if (nvrConfig == null)
            {
                throw new InvalidOperationException(
                    "NVR 설정을 찾을 수 없습니다. NVR 번호: "
                    + counterMap.NvrNo);
            }

            request.Channels.Add(
                new PlayerChannelTarget
                {
                    ScreenPosition = screenPosition,
                    NvrNo = counterMap.NvrNo,
                    ChannelNo = counterMap.ChannelNo,
                    OutputHandle = outputHandle,
                    NvrConfig = nvrConfig
                });
        }

        /// <summary>
        /// NVR 번호에 해당하는 NVR 설정을 찾는다.
        /// </summary>
        private NvrConfig FindNvrConfig(int nvrNo)
        {
            if (_viewerConfig.NvrList == null)
            {
                return null;
            }

            return _viewerConfig.NvrList
                .FirstOrDefault(x =>
                    x != null
                    && x.NvrNo == nvrNo);
        }

        /// <summary>
        /// 거래완료 후 보정 초를 반환한다.
        /// 설정값이 없거나 잘못된 경우 기본값 3초를 사용한다.
        /// </summary>
        private int GetAfterCompleteSeconds()
        {
            if (_viewerConfig.PlaybackOption == null)
            {
                return 3;
            }

            if (_viewerConfig.PlaybackOption.AfterCompleteSeconds < 0)
            {
                return 3;
            }

            if (_viewerConfig.PlaybackOption.AfterCompleteSeconds > 10)
            {
                return 10;
            }

            return _viewerConfig.PlaybackOption.AfterCompleteSeconds;
        }

        /// <summary>
        /// 생성된 재생 요청 정보를 확인하기 위한 임시 메시지를 만든다.
        /// </summary>
        private string BuildPlaybackRequestDebugMessage(
            PlayerPlaybackRequest request)
        {
            var lines = new List<string>();

            lines.Add("영상 재생 요청 정보");
            lines.Add("계산대번호: " + request.CounterNo);
            lines.Add("영상검색일시: " + request.SearchDateTime.ToString("yyyy-MM-dd tt hh:mm:ss"));
            lines.Add("재생 시작: " + request.PlayStartTime.ToString("yyyy-MM-dd tt hh:mm:ss"));
            lines.Add("재생 종료: " + request.PlayEndTime.ToString("yyyy-MM-dd tt hh:mm:ss"));
            lines.Add("");

            foreach (PlayerChannelTarget channel in request.Channels)
            {
                string screenText =
                    channel.ScreenPosition == ScreenPosition.Left
                        ? "좌측"
                        : "우측";

                lines.Add(
                    screenText
                    + " 화면: NVR "
                    + channel.NvrNo
                    + " / 채널 "
                    + channel.ChannelNo
                    + " / Provider "
                    + channel.NvrConfig.ProviderKey
                    + " / "
                    + channel.NvrConfig.Host
                    + ":"
                    + channel.NvrConfig.Port);
            }

            return string.Join(
                Environment.NewLine,
                lines);
        }
    }
}