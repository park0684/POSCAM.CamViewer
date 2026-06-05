using CamViewer.Views;
using CamViewer.Interfaces;


namespace CamViewer.Factories
{
    /// <summary>
    /// 설정 화면에서 사용하는 등록/수정 팝업 View를 생성한다.
    /// </summary>
    public sealed class SettingsViewFactory : ISettingsViewFactory
    {
        /// <summary>
        /// NVR 등록/수정 팝업 View를 생성한다.
        /// </summary>
        public INvrEditView CreateNvrEditView()
        {
            return new NvrEditView();
        }

        /// <summary>
        /// 계산대 등록/수정 팝업 View를 생성한다.
        /// </summary>
        public ICounterEditView CreateCounterEditView()
        {
            return new CounterEditView();
        }
    }
}
