using System.Diagnostics;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dxgi;

namespace PDFAnalyzer
{
    internal static class DXGIHelper
    {
        public static int GetBestDeviceId()
        {
            int deviceId = 0;
            nuint maxDedicatedVideoMemory = 0;
            try
            {
                DXGI_CREATE_FACTORY_FLAGS createFlags = 0;
                Windows.Win32.PInvoke.CreateDXGIFactory2(createFlags, typeof(IDXGIFactory2).GUID, out object dxgiFactoryObj).ThrowOnFailure();
                IDXGIFactory2? dxgiFactory = (IDXGIFactory2)dxgiFactoryObj;

                IDXGIAdapter1? selectedAdapter = null;

                var index = 0u;
                do
                {
                    var result = dxgiFactory.EnumAdapters1(index, out IDXGIAdapter1? dxgiAdapter1);

                    if (result.Failed)
                    {
                        if (result != HRESULT.DXGI_ERROR_NOT_FOUND)
                        {
                            result.ThrowOnFailure();
                        }
                        index = 0;
                    }
                    else
                    {
                        Debug.WriteLine($"Adapter {index}:");

                        DXGI_ADAPTER_DESC1 dxgiAdapterDesc = dxgiAdapter1.GetDesc1();

                        Debug.WriteLine($"\tDescription: {dxgiAdapterDesc.Description}");
                        const double gb = 1024 * 1024 * 1024;
                        Debug.WriteLine($"\tDedicatedVideoMemory: {dxgiAdapterDesc.DedicatedVideoMemory / gb}GB");
                        Debug.WriteLine($"\tSharedSystemMemory: {dxgiAdapterDesc.SharedSystemMemory / gb}GB");

                        if (selectedAdapter == null || dxgiAdapterDesc.DedicatedVideoMemory > maxDedicatedVideoMemory)
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
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return deviceId;
        }
    }
}
