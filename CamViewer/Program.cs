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
    }
}