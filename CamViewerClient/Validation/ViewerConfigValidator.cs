using System;
using System.Collections.Generic;
using System.Linq;
using CamViewerClient.Enums;
using CamViewerClient.Models.Config;

namespace CamViewerClient.Validation
{
    /// <summary>
    /// 캠뷰어 설정을 저장하거나 서버에 업로드하기 전에 유효성을 검증한다.
    /// </summary>
    public sealed class ViewerConfigValidator
    {
        /// <summary>
        /// 거래완료 후 보정 시간의 일반 설정 최대값.
        /// 확정 정책에 따라 최대 10초까지 허용한다.
        /// </summary>
        private const int MaxAfterCompleteSeconds = 10;

        /// <summary>
        /// 캠뷰어 전체 설정을 검증한다.
        /// </summary>
        /// <param name="config">검증할 캠뷰어 설정.</param>
        /// <returns>설정 검증 결과.</returns>
        public ConfigValidationResult Validate(ViewerConfig config)
        {
            var result = new ConfigValidationResult();

            if (config == null)
            {
                result.AddError(
                    ConfigValidationErrorCode.ConfigRequired,
                    "ViewerConfig",
                    string.Empty,
                    "캠뷰어 설정정보가 없습니다.");

                return result;
            }

            ValidateStoreCode(config, result);
            ValidateNvrList(config, result);
            ValidateCounterMapList(config, result);
            ValidatePlaybackOption(config, result);

            return result;
        }

        /// <summary>
        /// 설정이 적용되는 매장 코드를 검증한다.
        /// </summary>
        private static void ValidateStoreCode(
            ViewerConfig config,
            ConfigValidationResult result)
        {
            if (config.StoreCode > 0)
            {
                return;
            }

            result.AddError(
                ConfigValidationErrorCode.StoreCodeRequired,
                "ViewerConfig",
                "StoreCode",
                "설정이 적용되는 매장 코드가 없습니다.");
        }

        /// <summary>
        /// NVR 설정 목록을 검증한다.
        /// </summary>
        private static void ValidateNvrList(
            ViewerConfig config,
            ConfigValidationResult result)
        {
            if (config.NvrList == null)
            {
                return;
            }

            var duplicatedNvrNumbers = config.NvrList
                .Where(x => x != null)
                .GroupBy(x => x.NvrNo)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToList();

            foreach (int duplicatedNvrNo in duplicatedNvrNumbers)
            {
                result.AddError(
                    ConfigValidationErrorCode.NvrNoDuplicated,
                    "NvrConfig",
                    "NvrNo",
                    "중복된 NVR번호가 있습니다. NVR번호: " + duplicatedNvrNo,
                    nvrNo: duplicatedNvrNo);
            }

            foreach (NvrConfig nvr in config.NvrList)
            {
                ValidateNvrConfig(nvr, result);
            }
        }

