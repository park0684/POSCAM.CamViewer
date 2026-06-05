using System;
using System.Security.Cryptography;
using System.Text;
using CamViewerClient.Results;

namespace CamViewerClient.Security
{
    /// <summary>
    /// 로컬 설정 문자열을 현재 PC 기준으로 암호화/복호화한다.
    ///
    /// 현재 정책:
    /// - 설정 파일은 실행 파일과 동일한 위치에 저장한다.
    /// - 파일에는 NVR 비밀번호가 포함될 수 있으므로 평문 JSON으로 저장하지 않는다.
    /// - Windows DPAPI의 LocalMachine 범위를 사용하여 같은 PC에서 복호화할 수 있게 한다.
    ///
    /// 주의:
    /// - 다른 PC로 viewer_config.dat 파일만 복사해도 복호화되지 않는 것이 정상이다.
    /// - Windows 재설치 또는 시스템 보호 키 변경 시 기존 파일을 복호화하지 못할 수 있다.
    /// </summary>
    public sealed class LocalDataProtector
    {
        private static readonly byte[] Entropy =
            Encoding.UTF8.GetBytes("POSCAM.CamViewer.LocalConfig.v1");

        /// <summary>
        /// 평문 문자열을 암호화하여 Base64 문자열로 반환한다.
        /// </summary>
        /// <param name="plainText">암호화할 평문 문자열.</param>
        /// <returns>암호화된 Base64 문자열.</returns>
        public ClientResult<string> Protect(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return ClientResult<string>.Fail(
                    "암호화할 데이터가 없습니다.",
                    "PROTECT_SOURCE_REQUIRED");
            }

            try
            {
                byte[] plainBytes =
                    Encoding.UTF8.GetBytes(plainText);

                byte[] protectedBytes =
                    ProtectedData.Protect(
                        plainBytes,
                        Entropy,
                        DataProtectionScope.LocalMachine);

                string protectedText =
                    Convert.ToBase64String(protectedBytes);

                return ClientResult<string>.Ok(
                    protectedText,
                    "로컬 설정 데이터를 암호화했습니다.");
            }
            catch (Exception ex)
            {
                return ClientResult<string>.Fail(
                    "로컬 설정 데이터 암호화 중 오류가 발생했습니다. " + ex.Message,
                    "PROTECT_FAILED");
            }
        }

        /// <summary>
        /// Base64 암호문 문자열을 복호화하여 평문 문자열로 반환한다.
        /// </summary>
        /// <param name="protectedText">복호화할 Base64 암호문 문자열.</param>
        /// <returns>복호화된 평문 문자열.</returns>
        public ClientResult<string> Unprotect(string protectedText)
        {
            if (string.IsNullOrWhiteSpace(protectedText))
            {
                return ClientResult<string>.Fail(
                    "복호화할 데이터가 없습니다.",
                    "UNPROTECT_SOURCE_REQUIRED");
            }

            try
            {
                byte[] protectedBytes =
                    Convert.FromBase64String(protectedText);

                byte[] plainBytes =
                    ProtectedData.Unprotect(
                        protectedBytes,
                        Entropy,
                        DataProtectionScope.LocalMachine);

                string plainText =
                    Encoding.UTF8.GetString(plainBytes);

                return ClientResult<string>.Ok(
                    plainText,
                    "로컬 설정 데이터를 복호화했습니다.");
            }
            catch (FormatException ex)
            {
                return ClientResult<string>.Fail(
                    "로컬 설정 파일 형식이 올바르지 않습니다. " + ex.Message,
                    "UNPROTECT_FORMAT_FAILED");
            }
            catch (CryptographicException ex)
            {
                return ClientResult<string>.Fail(
                    "로컬 설정 파일을 복호화할 수 없습니다. " + ex.Message,
                    "UNPROTECT_CRYPTO_FAILED");
            }
            catch (Exception ex)
            {
                return ClientResult<string>.Fail(
                    "로컬 설정 데이터 복호화 중 오류가 발생했습니다. " + ex.Message,
                    "UNPROTECT_FAILED");
            }
        }
    }
}
