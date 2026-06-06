using System;
using System.Linq;
using System.Threading.Tasks;
using CamViewerClient.Validation;
using CamViewer.Factories;
using CamViewer.Helpers;
using CamViewer.Interfaces;
using CamViewer.Models.ViewModels;
using CamViewerClient.Enums;
using CamViewerClient.Models.Config;
using CamViewer.Nvr.Core.Abstractions;

namespace CamViewer.Presenters
{
    /// <summary>
    /// 캠뷰어 설정 화면의 NVR 및 계산대 등록정보를 관리한다.
    ///
    /// 설정 화면에서 작업하는 동안에는 원본 설정을 직접 수정하지 않고
    /// 복사된 작업 설정을 사용한다.
    ///
    /// 실제 로컬 설정 파일 저장과 서버 업로드는
    /// 상위 흐름 서비스에서 처리한다.
    /// </summary>
    public sealed class SettingsPresenter
    {
        private readonly ISettingsView _view;
        private readonly ISettingsViewFactory _viewFactory;
        private readonly INvrProviderCatalog _providerCatalog;
        private readonly INvrProviderFactory _providerFactory;
        //private readonly Action<ViewerConfig> _onSaveRequested;

        private readonly Func<ViewerConfig, Task<bool>> _onSaveRequested;
        private readonly ViewerConfigValidator _validator;

        private readonly ViewerConfig _workingConfig;

        /// <summary>
        /// 설정 화면 Presenter를 초기화한다.
        /// </summary>
        /// <param name="view">설정 화면 View.</param>
        /// <param name="viewFactory">NVR 및 계산대 팝업 View 생성 Factory.</param>
        /// <param name="providerCatalog">등록된 NVR Provider 목록.</param>
        /// <param name="providerFactory">NVR Provider 생성 Factory.</param>
        /// <param name="viewerConfig">현재 캠뷰어 설정.</param>
        /// <param name="onSaveRequested">
        /// 설정 검증 성공 후 저장할 설정을 전달하는 콜백.
        /// </param>
        public SettingsPresenter(
            ISettingsView view,
            ISettingsViewFactory viewFactory,
            INvrProviderCatalog providerCatalog,
            INvrProviderFactory providerFactory,
            ViewerConfig viewerConfig,
            Func<ViewerConfig, Task<bool>> onSaveRequested)
        {
            if (view == null)
            {
                throw new ArgumentNullException("view");
            }

            if (viewFactory == null)
            {
                throw new ArgumentNullException("viewFactory");
            }

            if (providerCatalog == null)
            {
                throw new ArgumentNullException("providerCatalog");
            }

            if (providerFactory == null)
            {
                throw new ArgumentNullException("providerFactory");
            }

            if (onSaveRequested == null)
            {
                throw new ArgumentNullException("onSaveRequested");
            }

            _view = view;
            _viewFactory = viewFactory;
            _providerCatalog = providerCatalog;
            _providerFactory = providerFactory;
            _onSaveRequested = onSaveRequested;

            _workingConfig = ViewerConfigCloneHelper.Clone(viewerConfig);
            _validator = new ViewerConfigValidator();

            InitializeNextNvrNo();

            _view.LoadViewEvent += OnLoadView;
            _view.AddNvrEvent += OnAddNvr;
            _view.EditNvrEvent += OnEditNvr;
            _view.DeleteNvrEvent += OnDeleteNvr;
            _view.AddCounterMapEvent += OnAddCounterMap;
            _view.EditCounterMapEvent += OnEditCounterMap;
            _view.DeleteCounterMapEvent += OnDeleteCounterMap;
            _view.SaveEvent += OnSave;
            _view.CloseEvent += OnClose;
        }

        /// <summary>
        /// 설정 화면을 표시한다.
        /// </summary>
        public void Show()
        {
            _view.ShowView();
        }

        /// <summary>
        /// 설정 화면이 표시되면 목록과 설정 상태를 갱신한다.
        /// </summary>
        private void OnLoadView(object sender, EventArgs e)
        {
            RefreshView();
        }

        /// <summary>
        /// 신규 NVR 등록 팝업을 표시한다.
        /// </summary>
        private void OnAddNvr(object sender, EventArgs e)
        {
            INvrEditView editView =
                _viewFactory.CreateNvrEditView();

            var presenter = new NvrEditPresenter(
                editView,
                _providerCatalog,
                _providerFactory,
                null,
                AddNvr);

            presenter.Show();
        }

