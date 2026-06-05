using CamViewer.Factories;
using CamViewer.Infrastructure;
using CamViewer.Nvr.Core.Providers;
using CamViewer.Presenters;
using CamViewer.Services;
using CamViewer.Views;
using CamViewerClient;
using CamViewerClient.Models.Config;
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
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);

            //// Provider 등록정보를 관리한다.
            //var providerCatalog = new NvrProviderCatalog();

            //// providers 폴더의 Provider DLL을 검색하고 등록한다.
            //var providerLoader = new ProviderAssemblyLoader();
            //var providerBootstrapper =
            //    new NvrProviderBootstrapper(
            //        providerLoader,
            //        providerCatalog);

            //string providerPath = Path.Combine(
            //    AppDomain.CurrentDomain.BaseDirectory,
            //    "providers");

            //providerBootstrapper.LoadAndRegister(providerPath);

            //// ProviderKey를 기준으로 Provider 인스턴스를 생성한다.
            //var providerFactory =
            //    new NvrProviderFactory(providerCatalog);

            //// 현재는 서버/로컬 설정 로드 전이므로 임시 설정을 사용한다.
            //var viewerConfig = new ViewerConfig
            //{
            //    StoreCode = 1,
            //    ConfigVersion = 0,
            //    NextNvrNo = 1
            //};

            //var settingsView = new SettingsView();
            //var settingsViewFactory = new SettingsViewFactory();

            //var settingsPresenter = new SettingsPresenter(
            //    settingsView,
            //    settingsViewFactory,
            //    providerCatalog,
            //    providerFactory,
            //    viewerConfig,
            //    savedConfig =>
            //    {
            //        MessageBox.Show(
            //            "설정 저장 요청이 정상적으로 전달되었습니다.",
            //            "캠뷰어 설정",
            //            MessageBoxButtons.OK,
            //            MessageBoxIcon.Information);
            //    });

            //// Application.Run(new CounterEditView())를 사용하지 않는다.
            //settingsPresenter.Show();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var loginView = new LoginView();

            var presenter = new LoginPresenter(
                loginView,
                new CamViewerClientFacade(),
                new ClientEnvironmentProvider());

            bool loginSuccess = presenter.ShowDialog();

            MessageBox.Show(
                loginSuccess
                    ? "로그인 성공"
                    : "로그인 취소");

        }
    }
}