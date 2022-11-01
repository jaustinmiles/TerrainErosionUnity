using UnityEngine;

public class LocalMinMaxViz : MonoBehaviour
{
    public Terrain terrain;

    public bool show;

    private void OnDrawGizmosSelected()
    {
        if (!show) return;   
        int res = terrain.terrainData.heightmapResolution;
        float[,] heightmap = terrain.terrainData.GetHeights(0, 0, res, res);
        for (int i = 0; i < res; i++)
        {
            for (int j = 0; j < res; j++)
            {
                if (IsLocalExtrema(i, j, heightmap, false))
                {
                    Gizmos.color = Color.green;
                    DrawDebugCube(i, j);
                }

                if (IsLocalExtrema(i, j, heightmap, true))
                {
                    Gizmos.color = Color.red;
                    DrawDebugCube(i, j);
                }
            }
        }
        
    }

    private void DrawDebugCube(int i, int j)
    {
        var data = terrain.terrainData;
        int res = data.heightmapResolution;
        // TODO: refactor this to a common util
        var position = this.transform.position;
        float ballX = (((float)j)/res) * data.size.x + position.x;
        float ballY = (((float)i)/res) * data.size.z + position.z;
        float ballZ = data.GetHeight(j, i) + position.y;
        Gizmos.DrawCube(new Vector3(ballX, ballZ, ballY), new Vector3(10, 10, 10));
    }

    public bool IsLocalExtrema(int x, int y, float[,] heightMap, bool max)
    {
        float val = heightMap[x, y];
        bool localExtrema = true;
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                if (i == 0 && j == 0) continue;
                int ii = x + i;
                int jj = y + j;
                if (!InBounds(ii, jj)) continue;
                if (max)
                    localExtrema = localExtrema && heightMap[ii, jj] <= val;
                else
                    localExtrema = localExtrema && heightMap[ii, jj] >= val;
            }

        }

        return localExtrema;
    }

    private bool InBounds(int x, int y)
    {
        int res = terrain.terrainData.heightmapResolution;
        return x >= 0 && x < res && y >= 0 && y < res;
    }
}
