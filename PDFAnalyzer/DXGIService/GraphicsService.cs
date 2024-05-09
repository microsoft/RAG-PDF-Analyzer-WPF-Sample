using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace PDFAnalyzer.DXGIService
{
    internal unsafe class GraphicsService
    {
        public static int GetBestDeviceId(out nuint maxDedicatedVideoMemory)
        {
            ComPtr<IDXGIFactory3> _dxgiFactory = new();

            int deviceId = 0;
            maxDedicatedVideoMemory = 0;

            try
            {
                IDXGIFactory3* dxgiFactory = CreateDxgiFactory(out _);
                _dxgiFactory.Attach(dxgiFactory);

                IDXGIAdapter1* selectedAdapter = null;

                IDXGIAdapter1* dxgiAdapter1 = null;

                var index = 0u;
                do
                {
                    var result = dxgiFactory->EnumAdapters1(index, &dxgiAdapter1);

                    if (result.FAILED)
                    {
                        if (result != DXGI.DXGI_ERROR_NOT_FOUND)
                        {
                            ReleaseIfNotNull(dxgiAdapter1);
                            ThrowExternalException(nameof(IDXGIFactory1.EnumAdapters1), result);
                        }
                        index = 0;
                    }
                    else
                    {
                        Debug.WriteLine($"Adapter {index}:");

                        DXGI_ADAPTER_DESC1 dxgiAdapterDesc;
                        ThrowExternalExceptionIfFailed(dxgiAdapter1->GetDesc1(&dxgiAdapterDesc));

                        //Debug.WriteLine($"\tDescription: {dxgiAdapter1.Description1.Description}");
                        Debug.WriteLine($"\tDedicatedVideoMemory: {(long)dxgiAdapterDesc.DedicatedVideoMemory / 1000000000}GB");
                        Debug.WriteLine($"\tSharedSystemMemory: {(long)dxgiAdapterDesc.SharedSystemMemory / 1000000000}GB");
                        if (selectedAdapter == null || (long)dxgiAdapterDesc.DedicatedVideoMemory > (long)dxgiAdapterDesc.DedicatedVideoMemory)
                        {
                            maxDedicatedVideoMemory = dxgiAdapterDesc.DedicatedVideoMemory;
                            selectedAdapter = dxgiAdapter1;
                            deviceId = (int)index;
                        }

                        index++;
                        dxgiAdapter1 = null;
                    }
                }
                while (index != 0);
            }
            finally
            {
                _ = _dxgiFactory.Reset();
            }

            return deviceId;
        }

        private static IDXGIFactory3* CreateDxgiFactory(out uint dxgiFactoryVersion)
        {
            IDXGIFactory3* dxgiFactory3;

            var createFlags = 0u;
            ThrowExternalExceptionIfFailed(DirectX.CreateDXGIFactory2(createFlags, TerraFX.Interop.Windows.Windows.__uuidof<IDXGIFactory4>(), (void**)&dxgiFactory3));

            return GetLatestDxgiFactory(dxgiFactory3, out dxgiFactoryVersion);
        }

        public static IDXGIFactory3* GetLatestDxgiFactory(IDXGIFactory3* dxgiFactory, out uint dxgiFactoryVersion)
        {
            IDXGIFactory3* result;

            if (dxgiFactory->QueryInterface(TerraFX.Interop.Windows.Windows.__uuidof<IDXGIFactory7>(), (void**)&result).SUCCEEDED)
            {
                dxgiFactoryVersion = 7;
                _ = dxgiFactory->Release();
            }
            else if (dxgiFactory->QueryInterface(TerraFX.Interop.Windows.Windows.__uuidof<IDXGIFactory6>(), (void**)&result).SUCCEEDED)
            {
                dxgiFactoryVersion = 6;
                _ = dxgiFactory->Release();
            }
            else if (dxgiFactory->QueryInterface(TerraFX.Interop.Windows.Windows.__uuidof<IDXGIFactory5>(), (void**)&result).SUCCEEDED)
            {
                dxgiFactoryVersion = 5;
                _ = dxgiFactory->Release();
            }
            else if (dxgiFactory->QueryInterface(TerraFX.Interop.Windows.Windows.__uuidof<IDXGIFactory4>(), (void**)&result).SUCCEEDED)
            {
                dxgiFactoryVersion = 4;
                _ = dxgiFactory->Release();
            }
            else
            {
                dxgiFactoryVersion = 3;
                result = dxgiFactory;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowExternalExceptionIfFailed(HRESULT value, [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
        {
            if (value.FAILED)
            {
                AssertNotNull(valueExpression);
                ThrowExternalException(valueExpression, value);
            }
        }

        /// <summary>Throws an <see cref="ExternalException" />.</summary>
        /// <param name="methodName">The name of the method that caused the exception.</param>
        /// <param name="errorCode">The underlying error code for the exception.</param>
        /// <exception cref="ExternalException">'<paramref name="methodName" />' failed with an error code of '<paramref name="errorCode" />'.</exception>
        [DoesNotReturn]
        public static void ThrowExternalException(string methodName, int errorCode)
        {
            var message = string.Format(CultureInfo.InvariantCulture, "'{0}' failed with an error code of '{1:X8}'", methodName, errorCode);
            throw new ExternalException(message, errorCode);
        }

        /// <summary>Asserts that <paramref name="value" /> is not <c>null</c>.</summary>
        /// <typeparam name="T">The type of <paramref name="value" />.</typeparam>
        /// <param name="value">The value to assert is not <c>null</c>.</param>
        [Conditional("DEBUG")]
        public static void AssertNotNull<T>([NotNull] T? value)
            where T : class => Assert(value is not null);

        /// <summary>Asserts that a condition is <c>true</c>.</summary>
        /// <param name="condition">The condition to assert.</param>
        /// <param name="conditionExpression">The expression of the condition that caused the exception.</param>
        /// <exception cref="InvalidOperationException">TerraFX based assertions are disabled.</exception>
        [Conditional("DEBUG")]
        public static void Assert([DoesNotReturnIf(false)] bool condition, [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
        {
            if (!condition)
            {
                Fail(conditionExpression);
            }
        }

        [Conditional("DEBUG")]
        [DoesNotReturn]
        public static void Fail(string? message = null)
            => ThrowUnreachableException(message);

        [DoesNotReturn]
        public static void ThrowUnreachableException(string? message) => throw new UnreachableException(message);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReleaseIfNotNull<TUnknown>(TUnknown* unknown)
        where TUnknown : unmanaged, IUnknown.Interface
        {
            if (unknown is not null)
            {
                _ = unknown->Release();
            }
        }
    }
}
