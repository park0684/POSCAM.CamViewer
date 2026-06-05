using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Attributes;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;

namespace CamViewer.Nvr.Core.Providers
{
    /// <summary>
    /// 지정된 폴더에서 NVR Provider DLL을 검색하고 등록 정보를 생성한다.
    /// </summary>
    public sealed class ProviderAssemblyLoader
    {
        /// <summary>
        /// 지정된 Provider 폴더에서 INvrProvider 구현체를 검색한다.
        /// 특정 DLL 로드 실패가 전체 검색을 중단하지 않도록 오류를 보고서에 기록한다.
        /// </summary>
        /// <param name="providerRootPath">
        /// Provider DLL을 검색할 루트 경로.
        /// 하위 폴더까지 재귀적으로 검색한다.
        /// </param>
        /// <returns>Provider 검색 및 로드 결과.</returns>
        public ProviderLoadReport Load(string providerRootPath)
        {
            var report = new ProviderLoadReport();

            if (string.IsNullOrWhiteSpace(providerRootPath))
            {
                report.Errors.Add(new ProviderLoadError
                {
                    AssemblyPath = providerRootPath,
                    Message = "Provider 검색 경로가 비어 있습니다.",
                    ExceptionType = typeof(ArgumentException).FullName
                });

                return report;
            }

            if (!Directory.Exists(providerRootPath))
            {
                report.Errors.Add(new ProviderLoadError
                {
                    AssemblyPath = providerRootPath,
                    Message = "Provider 검색 폴더가 존재하지 않습니다.",
                    ExceptionType = typeof(DirectoryNotFoundException).FullName
                });

                return report;
            }

            IEnumerable<string> assemblyPaths;

            try
            {
                assemblyPaths = Directory
                    .EnumerateFiles(
                        providerRootPath,
                        "POSCAM.Nvr.*.dll",
                        SearchOption.AllDirectories)
                    .Where(x => !IsCoreAssembly(x))
                    .ToList();
            }
            catch (Exception ex)
            {
                report.Errors.Add(CreateLoadError(providerRootPath, null, ex));
                return report;
            }

            foreach (var assemblyPath in assemblyPaths)
            {
                LoadAssemblyProviders(assemblyPath, report);
            }

            return report;
        }

        /// <summary>
        /// 하나의 Provider DLL에서 INvrProvider 구현체를 검색한다.
        /// </summary>
        private static void LoadAssemblyProviders(
            string assemblyPath,
            ProviderLoadReport report)
        {
            Assembly assembly;

            try
            {
                assembly = Assembly.LoadFrom(assemblyPath);
            }
            catch (Exception ex)
            {
                report.Errors.Add(CreateLoadError(assemblyPath, null, ex));
                return;
            }

            IEnumerable<Type> providerTypes;

            try
            {
                providerTypes = GetLoadableTypes(assembly)
                    .Where(IsProviderType)
                    .ToList();
            }
            catch (Exception ex)
            {
                report.Errors.Add(CreateLoadError(assemblyPath, null, ex));
                return;
            }

            foreach (var providerType in providerTypes)
            {
                try
                {
                    var exportAttribute = providerType
                        .GetCustomAttributes(
                            typeof(NvrProviderExportAttribute),
                            false)
                        .OfType<NvrProviderExportAttribute>()
                        .FirstOrDefault();

                    if (exportAttribute == null)
                    {
                        continue;
                    }

                    report.Registrations.Add(new ProviderRegistration
                    {
                        ProviderKey = exportAttribute.ProviderKey,
                        DisplayName = exportAttribute.DisplayName,
                        Vendor = exportAttribute.Vendor,
                        ConnectionType = exportAttribute.ConnectionType,
                        ProviderType = providerType,
                        AssemblyPath = assemblyPath
                    });
                }
                catch (Exception ex)
                {
                    report.Errors.Add(
                        CreateLoadError(
                            assemblyPath,
                            providerType.FullName,
                            ex));
                }
            }
        }

        /// <summary>
        /// 로드 가능한 Type만 반환한다.
        /// 일부 Type 로드 실패 시 정상 Type은 계속 사용한다.
        /// </summary>
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(x => x != null);
            }
        }

        /// <summary>
        /// Provider 등록 대상 클래스인지 확인한다.
        /// </summary>
        private static bool IsProviderType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (!type.IsClass || type.IsAbstract)
            {
                return false;
            }

            if (!typeof(INvrProvider).IsAssignableFrom(type))
            {
                return false;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                return false;
            }

            return type.IsDefined(
                typeof(NvrProviderExportAttribute),
                false);
        }

        /// <summary>
        /// Core DLL이 Provider 검색 대상에 포함되지 않도록 확인한다.
        /// </summary>
        private static bool IsCoreAssembly(string assemblyPath)
        {
            var fileName = Path.GetFileName(assemblyPath);

            return string.Equals(
                fileName,
                "POSCAM.Nvr.Core.dll",
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Provider 로드 오류 정보를 생성한다.
        /// </summary>
        private static ProviderLoadError CreateLoadError(
            string assemblyPath,
            string typeName,
            Exception exception)
        {
            return new ProviderLoadError
            {
                AssemblyPath = assemblyPath,
                TypeName = typeName,
                Message = exception.Message,
                ExceptionType = exception.GetType().FullName
            };
        }
    }
}