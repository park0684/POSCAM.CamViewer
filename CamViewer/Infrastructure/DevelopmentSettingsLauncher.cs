using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CamViewer.Factories;
using CamViewer.Presenters;
using CamViewer.Views;
using CamViewerClient;
using CamViewerClient.Models.Config;
using CamViewerClient.Results;
using CamViewer.Nvr.Core.Providers;
using CamViewer.Nvr.Core.Results;

namespace CamViewer.Infrastructure
{
    /// <summary>
    /// 설정 화면 테스트 실행을 위한 임시 런처이다.
    ///
    /// 역할:
    /// - NVR Provider DLL 로드
    /// - 로컬 viewer_config.dat 설정 파일 로드
    /// - 설정 화면 실행
    /// - 설정 저장 시 viewer_config.dat에 암호화 저장
    ///
    /// 랜딩페이지, 로그인, 인증 흐름이 구현되면
    /// 이 클래스의 역할은 실제 부트스트랩 서비스로 이동한다.
    /// </summary>
    public static class DevelopmentSettingsLauncher
    {
        /// <summary>
        /// 설정 화면 테스트 흐름을 실행한다.
        /// </summary>
        public static void Run()
        {
            var providerCatalog = new NvrProviderCatalog();

            LoadProviders(providerCatalog);

            var providerFactory =
                new NvrProviderFactory(providerCatalog);

            var clientFacade =
                new CamViewerClientFacade();

            ViewerConfig viewerConfig =
                LoadOrCreateConfig(clientFacade);

            var settingsView = new SettingsView();
            var settingsViewFactory = new SettingsViewFactory();

            var settingsPresenter = new SettingsPresenter(
                settingsView,
                settingsViewFactory,
                providerCatalog,
                providerFactory,
                viewerConfig,
                savedConfig =>
                {
                    ClientResult saveResult =
                        clientFacade.SaveLocalConfig(savedConfig);

                    if (!saveResult.Success)
                    {
                        MessageBox.Show(
                            saveResult.Message,
                            "캠뷰어 설정 저장 실패",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        return;
                    }

                    viewerConfig = savedConfig;

                    MessageBox.Show(
                        "캠뷰어 설정이 저장되었습니다."
                        + Environment.NewLine
                        + clientFacade.GetLocalConfigFilePath(),
                        "캠뷰어 설정",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                });

            settingsPresenter.Show();
        }

        /// <summary>
        /// 실행 폴더의 providers 하위 DLL을 검색하여 Provider Catalog에 등록한다.
        /// </summary>
        private static void LoadProviders(
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

            string message =
                BuildProviderLoadMessage(
                    providerPath,
                    loadReport);

            MessageBox.Show(
                message,
                "NVR Provider 로드 확인",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        /// <summary>
        /// 로컬 설정 파일이 있으면 불러오고,
        /// 없거나 불러오기에 실패하면 신규 설정을 생성한다.
        /// </summary>
        private static ViewerConfig LoadOrCreateConfig(
            CamViewerClientFacade clientFacade)
        {
            if (!clientFacade.HasLocalConfig())
            {
                return clientFacade.CreateDefaultConfig();
            }

            ClientResult<ViewerConfig> loadResult =
                clientFacade.LoadLocalConfig();

            if (loadResult.Success && loadResult.Data != null)
            {
                return loadResult.Data;
            }

            MessageBox.Show(
                "로컬 설정 파일을 불러오지 못했습니다."
                + Environment.NewLine
                + loadResult.Message
                + Environment.NewLine
                + "신규 설정으로 시작합니다.",
                "캠뷰어 설정",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return clientFacade.CreateDefaultConfig();
        }

        /// <summary>
        /// Provider 로드 결과를 사용자 확인용 메시지로 변환한다.
        /// </summary>
        private static string BuildProviderLoadMessage(
            string providerPath,
            ProviderLoadReport loadReport)
        {
            string message =
                "사용 가능한 NVR Provider를 불러오지 못했거나 일부 오류가 있습니다."
                + Environment.NewLine
                + Environment.NewLine
                + "Provider 검색 경로:"
                + Environment.NewLine
                + providerPath
                + Environment.NewLine
                + Environment.NewLine
                + "등록된 Provider 수: "
                + loadReport.LoadedCount
                + Environment.NewLine
                + "로드 오류 수: "
                + loadReport.Errors.Count;

            if (Directory.Exists(providerPath))
            {
                string[] providerFiles = Directory.GetFiles(
                    providerPath,
                    "POSCAM.Nvr.*.dll",
                    SearchOption.AllDirectories);

                message += Environment.NewLine
                    + Environment.NewLine
                    + "검색된 DLL 수: "
                    + providerFiles.Length;

                foreach (string file in providerFiles)
                {
                    message += Environment.NewLine + "- " + file;
                }
            }
            else
            {
                message += Environment.NewLine
                    + Environment.NewLine
                    + "providers 폴더가 없습니다.";
            }

            if (loadReport.Errors.Count > 0)
            {
                message += Environment.NewLine
                    + Environment.NewLine
                    + "로드 오류:";

                foreach (ProviderLoadError error in loadReport.Errors)
                {
                    message += Environment.NewLine
                        + "- " + error.Message
                        + Environment.NewLine
                        + "  파일: " + error.AssemblyPath
                        + Environment.NewLine
                        + "  형식: " + error.TypeName;
                }
            }

            return message;
        }
    }
}
