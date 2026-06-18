using NetSDKCS;
using System;
using System.Net;
using System.Reflection;

namespace CamViewer.Nvr.Dahua.Sdk
{
    /// <summary>
    /// Dahua C# SDK 래퍼를 사용하여 채널 인코딩 설정에서 영상 해상도 정보를 읽는다.
    /// 
    /// 주의:
    /// Native P/Invoke를 직접 호출하지 않고,
    /// Dahua 샘플과 동일하게 NETClient.GetNewDevConfig를 사용한다.
    /// </summary>
    internal static class DahuaEncodeConfigReader
    {
        private const string ConfigCommandEncode = "Encode";

        /// <summary>
        /// Dahua 채널의 Main Stream 인코딩 설정을 조회한다.
        /// </summary>
        public static DahuaEncodeConfigResult ReadMainStreamEncodeConfig(
            IntPtr loginHandle,
            int channelNo)
        {
            if (loginHandle == IntPtr.Zero)
            {
                return DahuaEncodeConfigResult.Fail(
                    "DAHUA_INVALID_LOGIN_HANDLE",
                    "Dahua 로그인 핸들이 유효하지 않습니다.",
                    "");
            }

            int sdkChannelNo =
                NormalizeChannelNo(channelNo);

            try
            {
                NET_CFG_ENCODE_INFO info =
                    new NET_CFG_ENCODE_INFO();

                object obj =
                    info;

                bool result =
                    NETClient.GetNewDevConfig(
                        loginHandle,
                        sdkChannelNo,
                        ConfigCommandEncode,
                        ref obj,
                        typeof(NET_CFG_ENCODE_INFO),
                        5000);

                if (!result)
                {
                    return DahuaEncodeConfigResult.Fail(
                        "DAHUA_GET_ENCODE_CONFIG_FAILED",
                        "Dahua 인코딩 설정 조회에 실패했습니다. "
                        + NETClient.GetLastError(),
                        "");
                }

                info =
                    (NET_CFG_ENCODE_INFO)obj;

                object videoFormat =
                    GetMainStreamVideoFormat(info);

                if (videoFormat == null)
                {
                    return DahuaEncodeConfigResult.Fail(
                        "DAHUA_VIDEO_FORMAT_NOT_FOUND",
                        "Dahua Main Stream 영상 포맷 정보를 찾지 못했습니다.",
                        "");
                }

                int width;
                int height;

                if (!TryReadVideoSize(
                        videoFormat,
                        out width,
                        out height))
                {
                    return DahuaEncodeConfigResult.Fail(
                        "DAHUA_VIDEO_SIZE_NOT_FOUND",
                        "Dahua 영상 포맷에서 해상도 필드를 찾지 못했습니다. "
                        + BuildFieldList(videoFormat),
                        "");
                }

                if (width <= 0 || height <= 0)
                {
                    return DahuaEncodeConfigResult.Fail(
                        "DAHUA_INVALID_VIDEO_SIZE",
                        "Dahua 영상 해상도 값이 올바르지 않습니다. "
                        + "Width="
                        + width
                        + ", Height="
                        + height,
                        "");
                }

                return DahuaEncodeConfigResult.Ok(
                    width,
                    height);
            }
            catch (Exception ex)
            {
                return DahuaEncodeConfigResult.Fail(
                    "DAHUA_ENCODE_CONFIG_EXCEPTION",
                    "Dahua 인코딩 설정 조회 중 예외가 발생했습니다. "
                    + ex.GetType().Name
                    + ": "
                    + ex.Message,
                    "");
            }
        }

        /// <summary>
        /// SDK 채널 번호를 보정한다.
        /// Dahua Encode 설정 조회 샘플은 0부터 시작하는 채널 인덱스를 사용한다.
        /// </summary>
        private static int NormalizeChannelNo(int channelNo)
        {
            if (channelNo <= 0)
            {
                return 0;
            }

            // CamViewer 설정에서 채널을 1부터 저장하고 있다면 SDK 호출은 0부터 시작해야 한다.
            return channelNo - 1;
        }

        /// <summary>
        /// NET_CFG_ENCODE_INFO에서 Main Stream 0번 영상 포맷 객체를 꺼낸다.
        /// </summary>
        private static object GetMainStreamVideoFormat(
            NET_CFG_ENCODE_INFO info)
        {
            if (info.stuMainStream == null
                || info.stuMainStream.Length == 0)
            {
                return null;
            }

            return info.stuMainStream[0].stuVideoFormat;
        }

