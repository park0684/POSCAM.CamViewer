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
        /// 화면 버튼 표시를 위한 마지막 재생 방향 상태.
        /// Playing 또는 Rewinding을 보관한다.
        /// </summary>
        private PlaybackState _lastPlaybackDirection;

        /// <summary>
        /// 프로그램 최초 실행 또는 외부 프로그램으로부터 전달된
        /// 영상 조회 요청을 Player가 준비될 때까지 보관하는 저장소.
        /// </summary>
        private readonly IApplicationLaunchRequestStore _launchRequestStore;

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
                Func<ViewerConfig> reloadConfigFunc,
                IApplicationLaunchRequestStore launchRequestStore)
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
            if (launchRequestStore == null)
            {
                throw new ArgumentNullException("launchRequestStore");
            }

            _view = view;
            _viewerConfig = viewerConfig;
            _playbackService = playbackService;
            _openSettingsFunc = openSettingsFunc;
            _reloadConfigFunc = reloadConfigFunc;
            _launchRequestStore = launchRequestStore;

            _playbackState = PlaybackState.Stopped;
            _lastPlaybackDirection = PlaybackState.Playing;

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

            _view.SyncEvent += OnSync;
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
        /// PlayerView가 표시될 때 초기 화면을 구성하고
        /// 최초 실행 요청에 따라 자동으로 영상을 조회한다.
        /// 
        /// 처리 기준:
        /// - 직접 실행이면 Player 준비 시점의 현재 시간을 기준으로 사용한다.
        /// - 외부 실행이면 전달받은 기준 시간을 사용한다.
        /// - 조회 시작은 기준 시간 이전 보정 초를 적용한다.
        /// - 조회 종료는 기준 시간 이후 보정 초를 적용한다.
        /// </summary>
        private async void OnLoadView(
            object sender,
            EventArgs e)
        {
            _view.SetPlaybackTime(null);
            _view.SetPlaybackState(_playbackState);

            _view.SetPlaybackSpeedOptions();
            _view.SelectPlaybackSpeed(PlaybackSpeed.Normal);

            // 설정에 등록된 계산대와 화면 정보를 먼저 반영한다.
            ReloadViewByConfig();

            ApplicationLaunchRequest launchRequest;

            // Program에서 보관한 실행 요청이 있으면 가져온다.
            // 요청을 가져오는 순간 저장소에서는 제거되어
            // 같은 요청이 중복 실행되지 않는다.
            if (!_launchRequestStore.TryTakePendingRequest(out launchRequest))
            {
                // 저장된 요청이 없는 예외 상황에서는
                // 일반 직접 실행 요청으로 처리한다.
                launchRequest = ApplicationLaunchRequest.CreateDirectLaunch();
            }

            await ApplyLaunchRequestAsync(launchRequest, false);
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
            if (!TryBeginPlaybackCommand())
            {
                _view.SelectPlaybackSpeed(_playbackService.CurrentPlaybackSpeed);

                return;
            }

            try
            {
                PlayerPlaybackResult result =await _playbackService.SetPlaybackSpeedAsync(_view.SelectedPlaybackSpeed, CancellationToken.None);

                HandlePlaybackCommandResult(result);

                if (result == null || !result.Success)
                {
                    _view.SelectPlaybackSpeed(_playbackService.CurrentPlaybackSpeed);

                    return;
                }

                _view.SetPlaybackTime(_playbackService.CurrentPlaybackTime);
                _view.SetTimelinePlaybackTime(_playbackService.CurrentPlaybackTime);

                UpdatePlaybackTimerState();
                UpdatePlaybackStateDisplay();
            }
            finally
            {
                /*
                 * 여기에서 현재 속도에 따라
                 * ±시간 이동과 동기화 버튼 상태도 갱신된다.
                 */
                EndPlaybackCommand();
            }
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

                if (result == null || !result.Success)
                {
                    return;
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
                PlayerPlaybackResult result;

                if (_playbackService.CurrentState == PlaybackState.Rewinding)
                {
                    result = await _playbackService.PauseAsync(CancellationToken.None);
                }
                else if (_playbackService.CurrentState == PlaybackState.Paused && _lastPlaybackDirection == PlaybackState.Rewinding)
                {
                    result = await _playbackService.ResumeAsync(CancellationToken.None);
                }
                else
                {
                    result = await _playbackService.RewindAsync(CancellationToken.None);
                }

                HandlePlaybackCommandResult(result);

                if (result == null || !result.Success)
                {
                    return;
                }

                DateTime? playbackTime = await _playbackService.SyncPlaybackTimeAsync(CancellationToken.None);

                _view.SetPlaybackTime(playbackTime);
                _view.SetTimelinePlaybackTime(playbackTime);

                UpdatePlaybackTimerState();
                UpdatePlaybackStateDisplay();
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
            if (_playbackService.CurrentPlaybackSpeed != PlaybackSpeed.Normal)
            {
                _view.SetStatus(
                    "이전/다음 시간 이동은 1배속에서만 사용할 수 있습니다.");

                UpdatePlaybackControlAvailability();

                return;
            }

            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            try
            {
                int seconds = _view.TimeAdjustSeconds;

                PlayerPlaybackResult result = await _playbackService.SeekSecondsAsync(-seconds, CancellationToken.None);

                HandlePlaybackCommandResult(result);

                if (result == null || !result.Success)
                {
                    return;
                }


                DateTime? playbackTime = await _playbackService.SyncPlaybackTimeAsync(CancellationToken.None);

                _view.SetPlaybackTime(playbackTime);
                _view.SetTimelinePlaybackTime(playbackTime);
                UpdatePlaybackTimerState();
                UpdatePlaybackStateDisplay();
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

                if (_playbackService.CurrentState == PlaybackState.Playing)
                {
                    result =
                        await _playbackService.PauseAsync(
                            CancellationToken.None);
                }
                else if (_playbackService.CurrentState == PlaybackState.Paused
                    && _lastPlaybackDirection == PlaybackState.Playing)
                {
                    result =
                        await _playbackService.ResumeAsync(
                            CancellationToken.None);
                }
                else if (_playbackService.CurrentState == PlaybackState.Rewinding)
                {
                    result =
                        await _playbackService.PlayForwardFromCurrentTimeAsync(
                            CancellationToken.None);
                }
                else if (_playbackService.CurrentState == PlaybackState.Paused
                    && _lastPlaybackDirection == PlaybackState.Rewinding)
                {
                    result =
                        await _playbackService.PlayForwardFromCurrentTimeAsync(
                            CancellationToken.None);
                }
                else
                {
                    result =
                        await PlayFromCurrentSearchRangeAsync();
                }

                HandlePlaybackCommandResult(result);

                if (result == null || !result.Success)
                {
                    return;
                }

                DateTime? playbackTime =
                    _playbackService.CurrentPlaybackTime;

                _view.SetPlaybackTime(playbackTime);
                _view.SetTimelinePlaybackTime(playbackTime);

                UpdatePlaybackTimerState();
                UpdatePlaybackStateDisplay();
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        /// <summary>
        /// 재생 대상 채널의 실제 영상 해상도를 조회하여
        /// 조회에 성공한 화면의 원본 크기만 갱신한다.
        ///
        /// 원본 정보 조회에 실패한 화면은 기존 크기를 유지한다.
        /// </summary>
        private async Task ApplyVideoSourceInfoAsync(
            PlayerPlaybackRequest request)
        {
            if (request == null
                || request.Channels == null
                || request.Channels.Count == 0)
            {
                _view.SetStatus(
                    "영상 원본 정보를 조회할 채널이 없습니다.");

                return;
            }

            var resultMessages =
                new List<string>();

            bool layoutChanged =
                false;

            foreach (PlayerChannelTarget channel
                in request.Channels)
            {
                if (channel == null)
                {
                    continue;
                }

                PlayerVideoSourceInfoResult infoResult =
                    await _playbackService.GetVideoSourceInfoAsync(
                        channel,
                        CancellationToken.None);

                string screenName =
                    channel.ScreenPosition == ScreenPosition.Left
                        ? "좌측"
                        : "우측";

                if (infoResult == null)
                {
                    resultMessages.Add(
                        screenName
                        + ": 원본 정보 조회 결과 없음");

                    continue;
                }

                if (!infoResult.Success
                    || infoResult.Width <= 0
                    || infoResult.Height <= 0)
                {
                    /*
                     * 조회 실패 시 0 × 0으로 초기화하지 않는다.
                     * 마지막으로 정상 조회된 원본 비율을 유지한다.
                     */
                    resultMessages.Add(
                        screenName
                        + ": 원본 정보 조회 실패 - "
                        + infoResult.Message);

                    continue;
                }

                _view.SetVideoSourceSize(
                    infoResult.ScreenPosition,
                    infoResult.Width,
                    infoResult.Height);

                layoutChanged =
                    true;

                resultMessages.Add(
                    screenName
                    + ": "
                    + infoResult.Width
                    + " × "
                    + infoResult.Height);
            }

            /*
             * 실제 원본 크기가 하나 이상 갱신된 경우에만
             * 화면 배치를 다시 계산한다.
             */
            if (layoutChanged)
            {
                _view.UpdateVideoLayout();
            }

            if (resultMessages.Count > 0)
            {
                _view.SetStatus(
                    string.Join(
                        " / ",
                        resultMessages));
            }
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
            if (_playbackService.CurrentPlaybackSpeed != PlaybackSpeed.Normal)
            {
                _view.SetStatus(
                    "이전/다음 시간 이동은 1배속에서만 사용할 수 있습니다.");

                UpdatePlaybackControlAvailability();

                return;
            }
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

                if (result == null || !result.Success)
                {
                    return;
                }

                DateTime? playbackTime =
                    await _playbackService.SyncPlaybackTimeAsync(
                        CancellationToken.None);

                _view.SetPlaybackTime(playbackTime);
                _view.SetTimelinePlaybackTime(playbackTime);
                UpdatePlaybackTimerState();
                UpdatePlaybackStateDisplay();
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
                /*
                 * 화면에 오류를 표시하기 전에 공통 실패 정보를 기록한다.
                 *
                 * NVR번호와 채널번호 등 세부 정보는 다음 단계에서
                 * 서비스가 실패 결과를 생성하는 위치에 추가한다.
                 */
                PlaybackLogWriter.WriteResult("Player 재생 명령", result);

                _view.ShowMessage(BuildPlaybackFailureMessage(result));

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
            //_view.UpdateVideoLayout();

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
        /// 영상 조회 기준 시각 이전부터 재생할 시간(초)을 반환한다.
        /// 
        /// 설정값 처리 기준:
        /// - PlaybackOption이 없으면 기본값 30초
        /// - 음수이면 기본값 30초
        /// - 300초를 초과하면 최대 300초
        /// - 그 외에는 설정된 BeforeSeconds 사용
        /// </summary>
        /// <returns>기준 시각 이전 재생 시간(초).</returns>
        private int GetBeforePlaybackSeconds()
        {
            if (_viewerConfig == null
                || _viewerConfig.PlaybackOption == null)
            {
                return 30;
            }

            int beforeSeconds =
                _viewerConfig.PlaybackOption.BeforeSeconds;

            if (beforeSeconds < 0)
            {
                return 30;
            }

            if (beforeSeconds > 300)
            {
                return 300;
            }

            return beforeSeconds;
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

                PlaybackSyncStatus syncStatus = await _playbackService.GetPlaybackSyncStatusAsync(CancellationToken.None);

                if (syncStatus != null)
                {
                    _view.SetPlaybackSyncStatus(syncStatus.ToDisplayText());
                }
            }
            finally
            {
                _isPlaybackTimerTickRunning = false;
            }
        }

        /// <summary>
        /// 영상 동기화 버튼 요청을 처리한다.
        /// 현재 정방향 또는 역방향 상태를 유지하면서
        /// 좌우 채널의 실제 재생시간을 보정한다.
        /// </summary>
        private async void OnSync(object sender, EventArgs e)
        {
            if (_playbackService.CurrentPlaybackSpeed != PlaybackSpeed.Normal)
            {
                _view.SetStatus("영상 동기화는 1배속에서만 사용할 수 있습니다.");

                UpdatePlaybackControlAvailability();

                return;
            }

            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            try
            {
                PlayerPlaybackResult result =
                    await _playbackService.ResyncPlaybackSessionsAsync(
                        CancellationToken.None);

                HandlePlaybackCommandResult(
                    result);

                if (result == null
                    || !result.Success)
                {
                    UpdatePlaybackTimerState();
                    UpdatePlaybackStateDisplay();
                    return;
                }

                DateTime? playbackTime =
                    await _playbackService.SyncPlaybackTimeAsync(
                        CancellationToken.None);

                _view.SetPlaybackTime(
                    playbackTime);

                _view.SetTimelinePlaybackTime(
                    playbackTime);

                PlaybackSyncStatus syncStatus =
                    await _playbackService.GetPlaybackSyncStatusAsync(
                        CancellationToken.None);

                if (syncStatus != null)
                {
                    _view.SetPlaybackSyncStatus(
                        syncStatus.ToDisplayText());
                }

                /*
                 * 역재생 동기화 과정에서 세션이 다시 생성될 수 있으므로
                 * 실제 서비스 상태를 버튼과 타이머에 다시 반영한다.
                 */
                UpdatePlaybackTimerState();
                UpdatePlaybackStateDisplay();
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        /// <summary>
        /// 타임라인 클릭 이동 요청을 처리한다.
        ///
        /// 배속 상태에서도 타임라인 입력은 허용하지만,
        /// 이동 결과는 항상 1배속으로 통일한다.
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
                    await _playbackService.SeekTimelineToTimeAsync(
                        e.TargetTime,
                        CancellationToken.None);

                /*
                 * 타임라인 이동이 정상 실행되면 서비스의 현재 속도는
                 * 1배속으로 변경되어 있다.
                 *
                 * 프로그램 선택이므로 View 내부에서
                 * PlaybackSpeedChangedEvent를 다시 발생시키지 않는다.
                 */
                _view.SelectPlaybackSpeed(
                    _playbackService.CurrentPlaybackSpeed);

                HandlePlaybackCommandResult(
                    result);

                if (result == null
                    || !result.Success)
                {
                    return;
                }

                DateTime? playbackTime =
                    await _playbackService.SyncPlaybackTimeAsync(
                        CancellationToken.None);

                _view.SetPlaybackTime(
                    playbackTime);

                _view.SetTimelinePlaybackTime(
                    playbackTime);

                UpdatePlaybackTimerState();
                UpdatePlaybackStateDisplay();
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        /// <summary>
        /// 직접 실행 또는 외부 프로그램에서 전달된 영상 조회 요청을 적용한다.
        ///
        /// 직접 실행:
        /// 1. Player가 준비된 현재 시각을 기준 시각으로 사용
        /// 2. 설정된 이전/이후 시간을 반영하여 조회 범위 생성
        /// 3. 조회 조건만 화면에 표시
        /// 4. 자동 재생하지 않음
        ///
        /// 외부 실행:
        /// 1. 외부에서 전달된 결제 완료 시각을 기준 시각으로 사용
        /// 2. 설정된 이전/이후 시간을 반영하여 조회 범위 생성
        /// 3. 기존 영상이 재생 중이면 중지
        /// 4. 새로운 조회 범위를 적용하고 즉시 재생
        /// </summary>
        /// <param name="request">적용할 실행 요청.</param>
        /// <param name="showReplacementMessage">
        /// 실행 중 새로운 외부 요청을 받은 경우 안내 메시지를 표시할지 여부.
        /// 최초 실행 시에는 false를 사용한다.
        /// </param>
        public async Task ApplyLaunchRequestAsync(
            ApplicationLaunchRequest request,
            bool showReplacementMessage)
        {
            if (request == null)
            {
                request =
                    ApplicationLaunchRequest.CreateDirectLaunch();
            }

            /*
             * 조회 가능한 계산대 목록을 확인한다.
             *
             * 직접 실행과 외부 실행 모두 조회시간을 화면에 표시하려면
             * 선택할 계산대가 하나 이상 필요하다.
             */
            List<int> counterNumbers =
                GetCounterNumbers();

            if (counterNumbers.Count == 0)
            {
                _view.SetStatus(
                    "등록된 계산대 설정이 없어 영상 조회 조건을 설정할 수 없습니다.");

                return;
            }

            /*
             * 실행 요청에 따라 기준 시각을 결정한다.
             *
             * 외부 실행:
             * 외부 프로그램에서 전달된 결제 완료 시각을 사용한다.
             *
             * 직접 실행:
             * Program 시작 시각이 아니라 Player 화면이 실제 준비된
             * 현재 시각을 사용한다.
             */
            DateTime referenceTime;

            if (request.IsExternalPlaybackRequest)
            {
                /*
                 * 외부 실행 요청에는 반드시 결제 완료 시각이 있어야 한다.
                 *
                 * 외부 요청인데 ReferenceTime이 없는 경우 현재 시각으로
                 * 대체하면 잘못된 영상을 재생할 수 있으므로 오류 처리한다.
                 */
                if (!request.ReferenceTime.HasValue)
                {
                    _view.ShowMessage(
                        "외부 영상 조회 요청에 기준 시각이 없습니다.");

                    return;
                }

                referenceTime =
                    request.ReferenceTime.Value;
            }
            else
            {
                referenceTime =
                    DateTime.Now;
            }

            /*
             * 별도의 계산대번호가 전달되지 않은 경우
             * 설정된 첫 번째 계산대를 기본으로 선택한다.
             */
            int selectedCounterNo =
                counterNumbers[0];

            if (request.CounterNo.HasValue)
            {
                if (!counterNumbers.Contains(
                    request.CounterNo.Value))
                {
                    _view.ShowMessage(
                        "요청된 계산대번호가 설정에 존재하지 않습니다."
                        + Environment.NewLine
                        + "계산대번호: "
                        + request.CounterNo.Value);

                    return;
                }

                selectedCounterNo =
                    request.CounterNo.Value;
            }

            /*
             * 로컬 설정에 저장된 영상검색 조정시간을 가져온다.
             *
             * BeforeSeconds:
             * 결제 완료 시각 이전부터 조회할 시간
             *
             * AfterCompleteSeconds:
             * 결제 완료 시각 이후 추가로 조회할 시간
             */
            int beforeSeconds =
                GetBeforePlaybackSeconds();

            int afterSeconds =
                GetAfterCompleteSeconds();

            /*
             * 실제 영상 조회 구간을 계산한다.
             *
             * 시작 시각 = 기준 시각 - 이전 조회시간
             * 종료 시각 = 기준 시각 + 완료 후 보정시간
             */
            DateTime startTime =
                referenceTime.AddSeconds(
                    -beforeSeconds);

            DateTime endTime =
                referenceTime.AddSeconds(
                    afterSeconds);

            /*
             * 직접 실행은 조회 조건만 화면에 적용한다.
             *
             * 현재 시각을 기준으로 계산대와 조회시간을 표시하지만,
             * 사용자가 재생 버튼을 누르기 전에는 영상을 조회하지 않는다.
             */
            if (!request.IsExternalPlaybackRequest)
            {
                _view.SelectCounterNo(
                    selectedCounterNo);

                UpdateSelectedCounterInfo();

                _view.SetSearchRange(
                    startTime,
                    endTime);

                _view.SetStatus(
                    "현재 시각 기준으로 영상 조회 시간이 설정되었습니다."
                    + " 기준 시각: "
                    + referenceTime.ToString(
                        "yyyy-MM-dd HH:mm:ss"));

                return;
            }

            /*
             * 여기부터는 외부 프로그램에서 전달된 영상 조회 요청에만 해당한다.
             *
             * 실제 재생 명령을 실행할 수 있는 상태인지 확인한다.
             */
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            try
            {
                /*
                 * 프로그램이 이미 실행 중인 상태에서 새로운 외부 요청을
                 * 전달받은 경우에만 교체 재생 메시지를 표시한다.
                 */
                if (showReplacementMessage)
                {
                    _view.ShowMessage(
                        "새로운 요청 영상을 재생하겠습니다.");
                }

                /*
                 * 기존 영상이 재생 중이면 새로운 조회 요청을 적용하기 전에
                 * 기존 재생을 먼저 중지한다.
                 */
                if (_playbackService.CurrentState
                    != PlaybackState.Stopped)
                {
                    _view.StopPlaybackTimer();

                    PlayerPlaybackResult stopResult =
                        await _playbackService.StopAsync(
                            CancellationToken.None);

                    if (!stopResult.Success)
                    {
                        _view.ShowMessage(
                            stopResult.Message);

                        return;
                    }
                }

                /*
                 * 외부 요청에서 결정된 계산대와 조회 구간을 화면에 적용한다.
                 */
                _view.SelectCounterNo(
                    selectedCounterNo);

                UpdateSelectedCounterInfo();

                _view.SetSearchRange(
                    startTime,
                    endTime);

                _view.SetStatus(
                    "외부 요청 영상을 조회합니다."
                    + " 기준 시각: "
                    + referenceTime.ToString(
                        "yyyy-MM-dd HH:mm:ss"));

                /*
                 * 외부 요청은 조회 범위를 적용한 뒤 즉시 영상을 재생한다.
                 */
                PlayerPlaybackResult playResult =
                    await PlayFromCurrentSearchRangeAsync();

                HandlePlaybackCommandResult(
                    playResult);

                if (!playResult.Success)
                {
                    return;
                }

                UpdatePlaybackTimerState();
                UpdatePlaybackStateDisplay();
            }
            catch (Exception ex)
            {
                _view.ShowMessage(
                    "실행 요청 영상을 재생하는 중 오류가 발생했습니다."
                    + Environment.NewLine
                    + ex.Message);
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        private void UpdatePositionControlAvailability()
        {
            PlaybackState state =
                _playbackService.CurrentState;

            PlaybackSpeed speed =
                _playbackService.CurrentPlaybackSpeed;

            bool hasPlayback =
                state != PlaybackState.Stopped;

            bool isNormalSpeed =
                speed == PlaybackSpeed.Normal;

            /*
             * 상대 이동은 현재시간 정확도가 필요하므로
             * 1배속에서만 허용한다.
             */
            _view.SetSeekButtonsEnabled(
                hasPlayback
                && isNormalSpeed
                && !_isPlaybackCommandRunning);

            /*
             * 타임라인은 절대 시각을 지정하므로
             * 배속 상태에서도 허용한다.
             */
            _view.SetTimelineSeekEnabled(
                hasPlayback
                && !_isPlaybackCommandRunning);

            /*
             * 동기화는 1배속에서만 직접 실행한다.
             * 배속 중 클릭을 허용한다면 내부에서 먼저 1배속으로 전환해야 한다.
             */
            _view.SetPlaybackSyncEnabled(
                hasPlayback
                && isNormalSpeed
                && !_isPlaybackCommandRunning);
        }

        /// <summary>
        /// 재생 실패 유형에 따라 사용자가 취할 수 있는 대응 방법을 추가한다.
        /// </summary>
        private static string BuildPlaybackFailureMessage(
            PlayerPlaybackResult result)
        {
            if (result == null)
            {
                return "재생 처리 결과를 확인할 수 없습니다.";
            }

            string message =
                string.IsNullOrWhiteSpace(
                    result.Message)
                    ? "재생 처리에 실패했습니다."
                    : result.Message;

            switch (result.FailureCategory)
            {
                case PlaybackFailureCategory.Retryable:
                    return message
                        + Environment.NewLine
                        + Environment.NewLine
                        + "NVR 또는 네트워크 상태를 확인한 후 다시 재생해 주세요.";

                case PlaybackFailureCategory.Configuration:
                    return message
                        + Environment.NewLine
                        + Environment.NewLine
                        + "NVR 주소, 포트, 계정, Provider 및 채널 설정을 확인해 주세요.";

                case PlaybackFailureCategory.NoRecord:
                    return message
                        + Environment.NewLine
                        + Environment.NewLine
                        + "해당 시간에 녹화 영상이 없습니다. 조회 시간을 변경해 주세요.";

                case PlaybackFailureCategory.NotSupported:
                    return message
                        + Environment.NewLine
                        + Environment.NewLine
                        + "현재 NVR 또는 Provider에서 지원하지 않는 기능입니다.";

                case PlaybackFailureCategory.System:
                    return message
                        + Environment.NewLine
                        + Environment.NewLine
                        + "프로그램 또는 Provider 구성 확인이 필요합니다."
                        + Environment.NewLine
                        + "오류 코드: "
                        + result.ErrorCode;

                case PlaybackFailureCategory.Cancelled:
                default:
                    return message;
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


        private bool TryBeginPlaybackCommand()
        {
            if (_isPlaybackCommandRunning)
            {
                _view.SetStatus(
                    "이전 재생 명령을 처리 중입니다.");

                return false;
            }

            _isPlaybackCommandRunning =
                true;

            UpdatePlaybackControlAvailability();

            return true;
        }

        /// <summary>
        /// 재생 명령 실행 상태를 해제한다.
        /// </summary>
        private void EndPlaybackCommand()
        {
            _isPlaybackCommandRunning =
                false;

            UpdatePlaybackControlAvailability();
        }

        /// <summary>
        /// 서비스의 현재 재생 상태를 Presenter 내부 방향 상태와 View 버튼 표시에 반영한다.
        /// </summary>
        private void UpdatePlaybackStateDisplay()
        {
            PlaybackState state =
                _playbackService.CurrentState;

            if (state == PlaybackState.Playing)
            {
                _lastPlaybackDirection = PlaybackState.Playing;
            }
            else if (state == PlaybackState.Rewinding)
            {
                _lastPlaybackDirection = PlaybackState.Rewinding;
            }

            _view.SetPlaybackState(state);
        }

        /// <summary>
        /// 현재 재생 상태에 따라 영상재생시간 갱신 타이머를 시작하거나 중지한다.
        /// </summary>
        private void UpdatePlaybackTimerState()
        {
            PlaybackState state =
                _playbackService.CurrentState;

            if (state == PlaybackState.Playing
                || state == PlaybackState.Rewinding)
            {
                _view.StartPlaybackTimer();
            }
            else
            {
                _view.StopPlaybackTimer();
            }
        }

        /// <summary>
        /// 현재 재생 상태와 속도에 따라
        /// 상대 이동, 타임라인, 수동 동기화 기능을 제어한다.
        /// </summary>
        private void UpdatePlaybackControlAvailability()
        {
            PlaybackState state =
                _playbackService.CurrentState;

            PlaybackSpeed speed =
                _playbackService.CurrentPlaybackSpeed;

            bool hasPlayback =
                state != PlaybackState.Stopped;

            bool commandAvailable =
                !_isPlaybackCommandRunning;

            bool isNormalSpeed =
                speed == PlaybackSpeed.Normal;

            /*
             * ±시간 이동은 현재 재생시간을 기준으로 계산하므로
             * 1배속에서만 허용한다.
             */
            _view.SetSeekButtonsEnabled(
                hasPlayback
                && commandAvailable
                && isNormalSpeed);

            /*
             * 타임라인은 절대 시각을 지정하므로
             * 모든 배속에서 허용한다.
             */
            _view.SetTimelineSeekEnabled(
                hasPlayback
                && commandAvailable);

            /*
             * 수동 동기화는 내부적으로 Seek가 발생할 수 있으므로
             * 1배속에서만 허용한다.
             */
            _view.SetPlaybackSyncEnabled(
                hasPlayback
                && commandAvailable
                && isNormalSpeed);
        }
    }
}