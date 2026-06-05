using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace CamViewer.Services
{
    /// <summary>
    /// 캠뷰어 실행 환경 정보를 제공한다.
    ///
    /// HWID는 Windows MachineGuid와 PC 이름을 조합한 뒤 SHA256으로 해시한다.
    /// </summary>
    public sealed class ClientEnvironmentProvider : IClientEnvironmentProvider
    {
        /// <summary>
        /// 현재 PC의 HWID를 반환한다.
        /// </summary>
        public string GetHwid()
        {
            string machineGuid = GetMachineGuid();
            string machineName = Environment.MachineName ?? string.Empty;

            string source = machineGuid + "|" + machineName;

            return ComputeSha256(source);
        }

        /// <summary>
        /// 현재 PC 또는 장비 표시명을 반환한다.
        /// </summary>
        public string GetDeviceName()
        {
            return Environment.MachineName;
        }

        /// <summary>
        /// 현재 캠뷰어 프로그램 버전을 반환한다.
        /// </summary>
        public string GetProgramVersion()
        {
            Assembly assembly = Assembly.GetEntryAssembly();

            if (assembly == null)
            {
                return "1.0.0.0";
            }

            Version version = assembly.GetName().Version;

            return version == null
                ? "1.0.0.0"
                : version.ToString();
        }

        /// <summary>
        /// Windows MachineGuid 값을 조회한다.
        /// 조회에 실패하면 PC 이름을 대체값으로 사용한다.
        /// </summary>
        private static string GetMachineGuid()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Cryptography"))
                {
                    if (key == null)
                    {
                        return Environment.MachineName;
                    }

                    object value = key.GetValue("MachineGuid");

                    return value == null
                        ? Environment.MachineName
                        : value.ToString();
                }
            }
            catch
            {
                return Environment.MachineName;
            }
        }

        /// <summary>
        /// 문자열을 SHA256 해시 문자열로 변환한다.
        /// </summary>
        private static string ComputeSha256(string source)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] sourceBytes =
                    Encoding.UTF8.GetBytes(source ?? string.Empty);

                byte[] hashBytes =
                    sha256.ComputeHash(sourceBytes);

                var builder = new StringBuilder();

                foreach (byte item in hashBytes)
                {
                    builder.Append(item.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}