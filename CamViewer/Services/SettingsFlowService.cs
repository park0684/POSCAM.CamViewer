using CamViewer.Factories;
using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Presenters;
using CamViewer.Views;
using CamViewerClient;
using CamViewerClient.Models.Config;
using CamViewerClient.Models.Auth;
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
    /// - 설정 저장 성공 여부 반환
    ///
    /// 서버 업로드 여부 확인 및 api/config/sync 호출은 다음 단계에서 추가한다.
    /// </summary>
    public sealed class SettingsFlowService
    {
        private readonly CamViewerClientFacade _clientFacade;
        private readonly INvrProviderCatalog _providerCatalog;
        private readonly INvrProviderFactory _providerFactory;
        private readonly IClientEnvironmentProvider _environmentProvider;
        private bool _saved;

        /// <summary>
        /// SettingsFlowService를 초기화한다.
        /// </summary>
        public SettingsFlowService(
            CamViewerClientFacade clientFacade,
            INvrProviderCatalog providerCatalog,
            INvrProviderFactory providerFactory,
            IClientEnvironmentProvider environmentProvider)
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

            if (environmentProvider == null)
            {
                throw new ArgumentNullException("environmentProvider");
            }

            _clientFacade = clientFacade;
            _providerCatalog = providerCatalog;
            _providerFactory = providerFactory;
            _environmentProvider = environmentProvider;
        }

        /// <summary>
        /// 설정 화면을 실행하고 저장 성공 여부를 반환한다.
        /// </summary>
        /// <returns>설정 저장에 성공했으면 true.</returns>
        public bool OpenSettings()
        {
            _saved = false;

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
                SaveConfigAsync);

            presenter.Show();

            return _saved;
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
        /// 설정 화면에서 저장 요청한 ViewerConfig를 로컬 파일에 저장하고,
        /// 사용자가 동의하면 인증서버에도 동기화한다.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> SaveConfigAsync(
            ViewerConfig savedConfig)
        {
            ClientResult localSaveResult =
                _clientFacade.SaveLocalConfig(savedConfig);

            if (!localSaveResult.Success)
            {
                MessageBox.Show(
                    localSaveResult.Message,
                    "POSCAM 캠뷰어 설정 저장 실패",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            bool uploadToServer =
                MessageBox.Show(
                    "캠뷰어 설정이 로컬에 저장되었습니다."
                    + Environment.NewLine
                    + "이 설정을 인증서버에도 등록하시겠습니까?",
                    "POSCAM 캠뷰어 설정",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes;

            if (!uploadToServer)
            {
                _saved = true;

                MessageBox.Show(
                    "캠뷰어 설정이 로컬에 저장되었습니다.",
                    "POSCAM 캠뷰어 설정",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return true;
            }

            bool synced =
                await SyncConfigToServerAsync(savedConfig);

            _saved = true;

            return synced;
        }


        /// <summary>
        /// 설정 화면에서 저장 요청한 ViewerConfig를 로컬 파일에 저장하고,
        /// 사용자가 동의하면 인증서버에도 동기화한다.
        /// </summary>
        private async void SaveConfig(ViewerConfig savedConfig)
        {
            ClientResult localSaveResult =
                _clientFacade.SaveLocalConfig(savedConfig);

            if (!localSaveResult.Success)
            {
                MessageBox.Show(
                    localSaveResult.Message,
                    "POSCAM 캠뷰어 설정 저장 실패",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            bool uploadToServer =
                MessageBox.Show(
                    "캠뷰어 설정이 로컬에 저장되었습니다."
                    + Environment.NewLine
                    + "이 설정을 인증서버에도 등록하시겠습니까?",
                    "POSCAM 캠뷰어 설정",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes;

            if (!uploadToServer)
            {
                _saved = true;

                MessageBox.Show(
                    "캠뷰어 설정이 로컬에 저장되었습니다.",
                    "POSCAM 캠뷰어 설정",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            await SyncConfigToServerAsync(savedConfig);
        }

        /// <summary>
        /// 로컬 설정을 인증서버에 동기화한다.
        ///
        /// 서버 동기화 요청에는 토큰, HWID, 수정자, 프로그램 버전이 필요하다.
        /// 현재 수정자는 캠뷰어 장비명으로 전달한다.
        /// 서버 동기화에 실패해도 로컬 저장은 완료된 상태이므로 true를 반환한다.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> SyncConfigToServerAsync(
            ViewerConfig savedConfig)
        {
            ClientResult<ViewerAuthToken> tokenResult =
                _clientFacade.LoadLocalToken();

            if (!tokenResult.Success || tokenResult.Data == null)
            {
                MessageBox.Show(
                    "로컬 설정은 저장되었지만 인증 토큰을 불러오지 못해 서버 등록은 진행하지 못했습니다."
                    + Environment.NewLine
                    + tokenResult.Message,
                    "POSCAM 캠뷰어 설정",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return true;
            }

            string hwid =
                _environmentProvider.GetHwid();

            string deviceName =
                _environmentProvider.GetDeviceName();

            string programVersion =
                _environmentProvider.GetProgramVersion();

            ClientResult<ViewerConfig> syncResult =
                await _clientFacade.SyncServerConfigAsync(
                    tokenResult.Data.Token,
                    hwid,
                    deviceName,
                    programVersion,
                    savedConfig,
                    System.Threading.CancellationToken.None);

            if (!syncResult.Success)
            {
                MessageBox.Show(
                    "로컬 설정은 저장되었지만 서버 등록에 실패했습니다."
                    + Environment.NewLine
                    + syncResult.Message,
                    "POSCAM 캠뷰어 설정",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return true;
            }

            ViewerConfig syncedConfig =
                syncResult.Data ?? savedConfig;

            ClientResult resaveResult =
                _clientFacade.SaveLocalConfig(syncedConfig);

            if (!resaveResult.Success)
            {
                MessageBox.Show(
                    "서버 등록은 완료되었지만 서버 버전이 반영된 설정을 로컬에 다시 저장하지 못했습니다."
                    + Environment.NewLine
                    + resaveResult.Message,
                    "POSCAM 캠뷰어 설정",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return true;
            }

            MessageBox.Show(
                "캠뷰어 설정이 로컬과 인증서버에 저장되었습니다.",
                "POSCAM 캠뷰어 설정",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return true;
        }
    }
}