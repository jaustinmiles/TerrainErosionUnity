using System;

public class GaussianBlur
{
    private readonly float[,] _kernel;
    private readonly int _kernelWidth;

    public GaussianBlur(int kernelRadius)
    {
        float sigma = Math.Max(kernelRadius / 2, 1);
        int kernelWidth = 2 * kernelRadius + 1;
        _kernel = new float[kernelWidth, kernelWidth];
        _kernelWidth = kernelWidth;
        float total = 0f; 
        for (int i = -kernelRadius; i <= kernelRadius; i++)
        {
            for (int j = -kernelRadius; j <= kernelRadius; j++)
            {
                float g = Gaussian(i, j, sigma);
                _kernel[i + kernelRadius, j + kernelRadius] = g;
                total += g;
            }
        }
        for (int i = 0; i < kernelWidth; i++)
        {
            for (int j = 0; j < kernelWidth; j++)
            {
                _kernel[i, j]/= total;
            }
        }
    }


    private float Gaussian(int x, int y, float sigma)
    {
        double numerator = -1 * (x * x + y * y);
        double denominator = (2.0 * sigma * sigma);
        double expression = Math.Pow(Math.E, numerator / denominator);
        return (float)(expression / (2.0 * Math.PI * sigma * sigma));
    }

    public void Apply(ref float[,] inMap)
    {
        int width = inMap.GetLength(0);
        int height = inMap.GetLength(1);
        float[,] temp = new float[width, height];
        CopyTo(ref inMap, ref temp);
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                ApplyAt(ref inMap, ref temp, i, j);
            }
        }
    }

    private void ApplyAt(ref float[,] inMap, ref float[,] temp, int i, int j)
    {
        float total = 0f;
        int radius = (_kernelWidth - 1) / 2;
        float normalize = 1f;
        for (int ki = -radius; ki <= radius; ki++)
        {
            for (int kj = -radius; kj <= radius; kj++)
            {
                int ii = i - ki;
                int jj = j - kj;
                if (!CheckBounds(ref inMap, ii, jj))
                {
                    normalize -= _kernel[ki + radius, kj + radius];
                    continue;
                }
                total += _kernel[ki + radius, kj + radius] * temp[ii, jj];
            }
        }
        inMap[i, j] = total / normalize;
    }

    private bool CheckBounds(ref float[,] inMap, int i, int j)
    {
        if (i >= inMap.GetLength(0) || i < 0) return false;
        if (j >= inMap.GetLength(1) || j < 0) return false;
        return true;
    }

    private void CopyTo(ref float[,] in1, ref float[,] in2)
    {
        for (int i = 0; i < in1.GetLength(0); i++)
        {
            for (int j = 0; j < in1.GetLength(1); j++)
            {
                in2[i,j] = in1[i,j];
            }
        }
    }
}