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
        /// 디버깅용으로 영상 포맷 객체의 필드 목록을 만든다.
        /// </summary>
        private static string BuildFieldList(
            object target)
        {
            if (target == null)
            {
                return "";
            }

            FieldInfo[] fields =
                target.GetType().GetFields(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);

            if (fields == null || fields.Length == 0)
            {
                return "Fields: none";
            }

            string result =
                "Fields: ";

            for (int index = 0; index < fields.Length; index++)
            {
                if (index > 0)
                {
                    result += ", ";
                }

                result += fields[index].Name;
            }

            return result;
        }
    }
}