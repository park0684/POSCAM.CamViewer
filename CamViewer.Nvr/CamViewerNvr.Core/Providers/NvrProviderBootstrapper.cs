using System;
using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Results;

namespace CamViewer.Nvr.Core.Providers
{
    /// <summary>
    /// Provider DLL 검색 결과를 Catalog에 등록한다.
    /// </summary>
    public sealed class NvrProviderBootstrapper
    {
        private readonly ProviderAssemblyLoader _assemblyLoader;
        private readonly INvrProviderCatalog _catalog;

        /// <summary>
        /// Provider Bootstrapper를 초기화한다.
        /// </summary>
        public NvrProviderBootstrapper(
            ProviderAssemblyLoader assemblyLoader,
            INvrProviderCatalog catalog)
        {
            if (assemblyLoader == null)
            {
                throw new ArgumentNullException("assemblyLoader");
            }

            if (catalog == null)
            {
                throw new ArgumentNullException("catalog");
            }

            _assemblyLoader = assemblyLoader;
            _catalog = catalog;
        }

        /// <summary>
        /// Provider 폴더를 검색하고 검색된 Provider를 Catalog에 등록한다.
        /// 중복 ProviderKey는 등록하지 않고 오류로 기록한다.
        /// </summary>
        public ProviderLoadReport LoadAndRegister(string providerRootPath)
        {
            var report = _assemblyLoader.Load(providerRootPath);

            foreach (var registration in report.Registrations)
            {
                if (_catalog.Register(registration))
                {
                    continue;
                }

                report.Errors.Add(new ProviderLoadError
                {
                    AssemblyPath = registration.AssemblyPath,
                    TypeName = registration.ProviderType.FullName,
                    Message = "중복된 ProviderKey가 발견되었습니다. ProviderKey: "
                        + registration.ProviderKey,
                    ExceptionType = "DuplicateProviderKey"
                });
            }

            return report;
        }
    }
}