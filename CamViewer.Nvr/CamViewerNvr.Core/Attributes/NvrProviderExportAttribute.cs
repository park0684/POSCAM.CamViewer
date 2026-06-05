using System;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Abstractions;
namespace CamViewer.Nvr.Core.Attributes
{
    /// <summary>
    /// INvrProvider 구현 클래스를 NVR Provider로 등록하기 위한 특성이다.
    /// Provider DLL 검색 시 이 특성이 선언된 클래스만 등록 대상으로 사용한다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class NvrProviderExportAttribute : Attribute
    {
        /// <summary>
        /// Provider 등록 정보를 초기화한다.
        /// </summary>
        /// <param name="providerKey">
        /// Provider를 선택하기 위한 고유 키.
        /// 예: DAHUA_SDK, TPLINK_API, GENERIC_RTSP
        /// </param>
        /// <param name="displayName">화면 또는 로그에 표시할 Provider 이름.</param>
        /// <param name="vendor">NVR 제조사명.</param>
        /// <param name="connectionType">NVR 접속 방식.</param>
        public NvrProviderExportAttribute(
            string providerKey,
            string displayName,
            string vendor,
            NvrConnectionType connectionType)
        {
            ProviderKey = providerKey;
            DisplayName = displayName;
            Vendor = vendor;
            ConnectionType = connectionType;
        }

        /// <summary>
        /// Provider를 선택하기 위한 고유 키.
        /// </summary>
        public string ProviderKey { get; private set; }

        /// <summary>
        /// 화면 또는 로그에 표시할 Provider 이름.
        /// </summary>
        public string DisplayName { get; private set; }

        /// <summary>
        /// NVR 제조사명.
        /// </summary>
        public string Vendor { get; private set; }

        /// <summary>
        /// NVR 접속 방식.
        /// </summary>
        public NvrConnectionType ConnectionType { get; private set; }
    }
}