        /// <summary>
        /// 선택된 NVR 수정 팝업을 표시한다.
        /// </summary>
        private void OnEditNvr(object sender, EventArgs e)
        {
            if (!_view.SelectedNvrNo.HasValue)
            {
                _view.ShowMessage("수정할 NVR을 선택해야 합니다.");
                return;
            }

            int nvrNo = _view.SelectedNvrNo.Value;

            NvrConfig nvr = _workingConfig.NvrList
                .FirstOrDefault(x => x != null && x.NvrNo == nvrNo);

            if (nvr == null)
            {
                _view.ShowMessage("선택한 NVR 설정을 찾을 수 없습니다.");
                return;
            }

            INvrEditView editView =
                _viewFactory.CreateNvrEditView();

            var presenter = new NvrEditPresenter(
                editView,
                _providerCatalog,
                _providerFactory,
                nvr,
                updated => UpdateNvr(nvrNo, updated));

            presenter.Show();
        }

        /// <summary>
        /// 선택된 NVR을 삭제한다.
        ///
        /// 계산대 등록정보에서 사용 중인 NVR은 삭제할 수 없다.
        /// 삭제된 NVR번호는 다시 사용하지 않는다.
        /// </summary>
        private void OnDeleteNvr(object sender, EventArgs e)
        {
            if (!_view.SelectedNvrNo.HasValue)
            {
                _view.ShowMessage("삭제할 NVR을 선택해야 합니다.");
                return;
            }

            int nvrNo = _view.SelectedNvrNo.Value;

            bool isUsed = _workingConfig.CounterMapList
                .Any(x => x != null && x.NvrNo == nvrNo);

            if (isUsed)
            {
                _view.ShowMessage(
                    "선택한 NVR을 사용하는 계산대 등록정보가 있습니다."
                    + Environment.NewLine
                    + "계산대 등록정보를 먼저 삭제해야 합니다.");

                return;
            }

            if (!_view.Confirm("선택한 NVR 설정을 삭제하시겠습니까?"))
            {
                return;
            }

            NvrConfig nvr = _workingConfig.NvrList
                .FirstOrDefault(x => x != null && x.NvrNo == nvrNo);

            if (nvr != null)
            {
                _workingConfig.NvrList.Remove(nvr);
            }

            RefreshView();
        }

        /// <summary>
        /// 신규 계산대 등록 팝업을 표시한다.
        /// </summary>
        private void OnAddCounterMap(object sender, EventArgs e)
        {
            if (_workingConfig.NvrList.Count == 0)
            {
                _view.ShowMessage(
                    "계산대를 등록하기 전에 NVR을 먼저 등록해야 합니다.");

                return;
            }

            ICounterEditView editView =
                _viewFactory.CreateCounterEditView();

            var presenter = new CounterEditPresenter(
                editView,
                _workingConfig,
                null,
                AddCounterMap);

            presenter.Show();
        }

        /// <summary>
        /// 선택된 계산대 등록정보 수정 팝업을 표시한다.
        /// </summary>
        private void OnEditCounterMap(object sender, EventArgs e)
        {
            CounterMapKey key = _view.SelectedCounterMapKey;

            if (key == null)
            {
                _view.ShowMessage(
                    "수정할 계산대 등록정보를 선택해야 합니다.");

                return;
            }

            CounterMap counterMap = _workingConfig.CounterMapList
                .FirstOrDefault(x =>
                    x != null
                    && x.CounterNo == key.CounterNo
                    && x.ScreenPosition == key.ScreenPosition);

            if (counterMap == null)
            {
                _view.ShowMessage(
                    "선택한 계산대 등록정보를 찾을 수 없습니다.");

                return;
            }

            ICounterEditView editView =
                _viewFactory.CreateCounterEditView();

            var presenter = new CounterEditPresenter(
                editView,
                _workingConfig,
                counterMap,
                updated => UpdateCounterMap(key, updated));

            presenter.Show();
        }

