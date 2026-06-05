using CamViewerClient.Validation;
using CamViewer.Helpers;
using CamViewer.Interfaces;
using CamViewer.Models.ViewModels;
using CamViewerClient.Models.Config;
using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

namespace CamViewer.Presenters
{
    /// <summary>
    /// NVR 등록/수정 팝업의 사용자 입력, 제조사 선택,
    /// 연결 테스트 및 저장 요청을 처리한다.
    ///
    /// 사용자는 제조사만 선택하며,
    /// 접속방식과 ProviderKey는 제조사에 따라 자동으로 결정된다.
    ///
    /// 신규 등록 시 NVR번호는 이 Presenter에서 지정하지 않고,
    /// SettingsPresenter에서 등록 순서에 따라 자동 지정한다.
    /// </summary>
    public sealed class NvrEditPresenter
    {
        private readonly INvrEditView _view;
        private readonly INvrProviderCatalog _providerCatalog;
        private readonly INvrProviderFactory _providerFactory;
        private readonly NvrConfig _editConfig;
        private readonly Action<NvrConfig> _onSaved;

        private List<VendorOptionItem> _vendorOptions;

        /// <summary>
        /// NVR 등록/수정 Presenter를 초기화한다.
        /// </summary>
        public NvrEditPresenter(
            INvrEditView view,
            INvrProviderCatalog providerCatalog,
            INvrProviderFactory providerFactory,
            NvrConfig editConfig,
            Action<NvrConfig> onSaved)
        {
            if (view == null)
            {
                throw new ArgumentNullException("view");
            }

            if (providerCatalog == null)
            {
                throw new ArgumentNullException("providerCatalog");
            }

            if (providerFactory == null)
            {
                throw new ArgumentNullException("providerFactory");
            }

            if (onSaved == null)
            {
                throw new ArgumentNullException("onSaved");
            }

            _view = view;
            _providerCatalog = providerCatalog;
            _providerFactory = providerFactory;
            _editConfig = ViewerConfigCloneHelper.Clone(editConfig);
            _onSaved = onSaved;

            _vendorOptions = new List<VendorOptionItem>();

            _view.LoadViewEvent += OnLoadView;
            _view.VendorChangedEvent += OnVendorChanged;
            _view.TestConnectionEvent += OnTestConnection;
            _view.SaveEvent += OnSave;
            _view.CloseEvent += OnClose;
        }

        /// <summary>
        /// NVR 등록/수정 팝업을 표시한다.
        /// </summary>
        public void Show()
        {
            _view.ShowView();
        }

        /// <summary>
        /// 팝업 화면의 제조사 목록과 초기값을 설정한다.
        /// </summary>
        private void OnLoadView(object sender, EventArgs e)
        {
            _vendorOptions = SettingsViewModelMapper
                .ToVendorOptionItems(_providerCatalog.GetAll())
                .ToList();

            _view.SetVendorOptions(_vendorOptions);

            if (_editConfig == null)
            {
                LoadCreateMode();
                return;
            }

            LoadEditMode();
        }

        /// <summary>
        /// 신규 등록 모드의 화면 초기값을 설정한다.
        /// </summary>
        private void LoadCreateMode()
        {
            _view.SetTitle("NVR 등록");
            _view.SetNvrNoDisplay("자동 지정");

            if (_vendorOptions.Count > 0)
            {
                _view.SelectedVendor = _vendorOptions[0].Vendor;
            }

            ApplySelectedVendorInfo();
        }

        /// <summary>
        /// 수정 모드의 화면 초기값을 설정한다.
        /// </summary>
        private void LoadEditMode()
        {
            _view.SetTitle("NVR 수정");
            _view.SetNvrNoDisplay(_editConfig.NvrNo.ToString());

            _view.SelectedVendor = _editConfig.Vendor;
            _view.Host = _editConfig.Host;
            _view.Port = _editConfig.Port;
            _view.ChannelCount = _editConfig.ChannelCount;
            _view.UserId = _editConfig.UserId;
            _view.Password = _editConfig.Password;

            _view.ProviderSettings =
                new Dictionary<string, string>(_editConfig.ProviderSettings);

            ApplySelectedVendorInfo();
        }

