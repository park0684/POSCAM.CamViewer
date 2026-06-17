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
        /// 현재 재생 명령에서 사용하는 취소 토큰 원본.
        ///
        /// 외부 영상 요청, 설정 진입 또는 프로그램 종료 시
        /// 현재 실행 중인 NVR 작업을 취소하기 위해 사용한다.
        /// </summary>
        private CancellationTokenSource _playbackCommandCancellationTokenSource;

        /// <summary>
        /// Pipe 수신 스레드와 UI 스레드에서
        /// 취소 토큰 원본에 동시에 접근하는 것을 보호한다.
        /// </summary>
        private readonly object _playbackCommandCancellationSyncRoot = new object();
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
        /// PlayerView가 실행되는 WinForms UI 스레드의 컨텍스트.
        ///
        /// Named Pipe 요청은 백그라운드 스레드에서 수신되므로
        /// View를 변경하기 전에 이 컨텍스트를 통해 UI 스레드로 전환한다.
        /// </summary>
        private SynchronizationContext _uiSynchronizationContext;

        /// <summary>
        /// PlayerView의 Load 처리가 완료되어
        /// 외부 실행 요청을 즉시 적용할 수 있는지 여부.
        /// </summary>
        private bool _isViewLoaded;

        /// <summary>
        /// 저장소에 들어온 외부 실행 요청을 처리 중인지 여부.
        ///
        /// 여러 Pipe 요청이 짧은 시간 안에 연속으로 들어와도
        /// 중복 처리 루프가 실행되지 않도록 보호한다.
        /// </summary>
        private bool _isPendingLaunchRequestProcessing;

        /// <summary>
        /// 설정 화면이 열려 있는지 여부.
        ///
        /// 설정 창이 열린 동안 외부 요청은 저장소에 보관하고,
        /// 설정 창이 닫힌 뒤 최신 요청을 처리한다.
        /// </summary>
        private volatile bool _isSettingsOpen;

        /// <summary>
        /// PlayerView와 재생 서비스를 종료하는 중인지 여부.
        ///
        /// 종료가 시작된 뒤에는 새로운 외부 요청을 처리하지 않는다.
        /// </summary>
        private volatile bool _isClosing;

        /// <summary>
        /// 재생 상태가 변경되는 명령의 세대 번호.
        ///
        /// 타이머 동기화 작업을 시작한 뒤 새로운 재생 명령이 실행되면
        /// 이전 타이머 결과가 현재 화면을 덮어쓰지 않도록 비교한다.
        /// </summary>
        private long _playbackCommandVersion;

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
                throw new ArgumentNullException(
                    "launchRequestStore");
            }

            _view = view;
            _viewerConfig = viewerConfig;
            _playbackService = playbackService;
            _openSettingsFunc = openSettingsFunc;
            _reloadConfigFunc = reloadConfigFunc;
            _launchRequestStore = launchRequestStore;

            /*
             * 실행 중 Pipe Server가 저장소에 새로운 요청을 저장하면
             * PlayerPresenter가 이를 감지한다.
             *
             * 이벤트는 Pipe 백그라운드 스레드에서 발생할 수 있으므로
             * 이벤트 처리 메서드에서 UI 스레드로 전환한다.
             */
            _launchRequestStore.PendingRequestStored += OnPendingLaunchRequestStored;

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
            /*
             * Form Load 이벤트는 WinForms UI 스레드에서 실행된다.
             *
             * Named Pipe 백그라운드 스레드에서 수신한 요청을
             * 안전하게 UI 스레드로 되돌리기 위해 현재 컨텍스트를 저장한다.
             */
            _uiSynchronizationContext =
                SynchronizationContext.Current;

            _isViewLoaded = true;

            _view.SetPlaybackTime(null);
            _view.SetPlaybackState(_playbackState);

            _view.SetPlaybackSpeedOptions();
            _view.SelectPlaybackSpeed(
                PlaybackSpeed.Normal);

            // 설정에 등록된 계산대와 화면 정보를 먼저 반영한다.
            ReloadViewByConfig();

            ApplicationLaunchRequest launchRequest;

            // Program에서 보관한 실행 요청이 있으면 가져온다.
            // 요청을 가져오는 순간 저장소에서는 제거되어
            // 같은 요청이 중복 실행되지 않는다.
            if (!_launchRequestStore.TryTakePendingRequest(
                out launchRequest))
            {
                // 저장된 요청이 없는 예외 상황에서는
                // 일반 직접 실행 요청으로 처리한다.
                launchRequest =
                    ApplicationLaunchRequest.CreateDirectLaunch();
            }

            await ApplyLaunchRequestAsync(
                launchRequest,
                false);
        }


        /// <summary>
        /// 실행 요청 저장소에 새로운 요청이 저장되었을 때 호출된다.
        ///
        /// 이 메서드는 Named Pipe 수신 백그라운드 스레드에서
        /// 호출될 수 있으므로 View에 직접 접근하지 않는다.
        /// </summary>
        /// <param name="request">
        /// 저장소에 새로 저장된 실행 요청.
        /// </param>
        private void OnPendingLaunchRequestStored(
            ApplicationLaunchRequest request)
        {
            /*
             * 직접 중복 실행은 기존 창 활성화만 수행해야 하므로
             * Player의 조회 조건이나 재생 상태를 변경하지 않는다.
             */
            if (request == null || !request.IsExternalPlaybackRequest)
            {
                return;
            }

            /*
             * 종료가 시작된 후 들어온 요청은 처리하지 않는다.
             */
            if (_isClosing)
            {
                return;
            }

            Interlocked.Increment(
                ref _playbackCommandVersion);

            /*
             * 실행 중인 재생 명령에는 취소를 요청한다.
             */
            CancelCurrentPlaybackCommand();

            /*
             * 설정 화면이 열려 있으면 요청을 저장소에 남겨 둔다.
             * 설정 창이 닫힌 뒤 OnSettings()에서 처리한다.
             */
            if (_isSettingsOpen)
            {
                return;
            }

            SynchronizationContext uiContext = _uiSynchronizationContext;

            /*
             * Player 화면이 아직 준비되지 않았다면
             * 요청은 저장소에 그대로 남아 있다.
             *
             * 이후 OnLoadView()의 TryTakePendingRequest()가
             * 해당 요청을 가져와 처리한다.
             */
            if (!_isViewLoaded
                || uiContext == null)
            {
                return;
            }

            /*
             * Pipe 수신 스레드에서 WinForms 컨트롤에 접근하지 않고
             * UI 스레드에 요청 처리를 예약한다.
             */
            uiContext.Post(
                async state =>
                {
                    await ProcessPendingLaunchRequestsAsync();
                },
                null);
        }

        /// <summary>
        /// 저장소에 보관된 외부 실행 요청을 UI 스레드에서 처리한다.
        ///
        /// 처리 도중 새 요청이 들어오면 저장소의 최신 요청을 계속 확인하여
        /// 이벤트 호출 시점의 경합으로 요청이 남지 않도록 한다.
        /// </summary>
        private async Task ProcessPendingLaunchRequestsAsync()
        {
            if( !_isViewLoaded || _isClosing || _isSettingsOpen)
            {
                return;
            }
            /*
             * 외부 요청을 처리할 수 있는 동안 계속 확인한다.
             *
             * 내부 처리 종료 직전에 새 요청이 저장된 경우에도
             * 저장소를 다시 확인하여 요청 유실을 막는다.
             */
            while (_isViewLoaded && !_isClosing && !_isSettingsOpen)
            {
                /*
                 * 이미 다른 처리 루프가 동작 중이면
                 * 해당 루프가 저장소의 최신 요청까지 처리한다.
                 */
                if (_isPendingLaunchRequestProcessing)
                {
                    return;
                }

                _isPendingLaunchRequestProcessing = true;

                try
                {
                    while (_isViewLoaded)
                    {
                        /*
                         * 기존 명령에 취소 요청을 전달했더라도
                         * finally에서 EndPlaybackCommand()가 실행될 때까지 기다린다.
                         */
                        if (_isPlaybackCommandRunning)
                        {
                            await Task.Delay(100);
                            continue;
                        }

                        ApplicationLaunchRequest pendingRequest;

                        /*
                         * 저장소에서 가장 최근 요청을 꺼내면서 제거한다.
                         *
                         * 처리할 요청이 없으면 현재 내부 루프를 종료한다.
                         */
                        if (!_launchRequestStore.TryTakePendingRequest( out pendingRequest))
                        {
                            break;
                        }

                        if (pendingRequest == null
                            || !pendingRequest.IsExternalPlaybackRequest)
                        {
                            continue;
                        }

                        /*
                         * 실행 중 전달된 요청이므로
                         * 기존 영상 대신 새 요청 영상을 재생한다.
                         */
                        await ApplyLaunchRequestAsync( pendingRequest, true);
                    }
                }
                finally
                {
                    _isPendingLaunchRequestProcessing = false;
                }

                /*
                 * 처리 종료 직전에 새 요청이 저장되었는지 다시 확인한다.
                 *
                 * 새 요청이 없다면 정상 종료하고,
                 * 요청이 있으면 외부 while 루프가 다시 처리권을 획득한다.
                 */
                if (!_isViewLoaded || !_isClosing || _isSettingsOpen || !_launchRequestStore.HasPendingRequest)
                {
                    return;
                }
            }
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
                return;
            }

            CancellationToken cancellationToken =
                GetPlaybackCommandCancellationToken();

            try
            {
                PlayerPlaybackResult result =
                    await _playbackService.SetPlaybackSpeedAsync(
                        _view.SelectedPlaybackSpeed,
                        cancellationToken);

                cancellationToken
                    .ThrowIfCancellationRequested();

                if (!result.Success)
                {
                    _view.ShowMessage(
                        result.Message);

                    _view.SelectPlaybackSpeed(
                        _playbackService.CurrentPlaybackSpeed);

                    return;
                }

                _view.SetPlaybackTime(
                    _playbackService.CurrentPlaybackTime);

                _view.SetPlaybackState(
                    _playbackService.CurrentState);

                if (_playbackService.CurrentState
                        == PlaybackState.Paused
                    || _playbackService.CurrentState
                        == PlaybackState.Stopped)
                {
                    _view.StopPlaybackTimer();
                }
                else
                {
                    _view.StartPlaybackTimer();
                }

                _view.SetStatus(
                    result.Message);
            }
            catch (OperationCanceledException)
            {
                /*
                 * 새로운 외부 요청으로 속도 변경이 취소되었다.
                 * 서비스의 실제 속도를 다시 화면에 반영한다.
                 */
                _view.SelectPlaybackSpeed(
                    _playbackService.CurrentPlaybackSpeed);

                _view.SetStatus(
                    "새로운 외부 요청을 처리하기 위해 "
                    + "재생속도 변경을 취소했습니다.");
            }
            catch (Exception ex)
            {
                _view.SelectPlaybackSpeed(
                    _playbackService.CurrentPlaybackSpeed);

                _view.ShowMessage(
                    "재생속도를 변경하는 중 오류가 발생했습니다."
                    + Environment.NewLine
                    + ex.Message);
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        /// <summary>
        /// 검색 버튼 클릭 시 현재 조회 조건으로 재생을 시작한다.
        /// </summary>
        private async void OnSearch(
            object sender,
            EventArgs e)
        {
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            /*
             * 이번 검색 명령에서 사용할 토큰을
             * 명령 시작 직후 한 번만 가져온다.
             */
            CancellationToken cancellationToken =
                GetPlaybackCommandCancellationToken();

            try
            {
                PlayerPlaybackResult result =
                    await PlayFromCurrentSearchRangeAsync( cancellationToken);

                /*
                 * 작업 완료 직전에 취소된 경우
                 * 과거 결과를 화면에 반영하지 않는다.
                 */
                cancellationToken.ThrowIfCancellationRequested();

                HandlePlaybackCommandResult(
                    result);

                if (!result.Success)
                {
                    _view.ShowMessage(
                        result.Message);
                }
            }
            catch (OperationCanceledException)
            {
                /*
                 * 새 외부 요청 등으로 취소된 정상 흐름이다.
                 * 오류 메시지 창은 표시하지 않는다.
                 */
                _view.SetStatus(
                    "이전 영상 조회 작업이 취소되었습니다.");
            }
            catch (Exception ex)
            {
                _view.ShowMessage(
                    "영상 조회 중 오류가 발생했습니다."
                    + Environment.NewLine
                    + ex.Message);
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        /// <summary>
        /// 역재생 버튼 요청을 처리한다.
        /// </summary>
        private async void OnRewind(
            object sender,
            EventArgs e)
        {
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            CancellationToken cancellationToken =
                GetPlaybackCommandCancellationToken();

            try
            {
                PlayerPlaybackResult result;

                if (_playbackService.CurrentState
                    == PlaybackState.Rewinding)
                {
                    result =
                        await _playbackService.PauseAsync(
                            cancellationToken);
                }
                else if (_playbackService.CurrentState
                             == PlaybackState.Paused
                    && _lastPlaybackDirection
                             == PlaybackState.Rewinding)
                {
                    result =
                        await _playbackService.ResumeAsync(
                            cancellationToken);
                }
                else
                {
                    result =
                        await _playbackService.RewindAsync(
                            cancellationToken);
                }

                cancellationToken
                    .ThrowIfCancellationRequested();

                HandlePlaybackCommandResult(
                    result);

                if (!result.Success)
                {
                    return;
                }

                DateTime? playbackTime =
                    await _playbackService
                        .SyncPlaybackTimeAsync(
                            cancellationToken);

                cancellationToken
                    .ThrowIfCancellationRequested();

                _view.SetPlaybackTime(
                    playbackTime);

                _view.SetTimelinePlaybackTime(
                    playbackTime);

                UpdatePlaybackTimerState();
                UpdatePlaybackStateDisplay();
            }
            catch (OperationCanceledException)
            {
                _view.SetStatus(
                    "새로운 외부 요청을 처리하기 위해 "
                    + "기존 역재생 명령을 취소했습니다.");
            }
            catch (Exception ex)
            {
                _view.ShowMessage(
                    "역재생 상태를 변경하는 중 오류가 발생했습니다."
                    + Environment.NewLine
                    + ex.Message);
            }
            finally
            {
                EndPlaybackCommand();
            }
        }


        /// <summary>
        /// 설정된 시간만큼 이전 위치로 이동한다.
        /// </summary>
        private async void OnSeekBackward10(
            object sender,
            EventArgs e)
        {
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            CancellationToken cancellationToken =
                GetPlaybackCommandCancellationToken();

            try
            {
                int seconds =
                    _view.TimeAdjustSeconds;

                PlayerPlaybackResult result =
                    await _playbackService.SeekSecondsAsync(
                        -seconds,
                        cancellationToken);

                cancellationToken
                    .ThrowIfCancellationRequested();

                HandlePlaybackCommandResult(
                    result);

                if (!result.Success)
                {
                    return;
                }

                DateTime? playbackTime =
                    await _playbackService
                        .SyncPlaybackTimeAsync(
                            cancellationToken);

                cancellationToken
                    .ThrowIfCancellationRequested();

                _view.SetPlaybackTime(
                    playbackTime);

                _view.SetTimelinePlaybackTime(
                    playbackTime);

                _view.StartPlaybackTimer();
            }
            catch (OperationCanceledException)
            {
                _view.SetStatus(
                    "새로운 외부 요청을 처리하기 위해 "
                    + "이전 위치 이동 작업을 취소했습니다.");
            }
            catch (Exception ex)
            {
                _view.ShowMessage(
                    "이전 위치로 이동하는 중 오류가 발생했습니다."
                    + Environment.NewLine
                    + ex.Message);
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
             CancellationToken cancellationToken = GetPlaybackCommandCancellationToken();

            try
            {
                PlayerPlaybackResult result;

                if (_playbackService.CurrentState == PlaybackState.Playing)
                {
                    result = await _playbackService.PauseAsync( cancellationToken);
                }
                else if (_playbackService.CurrentState == PlaybackState.Paused
                    && _lastPlaybackDirection == PlaybackState.Playing)
                {
                    result = await _playbackService.ResumeAsync(cancellationToken);
                }
                else if (_playbackService.CurrentState == PlaybackState.Rewinding)
                {
                    result = await _playbackService.PlayForwardFromCurrentTimeAsync(cancellationToken);
                }
                else if (_playbackService.CurrentState == PlaybackState.Paused
                    && _lastPlaybackDirection == PlaybackState.Rewinding)
                {
                    result = await _playbackService.PlayForwardFromCurrentTimeAsync(cancellationToken);
                }
                else
                {
                    result = await PlayFromCurrentSearchRangeAsync(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();

                HandlePlaybackCommandResult(result);

                if (!result.Success)
                {
                    return;
                }

                DateTime? playbackTime = _playbackService.CurrentPlaybackTime;

                _view.SetPlaybackTime(playbackTime);
                _view.SetTimelinePlaybackTime(playbackTime);

                UpdatePlaybackTimerState();
                UpdatePlaybackStateDisplay();
            }
            catch (OperationCanceledException)
            {
                _view.SetStatus("새로운 외부 요청을 처리하기 위"
                    + "기존 재생 명령을 취소했습니다.");
            }
            catch (Exception ex)
            {
                _view.SetStatus(
                    "재생 상태를 변경하는 중 오류가 발생했습니다."
                    + Environment.NewLine
                    + ex.ToString());
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        /// <summary> 
        /// 재생 대상 채널의 실제 영상 해상도를 조회하여 View에 반영한다. 
        /// </summary> 
        /// <param name="request"> 
        /// 해상도를 조회할 재생 요청. 
        /// </param> 
        /// <param name="cancellationToken"> 
        /// 현재 재생 명령의 취소 토큰. 
        /// </param> 
        private async Task ApplyVideoSourceInfoAsync( PlayerPlaybackRequest request, CancellationToken cancellationToken) 
        { 
            if (request == null || request.Channels == null) 
            { 
                return; 
            } 
            foreach (PlayerChannelTarget channel in request.Channels) 
            { 
                cancellationToken .ThrowIfCancellationRequested(); 
                PlayerVideoSourceInfoResult infoResult = await _playbackService .GetVideoSourceInfoAsync( channel, cancellationToken); 
                cancellationToken .ThrowIfCancellationRequested(); 
                if (!infoResult.Success) 
                { 
                    continue; 
                }
                
                _view.SetVideoSourceSize( infoResult.ScreenPosition, infoResult.Width, infoResult.Height); 
            } 
            
            cancellationToken .ThrowIfCancellationRequested(); 
            
            _view.UpdateVideoLayout(); 
        }


        /// <summary>
        /// 현재 PlayerView의 조회 조건을 기준으로 재생을 시작한다.
        /// </summary>
        private async Task<PlayerPlaybackResult> PlayFromCurrentSearchRangeAsync(CancellationToken cancellationToken)
        {
            PlayerPlaybackRequest request;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                request = BuildPlaybackRequest();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return PlayerPlaybackResult.Fail(
                    ex.Message,
                    "PLAYBACK_REQUEST_BUILD_FAILED");
            }

            _view.StopPlaybackTimer();

            await ApplyVideoSourceInfoAsync(request, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            PlayerPlaybackResult playResult = await _playbackService.PlayAsync( request, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

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
            DateTime? playbackTime = _playbackService.CurrentPlaybackTime;

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
        /// 설정된 시간만큼 이후 위치로 이동한다.
        /// </summary>
        private async void OnSeekForward10(
            object sender,
            EventArgs e)
        {
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            CancellationToken cancellationToken =
                GetPlaybackCommandCancellationToken();

            try
            {
                int seconds =
                    _view.TimeAdjustSeconds;

                PlayerPlaybackResult result =
                    await _playbackService.SeekSecondsAsync(
                        seconds,
                        cancellationToken);

                cancellationToken
                    .ThrowIfCancellationRequested();

                HandlePlaybackCommandResult(
                    result);

                if (!result.Success)
                {
                    return;
                }

                DateTime? playbackTime =
                    await _playbackService
                        .SyncPlaybackTimeAsync(
                            cancellationToken);

                cancellationToken
                    .ThrowIfCancellationRequested();

                _view.SetPlaybackTime(
                    playbackTime);

                _view.SetTimelinePlaybackTime(
                    playbackTime);

                _view.StartPlaybackTimer();
            }
            catch (OperationCanceledException)
            {
                _view.SetStatus(
                    "새로운 외부 요청을 처리하기 위해 "
                    + "이후 위치 이동 작업을 취소했습니다.");
            }
            catch (Exception ex)
            {
                _view.ShowMessage(
                    "이후 위치로 이동하는 중 오류가 발생했습니다."
                    + Environment.NewLine
                    + ex.Message);
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

            CancellationToken cancellationToken =
                GetPlaybackCommandCancellationToken();

            try
            {
                _view.StopPlaybackTimer();

                PlayerPlaybackResult result =
                    await _playbackService.StopAsync(
                        cancellationToken);

                cancellationToken
                    .ThrowIfCancellationRequested();

                HandlePlaybackCommandResult(
                    result);

                if (!result.Success)
                {
                    return;
                }

                _view.SetPlaybackTime(
                    null);

                _view.SetTimelinePlaybackTime(
                    null);

                _view.SetPlaybackState(
                    _playbackService.CurrentState);
            }
            catch (OperationCanceledException)
            {
                /*
                 * 새로운 외부 요청이 들어온 경우 정지 처리 완료를 기다리지 않고
                 * 최신 외부 요청 처리 흐름으로 넘어간다.
                 */
                _view.SetStatus(
                    "새로운 외부 요청을 처리하기 위해 "
                    + "기존 정지 작업을 취소했습니다.");
            }
            catch (Exception ex)
            {
                _view.ShowMessage(
                    "영상을 정지하는 중 오류가 발생했습니다."
                    + Environment.NewLine
                    + ex.Message);
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        /// <summary>
        /// PlayerView 종료 요청을 처리한다.
        ///
        /// 현재 진행 중인 재생 명령을 취소하고,
        /// Pipe 이벤트와 NVR 재생 리소스를 정리한 뒤 화면을 닫는다.
        /// </summary>
        private async void OnClose( object sender, EventArgs e)
        {
            if (_isClosing)
            {
                return;
            }

            _isClosing = true;
            _isViewLoaded = false;

            /*
             * 종료가 시작된 이후에는 새로운 Pipe 요청이
             * PlayerPresenter로 전달되지 않도록 먼저 구독을 해제한다.
             */
            _launchRequestStore.PendingRequestStored -= OnPendingLaunchRequestStored;

            /*
             * 아직 처리하지 않은 실행 요청은 종료 시 폐기한다.
             */
            _launchRequestStore.Clear();

            _view.StopPlaybackTimer();

            /*
             * 현재 실행 중인 NVR 명령에 취소를 요청한다.
             */
            CancelCurrentPlaybackCommand();

            /*
             * 기존 명령의 finally에서 EndPlaybackCommand()가 실행될 때까지
             * 최대 5초 동안 기다린다.
             */
            DateTime waitLimit = DateTime.UtcNow.AddSeconds(5);

            while (_isPlaybackCommandRunning && DateTime.UtcNow < waitLimit)
            {
                await Task.Delay(50);
            }

            try
            {
                /*
                 * 기존 명령이 정상적으로 종료된 경우에만
                 * 마지막 Stop 명령을 별도로 실행한다.
                 *
                 * 아직 명령이 끝나지 않았다면 Dispose를 통해
                 * Provider 리소스 정리를 시도한다.
                 */
                if (!_isPlaybackCommandRunning)
                {
                    using (var closeCancellationTokenSource = new CancellationTokenSource())
                    {
                        closeCancellationTokenSource
                            .CancelAfter( TimeSpan.FromSeconds(5));

                        try
                        {
                            await _playbackService.StopAsync( closeCancellationTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            /*
                             * 종료 시 정지 명령이 제한시간을 넘긴 경우에도
                             * 애플리케이션 종료는 계속 진행한다.
                             */
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                /*
                 * 다른 종료 경로에서 서비스가 먼저 해제된 경우이다.
                 */
            }
            catch (Exception ex)
            {
                /*
                 * 종료 자체를 막지는 않고 상태만 기록한다.
                 */
                _view.SetStatus(
                    "재생 리소스 정리 중 오류가 발생했습니다. "
                    + ex.Message);
            }
            finally
            {
                try
                {
                    _playbackService.Dispose();
                }
                catch
                {
                    /*
                     * 종료 단계에서는 Dispose 오류로
                     * 애플리케이션 종료를 중단하지 않는다.
                     */
                }

                _uiSynchronizationContext = null;

                _view.CloseView();
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
        ///
        /// 재생 중이면 먼저 재생을 정지한다.
        /// 설정 화면이 열린 동안 들어온 외부 요청은 저장소에 보관하고,
        /// 설정 화면이 닫힌 뒤 최신 요청을 처리한다.
        /// </summary>
        private async void OnSettings(object sender, EventArgs e)
        {
            if (_isClosing
                || _isSettingsOpen)
            {
                return;
            }

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
                _view.ShowMessage(
                    "설정 화면 실행 구성이 없습니다.");

                return;
            }

            /*
             * 영상 재생 중이라면 설정 화면을 열기 전에 정지한다.
             */
            if (_playbackService.CurrentState
                != PlaybackState.Stopped)
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

                if (!TryBeginPlaybackCommand())
                {
                    return;
                }

                CancellationToken cancellationToken = GetPlaybackCommandCancellationToken();

                try
                {
                    _view.StopPlaybackTimer();

                    PlayerPlaybackResult stopResult = await _playbackService.StopAsync(cancellationToken);

                    cancellationToken .ThrowIfCancellationRequested();

                    if (!stopResult.Success)
                    {
                        _view.ShowMessage(stopResult.Message);

                        return;
                    }

                    _view.SetPlaybackTime(null);

                    _view.SetTimelinePlaybackTime(null);

                    _view.SetPlaybackState(_playbackService.CurrentState);

                    _view.SetStatus("재생을 중지했습니다.");
                }
                catch (OperationCanceledException)
                {
                    /*
                     * 정지 처리 도중 외부 요청이 들어온 경우
                     * 설정 화면을 열지 않고 최신 외부 요청을 우선 처리한다.
                     */
                    _view.SetStatus(
                        "새로운 외부 요청으로 인해 "
                        + "설정 화면 실행을 취소했습니다.");

                    return;
                }
                catch (Exception ex)
                {
                    _view.ShowMessage(
                        "설정 화면을 열기 위해 영상을 정지하는 중 "
                        + "오류가 발생했습니다."
                        + Environment.NewLine
                        + ex.Message);

                    return;
                }
                finally
                {
                    EndPlaybackCommand();
                }
            }

            /*
             * 여기부터는 설정 창이 열린 동안 외부 요청 자동 실행을 보류한다.
             */
            _isSettingsOpen = true;

            try
            {
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
                    _view.ShowMessage( "변경된 설정을 다시 불러오지 못했습니다.");

                    return;
                }

                _viewerConfig = reloadedConfig;

                ReloadViewByConfig();

                _view.SetStatus( "설정이 변경되어 화면을 갱신했습니다.");
            }
            catch (Exception ex)
            {
                _view.ShowMessage( "설정 화면을 처리하는 중 오류가 발생했습니다."
                    + Environment.NewLine
                    + ex.Message);
            }
            finally
            {
                _isSettingsOpen = false;

                /*
                 * 설정 창이 열린 동안 외부 요청이 들어왔다면
                 * 설정 반영이 끝난 후 최신 요청을 처리한다.
                 */
                if (_isViewLoaded && !_isClosing && _launchRequestStore.HasPendingRequest)
                {
                    await ProcessPendingLaunchRequestsAsync();
                }
            }
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
        /// 영상재생시간 갱신 Tick을 받아 현재 재생 위치와
        /// 좌우 동기화 상태를 화면에 반영한다.
        ///
        /// 타이머 조회 도중 새로운 재생 명령이 시작되면
        /// 이전 조회 결과는 화면에 반영하지 않는다.
        /// </summary>
        private async void OnPlaybackTimerTick( object sender, EventArgs e)
        {
            /*
             * 이전 Tick이 아직 처리 중이거나
             * 재생 명령이 실행 중이면 이번 Tick은 건너뛴다.
             */
            if (_isPlaybackTimerTickRunning
                || _isPlaybackCommandRunning
                || !_isViewLoaded)
            {
                return;
            }

            _isPlaybackTimerTickRunning = true;

            /*
             * 이번 타이머 작업이 시작된 시점의 명령 버전을 보관한다.
             */
            long commandVersion =
                Interlocked.Read(ref _playbackCommandVersion);

            try
            {
                DateTime? playbackTime = await _playbackService .SyncPlaybackTimeAsync(CancellationToken.None);

                /*
                 * 조회 도중 새로운 재생 명령이 시작되었다면
                 * 과거 재생시간을 현재 화면에 적용하지 않는다.
                 */
                if (!_isViewLoaded
                    || _isPlaybackCommandRunning
                    || commandVersion
                        != Interlocked.Read(ref _playbackCommandVersion))
                {
                    return;
                }

                _view.SetPlaybackTime(playbackTime);

                _view.SetTimelinePlaybackTime(playbackTime);

                PlaybackSyncStatus syncStatus =
                    await _playbackService
                        .GetPlaybackSyncStatusAsync(CancellationToken.None);

                /*
                 * 동기화 상태를 가져오는 동안에도
                 * 새로운 명령이 시작될 수 있으므로 다시 확인한다.
                 */
                if (!_isViewLoaded
                    || _isPlaybackCommandRunning
                    || commandVersion
                        != Interlocked.Read(ref _playbackCommandVersion))
                {
                    return;
                }

                if (syncStatus != null)
                {
                    _view.SetPlaybackSyncStatus(syncStatus.ToDisplayText());
                }
            }
            catch (ObjectDisposedException)
            {
                /*
                 * Player 종료와 타이머 조회가 동시에 발생한 경우에는
                 * 종료 과정의 정상적인 경합으로 처리한다.
                 */
            }
            catch (Exception ex)
            {
                /*
                 * 반복되는 타이머 오류에서 메시지 창을 계속 띄우지 않고
                 * 상태 영역에만 오류를 표시한다.
                 */
                if (_isViewLoaded)
                {
                    _view.SetStatus(
                        "재생시간을 동기화하지 못했습니다. "
                        + ex.Message);
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
        private async void OnSync(object sender,EventArgs e)
        {
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            CancellationToken cancellationToken =
                GetPlaybackCommandCancellationToken();

            try
            {
                PlayerPlaybackResult result =
                    await _playbackService
                        .ResyncPlaybackSessionsAsync(
                            cancellationToken);

                cancellationToken
                    .ThrowIfCancellationRequested();

                HandlePlaybackCommandResult(
                    result);

                if (!result.Success)
                {
                    return;
                }

                DateTime? playbackTime =
                    await _playbackService
                        .SyncPlaybackTimeAsync(
                            cancellationToken);

                cancellationToken
                    .ThrowIfCancellationRequested();

                _view.SetPlaybackTime(
                    playbackTime);

                _view.SetTimelinePlaybackTime(
                    playbackTime);

                PlaybackSyncStatus syncStatus =
                    await _playbackService
                        .GetPlaybackSyncStatusAsync(
                            cancellationToken);

                cancellationToken
                    .ThrowIfCancellationRequested();

                if (syncStatus != null)
                {
                    _view.SetPlaybackSyncStatus(
                        syncStatus.ToDisplayText());
                }
            }
            catch (OperationCanceledException)
            {
                _view.SetStatus(
                    "새로운 외부 요청을 처리하기 위해 "
                    + "영상 동기화 작업을 취소했습니다.");
            }
            catch (Exception ex)
            {
                _view.ShowMessage(
                    "영상을 동기화하는 중 오류가 발생했습니다."
                    + Environment.NewLine
                    + ex.Message);
            }
            finally
            {
                EndPlaybackCommand();
            }
        }

        /// <summary>
        /// 타임라인 클릭에 따른 재생 위치 이동 요청을 처리한다.
        /// </summary>
        private async void OnTimelineSeekRequested(
            object sender,
            TimelineSeekRequestedEventArgs e)
        {
            if (!TryBeginPlaybackCommand())
            {
                return;
            }

            CancellationToken cancellationToken =
                GetPlaybackCommandCancellationToken();

            try
            {
                PlayerPlaybackResult result =
                    await _playbackService.SeekToTimeAsync(
                        e.TargetTime,
                        cancellationToken);

                cancellationToken
                    .ThrowIfCancellationRequested();

                HandlePlaybackCommandResult(
                    result);

                if (!result.Success)
                {
                    return;
                }

                DateTime? playbackTime =
                    await _playbackService
                        .SyncPlaybackTimeAsync(
                            cancellationToken);

                cancellationToken
                    .ThrowIfCancellationRequested();

                _view.SetPlaybackTime(
                    playbackTime);

                _view.SetTimelinePlaybackTime(
                    playbackTime);

                _view.StartPlaybackTimer();
            }
            catch (OperationCanceledException)
            {
                _view.SetStatus(
                    "새로운 외부 요청을 처리하기 위해 "
                    + "타임라인 이동 작업을 취소했습니다.");
            }
            catch (Exception ex)
            {
                _view.ShowMessage(
                    "타임라인 위치로 이동하는 중 오류가 발생했습니다."
                    + Environment.NewLine
                    + ex.Message);
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

            CancellationToken cancellationToken = GetPlaybackCommandCancellationToken();

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
                            cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

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
                    await PlayFromCurrentSearchRangeAsync( cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                HandlePlaybackCommandResult(
                    playResult);

                if (!playResult.Success)
                {
                    return;
                }

                UpdatePlaybackTimerState();
                UpdatePlaybackStateDisplay();
            }
            catch (OperationCanceledException)
            {
                /*
                 * 현재 외부 요청을 처리하는 도중
                 * 더 최근의 외부 요청이 들어온 경우이다.
                 *
                 * 오류로 취급하지 않고 최신 요청 처리로 넘어간다.
                 */
                _view.SetStatus(
                    "새로운 외부 요청을 처리하기 위해 "
                    + "이전 재생 작업을 취소했습니다.");
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


        /// <summary>
        /// 새로운 재생 명령을 시작한다.
        ///
        /// 명령이 시작되면 해당 명령에서 사용할
        /// CancellationTokenSource를 생성한다.
        /// </summary>
        private bool TryBeginPlaybackCommand()
        {
            lock (_playbackCommandCancellationSyncRoot)
            {
                if (_isPlaybackCommandRunning)
                {
                    _view.SetStatus(
                        "이전 재생 명령을 처리 중입니다.");

                    return false;
                }

                _playbackCommandCancellationTokenSource =
                    new CancellationTokenSource();

                /*
                 * 새로운 재생 명령이 시작되었으므로
                 * 이전 비동기 조회 결과를 무효화한다.
                 */
                Interlocked.Increment(
                    ref _playbackCommandVersion);

                _isPlaybackCommandRunning = true;

                return true;
            }
        }

        /// <summary>
        /// 현재 실행 중인 재생 명령의 취소 토큰을 반환한다.
        ///
        /// 실행 중인 명령이 없으면 취소할 수 없는 기본 토큰을 반환한다.
        /// </summary>
        private CancellationToken GetPlaybackCommandCancellationToken()
        {
            lock (_playbackCommandCancellationSyncRoot)
            {
                if (_playbackCommandCancellationTokenSource == null)
                {
                    return CancellationToken.None;
                }

                return _playbackCommandCancellationTokenSource.Token;
            }
        }

        /// <summary>
        /// 현재 실행 중인 재생 명령에 취소를 요청한다.
        ///
        /// 이 메서드는 Named Pipe 백그라운드 스레드에서도
        /// 호출될 수 있으므로 내부 접근을 lock으로 보호한다.
        /// </summary>
        private void CancelCurrentPlaybackCommand()
        {
            CancellationTokenSource cancellationTokenSource;

            lock (_playbackCommandCancellationSyncRoot)
            {
                cancellationTokenSource =
                    _playbackCommandCancellationTokenSource;
            }

            if (cancellationTokenSource == null
                || cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            try
            {
                cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
                /*
                 * 작업 종료와 취소 요청이 동시에 발생한 경우
                 * 이미 해제된 토큰에 대한 예외는 무시한다.
                 */
            }
        }

        /// <summary>
        /// 재생 명령 실행 상태와 취소 토큰을 정리한다.
        /// </summary>
        private void EndPlaybackCommand()
        {
            CancellationTokenSource cancellationTokenSource;

            lock (_playbackCommandCancellationSyncRoot)
            {
                cancellationTokenSource =
                    _playbackCommandCancellationTokenSource;

                _playbackCommandCancellationTokenSource =
                    null;

                _isPlaybackCommandRunning =
                    false;
            }

            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Dispose();
            }
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
    }
}