        /// <summary>
        /// 선택된 계산대 등록정보를 삭제한다.
        /// </summary>
        private void OnDeleteCounterMap(object sender, EventArgs e)
        {
            CounterMapKey key = _view.SelectedCounterMapKey;

            if (key == null)
            {
                _view.ShowMessage(
                    "삭제할 계산대 등록정보를 선택해야 합니다.");

                return;
            }

            if (!_view.Confirm(
                "선택한 계산대 등록정보를 삭제하시겠습니까?"))
            {
                return;
            }

            CounterMap counterMap = _workingConfig.CounterMapList
                .FirstOrDefault(x =>
                    x != null
                    && x.CounterNo == key.CounterNo
                    && x.ScreenPosition == key.ScreenPosition);

            if (counterMap != null)
            {
                _workingConfig.CounterMapList.Remove(counterMap);
            }

            RefreshView();
        }

        /// <summary>
        /// 설정 전체를 검증하고 저장 요청을 상위 흐름에 전달한다.
        /// 저장이 성공한 경우에만 설정 화면을 닫는다.
        /// </summary>
        private async void OnSave(object sender, EventArgs e)
        {
            ConfigValidationResult validationResult =
                _validator.Validate(_workingConfig);

            if (!validationResult.IsValid)
            {
                string message = string.Join(
                    Environment.NewLine,
                    validationResult.Errors.Select(x => "- " + x.Message));

                _view.ShowMessage(message);
                return;
            }

            _workingConfig.SyncStatus =
                ViewerConfigSyncStatus.LocalModified;

            bool saved = await _onSaveRequested(
                ViewerConfigCloneHelper.Clone(_workingConfig));

            if (!saved)
            {
                return;
            }

            _view.CloseView();
        }

        /// <summary>
        /// 설정 화면을 닫는다.
        /// </summary>
        private void OnClose(object sender, EventArgs e)
        {
            _view.CloseView();
        }

        /// <summary>
        /// 신규 NVR 설정을 추가한다.
        ///
        /// NVR번호는 사용자가 입력하지 않으며,
        /// NextNvrNo 값을 사용하여 자동 지정한다.
        /// </summary>
        private void AddNvr(NvrConfig nvr)
        {
            if (nvr == null)
            {
                _view.ShowMessage("등록할 NVR 설정정보가 없습니다.");
                return;
            }

            NvrConfig newNvr =
                ViewerConfigCloneHelper.Clone(nvr);

            newNvr.NvrNo = GetNextNvrNo();

            _workingConfig.NvrList.Add(newNvr);

            RefreshView();
        }

        /// <summary>
        /// 기존 NVR 설정을 수정한다.
        ///
        /// NVR번호는 수정하지 않고 기존 번호를 유지한다.
        /// </summary>
        private void UpdateNvr(
            int originalNvrNo,
            NvrConfig updated)
        {
            if (updated == null)
            {
                _view.ShowMessage("수정할 NVR 설정정보가 없습니다.");
                return;
            }

            NvrConfig target = _workingConfig.NvrList
                .FirstOrDefault(x =>
                    x != null
                    && x.NvrNo == originalNvrNo);

            if (target == null)
            {
                _view.ShowMessage("수정할 NVR 설정을 찾을 수 없습니다.");
                return;
            }

            CounterMap invalidCounterMap =
                _workingConfig.CounterMapList.FirstOrDefault(x =>
                    x != null
                    && x.NvrNo == originalNvrNo
                    && x.ChannelNo > updated.ChannelCount);

            if (invalidCounterMap != null)
            {
                _view.ShowMessage(
                    "변경하려는 채널 수보다 큰 채널번호를 사용하는 "
                    + "계산대 등록정보가 있습니다."
                    + Environment.NewLine
                    + string.Format(
                        "계산대번호: {0}, 채널번호: {1}",
                        invalidCounterMap.CounterNo,
                        invalidCounterMap.ChannelNo));

                return;
            }

            target.Vendor = updated.Vendor;
            target.ConnectionType = updated.ConnectionType;
            target.ProviderKey = updated.ProviderKey;
            target.Host = updated.Host;
            target.Port = updated.Port;
            target.ChannelCount = updated.ChannelCount;
            target.UserId = updated.UserId;
            target.Password = updated.Password;

            target.ProviderSettings.Clear();

            if (updated.ProviderSettings != null)
            {
                foreach (var item in updated.ProviderSettings)
                {
                    target.ProviderSettings[item.Key] = item.Value;
                }
            }

            RefreshView();
        }