        /// <summary>
        /// 제조사 선택값이 변경되면 고정된 접속방식을 화면에 표시한다.
        /// </summary>
        private void OnVendorChanged(object sender, EventArgs e)
        {
            ApplySelectedVendorInfo();
        }

        /// <summary>
        /// 현재 선택된 제조사에 연결된 접속방식을 화면에 표시한다.
        /// </summary>
        private void ApplySelectedVendorInfo()
        {
            VendorOptionItem selectedOption = GetSelectedVendorOption();

            if (selectedOption == null)
            {
                _view.SetConnectionType(string.Empty);
                return;
            }

            _view.SetConnectionType(selectedOption.ConnectionType);
        }

        /// <summary>
        /// 현재 화면에서 선택된 제조사 옵션을 반환한다.
        /// </summary>
        private VendorOptionItem GetSelectedVendorOption()
        {
            string selectedVendor = _view.SelectedVendor;

            if (string.IsNullOrWhiteSpace(selectedVendor))
            {
                return null;
            }

            return _vendorOptions.FirstOrDefault(x =>
                string.Equals(
                    x.Vendor,
                    selectedVendor,
                    StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 현재 화면 입력값으로 NVR 연결 테스트를 수행한다.
        ///
        /// 연결 테스트는 설정 저장 여부와 관계없이 현재 입력값을 사용한다.
        /// 연결 테스트가 실패하더라도 설정 저장 자체는 허용한다.
        /// </summary>
        private async void OnTestConnection(object sender, EventArgs e)
        {
            NvrConfig nvrConfig = BuildConfigFromView();

            ConfigValidationResult validationResult =
                ValidateNvrConfig(nvrConfig);

            if (!validationResult.IsValid)
            {
                _view.ShowMessage(
                    BuildValidationMessage(validationResult));

                return;
            }

            NvrResult<INvrProvider> createResult =
                _providerFactory.Create(nvrConfig.ProviderKey);

            if (!createResult.Success || createResult.Data == null)
            {
                string message = string.IsNullOrWhiteSpace(createResult.Message)
                    ? "선택한 제조사의 NVR Provider를 찾을 수 없습니다."
                    : createResult.Message;

                _view.ShowMessage(message);
                return;
            }

            INvrProvider provider = createResult.Data;

            try
            {
                _view.SetConnectionTestState(
                    true,
                    "NVR 연결을 확인하고 있습니다.");

                NvrResult initializeResult = provider.Initialize();

                if (!initializeResult.Success)
                {
                    string message = string.IsNullOrWhiteSpace(
                        initializeResult.Message)
                        ? "NVR Provider 초기화에 실패했습니다."
                        : initializeResult.Message;

                    _view.ShowMessage(message);
                    return;
                }

                ProviderCapabilities capabilities =
                    provider.GetCapabilities();

                if (capabilities == null
                    || !capabilities.CanTestConnection)
                {
                    _view.ShowMessage(
                        "선택한 제조사의 연결 테스트 기능은 아직 지원되지 않습니다.");

                    return;
                }

                NvrConnectionInfo connectionInfo =
                    ToConnectionInfo(nvrConfig);

                NvrResult testResult =
                    await provider.TestConnectionAsync(
                        connectionInfo,
                        CancellationToken.None);

                if (testResult.Success)
                {
                    _view.ShowMessage(
                        "NVR 연결 테스트에 성공했습니다.");

                    return;
                }

                string testMessage = string.IsNullOrWhiteSpace(
                    testResult.Message)
                    ? "NVR 연결 테스트에 실패했습니다."
                    : testResult.Message;

                _view.ShowMessage(testMessage);
            }
            catch (Exception ex)
            {
                _view.ShowMessage(
                    "NVR 연결 테스트 중 오류가 발생했습니다."
                    + Environment.NewLine
                    + ex.Message);
            }
            finally
            {
                provider.Dispose();

                _view.SetConnectionTestState(
                    false,
                    string.Empty);
            }
        }

        /// <summary>
        /// 현재 입력값을 검증하고 NVR 설정을 상위 설정 화면에 전달한다.
        /// </summary>
        private void OnSave(object sender, EventArgs e)
        {
            NvrConfig nvrConfig = BuildConfigFromView();

            ConfigValidationResult validationResult =
                ValidateNvrConfig(nvrConfig);

            if (!validationResult.IsValid)
            {
                _view.ShowMessage(
                    BuildValidationMessage(validationResult));

                return;
            }

            _onSaved(nvrConfig);
            _view.CloseView();
        }

        /// <summary>
        /// NVR 등록/수정 팝업을 닫는다.
        /// </summary>
        private void OnClose(object sender, EventArgs e)
        {
            _view.CloseView();
        }

        /// <summary>
        /// 현재 화면 입력값으로 NVR 설정 모델을 생성한다.
        ///
        /// 신규 등록 시 NvrNo는 0으로 전달하며,
        /// SettingsPresenter에서 실제 번호를 자동 지정한다.
        /// 수정 시에는 기존 NVR번호를 유지한다.
        /// </summary>
        private NvrConfig BuildConfigFromView()
        {
            VendorOptionItem selectedOption =
                GetSelectedVendorOption();

            var config = new NvrConfig
            {
                NvrNo = _editConfig == null
                    ? 0
                    : _editConfig.NvrNo,

                Vendor = selectedOption == null
                    ? string.Empty
                    : selectedOption.Vendor,

                ConnectionType = selectedOption == null
                    ? string.Empty
                    : selectedOption.ConnectionType,

                ProviderKey = selectedOption == null
                    ? string.Empty
                    : selectedOption.ProviderKey,

                Host = _view.Host,
                Port = _view.Port ?? 0,
                ChannelCount = _view.ChannelCount ?? 0,
                UserId = _view.UserId,
                Password = _view.Password
            };

            IDictionary<string, string> providerSettings =
                _view.ProviderSettings;

            if (providerSettings != null)
            {
                foreach (KeyValuePair<string, string> item
                    in providerSettings)
                {
                    config.ProviderSettings[item.Key] = item.Value;
                }
            }

            return config;
        }

        /// <summary>
        /// 개별 NVR 설정을 전체 설정 검증 서비스를 사용하여 검증한다.
        ///
        /// 신규 등록 시 NVR번호는 아직 자동 지정 전이므로,
        /// 검증을 위해 임시 NVR번호 1을 사용한다.
        /// </summary>
        private static ConfigValidationResult ValidateNvrConfig(
            NvrConfig nvrConfig)
        {
            NvrConfig validationTarget =
                ViewerConfigCloneHelper.Clone(nvrConfig);

            if (validationTarget.NvrNo <= 0)
            {
                validationTarget.NvrNo = 1;
            }

            var tempConfig = new ViewerConfig
            {
                StoreCode = 1
            };

            tempConfig.NvrList.Add(validationTarget);

            ConfigValidationResult fullResult =
                new ViewerConfigValidator().Validate(tempConfig);

            var result = new ConfigValidationResult();

            foreach (ConfigValidationError error in fullResult.Errors
                .Where(x => x.TargetType == "NvrConfig"))
            {
                result.Errors.Add(error);
            }

            return result;
        }

        /// <summary>
        /// NVR 설정을 NVR Core의 접속 정보 모델로 변환한다.
        /// </summary>
        private static NvrConnectionInfo ToConnectionInfo(
            NvrConfig source)
        {
            NvrConnectionType connectionType;

            bool parsed = Enum.TryParse(
                source.ConnectionType,
                true,
                out connectionType);

            if (!parsed)
            {
                throw new InvalidOperationException(
                    "NVR 접속방식 값을 변환할 수 없습니다."
                    + Environment.NewLine
                    + "접속방식: "
                    + source.ConnectionType);
            }

            var target = new NvrConnectionInfo
            {
                NvrNo = source.NvrNo,
                ProviderKey = source.ProviderKey,
                Vendor = source.Vendor,
                ConnectionType = connectionType,
                Host = source.Host,
                Port = source.Port,
                UserId = source.UserId,
                Password = source.Password,
                ChannelCount = source.ChannelCount
            };

            if (source.ProviderSettings != null)
            {
                foreach (KeyValuePair<string, string> item
                    in source.ProviderSettings)
                {
                    target.ProviderSettings[item.Key] = item.Value;
                }
            }

            return target;
        }

        /// <summary>
        /// 검증 오류 목록을 사용자 표시 메시지로 변환한다.
        /// </summary>
        private static string BuildValidationMessage(
            ConfigValidationResult result)
        {
            return string.Join(
                Environment.NewLine,
                result.Errors.Select(x => "- " + x.Message));
        }
    }
}