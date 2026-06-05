using CamViewer.Factories;
using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Presenters;
using CamViewer.Views;
using CamViewerClient;
using CamViewerClient.Models.Config;
using CamViewerClient.Results;
using System;
using System.Windows.Forms;

namespace CamViewer.Services
{
    /// <summary>
    /// 캠뷰어 설정 화면 실행 흐름을 담당한다.
    ///
    /// 역할:
    /// - 로컬 설정 불러오기
    /// - 설정 화면 실행
    /// - 설정 저장 시 viewer_config.dat 저장
    ///
    /// 서버 업로드 여부 확인 및 api/config/sync 호출은 다음 단계에서 추가한다.
    /// </summary>
    public sealed class SettingsFlowService
    {
        private readonly CamViewerClientFacade _clientFacade;
        private readonly INvrProviderCatalog _providerCatalog;
        private readonly INvrProviderFactory _providerFactory;

        /// <summary>
        /// SettingsFlowService를 초기화한다.
        /// </summary>
        public SettingsFlowService(
            CamViewerClientFacade clientFacade,
            INvrProviderCatalog providerCatalog,
            INvrProviderFactory providerFactory)
        {
            if (clientFacade == null)
            {
                throw new ArgumentNullException("clientFacade");
            }

            if (providerCatalog == null)
            {
                throw new ArgumentNullException("providerCatalog");
            }

            if (providerFactory == null)
            {
                throw new ArgumentNullException("providerFactory");
            }

            _clientFacade = clientFacade;
            _providerCatalog = providerCatalog;
            _providerFactory = providerFactory;
        }

        /// <summary>
        /// 설정 화면을 실행한다.
        /// </summary>
        public void OpenSettings()
        {
            ViewerConfig viewerConfig =
                LoadOrCreateConfig();

            var settingsView = new SettingsView();
            var settingsViewFactory = new SettingsViewFactory();

            var presenter = new SettingsPresenter(
                settingsView,
                settingsViewFactory,
                _providerCatalog,
                _providerFactory,
                viewerConfig,
                SaveConfig);

            presenter.Show();
        }

        /// <summary>
        /// 로컬 설정 파일이 있으면 불러오고, 없으면 기본 설정을 생성한다.
        /// </summary>
        private ViewerConfig LoadOrCreateConfig()
        {
            if (!_clientFacade.HasLocalConfig())
            {
                return _clientFacade.CreateDefaultConfig();
            }

            ClientResult<ViewerConfig> loadResult =
                _clientFacade.LoadLocalConfig();

            if (loadResult.Success && loadResult.Data != null)
            {
                return loadResult.Data;
            }

            MessageBox.Show(
                "로컬 설정 정보를 불러오지 못했습니다."
                + Environment.NewLine
                + loadResult.Message
                + Environment.NewLine
                + "새 설정으로 시작합니다.",
                "POSCAM 캠뷰어 설정",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return _clientFacade.CreateDefaultConfig();
        }

        /// <summary>
        /// 설정 화면에서 저장 요청한 ViewerConfig를 로컬 파일에 저장한다.
        /// </summary>
        private void SaveConfig(ViewerConfig savedConfig)
        {
            ClientResult saveResult =
                _clientFacade.SaveLocalConfig(savedConfig);

            if (!saveResult.Success)
            {
                MessageBox.Show(
                    saveResult.Message,
                    "POSCAM 캠뷰어 설정 저장 실패",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            MessageBox.Show(
                "캠뷰어 설정이 저장되었습니다.",
                "POSCAM 캠뷰어 설정",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}