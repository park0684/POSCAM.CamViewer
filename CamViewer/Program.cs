using CamViewer.Infrastructure;
using CamViewer.Interfaces;
using CamViewer.Models;
using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Providers;
using CamViewer.Nvr.Core.Results;
using CamViewer.Presenters;
using CamViewer.Services;
using CamViewer.Views;
using CamViewerClient;
using CamViewerClient.Models.Config;
using CamViewerClient.Results;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;


namespace CamViewer
{
    internal static class Program
    {
        /// <summary>
        /// CamViewer 애플리케이션의 주 진입점이다.
        ///
        /// 처리 순서:
        /// 1. Windows Forms 실행 환경 초기화
        /// 2. 프로그램 실행 인자 분석
        /// 3. 단일 인스턴스 실행 권한 확인
        /// 4. 중복 직접 실행이면 기존 창 활성화 후 종료
        /// 5. 중복 외부 실행이면 기존 프로세스로 영상 요청 전송 후 종료
        /// 6. 최초 프로세스이면 Named Pipe 서버 시작
        /// 7. 최초 실행 요청을 메모리 저장소에 보관
        /// 8. 인증 및 설정 확인을 담당하는 Landing 화면 실행
        /// 9. 인증 완료 후 PlayerPresenter에 동일한 요청 저장소 전달
        /// </summary>
        [STAThread]
        private static void Main(
            string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            InitializeDebugFileLog();

            /*
             * 명령행 인자를 CamViewer 내부 실행 요청으로 변환한다.
             *
             * 인자가 없으면 직접 실행 요청,
             * --playback-time이 있으면 외부 영상 조회 요청이다.
             */
            IApplicationLaunchArgumentParser argumentParser =
                new ApplicationLaunchArgumentParser();

            ApplicationLaunchRequest launchRequest;
            string parseErrorMessage;

            bool parseSuccess =
                argumentParser.TryParse(
                    args,
                    out launchRequest,
                    out parseErrorMessage);

            if (!parseSuccess)
            {
                MessageBox.Show(
                    parseErrorMessage,
                    "CamViewer 실행 요청 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            /*
             * ApplicationSingleInstance는 프로그램이 종료될 때까지 유지한다.
             *
             * 첫 번째 프로세스가 Mutex를 소유하고,
             * 두 번째 프로세스는 기존 프로세스에 요청만 전달하고 종료한다.
             */
            using (var singleInstance =
                new ApplicationSingleInstance())
            {
                bool isFirstInstance =
                    singleInstance.TryAcquire();

                /*
                 * 이미 CamViewer가 실행 중인 경우.
                 */
                if (!isFirstInstance)
                {
                    /*
                     * 직접 중복 실행은 기존 재생 상태를 변경하지 않는다.
                     *
                     * 기존 창만 복원하고 두 번째 프로세스를 종료한다.
                     */
                    if (!launchRequest.IsExternalPlaybackRequest)
                    {
                        MessageBox.Show(
                            "프로그램이 이미 실행 중입니다.",
                            "POSCAM CamViewer",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        singleInstance.TryActivateExistingInstance();

                        return;
                    }

                    /*
                     * 외부 영상 조회 요청이면 현재 프로세스에서 Player를 만들지 않고
                     * 기존 CamViewer의 Named Pipe 서버로 요청을 전달한다.
                     */
                    var pipeClient =
                        new ApplicationLaunchPipeClient();

                    string pipeErrorMessage;

                    bool sendSuccess =
                        pipeClient.TrySend(
                            launchRequest,
                            out pipeErrorMessage);

                    if (!sendSuccess)
                    {
                        /*
                         * 요청 전송에 실패한 경우 새 프로세스를 계속 실행하면
                         * 단일 실행 정책이 깨지므로 오류만 안내하고 종료한다.
                         */
                        MessageBox.Show(
                            "실행 중인 CamViewer로 영상 요청을 전달하지 못했습니다."
                            + Environment.NewLine
                            + pipeErrorMessage,
                            "POSCAM CamViewer",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }

                    /*
                     * 요청 전송 성공 여부와 관계없이 기존 CamViewer 창을
                     * 복원하여 사용자가 현재 상태를 확인할 수 있게 한다.
                     */
                    singleInstance.TryActivateExistingInstance();

                    return;
                }

                /*
                 * 최초 프로세스에서 사용할 요청 저장소이다.
                 *
                 * 최초 실행 요청과 실행 중 Pipe로 전달되는 요청이
                 * 모두 동일한 저장소를 사용해야 한다.
                 */
                IApplicationLaunchRequestStore launchRequestStore =
                    new ApplicationLaunchRequestStore();

                /*
                 * 최초 실행 요청을 저장한다.
                 *
                 * PlayerPresenter가 생성된 뒤 OnLoadView에서
                 * TryTakePendingRequest()로 가져간다.
                 */
                launchRequestStore.SetPendingRequest(
                    launchRequest);

                /*
                 * 기존 프로세스로 전달되는 실행 요청을 수신할
                 * Named Pipe 서버를 생성한다.
                 *
                 * using 범위를 Application.Run 종료 시점까지 유지하여
                 * 프로그램 실행 중 항상 요청을 받을 수 있도록 한다.
                 */
                using (var pipeServer =
                    new ApplicationLaunchPipeServer())
                {
                    /*
                     * Pipe Server가 받은 요청을 공용 요청 저장소에 저장한다.
                     *
                     * 저장소는 PendingRequestStored 이벤트를 발생시키고,
                     * PlayerPresenter가 이를 UI 스레드에서 처리한다.
                     */
                    pipeServer.RequestReceived +=
                        launchRequestStore.SetPendingRequest;

                    /*
                     * Landing, Login 또는 Player 화면이 실행되기 전에
                     * Pipe 서버를 먼저 시작한다.
                     *
                     * 따라서 프로그램 초기화 도중 외부 요청이 들어와도
                     * 저장소에 보관할 수 있다.
                     */
                    pipeServer.Start();

                    try
                    {
                        /*
                         * CamViewer 인증, 설정 API 및 로컬 설정 저장 기능을 제공한다.
                         */
                        var clientFacade =
                            new CamViewerClientFacade();

                        /*
                         * HWID, PC 이름, 프로그램 버전 등
                         * 현재 PC 실행환경 정보를 제공한다.
                         */
                        var environmentProvider =
                            new ClientEnvironmentProvider();

                        /*
                         * 온라인 인증 후 로컬 설정과 서버 설정 버전을 확인한다.
                         */
                        ILandingStartupService landingStartupService =
                            new LandingStartupService(
                                clientFacade);

                        /*
                         * 실행 폴더의 NVR Provider를 등록한다.
                         */
                        var providerCatalog =
                            new NvrProviderCatalog();

                        LoadNvrProviders(
                            providerCatalog);

                        /*
                         * 설정에 기록된 ProviderKey에 맞는
                         * NVR Provider 인스턴스를 생성한다.
                         */
                        var providerFactory =
                            new NvrProviderFactory(
                                providerCatalog);

                        /*
                         * 설정 화면 표시, 로컬 저장 및 서버 업로드를 담당한다.
                         */
                        var settingsFlowService =
                            new SettingsFlowService(
                                clientFacade,
                                providerCatalog,
                                providerFactory,
                                environmentProvider);

                        /*
                         * 프로그램 시작 화면을 생성한다.
                         */
                        var landingView =
                            new LandingView();

                        /*
                         * 인증과 설정 확인이 완료되면 Player를 생성한다.
                         *
                         * 최초 요청과 실행 중 외부 요청이 들어오는
                         * 동일한 launchRequestStore를 PlayerPresenter에 전달한다.
                         */
                        var landingPresenter =
                            new LandingPresenter(
                                landingView,
                                clientFacade,
                                landingStartupService,
                                environmentProvider,
                                CreateLoginView,
                                settingsFlowService.OpenSettings,
                                () =>
                                {
                                    OpenPlayerTemporary(
                                        clientFacade,
                                        settingsFlowService,
                                        providerFactory,
                                        launchRequestStore);
                                });

                        /*
                         * LandingView.ShowView() 내부에서 Application.Run()이 실행된다.
                         *
                         * 화면이 종료될 때까지 현재 메서드는 여기에서 대기하므로
                         * Mutex와 Pipe Server도 계속 유지된다.
                         */
                        landingPresenter.Show();
                    }
                    finally
                    {
                        /*
                         * 애플리케이션 종료 시 이벤트 연결을 해제한다.
                         *
                         * pipeServer.Dispose()는 using 블록 종료 시 자동 호출되어
                         * 연결 대기 중인 Named Pipe도 정리된다.
                         */
                        pipeServer.RequestReceived -=
                            launchRequestStore.SetPendingRequest;
                    }
                }
            }
        }

        /// <summary>
        /// 로그인 화면 인스턴스를 생성한다.
        /// </summary>
        /// <returns>새로 생성된 로그인 View.</returns>
        private static ILoginView CreateLoginView()
        {
            return new LoginView();
        }

        /// <summary>
        /// 실행 폴더의 providers 하위 DLL을 검색하여
        /// NVR Provider Catalog에 등록한다.
        /// </summary>
        /// <param name="providerCatalog">
        /// Provider를 등록할 Catalog.
        /// </param>
        private static void LoadNvrProviders(
            NvrProviderCatalog providerCatalog)
        {
            string providerPath =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "providers");

            var providerLoader =
                new ProviderAssemblyLoader();

            var providerBootstrapper =
                new NvrProviderBootstrapper(
                    providerLoader,
                    providerCatalog);

            ProviderLoadReport loadReport =
                providerBootstrapper.LoadAndRegister(
                    providerPath);

            if (loadReport.LoadedCount > 0
                && loadReport.Errors.Count == 0)
            {
                return;
            }

            MessageBox.Show(
                "사용 가능한 NVR Provider를 불러오지 못했거나 일부 오류가 있습니다."
                + Environment.NewLine
                + "Provider 경로: "
                + providerPath
                + Environment.NewLine
                + "등록된 Provider 수: "
                + loadReport.LoadedCount
                + Environment.NewLine
                + "오류 수: "
                + loadReport.Errors.Count,
                "NVR Provider 로드 확인",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        /// <summary>
        /// 인증 및 설정 확인이 완료된 후 PlayerView를 실행한다.
        ///
        /// 처리 순서:
        /// 1. 저장된 최신 로컬 설정을 다시 불러온다.
        /// 2. PlayerView 및 재생 서비스를 생성한다.
        /// 3. 최초 실행 요청 저장소를 PlayerPresenter에 전달한다.
        /// 4. PlayerView가 로드되면 저장된 요청을 꺼내 자동 재생한다.
        /// </summary>
        /// <param name="clientFacade">
        /// 로컬 설정 및 인증 관련 기능을 제공하는 Facade.
        /// </param>
        /// <param name="settingsFlowService">
        /// 설정 화면을 열고 저장하는 서비스.
        /// </param>
        /// <param name="providerFactory">
        /// 설정에 맞는 NVR Provider를 생성하는 Factory.
        /// </param>
        /// <param name="launchRequestStore">
        /// 직접 실행 또는 외부 프로그램에서 전달된
        /// 영상 조회 요청을 보관한 저장소.
        /// </param>
        private static void OpenPlayerTemporary(
            CamViewerClientFacade clientFacade,
            SettingsFlowService settingsFlowService,
            INvrProviderFactory providerFactory,
            IApplicationLaunchRequestStore launchRequestStore)
        {
            /*
             * 설정 화면에서 저장한 최신 BeforeSeconds 등의 값을 사용하도록
             * PlayerPresenter 생성 직전에 로컬 설정을 다시 불러온다.
             */
            ClientResult<ViewerConfig> configResult =
                clientFacade.LoadLocalConfig();

            if (!configResult.Success
                || configResult.Data == null)
            {
                MessageBox.Show(
                    "PlayerView를 실행할 설정 정보를 불러오지 못했습니다."
                    + Environment.NewLine
                    + configResult.Message,
                    "POSCAM CamViewer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                Application.Exit();
                return;
            }

            var playerView =
                new PlayerView();

            /*
             * PlayerView가 종료되면 숨겨진 LandingView도 함께 정리하도록
             * 애플리케이션 전체를 종료한다.
             */
            playerView.FormClosed +=
                (sender, e) =>
                {
                    Application.Exit();
                };

            var playbackService =
                new NvrPlayerPlaybackService(
                    providerFactory);

            /*
             * PlayerPresenter에 최초 실행 요청 저장소를 전달한다.
             *
             * PlayerPresenter.OnLoadView()에서:
             * - 직접 실행이면 현재 시각
             * - 외부 요청이면 전달받은 ReferenceTime
             *
             * 을 기준으로 ViewerConfig.PlaybackOption.BeforeSeconds와
             * AfterCompleteSeconds를 적용하여 재생 구간을 계산한다.
             */
            var playerPresenter =
                new PlayerPresenter(
                    playerView,
                    configResult.Data,
                    playbackService,
                    settingsFlowService.OpenSettings,
                    () =>
                    {
                        ClientResult<ViewerConfig> reloadResult =
                            clientFacade.LoadLocalConfig();

                        if (!reloadResult.Success
                            || reloadResult.Data == null)
                        {
                            return null;
                        }

                        return reloadResult.Data;
                    },
                    launchRequestStore);

            playerPresenter.Show();
        }

        /// <summary>
        /// CamViewer 디버그 파일 로그를 초기화한다.
        ///
        /// 기본 빌드에서는 실행되지 않는다.
        /// CAMVIEWER_DEBUG_LOG 컴파일 심볼을 켰을 때만 동작한다.
        /// </summary>
        [Conditional("CAMVIEWER_DEBUG_LOG")]
        private static void InitializeDebugFileLog()
        {
            try
            {
                string logDirectory =
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "logs");

                if (!Directory.Exists(
                    logDirectory))
                {
                    Directory.CreateDirectory(
                        logDirectory);
                }

                string logPath =
                    Path.Combine(
                        logDirectory,
                        "camviewer_debug.log");

                string startMessage =
                    Environment.NewLine
                    + "=================================================="
                    + Environment.NewLine
                    + DateTime.Now.ToString(
                        "yyyy-MM-dd HH:mm:ss.fff")
                    + " | CamViewer debug log started"
                    + Environment.NewLine
                    + "BaseDirectory: "
                    + AppDomain.CurrentDomain.BaseDirectory
                    + Environment.NewLine
                    + "LogPath: "
                    + logPath
                    + Environment.NewLine
                    + "=================================================="
                    + Environment.NewLine;

                File.AppendAllText(
                    logPath,
                    startMessage,
                    System.Text.Encoding.UTF8);
            }
            catch
            {
                // 디버그 로그 생성 실패는 프로그램 실행을 막지 않는다.
            }
        }

        /// <summary>
        /// CamViewer 디버그 로그를 파일에 기록한다.
        ///
        /// 기본 빌드에서는 호출 코드가 컴파일 결과에서 제외된다.
        /// CAMVIEWER_DEBUG_LOG 컴파일 심볼을 켰을 때만 동작한다.
        /// </summary>
        /// <param name="message">기록할 디버그 메시지.</param>
        [Conditional("CAMVIEWER_DEBUG_LOG")]
        internal static void WriteDebugLog(
            string message)
        {
            try
            {
                string logDirectory =
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "logs");

                if (!Directory.Exists(
                    logDirectory))
                {
                    Directory.CreateDirectory(
                        logDirectory);
                }

                string logPath =
                    Path.Combine(
                        logDirectory,
                        "camviewer_debug.log");

                string line =
                    DateTime.Now.ToString(
                        "yyyy-MM-dd HH:mm:ss.fff")
                    + " | "
                    + message
                    + Environment.NewLine;

                File.AppendAllText(
                    logPath,
                    line,
                    System.Text.Encoding.UTF8);
            }
            catch
            {
                // 디버그 로그 기록 실패는 프로그램 실행을 막지 않는다.
            }
        }
    }
}