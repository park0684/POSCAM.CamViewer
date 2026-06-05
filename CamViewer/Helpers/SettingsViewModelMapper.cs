using System.Collections.Generic;
using System.Linq;
using CamViewer.Models.ViewModels;
using CamViewerClient.Enums;
using CamViewerClient.Models.Config;
using CamViewer.Nvr.Core.Models;

namespace CamViewer.Helpers
{
    /// <summary>
    /// 캠뷰어 설정 모델과 NVR Provider 등록정보를
    /// 화면 표시용 ViewModel로 변환한다.
    /// </summary>
    public static class SettingsViewModelMapper
    {
        /// <summary>
        /// NVR 설정 목록을 설정 화면 표시용 목록으로 변환한다.
        /// 비밀번호와 Provider별 추가 설정값은 포함하지 않는다.
        /// </summary>
        /// <param name="source">NVR 설정 목록.</param>
        /// <returns>설정 화면에 표시할 NVR 목록.</returns>
        public static IEnumerable<NvrListItem> ToNvrListItems(
            IEnumerable<NvrConfig> source)
        {
            if (source == null)
            {
                return new List<NvrListItem>();
            }

            return source
                .Where(x => x != null)
                .OrderBy(x => x.NvrNo)
                .Select(x => new NvrListItem
                {
                    NvrNo = x.NvrNo,
                    Vendor = x.Vendor,
                    ConnectionType = x.ConnectionType,
                    ProviderKey = x.ProviderKey,
                    Host = x.Host,
                    Port = x.Port,
                    ChannelCount = x.ChannelCount,
                    UserId = x.UserId
                })
                .ToList();
        }

        /// <summary>
        /// 계산대 등록 목록을 설정 화면 표시용 목록으로 변환한다.
        /// </summary>
        /// <param name="source">계산대 등록 목록.</param>
        /// <returns>설정 화면에 표시할 계산대 등록 목록.</returns>
        public static IEnumerable<CounterMapListItem> ToCounterMapListItems(
            IEnumerable<CounterMap> source)
        {
            if (source == null)
            {
                return new List<CounterMapListItem>();
            }

            return source
                .Where(x => x != null)
                .OrderBy(x => x.CounterNo)
                .ThenBy(x => x.ScreenPosition)
                .Select(x => new CounterMapListItem
                {
                    CounterNo = x.CounterNo,
                    NvrNo = x.NvrNo,
                    ChannelNo = x.ChannelNo,
                    ScreenPosition = (int)x.ScreenPosition,
                    ScreenPositionText =
                        ToScreenPositionText(x.ScreenPosition)
                })
                .ToList();
        }

        /// <summary>
        /// NVR Provider 등록정보를 제조사 선택 목록으로 변환한다.
        ///
        /// 현재 정책에서는 하나의 제조사에 하나의 기본 Provider만 사용할 수 있다.
        /// 동일 제조사의 Provider가 여러 개 등록되어 있으면
        /// ProviderKey 오름차순 기준 첫 번째 Provider를 사용한다.
        /// </summary>
        /// <param name="source">NVR Provider 등록정보 목록.</param>
        /// <returns>NVR 등록/수정 화면에 표시할 제조사 선택 목록.</returns>
        public static IEnumerable<VendorOptionItem> ToVendorOptionItems(
            IEnumerable<ProviderRegistration> source)
        {
            if (source == null)
            {
                return new List<VendorOptionItem>();
            }

            return source
                .Where(x =>
                    x != null
                    && !string.IsNullOrWhiteSpace(x.Vendor)
                    && !string.IsNullOrWhiteSpace(x.ProviderKey))
                .GroupBy(x => x.Vendor)
                .Select(group => group
                    .OrderBy(x => x.ProviderKey)
                    .First())
                .OrderBy(x => x.Vendor)
                .Select(x => new VendorOptionItem
                {
                    Vendor = x.Vendor,
                    ConnectionType = x.ConnectionType.ToString(),
                    ProviderKey = x.ProviderKey,
                    DisplayText = x.Vendor
                })
                .ToList();
        }

        /// <summary>
        /// NVR 설정 목록을 계산대 등록/수정 화면의
        /// NVR 선택 목록으로 변환한다.
        /// </summary>
        /// <param name="source">NVR 설정 목록.</param>
        /// <returns>계산대 등록/수정 화면에 표시할 NVR 선택 목록.</returns>
        public static IEnumerable<NvrOptionItem> ToNvrOptionItems(
            IEnumerable<NvrConfig> source)
        {
            if (source == null)
            {
                return new List<NvrOptionItem>();
            }

            return source
                .Where(x => x != null)
                .OrderBy(x => x.NvrNo)
                .Select(x => new NvrOptionItem
                {
                    NvrNo = x.NvrNo,
                    ChannelCount = x.ChannelCount,
                    DisplayText = string.Format(
                        "NVR {0} - {1} / {2}",
                        x.NvrNo,
                        x.Vendor,
                        x.Host)
                })
                .ToList();
        }

        /// <summary>
        /// 스크린위치 값을 사용자 표시용 문자열로 변환한다.
        /// </summary>
        /// <param name="screenPosition">스크린위치 내부값.</param>
        /// <returns>좌측 또는 우측 문자열.</returns>
        public static string ToScreenPositionText(
            ScreenPosition screenPosition)
        {
            switch (screenPosition)
            {
                case ScreenPosition.Left:
                    return "좌측";

                case ScreenPosition.Right:
                    return "우측";

                default:
                    return "알 수 없음";
            }
        }
    }
}