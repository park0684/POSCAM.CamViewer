using CamViewerClient.Enums;

namespace CamViewer.Models.ViewModels
{
    /// <summary>
    /// 계산대 등록정보를 식별하기 위한 키 모델이다.
    ///
    /// 하나의 계산대에는 좌측과 우측 스크린위치가 각각 한 개씩 존재할 수 있다.
    /// </summary>
    public sealed class CounterMapKey
    {
        /// <summary>
        /// 계산대번호.
        /// </summary>
        public int CounterNo { get; set; }

        /// <summary>
        /// 스크린위치.
        /// </summary>
        public ScreenPosition ScreenPosition { get; set; }
    }
}