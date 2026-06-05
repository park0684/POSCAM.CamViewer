using System;
using System.Collections.Generic;
using System.Linq;
using CamViewer.Helpers;
using CamViewer.Interfaces;
using CamViewer.Models.ViewModels;
using CamViewerClient.Enums;
using CamViewerClient.Models.Config;

namespace CamViewer.Presenters
{
    /// <summary>
    /// 계산대 등록/수정 팝업의 입력값, NVR 선택,
    /// 채널번호 범위 및 저장 요청을 처리한다.
    /// </summary>
    public sealed class CounterEditPresenter
    {
        private readonly ICounterEditView _view;
        private readonly ViewerConfig _viewerConfig;
        private readonly CounterMap _editCounterMap;
        private readonly Action<CounterMap> _onSaved;

        /// <summary>
        /// 계산대 등록/수정 Presenter를 초기화한다.
        /// </summary>
        /// <param name="view">계산대 등록/수정 View.</param>
        /// <param name="viewerConfig">현재 작업 중인 캠뷰어 설정.</param>
        /// <param name="editCounterMap">
        /// 수정할 계산대 등록정보. 신규 등록이면 null이다.
        /// </param>
        /// <param name="onSaved">
        /// 입력값 검증 성공 후 계산대 등록정보를 전달할 콜백.
        /// </param>
        public CounterEditPresenter(
            ICounterEditView view,
            ViewerConfig viewerConfig,
            CounterMap editCounterMap,
            Action<CounterMap> onSaved)
        {
            if (view == null)
            {
                throw new ArgumentNullException("view");
            }

            if (viewerConfig == null)
            {
                throw new ArgumentNullException("viewerConfig");
            }

            if (onSaved == null)
            {
                throw new ArgumentNullException("onSaved");
            }

            _view = view;
            _viewerConfig = viewerConfig;
            _editCounterMap =
                ViewerConfigCloneHelper.Clone(editCounterMap);
            _onSaved = onSaved;

            _view.LoadViewEvent += OnLoadView;
            _view.NvrChangedEvent += OnNvrChanged;
            _view.SaveEvent += OnSave;
            _view.CloseEvent += OnClose;
        }

        /// <summary>
        /// 계산대 등록/수정 팝업을 표시한다.
        /// </summary>
        public void Show()
        {
            _view.ShowView();
        }

        /// <summary>
        /// 팝업 화면의 NVR 목록과 초기값을 설정한다.
        /// </summary>
        private void OnLoadView(object sender, EventArgs e)
        {
            List<NvrOptionItem> nvrOptions =
                SettingsViewModelMapper
                    .ToNvrOptionItems(_viewerConfig.NvrList)
                    .ToList();

            _view.SetNvrOptions(nvrOptions);

            if (_editCounterMap == null)
            {
                LoadCreateMode(nvrOptions);
                return;
            }

            LoadEditMode();
        }

        /// <summary>
        /// 신규 등록 모드의 초기값을 설정한다.
        /// </summary>
        private void LoadCreateMode(
            IList<NvrOptionItem> nvrOptions)
        {
            if (nvrOptions != null && nvrOptions.Count > 0)
            {
                _view.SelectedNvrNo = nvrOptions[0].NvrNo;
            }

            _view.ScreenPosition = ScreenPosition.Left;

            SetSelectedNvrChannelRange();
        }

        /// <summary>
        /// 수정 모드의 초기값을 설정한다.
        /// </summary>
        private void LoadEditMode()
        {
            _view.CounterNo = _editCounterMap.CounterNo;
            _view.SelectedNvrNo = _editCounterMap.NvrNo;

            SetSelectedNvrChannelRange();

            _view.ChannelNo = _editCounterMap.ChannelNo;
            _view.ScreenPosition = _editCounterMap.ScreenPosition;
        }

        /// <summary>
        /// NVR 선택값이 변경되면 채널번호 입력 범위를 갱신한다.
        /// </summary>
        private void OnNvrChanged(object sender, EventArgs e)
        {
            SetSelectedNvrChannelRange();
        }

        /// <summary>
        /// 현재 입력값을 검증하고 계산대 등록정보를
        /// 상위 설정 화면에 전달한다.
        /// </summary>
        private void OnSave(object sender, EventArgs e)
        {
            CounterMap counterMap = BuildCounterMapFromView();

            string validationMessage = Validate(counterMap);

            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                _view.ShowMessage(validationMessage);
                return;
            }

            _onSaved(counterMap);
            _view.CloseView();
        }

        /// <summary>
        /// 계산대 등록/수정 팝업을 닫는다.
        /// </summary>
        private void OnClose(object sender, EventArgs e)
        {
            _view.CloseView();
        }

        /// <summary>
        /// 현재 화면 입력값으로 계산대 등록정보를 생성한다.
        /// </summary>
        private CounterMap BuildCounterMapFromView()
        {
            return new CounterMap
            {
                CounterNo = _view.CounterNo ?? 0,
                NvrNo = _view.SelectedNvrNo ?? 0,
                ChannelNo = _view.ChannelNo ?? 0,
                ScreenPosition = _view.ScreenPosition
            };
        }

        /// <summary>
        /// 계산대 등록 입력값을 검증한다.
        ///
        /// 계산대번호와 스크린위치 중복 여부는
        /// SettingsPresenter에서 최종 확인한다.
        /// </summary>
        private string Validate(CounterMap counterMap)
        {
            if (counterMap.CounterNo <= 0)
            {
                return "계산대번호는 1 이상의 값이어야 합니다.";
            }

            NvrConfig nvr = _viewerConfig.NvrList
                .FirstOrDefault(x =>
                    x != null
                    && x.NvrNo == counterMap.NvrNo);

            if (nvr == null)
            {
                return "계산대에 연결할 NVR을 선택해야 합니다.";
            }

            if (counterMap.ChannelNo < 1
                || counterMap.ChannelNo > nvr.ChannelCount)
            {
                return string.Format(
                    "채널번호는 1부터 {0} 사이의 값이어야 합니다.",
                    nvr.ChannelCount);
            }

            if (!Enum.IsDefined(
                typeof(ScreenPosition),
                counterMap.ScreenPosition))
            {
                return "스크린위치 값이 올바르지 않습니다.";
            }

            return null;
        }

        /// <summary>
        /// 선택된 NVR의 채널번호 입력 범위를 화면에 표시한다.
        /// </summary>
        private void SetSelectedNvrChannelRange()
        {
            if (!_view.SelectedNvrNo.HasValue)
            {
                _view.SetChannelRange(1, 1);
                return;
            }

            NvrConfig nvr = _viewerConfig.NvrList
                .FirstOrDefault(x =>
                    x != null
                    && x.NvrNo == _view.SelectedNvrNo.Value);

            if (nvr == null || nvr.ChannelCount <= 0)
            {
                _view.SetChannelRange(1, 1);
                return;
            }

            _view.SetChannelRange(1, nvr.ChannelCount);
        }
    }
}