        /// <summary>
        /// 신규 계산대 등록정보를 추가한다.
        /// </summary>
        private void AddCounterMap(CounterMap counterMap)
        {
            if (counterMap == null)
            {
                _view.ShowMessage(
                    "등록할 계산대 설정정보가 없습니다.");

                return;
            }

            if (HasCounterMapKey(
                counterMap.CounterNo,
                counterMap.ScreenPosition,
                null))
            {
                _view.ShowMessage(
                    "같은 계산대번호의 같은 스크린위치가 "
                    + "이미 등록되어 있습니다.");

                return;
            }

            _workingConfig.CounterMapList.Add(
                ViewerConfigCloneHelper.Clone(counterMap));

            RefreshView();
        }

        /// <summary>
        /// 기존 계산대 등록정보를 수정한다.
        /// </summary>
        private void UpdateCounterMap(
            CounterMapKey originalKey,
            CounterMap updated)
        {
            if (originalKey == null || updated == null)
            {
                _view.ShowMessage(
                    "수정할 계산대 설정정보가 없습니다.");

                return;
            }

            if (HasCounterMapKey(
                updated.CounterNo,
                updated.ScreenPosition,
                originalKey))
            {
                _view.ShowMessage(
                    "같은 계산대번호의 같은 스크린위치가 "
                    + "이미 등록되어 있습니다.");

                return;
            }

            CounterMap target = _workingConfig.CounterMapList
                .FirstOrDefault(x =>
                    x != null
                    && x.CounterNo == originalKey.CounterNo
                    && x.ScreenPosition == originalKey.ScreenPosition);

            if (target == null)
            {
                _view.ShowMessage(
                    "수정할 계산대 등록정보를 찾을 수 없습니다.");

                return;
            }

            target.CounterNo = updated.CounterNo;
            target.NvrNo = updated.NvrNo;
            target.ChannelNo = updated.ChannelNo;
            target.ScreenPosition = updated.ScreenPosition;

            RefreshView();
        }

        /// <summary>
        /// 계산대번호와 스크린위치 조합의 중복 여부를 확인한다.
        /// </summary>
        private bool HasCounterMapKey(
            int counterNo,
            ScreenPosition screenPosition,
            CounterMapKey excludeKey)
        {
            return _workingConfig.CounterMapList.Any(x =>
                x != null
                && x.CounterNo == counterNo
                && x.ScreenPosition == screenPosition
                && (excludeKey == null
                    || x.CounterNo != excludeKey.CounterNo
                    || x.ScreenPosition != excludeKey.ScreenPosition));
        }

        /// <summary>
        /// 기존 설정의 NVR번호를 기준으로 NextNvrNo 값을 보정한다.
        ///
        /// 이전 설정 파일에 NextNvrNo가 없거나 잘못된 경우에도
        /// 현재 가장 큰 NVR번호보다 큰 값이 되도록 처리한다.
        /// </summary>
        private void InitializeNextNvrNo()
        {
            int maxNvrNo = _workingConfig.NvrList
                .Where(x => x != null)
                .Select(x => x.NvrNo)
                .DefaultIfEmpty(0)
                .Max();

            int minimumNextNvrNo = maxNvrNo + 1;

            if (_workingConfig.NextNvrNo < minimumNextNvrNo)
            {
                _workingConfig.NextNvrNo = minimumNextNvrNo;
            }

            if (_workingConfig.NextNvrNo <= 0)
            {
                _workingConfig.NextNvrNo = 1;
            }
        }

        /// <summary>
        /// 신규 NVR에 사용할 번호를 반환하고
        /// 다음 NVR번호 값을 증가시킨다.
        /// </summary>
        private int GetNextNvrNo()
        {
            InitializeNextNvrNo();

            int nextNvrNo = _workingConfig.NextNvrNo;

            if (nextNvrNo == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "더 이상 NVR번호를 자동 생성할 수 없습니다.");
            }

            _workingConfig.NextNvrNo++;

            return nextNvrNo;
        }

        /// <summary>
        /// 설정 화면의 목록과 상태 정보를 갱신한다.
        /// </summary>
        private void RefreshView()
        {
            _view.SetNvrList(
                SettingsViewModelMapper.ToNvrListItems(
                    _workingConfig.NvrList));

            _view.SetCounterMapList(
                SettingsViewModelMapper.ToCounterMapListItems(
                    _workingConfig.CounterMapList));

            _view.SetConfigStatus(
                _workingConfig.ConfigVersion,
                _workingConfig.SyncStatus,
                _workingConfig.LastDownloadedAtUtc,
                _workingConfig.LastUploadedAtUtc);
        }
    }
}
