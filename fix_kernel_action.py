import re

with open('FractalExplorer/Fractal.Compute/ILGPUFractalGenerator.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Add alias at the top
alias = """using System.Threading.Tasks;
using ILGPU;
using ILGPU.Runtime;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Fractal.Core.Exceptions;

using KernelAction = System.Action<
    ILGPU.Index1D,
    ILGPU.Runtime.ArrayView1D<double, ILGPU.Runtime.Stride1D.Dense>,
    ILGPU.Runtime.ArrayView1D<byte, ILGPU.Runtime.Stride1D.Dense>,
    ILGPU.Runtime.ArrayView1D<byte, ILGPU.Runtime.Stride1D.Dense>,
    Fractal.Core.Models.FractalParams>;
"""
content = content.replace("using System.Threading.Tasks;\nusing ILGPU;\nusing ILGPU.Runtime;\nusing Fractal.Core.Models;\nusing Fractal.Core.Services;\nusing Fractal.Core.Exceptions;", alias)

# Replace fields
fields_old = """    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> _mandelbrotKernel;
    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> _juliaKernel;
    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> _burningShipKernel;
    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> _tricornKernel;
    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> _celticKernel;
    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> _buffaloKernel;
    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> _multibrot3Kernel;"""

fields_new = """    private readonly KernelAction _mandelbrotKernel;
    private readonly KernelAction _juliaKernel;
    private readonly KernelAction _burningShipKernel;
    private readonly KernelAction _tricornKernel;
    private readonly KernelAction _celticKernel;
    private readonly KernelAction _buffaloKernel;
    private readonly KernelAction _multibrot3Kernel;"""
content = content.replace(fields_old, fields_new)

# Add factory method and replace loads
loads_old = """        _mandelbrotKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(MandelbrotKernel);
        _juliaKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(JuliaKernel);
        _burningShipKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(BurningShipKernel);
        _tricornKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(TricornKernel);
        _celticKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(CelticKernel);
        _buffaloKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(BuffaloKernel);
        _multibrot3Kernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(Multibrot3Kernel);"""

loads_new = """        _mandelbrotKernel = LoadKernel(MandelbrotKernel);
        _juliaKernel = LoadKernel(JuliaKernel);
        _burningShipKernel = LoadKernel(BurningShipKernel);
        _tricornKernel = LoadKernel(TricornKernel);
        _celticKernel = LoadKernel(CelticKernel);
        _buffaloKernel = LoadKernel(BuffaloKernel);
        _multibrot3Kernel = LoadKernel(Multibrot3Kernel);
    }

    private KernelAction LoadKernel(KernelAction kernelMethod)
    {
        return _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(kernelMethod);"""
content = content.replace(loads_old, loads_new)

# In GenerateAsync, update action type
local_action_old = """            Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> selectedKernel = settings.Type switch"""
local_action_new = """            KernelAction selectedKernel = settings.Type switch"""
content = content.replace(local_action_old, local_action_new)

with open('FractalExplorer/Fractal.Compute/ILGPUFractalGenerator.cs', 'w', encoding='utf-8') as f:
    f.write(content)
