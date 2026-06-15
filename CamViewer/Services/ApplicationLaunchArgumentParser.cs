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
    }
}