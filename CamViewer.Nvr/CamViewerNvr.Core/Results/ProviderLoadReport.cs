using System.Collections.Generic;
using CamViewer.Nvr.Core.Models;

namespace CamViewer.Nvr.Core.Results
{
    /// <summary>
    /// Provider DLL 검색 및 등록 결과를 나타낸다.
    /// </summary>
    public sealed class ProviderLoadReport
    {
        public ProviderLoadReport()
        {
            Registrations = new List<ProviderRegistration>();
            Errors = new List<ProviderLoadError>();
        }

        /// <summary>
        /// 정상적으로 검색된 Provider 등록 정보 목록.
        /// </summary>
        public IList<ProviderRegistration> Registrations { get; private set; }

        /// <summary>
        /// Provider 검색 또는 로드 중 발생한 오류 목록.
        /// </summary>
        public IList<ProviderLoadError> Errors { get; private set; }

        /// <summary>
        /// 정상적으로 등록된 Provider 수.
        /// </summary>
        public int LoadedCount
        {
            get { return Registrations.Count; }
        }

        /// <summary>
        /// 발생한 오류 수.
        /// </summary>
        public int ErrorCount
        {
            get { return Errors.Count; }
        }
    }
}