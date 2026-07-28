import re

with open('FractalExplorer/Fractal.Compute/ILGPUFractalGenerator.cs', 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace("Stride1D.Dense", "ILGPU.Stride1D.Dense")

with open('FractalExplorer/Fractal.Compute/ILGPUFractalGenerator.cs', 'w', encoding='utf-8') as f:
    f.write(content)
