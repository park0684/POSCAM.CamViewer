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
        /// 3. 직접 실행 또는 외부 영상 조회 요청 생성
        /// 4. Player 화면이 준비될 때까지 실행 요청 저장
        /// 5. 인증 및 설정 확인을 담당하는 Landing 화면 실행
        /// 6. 인증 완료 후 PlayerPresenter에 동일한 요청 저장소 전달
        /// </summary>
        /// <param name="args">
        /// 프로그램 실행 시 전달된 명령행 인자.
        ///
        /// 직접 실행:
        /// CamViewer.exe
        ///
        /// 외부 시간 전달:
        /// CamViewer.exe --playback-time "2026-06-15T14:30:00"
        ///
        /// 외부 시간 및 계산대 전달:
        /// CamViewer.exe --playback-time "2026-06-15T14:30:00" --counter 2
        /// </param>
        [STAThread]
        private static void Main(
            string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            InitializeDebugFileLog();

            /*
             * 프로그램 실행 인자를 CamViewer 내부 실행 요청 모델로 변환한다.
             *
             * 인자가 없으면 DirectLaunch 요청이 생성되고,
             * --playback-time이 있으면 ExternalPlayback 요청이 생성된다.
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
             * 인증 및 설정 확인 중에는 PlayerPresenter가 아직 생성되지 않는다.
             * 따라서 최초 실행 요청을 메모리 저장소에 임시 보관한다.
             *
             * 이 인스턴스는 PlayerPresenter까지 동일하게 전달되어야 한다.
             */
            IApplicationLaunchRequestStore launchRequestStore =
                new ApplicationLaunchRequestStore();

            launchRequestStore.SetPendingRequest(
                launchRequest);

            /*
             * CamViewer 인증, 설정 API 및 로컬 설정 저장 기능을 제공하는
             * Client Facade를 생성한다.
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
             * 온라인 인증 이후 로컬 설정 확인,
             * 서버 설정 버전 확인 및 다운로드 정책을 담당한다.
             */
            ILandingStartupService landingStartupService =
                new LandingStartupService(
                    clientFacade);

            /*
             * NVR Provider DLL을 등록할 Catalog를 생성하고
             * providers 폴더의 Provider를 동적으로 불러온다.
             */
            var providerCatalog =
                new NvrProviderCatalog();

            LoadNvrProviders(
                providerCatalog);

            /*
             * 설정에 기록된 ProviderKey를 기준으로
             * 실제 NVR Provider 인스턴스를 생성하는 Factory이다.
             */
            var providerFactory =
                new NvrProviderFactory(
                    providerCatalog);

            /*
             * 설정 화면 표시, 설정 저장 및 서버 업로드 흐름을 담당한다.
             */
            var settingsFlowService =
                new SettingsFlowService(
                    clientFacade,
                    providerCatalog,
                    providerFactory,
                    environmentProvider);

            /*
             * 프로그램 시작 시 인증 상태와 설정 상태를 표시하는
             * Landing 화면을 생성한다.
             */
            var landingView =
                new LandingView();

            /*
             * LandingPresenter는 실행 인자 자체를 알 필요가 없다.
             *
             * 인증 및 설정 확인이 끝난 후 실행되는 openPlayerAction 람다가
             * launchRequestStore를 캡처하여 Player 생성 메서드로 전달한다.
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

            landingPresenter.Show();
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