        /// <summary>
        /// 개별 NVR 설정을 검증한다.
        /// </summary>
        private static void ValidateNvrConfig(
            NvrConfig nvr,
            ConfigValidationResult result)
        {
            if (nvr == null)
            {
                result.AddError(
                    ConfigValidationErrorCode.ConfigRequired,
                    "NvrConfig",
                    string.Empty,
                    "NVR 설정정보가 없습니다.");

                return;
            }

            if (nvr.NvrNo <= 0)
            {
                result.AddError(
                    ConfigValidationErrorCode.NvrNoInvalid,
                    "NvrConfig",
                    "NvrNo",
                    "NVR번호는 1 이상의 값이어야 합니다.",
                    nvrNo: nvr.NvrNo);
            }

            if (string.IsNullOrWhiteSpace(nvr.Vendor))
            {
                result.AddError(
                    ConfigValidationErrorCode.NvrVendorRequired,
                    "NvrConfig",
                    "Vendor",
                    "NVR 제조사를 선택해야 합니다.",
                    nvrNo: nvr.NvrNo);
            }

            if (string.IsNullOrWhiteSpace(nvr.ConnectionType))
            {
                result.AddError(
                    ConfigValidationErrorCode.NvrConnectionTypeRequired,
                    "NvrConfig",
                    "ConnectionType",
                    "NVR 접속방식을 선택해야 합니다.",
                    nvrNo: nvr.NvrNo);
            }

            if (string.IsNullOrWhiteSpace(nvr.ProviderKey))
            {
                result.AddError(
                    ConfigValidationErrorCode.NvrProviderKeyRequired,
                    "NvrConfig",
                    "ProviderKey",
                    "NVR Provider가 선택되지 않았습니다.",
                    nvrNo: nvr.NvrNo);
            }

            if (string.IsNullOrWhiteSpace(nvr.Host))
            {
                result.AddError(
                    ConfigValidationErrorCode.NvrHostRequired,
                    "NvrConfig",
                    "Host",
                    "NVR IP 또는 도메인을 입력해야 합니다.",
                    nvrNo: nvr.NvrNo);
            }

            if (nvr.Port < 1 || nvr.Port > 65535)
            {
                result.AddError(
                    ConfigValidationErrorCode.NvrPortInvalid,
                    "NvrConfig",
                    "Port",
                    "NVR 포트 번호는 1부터 65535 사이여야 합니다.",
                    nvrNo: nvr.NvrNo);
            }

            if (nvr.ChannelCount <= 0)
            {
                result.AddError(
                    ConfigValidationErrorCode.NvrChannelCountInvalid,
                    "NvrConfig",
                    "ChannelCount",
                    "NVR 채널 수는 1 이상의 값이어야 합니다.",
                    nvrNo: nvr.NvrNo);
            }

            if (string.IsNullOrWhiteSpace(nvr.UserId))
            {
                result.AddError(
                    ConfigValidationErrorCode.NvrUserIdRequired,
                    "NvrConfig",
                    "UserId",
                    "NVR 로그인 ID를 입력해야 합니다.",
                    nvrNo: nvr.NvrNo);
            }

            if (string.IsNullOrWhiteSpace(nvr.Password))
            {
                result.AddError(
                    ConfigValidationErrorCode.NvrPasswordRequired,
                    "NvrConfig",
                    "Password",
                    "NVR 로그인 비밀번호를 입력해야 합니다.",
                    nvrNo: nvr.NvrNo);
            }
        }

        /// <summary>
        /// 계산대 채널 매핑 목록을 검증한다.
        /// </summary>
        private static void ValidateCounterMapList(
            ViewerConfig config,
            ConfigValidationResult result)
        {
            if (config.CounterMapList == null)
            {
                return;
            }

            IDictionary<int, NvrConfig> nvrMap = config.NvrList
                .Where(x => x != null && x.NvrNo > 0)
                .GroupBy(x => x.NvrNo)
                .ToDictionary(x => x.Key, x => x.First());

            var duplicatedScreenPositions = config.CounterMapList
                .Where(x => x != null)
                .GroupBy(x => new
                {
                    x.CounterNo,
                    x.ScreenPosition
                })
                .Where(x => x.Count() > 1)
                .ToList();

            foreach (var duplicated in duplicatedScreenPositions)
            {
                result.AddError(
                    ConfigValidationErrorCode.CounterScreenPositionDuplicated,
                    "CounterMap",
                    "ScreenPosition",
                    string.Format(
                        "계산대 {0}번의 {1} 스크린위치가 중복 등록되어 있습니다.",
                        duplicated.Key.CounterNo,
                        ToScreenPositionText(duplicated.Key.ScreenPosition)),
                    counterNo: duplicated.Key.CounterNo);
            }

            foreach (CounterMap counterMap in config.CounterMapList)
            {
                ValidateCounterMap(counterMap, nvrMap, result);
            }
        }

