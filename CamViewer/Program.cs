using CamViewer.Interfaces;
using CamViewer.Nvr.Core.Providers;
using CamViewer.Nvr.Core.Results;
using CamViewer.Presenters;
using CamViewer.Services;
using CamViewer.Views;
using CamViewerClient;
using System;
using System.IO;
using System.Windows.Forms;

namespace CamViewer
{
    internal static class Program
    {
        /// <summary>
        /// 애플리케이션의 주 진입점.
        /// </summary>
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
                    providerFactory);

            var landingView = new LandingView();

            var landingPresenter = new LandingPresenter(
                landingView,
                clientFacade,
                environmentProvider,
                CreateLoginView,
                settingsFlowService.OpenSettings,
                OpenMainTemporary);

            landingPresenter.Show();
        }

        /// <summary>
        /// 로그인 View를 생성한다.
        /// </summary>
        private static ILoginView CreateLoginView()
        {
            return new LoginView();
        }

        /// <summary>
        /// 실행 폴더의 providers 하위 DLL을 검색하여 NVR Provider를 등록한다.
        /// </summary>
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
        /// 메인 화면을 임시로 실행한다.
        /// 이후 MainView 또는 PlaybackView로 교체한다.
        /// </summary>
        private static void OpenMainTemporary()
        {
            MessageBox.Show(
                "메인 화면은 다음 단계에서 연결합니다.",
                "POSCAM 캠뷰어",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}