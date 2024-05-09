using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dxgi;

namespace PDFAnalyzer.DXGIService
{
    internal class GraphicsService
    {
        public static int GetBestDeviceId(out nuint maxDedicatedVideoMemory)
        {
            int deviceId = 0;
            maxDedicatedVideoMemory = 0;

            IDXGIFactory2? dxgiFactory = null;
            try
            {
                var createFlags = 0u;
                Windows.Win32.PInvoke.CreateDXGIFactory2(createFlags, typeof(IDXGIFactory2).GUID, out object dxgiFactoryObj).ThrowOnFailure();
                dxgiFactory = (IDXGIFactory2)dxgiFactoryObj;

                IDXGIAdapter1? selectedAdapter = null;

                var index = 0u;
                do
                {
                    var result = dxgiFactory.EnumAdapters1(index, out IDXGIAdapter1? dxgiAdapter1);

                    if (result.Failed)
                    {
                        if (result != HRESULT.DXGI_ERROR_NOT_FOUND)
                        {
                            ReleaseIfNotNull(dxgiAdapter1);
                            result.ThrowOnFailure();
                        }
                        index = 0;
                    }
                    else
                    {
                        Debug.WriteLine($"Adapter {index}:");

                        DXGI_ADAPTER_DESC1 dxgiAdapterDesc;
                        unsafe
                        {
                            dxgiAdapter1.GetDesc1(&dxgiAdapterDesc);
                            
                            Debug.WriteLine($"\tDescription: {dxgiAdapterDesc.Description.AsReadOnlySpan()}");
                            Debug.WriteLine($"\tDedicatedVideoMemory: {(long)dxgiAdapterDesc.DedicatedVideoMemory / 1000000000}GB");
                            Debug.WriteLine($"\tSharedSystemMemory: {(long)dxgiAdapterDesc.SharedSystemMemory / 1000000000}GB");
                        }
                        if (selectedAdapter == null || dxgiAdapterDesc.DedicatedVideoMemory > maxDedicatedVideoMemory)
                        {
                            maxDedicatedVideoMemory = dxgiAdapterDesc.DedicatedVideoMemory;
                            selectedAdapter = dxgiAdapter1;
                            deviceId = (int)index;
                        }

                        index++;
                        ReleaseIfNotNull(dxgiAdapter1);
                        dxgiAdapter1 = null;
                    }
                }
                while (index != 0);
            }
            finally
            {
                ReleaseIfNotNull(dxgiFactory);
            }

            return deviceId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReleaseIfNotNull(object? unknown)
        {
            if (unknown is not null)
            {
                Marshal.FinalReleaseComObject(unknown);
            }
        }
    }
}