        /// <summary>
        /// 개별 계산대 채널 매핑을 검증한다.
        /// </summary>
        private static void ValidateCounterMap(
            CounterMap counterMap,
            IDictionary<int, NvrConfig> nvrMap,
            ConfigValidationResult result)
        {
            if (counterMap == null)
            {
                result.AddError(
                    ConfigValidationErrorCode.ConfigRequired,
                    "CounterMap",
                    string.Empty,
                    "계산대 등록정보가 없습니다.");

                return;
            }

            if (counterMap.CounterNo <= 0)
            {
                result.AddError(
                    ConfigValidationErrorCode.CounterNoInvalid,
                    "CounterMap",
                    "CounterNo",
                    "계산대번호는 1 이상의 값이어야 합니다.",
                    counterNo: counterMap.CounterNo);
            }

            if (counterMap.NvrNo <= 0)
            {
                result.AddError(
                    ConfigValidationErrorCode.CounterNvrNoInvalid,
                    "CounterMap",
                    "NvrNo",
                    "계산대에 연결할 NVR번호가 올바르지 않습니다.",
                    nvrNo: counterMap.NvrNo,
                    counterNo: counterMap.CounterNo);

                return;
            }

            NvrConfig nvr;

            if (!nvrMap.TryGetValue(counterMap.NvrNo, out nvr))
            {
                result.AddError(
                    ConfigValidationErrorCode.CounterNvrNotFound,
                    "CounterMap",
                    "NvrNo",
                    "계산대에 연결된 NVR 설정을 찾을 수 없습니다.",
                    nvrNo: counterMap.NvrNo,
                    counterNo: counterMap.CounterNo);

                return;
            }

            if (counterMap.ChannelNo <= 0)
            {
                result.AddError(
                    ConfigValidationErrorCode.CounterChannelNoInvalid,
                    "CounterMap",
                    "ChannelNo",
                    "채널번호는 1 이상의 값이어야 합니다.",
                    nvrNo: counterMap.NvrNo,
                    counterNo: counterMap.CounterNo);
            }
            else if (counterMap.ChannelNo > nvr.ChannelCount)
            {
                result.AddError(
                    ConfigValidationErrorCode.CounterChannelOutOfRange,
                    "CounterMap",
                    "ChannelNo",
                    string.Format(
                        "채널번호가 NVR 채널 수를 초과했습니다. NVR번호: {0}, 최대 채널: {1}",
                        counterMap.NvrNo,
                        nvr.ChannelCount),
                    nvrNo: counterMap.NvrNo,
                    counterNo: counterMap.CounterNo);
            }

            if (!Enum.IsDefined(
                typeof(ScreenPosition),
                counterMap.ScreenPosition))
            {
                result.AddError(
                    ConfigValidationErrorCode.CounterScreenPositionInvalid,
                    "CounterMap",
                    "ScreenPosition",
                    "스크린위치 값이 올바르지 않습니다.",
                    nvrNo: counterMap.NvrNo,
                    counterNo: counterMap.CounterNo);
            }
        }

        /// <summary>
        /// 영상 재생 옵션을 검증한다.
        /// </summary>
        private static void ValidatePlaybackOption(
            ViewerConfig config,
            ConfigValidationResult result)
        {
            if (config.PlaybackOption == null)
            {
                result.AddError(
                    ConfigValidationErrorCode.PlaybackOptionRequired,
                    "PlaybackOption",
                    string.Empty,
                    "영상 재생 옵션이 없습니다.");

                return;
            }

            if (config.PlaybackOption.BeforeSeconds < 0)
            {
                result.AddError(
                    ConfigValidationErrorCode.BeforeSecondsInvalid,
                    "PlaybackOption",
                    "BeforeSeconds",
                    "검색시간조정 값은 0초 이상이어야 합니다.");
            }

            if (config.PlaybackOption.AfterCompleteSeconds < 0
                || config.PlaybackOption.AfterCompleteSeconds > MaxAfterCompleteSeconds)
            {
                result.AddError(
                    ConfigValidationErrorCode.AfterCompleteSecondsInvalid,
                    "PlaybackOption",
                    "AfterCompleteSeconds",
                    "거래완료 후 보정 시간은 0초부터 10초 사이여야 합니다.");
            }
        }

        /// <summary>
        /// 스크린위치 값을 사용자 표시용 문자열로 변환한다.
        /// </summary>
        private static string ToScreenPositionText(ScreenPosition screenPosition)
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