        /// <summary>
        /// Dahua SDK 버전별 필드명 차이를 고려하여 영상 너비/높이를 읽는다.
        /// </summary>
        private static bool TryReadVideoSize(
            object videoFormat,
            out int width,
            out int height)
        {
            width = 0;
            height = 0;

            if (videoFormat == null)
            {
                return false;
            }

            Type type =
                videoFormat.GetType();

            width =
                ReadIntField(
                    videoFormat,
                    type,
                    "nWidth",
                    "nImageWidth",
                    "nVideoWidth",
                    "dwWidth");

            height =
                ReadIntField(
                    videoFormat,
                    type,
                    "nHeight",
                    "nImageHeight",
                    "nVideoHeight",
                    "dwHeight");

            return width > 0 && height > 0;
        }

        /// <summary>
        /// 지정 후보 이름 중 존재하는 정수 필드 값을 읽는다.
        /// </summary>
        private static int ReadIntField(
            object target,
            Type type,
            params string[] fieldNames)
        {
            for (int index = 0; index < fieldNames.Length; index++)
            {
                FieldInfo field =
                    type.GetField(
                        fieldNames[index],
                        BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic);

                if (field == null)
                {
                    continue;
                }

                object value =
                    field.GetValue(target);

                if (value == null)
                {
                    continue;
                }

                try
                {
                    return Convert.ToInt32(value);
                }
                catch
                {
                    return 0;
                }
            }

            return 0;
        }

        /// <summary>
        /// 영상 포맷 객체가 가지고 있는 필드와 속성의 이름, 형식, 값을
        /// 진단 메시지로 생성한다.
        ///
        /// Dahua NetSDKCS 버전에 따라 해상도가
        /// 필드, 속성, 열거형 또는 중첩 구조체로 제공될 수 있으므로
        /// 현재 사용 중인 SDK 구조를 확인하기 위해 사용한다.
        /// </summary>
        private static string BuildFieldList(
            object target)
        {
            if (target == null)
            {
                return "Target: null";
            }

            Type type =
                target.GetType();

            string result =
                "Type: "
                + type.FullName;

            FieldInfo[] fields =
                type.GetFields(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);

            result += " / Fields: ";

            if (fields == null || fields.Length == 0)
            {
                result += "none";
            }
            else
            {
                for (int index = 0; index < fields.Length; index++)
                {
                    if (index > 0)
                    {
                        result += ", ";
                    }

                    FieldInfo field =
                        fields[index];

                    object value = null;

                    try
                    {
                        value =
                            field.GetValue(target);
                    }
                    catch
                    {
                        // 진단 과정의 필드 읽기 실패는 무시한다.
                    }

                    result +=
                        field.Name
                        + "("
                        + field.FieldType.Name
                        + ")="
                        + ConvertMemberValueToText(value);
                }
            }

            PropertyInfo[] properties =
                type.GetProperties(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);

            result += " / Properties: ";

            if (properties == null || properties.Length == 0)
            {
                result += "none";
            }
            else
            {
                for (int index = 0; index < properties.Length; index++)
                {
                    if (index > 0)
                    {
                        result += ", ";
                    }

                    PropertyInfo property =
                        properties[index];

                    object value = null;

                    /*
                     * 인덱서 속성은 인수 없이 읽을 수 없으므로 제외한다.
                     */
                    if (property.GetIndexParameters().Length == 0)
                    {
                        try
                        {
                            value =
                                property.GetValue(
                                    target,
                                    null);
                        }
                        catch
                        {
                            // 일부 SDK 속성은 Getter 호출 중 예외가 발생할 수 있다.
                        }
                    }

                    result +=
                        property.Name
                        + "("
                        + property.PropertyType.Name
                        + ")="
                        + ConvertMemberValueToText(value);
                }
            }

            return result;
        }

        /// <summary>
        /// 진단 대상 멤버 값을 안전한 문자열로 변환한다.
        /// 배열이나 복잡한 구조체는 형식명만 표시한다.
        /// </summary>
        private static string ConvertMemberValueToText(
            object value)
        {
            if (value == null)
            {
                return "null";
            }

            Type valueType =
                value.GetType();

            if (valueType.IsPrimitive
                || valueType.IsEnum
                || value is string
                || value is decimal)
            {
                return value.ToString();
            }

            Array array =
                value as Array;

            if (array != null)
            {
                return valueType.Name
                    + "[Length="
                    + array.Length
                    + "]";
            }

            return valueType.FullName;
        }
    }
}