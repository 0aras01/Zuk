import re

with open('FractalExplorer/Fractal.Compute/ILGPUFractalGenerator.cs', 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace("""using KernelAction = System.Action<
    ILGPU.Index1D,
    ILGPU.Runtime.ArrayView1D<double, Stride1D.Dense>,
    ILGPU.Runtime.ArrayView1D<byte, Stride1D.Dense>,
    ILGPU.Runtime.ArrayView1D<byte, Stride1D.Dense>,
    Fractal.Core.Models.FractalParams>;""", """using KernelAction = System.Action<
    ILGPU.Index1D,
    ILGPU.Runtime.ArrayView1D<double, ILGPU.Runtime.Stride1D.Dense>,
    ILGPU.Runtime.ArrayView1D<byte, ILGPU.Runtime.Stride1D.Dense>,
    ILGPU.Runtime.ArrayView1D<byte, ILGPU.Runtime.Stride1D.Dense>,
    Fractal.Core.Models.FractalParams>;""")

with open('FractalExplorer/Fractal.Compute/ILGPUFractalGenerator.cs', 'w', encoding='utf-8') as f:
    f.write(content)
