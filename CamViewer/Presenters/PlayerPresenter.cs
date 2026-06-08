using CamViewer.Interfaces;
using CamViewer.Models;
using CamViewer.Services;
using CamViewerClient.Enums;
using CamViewerClient.Models.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private bool _isPlaybackTimerTickRunning;

        /// <summary>
        /// 재생 관련 명령이 실행 중인지 여부.
        /// 중복 클릭으로 인한 Provider 상태 꼬임을 방지한다.
        /// </summary>
        private bool _isPlaybackCommandRunning;

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
            _view.RewindEvent += OnRewind;
            _view.SeekBackward10Event += OnSeekBackward10;
            _view.PlayPauseEvent += OnPlayPause;
            _view.SeekForward10Event += OnSeekForward10;
            _view.StopEvent += OnStop;

            _view.SettingsEvent += OnSettings;
            _view.MinimizeEvent += OnMinimize;
            _view.CloseEvent += OnClose;

            _view.PlaybackTimerTickEvent += OnPlaybackTimerTick;
            _view.PlaybackSpeedChangedEvent += OnPlaybackSpeedChanged;

            view.SyncEvent += OnSync;
            _view.TimelineSeekRequestedEvent += OnTimelineSeekRequested;
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
            //List<int> counterNumbers = GetCounterNumbers();

            //_view.SetCounterNumbers(counterNumbers);
            //_view.SetSearchDateTime(DateTime.Now);
            ////_view.SetPlaybackDateTime(null);
            //_view.SetPlaybackState(_playbackState);

            //if (counterNumbers.Count == 0)
            //{
            //    _view.SetLeftVideoTitle("좌측 영상 설정 없음");
            //    _view.SetRightVideoTitle("우측 영상 설정 없음");
            //    _view.SetStatus("등록된 계산대 설정이 없습니다.");
            //    return;
            //}

            //_view.SelectCounterNo(counterNumbers[0]);
            //UpdateSelectedCounterInfo();

            //_view.SetStatus("캠뷰어 실행 준비 완료");

            DateTime baseTime = DateTime.Now;

            _view.SetSearchRange(
                baseTime.AddSeconds(-30),
                baseTime.AddSeconds(3));

            _view.SetPlaybackTime(null);
            _view.SetPlaybackState(_playbackState);

            _view.SetPlaybackSpeedOptions();
            _view.SelectPlaybackSpeed(PlaybackSpeed.Normal);

            ReloadViewByConfig();

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
        /// 재생속도 선택 변경을 처리한다.
        /// </summary>
        private async void OnPlaybackSpeedChanged(object sender, EventArgs e)
        {
            if (_isPlaybackCommandRunning)
            {
                _view.SetStatus("재생 명령 처리 중에는 재생속도를 변경할 수 없습니다.");
                return;
            }

            PlayerPlaybackResult result =
                await _playbackService.SetPlaybackSpeedAsync(
                    _view.SelectedPlaybackSpeed,
                    CancellationToken.None);

            if (!result.Success)
            {
                _view.ShowMessage(result.Message);

                _view.SelectPlaybackSpeed(
                    _playbackService.CurrentPlaybackSpeed);

                return;
            }

            _view.SetPlaybackTime(
                _playbackService.CurrentPlaybackTime);

            _view.SetPlaybackState(
                _playbackService.CurrentState);

            if (_playbackService.CurrentState == PlaybackState.Paused
                || _playbackService.CurrentState == PlaybackState.Stopped)
            {
                _view.StopPlaybackTimer();
            }
            else
            {
                _view.StartPlaybackTimer();
            }

            _view.SetStatus(result.Message);
        }

        /// <summary>
        /// 검색 버튼 클릭 시 현재 조회 조건으로 재생을 시작한다.
        /// </summary>
        private async void OnSearch(object sender, EventArgs e)
        {
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            try
            {
                PlayerPlaybackResult result =
                    await PlayFromCurrentSearchRangeAsync();

                HandlePlaybackCommandResult(result);

                if (!result.Success)
                {
                    _view.ShowMessage(result.Message);
                }
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        /// <summary>
        /// 역재생 버튼 요청을 처리한다.
        /// </summary>
        private async void OnRewind(object sender, EventArgs e)
        {
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            try
            {
                PlayerPlaybackResult result =
                    await _playbackService.RewindAsync(CancellationToken.None);

                HandlePlaybackCommandResult(result);

                if (!result.Success)
                {
                    return;
                }

                _view.SetPlaybackTime(
                    _playbackService.CurrentPlaybackTime);

                _view.StartPlaybackTimer();
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        /// <summary>
        /// 10초 전 이동 요청을 처리한다.
        /// </summary>
        private async void OnSeekBackward10(object sender, EventArgs e)
        {
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            try
            {
                int seconds = _view.TimeAdjustSeconds;

                PlayerPlaybackResult result =
                    await _playbackService.SeekSecondsAsync(
                        -seconds,
                        CancellationToken.None);

                HandlePlaybackCommandResult(result);

                if (!result.Success)
                {
                    return;
                }


                DateTime? playbackTime =
                    await _playbackService.SyncPlaybackTimeAsync(
                        CancellationToken.None);

                _view.SetPlaybackTime(playbackTime);
                _view.SetTimelinePlaybackTime(playbackTime);
                _view.StartPlaybackTimer();
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        /// <summary>
        /// 재생/일시정지/재개 요청을 처리한다.
        /// 정지 상태에서는 현재 조회 조건으로 처음부터 재생한다.
        /// </summary>
        private async void OnPlayPause(object sender, EventArgs e)
        {
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            try
            {
                PlayerPlaybackResult result;

                if (_playbackService.CurrentState == PlaybackState.Playing
                    || _playbackService.CurrentState == PlaybackState.Rewinding)
                {
                    result = await _playbackService.PauseAsync(
                        CancellationToken.None);
                }
                else if (_playbackService.CurrentState == PlaybackState.Paused)
                {
                    result = await _playbackService.ResumeAsync(
                        CancellationToken.None);
                }
                else
                {
                    result = await PlayFromCurrentSearchRangeAsync();
                }

                HandlePlaybackCommandResult(result);

                if (!result.Success)
                {
                    return;
                }

                _view.SetPlaybackTime(
                    _playbackService.CurrentPlaybackTime);

                if (_playbackService.CurrentState == PlaybackState.Paused)
                {
                    _view.StopPlaybackTimer();
                }
                else if (_playbackService.CurrentState == PlaybackState.Playing
                    || _playbackService.CurrentState == PlaybackState.Rewinding)
                {
                    _view.StartPlaybackTimer();
                }
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        private async Task ApplyVideoSourceInfoAsync(
    PlayerPlaybackRequest request)
        {
            if (request == null || request.Channels == null)
            {
                return;
            }

            foreach (PlayerChannelTarget channel in request.Channels)
            {
                PlayerVideoSourceInfoResult infoResult =
                    await _playbackService.GetVideoSourceInfoAsync(
                        channel,
                        CancellationToken.None);

                if (!infoResult.Success)
                {
                    continue;
                }

                _view.SetVideoSourceSize(
                    infoResult.ScreenPosition,
                    infoResult.Width,
                    infoResult.Height);
            }

            _view.UpdateVideoLayout();
        }


        /// <summary>
        /// 현재 PlayerView의 조회 조건을 기준으로 재생을 시작한다.
        /// </summary>
        private async Task<PlayerPlaybackResult> PlayFromCurrentSearchRangeAsync()
        {
            PlayerPlaybackRequest request;

            try
            {
                request = BuildPlaybackRequest();
            }
            catch (Exception ex)
            {
                return PlayerPlaybackResult.Fail(
                    ex.Message,
                    "PLAYBACK_REQUEST_BUILD_FAILED");
            }

            _view.StopPlaybackTimer();

            await ApplyVideoSourceInfoAsync(request);

            PlayerPlaybackResult playResult =
                await _playbackService.PlayAsync(
                    request,
                    CancellationToken.None);

            if (!playResult.Success)
            {
                _view.SetPlaybackState(
                    _playbackService.CurrentState);

                _view.SetPlaybackTime(
                    _playbackService.CurrentPlaybackTime);
                _view.SetTimelinePlaybackTime(
                    _playbackService.CurrentPlaybackTime);


                return playResult;
            }
            DateTime? playbackTime =
                _playbackService.CurrentPlaybackTime;

            _view.SetPlaybackTime(playbackTime);

            _view.SetTimelineRange(
                request.PlayStartTime,
                request.PlayEndTime);

            _view.SetTimelinePlaybackTime(playbackTime);

            _view.SetPlaybackState(
                _playbackService.CurrentState);

            _view.StartPlaybackTimer();

            return playResult;
        }
        /// <summary>
        /// 10초 뒤 이동 요청을 처리한다.
        /// </summary>
        private async void OnSeekForward10(object sender, EventArgs e)
        {
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            try
            {
                int seconds =
                    _view.TimeAdjustSeconds;

                PlayerPlaybackResult result =
                    await _playbackService.SeekSecondsAsync(
                        seconds,
                        CancellationToken.None);

                HandlePlaybackCommandResult(result);

                if (!result.Success)
                {
                    _view.ShowMessage(result.Message);
                    return;
                }

                DateTime? playbackTime =
                    await _playbackService.SyncPlaybackTimeAsync(
                        CancellationToken.None);

                _view.SetPlaybackTime(playbackTime);
                _view.SetTimelinePlaybackTime(playbackTime);
                _view.StartPlaybackTimer();
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        /// <summary>
        /// 정지 버튼 요청을 처리한다.
        /// </summary>
        private async void OnStop(object sender, EventArgs e)
        {
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            try
            {
                _view.StopPlaybackTimer();

                PlayerPlaybackResult result =
                    await _playbackService.StopAsync(
                        CancellationToken.None);

                HandlePlaybackCommandResult(result);

                _view.SetPlaybackTime(null);
                _view.SetTimelinePlaybackTime(null);

                _view.SetPlaybackState(
                    _playbackService.CurrentState);
            }
            finally
            {
                EndPlaybackCommand();
            }            
        }

        /// <summary>
        /// PlayerView 종료 요청을 처리한다.
        /// NVR 재생과 privier 리소스를 정리한 뒤 화면을 닫는다.
        /// </summary>
        private async void OnClose(object sender, EventArgs e)
        {
            if (_isPlaybackCommandRunning)
            {
                               _view.ShowMessage(
                    "재생 명령을 처리 중입니다."
                    + Environment.NewLine
                    + "처리가 완료된 후 whdfygo 주세요.");
                return;
            }

            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            try
            {
                _view.StopPlaybackTimer();

                await _playbackService.StopAsync(CancellationToken.None);

                _playbackService.Dispose();

                _view.CloseView();
            }
            finally
            {
                EndPlaybackCommand();
            }
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

                _view.SetVideoSourceSize(ScreenPosition.Left, 0, 0);
                _view.SetVideoSourceSize(ScreenPosition.Right, 0, 0);

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

            // 현재 CounterMap에 원본 해상도 정보가 없다면 0, 0으로 전달한다.
            // 이 경우 원본 비율 모드는 안전하게 전체 채우기로 동작한다.
            //_view.SetVideoSourceSize(
            //    ScreenPosition.Left,
            //    leftMap == null ? 0 : leftMap.VideoWidth,
            //    leftMap == null ? 0 : leftMap.VideoHeight);

            //_view.SetVideoSourceSize(
            //    ScreenPosition.Right,
            //    rightMap == null ? 0 : rightMap.VideoWidth,
            //    rightMap == null ? 0 : rightMap.VideoHeight);

            _view.SetVideoSourceSize(
                ScreenPosition.Left,
                1024,
                768);

            _view.SetVideoSourceSize(
                ScreenPosition.Right,
                1024,
                768);
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

            //_view.SetPlaybackDateTime(_currentPlaybackDateTime);
        }



        /// <summary>
        /// PlayerView에서 설정 버튼 클릭 시 설정 화면을 연다.
        /// 재생 중이면 먼저 재생을 정지한 뒤 설정 화면을 실행한다.
        /// 설정 저장 후에는 로컬 설정을 다시 불러와 화면을 갱신한다.
        /// </summary>
        private async void OnSettings(object sender, EventArgs e)
        {
            if (_isPlaybackCommandRunning)
            {
                _view.ShowMessage(
                    "재생 명령을 처리 중입니다."
                    + Environment.NewLine
                    + "처리가 완료된 후 설정을 열어 주세요.");
                return;
            }

            if (_openSettingsFunc == null)
            {
                _view.ShowMessage("설정 화면 실행 구성이 없습니다.");
                return;
            }

            if (_playbackService.CurrentState != PlaybackState.Stopped)
            {
                bool confirmStop =
                    _view.Confirm(
                        "현재 영상이 재생 중입니다."
                        + Environment.NewLine
                        + "설정을 변경하려면 재생을 중지해야 합니다."
                        + Environment.NewLine
                        + "재생을 중지하고 설정 화면을 여시겠습니까?");

                if (!confirmStop)
                {
                    return;
                }

                _view.StopPlaybackTimer();

                PlayerPlaybackResult stopResult =
                    await _playbackService.StopAsync(
                        CancellationToken.None);

                if (!stopResult.Success)
                {
                    _view.ShowMessage(stopResult.Message);
                    return;
                }

                _view.SetPlaybackTime(null);
                _view.SetPlaybackState(_playbackService.CurrentState);
                _view.SetStatus("재생을 중지했습니다.");
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

            ViewerConfig reloadedConfig =
                _reloadConfigFunc();

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
        /// 현재 ViewerConfig 기준으로 PlayerView 화면을 갱신한다.
        /// </summary>
        private void ReloadViewByConfig()
        {
            if (_viewerConfig == null)
            {
                _view.SetStatus("설정 정보가 없습니다.");
                return;
            }

            _view.SelectVideoRenderMode(
                _viewerConfig.VideoRenderMode);

            List<int> counterNumbers =
                GetCounterNumbers();

            _view.SetCounterNumbers(counterNumbers);

            if (counterNumbers.Count == 0)
            {
                _view.SetLeftVideoTitle("좌측 영상 설정 없음");
                _view.SetRightVideoTitle("우측 영상 설정 없음");

                _view.SetVideoSourceSize(
                    ScreenPosition.Left,
                    0,
                    0);

                _view.SetVideoSourceSize(
                    ScreenPosition.Right,
                    0,
                    0);

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
            _view.UpdateVideoLayout();

            if (!_view.SelectedCounterNo.HasValue)
            {
                throw new InvalidOperationException(
                    "계산대번호를 선택해 주세요.");
            }

            int counterNo = _view.SelectedCounterNo.Value;

            DateTime startTime = _view.SearchStartTime;
            DateTime endTime = _view.SearchEndTime;

            if (startTime >= endTime)
            {
                throw new InvalidOperationException(
                    "조회 시작시간은 조회 종료시간보다 이전이어야 합니다.");
            }

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

                // 기존 모델 호환용 기준시간이다.
                // 직접 구간 조회에서는 조회 시작시간을 기준값으로 사용한다.
                SearchDateTime = startTime,

                PlayStartTime = startTime,
                PlayEndTime = endTime
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
                    NvrConfig = nvrConfig,
                    TimeOffsetSeconds = 0
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
            lines.Add("조회 시작시간: " + request.PlayStartTime.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.Add("조회 종료시간: " + request.PlayEndTime.ToString("yyyy-MM-dd HH:mm:ss"));
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

        /// <summary>
        /// 영상재생시간 갱신 Tick을 받아 현재 재생 위치를 화면에 반영한다.
        /// Provider가 실제 재생시간을 제공하면 실제 시간을 사용하고,
        /// 제공하지 못하면 추정 시간을 사용한다.
        /// </summary>
        /// <summary>
        /// 영상재생시간 갱신 Tick을 받아 현재 재생 위치와 좌/우 동기화 상태를 화면에 반영한다.
        /// </summary>
        private async void OnPlaybackTimerTick(object sender, EventArgs e)
        {
            if (_isPlaybackTimerTickRunning)
            {
                return;
            }

            _isPlaybackTimerTickRunning = true;

            try
            {
                DateTime? playbackTime =
                    await _playbackService.SyncPlaybackTimeAsync(
                        CancellationToken.None);

                _view.SetPlaybackTime(playbackTime);
                _view.SetTimelinePlaybackTime(playbackTime);

                PlaybackSyncStatus syncStatus =
                    await _playbackService.GetPlaybackSyncStatusAsync(
                        CancellationToken.None);

                if (syncStatus != null)
                {
                    _view.SetPlaybackSyncStatus(
                        syncStatus.ToDisplayText());
                }
            }
            finally
            {
                _isPlaybackTimerTickRunning = false;
            }
        }

        /// <summary>
        /// 영상 동기화 버튼 요청을 처리한다.
        /// </summary>
        private async void OnSync(object sender, EventArgs e)
        {
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            try
            {
                PlayerPlaybackResult result =
                    await _playbackService.ResyncPlaybackSessionsAsync(
                        CancellationToken.None);

                HandlePlaybackCommandResult(result);

                DateTime? playbackTime =
                    await _playbackService.SyncPlaybackTimeAsync(
                        CancellationToken.None);

                _view.SetPlaybackTime(playbackTime);
                _view.SetTimelinePlaybackTime(playbackTime);

                PlaybackSyncStatus syncStatus =
                    await _playbackService.GetPlaybackSyncStatusAsync(
                        CancellationToken.None);

                if (syncStatus != null)
                {
                    _view.SetPlaybackSyncStatus(
                        syncStatus.ToDisplayText());
                }
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        /// <summary>
        /// 타임라인 클릭 이동 요청을 처리한다.
        /// </summary>
        private async void OnTimelineSeekRequested(
            object sender,
            TimelineSeekRequestedEventArgs e)
        {
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            try
            {
                PlayerPlaybackResult result =
                    await _playbackService.SeekToTimeAsync(
                        e.TargetTime,
                        CancellationToken.None);

                HandlePlaybackCommandResult(result);

                if (!result.Success)
                {
                    _view.ShowMessage(result.Message);
                    return;
                }

                DateTime? playbackTime =
                    await _playbackService.SyncPlaybackTimeAsync(
                        CancellationToken.None);

                _view.SetPlaybackTime(playbackTime);
                _view.SetTimelinePlaybackTime(playbackTime);
                _view.StartPlaybackTimer();
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        /// <summary>
        /// 영상재생시간 갱신 Tick을 받아 현재 재생 위치와 좌/우 동기화 상태를 화면에 반영한다.
        /// </summary>
        //private async void OnPlaybackTimerTick(object sender, EventArgs e)
        //{
        //    if (_isPlaybackTimerTickRunning)
        //    {
        //        return;
        //    }

        //    _isPlaybackTimerTickRunning = true;

        //    try
        //    {
        //        DateTime? playbackTime =
        //            await _playbackService.SyncPlaybackTimeAsync(
        //                CancellationToken.None);

        //        _view.SetPlaybackTime(playbackTime);

        //        PlaybackSyncStatus syncStatus =
        //            await _playbackService.GetPlaybackSyncStatusAsync(
        //                CancellationToken.None);

        //        if (syncStatus != null)
        //        {
        //            _view.SetPlaybackSyncStatus(
        //                syncStatus.ToDisplayText());
        //        }
        //    }
        //    finally
        //    {
        //        _isPlaybackTimerTickRunning = false;
        //    }
        //}

        /// <summary>
        /// 재생 명령을 실행할 수 있는지 확인한다.
        /// 이미 다른 재생 명령이 처리 중이면 false를 반환한다.
        /// </summary>
        private bool TryBeginPlaybackCommand()
        {
            if (_isPlaybackCommandRunning)
            {
                _view.SetStatus("이전 재생 명령을 처리 중입니다.");
                return false;
            }

            _isPlaybackCommandRunning = true;
            return true;
        }

        /// <summary>
        /// 재생 명령 실행 상태를 해제한다.
        /// </summary>
        private void EndPlaybackCommand()
        {
            _isPlaybackCommandRunning = false;
        }
    }
}