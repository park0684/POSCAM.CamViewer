using CamViewer.Models;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CamViewer.Services
{
    /// <summary>
    /// CamViewer 명령행 인자를 분석하는 기본 구현체이다.
    /// 
    /// 지원 인자:
    /// --playback-time "yyyy-MM-ddTHH:mm:ss"
    /// --counter 1
    /// </summary>
    public sealed class ApplicationLaunchArgumentParser : IApplicationLaunchArgumentParser
    {
        private const string PlaybackTimeArgument =
            "--playback-time";

        private const string CounterArgument =
            "--counter";

        /// <summary>
        /// 외부 POS 프로그램에서 전달하는 계산대번호 항목명.
        /// </summary>
        private const string LegacyCounterArgument =
            "opr";

        /// <summary>
        /// 외부 POS 프로그램에서 전달하는 거래 완료 시각 항목명.
        /// </summary>
        private const string LegacyPlaybackTimeArgument =
            "starttime";

        /// <summary>
        /// 외부 프로그램에서 전달할 수 있는 안전한 날짜 형식 목록이다.
        /// 지역 설정에 따라 일/월이 뒤바뀌지 않도록 명확한 형식만 허용한다.
        /// </summary>
        private static readonly string[] SupportedDateTimeFormats =
        {
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "yyyyMMddHHmmss"
        };

        /// <summary>
        /// 명령행 인자를 분석하여 실행 요청을 생성한다.
        /// </summary>
        public bool TryParse(
            string[] args,
            out ApplicationLaunchRequest request,
            out string errorMessage)
        {
            request = null;
            errorMessage = string.Empty;

            // 명령행 인자가 없으면 사용자가 CamViewer를 직접 실행한 것으로 처리한다.
            if (args == null || args.Length == 0)
            {
                request =
                    ApplicationLaunchRequest.CreateDirectLaunch();

                return true;
            }

            /*
             * 외부 POS 프로그램은 다음과 같은 QueryString 형식으로
             * 실행 인자를 전달할 수 있다.
             *
             * pre=1&starttime=2026-06-11 08:51:42
             *
             * 날짜와 시간 사이의 공백으로 인해 운영체제에서 인자가
             * 둘 이상으로 분리될 수도 있으므로 전체 인자를 다시 합친다.
             */
            string combinedArgument =
                string.Join(
                    " ",
                    args).Trim();

            /*
             * QueryString 형식이면 기존 --playback-time 분석보다 먼저 처리한다.
             */
            if (IsLegacyQueryArgument(
                combinedArgument))
            {
                return TryParseLegacyQueryArgument(
                    combinedArgument,
                    out request,
                    out errorMessage);
            }

            var argumentValues =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < args.Length; index++)
            {
                string argumentName =
                    args[index];

                if (string.IsNullOrWhiteSpace(argumentName))
                {
                    continue;
                }

                if (!IsSupportedArgument(argumentName))
                {
                    errorMessage =
                        "지원하지 않는 실행 인자가 전달되었습니다."
                        + Environment.NewLine
                        + argumentName;

                    return false;
                }

                // 현재 지원하는 모든 인자는 다음 위치에 값이 있어야 한다.
                if (index + 1 >= args.Length)
                {
                    errorMessage =
                        "실행 인자에 필요한 값이 없습니다."
                        + Environment.NewLine
                        + argumentName;

                    return false;
                }

                string argumentValue =
                    args[index + 1];

                if (string.IsNullOrWhiteSpace(argumentValue)
                    || argumentValue.StartsWith(
                        "--",
                        StringComparison.Ordinal))
                {
                    errorMessage =
                        "실행 인자에 필요한 값이 없습니다."
                        + Environment.NewLine
                        + argumentName;

                    return false;
                }

                if (argumentValues.ContainsKey(argumentName))
                {
                    errorMessage =
                        "동일한 실행 인자가 중복되었습니다."
                        + Environment.NewLine
                        + argumentName;

                    return false;
                }

                argumentValues.Add(
                    argumentName,
                    argumentValue.Trim());

                // 값 부분까지 처리했으므로 다음 인덱스를 건너뛴다.
                index++;
            }

            string playbackTimeText;

            // 인자가 전달된 경우 외부 영상 요청이므로
            // playback-time은 반드시 존재해야 한다.
            if (!argumentValues.TryGetValue(
                PlaybackTimeArgument,
                out playbackTimeText))
            {
                errorMessage =
                    "외부 영상 조회 요청에 재생 기준 시간이 없습니다."
                    + Environment.NewLine
                    + PlaybackTimeArgument
                    + " 값을 전달해 주세요.";

                return false;
            }

            DateTime referenceTime;

            if (!DateTime.TryParseExact(
                    playbackTimeText,
                    SupportedDateTimeFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out referenceTime))
            {
                errorMessage =
                    "전달된 영상 조회 시간이 올바르지 않습니다."
                    + Environment.NewLine
                    + "허용 형식: yyyy-MM-ddTHH:mm:ss"
                    + Environment.NewLine
                    + "전달값: "
                    + playbackTimeText;

                return false;
            }

            int? counterNo = null;
            string counterText;

            if (argumentValues.TryGetValue(
                    CounterArgument,
                    out counterText))
            {
                int parsedCounterNo;

                if (!int.TryParse(
                        counterText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out parsedCounterNo)
                    || parsedCounterNo <= 0)
                {
                    errorMessage =
                        "전달된 계산대 번호가 올바르지 않습니다."
                        + Environment.NewLine
                        + "계산대 번호는 1 이상의 정수여야 합니다."
                        + Environment.NewLine
                        + "전달값: "
                        + counterText;

                    return false;
                }

                counterNo =
                    parsedCounterNo;
            }

            request =
                ApplicationLaunchRequest.CreateExternalPlayback(
                    referenceTime,
                    counterNo);

            return true;
        }

        /// <summary>
        /// 현재 지원하는 실행 인자인지 확인한다.
        /// </summary>
        private static bool IsSupportedArgument(
            string argumentName)
        {
            return string.Equals(
                       argumentName,
                       PlaybackTimeArgument,
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       argumentName,
                       CounterArgument,
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 외부 POS 프로그램의 QueryString 형식 실행 인자인지 확인한다.
        /// </summary>
        private static bool IsLegacyQueryArgument(
            string argumentText)
        {
            if (string.IsNullOrWhiteSpace(
                argumentText))
            {
                return false;
            }

            return argumentText.IndexOf(
                       LegacyPlaybackTimeArgument + "=",
                       StringComparison.OrdinalIgnoreCase) >= 0
                   || argumentText.IndexOf(
                       LegacyCounterArgument + "=",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 외부 POS 프로그램에서 전달한 QueryString 형식의
        /// 실행 인자를 분석한다.
        ///
        /// 지원 형식:
        /// pre=1&amp;starttime=2026-06-11 08:51:42
        /// </summary>
        private static bool TryParseLegacyQueryArgument(
            string argumentText,
            out ApplicationLaunchRequest request,
            out string errorMessage)
        {
            request = null;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(
                argumentText))
            {
                errorMessage =
                    "외부 영상 조회 실행 인자가 비어 있습니다.";

                return false;
            }

            /*
             * 명령 프롬프트에서 전체 인자를 따옴표로 감싼 경우
             * 앞뒤 따옴표를 제거한다.
             */
            string normalizedArgument =
                argumentText
                    .Trim()
                    .Trim('"');

            var argumentValues =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            string[] argumentParts =
                normalizedArgument.Split(
                    new[]
                    {
                '&'
                    },
                    StringSplitOptions.RemoveEmptyEntries);

            foreach (string argumentPart in argumentParts)
            {
                int separatorIndex =
                    argumentPart.IndexOf('=');

                if (separatorIndex <= 0)
                {
                    errorMessage =
                        "외부 영상 조회 실행 인자 형식이 올바르지 않습니다."
                        + Environment.NewLine
                        + "전달값: "
                        + argumentPart;

                    return false;
                }

                string argumentName =
                    argumentPart
                        .Substring(
                            0,
                            separatorIndex)
                        .Trim();

                string argumentValue =
                    argumentPart
                        .Substring(
                            separatorIndex + 1)
                        .Trim();

                argumentValue =
                    DecodeLegacyArgumentValue(
                        argumentValue);

                if (string.IsNullOrWhiteSpace(
                    argumentName))
                {
                    errorMessage =
                        "외부 영상 조회 실행 인자 이름이 비어 있습니다.";

                    return false;
                }

                if (argumentValues.ContainsKey(
                    argumentName))
                {
                    errorMessage =
                        "동일한 외부 실행 인자가 중복되었습니다."
                        + Environment.NewLine
                        + argumentName;

                    return false;
                }

                argumentValues.Add(
                    argumentName,
                    argumentValue);
            }

            string playbackTimeText;

            if (!argumentValues.TryGetValue(
                LegacyPlaybackTimeArgument,
                out playbackTimeText)
                || string.IsNullOrWhiteSpace(
                    playbackTimeText))
            {
                errorMessage =
                    "외부 영상 조회 요청에 거래 완료 시각이 없습니다."
                    + Environment.NewLine
                    + LegacyPlaybackTimeArgument
                    + " 값을 전달해 주세요.";

                return false;
            }

            DateTime referenceTime;

            if (!DateTime.TryParseExact(
                    playbackTimeText,
                    SupportedDateTimeFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out referenceTime))
            {
                errorMessage =
                    "전달된 영상 조회 시간이 올바르지 않습니다."
                    + Environment.NewLine
                    + "허용 형식: yyyy-MM-dd HH:mm:ss"
                    + Environment.NewLine
                    + "전달값: "
                    + playbackTimeText;

                return false;
            }

            int? counterNo = null;
            string counterText;

            if (argumentValues.TryGetValue(
                LegacyCounterArgument,
                out counterText)
                && !string.IsNullOrWhiteSpace(
                    counterText))
            {
                int parsedCounterNo;

                if (!int.TryParse(
                        counterText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out parsedCounterNo)
                    || parsedCounterNo <= 0)
                {
                    errorMessage =
                        "전달된 계산대 번호가 올바르지 않습니다."
                        + Environment.NewLine
                        + "계산대 번호는 1 이상의 정수여야 합니다."
                        + Environment.NewLine
                        + "전달값: "
                        + counterText;

                    return false;
                }

                counterNo =
                    parsedCounterNo;
            }

            request =
                ApplicationLaunchRequest
                    .CreateExternalPlayback(
                        referenceTime,
                        counterNo);

            return true;
        }

        /// <summary>
        /// URL 인코딩된 외부 인자값을 원래 문자열로 복원한다.
        ///
        /// 예:
        /// 2026-06-11+08%3A51%3A42
        /// → 2026-06-11 08:51:42
        /// </summary>
        private static string DecodeLegacyArgumentValue(
            string value)
        {
            if (string.IsNullOrEmpty(
                value))
            {
                return string.Empty;
            }

            string normalizedValue =
                value.Replace(
                    "+",
                    " ");

            try
            {
                return Uri.UnescapeDataString(
                    normalizedValue);
            }
            catch (UriFormatException)
            {
                /*
                 * 잘못된 URL 인코딩이 있어도 원문을 가지고
                 * 이후 날짜 또는 숫자 검증을 수행한다.
                 */
                return normalizedValue;
            }
        }
    }
}