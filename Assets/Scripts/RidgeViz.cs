
using System;
using UnityEngine;

public class RidgeViz: MonoBehaviour
{
    public Terrain terrain;

    public bool show;
    
    private void DrawDebugCube(int i, int j)
    {
        var data = terrain.terrainData;
        int res = data.heightmapResolution;
        // TODO: refactor this to a common util
        var position = this.transform.position;
        float ballX = (((float)j)/res) * data.size.x + position.x;
        float ballY = (((float)i)/res) * data.size.z + position.z;
        float ballZ = data.GetHeight(j, i) + position.y;
        Gizmos.DrawCube(new Vector3(ballX, ballZ, ballY), new Vector3(5, 5, 5));
    }

    private void OnDrawGizmosSelected()
    {
        if (!show) return;   
        int res = terrain.terrainData.heightmapResolution;
        float[,] heightmap = terrain.terrainData.GetHeights(0, 0, res, res);
        for (int i = 0; i < res; i++)
        {
            for (int j = 0; j < res; j++)
            {
                if (IsRidge(i, j, heightmap))
                {
                    Gizmos.color = Color.magenta;
                    DrawDebugCube(i, j);
                }
            }
        }
    }

    private bool IsRidge(int x, int y, float[,] heightMap)
    {
        return CheckAllDirectionsForRidge(x, y, heightMap);
    }

    private bool CheckAllDirectionsForRidge(int x, int y, float[,] heightMap)
    {
        Vector2 curr = new Vector2(x, y);
        Vector2 up = new Vector2(0, -1) + curr;
        Vector2 down = new Vector2(0, 1) + curr;
        Vector2 left = new Vector2(-1, 0) + curr;
        Vector2 right = new Vector2(0, 1) + curr;
        Vector2 topLeft = new Vector2(-1, -1) + curr;
        Vector2 topRight = new Vector2(1, -1) + curr;
        Vector2 bottomLeft = new Vector2(-1, 1) + curr;
        Vector2 bottomRight = new Vector2(1, 1) + curr;


        float val = At(curr, heightMap);

        var ridge = (At(up, heightMap) < val 
                     && At(down, heightMap) < val
                     && AreDifferent(left, right, val, heightMap)
                     && AreDifferent(topRight, bottomLeft, val, heightMap)
                     && AreDifferent(topLeft, bottomRight, val, heightMap)
            );
        ridge = ridge || 
                (At(left, heightMap) < val 
                 && At(right, heightMap) < val
                 && AreDifferent(up, down, val, heightMap)
                 && AreDifferent(topRight, bottomLeft, val, heightMap)
                 && AreDifferent(topLeft, bottomRight, val, heightMap)
                 );
        ridge = ridge || 
                (At(topLeft, heightMap) < val 
                 && At(bottomRight, heightMap) < val
                 && AreDifferent(topRight, bottomLeft, val, heightMap)
                 && AreDifferent(up, down, val, heightMap)
                 );
        ridge = ridge || 
                (At(topRight, heightMap) < val 
                 && At(bottomLeft, heightMap) < val
                 && AreDifferent(topLeft, bottomRight, val, heightMap)
                 && AreDifferent(up, down, val, heightMap)
                 && AreDifferent(left, right, val, heightMap)
                 );

        return ridge;
    }

    private bool AreDifferent(Vector2 pos1, Vector2 pos2, float val, float[,] heightMap)
    {
        return (At(pos1, heightMap) < val && At(pos2, heightMap) > val
                || At(pos1, heightMap) > val && At(pos2, heightMap) < val);
    }

    private float At(Vector2 pos, float[,] heightMap)
    {
        int x = (int)pos.x;
        int y = (int)pos.y;
        if (!InBounds(x, y)) return Single.PositiveInfinity;
        return heightMap[x, y];
    }

    private bool InBounds(int x, int y)
    {
        int res = terrain.terrainData.heightmapResolution;
        return x >= 0 && x < res && y >= 0 && y < res;
    }
}