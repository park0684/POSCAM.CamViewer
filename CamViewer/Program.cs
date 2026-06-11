using CamViewer.Interfaces;
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
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace CamViewer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var clientFacade = new CamViewerClientFacade();
            var environmentProvider = new ClientEnvironmentProvider();
            ILandingStartupService landingStartup = new LandingStartupService(clientFacade);
            var providerCatalog = new NvrProviderCatalog();
            
            LoadNvrProviders(providerCatalog);

            var providerFactory =
                new NvrProviderFactory(providerCatalog);

            var settingsFlowService =
                new SettingsFlowService(
                    clientFacade,
                    providerCatalog,
                    providerFactory,
                    environmentProvider);

            var landingView = new LandingView();

            var landingPresenter = new LandingPresenter(
                landingView,
                clientFacade,
                landingStartup,
                environmentProvider,
                CreateLoginView,
                settingsFlowService.OpenSettings,
                () => OpenPlayerTemporary(
                    clientFacade,
                    settingsFlowService,
                    providerFactory));

            landingPresenter.Show();
        }

        private static ILoginView CreateLoginView()
        {
            return new LoginView();
        }

        private static void LoadNvrProviders(
            NvrProviderCatalog providerCatalog)
        {
            string providerPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "providers");

            var providerLoader = new ProviderAssemblyLoader();

            var providerBootstrapper =
                new NvrProviderBootstrapper(
                    providerLoader,
                    providerCatalog);

            ProviderLoadReport loadReport =
                providerBootstrapper.LoadAndRegister(providerPath);

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
        /// PlayerView를 실행한다.
        /// 현재 단계에서는 실제 NVR 재생 전이며,
        /// 로컬 설정 기반 화면 표시와 버튼 이벤트만 확인한다.
        /// </summary>
        private static void OpenPlayerTemporary(
            CamViewerClientFacade clientFacade,
            SettingsFlowService settingsFlowService,
            INvrProviderFactory providerFactory)
        {
            ClientResult<ViewerConfig> configResult =
                clientFacade.LoadLocalConfig();

            if (!configResult.Success || configResult.Data == null)
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

            var playerView = new PlayerView();

            playerView.FormClosed += (sender, e) =>
            {
                Application.Exit();
            };
            var playerPresenter = new PlayerPresenter(
                playerView,
                configResult.Data,
                new NvrPlayerPlaybackService(providerFactory),
                settingsFlowService.OpenSettings,
                () =>
                {
                    ClientResult<ViewerConfig> reloadResult =
                        clientFacade.LoadLocalConfig();

                    return reloadResult.Success
                        ? reloadResult.Data
                        : null;
                });

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

                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                string logPath =
                    Path.Combine(
                        logDirectory,
                        "camviewer_debug.log");

                string startMessage =
                    Environment.NewLine
                    + "=================================================="
                    + Environment.NewLine
                    + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
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
                // 디버그 로그 실패는 프로그램 실행을 막지 않는다.
            }
        }

        /// <summary>
        /// CamViewer 디버그 로그를 파일에 직접 기록한다.
        /// 
        /// 기본 빌드에서는 호출 코드가 컴파일 결과에서 제외된다.
        /// CAMVIEWER_DEBUG_LOG 컴파일 심볼을 켰을 때만 동작한다.
        /// </summary>
        [Conditional("CAMVIEWER_DEBUG_LOG")]
        internal static void WriteDebugLog(string message)
        {
            try
            {
                string logDirectory =
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "logs");

                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                string logPath =
                    Path.Combine(
                        logDirectory,
                        "camviewer_debug.log");

                string line =
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
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
                // 디버그 로그 실패는 프로그램 실행을 막지 않는다.
            }
        }
    }
}