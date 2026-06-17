using CamViewer.Models;
using System;
using System.Globalization;

namespace CamViewer.Services
{
    /// <summary>
    /// CamViewer 프로세스 사이에서 실행 요청을 전달하기 위한
    /// Named Pipe 메시지 변환 규칙을 제공한다.
    ///
    /// 메시지 형식:
    ///
    /// 직접 실행:
    /// 1|DIRECT||
    ///
    /// 외부 영상 조회:
    /// 1|EXTERNAL|2026-06-16T14:30:00.0000000|2
    ///
    /// 구성:
    /// - 프로토콜 버전
    /// - 요청 유형
    /// - 영상 조회 기준 시각
    /// - 계산대 번호
    /// </summary>
    public static class ApplicationLaunchRequestPipeProtocol
    {
        /// <summary>
        /// 현재 Pipe 메시지 프로토콜 버전.
        ///
        /// 향후 전달 항목이 변경되면 버전을 증가시켜
        /// 이전 프로그램과의 형식 충돌을 구분한다.
        /// </summary>
        private const string ProtocolVersion = "1";

        private const string DirectRequestType =
            "DIRECT";

        private const string ExternalRequestType =
            "EXTERNAL";

        private const char FieldSeparator =
            '|';

        /// <summary>
        /// ApplicationLaunchRequest를 Named Pipe로 전송 가능한
        /// 한 줄 문자열로 변환한다.
        /// </summary>
        /// <param name="request">
        /// 문자열로 변환할 실행 요청.
        /// </param>
        /// <returns>
        /// Named Pipe로 전송할 메시지 문자열.
        /// </returns>
        public static string Serialize(
            ApplicationLaunchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    "request");
            }

            /*
             * 직접 실행 요청은 기준 시각과 계산대 번호가 없다.
             */
            if (!request.IsExternalPlaybackRequest)
            {
                return string.Join(
                    FieldSeparator.ToString(),
                    ProtocolVersion,
                    DirectRequestType,
                    string.Empty,
                    string.Empty);
            }

            /*
             * 외부 영상 요청에는 반드시 기준 시각이 있어야 한다.
             *
             * 기준 시각이 없는 외부 요청을 전달하면 현재 시각의
             * 잘못된 영상이 재생될 수 있으므로 예외 처리한다.
             */
            if (!request.ReferenceTime.HasValue)
            {
                throw new InvalidOperationException(
                    "외부 영상 조회 요청에 기준 시각이 없습니다.");
            }

            /*
             * DateTime은 지역 설정의 영향을 받지 않고
             * 정밀도와 Kind 값을 보존할 수 있도록
             * 왕복 형식인 O 포맷으로 저장한다.
             */
            string referenceTimeText =
                request.ReferenceTime.Value.ToString(
                    "O",
                    CultureInfo.InvariantCulture);

            string counterNoText =
                request.CounterNo.HasValue
                    ? request.CounterNo.Value.ToString(
                        CultureInfo.InvariantCulture)
                    : string.Empty;

            return string.Join(
                FieldSeparator.ToString(),
                ProtocolVersion,
                ExternalRequestType,
                referenceTimeText,
                counterNoText);
        }

        /// <summary>
        /// Named Pipe에서 수신한 메시지를
        /// ApplicationLaunchRequest로 변환한다.
        /// </summary>
        /// <param name="message">
        /// Named Pipe에서 수신한 메시지.
        /// </param>
        /// <param name="request">
        /// 변환된 실행 요청.
        /// </param>
        /// <param name="errorMessage">
        /// 변환 실패 원인.
        /// </param>
        /// <returns>
        /// 정상적으로 변환했으면 true.
        /// </returns>
        public static bool TryDeserialize(
            string message,
            out ApplicationLaunchRequest request,
            out string errorMessage)
        {
            request = null;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(
                message))
            {
                errorMessage =
                    "수신한 실행 요청 메시지가 비어 있습니다.";

                return false;
            }

            string[] fields =
                message.Split(
                    new[]
                    {
                        FieldSeparator
                    },
                    StringSplitOptions.None);

            /*
             * 현재 프로토콜은 반드시 4개의 필드를 가져야 한다.
             */
            if (fields.Length != 4)
            {
                errorMessage =
                    "수신한 실행 요청 메시지 형식이 올바르지 않습니다."
                    + Environment.NewLine
                    + "필드 수: "
                    + fields.Length;

                return false;
            }

            string version =
                fields[0];

            string requestType =
                fields[1];

            string referenceTimeText =
                fields[2];

            string counterNoText =
                fields[3];

            if (!string.Equals(
                    version,
                    ProtocolVersion,
                    StringComparison.Ordinal))
            {
                errorMessage =
                    "지원하지 않는 실행 요청 프로토콜 버전입니다."
                    + Environment.NewLine
                    + "수신 버전: "
                    + version;

                return false;
            }

            /*
             * 직접 실행 요청은 별도의 시각이나 계산대 번호 없이
             * 기존 창 활성화 용도로만 사용한다.
             */
            if (string.Equals(
                    requestType,
                    DirectRequestType,
                    StringComparison.OrdinalIgnoreCase))
            {
                request =
                    ApplicationLaunchRequest
                        .CreateDirectLaunch();

                return true;
            }

            if (!string.Equals(
                    requestType,
                    ExternalRequestType,
                    StringComparison.OrdinalIgnoreCase))
            {
                errorMessage =
                    "지원하지 않는 실행 요청 유형입니다."
                    + Environment.NewLine
                    + "요청 유형: "
                    + requestType;

                return false;
            }

            DateTime referenceTime;

            if (!DateTime.TryParseExact(
                    referenceTimeText,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out referenceTime))
            {
                errorMessage =
                    "외부 영상 조회 요청의 기준 시각이 올바르지 않습니다."
                    + Environment.NewLine
                    + "전달값: "
                    + referenceTimeText;

                return false;
            }

            int? counterNo = null;

            if (!string.IsNullOrWhiteSpace(
                counterNoText))
            {
                int parsedCounterNo;

                if (!int.TryParse(
                        counterNoText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out parsedCounterNo)
                    || parsedCounterNo <= 0)
                {
                    errorMessage =
                        "외부 영상 조회 요청의 계산대 번호가 올바르지 않습니다."
                        + Environment.NewLine
                        + "전달값: "
                        + counterNoText;

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
    }
}