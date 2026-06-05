using CamViewer.Interfaces;

namespace CamViewer.Factories
{
    /// <summary>
    /// 설정 화면에서 사용하는 등록/수정 팝업 View를 생성한다.
    ///
    /// Presenter가 실제 Form 구현체를 직접 생성하지 않도록 분리한다.
    /// </summary>
    public interface ISettingsViewFactory
    {
        /// <summary>
        /// NVR 등록/수정 팝업 View를 생성한다.
        /// </summary>
        INvrEditView CreateNvrEditView();

        /// <summary>
        /// 계산대 등록/수정 팝업 View를 생성한다.
        /// </summary>
        ICounterEditView CreateCounterEditView();
    }
}