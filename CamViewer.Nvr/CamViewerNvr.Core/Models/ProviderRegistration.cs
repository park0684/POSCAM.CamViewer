using System;
using CamViewer.Nvr.Core.Enums;

namespace CamViewer.Nvr.Core.Models
{
    /// <summary>
    /// 검색된 NVR Provider 구현 클래스의 등록 정보를 나타낸다.
    /// Provider 인스턴스가 아니라 구현 Type을 보관한다.
    /// </summary>
    public sealed class ProviderRegistration
    {
        /// <summary>
        /// Provider를 선택하기 위한 고유 키.
        /// </summary>
        public string ProviderKey { get; set; }

        /// <summary>
        /// 화면 또는 로그에 표시할 Provider 이름.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// NVR 제조사명.
        /// </summary>
        public string Vendor { get; set; }

        /// <summary>
        /// NVR 접속 방식.
        /// </summary>
        public NvrConnectionType ConnectionType { get; set; }

        /// <summary>
        /// INvrProvider를 구현한 클래스 Type.
        /// Factory는 이 Type을 사용하여 새로운 Provider 인스턴스를 생성한다.
        /// </summary>
        public Type ProviderType { get; set; }

        /// <summary>
        /// Provider 구현체가 포함된 어셈블리 파일 경로.
        /// </summary>
        public string AssemblyPath { get; set; }
    }
}