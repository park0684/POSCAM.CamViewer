using CamViewer.Factories;
using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Presenters;
using CamViewer.Views;
using CamViewerClient;
using CamViewerClient.Enums;
using CamViewerClient.Models.Auth;
using CamViewerClient.Models.Config;
using CamViewerClient.Results;
using System;
using System.Threading;
using System.Threading.Tasks;
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
    /// - 사용자 확인 후 AuthServer api/config/sync로 서버 업로드
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
        /// 설정 화면을 모달로 실행한다.
        /// 설정 저장에 성공하면 true를 반환한다.
        /// </summary>
        /// <returns>설정 저장에 성공했으면 true.</returns>
        public bool OpenSettings()
        {
            _saved = false;

            ViewerConfig viewerConfig =
                LoadOrCreateConfig();

            var settingsView =
                new SettingsView();

            var settingsViewFactory =
                new SettingsViewFactory();

            var presenter =
                new SettingsPresenter(
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
        /// 로컬 설정 파일이 있으면 불러오고,
        /// 없으면 현재 인증 토큰의 매장코드를 반영한 기본 설정을 생성한다.
        /// </summary>
        private ViewerConfig LoadOrCreateConfig()
        {
            if (!_clientFacade.HasLocalConfig())
            {
                return CreateDefaultConfigFromToken();
            }

            ClientResult<ViewerConfig> loadResult =
                _clientFacade.LoadLocalConfig();

            if (loadResult.Success && loadResult.Data != null)
            {
                EnsureStoreCodeFromToken(
                    loadResult.Data);

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

            return CreateDefaultConfigFromToken();
        }

        /// <summary>
        /// 설정 화면에서 저장 요청한 ViewerConfig를 로컬 파일에 저장하고,
        /// 사용자가 원하면 AuthServer에 업로드한다.
        /// </summary>
        /// <param name="savedConfig">설정 화면에서 저장 요청한 설정.</param>
        /// <returns>설정 화면을 닫아도 되는 저장 성공 여부.</returns>
        private async Task<bool> SaveConfigAsync(
            ViewerConfig savedConfig)
        {
            if (savedConfig == null)
            {
                MessageBox.Show(
                    "저장할 캠뷰어 설정정보가 없습니다.",
                    "POSCAM 캠뷰어 설정 저장 실패",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            /*
             * 먼저 로컬 설정 파일에 저장한다.
             * 서버 업로드 실패가 발생해도 최소한 로컬 설정은 보존되어야 한다.
             */
            savedConfig.SyncStatus =
                ViewerConfigSyncStatus.LocalModified;

            ClientResult saveResult =
                _clientFacade.SaveLocalConfig(
                    savedConfig);

            if (!saveResult.Success)
            {
                MessageBox.Show(
                    saveResult.Message,
                    "POSCAM 캠뷰어 설정 저장 실패",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            bool uploadConfirm =
                MessageBox.Show(
                    "캠뷰어 설정이 로컬에 저장되었습니다."
                    + Environment.NewLine
                    + "서버에도 설정을 업로드하시겠습니까?",
                    "POSCAM 캠뷰어 설정",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes;

            if (!uploadConfirm)
            {
                MessageBox.Show(
                    "캠뷰어 설정이 로컬에 저장되었습니다.",
                    "POSCAM 캠뷰어 설정",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                _saved = true;
                return true;
            }

            ClientResult<ViewerAuthToken> tokenResult =
                _clientFacade.LoadLocalToken();

            if (!tokenResult.Success
                || tokenResult.Data == null
                || string.IsNullOrWhiteSpace(tokenResult.Data.Token))
            {
                MessageBox.Show(
                    "로컬 설정은 저장되었지만 서버 업로드는 진행할 수 없습니다."
                    + Environment.NewLine
                    + "캠뷰어 인증 정보가 없습니다."
                    + Environment.NewLine
                    + tokenResult.Message,
                    "POSCAM 캠뷰어 설정",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _saved = true;
                return true;
            }

            string hwid =
                _environmentProvider.GetHwid();

            string programVersion =
                _environmentProvider.GetProgramVersion();

            string modifiedBy =
                Environment.UserName;

            ClientResult<ViewerConfig> syncResult =
                await _clientFacade.SyncServerConfigAsync(
                    tokenResult.Data.Token,
                    hwid,
                    modifiedBy,
                    programVersion,
                    savedConfig,
                    CancellationToken.None);

            if (!syncResult.Success || syncResult.Data == null)
            {
                MessageBox.Show(
                    "로컬 설정은 저장되었지만 서버 업로드에 실패했습니다."
                    + Environment.NewLine
                    + syncResult.Message,
                    "POSCAM 캠뷰어 설정",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _saved = true;
                return true;
            }

            /*
             * 서버 업로드 성공 시 AuthServer가 반환한 ConfigVersion을
             * 로컬 설정에도 다시 저장한다.
             */
            ClientResult saveSyncedResult =
                _clientFacade.SaveLocalConfig(
                    syncResult.Data);

            if (!saveSyncedResult.Success)
            {
                MessageBox.Show(
                    "서버 업로드는 성공했지만 동기화된 설정을 로컬에 다시 저장하지 못했습니다."
                    + Environment.NewLine
                    + saveSyncedResult.Message,
                    "POSCAM 캠뷰어 설정",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _saved = true;
                return true;
            }

            MessageBox.Show(
                "캠뷰어 설정이 로컬과 서버에 저장되었습니다.",
                "POSCAM 캠뷰어 설정",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            _saved = true;
            return true;
        }

        /// <summary>
        /// 현재 인증 토큰의 매장코드를 반영한 기본 설정을 생성한다.
        /// 
        /// 첫 로그인 후 로컬 설정 파일이 없는 경우,
        /// 사용자가 매장코드를 직접 입력하지 않으므로
        /// 로그인 토큰의 StoreCode를 ViewerConfig에 반영한다.
        /// </summary>
        private ViewerConfig CreateDefaultConfigFromToken()
        {
            ViewerConfig config =
                _clientFacade.CreateDefaultConfig();

            EnsureStoreCodeFromToken(
                config);

            return config;
        }

        /// <summary>
        /// ViewerConfig의 StoreCode가 비어 있으면
        /// 로컬 인증 토큰의 StoreCode로 보정한다.
        /// </summary>
        /// <param name="config">보정할 캠뷰어 설정.</param>
        private void EnsureStoreCodeFromToken(
            ViewerConfig config)
        {
            if (config == null)
            {
                return;
            }

            if (config.StoreCode > 0)
            {
                return;
            }

            ClientResult<ViewerAuthToken> tokenResult =
                _clientFacade.LoadLocalToken();

            if (!tokenResult.Success || tokenResult.Data == null)
            {
                return;
            }

            if (tokenResult.Data.StoreCode <= 0)
            {
                return;
            }

            config.StoreCode =
                tokenResult.Data.StoreCode;
        }
    }
}